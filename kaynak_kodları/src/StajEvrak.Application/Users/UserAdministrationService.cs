using StajEvrak.Application.Abstractions;
using StajEvrak.Application.Auth;
using StajEvrak.Application.Common;
using StajEvrak.Domain.Applications;
using StajEvrak.Domain.Common;
using StajEvrak.Domain.Users;

namespace StajEvrak.Application.Users;

public sealed record UserDetails(User User, InternshipApplication? Application, IReadOnlyList<LoginAttempt> Attempts)
{
    public LoginAttempt? LastSuccessfulLogin => Attempts.FirstOrDefault(a => a.Success);
}

/// <summary>Admin'in düzenleyebildiği hesap bilgileri. Öğrenci no ve ad soyad yalnızca öğrenci hesaplarında kullanılır.</summary>
public sealed record UpdateAccountRequest(string? UserName, string? Email, string? StudentNo, string? FullName);

/// <summary>
/// Admin panelindeki hesap işlemleri: bilgileri düzenleme, şifre sıfırlama, kilit açma,
/// oturumları sonlandırma ve öğrenci hesabını (başvurusu ve belgeleriyle) silme.
/// Her işlem önce işlemi yapanın admin olduğunu doğrular.
/// </summary>
public sealed class UserAdministrationService(
    IUserRepository users,
    ILoginAttemptRepository attempts,
    IApplicationRepository applications,
    IDatasetRepository dataset,
    IPasswordService passwords,
    IFileStorage storage,
    IUnitOfWork unitOfWork)
{
    public Task<List<User>> SearchAsync(string? query, string? role, CancellationToken ct = default) =>
        users.SearchAsync(query, role, ct);

    public async Task<UserDetails?> GetDetailsAsync(int userId, CancellationToken ct = default)
    {
        var user = await users.FindByIdAsync(userId, ct);
        if (user is null) return null;
        if (user is Student) user = await users.GetStudentAsync(userId, ct); // okul kaydıyla birlikte
        var application = user is Student ? await applications.GetByStudentAsync(userId, ct) : null;
        return new UserDetails(user!, application, await attempts.ForUserAsync(userId, 20, ct));
    }

    public async Task<OperationResult> UpdateAccountAsync(int adminId, int userId, UpdateAccountRequest request, CancellationToken ct = default)
    {
        var (admin, user, error) = await LoadAsync(adminId, userId, ct);
        if (error is not null) return error;

        var userName = request.UserName?.Trim() ?? string.Empty;
        var email = request.Email?.Trim().ToLowerInvariant() ?? string.Empty;
        var studentNo = request.StudentNo?.Trim() ?? string.Empty;

        var formatError = AccountRules.ValidateUserName(userName)
            ?? AccountRules.ValidateEmail(email)
            ?? (user is Student ? AccountRules.ValidateStudentNo(studentNo) : null);
        if (formatError is not null) return OperationResult.Fail(formatError);

        if (await users.UserNameExistsAsync(userName, ct, user!.Id)) return OperationResult.Fail("Bu kullanıcı adı başka bir hesapta kullanılıyor.");
        if (await users.EmailExistsAsync(email, ct, user.Id)) return OperationResult.Fail("Bu e-posta adresi başka bir hesapta kullanılıyor.");

        try
        {
            user.ChangeUserName(userName);
            if (user is Student student)
            {
                if (await users.StudentNoExistsAsync(studentNo, ct, user.Id))
                    return OperationResult.Fail("Bu öğrenci numarası başka bir hesapta kullanılıyor.");

                var identityChanged = student.StudentNo != studentNo || student.Email != email;
                student.ChangeStudentNo(studentNo);
                student.ChangeFullName(request.FullName);
                student.ChangeEmail(email);

                // Öğrenci no veya e-posta değiştiyse okul kaydıyla eşleşme yeniden aranır.
                if (identityChanged)
                {
                    var match = await dataset.FindByStudentNoOrEmailAsync(studentNo, email, ct);
                    if (match is not null) student.LinkToDataset(match);
                    else student.UnlinkDataset();
                }
            }
            else
            {
                user.ChangeEmail(email);
            }
        }
        catch (DomainException ex)
        {
            return OperationResult.Fail(ex.Message);
        }

        await unitOfWork.SaveChangesAsync(ct);
        return OperationResult.Ok();
    }

    /// <summary>Yeni şifre belirler. Güvenlik damgası yenilendiği için kullanıcının açık oturumları kapanır.</summary>
    public async Task<OperationResult> ResetPasswordAsync(int adminId, int userId, string? newPassword, CancellationToken ct = default)
    {
        var (_, user, error) = await LoadAsync(adminId, userId, ct);
        if (error is not null) return error;

        var passwordError = AccountRules.ValidatePassword(newPassword);
        if (passwordError is not null) return OperationResult.Fail(passwordError);

        user!.SetPasswordHash(passwords.Hash(user, newPassword!));
        user.Unlock();
        await unitOfWork.SaveChangesAsync(ct);
        return OperationResult.Ok();
    }

    public async Task<OperationResult> UnlockAsync(int adminId, int userId, CancellationToken ct = default)
    {
        var (_, user, error) = await LoadAsync(adminId, userId, ct);
        if (error is not null) return error;
        user!.Unlock();
        await unitOfWork.SaveChangesAsync(ct);
        return OperationResult.Ok();
    }

    /// <summary>Kullanıcının tüm cihazlardaki oturumlarını kapatır (güvenlik damgası yenilenir).</summary>
    public async Task<OperationResult> EndSessionsAsync(int adminId, int userId, CancellationToken ct = default)
    {
        var (admin, user, error) = await LoadAsync(adminId, userId, ct);
        if (error is not null) return error;
        if (admin!.Id == user!.Id) return OperationResult.Fail("Kendi oturumunuzu kapatmak için çıkış yapın.");
        user.RenewSecurityStamp();
        await unitOfWork.SaveChangesAsync(ct);
        return OperationResult.Ok();
    }

    /// <summary>
    /// Öğrenci hesabını siler; başvurusu, tüm belge sürümleri (dosyalarıyla) ve Ek-16 bilgileri de silinir.
    /// Giriş denemesi kayıtları log olarak saklanır. Admin hesapları silinemez.
    /// </summary>
    public async Task<OperationResult> DeleteAsync(int adminId, int userId, CancellationToken ct = default)
    {
        var (admin, user, error) = await LoadAsync(adminId, userId, ct);
        if (error is not null) return error;
        if (!user!.CanBeDeletedBy(admin!))
            return OperationResult.Fail(user is Admin ? "Admin hesapları silinemez." : "Bu hesabı silme yetkiniz yok.");

        var application = await applications.GetByStudentAsync(user.Id, ct);
        var paths = application?.Documents.Select(d => d.StoredPath).ToList() ?? [];
        if (application is not null) applications.Remove(application);
        users.Remove(user);
        await unitOfWork.SaveChangesAsync(ct);
        foreach (var path in paths) storage.Delete(path);
        return OperationResult.Ok();
    }

    private async Task<(User? Admin, User? User, OperationResult? Error)> LoadAsync(int adminId, int userId, CancellationToken ct)
    {
        var admin = await users.FindByIdAsync(adminId, ct);
        if (admin is null || !admin.CanReviewApplications) return (null, null, OperationResult.Fail("Bu işlem için yetkiniz yok."));
        var user = userId == adminId ? admin : await users.FindByIdAsync(userId, ct);
        if (user is null) return (admin, null, OperationResult.Fail("Kullanıcı bulunamadı."));
        return (admin, user, null);
    }
}

using Microsoft.Extensions.Options;
using StajEvrak.Application.Abstractions;
using StajEvrak.Application.Common;
using StajEvrak.Domain.Users;

namespace StajEvrak.Application.Auth;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";
    public int MaxFailedAttempts { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;
}

public sealed record RegisterStudentRequest(string UserName, string Email, string StudentNo, string FullName, string Password);

public enum LoginStatus { Success, InvalidCredentials, LockedOut }

public sealed record LoginResult(LoginStatus Status, User? User, string? Message, DateTime? LockedUntil = null)
{
    public bool Succeeded => Status == LoginStatus.Success;
}

/// <summary>Kayıt, giriş (hatalı deneme sayacı + kilit + log), çıkış ve oturum damgası doğrulama.</summary>
public sealed class AuthService(
    IUserRepository users,
    ILoginAttemptRepository attempts,
    IDatasetRepository dataset,
    IPasswordService passwords,
    IUnitOfWork unitOfWork,
    IClock clock,
    IOptions<AuthOptions> options)
{
    private readonly AuthOptions _options = options.Value;

    public async Task<OperationResult<Student>> RegisterStudentAsync(RegisterStudentRequest request, CancellationToken ct = default)
    {
        var userName = request.UserName?.Trim() ?? string.Empty;
        var email = request.Email?.Trim().ToLowerInvariant() ?? string.Empty;
        var studentNo = request.StudentNo?.Trim() ?? string.Empty;

        var formatError = AccountRules.ValidateUserName(userName)
            ?? AccountRules.ValidateEmail(email)
            ?? AccountRules.ValidateStudentNo(studentNo)
            ?? AccountRules.ValidatePassword(request.Password);
        if (formatError is not null) return OperationResult<Student>.Fail(formatError);

        if (await users.UserNameExistsAsync(userName, ct)) return OperationResult<Student>.Fail("Bu kullanıcı adı zaten kullanılıyor.");
        if (await users.EmailExistsAsync(email, ct)) return OperationResult<Student>.Fail("Bu e-posta adresiyle kayıtlı bir hesap var.");
        if (await users.StudentNoExistsAsync(studentNo, ct)) return OperationResult<Student>.Fail("Bu öğrenci numarasıyla kayıtlı bir hesap var.");

        var student = new Student(userName, email, studentNo, request.FullName ?? string.Empty, clock.UtcNow);
        student.SetPasswordHash(passwords.Hash(student, request.Password));

        var match = await dataset.FindByStudentNoOrEmailAsync(studentNo, email, ct);
        if (match is not null) student.LinkToDataset(match);

        users.Add(student);
        await unitOfWork.SaveChangesAsync(ct);
        return OperationResult<Student>.Ok(student);
    }

    public async Task<LoginResult> LoginAsync(string identifier, string password, string? ipAddress, CancellationToken ct = default)
    {
        identifier = identifier?.Trim() ?? string.Empty;
        var now = clock.UtcNow;
        var lockout = TimeSpan.FromMinutes(_options.LockoutMinutes);

        // IP bazlı kilit: aynı IP'den kısa sürede çok sayıda hatalı deneme (var olmayan kullanıcı adları dahil).
        if (!string.IsNullOrEmpty(ipAddress))
        {
            var ipFailures = await attempts.CountFailuresFromIpSinceAsync(ipAddress, now - lockout, ct);
            if (ipFailures >= _options.MaxFailedAttempts * 2)
            {
                await LogAsync(identifier, ipAddress, false, "IP geçici olarak engellendi", null, ct);
                return new LoginResult(LoginStatus.LockedOut, null,
                    $"Çok sayıda hatalı deneme yapıldı. Lütfen {_options.LockoutMinutes} dakika sonra tekrar deneyin.");
            }
        }

        var user = await users.FindByUserNameOrEmailAsync(identifier, ct);
        if (user is null)
        {
            await LogAsync(identifier, ipAddress, false, "Kullanıcı bulunamadı", null, ct);
            return new LoginResult(LoginStatus.InvalidCredentials, null, "Kullanıcı adı/e-posta veya şifre hatalı.");
        }

        if (user.IsLockedOut(now))
        {
            await LogAsync(identifier, ipAddress, false, "Hesap kilitli", user.Id, ct);
            return LockedResult(user.LockoutEnd!.Value);
        }

        var check = passwords.Verify(user, password ?? string.Empty);
        if (!check.Success)
        {
            var lockedNow = user.RegisterFailedLogin(now, _options.MaxFailedAttempts, lockout);
            await LogAsync(identifier, ipAddress, false, lockedNow ? "Hatalı şifre - hesap kilitlendi" : "Hatalı şifre", user.Id, ct);
            if (lockedNow) return LockedResult(user.LockoutEnd!.Value);

            var remaining = _options.MaxFailedAttempts - user.FailedLoginCount;
            return new LoginResult(LoginStatus.InvalidCredentials, null,
                $"Kullanıcı adı/e-posta veya şifre hatalı. Hesap kilitlenmeden önce kalan deneme hakkı: {remaining}.");
        }

        user.RegisterSuccessfulLogin();
        if (check.NeedsRehash) user.SetPasswordHash(passwords.Hash(user, password!));
        await LogAsync(identifier, ipAddress, true, null, user.Id, ct);
        return new LoginResult(LoginStatus.Success, user, null);
    }

    /// <summary>Güvenlik damgasını yeniler; kullanıcının mevcut tüm oturum çerezleri geçersiz olur.</summary>
    public async Task LogoutAsync(int userId, CancellationToken ct = default)
    {
        var user = await users.FindByIdAsync(userId, ct);
        if (user is null) return;
        user.RenewSecurityStamp();
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<bool> IsSessionValidAsync(int userId, string? securityStamp, CancellationToken ct = default)
    {
        var user = await users.FindByIdAsync(userId, ct);
        return user is not null && securityStamp is not null && user.SecurityStamp == securityStamp && !user.IsLockedOut(clock.UtcNow);
    }

    public async Task<OperationResult> UnlockAsync(int userId, CancellationToken ct = default)
    {
        var user = await users.FindByIdAsync(userId, ct);
        if (user is null) return OperationResult.Fail("Kullanıcı bulunamadı.");
        user.Unlock();
        await unitOfWork.SaveChangesAsync(ct);
        return OperationResult.Ok();
    }

    public static string? ValidatePassword(string? password) => AccountRules.ValidatePassword(password);

    private LoginResult LockedResult(DateTime until)
    {
        var minutes = Math.Max(1, (int)Math.Ceiling((until - clock.UtcNow).TotalMinutes));
        return new LoginResult(LoginStatus.LockedOut, null,
            $"{_options.MaxFailedAttempts} hatalı deneme nedeniyle hesap kilitlendi. {minutes} dakika sonra tekrar deneyin veya admin ile iletişime geçin.",
            until);
    }

    private async Task LogAsync(string identifier, string? ip, bool success, string? reason, int? userId, CancellationToken ct)
    {
        attempts.Add(new LoginAttempt(identifier, ip, success, reason, clock.UtcNow, userId));
        await unitOfWork.SaveChangesAsync(ct);
    }
}

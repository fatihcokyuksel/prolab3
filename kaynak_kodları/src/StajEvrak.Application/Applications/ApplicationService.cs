using StajEvrak.Application.Abstractions;
using StajEvrak.Application.Common;
using StajEvrak.Application.Comparison;
using StajEvrak.Domain.Applications;
using StajEvrak.Domain.Common;
using StajEvrak.Domain.Users;

namespace StajEvrak.Application.Applications;

public sealed record ApplicationDetails(InternshipApplication Application, ComparisonReport? Report);

public sealed record DashboardStats(int StudentCount, int DatasetCount, IReadOnlyDictionary<ApplicationStatus, int> ByStatus)
{
    public int Count(ApplicationStatus status) => ByStatus.TryGetValue(status, out var c) ? c : 0;
    public int TotalApplications => ByStatus.Values.Sum();
}

/// <summary>Başvuruların listelenmesi, raporun hazırlanması ve admin statü işlemleri.</summary>
public sealed class ApplicationService(
    IApplicationRepository applications,
    IProfileRepository profiles,
    IUserRepository users,
    IDatasetRepository dataset,
    IFileStorage storage,
    ComparisonService comparison,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public Task<List<InternshipApplication>> ListAsync(ApplicationFilter filter, CancellationToken ct = default) =>
        applications.ListAsync(filter, ct);

    public async Task<DashboardStats> GetStatsAsync(CancellationToken ct = default)
    {
        var all = await users.ListAsync(ct);
        return new DashboardStats(all.OfType<Student>().Count(), await dataset.CountAsync(ct), await applications.CountByStatusAsync(ct));
    }

    public async Task<ApplicationDetails?> GetForStudentAsync(int studentId, CancellationToken ct = default)
    {
        var application = await applications.GetByStudentAsync(studentId, ct);
        return application is null ? null : await BuildDetailsAsync(application, ct);
    }

    public async Task<ApplicationDetails?> GetAsync(int viewerId, int applicationId, CancellationToken ct = default)
    {
        var viewer = await users.FindByIdAsync(viewerId, ct);
        var application = await applications.GetAsync(applicationId, ct);
        if (viewer is null || application is null || !viewer.CanAccess(application)) return null;
        return await BuildDetailsAsync(application, ct);
    }

    public Task<OperationResult> ApproveAsync(int adminId, int applicationId, string? note, CancellationToken ct = default) =>
        ReviewAsync(adminId, applicationId, (app, admin) => app.Approve(admin, note, clock.UtcNow), ct);

    public Task<OperationResult> MarkInvalidAsync(int adminId, int applicationId, string note, CancellationToken ct = default) =>
        ReviewAsync(adminId, applicationId, (app, admin) => app.MarkInvalid(admin, note, clock.UtcNow), ct);

    public Task<OperationResult> CompleteAsync(int adminId, int applicationId, CancellationToken ct = default) =>
        ReviewAsync(adminId, applicationId, (app, admin) => app.Complete(admin, clock.UtcNow), ct);

    /// <summary>Başvuruyu (tüm belge versiyonlarıyla) siler. Yalnızca admin; Tamamlandı statüsü dahil.</summary>
    public async Task<OperationResult> DeleteAsync(int adminId, int applicationId, CancellationToken ct = default)
    {
        var admin = await users.FindByIdAsync(adminId, ct);
        var application = await applications.GetAsync(applicationId, ct);
        if (admin is null || !admin.CanReviewApplications) return OperationResult.Fail("Bu işlem için yetkiniz yok.");
        if (application is null) return OperationResult.Fail("Başvuru bulunamadı.");

        var paths = application.Documents.Select(d => d.StoredPath).ToList();
        applications.Remove(application);
        await unitOfWork.SaveChangesAsync(ct);
        foreach (var path in paths) storage.Delete(path);
        return OperationResult.Ok();
    }

    private async Task<OperationResult> ReviewAsync(int adminId, int applicationId, Action<InternshipApplication, User> action, CancellationToken ct)
    {
        var admin = await users.FindByIdAsync(adminId, ct);
        var application = await applications.GetAsync(applicationId, ct);
        if (admin is null) return OperationResult.Fail("Kullanıcı bulunamadı.");
        if (application is null) return OperationResult.Fail("Başvuru bulunamadı.");
        try
        {
            action(application, admin);
            await unitOfWork.SaveChangesAsync(ct);
            return OperationResult.Ok();
        }
        catch (DomainException ex)
        {
            return OperationResult.Fail(ex.Message);
        }
    }

    private async Task<ApplicationDetails> BuildDetailsAsync(InternshipApplication application, CancellationToken ct)
    {
        var student = application.Student ?? await users.GetStudentAsync(application.StudentId, ct);
        var profile = await profiles.GetAsync(application.StudentId, ct);
        var document = application.LatestDocument;

        ComparisonReport? report = null;
        if (document is not null || profile is not null)
        {
            report = comparison.Build(
                document?.ToFieldValues(),
                document?.AutoReadSupported ?? false,
                document?.ReadNote,
                profile?.ToFieldValues(),
                student?.DatasetStudent?.ToFieldValues());
        }
        return new ApplicationDetails(application, report);
    }
}

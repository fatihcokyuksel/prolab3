using StajEvrak.Domain.Applications;
using StajEvrak.Domain.Dataset;
using StajEvrak.Domain.Profiles;
using StajEvrak.Domain.Users;

namespace StajEvrak.Application.Abstractions;

public interface IClock
{
    DateTime UtcNow { get; }
}

public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface IUserRepository
{
    Task<User?> FindByIdAsync(int id, CancellationToken ct = default);
    Task<User?> FindByUserNameOrEmailAsync(string identifier, CancellationToken ct = default);
    /// <param name="exceptUserId">Düzenlenen hesabın kendisi kontrol dışı bırakılır.</param>
    Task<bool> UserNameExistsAsync(string userName, CancellationToken ct = default, int? exceptUserId = null);
    Task<bool> EmailExistsAsync(string email, CancellationToken ct = default, int? exceptUserId = null);
    Task<bool> StudentNoExistsAsync(string studentNo, CancellationToken ct = default, int? exceptUserId = null);
    Task<Student?> GetStudentAsync(int id, CancellationToken ct = default);
    Task<List<Student>> GetUnlinkedStudentsAsync(CancellationToken ct = default);
    Task<List<User>> ListAsync(CancellationToken ct = default);
    Task<List<User>> SearchAsync(string? query, string? role, CancellationToken ct = default);
    void Add(User user);
    void Remove(User user);
}

public interface ILoginAttemptRepository
{
    void Add(LoginAttempt attempt);
    Task<int> CountFailuresFromIpSinceAsync(string ip, DateTime since, CancellationToken ct = default);
    Task<List<LoginAttempt>> RecentAsync(int take, CancellationToken ct = default);
    Task<List<LoginAttempt>> ForUserAsync(int userId, int take, CancellationToken ct = default);
}

public interface IDatasetRepository
{
    Task<DatasetStudent?> FindByStudentNoOrEmailAsync(string studentNo, string email, CancellationToken ct = default);
    Task<Dictionary<string, DatasetStudent>> GetByTcNumbersAsync(IEnumerable<string> tcNumbers, CancellationToken ct = default);
    Task<List<DatasetStudent>> SearchAsync(string? query, string? department, CancellationToken ct = default);
    Task<List<string>> GetDepartmentsAsync(CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default);
    void Add(DatasetStudent student);
}

public sealed record ApplicationFilter(ApplicationStatus? Status = null, string? Department = null, string? Query = null);

public interface IApplicationRepository
{
    Task<InternshipApplication?> GetByStudentAsync(int studentId, CancellationToken ct = default);
    Task<InternshipApplication?> GetAsync(int id, CancellationToken ct = default);
    Task<InternshipApplication?> GetByDocumentAsync(int documentId, CancellationToken ct = default);
    Task<List<InternshipApplication>> ListAsync(ApplicationFilter filter, CancellationToken ct = default);
    Task<Dictionary<ApplicationStatus, int>> CountByStatusAsync(CancellationToken ct = default);
    void Add(InternshipApplication application);
    void Remove(InternshipApplication application);
}

public interface IProfileRepository
{
    Task<StudentProfile?> GetAsync(int studentId, CancellationToken ct = default);
    void Add(StudentProfile profile);
}

/// <summary>Yüklenen dosyaların fiziksel saklanması.</summary>
public interface IFileStorage
{
    Task<string> SaveAsync(Stream content, string extension, string folder, CancellationToken ct = default);
    string GetFullPath(string storedPath);
    void Delete(string storedPath);
}

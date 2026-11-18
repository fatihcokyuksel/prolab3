using Microsoft.EntityFrameworkCore;
using StajEvrak.Application.Abstractions;
using StajEvrak.Domain.Applications;
using StajEvrak.Domain.Dataset;
using StajEvrak.Domain.Profiles;
using StajEvrak.Domain.Users;

namespace StajEvrak.Infrastructure.Data;

public sealed class UserRepository(AppDbContext db) : IUserRepository
{
    public Task<User?> FindByIdAsync(int id, CancellationToken ct = default) =>
        db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> FindByUserNameOrEmailAsync(string identifier, CancellationToken ct = default)
    {
        // Kültürden bağımsız küçük harf: tr-TR'de "I".ToLower() = "ı" olduğundan Invariant kullanılır.
        var key = identifier.Trim().ToLowerInvariant();
        return db.Users.FirstOrDefaultAsync(u => u.UserName.ToLower() == key || u.Email == key, ct);
    }

    public Task<bool> UserNameExistsAsync(string userName, CancellationToken ct = default, int? exceptUserId = null)
    {
        var key = userName.Trim().ToLowerInvariant();
        return db.Users.AnyAsync(u => u.UserName.ToLower() == key && u.Id != exceptUserId, ct);
    }

    public Task<bool> EmailExistsAsync(string email, CancellationToken ct = default, int? exceptUserId = null)
    {
        var key = email.Trim().ToLowerInvariant();
        return db.Users.AnyAsync(u => u.Email == key && u.Id != exceptUserId, ct);
    }

    public Task<bool> StudentNoExistsAsync(string studentNo, CancellationToken ct = default, int? exceptUserId = null) =>
        db.Students.AnyAsync(s => s.StudentNo == studentNo && s.Id != exceptUserId, ct);

    public Task<Student?> GetStudentAsync(int id, CancellationToken ct = default) =>
        db.Students.Include(s => s.DatasetStudent).FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<List<Student>> GetUnlinkedStudentsAsync(CancellationToken ct = default) =>
        db.Students.Where(s => s.DatasetStudentId == null).ToListAsync(ct);

    public Task<List<User>> ListAsync(CancellationToken ct = default) =>
        db.Users.OrderBy(u => u.UserName).ToListAsync(ct);

    public Task<List<User>> SearchAsync(string? query, string? role, CancellationToken ct = default)
    {
        var q = db.Users.AsQueryable();
        if (role == Roles.Student) q = q.Where(u => u is Student);
        else if (role == Roles.Admin) q = q.Where(u => u is Admin);
        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim().ToLowerInvariant();
            q = q.Where(u => u.UserName.ToLower().Contains(term) || u.Email.Contains(term) ||
                             (u is Student && (((Student)u).StudentNo.Contains(term) || ((Student)u).FullName.ToLower().Contains(term))));
        }
        return q.OrderBy(u => u.UserName).ToListAsync(ct);
    }

    public void Add(User user) => db.Users.Add(user);

    public void Remove(User user) => db.Users.Remove(user);
}

public sealed class LoginAttemptRepository(AppDbContext db) : ILoginAttemptRepository
{
    public void Add(LoginAttempt attempt) => db.LoginAttempts.Add(attempt);

    public Task<int> CountFailuresFromIpSinceAsync(string ip, DateTime since, CancellationToken ct = default) =>
        db.LoginAttempts.CountAsync(a => a.IpAddress == ip && !a.Success && a.Time >= since, ct);

    public Task<List<LoginAttempt>> RecentAsync(int take, CancellationToken ct = default) =>
        db.LoginAttempts.OrderByDescending(a => a.Time).Take(take).ToListAsync(ct);

    public Task<List<LoginAttempt>> ForUserAsync(int userId, int take, CancellationToken ct = default) =>
        db.LoginAttempts.Where(a => a.UserId == userId).OrderByDescending(a => a.Time).Take(take).ToListAsync(ct);
}

public sealed class DatasetRepository(AppDbContext db) : IDatasetRepository
{
    public Task<DatasetStudent?> FindByStudentNoOrEmailAsync(string studentNo, string email, CancellationToken ct = default)
    {
        var mail = email.Trim().ToLowerInvariant();
        return db.DatasetStudents.FirstOrDefaultAsync(d => d.OgrenciNo == studentNo.Trim() || (mail != "" && d.Email == mail), ct);
    }

    public async Task<Dictionary<string, DatasetStudent>> GetByTcNumbersAsync(IEnumerable<string> tcNumbers, CancellationToken ct = default)
    {
        var list = tcNumbers.Distinct().ToList();
        return await db.DatasetStudents.Where(d => list.Contains(d.TcKimlikNo)).ToDictionaryAsync(d => d.TcKimlikNo, ct);
    }

    public Task<List<DatasetStudent>> SearchAsync(string? query, string? department, CancellationToken ct = default)
    {
        var q = db.DatasetStudents.AsQueryable();
        if (!string.IsNullOrWhiteSpace(department)) q = q.Where(d => d.Bolum == department);
        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim();
            q = q.Where(d => d.AdSoyad.Contains(term) || d.TcKimlikNo.Contains(term) || d.OgrenciNo.Contains(term) || d.Email.Contains(term));
        }
        return q.OrderBy(d => d.AdSoyad).ToListAsync(ct);
    }

    public Task<List<string>> GetDepartmentsAsync(CancellationToken ct = default) =>
        db.DatasetStudents.Where(d => d.Bolum != "").Select(d => d.Bolum).Distinct().OrderBy(b => b).ToListAsync(ct);

    public Task<int> CountAsync(CancellationToken ct = default) => db.DatasetStudents.CountAsync(ct);

    public void Add(DatasetStudent student) => db.DatasetStudents.Add(student);
}

public sealed class ApplicationRepository(AppDbContext db) : IApplicationRepository
{
    private IQueryable<InternshipApplication> WithDetails() => db.Applications
        .Include(a => a.Student).ThenInclude(s => s.DatasetStudent)
        .Include(a => a.Documents).ThenInclude(d => d.ExtractedFields)
        .AsSplitQuery();

    public Task<InternshipApplication?> GetByStudentAsync(int studentId, CancellationToken ct = default) =>
        WithDetails().FirstOrDefaultAsync(a => a.StudentId == studentId, ct);

    public Task<InternshipApplication?> GetAsync(int id, CancellationToken ct = default) =>
        WithDetails().FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<InternshipApplication?> GetByDocumentAsync(int documentId, CancellationToken ct = default) =>
        WithDetails().FirstOrDefaultAsync(a => a.Documents.Any(d => d.Id == documentId), ct);

    public Task<List<InternshipApplication>> ListAsync(ApplicationFilter filter, CancellationToken ct = default)
    {
        var q = db.Applications
            .Include(a => a.Student).ThenInclude(s => s.DatasetStudent)
            .Include(a => a.Documents)
            .AsSplitQuery()
            .AsQueryable();

        if (filter.Status is { } status) q = q.Where(a => a.Status == status);
        if (!string.IsNullOrWhiteSpace(filter.Department))
            q = q.Where(a => a.Student.DatasetStudent != null && a.Student.DatasetStudent.Bolum == filter.Department);
        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            var term = filter.Query.Trim();
            q = q.Where(a => a.Student.UserName.Contains(term) || a.Student.StudentNo.Contains(term) ||
                             a.Student.FullName.Contains(term) || a.Student.Email.Contains(term));
        }
        return q.OrderByDescending(a => a.UpdatedAt).ToListAsync(ct);
    }

    public async Task<Dictionary<ApplicationStatus, int>> CountByStatusAsync(CancellationToken ct = default) =>
        await db.Applications.GroupBy(a => a.Status).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

    public void Add(InternshipApplication application) => db.Applications.Add(application);

    public void Remove(InternshipApplication application) => db.Applications.Remove(application);
}

public sealed class ProfileRepository(AppDbContext db) : IProfileRepository
{
    public Task<StudentProfile?> GetAsync(int studentId, CancellationToken ct = default) =>
        db.StudentProfiles.Include(p => p.Values).FirstOrDefaultAsync(p => p.StudentId == studentId, ct);

    public void Add(StudentProfile profile) => db.StudentProfiles.Add(profile);
}

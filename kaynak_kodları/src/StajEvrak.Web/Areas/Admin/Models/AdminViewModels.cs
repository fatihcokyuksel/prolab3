using StajEvrak.Application.Abstractions;
using StajEvrak.Application.Applications;
using StajEvrak.Application.Dataset;
using StajEvrak.Application.Users;
using StajEvrak.Domain.Applications;
using StajEvrak.Domain.Dataset;
using StajEvrak.Domain.Users;

namespace StajEvrak.Web.Areas.Admin.Models;

public class AdminHomeViewModel
{
    public DashboardStats Stats { get; init; } = null!;
    public List<InternshipApplication> Queue { get; init; } = [];
    public List<InternshipApplication> Recent { get; init; } = [];
}

public class DatasetIndexViewModel
{
    public List<DatasetStudent> Students { get; init; } = [];
    public List<string> Departments { get; init; } = [];
    public string? Query { get; init; }
    public string? Department { get; init; }
    public DatasetImportResult? ImportResult { get; init; }
    public IReadOnlyCollection<string> SupportedExtensions { get; init; } = [];
}

public class ApplicationsIndexViewModel
{
    public List<InternshipApplication> Applications { get; init; } = [];
    public ApplicationFilter Filter { get; init; } = new();
    public List<string> Departments { get; init; } = [];
    public DashboardStats Stats { get; init; } = null!;
    public bool CompletedTab { get; init; }
}

public class UsersIndexViewModel
{
    public List<User> Users { get; init; } = [];
    public List<LoginAttempt> RecentAttempts { get; init; } = [];
    public DateTime Now { get; init; }
    public string? Query { get; init; }
    public string? Role { get; init; }
    public int StudentCount { get; init; }
    public int AdminCount { get; init; }
}

public class UserDetailsViewModel
{
    public UserDetails Details { get; init; } = null!;
    public DateTime Now { get; init; }
    public bool IsSelf { get; init; }
    public bool CanDelete { get; init; }
    public User User => Details.User;
    public Domain.Users.Student? Student => Details.User as Domain.Users.Student;
}

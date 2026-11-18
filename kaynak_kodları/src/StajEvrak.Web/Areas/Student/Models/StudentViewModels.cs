using StajEvrak.Application.Applications;
using StajEvrak.Domain.Applications;

namespace StajEvrak.Web.Areas.Student.Models;

public class StudentHomeViewModel
{
    public Domain.Users.Student Student { get; init; } = null!;
    public ApplicationDetails? Details { get; init; }
    public bool ProfileFilled { get; init; }
}

public class ProfileViewModel
{
    public Dictionary<string, string?> Values { get; init; } = [];
    public bool CanEdit { get; init; }
    public IReadOnlyDictionary<string, string> Errors { get; init; } = new Dictionary<string, string>();

    public string? Get(string code) => Values.TryGetValue(code, out var v) ? v : null;
    public string? Error(string code) => Errors.TryGetValue(code, out var e) ? e : null;
}

public class DocumentsViewModel
{
    public InternshipApplication? Application { get; init; }
    public bool CanUpload { get; init; }
    public IReadOnlyCollection<string> AllowedExtensions { get; init; } = [];
}

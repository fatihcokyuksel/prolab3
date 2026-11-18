using StajEvrak.Domain.Applications.States;
using StajEvrak.Domain.Common;
using StajEvrak.Domain.Users;

namespace StajEvrak.Domain.Applications;

/// <summary>
/// Öğrencinin staj başvurusu (Ek-16 evrakı). Statü yalnızca davranış metotlarıyla değişir;
/// her geçiş mevcut <see cref="ApplicationState"/> nesnesine sorulur.
/// </summary>
public class InternshipApplication : Entity
{
    private readonly List<UploadedDocument> _documents = [];

    private InternshipApplication() { }

    public InternshipApplication(int studentId, DateTime now)
    {
        StudentId = studentId;
        Status = ApplicationStatus.AwaitingDocument;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public int StudentId { get; private set; }
    public Student Student { get; private set; } = null!;
    public ApplicationStatus Status { get; private set; }
    public string? ReviewNote { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public IReadOnlyCollection<UploadedDocument> Documents => _documents.AsReadOnly();

    public ApplicationState State => ApplicationState.For(Status);

    public UploadedDocument? LatestDocument => _documents.OrderByDescending(d => d.Version).FirstOrDefault();

    public bool CanBeModifiedBy(User user) => user.CanAccess(this) && State.CanBeModifiedBy(user);

    public UploadedDocument AddDocument(User by, string originalFileName, string storedPath, string extension, long sizeBytes, DateTime now)
    {
        EnsureCanModify(by);
        var next = State.OnUpload();
        var version = _documents.Count == 0 ? 1 : _documents.Max(d => d.Version) + 1;
        var document = new UploadedDocument(version, originalFileName, storedPath, extension, sizeBytes, now);
        _documents.Add(document);
        ReviewNote = null;
        ChangeStatus(next, now);
        return document;
    }

    public void RemoveDocument(User by, int documentId, DateTime now)
    {
        EnsureCanModify(by);
        var document = _documents.FirstOrDefault(d => d.Id == documentId)
                       ?? throw new DomainException("Belge bulunamadı.");
        _documents.Remove(document);
        if (_documents.Count == 0) ChangeStatus(ApplicationStatus.AwaitingDocument, now);
        else UpdatedAt = now;
    }

    public void StartReview(DateTime now) => ChangeStatus(State.OnStartReview(), now);

    public void Approve(User by, string? note, DateTime now)
    {
        EnsureReviewer(by);
        var next = State.OnApprove();
        ReviewNote = note;
        ChangeStatus(next, now);
    }

    public void MarkInvalid(User by, string note, DateTime now)
    {
        EnsureReviewer(by);
        if (string.IsNullOrWhiteSpace(note)) throw new DomainException("Hatalı/eksik işaretlemek için açıklama girilmelidir.");
        var next = State.OnMarkInvalid();
        ReviewNote = note.Trim();
        ChangeStatus(next, now);
    }

    public void Complete(User by, DateTime now)
    {
        EnsureReviewer(by);
        ChangeStatus(State.OnComplete(), now);
    }

    private void ChangeStatus(ApplicationStatus next, DateTime now)
    {
        Status = next;
        UpdatedAt = now;
    }

    private void EnsureCanModify(User user)
    {
        if (!CanBeModifiedBy(user))
            throw new DomainException($"'{State.DisplayName}' statüsündeki başvuru üzerinde değişiklik yapma yetkiniz yok.");
    }

    private static void EnsureReviewer(User user)
    {
        if (!user.CanReviewApplications)
            throw new DomainException("Bu işlem yalnızca admin tarafından yapılabilir.");
    }
}

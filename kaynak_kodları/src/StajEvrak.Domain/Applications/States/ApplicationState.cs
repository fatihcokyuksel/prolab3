using StajEvrak.Domain.Common;
using StajEvrak.Domain.Users;

namespace StajEvrak.Domain.Applications.States;

/// <summary>
/// State tasarım deseni: Her statü kendi geçiş kurallarını ve yetki davranışını tanımlar (çok biçimlilik).
/// Varsayılan olarak hiçbir geçişe izin verilmez; alt sınıflar yalnızca geçerli geçişleri override eder.
/// </summary>
public abstract class ApplicationState
{
    public abstract ApplicationStatus Status { get; }
    public abstract string DisplayName { get; }

    /// <summary>Arayüzdeki statü rengi için CSS sınıfı.</summary>
    public abstract string BadgeCss { get; }

    /// <summary>Bu statüdeki başvuru (belge yükleme/silme) verilen kullanıcı tarafından değiştirilebilir mi?</summary>
    public virtual bool CanBeModifiedBy(User user) => true;

    public virtual ApplicationStatus OnUpload() => throw Denied("yeni belge yüklenemez");
    public virtual ApplicationStatus OnStartReview() => throw Denied("incelemeye alınamaz");
    public virtual ApplicationStatus OnApprove() => throw Denied("onaylanamaz");
    public virtual ApplicationStatus OnMarkInvalid() => throw Denied("hatalı/eksik olarak işaretlenemez");
    public virtual ApplicationStatus OnComplete() => throw Denied("tamamlanamaz");

    protected DomainException Denied(string action) =>
        new($"'{DisplayName}' statüsündeki başvuru {action}.");

    private static readonly Dictionary<ApplicationStatus, ApplicationState> States = new()
    {
        [ApplicationStatus.AwaitingDocument] = new AwaitingDocumentState(),
        [ApplicationStatus.Uploaded] = new UploadedState(),
        [ApplicationStatus.Reviewing] = new ReviewingState(),
        [ApplicationStatus.Invalid] = new InvalidState(),
        [ApplicationStatus.Approved] = new ApprovedState(),
        [ApplicationStatus.Completed] = new CompletedState(),
    };

    public static ApplicationState For(ApplicationStatus status) => States[status];

    public static IEnumerable<ApplicationState> AllStates => States.Values;
}

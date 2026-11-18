using StajEvrak.Domain.Users;

namespace StajEvrak.Domain.Applications.States;

public sealed class AwaitingDocumentState : ApplicationState
{
    public override ApplicationStatus Status => ApplicationStatus.AwaitingDocument;
    public override string DisplayName => "Evrak bekleniyor";
    public override string BadgeCss => "st-awaiting";
    public override ApplicationStatus OnUpload() => ApplicationStatus.Uploaded;
}

public sealed class UploadedState : ApplicationState
{
    public override ApplicationStatus Status => ApplicationStatus.Uploaded;
    public override string DisplayName => "Yüklendi";
    public override string BadgeCss => "st-uploaded";
    public override ApplicationStatus OnUpload() => ApplicationStatus.Uploaded;
    public override ApplicationStatus OnStartReview() => ApplicationStatus.Reviewing;
}

public sealed class ReviewingState : ApplicationState
{
    public override ApplicationStatus Status => ApplicationStatus.Reviewing;
    public override string DisplayName => "İnceleniyor";
    public override string BadgeCss => "st-reviewing";
    public override ApplicationStatus OnUpload() => ApplicationStatus.Uploaded;
    public override ApplicationStatus OnApprove() => ApplicationStatus.Approved;
    public override ApplicationStatus OnMarkInvalid() => ApplicationStatus.Invalid;
}

public sealed class InvalidState : ApplicationState
{
    public override ApplicationStatus Status => ApplicationStatus.Invalid;
    public override string DisplayName => "Hatalı / eksik";
    public override string BadgeCss => "st-invalid";

    // Düzeltilip yeniden yüklenir.
    public override ApplicationStatus OnUpload() => ApplicationStatus.Uploaded;
    public override ApplicationStatus OnApprove() => ApplicationStatus.Approved;
}

public sealed class ApprovedState : ApplicationState
{
    public override ApplicationStatus Status => ApplicationStatus.Approved;
    public override string DisplayName => "Onaylandı";
    public override string BadgeCss => "st-approved";
    public override ApplicationStatus OnMarkInvalid() => ApplicationStatus.Invalid;
    public override ApplicationStatus OnComplete() => ApplicationStatus.Completed;

    // Onaylanmış belge öğrenci tarafından değiştirilemez; admin geri alabilir.
    public override bool CanBeModifiedBy(User user) => user.CanReviewApplications;
}

/// <summary>
/// Tamamlandı: öğrenci hiçbir şekilde değiştiremez/silemez; yalnızca admin güncelleyip silebilir.
/// </summary>
public sealed class CompletedState : ApplicationState
{
    public override ApplicationStatus Status => ApplicationStatus.Completed;
    public override string DisplayName => "Tamamlandı";
    public override string BadgeCss => "st-completed";
    public override bool CanBeModifiedBy(User user) => user.CanReviewApplications;

    // Admin gerekirse tamamlanmış başvuruyu yeniden açabilir.
    public override ApplicationStatus OnMarkInvalid() => ApplicationStatus.Invalid;
}

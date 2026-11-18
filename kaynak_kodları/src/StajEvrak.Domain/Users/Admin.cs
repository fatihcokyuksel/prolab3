using StajEvrak.Domain.Applications;

namespace StajEvrak.Domain.Users;

public class Admin : User
{
    private Admin() { }

    public Admin(string userName, string email, DateTime createdAt) : base(userName, email, createdAt) { }

    public override string Role => Roles.Admin;
    public override string HomePath => "/Admin";
    public override bool CanReviewApplications => true;

    // Admin tüm başvurulara erişebilir.
    public override bool CanAccess(InternshipApplication application) => true;

    // Sistemin yöneticisiz kalmaması için admin hesapları panelden silinemez.
    public override bool CanBeDeletedBy(User actor) => false;
}

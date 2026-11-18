using StajEvrak.Domain.Applications;
using StajEvrak.Domain.Common;

namespace StajEvrak.Domain.Users;

/// <summary>
/// Sisteme giriş yapabilen tüm kullanıcıların soyut atası.
/// Şifre, kilit ve güvenlik damgası alanları dışarıdan doğrudan değiştirilemez (kapsülleme);
/// yalnızca anlamlı davranış metotlarıyla güncellenir.
/// </summary>
public abstract class User : Entity
{
    protected User() { }

    protected User(string userName, string email, DateTime createdAt)
    {
        if (string.IsNullOrWhiteSpace(userName)) throw new DomainException("Kullanıcı adı boş olamaz.");
        if (string.IsNullOrWhiteSpace(email)) throw new DomainException("E-posta boş olamaz.");
        UserName = userName.Trim();
        Email = email.Trim().ToLowerInvariant();
        CreatedAt = createdAt;
        RenewSecurityStamp();
    }

    public string UserName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public int FailedLoginCount { get; private set; }
    public DateTime? LockoutEnd { get; private set; }
    public string SecurityStamp { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }

    /// <summary>Cookie'ye yazılan rol adı (ROLE_STUDENT / ROLE_ADMIN).</summary>
    public abstract string Role { get; }

    /// <summary>Giriş sonrası yönlendirilecek ana sayfa.</summary>
    public abstract string HomePath { get; }

    /// <summary>Kullanıcı başvuruları inceleyip statüsünü değiştirebilir mi?</summary>
    public virtual bool CanReviewApplications => false;

    /// <summary>Kullanıcı verilen başvuruyu görebilir mi? Alt sınıflar kendi kuralını uygular.</summary>
    public abstract bool CanAccess(InternshipApplication application);

    /// <summary>Hesap verilen kullanıcı tarafından silinebilir mi? Alt sınıflar kendi kuralını uygular.</summary>
    public abstract bool CanBeDeletedBy(User actor);

    public void ChangeUserName(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName)) throw new DomainException("Kullanıcı adı boş olamaz.");
        UserName = userName.Trim();
    }

    public void ChangeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) throw new DomainException("E-posta boş olamaz.");
        Email = email.Trim().ToLowerInvariant();
    }

    public void SetPasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash)) throw new DomainException("Şifre özeti boş olamaz.");
        PasswordHash = passwordHash;
        RenewSecurityStamp();
    }

    public bool IsLockedOut(DateTime now) => LockoutEnd.HasValue && LockoutEnd.Value > now;

    /// <summary>Hatalı girişi sayar; eşik aşılırsa hesabı kilitler. Kilitlendiyse true döner.</summary>
    public bool RegisterFailedLogin(DateTime now, int maxAttempts, TimeSpan lockoutDuration)
    {
        FailedLoginCount++;
        if (FailedLoginCount < maxAttempts) return false;
        LockoutEnd = now.Add(lockoutDuration);
        FailedLoginCount = 0;
        return true;
    }

    public void RegisterSuccessfulLogin()
    {
        FailedLoginCount = 0;
        LockoutEnd = null;
    }

    public void Unlock() => RegisterSuccessfulLogin();

    /// <summary>Damgayı yeniler; eski oturum çerezleri geçersiz hale gelir.</summary>
    public void RenewSecurityStamp() => SecurityStamp = Guid.NewGuid().ToString("N");
}

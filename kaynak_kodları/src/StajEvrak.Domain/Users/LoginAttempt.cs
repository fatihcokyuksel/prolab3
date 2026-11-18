using StajEvrak.Domain.Common;

namespace StajEvrak.Domain.Users;

/// <summary>Başarılı/başarısız her giriş denemesinin kaydı (log gereksinimi).</summary>
public class LoginAttempt : Entity
{
    private LoginAttempt() { }

    public LoginAttempt(string identifier, string? ipAddress, bool success, string? reason, DateTime time, int? userId)
    {
        Identifier = identifier;
        IpAddress = ipAddress;
        Success = success;
        Reason = reason;
        Time = time;
        UserId = userId;
    }

    public string Identifier { get; private set; } = string.Empty;
    public string? IpAddress { get; private set; }
    public bool Success { get; private set; }
    public string? Reason { get; private set; }
    public DateTime Time { get; private set; }
    public int? UserId { get; private set; }
}

using Microsoft.AspNetCore.Identity;
using StajEvrak.Domain.Users;

namespace StajEvrak.Application.Auth;

public interface IPasswordService
{
    string Hash(User user, string password);
    PasswordCheck Verify(User user, string password);
}

public sealed record PasswordCheck(bool Success, bool NeedsRehash);

/// <summary>
/// Şifreleri ASP.NET Core Identity'nin PBKDF2 (HMAC-SHA512, 100.000 iterasyon, rastgele salt) hasher'ı ile saklar.
/// Veritabanında açık şifre tutulmaz.
/// </summary>
public sealed class PasswordService : IPasswordService
{
    private readonly PasswordHasher<User> _hasher = new();

    public string Hash(User user, string password) => _hasher.HashPassword(user, password);

    public PasswordCheck Verify(User user, string password)
    {
        if (string.IsNullOrEmpty(user.PasswordHash)) return new PasswordCheck(false, false);
        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        return result switch
        {
            PasswordVerificationResult.Success => new PasswordCheck(true, false),
            PasswordVerificationResult.SuccessRehashNeeded => new PasswordCheck(true, true),
            _ => new PasswordCheck(false, false)
        };
    }
}

using System.Text.RegularExpressions;

namespace StajEvrak.Application.Auth;

/// <summary>Hesap bilgilerinin biçim kuralları. Kayıt ekranı ve admin düzenleme ekranı aynı kuralları kullanır.</summary>
public static partial class AccountRules
{
    public static string? ValidateUserName(string? userName) =>
        UserNameRegex().IsMatch(userName ?? string.Empty)
            ? null
            : "Kullanıcı adı 3-30 karakter olmalı; yalnızca harf, rakam, nokta, alt çizgi ve tire içerebilir.";

    public static string? ValidateEmail(string? email) =>
        email is { Length: <= 200 } && EmailRegex().IsMatch(email)
            ? null
            : "Geçerli bir e-posta adresi giriniz (örn. ad@alanadi.uzanti).";

    public static string? ValidateStudentNo(string? studentNo) =>
        StudentNoRegex().IsMatch(studentNo ?? string.Empty)
            ? null
            : "Öğrenci numarası yalnızca rakamlardan oluşmalıdır (5-15 hane).";

    public static string? ValidatePassword(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 8)
            return "Şifre en az 8 karakter olmalıdır.";
        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
            return "Şifre en az bir harf ve bir rakam içermelidir.";
        return null;
    }

    [GeneratedRegex(@"^[A-Za-z0-9._-]{3,30}$")]
    private static partial Regex UserNameRegex();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[A-Za-z]{2,}$")]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"^\d{5,15}$")]
    private static partial Regex StudentNoRegex();
}

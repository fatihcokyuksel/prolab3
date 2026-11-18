using StajEvrak.Domain.Common;
using StajEvrak.Domain.Ek16;

namespace StajEvrak.Domain.Profiles;

/// <summary>
/// Öğrencinin arayüzden elle girdiği Ek-16 bilgileri.
/// Değerler alan kodu → değer biçiminde tutulur; yalnızca katalogda tanımlı alanlar kabul edilir.
/// </summary>
public class StudentProfile : Entity
{
    private readonly List<ProfileFieldValue> _values = [];

    private StudentProfile() { }

    public StudentProfile(int studentId, DateTime now)
    {
        StudentId = studentId;
        UpdatedAt = now;
    }

    public int StudentId { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public IReadOnlyCollection<ProfileFieldValue> Values => _values.AsReadOnly();

    public string? GetValue(string code) =>
        _values.FirstOrDefault(v => string.Equals(v.FieldCode, code, StringComparison.OrdinalIgnoreCase))?.Value;

    public void SetValues(IReadOnlyDictionary<string, string?> values, DateTime now)
    {
        foreach (var (code, value) in values)
        {
            if (!Ek16Fields.IsKnown(code)) throw new DomainException($"Bilinmeyen alan: {code}");
            var existing = _values.FirstOrDefault(v => v.FieldCode == code);
            var clean = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            if (existing is null) _values.Add(new ProfileFieldValue(code, clean));
            else existing.ChangeValue(clean);
        }
        UpdatedAt = now;
    }

    public IReadOnlyDictionary<string, string?> ToFieldValues() =>
        _values.ToDictionary(v => v.FieldCode, v => v.Value);
}

public class ProfileFieldValue : Entity
{
    private ProfileFieldValue() { }

    public ProfileFieldValue(string fieldCode, string? value)
    {
        FieldCode = fieldCode;
        Value = value;
    }

    public string FieldCode { get; private set; } = string.Empty;
    public string? Value { get; private set; }

    internal void ChangeValue(string? value) => Value = value;
}

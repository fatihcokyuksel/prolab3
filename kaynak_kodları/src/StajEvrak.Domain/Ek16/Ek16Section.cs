namespace StajEvrak.Domain.Ek16;

public enum Ek16Section
{
    Student = 1,   // ÖĞRENCİNİN
    Company = 2,   // STAJ YAPILAN YERİN
    Employer = 3   // İŞVEREN VEYA YETKİLİNİN
}

public enum FieldKind
{
    Text,
    Number,
    NationalId,   // T.C. kimlik / yabancı kimlik no (11 hane)
    Date,
    Email,
    Phone,
    YesNo,
    Choice,
    AcademicYear
}

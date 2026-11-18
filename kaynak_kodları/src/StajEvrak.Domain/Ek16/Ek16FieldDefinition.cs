namespace StajEvrak.Domain.Ek16;

/// <summary>
/// Ek-16 formundaki tek bir alanın tanımı.
/// <paramref name="DocxLabel"/>: Word belgesinde değerin solundaki etiket hücresinin başlangıcı.
/// <paramref name="DocxPart"/>: Aynı hücrede birden çok değer varsa (ör. "TC / Uyruk") kaçıncı parça olduğu.
/// </summary>
public sealed record Ek16FieldDefinition(
    string Code,
    string Label,
    Ek16Section Section,
    FieldKind Kind,
    string DocxLabel,
    int? DocxPart = null,
    bool Required = true);

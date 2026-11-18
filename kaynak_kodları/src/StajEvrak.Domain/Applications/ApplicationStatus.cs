namespace StajEvrak.Domain.Applications;

/// <summary>Belge statü akışı (Bölüm 5): Yüklendi → İnceleniyor → Hatalı/Eksik ↺ → Onaylandı → Tamamlandı.</summary>
public enum ApplicationStatus
{
    AwaitingDocument = 0,
    Uploaded = 1,
    Reviewing = 2,
    Invalid = 3,
    Approved = 4,
    Completed = 5
}

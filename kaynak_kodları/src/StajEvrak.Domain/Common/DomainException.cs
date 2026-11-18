namespace StajEvrak.Domain.Common;

/// <summary>İş kuralı ihlallerinde fırlatılır; arayüzde kullanıcıya mesaj olarak gösterilir.</summary>
public class DomainException(string message) : Exception(message);

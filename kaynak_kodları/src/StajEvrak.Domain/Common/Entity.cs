namespace StajEvrak.Domain.Common;

/// <summary>Tüm kalıcı varlıkların ortak atası (soyutlama).</summary>
public abstract class Entity
{
    public int Id { get; protected set; }
}

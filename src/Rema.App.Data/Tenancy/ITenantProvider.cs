namespace Rema.App.Data.Tenancy;

/// <summary>
/// Leverer id på den butik den aktuelle bruger er logget ind i.
/// Implementeres i web-laget ud fra brugerens claims.
/// </summary>
public interface ITenantProvider
{
    /// <summary>
    /// Den aktuelle butiks id, eller <see cref="Guid.Empty"/> hvis der ikke er
    /// en butikskontekst (fx ved design-time, baggrundsjob eller anonyme sider).
    /// </summary>
    Guid StoreId { get; }
}

/// <summary>Fast tenant – brugbar til seeding, tests og design-time.</summary>
public sealed class FixedTenantProvider(Guid storeId) : ITenantProvider
{
    public Guid StoreId { get; } = storeId;
}

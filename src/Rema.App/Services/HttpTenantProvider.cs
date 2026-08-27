using Rema.App.Data.Tenancy;

namespace Rema.App.Services;

/// <summary>
/// Henter den aktuelle butik fra den indloggede brugers claims.
/// Returnerer <see cref="Guid.Empty"/> for anonyme forespørgsler, hvilket
/// slår tenant-filteret fra (der er alligevel ingen butiksdata at vise).
/// </summary>
public sealed class HttpTenantProvider(IHttpContextAccessor accessor) : ITenantProvider
{
    public Guid StoreId
    {
        get
        {
            var raw = accessor.HttpContext?.User?.FindFirst(AppClaims.StoreId)?.Value;
            return Guid.TryParse(raw, out var id) ? id : Guid.Empty;
        }
    }
}

namespace Rema.App.Data.Tenancy;

/// <summary>
/// Markerer en entitet som butiks-ejet. Alle sådanne entiteter filtreres
/// automatisk på den aktuelle butik via et globalt query-filter i <c>AppDbContext</c>.
/// </summary>
public interface ITenantEntity
{
    /// <summary>Id på den butik (<c>Store</c>) rækken tilhører.</summary>
    Guid StoreId { get; set; }
}

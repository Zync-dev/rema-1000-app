using System.ComponentModel.DataAnnotations;
using Rema.App.Data.Tenancy;

namespace Rema.App.Data.Entities;

/// <summary>
/// En gemt kalkulation fra avancekalkulatoren, så butikken kan finde den frem igen.
/// </summary>
public class ProductCalculation : ITenantEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid StoreId { get; set; }

    [MaxLength(200)]
    public string ProductName { get; set; } = string.Empty;

    [MaxLength(64)]
    public string? ProductNumber { get; set; }

    /// <summary>Kostpris ekskl. moms.</summary>
    public decimal CostExVat { get; set; }

    /// <summary>Salgspris inkl. moms og inkl. pant.</summary>
    public decimal SalesPriceInclVat { get; set; }

    /// <summary>Pant pr. enhed (0 hvis ingen).</summary>
    public decimal Deposit { get; set; }

    /// <summary>Anvendt momssats som decimal, fx 0.25.</summary>
    public decimal VatRate { get; set; } = Core.Profit.ProfitCalculator.DefaultVatRate;

    /// <summary>Beregnet dækningsbidrag i kroner på gemmetidspunktet.</summary>
    public decimal Contribution { get; set; }

    /// <summary>Beregnet dækningsgrad i procent på gemmetidspunktet.</summary>
    public decimal MarginPct { get; set; }

    [MaxLength(1000)]
    public string? Note { get; set; }

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public Guid CreatedByUserId { get; set; }

    [MaxLength(120)]
    public string CreatedByName { get; set; } = string.Empty;
}

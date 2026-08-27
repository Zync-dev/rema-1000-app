namespace Rema.App.Core.Profit;

/// <summary>
/// Resultatet af en DB/DG-beregning. Alle beløb er pr. enkelt vare og med fuld
/// decimalpræcision – afrunding overlades til visningslaget.
/// </summary>
/// <param name="CostExVat">Kostpris ekskl. moms.</param>
/// <param name="SalesPriceInclVat">Salgspris inkl. moms og inkl. pant.</param>
/// <param name="Deposit">Pant der indgår i salgsprisen.</param>
/// <param name="NetSales">Nettoomsætning: salgspris ekskl. moms og pant.</param>
/// <param name="VatAmount">Momsbeløbet af salget.</param>
/// <param name="Contribution">Dækningsbidrag (DB) i kroner.</param>
/// <param name="MarginPct">Dækningsgrad (DG) i procent af nettoomsætningen.</param>
/// <param name="MarkupPct">Avance i procent af kostprisen.</param>
public readonly record struct MarginResult(
    decimal CostExVat,
    decimal SalesPriceInclVat,
    decimal Deposit,
    decimal NetSales,
    decimal VatAmount,
    decimal Contribution,
    decimal MarginPct,
    decimal MarkupPct)
{
    /// <summary>Salgspris ekskl. moms, men inkl. pant.</summary>
    public decimal SalesPriceExVat => NetSales + Deposit;

    /// <summary>Sand hvis varen sælges med tab (negativt dækningsbidrag).</summary>
    public bool IsLoss => Contribution < 0m;
}

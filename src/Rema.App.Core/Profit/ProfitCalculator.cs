namespace Rema.App.Core.Profit;

/// <summary>
/// Dækningsbidrag (DB) og dækningsgrad (DG) beregninger for detailhandel.
///
/// Begreber:
///   - Kostpris: indkøbspris pr. enhed, altid ekskl. moms.
///   - Salgspris: udsalgspris pr. enhed, som standard inkl. moms (25 % i DK).
///   - Pant: gennemløbspost uden moms – trækkes fra før momsberegning.
///   - Nettoomsætning: salgspris ekskl. moms og ekskl. pant.
///   - DB (dækningsbidrag): nettoomsætning − kostpris.
///   - DG (dækningsgrad): DB i procent af nettoomsætningen.
///   - Avance: DB i procent af kostprisen.
/// </summary>
public static class ProfitCalculator
{
    /// <summary>Standard dansk momssats (25 %).</summary>
    public const decimal DefaultVatRate = 0.25m;

    /// <summary>
    /// Beregner DB/DG ud fra en kendt salgspris.
    /// </summary>
    /// <param name="costExVat">Kostpris ekskl. moms.</param>
    /// <param name="salesPriceInclVat">Salgspris inkl. moms (og inkl. evt. pant, jf. <paramref name="deposit"/>).</param>
    /// <param name="vatRate">Momssats som decimal, fx 0.25 for 25 %.</param>
    /// <param name="deposit">Pant pr. enhed, som indgår i <paramref name="salesPriceInclVat"/>.</param>
    public static MarginResult FromSalesPrice(
        decimal costExVat,
        decimal salesPriceInclVat,
        decimal vatRate = DefaultVatRate,
        decimal deposit = 0m)
    {
        Guard(costExVat, vatRate, deposit);
        if (salesPriceInclVat < 0)
            throw new ArgumentOutOfRangeException(nameof(salesPriceInclVat), "Salgspris kan ikke være negativ.");

        var netSales = (salesPriceInclVat - deposit) / (1 + vatRate);
        var vatAmount = salesPriceInclVat - deposit - netSales;
        var contribution = netSales - costExVat;

        var marginPct = netSales == 0m ? 0m : contribution / netSales * 100m;
        var markupPct = costExVat == 0m ? 0m : contribution / costExVat * 100m;

        return new MarginResult(
            CostExVat: costExVat,
            SalesPriceInclVat: salesPriceInclVat,
            Deposit: deposit,
            NetSales: netSales,
            VatAmount: vatAmount,
            Contribution: contribution,
            MarginPct: marginPct,
            MarkupPct: markupPct);
    }

    /// <summary>
    /// Beregner den salgspris (inkl. moms) der giver en ønsket dækningsgrad.
    /// </summary>
    public static MarginResult FromTargetMargin(
        decimal costExVat,
        decimal targetMarginPct,
        decimal vatRate = DefaultVatRate,
        decimal deposit = 0m)
    {
        Guard(costExVat, vatRate, deposit);
        if (targetMarginPct >= 100m)
            throw new ArgumentOutOfRangeException(nameof(targetMarginPct), "Dækningsgrad skal være under 100 %.");
        if (targetMarginPct <= -100000m)
            throw new ArgumentOutOfRangeException(nameof(targetMarginPct), "Urealistisk lav dækningsgrad.");

        var netSales = costExVat / (1m - targetMarginPct / 100m);
        var salesPriceInclVat = netSales * (1m + vatRate) + deposit;

        return FromSalesPrice(costExVat, salesPriceInclVat, vatRate, deposit);
    }

    /// <summary>
    /// Beregner den salgspris (inkl. moms) der giver en ønsket avance (DB i procent af kostpris).
    /// </summary>
    public static MarginResult FromTargetMarkup(
        decimal costExVat,
        decimal targetMarkupPct,
        decimal vatRate = DefaultVatRate,
        decimal deposit = 0m)
    {
        Guard(costExVat, vatRate, deposit);
        if (targetMarkupPct <= -100m)
            throw new ArgumentOutOfRangeException(nameof(targetMarkupPct), "Avance skal være over −100 %.");

        var netSales = costExVat * (1m + targetMarkupPct / 100m);
        var salesPriceInclVat = netSales * (1m + vatRate) + deposit;

        return FromSalesPrice(costExVat, salesPriceInclVat, vatRate, deposit);
    }

    private static void Guard(decimal costExVat, decimal vatRate, decimal deposit)
    {
        if (costExVat < 0)
            throw new ArgumentOutOfRangeException(nameof(costExVat), "Kostpris kan ikke være negativ.");
        if (vatRate < 0 || vatRate > 1)
            throw new ArgumentOutOfRangeException(nameof(vatRate), "Momssats angives som decimal mellem 0 og 1.");
        if (deposit < 0)
            throw new ArgumentOutOfRangeException(nameof(deposit), "Pant kan ikke være negativ.");
    }
}

using Rema.App.Core.Profit;

namespace Rema.App.Tests;

public class ProfitCalculatorTests
{
    [Fact]
    public void FromSalesPrice_computes_db_and_margin_with_25pct_vat()
    {
        // Kostpris 10, salgspris 20 inkl. moms -> netto 16, DB 6, DG 37,5 %
        var r = ProfitCalculator.FromSalesPrice(costExVat: 10m, salesPriceInclVat: 20m);

        Assert.Equal(16m, r.NetSales, 4);
        Assert.Equal(4m, r.VatAmount, 4);
        Assert.Equal(6m, r.Contribution, 4);
        Assert.Equal(37.5m, r.MarginPct, 4);
        Assert.Equal(60m, r.MarkupPct, 4);
        Assert.False(r.IsLoss);
    }

    [Fact]
    public void FromSalesPrice_excludes_deposit_from_revenue_and_vat()
    {
        // 1,50 kr pant er en gennemløbspost: samme netto/DB som uden pant.
        var withDeposit = ProfitCalculator.FromSalesPrice(10m, 21.5m, deposit: 1.5m);
        var without = ProfitCalculator.FromSalesPrice(10m, 20m);

        Assert.Equal(without.NetSales, withDeposit.NetSales, 4);
        Assert.Equal(without.Contribution, withDeposit.Contribution, 4);
        Assert.Equal(without.VatAmount, withDeposit.VatAmount, 4);
        Assert.Equal(21.5m, withDeposit.SalesPriceInclVat, 4);
    }

    [Fact]
    public void FromSalesPrice_flags_loss_when_price_below_cost()
    {
        var r = ProfitCalculator.FromSalesPrice(costExVat: 10m, salesPriceInclVat: 10m);

        Assert.True(r.IsLoss);
        Assert.True(r.Contribution < 0m);
    }

    [Theory]
    [InlineData(10, 25)]
    [InlineData(7.5, 40)]
    [InlineData(100, 12.5)]
    public void FromTargetMargin_roundtrips_to_requested_margin(decimal cost, decimal targetMargin)
    {
        var r = ProfitCalculator.FromTargetMargin(cost, targetMargin);

        Assert.Equal(targetMargin, r.MarginPct, 4);
        Assert.Equal(cost, r.CostExVat, 4);
    }

    [Fact]
    public void FromTargetMargin_adds_deposit_on_top_of_priced_result()
    {
        var noDeposit = ProfitCalculator.FromTargetMargin(10m, 25m);
        var withDeposit = ProfitCalculator.FromTargetMargin(10m, 25m, deposit: 1m);

        Assert.Equal(noDeposit.SalesPriceInclVat + 1m, withDeposit.SalesPriceInclVat, 4);
        Assert.Equal(25m, withDeposit.MarginPct, 4);
    }

    [Fact]
    public void FromTargetMarkup_roundtrips_to_requested_markup()
    {
        var r = ProfitCalculator.FromTargetMarkup(costExVat: 12m, targetMarkupPct: 50m);

        Assert.Equal(50m, r.MarkupPct, 4);
        Assert.Equal(18m, r.NetSales, 4);
    }

    [Fact]
    public void FromTargetMargin_rejects_margin_of_100_or_more()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ProfitCalculator.FromTargetMargin(10m, 100m));
    }

    [Theory]
    [InlineData(-1, 20, 0, 25)]
    [InlineData(10, -1, 0, 25)]
    [InlineData(10, 20, -1, 25)]
    [InlineData(10, 20, 0, 150)]
    public void Guard_rejects_invalid_inputs(decimal cost, decimal price, decimal deposit, decimal vatPercent)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ProfitCalculator.FromSalesPrice(cost, price, vatPercent / 100m, deposit));
    }

    [Fact]
    public void Custom_vat_rate_is_respected()
    {
        // 0 % moms: netto == salgspris.
        var r = ProfitCalculator.FromSalesPrice(10m, 15m, vatRate: 0m);

        Assert.Equal(15m, r.NetSales, 4);
        Assert.Equal(5m, r.Contribution, 4);
    }
}

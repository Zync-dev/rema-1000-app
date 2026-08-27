using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Rema.App.Core.Profit;
using Rema.App.Data;
using Rema.App.Data.Entities;

namespace Rema.App.Pages.Calculator;

public class IndexModel(AppDbContext db, UserManager<ApplicationUser> userManager) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public MarginResult? Result { get; private set; }

    public IReadOnlyList<ProductCalculation> Recent { get; private set; } = [];

    public enum CalcMode
    {
        [Display(Name = "Jeg kender salgsprisen")] FromPrice,
        [Display(Name = "Jeg vil ramme en dækningsgrad")] FromMargin,
        [Display(Name = "Jeg vil ramme en avance")] FromMarkup,
    }

    public class InputModel
    {
        [Display(Name = "Varenavn")]
        [StringLength(200)]
        public string? ProductName { get; set; }

        [Display(Name = "Varenummer")]
        [StringLength(64)]
        public string? ProductNumber { get; set; }

        [Display(Name = "Kostpris ekskl. moms")]
        [Range(0, 1_000_000, ErrorMessage = "Kostpris skal være 0 eller derover.")]
        public decimal CostExVat { get; set; }

        [Display(Name = "Momssats %")]
        [Range(0, 100)]
        public decimal VatPercent { get; set; } = 25m;

        [Display(Name = "Pant pr. stk.")]
        [Range(0, 100_000)]
        public decimal Deposit { get; set; }

        public CalcMode Mode { get; set; } = CalcMode.FromPrice;

        [Display(Name = "Salgspris inkl. moms")]
        [Range(0, 1_000_000)]
        public decimal? SalesPriceInclVat { get; set; }

        [Display(Name = "Ønsket dækningsgrad %")]
        [Range(-1000, 99.99)]
        public decimal? TargetMarginPercent { get; set; }

        [Display(Name = "Ønsket avance %")]
        [Range(-99.99, 100000)]
        public decimal? TargetMarkupPercent { get; set; }

        [Display(Name = "Gem beregningen")]
        public bool Save { get; set; }

        [Display(Name = "Note")]
        [StringLength(1000)]
        public string? Note { get; set; }
    }

    public async Task OnGetAsync() => await LoadRecentAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadRecentAsync();

        var vatRate = Input.VatPercent / 100m;

        try
        {
            Result = Input.Mode switch
            {
                CalcMode.FromPrice when Input.SalesPriceInclVat is { } p =>
                    ProfitCalculator.FromSalesPrice(Input.CostExVat, p, vatRate, Input.Deposit),
                CalcMode.FromMargin when Input.TargetMarginPercent is { } m =>
                    ProfitCalculator.FromTargetMargin(Input.CostExVat, m, vatRate, Input.Deposit),
                CalcMode.FromMarkup when Input.TargetMarkupPercent is { } m =>
                    ProfitCalculator.FromTargetMarkup(Input.CostExVat, m, vatRate, Input.Deposit),
                _ => null,
            };
        }
        catch (ArgumentOutOfRangeException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return Page();
        }

        if (Result is null)
        {
            ModelState.AddModelError(string.Empty, "Udfyld feltet for den valgte beregningsmetode.");
            return Page();
        }

        if (Input.Save && ModelState.IsValid)
        {
            var user = await userManager.GetUserAsync(User);
            var r = Result.Value;
            db.ProductCalculations.Add(new ProductCalculation
            {
                ProductName = string.IsNullOrWhiteSpace(Input.ProductName) ? "(uden navn)" : Input.ProductName.Trim(),
                ProductNumber = string.IsNullOrWhiteSpace(Input.ProductNumber) ? null : Input.ProductNumber.Trim(),
                CostExVat = Input.CostExVat,
                SalesPriceInclVat = r.SalesPriceInclVat,
                Deposit = Input.Deposit,
                VatRate = vatRate,
                Contribution = r.Contribution,
                MarginPct = r.MarginPct,
                Note = string.IsNullOrWhiteSpace(Input.Note) ? null : Input.Note.Trim(),
                CreatedByUserId = user!.Id,
                CreatedByName = user.DisplayName,
            });
            await db.SaveChangesAsync();
            TempData["StatusMessage"] = "Beregningen er gemt.";
            return RedirectToPage();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        var row = await db.ProductCalculations.FirstOrDefaultAsync(c => c.Id == id);
        if (row is not null)
        {
            db.ProductCalculations.Remove(row);
            await db.SaveChangesAsync();
            TempData["StatusMessage"] = "Beregningen er slettet.";
        }
        return RedirectToPage();
    }

    private async Task LoadRecentAsync()
    {
        Recent = await db.ProductCalculations
            .OrderByDescending(c => c.CreatedUtc)
            .Take(25)
            .ToListAsync();
    }
}

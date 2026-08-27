using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Rema.App.Data;
using Rema.App.Data.Entities;

namespace Rema.App.Pages.Account;

[AllowAnonymous]
public class RegisterModel(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    AppDbContext db,
    ILogger<RegisterModel> logger) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required(ErrorMessage = "Angiv butiksnavn.")]
        [StringLength(160, MinimumLength = 2)]
        [Display(Name = "Butiksnavn")]
        public string StoreName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Angiv butiksnummer.")]
        [StringLength(16)]
        [Display(Name = "Butiksnummer")]
        public string StoreNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Angiv by.")]
        [StringLength(120)]
        [Display(Name = "By")]
        public string City { get; set; } = string.Empty;

        [Required(ErrorMessage = "Angiv dit navn.")]
        [StringLength(120)]
        [Display(Name = "Dit navn")]
        public string DisplayName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Angiv e-mail.")]
        [EmailAddress(ErrorMessage = "Ugyldig e-mail.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vælg en adgangskode.")]
        [StringLength(100, MinimumLength = 10, ErrorMessage = "Adgangskoden skal være mindst 10 tegn.")]
        [DataType(DataType.Password)]
        [Display(Name = "Adgangskode")]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "De to adgangskoder er ikke ens.")]
        [Display(Name = "Gentag adgangskode")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var storeNumber = Input.StoreNumber.Trim();
        if (await db.Stores.AnyAsync(s => s.StoreNumber == storeNumber))
        {
            ModelState.AddModelError("Input.StoreNumber", "Der findes allerede en butik med dette nummer. Kontakt os hvis det er din butik.");
            return Page();
        }

        await using var tx = await db.Database.BeginTransactionAsync();

        var store = new Store
        {
            StoreNumber = storeNumber,
            Name = Input.StoreName.Trim(),
            City = Input.City.Trim(),
        };
        db.Stores.Add(store);
        await db.SaveChangesAsync();

        var user = new ApplicationUser
        {
            UserName = Input.Email.Trim(),
            Email = Input.Email.Trim(),
            EmailConfirmed = true,
            DisplayName = Input.DisplayName.Trim(),
            StoreId = store.Id,
        };

        var created = await userManager.CreateAsync(user, Input.Password);
        if (!created.Succeeded)
        {
            await tx.RollbackAsync();
            foreach (var error in created.Errors)
                ModelState.AddModelError(string.Empty, Translate(error));
            return Page();
        }

        await userManager.AddToRoleAsync(user, RoleNames.Koebmand);
        await tx.CommitAsync();

        logger.LogInformation("Ny butik oprettet: {StoreNumber} {StoreName}", store.StoreNumber, store.Name);

        await signInManager.SignInAsync(user, isPersistent: true);
        return RedirectToPage("/Index");
    }

    private static string Translate(IdentityError error) => error.Code switch
    {
        "DuplicateUserName" or "DuplicateEmail" => "Der findes allerede en bruger med denne e-mail.",
        "PasswordTooShort" => "Adgangskoden er for kort.",
        "PasswordRequiresDigit" => "Adgangskoden skal indeholde mindst ét tal.",
        "PasswordRequiresUpper" => "Adgangskoden skal indeholde mindst ét stort bogstav.",
        "PasswordRequiresLower" => "Adgangskoden skal indeholde mindst ét lille bogstav.",
        _ => error.Description,
    };
}

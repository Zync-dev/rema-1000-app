using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Rema.App.Data.Entities;

namespace Rema.App.Pages.Profile;

public class PasswordModel(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required(ErrorMessage = "Angiv din nuværende adgangskode.")]
        [DataType(DataType.Password)]
        [Display(Name = "Nuværende adgangskode")]
        public string Current { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vælg en ny adgangskode.")]
        [StringLength(100, MinimumLength = 10, ErrorMessage = "Adgangskoden skal være mindst 10 tegn.")]
        [DataType(DataType.Password)]
        [Display(Name = "Ny adgangskode")]
        public string New { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Compare(nameof(New), ErrorMessage = "De to adgangskoder er ikke ens.")]
        [Display(Name = "Gentag ny adgangskode")]
        public string Confirm { get; set; } = string.Empty;
    }

    public IActionResult OnGet() => Page();

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Challenge();
        if (!ModelState.IsValid) return Page();

        var result = await userManager.ChangePasswordAsync(user, Input.Current, Input.New);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors)
                ModelState.AddModelError(string.Empty, e.Code == "PasswordMismatch"
                    ? "Den nuværende adgangskode er forkert."
                    : e.Description);
            return Page();
        }

        await signInManager.RefreshSignInAsync(user);
        TempData["StatusMessage"] = "Din adgangskode er skiftet.";
        return RedirectToPage("Index");
    }
}

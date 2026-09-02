using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Rema.App.Data.Entities;

namespace Rema.App.Pages.Account;

[AllowAnonymous]
public class LoginModel(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Angiv e-mail.")]
        [EmailAddress(ErrorMessage = "Ugyldig e-mail.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Angiv adgangskode.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Husk mig")]
        public bool RememberMe { get; set; } = true;
    }

    public void OnGet(string? returnUrl = null) => ReturnUrl = returnUrl;

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
        if (!ModelState.IsValid)
            return Page();

        var result = await signInManager.PasswordSignInAsync(
            Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
            return LocalRedirect(returnUrl ?? Url.Content("~/"));

        if (result.IsLockedOut)
        {
            // Deaktiverede konti låses permanent – skeln dem fra en midlertidig spærring.
            var user = await userManager.FindByEmailAsync(Input.Email);
            ModelState.AddModelError(string.Empty, user is { IsActive: false }
                ? "Din konto er deaktiveret. Kontakt din købmand."
                : "Kontoen er midlertidigt låst efter for mange forsøg. Prøv igen om lidt.");
            return Page();
        }

        ModelState.AddModelError(string.Empty, "Forkert e-mail eller adgangskode.");
        return Page();
    }
}

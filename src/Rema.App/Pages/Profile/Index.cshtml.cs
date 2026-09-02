using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Rema.App.Data.Entities;
using Rema.App.Services;

namespace Rema.App.Pages.Profile;

public class IndexModel(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string Email { get; private set; } = "";
    public string RoleLabel { get; private set; } = "";

    public class InputModel
    {
        [Required(ErrorMessage = "Angiv dit navn.")]
        [StringLength(120, MinimumLength = 2)]
        [Display(Name = "Navn")]
        public string DisplayName { get; set; } = string.Empty;

        [Phone(ErrorMessage = "Ugyldigt telefonnummer.")]
        [StringLength(30)]
        [Display(Name = "Mobilnummer")]
        public string? Phone { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Challenge();
        await LoadAsync(user);
        Input = new InputModel { DisplayName = user.DisplayName, Phone = user.PhoneNumber };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Challenge();
        await LoadAsync(user);

        if (!ModelState.IsValid) return Page();

        user.DisplayName = Input.DisplayName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(Input.Phone) ? null : Input.Phone.Trim();
        await userManager.UpdateAsync(user);
        await signInManager.RefreshSignInAsync(user);
        TempData["StatusMessage"] = "Din profil er opdateret.";
        return RedirectToPage();
    }

    private async Task LoadAsync(ApplicationUser user)
    {
        Email = user.Email ?? "";
        var roles = await userManager.GetRolesAsync(user);
        RoleLabel = RoleInfo.Label(roles.OrderBy(RoleInfo.Rank).FirstOrDefault());
    }
}

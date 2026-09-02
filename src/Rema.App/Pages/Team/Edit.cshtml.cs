using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Rema.App.Data.Entities;
using Rema.App.Services;

namespace Rema.App.Pages.Team;

[Authorize(Policy = "ErLeder")]
public class EditModel(
    UserManager<ApplicationUser> userManager,
    TeamDirectory team,
    ILogger<EditModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string Email { get; private set; } = "";
    public string CurrentRole { get; private set; } = "";
    public bool IsActive { get; private set; }
    public bool ActorIsKoebmand => User.IsInRole(RoleNames.Koebmand);
    public string? NewPassword { get; private set; }

    public IEnumerable<string> RoleOptions => RoleInfo.AssignableBy(ActorIsKoebmand);

    public class InputModel
    {
        [Required(ErrorMessage = "Angiv et navn.")]
        [StringLength(120, MinimumLength = 2)]
        [Display(Name = "Navn")]
        public string DisplayName { get; set; } = string.Empty;

        [Phone(ErrorMessage = "Ugyldigt telefonnummer.")]
        [StringLength(30)]
        [Display(Name = "Mobilnummer")]
        public string? Phone { get; set; }

        [Required]
        [Display(Name = "Rolle")]
        public string Role { get; set; } = RoleNames.Medarbejder;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await team.FindInStoreAsync(Id);
        if (user is null) return NotFound();
        if (user.Id == GetUserId()) return RedirectToPage("/Profile/Index");

        await LoadAsync(user);
        Input = new InputModel { DisplayName = user.DisplayName, Phone = user.PhoneNumber, Role = CurrentRole };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await team.FindInStoreAsync(Id);
        if (user is null) return NotFound();
        if (user.Id == GetUserId()) return RedirectToPage("/Profile/Index");

        await LoadAsync(user);

        if (!ActorMayManage(CurrentRole))
            ModelState.AddModelError(string.Empty, "Kun en købmand kan redigere en anden leder.");
        if (!RoleOptions.Contains(Input.Role))
            ModelState.AddModelError("Input.Role", "Du kan ikke tildele denne rolle.");
        if (CurrentRole == RoleNames.Koebmand && Input.Role != RoleNames.Koebmand
            && await team.ActiveKoebmandCountAsync() <= 1)
            ModelState.AddModelError("Input.Role", "Butikken skal have mindst én købmand.");

        if (!ModelState.IsValid) return Page();

        user.DisplayName = Input.DisplayName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(Input.Phone) ? null : Input.Phone.Trim();
        await userManager.UpdateAsync(user);

        if (Input.Role != CurrentRole)
        {
            await userManager.RemoveFromRoleAsync(user, CurrentRole);
            await userManager.AddToRoleAsync(user, Input.Role);
            await userManager.UpdateSecurityStampAsync(user);
            logger.LogInformation("Rolle ændret for {UserId}: {Old} -> {New}", user.Id, CurrentRole, Input.Role);
        }

        TempData["StatusMessage"] = "Ændringerne er gemt.";
        return RedirectToPage(new { Id });
    }

    public async Task<IActionResult> OnPostResetPasswordAsync()
    {
        var user = await team.FindInStoreAsync(Id);
        if (user is null) return NotFound();
        await LoadAsync(user);
        if (!ActorMayManage(CurrentRole)) return Forbid();

        var pw = NewModel.GeneratePassword();
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, token, pw);
        if (!result.Succeeded)
        {
            TempData["StatusMessage"] = "Kunne ikke nulstille adgangskoden.";
            return RedirectToPage(new { Id });
        }

        await userManager.UpdateSecurityStampAsync(user);
        Input = new InputModel { DisplayName = user.DisplayName, Phone = user.PhoneNumber, Role = CurrentRole };
        NewPassword = pw;
        logger.LogInformation("Adgangskode nulstillet for {UserId}", user.Id);
        return Page();
    }

    public async Task<IActionResult> OnPostDeactivateAsync()
    {
        var user = await team.FindInStoreAsync(Id);
        if (user is null) return NotFound();
        await LoadAsync(user);
        if (!ActorMayManage(CurrentRole)) return Forbid();
        if (user.Id == GetUserId()) return RedirectToPage("/Profile/Index");

        if (CurrentRole == RoleNames.Koebmand && await team.ActiveKoebmandCountAsync() <= 1)
        {
            TempData["StatusMessage"] = "Butikken skal have mindst én aktiv købmand.";
            return RedirectToPage(new { Id });
        }

        user.IsActive = false;
        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.MaxValue;
        await userManager.UpdateAsync(user);
        await userManager.UpdateSecurityStampAsync(user);
        TempData["StatusMessage"] = $"{user.DisplayName} er deaktiveret og kan ikke længere logge ind.";
        return RedirectToPage(new { Id });
    }

    public async Task<IActionResult> OnPostReactivateAsync()
    {
        var user = await team.FindInStoreAsync(Id);
        if (user is null) return NotFound();
        await LoadAsync(user);
        if (!ActorMayManage(CurrentRole)) return Forbid();

        user.IsActive = true;
        user.LockoutEnd = null;
        await userManager.UpdateAsync(user);
        TempData["StatusMessage"] = $"{user.DisplayName} er aktiveret igen.";
        return RedirectToPage(new { Id });
    }

    private async Task LoadAsync(ApplicationUser user)
    {
        Email = user.Email ?? "";
        IsActive = user.IsActive;
        var roles = await userManager.GetRolesAsync(user);
        CurrentRole = roles.OrderBy(RoleInfo.Rank).FirstOrDefault() ?? RoleNames.Medarbejder;
    }

    private bool ActorMayManage(string targetRole) =>
        ActorIsKoebmand || RoleInfo.Rank(targetRole) > RoleInfo.Rank(RoleNames.Souschef);

    private Guid GetUserId() =>
        Guid.TryParse(userManager.GetUserId(User), out var id) ? id : Guid.Empty;
}

using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Rema.App.Data;
using Rema.App.Data.Entities;
using Rema.App.Data.Tenancy;
using Rema.App.Services;

namespace Rema.App.Pages.Team;

[Authorize(Policy = "ErLeder")]
public class NewModel(
    UserManager<ApplicationUser> userManager,
    AppDbContext db,
    ITenantProvider tenant,
    ILogger<NewModel> logger) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    /// <summary>Sat efter oprettelse – så viser siden loginoplysningerne i stedet for formularen.</summary>
    public CreatedInfo? Created { get; private set; }

    public bool ActorIsKoebmand => User.IsInRole(RoleNames.Koebmand);

    public IEnumerable<string> RoleOptions => RoleInfo.AssignableBy(ActorIsKoebmand);

    public sealed record CreatedInfo(string DisplayName, string Email, string Password);

    public class InputModel
    {
        [Required(ErrorMessage = "Angiv medarbejderens navn.")]
        [StringLength(120, MinimumLength = 2)]
        [Display(Name = "Navn")]
        public string DisplayName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Angiv en e-mail. Den bruges som login.")]
        [EmailAddress(ErrorMessage = "Ugyldig e-mail.")]
        [Display(Name = "E-mail (login)")]
        public string Email { get; set; } = string.Empty;

        [Phone(ErrorMessage = "Ugyldigt telefonnummer.")]
        [StringLength(30)]
        [Display(Name = "Mobilnummer")]
        public string? Phone { get; set; }

        [Required]
        [Display(Name = "Rolle")]
        public string Role { get; set; } = RoleNames.Medarbejder;

        [Required(ErrorMessage = "Vælg eller generér en adgangskode.")]
        [StringLength(100, MinimumLength = 10, ErrorMessage = "Adgangskoden skal være mindst 10 tegn.")]
        [Display(Name = "Midlertidig adgangskode")]
        public string Password { get; set; } = string.Empty;
    }

    public void OnGet() => Input.Password = GeneratePassword();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!RoleOptions.Contains(Input.Role))
            ModelState.AddModelError("Input.Role", "Du kan ikke tildele denne rolle.");

        var email = Input.Email.Trim();
        if (await db.Users.AnyAsync(u => u.NormalizedEmail == email.ToUpperInvariant()))
            ModelState.AddModelError("Input.Email", "Der findes allerede en bruger med denne e-mail.");

        if (!ModelState.IsValid)
            return Page();

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = Input.DisplayName.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(Input.Phone) ? null : Input.Phone.Trim(),
            StoreId = tenant.StoreId,
            IsActive = true,
        };

        var created = await userManager.CreateAsync(user, Input.Password);
        if (!created.Succeeded)
        {
            foreach (var e in created.Errors)
                ModelState.AddModelError(string.Empty, Translate(e));
            return Page();
        }

        await userManager.AddToRoleAsync(user, Input.Role);
        logger.LogInformation("Medarbejderkonto oprettet i butik {StoreId}: {Email} ({Role})",
            tenant.StoreId, email, Input.Role);

        Created = new CreatedInfo(user.DisplayName, email, Input.Password);
        Input = new InputModel { Password = GeneratePassword() };
        ModelState.Clear();
        return Page();
    }

    /// <summary>12 tegn, altid mindst ét stort bogstav, ét lille og ét tal – uden let forvekslelige tegn.</summary>
    public static string GeneratePassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnpqrstuvwxyz";
        const string digits = "23456789";
        const string all = upper + lower + digits;

        Span<char> buf = stackalloc char[12];
        buf[0] = upper[RandomNumberGenerator.GetInt32(upper.Length)];
        buf[1] = lower[RandomNumberGenerator.GetInt32(lower.Length)];
        buf[2] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
        for (var i = 3; i < buf.Length; i++)
            buf[i] = all[RandomNumberGenerator.GetInt32(all.Length)];

        // Bland, så de garanterede tegn ikke altid står forrest.
        for (var i = buf.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (buf[i], buf[j]) = (buf[j], buf[i]);
        }
        return new string(buf);
    }

    private static string Translate(IdentityError error) => error.Code switch
    {
        "DuplicateUserName" or "DuplicateEmail" => "Der findes allerede en bruger med denne e-mail.",
        "PasswordTooShort" => "Adgangskoden er for kort (mindst 10 tegn).",
        "PasswordRequiresDigit" => "Adgangskoden skal indeholde mindst ét tal.",
        "PasswordRequiresUpper" => "Adgangskoden skal indeholde mindst ét stort bogstav.",
        "PasswordRequiresLower" => "Adgangskoden skal indeholde mindst ét lille bogstav.",
        _ => error.Description,
    };
}

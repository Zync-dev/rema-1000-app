using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Rema.App.Data;
using Rema.App.Data.Entities;
using Rema.App.Services;

namespace Rema.App.Pages.Reminders;

[Authorize]
public class IndexModel(
    AppDbContext db,
    TeamDirectory team,
    UserManager<ApplicationUser> userManager) : PageModel
{
    public List<Row> Upcoming { get; private set; } = [];
    public List<Row> History { get; private set; } = [];
    public IReadOnlyList<TeamMember> People { get; private set; } = [];

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public sealed record Row(
        Guid Id, string Text, DateTimeOffset DueAtUtc, string Recipient,
        ReminderStatus Status, string? Error);

    public class InputModel
    {
        [Required(ErrorMessage = "Skriv hvad der skal huskes.")]
        [StringLength(500, MinimumLength = 3)]
        [Display(Name = "Hvad skal huskes?")]
        public string Text { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vælg en dato.")]
        [DataType(DataType.Date)]
        [Display(Name = "Dato")]
        public DateOnly? Date { get; set; }

        [Required(ErrorMessage = "Vælg et klokkeslæt.")]
        [DataType(DataType.Time)]
        [Display(Name = "Klokkeslæt")]
        public TimeOnly? Time { get; set; }

        [Display(Name = "Send")]
        public int LeadMinutes { get; set; }

        [Display(Name = "Modtager")]
        public string RecipientKind { get; set; } = "user";

        public Guid? RecipientUserId { get; set; }

        [Display(Name = "Navn")]
        [StringLength(120)]
        public string? RecipientName { get; set; }

        [Display(Name = "E-mail")]
        [EmailAddress(ErrorMessage = "Ugyldig e-mail.")]
        [StringLength(200)]
        public string? RecipientEmail { get; set; }
    }

    public static readonly (int Minutes, string Label)[] LeadOptions =
    [
        (0, "På tidspunktet"),
        (15, "15 minutter før"),
        (60, "1 time før"),
        (180, "3 timer før"),
        (1440, "1 dag før"),
    ];

    public async Task OnGetAsync()
    {
        await LoadAsync();
        Input.Date = DanishTime.Today;
        Input.Time = new TimeOnly(8, 0);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadAsync();

        DateTimeOffset dueUtc = default;
        if (Input.Date is { } d && Input.Time is { } t)
        {
            dueUtc = DanishTime.ToUtc(d.ToDateTime(t));
            if (dueUtc <= DateTimeOffset.UtcNow)
                ModelState.AddModelError("Input.Time", "Tidspunktet skal være i fremtiden.");
        }

        Guid? recipientUserId = null;
        string? recipientName = null, recipientEmail = null;

        if (Input.RecipientKind == "user")
        {
            var person = People.FirstOrDefault(p => p.Id == Input.RecipientUserId);
            if (person is null)
                ModelState.AddModelError("Input.RecipientUserId", "Vælg en medarbejder.");
            else if (string.IsNullOrWhiteSpace(person.Email))
                ModelState.AddModelError("Input.RecipientUserId", $"{person.DisplayName} har ingen e-mail.");
            else
                recipientUserId = person.Id;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(Input.RecipientName))
                ModelState.AddModelError("Input.RecipientName", "Skriv et navn.");
            if (string.IsNullOrWhiteSpace(Input.RecipientEmail))
                ModelState.AddModelError("Input.RecipientEmail", "Skriv en e-mail.");
            recipientName = Input.RecipientName?.Trim();
            recipientEmail = Input.RecipientEmail?.Trim();
        }

        if (!ModelState.IsValid) return Page();

        var lead = LeadOptions.Any(o => o.Minutes == Input.LeadMinutes) ? Input.LeadMinutes : 0;
        db.Reminders.Add(new Reminder
        {
            Text = Input.Text.Trim(),
            DueAtUtc = dueUtc,
            LeadMinutes = lead,
            SendAtUtc = dueUtc.AddMinutes(-lead),
            RecipientUserId = recipientUserId,
            RecipientName = recipientName,
            RecipientEmail = recipientEmail,
            CreatedByUserId = Guid.TryParse(userManager.GetUserId(User), out var uid) ? uid : Guid.Empty,
        });
        await db.SaveChangesAsync();
        TempData["StatusMessage"] = "Påmindelsen er oprettet.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCancelAsync(Guid id)
    {
        var r = await db.Reminders.FirstOrDefaultAsync(x => x.Id == id);
        if (r is not null && r.Status == ReminderStatus.Scheduled)
        {
            r.Status = ReminderStatus.Cancelled;
            await db.SaveChangesAsync();
            TempData["StatusMessage"] = "Påmindelsen er aflyst.";
        }
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        People = await team.AssignableAsync();
        var names = People.ToDictionary(p => p.Id, p => p.DisplayName);

        string RecipientOf(Reminder r) =>
            r.RecipientUserId is Guid g && names.TryGetValue(g, out var n) ? n
            : r.RecipientName ?? r.RecipientEmail ?? "–";

        var all = await db.Reminders.OrderBy(r => r.SendAtUtc).ToListAsync();

        Upcoming = all
            .Where(r => r.Status == ReminderStatus.Scheduled)
            .Select(r => new Row(r.Id, r.Text, r.DueAtUtc, RecipientOf(r), r.Status, r.Error))
            .ToList();

        History = all
            .Where(r => r.Status != ReminderStatus.Scheduled)
            .OrderByDescending(r => r.SentUtc ?? r.SendAtUtc)
            .Take(15)
            .Select(r => new Row(r.Id, r.Text, r.DueAtUtc, RecipientOf(r), r.Status, r.Error))
            .ToList();
    }
}

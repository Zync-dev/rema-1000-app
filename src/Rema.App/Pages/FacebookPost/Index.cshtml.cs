using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Rema.App.Data;
using Rema.App.Data.Entities;
using Rema.App.Services.Ai;

namespace Rema.App.Pages.FacebookPost;

public class IndexModel(
    AppDbContext db,
    UserManager<ApplicationUser> userManager,
    IFacebookPostGenerator generator) : PageModel
{
    public StoreAiSettings? Settings { get; private set; }
    public bool CanConfigure { get; private set; }
    public IReadOnlyList<Data.Entities.FacebookPost> Recent { get; private set; } = [];
    public Data.Entities.FacebookPost? Focus { get; private set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        public FacebookPostType PostType { get; set; } = FacebookPostType.Tilbud;

        [Required(ErrorMessage = "Skriv de oplysninger opslaget skal bygge på.")]
        [StringLength(4000, MinimumLength = 3)]
        [Display(Name = "Oplysninger")]
        public string Brief { get; set; } = string.Empty;
    }

    public async Task OnGetAsync(Guid? post) => await LoadAsync(post);

    public async Task<IActionResult> OnPostGenerateAsync()
    {
        await LoadAsync(null);

        if (Settings is null || !Settings.HasApiKey)
        {
            ModelState.AddModelError(string.Empty, "Der er ikke sat en API-nøgle op endnu. Bed købmanden om at gøre det under Indstillinger.");
            return Page();
        }
        if (!ModelState.IsValid)
            return Page();

        var store = await db.Stores.FirstAsync(s => s.Id == Settings.StoreId);
        var examples = Settings.Examples.OrderBy(e => e.SortOrder).Select(e => e.Text).ToList();

        GeneratedPost result;
        try
        {
            result = await generator.GenerateAsync(store, Settings, examples, Input.PostType, Input.Brief, HttpContext.RequestAborted);
        }
        catch (AiGenerationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return Page();
        }

        var user = await userManager.GetUserAsync(User);
        var post = new Data.Entities.FacebookPost
        {
            PostType = Input.PostType,
            Brief = Input.Brief.Trim(),
            Text = result.Text,
            Model = result.Model,
            InputTokens = result.InputTokens,
            OutputTokens = result.OutputTokens,
            CreatedByUserId = user!.Id,
            CreatedByName = user.DisplayName,
        };
        db.FacebookPosts.Add(post);
        await db.SaveChangesAsync();

        return RedirectToPage(new { post = post.Id });
    }

    public async Task<IActionResult> OnPostUpdateAsync(Guid id, string? text, FacebookPostStatus status)
    {
        var post = await db.FacebookPosts.FirstOrDefaultAsync(p => p.Id == id);
        if (post is null) return NotFound();

        var newText = (text ?? string.Empty).Trim();
        if (newText != post.Text)
        {
            post.Text = newText[..Math.Min(newText.Length, 8000)];
            post.EditedByUser = true;
        }
        post.Status = status;
        await db.SaveChangesAsync();
        TempData["StatusMessage"] = "Opslaget er gemt.";
        return RedirectToPage(new { post = id });
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        var post = await db.FacebookPosts.FirstOrDefaultAsync(p => p.Id == id);
        if (post is not null)
        {
            db.FacebookPosts.Remove(post);
            await db.SaveChangesAsync();
            TempData["StatusMessage"] = "Opslaget er slettet.";
        }
        return RedirectToPage();
    }

    private async Task LoadAsync(Guid? focusId)
    {
        Settings = await db.StoreAiSettings.Include(s => s.Examples).FirstOrDefaultAsync();
        CanConfigure = User.IsInRole(RoleNames.Koebmand) || User.IsInRole(RoleNames.Souschef);

        Recent = await db.FacebookPosts
            .OrderByDescending(p => p.CreatedUtc)
            .Take(30)
            .ToListAsync();

        if (focusId is { } id)
            Focus = Recent.FirstOrDefault(p => p.Id == id)
                ?? await db.FacebookPosts.FirstOrDefaultAsync(p => p.Id == id);
    }
}

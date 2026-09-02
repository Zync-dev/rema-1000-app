using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Rema.App.Services;

namespace Rema.App.Pages.Team;

[Authorize(Policy = "ErLeder")]
public class IndexModel(TeamDirectory team) : PageModel
{
    public IReadOnlyList<TeamMember> Members { get; private set; } = [];

    public async Task OnGetAsync() => Members = await team.ListAsync();
}

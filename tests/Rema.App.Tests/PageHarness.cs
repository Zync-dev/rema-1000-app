using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;

namespace Rema.App.Tests;

/// <summary>Giver en PageModel nok kontekst til at TempData og ModelState virker i tests.</summary>
public static class PageHarness
{
    public static T Wire<T>(this T page, HttpContext? http = null) where T : PageModel
    {
        http ??= new DefaultHttpContext();
        var actionContext = new Microsoft.AspNetCore.Mvc.ActionContext(
            http, new RouteData(), new Microsoft.AspNetCore.Mvc.RazorPages.CompiledPageActionDescriptor(),
            new ModelStateDictionary());

        page.PageContext = new PageContext(actionContext);
        page.TempData = new TempDataDictionary(http, new NullTempDataProvider());
        return page;
    }

    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) => new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values) { }
    }
}

using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Rema.App.Data;
using Rema.App.Data.Entities;
using Rema.App.Data.Tenancy;
using Rema.App.Services;

var builder = WebApplication.CreateBuilder(args);

// --- Database ---------------------------------------------------------------
var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection").NullIfBlank()
    ?? Environment.GetEnvironmentVariable("DATABASE_URL").NullIfBlank()
    ?? throw new InvalidOperationException(
        "Ingen databaseforbindelse. Sæt ConnectionStrings:DefaultConnection " +
        "(user-secrets i udvikling) eller miljøvariablen DATABASE_URL.");

builder.Services.AddRemaData(connectionString);

// --- Multi-tenant ----------------------------------------------------------
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantProvider, HttpTenantProvider>();

// --- AI (Facebook-opslag) ------------------------------------------------
builder.Services.AddSingleton<Rema.App.Services.Ai.ApiKeyProtector>();
builder.Services.AddHttpClient<Rema.App.Services.Ai.GeminiClient>(c =>
{
    c.BaseAddress = new Uri(Rema.App.Services.Ai.GeminiClient.BaseAddress);
    c.Timeout = TimeSpan.FromSeconds(60);
});
builder.Services.AddScoped<Rema.App.Services.Ai.IFacebookPostGenerator,
    Rema.App.Services.Ai.GeminiFacebookPostGenerator>();

// --- Identity / auth ------------------------------------------------------
builder.Services
    .AddIdentity<ApplicationUser, ApplicationRole>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequiredLength = 10;
        options.Password.RequireNonAlphanumeric = false;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddScoped<
    IUserClaimsPrincipalFactory<ApplicationUser>, AppUserClaimsPrincipalFactory>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
    options.Cookie.Name = "rema.auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    // Kræv HTTPS for cookien i produktion; tillad HTTP lokalt så udvikling virker.
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});

// Data Protection-nøgler i databasen, så cookies overlever en genstart / ny container.
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<AppDbContext>()
    .SetApplicationName("rema-app");

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ErLeder", p => p.RequireRole(RoleNames.Koebmand, RoleNames.Souschef));
    options.AddPolicy("ErKoebmand", p => p.RequireRole(RoleNames.Koebmand));
});

// --- Razor Pages ---------------------------------------------------------
builder.Services.AddRazorPages(options =>
{
    // Alt kræver login, undtagen det der eksplicit åbnes i de enkelte sider.
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToFolder("/Account");
    options.Conventions.AllowAnonymousToPage("/Index");
    options.Conventions.AllowAnonymousToPage("/Privacy");
    options.Conventions.AllowAnonymousToPage("/Error");
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();

// Migrér + seed roller ved opstart (kan slås fra med RunMigrationsAtStartup=false).
if (app.Configuration.GetValue("RunMigrationsAtStartup", true))
    await DbInitializer.RunAsync(app.Services, migrate: true);

app.Run();

/// <summary>Gør Program synlig for integrationstests.</summary>
public partial class Program;

internal static class StringExtensions
{
    public static string? NullIfBlank(this string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;
}

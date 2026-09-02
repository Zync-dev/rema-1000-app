using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Rema.App.Data;
using Rema.App.Data.Entities;
using Rema.App.Data.Tenancy;
using Rema.App.Services;

var builder = WebApplication.CreateBuilder(args);

// Railway (og de fleste PaaS) tildeler porten via miljøvariablen PORT.
var port = Environment.GetEnvironmentVariable("PORT");
builder.WebHost.UseUrls($"http://0.0.0.0:{(string.IsNullOrWhiteSpace(port) ? "8080" : port)}");

// Kør bag Railways proxy: stol på X-Forwarded-* så HTTPS/klient-IP er korrekt.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});

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

// --- Butikkens brugere ---------------------------------------------------
builder.Services.AddScoped<Rema.App.Services.TeamDirectory>();

// --- Opgavelister ------------------------------------------------------
builder.Services.AddScoped<Rema.App.Services.ChecklistService>();

// --- Påmindelser + mail ------------------------------------------------
builder.Services.Configure<Rema.App.Services.Email.EmailOptions>(
    builder.Configuration.GetSection(Rema.App.Services.Email.EmailOptions.Section));
var emailOptions = builder.Configuration
    .GetSection(Rema.App.Services.Email.EmailOptions.Section)
    .Get<Rema.App.Services.Email.EmailOptions>() ?? new();
if (emailOptions.IsConfigured)
    builder.Services.AddHttpClient<Rema.App.Services.Email.IEmailSender, Rema.App.Services.Email.ResendEmailSender>(c =>
    {
        c.BaseAddress = new Uri(Rema.App.Services.Email.ResendEmailSender.BaseAddress);
        c.Timeout = TimeSpan.FromSeconds(30);
    });
else
    builder.Services.AddSingleton<Rema.App.Services.Email.IEmailSender, Rema.App.Services.Email.LogEmailSender>();
builder.Services.AddScoped<Rema.App.Services.ReminderSender>();
builder.Services.AddHostedService<Rema.App.Services.ReminderDispatcher>();

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

// Krypter selve nøgleringen i databasen med en AES-nøgle fra miljøet, så et
// databaselæk ikke også afslører auth-cookie-nøgler og gemte API-nøgler.
var dpKeyB64 = Environment.GetEnvironmentVariable("DATAPROTECTION_KEY").NullIfBlank();
if (dpKeyB64 is not null)
{
    var masterKey = new DataProtectionMasterKey(Convert.FromBase64String(dpKeyB64));
    builder.Services.AddSingleton(masterKey);
    builder.Services.Configure<KeyManagementOptions>(o => o.XmlEncryptor = new AesXmlEncryptor(masterKey));
}

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ErLeder", p => p.RequireRole(RoleNames.Koebmand, RoleNames.Souschef));
    options.AddPolicy("ErKoebmand", p => p.RequireRole(RoleNames.Koebmand));
});

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database");

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

if (dpKeyB64 is null && !app.Environment.IsDevelopment())
    app.Logger.LogWarning(
        "DATAPROTECTION_KEY er ikke sat. Data Protection-nøgleringen (auth-cookies + gemte "
        + "Gemini API-nøgler) gemmes UKRYPTERET i databasen. Sæt en base64-nøgle: openssl rand -base64 32");

if (!emailOptions.IsConfigured && !app.Environment.IsDevelopment())
    app.Logger.LogWarning(
        "Email er ikke opsat (Email__ApiKey / Email__FromEmail). Påmindelser bliver IKKE sendt – "
        + "de logges kun. Opret en gratis Resend-konto og sæt nøglen + en verificeret afsender.");

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
    app.UseHttpsRedirection();
}

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();
app.MapHealthChecks("/healthz");

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

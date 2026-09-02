namespace Rema.App.Services.Email;

/// <summary>
/// Opsætning for udgående mail via <see href="https://resend.com">Resend</see>.
/// Bindes fra sektionen <c>Email</c> (appsettings eller miljøvariabler som
/// <c>Email__ApiKey</c>). Er <see cref="ApiKey"/> eller <see cref="FromEmail"/>
/// tom, sendes der ingen rigtige mails – de logges i stedet.
/// </summary>
public sealed class EmailOptions
{
    public const string Section = "Email";

    /// <summary>Resend API-nøgle (starter med <c>re_</c>).</summary>
    public string? ApiKey { get; set; }

    /// <summary>Afsenderadresse. Domænet skal være verificeret i Resend (i test: en @resend.dev-adresse).</summary>
    public string FromEmail { get; set; } = "";

    public string FromName { get; set; } = "Rema Butiksværktøjer";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(FromEmail);
}

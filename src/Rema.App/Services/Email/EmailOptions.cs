namespace Rema.App.Services.Email;

/// <summary>
/// SMTP-opsætning. Bindes fra sektionen <c>Email</c> (appsettings eller miljøvariabler
/// som <c>Email__Host</c>). Er <see cref="Host"/> tom, sendes der ingen rigtige mails –
/// de logges i stedet.
/// </summary>
public sealed class EmailOptions
{
    public const string Section = "Email";

    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public bool UseStartTls { get; set; } = true;
    public string? Username { get; set; }
    public string? Password { get; set; }

    /// <summary>Afsenderadresse. Skal være verificeret hos mailudbyderen (fx Brevo).</summary>
    public string FromEmail { get; set; } = "";
    public string FromName { get; set; } = "Rema Butiksværktøjer";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(FromEmail);
}

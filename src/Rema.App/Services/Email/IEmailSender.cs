namespace Rema.App.Services.Email;

/// <summary>Sender en enkelt mail. Implementeringen vælges ved opstart ud fra konfigurationen.</summary>
public interface IEmailSender
{
    /// <returns><c>true</c> hvis mailen blev afleveret til mailserveren.</returns>
    Task<bool> SendAsync(EmailMessage message, CancellationToken ct = default);
}

public sealed record EmailMessage(
    string ToEmail,
    string? ToName,
    string Subject,
    string BodyText,
    string? BodyHtml = null);

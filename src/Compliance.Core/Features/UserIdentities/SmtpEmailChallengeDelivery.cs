using System.Net;
using System.Net.Mail;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Sends through a STARTTLS SMTP relay using a stable message identity per challenge.</summary>
public sealed class SmtpEmailChallengeDelivery(EmailChallengeDeliverySettings settings)
    : IEmailChallengeDelivery
{
    public async ValueTask SendAsync(Uuid challengeId, Uuid userId, string emailAddress,
        string token, CancellationToken ct)
    {
        _ = userId;
        using var message = new MailMessage(settings.FromAddress!, emailAddress)
        {
            Subject = "Verify your Badgers email address",
            Body = $"Your Badgers email verification code is {token}. It expires in 15 minutes.\n",
        };
        var domain = new MailAddress(settings.FromAddress!).Host;
        message.Headers.Add("Message-ID",
            $"<bdgrz-email-{challengeId.ToString().Replace("-", "", StringComparison.Ordinal)}@{domain}>");
        using var client = new SmtpClient(settings.SmtpHost!, settings.SmtpPort)
        {
            EnableSsl = true,
            UseDefaultCredentials = false,
            Credentials = settings.Username is null ? null :
                new NetworkCredential(settings.Username, settings.Password),
        };
        try
        {
            await client.SendMailAsync(message, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // SMTP exceptions can echo recipient addresses or provider details. The worker
            // records only a generic failure code and retries the same effect identity.
            throw new InvalidOperationException("Email challenge delivery failed.");
        }
    }
}

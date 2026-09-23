using System.Net;
using System.Net.Mail;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>Sends application-managed invitations with a stable message identity per attempt.</summary>
public sealed class SmtpTenantInvitationDelivery(EmailChallengeDeliverySettings settings)
    : ITenantInvitationDelivery
{
    public async ValueTask SendAsync(Uuid attemptId, Uuid tenantId, string emailAddress,
        string token, CancellationToken ct)
    {
        try
        {
            using var message = new MailMessage(settings.FromAddress!, emailAddress)
            {
                Subject = "Your Badgers organization invitation",
                Body = $"Sign in and verify this email address to accept the invitation for organization {tenantId}. " +
                       $"Your invitation code is {token}. It expires in 7 days.\n",
            };
            var domain = new MailAddress(settings.FromAddress!).Host;
            message.Headers.Add("Message-ID",
                $"<bdgrz-invitation-{attemptId.ToString().Replace("-", "", StringComparison.Ordinal)}@{domain}>");
            using var client = new SmtpClient(settings.SmtpHost!, settings.SmtpPort)
            {
                EnableSsl = true,
                UseDefaultCredentials = false,
                Credentials = settings.Username is null ? null :
                    new NetworkCredential(settings.Username, settings.Password),
            };
            await client.SendMailAsync(message, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // SMTP failures may include recipient addresses or provider secrets.
            throw new InvalidOperationException("Invitation delivery failed.");
        }
    }
}

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
        await SendMessageAsync(challengeId, emailAddress, "email",
            "Verify your Badgers email address",
            $"Your Badgers email verification code is {token}. It expires in 15 minutes.\n",
            ct).ConfigureAwait(false);
    }

    public ValueTask SendRecoveryAsync(Uuid challengeId, Uuid userId, string emailAddress,
        string token, CancellationToken ct)
    {
        _ = userId;
        return SendMessageAsync(challengeId, emailAddress, "identity-recovery",
            "Secure your Badgers account",
            $"Your account identity recovery code is {token}. It expires in 15 minutes. " +
            "If you did not request this code, you can ignore this message.\n", ct);
    }

    public ValueTask SendRecoveryCompletedAsync(Uuid challengeId, Uuid userId,
        string emailAddress, CancellationToken ct)
    {
        _ = userId;
        return SendMessageAsync(challengeId, emailAddress, "identity-recovery-completed",
            "Your Badgers account identity was updated",
            "An identity linked to your Badgers account was replaced. If you did not make this " +
            "change, contact support and secure your email account.\n", ct);
    }

    async ValueTask SendMessageAsync(Uuid effectId, string emailAddress, string purpose,
        string subject, string body, CancellationToken ct)
    {
        using var message = new MailMessage(settings.FromAddress!, emailAddress)
        {
            Subject = subject,
            Body = body,
        };
        var domain = new MailAddress(settings.FromAddress!).Host;
        message.Headers.Add("Message-ID",
            $"<bdgrz-{purpose}-{effectId.ToString().Replace("-", "", StringComparison.Ordinal)}@{domain}>");
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

using Microsoft.Extensions.Configuration;

namespace Bdgrz.Compliance.Features.UserIdentities;

public sealed record EmailChallengeDeliverySettings(
    string Mode, string? SmtpHost, int SmtpPort, string? FromAddress,
    string? Username, string? Password)
{
    public static EmailChallengeDeliverySettings FromConfiguration(IConfiguration configuration,
        bool requireRealDelivery)
    {
        var section = configuration.GetSection("Compliance:EmailDelivery");
        var mode = section["Mode"]?.Trim().ToLowerInvariant() ?? "mock";
        if (requireRealDelivery && mode != "smtp")
            throw new InvalidOperationException("Production email challenge delivery must be configured.");
        if (mode == "mock" && !requireRealDelivery)
            return new EmailChallengeDeliverySettings(mode, null, 0, null, null, null);
        if (mode != "smtp")
            throw new InvalidOperationException("Unsupported email challenge delivery mode.");

        var host = section["Smtp:Host"];
        var from = section["Smtp:FromAddress"];
        var username = section["Smtp:Username"];
        var password = section["Smtp:Password"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from) ||
            !int.TryParse(section["Smtp:Port"], out var port) || port is < 1 or > 65535 ||
            string.IsNullOrEmpty(username) != string.IsNullOrEmpty(password))
            throw new InvalidOperationException("SMTP email challenge delivery configuration is incomplete.");
        if (!System.Net.Mail.MailAddress.TryCreate(from, out _))
            throw new InvalidOperationException("SMTP email challenge sender is invalid.");
        return new EmailChallengeDeliverySettings(mode, host, port, from, username, password);
    }
}

namespace Bdgrz.Compliance.Tests.Features.Tenants;

/// <summary>Isolates the process-wide SmtpClient TLS callback from every other test.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SmtpInvitationCollectionDefinition
{
    public const string Name = "SMTP invitation delivery";
}

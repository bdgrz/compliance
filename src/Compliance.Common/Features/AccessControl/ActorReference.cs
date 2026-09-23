using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>A durable identity and display snapshot for a member or named system process.</summary>
public sealed record ActorReference(string Kind, string Id, string Display)
{
    public static ActorReference ForMember(Uuid memberId, string display) =>
        new("member", memberId.ToString(), display);

    public static ActorReference ForSystemProcess(string processId, string display) =>
        new("system_process", processId, display);

    /// <summary>Reads a named Compliance process from Portia's durable event envelope.</summary>
    public static ActorReference? FromSystemMetadata(DomainEventMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        const string prefix = "reactor:";
        var actor = metadata.Actor;
        return actor is { Issuer: "bdgrz.system" } &&
               actor.Subject.StartsWith(prefix, StringComparison.Ordinal) &&
               actor.Subject.Length > prefix.Length
            ? ForSystemProcess(actor.Subject, actor.Subject[prefix.Length..])
            : null;
    }
}

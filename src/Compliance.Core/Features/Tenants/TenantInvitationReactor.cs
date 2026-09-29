using System.Diagnostics;
using Cntryl.Portia;
using Microsoft.Extensions.Logging;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed partial class TenantInvitationReactor(IProjectionCheckpointStore checkpoints, IRequestBus bus,
    ILogger<TenantInvitationReactor> logger)
    : Reactor(checkpoints, EventStreamPattern.ForTenant("tenant-invitations")),
      IReactorHandler<TenantInvitationAccepted>
{
    public async ValueTask HandleAsync(IReactorContext<TenantInvitationAccepted> context, CancellationToken ct)
    {
        await bus.SendReactionAsync(new RegisterMember(context.Trigger.TenantId, context.Trigger.UserId,
            context.Trigger.Affiliation), context, ct);
        var memberId = RbacIds.Member(context.Trigger.TenantId, context.Trigger.UserId);
        if (context.Trigger.BuiltInRole is { } role)
        {
            var teamId = BuiltInRbac.TeamIdForRole(context.Trigger.TenantId, role) ??
                throw new InvalidOperationException("The accepted invitation has an unknown built-in role.");
            await bus.SendReactionAsync(new AssignTeamMember(context.Trigger.TenantId,
                teamId, memberId), context, ct);
        }
        if (context.Trigger.Administrator)
        {
            await bus.SendReactionAsync(new AssignTeamMember(context.Trigger.TenantId,
                BuiltInRbac.AdministratorsTeamId(context.Trigger.TenantId), memberId), context, ct);
            var activation = new ActivateTenant(context.Trigger.TenantId,
                context.Trigger.UserId, context.Trigger.EmailAddress);
            var started = Stopwatch.GetTimestamp();
            var attempts = 0;
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    await bus.SendReactionAsync(activation, context, ct);
                    if (attempts > 0 && logger.IsEnabled(LogLevel.Information))
                    {
                        var elapsedMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                        LogActivationRecovered(logger, context.Trigger.TenantId, attempts + 1,
                            elapsedMilliseconds);
                    }
                    break;
                }
                catch (ReactionCommandFailedException failure) when (failure.Error.IsTransient)
                {
                    attempts++;
                    if (attempts == 1)
                        LogActivationWaiting(logger, context.Trigger.TenantId, failure.Error.Kind);
                    if (Stopwatch.GetElapsedTime(started) >= TimeSpan.FromSeconds(8))
                    {
                        LogActivationDeferred(logger, context.Trigger.TenantId, attempts,
                            failure.Error.Kind);
                        throw;
                    }

                    // A permission or membership projection can lag an accepted invitation.
                    // Retry briefly while the tenant workload stays leased. An extended lag
                    // returns to Portia's durable pass retry rather than holding this pass forever.
                    await Task.Delay(TimeSpan.FromMilliseconds(250), ct);
                }
            }
        }
    }

    [LoggerMessage(EventId = 41501, Level = LogLevel.Information,
        Message = "Tenant {TenantId} activation is waiting for provisioning after {ErrorKind}.")]
    static partial void LogActivationWaiting(ILogger logger, Uuid tenantId, RequestErrorKind errorKind);

    [LoggerMessage(EventId = 41502, Level = LogLevel.Warning,
        Message = "Tenant {TenantId} activation remains pending after {Attempts} attempts and {ErrorKind}; the reaction will replay.")]
    static partial void LogActivationDeferred(ILogger logger, Uuid tenantId, int attempts,
        RequestErrorKind errorKind);

    [LoggerMessage(EventId = 41503, Level = LogLevel.Information,
        Message = "Tenant {TenantId} activation recovered on attempt {Attempt} after {ElapsedMilliseconds} ms.")]
    static partial void LogActivationRecovered(ILogger logger, Uuid tenantId, int attempt,
        double elapsedMilliseconds);
}

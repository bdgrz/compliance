using System.Globalization;
using Cntryl.Fitz;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public interface IApplicationDirectoryReader
{
    ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default);
    ValueTask<ApplicationView?> GetAsync(Uuid tenantId, Uuid applicationId,
        CancellationToken ct = default);
    ValueTask<Page<ApplicationView>> ListAsync(Uuid tenantId, int limit, string? cursor,
        CancellationToken ct = default);
    ValueTask<ApplicationRevisionView?> GetRevisionAsync(Uuid tenantId, Uuid applicationId,
        long revision, CancellationToken ct = default);
    ValueTask<Page<ApplicationRevisionView>?> ListRevisionsAsync(Uuid tenantId,
        Uuid applicationId, int limit, string? cursor, CancellationToken ct = default);
    ValueTask<SystemInstanceView?> GetInstanceAsync(Uuid tenantId, Uuid instanceId,
        CancellationToken ct = default);
    ValueTask<Page<SystemInstanceView>> ListInstancesAsync(Uuid tenantId, Uuid applicationId,
        int limit, string? cursor, CancellationToken ct = default);
}

public interface IApplicationDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}

static class ApplicationDirectorySchema
{
    public static readonly KvDirectoryIndex<ApplicationView> ByName = new(
        "by_name", 1, static app => [app.Name.ToUpperInvariant(), app.ApplicationId.ToString()]);

    public static readonly KvDirectory<ApplicationView, Uuid> Applications = new(
        "applications", ComplianceCoreJsonContext.Default.ApplicationView,
        static app => app.ApplicationId, static id => [id.ToString()], [ByName]);

    public static readonly KvDirectoryIndex<ApplicationRevisionView> RevisionsByApplication =
        new("by_application", 1, static revision =>
            [revision.ApplicationId.ToString(),
                revision.Revision.ToString("D20", CultureInfo.InvariantCulture)]);

    public static readonly KvDirectory<ApplicationRevisionView, string> Revisions = new(
        "application_revisions", ComplianceCoreJsonContext.Default.ApplicationRevisionView,
        static revision => RevisionKey(revision.ApplicationId, revision.Revision),
        static key => [key], [RevisionsByApplication]);

    public static string RevisionKey(Uuid applicationId, long revision) =>
        $"{applicationId}:{revision.ToString("D20", CultureInfo.InvariantCulture)}";

    public static readonly KvDirectoryIndex<SystemInstanceView> ByApplication = new(
        "by_application", 1,
        static instance => [instance.ApplicationId.ToString(), instance.SystemInstanceId.ToString()]);

    public static readonly KvDirectory<SystemInstanceView, Uuid> Instances = new(
        "system_instances", ComplianceCoreJsonContext.Default.SystemInstanceView,
        static instance => instance.SystemInstanceId, static id => [id.ToString()], [ByApplication]);
}

sealed class FitzApplicationDirectory(IKvClient client)
    : FitzKvProjectionStore(client, "kv://bdgrz/application-directory-v2/projection",
          "ApplicationDirectoryV2"), IApplicationDirectoryReader, IApplicationDirectoryProjection
{
    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        switch (domainEvent)
        {
            case ApplicationDeclared declared:
                var declaredView = new ApplicationView(declared.TenantId,
                    declared.ApplicationId, 1, declared.Name, declared.Purpose,
                    declared.OwnerReference, "manual", declared.ApplicationId.ToString(),
                    false, Gaps(declared.OwnerReference, declared.Classification, false),
                    declared.ActorMemberId, declared.ActorDisplay, declared.ChangedAt)
                {
                    Classification = declared.Classification,
                };
                await ApplicationDirectorySchema.Applications.InsertAsync(Transaction,
                    declaredView, ct).ConfigureAwait(false);
                await InsertRevisionAsync(declaredView, "declared", null, ct)
                    .ConfigureAwait(false);
                break;
            case ApplicationRevised revised:
                var before = await RequireApplicationAsync(revised.ApplicationId, ct)
                    .ConfigureAwait(false);
                var revisedView = before with
                {
                    Revision = revised.Revision,
                    Name = revised.Name,
                    Purpose = revised.Purpose,
                    OwnerReference = revised.OwnerReference,
                    Classification = revised.Classification,
                    Unresolved = Gaps(revised.OwnerReference, revised.Classification,
                        before.HasSystemInstances),
                    LastChangedByMemberId = revised.ActorMemberId,
                    LastChangedByDisplay = revised.ActorDisplay,
                    LastChangedAt = revised.ChangedAt,
                };
                await ApplicationDirectorySchema.Applications.ReplaceAsync(Transaction, before,
                    revisedView, ct).ConfigureAwait(false);
                await InsertRevisionAsync(revisedView, "revised", null, ct).ConfigureAwait(false);
                break;
            case SystemInstanceDeclared instance:
                var application = await RequireApplicationAsync(instance.ApplicationId, ct)
                    .ConfigureAwait(false);
                var declaredInstance = InstanceView(instance.TenantId, instance.ApplicationId,
                    instance.SystemInstanceId, instance.Name, instance.Kind,
                    instance.AccessBoundaryReference, instance.SourceIdentifier,
                    instance.ActorMemberId, instance.ActorDisplay, instance.ChangedAt,
                    1, instance.ApplicationRevision);
                await ApplicationDirectorySchema.Instances.InsertAsync(Transaction,
                    declaredInstance, ct).ConfigureAwait(false);
                var instanceView = application with
                {
                    Revision = instance.ApplicationRevision,
                    HasSystemInstances = true,
                    Unresolved = Gaps(application.OwnerReference, application.Classification, true),
                    LastChangedByMemberId = instance.ActorMemberId,
                    LastChangedByDisplay = instance.ActorDisplay,
                    LastChangedAt = instance.ChangedAt,
                };
                await ApplicationDirectorySchema.Applications.ReplaceAsync(Transaction, application,
                    instanceView, ct).ConfigureAwait(false);
                await InsertRevisionAsync(instanceView, "system_instance_declared",
                    declaredInstance, ct).ConfigureAwait(false);
                break;
            case SystemInstanceRegistered registered:
                var parent = await RequireApplicationAsync(registered.ApplicationId, ct)
                    .ConfigureAwait(false);
                var registeredInstance = InstanceView(registered.TenantId,
                    registered.ApplicationId, registered.SystemInstanceId, registered.Name,
                    registered.Kind, registered.AccessBoundaryReference,
                    registered.SourceIdentifier, registered.ActorMemberId,
                    registered.ActorDisplay, registered.ChangedAt, registered.Revision, null);
                await ApplicationDirectorySchema.Instances.InsertAsync(Transaction,
                    registeredInstance, ct).ConfigureAwait(false);
                if (!parent.HasSystemInstances)
                    await ApplicationDirectorySchema.Applications.ReplaceAsync(Transaction,
                        parent, parent with
                        {
                            HasSystemInstances = true,
                            Unresolved = Gaps(parent.OwnerReference, parent.Classification, true),
                        }, ct).ConfigureAwait(false);
                break;
        }
    }

    static SystemInstanceView InstanceView(Uuid tenantId, Uuid applicationId,
        Uuid instanceId, string name, string kind, string? accessBoundaryReference,
        string? sourceIdentifier, Uuid actorMemberId, string actorDisplay,
        DateTimeOffset changedAt, long revision, long? legacyApplicationRevision) =>
        new(tenantId, applicationId, instanceId, name, kind, accessBoundaryReference,
            "manual", sourceIdentifier,
            [accessBoundaryReference is null
                    ? "access_boundary_missing" : "access_boundary_unverified",
                sourceIdentifier is null
                    ? "source_identifier_missing" : "source_identifier_unverified"],
            actorMemberId, actorDisplay, changedAt)
        {
            Revision = revision,
            LegacyApplicationRevision = legacyApplicationRevision,
        };

    async ValueTask InsertRevisionAsync(ApplicationView view, string changeKind,
        SystemInstanceView? systemInstance, CancellationToken ct) =>
        await ApplicationDirectorySchema.Revisions.InsertAsync(Transaction,
            new ApplicationRevisionView(view.TenantId, view.ApplicationId, view.Revision,
                view.Name, view.Purpose, view.OwnerReference, view.SourceKind,
                view.SourceIdentifier, view.HasSystemInstances, view.Unresolved,
                view.LastChangedByMemberId, view.LastChangedByDisplay, view.LastChangedAt,
                changeKind, systemInstance?.SystemInstanceId, systemInstance)
            {
                Classification = view.Classification,
            }, ct)
            .ConfigureAwait(false);

    static IReadOnlyList<string> Gaps(string? owner, string? classification, bool hasInstances) =>
        [string.IsNullOrWhiteSpace(owner) ? "owner_missing" : "owner_unverified",
            string.IsNullOrWhiteSpace(classification)
                ? "classification_unresolved" : "classification_unverified",
            hasInstances ? "access_boundary_review_pending" : "system_instances_missing"];

    async ValueTask<ApplicationView> RequireApplicationAsync(Uuid applicationId, CancellationToken ct) =>
        await ApplicationDirectorySchema.Applications.GetAsync(Transaction, applicationId, ct)
            .ConfigureAwait(false) ?? throw new InvalidOperationException(
            "An application change cannot project before its declaration.");

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity("ApplicationDirectoryV2",
            EventStreamPattern.ForPattern(tenantId.ToString())), ct);

    public async ValueTask<ApplicationView?> GetAsync(Uuid tenantId, Uuid applicationId,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await ApplicationDirectorySchema.Applications.GetAsync(tx, applicationId, ct)
            .ConfigureAwait(false);
    }

    public async ValueTask<Page<ApplicationView>> ListAsync(Uuid tenantId, int limit,
        string? cursor, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await ApplicationDirectorySchema.Applications.QueryAsync(tx,
            ApplicationDirectorySchema.ByName.Query().Take(limit).After(cursor), ct)
            .ConfigureAwait(false);
    }

    public async ValueTask<ApplicationRevisionView?> GetRevisionAsync(Uuid tenantId,
        Uuid applicationId, long revision, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await ApplicationDirectorySchema.Revisions.GetAsync(tx,
            ApplicationDirectorySchema.RevisionKey(applicationId, revision), ct)
            .ConfigureAwait(false);
    }

    public async ValueTask<Page<ApplicationRevisionView>?> ListRevisionsAsync(Uuid tenantId,
        Uuid applicationId, int limit, string? cursor, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        if (await ApplicationDirectorySchema.Applications.GetAsync(tx, applicationId, ct)
                .ConfigureAwait(false) is null)
            return null;
        return await ApplicationDirectorySchema.Revisions.QueryAsync(tx,
            ApplicationDirectorySchema.RevisionsByApplication.Query()
                .WithPrefix(applicationId.ToString()).Take(Math.Clamp(limit, 1, 200))
                .After(cursor), ct).ConfigureAwait(false);
    }

    public async ValueTask<SystemInstanceView?> GetInstanceAsync(Uuid tenantId,
        Uuid instanceId, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await ApplicationDirectorySchema.Instances.GetAsync(tx, instanceId, ct)
            .ConfigureAwait(false);
    }

    public async ValueTask<Page<SystemInstanceView>> ListInstancesAsync(Uuid tenantId,
        Uuid applicationId, int limit, string? cursor, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await ApplicationDirectorySchema.Instances.QueryAsync(tx,
            ApplicationDirectorySchema.ByApplication.Query().WithPrefix(applicationId.ToString())
                .Take(limit).After(cursor), ct).ConfigureAwait(false);
    }
}

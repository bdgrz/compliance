using System.Globalization;
using Cntryl.Fitz;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public interface IApplicationDirectoryReader
{
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
    : FitzKvProjectionStore(client, "kv://bdgrz/application-directory/projection",
          "ApplicationDirectory"), IApplicationDirectoryReader, IApplicationDirectoryProjection
{
    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        switch (domainEvent)
        {
            case ApplicationDeclared declared:
                var declaredView = new ApplicationView(declared.TenantId,
                    declared.ApplicationId, 1, declared.Name, declared.Purpose,
                    declared.OwnerReference, "manual", declared.ApplicationId.ToString(),
                    false, Gaps(declared.OwnerReference, false), declared.ActorMemberId,
                    declared.ActorDisplay, declared.ChangedAt);
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
                    Unresolved = Gaps(revised.OwnerReference, before.HasSystemInstances),
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
                var declaredInstance = new SystemInstanceView(instance.TenantId,
                    instance.ApplicationId, instance.SystemInstanceId, instance.Name,
                    instance.Kind, instance.AccessBoundaryReference, "manual",
                    instance.SourceIdentifier,
                    [instance.AccessBoundaryReference is null
                            ? "access_boundary_missing" : "access_boundary_unverified",
                        instance.SourceIdentifier is null
                            ? "source_identifier_missing" : "source_identifier_unverified"],
                    instance.ActorMemberId, instance.ActorDisplay, instance.ChangedAt);
                await ApplicationDirectorySchema.Instances.InsertAsync(Transaction,
                    declaredInstance, ct).ConfigureAwait(false);
                var instanceView = application with
                {
                    Revision = instance.ApplicationRevision,
                    HasSystemInstances = true,
                    Unresolved = Gaps(application.OwnerReference, true),
                    LastChangedByMemberId = instance.ActorMemberId,
                    LastChangedByDisplay = instance.ActorDisplay,
                    LastChangedAt = instance.ChangedAt,
                };
                await ApplicationDirectorySchema.Applications.ReplaceAsync(Transaction, application,
                    instanceView, ct).ConfigureAwait(false);
                await InsertRevisionAsync(instanceView, "system_instance_declared",
                    declaredInstance, ct).ConfigureAwait(false);
                break;
        }
    }

    async ValueTask InsertRevisionAsync(ApplicationView view, string changeKind,
        SystemInstanceView? systemInstance, CancellationToken ct) =>
        await ApplicationDirectorySchema.Revisions.InsertAsync(Transaction,
            new ApplicationRevisionView(view.TenantId, view.ApplicationId, view.Revision,
                view.Name, view.Purpose, view.OwnerReference, view.SourceKind,
                view.SourceIdentifier, view.HasSystemInstances, view.Unresolved,
                view.LastChangedByMemberId, view.LastChangedByDisplay, view.LastChangedAt,
                changeKind, systemInstance?.SystemInstanceId, systemInstance), ct)
            .ConfigureAwait(false);

    static IReadOnlyList<string> Gaps(string? owner, bool hasInstances) =>
        [string.IsNullOrWhiteSpace(owner) ? "owner_missing" : "owner_unverified",
            "classification_unresolved",
            hasInstances ? "access_boundary_review_pending" : "system_instances_missing"];

    async ValueTask<ApplicationView> RequireApplicationAsync(Uuid applicationId, CancellationToken ct) =>
        await ApplicationDirectorySchema.Applications.GetAsync(Transaction, applicationId, ct)
            .ConfigureAwait(false) ?? throw new InvalidOperationException(
            "An application change cannot project before its declaration.");

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

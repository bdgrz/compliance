using System.Text.Json;
using System.Text.Json.Nodes;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Applications;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationActorSnapshotTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ApplicationId = Uuid.CreateVersion4();
    static readonly Uuid MemberId = Uuid.CreateVersion4();
    static readonly DateTimeOffset At = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldReplayLegacyApplicationEventsWithMemberActorGivenEventsWithoutActor()
    {
        // Arrange
        var declared = Deserialize<ApplicationDeclared>($$"""
            {"tenant_id":"{{TenantId}}","application_id":"{{ApplicationId}}","name":"Payroll","purpose":"Run payroll","owner_reference":null,"actor_member_id":"{{MemberId}}","actor_display":"First display","changed_at":"2026-09-27T12:00:00+00:00","classification":null,"system_owner_person_id":null,"access_owner_person_id":null}
            """);
        var revised = Deserialize<ApplicationRevised>($$"""
            {"tenant_id":"{{TenantId}}","application_id":"{{ApplicationId}}","revision":2,"name":"Payroll","purpose":"Run monthly payroll","owner_reference":null,"actor_member_id":"{{MemberId}}","actor_display":"Second display","changed_at":"2026-09-27T12:01:00+00:00","classification":null,"system_owner_person_id":null,"access_owner_person_id":null}
            """);
        var legacyInstance = Deserialize<SystemInstanceDeclared>($$"""
            {"tenant_id":"{{TenantId}}","application_id":"{{ApplicationId}}","system_instance_id":"{{Uuid.CreateVersion4()}}","application_revision":3,"name":"Production","kind":"production","access_boundary_reference":null,"source_identifier":"payroll-prod","actor_member_id":"{{MemberId}}","actor_display":"Legacy instance actor","changed_at":"2026-09-27T12:02:00+00:00"}
            """);
        var registered = Deserialize<SystemInstanceRegistered>($$"""
            {"tenant_id":"{{TenantId}}","application_id":"{{ApplicationId}}","system_instance_id":"{{Uuid.CreateVersion4()}}","revision":1,"name":"Staging","kind":"staging","access_boundary_reference":null,"source_identifier":"payroll-stage","actor_member_id":"{{MemberId}}","actor_display":"Registered instance actor","changed_at":"2026-09-27T12:03:00+00:00"}
            """);

        // Act
        var actors = new[] { declared.Actor, revised.Actor, legacyInstance.Actor,
            registered.Actor };

        // Assert
        Assert.All(new[] { declared.StoredActor, revised.StoredActor,
            legacyInstance.StoredActor, registered.StoredActor }, Assert.Null);
        Assert.Equal([
            ActorReference.ForMember(MemberId, "First display"),
            ActorReference.ForMember(MemberId, "Second display"),
            ActorReference.ForMember(MemberId, "Legacy instance actor"),
            ActorReference.ForMember(MemberId, "Registered instance actor"),
        ], actors);
    }

    [Fact]
    public void ShouldExposeMemberActorGivenLegacyApplicationViews()
    {
        // Arrange
        var application = Deserialize<ApplicationView>($$"""
            {"tenant_id":"{{TenantId}}","application_id":"{{ApplicationId}}","revision":1,"name":"Payroll","purpose":"Run payroll","owner_reference":null,"source_kind":"manual","source_identifier":"{{ApplicationId}}","has_system_instances":false,"unresolved":[],"last_changed_by_member_id":"{{MemberId}}","last_changed_by_display":"Application actor","last_changed_at":"2026-09-27T12:00:00+00:00"}
            """);
        var revision = Deserialize<ApplicationRevisionView>($$"""
            {"tenant_id":"{{TenantId}}","application_id":"{{ApplicationId}}","revision":1,"name":"Payroll","purpose":"Run payroll","owner_reference":null,"source_kind":"manual","source_identifier":"{{ApplicationId}}","has_system_instances":false,"unresolved":[],"last_changed_by_member_id":"{{MemberId}}","last_changed_by_display":"Revision actor","last_changed_at":"2026-09-27T12:00:00+00:00","change_kind":"declared","system_instance_id":null,"system_instance":null}
            """);
        var instance = Deserialize<SystemInstanceView>($$"""
            {"tenant_id":"{{TenantId}}","application_id":"{{ApplicationId}}","system_instance_id":"{{Uuid.CreateVersion4()}}","name":"Production","kind":"production","access_boundary_reference":null,"source_kind":"manual","source_identifier":"payroll-prod","unresolved":[],"declared_by_member_id":"{{MemberId}}","declared_by_display":"Instance actor","declared_at":"2026-09-27T12:00:00+00:00","revision":1,"legacy_application_revision":null}
            """);

        // Act
        var applicationJson = JsonNode.Parse(JsonSerializer.Serialize(application,
            ComplianceCoreJsonContext.Default.ApplicationView))!;
        var revisionJson = JsonNode.Parse(JsonSerializer.Serialize(revision,
            ComplianceCoreJsonContext.Default.ApplicationRevisionView))!;
        var instanceJson = JsonNode.Parse(JsonSerializer.Serialize(instance,
            ComplianceCoreJsonContext.Default.SystemInstanceView))!;

        // Assert
        AssertActor(applicationJson["last_changed_by"], "Application actor");
        AssertActor(revisionJson["actor"], "Revision actor");
        AssertActor(instanceJson["declared_by"], "Instance actor");
    }

    static void AssertActor(JsonNode? node, string display)
    {
        Assert.Equal("member", (string?)node?["kind"]);
        Assert.Equal(MemberId.ToString(), (string?)node?["id"]);
        Assert.Equal(display, (string?)node?["display"]);
    }

    static T Deserialize<T>(string json) where T : class =>
        Assert.IsType<T>(JsonSerializer.Deserialize(json,
            ComplianceCoreJsonContext.Default.GetTypeInfo(typeof(T))!));
}

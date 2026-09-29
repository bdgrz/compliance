using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Responsibilities;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.E2E;

static class MemberResponsibilityReadiness
{
    public static async Task<JsonElement> WaitForAsync(IServiceProvider workerServices,
        HttpClient administrator, Uuid tenantId, Uuid boundaryId, Uuid versionId,
        Uuid memberId, string path)
    {
        var responsibilityScope = new ResponsibilityScope("boundary", boundaryId, versionId, 1);
        var checkpointIdentity = new CheckpointIdentity("MemberResponsibilitiesV1",
            EventStreamPattern.ForPattern(tenantId.ToString()));
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        var sourceAssignment = Uuid.Empty;
        var workerCheckpointAdvanced = false;
        var indexed = false;
        var lastHttpStatus = HttpStatusCode.NotFound;
        while (DateTimeOffset.UtcNow < deadline)
        {
            await using (var scope = workerServices.CreateAsyncScope())
            {
                var source = await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
                    .HydrateAsync(new SystemBoundary(tenantId, boundaryId));
                sourceAssignment = source.GetResponsibilitySet(responsibilityScope)
                    .ReadAssignments().FirstOrDefault(item => item.MemberId == memberId)
                    ?.AssignmentId ?? Uuid.Empty;
                workerCheckpointAdvanced = await scope.ServiceProvider
                    .GetRequiredService<IProjectionCheckpointStore>().LoadAsync(checkpointIdentity) !=
                    ProjectionCheckpoint.Start;
                indexed = sourceAssignment != Uuid.Empty &&
                    (await scope.ServiceProvider.GetRequiredService<IMemberResponsibilityIndex>()
                        .GetAsync(tenantId, memberId))
                    .Any(item => item.AssignmentId == sourceAssignment);
            }
            using var response = await administrator.GetAsync(path);
            lastHttpStatus = response.StatusCode;
            if (sourceAssignment != Uuid.Empty && workerCheckpointAdvanced && indexed &&
                lastHttpStatus == HttpStatusCode.OK)
            {
                var document = await response.Content.ReadFromJsonAsync<JsonElement>();
                if (document.ValueKind == JsonValueKind.Array && document.GetArrayLength() == 1 &&
                    document[0].GetProperty("assignment_id").GetString() == sourceAssignment.ToString())
                    return document;
            }
            await Task.Delay(250);
        }
        throw new TimeoutException("Member responsibility readback did not become ready. " +
            $"sourceAssignment={sourceAssignment}; workerCheckpointAdvanced={workerCheckpointAdvanced}; " +
            $"indexed={indexed}; lastHttpStatus={lastHttpStatus}; path={path}.");
    }
}

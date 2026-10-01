namespace Bdgrz.Compliance.Features.Workforce;

public static class WorkforceSourceReconciliation
{
    public static bool HasBoundedFacts(WorkforceSourceFacts facts) =>
        (facts.Person is not { } person || person.DisplayName.Length <= 200 &&
            (person.WorkEmail?.Length ?? 0) <= 320) &&
        (facts.WorkRelationship is not { } job || (job.WorkerType?.Length ?? 0) <= 50 &&
            (job.LifecycleStatus?.Length ?? 0) <= 50 && (job.Department?.Length ?? 0) <= 200 &&
            (job.EmploymentStatusReason?.Length ?? 0) <= 1000) &&
        (facts.ServiceIdentity is not { } identity || identity.DisplayName.Length <= 200 &&
            (identity.IdentityKind?.Length ?? 0) <= 50 && (identity.Environment?.Length ?? 0) <= 100 &&
            (identity.LifecycleStatus?.Length ?? 0) <= 50);

    public static WorkforceSourcePreview Preview(WorkforceSourceObserved observed, long revision,
        long currentTargetRevision, WorkforceSourceFacts currentFacts, WorkforceSourceDecision? decision)
    {
        var facts = Normalize(observed.Facts);
        var current = Normalize(currentFacts);
        var fields = new List<string>();
        void Compare<T>(string field, T expected, T actual)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                fields.Add(field);
        }
        var restrictedConflict = false;
        if (facts.Person is { } person && current.Person is { } currentPerson)
        {
            Compare("display_name", person.DisplayName, currentPerson.DisplayName);
            Compare("work_email", person.WorkEmail, currentPerson.WorkEmail);
        }
        if (facts.WorkRelationship is { } job && current.WorkRelationship is { } currentJob)
        {
            Compare("worker_type", job.WorkerType, currentJob.WorkerType);
            Compare("lifecycle_status", job.LifecycleStatus, currentJob.LifecycleStatus);
            Compare("start_date", job.StartDate, currentJob.StartDate);
            Compare("end_date", job.EndDate, currentJob.EndDate);
            Compare("department", job.Department, currentJob.Department);
            Compare("sponsor_person_id", job.SponsorPersonId, currentJob.SponsorPersonId);
            restrictedConflict = job.ManagerPersonId != currentJob.ManagerPersonId ||
                                 job.EmploymentStatusReason != currentJob.EmploymentStatusReason;
        }
        if (facts.ServiceIdentity is { } identity && current.ServiceIdentity is { } currentIdentity)
        {
            Compare("display_name", identity.DisplayName, currentIdentity.DisplayName);
            Compare("identity_kind", identity.IdentityKind, currentIdentity.IdentityKind);
            Compare("environment", identity.Environment, currentIdentity.Environment);
            Compare("lifecycle_status", identity.LifecycleStatus, currentIdentity.LifecycleStatus);
            Compare("expires_on", identity.ExpiresOn, currentIdentity.ExpiresOn);
        }
        return new WorkforceSourcePreview(observed.ObservationId, revision, observed.Source,
            observed.TargetKind, observed.TargetId, observed.ObservedTargetRevision,
            currentTargetRevision, observed.Source.SourceKind == "hris" ? "authoritative" : "corroborating",
            fields, restrictedConflict, decision is null && facts == current,
            decision?.Outcome == "accepted" && decision.TargetRevision == currentTargetRevision, decision);
    }

    static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static WorkforceSourceFacts Normalize(WorkforceSourceFacts facts) => facts with
    {
        Person = facts.Person is { } person ? person with
        {
            DisplayName = Clean(person.DisplayName) ?? string.Empty,
            WorkEmail = Clean(person.WorkEmail),
        } : null,
        WorkRelationship = facts.WorkRelationship is { } relationship ? relationship with
        {
            Department = Clean(relationship.Department),
            EmploymentStatusReason = Clean(relationship.EmploymentStatusReason),
        } : null,
        ServiceIdentity = facts.ServiceIdentity is { } identity ? identity with
        {
            DisplayName = Clean(identity.DisplayName) ?? string.Empty,
            Environment = Clean(identity.Environment),
        } : null,
    };
}

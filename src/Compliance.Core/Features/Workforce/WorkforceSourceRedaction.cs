namespace Bdgrz.Compliance.Features.Workforce;

static class WorkforceSourceRedaction
{
    public static WorkforceSourceView Apply(WorkforceSourceView view, bool canReadManager, bool canReadReason) =>
        view.Facts.WorkRelationship is { } relationship ? view with
        {
            Facts = view.Facts with
            {
                WorkRelationship = relationship with
                {
                    ManagerPersonId = canReadManager ? relationship.ManagerPersonId : null,
                    EmploymentStatusReason = canReadReason ? relationship.EmploymentStatusReason : null,
                },
            },
            RestrictedFieldsRedacted = !canReadManager || !canReadReason,
        } : view;
}

import { state } from '@askrjs/askr';
import { resource } from '@askrjs/askr/resources';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
  Page,
  PageHeader,
  Spinner,
  Stack,
} from '@askrjs/themes/components';

import { listMembers, memberLabel, type MemberSummary } from '../../members/members.js';
import { organizationPath } from '../../tenants/tenants.js';
import { ResolutionSummary, ResolveObservationForm } from '../observation-resolution.js';
import {
  listPeople,
  listReconciliationObservations,
  observationStatuses,
  optionLabel,
  reconciliationKinds,
  reconciliationReasonLabel,
  sourcePrecedence,
  WorkforceRequestError,
  type ReconciliationObservation,
} from '../workforce.js';
import { inputValue, LoadFailure, WorkforceForbidden, WorkforceSections } from '../workforce-shared.js';

function Subjects({
  item,
  names,
  members,
}: {
  item: ReconciliationObservation;
  names: Map<string, string>;
  members: Map<string, MemberSummary>;
}) {
  return (
    <ul className="plain-list workforce-subjects">
      {item.personIds.map((personId) => (
        <li>
          <a href={organizationPath(`/workforce/people/${personId}`)}>{names.get(personId) ?? 'Unknown person'}</a>
        </li>
      ))}
      {item.relationshipIds.map((relationshipId) => (
        <li>
          <a href={organizationPath(`/workforce/relationships/${relationshipId}`)}>Work relationship</a>
        </li>
      ))}
      {item.userIds.map((userId) => (
        <li>
          <a href={organizationPath(`/members/${userId}`)}>{memberLabel(members.get(userId), userId)}</a>
        </li>
      ))}
    </ul>
  );
}

export function WorkforceReconciliationPage() {
  const [kind, setKind] = state('');
  const [status, setStatus] = state('open');
  const [version, setVersion] = state(0);
  const [notice, setNotice] = state<string | null>(null);
  const observations = resource(
    () => listReconciliationObservations(kind() || null, status() || null),
    [kind(), status(), version()]
  );
  const people = resource(() => listPeople().catch(() => []), [version()]);
  const members = resource(() => listMembers().catch(() => [] as MemberSummary[]), []);

  if (observations.error instanceof WorkforceRequestError && observations.error.status === 403) {
    return <WorkforceForbidden title="Roster reconciliation is not available to you" />;
  }

  const names = new Map((people.value ?? []).map((person) => [person.personId, person.displayName]));
  const memberIndex = new Map((members.value ?? []).map((member) => [member.userId, member]));
  const items = observations.value ?? [];

  return (
    <Page>
      <PageHeader
        title="Roster reconciliation"
        description="Where the workforce roster and organization membership disagree."
      />
      <Stack gap="md">
        <WorkforceSections current="/workforce/reconciliation" />
        <p className="workforce-callout" role="note">
          Findings are for an attributed decision. Closing one never grants, changes, or revokes anyone's access;
          suspend a member or end a work relationship separately.
        </p>
        <Card>
          <CardHeader>
            <CardTitle>Source precedence</CardTitle>
            <CardDescription>
              Which source wins when roster sources disagree, highest first. Conflicts between sources are not raised
              until a second source is connected.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <ol className="workforce-precedence">
              {sourcePrecedence.map((source) => (
                <li>
                  <strong>{source.label}</strong>
                  {source.kind === 'manual' ? <span className="workforce-active-source"> (in use)</span> : null}:{' '}
                  {source.role}
                </li>
              ))}
            </ol>
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>Findings</CardTitle>
            <CardDescription>
              Missing, duplicate, conflicting, stale, and access-only records, evaluated against current memberships.
              Correlate a person with their member on the person's page.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <Stack gap="sm">
              {notice() ? <p role="status">{notice()}</p> : null}
              <div className="workforce-filters">
                <label className="registration-field">
                  <span>Kind</span>
                  <select value={kind()} onChange={(event: Event) => setKind(inputValue(event))}>
                    <option value="">All kinds</option>
                    {reconciliationKinds.map((option) => (
                      <option value={option.value}>{option.label}</option>
                    ))}
                  </select>
                </label>
                <label className="registration-field">
                  <span>Status</span>
                  <select value={status()} onChange={(event: Event) => setStatus(inputValue(event))}>
                    <option value="">Any status</option>
                    {observationStatuses.map((option) => (
                      <option value={option.value}>{option.label}</option>
                    ))}
                  </select>
                </label>
              </div>
              {observations.pending && !observations.value ? (
                <Spinner label="Loading reconciliation findings" />
              ) : observations.error ? (
                <LoadFailure error={observations.error} onRetry={() => observations.refresh()} />
              ) : items.length === 0 ? (
                <p>
                  {status() === 'open' && !kind()
                    ? 'The roster and membership agree. No open findings.'
                    : 'No findings match these filters.'}
                </p>
              ) : (
                <ul className="plain-list workforce-findings">
                  {items.map((item) => (
                    <li className="workforce-finding">
                      <p>
                        <span className={`workforce-kind workforce-recon-${item.kind}`}>
                          {optionLabel(reconciliationKinds, item.kind)}
                        </span>{' '}
                        <strong>{reconciliationReasonLabel(item.reason)}</strong> ·{' '}
                        {optionLabel(observationStatuses, item.status)}
                      </p>
                      <Subjects item={item} names={names} members={memberIndex} />
                      {item.resolution ? (
                        <ResolutionSummary resolution={item.resolution} />
                      ) : item.status === 'open' ? (
                        <details className="workforce-close">
                          <summary>Close this finding…</summary>
                          <ResolveObservationForm
                            observationId={item.observationId}
                            label={reconciliationReasonLabel(item.reason).toLowerCase()}
                            onClosed={(message) => {
                              setNotice(message);
                              setVersion(version() + 1);
                            }}
                          />
                        </details>
                      ) : null}
                    </li>
                  ))}
                </ul>
              )}
            </Stack>
          </CardContent>
        </Card>
      </Stack>
    </Page>
  );
}

import { state } from '@askrjs/askr';
import { resource } from '@askrjs/askr/resources';
import {
  Button,
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

import { organizationPath } from '../../tenants/tenants.js';
import { RelationshipFields } from '../relationship-fields.js';
import {
  formatDate,
  getWorkRelationship,
  lifecycleStatuses,
  listPeople,
  optionLabel,
  reviseWorkRelationship,
  sourceKindLabel,
  workerTypes,
  type Person,
  type WorkRelationship,
  type WorkRelationshipTerms,
} from '../workforce.js';
import { ActionError, RecordFailure } from '../workforce-shared.js';
import { SourceObservationsPanel } from '../source-record.js';

function RelationshipEditor({
  relationship,
  people,
  onSaved,
}: {
  relationship: WorkRelationship;
  people: Person[];
  onSaved: () => void;
}) {
  const [terms, setTerms] = state<WorkRelationshipTerms>({
    workerType: relationship.workerType,
    lifecycleStatus: relationship.lifecycleStatus,
    startDate: relationship.startDate,
    endDate: relationship.endDate,
    department: relationship.department,
    managerPersonId: relationship.managerPersonId,
    sponsorPersonId: relationship.sponsorPersonId,
    employmentStatusReason: relationship.employmentStatusReason,
  });
  const [pending, setPending] = state(false);
  const [error, setError] = state<Error | null>(null);
  const [notice, setNotice] = state<string | null>(null);

  async function save(event: Event) {
    event.preventDefault();
    setError(null);
    setNotice(null);
    setPending(true);
    try {
      await reviseWorkRelationship(relationship.relationshipId, relationship.revision, terms());
      setNotice(terms().lifecycleStatus === 'ended' ? 'Leaver recorded.' : 'Work relationship saved.');
      onSaved();
    } catch (failure) {
      setError(failure instanceof Error ? failure : new Error('Unable to save this work relationship.'));
    } finally {
      setPending(false);
    }
  }

  const others = people.filter((person) => person.personId !== relationship.personId);
  return (
    <form onSubmit={(event: Event) => void save(event)}>
      <Stack gap="sm">
        <RelationshipFields terms={terms()} people={others} onChange={setTerms} showManager />
        <ActionError error={error()} noun="work relationship" />
        {notice() ? <p role="status">{notice()}</p> : null}
        <Button variant="primary" type="submit" disabled={pending()}>
          {pending() ? 'Saving…' : 'Save work relationship'}
        </Button>
      </Stack>
    </form>
  );
}

export function WorkRelationshipDetailPage({ relationshipId }: { relationshipId: string }) {
  const [version, setVersion] = state(0);
  const relationship = resource(() => getWorkRelationship(relationshipId), [relationshipId, version()]);
  const people = resource(() => listPeople(), [version()]);

  if (relationship.pending && !relationship.value) {
    return (
      <Page>
        <Spinner label="Loading work relationship" />
      </Page>
    );
  }

  if (relationship.error) {
    return (
      <RecordFailure
        error={relationship.error}
        noun="work relationship"
        backPath="/workforce"
        backLabel="Back to workforce"
        onRetry={() => relationship.refresh()}
      />
    );
  }

  const current = relationship.value!;
  const names = new Map((people.value ?? []).map((person) => [person.personId, person.displayName]));
  const nameOf = (id: string | null) => (id ? (names.get(id) ?? 'Unknown person') : 'None');

  return (
    <Page>
      <PageHeader
        title={`Worker ${current.sourceWorkerId}`}
        description={`${nameOf(current.personId)} · ${optionLabel(lifecycleStatuses, current.lifecycleStatus)} · revision ${current.revision}`}
      />
      <Stack gap="md">
        <a href={organizationPath('/workforce')}>Back to workforce</a>
        <Card>
          <CardHeader>
            <CardTitle>Current terms</CardTitle>
            <CardDescription>
              {sourceKindLabel(current.sourceKind)}. Last changed by {current.lastChangedBy} on{' '}
              {new Date(current.lastChangedAt).toLocaleString()}.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <dl className="workforce-terms">
              <dt>Person</dt>
              <dd>{nameOf(current.personId)}</dd>
              <dt>Worker type</dt>
              <dd>{optionLabel(workerTypes, current.workerType)}</dd>
              <dt>Dates</dt>
              <dd>
                {formatDate(current.startDate)}
                {current.endDate ? ` – ${formatDate(current.endDate)}` : ''}
              </dd>
              <dt>Manager (restricted)</dt>
              <dd>{current.restrictedFieldsRedacted ? 'Restricted' : nameOf(current.managerPersonId)}</dd>
              <dt>Sponsor</dt>
              <dd>{nameOf(current.sponsorPersonId)}</dd>
              <dt>Employment status reason (restricted)</dt>
              <dd>{current.employmentStatusReason ?? (current.restrictedFieldsRedacted ? 'No value disclosed' : 'Not set')}</dd>
            </dl>
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>Revise terms</CardTitle>
            <CardDescription>To record a leaver, set the status to Ended and give the end date.</CardDescription>
          </CardHeader>
          <CardContent>
            {current.restrictedFieldsRedacted ? (
              <p>Restricted details must be available before editing all terms, so hidden manager and employment status reason values are preserved. Ask for both workforce field read grants.</p>
            ) : people.pending && !people.value ? (
              <Spinner label="Loading people" />
            ) : (
              <RelationshipEditor
                relationship={current}
                people={people.value ?? []}
                onSaved={() => setVersion(version() + 1)}
              />
            )}
          </CardContent>
        </Card>
        <SourceObservationsPanel restrictedFieldsRedacted={current.restrictedFieldsRedacted} target={{ kind: 'work_relationship', id: current.relationshipId, revision: current.revision, facts: { work_relationship: {
          worker_type: current.workerType, lifecycle_status: current.lifecycleStatus, start_date: current.startDate,
          end_date: current.endDate, department: current.department, manager_person_id: current.managerPersonId,
          sponsor_person_id: current.sponsorPersonId, employment_status_reason: current.employmentStatusReason ?? null,
        } } }} />
      </Stack>
    </Page>
  );
}

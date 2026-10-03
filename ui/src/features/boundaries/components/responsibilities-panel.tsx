import { state } from '@askrjs/askr';
import { Button, Card, CardContent, CardDescription, CardHeader, CardTitle, Spinner, Stack } from '@askrjs/themes/components';

import { organizationPath } from '../../tenants/tenants.js';
import { label } from '../boundaries.js';
import {
  assignResponsibility,
  previewConflicts,
  responsibilityTypes,
  revokeResponsibility,
  shortId,
  type Assignment,
  type Conflict,
  type MemberOption,
  type ResponsibilityScope,
  type ResponsibilityType,
} from '../responsibilities.js';
import { describeBoundaryFailure } from './boundary-form.js';

export interface AssignmentsState {
  pending: boolean;
  error: Error | null | undefined;
  value: { revision: number; assignments: Assignment[] } | null | undefined;
  refresh: () => void;
}

export function conflictSentence(conflict: Conflict): string {
  return `${label(conflict.kind)}: the same member is ${label(conflict.existingType)} and ${label(conflict.proposedType)}; a second administrator must approve a ${conflict.waiverAction} exception before this member can ${conflict.waiverAction}.`;
}

function waiverIdsFrom(text: string): string[] {
  return text
    .split(/[\s,]+/)
    .map((value) => value.trim())
    .filter((value) => value !== '');
}

// Responsibilities on the exact draft revision. Conflicts are previewed before assignment and the
// assignment list feeds the standing-conflict summary shown ahead of review and approval.
export function ResponsibilitiesPanel({
  programId,
  scope,
  members,
  assignments,
  onChanged,
  waiverPath: waiverPathOverride,
}: {
  programId: string;
  scope: ResponsibilityScope;
  members: MemberOption[];
  assignments: AssignmentsState;
  onChanged: (revision: number) => void;
  waiverPath?: string;
}) {
  const [memberUserId, setMemberUserId] = state('');
  const [type, setType] = state<ResponsibilityType>('assigned_reviewer');
  const [waivers, setWaivers] = state('');
  const [preview, setPreview] = state<{ key: string; conflicts: Conflict[] } | null>(null);
  const [pending, setPending] = state(false);
  const [actionError, setActionError] = state<Error | null>(null);
  const [notice, setNotice] = state<string | null>(null);
  const [revokeReason, setRevokeReason] = state('');

  const selectionKey = () => `${memberUserId()}|${type()}`;
  const previewed = () => preview()?.key === selectionKey();
  const waiverPath =
    waiverPathOverride ?? organizationPath(`/programs/${programId}/boundaries/${scope.recordId}/exceptions`);

  async function run(action: () => Promise<string | null>) {
    setActionError(null);
    setNotice(null);
    setPending(true);
    try {
      setNotice(await action());
    } catch (failure) {
      setActionError(failure instanceof Error ? failure : new Error('The request failed.'));
    } finally {
      setPending(false);
    }
  }

  function checkConflicts() {
    void run(async () => {
      const result = await previewConflicts(scope, memberUserId(), type(), new Date().toISOString());
      setPreview({ key: selectionKey(), conflicts: result.conflicts });
      return result.conflicts.length === 0 ? 'No separation-of-duties conflicts for this assignment.' : null;
    });
  }

  function assign(event: Event) {
    event.preventDefault();
    if (!previewed()) {
      checkConflicts();
      return;
    }
    void run(async () => {
      await assignResponsibility(scope, memberUserId(), type(), new Date().toISOString(), waiverIdsFrom(waivers()));
      setPreview(null);
      setWaivers('');
      const next = (assignments.value?.revision ?? 0) + 1;
      onChanged(next);
      return 'Responsibility assigned.';
    });
  }

  function revoke(assignmentId: string) {
    void run(async () => {
      await revokeResponsibility(scope, assignmentId, revokeReason() || `Revoked from the ${label(scope.recordType)} page.`);
      onChanged((assignments.value?.revision ?? 0) + 1);
      return 'Responsibility revoked.';
    });
  }

  const waiverText = waivers();
  const error = actionError();
  const conflicts = previewed() ? (preview()?.conflicts ?? []) : [];

  return (
    <Card>
      <CardHeader>
        <CardTitle>Responsibilities on draft revision {scope.revision}</CardTitle>
        <CardDescription>
          Assign reviewers and approvers for this exact revision. Conflicts are checked before anything is saved.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <Stack gap="sm">
          {assignments.pending && !assignments.value ? (
            <Spinner label="Loading responsibilities" />
          ) : assignments.error ? (
            <Stack gap="sm">
              <p role="alert">{assignments.error.message}</p>
              <Button variant="secondary" onPress={() => assignments.refresh()}>
                Try again
              </Button>
            </Stack>
          ) : (assignments.value?.assignments ?? []).length === 0 ? (
            <p>No responsibilities assigned on this revision.</p>
          ) : (
            <ul className="plain-list boundary-assignments">
              {(assignments.value?.assignments ?? []).map((assignment) => (
                <li>
                  <strong>{label(assignment.type)}</strong> · member {shortId(assignment.memberId)} · assigned by{' '}
                  {assignment.assignedBy} on {new Date(assignment.assignedAt).toLocaleDateString()}
                  {assignment.waiverIds.length > 0 ? ` · under exception ${assignment.waiverIds.map(shortId).join(', ')}` : ''}
                  {assignment.revokedAt ? (
                    <span className="boundary-meta">
                      {' '}
                      · revoked by {assignment.revokedBy ?? 'a member'} on {new Date(assignment.revokedAt).toLocaleDateString()}
                      {assignment.revocationReason ? `: ${assignment.revocationReason}` : ''}
                    </span>
                  ) : (
                    <>
                      {' '}
                      <Button variant="secondary" disabled={pending()} onPress={() => revoke(assignment.assignmentId)}>
                        Revoke {label(assignment.type)} {shortId(assignment.memberId)}
                      </Button>
                    </>
                  )}
                </li>
              ))}
            </ul>
          )}
          <label className="registration-field">
            <span>Reason for revocations (optional)</span>
            <input type="text" value={revokeReason()} onInput={(event: Event) => setRevokeReason((event.target as HTMLInputElement).value)} />
          </label>
          <form className="boundary-assign" onSubmit={(event: Event) => assign(event)}>
            <Stack gap="sm">
              <label className="registration-field">
                <span>Member</span>
                <select
                  value={memberUserId()}
                  onChange={(event: Event) => setMemberUserId((event.target as HTMLSelectElement).value)}
                  required
                >
                  <option value="" selected={memberUserId() === ''}>
                    Choose a member
                  </option>
                  {members.map((member) => (
                    <option value={member.userId} selected={member.userId === memberUserId()}>
                      {member.label}
                    </option>
                  ))}
                </select>
              </label>
              <label className="registration-field">
                <span>Responsibility</span>
                <select
                  value={type()}
                  onChange={(event: Event) => setType((event.target as HTMLSelectElement).value as ResponsibilityType)}
                >
                  {responsibilityTypes.map((value) => (
                    <option value={value} selected={value === type()}>
                      {label(value)}
                    </option>
                  ))}
                </select>
              </label>
              {conflicts.length > 0 ? (
                <div className="boundary-conflicts" role="alert">
                  <strong>Separation-of-duties conflict</strong>
                  <ul>
                    {conflicts.map((conflict) => (
                      <li>{conflictSentence(conflict)}</li>
                    ))}
                  </ul>
                  <a href={waiverPath}>Request an exception</a>
                </div>
              ) : null}
              {conflicts.length > 0 ? (
                <label className="registration-field">
                  <span>Approved exception IDs (optional, comma separated)</span>
                  <input type="text" value={waiverText} onInput={(event: Event) => setWaivers((event.target as HTMLInputElement).value)} />
                </label>
              ) : null}
              {error ? <p role="alert">{describeBoundaryFailure(error, 'these responsibilities')}</p> : null}
              {notice() ? <p role="status">{notice()}</p> : null}
              <Button variant="primary" type="submit" disabled={pending() || memberUserId() === ''}>
                {previewed() ? 'Assign responsibility' : 'Check for conflicts'}
              </Button>
            </Stack>
          </form>
        </Stack>
      </CardContent>
    </Card>
  );
}

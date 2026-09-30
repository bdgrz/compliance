import type { TeamSummary } from '../teams/teams.js';
import { identityKinds, latestReviewDate, type Person, type ServiceIdentityTerms } from './workforce.js';
import { inputValue } from './workforce-shared.js';

export const emptyIdentity: ServiceIdentityTerms = {
  displayName: '',
  identityKind: 'service',
  purpose: '',
  environment: null,
  ownerKind: 'person',
  ownerId: '',
  reviewBy: '',
};

// The editable terms of a non-human identity: exactly one accountable owner (a person or a team)
// and an ownership review date no more than one year out.
export function IdentityFields({
  terms,
  people,
  teams,
  onChange,
}: {
  terms: ServiceIdentityTerms;
  people: Person[];
  teams: TeamSummary[];
  onChange: (next: ServiceIdentityTerms) => void;
}) {
  const owners =
    terms.ownerKind === 'team'
      ? teams.map((team) => ({ id: team.teamId, name: team.name }))
      : people.map((person) => ({ id: person.personId, name: person.displayName }));
  return (
    <>
      <label className="registration-field">
        <span>Name</span>
        <input
          type="text"
          value={terms.displayName}
          onInput={(event: Event) => onChange({ ...terms, displayName: inputValue(event) })}
          required
        />
      </label>
      <label className="registration-field">
        <span>Identity kind</span>
        <select
          value={terms.identityKind}
          onChange={(event: Event) => onChange({ ...terms, identityKind: inputValue(event) })}
          required
        >
          {identityKinds.map((option) => (
            <option value={option.value}>{option.label}</option>
          ))}
        </select>
      </label>
      <label className="registration-field">
        <span>Approved purpose</span>
        <textarea
          value={terms.purpose}
          onInput={(event: Event) => onChange({ ...terms, purpose: inputValue(event) })}
          required
        />
      </label>
      <label className="registration-field">
        <span>Environment (optional)</span>
        <input
          type="text"
          value={terms.environment ?? ''}
          onInput={(event: Event) => onChange({ ...terms, environment: inputValue(event) || null })}
        />
      </label>
      <label className="registration-field">
        <span>Owner type</span>
        <select
          value={terms.ownerKind}
          onChange={(event: Event) => onChange({ ...terms, ownerKind: inputValue(event), ownerId: '' })}
          required
        >
          <option value="person">Person on the roster</option>
          <option value="team">Team</option>
        </select>
      </label>
      <label className="registration-field">
        <span>Accountable owner</span>
        <select
          value={terms.ownerId}
          onChange={(event: Event) => onChange({ ...terms, ownerId: inputValue(event) })}
          required
        >
          <option value="">{terms.ownerKind === 'team' ? 'Choose a team' : 'Choose a person'}</option>
          {owners.map((owner) => (
            <option value={owner.id}>{owner.name}</option>
          ))}
        </select>
      </label>
      <label className="registration-field">
        <span>Review ownership by (within one year)</span>
        <input
          type="date"
          value={terms.reviewBy}
          onInput={(event: Event) => onChange({ ...terms, reviewBy: inputValue(event) })}
          required
        />
      </label>
    </>
  );
}

// Client-side guard matching the server rule; the server stays authoritative.
export function reviewDateProblem(reviewBy: string, today = new Date()): string | null {
  if (!reviewBy) return 'Choose an ownership review date.';
  if (reviewBy <= today.toISOString().slice(0, 10)) return 'The review date must be in the future.';
  if (reviewBy > latestReviewDate(today)) return 'The review date must be no more than one year out.';
  return null;
}

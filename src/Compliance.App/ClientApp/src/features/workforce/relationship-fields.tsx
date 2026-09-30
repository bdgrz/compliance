import { lifecycleStatuses, workerTypes, type Person, type WorkRelationshipTerms } from './workforce.js';
import { inputValue } from './workforce-shared.js';

export const emptyTerms: WorkRelationshipTerms = {
  workerType: 'employee',
  lifecycleStatus: 'active',
  startDate: '',
  endDate: null,
  department: null,
  managerPersonId: null,
  sponsorPersonId: null,
};

// The editable terms of a work relationship. Recording a leaver is choosing "Ended" with an end date.
export function RelationshipFields({
  terms,
  people,
  onChange,
  showManager,
}: {
  terms: WorkRelationshipTerms;
  people: Person[];
  onChange: (next: WorkRelationshipTerms) => void;
  showManager: boolean;
}) {
  const ended = terms.lifecycleStatus === 'ended';
  const external = terms.workerType === 'external_collaborator';
  return (
    <>
      <label className="registration-field">
        <span>Worker type</span>
        <select
          value={terms.workerType}
          onChange={(event: Event) => onChange({ ...terms, workerType: inputValue(event) })}
          required
        >
          {workerTypes.map((option) => (
            <option value={option.value}>{option.label}</option>
          ))}
        </select>
      </label>
      <label className="registration-field">
        <span>Lifecycle status</span>
        <select
          value={terms.lifecycleStatus}
          onChange={(event: Event) => onChange({ ...terms, lifecycleStatus: inputValue(event) })}
          required
        >
          {lifecycleStatuses.map((option) => (
            <option value={option.value}>{option.label}</option>
          ))}
        </select>
      </label>
      <label className="registration-field">
        <span>Start date</span>
        <input
          type="date"
          value={terms.startDate}
          onInput={(event: Event) => onChange({ ...terms, startDate: inputValue(event) })}
          required
        />
      </label>
      <label className="registration-field">
        <span>{ended ? 'End date (required for a leaver)' : 'End date'}</span>
        <input
          type="date"
          value={terms.endDate ?? ''}
          onInput={(event: Event) => onChange({ ...terms, endDate: inputValue(event) || null })}
          required={ended}
        />
      </label>
      <label className="registration-field">
        <span>Department</span>
        <input
          type="text"
          value={terms.department ?? ''}
          onInput={(event: Event) => onChange({ ...terms, department: inputValue(event) || null })}
        />
      </label>
      {showManager ? (
        <label className="registration-field">
          <span>Manager (restricted)</span>
          <select
            value={terms.managerPersonId ?? ''}
            onChange={(event: Event) => onChange({ ...terms, managerPersonId: inputValue(event) || null })}
          >
            <option value="">No manager</option>
            {people.map((person) => (
              <option value={person.personId}>{person.displayName}</option>
            ))}
          </select>
        </label>
      ) : null}
      <label className="registration-field">
        <span>{external ? 'Internal sponsor (required for external collaborators)' : 'Sponsor'}</span>
        <select
          value={terms.sponsorPersonId ?? ''}
          onChange={(event: Event) => onChange({ ...terms, sponsorPersonId: inputValue(event) || null })}
          required={external}
        >
          <option value="">No sponsor</option>
          {people.map((person) => (
            <option value={person.personId}>{person.displayName}</option>
          ))}
        </select>
      </label>
    </>
  );
}

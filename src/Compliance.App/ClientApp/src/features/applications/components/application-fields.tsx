import { resource } from '@askrjs/askr/resources';

import { listPeople, type Person } from '../../workforce/workforce.js';
import type { ApplicationContent } from '../applications.js';

export const emptyContent: ApplicationContent = {
  name: '',
  purpose: '',
  ownerReference: null,
  classification: null,
  systemOwnerPersonId: null,
  accessOwnerPersonId: null,
};

function blankToNull(value: string | null): string | null {
  return value !== null && value.trim() !== '' ? value.trim() : null;
}

export function cleanContent(content: ApplicationContent): ApplicationContent {
  return {
    name: content.name.trim(),
    purpose: content.purpose.trim(),
    ownerReference: blankToNull(content.ownerReference),
    classification: blankToNull(content.classification),
    systemOwnerPersonId: blankToNull(content.systemOwnerPersonId),
    accessOwnerPersonId: blankToNull(content.accessOwnerPersonId),
  };
}

type TextKey = 'name' | 'purpose' | 'ownerReference' | 'classification';
type PersonKey = 'systemOwnerPersonId' | 'accessOwnerPersonId';

const textFields: { key: TextKey; label: string; required: boolean; hint?: string }[] = [
  { key: 'name', label: 'Application name', required: true },
  { key: 'purpose', label: 'Purpose', required: true },
  { key: 'ownerReference', label: 'Owner', required: false, hint: 'A person or team; leave blank if unknown.' },
  { key: 'classification', label: 'Data classification', required: false, hint: 'For example confidential or internal.' },
];

const personFields: { key: PersonKey; label: string; hint: string }[] = [
  { key: 'systemOwnerPersonId', label: 'System owner', hint: 'Accountable for the application on the workforce roster.' },
  { key: 'accessOwnerPersonId', label: 'Access owner', hint: 'Approves who may access the application.' },
];

// Chooses an owner from the workforce roster. A saved owner who is no longer listed stays selectable
// so an unrelated edit never clears it silently.
function PersonPicker({
  label,
  hint,
  value,
  people,
  unavailable,
  onChange,
}: {
  label: string;
  hint: string;
  value: string | null;
  people: Person[];
  unavailable: boolean;
  onChange: (next: string | null) => void;
}) {
  const known = value === null || people.some((person) => person.personId === value);
  return (
    <label className="registration-field">
      <span>{label}</span>
      <select value={value ?? ''} onChange={(event: Event) => onChange((event.target as HTMLSelectElement).value || null)}>
        <option value="">Not assigned</option>
        {known ? null : <option value={value!}>Person not on the roster</option>}
        {people.map((person) => (
          <option value={person.personId}>
            {person.displayName}
            {person.workEmail ? ` (${person.workEmail})` : ''}
          </option>
        ))}
      </select>
      <small>{unavailable ? 'The workforce roster could not be loaded; owners cannot be changed right now.' : hint}</small>
    </label>
  );
}

export function ApplicationFields({
  content,
  onChange,
}: {
  content: ApplicationContent;
  onChange: (next: ApplicationContent) => void;
}) {
  const people = resource(() => listPeople(), []);
  const roster = [...(people.value ?? [])].sort((a, b) => a.displayName.localeCompare(b.displayName));
  return (
    <>
      {textFields.map((field) => (
        <label className="registration-field">
          <span>{field.label}</span>
          <input
            type="text"
            value={content[field.key] ?? ''}
            onInput={(event: Event) => onChange({ ...content, [field.key]: (event.target as HTMLInputElement).value })}
            required={field.required}
          />
          {field.hint ? <small>{field.hint}</small> : null}
        </label>
      ))}
      {personFields.map((field) => (
        <PersonPicker
          label={field.label}
          hint={field.hint}
          value={content[field.key]}
          people={roster}
          unavailable={people.error !== null && people.error !== undefined}
          onChange={(next) => onChange({ ...content, [field.key]: next })}
        />
      ))}
    </>
  );
}

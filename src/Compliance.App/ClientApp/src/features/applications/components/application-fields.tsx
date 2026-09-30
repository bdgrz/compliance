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

const fields: { key: keyof ApplicationContent; label: string; required: boolean; hint?: string }[] = [
  { key: 'name', label: 'Application name', required: true },
  { key: 'purpose', label: 'Purpose', required: true },
  { key: 'ownerReference', label: 'Owner', required: false, hint: 'A person or team; leave blank if unknown.' },
  { key: 'classification', label: 'Data classification', required: false, hint: 'For example confidential or internal.' },
  { key: 'systemOwnerPersonId', label: 'System owner person ID', required: false },
  { key: 'accessOwnerPersonId', label: 'Access owner person ID', required: false },
];

export function ApplicationFields({
  content,
  onChange,
}: {
  content: ApplicationContent;
  onChange: (next: ApplicationContent) => void;
}) {
  return (
    <>
      {fields.map((field) => (
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
    </>
  );
}

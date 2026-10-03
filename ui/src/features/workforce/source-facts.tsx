import {
  type SourceFacts,
  type SourceTargetKind,
} from './source-observations.js';
import { inputValue } from './workforce-shared.js';

const labels: Record<string, string> = {
  display_name: 'display name',
  work_email: 'work email',
  worker_type: 'worker type',
  lifecycle_status: 'lifecycle status',
  start_date: 'start date',
  end_date: 'end date',
  department: 'department',
  manager_person_id: 'manager person ID (restricted)',
  sponsor_person_id: 'sponsor person ID',
  employment_status_reason: 'employment status reason (restricted)',
  identity_kind: 'identity kind',
  environment: 'environment',
  expires_on: 'credential expiry',
};

export function SourceFactsSummary({
  kind,
  facts,
  restrictedFieldsRedacted,
}: {
  kind: SourceTargetKind;
  facts: SourceFacts;
  restrictedFieldsRedacted?: boolean;
}) {
  const values = facts[kind];
  if (!values) return null;
  return (
    <dl>
      {Object.entries(values)
        .filter(([key]) => Object.hasOwn(labels, key))
        .map(([key, value]) => (
          <div key={key}>
            <dt>Observed {labels[key]}</dt>
            <dd>
              {value ??
                (restrictedFieldsRedacted &&
                (key === 'manager_person_id' ||
                  key === 'employment_status_reason')
                  ? 'No value disclosed'
                  : 'Not set')}
            </dd>
          </div>
        ))}
    </dl>
  );
}

export function SourceFactsFields({
  kind,
  facts,
  onChange,
}: {
  kind: SourceTargetKind;
  facts: SourceFacts;
  onChange: (facts: SourceFacts) => void;
}) {
  const values = facts[kind];
  if (!values) return null;
  return (
    <>
      {Object.entries(values).map(([key, value]) => (
        <label className="registration-field" key={key}>
          <span>Observed {labels[key] ?? key}</span>
          <input
            type={
              key.endsWith('_date') || key === 'expires_on' ? 'date' : 'text'
            }
            value={value ?? ''}
            maxLength={
              key === 'employment_status_reason'
                ? 1000
                : key === 'work_email'
                  ? 320
                  : key === 'environment'
                    ? 100
                    : key.endsWith('_kind') ||
                        key === 'worker_type' ||
                        key === 'lifecycle_status'
                      ? 50
                      : 200
            }
            onInput={(event: Event) =>
              onChange({
                ...facts,
                [kind]: {
                  ...values,
                  [key]:
                    inputValue(event) ||
                    (key === 'display_name' ||
                    key === 'start_date' ||
                    key === 'worker_type' ||
                    key === 'lifecycle_status' ||
                    key === 'identity_kind'
                      ? ''
                      : null),
                },
              })
            }
          />
        </label>
      ))}
    </>
  );
}

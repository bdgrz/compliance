import { state } from '@askrjs/askr';
import { Button, Stack } from '@askrjs/themes/components';

import {
  ProgramRequestError,
  type ApplicabilityReference,
  type ControlContent,
} from '../controls.js';

// Applications and system instances must cite their governed inventory record; risks and
// processes are recorded as named subjects until their registers are linked.
const subjectTypes: { code: string; label: string; governed: boolean }[] = [
  { code: 'application', label: 'Application', governed: true },
  { code: 'system_instance', label: 'System instance', governed: true },
  { code: 'risk', label: 'Risk', governed: false },
  { code: 'process', label: 'Process', governed: false },
];

function isGoverned(subjectType: string) {
  return (
    subjectTypes.find((type) => type.code === subjectType)?.governed ?? false
  );
}

function newReference(): ApplicabilityReference {
  return {
    entry_id: crypto.randomUUID(),
    subject_type: 'process',
    subject: '',
    governed_record_id: null,
    rationale: '',
    unresolved: true,
  };
}

function withSubjectType(
  reference: ApplicabilityReference,
  subjectType: string
): ApplicabilityReference {
  const governed = isGoverned(subjectType);
  return {
    ...reference,
    subject_type: subjectType,
    unresolved: !governed,
    governed_record_id: governed ? (reference.governed_record_id ?? '') : null,
  };
}

const textFields: {
  key: 'title' | 'objective' | 'description' | 'implementationNarrative';
  label: string;
  multiline: boolean;
}[] = [
  { key: 'title', label: 'Title', multiline: false },
  { key: 'objective', label: 'Objective', multiline: true },
  { key: 'description', label: 'Description', multiline: true },
  {
    key: 'implementationNarrative',
    label: 'Implementation narrative',
    multiline: true,
  },
];

export function describeControlFailure(error: Error): string {
  if (error instanceof ProgramRequestError && error.status === 403) {
    return 'You do not have permission to change controls in this program.';
  }
  if (
    error instanceof ProgramRequestError &&
    error.status === 409 &&
    !error.transient
  ) {
    return 'Someone else changed this control since you opened it. Reload to see their changes, then edit again.';
  }
  return error.message;
}

// Shared by create and edit. Expected evidence is one description per line; applicability
// references are edited inline; each keeps its stable entry ID across revisions.
export function ControlForm({
  initial,
  withIdentifier,
  submitLabel,
  onSubmit,
}: {
  initial: ControlContent;
  withIdentifier: boolean;
  submitLabel: string;
  onSubmit: (
    identifier: string,
    content: ControlContent
  ) => Promise<string | null>;
}) {
  const [identifier, setIdentifier] = state('');
  const [content, setContent] = state<ControlContent>({ ...initial });
  const [evidence, setEvidence] = state(initial.expectedEvidence.join('\n'));
  const [pending, setPending] = state(false);
  const [actionError, setActionError] = state<Error | null>(null);
  const [notice, setNotice] = state<string | null>(null);

  async function submit(event: Event) {
    event.preventDefault();
    setActionError(null);
    setNotice(null);
    setPending(true);
    try {
      const expectedEvidence = evidence()
        .split('\n')
        .map((line) => line.trim())
        .filter((line) => line !== '');
      setNotice(
        await onSubmit(identifier(), { ...content(), expectedEvidence })
      );
    } catch (failure) {
      setActionError(
        failure instanceof Error
          ? failure
          : new Error('Unable to save the control.')
      );
    } finally {
      setPending(false);
    }
  }

  const error = actionError();

  return (
    <form onSubmit={(event: Event) => void submit(event)}>
      <Stack gap="sm">
        {withIdentifier ? (
          <label className="registration-field">
            <span>Identifier</span>
            <input
              type="text"
              value={identifier()}
              maxLength={80}
              aria-describedby="control-identifier-hint"
              onInput={(event: Event) =>
                setIdentifier((event.target as HTMLInputElement).value)
              }
              required
            />
            <small id="control-identifier-hint">
              Letters, digits, hyphens, underscores, or periods, for example
              AC-01.
            </small>
          </label>
        ) : null}
        {textFields.map((field) => (
          <label className="registration-field">
            <span>{field.label}</span>
            {field.multiline ? (
              <textarea
                rows={field.key === 'implementationNarrative' ? 6 : 3}
                value={content()[field.key]}
                onInput={(event: Event) =>
                  setContent({
                    ...content(),
                    [field.key]: (event.target as HTMLTextAreaElement).value,
                  })
                }
                required
              />
            ) : (
              <input
                type="text"
                value={content()[field.key]}
                onInput={(event: Event) =>
                  setContent({
                    ...content(),
                    [field.key]: (event.target as HTMLInputElement).value,
                  })
                }
                required
              />
            )}
          </label>
        ))}
        <label className="registration-field">
          <span>Expected evidence (one description per line)</span>
          <textarea
            rows={3}
            value={evidence()}
            onInput={(event: Event) =>
              setEvidence((event.target as HTMLTextAreaElement).value)
            }
            required
          />
        </label>
        <label className="registration-field">
          <span>Owner (optional)</span>
          <input
            type="text"
            value={content().ownerReference ?? ''}
            onInput={(event: Event) =>
              setContent({
                ...content(),
                ownerReference: (event.target as HTMLInputElement).value,
              })
            }
          />
        </label>
        <fieldset className="control-applicability-editor">
          <legend>Applicability</legend>
          <p className="control-meta">
            Name what this control applies to and why. Applications and system
            instances need their inventory record ID; risks and processes stay
            unlinked until their registers are connected.
          </p>
          {content().applicability.map((reference, index) => {
            const update = (next: ApplicabilityReference) =>
              setContent({
                ...content(),
                applicability: content().applicability.map((item, position) =>
                  position === index ? next : item
                ),
              });
            return (
              <div
                className="control-applicability-entry"
                role="group"
                aria-label={`Applicability ${index + 1}`}
              >
                <label className="registration-field">
                  <span>Subject type</span>
                  <select
                    value={reference.subject_type}
                    onChange={(event: Event) =>
                      update(
                        withSubjectType(
                          reference,
                          (event.target as HTMLSelectElement).value
                        )
                      )
                    }
                  >
                    {subjectTypes.map((type) => (
                      <option value={type.code}>{type.label}</option>
                    ))}
                  </select>
                </label>
                <label className="registration-field">
                  <span>Subject</span>
                  <input
                    type="text"
                    maxLength={500}
                    value={reference.subject}
                    onInput={(event: Event) =>
                      update({
                        ...reference,
                        subject: (event.target as HTMLInputElement).value,
                      })
                    }
                    required
                  />
                </label>
                {isGoverned(reference.subject_type) ? (
                  <label className="registration-field">
                    <span>Inventory record ID</span>
                    <input
                      type="text"
                      value={reference.governed_record_id ?? ''}
                      onInput={(event: Event) =>
                        update({
                          ...reference,
                          governed_record_id: (
                            event.target as HTMLInputElement
                          ).value.trim(),
                        })
                      }
                      required
                    />
                  </label>
                ) : null}
                <label className="registration-field">
                  <span>Why it applies</span>
                  <textarea
                    rows={2}
                    maxLength={2000}
                    value={reference.rationale}
                    onInput={(event: Event) =>
                      update({
                        ...reference,
                        rationale: (event.target as HTMLTextAreaElement).value,
                      })
                    }
                    required
                  />
                </label>
                <Button
                  variant="secondary"
                  onPress={() =>
                    setContent({
                      ...content(),
                      applicability: content().applicability.filter(
                        (_item, position) => position !== index
                      ),
                    })
                  }
                >
                  Remove applicability {index + 1}
                </Button>
              </div>
            );
          })}
          <Button
            variant="secondary"
            onPress={() =>
              setContent({
                ...content(),
                applicability: [...content().applicability, newReference()],
              })
            }
          >
            Add applicability
          </Button>
        </fieldset>
        {error ? <p role="alert">{describeControlFailure(error)}</p> : null}
        {notice() ? <p role="status">{notice()}</p> : null}
        <Button variant="primary" type="submit" disabled={pending()}>
          {pending() ? 'Saving…' : submitLabel}
        </Button>
      </Stack>
    </form>
  );
}

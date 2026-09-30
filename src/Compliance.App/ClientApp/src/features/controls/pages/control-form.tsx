import { state } from '@askrjs/askr';
import { Button, Stack } from '@askrjs/themes/components';

import { ProgramRequestError, type ControlContent } from '../controls.js';

const textFields: { key: 'title' | 'objective' | 'description' | 'implementationNarrative'; label: string; multiline: boolean }[] = [
  { key: 'title', label: 'Title', multiline: false },
  { key: 'objective', label: 'Objective', multiline: true },
  { key: 'description', label: 'Description', multiline: true },
  { key: 'implementationNarrative', label: 'Implementation narrative', multiline: true },
];

export function describeControlFailure(error: Error): string {
  if (error instanceof ProgramRequestError && error.status === 403) {
    return 'You do not have permission to change controls in this program.';
  }
  if (error instanceof ProgramRequestError && error.status === 409 && !error.transient) {
    return 'Someone else changed this control since you opened it. Reload to see their changes, then edit again.';
  }
  return error.message;
}

// Shared by create and edit. Expected evidence is one description per line; applicability
// references are preserved as-is (they are edited through their governed records).
export function ControlForm({
  initial,
  withIdentifier,
  submitLabel,
  onSubmit,
}: {
  initial: ControlContent;
  withIdentifier: boolean;
  submitLabel: string;
  onSubmit: (identifier: string, content: ControlContent) => Promise<string | null>;
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
      setNotice(await onSubmit(identifier(), { ...content(), expectedEvidence }));
    } catch (failure) {
      setActionError(failure instanceof Error ? failure : new Error('Unable to save the control.'));
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
              onInput={(event: Event) => setIdentifier((event.target as HTMLInputElement).value)}
              required
            />
            <small id="control-identifier-hint">Letters, digits, hyphens, underscores, or periods, for example AC-01.</small>
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
                  setContent({ ...content(), [field.key]: (event.target as HTMLTextAreaElement).value })
                }
                required
              />
            ) : (
              <input
                type="text"
                value={content()[field.key]}
                onInput={(event: Event) =>
                  setContent({ ...content(), [field.key]: (event.target as HTMLInputElement).value })
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
            onInput={(event: Event) => setEvidence((event.target as HTMLTextAreaElement).value)}
            required
          />
        </label>
        <label className="registration-field">
          <span>Owner (optional)</span>
          <input
            type="text"
            value={content().ownerReference ?? ''}
            onInput={(event: Event) => setContent({ ...content(), ownerReference: (event.target as HTMLInputElement).value })}
          />
        </label>
        {error ? <p role="alert">{describeControlFailure(error)}</p> : null}
        {notice() ? <p role="status">{notice()}</p> : null}
        <Button variant="primary" type="submit" disabled={pending()}>
          {pending() ? 'Saving…' : submitLabel}
        </Button>
      </Stack>
    </form>
  );
}

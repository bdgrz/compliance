import { Button, EmptyState, Page, Stack } from '@askrjs/themes/components';

import { organizationPath } from '../tenants/tenants.js';
import { isStaleConflict, WorkforceRequestError } from './workforce.js';

const sections = [
  { path: '/workforce', label: 'Roster' },
  { path: '/workforce/observations', label: 'Joiners, movers, and leavers' },
  { path: '/workforce/service-identities', label: 'Service identities' },
];

export function WorkforceSections({ current }: { current: string }) {
  return (
    <nav aria-label="Workforce sections">
      <ul className="workforce-sections">
        {sections.map((section) => (
          <li>
            <a href={organizationPath(section.path)} aria-current={section.path === current ? 'page' : undefined}>
              {section.label}
            </a>
          </li>
        ))}
      </ul>
    </nav>
  );
}

export function WorkforceForbidden({ title }: { title: string }) {
  return (
    <Page>
      <EmptyState
        title={title}
        titleAs="h1"
        description="Workforce records need the workforce management permission. Ask an Org Admin or Compliance Lead for access."
      />
    </Page>
  );
}

// A list section's failure: projection lag and ordinary failures both offer a retry.
export function LoadFailure({ error, onRetry }: { error: Error; onRetry: () => void }) {
  return (
    <Stack gap="sm">
      <p role="alert">{error.message}</p>
      <Button variant="secondary" onPress={onRetry}>
        Try again
      </Button>
    </Stack>
  );
}

// A single record's failure: forbidden and not found link back, everything else retries.
export function RecordFailure({
  error,
  noun,
  backPath,
  backLabel,
  onRetry,
}: {
  error: Error;
  noun: string;
  backPath: string;
  backLabel: string;
  onRetry: () => void;
}) {
  const status = error instanceof WorkforceRequestError ? error.status : null;
  const back = <a href={organizationPath(backPath)}>{backLabel}</a>;
  return (
    <Page>
      <EmptyState
        title={
          status === 403
            ? `This ${noun} is not available to you`
            : status === 404
              ? `${noun[0]!.toUpperCase()}${noun.slice(1)} not found`
              : `${noun[0]!.toUpperCase()}${noun.slice(1)} could not be loaded`
        }
        titleAs="h1"
        description={error.message}
        action={
          status === 403 || status === 404 ? (
            back
          ) : (
            <Button variant="primary" onPress={onRetry}>
              Try again
            </Button>
          )
        }
      />
    </Page>
  );
}

export function ActionError({ error, noun }: { error: Error | null; noun: string }) {
  if (!error) return null;
  return (
    <p role="alert">
      {isStaleConflict(error)
        ? `Someone else changed this ${noun} since you opened it. Reload to see their changes, then edit again.`
        : error.message}
    </p>
  );
}

export function inputValue(event: Event): string {
  return (event.target as HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement).value;
}

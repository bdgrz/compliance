import { state } from '@askrjs/askr';
import { resource } from '@askrjs/askr/resources';
import {
  Block,
  Button,
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
  Spinner,
  Stack,
} from '@askrjs/themes/components';

import {
  grantPlatformOperator,
  listPlatformOperators,
  revokePlatformOperator,
  userIdPattern,
} from '../operator.js';

// Grants and revokes platform operator status. Every change carries a recorded reason, and the
// server refuses to remove the last operator.
export function PlatformOperatorsCard() {
  const [version, setVersion] = state(0);
  const [userId, setUserId] = state('');
  const [reason, setReason] = state('');
  const [pending, setPending] = state(false);
  const [actionError, setActionError] = state<string | null>(null);
  const [notice, setNotice] = state<string | null>(null);
  const operators = resource(() => listPlatformOperators(), [version()]);

  async function change(action: () => Promise<void>, done: string) {
    setActionError(null);
    setNotice(null);
    if (reason().trim().length === 0) {
      setActionError('Give a reason; operator changes are audited.');
      return;
    }
    setPending(true);
    try {
      await action();
      setNotice(done);
      setUserId('');
      setReason('');
      setVersion(version() + 1);
    } catch (failure) {
      setActionError(failure instanceof Error ? failure.message : 'The change could not be saved.');
    } finally {
      setPending(false);
    }
  }

  function grant(event: Event) {
    event.preventDefault();
    if (!userIdPattern.test(userId().trim())) {
      setActionError('Enter the user ID of the person to make an operator.');
      return;
    }
    const target = userId().trim();
    void change(() => grantPlatformOperator(target, reason()), `Operator status granted to ${target}.`);
  }

  const ids = operators.value ?? [];
  return (
    <Card>
      <CardHeader>
        <CardTitle>Platform operators</CardTitle>
        <CardDescription>
          Operators provision, suspend, and reactivate organizations. Operator status never opens an organization's
          business records.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <Stack gap="md">
          {actionError() ? <p role="alert">{actionError()}</p> : null}
          {notice() ? <p role="status">{notice()}</p> : null}
          {operators.pending && !operators.value ? (
            <Spinner label="Loading platform operators" />
          ) : operators.error ? (
            <Stack gap="sm">
              <p role="alert">{operators.error.message}</p>
              <Button variant="secondary" onPress={() => operators.refresh()}>
                Try again
              </Button>
            </Stack>
          ) : ids.length === 0 ? (
            <p>No platform operators are recorded.</p>
          ) : (
            <ul className="plain-list">
              {ids.map((id) => (
                <li>
                  <Block direction="row" gap="sm" align="center" wrap>
                    <code>{id}</code>
                    <Button
                      variant="destructive"
                      size="sm"
                      disabled={pending()}
                      aria-label={`Revoke operator status from ${id}`}
                      onPress={() =>
                        void change(() => revokePlatformOperator(id, reason()), `Operator status revoked from ${id}.`)
                      }
                    >
                      Revoke
                    </Button>
                  </Block>
                </li>
              ))}
            </ul>
          )}
          <form onSubmit={grant} aria-label="Grant operator status">
            <Stack gap="sm">
              <label className="registration-field">
                <span>Reason (required to grant or revoke)</span>
                <input
                  type="text"
                  value={reason()}
                  maxLength={500}
                  onInput={(event: Event) => setReason((event.target as HTMLInputElement).value)}
                />
              </label>
              <label className="registration-field">
                <span>User ID to grant</span>
                <input
                  type="text"
                  value={userId()}
                  onInput={(event: Event) => setUserId((event.target as HTMLInputElement).value)}
                />
              </label>
              <Button variant="primary" type="submit" disabled={pending()}>
                {pending() ? 'Saving…' : 'Grant operator status'}
              </Button>
            </Stack>
          </form>
        </Stack>
      </CardContent>
    </Card>
  );
}

import { state } from '@askrjs/askr';
import { Button, Stack } from '@askrjs/themes/components';

import { createApiClient } from '../../api-client/index.js';

const client = createApiClient();

export function EmailVerificationForm({
  userId,
  onVerified,
}: {
  userId: string;
  onVerified: () => void;
}) {
  const [email, setEmail] = state('');
  const [challengedEmail, setChallengedEmail] = state<string | null>(null);
  const [token, setToken] = state('');
  const [busy, setBusy] = state(false);
  const [error, setError] = state<string | null>(null);
  const [delivery, setDelivery] = state('');
  const [verified, setVerified] = state(false);

  function requireSuccess(result: {
    ok: boolean;
    status: number;
    error?: unknown;
  }) {
    if (result.ok) return;
    const detail =
      result.error &&
      typeof result.error === 'object' &&
      'detail' in result.error
        ? String(result.error.detail)
        : 'Email verification is unavailable. Try again.';
    throw new Error(
      result.status === 403 ? 'You cannot verify this email address.' : detail
    );
  }

  async function refreshStatus(address: string) {
    const result = await client.getEmailChallengeStatus({
      params: { user_id: userId, email_address: address },
    });
    requireSuccess(result);
    const status = result.ok ? result.data?.delivery_status : undefined;
    if (status === 'verified') {
      setToken('');
      setVerified(true);
      onVerified();
    }
    setDelivery(
      status === 'delivered'
        ? 'Check your email for the verification code.'
        : status === 'failed'
          ? 'The email could not be delivered. Try sending a new code.'
          : status === 'expired'
            ? 'The code has expired. Send a new code.'
            : 'The verification email is waiting to be delivered. Check delivery status shortly.'
    );
  }

  async function run(action: () => Promise<void>) {
    setBusy(true);
    setError(null);
    try {
      await action();
    } catch (failure) {
      setError(
        failure instanceof Error
          ? failure.message
          : 'Unable to verify the email address.'
      );
    } finally {
      setBusy(false);
    }
  }

  async function sendCode() {
    const address = email().trim();
    const params = { user_id: userId, email_address: address };
    requireSuccess(await client.reserveEmail({ params }));
    requireSuccess(await client.issueEmailChallenge({ params }));
    setChallengedEmail(address);
    setToken('');
    await refreshStatus(address);
  }

  async function verifyCode() {
    requireSuccess(
      await client.completeEmailChallenge({
        params: { user_id: userId, email_address: challengedEmail()! },
        body: { token: token().trim() },
      })
    );
    setToken('');
    setVerified(true);
    onVerified();
  }

  return (
    <form
      onSubmit={(event: Event) => {
        event.preventDefault();
        if (!busy()) void run(challengedEmail() ? verifyCode : sendCode);
      }}
    >
      <Stack gap="md">
        {verified() ? (
          <>
            <p role="status">
              Your email address is verified. Organization access may take a
              moment to update.
            </p>
            <Button type="button" disabled={busy()} onPress={onVerified}>
              Refresh organization access
            </Button>
          </>
        ) : (
          <>
            <label className="registration-field">
              <span>Email address</span>
              <input
                type="email"
                required
                aria-describedby={
                  error() ? 'email-verification-error' : undefined
                }
                autoComplete="email"
                value={email()}
                disabled={busy() || challengedEmail() !== null}
                onInput={(event: Event) =>
                  setEmail((event.target as HTMLInputElement).value)
                }
              />
            </label>
            {challengedEmail() ? (
              <>
                <p role="status">{delivery()}</p>
                <label className="registration-field">
                  <span>Verification code</span>
                  <input
                    type="text"
                    required
                    aria-describedby={
                      error() ? 'email-verification-error' : undefined
                    }
                    autoComplete="one-time-code"
                    value={token()}
                    disabled={busy()}
                    onInput={(event: Event) =>
                      setToken((event.target as HTMLInputElement).value)
                    }
                  />
                </label>
              </>
            ) : null}
            {error() ? (
              <p id="email-verification-error" role="alert">
                {error()}
              </p>
            ) : null}
            <Button type="submit" disabled={busy()}>
              {busy()
                ? 'Verifying…'
                : challengedEmail()
                  ? 'Verify email address'
                  : 'Send verification code'}
            </Button>
            {challengedEmail() ? (
              <>
                <Button
                  type="button"
                  variant="secondary"
                  disabled={busy()}
                  onPress={() =>
                    void run(() => refreshStatus(challengedEmail()!))
                  }
                >
                  Check delivery status
                </Button>
                <Button
                  type="button"
                  variant="secondary"
                  disabled={busy()}
                  onPress={() => void run(sendCode)}
                >
                  Send a new code
                </Button>
                <Button
                  type="button"
                  variant="ghost"
                  disabled={busy()}
                  onPress={() => {
                    setChallengedEmail(null);
                    setToken('');
                    setError(null);
                  }}
                >
                  Use another email address
                </Button>
              </>
            ) : null}
          </>
        )}
      </Stack>
    </form>
  );
}

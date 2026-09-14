import { state } from '@askrjs/askr';
import {
  Block,
  Button,
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
  Page,
  Stack,
} from '@askrjs/themes/components';

import { ComplianceBrand } from '../components/compliance-brand.js';
import { normalizeReturnPath } from '../features/authentication/auth.js';

export function RegistrationPage() {
  const [emailAddress, setEmailAddress] = state('');
  const [error, setError] = state<string | null>(null);
  const [submitting, setSubmitting] = state(false);

  async function register(event?: { preventDefault?: () => void }) {
    event?.preventDefault?.();
    setError(null);
    setSubmitting(true);

    try {
      const returnUrl = normalizeReturnPath(
        new URLSearchParams(window.location.search).get('returnUrl'),
        window.location.origin
      );
      const response = await fetch('/api/v1/developer-user-identities', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
        body: JSON.stringify({ email_address: emailAddress() }),
      });
      if (!response.ok) {
        const problem = (await response.json()) as {
          errors?: Record<string, string[]>;
        };
        throw new Error(problem.errors?.email_address?.[0] ?? 'Registration failed.');
      }

      window.location.assign(returnUrl);
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : 'Registration failed.');
      setSubmitting(false);
    }
  }

  return (
    <Page background="muted" center>
      <Block as="section" align="center" justify="center" grow>
        <Block width="full" maxWidth="sm" direction="column" gap="lg">
          <Card variant="raised">
            <CardHeader>
              <ComplianceBrand />
              <CardTitle>Create your Badgers account</CardTitle>
              <CardDescription>
                Enter your email address to start your workspace.
              </CardDescription>
            </CardHeader>
            <CardContent>
              <form onSubmit={(event) => void register(event)}>
                <Stack gap="md">
                  <label className="registration-field">
                    <span>Email address</span>
                    <input
                      type="email"
                      name="email"
                      value={emailAddress()}
                      onInput={(event: Event) => setEmailAddress((event.target as HTMLInputElement).value)}
                      autocomplete="email"
                      required
                    />
                  </label>
                  {error() ? <p role="alert">{error()}</p> : null}
                  <Button variant="primary" width="full" type="submit" disabled={submitting()}>
                    {submitting() ? 'Creating account…' : 'Create account'}
                  </Button>
                </Stack>
              </form>
            </CardContent>
          </Card>
        </Block>
      </Block>
    </Page>
  );
}

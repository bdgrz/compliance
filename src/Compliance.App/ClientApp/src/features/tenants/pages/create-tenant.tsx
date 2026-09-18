import { state } from '@askrjs/askr';
import {
  Block,
  Button,
  Card,
  CardContent,
  CardHeader,
  CardTitle,
  Page,
  PageHeader,
  Stack,
} from '@askrjs/themes/components';

import { registerTenant, slugPattern, writeActiveTenant } from '../tenants.js';

export function CreateTenantPage() {
  const [name, setName] = state('');
  const [slug, setSlug] = state('');
  const [error, setError] = state<string | null>(null);
  const [submitting, setSubmitting] = state(false);

  async function submit(event?: { preventDefault?: () => void }) {
    event?.preventDefault?.();
    setError(null);

    if (!slugPattern.test(slug())) {
      setError(
        'The slug must be 4-63 characters, start with a letter, and contain only lowercase letters, digits, and single hyphens.'
      );
      return;
    }

    setSubmitting(true);
    try {
      const tenant = await registerTenant(name(), slug());
      if (tenant) {
        writeActiveTenant({ tenantId: tenant.tenant_id, slug: tenant.slug });
      }

      window.location.assign('/');
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : 'Unable to create the organization.');
      setSubmitting(false);
    }
  }

  return (
    <Page background="muted" center>
      <Block as="section" align="center" justify="center" grow>
        <Block width="full" maxWidth="sm" direction="column" gap="lg">
          <PageHeader
            title="Create your organization"
            description="Every SOC 2 program lives inside its own organization."
          />
          <Card variant="raised">
            <CardHeader>
              <CardTitle>Organization details</CardTitle>
            </CardHeader>
            <CardContent>
              <form onSubmit={(event) => void submit(event)}>
                <Stack gap="md">
                  <label className="registration-field">
                    <span>Name</span>
                    <input
                      type="text"
                      value={name()}
                      onInput={(event: Event) => setName((event.target as HTMLInputElement).value)}
                      required
                    />
                  </label>
                  <label className="registration-field">
                    <span>Slug</span>
                    <input
                      type="text"
                      value={slug()}
                      onInput={(event: Event) => setSlug((event.target as HTMLInputElement).value)}
                      required
                    />
                  </label>
                  {error() ? <p role="alert">{error()}</p> : null}
                  <Button variant="primary" width="full" type="submit" disabled={submitting()}>
                    {submitting() ? 'Creating…' : 'Create organization'}
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

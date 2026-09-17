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

import { listMyTenants, writeActiveTenantSlug, type TenantMembershipSummary } from '../tenants.js';

export function SelectTenantPage() {
  const [tenants, setTenants] = state<TenantMembershipSummary[] | null>(null);
  const [error, setError] = state<string | null>(null);

  void listMyTenants()
    .then(setTenants)
    .catch((failure: unknown) =>
      setError(failure instanceof Error ? failure.message : 'Unable to load your organizations.')
    );

  function select(slug: string) {
    writeActiveTenantSlug(slug);
    window.location.assign('/');
  }

  return (
    <Page background="muted" center>
      <Block as="section" align="center" justify="center" grow>
        <Block width="full" maxWidth="sm" direction="column" gap="lg">
          <PageHeader title="Choose an organization" description="Select which organization to work in." />
          <Card variant="raised">
            <CardHeader>
              <CardTitle>Your organizations</CardTitle>
            </CardHeader>
            <CardContent>
              <Stack gap="md">
                {error() ? <p role="alert">{error()}</p> : null}
                {tenants() === null && !error() ? <p>Loading…</p> : null}
                {(tenants() ?? []).map((tenant) => (
                  <Button
                    key={tenant.tenantId}
                    variant="secondary"
                    width="full"
                    onPress={() => select(tenant.slug)}
                  >
                    {tenant.name}
                  </Button>
                ))}
                <Button asChild variant="ghost" width="full">
                  <a href="/organizations/new">Create a new organization</a>
                </Button>
              </Stack>
            </CardContent>
          </Card>
        </Block>
      </Block>
    </Page>
  );
}

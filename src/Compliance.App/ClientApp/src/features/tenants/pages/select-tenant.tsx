import { resource } from '@askrjs/askr/resources';
import {
  Block,
  Button,
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
  Page,
  PageHeader,
  Spinner,
  Stack,
} from '@askrjs/themes/components';

import { listMyTenants } from '../tenants.js';

export function SelectTenantPage() {
  const tenants = resource(() => listMyTenants(), []);

  const memberships = tenants.pending ? null : tenants.value;
  const error = tenants.error?.message ?? null;
  const noAccess = memberships !== null && memberships.length === 0;

  return (
    <Page background="muted" center>
      <Block as="section" align="center" justify="center" grow>
        <Block width="full" maxWidth="sm" direction="column" gap="lg">
          <PageHeader
            title={noAccess ? 'No organization access yet' : 'Choose an organization'}
            description={
              noAccess
                ? 'You are signed in, but you are not a member of any organization.'
                : 'Select which organization to work in.'
            }
          />
          <Card variant="raised">
            <CardHeader>
              <CardTitle>{noAccess ? 'Get access' : 'Your organizations'}</CardTitle>
              {noAccess ? (
                <CardDescription>
                  Ask an administrator of your organization to invite you, or create a new
                  organization. Creating an organization requires a verified email address.
                </CardDescription>
              ) : null}
            </CardHeader>
            <CardContent>
              <Stack gap="md">
                {error ? (
                  <Stack gap="sm">
                    <p role="alert">{error}</p>
                    <Button variant="secondary" width="full" onPress={() => tenants.refresh()}>
                      Try again
                    </Button>
                  </Stack>
                ) : null}
                {memberships === null && !error ? <Spinner label="Loading organizations" /> : null}
                {(memberships ?? []).map((tenant) => (
                  <Button asChild variant="secondary" width="full">
                    <a href={`/${tenant.slug}`}>{tenant.name}</a>
                  </Button>
                ))}
                <Button asChild variant={noAccess ? 'primary' : 'ghost'} width="full">
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

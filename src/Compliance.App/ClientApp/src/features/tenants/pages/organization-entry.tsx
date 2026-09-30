import { resource } from '@askrjs/askr/resources';
import { Block, Button, EmptyState, Page, Spinner } from '@askrjs/themes/components';

import { chooseOrganizationEntry, listMyTenants, readRememberedSlug } from '../tenants.js';

// Sends a signed-in user who has not named an organization to the right place. `suffix` keeps
// pre-slug links such as /teams working by opening the same page inside the organization.
function OrganizationEntry({ suffix }: { suffix: string }) {
  const entry = resource(async () => {
    const memberships = await listMyTenants();
    const next = chooseOrganizationEntry(memberships, readRememberedSlug(), suffix);
    window.location.replace(next.kind === 'enter' ? next.href : '/organizations');
    return next;
  }, [suffix]);

  if (entry.error) {
    return (
      <Page>
        <EmptyState
          title="Your organizations could not be loaded"
          titleAs="h1"
          description={entry.error.message}
          action={
            <Button variant="primary" onPress={() => entry.refresh()}>
              Try again
            </Button>
          }
        />
      </Page>
    );
  }

  return (
    <Page background="muted" center>
      <Block as="section" align="center" justify="center" grow>
        <Spinner label="Opening your organization" />
      </Block>
    </Page>
  );
}

export function OrganizationEntryPage() {
  return <OrganizationEntry suffix="" />;
}

export function TeamsEntryPage() {
  return <OrganizationEntry suffix="/teams" />;
}

export function RolesEntryPage() {
  return <OrganizationEntry suffix="/roles" />;
}

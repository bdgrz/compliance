import { resource } from '@askrjs/askr/resources';
import {
  Button,
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
  EmptyState,
  Page,
  PageHeader,
  Spinner,
  Stack,
} from '@askrjs/themes/components';

import { organizationPath } from '../../tenants/tenants.js';
import { getMemberAccess, MemberRequestError } from '../members.js';
import { AccessGrantsCard } from './access-grants-card.js';
import { MembershipPanel } from './membership-panel.js';



export function MemberAccessPage({ userId }: { userId: string }) {
  const access = resource(() => getMemberAccess(userId), [userId]);
  const back = (
    <a href={organizationPath('/members')}>Back to members</a>
  );

  if (access.pending) {
    return (
      <Page>
        <Spinner label="Loading access" />
      </Page>
    );
  }

  if (access.error) {
    const status = access.error instanceof MemberRequestError ? access.error.status : null;
    return (
      <Page>
        <EmptyState
          title={status === 403 ? 'Access details are not available to you' : status === 404 ? 'Member not found' : 'Access could not be loaded'}
          titleAs="h1"
          description={access.error.message}
          action={
            status === 403 || status === 404 ? (
              back
            ) : (
              <Button variant="primary" onPress={() => access.refresh()}>
                Try again
              </Button>
            )
          }
        />
      </Page>
    );
  }

  const value = access.value!;
  const permissions = value.effective_permissions.filter((p): p is string => typeof p === 'string');
  const teamPaths = value.paths.filter((p) => p !== null);

  return (
    <Page>
      <PageHeader
        title={`Member ${value.user_id.slice(0, 8)}`}
        description="Why this member can do what they can do."
      />
      <Stack gap="md">
        {back}
        <MembershipPanel userId={userId} onChanged={() => access.refresh()} />
        <Card>
          <CardHeader>
            <CardTitle>Effective permissions</CardTitle>
            <CardDescription>The combined result of every team and grant below.</CardDescription>
          </CardHeader>
          <CardContent>
            {permissions.length === 0 ? (
              <p>This member currently has no permissions.</p>
            ) : (
              <ul className="plain-list">
                {permissions.map((permission) => (
                  <li>
                    <code>{permission}</code>
                  </li>
                ))}
              </ul>
            )}
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>Through teams</CardTitle>
          </CardHeader>
          <CardContent>
            {teamPaths.length === 0 ? (
              <p>No team memberships grant access.</p>
            ) : (
              <ul className="plain-list">
                {teamPaths.map((path) => (
                  <li>
                    Team <strong>{path.team_name}</strong> has role <strong>{path.role_name}</strong>
                  </li>
                ))}
              </ul>
            )}
          </CardContent>
        </Card>
        <AccessGrantsCard access={value} onChanged={() => access.refresh()} />
      </Stack>
    </Page>
  );
}

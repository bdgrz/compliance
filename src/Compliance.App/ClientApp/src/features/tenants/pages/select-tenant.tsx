import { currentAuth } from '@askrjs/askr/router';
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

import {
  listMyTenants,
  readEmailVerification,
  type EmailVerification,
} from '../tenants.js';
import { EmailVerificationForm } from '../../authentication/email-verification.js';

const accessGuidance: Record<EmailVerification, string> = {
  verified:
    'Ask an administrator of your organization to invite you, or create a new organization. Your email address is verified, so you can create one now and become its first Org Admin.',
  unverified:
    'Ask an administrator of your organization to invite you. Creating an organization requires a verified email address: verify an email address you own, then return here.',
  unknown:
    'Ask an administrator of your organization to invite you, or create a new organization. Creating an organization requires a verified email address.',
};

export function SelectTenantPage({ userId }: { userId?: string | null } = {}) {
  const viewer =
    userId === undefined ? (currentAuth().principal?.id ?? null) : userId;
  const tenants = resource(() => listMyTenants(), []);
  const verification = resource(() => readEmailVerification(viewer), [viewer]);

  const memberships = tenants.pending ? null : tenants.value;
  const error = tenants.error?.message ?? null;
  const noAccess =
    memberships !== null &&
    memberships !== undefined &&
    memberships.length === 0;
  const verified: EmailVerification = verification.value ?? 'unknown';
  const canCreate = !noAccess || verified !== 'unverified';

  return (
    <Page background="muted" center>
      <Block as="section" align="center" justify="center" grow>
        <Block width="full" maxWidth="sm" direction="column" gap="lg">
          <PageHeader
            title={
              noAccess ? 'No organization access yet' : 'Choose an organization'
            }
            description={
              noAccess
                ? 'You are signed in, but you are not a member of any organization.'
                : 'Select which organization to work in.'
            }
          />
          <Card variant="raised">
            <CardHeader>
              <CardTitle>
                {noAccess ? 'Get access' : 'Your organizations'}
              </CardTitle>
              {noAccess ? (
                <CardDescription>{accessGuidance[verified]}</CardDescription>
              ) : null}
            </CardHeader>
            <CardContent>
              <Stack gap="md">
                {error ? (
                  <Stack gap="sm">
                    <p role="alert">{error}</p>
                    <Button
                      variant="secondary"
                      width="full"
                      onPress={() => tenants.refresh()}
                    >
                      Try again
                    </Button>
                  </Stack>
                ) : null}
                {memberships === null && !error ? (
                  <Spinner label="Loading organizations" />
                ) : null}
                {noAccess && verified === 'unverified' && viewer ? (
                  <EmailVerificationForm
                    userId={viewer}
                    onVerified={() => verification.refresh()}
                  />
                ) : null}
                {(memberships ?? []).map((tenant) => (
                  <Button asChild variant="secondary" width="full">
                    <a href={`/${tenant.slug}`}>{tenant.name}</a>
                  </Button>
                ))}
                {canCreate ? (
                  <Button
                    asChild
                    variant={noAccess ? 'primary' : 'ghost'}
                    width="full"
                  >
                    <a href="/organizations/new">Create a new organization</a>
                  </Button>
                ) : null}
              </Stack>
            </CardContent>
          </Card>
        </Block>
      </Block>
    </Page>
  );
}

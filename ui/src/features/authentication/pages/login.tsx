import { LogInIcon } from '@askrjs/lucide';
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

import { ComplianceBrand } from '../../../components/compliance-brand.js';
import {
  beginSignIn,
  readAuthenticationError,
} from '../auth.js';

export function LoginPage() {
  const next = new URLSearchParams(window.location.search).get('next');
  const error = readAuthenticationError();

  return (
    <Page background="muted" center>
      <Block as="section" align="center" justify="center" grow>
        <Block width="full" maxWidth="sm" direction="column" gap="lg">
          <Card variant="raised">
            <CardHeader>
              <ComplianceBrand />
              <CardTitle>Sign in to your workspace</CardTitle>
              <CardDescription>
                Continue with your organization&apos;s identity provider.
              </CardDescription>
            </CardHeader>
            <CardContent>
              <Stack gap="md">
                {error ? <p role="alert">{error}</p> : null}
                <Button
                  variant="primary"
                  width="full"
                  onPress={() => void beginSignIn(next ?? '/')}
                >
                  <LogInIcon size={16} aria-hidden="true" />
                  <span>Continue to sign in</span>
                </Button>
              </Stack>
            </CardContent>
          </Card>
        </Block>
      </Block>
    </Page>
  );
}

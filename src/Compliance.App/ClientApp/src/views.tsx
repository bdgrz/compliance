import { Link } from '@askrjs/askr/router';
import { FileCheckIcon, ShieldCheckIcon } from '@askrjs/lucide';
import {
  Block,
  Button,
  Card,
  CardContent,
  Container,
  EmptyState,
  Header,
  Main,
  NavBrand,
  Navbar,
  PageHeader,
  Stack,
} from '@askrjs/themes/components';
import { ThemeScope } from '@askrjs/themes/theme';

import { beginSignIn, readAuthenticationError, signOut } from './auth.js';

function ComplianceLayout({
  children,
  signedIn = false,
}: {
  children?: unknown;
  signedIn?: boolean;
}) {
  return (
    <ThemeScope storageKey="bdgrz-compliance-theme">
      <Header position="sticky">
        <Container size="xl" paddingY="lg">
          <Navbar>
            <NavBrand>
              <Link href="/" aria-label="Compliance home">
                <ShieldCheckIcon aria-hidden="true" /> Compliance
              </Link>
            </NavBrand>
            {signedIn ? (
              <Button variant="ghost" onPress={signOut}>
                <span>Sign out</span>
              </Button>
            ) : null}
          </Navbar>
        </Container>
      </Header>
      <Main>
        <Container size="xl" paddingY="2xl">
          <Stack gap="2xl">{children}</Stack>
        </Container>
      </Main>
    </ThemeScope>
  );
}

export function LoginPage() {
  const next = new URLSearchParams(window.location.search).get('next');
  const error = readAuthenticationError();

  return (
    <ComplianceLayout>
      <PageHeader
        title="Sign in to Compliance"
        description="Continue with your organization's identity provider."
      />
      <Card>
        <CardContent>
          <Stack gap="md">
            {error ? <span role="alert">{error}</span> : null}
            <Button
              variant="primary"
              onPress={() => void beginSignIn(next ?? '/')}
            >
              <span>Continue to sign in</span>
            </Button>
          </Stack>
        </CardContent>
      </Card>
    </ComplianceLayout>
  );
}

export function AuthenticationCallbackPage() {
  return (
    <ComplianceLayout>
      <PageHeader
        title="Completing sign in"
        description="Validating the identity provider response."
      />
    </ComplianceLayout>
  );
}

export function HomePage() {
  return (
    <ComplianceLayout signedIn>
      <PageHeader
        title="Compliance workspace"
        description="Review controls, collect evidence, and coordinate compliance work."
      />
      <Card>
        <CardContent>
          <Block direction="row" gap="md" align="center">
            <FileCheckIcon aria-hidden="true" />
            <Stack gap="sm">
              <strong>Application shell is ready</strong>
              <span>
                AskrJS is bundled through the ASP.NET Core static web assets
                pipeline.
              </span>
            </Stack>
          </Block>
        </CardContent>
      </Card>
    </ComplianceLayout>
  );
}

export function NotFoundPage() {
  return (
    <ComplianceLayout signedIn>
      <EmptyState
        title="Page not found"
        description="The requested compliance page does not exist."
        action={
          <Button asChild variant="primary">
            <Link href="/">Return home</Link>
          </Button>
        }
      />
    </ComplianceLayout>
  );
}

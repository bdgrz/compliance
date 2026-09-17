import { currentAuth, currentRoute } from '@askrjs/askr/router';
import { LogOutIcon, MoonIcon, SunIcon } from '@askrjs/lucide';
import {
  Block,
  Button,
  Container,
  Header,
  Navbar,
  NavBrand,
  NavGroup,
  NavLink,
} from '@askrjs/themes/components';
import { ThemeScope, ThemeToggle } from '@askrjs/themes/theme';
import { OverlayHost } from '@askrjs/ui';

import { ComplianceBrand } from '../components/compliance-brand.js';
import { signOut } from '../features/authentication/auth.js';

export function PageLayout({ children }: { children?: unknown }) {
  const route = currentRoute();
  const isAuthenticationRoute =
    route.path === '/login' || route.path === '/auth/callback';

  return (
    <ThemeScope defaultTheme="light" storageKey="bdgrz-compliance-theme">
      <OverlayHost>
        <Block minHeight="screen" direction="column">
          {!isAuthenticationRoute ? (
            <Header sticky>
              <Container size="xl" paddingY="sm">
                <Navbar aria-label="Primary navigation">
                  <NavBrand>
                    <ComplianceBrand />
                  </NavBrand>
                  <NavGroup>
                    <Block hide={{ base: true, sm: false }}>
                      <NavLink href="/">Overview</NavLink>
                    </Block>
                    <Block hide={{ base: true, sm: false }}>
                      <NavLink href="/teams">Teams</NavLink>
                    </Block>
                  </NavGroup>
                  <NavGroup align="end">
                    <ThemeToggle
                      aria-label="Toggle theme"
                      variant="ghost"
                      size="icon"
                      lightIcon={<SunIcon size={16} aria-hidden="true" />}
                      darkIcon={<MoonIcon size={16} aria-hidden="true" />}
                    />
                    {currentAuth().authenticated ? (
                      <Button variant="ghost" onPress={() => void signOut()}>
                        <LogOutIcon size={16} aria-hidden="true" />
                        <Block as="span" hide={{ base: true, sm: false }}>
                          Sign out
                        </Block>
                      </Button>
                    ) : null}
                  </NavGroup>
                </Navbar>
              </Container>
            </Header>
          ) : null}
          {children}
        </Block>
      </OverlayHost>
    </ThemeScope>
  );
}

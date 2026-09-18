import { Link, currentAuth, currentRoute } from '@askrjs/askr/router';
import { HomeIcon, LogOutIcon, MoonIcon, SunIcon, UsersIcon } from '@askrjs/lucide';
import {
  Block,
  Button,
  Container,
  Grid,
  Header,
  Navbar,
  NavBrand,
  NavGroup,
  Sidebar,
  SidebarContent,
  SidebarGroup,
  SidebarGroupContent,
  SidebarGroupLabel,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
} from '@askrjs/themes/components';
import { ThemeScope, ThemeToggle } from '@askrjs/themes/theme';
import { OverlayHost } from '@askrjs/ui';

import { ComplianceBrand } from '../components/compliance-brand.js';
import { signOut } from '../features/authentication/auth.js';

const primaryNavLinks = [
  { title: 'Overview', href: '/', icon: HomeIcon, exact: true },
  { title: 'Teams', href: '/teams', icon: UsersIcon, exact: false },
];

function isActiveNavLink(currentPath: string, href: string, exact: boolean) {
  return exact ? currentPath === href : currentPath === href || currentPath.startsWith(`${href}/`);
}

function PrimaryNavigation() {
  const route = currentRoute();

  return (
    <Sidebar
      class="app-sidebar"
      collapsible="none"
      minHeight="auto"
      padding="md"
      borderRight={false}
      shrink={false}
      width="full"
      aria-label="Primary navigation"
    >
      <SidebarContent>
        <SidebarGroup>
          <SidebarGroupLabel>Workspace</SidebarGroupLabel>
          <SidebarGroupContent>
            <SidebarMenu>
              {primaryNavLinks.map((link) => (
                <SidebarMenuItem>
                  <SidebarMenuButton
                    active={isActiveNavLink(route.path, link.href, link.exact)}
                    asChild
                  >
                    <Link
                      href={link.href}
                      aria-current={
                        isActiveNavLink(route.path, link.href, link.exact) ? 'page' : undefined
                      }
                    >
                      <link.icon size={16} aria-hidden="true" />
                      <span>{link.title}</span>
                    </Link>
                  </SidebarMenuButton>
                </SidebarMenuItem>
              ))}
            </SidebarMenu>
          </SidebarGroupContent>
        </SidebarGroup>
      </SidebarContent>
    </Sidebar>
  );
}

export function PageLayout({ children }: { children?: unknown }) {
  const route = currentRoute();
  const isAuthenticationRoute =
    route.path === '/login' || route.path === '/auth/callback';
  const isOnboardingRoute =
    route.path === '/organizations' || route.path === '/organizations/new';
  const showAppShell =
    !isAuthenticationRoute && !isOnboardingRoute && currentAuth().authenticated;

  return (
    <ThemeScope defaultTheme="light" storageKey="bdgrz-compliance-theme">
      <OverlayHost>
        <Block minHeight="screen" direction="column">
          {!isAuthenticationRoute ? (
            <Header sticky>
              <Container size="xl" paddingY="sm">
                <Navbar aria-label="Site header">
                  <NavBrand>
                    <ComplianceBrand />
                  </NavBrand>
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
          {showAppShell ? (
            <Container size="xl" width="full" paddingY="0" grow>
              <Grid
                class="app-shell-layout"
                columns={{ base: '1fr', md: '13rem minmax(0, 1fr)' }}
                gap="md"
                align="start"
              >
                <PrimaryNavigation />
                <Block class="app-content-surface">{children}</Block>
              </Grid>
            </Container>
          ) : (
            children
          )}
        </Block>
      </OverlayHost>
    </ThemeScope>
  );
}

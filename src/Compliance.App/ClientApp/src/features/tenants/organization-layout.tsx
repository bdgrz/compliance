import { state } from '@askrjs/askr';
import { Link, currentRoute } from '@askrjs/askr/router';
import { resource } from '@askrjs/askr/resources';
import { ArrowLeftRightIcon, ClipboardListIcon, DatabaseIcon, HomeIcon, ShieldIcon, UserPlusIcon, UsersIcon } from '@askrjs/lucide';
import {
  Block,
  Button,
  Container,
  EmptyState,
  Grid,
  Page,
  Sidebar,
  SidebarContent,
  SidebarGroup,
  SidebarGroupContent,
  SidebarGroupLabel,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  Spinner,
} from '@askrjs/themes/components';

import {
  organizationPath,
  recordSlugMove,
  resolveTenantRoute,
  takeSlugMove,
  type ActiveTenant,
} from './tenants.js';

const organizationNavLinks = [
  { title: 'Overview', path: '', icon: HomeIcon, exact: true },
  { title: 'Programs', path: '/programs', icon: ClipboardListIcon, exact: false },
  { title: 'Inventory', path: '/inventory', icon: DatabaseIcon, exact: false },
  { title: 'Members', path: '/members', icon: UserPlusIcon, exact: false },
  { title: 'Teams', path: '/teams', icon: UsersIcon, exact: false },
  { title: 'Roles', path: '/roles', icon: ShieldIcon, exact: false },
];

function isActiveNavLink(currentPath: string, href: string, exact: boolean) {
  return exact ? currentPath === href : currentPath === href || currentPath.startsWith(`${href}/`);
}

function OrganizationNavigation() {
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
      aria-label="Organization navigation"
    >
      <SidebarContent>
        <SidebarGroup>
          <SidebarGroupLabel>Workspace</SidebarGroupLabel>
          <SidebarGroupContent>
            <SidebarMenu>
              {organizationNavLinks.map((link) => {
                const href = organizationPath(link.path);
                const active = isActiveNavLink(route.path, href, link.exact);
                return (
                  <SidebarMenuItem>
                    <SidebarMenuButton active={active} asChild>
                      <Link href={href} aria-current={active ? 'page' : undefined}>
                        <link.icon size={16} aria-hidden="true" />
                        <span>{link.title}</span>
                      </Link>
                    </SidebarMenuButton>
                  </SidebarMenuItem>
                );
              })}
            </SidebarMenu>
          </SidebarGroupContent>
        </SidebarGroup>
      </SidebarContent>
    </Sidebar>
  );
}

export function OrganizationLoading() {
  return (
    <Page background="muted" center>
      <Block as="section" align="center" justify="center" grow>
        <Spinner label="Opening organization" />
      </Block>
    </Page>
  );
}

export function OrganizationFailed({ message, onRetry }: { message: string; onRetry: () => void }) {
  return (
    <Page>
      <EmptyState
        title="Organization could not be opened"
        titleAs="h1"
        description={message}
        action={
          <Button variant="primary" onPress={onRetry}>
            Try again
          </Button>
        }
      />
    </Page>
  );
}

// Deliberately identical for unknown, retired, and denied slugs.
export function OrganizationUnavailable() {
  return (
    <Page>
      <EmptyState
        title="Organization unavailable"
        titleAs="h1"
        description="This organization does not exist, or you do not have access to it."
        action={
          <Button asChild variant="primary">
            <a href="/organizations">Choose an organization</a>
          </Button>
        }
      />
    </Page>
  );
}

export function OrganizationBar({ tenant, movedFrom }: { tenant: ActiveTenant; movedFrom: string | null }) {
  return (
    <Block class="organization-bar" direction="column" gap="xs">
      <Block direction="row" align="center" justify="between" gap="sm" wrap>
        <Block direction="column">
          <span class="organization-bar-label">Organization</span>
          <strong class="organization-bar-name">{tenant.name}</strong>
        </Block>
        <Button asChild variant="ghost" size="sm">
          <a href="/organizations">
            <ArrowLeftRightIcon size={16} aria-hidden="true" />
            <span>Switch organization</span>
          </a>
        </Button>
      </Block>
      {movedFrom ? (
        <p role="status" class="organization-bar-notice">
          This organization's address changed from /{movedFrom} to /{tenant.slug}. Update any saved
          links.
        </p>
      ) : null}
    </Block>
  );
}

export function OrganizationLayout({ children }: { children?: unknown }) {
  const slug = String(currentRoute().params.slug ?? '');
  const [movedFrom] = state(takeSlugMove());
  const resolution = resource(async () => {
    const next = await resolveTenantRoute(slug, window.location);
    if (next.kind === 'redirect') {
      recordSlugMove(slug);
      window.location.replace(next.href);
    } else if (next.kind === 'reload') {
      window.location.replace(next.href);
    }
    return next;
  }, [slug]);

  const current = resolution.value;
  if (resolution.error || current?.kind === 'failed') {
    return (
      <OrganizationFailed
        message={
          current?.kind === 'failed'
            ? current.message
            : (resolution.error?.message ?? 'Unable to open this organization.')
        }
        onRetry={() => resolution.refresh()}
      />
    );
  }

  if (current?.kind === 'unavailable') {
    return <OrganizationUnavailable />;
  }

  if (resolution.pending || current?.kind !== 'ready') {
    return <OrganizationLoading />;
  }

  return (
    <Container size="xl" width="full" paddingY="0" grow>
      <Grid
        class="app-shell-layout"
        columns={{ base: '1fr', md: '13rem minmax(0, 1fr)' }}
        gap="md"
        align="start"
      >
        <OrganizationNavigation />
        <Block class="app-content-surface" direction="column" gap="md">
          <OrganizationBar tenant={current.tenant} movedFrom={movedFrom()} />
          {children}
        </Block>
      </Grid>
    </Container>
  );
}

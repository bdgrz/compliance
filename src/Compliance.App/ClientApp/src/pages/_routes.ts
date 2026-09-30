import { requireAnonymous, requireUser } from '@askrjs/auth';
import { createRouteRegistry, group, lazy, route } from '@askrjs/askr/router';

import {
  normalizeReturnPath,
  resolveBrowserAuthentication,
} from '../features/authentication/auth.js';
import { OrganizationLayout } from '../features/tenants/organization-layout.js';
import { PageLayout } from './_layout.js';

const AuthenticationCallbackPage = lazy(() =>
  import('../features/authentication/pages/auth-callback.js').then(
    (module) => module.AuthenticationCallbackPage
  )
);
const HomePage = lazy(() =>
  import('./home.js').then((module) => module.HomePage)
);
const LoginPage = lazy(() =>
  import('../features/authentication/pages/login.js').then(
    (module) => module.LoginPage
  )
);
const DeveloperLoginPage = lazy(() =>
  import('../features/authentication/pages/developer-login.js').then(
    (module) => module.DeveloperLoginPage
  )
);
const NotFoundPage = lazy(() =>
  import('./not-found.js').then((module) => module.NotFoundPage)
);
const CreateTenantPage = lazy(() =>
  import('../features/tenants/pages/create-tenant.js').then(
    (module) => module.CreateTenantPage
  )
);
const OrganizationEntryPage = lazy(() =>
  import('../features/tenants/pages/organization-entry.js').then(
    (module) => module.OrganizationEntryPage
  )
);
const TeamsEntryPage = lazy(() =>
  import('../features/tenants/pages/organization-entry.js').then(
    (module) => module.TeamsEntryPage
  )
);
const RolesEntryPage = lazy(() =>
  import('../features/tenants/pages/organization-entry.js').then(
    (module) => module.RolesEntryPage
  )
);
const SelectTenantPage = lazy(() =>
  import('../features/tenants/pages/select-tenant.js').then(
    (module) => module.SelectTenantPage
  )
);
const TeamsListPage = lazy(() =>
  import('../features/teams/pages/teams-list.js').then(
    (module) => module.TeamsListPage
  )
);
const TeamDetailPage = lazy(() =>
  import('../features/teams/pages/team-detail.js').then(
    (module) => module.TeamDetailPage
  )
);
const MembersPage = lazy(() =>
  import('../features/members/pages/members-list.js').then((module) => module.MembersPage)
);
const MemberAccessPage = lazy(() =>
  import('../features/members/pages/member-access.js').then((module) => module.MemberAccessPage)
);
const ProgramsPage = lazy(() =>
  import('../features/programs/pages/programs-list.js').then((module) => module.ProgramsPage)
);
const ProgramDetailPage = lazy(() =>
  import('../features/programs/pages/program-detail.js').then((module) => module.ProgramDetailPage)
);
const RolesListPage = lazy(() =>
  import('../features/roles/pages/roles-list.js').then(
    (module) => module.RolesListPage
  )
);
const RoleDetailPage = lazy(() =>
  import('../features/roles/pages/role-detail.js').then(
    (module) => module.RoleDetailPage
  )
);

export const pageRegistry = createRouteRegistry(
  () => {
    group({ layout: PageLayout }, () => {
      route('/login', LoginPage, { auth: requireAnonymous() });
      route('/developer-login', DeveloperLoginPage, { auth: requireAnonymous() });
      route('/auth/callback', AuthenticationCallbackPage);

      group({ auth: requireUser() }, () => {
        route('/', OrganizationEntryPage);
        route('/organizations', SelectTenantPage);
        route('/organizations/new', CreateTenantPage);
        // Links from before organization slugs existed open the same page in the user's organization.
        route('/teams', TeamsEntryPage);
        route('/roles', RolesEntryPage);

        // Every organization page lives under its slug. The layout resolves the slug to the opaque
        // tenant_id that API calls use, and handles unavailable, renamed, and switched organizations.
        group({ layout: OrganizationLayout }, () => {
          route('/{slug}', HomePage, {
            meta: {
              title: 'Badgers - The Compliance Platform',
              description: 'Badgers - The Compliance Platform',
              html: { lang: 'en', dir: 'ltr' },
            },
          });
          route('/{slug}/members', MembersPage);
          route('/{slug}/members/{userId}', MemberAccessPage);
          route('/{slug}/programs', ProgramsPage);
          route('/{slug}/programs/{programId}', ProgramDetailPage);
          route('/{slug}/teams', TeamsListPage);
          route('/{slug}/teams/{teamId}', TeamDetailPage);
          route('/{slug}/roles', RolesListPage);
          route('/{slug}/roles/{roleId}', RoleDetailPage);
        });
        route('/*', NotFoundPage);
      });
    });
  },
  {
    auth: {
      resolve: resolveBrowserAuthentication,
      loginPath: (context) => `/login?next=${encodeURIComponent(context.href)}`,
      authenticatedRedirectTo: (context) =>
        normalizeReturnPath(
          new URLSearchParams(context.search).get('next'),
          window.location.origin
        ),
    },
  }
);

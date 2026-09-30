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
const TenantInventoryPage = lazy(() =>
  import('../features/operator/pages/tenant-inventory.js').then((module) => module.TenantInventoryPage)
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
const ProgramRisksPage = lazy(() =>
  import('../features/risks/pages/program-risks.js').then((module) => module.ProgramRisksPage)
);
const InventoryPage = lazy(() =>
  import('../features/inventory/pages/inventory.js').then((module) => module.InventoryPage)
);
const ApplicationsPage = lazy(() =>
  import('../features/applications/pages/applications-list.js').then((module) => module.ApplicationsPage)
);
const ApplicationDetailPage = lazy(() =>
  import('../features/applications/pages/application-detail.js').then((module) => module.ApplicationDetailPage)
);
const WorkforceRosterPage = lazy(() =>
  import('../features/workforce/pages/workforce-roster.js').then((module) => module.WorkforceRosterPage)
);
const PersonDetailPage = lazy(() =>
  import('../features/workforce/pages/person-detail.js').then((module) => module.PersonDetailPage)
);
const WorkRelationshipDetailPage = lazy(() =>
  import('../features/workforce/pages/relationship-detail.js').then((module) => module.WorkRelationshipDetailPage)
);
const WorkforceObservationsPage = lazy(() =>
  import('../features/workforce/pages/workforce-observations.js').then((module) => module.WorkforceObservationsPage)
);
const ServiceIdentitiesPage = lazy(() =>
  import('../features/workforce/pages/service-identities.js').then((module) => module.ServiceIdentitiesPage)
);
const ServiceIdentityDetailPage = lazy(() =>
  import('../features/workforce/pages/service-identity-detail.js').then((module) => module.ServiceIdentityDetailPage)
);
const ControlsPage = lazy(() =>
  import('../features/controls/pages/controls-list.js').then((module) => module.ControlsPage)
);
const ControlDetailPage = lazy(() =>
  import('../features/controls/pages/control-detail.js').then((module) => module.ControlDetailPage)
);
const BoundariesPage = lazy(() =>
  import('../features/boundaries/pages/boundaries-list.js').then((module) => module.BoundariesPage)
);
const BoundaryDetailPage = lazy(() =>
  import('../features/boundaries/pages/boundary-detail.js').then((module) => module.BoundaryDetailPage)
);
const BoundaryExceptionsPage = lazy(() =>
  import('../features/boundaries/pages/boundary-exceptions.js').then((module) => module.BoundaryExceptionsPage)
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
        route('/admin/tenants', TenantInventoryPage);
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
          route('/{slug}/programs/{programId}/risks', ProgramRisksPage);
          route('/{slug}/inventory', InventoryPage);
          route('/{slug}/applications', ApplicationsPage);
          route('/{slug}/applications/{applicationId}', ApplicationDetailPage);
          route('/{slug}/workforce', WorkforceRosterPage);
          route('/{slug}/workforce/people/{personId}', PersonDetailPage);
          route('/{slug}/workforce/relationships/{relationshipId}', WorkRelationshipDetailPage);
          route('/{slug}/workforce/observations', WorkforceObservationsPage);
          route('/{slug}/workforce/service-identities', ServiceIdentitiesPage);
          route('/{slug}/workforce/service-identities/{serviceIdentityId}', ServiceIdentityDetailPage);
          route('/{slug}/programs/{programId}/controls', ControlsPage);
          route('/{slug}/programs/{programId}/controls/{controlId}', ControlDetailPage);
          route('/{slug}/programs/{programId}/boundaries', BoundariesPage);
          route('/{slug}/programs/{programId}/boundaries/{boundaryId}', BoundaryDetailPage);
          route('/{slug}/programs/{programId}/boundaries/{boundaryId}/exceptions', BoundaryExceptionsPage);
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

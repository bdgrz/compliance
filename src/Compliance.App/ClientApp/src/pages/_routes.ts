import { requireAnonymous, requireUser } from '@askrjs/auth';
import { createRouteRegistry, group, lazy, route } from '@askrjs/askr/router';

import {
  normalizeReturnPath,
  resolveBrowserAuthentication,
} from '../features/authentication/auth.js';
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

export const pageRegistry = createRouteRegistry(
  () => {
    group({ layout: PageLayout }, () => {
      route('/login', LoginPage, { auth: requireAnonymous() });
      route('/developer-login', DeveloperLoginPage, { auth: requireAnonymous() });
      route('/auth/callback', AuthenticationCallbackPage);

      group({ auth: requireUser() }, () => {
        route('/', HomePage, {
          meta: {
            title: 'Badgers - The Compliance Platform',
            description: 'Badgers - The Compliance Platform',
            html: { lang: 'en', dir: 'ltr' },
          },
        });
        route('/organizations', SelectTenantPage);
        route('/organizations/new', CreateTenantPage);
        route('/teams', TeamsListPage);
        route('/teams/{teamId}', TeamDetailPage);
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

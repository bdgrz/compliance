import { requireAnonymous, requireUser } from '@askrjs/auth';
import { createRouteRegistry, group, lazy, route } from '@askrjs/askr/router';

import {
  normalizeReturnPath,
  resolveBrowserAuthentication,
} from '../features/authentication/auth.js';
import { PageLayout } from './_layout.js';

const AuthenticationCallbackPage = lazy(() =>
  import('./auth-callback.js').then(
    (module) => module.AuthenticationCallbackPage
  )
);
const HomePage = lazy(() =>
  import('./home.js').then((module) => module.HomePage)
);
const LoginPage = lazy(() =>
  import('./login.js').then((module) => module.LoginPage)
);
const NotFoundPage = lazy(() =>
  import('./not-found.js').then((module) => module.NotFoundPage)
);

export const pageRegistry = createRouteRegistry(
  () => {
    group({ layout: PageLayout }, () => {
      route('/login', LoginPage, { auth: requireAnonymous() });
      route('/auth/callback', AuthenticationCallbackPage);

      group({ auth: requireUser() }, () => {
        route('/', HomePage, {
          meta: {
            title: 'Compliance',
            description: 'Compliance operations and evidence workspace.',
            html: { lang: 'en', dir: 'ltr' },
          },
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

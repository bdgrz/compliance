import { requireAnonymous, requireUser } from '@askrjs/auth';
import { createRouteRegistry, group, route } from '@askrjs/askr/router';

import { resolveBrowserAuthentication } from './auth.js';
import {
  AuthenticationCallbackPage,
  HomePage,
  LoginPage,
  NotFoundPage,
} from './views.js';

export const pageRegistry = createRouteRegistry(
  () => {
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
  },
  {
    auth: {
      resolve: resolveBrowserAuthentication,
      loginPath: '/login',
      authenticatedRedirectTo: '/',
    },
  }
);

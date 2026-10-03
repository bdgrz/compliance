import { createSPA } from '@askrjs/askr/boot';

import { completeOidcCallbackIfPresent } from './features/authentication/auth.js';
import { pageRegistry } from './pages/_routes.js';
import './styles.css';

await completeOidcCallbackIfPresent();
await createSPA({
  root: '#app',
  registry: pageRegistry,
});

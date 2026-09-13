import { createSPA } from '@askrjs/askr/boot';

import { completeOidcCallbackIfPresent } from './auth.js';
import { pageRegistry } from './routes.js';
import './styles.css';

await completeOidcCallbackIfPresent();
await createSPA({
  root: '#app',
  registry: pageRegistry,
});

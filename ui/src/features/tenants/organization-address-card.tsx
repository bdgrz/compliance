import { state } from '@askrjs/askr';
import { currentAuth } from '@askrjs/askr/router';
import { resource } from '@askrjs/askr/resources';
import {
  Button,
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
  Stack,
} from '@askrjs/themes/components';

import { getMemberAccess, MemberRequestError } from '../members/members.js';
import {
  changeCurrentTenantSlug,
  currentTenant,
  recordSlugMove,
  slugPattern,
  slugRule,
  waitForTenantSlugRedirect,
} from './tenants.js';

export function OrganizationAddressCard() {
  const userId = currentAuth().principal?.id ?? '';
  const access = resource(() => (userId ? getMemberAccess(userId) : Promise.resolve(null)), [userId]);

  if (access.pending) return null;
  if (access.error) {
    if (access.error instanceof MemberRequestError && access.error.status === 403) return null;
    return (
      <Card>
        <CardHeader>
          <CardTitle>Organization address</CardTitle>
          <CardDescription>Address settings could not be loaded.</CardDescription>
        </CardHeader>
        <CardContent>
          <Stack gap="sm">
            <p role="alert">{access.error.message}</p>
            <Button variant="secondary" onPress={() => access.refresh()}>
              Try again
            </Button>
          </Stack>
        </CardContent>
      </Card>
    );
  }

  if (!access.value?.effective_permissions.includes('tenant.rbac.manage')) return null;
  const tenant = currentTenant();
  if (!tenant) return null;

  return <ChangeOrganizationAddress slug={tenant.slug} />;
}

function ChangeOrganizationAddress({ slug }: { slug: string }) {
  const [value, setValue] = state(slug);
  const [submitting, setSubmitting] = state(false);
  const [error, setError] = state<string | null>(null);
  const [notice, setNotice] = state<string | null>(null);

  async function submit(event?: { preventDefault?: () => void }) {
    event?.preventDefault?.();
    setError(null);
    setNotice(null);
    const entered = value().trim();
    if (!slugPattern.test(entered)) {
      setError(`That address is not valid. ${slugRule}`);
      return;
    }
    if (entered === slug) {
      setNotice('This organization already uses that address.');
      return;
    }

    setSubmitting(true);
    try {
      await changeCurrentTenantSlug(entered);
      setNotice(`Address change requested. Waiting for /${entered} to be confirmed…`);
      const confirmed = await waitForTenantSlugRedirect(slug, entered);
      if (!confirmed) {
        setNotice(
          `The address change is still processing. This organization will redirect from /${slug} ` +
            `to /${entered} when it is confirmed. Refresh this page in a moment.`
        );
        return;
      }

      recordSlugMove(slug);
      const rest = window.location.pathname.slice(slug.length + 1);
      window.location.replace(`/${entered}${rest}${window.location.search}${window.location.hash}`);
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : 'The change could not be saved.');
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>Organization address</CardTitle>
        <CardDescription>
          Current address: <code>/{slug}</code>. Members who open the old address will be redirected
          to the new one and see a notice to update saved links.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <form onSubmit={(event) => void submit(event)}>
          <Stack gap="md">
            <label className="registration-field">
              <span>New organization address</span>
              <input
                name="slug"
                type="text"
                autocomplete="off"
                spellCheck="false"
                value={value()}
                onInput={(event: Event) => setValue((event.target as HTMLInputElement).value)}
                required
              />
              <span className="role-explanation">{slugRule}</span>
            </label>
            {error() ? <p role="alert">{error()}</p> : null}
            {notice() ? <p role="status">{notice()}</p> : null}
            <Button variant="primary" type="submit" disabled={submitting()}>
              {submitting() ? 'Saving…' : 'Change organization address'}
            </Button>
          </Stack>
        </form>
      </CardContent>
    </Card>
  );
}

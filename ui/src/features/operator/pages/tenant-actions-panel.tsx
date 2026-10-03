import { state } from '@askrjs/askr';
import {
  Block,
  Button,
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
  Stack,
} from '@askrjs/themes/components';

import { slugPattern, slugRule } from '../../tenants/tenants.js';
import {
  changeTenantSlug,
  inviteFirmStaff,
  type TenantInventoryItem,
} from '../operator.js';

export type TenantAction = 'slug' | 'firm-staff';

interface Props {
  key?: string;
  item: TenantInventoryItem;
  action: TenantAction;
  onClose: () => void;
  onChanged: () => void;
}

// One organization's address change or firm-staff invitation, opened from the inventory row.
export function TenantActionsPanel({ item, action, onClose, onChanged }: Props) {
  const [value, setValue] = state('');
  const [submitting, setSubmitting] = state(false);
  const [error, setError] = state<string | null>(null);
  const [notice, setNotice] = state<string | null>(null);

  async function submit(event?: { preventDefault?: () => void }) {
    event?.preventDefault?.();
    setError(null);
    setNotice(null);
    const entered = value().trim();
    if (action === 'slug' && !slugPattern.test(entered)) {
      setError(`That address is not valid. ${slugRule}`);
      return;
    }
    setSubmitting(true);
    try {
      if (action === 'slug') {
        await changeTenantSlug(item.tenantId, entered);
        setNotice(
          `Address change requested for ${item.name}: /${item.slug} becomes /${entered}. Members who open the old address are redirected and told it moved.`
        );
      } else {
        await inviteFirmStaff(item.tenantId, entered);
        setNotice(
          `Invitation sent to ${entered}. They have no business-record access to ${item.name} until an accepted engagement assignment gives them a role.`
        );
      }
      setValue('');
      onChanged();
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : 'The change could not be saved.');
    } finally {
      setSubmitting(false);
    }
  }

  const isSlug = action === 'slug';
  return (
    <Card>
      <CardHeader>
        <CardTitle>
          {isSlug ? `Change address of ${item.name}` : `Add firm staff to ${item.name}`}
        </CardTitle>
        <CardDescription>
          {isSlug
            ? `The current address is /${item.slug}. The old address keeps redirecting to the new one.`
            : 'Firm staff have no access to business records. They reach them only through an accepted engagement assignment, which is accepted separately.'}
        </CardDescription>
      </CardHeader>
      <CardContent>
        <form onSubmit={(event) => void submit(event)}>
          <Stack gap="md">
            {isSlug ? (
              <label className="registration-field">
                <span>New address</span>
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
            ) : (
              <label className="registration-field">
                <span>Firm staff email address</span>
                <input
                  type="email"
                  autocomplete="off"
                  value={value()}
                  onInput={(event: Event) => setValue((event.target as HTMLInputElement).value)}
                  required
                />
              </label>
            )}
            {error() ? <p role="alert">{error()}</p> : null}
            {notice() ? <p role="status">{notice()}</p> : null}
            <Block direction="row" gap="sm" wrap>
              <Button variant="primary" type="submit" disabled={submitting()}>
                {submitting() ? 'Saving…' : isSlug ? 'Change address' : 'Send invitation'}
              </Button>
              <Button variant="ghost" type="button" onPress={onClose}>
                Close
              </Button>
            </Block>
          </Stack>
        </form>
      </CardContent>
    </Card>
  );
}

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

import type { TenantInventoryItem } from '../operator.js';

export type TenantLifecycleAction = 'suspend' | 'reactivate';

interface Props {
  key?: string;
  item: TenantInventoryItem;
  action: TenantLifecycleAction;
  submitting: boolean;
  onClose: () => void;
  onSubmit: (reason: string) => Promise<boolean>;
}

export function TenantLifecyclePanel({ item, action, submitting, onClose, onSubmit }: Props) {
  const [reason, setReason] = state('');
  const [error, setError] = state<string | null>(null);
  const isSuspension = action === 'suspend';

  async function submit(event?: { preventDefault?: () => void }) {
    event?.preventDefault?.();
    setError(null);
    const entered = reason().trim();
    if (entered.length === 0 || entered.length > 500) {
      setError('Enter a reason with at most 500 characters.');
      return;
    }
    if (await onSubmit(entered)) setReason('');
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>{isSuspension ? `Suspend ${item.name}` : `Reactivate ${item.name}`}</CardTitle>
        <CardDescription>
          {isSuspension
            ? 'Suspension immediately blocks access for client members and assigned firm staff while preserving records.'
            : 'Reactivation restores access for eligible client members and assigned firm staff.'}
        </CardDescription>
      </CardHeader>
      <CardContent>
        <form onSubmit={(event) => void submit(event)}>
          <Stack gap="md">
            <label className="registration-field">
              <span>{isSuspension ? 'Reason for suspension' : 'Reason for reactivation'}</span>
              <textarea
                name="reason"
                value={reason()}
                maxLength={500}
                onInput={(event: Event) => setReason((event.target as HTMLTextAreaElement).value)}
                required
              />
            </label>
            {error() ? <p role="alert">{error()}</p> : null}
            <Block direction="row" gap="sm" wrap>
              <Button variant={isSuspension ? 'destructive' : 'primary'} type="submit" disabled={submitting}>
                {submitting ? 'Saving…' : isSuspension ? 'Suspend organization' : 'Reactivate organization'}
              </Button>
              <Button variant="ghost" type="button" onPress={onClose}>
                Cancel
              </Button>
            </Block>
          </Stack>
        </form>
      </CardContent>
    </Card>
  );
}

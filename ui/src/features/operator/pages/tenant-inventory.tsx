import { state } from '@askrjs/askr';
import { resource } from '@askrjs/askr/resources';
import {
  Block,
  Button,
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
  EmptyState,
  Page,
  PageHeader,
  Spinner,
  Stack,
} from '@askrjs/themes/components';

import {
  listTenantInventory,
  OperatorRequestError,
  provisioningLabel,
  reactivateTenant,
  suspendTenant,
  type TenantInventoryItem,
} from '../operator.js';
import { PlatformOperatorsCard } from './platform-operators-card.js';
import { TenantActionsPanel, type TenantAction } from './tenant-actions-panel.js';
import { TenantLifecyclePanel, type TenantLifecycleAction } from './tenant-lifecycle-panel.js';

function isLifecycleAction(action: TenantAction | TenantLifecycleAction): action is TenantLifecycleAction {
  return action === 'suspend' || action === 'reactivate';
}

export function TenantInventoryPage() {
  const [cursors, setCursors] = state<(string | null)[]>([null]);
  const [pending, setPending] = state<string | null>(null);
  const [actionError, setActionError] = state<string | null>(null);
  const [notice, setNotice] = state<string | null>(null);
  const [version, setVersion] = state(0);
  const [selected, setSelected] = state<{
    item: TenantInventoryItem;
    action: TenantAction | TenantLifecycleAction;
  } | null>(null);

  const cursor = cursors()[cursors().length - 1] ?? null;
  const page = resource(() => listTenantInventory(cursor), [cursor, version()]);

  async function change(item: TenantInventoryItem, action: TenantLifecycleAction, reason: string): Promise<boolean> {
    setActionError(null);
    setNotice(null);
    setPending(item.tenantId);
    try {
      await (action === 'suspend'
        ? suspendTenant(item.tenantId, reason)
        : reactivateTenant(item.tenantId, reason));
      setNotice(
        action === 'suspend'
          ? `${item.name} is suspended. Its members cannot sign in to it until it is reactivated.`
          : `${item.name} is reactivated.`
      );
      setVersion(version() + 1);
      setSelected(null);
      return true;
    } catch (failure) {
      setActionError(failure instanceof Error ? failure.message : 'The change could not be saved.');
      return false;
    } finally {
      setPending(null);
    }
  }

  if (page.error instanceof OperatorRequestError && page.error.status === 403) {
    return (
      <Page>
        <EmptyState
          title="Operator access required"
          titleAs="h1"
          description="The organization inventory is available to platform operators only."
        />
      </Page>
    );
  }

  const items = page.value?.items ?? [];
  const selection = selected();
  const lifecycleAction = selection && isLifecycleAction(selection.action) ? selection.action : null;
  const tenantAction = selection && !isLifecycleAction(selection.action) ? selection.action : null;

  return (
    <Page>
      <PageHeader
        title="Organization inventory"
        description="Platform operator view of organization metadata and provisioning status."
      />
      <Card>
        <CardHeader>
          <CardTitle>Organizations</CardTitle>
          <CardDescription>
            This view shows organization metadata only. Operator status does not open any
            organization's business records.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <Stack gap="md">
            {actionError() ? <p role="alert">{actionError()}</p> : null}
            {notice() ? <p role="status">{notice()}</p> : null}
            {page.pending && !page.value ? (
              <Spinner label="Loading organizations" />
            ) : page.error ? (
              <Stack gap="sm">
                <p role="alert">{page.error.message}</p>
                <Button variant="secondary" onPress={() => page.refresh()}>
                  Try again
                </Button>
              </Stack>
            ) : items.length === 0 ? (
              <p>No organizations have been provisioned yet.</p>
            ) : (
              <table className="inventory-table">
                <caption className="visually-hidden">Organizations</caption>
                <thead>
                  <tr>
                    <th scope="col">Organization</th>
                    <th scope="col">Slug</th>
                    <th scope="col">Status</th>
                    <th scope="col">Provisioning</th>
                    <th scope="col">
                      <span className="visually-hidden">Actions</span>
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {items.map((item) => (
                    <tr>
                      <td>
                        {item.name}
                        {item.legalName && item.legalName !== item.name ? (
                          <span className="inventory-legal"> ({item.legalName})</span>
                        ) : null}
                      </td>
                      <td>
                        <code>{item.slug}</code>
                      </td>
                      <td>{item.status === 'suspended' ? 'Suspended' : item.status === 'active' ? 'Active' : item.status}</td>
                      <td>{provisioningLabel(item) ?? 'Complete'}</td>
                      <td>
                        <Block direction="row" gap="sm" wrap>
                        <Button
                          variant="secondary"
                          size="sm"
                          aria-label={`Change address of ${item.name}`}
                          onPress={() => setSelected({ item, action: 'slug' })}
                        >
                          Change address
                        </Button>
                        <Button
                          variant="secondary"
                          size="sm"
                          aria-label={`Add firm staff to ${item.name}`}
                          onPress={() => setSelected({ item, action: 'firm-staff' })}
                        >
                          Add firm staff
                        </Button>
                        {item.status === 'suspended' ? (
                          <Button
                            variant="secondary"
                            size="sm"
                            disabled={pending() !== null}
                            aria-label={`Reactivate ${item.name}`}
                            onPress={() => setSelected({ item, action: 'reactivate' })}
                          >
                            Reactivate
                          </Button>
                        ) : (
                          <Button
                            variant="destructive"
                            size="sm"
                            disabled={pending() !== null}
                            aria-label={`Suspend ${item.name}`}
                            onPress={() => setSelected({ item, action: 'suspend' })}
                          >
                            Suspend
                          </Button>
                        )}
                        </Block>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
            <Block direction="row" gap="sm" wrap>
              {cursors().length > 1 ? (
                <Button variant="ghost" onPress={() => setCursors(cursors().slice(0, -1))}>
                  Previous page
                </Button>
              ) : null}
              {page.value?.nextCursor ? (
                <Button variant="ghost" onPress={() => setCursors([...cursors(), page.value!.nextCursor])}>
                  Next page
                </Button>
              ) : null}
              <Button asChild variant="ghost">
                <a href="/organizations/new">Create an organization</a>
              </Button>
            </Block>
          </Stack>
        </CardContent>
      </Card>
      {selection && lifecycleAction ? (
          <TenantLifecyclePanel
            key={`${selection.item.tenantId}:${lifecycleAction}`}
            item={selection.item}
            action={lifecycleAction}
            submitting={pending() === selection.item.tenantId}
            onClose={() => setSelected(null)}
            onSubmit={(reason) => change(selection.item, lifecycleAction, reason)}
          />
      ) : selection && tenantAction ? (
          <TenantActionsPanel
            key={`${selection.item.tenantId}:${tenantAction}`}
            item={selection.item}
            action={tenantAction}
            onClose={() => setSelected(null)}
            onChanged={() => setVersion(version() + 1)}
          />
      ) : null}
      <PlatformOperatorsCard />
    </Page>
  );
}

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

export function TenantInventoryPage() {
  const [cursors, setCursors] = state<(string | null)[]>([null]);
  const [pending, setPending] = state<string | null>(null);
  const [actionError, setActionError] = state<string | null>(null);
  const [notice, setNotice] = state<string | null>(null);
  const [version, setVersion] = state(0);

  const cursor = cursors()[cursors().length - 1] ?? null;
  const page = resource(() => listTenantInventory(cursor), [cursor, version()]);

  async function change(item: TenantInventoryItem, suspend: boolean) {
    setActionError(null);
    setNotice(null);
    setPending(item.tenantId);
    try {
      await (suspend ? suspendTenant(item.tenantId) : reactivateTenant(item.tenantId));
      setNotice(
        suspend
          ? `${item.name} is suspended. Its members cannot sign in to it until it is reactivated.`
          : `${item.name} is reactivated.`
      );
      setVersion(version() + 1);
    } catch (failure) {
      setActionError(failure instanceof Error ? failure.message : 'The change could not be saved.');
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
                        {item.status === 'suspended' ? (
                          <Button
                            variant="secondary"
                            size="sm"
                            disabled={pending() !== null}
                            aria-label={`Reactivate ${item.name}`}
                            onPress={() => void change(item, false)}
                          >
                            Reactivate
                          </Button>
                        ) : (
                          <Button
                            variant="destructive"
                            size="sm"
                            disabled={pending() !== null}
                            aria-label={`Suspend ${item.name}`}
                            onPress={() => void change(item, true)}
                          >
                            Suspend
                          </Button>
                        )}
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
      <PlatformOperatorsCard />
    </Page>
  );
}

import { createApiClient } from '../../api-client/index.js';

const client = createApiClient();

export interface TenantInventoryItem {
  tenantId: string;
  name: string;
  slug: string;
  status: string;
  legalName: string | null;
  requiresInvitation: boolean;
  requiresActivation: boolean;
}

export interface TenantInventoryPage {
  items: TenantInventoryItem[];
  nextCursor: string | null;
}

export class OperatorRequestError extends Error {
  constructor(
    message: string,
    readonly status: number | null
  ) {
    super(message);
  }
}

// Platform-level tenant metadata for operators. It never includes a tenant's business records.
export async function listTenantInventory(cursor: string | null): Promise<TenantInventoryPage> {
  const result = await client.listTenants({ query: { cursor: cursor ?? undefined, limit: 50 } });
  if (!result.ok) throw failure(result, 'load the organization inventory');
  return {
    items: (result.data?.items ?? [])
      .filter((item) => item !== null)
      .map((item) => ({
        tenantId: item.tenant_id,
        name: item.name,
        slug: item.slug,
        status: item.status ?? 'active',
        legalName: item.legal_name ?? null,
        requiresInvitation: item.requires_invitation ?? false,
        requiresActivation: item.requires_activation ?? false,
      })),
    nextCursor: result.data?.next_cursor ?? null,
  };
}

export async function suspendTenant(tenantId: string): Promise<void> {
  const result = await client.suspendTenant({ params: { tenant_id: tenantId } });
  if (!result.ok) throw failure(result, 'suspend this organization');
}

export async function reactivateTenant(tenantId: string): Promise<void> {
  const result = await client.reactivateTenant({ params: { tenant_id: tenantId } });
  if (!result.ok) throw failure(result, 'reactivate this organization');
}

// Platform operators by user id. Operator status covers organization metadata and lifecycle only.
export async function listPlatformOperators(): Promise<string[]> {
  const result = await client.listPlatformOperators();
  if (!result.ok) throw failure(result, 'load the platform operators');
  return result.data?.user_ids ?? [];
}

export async function grantPlatformOperator(userId: string, reason: string): Promise<void> {
  const result = await client.grantPlatformOperator({ body: { user_id: userId.trim(), reason: reason.trim() } });
  if (!result.ok) throw failure(result, 'grant operator status');
}

export async function revokePlatformOperator(userId: string, reason: string): Promise<void> {
  const result = await client.revokePlatformOperator({ body: { user_id: userId, reason: reason.trim() } });
  if (!result.ok) throw failure(result, 'revoke operator status');
}

export const userIdPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export function provisioningLabel(item: TenantInventoryItem): string | null {
  if (item.requiresInvitation) return 'First administrator not invited';
  if (item.requiresActivation) return 'Awaiting first administrator activation';
  return null;
}

function failure(result: { ok: false; kind: string; status: number; error?: unknown }, action: string) {
  if (result.status === 403) {
    return new OperatorRequestError(`Only platform operators can ${action}.`, 403);
  }
  const detail =
    result.kind === 'http' && typeof result.error === 'object' && result.error !== null
      ? (result.error as { detail?: unknown }).detail
      : undefined;
  return new OperatorRequestError(
    typeof detail === 'string' && detail.length > 0 ? detail : `Unable to ${action}.`,
    result.kind === 'http' ? result.status : null
  );
}

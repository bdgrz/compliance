import { createApiClient } from '../../api-client/index.js';
import type {
  ListMyTenantsResponse200,
  RegisterTenantResponse200,
} from '../../api-client/operations.js';

const client = createApiClient();

const activeTenantKey = 'bdgrz.compliance.active-tenant-slug';

export interface TenantMembershipSummary {
  tenantId: string;
  name: string;
  slug: string;
}

export interface ActiveTenant {
  tenantId: string;
  slug: string;
}

// Mirrors TenantSlugs.TryNormalize's rule (src/Compliance.Core/Features/Tenants/TenantSlugs.cs) so
// the form can reject an invalid slug before a round trip — the server remains the source of truth
// and re-validates (reserved routes, retired slugs, and uniqueness can't be checked client-side).
export const slugPattern = /^[a-z](?:[a-z0-9]|-(?=[a-z0-9])){3,62}$/;

export async function registerTenant(
  name: string,
  slug: string
): Promise<RegisterTenantResponse200> {
  const result = await client.registerTenant({ body: { name, slug } });
  if (!result.ok) {
    throw new Error(describeFailure(result));
  }

  return result.data;
}

export async function listMyTenants(): Promise<TenantMembershipSummary[]> {
  const tenants: TenantMembershipSummary[] = [];
  let cursor: string | null = null;
  do {
    const result: Awaited<ReturnType<typeof client.listMyTenants>> =
      await client.listMyTenants({ query: { cursor: cursor ?? undefined } });
    if (!result.ok) {
      throw new Error(describeFailure(result));
    }

    const page: ListMyTenantsResponse200 = result.data;
    for (const item of page?.items ?? []) {
      if (item) {
        tenants.push({ tenantId: item.tenant_id, name: item.name, slug: item.slug });
      }
    }

    cursor = page?.next_cursor ?? null;
  } while (cursor !== null);

  return tenants;
}

export function readActiveTenant(): ActiveTenant | null {
  try {
    const stored = window.localStorage.getItem(activeTenantKey);
    if (!stored) {
      return null;
    }

    const parsed: unknown = JSON.parse(stored);
    return typeof parsed === 'object' &&
      parsed !== null &&
      typeof (parsed as ActiveTenant).tenantId === 'string' &&
      typeof (parsed as ActiveTenant).slug === 'string'
      ? (parsed as ActiveTenant)
      : null;
  } catch {
    return null;
  }
}

export function writeActiveTenant(tenant: ActiveTenant): void {
  try {
    window.localStorage.setItem(activeTenantKey, JSON.stringify(tenant));
  } catch {
    // Best-effort convenience only; nothing depends on this succeeding.
  }
}

// A different user signing in on the same browser must not silently inherit the previous user's
// tenant selection, so this is cleared on sign-out.
export function clearActiveTenant(): void {
  try {
    window.localStorage.removeItem(activeTenantKey);
  } catch {
    // Best-effort convenience only; nothing depends on this succeeding.
  }
}

// Implements the post-sign-in redirect rule: 0 tenants -> create, 1 -> remember and continue here,
// 2+ with none remembered yet -> let the caller send the user to the selector. Returns true when
// the caller should render its own page (an active tenant is settled), false when it already
// redirected and the caller should render nothing further.
export async function ensureActiveTenant(): Promise<boolean> {
  if (readActiveTenant() !== null) {
    return true;
  }

  const tenants = await listMyTenants();
  if (tenants.length === 0) {
    window.location.assign('/organizations/new');
    return false;
  }

  if (tenants.length === 1) {
    writeActiveTenant({ tenantId: tenants[0]!.tenantId, slug: tenants[0]!.slug });
    return true;
  }

  window.location.assign('/organizations');
  return false;
}

function describeFailure(result: { ok: false; kind: string; status?: number }): string {
  return result.kind === 'http'
    ? `The request failed (${result.status ?? 'unknown status'}).`
    : 'The request could not be completed.';
}

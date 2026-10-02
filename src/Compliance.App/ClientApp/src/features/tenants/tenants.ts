import { createApiClient } from '../../api-client/index.js';
import type {
  ListMyTenantsResponse200,
  RegisterTenantResponse200,
} from '../../api-client/operations.js';

const client = createApiClient();

const rememberedSlugKey = 'bdgrz.compliance.active-tenant-slug';
const movedFromKey = 'bdgrz.compliance.tenant-slug-moved-from';

export interface TenantMembershipSummary {
  tenantId: string;
  name: string;
  slug: string;
}

export interface ActiveTenant {
  tenantId: string;
  slug: string;
  name: string;
}

// Mirrors TenantSlugs.TryNormalize's rule (src/Compliance.Core/Features/Tenants/TenantSlugs.cs) so
// the form can reject an invalid slug before a round trip — the server remains the source of truth
// and re-validates (reserved routes, retired slugs, and uniqueness can't be checked client-side).
export const slugPattern = /^[a-z](?:[a-z0-9]|-(?=[a-z0-9])){3,62}$/;
export const slugRule =
  'Use 4 to 63 characters: lowercase letters, digits, and single hyphens, starting with a letter.';

// The organization resolved from the current document's URL. It lives only in memory: API adapters
// read the tenant_id from here, never from storage, so a request can only target the organization
// the URL names. Switching organizations reloads the document, which discards it together with
// every other piece of in-memory tenant data.
let activeTenant: ActiveTenant | null = null;

export function currentTenant(): ActiveTenant | null {
  return activeTenant;
}

export function requireActiveTenantId(): string {
  if (!activeTenant) {
    throw new Error('No active organization is selected.');
  }

  return activeTenant.tenantId;
}

// Browser path inside the active organization, e.g. organizationPath('/teams') -> '/acme/teams'.
export function organizationPath(path = ''): string {
  return activeTenant ? `/${activeTenant.slug}${path}` : '/';
}

export type TenantRouteResolution =
  | { kind: 'ready'; tenant: ActiveTenant }
  | { kind: 'redirect'; href: string }
  | { kind: 'reload'; href: string }
  | { kind: 'unavailable' }
  | { kind: 'failed'; message: string };

interface BrowserLocation {
  pathname: string;
  search: string;
  hash: string;
}

// Resolves the slug in the browser location to the organization it names. Unknown, retired, and
// denied slugs are all 'unavailable', so the page cannot reveal whether an organization exists. A
// renamed organization resolves to a redirect onto its current slug.
export async function resolveTenantRoute(
  slug: string,
  location: BrowserLocation
): Promise<TenantRouteResolution> {
  if (!slugPattern.test(slug)) {
    return { kind: 'unavailable' };
  }

  const result = await client.resolveMyTenantSlug({ params: { slug } });
  if (!result.ok) {
    return [400, 403, 404].includes(result.status)
      ? { kind: 'unavailable' }
      : { kind: 'failed', message: describeFailure(result) };
  }

  if (!result.data) {
    return { kind: 'unavailable' };
  }

  const { tenant_id: tenantId, current_slug: currentSlug, redirect } = result.data;
  if (redirect || currentSlug !== slug) {
    const rest = location.pathname.slice(slug.length + 1);
    return { kind: 'redirect', href: `/${currentSlug}${rest}${location.search}${location.hash}` };
  }

  // Another organization is already loaded in this document: reload so nothing held in memory for
  // it can reach this organization's pages.
  if (activeTenant && activeTenant.tenantId !== tenantId) {
    return { kind: 'reload', href: `${location.pathname}${location.search}${location.hash}` };
  }

  let memberships: TenantMembershipSummary[];
  try {
    memberships = await listMyTenants();
  } catch (failure) {
    return {
      kind: 'failed',
      message: failure instanceof Error ? failure.message : 'Unable to load your organizations.',
    };
  }

  const membership = memberships.find((tenant) => tenant.tenantId === tenantId);
  if (!membership) {
    return { kind: 'unavailable' };
  }

  activeTenant = { tenantId, slug: currentSlug, name: membership.name };
  rememberSlug(currentSlug);
  return { kind: 'ready', tenant: activeTenant };
}

export type OrganizationEntry =
  | { kind: 'enter'; href: string }
  | { kind: 'choose' }
  | { kind: 'none' };

// Where to send a signed-in user who has not named an organization: straight into their only
// organization, into the one they last used if it is still theirs, otherwise to the selector.
export function chooseOrganizationEntry(
  memberships: TenantMembershipSummary[],
  rememberedSlug: string | null,
  suffix = ''
): OrganizationEntry {
  if (memberships.length === 0) {
    return { kind: 'none' };
  }

  const target =
    memberships.length === 1
      ? memberships[0]
      : memberships.find((tenant) => tenant.slug === rememberedSlug);
  return target ? { kind: 'enter', href: `/${target.slug}${suffix}` } : { kind: 'choose' };
}

// Keeps the HTTP status so creation can explain the verified-email requirement (403) apart from
// validation problems such as a taken slug.
export class TenantRequestError extends Error {
  constructor(
    message: string,
    readonly status: number | null
  ) {
    super(message);
  }
}

// Self-service creation: the creator becomes the organization's first Org Admin immediately.
export async function registerTenant(
  name: string,
  slug: string,
  legalName: string | null = null
): Promise<RegisterTenantResponse200> {
  const trimmedLegalName = legalName?.trim() ?? '';
  const result = await client.registerTenant({
    body: { name, slug, ...(trimmedLegalName ? { legal_name: trimmedLegalName } : {}) },
  });
  if (!result.ok) throw tenantRequestFailure(result, describeFailure(result));

  return result.data;
}

export async function changeCurrentTenantSlug(slug: string): Promise<void> {
  const tenant = currentTenant();
  if (!tenant) {
    throw new TenantRequestError('No active organization is selected.', null);
  }

  const result = await client.changeTenantSlug({
    params: { tenant_id: tenant.tenantId },
    body: { slug },
  });
  if (!result.ok) {
    throw tenantRequestFailure(result, 'Unable to change this organization address.');
  }
}

// Waits for the event-sourced redirect to become authoritative before the initiating admin leaves
// the old route. This avoids navigating to a slug whose reservation is still being confirmed.
export async function waitForTenantSlugRedirect(
  previousSlug: string,
  expectedSlug: string,
  timeoutMs = 30_000
): Promise<boolean> {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    const result = await client.resolveMyTenantSlug({ params: { slug: previousSlug } });
    if (
      result.ok &&
      result.data?.redirect &&
      result.data.current_slug === expectedSlug
    ) {
      return true;
    }

    await new Promise((resolve) => window.setTimeout(resolve, 300));
  }

  return false;
}

function tenantRequestFailure(
  result: { ok: false; kind: string; status?: number; error?: unknown },
  fallback: string
) {
  const detail =
    result.kind === 'http' && typeof result.error === 'object' && result.error !== null
      ? (result.error as { detail?: unknown }).detail
      : undefined;
  return new TenantRequestError(
    typeof detail === 'string' && detail.length > 0 ? detail : fallback,
    result.kind === 'http' ? (result.status ?? null) : null
  );
}

export type EmailVerification = 'verified' | 'unverified' | 'unknown';

// Whether the signed-in user owns a verified email address, which self-service creation requires.
// 'unknown' means the check could not be made; the server still decides on creation.
export async function readEmailVerification(userId: string | null | undefined): Promise<EmailVerification> {
  if (!userId) return 'unknown';
  let cursor: string | undefined;
  try {
    do {
      const result = await client.listEmailAddresses({ params: { user_id: userId }, query: { cursor } });
      if (!result.ok) return 'unknown';
      if ((result.data?.items ?? []).some((item) => item?.verified)) return 'verified';
      cursor = result.data?.next_cursor ?? undefined;
    } while (cursor !== undefined);
  } catch {
    return 'unknown';
  }
  return 'unverified';
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

// Only the slug is remembered, as a routing convenience for '/'. It never selects the tenant_id an
// API request uses; the URL does.
export function readRememberedSlug(): string | null {
  try {
    const stored = window.localStorage.getItem(rememberedSlugKey);
    return stored && slugPattern.test(stored) ? stored : null;
  } catch {
    return null;
  }
}

function rememberSlug(slug: string): void {
  try {
    window.localStorage.setItem(rememberedSlugKey, slug);
  } catch {
    // Best-effort convenience only; nothing depends on this succeeding.
  }
}

// A different user signing in on the same browser must not silently inherit the previous user's
// organization, so this is cleared on sign-out.
export function clearActiveTenant(): void {
  activeTenant = null;
  try {
    window.localStorage.removeItem(rememberedSlugKey);
  } catch {
    // Best-effort convenience only; nothing depends on this succeeding.
  }
}

// Carries "this organization's address changed" across the redirect's document load.
export function recordSlugMove(previousSlug: string): void {
  try {
    window.sessionStorage.setItem(movedFromKey, previousSlug);
  } catch {
    // The notice is informational; the redirect itself does not depend on it.
  }
}

export function takeSlugMove(): string | null {
  try {
    const previous = window.sessionStorage.getItem(movedFromKey);
    window.sessionStorage.removeItem(movedFromKey);
    return previous;
  } catch {
    return null;
  }
}

function describeFailure(result: { ok: false; kind: string; status?: number }): string {
  return result.kind === 'http'
    ? `The request failed (${result.status ?? 'unknown status'}).`
    : 'The request could not be completed.';
}

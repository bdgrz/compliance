import type { AuthContext, Principal } from '@askrjs/auth';
import {
  createOidcClient,
  type OidcAuthorizationRequest,
} from '@askrjs/auth/oidc';

export interface AuthenticationConfiguration {
  enabled: boolean;
  issuer: string | null;
  client_id: string | null;
  scopes: string[];
  authorization_audience: string | null;
}

interface StoredAuthorizationRequest {
  request: Pick<OidcAuthorizationRequest, 'state' | 'nonce' | 'codeVerifier'>;
  returnPath: string;
}

interface StoredSession {
  accessToken: string;
  expiresAt: number | null;
  principal: Principal;
  scopes: string[];
}

const authorizationRequestKey = 'bdgrz.compliance.auth.request';
const authenticationErrorKey = 'bdgrz.compliance.auth.error';
const sessionKey = 'bdgrz.compliance.auth.session';
let configurationPromise: Promise<AuthenticationConfiguration> | undefined;

export function normalizeReturnPath(
  candidate: string | null,
  origin = 'https://compliance.invalid'
): string {
  if (!candidate) return '/';

  try {
    const target = new URL(candidate, origin);
    if (target.origin !== origin || target.pathname === '/auth/callback') {
      return '/';
    }

    return `${target.pathname}${target.search}${target.hash}`;
  } catch {
    return '/';
  }
}

export function parseAuthenticationConfiguration(
  value: unknown
): AuthenticationConfiguration {
  if (!value || typeof value !== 'object') {
    throw new Error('The authentication configuration response is invalid.');
  }

  const candidate = value as Record<string, unknown>;
  if (typeof candidate.enabled !== 'boolean') {
    throw new Error('The authentication configuration is missing enabled.');
  }

  if (!candidate.enabled) {
    return {
      enabled: false,
      issuer: null,
      client_id: null,
      scopes: [],
      authorization_audience: null,
    };
  }

  if (
    typeof candidate.issuer !== 'string' ||
    typeof candidate.client_id !== 'string' ||
    !Array.isArray(candidate.scopes) ||
    !candidate.scopes.every((scope) => typeof scope === 'string') ||
    (candidate.authorization_audience !== null &&
      typeof candidate.authorization_audience !== 'string')
  ) {
    throw new Error('The external authentication configuration is incomplete.');
  }

  return candidate as unknown as AuthenticationConfiguration;
}

export async function beginSignIn(returnPath?: string): Promise<void> {
  const configuration = await loadConfiguration();
  if (!configuration.enabled) {
    window.location.assign(normalizeReturnPath(returnPath ?? '/'));
    return;
  }

  const client = createClient(configuration);
  const request = await client.createAuthorizationRequest();
  const safeReturnPath = normalizeReturnPath(
    returnPath ?? '/',
    window.location.origin
  );
  const stored: StoredAuthorizationRequest = {
    request: {
      state: request.state,
      nonce: request.nonce,
      codeVerifier: request.codeVerifier,
    },
    returnPath: safeReturnPath,
  };
  window.sessionStorage.setItem(
    authorizationRequestKey,
    JSON.stringify(stored)
  );

  const authorizationUrl = new URL(request.url);
  if (configuration.authorization_audience) {
    authorizationUrl.searchParams.set(
      'audience',
      configuration.authorization_audience
    );
  }

  window.location.assign(authorizationUrl.toString());
}

export async function completeOidcCallbackIfPresent(): Promise<void> {
  if (window.location.pathname !== '/auth/callback') return;

  try {
    const parameters = new URLSearchParams(window.location.search);
    const providerError = parameters.get('error');
    if (providerError) {
      throw new Error(
        parameters.get('error_description') ??
          `The identity provider returned ${providerError}.`
      );
    }

    const code = parameters.get('code');
    const state = parameters.get('state');
    const stored = readJson<StoredAuthorizationRequest>(
      authorizationRequestKey
    );
    window.sessionStorage.removeItem(authorizationRequestKey);

    if (!code || !state || !stored) {
      throw new Error('The sign-in callback is missing its saved request.');
    }

    const configuration = await loadConfiguration();
    if (!configuration.enabled) {
      throw new Error('External authentication is not enabled.');
    }

    const result = await createClient(configuration).exchangeCode({
      code,
      state,
      request: stored.request,
    });
    const expiresAt = result.tokens.expires_in
      ? Date.now() + result.tokens.expires_in * 1000
      : null;
    const session: StoredSession = {
      accessToken: result.tokens.access_token,
      expiresAt,
      principal: result.principal,
      scopes: configuration.scopes,
    };
    window.sessionStorage.setItem(sessionKey, JSON.stringify(session));
    window.sessionStorage.removeItem(authenticationErrorKey);
    window.history.replaceState(
      null,
      '',
      normalizeReturnPath(stored.returnPath, window.location.origin)
    );
  } catch (error) {
    window.sessionStorage.removeItem(sessionKey);
    window.sessionStorage.setItem(
      authenticationErrorKey,
      error instanceof Error ? error.message : 'Sign-in failed.'
    );
    window.history.replaceState(null, '', '/login');
  }
}

export async function resolveBrowserAuthentication(): Promise<AuthContext> {
  const configuration = await loadConfiguration();
  if (!configuration.enabled) {
    return {
      authenticated: true,
      principal: { id: 'development', subject: 'development' },
      session: null,
      tenant: null,
      scopes: [],
    };
  }

  const session = readJson<StoredSession>(sessionKey);
  if (
    !session ||
    (session.expiresAt !== null && session.expiresAt <= Date.now())
  ) {
    window.sessionStorage.removeItem(sessionKey);
    return anonymousContext();
  }

  return {
    authenticated: true,
    principal: session.principal,
    session: null,
    tenant: null,
    scopes: session.scopes,
  };
}

export async function authorizedFetch(
  input: RequestInfo | URL,
  init: RequestInit = {}
): Promise<Response> {
  const session = readJson<StoredSession>(sessionKey);
  const headers = new Headers(init.headers);
  if (session?.accessToken) {
    headers.set('Authorization', `Bearer ${session.accessToken}`);
  }

  return fetch(input, { ...init, headers });
}

export function signOut(): void {
  window.sessionStorage.removeItem(sessionKey);
  window.location.assign('/login');
}

export function readAuthenticationError(): string | null {
  const error = window.sessionStorage.getItem(authenticationErrorKey);
  window.sessionStorage.removeItem(authenticationErrorKey);
  return error;
}

async function loadConfiguration(): Promise<AuthenticationConfiguration> {
  configurationPromise ??= fetch('/auth/config', {
    headers: { Accept: 'application/json' },
  }).then(async (response) => {
    if (!response.ok) {
      throw new Error('Authentication configuration is unavailable.');
    }

    return parseAuthenticationConfiguration(await response.json());
  });
  return configurationPromise;
}

function createClient(configuration: AuthenticationConfiguration) {
  if (!configuration.issuer || !configuration.client_id) {
    throw new Error('External authentication configuration is incomplete.');
  }

  return createOidcClient({
    issuer: configuration.issuer,
    clientId: configuration.client_id,
    redirectUri: `${window.location.origin}/auth/callback`,
    scopes: configuration.scopes,
  });
}

function readJson<T>(key: string): T | null {
  const value = window.sessionStorage.getItem(key);
  if (!value) return null;

  try {
    return JSON.parse(value) as T;
  } catch {
    window.sessionStorage.removeItem(key);
    return null;
  }
}

function anonymousContext(): AuthContext {
  return {
    authenticated: false,
    principal: null,
    session: null,
    tenant: null,
    scopes: [],
  };
}

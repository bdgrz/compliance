import { vi } from 'vitest';

type Reply = { status: number; body?: unknown };

const NativeRequest = globalThis.Request;
// The typed client builds requests from same-origin paths, which browsers resolve against the
// page and Node's Request does not; resolve them against a test origin.
class SameOriginRequest extends NativeRequest {
  constructor(input: RequestInfo | URL, init?: RequestInit) {
    super(typeof input === 'string' ? new URL(input, 'http://app.test') : input, init);
  }
}

// Stubs the browser API for the typed client: replies by path, 404 otherwise, and records every
// requested path so tests can assert what reached the server.
export function stubApi() {
  const replies = new Map<string, Reply>();
  const requested: string[] = [];
  vi.stubGlobal('Request', SameOriginRequest);
  vi.stubGlobal('fetch', async (input: RequestInfo | URL) => {
    const url = new URL(
      typeof input === 'string' || input instanceof URL ? input : input.url,
      'http://app.test'
    );
    requested.push(url.pathname);
    const match = replies.get(url.pathname) ?? { status: 404 };
    return new Response(match.body === undefined ? null : JSON.stringify(match.body), {
      status: match.status,
      headers: {
        'Content-Type': match.status < 300 ? 'application/json' : 'application/problem+json',
      },
    });
  });

  return {
    requested,
    reply(path: string, status: number, body?: unknown) {
      replies.set(path, { status, body });
    },
  };
}

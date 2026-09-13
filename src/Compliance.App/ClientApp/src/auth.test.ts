import { describe, expect, it } from 'vitest';

import {
  normalizeReturnPath,
  parseAuthenticationConfiguration,
} from './auth.js';

describe('authentication foundation', () => {
  it('accepts same-origin return paths', () => {
    expect(
      normalizeReturnPath(
        '/controls?state=open#current',
        'https://example.test'
      )
    ).toBe('/controls?state=open#current');
  });

  it('rejects external and callback return paths', () => {
    expect(
      normalizeReturnPath('https://attacker.test/', 'https://example.test')
    ).toBe('/');
    expect(
      normalizeReturnPath('/auth/callback?code=secret', 'https://example.test')
    ).toBe('/');
  });

  it('rejects incomplete external provider configuration', () => {
    expect(() =>
      parseAuthenticationConfiguration({ enabled: true, scopes: [] })
    ).toThrow(/incomplete/);
  });
});

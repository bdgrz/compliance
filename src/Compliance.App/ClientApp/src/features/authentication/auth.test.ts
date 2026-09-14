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
      parseAuthenticationConfiguration({
        enabled: true,
        developer_registration_enabled: false,
        scopes: [],
      })
    ).toThrow(/incomplete/);
  });

  it('accepts explicit development registration configuration', () => {
    expect(
      parseAuthenticationConfiguration({
        enabled: false,
        developer_registration_enabled: true,
      })
    ).toEqual({
      enabled: false,
      developer_registration_enabled: true,
      issuer: null,
      client_id: null,
      scopes: [],
      authorization_audience: null,
    });
  });
});

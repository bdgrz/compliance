// @vitest-environment jsdom
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';

import { render, type RenderResult } from '@askrjs/askr/testing';
import { afterEach, describe, expect, it } from 'vitest';

import { LoginPage } from '../features/authentication/pages/login.js';
import { NotFoundPage } from '../pages/not-found.js';
import {
  accessibilityViolations,
  documentAccessibilityViolations,
} from './axe.js';

const mounted: RenderResult[] = [];

function mount(component: () => unknown): HTMLElement {
  const result = render(component as never);
  mounted.push(result);
  return result.container;
}

afterEach(() => {
  while (mounted.length > 0) {
    mounted.pop()?.cleanup();
  }
});

describe('WCAG 2.2 AA automated baseline (M0-D24)', () => {
  it('reports an injected violation', async () => {
    const container = mount(() => (
      <main>
        <h1>Fixture</h1>
        <img src="/fixture.png" />
        <button type="button"></button>
      </main>
    ));

    const violations = await accessibilityViolations(container);

    expect(violations.map((violation) => violation.id)).toEqual(
      expect.arrayContaining(['image-alt', 'button-name'])
    );
  });

  it('finds no violations on the not-found page', async () => {
    const container = mount(() => <NotFoundPage />);

    expect(container.querySelector('h1')?.textContent).toBe('Page not found');
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('finds no violations on the sign-in page', async () => {
    const container = mount(() => <LoginPage />);

    expect(container.textContent).toContain('Continue to sign in');
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('reports document-level violations in an injected shell', async () => {
    const violations = await documentAccessibilityViolations(
      '<!doctype html><html><head><meta name="viewport" ' +
        'content="width=device-width, user-scalable=no" /></head>' +
        '<body><main></main></body></html>'
    );

    expect(violations.map((violation) => violation.id)).toEqual(
      expect.arrayContaining([
        'html-has-lang',
        'document-title',
        'meta-viewport',
      ])
    );
  });

  it('finds no document-level violations in the application shell', async () => {
    const shell = readFileSync(
      resolve(import.meta.dirname, '../../index.html'),
      'utf8'
    );

    expect(await documentAccessibilityViolations(shell)).toEqual([]);
  });
});

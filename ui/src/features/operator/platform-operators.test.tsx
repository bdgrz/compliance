// @vitest-environment jsdom
import { render, type RenderResult } from '@askrjs/askr/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { accessibilityViolations } from '../../accessibility/axe.js';
import { stubApi } from '../../test-support/api-stub.js';
import { PlatformOperatorsCard } from './pages/platform-operators-card.js';

const operators = '/api/v1/platform/operators';
const ownerId = '0190a1b2-0000-7000-8000-0000000000a1';
const newId = '0190a1b2-0000-7000-8000-0000000000a2';
let api: ReturnType<typeof stubApi>;
const mounted: RenderResult[] = [];

function mount(component: () => unknown): HTMLElement {
  const result = render(component as never);
  mounted.push(result);
  return result.container;
}

function setField(container: Element, label: string, value: string) {
  const input = [...container.querySelectorAll('label')].find((l) => l.textContent?.startsWith(label))!.querySelector(
    'input'
  ) as HTMLInputElement;
  input.value = value;
  input.dispatchEvent(new Event('input', { bubbles: true }));
}

function submit(container: Element) {
  container.querySelector('form')!.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
}

beforeEach(() => {
  api = stubApi();
});

afterEach(() => {
  vi.unstubAllGlobals();
  while (mounted.length > 0) mounted.pop()?.cleanup();
});

describe('platform operator grants (R1-15 frontend #176)', () => {
  it('ShouldGrantOperatorStatusWithAReason', async () => {
    // Arrange
    api.reply(`GET ${operators}`, 200, { user_ids: [ownerId] });
    api.reply('POST /api/v1/platform/operator-grants', 204);
    const container = mount(PlatformOperatorsCard);
    await vi.waitFor(() => expect(container.textContent).toContain(ownerId));
    setField(container, 'Reason', 'On-call rotation');
    setField(container, 'User ID to grant', ` ${newId} `);

    // Act
    submit(container);
    await vi.waitFor(() => expect(container.querySelector('[role="status"]')).not.toBeNull());

    // Assert
    expect(api.bodies.find((b) => b.path === '/api/v1/platform/operator-grants')?.body).toEqual({
      user_id: newId,
      reason: 'On-call rotation',
    });
    expect(api.requested.filter((p) => p === operators).length).toBeGreaterThanOrEqual(2);
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldRequireAReasonAndAUserId', async () => {
    // Arrange
    api.reply(`GET ${operators}`, 200, { user_ids: [ownerId] });
    const container = mount(PlatformOperatorsCard);
    await vi.waitFor(() => expect(container.textContent).toContain(ownerId));
    setField(container, 'User ID to grant', 'not-a-user');
    setField(container, 'Reason', 'x');

    // Act
    submit(container);
    await vi.waitFor(() => expect(container.querySelector('[role="alert"]')).not.toBeNull());

    // Assert
    expect(container.querySelector('[role="alert"]')?.textContent).toContain('user ID');
    expect(api.bodies.some((b) => b.method === 'POST')).toBe(false);
  });

  it('ShouldShowTheServerReasonGivenTheLastOperatorCannotBeRevoked', async () => {
    // Arrange
    api.reply(`GET ${operators}`, 200, { user_ids: [ownerId] });
    api.reply('POST /api/v1/platform/operator-revocations', 409, {
      type: 'about:blank', title: 'Conflict', status: 409, detail: 'The last platform operator cannot be revoked.', instance: '/',
    });
    const container = mount(PlatformOperatorsCard);
    await vi.waitFor(() => expect(container.textContent).toContain(ownerId));
    setField(container, 'Reason', 'Leaving');

    // Act
    (container.querySelector(`button[aria-label="Revoke operator status from ${ownerId}"]`) as HTMLButtonElement).click();
    await vi.waitFor(() => expect(container.querySelector('[role="alert"]')).not.toBeNull());

    // Assert
    expect(container.querySelector('[role="alert"]')?.textContent).toBe('The last platform operator cannot be revoked.');
    expect(api.bodies.find((b) => b.path === '/api/v1/platform/operator-revocations')?.body).toEqual({
      user_id: ownerId,
      reason: 'Leaving',
    });
  });

  it('ShouldExplainForbiddenAndOfferRetry', async () => {
    // Arrange
    api.reply(`GET ${operators}`, 403, { type: 'about:blank', title: 'Forbidden', status: 403, detail: 'No.', instance: '/' });

    // Act
    const container = mount(PlatformOperatorsCard);
    await vi.waitFor(() => expect(container.querySelector('[role="alert"]')).not.toBeNull());

    // Assert
    expect(container.querySelector('[role="alert"]')?.textContent).toContain('Only platform operators');
    expect([...container.querySelectorAll('button')].some((b) => b.textContent === 'Try again')).toBe(true);
  });
});

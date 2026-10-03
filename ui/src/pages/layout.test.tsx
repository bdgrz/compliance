// @vitest-environment jsdom
import { render } from '@askrjs/askr/testing';
import { expect, it, vi } from 'vitest';

import { accessibilityViolations } from '../accessibility/axe.js';
import { PageLayout } from './_layout.js';

vi.mock('@askrjs/askr/router', async (importOriginal) => ({
  ...(await importOriginal<object>()),
  currentRoute: () => ({ path: '/organizations' }),
  currentAuth: () => ({ authenticated: true }),
}));

it('ShouldNameSignOutGivenHiddenNarrowViewportText', async () => {
  // Arrange
  const result = render(() => (
    <PageLayout>
      <main>Choose an organization</main>
    </PageLayout>
  ));
  try {
    const button = [...result.container.querySelectorAll('button')].find((b) =>
      b.textContent?.includes('Sign out')
    )!;
    const text = button.querySelector('span')!;

    // Act: reproduce the narrow viewport's hidden label without relying on jsdom media queries.
    text.style.display = 'none';

    // Assert
    expect(await accessibilityViolations(button)).toEqual([]);
  } finally {
    result.cleanup();
  }
});

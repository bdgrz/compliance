import axe from 'axe-core';

/** WCAG 2.2 AA rule tags selected by M0-D24. */
export const wcag22AaTags = [
  'wcag2a',
  'wcag2aa',
  'wcag21a',
  'wcag21aa',
  'wcag22aa',
];

/**
 * Rules that need real layout and rendering, which jsdom cannot provide.
 * The per-story manual assistive-technology pass covers them.
 */
const layoutDependentRules = { 'color-contrast': { enabled: false } };

export interface AccessibilityViolation {
  id: string;
  impact: string | null;
  help: string;
  targets: string[];
}

/** Run the automated WCAG 2.2 AA baseline against a rendered element. */
export async function accessibilityViolations(
  element: Element
): Promise<AccessibilityViolation[]> {
  const results = await axe.run(element, {
    runOnly: { type: 'tag', values: wcag22AaTags },
    rules: layoutDependentRules,
    resultTypes: ['violations'],
  });

  return results.violations.map((violation) => ({
    id: violation.id,
    impact: violation.impact ?? null,
    help: violation.help,
    targets: violation.nodes.map((node) => node.target.join(' ')),
  }));
}

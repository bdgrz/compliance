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
 * The per-story manual pass covers them.
 */
const layoutDependentRules = {
  'color-contrast': { enabled: false },
  'target-size': { enabled: false },
};

/** Rules whose subject is the document itself rather than a rendered page. */
const documentRules = [
  'html-has-lang',
  'html-lang-valid',
  'document-title',
  'meta-viewport',
];

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

  return toViolations(results);
}

/**
 * Run the document-level rules (language, title, viewport) against an HTML
 * shell. The shell temporarily replaces the test document.
 */
export async function documentAccessibilityViolations(
  html: string
): Promise<AccessibilityViolation[]> {
  const original = document.documentElement.outerHTML;
  document.open();
  document.write(html);
  document.close();
  try {
    const results = await axe.run(document, {
      runOnly: { type: 'rule', values: documentRules },
      resultTypes: ['violations'],
    });
    return toViolations(results);
  } finally {
    document.open();
    document.write(original);
    document.close();
  }
}

function toViolations(results: axe.AxeResults): AccessibilityViolation[] {
  return results.violations.map((violation) => ({
    id: violation.id,
    impact: violation.impact ?? null,
    help: violation.help,
    targets: violation.nodes.map((node) => node.target.join(' ')),
  }));
}

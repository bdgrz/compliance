import { codeLabel, type ChangePreview } from '../applications.js';

const fieldLabels: Record<string, string> = {
  name: 'Name',
  purpose: 'Purpose',
  owner_reference: 'Owner',
  classification: 'Classification',
};

// Advisory impact of a revision or retirement. The server never reports this as complete, so the
// pending contexts stay visible rather than implying the change is safe.
export function ChangePreviewView({ preview }: { preview: ChangePreview }) {
  return (
    <section className="change-preview" aria-label="Change preview">
      <h3>{preview.changeKind === 'retire' ? 'Retirement impact' : 'Change impact'}</h3>
      {preview.changeKind === 'revise' ? (
        preview.changes.length === 0 ? (
          <p>No content changes compared with revision {preview.applicationRevision}.</p>
        ) : (
          <ul className="plain-list">
            {preview.changes.map((change) => (
              <li>
                <strong>{fieldLabels[change.field] ?? codeLabel(change.field)}</strong>: {change.before ?? 'not set'} →{' '}
                {change.after ?? 'not set'}
              </li>
            ))}
          </ul>
        )
      ) : null}
      <h4>Affected work</h4>
      {preview.boundaryReferences.length +
        preview.controlDraftReferences.length +
        preview.systemInstanceReferences.length ===
      0 ? (
        <p>No boundaries, control drafts, or system instances reference this application.</p>
      ) : (
        <ul className="plain-list">
          {preview.boundaryReferences.map((reference) => (
            <li>
              Boundary entry ({reference.status}): {reference.subject}. {reference.rationale}
            </li>
          ))}
          {preview.controlDraftReferences.map((reference) => (
            <li>
              Control draft {reference.identifier}: {reference.subject}. {reference.rationale}
            </li>
          ))}
          {preview.systemInstanceReferences.map((reference) => (
            <li>
              System instance {reference.name} ({reference.kind})
            </li>
          ))}
        </ul>
      )}
      {preview.complete ? null : (
        <p className="change-preview-incomplete">
          This preview is not complete. Impact on these areas is not yet checked:{' '}
          {preview.pendingContexts.map(codeLabel).join(', ') || 'other areas'}.
        </p>
      )}
    </section>
  );
}

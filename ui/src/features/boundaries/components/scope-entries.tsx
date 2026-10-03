import { label, type BoundaryContent } from '../boundaries.js';

// Read-only view of a boundary version's scope; governed links and unresolved references are
// labelled distinctly so reviewers can tell them apart without relying on colour.
export function ScopeEntries({ content }: { content: BoundaryContent }) {
  return (
    <div className="boundary-scope">
      <p>{content.statement}</p>
      <p className="boundary-meta">
        {label(content.engagementStage)} · categories: {content.categories.map(label).join(', ')}
      </p>
      {content.entries.length === 0 ? (
        <p>No scope entries.</p>
      ) : (
        <ul className="plain-list boundary-entries">
          {content.entries.map((entry) => (
            <li>
              <strong>{label(entry.kind)}</strong> · {label(entry.subject_type)}: {entry.subject}{' '}
              <span className={entry.unresolved ? 'boundary-tag boundary-tag-unresolved' : 'boundary-tag'}>
                {entry.unresolved ? 'Unresolved reference' : 'Governed inventory link'}
              </span>
              <br />
              <span className="boundary-meta">
                Owner {entry.owner_reference} · {entry.rationale}
              </span>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

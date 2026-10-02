import { organizationPath } from '../../tenants/tenants.js';
import { optionLabel, unresolvedLabel, type Provider, type ProviderDependency, type SourceCitation } from '../providers.js';

export const formatDate = (instant: string) => new Date(instant).toLocaleDateString();

export function UnresolvedList({ label, codes }: { label: string; codes: string[] }) {
  if (codes.length === 0) return null;
  return (
    <ul className="application-unresolved" aria-label={label}>
      {codes.map((code) => (
        <li>{unresolvedLabel(code)}</li>
      ))}
    </ul>
  );
}

// Shows citation metadata only. The locator is plain text and a governed artifact is a bare
// reference: the register never links to, fetches, or reveals the cited content.
export function CitationView({ citation }: { citation: SourceCitation }) {
  return (
    <dl className="application-facts provider-citation">
      <dt>Source</dt>
      <dd>{citation.title}</dd>
      <dt>Kind</dt>
      <dd>{citation.artifactKind}</dd>
      <dt>Version or date</dt>
      <dd>{citation.versionOrDate}</dd>
      <dt>Locator</dt>
      <dd>{citation.locator}</dd>
      <dt>Metadata classification</dt>
      <dd>{optionLabel(citation.metadataClassification)}</dd>
      {citation.artifactId ? (
        <>
          <dt>Governed artifact</dt>
          <dd>Governed artifact reference</dd>
        </>
      ) : null}
    </dl>
  );
}

function DependencyView({ dependency, applicationNames }: { dependency: ProviderDependency; applicationNames: Map<string, string> }) {
  const interval = dependency.effectiveUntilExclusive
    ? `${formatDate(dependency.effectiveFrom)} until before ${formatDate(dependency.effectiveUntilExclusive)}`
    : `from ${formatDate(dependency.effectiveFrom)}`;
  return (
    <li>
      <strong>{optionLabel(dependency.subjectKind)}</strong>
      {': '}
      {dependency.subjectId === null ? (
        <span>{dependency.unresolvedReference ?? 'Subject unresolved'} (unresolved)</span>
      ) : dependency.subjectKind === 'system_instance' && dependency.applicationId ? (
        <a href={organizationPath(`/applications/${dependency.applicationId}`)}>
          {`System instance in ${applicationNames.get(dependency.applicationId) ?? 'its application'}`}
        </a>
      ) : dependency.programId ? (
        <a href={organizationPath(`/programs/${dependency.programId}`)}>Client service in its program</a>
      ) : (
        <span>Registered {optionLabel(dependency.subjectKind).toLowerCase()}</span>
      )}
      {` · ${interval} · ${dependency.rationale}`}
      {dependency.sourceCitation ? ` · Source: ${dependency.sourceCitation.title} (${optionLabel(dependency.sourceCitation.metadataClassification)})` : ''}
    </li>
  );
}

export function DependencyList({ dependencies, applicationNames }: { dependencies: ProviderDependency[]; applicationNames: Map<string, string> }) {
  if (dependencies.length === 0) return <p>No dependencies declared.</p>;
  return (
    <ul className="plain-list provider-dependencies-list">
      {dependencies.map((dependency) => (
        <DependencyView dependency={dependency} applicationNames={applicationNames} />
      ))}
    </ul>
  );
}

export function ProviderFacts({ provider, ownerName }: { provider: Provider; ownerName: string | null }) {
  const facts = provider.content;
  return (
    <dl className="application-facts">
      <dt>Kind</dt>
      <dd>{facts.providerKind}</dd>
      <dt>Materiality</dt>
      <dd>{facts.materiality ? optionLabel(facts.materiality) : 'Materiality unresolved'}</dd>
      <dt>Materiality basis</dt>
      <dd>{facts.materialityBasis.length > 0 ? facts.materialityBasis.map(optionLabel).join(', ') : 'None recorded'}</dd>
      <dt>Materiality rationale</dt>
      <dd>{facts.materialityRationale ?? 'Unresolved'}</dd>
      <dt>Subservice treatment</dt>
      <dd>
        {facts.subservice && facts.boundaryTreatment
          ? `Subservice organization, ${optionLabel(facts.boundaryTreatment)}`
          : 'Not a subservice organization'}
      </dd>
      {facts.subservice && facts.boundaryTreatmentRationale ? (
        <>
          <dt>Treatment rationale</dt>
          <dd>{facts.boundaryTreatmentRationale}</dd>
        </>
      ) : null}
      <dt>Accountable owner</dt>
      <dd>{facts.ownerPersonId ? ownerName ?? 'Person not on the roster' : 'Owner unresolved'}</dd>
      {facts.ownerReference ? (
        <>
          <dt>Owner reference</dt>
          <dd>{facts.ownerReference}</dd>
        </>
      ) : null}
    </dl>
  );
}

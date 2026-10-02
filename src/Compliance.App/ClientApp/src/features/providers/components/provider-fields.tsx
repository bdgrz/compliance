import { resource } from '@askrjs/askr/resources';
import { Button, Stack } from '@askrjs/themes/components';

import { listApplications, listSystemInstances } from '../../applications/applications.js';
import { listPeople } from '../../workforce/workforce.js';
import {
  listClientServiceChoices,
  type BoundaryTreatment,
  type DependencyKind,
  type MaterialityBasis,
  type MetadataClassification,
  type ProviderContent,
  type ProviderDependency,
  type SourceCitation,
} from '../providers.js';

export const emptyContent: ProviderContent = {
  name: '',
  providerKind: '',
  materiality: null,
  materialityBasis: [],
  materialityRationale: null,
  subservice: false,
  boundaryTreatment: null,
  boundaryTreatmentRationale: null,
  ownerPersonId: null,
  ownerReference: null,
  dependencies: [],
  sourceCitation: null,
};

function blankToNull(value: string | null): string | null {
  return value !== null && value.trim() !== '' ? value.trim() : null;
}

function cleanCitation(citation: SourceCitation | null): SourceCitation | null {
  if (!citation) return null;
  const cleaned = {
    ...citation,
    artifactKind: citation.artifactKind.trim(),
    title: citation.title.trim(),
    versionOrDate: citation.versionOrDate.trim(),
    locator: citation.locator.trim(),
    artifactId: blankToNull(citation.artifactId),
  };
  // A citation with nothing typed is an omitted citation, which stays visibly unresolved.
  return cleaned.artifactKind === '' && cleaned.title === '' && cleaned.versionOrDate === '' && cleaned.locator === '' && cleaned.artifactId === null
    ? null
    : cleaned;
}

export function cleanContent(content: ProviderContent): ProviderContent {
  return {
    ...content,
    name: content.name.trim(),
    providerKind: content.providerKind.trim(),
    materialityRationale: blankToNull(content.materialityRationale),
    boundaryTreatment: content.subservice ? (content.boundaryTreatment ?? 'carve_out') : null,
    boundaryTreatmentRationale: content.subservice ? blankToNull(content.boundaryTreatmentRationale) : null,
    ownerReference: blankToNull(content.ownerReference),
    sourceCitation: cleanCitation(content.sourceCitation),
    dependencies: content.dependencies.map((dependency) => ({
      ...dependency,
      rationale: dependency.rationale.trim(),
      unresolvedReference: dependency.subjectId ? null : blankToNull(dependency.unresolvedReference),
      effectiveUntilExclusive: blankToNull(dependency.effectiveUntilExclusive),
      sourceCitation: cleanCitation(dependency.sourceCitation),
    })),
  };
}

function inputValue(event: Event): string {
  return (event.target as HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement).value;
}

// Date inputs give a calendar day; the API takes an instant, so use the start of that UTC day.
const dayOf = (instant: string | null) => (instant ? instant.slice(0, 10) : '');
const startOfDay = (day: string) => (day === '' ? null : `${day}T00:00:00Z`);

const blankCitation: SourceCitation = {
  artifactKind: '',
  title: '',
  versionOrDate: '',
  locator: '',
  metadataClassification: 'public',
  artifactId: null,
};

function CitationFields({
  prefix,
  citation,
  onChange,
}: {
  prefix: string;
  citation: SourceCitation | null;
  onChange: (next: SourceCitation) => void;
}) {
  const current = citation ?? blankCitation;
  const text = (label: string, key: 'artifactKind' | 'title' | 'versionOrDate' | 'locator', hint?: string) => (
    <label className="registration-field">
      <span>{`${prefix}${label}`}</span>
      <input type="text" value={current[key]} onInput={(event: Event) => onChange({ ...current, [key]: inputValue(event) })} />
      {hint ? <small>{hint}</small> : null}
    </label>
  );
  return (
    <>
      {text('kind', 'artifactKind', 'For example soc2_report or contract.')}
      {text('title', 'title')}
      {text('version or date', 'versionOrDate')}
      {text('locator', 'locator', 'Where the source lives. It is recorded, never fetched.')}
      <label className="registration-field">
        <span>{`${prefix}metadata classification`}</span>
        <select
          value={current.metadataClassification}
          onChange={(event: Event) => onChange({ ...current, metadataClassification: inputValue(event) as MetadataClassification })}
        >
          <option value="public">Public</option>
          <option value="internal">Internal</option>
        </select>
        <small>Classifies this citation metadata. Do not enter confidential or restricted source content.</small>
      </label>
    </>
  );
}

function SystemInstanceChoice({ dependency, onChange }: { dependency: ProviderDependency; onChange: (next: ProviderDependency) => void }) {
  const applications = resource(() => listApplications().catch(() => null), []);
  const applicationId = dependency.applicationId ?? '';
  const instances = resource(
    () => (applicationId === '' ? Promise.resolve([]) : listSystemInstances(applicationId).catch(() => null)),
    [applicationId]
  );
  const failed = applications.value === null || instances.value === null;
  return (
    <>
      <label className="registration-field">
        <span>Application</span>
        <select
          value={applicationId}
          onChange={(event: Event) => onChange({ ...dependency, applicationId: inputValue(event) || null, subjectId: null, programId: null })}
        >
          <option value="">Not yet identified</option>
          {(applications.value ?? []).map((application) => (
            <option value={application.applicationId}>{application.name}</option>
          ))}
        </select>
      </label>
      {applicationId === '' ? null : (
        <label className="registration-field">
          <span>System instance</span>
          <select value={dependency.subjectId ?? ''} onChange={(event: Event) => onChange({ ...dependency, subjectId: inputValue(event) || null })}>
            <option value="">Not yet identified</option>
            {(instances.value ?? []).map((instance) => (
              <option value={instance.systemInstanceId}>{instance.name}</option>
            ))}
          </select>
        </label>
      )}
      {failed ? <small>Applications or systems could not be loaded; use an unresolved reference for now.</small> : null}
    </>
  );
}

function ClientServiceChoice({ dependency, onChange }: { dependency: ProviderDependency; onChange: (next: ProviderDependency) => void }) {
  const services = resource(() => listClientServiceChoices().catch(() => null), []);
  return (
    <label className="registration-field">
      <span>Client service</span>
      <select
        value={dependency.subjectId ?? ''}
        onChange={(event: Event) => {
          const chosen = (services.value ?? []).find((service) => service.serviceId === inputValue(event));
          onChange({ ...dependency, subjectId: chosen?.serviceId ?? null, programId: chosen?.programId ?? null });
        }}
      >
        <option value="">Not yet identified</option>
        {(services.value ?? []).map((service) => (
          <option value={service.serviceId}>{service.name}</option>
        ))}
      </select>
      {services.value === null ? <small>Client services could not be loaded; use an unresolved reference for now.</small> : null}
    </label>
  );
}

function DependencyRow({
  index,
  dependency,
  onChange,
  onRemove,
}: {
  index: number;
  dependency: ProviderDependency;
  onChange: (next: ProviderDependency) => void;
  onRemove: () => void;
}) {
  return (
    <fieldset className="provider-dependency-row">
      <legend>Dependency {index + 1}</legend>
      <Stack gap="sm">
        <label className="registration-field">
          <span>Dependency kind</span>
          <select
            value={dependency.subjectKind}
            onChange={(event: Event) =>
              onChange({ ...dependency, subjectKind: inputValue(event) as DependencyKind, subjectId: null, programId: null, applicationId: null })
            }
          >
            <option value="client_service">Client service</option>
            <option value="system_instance">System instance</option>
          </select>
        </label>
        {dependency.subjectKind === 'client_service' ? (
          <ClientServiceChoice dependency={dependency} onChange={onChange} />
        ) : (
          <SystemInstanceChoice dependency={dependency} onChange={onChange} />
        )}
        {dependency.subjectId ? null : (
          <label className="registration-field">
            <span>Unresolved reference</span>
            <input
              type="text"
              value={dependency.unresolvedReference ?? ''}
              required
              onInput={(event: Event) => onChange({ ...dependency, unresolvedReference: inputValue(event) })}
            />
            <small>Describe the dependency until it can be tied to a registered record.</small>
          </label>
        )}
        <label className="registration-field">
          <span>Dependency rationale</span>
          <textarea value={dependency.rationale} required onInput={(event: Event) => onChange({ ...dependency, rationale: inputValue(event) })} />
        </label>
        <label className="registration-field">
          <span>Effective from</span>
          <input
            type="date"
            value={dayOf(dependency.effectiveFrom)}
            required
            onInput={(event: Event) => onChange({ ...dependency, effectiveFrom: startOfDay(inputValue(event)) ?? dependency.effectiveFrom })}
          />
        </label>
        <label className="registration-field">
          <span>Effective until (exclusive, optional)</span>
          <input
            type="date"
            value={dayOf(dependency.effectiveUntilExclusive)}
            onInput={(event: Event) => onChange({ ...dependency, effectiveUntilExclusive: startOfDay(inputValue(event)) })}
          />
        </label>
        <details>
          <summary>Dependency source citation (optional)</summary>
          <Stack gap="sm">
            <CitationFields prefix="Dependency citation " citation={dependency.sourceCitation} onChange={(next) => onChange({ ...dependency, sourceCitation: next })} />
          </Stack>
        </details>
        <Button variant="secondary" onPress={onRemove}>
          {`Remove dependency ${index + 1}`}
        </Button>
      </Stack>
    </fieldset>
  );
}

function OwnerPicker({ value, onChange }: { value: string | null; onChange: (next: string | null) => void }) {
  const people = resource(() => listPeople(), []);
  const roster = [...(people.value ?? [])].sort((a, b) => a.displayName.localeCompare(b.displayName));
  const known = value === null || roster.some((person) => person.personId === value);
  const failed = people.error !== null && people.error !== undefined;
  return (
    <label className="registration-field">
      <span>Accountable owner</span>
      <select value={value ?? ''} onChange={(event: Event) => onChange(inputValue(event) || null)}>
        <option value="">Not assigned</option>
        {known ? null : <option value={value!}>Person not on the roster</option>}
        {roster.map((person) => (
          <option value={person.personId}>
            {person.displayName}
            {person.workEmail ? ` (${person.workEmail})` : ''}
          </option>
        ))}
      </select>
      <small>
        {failed
          ? 'The workforce roster could not be loaded; the owner cannot be changed right now.'
          : 'Choose a person on the workforce roster. They need no login. Leave unassigned to keep the owner unresolved.'}
      </small>
    </label>
  );
}

const exposures: { value: MaterialityBasis; label: string }[] = [
  { value: 'customer_data', label: 'Customer data exposure' },
  { value: 'critical_path', label: 'Critical path exposure' },
];

export function ProviderFields({ content, onChange }: { content: ProviderContent; onChange: (next: ProviderContent) => void }) {
  const set = (patch: Partial<ProviderContent>) => onChange({ ...content, ...patch });
  const inclusive = content.subservice && content.boundaryTreatment === 'inclusive';
  const toggleBasis = (basis: MaterialityBasis, checked: boolean) =>
    set({
      materialityBasis: checked ? [...content.materialityBasis.filter((b) => b !== basis), basis] : content.materialityBasis.filter((b) => b !== basis),
    });
  return (
    <>
      <label className="registration-field">
        <span>Provider name</span>
        <input type="text" value={content.name} required onInput={(event: Event) => set({ name: inputValue(event) })} />
        <small>Unique among active providers in this organization.</small>
      </label>
      <label className="registration-field">
        <span>Provider kind</span>
        <input type="text" value={content.providerKind} required onInput={(event: Event) => set({ providerKind: inputValue(event) })} />
        <small>For example hosting, payroll, or support tooling.</small>
      </label>
      <label className="registration-field">
        <span>Materiality</span>
        <select
          value={content.materiality ?? ''}
          onChange={(event: Event) => {
            const materiality = (inputValue(event) || null) as ProviderContent['materiality'];
            set({ materiality, materialityBasis: materiality === 'not_material' ? [] : content.materialityBasis });
          }}
        >
          <option value="">Unresolved</option>
          <option value="material">Material</option>
          <option value="not_material">Not material</option>
        </select>
      </label>
      {content.materiality === 'not_material' ? null : (
        <fieldset className="provider-exposures">
          <legend>Materiality basis</legend>
          {exposures.map((exposure) => (
            <label>
              <input
                type="checkbox"
                checked={content.materialityBasis.includes(exposure.value)}
                onChange={(event: Event) => toggleBasis(exposure.value, (event.target as HTMLInputElement).checked)}
              />
              <span>{exposure.label}</span>
            </label>
          ))}
          <small>Spend is not a basis. Either exposure makes a provider material.</small>
        </fieldset>
      )}
      <label className="registration-field">
        <span>Materiality rationale</span>
        <textarea value={content.materialityRationale ?? ''} onInput={(event: Event) => set({ materialityRationale: inputValue(event) })} />
      </label>
      <label className="provider-checkbox">
        <input
          type="checkbox"
          checked={content.subservice}
          onChange={(event: Event) => {
            const subservice = (event.target as HTMLInputElement).checked;
            set({ subservice, boundaryTreatment: subservice ? 'carve_out' : null, boundaryTreatmentRationale: subservice ? content.boundaryTreatmentRationale : null });
          }}
        />
        <span>Subservice organization</span>
      </label>
      {content.subservice ? (
        <>
          <label className="registration-field">
            <span>Boundary treatment</span>
            <select
              value={content.boundaryTreatment ?? 'carve_out'}
              onChange={(event: Event) => set({ boundaryTreatment: inputValue(event) as BoundaryTreatment })}
            >
              <option value="carve_out">Carve-out</option>
              <option value="inclusive">Inclusive</option>
            </select>
            <small>Carve-out is the default; its CSOCs stay unresolved until due diligence records them.</small>
          </label>
          <label className="registration-field">
            <span>Treatment rationale</span>
            <textarea
              value={content.boundaryTreatmentRationale ?? ''}
              required={inclusive}
              onInput={(event: Event) => set({ boundaryTreatmentRationale: inputValue(event) })}
            />
            {inclusive ? <small>Required for inclusive treatment.</small> : null}
          </label>
        </>
      ) : null}
      <OwnerPicker value={content.ownerPersonId} onChange={(next) => set({ ownerPersonId: next })} />
      <label className="registration-field">
        <span>Owner reference (source text)</span>
        <input type="text" value={content.ownerReference ?? ''} onInput={(event: Event) => set({ ownerReference: inputValue(event) })} />
        <small>Optional source text. It does not resolve the owner on its own.</small>
      </label>
      <details open={content.sourceCitation !== null}>
        <summary>Source citation</summary>
        <Stack gap="sm">
          <CitationFields prefix="Citation " citation={content.sourceCitation} onChange={(next) => set({ sourceCitation: next })} />
          <small>Optional. Leave blank to keep the source citation unresolved.</small>
        </Stack>
      </details>
      <fieldset className="provider-dependencies">
        <legend>Service and system dependencies</legend>
        <Stack gap="sm">
          {content.dependencies.length === 0 ? <p>No dependencies declared.</p> : null}
          {content.dependencies.map((dependency, index) => (
            <DependencyRow
              index={index}
              dependency={dependency}
              onChange={(next) => set({ dependencies: content.dependencies.map((existing, i) => (i === index ? next : existing)) })}
              onRemove={() => set({ dependencies: content.dependencies.filter((_, i) => i !== index) })}
            />
          ))}
          <Button
            variant="secondary"
            onPress={() =>
              set({
                dependencies: [
                  ...content.dependencies,
                  {
                    subjectKind: 'client_service',
                    subjectId: null,
                    programId: null,
                    applicationId: null,
                    rationale: '',
                    effectiveFrom: `${new Date().toISOString().slice(0, 10)}T00:00:00Z`,
                    effectiveUntilExclusive: null,
                    unresolvedReference: null,
                    sourceCitation: null,
                  },
                ],
              })
            }
          >
            Add dependency
          </Button>
        </Stack>
      </fieldset>
    </>
  );
}

import { state } from '@askrjs/askr';
import { resource } from '@askrjs/askr/resources';
import {
  Button,
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
  EmptyState,
  Page,
  PageHeader,
  Spinner,
  Stack,
} from '@askrjs/themes/components';

import { listApplications, listSystemInstances } from '../../applications/applications.js';
import {
  classifications,
  componentCategories,
  InventoryRequestError,
  lifecycles,
  listAssetRevisions,
  listAssets,
  listComponentRevisions,
  listComponents,
  listFlowRevisions,
  listFlows,
  listPeople,
  previewAssetChange,
  recordAsset,
  recordComponent,
  recordFlow,
  reviseAsset,
  reviseComponent,
  reviseFlow,
  vocabularyLabel,
  type AssetChangeImpact,
  type AssetContent,
  type ComponentContent,
  type DataFlow,
  type FlowContent,
  type InformationAsset,
  type Person,
  type TechnologyComponent,
} from '../inventory.js';

type Section = 'components' | 'assets' | 'flows';

const sections: { key: Section; label: string }[] = [
  { key: 'components', label: 'Technology components' },
  { key: 'assets', label: 'Information assets' },
  { key: 'flows', label: 'Data flows' },
];

function inputValue(event: Event) {
  return (event.target as HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement).value;
}

function optional(value: string | null) {
  return value && value.trim() !== '' ? value.trim() : null;
}

function formatDate(value: string) {
  return new Date(value).toLocaleDateString(undefined, { timeZone: 'UTC' });
}

function ActionError({ error, record }: { error: Error | null; record: string }) {
  if (!error) return null;
  const conflict = error instanceof InventoryRequestError && error.status === 409 && !error.transient;
  return (
    <p role="alert">
      {conflict
        ? `Someone else changed this ${record} since you opened it. Reload to see their changes, then edit again.`
        : error.message}
    </p>
  );
}

function useAction(onDone: () => void) {
  const [pending, setPending] = state(false);
  const [error, setError] = state<Error | null>(null);
  const [notice, setNotice] = state<string | null>(null);
  async function run(event: Event, work: () => Promise<unknown>, success: string) {
    event.preventDefault();
    setError(null);
    setNotice(null);
    setPending(true);
    try {
      await work();
      setNotice(success);
      onDone();
    } catch (failure) {
      setError(failure instanceof Error ? failure : new Error('The request failed.'));
    } finally {
      setPending(false);
    }
  }
  return { pending, error, notice, run };
}

function Field({ label, children }: { label: string; children?: unknown }) {
  return (
    <label className="registration-field">
      <span>{label}</span>
      {children}
    </label>
  );
}

function Choice({
  label,
  value,
  options,
  onChange,
}: {
  label: string;
  value: string;
  options: { value: string; label: string }[];
  onChange: (value: string) => void;
}) {
  return (
    <Field label={label}>
      <select value={value} required onChange={(event: Event) => onChange(inputValue(event))}>
        <option value="" selected={value === ''}>
          Choose…
        </option>
        {options.map((option) => (
          <option value={option.value} selected={value === option.value}>
            {option.label}
          </option>
        ))}
      </select>
    </Field>
  );
}

function Text({
  label,
  value,
  onInput,
  required = false,
  type = 'text',
}: {
  label: string;
  value: string | null;
  onInput: (value: string) => void;
  required?: boolean;
  type?: string;
}) {
  return (
    <Field label={label}>
      <input type={type} value={value ?? ''} required={required} onInput={(event: Event) => onInput(inputValue(event))} />
    </Field>
  );
}

function Submit({ pending, label }: { pending: boolean; label: string }) {
  return (
    <Button variant="primary" type="submit" disabled={pending}>
      {pending ? 'Saving…' : label}
    </Button>
  );
}

const vocabulary = (values: readonly string[]) => values.map((value) => ({ value, label: vocabularyLabel(value) }));

type InstanceOption = { value: string; label: string };

// Every active system instance across applications, labelled "Application — Instance", so a cloud
// account can be tied to the reviewed system it hosts.
async function loadInstanceOptions(): Promise<InstanceOption[]> {
  const applications = (await listApplications()).filter((a) => a.hasSystemInstances && a.lifecycle === 'active');
  const groups = await Promise.all(
    applications.map(async (a) =>
      (await listSystemInstances(a.applicationId))
        .filter((i) => i.lifecycle === 'active')
        .map((i) => ({ value: i.systemInstanceId, label: `${a.name} — ${i.name}` }))
    )
  );
  return groups.flat().sort((x, y) => x.label.localeCompare(y.label));
}

function SystemInstancePicker({ value, onChange }: { value: string | null; onChange: (value: string) => void }) {
  const options = resource(() => loadInstanceOptions(), []);
  if (options.pending && !options.value) return <Spinner label="Loading system instances" />;
  if (options.error) {
    return (
      <Stack gap="sm">
        <p role="alert">{options.error.message}</p>
        <Button variant="secondary" onPress={() => options.refresh()}>
          Try again
        </Button>
      </Stack>
    );
  }
  const list = options.value ?? [];
  if (list.length === 0) return <p>No system instances are declared yet. Declare one on an application first.</p>;
  return <Choice label="System instance" value={value ?? ''} options={list} onChange={onChange} />;
}

function ComponentForm({
  existing,
  people,
  onSaved,
}: {
  existing: TechnologyComponent | null;
  people: Person[];
  onSaved: () => void;
}) {
  const [content, setContent] = state<ComponentContent>(
    existing ?? {
      category: '',
      name: '',
      ownerPersonId: '',
      environmentReference: null,
      locationReference: null,
      systemInstanceId: null,
      endpointCount: null,
      managementSource: null,
      lifecycle: 'active',
    }
  );
  const action = useAction(onSaved);
  const set = (patch: Partial<ComponentContent>) => setContent({ ...content(), ...patch });
  const c = content();
  function save(event: Event) {
    const cleaned = {
      ...c,
      name: c.name.trim(),
      environmentReference: optional(c.environmentReference),
      locationReference: optional(c.locationReference),
      managementSource: optional(c.managementSource),
    };
    void action.run(
      event,
      () => (existing ? reviseComponent(existing.componentId, existing.revision, cleaned) : recordComponent(cleaned)),
      existing ? 'Component saved.' : 'Component recorded.'
    );
  }
  return (
    <form aria-label={existing ? `Revise ${existing.name}` : 'Record a technology component'} onSubmit={save}>
      <Stack gap="sm">
        {existing ? (
          <p>Category: {vocabularyLabel(c.category)} (fixed once recorded)</p>
        ) : (
          <Choice label="Category" value={c.category} options={vocabulary(componentCategories)} onChange={(category) => set({ category })} />
        )}
        <Text label="Name" value={c.name} required onInput={(name) => set({ name })} />
        <Choice
          label="Owner"
          value={c.ownerPersonId}
          options={people.map((p) => ({ value: p.personId, label: p.displayName }))}
          onChange={(ownerPersonId) => set({ ownerPersonId })}
        />
        <Text label="Environment reference (optional)" value={c.environmentReference} onInput={(environmentReference) => set({ environmentReference })} />
        <Text label="Location reference (optional)" value={c.locationReference} onInput={(locationReference) => set({ locationReference })} />
        {!existing && c.category === 'cloud_account' ? (
          <SystemInstancePicker value={c.systemInstanceId} onChange={(systemInstanceId) => set({ systemInstanceId })} />
        ) : null}
        {c.category === 'endpoint_class' ? (
          <>
            <Text
              label="Endpoint count"
              type="number"
              value={c.endpointCount === null ? '' : String(c.endpointCount)}
              required
              onInput={(value) => set({ endpointCount: value === '' ? null : Number(value) })}
            />
            <Text label="Management source" value={c.managementSource} required onInput={(managementSource) => set({ managementSource })} />
          </>
        ) : null}
        {existing ? (
          <Choice label="Lifecycle" value={c.lifecycle} options={vocabulary(lifecycles)} onChange={(lifecycle) => set({ lifecycle })} />
        ) : null}
        <ActionError error={action.error()} record="component" />
        {action.notice() ? <p role="status">{action.notice()}</p> : null}
        <Submit pending={action.pending()} label={existing ? 'Save component' : 'Record component'} />
      </Stack>
    </form>
  );
}

// Lists the data flows a classification or lifecycle change would reclassify or break, so the
// owner sees the downstream effect before saving.
function AssetImpact({ impact, flows }: { impact: AssetChangeImpact; flows: DataFlow[] }) {
  const purpose = (id: string) => flows.find((f) => f.dataFlowId === id)?.purpose ?? 'Unknown data flow';
  return (
    <section className="inventory-impact" aria-label="Impact of this change">
      <p>
        Classification: {vocabularyLabel(impact.currentClassification)} → {vocabularyLabel(impact.proposedClassification)}. Lifecycle:{' '}
        {vocabularyLabel(impact.currentLifecycle)} → {vocabularyLabel(impact.proposedLifecycle)}.
      </p>
      {impact.affectedFlows.length === 0 ? (
        <p>No data flows are affected.</p>
      ) : (
        <ul className="plain-list">
          {impact.affectedFlows.map((f) => (
            <li>
              <strong>{purpose(f.dataFlowId)}</strong>
              {f.classificationChanged
                ? `: reclassified ${vocabularyLabel(f.recordedClassification)} → ${vocabularyLabel(f.recomputedClassification)}`
                : `: stays ${vocabularyLabel(f.recordedClassification)}`}
              {f.encryptionViolation ? '; needs encryption or an exception' : ''}
              {f.carriesRetiredAssetOnly ? '; would carry only retired assets' : ''}
            </li>
          ))}
        </ul>
      )}
      {impact.flowsOverLimit ? <p>More flows are affected than this preview lists.</p> : null}
    </section>
  );
}

function AssetForm({
  existing,
  people,
  flows = [],
  onSaved,
}: {
  existing: InformationAsset | null;
  people: Person[];
  flows?: DataFlow[];
  onSaved: () => void;
}) {
  const [impact, setImpact] = state<AssetChangeImpact | null>(null);
  const [impactError, setImpactError] = state<Error | null>(null);
  const [content, setContent] = state<AssetContent>(
    existing ?? { name: '', classification: '', retentionReference: '', ownerPersonId: '', description: null, lifecycle: 'active' }
  );
  const action = useAction(onSaved);
  const set = (patch: Partial<AssetContent>) => setContent({ ...content(), ...patch });
  const c = content();
  function save(event: Event) {
    const cleaned = { ...c, name: c.name.trim(), retentionReference: c.retentionReference.trim(), description: optional(c.description) };
    void action.run(
      event,
      () => (existing ? reviseAsset(existing.informationAssetId, existing.revision, cleaned) : recordAsset(cleaned)),
      existing ? 'Information asset saved.' : 'Information asset recorded.'
    );
  }
  return (
    <form aria-label={existing ? `Revise ${existing.name}` : 'Record an information asset'} onSubmit={save}>
      <Stack gap="sm">
        <Text label="Name" value={c.name} required onInput={(name) => set({ name })} />
        <Choice label="Classification" value={c.classification} options={vocabulary(classifications)} onChange={(classification) => set({ classification })} />
        <Text label="Retention reference" value={c.retentionReference} required onInput={(retentionReference) => set({ retentionReference })} />
        <Choice
          label="Owner"
          value={c.ownerPersonId}
          options={people.map((p) => ({ value: p.personId, label: p.displayName }))}
          onChange={(ownerPersonId) => set({ ownerPersonId })}
        />
        <Field label="Description (optional)">
          <textarea value={c.description ?? ''} onInput={(event: Event) => set({ description: inputValue(event) })} />
        </Field>
        {existing ? (
          <Choice label="Lifecycle" value={c.lifecycle} options={vocabulary(lifecycles)} onChange={(lifecycle) => set({ lifecycle })} />
        ) : null}
        {existing ? (
          <>
            <Button
              variant="secondary"
              onPress={() => {
                setImpactError(null);
                previewAssetChange(existing.informationAssetId, existing.revision, { classification: c.classification, lifecycle: c.lifecycle })
                  .then(setImpact)
                  .catch((failure: unknown) => setImpactError(failure instanceof Error ? failure : new Error('The preview failed.')));
              }}
            >
              Preview impact
            </Button>
            <ActionError error={impactError()} record="information asset" />
            {impact() ? <AssetImpact impact={impact()!} flows={flows} /> : null}
          </>
        ) : null}
        <ActionError error={action.error()} record="information asset" />
        {action.notice() ? <p role="status">{action.notice()}</p> : null}
        <Submit pending={action.pending()} label={existing ? 'Save information asset' : 'Record information asset'} />
      </Stack>
    </form>
  );
}

function FlowForm({
  existing,
  people,
  components,
  assets,
  onSaved,
}: {
  existing: DataFlow | null;
  people: Person[];
  components: TechnologyComponent[];
  assets: InformationAsset[];
  onSaved: () => void;
}) {
  const [content, setContent] = state<FlowContent>(
    existing ?? {
      sourceType: 'technology_component',
      sourceId: '',
      destinationType: 'technology_component',
      destinationId: null,
      destinationParty: null,
      informationAssetIds: [],
      purpose: '',
      encryptedInTransit: true,
      encryptedAtRest: true,
      exceptionReference: null,
      effectiveFrom: new Date().toISOString().slice(0, 10),
      ownerPersonId: '',
      lifecycle: 'active',
    }
  );
  const action = useAction(onSaved);
  const set = (patch: Partial<FlowContent>) => setContent({ ...content(), ...patch });
  const c = content();
  const carried = assets.filter((a) => c.informationAssetIds.includes(a.informationAssetId));
  const sensitive = carried.some((a) => a.classification === 'confidential' || a.classification === 'restricted');
  const needsException = sensitive && !(c.encryptedInTransit && c.encryptedAtRest);
  function save(event: Event) {
    const cleaned = {
      ...c,
      purpose: c.purpose.trim(),
      destinationParty: optional(c.destinationParty),
      exceptionReference: optional(c.exceptionReference),
    };
    void action.run(
      event,
      () => (existing ? reviseFlow(existing.dataFlowId, existing.revision, cleaned) : recordFlow(cleaned)),
      existing ? 'Data flow saved.' : 'Data flow recorded.'
    );
  }
  const componentOptions = components.map((x) => ({ value: x.componentId, label: `${x.name} (${vocabularyLabel(x.category)})` }));
  return (
    <form aria-label={existing ? `Revise data flow ${existing.purpose}` : 'Record a data flow'} onSubmit={save}>
      <Stack gap="sm">
        <Choice label="Source component" value={c.sourceId} options={componentOptions} onChange={(sourceId) => set({ sourceId })} />
        <Choice
          label="Destination type"
          value={c.destinationType}
          options={[
            { value: 'technology_component', label: 'Data store component' },
            { value: 'external_party', label: 'Named external party' },
          ]}
          onChange={(destinationType) => set({ destinationType, destinationId: null, destinationParty: null })}
        />
        {c.destinationType === 'external_party' ? (
          <Text label="External party" value={c.destinationParty} required onInput={(destinationParty) => set({ destinationParty })} />
        ) : (
          <Choice
            label="Destination data store"
            value={c.destinationId ?? ''}
            options={components.filter((x) => x.category === 'data_store').map((x) => ({ value: x.componentId, label: x.name }))}
            onChange={(destinationId) => set({ destinationId })}
          />
        )}
        <fieldset className="inventory-assets">
          <legend>Information assets carried</legend>
          {assets.length === 0 ? <p>Record an information asset first.</p> : null}
          {assets.map((asset) => (
            <label>
              <input
                type="checkbox"
                checked={c.informationAssetIds.includes(asset.informationAssetId)}
                onChange={(event: Event) =>
                  set({
                    informationAssetIds: (event.target as HTMLInputElement).checked
                      ? [...c.informationAssetIds, asset.informationAssetId]
                      : c.informationAssetIds.filter((id) => id !== asset.informationAssetId),
                  })
                }
              />{' '}
              {asset.name} ({vocabularyLabel(asset.classification)})
            </label>
          ))}
        </fieldset>
        <Field label="Purpose">
          <textarea value={c.purpose} required onInput={(event: Event) => set({ purpose: inputValue(event) })} />
        </Field>
        <label>
          <input
            type="checkbox"
            checked={c.encryptedInTransit}
            onChange={(event: Event) => set({ encryptedInTransit: (event.target as HTMLInputElement).checked })}
          />{' '}
          Encrypted in transit
        </label>
        <label>
          <input
            type="checkbox"
            checked={c.encryptedAtRest}
            onChange={(event: Event) => set({ encryptedAtRest: (event.target as HTMLInputElement).checked })}
          />{' '}
          Encrypted at rest
        </label>
        {needsException ? (
          <p className="inventory-rule" role="note">
            This flow carries confidential or restricted information without full encryption. Record an approved exception
            reference.
          </p>
        ) : null}
        <Text label="Exception reference (optional)" value={c.exceptionReference} required={needsException} onInput={(exceptionReference) => set({ exceptionReference })} />
        <Text label="Effective from" type="date" value={c.effectiveFrom} required onInput={(effectiveFrom) => set({ effectiveFrom })} />
        <Choice
          label="Owner"
          value={c.ownerPersonId}
          options={people.map((p) => ({ value: p.personId, label: p.displayName }))}
          onChange={(ownerPersonId) => set({ ownerPersonId })}
        />
        {existing ? (
          <Choice label="Lifecycle" value={c.lifecycle} options={vocabulary(lifecycles)} onChange={(lifecycle) => set({ lifecycle })} />
        ) : null}
        <ActionError error={action.error()} record="data flow" />
        {action.notice() ? <p role="status">{action.notice()}</p> : null}
        <Submit pending={action.pending()} label={existing ? 'Save data flow' : 'Record data flow'} />
      </Stack>
    </form>
  );
}

function History<T extends { revision: number; lastChangedBy: string; lastChangedAt: string }>({
  load,
  describe,
}: {
  load: () => Promise<T[]>;
  describe: (item: T) => string;
}) {
  const history = resource(load, []);
  if (history.pending) return <Spinner label="Loading history" />;
  if (history.error) {
    return (
      <Stack gap="sm">
        <p role="alert">{history.error.message}</p>
        <Button variant="secondary" onPress={() => history.refresh()}>
          Try again
        </Button>
      </Stack>
    );
  }
  return (
    <ol className="plain-list inventory-history">
      {(history.value ?? []).map((item) => (
        <li>
          <strong>Revision {item.revision}</strong> by {item.lastChangedBy} on {formatDate(item.lastChangedAt)}: {describe(item)}
        </li>
      ))}
    </ol>
  );
}

// One inventory record: its summary plus on-demand revise and history panels.
function RecordRow({
  name,
  summary,
  editor,
  history,
}: {
  name: string;
  summary: string;
  editor: () => unknown;
  history: () => unknown;
}) {
  const [panel, setPanel] = state<'none' | 'edit' | 'history'>('none');
  const toggle = (next: 'edit' | 'history') => setPanel(panel() === next ? 'none' : next);
  return (
    <li className="inventory-record">
      <p>
        <strong>{name}</strong> · {summary}
      </p>
      <div className="inventory-actions">
        <Button variant="secondary" onPress={() => toggle('edit')} aria-expanded={panel() === 'edit' ? 'true' : 'false'}>
          Revise {name}
        </Button>
        <Button variant="secondary" onPress={() => toggle('history')} aria-expanded={panel() === 'history' ? 'true' : 'false'}>
          History of {name}
        </Button>
      </div>
      {panel() === 'edit' ? editor() : panel() === 'history' ? history() : null}
    </li>
  );
}

function componentSummary(c: TechnologyComponent, owner: (id: string) => string) {
  return [
    vocabularyLabel(c.category),
    `owner ${owner(c.ownerPersonId)}`,
    vocabularyLabel(c.lifecycle),
    c.environmentReference ? `environment ${c.environmentReference}` : null,
    c.endpointCount !== null ? `${c.endpointCount} endpoints via ${c.managementSource}` : null,
    `revision ${c.revision}`,
    c.sourceKind === 'manual' ? 'manually recorded' : `source ${c.sourceKind}`,
  ]
    .filter(Boolean)
    .join(' · ');
}

function assetSummary(a: InformationAsset, owner: (id: string) => string) {
  return [
    `${vocabularyLabel(a.classification)} information`,
    `retention ${a.retentionReference}`,
    `owner ${owner(a.ownerPersonId)}`,
    vocabularyLabel(a.lifecycle),
    `revision ${a.revision}`,
  ].join(' · ');
}

export function InventoryPage() {
  const [section, setSection] = state<Section>('components');
  const [version, setVersion] = state(0);
  const people = resource(() => listPeople(), [version()]);
  const components = resource(() => listComponents(), [version()]);
  const assets = resource(() => listAssets(), [version()]);
  const flows = resource(() => listFlows(), [version()]);
  const reload = () => setVersion(version() + 1);

  const denied = [components.error, assets.error, flows.error].every(
    (error) => error instanceof InventoryRequestError && error.status === 403
  );
  if (denied) {
    return (
      <Page>
        <EmptyState
          title="The inventory is not available to you"
          titleAs="h1"
          description="Ask a Compliance Lead or Org Admin for access to this organization's technology inventory."
        />
      </Page>
    );
  }

  const peopleList = people.value ?? [];
  const owner = (id: string) => peopleList.find((p) => p.personId === id)?.displayName ?? 'Unknown person';
  const componentList = components.value ?? [];
  const assetList = assets.value ?? [];
  const componentName = (id: string | null) => componentList.find((x) => x.componentId === id)?.name ?? 'Unknown component';
  const assetName = (id: string) => assetList.find((x) => x.informationAssetId === id)?.name ?? 'Unknown asset';

  function listState<T>(
    list: { pending: boolean; error: Error | null | undefined; value: T[] | null | undefined; refresh: () => void },
    noun: string,
    render: (items: T[]) => unknown
  ) {
    if (list.pending && !list.value) return <Spinner label={`Loading ${noun}`} />;
    if (list.error) {
      const forbidden = list.error instanceof InventoryRequestError && list.error.status === 403;
      return (
        <Stack gap="sm">
          <p role="alert">{list.error.message}</p>
          {forbidden ? null : (
            <Button variant="secondary" onPress={() => list.refresh()}>
              Try again
            </Button>
          )}
        </Stack>
      );
    }
    const items = list.value ?? [];
    if (items.length === 0) return <p>No {noun} recorded yet.</p>;
    return render(items);
  }

  const flowSummary = (f: DataFlow) =>
    [
      `${componentName(f.sourceId)} → ${f.destinationType === 'external_party' ? `${f.destinationParty} (external)` : componentName(f.destinationId)}`,
      `carries ${f.informationAssetIds.map(assetName).join(', ')} (${vocabularyLabel(f.classification)})`,
      `in transit ${f.encryptedInTransit ? 'encrypted' : 'not encrypted'}, at rest ${f.encryptedAtRest ? 'encrypted' : 'not encrypted'}`,
      f.exceptionReference ? `exception ${f.exceptionReference}` : null,
      `owner ${owner(f.ownerPersonId)}`,
      `effective ${f.effectiveFrom}`,
      vocabularyLabel(f.lifecycle),
      `revision ${f.revision}`,
    ]
      .filter(Boolean)
      .join(' · ');

  const current = section();
  return (
    <Page>
      <PageHeader
        title="Inventory"
        description="Record the technology components, information assets, and data flows your SOC 2 scope depends on."
      />
      <Stack gap="md">
        <nav aria-label="Inventory sections" className="inventory-sections">
          {sections.map((item) => (
            <Button
              variant={current === item.key ? 'primary' : 'secondary'}
              aria-pressed={current === item.key ? 'true' : 'false'}
              onPress={() => setSection(item.key)}
            >
              {item.label}
            </Button>
          ))}
        </nav>
        {people.error ? (
          <p role="alert">
            {people.error.message} Owners can't be chosen until the people directory loads.{' '}
            <Button variant="secondary" onPress={() => people.refresh()}>
              Try again
            </Button>
          </p>
        ) : null}
        {current === 'components' ? (
          <Card>
            <CardHeader>
              <CardTitle>Technology components</CardTitle>
              <CardDescription>Cloud accounts, environments, networks, data stores, repositories, and endpoint classes.</CardDescription>
            </CardHeader>
            <CardContent>
              <Stack gap="md">
                {listState(components, 'technology components', (items: TechnologyComponent[]) => (
                  <ul className="plain-list inventory-records">
                    {items.map((item) => (
                      <RecordRow
                        name={item.name}
                        summary={componentSummary(item, owner)}
                        editor={() => <ComponentForm existing={item} people={peopleList} onSaved={reload} />}
                        history={() => (
                          <History
                            load={() => listComponentRevisions(item.componentId)}
                            describe={(r: TechnologyComponent) => `${r.name}; ${componentSummary(r, owner)}`}
                          />
                        )}
                      />
                    ))}
                  </ul>
                ))}
                <details>
                  <summary>Record a technology component</summary>
                  <ComponentForm existing={null} people={peopleList} onSaved={reload} />
                </details>
              </Stack>
            </CardContent>
          </Card>
        ) : current === 'assets' ? (
          <Card>
            <CardHeader>
              <CardTitle>Information assets</CardTitle>
              <CardDescription>The information you hold, its classification, retention, and owner.</CardDescription>
            </CardHeader>
            <CardContent>
              <Stack gap="md">
                {listState(assets, 'information assets', (items: InformationAsset[]) => (
                  <ul className="plain-list inventory-records">
                    {items.map((item) => (
                      <RecordRow
                        name={item.name}
                        summary={assetSummary(item, owner)}
                        editor={() => <AssetForm existing={item} people={peopleList} flows={flows.value ?? []} onSaved={reload} />}
                        history={() => (
                          <History
                            load={() => listAssetRevisions(item.informationAssetId)}
                            describe={(r: InformationAsset) => `${r.name}; ${assetSummary(r, owner)}`}
                          />
                        )}
                      />
                    ))}
                  </ul>
                ))}
                <details>
                  <summary>Record an information asset</summary>
                  <AssetForm existing={null} people={peopleList} onSaved={reload} />
                </details>
              </Stack>
            </CardContent>
          </Card>
        ) : (
          <Card>
            <CardHeader>
              <CardTitle>Data flows</CardTitle>
              <CardDescription>
                How information moves between components and to external parties. Flows carrying confidential or restricted
                information must be encrypted in transit and at rest, or cite an approved exception.
              </CardDescription>
            </CardHeader>
            <CardContent>
              <Stack gap="md">
                {listState(flows, 'data flows', (items: DataFlow[]) => (
                  <ul className="plain-list inventory-records">
                    {items.map((item) => (
                      <RecordRow
                        name={item.purpose}
                        summary={flowSummary(item)}
                        editor={() => (
                          <FlowForm existing={item} people={peopleList} components={componentList} assets={assetList} onSaved={reload} />
                        )}
                        history={() => (
                          <History load={() => listFlowRevisions(item.dataFlowId)} describe={(r: DataFlow) => `${r.purpose}; ${flowSummary(r)}`} />
                        )}
                      />
                    ))}
                  </ul>
                ))}
                <details>
                  <summary>Record a data flow</summary>
                  <FlowForm existing={null} people={peopleList} components={componentList} assets={assetList} onSaved={reload} />
                </details>
              </Stack>
            </CardContent>
          </Card>
        )}
      </Stack>
    </Page>
  );
}

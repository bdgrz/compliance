#!/usr/bin/env node
// Jev-assisted backlog triage. See docs/product/delivery-cycle.md.
//
// Code owns the dependency graph, thresholds, and ranking policy. Jev supplies
// semantic judgments that code cannot: issue shape, delivered acceptance,
// unrecorded blockers, release value, and bundle separability. Output is a
// triage signal for a person; it never edits GitHub.
//
// Usage:
//   TYPESAFE_API_KEY=... node scripts/backlog-jev.mjs triage [--milestone R1] [--json out.json]
//   TYPESAFE_API_KEY=... node scripts/backlog-jev.mjs criteria <issue> [<issue> ...] [--json out.json]
//   TYPESAFE_API_KEY=... node scripts/backlog-jev.mjs preflight <issue> [<issue> ...] [--base origin/develop] [--json out.json]

import { execFileSync } from 'node:child_process';
import fs from 'node:fs';

const REPO = process.env.BACKLOG_REPO ?? 'bdgrz/compliance';
const PROJECT_OWNER = process.env.BACKLOG_PROJECT_OWNER ?? 'bdgrz';
const PROJECT_NUMBER = process.env.BACKLOG_PROJECT_NUMBER ?? '1';
const MODEL = process.env.TYPESAFE_MODEL ?? 'jev-latest';
const CONCURRENCY = 4;

// Policy thresholds. Jev probabilities are triage signals, not closure authority.
const HIDDEN_BLOCKER_REVIEW = 0.5;
const SPLIT_CONFIDENCE = 0.7;
const REQUIREMENT_MIN = 0.5;
const DELIVERED_MIN = 0.7;

const R1_GOAL =
    'R1 goal: a first client organization can use a manual, governed SOC 2 program (no imports or connectors) to record its scope, criteria, controls, risks, inventories, workforce, commitments, and vendors, then complete a readiness assessment and own a gap plan.';

const gh = (...args) => execFileSync('gh', args, { encoding: 'utf8', maxBuffer: 64 * 1024 * 1024 });
const ghJson = (...args) => JSON.parse(gh(...args));
const trunc = (s, n) => ((s ?? '').length > n ? `${s.slice(0, n)} …[truncated]` : (s ?? ''));
const pct = (x) => x.toFixed(2);

async function jev(state, questions, attempt = 0) {
    const key = process.env.TYPESAFE_API_KEY;
    if (!key) throw new Error('Set TYPESAFE_API_KEY for this command only.');
    const res = await fetch('https://api.typesafe.ai/v1/systemone', {
        method: 'POST',
        headers: { Authorization: `Bearer ${key}`, 'Content-Type': 'application/json' },
        body: JSON.stringify({ model: MODEL, state, questions }),
    });
    if ((res.status === 429 || res.status >= 500) && attempt < 5) {
        await new Promise((r) => setTimeout(r, 1000 * 2 ** attempt));
        return jev(state, questions, attempt + 1);
    }
    if (!res.ok) throw new Error(`TypeSafe ${res.status}: ${await res.text()}`);
    const body = await res.json();
    usage.input += body.usage?.input_tokens ?? 0;
    return body.answers;
}
const usage = { input: 0 };

async function pool(items, fn) {
    const out = new Array(items.length);
    let next = 0;
    await Promise.all(
        Array.from({ length: CONCURRENCY }, async () => {
            while (next < items.length) {
                const k = next++;
                out[k] = await fn(items[k]);
            }
        }),
    );
    return out;
}

// Most probable level label of a Score answer, e.g. "Very large".
const level = (a) => {
    const [k] = Object.entries(a.probabilities).sort((x, y) => y[1] - x[1])[0];
    return a.legend[k].split(':')[0];
};

// ---------- GitHub loading ----------

function loadMergedPrs() {
    return ghJson('pr', 'list', '--repo', REPO, '--state', 'merged', '--limit', '400', '--json', 'number,title,body,mergedAt');
}

function prsReferencing(prs, n) {
    const re = new RegExp(`#${n}\\b`);
    return prs.filter((p) => re.test(p.title) || re.test(p.body ?? ''));
}

function loadIssue(n, prs) {
    const issue = ghJson('issue', 'view', String(n), '--repo', REPO, '--json', 'number,title,body,labels,milestone,comments,state');
    const edges = (path) => {
        try {
            return ghJson('api', `repos/${REPO}/issues/${n}/${path}`, '--jq', '[.[] | {number, state, title}]');
        } catch {
            return [];
        }
    };
    issue.blockers = edges('dependencies/blocked_by');
    issue.subIssues = edges('sub_issues');
    issue.openBlockers = issue.blockers.filter((b) => b.state === 'open');
    issue.labelNames = issue.labels.map((l) => l.name);
    issue.prs = prsReferencing(prs, n);
    issue.kind = issue.labelNames.includes('type:discovery')
        ? 'discovery'
        : issue.subIssues.length > 0 || !/backend|frontend|reliability/i.test(issue.title)
            ? 'parent'
            : 'implementation';
    return issue;
}

function loadQueues() {
    const items = ghJson('project', 'item-list', PROJECT_NUMBER, '--owner', PROJECT_OWNER, '--limit', '1000', '--format', 'json').items;
    return new Map(items.filter((i) => i.content?.number).map((i) => [i.content.number, i['delivery queue'] ?? 'none']));
}

const evidence = (issue, prBodyLimit) => ({
    progress_comments: issue.comments.slice(-4).map((c) => ({ date: c.createdAt.slice(0, 10), body: trunc(c.body, 1500) })),
    merged_prs_referencing_issue: issue.prs.slice(0, 8).map((p) => ({
        number: p.number,
        title: p.title,
        merged: p.mergedAt?.slice(0, 10),
        body: trunc(p.body, prBodyLimit),
    })),
});

// ---------- triage ----------

const implementationQuestions = {
    shape: {
        type: 'choice',
        instructions:
            'Classify how the remaining work in `issue` should be delivered. Delivery rule: each pull request should fully close at least one issue and be one reviewable capability (domain behavior, authorized API, and acceptance tests together). Use `progress_comments` and `merged_prs_referencing_issue` to judge what remains.',
        criteria: {
            single_capability: 'The remaining acceptance is one coherent capability that one reviewable pull request can deliver and close.',
            split_needed:
                'The remaining acceptance bundles several independently useful outcomes (separate aggregates, records, workflows, or policies) that would each need their own pull request; the issue should be split so each part can close on its own.',
            test_or_layer_only:
                "The issue only asks for tests, proofs, or one technical layer, with no user or operator outcome of its own; it belongs inside another capability's pull request.",
            already_satisfied: 'The evidence indicates the acceptance is already delivered; what remains is verification and closing the issue.',
        },
    },
    delivered: {
        type: 'score',
        instructions: 'Based on `progress_comments` and `merged_prs_referencing_issue`, how much of the acceptance criteria in `issue.body` has already been delivered?',
        criteria: [
            'None: no merged work delivers any acceptance criterion of this issue.',
            'Some: merged work delivers a part, but most acceptance criteria remain.',
            'Most: most acceptance criteria are delivered and the remainder is small and named.',
            'All: every acceptance criterion appears delivered; only verification or closure remains.',
        ],
    },
    hidden_blocker: {
        type: 'noul',
        instructions: [
            'Can work on the remaining acceptance of `issue` start today? Open items in `recorded_dependencies` are tracked separately; ignore them here.',
            'Answer yes only if `issue.body` or `progress_comments` names an UNRESOLVED prerequisite that is NOT in `recorded_dependencies`: an open product or professional decision, a missing real-world fact from a client, auditor, or supplier, or a missing upstream platform capability (for example a Portia or Fitz feature that does not exist yet).',
            'Closed items in `recorded_dependencies` are resolved. Items the text says do not block, or that only apply to later imports, connectors, or frontend delivery, are not prerequisites.',
        ],
        criteria: {
            true: 'Yes: an unrecorded, unresolved prerequisite prevents starting the remaining work.',
            false: 'No: nothing outside `recorded_dependencies` prevents starting the remaining work.',
        },
    },
    release_value: {
        type: 'score',
        instructions: 'How much does finishing `issue` advance `goal`?',
        criteria: [
            'Peripheral: reliability hardening, convenience, import, or later-stage work the goal does not need.',
            'Supporting: useful to the goal but the readiness assessment can be completed without it.',
            'Core: a record or workflow that the readiness assessment directly depends on.',
            'Critical path: the readiness assessment cannot be completed or trusted without it, and several other R1 items wait on it.',
        ],
    },
};

const discoveryQuestions = {
    evidence_source: {
        type: 'choice',
        instructions: 'Who or what must supply the answer that closes `issue`?',
        criteria: {
            product_owner: 'The product owner can decide it alone from existing knowledge.',
            first_client: 'Facts or artifacts from the first client organization.',
            auditor_or_consultant: 'The audit firm or the readiness consultant.',
            supplier_or_licensor: 'An external content supplier or licensor.',
            measurement: 'Observed usage or measured effort that does not exist yet.',
        },
    },
    blocks_manual_path: {
        type: 'noul',
        instructions:
            'Does building and closing the manual R1 data path (people entering records by hand in the product) actually require the answer to `issue` first? Answer no if the answer only informs the content entered later, imports, connectors, or later milestones.',
        criteria: { true: 'Yes, manual-path implementation cannot be finished without this answer.', false: 'No, the manual path can be built and closed without it.' },
    },
};

const bundleQuestions = {
    bundle: {
        type: 'noul',
        instructions:
            'Should `upstream` and `downstream` be delivered in ONE pull request? Yes only when their acceptance is inseparable: neither can be accepted and closed without the other (a shared new contract, a circular dependency, or `upstream` has no meaningful proof except through `downstream`). No when `upstream` can be merged and closed first and `downstream` builds on it later.',
        criteria: { true: 'Inseparable; deliver together.', false: 'Separable; deliver upstream first.' },
    },
};

const issueState = (issue) => ({
    goal: R1_GOAL,
    issue: { number: issue.number, title: issue.title, milestone: issue.milestone?.title, labels: issue.labelNames, body: trunc(issue.body, 7000) },
    ...evidence(issue, 700),
    recorded_dependencies: issue.blockers.map((b) => ({ issue: `#${b.number} ${b.title}`, state: b.state })),
});

async function triage(milestonePrefix) {
    const open = ghJson('issue', 'list', '--repo', REPO, '--state', 'open', '--limit', '1000', '--json', 'number,milestone');
    const numbers = open.filter((i) => (i.milestone?.title ?? '').startsWith(milestonePrefix)).map((i) => i.number);
    const prs = loadMergedPrs();
    const queues = loadQueues();
    const issues = new Map(numbers.map((n) => [n, loadIssue(n, prs)]));
    for (const i of issues.values()) i.queue = queues.get(i.number) ?? 'none';

    const all = [...issues.values()];
    const impl = all.filter((i) => i.kind === 'implementation');
    const disc = all.filter((i) => i.kind === 'discovery');
    const pairs = impl.flatMap((d) => d.openBlockers.map((b) => issues.get(b.number)).filter((u) => u?.kind === 'implementation').map((u) => [u, d]));

    const implAnswers = await pool(impl, async (i) => ({ issue: i, a: await jev(issueState(i), implementationQuestions) }));
    const discAnswers = await pool(disc, async (i) => ({ issue: i, a: await jev(issueState(i), discoveryQuestions) }));
    const pairAnswers = await pool(pairs, async ([u, d]) => {
        const state = {
            upstream: { number: u.number, title: u.title, body: trunc(u.body, 3500) },
            downstream: { number: d.number, title: d.title, body: trunc(d.body, 3500) },
        };
        return { upstream: u, downstream: d, p: (await jev(state, bundleQuestions)).bundle.noul };
    });

    // Transitive count of open issues waiting on each issue (graph, not model).
    const dependents = new Map();
    for (const i of all) for (const b of i.openBlockers) dependents.set(b.number, [...(dependents.get(b.number) ?? []), i.number]);
    const fanout = (n, seen = new Set()) => {
        for (const d of dependents.get(n) ?? []) if (!seen.has(d)) (seen.add(d), fanout(d, seen));
        return seen.size;
    };

    const out = [];
    const line = (s = '') => out.push(s);
    const title = (i) => `#${i.number} ${i.title}`;

    line(`# Backlog triage: ${milestonePrefix} (${new Date().toISOString().slice(0, 10)})`);
    line();
    line('Signals only. Confirm against issue state, decisions, code, tests, and CI before changing the board or closing anything.');

    line();
    line('## Queue says Blocked, graph says free');
    line();
    for (const { issue: i, a } of implAnswers.filter(({ issue: i }) => i.queue === 'Blocked' && i.openBlockers.length === 0)) {
        const p = a.hidden_blocker.noul;
        line(`- ${title(i)}: unrecorded blocker ${pct(p)} → ${p >= HIDDEN_BLOCKER_REVIEW ? 'review before Ready' : 'Ready'}`);
    }

    line();
    line('## Ready ranking');
    line();
    line('Rank = 0.5·release value + 0.35·relative fanout − 0.3·unrecorded blocker. Only issues with no open recorded blocker.');
    line();
    const ready = implAnswers.filter(({ issue: i }) => i.openBlockers.length === 0);
    const maxFan = Math.max(1, ...ready.map(({ issue: i }) => fanout(i.number)));
    const ranked = ready
        .map((r) => ({ ...r, fan: fanout(r.issue.number) }))
        .map((r) => ({ ...r, rank: (0.5 * r.a.release_value.score) / 3 + (0.35 * r.fan) / maxFan - 0.3 * r.a.hidden_blocker.noul }))
        .sort((x, y) => y.rank - x.rank);
    line('| Rank | Issue | Queue | Value | Unblocks | Unrecorded blocker | Shape |');
    line('| --- | --- | --- | --- | --- | --- | --- |');
    for (const r of ranked) {
        line(
            `| ${pct(r.rank)} | ${title(r.issue)} | ${r.issue.queue} | ${level(r.a.release_value)} | ${r.fan} | ${pct(r.a.hidden_blocker.noul)} | ${r.a.shape.choice} (${pct(r.a.shape.confidence)}) |`,
        );
    }

    line();
    line(`## Split before starting (split_needed ≥ ${SPLIT_CONFIDENCE})`);
    line();
    for (const { issue: i, a } of implAnswers.filter(({ a }) => a.shape.choice === 'split_needed' && a.shape.confidence >= SPLIT_CONFIDENCE)) {
        line(`- ${title(i)}: ${pct(a.shape.confidence)}, delivered ${level(a.delivered)}`);
    }

    line();
    line('## Possibly satisfied (run `criteria` before closing)');
    line();
    for (const { issue: i, a } of implAnswers.filter(({ a }) => a.shape.choice === 'already_satisfied' || a.delivered.probabilities['3'] >= 0.5)) {
        line(`- ${title(i)}: shape ${a.shape.choice} (${pct(a.shape.confidence)}), P(all delivered) ${pct(a.delivered.probabilities['3'])}`);
    }

    line();
    line('## Bundle candidates (P(inseparable) ≥ 0.5)');
    line();
    const bundles = pairAnswers.filter((b) => b.p >= 0.5).sort((x, y) => y.p - x.p);
    if (bundles.length === 0) line('- None. Deliver upstream issues first.');
    for (const b of bundles) line(`- ${pct(b.p)} ${title(b.upstream)} + ${title(b.downstream)}`);

    line();
    line('## Parents whose children are all closed');
    line();
    for (const i of all.filter((i) => i.subIssues.length > 0 && i.subIssues.every((s) => s.state !== 'open'))) line(`- ${title(i)}`);

    line();
    line('## Discovery');
    line();
    for (const { issue: i, a } of discAnswers) {
        line(`- ${title(i)}: evidence from ${a.evidence_source.choice} (${pct(a.evidence_source.confidence)}), blocks manual path ${pct(a.blocks_manual_path.noul)}`);
    }

    return {
        markdown: out.join('\n'),
        json: {
            implementation: implAnswers.map(({ issue, a }) => ({ number: issue.number, queue: issue.queue, answers: a })),
            discovery: discAnswers.map(({ issue, a }) => ({ number: issue.number, answers: a })),
            pairs: pairAnswers.map(({ upstream, downstream, p }) => ({ upstream: upstream.number, downstream: downstream.number, inseparable: p })),
        },
    };
}

// ---------- per-criterion acceptance ----------

const SKIP_SECTION = /(decided|definition of done|not in this|dependencies|questions to answer|involve)\b/i;
const BOILERPLATE =
    /^(Backend child of|Frontend child of|Define authorized|Prove allowed|Personal approvals|Close only|Applicable backend dependencies|M0-D24 #136|Source rights|This story's acceptance|Decided \d|Dependencies:)/;

export function extractCriteria(body) {
    const criteria = [];
    let section = '';
    for (const block of (body ?? '').split(/\n\s*\n/)) {
        for (const raw of block.split('\n')) {
            const heading = raw.match(/^#+\s+(.*)/);
            if (heading) section = heading[1];
        }
        if (SKIP_SECTION.test(section)) continue;
        for (const raw of block.split('\n')) {
            const item = raw.match(/^\s*[-*]\s+\[( |x)\]\s+(.*)/i);
            // One claim per criterion: a compound checklist item splits on semicolons.
            if (item) {
                for (const part of item[2].split(/;\s+/)) {
                    criteria.push({ section, text: part.trim(), checked: item[1].toLowerCase() === 'x' });
                }
            }
        }
        const prose = block
            .split('\n')
            .filter((l) => !/^\s*(#|[-*]\s|\d+\.\s|\|)/.test(l))
            .join(' ')
            .trim();
        for (const sentence of prose.split(/(?<=[.;])\s+(?=[A-Z`])/)) {
            const text = sentence.trim();
            if (text.length > 25 && !BOILERPLATE.test(text)) criteria.push({ section, text, checked: false });
        }
    }
    return criteria.slice(0, 40);
}

const criterionQuestions = {
    is_requirement: {
        type: 'noul',
        instructions:
            'Is `criterion` a concrete deliverable requirement of `issue`, something that must be built or proven before the issue can close? Background, rationale, exclusions, notes that something is out of scope or does not block, and references to later work are not requirements.',
        criteria: { true: 'A concrete requirement this issue must deliver.', false: 'Context, rationale, exclusion, or deferred work.' },
    },
    status: {
        type: 'choice',
        instructions:
            'Judging only from `evidence`, what is the delivery status of `criterion`? Merged pull request descriptions and progress comments are the evidence; do not assume work happened because the issue exists.',
        criteria: {
            delivered: 'The evidence explicitly shows merged work that satisfies this criterion.',
            partially_delivered: 'The evidence shows merged work covering part of this criterion, with a named or evident remainder.',
            not_delivered: 'The evidence says this criterion remains open, or addresses the area without satisfying it.',
            no_evidence: 'The evidence does not address this criterion at all.',
        },
    },
};

async function criteria(numbers) {
    const prs = loadMergedPrs();
    const out = [];
    const json = [];
    for (const n of numbers) {
        const issue = loadIssue(n, prs);
        const ev = evidence(issue, 3000);
        const items = extractCriteria(issue.body);
        const answers = await pool(items, async (c) => ({
            ...c,
            a: await jev({ issue: { number: issue.number, title: issue.title }, criterion: c.text, section: c.section, evidence: ev }, criterionQuestions),
        }));
        const requirements = answers.filter((c) => c.a.is_requirement.noul >= REQUIREMENT_MIN);
        const done = requirements.filter((c) => c.a.status.choice === 'delivered' && c.a.status.probabilities.delivered >= DELIVERED_MIN);
        const remaining = requirements.filter((c) => !done.includes(c));
        const verdict =
            requirements.length > 0 && remaining.length === 0 ? 'close candidate: verify evidence, then close' : `keep open: ${remaining.length} of ${requirements.length} requirements not shown delivered`;

        out.push(`## #${issue.number} ${issue.title}`, '', `${verdict}. Evidence: ${issue.prs.map((p) => `#${p.number}`).join(', ') || 'no referencing PRs'}.`, '');
        out.push('| Status | P | Criterion |', '| --- | --- | --- |');
        for (const c of requirements) {
            out.push(`| ${c.a.status.choice} | ${pct(c.a.status.probabilities[c.a.status.choice])} | ${trunc(c.text, 220).replace(/\|/g, '\\|')} |`);
        }
        out.push('');
        json.push({ number: issue.number, verdict, criteria: answers.map(({ text, section, a }) => ({ text, section, answers: a })) });
    }
    return { markdown: out.join('\n'), json };
}

// ---------- branch preflight ----------

const git = (...args) => execFileSync('git', args, { encoding: 'utf8', maxBuffer: 64 * 1024 * 1024 });

// Generated or lock files are required by whatever produced them; they are not judged.
const GENERATED = /(^|\/)(api-client\/|openapi\/openapi\.json$|package-lock\.json$|packages\.lock\.json$)/;
const TEST_FILE = /(\.test\.[tj]sx?$|^test\/)/;

const brokerQuestions = {
    broker_impact: {
        type: 'noul',
        instructions:
            'Does `hunk` change behavior that only a real message broker or split API/worker deployment can prove: event-sourced aggregate persistence or streams, Fitz projections or read models, reactors or background workers, projection lag or replay, or host composition for split API and worker processes? Pure request validation, authorization rules, DTOs, and HTTP mapping that unit tests cover do not count.',
        criteria: { true: 'Needs BrokerIntegration tests.', false: 'Ordinary tests are enough.' },
    },
};

// Coverage is a selection, not a verdict: Jev must name the one changed test that proves a
// criterion, or none, so a person can check the match.
const coverageQuestions = (issue, tests) => ({
    is_requirement: criterionQuestions.is_requirement,
    proving_test: {
        type: 'choice',
        instructions: `Which one test proves \`criterion\` for issue #${issue.number} "${issue.title}"? The test must exercise this issue's feature and this specific criterion; a test of a different feature, or one that only shares generic words such as loading, error, or retry, does not prove it.`,
        criteria: {
            ...Object.fromEntries(tests.map((t, i) => [`t${i}`, `${t.name} (${t.file})`])),
            none: 'No listed test proves this criterion for this issue.',
        },
    },
});

const scopeQuestions = {
    in_scope: {
        type: 'choice',
        instructions: 'How does the change to `file` relate to delivering `issue`? `excerpt` shows part of its diff.',
        criteria: {
            acceptance: 'It implements or tests acceptance of `issue`.',
            required_maintenance: 'It is maintenance the acceptance work depends on, such as updating callers, regenerating contracts, or fixing a defect in code the issue touches.',
            unrelated: 'It serves some other purpose and belongs in a different change.',
        },
    },
};

function changedTests(base) {
    const tests = [];
    for (const file of git('diff', '--name-only', `${base}...HEAD`).trim().split('\n').filter((f) => TEST_FILE.test(f) || f.endsWith('Tests.cs'))) {
        for (const line of git('diff', `${base}...HEAD`, '-U0', '--', file).split('\n')) {
            if (!line.startsWith('+') || line.startsWith('+++')) continue;
            const ts = line.match(/\bit\(\s*['"`](.+?)['"`]/);
            const cs = line.match(/\b(?:async\s+Task|void)\s+(Should\w+)\s*\(/);
            const name = ts?.[1] ?? cs?.[1];
            if (name) tests.push({ name, file });
        }
    }
    return tests.slice(0, 250);
}

async function preflight(numbers, base) {
    const files = git('diff', '--name-only', `${base}...HEAD`).trim().split('\n').filter(Boolean);
    const judged = files.filter((f) => !GENERATED.test(f));
    const hunk = (f, n) => trunc(git('diff', `${base}...HEAD`, '-U2', '--', f), n);

    // Gates: path rules in code; Jev only decides broker impact for C# hunks.
    const client = files.some((f) => f.startsWith('src/Compliance.App/ClientApp/'));
    const dotnet = files.filter((f) => /\.(cs|csproj|props|slnx)$/.test(f));
    const brokerAnswers = await pool(
        dotnet.filter((f) => f.endsWith('.cs') && !TEST_FILE.test(f)),
        async (f) => ({ f, p: (await jev({ file: f, hunk: hunk(f, 6000) }, brokerQuestions)).broker_impact.noul }),
    );
    const broker = brokerAnswers.filter((b) => b.p >= 0.5);

    const out = [`# Preflight: ${files.length} changed files against ${base}`, '', '## Gates', ''];
    if (client) out.push('- `npm run client:check`');
    if (dotnet.length > 0) {
        out.push('- `dotnet format Compliance.slnx --verify-no-changes --no-restore`', '- `dotnet build Compliance.slnx -c Release --no-restore`');
        out.push("- `./scripts/check-backend.sh focused '<filter for the changed tests>'`");
    }
    out.push(
        broker.length > 0
            ? `- \`./scripts/check-backend.sh full\` (broker impact: ${broker.map((b) => `${b.f} ${pct(b.p)}`).join(', ')})`
            : dotnet.length > 0
              ? '- Broker tests not indicated for the C# changes'
              : '- No .NET gates: no C# or project files changed',
    );

    const tests = changedTests(base);
    const prs = loadMergedPrs();
    const json = { files, broker: brokerAnswers, issues: [] };
    for (const n of numbers) {
        const issue = loadIssue(n, prs);
        const questions = coverageQuestions(issue, tests);
        const criteriaAnswers = await pool(extractCriteria(issue.body), async (c) => ({
            ...c,
            a: await jev({ issue: { number: n, title: issue.title }, criterion: c.text }, questions),
        }));
        const provedBy = (c) => {
            const pick = c.a.proving_test;
            return pick.choice === 'none' || pick.probabilities[pick.choice] < DELIVERED_MIN ? null : tests[Number(pick.choice.slice(1))];
        };
        const requirements = criteriaAnswers.filter((c) => c.a.is_requirement.noul >= REQUIREMENT_MIN);
        const scope = await pool(judged, async (f) => ({
            f,
            a: (await jev({ issue: { number: n, title: issue.title, body: trunc(issue.body, 5000) }, file: f, excerpt: hunk(f, 2500) }, scopeQuestions)).in_scope,
        }));
        const gaps = requirements.filter((c) => provedBy(c) === null);
        const unrelated = scope.filter((s) => s.a.choice === 'unrelated');

        out.push('', `## #${n} ${issue.title}`, '', `${requirements.length - gaps.length} of ${requirements.length} requirements covered by a changed test.`, '');
        out.push('| Proving test | P | Criterion |', '| --- | --- | --- |');
        for (const c of requirements) {
            const test = provedBy(c);
            const p = c.a.proving_test.probabilities[c.a.proving_test.choice];
            out.push(`| ${test ? test.name : '**none**'} | ${pct(p)} | ${trunc(c.text, 200).replace(/\|/g, '\\|')} |`);
        }
        out.push('', unrelated.length === 0 ? 'Scope: every judged file serves this issue.' : 'Scope: possibly unrelated files:', '');
        for (const s of unrelated) out.push(`- ${s.f} (${pct(s.a.probabilities.unrelated)})`);
        json.issues.push({ number: n, criteria: criteriaAnswers, scope });
    }
    out.push('', `Changed tests considered: ${tests.length}. Generated files skipped: ${files.length - judged.length}.`);
    return { markdown: out.join('\n'), json };
}

// ---------- CLI ----------

async function main() {
    const [command, ...rest] = process.argv.slice(2);
    const flag = (name) => {
        const k = rest.indexOf(name);
        return k >= 0 ? rest.splice(k, 2)[1] : undefined;
    };
    const jsonPath = flag('--json');
    let result;
    if (command === 'triage') result = await triage(flag('--milestone') ?? 'R1');
    else if (command === 'criteria' && rest.length > 0) result = await criteria(rest.map(Number));
    else if (command === 'preflight' && rest.length > 0) {
        const base = flag('--base') ?? 'origin/develop';
        result = await preflight(rest.map(Number), base);
    }
    else {
        console.error('usage: backlog-jev.mjs triage [--milestone R1] [--json out.json]\n       backlog-jev.mjs criteria <issue> [<issue> ...] [--json out.json]\n       backlog-jev.mjs preflight <issue> [<issue> ...] [--base origin/develop] [--json out.json]');
        process.exit(2);
    }
    if (jsonPath) fs.writeFileSync(jsonPath, `${JSON.stringify(result.json, null, 2)}\n`);
    console.log(result.markdown);
    console.error(`TypeSafe input tokens: ${usage.input}`);
}

if (import.meta.url === `file://${process.argv[1]}`) await main();

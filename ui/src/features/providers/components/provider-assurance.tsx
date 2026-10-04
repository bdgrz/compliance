import { resource } from '@askrjs/askr/resources';
import { Button, Card, CardContent, CardDescription, CardHeader, CardTitle, Spinner, Stack } from '@askrjs/themes/components';

import {
  getProviderAssurance,
  optionLabel,
  ProviderRequestError,
  type Provider,
  type ProviderAssuranceData,
  type ProviderAssuranceReport,
  type ProviderReview,
} from '../providers.js';

function period(start: string | null, end: string): string {
  return `${start ?? 'Start date not recorded'} – ${end}`;
}

function coverageStatus(status: string): string {
  const labels: Record<string, string> = {
    not_required: 'Assurance not required',
    unresolved_materiality: 'Materiality unresolved',
    no_review: 'No review recorded',
    not_acceptable: 'Review conclusion needs follow-up',
    review_overdue: 'Review overdue',
    evidence_unrecorded: 'Evidence not recorded',
    evidence_stale: 'Stale evidence',
    current: 'Coverage current',
  };
  return labels[status] ?? optionLabel(status);
}

function reportFreshness(data: ProviderAssuranceData, reportId: string, revision: number): string {
  const coverage = data.coverage.reports.find((item) => item.reportId === reportId && item.revision === revision);
  if (!coverage) return 'Coverage unavailable for this report revision';
  const currency =
    coverage.currency === 'stale' ? 'Stale evidence' : coverage.currency === 'current' ? 'Current evidence' : optionLabel(coverage.currency);
  return `${currency} · ${optionLabel(coverage.periodCoverage)}`;
}

function reviewStatus(
  data: ProviderAssuranceData,
  provider: Provider,
  review: ProviderReview,
  report?: ProviderAssuranceReport
): string {
  const matchesCurrentRevision =
    review.providerRevision === provider.revision &&
    (review.assuranceReportId === null || review.assuranceReportRevision === report?.revision);
  if (!matchesCurrentRevision) return 'Historical review';
  if (data.coverage.status === 'review_overdue' || review.nextReviewDue < data.coverage.asOf) return 'Review overdue';
  if (data.coverage.status !== 'current') return coverageStatus(data.coverage.status);
  return 'Current review';
}

export function ProviderAssurance({ provider, ownerName }: { provider: Provider; ownerName: string | null }) {
  const assurance = resource(() => getProviderAssurance(provider.providerId), [provider.providerId, provider.revision]);
  const data = assurance.value;
  const owner = ownerName ?? (provider.content.ownerPersonId ? 'Person not on the roster' : 'Owner unresolved');
  const openCoverageGaps = data?.coverageGaps.filter((gap) => gap.status === 'open') ?? [];

  return (
    <Card>
      <CardHeader>
        <CardTitle>Due diligence and assurance</CardTitle>
        <CardDescription>
          Reviews, report scope and period, evidence freshness, open coverage gaps, and the next review due date.
        </CardDescription>
      </CardHeader>
      <CardContent>
        {assurance.pending && !data ? (
          <Spinner label="Loading provider assurance" />
        ) : assurance.error ? (
          <Stack gap="sm">
            <p role="alert">{assurance.error.message}</p>
            {assurance.error instanceof ProviderRequestError && assurance.error.status === 403 ? null : (
              <Button variant="secondary" onPress={() => assurance.refresh()}>
                Try again
              </Button>
            )}
          </Stack>
        ) : data ? (
          <Stack gap="md">
            <section aria-label="Assurance coverage status">
              <h3>Coverage status</h3>
              <p>
                {coverageStatus(data.coverage.status)} as of {data.coverage.asOf} ·{' '}
                {data.coverage.complete ? 'Complete' : 'Follow-up required'}
              </p>
              {data.coverage.reasons.length > 0 ? (
                <ul>
                  {data.coverage.reasons.map((reason) => <li>{optionLabel(reason)}</li>)}
                </ul>
              ) : null}
              <p>Last reviewed: {data.coverage.latestReviewedAt ?? 'Not recorded'}</p>
              <p>Next review due: {data.coverage.nextReviewDue ?? 'Not scheduled'}</p>
              <p>Accountable provider owner: {owner}</p>
            </section>

            <section aria-label="Assurance reports">
              <h3>Assurance reports</h3>
              {data.reports.length === 0 ? (
                <p>No assurance reports recorded.</p>
              ) : (
                <ul className="plain-list">
                  {data.reports.map((report) => {
                    const review = data.reviews.find((candidate) => candidate.assuranceReportId === report.reportId);
                    return (
                      <li>
                        <article aria-label={`${report.reportKind} report from ${report.issuer}`}>
                          <h4>{optionLabel(report.reportKind)} · {report.issuer}</h4>
                          <p>Scope: {report.scope}</p>
                          {report.coveredServices.length > 0 ? <p>Covered services: {report.coveredServices.join(', ')}</p> : null}
                          <p>Covered period: {period(report.periodStart, report.periodEnd)}</p>
                          {report.opinion ? <p>Auditor opinion: {optionLabel(report.opinion)}</p> : null}
                          <p>Evidence status: {reportFreshness(data, report.reportId, report.revision)}</p>
                          {review ? (
                            <p>
                              {reviewStatus(data, provider, review, report)} · Reviewed by {review.reviewedBy} · Conclusion:{' '}
                              {optionLabel(review.conclusion)}
                            </p>
                          ) : (
                            <p>No due-diligence review is linked to this report revision.</p>
                          )}
                          {review && reviewStatus(data, provider, review, report) === 'Historical review' ? (
                            <p>This review records provider revision {review.providerRevision} and report revision {review.assuranceReportRevision ?? 'none'}.</p>
                          ) : null}
                        </article>
                      </li>
                    );
                  })}
                </ul>
              )}
            </section>

            <section aria-label="Due-diligence reviews">
              <h3>Due-diligence reviews</h3>
              {data.reviews.length === 0 ? (
                <p>No due-diligence assessments recorded.</p>
              ) : (
                <ul className="plain-list">
                  {data.reviews.map((review) => {
                    const report = data.reports.find((candidate) => candidate.reportId === review.assuranceReportId);
                    return (
                      <li>
                        <article aria-label={`Due-diligence review from ${review.reviewedAt}`}>
                          <h4>{reviewStatus(data, provider, review, report)}</h4>
                          <p>Reviewed by: {review.reviewedBy}</p>
                          <p>Conclusion: {optionLabel(review.conclusion)}</p>
                          <p>Reviewed at: {review.reviewedAt} · Next review due: {review.nextReviewDue}</p>
                          {review.assuranceReportId === null ? (
                            <p>Scope and covered period are not linked to an assurance report.</p>
                          ) : report && review.assuranceReportRevision === report.revision ? (
                            <>
                              <p>Evidence scope: {report.scope}</p>
                              {report.coveredServices.length > 0 ? <p>Covered services: {report.coveredServices.join(', ')}</p> : null}
                              <p>Covered period: {period(report.periodStart, report.periodEnd)}</p>
                            </>
                          ) : report ? (
                            <p>
                              Linked report revision {review.assuranceReportRevision ?? 'not recorded'} differs from current report revision {report.revision};
                              its exact scope and covered period are unavailable, so current report details are not attributed to this review.
                            </p>
                          ) : (
                            <p>The linked assurance report is not available; its exact scope and covered period cannot be shown.</p>
                          )}
                          <p>{review.rationale}</p>
                          {reviewStatus(data, provider, review, report) === 'Historical review' ? (
                            <p>This assessment predates the current provider or report revision.</p>
                          ) : null}
                        </article>
                      </li>
                    );
                  })}
                </ul>
              )}
            </section>

            <section aria-label="Unresolved coverage gaps">
              <h3>Unresolved coverage gaps</h3>
              {openCoverageGaps.length === 0 ? (
                <p>No unresolved coverage gaps.</p>
              ) : (
                <ul className="plain-list">
                  {openCoverageGaps.map((gap) => (
                      <li>
                        <article aria-label={`Coverage gap for ${gap.service}`}>
                          <h4>{gap.service}</h4>
                          <p>{gap.assertion}</p>
                          <p>Uncovered period: {gap.periodStart} – {gap.periodEnd}</p>
                          {gap.description ? <p>{gap.description}</p> : null}
                          {gap.redacted ? (
                            <p>Risk acceptance details are restricted.</p>
                          ) : gap.riskAcceptances.length > 0 ? (
                            <>
                              <ul aria-label="Linked R1-07 risk acceptances">
                                {gap.riskAcceptances.map((acceptance) => (
                                  <li key={acceptance.acceptanceId}>
                                    R1-07 risk acceptance {acceptance.acceptanceId} for risk {acceptance.riskId}. Expires at{' '}
                                    {acceptance.expiresAt}
                                  </li>
                                ))}
                              </ul>
                              <p>The provider coverage gap stays open; remediation remains on the readiness-gap plan.</p>
                            </>
                          ) : (
                            <>
                              <p>No R1-07 risk acceptance linked.</p>
                              <p>The provider coverage gap stays open; remediation remains on the readiness-gap plan.</p>
                            </>
                          )}
                          <p>Status: Open · Provider owner: {owner}</p>
                        </article>
                      </li>
                    ))}
                </ul>
              )}
            </section>
          </Stack>
        ) : null}
      </CardContent>
    </Card>
  );
}

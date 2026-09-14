import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
  Page,
  PageHeader,
} from '@askrjs/themes/components';

export function HomePage() {
  return (
    <Page>
      <PageHeader
        title="Overview"
        description="Build and maintain the evidence your SOC 2 program needs."
      />
      <Card>
        <CardHeader>
          <CardTitle>One audit journey</CardTitle>
          <CardDescription>
            Controls, policies, evidence, and reviews stay connected to the
            audit journey.
          </CardDescription>
        </CardHeader>
        <CardContent>
          Track the work your team and consultants need to complete without
          losing the context behind each control.
        </CardContent>
      </Card>
    </Page>
  );
}

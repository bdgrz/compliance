import { state } from '@askrjs/askr';
import {
  Block,
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
  Page,
  PageHeader,
  Spinner,
} from '@askrjs/themes/components';

import { ensureActiveTenant } from '../features/tenants/tenants.js';

export function HomePage() {
  const [ready, setReady] = state(false);

  void ensureActiveTenant().then((shouldRender) => {
    if (shouldRender) {
      setReady(true);
    }
  });

  if (!ready()) {
    return (
      <Page background="muted" center>
        <Block as="section" align="center" justify="center" grow>
          <Spinner label="Loading" />
        </Block>
      </Page>
    );
  }

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

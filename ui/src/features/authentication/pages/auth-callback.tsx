import { Block, Page, Spinner, Text } from '@askrjs/themes/components';

import { ComplianceBrand } from '../../../components/compliance-brand.js';

export function AuthenticationCallbackPage() {
  return (
    <Page background="muted" center>
      <Block as="section" align="center" justify="center" grow>
        <Block align="center" direction="column" gap="lg">
          <ComplianceBrand />
          <Spinner label="Completing sign in" />
          <Text tone="muted">Completing sign in…</Text>
        </Block>
      </Block>
    </Page>
  );
}

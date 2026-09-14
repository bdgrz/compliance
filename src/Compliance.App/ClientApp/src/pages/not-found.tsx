import { Link } from '@askrjs/askr/router';
import { Button, EmptyState, Page } from '@askrjs/themes/components';

export function NotFoundPage() {
  return (
    <Page>
      <EmptyState
        title="Page not found"
        titleAs="h1"
        description="The requested compliance page does not exist."
        action={
          <Button asChild variant="primary">
            <Link href="/">Return home</Link>
          </Button>
        }
      />
    </Page>
  );
}

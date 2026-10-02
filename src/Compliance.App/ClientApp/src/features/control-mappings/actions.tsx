import { state } from '@askrjs/askr';

import { describeMappingFailure } from './mappings.js';

export function useAction() {
  const [pending, setPending] = state(false);
  const [failure, setFailure] = state<Error | null>(null);
  const [notice, setNotice] = state<string | null>(null);
  async function run(action: () => Promise<string>) {
    setFailure(null);
    setNotice(null);
    setPending(true);
    try {
      setNotice(await action());
    } catch (error) {
      setFailure(
        error instanceof Error ? error : new Error('The request failed.')
      );
    } finally {
      setPending(false);
    }
  }
  return { pending, failure, notice, run };
}

export function Feedback({
  failure,
  notice,
}: {
  failure: Error | null;
  notice: string | null;
}) {
  return (
    <>
      {failure ? <p role="alert">{describeMappingFailure(failure)}</p> : null}
      {notice ? <p role="status">{notice}</p> : null}
    </>
  );
}

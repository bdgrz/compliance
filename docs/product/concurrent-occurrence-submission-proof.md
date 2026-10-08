# Concurrent occurrence submission acceptance

Refs #278. This increment adds backend unit proof of the existing append-concurrency boundary; it changes no production behavior.

Two valid personal HTTP-dispatch commands, from the approved owner and backup owner, target the same expected occurrence at revision 0. A test-only event-store decorator delegates every source read and append to the retained in-memory store. It holds the two prepared attestation batches immediately before append until both have reached the same physical expected stream position. It does not implement concurrency, alter source events, normalize request outcomes, or choose a winner.

The production request composition returns one successful submission and propagates one real `EventStreamConcurrencyException`. Exactly one opening and one attestation survive, with the successful actor's envelope subject, opening attribution, recorder, performer and notes. The public occurrence population contains that occurrence once. Fresh registered tenant projectors replay the same retained history into new Fitz KV projections and expose one independent review item; repeated catch-up preserves its identity and counts.

The bus deliberately propagates append contention. The pinned Portia 0.7.0 generated HTTP binding maps that exception to a transient `Conflict` with the generic concurrent-update message. That generated source was inspected separately; this test invokes the production bus with a native `HttpInvocation`, rather than making an HTTP network request or claiming host/broker acceptance. An initial test expectation that the bus returned a conflict `Result` was corrected to assert its actual exception contract; no production RED→GREEN change is claimed.

Native plan/control setup uses the existing `OperationsFixture`. Actor/grant, resource-scope, tenant-activity and membership-directory doubles are explicit scaffolding. Evidence remains unresolved and governed artifact content/version acceptance stays under #196/#272. The broader #278 population reconciliation and other remaining criteria are not declared complete by this focused concurrency proof. Public pagination is outside this increment.

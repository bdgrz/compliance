# Personal control decision transport

Control review and approval are explicitly HTTP-only in `control-draft-v1.md`; retirement approval also has an explicit HTTP-only handler contract. `ReviewControl`, `ApproveControl` and `RetireControl` now require native HTTP, a canonical Bdgrz user and a non-system actor before release, source or waiver reads. Ordinary draft creation, revision, proposal, withdrawal and reads retain their existing behavior.

Six valid production AddCompliance Direct/MCP decisions succeeded before the guards. Their tests now prove denial without retained position, revision or decision changes. Native HTTP positives retain canonical decision attribution, exact target version/revision, accepted review provenance, approved/retired state and the current retirement impact digest. Real source owners and all seven production impact contributors are used; test configuration explicitly enables the existing activation/lifecycle release gates.

Native HTTP business cases assert exact author/owner separation, current source-owner verification, draft revision, accepted-review and impact-digest refusals. The transport correction does not invent additional reviewer-versus-approver duties. Existing fixtures use native HTTP only for these three decisions and retain exact review request metadata; the queue reviewer fixture changes only the control/control-assigned branch. Existing release-gate and successor/current-history tests continue to exercise their business outcomes.

This is reserve evidence under issue 277, with no parent forecast decrement or broader professional authority/compartment completion claim. No route, wire, event, grant, persistence or deployment changes are introduced.

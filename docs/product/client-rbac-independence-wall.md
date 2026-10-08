# Client administration independence wall

Thirteen concrete client administration writes additionally deny a canonical
user with retained actual Attest assignment history for that client: team
definition/deletion, team membership/role assignment/removal, member suspension,
reinstatement/deprovisioning, access grant issue/revocation, legacy role deletion
and organization invitation. Advisory and other-client history do not deny these
writes. Ordinary membership, tenant activity, role catalog, source and grants
still govern every operation.

The explicit `IClientRbacMutationRequest` family uses a supplemental authorizer.
Its trusted-system branch adds no restriction; the existing RBAC authorizer
continues to decide bootstrap authority and refuses system access-grant changes.
Fixed role catalog writes, ordinary reads and internal member registration retain
their existing authority. Neither the mixed RBAC interface nor personal invitation
acceptance gets a blanket management marker.

For human writes, exact returned tenant/user membership identity is required before
consulting canonical assignment history. Missing, suspended or deprovisioned
membership remains hidden. Relinking and engagement closure do not erase history.

Actual production dispatch tests demonstrate denied team creation/deletion without
retained mutation, ordinary client/Advisory/other-client success, permitted current
member reads, trusted system team bootstrap, refused system access-grant revocation,
fixed-role catalog denial and missing ordinary grants. Membership and permissions
are explicit doubles; handlers and retained aggregate events are real. Synthetic
assignment fixtures confer no professional acceptance, rule ratification, partner
duty or access. Cross-stream acceptance/write atomicity remains separate.

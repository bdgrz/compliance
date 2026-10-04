# Restricted application visibility

This decision records the product direction accepted in [#492](https://github.com/bdgrz/compliance/issues/492#issuecomment-5942855525) for [#449](https://github.com/bdgrz/compliance/issues/449).

## Decision

- Restriction is an explicit, governed boolean on an application. Classification and owner text do not grant or imply access.
- Reading restricted application data requires the separate `application.restricted.read` permission. That permission can be granted organization-wide or through an attributable access grant scoped to one application or one SystemInstance.
- An organization-scoped grant covers every restricted application and its SystemInstances. An application-scoped grant covers that application, its history, and its SystemInstances. A SystemInstance-scoped grant covers that instance and its instance-specific references; it does not grant access to the parent application's details, history, or sibling instances.
- The default Org Admin and Compliance Lead roles receive the read permission through recorded role-permission assignments. Membership and role/team assignment remain attributable. Other users need an explicit restricted-read grant.
- Changing an application's restriction requires `application_inventory.manage`. The change is an application revision retaining the actor, timestamp, and previous state in history.
- Unauthorized restricted records are omitted from lists, counts, search, references, notifications, and exports. Direct reads return NotFound so record existence is not disclosed.

## Delivery boundary

Implement the backend behavior with focused unit tests. Browser end-to-end testing is owned by #453 and follows only after the backend and client features work independently.

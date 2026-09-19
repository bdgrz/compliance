# Public source reference and standards-use policy

Status: required product and engineering policy, 2026-09-14

Compliance is licensed under Apache License 2.0. Domain definitions, backlog
issues, implementation subtasks, schemas, adapters, tests, and user-facing
claims must cite the public sources on which their semantics depend and must not
silently copy material whose reuse terms are incompatible or unclear.

This is an engineering policy, not legal advice. If the rights needed to use a
source are not confirmed, its information is excluded from the canonical model,
implementation, schemas, tests, and product claims until counsel or an explicit
licensing decision clears it.

## Required issue references

Every discovery issue, architecture decision, enabler, user story, and delivery
slice must include a `Public references` section containing direct links to:

- each external standard, protocol, taxonomy, regulatory source, or
  professional source used to define behavior;
- the source's copyright, implementation, or redistribution terms when any
  source material may enter the product or repository;
- the exact version or dated edition used;
- the internal canonical-model section or decision that applies.

Each implementation subtask must identify which cited semantic contract it
implements. A bare standards-body home page, search result, private portal,
vendor marketing page, or uncited claim is not sufficient.

If no external source governs a product-specific decision, the section states
`No external normative source; product decision` and links the internal decision
record. “No source” must never be left implicit.

## Use classifications

| Classification | Permitted use |
| --- | --- |
| `reference` | Read, cite, and implement the ideas or interoperability behavior without copying protected expression. |
| `redistributable` | Material may be copied or adapted only while satisfying the cited license and repository notice obligations. |
| `excluded` | Do not use the source to define the model, copy it, bundle it, or claim conformance. A public URL does not establish usable rights. |

Apache-2.0 remains the license for original Compliance code. Third-party
material retains its own license and notices; it must be isolated and recorded
in the repository's third-party-notice process rather than relicensed by
assertion.

## Approved reference register

| Source and version | Model use | Public terms | Classification and constraints |
| --- | --- | --- | --- |
| [RFC 7643, SCIM Core Schema](https://www.rfc-editor.org/rfc/rfc7643) and [RFC 7644, SCIM Protocol](https://www.rfc-editor.org/rfc/rfc7644), September 2015 | Accounts, groups, direct members, common identifiers, enterprise workforce attributes, provisioning behavior | [IETF Trust Legal Provisions 5.0](https://trustee.ietf.org/documents/trust-legal-provisions/tlp-5/) | `reference`; implementation is permitted. Do not copy RFC prose. Any copied IETF code component must retain the applicable Revised BSD notice and attribution. The IETF terms grant no patent license. |
| [OpenID Connect Core 1.0 incorporating errata set 2](https://openid.net/specs/openid-connect-core-1_0.html), December 2023 | Federated identity keyed by exact issuer and subject | The exact specification's [Appendix C notices](https://openid.net/specs/openid-connect-core-1_0.html) and [OIDF intellectual-property policy](https://openid.net/intellectual-property/openid-foundation-contribution-agreements/) | `reference`; Appendix C grants an implementation-purpose, royalty-free copyright license subject to OIDF attribution and no implied endorsement. Its contributor patent promise is limited; this row makes no broader patent or certification claim. |
| [NIST IR 6192, A Revised Model for Role Based Access Control](https://csrc.nist.gov/pubs/ir/6192/final), July 1998 | Roles, permissions, assignments, constraints, and hierarchies | [NIST publication reuse guidance](https://www.nist.gov/nist-research-library/library-faqs) | `reference`; the report lists a NIST author. Credit NIST and do not copy incorporated third-party material or the copyrighted INCITS 359 text. The NIST reuse FAQ warns it is no longer updated, so no copied NIST artifact is approved by this row. |
| [W3C PROV-O Recommendation](https://www.w3.org/TR/2013/REC-prov-o-20130430/), April 2013 | Entity, activity, agent, attribution, derivation, and collection concepts | The recommendation's linked [W3C document-use rules](https://www.w3.org/copyright/) | `reference`; cite and implement concepts. Do not copy or modify recommendation prose or ontology artifacts without confirming and preserving the applicable W3C terms and notices. |
| [RFC 8348, YANG Hardware Management](https://www.rfc-editor.org/rfc/rfc8348), March 2018 | Device components, containment, hardware classes, identifiers, and state | [IETF Trust Legal Provisions 5.0](https://trustee.ietf.org/documents/trust-legal-provisions/tlp-5/) | `reference`; copied YANG/code components use the applicable Revised BSD notice. Prefer an original canonical vocabulary unless wire-level YANG compatibility is required. |
| [DMTF Redfish-Publications 2026.2](https://github.com/DMTF/Redfish-Publications/tree/2026.2), release commit `4f81814e399213c9055863ddaff42791c6b3706a` | Server, chassis, component, adapter, and interface adapter mappings | Tagged repository [BSD-3-Clause license](https://github.com/DMTF/Redfish-Publications/blob/2026.2/LICENSE.md); the [Apache Software Foundation classifies BSD-3-Clause as Category A](https://www.apache.org/legal/resolved.html#category-a) | `redistributable` for files under that license; preserve the BSD copyright, conditions, and disclaimer, and check any file-level notice before copying a schema. No schema is bundled by this register entry. Do not use DMTF names to imply endorsement. |

## Excluded-source register

| Source | Reason excluded | Reconsideration requirement |
| --- | --- | --- |
| [DMTF CIM specifications and schema](https://www.dmtf.org/standards/cim) | The public DMTF terms permit attributed standards-purpose reproduction but the exact schema redistribution terms and potentially applicable RAND patent disclosures have not been cleared for this Apache-2.0 product. No CIM-derived names, definitions, schema, or mappings are part of the canonical model. | Record the exact artifact, version, copyright license, required notices, and applicable patent-disclosure review before moving it to the approved register. |
| [NIST SP 800-162 update 2](https://csrc.nist.gov/pubs/sp/800/162/upd2/final) | The August 2019 update lists non-NIST coauthors, so the generic federal-publication reuse statement does not establish rights for all of its material. No SP 800-162-derived schema, prose, or ABAC-specific model is used here. | Clear the exact intended use and third-party contributions before adding it to the approved register; otherwise retain product-original authorization semantics. |

## Acceptance rules

- A canonical entity or relationship is not standards-aligned merely because
  its name resembles a standard term. The model records the exact mapping and
  semantic differences.
- Original field names and descriptions are preferred. Copy a schema, enum,
  example, or definition only when interoperability requires it and the
  register permits redistribution.
- A standards trademark or conformance claim requires its own review. Reference
  alignment does not authorize “certified,” “compliant,” or logo use.
- A standard with RAND, unknown, field-of-use, non-commercial, or
  no-derivatives terms is excluded until the exact intended use is affirmatively
  cleared. Do not use it as an unacknowledged conceptual source.
- Every dependency, generated artifact, copied schema, and test corpus retains
  its original license notice and is included in third-party inventory.
- Links are checked before an issue becomes ready and before a release. Broken,
  private, or superseded references are replaced with stable public sources;
  the previously implemented version remains recorded for history.
- Legal or licensing uncertainty fails closed: retain the product need, record
  the rejected candidate in the excluded-source register, and derive no model
  or implementation information from it.

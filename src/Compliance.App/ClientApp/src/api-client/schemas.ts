export type Portia022A384CC13FCCBBBDACFD95B82E4D9EE161732C5FF61B3FF97C70AF11E4D94C = {
  "delivery_status": string;
  "expires_at": string | null;
} | null;

export type Portia069969C2489ADD7162ECE80D4C8B7385BA13F5931CCE303868141921126F6CBF = {
  "items": Array<{
  "tenant_id": string;
  "program_id": string;
  "risk_id": string;
  "identifier": string;
  "revision": number | string;
  "status": string;
  "owner_resolution": string;
  "content": {
  "title": string;
  "scenario": string;
  "potential_effect": string;
  "source_note": string | null;
};
  "last_changed_by_member_id": string;
  "last_changed_by_display": string;
  "last_changed_at": string;
  "last_changed_by"?: {
  "kind": string;
  "id": string;
  "display": string;
};
} | null>;
  "next_cursor": string | null;
} | null;

export type Portia0B6601B102C5751CB9E0454A861D244D753DE3A3F0BA4871D392E5D08B917A5F = {
  "program_id": string;
  "revision": number | string;
  "name": string;
  "plan": {
  "target_readiness_date": string | null;
  "target_type_i_as_of_date": string | null;
  "target_type_ii_start_date": string | null;
  "target_type_ii_end_date": string | null;
  "readiness_advisor": string | null;
  "audit_firm": string | null;
};
  "actor_member_id": string;
  "actor_display": string;
  "changed_at": string;
  "actor"?: {
  "kind": string;
  "id": string;
  "display": string;
} | null;
  "criteria_edition_id"?: string | null;
} | null;

export type Portia0B8DD226C90BCDBE54774D138909CEACAC410368E2CA21791FD1B5EC3FCB4C2D = Array<string> | null;

export type Portia0BBEEB50D9C1BAC9B12E86679F1B76D5365AB62E7358C3F3CBF8735D3BE93FDA = {
  "user_ids": Array<string>;
} | null;

export type Portia0FBE6D4CEB2CC84764DF0FFF82C76BB333FB69E323061CA548534BC3DFB78B51 = {
  "boundary_id": string;
  "draft_version_id": string;
} | null;

export type Portia1667971800402A6979C26E522A3DC756AAF0573767E53910C942723A6B2DC3D5 = {
  "scope": {
  "record_type": string;
  "record_id": string;
  "version_id": string;
  "revision": number | string;
};
  "revision": number | string;
  "conflicts": Array<{
  "kind": "self_review" | "self_approval";
  "member_id": string;
  "scope": {
  "record_type": string;
  "record_id": string;
  "version_id": string;
  "revision": number | string;
};
  "existing_assignment_id": string;
  "proposed_assignment_id": string;
  "existing_type": "control_owner" | "evidence_contributor" | "assigned_reviewer" | "access_reviewer" | "corrective_action_owner" | "policy_approver";
  "proposed_type": "control_owner" | "evidence_contributor" | "assigned_reviewer" | "access_reviewer" | "corrective_action_owner" | "policy_approver";
  "waiver_action": string;
} | null>;
} | null;

export type Portia166CE150C5FA11C2FF07F303878BE1D989853E482321CEFA07EC5F3D792CC0DF = {
  "items": Array<{
  "tenant_id": string;
  "name": string;
  "slug": string;
  "status"?: string;
  "legal_name"?: string | null;
  "operator_user_id"?: string | null;
  "requires_invitation"?: boolean;
  "requires_activation"?: boolean;
} | null>;
  "next_cursor": string | null;
} | null;

export type Portia17403E183EEEE5417F57DA6B2D792B7A88B5E631D52B05919D48C9D69FFB6C0D = {
  "items": Array<{
  "tenant_id": string;
  "program_id": string;
  "draft_id": string;
  "service_id": string;
  "kind": string;
  "identifier": string;
  "revision": number | string;
  "status": string;
  "source_resolution": string;
  "owner_resolution": string;
  "applicability_resolution": string;
  "statement": string;
  "context": string;
  "source_reference": string;
  "last_changed_by_member_id": string;
  "last_changed_by_display": string;
  "last_changed_at": string;
  "last_changed_by"?: {
  "kind": string;
  "id": string;
  "display": string;
};
} | null>;
  "next_cursor": string | null;
} | null;

export type Portia17488FFB4D3C927EDA1FEEAABE0836C927B726E447755DAE04EE2C5CFE567A82 = {
  "items": Array<{
  "tenant_id": string;
  "boundary_id": string;
  "decision_id": string;
  "version_id": string;
  "revision": number | string;
  "outcome": string;
  "actor_member_id": string;
  "actor_display": string;
  "rationale": string;
  "decided_at": string;
  "supersedes_decision_id": string | null;
  "relies_on_decision_id": string | null;
  "impact_digest": string | null;
  "separation_of_duties_waiver_id"?: string | null;
  "actor"?: {
  "kind": string;
  "id": string;
  "display": string;
};
  "separation_of_duties_waived"?: boolean;
} | null>;
  "next_cursor": string | null;
} | null;

export type Portia1D599653835771B5B38C77F6C963CA25D736248D35B45B1CD6CED58C58E70B11 = {
  "application_id": string;
} | null;

export type Portia1E5BBCD6BE4B1F969208CC6975472A2A383EBE2A1300801E1AE359E9E687516A = {
  "tenant_id": string;
  "user_id": string;
  "member_id": string;
  "paths": Array<{
  "team_id": string;
  "team_name": string;
  "role_id": string;
  "role_name": string;
  "permissions": Array<string | null>;
} | null>;
  "grant_paths": Array<{
  "grant": {
  "tenant_id": string;
  "grant_id": string;
  "terms": {
  "principal": {
  "kind": "member" | "team";
  "id": string;
};
  "role_id": string;
  "scope": {
  "kind": "organization" | "program" | "engagement" | "shared_resource";
  "id": string;
  "resource_type"?: string | null;
};
  "source": {
  "kind": string;
  "id": string;
};
  "granted_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "effective_from": string;
  "effective_until": string | null;
};
  "revoked_by": {
  "kind": string;
  "id": string;
  "display": string;
} | null;
  "revoked_at": string | null;
};
  "role_name": string;
  "permissions": Array<string | null>;
  "is_effective": boolean;
} | null>;
  "effective_permissions": Array<string | null>;
} | null;

export type Portia1E60B56A95A64AF1BB8BE85BC8B2174D1CD473A879DA9B1370C87F09E86C448F = {
  "statement": string;
  "engagement_stage": string;
  "trust_services_categories": Array<string | null>;
  "entries": Array<{
  "entry_id": string;
  "kind": string;
  "subject_type": string;
  "subject": string;
  "governed_record_id": string | null;
  "owner_reference": string;
  "rationale": string;
  "unresolved": boolean;
} | null>;
} | null;

export type Portia1EB0255AC1C1D800F4E43175A476BE2EEF9E1FBDC2D4430BCED070CC5D913FAD = {
  "team_id": string;
  "name": string;
} | null;

export type Portia293AC99442386DA2E4203C76C9DCACD9B0159C4FF4F351D4C5C91AF91E5AADA5 = {
  "tenant_id": string;
  "snapshot_id": string;
  "canonical_manifest": string;
  "content_sha256": string;
} | null;

export type Portia2D5AB432C63465374C50AF5FA1724B74CECAE9F5CB78FC5BE2C2AF45228FF552 = {
  "user_id": string;
  "email_address": string;
  "verified": boolean;
} | null;

export type Portia2D5CCE34626A84B45EE900195D49320E848D2908BBBBF8C24AF6A2EAF51BBCBA = {
  "tenant_id": string;
  "boundary_id": string;
  "decision_id": string;
  "version_id": string;
  "revision": number | string;
  "outcome": string;
  "actor_member_id": string;
  "actor_display": string;
  "rationale": string;
  "decided_at": string;
  "supersedes_decision_id": string | null;
  "relies_on_decision_id": string | null;
  "impact_digest": string | null;
  "separation_of_duties_waiver_id"?: string | null;
  "actor"?: {
  "kind": string;
  "id": string;
  "display": string;
};
  "separation_of_duties_waived"?: boolean;
} | null;

export type Portia3115C5308C1E17753EFACC8F1ED1916351FD777796131BD3E35D75C5D4E1E415 = {
  "user_id": string;
  "tenant_id": string;
  "affiliation"?: string;
  "is_suspended"?: boolean;
  "suspended_at"?: string | null;
  "suspended_by_member_id"?: string;
  "suspended_by_display"?: string | null;
  "suspension_reason"?: string | null;
  "reinstated_at"?: string | null;
  "reinstated_by_member_id"?: string;
  "reinstated_by_display"?: string | null;
} | null;

export type Portia317D1FB1FCD29A7DD0797791039C62768591F13C9FDBA864BA12B4FD00EC056C = {
  "tenant_id": string;
  "application_id": string;
  "revision": number | string;
  "name": string;
  "purpose": string;
  "owner_reference": string | null;
  "source_kind": string;
  "source_identifier": string;
  "has_system_instances": boolean;
  "unresolved": Array<string | null>;
  "last_changed_by_member_id": string;
  "last_changed_by_display": string;
  "last_changed_at": string;
  "last_changed_by"?: {
  "kind": string;
  "id": string;
  "display": string;
};
  "classification"?: string | null;
  "system_owner_person_id"?: string | null;
  "access_owner_person_id"?: string | null;
} | null;

export type Portia3AA867E8899C6C43AB80B916C3F53DC57184409D52C29C8217ABCD226DD97BB5 = {
  "tenant_id": string;
  "program_id": string;
  "draft_id": string;
  "service_id": string;
  "kind": string;
  "identifier": string;
  "revision": number | string;
  "status": string;
  "source_resolution": string;
  "owner_resolution": string;
  "applicability_resolution": string;
  "statement": string;
  "context": string;
  "source_reference": string;
  "last_changed_by_member_id": string;
  "last_changed_by_display": string;
  "last_changed_at": string;
  "last_changed_by"?: {
  "kind": string;
  "id": string;
  "display": string;
};
} | null;

export type Portia3C2330B1E721B3907D876EAF59B8BADC89F6106BC2A83EBC53F96A325C8F9D64 = {
  "tenant_id": string;
  "boundary_id": string;
  "draft_version_id": string;
  "revision": number | string;
  "approved_version_id": string | null;
  "changes": Array<{
  "field": string;
  "change_type": string;
  "entry_id": string | null;
  "previous_value": string | null;
  "proposed_value": string | null;
  "previous_entry": {
  "entry_id": string;
  "kind": string;
  "subject_type": string;
  "subject": string;
  "governed_record_id": string | null;
  "owner_reference": string;
  "rationale": string;
  "unresolved": boolean;
} | null;
  "proposed_entry": {
  "entry_id": string;
  "kind": string;
  "subject_type": string;
  "subject": string;
  "governed_record_id": string | null;
  "owner_reference": string;
  "rationale": string;
  "unresolved": boolean;
} | null;
} | null>;
  "contributions": Array<{
  "context": string;
  "records": Array<{
  "tenant_id": string;
  "context": string;
  "record_type": string;
  "record_id": string;
  "reason": string;
} | null>;
  "complete": boolean;
} | null>;
  "pending_contexts": Array<string | null>;
  "complete": boolean;
  "digest": string;
} | null;

export type Portia3C70E8DCFE6903F2FDCA0DF299E0A8C4BE8585E75798B4037723C1A3360251E4 = {
  "tenant_id": string;
  "program_id": string;
  "risk_id": string;
  "identifier": string;
  "revision": number | string;
  "content": {
  "title": string;
  "scenario": string;
  "potential_effect": string;
  "source_note": string | null;
};
  "changed_by_member_id": string;
  "changed_by_display": string;
  "changed_at": string;
  "actor"?: {
  "kind": string;
  "id": string;
  "display": string;
};
} | null;

export type Portia3F6E09D9310B082917FB4E979E225D9A959D6DE8796E3B5D244A47C587B91F82 = {
  "tenant_id": string;
  "waiver_id": string;
  "scope": {
  "record_type": string;
  "record_id": string;
  "version_id": string;
  "revision": number | string;
  "action": string;
};
  "beneficiary_member_id": string;
  "requester_member_id": string;
  "requester_display": string;
  "rationale": string;
  "requested_at": string;
  "expires_at": string;
  "approver_member_id": string | null;
  "approver_display": string | null;
  "approved_at": string | null;
  "status": string;
  "active": boolean;
  "requester"?: {
  "kind": string;
  "id": string;
  "display": string;
};
  "approver"?: {
  "kind": string;
  "id": string;
  "display": string;
} | null;
} | null;

export type Portia3FB59D2954C0596502DAAFC4D857D1FECF2F3D8E1E967CE98B8A0A7D8B5A8D6F = Array<{
  "edition_id": string;
  "framework": string;
  "edition_label": string;
  "published_at": string;
  "is_complete": boolean;
  "coverage_note": string;
  "source_url": string;
  "content_rights": string;
  "support_gaps": Array<{
  "category": string;
  "code": string;
  "note": string;
} | null>;
} | null> | null;

export type Portia411B3239057A525C3562E4BE0D1A71F30A3844A0A56231069471136CDD4A37E4 = {
  "items": Array<{
  "role_id": string;
  "name": string;
} | null>;
  "next_cursor": string | null;
} | null;

export type Portia42415CD397320D0C15EC3283D7AB52C15BCC5C24CB26307A7BCA7CB15F1D7ABF = {
  "items": Array<{
  "tenant_id": string;
  "batch_id": string;
  "row_id": string;
  "row_number": number | string;
  "source_record_id": string | null;
  "name": string | null;
  "purpose": string | null;
  "owner_reference": string | null;
  "validation_findings": Array<string | null>;
  "match_state": string;
  "candidate_application_ids": Array<string>;
  "changed_fields": Array<string | null>;
  "acceptance_blockers": Array<string | null>;
} | null>;
  "next_cursor": string | null;
} | null;

export type Portia444702261363EF785C20C78504EBD948CC2950CA076C0FEA93FBD650F83E4680 = {
  "role_id": string;
  "name": string;
} | null;

export type Portia445823D45DEBC21876B8F7D21A7706F96E8DB18D68A39E931F58C101C313F09E = {
  "items": Array<{
  "role_id": string;
  "team_id": string;
} | null>;
  "next_cursor": string | null;
} | null;

export type Portia4D2B2E7846C05C249F068D80B37B37F2C75C5ADD864571B26E4DF5A345F9CD19 = {
  "items": Array<{
  "tenant_id": string;
  "program_id": string;
  "control_id": string;
  "identifier": string;
  "revision": number | string;
  "status": string;
  "owner_resolution": string;
  "applicability_resolution": string;
  "content": {
  "title": string;
  "objective": string;
  "description": string;
  "implementation_narrative": string;
  "expected_evidence_descriptions": Array<string | null>;
  "owner_reference"?: string | null;
  "applicability"?: Array<{
  "entry_id": string;
  "subject_type": string;
  "subject": string;
  "governed_record_id": string | null;
  "rationale": string;
  "unresolved": boolean;
} | null> | null;
};
  "last_changed_by_member_id": string;
  "last_changed_by_display": string;
  "last_changed_at": string;
  "last_changed_by"?: {
  "kind": string;
  "id": string;
  "display": string;
};
} | null>;
  "next_cursor": string | null;
} | null;

export type Portia4EFE49B1348AFC5BA81DF069FB9A0B89E1FBF2AD30994145DEF20BA105EBDEBE = {
  "tenant_id": string;
  "boundary_id": string;
  "program_id": string;
  "version_id": string;
  "revision": number | string;
  "content": {
  "statement": string;
  "engagement_stage": string;
  "trust_services_categories": Array<string | null>;
  "entries": Array<{
  "entry_id": string;
  "kind": string;
  "subject_type": string;
  "subject": string;
  "governed_record_id": string | null;
  "owner_reference": string;
  "rationale": string;
  "unresolved": boolean;
} | null>;
};
  "status": string;
  "effective_from": string | null;
  "author_member_id": string;
  "author_display": string;
  "changed_at": string;
} | null;

export type Portia4FFF06F1347F0DF9A617B1AA79C57C4BE787E4DE34BC7EED6A0E8052C8F5EF7C = {
  "edition_id": string;
  "identifier": string;
  "source_identifier": string | null;
  "category": string;
  "kind": string;
  "parent_identifier": string | null;
  "summary": string;
} | null;

export type Portia533F78EAEB8819DE62744BA4544C862D58319F328039FE16CF9904D0F81B6726 = {
  "tenant_id": string;
  "name": string;
  "slug": string;
  "status"?: string;
  "legal_name"?: string | null;
  "operator_user_id"?: string | null;
  "requires_invitation"?: boolean;
  "requires_activation"?: boolean;
} | null;

export type Portia534841146AA01FC616B441EB6CC816411AE29E916AB93C8329D97EB96B560E0A = {
  "items": Array<{
  "team_id": string;
  "member_id": string;
} | null>;
  "next_cursor": string | null;
} | null;

export type Portia559C6B2CB453C53C337F5E8AA18127BB374E6308F310A0A099A30C3421B412A3 = {
  "items": Array<{
  "program_id": string;
  "revision": number | string;
  "name": string;
  "plan": {
  "target_readiness_date": string | null;
  "target_type_i_as_of_date": string | null;
  "target_type_ii_start_date": string | null;
  "target_type_ii_end_date": string | null;
  "readiness_advisor": string | null;
  "audit_firm": string | null;
};
  "actor_member_id": string;
  "actor_display": string;
  "changed_at": string;
  "actor"?: {
  "kind": string;
  "id": string;
  "display": string;
} | null;
  "criteria_edition_id"?: string | null;
} | null>;
  "next_cursor": string | null;
} | null;

export type Portia5647C83C9923AF4B8A5A60D0E0D9CB27904F9C4571DEAE7F66B50E1FACD98453 = {
  "edition_id": string;
  "framework": string;
  "edition_label": string;
  "published_at": string;
  "is_complete": boolean;
  "coverage_note": string;
  "source_url": string;
  "content_rights": string;
  "support_gaps": Array<{
  "category": string;
  "code": string;
  "note": string;
} | null>;
} | null;

export type Portia583B4F63A28210D35E65C0C2C3B23C1B843FF040116C8ED76F096FDA3FCE3137 = {
  "items": Array<{
  "tenant_id": string;
  "name": string;
  "slug": string;
  "status"?: string;
} | null>;
  "next_cursor": string | null;
} | null;

export type Portia5AF07B3208EB9275DF00078FDEF65642C66304D924A78125731567AACD08E2A4 = {
  "tenant_id": string;
  "program_id": string;
  "program_revision": number | string;
  "items": Array<{
  "code": string;
  "detail": string;
  "source_type": string;
  "source_id": string | null;
} | null>;
  "next_boundary_cursor": string | null;
} | null;

export type Portia5D9B3D88A76CFA247FEA271A78CAAC133EF00DBEBE6B5C8AC9431402F338F317 = {
  "tenant_id": string;
  "program_id": string;
  "draft_id": string;
  "service_id": string;
  "kind": string;
  "identifier": string;
  "revision": number | string;
  "statement": string;
  "context": string;
  "source_reference": string;
  "changed_by_member_id": string;
  "changed_by_display": string;
  "changed_at": string;
  "actor"?: {
  "kind": string;
  "id": string;
  "display": string;
};
} | null;

export type Portia5DAD64FAA90520CAB8D26EA34171BA23DCFCC7C27E8F9E4C62DB581CD28EEC56 = {
  "items": Array<{
  "tenant_id": string;
  "application_id": string;
  "revision": number | string;
  "name": string;
  "purpose": string;
  "owner_reference": string | null;
  "source_kind": string;
  "source_identifier": string;
  "has_system_instances": boolean;
  "unresolved": Array<string | null>;
  "last_changed_by_member_id": string;
  "last_changed_by_display": string;
  "last_changed_at": string;
  "last_changed_by"?: {
  "kind": string;
  "id": string;
  "display": string;
};
  "classification"?: string | null;
  "system_owner_person_id"?: string | null;
  "access_owner_person_id"?: string | null;
} | null>;
  "next_cursor": string | null;
} | null;

export type Portia5DC40A4F1CB15D96F9110C5B5E53AA99EDE859D42D78207E7C39CA076C05FE94 = {
  "tenant_id": string;
  "slug": string;
} | null;

export type Portia6AE7436CAF1D81D68D1CA26BCFADC44860AAA63B87A822CFB2212F1AE5375C3A = {
  "tenant_id": string;
  "current_slug": string;
  "redirect": boolean;
} | null;

export type Portia6EDDE774B86AAF1F68AF85D567C349F34299BBD542CD401970A9950506CB9FB1 = {
  "tenant_id": string;
  "person_id": string;
  "revision": number | string;
  "display_name": string;
  "work_email": string | null;
  "source_kind": string;
  "last_changed_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "last_changed_at": string;
} | null;

export type Portia6EDEFDF3147D5333AAABFE7768FCC99D88F93C8F7C81708F0C551ABD6FD7A117 = {
  "record_type": string;
  "record_id": string;
  "version_id": string;
  "revision": number | string;
  "action": string;
} | null;

export type Portia70B24E90A73EA0794A4DD21F876262A340BEE644ACBC9B8FF2F0890A79B9D474 = {
  "user_identity_id": string;
  "user_id": string;
  "email_address": string | null;
} | null;

export type Portia757E16DD9071C1EA6CD19C09CEF1B1E1BC16F3C168A35CA627AC8EFC9996C48D = {
  "items": Array<{
  "tenant_id": string;
  "snapshot_id": string;
  "root_snapshot_id": string;
  "amends_snapshot_id": string | null;
  "program_id": string;
  "kind": string;
  "revision": number | string;
  "manifest": {
  "format_version": number | string;
  "tenant_id": string;
  "program_id": string;
  "program_revision": number | string;
  "program_content_sha256": string;
  "boundary_id": string;
  "approved_boundary_version_id": string;
  "boundary_content_sha256": string;
};
  "canonical_manifest": string;
  "content_sha256": string;
  "amendment_reason": string | null;
  "actor_member_id": string;
  "actor_display": string;
  "frozen_at": string;
} | null>;
  "next_cursor": string | null;
} | null;

export type Portia75DB7E91559424F7B5B78D00E6D5C15DE7D087132C05E6E4E37BD168D7C10902 = {
  "tenant_id": string;
  "program_id": string;
  "name": string;
  "stage": string;
  "next_stage": string | null;
  "revision": number | string;
  "plan": {
  "target_readiness_date": string | null;
  "target_type_i_as_of_date": string | null;
  "target_type_ii_start_date": string | null;
  "target_type_ii_end_date": string | null;
  "readiness_advisor": string | null;
  "audit_firm": string | null;
};
  "last_changed_by_member_id": string;
  "last_changed_by_display": string;
  "last_changed_at": string;
  "stage_plan": Array<{
  "stage": string;
  "advance_when": string;
} | null>;
  "last_changed_by"?: {
  "kind": string;
  "id": string;
  "display": string;
} | null;
  "criteria_edition_id"?: string | null;
} | null;

export type Portia7B406DA21A6EBDB3B0EC40C4DEED4FE06FB99131586B6D1C6E24E83E42204F64 = {
  "tenant_id": string;
  "boundary_id": string;
  "program_id": string;
  "draft": {
  "tenant_id": string;
  "boundary_id": string;
  "program_id": string;
  "version_id": string;
  "revision": number | string;
  "content": {
  "statement": string;
  "engagement_stage": string;
  "trust_services_categories": Array<string | null>;
  "entries": Array<{
  "entry_id": string;
  "kind": string;
  "subject_type": string;
  "subject": string;
  "governed_record_id": string | null;
  "owner_reference": string;
  "rationale": string;
  "unresolved": boolean;
} | null>;
};
  "status": string;
  "effective_from": string | null;
  "author_member_id": string;
  "author_display": string;
  "changed_at": string;
} | null;
  "latest_approved_version": {
  "tenant_id": string;
  "boundary_id": string;
  "program_id": string;
  "version_id": string;
  "revision": number | string;
  "content": Portia7B406DA21A6EBDB3B0EC40C4DEED4FE06FB99131586B6D1C6E24E83E42204F641;
  "status": string;
  "effective_from": string | null;
  "author_member_id": string;
  "author_display": string;
  "changed_at": string;
} | null;
  "latest_decision": {
  "tenant_id": string;
  "boundary_id": string;
  "decision_id": string;
  "version_id": string;
  "revision": number | string;
  "outcome": string;
  "actor_member_id": string;
  "actor_display": string;
  "rationale": string;
  "decided_at": string;
  "supersedes_decision_id": string | null;
  "relies_on_decision_id": string | null;
  "impact_digest": string | null;
  "separation_of_duties_waiver_id"?: string | null;
  "actor"?: {
  "kind": string;
  "id": string;
  "display": string;
};
  "separation_of_duties_waived"?: boolean;
} | null;
  "revision"?: number | string;
} | null;

export type Portia7B406DA21A6EBDB3B0EC40C4DEED4FE06FB99131586B6D1C6E24E83E42204F641 = {
  "statement": string;
  "engagement_stage": string;
  "trust_services_categories": Array<string | null>;
  "entries": Array<{
  "entry_id": string;
  "kind": string;
  "subject_type": string;
  "subject": string;
  "governed_record_id": string | null;
  "owner_reference": string;
  "rationale": string;
  "unresolved": boolean;
} | null>;
};

export type Portia7B41C47C738A8DA20460C8A8BE569630949FEA19EAB61C34DCBC49E83CDD123C = {
  "items": Array<{
  "tenant_id": string;
  "program_id": string;
  "draft_id": string;
  "service_id": string;
  "kind": string;
  "identifier": string;
  "revision": number | string;
  "statement": string;
  "context": string;
  "source_reference": string;
  "changed_by_member_id": string;
  "changed_by_display": string;
  "changed_at": string;
  "actor"?: {
  "kind": string;
  "id": string;
  "display": string;
};
} | null>;
  "next_cursor": string | null;
} | null;

export type Portia7F8B4C9203507BBF4A214CB378B625DDB003E856C25F722EAE639A78025702C1 = {
  "program_id": string;
} | null;

export type Portia8165C4E0E3F218936EE79C52D55D60B2229FE25D669BF8BC3124D4D9E388B010 = {
  "service_id": string;
  "revision": number | string;
  "name": string;
  "purpose": string;
  "owner_reference": string;
  "status": string;
  "retirement_rationale": string | null;
  "actor_member_id": string;
  "actor_display": string;
  "changed_at": string;
  "program_id"?: string | null;
  "actor"?: {
  "kind": string;
  "id": string;
  "display": string;
} | null;
} | null;

export type Portia84715FA1F92BA1E2F3F5CB5B59532DE980DF31373358E5412185F988E9D5FE48 = {
  "tenant_id": string;
  "revision": number | string;
  "grants": Array<{
  "tenant_id": string;
  "grant_id": string;
  "terms": {
  "principal": {
  "kind": "member" | "team";
  "id": string;
};
  "role_id": string;
  "scope": {
  "kind": "organization" | "program" | "engagement" | "shared_resource";
  "id": string;
  "resource_type"?: string | null;
};
  "source": {
  "kind": string;
  "id": string;
};
  "granted_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "effective_from": string;
  "effective_until": string | null;
};
  "revoked_by": {
  "kind": string;
  "id": string;
  "display": string;
} | null;
  "revoked_at": string | null;
} | null>;
} | null;

export type Portia893477255AAE0AA0AD6E0DCBE3CE082174CE5BCEBAB798A5A9AE84E4D5B70F2C = {
  "items": Array<{
  "tenant_id": string;
  "program_id": string;
  "name": string;
  "stage": string;
  "next_stage": string | null;
  "revision": number | string;
  "plan": {
  "target_readiness_date": string | null;
  "target_type_i_as_of_date": string | null;
  "target_type_ii_start_date": string | null;
  "target_type_ii_end_date": string | null;
  "readiness_advisor": string | null;
  "audit_firm": string | null;
};
  "last_changed_by_member_id": string;
  "last_changed_by_display": string;
  "last_changed_at": string;
  "stage_plan": Array<{
  "stage": string;
  "advance_when": string;
} | null>;
  "last_changed_by"?: {
  "kind": string;
  "id": string;
  "display": string;
} | null;
  "criteria_edition_id"?: string | null;
} | null>;
  "next_cursor": string | null;
} | null;

export type Portia8E394EAF88AA2A7C8FFD2FC3F0EF138F753B34F45673B6225474AFED0CB59A46 = {
  "tenant_id": string;
  "program_id": string;
  "control_id": string;
  "identifier": string;
  "revision": number | string;
  "status": string;
  "owner_resolution": string;
  "applicability_resolution": string;
  "content": {
  "title": string;
  "objective": string;
  "description": string;
  "implementation_narrative": string;
  "expected_evidence_descriptions": Array<string | null>;
  "owner_reference"?: string | null;
  "applicability"?: Array<{
  "entry_id": string;
  "subject_type": string;
  "subject": string;
  "governed_record_id": string | null;
  "rationale": string;
  "unresolved": boolean;
} | null> | null;
};
  "last_changed_by_member_id": string;
  "last_changed_by_display": string;
  "last_changed_at": string;
  "last_changed_by"?: {
  "kind": string;
  "id": string;
  "display": string;
};
} | null;

export type Portia96294E0FB33C6297223052ED7387F5E7FAA0C2A7CD4DC2CD6B9BFA4998A6D5C7 = Array<{
  "tenant_id": string;
  "assignment_id": string;
  "member_id": string;
  "type": "control_owner" | "evidence_contributor" | "assigned_reviewer" | "access_reviewer" | "corrective_action_owner" | "policy_approver";
  "scope": {
  "record_type": string;
  "record_id": string;
  "version_id": string;
  "revision": number | string;
};
  "assigned_at": string;
  "assigned_by_member_id": string;
  "effective_from": string;
  "effective_until": string | null;
  "revoked_at": string | null;
  "revoked_by_member_id": string;
  "separation_of_duties_waiver_ids": Array<string>;
  "assigned_by_display"?: string;
  "revoked_by_display"?: string | null;
  "revocation_reason"?: string | null;
} | null> | null;

export type Portia9890D4629429B264BE737520EC815DFE7400C9A6ECC4B4451A28AF5E22133782 = {
  "tenant_id": string;
  "snapshot_id": string;
  "verified": boolean;
  "snapshot": {
  "status": string;
  "expected_content_sha256": string;
  "observed_content_sha256": string | null;
};
  "program_revision": {
  "status": string;
  "expected_content_sha256": string;
  "observed_content_sha256": string | null;
};
  "approved_boundary_version": {
  "status": string;
  "expected_content_sha256": string;
  "observed_content_sha256": string | null;
};
} | null;

export type Portia99A90254D5BFDBDC157B8B5C8D8360DE08AAA6B5160C5F1020B01CBABB085F66 = {
  "items": Array<{
  "tenant_id": string;
  "boundary_id": string;
  "program_id": string;
  "version_id": string;
  "revision": number | string;
  "content": {
  "statement": string;
  "engagement_stage": string;
  "trust_services_categories": Array<string | null>;
  "entries": Array<{
  "entry_id": string;
  "kind": string;
  "subject_type": string;
  "subject": string;
  "governed_record_id": string | null;
  "owner_reference": string;
  "rationale": string;
  "unresolved": boolean;
} | null>;
};
  "status": string;
  "effective_from": string | null;
  "author_member_id": string;
  "author_display": string;
  "changed_at": string;
} | null>;
  "next_cursor": string | null;
} | null;

export type Portia9CA30FFC559321862CA47D0242119C614DCEC0E1E9443BE7873F142DB785415F = {
  "target_readiness_date": string | null;
  "target_type_i_as_of_date": string | null;
  "target_type_ii_start_date": string | null;
  "target_type_ii_end_date": string | null;
  "readiness_advisor": string | null;
  "audit_firm": string | null;
} | null;

export type PortiaA061B71D56F46AF68311A2B1F0B839D542E35F3F14FA3469112AC9DBB2C8EF35 = {
  "items": Array<{
  "tenant_id": string;
  "application_id": string;
  "revision": number | string;
  "name": string;
  "purpose": string;
  "owner_reference": string | null;
  "source_kind": string;
  "source_identifier": string;
  "has_system_instances": boolean;
  "unresolved": Array<string | null>;
  "last_changed_by_member_id": string;
  "last_changed_by_display": string;
  "last_changed_at": string;
  "change_kind": string;
  "system_instance_id": string | null;
  "system_instance": {
  "tenant_id": string;
  "application_id": string;
  "system_instance_id": string;
  "name": string;
  "kind": string;
  "access_boundary_reference": string | null;
  "source_kind": string;
  "source_identifier": string | null;
  "unresolved": Array<string | null>;
  "declared_by_member_id": string;
  "declared_by_display": string;
  "declared_at": string;
  "declared_by"?: {
  "kind": string;
  "id": string;
  "display": string;
};
  "revision"?: number | string;
  "legacy_application_revision"?: number | string | null;
} | null;
  "actor"?: {
  "kind": string;
  "id": string;
  "display": string;
};
  "classification"?: string | null;
  "system_owner_person_id"?: string | null;
  "access_owner_person_id"?: string | null;
} | null>;
  "next_cursor": string | null;
} | null;

export type PortiaA4299F97F2F2793A334427A6DBE3AB10187D44660E16D667981207CF14AD4341 = {
  "person_id": string;
} | null;

export type PortiaA6B51F7C7F08B507C488A94D38D27F9FD35725E3CB368F3DB31BA25BF1CBAE64 = {
  "batch_id": string;
  "revision": number | string;
  "content_sha256": string;
} | null;

export type PortiaAC26148312D1D19C14B10EE61B64A0F265378CB38284D4E50CE0C3D7153A7508 = {
  "items": Array<{
  "tenant_id": string;
  "service_id": string;
  "revision": number | string;
  "name": string;
  "purpose": string;
  "owner_reference": string;
  "status": string;
  "last_changed_by_member_id": string;
  "last_changed_by_display": string;
  "last_changed_at": string;
  "program_id"?: string | null;
  "last_changed_by"?: {
  "kind": string;
  "id": string;
  "display": string;
} | null;
} | null>;
  "next_cursor": string | null;
} | null;

export type PortiaACB074BCE785D34D94E14D208E190145EA345C44039DCEA40D390A0B5B303A9C = {
  "tenant_id": string;
  "set_id": string;
  "scope": {
  "record_type": string;
  "record_id": string;
  "version_id": string;
  "revision": number | string;
};
  "revision": number | string;
  "assignments": Array<{
  "tenant_id": string;
  "assignment_id": string;
  "member_id": string;
  "type": "control_owner" | "evidence_contributor" | "assigned_reviewer" | "access_reviewer" | "corrective_action_owner" | "policy_approver";
  "scope": {
  "record_type": string;
  "record_id": string;
  "version_id": string;
  "revision": number | string;
};
  "assigned_at": string;
  "assigned_by_member_id": string;
  "effective_from": string;
  "effective_until": string | null;
  "revoked_at": string | null;
  "revoked_by_member_id": string;
  "separation_of_duties_waiver_ids": Array<string>;
  "assigned_by_display"?: string;
  "revoked_by_display"?: string | null;
  "revocation_reason"?: string | null;
} | null>;
} | null;

export type PortiaAD1A36FB0E7DA3A7661197E93F9C3FAC072DFA215C0D19CDBA14BC399E425EC3 = {
  "snapshot_id": string;
  "content_sha256": string;
} | null;

export type PortiaAFAA47FF36B14A9CA4F142278676DAA600886F20FA43D1A1F114283D4EE01219 = {
  "items": Array<{
  "tenant_id": string;
  "program_id": string;
  "control_id": string;
  "identifier": string;
  "revision": number | string;
  "content": {
  "title": string;
  "objective": string;
  "description": string;
  "implementation_narrative": string;
  "expected_evidence_descriptions": Array<string | null>;
  "owner_reference"?: string | null;
  "applicability"?: Array<{
  "entry_id": string;
  "subject_type": string;
  "subject": string;
  "governed_record_id": string | null;
  "rationale": string;
  "unresolved": boolean;
} | null> | null;
};
  "changed_by_member_id": string;
  "changed_by_display": string;
  "changed_at": string;
  "actor"?: {
  "kind": string;
  "id": string;
  "display": string;
};
} | null>;
  "next_cursor": string | null;
} | null;

export type PortiaB40D0675C1B674F43BFBDCE25F20837F7CF28C29B2067DF1534D10FA9DE94F05 = {
  "draft_id": string;
  "kind": string;
  "identifier": string;
  "revision": number | string;
} | null;

export type PortiaB4F7BAA8B99F52AE6970F7ACF0928494556285076363E5938D8AA4FF64C9EC95 = {
  "tenant_id": string;
  "application_id": string;
  "revision": number | string;
  "name": string;
  "purpose": string;
  "owner_reference": string | null;
  "source_kind": string;
  "source_identifier": string;
  "has_system_instances": boolean;
  "unresolved": Array<string | null>;
  "last_changed_by_member_id": string;
  "last_changed_by_display": string;
  "last_changed_at": string;
  "change_kind": string;
  "system_instance_id": string | null;
  "system_instance": {
  "tenant_id": string;
  "application_id": string;
  "system_instance_id": string;
  "name": string;
  "kind": string;
  "access_boundary_reference": string | null;
  "source_kind": string;
  "source_identifier": string | null;
  "unresolved": Array<string | null>;
  "declared_by_member_id": string;
  "declared_by_display": string;
  "declared_at": string;
  "declared_by"?: {
  "kind": string;
  "id": string;
  "display": string;
};
  "revision"?: number | string;
  "legacy_application_revision"?: number | string | null;
} | null;
  "actor"?: {
  "kind": string;
  "id": string;
  "display": string;
};
  "classification"?: string | null;
  "system_owner_person_id"?: string | null;
  "access_owner_person_id"?: string | null;
} | null;

export type PortiaB5B4C6C1CCCDD997C7F390CEAD0680DD868403FA1B09E115245EBB04DFD8F886 = {
  "risk_id": string;
  "identifier": string;
  "revision": number | string;
} | null;

export type PortiaBDB3727E11513755EF2C20FC36CA203734F5F1EAB005F72399870A161757C2FE = {
  "items": Array<{
  "user_id": string;
  "tenant_id": string;
  "affiliation"?: string;
  "is_suspended"?: boolean;
  "suspended_at"?: string | null;
  "suspended_by_member_id"?: string;
  "suspended_by_display"?: string | null;
  "suspension_reason"?: string | null;
  "reinstated_at"?: string | null;
  "reinstated_by_member_id"?: string;
  "reinstated_by_display"?: string | null;
} | null>;
  "next_cursor": string | null;
} | null;

export type PortiaBE1636B313652DA76C47508EB54DDB2BC0EB3E2F94DD70683296AF4B4AC9B979 = {
  "items": Array<{
  "tenant_id": string;
  "program_id": string;
  "risk_id": string;
  "identifier": string;
  "revision": number | string;
  "content": {
  "title": string;
  "scenario": string;
  "potential_effect": string;
  "source_note": string | null;
};
  "changed_by_member_id": string;
  "changed_by_display": string;
  "changed_at": string;
  "actor"?: {
  "kind": string;
  "id": string;
  "display": string;
};
} | null>;
  "next_cursor": string | null;
} | null;

export type PortiaBE625002347E2BBF2D03CC7136B2D484C2DA3FD77DB41B321C10C3D110B761D3 = {
  "system_instance_id": string;
} | null;

export type PortiaC28D3107D1A3216DD3490F3FFBA111479B8B479E23A4FAFFD1222C5EF6FF1520 = {
  "title": string;
  "objective": string;
  "description": string;
  "implementation_narrative": string;
  "expected_evidence_descriptions": Array<string | null>;
  "owner_reference"?: string | null;
  "applicability"?: Array<{
  "entry_id": string;
  "subject_type": string;
  "subject": string;
  "governed_record_id": string | null;
  "rationale": string;
  "unresolved": boolean;
} | null> | null;
} | null;

export type PortiaC3C24AD50D924F9A1F4D0C270B605BEB0F1317AF1B9B674857E2967845D5EABE = {
  "items": Array<{
  "tenant_id": string;
  "email_address": string;
  "affiliation": string;
  "administrator": boolean;
  "built_in_role": string | null;
  "status": string;
  "expires_at": string;
  "invited_by": string;
  "accepted_user_id": string | null;
  "delivery_status"?: string;
} | null>;
  "next_cursor": string | null;
} | null;

export type PortiaC695C5A9B781750DCBCA504BA9AFE97AFE550E8F63FAF6ADE2DFE5B2E157C781 = Array<{
  "source_record_id": string | null;
  "name": string | null;
  "purpose": string | null;
  "owner_reference": string | null;
} | null> | null;

export type PortiaC7557948A66C1C9A063FD76DD42EB5D78FE6546680A69290B47D592328A411EC = {
  "items": Array<{
  "tenant_id": string;
  "person_id": string;
  "revision": number | string;
  "display_name": string;
  "work_email": string | null;
  "source_kind": string;
  "last_changed_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "last_changed_at": string;
} | null>;
  "next_cursor": string | null;
} | null;

export type PortiaCADDBB36C737CF14F2057C0A4E15C9E63FDD811D019E6BEFA5647B9E2BA9E2C8 = {
  "service_id": string;
} | null;

export type PortiaCCB8911E2FB70686013446C9D524FAAFA073B59022AB0B13FB7788B463ACDDE8 = {
  "items": Array<{
  "service_id": string;
  "revision": number | string;
  "name": string;
  "purpose": string;
  "owner_reference": string;
  "status": string;
  "retirement_rationale": string | null;
  "actor_member_id": string;
  "actor_display": string;
  "changed_at": string;
  "program_id"?: string | null;
  "actor"?: {
  "kind": string;
  "id": string;
  "display": string;
} | null;
} | null>;
  "next_cursor": string | null;
} | null;

export type PortiaCE291342413ACA5E55E75E29F7C92CAA822AC0B201F64DA7A38365474F0782CD = {
  "control_id": string;
  "identifier": string;
  "revision": number | string;
} | null;

export type PortiaD493FB86F4D080CAC6A555893E11E63CE55AFFCBD6E771A8684B58B7C9536582 = {
  "principal": {
  "kind": "member" | "team";
  "id": string;
};
  "role_id": string;
  "scope": {
  "kind": "organization" | "program" | "engagement" | "shared_resource";
  "id": string;
  "resource_type"?: string | null;
};
  "source": {
  "kind": string;
  "id": string;
};
  "effective_from": string;
  "effective_until": string | null;
} | null;

export type PortiaD56D35AC620E2A97A9B2A59A435078B6568941B2238932A20E4EABB153C1309C = {
  "items": Array<{
  "tenant_id": string;
  "batch_id": string;
  "row_id": string;
  "row_number": number | string;
  "source_record_id": string | null;
  "name": string | null;
  "purpose": string | null;
  "owner_reference": string | null;
  "validation_findings": Array<string | null>;
  "processing_state": string;
  "application_id": string | null;
} | null>;
  "next_cursor": string | null;
} | null;

export type PortiaD775A5D8BCBA1B953633C0582AECCE997439EE8D2C3F4CB8CF37FA9D5E33A873 = {
  "items": Array<{
  "tenant_id": string;
  "boundary_id": string;
  "program_id": string;
  "draft": {
  "tenant_id": string;
  "boundary_id": string;
  "program_id": string;
  "version_id": string;
  "revision": number | string;
  "content": {
  "statement": string;
  "engagement_stage": string;
  "trust_services_categories": Array<string | null>;
  "entries": Array<{
  "entry_id": string;
  "kind": string;
  "subject_type": string;
  "subject": string;
  "governed_record_id": string | null;
  "owner_reference": string;
  "rationale": string;
  "unresolved": boolean;
} | null>;
};
  "status": string;
  "effective_from": string | null;
  "author_member_id": string;
  "author_display": string;
  "changed_at": string;
} | null;
  "latest_approved_version": {
  "tenant_id": string;
  "boundary_id": string;
  "program_id": string;
  "version_id": string;
  "revision": number | string;
  "content": PortiaD775A5D8BCBA1B953633C0582AECCE997439EE8D2C3F4CB8CF37FA9D5E33A8731;
  "status": string;
  "effective_from": string | null;
  "author_member_id": string;
  "author_display": string;
  "changed_at": string;
} | null;
  "latest_decision": {
  "tenant_id": string;
  "boundary_id": string;
  "decision_id": string;
  "version_id": string;
  "revision": number | string;
  "outcome": string;
  "actor_member_id": string;
  "actor_display": string;
  "rationale": string;
  "decided_at": string;
  "supersedes_decision_id": string | null;
  "relies_on_decision_id": string | null;
  "impact_digest": string | null;
  "separation_of_duties_waiver_id"?: string | null;
  "actor"?: {
  "kind": string;
  "id": string;
  "display": string;
};
  "separation_of_duties_waived"?: boolean;
} | null;
  "revision"?: number | string;
} | null>;
  "next_cursor": string | null;
} | null;

export type PortiaD775A5D8BCBA1B953633C0582AECCE997439EE8D2C3F4CB8CF37FA9D5E33A8731 = {
  "statement": string;
  "engagement_stage": string;
  "trust_services_categories": Array<string | null>;
  "entries": Array<{
  "entry_id": string;
  "kind": string;
  "subject_type": string;
  "subject": string;
  "governed_record_id": string | null;
  "owner_reference": string;
  "rationale": string;
  "unresolved": boolean;
} | null>;
};

export type PortiaDC527050B61B249601EF0A860E162A0EF79BEEECC8578F3452072A407BA6A93F = {
  "tenant_id": string;
  "program_id": string;
  "control_id": string;
  "identifier": string;
  "revision": number | string;
  "content": {
  "title": string;
  "objective": string;
  "description": string;
  "implementation_narrative": string;
  "expected_evidence_descriptions": Array<string | null>;
  "owner_reference"?: string | null;
  "applicability"?: Array<{
  "entry_id": string;
  "subject_type": string;
  "subject": string;
  "governed_record_id": string | null;
  "rationale": string;
  "unresolved": boolean;
} | null> | null;
};
  "changed_by_member_id": string;
  "changed_by_display": string;
  "changed_at": string;
  "actor"?: {
  "kind": string;
  "id": string;
  "display": string;
};
} | null;

export type PortiaDEDCF320D66371B8A514AC43F05EF3AFD0C2C09B2E6784A1FAA3AB8ADC9C402D = {
  "items": Array<{
  "tenant_id": string;
  "application_id": string;
  "system_instance_id": string;
  "name": string;
  "kind": string;
  "access_boundary_reference": string | null;
  "source_kind": string;
  "source_identifier": string | null;
  "unresolved": Array<string | null>;
  "declared_by_member_id": string;
  "declared_by_display": string;
  "declared_at": string;
  "declared_by"?: {
  "kind": string;
  "id": string;
  "display": string;
};
  "revision"?: number | string;
  "legacy_application_revision"?: number | string | null;
} | null>;
  "next_cursor": string | null;
} | null;

export type PortiaDF9CF8F410540161933C37D2952A4754D8E575F091E0F87B7AFD038C972A5738 = {
  "items": Array<{
  "edition_id": string;
  "identifier": string;
  "source_identifier": string | null;
  "category": string;
  "kind": string;
  "parent_identifier": string | null;
  "summary": string;
} | null>;
  "next_cursor": string | null;
} | null;

export type PortiaE2F70B587A599F2A92FE1BC5C9941D3194A8ABA84334EB2D1BFDB23F21AF2C00 = {
  "tenant_id": string;
  "snapshot_id": string;
  "root_snapshot_id": string;
  "amends_snapshot_id": string | null;
  "program_id": string;
  "kind": string;
  "revision": number | string;
  "manifest": {
  "format_version": number | string;
  "tenant_id": string;
  "program_id": string;
  "program_revision": number | string;
  "program_content_sha256": string;
  "boundary_id": string;
  "approved_boundary_version_id": string;
  "boundary_content_sha256": string;
};
  "canonical_manifest": string;
  "content_sha256": string;
  "amendment_reason": string | null;
  "actor_member_id": string;
  "actor_display": string;
  "frozen_at": string;
} | null;

export type PortiaEA6D5265BAC64B7B7ECBB403179F0802AEF5CDBFF6D32E0FCDF3EF54CB637D54 = {
  "items": Array<{
  "team_id": string;
  "name": string;
} | null>;
  "next_cursor": string | null;
} | null;

export type PortiaEC8A68FE610BE733B5FB5E10D0C0A79743573CEEBB9114A5BBD9B4542987EC3B = {
  "items": Array<{
  "role_id": string;
  "permission": string;
} | null>;
  "next_cursor": string | null;
} | null;

export type PortiaED1DF65949C6DF6D7D9CCF63B9EE97A054381BD875B459C20A7BD5339B9C7626 = {
  "items": Array<{
  "tenant_id": string;
  "subject_type": string;
  "governed_record_id": string;
  "boundary_id": string;
  "program_id": string;
  "version_id": string;
  "entry_id": string;
  "revision": number | string;
  "status": string;
  "effective_from": string | null;
  "kind": string;
  "subject": string;
  "owner_reference": string;
  "rationale": string;
} | null>;
  "next_cursor": string | null;
} | null;

export type PortiaED3F7B7FEBE1F20B8D98DAE3ABE7516820F289A5DFA781A7229620B1EC2C9BF7 = {
  "tenant_id": string;
  "service_id": string;
  "revision": number | string;
  "name": string;
  "purpose": string;
  "owner_reference": string;
  "status": string;
  "last_changed_by_member_id": string;
  "last_changed_by_display": string;
  "last_changed_at": string;
  "program_id"?: string | null;
  "last_changed_by"?: {
  "kind": string;
  "id": string;
  "display": string;
} | null;
} | null;

export type PortiaEED978F2141A5821BB72161402A02FDBDB787544E177B8457CBD97CE06CEB5F3 = {
  "tenant_id": string;
  "batch_id": string;
  "submission_id": string;
  "source_key": string;
  "source_namespace": string;
  "coverage": string;
  "content_sha256": string;
  "revision": number | string;
  "state": string;
  "submitted_by_member_id": string;
  "submitted_by_display": string;
  "submitted_at": string;
  "row_count": number | string;
  "invalid_count": number | string;
  "pending_count": number | string;
  "applied_count": number | string;
  "skipped_count": number | string;
  "failed_count": number | string;
  "last_progress_at": string;
} | null;

export type PortiaEFE869FF2C47D1239F76963FA0AB021FCF0B3A5037DC8506F1AE7FF602338979 = {
  "items": Array<{
  "user_id": string;
  "email_address": string;
  "verified": boolean;
} | null>;
  "next_cursor": string | null;
} | null;

export type PortiaF5D86DD63563039EDAF243CA29FEAE85382ED90210D8D752F40BC3E24D4ED319 = {
  "tenant_id": string;
  "program_id": string;
  "risk_id": string;
  "identifier": string;
  "revision": number | string;
  "status": string;
  "owner_resolution": string;
  "content": {
  "title": string;
  "scenario": string;
  "potential_effect": string;
  "source_note": string | null;
};
  "last_changed_by_member_id": string;
  "last_changed_by_display": string;
  "last_changed_at": string;
  "last_changed_by"?: {
  "kind": string;
  "id": string;
  "display": string;
};
} | null;

export type PortiaFDA3055E83DE2E638D548DE1150B986A21043869BFD9C65F10F6CFFBBDE7AA25 = {
  "tenant_id": string;
  "application_id": string;
  "application_revision": number | string;
  "change_kind": string;
  "changes": Array<{
  "field": string;
  "before": string | null;
  "after": string | null;
} | null>;
  "boundary_references": Array<{
  "tenant_id": string;
  "subject_type": string;
  "governed_record_id": string;
  "boundary_id": string;
  "program_id": string;
  "version_id": string;
  "entry_id": string;
  "revision": number | string;
  "status": string;
  "effective_from": string | null;
  "kind": string;
  "subject": string;
  "owner_reference": string;
  "rationale": string;
} | null>;
  "pending_contexts": Array<string | null>;
  "complete": boolean;
  "control_draft_references"?: Array<{
  "tenant_id": string;
  "subject_type": string;
  "governed_record_id": string;
  "program_id": string;
  "control_id": string;
  "identifier": string;
  "revision": number | string;
  "entry_id": string;
  "subject": string;
  "rationale": string;
} | null>;
} | null;

export type PortiaFE118EE4B74A4501C2BC3C6DDF29D5D7C5248C8FE0F51D9F3D797DE0F5856377 = {
  "tenant_id": string;
  "application_id": string;
  "system_instance_id": string;
  "name": string;
  "kind": string;
  "access_boundary_reference": string | null;
  "source_kind": string;
  "source_identifier": string | null;
  "unresolved": Array<string | null>;
  "declared_by_member_id": string;
  "declared_by_display": string;
  "declared_at": string;
  "declared_by"?: {
  "kind": string;
  "id": string;
  "display": string;
};
  "revision"?: number | string;
  "legacy_application_revision"?: number | string | null;
} | null;

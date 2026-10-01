export type Portia022A384CC13FCCBBBDACFD95B82E4D9EE161732C5FF61B3FF97C70AF11E4D94C = {
  "delivery_status": string;
  "expires_at": string | null;
} | null;

export type Portia03ACFAAE67D35BA82DFA61EDE278D3779AC79928770C69B153337B1F788534A3 = {
  "tenant_id": string;
  "snapshot_id": string;
  "root_snapshot_id": string;
  "amends_snapshot_id": string | null;
  "content_sha256": string;
  "row_count": number | string;
  "amendment_reason": string | null;
  "frozen_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "frozen_at": string;
  "people": Array<{
  "person_id": string;
  "revision": number | string;
  "display_name": string;
  "work_email": string | null;
} | null>;
  "work_relationships": Array<{
  "relationship_id": string;
  "revision": number | string;
  "person_id": string;
  "source_worker_id": string;
  "worker_type": string;
  "lifecycle_status": string;
  "start_date": string;
  "end_date": string | null;
  "department": string | null;
  "manager_person_id": string | null;
  "sponsor_person_id": string | null;
} | null>;
  "restricted_fields_redacted": boolean;
} | null;

export type Portia05DF79CF8D43AF3901EC737C6EC176C9A07E4185CEB04118426D4B24E8D91153 = {
  "items": Array<{
  "tenant_id": string;
  "program_id": string;
  "control_id": string;
  "identifier": string;
  "version_id": string;
  "revision": number | string;
  "status": string;
  "content_origin": string;
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
  "effective_from": string;
  "predecessor_version_id": string | null;
  "owner_assignment_id": string;
  "owner_member_id": string;
  "owner_resolution": string;
  "accepted_review_decision_id": string;
  "approval_decision_id": string;
  "approved_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "approval_rationale": string;
  "approved_at": string;
  "separation_of_duties_waiver_id": string | null;
  "effective_until"?: string | null;
} | null>;
  "next_cursor": string | null;
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

export type Portia097249E53E54CC03E10877C906EFC2594FFE7AAC91C87D347D57D7145C1CC24C = {
  "assessment_id": string;
  "phase": string;
  "method_version_id": string;
  "method_version": number | string;
  "likelihood": number | string;
  "impact": number | string;
  "score": number | string;
  "rationale": string;
  "assessor": {
  "kind": string;
  "id": string;
  "display": string;
};
  "assessed_at": string;
} | null;

export type Portia0A9CDB70B104DD500F1650ECF17E2D71437EAAA3D83CD49AD8A315E22A081664 = {
  "items": Array<{
  "tenant_id": string;
  "program_id": string;
  "draft_id": string;
  "decision_id": string;
  "revision": number | string;
  "outcome": string;
  "owner_reference": string | null;
  "applicability": string | null;
  "interpretation": string | null;
  "interpretation_note": string | null;
  "rationale": string;
  "version": number | string | null;
  "effective_from": string | null;
  "impact_digest": string | null;
  "actor_member_id": string;
  "actor_display": string;
  "decided_at": string;
  "separation_of_duties_waiver_id": string | null;
  "actor"?: {
  "kind": string;
  "id": string;
  "display": string;
};
  "separation_of_duties_waived"?: boolean;
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

export type Portia192271E1EABBA6FC52BAF89F6B378FCACE65CA8E7A48D41243102EBBF5417625 = {
  "tenant_id": string;
  "program_id": string;
  "draft_id": string;
  "revision": number | string;
  "effective_version": number | string | null;
  "changes": Array<{
  "field": string;
  "before": string | null;
  "after": string | null;
} | null>;
  "dependents": Array<{
  "context": string;
  "record_type": string;
  "record_id": string;
  "relationship": string;
} | null>;
  "unlinked_contexts": Array<string | null>;
  "complete": boolean;
  "digest": string;
} | null;

export type Portia1A72141BBDF886D72A311BA94564A77E3CFA5B0DBA1A3FD78CF78C689F1F779D = {
  "tenant_id": string;
  "application_id": string;
  "system_instance_id": string;
  "system_instance_revision": number | string;
  "decision_id": string;
  "sequence": number | string;
  "decision": string;
  "reason": string;
  "effective_from": string;
  "review_by": string | null;
  "approved_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "decided_at": string;
  "separation_of_duties_waiver_id": string | null;
} | null;

export type Portia1D599653835771B5B38C77F6C963CA25D736248D35B45B1CD6CED58C58E70B11 = {
  "application_id": string;
} | null;

export type Portia1DAABC0EE92F473717483C90B8CF7E689206159947287755819C2AE6D3D4ECF4 = {
  "tenant_id": string;
  "program_id": string;
  "method_version_id": string;
  "version": number | string;
  "kind": string;
  "likelihood_scale": Array<string | null>;
  "impact_scale": Array<string | null>;
  "appetite_threshold": number | string | null;
  "reassessment_interval": string;
  "published_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "published_at": string;
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

export type Portia24AADB452F032CF586816A93E0006D88CF6B74A01ECC66A7DD7E222A5F991AAF = {
  "gap_id": string;
  "owner_member_id": string;
  "target_date": string;
  "action": string;
  "planned_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "planned_at": string;
} | null;

export type Portia274B659861C11787AC6AB4C8D76C648726966FA7DA2D30804A55E91A1C4D27B2 = {
  "mapping_id": string;
  "revision": number | string;
  "version_number": number | string;
} | null;

export type Portia28D5187F005E5A41920CA5A644AAA3FD5806E031D8B7A74C154FFEB4E97A5BB3 = {
  "tenant_id": string;
  "program_id": string;
  "mapping_id": string;
  "control_id": string;
  "edition_id": string;
  "criterion_identifier": string;
  "criterion_kind": string;
  "revision": number | string;
  "status": string;
  "active_version_number": number | string | null;
  "active_control_version_id": string | null;
  "versions": Array<{
  "version_number": number | string;
  "control_version_id": string;
  "status": string;
  "rationale": string;
  "applicability_explanation": string;
  "proposed_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "proposed_at": string;
  "review_decision_id": string | null;
  "reviewed_by": {
  "kind": string;
  "id": string;
  "display": string;
} | null;
  "review_rationale": string | null;
  "reviewed_at": string | null;
  "separation_of_duties_waiver_id": string | null;
  "retired_by": {
  "kind": string;
  "id": string;
  "display": string;
} | null;
  "retirement_rationale": string | null;
  "retired_at": string | null;
} | null>;
} | null;

export type Portia293AC99442386DA2E4203C76C9DCACD9B0159C4FF4F351D4C5C91AF91E5AADA5 = {
  "tenant_id": string;
  "snapshot_id": string;
  "canonical_manifest": string;
  "content_sha256": string;
} | null;

export type Portia29DA1B59E17FFC5BC5537FA36443BD20DD5A6C59FED0530C3D8E91FBB7EAB977 = {
  "decision_id": string;
  "assessment_id": string;
  "outcome": string;
  "rationale": string;
  "decider_member_id": string;
  "decided_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "decided_at": string;
  "separation_of_duties_waiver_id": string | null;
} | null;

export type Portia2AED3AF47093A366AD687BFFDD638FD60E8E83EBFD47EB5EB710621485B5041F = {
  "tenant_id": string;
  "relationship_id": string;
  "revision": number | string;
  "person_id": string;
  "source_worker_id": string;
  "worker_type": string;
  "lifecycle_status": string;
  "start_date": string;
  "end_date": string | null;
  "department": string | null;
  "manager_person_id": string | null;
  "sponsor_person_id": string | null;
  "restricted_fields_redacted": boolean;
  "source_kind": string;
  "last_changed_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "last_changed_at": string;
} | null;

export type Portia2C17E7023EE4294E36F1860F97F42B06DFE03B828D3FCC19679435D2C263FACB = {
  "items": Array<{
  "tenant_id": string;
  "program_id": string;
  "risk_id": string;
  "revision": number | string;
  "kind": string;
  "assessment": {
  "assessment_id": string;
  "phase": string;
  "method_version_id": string;
  "method_version": number | string;
  "likelihood": number | string;
  "impact": number | string;
  "score": number | string;
  "rationale": string;
  "assessor": {
  "kind": string;
  "id": string;
  "display": string;
};
  "assessed_at": string;
} | null;
  "treatment": {
  "kind": string;
  "rationale": string;
  "chosen_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "chosen_at": string;
} | null;
  "acceptance": {
  "acceptance_id": string;
  "residual_assessment_id": string;
  "residual_score": number | string;
  "appetite_threshold": number | string | null;
  "approver_member_id": string;
  "approver": {
  "kind": string;
  "id": string;
  "display": string;
};
  "approver_authority": string;
  "rationale": string;
  "accepted_at": string;
  "expires_at": string;
  "status"?: string;
} | null;
  "actor": {
  "kind": string;
  "id": string;
  "display": string;
};
  "occurred_at": string;
} | null>;
  "next_cursor": string | null;
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

export type Portia33A914476F5916BA05D9CCA32D0BA6790B75A8EBFC139D7F65E58BAF1716FF4D = {
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
  "lifecycle"?: string;
  "retirement"?: {
  "effective_at": string;
  "reason": string;
  "merged_into_application_id": string | null;
} | null;
} | null>;
  "next_cursor": string | null;
} | null;

export type Portia36D1608FA46982FB7C0145375EB90B733846AA018C6B495E9A88F5B790185F37 = {
  "relationship_id": string;
} | null;

export type Portia38AC222880FC3D76BC0113586E6EADD6FC945BF8A6AE17809DC976132058D642 = {
  "items": Array<{
  "assessment_id": string;
  "rule_version": string;
  "as_of": string;
  "rule_met_count": number | string;
  "gap_count": number | string;
  "run_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "run_at": string;
  "decision_outcome": string | null;
} | null>;
  "next_cursor": string | null;
} | null;

export type Portia3A795A94620F3B58B5F1452221FC679AB9B2B1CA5FD22BC2E89D277F4D764F4B = {
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
  "lifecycle"?: string;
  "retirement"?: {
  "effective_at": string;
  "reason": string;
  "merged_into_application_id": string | null;
} | null;
} | null>;
  "next_cursor": string | null;
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

export type Portia3BD7B3C292671FD9C7BC3514F31B918E92F82B060108984374D5D9FD3FAF7737 = {
  "tenant_id": string;
  "data_flow_id": string;
  "revision": number | string;
  "content": {
  "source_type": string;
  "source_id": string;
  "destination_type": string;
  "destination_id": string | null;
  "destination_party": string | null;
  "information_asset_ids": Array<string>;
  "purpose": string;
  "encrypted_in_transit": boolean;
  "encrypted_at_rest": boolean;
  "exception_reference": string | null;
  "effective_from": string;
  "owner_person_id": string;
  "lifecycle": string;
  "classification": string;
};
  "source_kind": string;
  "last_changed_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "last_changed_at": string;
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

export type Portia41B8ADFE95447D60E9DF0AEDA30008085B43FC2FAED3D48AD08C2B4111A952E8 = {
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
  "draft_version_id"?: string;
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

export type Portia45F55D96B4D015448EC3803E9C00616C8AA2610641128A2A4A9CCB81303CEE9D = {
  "information_asset_id": string;
} | null;

export type Portia463D989E08D87242FE062F7ABA25B4A4852CDED11C651DDB003CB1493628FBA2 = {
  "tenant_id": string;
  "information_asset_id": string;
  "revision": number | string;
  "content": {
  "name": string;
  "classification": string;
  "retention_reference": string;
  "owner_person_id": string;
  "description": string | null;
  "lifecycle": string;
};
  "source_kind": string;
  "last_changed_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "last_changed_at": string;
} | null;

export type Portia474D872CD82992E0F6DAA877E95A738CD0E651D58C89FD20650936F7E9442787 = {
  "items": Array<{
  "tenant_id": string;
  "relationship_id": string;
  "revision": number | string;
  "person_id": string;
  "source_worker_id": string;
  "worker_type": string;
  "lifecycle_status": string;
  "start_date": string;
  "end_date": string | null;
  "department": string | null;
  "manager_person_id": string | null;
  "sponsor_person_id": string | null;
  "restricted_fields_redacted": boolean;
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

export type Portia57F8BF807B6A24BB37FB6F863CFF0D6B83B16BAAC26E6D8EBEC4A8CC88C6A440 = {
  "items": Array<{
  "edition_id": string;
  "identifier": string;
  "kind": string;
  "category": string;
  "parent_identifier": string | null;
  "summary": string;
  "coverage_state": string;
  "mapped_controls": Array<{
  "mapping_id": string;
  "control_id": string;
  "control_version_id": string;
  "version_number": number | string;
  "applicability_explanation": string;
} | null>;
  "pending_proposal_count": number | string;
} | null>;
  "next_cursor": string | null;
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

export type Portia5C08B534C036029E349B3D0CE114F3E24CAD136A7E31207CE6E7D5B26A094755 = {
  "tenant_id": string;
  "program_id": string;
  "control_id": string;
  "kind": string;
  "target_id": string;
  "revision": number | string;
  "current_version_id": string | null;
  "changes": Array<{
  "field": string;
  "change_type": string;
  "entry_id": string | null;
  "previous_value": string | null;
  "proposed_value": string | null;
} | null>;
  "contributions": Array<{
  "context": string;
  "status": string;
  "freshness": string;
  "records": Array<{
  "tenant_id": string;
  "context": string;
  "record_type": string;
  "record_id": string;
  "version_id": string | null;
  "reason": string;
} | null>;
  "complete": boolean;
} | null>;
  "pending_contexts": Array<string | null>;
  "complete": boolean;
  "digest": string;
} | null;

export type Portia5C785825C5C3F7E845CCBEE2767058870CB823F13882AECF91DBC1CBDA9F87FC = {
  "control_id": string;
  "draft_version_id": string;
  "predecessor_version_id": string;
  "revision": number | string;
} | null;

export type Portia5D4504657CF0F248BBB0D39DEB82554EBDBEFE27BFFEFBB869B53903B87F98DC = {
  "tenant_id": string;
  "service_identity_id": string;
  "revision": number | string;
  "display_name": string;
  "identity_kind": string;
  "purpose": string;
  "environment": string | null;
  "lifecycle_status": string;
  "owner_kind": string;
  "owner_id": string;
  "review_by": string;
  "source_kind": string;
  "unowned": boolean;
  "unowned_reasons": Array<string | null>;
  "last_changed_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "last_changed_at": string;
  "expires_on"?: string | null;
  "expired"?: boolean;
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

export type Portia5DC40A4F1CB15D96F9110C5B5E53AA99EDE859D42D78207E7C39CA076C05FE94 = {
  "tenant_id": string;
  "slug": string;
} | null;

export type Portia63D54EA89C5C07D310D2BCA13AD5F7B74227B5C1A92D8305F29836A899A420D7 = {
  "component_id": string;
} | null;

export type Portia64BE94797D3B12DE7F671F3383F5650BDF178E0C4D09A41959A9EA71BAA14237 = {
  "items": Array<{
  "tenant_id": string;
  "program_id": string;
  "draft_id": string;
  "service_id": string;
  "kind": string;
  "identifier": string;
  "version": number | string;
  "revision": number | string;
  "statement": string;
  "context": string;
  "source_reference": string;
  "owner_reference": string;
  "applicability": string;
  "interpretation": string;
  "interpretation_note": string | null;
  "performed_by": string;
  "internally_performed": boolean;
  "effective_from": string;
  "decision": {
  "tenant_id": string;
  "program_id": string;
  "draft_id": string;
  "decision_id": string;
  "revision": number | string;
  "outcome": string;
  "owner_reference": string | null;
  "applicability": string | null;
  "interpretation": string | null;
  "interpretation_note": string | null;
  "rationale": string;
  "version": number | string | null;
  "effective_from": string | null;
  "impact_digest": string | null;
  "actor_member_id": string;
  "actor_display": string;
  "decided_at": string;
  "separation_of_duties_waiver_id": string | null;
  "actor"?: {
  "kind": string;
  "id": string;
  "display": string;
};
  "separation_of_duties_waived"?: boolean;
};
} | null>;
  "next_cursor": string | null;
} | null;

export type Portia671AC09DAB490DF2776880B0704E8B578BD2F034EE3EF6D6D28051D39931B2B7 = {
  "tenant_id": string;
  "program_id": string;
  "draft_id": string;
  "service_id": string;
  "kind": string;
  "identifier": string;
  "version": number | string;
  "revision": number | string;
  "statement": string;
  "context": string;
  "source_reference": string;
  "owner_reference": string;
  "applicability": string;
  "interpretation": string;
  "interpretation_note": string | null;
  "performed_by": string;
  "internally_performed": boolean;
  "effective_from": string;
  "decision": {
  "tenant_id": string;
  "program_id": string;
  "draft_id": string;
  "decision_id": string;
  "revision": number | string;
  "outcome": string;
  "owner_reference": string | null;
  "applicability": string | null;
  "interpretation": string | null;
  "interpretation_note": string | null;
  "rationale": string;
  "version": number | string | null;
  "effective_from": string | null;
  "impact_digest": string | null;
  "actor_member_id": string;
  "actor_display": string;
  "decided_at": string;
  "separation_of_duties_waiver_id": string | null;
  "actor"?: {
  "kind": string;
  "id": string;
  "display": string;
};
  "separation_of_duties_waived"?: boolean;
};
} | null;

export type Portia67FF42154696CE62730A90222AB6C1618C3883A7B83A530679D7947EF82A7536 = {
  "items": Array<{
  "team_id": string;
  "role_id": string;
} | null>;
  "next_cursor": string | null;
} | null;

export type Portia6AE7436CAF1D81D68D1CA26BCFADC44860AAA63B87A822CFB2212F1AE5375C3A = {
  "tenant_id": string;
  "current_slug": string;
  "redirect": boolean;
} | null;

export type Portia6D456EA5A910F2BECBEC72F07A1EDDD7ACA1FD6D3C14C5F2B93DE61C863E48FD = {
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
  "lifecycle"?: string;
  "retirement"?: {
  "effective_at": string;
  "reason": string;
  "merged_into_application_id": string | null;
} | null;
} | null;
  "actor"?: {
  "kind": string;
  "id": string;
  "display": string;
};
  "classification"?: string | null;
  "system_owner_person_id"?: string | null;
  "access_owner_person_id"?: string | null;
  "lifecycle"?: string;
  "retirement"?: {
  "effective_at": string;
  "reason": string;
  "merged_into_application_id": string | null;
} | null;
} | null;

export type Portia6E87F3E4BE9D729B36C701C34B83DA3B513977C84714C03C6C50E46E2437A4A8 = {
  "tenant_id": string;
  "program_id": string;
  "risk_id": string;
  "revision": number | string;
  "status": string;
  "assessments": Array<{
  "assessment_id": string;
  "phase": string;
  "method_version_id": string;
  "method_version": number | string;
  "likelihood": number | string;
  "impact": number | string;
  "score": number | string;
  "rationale": string;
  "assessor": {
  "kind": string;
  "id": string;
  "display": string;
};
  "assessed_at": string;
} | null>;
  "treatment": {
  "kind": string;
  "rationale": string;
  "chosen_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "chosen_at": string;
} | null;
  "acceptances": Array<{
  "acceptance_id": string;
  "residual_assessment_id": string;
  "residual_score": number | string;
  "appetite_threshold": number | string | null;
  "approver_member_id": string;
  "approver": {
  "kind": string;
  "id": string;
  "display": string;
};
  "approver_authority": string;
  "rationale": string;
  "accepted_at": string;
  "expires_at": string;
  "status"?: string;
} | null>;
  "reassessment_due_at": string | null;
  "last_changed_by": {
  "kind": string;
  "id": string;
  "display": string;
} | null;
  "last_changed_at": string | null;
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

export type Portia73AADDF0B812FAE76C884FD50264381D369C3E9825947F115D6EE39E12C94DC6 = {
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
  "lifecycle"?: string;
  "retirement"?: {
  "effective_at": string;
  "reason": string;
  "merged_into_application_id": string | null;
} | null;
} | null;
  "actor"?: {
  "kind": string;
  "id": string;
  "display": string;
};
  "classification"?: string | null;
  "system_owner_person_id"?: string | null;
  "access_owner_person_id"?: string | null;
  "lifecycle"?: string;
  "retirement"?: {
  "effective_at": string;
  "reason": string;
  "merged_into_application_id": string | null;
} | null;
} | null>;
  "next_cursor": string | null;
} | null;

export type Portia7447FB6BBB482B1C467364D016B654F11C2ED1628609E41058681ED0CB44705E = {
  "tenant_id": string;
  "information_asset_id": string;
  "revision": number | string;
  "current_classification": string;
  "proposed_classification": string;
  "current_lifecycle": string;
  "proposed_lifecycle": string;
  "affected_flows": Array<{
  "data_flow_id": string;
  "revision": number | string;
  "recorded_classification": string;
  "recomputed_classification": string;
  "classification_changed": boolean;
  "encrypted_in_transit": boolean;
  "encrypted_at_rest": boolean;
  "exception_reference": string | null;
  "encryption_violation": boolean;
  "carries_retired_asset_only": boolean;
} | null>;
  "flows_over_limit": boolean;
} | null;

export type Portia74A5A31DE9F3A44C25116AA828DDAC667A065C414D865FF4FF6A5FC4C4C5A47A = {
  "items": Array<{
  "tenant_id": string;
  "data_flow_id": string;
  "revision": number | string;
  "content": {
  "source_type": string;
  "source_id": string;
  "destination_type": string;
  "destination_id": string | null;
  "destination_party": string | null;
  "information_asset_ids": Array<string>;
  "purpose": string;
  "encrypted_in_transit": boolean;
  "encrypted_at_rest": boolean;
  "exception_reference": string | null;
  "effective_from": string;
  "owner_person_id": string;
  "lifecycle": string;
  "classification": string;
};
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

export type Portia7A8595D2FC8E586A820EE122D8C373E01E418CA296483244AB95A35A0943E149 = {
  "tenant_id": string;
  "application_id": string;
  "system_instance_id": string;
  "as_of": string;
  "status": string;
  "effective": {
  "tenant_id": string;
  "application_id": string;
  "system_instance_id": string;
  "system_instance_revision": number | string;
  "decision_id": string;
  "sequence": number | string;
  "decision": string;
  "reason": string;
  "effective_from": string;
  "review_by": string | null;
  "approved_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "decided_at": string;
  "separation_of_duties_waiver_id": string | null;
} | null;
  "decisions": Array<{
  "tenant_id": string;
  "application_id": string;
  "system_instance_id": string;
  "system_instance_revision": number | string;
  "decision_id": string;
  "sequence": number | string;
  "decision": string;
  "reason": string;
  "effective_from": string;
  "review_by": string | null;
  "approved_by": Portia7A8595D2FC8E586A820EE122D8C373E01E418CA296483244AB95A35A0943E1491;
  "decided_at": string;
  "separation_of_duties_waiver_id": string | null;
} | null>;
} | null;

export type Portia7A8595D2FC8E586A820EE122D8C373E01E418CA296483244AB95A35A0943E1491 = {
  "kind": string;
  "id": string;
  "display": string;
};

export type Portia7B1AEE3985B856552CEAEAB86F86A8B046D256BB2EA8D4AB06B5F839FFA36B9F = {
  "tenant_id": string;
  "component_id": string;
  "revision": number | string;
  "content": {
  "category": string;
  "name": string;
  "owner_person_id": string;
  "environment_reference": string | null;
  "location_reference": string | null;
  "system_instance_id": string | null;
  "endpoint_count": number | string | null;
  "management_source": string | null;
  "lifecycle": string;
};
  "source_kind": string;
  "last_changed_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "last_changed_at": string;
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

export type Portia7C156CFCACA80768D1A08D2432719D7F1C4D591F3979C08C55586944821D80F5 = {
  "items": Array<{
  "tenant_id": string;
  "observation_id": string;
  "kind": string;
  "reason": string;
  "status": string;
  "person_ids": Array<string>;
  "relationship_ids": Array<string>;
  "user_ids": Array<string>;
  "resolution"?: {
  "tenant_id": string;
  "observation_id": string;
  "resolution": string;
  "note": string;
  "resolved_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "resolved_at": string;
} | null;
} | null>;
  "next_cursor": string | null;
} | null;

export type Portia7CD37B602BC1B9CED0A3D0552650BDFFF97D061E925708668EA377395BC1DBB7 = {
  "tenant_id": string;
  "program_id": string;
  "assessment_id": string;
  "revision": number | string;
  "rule_version": string;
  "as_of": string;
  "edition_id": string | null;
  "input_fingerprint": string;
  "inputs": Array<{
  "family": string;
  "status": string;
  "record_count": number | string;
  "explanation": string;
} | null>;
  "findings": Array<{
  "criterion_identifier": string;
  "category": string;
  "summary": string;
  "rule_id": string;
  "outcome": string;
  "explanation": string;
  "sources": Array<{
  "kind": string;
  "id": string;
  "version": string | null;
} | null>;
  "gap_id": string | null;
} | null>;
  "gaps": Array<{
  "gap_id": string;
  "kind": string;
  "subject": string;
  "rule_id": string;
  "explanation": string;
  "sources": Array<Portia7CD37B602BC1B9CED0A3D0552650BDFFF97D061E925708668EA377395BC1DBB71>;
  "plan"?: {
  "gap_id": string;
  "owner_member_id": string;
  "target_date": string;
  "action": string;
  "planned_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "planned_at": string;
} | null;
} | null>;
  "rule_met_count": number | string;
  "gap_count": number | string;
  "run_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "run_at": string;
  "decision": {
  "decision_id": string;
  "assessment_id": string;
  "outcome": string;
  "rationale": string;
  "decider_member_id": string;
  "decided_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "decided_at": string;
  "separation_of_duties_waiver_id": string | null;
} | null;
} | null;

export type Portia7CD37B602BC1B9CED0A3D0552650BDFFF97D061E925708668EA377395BC1DBB71 = {
  "kind": string;
  "id": string;
  "version": string | null;
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

export type Portia96BF44EECD956DB8397701FC811AF70DE0D6E1ECDB8AB95318C6F10318CD1EDD = {
  "items": Array<{
  "tenant_id": string;
  "snapshot_id": string;
  "root_snapshot_id": string;
  "amends_snapshot_id": string | null;
  "kind": string;
  "row_count": number | string;
  "content_sha256": string;
  "amendment_reason": string | null;
  "frozen_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "frozen_at": string;
} | null>;
  "next_cursor": string | null;
} | null;

export type Portia97032B105C2A6D6A1B226A232D4A8D42B4A96F297375BA0A5D48F0938F15A14C = {
  "data_flow_id": string;
} | null;

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

export type Portia9B0607CA8CEF3829E2071107C5E2DC5C50730AFFBF2290344613FA16D3D34583 = {
  "tenant_id": string;
  "program_id": string;
  "control_id": string;
  "decision_id": string;
  "version_id": string;
  "revision": number | string;
  "kind": string;
  "outcome": string;
  "actor": {
  "kind": string;
  "id": string;
  "display": string;
};
  "rationale": string;
  "decided_at": string;
  "supersedes_decision_id": string | null;
  "relies_on_decision_id": string | null;
  "separation_of_duties_waiver_id": string | null;
  "separation_of_duties_waived"?: boolean;
} | null;

export type Portia9CA30FFC559321862CA47D0242119C614DCEC0E1E9443BE7873F142DB785415F = {
  "target_readiness_date": string | null;
  "target_type_i_as_of_date": string | null;
  "target_type_ii_start_date": string | null;
  "target_type_ii_end_date": string | null;
  "readiness_advisor": string | null;
  "audit_firm": string | null;
} | null;

export type Portia9CDD6217C5E790499885C0A6CB01BFA6E45CF9175F1EF7BBEF46BFE9C3AE0C59 = {
  "tenant_id": string;
  "program_id": string;
  "control_id": string;
  "identifier": string;
  "version_id": string;
  "revision": number | string;
  "status": string;
  "content_origin": string;
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
  "effective_from": string;
  "predecessor_version_id": string | null;
  "owner_assignment_id": string;
  "owner_member_id": string;
  "owner_resolution": string;
  "accepted_review_decision_id": string;
  "approval_decision_id": string;
  "approved_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "approval_rationale": string;
  "approved_at": string;
  "separation_of_duties_waiver_id": string | null;
  "effective_until"?: string | null;
} | null;

export type PortiaA277510249DA33D1E77EA806415366BBFB478D775605D021833F8109CE070FCA = {
  "items": Array<{
  "tenant_id": string;
  "information_asset_id": string;
  "revision": number | string;
  "content": {
  "name": string;
  "classification": string;
  "retention_reference": string;
  "owner_person_id": string;
  "description": string | null;
  "lifecycle": string;
};
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

export type PortiaA4299F97F2F2793A334427A6DBE3AB10187D44660E16D667981207CF14AD4341 = {
  "person_id": string;
} | null;

export type PortiaA5E01F0DEB4FB6B8D77D19E040EC8B616C620CF9A147CE52DA9ACDE76BDE349A = {
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
  "draft_version_id"?: string;
} | null>;
  "next_cursor": string | null;
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

export type PortiaAD20397E4469C88F974BC9D5A386CD8E3C4A25CB034E5C0233899BA467B9C283 = {
  "service_identity_id": string;
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

export type PortiaB5B4C6C1CCCDD997C7F390CEAD0680DD868403FA1B09E115245EBB04DFD8F886 = {
  "risk_id": string;
  "identifier": string;
  "revision": number | string;
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

export type PortiaC166B2A6A50AB323EBE10D693B981A72966248C62D0DBCA17A9D1F8C2A0493E1 = {
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
  "system_instance_references"?: Array<{
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
  "lifecycle"?: string;
  "retirement"?: {
  "effective_at": string;
  "reason": string;
  "merged_into_application_id": string | null;
} | null;
} | null>;
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

export type PortiaC39C98E2DBB3BF252193560530969F5DF01D0CB33395E2B40E2B8496665ED57A = {
  "items": Array<{
  "tenant_id": string;
  "program_id": string;
  "control_id": string;
  "decision_id": string;
  "version_id": string;
  "revision": number | string;
  "kind": string;
  "outcome": string;
  "actor": {
  "kind": string;
  "id": string;
  "display": string;
};
  "rationale": string;
  "decided_at": string;
  "supersedes_decision_id": string | null;
  "relies_on_decision_id": string | null;
  "separation_of_duties_waiver_id": string | null;
  "separation_of_duties_waived"?: boolean;
} | null>;
  "next_cursor": string | null;
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

export type PortiaC646B2FC53C967587683B193B991EC40E30A4137E8479560E880A6AAEA504244 = {
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
  "correlated_user_id"?: string | null;
} | null>;
  "next_cursor": string | null;
} | null;

export type PortiaC695C5A9B781750DCBCA504BA9AFE97AFE550E8F63FAF6ADE2DFE5B2E157C781 = Array<{
  "source_record_id": string | null;
  "name": string | null;
  "purpose": string | null;
  "owner_reference": string | null;
} | null> | null;

export type PortiaC6B58953EEC5AF0CF98AEC8E58EA963F8A1EF8B3D2BFA36F2F60E3073C7B1556 = {
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
  "verified_email_address"?: string | null;
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

export type PortiaCDEB60612E2CCB73291DF472ACC26D72C213DF36B15FC7D666DCAD87F850A4F9 = {
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
  "lifecycle"?: string;
  "retirement"?: {
  "effective_at": string;
  "reason": string;
  "merged_into_application_id": string | null;
} | null;
} | null;

export type PortiaCE291342413ACA5E55E75E29F7C92CAA822AC0B201F64DA7A38365474F0782CD = {
  "control_id": string;
  "identifier": string;
  "revision": number | string;
} | null;

export type PortiaCE317BDB9F287F13D0C5620C1C17559DBCA77AC9779C300BC881F44035E4A26D = {
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
  "lifecycle"?: string;
  "retirement"?: {
  "effective_at": string;
  "reason": string;
  "merged_into_application_id": string | null;
} | null;
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

export type PortiaDAC5690D56A8404A50FAA955F800B7A921C23C5147D404EFC243B5021316A557 = {
  "items": Array<{
  "tenant_id": string;
  "service_identity_id": string;
  "revision": number | string;
  "display_name": string;
  "identity_kind": string;
  "purpose": string;
  "environment": string | null;
  "lifecycle_status": string;
  "owner_kind": string;
  "owner_id": string;
  "review_by": string;
  "source_kind": string;
  "unowned": boolean;
  "unowned_reasons": Array<string | null>;
  "last_changed_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "last_changed_at": string;
  "expires_on"?: string | null;
  "expired"?: boolean;
} | null>;
  "next_cursor": string | null;
} | null;

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

export type PortiaDE57F78A9D37368116B1A7EC1CA79CF96CE81F200951380435CC4A669F8837DE = {
  "assessment_id": string;
  "revision": number | string;
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

export type PortiaE056B52B06213FCC543902C08F961FB0AC9C4CF3C96070D2E7394EE553854F2A = {
  "acceptance_id": string;
  "residual_assessment_id": string;
  "residual_score": number | string;
  "appetite_threshold": number | string | null;
  "approver_member_id": string;
  "approver": {
  "kind": string;
  "id": string;
  "display": string;
};
  "approver_authority": string;
  "rationale": string;
  "accepted_at": string;
  "expires_at": string;
  "status"?: string;
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

export type PortiaE533A13177B62D1EFE8E47B2C8DBE1B703CC5A9D68DD56C8A5015618FD353625 = {
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
  "correlated_user_id"?: string | null;
} | null;

export type PortiaE696B3EC1B513A6E3725F1903D7BE6C03EAA06E8EC473F8555A1F39F198BB262 = {
  "items": Array<{
  "tenant_id": string;
  "application_id": string;
  "system_instance_id": string;
  "instance_lifecycle": string;
  "as_of": string;
  "status": string;
  "review_overdue": boolean;
  "decision_count": number | string;
  "effective": {
  "tenant_id": string;
  "application_id": string;
  "system_instance_id": string;
  "system_instance_revision": number | string;
  "decision_id": string;
  "sequence": number | string;
  "decision": string;
  "reason": string;
  "effective_from": string;
  "review_by": string | null;
  "approved_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "decided_at": string;
  "separation_of_duties_waiver_id": string | null;
} | null;
  "upcoming": {
  "tenant_id": string;
  "application_id": string;
  "system_instance_id": string;
  "system_instance_revision": number | string;
  "decision_id": string;
  "sequence": number | string;
  "decision": string;
  "reason": string;
  "effective_from": string;
  "review_by": string | null;
  "approved_by": PortiaE696B3EC1B513A6E3725F1903D7BE6C03EAA06E8EC473F8555A1F39F198BB2621;
  "decided_at": string;
  "separation_of_duties_waiver_id": string | null;
} | null;
} | null>;
  "next_cursor": string | null;
} | null;

export type PortiaE696B3EC1B513A6E3725F1903D7BE6C03EAA06E8EC473F8555A1F39F198BB2621 = {
  "kind": string;
  "id": string;
  "display": string;
};

export type PortiaEA6D5265BAC64B7B7ECBB403179F0802AEF5CDBFF6D32E0FCDF3EF54CB637D54 = {
  "items": Array<{
  "team_id": string;
  "name": string;
} | null>;
  "next_cursor": string | null;
} | null;

export type PortiaEBEBAE40CDBFE8B4726C2E5FDB6EE27438942900064BA238E5F7452B92135107 = Array<string | null> | null;

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

export type PortiaF3BACE104C01FC9709E1FE05FA74F9ED4D50EA2EACF73C77510A4B743F9B8CC6 = {
  "control_id": string;
  "retirement_id": string;
  "version_id": string;
  "revision": number | string;
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

export type PortiaF91217E35959EE564AF2004EA1727032FC5084CF5CF0155ED5D422DB017C5B3F = {
  "items": Array<{
  "tenant_id": string;
  "component_id": string;
  "revision": number | string;
  "content": {
  "category": string;
  "name": string;
  "owner_person_id": string;
  "environment_reference": string | null;
  "location_reference": string | null;
  "system_instance_id": string | null;
  "endpoint_count": number | string | null;
  "management_source": string | null;
  "lifecycle": string;
};
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

export type PortiaF9B77C7ADBEF0EB8FE1C2659269AA790D9F0D90014FCAFB7B2571FB108D35FD8 = {
  "items": Array<{
  "gap_id": string;
  "kind": string;
  "subject": string;
  "rule_id": string;
  "explanation": string;
  "sources": Array<{
  "kind": string;
  "id": string;
  "version": string | null;
} | null>;
  "plan"?: {
  "gap_id": string;
  "owner_member_id": string;
  "target_date": string;
  "action": string;
  "planned_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "planned_at": string;
} | null;
} | null>;
  "next_cursor": string | null;
} | null;

export type PortiaFA8797A2099AD3C0E34408B3661A645092B14759512BA3656FC05B7770BF1E26 = {
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
  "verified_email_address"?: string | null;
} | null;

export type PortiaFB363E63265F366744A1615DFD1F101B4EADC47287216F1A34FA3DACB525E2AF = {
  "items": Array<{
  "tenant_id": string;
  "observation_id": string;
  "kind": string;
  "status": string;
  "relationship_id": string;
  "person_id": string;
  "source_worker_id": string;
  "relationship_revision": number | string;
  "effective_date": string;
  "changed_fields": Array<string | null>;
  "observed_from": {
  "kind": string;
  "id": string;
  "display": string;
};
  "observed_at": string;
  "resolution"?: {
  "tenant_id": string;
  "observation_id": string;
  "resolution": string;
  "note": string;
  "resolved_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "resolved_at": string;
} | null;
} | null>;
  "next_cursor": string | null;
} | null;

export type PortiaFCF00D94A0472AF13CDF0CB77F1A463EF3413B0EDADF00C16BFD3C56766F97C2 = {
  "items": Array<{
  "tenant_id": string;
  "program_id": string;
  "mapping_id": string;
  "control_id": string;
  "edition_id": string;
  "criterion_identifier": string;
  "criterion_kind": string;
  "revision": number | string;
  "status": string;
  "active_version_number": number | string | null;
  "active_control_version_id": string | null;
  "versions": Array<{
  "version_number": number | string;
  "control_version_id": string;
  "status": string;
  "rationale": string;
  "applicability_explanation": string;
  "proposed_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "proposed_at": string;
  "review_decision_id": string | null;
  "reviewed_by": {
  "kind": string;
  "id": string;
  "display": string;
} | null;
  "review_rationale": string | null;
  "reviewed_at": string | null;
  "separation_of_duties_waiver_id": string | null;
  "retired_by": {
  "kind": string;
  "id": string;
  "display": string;
} | null;
  "retirement_rationale": string | null;
  "retired_at": string | null;
} | null>;
} | null>;
  "next_cursor": string | null;
} | null;

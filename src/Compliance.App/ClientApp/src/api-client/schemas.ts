export type Portia013D7F6C3C0FEB6E8082C97A2D31504B222E662A6A84DD2B56600D5BF2D16139 = {
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
  "stage"?: string;
  "accepted_review_decision_id"?: string | null;
  "source_verification"?: string | null;
  "source_verified_reference"?: string | null;
  "source_evidence"?: string | null;
  "actor"?: {
  "kind": string;
  "id": string;
  "display": string;
};
  "separation_of_duties_waived"?: boolean;
};
  "source_resolution"?: string;
  "source_evidence"?: string | null;
  "approval"?: {
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
  "stage"?: string;
  "accepted_review_decision_id"?: string | null;
  "source_verification"?: string | null;
  "source_verified_reference"?: string | null;
  "source_evidence"?: string | null;
  "actor"?: Portia013D7F6C3C0FEB6E8082C97A2D31504B222E662A6A84DD2B56600D5BF2D161391;
  "separation_of_duties_waived"?: boolean;
} | null;
} | null>;
  "next_cursor": string | null;
} | null;

export type Portia013D7F6C3C0FEB6E8082C97A2D31504B222E662A6A84DD2B56600D5BF2D161391 = {
  "kind": string;
  "id": string;
  "display": string;
};

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

export type Portia055CA1599B22C4A39EEDF56A03F53F231CEBE6075D8937F744977DD9D66E54FD = {
  "tenant_id": string;
  "system_instance_id": string;
  "revision": number | string;
  "expectations": Array<{
  "expectation_id": string;
  "system_instance_id": string;
  "supersedes_expectation_id": string | null;
  "rule_kind": string;
  "parameters": {
  "principal_kind"?: string | null;
  "entitlement_kind"?: string | null;
  "provider_entitlement_id"?: string | null;
  "provider_subject_id"?: string | null;
};
  "rationale": string;
  "effective_from": string;
  "effective_until": string | null;
  "status": string;
  "proposed_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "proposed_at": string;
  "approved_by": {
  "kind": string;
  "id": string;
  "display": string;
} | null;
  "approved_at": string | null;
  "separation_of_duties_waiver_id": string | null;
} | null>;
  "exceptions": Array<{
  "exception_id": string;
  "expectation_id": string;
  "provider_subject_id": string;
  "provider_entitlement_id": string | null;
  "rationale": string;
  "expires_at": string;
  "approved_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "approved_at": string;
} | null>;
  "population_exceptions": Array<{
  "exception_id": string;
  "system_instance_id": string;
  "reason": string;
  "expires_at": string;
  "approved_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "approved_at": string;
} | null>;
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

export type Portia0F54B0A48070763F53BE45B0F025C1745F6F2EE7C0D73202A16DA9FE1E41E536 = {
  "title": string;
  "purpose": string;
  "audience_kind": string;
  "audience_teams": Array<string | null> | null;
  "review_cadence_months": number | string;
  "body": string | null;
  "source_reference": string | null;
  "owner_reference": string | null;
  "applicability": Array<{
  "subject_type": string;
  "subject": string;
  "record_id"?: string | null;
} | null> | null;
} | null;

export type Portia0FAA5D97CF311C7291EA6ADDA14F5C608F6E845037404DCF66A7FA2DA33BEAF7 = {
  "kind": string;
  "id": string;
} | null;

export type Portia0FBE6D4CEB2CC84764DF0FFF82C76BB333FB69E323061CA548534BC3DFB78B51 = {
  "boundary_id": string;
  "draft_version_id": string;
} | null;

export type Portia119B1B96AE3CD15CA98EA1845F142D63F8BCAC26FF8D09C7414F2C69563FF5B0 = {
  "decision_id": string;
  "revision": number | string;
  "version_number": number | string;
} | null;

export type Portia141A8449007BC3642408B5E3A41E19F30E829AC6B607080B6F410B38E370BD2E = {
  "acknowledgement_id": string;
  "person_id": string;
  "policy_version": number | string;
  "content_sha256": string;
  "acknowledgement_text": string;
  "performer": {
  "kind": string;
  "id": string;
  "display": string;
};
  "recorder": {
  "kind": string;
  "id": string;
  "display": string;
};
  "recorded_on_behalf": boolean;
  "acknowledged_at": string;
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

export type Portia183272C2785AB7DAF0DDB9887649BB6524E07D26A9750A0F1422C8AD50831FB9 = {
  "items": Array<{
  "control_id": string;
  "identifier": string;
  "control_version_id": string | null;
  "kind": string;
  "explanation": string;
  "occurrence_id"?: string | null;
} | null>;
  "next_cursor": string | null;
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

export type Portia1AABC072058538BE7FD1B099EC8BCE55A4F7A5AAF4C4B650FF18FB1F414E1E86 = {
  "finding_id": string;
  "revision": number | string;
} | null;

export type Portia1B51602F06141206ED19B855080CC6A10F95F1354F62A54606D6649DA73FBC07 = {
  "kind": string;
  "record_id": string | null;
  "version": string | null;
  "source_text": string;
} | null;

export type Portia1C25C4B0018EA4C94541F8463FC978A3F4C63744AC2F9FC2D98FEE5B6F55AA8E = {
  "principal_kind"?: string | null;
  "entitlement_kind"?: string | null;
  "provider_entitlement_id"?: string | null;
  "provider_subject_id"?: string | null;
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

export type Portia1E516B7E59F08223EE49BB74695B06EE1AE7FE8D6BF8E50F76EE68386F7A72DA = {
  "email_digest_enabled": boolean;
  "changed_at": string | null;
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

export type Portia1FF79BD41EDC10E67AF82C5AC8AA375E30EB20870D88CD7322218F9CD2983831 = {
  "expectation_id": string;
  "system_instance_id": string;
  "supersedes_expectation_id": string | null;
  "rule_kind": string;
  "parameters": {
  "principal_kind"?: string | null;
  "entitlement_kind"?: string | null;
  "provider_entitlement_id"?: string | null;
  "provider_subject_id"?: string | null;
};
  "rationale": string;
  "effective_from": string;
  "effective_until": string | null;
  "status": string;
  "proposed_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "proposed_at": string;
  "approved_by": {
  "kind": string;
  "id": string;
  "display": string;
} | null;
  "approved_at": string | null;
  "separation_of_duties_waiver_id": string | null;
} | null;

export type Portia223D8ED908741464C092218194160F10C7407F5339064207F7EDDFB8FC5706D2 = {
  "tenant_id": string;
  "program_id": string;
  "policy_id": string;
  "identifier": string;
  "status": string;
  "pending_status": string | null;
  "revision": number | string;
  "draft": {
  "title": string;
  "purpose": string;
  "audience_kind": string;
  "audience_teams": Array<string | null> | null;
  "review_cadence_months": number | string;
  "body": string | null;
  "source_reference": string | null;
  "owner_reference": string | null;
  "applicability": Array<{
  "subject_type": string;
  "subject": string;
  "record_id"?: string | null;
} | null> | null;
} | null;
  "draft_content_sha256": string | null;
  "draft_predecessor_version": number | string | null;
  "accepted_review_decision_id": string | null;
  "current_version": number | string | null;
  "current_effective_from": string | null;
  "last_reviewed_on": string | null;
  "next_review_due_on": string | null;
  "review_overdue": boolean;
  "pending_retirement": {
  "version": number | string;
  "effective_until": string;
  "rationale": string;
  "proposed_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "proposed_at": string;
} | null;
  "last_changed_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "last_changed_at": string;
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

export type Portia2664E66364A3BD18497EAE1B5A14F44FE6851C2CFF87AA850690EA322CF1731E = {
  "items": Array<{
  "tenant_id": string;
  "program_id": string;
  "policy_id": string;
  "identifier": string;
  "title": string;
  "status": string;
  "pending_status": string | null;
  "revision": number | string;
  "current_version": number | string | null;
  "current_effective_from": string | null;
  "next_review_due_on": string | null;
  "review_overdue": boolean;
  "last_changed_at": string;
} | null>;
  "next_cursor": string | null;
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

export type Portia293DE426AA53AD49D4AE678B0FEDB2F4734ED0C23AA4984147FA026082080856 = {
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
  "provenance"?: {
  "origin": string;
  "source_name"?: string | null;
  "source_reference"?: string | null;
  "source_version"?: string | null;
} | null;
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
  "owner_person_id"?: string | null;
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

export type Portia30E355AC0C727AD6771307EDF8639387797EB7EF33A7FF597E35281049D3A67B = {
  "campaign_id": string;
  "reconciliation": number | string;
  "roster_snapshot_id": string;
  "amendments": Array<{
  "person_id": string;
  "display_name": string;
  "reason": string;
  "worker_type": string | null;
  "department": string | null;
  "due_on": string | null;
} | null>;
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

export type Portia380ED2C37CEF989571B9646AABA84F263F4332B0073F69A22C6E3A516CEB2197 = {
  "item": {
  "work_item_id": string;
  "kind": string;
  "source_id": string;
  "control_id": string | null;
  "finding_id": string | null;
  "summary": string;
  "reason": string;
  "due_on": string | null;
  "overdue": boolean;
  "materiality": string | null;
  "next_action": string;
  "action_path": string;
  "responsible": {
  "kind": string;
  "id": string;
};
  "assignee_member_id": string | null;
  "assignment_revision": number | string;
  "escalated": boolean;
  "escalated_by": string | null;
  "created_at": string;
};
  "history": Array<{
  "revision": number | string;
  "action": string;
  "assignee_member_id": string | null;
  "previous_assignee_member_id": string | null;
  "reason": string | null;
  "by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "at": string;
} | null>;
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

export type Portia3A00A9545DBC606A9D844545423CA77D102CDF0448DC82A4001C72CD45E3849A = {
  "decisions": Array<{
  "decision_id": string;
  "item_id": string;
  "decision": string;
  "rationale": string;
  "reviewer_member_id": string;
  "decided_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "decided_at": string;
  "bulk_preview_token": string | null;
  "after_deadline": boolean;
  "separation_of_duties_waiver_id": string | null;
} | null>;
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

export type Portia3D403D282DFA1E4ED98B4578E4D7850CCEC17460A271A4EEE9124D80AD091488 = {
  "exception_id": string;
  "expectation_id": string;
  "provider_subject_id": string;
  "provider_entitlement_id": string | null;
  "rationale": string;
  "expires_at": string;
  "approved_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "approved_at": string;
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

export type Portia452F41728C6AFA9916F07B977123DCCF98284CE3451ABA0C4C76C709D48F8747 = {
  "digest_id": string;
  "week_of": string;
  "email_digest_enabled": boolean;
  "overdue": Array<{
  "work_item_id": string;
  "kind": string;
  "source_id": string;
  "control_id": string | null;
  "finding_id": string | null;
  "summary": string;
  "reason": string;
  "due_on": string | null;
  "overdue": boolean;
  "materiality": string | null;
  "next_action": string;
  "action_path": string;
  "responsible": {
  "kind": string;
  "id": string;
};
  "assignee_member_id": string | null;
  "assignment_revision": number | string;
  "escalated": boolean;
  "escalated_by": string | null;
  "created_at": string;
} | null>;
  "due_soon": Array<Portia452F41728C6AFA9916F07B977123DCCF98284CE3451ABA0C4C76C709D48F87471>;
} | null;

export type Portia452F41728C6AFA9916F07B977123DCCF98284CE3451ABA0C4C76C709D48F87471 = {
  "work_item_id": string;
  "kind": string;
  "source_id": string;
  "control_id": string | null;
  "finding_id": string | null;
  "summary": string;
  "reason": string;
  "due_on": string | null;
  "overdue": boolean;
  "materiality": string | null;
  "next_action": string;
  "action_path": string;
  "responsible": {
  "kind": string;
  "id": string;
};
  "assignee_member_id": string | null;
  "assignment_revision": number | string;
  "escalated": boolean;
  "escalated_by": string | null;
  "created_at": string;
} | null;

export type Portia45F55D96B4D015448EC3803E9C00616C8AA2610641128A2A4A9CCB81303CEE9D = {
  "information_asset_id": string;
} | null;

export type Portia46334DBC52E6A1AB64DEA808B9B7518BD8CA5116F786F7BAAF173857E10A75C3 = {
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
  "provenance"?: {
  "origin": string;
  "source_name"?: string | null;
  "source_reference"?: string | null;
  "source_version"?: string | null;
} | null;
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

export type Portia4745E5442B58B410B21973D635D16710CD3A17B4B309CF195296D53640E3785B = {
  "snapshot_id": string;
  "content_sha256": string;
  "attestation": string;
  "completed_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "completed_at": string;
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

export type Portia47859CF5FF8AFC9721EEF5A600985A572F055CA4FA57B5D4FB578E7C08549EC9 = {
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
  "provenance"?: {
  "origin": string;
  "source_name"?: string | null;
  "source_reference"?: string | null;
  "source_version"?: string | null;
} | null;
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

export type Portia47FBF2CE655A0BFE041F48054259143EC9BF203612F24E82B9C8E3DC73E0056B = {
  "items": Array<{
  "tenant_id": string;
  "program_id": string;
  "requirement_id": string;
  "identifier": string;
  "version": number | string;
  "latest_version": number | string;
  "content": {
  "course_name": string;
  "description": string | null;
  "audience_kind": string;
  "audience_teams": Array<string | null> | null;
  "delivery_source": string;
};
  "content_sha256": string;
  "cadence": string;
  "changed_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "changed_at": string;
} | null>;
  "next_cursor": string | null;
} | null;

export type Portia4A57DD196CC4DFA74BA2A81FA6DD69B318AF958916620C3396D9D260326EE1B3 = {
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
  "provenance"?: {
  "origin": string;
  "source_name"?: string | null;
  "source_reference"?: string | null;
  "source_version"?: string | null;
} | null;
} | null;

export type Portia4D86519FCC16F02548C3E29E580EC003867A07CF26D256F57E4B3D9A40816FFC = {
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
  "remap_required"?: boolean;
} | null>;
  "pending_proposal_count": number | string;
  "not_applicable_decision_id"?: string | null;
} | null>;
  "next_cursor": string | null;
} | null;

export type Portia4DD7D3410340405FBA257F32935EE3C1C844A4AB7348761CE6EC36FE0442CFEA = {
  "items": Array<{
  "tenant_id": string;
  "program_id": string;
  "policy_id": string;
  "decision_id": string;
  "kind": string;
  "outcome": string;
  "revision": number | string | null;
  "version": number | string | null;
  "rationale": string;
  "actor": {
  "kind": string;
  "id": string;
  "display": string;
};
  "decided_at": string;
  "separation_of_duties_waiver_id": string | null;
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

export type Portia4F9472E8C89E75A49CAA5EB8FF7F639F8D013338A1A5DAFFE37FAD78E28C30DA = {
  "items": Array<{
  "tenant_id": string;
  "program_id": string;
  "campaign_id": string;
  "subject_kind": string;
  "subject_id": string;
  "subject_identifier": string;
  "subject_version": number | string;
  "title": string;
  "due_on": string;
  "status": string;
  "launch_audience": number | string;
  "launched_at": string;
} | null>;
  "next_cursor": string | null;
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

export type Portia51FE555DD79808725AEE8E82914094E523850FDA6EFBE755215A4071F1A05B52 = {
  "campaign_id": string;
  "audience_count": number | string;
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

export type Portia5395E8EB93A7F927C06702E6F40680736BAFDF03A8CC1607A7345BA92295690E = {
  "sequence": number | string;
  "classification": string;
  "person_id": string | null;
  "service_identity_id": string | null;
  "accountable_owner_person_id": string | null;
  "shared_justification": string | null;
  "rationale": string;
  "proposed_classification": string | null;
  "classified_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "classified_at": string;
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

export type Portia613EC1E24171579C38DBADD1656E2EBACEFD2C2F65F7AE93AAB5427AD81797DE = {
  "items": Array<{
  "tenant_id": string;
  "program_id": string;
  "control_id": string;
  "occurrence_id": string;
  "revision": number | string;
  "kind": string;
  "state": string;
  "control_version_id": string;
  "plan_version_id": string;
  "period_start": string | null;
  "period_end": string | null;
  "due_on": string | null;
  "trigger": string | null;
  "assignee": {
  "kind": string;
  "id": string;
};
  "attestations": Array<{
  "attestation_id": string;
  "version": number | string;
  "result": string;
  "performed_at": string;
  "covered_from": string | null;
  "covered_until": string | null;
  "notes": string | null;
  "rationale": string | null;
  "evidence": Array<{
  "expected_evidence_index": number | string | null;
  "kind": string;
  "reference": string;
  "description"?: string | null;
  "resolution"?: string | null;
} | null>;
  "performed_by": {
  "kind": string;
  "id": string;
};
  "recorder_member_id": string;
  "recorded_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "recorded_at": string;
  "control_version_id": string;
  "plan_version_id": string;
  "expected_evidence": Array<string | null>;
  "correction_reason"?: string | null;
  "supersedes_attestation_id"?: string | null;
} | null>;
  "reviews": Array<{
  "decision_id": string;
  "attestation_id": string;
  "attestation_version": number | string;
  "outcome": string;
  "rationale": string;
  "requested_actions": Array<string | null>;
  "reviewer_member_id": string;
  "reviewed_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "reviewed_at": string;
  "separation_of_duties_waiver_id"?: string | null;
} | null>;
  "reassignments": Array<{
  "occurrence_id": string;
  "from": {
  "kind": string;
  "id": string;
} | null;
  "to": {
  "kind": string;
  "id": string;
};
  "plan_version_id": string;
} | null>;
} | null>;
  "next_cursor": string | null;
} | null;

export type Portia63D54EA89C5C07D310D2BCA13AD5F7B74227B5C1A92D8305F29836A899A420D7 = {
  "component_id": string;
} | null;

export type Portia63F211009C81A9D69EBDC64C2FF858EA739198D57AE189F575F6DB29D8401827 = Array<{
  "principal_provider_subject_id": string;
  "provider_entitlement_id": string;
  "expires_at"?: string | null;
} | null> | null;

export type Portia64968A1E13B54F46E6D59363F33BAE3CC4FF6500FD184206E0B3BFBEA3161514 = {
  "items": Array<{
  "tenant_id": string;
  "program_id": string;
  "control_id": string;
  "evaluation_id": string;
  "revision": number | string;
  "control_version_id": string;
  "state": string;
  "round": number | string;
  "evaluator_member_id": string;
  "started_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "started_at": string;
  "steps": Array<{
  "step_id": string;
  "index": number | string;
  "assertion": string;
  "method": string;
  "inspected_items": Array<{
  "kind": string;
  "reference": string;
  "version": string;
} | null>;
  "expected_condition": string;
  "result": {
  "round": number | string;
  "result": string;
  "rationale": string;
  "inspected_items": Array<Portia64968A1E13B54F46E6D59363F33BAE3CC4FF6500FD184206E0B3BFBEA31615141>;
  "recorded_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "recorded_at": string;
} | null;
} | null>;
  "deviations": Array<{
  "deviation_id": string;
  "step_id": string;
  "round": number | string;
  "classification": string;
  "description": string;
  "status": string;
  "finding_id": string | null;
  "disposition": string | null;
  "disposition_rationale": string | null;
  "waiver_id": string | null;
  "detected_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "detected_at": string;
} | null>;
  "submissions": Array<{
  "round": number | string;
  "conclusions": Array<{
  "assertion": string;
  "conclusion": string;
  "rationale": string;
} | null>;
  "overall": string;
  "steps": Array<Portia64968A1E13B54F46E6D59363F33BAE3CC4FF6500FD184206E0B3BFBEA31615142>;
  "deviations": Array<Portia64968A1E13B54F46E6D59363F33BAE3CC4FF6500FD184206E0B3BFBEA31615143>;
  "submitted_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "submitted_at": string;
} | null>;
  "reviews": Array<{
  "decision_id": string;
  "round": number | string;
  "decision": string;
  "rationale": string;
  "reviewer_member_id": string;
  "reviewed_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "reviewed_at": string;
  "separation_of_duties_waiver_id"?: string | null;
} | null>;
  "latest_review": {
  "decision_id": string;
  "round": number | string;
  "decision": string;
  "rationale": string;
  "reviewer_member_id": string;
  "reviewed_by": Portia64968A1E13B54F46E6D59363F33BAE3CC4FF6500FD184206E0B3BFBEA31615144;
  "reviewed_at": string;
  "separation_of_duties_waiver_id"?: string | null;
} | null;
  "accepted_overall": string | null;
  "retest_of_evaluation_id": string | null;
  "retest_of_deviation_ids": Array<string>;
  "retest_status": string;
  "retest_evaluation_ids": Array<string>;
} | null>;
  "next_cursor": string | null;
} | null;

export type Portia64968A1E13B54F46E6D59363F33BAE3CC4FF6500FD184206E0B3BFBEA31615141 = {
  "kind": string;
  "reference": string;
  "version": string;
} | null;

export type Portia64968A1E13B54F46E6D59363F33BAE3CC4FF6500FD184206E0B3BFBEA31615142 = {
  "step_id": string;
  "index": number | string;
  "assertion": string;
  "method": string;
  "inspected_items": Array<{
  "kind": string;
  "reference": string;
  "version": string;
} | null>;
  "expected_condition": string;
  "result": {
  "round": number | string;
  "result": string;
  "rationale": string;
  "inspected_items": Array<Portia64968A1E13B54F46E6D59363F33BAE3CC4FF6500FD184206E0B3BFBEA31615141>;
  "recorded_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "recorded_at": string;
} | null;
} | null;

export type Portia64968A1E13B54F46E6D59363F33BAE3CC4FF6500FD184206E0B3BFBEA31615143 = {
  "deviation_id": string;
  "step_id": string;
  "round": number | string;
  "classification": string;
  "description": string;
  "status": string;
  "finding_id": string | null;
  "disposition": string | null;
  "disposition_rationale": string | null;
  "waiver_id": string | null;
  "detected_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "detected_at": string;
} | null;

export type Portia64968A1E13B54F46E6D59363F33BAE3CC4FF6500FD184206E0B3BFBEA31615144 = {
  "kind": string;
  "id": string;
  "display": string;
};

export type Portia67B9E95C80AD633362E30AED291963C74151F8CE6124A0F98697F8E3DCABB8D7 = {
  "tenant_id": string;
  "campaign_id": string;
  "revision": number | string;
  "name": string;
  "instructions": string;
  "deadline": string;
  "status": string;
  "snapshot_id": string;
  "content_sha256": string;
  "reviewers": Array<{
  "population_id": string;
  "system_instance_id": string;
  "reviewer_member_id": string;
  "delegated": boolean;
  "delegation_reason": string | null;
} | null>;
  "items": Array<{
  "item": {
  "item_id": string;
  "population_id": string;
  "population_snapshot_id": string;
  "system_instance_id": string;
  "reviewer_member_id": string;
  "provider_subject_id": string;
  "principal_kind": string;
  "subject_display_name": string;
  "classification": string;
  "subject_person_id": string | null;
  "subject_user_id": string | null;
  "provider_entitlement_id": string;
  "entitlement_kind": string;
  "entitlement_display_name": string;
  "privileged": boolean;
  "variance_category": string;
  "paths": Array<{
  "hops": Array<{
  "kind": string;
  "from": string;
  "to": string;
} | null>;
  "expires_at": string | null;
} | null>;
  "expires_at": string | null;
};
  "status": string;
  "decision": {
  "decision_id": string;
  "item_id": string;
  "decision": string;
  "rationale": string;
  "reviewer_member_id": string;
  "decided_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "decided_at": string;
  "bulk_preview_token": string | null;
  "after_deadline": boolean;
  "separation_of_duties_waiver_id": string | null;
} | null;
  "decision_history": Array<{
  "decision_id": string;
  "item_id": string;
  "decision": string;
  "rationale": string;
  "reviewer_member_id": string;
  "decided_by": Portia67B9E95C80AD633362E30AED291963C74151F8CE6124A0F98697F8E3DCABB8D71;
  "decided_at": string;
  "bulk_preview_token": string | null;
  "after_deadline": boolean;
  "separation_of_duties_waiver_id": string | null;
} | null>;
  "remediation_status": string;
  "provider_changes": Array<{
  "change_id": string;
  "reference": string;
  "description": string;
  "changed_at": string;
  "recorded_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "recorded_at": string;
} | null>;
  "verification": {
  "population_id": string;
  "snapshot_id": string;
  "calculation_id": string;
  "observed_at": string;
  "outcome": string;
  "verified_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "verified_at": string;
} | null;
  "exception": {
  "exception_id": string;
  "rationale": string;
  "expires_at": string | null;
  "approved_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "approved_at": string;
} | null;
} | null>;
  "unresolved_count": number | string;
  "remediation_open_count": number | string;
  "launched_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "launched_at": string;
  "completion": {
  "snapshot_id": string;
  "content_sha256": string;
  "attestation": string;
  "completed_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "completed_at": string;
} | null;
} | null;

export type Portia67B9E95C80AD633362E30AED291963C74151F8CE6124A0F98697F8E3DCABB8D71 = {
  "kind": string;
  "id": string;
  "display": string;
};

export type Portia67FF42154696CE62730A90222AB6C1618C3883A7B83A530679D7947EF82A7536 = {
  "items": Array<{
  "team_id": string;
  "role_id": string;
} | null>;
  "next_cursor": string | null;
} | null;

export type Portia68A3EA0CA5F80C9797BB122EE28E1022CC49939048D6F526CD8CA215FECE13B0 = {
  "population_id": string;
  "snapshot_id": string;
  "calculation_id": string;
  "observed_at": string;
  "outcome": string;
  "verified_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "verified_at": string;
} | null;

export type Portia68D897B5E51EC2FC3E79D0212B58DE2291C916C1893D2C59029BFF357274E581 = {
  "waiver_id": string;
  "person_id": string;
  "reason": string;
  "expires_on": string;
  "approver": {
  "kind": string;
  "id": string;
  "display": string;
};
  "approved_at": string;
} | null;

export type Portia6AE7436CAF1D81D68D1CA26BCFADC44860AAA63B87A822CFB2212F1AE5375C3A = {
  "tenant_id": string;
  "current_slug": string;
  "redirect": boolean;
} | null;

export type Portia6C9CF32EA0FA52D3E46677C05C4B09117A3F07A53F1D7BF0E3FD06FA6B3248BB = {
  "tenant_id": string;
  "program_id": string;
  "policy_id": string;
  "decision_id": string;
  "kind": string;
  "outcome": string;
  "revision": number | string | null;
  "version": number | string | null;
  "rationale": string;
  "actor": {
  "kind": string;
  "id": string;
  "display": string;
};
  "decided_at": string;
  "separation_of_duties_waiver_id": string | null;
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

export type Portia717F77E4665D9616BE94EEEF72BC01EF932245408F2AEAE20394ADACE6A23687 = {
  "items": Array<{
  "tenant_id": string;
  "program_id": string;
  "finding_id": string;
  "revision": number | string;
  "source": {
  "kind": string;
  "record_id": string | null;
  "version": string | null;
  "source_text": string;
};
  "title": string;
  "description": string;
  "severity": string;
  "affected_scope": string;
  "owner_member_id": string;
  "due_on": string;
  "root_cause": string | null;
  "status": string;
  "readiness_status": string;
  "links": Array<{
  "kind": string;
  "reference": string;
} | null>;
  "corrective_actions": Array<{
  "action_id": string;
  "description": string;
  "owner_member_id": string;
  "due_on": string;
  "status": string;
  "added_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "added_at": string;
  "resolution_notes"?: string | null;
  "evidence"?: Array<{
  "expected_evidence_index": number | string | null;
  "kind": string;
  "reference": string;
  "description"?: string | null;
  "resolution"?: string | null;
} | null> | null;
  "completed_by_member_id"?: string | null;
  "completed_by"?: {
  "kind": string;
  "id": string;
  "display": string;
} | null;
  "completed_at"?: string | null;
} | null>;
  "acceptances": Array<{
  "kind": string;
  "record_id": string;
  "decision_id": string | null;
  "expires_at": string;
  "linked_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "linked_at": string;
  "state"?: string;
} | null>;
  "closure": {
  "decision_id": string;
  "verification_rationale": string;
  "resolution_evidence": Array<Portia717F77E4665D9616BE94EEEF72BC01EF932245408F2AEAE20394ADACE6A236871>;
  "rationale": string;
  "closer_member_id": string;
  "closed_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "closed_at": string;
  "separation_of_duties_waiver_id"?: string | null;
} | null;
  "history": Array<{
  "revision": number | string;
  "change": string;
  "summary": string;
  "actor": {
  "kind": string;
  "id": string;
  "display": string;
};
  "at": string;
} | null>;
  "raised_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "raised_at": string;
} | null>;
  "next_cursor": string | null;
} | null;

export type Portia717F77E4665D9616BE94EEEF72BC01EF932245408F2AEAE20394ADACE6A236871 = {
  "expected_evidence_index": number | string | null;
  "kind": string;
  "reference": string;
  "description"?: string | null;
  "resolution"?: string | null;
} | null;

export type Portia7289719EC72DE6F96317B0AF7168E28E848A0049DF1FA90D4E65F4328B1EC586 = {
  "tenant_id": string;
  "snapshot_id": string;
  "kind": string;
  "row_count": number | string;
  "chunk_count": number | string;
  "content_sha256": string;
  "canonical_manifest": string;
  "manifest_sha256": string;
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

export type Portia77BC6F20B8FD14DA873B0870E2E097EC40F2D759251FC89539FC397ABB0863C7 = {
  "campaign_id": string;
  "revision": number | string;
  "decision": string;
  "eligible": Array<{
  "item_id": string;
  "population_id": string;
  "population_snapshot_id": string;
  "system_instance_id": string;
  "reviewer_member_id": string;
  "provider_subject_id": string;
  "principal_kind": string;
  "subject_display_name": string;
  "classification": string;
  "subject_person_id": string | null;
  "subject_user_id": string | null;
  "provider_entitlement_id": string;
  "entitlement_kind": string;
  "entitlement_display_name": string;
  "privileged": boolean;
  "variance_category": string;
  "paths": Array<{
  "hops": Array<{
  "kind": string;
  "from": string;
  "to": string;
} | null>;
  "expires_at": string | null;
} | null>;
  "expires_at": string | null;
} | null>;
  "rejected": Array<{
  "item_id": string;
  "reason": string;
} | null>;
  "preview_token": string;
} | null;

export type Portia78094BDF93BB27C5D0F9AA6EC6C971ACDDDD95176F34DADB21AF495C1971ED1D = {
  "population_id": string;
  "revision": number | string;
} | null;

export type Portia797165BE01568FE0B8DBC2E797503C47C5739072DA71A828AE545B546C5C11A2 = {
  "change_id": string;
  "reference": string;
  "description": string;
  "changed_at": string;
  "recorded_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "recorded_at": string;
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

export type Portia7BA2123C497578ACF1569F2812DABDA302200FD1023402720CA66785EF762FED = Array<{
  "population_id": string;
  "reviewer_member_id": string;
  "delegation_reason"?: string | null;
} | null> | null;

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

export type Portia83D07B4EFEB18ECE4EF3A7B4B6F2244853EEB9FFDBEFBF7F04C3C0CEAE56B9CB = {
  "tenant_id": string;
  "program_id": string;
  "control_id": string;
  "evaluation_id": string;
  "revision": number | string;
  "control_version_id": string;
  "state": string;
  "round": number | string;
  "evaluator_member_id": string;
  "started_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "started_at": string;
  "steps": Array<{
  "step_id": string;
  "index": number | string;
  "assertion": string;
  "method": string;
  "inspected_items": Array<{
  "kind": string;
  "reference": string;
  "version": string;
} | null>;
  "expected_condition": string;
  "result": {
  "round": number | string;
  "result": string;
  "rationale": string;
  "inspected_items": Array<Portia83D07B4EFEB18ECE4EF3A7B4B6F2244853EEB9FFDBEFBF7F04C3C0CEAE56B9CB1>;
  "recorded_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "recorded_at": string;
} | null;
} | null>;
  "deviations": Array<{
  "deviation_id": string;
  "step_id": string;
  "round": number | string;
  "classification": string;
  "description": string;
  "status": string;
  "finding_id": string | null;
  "disposition": string | null;
  "disposition_rationale": string | null;
  "waiver_id": string | null;
  "detected_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "detected_at": string;
} | null>;
  "submissions": Array<{
  "round": number | string;
  "conclusions": Array<{
  "assertion": string;
  "conclusion": string;
  "rationale": string;
} | null>;
  "overall": string;
  "steps": Array<Portia83D07B4EFEB18ECE4EF3A7B4B6F2244853EEB9FFDBEFBF7F04C3C0CEAE56B9CB2>;
  "deviations": Array<Portia83D07B4EFEB18ECE4EF3A7B4B6F2244853EEB9FFDBEFBF7F04C3C0CEAE56B9CB3>;
  "submitted_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "submitted_at": string;
} | null>;
  "reviews": Array<{
  "decision_id": string;
  "round": number | string;
  "decision": string;
  "rationale": string;
  "reviewer_member_id": string;
  "reviewed_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "reviewed_at": string;
  "separation_of_duties_waiver_id"?: string | null;
} | null>;
  "latest_review": {
  "decision_id": string;
  "round": number | string;
  "decision": string;
  "rationale": string;
  "reviewer_member_id": string;
  "reviewed_by": Portia83D07B4EFEB18ECE4EF3A7B4B6F2244853EEB9FFDBEFBF7F04C3C0CEAE56B9CB4;
  "reviewed_at": string;
  "separation_of_duties_waiver_id"?: string | null;
} | null;
  "accepted_overall": string | null;
  "retest_of_evaluation_id": string | null;
  "retest_of_deviation_ids": Array<string>;
  "retest_status": string;
  "retest_evaluation_ids": Array<string>;
} | null;

export type Portia83D07B4EFEB18ECE4EF3A7B4B6F2244853EEB9FFDBEFBF7F04C3C0CEAE56B9CB1 = {
  "kind": string;
  "reference": string;
  "version": string;
} | null;

export type Portia83D07B4EFEB18ECE4EF3A7B4B6F2244853EEB9FFDBEFBF7F04C3C0CEAE56B9CB2 = {
  "step_id": string;
  "index": number | string;
  "assertion": string;
  "method": string;
  "inspected_items": Array<{
  "kind": string;
  "reference": string;
  "version": string;
} | null>;
  "expected_condition": string;
  "result": {
  "round": number | string;
  "result": string;
  "rationale": string;
  "inspected_items": Array<Portia83D07B4EFEB18ECE4EF3A7B4B6F2244853EEB9FFDBEFBF7F04C3C0CEAE56B9CB1>;
  "recorded_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "recorded_at": string;
} | null;
} | null;

export type Portia83D07B4EFEB18ECE4EF3A7B4B6F2244853EEB9FFDBEFBF7F04C3C0CEAE56B9CB3 = {
  "deviation_id": string;
  "step_id": string;
  "round": number | string;
  "classification": string;
  "description": string;
  "status": string;
  "finding_id": string | null;
  "disposition": string | null;
  "disposition_rationale": string | null;
  "waiver_id": string | null;
  "detected_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "detected_at": string;
} | null;

export type Portia83D07B4EFEB18ECE4EF3A7B4B6F2244853EEB9FFDBEFBF7F04C3C0CEAE56B9CB4 = {
  "kind": string;
  "id": string;
  "display": string;
};

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

export type Portia848218C1EDDB0BB69AE69A522A5399F4F051FF727DB3D3A8516B5D470E862F9C = {
  "policy_id": string;
  "identifier": string;
  "revision": number | string;
} | null;

export type Portia871F8CC05AD7D5F92C62179C2FAF4B0C9482F621AC1C5337DA051CA3449935F8 = Array<{
  "kind": string;
  "reference": string;
} | null> | null;

export type Portia88689C12C0FB6431DB31439CCE47EDDF15E892678804B4AAC1633CF025902FA4 = {
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
  "provenance"?: {
  "origin": string;
  "source_name"?: string | null;
  "source_reference"?: string | null;
  "source_version"?: string | null;
} | null;
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

export type Portia88B0F753F42B585D52BCAE1FC7E825AE67D21A190630E1D80A18A96727D1186F = {
  "tenant_id": string;
  "program_id": string;
  "risk_id": string;
  "revision": number | string;
  "owner": {
  "person_id": string;
  "correlated_member_id": string | null;
  "rationale": string;
  "assigned_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "assigned_at": string;
} | null;
  "control_treatments": Array<{
  "treatment_id": string;
  "risk_id": string;
  "control_id": string;
  "control_version_id": string;
  "status": string;
  "rationale": string;
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
  "reassessment_triggers": Array<{
  "trigger_id": string;
  "risk_id": string;
  "trigger_kind": string;
  "source_reference": string;
  "raised_at": string;
  "status"?: string;
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

export type Portia895E347267064464C7EEA3101D9897828DE9ACE40AE8D3EB6B31BD0E2E552E59 = {
  "scope": string;
  "as_of": string;
  "counts": {
  "total": number | string;
  "overdue": number | string;
  "due_today": number | string;
  "escalated": number | string;
};
  "items": Array<{
  "work_item_id": string;
  "kind": string;
  "source_id": string;
  "control_id": string | null;
  "finding_id": string | null;
  "summary": string;
  "reason": string;
  "due_on": string | null;
  "overdue": boolean;
  "materiality": string | null;
  "next_action": string;
  "action_path": string;
  "responsible": {
  "kind": string;
  "id": string;
};
  "assignee_member_id": string | null;
  "assignment_revision": number | string;
  "escalated": boolean;
  "escalated_by": string | null;
  "created_at": string;
} | null>;
} | null;

export type Portia8A10B3D9610CE4B221E33CC56E464200ACA1EE75D7D9944E4FE43F40B69577C1 = {
  "items": Array<{
  "tenant_id": string;
  "program_id": string;
  "policy_id": string;
  "identifier": string;
  "version": number | string;
  "revision": number | string;
  "major": boolean;
  "status": string;
  "content": {
  "title": string;
  "purpose": string;
  "audience_kind": string;
  "audience_teams": Array<string | null> | null;
  "review_cadence_months": number | string;
  "body": string | null;
  "source_reference": string | null;
  "owner_reference": string | null;
  "applicability": Array<{
  "subject_type": string;
  "subject": string;
  "record_id"?: string | null;
} | null> | null;
};
  "content_sha256": string;
  "effective_from": string;
  "effective_until": string | null;
  "predecessor_version": number | string | null;
  "approval_decision_id": string;
  "accepted_review_decision_id": string;
  "approved_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "approved_at": string;
  "impact_digest": string | null;
  "separation_of_duties_waiver_id": string | null;
} | null>;
  "next_cursor": string | null;
} | null;

export type Portia8D5633FE067F626EBE517251F6EFAB49E2AE1C79C435B49EA490311B53BCCF8A = {
  "population_id": string;
  "snapshot_id": string;
  "content_sha256": string;
  "calculation_id": string;
} | null;

export type Portia8EA37BD5888AD3C09F39A36A7FACB11427760BFA167EC4F8BAFBAEEF95F7ABE3 = {
  "items": Array<{
  "population_id": string;
  "provider_subject_id": string;
  "principal_kind": string;
  "display_name": string;
  "status": string;
  "email": string | null;
  "is_access_structure": boolean;
  "classification": string;
  "current": {
  "sequence": number | string;
  "classification": string;
  "person_id": string | null;
  "service_identity_id": string | null;
  "accountable_owner_person_id": string | null;
  "shared_justification": string | null;
  "rationale": string;
  "proposed_classification": string | null;
  "classified_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "classified_at": string;
} | null;
  "history": Array<{
  "sequence": number | string;
  "classification": string;
  "person_id": string | null;
  "service_identity_id": string | null;
  "accountable_owner_person_id": string | null;
  "shared_justification": string | null;
  "rationale": string;
  "proposed_classification": string | null;
  "classified_by": Portia8EA37BD5888AD3C09F39A36A7FACB11427760BFA167EC4F8BAFBAEEF95F7ABE31;
  "classified_at": string;
} | null>;
  "proposal": {
  "classification": string;
  "person_id": string | null;
  "reason": string;
} | null;
  "gaps": Array<string | null>;
  "entitlement_count": number | string;
} | null>;
  "next_cursor": string | null;
} | null;

export type Portia8EA37BD5888AD3C09F39A36A7FACB11427760BFA167EC4F8BAFBAEEF95F7ABE31 = {
  "kind": string;
  "id": string;
  "display": string;
};

export type Portia8EC39D203E40F38A63A668A3300716B942ECA6885268F24789B7087CBB825751 = {
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
  "separation_of_duties_waiver_id"?: string | null;
} | null;

export type Portia8F0CE391B931A838823BCA7B954533FA430653B8233464EE2D86EE0099BE798A = {
  "decision_id": string;
  "item_id": string;
  "decision": string;
  "rationale": string;
  "reviewer_member_id": string;
  "decided_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "decided_at": string;
  "bulk_preview_token": string | null;
  "after_deadline": boolean;
  "separation_of_duties_waiver_id": string | null;
} | null;

export type Portia9057974446C97EEBDB9DE36F30C6DB793B5538A21235EA3265B9649504F306BE = {
  "items": Array<{
  "tenant_id": string;
  "program_id": string;
  "decision_id": string;
  "edition_id": string;
  "criterion_identifier": string;
  "revision": number | string;
  "status": string;
  "active_version_number": number | string | null;
  "versions": Array<{
  "version_number": number | string;
  "status": string;
  "rationale": string;
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
  "withdrawn_by": {
  "kind": string;
  "id": string;
  "display": string;
} | null;
  "withdrawal_rationale": string | null;
  "withdrawn_at": string | null;
} | null>;
} | null>;
  "next_cursor": string | null;
} | null;

export type Portia9065D279549CE38EF784C0EDC609B94880345498852BE7465233B097BA2F7869 = {
  "control_id": string;
  "revision": number | string;
  "current": {
  "tenant_id": string;
  "program_id": string;
  "control_id": string;
  "plan_version_id": string;
  "revision": number | string;
  "status": string;
  "control_version_id": string;
  "owner": {
  "kind": string;
  "id": string;
};
  "backup_owner": {
  "kind": string;
  "id": string;
} | null;
  "reviewer_member_id": string;
  "cadence": {
  "kind": string;
  "frequency"?: string | null;
  "first_period_start"?: string | null;
  "due_within_days"?: number | string | null;
  "trigger"?: string | null;
};
  "cadence_description": string;
  "expected_evidence": Array<string | null>;
  "effective_from": string;
  "effective_until": string | null;
  "rationale": string;
  "proposer_member_id": string;
  "proposed_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "proposed_at": string;
  "proposal_separation_of_duties_waiver_id"?: string | null;
  "approved_by"?: {
  "kind": string;
  "id": string;
  "display": string;
} | null;
  "approved_at"?: string | null;
  "approval_rationale"?: string | null;
  "approval_separation_of_duties_waiver_id"?: string | null;
  "reassignments"?: Array<{
  "occurrence_id": string;
  "from": {
  "kind": string;
  "id": string;
} | null;
  "to": {
  "kind": string;
  "id": string;
};
  "plan_version_id": string;
} | null> | null;
} | null;
  "pending": {
  "tenant_id": string;
  "program_id": string;
  "control_id": string;
  "plan_version_id": string;
  "revision": number | string;
  "status": string;
  "control_version_id": string;
  "owner": Portia9065D279549CE38EF784C0EDC609B94880345498852BE7465233B097BA2F78691;
  "backup_owner": Portia9065D279549CE38EF784C0EDC609B94880345498852BE7465233B097BA2F78692;
  "reviewer_member_id": string;
  "cadence": Portia9065D279549CE38EF784C0EDC609B94880345498852BE7465233B097BA2F78693;
  "cadence_description": string;
  "expected_evidence": Portia9065D279549CE38EF784C0EDC609B94880345498852BE7465233B097BA2F78694;
  "effective_from": string;
  "effective_until": string | null;
  "rationale": string;
  "proposer_member_id": string;
  "proposed_by": Portia9065D279549CE38EF784C0EDC609B94880345498852BE7465233B097BA2F78695;
  "proposed_at": string;
  "proposal_separation_of_duties_waiver_id"?: string | null;
  "approved_by"?: Portia9065D279549CE38EF784C0EDC609B94880345498852BE7465233B097BA2F78696;
  "approved_at"?: string | null;
  "approval_rationale"?: string | null;
  "approval_separation_of_duties_waiver_id"?: string | null;
  "reassignments"?: Portia9065D279549CE38EF784C0EDC609B94880345498852BE7465233B097BA2F78697;
} | null;
  "history": Array<{
  "tenant_id": string;
  "program_id": string;
  "control_id": string;
  "plan_version_id": string;
  "revision": number | string;
  "status": string;
  "control_version_id": string;
  "owner": Portia9065D279549CE38EF784C0EDC609B94880345498852BE7465233B097BA2F78691;
  "backup_owner": Portia9065D279549CE38EF784C0EDC609B94880345498852BE7465233B097BA2F78692;
  "reviewer_member_id": string;
  "cadence": Portia9065D279549CE38EF784C0EDC609B94880345498852BE7465233B097BA2F78693;
  "cadence_description": string;
  "expected_evidence": Portia9065D279549CE38EF784C0EDC609B94880345498852BE7465233B097BA2F78694;
  "effective_from": string;
  "effective_until": string | null;
  "rationale": string;
  "proposer_member_id": string;
  "proposed_by": Portia9065D279549CE38EF784C0EDC609B94880345498852BE7465233B097BA2F78695;
  "proposed_at": string;
  "proposal_separation_of_duties_waiver_id"?: string | null;
  "approved_by"?: Portia9065D279549CE38EF784C0EDC609B94880345498852BE7465233B097BA2F78696;
  "approved_at"?: string | null;
  "approval_rationale"?: string | null;
  "approval_separation_of_duties_waiver_id"?: string | null;
  "reassignments"?: Portia9065D279549CE38EF784C0EDC609B94880345498852BE7465233B097BA2F78697;
} | null>;
} | null;

export type Portia9065D279549CE38EF784C0EDC609B94880345498852BE7465233B097BA2F78691 = {
  "kind": string;
  "id": string;
};

export type Portia9065D279549CE38EF784C0EDC609B94880345498852BE7465233B097BA2F78692 = {
  "kind": string;
  "id": string;
} | null;

export type Portia9065D279549CE38EF784C0EDC609B94880345498852BE7465233B097BA2F78693 = {
  "kind": string;
  "frequency"?: string | null;
  "first_period_start"?: string | null;
  "due_within_days"?: number | string | null;
  "trigger"?: string | null;
};

export type Portia9065D279549CE38EF784C0EDC609B94880345498852BE7465233B097BA2F78694 = Array<string | null>;

export type Portia9065D279549CE38EF784C0EDC609B94880345498852BE7465233B097BA2F78695 = {
  "kind": string;
  "id": string;
  "display": string;
};

export type Portia9065D279549CE38EF784C0EDC609B94880345498852BE7465233B097BA2F78696 = {
  "kind": string;
  "id": string;
  "display": string;
} | null;

export type Portia9065D279549CE38EF784C0EDC609B94880345498852BE7465233B097BA2F78697 = Array<{
  "occurrence_id": string;
  "from": {
  "kind": string;
  "id": string;
} | null;
  "to": {
  "kind": string;
  "id": string;
};
  "plan_version_id": string;
} | null> | null;

export type Portia92D234454C20DC8E7698D3F4608F463CC45108FD7C6FCB35EBDEFD7131274C0D = {
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
  "unlinked_contexts": Array<{
  "context": string;
  "reason": string;
} | null>;
  "complete": boolean;
  "digest": string;
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

export type Portia96B1731EB5D13928DBD0ED8D189660CF91F7740AD92E08EBFD63D3BF2BBC958F = {
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
  "stage"?: string;
  "accepted_review_decision_id"?: string | null;
  "source_verification"?: string | null;
  "source_verified_reference"?: string | null;
  "source_evidence"?: string | null;
  "actor"?: {
  "kind": string;
  "id": string;
  "display": string;
};
  "separation_of_duties_waived"?: boolean;
};
  "source_resolution"?: string;
  "source_evidence"?: string | null;
  "approval"?: {
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
  "stage"?: string;
  "accepted_review_decision_id"?: string | null;
  "source_verification"?: string | null;
  "source_verified_reference"?: string | null;
  "source_evidence"?: string | null;
  "actor"?: Portia96B1731EB5D13928DBD0ED8D189660CF91F7740AD92E08EBFD63D3BF2BBC958F1;
  "separation_of_duties_waived"?: boolean;
} | null;
} | null;

export type Portia96B1731EB5D13928DBD0ED8D189660CF91F7740AD92E08EBFD63D3BF2BBC958F1 = {
  "kind": string;
  "id": string;
  "display": string;
};

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

export type Portia98DBC88EDB4D5BDDE18F31C1436D60500C23C1F99805D1913C5915F5ADDC59DD = {
  "kind": string;
  "frequency"?: string | null;
  "first_period_start"?: string | null;
  "due_within_days"?: number | string | null;
  "trigger"?: string | null;
} | null;

export type Portia999618C71B7B006783D3F23026F9C4D587F517B253F9F62802EFE693018FB2CA = {
  "tenant_id": string;
  "population_id": string;
  "revision": number | string;
  "application_id": string;
  "system_instance_id": string;
  "system_instance_revision": number | string;
  "observed_at": string;
  "source_kind": string;
  "source": string;
  "status": string;
  "facts": {
  "principals": Array<{
  "provider_subject_id": string;
  "principal_kind": string;
  "display_name": string;
  "status": string;
  "email"?: string | null;
} | null>;
  "entitlements": Array<{
  "provider_entitlement_id": string;
  "entitlement_kind": string;
  "display_name": string;
} | null>;
  "group_members": Array<{
  "group_provider_subject_id": string;
  "member_provider_subject_id": string;
} | null>;
  "assignments": Array<{
  "principal_provider_subject_id": string;
  "provider_entitlement_id": string;
  "expires_at"?: string | null;
} | null>;
};
  "issues": Array<{
  "category": string;
  "code": string;
  "subject": string;
  "message": string;
} | null>;
  "effective_access": Array<{
  "provider_subject_id": string;
  "provider_entitlement_id": string;
  "direct": boolean;
  "paths": Array<{
  "hops": Array<{
  "kind": string;
  "from": string;
  "to": string;
} | null>;
  "expires_at": string | null;
} | null>;
  "expires_at": string | null;
} | null>;
  "calculation_id": string | null;
  "snapshot_id": string | null;
  "content_sha256": string | null;
  "attestation": string | null;
  "opened_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "accepted_by": {
  "kind": string;
  "id": string;
  "display": string;
} | null;
  "accepted_at": string | null;
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

export type Portia9D06184903CEE42AA204A04AF68AF0059EF7DEDC923932BC599AB3BB26412113 = {
  "tenant_id": string;
  "program_id": string;
  "requirement_id": string;
  "identifier": string;
  "version": number | string;
  "latest_version": number | string;
  "content": {
  "course_name": string;
  "description": string | null;
  "audience_kind": string;
  "audience_teams": Array<string | null> | null;
  "delivery_source": string;
};
  "content_sha256": string;
  "cadence": string;
  "changed_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "changed_at": string;
} | null;

export type Portia9EE7CFBC2DB1E57A7AB5450792B4056ACF5FDF2A4383256FD21D6EEEBF893879 = {
  "cadence_description": string;
  "expected_evidence": Array<string | null>;
  "upcoming_occurrences": Array<{
  "tenant_id": string;
  "program_id": string;
  "control_id": string;
  "occurrence_id": string;
  "revision": number | string;
  "kind": string;
  "state": string;
  "control_version_id": string;
  "plan_version_id": string;
  "period_start": string | null;
  "period_end": string | null;
  "due_on": string | null;
  "trigger": string | null;
  "assignee": {
  "kind": string;
  "id": string;
};
  "attestations": Array<{
  "attestation_id": string;
  "version": number | string;
  "result": string;
  "performed_at": string;
  "covered_from": string | null;
  "covered_until": string | null;
  "notes": string | null;
  "rationale": string | null;
  "evidence": Array<{
  "expected_evidence_index": number | string | null;
  "kind": string;
  "reference": string;
  "description"?: string | null;
  "resolution"?: string | null;
} | null>;
  "performed_by": {
  "kind": string;
  "id": string;
};
  "recorder_member_id": string;
  "recorded_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "recorded_at": string;
  "control_version_id": string;
  "plan_version_id": string;
  "expected_evidence": Array<string | null>;
  "correction_reason"?: string | null;
  "supersedes_attestation_id"?: string | null;
} | null>;
  "reviews": Array<{
  "decision_id": string;
  "attestation_id": string;
  "attestation_version": number | string;
  "outcome": string;
  "rationale": string;
  "requested_actions": Array<string | null>;
  "reviewer_member_id": string;
  "reviewed_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "reviewed_at": string;
  "separation_of_duties_waiver_id"?: string | null;
} | null>;
  "reassignments": Array<{
  "occurrence_id": string;
  "from": {
  "kind": string;
  "id": string;
} | null;
  "to": {
  "kind": string;
  "id": string;
};
  "plan_version_id": string;
} | null>;
} | null>;
  "conflicts": Array<string | null>;
  "reassignments": Array<Portia9EE7CFBC2DB1E57A7AB5450792B4056ACF5FDF2A4383256FD21D6EEEBF8938791>;
} | null;

export type Portia9EE7CFBC2DB1E57A7AB5450792B4056ACF5FDF2A4383256FD21D6EEEBF8938791 = {
  "occurrence_id": string;
  "from": {
  "kind": string;
  "id": string;
} | null;
  "to": {
  "kind": string;
  "id": string;
};
  "plan_version_id": string;
} | null;

export type Portia9FA5D5CF525C4AF42E939440C7466EDE599F60FC5B4D7162FD53DD072DBF2027 = {
  "tenant_id": string;
  "program_id": string;
  "policy_id": string;
  "identifier": string;
  "version": number | string;
  "revision": number | string;
  "major": boolean;
  "status": string;
  "content": {
  "title": string;
  "purpose": string;
  "audience_kind": string;
  "audience_teams": Array<string | null> | null;
  "review_cadence_months": number | string;
  "body": string | null;
  "source_reference": string | null;
  "owner_reference": string | null;
  "applicability": Array<{
  "subject_type": string;
  "subject": string;
  "record_id"?: string | null;
} | null> | null;
};
  "content_sha256": string;
  "effective_from": string;
  "effective_until": string | null;
  "predecessor_version": number | string | null;
  "approval_decision_id": string;
  "accepted_review_decision_id": string;
  "approved_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "approved_at": string;
  "impact_digest": string | null;
  "separation_of_duties_waiver_id": string | null;
} | null;

export type Portia9FCAF1CC602FB2404B5CF52AB3A7AF4FB9BF7AD634FD20E054362C2971D5C6E4 = {
  "items": Array<{
  "tenant_id": string;
  "population_id": string;
  "application_id": string;
  "system_instance_id": string;
  "system_instance_revision": number | string;
  "observed_at": string;
  "revision": number | string;
  "status": string;
  "snapshot_id": string | null;
  "content_sha256": string | null;
  "accepted_at": string | null;
} | null>;
  "next_cursor": string | null;
} | null;

export type PortiaA25FBFD25C5DA718EF703EC9807E1F0349D507CDB2F63B9398B7F26E9EC369D9 = Array<{
  "assertion": string;
  "method": string;
  "inspected_items": Array<{
  "kind": string;
  "reference": string;
  "version": string;
} | null>;
  "expected_condition": string;
} | null> | null;

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

export type PortiaA575A714BD65E0A40C74D0ADDC7743F0FC29A5EC04226EA7371EBAD7B642251C = {
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
  "provenance"?: {
  "origin": string;
  "source_name"?: string | null;
  "source_reference"?: string | null;
  "source_version"?: string | null;
} | null;
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

export type PortiaAB78B394BEAB9C3EAC62EC80E078A79A64B1AE85D3A626BAD90D554F0E0FF5F8 = {
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
  "separation_of_duties_waiver_id"?: string | null;
} | null>;
  "reassessment_due_at": string | null;
  "last_changed_by": {
  "kind": string;
  "id": string;
  "display": string;
} | null;
  "last_changed_at": string | null;
  "open_reassessment_triggers"?: Array<{
  "trigger_id": string;
  "risk_id": string;
  "trigger_kind": string;
  "source_reference": string;
  "raised_at": string;
  "status"?: string;
} | null> | null;
} | null;

export type PortiaABC62954E80A62897E82DF675C5392B33BE1B4C71C1B877E8115AC0CBFD97E27 = Array<{
  "expected_evidence_index": number | string | null;
  "kind": string;
  "reference": string;
  "description"?: string | null;
  "resolution"?: string | null;
} | null> | null;

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

export type PortiaACD105735FFCA90DB185696163BF28386D9855A759F2E53F1B2B4A58AFE087CD = {
  "tenant_id": string;
  "program_id": string;
  "decision_id": string;
  "edition_id": string;
  "criterion_identifier": string;
  "revision": number | string;
  "status": string;
  "active_version_number": number | string | null;
  "versions": Array<{
  "version_number": number | string;
  "status": string;
  "rationale": string;
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
  "withdrawn_by": {
  "kind": string;
  "id": string;
  "display": string;
} | null;
  "withdrawal_rationale": string | null;
  "withdrawn_at": string | null;
} | null>;
} | null;

export type PortiaAD1A36FB0E7DA3A7661197E93F9C3FAC072DFA215C0D19CDBA14BC399E425EC3 = {
  "snapshot_id": string;
  "content_sha256": string;
} | null;

export type PortiaAD20397E4469C88F974BC9D5A386CD8E3C4A25CB034E5C0233899BA467B9C283 = {
  "service_identity_id": string;
} | null;

export type PortiaADD2B4C414E1944EE190450BDA58B841C185CE857DDB2F46E1785464B36D095B = {
  "requirement_id": string;
  "identifier": string;
  "version": number | string;
} | null;

export type PortiaAF9D90F9D29976C8CE67E2F682A350FB57A5B5B729FF40887ACC33E345E34BBC = {
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
  "separation_of_duties_waiver_id"?: string | null;
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

export type PortiaAFAF766C0228CE70B8538430CAEDC481CB9225A47B3D7DB91672B60DE869A007 = Array<{
  "provider_entitlement_id": string;
  "entitlement_kind": string;
  "display_name": string;
} | null> | null;

export type PortiaB2401AF589C5863F4C683B88251FE1B9E91B82E95AFAA57501CD8611540CBC1B = {
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
  "provenance"?: {
  "origin": string;
  "source_name"?: string | null;
  "source_reference"?: string | null;
  "source_version"?: string | null;
} | null;
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
  "owner_person_id"?: string | null;
} | null>;
  "next_cursor": string | null;
} | null;

export type PortiaB40D0675C1B674F43BFBDCE25F20837F7CF28C29B2067DF1534D10FA9DE94F05 = {
  "draft_id": string;
  "kind": string;
  "identifier": string;
  "revision": number | string;
} | null;

export type PortiaB47D44CD61F1BAE17F9B3642AC00CEF06F2C4D1DEB0364DFC7C6B68A7F1C6300 = Array<{
  "assertion": string;
  "conclusion": string;
  "rationale": string;
} | null> | null;

export type PortiaB4DD33A7F5FCF2E0B8851762FDE92625FD4111B116DCEEFF5492192AC3808FD1 = {
  "population_id": string;
  "revision": number | string;
  "issues": Array<{
  "category": string;
  "code": string;
  "subject": string;
  "message": string;
} | null>;
  "effective_access": Array<{
  "provider_subject_id": string;
  "provider_entitlement_id": string;
  "direct": boolean;
  "paths": Array<{
  "hops": Array<{
  "kind": string;
  "from": string;
  "to": string;
} | null>;
  "expires_at": string | null;
} | null>;
  "expires_at": string | null;
} | null>;
  "can_accept": boolean;
} | null;

export type PortiaB5702939DCF5C30F95C9A73E21F8F0F14F920AEA657BCC2634DED63C18886EEF = {
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
  "stage"?: string;
  "accepted_review_decision_id"?: string | null;
  "source_verification"?: string | null;
  "source_verified_reference"?: string | null;
  "source_evidence"?: string | null;
  "actor"?: {
  "kind": string;
  "id": string;
  "display": string;
};
  "separation_of_duties_waived"?: boolean;
} | null>;
  "next_cursor": string | null;
} | null;

export type PortiaB5B4C6C1CCCDD997C7F390CEAD0680DD868403FA1B09E115245EBB04DFD8F886 = {
  "risk_id": string;
  "identifier": string;
  "revision": number | string;
} | null;

export type PortiaB869DD0526AA33A1F47CE6CE38CC1B91E9DFADCED8528496D3AA2817193F8E4C = {
  "campaign_id": string;
  "snapshot_id": string;
  "content_sha256": string;
  "item_count": number | string;
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

export type PortiaBF61A0E2E929B0B0C2C8FD90D26A50B2912F90E0432F13057B32A47660B93B93 = {
  "items": Array<{
  "tenant_id": string;
  "campaign_id": string;
  "name": string;
  "deadline": string;
  "status": string;
  "item_count": number | string;
  "snapshot_id": string;
  "launched_at": string;
  "final_snapshot_id": string | null;
  "completed_at": string | null;
} | null>;
  "next_cursor": string | null;
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

export type PortiaCAB9DEB40F4B7BA004A8D9BA3C94D8944AE60BFF46D6005CDD7D49FFE715CBA0 = {
  "items": Array<{
  "person_id": string;
  "display_name": string;
  "worker_type": string;
  "department": string | null;
  "inclusion": string;
  "included_by_snapshot_id": string;
  "state": string;
  "due_on": string;
  "removed_reason": string | null;
  "removed_by_snapshot_id": string | null;
  "acknowledgement": {
  "acknowledgement_id": string;
  "person_id": string;
  "policy_version": number | string;
  "content_sha256": string;
  "acknowledgement_text": string;
  "performer": {
  "kind": string;
  "id": string;
  "display": string;
};
  "recorder": {
  "kind": string;
  "id": string;
  "display": string;
};
  "recorded_on_behalf": boolean;
  "acknowledged_at": string;
} | null;
  "completion": {
  "completion_id": string;
  "person_id": string;
  "requirement_version": number | string;
  "completed_on": string;
  "source": string;
  "evidence_reference": string;
  "recorder": {
  "kind": string;
  "id": string;
  "display": string;
};
  "recorded_at": string;
} | null;
  "waiver": {
  "waiver_id": string;
  "person_id": string;
  "reason": string;
  "expires_on": string;
  "approver": {
  "kind": string;
  "id": string;
  "display": string;
};
  "approved_at": string;
} | null;
} | null>;
  "next_cursor": string | null;
} | null;

export type PortiaCADDBB36C737CF14F2057C0A4E15C9E63FDD811D019E6BEFA5647B9E2BA9E2C8 = {
  "service_id": string;
} | null;

export type PortiaCBA6630CFA91012BCB9C1FDEAAE964104EC054B36A1E0C6FE396D235D74C8C4F = {
  "tenant_id": string;
  "program_id": string;
  "finding_id": string;
  "revision": number | string;
  "source": {
  "kind": string;
  "record_id": string | null;
  "version": string | null;
  "source_text": string;
};
  "title": string;
  "description": string;
  "severity": string;
  "affected_scope": string;
  "owner_member_id": string;
  "due_on": string;
  "root_cause": string | null;
  "status": string;
  "readiness_status": string;
  "links": Array<{
  "kind": string;
  "reference": string;
} | null>;
  "corrective_actions": Array<{
  "action_id": string;
  "description": string;
  "owner_member_id": string;
  "due_on": string;
  "status": string;
  "added_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "added_at": string;
  "resolution_notes"?: string | null;
  "evidence"?: Array<{
  "expected_evidence_index": number | string | null;
  "kind": string;
  "reference": string;
  "description"?: string | null;
  "resolution"?: string | null;
} | null> | null;
  "completed_by_member_id"?: string | null;
  "completed_by"?: {
  "kind": string;
  "id": string;
  "display": string;
} | null;
  "completed_at"?: string | null;
} | null>;
  "acceptances": Array<{
  "kind": string;
  "record_id": string;
  "decision_id": string | null;
  "expires_at": string;
  "linked_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "linked_at": string;
  "state"?: string;
} | null>;
  "closure": {
  "decision_id": string;
  "verification_rationale": string;
  "resolution_evidence": Array<PortiaCBA6630CFA91012BCB9C1FDEAAE964104EC054B36A1E0C6FE396D235D74C8C4F1>;
  "rationale": string;
  "closer_member_id": string;
  "closed_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "closed_at": string;
  "separation_of_duties_waiver_id"?: string | null;
} | null;
  "history": Array<{
  "revision": number | string;
  "change": string;
  "summary": string;
  "actor": {
  "kind": string;
  "id": string;
  "display": string;
};
  "at": string;
} | null>;
  "raised_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "raised_at": string;
} | null;

export type PortiaCBA6630CFA91012BCB9C1FDEAAE964104EC054B36A1E0C6FE396D235D74C8C4F1 = {
  "expected_evidence_index": number | string | null;
  "kind": string;
  "reference": string;
  "description"?: string | null;
  "resolution"?: string | null;
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

export type PortiaD1911225671F0102A93760A010419412AB90210CE93B2AFC1084DF988DE0A198 = {
  "tenant_id": string;
  "application_id": string;
  "as_of": string;
  "instances": Array<{
  "system_instance_id": string;
  "name": string;
  "scope_status": string;
  "coverage": string;
  "population_id": string | null;
  "snapshot_id": string | null;
  "observed_at": string | null;
  "exception": {
  "exception_id": string;
  "system_instance_id": string;
  "reason": string;
  "expires_at": string;
  "approved_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "approved_at": string;
} | null;
} | null>;
} | null;

export type PortiaD38366C0F7A014B7D38BCF2DDAEABAEB55C1BD6883615DFD34DDBA3D43322457 = {
  "tenant_id": string;
  "population_id": string;
  "snapshot_id": string;
  "calculation_id": string;
  "observed_at": string;
  "items": Array<{
  "category": string;
  "provider_subject_id": string;
  "provider_entitlement_id": string | null;
  "privileged": boolean;
  "findings": Array<{
  "kind": string;
  "expectation_id": string | null;
  "exception_id": string | null;
  "explanation": string;
} | null>;
} | null>;
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

export type PortiaD6BB547EF6FB03ADDD6888D292BD58FA5D2D19E4D01A028E872206CC71819530 = {
  "course_name": string;
  "description": string | null;
  "audience_kind": string;
  "audience_teams": Array<string | null> | null;
  "delivery_source": string;
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

export type PortiaD903C9F6E761AA93918B6873BE31012F4F4BD0B196CC911D5AE8BD35E6B778B0 = Array<{
  "kind": string;
  "reference": string;
  "version": string;
} | null> | null;

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

export type PortiaDCF1EC4C8F6FE11762AB8AED23484BA812024D7817D6BDDEF7F58B3DFFA66AE3 = {
  "exception_id": string;
  "rationale": string;
  "expires_at": string | null;
  "approved_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "approved_at": string;
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

export type PortiaE34EE1C8D184A6D16AD1FE0849FC8B94B131A1A6FA453BB4C4FF29D2EC239B8C = Array<{
  "group_provider_subject_id": string;
  "member_provider_subject_id": string;
} | null> | null;

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

export type PortiaE743D385FF26935D87C27AF6254C90A88FE79C15319CDD62451780019AF8EDBD = Array<{
  "reminder_id": string;
  "kind": string;
  "work_item_id": string;
  "summary": string;
  "due_on": string | null;
  "days_overdue": number | string;
  "action_path": string;
} | null> | null;

export type PortiaE7F9DFDF55BC2B0ECD9A9FF37523ADE10CA787A7D4B97D66CCE5EAF13D29AFA4 = {
  "completion_id": string;
  "person_id": string;
  "requirement_version": number | string;
  "completed_on": string;
  "source": string;
  "evidence_reference": string;
  "recorder": {
  "kind": string;
  "id": string;
  "display": string;
};
  "recorded_at": string;
} | null;

export type PortiaE86118FEDCAAD9C8EECB6F0820286060A99630C598D8AE761402890F6BA8A2E2 = {
  "items": Array<{
  "reconciliation": number | string;
  "person_id": string;
  "display_name": string;
  "reason": string;
  "due_on": string | null;
  "roster_snapshot_id": string;
  "roster_content_sha256": string;
  "actor": {
  "kind": string;
  "id": string;
  "display": string;
};
  "amended_at": string;
} | null>;
  "next_cursor": string | null;
} | null;

export type PortiaE9B2575F51D37D152BB376D660B9FD59648CDADCCFB631542854BC97BC5CBA7D = {
  "responsibilities": Array<{
  "control_id": string;
  "plan_version_id": string;
  "role": string;
  "holder": {
  "kind": string;
  "id": string;
};
  "effective_from": string;
  "effective_until": string | null;
  "cadence_description": string;
} | null>;
  "items": Array<{
  "kind": string;
  "source_id": string;
  "control_id": string | null;
  "finding_id": string | null;
  "summary": string;
  "due_on": string | null;
  "overdue": boolean;
  "materiality": string | null;
} | null>;
} | null;

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

export type PortiaF04DF7A5E293A53D9AB5D519AC3C499DF057DE3B1C4CFD202ABDCB850A627B54 = {
  "tenant_id": string;
  "program_id": string;
  "control_id": string;
  "occurrence_id": string;
  "revision": number | string;
  "kind": string;
  "state": string;
  "control_version_id": string;
  "plan_version_id": string;
  "period_start": string | null;
  "period_end": string | null;
  "due_on": string | null;
  "trigger": string | null;
  "assignee": {
  "kind": string;
  "id": string;
};
  "attestations": Array<{
  "attestation_id": string;
  "version": number | string;
  "result": string;
  "performed_at": string;
  "covered_from": string | null;
  "covered_until": string | null;
  "notes": string | null;
  "rationale": string | null;
  "evidence": Array<{
  "expected_evidence_index": number | string | null;
  "kind": string;
  "reference": string;
  "description"?: string | null;
  "resolution"?: string | null;
} | null>;
  "performed_by": {
  "kind": string;
  "id": string;
};
  "recorder_member_id": string;
  "recorded_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "recorded_at": string;
  "control_version_id": string;
  "plan_version_id": string;
  "expected_evidence": Array<string | null>;
  "correction_reason"?: string | null;
  "supersedes_attestation_id"?: string | null;
} | null>;
  "reviews": Array<{
  "decision_id": string;
  "attestation_id": string;
  "attestation_version": number | string;
  "outcome": string;
  "rationale": string;
  "requested_actions": Array<string | null>;
  "reviewer_member_id": string;
  "reviewed_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "reviewed_at": string;
  "separation_of_duties_waiver_id"?: string | null;
} | null>;
  "reassignments": Array<{
  "occurrence_id": string;
  "from": {
  "kind": string;
  "id": string;
} | null;
  "to": {
  "kind": string;
  "id": string;
};
  "plan_version_id": string;
} | null>;
} | null;

export type PortiaF38F4E93C39265245C5ADAB3FA4776FF096BEF74C5FFABFD2BC49604E5DD0416 = {
  "treatment_id": string;
  "revision": number | string;
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

export type PortiaF7F18D546C1EB99D8AF0A43E4B93EF618B497FF7B4FC9EE8E09418D29FD14BD0 = {
  "exception_id": string;
  "system_instance_id": string;
  "reason": string;
  "expires_at": string;
  "approved_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "approved_at": string;
} | null;

export type PortiaF81CA4F12726F70D1A19DD820214E8112F0BB998595F2B4B96ACC142CEDA1289 = {
  "tenant_id": string;
  "program_id": string;
  "policy_id": string;
  "revision": number | string;
  "predecessor_version": number | string | null;
  "changed_fields": Array<string | null>;
  "added_applicability": Array<{
  "subject_type": string;
  "subject": string;
  "record_id"?: string | null;
} | null>;
  "removed_applicability": Array<PortiaF81CA4F12726F70D1A19DD820214E8112F0BB998595F2B4B96ACC142CEDA12891>;
  "retained_applicability": Array<PortiaF81CA4F12726F70D1A19DD820214E8112F0BB998595F2B4B96ACC142CEDA12891>;
  "affected_campaign_ids": Array<string>;
  "audience_changed": boolean;
  "digest": string;
} | null;

export type PortiaF81CA4F12726F70D1A19DD820214E8112F0BB998595F2B4B96ACC142CEDA12891 = {
  "subject_type": string;
  "subject": string;
  "record_id"?: string | null;
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

export type PortiaFDFB5EB07BD18FCE44E06DBD37F46C64A2C4C2935EAE7D0785664C275FB1204A = {
  "tenant_id": string;
  "program_id": string;
  "control_id": string;
  "plan_version_id": string;
  "revision": number | string;
  "status": string;
  "control_version_id": string;
  "owner": {
  "kind": string;
  "id": string;
};
  "backup_owner": {
  "kind": string;
  "id": string;
} | null;
  "reviewer_member_id": string;
  "cadence": {
  "kind": string;
  "frequency"?: string | null;
  "first_period_start"?: string | null;
  "due_within_days"?: number | string | null;
  "trigger"?: string | null;
};
  "cadence_description": string;
  "expected_evidence": Array<string | null>;
  "effective_from": string;
  "effective_until": string | null;
  "rationale": string;
  "proposer_member_id": string;
  "proposed_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "proposed_at": string;
  "proposal_separation_of_duties_waiver_id"?: string | null;
  "approved_by"?: {
  "kind": string;
  "id": string;
  "display": string;
} | null;
  "approved_at"?: string | null;
  "approval_rationale"?: string | null;
  "approval_separation_of_duties_waiver_id"?: string | null;
  "reassignments"?: Array<{
  "occurrence_id": string;
  "from": {
  "kind": string;
  "id": string;
} | null;
  "to": {
  "kind": string;
  "id": string;
};
  "plan_version_id": string;
} | null> | null;
} | null;

export type PortiaFF23C6AFCFE989210F90258F60DB04711DE3E6CCC9A94A0EF8BEC518127F86BE = Array<{
  "provider_subject_id": string;
  "principal_kind": string;
  "display_name": string;
  "status": string;
  "email"?: string | null;
} | null> | null;

export type PortiaFF553F232A8414665BAE406E202CCC0CC27AC9885754BE84D83F540E440E0733 = {
  "tenant_id": string;
  "program_id": string;
  "campaign_id": string;
  "subject": {
  "kind": string;
  "record_id": string;
  "identifier": string;
  "title": string;
  "version": number | string;
  "content_sha256": string;
};
  "audience_kind": string;
  "audience_teams": Array<string | null>;
  "roster_snapshot_id": string;
  "roster_content_sha256": string;
  "latest_roster_snapshot_id": string;
  "launched_on": string;
  "due_on": string;
  "instructions": string | null;
  "acknowledgement_text": string | null;
  "status": string;
  "as_of": string;
  "totals": {
  "population": number | string;
  "launch_audience": number | string;
  "added": number | string;
  "removed": number | string;
  "pending": number | string;
  "overdue": number | string;
  "satisfied": number | string;
  "excepted": number | string;
};
  "launched_by": {
  "kind": string;
  "id": string;
  "display": string;
};
  "launched_at": string;
  "closed_by": {
  "kind": string;
  "id": string;
  "display": string;
} | null;
  "closed_at": string | null;
  "closing_totals": {
  "population": number | string;
  "launch_audience": number | string;
  "added": number | string;
  "removed": number | string;
  "pending": number | string;
  "overdue": number | string;
  "satisfied": number | string;
  "excepted": number | string;
} | null;
} | null;

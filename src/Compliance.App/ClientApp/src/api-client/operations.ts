import type { Portia1EB0255AC1C1D800F4E43175A476BE2EEF9E1FBDC2D4430BCED070CC5D913FAD, Portia534841146AA01FC616B441EB6CC816411AE29E916AB93C8329D97EB96B560E0A, Portia5DC40A4F1CB15D96F9110C5B5E53AA99EDE859D42D78207E7C39CA076C05FE94, Portia70B24E90A73EA0794A4DD21F876262A340BEE644ACBC9B8FF2F0890A79B9D474, PortiaA79F728CB0B620D8928F1E786F7B1B23A937E23A33A65D5C092E345876533FC4, PortiaEA6D5265BAC64B7B7ECBB403179F0802AEF5CDBFF6D32E0FCDF3EF54CB637D54 } from "./schemas";

export type ContinueWithDeveloperIdentityBody = {
  "email_address": string | null;
};

export type ContinueWithDeveloperIdentityResponse200 = Portia70B24E90A73EA0794A4DD21F876262A340BEE644ACBC9B8FF2F0890A79B9D474;

export type ContinueWithDeveloperIdentityError_400 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type ContinueWithDeveloperIdentityError_401 = undefined;

export type ContinueWithDeveloperIdentityError_403 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type ContinueWithDeveloperIdentityError_404 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type ContinueWithDeveloperIdentityError_409 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type ContinueWithDeveloperIdentityError_413 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type ContinueWithDeveloperIdentityError_415 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type ContinueWithDeveloperIdentityError_500 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type RegisterTenantBody = {
  "name": string | null;
  "slug": string | null;
};

export type RegisterTenantResponse200 = Portia5DC40A4F1CB15D96F9110C5B5E53AA99EDE859D42D78207E7C39CA076C05FE94;

export type RegisterTenantError_400 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type RegisterTenantError_401 = undefined;

export type RegisterTenantError_403 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type RegisterTenantError_404 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type RegisterTenantError_409 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type RegisterTenantError_413 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type RegisterTenantError_415 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type RegisterTenantError_500 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type ListMyTenantsQuery = {
  "cursor"?: string | null;
  "limit"?: number | string;
};

export type ListMyTenantsResponse200 = PortiaA79F728CB0B620D8928F1E786F7B1B23A937E23A33A65D5C092E345876533FC4;

export type ListMyTenantsError_400 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type ListMyTenantsError_401 = undefined;

export type ListMyTenantsError_403 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type ListMyTenantsError_404 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type ListMyTenantsError_409 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type ListMyTenantsError_413 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type ListMyTenantsError_415 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type ListMyTenantsError_500 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type RequestTenantSlugSurrenderPath = {
  "slug": string | null;
  "tenantId": string;
};

export type RequestTenantSlugSurrenderResponse204 = undefined;

export type RequestTenantSlugSurrenderError_400 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type RequestTenantSlugSurrenderError_401 = undefined;

export type RequestTenantSlugSurrenderError_403 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type RequestTenantSlugSurrenderError_404 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type RequestTenantSlugSurrenderError_409 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type RequestTenantSlugSurrenderError_413 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type RequestTenantSlugSurrenderError_415 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type RequestTenantSlugSurrenderError_500 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type ListTeamsPath = {
  "tenantId": string;
};

export type ListTeamsQuery = {
  "cursor"?: string | null;
  "limit"?: number | string;
  "search"?: string | null;
  "sort"?: string | null;
};

export type ListTeamsResponse200 = PortiaEA6D5265BAC64B7B7ECBB403179F0802AEF5CDBFF6D32E0FCDF3EF54CB637D54;

export type ListTeamsError_400 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type ListTeamsError_401 = undefined;

export type ListTeamsError_403 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type ListTeamsError_404 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type ListTeamsError_409 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type ListTeamsError_413 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type ListTeamsError_415 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type ListTeamsError_500 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type GetTeamPath = {
  "teamId": string;
  "tenantId": string;
};

export type GetTeamResponse200 = Portia1EB0255AC1C1D800F4E43175A476BE2EEF9E1FBDC2D4430BCED070CC5D913FAD;

export type GetTeamError_400 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type GetTeamError_401 = undefined;

export type GetTeamError_403 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type GetTeamError_404 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type GetTeamError_409 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type GetTeamError_413 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type GetTeamError_415 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type GetTeamError_500 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type DefineTeamPath = {
  "teamId": string;
  "tenantId": string;
};

export type DefineTeamBody = {
  "name": string | null;
};

export type DefineTeamResponse204 = undefined;

export type DefineTeamError_400 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type DefineTeamError_401 = undefined;

export type DefineTeamError_403 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type DefineTeamError_404 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type DefineTeamError_409 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type DefineTeamError_413 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type DefineTeamError_415 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type DefineTeamError_500 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type DeleteTeamPath = {
  "teamId": string;
  "tenantId": string;
};

export type DeleteTeamResponse204 = undefined;

export type DeleteTeamError_400 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type DeleteTeamError_401 = undefined;

export type DeleteTeamError_403 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type DeleteTeamError_404 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type DeleteTeamError_409 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type DeleteTeamError_413 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type DeleteTeamError_415 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type DeleteTeamError_500 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type ListTeamMembersPath = {
  "teamId": string;
  "tenantId": string;
};

export type ListTeamMembersQuery = {
  "cursor"?: string | null;
  "limit"?: number | string;
  "search"?: string | null;
  "sort"?: string | null;
};

export type ListTeamMembersResponse200 = Portia534841146AA01FC616B441EB6CC816411AE29E916AB93C8329D97EB96B560E0A;

export type ListTeamMembersError_400 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type ListTeamMembersError_401 = undefined;

export type ListTeamMembersError_403 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type ListTeamMembersError_404 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type ListTeamMembersError_409 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type ListTeamMembersError_413 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type ListTeamMembersError_415 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type ListTeamMembersError_500 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type AssignTeamMemberPath = {
  "memberId": string;
  "teamId": string;
  "tenantId": string;
};

export type AssignTeamMemberResponse204 = undefined;

export type AssignTeamMemberError_400 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type AssignTeamMemberError_401 = undefined;

export type AssignTeamMemberError_403 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type AssignTeamMemberError_404 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type AssignTeamMemberError_409 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type AssignTeamMemberError_413 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type AssignTeamMemberError_415 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type AssignTeamMemberError_500 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type RemoveTeamMemberPath = {
  "memberId": string;
  "teamId": string;
  "tenantId": string;
};

export type RemoveTeamMemberResponse204 = undefined;

export type RemoveTeamMemberError_400 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type RemoveTeamMemberError_401 = undefined;

export type RemoveTeamMemberError_403 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type RemoveTeamMemberError_404 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type RemoveTeamMemberError_409 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type RemoveTeamMemberError_413 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type RemoveTeamMemberError_415 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type RemoveTeamMemberError_500 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

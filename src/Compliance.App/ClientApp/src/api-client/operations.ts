import type { Portia1EB0255AC1C1D800F4E43175A476BE2EEF9E1FBDC2D4430BCED070CC5D913FAD, Portia411B3239057A525C3562E4BE0D1A71F30A3844A0A56231069471136CDD4A37E4, Portia444702261363EF785C20C78504EBD948CC2950CA076C0FEA93FBD650F83E4680, Portia445823D45DEBC21876B8F7D21A7706F96E8DB18D68A39E931F58C101C313F09E, Portia534841146AA01FC616B441EB6CC816411AE29E916AB93C8329D97EB96B560E0A, Portia5DC40A4F1CB15D96F9110C5B5E53AA99EDE859D42D78207E7C39CA076C05FE94, Portia70B24E90A73EA0794A4DD21F876262A340BEE644ACBC9B8FF2F0890A79B9D474, PortiaA79F728CB0B620D8928F1E786F7B1B23A937E23A33A65D5C092E345876533FC4, PortiaEA6D5265BAC64B7B7ECBB403179F0802AEF5CDBFF6D32E0FCDF3EF54CB637D54, PortiaEC8A68FE610BE733B5FB5E10D0C0A79743573CEEBB9114A5BBD9B4542987EC3B } from "./schemas";

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

export type ListRolesPath = {
  "tenantId": string;
};

export type ListRolesQuery = {
  "cursor"?: string | null;
  "limit"?: number | string;
  "search"?: string | null;
  "sort"?: string | null;
};

export type ListRolesResponse200 = Portia411B3239057A525C3562E4BE0D1A71F30A3844A0A56231069471136CDD4A37E4;

export type ListRolesError_400 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type ListRolesError_401 = undefined;

export type ListRolesError_403 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type ListRolesError_404 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type ListRolesError_409 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type ListRolesError_413 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type ListRolesError_415 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type ListRolesError_500 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type GetRolePath = {
  "roleId": string;
  "tenantId": string;
};

export type GetRoleResponse200 = Portia444702261363EF785C20C78504EBD948CC2950CA076C0FEA93FBD650F83E4680;

export type GetRoleError_400 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type GetRoleError_401 = undefined;

export type GetRoleError_403 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type GetRoleError_404 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type GetRoleError_409 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type GetRoleError_413 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type GetRoleError_415 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type GetRoleError_500 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type DefineRolePath = {
  "roleId": string;
  "tenantId": string;
};

export type DefineRoleBody = {
  "name": string | null;
};

export type DefineRoleResponse204 = undefined;

export type DefineRoleError_400 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type DefineRoleError_401 = undefined;

export type DefineRoleError_403 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type DefineRoleError_404 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type DefineRoleError_409 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type DefineRoleError_413 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type DefineRoleError_415 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type DefineRoleError_500 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type DeleteRolePath = {
  "roleId": string;
  "tenantId": string;
};

export type DeleteRoleResponse204 = undefined;

export type DeleteRoleError_400 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type DeleteRoleError_401 = undefined;

export type DeleteRoleError_403 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type DeleteRoleError_404 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type DeleteRoleError_409 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type DeleteRoleError_413 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type DeleteRoleError_415 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type DeleteRoleError_500 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type ListRolePermissionsPath = {
  "roleId": string;
  "tenantId": string;
};

export type ListRolePermissionsQuery = {
  "cursor"?: string | null;
  "limit"?: number | string;
  "search"?: string | null;
  "sort"?: string | null;
};

export type ListRolePermissionsResponse200 = PortiaEC8A68FE610BE733B5FB5E10D0C0A79743573CEEBB9114A5BBD9B4542987EC3B;

export type ListRolePermissionsError_400 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type ListRolePermissionsError_401 = undefined;

export type ListRolePermissionsError_403 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type ListRolePermissionsError_404 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type ListRolePermissionsError_409 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type ListRolePermissionsError_413 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type ListRolePermissionsError_415 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type ListRolePermissionsError_500 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type AssignRolePermissionPath = {
  "permission": string | null;
  "roleId": string;
  "tenantId": string;
};

export type AssignRolePermissionResponse204 = undefined;

export type AssignRolePermissionError_400 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type AssignRolePermissionError_401 = undefined;

export type AssignRolePermissionError_403 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type AssignRolePermissionError_404 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type AssignRolePermissionError_409 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type AssignRolePermissionError_413 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type AssignRolePermissionError_415 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type AssignRolePermissionError_500 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type RemoveRolePermissionPath = {
  "permission": string | null;
  "roleId": string;
  "tenantId": string;
};

export type RemoveRolePermissionResponse204 = undefined;

export type RemoveRolePermissionError_400 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type RemoveRolePermissionError_401 = undefined;

export type RemoveRolePermissionError_403 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type RemoveRolePermissionError_404 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type RemoveRolePermissionError_409 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type RemoveRolePermissionError_413 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type RemoveRolePermissionError_415 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type RemoveRolePermissionError_500 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type ListRoleTeamsPath = {
  "roleId": string;
  "tenantId": string;
};

export type ListRoleTeamsQuery = {
  "cursor"?: string | null;
  "limit"?: number | string;
  "search"?: string | null;
  "sort"?: string | null;
};

export type ListRoleTeamsResponse200 = Portia445823D45DEBC21876B8F7D21A7706F96E8DB18D68A39E931F58C101C313F09E;

export type ListRoleTeamsError_400 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type ListRoleTeamsError_401 = undefined;

export type ListRoleTeamsError_403 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type ListRoleTeamsError_404 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type ListRoleTeamsError_409 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type ListRoleTeamsError_413 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type ListRoleTeamsError_415 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type ListRoleTeamsError_500 = {
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

export type AssignTeamRolePath = {
  "roleId": string;
  "teamId": string;
  "tenantId": string;
};

export type AssignTeamRoleResponse204 = undefined;

export type AssignTeamRoleError_400 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type AssignTeamRoleError_401 = undefined;

export type AssignTeamRoleError_403 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type AssignTeamRoleError_404 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type AssignTeamRoleError_409 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type AssignTeamRoleError_413 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type AssignTeamRoleError_415 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type AssignTeamRoleError_500 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type RemoveTeamRolePath = {
  "roleId": string;
  "teamId": string;
  "tenantId": string;
};

export type RemoveTeamRoleResponse204 = undefined;

export type RemoveTeamRoleError_400 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type RemoveTeamRoleError_401 = undefined;

export type RemoveTeamRoleError_403 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type RemoveTeamRoleError_404 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type RemoveTeamRoleError_409 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type RemoveTeamRoleError_413 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type RemoveTeamRoleError_415 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

export type RemoveTeamRoleError_500 = {
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
};

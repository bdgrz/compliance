import { defineApi, createClient, del, empty, get, json, post } from "@askrjs/fetch";
import type { ClientOptions } from "@askrjs/fetch";
import type { Portia1EB0255AC1C1D800F4E43175A476BE2EEF9E1FBDC2D4430BCED070CC5D913FAD, Portia534841146AA01FC616B441EB6CC816411AE29E916AB93C8329D97EB96B560E0A, Portia5DC40A4F1CB15D96F9110C5B5E53AA99EDE859D42D78207E7C39CA076C05FE94, Portia70B24E90A73EA0794A4DD21F876262A340BEE644ACBC9B8FF2F0890A79B9D474, PortiaA79F728CB0B620D8928F1E786F7B1B23A937E23A33A65D5C092E345876533FC4, PortiaEA6D5265BAC64B7B7ECBB403179F0802AEF5CDBFF6D32E0FCDF3EF54CB637D54 } from "./schemas";
import type { AssignTeamMemberPath, DefineTeamPath, DeleteTeamPath, GetTeamPath, ListMyTenantsQuery, ListTeamMembersPath, ListTeamMembersQuery, ListTeamsPath, ListTeamsQuery, RemoveTeamMemberPath, RequestTenantSlugSurrenderPath } from "./operations";

export const api = defineApi({
  continueWithDeveloperIdentity: post("/api/v1/developer-user-sessions")
    .body(json<{
  "email_address": string | null;
}>())
    .returns(json<Portia70B24E90A73EA0794A4DD21F876262A340BEE644ACBC9B8FF2F0890A79B9D474>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  registerTenant: post("/api/v1/tenants")
    .body(json<{
  "name": string | null;
  "slug": string | null;
}>())
    .returns(json<Portia5DC40A4F1CB15D96F9110C5B5E53AA99EDE859D42D78207E7C39CA076C05FE94>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listMyTenants: get("/api/v1/tenants/mine")
    .query<ListMyTenantsQuery>({ "limit": { style: "form", explode: true } })
    .returns(json<PortiaA79F728CB0B620D8928F1E786F7B1B23A937E23A33A65D5C092E345876533FC4>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  requestTenantSlugSurrender: del("/api/v1/tenants/{tenantId}/slugs/{slug}")
    .params<RequestTenantSlugSurrenderPath>({ "slug": { style: "simple", explode: false }, "tenantId": { style: "simple", explode: false } })
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listTeams: get("/api/v1/tenants/{tenantId}/teams")
    .params<ListTeamsPath>({ "tenantId": { style: "simple", explode: false } })
    .query<ListTeamsQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true }, "search": { style: "form", explode: true }, "sort": { style: "form", explode: true } })
    .returns(json<PortiaEA6D5265BAC64B7B7ECBB403179F0802AEF5CDBFF6D32E0FCDF3EF54CB637D54>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getTeam: get("/api/v1/tenants/{tenantId}/teams/{teamId}")
    .params<GetTeamPath>({ "teamId": { style: "simple", explode: false }, "tenantId": { style: "simple", explode: false } })
    .returns(json<Portia1EB0255AC1C1D800F4E43175A476BE2EEF9E1FBDC2D4430BCED070CC5D913FAD>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  defineTeam: post("/api/v1/tenants/{tenantId}/teams/{teamId}")
    .params<DefineTeamPath>({ "teamId": { style: "simple", explode: false }, "tenantId": { style: "simple", explode: false } })
    .body(json<{
  "name": string | null;
}>())
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  deleteTeam: del("/api/v1/tenants/{tenantId}/teams/{teamId}")
    .params<DeleteTeamPath>({ "teamId": { style: "simple", explode: false }, "tenantId": { style: "simple", explode: false } })
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listTeamMembers: get("/api/v1/tenants/{tenantId}/teams/{teamId}/members")
    .params<ListTeamMembersPath>({ "teamId": { style: "simple", explode: false }, "tenantId": { style: "simple", explode: false } })
    .query<ListTeamMembersQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true }, "search": { style: "form", explode: true }, "sort": { style: "form", explode: true } })
    .returns(json<Portia534841146AA01FC616B441EB6CC816411AE29E916AB93C8329D97EB96B560E0A>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  assignTeamMember: post("/api/v1/tenants/{tenantId}/teams/{teamId}/members/{memberId}")
    .params<AssignTeamMemberPath>({ "memberId": { style: "simple", explode: false }, "teamId": { style: "simple", explode: false }, "tenantId": { style: "simple", explode: false } })
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  removeTeamMember: del("/api/v1/tenants/{tenantId}/teams/{teamId}/members/{memberId}")
    .params<RemoveTeamMemberPath>({ "memberId": { style: "simple", explode: false }, "teamId": { style: "simple", explode: false }, "tenantId": { style: "simple", explode: false } })
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
}, {
  "servers": [],
  "securitySchemes": {}
});

export const createApiClient = (options?: ClientOptions) => createClient(api, options);

# Shelf

Static documentation hosting platform for Cocoar products (docs.cocoar.dev).
Serves VitePress-generated documentation with multi-product and multi-version support,
plus a Vue 3 admin UI, analytics, and CI/CD upload API.

Internal knowledge (architecture decisions, deployment runbook, dev-environment context)
lives in **Atlas → `shelf/`** — the public product docs are the VitePress site in `website/`.

## Build & Test

```bash
dotnet build ./src -c Release
dotnet test ./src -c Release          # needs Docker (Testcontainers PostgreSQL)
cd src/Cocoar.Shelf.Client && pnpm run build   # Vue client → ../Cocoar.Shelf/wwwroot
```

Local dev quickstart (modgud + mailpit stack, seed, test logins): `dev/README.md`.

## Architecture

- **ASP.NET Core (net10.0)** + **Marten/PostgreSQL** (required — no file-only mode) +
  **Vue 3 SPA** (`src/Cocoar.Shelf.Client`, @cocoar/vue-ui + vue-data-grid, desktop-only)
- Docs on the filesystem: `{DocsRoot}/{product}/{version}/`, versions auto-detected
  (SemVer regex), highest stable = "latest", `FileSystemWatcher` invalidates caches
- **Base path rewriting**: VitePress builds with `base: '/'` are rewritten to
  `/{product}/{version}/` in HTML/CSS/JS responses (`DocsRoutingMiddleware` + `BasePathRewriter`)
- Products, users, settings and the access log live in PostgreSQL (schema `shelf`);
  JSON files in `{ConfigRoot}/products/` are imported once at startup (DB wins)

### Authentication (modgud federation, OIDC code flow + SSO)

All human auth is federated to **modgud** (Cocoar's OIDC IdP) — BFF with the standard
authorization-code flow (citydiary pattern, code + PKCE):

- `GET /login` challenges modgud; the callback (`/signin-oidc`) JIT-provisions the user and
  mints the `shelf.auth` cookie (`OnTicketReceived` swaps in the Identity principal). Tokens
  stay server-side (`SaveTokens` only for the logout `id_token_hint`). An existing modgud
  browser session logs in silently → SSO across Cocoar apps. `GET /logout` ends cookie AND
  modgud session. Unauthenticated `/admin` auto-challenges; the landing page has a Sign-in
  button. Client `shelf-web` needs: grant `authorization_code`, redirect `/signin-oidc` +
  `/signout-callback-oidc` URIs, all six scopes, Access Token Type JWT.
- Thin local user layer: `UserDocument` (Id == modgud `sub`), JIT-created at login.
  ASP.NET Identity plumbing exists only for the cookie/SecurityStamp pipeline and the
  test seam (`/_api/test/signin`, mapped only when `ShelfOptions.TestAuth`).
- RBAC: `resource_access[shelf]` stamped on the cookie, flattened per request
  (`ModgudClaimsTransformation`); **Admin** = permission `shelf:admin` OR email in
  `Modgud.Admins` allowlist (`AdminCheck`, wired as the `"Admin"` policy).
- API keys for CI/CD (`ApiKeyFilter`): per-product key → UI-managed master key
  (`ShelfSettings` doc) → config/env bootstrap key. Keys are readable for admins
  (deliberate: docs hosting, not an IdP).
- **Product writes require admin** on the cookie path (`ApiKeyFilter` → `IAccessResolver`);
  the Bearer API-key path is unchanged (machine track).

### Access control v2 (restricted products, groups, principals)

- `ProductConfig.Restricted` (orthogonal to `Visibility`) hides a product from anyone without a
  read grant; `DocsRoutingMiddleware` gates it — authed-no-grant → **404** (no existence leak),
  anonymous HTML → login redirect, anonymous asset → 401. Products API / landing / `llms.txt`
  filter restricted products out for non-grantees.
- **Groups** (`Group` doc) are permission carriers: `MemberEmails` (explicit, stageable pre-login)
  and/or `MembershipMode.Auto` with a **JsEval** predicate over the persisted login **claims
  snapshot** (`UserDocument.Claims/Permissions`, refreshed each login). Auto-membership is
  materialized (`AutoMemberUserIds`) by `IGroupMembershipRecalculator` at login / group save /
  manual. `IsAdminGroup` grants adminship.
- Grants live **on the product**: `ProductConfig.ReadPrincipals` (a `PrincipalRef` list). Both
  `Group` and `UserDocument` implement `IPrincipal`, so groups and individual users (by email)
  are assignable. `IAccessResolver` (per-request, memoized) resolves `{isAdmin, groupIds, email}`
  and `CanRead(product)` — no JsEval in the hot path.
- **Admin** = `shelf:admin` token permission OR `Modgud.Admins` allowlist OR an `IsAdminGroup`
  group (via `IAccessResolver.IsAdminAsync`, wired into the `"Admin"` policy, `ApiKeyFilter`, `/auth/me`).
- JsEval: `Cocoar.JsEval.Engine` (copy+adapt from timetodo, in-memory — no event sourcing / SQL
  translation); predicates are sandboxed and **fail-closed**.

## Configuration

`data/configuration.json` (Cocoar.Configuration) + env overrides with `Shelf__` prefix.
NOTE: the app reads the *build output copy* of the file — config edits need a build.

Key options: `Database.ConnectionString` (**required**), `Modgud.{Issuer,Audience,
WebClientId,WebClientSecret,AdminPermission,Admins}` — `Issuer` may be the App-Origin
subdomain (branded login, e.g. `https://shelf.auth.cocoar.dev`), `ApiKey` (bootstrap master key),
`AccessLog.{Enabled,RetentionDays}`, `DocsRoot`, `ConfigRoot`, `PathBase`, `VersionPattern`,
`MaxUploadSizeBytes`, `TestAuth` (test fixture only).

## API surface (`/_api`)

- Public read: `GET /products`, `GET /products/{p}`, `GET /products/{p}/versions`, `GET /shelf-config`
  (restricted products filtered out / 404 for non-grantees, per request via `IAccessResolver`)
- Product CRUD, version upload/delete (`ApiKeyFilter`) — Bearer key, or **admin** cookie
- Auth: login is the server route `GET /login` (OIDC challenge); `GET /logout`, `GET /_api/auth/me`
- Admin policy: `/settings/*` (master key), `/products/{p}/api-key` (reveal), `/users/*`,
  `/analytics/*` (visits+summary with `product`/`from`/`to`/`excludeIps`, GeoIP status/download),
  `/groups/*` (CRUD, `/recalculate`, `/test-script` dry-run), `/principals` (assignable groups+users)

## Key Files

Backend (`src/Cocoar.Shelf/`):
- `Program.cs` — startup, Marten schema, auth wiring, middleware pipeline
- `ShelfOptions.cs` — all configuration (incl. `ModgudOptions`)
- `Identity/` — ModgudUserProvisioning, ModgudClaimsTransformation, ClaimsSnapshot,
  RbacCookiePreservation, AdminCheck, MartenUserStore
- `Models/` — ProductConfig (+`Restricted`, `ReadPrincipals`, `Openness`, `RepositoryUrl`), Group,
  Principal (`IPrincipal`/`PrincipalRef`), ProductOpenness (enum), UserDocument (+claims snapshot),
  AccessLogEntry (+lat/lng)
- `Endpoints/` — ApiEndpoints (products/versions), AuthEndpoints, SettingsEndpoints,
  UserEndpoints, AnalyticsEndpoints, GroupEndpoints, PrincipalEndpoints, TestAuthEndpoints, ApiKeyFilter
- `Middleware/DocsRoutingMiddleware.cs` — docs routing, rewriting, access-log recording
  (HTML GETs only), restricted-product gating
- `Services/` — ManifestService, UploadService, SettingsService, GeoIpService,
  AccessLogChannel/PersistenceService, MartenProductConfigService + migration,
  MartenGroupService, BasePathDetector/Rewriter
- `Services/Access/` — IAccessResolver, IGroupMembershipEvaluator (JsEval),
  IGroupMembershipRecalculator, ILoginAccessProcessor

Frontend (`src/Cocoar.Shelf.Client/src/`):
- Routed modals via `@cocoar/vue-fragment-parser` (`route.meta.routedFragments`, URL-hash
  `#create` / `#<id>`; fixed height via `overlayOptions`); shared shell `components/ModalLayout.vue`
- `layouts/AppLayout.vue` — one shared shell for landing + admin (header always; sidebar for
  logged-in admins everywhere); `views/LandingView.vue` renders inside it
- `views/products/` — grid + tabbed edit modal (General / **Access** / Tags & API / Versions);
  General tab = Openness (`ProductOpenness`) select + optional Repository URL; Access tab =
  Restricted toggle + principal picker (groups + users)
- `views/LandingView.vue` — product cards show an **openness badge** (Open Source / Proprietary,
  none when unspecified) + an optional "Source ↗" repo link; toolbar filters by tag AND openness
  (`preferences.store` persists `opennessFilter`)
- `views/groups/` — Groups grid + tabbed modal (General / Members / Auto-membership with
  `CoarScriptEditor` from `@cocoar/vue-script-editor` + dry-run)
- `views/admin/` — GeneralSettings (master key), Users, Groups, AccessLog, GeoIP;
  `views/AnalyticsView.vue` — dashboard with world map (`@cocoar/vue-map`), flags (`flag-icons`)
- `core/geo.ts` — country code → name (`Intl.DisplayNames`) + SVG flag class (flag-icons;
  emoji flags don't render on Windows)
- Conventions: labels via `CoarFormField` (inputs have NO label prop — also true in vue-ui 2.x), card
  titles via `CoarCard` `#header` slot, destructive actions via `useDialog().confirm`,
  grids bind `rowDataRef(computed(() => store.items))` to Pinia stores; bump
  `persistColumnState` keys when changing columns

## Testing

- Integration tests: Testcontainers PostgreSQL, in-process host (`ShelfFixture` sets
  `Shelf__TestAuth=true` and signs in via the seam — no modgud needed)
- `TestSignInRequest.Admin = true` stamps a modgud-shaped `resource_access` so the real
  RBAC path is exercised

## Design Decisions

- **modgud owns identity** — Shelf keeps only the thin mirror; no local credentials ever
- **PostgreSQL is mandatory** — the file-based fallback was removed with the federation
- **DB is the source of truth for products** — JSON files are a one-time startup seed
- **Public read endpoints stay unauthenticated**; keys/admin data are admin-gated
- **Access log counts page views** (HTML GETs), not assets or HEAD requests
- **Admin UI is desktop-only** — no mobile optimization
- **Access control is opt-in** — `Restricted` defaults false; unrestricted products behave exactly as before
- **Grants live on the product** (`ReadPrincipals`), not the group — you assign who may read where you toggle Restricted; groups/users are both `IPrincipal`
- **Restricted → 404, not 403** for authed-no-grant (no existence leak); restricted products are fully invisible (no teaser)
- **Analytics aggregation is server-side** — the browser only renders; country flags/names are derived client-side from the ISO code (display only)
- **modgud owns identity, Shelf owns authorization** — groups/grants reference products, which only exist in Shelf, so revocation is immediate (per request, not next login)
- **Openness is a display marker, not access control** — `ProductConfig.Openness` (Open Source / Proprietary / Unspecified) only drives a landing badge + filter; proprietary docs are hosted publicly and findable, just clearly labeled. Hide docs with `Restricted`, not `Openness`. The repo link lives in the product's own VitePress docs; Shelf adds only the optional `RepositoryUrl` "Source ↗" link — omit it for closed source

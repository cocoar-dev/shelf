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

## Configuration

`data/configuration.json` (Cocoar.Configuration) + env overrides with `Shelf__` prefix.
NOTE: the app reads the *build output copy* of the file — config edits need a build.

Key options: `Database.ConnectionString` (**required**), `Modgud.{Issuer,Audience,
WebClientId,WebClientSecret,AdminPermission,Admins}` — `Issuer` may be the App-Origin
subdomain (branded login, e.g. `https://shelf.auth.cocoar.dev`), `ApiKey` (bootstrap master key),
`AccessLog.{Enabled,RetentionDays}`, `DocsRoot`, `ConfigRoot`, `PathBase`, `VersionPattern`,
`MaxUploadSizeBytes`, `TestAuth` (test fixture only).

## API surface (`/_api`)

- Public: `GET /products`, `GET /products/{p}`, `GET /products/{p}/versions`, `GET /shelf-config`
- Cookie or Bearer key: product CRUD, version upload/delete (`ApiKeyFilter`)
- Auth: `POST /auth/otp/request|verify`, `POST /auth/logout`, `GET /auth/me`
- Admin policy: `/settings/*` (master key), `/products/{p}/api-key` (reveal), `/users/*`,
  `/analytics/*` (visits, summary, GeoIP status/download)

## Key Files

Backend (`src/Cocoar.Shelf/`):
- `Program.cs` — startup, Marten schema, auth wiring, middleware pipeline
- `ShelfOptions.cs` — all configuration (incl. `ModgudOptions`)
- `Identity/` — ModgudUserProvisioning, ModgudClaimsTransformation,
  RbacCookiePreservation, AdminCheck, MartenUserStore
- `Endpoints/` — ApiEndpoints (products/versions), AuthEndpoints, SettingsEndpoints,
  UserEndpoints, AnalyticsEndpoints, TestAuthEndpoints, ApiKeyFilter
- `Middleware/DocsRoutingMiddleware.cs` — docs routing, rewriting, access-log recording
  (HTML GETs only)
- `Services/` — ManifestService, UploadService, SettingsService, GeoIpService,
  AccessLogChannel/PersistenceService, MartenProductConfigService + migration,
  BasePathDetector/Rewriter

Frontend (`src/Cocoar.Shelf.Client/src/`):
- Routed modals via `@cocoar/vue-fragment-parser` (`route.meta.routedFragments`, URL-hash
  `#create` / `#<id>`; fixed height via `overlayOptions`); shared shell `components/ModalLayout.vue`
- `views/products/` — grid + tabbed edit modal (General / Tags & API / Versions)
- `views/admin/` — GeneralSettings (master key), Users, AccessLog, GeoIP; `views/AnalyticsView.vue`
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

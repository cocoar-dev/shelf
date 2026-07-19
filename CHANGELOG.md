# Changelog

All notable changes to Shelf will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/), and this project adheres to [Semantic Versioning](https://semver.org/).

## [2.1.1] — 2026-07-19

### Fixed

- **A deleted product no longer reappears after a restart.** The product JSON seed is now genuinely one-time: a `SeedState` marker records that the import has run, and the migration never re-reads the seed files again. Previously the seed re-imported any product that was missing from the database on every startup, so a product deleted via the UI/API was resurrected from its lingering seed file. Existing databases are marked seeded on first boot of this version without re-importing, so previously-deleted products stay deleted.

## [2.1.0] — 2026-07-19

Access control for restricted documentation, a much richer analytics dashboard,
and a unified app shell. Backwards-compatible: existing products stay public and
nothing changes until you mark a product restricted.

### Added

- **Restricted products & access control** — a product can be marked *restricted*: it is then hidden from anyone without a read grant and returns `404` on unauthorized access (no existence leak); anonymous visitors are sent to login, admins see everything. Orthogonal to visibility, so a product can be preview + restricted.
- **Permission groups** — groups grant read access to restricted products and, optionally, Shelf adminship. Membership is either an explicit email list (stageable before a user's first login) or **auto-computed** from a sandboxed JsEval predicate over the user's login claims (e.g. `user.email.endsWith('@cocoar.dev')`), recomputed at login / on save / on demand. Managed under Administration → Groups, with a script dry-run.
- **Product-side access grants** — assign who may read a restricted product directly on the product (Access tab): both **groups and individual users** are principals and can be granted. Users are keyed by email so access can be staged before their first login.
- **Detailed analytics dashboard** — an interactive **world map** of visitor cities (bubbles by volume), country and city breakdowns with **flags and full names**, browser / OS / language / referrer breakdowns, per-product filtering and an IP-exclude filter. All aggregation happens server-side.
- **Groups & principals API** — `GET/POST/PUT/DELETE /_api/groups`, `POST /_api/groups/recalculate`, `POST /_api/groups/test-script`, `GET /_api/principals`.

### Changed

- **Unified app shell** — the public landing page and the admin UI now share one shell: the same header everywhere (with dark/light toggle), the admin sidebar for logged-in admins on every page, and dark mode working on the landing page too.
- **Analytics summary** now returns cities, geolocated map points, and browser/OS/language/referrer breakdowns; the access log stores visitor lat/lng and shows country flags.
- Groups live under Administration alongside Users.

### Security

- **Product writes via the admin cookie now require admin.** Previously any authenticated (JIT-provisioned) user could create, update or delete products through the cookie path; this is now gated to admins. The CI/CD Bearer API-key path is unchanged.
- Admin can additionally be granted through an `IsAdminGroup` permission group (in addition to the `shelf:admin` token permission and the `Modgud.Admins` allowlist).

## [2.0.0] — 2026-07-18

Shelf 2.0 turns the standalone file-only server into a modgud-federated,
PostgreSQL-backed platform with a full admin UI. Docs serving is unchanged;
the CI/CD upload API keeps its shape and keys but now lives under `/_api`
(pipelines already targeting `/_api` keep working as-is).

### Added

- **Admin UI** (`/admin`) — Vue 3 SPA for managing products, versions, users, analytics and settings (desktop-only)
- **Federated login with SSO** — standard OIDC authorization-code flow (code + PKCE) against modgud; unauthenticated `/admin` visits challenge automatically, the landing page gets a *Sign in* button, and an existing modgud browser session signs in silently across Cocoar apps. Credentials (password, email code, passkey) live entirely on modgud's login page — no local passwords. Logout ends both the Shelf cookie and the modgud session.
- **Role-based admin** — `shelf:admin` permission (or a configured email allowlist) gates users, settings, analytics and API-key reveal
- **Product management API** — `POST/PUT/DELETE /_api/products[/{product}]` alongside the existing upload endpoints
- **UI-managed API keys** — master key on the settings page plus per-product upload keys; keys are readable for admins
- **Analytics** — page-view access log with configurable retention, visit charts and summaries, optional GeoIP country resolution (MaxMind)
- **Product metadata** — tags, visibility (`public`/`preview`) and `showWhenEmpty`
- **`/llms.txt`** — generated index of all public products for LLM consumption

### Changed

- **PostgreSQL is now required** — products, users, settings and the access log live in the database (Marten)
- **Product JSON files became a one-time seed** — imported at first startup; afterwards the database is the source of truth
- **API base path is `/_api`** (was `/api`)
- Access log counts HTML page views only — assets and HEAD requests are not recorded

### Removed

- File-only mode (running without a database)
- API-key admin login — admin access is a real modgud login now; API keys remain for CI/CD uploads

### Security

- No credentials are stored in Shelf — authentication is fully delegated to modgud
- Marten updated to 9.11 (fixes GHSA-vmw2-qwm8-x84c, SQL injection in the full-text search APIs — not used by Shelf, patched regardless)

### Upgrading from 1.x

1. Provide a PostgreSQL database (`Shelf__Database__ConnectionString`) — the schema is created automatically on startup
2. Register Shelf in modgud and configure `Shelf__Modgud__*` (issuer, audience, web client id/secret, admin allowlist)
3. Keep the existing `docs/` and `config/` volumes — versions are detected as before, product JSONs are imported on first start
4. Rollback caveat: products created via UI/API after the switch exist only in the database — a 1.x binary won't see them

## [1.0.0]

### Added

- **Upload API** — Deploy documentation versions via HTTP POST with ZIP files
- **Product Registration** — JSON-based product configuration with live reload
- **Landing Page** — Optional product overview page at the root URL (`EnableLandingPage`)
- **SemVer Versioning** — Support for `v5`, `v5.2`, `v5.2.0`, `v5.2.0-beta.1` version formats
- **PathBase** — Configurable global URL prefix for sub-path hosting
- **API Endpoints** — `GET /api/products`, `GET /api/products/{product}/versions`, `POST /api/products/{product}/versions/{version}`
- **API Key Authentication** — Bearer token auth for upload endpoint
- **LLM Documentation** — Guide for serving `llms.txt` and `llms-full.txt`
- **VitePress Documentation** — Full documentation site with guides for setup, deployment, and troubleshooting

### Fixed

- ZIP extraction now normalizes backslashes from Windows-created archives
- Directory detection works correctly for SemVer version paths containing dots (e.g., `v0.1`)

## [0.1.0] — 2025-03-18

### Added

- Initial release
- Static file serving from mounted volumes
- Multi-product and multi-version support
- Automatic version detection via filesystem scanning
- Latest version redirect (302)
- Base path rewriting for VitePress (HTML, CSS, JS)
- Immutable caching for hashed assets
- Resilient file monitoring via Cocoar.FileSystem

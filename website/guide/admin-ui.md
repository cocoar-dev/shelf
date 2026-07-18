# Admin UI

Shelf includes a built-in web interface for managing products, documentation versions and the instance itself. The Admin UI is a Vue 3 single-page application served at `/admin/`, designed for desktop use.

## Accessing the Admin UI

Navigate to `/admin/` (or `{PathBase}/admin/` if a [PathBase](./configuration.md#pathbase) is configured) and sign in with your email address — Modgud sends you a one-time code. See [Authentication](./authentication.md) for how the login works and how administrators are determined.

## Dashboard

The dashboard shows an overview of your Shelf instance: registered products, deployed version counts, and quick links into product management.

## Managing Products

The products page is a data grid of all registered products. Everything is reachable from there:

- **Create** — the "New Product" button opens the product dialog
- **Edit** — double-click a row (or context menu → Edit)
- **Open Docs** — context menu → opens the public documentation site
- **Delete** — context menu → Delete Product (with confirmation)

The product dialog is a modal with three tabs:

### General

- **Name** — product identifier used in URLs (e.g. `configuration`). Cannot be changed after creation.
- **Visibility** — `public` or `preview`
- **Display Name / Description** — shown on the landing page
- **Show when empty** — display on the landing page before any version is deployed ("Coming soon" teaser)

### Tags & API

- **Tags** — free-form labels (e.g. `C#`, `UI`) that power the landing page tag filter
- **API Key** — an optional per-product upload key for CI/CD, valid only for this product. Admins can view, generate, copy, replace and remove it. See [Authentication](./authentication.md#api-keys-cicd).

### Versions

The complete version management for the product:

- **Upload** — pick a version identifier (e.g. `v5.2.0`) and a ZIP with the VitePress build output ( `index.html` at the root)
- **Existing versions** — every deployed version with its "latest" status, a direct link to the docs, and a delete action (with confirmation)

Uploads and deletions take effect immediately — no save needed; the grid refreshes automatically.

### Visibility

| Visibility | Landing Page | Direct URL | API |
|------------|-------------|------------|-----|
| `public` | Shown by default | Accessible | Listed |
| `preview` | Hidden (shown with "Show preview" toggle) | Accessible | Listed |

## Analytics <Badge type="info" text="admin" />

The Analytics page shows documentation traffic from the [access log](./configuration.md#access-log): total visits and unique visitors, visits per day, and top products, pages and countries — filterable by time range (7/30/90 days, all time).

The access log records one entry per documentation page view (HTML page loads — not assets, not HEAD requests).

## Administration <Badge type="info" text="admin" />

The Administration area has four sections:

- **General** — instance settings, currently the master API key: view, generate, replace or remove the UI-managed key; shows whether a config/env key is present.
- **Users** — the local user list (thin mirrors of Modgud identities, created on first login). Deactivate a user to block sign-in, or delete the mirror (it is re-created on their next login). Accounts and credentials themselves live in Modgud.
- **Access Log** — the raw visit log with time, IP, product, version, path, geo data and user agent.
- **GeoIP** — download/update the free DB-IP Lite database that resolves visitor IPs to country and city for the access log.

## When to Use the Admin UI vs API

| Task | Admin UI | API / CLI |
|------|----------|-----------|
| Initial setup / exploration | Recommended | -- |
| One-off product management | Recommended | -- |
| CI/CD deployment | -- | Recommended |
| Automated workflows | -- | Recommended |

The Admin UI and the API are backed by the same endpoints. Anything you do in the Admin UI can also be done via `curl` or any HTTP client.

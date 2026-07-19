---
title: Admin UI
description: "The built-in web interface at /admin — manage products and versions, browse analytics and the access log, configure API keys and GeoIP. Desktop-only by design."
---

# Admin UI

Shelf includes a built-in web interface for managing products, documentation versions and the instance itself. The Admin UI is a Vue 3 single-page application served at `/admin/`, designed for desktop use.

## Accessing the Admin UI

Navigate to `/admin/` (or `{PathBase}/admin/` if a [PathBase](./configuration.md#pathbase) is configured). Unauthenticated visits redirect to Modgud's login (OIDC); an existing Modgud session signs you in silently. See [Authentication](./authentication.md) for how the login works and how administrators are determined.

The landing page and the admin UI share one shell — the same header (with a dark/light toggle) everywhere, and the admin sidebar shows for logged-in admins on every page.

## Dashboard

The dashboard shows an overview of your Shelf instance: registered products, deployed version counts, and quick links into product management.

## Managing Products

The products page is a data grid of all registered products. Everything is reachable from there:

- **Create** — the "New Product" button opens the product dialog
- **Edit** — double-click a row (or context menu → Edit)
- **Open Docs** — context menu → opens the public documentation site
- **Delete** — context menu → Delete Product (with confirmation)

The product dialog is a modal with four tabs:

### General

- **Name** — product identifier used in URLs (e.g. `configuration`). Cannot be changed after creation.
- **Visibility** — `public` or `preview`
- **Display Name / Description** — shown on the landing page
- **Show when empty** — display on the landing page before any version is deployed ("Coming soon" teaser)

### Access

- **Restricted** — when on, the product is hidden from anyone without a read grant and returns `404` on unauthorized access. Orthogonal to visibility (a product can be preview + restricted).
- **Who may read** — assign the principals that may read a restricted product: pick existing **groups or users**, or add a user by email (to stage access before their first login). See [Access Control](#access-control).

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

The Analytics page is a dashboard over the [access log](./configuration.md#access-log). All aggregation happens server-side; the page renders:

- **KPI tiles** — total visits, unique visitors, countries, cities, top product.
- **World map** — an interactive map with one bubble per visitor city, coloured by traffic volume (needs [GeoIP](#geoip) resolved).
- **Breakdowns** — top countries and cities (with flags and full names), top products and pages, and browser / OS / language / referrer.
- **Filters** — time range (7/30/90 days, all time), a product selector, and an IP-exclude field to hide specific IPs (e.g. your own) from every figure.

The access log records one entry per documentation page view (HTML page loads — not assets, not HEAD requests).

## Access Control

By default every product is **public**. Marking a product **restricted** (product dialog → Access tab) hides it from anyone without a read grant: it disappears from the landing page and the product API, and any direct URL returns `404` for unauthorized visitors (anonymous visitors are sent to login instead). Admins always see everything.

Access is granted to **principals** — permission **groups** and/or individual **users** — assigned on the product's Access tab. Users are keyed by email, so you can grant access before someone's first login.

**Groups** (Administration → Groups) are the reusable way to grant access:

- **Members** — an explicit email list, and/or
- **Auto-membership** — a JavaScript predicate over the signed-in user's claims (e.g. `user.email.endsWith('@cocoar.dev')` or `user.permissions.includes('shelf:internal')`). It is evaluated at each login and whenever the group is saved; a *Test against a user* dry-run is built in. Available: `user.email`, `user.permissions`, `user.claims`.
- **Admin group** — members of a group marked *admin* are Shelf administrators.

Only admins can read a restricted product's docs unless they (or a group they're in) are granted; groups themselves are managed only by admins.

## Administration <Badge type="info" text="admin" />

The Administration area has five sections:

- **General** — instance settings, currently the master API key: view, generate, replace or remove the UI-managed key; shows whether a config/env key is present.
- **Users** — the local user list (thin mirrors of Modgud identities, created on first login). Deactivate a user to block sign-in, or delete the mirror (it is re-created on their next login). Shows each user's group memberships. Accounts and credentials themselves live in Modgud.
- **Groups** — permission groups that grant read access to restricted products and, optionally, adminship (see [Access Control](#access-control)).
- **Access Log** — the raw visit log with time, IP, product, version, path, geo data (country flag + city) and user agent.
- **GeoIP** — download/update the free DB-IP Lite database that resolves visitor IPs to country and city for the access log and the analytics map.

## When to Use the Admin UI vs API

| Task | Admin UI | API / CLI |
|------|----------|-----------|
| Initial setup / exploration | Recommended | -- |
| One-off product management | Recommended | -- |
| CI/CD deployment | -- | Recommended |
| Automated workflows | -- | Recommended |

The Admin UI and the API are backed by the same endpoints. Anything you do in the Admin UI can also be done via `curl` or any HTTP client.

---
title: Authentication
description: "Shelf's two auth surfaces: human admin login federated to Modgud (OIDC code flow with SSO, no local passwords) and Bearer API keys for CI/CD uploads."
---

# Authentication

Shelf has two separate authentication surfaces:

1. **Admin UI login** — for humans. Federated to [Modgud](https://docs.cocoar.dev/modgud/), Cocoar's OpenID Connect identity provider. Shelf stores no passwords.
2. **API keys** — for machines. CI/CD pipelines authenticate with a Bearer token.

## Admin Login (Modgud Federation)

Login is the standard **OpenID Connect authorization-code flow** (code + PKCE):

1. Visiting `/login` (or any admin page) redirects the browser to Modgud's login page
2. You authenticate on Modgud (password, email code, passkey — Modgud's choice)
3. Modgud redirects back to `/signin-oidc`; Shelf mints a cookie session (`shelf.auth`)

Shelf acts as a backend-for-frontend: tokens stay server-side and the cookie is the session. An existing Modgud browser session signs you in **silently** — single sign-on across Cocoar apps. Users are created on first login automatically; there is no local user registration or password management. `/logout` ends both the Shelf cookie and the Modgud session.

### Configuration

Federation is configured in the `Modgud` section (see [Configuration](./configuration.md)):

| Option | Env Variable | Description |
|---|---|---|
| `Modgud.Issuer` | `Shelf__Modgud__Issuer` | Modgud realm host root, e.g. `https://auth.example.com` |
| `Modgud.Audience` | `Shelf__Modgud__Audience` | Registered OAuth API name (default `shelf`) |
| `Modgud.AuthBase` | `Shelf__Modgud__AuthBase` | Optional app subdomain for the OTP request. Unset = Issuer |
| `Modgud.WebClientId` | `Shelf__Modgud__WebClientId` | Confidential OIDC client for the login broker |
| `Modgud.WebClientSecret` | `Shelf__Modgud__WebClientSecret` | Client secret — set via environment, never in files |
| `Modgud.AdminPermission` | `Shelf__Modgud__AdminPermission` | Permission that grants admin (default `shelf:admin`) |
| `Modgud.Admins` | — | Email allowlist that is always admin (no-lockout floor) |

In Modgud, register an Application `shelf` with a `shelf:admin` permission, an OAuth API + scope `shelf`, and a confidential client with the `authorization_code` grant, the `/signin-oidc` + `/signout-callback-oidc` redirect URIs, all six scopes and JWT access tokens.

### Who Is an Admin?

A signed-in user is an administrator if any of:

- their Modgud identity carries the `shelf:admin` permission (via Modgud roles/groups), or
- their email is listed in `Modgud.Admins` — useful as a bootstrap before Modgud RBAC is set up, or
- they belong to a Shelf permission group marked *admin group* (see [Access Control](./admin-ui.md#access-control))

The Admin UI is admin-only in practice: product management, groups, analytics and the Administration area all require admin. A signed-in non-admin has access only to restricted products they've been granted (see below) and their own profile.

## Access Control (Restricted Products)

By default every product is public. A product can be marked **restricted** — it is then hidden from anyone without a read grant and returns `404` on unauthorized access. Access is granted to **principals** (permission groups and/or individual users) on the product's Access tab. Full details, including auto-membership scripting, are in [Admin UI → Access Control](./admin-ui.md#access-control).

## API Keys (CI/CD)

Programmatic access uses `Authorization: Bearer <key>`:

```bash
curl -X POST \
  -H "Authorization: Bearer $SHELF_API_KEY" \
  -H "Content-Type: application/zip" \
  --data-binary @docs.zip \
  https://docs.example.com/_api/products/configuration/versions/v6
```

Three kinds of keys are accepted, checked in this order:

| Key | Scope | Managed via |
|---|---|---|
| **Per-product key** | One product only | Product form in the Admin UI (Tags & API tab) |
| **Master key (UI-managed)** | All products | Administration → General |
| **Master key (config/env)** | All products | `Shelf__ApiKey` environment variable |

Per-product keys are the recommended way to give each CI pipeline exactly the access it needs — a leaked key can only deploy that one product. The config/env key stays valid alongside the UI-managed one, so a deployment always has a bootstrap key that survives database resets.

Admins can view, generate, replace and remove keys in the Admin UI. Keys authorize the [Upload API](./upload-api.md) endpoints (product CRUD, version upload/delete) — they do not grant access to the Administration area.

## Auth Endpoints

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/login` | Challenge Modgud (OIDC). Server route, not under `/_api` |
| `GET` | `/logout` | End the Shelf cookie **and** the Modgud session |
| `GET` | `/_api/auth/me` | Current auth status, admin flag and permissions |

## Protected Endpoints

Product and version write endpoints accept an **admin** cookie session **or** a Bearer API key:

| Method | Path |
|--------|------|
| `POST` | `/_api/products` |
| `PUT` | `/_api/products/{product}` |
| `DELETE` | `/_api/products/{product}` |
| `POST` | `/_api/products/{product}/versions/{version}` |
| `DELETE` | `/_api/products/{product}/versions/{version}` |

::: warning
The cookie path requires **admin**. A non-admin signed-in session cannot write products; use a Bearer API key for CI/CD. (Before 2.1 any authenticated cookie could write — this is now gated.)
:::

Admin-only endpoints (cookie session with admin rights required):

| Method | Path |
|--------|------|
| `GET/PUT` | `/_api/settings/…` |
| `GET` | `/_api/products/{product}/api-key` |
| `GET/PUT/DELETE` | `/_api/users/…` |
| `GET/POST` | `/_api/analytics/…` |
| `GET/POST/PUT/DELETE` | `/_api/groups/…` |
| `GET` | `/_api/principals` |

## Error Responses

| Status | When |
|--------|------|
| `401` | Not signed in / missing or invalid API key |
| `403` | Signed in, but not an administrator (admin-only endpoints) |

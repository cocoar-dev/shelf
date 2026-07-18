---
title: Authentication
description: "Shelf's two auth surfaces: human admin login federated to Modgud (OIDC code flow with SSO, no local passwords) and Bearer API keys for CI/CD uploads."
---

# Authentication

Shelf has two separate authentication surfaces:

1. **Admin UI login** — for humans. Federated to [Modgud](https://docs.cocoar.dev/modgud/), Cocoar's OpenID Connect identity provider. Shelf stores no passwords.
2. **API keys** — for machines. CI/CD pipelines authenticate with a Bearer token.

## Admin Login (Modgud Federation)

Administrators sign in to the [Admin UI](./admin-ui.md) with their email address and a one-time code:

1. Enter your email on the login screen
2. Modgud emails you a 6-digit code
3. Enter the code — Shelf establishes a cookie session (`shelf.auth`)

Behind the scenes Shelf acts as a backend-for-frontend: the browser never talks to Modgud directly. The backend redeems the code server-to-server at Modgud's token endpoint and mints a normal cookie session keyed to the Modgud identity. Users are created on first login automatically — there is no local user registration or password management.

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

In Modgud, register an Application `shelf` with a `shelf:admin` permission, an OAuth API + scope `shelf`, and a confidential client with the `urn:cocoar:otp` grant and JWT access tokens.

### Who Is an Admin?

A signed-in user is an administrator if either:

- their Modgud identity carries the `shelf:admin` permission (via Modgud roles/groups), or
- their email is listed in `Modgud.Admins` — useful as a bootstrap before Modgud RBAC is set up

Non-admin users can sign in and manage products, but the Administration area (users, access log, GeoIP, global settings) and analytics are admin-only.

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
| `POST` | `/_api/auth/otp/request` | Ask Modgud to email a login code |
| `POST` | `/_api/auth/otp/verify` | Redeem the code, receive the session cookie |
| `POST` | `/_api/auth/logout` | Clear the session cookie |
| `GET` | `/_api/auth/me` | Current auth status, admin flag and permissions |

## Protected Endpoints

Product and version write endpoints accept a cookie session **or** a Bearer API key:

| Method | Path |
|--------|------|
| `POST` | `/_api/products` |
| `PUT` | `/_api/products/{product}` |
| `DELETE` | `/_api/products/{product}` |
| `POST` | `/_api/products/{product}/versions/{version}` |
| `DELETE` | `/_api/products/{product}/versions/{version}` |

Admin-only endpoints (cookie session with admin rights required):

| Method | Path |
|--------|------|
| `GET/PUT` | `/_api/settings/…` |
| `GET` | `/_api/products/{product}/api-key` |
| `GET/PUT/DELETE` | `/_api/users/…` |
| `GET/POST` | `/_api/analytics/…` |

## Error Responses

| Status | When |
|--------|------|
| `401` | Not signed in / missing or invalid API key |
| `403` | Signed in, but not an administrator (admin-only endpoints) |

# Access Control & Multi-Tenancy Concept

This document outlines the design for adding user authentication, scoped permissions, and multi-tenancy to Shelf. The goal is to support private documentation — products that are only visible to authorized users — while keeping the public landing page and open docs working as before.

## Status

**Draft** — This is a design document, not yet implemented.

## Goals

1. **Private products** — Products that only authorized users can see and access
2. **Multi-tenancy** — Multiple organizations can use the same Shelf instance, each with isolated products and users
3. **Local users** — Shelf manages its own user accounts
4. **External login** — Optional OAuth/OIDC integration (login with cocoar.auth, Google, etc.)
5. **Backward compatible** — Public products continue to work without authentication

## Non-Goals (for now)

- Self-service registration (admin creates all users)
- Per-version access control (access is per product, not per version)
- SaaS billing/subscription management

---

## Architecture Overview

### Storage: Marten (PostgreSQL)

Shelf moves from file-based configuration to **Marten with event sourcing** for identity and permissions, and document storage for product config. This gives us:

- **Event sourcing** for users, roles, groups, and permission grants — full audit trail of who changed what and when
- **Document storage** for product configs and read-model projections
- **Built-in multi-tenancy** (per-tenant databases)
- Same infrastructure as AlertHub and cocoar.auth — proven patterns, code reuse
- PostgreSQL as the single external dependency

Why event sourcing for identity/permissions: In a SaaS scenario, the audit trail is invaluable for debugging permission issues ("the customer changed their own permissions at 14:32, which removed access to product X"). The code for event-sourced identity stores already exists in cocoar.auth and AlertHub and can be adapted directly.

### What Moves to the Database

| Today (files) | Tomorrow (database) |
|---|---|
| `data/config/products/*.json` | `ProductConfig` documents in tenant DB |
| No users | `UserAggregate` (event-sourced) → `UserState` projection |
| No roles | `PermissionRoleAggregate` (event-sourced) → `PermissionRoleState` projection |
| No permissions | `PermissionGrantAggregate` (event-sourced) → `PermissionGrantState` projection |
| No groups | `GroupAggregate` (event-sourced) → `GroupState` projection |
| API key in `data/configuration.json` | Stays in config file (server-level, not tenant-level) |

### What Stays on the Filesystem

- **Docs content** (`/data/docs/{product}/{version}/`) — Static files, served directly
- **Server configuration** (`data/configuration.json`) — AppUrl, DocsRoot, DB connection string, etc.

---

## Multi-Tenancy

### Tenant Model

A **tenant** represents an organization using Shelf. Each tenant has:

- **Slug** — URL-safe identifier (e.g., `cocoar`, `acme`), used in URL routing
- **Display name** — Human-readable name
- **Database** — Isolated PostgreSQL database per tenant (via Marten multi-tenancy)
- **Docs directory** — `{DocsRoot}/{tenant}/{product}/{version}/` or configurable per tenant

### URL Structure

URLs are always `/{product}/{version}/` — no tenant prefix in the path. Tenant resolution happens via the **domain** (Host header), not the URL path.

```
docs.cocoar.dev/configuration/v5/       → Tenant "cocoar"
docs.acme.com/my-product/v1/            → Tenant "acme" (custom domain)
acme.docs.cocoar.dev/my-product/v1/     → Tenant "acme" (subdomain)
```

API routes follow the same pattern:

```
docs.cocoar.dev/_api/products            → Tenant "cocoar" products
docs.acme.com/_api/products              → Tenant "acme" products
```

### Tenant Resolution

Each tenant has one or more **domains** configured. The `TenantResolutionMiddleware` resolves the tenant from the `Host` header:

1. Look up the request's `Host` in the tenant domain mapping
2. If found → use that tenant's context (database, docs directory)
3. If not found → use the **default tenant** (configured in `data/configuration.json`)

The default tenant acts as a fallback — in single-tenant deployments, no domain configuration is needed. Everything resolves to the default tenant.

```
Tenant {
    Id              — Unique identifier
    Slug            — Internal identifier (e.g., "cocoar", "acme")
    DisplayName     — "Cocoar" / "ACME Corp"
    Domains         — ["docs.cocoar.dev", "cocoar.localhost"]
    IsActive        — Tenant status
}
```

This approach means:
- **No path collisions** — Product names never conflict with tenant slugs
- **Clean URLs** — Always `/{product}/{version}/`, regardless of tenant
- **Custom domains** — Tenants can use their own domain (e.g., `docs.acme.com`)
- **Subdomains** — Multi-tenant SaaS via `{tenant}.docs.cocoar.dev`
- **Single-tenant simplicity** — No domain config needed, everything goes to default tenant

### Admin UI & System Administration

The Admin UI lives at `/admin` on every tenant's domain. What you see depends on your permissions:

```
docs.cocoar.dev/admin                → Admin for tenant "cocoar" + system management
acme.docs.cocoar.dev/admin           → Admin for tenant "acme" only
```

| Feature | Tenant Admin | System Admin |
|---|---|---|
| Products, versions | Own tenant | Own tenant |
| Users, roles, groups | Own tenant | Own tenant |
| Tenant settings | Own tenant | Any tenant |
| Create/delete tenants | No | Yes |
| Manage tenant domains | No | Yes |

There is no separate system domain or system endpoint. The system-level admin pages (tenant management) are additional menu items visible only to users with system-level permissions, accessed through the default tenant's domain.

### Local Development

For multi-tenant testing, modern browsers (Chrome, Firefox) resolve `*.localhost` to `127.0.0.1` automatically — no `/etc/hosts` changes needed.

```
localhost:8080                       → Default tenant (always works)
acme.localhost:8080                  → Tenant "acme"
customer.localhost:8080              → Tenant "customer"
```

Single-tenant development needs no domain configuration at all — `localhost` resolves to the default tenant.

---

## Permission Model (Scoped RBAC)

Adapted from the AlertHub pattern — permissions defined in code, granted to users with optional scoping.

### Resources and Actions

```
product:view              — See the product on the landing page and access its docs
product:manage            — Create, edit, delete products
product:upload            — Upload new documentation versions
product:delete-version    — Delete a documentation version
tenant:admin              — Full access to everything in the tenant
tenant:manage-users       — Create, edit, delete users and manage permissions
```

These are registered at startup via a `ResourceRegistry` (same as AlertHub).

### Permission Grants

A grant assigns a permission to a subject, optionally scoped to a specific product. Grants are event-sourced for full audit trail.

**Aggregate:** `PermissionGrantAggregate`
**Events:** `PermissionGrantCreated`, `PermissionGrantRevoked`
**Projection:** `PermissionGrantState`

```
PermissionGrantState {
    Id              — Unique identifier
    SubjectType     — User or Group
    SubjectId       — Who gets the permission
    RoleId?         — Via role (optional)
    DirectPermission? — Direct permission string (optional)
    ProductScope?   — Restrict to specific product (null = all products)
    IsRevoked       — Soft revoke
    GrantedBy       — Audit trail
    GrantedAt       — Audit trail
}
```

**Examples:**

```
# User can view all products
Subject: user-123, Permission: "product:view", ProductScope: null

# User can only view "configuration" product
Subject: user-123, Permission: "product:view", ProductScope: "configuration"

# User can upload docs for "shelf" product only
Subject: user-123, Permission: "product:upload", ProductScope: "shelf"

# User is tenant admin (can do everything)
Subject: user-123, Permission: "tenant:admin"
```

### Permission Roles

Roles bundle permissions for convenient assignment. Roles are event-sourced.

**Aggregate:** `PermissionRoleAggregate`
**Events:** `PermissionRoleCreated`, `PermissionRoleUpdated`, `PermissionRoleDeleted`
**Projection:** `PermissionRoleState`

```
PermissionRoleState {
    Id              — Unique identifier
    Name            — e.g., "Documentation Viewer", "Product Manager"
    Description     — Optional
    Permissions     — List of permission strings
    IsDeleted       — Soft delete
}
```

**Example roles:**

| Role | Permissions |
|---|---|
| Viewer | `product:view` |
| Contributor | `product:view`, `product:upload` |
| Product Manager | `product:view`, `product:manage`, `product:upload`, `product:delete-version` |
| Admin | `tenant:admin` |

### Permission Resolution

When checking if a user can perform an action:

1. Collect all active grants for the user (direct + from groups)
2. Expand role grants into individual permissions
3. Check for `tenant:admin` (shortcut — allows everything)
4. Match against requested permission + product scope
5. If any grant matches → allowed

### Groups

Groups bundle users and can be granted roles. Groups are event-sourced.

**Aggregate:** `GroupAggregate`
**Events:** `GroupCreated`, `GroupMemberAdded`, `GroupMemberRemoved`, `GroupChildAdded`, `GroupChildRemoved`, `GroupRoleGranted`, `GroupRoleRevoked`, `GroupArchived`
**Projection:** `GroupState`

```
GroupState {
    Id              — Unique identifier
    Name            — e.g., "ACME Team", "Internal Developers"
    MemberIds       — Users in this group
    ChildGroupIds   — Nested groups (hierarchy)
    RoleGrants      — Roles assigned to this group
    IsArchived      — Soft delete
}
```

Users inherit permissions from all groups they belong to (including parent groups in nested hierarchies). Same pattern as cocoar.auth's `GroupAggregate`.

---

## User Model

Users are event-sourced, adapted from the cocoar.auth `UserAggregate` pattern.

**Aggregate:** `UserAggregate`
**Events:** `UserCreated`, `UserUpdated`, `UserDeactivated`, `UserPasswordChanged`, `UserExternalLoginLinked`, `UserRoleAssigned`, `UserRoleRemoved`
**Projection:** `UserState`

```
UserState {
    Id              — Unique identifier
    Username        — Unique login name
    Email?          — Optional
    DisplayName?    — Optional
    IsActive        — Account status
    Roles           — List of assigned role IDs
    ExternalLogins  — Linked external providers [{provider, providerKey}]
    CreatedAt       — Audit
}
```

Password hashes are stored separately (same pattern as cocoar.auth `UserSecurityData`) to avoid leaking them through projections or API responses.

### Authentication Methods

1. **Local password** — Username + password, managed by Shelf
2. **External login** — OAuth/OIDC provider (cocoar.auth, Google, Microsoft, etc.)
3. **API key** — For CI/CD (unchanged, server-level)

The BFF (Backend For Frontend) cookie pattern we already have stays. Login endpoint validates credentials (local or external), sets a cookie. The cookie carries the user identity, permission checks happen server-side.

---

## Product Visibility

Products gain a new field alongside the existing `visibility`:

```
ProductConfig {
    ...existing fields...
    Visibility      — "public" | "preview" | "private"
}
```

| Visibility | Landing Page (not logged in) | Landing Page (logged in) | Docs Access |
|---|---|---|---|
| `public` | Visible | Visible | Open to everyone |
| `preview` | Hidden (toggle) | Hidden (toggle) | Open to everyone |
| `private` | Hidden | Visible if `product:view` permission | Requires `product:view` permission |

**Docs serving changes:**

Today the `DocsRoutingMiddleware` serves files without auth checks. For `private` products, it needs to:

1. Look up the product config (from DB)
2. If `private` → check if user is authenticated and has `product:view` for this product
3. If not authorized → 401 (not logged in) or 403 (logged in but no access)
4. If `public` or `preview` → serve as today

---

## Landing Page Changes

The landing page groups products by access level:

**Not logged in:**
- Public products (as today)
- "Show preview" toggle (as today)
- "Sign in to see more" hint (if there are private products)

**Logged in:**
- Public products
- Private products the user can access (grouped separately, e.g., "Your Documentation")
- "Show preview" toggle
- User menu (profile, sign out)

---

## API Changes

### New Endpoints

```
POST   /_api/auth/login          — Local login (username + password)
POST   /_api/auth/login/external — Initiate external login flow
GET    /_api/auth/callback       — External login callback
POST   /_api/auth/logout         — Logout
GET    /_api/auth/me             — Current user info + permissions

GET    /_api/users               — List users (admin)
POST   /_api/users               — Create user (admin)
PUT    /_api/users/{id}          — Update user (admin)
DELETE /_api/users/{id}          — Delete user (admin)

GET    /_api/roles               — List permission roles (admin)
POST   /_api/roles               — Create role (admin)
PUT    /_api/roles/{id}          — Update role (admin)
DELETE /_api/roles/{id}          — Delete role (admin)

GET    /_api/groups              — List groups (admin)
POST   /_api/groups              — Create group (admin)
...

POST   /_api/grants              — Create permission grant (admin)
DELETE /_api/grants/{id}         — Revoke permission grant (admin)
```

### Existing Endpoints (changed behavior)

```
GET    /_api/products            — Returns only products the user can see
                                   (public + authorized private products)
```

---

## Migration Path

### Phase 1: Database Foundation
- Add PostgreSQL/Marten dependency
- Migrate ProductConfig from JSON files to Marten documents
- Keep FileSystemWatcher as import mechanism (reads JSON → writes to DB)
- Admin UI reads from DB instead of ProductConfigService

### Phase 2: User Management
- User model + local authentication (username/password)
- Login endpoint changes from API key to username/password
- Admin UI: user management pages
- API key auth for CI/CD stays unchanged

### Phase 3: Permission System
- PermissionRole, PermissionGrant models
- Permission checking service
- `private` product visibility
- DocsRoutingMiddleware auth checks
- Admin UI: role and permission management

### Phase 4: Groups & External Login
- Group model with nested hierarchy
- OAuth/OIDC external login support
- Landing page "Your Documentation" section

### Phase 5: Multi-Tenant Routing (future)
- Tenant slug in URLs
- Tenant resolution middleware
- Per-tenant docs directories

---

## Database Configuration

New fields in `data/configuration.json`:

```json
{
  "AppUrl": "http://0.0.0.0:8080",
  "DocsRoot": "/data/docs",
  "Database": {
    "ConnectionString": "Host=localhost;Database=shelf;Username=shelf;Password=..."
  },
  "DefaultTenant": "cocoar",
  "ApiKey": "...",
  "Logging": {
    "LogLevels": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Marten": "Warning"
    }
  }
}
```

---

## Code Reuse

The event-sourced identity stores are adapted from existing Cocoar projects:

| Component | Source | Adaptation for Shelf |
|---|---|---|
| `UserAggregate` + events | cocoar.auth | Remove OAuth-specific fields, keep core user lifecycle |
| `UserSecurityData` | cocoar.auth | Reuse as-is for password storage |
| `GroupAggregate` + events | cocoar.auth | Reuse as-is |
| `PermissionRoleAggregate` | AlertHub | Reuse as-is |
| `PermissionGrantAggregate` | AlertHub | Replace `SubtypeId` + `ScopeGroupId` with `ProductScope` |
| `PermissionService` | AlertHub | Adapt scope resolution from group hierarchy to product scope |
| `ResourceRegistry` | AlertHub | Define Shelf-specific resources and actions |
| `RequiresPermissionAttribute` | AlertHub | Reuse as-is |
| `TenantResolutionMiddleware` | AlertHub | Reuse pattern, adapt URL structure |
| `EffectiveRolesService` | cocoar.auth | Reuse as-is for role resolution through group hierarchy |

This is not a copy-paste — the goal is a shared understanding of patterns. If these patterns prove stable across Shelf, AlertHub, and cocoar.auth, they could eventually become a shared `Cocoar.Identity` package.

---

## Decisions

1. **PostgreSQL is a hard requirement.** No file-based fallback mode. Shelf deployments include a PostgreSQL container.

2. **Setup wizard** for initial admin creation. Browser-based first-run experience (same pattern as cocoar.auth).

3. **API keys are per-product.** Each CI/CD pipeline gets a key scoped to the product(s) it deploys. This is implemented as Service Accounts — users without passwords, authenticated via API key, with `product:upload` permission scoped to specific products.

4. **No server-side session storage needed.** Authentication uses cookies (standard ASP.NET Core cookie auth). Client-side state (filters, preferences) stays in the browser. No custom session state to persist.

5. **Docs directory includes tenant:** `{DocsRoot}/{tenant}/{product}/{version}/`. Physical isolation per tenant — clean deletion, no cross-tenant risk.

6. **Identity stores are Shelf-specific** (not a shared package). The patterns are adapted from cocoar.auth and AlertHub, but each project has its own User model with different properties. A shared `Cocoar.Identity` package would need to be generic enough to handle varying user shapes — that's a separate effort outside the scope of Shelf.

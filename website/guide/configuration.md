---
title: Configuration
description: "Every configuration option — PostgreSQL connection, volumes, Modgud federation, API keys, access log, upload limits — via configuration.json or Shelf__ environment variables."
---

# Configuration

Shelf needs a PostgreSQL database, two volume mounts and a handful of environment variables.

## Configuration File

Shelf uses its own configuration file at `data/configuration.json` (relative to the application root, typically `/data/configuration.json` in Docker). This is powered by [Cocoar.Configuration](https://github.com/cocoar-dev/Cocoar.Configuration) and replaces the standard ASP.NET Core `appsettings.json` pattern.

```json
{
  "AppUrl": "http://0.0.0.0:8080",
  "DocsRoot": "/data/docs",
  "ConfigRoot": "/data/config",
  "PathBase": "",
  "ApiKey": "",
  "MaxUploadSizeBytes": 104857600,
  "VersionPattern": "^v?\\d+(\\.\\d+(\\.\\d+(-[\\w.-]+)?)?)?$",
  "Database": {
    "ConnectionString": "Host=postgres;Database=shelf;Username=postgres;Password=..."
  },
  "Modgud": {
    "Issuer": "https://auth.example.com",
    "Audience": "shelf",
    "WebClientId": "shelf-web"
  },
  "AccessLog": {
    "Enabled": true,
    "RetentionDays": 90
  },
  "Logging": {
    "LogLevels": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

## Options

Configuration can be set via `data/configuration.json` or overridden with environment variables using the `Shelf__` prefix:

| Option | Env Variable | Default | Description |
|---|---|---|---|
| `AppUrl` | `Shelf__AppUrl` | `http://0.0.0.0:8080` | Server binding URL and port |
| `DocsRoot` | `Shelf__DocsRoot` | `/data/docs` | Root directory for documentation files |
| `ConfigRoot` | `Shelf__ConfigRoot` | `/data/config` | Root directory for [product config](./product-registration.md) seed files |
| `PathBase` | `Shelf__PathBase` | _(empty)_ | Global URL prefix for running under a sub-path |
| `VersionPattern` | `Shelf__VersionPattern` | `^v?\d+(\.\d+(\.\d+(-[\w.-]+)?)?)?$` | Regex pattern to identify version directories |
| `ApiKey` | `Shelf__ApiKey` | _(empty)_ | Bootstrap master API key. See [Authentication](./authentication.md#api-keys-cicd) |
| `MaxUploadSizeBytes` | `Shelf__MaxUploadSizeBytes` | `104857600` | Maximum upload size in bytes (100 MB) |
| `Database.ConnectionString` | `Shelf__Database__ConnectionString` | — | **Required.** PostgreSQL connection string |
| `Modgud.*` | `Shelf__Modgud__*` | — | Identity provider federation. See [Authentication](./authentication.md#configuration) |
| `AccessLog.Enabled` | `Shelf__AccessLog__Enabled` | `false` | Record documentation page views for [Analytics](./admin-ui.md#analytics) |
| `AccessLog.RetentionDays` | `Shelf__AccessLog__RetentionDays` | `90` | How long access log entries are kept |

## Database

Shelf requires PostgreSQL (via [Marten](https://martendb.io/)). It stores product registrations, users, runtime settings and the access log; the schema is created and migrated automatically on startup. Documentation files themselves stay on the filesystem.

Product registration JSON files found in `{ConfigRoot}/products/` are imported into the database once at startup (existing database entries win) — this makes upgrades from file-based setups seamless. After the import, products are managed via the Admin UI or API.

## Access Log

When `AccessLog.Enabled` is true, Shelf records one entry per documentation page view (HTML page loads only — assets and HEAD requests are not counted) with IP, product, version, path, user agent and referrer. The [Analytics](./admin-ui.md#analytics) page visualizes this data; the optional [GeoIP database](./admin-ui.md#administration) adds country/city resolution.

## PathBase

When Shelf runs behind a reverse proxy under a sub-path (e.g., `example.com/docs/` instead of `docs.example.com/`), set `PathBase` to the prefix:

```yaml
environment:
  - Shelf__PathBase=/docs
```

All routes, redirects, and base path rewriting automatically include the prefix:

| PathBase | Docs URL | API URL |
|----------|----------|---------|
| _(empty)_ | `/configuration/v5/` | `/_api/products` |
| `/docs` | `/docs/configuration/v5/` | `/docs/_api/products` |

## Landing Page

Shelf serves a Vue SPA as the landing page at the root URL (`/` or `{PathBase}/`). The page shows cards for all [registered products](./product-registration.md) that have at least one deployed stable version, or that have the `showWhenEmpty` flag enabled.

### Tag Filter

All tags used across any registered product are automatically collected and displayed as clickable filter chips in a toolbar above the product grid. Clicking a tag narrows the visible products to those that carry that tag. Multiple tags can be active simultaneously (AND filter). The active selection is persisted in `localStorage` so visitors get the same view on their next visit.

### Preview Toggle

Products with `visibility: "preview"` and pre-release-only versions are hidden by default. A "Show preview" toggle reveals:

- Products with `visibility: "preview"`
- Products that only have pre-release versions
- Pre-release versions on public products

This setting is also persisted in `localStorage`.

### Teaser Cards

Products with `showWhenEmpty: true` appear in the grid even without any deployed versions. The card is rendered with a dashed border and a **Coming soon** indicator at the bottom to make the teaser state visually distinct from products with actual documentation.

## Version Pattern

By default, Shelf recognizes a wide range of version formats:

| Format | Examples |
|--------|----------|
| Major only | `v1`, `v5`, `v10` |
| Major.Minor | `v5.1`, `v5.2`, `5.2` |
| Full SemVer | `v5.2.0`, `5.2.0` |
| Pre-release | `v6.0.0-beta.1`, `v6.0.0-rc.1`, `v1.0.0-vue-ui.10` |

The `v` prefix is optional. Pre-release labels support hyphens (e.g., `v1.0.0-vue-ui.10`). Directories that don't match the pattern are ignored:

```
/data/docs/configuration/
├── v5.1.0/                ← recognized
├── v5.2.0/                ← recognized
├── v6.0.0-beta.1/         ← recognized (pre-release)
├── v1.0.0-vue-ui.10/      ← recognized (pre-release with hyphens)
├── assets/                ← ignored
└── .hidden/               ← ignored
```

### Version Sorting

Versions are sorted numerically by Major, Minor, and Patch. The **latest** version is determined as follows:

1. **Stable versions** (without pre-release label) are always preferred
2. Among stable versions, the highest Major.Minor.Patch wins
3. If only pre-release versions exist, the highest one is used as latest

Example: With `v5.2.0`, `v6.0.0-beta.1`, and `v5.1.0`, the latest is `v5.2.0` -- because `v6.0.0-beta.1` is a pre-release.

::: tip GitVersion / SemVer
Shelf works well with [GitVersion](https://gitversion.net/) or any SemVer-based versioning. Use the version output from your CI pipeline directly as the version directory name.
:::

### Customizing the Version Pattern

If you need a stricter or different versioning scheme, override the pattern:

```yaml
environment:
  # Only allow full SemVer with v prefix
  - Shelf__VersionPattern=^v\d+\.\d+\.\d+$

  # Only major versions
  - Shelf__VersionPattern=^v\d+$
```

## Logging

Shelf uses [Serilog](https://serilog.net/) for structured logging. Log levels are configured in the `Logging.LogLevels` section of `data/configuration.json`:

```json
{
  "Logging": {
    "LogLevels": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Cocoar.Shelf": "Debug"
    }
  }
}
```

Log levels can also be set via environment variables:

```yaml
environment:
  - Shelf__Logging__LogLevels__Default=Information
  - Shelf__Logging__LogLevels__Microsoft.AspNetCore=Warning
```

Available levels: `Verbose`, `Debug`, `Information`, `Warning`, `Error`, `Fatal`.

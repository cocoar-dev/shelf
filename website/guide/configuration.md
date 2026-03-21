# Configuration

Shelf requires minimal configuration. Most setups work with just Docker volume mounts and a few environment variables.

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
| `ConfigRoot` | `Shelf__ConfigRoot` | `/data/config` | Root directory for [product config](./product-registration.md) files |
| `PathBase` | `Shelf__PathBase` | _(empty)_ | Global URL prefix for running under a sub-path |
| `VersionPattern` | `Shelf__VersionPattern` | `^v?\d+(\.\d+(\.\d+(-[\w.-]+)?)?)?$` | Regex pattern to identify version directories |
| `ApiKey` | `Shelf__ApiKey` | _(empty)_ | API key for protected endpoints. Empty = upload and admin disabled |
| `MaxUploadSizeBytes` | `Shelf__MaxUploadSizeBytes` | `104857600` | Maximum upload size in bytes (100 MB) |

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

Shelf serves a Vue SPA as the landing page at the root URL (`/` or `{PathBase}/`). The page shows cards for all [registered products](./product-registration.md) that have at least one deployed stable version.

Products with `visibility: "preview"` and pre-release-only versions are hidden by default. A "Show preview" toggle reveals:

- Products with `visibility: "preview"`
- Products that only have pre-release versions
- Pre-release versions on public products

The landing page also includes `<link rel="alternate">` pointing to `/llms.txt` for LLM discoverability.

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

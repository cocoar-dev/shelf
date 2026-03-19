# Configuration

Shelf requires minimal configuration. Most setups work with just Docker volume mounts and a few environment variables.

## Options

Configuration is done via environment variables using the ASP.NET Core configuration pattern (`Shelf__PropertyName`) or via `appsettings.json`:

| Option | Env Variable | Default | Description |
|---|---|---|---|
| `DocsRoot` | `Shelf__DocsRoot` | `/data/docs` | Root directory for documentation files |
| `ConfigRoot` | `Shelf__ConfigRoot` | `/data/config` | Root directory for [product config](./product-registration.md) files |
| `PathBase` | `Shelf__PathBase` | _(empty)_ | Global URL prefix for running under a sub-path |
| `VersionPattern` | `Shelf__VersionPattern` | `^v?\d+(\.\d+(\.\d+(-[\w.]+)?)?)?$` | Regex pattern to identify version directories |
| `EnableLandingPage` | `Shelf__EnableLandingPage` | `false` | Show a product overview page at the root URL |
| `ApiKey` | `Shelf__ApiKey` | _(empty)_ | API key for [upload endpoint](./upload-api.md). Empty = upload disabled |
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
| _(empty)_ | `/configuration/v5/` | `/api/products` |
| `/docs` | `/docs/configuration/v5/` | `/docs/api/products` |

## Landing Page

When `EnableLandingPage` is `true`, Shelf renders a product overview page at the root URL (`/` or `{PathBase}/`). The page shows cards for all [registered products](./product-registration.md) that have at least one deployed version. Clicking a product opens its documentation in a new tab.

```yaml
environment:
  - Shelf__EnableLandingPage=true
```

Only products with a config file and deployed versions appear on the landing page. Products deployed manually (without registration) are still accessible via direct URL but won't be listed.

## Version Pattern

By default, Shelf recognizes a wide range of version formats:

| Format | Examples |
|--------|----------|
| Major only | `v1`, `v5`, `v10` |
| Major.Minor | `v5.1`, `v5.2`, `5.2` |
| Full SemVer | `v5.2.0`, `5.2.0` |
| Pre-release | `v6.0.0-beta.1`, `v6.0.0-rc.1` |

The `v` prefix is optional. Directories that don't match the pattern are ignored:

```
/data/docs/configuration/
├── v5.1.0/                ← recognized
├── v5.2.0/                ← recognized
├── v6.0.0-beta.1/         ← recognized (pre-release)
├── assets/                ← ignored
└── .hidden/               ← ignored
```

### Version Sorting

Versions are sorted numerically by Major, Minor, and Patch. The **latest** version is determined as follows:

1. **Stable versions** (without pre-release label) are always preferred
2. Among stable versions, the highest Major.Minor.Patch wins
3. If only pre-release versions exist, the highest one is used as latest

Example: With `v5.2.0`, `v6.0.0-beta.1`, and `v5.1.0`, the latest is `v5.2.0` — because `v6.0.0-beta.1` is a pre-release.

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

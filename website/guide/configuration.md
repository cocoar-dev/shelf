# Configuration

Shelf requires minimal configuration. Most setups work with just Docker volume mounts and a few environment variables.

## Options

Configuration is done via environment variables using the ASP.NET Core configuration pattern (`Shelf__PropertyName`) or via `appsettings.json`:

| Option | Env Variable | Default | Description |
|---|---|---|---|
| `DocsRoot` | `Shelf__DocsRoot` | `/data/docs` | Root directory for documentation files |
| `ConfigRoot` | `Shelf__ConfigRoot` | `/data/config` | Root directory for [product config](./product-registration.md) files |
| `PathBase` | `Shelf__PathBase` | _(empty)_ | Global URL prefix for running under a sub-path |
| `VersionPattern` | `Shelf__VersionPattern` | `^v\d+$` | Regex pattern to identify version directories |
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

When `EnableLandingPage` is `true`, Shelf renders a product overview page at the root URL (`/` or `{PathBase}/`). The page shows a sidebar listing all [registered products](./product-registration.md) that have at least one deployed version, with an iframe displaying the selected product's documentation.

```yaml
environment:
  - Shelf__EnableLandingPage=true
```

Only products with a config file and deployed versions appear on the landing page. Products deployed manually (without registration) are still accessible via direct URL but won't be listed.

## Version Pattern

By default, Shelf recognizes directories matching `v` followed by a number as version directories: `v1`, `v2`, `v10`, etc.

Directories that don't match this pattern are ignored during version scanning. This means you can have non-version directories alongside version directories without issues:

```
/data/docs/configuration/
├── v4/          ← recognized as version
├── v5/          ← recognized as version
├── assets/      ← ignored (not a version)
└── .hidden/     ← ignored (not a version)
```

## Customizing the Version Pattern

If you need a different versioning scheme, override the pattern:

```yaml
environment:
  - Shelf__VersionPattern=^v\d+\.\d+$    # matches v1.0, v2.1, etc.
```

::: warning
Changing the version pattern affects how "latest" is determined. The default sorting extracts the number after `v` and sorts numerically. Custom patterns with non-numeric segments may not sort as expected.
:::

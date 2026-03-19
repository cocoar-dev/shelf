# Product Registration

Before documentation can be deployed via the [Upload API](./upload-api.md), a product must be registered. Registration is done by placing a JSON config file in the config directory.

## Config Directory

Product config files live in `{ConfigRoot}/products/`, one file per product:

```
/data/config/
└── products/
    ├── configuration.json
    ├── capabilities.json
    └── filesystem.json
```

## Config File Format

Each JSON file describes one product:

```json
{
  "name": "configuration",
  "displayName": "Cocoar.Configuration",
  "description": "Reactive configuration for .NET",
  "source": "upload"
}
```

| Field | Required | Description |
|-------|----------|-------------|
| `name` | Yes | Product identifier, used in URLs and API calls |
| `displayName` | No | Human-readable name for API responses |
| `description` | No | Short description of the product |
| `source` | No | Deployment source type. Default: `"upload"` |

::: tip
The `name` field determines the URL path, not the filename. By convention, keep both in sync (e.g., `configuration.json` with `"name": "configuration"`).
:::

## Live Reload

Config files are loaded at startup and monitored for changes. When you add, modify, or remove a config file, Shelf picks up the change automatically — no restart needed.

## Why Registration?

Product registration is required for the Upload API to prevent accidental creation of arbitrary products. Without registration, a typo in a CI pipeline (`configration` instead of `configuration`) would silently create a new product.

Manual deployment (copying files directly into the docs volume) does **not** require registration — Shelf serves any product directory it finds, regardless of whether a config file exists.

## API Integration

Registered products are visible via the API:

```bash
# List all registered products with their versions
curl https://docs.cocoar.dev/api/products
```

```json
[
  {
    "name": "configuration",
    "displayName": "Cocoar.Configuration",
    "description": "Reactive configuration for .NET",
    "source": "upload",
    "latest": "v5",
    "versions": ["v5", "v4"]
  }
]
```

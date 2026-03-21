# Product Registration

Before documentation can be deployed via the [Upload API](./upload-api.md), a product must be registered. Products can be managed through the [Admin UI](./admin-ui.md), the API, or by placing JSON config files in the config directory.

## Admin UI (Recommended)

The easiest way to register products is through the [Admin UI](./admin-ui.md) at `/admin/`:

1. Log in with your API key
2. Navigate to the product management section
3. Click "Create Product"
4. Fill in the product details (name, display name, description, visibility)
5. Save

See [Admin UI](./admin-ui.md) for the full workflow.

## API

Products can be created, updated, and deleted via the API. All mutating endpoints require [authentication](./authentication.md).

### Create a Product

```bash
curl -X POST \
  -H "Authorization: Bearer $SHELF_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "configuration",
    "displayName": "Cocoar.Configuration",
    "description": "Reactive configuration for .NET",
    "source": "upload",
    "visibility": "public"
  }' \
  https://docs.cocoar.dev/_api/products
```

### Update a Product

```bash
curl -X PUT \
  -H "Authorization: Bearer $SHELF_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "displayName": "Cocoar.Configuration",
    "description": "Updated description",
    "visibility": "preview"
  }' \
  https://docs.cocoar.dev/_api/products/configuration
```

### Delete a Product

```bash
curl -X DELETE \
  -H "Authorization: Bearer $SHELF_API_KEY" \
  https://docs.cocoar.dev/_api/products/configuration
```

## JSON Config Files

Products can also be registered by placing JSON config files in `{ConfigRoot}/products/`, one file per product:

```
/data/config/
└── products/
    ├── configuration.json
    ├── capabilities.json
    └── filesystem.json
```

### Config File Format

Each JSON file describes one product:

```json
{
  "name": "configuration",
  "displayName": "Cocoar.Configuration",
  "description": "Reactive configuration for .NET",
  "source": "upload",
  "visibility": "public"
}
```

| Field | Required | Description |
|-------|----------|-------------|
| `name` | Yes | Product identifier, used in URLs and API calls |
| `displayName` | No | Human-readable name for API responses and landing page |
| `description` | No | Short description of the product |
| `source` | No | Deployment source type. Default: `"upload"` |
| `visibility` | No | `"public"` or `"preview"`. Default: `"public"` |

::: tip
The `name` field determines the URL path, not the filename. By convention, keep both in sync (e.g., `configuration.json` with `"name": "configuration"`).
:::

### Visibility

The `visibility` field controls how a product appears on the landing page:

| Value | Behavior |
|-------|----------|
| `"public"` | Shown on the landing page by default (if it has stable versions) |
| `"preview"` | Hidden by default, shown when "Show preview" is toggled on |

Preview visibility is useful for products that are in development or not yet ready for general use. The documentation is still accessible via direct URL regardless of visibility.

### Live Reload

Config files are loaded at startup and monitored for changes. When you add, modify, or remove a config file, Shelf picks up the change automatically -- no restart needed.

## Why Registration?

Product registration is required for the Upload API to prevent accidental creation of arbitrary products. Without registration, a typo in a CI pipeline (`configration` instead of `configuration`) would silently create a new product.

Manual deployment (copying files directly into the docs volume) does **not** require registration -- Shelf serves any product directory it finds, regardless of whether a config file exists.

## API Integration

Registered products are visible via the API:

```bash
# List all registered products with their versions
curl https://docs.cocoar.dev/_api/products
```

```json
[
  {
    "name": "configuration",
    "displayName": "Cocoar.Configuration",
    "description": "Reactive configuration for .NET",
    "source": "upload",
    "visibility": "public",
    "latest": "v5",
    "versions": ["v5", "v4"]
  }
]
```

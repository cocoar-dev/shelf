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
    "visibility": "public",
    "tags": ["C#", ".NET"],
    "showWhenEmpty": false
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
    "visibility": "preview",
    "tags": ["C#", ".NET", "Configuration"],
    "showWhenEmpty": true
  }' \
  https://docs.cocoar.dev/_api/products/configuration
```

### Delete a Product

```bash
# Remove registration only (docs files kept on disk)
curl -X DELETE \
  -H "Authorization: Bearer $SHELF_API_KEY" \
  https://docs.cocoar.dev/_api/products/configuration

# Remove registration AND all documentation files
curl -X DELETE \
  -H "Authorization: Bearer $SHELF_API_KEY" \
  "https://docs.cocoar.dev/_api/products/configuration?deleteData=true"
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
  "visibility": "public",
  "tags": ["C#", ".NET"],
  "showWhenEmpty": false
}
```

| Field | Required | Description |
|-------|----------|--------------|
| `name` | Yes | Product identifier, used in URLs and API calls |
| `displayName` | No | Human-readable name for API responses and landing page |
| `description` | No | Short description of the product |
| `source` | No | Deployment source type. Default: `"upload"` |
| `visibility` | No | `"public"` or `"preview"`. Default: `"public"` |
| `tags` | No | List of free-form labels (e.g. `["C#", "UI"]`). Used for filtering on the landing page |
| `showWhenEmpty` | No | Show on landing page even without any deployed versions. Default: `false` |

::: tip
The `name` field determines the URL path, not the filename. By convention, keep both in sync (e.g., `configuration.json` with `"name": "configuration"`).
:::

### Visibility

The `visibility` field controls how a product appears on the landing page:

| Value | Behavior |
|-------|----------|
| `"public"` | Shown on the landing page by default (if it has stable versions, or `showWhenEmpty` is set) |
| `"preview"` | Hidden by default, shown when "Show preview" is toggled on |

Preview visibility is useful for products that are in development or not yet ready for general use. The documentation is still accessible via direct URL regardless of visibility.

### Tags

Tags are free-form labels you can attach to a product (e.g. `"C#"`, `"UI"`, `".NET"`, `"CLI"`). They appear as chips on product cards, and the landing page provides a tag filter bar so visitors can narrow down the list to products that interest them.

Tags are stored as an array of strings. Shelf normalises them automatically: leading/trailing whitespace is trimmed, duplicates and empty values are removed, and the list is sorted alphabetically.

```json
{
  "tags": ["C#", ".NET", "Configuration"]
}
```

### Show When Empty

By default, a product only appears on the landing page once it has at least one deployed version. Set `showWhenEmpty: true` to display the product card immediately — even before any documentation is uploaded. The card is rendered in a distinct teaser style with a "Coming soon" indicator.

This is useful for:
- **Testing** — verify the product is registered and visible before pushing the first version
- **Teasers** — announce upcoming documentation while it is still being written

```json
{
  "showWhenEmpty": true
}
```

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
    "tags": ["C#", ".NET"],
    "showWhenEmpty": false,
    "latest": "v5",
    "versions": ["v5", "v4"]
  }
]
```

# Upload API

Shelf provides an HTTP API for deploying documentation versions. This is designed for CI/CD pipelines — analogous to `nuget push` or `docker push`.

## Prerequisites

1. The product must be [registered](./product-registration.md) via a config file
2. An API key must be [configured](./configuration.md) (`Shelf__ApiKey`)

If no API key is configured, the upload endpoint returns `503 Service Unavailable`.

## Uploading a Version

```bash
curl -X POST \
  -H "Authorization: Bearer $SHELF_API_KEY" \
  -H "Content-Type: application/zip" \
  --data-binary @docs.zip \
  https://docs.cocoar.dev/api/products/configuration/versions/v6
```

The ZIP file should contain the VitePress build output with `index.html` at the root:

```
docs.zip
├── index.html
├── assets/
│   ├── style.a1b2c3.css
│   └── app.d4e5f6.js
├── guide/
│   └── getting-started.html
├── llms.txt
└── llms-full.txt
```

### What Happens

1. API key is validated
2. Product registration is checked (must exist in config)
3. Version format is validated against the [version pattern](./configuration.md#version-pattern) (supports SemVer: `v5`, `v5.2`, `v5.2.0`, `v5.2.0-beta.1`)
4. ZIP is extracted to a temporary directory
5. Validation: `index.html` must exist at the root
6. Atomic move to `/data/docs/{product}/{version}/`
7. ManifestService detects the new version automatically
8. Response: `201 Created`

Re-uploading an existing version replaces it atomically.

## GitHub Actions Example

```yaml
name: Deploy Docs

on:
  workflow_dispatch:
    inputs:
      version:
        description: 'Version tag (e.g. v6)'
        required: true

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Build docs
        run: |
          npm ci
          npx vitepress build

      - name: Package docs
        run: cd .vitepress/dist && zip -r ../../docs.zip .

      - name: Upload to Shelf
        run: |
          curl -f -X POST \
            -H "Authorization: Bearer ${{ secrets.SHELF_API_KEY }}" \
            -H "Content-Type: application/zip" \
            --data-binary @docs.zip \
            https://docs.cocoar.dev/api/products/configuration/versions/${{ inputs.version }}
```

## API Endpoints

### List Products

```
GET /api/products
```

Returns all registered products with their version information. No authentication required.

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

### List Versions

```
GET /api/products/{product}/versions
```

Returns version information for a specific product. No authentication required.

```json
{
  "name": "configuration",
  "latest": "v5",
  "versions": ["v5", "v4"]
}
```

### Upload Version

```
POST /api/products/{product}/versions/{version}
```

Uploads a ZIP file as a new documentation version. Requires `Authorization: Bearer {key}` header.

## Error Responses

| Status | When |
|--------|------|
| `201` | Successfully deployed |
| `400` | Invalid version format, corrupt ZIP, or missing `index.html` |
| `401` | Missing or invalid API key |
| `404` | Product not registered |
| `409` | Concurrent upload for the same product/version |
| `413` | ZIP exceeds maximum upload size |
| `503` | No API key configured (upload disabled) |

## Security

- **Authentication**: Bearer token checked against the configured API key
- **ZIP-Slip protection**: All extracted paths are validated to stay within the target directory
- **Atomic deployment**: Files are extracted to a temp directory, validated, then moved — Shelf never serves a half-extracted state
- **Concurrent upload protection**: Simultaneous uploads for the same product/version return `409 Conflict`
- **Size limit**: Configurable via `MaxUploadSizeBytes` (default: 100 MB)

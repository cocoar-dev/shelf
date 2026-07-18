---
title: Upload API
description: "Deploy documentation from CI/CD: the ZIP upload endpoint, API-key authentication, version format rules, and ready-to-use GitHub Actions examples."
---

# Upload API

Shelf provides an HTTP API for deploying documentation versions. This is designed for CI/CD pipelines -- analogous to `nuget push` or `docker push`.

## Prerequisites

1. The product must be [registered](./product-registration.md) (via Admin UI, API, or seed file)
2. An API key — either the product's own key, or a master key. See [Authentication](./authentication.md#api-keys-cicd)

::: tip Per-product keys
Give each pipeline the per-product key from its product's **Tags & API** tab instead of the master key — a leaked key can then only deploy that one product.
:::

## Uploading a Version

```bash
curl -X POST \
  -H "Authorization: Bearer $SHELF_API_KEY" \
  -H "Content-Type: application/zip" \
  --data-binary @docs.zip \
  https://docs.cocoar.dev/_api/products/configuration/versions/v6
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

1. API key is validated (Bearer token or cookie session)
2. Product registration is checked (must exist)
3. Version format is validated against the [version pattern](./configuration.md#version-pattern) (supports SemVer: `v5`, `v5.2`, `v5.2.0`, `v5.2.0-beta.1`, `v1.0.0-vue-ui.10`)
4. ZIP is extracted to a temporary directory
5. Validation: `index.html` must exist at the root
6. Atomic move to `/data/docs/{product}/{version}/`
7. ManifestService detects the new version automatically
8. Response: `201 Created`

Re-uploading an existing version replaces it atomically.

## Deleting a Version

```bash
curl -X DELETE \
  -H "Authorization: Bearer $SHELF_API_KEY" \
  https://docs.cocoar.dev/_api/products/configuration/versions/v6
```

The version directory is removed from disk. ManifestService detects the change automatically.

## CI/CD Integration

### Setup

1. Add `SHELF_API_KEY` as a repository secret in GitHub
2. Ensure the product is [registered](./product-registration.md) on the server
3. Add a deploy job to your workflow

### GitHub Actions with GitVersion

```yaml
name: Deploy Docs

on:
  push:
    branches: [main]

jobs:
  deploy-docs:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - name: GitVersion
        id: version
        uses: gittools/actions/gitversion/execute@v3

      - uses: actions/setup-node@v4
        with:
          node-version: 22

      - name: Build
        working-directory: website
        run: npm ci && npx vitepress build

      - name: Package
        run: cd website/.vitepress/dist && zip -r $GITHUB_WORKSPACE/docs.zip .

      - name: Deploy to Shelf
        env:                                            # [!code highlight]
          KEY: ${{ secrets.SHELF_API_KEY }}              # [!code highlight]
          VER: ${{ steps.version.outputs.major }}.${{ steps.version.outputs.minor }} # [!code highlight]
        run: |
          curl -f -X POST \
            -H "Authorization: Bearer $KEY" \
            -H "Content-Type: application/zip" \
            --data-binary @$GITHUB_WORKSPACE/docs.zip \
            https://docs.cocoar.dev/_api/products/configuration/versions/v$VER
```

::: tip Version Format
Choose what makes sense for your docs. Use the GitVersion output variables in the upload URL:
- **Major only** -- `v5` (use `outputs.major`)
- **Major.Minor** -- `v5.2` (recommended, use `outputs.major` + `outputs.minor`)
- **Full SemVer** -- `v5.2.0` (use `outputs.majorMinorPatch`)
- **Pre-release** -- `v5.2.0-beta.1` (use full version for pre-release builds)
:::

### Manual Trigger Variant

Replace the `on:` section to allow manual deploys with a version input:

```yaml
on:
  workflow_dispatch:
    inputs:
      version:
        description: 'Version (e.g. v5.2)'
        required: true
```

Then use the `inputs.version` variable instead of the GitVersion output in the `env` section.

### Key Points

- **ZIP from inside the dist directory** -- `cd .vitepress/dist && zip -r ... .` so `index.html` is at the root
- **Use `curl -f`** -- fails the step on HTTP errors (4xx/5xx)
- **Re-upload replaces atomically** -- safe to re-run the pipeline
- **Pre-release versions are never "latest"** -- visitors are redirected to the highest stable version
- **Node.js required** -- CI workflows need a Node.js setup step before building (for the Vue client)

## API Reference

All API routes use the `/_api/` prefix. See [Authentication](./authentication.md) for details on auth methods.

### Products

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `GET` | `/_api/products` | No | List all registered products with version info |
| `GET` | `/_api/products/{product}` | No | Get a single product with version info |
| `POST` | `/_api/products` | Yes | Create a new product |
| `PUT` | `/_api/products/{product}` | Yes | Update a product |
| `DELETE` | `/_api/products/{product}` | Yes | Delete product config. Pass `?deleteData=true` to also delete docs from disk |

### Versions

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `GET` | `/_api/products/{product}/versions` | No | List versions of a product |
| `POST` | `/_api/products/{product}/versions/{version}` | Yes | Upload a ZIP as a new version |
| `DELETE` | `/_api/products/{product}/versions/{version}` | Yes | Delete a version |

### Authentication

See [Authentication](./authentication.md) — the Admin UI signs in via Modgud (email code); API access uses Bearer keys.

### List Products

```
GET /_api/products
```

Returns all registered products with their version information. No authentication required.

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
    "latest": "v5.2.0",
    "versions": ["v5.2.0", "v5.1.0", "v5.0.0"]
  }
]
```

### List Versions

```
GET /_api/products/{product}/versions
```

Returns version information for a specific product. No authentication required.

```json
{
  "name": "configuration",
  "latest": "v5.2.0",
  "versions": ["v5.2.0", "v5.1.0", "v5.0.0"]
}
```

### Upload Version

```
POST /_api/products/{product}/versions/{version}
```

Uploads a ZIP file as a new documentation version. Requires [authentication](./authentication.md).

### Delete Version

```
DELETE /_api/products/{product}/versions/{version}
```

Deletes a documentation version. Requires [authentication](./authentication.md).

## Error Responses

| Status | When |
|--------|------|
| `201` | Successfully deployed |
| `400` | Invalid version format, corrupt ZIP, or missing `index.html` |
| `401` | Missing or invalid authentication |
| `404` | Product not registered |
| `409` | Concurrent upload for the same product/version |
| `413` | ZIP exceeds maximum upload size |

## Security

- **Authentication**: Bearer token or cookie session checked against the configured API key. See [Authentication](./authentication.md)
- **ZIP-Slip protection**: All extracted paths are validated to stay within the target directory
- **Atomic deployment**: Files are extracted to a temp directory, validated, then moved -- Shelf never serves a half-extracted state
- **Concurrent upload protection**: Simultaneous uploads for the same product/version return `409 Conflict`
- **Size limit**: Configurable via `MaxUploadSizeBytes` (default: 100 MB)

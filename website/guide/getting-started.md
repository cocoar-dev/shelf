# Getting Started

Shelf is a static documentation hosting platform for Cocoar products. It serves VitePress-generated documentation with support for multiple products and multiple versions per product.

## What It Does

- Serves static HTML/CSS/JS files from a mounted volume
- Routes requests to the correct product and version
- Automatically determines the "latest" version per product
- Provides an [Upload API](./upload-api.md) for deploying docs from CI/CD pipelines
- Includes a built-in [Admin UI](./admin-ui.md) for managing products and versions
- Caches version information and invalidates on filesystem changes

## Quick Start

### 1. Run with Docker

```yaml
# docker-compose.yml
services:
  shelf:
    image: ghcr.io/cocoar-dev/shelf:latest
    ports:
      - "80:8080"
    volumes:
      - docs-data:/data/docs
      - config-data:/data/config
    environment:
      - Shelf__ApiKey=${SHELF_API_KEY:-}
    restart: unless-stopped
```

```bash
docker compose up -d
```

### 2. Add Documentation

**Option A: Admin UI** (recommended for getting started)

Open `http://localhost/admin/`, log in with your API key, and create a product. Then upload a ZIP of your VitePress build output directly from the browser.

See [Admin UI](./admin-ui.md) for details.

**Option B: Upload API** (recommended for CI/CD)

Register the product, then upload a ZIP:

```bash
# Create product via API
curl -X POST \
  -H "Authorization: Bearer $SHELF_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{"name": "configuration", "displayName": "Cocoar.Configuration", "source": "upload"}' \
  http://localhost/_api/products

# Upload docs
curl -X POST \
  -H "Authorization: Bearer $SHELF_API_KEY" \
  -H "Content-Type: application/zip" \
  --data-binary @docs.zip \
  http://localhost/_api/products/configuration/versions/v5
```

See [Product Registration](./product-registration.md) and [Upload API](./upload-api.md) for details.

**Option C: Manual** (copy files directly)

Place your VitePress build output in the volume:

```
/data/docs/
└── configuration/
    └── v5/
        ├── index.html
        ├── assets/
        ├── guide/
        ├── llms.txt
        └── llms-full.txt
```

### 3. Access Your Docs

Your documentation is now available at:

- `http://localhost/configuration/` -- latest version (redirects to v5)
- `http://localhost/configuration/v5/` -- specific version
- `http://localhost/admin/` -- Admin UI for management

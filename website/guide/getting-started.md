# Getting Started

Shelf is a static documentation hosting platform for Cocoar products. It serves VitePress-generated documentation with support for multiple products and multiple versions per product.

## What It Does

- Serves static HTML/CSS/JS files from a mounted volume
- Routes requests to the correct product and version
- Automatically determines the "latest" version per product
- Provides an [Upload API](./upload-api.md) for deploying docs from CI/CD pipelines
- Caches version information and invalidates on filesystem changes

## Quick Start

### 1. Run with Docker

```yaml
# docker-compose.yml
services:
  shelf:
    image: cocoar/shelf
    ports:
      - "80:8080"
    volumes:
      - docs-data:/data/docs
      - config-data:/data/config:ro
    environment:
      - Shelf__ApiKey=${SHELF_API_KEY:-}
    restart: unless-stopped
```

```bash
docker compose up -d
```

### 2. Add Documentation

**Option A: Upload API** (recommended for CI/CD)

Register the product, then upload a ZIP:

```bash
# Create product config
echo '{"name": "configuration", "source": "upload"}' > /data/config/products/configuration.json

# Upload docs
curl -X POST \
  -H "Authorization: Bearer $SHELF_API_KEY" \
  -H "Content-Type: application/zip" \
  --data-binary @docs.zip \
  http://localhost/api/products/configuration/versions/v5
```

See [Product Registration](./product-registration.md) and [Upload API](./upload-api.md) for details.

**Option B: Manual** (copy files directly)

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

- `http://localhost/configuration/` — latest version (redirects to v5)
- `http://localhost/configuration/v5/` — specific version

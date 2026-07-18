---
title: Getting Started
description: "What Shelf is and the five-minute path from a VitePress build to hosted, versioned documentation: run the container, register a product, upload a ZIP."
---

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
      - Shelf__Database__ConnectionString=Host=postgres;Database=shelf;Username=postgres;Password=${POSTGRES_PASSWORD}
      - Shelf__Modgud__Issuer=https://auth.example.com
      - Shelf__Modgud__WebClientId=shelf-web
      - Shelf__Modgud__WebClientSecret=${SHELF_MODGUD_SECRET}
      - Shelf__ApiKey=${SHELF_API_KEY:-}
    restart: unless-stopped

  postgres:
    image: postgres:17
    environment:
      - POSTGRES_PASSWORD=${POSTGRES_PASSWORD}
      - POSTGRES_DB=shelf
    volumes:
      - pg-data:/var/lib/postgresql/data
    restart: unless-stopped
```

```bash
docker compose up -d
```

Shelf needs PostgreSQL and, for the admin login, a [Modgud](./authentication.md) identity provider. CI/CD-only setups can start with just the `Shelf__ApiKey` and add the admin login later.

### 2. Add Documentation

**Option A: Admin UI** (recommended for getting started)

Open `http://localhost/admin/` and sign in with your email — you'll receive a one-time code. Create a product and upload a ZIP of your VitePress build output directly from the browser.

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

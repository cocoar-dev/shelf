# Getting Started

Shelf is a static documentation hosting platform for Cocoar products. It serves VitePress-generated documentation with support for multiple products and multiple versions per product.

## What It Does

- Serves static HTML/CSS/JS files from a mounted volume
- Routes requests to the correct product and version
- Automatically determines the "latest" version per product
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
      - docs-data:/data/docs:ro
    restart: unless-stopped
```

```bash
docker compose up -d
```

### 2. Add Documentation

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

- `http://localhost/configuration/` — latest version
- `http://localhost/configuration/v5/` — specific version

That's it. No configuration files, no API calls, no manifest to maintain.

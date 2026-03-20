# Docker

Shelf runs as a Docker container with documentation and configuration files mounted as volumes.

## Basic Setup

```yaml
# docker-compose.yml
services:
  shelf:
    image: ghcr.io/cocoar-dev/shelf:latest
    ports:
      - "80:8080"
    volumes:
      - docs-data:/data/docs
      - config-data:/data/config:ro
    environment:
      - Shelf__ApiKey=${SHELF_API_KEY:-}
    restart: unless-stopped
```

Two volumes:
- **Docs volume** — writable, so the [Upload API](./upload-api.md) can deploy versions
- **Config volume** — read-only, contains [product registration](./product-registration.md) files

If you don't use the Upload API and only deploy manually, you can mount the docs volume as `:ro` (read-only).

## Volume Mounting

### Single Volume

The simplest approach. All products live in one volume:

```yaml
volumes:
  - docs-data:/data/docs
  - config-data:/data/config:ro
```

### One Volume Per Product

Useful when products are deployed independently:

```yaml
volumes:
  - config-docs:/data/docs/configuration
  - caps-docs:/data/docs/capabilities
```

### Host Directory

For development or simple setups, mount host directories directly:

```yaml
volumes:
  - /srv/docs:/data/docs
  - /srv/config:/data/config:ro
```

## Environment Variables

See [Configuration](./configuration.md) for all options. The most important ones for Docker:

| Variable | Default | Description |
|---|---|---|
| `Shelf__DocsRoot` | `/data/docs` | Root directory for documentation files |
| `Shelf__ConfigRoot` | `/data/config` | Root directory for product config files |
| `Shelf__ApiKey` | _(empty)_ | API key for upload endpoint. Empty = upload disabled |
| `Shelf__PathBase` | _(empty)_ | Global URL prefix (e.g. `/docs`) |

## Building From Source

```bash
docker compose build
```

Or build the image directly:

```bash
docker build -t cocoar/shelf .
```

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
      - config-data:/data/config
    environment:
      - Shelf__ApiKey=${SHELF_API_KEY:-}
    restart: unless-stopped
```

Two volumes:
- **Docs volume** -- writable, so the [Upload API](./upload-api.md) can deploy versions
- **Config volume** -- contains [product registration](./product-registration.md) files and the application configuration

Shelf stores its configuration in `data/configuration.json`. If you need to customize settings beyond environment variables, mount a config file:

```yaml
volumes:
  - ./configuration.json:/data/configuration.json:ro
```

## Volume Mounting

### Single Volume

The simplest approach. All products live in one volume:

```yaml
volumes:
  - docs-data:/data/docs
  - config-data:/data/config
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
  - /srv/config:/data/config
```

## Environment Variables

See [Configuration](./configuration.md) for all options. The most important ones for Docker:

| Variable | Default | Description |
|---|---|---|
| `Shelf__AppUrl` | `http://0.0.0.0:8080` | Server binding URL and port |
| `Shelf__DocsRoot` | `/data/docs` | Root directory for documentation files |
| `Shelf__ConfigRoot` | `/data/config` | Root directory for product config files |
| `Shelf__ApiKey` | _(empty)_ | API key for protected endpoints. Empty = upload and admin disabled |
| `Shelf__PathBase` | _(empty)_ | Global URL prefix (e.g. `/docs`) |

Environment variables override values from `data/configuration.json`.

## Building From Source

```bash
docker compose build
```

Or build the image directly:

```bash
docker build -t cocoar/shelf .
```

::: tip Node.js Required
The build process requires Node.js for the Vue client (Admin UI and landing page). The Dockerfile handles this automatically, but if building outside Docker, ensure Node.js is installed.
:::

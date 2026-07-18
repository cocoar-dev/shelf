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

Shelf needs a PostgreSQL database (see [Configuration](./configuration.md#database)) and two volumes:
- **Docs volume** -- writable, so the [Upload API](./upload-api.md) can deploy versions
- **Config volume** -- [product registration](./product-registration.md) seed files, imported into the database at startup

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
| `Shelf__ConfigRoot` | `/data/config` | Root directory for product config seed files |
| `Shelf__Database__ConnectionString` | — | **Required.** PostgreSQL connection string |
| `Shelf__Modgud__Issuer` | `https://auth.cocoar.dev` | Identity provider for the [admin login](./authentication.md) |
| `Shelf__Modgud__WebClientId` / `__WebClientSecret` | — | Confidential client for the login broker |
| `Shelf__ApiKey` | _(empty)_ | Bootstrap master API key for CI/CD |
| `Shelf__AccessLog__Enabled` | `false` | Record page views for [Analytics](./admin-ui.md#analytics) |
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

# Docker

Shelf runs as a Docker container with documentation files mounted as a volume.

## Basic Setup

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

The `:ro` (read-only) flag is recommended — Shelf never writes to the docs directory.

## Volume Mounting

### Single Volume

The simplest approach. All products live in one volume:

```yaml
volumes:
  - docs-data:/data/docs:ro
```

### One Volume Per Product

Useful when products are deployed independently:

```yaml
volumes:
  - config-docs:/data/docs/configuration:ro
  - caps-docs:/data/docs/capabilities:ro
```

### Host Directory

For development or simple setups, mount a host directory directly:

```yaml
volumes:
  - /srv/docs:/data/docs:ro
```

## Environment Variables

| Variable | Default | Description |
|---|---|---|
| `Shelf__DocsRoot` | `/data/docs` | Root directory for documentation files |

## Building From Source

```bash
docker compose build
```

Or build the image directly:

```bash
docker build -t cocoar/shelf .
```

# Shelf

**Shelf your Docs.** Static documentation hosting for Cocoar products.

Shelf is an ASP.NET Core application that serves VitePress-generated documentation sites with support for multiple products and multiple versions per product.

## Features

- **Multi-Product** — Host documentation for multiple products under one domain
- **Multi-Version** — Deploy new versions without touching old ones
- **Zero Config Deployment** — Just put files in the volume, Shelf detects new versions automatically
- **Base Path Rewriting** — VitePress sites built with default `base: '/'` work out of the box
- **Resilient File Monitoring** — Powered by [Cocoar.FileSystem](https://github.com/cocoar-dev/Cocoar.FileSystem)

## Quick Start

```yaml
# docker-compose.yml
services:
  shelf:
    image: ghcr.io/cocoar-dev/shelf:latest
    ports:
      - "80:8080"
    volumes:
      - docs-data:/data/docs:ro
    restart: unless-stopped
```

Deploy your docs by placing VitePress build output in the volume:

```
/data/docs/
└── configuration/
    └── v5/
        ├── index.html
        ├── assets/
        └── ...
```

Access at `http://localhost/configuration/` (redirects to latest version).

## Documentation

Full documentation is available at [docs.cocoar.dev/shelf](https://docs.cocoar.dev/shelf/).

## Development

```bash
# Build
dotnet build ./src -c Release

# Test
dotnet test ./src -c Release

# Run locally
dotnet run --project ./src/Cocoar.Shelf

# Docker
docker compose up --build
```

## License

Apache-2.0 — see [LICENSE](LICENSE) for details.

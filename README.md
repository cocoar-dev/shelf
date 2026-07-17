# Shelf

**Shelf your Docs.** Static documentation hosting for Cocoar products.

Shelf is an ASP.NET Core application that serves VitePress-generated documentation sites with support for multiple products and multiple versions per product.

## Features

- **Multi-Product** — Host documentation for multiple products under one domain
- **Multi-Version** — Deploy new versions without touching old ones, SemVer supported
- **Upload API** — Deploy docs from CI/CD pipelines via HTTP (`curl` + ZIP)
- **Base Path Rewriting** — VitePress sites built with default `base: '/'` work out of the box
- **LLM Documentation** — Serve `llms.txt` and `llms-full.txt` for AI-friendly docs
- **Resilient File Monitoring** — Powered by [Cocoar.FileSystem](https://github.com/cocoar-dev/Cocoar.FileSystem)

## Docker Image

```bash
docker pull ghcr.io/cocoar-dev/shelf:latest
```

Available tags: `latest`, `1`, `1.0`, `1.0.0`

## Quick Start

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

Shelf requires PostgreSQL. The admin UI signs in via [Modgud](https://github.com/cocoar-dev/modgud)
(email one-time code); CI/CD uses API keys.

Deploy via Upload API:

```bash
curl -X POST \
  -H "Authorization: Bearer $SHELF_API_KEY" \
  -H "Content-Type: application/zip" \
  --data-binary @docs.zip \
  https://docs.example.com/_api/products/configuration/versions/v5.2.0
```

Or place files directly in the volume:

```
/data/docs/configuration/v5/
├── index.html
├── assets/
└── ...
```

Access at `http://localhost/configuration/` (redirects to latest version).

## Documentation

Full documentation is available at **[docs.cocoar.dev/shelf](https://docs.cocoar.dev/shelf/)**.

## Live Example

Shelf hosts its own documentation (and the docs for other Cocoar products) at **[docs.cocoar.dev](https://docs.cocoar.dev/)**.

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

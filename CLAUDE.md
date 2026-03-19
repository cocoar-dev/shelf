# Shelf

Static documentation hosting platform for Cocoar products.
Serves VitePress-generated documentation with multi-product and multi-version support.

## Build

```bash
dotnet build ./src -c Release
dotnet test ./src -c Release
dotnet publish ./src/Cocoar.Shelf -c Release -o ./artifacts
```

## Docker

```bash
docker compose up --build
```

## Architecture

- **ASP.NET Core** app serving static files from a mounted volume
- Products and versions organized as `{docsRoot}/{product}/{version}/`
- Versions are auto-detected by scanning the filesystem (directories matching `v\d+`)
- Highest version number is automatically the "latest"
- Requests without explicit version are redirected (302) to the latest version
- `FileSystemWatcher` invalidates the cache when directories are added/removed
- **Base path rewriting**: Shelf detects the original VitePress base path and rewrites it in HTML, CSS, and JS responses to match the product/version URL

## Configuration

All settings are in the `Shelf` section of `appsettings.json` or via environment variables (`Shelf__PropertyName`).

| Property | Default | Description |
|----------|---------|-------------|
| `DocsRoot` | `/data/docs` | Root directory for documentation files |
| `ConfigRoot` | `/data/config` | Root directory for product config files |
| `PathBase` | `""` | Global URL prefix (e.g. `/docs` to serve under `example.com/docs/`) |
| `VersionPattern` | `^v\d+$` | Regex for valid version directory names |
| `BasePlaceholder` | `/__shelf__/` | Placeholder for non-root VitePress base paths |
| `ApiKey` | `""` | API key for upload endpoint. Empty = upload disabled (503) |
| `MaxUploadSizeBytes` | `104857600` | Max upload size (100 MB) |

### PathBase

When Shelf runs behind a reverse proxy under a sub-path, set `PathBase` to match. All routes, redirects, and base path rewriting automatically include the prefix:

```
PathBase=""      → /configuration/v5/    /api/products
PathBase="/docs" → /docs/configuration/v5/    /docs/api/products
```

## Product Config

Products must be registered via JSON files in `{ConfigRoot}/products/`. One file per product:

```
/data/config/products/configuration.json
```
```json
{
  "name": "configuration",
  "displayName": "Cocoar.Configuration",
  "description": "Reactive configuration for .NET",
  "source": "upload"
}
```

Config files are loaded at startup and live-reloaded via FileSystemWatcher. The `name` field is the product identifier used in URLs and API calls.

## Upload API

Shelf provides an HTTP API for deploying documentation versions, designed for CI/CD pipelines.

### Endpoints

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `GET` | `/api/products` | No | All registered products with version info |
| `GET` | `/api/products/{product}/versions` | No | Versions of a specific product |
| `POST` | `/api/products/{product}/versions/{version}` | Bearer | Upload a ZIP as a new version |

### Upload Flow

```bash
curl -X POST \
  -H "Authorization: Bearer $SHELF_API_KEY" \
  -H "Content-Type: application/zip" \
  --data-binary @docs.zip \
  https://docs.cocoar.dev/api/products/configuration/versions/v6
```

1. API key is validated (401 if wrong, 503 if not configured)
2. Product must be registered in config (404 if not)
3. Version format must match `VersionPattern` (400 if not)
4. ZIP is extracted to a temp directory, validated (index.html required), and atomically moved into place
5. ManifestService detects the new version automatically via FileSystemWatcher
6. Response: 201 Created

### Error Responses

| Status | When |
|--------|------|
| 201 | Successfully deployed |
| 400 | Invalid version format, corrupt ZIP, or missing index.html |
| 401 | Missing or invalid API key |
| 404 | Product not registered |
| 409 | Concurrent upload for same product/version |
| 413 | ZIP exceeds `MaxUploadSizeBytes` |
| 503 | No API key configured (upload disabled) |

### Security

- **API key auth**: `Authorization: Bearer {key}` checked against `ShelfOptions.ApiKey`
- **ZIP-Slip protection**: All extracted paths are validated to stay within the target directory
- **Atomic deployment**: Extract to temp dir, validate, then move — Shelf never serves a half-extracted state
- **Per-version locking**: Concurrent uploads for the same product/version return 409

## Base Path Rewriting

Shelf rewrites the VitePress base path so docs built with `base: '/'` work under `/{product}/{version}/` (or `{PathBase}/{product}/{version}/` when PathBase is set).
Rewriting happens at four levels:

1. **HTML**: `href="/..."` and `src="/..."` attribute values
2. **HTML site data**: `"base":"/"` in the inlined `__VP_SITE_DATA__` JSON
3. **CSS**: `url(/...)` font/asset references
4. **JS**: Two targeted VitePress patterns (router base constant + modulepreload URL builder) and search index document IDs

For non-root bases (e.g., `/__shelf__/`), a simple global string replace is used instead.

## Key Files

- `src/Cocoar.Shelf/Program.cs` — App startup, service registration, middleware pipeline
- `src/Cocoar.Shelf/ShelfOptions.cs` — All configuration properties
- `src/Cocoar.Shelf/Middleware/DocsRoutingMiddleware.cs` — Core routing logic + response rewriting
- `src/Cocoar.Shelf/Services/ManifestService.cs` — Scans filesystem, caches product versions
- `src/Cocoar.Shelf/Services/ProductConfigService.cs` — Reads product JSON configs, live-reload
- `src/Cocoar.Shelf/Services/UploadService.cs` — ZIP extraction, validation, atomic deployment
- `src/Cocoar.Shelf/Endpoints/ApiEndpoints.cs` — Minimal API endpoints (products, versions, upload)
- `src/Cocoar.Shelf/Endpoints/ApiKeyFilter.cs` — Bearer token auth for upload endpoint
- `src/Cocoar.Shelf/Services/BasePathDetector.cs` — Detects original VitePress base from index.html
- `src/Cocoar.Shelf/Services/BasePathRewriter.cs` — Rewrites base path in HTML/CSS/JS responses

## Volume Structure

```
/data/docs/{product}/{version}/   (e.g. /data/docs/configuration/v6/)
/data/config/products/{product}.json
```

Docs: New versions are detected automatically via FileSystemWatcher — either deployed via Upload API or placed manually.
Config: Product registration files, also live-reloaded via FileSystemWatcher.

## Design Decisions

- **ProductConfigService and ManifestService are independent** — no mutual dependency. API endpoints compose both.
- **Upload writes directly into the docs volume** → ManifestService detects it automatically. No manual cache invalidation needed.
- **Product must be registered first** (JSON config file) → no auto-creation. Enforces explicit product registration.
- **Source type as string** in ProductConfig (`"upload"`, future `"github-release"`) → extensible without breaking changes.

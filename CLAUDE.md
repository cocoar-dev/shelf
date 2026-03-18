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

## Base Path Rewriting

Shelf rewrites the VitePress base path so docs built with `base: '/'` work under `/{product}/{version}/`.
Rewriting happens at four levels:

1. **HTML**: `href="/..."` and `src="/..."` attribute values
2. **HTML site data**: `"base":"/"` in the inlined `__VP_SITE_DATA__` JSON
3. **CSS**: `url(/...)` font/asset references
4. **JS**: Two targeted VitePress patterns (router base constant + modulepreload URL builder) and search index document IDs

For non-root bases (e.g., `/__shelf__/`), a simple global string replace is used instead.

## Key Files

- `src/Cocoar.Shelf/Middleware/DocsRoutingMiddleware.cs` — Core routing logic + response rewriting
- `src/Cocoar.Shelf/Services/ManifestService.cs` — Scans filesystem, caches product versions
- `src/Cocoar.Shelf/Services/BasePathDetector.cs` — Detects original VitePress base from index.html
- `src/Cocoar.Shelf/Services/BasePathRewriter.cs` — Rewrites base path in HTML/CSS/JS responses
- `src/Cocoar.Shelf/ShelfOptions.cs` — Configuration (docs root path, version pattern)

## Volume Structure

Shelf expects static files in a mounted volume with this layout:

```
/data/docs/{product}/{version}/   (e.g. /data/docs/configuration/v6/)
```

New versions are detected automatically via FileSystemWatcher. Shelf only serves — how files get into the volume is outside its scope.

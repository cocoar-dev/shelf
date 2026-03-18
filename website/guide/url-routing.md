# URL Routing

Shelf routes requests based on the URL structure. Every URL starts with a product name, optionally followed by a version.

## Routing Rules

| URL | Behavior |
|---|---|
| `/configuration/` | **Redirect** to latest version (e.g., `/configuration/v5/`) |
| `/configuration/v5/` | Serve `v5/index.html` |
| `/configuration/v5/guide/getting-started.html` | Serve `v5/guide/getting-started.html` |
| `/configuration/v5/llms.txt` | Serve `v5/llms.txt` |

## Latest Version Redirect

When a URL has no explicit version (e.g., `/configuration/` or `/configuration/guide/getting-started.html`), Shelf **redirects** (HTTP 302) to the latest version URL.

This redirect is necessary because VitePress's client-side router needs the URL to match the base path. A transparent proxy would cause the URL in the browser (`/configuration/`) to differ from the base path in the JavaScript (`/configuration/v5/`), breaking client-side navigation.

## How "Latest" Is Determined

Shelf scans the product directory for subdirectories matching the version pattern (`v1`, `v2`, ...) and picks the **highest version number**.

Example: if `/data/docs/configuration/` contains `v3/`, `v5/`, `v4/`, then the latest version is `v5`.

The sorting is **numeric**, not alphabetical: `v10` is higher than `v9`.

## Version Detection

The first path segment after the product name is checked against the version pattern:

- `/configuration/v5/...` — `v5` matches `^v\d+$` → explicit version, serve directly
- `/configuration/guide/...` — `guide` does not match → redirect to latest

## VitePress Base Path

VitePress bakes the `base` path into the build output at build time. Shelf handles this automatically through [Base Path Rewriting](./base-path-rewriting.md) — you do **not** need to set a specific `base` in your VitePress config.

Just build your docs normally:

```bash
npx vitepress build
```

Shelf detects the original base path and rewrites it to match the product/version URL when serving responses.

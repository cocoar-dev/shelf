# URL Routing

Shelf routes requests based on the URL structure. Every URL starts with a product name, optionally followed by a version.

## Routing Rules

| URL | Behavior |
|---|---|
| `/configuration/` | **Redirect** to latest stable version (e.g., `/configuration/v5.2.0/`) |
| `/configuration/v5.2.0/` | Serve `v5.2.0/index.html` |
| `/configuration/v5.2.0/guide/getting-started.html` | Serve `v5.2.0/guide/getting-started.html` |
| `/configuration/v5.2.0/llms.txt` | Serve `v5.2.0/llms.txt` |

## Latest Version Redirect

When a URL has no explicit version (e.g., `/configuration/` or `/configuration/guide/getting-started.html`), Shelf **redirects** (HTTP 302) to the latest version URL.

This redirect is necessary because VitePress's client-side router needs the URL to match the base path. A transparent proxy would cause the URL in the browser (`/configuration/`) to differ from the base path in the JavaScript (`/configuration/v5/`), breaking client-side navigation.

## How "Latest" Is Determined

Shelf scans the product directory for subdirectories matching the [version pattern](./configuration.md#version-pattern) and picks the **highest stable version**.

- Sorting is **numeric** by Major.Minor.Patch: `v5.2.0` > `v5.1.0` > `v5.0.0`
- **Stable versions are preferred** over pre-releases: if both `v5.2.0` and `v6.0.0-beta.1` exist, the latest is `v5.2.0`
- If only pre-release versions exist, the highest one is used

## Version Detection

The first path segment after the product name is checked against the version pattern:

- `/configuration/v5.2.0/...` — matches version pattern → explicit version, serve directly
- `/configuration/guide/...` — does not match → redirect to latest

## VitePress Base Path

VitePress bakes the `base` path into the build output at build time. Shelf handles this automatically through [Base Path Rewriting](./base-path-rewriting.md) — you do **not** need to set a specific `base` in your VitePress config.

Just build your docs normally:

```bash
npx vitepress build
```

Shelf detects the original base path and rewrites it to match the product/version URL when serving responses.

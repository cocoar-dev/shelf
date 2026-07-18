---
title: How It Works
description: "Architecture in one page — an ASP.NET Core server over a docs volume: version auto-detection, latest-redirects, base path rewriting, and PostgreSQL for products, users and analytics."
---

# How It Works

Shelf is intentionally simple. It's an ASP.NET Core application that serves static files from a volume with smart URL routing and automatic base path rewriting.

## Volume Structure

Shelf expects documentation files organized by product and version:

```
/data/docs/
├── configuration/
│   ├── v4/
│   │   ├── index.html
│   │   ├── assets/
│   │   └── ...
│   └── v5/
│       ├── index.html
│       ├── assets/
│       └── ...
├── capabilities/
│   └── v1/
│       └── ...
└── filesystem/
    └── v1/
        └── ...
```

Each product is a directory. Each version within a product is a subdirectory matching the pattern `v{number}` (e.g., `v1`, `v5`, `v12`).

## Version Detection

Shelf scans the filesystem to determine available versions. The **highest version number** is automatically the "latest". There is no manifest file or configuration needed.

When a request for `/configuration/` comes in (no version), Shelf redirects to the latest version:

1. Identifies `configuration` as the product
2. Scans its subdirectories for version folders
3. Picks the highest version (e.g., `v5`)
4. Redirects to `/configuration/v5/`

Requests with an explicit version (e.g., `/configuration/v5/guide/getting-started.html`) are served directly.

## Base Path Rewriting

VitePress bakes the `base` path into all generated files at build time. Normally, you'd have to build your docs with the exact base path matching where they'll be hosted (e.g., `base: '/configuration/v5/'`).

**Shelf removes this requirement.** You can build your VitePress site with the default `base: '/'` and Shelf rewrites all paths on the fly when serving responses. See [Base Path Rewriting](./base-path-rewriting.md) for details on how this works.

## Automatic Cache Invalidation

Shelf watches the docs directory for changes using [Cocoar.FileSystem](https://github.com/cocoar-dev/Cocoar.FileSystem)'s `ResilientFileSystemMonitor`. When a new version directory is created or removed, the cached version list for that product is automatically invalidated.

This means deploying a new version is instant — whether via the [Upload API](./upload-api.md) or by copying files directly into the volume. Shelf picks it up without a restart.

We use `ResilientFileSystemMonitor` instead of .NET's raw `FileSystemWatcher` because it provides:

- **Automatic recovery** — if the docs volume is temporarily unavailable (Docker mount issues, network glitches), the monitor recovers automatically instead of silently dying
- **Debouncing** — rapid filesystem changes during a deployment are consolidated into a single cache invalidation
- **Audit timer** — periodically verifies that no events were missed, a known issue with `FileSystemWatcher` under high load
- **Polling fallback** — if native filesystem events aren't available (certain network shares, Docker volume edge cases), the monitor falls back to polling automatically

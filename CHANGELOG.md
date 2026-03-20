# Changelog

All notable changes to Shelf will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/), and this project adheres to [Semantic Versioning](https://semver.org/).

## [1.0.0]

### Added

- **Upload API** — Deploy documentation versions via HTTP POST with ZIP files
- **Product Registration** — JSON-based product configuration with live reload
- **Landing Page** — Optional product overview page at the root URL (`EnableLandingPage`)
- **SemVer Versioning** — Support for `v5`, `v5.2`, `v5.2.0`, `v5.2.0-beta.1` version formats
- **PathBase** — Configurable global URL prefix for sub-path hosting
- **API Endpoints** — `GET /api/products`, `GET /api/products/{product}/versions`, `POST /api/products/{product}/versions/{version}`
- **API Key Authentication** — Bearer token auth for upload endpoint
- **LLM Documentation** — Guide for serving `llms.txt` and `llms-full.txt`
- **VitePress Documentation** — Full documentation site with guides for setup, deployment, and troubleshooting

### Fixed

- ZIP extraction now normalizes backslashes from Windows-created archives
- Directory detection works correctly for SemVer version paths containing dots (e.g., `v0.1`)

## [0.1.0] — 2025-03-18

### Added

- Initial release
- Static file serving from mounted volumes
- Multi-product and multi-version support
- Automatic version detection via filesystem scanning
- Latest version redirect (302)
- Base path rewriting for VitePress (HTML, CSS, JS)
- Immutable caching for hashed assets
- Resilient file monitoring via Cocoar.FileSystem

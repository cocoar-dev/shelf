---
title: Base Path Rewriting
description: "Why VitePress sites built with base '/' work under /product/version/ URLs: the HTML/CSS/JS response rewriter and its placeholder mechanics."
---

# Base Path Rewriting

Shelf automatically rewrites the VitePress base path so that documentation sites built with the default `base: '/'` work correctly when served under a product/version URL like `/configuration/v5/`.

## Why This Is Needed

VitePress bakes the `base` path into the build output at build time. A site built with `base: '/'` generates links like:

```html
<link href="/assets/style.a1b2c3d4.css">
<script src="/assets/app.x7y8z9.js">
```

When Shelf serves these files under `/configuration/v5/`, the browser would request `/assets/style.a1b2c3d4.css` — without the product/version prefix. That request would fail.

Shelf solves this by detecting the original base path and rewriting it in all text-based responses before sending them to the browser.

## How Detection Works

When a product/version is first accessed, Shelf reads the `index.html` and looks for the first `href` attribute pointing to `assets/`. The path prefix before `assets/` is the original base path.

For example:
- `href="/assets/style.css"` → original base is `/`
- `href="/docs/assets/style.css"` → original base is `/docs/`
- `href="/__shelf__/assets/style.css"` → original base is `/__shelf__/`

The detected base is cached per product/version.

## What Gets Rewritten

Shelf rewrites different file types with different strategies:

### HTML Files

Attribute values starting with the original base path are rewritten:

| Before | After |
|---|---|
| `href="/assets/style.css"` | `href="/configuration/v5/assets/style.css"` |
| `src="/assets/app.js"` | `src="/configuration/v5/assets/app.js"` |
| `href="/guide/getting-started.html"` | `href="/configuration/v5/guide/getting-started.html"` |

External URLs (`href="https://..."`) and anchors (`href="#section"`) are not affected.

Additionally, the VitePress site data blob (`__VP_SITE_DATA__`) contains a `"base":"/"` property that is rewritten to `"base":"/configuration/v5/"`. This is critical for the client-side Vue router to work correctly.

### CSS Files

`url()` references are rewritten:

| Before | After |
|---|---|
| `url(/assets/font.woff2)` | `url(/configuration/v5/assets/font.woff2)` |

### JavaScript Files

JS rewriting is the most delicate part. VitePress generates minified JavaScript where the base path appears in specific patterns. Shelf targets these precisely to avoid false positives:

**Pattern 1 — Router base constant:**
```js
// Before:
){const n="/";t=pi(t.slice(n.length)
// After:
){const n="/configuration/v5/";t=pi(t.slice(n.length)
```

**Pattern 2 — Module preload URL builder:**
```js
// Before:
=function(e){return"/"+e}
// After:
=function(e){return"/configuration/v5/"+e}
```

**Pattern 3 — Search index document IDs:**

The local search index contains document paths like `"/guide/caching.html#caching"`. These are rewritten using a broader string replacement, but only in files that contain search index data (identified by the presence of `"documentIds"`).

::: warning Important
The JS patterns are specific to VitePress 1.x. If a future VitePress version changes the minified output structure, these patterns may need updating. If you encounter broken client-side navigation after a VitePress upgrade, this is the first place to investigate.
:::

## When Original Base Is Not `/`

If the original base is a unique string (e.g., `/__shelf__/` or `/docs/`), Shelf uses a simple string replacement across all text files. This is completely safe because the original base is unique enough to have no false positives.

This means you have two options for building your docs:

1. **Default `base: '/'`** — zero configuration, Shelf handles everything with targeted rewriting
2. **Custom `base`** (e.g., `base: '/__shelf__/'`) — even safer, simple string replacement with no pattern matching needed

## What Is Not Rewritten

- **Binary files** (images, fonts, WebAssembly) are served as-is
- **Plain text files** (`llms.txt`, `llms-full.txt`) are served as-is
- **External URLs** (`https://...`, `//cdn...`) are never modified
- **Anchor links** (`#section`) are never modified

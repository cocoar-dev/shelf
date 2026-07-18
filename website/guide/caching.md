---
title: Caching
description: "The cache headers Shelf applies: immutable hashed assets, revalidated HTML, and how the file watcher invalidates version manifests."
---

# Caching

Shelf applies caching headers to optimize delivery of static assets.

## Immutable Assets

VitePress generates CSS and JavaScript files with content hashes in their filenames (e.g., `style.a1b2c3d4.css`). These files never change — if the content changes, the filename changes too.

Shelf detects these hashed filenames and serves them with aggressive cache headers:

```
Cache-Control: public, max-age=31536000, immutable
```

This tells browsers and CDNs to cache these files for one year without revalidation.

## HTML Files

HTML files are served without explicit cache headers, meaning browsers will revalidate on each request. This ensures users always see the latest content after a new version is deployed.

## LLM Files

`llms.txt` and `llms-full.txt` are plain text files served with default caching behavior, similar to HTML files.

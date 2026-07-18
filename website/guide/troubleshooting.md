---
title: Troubleshooting
description: "Symptoms and fixes for the common failure modes: missing docs, broken styling, upload rejections, login problems, and where the useful logs live."
---

# Troubleshooting

## Documentation Not Showing Up

**Check the volume mount.** Verify that the files are actually visible inside the container:

```bash
docker exec shelf ls -la /data/docs/
docker exec shelf ls -la /data/docs/configuration/
```

**Check the version directory name.** Shelf only recognizes directories matching the version pattern (default: `v1`, `v2`, etc.). A directory named `latest`, `main`, or `1.0` will not be detected.

## Wrong Version Served as "Latest"

Shelf determines the latest version by sorting version numbers numerically. `v10` is higher than `v9`, not lower (it's not alphabetical sorting).

Verify which versions Shelf sees:

```bash
docker exec shelf ls /data/docs/configuration/
```

Or check via the API:

```bash
curl http://localhost/_api/products/configuration/versions
```

## Page Loads But Navigation Is Broken

This is most likely a **base path rewriting** issue. Shelf rewrites the VitePress base path in HTML, CSS, and JavaScript files. If the rewriting misses something, the page may render initially but client-side navigation (clicking sidebar links) fails.

**Check the site data base path.** Open the browser DevTools console and run:

```js
__VP_SITE_DATA__.base
```

This should return the full product/version path (e.g., `"/configuration/v5/"`). If it returns `"/"`, the site data rewriting is not working.

**Check the JavaScript base path.** In the Network tab, find the `framework.*.js` file and search for the base path. It should contain your product/version path, not `"/"`.

**VitePress version mismatch.** The JavaScript rewriting targets specific patterns in VitePress 1.x minified output. If you've upgraded VitePress and navigation breaks, the minified patterns may have changed. See [Base Path Rewriting](./base-path-rewriting.md) for details on which patterns Shelf targets.

## CSS/Fonts Not Loading

**Check the CSS `url()` paths.** In DevTools Network tab, look for 404 errors on font or CSS files. If the paths still have `"/"` as the prefix instead of `"/configuration/v5/"`, the CSS rewriting is not working.

This can happen if the CSS file's content type is not detected as `text/css`. Check that the file extension is `.css`.

## Files Exist But Return 404

**Check file extensions.** Shelf appends `index.html` to requests without a file extension. If your files have non-standard extensions, they may not be found.

**Check path traversal protection.** Shelf blocks any resolved path that falls outside the docs root directory. If your symlinks or relative paths point outside the volume, they will be blocked.

## Admin UI Not Loading

**Check the URL.** The Admin UI is served at `/admin/` (with trailing slash). If you have a `PathBase` configured, the URL is `{PathBase}/admin/`.

**Check browser console.** Open DevTools and look for JavaScript errors or failed network requests to `/_api/` endpoints.

**Cookie issues.** If login succeeds but you're immediately logged out, check that cookies are not being blocked. The Admin UI uses a `shelf.auth` cookie for session management.

## Login Code Never Arrives

The admin login is brokered through [Modgud](./authentication.md). Check that `Shelf__Modgud__Issuer` points at a reachable Modgud instance, the `WebClientId`/`WebClientSecret` match a registered confidential client with the `urn:cocoar:otp` grant, and the email address exists as a Modgud user. Modgud throttles code re-sends per user for a couple of minutes.

## Uploads Return 401

No valid API key was presented. Check the Bearer token against the product's per-product key or a master key — see [Authentication](./authentication.md#api-keys-cicd).

## Startup Fails: Database Required

Shelf requires PostgreSQL. Set `Shelf__Database__ConnectionString` — see [Configuration](./configuration.md#database).

## Overlapping Volume Mounts

Docker supports mounting multiple volumes at nested paths. If you have both a general mount and a product-specific mount:

```yaml
volumes:
  - docs-data:/data/docs
  - config-docs:/data/docs/configuration
```

The more specific mount (`/data/docs/configuration`) takes precedence and **hides** any `configuration/` directory that exists in the general `docs-data` volume. This is standard Linux mount behavior and is independent of the order in the compose file.

If a product's documentation is unexpectedly empty or outdated, check whether someone has added a more specific volume mount that shadows the expected data.

## Container Restarts

Shelf caches two things in memory:

1. **Version lists** per product -- invalidated automatically via `FileSystemWatcher`
2. **Detected base paths** per product/version -- cached indefinitely

If you suspect stale data after a problematic deployment, restarting the container clears all caches:

```bash
docker compose restart shelf
```

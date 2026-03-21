# Admin UI

Shelf includes a built-in web interface for managing products and documentation versions. The Admin UI is a Vue 3 single-page application served at `/admin/`.

## Accessing the Admin UI

Navigate to `/admin/` (or `{PathBase}/admin/` if a [PathBase](./configuration.md#pathbase) is configured). You'll be prompted to log in with your API key.

::: tip
The Admin UI requires an API key to be [configured](./configuration.md). If no API key is set, authentication is disabled and the Admin UI cannot be used.
:::

## Login

Enter your API key on the login screen. The Admin UI authenticates via a cookie session -- it sends your API key to `/_api/auth/login`, and the server sets a `shelf.auth` cookie. This means you stay logged in across page refreshes until you log out or the session expires.

See [Authentication](./authentication.md) for details on how cookie auth works.

## Dashboard

After logging in, the dashboard shows an overview of your Shelf instance:

- Total number of registered products
- Total number of deployed versions
- Quick links to product management

## Managing Products

### Creating a Product

1. Click "Create Product" on the dashboard or products page
2. Fill in the product details:
   - **Name** -- Product identifier used in URLs (e.g., `configuration`). Cannot be changed after creation.
   - **Display Name** -- Human-readable name (e.g., `Cocoar.Configuration`)
   - **Description** -- Short description of the product
   - **Source** -- Deployment source type (default: `upload`)
   - **Visibility** -- `public` or `preview`
3. Save the product

### Editing a Product

Click on a product to edit its display name, description, source, or visibility. The product name (URL identifier) cannot be changed after creation.

### Deleting a Product

Delete a product from the product detail view. This removes the product registration but does **not** delete deployed documentation files from disk.

### Visibility

Products can be set to `public` or `preview`:

| Visibility | Landing Page | Direct URL | API |
|------------|-------------|------------|-----|
| `public` | Shown by default | Accessible | Listed |
| `preview` | Hidden (shown with toggle) | Accessible | Listed |

Preview products are useful for documentation that is in development or not yet ready for general discovery. The docs are always accessible via direct URL regardless of visibility.

## Managing Versions

### Uploading a Version

1. Navigate to a product's detail page
2. Click "Upload Version"
3. Enter the version identifier (e.g., `v5.2.0`)
4. Select the ZIP file containing your VitePress build output
5. Upload

The ZIP must contain `index.html` at the root. See [Upload API](./upload-api.md) for details on the ZIP format.

### Deleting a Version

From the product detail page, click the delete button next to a version. This removes the version's files from disk. The ManifestService detects the change automatically.

### Version List

The product detail page shows all deployed versions with:

- Version identifier
- Whether it's the "latest" (highest stable version)
- Direct link to the documentation

## When to Use the Admin UI vs API

| Task | Admin UI | API / CLI |
|------|----------|-----------|
| Initial setup / exploration | Recommended | -- |
| One-off product management | Recommended | -- |
| CI/CD deployment | -- | Recommended |
| Automated workflows | -- | Recommended |
| Bulk operations | -- | Recommended |

The Admin UI and the API are backed by the same endpoints. Anything you do in the Admin UI can also be done via `curl` or any HTTP client.

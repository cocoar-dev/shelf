# Authentication

Shelf uses a single API key for authentication. Protected endpoints (product CRUD, version upload/delete) require one of two authentication methods: cookie session or Bearer token.

## API Key Configuration

Set the API key via environment variable or `data/configuration.json`:

```yaml
environment:
  - Shelf__ApiKey=your-secret-key-here
```

If no API key is configured, all protected endpoints return `503 Service Unavailable`. Public endpoints (`GET /_api/products`, `GET /_api/products/{product}/versions`) work without authentication.

## Authentication Methods

Both methods authenticate against the same configured API key. Use whichever fits your workflow.

### Cookie Session (Browser / Admin UI)

The cookie method is designed for browser-based usage, primarily the [Admin UI](./admin-ui.md).

**Login:**

```bash
curl -X POST \
  -H "Content-Type: application/json" \
  -d '{"apiKey": "your-secret-key-here"}' \
  https://docs.cocoar.dev/_api/auth/login
```

On success, the server sets a `shelf.auth` cookie. Subsequent requests with this cookie are authenticated automatically.

**Check auth status:**

```bash
curl https://docs.cocoar.dev/_api/auth/me
```

Returns the current authentication state (authenticated or not).

**Logout:**

```bash
curl -X POST https://docs.cocoar.dev/_api/auth/logout
```

Clears the `shelf.auth` cookie.

### Bearer Token (CI/CD / API)

The Bearer method is designed for programmatic access from CI/CD pipelines and scripts.

```bash
curl -X POST \
  -H "Authorization: Bearer your-secret-key-here" \
  -H "Content-Type: application/zip" \
  --data-binary @docs.zip \
  https://docs.cocoar.dev/_api/products/configuration/versions/v6
```

The API key is sent in the `Authorization` header with every request. No session state is maintained.

## Which Method to Use

| Scenario | Method | Why |
|----------|--------|-----|
| Admin UI | Cookie | Browser handles cookies automatically |
| CI/CD pipelines | Bearer | Stateless, no cookie management needed |
| Scripts / automation | Bearer | Simpler, one header per request |
| Interactive API testing | Either | Both work with tools like curl or Postman |

## Auth Endpoints

| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/_api/auth/login` | Authenticate with API key, receive session cookie |
| `POST` | `/_api/auth/logout` | Clear session cookie |
| `GET` | `/_api/auth/me` | Check current authentication status |

## Protected Endpoints

These endpoints require authentication (cookie or Bearer):

| Method | Path |
|--------|------|
| `POST` | `/_api/products` |
| `PUT` | `/_api/products/{product}` |
| `DELETE` | `/_api/products/{product}` |
| `POST` | `/_api/products/{product}/versions/{version}` |
| `DELETE` | `/_api/products/{product}/versions/{version}` |

## Error Responses

| Status | When |
|--------|------|
| `401` | Missing or invalid API key (wrong Bearer token or no valid cookie) |
| `503` | No API key configured on the server |

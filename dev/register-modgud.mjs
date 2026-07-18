#!/usr/bin/env node
/**
 * Registers shelf in a REAL modgud instance (auth-test / auth.cocoar.dev) — the productionized
 * sibling of seed-modgud.mjs: creates the `shelf` App (+ shelf:admin permission), the app-scoped
 * `shelf` scope + OAuth API, and the confidential `shelf-web` client for the OIDC
 * authorization-code flow (code + PKCE; redirect URIs derived from APP_URLS). Creates NO users
 * and takes the client secret from the environment — nothing here is dev-only. Re-running
 * patches an existing client's grants/redirect URIs.
 *
 * Idempotent — re-running skips entities that already exist by natural key.
 *
 *   BASE_URL=https://auth-test.cocoar.dev \
 *   ADMIN_USER=... ADMIN_PASSWORD=... \
 *   CLIENT_SECRET=<the secret from the stack .env> \
 *   APP_URLS=http://10.10.10.242:8083 \
 *   node dev/register-modgud.mjs
 *
 * Also reports (without creating) whether the login users you care about exist and are
 * confirmed — pass CHECK_USERS=a@x,b@y (default: bwi@doob.at).
 */

const BASE = (process.env.BASE_URL ?? '').replace(/\/$/, '')
const ADMIN_USER = process.env.ADMIN_USER
const ADMIN_PASSWORD = process.env.ADMIN_PASSWORD
const CLIENT_SECRET = process.env.CLIENT_SECRET
const CHECK_USERS = (process.env.CHECK_USERS ?? 'bwi@doob.at').split(',').map(s => s.trim()).filter(Boolean)

// Comma-separated base URLs of the shelf deployment(s), e.g. https://docs.cocoar.dev or
// http://10.10.10.242:8083 — /signin-oidc + /signout-callback-oidc are appended.
const APP_URLS = (process.env.APP_URLS ?? '').split(',').map(s => s.trim().replace(/\/$/, '')).filter(Boolean)
const REDIRECT_URIS = APP_URLS.map(u => `${u}/signin-oidc`)
const POST_LOGOUT_URIS = APP_URLS.map(u => `${u}/signout-callback-oidc`)

if (!BASE || !ADMIN_USER || !ADMIN_PASSWORD || !CLIENT_SECRET || APP_URLS.length === 0) {
  console.error('Required env: BASE_URL, ADMIN_USER, ADMIN_PASSWORD, CLIENT_SECRET, APP_URLS')
  process.exit(1)
}

const WEB_CLIENT_ID = 'shelf-web'
const AUDIENCE = 'shelf'
const SCOPES = ['openid', 'email', 'profile', 'roles', 'permissions', 'shelf']
const GRANTS = ['authorization_code', 'refresh_token']

let cookies = ''
async function req(method, path, body = null) {
  const headers = { Accept: 'application/json', Cookie: cookies }
  if (body !== null) headers['Content-Type'] = 'application/json'
  const res = await fetch(`${BASE}${path}`, { method, headers, body: body !== null ? JSON.stringify(body) : undefined, redirect: 'manual' })
  const set = typeof res.headers.getSetCookie === 'function' ? res.headers.getSetCookie() : (res.headers.get('set-cookie')?.split(/,(?=[^;]+=)/) ?? [])
  for (const sc of set) {
    const eq = sc.indexOf('='), semi = sc.indexOf(';')
    if (eq < 0) continue
    const name = sc.slice(0, eq), value = sc.slice(eq + 1, semi < 0 ? undefined : semi)
    cookies = cookies.split('; ').filter(c => c && !c.startsWith(name + '=')).concat([`${name}=${value}`]).join('; ')
  }
  const text = await res.text()
  let parsed = null
  if (text) { try { parsed = JSON.parse(text) } catch { parsed = text } }
  return { ok: res.ok, status: res.status, body: parsed }
}
const get = p => req('GET', p)
const post = (p, b) => req('POST', p, b)
const patch = (p, b) => req('PATCH', p, b)
const die = (msg, res) => { console.error(`✗ ${msg}: HTTP ${res.status} — ${typeof res.body === 'string' ? res.body : JSON.stringify(res.body)}`); process.exit(1) }

async function main() {
  // 1. Login.
  const login = await post('/api/account/login', { UserName: ADMIN_USER, Password: ADMIN_PASSWORD })
  if (!login.ok) die('admin login', login)
  console.log(`✓ logged in as ${ADMIN_USER} @ ${BASE}`)

  // 2. App `shelf` (+ shelf:admin permission for RBAC).
  const apps = await get('/api/app')
  if (!apps.ok) die('list apps', apps)
  let app = (apps.body ?? []).find(a => a.Slug === AUDIENCE)
  if (app) console.log(`— app '${AUDIENCE}' exists`)
  else {
    const r = await post('/api/app', {
      Slug: AUDIENCE, DisplayName: 'Shelf', Description: 'Cocoar documentation hosting',
      Permissions: [{ Resource: 'shelf', Action: 'admin', Description: 'Shelf admin' }],
    })
    if (!r.ok) die('create app', r)
    app = r.body
    console.log(`✓ app '${AUDIENCE}' created`)
  }
  const appId = app.Id

  // 3. Scope `shelf` (app-scoped; Resources binds the token audience to `shelf`).
  const scopes = await get('/api/admin/oauth/scopes')
  if ((scopes.body?.Items ?? []).some(s => s.Name?.toLowerCase() === AUDIENCE)) console.log(`— scope '${AUDIENCE}' exists`)
  else {
    const r = await post('/api/admin/oauth/scopes', {
      Name: AUDIENCE, DisplayName: 'Shelf API', Description: 'Access to the Shelf API',
      Resources: [AUDIENCE], Enabled: true, ShowInDiscoveryDocument: true, AppId: appId,
    })
    if (!r.ok) die('create scope', r)
    console.log(`✓ scope '${AUDIENCE}' created`)
  }

  // 4. OAuthApi `shelf` = the resource server / audience.
  const apis = await get('/api/admin/oauth/apis')
  if ((apis.body?.Items ?? []).some(a => a.Name?.toLowerCase() === AUDIENCE)) console.log(`— api '${AUDIENCE}' exists`)
  else {
    const r = await post('/api/admin/oauth/apis', {
      Name: AUDIENCE, DisplayName: 'Shelf API', Description: 'Shelf resource server',
      Enabled: true, Scopes: [AUDIENCE], UserClaims: [], AppId: appId, PermissionIds: [],
    })
    if (!r.ok) die('create api', r)
    console.log(`✓ api '${AUDIENCE}' created`)
  }

  // 5. Confidential BFF client — OIDC authorization-code flow (code + PKCE).
  const clients = await get('/api/admin/oauth/clients')
  const existingClient = (clients.body?.Items ?? []).find(c => c.ClientId?.toLowerCase() === WEB_CLIENT_ID)
  if (existingClient) {
    // Patch grants/redirect URIs/scopes onto the existing client (idempotent; secret untouched).
    const upd = await req('PUT', `/api/admin/oauth/clients/${existingClient.Id}`, {
      AllowedGrantTypes: GRANTS, RedirectUris: REDIRECT_URIS, PostLogoutRedirectUris: POST_LOGOUT_URIS,
      Scopes: SCOPES,
    })
    if (!upd.ok) die('update client grants/redirect uris', upd)
    console.log(`✓ client '${WEB_CLIENT_ID}' updated (code flow, redirect URIs; secret unchanged)`)
  } else {
    const r = await post('/api/admin/oauth/clients', {
      ClientId: WEB_CLIENT_ID, DisplayName: 'Shelf Web (BFF)', ClientType: 'confidential',
      ClientSecret: CLIENT_SECRET, ConsentType: 'implicit',
      RedirectUris: REDIRECT_URIS, PostLogoutRedirectUris: POST_LOGOUT_URIS,
      Scopes: SCOPES, AllowedGrantTypes: GRANTS, RequireConsent: false, RequireClientSecret: true,
      AccessTokenType: 'Jwt', Enabled: true, AppIds: [appId],
    })
    if (!r.ok) die('create client', r)
    console.log(`✓ client '${WEB_CLIENT_ID}' created (secret from env)`)
  }

  // 6. Report login users (must exist + be confirmed in this realm) — no writes.
  const users = await get('/api/user')
  const userItems = Array.isArray(users.body) ? users.body : (users.body?.Items ?? [])
  for (const email of CHECK_USERS) {
    const row = userItems.find(u => u.Email?.toLowerCase() === email.toLowerCase())
    if (!row) console.log(`! user ${email}: NOT FOUND — create/confirm in modgud before login works`)
    else console.log(`✓ user ${email}: exists${row.EmailConfirmed === false ? ' but email NOT confirmed' : ''}`)
  }

  console.log('\nDone. Shelf can now run the OIDC code flow against this modgud.')
}

main().catch(e => { console.error(e); process.exit(1) })

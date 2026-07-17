#!/usr/bin/env node
/**
 * Seeds a local modgud IdP with everything shelf needs to federate locally:
 * the `shelf` App (+ shelf:admin permission) + audience + scope, the confidential
 * `shelf-web` web-broker client with the urn:cocoar:otp grant, the test users, and
 * the realm-level NativeGrants flag plus relaxed OTP rate limits.
 *
 * Idempotent — re-running skips entities that already exist by natural key. The
 * web client secret is FIXED (dev only) so shelf's config stays stable across
 * re-seeds; modgud is told to use it verbatim.
 *
 *   node dev/seed-modgud.mjs
 *
 * Env overrides: BASE_URL (default http://localhost:8092), ADMIN_USER (admin),
 * ADMIN_PASSWORD (Passw0rd!Admin). Bootstrap the admin first (see dev/README.md).
 */

const BASE = (process.env.BASE_URL ?? 'http://localhost:8092').replace(/\/$/, '')
const ADMIN_USER = process.env.ADMIN_USER ?? 'admin'
const ADMIN_PASSWORD = process.env.ADMIN_PASSWORD ?? 'Passw0rd!Admin'

// What shelf expects (matches data/configuration.json: Audience=shelf, WebClientId=shelf-web).
const WEB_CLIENT_ID = 'shelf-web'
const WEB_CLIENT_SECRET = 'shelf-web-dev-secret'      // dev only — committed on purpose
const AUDIENCE = 'shelf'
const SCOPES = ['openid', 'email', 'profile', 'roles', 'permissions', 'shelf']
const GRANTS = ['urn:cocoar:otp', 'refresh_token']
const TEST_USERS = [
  { userName: 'tester', email: 'tester@shelf.local', firstname: 'Test', lastname: 'Tester', acronym: 'TT', password: 'Passw0rd!Test' },
  // Allowlisted admin in data/configuration.json (Modgud.Admins) — log in as this one for the admin UI.
  { userName: 'bwi', email: 'bwi@doob.at', firstname: 'Bernhard', lastname: 'Windisch', acronym: 'BW', password: 'Passw0rd!Test' },
]

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
  if (!login.ok) die('admin login (bootstrap the admin first — see dev/README.md)', login)
  console.log(`✓ logged in as ${ADMIN_USER}`)

  // 2. Realm settings: NativeGrants ON (gates the OTP grant) + relaxed auth rate limits so
  //    iterative local testing doesn't trip the prod-shaped 5-OTP/hour cap. (The live limiter
  //    caches its partition at boot — restart modgud once after the first seed:
  //    docker restart shelf-dev-modgud.)
  const ng = await patch('/api/admin/realm-settings', {
    NativeGrants: { Enabled: true },
    AuthRateLimits: {
      NativeOtp: { PermitLimit: 1000, WindowMinutes: 1 },
    },
  })
  if (!ng.ok) die('enable NativeGrants + relax rate limits', ng)
  console.log('✓ NativeGrants enabled + auth rate limits relaxed (realm)')

  // 3. App `shelf` (+ shelf:admin permission for RBAC).
  const apps = await get('/api/app')
  if (!apps.ok) die('list apps', apps)
  let app = (apps.body ?? []).find(a => a.Slug === AUDIENCE)
  if (app) console.log(`— app '${AUDIENCE}' exists`)
  else {
    const r = await post('/api/app', {
      Slug: AUDIENCE, DisplayName: 'Shelf', Description: 'Cocoar documentation hosting (local dev)',
      Permissions: [{ Resource: 'shelf', Action: 'admin', Description: 'Shelf admin' }],
    })
    if (!r.ok) die('create app', r)
    app = r.body
    console.log(`✓ app '${AUDIENCE}' created`)
  }
  const appId = app.Id

  // 4. Scope `shelf` (app-scoped; Resources binds the token audience to `shelf`).
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

  // 5. OAuthApi `shelf` = the resource server / audience.
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

  // 6. Confidential web-broker client with the urn:cocoar:otp grant.
  const clients = await get('/api/admin/oauth/clients')
  const existingClient = (clients.body?.Items ?? []).find(c => c.ClientId?.toLowerCase() === WEB_CLIENT_ID)
  let effectiveSecret = WEB_CLIENT_SECRET
  if (existingClient) {
    console.log(`— client '${WEB_CLIENT_ID}' exists (secret unchanged; using the configured dev secret)`)
  } else {
    const r = await post('/api/admin/oauth/clients', {
      ClientId: WEB_CLIENT_ID, DisplayName: 'Shelf Web (BFF broker)', ClientType: 'confidential',
      ClientSecret: WEB_CLIENT_SECRET, ConsentType: 'implicit', RedirectUris: [], PostLogoutRedirectUris: [],
      Scopes: SCOPES, AllowedGrantTypes: GRANTS, RequireConsent: false, RequireClientSecret: true,
      AccessTokenType: 'Jwt', Enabled: true, AppIds: [appId],
    })
    if (!r.ok) die('create client', r)
    if (r.body?.ClientSecret) effectiveSecret = r.body.ClientSecret
    console.log(`✓ client '${WEB_CLIENT_ID}' created`)
  }

  // 7. Test users. Native OTP only issues a code to a CONFIRMED user, so create + confirm.
  const users = await get('/api/user')
  const userItems = Array.isArray(users.body) ? users.body : (users.body?.Items ?? [])
  for (const TEST_USER of TEST_USERS) {
    let userRow = userItems.find(u => u.UserName?.toLowerCase() === TEST_USER.userName)
    if (!userRow) {
      const r = await post('/api/user', {
        UserName: TEST_USER.userName, Firstname: TEST_USER.firstname, Lastname: TEST_USER.lastname,
        Acronym: TEST_USER.acronym, Email: TEST_USER.email, Password: TEST_USER.password, EmailConfirmed: true,
      })
      if (!r.ok) die('create user', r)
      userRow = r.body
      console.log(`✓ user '${TEST_USER.userName}' (${TEST_USER.email}) created`)
    } else {
      console.log(`— user '${TEST_USER.userName}' exists`)
    }
    const uid = userRow.Id ?? userRow.id
    const conf = await req('PUT', `/api/user/${uid}`, {
      Firstname: TEST_USER.firstname, Lastname: TEST_USER.lastname, Acronym: TEST_USER.acronym,
      Email: TEST_USER.email, UserName: TEST_USER.userName, EmailConfirmed: true,
    })
    if (!conf.ok) die('confirm user email', conf)
    console.log(`✓ user '${TEST_USER.userName}' email confirmed`)
  }

  console.log('\n── shelf local config (env for the backend) ──')
  console.log(`Shelf__Modgud__Issuer=${BASE.replace('localhost', '127.0.0.1')}`)
  console.log(`Shelf__Modgud__Audience=${AUDIENCE}`)
  console.log(`Shelf__Modgud__WebClientId=${WEB_CLIENT_ID}`)
  console.log(`Shelf__Modgud__WebClientSecret=${effectiveSecret}`)
  console.log(`\nTest logins: ${TEST_USERS.map(u => u.email).join(', ')}  (OTP codes land in Mailpit → http://localhost:8025)`)
}

main().catch(e => { console.error(e); process.exit(1) })

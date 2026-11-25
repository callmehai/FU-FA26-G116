// The QA toolkit a plan's criteria run with. One criterion gets a fresh browser context: its own cookies, its own
// role, its own outside-request guard, so a criterion never passes because an earlier one left a session behind.
import { expect, request as playwrightRequest } from '@playwright/test'
import { mkdirSync } from 'node:fs'
import { join } from 'node:path'

/** The development seed accounts (Development only; the password is the documented dev seed, not a secret). */
export const ACCOUNTS = {
  admin: 'quantri@trustcabinet.local',
  chair: 'chutoa@trustcabinet.local',
  secretary: 'thuky@trustcabinet.local',
  member: 'thanhvien@trustcabinet.local',
  // Not a seed account: ensureNoCreator() makes it, with a role that holds no capability. Since ADR-0075 the seed
  // "Thành viên" may create meetings, so a criterion about someone who may not signs in as this one (role: 'nocreator').
  nocreator: 'qa-khong-tao-hop@trustcabinet.local',
}
const NO_CREATOR = { fullName: '[QA] Người không tạo họp', roleName: '[QA] Không tạo cuộc họp' }
const PASSWORD = process.env.QA_PASSWORD ?? 'TrustCabinet@2026'

/** The two screens every UI criterion can run on (check-responsive: phone at 390px with touch, desktop at 1440px). */
export const DEVICES = {
  desktop: { viewport: { width: 1440, height: 900 }, deviceScaleFactor: 2 },
  phone: { viewport: { width: 390, height: 844 }, deviceScaleFactor: 2, isMobile: true, hasTouch: true },
}

/** Hosts the app may reach besides its own origin: Google Fonts and Google Sign-In, as the server's CSP allows. */
const DEFAULT_ALLOWED_HOSTS = ['fonts.googleapis.com', 'fonts.gstatic.com', 'accounts.google.com']

/** Thrown by `qa.inconclusive()`: the criterion could not be judged here (needs a real mic, a second machine…). */
export class Inconclusive extends Error {}

const emailOf = (role) => ACCOUNTS[role] ?? role
const slugify = (text) =>
  String(text)
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .replace(/đ/g, 'd')
    .replace(/Đ/g, 'D')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-|-$/g, '')
    .slice(0, 48)

/** Signs in through the API: the refresh cookie lands in `context`, the access token is returned for API calls. */
async function login(api, base, role) {
  const res = await api.post(`${base}/api/auth/login`, { data: { email: emailOf(role), password: PASSWORD } })
  if (!res.ok()) throw new Error(`sign-in as ${role} failed: HTTP ${res.status()}`)
  return (await res.json()).accessToken
}

/** An API client for one role (or `anon`), separate from the browser so it never changes the page's session. */
async function apiClient(base, role) {
  const ctx = await playwrightRequest.newContext({ baseURL: base })
  const token = role === 'anon' ? null : await login(ctx, base, role)
  const call = async (method, path, body, headers = {}) => {
    const res = await ctx.fetch(`${base}${path}`, {
      method,
      headers: { ...(token ? { Authorization: `Bearer ${token}` } : {}), ...headers },
      ...(body === undefined ? {} : { data: body }),
    })
    const text = await res.text()
    let json
    try {
      json = text ? JSON.parse(text) : undefined
    } catch {
      json = undefined
    }
    return { status: res.status(), body: json ?? text, headers: res.headers() }
  }
  return {
    get: (path) => call('GET', path),
    post: (path, body, headers) => call('POST', path, body ?? {}, headers),
    put: (path, body, headers) => call('PUT', path, body ?? {}, headers),
    patch: (path, body, headers) => call('PATCH', path, body ?? {}, headers),
    delete: (path, headers) => call('DELETE', path, undefined, headers),
    dispose: () => ctx.dispose(),
  }
}

/**
 * Makes the `nocreator` account ready, as quantri@ through the API, and returns `{ id, email, fullName, roleId }`.
 * Idempotent, so a rerun reuses both: the role "[QA] Không tạo cuộc họp" is created when missing and stripped back to
 * no capability when someone gave it one; the account is created when missing, put back on that role and reactivated.
 * Neither is deleted afterwards: an account is only ever deactivated, and a role cannot go while an account holds it.
 * A criterion names it as its `prepare`, which the runner calls before signing in as the criterion's role.
 */
export async function ensureNoCreator({ base }) {
  const admin = await apiClient(base, 'admin')
  const must = (res, what, ok) => {
    if (res.status !== ok) throw new Error(`${what}: HTTP ${res.status} ${JSON.stringify(res.body).slice(0, 200)}`)
    return res.body
  }
  try {
    let role = must(await admin.get('/api/roles'), 'GET /api/roles', 200).find((r) => r.name === NO_CREATOR.roleName)
    if (!role) {
      const body = {
        name: NO_CREATOR.roleName,
        description: 'Kiểm thử QA: không có quyền nào, như vai trò Thành viên trước ADR-0075.',
        capabilities: [],
      }
      role = must(await admin.post('/api/roles', body), 'POST /api/roles', 201).role
    }
    const matrix = must(await admin.get('/api/roles/capabilities'), 'GET /api/roles/capabilities', 200)
    if (matrix.roles.find((r) => r.id === role.id)?.capabilities?.length)
      must(await admin.put('/api/roles/capabilities', { roleId: role.id, capabilities: [] }), 'PUT /api/roles/capabilities', 200)
    const email = ACCOUNTS.nocreator
    let user = must(await admin.get('/api/users'), 'GET /api/users', 200).find((u) => u.email.toLowerCase() === email)
    if (!user) {
      const body = { fullName: NO_CREATOR.fullName, email, password: PASSWORD, roleId: role.id }
      user = must(await admin.post('/api/users', body), 'POST /api/users', 201).user
    } else {
      if (user.roleId !== role.id)
        must(await admin.put(`/api/users/${user.id}`, { fullName: user.fullName, email, roleId: role.id }), 'PUT /api/users/{id}', 200)
      if (!user.isActive) must(await admin.put(`/api/users/${user.id}/status`, { isActive: true }), 'PUT /api/users/{id}/status', 200)
    }
    return { id: user.id, email, fullName: user.fullName, roleId: role.id }
  } finally {
    await admin.dispose()
  }
}

/**
 * Aborts every request that leaves the app's origin (and the allowed hosts) and returns the list of them: the test
 * server's Content-Security-Policy would block the same requests, so a page that tries one works locally only.
 */
async function guardOutsideRequests(context, base, allowHosts) {
  const origin = new URL(base).origin
  const allowed = new Set([...DEFAULT_ALLOWED_HOSTS, ...allowHosts])
  const outside = []
  const isOutside = (url) => {
    const u = new URL(url)
    if (u.protocol === 'data:' || u.protocol === 'blob:') return false
    const httpOrigin = u.origin.replace(/^ws(s?):/, 'http$1:')
    return httpOrigin !== origin && !allowed.has(u.hostname)
  }
  await context.route(
    (url) => isOutside(url.href),
    async (route) => {
      // Google Sign-In's own iframe loads its files from Google's hosts: the app's CSP does not apply inside it.
      let frameUrl = ''
      try {
        frameUrl = route.request().frame().url()
      } catch {
        frameUrl = ''
      }
      if (frameUrl.startsWith('http') && new URL(frameUrl).origin !== origin) return route.continue()
      outside.push(`${route.request().method()} ${route.request().url()}`)
      await route.abort('blockedbyclient')
    },
  )
  return outside
}

/**
 * Builds the `qa` object a criterion's `run(qa)` receives, on a new context. `close()` returns what the guard and the
 * page saw, so the runner can fail a criterion whose page called outside or threw.
 */
export async function openCriterion({ browser, base, outDir, criterion, device, counter, allowHosts }) {
  const context = await browser.newContext({
    ...DEVICES[device],
    baseURL: base,
    locale: 'vi-VN',
    timezoneId: 'Asia/Ho_Chi_Minh',
    permissions: ['microphone', 'camera'],
  })
  const outside = await guardOutsideRequests(context, base, allowHosts)
  const page = await context.newPage()
  const pageErrors = []
  page.on('pageerror', (error) => pageErrors.push(error.message))
  const shots = []
  const notes = []
  const apis = []
  let role = criterion.role ?? 'anon'
  mkdirSync(outDir, { recursive: true })

  const qa = {
    page,
    context,
    base,
    device,
    expect,
    get role() {
      return role
    },
    /** Signs this context in as `role` (admin, chair, secretary, member, or an email); `anon` signs out. */
    async signIn(next) {
      role = next
      await context.clearCookies()
      if (next === 'anon') {
        await page.evaluate(() => localStorage.clear()).catch(() => undefined)
        return
      }
      await login(context.request, base, next)
      // The SPA restores a session from its refresh cookie only when this marker says one exists.
      await context.addInitScript(() => localStorage.setItem('trustcabinet.signedIn', '1'))
    },
    /** Opens a path of the app and waits until the network settles (or 10 s). */
    async goto(path) {
      await page.goto(path)
      await page.waitForLoadState('networkidle', { timeout: 10_000 }).catch(() => undefined)
    },
    /** An API client signed in as `who` (default: the criterion's role). `anon` sends no token. */
    async api(who = role) {
      const client = await apiClient(base, who)
      apis.push(client)
      return client
    },
    /**
     * What `who` may do here: `{ roleName, capabilities }` from /api/auth/me. A local database whose roles were edited
     * by hand can give a seed account more than its name says, so check before calling a denial a pass or a defect.
     */
    async capabilities(who = role) {
      const res = await (await qa.api(who)).get('/api/auth/me')
      if (res.status !== 200) throw new Error(`/api/auth/me as ${who}: HTTP ${res.status}`)
      return { roleName: res.body.roleName, capabilities: res.body.capabilities ?? [] }
    },
    /** Asserts an API response's status, showing the body when it differs. */
    expectStatus(res, status) {
      expect(res.status, `HTTP ${res.status}: ${JSON.stringify(res.body).slice(0, 300)}`).toBe(status)
    },
    /** Saves a screenshot as evidence: `NN-<role>-<AC>-<device>-<slug>.png`, numbered in the order taken. */
    async shot(slug, { fullPage = false } = {}) {
      counter.n += 1
      const name = `${String(counter.n).padStart(2, '0')}-${slugify(role)}-${criterion.id}-${device}-${slugify(slug)}.png`
      // Transitions are finished first: a shot taken mid-fade shows controls half-drawn.
      await page.screenshot({ path: join(outDir, name), fullPage, animations: 'disabled', caret: 'hide' })
      shots.push(name)
      return name
    },
    /** `[QA] <text>`: every record a plan creates is named so, to find and delete it later. */
    name: (text) => `[QA] ${text}`,
    /** Something seen worth reporting, even when the criterion passes. */
    note: (text) => notes.push(String(text)),
    /** Stops the criterion as not judgeable here; the report asks a human for it. */
    inconclusive(reason) {
      throw new Inconclusive(reason)
    },
  }

  if (role !== 'anon') await qa.signIn(role)

  return {
    qa,
    async close() {
      for (const client of apis) await client.dispose()
      await context.close()
      return { shots, notes, outside, pageErrors }
    },
  }
}

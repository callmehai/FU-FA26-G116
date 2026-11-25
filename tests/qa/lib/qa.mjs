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
}
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
  const call = async (method, path, body) => {
    const res = await ctx.fetch(`${base}${path}`, {
      method,
      headers: token ? { Authorization: `Bearer ${token}` } : {},
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
    post: (path, body) => call('POST', path, body ?? {}),
    put: (path, body) => call('PUT', path, body ?? {}),
    patch: (path, body) => call('PATCH', path, body ?? {}),
    delete: (path) => call('DELETE', path),
    dispose: () => ctx.dispose(),
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
    async shot(slug, { fullPage = false, page: target = page, who = role } = {}) {
      counter.n += 1
      const name = `${String(counter.n).padStart(2, '0')}-${slugify(who)}-${criterion.id}-${device}-${slugify(slug)}.png`
      // Transitions are finished first: a shot taken mid-fade shows controls half-drawn.
      // `page` lets a criterion that drives a second browser (another user) keep that browser's evidence too.
      await target.screenshot({ path: join(outDir, name), fullPage, animations: 'disabled', caret: 'hide' })
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

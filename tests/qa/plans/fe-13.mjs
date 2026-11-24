// FE-13 — Email & Thông báo trong ứng dụng (QA subtask SCRUM-200): UC-76 view notifications, UC-77a mark one read,
// UC-77b mark all read, UC-78 open the target, and the rules they cite (BR-033, BR-034); which events notify whom, as the
// UCs and BRs of the producing features say (UC-17, UC-18 POST-5/6, BR-031, BR-032 publish, UC-14 reschedule, BR-031, BR-032, UC-30b, UC-30c roster,
// UC-20 start, UC-22 cancel, UC-76 reminder, UC-23a, UC-23b presenter, BR-035 not about your own action, UC-72, UC-73, UC-74 minutes,
// BR-009–BR-011 calendar sharing); and which events send email (BR-031, BR-032, UC-22, UC-23a, UC-23b, BR-035).
//
// Criteria come from docs/01-requirements/05-use-case-specifications.md (FE-13), 06-business-rules.md,
// docs/03-api/06-thong-bao-trong-ung-dung.md, 07-gui-email-thu-moi.md, 23-chia-se-lich.md and the message catalogue.
//
// Safe on a shared local dev database:
// - the seed accounts' notifications are only read here (opening the panel does not mark anything read, UC-76); every
//   read-state change happens on three throwaway accounts this plan creates, role Thành viên, on @trustcabinet.local:
//   qa-fe13-a@ (receives the meeting notifications), qa-fe13-b@ (mark all read), qa-fe13-empty@ (never notified);
// - every meeting is "[QA] FE-13 …", ten weeks ahead, organised by thuky@ with chutoa@ as Chair and approver. Two are
//   reused across runs (one Published meeting that is rescheduled to produce fresh notifications, one whose roster
//   qa-fe13-a@ joins and leaves); a criterion whose event is one-way (approve, publish, start, cancel, minutes) creates
//   its own.
// AC20–AC33 (Round 1, 2026-11-22) add: the 99+ badge and row times (thuky@ is read only), paging, list-load errors by
// route interception, Escape/outside click, phone layout, signed-out bell, a failed single mark, per-account mark all,
// two tabs, same-page open, a failed read on open, hub duplicate/reconnect through a WebSocket proxy, a role change
// mid-session (qa-fe13-b@, restored) and the late-invitee reminder (bell + read-only look at the mail queue in Postgres).
// Email: no endpoint exposes the outbox (email_messages / meeting_invitation_dispatches), so delivery is INCONCLUSIVE
// with the manual step written in the criterion.

import { expect } from '@playwright/test'
import { execFileSync } from 'node:child_process'

const PASSWORD = process.env.QA_PASSWORD ?? 'TrustCabinet@2026'
const SEED = {
  admin: 'quantri@trustcabinet.local',
  chair: 'chutoa@trustcabinet.local',
  secretary: 'thuky@trustcabinet.local',
  member: 'thanhvien@trustcabinet.local',
}
const QA = {
  a: { email: 'qa-fe13-a@trustcabinet.local', fullName: '[QA] FE-13 Người nhận' },
  b: { email: 'qa-fe13-b@trustcabinet.local', fullName: '[QA] FE-13 Đọc hết' },
  empty: { email: 'qa-fe13-empty@trustcabinet.local', fullName: '[QA] FE-13 Chưa có thông báo' },
}
const emailOf = (who) => SEED[who] ?? QA[who]?.email ?? who

// ---------------------------------------------------------------------------------------------------------------------
// API: a fetch client per account (it can send If-Match, which the runner's client cannot), tokens cached 5 minutes.

const args = process.argv.slice(2)
const BASE = (() => {
  const i = args.indexOf('--base')
  return (i >= 0 ? args[i + 1] : (process.env.QA_BASE ?? 'http://localhost:5173')).replace(/\/$/, '')
})()

const TOKENS = {}
async function token(who) {
  const email = emailOf(who)
  const cached = TOKENS[email]
  if (cached && Date.now() - cached.at < 5 * 60_000) return cached.token
  const res = await fetch(`${BASE}/api/auth/login`, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({ email, password: PASSWORD }),
  })
  if (!res.ok) throw new Error(`sign-in as ${email}: HTTP ${res.status}`)
  TOKENS[email] = { token: (await res.json()).accessToken, at: Date.now() }
  return TOKENS[email].token
}

/** `await call('secretary', 'POST', path, body, { 'If-Match': etag })` → { status, body, headers }. `anon` sends no token. */
async function call(who, method, path, body, headers = {}) {
  const auth = who === 'anon' ? {} : { Authorization: `Bearer ${await token(who)}` }
  const res = await fetch(`${BASE}${path}`, {
    method,
    headers: { ...auth, ...(body === undefined ? {} : { 'content-type': 'application/json' }), ...headers },
    body: body === undefined ? undefined : JSON.stringify(body),
  })
  const text = await res.text()
  let json
  try {
    json = text ? JSON.parse(text) : undefined
  } catch {
    json = undefined
  }
  return { status: res.status, body: json ?? text, headers: Object.fromEntries(res.headers.entries()) }
}
const get = (who, path) => call(who, 'GET', path)
const post = (who, path, body = {}, headers) => call(who, 'POST', path, body, headers)
const put = (who, path, body = {}, headers) => call(who, 'PUT', path, body, headers)
const del = (who, path, headers) => call(who, 'DELETE', path, undefined, headers)

function ok(qa, res, status, label = '') {
  qa.expect(res.status, `${label} HTTP ${res.status}: ${JSON.stringify(res.body).slice(0, 300)}`).toBe(status)
  return res.body
}

/** id, name of each account, read once from /api/auth/me. */
const IDS = {}
async function idOf(who) {
  const email = emailOf(who)
  if (!IDS[email]) {
    const me = await get(who, '/api/auth/me')
    if (me.status !== 200) throw new Error(`/api/auth/me as ${email}: HTTP ${me.status}`)
    IDS[email] = me.body.id
  }
  return IDS[email]
}

/**
 * Creates (or re-activates) the three QA accounts, role Thành viên, before any criterion signs in as one. Runs when the
 * runner imports the plan; never touches any other account. A failure here shows up as "could not open" on the
 * criteria that sign in as a QA account.
 */
async function ensureQaAccounts() {
  const roles = await get('admin', '/api/roles')
  const member = roles.body.find((r) => r.isBuiltIn && r.isDefault)
  const users = await get('admin', '/api/users')
  for (const u of Object.values(QA)) {
    const found = users.body.find((x) => x.email === u.email)
    if (!found) {
      const res = await post('admin', '/api/users', { fullName: u.fullName, email: u.email, password: PASSWORD, roleId: member.id })
      if (res.status !== 201) throw new Error(`create ${u.email}: HTTP ${res.status} ${JSON.stringify(res.body)}`)
    } else if (!found.isActive) {
      await put('admin', `/api/users/${found.id}/status`, { isActive: true })
    }
  }
}
try {
  const health = await fetch(`${BASE}/api/health`).then((r) => r.json())
  if (health.service === 'TrustCabinet.Web') await ensureQaAccounts()
} catch (error) {
  console.error(`fe-13: could not prepare the QA accounts: ${error.message}`)
}

// ---------------------------------------------------------------------------------------------------------------------
// Notifications through the API.

async function unread(who) {
  const res = await get(who, '/api/notifications/unread-count')
  if (res.status !== 200) throw new Error(`unread-count as ${who}: HTTP ${res.status}`)
  return res.body.unreadCount
}
async function latest(who, pageSize = 100) {
  const res = await get(who, `/api/notifications?page=1&pageSize=${pageSize}`)
  if (res.status !== 200) throw new Error(`notifications as ${who}: HTTP ${res.status}`)
  return res.body.items
}
/** The ids on `who`'s first page, to tell the notifications an action created from the ones before it. */
async function seen(who) {
  return new Set((await latest(who)).map((n) => n.id))
}
/** The notifications `who` received since `before` (a set from `seen`), optionally of one type / meeting. */
async function fresh(who, before, { type, meetingId, targetId } = {}) {
  return (await latest(who)).filter(
    (n) =>
      !before.has(n.id) &&
      (type === undefined || n.type === type) &&
      (meetingId === undefined || n.meetingId === meetingId) &&
      (targetId === undefined || n.targetId === targetId),
  )
}

// ---------------------------------------------------------------------------------------------------------------------
// Meetings.

const rand = () => Math.random().toString(36).slice(2, 6)
const iso = (ms) => new Date(ms).toISOString().replace(/\.\d{3}Z$/, 'Z')
const MINUTE = 60_000
const DAY = 86_400_000
let slotCounter = Math.floor(Math.random() * 11)
/** A slot on the day ten weeks from today, between 08:00 and 17:30 Vietnam time, varied per call. */
function slot(minutes = 45) {
  slotCounter += 1
  const day = new Date(Date.now() + 70 * DAY)
  day.setUTCHours(1, 0, 0, 0) // 08:00 in Vietnam
  const start = day.getTime() + ((slotCounter * 55) % 540) * MINUTE + Math.floor(Math.random() * 6) * 5 * MINUTE
  return { plannedStartTime: iso(start), plannedEndTime: iso(start + minutes * MINUTE) }
}

async function roster({ withA = true } = {}) {
  return [
    { userId: await idOf('chair'), role: 'Chair' },
    { userId: await idOf('secretary'), role: 'Secretary' },
    ...(withA ? [{ userId: await idOf('a'), role: 'Member' }] : []),
  ]
}

async function transition(qa, who, id, action, extra = {}) {
  return ok(qa, await post(who, `/api/meetings/${id}/transitions`, { action, ...extra }), 200, action)
}
const STEPS = {
  Draft: [],
  WaitingApproval: ['submit'],
  Approved: ['submit', 'approve'],
  Published: ['submit', 'approve', 'publish'],
  Ended: ['submit', 'approve', 'publish', 'start', 'end'],
}
async function step(qa, id, name) {
  const chairId = await idOf('chair')
  const [who, action, extra] = {
    submit: ['secretary', 'SubmitForApproval', { assignedApproverId: chairId }],
    approve: ['chair', 'Approve'],
    reject: ['chair', 'Reject', { reason: 'QA FE-13: bổ sung tài liệu.' }],
    publish: ['secretary', 'Publish'],
    start: ['chair', 'Start'],
    end: ['chair', 'End'],
  }[name]
  await transition(qa, who, id, action, extra)
}

/** Creates "[QA] FE-13 <label> <rand>" organised by thuky@ and drives it to `status`. */
async function createAt(qa, status, label, { withA = true } = {}) {
  const res = await post('secretary', '/api/meetings', {
    title: qa.name(`FE-13 ${label} ${rand()}`),
    ...slot(),
    mode: 'InPerson',
    location: 'Phòng họp QA',
    onlineLink: null,
    sensitivity: 'Normal',
    description: 'Cuộc họp do bộ kiểm thử QA FE-13 tạo.',
    participants: await roster({ withA }),
  })
  const id = ok(qa, res, 201, 'create').meeting.id
  for (const s of STEPS[status]) await step(qa, id, s)
  const m = ok(qa, await get('secretary', `/api/meetings/${id}`), 200)
  qa.expect(m.status).toBe(status)
  qa.note(`Tạo: ${m.title} (${status})`)
  return m
}

/** The Published meeting reused across runs, found by its fixed title; created the first time. */
const REUSED = {}
async function reused(qa, key, { withA }) {
  if (REUSED[key]) return REUSED[key]
  const title = qa.name(`FE-13 dùng lại ${key}`)
  const list = await get('secretary', `/api/meetings?q=${encodeURIComponent(title)}&status=Published&pageSize=20`)
  const hit = (list.body.items ?? []).find((m) => m.title === title)
  let m
  if (hit) m = ok(qa, await get('secretary', `/api/meetings/${hit.id}`), 200)
  else {
    const res = await post('secretary', '/api/meetings', {
      title,
      ...slot(),
      mode: 'InPerson',
      location: 'Phòng họp QA',
      onlineLink: null,
      sensitivity: 'Normal',
      description: 'Cuộc họp do bộ kiểm thử QA FE-13 tạo, dùng lại giữa các lần chạy.',
      participants: await roster({ withA }),
    })
    const id = ok(qa, res, 201, 'create reused').meeting.id
    for (const s of STEPS.Published) await step(qa, id, s)
    m = ok(qa, await get('secretary', `/api/meetings/${id}`), 200)
  }
  REUSED[key] = m
  return m
}
/** "[QA] FE-13 dùng lại Đã công bố": chutoa@ Chair, thuky@ Secretary, qa-fe13-a@ Member. */
const mainMeeting = (qa) => reused(qa, 'Đã công bố', { withA: true })
/** "[QA] FE-13 dùng lại thành phần": qa-fe13-a@ joins and leaves it in each run. */
const rosterMeeting = (qa) => reused(qa, 'thành phần', { withA: false })

/** thuky@ moves the meeting to another slot ten weeks ahead (UC-14 AF-1): every other invitee gets MeetingRescheduled. */
async function reschedule(qa, m) {
  const current = ok(qa, await get('secretary', `/api/meetings/${m.id}`), 200)
  let next = slot()
  if (next.plannedStartTime === current.plannedStartTime) next = slot()
  const res = await put('secretary', `/api/meetings/${m.id}`, {
    title: current.title,
    ...next,
    mode: current.mode,
    location: current.location,
    onlineLink: current.onlineLink,
    sensitivity: current.sensitivity,
    description: current.description,
  })
  ok(qa, res, 200, 'reschedule')
  return res.body.meeting ?? current
}

/** Reschedules the main meeting and returns qa-fe13-a@'s new MeetingRescheduled notification. */
async function freshForA(qa) {
  const m = await mainMeeting(qa)
  const before = await seen('a')
  await reschedule(qa, m)
  const got = await fresh('a', before, { type: 'MeetingRescheduled', meetingId: m.id })
  qa.expect(got.length, 'qa-fe13-a@ received one MeetingRescheduled').toBe(1)
  return { m, n: got[0] }
}

// Roster changes need If-Match (the participants revision).
async function participants(m) {
  const res = await get('secretary', `/api/meetings/${m.id}/participants?pageSize=100`)
  return { items: res.body.items, etag: res.headers.etag ?? `"participants-${res.body.revision}"` }
}
async function removeA(qa, m) {
  const { items, etag } = await participants(m)
  const row = items.find((p) => p.userId === IDS[QA.a.email])
  if (!row) return false
  ok(qa, await del('secretary', `/api/meetings/${m.id}/participants/${row.id}`, { 'If-Match': etag }), 200, 'remove qa-fe13-a@')
  return true
}
async function addA(qa, m) {
  const { etag } = await participants(m)
  const res = await post('secretary', `/api/meetings/${m.id}/participants`, { userIds: [await idOf('a')], role: 'Member' }, { 'If-Match': etag })
  ok(qa, res, 201, 'add qa-fe13-a@')
}
/** qa-fe13-a@ joins the roster meeting, then is removed: returns the invitation and the withdrawal notifications. */
async function inviteThenRemove(qa) {
  const m = await rosterMeeting(qa)
  await idOf('a')
  await removeA(qa, m)
  let before = await seen('a')
  await addA(qa, m)
  const invited = await fresh('a', before, { meetingId: m.id })
  before = await seen('a')
  await removeA(qa, m)
  const removed = await fresh('a', before)
  return { m, invited, removed }
}

// Calendar sharing (BR-009–BR-011): thuky@ asks to see an account's calendar.
const todayVn = (plus = 0) => new Date(Date.now() + 7 * 3_600_000 + plus * DAY).toISOString().slice(0, 10)
async function requestShare(qa, ownerWho) {
  const ownerId = await idOf(ownerWho)
  const mine = await get('secretary', '/api/calendar/shares')
  for (const s of mine.body.asViewer ?? []) {
    if (s.owner.id === ownerId && s.actions.includes('withdraw')) await post('secretary', `/api/calendar/shares/${s.id}/withdraw`)
    else if (s.owner.id === ownerId && s.actions.includes('end')) await post('secretary', `/api/calendar/shares/${s.id}/end`)
  }
  const res = await post('secretary', '/api/calendar/shares', { ownerId, level: 'Level1', startDate: todayVn(), endDate: todayVn(7) })
  return ok(qa, res, 201, 'request share').share
}

/** Runs every check, then fails once with all the wrong ones. */
function checker(qa) {
  const wrong = []
  return {
    check(label, res, status, code) {
      const good = res.status === status && (code === undefined || res.body?.code === code)
      if (!good) wrong.push(`${label}: want ${status}${code ? ` ${code}` : ''}, got ${res.status} ${JSON.stringify(res.body).slice(0, 220)}`)
      return good
    },
    fact(label, good, detail = '') {
      if (!good) wrong.push(`${label}${detail ? `: ${detail}` : ''}`)
      return good
    },
    done() {
      qa.expect(wrong, wrong.join('\n')).toEqual([])
    },
  }
}

// ---------------------------------------------------------------------------------------------------------------------
// Screen.

const bell = (qa) => qa.page.locator('button.notif-bell')
const panel = (qa) => qa.page.locator('section.notif-panel')
const rows = (qa) => panel(qa).locator('li.notif-item')
const toast = (qa, text) => qa.page.locator('.toast-stack').getByText(text)
const bellLabel = (n) => (n > 0 ? `Thông báo, ${n} chưa đọc` : 'Thông báo')

async function press(qa, locator) {
  if (qa.device === 'phone') await locator.tap()
  else await locator.click()
}
async function noSidewaysScroll(qa) {
  const sideways = await qa.page.evaluate(() => document.documentElement.scrollWidth - innerWidth)
  qa.expect(sideways, 'the page scrolls sideways').toBeLessThanOrEqual(0)
}
/** The panel lies inside the screen (on a phone it spans it). */
async function panelFits(qa) {
  const box = await panel(qa).boundingBox()
  const width = qa.page.viewportSize().width
  qa.expect(box.x, 'panel starts off screen').toBeGreaterThanOrEqual(0)
  qa.expect(box.x + box.width, 'panel ends off screen').toBeLessThanOrEqual(width + 0.5)
}
/** Opens a page and waits until the bell shows. */
async function home(qa, path = '/mgmt/meetings') {
  await qa.goto(path)
  await qa.expect(bell(qa)).toBeVisible()
}
async function openPanel(qa) {
  await press(qa, bell(qa))
  await qa.expect(panel(qa)).toBeVisible()
  await qa.expect(rows(qa).first().or(panel(qa).getByText('Chưa có thông báo nào'))).toBeVisible()
  await qa.expect(panel(qa).locator('.notif-skeleton')).toHaveCount(0)
}

/** A copy of `qa` aimed at another tab of the same browser context (the helpers above only read qa.page and qa.device). */
const tabOf = (qa, page) => ({ ...qa, page })

const JSON_500 = { status: 500, contentType: 'application/json', body: JSON.stringify({ code: 'MSG40', message: 'Không thể tải thông báo. Vui lòng thử lại.' }) }
/** Makes GET /api/notifications (the list, not the count) fail like the backend's own MSG40; returns a function that lifts it. */
async function failList(qa) {
  const handler = (route) => (route.request().method() === 'GET' ? route.fulfill(JSON_500) : route.fallback())
  const match = (url) => url.pathname === '/api/notifications'
  await qa.page.route(match, handler)
  return () => qa.page.unroute(match, handler)
}

/**
 * Puts a proxy in front of the notification hub's WebSocket so a criterion can drop the connection, keep it down
 * (a closed socket for every reconnect attempt) and push each message to the page twice. Install before the page opens.
 */
async function hubProxy(qa, { twice = false } = {}) {
  const state = { blocked: false, sockets: [], opened: 0 }
  // SignalR falls back to long polling when the socket fails, so the negotiate / polling requests are held down too.
  await qa.page.route(
    (u) => u.pathname.startsWith('/hubs/notifications'),
    (route) => (state.blocked ? route.abort('connectionrefused') : route.fallback()),
  )
  await qa.page.routeWebSocket(/\/hubs\/notifications/, (ws) => {
    if (state.blocked) {
      ws.close()
      return
    }
    state.opened += 1
    const server = ws.connectToServer()
    state.sockets.push(ws)
    ws.onMessage((m) => server.send(m))
    server.onMessage((m) => {
      ws.send(m)
      if (twice && typeof m === 'string' && m.includes('"target":"notificationReceived"')) ws.send(m)
    })
    ws.onClose((code, reason) => server.close({ code, reason }).catch(() => undefined))
    server.onClose((code, reason) => ws.close({ code, reason }).catch(() => undefined))
  })
  return {
    state,
    drop() {
      state.blocked = true
      for (const s of state.sockets) s.close({ code: 1011 }).catch(() => undefined)
      state.sockets = []
    },
    restore() {
      state.blocked = false
    },
  }
}
/** Rows as "title | body" in on-screen order, to compare the panel with what the API returns. */
async function panelTexts(qa) {
  return rows(qa).evaluateAll((els) =>
    els.map((el) => `${el.querySelector('.notif-title')?.textContent?.replace('Chưa đọc: ', '')} | ${el.querySelector('.notif-text')?.textContent ?? ''}`),
  )
}
const apiTexts = (items) => items.map((n) => `${n.title} | ${n.body ?? ''}`)
/** Gives `who` at least one unread notification (a calendar-share request thuky@ then withdraws). */
async function ensureUnread(qa, who) {
  if ((await unread(who)) > 0) return
  const share = await requestShare(qa, who)
  await post('secretary', `/api/calendar/shares/${share.id}/withdraw`)
}

// ---------------------------------------------------------------------------------------------------------------------

export default {
  ticket: 'SCRUM-200',
  title: 'FE-13 Email & Thông báo trong ứng dụng: chuông, danh sách, đánh dấu đã đọc, mở đích, realtime, ai được báo, email',
  devices: ['desktop'],
  criteria: [
    {
      id: 'AC1',
      role: QA.a.email,
      devices: ['desktop', 'phone'],
      title: 'Chuông hiện số chưa đọc; mở danh sách thấy thông báo mới nhất ở đầu, đánh dấu chưa đọc, và không làm đổi trạng thái đọc',
      steps: [
        'thuky@ dời lịch cuộc họp "[QA] FE-13 dùng lại Đã công bố" mà qa-fe13-a@ là Thành viên',
        'qa-fe13-a@ mở /mgmt/meetings, nhìn chuông',
        'Bấm chuông',
        'Đọc lại số chưa đọc qua API',
      ],
      expected:
        'Badge chuông = số chưa đọc của API (99+ khi > 99), nhãn "Thông báo, N chưa đọc"; panel "Thông báo" có chip "N mới", dòng đầu là "Cuộc họp đã dời lịch" của cuộc họp đó, tô chưa đọc; mở panel không đổi số chưa đọc và dòng đó vẫn chưa đọc (UC-76, UC-77b, UC-78); điện thoại: panel nằm gọn màn hình, không cuộn ngang',
      async run(qa) {
        const { m, n } = await freshForA(qa)
        const before = await unread('a')
        await home(qa)
        await qa.expect(bell(qa)).toHaveAttribute('aria-label', bellLabel(before))
        await qa.expect(bell(qa).locator('.notif-badge')).toHaveText(before > 99 ? '99+' : String(before))
        await qa.shot('chuong-co-so-chua-doc')
        await openPanel(qa)
        await qa.expect(panel(qa).locator('h2 .badge')).toHaveText(`${before > 99 ? '99+' : before} mới`)
        const first = rows(qa).first()
        await qa.expect(first).toContainText('Cuộc họp đã dời lịch')
        await qa.expect(first).toContainText(m.title)
        await qa.expect(first).toHaveClass(/notif-item-unread/)
        await panelFits(qa)
        await noSidewaysScroll(qa)
        await qa.shot('panel-dong-dau-chua-doc')
        qa.expect(await unread('a'), 'opening the list changed the unread count (UC-76)').toBe(before)
        const again = (await latest('a', 20)).find((x) => x.id === n.id)
        qa.expect(again?.isRead, 'opening the list marked the notification read (UC-76)').toBe(false)
      },
    },
    {
      id: 'AC2',
      role: QA.a.email,
      devices: ['desktop', 'phone'],
      title: 'Đánh dấu một thông báo đã đọc: dòng hết tô, số chưa đọc giảm 1; đánh dấu lại không giảm nữa',
      steps: [
        'thuky@ dời lịch cuộc họp dùng lại, qa-fe13-a@ có một thông báo mới',
        'qa-fe13-a@ mở chuông, bấm nút ✓ "Đánh dấu đã đọc" trên dòng đầu',
        'Gọi lại POST /api/notifications/{id}/read',
      ],
      expected:
        'Dòng hết tô chưa đọc và mất nút ✓; chip "N mới" và nhãn chuông giảm đúng 1; API: isRead = true, readAt có; gọi lại giữ readAt cũ và số chưa đọc không giảm thêm (UC-77a, UC-78); điện thoại: nút ✓ luôn hiện, không cần rê chuột',
      async run(qa) {
        const { m, n } = await freshForA(qa)
        const before = await unread('a')
        await home(qa)
        await openPanel(qa)
        const first = rows(qa).first()
        await qa.expect(first).toContainText(m.title)
        const mark = first.getByRole('button', { name: /^Đánh dấu đã đọc/ })
        await qa.expect(mark).toBeVisible()
        if (qa.device === 'phone') {
          const opacity = await mark.evaluate((el) => getComputedStyle(el).opacity)
          qa.expect(opacity, 'the ✓ button is hidden until hover on a touch screen').toBe('1')
        }
        await qa.shot('truoc-danh-dau')
        await press(qa, mark)
        await qa.expect(first).not.toHaveClass(/notif-item-unread/)
        await qa.expect(first.getByRole('button', { name: /^Đánh dấu đã đọc/ })).toHaveCount(0)
        await qa.expect(bell(qa)).toHaveAttribute('aria-label', bellLabel(before - 1))
        await qa.shot('sau-danh-dau')
        const read = (await latest('a', 50)).find((x) => x.id === n.id)
        qa.expect(read?.isRead).toBe(true)
        qa.expect(await unread('a')).toBe(before - 1)
        const again = await post('a', `/api/notifications/${n.id}/read`)
        ok(qa, again, 200, 'read again')
        qa.expect(again.body.readAt, 'readAt changed on a repeated read (UC-78)').toBe(read.readAt)
        qa.expect(await unread('a'), 'a repeated read lowered the count (UC-78)').toBe(before - 1)
      },
    },
    {
      id: 'AC3',
      role: QA.b.email,
      devices: ['desktop', 'phone'],
      title: 'Đánh dấu tất cả đã đọc, kể cả thông báo chưa tải ở trang sau; bấm lại khi không còn gì chưa đọc',
      steps: [
        'Chuẩn bị: qa-fe13-b@ có trên 20 thông báo chưa đọc (thuky@ xin xem lịch rồi rút lại, nhiều lần)',
        'qa-fe13-b@ mở chuông: chỉ 20 dòng đầu được tải, có "Xem thông báo cũ hơn"',
        'Bấm "Đánh dấu tất cả đã đọc"',
        'Đọc trang 2 qua API; gọi lại POST /api/notifications/read-all',
      ],
      expected:
        'Mọi dòng hết tô, chip "mới" và badge chuông biến mất, nút tắt; API: unreadCount 0 và cả dòng ở trang 2 đã đọc (UC-77b); gọi lại → 200 markedCount 0, unreadCount 0 (UC-77b AF-1); điện thoại: nút chỉ còn icon nhưng vẫn có tên',
      async run(qa) {
        let count = await unread('b')
        for (let i = 0; count < 22 && i < 15; i += 1) {
          const share = await requestShare(qa, 'b')
          ok(qa, await post('secretary', `/api/calendar/shares/${share.id}/withdraw`), 200, 'withdraw')
          count = await unread('b')
        }
        qa.expect(count, 'could not give qa-fe13-b@ more than 20 unread').toBeGreaterThan(20)
        await home(qa)
        await qa.expect(bell(qa)).toHaveAttribute('aria-label', bellLabel(count))
        await openPanel(qa)
        await qa.expect(rows(qa)).toHaveCount(20)
        await qa.expect(panel(qa).getByRole('button', { name: 'Xem thông báo cũ hơn' })).toBeVisible()
        await qa.expect(panel(qa).locator('h2 .badge')).toHaveText(`${count} mới`)
        await qa.shot('truoc-danh-dau-tat-ca')
        const all = panel(qa).getByRole('button', { name: 'Đánh dấu tất cả đã đọc' })
        await press(qa, all)
        await qa.expect(panel(qa).locator('li.notif-item-unread')).toHaveCount(0)
        await qa.expect(panel(qa).locator('h2 .badge')).toHaveCount(0)
        await qa.expect(bell(qa)).toHaveAttribute('aria-label', 'Thông báo')
        await qa.expect(bell(qa).locator('.notif-badge')).toHaveCount(0)
        await qa.expect(all).toBeDisabled()
        await noSidewaysScroll(qa)
        await qa.shot('sau-danh-dau-tat-ca')
        qa.expect(await unread('b')).toBe(0)
        const page2 = await get('b', '/api/notifications?page=2&pageSize=20')
        qa.expect(page2.body.items.length, 'page 2 is empty').toBeGreaterThan(0)
        qa.expect(page2.body.items.filter((x) => !x.isRead).length, 'unread rows left on page 2 (UC-77b)').toBe(0)
        const again = ok(qa, await post('b', '/api/notifications/read-all'), 200, 'read-all again')
        qa.expect(again).toEqual({ markedCount: 0, unreadCount: 0 })
      },
    },
    {
      id: 'AC4',
      role: QA.empty.email,
      devices: ['desktop', 'phone'],
      title: 'Người chưa có thông báo nào: chuông không có số, panel báo trống',
      steps: ['qa-fe13-empty@ (chưa từng được mời) mở /mgmt/meetings', 'Bấm chuông'],
      expected:
        'Không có badge, nhãn chuông "Thông báo"; panel hiện "Chưa có thông báo nào", nút "Đánh dấu tất cả đã đọc" tắt, không có "Xem thông báo cũ hơn"',
      async run(qa) {
        const list = ok(qa, await get('empty', '/api/notifications?page=1&pageSize=1'), 200)
        if (list.totalCount !== 0) qa.inconclusive(`qa-fe13-empty@ already has ${list.totalCount} notifications; someone notified it`)
        await home(qa)
        await qa.expect(bell(qa)).toHaveAttribute('aria-label', 'Thông báo')
        await qa.expect(bell(qa).locator('.notif-badge')).toHaveCount(0)
        await openPanel(qa)
        await qa.expect(panel(qa).getByText('Chưa có thông báo nào')).toBeVisible()
        await qa.expect(panel(qa).getByRole('button', { name: 'Đánh dấu tất cả đã đọc' })).toBeDisabled()
        await qa.expect(panel(qa).getByRole('button', { name: 'Xem thông báo cũ hơn' })).toHaveCount(0)
        await panelFits(qa)
        await noSidewaysScroll(qa)
        await qa.shot('panel-trong')
      },
    },
    {
      id: 'AC5',
      role: QA.a.email,
      devices: ['desktop', 'phone'],
      title: 'Bấm một thông báo cuộc họp: mở trang cuộc họp đó, thông báo thành đã đọc; bấm lại thông báo đã đọc không đổi số',
      steps: [
        'thuky@ dời lịch cuộc họp dùng lại, qa-fe13-a@ có một thông báo mới',
        'qa-fe13-a@ ở /mgmt/meetings mở chuông, bấm dòng đầu',
        'Mở chuông lại, bấm lại chính dòng đó (đã đọc)',
      ],
      expected:
        'Chuyển tới /mgmt/meetings/{id}, tiêu đề cuộc họp hiện; panel đóng; thông báo đã đọc, số chưa đọc giảm 1 (UC-78, BR-033, BR-034); lần hai vẫn mở đúng trang, số chưa đọc giữ nguyên (AF-1, UC-78)',
      async run(qa) {
        const { m, n } = await freshForA(qa)
        const before = await unread('a')
        await home(qa)
        await openPanel(qa)
        const first = rows(qa).first()
        await qa.expect(first).toContainText(m.title)
        await qa.shot('truoc-bam-thong-bao')
        await press(qa, first.locator('button.notif-open'))
        await qa.expect(qa.page).toHaveURL(new RegExp(`/mgmt/meetings/${m.id}$`))
        await qa.expect(qa.page.getByRole('heading', { level: 1, name: m.title })).toBeVisible()
        await qa.expect(panel(qa)).toHaveCount(0)
        await qa.expect(bell(qa)).toHaveAttribute('aria-label', bellLabel(before - 1))
        await qa.shot('da-mo-trang-cuoc-hop')
        await expect.poll(async () => (await latest('a', 20)).find((x) => x.id === n.id)?.isRead).toBe(true)
        qa.expect(await unread('a')).toBe(before - 1)
        await openPanel(qa)
        const same = rows(qa).first()
        await qa.expect(same).toContainText(m.title)
        await qa.expect(same).not.toHaveClass(/notif-item-unread/)
        await press(qa, same.locator('button.notif-open'))
        await qa.expect(panel(qa)).toHaveCount(0)
        await qa.expect(qa.page).toHaveURL(new RegExp(`/mgmt/meetings/${m.id}$`))
        await qa.expect(qa.page.getByRole('heading', { level: 1, name: m.title })).toBeVisible()
        qa.expect(await unread('a'), 'opening a read notification changed the count (UC-78)').toBe(before - 1)
      },
    },
    {
      id: 'AC6',
      role: QA.a.email,
      devices: ['desktop', 'phone'],
      title: 'Thông báo của cuộc họp mình đã bị gỡ khỏi: không mở được, không lộ nội dung; thông báo thu hồi không có link',
      steps: [
        'thuky@ thêm qa-fe13-a@ vào "[QA] FE-13 dùng lại thành phần" (Đã công bố) rồi gỡ ra',
        'qa-fe13-a@ mở chuông, bấm dòng "Lời mời tham dự cuộc họp" của cuộc họp đó',
        'Bấm dòng "Lời mời tham dự đã được thu hồi"',
        'Mở thẳng /mgmt/meetings/{id}',
      ],
      expected:
        'Lời mời: toast "Thông báo này không còn khả dụng.", ở nguyên trang, không thấy cuộc họp (BR-033, BR-034; API target 404); thu hồi: không có link (targetType null, body không có đường dẫn — UC-30c), bấm chỉ đánh dấu đã đọc, ở nguyên trang; mở thẳng URL không thấy tiêu đề cuộc họp',
      async run(qa) {
        const { m, invited, removed } = await inviteThenRemove(qa)
        qa.expect(invited.length, 'qa-fe13-a@ got one invitation when added (BR-031, BR-032)').toBe(1)
        qa.expect(removed.length, 'qa-fe13-a@ got one withdrawal when removed (BR-031, BR-032)').toBe(1)
        qa.expect(removed[0].targetType, 'the withdrawal links to the meeting (UC-30c)').toBeNull()
        qa.expect(removed[0].body).not.toMatch(/\/mgmt\/|https?:/)
        ok(qa, await get('a', `/api/notifications/${invited[0].id}/target`), 404, 'target of the old invitation')
        await home(qa)
        await openPanel(qa)
        const withdrawal = rows(qa).filter({ hasText: 'Lời mời tham dự đã được thu hồi' }).filter({ hasText: m.title }).first()
        const invitation = rows(qa).filter({ hasText: 'Lời mời tham dự cuộc họp' }).filter({ hasText: m.title }).first()
        await qa.expect(withdrawal).toBeVisible()
        await qa.expect(invitation).toBeVisible()
        await qa.shot('thu-hoi-va-loi-moi')
        await press(qa, invitation.locator('button.notif-open'))
        await qa.expect(toast(qa, 'Thông báo này không còn khả dụng.')).toBeVisible()
        await qa.expect(qa.page).toHaveURL(/\/mgmt\/meetings$/)
        await qa.shot('loi-moi-khong-con-kha-dung')
        if (!(await panel(qa).isVisible())) await openPanel(qa)
        await press(qa, withdrawal.locator('button.notif-open'))
        await qa.expect(withdrawal).not.toHaveClass(/notif-item-unread/)
        await qa.expect(qa.page).toHaveURL(/\/mgmt\/meetings$/)
        await qa.shot('thu-hoi-chi-danh-dau-doc')
        await qa.goto(`/mgmt/meetings/${m.id}`)
        await qa.expect(qa.page.getByRole('heading', { level: 1, name: m.title })).toHaveCount(0)
        await qa.expect(qa.page.getByText(/Không tìm thấy/).first()).toBeVisible()
        await qa.shot('mo-thang-url-bi-an')
      },
    },
    {
      id: 'AC7',
      role: 'member',
      title: 'API: chỉ đọc/sửa được thông báo của chính mình; chưa đăng nhập 401; trang sai MSG39',
      steps: [
        'thanhvien@ gọi POST /api/notifications/{id}/read và GET /{id}/target với id thông báo của qa-fe13-a@',
        'Không token: GET /api/notifications, /unread-count, POST /read-all, negotiate hub',
        'qa-fe13-a@ gọi GET /api/notifications?pageSize=0 và ?page=0',
      ],
      expected:
        '404 cho thông báo của người khác, thông báo đó vẫn chưa đọc (BR-033, BR-034); 401 khi chưa đăng nhập; 400 MSG39 "Trang thông báo được yêu cầu không khả dụng." (UC-76 EX-2)',
      async run(qa) {
        const c = checker(qa)
        const { n } = await freshForA(qa)
        c.check('thanhvien@ đánh dấu thông báo của qa-fe13-a@', await post('member', `/api/notifications/${n.id}/read`), 404)
        c.check('thanhvien@ mở đích thông báo của qa-fe13-a@', await get('member', `/api/notifications/${n.id}/target`), 404)
        const still = (await latest('a', 20)).find((x) => x.id === n.id)
        c.fact('thông báo của qa-fe13-a@ vẫn chưa đọc', still?.isRead === false, JSON.stringify(still))
        c.check('không token: danh sách', await get('anon', '/api/notifications'), 401)
        c.check('không token: số chưa đọc', await get('anon', '/api/notifications/unread-count'), 401)
        c.check('không token: đánh dấu tất cả', await post('anon', '/api/notifications/read-all'), 401)
        c.check('không token: hub negotiate', await post('anon', '/hubs/notifications/negotiate?negotiateVersion=1'), 401)
        const bad = await get('a', '/api/notifications?page=1&pageSize=0')
        if (c.check('pageSize=0', bad, 400, 'MSG39'))
          c.fact('câu MSG39', bad.body.message === 'Trang thông báo được yêu cầu không khả dụng.', bad.body.message)
        c.check('page=0', await get('a', '/api/notifications?page=0&pageSize=20'), 400, 'MSG39')
        c.done()
      },
    },
    {
      id: 'AC8',
      role: QA.a.email,
      devices: ['desktop', 'phone'],
      title: 'Thông báo mới tới ngay, không tải lại trang: panel đóng → toast và chuông tăng; panel mở → dòng mới hiện ở đầu',
      steps: [
        'qa-fe13-a@ mở /mgmt/meetings, chờ kết nối hub',
        'thuky@ dời lịch cuộc họp dùng lại (panel đang đóng)',
        'qa-fe13-a@ mở chuông; thuky@ dời lịch lần nữa',
      ],
      expected:
        'Toast "Cuộc họp đã dời lịch — Cuộc họp “…” …" và nhãn chuông +1, trang không tải lại (UC-76); panel mở: thêm đúng một dòng ở đầu, chip "mới" +1',
      async run(qa) {
        const m = await mainMeeting(qa)
        const hub = qa.page.waitForEvent('websocket', { predicate: (ws) => ws.url().includes('/hubs/notifications'), timeout: 20_000 })
        await home(qa)
        const ws = await hub
        await ws.waitForEvent('framereceived', { timeout: 20_000 })
        await qa.page.evaluate(() => {
          window.__qaNoReload = true
        })
        const before = await unread('a')
        await qa.expect(bell(qa)).toHaveAttribute('aria-label', bellLabel(before))
        await reschedule(qa, m)
        await qa.expect(toast(qa, `Cuộc họp đã dời lịch — Cuộc họp “${m.title}”`).first()).toBeVisible({ timeout: 20_000 })
        await qa.expect(bell(qa)).toHaveAttribute('aria-label', bellLabel(before + 1), { timeout: 20_000 })
        qa.expect(await qa.page.evaluate(() => window.__qaNoReload), 'the page reloaded').toBe(true)
        await qa.shot('toast-va-chuong-tang')
        await openPanel(qa)
        const shown = await rows(qa).count()
        const firstText = await rows(qa).first().innerText()
        await reschedule(qa, m)
        await qa.expect(rows(qa)).toHaveCount(shown + 1, { timeout: 20_000 })
        await qa.expect(rows(qa).first()).toContainText(m.title)
        await qa.expect(rows(qa).first()).toHaveClass(/notif-item-unread/)
        qa.expect(await rows(qa).nth(1).innerText(), 'the new row is not on top').toBe(firstText)
        await qa.expect(bell(qa)).toHaveAttribute('aria-label', bellLabel(before + 2))
        qa.expect(await qa.page.evaluate(() => window.__qaNoReload), 'the page reloaded').toBe(true)
        await qa.shot('panel-mo-dong-moi-o-dau')
      },
    },
    {
      id: 'AC9',
      role: 'secretary',
      title: 'Luồng duyệt: gửi duyệt báo người duyệt; từ chối báo người tổ chức kèm lý do; duyệt báo người tổ chức; không ai nhận báo về việc mình làm',
      steps: [
        'thuky@ tạo "[QA] FE-13 Luồng duyệt …", gửi duyệt cho chutoa@',
        'chutoa@ từ chối có lý do; thuky@ gửi lại; chutoa@ duyệt',
        'thuky@ mở chuông (chỉ xem)',
      ],
      expected:
        'chutoa@ nhận 2 "Cuộc họp cần được duyệt" (UC-17 POST-5); thuky@ nhận "Cuộc họp bị từ chối" có lý do và "Cuộc họp đã được duyệt" (UC-18 POST-6); thuky@ không nhận báo gửi duyệt, chutoa@ không nhận báo duyệt/từ chối (BR-035); qa-fe13-a@ chưa nhận gì vì chưa công bố',
      async run(qa) {
        const c = checker(qa)
        const m = await createAt(qa, 'Draft', 'Luồng duyệt')
        const b = { chair: await seen('chair'), secretary: await seen('secretary'), a: await seen('a') }
        await step(qa, m.id, 'submit')
        await step(qa, m.id, 'reject')
        await step(qa, m.id, 'submit')
        await step(qa, m.id, 'approve')
        const chair = await fresh('chair', b.chair, { meetingId: m.id })
        const sec = await fresh('secretary', b.secretary, { meetingId: m.id })
        const a = await fresh('a', b.a, { meetingId: m.id })
        const types = (list) => list.map((n) => n.type).sort().join(',')
        c.fact('chutoa@: 2 MeetingWaitingApproval, không gì khác', types(chair) === 'MeetingWaitingApproval,MeetingWaitingApproval', types(chair))
        c.fact('thuky@: MeetingApproved + MeetingRejected', types(sec) === 'MeetingApproved,MeetingRejected', types(sec))
        const rejected = sec.find((n) => n.type === 'MeetingRejected')
        c.fact('từ chối có lý do', rejected?.body?.includes('QA FE-13: bổ sung tài liệu.'), rejected?.body)
        c.fact('qa-fe13-a@ chưa nhận gì', a.length === 0, types(a))
        for (const n of [...chair, ...sec]) c.fact(`${n.type} trỏ tới cuộc họp`, n.targetType === 'Meeting' && n.targetId === m.id)
        await home(qa)
        await openPanel(qa)
        await qa.expect(rows(qa).filter({ hasText: m.title }).first()).toBeVisible()
        await qa.shot('thu-ky-thay-duyet-va-tu-choi')
        c.done()
      },
    },
    {
      id: 'AC10',
      role: 'secretary',
      title: 'Công bố: mọi người dự họp nhận lời mời trong ứng dụng, trừ người bấm Công bố; lời mời mở được trang cuộc họp',
      steps: ['Cuộc họp đã duyệt "[QA] FE-13 Công bố …" (chutoa@ Chủ toạ, thuky@ Thư ký, qa-fe13-a@ Thành viên)', 'thuky@ bấm Công bố'],
      expected:
        'qa-fe13-a@ và chutoa@ mỗi người một "Cuộc họp đã được công bố" — "Bạn được mời tham dự cuộc họp “…”"; thuky@ không nhận (BR-031, BR-032, BR-035); GET target của qa-fe13-a@ → url /mgmt/meetings/{id} (UC-19, UC-30a)',
      async run(qa) {
        const c = checker(qa)
        const m = await createAt(qa, 'Approved', 'Công bố')
        const b = { chair: await seen('chair'), secretary: await seen('secretary'), a: await seen('a') }
        await step(qa, m.id, 'publish')
        const a = await fresh('a', b.a, { meetingId: m.id })
        const chair = await fresh('chair', b.chair, { meetingId: m.id })
        const sec = await fresh('secretary', b.secretary, { meetingId: m.id })
        c.fact('qa-fe13-a@ một MeetingInvited', a.length === 1 && a[0].type === 'MeetingInvited', JSON.stringify(a.map((n) => n.type)))
        c.fact('chutoa@ một MeetingInvited', chair.length === 1 && chair[0].type === 'MeetingInvited', JSON.stringify(chair.map((n) => n.type)))
        c.fact('thuky@ không nhận', sec.length === 0, JSON.stringify(sec.map((n) => n.type)))
        c.fact('nội dung lời mời', a[0]?.body === `Bạn được mời tham dự cuộc họp “${m.title}”.`, a[0]?.body)
        if (a[0]) {
          const t = await get('a', `/api/notifications/${a[0].id}/target`)
          if (c.check('target', t, 200)) c.fact('url', t.body.url === `/mgmt/meetings/${m.id}`, t.body.url)
        }
        // The Organizer's own panel: nothing about the meeting they just published (NOTI_FT27).
        await home(qa)
        await openPanel(qa)
        c.fact('panel của thuky@ không có lời mời của cuộc họp vừa công bố', (await rows(qa).filter({ hasText: m.title }).filter({ hasText: 'Bạn được mời tham dự' }).count()) === 0)
        await qa.shot('panel-nguoi-cong-bo-khong-co-loi-moi')
        c.done()
      },
    },
    {
      id: 'AC11',
      role: 'secretary',
      title: 'Dời lịch cuộc họp đã công bố: người được mời nhận thông báo, trừ người dời lịch',
      steps: ['thuky@ dời giờ "[QA] FE-13 dùng lại Đã công bố"'],
      expected: 'qa-fe13-a@ và chutoa@ mỗi người một "Cuộc họp đã dời lịch" trỏ tới cuộc họp; thuky@ không nhận (UC-14, BR-035)',
      async run(qa) {
        const c = checker(qa)
        const m = await mainMeeting(qa)
        const b = { chair: await seen('chair'), secretary: await seen('secretary'), a: await seen('a') }
        await reschedule(qa, m)
        for (const who of ['a', 'chair']) {
          const got = await fresh(who, b[who], { meetingId: m.id })
          c.fact(`${who}: một MeetingRescheduled`, got.length === 1 && got[0].type === 'MeetingRescheduled', JSON.stringify(got.map((n) => n.type)))
          c.fact(`${who}: trỏ tới cuộc họp`, got[0]?.targetType === 'Meeting' && got[0]?.targetId === m.id)
        }
        const sec = await fresh('secretary', b.secretary, { meetingId: m.id })
        c.fact('thuky@ không nhận', sec.length === 0, JSON.stringify(sec.map((n) => n.type)))
        c.done()
      },
    },
    {
      id: 'AC12',
      role: 'secretary',
      title: 'Huỷ họp: đã công bố → mọi người được mời nhận kèm lý do; đang chờ duyệt → người duyệt nhận',
      steps: [
        'thuky@ huỷ "[QA] FE-13 Huỷ đã công bố …" với lý do',
        'thuky@ huỷ "[QA] FE-13 Huỷ chờ duyệt …" (đang chờ chutoa@ duyệt)',
      ],
      expected:
        'Đã công bố: qa-fe13-a@ và chutoa@ nhận "Cuộc họp đã bị huỷ" có lý do, thuky@ không (UC-22, BR-035); chờ duyệt: chutoa@ nhận "Cuộc họp đã bị huỷ" có lý do, qa-fe13-a@ không nhận gì (UC-22)',
      async run(qa) {
        const c = checker(qa)
        const reason = `QA FE-13: lãnh đạo đi công tác ${rand()}.`
        const pub = await createAt(qa, 'Published', 'Huỷ đã công bố')
        let b = { chair: await seen('chair'), secretary: await seen('secretary'), a: await seen('a') }
        await transition(qa, 'secretary', pub.id, 'Cancel', { reason })
        for (const who of ['a', 'chair']) {
          const got = await fresh(who, b[who], { meetingId: pub.id })
          c.fact(`đã công bố → ${who}: một MeetingCancelled`, got.length === 1 && got[0].type === 'MeetingCancelled', JSON.stringify(got.map((n) => n.type)))
          c.fact(`đã công bố → ${who}: có lý do`, got[0]?.body?.includes(reason), got[0]?.body)
        }
        c.fact('đã công bố → thuky@ không nhận', (await fresh('secretary', b.secretary, { meetingId: pub.id })).length === 0)
        const wait = await createAt(qa, 'WaitingApproval', 'Huỷ chờ duyệt')
        b = { chair: await seen('chair'), a: await seen('a') }
        await transition(qa, 'secretary', wait.id, 'Cancel', { reason })
        const chair = await fresh('chair', b.chair, { meetingId: wait.id })
        c.fact('chờ duyệt → chutoa@: một MeetingCancelled', chair.length === 1 && chair[0].type === 'MeetingCancelled', JSON.stringify(chair.map((n) => n.type)))
        c.fact('chờ duyệt → có lý do', chair[0]?.body?.includes(reason), chair[0]?.body)
        c.fact('chờ duyệt → qa-fe13-a@ không nhận', (await fresh('a', b.a, { meetingId: wait.id })).length === 0)
        c.done()
      },
    },
    {
      id: 'AC13',
      role: 'chair',
      title: 'Bắt đầu họp: người dự họp nhận "Cuộc họp đã bắt đầu", trừ Chủ toạ bấm Bắt đầu',
      steps: ['chutoa@ bắt đầu "[QA] FE-13 Bắt đầu …" (đã công bố), rồi kết thúc'],
      expected: 'qa-fe13-a@ và thuky@ mỗi người một MeetingStarted "Cuộc họp “…” đã bắt đầu."; chutoa@ không nhận (UC-20, BR-035)',
      async run(qa) {
        const c = checker(qa)
        const m = await createAt(qa, 'Published', 'Bắt đầu')
        const b = { chair: await seen('chair'), secretary: await seen('secretary'), a: await seen('a') }
        await step(qa, m.id, 'start')
        for (const who of ['a', 'secretary']) {
          const got = await fresh(who, b[who], { meetingId: m.id })
          c.fact(`${who}: một MeetingStarted`, got.length === 1 && got[0].type === 'MeetingStarted', JSON.stringify(got.map((n) => n.type)))
          c.fact(`${who}: nội dung`, got[0]?.body === `Cuộc họp “${m.title}” đã bắt đầu.`, got[0]?.body)
        }
        c.fact('chutoa@ không nhận', (await fresh('chair', b.chair, { meetingId: m.id })).length === 0)
        await step(qa, m.id, 'end')
        c.done()
      },
    },
    {
      id: 'AC14',
      role: 'secretary',
      title: 'Thành phần tham dự sau công bố: thêm người → lời mời; đổi vai trò → báo vai trò mới; gỡ → báo thu hồi không kèm link',
      steps: [
        'thuky@ thêm qa-fe13-a@ (Thành viên) vào "[QA] FE-13 dùng lại thành phần"',
        'thuky@ đổi qa-fe13-a@ sang Thư ký',
        'thuky@ gỡ qa-fe13-a@',
      ],
      expected:
        'qa-fe13-a@ nhận lần lượt "Lời mời tham dự cuộc họp" (BR-031, BR-032), "Vai trò cuộc họp đã thay đổi" — "…bạn đã được giao làm Thư ký." (UC-30b), "Lời mời tham dự đã được thu hồi" không có targetType/đường dẫn (BR-031, BR-032, UC-30c); thuky@ không nhận gì (BR-035); target của lời mời cũ → 404 (BR-033, BR-034)',
      async run(qa) {
        const c = checker(qa)
        const m = await rosterMeeting(qa)
        await idOf('a')
        await removeA(qa, m)
        const bs = await seen('secretary')
        let b = await seen('a')
        await addA(qa, m)
        const invited = await fresh('a', b, { meetingId: m.id })
        c.fact('thêm → một lời mời', invited.length === 1 && invited[0].type === 'MeetingInvited', JSON.stringify(invited.map((n) => n.type)))
        c.fact('lời mời trỏ tới cuộc họp', invited[0]?.targetType === 'Meeting' && invited[0]?.targetId === m.id)
        b = await seen('a')
        const { items, etag } = await participants(m)
        const row = items.find((p) => p.userId === IDS[QA.a.email])
        const changed = await put('secretary', `/api/meetings/${m.id}/participants/assignments`, { changes: [{ participantId: row.id, role: 'Secretary' }] }, { 'If-Match': etag })
        c.check('đổi vai trò', changed, 200, 'MSG100')
        const role = await fresh('a', b, { meetingId: m.id })
        c.fact('đổi vai trò → một MeetingRoleChanged', role.length === 1 && role[0].type === 'MeetingRoleChanged', JSON.stringify(role.map((n) => n.type)))
        c.fact('nói vai trò mới', role[0]?.body === `Cuộc họp “${m.title}”: bạn đã được giao làm Thư ký.`, role[0]?.body)
        b = await seen('a')
        await removeA(qa, m)
        const removed = await fresh('a', b)
        c.fact('gỡ → một MeetingParticipantRemoved', removed.length === 1 && removed[0].type === 'MeetingParticipantRemoved', JSON.stringify(removed.map((n) => n.type)))
        c.fact('thu hồi không có link', removed[0]?.targetType === null && removed[0]?.actionUrl == null && !/\/mgmt\/|https?:/.test(removed[0]?.body ?? ''), JSON.stringify(removed[0]))
        if (invited[0]) c.check('target lời mời cũ sau khi bị gỡ', await get('a', `/api/notifications/${invited[0].id}/target`), 404)
        c.fact('thuky@ không nhận gì', (await fresh('secretary', bs, { meetingId: m.id })).length === 0)
        c.done()
      },
    },
    {
      id: 'AC15',
      role: 'secretary',
      title: 'Giao người trình bày khi cuộc họp đã công bố: người được giao nhận thông báo; tự giao cho mình thì không',
      steps: [
        'thuky@ thêm mục nghị sự "[QA] FE-13 mục …" vào "[QA] FE-13 dùng lại Đã công bố", người trình bày qa-fe13-a@',
        'thuky@ thêm mục khác, người trình bày là chính mình',
        'Xoá hai mục',
      ],
      expected: 'qa-fe13-a@ nhận một AgendaPresenterAssigned nêu tên mục (UC-23a, UC-23b); thuky@ không nhận cho mục tự giao (BR-035)',
      async run(qa) {
        const c = checker(qa)
        const m = await mainMeeting(qa)
        const A = `/api/meetings/${m.id}/agenda`
        const body = (title, presenterId) => ({ title, description: '', presenterId, presenterGuestInvitationId: null })
        const b = { a: await seen('a'), secretary: await seen('secretary') }
        const t1 = qa.name(`FE-13 mục giao người khác ${rand()}`)
        const t2 = qa.name(`FE-13 mục tự giao ${rand()}`)
        const r1 = await post('secretary', A, body(t1, await idOf('a')))
        const r2 = await post('secretary', A, body(t2, await idOf('secretary')))
        c.check('thêm mục giao qa-fe13-a@', r1, 201)
        c.check('thêm mục tự giao', r2, 201)
        const a = await fresh('a', b.a, { type: 'AgendaPresenterAssigned', meetingId: m.id })
        c.fact('qa-fe13-a@ một AgendaPresenterAssigned', a.length === 1, JSON.stringify(a.map((n) => n.body)))
        c.fact('nêu tên mục', `${a[0]?.title} ${a[0]?.body}`.includes(t1), `${a[0]?.title} | ${a[0]?.body}`)
        const sec = await fresh('secretary', b.secretary, { type: 'AgendaPresenterAssigned', meetingId: m.id })
        c.fact('thuky@ không nhận', sec.length === 0, JSON.stringify(sec.map((n) => n.body)))
        for (const r of [r1, r2]) if (r.body?.agendaItemId) await del('secretary', `${A}/${r.body.agendaItemId}`)
        c.done()
      },
    },
    {
      id: 'AC16',
      role: 'secretary',
      title: 'Biên bản: gửi duyệt báo Chủ toạ; trả lại báo Thư ký; ban hành báo người đọc được, trừ Chủ toạ vừa duyệt',
      steps: [
        '"[QA] FE-13 Biên bản …" đã kết thúc; thuky@ tạo bản nháp, gửi duyệt',
        'chutoa@ trả lại có lý do; thuky@ gửi lại; chutoa@ duyệt (ban hành)',
      ],
      expected:
        'chutoa@ nhận 2 MinutesWaitingApproval; thuky@ nhận MinutesReturned và MinutesIssued; qa-fe13-a@ nhận MinutesIssued; chutoa@ không nhận MinutesIssued (UC-72, UC-73, UC-74, BR-035)',
      async run(qa) {
        const c = checker(qa)
        const m = await createAt(qa, 'Ended', 'Biên bản')
        const created = await post('secretary', `/api/meetings/${m.id}/minutes`, { rebuild: false })
        if (!c.check('tạo bản nháp', created, 201, 'MSG395')) return c.done()
        const state = async () => ok(qa, await get('secretary', `/api/meetings/${m.id}/minutes`), 200).minutes
        const b = { chair: await seen('chair'), secretary: await seen('secretary'), a: await seen('a') }
        let mins = await state()
        c.check('gửi duyệt', await post('secretary', `/api/minutes/${mins.id}/submit`, { updatedAt: mins.updatedAt }), 200, 'MSG424')
        mins = await state()
        const reason = `QA FE-13: sửa phần kết luận ${rand()}.`
        c.check('trả lại', await post('chair', `/api/minutes/${mins.id}/reject`, { reason, updatedAt: mins.updatedAt }), 200, 'MSG430')
        mins = await state()
        c.check('gửi lại', await post('secretary', `/api/minutes/${mins.id}/submit`, { updatedAt: mins.updatedAt }), 200, 'MSG424')
        mins = await state()
        c.check('duyệt', await post('chair', `/api/minutes/${mins.id}/approve`, { updatedAt: mins.updatedAt }), 200, 'MSG427')
        const types = async (who) => (await fresh(who, b[who], { meetingId: m.id })).map((n) => n.type).sort()
        const chair = await types('chair')
        const sec = await fresh('secretary', b.secretary, { meetingId: m.id })
        const a = await types('a')
        c.fact('chutoa@: 2 MinutesWaitingApproval, không MinutesIssued', chair.join(',') === 'MinutesWaitingApproval,MinutesWaitingApproval', chair.join(','))
        c.fact('thuky@: MinutesIssued + MinutesReturned', sec.map((n) => n.type).sort().join(',') === 'MinutesIssued,MinutesReturned', sec.map((n) => n.type).join(','))
        const returned = sec.find((n) => n.type === 'MinutesReturned')
        // UC-74 POST-4 / UC-72, UC-73, UC-74 ask only that the Secretary is notified; the reason lives on the minutes tab.
        if (returned && !returned.body.includes(reason)) qa.note(`MinutesReturned không kèm lý do: "${returned.body}" (UC-74 không đòi)`)
        c.fact('qa-fe13-a@: MinutesIssued', a.join(',') === 'MinutesIssued', a.join(','))
        c.done()
      },
    },
    {
      id: 'AC17',
      role: QA.a.email,
      title: 'Chia sẻ lịch: xin xem lịch báo chủ lịch, bấm thông báo mở đúng chia sẻ; từ chối báo người xin',
      steps: [
        'thuky@ xin xem lịch của qa-fe13-a@',
        'qa-fe13-a@ mở chuông, bấm "Yêu cầu xem lịch"',
        'qa-fe13-a@ từ chối',
      ],
      expected:
        'qa-fe13-a@ nhận CalendarShareRequested trỏ tới chia sẻ (không phải cuộc họp); bấm → /calendar/sharing?share={id}, dòng chia sẻ của thuky@ hiện; từ chối → thuky@ nhận CalendarShareDeclined của đúng chia sẻ đó (BR-009–BR-011)',
      async run(qa) {
        const c = checker(qa)
        const before = await seen('a')
        const share = await requestShare(qa, 'a')
        const got = await fresh('a', before, { targetId: share.id })
        c.fact('qa-fe13-a@ một CalendarShareRequested', got.length === 1 && got[0].type === 'CalendarShareRequested' && got[0].targetType === 'CalendarShare' && got[0].meetingId === null, JSON.stringify(got))
        await home(qa)
        await openPanel(qa)
        const row = rows(qa).filter({ hasText: 'Yêu cầu xem lịch' }).first()
        await qa.expect(row).toBeVisible()
        await qa.shot('yeu-cau-xem-lich')
        await press(qa, row.locator('button.notif-open'))
        await qa.expect(qa.page).toHaveURL(new RegExp(`/calendar/sharing\\?share=${share.id}$`))
        await qa.expect(qa.page.locator(`#share-${share.id}`)).toBeVisible()
        const requester = (await get('secretary', '/api/auth/me')).body.fullName
        await qa.expect(qa.page.locator(`#share-${share.id}`)).toContainText(requester)
        await qa.shot('mo-trang-chia-se')
        const bs = await seen('secretary')
        c.check('qa-fe13-a@ từ chối', await post('a', `/api/calendar/shares/${share.id}/decline`), 200, 'MSG437')
        const back = await fresh('secretary', bs, { targetId: share.id })
        c.fact('thuky@ một CalendarShareDeclined', back.length === 1 && back[0].type === 'CalendarShareDeclined', JSON.stringify(back.map((n) => n.type)))
        c.done()
      },
    },
    {
      id: 'AC18',
      role: 'secretary',
      title: 'Nhắc họp sắp bắt đầu (15 phút trước giờ họp)',
      steps: ['Cần một cuộc họp Đã công bố bắt đầu trong vòng 15 phút'],
      expected: 'Mỗi người được mời nội bộ đang hoạt động nhận một "sắp bắt đầu"; dời lịch thì được nhắc lại theo giờ mới (UC-76)',
      async run(qa) {
        qa.inconclusive(
          'Kế hoạch chỉ đặt lịch 10 tuần tới. Kiểm tay: công bố một cuộc họp bắt đầu sau 10 phút (thuky@ tổ chức, thanhvien@ dự), chờ ≤ 5 giây — thanhvien@ và chutoa@ nhận "Cuộc họp sắp bắt đầu" một lần; dời lịch sang 12 phút sau → nhận lại một lần; rồi huỷ cuộc họp.',
        )
      },
    },
    {
      id: 'AC19',
      role: 'secretary',
      title: 'Email: công bố gửi thư mời cho mọi người dự họp (kể cả người công bố); huỷ cuộc họp đã công bố gửi thư huỷ; người trình bày là Khách nhận thư',
      steps: ['AC10, AC12 đã tạo các sự kiện', 'Tìm hàng đợi thư qua API'],
      expected:
        'Mỗi người dự họp nội bộ có một thư mời (cả thuky@), thư huỷ kèm lý do cho người đã nhận thư mời (BR-031, BR-032, UC-22, BR-035); thư tới hộp mail thật',
      async run(qa) {
        qa.inconclusive(
          'Không có endpoint nào đọc hàng đợi thư (email_messages, meeting_invitation_dispatches) và tài khoản QA dùng @trustcabinet.local nên không có hộp mail. Kiểm tay (chỉ đọc): select to_address, subject, status, delivery_state, created_at from email_messages where subject like \'%[QA] FE-13%\' order by created_at desc — mong đợi: mỗi "[QA] FE-13 Công bố …" có thư cho chutoa@, thuky@, qa-fe13-a@; mỗi "[QA] FE-13 Huỷ đã công bố …" có thư huỷ cho cùng ba người; "[QA] FE-13 Huỷ chờ duyệt …" không có thư. Gửi thật: công bố một cuộc họp có một người dùng email thật, xem hộp thư (có .ics, link /mgmt/meetings/{id}).',
        )
      },
    },
    {
      id: 'AC20',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Hơn 99 thông báo chưa đọc: chuông "99+", chip "99+ mới"; mỗi dòng có giờ tương đối và giờ chính xác khi rê chuột',
      steps: [
        'thuky@ (tài khoản seed đang có hơn 99 thông báo chưa đọc; chỉ xem, không đánh dấu) mở /mgmt/meetings',
        'Bấm chuông',
        'Rê chuột vào giờ của dòng đầu',
      ],
      expected:
        'Badge "99+", nhãn "Thông báo, N chưa đọc"; panel có chip "99+ mới"; mỗi dòng có giờ tương đối ("Vừa xong", "N phút trước", "N giờ trước", "Hôm qua lúc …") và tooltip là ngày giờ đầy đủ (UC-76)',
      async run(qa) {
        const count = await unread('secretary')
        if (count <= 99) qa.inconclusive(`thuky@ chỉ có ${count} chưa đọc, cần hơn 99; tạo thêm thông báo cho thuky@ rồi chạy lại`)
        await home(qa)
        await qa.expect(bell(qa).locator('.notif-badge')).toHaveText('99+')
        await qa.expect(bell(qa)).toHaveAttribute('aria-label', bellLabel(count))
        await qa.shot('chuong-99-cong')
        await openPanel(qa)
        await qa.expect(panel(qa).locator('h2 .badge')).toHaveText('99+ mới')
        const time = rows(qa).first().locator('time.notif-time')
        const text = (await time.innerText()).trim()
        qa.expect(text, 'relative time').toMatch(/^(Vừa xong|\d+ phút trước|\d+ giờ trước|Hôm qua lúc .+|\d{2}:\d{2} \d{2}\/\d{2}\/\d{4})$/)
        const tip = await time.getAttribute('data-tooltip')
        qa.expect(tip, 'exact time tooltip').toMatch(/^\d{2}:\d{2} \d{2}\/\d{2}\/\d{4}$/)
        qa.note(`giờ tương đối "${text}", tooltip "${tip}"`)
        if (qa.device === 'desktop') {
          await time.hover()
          await qa.page.waitForTimeout(900)
        }
        await qa.shot('panel-99-cong-gio-tuong-doi')
        await panelFits(qa)
        await noSidewaysScroll(qa)
      },
    },
    {
      id: 'AC21',
      role: QA.b.email,
      devices: ['desktop', 'phone'],
      title: 'Phân trang danh sách: "Xem thông báo cũ hơn" nối thêm dòng, hiện "Đang tải…", không lặp, nút biến mất ở trang cuối',
      steps: ['qa-fe13-b@ (đã có hơn 20 thông báo, tất cả đã đọc sau AC3) mở chuông', 'Bấm "Xem thông báo cũ hơn" cho tới khi hết'],
      expected:
        'Lần bấm đầu: nút "Đang tải…" rồi dòng được nối thêm (API ?page=2&pageSize=20 → 200); cuối cùng số dòng = tổng số thông báo của API, không lặp, nút biến mất',
      async run(qa) {
        const list = ok(qa, await get('b', '/api/notifications?page=1&pageSize=20'), 200)
        if (list.totalCount <= 20) qa.inconclusive(`qa-fe13-b@ chỉ có ${list.totalCount} thông báo; chạy AC3 trước`)
        await qa.page.route(
          (u) => u.pathname === '/api/notifications' && u.searchParams.get('page') === '2',
          async (route) => {
            await new Promise((r) => setTimeout(r, 1200))
            await route.fallback()
          },
        )
        await home(qa)
        await openPanel(qa)
        await qa.expect(rows(qa)).toHaveCount(20)
        const more = panel(qa).getByRole('button', { name: /Xem thông báo cũ hơn|Đang tải/ })
        const page2 = qa.page.waitForResponse((r) => new URL(r.url()).pathname === '/api/notifications' && r.url().includes('page=2'))
        await press(qa, more)
        await qa.expect(more).toHaveText('Đang tải…')
        await qa.shot('dang-tai-trang-2')
        qa.expect((await page2).status()).toBe(200)
        await qa.expect(rows(qa)).toHaveCount(Math.min(40, list.totalCount))
        await qa.shot('da-noi-them-dong')
        for (let i = 0; i < 40 && (await panel(qa).getByRole('button', { name: 'Xem thông báo cũ hơn' }).count()) > 0; i += 1) {
          await press(qa, panel(qa).getByRole('button', { name: 'Xem thông báo cũ hơn' }))
          await qa.page.waitForTimeout(1500)
        }
        await qa.expect(panel(qa).getByRole('button', { name: /Xem thông báo cũ hơn|Đang tải/ })).toHaveCount(0)
        const total = ok(qa, await get('b', '/api/notifications?page=1&pageSize=1'), 200).totalCount
        await qa.expect(rows(qa)).toHaveCount(total)
        qa.note(`${total} dòng = totalCount`)
        await qa.shot('trang-cuoi-het-nut')
      },
    },
    {
      id: 'AC22',
      role: QA.a.email,
      devices: ['desktop', 'phone'],
      title: 'Lỗi tải danh sách: lần đầu báo lỗi kèm "Thử lại"; lần làm mới sau giữ danh sách cũ và báo "Không làm mới được danh sách."',
      steps: [
        'Chặn GET /api/notifications trả 500 MSG40 (chèn lỗi bằng trình duyệt), qa-fe13-a@ mở chuông lần đầu',
        'Bỏ chặn, bấm "Thử lại"',
        'Đóng panel, chặn lại, mở lại chuông',
      ],
      expected:
        'Lần đầu: "Không thể tải thông báo. Vui lòng thử lại." và nút "Thử lại"; bấm Thử lại thì danh sách hiện; lần sau: các dòng cũ vẫn hiện, một dòng "Không làm mới được danh sách." kèm "Thử lại" ở trên (UC-76 EX-1)',
      async run(qa) {
        await freshForA(qa)
        await home(qa)
        const lift = await failList(qa)
        await press(qa, bell(qa))
        await qa.expect(panel(qa).getByText('Không thể tải thông báo. Vui lòng thử lại.')).toBeVisible()
        const retry = panel(qa).getByRole('button', { name: 'Thử lại' })
        await qa.expect(retry).toBeVisible()
        await qa.expect(rows(qa)).toHaveCount(0)
        await panelFits(qa)
        await qa.shot('loi-tai-lan-dau-co-thu-lai')
        await lift()
        await press(qa, retry)
        await qa.expect(rows(qa).first()).toBeVisible()
        await qa.shot('thu-lai-thanh-cong')
        const shown = await rows(qa).count()
        await press(qa, bell(qa))
        await qa.expect(panel(qa)).toHaveCount(0)
        const lift2 = await failList(qa)
        await press(qa, bell(qa))
        await qa.expect(panel(qa).getByText('Không làm mới được danh sách.')).toBeVisible()
        await qa.expect(rows(qa)).toHaveCount(shown)
        await qa.expect(panel(qa).getByRole('button', { name: 'Thử lại' })).toBeVisible()
        await noSidewaysScroll(qa)
        await qa.shot('lam-moi-loi-giu-danh-sach-cu')
        await lift2()
      },
    },
    {
      id: 'AC23',
      role: QA.a.email,
      devices: ['desktop', 'phone'],
      title: 'Panel không chặn trang: Escape đóng và trả tiêu điểm về chuông; bấm ra ngoài đóng; trang phía sau vẫn nhận thao tác',
      steps: [
        'Mở chuông, bấm vào ô "Tìm cuộc họp…" của trang (desktop) hoặc vùng trống thanh trên (điện thoại)',
        'Mở chuông, nhấn Escape',
      ],
      expected: 'Bấm ngoài đóng panel và trên desktop ô tìm kiếm phía sau nhận ngay chữ vừa gõ (không có lớp phủ chặn); Escape đóng panel, bàn phím về nút chuông',
      async run(qa) {
        await ensureUnread(qa, 'a')
        await home(qa)
        await openPanel(qa)
        if (qa.device === 'desktop') {
          const search = qa.page.getByPlaceholder('Tìm cuộc họp…')
          await search.click()
          await qa.page.keyboard.type('xyz')
          await qa.expect(search).toHaveValue('xyz')
          await qa.expect(panel(qa)).toHaveCount(0)
          await qa.shot('bam-ngoai-dong-va-trang-sau-nhan-thao-tac')
        } else {
          await qa.page.touchscreen.tap(225, 28)
          await qa.expect(panel(qa)).toHaveCount(0)
          await qa.shot('cham-ngoai-dong-panel')
        }
        await openPanel(qa)
        await qa.shot('panel-dang-mo')
        await qa.page.keyboard.press('Escape')
        await qa.expect(panel(qa)).toHaveCount(0)
        await qa.expect(bell(qa)).toBeFocused()
        await qa.shot('escape-dong-focus-ve-chuong')
      },
    },
    {
      id: 'AC24',
      role: QA.a.email,
      devices: ['desktop', 'phone'],
      title: 'Panel vừa màn hình điện thoại: nút "Đọc hết" thay "Đánh dấu tất cả đã đọc", nút ✓ từng dòng luôn hiện; desktop dùng chữ đầy đủ',
      steps: ['qa-fe13-a@ có thông báo chưa đọc, mở chuông ở 390px (cảm ứng) và 1440px'],
      expected:
        'Điện thoại: panel trải hết chiều ngang, nút ghi "Đọc hết", nút ✓ hiện không cần rê chuột, trang không cuộn ngang; desktop: nút ghi "Đánh dấu tất cả đã đọc"',
      async run(qa) {
        await freshForA(qa)
        await home(qa)
        await openPanel(qa)
        const full = panel(qa).locator('.notif-mark-all-label')
        const short = panel(qa).locator('.notif-mark-all-short')
        const mark = panel(qa).locator('button.notif-mark').first()
        await qa.expect(mark).toBeVisible()
        if (qa.device === 'phone') {
          await qa.expect(short).toBeVisible()
          // The long label stays for screen readers, clipped to one pixel.
          qa.expect((await full.boundingBox()).width, 'the long label is still drawn').toBeLessThanOrEqual(2)
          qa.expect(await mark.evaluate((el) => getComputedStyle(el).opacity), 'check hidden until hover').toBe('1')
          const box = await panel(qa).boundingBox()
          qa.expect(box.width, 'panel width on phone').toBeGreaterThan(qa.page.viewportSize().width - 40)
        } else {
          await qa.expect(full).toBeVisible()
          await qa.expect(short).toBeHidden()
        }
        await panelFits(qa)
        await noSidewaysScroll(qa)
        await qa.shot('nut-doc-het-va-nut-tick')
      },
    },
    {
      id: 'AC25',
      role: 'anon',
      devices: ['desktop', 'phone'],
      title: 'Chưa đăng nhập: /calendar chuyển về /login, không có chuông, không có yêu cầu nào tới /api/notifications hay hub',
      steps: ['Mở /calendar khi chưa có phiên'],
      expected: 'Về /login; không thấy chuông hay panel; không có request /api/notifications hoặc /hubs/notifications nào',
      async run(qa) {
        const seenUrls = []
        qa.page.on('request', (r) => {
          if (/^\/(api|hubs)\/notifications(\/|$)/.test(new URL(r.url()).pathname)) seenUrls.push(r.url())
        })
        qa.page.on('websocket', (ws) => {
          if (ws.url().includes('/hubs/notifications')) seenUrls.push(ws.url())
        })
        await qa.goto('/calendar')
        await qa.expect(qa.page).toHaveURL(/\/login/)
        await qa.expect(bell(qa)).toHaveCount(0)
        await qa.expect(panel(qa)).toHaveCount(0)
        qa.expect(seenUrls, 'requests to notifications while signed out').toEqual([])
        await qa.shot('ve-login-khong-chuong')
      },
    },
    {
      id: 'AC26',
      role: QA.a.email,
      devices: ['desktop', 'phone'],
      title: 'Đánh dấu một dòng bị lỗi: dòng hết tô rồi tô lại, số chưa đọc trở về, toast đỏ báo lỗi, dữ liệu vẫn chưa đọc',
      steps: ['qa-fe13-a@ mở chuông', 'Chặn POST /api/notifications/{id}/read (trả 500, chậm 1,5 giây) rồi bấm ✓ trên dòng đầu'],
      expected:
        'Ngay sau khi bấm dòng hết tô và chuông giảm 1; sau khi lỗi dòng tô lại, chuông về số cũ, toast đỏ "Máy chủ đang gặp sự cố. Vui lòng thử lại sau."; API: vẫn chưa đọc',
      async run(qa) {
        const { n } = await freshForA(qa)
        const before = await unread('a')
        await home(qa)
        await openPanel(qa)
        await qa.page.route(
          (u) => /\/api\/notifications\/[^/]+\/read$/.test(u.pathname),
          async (route) => {
            await new Promise((r) => setTimeout(r, 1500))
            await route.fulfill({ status: 500, contentType: 'application/json', body: '{}' })
          },
        )
        const first = rows(qa).first()
        await press(qa, first.locator('button.notif-mark'))
        await qa.expect(first).not.toHaveClass(/notif-item-unread/)
        await qa.expect(bell(qa)).toHaveAttribute('aria-label', bellLabel(before - 1))
        await qa.shot('truoc-khi-loi-dong-da-doc-tam')
        await qa.expect(first).toHaveClass(/notif-item-unread/, { timeout: 8000 })
        await qa.expect(bell(qa)).toHaveAttribute('aria-label', bellLabel(before))
        await qa.expect(toast(qa, 'Máy chủ đang gặp sự cố. Vui lòng thử lại sau.')).toBeVisible()
        await qa.shot('sau-loi-dong-to-lai-toast-do')
        const now = (await latest('a', 20)).find((x) => x.id === n.id)
        qa.expect(now?.isRead, 'the notification was marked read despite the failure').toBe(false)
      },
    },
    {
      id: 'AC27',
      role: QA.b.email,
      devices: ['desktop', 'phone'],
      title: 'Đánh dấu tất cả đã đọc chỉ ảnh hưởng tài khoản của mình: tài khoản khác giữ nguyên số chưa đọc và dòng chưa đọc',
      steps: ['qa-fe13-b@ bấm "Đánh dấu tất cả đã đọc"', 'Đăng nhập qa-fe13-a@ (phiên khác) mở chuông'],
      expected: 'qa-fe13-b@ về 0; qa-fe13-a@ giữ nguyên số chưa đọc trước đó, các dòng chưa đọc vẫn tô (UC-77b, BR-033, BR-034)',
      async run(qa) {
        await freshForA(qa)
        await ensureUnread(qa, 'b')
        const aBefore = await unread('a')
        await home(qa)
        await openPanel(qa)
        await press(qa, panel(qa).getByRole('button', { name: 'Đánh dấu tất cả đã đọc' }))
        await qa.expect(bell(qa)).toHaveAttribute('aria-label', 'Thông báo')
        await qa.shot('b-da-doc-het')
        qa.expect(await unread('b')).toBe(0)
        qa.expect(await unread('a'), 'qa-fe13-a@ unread changed by qa-fe13-b@').toBe(aBefore)
        await qa.signIn(QA.a.email)
        await home(qa)
        await qa.expect(bell(qa)).toHaveAttribute('aria-label', bellLabel(aBefore))
        await openPanel(qa)
        await qa.expect(panel(qa).locator('li.notif-item-unread').first()).toBeVisible()
        await qa.shot('a-van-con-chua-doc')
      },
    },
    {
      id: 'AC28',
      role: QA.b.email,
      devices: ['desktop'],
      title: 'Hai tab cùng tài khoản: tab này đánh dấu hết, tab kia mở chuông thì đọc lại trang 1 và thấy tất cả đã đọc',
      steps: ['Mở hai tab cho qa-fe13-b@ (có thông báo chưa đọc)', 'Tab 1 bấm "Đánh dấu tất cả đã đọc"', 'Tab 2 mở chuông'],
      expected: 'Tab 2 đọc lại trang 1 khi mở: mọi dòng đã đọc, chip và badge biến mất, nút "Đánh dấu tất cả đã đọc" tắt',
      async run(qa) {
        await ensureUnread(qa, 'b')
        await home(qa)
        const tab1 = await qa.context.newPage()
        const other = tabOf(qa, tab1)
        await tab1.goto(`${qa.base}/mgmt/meetings`)
        await qa.expect(bell(other)).toBeVisible()
        await openPanel(other)
        await press(other, panel(other).getByRole('button', { name: 'Đánh dấu tất cả đã đọc' }))
        await qa.expect(panel(other).locator('li.notif-item-unread')).toHaveCount(0)
        qa.expect(await unread('b')).toBe(0)
        await qa.shot('tab-2-truoc-khi-mo-chuong')
        await openPanel(qa)
        await qa.expect(panel(qa).locator('li.notif-item-unread')).toHaveCount(0)
        await qa.expect(panel(qa).locator('h2 .badge')).toHaveCount(0)
        await qa.expect(bell(qa)).toHaveAttribute('aria-label', 'Thông báo')
        await qa.expect(panel(qa).getByRole('button', { name: 'Đánh dấu tất cả đã đọc' })).toBeDisabled()
        await qa.shot('tab-2-thay-tat-ca-da-doc')
        await tab1.close()
      },
    },
    {
      id: 'AC29',
      role: QA.a.email,
      devices: ['desktop', 'phone'],
      title: 'Bấm thông báo của trang đang đứng: ở nguyên URL, trang đọc lại dữ liệu, không chồng lịch sử (Back về trang trước đó)',
      steps: [
        'thuky@ dời lịch hai lần để qa-fe13-a@ có hai thông báo cùng cuộc họp',
        'qa-fe13-a@ từ /mgmt/meetings mở chuông, bấm dòng đầu (vào trang cuộc họp)',
        'Mở chuông, bấm dòng thứ hai của cùng cuộc họp',
        'Nhấn Back một lần',
      ],
      expected: 'Lần hai URL vẫn là /mgmt/meetings/{id} và trang gọi lại GET /api/meetings/{id}; Back về /mgmt/meetings chứ không về một bản trùng của trang cuộc họp',
      async run(qa) {
        const { m } = await freshForA(qa)
        await reschedule(qa, m)
        await home(qa)
        const calls = []
        qa.page.on('request', (r) => {
          if (r.method() === 'GET' && new URL(r.url()).pathname === `/api/meetings/${m.id}`) calls.push(Date.now())
        })
        await openPanel(qa)
        await press(qa, rows(qa).filter({ hasText: m.title }).first().locator('button.notif-open'))
        await qa.expect(qa.page).toHaveURL(new RegExp(`/mgmt/meetings/${m.id}$`))
        await qa.expect(qa.page.getByRole('heading', { level: 1, name: m.title })).toBeVisible()
        const afterFirst = calls.length
        await openPanel(qa)
        await press(qa, rows(qa).filter({ hasText: m.title }).nth(1).locator('button.notif-open'))
        await qa.expect(panel(qa)).toHaveCount(0)
        await qa.expect(qa.page).toHaveURL(new RegExp(`/mgmt/meetings/${m.id}$`))
        await qa.expect(qa.page.getByRole('heading', { level: 1, name: m.title })).toBeVisible()
        await qa.page.waitForTimeout(1000)
        await qa.shot('o-nguyen-trang-cuoc-hop')
        qa.expect(calls.length, 'the meeting page did not re-read its data').toBeGreaterThan(afterFirst)
        await qa.page.goBack()
        await qa.expect(qa.page).toHaveURL(/\/mgmt\/meetings$/)
        await qa.shot('back-ve-trang-truoc')
      },
    },
    {
      id: 'AC30',
      role: QA.a.email,
      devices: ['desktop', 'phone'],
      title: 'Đánh dấu đã đọc bị lỗi khi bấm vào thông báo: vẫn tới được cuộc họp, mở lại chuông thì dòng còn chưa đọc',
      steps: ['Chặn POST /api/notifications/{id}/read (500), giữ GET /{id}/target', 'qa-fe13-a@ bấm dòng chưa đọc đầu tiên', 'Mở lại chuông'],
      expected: 'Trình duyệt vẫn tới /mgmt/meetings/{id}, trang cuộc họp hiện; mở lại chuông: dòng đó còn chưa đọc (UC-78, BR-033, BR-034)',
      async run(qa) {
        const { m, n } = await freshForA(qa)
        await home(qa)
        await qa.page.route(
          (u) => /\/api\/notifications\/[^/]+\/read$/.test(u.pathname),
          (route) => route.fulfill({ status: 500, contentType: 'application/json', body: '{}' }),
        )
        await openPanel(qa)
        await press(qa, rows(qa).first().locator('button.notif-open'))
        await qa.expect(qa.page).toHaveURL(new RegExp(`/mgmt/meetings/${m.id}$`))
        await qa.expect(qa.page.getByRole('heading', { level: 1, name: m.title })).toBeVisible()
        await qa.shot('van-toi-trang-cuoc-hop')
        await openPanel(qa)
        await qa.expect(rows(qa).first()).toHaveClass(/notif-item-unread/)
        await qa.shot('mo-lai-chuong-con-chua-doc')
        qa.expect((await latest('a', 20)).find((x) => x.id === n.id)?.isRead).toBe(false)
      },
    },
    {
      id: 'AC31',
      role: QA.a.email,
      devices: ['desktop'],
      title: 'Realtime: thông báo đẩy hai lần chỉ hiện một dòng/một toast; mất kết nối hub rồi nối lại thì đọc lại số và danh sách, có cả thông báo tới lúc mất mạng',
      steps: [
        'Đặt proxy trước WebSocket của hub, gửi mỗi tin hai lần; panel đóng, thuky@ dời lịch',
        'Mở chuông, so với API',
        'Cắt WebSocket (không cho nối lại), thuky@ dời lịch hai lần, rồi cho nối lại',
      ],
      expected:
        'Một toast duy nhất và badge +1 dù tin đến hai lần; danh sách khớp API từng dòng; sau khi nối lại, không tải lại trang, badge và đầu danh sách có cả hai thông báo đến lúc mất kết nối (UC-76)',
      async run(qa) {
        const m = await mainMeeting(qa)
        const hub = await hubProxy(qa, { twice: true })
        await home(qa)
        await expect.poll(() => hub.state.opened, { timeout: 20_000 }).toBeGreaterThan(0)
        await qa.page.waitForTimeout(1500)
        await qa.page.evaluate(() => {
          window.__qaNoReload = true
        })
        const before = await unread('a')
        await qa.expect(bell(qa)).toHaveAttribute('aria-label', bellLabel(before))
        await reschedule(qa, m)
        await qa.expect(bell(qa)).toHaveAttribute('aria-label', bellLabel(before + 1), { timeout: 20_000 })
        await qa.page.waitForTimeout(1500)
        qa.expect(await qa.page.locator('.toast-stack').getByText('Cuộc họp đã dời lịch —', { exact: false }).count(), 'toasts for one notification pushed twice').toBe(1)
        await qa.shot('mot-toast-du-day-hai-lan')
        await openPanel(qa)
        qa.expect(await panelTexts(qa)).toEqual(apiTexts((await latest('a', 20)).slice(0, await rows(qa).count())))
        await qa.expect(rows(qa).first()).toContainText(m.title)
        await qa.shot('panel-khop-api')
        // The connection drops and stays down while two notifications arrive.
        hub.drop()
        await qa.page.waitForTimeout(1500)
        await reschedule(qa, m)
        await reschedule(qa, m)
        await qa.page.waitForTimeout(3000)
        qa.expect(await unread('a')).toBe(before + 3)
        await qa.expect(bell(qa)).toHaveAttribute('aria-label', bellLabel(before + 1))
        await qa.shot('mat-ket-noi-chua-thay-hai-thong-bao-moi')
        hub.restore()
        await qa.expect(bell(qa)).toHaveAttribute('aria-label', bellLabel(before + 3), { timeout: 60_000 })
        await expect
          .poll(async () => (await panelTexts(qa)).join('\n'), { timeout: 30_000 })
          .toBe(apiTexts((await latest('a', 20)).slice(0, await rows(qa).count())).join('\n'))
        qa.expect(await qa.page.evaluate(() => window.__qaNoReload), 'the page reloaded').toBe(true)
        await qa.shot('noi-lai-co-du-thong-bao')
      },
    },
    {
      id: 'AC32',
      role: QA.b.email,
      devices: ['desktop'],
      title: 'Quản trị đổi vai trò giữa phiên: toast "Quyền truy cập của bạn vừa được cập nhật." và tên vai trò đổi theo, không phải đăng nhập lại',
      steps: ['qa-fe13-b@ đang mở /mgmt/meetings', 'quantri@ đổi vai trò của qa-fe13-b@ (PUT /api/users/{id})', 'Đổi trả lại Thành viên'],
      expected: 'Toast info "Quyền truy cập của bạn vừa được cập nhật."; vai trò ở góc dưới thanh bên đổi sang vai trò mới, vẫn ở trang cũ, không về /login',
      async run(qa) {
        const roles = (await get('admin', '/api/roles')).body
        const member = roles.find((r) => r.isBuiltIn && r.isDefault)
        const next = roles.find((r) => r.id !== member.id)
        const user = (await get('admin', '/api/users')).body.find((u) => u.email === QA.b.email)
        const putRole = (roleId) => put('admin', `/api/users/${user.id}`, { fullName: user.fullName, email: user.email, roleId })
        const hub = qa.page.waitForEvent('websocket', { predicate: (ws) => ws.url().includes('/hubs/notifications'), timeout: 20_000 })
        await home(qa)
        const ws = await hub
        await ws.waitForEvent('framereceived', { timeout: 20_000 })
        await qa.page.waitForTimeout(1000)
        const roleLabel = qa.page.locator('.account-summary-role').first()
        await qa.expect(roleLabel).toHaveText(member.name)
        try {
          ok(qa, await putRole(next.id), 200, 'đổi vai trò')
          await qa.expect(toast(qa, 'Quyền truy cập của bạn vừa được cập nhật.')).toBeVisible({ timeout: 20_000 })
          await qa.expect(roleLabel).toHaveText(next.name)
          await qa.expect(qa.page).toHaveURL(/\/mgmt\/meetings$/)
          await qa.shot('toast-quyen-va-vai-tro-moi')
        } finally {
          await putRole(member.id)
        }
      },
    },
    {
      id: 'AC33',
      role: QA.b.email,
      devices: ['desktop'],
      title: 'Nhắc họp: người thêm sau khi cửa sổ 15 phút đã mở vẫn được chuông nhắc, nhưng không có thêm thư "sắp bắt đầu"',
      steps: [
        'thuky@ tạo và công bố cuộc họp bắt đầu sau 10 phút (qa-fe13-a@ dự họp)',
        'Chờ vòng nhắc (≤ 5 giây) cho qa-fe13-a@',
        'thuky@ thêm qa-fe13-b@ vào thành phần, chờ vòng nhắc',
        'Đọc hàng đợi thư trong database (chỉ đọc)',
      ],
      expected:
        'qa-fe13-b@ nhận "Bạn được mời họp" rồi "Cuộc họp sắp bắt đầu" trên chuông; hàng đợi thư có thư mời của qa-fe13-b@ nhưng không có thư "Cuộc họp sắp bắt đầu" cho qa-fe13-b@ (UC-76, PO 15/11/2026)',
      async run(qa) {
        const c = checker(qa)
        const start = Date.now() + 10 * MINUTE
        const res = await post('secretary', '/api/meetings', {
          title: qa.name(`FE-13 Nhắc họp ${rand()}`),
          plannedStartTime: iso(start),
          plannedEndTime: iso(start + 45 * MINUTE),
          mode: 'InPerson',
          location: 'Phòng họp QA',
          onlineLink: null,
          sensitivity: 'Normal',
          description: 'Cuộc họp do bộ kiểm thử QA FE-13 tạo.',
          participants: await roster(),
        })
        const id = ok(qa, res, 201, 'create').meeting.id
        for (const s of STEPS.Published) await step(qa, id, s)
        const poll = async (who, type) => {
          for (let i = 0; i < 12; i += 1) {
            const got = (await latest(who, 30)).filter((n) => n.type === type && n.meetingId === id)
            if (got.length) return got
            await new Promise((r) => setTimeout(r, 2000))
          }
          return []
        }
        const aReminder = await poll('a', 'MeetingStartingSoon')
        c.fact('qa-fe13-a@ nhận một "sắp bắt đầu"', aReminder.length === 1, String(aReminder.length))
        const { etag } = await participants({ id })
        ok(qa, await post('secretary', `/api/meetings/${id}/participants`, { userIds: [await idOf('b')], role: 'Member' }, { 'If-Match': etag }), 201, 'add b')
        const bReminder = await poll('b', 'MeetingStartingSoon')
        c.fact('qa-fe13-b@ nhận chuông "sắp bắt đầu"', bReminder.length === 1, String(bReminder.length))
        c.fact('qa-fe13-b@ nhận chuông lời mời', (await latest('b', 30)).some((n) => n.type === 'MeetingInvited' && n.meetingId === id))
        await new Promise((r) => setTimeout(r, 12_000))
        const psql = (sql) => {
          try {
            return execFileSync('docker', ['exec', 'trustcabinet-postgres', 'psql', '-U', 'trustcabinet', '-d', 'trustcabinet', '-At', '-F', '|', '-c', sql], { encoding: 'utf8' })
          } catch (error) {
            return qa.inconclusive(`Không đọc được database (cần docker, container trustcabinet-postgres): ${error.message.slice(0, 120)}`)
          }
        }
        // Email sending is off on this machine, so an invitation waits as a Pending dispatch rather than an email_messages row.
        const dispatches = psql(
          `select u.email, d.event_kind, d.state from meeting_invitation_dispatches d join meeting_participants p on p.id = d.participant_id join users u on u.id = p.user_id where d.meeting_id = '${id}' order by d.created_at`,
        )
        const mail = psql(`select to_address, subject from email_messages where meeting_id = '${id}' order by created_at`)
        qa.note(`meeting_invitation_dispatches:\n${dispatches}\nemail_messages:\n${mail}`)
        c.fact('thư mời của qa-fe13-b@ đã vào hàng đợi (dispatch Invitation)', dispatches.split('\n').some((l) => l.startsWith(`${QA.b.email}|Invitation`)), dispatches)
        c.fact('không có thư "Cuộc họp sắp bắt đầu" cho qa-fe13-b@', !mail.split('\n').some((l) => l.startsWith(QA.b.email) && l.includes('sắp bắt đầu')), mail)
        await home(qa)
        await openPanel(qa)
        await qa.shot('chuong-b-moi-va-nhac')
        await post('secretary', `/api/meetings/${id}/transitions`, { action: 'Cancel', reason: 'QA FE-13: dọn dẹp.' })
        c.done()
      },
    },
  ],
}

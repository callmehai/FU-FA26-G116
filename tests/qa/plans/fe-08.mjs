// FE-08 — Phòng họp trực tuyến (QA subtask SCRUM-195): UC-37 join (lobby, recording notice, device check), UC-38 leave,
// UC-39a microphone, UC-39b camera, UC-39c screen sharing, UC-39d device pickers, UC-40 participants panel, UC-41 Chair
// mutes another person, UC-42 in-room chat, UC-78 floating window, UC-79 raise hand (as far as the room shows it), and
// the rules they cite: BR-143, BR-323 → BR-332, BR-336 → BR-338.
//
// Criteria come from docs/01-requirements/05-use-case-specifications.md (UC-37 → UC-42, UC-78, UC-79),
// 06-business-rules.md, docs/03-api/09-phong-hop-truc-tuyen.md, docs/03-api/00-danh-muc-ma-thong-bao.md (MSG117, MSG123,
// MSG280, MSG282, MSG328 → MSG343, MSG350), ADR-0056 (what the recording notice says per sensitivity) and the SCRUM-41
// comments (the room opens only while the meeting is InProgress, PO 26/10; UC-41 is mute only — removing someone from
// the room and muting everyone are out of this version; Chair hand-over is UC-73 on the roster, and the room follows it,
// BR-329).
//
// Two people in one room are two browser contexts inside one criterion (`person()` below), each signed in as its own
// seed account, both on Chromium's fake camera and microphone. What needs a real voice, a second machine, a phone camera,
// a Guest's OTP mailbox, a LiveKit outage or 50 people is INCONCLUSIVE with the manual step for the rehearsal (SCRUM-205).
//
// LiveKit runs on another origin locally (ws://localhost:7880, from LiveKit:PublicUrl): `allowHosts: ['localhost']`
// lets the room connect; the outside-request guard matches on the host name only, without the port.
//
// Safe on a shared local dev database: every meeting is created here through the API, titled "[QA] FE-08 …", twelve
// weeks ahead, organised by thuky@ (Secretary) with chutoa@ as Chair and approver and thanhvien@ as Member, and started
// early by the Chair. Meetings whose state no criterion changes are reused across runs ("[QA] FE-08 dùng lại …");
// a criterion that ends a meeting, starts one, removes someone or hands the Chair over gets its own meeting and ends it.
import { readdirSync, statSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { DEVICES } from '../lib/qa.mjs'

const PASSWORD = process.env.QA_PASSWORD ?? 'TrustCabinet@2026'
const SEED = {
  admin: 'quantri@trustcabinet.local',
  chair: 'chutoa@trustcabinet.local',
  secretary: 'thuky@trustcabinet.local',
  member: 'thanhvien@trustcabinet.local',
}
const args = process.argv.slice(2)
const BASE = (() => {
  const i = args.indexOf('--base')
  return (i >= 0 ? args[i + 1] : (process.env.QA_BASE ?? 'http://localhost:5173')).replace(/\/$/, '')
})()

// ---------------------------------------------------------------------------------------------------------------------
// API: a fetch client per account (it can send If-Match, which the runner's client cannot), tokens cached 5 minutes.

const TOKENS = {}
async function token(who) {
  const email = SEED[who] ?? who
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

const USERS = {}
async function users() {
  for (const who of Object.keys(SEED)) {
    if (USERS[who]) continue
    const me = await get(who, '/api/auth/me')
    if (me.status !== 200) throw new Error(`/api/auth/me as ${who}: HTTP ${me.status}`)
    USERS[who] = { id: me.body.id, name: me.body.fullName }
  }
  return USERS
}

// ---------------------------------------------------------------------------------------------------------------------
// Meetings.

const iso = (ms) => new Date(ms).toISOString().replace(/\.\d{3}Z$/, 'Z')
const MINUTE = 60_000
const DAY = 86_400_000
let slotCounter = Math.floor(Math.random() * 11)
/** A 45-minute slot twelve weeks from today, between 07:30 and 17:30 Vietnam time, varied per meeting. */
function slot() {
  slotCounter += 1
  const day = new Date(Date.now() + 84 * DAY)
  day.setUTCHours(0, 30, 0, 0) // 07:30 in Vietnam
  const start = day.getTime() + ((slotCounter * 55) % 600) * MINUTE + Math.floor(Math.random() * 6) * 5 * MINUTE
  return { plannedStartTime: iso(start), plannedEndTime: iso(start + 45 * MINUTE) }
}

async function meetingBody(title, { mode = 'Online', sensitivity = 'Normal' } = {}) {
  const u = await users()
  return {
    title,
    ...slot(),
    mode,
    location: mode === 'Online' ? null : 'Phòng họp QA',
    onlineLink: null,
    sensitivity,
    description: 'Cuộc họp do bộ kiểm thử QA FE-08 tạo.',
    participants: [
      { userId: u.chair.id, role: 'Chair' },
      { userId: u.secretary.id, role: 'Secretary' },
      { userId: u.member.id, role: 'Member' },
    ],
  }
}

const PATHS = {
  Published: ['submit', 'approve', 'publish'],
  InProgress: ['submit', 'approve', 'publish', 'start'],
  Ended: ['submit', 'approve', 'publish', 'start', 'end'],
  Cancelled: ['submit', 'approve', 'publish', 'cancel'],
}

async function transition(qa, who, id, action, approver = 'chair') {
  const u = await users()
  const body = {
    submit: { action: 'SubmitForApproval', assignedApproverId: u[approver].id },
    approve: { action: 'Approve' },
    publish: { action: 'Publish' },
    start: { action: 'Start' },
    end: { action: 'End' },
    cancel: { action: 'Cancel', reason: 'QA FE-08: huỷ để kiểm phòng họp của cuộc họp đã huỷ.' },
  }[action]
  ok(qa, await post(who, `/api/meetings/${id}/transitions`, body), 200, `${action} by ${who}`)
}

/** Creates a meeting and drives it to `status`. `organizer` creates and publishes; `approver` approves. */
async function createAt(qa, status, title, { organizer = 'secretary', approver = 'chair', ...over } = {}) {
  const created = ok(qa, await post(organizer, '/api/meetings', await meetingBody(title, over)), 201, 'create meeting')
  const id = created.meeting.id
  for (const step of PATHS[status]) {
    const who = step === 'approve' ? approver : ['start', 'end'].includes(step) ? 'chair' : organizer
    await transition(qa, who, id, step, approver)
  }
  const got = ok(qa, await get(organizer, `/api/meetings/${id}`), 200, 'read meeting')
  qa.expect(got.status).toBe(status)
  return got
}

const REUSED = {}
const REUSABLE = {
  live: { title: '[QA] FE-08 dùng lại phòng đang họp (Thường)', status: 'InProgress' },
  secret: { title: '[QA] FE-08 dùng lại phòng Mật (kết hợp)', status: 'InProgress', mode: 'Hybrid', sensitivity: 'Secret' },
  published: { title: '[QA] FE-08 dùng lại chưa bắt đầu', status: 'Published' },
  inPerson: { title: '[QA] FE-08 dùng lại họp trực tiếp', status: 'InProgress', mode: 'InPerson' },
  ended: { title: '[QA] FE-08 dùng lại đã kết thúc', status: 'Ended' },
  cancelled: { title: '[QA] FE-08 dùng lại đã huỷ sau công bố', status: 'Cancelled' },
  adminOrganizer: { title: '[QA] FE-08 dùng lại người tạo ngoài thành phần', status: 'InProgress', organizer: 'admin' },
  adminApprover: { title: '[QA] FE-08 dùng lại người duyệt ngoài thành phần', status: 'InProgress', approver: 'admin' },
}

/** A meeting no criterion changes, found by title (created the first time). */
async function reused(qa, key) {
  if (REUSED[key]) return REUSED[key]
  const { title, status, ...over } = REUSABLE[key]
  const organizer = over.organizer ?? 'secretary'
  const list = await get(organizer, `/api/meetings?q=${encodeURIComponent(title)}&status=${status}&pageSize=20`)
  const hit = (list.body?.items ?? []).find((m) => m.title === title)
  const m = hit ? ok(qa, await get(organizer, `/api/meetings/${hit.id}`), 200) : await createAt(qa, status, title, over)
  REUSED[key] = m
  return m
}

/** A meeting of its own for a criterion that changes it; `finish(qa)` ends it if it still runs. */
async function fresh(qa, status, label, over = {}) {
  const m = await createAt(qa, status, `[QA] FE-08 ${label} ${new Date().toISOString().slice(5, 16)}`, over)
  qa.__fresh ??= []
  qa.__fresh.push(m.id)
  return m
}
async function endFresh(qa) {
  for (const id of qa.__fresh ?? []) {
    const m = await get('secretary', `/api/meetings/${id}`)
    if (m.body?.status !== 'InProgress') continue
    // The Chair may have been handed over; end it as whoever is Chair now.
    for (const who of ['chair', 'member', 'secretary']) {
      const res = await post(who, `/api/meetings/${id}/transitions`, { action: 'End' })
      if (res.status === 200) break
    }
  }
}

// Roster changes need If-Match (the participants revision).
async function roster(m) {
  const res = await get('secretary', `/api/meetings/${m.id}/participants?pageSize=100`)
  return { items: res.body.items, etag: res.headers.etag ?? `"participants-${res.body.revision}"` }
}

// ---------------------------------------------------------------------------------------------------------------------
// Screen helpers.

async function noSidewaysScroll(qa, page = qa.page) {
  const sideways = await page.evaluate(() => document.documentElement.scrollWidth - innerWidth)
  qa.expect(sideways, 'the page scrolls sideways').toBeLessThanOrEqual(0)
}
async function noRawCodes(qa, page = qa.page) {
  const text = await page.locator('body').innerText()
  qa.expect(text, 'a raw MSG code is shown to the user').not.toMatch(/MSG\d+/)
}
async function press(qa, locator) {
  if (qa.device === 'phone') await locator.tap()
  else await locator.click()
}
const toast = (page, text) => page.locator('.toast-stack').getByText(text)
const lobbyHeading = (page) => page.getByRole('heading', { level: 1, name: 'Sẵn sàng tham gia?' })
const joinButton = (page) => page.getByRole('button', { name: 'Vào phòng', exact: true })
const controls = (page) => page.locator('footer.room-controls')
const leaveButton = (page) => controls(page).getByRole('button', { name: 'Rời phòng' })

/** The person's devices: mic and camera as given, remembered in the browser before the page loads (BR-331). */
async function setPrefs(context, prefs) {
  await context.addInitScript((p) => {
    try {
      const saved = JSON.parse(localStorage.getItem('trustcabinet.call.prefs') ?? '{}')
      if (!sessionStorage.getItem('qa.prefs.set')) {
        localStorage.setItem('trustcabinet.call.prefs', JSON.stringify({ ...saved, ...p }))
        sessionStorage.setItem('qa.prefs.set', '1')
      }
    } catch {
      // ignore
    }
  }, prefs)
}

/** Opens the room page and waits for the lobby. */
async function openLobby(qa, m, page = qa.page) {
  await page.goto(`/mgmt/meetings/${m.id}/room`)
  await qa.expect(lobbyHeading(page)).toBeVisible({ timeout: 15_000 })
}

/** From the lobby into the room; waits until the call controls and the own tile show. */
async function joinRoom(qa, page = qa.page) {
  await joinButton(page).click()
  await qa.expect(leaveButton(page)).toBeVisible({ timeout: 20_000 })
}

/** How many people the room header says are in the room. */
async function headerCount(page) {
  const text = await page.locator('.room-count').innerText()
  return Number(text.match(/\d+/)?.[0] ?? NaN)
}

/**
 * A second person in the same criterion: a new browser context signed in as `who`, with the fake camera and microphone,
 * on its own desktop window. Closed by `closePeople(qa)` at the end of the criterion.
 */
async function person(qa, who, { device = 'desktop', mic = false, cam = false } = {}) {
  const context = await qa.context.browser().newContext({
    ...DEVICES[device],
    baseURL: qa.base,
    locale: 'vi-VN',
    timezoneId: 'Asia/Ho_Chi_Minh',
    permissions: ['microphone', 'camera'],
  })
  const res = await context.request.post(`${qa.base}/api/auth/login`, { data: { email: SEED[who] ?? who, password: PASSWORD } })
  if (!res.ok()) throw new Error(`sign-in as ${who} failed: HTTP ${res.status()}`)
  await context.addInitScript(() => localStorage.setItem('trustcabinet.signedIn', '1'))
  await setPrefs(context, { mic, cam })
  const page = await context.newPage()
  const errors = []
  page.on('pageerror', (e) => errors.push(e.message))
  qa.__people ??= []
  qa.__people.push({ context, who, errors })
  return page
}
async function closePeople(qa) {
  for (const p of qa.__people ?? []) {
    if (p.errors.length) qa.note(`Trang của ${p.who} (người thứ hai) có lỗi JavaScript: ${p.errors.join(' | ').slice(0, 300)}`)
    await p.context.close().catch(() => undefined)
  }
  qa.__people = []
}

/** Where the runner writes this run's evidence: the folder holding the shot just taken. */
const here = dirname(fileURLToPath(import.meta.url))
function outDirOf(shotName) {
  const root = join(here, '..', 'out', 'SCRUM-195')
  const dirs = readdirSync(root)
    .map((d) => join(root, d))
    .filter((d) => statSync(d).isDirectory())
    .sort((a, b) => statSync(b).mtimeMs - statSync(a).mtimeMs)
  return dirs.find((d) => readdirSync(d).includes(shotName)) ?? dirs[0]
}
/**
 * Evidence of the second person's window: saved next to the criterion's own shots (the runner only shoots the main
 * page), named after the main shot taken just before, and listed in a note so the report points to it.
 */
async function shotOf(qa, page, slug) {
  const main = await qa.shot(`${slug} (cửa sổ chính)`)
  const name = main.replace(/\.png$/, `-nguoi-thu-hai.png`)
  await page.screenshot({ path: join(outDirOf(main), name), animations: 'disabled', caret: 'hide' })
  qa.note(`Ảnh cửa sổ người thứ hai: ${name}`)
}

/** Runs a criterion body, then always closes the extra people and ends the fresh meetings it made. */
const guarded = (body) => async (qa) => {
  try {
    await body(qa)
  } finally {
    await closePeople(qa)
    await endFresh(qa).catch(() => undefined)
  }
}

/** Runs every API check, then fails once with all the wrong ones. */
function checker(qa) {
  const wrong = []
  return {
    check(label, res, status, code) {
      const good = res.status === status && (code === undefined || res.body?.code === code)
      if (!good) wrong.push(`${label}: want ${status}${code ? ` ${code}` : ''}, got ${res.status} ${JSON.stringify(res.body).slice(0, 200)}`)
      return good
    },
    fact(label, good, detail = '') {
      if (!good) wrong.push(`${label}${detail ? `: ${detail}` : ''}`)
    },
    done() {
      qa.expect(wrong, wrong.join('\n')).toEqual([])
    },
  }
}

const NOTICE_NORMAL = 'lời thoại (chữ, không có tiếng) có thể được gửi tới dịch vụ AI bên ngoài'
const NOTICE_RESTRICTED = 'Dự thảo biên bản do máy AI của nhóm viết; lời thoại không ra khỏi hệ thống.'

// ---------------------------------------------------------------------------------------------------------------------

export default {
  ticket: 'SCRUM-195',
  title: 'FE-08 Phòng họp trực tuyến: màn chờ, vào/rời phòng, micro/camera, người dự, chủ toạ tắt mic, chat, trạng thái phòng, quyền vào',
  devices: ['desktop'],
  allowHosts: ['localhost'],
  criteria: [
    {
      id: 'AC1',
      role: 'chair',
      devices: ['desktop', 'phone'],
      title: 'Chủ toạ mở phòng từ chi tiết cuộc họp đang diễn ra: màn chờ có xem trước, micro bật, camera tắt, thông báo ghi âm',
      steps: [
        'Cuộc họp Trực tuyến, mức Thường, đang diễn ra; chutoa@ là Chủ toạ',
        'Mở chi tiết cuộc họp, bấm "Vào phòng họp"',
      ],
      expected:
        'Màn chờ "Sẵn sàng tham gia?" với tên cuộc họp, khung xem trước "Camera đang tắt", nút Tắt micro (micro bật) và Bật camera (camera tắt), ô chọn Micro/Camera, dòng "Cuộc họp được ghi âm để lập biên bản · tự xoá sau N ngày" (N từ server), nút Vào phòng và "Quay lại trang cuộc họp"; không cuộn ngang trên điện thoại (UC-37 bước 1–3, BR-331)',
      run: guarded(async (qa) => {
        const m = await reused(qa, 'live')
        const state = ok(qa, await get('chair', `/api/meetings/${m.id}/call`), 200, 'GET call')
        await qa.goto(`/mgmt/meetings/${m.id}`)
        await qa.expect(qa.page.getByRole('heading', { level: 1, name: m.title })).toBeVisible()
        const go = qa.page.getByRole('link', { name: 'Vào phòng họp' })
        await qa.expect(go).toBeVisible()
        await qa.shot('chi tiet cuoc hop co nut vao phong')
        await press(qa, go)
        await qa.expect(qa.page).toHaveURL(new RegExp(`/mgmt/meetings/${m.id}/room$`))
        await qa.expect(lobbyHeading(qa.page)).toBeVisible({ timeout: 15_000 })
        const side = qa.page.getByRole('region', { name: 'Sẵn sàng tham gia?' })
        await qa.expect(side.getByText(m.title)).toBeVisible()
        const preview = qa.page.getByRole('group', { name: 'Xem trước hình của bạn' })
        await qa.expect(preview.getByText('Camera đang tắt')).toBeVisible()
        await qa.expect(preview.getByRole('button', { name: 'Tắt micro' })).toBeVisible()
        await qa.expect(preview.getByRole('button', { name: 'Bật camera' })).toBeVisible()
        await qa.expect(side.getByText(`Cuộc họp được ghi âm để lập biên bản · tự xoá sau ${state.retentionDays} ngày`)).toBeVisible({ timeout: 10_000 })
        await qa.expect(joinButton(qa.page)).toBeEnabled()
        await qa.expect(qa.page.getByRole('link', { name: 'Quay lại trang cuộc họp' })).toBeVisible()
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
        await qa.shot('man cho')
      }),
    },
    {
      id: 'AC2',
      role: 'member',
      devices: ['desktop', 'phone'],
      title: 'Thông báo ghi âm ở màn chờ nói đúng nơi lời thoại đi theo mức độ mật (ADR-0056); cuộc họp Mật có nhãn Mật',
      steps: [
        'thanhvien@ mở màn chờ của cuộc họp Thường, bấm (i) cạnh thông báo ghi âm',
        'Mở màn chờ của cuộc họp Mật (kết hợp), bấm (i)',
      ],
      expected:
        'Thường: (i) nói lời thoại (chữ) có thể gửi tới dịch vụ AI bên ngoài. Mật: (i) nói dự thảo do máy AI của nhóm viết, lời thoại không ra khỏi hệ thống; màn chờ có nhãn "Mật"',
      run: guarded(async (qa) => {
        const normal = await reused(qa, 'live')
        const secret = await reused(qa, 'secret')
        await openLobby(qa, normal)
        const side = qa.page.getByRole('region', { name: 'Sẵn sàng tham gia?' })
        const tip = side.locator('.prejoin-recording .info-tip')
        await qa.expect(tip).toBeVisible({ timeout: 10_000 })
        qa.expect(await tip.getAttribute('aria-label')).toContain(NOTICE_NORMAL)
        await press(qa, tip)
        await qa.shot('thuong - thong bao ghi am')
        await openLobby(qa, secret)
        await qa.expect(side.locator('.level').filter({ hasText: 'Mật' })).toBeVisible()
        await qa.expect(tip).toBeVisible({ timeout: 10_000 })
        const label = await tip.getAttribute('aria-label')
        qa.expect(label).toContain(NOTICE_RESTRICTED)
        qa.expect(label).not.toContain('dịch vụ AI bên ngoài')
        await press(qa, tip)
        await noSidewaysScroll(qa)
        await qa.shot('mat - thong bao ghi am')
      }),
    },
    {
      id: 'AC3',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Ở màn chờ bật camera thấy hình xem trước, tắt micro; trình duyệt nhớ lựa chọn cho lần sau; ô chọn thiết bị liệt kê thiết bị',
      steps: [
        'thuky@ mở màn chờ của cuộc họp đang diễn ra',
        'Bấm Bật camera, bấm Tắt micro',
        'Mở ô chọn Camera',
        'Tải lại trang',
      ],
      expected:
        'Camera bật: khung xem trước hiện hình, nút thành "Tắt camera". Micro tắt: nút thành "Bật micro". Ô chọn liệt kê camera giả lập. Tải lại: vẫn camera bật, micro tắt (UC-39a/39b/39d, BR-331)',
      run: guarded(async (qa) => {
        const m = await reused(qa, 'live')
        await qa.page.addInitScript(() => {
          if (!sessionStorage.getItem('qa.ac3')) {
            localStorage.removeItem('trustcabinet.call.prefs')
            sessionStorage.setItem('qa.ac3', '1')
          }
        })
        await openLobby(qa, m)
        const preview = qa.page.getByRole('group', { name: 'Xem trước hình của bạn' })
        await press(qa, preview.getByRole('button', { name: 'Bật camera' }))
        await qa.expect(preview.getByRole('button', { name: 'Tắt camera' })).toBeVisible()
        await qa.expect(preview.locator('video')).toBeVisible({ timeout: 10_000 })
        await qa.page.waitForFunction(() => (document.querySelector('.prejoin-preview video')?.videoWidth ?? 0) > 0, null, { timeout: 10_000 })
        await press(qa, preview.getByRole('button', { name: 'Tắt micro' }))
        await qa.expect(preview.getByRole('button', { name: 'Bật micro' })).toBeVisible()
        await qa.shot('camera bat micro tat')
        const pickers = qa.page.locator('.prejoin-devices')
        const camPicker = pickers.getByRole('button', { name: /^Camera:/ })
        await press(qa, camPicker)
        const options = qa.page.getByRole('menu', { name: 'Camera' }).getByRole('menuitemradio')
        await qa.expect(options.first()).toBeVisible()
        const n = await options.count()
        qa.expect(n, 'the camera picker lists at least one camera').toBeGreaterThan(0)
        await qa.shot('o chon camera')
        await qa.page.keyboard.press('Escape')
        await qa.page.reload()
        await qa.expect(lobbyHeading(qa.page)).toBeVisible({ timeout: 15_000 })
        await qa.expect(preview.getByRole('button', { name: 'Tắt camera' })).toBeVisible()
        await qa.expect(preview.getByRole('button', { name: 'Bật micro' })).toBeVisible()
        await noSidewaysScroll(qa)
        await qa.shot('sau khi tai lai van nho')
      }),
    },
    {
      id: 'AC4',
      role: 'member',
      devices: ['desktop', 'phone'],
      title: 'Thành viên vào phòng: lưới hình với ô của mình, thanh điều khiển, không có thông báo thành công; cuộc họp và điểm danh không đổi',
      steps: ['thanhvien@ mở màn chờ của cuộc họp đang diễn ra (micro tắt, camera tắt)', 'Bấm Vào phòng'],
      expected:
        'Phòng mở: đầu phòng có tên cuộc họp và số người trong phòng, ô của mình; nút Bật micro, Bật camera, Rời phòng, Người dự, Chat (máy tính có thêm Chia sẻ màn hình; điện thoại không có). Cuộc họp vẫn Đang diễn ra; điểm danh của thanhvien@ không đổi (UC-37 bước 5–8, BR-324, BR-330, UC-39c AF-2)',
      run: guarded(async (qa) => {
        const m = await reused(qa, 'live')
        const before = ok(qa, await get('secretary', `/api/meetings/${m.id}/attendance?pageSize=100`), 200, 'attendance before')
        const u = await users()
        const mine = (list) => list.items.find((i) => i.displayName === u.member.name)?.attendance
        await setPrefs(qa.context, { mic: false, cam: false })
        await openLobby(qa, m)
        await qa.shot('man cho truoc khi vao')
        await joinRoom(qa)
        await qa.expect(qa.page.locator('.room-title')).toHaveText(m.title)
        await qa.expect.poll(() => headerCount(qa.page), { timeout: 15_000 }).toBeGreaterThanOrEqual(1)
        const c = controls(qa.page)
        await qa.expect(c.getByRole('button', { name: 'Bật micro' })).toBeVisible()
        await qa.expect(c.getByRole('button', { name: 'Bật camera' })).toBeVisible()
        await qa.expect(c.getByRole('button', { name: 'Người dự' })).toBeVisible()
        await qa.expect(c.getByRole('button', { name: 'Chat' })).toBeVisible()
        const share = c.getByRole('button', { name: 'Chia sẻ màn hình' })
        if (qa.device === 'phone') await qa.expect(share).toHaveCount(0)
        else await qa.expect(share).toBeVisible()
        await qa.expect(qa.page.locator('.toast-stack .toast')).toHaveCount(0)
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
        await qa.shot('trong phong')
        const after = ok(qa, await get('secretary', `/api/meetings/${m.id}/attendance?pageSize=100`), 200, 'attendance after')
        qa.expect(mine(after), 'joining the room changed attendance (BR-330)').toBe(mine(before))
        qa.expect((await get('secretary', `/api/meetings/${m.id}`)).body.status).toBe('InProgress')
      }),
    },
    {
      id: 'AC5',
      role: 'chair',
      devices: ['desktop', 'phone'],
      title: 'Trong phòng bật/tắt micro và camera của mình (cả phím tắt Ctrl+D, Ctrl+E trên máy tính)',
      steps: [
        'chutoa@ vào phòng với micro tắt, camera tắt',
        'Bấm Bật camera, rồi Bật micro',
        'Máy tính: Ctrl+D, Ctrl+E',
      ],
      expected:
        'Bật camera: ô của mình hiện hình, nút thành "Tắt camera". Bật micro: nút thành "Tắt micro". Phím tắt đảo lại cả hai (UC-39a/39b)',
      run: guarded(async (qa) => {
        const m = await reused(qa, 'live')
        await setPrefs(qa.context, { mic: false, cam: false })
        await openLobby(qa, m)
        await joinRoom(qa)
        const c = controls(qa.page)
        await press(qa, c.getByRole('button', { name: 'Bật camera' }))
        await qa.expect(c.getByRole('button', { name: 'Tắt camera' })).toBeVisible({ timeout: 10_000 })
        await qa.page.waitForFunction(
          () => [...document.querySelectorAll('.room-stage video')].some((v) => v.videoWidth > 0),
          null,
          { timeout: 15_000 },
        )
        await press(qa, c.getByRole('button', { name: 'Bật micro' }))
        await qa.expect(c.getByRole('button', { name: 'Tắt micro' })).toBeVisible({ timeout: 10_000 })
        await qa.shot('camera va micro bat')
        if (qa.device === 'desktop') {
          await qa.page.locator('.room-stage').click({ position: { x: 5, y: 5 } })
          // ⌘ on a Mac, Ctrl elsewhere, as the room reads navigator.platform.
          const mod = (await qa.page.evaluate(() => /Mac|iPhone|iPad/.test(navigator.platform))) ? 'Meta' : 'Control'
          await qa.page.keyboard.press(`${mod}+d`)
          await qa.expect(c.getByRole('button', { name: 'Bật micro' })).toBeVisible({ timeout: 10_000 })
          await qa.page.keyboard.press(`${mod}+e`)
          await qa.expect(c.getByRole('button', { name: 'Bật camera' })).toBeVisible({ timeout: 10_000 })
          await qa.shot('phim tat tat ca hai')
        }
        await noSidewaysScroll(qa)
      }),
    },
    {
      id: 'AC6',
      role: 'member',
      devices: ['desktop', 'phone'],
      title: 'Hai người trong phòng: panel Người dự liệt kê người đang kết nối theo vai trò, đánh dấu "(Bạn)", cập nhật khi có người vào/ra',
      steps: [
        'chutoa@ (cửa sổ thứ hai) vào phòng',
        'thanhvien@ vào phòng, mở Người dự',
        'chutoa@ rời phòng',
      ],
      expected:
        'Panel "Người dự (2)": Chủ toạ đứng trước (Nguyễn Hoàng Thanh Hương · Chủ toạ), rồi "Lê An (Bạn) · Thành viên"; micro của từng người hiện trạng thái; thành viên không có nút Tắt mic. Khi chủ toạ rời, danh sách còn 1 không cần tải lại (UC-40, BR-330)',
      run: guarded(async (qa) => {
        const m = await reused(qa, 'live')
        const u = await users()
        const chair = await person(qa, 'chair')
        await openLobby(qa, m, chair)
        await joinRoom(qa, chair)
        await setPrefs(qa.context, { mic: false, cam: false })
        await openLobby(qa, m)
        await joinRoom(qa)
        await press(qa, controls(qa.page).getByRole('button', { name: 'Người dự' }))
        const panel = qa.page.getByRole('complementary', { name: 'Người dự' })
        await qa.expect(panel.getByRole('heading', { name: /^Người dự \(\d+\)$/ })).toBeVisible()
        const rows = panel.locator('li.room-person')
        await qa.expect(rows.filter({ hasText: u.chair.name })).toBeVisible({ timeout: 20_000 })
        await qa.expect(rows.filter({ hasText: `${u.member.name} (Bạn)` })).toBeVisible()
        await qa.expect(rows.filter({ hasText: u.chair.name }).getByText('Chủ toạ')).toBeVisible()
        await qa.expect(rows.filter({ hasText: u.member.name }).getByText('Thành viên')).toBeVisible()
        const names = await rows.locator('strong').allInnerTexts()
        qa.expect(names.indexOf(u.chair.name), 'the Chair is listed before the Member').toBeLessThan(
          names.findIndex((n) => n.startsWith(u.member.name)),
        )
        await qa.expect(panel.getByRole('button', { name: 'Tắt mic' })).toHaveCount(0)
        await noSidewaysScroll(qa)
        await qa.shot('panel nguoi du hai nguoi')
        const countWithChair = await rows.count()
        await leaveButton(chair).click()
        await chair.getByRole('dialog').getByRole('button', { name: 'Rời phòng' }).click()
        await qa.expect(rows.filter({ hasText: u.chair.name })).toHaveCount(0, { timeout: 20_000 })
        await qa.expect(rows).toHaveCount(countWithChair - 1)
        await qa.shot('chu toa da roi')
      }),
    },
    {
      id: 'AC7',
      role: 'member',
      devices: ['desktop', 'phone'],
      title: 'Chủ toạ tắt mic một người trong phòng: hai bên nhận đúng câu báo, người bị tắt tự bật lại được',
      steps: [
        'thanhvien@ vào phòng với micro bật',
        'chutoa@ (cửa sổ thứ hai) vào phòng, mở Người dự, bấm "Tắt mic" ở dòng của Lê An',
        'thanhvien@ bấm Bật micro',
      ],
      expected:
        'Chủ toạ: toast "Đã tắt micro của Lê An." (MSG342). Thành viên: toast "Chủ toạ đã tắt micro của bạn. Bạn có thể bật lại khi cần phát biểu." (MSG339), nút thành Bật micro; bật lại được và chủ toạ thấy micro bật lại, nút Tắt mic hiện lại (UC-41, BR-325)',
      run: guarded(async (qa) => {
        const m = await reused(qa, 'live')
        const u = await users()
        await setPrefs(qa.context, { mic: true, cam: false })
        await openLobby(qa, m)
        await joinRoom(qa)
        await qa.expect(controls(qa.page).getByRole('button', { name: 'Tắt micro' })).toBeVisible({ timeout: 15_000 })
        const chair = await person(qa, 'chair')
        await openLobby(qa, m, chair)
        await joinRoom(qa, chair)
        await controls(chair).getByRole('button', { name: 'Người dự' }).click()
        const row = chair.getByRole('complementary', { name: 'Người dự' }).locator('li.room-person').filter({ hasText: u.member.name })
        const mute = row.getByRole('button', { name: 'Tắt mic' })
        await qa.expect(mute).toBeVisible({ timeout: 20_000 })
        await mute.click()
        await qa.expect(toast(chair, `Đã tắt micro của ${u.member.name}.`)).toBeVisible({ timeout: 10_000 })
        await qa.expect(row.locator('.room-person-mic.is-muted')).toBeVisible({ timeout: 10_000 })
        await qa.expect(toast(qa.page, 'Chủ toạ đã tắt micro của bạn. Bạn có thể bật lại khi cần phát biểu.')).toBeVisible({ timeout: 10_000 })
        await qa.expect(controls(qa.page).getByRole('button', { name: 'Bật micro' })).toBeVisible()
        await shotOf(qa, chair, 'bi chu toa tat mic')
        await press(qa, controls(qa.page).getByRole('button', { name: 'Bật micro' }))
        await qa.expect(controls(qa.page).getByRole('button', { name: 'Tắt micro' })).toBeVisible({ timeout: 10_000 })
        await qa.expect(row.getByRole('button', { name: 'Tắt mic' })).toBeVisible({ timeout: 10_000 })
        await noSidewaysScroll(qa)
        await qa.shot('tu bat lai micro')
      }),
    },
    {
      id: 'AC8',
      role: 'chair',
      devices: ['desktop', 'phone'],
      title: 'Chat trong phòng: tin tới người đang trong phòng, đếm chưa đọc, đếm ký tự, Shift+Enter xuống dòng, người vào sau không thấy tin cũ',
      steps: [
        'chutoa@ và thanhvien@ (cửa sổ thứ hai) cùng trong phòng; chủ toạ để panel Chat đóng',
        'thanhvien@ gửi một tin',
        'chủ toạ mở Chat, gõ hai dòng (Shift+Enter) rồi Enter',
        'thuky@ (cửa sổ thứ ba) vào phòng sau, mở Chat',
      ],
      expected:
        'Nút Chat của chủ toạ hiện số 1; panel có câu "Tin nhắn chỉ hiện với người đang trong phòng và không được lưu lại sau cuộc họp.", tin của Lê An kèm giờ; đếm "n/1000"; tin của chủ toạ hiện "Bạn", ô nhập trống, đếm về 0/1000; thuky@ vào sau thấy "Chưa có tin nhắn nào." (UC-42, BR-327)',
      run: guarded(async (qa) => {
        const m = await reused(qa, 'live')
        const u = await users()
        await setPrefs(qa.context, { mic: false, cam: false })
        await openLobby(qa, m)
        await joinRoom(qa)
        const member = await person(qa, 'member')
        await openLobby(qa, m, member)
        await joinRoom(qa, member)
        const stamp = Date.now().toString(36)
        await controls(member).getByRole('button', { name: 'Chat' }).click()
        const memberBox = member.getByRole('textbox', { name: 'Tin nhắn' })
        await memberBox.fill(`Chào chủ toạ ${stamp}`)
        await memberBox.press('Enter')
        await qa.expect(member.locator('.room-chat-message.is-mine').filter({ hasText: stamp })).toBeVisible({ timeout: 10_000 })
        const chatButton = controls(qa.page).getByRole('button', { name: 'Chat' })
        await qa.expect(chatButton.locator('.room-badge')).toHaveText('1', { timeout: 15_000 })
        await qa.shot('dem tin chua doc')
        await press(qa, chatButton)
        const panel = qa.page.getByRole('complementary', { name: 'Chat' })
        await qa.expect(panel.getByText('Tin nhắn chỉ hiện với người đang trong phòng và không được lưu lại sau cuộc họp.')).toBeVisible()
        const theirs = panel.locator('.room-chat-message').filter({ hasText: stamp })
        await qa.expect(theirs.getByText(u.member.name)).toBeVisible()
        await qa.expect(theirs.locator('time')).toHaveText(/^\d{2}:\d{2}$/)
        const box = panel.getByRole('textbox', { name: 'Tin nhắn' })
        await box.click()
        await box.pressSequentially('Dòng một')
        await box.press('Shift+Enter')
        await box.pressSequentially(`Dòng hai ${stamp}`)
        qa.expect(await box.inputValue()).toBe(`Dòng một\nDòng hai ${stamp}`)
        await qa.expect(panel.getByText(`${`Dòng một\nDòng hai ${stamp}`.length}/1000`)).toBeVisible()
        await qa.shot('dang go hai dong')
        await box.press('Enter')
        const mine = panel.locator('.room-chat-message.is-mine').filter({ hasText: `Dòng hai ${stamp}` })
        await qa.expect(mine.getByText('Bạn', { exact: true })).toBeVisible({ timeout: 10_000 })
        await qa.expect(box).toHaveValue('')
        await qa.expect(panel.getByText('0/1000')).toBeVisible()
        await qa.expect(member.locator('.room-chat-message').filter({ hasText: `Dòng hai ${stamp}` })).toBeVisible({ timeout: 10_000 })
        await noSidewaysScroll(qa)
        await qa.shot('da gui')
        const late = await person(qa, 'secretary')
        await openLobby(qa, m, late)
        await joinRoom(qa, late)
        await controls(late).getByRole('button', { name: 'Chat' }).click()
        await qa.expect(late.getByRole('complementary', { name: 'Chat' }).getByText('Chưa có tin nhắn nào.')).toBeVisible()
        await shotOf(qa, late, 'nguoi vao sau khong thay tin cu')
      }),
    },
    {
      id: 'AC9',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Rời phòng hỏi xác nhận: "Ở lại" giữ kết nối, "Rời phòng" về chi tiết cuộc họp, cuộc họp vẫn diễn ra và vào lại được',
      steps: [
        'thuky@ vào phòng; chutoa@ (cửa sổ thứ hai) cũng trong phòng',
        'Bấm Rời phòng, chọn Ở lại',
        'Bấm nút quay lại ở đầu phòng, chọn Rời phòng',
        'Bấm Vào phòng họp lần nữa',
      ],
      expected:
        'Hộp "Rời phòng họp" với câu "Cuộc họp vẫn tiếp tục với những người khác. Bạn có thể vào lại khi cuộc họp còn đang diễn ra.", nút Ở lại / Rời phòng; Ở lại: vẫn trong phòng; Rời phòng: về trang chi tiết, chủ toạ thấy số người giảm, cuộc họp vẫn Đang diễn ra; vào lại được (UC-38, BR-324)',
      run: guarded(async (qa) => {
        const m = await reused(qa, 'live')
        const chair = await person(qa, 'chair')
        await openLobby(qa, m, chair)
        await joinRoom(qa, chair)
        await setPrefs(qa.context, { mic: false, cam: false })
        await openLobby(qa, m)
        await joinRoom(qa)
        await qa.expect.poll(() => headerCount(chair), { timeout: 20_000 }).toBeGreaterThanOrEqual(2)
        const withMe = await headerCount(chair)
        await press(qa, leaveButton(qa.page))
        const dialog = qa.page.getByRole('dialog', { name: 'Rời phòng họp' })
        await qa.expect(dialog).toBeVisible()
        await qa.expect(dialog.getByText('Cuộc họp vẫn tiếp tục với những người khác. Bạn có thể vào lại khi cuộc họp còn đang diễn ra.')).toBeVisible()
        await noSidewaysScroll(qa)
        await qa.shot('hop thoai roi phong')
        await press(qa, dialog.getByRole('button', { name: 'Ở lại' }))
        await qa.expect(dialog).toHaveCount(0)
        await qa.expect(leaveButton(qa.page)).toBeVisible()
        await press(qa, qa.page.getByRole('button', { name: 'Quay lại trang cuộc họp' }))
        await qa.expect(dialog).toBeVisible()
        await press(qa, dialog.getByRole('button', { name: 'Rời phòng' }))
        await qa.expect(qa.page).toHaveURL(new RegExp(`/mgmt/meetings/${m.id}$`), { timeout: 15_000 })
        await qa.expect(qa.page.getByRole('heading', { level: 1, name: m.title })).toBeVisible()
        await qa.expect.poll(() => headerCount(chair), { timeout: 20_000 }).toBe(withMe - 1)
        qa.expect((await get('secretary', `/api/meetings/${m.id}`)).body.status).toBe('InProgress')
        await qa.shot('ve chi tiet cuoc hop')
        await press(qa, qa.page.getByRole('link', { name: 'Vào phòng họp' }))
        await qa.expect(lobbyHeading(qa.page)).toBeVisible({ timeout: 15_000 })
        await joinRoom(qa)
        await qa.shot('vao lai duoc')
      }),
    },
    {
      id: 'AC10',
      role: 'member',
      title: 'Một tài khoản một kết nối: vào từ cửa sổ khác thì cửa sổ cũ báo MSG329 và có nút Vào lại',
      steps: ['thanhvien@ vào phòng', 'Cùng tài khoản vào phòng ở cửa sổ thứ hai'],
      expected:
        'Cửa sổ đầu: "Bạn đã vào phòng này từ tab hoặc thiết bị khác. Tab này đã rời phòng." với nút Vào lại và Về trang cuộc họp (UC-38 EX-2, BR-328)',
      run: guarded(async (qa) => {
        const m = await reused(qa, 'live')
        await setPrefs(qa.context, { mic: false, cam: false })
        await openLobby(qa, m)
        await joinRoom(qa)
        const again = await person(qa, 'member')
        await openLobby(qa, m, again)
        await joinRoom(qa, again)
        await qa.expect(
          qa.page.getByRole('heading', { name: 'Bạn đã vào phòng này từ tab hoặc thiết bị khác. Tab này đã rời phòng.' }),
        ).toBeVisible({ timeout: 20_000 })
        await qa.expect(qa.page.getByRole('button', { name: 'Vào lại' })).toBeVisible()
        await qa.expect(qa.page.getByRole('link', { name: 'Về trang cuộc họp' })).toBeVisible()
        await qa.shot('cua so cu bi dua ra')
      }),
    },
    {
      id: 'AC11',
      role: 'member',
      devices: ['desktop', 'phone'],
      title: 'Chủ toạ kết thúc cuộc họp khi đang có người trong phòng: phòng đóng, báo MSG331, không có Vào lại',
      steps: ['thanhvien@ vào phòng của một cuộc họp riêng đang diễn ra', 'Chủ toạ kết thúc cuộc họp (API)'],
      expected:
        'Thành viên bị đưa ra: "Cuộc họp đã kết thúc, phòng họp đã đóng." với "Về trang cuộc họp", không có nút Vào lại; mở lại link phòng vẫn báo câu đó (UC-38 EX-1, BR-143)',
      run: guarded(async (qa) => {
        const m = await fresh(qa, 'InProgress', 'kết thúc khi đang trong phòng')
        await setPrefs(qa.context, { mic: false, cam: false })
        await openLobby(qa, m)
        await joinRoom(qa)
        await qa.expect.poll(() => headerCount(qa.page), { timeout: 15_000 }).toBeGreaterThanOrEqual(1)
        await qa.shot('dang trong phong')
        ok(qa, await post('chair', `/api/meetings/${m.id}/transitions`, { action: 'End' }), 200, 'End')
        const closed = qa.page.getByRole('heading', { name: 'Cuộc họp đã kết thúc, phòng họp đã đóng.' })
        await qa.expect(closed).toBeVisible({ timeout: 40_000 })
        await qa.expect(qa.page.getByRole('button', { name: 'Vào lại' })).toHaveCount(0)
        await qa.expect(qa.page.getByRole('link', { name: 'Về trang cuộc họp' })).toBeVisible()
        await noSidewaysScroll(qa)
        await qa.shot('phong da dong')
        await qa.goto(`/mgmt/meetings/${m.id}/room`)
        await qa.expect(closed).toBeVisible({ timeout: 15_000 })
        await qa.expect(joinButton(qa.page)).toHaveCount(0)
      }),
    },
    {
      id: 'AC12',
      role: 'member',
      title: 'Người bị gỡ khỏi thành phần khi đang trong phòng bị đưa ra với MSG330, không vào lại được',
      steps: ['thanhvien@ vào phòng của một cuộc họp riêng đang diễn ra', 'Thư ký gỡ thanhvien@ khỏi thành phần (API)'],
      expected:
        'Thành viên bị đưa ra: "Bạn không còn trong danh sách tham dự phòng họp này." không có Vào lại, nút "Về danh sách cuộc họp" (không có "Về trang cuộc họp", trang đó giờ là 404 với họ) mở danh sách cuộc họp; GET …/call và POST …/call/join của thanhvien@ trả 404; mở lại link phòng là trang "Không tìm thấy trang" (UC-38 EX-3, UC-37 EX-4, BR-101)',
      run: guarded(async (qa) => {
        const m = await fresh(qa, 'InProgress', 'gỡ người khi đang trong phòng')
        const u = await users()
        await setPrefs(qa.context, { mic: false, cam: false })
        await openLobby(qa, m)
        await joinRoom(qa)
        await qa.page.waitForTimeout(3000) // let LiveKit report the join to the backend (webhook) before the removal
        const { items, etag } = await roster(m)
        const row = items.find((p) => p.userId === u.member.id)
        ok(qa, await del('secretary', `/api/meetings/${m.id}/participants/${row.id}`, { 'If-Match': etag }), 200, 'remove member')
        await qa.expect(qa.page.getByRole('heading', { name: 'Bạn không còn trong danh sách tham dự phòng họp này.' })).toBeVisible({ timeout: 40_000 })
        await qa.expect(qa.page.getByRole('button', { name: 'Vào lại' })).toHaveCount(0)
        await qa.expect(qa.page.getByRole('link', { name: 'Về trang cuộc họp' })).toHaveCount(0)
        const toList = qa.page.getByRole('link', { name: 'Về danh sách cuộc họp' })
        await qa.expect(toList).toHaveAttribute('href', '/mgmt/meetings')
        await qa.shot('bi dua ra khoi phong')
        const k = checker(qa)
        k.check('GET call after removal', await get('member', `/api/meetings/${m.id}/call`), 404)
        k.check('POST join after removal', await post('member', `/api/meetings/${m.id}/call/join`), 404)
        k.done()
        await qa.goto(`/mgmt/meetings/${m.id}/room`)
        await qa.expect(qa.page.getByRole('heading', { name: 'Không tìm thấy trang' })).toBeVisible({ timeout: 15_000 })
        await qa.shot('mo lai link phong')
      }),
    },
    {
      id: 'AC13',
      role: 'member',
      title: 'Bàn giao Chủ toạ khi đang trong phòng: vai trò mới hiện ngay trong panel, chủ toạ mới tắt mic được, chủ toạ cũ không (BR-329)',
      steps: [
        'chutoa@ (cửa sổ thứ hai) và thanhvien@ cùng trong phòng của một cuộc họp riêng, thanhvien@ mở Người dự',
        'chutoa@ bàn giao Chủ toạ cho thanhvien@ (API)',
        'chutoa@ cũ thử tắt mic thanhvien@ (API)',
      ],
      expected:
        'Không cần vào lại: dòng của Lê An thành Chủ toạ, dòng của Nguyễn Hoàng Thanh Hương thành Thành viên, và Lê An có nút Tắt mic ở dòng người kia khi micro người đó bật; chủ toạ cũ tắt mic trả 403 MSG123 (UC-40 AF-2, UC-41 EX-1)',
      run: guarded(async (qa) => {
        const m = await fresh(qa, 'InProgress', 'bàn giao chủ toạ trong phòng')
        const u = await users()
        const chair = await person(qa, 'chair', { mic: true })
        await openLobby(qa, m, chair)
        await joinRoom(qa, chair)
        await setPrefs(qa.context, { mic: false, cam: false })
        await openLobby(qa, m)
        await joinRoom(qa)
        await press(qa, controls(qa.page).getByRole('button', { name: 'Người dự' }))
        const rows = qa.page.getByRole('complementary', { name: 'Người dự' }).locator('li.room-person')
        const chairRow = rows.filter({ hasText: u.chair.name })
        const meRow = rows.filter({ hasText: `${u.member.name} (Bạn)` })
        await qa.expect(chairRow.getByText('Chủ toạ')).toBeVisible({ timeout: 20_000 })
        await qa.expect(chairRow.getByRole('button', { name: 'Tắt mic' })).toHaveCount(0)
        await qa.shot('truoc khi ban giao')
        const { items, etag } = await roster(m)
        const outgoing = items.find((p) => p.userId === u.chair.id)
        const incoming = items.find((p) => p.userId === u.member.id)
        ok(
          qa,
          await put(
            'chair',
            `/api/meetings/${m.id}/participants/assignments`,
            {
              changes: [
                { participantId: outgoing.id, role: 'Member' },
                { participantId: incoming.id, role: 'Chair' },
              ],
              chairReplacement: { outgoingParticipantId: outgoing.id, incomingParticipantId: incoming.id, confirmed: true },
            },
            { 'If-Match': etag },
          ),
          200,
          'hand over the Chair',
        )
        await qa.expect(meRow.getByText('Chủ toạ')).toBeVisible({ timeout: 20_000 })
        await qa.expect(chairRow.getByText('Thành viên')).toBeVisible()
        await qa.expect(chairRow.getByRole('button', { name: 'Tắt mic' })).toBeVisible({ timeout: 15_000 })
        await qa.shot('sau khi ban giao')
        const k = checker(qa)
        k.check('former Chair mutes the new Chair', await post('chair', `/api/meetings/${m.id}/call/participants/${u.member.id}/mute`), 403, 'MSG123')
        k.done()
      }),
    },
    {
      id: 'AC14',
      role: 'member',
      devices: ['desktop', 'phone'],
      title: 'Phòng chưa mở / đã đóng: chưa bắt đầu MSG117, đã kết thúc MSG331, đã huỷ MSG350, họp trực tiếp MSG280 — không có nút Vào phòng',
      steps: [
        'thanhvien@ mở /mgmt/meetings/{id}/room của: cuộc họp đã công bố chưa bắt đầu, đã kết thúc, đã huỷ sau công bố, họp trực tiếp đang diễn ra',
        'Mở chi tiết từng cuộc họp',
      ],
      expected:
        'Lần lượt "Phòng họp chỉ mở khi cuộc họp đang diễn ra.", "Cuộc họp đã kết thúc, phòng họp đã đóng.", "Cuộc họp đã huỷ nên phòng họp không mở.", "Cuộc họp này họp trực tiếp nên không có phòng họp trực tuyến."; mỗi màn có tên cuộc họp và "Về trang cuộc họp", không có Vào phòng; chi tiết cuộc họp không có nút Vào phòng họp (UC-37 AF-3, EX-1, EX-2, EX-6)',
      run: guarded(async (qa) => {
        const cases = [
          ['published', 'Phòng họp chỉ mở khi cuộc họp đang diễn ra.'],
          ['ended', 'Cuộc họp đã kết thúc, phòng họp đã đóng.'],
          ['cancelled', 'Cuộc họp đã huỷ nên phòng họp không mở.'],
          ['inPerson', 'Cuộc họp này họp trực tiếp nên không có phòng họp trực tuyến.'],
        ]
        for (const [key, text] of cases) {
          const m = await reused(qa, key)
          await qa.goto(`/mgmt/meetings/${m.id}/room`)
          await qa.expect(qa.page.getByRole('heading', { level: 1, name: text })).toBeVisible({ timeout: 15_000 })
          await qa.expect(qa.page.getByText(m.title)).toBeVisible()
          await qa.expect(qa.page.getByRole('link', { name: 'Về trang cuộc họp' })).toBeVisible()
          await qa.expect(joinButton(qa.page)).toHaveCount(0)
          await noSidewaysScroll(qa)
          await qa.shot(`phong ${key}`)
          await qa.goto(`/mgmt/meetings/${m.id}`)
          await qa.expect(qa.page.getByRole('heading', { level: 1, name: m.title })).toBeVisible()
          await qa.expect(qa.page.getByRole('link', { name: 'Vào phòng họp' })).toHaveCount(0)
        }
      }),
    },
    {
      id: 'AC15',
      role: 'member',
      title: 'Đang chờ ở màn "chỉ mở khi đang diễn ra" thì Chủ toạ bắt đầu cuộc họp: màn chờ tự mở thành "Sẵn sàng tham gia?"',
      steps: ['thanhvien@ mở phòng của một cuộc họp riêng đã công bố, chưa bắt đầu', 'Chủ toạ bắt đầu cuộc họp (API)'],
      expected: 'Không tải lại trang, màn đổi sang "Sẵn sàng tham gia?" có nút Vào phòng (UC-37 AF-3)',
      run: guarded(async (qa) => {
        const m = await fresh(qa, 'Published', 'tự mở khi bắt đầu')
        await qa.goto(`/mgmt/meetings/${m.id}/room`)
        await qa.expect(qa.page.getByRole('heading', { name: 'Phòng họp chỉ mở khi cuộc họp đang diễn ra.' })).toBeVisible({ timeout: 15_000 })
        await qa.shot('dang cho')
        ok(qa, await post('chair', `/api/meetings/${m.id}/transitions`, { action: 'Start' }), 200, 'Start')
        await qa.expect(lobbyHeading(qa.page)).toBeVisible({ timeout: 30_000 })
        await qa.expect(joinButton(qa.page)).toBeVisible()
        await qa.shot('tu mo')
      }),
    },
    {
      id: 'AC16',
      role: 'admin',
      devices: ['desktop', 'phone'],
      title: 'Người tạo và người duyệt không có tên trong thành phần không vào được phòng: link phòng là trang "Không tìm thấy trang", không có nút Vào phòng họp',
      steps: [
        'quantri@ là người tạo (không trong thành phần) của một cuộc họp đang diễn ra; là người duyệt (không trong thành phần) của một cuộc họp khác',
        'Mở chi tiết và /room của từng cuộc họp; mở /room của một id không tồn tại',
      ],
      expected: 'Chi tiết mở được nhưng không có "Vào phòng họp"; /room hiện "Không tìm thấy trang" như id không tồn tại (BR-323, UC-37 EX-4)',
      run: guarded(async (qa) => {
        const caps = await qa.capabilities('admin')
        qa.note(`quantri@ mang vai trò "${caps.roleName}": ${caps.capabilities.join(', ')}`)
        for (const key of ['adminOrganizer', 'adminApprover']) {
          const m = await reused(qa, key)
          await qa.goto(`/mgmt/meetings/${m.id}`)
          await qa.expect(qa.page.getByRole('heading', { level: 1, name: m.title })).toBeVisible({ timeout: 15_000 })
          await qa.expect(qa.page.getByRole('link', { name: 'Vào phòng họp' })).toHaveCount(0)
          await qa.shot(`chi tiet ${key}`)
          await qa.goto(`/mgmt/meetings/${m.id}/room`)
          await qa.expect(qa.page.getByRole('heading', { name: 'Không tìm thấy trang' })).toBeVisible({ timeout: 15_000 })
          await noSidewaysScroll(qa)
          await qa.shot(`phong ${key}`)
        }
        await qa.goto('/mgmt/meetings/0199ffff-ffff-7fff-bfff-ffffffffffff/room')
        await qa.expect(qa.page.getByRole('heading', { name: 'Không tìm thấy trang' })).toBeVisible({ timeout: 15_000 })
        await qa.shot('id khong ton tai')
      }),
    },
    {
      id: 'AC17',
      role: 'chair',
      title: 'API cấp vé vào phòng theo vai trò và trạng thái (role × action)',
      steps: [
        'GET …/call và POST …/call/join bằng chutoa@, thuky@, thanhvien@ trên cuộc họp đang diễn ra',
        'quantri@ (người tạo / người duyệt ngoài thành phần), chưa đăng nhập, id không tồn tại',
        'POST join trên cuộc họp đã công bố, đã kết thúc, đã huỷ, họp trực tiếp',
      ],
      expected:
        'Người dự: GET 200 {status Open, myRole đúng, available true, retentionDays}, join 200 {serverUrl ws://…, roomName meeting-{id}, identity = userId, token}. quantri@: 404/404. Chưa đăng nhập: 401. Id lạ: 404. Đã công bố / đã kết thúc: join 409 MSG117; họp trực tiếp: 409 MSG280; đã huỷ: GET status Cancelled, join 409 MSG117 (docs/03-api/09)',
      run: async (qa) => {
        const u = await users()
        const live = await reused(qa, 'live')
        const k = checker(qa)
        for (const [who, role] of [['chair', 'Chair'], ['secretary', 'Secretary'], ['member', 'Member']]) {
          const s = await get(who, `/api/meetings/${live.id}/call`)
          if (k.check(`${who} GET call`, s, 200)) {
            k.fact(`${who} status Open`, s.body.status === 'Open', s.body.status)
            k.fact(`${who} myRole ${role}`, s.body.myRole === role, s.body.myRole)
            k.fact(`${who} available`, s.body.available === true)
            k.fact(`${who} retentionDays > 0`, s.body.retentionDays > 0, String(s.body.retentionDays))
          }
          const j = await post(who, `/api/meetings/${live.id}/call/join`)
          if (k.check(`${who} join`, j, 200)) {
            k.fact(`${who} serverUrl ws`, /^wss?:\/\//.test(j.body.serverUrl), j.body.serverUrl)
            k.fact(`${who} roomName`, j.body.roomName === `meeting-${live.id}`, j.body.roomName)
            k.fact(`${who} identity is userId`, j.body.identity === u[who].id, j.body.identity)
            k.fact(`${who} token`, typeof j.body.token === 'string' && j.body.token.length > 20)
          }
        }
        for (const key of ['adminOrganizer', 'adminApprover']) {
          const m = await reused(qa, key)
          k.check(`admin GET call (${key})`, await get('admin', `/api/meetings/${m.id}/call`), 404)
          k.check(`admin join (${key})`, await post('admin', `/api/meetings/${m.id}/call/join`), 404)
          // The Chair of the same meeting gets in, so the 404 is about quantri@, not the meeting.
          k.check(`chair join (${key})`, await post('chair', `/api/meetings/${m.id}/call/join`), 200)
        }
        k.check('anon GET call', await get('anon', `/api/meetings/${live.id}/call`), 401)
        k.check('anon join', await post('anon', `/api/meetings/${live.id}/call/join`), 401)
        k.check('anon mute', await post('anon', `/api/meetings/${live.id}/call/participants/${u.member.id}/mute`), 401)
        const ghost = '0199ffff-ffff-7fff-bfff-ffffffffffff'
        k.check('GET call unknown meeting', await get('chair', `/api/meetings/${ghost}/call`), 404)
        k.check('join unknown meeting', await post('chair', `/api/meetings/${ghost}/call/join`), 404)
        const states = [
          ['published', 'NotStarted', 409, 'MSG117'],
          ['ended', 'Ended', 409, 'MSG117'],
          ['cancelled', 'Cancelled', 409, 'MSG117'],
          ['inPerson', 'InPersonOnly', 409, 'MSG280'],
        ]
        for (const [key, status, code, msg] of states) {
          const m = await reused(qa, key)
          const s = await get('member', `/api/meetings/${m.id}/call`)
          if (k.check(`GET call ${key}`, s, 200)) k.fact(`${key} status ${status}`, s.body.status === status, s.body.status)
          k.check(`join ${key}`, await post('member', `/api/meetings/${m.id}/call/join`), code, msg)
        }
        k.done()
      },
    },
    {
      id: 'AC18',
      role: 'chair',
      title: 'API tắt mic: chỉ Chủ toạ hiện tại; người không trong phòng, phòng đã đóng bị từ chối đúng mã',
      steps: [
        'thuky@, thanhvien@ gọi POST …/call/participants/{chutoa}/mute',
        'chutoa@ tắt mic một danh tính không có trong phòng',
        'chutoa@ tắt mic trong cuộc họp đã kết thúc; quantri@ (không trong thành phần) gọi mute',
      ],
      expected:
        'Thư ký / Thành viên: 403 MSG123. Người không trong phòng: 409 MSG282. Đã kết thúc: 409 MSG117 hoặc 404. quantri@: 404 (UC-41, BR-325, docs/03-api/09)',
      run: async (qa) => {
        const u = await users()
        const live = await reused(qa, 'live')
        const ended = await reused(qa, 'ended')
        const adminOrg = await reused(qa, 'adminOrganizer')
        const k = checker(qa)
        const path = (m, id) => `/api/meetings/${m.id}/call/participants/${id}/mute`
        k.check('secretary mutes chair', await post('secretary', path(live, u.chair.id)), 403, 'MSG123')
        k.check('member mutes chair', await post('member', path(live, u.chair.id)), 403, 'MSG123')
        k.check('chair mutes someone not in the room', await post('chair', path(live, '0199ffff-ffff-7fff-bfff-000000000001')), 409, 'MSG282')
        const endedRes = await post('chair', path(ended, u.member.id))
        k.fact('chair mutes in an ended meeting: 409 MSG117 (UC-41 EX-5)', endedRes.status === 409 && endedRes.body?.code === 'MSG117', `${endedRes.status} ${JSON.stringify(endedRes.body).slice(0, 120)}`)
        k.check('admin (not on roster) mutes', await post('admin', path(adminOrg, u.member.id)), 404)
        k.done()
      },
    },
    {
      id: 'AC19',
      role: 'member',
      devices: ['desktop', 'phone'],
      title: 'Bảng của cuộc họp trong phòng: Nội dung họp, Tài liệu, Biểu quyết, Phát biểu mở được; Ghi chú chỉ của Chủ toạ/Thư ký; trên điện thoại nằm sau nút Thêm',
      steps: ['thanhvien@ vào phòng', 'Mở lần lượt Nội dung họp, Biểu quyết (máy tính: nút bên phải; điện thoại: nút Thêm)'],
      expected:
        'Mỗi bảng mở ở cạnh phải với tiêu đề của nó, đóng lại bằng chính nút đó; thành viên không có "Ghi chú"; điện thoại không cuộn ngang (ADR-0011)',
      run: guarded(async (qa) => {
        const m = await reused(qa, 'live')
        await setPrefs(qa.context, { mic: false, cam: false })
        await openLobby(qa, m)
        await joinRoom(qa)
        const c = controls(qa.page)
        const open = async (name) => {
          if (qa.device === 'phone') {
            await press(qa, c.getByRole('button', { name: 'Thêm' }))
            const menu = qa.page.getByRole('menu', { name: 'Thêm' })
            await qa.expect(menu).toBeVisible()
            await qa.expect(menu.getByRole('menuitem', { name: 'Ghi chú' })).toHaveCount(0)
            await press(qa, menu.getByRole('menuitem', { name }))
          } else {
            await qa.expect(c.getByRole('button', { name: 'Ghi chú' })).toHaveCount(0)
            await press(qa, c.getByRole('button', { name }))
          }
          await qa.expect(qa.page.getByRole('complementary', { name })).toBeVisible()
        }
        await open('Nội dung họp')
        await noSidewaysScroll(qa)
        await qa.shot('bang noi dung hop')
        await open('Biểu quyết')
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
        await qa.shot('bang bieu quyet')
        await open('Phát biểu')
        await qa.shot('bang phat bieu')
      }),
    },
    {
      id: 'AC20',
      role: 'chair',
      title: 'Giơ tay trong phòng: thành viên giơ tay, Chủ toạ thấy hàng đợi và mời, người được mời thấy "Đến lượt bạn phát biểu"',
      steps: [
        'chutoa@ và thanhvien@ (cửa sổ thứ hai) cùng trong phòng',
        'thanhvien@ bấm Giơ tay',
        'Chủ toạ mở Phát biểu, bấm Mời ở dòng Lê An',
        'thanhvien@ hạ tay / kết thúc để dọn hàng đợi',
      ],
      expected:
        'Nút Phát biểu của Chủ toạ hiện số 1, bảng liệt kê Lê An; Mời → thành viên thấy dải "Đến lượt bạn phát biểu" với nút Bắt đầu phát biểu; giơ tay không bật micro (UC-79, BR-337, BR-338)',
      run: guarded(async (qa) => {
        const m = await reused(qa, 'live')
        const u = await users()
        // A request left open by an earlier run is closed first, so the queue starts empty.
        const before = await get('chair', `/api/meetings/${m.id}/speaking-requests`)
        for (const r of before.body?.requests ?? []) {
          if (r.status === 'Speaking' || r.status === 'Invited') await post('chair', `/api/meetings/${m.id}/speaking-requests/${r.id}/finish`)
          else if (r.status === 'Queued') await post('chair', `/api/meetings/${m.id}/speaking-requests/${r.id}/decline`)
        }
        await setPrefs(qa.context, { mic: false, cam: false })
        await openLobby(qa, m)
        await joinRoom(qa)
        const member = await person(qa, 'member')
        await openLobby(qa, m, member)
        await joinRoom(qa, member)
        const hand = controls(member).getByRole('button', { name: 'Giơ tay' })
        await qa.expect(hand).toBeVisible({ timeout: 15_000 })
        await qa.expect(async () => {
          if (await hand.isVisible()) await hand.click()
          await qa.expect(controls(member).getByRole('button', { name: 'Hạ tay' })).toBeVisible({ timeout: 3_000 })
        }).toPass({ timeout: 30_000 })
        const speakingButton = controls(qa.page).getByRole('button', { name: 'Phát biểu' })
        await qa.expect(speakingButton.locator('.room-badge')).toHaveText('1', { timeout: 15_000 })
        await speakingButton.click()
        const panel = qa.page.getByRole('complementary', { name: 'Phát biểu' })
        const row = panel.getByRole('list', { name: 'Hàng đợi phát biểu' }).locator('li').filter({ hasText: u.member.name })
        await qa.expect(row).toBeVisible()
        await qa.shot('hang doi phat bieu')
        await row.getByRole('button', { name: 'Mời' }).click()
        await qa.expect(member.getByText('Đến lượt bạn phát biểu')).toBeVisible({ timeout: 15_000 })
        await qa.expect(member.getByRole('button', { name: 'Bắt đầu phát biểu' })).toBeVisible()
        await qa.expect(controls(member).getByRole('button', { name: 'Bật micro' })).toBeVisible()
        await shotOf(qa, member, 'den luot thanh vien')
        await controls(member).getByRole('button', { name: 'Hạ tay' }).click().catch(() => undefined)
      }),
    },
    {
      id: 'AC21',
      role: 'chair',
      title: 'Chia sẻ màn hình trên máy tính: người trình bày thấy "Bạn đang trình bày cho mọi người", người khác thấy tên người trình bày',
      steps: ['chutoa@ và thanhvien@ (cửa sổ thứ hai) cùng trong phòng', 'Chủ toạ bấm Chia sẻ màn hình', 'Bấm Dừng chia sẻ'],
      expected:
        'Sân khấu hiện màn hình chia sẻ, chú thích "Bạn đang trình bày cho mọi người"; thành viên thấy "Nguyễn Hoàng Thanh Hương đang trình bày"; dừng thì về lưới (UC-39c)',
      run: guarded(async (qa) => {
        const m = await reused(qa, 'live')
        const u = await users()
        await setPrefs(qa.context, { mic: false, cam: false })
        await openLobby(qa, m)
        await joinRoom(qa)
        const member = await person(qa, 'member')
        await openLobby(qa, m, member)
        await joinRoom(qa, member)
        await controls(qa.page).getByRole('button', { name: 'Chia sẻ màn hình' }).click()
        const caption = qa.page.getByText('Bạn đang trình bày cho mọi người')
        try {
          await qa.expect(caption).toBeVisible({ timeout: 10_000 })
        } catch {
          qa.inconclusive(
            'Chromium không giao diện của bộ chạy không chọn được màn hình để chia sẻ (getDisplayMedia cần người bấm chọn). Kiểm tay khi diễn tập: hai máy trong phòng, một máy bấm Chia sẻ màn hình, máy kia thấy "<tên> đang trình bày"; máy thứ hai chia sẻ thì máy đầu tự dừng và báo MSG340.',
          )
        }
        await qa.expect(member.getByText(`${u.chair.name} đang trình bày`)).toBeVisible({ timeout: 15_000 })
        // The viewer sees the shared picture itself, not only the caption over a black stage.
        await member
          .waitForFunction(() => (document.querySelector('.room-screen video')?.videoWidth ?? 0) > 0, null, { timeout: 20_000 })
          .catch(() => {
            throw new Error('the viewer stage stays black: the shared screen shows no picture after 20 s')
          })
        await shotOf(qa, member, 'dang trinh bay')
        await controls(qa.page).getByRole('button', { name: 'Dừng chia sẻ' }).click()
        await qa.expect(caption).toHaveCount(0, { timeout: 10_000 })
      }),
    },
    {
      id: 'AC22',
      role: 'secretary',
      title: 'Cửa sổ nổi (UC-78): mở thì sân khấu báo "Cuộc họp đang ở cửa sổ nổi", đưa về được, không vào phòng lần hai',
      steps: ['thuky@ vào phòng', 'Bấm Mở cửa sổ nổi', 'Bấm "Đưa cuộc họp về cửa sổ này"'],
      expected:
        'Sân khấu chính: "Cuộc họp đang ở cửa sổ nổi" với nút đưa về; số người trong phòng không tăng; đưa về thì lưới trở lại (UC-78, BR-332)',
      run: guarded(async (qa) => {
        const m = await reused(qa, 'live')
        await setPrefs(qa.context, { mic: false, cam: false })
        await openLobby(qa, m)
        await joinRoom(qa)
        const pip = controls(qa.page).getByRole('button', { name: 'Mở cửa sổ nổi' })
        if ((await pip.count()) === 0)
          qa.inconclusive(
            'Chromium của bộ chạy không có Document Picture-in-Picture nên nút Mở cửa sổ nổi không hiện (UC-78 AF-3, đúng đặc tả). Kiểm tay khi diễn tập trên Chrome thật: bấm Mở cửa sổ nổi, chuyển tab, rời phòng từ cửa sổ nổi không hỏi xác nhận.',
          )
        await qa.expect.poll(() => headerCount(qa.page), { timeout: 15_000 }).toBeGreaterThanOrEqual(1)
        const count = await headerCount(qa.page)
        await pip.click()
        const elsewhere = qa.page.getByRole('heading', { name: 'Cuộc họp đang ở cửa sổ nổi' })
        try {
          await qa.expect(elsewhere).toBeVisible({ timeout: 10_000 })
        } catch {
          qa.inconclusive(
            'Trình duyệt không giao diện từ chối mở cửa sổ nổi (UC-78 EX-1: không báo gì, cuộc gọi ở lại cửa sổ chính). Kiểm tay khi diễn tập trên Chrome thật.',
          )
        }
        qa.expect(await headerCount(qa.page), 'the floating window joined a second time').toBe(count)
        await qa.shot('dang o cua so noi')
        await qa.page.getByRole('button', { name: 'Đưa cuộc họp về cửa sổ này' }).click()
        await qa.expect(elsewhere).toHaveCount(0)
        await qa.shot('da dua ve')
      }),
    },
    {
      id: 'AC23',
      role: 'anon',
      title: 'Khách vào phòng qua lời mời (OTP qua email), xác nhận thông báo ghi âm — cần hộp thư thật',
      steps: [
        'Thư ký mời một email khách thật vào cuộc họp Trực tuyến đã công bố',
        'Khách mở link, nhận OTP qua email, khai tên, chờ ở màn chờ; Chủ toạ bắt đầu',
        'Khách tick "Tôi biết cuộc họp được ghi âm…", bấm Vào phòng',
      ],
      expected:
        'Trước khi tick, nút Vào phòng bị khoá; màn chờ không có nhãn mức độ mật và (i) không nói về AI; vào phòng thì Chủ toạ thấy dòng "Khách"; Khách không có nút Tắt mic; thu hồi lời mời thì Khách bị đưa ra màn kết thúc (UC-37 AF-2, EX-9, UC-04)',
      run: async (qa) => {
        qa.inconclusive(
          'Khách cần mã OTP gửi tới một hộp thư thật (UC-04), bộ chạy không đọc được thư. Kiểm tay khi diễn tập (SCRUM-205): mời một email thật vào cuộc họp Trực tuyến, khách xác minh OTP, khai tên, chờ ở màn chờ tới khi Chủ toạ bắt đầu (trang tự hỏi lại mỗi 10 giây), tick xác nhận ghi âm rồi Vào phòng; Chủ toạ thấy "Khách" trong Người dự và tắt mic khách được; thu hồi lời mời thì khách bị đưa ra ngay.',
        )
      },
    },
    {
      id: 'AC24',
      role: 'anon',
      title: 'Âm thanh thật, mất kết nối, trình duyệt chặn thiết bị, phòng đầy 50 người — kiểm tay khi diễn tập',
      steps: [
        'Hai máy thật: nói vào micro, nghe ở máy kia; viền người đang nói',
        'Tắt mạng một máy giữa cuộc họp',
        'Chặn quyền micro/camera trong trình duyệt rồi vào phòng',
        'Trình duyệt chặn tự phát âm thanh',
      ],
      expected:
        'Nghe rõ không vọng; dải "Mất kết nối, đang kết nối lại…" (MSG333), rồi "Mất kết nối tới phòng họp." (MSG332) có Vào lại; chặn quyền: MSG334, vẫn vào nghe được; MSG338 với nút "Bấm để nghe mọi người"; người thứ 51 không vào được (BR-336, chưa có mã)',
      run: async (qa) => {
        qa.inconclusive(
          'Cần người thật và thiết bị thật: chất lượng tiếng/vọng, viền người đang nói theo giọng thật, mất mạng (MSG333 → MSG332), chặn quyền thiết bị (MSG334) và chặn phát âm thanh (MSG338) cần thao tác trong cài đặt trình duyệt; 50 người cần bộ tải tests/load. Kiểm tay khi diễn tập (SCRUM-205) trên hai máy và một điện thoại.',
        )
      },
    },
  ],
}

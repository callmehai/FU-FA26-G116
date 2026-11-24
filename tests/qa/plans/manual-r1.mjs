// Round 1 UI — the cases the handover note (docs/notes/NOTE-ban-giao-manual-ui-round1.md) left for a person, group H
// ("chạy tay thẳng trên giao diện") and the parts of D/F/G a single machine can drive. Each criterion's id is the case
// id in docs/08-reports/test-reports/cases/FE-xx/ui.json, so a result goes straight back into round1.
//
// Run on an isolated database: it creates users, deactivates one, edits rows by SQL (QA_PSQL) and drives meetings to
// every state. Every record it creates is titled "[QA] MR1 …".

import { execFileSync } from 'node:child_process'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { request as playwrightRequest } from '@playwright/test'

const rand = () => Math.random().toString(36).slice(2, 7)
const ACCOUNT = {
  admin: 'quantri@trustcabinet.local',
  chair: 'chutoa@trustcabinet.local',
  secretary: 'thuky@trustcabinet.local',
  member: 'thanhvien@trustcabinet.local',
}

/** A multipart POST as `role` (the qa API client sends JSON only). */
async function multipart(qa, role, path, form) {
  const ctx = await playwrightRequest.newContext({ baseURL: qa.base })
  try {
    const login = await ctx.post('/api/auth/login', { data: { email: ACCOUNT[role] ?? role, password: 'TrustCabinet@2026' } })
    const token = (await login.json()).accessToken
    const res = await ctx.post(path, { headers: { Authorization: `Bearer ${token}` }, multipart: form, timeout: 120_000 })
    const text = await res.text()
    let body
    try {
      body = JSON.parse(text)
    } catch {
      body = text
    }
    return { status: res.status(), body }
  } finally {
    await ctx.dispose()
  }
}

/** A small valid one-page PDF with a title. */
function pdf(title) {
  const objs = [
    '<</Type/Catalog/Pages 2 0 R>>',
    '<</Type/Pages/Kids[3 0 R]/Count 1>>',
    '<</Type/Page/Parent 2 0 R/MediaBox[0 0 300 200]/Contents 4 0 R/Resources<</Font<</F1 5 0 R>>>>>>',
    null,
    '<</Type/Font/Subtype/Type1/BaseFont/Helvetica>>',
  ]
  const stream = `BT /F1 14 Tf 20 100 Td (${title.replace(/[()\\]/g, '')}) Tj ET`
  objs[3] = `<</Length ${stream.length}>>\nstream\n${stream}\nendstream`
  let out = '%PDF-1.4\n'
  const offsets = []
  objs.forEach((o, i) => {
    offsets.push(out.length)
    out += `${i + 1} 0 obj\n${o}\nendobj\n`
  })
  const xref = out.length
  out += `xref\n0 ${objs.length + 1}\n0000000000 65535 f \n${offsets.map((o) => `${String(o).padStart(10, '0')} 00000 n \n`).join('')}`
  out += `trailer\n<</Size ${objs.length + 1}/Root 1 0 R>>\nstartxref\n${xref}\n%%EOF\n`
  return Buffer.from(out, 'latin1')
}

/** Two seconds of a tone as 16 kHz mono WAV. */
function wav(seconds = 2) {
  const n = 16000 * seconds
  const pcm = Buffer.alloc(n * 2)
  for (let i = 0; i < n; i++) pcm.writeInt16LE(Math.round(Math.sin((i / 16000) * 2 * Math.PI * 440) * 8000), i * 2)
  const h = Buffer.alloc(44)
  h.write('RIFF', 0)
  h.writeUInt32LE(36 + pcm.length, 4)
  h.write('WAVEfmt ', 8)
  h.writeUInt32LE(16, 16)
  h.writeUInt16LE(1, 20)
  h.writeUInt16LE(1, 22)
  h.writeUInt32LE(16000, 24)
  h.writeUInt32LE(32000, 28)
  h.writeUInt16LE(2, 32)
  h.writeUInt16LE(16, 34)
  h.write('data', 36)
  h.writeUInt32LE(pcm.length, 40)
  return Buffer.concat([h, pcm])
}

async function uploadRecording(qa, meetingId) {
  const res = await multipart(qa, 'secretary', `/api/meetings/${meetingId}/recordings`, {
    file: { name: 'qa-mr1.wav', mimeType: 'audio/wav', buffer: wav(2) },
    consentConfirmed: 'true',
    consentPurpose: 'QA MR1: lập biên bản',
    durationMs: '2000',
  })
  qa.expect(res.status, JSON.stringify(res.body).slice(0, 200)).toBe(201)
  return res.body.recordingId
}

/** One room-mic take sent as the browser sends it (POST takes, PUT parts, POST complete). */
/** The PCM samples of a 16 kHz mono 16-bit WAV file (whatever chunks precede "data"). */
function pcmOf(file) {
  const b = readFileSync(file)
  const at = b.indexOf('data', 12)
  return b.subarray(at + 8, at + 8 + b.readUInt32LE(at + 4))
}
async function roomTake(qa, meetingId, { consent = false, speech } = {}) {
  const sec = await client(qa, 'secretary')
  const opened = await sec.post(`/api/meetings/${meetingId}/recordings/takes`, consent ? { consentConfirmed: true, consentPurpose: 'QA MR1: lập biên bản' } : {})
  qa.expectStatus(opened, 201)
  const { recordingId, partSize } = opened.body
  const head = Buffer.from(wav(0).subarray(0, 44))
  head.writeUInt32LE(0xffffffff, 4)
  head.writeUInt32LE(0xffffffff - 36, 40)
  // Speech (a machine voice from QA_TTS_DIR) between seconds of silence, as a real take has; a tone otherwise.
  const quiet = Buffer.alloc(32000)
  const body = speech && process.env.QA_TTS_DIR ? Buffer.concat([quiet, pcmOf(join(process.env.QA_TTS_DIR, speech)), quiet]) : wav(3).subarray(44)
  const payload = Buffer.concat([head, body])
  const ctx = await playwrightRequest.newContext({ baseURL: qa.base })
  try {
    const login = await ctx.post('/api/auth/login', { data: { email: ACCOUNT.secretary, password: 'TrustCabinet@2026' } })
    const token = (await login.json()).accessToken
    let n = 0
    for (let at = 0; at < payload.length; at += partSize) {
      n += 1
      const final = at + partSize >= payload.length
      const r = await ctx.put(`/api/recordings/${recordingId}/parts/${n}${final ? '?final=true' : ''}`, {
        data: payload.subarray(at, at + partSize),
        headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/octet-stream' },
      })
      qa.expect(r.status(), `part ${n}`).toBe(204)
    }
  } finally {
    await ctx.dispose()
  }
  qa.expectStatus(await sec.post(`/api/recordings/${recordingId}/complete`), 200)
  return recordingId
}
async function waitTranscript(qa, recordingId, ms = 180_000) {
  const sec = await client(qa, 'secretary')
  const until = Date.now() + ms
  let last
  while (Date.now() < until) {
    last = (await sec.get(`/api/recordings/${recordingId}/transcript`)).body
    if (last?.status === 'Completed' || last?.status === 'Failed') return last
    await new Promise((r) => setTimeout(r, 3000))
  }
  return last
}


/** Messages mailpit (http://localhost:8025) received for `to`: subject list, newest first. */
async function mailpit(to) {
  const r = await fetch(`http://localhost:8025/api/v1/search?query=${encodeURIComponent(`to:"${to}"`)}`)
  const j = await r.json()
  return (j.messages ?? []).map((m) => ({ id: m.ID, subject: m.Subject, attachments: m.Attachments }))
}

/** The plain text of the newest mailpit message to `to` whose subject matches `re`, polled up to `ms`. */
async function mailText(to, re, ms = 90_000) {
  const until = Date.now() + ms
  while (Date.now() < until) {
    const hit = (await mailpit(to)).find((x) => re.test(x.subject))
    if (hit) {
      const full = await (await fetch(`http://localhost:8025/api/v1/message/${hit.id}`)).json()
      return { subject: hit.subject, text: full.Text ?? '', html: full.HTML ?? '', attachments: (full.Attachments ?? []).map((a) => a.FileName) }
    }
    await new Promise((r) => setTimeout(r, 2000))
  }
  return null
}
/** The six-digit code of the newest "Mã xác minh" email to a Guest. */
async function guestCode(to) {
  return ((await mailText(to, /Mã xác minh/))?.text ?? '').match(/\b\d{6}\b/)?.[0] ?? ''
}
async function mailCount(to, re) {
  return (await mailpit(to)).filter((x) => re.test(x.subject)).length
}

// Minutes
const MIN = (id) => `/api/meetings/${id}/minutes`
async function minutesOf(qa, role, meetingId) {
  return (await (await client(qa, role)).get(MIN(meetingId))).body?.minutes ?? null
}
/** Drives the latest minutes revision to `target`: none → Draft → WaitingApproval → Issued → (correction) Draft. */
async function minutesTo(qa, meetingId, target) {
  const sec = await client(qa, 'secretary')
  const chair = await client(qa, 'chair')
  for (let i = 0; i < 8; i++) {
    const m = await minutesOf(qa, 'secretary', meetingId)
    if (m?.status === target) return m
    let res
    if (!m) res = await sec.post(MIN(meetingId), { rebuild: false })
    else if (m.status === 'Draft' || m.status === 'Rejected') res = await sec.post(`/api/minutes/${m.id}/submit`, { updatedAt: m.updatedAt })
    else if (m.status === 'WaitingApproval') res = await chair.post(`/api/minutes/${m.id}/approve`, { updatedAt: m.updatedAt })
    else if (m.status === 'Issued') res = await sec.post(`/api/minutes/${m.id}/corrections`)
    qa.expect([200, 201], `minutes → ${target}: ${res.status} ${JSON.stringify(res.body).slice(0, 200)}`).toContain(res.status)
  }
  throw new Error(`minutes never reached ${target}`)
}
const minutesCard = (qa) => qa.page.getByRole('region', { name: 'Biên bản', exact: true })
const minutesBtn = (qa, name) => minutesCard(qa).getByRole('button', { name, exact: true })
async function openMinutes(qa, m) {
  await qa.goto(`/mgmt/meetings/${m.id}?tab=minutes`)
  await qa.expect(qa.page.getByRole('heading', { level: 1, name: m.title })).toBeVisible()
  await qa.expect(minutesCard(qa)).toBeVisible()
  await qa.expect(minutesCard(qa)).not.toHaveAttribute('aria-busy', 'true')
}
const minutesBadge = (qa) => minutesCard(qa).locator('.minutes-card-head')

/** A second signed-in browser context for `who`, at the same screen size. */
async function second(qa, who) {
  const ctx = await qa.context.browser().newContext({ viewport: { width: 1440, height: 900 }, baseURL: qa.base, locale: 'vi-VN', timezoneId: 'Asia/Ho_Chi_Minh', permissions: ['microphone', 'camera'] })
  const res = await ctx.request.post('/api/auth/login', { data: { email: ACCOUNT[who] ?? who, password: 'TrustCabinet@2026' } })
  qa.expect(res.status()).toBe(200)
  await ctx.addInitScript(() => localStorage.setItem('trustcabinet.signedIn', '1'))
  qa.__extra ??= []
  qa.__extra.push(ctx)
  return ctx.newPage()
}
const iso = (ms) => new Date(ms).toISOString().replace(/\.\d{3}Z$/, 'Z')

// ---------------------------------------------------------------------------------------------------------------------
// Accounts and API

const USERS = {}
async function users(qa) {
  for (const role of ['admin', 'chair', 'secretary', 'member']) {
    if (USERS[role]) continue
    const me = (await (await client(qa, role)).get('/api/auth/me')).body
    USERS[role] = { id: me.id, name: me.fullName, email: me.email, roleId: me.roleId, capabilities: me.capabilities ?? [] }
  }
  return USERS
}
async function client(qa, role) {
  qa.__clients ??= {}
  qa.__clients[role] ??= await qa.api(role)
  return qa.__clients[role]
}

/** Runs SQL on the isolated database through the helper script named by QA_PSQL. */
function sql(text) {
  const script = process.env.QA_PSQL
  if (!script) throw new Error('QA_PSQL is not set')
  return execFileSync('powershell', ['-NoProfile', '-File', script, text], { encoding: 'utf8' }).trim()
}

let slotCounter = 0
function slot(daysAhead = 3) {
  slotCounter += 1
  const hour = 3_600_000
  const start = Math.ceil((Date.now() + daysAhead * 24 * hour) / hour) * hour + slotCounter * 20 * 60_000 + Math.floor(Math.random() * 40) * 3_600_000
  return { plannedStartTime: iso(start), plannedEndTime: iso(start + hour) }
}

async function roster(qa, { secretary = true, extra = [] } = {}) {
  const u = await users(qa)
  return [
    { userId: u.chair.id, role: 'Chair' },
    ...(secretary ? [{ userId: u.secretary.id, role: 'Secretary' }] : []),
    { userId: u.member.id, role: 'Member' },
    ...extra,
  ]
}

const PATH = {
  Draft: [],
  WaitingApproval: ['submit'],
  Approved: ['submit', 'approve'],
  Published: ['submit', 'approve', 'publish'],
  InProgress: ['submit', 'approve', 'publish', 'start'],
  Ended: ['submit', 'approve', 'publish', 'start', 'end'],
}

/** Creates a meeting organised by thuky@ and drives it to `status` through the API. */
async function meetingAt(qa, status, { label = status, over = {}, approver, participants } = {}) {
  const u = await users(qa)
  const sec = await client(qa, 'secretary')
  const created = await sec.post('/api/meetings', {
    title: qa.name(`MR1 ${label} ${rand()}`),
    ...slot(),
    mode: 'InPerson',
    location: 'Phòng họp QA',
    onlineLink: null,
    sensitivity: 'Normal',
    description: 'Cuộc họp do bộ kiểm thử QA tạo.',
    participants: participants ?? (await roster(qa)),
    ...over,
  })
  qa.expectStatus(created, 201)
  const id = created.body.meeting.id
  for (const step of PATH[status]) {
    const [role, body] = {
      submit: ['secretary', { action: 'SubmitForApproval', assignedApproverId: approver ?? u.chair.id }],
      approve: ['chair', { action: 'Approve' }],
      publish: ['secretary', { action: 'Publish' }],
      start: ['chair', { action: 'Start' }],
      end: ['chair', { action: 'End' }],
    }[step]
    const res = await (await client(qa, role)).post(`/api/meetings/${id}/transitions`, body)
    qa.expectStatus(res, 200)
  }
  const got = (await sec.get(`/api/meetings/${id}`)).body
  qa.note(`Cuộc họp: ${got.title} (${id}, ${got.status})`)
  return got
}

/** A user created by the administrator, signed in with a known password. */
const PASSWORD = 'TrustCabinet@2026'
async function makeUser(qa, label, roleName) {
  const admin = await client(qa, 'admin')
  const roles = (await admin.get('/api/roles')).body
  const role = (roles.items ?? roles).find((r) => r.name === roleName)
  const email = `qa-mr1-${label}-${rand()}@trustcabinet.local`
  const name = `QA MR1 ${label} ${email.slice(-25, -20)}`
  const res = await admin.post('/api/users', { fullName: name, email, password: PASSWORD, roleId: role.id })
  qa.expectStatus(res, 201)
  return { id: res.body.user.id, email, name }
}

// ---------------------------------------------------------------------------------------------------------------------
// Screen

async function noSidewaysScroll(qa) {
  const sideways = await qa.page.evaluate(() => document.documentElement.scrollWidth - innerWidth)
  qa.expect(sideways, 'the page scrolls sideways').toBeLessThanOrEqual(0)
}
async function settled(qa) {
  await qa.page.waitForFunction(() =>
    document.getAnimations().every((a) => a.playState !== 'running' || a.effect?.getTiming().iterations === Infinity),
  )
}
async function snap(qa, slug, opts) {
  const closers = qa.page.locator('.toast-stack .toast-info .toast-close')
  for (let i = await closers.count(); i > 0; i--) await closers.first().evaluate((b) => b.click()).catch(() => undefined)
  await settled(qa).catch(() => undefined)
  return qa.shot(slug, opts)
}
const toast = (qa, text) => qa.page.locator('.toast-stack').getByText(text)
const dialog = (qa, name) => qa.page.getByRole('dialog', { name })
async function openMeeting(qa, m, tab = '') {
  await qa.goto(`/mgmt/meetings/${m.id}${tab ? `?tab=${tab}` : ''}`)
  await qa.expect(qa.page.getByRole('heading', { level: 1, name: m.title })).toBeVisible()
}
/** Records every request to a path, to prove something was or was not sent. */
function watch(qa, method, pattern) {
  const seen = []
  qa.page.on('request', (r) => {
    if (r.method() === method && pattern.test(new URL(r.url()).pathname)) seen.push(`${r.method()} ${r.url()}`)
  })
  return seen
}
function vnDay(offsetDays = 0) {
  const d = new Date(Date.now() + offsetDays * 86_400_000 + 7 * 3_600_000)
  return { iso: d.toISOString().slice(0, 10), vn: `${d.toISOString().slice(8, 10)}/${d.toISOString().slice(5, 7)}/${d.toISOString().slice(0, 4)}` }
}

// ---------------------------------------------------------------------------------------------------------------------

export default {
  ticket: 'MANUAL-R1',
  title: 'Round 1 UI: các case để chạy tay (nhóm H, một phần D/F/G)',
  devices: ['desktop'],
  criteria: [
    // ------------------------------------------------------------------------------------------------ FE-04 meetings
    {
      id: 'MEET_FT03',
      role: 'secretary',
      title: 'Form tạo cuộc họp báo kết thúc trước bắt đầu và bắt đầu đã qua',
      steps: ['Mở form, hôm nay 09:00–08:00', 'Sửa kết thúc 10:00, bắt đầu giờ đã qua, bấm "Tạo cuộc họp"'],
      expected: '"Thời gian kết thúc phải sau thời gian bắt đầu."; rồi "Thời gian bắt đầu phải sau thời điểm hiện tại."; form vẫn mở, không tạo gì',
      async run(qa) {
        const posts = watch(qa, 'POST', /^\/api\/meetings$/)
        const today = vnDay(0)
        await qa.goto(`/mgmt/meetings?create=${today.iso}`)
        const form = dialog(qa, 'Tạo cuộc họp')
        await qa.expect(form).toBeVisible()
        await form.getByPlaceholder('Ví dụ: Giao ban tuần 40').fill(qa.name('MR1 FT03 giờ sai'))
        await form.getByLabel('Bắt đầu').fill('09:00')
        await form.getByLabel('Kết thúc').fill('08:00')
        await form.getByPlaceholder('Ví dụ: Giao ban tuần 40').click()
        await qa.expect(form.getByText('Thời gian kết thúc phải sau thời gian bắt đầu.')).toBeVisible()
        await snap(qa, 'ket-thuc-truoc-bat-dau', { fullPage: true })
        // A start already passed today: one minute after midnight in Vietnam is always past.
        await form.getByLabel('Kết thúc').fill('23:59')
        await form.getByLabel('Bắt đầu').fill('00:01')
        await form.getByRole('button', { name: 'Tạo cuộc họp' }).click()
        await qa.expect(form.getByText('Thời gian bắt đầu phải sau thời điểm hiện tại.')).toBeVisible()
        await snap(qa, 'bat-dau-da-qua', { fullPage: true })
        await qa.expect(form).toBeVisible()
        qa.expect(posts, 'a create request was sent').toEqual([])
        const api = await (await client(qa, 'secretary')).post('/api/meetings', {
          title: qa.name('MR1 FT03 api'),
          plannedStartTime: iso(Date.now() - 3_600_000),
          plannedEndTime: iso(Date.now() + 3_600_000),
          mode: 'InPerson',
          location: 'QA',
          onlineLink: null,
          sensitivity: 'Normal',
          description: '',
          participants: await roster(qa),
        })
        qa.note(`API bắt đầu đã qua: ${api.status} ${api.body?.code}`)
        qa.expect(api.status).toBe(400)
        qa.expect(api.body.code).toBe('MSG31')
      },
    },
    {
      id: 'MEET_FT04',
      role: 'secretary',
      title: 'Form tạo cuộc họp hiện lỗi danh sách thành phần của backend ngay ở người bị vô hiệu hoá',
      steps: ['Mở form, chọn một người', 'Quản trị vô hiệu hoá người đó (tab khác)', 'Bấm "Tạo cuộc họp"'],
      expected: '400 MSG277 với participants[i].userId; lỗi hiện ở dòng người đó hoặc dưới ô thành phần; form giữ nguyên',
      async run(qa) {
        const victim = await makeUser(qa, 'vo-hieu', 'Thành viên')
        await qa.goto(`/mgmt/meetings?create=${vnDay(4).iso}`)
        const form = dialog(qa, 'Tạo cuộc họp')
        await qa.expect(form).toBeVisible()
        const title = qa.name(`MR1 FT04 ${rand()}`)
        await form.getByPlaceholder('Ví dụ: Giao ban tuần 40').fill(title)
        const search = form.getByRole('combobox', { name: 'Tìm người dự họp' })
        await search.click()
        await search.fill('qa-mr1-vo-hieu')
        const option = qa.page.getByRole('option').filter({ hasText: victim.email })
        await qa.expect(option).toBeVisible()
        await option.click()
        await form.getByPlaceholder('Ví dụ: Giao ban tuần 40').click()
        const off = await (await client(qa, 'admin')).put(`/api/users/${victim.id}/status`, { isActive: false })
        qa.note(`Vô hiệu hoá: ${off.status} ${off.body?.code ?? ''}`)
        const response = qa.page.waitForResponse((r) => r.request().method() === 'POST' && new URL(r.url()).pathname === '/api/meetings')
        await form.getByRole('button', { name: 'Tạo cuộc họp' }).click()
        const res = await response
        const body = await res.json().catch(() => ({}))
        qa.note(`POST /api/meetings: ${res.status()} ${JSON.stringify(body).slice(0, 300)}`)
        qa.expect(res.status()).toBe(400)
        qa.expect(body.code).toBe('MSG277')
        await qa.expect(form).toBeVisible()
        await qa.expect(form.getByPlaceholder('Ví dụ: Giao ban tuần 40')).toHaveValue(title)
        const row = form.locator('li, .participant-row, [class*="person"]').filter({ hasText: victim.name }).first()
        const text = (await form.innerText()).replace(/\s+/g, ' ')
        qa.note(`Form sau lỗi: ${text.slice(0, 600)}`)
        await snap(qa, 'loi-nguoi-bi-vo-hieu-hoa', { fullPage: true })
        const msg = Object.values(body.errors ?? {}).flat()[0] ?? body.message
        qa.expect(text, 'the backend roster error is not shown in the form').toContain(String(msg).trim())
        qa.expect(await row.count()).toBeGreaterThan(0)
      },
    },
    {
      id: 'MEET_FT06',
      role: 'secretary',
      title: 'Phiên hết hạn khi đang tạo họp: về trang đăng nhập, không tạo cuộc họp',
      steps: ['Mở form, điền hợp lệ', 'Xoá token và cookie làm mới', 'Bấm "Tạo cuộc họp"'],
      expected: '401, làm mới thất bại; về /login; đăng nhập lại không thấy cuộc họp',
      async run(qa) {
        await qa.goto(`/mgmt/meetings?create=${vnDay(4).iso}`)
        const form = dialog(qa, 'Tạo cuộc họp')
        await qa.expect(form).toBeVisible()
        const title = qa.name(`MR1 FT06 ${rand()}`)
        await form.getByPlaceholder('Ví dụ: Giao ban tuần 40').fill(title)
        // DevTools: the refresh cookie is deleted; the access token lives in memory only (the signed-in marker in
        // localStorage is not the token and stays).
        await qa.context.clearCookies()
        // The access token lives in memory: a forged one makes the next call 401, as an expired one would.
        await qa.page.route('**/api/meetings', (route) =>
          route.continue({ headers: { ...route.request().headers(), authorization: 'Bearer expired.token.value' } }),
        )
        await form.getByRole('button', { name: 'Tạo cuộc họp' }).click()
        await qa.expect(qa.page).toHaveURL(/\/login/, { timeout: 15_000 })
        await snap(qa, 've-trang-dang-nhap')
        const list = (await (await client(qa, 'secretary')).get(`/api/meetings?q=${encodeURIComponent(title)}`)).body
        qa.expect(list.items.length, 'the meeting was created').toBe(0)
      },
    },
    {
      id: 'MEET_FT14',
      role: 'chair',
      title: 'Thẻ "Cần xử lý" cho biết việc chờ người dùng',
      steps: ['chutoa@ (Phê duyệt viên) có một cuộc họp chờ duyệt, mở /mgmt/meetings', 'Bấm thẻ "Cần xử lý"'],
      expected: 'Phụ đề thẻ theo quyền; danh sách có cuộc họp được giao, URL scope=todo',
      async run(qa) {
        const m = await meetingAt(qa, 'WaitingApproval', { label: 'FT14 chờ duyệt' })
        await qa.goto('/mgmt/meetings')
        const cardEl = qa.page.locator('.stat-cards').getByRole('link', { name: /Cần xử lý/ }).or(qa.page.locator('.stat-cards').getByRole('button', { name: /Cần xử lý/ })).first()
        await qa.expect(cardEl).toBeVisible()
        const sub = (await cardEl.innerText()).replace(/\s+/g, ' ')
        qa.note(`Thẻ (chutoa@, MeetingCreator + MeetingApprover): ${sub}`)
        qa.expect(sub).toContain('Chờ bạn duyệt')
        await cardEl.click()
        await qa.expect(qa.page).toHaveURL(/scope=todo/)
        await qa.expect(qa.page.getByRole('link', { name: m.title, exact: true })).toBeVisible()
        await snap(qa, 'can-xu-ly')
        // A Member with nothing waiting.
        await qa.signIn('member')
        await qa.goto('/mgmt/meetings?scope=todo')
        const text = (await qa.page.locator('main').innerText()).replace(/\s+/g, ' ')
        qa.note(`thanhvien@ scope=todo: ${text.slice(0, 300)}`)
        await snap(qa, 'thanh-vien-khong-co-viec')
      },
    },
    {
      id: 'MEET_FT15',
      role: 'secretary',
      title: 'Bộ lọc "Thời điểm" chỉ có các lựa chọn cố định',
      steps: ['Mở /mgmt/meetings?status=all, "Bộ lọc", mở "Thời điểm"'],
      expected: 'Mọi thời điểm, Hôm nay, Tuần này, Chưa diễn ra, Đã diễn ra; không có chọn khoảng ngày; upcoming / past',
      async run(qa) {
        const lists = []
        qa.page.on('request', (r) => {
          const u = new URL(r.url())
          if (u.pathname === '/api/meetings') lists.push(u.search)
        })
        await qa.goto('/mgmt/meetings?status=all')
        // Desktop shows the filters inline; a phone folds them behind "Bộ lọc".
        const toggle = qa.page.getByRole('button', { name: /Bộ lọc/ })
        if (await toggle.isVisible().catch(() => false)) await toggle.click()
        const when = qa.page.getByRole('button', { name: /Thời điểm|Mọi thời điểm/ }).first()
        await when.click()
        const box = qa.page.getByRole('listbox').last()
        await qa.expect(box).toBeVisible()
        const options = (await box.getByRole('option').allInnerTexts()).map((t) => t.trim())
        qa.note(`Lựa chọn: ${options.join(' | ')}`)
        await snap(qa, 'loc-thoi-diem')
        qa.expect(options).toEqual(['Mọi thời điểm', 'Hôm nay', 'Tuần này', 'Chưa diễn ra', 'Đã diễn ra'])
        qa.expect(await qa.page.locator('input[type="date"]').count()).toBe(0)
        await box.getByRole('option', { name: 'Chưa diễn ra' }).click()
        await qa.expect.poll(() => lists.some((s) => s.includes('timing=upcoming'))).toBe(true)
        await qa.page.getByRole('button', { name: /Chưa diễn ra/ }).first().click()
        await qa.page.getByRole('listbox').last().getByRole('option', { name: 'Đã diễn ra' }).click()
        await qa.expect.poll(() => lists.some((s) => s.includes('timing=past'))).toBe(true)
        qa.note(`Yêu cầu: ${lists.slice(-3).join(' ; ')}`)
      },
    },
    {
      id: 'MEET_FT16',
      role: 'secretary',
      title: 'Phân trang giữ bộ lọc, quay lại từ chi tiết về đúng trang',
      steps: ['/mgmt/meetings?status=all có hơn 20 cuộc họp, sang trang 2', 'Mở một cuộc họp rồi bấm "‹ Cuộc họp"'],
      expected: 'Trang 2 có page=2, giữ bộ lọc; quay lại đúng bộ lọc và trang',
      async run(qa) {
        const sec = await client(qa, 'secretary')
        let total = (await sec.get('/api/meetings?status=all&sensitivity=Normal&pageSize=1')).body.totalCount
        while (total <= 22) {
          await meetingAt(qa, 'Draft', { label: 'FT16 phân trang' })
          total += 1
        }
        await qa.goto('/mgmt/meetings?status=all&sensitivity=Normal')
        const first = await qa.page.locator('tbody tr').first().innerText()
        const pager = qa.page.locator('.pagination, nav[aria-label*="trang" i]').first()
        qa.note(`Phân trang: ${(await qa.page.locator('main').getByRole('button').evaluateAll((bs) => bs.map((b) => b.getAttribute('aria-label') || b.innerText).filter((t) => /^\d|trang/i.test(t)))).join(' | ')}`)
        await qa.page.getByRole('button', { name: /^(Trang )?2$/ }).or(pager.getByText('2', { exact: true })).first().click()
        await qa.expect(qa.page).toHaveURL(/page=2/)
        await qa.expect(qa.page).toHaveURL(/sensitivity=Normal/)
        await qa.expect.poll(async () => qa.page.locator('tbody tr').first().innerText()).not.toBe(first)
        const url2 = qa.page.url()
        await snap(qa, 'trang-2')
        await qa.page.locator('tbody tr a').first().click()
        await qa.expect(qa.page).toHaveURL(/\/mgmt\/meetings\/[0-9a-f-]{36}/)
        // The back link above the title, not the sidebar item of the same name.
        await qa.page.locator('main').getByRole('link', { name: 'Cuộc họp', exact: true }).first().click()
        await qa.expect(qa.page).toHaveURL(/page=2/)
        qa.note(`Trang 2: ${url2} → quay lại: ${qa.page.url()}`)
        qa.expect(new URL(qa.page.url()).search).toBe(new URL(url2).search)
        await snap(qa, 'quay-lai-trang-2')
      },
    },
    {
      id: 'MEET_FT20',
      role: 'member',
      title: 'Họp đang diễn ra có nút "Vào phòng họp"',
      steps: ['Thành viên mở /mgmt/meetings có họp trực tuyến đang diễn ra', 'Bấm "Vào phòng họp"'],
      expected: 'Nhãn "Đang diễn ra", cột thao tác có "Vào phòng họp"; họp trực tiếp có nút mắt "Xem chi tiết"; nút mở /room',
      async run(qa) {
        const online = await meetingAt(qa, 'InProgress', { label: 'FT20 trực tuyến', over: { mode: 'Online', location: null } })
        const inPerson = await meetingAt(qa, 'InProgress', { label: 'FT20 trực tiếp' })
        await qa.goto('/mgmt/meetings?status=InProgress')
        const row = qa.page.locator('tbody tr').filter({ hasText: online.title })
        await qa.expect(row.getByText('Đang diễn ra')).toBeVisible()
        await qa.expect(row.getByText('Vào phòng họp')).toBeVisible()
        const row2 = qa.page.locator('tbody tr').filter({ hasText: inPerson.title })
        await qa.expect(row2.getByRole('link', { name: `Xem chi tiết ${inPerson.title}` }).or(row2.getByRole('button', { name: `Xem chi tiết ${inPerson.title}` }))).toBeVisible()
        await snap(qa, 'dong-dang-dien-ra')
        // The room opens in a tab of its own (features/calls/TabLinks.tsx).
        const popup = qa.context.waitForEvent('page', { timeout: 10_000 }).catch(() => null)
        await row.getByText('Vào phòng họp').click()
        const tab = (await popup) ?? qa.page
        await tab.waitForLoadState()
        qa.note(`Mở ở ${tab === qa.page ? 'cùng tab' : 'tab mới'}: ${tab.url()}`)
        qa.expect(tab.url()).toMatch(new RegExp(`/mgmt/meetings/${online.id}/room`))
        await tab.screenshot({ path: `${process.cwd()}/out/ft20-room-tab.png` }).catch(() => undefined)
        await snap(qa, 'sau-khi-bam')
      },
    },
    {
      id: 'MEET_FT25',
      role: 'secretary',
      title: 'Cuộc họp Chủ toạ quên kết thúc: lịch sử có dòng hệ thống tự kết thúc',
      steps: ['Mở cuộc họp đã bị hệ thống tự kết thúc (quá 24 giờ sau giờ kết thúc dự kiến)'],
      expected: 'Nhãn "Đã kết thúc"; dòng mới nhất "Tự kết thúc (quá giờ)" bởi "Hệ thống"; phòng họp không mở cuộc gọi',
      async run(qa) {
        const id = process.env.QA_OVERDUE_ID
        if (!id) qa.inconclusive('Chưa có cuộc họp quá giờ (QA_OVERDUE_ID)')
        const m = (await (await client(qa, 'secretary')).get(`/api/meetings/${id}`)).body
        if (m.status !== 'Ended') qa.inconclusive(`Cuộc họp còn ${m.status}: job quá giờ chưa chạy`)
        await openMeeting(qa, m)
        await qa.expect(qa.page.locator('.record-title').getByText('Đã kết thúc', { exact: true })).toBeVisible()
        const history = qa.page.getByRole('region', { name: 'Lịch sử trạng thái' }).or(qa.page.locator('section, aside').filter({ hasText: 'Lịch sử trạng thái' })).first()
        await qa.expect(history.getByText('Tự kết thúc (quá giờ)')).toBeVisible()
        qa.note(`Lịch sử: ${(await history.innerText()).replace(/\s+/g, ' ')}`)
        await snap(qa, 'tu-ket-thuc')
        const join = await (await client(qa, 'chair')).post(`/api/meetings/${id}/call/join`)
        qa.note(`call/join sau khi tự kết thúc: ${join.status} ${join.body?.code ?? ''}`)
        qa.expect(join.status).not.toBe(200)
        const hist = (await (await client(qa, 'secretary')).get(`/api/meetings/${id}/history`)).body.items
        qa.note(`API lịch sử mới nhất: ${JSON.stringify(hist.at(-1) ?? hist[0]).slice(0, 300)}`)
      },
    },
    {
      id: 'MEET_FT28',
      role: 'secretary',
      title: 'Cuộc họp thiếu thông tin: thanh liệt kê điều còn thiếu, nút "Gửi duyệt" khoá',
      steps: ['Bản nháp cũ không có lịch hợp lệ (sửa trong database)', 'Đọc thanh, trỏ vào "Gửi duyệt"'],
      expected: '"Cần hoàn thiện trước khi gửi duyệt:" + thông báo backend; nút khoá, chú thích "Hoàn thiện thông tin trước khi gửi duyệt"; API 400 MSG81',
      async run(qa) {
        const m = await meetingAt(qa, 'Draft', { label: 'FT28 thiếu lịch' })
        // A legacy row the form could never save: the end before the start.
        sql(`update meetings set planned_end_time = planned_start_time - interval '1 hour' where id = '${m.id}'`)
        await openMeeting(qa, m)
        await qa.expect(qa.page.getByText('Cần hoàn thiện trước khi gửi duyệt:')).toBeVisible()
        const submit = qa.page.getByRole('button', { name: 'Gửi duyệt' }).first()
        await submit.hover()
        await qa.expect(qa.page.getByRole('tooltip')).toContainText('Hoàn thiện thông tin trước khi gửi duyệt')
        const bar = qa.page.getByText('Cần hoàn thiện trước khi gửi duyệt:').locator('xpath=..')
        qa.note(`Thanh: ${(await bar.innerText()).replace(/\s+/g, ' ')}`)
        await snap(qa, 'thieu-thong-tin')
        const u = await users(qa)
        const res = await (await client(qa, 'secretary')).post(`/api/meetings/${m.id}/transitions`, { action: 'SubmitForApproval', assignedApproverId: u.chair.id })
        qa.note(`API gửi duyệt: ${res.status} ${res.body?.code} ${JSON.stringify(res.body).slice(0, 200)}`)
        qa.expect(res.status).toBe(400)
        qa.expect(res.body.code).toBe('MSG81')
      },
    },
    {
      id: 'MEET_FT32',
      role: 'chair',
      title: 'Người duyệt không được giao không có nút Duyệt / Từ chối; Thành viên thấy trang không tìm thấy',
      steps: ['Phê duyệt viên thứ hai (không được giao) mở cuộc họp chờ duyệt bằng đường dẫn', 'Thành viên của cuộc họp mở đường dẫn đó'],
      expected: 'Người duyệt không liên quan: 404; người đọc được thì không có Duyệt/Từ chối; Thành viên: 404 vì chưa công bố',
      async run(qa) {
        const other = await makeUser(qa, 'duyet-2', 'Phê duyệt viên')
        const m = await meetingAt(qa, 'WaitingApproval', { label: 'FT32 giao chutoa' })
        await qa.signIn(other.email)
        await qa.goto(`/mgmt/meetings/${m.id}`)
        const text1 = (await qa.page.locator('main').innerText()).replace(/\s+/g, ' ')
        qa.note(`Người duyệt không được giao: ${text1.slice(0, 200)}`)
        await snap(qa, 'nguoi-duyet-khong-duoc-giao')
        const seesIt = await qa.page.getByRole('heading', { level: 1, name: m.title }).count()
        if (seesIt) {
          await qa.expect(qa.page.getByRole('button', { name: 'Duyệt', exact: true })).toHaveCount(0)
          await qa.expect(qa.page.getByRole('button', { name: 'Từ chối', exact: true })).toHaveCount(0)
        } else await qa.expect(qa.page.getByRole('heading', { name: 'Không tìm thấy trang' })).toBeVisible()
        await qa.signIn('member')
        await qa.goto(`/mgmt/meetings/${m.id}`)
        await qa.expect(qa.page.getByRole('heading', { name: 'Không tìm thấy trang' })).toBeVisible()
        await snap(qa, 'thanh-vien-404')
      },
    },
    {
      id: 'MEET_FT34',
      role: 'secretary',
      title: 'Công bố bị từ chối khi không có Thư ký',
      steps: ['Cuộc họp đã duyệt, Thư ký đã bị bỏ khỏi danh sách', 'Bấm "Công bố" và xác nhận'],
      expected: '409 MSG137; hộp đóng, toast thông báo; vẫn "Đã duyệt"',
      async run(qa) {
        const m = await meetingAt(qa, 'Approved', { label: 'FT34 không thư ký' })
        // The organizer (thuky@) is also the Secretary: take that assignment away in the database.
        const n = sql(`update meeting_participants set role = 'Member' where meeting_id = '${m.id}' and role = 'Secretary' returning id`)
        qa.note(`Đổi Thư ký thành Thành viên (SQL): ${n.split('\n').length} dòng`)
        await openMeeting(qa, m)
        const response = qa.page.waitForResponse((r) => r.request().method() === 'POST' && r.url().includes('/transitions'))
        await qa.page.getByRole('button', { name: 'Công bố' }).first().click()
        const confirm = qa.page.getByRole('dialog')
        await confirm.getByRole('button', { name: /Công bố/ }).click()
        const res = await response
        const body = await res.json().catch(() => ({}))
        qa.note(`POST transitions: ${res.status()} ${body.code} ${body.message}`)
        qa.expect(res.status()).toBe(409)
        qa.expect(body.code).toBe('MSG137')
        await qa.expect(toast(qa, 'Cuộc họp cần có đúng một Chủ toạ đang hoạt động và ít nhất một Thư ký đang hoạt động trước khi công bố.')).toBeVisible()
        await qa.expect(qa.page.getByRole('dialog')).toHaveCount(0)
        await qa.expect(qa.page.locator('.record-title').getByText('Đã duyệt', { exact: true })).toBeVisible()
        await snap(qa, 'cong-bo-bi-tu-choi')
      },
    },
    {
      id: 'MEET_FT37',
      role: 'chair',
      title: 'Không bắt đầu được khi không có Thư ký',
      steps: ['Cuộc họp đã công bố, Thư ký đã bị bỏ', 'Chủ toạ bấm "Bắt đầu họp"'],
      expected: 'Hộp "Chưa thể bắt đầu": "Cuộc họp cần ít nhất một Thư ký." và hướng dẫn; bấm tiếp → 409 MSG112; vẫn Đã công bố',
      async run(qa) {
        const m = await meetingAt(qa, 'Published', { label: 'FT37 không thư ký' })
        sql(`update meeting_participants set role = 'Member' where meeting_id = '${m.id}' and role = 'Secretary'`)
        await openMeeting(qa, m)
        await qa.page.getByRole('button', { name: 'Bắt đầu họp' }).first().click()
        const dlg = dialog(qa, 'Chưa thể bắt đầu')
        await qa.expect(dlg).toBeVisible()
        await qa.expect(dlg.getByText('Cuộc họp cần ít nhất một Thư ký.')).toBeVisible()
        qa.note(`Hộp: ${(await dlg.innerText()).replace(/\s+/g, ' ')}`)
        await snap(qa, 'chua-the-bat-dau')
        const startBtn = dlg.getByRole('button', { name: 'Bắt đầu họp' })
        if (await startBtn.count()) qa.note(`Nút "Bắt đầu họp" trong hộp: ${(await startBtn.isEnabled()) ? 'bấm được' : 'bị khoá'}`)
        if ((await startBtn.count()) && (await startBtn.isEnabled())) {
          const response = qa.page.waitForResponse((r) => r.request().method() === 'POST' && r.url().includes('/transitions'))
          await startBtn.click({ force: true })
          const res = await response
          const body = await res.json().catch(() => ({}))
          qa.note(`POST Start: ${res.status()} ${body.code} ${body.message}`)
          qa.expect(res.status()).toBe(409)
          qa.expect(body.code).toBe('MSG112')
        } else qa.note('Hộp không có nút "Bắt đầu họp"')
        await snap(qa, 'sau-khi-bam')
        const api = await (await client(qa, 'chair')).post(`/api/meetings/${m.id}/transitions`, { action: 'Start' })
        qa.note(`API Start: ${api.status} ${api.body?.code} ${api.body?.message}`)
        qa.expect(api.status).toBe(409)
        qa.expect(api.body.code).toBe('MSG112')
        const got = (await (await client(qa, 'chair')).get(`/api/meetings/${m.id}`)).body
        qa.expect(got.status).toBe('Published')
      },
    },

    // --------------------------------------------------------------------------------------------- FE-02/03 admin
    {
      id: 'USER_FT27',
      role: 'admin',
      title: 'Hạ vai trò quản trị viên cuối cùng bị từ chối với lời rõ ràng',
      steps: ['/mgmt/admin/users, Sửa dòng của mình (quản trị viên duy nhất), chọn "Thành viên", "Lưu thay đổi"'],
      expected: '409 MSG12; hộp báo "Không thể đổi vai trò: …" và vẫn mở; vai trò không đổi',
      async run(qa) {
        const u = await users(qa)
        const admins = sql(`select count(*) from users u join role_capabilities rc on rc.role_id = u.role_id where rc.capability = 'AdminUsers' and u.is_active`)
        if (admins !== '1') qa.inconclusive(`Có ${admins} tài khoản quản trị đang hoạt động`)
        await qa.goto('/mgmt/admin/users')
        const row = qa.page.locator('tbody tr').filter({ hasText: u.admin.email })
        await row.getByRole('button', { name: /Sửa/ }).first().click()
        const dlg = qa.page.getByRole('dialog')
        await qa.expect(dlg).toBeVisible()
        await dlg.getByRole('button', { name: /Vai trò|Quản trị viên/ }).first().click()
        await qa.page.getByRole('listbox').last().getByRole('option', { name: 'Thành viên' }).click()
        const response = qa.page.waitForResponse((r) => r.request().method() === 'PUT' && /\/api\/users\//.test(r.url()))
        await dlg.getByRole('button', { name: 'Lưu thay đổi' }).click()
        const res = await response
        const body = await res.json().catch(() => ({}))
        qa.note(`PUT: ${res.status()} ${body.code} ${body.message}`)
        qa.expect(res.status()).toBe(409)
        qa.expect(body.code).toBe('MSG12')
        await qa.expect(dlg.getByText('Không thể đổi vai trò: sau thao tác này sẽ không còn tài khoản quản trị nào đang hoạt động.')).toBeVisible()
        await snap(qa, 'khong-the-ha-quan-tri')
        await qa.goto('/mgmt/admin/users')
        await qa.expect(qa.page.locator('tbody tr').filter({ hasText: u.admin.email }).first()).toContainText('Quản trị viên')
        qa.expect(sql(`select r.name from users u join roles r on r.id = u.role_id where u.id = '${u.admin.id}'`)).toBe('Quản trị viên')
      },
    },
    {
      id: 'RBAC_FT01',
      role: 'admin',
      title: 'Ma trận vai trò × quyền, nhóm theo chức năng',
      steps: ['/mgmt/admin/roles, tab "Ma trận phân quyền"'],
      expected: 'Cột "Vai trò" + mỗi quyền một cột dưới nhóm Cuộc họp / Biên bản / Quản trị; tiêu đề có (i); chip "Mặc định", "Vai trò của bạn"; ô tick đúng; tab danh sách có số vai trò',
      async run(qa) {
        await qa.goto('/mgmt/admin/roles')
        const listTab = qa.page.getByRole('tab', { name: /Danh sách/ })
        qa.note(`Tab danh sách: ${(await listTab.innerText()).replace(/\s+/g, ' ')}`)
        await qa.page.getByRole('tab', { name: /Ma trận/ }).click()
        const table = qa.page.locator('table').first()
        await qa.expect(table).toBeVisible()
        // The column headings are uppercased by CSS: compare the DOM text.
        const head = (await table.locator('thead').textContent()).replace(/\s+/g, ' ')
        qa.note(`Tiêu đề: ${head}`)
        for (const t of ['Vai trò', 'Tạo cuộc họp', 'Phê duyệt cuộc họp', 'Thu giọng mẫu hộ', 'Quản trị người dùng']) qa.expect(head.toLowerCase()).toContain(t.toLowerCase())
        qa.note(`Nhóm "Biên bản" / quyền "Soạn biên bản": ${/biên bản/i.test(head) ? 'có' : 'không có'}`)
        const tips = await table.locator('thead [data-tooltip], thead button, thead .info-tip').count()
        qa.note(`Số (i) ở tiêu đề: ${tips}`)
        const body = (await table.locator('tbody').innerText()).replace(/\s+/g, ' ')
        qa.note(`Dòng: ${body.slice(0, 400)}`)
        qa.expect(body).toContain('Mặc định')
        qa.expect(body).toContain('Vai trò của bạn')
        await snap(qa, 'ma-tran', { fullPage: true })
        const caps = sql(`select r.name || ':' || string_agg(rc.capability, ',' order by rc.capability) from roles r left join role_capabilities rc on rc.role_id = r.id group by r.name order by r.name`)
        qa.note(`Database: ${caps.replace(/\n/g, ' ; ')}`)
        const checked = await table.locator('tbody tr').evaluateAll((rows) =>
          rows.map((r) => `${r.querySelector('th, td')?.innerText.split('\n')[0]}:${[...r.querySelectorAll('input[type=checkbox]')].map((c) => (c.checked ? 1 : 0)).join('')}`),
        )
        qa.note(`Ô tick: ${checked.join(' ; ')}`)
      },
    },
    {
      id: 'RBAC_FT06',
      role: 'admin',
      title: 'Máy chủ từ chối lưu cấu hình làm mất quản trị viên, dòng báo lý do',
      steps: ['Tab Ma trận phân quyền, bỏ tick "Quản trị người dùng" của dòng duy nhất có nó, bấm "Lưu"'],
      expected: '400 MSG17; dòng hiện lời đỏ; ô bị viền lỗi; tải lại không đổi',
      async run(qa) {
        await qa.goto('/mgmt/admin/roles')
        await qa.page.getByRole('tab', { name: /Ma trận/ }).click()
        const table = qa.page.locator('table').first()
        const row = table.locator('tbody tr').filter({ hasText: 'Quản trị viên' }).first()
        const col = await table.locator('thead th').evaluateAll((ths) => ths.findIndex((th) => th.innerText.includes('Quản trị người dùng')))
        qa.note(`Cột "Quản trị người dùng": ${col}`)
        const box = row.getByRole('checkbox', { name: /Quản trị người dùng/ }).first()
        await qa.expect(box).toBeChecked()
        await box.click()
        const response = qa.page.waitForResponse((r) => r.request().method() === 'PUT' && r.url().includes('/api/roles'))
        await qa.page.getByRole('button', { name: /^Lưu/ }).first().click()
        const res = await response
        const body = await res.json().catch(() => ({}))
        qa.note(`PUT: ${res.status()} ${body.code} ${JSON.stringify(body).slice(0, 300)}`)
        qa.expect(res.status()).toBe(400)
        qa.expect(body.code).toBe('MSG17')
        await qa.expect(qa.page.getByText(/sau khi lưu sẽ không còn tài khoản đang hoạt động nào giữ quyền quản trị người dùng/)).toBeVisible()
        await snap(qa, 'loi-mat-quan-tri', { fullPage: true })
        await qa.goto('/mgmt/admin/roles')
        await qa.page.getByRole('tab', { name: /Ma trận/ }).click()
        await qa.expect(qa.page.locator('table tbody tr').filter({ hasText: 'Quản trị viên' }).first().getByRole('checkbox', { name: /Quản trị người dùng/ }).first()).toBeChecked()
      },
    },

    // ------------------------------------------------------------------------------------------------ FE-12 minutes
    {
      id: 'MIN_FT03',
      role: 'secretary',
      title: 'Chưa bắt đầu họp thì không tạo được bản nháp biên bản',
      steps: ['Thư ký mở tab "Biên bản" của cuộc họp Đã công bố', 'Gọi API tạo bản nháp'],
      expected: 'Không có "Tạo bản nháp"; API 409 MSG398',
      async run(qa) {
        const m = await meetingAt(qa, 'Published', { label: 'FT03 biên bản chưa họp' })
        await qa.goto(`/mgmt/meetings/${m.id}?tab=minutes`)
        await qa.expect(qa.page.getByRole('heading', { level: 1, name: m.title })).toBeVisible()
        const tab = qa.page.getByRole('tab', { name: 'Biên bản' })
        qa.note(`Tab Biên bản: aria-disabled=${await tab.getAttribute('aria-disabled')}; mô tả "${await tab.getAttribute('aria-describedby') ? await qa.page.evaluate((el) => document.getElementById(el.getAttribute('aria-describedby'))?.textContent, await tab.elementHandle()) : ''}"`)
        await qa.expect(qa.page.getByRole('button', { name: 'Tạo bản nháp' })).toHaveCount(0)
        await snap(qa, 'tab-bien-ban-chua-hop')
        const res = await (await client(qa, 'secretary')).post(MIN(m.id), { rebuild: false })
        qa.note(`API: ${res.status} ${res.body?.code} ${res.body?.message}`)
        qa.expect(res.status).toBe(409)
        qa.expect(res.body.code).toBe('MSG398')
      },
    },
    {
      id: 'MIN_FT08',
      role: 'secretary',
      title: 'Người ngoài cuộc họp không mở được biên bản',
      steps: ['Một người không trong cuộc họp mở /mgmt/meetings/{id}?tab=minutes của cuộc họp đã ban hành biên bản'],
      expected: '"Không tìm thấy trang"; không lộ gì về biên bản',
      async run(qa) {
        const m = await meetingAt(qa, 'Ended', { label: 'FT08 biên bản đã ban hành' })
        await minutesTo(qa, m.id, 'Issued')
        const outsider = await makeUser(qa, 'ngoai', 'Thành viên')
        await qa.signIn(outsider.email)
        await qa.goto(`/mgmt/meetings/${m.id}?tab=minutes`)
        await qa.expect(qa.page.getByRole('heading', { name: 'Không tìm thấy trang' })).toBeVisible()
        const text = await qa.page.locator('main').innerText()
        qa.note(`Trang: ${text.replace(/\s+/g, ' ')}`)
        qa.expect(text).not.toContain(m.title)
        await snap(qa, 'nguoi-ngoai-404')
        const api = await (await client(qa, outsider.email)).get(MIN(m.id))
        qa.note(`API GET minutes: ${api.status}`)
        qa.expect(api.status).toBe(404)
      },
    },
    {
      id: 'MIN_FT11',
      role: 'secretary',
      title: '"Huỷ" đóng trình soạn, không lưu',
      steps: ['Bấm "Sửa", gõ "Chưa lưu"', 'Bấm "Huỷ"', 'Tải lại trang'],
      expected: 'Trình soạn đóng, tờ biên bản không có "Chưa lưu", không có PUT; sau tải lại không đổi',
      async run(qa) {
        const m = await meetingAt(qa, 'Ended', { label: 'FT11 huỷ sửa biên bản' })
        const before = await minutesTo(qa, m.id, 'Draft')
        const puts = watch(qa, 'PUT', /\/api\/minutes\//)
        await openMinutes(qa, m)
        await minutesBtn(qa, 'Sửa').click()
        const editor = qa.page.locator('[contenteditable="true"]').first()
        await qa.expect(editor).toBeVisible()
        await editor.click()
        await qa.page.keyboard.type('Chưa lưu')
        await snap(qa, 'dang-sua')
        await qa.page.getByRole('button', { name: 'Huỷ', exact: true }).first().click()
        const confirm = qa.page.getByRole('dialog')
        if (await confirm.isVisible().catch(() => false)) {
          qa.note(`Hộp xác nhận khi huỷ: ${(await confirm.innerText()).replace(/\s+/g, ' ')}`)
          await confirm.getByRole('button', { name: /Bỏ|Huỷ thay đổi|Đồng ý|Không lưu/ }).first().click()
        }
        await qa.expect(qa.page.locator('[contenteditable="true"]')).toHaveCount(0)
        await qa.expect(minutesCard(qa).locator('article.minutes-paper')).not.toContainText('Chưa lưu')
        await snap(qa, 'da-huy')
        qa.expect(puts).toEqual([])
        await qa.page.reload()
        await qa.expect(minutesCard(qa).locator('article.minutes-paper')).toBeVisible()
        await qa.expect(minutesCard(qa).locator('article.minutes-paper')).not.toContainText('Chưa lưu')
        const after = await minutesOf(qa, 'secretary', m.id)
        qa.expect(after.updatedAt).toBe(before.updatedAt)
      },
    },
    {
      id: 'MIN_FT18',
      role: 'secretary',
      title: 'Chưa kết thúc họp thì không gửi duyệt biên bản',
      steps: ['Thư ký của cuộc họp Đang diễn ra có bản nháp biên bản mở tab "Biên bản"'],
      expected: 'Không gửi được trước khi kết thúc; API submit bị từ chối',
      async run(qa) {
        const m = await meetingAt(qa, 'InProgress', { label: 'FT18 gửi duyệt khi đang họp' })
        // The app builds minutes only once the meeting has ended (MSG398), so an InProgress meeting never has a draft.
        const build = await (await client(qa, 'secretary')).post(MIN(m.id), { rebuild: false })
        qa.note(`Tạo bản nháp khi đang họp: ${build.status} ${build.body?.code} ${build.body?.message}`)
        const d = build.status === 201 || build.status === 200 ? await minutesOf(qa, 'secretary', m.id) : null
        await qa.goto(`/mgmt/meetings/${m.id}?tab=minutes`)
        await qa.expect(qa.page.getByRole('heading', { level: 1, name: m.title })).toBeVisible()
        const tab = qa.page.getByRole('tab', { name: 'Biên bản' })
        const locked = (await tab.getAttribute('aria-disabled')) === 'true'
        qa.note(`Tab Biên bản khi đang họp: ${locked ? 'khoá' : 'mở'}`)
        if (!locked) {
          await qa.expect(minutesCard(qa)).toBeVisible()
          const send = minutesBtn(qa, 'Gửi duyệt')
          if (await send.count()) {
            await send.click()
            const dlg = qa.page.getByRole('dialog')
            qa.note(`Hộp: ${(await dlg.innerText()).replace(/\s+/g, ' ')}`)
            await qa.expect(dlg.getByText('Chỉ gửi duyệt biên bản sau khi cuộc họp kết thúc.')).toBeVisible()
          }
        } else await qa.expect(minutesCard(qa)).toHaveCount(0)
        await snap(qa, 'dang-hop-khong-gui-duyet')
        if (d) {
          const res = await (await client(qa, 'secretary')).post(`/api/minutes/${d.id}/submit`, { updatedAt: d.updatedAt })
          qa.note(`API submit: ${res.status} ${res.body?.code} ${res.body?.message}`)
          qa.expect(res.status).toBe(409)
        } else {
          qa.expect(build.status).toBe(409)
          qa.expect(build.body.code).toBe('MSG398')
        }
      },
    },
    {
      id: 'MIN_FT19',
      role: 'chair',
      title: 'Chủ toạ không có "Gửi duyệt"',
      steps: ['Chủ toạ mở tab "Biên bản" của cuộc họp Đã kết thúc có bản nháp'],
      expected: 'Có "Tải xuống", "Tạo lại", "Sửa"; không có "Gửi duyệt"',
      async run(qa) {
        const m = await meetingAt(qa, 'Ended', { label: 'FT19 chủ toạ bản nháp' })
        await minutesTo(qa, m.id, 'Draft')
        await openMinutes(qa, m)
        const buttons = (await minutesCard(qa).getByRole('button').allInnerTexts()).map((t) => t.trim()).filter(Boolean)
        qa.note(`Nút của Chủ toạ: ${buttons.join(' | ')}`)
        await snap(qa, 'chu-toa-ban-nhap')
        await qa.expect(minutesBtn(qa, 'Gửi duyệt')).toHaveCount(0)
        for (const b of ['Tải xuống', 'Tạo lại', 'Sửa']) qa.expect(buttons.join('|')).toContain(b)
      },
    },
    {
      id: 'MIN_FT21',
      role: 'secretary',
      title: 'Huỷ hộp gửi duyệt thì không gửi gì',
      steps: ['Thư ký bấm "Gửi duyệt" rồi "Huỷ"'],
      expected: 'Hộp đóng; vẫn "Bản nháp"; không có POST submit; Chủ toạ không nhận thông báo',
      async run(qa) {
        const m = await meetingAt(qa, 'Ended', { label: 'FT21 huỷ gửi duyệt' })
        await minutesTo(qa, m.id, 'Draft')
        const posts = watch(qa, 'POST', /\/submit$/)
        await openMinutes(qa, m)
        await minutesBtn(qa, 'Gửi duyệt').click()
        const dlg = qa.page.getByRole('dialog')
        await qa.expect(dlg).toBeVisible()
        await snap(qa, 'hop-gui-duyet')
        await dlg.getByRole('button', { name: 'Huỷ', exact: true }).click()
        await qa.expect(qa.page.getByRole('dialog')).toHaveCount(0)
        await qa.expect(minutesBadge(qa).getByText('Bản nháp', { exact: true })).toBeVisible()
        await snap(qa, 'sau-khi-huy')
        qa.expect(posts).toEqual([])
        qa.expect((await minutesOf(qa, 'secretary', m.id)).status).toBe('Draft')
        const notes = (await (await client(qa, 'chair')).get('/api/notifications?page=1&pageSize=50')).body.items.filter((n) => n.meetingId === m.id && /Minutes/i.test(n.type))
        qa.expect(notes).toEqual([])
      },
    },
    {
      id: 'MIN_FT23',
      role: 'chair',
      title: 'Ban hành biên bản: người đọc được báo, email mở đúng tab biên bản',
      steps: ['Chủ toạ "Duyệt và ban hành"', 'Thành viên mở chuông', 'Mở thư ban hành, bấm "Xem biên bản"'],
      expected: 'Thành viên có "Biên bản đã ban hành", Chủ toạ không; thư "Biên bản cuộc họp đã ban hành: <tên>" mở ?tab=minutes',
      async run(qa) {
        const u = await users(qa)
        const m = await meetingAt(qa, 'Ended', { label: 'FT23 ban hành' })
        await minutesTo(qa, m.id, 'WaitingApproval')
        await openMinutes(qa, m)
        await minutesBtn(qa, 'Duyệt và ban hành').click()
        const dlg = qa.page.getByRole('dialog')
        if (await dlg.isVisible().catch(() => false)) await dlg.getByRole('button', { name: /Duyệt và ban hành|Ban hành/ }).last().click()
        await qa.expect(toast(qa, 'Đã duyệt và ban hành biên bản.')).toBeVisible()
        await snap(qa, 'da-ban-hanh')
        const member = await second(qa, 'member')
        await member.goto('/mgmt/meetings')
        await member.getByRole('button', { name: /Thông báo/ }).first().click()
        const panel = member.getByRole('dialog').or(member.locator('.notification-panel')).first()
        await qa.expect(panel.getByText(m.title).first()).toBeVisible({ timeout: 15_000 })
        qa.note(`Chuông Thành viên: ${(await panel.innerText()).replace(/\s+/g, ' ').slice(0, 300)}`)
        await member.screenshot({ path: `${process.cwd()}/out/min23-member-bell.png` })
        const own = (await (await client(qa, 'chair')).get('/api/notifications?page=1&pageSize=50')).body.items.filter((n) => n.meetingId === m.id && /Issued/i.test(n.type))
        qa.note(`Thông báo ban hành của Chủ toạ: ${own.length}`)
        qa.expect(own.length).toBe(0)
        // The email, as the outbox holds it (SMTP is off on this database; the body is what the inbox would show).
        const mail = sql(`select subject || '|' || coalesce(html_body, body) from email_messages where to_address = '${u.member.email}' and meeting_id = '${m.id}' and subject like 'Biên bản%' order by created_at desc limit 1`)
        qa.note(`Thư: ${mail.slice(0, 160)}`)
        qa.expect(mail).toContain(`Biên bản cuộc họp đã ban hành: ${m.title}`)
        const link = mail.match(/https?:\/\/[^"'\s<>]+tab=minutes/)?.[0]
        qa.note(`Liên kết "Xem biên bản": ${link}`)
        qa.expect(link, 'no minutes link in the email').toBeTruthy()
        await member.goto(new URL(link).pathname + new URL(link).search)
        await qa.expect(member.getByRole('region', { name: 'Biên bản', exact: true })).toBeVisible()
        await qa.expect(member.getByRole('region', { name: 'Biên bản', exact: true }).getByRole('button', { name: 'Tải xuống' })).toBeVisible()
        await member.screenshot({ path: `${process.cwd()}/out/min23-member-link.png` })
      },
    },
    {
      id: 'MIN_FT24',
      role: 'secretary',
      title: 'Thư ký không duyệt được',
      steps: ['Thư ký mở tab "Biên bản" có biên bản Chờ duyệt'],
      expected: 'Nhãn "Chờ duyệt"; không có "Trả lại", "Duyệt và ban hành"',
      async run(qa) {
        const m = await meetingAt(qa, 'Ended', { label: 'FT24 thư ký chờ duyệt' })
        await minutesTo(qa, m.id, 'WaitingApproval')
        await openMinutes(qa, m)
        await qa.expect(minutesBadge(qa).getByText('Chờ duyệt', { exact: true })).toBeVisible()
        await qa.expect(minutesBtn(qa, 'Trả lại')).toHaveCount(0)
        await qa.expect(minutesBtn(qa, 'Duyệt và ban hành')).toHaveCount(0)
        await snap(qa, 'thu-ky-cho-duyet')
      },
    },
    {
      id: 'MIN_FT27',
      role: 'chair',
      title: 'Huỷ trả lại thì biên bản vẫn chờ duyệt',
      steps: ['Chủ toạ bấm "Trả lại", nhập lý do, bấm "Huỷ"'],
      expected: 'Hộp đóng; vẫn "Chờ duyệt"; không gửi gì; Thư ký không được báo',
      async run(qa) {
        const m = await meetingAt(qa, 'Ended', { label: 'FT27 huỷ trả lại' })
        await minutesTo(qa, m.id, 'WaitingApproval')
        const posts = watch(qa, 'POST', /\/reject$/)
        await openMinutes(qa, m)
        await minutesBtn(qa, 'Trả lại').click()
        const dlg = dialog(qa, 'Trả lại biên bản')
        await qa.expect(dlg).toBeVisible()
        await dlg.getByRole('textbox').fill('QA: bổ sung kết luận.')
        await snap(qa, 'hop-tra-lai')
        await dlg.getByRole('button', { name: 'Huỷ', exact: true }).click()
        await qa.expect(qa.page.getByRole('dialog')).toHaveCount(0)
        await qa.expect(minutesBadge(qa).getByText('Chờ duyệt', { exact: true })).toBeVisible()
        await snap(qa, 'van-cho-duyet')
        qa.expect(posts).toEqual([])
        qa.expect((await minutesOf(qa, 'chair', m.id)).status).toBe('WaitingApproval')
      },
    },
    {
      id: 'MIN_FT31',
      role: 'secretary',
      title: 'Bản đính chính ban hành thay bản gốc, bản gốc vẫn đọc được',
      steps: ['Thư ký sửa bản đính chính, lưu, "Gửi duyệt"', 'Chủ toạ "Duyệt và ban hành"', 'Thành viên mở tab, chọn "Bản gốc"'],
      expected: 'Toast ban hành; thông báo + thư "Bản đính chính…"; Thành viên đọc bản đính chính "Đã ban hành"; "Bản gốc" có "Đã thay thế", tải được',
      async run(qa) {
        const u = await users(qa)
        const m = await meetingAt(qa, 'Ended', { label: 'FT31 đính chính' })
        await minutesTo(qa, m.id, 'Issued')
        await minutesTo(qa, m.id, 'Draft')
        await openMinutes(qa, m)
        await minutesBtn(qa, 'Sửa').click()
        const editor = qa.page.locator('[contenteditable="true"]').first()
        await qa.expect(editor).toBeVisible()
        // Click at the end of the "Địa điểm" line, as a person would, and add the correction there.
        const line = editor.locator('p').filter({ hasText: 'Địa điểm' }).first()
        await line.click()
        await qa.page.keyboard.press('End')
        await qa.page.keyboard.type(' Đính chính: sửa tên dự án.')
        await qa.expect(editor).toContainText('Đính chính: sửa tên dự án.')
        await qa.page.getByRole('button', { name: 'Lưu', exact: true }).first().click()
        await qa.expect(toast(qa, 'Đã lưu biên bản.')).toBeVisible()
        await minutesBtn(qa, 'Gửi duyệt').click()
        await qa.page.getByRole('dialog').getByRole('button', { name: 'Gửi duyệt' }).click()
        await qa.expect(minutesBadge(qa)).toContainText(/chờ duyệt/i)
        qa.note(`Nhãn sau gửi duyệt: ${(await minutesBadge(qa).innerText()).replace(/\s+/g, ' ')}`)
        await snap(qa, 'dinh-chinh-cho-duyet')
        const chair = await second(qa, 'chair')
        await chair.goto(`/mgmt/meetings/${m.id}?tab=minutes`)
        const chairCard = chair.getByRole('region', { name: 'Biên bản', exact: true })
        await chairCard.getByRole('button', { name: 'Duyệt và ban hành', exact: true }).click()
        const cd = chair.getByRole('dialog')
        if (await cd.isVisible().catch(() => false)) await cd.getByRole('button', { name: /Duyệt và ban hành|Ban hành/ }).last().click()
        await qa.expect(chair.locator('.toast-stack').getByText('Đã duyệt và ban hành biên bản.')).toBeVisible()
        await chair.screenshot({ path: `${process.cwd()}/out/min31-chair.png` })
        const mail = sql(`select subject || '|' || coalesce(html_body, body) from email_messages where to_address = '${u.member.email}' and meeting_id = '${m.id}' order by created_at desc limit 1`)
        qa.note(`Thư mới nhất cho Thành viên: ${mail.slice(0, 200).replace(/\n/g, ' ')}`)
        qa.expect(mail).toMatch(/đính chính/i)
        const bell = (await (await client(qa, 'member')).get('/api/notifications?page=1&pageSize=50')).body.items.filter((n) => n.meetingId === m.id)
        qa.note(`Thông báo của Thành viên: ${bell.map((n) => n.title ?? n.type).join(' | ')}`)
        await qa.signIn('member')
        await openMinutes(qa, m)
        qa.note(`Nhãn Thành viên thấy: ${(await minutesBadge(qa).innerText()).replace(/\s+/g, ' ')}`)
        await qa.expect(minutesBadge(qa)).toContainText(/ban hành/i)
        await qa.expect(minutesCard(qa)).toContainText('Đính chính: sửa tên dự án.')
        await snap(qa, 'thanh-vien-ban-dinh-chinh')
        await minutesCard(qa).getByRole('button', { name: /Phiên bản biên bản/ }).click()
        await qa.page.getByRole('option', { name: /Bản gốc/ }).click()
        await qa.expect(minutesCard(qa).getByText('Đã thay thế', { exact: true })).toBeVisible()
        await qa.expect(minutesCard(qa)).not.toContainText('Đính chính: sửa tên dự án.')
        await qa.expect(minutesBtn(qa, 'Tải xuống')).toBeVisible()
        await snap(qa, 'ban-goc-da-thay-the')
      },
    },

    {
      id: 'PROBE_FIRSTCHAR',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Thăm dò: ký tự đầu tiên gõ sau Ctrl+Home (hoặc chạm đầu trang) trong trình soạn biên bản có bị mất không',
      steps: ['Mở Sửa biên bản, Ctrl+Home, gõ "Đính chính" rồi "ABC" ở lần mở khác'],
      expected: 'Chữ gõ còn nguyên ở đầu tài liệu',
      async run(qa) {
        const m = await meetingAt(qa, 'Ended', { label: 'probe gõ đầu trang' })
        await minutesTo(qa, m.id, 'Draft')
        const results = []
        for (const text of ['Đính chính', 'ABC xyz', 'Đính chính']) {
          await openMinutes(qa, m)
          await minutesBtn(qa, 'Sửa').click()
          const editor = qa.page.locator('[contenteditable="true"]').first()
          await qa.expect(editor).toBeVisible()
          if (qa.device === 'phone') await editor.locator('p').first().tap()
          else await editor.click()
          await qa.page.keyboard.press('Control+Home')
          await qa.page.keyboard.type(text)
          const first = await editor.evaluate((el) => el.innerText.trim().slice(0, 40))
          results.push(`"${text}" → "${first}"`)
          await snap(qa, `vua-go-${results.length}`)
          await qa.page.getByRole('button', { name: 'Huỷ', exact: true }).first().click()
          const confirm = qa.page.getByRole('dialog')
          if (await confirm.isVisible().catch(() => false)) await confirm.getByRole('button').last().click()
        }
        qa.note(results.join(' ; '))
        await snap(qa, 'sau-tham-do')
      },
    },

    // ------------------------------------------------------------------------------------------------ FE-05 agenda
    {
      id: 'AGENDA_FT03',
      role: 'secretary',
      title: 'Mục nghị sự hiện tài liệu của nó, mục không có tài liệu báo trống',
      steps: ['Mở tab "Nội dung cuộc họp"', 'Mở mục có hai tài liệu, rồi mục không có', 'Bấm tên một tài liệu'],
      expected: 'Mục đầu liệt kê tên tệp với biểu tượng; mục sau "Chưa có tài liệu."; tài liệu mở ở /documents/<id>, quay lại về tab nội dung',
      async run(qa) {
        const m = await meetingAt(qa, 'Published', { label: 'FT03 tài liệu mục nghị sự' })
        const sec = await client(qa, 'secretary')
        const A = `/api/meetings/${m.id}/agenda`
        const one = await sec.post(A, { title: 'Báo cáo tài chính quý IV', description: '', presenterId: null, presenterGuestInvitationId: null })
        const two = await sec.post(A, { title: 'Kế hoạch năm 2027', description: '', presenterId: null, presenterGuestInvitationId: null })
        qa.expectStatus(one, 201)
        qa.expectStatus(two, 201)
        const up = await multipart(qa, 'secretary', `${A}/${one.body.agendaItemId}/documents/upload`, {
          files: { name: 'bao-cao-tai-chinh.pdf', mimeType: 'application/pdf', buffer: pdf('Bao cao tai chinh') },
        })
        const up2 = await multipart(qa, 'secretary', `${A}/${one.body.agendaItemId}/documents/upload`, {
          files: { name: 'phu-luc-so-lieu.pdf', mimeType: 'application/pdf', buffer: pdf('Phu luc so lieu') },
        })
        qa.note(`Tải tài liệu vào mục: ${up.status} ${up.body?.code}; ${up2.status} ${up2.body?.code} ${up2.status >= 400 ? JSON.stringify(up2.body).slice(0, 200) : ''}`)
        qa.expect(up.status).toBe(200)
        qa.expect(up2.status).toBe(200)
        await openMeeting(qa, m, 'agenda')
        const item1 = qa.page.getByText('Báo cáo tài chính quý IV').first()
        await item1.click()
        await qa.expect(qa.page.getByText('bao-cao-tai-chinh.pdf').first()).toBeVisible()
        await qa.expect(qa.page.getByText('phu-luc-so-lieu.pdf').first()).toBeVisible()
        await snap(qa, 'muc-co-hai-tai-lieu', { fullPage: true })
        await qa.page.getByText('Kế hoạch năm 2027').first().click()
        await qa.expect(qa.page.getByText('Chưa có tài liệu.').first()).toBeVisible()
        await snap(qa, 'muc-chua-co-tai-lieu', { fullPage: true })
        const doc = qa.page.getByText('bao-cao-tai-chinh.pdf').first()
        if (!(await doc.isVisible())) await qa.page.getByText('Báo cáo tài chính quý IV').first().click()
        await doc.click()
        await qa.expect(qa.page).toHaveURL(new RegExp(`/mgmt/meetings/${m.id}/documents/[0-9a-f-]{36}`))
        await snap(qa, 'mo-tai-lieu')
        await qa.page.locator('main').getByRole('link').first().click()
        qa.note(`Quay lại: ${qa.page.url()}`)
        await qa.expect(qa.page).toHaveURL(/tab=agenda/)
      },
    },
    {
      id: 'AGENDA_FT20',
      role: 'chair',
      title: 'Chỉ một mục được là mục đang bàn',
      steps: ['Hai trình duyệt Chủ toạ cùng bấm "Bắt đầu" ở hai mục khác nhau'],
      expected: 'Một 200 MSG64, một 409 MSG72 "Đã có mục nghị sự đang diễn ra."; màn hình thua tải lại, thấy mục thắng "Đang bàn"',
      async run(qa) {
        const m = await meetingAt(qa, 'InProgress', { label: 'FT20 một mục đang bàn' })
        const sec = await client(qa, 'secretary')
        for (const t of ['Mục một', 'Mục hai', 'Mục ba'])
          qa.expectStatus(await sec.post(`/api/meetings/${m.id}/agenda`, { title: t, description: '', presenterId: null, presenterGuestInvitationId: null }), 201)
        const other = await second(qa, 'chair')
        await openMeeting(qa, m, 'agenda')
        await other.goto(`/mgmt/meetings/${m.id}?tab=agenda`)
        const startIn = (page, title) => page.locator('li, tr, .agenda-row, [class*="agenda-item"]').filter({ hasText: title }).getByRole('button', { name: /^Bắt đầu/ }).first()
        await qa.expect(startIn(qa.page, 'Mục một')).toBeVisible()
        await qa.expect(startIn(other, 'Mục hai')).toBeVisible()
        const responses = []
        const grab = (page) => page.on('response', async (r) => {
          if (r.request().method() === 'POST' && /\/current$/.test(r.url())) responses.push({ status: r.status(), body: await r.json().catch(() => ({})) })
        })
        grab(qa.page)
        grab(other)
        await Promise.all([startIn(qa.page, 'Mục một').click(), startIn(other, 'Mục hai').click()])
        await qa.expect.poll(() => responses.length, { timeout: 15_000 }).toBe(2)
        qa.note(`Hai lời gọi: ${responses.map((r) => `${r.status} ${r.body.code} ${r.body.message ?? ''}`).join(' ; ')}`)
        const codes = responses.map((r) => `${r.status}/${r.body.code}`).sort()
        qa.expect(codes).toEqual(['200/MSG64', '409/MSG72'])
        await qa.page.waitForTimeout(2000)
        await snap(qa, 'trinh-duyet-1', { fullPage: true })
        await other.screenshot({ path: `${process.cwd()}/out/agenda20-browser2.png`, fullPage: true })
        const current = (await sec.get(`/api/meetings/${m.id}/agenda`)).body.items.filter((i) => i.status === 'Current' || i.isCurrent)
        qa.note(`Mục đang bàn (API): ${current.map((i) => i.title).join(', ')}`)
        const winner = current[0]?.title
        for (const page of [qa.page, other]) await qa.expect(page.locator('main')).toContainText('Đang bàn')
        qa.note(`Mục thắng: ${winner}`)
      },
    },

    // ------------------------------------------------------------------------------------------------ FE-09 recording
    {
      id: 'REC_FT13',
      role: 'chair',
      title: 'Không ai tải được bản ghi của cuộc họp Mật',
      steps: ['Chủ toạ mở tab "Bản ghi" của họp Mật có một bản ghi', 'Gọi API tải về'],
      expected: 'Không có "Tải MP3" cho Chủ toạ và Thư ký, vẫn nghe được; API tải về 403 MSG356',
      async run(qa) {
        const m = await meetingAt(qa, 'InProgress', { label: 'FT13 họp Mật có bản ghi', over: { sensitivity: 'Secret' } })
        const id = await uploadRecording(qa, m.id)
        for (const role of ['chair', 'secretary']) {
          if (role === 'secretary') await qa.signIn('secretary')
          await openMeeting(qa, m, 'recordings')
          const row = qa.page.locator('li.recording-row').first()
          await qa.expect(row).toBeVisible()
          await qa.expect(qa.page.getByRole('button', { name: 'Tải MP3' })).toHaveCount(0)
          await qa.expect(row.locator('audio, [aria-label*="Phát"], button[aria-label*="Nghe"]').first()).toBeAttached()
          await snap(qa, `${role}-hop-mat-khong-tai`)
          const dl = await (await client(qa, role)).get(`/api/recordings/${id}/download`)
          qa.note(`${role} tải về: ${dl.status} ${dl.body?.code ?? ''}`)
          qa.expect(dl.status).toBe(403)
          qa.expect(dl.body.code).toBe('MSG356')
          const play = await (await client(qa, role)).get(`/api/recordings/${id}/playback`)
          qa.note(`${role} nghe: ${play.status}`)
        }
      },
    },

    {
      id: 'REC_FT01',
      role: 'secretary',
      title: 'Thư ký thấy bản ghi của cuộc họp: nguồn, độ dài, dung lượng, trình phát mở sẵn',
      steps: ['Họp trực tiếp Đã kết thúc có một bản ghi đã gỡ băng xong', 'Thư ký mở tab "Bản ghi"'],
      expected: 'GET recordings 200; dòng có tên tệp, "Tải lên"/"Mic phòng", độ dài, dung lượng, ngày lưu, "Có bản gỡ băng"; dòng mở sẵn với trình phát và bản gỡ băng',
      async run(qa) {
        const m = await meetingAt(qa, 'Ended', { label: 'REC01 có bản ghi' })
        const id = await uploadRecording(qa, m.id)
        const t = await waitTranscript(qa, id)
        qa.note(`Bản gỡ băng: ${t?.status} ${t?.errorMessage ?? ''} (${t?.segments?.length ?? 0} đoạn)`)
        const list = qa.page.waitForResponse((r) => r.request().method() === 'GET' && r.url().endsWith(`/meetings/${m.id}/recordings`))
        await openMeeting(qa, m, 'recordings')
        qa.expect((await list).status()).toBe(200)
        const row = qa.page.locator('li.recording-row').first()
        await qa.expect(row).toBeVisible()
        const text = (await row.innerText()).replace(/\s+/g, ' ')
        qa.note(`Dòng bản ghi: ${text}`)
        for (const want of ['qa-mr1.wav', 'Tải lên']) qa.expect(text).toContain(want)
        qa.expect(text).toMatch(/KB|MB/)
        qa.expect(text).toMatch(/\d:\d\d|giây/)
        if (t?.status === 'Completed') qa.expect(text).toContain('Có bản gỡ băng')
        await qa.expect(row.locator('audio, [class*="player"]').first()).toBeAttached({ timeout: 10_000 })
        await snap(qa, 'danh-sach-ban-ghi', { fullPage: true })
      },
    },
    {
      id: 'REC_FT36',
      role: 'member',
      title: 'Thành viên không có nút xoá lượt',
      steps: ['Bản ghi mic phòng ba lượt đã gỡ băng', 'Thành viên mở tab "Bản ghi"'],
      expected: 'Các vạch "Lượt 1", "Lượt 2"… có giờ, không có thùng rác',
      async run(qa) {
        const m = await meetingAt(qa, 'InProgress', { label: 'REC36 ba lượt' })
        const id = await roomTake(qa, m.id, { consent: true, speech: 't1.wav' })
        await roomTake(qa, m.id, { speech: 't2.wav' })
        await roomTake(qa, m.id, { speech: 't3.wav' })
        const t = await waitTranscript(qa, id)
        qa.note(`Bản gỡ băng: ${t?.status} ${t?.errorMessage ?? ''} (${t?.segments?.length ?? 0} đoạn)`)
        const rec = (await (await client(qa, 'secretary')).get(`/api/meetings/${m.id}/recordings`)).body.items.find((r) => r.id === id)
        qa.note(`Lượt: ${JSON.stringify(rec?.takes)}`)
        if (t?.status !== 'Completed') qa.inconclusive(`Bản gỡ băng ${t?.status}: vạch lượt chỉ hiện khi gỡ băng xong`)
        await openMeeting(qa, m, 'recordings')
        const row = qa.page.locator('li.recording-row').filter({ hasText: '3 lượt ghi' })
        await qa.expect(row).toBeVisible()
        await qa.expect(row.getByText('Lượt 1', { exact: false }).first()).toBeVisible({ timeout: 15_000 })
        await qa.expect(row.getByText('Lượt 2', { exact: false }).first()).toBeVisible()
        await qa.expect(row.getByRole('button', { name: /Xoá lượt/ })).toHaveCount(0)
        await snap(qa, 'thanh-vien-khong-xoa-luot', { fullPage: true })
        qa.note(`Vạch lượt: ${(await row.locator('.transcript-list').innerText().catch(() => '')).replace(/\s+/g, ' ').slice(0, 200)}`)
      },
    },

    {
      id: 'REC_FT35',
      role: 'secretary',
      title: 'Thư ký xoá một lượt của bản ghi mic phòng',
      steps: ['Bản ghi mic phòng ba lượt đã gỡ băng', 'Bấm "Xoá lượt 2", trong hộp "Xoá lượt 2?" bấm "Xoá lượt"'],
      expected: 'Hộp báo tiếng và chữ của lượt bị xoá hẳn, các lượt khác giữ nguyên; DELETE takes/2 200 MSG366, toast "Đã xoá lượt ghi."; còn "2 lượt ghi"',
      async run(qa) {
        const m = await meetingAt(qa, 'Ended', { label: 'REC35 xoá lượt', over: {} })
        // Takes need a running meeting: drive a fresh one to InProgress, record, then end it.
        const live = await meetingAt(qa, 'InProgress', { label: 'REC35 xoá lượt' })
        const id = await roomTake(qa, live.id, { consent: true, speech: 't1.wav' })
        await roomTake(qa, live.id, { speech: 't2.wav' })
        await roomTake(qa, live.id, { speech: 't3.wav' })
        qa.expectStatus(await (await client(qa, 'chair')).post(`/api/meetings/${live.id}/transitions`, { action: 'End' }), 200)
        qa.note(`(Bỏ qua cuộc họp phụ ${m.id})`)
        const t = await waitTranscript(qa, id)
        qa.note(`Bản gỡ băng: ${t?.status} (${t?.segments?.length ?? 0} đoạn): ${JSON.stringify((t?.segments ?? []).map((s) => [s.startMs, s.text.slice(0, 25)]))}`)
        if (t?.status !== 'Completed' || !t.segments?.length) qa.inconclusive('Bản gỡ băng không có câu nào: vạch lượt không hiện')
        const del = qa.page.waitForResponse((r) => r.request().method() === 'DELETE' && /\/takes\/2$/.test(r.url()))
        const m2 = (await (await client(qa, 'secretary')).get(`/api/meetings/${live.id}`)).body
        await openMeeting(qa, m2, 'recordings')
        const row = qa.page.locator('li.recording-row').filter({ hasText: '3 lượt ghi' })
        await qa.expect(row).toBeVisible()
        await qa.expect(row.getByRole('button', { name: 'Xoá lượt 2' })).toBeVisible({ timeout: 15_000 })
        await snap(qa, 'ba-luot', { fullPage: true })
        await row.getByRole('button', { name: 'Xoá lượt 2' }).click()
        const dlg = dialog(qa, 'Xoá lượt 2?')
        await qa.expect(dlg.getByText('Tiếng và chữ của lượt này bị xoá hẳn, không lấy lại được.')).toBeVisible()
        qa.note(`Hộp: ${(await dlg.innerText()).replace(/\s+/g, ' ')}`)
        await qa.expect(dlg).toContainText('Các lượt khác giữ nguyên chữ đã sửa và tên đã gán.')
        await snap(qa, 'hoi-xoa-luot-2')
        await dlg.getByRole('button', { name: 'Xoá lượt' }).click()
        const res = await del
        const body = await res.json().catch(() => ({}))
        qa.note(`DELETE takes/2: ${res.status()} ${body.code}`)
        qa.expect(res.status()).toBe(200)
        qa.expect(body.code).toBe('MSG366')
        await qa.expect(toast(qa, 'Đã xoá lượt ghi.')).toBeVisible()
        await qa.expect(qa.page.locator('li.recording-row').filter({ hasText: '2 lượt ghi' })).toBeVisible({ timeout: 15_000 })
        await snap(qa, 'con-hai-luot', { fullPage: true })
      },
    },

    // ------------------------------------------------------------------------------------------------ D: email (outbox)
    {
      id: 'NOTI_FT22',
      role: 'secretary',
      title: 'Công bố cuộc họp gửi thư mời tới người được mời',
      steps: ['Người tổ chức bấm "Công bố", xác nhận', 'Mở hộp thư người được mời (bảng email_messages)'],
      expected: 'Thư "Lời mời tham dự cuộc họp" có thông tin họp, link /mgmt/meetings/<id>, tệp thu-moi-hop.ics; lệnh công bố không chờ gửi thư',
      async run(qa) {
        const u = await users(qa)
        const m = await meetingAt(qa, 'Approved', { label: 'NOTI22 công bố' })
        await openMeeting(qa, m)
        const t0 = Date.now()
        const response = qa.page.waitForResponse((r) => r.request().method() === 'POST' && r.url().includes('/transitions'))
        await qa.page.getByRole('button', { name: 'Công bố' }).first().click()
        await qa.page.getByRole('dialog').getByRole('button', { name: /Công bố/ }).click()
        const res = await response
        qa.note(`Công bố: ${res.status()} trong ${Date.now() - t0} ms`)
        qa.expect(res.status()).toBe(200)
        await qa.expect.poll(() => sql(`select count(*) from email_messages where meeting_id = '${m.id}' and to_address = '${u.member.email}'`), { timeout: 90_000 }).not.toBe('0')
        const mail = sql(`select subject || ' ¦ ics=' || (calendar_attachment is not null) || ' ¦ ' || status || ' ¦ ' || coalesce(html_body, body) from email_messages where meeting_id = '${m.id}' and to_address = '${u.member.email}' order by created_at limit 1`)
        qa.note(`Thư: ${mail.slice(0, 220).replace(/\n/g, ' ')}`)
        qa.expect(mail).toContain('ics=true')
        qa.expect(mail).toContain(`/mgmt/meetings/${m.id}`)
        qa.expect(mail).toMatch(/Lời mời tham dự cuộc họp/)
        await qa.expect.poll(async () => (await mailpit(u.member.email)).filter((x) => x.subject.includes(m.title)).length, { timeout: 90_000 }).toBeGreaterThan(0)
        const inbox = (await mailpit(u.member.email)).filter((x) => x.subject.includes(m.title))
        qa.note(`Mailpit (${u.member.email}): ${inbox.map((x) => `${x.subject} [${x.attachments} tệp]`).join(' | ')}`)
        const full = await (await fetch(`http://localhost:8025/api/v1/message/${inbox.at(-1).id}`)).json()
        qa.note(`Tệp đính kèm: ${(full.Attachments ?? []).map((a) => a.FileName).join(', ')}`)
        qa.expect((full.Attachments ?? []).map((a) => a.FileName)).toContain('thu-moi-hop.ics')
        const ics = sql(`select calendar_attachment from email_messages where meeting_id = '${m.id}' and to_address = '${u.member.email}' order by created_at limit 1`)
        qa.note(`ICS: ${ics.slice(0, 120).replace(/\r?\n/g, ' ')}`)
        const name = execFileSync('git', ['grep', '-n', 'thu-moi-hop.ics', '--', '../../backend'], { encoding: 'utf8' }).split('\n')[0]
        qa.note(`Tên tệp đính kèm trong code: ${name}`)
        await snap(qa, 'da-cong-bo')
      },
    },
    {
      id: 'NOTI_FT23',
      role: 'secretary',
      title: 'Dời lịch gửi thư "Cuộc họp đã dời lịch" tới mọi người dự',
      steps: ['Người tổ chức dời cuộc họp Đã công bố sang giờ mới', 'Mở hộp thư người dự'],
      expected: 'Mỗi người dự nhận "Cuộc họp đã dời lịch", giờ cũ gạch ngang cạnh giờ mới; ai trừ người tổ chức cũng có thông báo chuông',
      async run(qa) {
        const u = await users(qa)
        const m = await meetingAt(qa, 'Published', { label: 'NOTI23 dời lịch' })
        await openMeeting(qa, m)
        await qa.page.getByRole('link', { name: 'Dời lịch' }).first().click()
        const form = qa.page.getByRole('dialog')
        await qa.expect(form).toBeVisible()
        const start = form.getByLabel('Bắt đầu')
        const end = form.getByLabel('Kết thúc')
        const s = await start.inputValue()
        const [h, mm] = s.split(':').map(Number)
        const nh = (h + 2) % 22
        await end.fill(`${String(nh + 1).padStart(2, '0')}:${String(mm).padStart(2, '0')}`)
        await start.fill(`${String(nh).padStart(2, '0')}:${String(mm).padStart(2, '0')}`)
        await snap(qa, 'form-doi-lich')
        const response = qa.page.waitForResponse((r) => r.request().method() === 'PUT' && new URL(r.url()).pathname === `/api/meetings/${m.id}`)
        await form.getByRole('button', { name: /Lưu|Dời lịch|Cập nhật/ }).last().click()
        const confirm = qa.page.getByRole('dialog').filter({ hasText: /dời lịch/i })
        if (await confirm.getByRole('button', { name: /Dời lịch|Xác nhận/ }).count().catch(() => 0)) await confirm.getByRole('button', { name: /Dời lịch|Xác nhận/ }).last().click().catch(() => undefined)
        const res = await response
        const body = await res.json().catch(() => ({}))
        qa.note(`PUT: ${res.status()} ${body.code} ${body.message}`)
        qa.expect(body.code).toBe('MSG138')
        await snap(qa, 'da-doi-lich')
        for (const who of ['chair', 'member']) {
          await qa.expect.poll(() => sql(`select count(*) from email_messages where meeting_id = '${m.id}' and to_address = '${u[who].email}' and subject like 'Cuộc họp đã dời lịch%'`), { timeout: 90_000 }).not.toBe('0')
          const html = sql(`select subject || ' ¦ ' || coalesce(html_body, body) from email_messages where meeting_id = '${m.id}' and to_address = '${u[who].email}' and subject like 'Cuộc họp đã dời lịch%' limit 1`)
          qa.note(`${who}: ${html.slice(0, 120)}; gạch ngang: ${/<s>|<del>|<s |<del |line-through/.test(html)}`)
          qa.expect(/<s>|<del>|<s |<del |line-through/.test(html), 'old time not struck through').toBe(true)
        }
        const self = sql(`select count(*) from email_messages where meeting_id = '${m.id}' and to_address = '${u.secretary.email}' and subject like 'Cuộc họp đã dời lịch%'`)
        qa.note(`Thư dời lịch cho người tổ chức (thuky@, cũng là Thư ký): ${self}`)
        for (const who of ['chair', 'member', 'secretary']) {
          const n = (await (await client(qa, who)).get('/api/notifications?page=1&pageSize=50')).body.items.filter((x) => x.meetingId === m.id && /Reschedul/i.test(x.type))
          qa.note(`Chuông ${who}: ${n.length}`)
          qa.expect(n.length).toBe(who === 'secretary' ? 0 : 1)
        }
      },
    },
    {
      id: 'NOTI_FT24',
      role: 'secretary',
      title: 'Huỷ cuộc họp gửi thư huỷ, không có đường vào',
      steps: ['Người tổ chức huỷ cuộc họp Đã công bố, có lý do', 'Mở hộp thư người được mời'],
      expected: 'Thư "Cuộc họp đã bị huỷ" có tệp huy-hop.ics (METHOD:CANCEL), không có link vào cuộc họp',
      async run(qa) {
        const u = await users(qa)
        const m = await meetingAt(qa, 'Published', { label: 'NOTI24 huỷ' })
        // The cancellation goes to those who were sent the invitation: wait for it first.
        qa.expect(await mailText(u.member.email, new RegExp(m.title.replace(/[[\]]/g, '.'))), 'no invitation reached the inbox').toBeTruthy()
        await openMeeting(qa, m)
        await qa.page.getByRole('button', { name: 'Huỷ cuộc họp' }).first().click()
        const dlg = dialog(qa, 'Huỷ cuộc họp?')
        await dlg.getByRole('textbox').fill('QA: lãnh đạo đi công tác.')
        await dlg.getByRole('button', { name: 'Huỷ cuộc họp' }).click()
        await qa.expect(qa.page.locator('.record-title').getByText('Đã huỷ', { exact: true })).toBeVisible()
        await snap(qa, 'da-huy')
        await qa.expect.poll(() => sql(`select count(*) from email_messages where meeting_id = '${m.id}' and to_address = '${u.member.email}' and subject like 'Cuộc họp đã bị h%'`), { timeout: 90_000 }).not.toBe('0')
        const mail = sql(`select subject || ' ¦ ' || coalesce(html_body, body) from email_messages where meeting_id = '${m.id}' and to_address = '${u.member.email}' and subject like 'Cuộc họp đã bị h%' limit 1`)
        const ics = sql(`select coalesce(calendar_attachment, '') from email_messages where meeting_id = '${m.id}' and to_address = '${u.member.email}' and subject like 'Cuộc họp đã bị h%' limit 1`)
        qa.note(`Thư: ${mail.slice(0, 120)}; METHOD:CANCEL=${ics.includes('METHOD:CANCEL')}; link vào họp=${mail.includes(`/mgmt/meetings/${m.id}`)}`)
        qa.expect(mail).toContain('Cuộc họp đã bị huỷ')
        qa.expect(ics).toContain('METHOD:CANCEL')
        await qa.expect.poll(async () => (await mailpit(u.member.email)).filter((x) => /^Cuộc họp đã bị h/.test(x.subject) && x.subject.includes(m.title)).length, { timeout: 90_000 }).toBeGreaterThan(0)
        const hit = (await mailpit(u.member.email)).find((x) => /^Cuộc họp đã bị h/.test(x.subject) && x.subject.includes(m.title))
        const full = await (await fetch(`http://localhost:8025/api/v1/message/${hit.id}`)).json()
        qa.note(`Mailpit: ${hit.subject}; tệp: ${(full.Attachments ?? []).map((a) => a.FileName).join(', ')}`)
        qa.expect((full.Attachments ?? []).map((a) => a.FileName)).toContain('huy-hop.ics')
        qa.expect(mail).not.toContain(`/mgmt/meetings/${m.id}`)
      },
    },
    {
      id: 'USER_FT29',
      role: 'admin',
      title: 'Quản trị gửi liên kết đặt lại mật khẩu từ danh sách',
      steps: ['/mgmt/admin/users, bấm "Đặt lại mật khẩu" của một tài khoản, đọc hộp', 'Bấm "Gửi liên kết"'],
      expected: 'Hộp nói rõ gửi tới địa chỉ, dùng một lần trong 60 phút, link cũ hết hiệu lực, mật khẩu cũ vẫn dùng được tới khi đặt mới; 200 MSG13; toast; thư có liên kết',
      async run(qa) {
        const target = await makeUser(qa, 'datlai', 'Thành viên')
        qa.__resetTarget = target
        await qa.goto('/mgmt/admin/users')
        await qa.page.getByRole('button', { name: `Gửi liên kết đặt lại mật khẩu cho ${target.name}` }).first().click()
        const dlg = dialog(qa, 'Đặt lại mật khẩu')
        await qa.expect(dlg).toBeVisible()
        const text = (await dlg.innerText()).replace(/\s+/g, ' ')
        qa.note(`Hộp: ${text}`)
        await snap(qa, 'hop-dat-lai')
        for (const want of [target.email, '60 phút']) qa.expect(text).toContain(want)
        const response = qa.page.waitForResponse((r) => r.request().method() === 'POST' && r.url().endsWith('/password-reset'))
        await dlg.getByRole('button', { name: 'Gửi liên kết' }).click()
        const res = await response
        const body = await res.json().catch(() => ({}))
        qa.note(`POST: ${res.status()} ${body.code}`)
        qa.expect(body.code).toBe('MSG13')
        await qa.expect(toast(qa, 'Đã gửi email đặt lại mật khẩu.')).toBeVisible()
        const mail = await mailText(target.email, /./)
        qa.note(`Mailpit: ${mail?.subject} · ${(mail?.text ?? '').slice(0, 160).replace(/\n/g, ' ')}`)
        qa.expect(mail?.text ?? '').toMatch(/reset-password#token=/)
        process.env.QA_RESET_EMAIL = target.email
      },
    },
    {
      id: 'USER_FT31',
      role: 'anon',
      title: 'Người dùng đặt mật khẩu mới từ liên kết trong thư',
      steps: ['Mở liên kết trong thư', 'Gõ mật khẩu 5 ký tự và xác nhận khác, bấm "Đặt mật khẩu mới"', 'Gõ mật khẩu hợp lệ hai lần, bấm'],
      expected: 'Trang "Đặt lại mật khẩu" có hướng dẫn; bước 2 báo hai lỗi, không gửi; bước 3 200 MSG92, về /login; mật khẩu mới đăng nhập được, cũ thì không',
      async run(qa) {
        const email = process.env.QA_RESET_EMAIL
        if (!email) qa.inconclusive('Chạy USER_FT29 trước')
        const link = ((await mailText(email, /./))?.text ?? '').match(/https?:\/\/\S*reset-password#token=[A-Za-z0-9_.%-]+/)?.[0]
        qa.expect(link, 'no reset link in the email').toBeTruthy()
        process.env.QA_RESET_LINK = link
        const u = new URL(link)
        const posts = watch(qa, 'POST', /\/api\/auth\/password-reset$/)
        await qa.goto(u.pathname + u.hash)
        await qa.expect(qa.page.getByRole('heading', { name: 'Đặt lại mật khẩu' })).toBeVisible()
        qa.note(`Trang: ${(await qa.page.locator('main, body').first().innerText()).replace(/\s+/g, ' ').slice(0, 300)}`)
        await qa.expect(qa.page.getByText('Chọn mật khẩu mới cho tài khoản của bạn.')).toBeVisible()
        await qa.expect(qa.page.getByText('Đặt lại xong, mọi thiết bị đang đăng nhập tài khoản này sẽ bị đăng xuất.')).toBeVisible()
        await qa.page.getByLabel('Mật khẩu mới', { exact: true }).fill('abcde')
        await qa.page.getByLabel('Nhập lại mật khẩu mới').fill('khac12345')
        await qa.page.getByRole('button', { name: 'Đặt mật khẩu mới' }).click()
        await qa.expect(qa.page.getByText('Mật khẩu phải có ít nhất 12 ký tự.')).toBeVisible()
        await qa.expect(qa.page.getByText('Mật khẩu nhập lại không khớp.')).toBeVisible()
        await snap(qa, 'loi-mat-khau')
        qa.expect(posts).toEqual([])
        const fresh = 'MatKhauMoi@QA2026'
        await qa.page.getByLabel('Mật khẩu mới', { exact: true }).fill(fresh)
        await qa.page.getByLabel('Nhập lại mật khẩu mới').fill(fresh)
        const response = qa.page.waitForResponse((r) => r.request().method() === 'POST' && r.url().endsWith('/api/auth/password-reset'))
        await qa.page.getByRole('button', { name: 'Đặt mật khẩu mới' }).click()
        const res = await response
        const body = await res.json().catch(() => ({}))
        qa.note(`POST: ${res.status()} ${body.code} ${body.message}`)
        qa.expect(body.code).toBe('MSG92')
        await qa.expect(qa.page).toHaveURL(/\/login/)
        await snap(qa, 've-dang-nhap')
        const ctx = await playwrightRequest.newContext({ baseURL: qa.base })
        const okNew = (await ctx.post('/api/auth/login', { data: { email, password: fresh } })).status()
        const okOld = (await ctx.post('/api/auth/login', { data: { email, password: 'TrustCabinet@2026' } })).status()
        await ctx.dispose()
        qa.note(`Đăng nhập mật khẩu mới: ${okNew}; mật khẩu cũ: ${okOld}`)
        qa.expect(okNew).toBe(200)
        qa.expect(okOld).not.toBe(200)
      },
    },
    {
      id: 'USER_FT32',
      role: 'anon',
      title: 'Liên kết đã dùng báo liên kết hết hiệu lực',
      steps: ['Mở lại liên kết đã dùng, gõ mật khẩu hợp lệ, gửi'],
      expected: '400 MSG93; báo "Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn…" kèm gợi ý và "Về trang đăng nhập"',
      async run(qa) {
        const link = process.env.QA_RESET_LINK
        if (!link) qa.inconclusive('Chạy USER_FT31 trước')
        const u = new URL(link)
        await qa.goto(u.pathname + u.hash)
        const pw = 'MatKhauKhac@QA2026'
        await qa.page.getByLabel('Mật khẩu mới', { exact: true }).fill(pw)
        await qa.page.getByLabel('Nhập lại mật khẩu mới').fill(pw)
        const response = qa.page.waitForResponse((r) => r.request().method() === 'POST' && r.url().endsWith('/api/auth/password-reset'))
        await qa.page.getByRole('button', { name: 'Đặt mật khẩu mới' }).click()
        const res = await response
        const body = await res.json().catch(() => ({}))
        qa.note(`POST: ${res.status()} ${body.code}`)
        qa.expect(res.status()).toBe(400)
        qa.expect(body.code).toBe('MSG93')
        await qa.expect(qa.page.getByText('Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn. Vui lòng liên hệ quản trị viên.')).toBeVisible()
        await qa.expect(qa.page.getByText('Liên kết chỉ dùng được một lần trong 60 phút, và hết hiệu lực khi có liên kết mới hơn được gửi.')).toBeVisible()
        await qa.expect(qa.page.getByRole('link', { name: 'Về trang đăng nhập' })).toBeVisible()
        await snap(qa, 'lien-ket-het-hieu-luc')
      },
    },

    // ------------------------------------------------------------------------------------------------ E: Guest by email code
    {
      id: 'PART_FT13',
      role: 'secretary',
      title: 'Thư ký mời Khách bằng tên và email; thư mời tới hộp thư Khách',
      steps: ['Thư ký mời Khách (tên + email) vào cuộc họp Đã công bố', 'Mở hộp thư Khách'],
      expected: '201 MSG96; Khách có thư mời với link /guest/<id>#token=…',
      async run(qa) {
        const m = await meetingAt(qa, 'Published', { label: 'GUEST mời khách' })
        const email = `khach-${rand()}@example.test`
        await openMeeting(qa, m, 'attendees')
        await qa.page.getByRole('button', { name: 'Mời khách ngoài hệ thống' }).first().click()
        const dlg = qa.page.getByRole('dialog')
        await qa.expect(dlg).toBeVisible()
        await dlg.getByLabel('Tên khách').fill('Nguyễn Văn Khách')
        await dlg.getByLabel('Email').fill(email)
        const response = qa.page.waitForResponse((r) => r.request().method() === 'POST' && r.url().endsWith('/guest-invitations'))
        await dlg.getByRole('button', { name: 'Mời', exact: true }).click()
        const res = await response
        const body = await res.json().catch(() => ({}))
        qa.note(`POST guest-invitations: ${res.status()} ${body.code} ${body.message}`)
        qa.expect(body.code).toBe('MSG96')
        await snap(qa, 'da-moi-khach')
        await qa.expect.poll(() => sql(`select count(*) from email_messages where to_address = '${email}'`), { timeout: 90_000 }).not.toBe('0')
        const mail = sql(`select subject || ' ¦ ' || body from email_messages where to_address = '${email}' order by created_at limit 1`)
        qa.note(`Thư mời Khách: ${mail.slice(0, 160).replace(/\n/g, ' ')}`)
        const link = mail.match(/https?:\/\/[^\s"<>]+\/guest\/[0-9a-f-]{36}#token=[A-Za-z0-9_-]+/)?.[0]
        qa.expect(link, 'no guest link').toBeTruthy()
        process.env.QA_GUEST_LINK = link
        process.env.QA_GUEST_EMAIL = email
        process.env.QA_GUEST_MEETING = m.id
      },
    },
    {
      id: 'AUTH_FT20',
      role: 'anon',
      title: 'Mở link thư mời: gửi mã tới email được mời',
      steps: ['Mở link trong thư mời (trình duyệt chưa có phiên Khách)'],
      expected: '"TrustCabinet mời bạn họp", tên cuộc họp, "Nhập mã 6 số đã gửi tới <email che> để xem cuộc họp."; hộp thư nhận một thư có mã 6 số; "Gửi lại mã" đếm ngược ~60 giây',
      async run(qa) {
        const link = process.env.QA_GUEST_LINK
        if (!link) qa.inconclusive('Chạy PART_FT13 trước')
        const email = process.env.QA_GUEST_EMAIL
        const before = await mailCount(email, /Mã xác minh/)
        const u = new URL(link)
        await qa.goto(u.pathname + u.hash)
        await qa.expect(qa.page.getByText('TrustCabinet mời bạn họp')).toBeVisible()
        await qa.expect(qa.page.getByText(/Nhập mã 6 số đã gửi tới .+ để xem cuộc họp\./)).toBeVisible({ timeout: 15_000 })
        const resend = qa.page.getByRole('button', { name: /Gửi lại mã/ })
        qa.note(`Trang: ${(await qa.page.locator('.login-head').innerText()).replace(/\s+/g, ' ')} · nút: ${await resend.innerText()}`)
        qa.expect(await resend.innerText()).toMatch(/Gửi lại mã \((1 phút|\d+ giây|0?:?[0-5]\d|1:00)\)/)
        await qa.page.waitForTimeout(2500)
        qa.note(`Sau 2,5 giây: ${await resend.innerText()}`)
        await snap(qa, 'nhap-ma')
        await qa.expect.poll(() => mailCount(email, /Mã xác minh/), { timeout: 90_000 }).toBe(before + 1)
        const code = await guestCode(email)
        qa.note(`Thư mã: có mã 6 số = ${/^\d{6}$/.test(code)}`)
        qa.expect(code).toMatch(/^\d{6}$/)
      },
    },
    {
      id: 'AUTH_FT22',
      role: 'anon',
      title: 'Nhập sai mã bị từ chối',
      steps: ['Mở link, nhập mã sai'],
      expected: 'Báo mã sai, không vào được',
      async run(qa) {
        const link = process.env.QA_GUEST_LINK
        if (!link) qa.inconclusive('Chạy PART_FT13 trước')
        const u = new URL(link)
        await qa.goto(u.pathname + u.hash)
        await qa.expect(qa.page.getByText(/Nhập mã 6 số/)).toBeVisible({ timeout: 15_000 })
        const real = await guestCode(process.env.QA_GUEST_EMAIL)
        const wrong = real === '000000' ? '111111' : '000000'
        await qa.page.getByLabel('Chữ số 1').fill(wrong)
        await qa.page.getByRole('button', { name: 'Xác minh' }).click()
        const err = qa.page.locator('#guest-code-note.field-error')
        await qa.expect(err).toBeVisible()
        qa.note(`Lỗi: ${await err.innerText()}`)
        await snap(qa, 'ma-sai')
        await qa.expect(qa.page).toHaveURL(/\/guest\//)
      },
    },
    {
      id: 'AUTH_FT24',
      role: 'anon',
      title: 'Tên hiển thị trống không được chấp nhận',
      steps: ['Nhập đúng mã', 'Để trống "Tên hiển thị", bấm "Vào cuộc họp"'],
      expected: 'Báo lỗi ở ô tên, không vào cuộc họp',
      async run(qa) {
        const link = process.env.QA_GUEST_LINK
        if (!link) qa.inconclusive('Chạy PART_FT13 trước')
        const u = new URL(link)
        await qa.goto(u.pathname + u.hash)
        await qa.expect(qa.page.getByText(/Nhập mã 6 số/)).toBeVisible({ timeout: 15_000 })
        await qa.page.waitForTimeout(1500)
        const code = await guestCode(process.env.QA_GUEST_EMAIL)
        await qa.page.getByLabel('Chữ số 1').fill(code)
        await qa.page.getByRole('button', { name: 'Xác minh' }).click()
        const name = qa.page.getByLabel(/Tên hiển thị/)
        await qa.expect(name).toBeVisible({ timeout: 15_000 })
        await name.fill('')
        await qa.page.getByRole('button', { name: 'Vào cuộc họp' }).click()
        const err = qa.page.locator('#guest-display-name-error')
        await qa.expect(err).toBeVisible()
        qa.note(`Lỗi tên: ${await err.innerText()}`)
        await snap(qa, 'ten-trong')
        process.env.QA_GUEST_CONTEXT_READY = '1'
      },
    },
    {
      id: 'AUTH_FT21',
      role: 'anon',
      title: 'Mã đúng dẫn tới form hồ sơ rồi vào cuộc họp',
      steps: ['Nhập đúng mã', 'Điền tên hiển thị, "Vào cuộc họp"'],
      expected: 'Form hồ sơ, rồi trang cuộc họp của Khách',
      async run(qa) {
        const link = process.env.QA_GUEST_LINK
        if (!link) qa.inconclusive('Chạy PART_FT13 trước')
        const u = new URL(link)
        await qa.goto(u.pathname + u.hash)
        await qa.page.waitForTimeout(1500)
        const already = await qa.page.getByLabel(/Tên hiển thị/).isVisible().catch(() => false)
        if (!already) {
          await qa.expect(qa.page.getByText(/Nhập mã 6 số/)).toBeVisible({ timeout: 15_000 })
          // The last code was spent by AUTH_FT24 (one use): ask for a new one once "Gửi lại mã" opens again.
          const resend = qa.page.getByRole('button', { name: /^Gửi lại mã/ })
          await qa.expect(resend).toBeEnabled({ timeout: 90_000 })
          const before = await mailCount(process.env.QA_GUEST_EMAIL, /Mã xác minh/)
          await resend.click()
          await qa.expect.poll(() => mailCount(process.env.QA_GUEST_EMAIL, /Mã xác minh/), { timeout: 90_000 }).toBe(before + 1)
          const code = await guestCode(process.env.QA_GUEST_EMAIL)
          await qa.page.getByLabel('Chữ số 1').fill(code)
          await qa.page.getByRole('button', { name: 'Xác minh' }).click()
        }
        await qa.expect(qa.page.getByLabel(/Tên hiển thị/)).toBeVisible({ timeout: 15_000 })
        await snap(qa, 'form-ho-so')
        await qa.page.getByLabel(/Tên hiển thị/).fill('Nguyễn Văn Khách')
        await qa.page.getByRole('button', { name: 'Vào cuộc họp' }).click()
        await qa.page.waitForLoadState('networkidle').catch(() => undefined)
        qa.note(`Sau khi vào: ${qa.page.url()} · ${(await qa.page.locator('h1').first().innerText().catch(() => ''))}`)
        await qa.expect(qa.page.locator('h1').first()).toContainText('GUEST mời khách', { timeout: 15_000 })
        await snap(qa, 'trang-cuoc-hop-cua-khach')
        // AUTH_FT26 and RPT_FT11 on the same Guest session.
        const token = await qa.page.evaluate(async () => {
          const r = await fetch('/api/guest-access/session', { credentials: 'include' })
          return r.status
        })
        qa.note(`GET /api/guest-access/session từ trang Khách: ${token}`)
        await qa.goto('/mgmt/meetings')
        qa.note(`Khách mở /mgmt/meetings: ${qa.page.url()} · ${(await qa.page.locator('h1').first().innerText().catch(() => ''))}`)
        await snap(qa, 'khach-mo-man-hinh-noi-bo')
        await qa.goto('/reports')
        qa.note(`Khách mở /reports: ${qa.page.url()} · ${(await qa.page.locator('h1').first().innerText().catch(() => ''))}`)
      },
    },

    // ------------------------------------------------------------------------------------------------ FE-08 room
    {
      id: 'ROOM_FT02',
      role: 'member',
      title: 'Thành viên vào phòng từ phòng chờ, thấy lưới gọi có ô của mình và thanh điều khiển',
      steps: ['Thành viên mở phòng chờ của họp trực tuyến Đang diễn ra', 'Bấm "Vào phòng"'],
      expected: 'call/join 200; "Đang vào phòng…"; tiêu đề, "1 người trong phòng", ô "(Bạn)", thanh "Tắt micro", "Tắt camera", "Người dự", "Chat", "Rời phòng"; không đổi trạng thái/điểm danh',
      async run(qa) {
        const m = await meetingAt(qa, 'InProgress', { label: 'ROOM02 trực tuyến', over: { mode: 'Online', location: null } })
        const before = (await (await client(qa, 'secretary')).get(`/api/meetings/${m.id}/participants`)).body
        await qa.goto(`/mgmt/meetings/${m.id}/room`)
        await qa.expect(qa.page.getByRole('heading', { name: 'Sẵn sàng tham gia?' })).toBeVisible()
        await snap(qa, 'phong-cho')
        const join = qa.page.waitForResponse((r) => r.request().method() === 'POST' && r.url().endsWith('/call/join'))
        await qa.page.getByRole('button', { name: 'Vào phòng', exact: true }).click()
        qa.note(`call/join: ${(await join).status()}`)
        qa.expect((await join).status()).toBe(200)
        await qa.expect(qa.page.locator('.room-count-label')).toBeVisible({ timeout: 20_000 })
        await qa.expect(qa.page.getByText('(Bạn)').first()).toBeVisible({ timeout: 20_000 })
        const labels = await qa.page.getByRole('button').evaluateAll((bs) => bs.map((b) => b.getAttribute('aria-label') || b.innerText.trim()).filter(Boolean))
        qa.note(`Nút trong phòng: ${labels.join(' | ')}`)
        for (const label of [/micro/i, /camera/i, /Người dự/, /Chat|Trò chuyện/i, /Rời phòng/])
          qa.expect(labels.some((l) => label.test(l)), `no button ${label}`).toBe(true)
        qa.note(`Đầu phòng: ${(await qa.page.locator('.room-count-label').locator('xpath=..').innerText()).replace(/\s+/g, ' ')}`)
        await qa.page.waitForTimeout(1500)
        await snap(qa, 'trong-phong')
        const after = (await (await client(qa, 'secretary')).get(`/api/meetings/${m.id}/participants`)).body
        qa.expect(JSON.stringify(after)).toBe(JSON.stringify(before))
        qa.expect((await (await client(qa, 'secretary')).get(`/api/meetings/${m.id}`)).body.status).toBe('InProgress')
      },
    },
    {
      id: 'ROOM_FT03',
      role: 'chair',
      title: 'Chủ toạ và Thư ký vào phòng như Thành viên; ô Chủ toạ có nhãn vai trò',
      steps: ['Chủ toạ vào phòng', 'Trình duyệt thứ hai: Thư ký vào phòng'],
      expected: 'Cả hai kết nối, mỗi người thấy hai ô và "2 người trong phòng"; ô Chủ toạ có "Chủ toạ"',
      async run(qa) {
        const m = await meetingAt(qa, 'InProgress', { label: 'ROOM03 trực tuyến', over: { mode: 'Online', location: null } })
        const enter = async (page) => {
          await page.goto(`/mgmt/meetings/${m.id}/room`)
          await page.getByRole('button', { name: 'Vào phòng', exact: true }).click()
          await qa.expect(page.locator('.room-count-label')).toBeVisible({ timeout: 20_000 })
        }
        await enter(qa.page)
        const sec = await second(qa, 'secretary')
        await enter(sec)
        for (const page of [qa.page, sec]) {
          await qa.expect(page.getByText(/2\s*người trong phòng/).first()).toBeVisible({ timeout: 20_000 })
          await qa.expect(page.getByText('Chủ toạ').first()).toBeVisible()
        }
        qa.note(`Ô trong phòng của Chủ toạ: ${(await qa.page.locator('main, body').first().innerText()).replace(/\s+/g, ' ').slice(0, 200)}`)
        await qa.page.waitForTimeout(1500)
        await snap(qa, 'chu-toa-trong-phong')
        await sec.screenshot({ path: `${process.cwd()}/out/room03-secretary.png` })
      },
    },
    {
      id: 'ROOM_FT54',
      role: 'secretary',
      title: 'Ghi chú khoá khi đã có bản nháp biên bản',
      steps: ['Cuộc họp trực tuyến Đã kết thúc, Thư ký mở ghi chú', 'Tạo bản nháp biên bản', 'Gõ ghi chú, đợi lưu'],
      expected: '409 MSG417; chữ đã gõ hiện ở "Chưa lưu được: <mục>" với "Sao chép" và "Bỏ"',
      async run(qa) {
        const m = await meetingAt(qa, 'Ended', { label: 'ROOM54 ghi chú', over: { mode: 'Online', location: null } })
        await openMeeting(qa, m)
        await qa.page.getByRole('button', { name: 'Mở ghi chú' }).click()
        const box = qa.page.getByRole('textbox').last()
        await qa.expect(box).toBeVisible()
        await box.fill('Ghi chú trước khi có biên bản.')
        await qa.page.waitForTimeout(3000)
        await minutesTo(qa, m.id, 'Draft')
        const response = qa.page.waitForResponse((r) => r.request().method() === 'PUT' && r.url().endsWith('/notes'), { timeout: 20_000 })
        await box.click()
        await qa.page.keyboard.press('End')
        await qa.page.keyboard.type(' Thêm sau khi có biên bản.')
        const res = await response
        const body = await res.json().catch(() => ({}))
        qa.note(`PUT notes: ${res.status()} ${body.code} ${body.message}`)
        qa.expect(res.status()).toBe(409)
        qa.expect(body.code).toBe('MSG417')
        const refused = qa.page.getByRole('alert').filter({ hasText: 'Chỉ ghi chú khi cuộc họp đang diễn ra' })
        await qa.expect(refused).toBeVisible()
        await qa.expect(box).not.toBeEditable()
        await snap(qa, 'bi-tu-choi-o-khoa')
        // "Tải lại" puts the panel read-only; the text the server refused is listed to copy (docs/03-api/19 §FE).
        await refused.getByRole('button', { name: 'Tải lại' }).click()
        await qa.expect(qa.page.getByText(/Chưa lưu được:/).first()).toBeVisible({ timeout: 10_000 })
        await qa.expect(qa.page.getByRole('button', { name: 'Sao chép' }).first()).toBeVisible()
        await qa.expect(qa.page.getByRole('button', { name: 'Bỏ', exact: true }).first()).toBeVisible()
        await snap(qa, 'chua-luu-duoc', { fullPage: true })
      },
    },

    // ------------------------------------------------------------------------------------------------ FE-16 find time
    {
      id: 'CAL_FT34',
      role: 'secretary',
      title: 'Không có khung chung: báo vậy và hiện các khung nhiều người rảnh nhất',
      steps: ['Tìm giờ với hai người không bao giờ cùng rảnh trong khoảng tìm'],
      expected: 'Thông báo không có khung hợp với tất cả, hiện khung nhiều người dự nhất; mỗi khung nêu ai bận / chưa biết',
      async run(qa) {
        const u = await users(qa)
        // Find Time sees only people who share their calendar with the caller: chutoa@ and thanhvien@ share with thuky@.
        for (const who of ['chair', 'member']) {
          const share = await (await client(qa, who)).post('/api/calendar/shares/direct', { viewerId: u.secretary.id, startDate: vnDay(0).iso, endDate: vnDay(30).iso })
          qa.note(`${who} chia sẻ lịch: ${share.status} ${share.body?.code ?? JSON.stringify(share.body).slice(0, 160)}`)
        }
        // A direct share waits for the viewer to accept it (UC-80c).
        const mine = (await (await client(qa, 'secretary')).get('/api/calendar/shares')).body.asViewer ?? []
        for (const s of mine.filter((s) => s.status === 'Pending' && (s.actions ?? []).includes('accept'))) {
          const ok = await (await client(qa, 'secretary')).post(`/api/calendar/shares/${s.id}/accept`)
          qa.note(`Thư ký chấp nhận chia sẻ: ${ok.status} ${ok.body?.code ?? ''}`)
        }
        // Next Monday: chutoa@ busy all morning, thanhvien@ busy all afternoon.
        const now = new Date(Date.now() + 7 * 3_600_000)
        const add = ((8 - now.getUTCDay()) % 7) + 7
        const day = vnDay(add).iso
        const at = (h, mm = 0) => new Date(`${day}T${String(h).padStart(2, '0')}:${String(mm).padStart(2, '0')}:00+07:00`).toISOString()
        const busy = async (who, from, to, label) => {
          const res = await (await client(qa, 'secretary')).post('/api/meetings', {
            title: qa.name(`MR1 CAL34 ${label} ${rand()}`), plannedStartTime: from, plannedEndTime: to, mode: 'InPerson', location: 'QA', onlineLink: null, sensitivity: 'Normal', description: '',
            participants: [{ userId: u[who].id, role: 'Chair' }, { userId: u.secretary.id, role: 'Secretary' }],
          })
          qa.expectStatus(res, 201)
          const id = res.body.meeting.id
          for (const [r, b] of [['secretary', { action: 'SubmitForApproval', assignedApproverId: u.chair.id }], ['chair', { action: 'Approve' }], ['secretary', { action: 'Publish' }]])
            qa.note(`${label} ${b.action}: ${(await (await client(qa, r)).post(`/api/meetings/${id}/transitions`, b)).status}`)
        }
        await busy('chair', at(7, 0), at(12, 0), 'chủ toạ bận sáng')
        await busy('member', at(13, 0), at(18, 0), 'thành viên bận chiều')
        const api = await (await client(qa, 'secretary')).post('/api/calendar/slots', { userIds: [u.chair.id, u.member.id], durationMinutes: 60, fromDate: day, toDate: day })
        qa.note(`API: ${api.status} ${api.body?.code} ${api.body?.message}; người: ${JSON.stringify(api.body?.people?.map((p) => p.status))}`)
        qa.expect(api.body.code).toBe('MSG445')
        await qa.goto('/calendar/find')
        const search = qa.page.getByRole('combobox', { name: 'Tìm người dự' })
        for (const who of ['chair', 'member']) {
          await search.click()
          await search.fill(u[who].email.split('@')[0])
          await qa.page.getByRole('listbox', { name: 'Người có thể mời' }).getByRole('option').filter({ hasText: u[who].email }).first().click()
        }
        const vn = `${day.slice(8, 10)}/${day.slice(5, 7)}/${day.slice(0, 4)}`
        for (const label of ['Từ ngày', 'Đến ngày']) {
          const f = qa.page.getByLabel(label, { exact: true })
          await f.fill(vn)
          await f.press('Tab')
        }
        const dur = qa.page.getByRole('button', { name: /Thời lượng họp/ })
        if (await dur.count()) {
          await dur.first().click()
          await qa.page.getByRole('option', { name: /^1 giờ$|60 phút/ }).first().click().catch(() => qa.note('Không chọn được thời lượng 1 giờ'))
        }
        await qa.page.getByRole('button', { name: 'Tìm giờ', exact: true }).click()
        const alert = qa.page.locator('.find-results .alert-warning')
        await qa.expect(alert).toBeVisible({ timeout: 15_000 })
        qa.note(`Thông báo: ${await alert.innerText()} · ${await qa.page.locator('.find-count').innerText()}`)
        await qa.expect(alert).toContainText('Không có khung giờ nào hợp với tất cả. Đang hiện các khung nhiều người dự được nhất.')
        await qa.page.locator('.find-chip').first().click()
        const detail = qa.page.getByRole('list', { name: 'Ai rảnh ở khung này' })
        await qa.expect(detail).toBeVisible()
        qa.note(`Khung đầu: ${(await detail.innerText()).replace(/\s+/g, ' ')}`)
        await snap(qa, 'khong-co-khung-chung', { fullPage: true })
      },
    },
  ],
}

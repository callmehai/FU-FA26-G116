// FE-11 — Biểu quyết (QA subtask SCRUM-198): UC-62a, UC-62b view votes and a vote's status, UC-63 create / edit / delete a
// draft, UC-64 open (from the card and from the form), UC-65 cast, UC-66 close, UC-67 record the conclusion, UC-68a, UC-68b
// attach / detach vote documents and the read gate of UC-42 (MSG252), plus the rules the PO settled in Jira SCRUM-28 /
// SCRUM-96: the voter list is fixed at opening for everyone, drafts left when the meeting ends are deleted, a ballot is
// never changed or withdrawn, a secret ballot never names its voter, the Chair picks the conclusion (never inferred).
//
// Criteria come from docs/01-requirements/05-use-case-specifications.md (UC-62a … UC-67, UC-68a/b), 06-business-rules.md,
// docs/03-api/15-bieu-quyet.md (§2 permissions by state, §7 as built), the message catalogue (MSG205–236, 252–255,
// 265–270) and the Jira comments on SCRUM-28 / SCRUM-96.
//
// AC23-AC31 (Round 1 UI, 2026-11-22): the list on one screen (AC23), empty (AC24), removed from the meeting (AC25),
// list read failure injected with page.route (AC26), create on an Approved meeting (AC27), storage full simulated by
// the library response (AC28), ballot failure injected (AC29), cancelling the close dialog (AC30), an abandoned
// conclusion (AC31); AC2, AC14, AC16, AC17 also check cancelled writes, the Member's view, the Secretary's screen.
//
// Safe on a shared local dev database: every meeting is created here through the API, titled "[QA] FE-11 …", dated
// seven weeks from today, organised by thuky@ with chutoa@ as Chair and approver, thuky@ as Secretary and thanhvien@ as
// Member. quantri@ is never on the roster (the non-participant). Two meetings are found again by title on the next run
// when a run stopped half-way ("Họp chuẩn bị biểu quyết" while Published, "Họp biểu quyết trực tiếp" while InProgress);
// a full run ends both (AC19, AC20) and cancels the third, so nothing is left running. The "empty" and "approved" meetings stay Published / Approved and are reused; AC25 makes and cancels its own.

import { ACCOUNTS } from '../lib/qa.mjs'

const ALL_ROLES = ['Chair', 'Secretary', 'Member', 'Guest']
const OPTIONS = ['Đồng ý', 'Không đồng ý', 'Không ý kiến']
const TITLES = {
  prep: 'FE-11 Họp chuẩn bị biểu quyết',
  live: 'FE-11 Họp biểu quyết trực tiếp',
  states: 'FE-11 Họp kiểm trạng thái biểu quyết',
  empty: 'FE-11 Họp chưa có biểu quyết',
  approved: 'FE-11 Họp đã duyệt chuẩn bị biểu quyết',
  gone: 'FE-11 Họp gỡ người xem biểu quyết',
}
const MSG228 = 'Không ghi được phiếu. Chưa có phiếu nào được ghi. Vui lòng thử lại.'
const MSG = {
  MSG205: 'Không truy cập được biểu quyết của cuộc họp này.',
  MSG209: 'Thông tin biểu quyết không hợp lệ. Kiểm tra các trường được đánh dấu.',
  MSG213:
    'Chỉ Chủ toạ hoặc Thư ký hiện hành được quản lý biểu quyết nháp, khi cuộc họp đã duyệt, đã công bố hoặc đang diễn ra.',
  MSG218: 'Chỉ Chủ toạ hiện hành được mở biểu quyết nháp, khi cuộc họp đang diễn ra.',
  MSG219: 'Cuộc họp đang có biểu quyết khác đang mở. Hãy đóng biểu quyết đó trước.',
  MSG220: 'Không có ai đủ điều kiện bỏ phiếu theo các vai trò đã chọn. Biểu quyết chưa được mở.',
  MSG224: 'Bạn không có trong danh sách được bỏ phiếu của biểu quyết này.',
  MSG225: 'Biểu quyết này không còn nhận phiếu.',
  MSG226: 'Phiếu của bạn đã được ghi nhận, không thể đổi hoặc rút.',
  MSG230: 'Chỉ Chủ toạ hiện hành được đóng biểu quyết đang mở, khi cuộc họp đang diễn ra hoặc đã kết thúc.',
  MSG233: 'Kết luận biểu quyết đã được ghi và không thể sửa.',
  MSG234:
    'Chỉ Chủ toạ hiện hành được ghi kết luận cho biểu quyết đã đóng, khi cuộc họp đang diễn ra hoặc đã kết thúc.',
}

// ---------------------------------------------------------------------------------------------------------------------
// Helpers: accounts, API clients, meetings.

const USERS = {}
async function users(qa) {
  for (const role of ['admin', 'chair', 'secretary', 'member']) {
    if (USERS[role]) continue
    const me = (await (await client(qa, role)).get('/api/auth/me')).body
    USERS[role] = { id: me.id, name: me.fullName, email: me.email }
  }
  return USERS
}

/** One signed-in API client per role and criterion. */
async function client(qa, role) {
  qa.__clients ??= {}
  qa.__clients[role] ??= await qa.api(role)
  return qa.__clients[role]
}

const rand = () => Math.random().toString(36).slice(2, 7)
const iso = (ms) => new Date(ms).toISOString().replace(/\.\d{3}Z$/, 'Z')

/** A 30-minute slot on the day seven weeks from today (Vietnam time), at its own hour per kind of meeting. */
const HOURS = { prep: [8, 0], live: [14, 0], states: [10, 30], empty: [9, 0], approved: [11, 30], gone: [12, 30] }
function slot(key) {
  const [h, m] = HOURS[key] ?? [16, 30]
  const day = new Date(Date.now() + 49 * 86_400_000 + 7 * 3_600_000).toISOString().slice(0, 10)
  const start = Date.parse(`${day}T${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')}:00+07:00`)
  return { plannedStartTime: iso(start), plannedEndTime: iso(start + 30 * 60_000) }
}

async function transition(qa, role, id, body) {
  return (await client(qa, role)).post(`/api/meetings/${id}/transitions`, body)
}

/** Drives a meeting one step, as the actor who does it. */
async function step(qa, id, name) {
  const u = await users(qa)
  const [role, body] = {
    submit: ['secretary', { action: 'SubmitForApproval', assignedApproverId: u.chair.id }],
    approve: ['chair', { action: 'Approve' }],
    publish: ['secretary', { action: 'Publish' }],
    start: ['chair', { action: 'Start' }],
    end: ['chair', { action: 'End' }],
    cancel: ['secretary', { action: 'Cancel', reason: 'QA: dọn dữ liệu kiểm thử biểu quyết.' }],
  }[name]
  const res = await transition(qa, role, id, body)
  qa.expectStatus(res, 200)
  return res
}

const PATH = {
  Draft: [],
  WaitingApproval: ['submit'],
  Approved: ['submit', 'approve'],
  Published: ['submit', 'approve', 'publish'],
  InProgress: ['submit', 'approve', 'publish', 'start'],
}

/** Creates a meeting titled "[QA] <title>" and drives it to `status`. */
async function createMeeting(qa, title, status, key) {
  const u = await users(qa)
  const created = await (await client(qa, 'secretary')).post('/api/meetings', {
    title: qa.name(title),
    ...slot(key),
    mode: 'InPerson',
    location: 'Phòng họp QA',
    onlineLink: null,
    sensitivity: 'Normal',
    description: 'Cuộc họp do bộ kiểm thử QA FE-11 (biểu quyết) tạo.',
    participants: [
      { userId: u.chair.id, role: 'Chair' },
      { userId: u.secretary.id, role: 'Secretary' },
      { userId: u.member.id, role: 'Member' },
    ],
  })
  qa.expectStatus(created, 201)
  const id = created.body.meeting.id
  for (const s of PATH[status]) await step(qa, id, s)
  return { id, title: qa.name(title), status }
}

/**
 * The meeting `key` in `status`: this run's one, or one an earlier run left in that status (found by its exact title),
 * or a new one. A live meeting is cleared of open votes first, so the criterion can open its own.
 */
const MEET = {}
async function meeting(qa, key, status) {
  const sec = await client(qa, 'secretary')
  const title = qa.name(TITLES[key])
  const still = async (m) => m && (await sec.get(`/api/meetings/${m.id}`)).body?.status === status
  if (!(await still(MEET[key]))) {
    const list = await sec.get(`/api/meetings?q=${encodeURIComponent(TITLES[key])}&pageSize=50`)
    const found = list.body?.items?.find((i) => i.title === title && i.status === status)
    MEET[key] = found ? { id: found.id, title, status } : await createMeeting(qa, TITLES[key], status, key)
  }
  if (status === 'InProgress') await closeOpenVotes(qa, MEET[key])
  return MEET[key]
}

// ---------------------------------------------------------------------------------------------------------------------
// Helpers: votes through the API.

const votesPath = (m) => `/api/meetings/${m.id}/votes`
const votePath = (m, v) => `/api/meetings/${m.id}/votes/${v.id}`

/** A vote body; `over` replaces any field. */
function voteBody(qa, label, over = {}) {
  return {
    title: qa.name(`FE-11 ${label} ${qa.device} ${rand()}`),
    description: '',
    options: OPTIONS,
    isAnonymous: false,
    eligibleRoles: ALL_ROLES,
    openImmediately: false,
    ...over,
  }
}

/** Creates a draft (as the secretary by default) and returns `{ id, title }`. */
async function draftVote(qa, m, label, over = {}, role = 'secretary') {
  const body = voteBody(qa, label, over)
  const res = await (await client(qa, role)).post(votesPath(m), body)
  qa.expectStatus(res, 201)
  return { id: res.body.voteId, title: body.title }
}

async function openVote(qa, m, v) {
  qa.expectStatus(await (await client(qa, 'chair')).post(`${votePath(m, v)}/open`), 200)
}

async function closeVote(qa, m, v) {
  qa.expectStatus(await (await client(qa, 'chair')).post(`${votePath(m, v)}/close`), 200)
}

/** Closes every vote still open in the meeting (one a stopped run left behind would block the next opening). */
async function closeOpenVotes(qa, m) {
  const chair = await client(qa, 'chair')
  const list = await chair.get(votesPath(m))
  for (const v of list.body?.votes?.filter((x) => x.status === 'Open') ?? []) await chair.post(`${votePath(m, v)}/close`)
}

/** A new vote, opened by the Chair. */
async function openedVote(qa, m, label, over = {}) {
  await closeOpenVotes(qa, m)
  const v = await draftVote(qa, m, label, over)
  await openVote(qa, m, v)
  return v
}

async function detail(qa, role, m, v) {
  return (await client(qa, role)).get(votePath(m, v))
}

/** Casts `role`'s ballot for option `index` (0-based) and returns the response. */
async function cast(qa, role, m, v, index) {
  const d = (await detail(qa, role, m, v)).body
  return (await client(qa, role)).post(`${votePath(m, v)}/ballots`, { optionId: d.options[index].id })
}

/** Runs every API check, then fails once with all the wrong ones, so one bad answer does not hide the next. */
function checker(qa) {
  const wrong = []
  return {
    check(label, res, status, code) {
      const ok = res.status === status && (code === undefined || res.body?.code === code)
      if (!ok) wrong.push(`${label}: want ${status}${code ? ` ${code}` : ''}, got ${res.status} ${JSON.stringify(res.body).slice(0, 220)}`)
      else if (code && MSG[code] && res.body?.message !== MSG[code]) wrong.push(`${label}: ${code} text "${res.body?.message}"`)
      return ok
    },
    fact(label, ok, detailText = '') {
      if (!ok) wrong.push(`${label}${detailText ? `: ${detailText}` : ''}`)
    },
    done() {
      qa.expect(wrong, wrong.join('\n')).toEqual([])
    },
  }
}

// ---------------------------------------------------------------------------------------------------------------------
// Helpers: screen.

async function noSidewaysScroll(qa) {
  const sideways = await qa.page.evaluate(() => document.documentElement.scrollWidth - innerWidth)
  qa.expect(sideways, 'the page scrolls sideways').toBeLessThanOrEqual(0)
}
async function noRawCodes(qa) {
  const text = await qa.page.locator('main').innerText()
  qa.expect(text, 'a raw MSG code is shown to the user').not.toMatch(/MSG\d+/)
}
async function settled(qa) {
  await qa.page.waitForFunction(() =>
    document.getAnimations().every((a) => a.playState !== 'running' || a.effect?.getTiming().iterations === Infinity),
  )
}
const toast = (qa, text) => qa.page.locator('.toast-stack').getByText(text)
const dialog = (qa, name) => qa.page.getByRole('dialog', { name })
const panel = (qa) => qa.page.locator('section.vote-panel')
/** The "Tạo biểu quyết" button sits in the toolbar above the card on the page (inside the panel only in the room). */
const createButton = (qa) => qa.page.locator('.vote-toolbar').getByRole('button', { name: 'Tạo biểu quyết' })
/** The card of one vote, by its exact title. */
const card = (qa, v) =>
  panel(qa).locator('li.vote-item').filter({ has: qa.page.getByRole('button', { name: v.title, exact: true }) })

async function press(qa, locator) {
  if (qa.device === 'phone') await locator.tap()
  else await locator.click()
}

/** Opens a vote card that starts collapsed (a closed vote with nothing left for the caller to do). */
async function expand(qa, v) {
  const head = card(qa, v).getByRole('button', { name: v.title, exact: true })
  if ((await head.getAttribute('aria-expanded')) === 'false') await press(qa, head)
}

/** Counts the writes (POST / PUT / DELETE) the page sends under one path, to prove a cancelled step sent none. */
function watchWrites(qa, pathMatches) {
  const sent = []
  const listener = (r) => {
    if (r.method() !== 'GET' && pathMatches(new URL(r.url()).pathname)) sent.push(`${r.method()} ${new URL(r.url()).pathname}`)
  }
  qa.page.on('request', listener)
  return { sent, stop: () => qa.page.off('request', listener) }
}

/** HTTP with a custom header: the runner's client sends none, and a roster change needs If-Match. */
async function removeFromRoster(qa, meetingId, userId) {
  const login = await fetch(`${qa.base}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email: ACCOUNTS.secretary, password: process.env.QA_PASSWORD ?? 'TrustCabinet@2026' }),
  })
  const headers = { Authorization: `Bearer ${(await login.json()).accessToken}` }
  const roster = await (await fetch(`${qa.base}/api/meetings/${meetingId}/participants?pageSize=100`, { headers })).json()
  const row = roster.items.find((i) => i.userId === userId)
  const res = await fetch(`${qa.base}/api/meetings/${meetingId}/participants/${row.id}`, {
    method: 'DELETE',
    headers: { ...headers, 'If-Match': `"participants-${roster.revision}"` },
  })
  return res.status
}

/** Opens the meeting's Biểu quyết tab and waits for the panel. */
async function openVotesTab(qa, m) {
  await qa.goto(`/mgmt/meetings/${m.id}?tab=votes`)
  await qa.expect(qa.page.getByRole('tab', { name: 'Biểu quyết' })).toHaveAttribute('aria-selected', 'true')
  await qa.expect(panel(qa)).toBeVisible()
}

// ---------------------------------------------------------------------------------------------------------------------
// Guest voter (UC-04 OTP by email): the FE-01 pattern (example.test lands in local Mailpit). The browser stays
// anonymous; every signed-in call goes through global fetch with a secretary login, like removeFromRoster above.

async function guestMailpit(to) {
  const r = await fetch(`http://localhost:8025/api/v1/search?query=${encodeURIComponent(`to:"${to}"`)}`)
  const j = await r.json()
  return (j.messages ?? []).map((m) => ({ id: m.ID, subject: m.Subject }))
}

async function guestCode(to) {
  const until = Date.now() + 90_000
  while (Date.now() < until) {
    const hit = (await guestMailpit(to)).find((x) => /Mã xác minh/.test(x.subject))
    if (hit) {
      const full = await (await fetch(`http://localhost:8025/api/v1/message/${hit.id}`)).json()
      const code = (full.Text ?? '').match(/\b\d{6}\b/)?.[0] ?? ''
      if (code) return code
    }
    await new Promise((r) => setTimeout(r, 2000))
  }
  return ''
}

async function guestInvite(qa, meetingId, name, email) {
  const login = await fetch(`${qa.base}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email: ACCOUNTS.secretary, password: process.env.QA_PASSWORD ?? 'TrustCabinet@2026' }),
  })
  const headers = { Authorization: `Bearer ${(await login.json()).accessToken}` }
  const roster = await (await fetch(`${qa.base}/api/meetings/${meetingId}/participants?pageSize=100`, { headers })).json()
  const res = await fetch(`${qa.base}/api/meetings/${meetingId}/guest-invitations`, {
    method: 'POST',
    headers: { ...headers, 'Content-Type': 'application/json', 'If-Match': `"participants-${roster.revision}"` },
    body: JSON.stringify({ invitedName: name, email }),
  })
  if (res.status !== 201) throw new Error(`guest invitation: HTTP ${res.status} ${await res.text()}`)
  let link = null
  await qa.expect
    .poll(
      async () => {
        const box = await guestMailpit(email)
        for (const m of box) {
          const full = await (await fetch(`http://localhost:8025/api/v1/message/${m.id}`)).json()
          const raw = full.Text ?? ''
          const pattern = /https?:\/\/\S*?\/guest\/[0-9a-f-]{36}#token=[A-Za-z0-9\-_+/=]+/
          const found = raw.match(pattern) ?? raw.replace(/\r?\n(?=[A-Za-z0-9\-_+/=])/g, '').match(pattern)
          if (found) {
            link = found[0]
            return link
          }
        }
        return null
      },
      { timeout: 90_000 },
    )
    .not.toBeNull()
  return link
}

/**
 * Revokes a guest invitation created by guestInvite (the link carries /guest/{id}#token=…), so a shared meeting
 * does not keep the guest as an eligible voter for later criteria. Never fails the criterion: cleanup only.
 */
async function revokeGuest(qa, meetingId, link) {
  try {
    const id = /\/guest\/([0-9a-f-]{36})/.exec(link ?? '')?.[1]
    if (!id) return
    const login = await fetch(`${qa.base}/api/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email: ACCOUNTS.secretary, password: process.env.QA_PASSWORD ?? 'TrustCabinet@2026' }),
    })
    const headers = { Authorization: `Bearer ${(await login.json()).accessToken}` }
    const roster = await (await fetch(`${qa.base}/api/meetings/${meetingId}/participants?pageSize=1`, { headers })).json()
    await fetch(`${qa.base}/api/meetings/${meetingId}/guest-invitations/${id}`, {
      method: 'DELETE',
      headers: { ...headers, 'If-Match': `"participants-${roster.revision}"` },
    })
  } catch {
    // Cleanup best-effort: a leftover guest only skews later eligible counts, it never fails this criterion.
  }
}

/** Opens the invitation link, verifies the OTP and declares the display name; lands on the guest meeting page. */
async function guestVerify(qa, link, email, displayName) {
  const u = new URL(link)
  await qa.goto(u.pathname + u.hash)
  await qa.expect(qa.page.getByText(/Nhập mã 6 số/)).toBeVisible({ timeout: 15_000 })
  const code = await guestCode(email)
  qa.expect(code, 'no 6-digit code mailed').toMatch(/^\d{6}$/)
  await qa.page.getByLabel('Chữ số 1').fill(code)
  await qa.page.getByRole('button', { name: 'Xác minh' }).click()
  await qa.expect(qa.page.getByLabel(/Tên hiển thị/)).toBeVisible({ timeout: 15_000 })
  await qa.page.getByLabel(/Tên hiển thị/).fill(displayName)
  await qa.page.getByRole('button', { name: 'Vào cuộc họp' }).click()
  await qa.expect(qa.page.getByRole('tab', { name: 'Biểu quyết' })).toBeVisible({ timeout: 15_000 })
}

/** A one-page PDF a strict reader accepts (correct xref offsets), with ASCII text. */
function makePdf(text) {
  const stream = `BT /F1 20 Tf 72 760 Td (${text}) Tj ET`
  const objs = [
    '<< /Type /Catalog /Pages 2 0 R >>',
    '<< /Type /Pages /Kids [3 0 R] /Count 1 >>',
    '<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>',
    `<< /Length ${stream.length} >>\nstream\n${stream}\nendstream`,
    '<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>',
  ]
  let out = '%PDF-1.4\n'
  const offsets = []
  objs.forEach((o, i) => {
    offsets.push(out.length)
    out += `${i + 1} 0 obj\n${o}\nendobj\n`
  })
  const xref = out.length
  out += `xref\n0 ${objs.length + 1}\n0000000000 65535 f \n`
  out += offsets.map((o) => `${String(o).padStart(10, '0')} 00000 n \n`).join('')
  out += `trailer\n<< /Size ${objs.length + 1} /Root 1 0 R >>\nstartxref\n${xref}\n%%EOF\n`
  return Buffer.from(out, 'latin1').toString('base64')
}

/**
 * Uploads a PDF to the meeting's documents as the signed-in page user (Chair or Secretary), from inside the page: the
 * browser exchanges its own refresh cookie for a token, so no password is used here. Returns `{ status, body }`.
 */
async function uploadMeetingPdf(qa, m, fileName) {
  return qa.page.evaluate(
    async ({ meetingId, fileName, b64 }) => {
      const r = await fetch('/api/auth/refresh', { method: 'POST', credentials: 'include' })
      const { accessToken } = await r.json()
      const bytes = Uint8Array.from(atob(b64), (c) => c.charCodeAt(0))
      const form = new FormData()
      form.append('files', new File([bytes], fileName, { type: 'application/pdf' }))
      const res = await fetch(`/api/meetings/${meetingId}/documents/upload`, {
        method: 'POST',
        headers: { Authorization: `Bearer ${accessToken}` },
        body: form,
      })
      return { status: res.status, body: await res.json().catch(() => null) }
    },
    { meetingId: m.id, fileName, b64: makePdf(`QA FE-11 ${fileName}`) },
  )
}

// ---------------------------------------------------------------------------------------------------------------------

export default {
  ticket: 'SCRUM-198',
  title: 'FE-11 Biểu quyết: nháp, mở, bỏ phiếu, đóng, kết luận, tài liệu biểu quyết, quyền theo vai trò và trạng thái',
  devices: ['desktop'],
  criteria: [
    // ------------------------------------------------------------------------------------------- UC-63 Normal Flow
    {
      id: 'AC1',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Thư ký tạo biểu quyết nháp trước giờ họp (cuộc họp Đã công bố); form mặc định Công khai, 3 phương án, đủ 4 vai trò',
      steps: [
        'Cuộc họp [QA] Đã công bố; thuky@ mở tab Biểu quyết, bấm "Tạo biểu quyết"',
        'Xem giá trị mặc định của form, nhập nội dung, bấm "Lưu nháp"',
      ],
      expected:
        'Form: Công khai được chọn, phương án Đồng ý / Không đồng ý / Không ý kiến, 4 vai trò đều tích, không có nút "Mở biểu quyết" (Thư ký). Lưu: toast "Tạo biểu quyết nháp thành công.", thẻ mới có chip "Nháp"; API: Draft, chưa chốt người bỏ phiếu (eligibleCount 0)',
      async run(qa) {
        const m = await meeting(qa, 'prep', 'Published')
        await openVotesTab(qa, m)
        await press(qa, createButton(qa))
        const dlg = dialog(qa, 'Tạo biểu quyết')
        await qa.expect(dlg).toBeVisible()
        await qa.expect(dlg.getByRole('radio', { name: 'Công khai' })).toBeChecked()
        for (const [i, label] of OPTIONS.entries()) await qa.expect(dlg.getByRole('textbox', { name: `Phương án ${i + 1}`, exact: true })).toHaveValue(label)
        for (const role of ['Chủ toạ', 'Thư ký', 'Thành viên', 'Khách'])
          await qa.expect(dlg.getByRole('checkbox', { name: role })).toBeChecked()
        await qa.expect(dlg.getByRole('button', { name: 'Mở biểu quyết' })).toHaveCount(0)
        await settled(qa)
        await qa.shot('form-mac-dinh')
        const title = qa.name(`FE-11 Thông qua kế hoạch quý I ${qa.device} ${rand()}`)
        await dlg.getByLabel('Nội dung biểu quyết').fill(title)
        await press(qa, dlg.getByRole('button', { name: 'Lưu nháp' }))
        await qa.expect(toast(qa, 'Tạo biểu quyết nháp thành công.')).toBeVisible()
        const v = { title }
        await qa.expect(card(qa, v).locator('.vote-status')).toHaveText(/Nháp/)
        await card(qa, v).scrollIntoViewIfNeeded()
        await settled(qa)
        await qa.shot('nhap-da-tao')
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
        const list = (await (await client(qa, 'secretary')).get(votesPath(m))).body
        const row = list.votes.find((x) => x.title === title)
        qa.expect(row?.status).toBe('Draft')
        qa.expect(row?.eligibleCount).toBe(0)
        qa.expect(row?.isAnonymous).toBe(false)
      },
    },
    // ------------------------------------------------------------------------------------------- UC-63 EX-1
    {
      id: 'AC2',
      role: 'secretary',
      title: 'Thông tin biểu quyết sai: form báo từng trường, không lưu; API từ chối 400 MSG209 kể cả khi bỏ qua form',
      steps: [
        'thuky@ mở form Tạo biểu quyết, xoá nội dung, sửa phương án 2 thành "đồng ý" (trùng), bỏ tích cả 4 vai trò, bấm Lưu nháp',
        'Huỷ, xác nhận "Bỏ thay đổi"',
        'API: POST nội dung rỗng; 2 phương án trùng khác hoa thường; 1 phương án; không vai trò; vai trò lạ; nội dung 301 ký tự; isAnonymous không phải bool',
      ],
      expected:
        'Form: "Nhập nội dung biểu quyết.", "Cần ít nhất 2 phương án khác nhau, không để trống.", "Chọn ít nhất một vai trò."; số biểu quyết không đổi. API: mỗi trường hợp 400 MSG209 "Thông tin biểu quyết không hợp lệ. Kiểm tra các trường được đánh dấu." kèm errors theo trường',
      async run(qa) {
        const m = await meeting(qa, 'prep', 'Published')
        const sec = await client(qa, 'secretary')
        const before = (await sec.get(votesPath(m))).body.votes.length
        await openVotesTab(qa, m)
        const writes = watchWrites(qa, (path) => path === votesPath(m))
        await press(qa, createButton(qa))
        const dlg = dialog(qa, 'Tạo biểu quyết')
        await dlg.getByRole('textbox', { name: 'Phương án 2', exact: true }).fill('đồng ý')
        for (const role of ['Chủ toạ', 'Thư ký', 'Thành viên', 'Khách']) await dlg.getByRole('checkbox', { name: role }).uncheck()
        await dlg.getByRole('button', { name: 'Lưu nháp' }).click()
        await qa.expect(dlg.getByText('Nhập nội dung biểu quyết.')).toBeVisible()
        await qa.expect(dlg.getByText('Cần ít nhất 2 phương án khác nhau, không để trống.')).toBeVisible()
        await qa.expect(dlg.getByText('Chọn ít nhất một vai trò.')).toBeVisible()
        await settled(qa)
        await qa.shot('loi-tung-truong')
        await dlg.getByRole('button', { name: 'Huỷ' }).click()
        const discard = dialog(qa, 'Bỏ thay đổi chưa lưu?')
        await qa.expect(discard).toBeVisible()
        await discard.getByRole('button', { name: 'Bỏ thay đổi' }).click()
        await qa.expect(dlg).toHaveCount(0)
        qa.expect(writes.sent, 'the form sent a write although it was refused').toEqual([])

        // VOTE_FT08: typed something, then Huỷ and "Bỏ thay đổi": nothing saved, nothing sent.
        await press(qa, createButton(qa))
        const abandoned = dialog(qa, 'Tạo biểu quyết')
        await abandoned.getByLabel('Nội dung biểu quyết').fill('Nháp bỏ dở')
        await abandoned.getByRole('button', { name: 'Huỷ' }).click()
        await qa.expect(dialog(qa, 'Bỏ thay đổi chưa lưu?')).toBeVisible()
        await settled(qa)
        await qa.shot('huy-giua-chung-hoi-bo-thay-doi')
        await dialog(qa, 'Bỏ thay đổi chưa lưu?').getByRole('button', { name: 'Bỏ thay đổi' }).click()
        await qa.expect(abandoned).toHaveCount(0)
        await qa.expect(dialog(qa, 'Bỏ thay đổi chưa lưu?')).toHaveCount(0)
        await qa.expect(panel(qa)).not.toContainText('Nháp bỏ dở')
        qa.expect(writes.sent, 'abandoning the form sent a write').toEqual([])
        writes.stop()
        await settled(qa)
        await qa.shot('khong-co-nhap-bo-do')

        const c = checker(qa)
        const cases = [
          ['nội dung rỗng', { title: '   ' }, 'title'],
          ['phương án trùng (khác hoa thường)', { options: ['Đồng ý', '  đồng ý '] }, 'options'],
          ['một phương án', { options: ['Đồng ý'] }, 'options'],
          ['phương án rỗng', { options: ['Đồng ý', ''] }, 'options'],
          ['không vai trò', { eligibleRoles: [] }, 'eligibleRoles'],
          ['vai trò lạ', { eligibleRoles: ['Admin'] }, null],
          ['nội dung 301 ký tự', { title: 'x'.repeat(301) }, 'title'],
          ['isAnonymous không phải bool', { isAnonymous: 'yes' }, null],
        ]
        for (const [label, over, field] of cases) {
          const res = await sec.post(votesPath(m), voteBody(qa, 'sai', over))
          if (c.check(label, res, 400, 'MSG209') && field)
            c.fact(`${label}: errors.${field}`, !!res.body.errors?.[field], JSON.stringify(res.body.errors))
        }
        const after = (await sec.get(votesPath(m))).body.votes.length
        c.fact('không có biểu quyết nào được tạo', after === before, `${before} → ${after}`)
        c.done()
      },
    },
    // ------------------------------------------------------------------------------------------- UC-63 AF-1
    {
      id: 'AC3',
      role: 'chair',
      devices: ['desktop', 'phone'],
      title: 'Chủ toạ sửa nháp do Thư ký tạo: đổi nội dung, thêm phương án, chuyển Bỏ phiếu kín, bỏ vai trò Khách',
      steps: [
        'thuky@ tạo nháp qua API trên cuộc họp Đã công bố',
        'chutoa@ mở tab Biểu quyết, bấm Sửa trên thẻ nháp',
        'Đổi nội dung, "Thêm phương án" → "Xin ý kiến thêm", chọn "Bỏ phiếu kín", bỏ tích "Khách", Lưu nháp',
      ],
      expected:
        'Thẻ nháp không có nút "Mở" (cuộc họp chưa diễn ra) và form không có "Mở biểu quyết"; toast "Cập nhật biểu quyết nháp thành công."; thẻ mang nội dung mới, "Kín", mở ra thấy 4 phương án và 3 vai trò; API khớp',
      async run(qa) {
        const m = await meeting(qa, 'prep', 'Published')
        const v = await draftVote(qa, m, 'Nháp để sửa')
        await openVotesTab(qa, m)
        const row = card(qa, v)
        await qa.expect(row).toBeVisible()
        await qa.expect(row.getByRole('button', { name: 'Mở', exact: true })).toHaveCount(0)
        await press(qa, row.getByRole('button', { name: `Sửa: ${v.title}` }))
        const dlg = dialog(qa, 'Sửa biểu quyết')
        await qa.expect(dlg.getByLabel('Nội dung biểu quyết')).toHaveValue(v.title)
        await qa.expect(dlg.getByRole('button', { name: 'Mở biểu quyết' })).toHaveCount(0)
        const edited = { title: `${v.title} (đã sửa)` }
        await dlg.getByLabel('Nội dung biểu quyết').fill(edited.title)
        await press(qa, dlg.getByRole('button', { name: 'Thêm phương án' }))
        await dlg.getByRole('textbox', { name: 'Phương án 4', exact: true }).fill('Xin ý kiến thêm')
        await dlg.getByRole('radio', { name: 'Bỏ phiếu kín' }).check()
        await dlg.getByRole('checkbox', { name: 'Khách' }).uncheck()
        await settled(qa)
        await qa.shot('form-sua')
        await press(qa, dlg.getByRole('button', { name: 'Lưu nháp' }))
        await qa.expect(toast(qa, 'Cập nhật biểu quyết nháp thành công.')).toBeVisible()
        const after = card(qa, edited)
        await qa.expect(after).toContainText('Kín')
        await press(qa, after.getByRole('button', { name: edited.title, exact: true }))
        await qa.expect(after.locator('.vote-option-list li')).toHaveCount(4)
        await qa.expect(after.locator('.vote-roles .badge')).toHaveText(['Chủ toạ', 'Thư ký', 'Thành viên'])
        await after.scrollIntoViewIfNeeded()
        await settled(qa)
        await qa.shot('nhap-da-sua')
        await noSidewaysScroll(qa)
        const d = (await detail(qa, 'chair', m, v)).body
        qa.expect(d.title).toBe(edited.title)
        qa.expect(d.isAnonymous).toBe(true)
        qa.expect(d.options.map((o) => o.label)).toEqual([...OPTIONS, 'Xin ý kiến thêm'])
        qa.expect(d.eligibleRoles).toEqual(['Chair', 'Secretary', 'Member'])
        qa.expect(d.status).toBe('Draft')
      },
    },
    // ------------------------------------------------------------------------------------------- UC-63 AF-2
    {
      id: 'AC4',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Thư ký xoá một nháp sau khi xác nhận; nháp khác giữ nguyên',
      steps: ['thuky@ tạo 2 nháp qua API', 'Mở tab Biểu quyết, bấm Xoá trên nháp thứ nhất, xác nhận "Xoá"'],
      expected:
        'Hộp thoại "Xoá biểu quyết nháp" hỏi "Xoá nháp “…”?"; toast "Xoá biểu quyết nháp thành công."; thẻ biến mất, nháp thứ hai còn; API GET nháp đã xoá 404 MSG205',
      async run(qa) {
        const m = await meeting(qa, 'prep', 'Published')
        const gone = await draftVote(qa, m, 'Nháp để xoá')
        const kept = await draftVote(qa, m, 'Nháp giữ lại')
        await openVotesTab(qa, m)
        await press(qa, card(qa, gone).getByRole('button', { name: `Xoá: ${gone.title}` }))
        const dlg = dialog(qa, 'Xoá biểu quyết nháp')
        await qa.expect(dlg).toContainText(`Xoá nháp “${gone.title}”?`)
        await settled(qa)
        await qa.shot('xac-nhan-xoa')
        await press(qa, dlg.getByRole('button', { name: 'Xoá', exact: true }))
        await qa.expect(toast(qa, 'Xoá biểu quyết nháp thành công.')).toBeVisible()
        await qa.expect(card(qa, gone)).toHaveCount(0)
        await qa.expect(card(qa, kept)).toBeVisible()
        await settled(qa)
        await qa.shot('da-xoa')
        await noSidewaysScroll(qa)
        const c = checker(qa)
        c.check('GET nháp đã xoá', await detail(qa, 'secretary', m, gone), 404, 'MSG205')
        c.check('GET nháp còn lại', await detail(qa, 'secretary', m, kept), 200)
        c.done()
      },
    },
    // ------------------------------------------------------------------------------------------- UC-62a AF-1, BR-086, BR-087
    {
      id: 'AC5',
      role: 'member',
      devices: ['desktop', 'phone'],
      title: 'Thành viên không thấy nháp: cuộc họp chỉ có nháp hiện "Không có biểu quyết nào để hiển thị."; API ẩn nháp bằng 404',
      steps: [
        'Cuộc họp Đã công bố có nháp của Thư ký',
        'thanhvien@ mở tab Biểu quyết',
        'API: GET danh sách, GET / PUT / DELETE nháp, POST tạo biểu quyết, POST mở nháp',
      ],
      expected:
        'Màn hình: "Không có biểu quyết nào để hiển thị.", không có nút "Tạo biểu quyết". API: danh sách rỗng, canManageDraft false; GET/PUT/DELETE nháp 404 MSG205; POST tạo 409 MSG213; mở nháp 404 MSG205',
      async run(qa) {
        const m = await meeting(qa, 'prep', 'Published')
        const v = await draftVote(qa, m, 'Nháp bị ẩn')
        await openVotesTab(qa, m)
        await qa.expect(panel(qa).getByText('Không có biểu quyết nào để hiển thị.')).toBeVisible()
        await qa.expect(createButton(qa)).toHaveCount(0)
        await qa.expect(panel(qa)).not.toContainText(v.title)
        await settled(qa)
        await qa.shot('khong-thay-nhap')
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
        if (qa.device !== 'desktop') return
        const api = await client(qa, 'member')
        const c = checker(qa)
        const list = await api.get(votesPath(m))
        if (c.check('GET danh sách', list, 200)) {
          c.fact('danh sách không có nháp', list.body.votes.length === 0, JSON.stringify(list.body.votes.map((x) => x.status)))
          c.fact('canManageDraft false', list.body.permissions.canManageDraft === false)
        }
        c.check('GET nháp', await api.get(votePath(m, v)), 404, 'MSG205')
        c.check('PUT nháp', await api.put(votePath(m, v), voteBody(qa, 'chiếm')), 404, 'MSG205')
        c.check('DELETE nháp', await api.delete(votePath(m, v)), 404, 'MSG205')
        c.check('POST tạo', await api.post(votesPath(m), voteBody(qa, 'thành viên tạo')), 409, 'MSG213')
        c.check('POST mở nháp', await api.post(`${votePath(m, v)}/open`), 404, 'MSG205')
        c.done()
      },
    },
    // ------------------------------------------------------------------------------------------- BR-016, BR-017, AGENTS.md 404
    {
      id: 'AC6',
      role: 'admin',
      title: 'Người không tham dự (quantri@) không thấy biểu quyết: trang 404, API 404 MSG205; chưa đăng nhập 401',
      steps: [
        'Cuộc họp đang diễn ra có một biểu quyết đã đóng; quantri@ không có trong thành phần',
        'quantri@ mở /mgmt/meetings/<id>?tab=votes',
        'API quantri@: GET danh sách, GET chi tiết, POST bỏ phiếu, POST tạo; API không token: GET danh sách',
      ],
      expected: '"Không tìm thấy trang"; API 404 MSG205 cho mọi lời gọi; không token 401',
      async run(qa) {
        const m = await meeting(qa, 'live', 'InProgress')
        const v = await openedVote(qa, m, 'Người ngoài không thấy')
        await closeVote(qa, m, v)
        await qa.goto(`/mgmt/meetings/${m.id}?tab=votes`)
        await qa.expect(qa.page.getByRole('heading', { name: 'Không tìm thấy trang' })).toBeVisible()
        await qa.shot('404')
        const api = await client(qa, 'admin')
        const c = checker(qa)
        c.check('GET danh sách', await api.get(votesPath(m)), 404, 'MSG205')
        c.check('GET chi tiết', await api.get(votePath(m, v)), 404, 'MSG205')
        const opt = (await detail(qa, 'chair', m, v)).body.options[0].id
        c.check('POST bỏ phiếu', await api.post(`${votePath(m, v)}/ballots`, { optionId: opt }), 404, 'MSG205')
        c.check('POST tạo', await api.post(votesPath(m), voteBody(qa, 'người ngoài')), 404, 'MSG205')
        c.check('không token', await (await qa.api('anon')).get(votesPath(m)), 401)
        c.done()
      },
    },
    // ------------------------------------------------------------------------------------------- UC-64 Normal Flow
    {
      id: 'AC7',
      role: 'chair',
      devices: ['desktop', 'phone'],
      title: 'Chủ toạ mở một nháp trong lúc họp: hộp thoại nêu vai trò sẽ chốt, thẻ chuyển "Đang mở" với 0/3',
      steps: ['Cuộc họp đang diễn ra, thuky@ tạo nháp (đủ 4 vai trò)', 'chutoa@ bấm "Mở" trên thẻ, xác nhận "Mở biểu quyết"'],
      expected:
        'Hộp thoại "Mở biểu quyết": "Người bỏ phiếu (Chủ toạ, Thư ký, Thành viên, Khách) chốt ngay lúc mở."; toast "Mở biểu quyết thành công."; chip "Đang mở", 0/3, nút "Đóng phiếu"; API Open, eligibleCount 3 (không có Khách), openedAt có giá trị',
      async run(qa) {
        const m = await meeting(qa, 'live', 'InProgress')
        const v = await draftVote(qa, m, 'Mở từ thẻ')
        try {
          await openVotesTab(qa, m)
          await press(qa, card(qa, v).getByRole('button', { name: 'Mở', exact: true }))
          const dlg = dialog(qa, 'Mở biểu quyết')
          await qa.expect(dlg).toContainText('Người bỏ phiếu (Chủ toạ, Thư ký, Thành viên, Khách)')
          await qa.expect(dlg).toContainText('chốt ngay lúc mở')
          await settled(qa)
          await qa.shot('xac-nhan-mo')
          await press(qa, dlg.getByRole('button', { name: 'Mở biểu quyết' }))
          await qa.expect(toast(qa, 'Mở biểu quyết thành công.')).toBeVisible()
          const row = card(qa, v)
          await qa.expect(row.locator('.vote-status')).toHaveText('Đang mở')
          await qa.expect(row.locator('.vote-turnout-text')).toHaveText('0/3')
          await qa.expect(row.getByRole('button', { name: 'Đóng phiếu' })).toBeVisible()
          await settled(qa)
          await qa.shot('dang-mo')
          await noSidewaysScroll(qa)
          await noRawCodes(qa)
          const d = (await detail(qa, 'chair', m, v)).body
          qa.expect(d.status).toBe('Open')
          qa.expect(d.eligibleCount).toBe(3)
          qa.expect(d.openedAt).toBeTruthy()
        } finally {
          await closeOpenVotes(qa, m)
        }
      },
    },
    // ------------------------------------------------------------------------------------------- UC-63 AF-3, UC-64 AF-1, EX-3
    {
      id: 'AC8',
      role: 'chair',
      devices: ['desktop', 'phone'],
      title: 'Chủ toạ mở ngay từ form tạo; mở thêm khi đã có phiếu đang mở thì form giữ nguyên, báo lỗi, không để lại nháp',
      steps: [
        'chutoa@ mở form Tạo biểu quyết, nhập nội dung, bấm "Mở biểu quyết"',
        'Mở lại form, nhập nội dung khác, bấm "Mở biểu quyết" trong khi phiếu đầu còn mở',
      ],
      expected:
        'Lần 1: toast "Mở biểu quyết thành công.", thẻ "Đang mở", không có nháp nào được lưu riêng. Lần 2: form còn mở, giữ nội dung đã nhập, báo "Cuộc họp đang có biểu quyết khác đang mở. Hãy đóng biểu quyết đó trước."; API không có biểu quyết nào mang nội dung thứ hai',
      async run(qa) {
        const m = await meeting(qa, 'live', 'InProgress')
        try {
          await openVotesTab(qa, m)
          await press(qa, createButton(qa))
          let dlg = dialog(qa, 'Tạo biểu quyết')
          const first = { title: qa.name(`FE-11 Mở từ form ${qa.device} ${rand()}`) }
          await dlg.getByLabel('Nội dung biểu quyết').fill(first.title)
          await settled(qa)
          await qa.shot('form-co-nut-mo')
          await press(qa, dlg.getByRole('button', { name: 'Mở biểu quyết' }))
          await qa.expect(toast(qa, 'Mở biểu quyết thành công.')).toBeVisible()
          await qa.expect(card(qa, first).locator('.vote-status')).toHaveText('Đang mở')

          await press(qa, createButton(qa))
          dlg = dialog(qa, 'Tạo biểu quyết')
          const second = qa.name(`FE-11 Mở thêm bị chặn ${qa.device} ${rand()}`)
          await dlg.getByLabel('Nội dung biểu quyết').fill(second)
          await press(qa, dlg.getByRole('button', { name: 'Mở biểu quyết' }))
          await qa.expect(dlg.getByRole('alert')).toHaveText(MSG.MSG219)
          // The user must see why nothing opened without hunting for it below the fold.
          await qa.expect(dlg.getByRole('alert')).toBeInViewport()
          await qa.expect(dlg.getByLabel('Nội dung biểu quyết')).toHaveValue(second)
          await settled(qa)
          await qa.shot('mo-them-bi-chan')
          await noSidewaysScroll(qa)
          const list = (await (await client(qa, 'chair')).get(votesPath(m))).body.votes
          qa.expect(list.filter((x) => x.title === second), 'a draft was left by the failed open').toHaveLength(0)
          qa.expect(list.filter((x) => x.title === first.title).map((x) => x.status)).toEqual(['Open'])
        } finally {
          await closeOpenVotes(qa, m)
        }
      },
    },
    // ------------------------------------------------------------------------------------------- UC-64 EX-2..EX-4
    {
      id: 'AC9',
      role: 'secretary',
      title: 'Chỉ Chủ toạ mở được, mỗi lúc một phiếu: Thư ký không có nút Mở; API MSG218 / MSG219 / MSG220 / MSG213',
      steps: [
        'Cuộc họp đang diễn ra; thuky@ mở tab Biểu quyết: thẻ nháp và form Tạo biểu quyết',
        'API: Thư ký mở nháp; Thư ký và Thành viên tạo kèm openImmediately; Chủ toạ mở nháp chỉ chọn Khách (cuộc họp không có khách)',
        'Chủ toạ mở A rồi mở B, sửa B kèm openImmediately; mở lại A đang mở / đã đóng; sửa và xoá A đã đóng',
      ],
      expected:
        'Thẻ nháp có Sửa / Xoá nhưng không có "Mở", form không có "Mở biểu quyết". API: Thư ký 409 MSG218; Thư ký openImmediately 409 MSG218, Thành viên 409 MSG213 (không tạo gì); chỉ Khách 409 MSG220, vẫn Nháp; B 409 MSG219 cả hai đường, B không đổi; A đang mở / đã đóng 409 MSG218; sửa / xoá A đã đóng 409 MSG213',
      async run(qa) {
        const m = await meeting(qa, 'live', 'InProgress')
        const draft = await draftVote(qa, m, 'Thư ký không mở được')
        await openVotesTab(qa, m)
        const row = card(qa, draft)
        await qa.expect(row.getByRole('button', { name: `Sửa: ${draft.title}` })).toBeVisible()
        await qa.expect(row.getByRole('button', { name: 'Mở', exact: true })).toHaveCount(0)
        await row.scrollIntoViewIfNeeded()
        await settled(qa)
        await qa.shot('thu-ky-khong-co-nut-mo')
        await createButton(qa).click()
        const dlg = dialog(qa, 'Tạo biểu quyết')
        await qa.expect(dlg.getByRole('button', { name: 'Lưu nháp' })).toBeVisible()
        await qa.expect(dlg.getByRole('button', { name: 'Mở biểu quyết' })).toHaveCount(0)
        await settled(qa)
        await qa.shot('form-thu-ky')

        const sec = await client(qa, 'secretary')
        const chair = await client(qa, 'chair')
        const c = checker(qa)
        c.check('Thư ký mở nháp', await sec.post(`${votePath(m, draft)}/open`), 409, 'MSG218')
        const secOpen = voteBody(qa, 'Thư ký mở ngay', { openImmediately: true })
        c.check('Thư ký openImmediately', await sec.post(votesPath(m), secOpen), 409, 'MSG218')
        const memOpen = voteBody(qa, 'Thành viên mở ngay', { openImmediately: true })
        c.check('Thành viên openImmediately', await (await client(qa, 'member')).post(votesPath(m), memOpen), 409, 'MSG213')
        const guestsOnly = await draftVote(qa, m, 'Chỉ khách', { eligibleRoles: ['Guest'] })
        c.check('mở khi không ai đủ điều kiện', await chair.post(`${votePath(m, guestsOnly)}/open`), 409, 'MSG220')
        c.fact('nháp chỉ khách vẫn Nháp', (await detail(qa, 'chair', m, guestsOnly)).body.status === 'Draft')
        const a = await draftVote(qa, m, 'A')
        const b = await draftVote(qa, m, 'B')
        c.check('mở A', await chair.post(`${votePath(m, a)}/open`), 200, 'MSG217')
        c.check('mở B khi A đang mở', await chair.post(`${votePath(m, b)}/open`), 409, 'MSG219')
        const bEdit = voteBody(qa, 'B sửa', { openImmediately: true })
        c.check('sửa B kèm openImmediately', await chair.put(votePath(m, b), bEdit), 409, 'MSG219')
        const bNow = (await detail(qa, 'chair', m, b)).body
        c.fact('B giữ nguyên', bNow.title === b.title && bNow.status === 'Draft', `${bNow.title} ${bNow.status}`)
        c.check('mở A lần nữa', await chair.post(`${votePath(m, a)}/open`), 409, 'MSG218')
        c.check('đóng A', await chair.post(`${votePath(m, a)}/close`), 200, 'MSG229')
        c.check('mở A đã đóng', await chair.post(`${votePath(m, a)}/open`), 409, 'MSG218')
        c.check('sửa A đã đóng', await chair.put(votePath(m, a), voteBody(qa, 'A sửa')), 409, 'MSG213')
        c.check('xoá A đã đóng', await chair.delete(votePath(m, a)), 409, 'MSG213')
        const titles = (await chair.get(votesPath(m))).body.votes.map((x) => x.title)
        c.fact('không tạo gì từ openImmediately bị từ chối', !titles.includes(secOpen.title) && !titles.includes(memOpen.title))
        c.done()
      },
    },
    // ------------------------------------------------------------------------------------------- UC-65 Normal Flow, EX-5
    {
      id: 'AC10',
      role: 'member',
      devices: ['desktop', 'phone'],
      title: 'Thành viên bỏ phiếu công khai một lần; khi phiếu còn mở không thấy kết quả theo phương án; không đổi, không rút được',
      steps: [
        'chutoa@ mở một biểu quyết công khai (đủ vai trò)',
        'thanhvien@ mở tab Biểu quyết, chọn "Không đồng ý", bấm "Gửi phiếu"; tải lại trang',
        'API: gửi phiếu lần hai',
      ],
      expected:
        'Trước: 3 phương án, "Gửi rồi không đổi, không rút được.", "Gửi phiếu" khoá tới khi chọn, 0/3, không có kết quả. Sau: toast "Đã ghi nhận phiếu của bạn.", "Bạn đã gửi phiếu.", 1/3, chip "Đã bỏ phiếu"; tải lại vẫn vậy. Lần hai 409 MSG226 "Phiếu của bạn đã được ghi nhận, không thể đổi hoặc rút.", lượt bỏ vẫn 1',
      async run(qa) {
        const m = await meeting(qa, 'live', 'InProgress')
        const v = await openedVote(qa, m, 'Thành viên bỏ phiếu')
        try {
          await openVotesTab(qa, m)
          const row = card(qa, v)
          await qa.expect(row.getByRole('radio')).toHaveCount(3)
          await qa.expect(row.getByText('Gửi rồi không đổi, không rút được.')).toBeVisible()
          const send = row.getByRole('button', { name: 'Gửi phiếu' })
          await qa.expect(send).toBeDisabled()
          await qa.expect(row.locator('.vote-turnout-text')).toHaveText('0/3')
          await qa.expect(row.locator('.vote-result')).toHaveCount(0)
          await settled(qa)
          await qa.shot('phieu-truoc-khi-gui')
          await press(qa, row.getByRole('radio', { name: 'Không đồng ý' }))
          await qa.expect(send).toBeEnabled()
          await press(qa, send)
          await qa.expect(toast(qa, 'Đã ghi nhận phiếu của bạn.')).toBeVisible()
          await qa.expect(row.getByText('Bạn đã gửi phiếu.')).toBeVisible()
          await qa.expect(row.locator('.vote-turnout-text')).toHaveText('1/3')
          await qa.expect(row.locator('.vote-voted')).toHaveText('Đã bỏ phiếu')
          await qa.expect(row.getByRole('radio')).toHaveCount(0)
          await settled(qa)
          await qa.shot('da-gui-phieu')
          await noSidewaysScroll(qa)
          await qa.page.reload()
          await qa.expect(card(qa, v).getByText('Bạn đã gửi phiếu.')).toBeVisible()
          await qa.expect(card(qa, v).getByRole('radio')).toHaveCount(0)
          const c = checker(qa)
          c.check('gửi lần hai', await cast(qa, 'member', m, v, 0), 409, 'MSG226')
          const d = (await detail(qa, 'member', m, v)).body
          c.fact('lượt bỏ vẫn 1', d.receiptCount === 1, `receiptCount ${d.receiptCount}`)
          c.fact('hasVoted', d.hasVoted === true)
          c.fact('không có kết quả khi còn mở', d.result === null, JSON.stringify(d.result))
          c.done()
        } finally {
          await closeOpenVotes(qa, m)
        }
      },
    },
    // ------------------------------------------------------------------------------------------- UC-65 EX-1..EX-4, EX-7
    {
      id: 'AC11',
      role: 'member',
      title: 'Thành viên ngoài danh sách thấy phiếu nhưng không bỏ được; API MSG224 / MSG222 / MSG223 / MSG225 / MSG205',
      steps: [
        'chutoa@ mở biểu quyết chỉ cho Chủ toạ và Thư ký',
        'thanhvien@ mở tab Biểu quyết',
        'API: thanhvien@ bỏ phiếu; thuky@ gửi thiếu optionId, optionId của biểu quyết khác, rồi đúng; đóng; chutoa@ bỏ phiếu sau khi đóng; thanhvien@ bỏ phiếu vào nháp',
      ],
      expected:
        'Thẻ "Đang mở", 0/2, "Bạn không có trong danh sách bỏ phiếu.", không có nút gửi. API: 409 MSG224; 400 MSG222; 400 MSG223; 200 MSG227; sau khi đóng 409 MSG225; nháp 404 MSG205',
      async run(qa) {
        const m = await meeting(qa, 'live', 'InProgress')
        const other = await draftVote(qa, m, 'Nháp khác')
        const v = await openedVote(qa, m, 'Chỉ Chủ toạ và Thư ký', { eligibleRoles: ['Chair', 'Secretary'] })
        try {
          await openVotesTab(qa, m)
          const row = card(qa, v)
          await qa.expect(row.locator('.vote-turnout-text')).toHaveText('0/2')
          await qa.expect(row.getByText('Bạn không có trong danh sách bỏ phiếu.')).toBeVisible()
          await qa.expect(row.getByRole('button', { name: 'Gửi phiếu' })).toHaveCount(0)
          await settled(qa)
          await qa.shot('khong-trong-danh-sach')
          await noRawCodes(qa)
          const c = checker(qa)
          const sec = await client(qa, 'secretary')
          const ballots = `${votePath(m, v)}/ballots`
          c.check('thành viên ngoài danh sách', await cast(qa, 'member', m, v, 0), 409, 'MSG224')
          c.check('thiếu optionId', await sec.post(ballots, {}), 400, 'MSG222')
          const foreign = (await detail(qa, 'secretary', m, other)).body.options[0].id
          c.check('optionId của biểu quyết khác', await sec.post(ballots, { optionId: foreign }), 400, 'MSG223')
          c.check('thư ký bỏ phiếu', await cast(qa, 'secretary', m, v, 1), 200, 'MSG227')
          await closeVote(qa, m, v)
          c.check('bỏ phiếu sau khi đóng', await cast(qa, 'chair', m, v, 0), 409, 'MSG225')
          const memberApi = await client(qa, 'member')
          const otherOpt = foreign
          c.check('bỏ phiếu vào nháp bị ẩn', await memberApi.post(`${votePath(m, other)}/ballots`, { optionId: otherOpt }), 404, 'MSG205')
          const d = (await detail(qa, 'chair', m, v)).body
          c.fact('lượt bỏ 1/2', d.receiptCount === 1 && d.eligibleCount === 2, `${d.receiptCount}/${d.eligibleCount}`)
          c.done()
        } finally {
          await closeOpenVotes(qa, m)
        }
      },
    },
    // ------------------------------------------------------------------------------------------- UC-62b step 5, realtime
    {
      id: 'AC12',
      role: 'member',
      title: 'Lượt bỏ phiếu và việc đóng phiếu tự cập nhật trên màn hình người khác, không cần tải lại',
      steps: [
        'chutoa@ mở biểu quyết; thanhvien@ để mở tab Biểu quyết',
        'chutoa@ rồi thuky@ bỏ phiếu qua API; chutoa@ đóng phiếu qua API',
      ],
      expected: 'Trên trang thanhvien@: 0/3 → 1/3 → 2/3, rồi chip "Đã đóng" và kết quả, mỗi lần trong vài giây, không tải lại',
      async run(qa) {
        const m = await meeting(qa, 'live', 'InProgress')
        const v = await openedVote(qa, m, 'Cập nhật trực tiếp')
        try {
          await openVotesTab(qa, m)
          const row = card(qa, v)
          await qa.expect(row.locator('.vote-turnout-text')).toHaveText('0/3')
          await qa.shot('0-tren-3')
          let marker = await qa.page.evaluate(() => (window.__qaMarker = Math.random()))
          const quick = { timeout: 8_000 }
          let t0 = Date.now()
          qa.expectStatus(await cast(qa, 'chair', m, v, 0), 200)
          await qa.expect(row.locator('.vote-turnout-text')).toHaveText('1/3', quick)
          const first = Date.now() - t0
          t0 = Date.now()
          qa.expectStatus(await cast(qa, 'secretary', m, v, 2), 200)
          await qa.expect(row.locator('.vote-turnout-text')).toHaveText('2/3', quick)
          const second = Date.now() - t0
          await settled(qa)
          await qa.shot('2-tren-3-khong-tai-lai')
          t0 = Date.now()
          await closeVote(qa, m, v)
          await qa.expect(row.locator('.vote-status')).toHaveText(/Đã đóng/, quick)
          const third = Date.now() - t0
          qa.expect(await qa.page.evaluate(() => window.__qaMarker), 'the page was reloaded').toBe(marker)
          await press(qa, row.getByRole('button', { name: v.title, exact: true }))
          await qa.expect(row.locator('.vote-result li')).toHaveCount(3)
          await row.locator('.vote-result').scrollIntoViewIfNeeded()
          await settled(qa)
          await qa.shot('da-dong-co-ket-qua')
          qa.note(`Thời gian cập nhật: ${first} ms, ${second} ms, ${third} ms (panel còn đọc lại 15 giây một lần khi có phiếu mở)`)
          marker = null
        } finally {
          await closeOpenVotes(qa, m)
        }
      },
    },
    // ------------------------------------------------------------------------------------------- UC-66, UC-62b AF-2 public
    {
      id: 'AC13',
      role: 'chair',
      devices: ['desktop', 'phone'],
      title: 'Chủ toạ đóng phiếu công khai: kết quả theo phương án, % trên số phiếu đã bỏ, từng người chọn gì hoặc không bỏ',
      steps: [
        'chutoa@ mở biểu quyết công khai; thanhvien@ chọn Đồng ý, thuky@ chọn Không đồng ý (API)',
        'chutoa@ bấm "Đóng phiếu", xác nhận',
        'Mở "Phiếu từng người"',
      ],
      expected:
        'Hộp thoại: "Đã bỏ 2/3; đóng rồi không ai bỏ thêm được."; toast "Đã đóng biểu quyết. Kết quả đã sẵn sàng."; Đồng ý 1 · 50%, Không đồng ý 1 · 50%, Không ý kiến 0 · 0%; "Phiếu từng người (2/3)": Lê An – Đồng ý, Trần Việt Hải – Không đồng ý, Chủ toạ – Không bỏ phiếu; form kết luận không chọn sẵn, "Ghi kết luận" khoá. API: bỏ phiếu sau khi đóng 409 MSG225',
      async run(qa) {
        const u = await users(qa)
        const m = await meeting(qa, 'live', 'InProgress')
        const v = await openedVote(qa, m, 'Đóng công khai')
        try {
          qa.expectStatus(await cast(qa, 'member', m, v, 0), 200)
          qa.expectStatus(await cast(qa, 'secretary', m, v, 1), 200)
          await openVotesTab(qa, m)
          const row = card(qa, v)
          await qa.expect(row.locator('.vote-turnout-text')).toHaveText('2/3')
          await press(qa, row.getByRole('button', { name: 'Đóng phiếu' }))
          const dlg = dialog(qa, 'Đóng biểu quyết')
          await qa.expect(dlg).toContainText('Đã bỏ 2/3; đóng rồi không ai bỏ thêm được.')
          await settled(qa)
          await qa.shot('xac-nhan-dong')
          await press(qa, dlg.getByRole('button', { name: 'Đóng phiếu' }))
          await qa.expect(toast(qa, 'Đã đóng biểu quyết. Kết quả đã sẵn sàng.')).toBeVisible()
          await qa.expect(row.locator('.vote-status')).toHaveText(/Đã đóng/)
          const results = row.locator('.vote-result li')
          await qa.expect(results).toHaveCount(3)
          await qa.expect(results.nth(0)).toContainText('Đồng ý')
          await qa.expect(results.nth(0).locator('.vote-result-count')).toHaveText('1 · 50%')
          await qa.expect(results.nth(1).locator('.vote-result-count')).toHaveText('1 · 50%')
          await qa.expect(results.nth(2).locator('.vote-result-count')).toHaveText('0 · 0%')
          const voters = row.locator('details.vote-voters')
          await qa.expect(voters.locator('summary')).toHaveText('Phiếu từng người (2/3)')
          await press(qa, voters.locator('summary'))
          await qa.expect(voters.locator('li').filter({ hasText: u.member.name })).toContainText('Đồng ý')
          await qa.expect(voters.locator('li').filter({ hasText: u.secretary.name })).toContainText('Không đồng ý')
          await qa.expect(voters.locator('li').filter({ hasText: u.chair.name })).toContainText('Không bỏ phiếu')
          const form = row.locator('form.vote-conclusion-form')
          // It starts on "Thông qua" so it reads as a choice to make (Hải 23/11/2026); nothing is recorded until pressed.
          await qa.expect(form.getByRole('radio', { checked: true })).toHaveCount(1)
          await qa.expect(form.getByRole('radio', { name: 'Thông qua', exact: true })).toBeChecked()
          await row.scrollIntoViewIfNeeded()
          await settled(qa)
          await qa.shot('ket-qua-cong-khai', { fullPage: true })
          await noSidewaysScroll(qa)
          await noRawCodes(qa)
          const c = checker(qa)
          c.check('bỏ phiếu sau khi đóng', await cast(qa, 'chair', m, v, 2), 409, 'MSG225')
          const d = (await detail(qa, 'member', m, v)).body
          c.fact('thành viên thấy kết quả công khai', d.result?.voters?.length === 3, JSON.stringify(d.result?.voters))
          c.done()
        } finally {
          await closeOpenVotes(qa, m)
        }
      },
    },
    // ------------------------------------------------------------------------------------------- UC-67
    {
      id: 'AC14',
      role: 'chair',
      devices: ['desktop', 'phone'],
      title: 'Chủ toạ ghi kết luận tự chọn (không suy từ số phiếu), kèm ghi chú; ghi rồi chỉ đọc; API MSG233 / MSG232 / MSG234',
      steps: [
        'Biểu quyết đã đóng: thanhvien@ chọn Đồng ý (1/1 phiếu)',
        'chutoa@ chọn "Không thông qua", nhập ghi chú, bấm "Ghi kết luận"',
        'API: ghi lần hai; biểu quyết đã đóng khác: "passed" viết thường, ghi chú 2001 ký tự, Thư ký ghi, Thành viên ghi, ghi trên phiếu đang mở; rồi Chủ toạ ghi "Deferred"',
      ],
      expected:
        'Toast "Ghi kết luận biểu quyết thành công."; chip "Không thông qua", ghi chú hiện, không còn form. API: lần hai 409 MSG233; sai giá trị / ghi chú dài 400 MSG232; Thư ký, Thành viên, phiếu đang mở 409 MSG234; "Deferred" 200 MSG235',
      async run(qa) {
        const m = await meeting(qa, 'live', 'InProgress')
        const v = await openedVote(qa, m, 'Kết luận')
        qa.expectStatus(await cast(qa, 'member', m, v, 0), 200)
        await closeVote(qa, m, v)
        await openVotesTab(qa, m)
        const row = card(qa, v)
        const form = row.locator('form.vote-conclusion-form')
        await qa.expect(form).toBeVisible()
        await press(qa, form.getByRole('radio', { name: /^Không (thông )?qua$/ }))
        const note = 'QA: chưa đủ số liệu, chủ toạ kết luận không thông qua dù phiếu đồng ý nhiều hơn.'
        await form.getByLabel('Ghi chú').fill(note)
        await press(qa, form.getByRole('button', { name: 'Ghi kết luận' }))
        await qa.expect(toast(qa, 'Ghi kết luận biểu quyết thành công.')).toBeVisible()
        await qa.expect(row.locator('.vote-box .vote-conclusion-chip')).toHaveText('Không thông qua')
        await qa.expect(row.getByText(note)).toBeVisible()
        await qa.expect(row.locator('form.vote-conclusion-form')).toHaveCount(0)
        await row.scrollIntoViewIfNeeded()
        await settled(qa)
        await qa.shot('da-ghi-ket-luan')
        await noSidewaysScroll(qa)
        // VOTE_FT23: the Member sees the recorded conclusion with its note, and still does after F5; nothing to edit.
        await qa.signIn('member')
        await openVotesTab(qa, m)
        await expand(qa, v)
        const mrow = card(qa, v)
        await qa.expect(mrow.locator('.vote-meta .vote-conclusion-chip')).toHaveText('Không thông qua')
        await qa.expect(mrow.getByText(note)).toBeVisible()
        await qa.expect(mrow.locator('form.vote-conclusion-form')).toHaveCount(0)
        await mrow.scrollIntoViewIfNeeded()
        await settled(qa)
        await qa.shot('thanh-vien-thay-ket-luan')
        await qa.page.reload()
        await expand(qa, v)
        await qa.expect(card(qa, v).locator('.vote-meta .vote-conclusion-chip')).toHaveText('Không thông qua')
        await qa.expect(card(qa, v).getByText(note)).toBeVisible()
        await qa.expect(card(qa, v).locator('form.vote-conclusion-form')).toHaveCount(0)
        await card(qa, v).scrollIntoViewIfNeeded()
        await settled(qa)
        await qa.shot('thanh-vien-sau-khi-tai-lai')
        if (qa.device !== 'desktop') return
        const chair = await client(qa, 'chair')
        const c = checker(qa)
        c.check('ghi lần hai', await chair.post(`${votePath(m, v)}/conclusion`, { conclusion: 'Passed', note: '' }), 409, 'MSG233')
        const d = (await detail(qa, 'member', m, v)).body
        c.fact('kết luận giữ nguyên', d.conclusion?.value === 'NotPassed' && d.conclusion?.note === note, JSON.stringify(d.conclusion))
        const w = await openedVote(qa, m, 'Kết luận sai')
        c.check('ghi trên phiếu đang mở', await chair.post(`${votePath(m, w)}/conclusion`, { conclusion: 'Passed' }), 409, 'MSG234')
        await closeVote(qa, m, w)
        const at = `${votePath(m, w)}/conclusion`
        c.check('"passed" viết thường', await chair.post(at, { conclusion: 'passed' }), 400, 'MSG232')
        c.check('ghi chú 2001 ký tự', await chair.post(at, { conclusion: 'Passed', note: 'x'.repeat(2001) }), 400, 'MSG232')
        c.check('Thư ký ghi', await (await client(qa, 'secretary')).post(at, { conclusion: 'Passed' }), 409, 'MSG234')
        c.check('Thành viên ghi', await (await client(qa, 'member')).post(at, { conclusion: 'Passed' }), 409, 'MSG234')
        c.check('Chủ toạ ghi Deferred', await chair.post(at, { conclusion: 'Deferred' }), 200, 'MSG235')
        c.done()
      },
    },
    // ------------------------------------------------------------------------------------------- BR-096, BR-097 secret ballot
    {
      id: 'AC15',
      role: 'member',
      devices: ['desktop', 'phone'],
      title: 'Phiếu kín: sau khi gửi không hiện lại lựa chọn; khi đóng chỉ có tổng và %, không có danh sách từng người',
      steps: [
        'chutoa@ mở biểu quyết Bỏ phiếu kín',
        'thanhvien@ chọn Đồng ý, Gửi phiếu',
        'chutoa@ chọn Đồng ý, thuky@ Không đồng ý (API); chutoa@ đóng; thanhvien@ tải lại, mở thẻ',
      ],
      expected:
        'Thẻ ghi "Kín"; sau khi gửi: "Bạn đã gửi phiếu." không kèm lựa chọn; khi đóng: Đồng ý 2 · 66,67%, Không đồng ý 1 · 33,33%, Không ý kiến 0 · 0%, không có "Phiếu từng người"; API result.voters null',
      async run(qa) {
        const m = await meeting(qa, 'live', 'InProgress')
        const v = await openedVote(qa, m, 'Phiếu kín', { isAnonymous: true })
        try {
          await openVotesTab(qa, m)
          const row = card(qa, v)
          await qa.expect(row.locator('.vote-meta')).toContainText('Kín')
          await press(qa, row.getByRole('radio', { name: 'Đồng ý', exact: true }))
          await press(qa, row.getByRole('button', { name: 'Gửi phiếu' }))
          await qa.expect(toast(qa, 'Đã ghi nhận phiếu của bạn.')).toBeVisible()
          await qa.expect(row.getByText('Bạn đã gửi phiếu.')).toBeVisible()
          await qa.expect(row.locator('.vote-body')).not.toContainText('Đồng ý')
          await settled(qa)
          await qa.shot('kin-da-gui')
          qa.expectStatus(await cast(qa, 'chair', m, v, 0), 200)
          qa.expectStatus(await cast(qa, 'secretary', m, v, 1), 200)
          await closeVote(qa, m, v)
          await qa.page.reload()
          const after = card(qa, v)
          await qa.expect(after.locator('.vote-status')).toHaveText(/Đã đóng/)
          await press(qa, after.getByRole('button', { name: v.title, exact: true }))
          const results = after.locator('.vote-result li .vote-result-count')
          await qa.expect(results).toHaveText([/^2 · 66[,.]67%$/, /^1 · 33[,.]33%$/, '0 · 0%'])
          await qa.expect(after.locator('details.vote-voters')).toHaveCount(0)
          await after.scrollIntoViewIfNeeded()
          await settled(qa)
          await qa.shot('kin-ket-qua')
          await noSidewaysScroll(qa)
          const d = (await detail(qa, 'member', m, v)).body
          qa.expect(d.result.voters).toBeNull()
          qa.expect(d.result.ballotCount).toBe(3)
        } finally {
          await closeOpenVotes(qa, m)
        }
      },
    },
    // ------------------------------------------------------------------------------------------- UC-66 EX-1
    {
      id: 'AC16',
      role: 'secretary',
      title: 'Chỉ Chủ toạ đóng được phiếu đang mở (API): Thư ký, Thành viên 409 MSG230; đóng nháp / đóng lần hai 409 MSG230',
      steps: ['chutoa@ mở biểu quyết', 'API: thuky@ đóng, thanhvien@ đóng, chutoa@ đóng nháp, chutoa@ đóng, chutoa@ đóng lần nữa'],
      expected: '409 MSG230 cho mọi lời gọi sai; chutoa@ 200 MSG229 kèm result; danh sách của Thư ký có permissions.canClose false',
      async run(qa) {
        const m = await meeting(qa, 'live', 'InProgress')
        const d = await draftVote(qa, m, 'Nháp không đóng được')
        const v = await openedVote(qa, m, 'Đóng sai người')
        // VOTE_FT20: on the screen the Secretary has no "Đóng phiếu" and no "Mở" on the open vote.
        await openVotesTab(qa, m)
        const row = card(qa, v)
        await qa.expect(row.locator('.vote-status')).toHaveText('Đang mở')
        await qa.expect(row.getByRole('button', { name: 'Đóng phiếu' })).toHaveCount(0)
        await qa.expect(row.getByRole('button', { name: 'Mở', exact: true })).toHaveCount(0)
        await qa.expect(createButton(qa)).toBeVisible()
        await row.scrollIntoViewIfNeeded()
        await settled(qa)
        await qa.shot('thu-ky-khong-co-nut-dong')
        const c = checker(qa)
        const list = (await (await client(qa, 'secretary')).get(votesPath(m))).body
        c.fact('Thư ký canClose false', list.permissions.canClose === false && list.permissions.canOpen === false)
        c.fact('Thư ký canManageDraft true', list.permissions.canManageDraft === true)
        c.check('Thư ký đóng', await (await client(qa, 'secretary')).post(`${votePath(m, v)}/close`), 409, 'MSG230')
        c.check('Thành viên đóng', await (await client(qa, 'member')).post(`${votePath(m, v)}/close`), 409, 'MSG230')
        const chair = await client(qa, 'chair')
        c.check('đóng nháp', await chair.post(`${votePath(m, d)}/close`), 409, 'MSG230')
        const closed = await chair.post(`${votePath(m, v)}/close`)
        if (c.check('Chủ toạ đóng', closed, 200, 'MSG229')) c.fact('kèm result', Array.isArray(closed.body.result?.options))
        c.check('đóng lần hai', await chair.post(`${votePath(m, v)}/close`), 409, 'MSG230')
        c.done()
      },
    },
    // ------------------------------------------------------------------------------------------- UC-68a, UC-42, UC-65 EX-8
    {
      id: 'AC17',
      role: 'chair',
      title: 'Tài liệu biểu quyết: Chủ toạ gắn từ tài liệu cuộc họp vào nháp; người bỏ phiếu phải xác nhận đã đọc trước khi gửi phiếu',
      steps: [
        'chutoa@ thêm một PDF vào cuộc họp đang diễn ra; thuky@ tạo nháp',
        'chutoa@ bấm kẹp giấy trên thẻ nháp → "Chọn từ tài liệu cuộc họp", tích tệp, "Gắn 1 tài liệu"; API gắn lại tệp đó',
        'chutoa@ mở biểu quyết; thanhvien@ mở tab, chọn phương án',
        'API thanhvien@: bỏ phiếu; xác nhận đọc với lastPageReached false',
        'thanhvien@ bấm tài liệu, trong trình xem phủ trên tab bấm "Xác nhận đã đọc", đóng trình xem, gửi phiếu',
      ],
      expected:
        'Toast "Gắn tài liệu vào biểu quyết thành công.", thẻ có kẹp giấy 1; gắn lại 409 MSG266. Thành viên: tài liệu "Chưa đọc", "Gửi phiếu" khoá dù đã chọn, gợi ý "Mở từng tài liệu, đọc tới trang cuối rồi xác nhận."; API bỏ phiếu 409 MSG252 kèm documents; lastPageReached false 400 MSG254. Sau xác nhận: "Đã đọc", gửi được, toast "Đã ghi nhận phiếu của bạn."',
      async run(qa) {
        const m = await meeting(qa, 'live', 'InProgress')
        await openVotesTab(qa, m)
        const fileName = `qa-fe11-${rand()}.pdf`
        const up = await uploadMeetingPdf(qa, m, fileName)
        if (up.status === 503) qa.inconclusive(`Kho tệp local không chạy (503 ${up.body?.code}). Người kiểm: docker compose up -d (SeaweedFS) và FileStorage:* user-secrets, rồi chạy lại AC17.`)
        qa.expect(up.status, JSON.stringify(up.body)).toBe(201)
        const docId = up.body.documents[0].id
        const v = await draftVote(qa, m, 'Có tài liệu')
        try {
          await qa.page.reload()
          const row = card(qa, v)
          await row.getByRole('button', { name: `Thêm tài liệu: ${v.title}` }).click()
          await qa.page.getByRole('menuitem', { name: 'Chọn từ tài liệu cuộc họp' }).click()
          const dlg = dialog(qa, 'Thêm tài liệu cho biểu quyết')
          await dlg.getByRole('checkbox', { name: new RegExp(fileName.replace('.', '\\.')) }).check()
          await settled(qa)
          await qa.shot('chon-tai-lieu')
          await dlg.getByRole('button', { name: 'Gắn 1 tài liệu' }).click()
          await qa.expect(toast(qa, 'Gắn tài liệu vào biểu quyết thành công.')).toBeVisible()
          await qa.expect(row.locator('.vote-meta')).toContainText('1')
          await qa.expect(row.locator('.vote-docs')).toContainText(fileName)
          await settled(qa)
          await qa.shot('da-gan-tai-lieu')
          const c = checker(qa)
          const chair = await client(qa, 'chair')
          c.check('gắn lại', await chair.post(`${votePath(m, v)}/documents`, { meetingDocumentIds: [docId] }), 409, 'MSG266')
          const hiddenDraft = await draftVote(qa, m, 'Nháp ẩn với thành viên')
          await openVote(qa, m, v)

          await qa.signIn('member')
          await openVotesTab(qa, m)
          const mrow = card(qa, v)
          await qa.expect(mrow.locator('.vote-docs li').filter({ hasText: fileName })).toContainText('Chưa đọc')
          // VOTE_FT10: a Member sees the open vote's documents but no paperclip and no detach; the draft is not listed.
          await qa.expect(mrow.getByRole('button', { name: `Thêm tài liệu: ${v.title}` })).toHaveCount(0)
          await qa.expect(mrow.getByRole('button', { name: `Gỡ khỏi biểu quyết: ${fileName}` })).toHaveCount(0)
          await qa.expect(card(qa, hiddenDraft)).toHaveCount(0)
          await press(qa, mrow.getByRole('radio', { name: 'Đồng ý', exact: true }))
          await qa.expect(mrow.getByRole('button', { name: 'Gửi phiếu' })).toBeDisabled()
          await qa.expect(mrow.getByText('Mở từng tài liệu, đọc tới trang cuối rồi xác nhận.')).toBeVisible()
          await settled(qa)
          await qa.shot('chua-doc-khoa-gui')
          const blocked = await cast(qa, 'member', m, v, 0)
          if (c.check('bỏ phiếu khi chưa đọc', blocked, 409, 'MSG252'))
            c.fact('MSG252 kèm tài liệu chưa đọc', blocked.body.documents?.some((d) => d.id === docId), JSON.stringify(blocked.body.documents))
          const confirmAt = `/api/meetings/${m.id}/documents/${docId}/read-confirmation`
          c.check('lastPageReached false', await (await client(qa, 'member')).post(confirmAt, { lastPageReached: false }), 400, 'MSG254')

          await mrow.getByRole('link', { name: new RegExp(fileName.replace('.', '\\.')) }).click()
          // UC-42: locked until the reader scrolls to the end of the last page; an A4 page at 100% is taller than the window.
          const locked = qa.page.getByRole('button', { name: 'Đọc tới trang cuối' })
          await qa.expect(locked).toBeDisabled({ timeout: 20_000 })
          await qa.page.locator('.viewer-scroll .pdf-page').first().waitFor()
          await qa.page.locator('.viewer-scroll').evaluate((el) => el.scrollTo(0, el.scrollHeight))
          const confirm = qa.page.getByRole('button', { name: 'Xác nhận đã đọc' })
          await qa.expect(confirm).toBeEnabled({ timeout: 20_000 })
          await settled(qa)
          await qa.shot('trinh-xem-xac-nhan')
          await confirm.click()
          await qa.expect(toast(qa, 'Đã xác nhận đọc tài liệu.')).toBeVisible()
          // ADR-0066: the viewer is a layer over the votes tab (a reload keeps it open); closing it re-reads the panel.
          const viewer = qa.page.getByRole('dialog', { name: 'Xem tài liệu' })
          await viewer.getByRole('button', { name: 'Đóng', exact: true }).click()
          await qa.expect(viewer).toHaveCount(0)
          const back = card(qa, v)
          await qa.expect(back.locator('.vote-docs li').filter({ hasText: fileName })).toContainText('Đã đọc')
          await back.getByRole('radio', { name: 'Đồng ý', exact: true }).check()
          await back.getByRole('button', { name: 'Gửi phiếu' }).click()
          await qa.expect(toast(qa, 'Đã ghi nhận phiếu của bạn.')).toBeVisible()
          await settled(qa)
          await qa.shot('da-doc-da-gui')
          await noRawCodes(qa)
          c.check('gỡ tài liệu khi đã mở', await chair.delete(`${votePath(m, v)}/documents/${docId}`), 409, 'MSG213')
          c.done()
        } finally {
          await closeOpenVotes(qa, m)
        }
      },
    },
    // ------------------------------------------------------------------------------------------- UC-68b
    {
      id: 'AC18',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Thư ký gỡ tài liệu khỏi biểu quyết nháp; tài liệu vẫn ở trong cuộc họp',
      steps: [
        'thuky@ thêm PDF vào cuộc họp đang diễn ra, tạo nháp và gắn PDF (API)',
        'Mở thẻ nháp, bấm "Gỡ khỏi biểu quyết" ở tệp, xác nhận "Gỡ"',
        'API: gỡ lần nữa; GET tài liệu cuộc họp',
      ],
      expected:
        'Hộp thoại "Gỡ tài liệu khỏi biểu quyết": "Gỡ “…” khỏi biểu quyết? Tài liệu vẫn ở trong cuộc họp."; toast "Gỡ tài liệu khỏi biểu quyết thành công."; tệp biến khỏi thẻ; gỡ lần nữa 404 MSG269; tệp vẫn trong danh sách tài liệu cuộc họp',
      async run(qa) {
        const m = await meeting(qa, 'live', 'InProgress')
        await openVotesTab(qa, m)
        const fileName = `qa-fe11-go-${rand()}.pdf`
        const up = await uploadMeetingPdf(qa, m, fileName)
        if (up.status === 503) qa.inconclusive(`Kho tệp local không chạy (503 ${up.body?.code}). Người kiểm: bật SeaweedFS rồi chạy lại AC18.`)
        qa.expect(up.status, JSON.stringify(up.body)).toBe(201)
        const docId = up.body.documents[0].id
        const v = await draftVote(qa, m, 'Gỡ tài liệu')
        const sec = await client(qa, 'secretary')
        qa.expectStatus(await sec.post(`${votePath(m, v)}/documents`, { meetingDocumentIds: [docId] }), 200)
        await qa.page.reload()
        const row = card(qa, v)
        await press(qa, row.getByRole('button', { name: v.title, exact: true }))
        await press(qa, row.getByRole('button', { name: `Gỡ khỏi biểu quyết: ${fileName}` }))
        const dlg = dialog(qa, 'Gỡ tài liệu khỏi biểu quyết')
        await qa.expect(dlg).toContainText(`Gỡ “${fileName}” khỏi biểu quyết? Tài liệu vẫn ở trong cuộc họp.`)
        await settled(qa)
        await qa.shot('xac-nhan-go')
        await press(qa, dlg.getByRole('button', { name: 'Gỡ', exact: true }))
        await qa.expect(toast(qa, 'Gỡ tài liệu khỏi biểu quyết thành công.')).toBeVisible()
        await qa.expect(row).not.toContainText(fileName)
        await settled(qa)
        await qa.shot('da-go')
        await noSidewaysScroll(qa)
        const c = checker(qa)
        c.check('gỡ lần nữa', await sec.delete(`${votePath(m, v)}/documents/${docId}`), 404, 'MSG269')
        const docs = await sec.get(`/api/meetings/${m.id}/documents`)
        c.fact('tệp vẫn trong cuộc họp', JSON.stringify(docs.body).includes(docId), `HTTP ${docs.status}`)
        c.done()
      },
    },
    // ------------------------------------------------------------------------------------------- BR-086, BR-087 / BR-090 by state
    {
      id: 'AC19',
      role: 'secretary',
      title: 'Quyền theo trạng thái cuộc họp (API): Nháp / Chờ duyệt / Đã huỷ / Đã kết thúc không tạo được; Đã duyệt chỉ chuẩn bị, Thành viên chưa thấy',
      steps: [
        'Cuộc họp mới: thuky@ tạo biểu quyết ở Nháp, Chờ duyệt; ở Đã duyệt: thuky@ và chutoa@ tạo, chutoa@ mở, thanhvien@ xem; huỷ rồi tạo',
        'Cuộc họp Đã công bố: tạo; Chủ toạ bắt đầu: tạo; kết thúc: tạo, xem nháp cũ',
      ],
      expected:
        'Nháp, Chờ duyệt, Đã huỷ, Đã kết thúc: 409 MSG213. Đã duyệt: 201 MSG210 cho cả hai, mở 409 MSG218, Thành viên GET 404 MSG205. Đã công bố, Đang diễn ra: 201. Sau kết thúc: danh sách của Chủ toạ không còn nháp, GET nháp cũ 404 MSG205',
      async run(qa) {
        const sec = await client(qa, 'secretary')
        const chair = await client(qa, 'chair')
        const c = checker(qa)
        const s = await createMeeting(qa, `${TITLES.states} ${rand()}`, 'Draft', 'states')
        c.check('Nháp: tạo', await sec.post(votesPath(s), voteBody(qa, 'nháp')), 409, 'MSG213')
        await step(qa, s.id, 'submit')
        c.check('Chờ duyệt: tạo', await sec.post(votesPath(s), voteBody(qa, 'chờ duyệt')), 409, 'MSG213')
        await step(qa, s.id, 'approve')
        const a1 = await sec.post(votesPath(s), voteBody(qa, 'đã duyệt thư ký'))
        c.check('Đã duyệt: Thư ký tạo', a1, 201, 'MSG210')
        c.check('Đã duyệt: Chủ toạ tạo', await chair.post(votesPath(s), voteBody(qa, 'đã duyệt chủ toạ')), 201, 'MSG210')
        if (a1.status === 201) c.check('Đã duyệt: mở', await chair.post(`/api/meetings/${s.id}/votes/${a1.body.voteId}/open`), 409, 'MSG218')
        c.check('Đã duyệt: Thành viên xem', await (await client(qa, 'member')).get(votesPath(s)), 404, 'MSG205')
        await step(qa, s.id, 'cancel')
        c.check('Đã huỷ: tạo', await sec.post(votesPath(s), voteBody(qa, 'đã huỷ')), 409, 'MSG213')

        const p = await meeting(qa, 'prep', 'Published')
        c.check('Đã công bố: tạo', await sec.post(votesPath(p), voteBody(qa, 'đã công bố')), 201, 'MSG210')
        await step(qa, p.id, 'start')
        const live = await sec.post(votesPath(p), voteBody(qa, 'đang diễn ra'))
        c.check('Đang diễn ra: tạo', live, 201, 'MSG210')
        await step(qa, p.id, 'end')
        MEET.prep = null
        c.check('Đã kết thúc: tạo', await sec.post(votesPath(p), voteBody(qa, 'đã kết thúc')), 409, 'MSG213')
        const after = (await chair.get(votesPath(p))).body
        c.fact('không còn nháp sau kết thúc', after.votes.every((x) => x.status !== 'Draft'), JSON.stringify(after.votes.map((x) => x.status)))
        if (live.status === 201) c.check('GET nháp cũ', await chair.get(`/api/meetings/${p.id}/votes/${live.body.voteId}`), 404, 'MSG205')
        qa.note(`Cuộc họp: ${s.id} (Đã huỷ), ${p.id} (Đã kết thúc)`)
        c.done()
      },
    },
    // ------------------------------------------------------------------------------------------- UC-66 AF-1, UC-67 AF-1, BR-092, BR-093, BR-086, BR-087
    {
      id: 'AC20',
      role: 'chair',
      title: 'Kết thúc cuộc họp: nháp bị xoá, phiếu đang mở không nhận thêm nhưng Chủ toạ vẫn đóng và ghi kết luận được',
      steps: [
        'Cuộc họp đang diễn ra có: nháp N, phiếu đang mở O (thanhvien@ đã bỏ), phiếu đã đóng C chưa kết luận',
        'chutoa@ kết thúc cuộc họp (API)',
        'chutoa@ mở tab Biểu quyết: thẻ O, bấm "Đóng phiếu"; ghi kết luận "Thông qua" cho C',
        'API: thuky@ bỏ phiếu vào O trước khi đóng, thuky@ tạo biểu quyết, GET N',
      ],
      expected:
        'N biến mất (GET 404 MSG205); O vẫn "Đang mở" 1/3, Chủ toạ thấy "Biểu quyết không còn nhận phiếu." và nút "Đóng phiếu"; thuky@ bỏ phiếu 409 MSG225; đóng được (toast MSG229); kết luận C sau khi kết thúc: toast "Ghi kết luận biểu quyết thành công."; tạo mới 409 MSG213',
      async run(qa) {
        const m = await meeting(qa, 'live', 'InProgress')
        const cl = await openedVote(qa, m, 'C đóng trước khi kết thúc')
        await closeVote(qa, m, cl)
        const o = await openedVote(qa, m, 'O còn mở lúc kết thúc')
        qa.expectStatus(await cast(qa, 'member', m, o, 0), 200)
        const n = await draftVote(qa, m, 'N nháp lúc kết thúc')
        await step(qa, m.id, 'end')
        MEET.live = null
        const c = checker(qa)
        c.check('GET nháp N', await detail(qa, 'chair', m, n), 404, 'MSG205')
        c.check('thư ký bỏ phiếu vào O', await cast(qa, 'secretary', m, o, 1), 409, 'MSG225')
        c.check('thư ký tạo', await (await client(qa, 'secretary')).post(votesPath(m), voteBody(qa, 'sau kết thúc')), 409, 'MSG213')
        await openVotesTab(qa, m)
        await qa.expect(card(qa, n)).toHaveCount(0)
        const row = card(qa, o)
        await qa.expect(row.locator('.vote-status')).toHaveText('Đang mở')
        await qa.expect(row.locator('.vote-turnout-text')).toHaveText('1/3')
        await qa.expect(row.getByText('Biểu quyết không còn nhận phiếu.')).toBeVisible()
        await qa.expect(row.getByRole('button', { name: 'Gửi phiếu' })).toHaveCount(0)
        await row.scrollIntoViewIfNeeded()
        await settled(qa)
        await qa.shot('sau-ket-thuc-phieu-con-mo')
        await row.getByRole('button', { name: 'Đóng phiếu' }).click()
        await dialog(qa, 'Đóng biểu quyết').getByRole('button', { name: 'Đóng phiếu' }).click()
        await qa.expect(toast(qa, 'Đã đóng biểu quyết. Kết quả đã sẵn sàng.')).toBeVisible()
        await qa.expect(row.locator('.vote-status')).toHaveText(/Đã đóng/)
        const crow = card(qa, cl)
        const form = crow.locator('form.vote-conclusion-form')
        await form.getByRole('radio', { name: 'Thông qua', exact: true }).check()
        await form.getByRole('button', { name: 'Ghi kết luận' }).click()
        await qa.expect(toast(qa, 'Ghi kết luận biểu quyết thành công.')).toBeVisible()
        await qa.expect(crow.locator('.vote-meta .vote-conclusion-chip')).toHaveText('Thông qua')
        // UC-67 step 6: the conclusion becomes read-only (as in AC14), the form gives way to it.
        await qa.expect(crow.locator('.vote-box .vote-conclusion-chip')).toHaveText('Thông qua')
        await qa.expect(crow.locator('form.vote-conclusion-form')).toHaveCount(0)
        await crow.scrollIntoViewIfNeeded()
        await settled(qa)
        await qa.shot('dong-va-ket-luan-sau-ket-thuc')
        await noRawCodes(qa)
        qa.note(`Cuộc họp ${m.id} đã kết thúc`)
        c.done()
      },
    },
    // ------------------------------------------------------------------------------------------- Guest, room
    {
      id: 'AC21',
      role: 'anon',
      title: 'Khách được mời bỏ phiếu bằng phiên khách đã xác minh; Khách mời sau lúc mở không có trong danh sách',
      steps: [
        'Chủ toạ mời một Khách (email thật), Khách nhập mã OTP gửi qua email để vào trang khách',
        'Chủ toạ mở biểu quyết có vai trò Khách; Khách bỏ phiếu; mời thêm Khách thứ hai, Khách này mở biểu quyết đó',
      ],
      expected: 'Khách thứ nhất bỏ phiếu được (MSG227); Khách thứ hai thấy phiếu nhưng không bỏ được (MSG224), eligibleCount không đổi',
      async run(qa) {
        qa.inconclusive(
          'Cần mã OTP gửi tới hộp thư của Khách, bộ chạy không đọc được. Người kiểm: mời một email thật vào một cuộc họp [QA] FE-11 đang diễn ra, vào trang khách bằng OTP, rồi làm các bước trên.',
        )
      },
    },
    {
      id: 'AC22',
      role: 'anon',
      title: 'Trong phòng họp trực tuyến: thông báo nổi "Chủ toạ mở biểu quyết" và panel Biểu quyết',
      steps: ['Chủ toạ và Thành viên cùng vào phòng họp LiveKit của cuộc họp đang diễn ra', 'Chủ toạ mở một biểu quyết'],
      expected: 'Thành viên thấy thông báo nổi với nút "Bỏ phiếu", bấm mở panel Biểu quyết và bỏ phiếu được',
      async run(qa) {
        qa.inconclusive(
          'Cần hai người trong một phòng LiveKit thật (máy chủ media không chạy trong bộ chạy này). Người kiểm: hai trình duyệt, chutoa@ và thanhvien@ vào phòng của một cuộc họp [QA] FE-11 đang diễn ra, chutoa@ mở biểu quyết.',
        )
      },
    },
    // ------------------------------------------------------------------------------------------- UC-62a / 52b list
    {
      id: 'AC23',
      role: 'chair',
      devices: ['desktop', 'phone'],
      title: 'Tab Biểu quyết của cuộc họp đang diễn ra: nháp, đang mở, đã đóng trên một màn; Thành viên không thấy nháp và nút tạo',
      steps: [
        'Cuộc họp đang diễn ra có một biểu quyết kín đã đóng (1 phiếu), một biểu quyết công khai đang mở, một nháp',
        'chutoa@ mở tab Biểu quyết',
        'thanhvien@ mở lại tab Biểu quyết',
      ],
      expected:
        'Chủ toạ: ba thẻ, chip "Đã đóng" / "Đang mở" / "Nháp", "Kín" hoặc "Công khai", lượt bỏ 1/3 và 0/3 (thẻ nháp không có lượt bỏ); nút "Tạo biểu quyết" hiện. Thành viên: chỉ hai thẻ, không có nháp, không có nút "Tạo biểu quyết"',
      async run(qa) {
        const m = await meeting(qa, 'live', 'InProgress')
        const closed = await openedVote(qa, m, 'Danh sách đã đóng', { isAnonymous: true })
        qa.expectStatus(await cast(qa, 'member', m, closed, 0), 200)
        await closeVote(qa, m, closed)
        const open = await openedVote(qa, m, 'Danh sách đang mở')
        const draft = await draftVote(qa, m, 'Danh sách nháp')
        try {
          await openVotesTab(qa, m)
          const cClosed = card(qa, closed)
          const cOpen = card(qa, open)
          const cDraft = card(qa, draft)
          await qa.expect(cClosed.locator('.vote-status')).toHaveText(/Đã đóng/)
          await qa.expect(cClosed.locator('.vote-meta')).toContainText('Kín')
          await qa.expect(cClosed.locator('.vote-turnout-text')).toHaveText('1/3')
          await qa.expect(cOpen.locator('.vote-status')).toHaveText('Đang mở')
          await qa.expect(cOpen.locator('.vote-meta')).toContainText('Công khai')
          await qa.expect(cOpen.locator('.vote-turnout-text')).toHaveText('0/3')
          await qa.expect(cDraft.locator('.vote-status')).toHaveText(/Nháp/)
          await qa.expect(cDraft.locator('.vote-turnout-text')).toHaveCount(0)
          await qa.expect(createButton(qa)).toBeVisible()
          await settled(qa)
          await qa.shot('chu-toa-ba-trang-thai', { fullPage: true })
          await noSidewaysScroll(qa)
          await noRawCodes(qa)

          await qa.signIn('member')
          await openVotesTab(qa, m)
          await qa.expect(card(qa, closed)).toBeVisible()
          await qa.expect(card(qa, open)).toBeVisible()
          await qa.expect(card(qa, draft)).toHaveCount(0)
          await qa.expect(panel(qa).locator('.vote-status', { hasText: 'Nháp' })).toHaveCount(0)
          await qa.expect(createButton(qa)).toHaveCount(0)
          await settled(qa)
          await qa.shot('thanh-vien-khong-thay-nhap', { fullPage: true })
          await noSidewaysScroll(qa)
        } finally {
          await closeOpenVotes(qa, m)
        }
      },
    },
    {
      id: 'AC24',
      role: 'chair',
      devices: ['desktop', 'phone'],
      title: 'Cuộc họp chưa có biểu quyết: câu "Không có biểu quyết nào để hiển thị."; gợi ý tạo chỉ hiện với người quản lý nháp',
      steps: ['Cuộc họp Đã công bố chưa có biểu quyết nào', 'chutoa@ mở tab Biểu quyết', 'thanhvien@ mở tab Biểu quyết'],
      expected:
        'Chủ toạ: "Không có biểu quyết nào để hiển thị." kèm "Tạo biểu quyết để chuẩn bị trước, mở khi đang họp." và nút "Tạo biểu quyết". Thành viên: chỉ câu đầu, không gợi ý, không nút',
      async run(qa) {
        const m = await meeting(qa, 'empty', 'Published')
        await openVotesTab(qa, m)
        await qa.expect(panel(qa).getByText('Không có biểu quyết nào để hiển thị.')).toBeVisible()
        await qa.expect(panel(qa).getByText('Tạo biểu quyết để chuẩn bị trước, mở khi đang họp.')).toBeVisible()
        await qa.expect(createButton(qa)).toBeVisible()
        await settled(qa)
        await qa.shot('chu-toa-chua-co-bieu-quyet')
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
        await qa.signIn('member')
        await openVotesTab(qa, m)
        await qa.expect(panel(qa).getByText('Không có biểu quyết nào để hiển thị.')).toBeVisible()
        await qa.expect(panel(qa).getByText('Tạo biểu quyết để chuẩn bị trước')).toHaveCount(0)
        await qa.expect(createButton(qa)).toHaveCount(0)
        await settled(qa)
        await qa.shot('thanh-vien-chua-co-bieu-quyet')
        await noSidewaysScroll(qa)
      },
    },
    {
      id: 'AC25',
      role: 'member',
      title: 'Người đang xem tab Biểu quyết bị gỡ khỏi cuộc họp: màn hình đổi sang trang "Không tìm thấy trang", API 404 MSG205',
      steps: [
        'thanhvien@ mở tab Biểu quyết của một cuộc họp Đã công bố',
        'thuky@ gỡ thanhvien@ khỏi thành phần (API, có If-Match)',
        'Chờ panel tự đọc lại (sự kiện participantsChanged)',
      ],
      expected: 'Trang đổi sang "Không tìm thấy trang" (không lộ cuộc họp có tồn tại); API GET danh sách 404 MSG205; không có mã MSG nào hiện ra',
      async run(qa) {
        const u = await users(qa)
        const m = await createMeeting(qa, `${TITLES.gone} ${rand()}`, 'Published', 'gone')
        try {
          await openVotesTab(qa, m)
          await qa.expect(panel(qa).getByText('Không có biểu quyết nào để hiển thị.')).toBeVisible()
          qa.expect(await removeFromRoster(qa, m.id, u.member.id), 'gỡ thành viên khỏi cuộc họp').toBe(200)
          // The meeting page itself re-reads on the same event and answers its 404 page (existence is not revealed);
          // the panel's own sentence shows only if the panel's read comes first.
          await qa.expect(qa.page.getByRole('heading', { name: 'Không tìm thấy trang' })).toBeVisible({ timeout: 20_000 })
          await settled(qa)
          await qa.shot('khong-con-xem-duoc')
          await noRawCodes(qa)
          const c = checker(qa)
          c.check('GET danh sách sau khi bị gỡ', await (await client(qa, 'member')).get(votesPath(m)), 404, 'MSG205')
          c.done()
        } finally {
          await step(qa, m.id, 'cancel')
        }
      },
    },
    // ------------------------------------------------------------------------------------------- UC-62a EX-2 (AX)
    {
      id: 'AC26',
      role: 'chair',
      devices: ['desktop', 'phone'],
      title: 'Lỗi tải danh sách (chèn 500 MSG206): tab báo lỗi kèm "Thử lại"; bấm "Thử lại" khi hết lỗi thì danh sách hiện',
      steps: [
        'Chặn GET /api/meetings/<id>/votes trả 500 MSG206 (page.route)',
        'chutoa@ mở tab Biểu quyết',
        'Bỏ chặn, bấm "Thử lại"',
      ],
      expected:
        'Hộp báo lỗi "Không tải được danh sách biểu quyết. Vui lòng thử lại." và nút "Thử lại", không có danh sách, không lộ mã MSG; sau Thử lại: danh sách các biểu quyết hiện',
      async run(qa) {
        const m = await meeting(qa, 'prep', 'Published')
        const v = await draftVote(qa, m, 'Danh sách sau thử lại')
        const failing = (url) => url.pathname === votesPath(m)
        await qa.page.route(failing, (route) =>
          route.request().method() === 'GET'
            ? route.fulfill({
                status: 500,
                contentType: 'application/json',
                body: JSON.stringify({ code: 'MSG206', message: 'Không tải được danh sách biểu quyết. Vui lòng thử lại.' }),
              })
            : route.continue(),
        )
        await qa.goto(`/mgmt/meetings/${m.id}?tab=votes`)
        const alert = qa.page.getByRole('alert').filter({ hasText: 'Không tải được danh sách biểu quyết. Vui lòng thử lại.' })
        await qa.expect(alert).toBeVisible()
        await qa.expect(alert.getByRole('button', { name: 'Thử lại' })).toBeVisible()
        await qa.expect(qa.page.locator('li.vote-item')).toHaveCount(0)
        await settled(qa)
        await qa.shot('loi-tai-danh-sach')
        await noRawCodes(qa)
        await noSidewaysScroll(qa)
        await qa.page.unroute(failing)
        await press(qa, alert.getByRole('button', { name: 'Thử lại' }))
        await qa.expect(card(qa, v)).toBeVisible()
        await qa.expect(alert).toHaveCount(0)
        await settled(qa)
        await qa.shot('thu-lai-thanh-cong')
      },
    },
    // ------------------------------------------------------------------------------------------- UC-63 on an Approved meeting
    {
      id: 'AC27',
      role: 'chair',
      devices: ['desktop', 'phone'],
      title: 'Chủ toạ tạo nháp trên cuộc họp Đã duyệt: chỉ chọn Chủ toạ và Thành viên; "Lưu nháp" hiện "Đang lưu…", thẻ mới có chip "Nháp"',
      steps: [
        'chutoa@ mở tab Biểu quyết của cuộc họp Đã duyệt, bấm "Tạo biểu quyết"',
        'Nhập nội dung, giữ Công khai, bỏ tích Thư ký và Khách (còn Chủ toạ, Thành viên), bấm "Lưu nháp" (yêu cầu bị giữ 1 giây để thấy nút đang lưu)',
      ],
      expected:
        'Form có "Nội dung biểu quyết", "Phương án" (3 dòng), "Hình thức" (Công khai / Bỏ phiếu kín), "Người được bỏ phiếu" (4 vai trò), "Mô tả"; nút "Đang lưu…" rồi toast "Tạo biểu quyết nháp thành công."; thẻ "Nháp" với 2 vai trò Chủ toạ, Thành viên; API: Draft, Công khai, roles Chair + Member',
      async run(qa) {
        const m = await meeting(qa, 'approved', 'Approved')
        await openVotesTab(qa, m)
        await press(qa, createButton(qa))
        const dlg = dialog(qa, 'Tạo biểu quyết')
        await qa.expect(dlg).toBeVisible()
        for (const name of ['Nội dung biểu quyết', 'Mô tả']) await qa.expect(dlg.getByLabel(name)).toBeVisible()
        await qa.expect(dlg.getByText('Phương án', { exact: true })).toBeVisible()
        await qa.expect(dlg.getByRole('radio', { name: 'Công khai' })).toBeVisible()
        await qa.expect(dlg.getByRole('radio', { name: 'Bỏ phiếu kín' })).toBeVisible()
        const title = qa.name(`FE-11 Thông qua kế hoạch quý IV ${qa.device} ${rand()}`)
        await dlg.getByLabel('Nội dung biểu quyết').fill(title)
        for (const role of ['Thư ký', 'Khách']) await dlg.getByRole('checkbox', { name: role }).uncheck()
        await qa.expect(dlg.getByRole('checkbox', { name: 'Chủ toạ' })).toBeChecked()
        await qa.expect(dlg.getByRole('checkbox', { name: 'Thành viên' })).toBeChecked()
        await settled(qa)
        await qa.shot('form-da-nhap')
        const held = (url) => url.pathname === votesPath(m)
        await qa.page.route(held, async (route) => {
          if (route.request().method() !== 'POST') return route.continue()
          await new Promise((r) => setTimeout(r, 1200))
          await route.continue()
        })
        try {
          await press(qa, dlg.getByRole('button', { name: 'Lưu nháp' }))
          await qa.expect(dlg.getByRole('button', { name: 'Đang lưu…' })).toBeVisible()
          await settled(qa)
          await qa.shot('dang-luu')
          await qa.expect(toast(qa, 'Tạo biểu quyết nháp thành công.')).toBeVisible()
        } finally {
          await qa.page.unroute(held)
        }
        const row = card(qa, { title })
        await qa.expect(row.locator('.vote-status')).toHaveText(/Nháp/)
        await press(qa, row.getByRole('button', { name: title, exact: true }))
        await qa.expect(row.locator('.vote-roles .badge')).toHaveText(['Chủ toạ', 'Thành viên'])
        await row.scrollIntoViewIfNeeded()
        await settled(qa)
        await qa.shot('nhap-da-tao')
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
        const list = (await (await client(qa, 'chair')).get(votesPath(m))).body
        const made = list.votes.find((x) => x.title === title)
        try {
          qa.expect(made?.status).toBe('Draft')
          qa.expect(made?.isAnonymous).toBe(false)
          qa.expect(made?.eligibleRoles).toEqual(['Chair', 'Member'])
        } finally {
          if (made) await (await client(qa, 'secretary')).delete(votePath(m, made))
        }
      },
    },
    // ------------------------------------------------------------------------------------------- UC-68a storage full (simulated)
    {
      id: 'AC28',
      role: 'chair',
      title: 'Kho tài liệu hệ thống gần đầy (giả lập qua phản hồi kho): hộp thoại gắn tệp từ máy chặn tệp trước khi gửi',
      steps: [
        'Giả lập GET /api/documents báo kho chỉ còn 100 byte (page.route; không đổi cấu hình máy chủ)',
        'chutoa@ mở thẻ nháp, kẹp giấy → "Tải tệp lên từ máy", chọn một PDF lớn hơn 100 byte',
      ],
      expected:
        'Hộp thoại báo "Bộ nhớ tài liệu của hệ thống không đủ chỗ. Hãy báo quản trị viên." kèm "Đang chọn … hệ thống còn …"; nút gắn bị khoá; không có yêu cầu tải lên nào được gửi',
      async run(qa) {
        const m = await meeting(qa, 'prep', 'Published')
        const v = await draftVote(qa, m, 'Kho đầy')
        const library = (url) => url.pathname === '/api/documents'
        await qa.page.route(library, async (route) => {
          if (route.request().method() !== 'GET') return route.continue()
          const res = await route.fetch()
          const body = await res.json()
          body.storageUsedBytes = body.storageLimitBytes - 100
          await route.fulfill({ response: res, json: body })
        })
        const uploads = watchWrites(qa, (path) => path.includes('/upload'))
        await openVotesTab(qa, m)
        await press(qa, card(qa, v).getByRole('button', { name: `Thêm tài liệu: ${v.title}` }))
        await qa.page.getByRole('menuitem', { name: 'Tải tệp lên từ máy' }).click()
        const dlg = dialog(qa, 'Thêm tài liệu cho biểu quyết')
        await qa.expect(dlg).toBeVisible()
        await dlg.locator('input[type=file]').setInputFiles({
          name: `qa-fe11-day-${rand()}.pdf`,
          mimeType: 'application/pdf',
          buffer: Buffer.from(makePdf('QA FE-11 kho day'), 'base64'),
        })
        await qa.expect(dlg.getByText('Bộ nhớ tài liệu của hệ thống không đủ chỗ. Hãy báo quản trị viên.')).toBeVisible()
        await qa.expect(dlg.getByText(/Đang chọn .*, hệ thống còn/)).toBeVisible()
        await settled(qa)
        await qa.shot('kho-day')
        await noRawCodes(qa)
        await noSidewaysScroll(qa)
        await qa.expect(dlg.getByRole('button', { name: /^(Thêm|Gắn|Tải)/ }).first()).toBeDisabled()
        qa.expect(uploads.sent, 'an upload request left the browser').toEqual([])
        uploads.stop()
      },
    },
    // ------------------------------------------------------------------------------------------- UC-65 EX-6 (AX)
    {
      id: 'AC29',
      role: 'member',
      devices: ['desktop', 'phone'],
      title: 'Phiếu không ghi được (chèn 503 MSG228): báo lỗi, phiếu vẫn mở để bỏ lại; gửi lại thì ghi đúng một phiếu',
      steps: [
        'chutoa@ mở biểu quyết công khai',
        'Chèn POST .../ballots trả 503 MSG228; thanhvien@ chọn "Đồng ý", bấm "Gửi phiếu"',
        'Bỏ chèn, bấm "Gửi phiếu" lần nữa',
      ],
      expected:
        'Lần 1: thông báo lỗi "Không ghi được phiếu. Chưa có phiếu nào được ghi. Vui lòng thử lại.", hộp phiếu còn các lựa chọn, lượt bỏ 0/3. Lần 2: toast "Đã ghi nhận phiếu của bạn.", "Bạn đã gửi phiếu.", 1/3; API receiptCount 1',
      async run(qa) {
        const m = await meeting(qa, 'live', 'InProgress')
        const v = await openedVote(qa, m, 'Lỗi ghi phiếu')
        try {
          await openVotesTab(qa, m)
          const row = card(qa, v)
          const ballots = (url) => url.pathname === `${votePath(m, v)}/ballots`
          await qa.page.route(ballots, (route) =>
            route.fulfill({
              status: 503,
              contentType: 'application/json',
              body: JSON.stringify({ code: 'MSG228', message: MSG228 }),
            }),
          )
          await press(qa, row.getByRole('radio', { name: 'Đồng ý', exact: true }))
          await press(qa, row.getByRole('button', { name: 'Gửi phiếu' }))
          await qa.expect(qa.page.getByText(MSG228).first()).toBeVisible()
          await qa.expect(row.getByRole('radio')).toHaveCount(3)
          await qa.expect(row.locator('.vote-turnout-text')).toHaveText('0/3')
          await qa.expect(row.getByText('Bạn đã gửi phiếu.')).toHaveCount(0)
          await settled(qa)
          await qa.shot('loi-ghi-phieu')
          await noRawCodes(qa)
          await noSidewaysScroll(qa)
          await qa.page.unroute(ballots)
          await qa.expect(row.getByRole('button', { name: 'Gửi phiếu' })).toBeEnabled()
          await press(qa, row.getByRole('button', { name: 'Gửi phiếu' }))
          await qa.expect(toast(qa, 'Đã ghi nhận phiếu của bạn.')).toBeVisible()
          await qa.expect(row.getByText('Bạn đã gửi phiếu.')).toBeVisible()
          await qa.expect(row.locator('.vote-turnout-text')).toHaveText('1/3')
          await settled(qa)
          await qa.shot('gui-lai-thanh-cong')
          const d = (await detail(qa, 'member', m, v)).body
          qa.expect(d.receiptCount).toBe(1)
        } finally {
          await closeOpenVotes(qa, m)
        }
      },
    },
    // ------------------------------------------------------------------------------------------- UC-76 cancelled
    {
      id: 'AC30',
      role: 'chair',
      devices: ['desktop', 'phone'],
      title: 'Chủ toạ huỷ hộp "Đóng biểu quyết": phiếu vẫn "Đang mở", không có yêu cầu đóng nào được gửi',
      steps: ['chutoa@ bấm "Đóng phiếu" trên phiếu đang mở', 'Trong hộp thoại "Đóng biểu quyết" bấm "Huỷ"'],
      expected: 'Hộp thoại đóng, chip vẫn "Đang mở", nút "Đóng phiếu" còn; không có POST .../close; API status Open',
      async run(qa) {
        const m = await meeting(qa, 'live', 'InProgress')
        const v = await openedVote(qa, m, 'Huỷ đóng phiếu')
        try {
          await openVotesTab(qa, m)
          const row = card(qa, v)
          const writes = watchWrites(qa, (path) => path.endsWith('/close'))
          await press(qa, row.getByRole('button', { name: 'Đóng phiếu', exact: true }))
          const dlg = dialog(qa, 'Đóng biểu quyết')
          await qa.expect(dlg).toBeVisible()
          await settled(qa)
          await qa.shot('hop-thoai-dong')
          await press(qa, dlg.getByRole('button', { name: 'Huỷ' }))
          await qa.expect(dlg).toHaveCount(0)
          await qa.expect(row.locator('.vote-status')).toHaveText('Đang mở')
          await qa.expect(row.getByRole('button', { name: 'Đóng phiếu', exact: true })).toBeVisible()
          await settled(qa)
          await qa.shot('van-dang-mo')
          await noSidewaysScroll(qa)
          qa.expect(writes.sent, 'a close request was sent').toEqual([])
          writes.stop()
          qa.expect((await detail(qa, 'chair', m, v)).body.status).toBe('Open')
        } finally {
          await closeOpenVotes(qa, m)
        }
      },
    },
    // ------------------------------------------------------------------------------------------- UC-67 abandoned
    {
      id: 'AC31',
      role: 'chair',
      devices: ['desktop', 'phone'],
      title: 'Chủ toạ chọn kết luận và gõ ghi chú nhưng không ghi: rời tab rồi quay lại vẫn chưa có kết luận; Thành viên thấy "Chưa có kết luận."',
      steps: [
        'Biểu quyết đã đóng chưa kết luận',
        'chutoa@ chọn "Thông qua", gõ ghi chú, không bấm "Ghi kết luận"; sang tab "Thông tin chung" rồi quay lại tab Biểu quyết',
        'thanhvien@ mở thẻ đó',
      ],
      expected:
        'Chủ toạ: không có chip kết luận, form "Ghi kết luận" còn và trống; không có POST .../conclusion; API conclusion null. Thành viên: "Chưa có kết luận."',
      async run(qa) {
        const m = await meeting(qa, 'live', 'InProgress')
        const v = await openedVote(qa, m, 'Bỏ dở kết luận')
        qa.expectStatus(await cast(qa, 'member', m, v, 0), 200)
        await closeVote(qa, m, v)
        try {
          await openVotesTab(qa, m)
          const row = card(qa, v)
          const writes = watchWrites(qa, (path) => path.endsWith('/conclusion'))
          const form = row.locator('form.vote-conclusion-form')
          await press(qa, form.getByRole('radio', { name: 'Thông qua', exact: true }))
          await form.getByLabel('Ghi chú').fill('Gõ dở, chưa ghi.')
          await settled(qa)
          await qa.shot('chon-chua-ghi')
          await press(qa, qa.page.getByRole('tab', { name: 'Thông tin chung' }))
          await qa.expect(qa.page.getByRole('tab', { name: 'Thông tin chung' })).toHaveAttribute('aria-selected', 'true')
          await press(qa, qa.page.getByRole('tab', { name: 'Biểu quyết' }))
          await qa.expect(panel(qa)).toBeVisible()
          const back = card(qa, v)
          await qa.expect(back.locator('form.vote-conclusion-form')).toBeVisible()
          await qa.expect(back.locator('.vote-conclusion-chip')).toHaveCount(0)
          await qa.expect(back.locator('form.vote-conclusion-form').getByRole('radio', { checked: true })).toHaveCount(1)
          await qa.expect(back.locator('form.vote-conclusion-form').getByRole('radio', { name: 'Thông qua', exact: true })).toBeChecked()
          await back.scrollIntoViewIfNeeded()
          await settled(qa)
          await qa.shot('quay-lai-van-chua-ket-luan')
          await noSidewaysScroll(qa)
          qa.expect(writes.sent, 'a conclusion was sent').toEqual([])
          writes.stop()
          qa.expect((await detail(qa, 'chair', m, v)).body.conclusion).toBeNull()

          await qa.signIn('member')
          await openVotesTab(qa, m)
          await expand(qa, v)
          await qa.expect(card(qa, v).getByText('Chưa có kết luận.')).toBeVisible()
          await card(qa, v).scrollIntoViewIfNeeded()
          await settled(qa)
          await qa.shot('thanh-vien-chua-co-ket-luan')
        } finally {
          await closeOpenVotes(qa, m)
        }
      },
    },
    // ------------------------------------------------------------------------------------------- UC-42 read gate
    // R1/FE-11 (24/11/2026): AC mới — DOC_FT51 (nút xác nhận chỉ mở ở trang cuối, 200 MSG253).
    {
      id: 'AC32',
      role: 'member',
      devices: ['desktop', 'phone'],
      title: 'Nút xác nhận chỉ mở ở trang cuối: "Đọc tới trang cuối" khoá, cuộn hết thì "Xác nhận đã đọc", bấm xong 200 MSG253 (DOC_FT51)',
      steps: [
        'chutoa@ thêm một PDF vào cuộc họp đang diễn ra, gắn vào biểu quyết đang mở; thanhvien@ mở tab, chọn phương án',
        'thanhvien@ bấm tài liệu (trình xem phủ trên tab mở với ?read=1), đọc nút ở đầu trước khi cuộn',
        'Cuộn xuống hết trang cuối, bấm "Xác nhận đã đọc", đóng trình xem, gửi phiếu',
      ],
      expected:
        'Trước khi cuộn nút xanh "Đọc tới trang cuối" bị khoá; cuộn hết thì thành "Xác nhận đã đọc" và mở được (UC-42); bấm: backend 200 MSG253, toast "Đã xác nhận đọc tài liệu.", đầu trình xem "Đã xác nhận đọc"; về thẻ tài liệu "Đã đọc", "Gửi phiếu" mở được',
      async run(qa) {
        const c = checker(qa)
        const m = await meeting(qa, 'live', 'InProgress')
        // The upload runs with the page's own session: the Chair's, like AC17 (a Member may not add files, MSG162).
        await qa.signIn('chair')
        await openVotesTab(qa, m)
        const fileName = `qa-fe11-doc51-${rand()}.pdf`
        const up = await uploadMeetingPdf(qa, m, fileName)
        if (up.status === 503) qa.inconclusive('Kho tệp local không chạy (503). Người kiểm: docker compose up -d (SeaweedFS) và FileStorage:* user-secrets, rồi chạy lại AC32.')
        qa.expect(up.status, JSON.stringify(up.body)).toBe(201)
        const docId = up.body.documents[0].id
        const v = await draftVote(qa, m, 'Đọc trang cuối')
        try {
          const chair = await client(qa, 'chair')
          qa.expectStatus(await chair.post(`${votePath(m, v)}/documents`, { meetingDocumentIds: [docId] }), 200)
          await openVote(qa, m, v)

          await qa.signIn('member')
          await openVotesTab(qa, m)
          const mrow = card(qa, v)
          await qa.expect(mrow.locator('.vote-docs li').filter({ hasText: fileName })).toContainText('Chưa đọc')
          await press(qa, mrow.getByRole('radio', { name: 'Đồng ý', exact: true }))
          await qa.expect(mrow.getByRole('button', { name: 'Gửi phiếu' })).toBeDisabled()
          await mrow.getByRole('link', { name: new RegExp(fileName.replace('.', '\\.')) }).click()
          // ADR-0066: the viewer is a layer over the tab (its link still carries ?read=1 for a new tab).
          await qa.expect(qa.page.getByRole('dialog', { name: 'Xem tài liệu' })).toBeVisible({ timeout: 20_000 })
          qa.expect(await mrow.getByRole('link', { name: new RegExp(fileName.replace('.', '\\.')) }).getAttribute('href')).toContain('read=1')
          // UC-42: locked until the reader reaches the end of the last page; the label says so (no tooltip on touch).
          const locked = qa.page.getByRole('button', { name: 'Đọc tới trang cuối' })
          await qa.expect(locked).toBeDisabled({ timeout: 20_000 })
          await qa.page.locator('.viewer-scroll .pdf-page').first().waitFor()
          await settled(qa)
          await qa.shot('chua-cuoi-khoa-xac-nhan')
          await qa.page.locator('.viewer-scroll').evaluate((el) => el.scrollTo(0, el.scrollHeight))
          const confirm = qa.page.getByRole('button', { name: 'Xác nhận đã đọc' })
          await qa.expect(confirm).toBeEnabled({ timeout: 20_000 })
          await settled(qa)
          await qa.shot('cuoi-trang-mo-xac-nhan')
          const answered = qa.page.waitForResponse((r) => r.url().includes('/read-confirmation') && r.request().method() === 'POST')
          await press(qa, confirm)
          const res = await answered
          qa.expect(res.status(), 'POST read-confirmation').toBe(200)
          const body = await res.json()
          qa.expect(body.code, JSON.stringify(body)).toBe('MSG253')
          await qa.expect(toast(qa, 'Đã xác nhận đọc tài liệu.')).toBeVisible()
          await qa.expect(qa.page.getByText('Đã xác nhận đọc', { exact: true })).toBeVisible()
          await settled(qa)
          await qa.shot('da-xac-nhan-doc')
          const viewer = qa.page.getByRole('dialog', { name: 'Xem tài liệu' })
          await viewer.getByRole('button', { name: 'Đóng', exact: true }).click()
          await qa.expect(viewer).toHaveCount(0)
          await noSidewaysScroll(qa)
          await noRawCodes(qa)
          const back = card(qa, v)
          await qa.expect(back.locator('.vote-docs li').filter({ hasText: fileName })).toContainText('Đã đọc')
          await back.getByRole('radio', { name: 'Đồng ý', exact: true }).check()
          await qa.expect(back.getByRole('button', { name: 'Gửi phiếu' })).toBeEnabled()
          await back.getByRole('button', { name: 'Gửi phiếu' }).click()
          await qa.expect(toast(qa, 'Đã ghi nhận phiếu của bạn.')).toBeVisible()
          await settled(qa)
          await qa.shot('da-doc-da-gui')
          c.done()
        } finally {
          await closeOpenVotes(qa, m)
        }
      },
    },
    {
      // R1/FE-11 (24/11/2026): AC mới — VOTE_FT15 (Khách bỏ phiếu kín qua OTP).
      id: 'AC33',
      role: 'anon',
      devices: ['desktop', 'phone'],
      title: 'Khách bỏ phiếu kín: hòm phiếu không tên người bỏ, bỏ xong báo "Bạn đã gửi phiếu.", lượt kín tăng một không gắn tên (VOTE_FT15)',
      steps: [
        'Thư ký mời Khách (email riêng cho lần chạy) vào cuộc họp đang diễn ra; Khách xác minh OTP, khai tên',
        'Thư ký tạo biểu quyết kín cho vai trò Khách; Chủ toạ mở',
        'Khách mở tab Biểu quyết, chọn phương án, gửi phiếu; Chủ toạ đọc lượt qua API',
      ],
      expected:
        'Hòm phiếu của Khách không hiện tên người bỏ; gửi xong Khách thấy "Bạn đã gửi phiếu."; lượt kín tăng một, phiếu không gắn tên (UC-65)',
      async run(qa) {
        const m = await meeting(qa, 'live', 'InProgress')
        const email = `qa-khach-vote15-${Date.now().toString(36)}-${qa.device}@example.test`
        const displayName = `Khách VOTE15 ${rand()}`
        const link = await guestInvite(qa, m.id, 'Khách Bỏ phiếu', email)
        const v = await draftVote(qa, m, 'Khach kin', { isAnonymous: true, eligibleRoles: ['Guest'] })
        try {
          await openVote(qa, m, v)
          await guestVerify(qa, link, email, displayName)
          await press(qa, qa.page.getByRole('tab', { name: 'Biểu quyết' }))
          const box = card(qa, v)
          await qa.expect(box).toBeVisible({ timeout: 15_000 })
          const names = `${(await users(qa)).chair.name}|${(await users(qa)).secretary.name}|${(await users(qa)).member.name}|${displayName}`
          qa.expect(await box.innerText(), 'voter names shown on the secret ballot box').not.toMatch(new RegExp(names))
          await press(qa, box.getByRole('radio', { name: 'Đồng ý', exact: true }))
          await qa.expect(box.getByRole('button', { name: 'Gửi phiếu' })).toBeEnabled()
          await box.getByRole('button', { name: 'Gửi phiếu' }).click()
          await qa.expect(box.getByText('Bạn đã gửi phiếu.')).toBeVisible({ timeout: 15_000 })
          await settled(qa)
          await qa.shot('khach da gui phieu kin')
          await noSidewaysScroll(qa)
          await noRawCodes(qa)
          const chair = await client(qa, 'chair')
          await closeVote(qa, m, v)
          const detail = (await chair.get(votePath(m, v))).body
          qa.expect(detail.result?.ballotCount, 'secret tally grew by one').toBe(1)
          qa.expect(detail.result?.voters, 'anonymous tally carries no voter names').toBeNull()
          const leaked = JSON.stringify(detail.result ?? {})
          qa.expect(leaked, `voter identity leaked into the tally: ${leaked.slice(0, 200)}`).not.toMatch(/voterName|displayName|email|userId/i)
          qa.note(`tally: ballotCount=${detail.result?.ballotCount}, voters=${detail.result?.voters}`)
        } finally {
          await closeOpenVotes(qa, m)
          await revokeGuest(qa, m.id, link)
        }
      },
    },
  ],
}

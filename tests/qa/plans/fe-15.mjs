// FE-15 — Báo cáo & Thống kê (QA subtask SCRUM-202). docs/uc-map.html maps FE-15 to UC-79, UC-79a View Reports &
// Statistics and UC-79b Export Report (story SCRUM-38). Criteria come from the UC specification
// (docs/01-requirements/05-use-case-specifications.md), BR-149, BR-150, UC-79a, BR-151, BR-152, BR-009–BR-011 (06-business-rules.md), the contract
// docs/03-api/22-bao-cao-thong-ke.md, NOTE-dac-ta-so-bo-scrum-38.md, MSG463–MSG466 in the catalogue, and the SCRUM-38
// comments (PO 09/11: Q1 admin sees only what it may read, Q2 duration added and no PDF, Q3 no capability).
//
// Other QA agents create meetings in parallel, so a number on a seed account's report is only ever compared with the API
// response the page itself received, or with the same API read just before. Exact figures are checked on an account of
// the plan's own, which no other agent knows about:
// - "[QA] FE-15 người xem báo cáo" (qa-fe15-reporter@trustcabinet.local, built-in role Thư ký) organizes every FE-15
//   meeting, all on one day 15 weeks from today (Vietnam time), with chutoa@ as Chair and approver, itself as Secretary
//   and thanhvien@ as Member: one meeting in each of the 8 statuses, mixed sensitivities, varied hours; the Ended one
//   has attendance taken (Chair and Secretary present, Member absent). Reused across runs, never cancelled.
// - AC5 creates one more meeting each run ("[QA] FE-15 đếm thêm …", same day) and cancels it in the same criterion: the
//   report counts Cancelled meetings too (BR-149, BR-150), so each run leaves one Cancelled meeting behind.
// - "[QA] FE-15 tài khoản tạm" (qa-fe15-inactive@trustcabinet.local, role Thành viên) is reactivated, signed in and
//   deactivated by AC12, and left deactivated.
// Every export (AC1, AC10, AC9) writes one audit row report.exported, as BR-009–BR-011 requires.
//
// The screen was reworked after the supervisor's review of 22/11/2026 (PR #488, docs/03-api/22-bao-cao-thong-ke.md): a
// band of manager figures from GET /api/reports/meetings on top, then a query over the main tables (GET /api/reports/query:
// Dữ liệu, Nhóm theo, Hiển thị and filter comboboxes, sortable columns, a Tổng row); Xuất Excel exports the table on screen.
// AC2, AC4, AC6, AC7, AC9, AC10 and AC11 check that screen; AC1, AC3, AC5, AC8 and AC12 still check the meeting report API.

import { readFileSync } from 'node:fs'
import { inflateRawSync } from 'node:zlib'

const PASSWORD = process.env.QA_PASSWORD ?? 'TrustCabinet@2026'
const EMAIL = {
  admin: 'quantri@trustcabinet.local',
  chair: 'chutoa@trustcabinet.local',
  secretary: 'thuky@trustcabinet.local',
  member: 'thanhvien@trustcabinet.local',
}
const REPORTER = 'qa-fe15-reporter@trustcabinet.local'
const INACTIVE = 'qa-fe15-inactive@trustcabinet.local'

// ---- months and days, in Vietnam's calendar as the server counts them (UC-79a) ----
const vnToday = () =>
  new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Ho_Chi_Minh', year: 'numeric', month: '2-digit', day: '2-digit' }).format(
    new Date(),
  )
const addDays = (day, n) => new Date(Date.UTC(+day.slice(0, 4), +day.slice(5, 7) - 1, +day.slice(8, 10) + n)).toISOString().slice(0, 10)
const mIndex = (month) => Number(month.slice(0, 4)) * 12 + Number(month.slice(5, 7)) - 1
const mFrom = (i) => `${Math.floor(i / 12)}-${String((i % 12) + 1).padStart(2, '0')}`
const addMonths = (month, n) => mFrom(mIndex(month) + n)
const monthLabel = (month) => `${month.slice(5, 7)}/${month.slice(0, 4)}`
const at = (day, hhmm) => `${day}T${hhmm}:00+07:00`
const vnMonthOf = (iso) =>
  new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Ho_Chi_Minh', year: 'numeric', month: '2-digit' }).format(new Date(iso)).slice(0, 7)

const TODAY = vnToday()
const NOW_MONTH = TODAY.slice(0, 7)
const DEFAULT = { from: addMonths(NOW_MONTH, -11), to: NOW_MONTH } // UC-79a: 12 months ending with this month
const FAR = addDays(TODAY, 105) // fifteen weeks ahead
const FAR_MONTH = FAR.slice(0, 7)
// AC6/AC7: three past months in which the seed accounts have meetings on this database (Sept–Oct 2026 hold data).
const P6 = { from: addMonths(NOW_MONTH, -3), to: addMonths(NOW_MONTH, -1) }
const EMPTY = { from: addMonths(FAR_MONTH, 2), to: addMonths(FAR_MONTH, 3) } // nothing of the reporter's there

const STATUSES = ['Draft', 'WaitingApproval', 'Rejected', 'Approved', 'Published', 'InProgress', 'Ended', 'Cancelled']
const STATUS_LABEL = {
  Draft: 'Nháp',
  WaitingApproval: 'Chờ duyệt',
  Rejected: 'Bị từ chối',
  Approved: 'Đã duyệt',
  Published: 'Đã công bố',
  InProgress: 'Đang diễn ra',
  Ended: 'Đã kết thúc',
  Cancelled: 'Đã huỷ',
}
const SENSITIVITIES = ['Normal', 'Internal', 'Secret']
const SENSITIVITY_LABEL = { Normal: 'Thường', Internal: 'Nội bộ', Secret: 'Mật' }

const MSG463 = 'Khoảng thời gian không hợp lệ. Chọn tối đa 24 tháng, tháng bắt đầu không sau tháng kết thúc.'
const MSG464 = 'Không tải được báo cáo. Vui lòng thử lại.'
const MSG465 = 'Không xuất được báo cáo. Vui lòng thử lại.'
const MSG466 = 'Không có cuộc họp nào bạn xem được trong khoảng này.'
const NO_DATA = 'Chưa có dữ liệu'

/** The reporter's meetings: one per status (BR-149, BR-150 counts every status, Cancelled included). */
const MEETINGS = [
  { status: 'Draft', title: 'FE-15 bản nháp', sensitivity: 'Normal', from: '07:30', to: '08:15' },
  { status: 'WaitingApproval', title: 'FE-15 chờ duyệt', sensitivity: 'Internal', from: '08:45', to: '09:30' },
  { status: 'Rejected', title: 'FE-15 bị từ chối', sensitivity: 'Secret', from: '10:00', to: '10:45' },
  { status: 'Approved', title: 'FE-15 đã duyệt', sensitivity: 'Normal', from: '11:15', to: '12:00' },
  { status: 'Published', title: 'FE-15 đã công bố', sensitivity: 'Internal', from: '13:30', to: '14:15' },
  { status: 'InProgress', title: 'FE-15 đang diễn ra', sensitivity: 'Normal', from: '14:45', to: '15:30' },
  { status: 'Ended', title: 'FE-15 đã kết thúc', sensitivity: 'Normal', from: '16:00', to: '17:00' },
  { status: 'Cancelled', title: 'FE-15 đã huỷ', sensitivity: 'Secret', from: '17:15', to: '18:00' },
]

// ---- formatting, as people read it (the screen: "87,3%", "1.011", "24,6") ----
const percent = new Intl.NumberFormat('vi-VN', { style: 'percent', maximumFractionDigits: 1 })
const decimal = new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 1 })
const fmtRate = (r) => percent.format(r)
const squash = (s) => String(s).replace(/[\s  ]+/g, ' ').trim()

// ---- API helpers ----
async function client(qa, who) {
  qa.__clients ??= {}
  qa.__clients[who] ??= await qa.api(who)
  return qa.__clients[who]
}
async function report(qa, who, from, to) {
  const q = from === undefined ? '' : `?from=${from}&to=${to}`
  const res = await (await client(qa, who)).get(`/api/reports/meetings${q}`)
  qa.expectStatus(res, 200)
  return res.body
}
const countOf = (rep, list, key, value) => rep[list].find((x) => x[key] === value)?.count

const people = {}
async function me(qa, who) {
  if (people[who]) return people[who]
  const res = await (await client(qa, who)).get('/api/auth/me')
  qa.expectStatus(res, 200)
  return (people[who] = res.body)
}

/** Finds or (re)activates one of the plan's own accounts. */
async function ensureAccount(qa, email, fullName, roleName, { active = true } = {}) {
  const admin = await client(qa, 'admin')
  const users = await admin.get('/api/users?pageSize=100&q=fe15')
  let list = users.status === 200 ? (Array.isArray(users.body) ? users.body : users.body.items) : []
  let user = list.find((u) => u.email === email)
  if (!user) {
    const all = await admin.get('/api/users?pageSize=100')
    list = Array.isArray(all.body) ? all.body : all.body.items
    user = list.find((u) => u.email === email)
  }
  if (!user) {
    const roles = (await admin.get('/api/roles')).body
    const roleId = roles.find((r) => r.name === roleName)?.id
    const created = await admin.post('/api/users', { fullName: qa.name(fullName), email, password: PASSWORD, roleId })
    qa.expectStatus(created, 201)
    user = created.body.user
    qa.note(`Đã tạo tài khoản ${email} (${roleName}).`)
  }
  if (active && !user.isActive) qa.expectStatus(await admin.put(`/api/users/${user.id}/status`, { isActive: true }), 200)
  return user
}

const made = {}
async function transition(qa, who, id, action) {
  const res = await (await client(qa, who)).post(`/api/meetings/${id}/transitions`, action)
  qa.expectStatus(res, 200)
}

/** Every meeting the reporter may read whose planned start is in `month`, read through the meeting list. */
async function reporterMeetings(qa, month) {
  const api = await client(qa, REPORTER)
  const from = `${month}-01T00:00:00+07:00`
  const to = `${addMonths(month, 1)}-01T00:00:00+07:00`
  const seen = new Map()
  for (const status of [null, ...STATUSES]) {
    const q = `/api/meetings?pageSize=100&from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}${status ? `&status=${status}` : ''}`
    const res = await api.get(q)
    qa.expectStatus(res, 200)
    qa.expect(res.body.totalCount, 'more than 100 reporter meetings in one month').toBeLessThanOrEqual(100)
    for (const m of res.body.items) if (vnMonthOf(m.plannedStartTime) === month) seen.set(m.id, m)
  }
  return [...seen.values()]
}

async function createMeeting(qa, spec) {
  await ensureAccount(qa, REPORTER, 'FE-15 người xem báo cáo', 'Thư ký')
  const rep = await me(qa, REPORTER)
  const chair = await me(qa, 'chair')
  const member = await me(qa, 'member')
  const created = await (await client(qa, REPORTER)).post('/api/meetings', {
    title: qa.name(spec.title),
    plannedStartTime: at(spec.day ?? FAR, spec.from),
    plannedEndTime: at(spec.day ?? FAR, spec.to),
    mode: 'InPerson',
    location: 'Phòng họp QA',
    onlineLink: null,
    sensitivity: spec.sensitivity,
    description: 'Dữ liệu kiểm thử FE-15 (báo cáo, thống kê).',
    participants: [
      { userId: chair.id, role: 'Chair' },
      { userId: rep.id, role: 'Secretary' },
      { userId: member.id, role: 'Member' },
    ],
  })
  qa.expectStatus(created, 201)
  return created.body.meeting
}

/** The reporter's meeting in `status`, created and driven there through the API when this database has none yet. */
async function meeting(qa, status) {
  if (made[status]) return made[status]
  const spec = MEETINGS.find((m) => m.status === status)
  const api = await client(qa, REPORTER)
  const found = await api.get(`/api/meetings?q=${encodeURIComponent(spec.title)}&pageSize=100`)
  qa.expectStatus(found, 200)
  const hit = found.body.items.find((m) => m.title === qa.name(spec.title) && m.status === status)
  if (hit) return (made[status] = hit)
  const m = await createMeeting(qa, spec)
  const chair = await me(qa, 'chair')
  const submit = [REPORTER, { action: 'SubmitForApproval', assignedApproverId: chair.id }]
  const path = {
    Draft: [],
    WaitingApproval: [submit],
    Rejected: [submit, ['chair', { action: 'Reject', reason: 'QA FE-15: cần bổ sung nội dung.' }]],
    Approved: [submit, ['chair', { action: 'Approve' }]],
    Published: [submit, ['chair', { action: 'Approve' }], [REPORTER, { action: 'Publish' }]],
    InProgress: [submit, ['chair', { action: 'Approve' }], [REPORTER, { action: 'Publish' }], ['chair', { action: 'Start' }]],
    Ended: [submit, ['chair', { action: 'Approve' }], [REPORTER, { action: 'Publish' }], ['chair', { action: 'Start' }]],
    Cancelled: [[REPORTER, { action: 'Cancel', reason: 'QA FE-15: huỷ để thử báo cáo.' }]],
  }[status]
  for (const [who, action] of path) await transition(qa, who, m.id, action)
  if (status === 'Ended') {
    // BR-151: Chair and Secretary present, Member absent; then the Chair ends the meeting.
    const att = await api.get(`/api/meetings/${m.id}/attendance?pageSize=100`)
    qa.expectStatus(att, 200)
    const entries = att.body.items.map((i) => ({
      id: i.id,
      kind: i.kind,
      attendance: i.role === 'Member' ? 'Absent' : 'Present',
      expectedVersion: i.rowVersion,
    }))
    qa.expectStatus(await api.post(`/api/meetings/${m.id}/attendance`, { entries }), 200)
    await transition(qa, 'chair', m.id, { action: 'End' })
  }
  const got = await api.get(`/api/meetings/${m.id}`)
  qa.expectStatus(got, 200)
  return (made[status] = got.body)
}
async function prepareAll(qa) {
  for (const s of STATUSES) await meeting(qa, s)
}
/** The month the plan's meetings are in: they are reused across runs, so it is the month of the day they were made. */
const planMonth = () => vnMonthOf(made.Ended.plannedStartTime)

/** What is wrong with a report on its own terms (UC-79a, BR-149, BR-150, BR-152 and the contract's shape). */
function problems(rep, from, to) {
  const wrong = []
  if (rep.from !== from || rep.to !== to) wrong.push(`period ${rep.from}..${rep.to}, asked ${from}..${to}`)
  const months = []
  for (let i = mIndex(from); i <= mIndex(to); i++) months.push(mFrom(i))
  if (JSON.stringify(rep.byMonth.map((m) => m.month)) !== JSON.stringify(months)) wrong.push(`byMonth months ${rep.byMonth.map((m) => m.month)}`)
  if (JSON.stringify(rep.byStatus.map((s) => s.status)) !== JSON.stringify(STATUSES)) wrong.push(`byStatus order ${rep.byStatus.map((s) => s.status)}`)
  if (JSON.stringify(rep.bySensitivity.map((s) => s.sensitivity)) !== JSON.stringify(SENSITIVITIES))
    wrong.push(`bySensitivity order ${rep.bySensitivity.map((s) => s.sensitivity)}`)
  const sum = (xs) => xs.reduce((a, x) => a + x.count, 0)
  for (const [name, xs] of [['byMonth', rep.byMonth], ['byStatus', rep.byStatus], ['bySensitivity', rep.bySensitivity]])
    if (sum(xs) !== rep.total) wrong.push(`sum(${name}) = ${sum(xs)} ≠ total ${rep.total}`)
  const ended = countOf(rep, 'byStatus', 'status', 'Ended')
  const a = rep.attendance
  const d = rep.duration
  const t = rep.minutesTurnaround
  if (a.meetings + a.meetingsWithoutAttendance !== ended) wrong.push(`attendance ${a.meetings}+${a.meetingsWithoutAttendance} ≠ Ended ${ended}`)
  if (d.meetings + d.meetingsWithoutTimes !== ended) wrong.push(`duration ${d.meetings}+${d.meetingsWithoutTimes} ≠ Ended ${ended}`)
  if (t.meetings + t.meetingsWithoutIssuedMinutes !== ended) wrong.push(`turnaround ${t.meetings}+${t.meetingsWithoutIssuedMinutes} ≠ Ended ${ended}`)
  if ((a.averageRate === null) !== (a.meetings === 0)) wrong.push(`averageRate ${a.averageRate} with ${a.meetings} meetings`)
  if ((d.averageMinutes === null) !== (d.meetings === 0)) wrong.push(`averageMinutes ${d.averageMinutes} with ${d.meetings} meetings`)
  if ((t.averageHours === null) !== (t.meetings === 0)) wrong.push(`averageHours ${t.averageHours} with ${t.meetings} meetings`)
  if (a.averageRate !== null && (a.averageRate < 0 || a.averageRate > 1)) wrong.push(`averageRate ${a.averageRate} out of 0..1`)
  if (!/\+07:00$/.test(rep.generatedAt)) wrong.push(`generatedAt ${rep.generatedAt} is not in Vietnam time`)
  // BR-152: counts, rates and averages only.
  const keys = new Set()
  const walk = (v) => {
    if (Array.isArray(v)) v.forEach(walk)
    else if (v && typeof v === 'object') for (const [k, x] of Object.entries(v)) (keys.add(k), walk(x))
  }
  walk(rep)
  const leaking = [...keys].filter((k) => /(^id$|Id$|title|name|participant|location|description|email)/i.test(k))
  if (leaking.length) wrong.push(`fields that describe a meeting: ${leaking}`)
  return wrong
}

// ---- screen helpers ----
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
async function press(qa, locator) {
  if (qa.device === 'phone') await locator.tap()
  else await locator.click()
}

/**
 * Records every GET the page makes to `path` (the meeting report by default, or the report query), with its query string
 * and body. Exports are other paths and are not recorded.
 */
function watchReports(qa, path = '/api/reports/meetings') {
  const seen = []
  qa.page.on('response', async (r) => {
    const u = new URL(r.url())
    if (u.pathname !== path || r.request().method() !== 'GET') return
    let body
    try {
      body = await r.json()
    } catch {
      body = null
    }
    const params = Object.fromEntries(u.searchParams)
    seen.push({ from: params.from, to: params.to, params, status: r.status(), body })
  })
  return seen
}
const watchTables = (qa) => watchReports(qa, '/api/reports/query')
async function lastReport(qa, seen, pred = () => true, timeout = 15_000) {
  const end = Date.now() + timeout
  while (Date.now() < end) {
    const hit = [...seen].reverse().find((x) => x.status === 200 && pred(x))
    if (hit) return hit.body
    await qa.page.waitForTimeout(100)
  }
  throw new Error('the page never received the report it should have asked for')
}

const region = (qa, name) => qa.page.getByRole('region', { name })
const band = (qa) => region(qa, 'Số liệu chính')
const queryCard = (qa) => region(qa, 'Truy vấn báo cáo')
const exportButton = (qa) => qa.page.getByRole('button', { name: /Xuất Excel|Đang xuất/ })

/** Waits for the band and the query table (or its empty line) to be on screen and not refreshing. */
async function figuresReady(qa) {
  await qa.expect(band(qa).locator('.report-figure dt')).toHaveCount(7, { timeout: 15_000 })
  await qa.expect(queryCard(qa).locator('.report-result, .report-query-empty').first()).toBeVisible({ timeout: 15_000 })
  await qa.page.waitForFunction(() => !document.querySelector('[aria-busy="true"], .data-table-stale'))
}

/** Picks `option` in the report combobox labelled `label` ("Dữ liệu", "Nhóm theo", "Hiển thị", a filter). */
async function pick(qa, label, option) {
  await press(qa, queryCard(qa).getByRole('button', { name: new RegExp(`^${label}:`) }))
  await press(qa, qa.page.getByRole('listbox', { name: label }).getByRole('option', { name: option, exact: true }))
}

// The figures as the screen writes them: 1.011, 74,3%, 24,6; nothing to show is "Chưa có".
const integer = new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 0 })
const fmtFigure = (v, format) =>
  v === null || v === undefined
    ? 'Chưa có'
    : format === 'percent'
      ? fmtRate(v)
      : format === 'hours' || format === 'megabytes'
        ? decimal.format(v)
        : integer.format(v)

/** The band holds the manager figures of the report the page received, in order (supervisor review 22/11/2026). */
async function expectBandMatches(qa, rep) {
  const count = (s) => countOf(rep, 'byStatus', 'status', s)
  const want = [
    ['Đã họp', integer.format(count('Ended'))],
    ['Sắp họp', integer.format(count('Approved') + count('Published'))],
    ['Đã huỷ', integer.format(count('Cancelled'))],
    ['Tỷ lệ có mặt', rep.attendance.averageRate === null ? 'Chưa có' : fmtRate(rep.attendance.averageRate)],
    ['Thông qua', `${integer.format(rep.votes.passed)} / ${integer.format(rep.votes.closed)} biểu quyết`],
    [
      'Biên bản ban hành',
      `${integer.format(rep.minutes.issued)}${rep.minutes.waitingApproval > 0 ? ` ${rep.minutes.waitingApproval} chờ duyệt` : ''}`,
    ],
    ['Giờ ghi âm', `${decimal.format(rep.recordings.recordedHours)} ${decimal.format(rep.recordings.transcribedHours)} gỡ băng`],
  ]
  const shown = await band(qa)
    .locator('.report-figure')
    .evaluateAll((els) => els.map((el) => [el.querySelector('dt')?.innerText, el.querySelector('dd')?.innerText]))
  const got = shown.map(([l, v]) => [squash(l), squash(v)])
  const wantSquashed = want.map(([l, v]) => [l, squash(v)])
  qa.expect(got, `band ${JSON.stringify(got)} ≠ ${JSON.stringify(wantSquashed)}`).toEqual(wantSquashed)
}

/** Columns the table shows: grouped by meeting, the meeting table drops the three that are 1 or 0 on every row. */
const shownColumns = (table) =>
  table.entity === 'meetings' && table.groupBy === 'meeting'
    ? table.columns.filter((c) => !['count', 'ended', 'cancelled'].includes(c.key))
    : table.columns

/** The result table shows exactly the table the page received: headers, ranked rows, the Tổng row. */
async function expectTableMatches(qa, table) {
  const wrong = []
  const columns = shownColumns(table)
  const result = queryCard(qa).locator('.report-result')
  if (qa.device !== 'phone') {
    // Headings are written in capitals by the stylesheet; compare the words.
    const heads = (await result.locator('thead th').allInnerTexts()).map((t) => squash(t).toLocaleLowerCase('vi'))
    const want = ['#', table.groupLabel, ...columns.map((c) => c.label)].map((t) => t.toLocaleLowerCase('vi'))
    if (JSON.stringify(heads.slice(0, want.length)) !== JSON.stringify(want)) wrong.push(`headers ${JSON.stringify(heads)} ≠ ${JSON.stringify(want)}`)
  }
  const rows = await result.locator('tbody tr').evaluateAll((trs) => trs.map((tr) => [...tr.cells].map((c) => c.innerText)))
  const want = table.rows.map((r) => [String(r.rank), r.label, ...columns.map((c) => fmtFigure(r.values[c.key], c.format))])
  const got = rows.map((r) => r.slice(0, 2 + columns.length).map(squash))
  if (JSON.stringify(got) !== JSON.stringify(want.map((r) => r.map(squash)))) wrong.push(`rows ${JSON.stringify(got)} ≠ ${JSON.stringify(want)}`)
  const total = (await result.locator('tfoot tr').evaluate((tr) => [...tr.cells].map((c) => c.innerText))).map(squash)
  const wantTotal = ['', 'Tổng', ...columns.map((c) => squash(fmtFigure(table.total[c.key], c.format)))]
  if (JSON.stringify(total.slice(0, wantTotal.length)) !== JSON.stringify(wantTotal)) wrong.push(`Tổng ${JSON.stringify(total)} ≠ ${JSON.stringify(wantTotal)}`)
  const cut = table.top !== null && table.groups > table.rows.length
  const note = queryCard(qa).locator('.report-cut')
  if (cut && squash(await note.innerText()) !== `${table.rows.length} / ${table.groups} nhóm`) wrong.push(`cut line "${await note.innerText()}"`)
  if (!cut && (await note.count())) wrong.push('a cut line shows while every group is listed')
  qa.expect(wrong, wrong.join('\n')).toEqual([])
}

/** The screen shows exactly what the page received (UC-79a step 4): the band, then the query table. */
async function expectScreenMatches(qa, rep, table) {
  await expectBandMatches(qa, rep)
  if (table) await expectTableMatches(qa, table)
}

/** A table on its own terms: ranks 1..n, at most `top` rows, `groups` ≥ rows, sorted as asked, nothing out of range. */
function tableProblems(table, params) {
  const wrong = []
  for (const [k, v] of Object.entries({ entity: params.entity, groupBy: params.groupBy, sort: params.sort }))
    if (v && table[k] !== v) wrong.push(`${k} ${table[k]}, asked ${v}`)
  if (table.rows.some((r, i) => r.rank !== i + 1)) wrong.push(`ranks ${table.rows.map((r) => r.rank)}`)
  if (table.top !== null && table.rows.length > table.top) wrong.push(`${table.rows.length} rows over top ${table.top}`)
  if (table.groups < table.rows.length) wrong.push(`groups ${table.groups} < rows ${table.rows.length}`)
  if (table.sort !== 'group') {
    const vals = table.rows.map((r) => r.values[table.sort] ?? -Infinity)
    if (vals.some((v, i) => i > 0 && v > vals[i - 1])) wrong.push(`not sorted by ${table.sort}: ${vals}`)
  }
  for (const c of table.columns)
    if (c.format === 'percent')
      for (const r of [...table.rows, { label: 'Tổng', values: table.total }])
        if (r.values[c.key] !== null && (r.values[c.key] < 0 || r.values[c.key] > 1)) wrong.push(`${r.label} ${c.key} ${r.values[c.key]}`)
  if (!/\+07:00$/.test(table.generatedAt)) wrong.push(`generatedAt ${table.generatedAt} is not in Vietnam time`)
  return wrong
}

/** A minimal ZIP reader for the exported .xlsx (stored or deflated entries). */
function unzip(buf) {
  const eocd = buf.lastIndexOf(Buffer.from([0x50, 0x4b, 0x05, 0x06]))
  if (eocd < 0) throw new Error('the file is not a ZIP (xlsx) archive')
  const n = buf.readUInt16LE(eocd + 10)
  let off = buf.readUInt32LE(eocd + 16)
  const files = new Map()
  for (let i = 0; i < n; i++) {
    const method = buf.readUInt16LE(off + 10)
    const csize = buf.readUInt32LE(off + 20)
    const nlen = buf.readUInt16LE(off + 28)
    const elen = buf.readUInt16LE(off + 30)
    const clen = buf.readUInt16LE(off + 32)
    const lho = buf.readUInt32LE(off + 42)
    const name = buf.toString('utf8', off + 46, off + 46 + nlen)
    const start = lho + 30 + buf.readUInt16LE(lho + 26) + buf.readUInt16LE(lho + 28)
    const data = buf.subarray(start, start + csize)
    files.set(name, method === 8 ? inflateRawSync(data) : Buffer.from(data))
    off += 46 + nlen + elen + clen
  }
  return files
}
const unxml = (s) => s.replace(/&lt;/g, '<').replace(/&gt;/g, '>').replace(/&quot;/g, '"').replace(/&apos;/g, "'").replace(/&amp;/g, '&')

/** The first sheet as rows of { A: {text|num}, B: … }, plus the sheet's name and its raw XML. */
function readXlsx(buf) {
  const files = unzip(buf)
  const workbook = files.get('xl/workbook.xml')?.toString('utf8') ?? ''
  const sheetName = unxml(/<sheet [^>]*name="([^"]+)"/.exec(workbook)?.[1] ?? '')
  const sheetFile = [...files.keys()].find((k) => /^xl\/worksheets\/sheet\d+\.xml$/.test(k))
  const xml = files.get(sheetFile).toString('utf8')
  const rows = new Map()
  for (const m of xml.matchAll(/<c r="([A-Z]+)(\d+)"([^>]*?)(?:\/>|>(.*?)<\/c>)/g)) {
    const [, col, row, attrs, inner = ''] = m
    const isText = /t="(inlineStr|s|str)"/.test(attrs)
    const value = isText ? unxml(/<t[^>]*>(.*?)<\/t>/.exec(inner)?.[1] ?? '') : /<v>(.*?)<\/v>/.exec(inner)?.[1]
    if (!rows.has(+row)) rows.set(+row, {})
    rows.get(+row)[col] = isText ? { text: value } : { num: value === undefined ? undefined : Number(value) }
  }
  return { sheetName, rows: [...rows.entries()].sort((a, b) => a[0] - b[0]).map(([, r]) => r), xml, files }
}

/**
 * The exported file holds exactly the table on screen (BR-009–BR-011): what was asked at the top, then one row per group with
 * its rank, then Tổng. A meeting title may appear only when the table is grouped by meeting.
 */
function tableFileProblems(file, table, titles = []) {
  const wrong = []
  const columns = table.columns
  if (file.sheetName !== table.entityLabel) wrong.push(`sheet "${file.sheetName}", expected "${table.entityLabel}"`)
  const byLabel = new Map(file.rows.filter((r) => r.A?.text && r.B).map((r) => [r.A.text, r]))
  if (byLabel.get('Khoảng thời gian')?.B?.text !== `${monthLabel(table.from)} – ${monthLabel(table.to)}`)
    wrong.push(`period cell "${byLabel.get('Khoảng thời gian')?.B?.text}"`)
  if (byLabel.get('Nhóm theo')?.B?.text !== table.groupLabel) wrong.push(`grouping cell "${byLabel.get('Nhóm theo')?.B?.text}"`)
  const filters = table.filters.length ? table.filters.map((f) => `${f.label}: ${f.valueLabel}`).join('; ') : 'Không'
  if (byLabel.get('Bộ lọc')?.B?.text !== filters) wrong.push(`filter cell "${byLabel.get('Bộ lọc')?.B?.text}", expected "${filters}"`)
  if (!/\(GMT\+7\)$/.test(byLabel.get('Thời điểm xuất')?.B?.text ?? '')) wrong.push(`export time "${byLabel.get('Thời điểm xuất')?.B?.text}"`)
  const letters = 'CDEFGH'
  const headAt = file.rows.findIndex((r) => r.A?.text === '#')
  if (headAt < 0) return [...wrong, 'no table heading row "#"']
  const head = file.rows[headAt]
  const wantHead = [table.groupLabel, ...columns.map((c) => c.label)]
  const gotHead = [head.B?.text, ...columns.map((_, i) => head[letters[i]]?.text)]
  if (JSON.stringify(gotHead) !== JSON.stringify(wantHead)) wrong.push(`heading ${JSON.stringify(gotHead)} ≠ ${JSON.stringify(wantHead)}`)
  const cell = (r, i) => r[letters[i]]
  const same = (c, v) => (v === null ? c?.text === NO_DATA : Math.abs((c?.num ?? NaN) - v) < 1e-6)
  const body = file.rows.slice(headAt + 1)
  table.rows.forEach((row, n) => {
    const r = body[n] ?? {}
    if (r.A?.num !== row.rank || r.B?.text !== row.label) wrong.push(`row ${n + 1} "${r.A?.num} ${r.B?.text}" ≠ "${row.rank} ${row.label}"`)
    columns.forEach((c, i) => {
      const v = c.format === 'count' && row.values[c.key] !== null ? Math.round(row.values[c.key]) : row.values[c.key]
      if (!same(cell(r, i), v)) wrong.push(`row "${row.label}" ${c.key} ${JSON.stringify(cell(r, i))}, API ${v}`)
    })
  })
  const total = body[table.rows.length] ?? {}
  if (total.B?.text !== 'Tổng') wrong.push(`no Tổng row after the groups (got ${JSON.stringify(total.B)})`)
  else
    columns.forEach((c, i) => {
      const v = c.format === 'count' && table.total[c.key] !== null ? Math.round(table.total[c.key]) : table.total[c.key]
      if (!same(cell(total, i), v)) wrong.push(`Tổng ${c.key} ${JSON.stringify(cell(total, i))}, API ${v}`)
    })
  if (table.groupBy !== 'meeting') for (const t of titles) if (file.xml.includes(t)) wrong.push(`meeting title "${t}" is in the file`)
  return wrong
}

async function downloadExport(qa) {
  const [dl] = await Promise.all([qa.page.waitForEvent('download', { timeout: 20_000 }), press(qa, exportButton(qa))])
  const path = await dl.path()
  return { name: dl.suggestedFilename(), buf: readFileSync(path) }
}

/**
 * The audit row of the export just made by `who`, read by the admin (BR-009–BR-011): exactly one since `since`, with `want` in its
 * details (which table, grouping and filters).
 */
async function expectAuditRow(qa, who, from, to, since, want = {}) {
  const user = await me(qa, who)
  const res = await (await client(qa, 'admin')).get(`/api/audit-log?action=report.exported&actor=${user.id}&pageSize=10`)
  qa.expectStatus(res, 200)
  const details = (r) => (typeof r.details === 'string' ? JSON.parse(r.details) : (r.details ?? {}))
  const rows = res.body.items.filter(
    (r) => new Date(r.occurredAt).getTime() >= since - 5_000 && details(r).from === from && details(r).to === to,
  )
  qa.expect(rows.length, `report.exported audit rows for ${who} ${from}..${to} since the export`).toBe(1)
  const [row] = rows
  qa.expect(row.objectType).toBe('Report')
  qa.expect(row.objectId ?? null).toBeNull()
  qa.expect(details(row)).toMatchObject(want)
  qa.note(`Audit: ${row.actionLabel ?? row.action} · ${row.actor?.name} · ${JSON.stringify(row.details)} · IP ${row.ipAddress}`)
}

// ---------------------------------------------------------------------------------------------------------------------

export default {
  ticket: 'SCRUM-202',
  title: 'FE-15 Báo cáo & Thống kê (UC-79a, UC-79b — SCRUM-38)',
  devices: ['desktop'],
  criteria: [
    {
      id: 'AC0',
      role: 'admin',
      title: 'Chuẩn bị dữ liệu [QA] FE-15 (qua API)',
      steps: [
        `Tìm hoặc tạo tài khoản ${REPORTER} (vai trò Thư ký)`,
        `Tài khoản đó tổ chức 8 cuộc họp [QA] FE-15 ngày ${FAR}, mỗi cuộc một trạng thái; cuộc Đã kết thúc có điểm danh 2 có mặt, 1 vắng`,
      ],
      expected: 'Đủ 8 cuộc họp đúng trạng thái, chutoa@ là Chủ toạ và người duyệt, thanhvien@ là Thành viên',
      async run(qa) {
        await ensureAccount(qa, REPORTER, 'FE-15 người xem báo cáo', 'Thư ký')
        await prepareAll(qa)
        for (const s of STATUSES) qa.note(`${s}: ${made[s].title} · ${made[s].sensitivity} · ${made[s].id}`)
      },
    },

    // ------------------------------------------------------------------------- who may see it (BR-149, BR-150)
    {
      id: 'AC1',
      role: 'anon',
      title: 'Quyền: mọi tài khoản nội bộ (kể cả Thành viên không có quyền nào) xem và xuất được; chưa đăng nhập bị 401 và màn chuyển về đăng nhập',
      steps: [
        'Gọi GET /api/reports/meetings và /export (khoảng mặc định) bằng quantri@, chutoa@, thuky@, thanhvien@ và không token',
        'Chưa đăng nhập, mở /mgmt/reports',
      ],
      expected:
        'Bốn tài khoản: 200, Cache-Control "private, no-store"; tệp có content type xlsx và tên bao-cao-cuoc-hop_<from>_<to>.xlsx. Không token: 401 cả hai. Màn: về /login, không hiện số liệu',
      async run(qa) {
        const member = await qa.capabilities('member')
        qa.note(`thanhvien@ (${member.roleName}) có capability: [${member.capabilities.join(', ')}]`)
        const wrong = []
        for (const who of ['admin', 'chair', 'secretary', 'member']) {
          const api = await client(qa, who)
          const r = await api.get('/api/reports/meetings')
          if (r.status !== 200) wrong.push(`${who} report → ${r.status}`)
          if (r.headers['cache-control'] !== 'private, no-store') wrong.push(`${who} report Cache-Control "${r.headers['cache-control']}"`)
          if (r.status === 200 && (r.body.from !== DEFAULT.from || r.body.to !== DEFAULT.to))
            wrong.push(`${who} default period ${r.body.from}..${r.body.to}, expected ${DEFAULT.from}..${DEFAULT.to} (UC-79a)`)
          const x = await api.get(`/api/reports/meetings/export?from=${DEFAULT.from}&to=${DEFAULT.to}`)
          if (x.status !== 200) wrong.push(`${who} export → ${x.status}`)
          if (x.headers['content-type'] !== 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet')
            wrong.push(`${who} export content-type "${x.headers['content-type']}"`)
          if (!String(x.headers['content-disposition']).includes(`bao-cao-cuoc-hop_${DEFAULT.from}_${DEFAULT.to}.xlsx`))
            wrong.push(`${who} export file name: ${x.headers['content-disposition']}`)
          if (x.headers['cache-control'] !== 'private, no-store') wrong.push(`${who} export Cache-Control "${x.headers['cache-control']}"`)
        }
        const anon = await client(qa, 'anon')
        for (const path of ['/api/reports/meetings', `/api/reports/meetings/export?from=${DEFAULT.from}&to=${DEFAULT.to}`]) {
          const r = await anon.get(path)
          if (r.status !== 401) wrong.push(`anon ${path} → ${r.status}`)
        }
        await qa.goto('/mgmt/reports')
        await qa.expect(qa.page).toHaveURL(/\/login/)
        await qa.expect(band(qa)).toHaveCount(0)
        await settled(qa)
        await qa.shot('chua-dang-nhap-ve-dang-nhap')
        qa.expect(wrong, wrong.join('\n')).toEqual([])
      },
    },
    {
      id: 'AC12',
      role: 'admin',
      title: 'Tài khoản đã bị vô hiệu hoá (token còn hạn) bị 403 ở cả xem và xuất',
      steps: [`Mở lại ${INACTIVE}, đăng nhập lấy token`, 'Quản trị vô hiệu hoá tài khoản', 'Gọi hai endpoint bằng token cũ'],
      expected: '403 không trả số liệu, không trả tệp (contract §Quyền: như mọi endpoint RequireActiveUser)',
      async run(qa) {
        const user = await ensureAccount(qa, INACTIVE, 'FE-15 tài khoản tạm', 'Thành viên')
        const api = await qa.api(INACTIVE)
        const before = await api.get('/api/reports/meetings')
        qa.expectStatus(before, 200)
        qa.expectStatus(await (await client(qa, 'admin')).put(`/api/users/${user.id}/status`, { isActive: false }), 200)
        const r = await api.get('/api/reports/meetings')
        const x = await api.get(`/api/reports/meetings/export?from=${DEFAULT.from}&to=${DEFAULT.to}`)
        qa.expect(r.status, `report: ${JSON.stringify(r.body).slice(0, 200)}`).toBe(403)
        qa.expect(x.status, `export: ${String(JSON.stringify(x.body)).slice(0, 200)}`).toBe(403)
        qa.expect(JSON.stringify(r.body ?? '')).not.toContain('byStatus')
        qa.expect(x.headers['content-type'] ?? '').not.toContain('spreadsheetml')
        qa.note(`${INACTIVE} để ở trạng thái vô hiệu hoá.`)
      },
    },

    // ------------------------------------------------------------------------- the page (UC-79a Normal Flow)
    {
      id: 'AC2',
      role: 'member',
      devices: ['desktop', 'phone'],
      title: 'Thành viên mở Báo cáo từ sidebar: dải số chính trên đầu, ô Dữ liệu/Nhóm theo/Hiển thị mặc định, bảng theo tháng khớp API, dòng Tổng; đổi Nhóm theo và bấm cột để xếp',
      steps: [
        'Đăng nhập thanhvien@, mở danh sách cuộc họp',
        'Bấm "Báo cáo" trên sidebar (điện thoại: mở menu trước)',
        'Chọn Nhóm theo = Trạng thái cuộc họp',
        'Máy tính: bấm tiêu đề cột "Đã họp", rồi tiêu đề cột nhóm',
      ],
      expected: `URL /mgmt/reports; hai ô "Từ tháng ${monthLabel(DEFAULT.from)}" "Đến tháng ${monthLabel(DEFAULT.to)}"; trang gọi /api/reports/meetings và /api/reports/query from=${DEFAULT.from}&to=${DEFAULT.to}; dải "Số liệu chính" 7 số theo thứ tự Đã họp, Sắp họp, Đã huỷ, Tỷ lệ có mặt, Thông qua (/ N biểu quyết), Biên bản ban hành, Giờ ghi âm (… gỡ băng) — đúng số API; ô "Dữ liệu: Cuộc họp", "Nhóm theo: Tháng", "Hiển thị: Tất cả"; bảng 12 tháng, cột #, Tháng, Số cuộc họp, Đã họp, Đã huỷ, Giờ họp, Tỷ lệ có mặt, dòng Tổng — đúng số API, Tổng số cuộc họp = total của báo cáo; Nhóm theo trạng thái → URL groupBy=status, bảng khớp API; bấm "Đã họp" → sort=ended, lớn trước; bấm cột nhóm → về thứ tự trạng thái; Xuất Excel bấm được; không cuộn ngang, không mã thô`,
      async run(qa) {
        const seen = watchReports(qa)
        const tables = watchTables(qa)
        await qa.goto('/mgmt/meetings')
        if (qa.device === 'phone') {
          const menu = qa.page.getByRole('button', { name: /menu|Mở thanh bên|Điều hướng/i }).first()
          if (await menu.isVisible().catch(() => false)) await press(qa, menu)
        }
        const link = qa.page.getByRole('link', { name: 'Báo cáo' })
        await qa.expect(link).toBeVisible()
        await press(qa, link)
        await qa.expect(qa.page).toHaveURL(/\/mgmt\/reports/)
        await qa.expect(qa.page.getByRole('heading', { name: 'Báo cáo', level: 1 })).toBeVisible()
        const rep = await lastReport(qa, seen)
        const table = await lastReport(qa, tables)
        qa.expect(seen[0], 'the first request is not the default period').toMatchObject({ from: DEFAULT.from, to: DEFAULT.to })
        qa.expect(tables[0].params).toMatchObject({ entity: 'meetings', from: DEFAULT.from, to: DEFAULT.to, groupBy: 'month', sort: 'group' })
        await figuresReady(qa)
        await qa.expect(qa.page.getByRole('button', { name: `Từ tháng: Tháng ${monthLabel(DEFAULT.from)}` })).toBeVisible()
        await qa.expect(qa.page.getByRole('button', { name: `Đến tháng: Tháng ${monthLabel(DEFAULT.to)}` })).toBeVisible()
        for (const name of ['Dữ liệu: Cuộc họp', 'Nhóm theo: Tháng', 'Hiển thị: Tất cả', 'Trạng thái cuộc họp: Mọi trạng thái'])
          await qa.expect(queryCard(qa).getByRole('button', { name })).toBeVisible()
        qa.expect(problems(rep, DEFAULT.from, DEFAULT.to)).toEqual([])
        qa.expect(tableProblems(table, tables[0].params)).toEqual([])
        qa.expect(table.rows.map((r) => r.key), 'the month table does not list every month').toEqual(rep.byMonth.map((m) => m.month))
        qa.expect(table.total.count, 'Tổng số cuộc họp ≠ the report total').toBe(rep.total)
        await expectScreenMatches(qa, rep, table)
        await qa.expect(qa.page.getByText(/^Số liệu lúc /)).toBeVisible()
        await qa.expect(exportButton(qa)).toBeEnabled()
        await settled(qa)
        await qa.shot('bao-cao-mac-dinh', { fullPage: true })
        await noSidewaysScroll(qa)
        await noRawCodes(qa)

        await pick(qa, 'Nhóm theo', 'Trạng thái cuộc họp')
        await qa.expect(qa.page).toHaveURL(/groupBy=status/)
        const byStatus = await lastReport(qa, tables, (x) => x.params.groupBy === 'status')
        await figuresReady(qa)
        qa.expect(tableProblems(byStatus, { entity: 'meetings', groupBy: 'status', sort: 'count' })).toEqual([])
        for (const r of byStatus.rows) qa.expect(r.values.count).toBe(countOf(rep, 'byStatus', 'status', r.key))
        await expectTableMatches(qa, byStatus)
        await settled(qa)
        await qa.shot('nhom-theo-trang-thai', { fullPage: true })
        if (qa.device === 'desktop') {
          // Sorting lives in the column headings (hidden on a phone, where each row is a block).
          await press(qa, queryCard(qa).getByRole('button', { name: /^Đã họp/ }))
          await qa.expect(qa.page).toHaveURL(/sort=ended/)
          const byHeld = await lastReport(qa, tables, (x) => x.params.groupBy === 'status' && x.params.sort === 'ended')
          await figuresReady(qa)
          qa.expect(tableProblems(byHeld, { groupBy: 'status', sort: 'ended' })).toEqual([])
          await qa.expect(queryCard(qa).locator('th[aria-sort="descending"]')).toContainText(/Đã họp/i)
          await expectTableMatches(qa, byHeld)
          await press(qa, queryCard(qa).getByRole('button', { name: /^Trạng thái cuộc họp$/ }))
          const natural = await lastReport(qa, tables, (x) => x.params.groupBy === 'status' && x.params.sort === 'group')
          await figuresReady(qa)
          const order = natural.rows.map((r) => STATUSES.indexOf(r.key))
          qa.expect(order, 'grouped in their own order').toEqual([...order].sort((a, b) => a - b))
          await expectTableMatches(qa, natural)
        }
      },
    },
    {
      id: 'AC3',
      role: 'admin',
      title: 'API tự khớp với mọi vai trò và nhiều khoảng: tổng theo tháng = theo trạng thái = theo mức độ = total, đủ tháng, 8 trạng thái đúng thứ tự enum, không trường nào mô tả cuộc họp',
      steps: [`Gọi GET /api/reports/meetings bằng 4 tài khoản seed với khoảng mặc định, ${DEFAULT.to}..${DEFAULT.to}, ${addMonths(NOW_MONTH, -23)}..${NOW_MONTH} (24 tháng) và ${NOW_MONTH}..${FAR_MONTH}`],
      expected:
        'Mỗi lần: byMonth đủ mọi tháng kể cả tháng 0; byStatus Draft, WaitingApproval, Rejected, Approved, Published, InProgress, Ended, Cancelled; bySensitivity Normal, Internal, Secret; ba tổng bằng total; cơ sở của 3 chỉ số trung bình cộng lại bằng số Đã kết thúc; trung bình null đúng khi không có cuộc họp để tính; generatedAt +07:00; không id/tiêu đề (BR-152)',
      async run(qa) {
        const wrong = []
        const periods = [
          [undefined, undefined, DEFAULT.from, DEFAULT.to],
          [NOW_MONTH, NOW_MONTH, NOW_MONTH, NOW_MONTH],
          [addMonths(NOW_MONTH, -23), NOW_MONTH, addMonths(NOW_MONTH, -23), NOW_MONTH],
          [NOW_MONTH, FAR_MONTH, NOW_MONTH, FAR_MONTH],
        ]
        for (const who of ['admin', 'chair', 'secretary', 'member'])
          for (const [f, t, ef, et] of periods) {
            const rep = await report(qa, who, f, t)
            for (const p of problems(rep, ef, et)) wrong.push(`${who} ${ef}..${et}: ${p}`)
          }
        qa.expect(wrong, wrong.join('\n')).toEqual([])
      },
    },
    {
      id: 'AC4',
      role: REPORTER,
      devices: ['desktop', 'phone'],
      title: `Số liệu đúng trên dữ liệu của riêng kế hoạch (tháng của các cuộc họp kế hoạch): dải số và bảng theo trạng thái, người dự họp theo điểm danh, từng cuộc họp — khớp danh sách cuộc họp; tỷ lệ có mặt và giờ họp đúng`,
      steps: [
        `Tài khoản ${REPORTER} có 8 cuộc họp [QA] FE-15 ngày ${FAR} (một mỗi trạng thái) và các cuộc "đếm thêm" đã huỷ của AC5`,
        'Mở /mgmt/reports trong tháng đó, Nhóm theo = Trạng thái cuộc họp (?groupBy=status)',
        'Chọn Dữ liệu = Người dự họp, Nhóm theo = Điểm danh',
        'Chọn Dữ liệu = Cuộc họp, Nhóm theo = Từng cuộc họp, Hiển thị = Tất cả',
      ],
      expected: `Bảng theo trạng thái: mỗi trạng thái bằng số đếm từ danh sách cuộc họp (cùng luật đọc, BR-149, BR-150), mỗi trạng thái ≥ 1; Tỷ lệ có mặt = trung bình Có mặt / số thành phần của các cuộc Đã kết thúc có điểm danh (BR-151); Giờ họp = tổng giờ thực tế của cuộc Đã kết thúc; dải số: Đã họp, Sắp họp, Đã huỷ đúng danh sách, Thông qua "0 / 0 biểu quyết", Biên bản ban hành 0 (chưa ban hành biên bản nào); Người dự họp theo điểm danh: Có mặt = số người có mặt, Vắng = số người vắng của cuộc Đã kết thúc, Lượt mời = 3 × số cuộc họp; Từng cuộc họp: đúng các cuộc họp của tài khoản, tên là link /mgmt/meetings/{id}, ẩn 3 cột luôn 1/0; màn khớp API; không cuộn ngang`,
      async run(qa) {
        await prepareAll(qa)
        // The plan's meetings are reused across runs, so they sit in the month of the day they were first made.
        const FAR_MONTH = planMonth()
        const list = await reporterMeetings(qa, FAR_MONTH)
        const api = await client(qa, REPORTER)
        const read = async (q) => {
          const res = await api.get(`/api/reports/query?from=${FAR_MONTH}&to=${FAR_MONTH}&${q}`)
          qa.expectStatus(res, 200)
          return res.body
        }
        const byStatus = await read('entity=meetings&groupBy=status')
        const wrong = tableProblems(byStatus, { entity: 'meetings', groupBy: 'status' })
        if (byStatus.total.count !== list.length) wrong.push(`total ${byStatus.total.count}, the meeting list has ${list.length}`)
        for (const s of STATUSES) {
          const n = list.filter((m) => m.status === s).length
          const got = byStatus.rows.find((r) => r.key === s)?.values.count ?? 0
          if (got !== n) wrong.push(`${s}: report ${got}, list ${n}`)
          if (n < 1) wrong.push(`no ${s} meeting in the reporter's list`)
        }
        // Expected figures from the Ended meetings themselves.
        const rates = []
        let hours = 0
        let present = 0
        let absent = 0
        for (const m of list.filter((x) => x.status === 'Ended')) {
          const att = (await api.get(`/api/meetings/${m.id}/attendance?pageSize=100`)).body
          if (att.totals.present + att.totals.absent > 0) rates.push(att.totals.present / att.totals.total)
          present += att.totals.present
          absent += att.totals.absent
          const full = (await api.get(`/api/meetings/${m.id}`)).body
          if (full.actualStartedAt && full.actualEndedAt) hours += (new Date(full.actualEndedAt) - new Date(full.actualStartedAt)) / 3_600_000
        }
        const mean = (xs) => xs.reduce((a, x) => a + x, 0) / xs.length
        const wantRate = rates.length ? Math.round(mean(rates) * 10000) / 10000 : null
        if (byStatus.total.attendance !== wantRate) wrong.push(`attendance ${byStatus.total.attendance}, expected ${wantRate} (${rates})`)
        if (Math.abs(byStatus.total.hours - Math.round(hours * 10) / 10) > 0.051) wrong.push(`hours ${byStatus.total.hours}, expected ${hours}`)
        const people = await read('entity=participants&groupBy=attendance')
        wrong.push(...tableProblems(people, { entity: 'participants', groupBy: 'attendance' }))
        if (people.total.count !== 3 * list.length) wrong.push(`invitations ${people.total.count}, expected 3 × ${list.length}`)
        if (people.total.present !== present || people.total.absent !== absent)
          wrong.push(`present/absent ${people.total.present}/${people.total.absent}, attendance lists say ${present}/${absent}`)
        const rep = await report(qa, REPORTER, FAR_MONTH, FAR_MONTH)
        if (rep.votes.closed !== 0 || rep.minutes.issued !== 0) wrong.push(`votes ${JSON.stringify(rep.votes)}, minutes ${JSON.stringify(rep.minutes)}`)
        qa.note(`Báo cáo ${FAR_MONTH} của ${REPORTER}: ${byStatus.rows.map((r) => `${r.key} ${r.values.count}`).join(', ')}; tỷ lệ ${byStatus.total.attendance}; ${byStatus.total.hours} giờ; có mặt ${present}, vắng ${absent}`)
        qa.expect(wrong, wrong.join('\n')).toEqual([])

        const seen = watchReports(qa)
        const tables = watchTables(qa)
        await qa.goto(`/mgmt/reports?from=${FAR_MONTH}&to=${FAR_MONTH}&groupBy=status`)
        const shownRep = await lastReport(qa, seen, (x) => x.from === FAR_MONTH && x.to === FAR_MONTH)
        const shown = await lastReport(qa, tables, (x) => x.params.groupBy === 'status')
        await figuresReady(qa)
        await qa.expect(qa.page.getByRole('button', { name: `Từ tháng: Tháng ${monthLabel(FAR_MONTH)}` })).toBeVisible()
        await expectScreenMatches(qa, shownRep, shown)
        await settled(qa)
        await qa.shot('so-lieu-thang-xa', { fullPage: true })

        await pick(qa, 'Dữ liệu', 'Người dự họp')
        await qa.expect(qa.page).toHaveURL(/entity=participants/)
        await pick(qa, 'Nhóm theo', 'Điểm danh')
        const shownPeople = await lastReport(qa, tables, (x) => x.params.entity === 'participants' && x.params.groupBy === 'attendance')
        await figuresReady(qa)
        await expectTableMatches(qa, shownPeople)
        await settled(qa)
        await qa.shot('nguoi-du-hop-theo-diem-danh', { fullPage: true })

        await pick(qa, 'Dữ liệu', 'Cuộc họp')
        await pick(qa, 'Nhóm theo', 'Từng cuộc họp')
        await pick(qa, 'Hiển thị', 'Tất cả')
        const perMeeting = await lastReport(
          qa,
          tables,
          (x) => x.params.entity === 'meetings' && x.params.groupBy === 'meeting' && x.params.top === undefined,
        )
        await figuresReady(qa)
        await expectTableMatches(qa, perMeeting)
        const ids = list.map((m) => m.id).sort()
        qa.expect(perMeeting.rows.map((r) => r.key).sort(), 'the meetings listed are not the reporter’s').toEqual(ids)
        const links = await queryCard(qa).locator('.report-result tbody a').evaluateAll((as) => as.map((a) => a.getAttribute('href')))
        qa.expect(links.sort()).toEqual(ids.map((id) => `/mgmt/meetings/${id}`).sort())
        await settled(qa)
        await qa.shot('tung-cuoc-hop', { fullPage: true })
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
      },
    },
    {
      id: 'AC5',
      role: REPORTER,
      title: `Tạo thêm một cuộc họp ngày ${FAR}: báo cáo của người tổ chức tăng đúng 1 (tháng, Nháp, Mật); Quản trị và Thành viên không thấy thay đổi; huỷ đi thì chuyển Nháp → Đã huỷ, tổng giữ nguyên`,
      steps: [
        `Đọc báo cáo ${FAR_MONTH} của ${REPORTER}, quantri@, thanhvien@`,
        `${REPORTER} tạo "[QA] FE-15 đếm thêm …" (Mật, Nháp) ngày ${FAR} 19:30, đọc lại cả ba`,
        'Huỷ cuộc họp đó, đọc lại báo cáo người tổ chức',
      ],
      expected:
        'Người tổ chức: total +1, tháng +1, Draft +1, Secret +1, các nhóm khác giữ nguyên. Quản trị (All-in-one, không tham dự) và Thành viên (chưa công bố): không đổi — Quản trị không thấy toàn hệ thống (BR-149, BR-150, PO Q1). Sau huỷ: Draft −1, Cancelled +1, total giữ nguyên (BR-149, BR-150: đã huỷ vẫn đếm)',
      async run(qa) {
        await ensureAccount(qa, REPORTER, 'FE-15 người xem báo cáo', 'Thư ký')
        const read = (who) => report(qa, who, FAR_MONTH, FAR_MONTH)
        const diff = (a, b) => {
          const out = { total: b.total - a.total }
          for (const s of STATUSES) out[s] = countOf(b, 'byStatus', 'status', s) - countOf(a, 'byStatus', 'status', s)
          for (const s of SENSITIVITIES) out[s] = countOf(b, 'bySensitivity', 'sensitivity', s) - countOf(a, 'bySensitivity', 'sensitivity', s)
          out.month = b.byMonth[0].count - a.byMonth[0].count
          return Object.fromEntries(Object.entries(out).filter(([, v]) => v !== 0))
        }
        const before = { rep: await read(REPORTER), admin: await read('admin'), member: await read('member') }
        const stamp = new Date().toISOString().slice(0, 19).replace('T', ' ')
        const m = await createMeeting(qa, { title: `FE-15 đếm thêm ${stamp}`, sensitivity: 'Secret', from: '19:30', to: '20:00' })
        const after = { rep: await read(REPORTER), admin: await read('admin'), member: await read('member') }
        qa.note(`Đã tạo ${m.title} (${m.id}).`)
        const wrong = []
        const d = diff(before.rep, after.rep)
        if (JSON.stringify(d) !== JSON.stringify({ total: 1, Draft: 1, Secret: 1, month: 1 })) wrong.push(`organizer moved by ${JSON.stringify(d)}`)
        const da = diff(before.admin, after.admin)
        if (Object.keys(da).length) wrong.push(`admin moved by ${JSON.stringify(da)} (a meeting it may not read)`)
        const dm = diff(before.member, after.member)
        if (Object.keys(dm).length) wrong.push(`member moved by ${JSON.stringify(dm)} (a draft it may not read)`)
        await transition(qa, REPORTER, m.id, { action: 'Cancel', reason: 'QA FE-15: huỷ sau khi đếm.' })
        const cancelled = await read(REPORTER)
        const dc = diff(after.rep, cancelled)
        if (JSON.stringify(dc) !== JSON.stringify({ Draft: -1, Cancelled: 1 })) wrong.push(`cancelling moved the organizer by ${JSON.stringify(dc)}`)
        qa.expect(wrong, wrong.join('\n')).toEqual([])
      },
    },

    // ------------------------------------------------------------------------- AF-1 choose another period
    {
      id: 'AC6',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Chọn khoảng khác bằng hai ô tháng: URL đổi, số liệu tải lại và khớp API; tải lại trang giữ khoảng',
      steps: ['thuky@ mở Báo cáo', `Chọn Từ tháng = Tháng ${monthLabel(P6.from)}`, `Chọn Đến tháng = Tháng ${monthLabel(P6.to)}`, 'Tải lại trang'],
      expected: `URL ?from=${P6.from}&to=${P6.to}; trang gọi đúng khoảng đó; bảng theo tháng 3 dòng ${monthLabel(P6.from)} → ${monthLabel(P6.to)}; mọi số khớp API; sau tải lại vẫn khoảng đó; không cuộn ngang`,
      async run(qa) {
        const seen = watchReports(qa)
        const tables = watchTables(qa)
        await qa.goto('/mgmt/reports')
        await lastReport(qa, seen)
        await figuresReady(qa)
        await press(qa, qa.page.getByRole('button', { name: /^Từ tháng:/ }))
        await press(qa, qa.page.getByRole('listbox', { name: 'Từ tháng' }).getByRole('option', { name: `Tháng ${monthLabel(P6.from)}` }))
        await qa.expect(qa.page).toHaveURL(new RegExp(`from=${P6.from}`))
        await press(qa, qa.page.getByRole('button', { name: /^Đến tháng:/ }))
        await press(qa, qa.page.getByRole('listbox', { name: 'Đến tháng' }).getByRole('option', { name: `Tháng ${monthLabel(P6.to)}` }))
        await qa.expect(qa.page).toHaveURL(new RegExp(`/mgmt/reports\\?from=${P6.from}&to=${P6.to}$`))
        const rep = await lastReport(qa, seen, (x) => x.from === P6.from && x.to === P6.to)
        if (rep.total === 0) qa.inconclusive(`thuky@ đọc được 0 cuộc họp trong ${P6.from}..${P6.to}: chọn khoảng khác có dữ liệu`)
        const table = await lastReport(qa, tables, (x) => x.from === P6.from && x.to === P6.to)
        await figuresReady(qa)
        await qa.expect(queryCard(qa).locator('.report-result tbody tr')).toHaveCount(3)
        qa.expect(problems(rep, P6.from, P6.to)).toEqual([])
        await expectScreenMatches(qa, rep, table)
        await settled(qa)
        await qa.shot('khoang-ba-thang', { fullPage: true })
        await noSidewaysScroll(qa)
        seen.length = 0
        tables.length = 0
        await qa.page.reload()
        const again = await lastReport(qa, seen, (x) => x.from === P6.from && x.to === P6.to)
        const againTable = await lastReport(qa, tables, (x) => x.from === P6.from && x.to === P6.to)
        await figuresReady(qa)
        await qa.expect(qa.page.getByRole('button', { name: `Từ tháng: Tháng ${monthLabel(P6.from)}` })).toBeVisible()
        await expectScreenMatches(qa, again, againTable)
      },
    },

    // ------------------------------------------------------------------------- EX-1 invalid period
    {
      id: 'AC7',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Khoảng sai trên màn (EX-1): tháng bắt đầu sau tháng kết thúc, hoặc quá 24 tháng → MSG463 dưới thanh chọn, không gọi API, khoá Xuất Excel; tháng sai dạng trên URL bị bỏ qua',
      steps: [
        'thuky@ mở /mgmt/reports?from=2026-06&to=2026-01',
        `Mở /mgmt/reports?from=${addMonths(NOW_MONTH, -24)}&to=${NOW_MONTH} (25 tháng)`,
        `Từ khoảng đúng ${monthLabel(P6.from)} – ${monthLabel(P6.to)}, chọn Từ tháng = Tháng ${monthLabel(NOW_MONTH)} (sau Đến tháng) bằng ô chọn: Đến tháng đi theo, không báo lỗi`,
        'Mở /mgmt/reports?from=2026-13&to=abc',
      ],
      expected: `Câu "${MSG463}" (role alert), không có dải số và thẻ truy vấn, không request /api/reports, nút Xuất Excel bị khoá; URL sai dạng: dùng khoảng mặc định ${DEFAULT.from}..${DEFAULT.to}, không báo lỗi`,
      async run(qa) {
        const seen = watchReports(qa)
        const tables = watchTables(qa)
        const expectInvalid = async (slug) => {
          await qa.expect(qa.page.getByRole('alert').filter({ hasText: MSG463 })).toBeVisible()
          await qa.expect(exportButton(qa)).toBeDisabled()
          await qa.expect(queryCard(qa)).toHaveCount(0)
          await qa.expect(band(qa)).toHaveCount(0)
          await settled(qa)
          await qa.shot(slug)
          await noSidewaysScroll(qa)
          await noRawCodes(qa)
        }
        await qa.goto('/mgmt/reports?from=2026-06&to=2026-01')
        await expectInvalid('bat-dau-sau-ket-thuc')
        await qa.goto(`/mgmt/reports?from=${addMonths(NOW_MONTH, -24)}&to=${NOW_MONTH}`)
        await expectInvalid('qua-24-thang')
        qa.expect([...seen, ...tables], `the page asked the API for an invalid period: ${JSON.stringify([...seen, ...tables].map((s) => [s.from, s.to]))}`).toEqual([])

        await qa.goto(`/mgmt/reports?from=${P6.from}&to=${P6.to}`)
        await lastReport(qa, seen, (x) => x.from === P6.from)
        await figuresReady(qa)
        seen.length = 0
        tables.length = 0
        // A picker never leaves an invalid period: picking a start after the end moves the end along (followPeriod).
        await press(qa, qa.page.getByRole('button', { name: /^Từ tháng:/ }))
        await press(qa, qa.page.getByRole('listbox', { name: 'Từ tháng' }).getByRole('option', { name: `Tháng ${monthLabel(NOW_MONTH)}` }))
        await qa.expect(qa.page).toHaveURL(new RegExp(`from=${NOW_MONTH}&to=${NOW_MONTH}`))
        await lastReport(qa, tables, (x) => x.from === NOW_MONTH && x.to === NOW_MONTH)
        await figuresReady(qa)
        await qa.expect(qa.page.getByRole('alert').filter({ hasText: MSG463 })).toHaveCount(0)
        await qa.expect(qa.page.getByRole('button', { name: `Đến tháng: Tháng ${monthLabel(NOW_MONTH)}` })).toBeVisible()
        await settled(qa)
        await qa.shot('chon-tu-thang-sau-den-thang')

        await qa.goto('/mgmt/reports?from=2026-13&to=abc')
        const rep = await lastReport(qa, seen, (x) => x.from === DEFAULT.from && x.to === DEFAULT.to)
        await figuresReady(qa)
        await qa.expect(qa.page.getByRole('alert').filter({ hasText: MSG463 })).toHaveCount(0)
        await qa.expect(qa.page.getByRole('button', { name: `Từ tháng: Tháng ${monthLabel(DEFAULT.from)}` })).toBeVisible()
        await expectBandMatches(qa, rep)
        await settled(qa)
        await qa.shot('url-sai-dang-dung-mac-dinh')
      },
    },
    {
      id: 'AC8',
      role: 'member',
      title: 'API từ chối khoảng sai bằng 400 MSG463 ở cả xem và xuất (UC-79a); 24 tháng vẫn được',
      steps: [
        'Gọi /api/reports/meetings và /export với: chỉ from; chỉ to; 2026-1; 2026-13; 1999-12; 9999-01; from sau to; 25 tháng',
        'Gọi với đúng 24 tháng',
      ],
      expected: `Mỗi trường hợp: 400 {code "MSG463", message "${MSG463}"}, xuất không trả tệp; 24 tháng: 200`,
      async run(qa) {
        const api = await client(qa, 'member')
        const bad = [
          `from=${NOW_MONTH}`,
          `to=${NOW_MONTH}`,
          'from=2026-1&to=2026-03',
          'from=2026-01&to=2026-13',
          'from=1999-12&to=2000-01',
          'from=9999-01&to=9999-01',
          'from=2026-06&to=2026-01',
          `from=${addMonths(NOW_MONTH, -24)}&to=${NOW_MONTH}`,
        ]
        const wrong = []
        for (const q of bad)
          for (const path of ['/api/reports/meetings', '/api/reports/meetings/export']) {
            const r = await api.get(`${path}?${q}`)
            if (r.status !== 400 || r.body?.code !== 'MSG463' || r.body?.message !== MSG463)
              wrong.push(`${path}?${q} → ${r.status} ${String(JSON.stringify(r.body)).slice(0, 160)}`)
            if (String(r.headers['content-type']).includes('spreadsheetml')) wrong.push(`${path}?${q} returned a file`)
          }
        for (const path of ['/api/reports/meetings', '/api/reports/meetings/export']) {
          const r = await api.get(`${path}?from=${addMonths(NOW_MONTH, -23)}&to=${NOW_MONTH}`)
          if (r.status !== 200) wrong.push(`24 months ${path} → ${r.status}`)
        }
        qa.expect(wrong, wrong.join('\n')).toEqual([])
      },
    },

    // ------------------------------------------------------------------------- AF-2 nothing readable
    {
      id: 'AC9',
      role: REPORTER,
      devices: ['desktop', 'phone'],
      title: 'Không có cuộc họp nào đọc được trong khoảng (AF-2): dải số toàn 0, MSG466 thay cho bảng; vẫn xuất được tệp bảng với Tổng 0 (UC-79b AF-1)',
      steps: [
        `${REPORTER} (chỉ có cuộc họp tháng ${monthLabel(FAR_MONTH)}) mở Báo cáo với khoảng mặc định`,
        `Mở /mgmt/reports?from=${EMPTY.from}&to=${EMPTY.to}, bấm Xuất Excel`,
      ],
      expected: `API báo cáo total 0, ba trung bình null; API truy vấn: các tháng đều 0, Tổng 0; màn: dải số Đã họp 0, Sắp họp 0, Đã huỷ 0, Tỷ lệ có mặt "Chưa có", Thông qua "0 / 0 biểu quyết"; "${MSG466}" thay cho bảng; Xuất Excel bật, tệp bao-cao-cuoc-hop_${EMPTY.from}_${EMPTY.to}.xlsx có các tháng 0 và dòng Tổng 0; đúng một dòng audit report.exported; không cuộn ngang`,
      async run(qa) {
        await prepareAll(qa)
        const seen = watchReports(qa)
        const tables = watchTables(qa)
        await qa.goto('/mgmt/reports')
        const def = await lastReport(qa, seen)
        qa.expect(def.total, `the reporter reads ${def.total} meetings in the default period`).toBe(0)
        await qa.expect(queryCard(qa).getByText(MSG466)).toBeVisible()
        await figuresReady(qa)
        await expectBandMatches(qa, def)
        await settled(qa)
        await qa.shot('mac-dinh-khong-co-cuoc-hop')

        await qa.goto(`/mgmt/reports?from=${EMPTY.from}&to=${EMPTY.to}`)
        const rep = await lastReport(qa, seen, (x) => x.from === EMPTY.from && x.to === EMPTY.to)
        const table = await lastReport(qa, tables, (x) => x.from === EMPTY.from && x.to === EMPTY.to)
        qa.expect(problems(rep, EMPTY.from, EMPTY.to)).toEqual([])
        qa.expect(rep.total).toBe(0)
        qa.expect([rep.attendance.averageRate, rep.duration.averageMinutes, rep.minutesTurnaround.averageHours]).toEqual([null, null, null])
        qa.expect(table.rows.map((r) => [r.key, r.values.count])).toEqual(rep.byMonth.map((m) => [m.month, 0]))
        qa.expect(table.total.count).toBe(0)
        await qa.expect(queryCard(qa).getByText(MSG466)).toBeVisible()
        await qa.expect(queryCard(qa).locator('.report-result')).toHaveCount(0)
        await figuresReady(qa)
        await expectBandMatches(qa, rep)
        await qa.expect(exportButton(qa)).toBeEnabled()
        await settled(qa)
        await qa.shot('khoang-trong-msg466')
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
        if (qa.device === 'desktop') {
          const since = Date.now()
          const file = await downloadExport(qa)
          qa.expect(file.name).toBe(`bao-cao-cuoc-hop_${EMPTY.from}_${EMPTY.to}.xlsx`)
          const wrong = tableFileProblems(readXlsx(file.buf), table)
          qa.expect(wrong, wrong.join('\n')).toEqual([])
          await expectAuditRow(qa, REPORTER, EMPTY.from, EMPTY.to, since, { report: 'meetings', groupBy: 'month' })
        }
      },
    },

    // ------------------------------------------------------------------------- UC-79b export
    {
      id: 'AC10',
      role: REPORTER,
      title: `Xuất Excel từ màn (UC-79b): tệp là đúng bảng đang xem (Người dự họp theo điểm danh, lọc Đã kết thúc, tháng của các cuộc họp kế hoạch), số là số, có dòng Tổng; đúng một dòng audit`,
      steps: [
        `${REPORTER} mở /mgmt/reports trong tháng của các cuộc họp kế hoạch`,
        'Chọn Dữ liệu = Người dự họp, Nhóm theo = Điểm danh, Trạng thái cuộc họp = Đã kết thúc',
        'Bấm Xuất Excel',
        'Quản trị đọc nhật ký hệ thống, sự kiện report.exported',
      ],
      expected: `Tệp bao-cao-thanh-phan_<tháng>_<tháng>.xlsx, sheet "Người dự họp": khoảng "<tháng> – <tháng>", Nhóm theo "Điểm danh", Bộ lọc "Trạng thái cuộc họp: Đã kết thúc", thời điểm xuất (GMT+7); hàng tiêu đề #, Điểm danh, Lượt mời, Có mặt, Vắng, Tỷ lệ có mặt; từng nhóm đúng hạng, nhãn và số API trang nhận, Tổng đúng; không có tiêu đề cuộc họp; nhật ký có đúng một dòng "Xuất báo cáo" mới của tài khoản đó, objectType Report, details report=participants, groupBy=attendance, filters status=Ended (BR-009–BR-011)`,
      async run(qa) {
        await prepareAll(qa)
        const FAR_MONTH = planMonth()
        const tables = watchTables(qa)
        await qa.goto(`/mgmt/reports?from=${FAR_MONTH}&to=${FAR_MONTH}`)
        await lastReport(qa, tables, (x) => x.from === FAR_MONTH)
        await figuresReady(qa)
        await pick(qa, 'Dữ liệu', 'Người dự họp')
        await pick(qa, 'Nhóm theo', 'Điểm danh')
        await pick(qa, 'Trạng thái cuộc họp', 'Đã kết thúc')
        await qa.expect(qa.page).toHaveURL(/status=Ended/)
        const table = await lastReport(
          qa,
          tables,
          (x) => x.params.entity === 'participants' && x.params.groupBy === 'attendance' && x.params.status === 'Ended',
        )
        await figuresReady(qa)
        await expectTableMatches(qa, table)
        qa.expect(table.filters).toEqual([{ key: 'status', label: 'Trạng thái cuộc họp', value: 'Ended', valueLabel: 'Đã kết thúc' }])
        const since = Date.now()
        const file = await downloadExport(qa)
        await settled(qa)
        await qa.shot('sau-khi-xuat-excel', { fullPage: true })
        qa.expect(file.name).toBe(`bao-cao-thanh-phan_${FAR_MONTH}_${FAR_MONTH}.xlsx`)
        const xlsx = readXlsx(file.buf)
        const titles = STATUSES.map((s) => made[s].title)
        const wrong = tableFileProblems(xlsx, table, titles)
        // The figures were computed again at export time: someone may have marked attendance in between, so re-read once.
        if (wrong.length) {
          const fresh = (
            await (await client(qa, REPORTER)).get(
              `/api/reports/query?entity=participants&from=${FAR_MONTH}&to=${FAR_MONTH}&groupBy=attendance&status=Ended`,
            )
          ).body
          const again = tableFileProblems(xlsx, fresh, titles)
          if (again.length < wrong.length) wrong.splice(0, wrong.length, ...again)
        }
        qa.note(`Tệp: ${file.name}, ${file.buf.length} byte, ${xlsx.rows.length} dòng có dữ liệu`)
        qa.expect(wrong, wrong.join('\n')).toEqual([])
        await expectAuditRow(qa, REPORTER, FAR_MONTH, FAR_MONTH, since, { report: 'participants', groupBy: 'attendance', filters: 'status=Ended' })
      },
    },

    // ------------------------------------------------------------------------- EX-2 (server answers simulated in the browser)
    {
      id: 'AC11',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Lỗi máy chủ (giả lập câu trả lời trong trình duyệt): MSG464 của truy vấn kèm Thử lại trong thẻ truy vấn, của dải số thay dải số; MSG465 là toast; MSG463 của server hiện dưới thanh chọn và khoá Xuất Excel',
      steps: [
        'Chặn GET /api/reports/query trả 500 {code MSG464}, mở Báo cáo',
        'Bỏ chặn, bấm Thử lại',
        'Chặn GET /api/reports/meetings trả 500 {code MSG464}, tải lại trang',
        'Bỏ chặn; chặn /api/reports/query/export trả 500 {code MSG465}, bấm Xuất Excel',
        'Chặn GET /api/reports/query trả 400 {code MSG463} cho một khoảng đúng, mở khoảng đó',
      ],
      expected: `Truy vấn lỗi: "${MSG464}" (role alert) với nút Thử lại trong thẻ truy vấn, không có bảng, dải số vẫn hiện, Xuất Excel khoá; Thử lại → bảng hiện; dải số lỗi: "${MSG464}" thay dải số, bảng vẫn hiện; toast "${MSG465}", không tải tệp; "${MSG463}" dưới thanh chọn, không có dải số và bảng, nút Xuất Excel bị khoá. (Lỗi database thật không gây ra được ở đây: đây chỉ kiểm phần FE.)`,
      async run(qa) {
        const isQuery = (url) => new URL(url).pathname === '/api/reports/query'
        const isReport = (url) => new URL(url).pathname === '/api/reports/meetings'
        const isExport = (url) => new URL(url).pathname === '/api/reports/query/export'
        const fail = (status, code, message) => (route) =>
          route.fulfill({ status, contentType: 'application/json', body: JSON.stringify({ code, message }) })
        await qa.page.route(isQuery, fail(500, 'MSG464', MSG464))
        await qa.goto('/mgmt/reports')
        const alert = queryCard(qa).getByRole('alert').filter({ hasText: MSG464 })
        await qa.expect(alert).toBeVisible()
        await qa.expect(queryCard(qa).locator('.report-result')).toHaveCount(0)
        await qa.expect(band(qa).locator('.report-figure dt')).toHaveCount(7)
        await qa.expect(exportButton(qa)).toBeDisabled()
        await settled(qa)
        await qa.shot('msg464-thu-lai')
        await noSidewaysScroll(qa)
        await qa.page.unroute(isQuery)
        await press(qa, alert.getByRole('button', { name: 'Thử lại' }))
        await figuresReady(qa)

        await qa.page.route(isReport, fail(500, 'MSG464', MSG464))
        await qa.page.reload()
        await qa.expect(qa.page.getByRole('alert').filter({ hasText: MSG464 })).toBeVisible()
        await qa.expect(band(qa)).toHaveCount(0)
        await qa.expect(queryCard(qa).locator('.report-result')).toBeVisible()
        await settled(qa)
        await qa.shot('msg464-dai-so-lieu')
        await qa.page.unroute(isReport)
        await qa.page.reload()
        await figuresReady(qa)

        await qa.page.route(isExport, fail(500, 'MSG465', MSG465))
        let downloaded = false
        qa.page.on('download', () => (downloaded = true))
        await press(qa, exportButton(qa))
        await qa.expect(qa.page.locator('.toast-stack').getByText(MSG465)).toBeVisible()
        await settled(qa)
        await qa.shot('msg465-toast')
        qa.expect(downloaded, 'a file was downloaded on MSG465').toBe(false)
        await qa.page.unroute(isExport)

        await qa.page.route(isQuery, fail(400, 'MSG463', MSG463))
        await qa.goto('/mgmt/reports?from=2026-02&to=2026-04')
        await qa.expect(qa.page.getByRole('alert').filter({ hasText: MSG463 })).toBeVisible()
        await qa.expect(queryCard(qa)).toHaveCount(0)
        await qa.expect(band(qa)).toHaveCount(0)
        await qa.expect(exportButton(qa)).toBeDisabled()
        await settled(qa)
        await qa.shot('msg463-tu-server')
        await noRawCodes(qa)
      },
    },
  ],
}

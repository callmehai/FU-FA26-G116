// FE-15 — Báo cáo & Thống kê (QA subtask SCRUM-202). docs/uc-map.html maps FE-15 to UC-70, UC-70a View Reports &
// Statistics and UC-70b Export Report (story SCRUM-38). Criteria come from the UC specification
// (docs/01-requirements/05-use-case-specifications.md), BR-432 → BR-438 and BR-447 (06-business-rules.md), the contract
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
//   report counts Cancelled meetings too (BR-434), so each run leaves one Cancelled meeting behind.
// - "[QA] FE-15 tài khoản tạm" (qa-fe15-inactive@trustcabinet.local, role Thành viên) is reactivated, signed in and
//   deactivated by AC12, and left deactivated.
// Every export (AC1, AC10, AC9) writes one audit row report.exported, as BR-438 requires.

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

// ---- months and days, in Vietnam's calendar as the server counts them (BR-433) ----
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
const DEFAULT = { from: addMonths(NOW_MONTH, -11), to: NOW_MONTH } // BR-433: 12 months ending with this month
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

/** The reporter's meetings: one per status (BR-434 counts every status, Cancelled included). */
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

// ---- formatting, as people read it (the contract: "87,3 %", "1 giờ 35 phút", "30,5 giờ") ----
const percent = new Intl.NumberFormat('vi-VN', { style: 'percent', maximumFractionDigits: 1 })
const decimal = new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 1 })
const fmtRate = (r) => percent.format(r)
const fmtMinutes = (m) => {
  const total = Math.round(m)
  const h = Math.floor(total / 60)
  const rest = total % 60
  if (h === 0) return `${rest} phút`
  return rest === 0 ? `${h} giờ` : `${h} giờ ${rest} phút`
}
const fmtHours = (h) => (h < 48 ? `${decimal.format(h)} giờ` : `${decimal.format(h / 24)} ngày`)
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
    // BR-435: Chair and Secretary present, Member absent; then the Chair ends the meeting.
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

/** What is wrong with a report on its own terms (BR-433, BR-434, BR-437 and the contract's shape). */
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
  // BR-437: counts, rates and averages only.
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

/** Records every GET /api/reports/meetings the page makes (not the export), with its query and body. */
function watchReports(qa) {
  const seen = []
  qa.page.on('response', async (r) => {
    const u = new URL(r.url())
    if (u.pathname !== '/api/reports/meetings' || r.request().method() !== 'GET') return
    let body
    try {
      body = await r.json()
    } catch {
      body = null
    }
    seen.push({ from: u.searchParams.get('from'), to: u.searchParams.get('to'), status: r.status(), body })
  })
  return seen
}
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
const exportButton = (qa) => qa.page.getByRole('button', { name: /Xuất Excel|Đang xuất/ })

/** Waits for the figures to be on screen and not refreshing. */
async function figuresReady(qa) {
  await qa.expect(region(qa, 'Theo trạng thái').locator('tbody tr')).toHaveCount(8, { timeout: 15_000 })
  await qa.page.waitForFunction(() => !document.querySelector('[aria-busy="true"], .data-table-stale'))
}

/** The screen shows exactly the report the page received (UC-70a step 4). */
async function expectScreenMatches(qa, rep) {
  const wrong = []
  const header = squash(await qa.page.locator('.page-header-desc').innerText())
  if (header !== `${rep.total} cuộc họp`) wrong.push(`header "${header}", API total ${rep.total}`)
  const rows = async (name) =>
    (await region(qa, name).locator('tbody tr').all()).map(async (tr) => (await tr.locator('td').allInnerTexts()).map(squash))
  const status = await Promise.all(await rows('Theo trạng thái'))
  const wantStatus = rep.byStatus.map((s) => [STATUS_LABEL[s.status], String(s.count)])
  if (JSON.stringify(status) !== JSON.stringify(wantStatus)) wrong.push(`status table ${JSON.stringify(status)} ≠ ${JSON.stringify(wantStatus)}`)
  const sens = await Promise.all(await rows('Theo mức độ'))
  const wantSens = rep.bySensitivity.map((s) => [SENSITIVITY_LABEL[s.sensitivity], String(s.count)])
  if (JSON.stringify(sens) !== JSON.stringify(wantSens)) wrong.push(`sensitivity table ${JSON.stringify(sens)} ≠ ${JSON.stringify(wantSens)}`)
  const months = (await Promise.all(await rows('Theo tháng'))).map((r) => r.slice(0, 2))
  const wantMonths = rep.byMonth.map((m) => [monthLabel(m.month), String(m.count)])
  if (JSON.stringify(months) !== JSON.stringify(wantMonths)) wrong.push(`month table ${JSON.stringify(months)} ≠ ${JSON.stringify(wantMonths)}`)
  const ended = countOf(rep, 'byStatus', 'status', 'Ended')
  const overview = await Promise.all(await rows('Tổng quan'))
  const wantOverview = [
    ['Tỷ lệ tham dự trung bình', rep.attendance.averageRate === null ? NO_DATA : fmtRate(rep.attendance.averageRate), rep.attendance.meetings],
    ['Thời lượng họp trung bình', rep.duration.averageMinutes === null ? NO_DATA : fmtMinutes(rep.duration.averageMinutes), rep.duration.meetings],
    [
      'Từ kết thúc tới ban hành biên bản',
      rep.minutesTurnaround.averageHours === null ? NO_DATA : fmtHours(rep.minutesTurnaround.averageHours),
      rep.minutesTurnaround.meetings,
    ],
  ]
  if (overview.length !== 3) wrong.push(`overview has ${overview.length} rows`)
  wantOverview.forEach(([label, value, basis], i) => {
    const [l, v] = overview[i] ?? []
    if (l !== label) wrong.push(`overview row ${i + 1} label "${l}" ≠ "${label}"`)
    // The value and "trên x/y" are two boxes side by side (a line apart on a phone): compare without the gap.
    const want = squash(`${value} trên ${basis}/${ended} cuộc họp`)
    if (String(v).replace(/ /g, '') !== want.replace(/ /g, '')) wrong.push(`overview "${label}" shows "${v}", expected "${want}"`)
  })
  qa.expect(wrong, wrong.join('\n')).toEqual([])
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

/** The exported file holds exactly the figures of the report (BR-438) and nothing about a meeting (BR-437). */
function fileProblems(file, rep, titles = []) {
  const wrong = []
  if (file.sheetName !== 'Báo cáo cuộc họp') wrong.push(`sheet "${file.sheetName}"`)
  const byLabel = new Map()
  const blocks = []
  let block = null
  for (const r of file.rows) {
    const label = r.A?.text
    if (!label) continue
    if (r.B?.text === 'Số cuộc họp' || r.B?.text === 'Giá trị') {
      block = { head: label, rows: [] }
      blocks.push(block)
      continue
    }
    if (block) block.rows.push(r)
    else byLabel.set(label, r)
  }
  if (byLabel.get('Khoảng thời gian')?.B?.text !== `${monthLabel(rep.from)} – ${monthLabel(rep.to)}`)
    wrong.push(`period cell "${byLabel.get('Khoảng thời gian')?.B?.text}"`)
  if (!/\(GMT\+7\)$/.test(byLabel.get('Thời điểm xuất')?.B?.text ?? '')) wrong.push(`export time "${byLabel.get('Thời điểm xuất')?.B?.text}"`)
  if (byLabel.get('Tổng số cuộc họp')?.B?.num !== rep.total) wrong.push(`total cell ${byLabel.get('Tổng số cuộc họp')?.B?.num} ≠ ${rep.total}`)
  const table = (head) => blocks.find((b) => b.head === head)?.rows.map((r) => [r.A?.text, r.B?.num]) ?? null
  const months = table('Tháng')
  const want = rep.byMonth.map((m) => [monthLabel(m.month), m.count])
  if (JSON.stringify(months) !== JSON.stringify(want)) wrong.push(`month table ${JSON.stringify(months)} ≠ ${JSON.stringify(want)}`)
  const st = table('Trạng thái')
  const wantSt = rep.byStatus.map((s) => [STATUS_LABEL[s.status], s.count])
  if (JSON.stringify(st) !== JSON.stringify(wantSt)) wrong.push(`status table ${JSON.stringify(st)} ≠ ${JSON.stringify(wantSt)}`)
  const se = table('Mức độ mật')
  const wantSe = rep.bySensitivity.map((s) => [SENSITIVITY_LABEL[s.sensitivity], s.count])
  if (JSON.stringify(se) !== JSON.stringify(wantSe)) wrong.push(`sensitivity table ${JSON.stringify(se)} ≠ ${JSON.stringify(wantSe)}`)
  const avg = blocks.find((b) => b.head === 'Chỉ số')?.rows ?? []
  const wantAvg = [
    [/^Tỷ lệ tham dự/, rep.attendance.averageRate, rep.attendance.meetings, rep.attendance.meetingsWithoutAttendance],
    [/^Thời lượng họp/, rep.duration.averageMinutes, rep.duration.meetings, rep.duration.meetingsWithoutTimes],
    [/ban hành biên bản/, rep.minutesTurnaround.averageHours, rep.minutesTurnaround.meetings, rep.minutesTurnaround.meetingsWithoutIssuedMinutes],
  ]
  if (avg.length !== 3) wrong.push(`${avg.length} average rows, expected 3`)
  wantAvg.forEach(([label, value, basis, missing], i) => {
    const r = avg[i] ?? {}
    if (!label.test(r.A?.text ?? '')) wrong.push(`average row ${i + 1} "${r.A?.text}"`)
    if (value === null ? r.B?.text !== NO_DATA : Math.abs((r.B?.num ?? NaN) - value) > 1e-6) wrong.push(`"${r.A?.text}" value ${JSON.stringify(r.B)}, API ${value}`)
    if (r.C?.num !== basis || r.D?.num !== missing) wrong.push(`"${r.A?.text}" counted ${r.C?.num}/${r.D?.num}, API ${basis}/${missing}`)
  })
  for (const t of titles) if (file.xml.includes(t)) wrong.push(`meeting title "${t}" is in the file`)
  if (/\[QA\]/.test(file.xml)) wrong.push('a "[QA]" meeting title is in the file')
  return wrong
}

async function downloadExport(qa) {
  const [dl] = await Promise.all([qa.page.waitForEvent('download', { timeout: 20_000 }), press(qa, exportButton(qa))])
  const path = await dl.path()
  return { name: dl.suggestedFilename(), buf: readFileSync(path) }
}

/** The audit row of the export just made by `who`, read by the admin (BR-438). */
async function expectAuditRow(qa, who, from, to, since) {
  const user = await me(qa, who)
  const res = await (await client(qa, 'admin')).get(`/api/audit-log?action=report.exported&actor=${user.id}&pageSize=10`)
  qa.expectStatus(res, 200)
  const row = res.body.items.find(
    (r) => new Date(r.occurredAt).getTime() >= since - 5_000 && JSON.stringify(r.details).includes(from) && JSON.stringify(r.details).includes(to),
  )
  qa.expect(row, `no report.exported audit row for ${who} ${from}..${to} since the export`).toBeTruthy()
  qa.expect(row.objectType).toBe('Report')
  qa.expect(row.objectId ?? null).toBeNull()
  qa.note(`Audit: ${row.actionLabel ?? row.action} · ${row.actor?.name} · ${JSON.stringify(row.details)} · IP ${row.ipAddress}`)
}

// ---------------------------------------------------------------------------------------------------------------------

export default {
  ticket: 'SCRUM-202',
  title: 'FE-15 Báo cáo & Thống kê (UC-70a, UC-70b — SCRUM-38)',
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

    // ------------------------------------------------------------------------- who may see it (BR-432)
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
            wrong.push(`${who} default period ${r.body.from}..${r.body.to}, expected ${DEFAULT.from}..${DEFAULT.to} (BR-433)`)
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
        await qa.expect(qa.page.getByText('Theo trạng thái')).toHaveCount(0)
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

    // ------------------------------------------------------------------------- the page (UC-70a Normal Flow)
    {
      id: 'AC2',
      role: 'member',
      devices: ['desktop', 'phone'],
      title: 'Thành viên mở Báo cáo từ sidebar: khoảng mặc định 12 tháng tới tháng này, mọi số trên màn khớp API trang nhận được, 8 trạng thái theo thứ tự',
      steps: ['Đăng nhập thanhvien@, mở danh sách cuộc họp', 'Bấm "Báo cáo" trên sidebar (điện thoại: mở menu trước)'],
      expected: `URL /mgmt/reports; hai ô "Từ tháng ${monthLabel(DEFAULT.from)}" "Đến tháng ${monthLabel(DEFAULT.to)}"; trang gọi API from=${DEFAULT.from}&to=${DEFAULT.to}; dòng mô tả "N cuộc họp" = total; bảng Theo trạng thái đủ 8 dòng Nháp → Đã huỷ, Theo mức độ 3 dòng, Theo tháng 12 dòng, Tổng quan 3 chỉ số kèm "trên x/y cuộc họp" — đúng số API; API tự khớp (tổng các nhóm = total); nút Xuất Excel bấm được; không cuộn ngang, không mã thô`,
      async run(qa) {
        const seen = watchReports(qa)
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
        qa.expect(seen[0], 'the first request is not the default period').toMatchObject({ from: DEFAULT.from, to: DEFAULT.to })
        await figuresReady(qa)
        await qa.expect(qa.page.getByRole('button', { name: `Từ tháng: Tháng ${monthLabel(DEFAULT.from)}` })).toBeVisible()
        await qa.expect(qa.page.getByRole('button', { name: `Đến tháng: Tháng ${monthLabel(DEFAULT.to)}` })).toBeVisible()
        qa.expect(problems(rep, DEFAULT.from, DEFAULT.to)).toEqual([])
        await expectScreenMatches(qa, rep)
        await qa.expect(qa.page.getByText(/^Số liệu lúc /)).toBeVisible()
        await qa.expect(exportButton(qa)).toBeEnabled()
        await settled(qa)
        await qa.shot('bao-cao-mac-dinh', { fullPage: true })
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
        // An explanation is behind a visible (i) that works on a tap too (no hover-only text).
        const tip = region(qa, 'Tổng quan').getByRole('button', { name: /Số người có mặt/ })
        await press(qa, tip)
        await qa.expect(qa.page.getByRole('tooltip')).toContainText('Số người có mặt')
        await qa.shot('giai-thich-ty-le-tham-du')
      },
    },
    {
      id: 'AC3',
      role: 'admin',
      title: 'API tự khớp với mọi vai trò và nhiều khoảng: tổng theo tháng = theo trạng thái = theo mức độ = total, đủ tháng, 8 trạng thái đúng thứ tự enum, không trường nào mô tả cuộc họp',
      steps: [`Gọi GET /api/reports/meetings bằng 4 tài khoản seed với khoảng mặc định, ${DEFAULT.to}..${DEFAULT.to}, ${addMonths(NOW_MONTH, -23)}..${NOW_MONTH} (24 tháng) và ${NOW_MONTH}..${FAR_MONTH}`],
      expected:
        'Mỗi lần: byMonth đủ mọi tháng kể cả tháng 0; byStatus Draft, WaitingApproval, Rejected, Approved, Published, InProgress, Ended, Cancelled; bySensitivity Normal, Internal, Secret; ba tổng bằng total; cơ sở của 3 chỉ số trung bình cộng lại bằng số Đã kết thúc; trung bình null đúng khi không có cuộc họp để tính; generatedAt +07:00; không id/tiêu đề (BR-437)',
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
      title: `Số liệu đúng trên dữ liệu của riêng kế hoạch (tháng ${monthLabel(FAR_MONTH)}): mỗi trạng thái, mức độ khớp danh sách cuộc họp; tỷ lệ tham dự, thời lượng đúng; chỉ số chưa có dữ liệu ghi "Chưa có dữ liệu"; không lộ tên cuộc họp`,
      steps: [
        `Tài khoản ${REPORTER} có 8 cuộc họp [QA] FE-15 ngày ${FAR} (một mỗi trạng thái) và các cuộc "đếm thêm" đã huỷ của AC5`,
        `Mở /mgmt/reports?from=${FAR_MONTH}&to=${FAR_MONTH}`,
      ],
      expected: `Mỗi trạng thái và mức độ trên API bằng số đếm từ danh sách cuộc họp (cùng luật đọc, BR-432/BR-434), mỗi trạng thái ≥ 1; tỷ lệ tham dự = trung bình Có mặt / số thành phần của các cuộc Đã kết thúc có điểm danh (BR-435); thời lượng = từ bắt đầu tới kết thúc thực tế, phút 1 chữ số (BR-447); "Từ kết thúc tới ban hành biên bản" ghi "Chưa có dữ liệu" (chưa ban hành biên bản nào, AF-3); màn khớp API; không có tên cuộc họp nào trên trang (BR-437); không cuộn ngang`,
      async run(qa) {
        await prepareAll(qa)
        const list = await reporterMeetings(qa, FAR_MONTH)
        const rep = await report(qa, REPORTER, FAR_MONTH, FAR_MONTH)
        const wrong = problems(rep, FAR_MONTH, FAR_MONTH)
        if (rep.total !== list.length) wrong.push(`total ${rep.total}, the meeting list has ${list.length}`)
        for (const s of STATUSES) {
          const n = list.filter((m) => m.status === s).length
          if (countOf(rep, 'byStatus', 'status', s) !== n) wrong.push(`${s}: report ${countOf(rep, 'byStatus', 'status', s)}, list ${n}`)
          if (n < 1) wrong.push(`no ${s} meeting in the reporter's list`)
        }
        for (const s of SENSITIVITIES) {
          const n = list.filter((m) => m.sensitivity === s).length
          if (countOf(rep, 'bySensitivity', 'sensitivity', s) !== n) wrong.push(`${s}: report ${countOf(rep, 'bySensitivity', 'sensitivity', s)}, list ${n}`)
        }
        // Expected averages from the Ended meetings themselves.
        const api = await client(qa, REPORTER)
        const rates = []
        const minutes = []
        for (const m of list.filter((x) => x.status === 'Ended')) {
          const att = (await api.get(`/api/meetings/${m.id}/attendance?pageSize=100`)).body
          if (att.totals.present + att.totals.absent > 0) rates.push(att.totals.present / att.totals.total)
          const full = (await api.get(`/api/meetings/${m.id}`)).body
          if (full.actualStartedAt && full.actualEndedAt) minutes.push((new Date(full.actualEndedAt) - new Date(full.actualStartedAt)) / 60000)
        }
        const mean = (xs) => xs.reduce((a, x) => a + x, 0) / xs.length
        const wantRate = rates.length ? Math.round(mean(rates) * 10000) / 10000 : null
        const wantMin = minutes.length ? Math.round(mean(minutes) * 10) / 10 : null
        if (rep.attendance.averageRate !== wantRate) wrong.push(`averageRate ${rep.attendance.averageRate}, expected ${wantRate} (${rates})`)
        if (rep.attendance.meetings !== rates.length) wrong.push(`attendance.meetings ${rep.attendance.meetings}, expected ${rates.length}`)
        if (wantMin === null ? rep.duration.averageMinutes !== null : Math.abs(rep.duration.averageMinutes - wantMin) > 0.051)
          wrong.push(`averageMinutes ${rep.duration.averageMinutes}, expected ${wantMin}`)
        if (rep.minutesTurnaround.averageHours !== null) wrong.push(`averageHours ${rep.minutesTurnaround.averageHours}, no minutes were issued`)
        qa.note(`Báo cáo ${FAR_MONTH} của ${REPORTER}: total ${rep.total}; ${rep.byStatus.map((s) => `${s.status} ${s.count}`).join(', ')}; tỷ lệ ${rep.attendance.averageRate}; thời lượng ${rep.duration.averageMinutes} phút`)
        qa.expect(wrong, wrong.join('\n')).toEqual([])

        const seen = watchReports(qa)
        await qa.goto(`/mgmt/reports?from=${FAR_MONTH}&to=${FAR_MONTH}`)
        const shown = await lastReport(qa, seen, (x) => x.from === FAR_MONTH && x.to === FAR_MONTH)
        await figuresReady(qa)
        await qa.expect(qa.page.getByRole('button', { name: `Từ tháng: Tháng ${monthLabel(FAR_MONTH)}` })).toBeVisible()
        await expectScreenMatches(qa, shown)
        await qa.expect(region(qa, 'Tổng quan').getByText(NO_DATA)).toBeVisible()
        await settled(qa)
        await qa.shot('so-lieu-thang-xa', { fullPage: true })
        const text = await qa.page.locator('main').innerText()
        for (const m of list) qa.expect(text, `meeting title "${m.title}" is on the page`).not.toContain(m.title)
        qa.expect(text).not.toContain('[QA]')
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
        'Người tổ chức: total +1, tháng +1, Draft +1, Secret +1, các nhóm khác giữ nguyên. Quản trị (All-in-one, không tham dự) và Thành viên (chưa công bố): không đổi — Quản trị không thấy toàn hệ thống (BR-432, PO Q1). Sau huỷ: Draft −1, Cancelled +1, total giữ nguyên (BR-434: đã huỷ vẫn đếm)',
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
      expected: `URL ?from=${P6.from}&to=${P6.to}; trang gọi đúng khoảng đó; Theo tháng 3 dòng ${monthLabel(P6.from)} → ${monthLabel(P6.to)}; mọi số khớp API; sau tải lại vẫn khoảng đó; không cuộn ngang`,
      async run(qa) {
        const seen = watchReports(qa)
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
        await figuresReady(qa)
        await qa.expect(region(qa, 'Theo tháng').locator('tbody tr')).toHaveCount(3)
        qa.expect(problems(rep, P6.from, P6.to)).toEqual([])
        await expectScreenMatches(qa, rep)
        await settled(qa)
        await qa.shot('khoang-ba-thang', { fullPage: true })
        await noSidewaysScroll(qa)
        seen.length = 0
        await qa.page.reload()
        const again = await lastReport(qa, seen, (x) => x.from === P6.from && x.to === P6.to)
        await figuresReady(qa)
        await qa.expect(qa.page.getByRole('button', { name: `Từ tháng: Tháng ${monthLabel(P6.from)}` })).toBeVisible()
        await expectScreenMatches(qa, again)
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
        `Từ khoảng đúng ${monthLabel(P6.from)} – ${monthLabel(P6.to)}, chọn Từ tháng = Tháng ${monthLabel(NOW_MONTH)} (sau Đến tháng) bằng ô chọn`,
        'Mở /mgmt/reports?from=2026-13&to=abc',
      ],
      expected: `Câu "${MSG463}" (role alert), không có bảng số liệu, không request /api/reports, nút Xuất Excel bị khoá; URL sai dạng: dùng khoảng mặc định ${DEFAULT.from}..${DEFAULT.to}, không báo lỗi`,
      async run(qa) {
        const seen = watchReports(qa)
        const expectInvalid = async (slug) => {
          await qa.expect(qa.page.getByRole('alert').filter({ hasText: MSG463 })).toBeVisible()
          await qa.expect(exportButton(qa)).toBeDisabled()
          await qa.expect(region(qa, 'Theo trạng thái')).toHaveCount(0)
          await qa.expect(qa.page.locator('.page-header-desc')).toHaveCount(0)
          await settled(qa)
          await qa.shot(slug)
          await noSidewaysScroll(qa)
          await noRawCodes(qa)
        }
        await qa.goto('/mgmt/reports?from=2026-06&to=2026-01')
        await expectInvalid('bat-dau-sau-ket-thuc')
        await qa.goto(`/mgmt/reports?from=${addMonths(NOW_MONTH, -24)}&to=${NOW_MONTH}`)
        await expectInvalid('qua-24-thang')
        qa.expect(seen, `the page asked the API for an invalid period: ${JSON.stringify(seen.map((s) => [s.from, s.to]))}`).toEqual([])

        await qa.goto(`/mgmt/reports?from=${P6.from}&to=${P6.to}`)
        await lastReport(qa, seen, (x) => x.from === P6.from)
        await qa.expect(qa.page.locator('.page-header-desc')).toBeVisible()
        seen.length = 0
        await press(qa, qa.page.getByRole('button', { name: /^Từ tháng:/ }))
        await press(qa, qa.page.getByRole('listbox', { name: 'Từ tháng' }).getByRole('option', { name: `Tháng ${monthLabel(NOW_MONTH)}` }))
        await qa.expect(qa.page).toHaveURL(new RegExp(`from=${NOW_MONTH}&to=${P6.to}`))
        await expectInvalid('chon-tu-thang-sau-den-thang')
        await qa.page.waitForTimeout(500)
        qa.expect(seen, 'the page asked the API after an invalid choice').toEqual([])

        await qa.goto('/mgmt/reports?from=2026-13&to=abc')
        const rep = await lastReport(qa, seen, (x) => x.from === DEFAULT.from && x.to === DEFAULT.to)
        await figuresReady(qa)
        await qa.expect(qa.page.getByRole('alert').filter({ hasText: MSG463 })).toHaveCount(0)
        await qa.expect(qa.page.getByRole('button', { name: `Từ tháng: Tháng ${monthLabel(DEFAULT.from)}` })).toBeVisible()
        await expectScreenMatches(qa, rep)
        await settled(qa)
        await qa.shot('url-sai-dang-dung-mac-dinh')
      },
    },
    {
      id: 'AC8',
      role: 'member',
      title: 'API từ chối khoảng sai bằng 400 MSG463 ở cả xem và xuất (BR-433); 24 tháng vẫn được',
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
      title: 'Không có cuộc họp nào đọc được trong khoảng (AF-2): MSG466, "0 cuộc họp", không có chỉ số trung bình; vẫn xuất được tệp với số 0 (UC-70b AF-1)',
      steps: [
        `${REPORTER} (chỉ có cuộc họp tháng ${monthLabel(FAR_MONTH)}) mở Báo cáo với khoảng mặc định`,
        `Mở /mgmt/reports?from=${EMPTY.from}&to=${EMPTY.to}, bấm Xuất Excel`,
      ],
      expected: `API total 0, mọi nhóm 0, ba trung bình null; màn: "${MSG466}", dòng mô tả "0 cuộc họp", không có bảng chỉ số; Xuất Excel bật, tệp bao-cao-cuoc-hop_${EMPTY.from}_${EMPTY.to}.xlsx có tổng 0, mọi dòng 0, ba trung bình "Chưa có dữ liệu"; một dòng audit report.exported; không cuộn ngang`,
      async run(qa) {
        await prepareAll(qa)
        const seen = watchReports(qa)
        await qa.goto('/mgmt/reports')
        const def = await lastReport(qa, seen)
        qa.expect(def.total, `the reporter reads ${def.total} meetings in the default period`).toBe(0)
        await qa.expect(qa.page.getByText(MSG466)).toBeVisible()
        await qa.expect(qa.page.locator('.page-header-desc')).toHaveText('0 cuộc họp')
        await settled(qa)
        await qa.shot('mac-dinh-khong-co-cuoc-hop')

        await qa.goto(`/mgmt/reports?from=${EMPTY.from}&to=${EMPTY.to}`)
        const rep = await lastReport(qa, seen, (x) => x.from === EMPTY.from && x.to === EMPTY.to)
        qa.expect(problems(rep, EMPTY.from, EMPTY.to)).toEqual([])
        qa.expect(rep.total).toBe(0)
        qa.expect([rep.attendance.averageRate, rep.duration.averageMinutes, rep.minutesTurnaround.averageHours]).toEqual([null, null, null])
        await qa.expect(qa.page.getByText(MSG466)).toBeVisible()
        await qa.expect(qa.page.locator('.page-header-desc')).toHaveText('0 cuộc họp')
        await qa.expect(region(qa, 'Tổng quan')).toHaveCount(0)
        await qa.expect(exportButton(qa)).toBeEnabled()
        await settled(qa)
        await qa.shot('khoang-trong-msg466')
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
        if (qa.device === 'desktop') {
          const since = Date.now()
          const file = await downloadExport(qa)
          qa.expect(file.name).toBe(`bao-cao-cuoc-hop_${EMPTY.from}_${EMPTY.to}.xlsx`)
          const wrong = fileProblems(readXlsx(file.buf), rep)
          qa.expect(wrong, wrong.join('\n')).toEqual([])
          await expectAuditRow(qa, REPORTER, EMPTY.from, EMPTY.to, since)
        }
      },
    },

    // ------------------------------------------------------------------------- UC-70b export
    {
      id: 'AC10',
      role: REPORTER,
      title: `Xuất Excel từ màn (UC-70b): tệp mang đúng số trên màn tháng ${monthLabel(FAR_MONTH)}, nhãn tiếng Việt, số là số, không có tên cuộc họp; xuất được ghi audit`,
      steps: [`${REPORTER} mở /mgmt/reports?from=${FAR_MONTH}&to=${FAR_MONTH}`, 'Bấm Xuất Excel', 'Quản trị đọc nhật ký hệ thống, sự kiện report.exported'],
      expected: `Tệp bao-cao-cuoc-hop_${FAR_MONTH}_${FAR_MONTH}.xlsx, sheet "Báo cáo cuộc họp": khoảng "${monthLabel(FAR_MONTH)} – ${monthLabel(FAR_MONTH)}", thời điểm xuất (GMT+7), tổng; bảng Tháng, Trạng thái (8 nhãn), Mức độ mật (3 nhãn) và 3 dòng trung bình kèm số cuộc họp được tính / chưa có dữ liệu — đúng số API trang nhận; không có tiêu đề cuộc họp (BR-437); nhật ký có dòng "Xuất báo cáo cuộc họp" của tài khoản đó, objectType Report, details có from/to (BR-438)`,
      async run(qa) {
        await prepareAll(qa)
        const seen = watchReports(qa)
        await qa.goto(`/mgmt/reports?from=${FAR_MONTH}&to=${FAR_MONTH}`)
        const rep = await lastReport(qa, seen, (x) => x.from === FAR_MONTH)
        await figuresReady(qa)
        const since = Date.now()
        const file = await downloadExport(qa)
        await settled(qa)
        await qa.shot('sau-khi-xuat-excel')
        qa.expect(file.name).toBe(`bao-cao-cuoc-hop_${FAR_MONTH}_${FAR_MONTH}.xlsx`)
        const xlsx = readXlsx(file.buf)
        const titles = STATUSES.map((s) => made[s].title)
        const wrong = fileProblems(xlsx, rep, titles)
        // The figures were computed again at export time: someone may have added a meeting in between, so re-read once.
        if (wrong.length) {
          const fresh = await report(qa, REPORTER, FAR_MONTH, FAR_MONTH)
          const again = fileProblems(xlsx, fresh, titles)
          if (again.length < wrong.length) wrong.splice(0, wrong.length, ...again)
        }
        qa.note(`Tệp: ${file.name}, ${file.buf.length} byte, ${xlsx.rows.length} dòng có dữ liệu`)
        qa.expect(wrong, wrong.join('\n')).toEqual([])
        await expectAuditRow(qa, REPORTER, FAR_MONTH, FAR_MONTH, since)
      },
    },

    // ------------------------------------------------------------------------- EX-2 (server answers simulated in the browser)
    {
      id: 'AC11',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Lỗi máy chủ (giả lập câu trả lời trong trình duyệt): MSG464 kèm Thử lại; MSG465 là toast; MSG463 của server hiện dưới thanh chọn và khoá Xuất Excel',
      steps: [
        'Chặn GET /api/reports/meetings trả 500 {code MSG464}, mở Báo cáo',
        'Bỏ chặn, bấm Thử lại',
        'Chặn export trả 500 {code MSG465}, bấm Xuất Excel',
        'Chặn GET trả 400 {code MSG463} cho một khoảng đúng, mở khoảng đó',
      ],
      expected: `"${MSG464}" (role alert) với nút Thử lại, không có bảng; Thử lại → số liệu hiện; toast "${MSG465}", không tải tệp; "${MSG463}" dưới thanh chọn, nút Xuất Excel bị khoá. (Lỗi database thật không gây ra được ở đây: đây chỉ kiểm phần FE.)`,
      async run(qa) {
        const isReport = (url) => new URL(url).pathname === '/api/reports/meetings'
        const isExport = (url) => new URL(url).pathname === '/api/reports/meetings/export'
        const fail = (status, code, message) => (route) =>
          route.fulfill({ status, contentType: 'application/json', body: JSON.stringify({ code, message }) })
        await qa.page.route(isReport, fail(500, 'MSG464', MSG464))
        await qa.goto('/mgmt/reports')
        const alert = qa.page.getByRole('alert').filter({ hasText: MSG464 })
        await qa.expect(alert).toBeVisible()
        await qa.expect(region(qa, 'Theo trạng thái')).toHaveCount(0)
        await settled(qa)
        await qa.shot('msg464-thu-lai')
        await noSidewaysScroll(qa)
        await qa.page.unroute(isReport)
        await press(qa, alert.getByRole('button', { name: 'Thử lại' }))
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

        await qa.page.route(isReport, fail(400, 'MSG463', MSG463))
        await qa.goto('/mgmt/reports?from=2026-02&to=2026-04')
        await qa.expect(qa.page.getByRole('alert').filter({ hasText: MSG463 })).toBeVisible()
        await qa.expect(exportButton(qa)).toBeDisabled()
        await settled(qa)
        await qa.shot('msg463-tu-server')
        await noRawCodes(qa)
      },
    },
  ],
}

// Shared helpers of the System Test plans (Report 5.3 sys-wf-*.mjs and Report 5.4 nfr-*.mjs). A system test follows one
// business flow across features and users, so a plan keeps the records an earlier case made in a module-level `state`
// and the later cases build on them, in order (docs/08-reports/test-reports/cases/system/README.md).
//
// Every record a plan makes is titled "[SYS] …" (meetings, roles, accounts) so it can be found and told apart from the
// FE plans' "[QA] …" data. Accounts live on @trustcabinet.local, which the backend never mails
// (Email:UndeliverableDomains): an email is checked in the outbox table, where it is written with its subject and body.

import { execFileSync } from 'node:child_process'
import { crc32 } from 'node:zlib'

export const PASSWORD = process.env.QA_PASSWORD ?? 'TrustCabinet@2026'

/** Seed accounts the system tests act as (roles after ADR-0075). */
export const PEOPLE = {
  admin: 'quantri@trustcabinet.local', // Quản trị viên (AdminUsers, VoiceCollector, MeetingCreator)
  approver: 'chutoa@trustcabinet.local', // Phê duyệt viên — "Trần Chủ Toạ"
  approver2: 'thuky@trustcabinet.local', // Phê duyệt viên — "Nguyễn Thư Ký"
  organizer: 'thanhvien@trustcabinet.local', // Thành viên — "Lê Thành Viên"
  memberA: 'duc.hoang@trustcabinet.local', // Thành viên — "Hoàng Minh Đức"
  memberB: 'lan.vu@trustcabinet.local', // Thành viên — "Vũ Thị Lan"
  memberC: 'bao.dang@trustcabinet.local', // Thành viên — "Đặng Quốc Bảo"
  memberD: 'mai.phan@trustcabinet.local', // Thành viên — "Phan Ngọc Mai" (outsider in isolation tests)
  memberE: 'son.ngo@trustcabinet.local', // Thành viên — "Ngô Thanh Sơn"
}

export const RUN = Date.now().toString(36).slice(-5)
export const rand = () => Math.random().toString(36).slice(2, 6)
export const iso = (ms) => new Date(ms).toISOString().replace(/\.\d{3}Z$/, 'Z')
export const sysName = (text) => `[SYS] ${text}`

const PEOPLE_CACHE = {}
/** id, full name and email of a person (key of PEOPLE or an email), read once per run. */
export async function person(qa, who) {
  const email = PEOPLE[who] ?? who
  if (!PEOPLE_CACHE[email]) {
    const me = (await (await client(qa, email)).get('/api/auth/me')).body
    PEOPLE_CACHE[email] = { id: me.id, name: me.fullName, email: me.email, role: me.roleName }
  }
  return PEOPLE_CACHE[email]
}

/** One signed-in API client per person and criterion. */
export async function client(qa, who) {
  const email = PEOPLE[who] ?? who
  qa.__clients ??= {}
  qa.__clients[email] ??= await qa.api(email)
  return qa.__clients[email]
}

/** Signs the criterion's browser in as a person (key of PEOPLE or an email). */
export async function as(qa, who) {
  await qa.signIn(PEOPLE[who] ?? who)
}

/**
 * A second browser (its own context, cookies and screen) signed in as `who`, for the steps where two users act at once.
 * It is closed by `closeSecond`.
 */
export async function secondBrowser(qa, who, { phone = false, password = PASSWORD, base = qa.base, signIn = true } = {}) {
  const browser = qa.context.browser()
  const ctx = await browser.newContext({
    baseURL: base,
    locale: 'vi-VN',
    timezoneId: 'Asia/Ho_Chi_Minh',
    viewport: phone ? { width: 390, height: 844 } : { width: 1440, height: 900 },
    deviceScaleFactor: 2,
    ...(phone ? { isMobile: true, hasTouch: true } : {}),
    permissions: ['microphone', 'camera'],
  })
  const email = PEOPLE[who] ?? who
  if (signIn) {
    const res = await ctx.request.post(`${base}/api/auth/login`, { data: { email, password } })
    if (!res.ok()) throw new Error(`sign-in as ${email} failed: HTTP ${res.status()}`)
    await ctx.addInitScript(() => localStorage.setItem('trustcabinet.signedIn', '1'))
  }
  const page = await ctx.newPage()
  qa.__seconds ??= []
  qa.__seconds.push(ctx)
  return page
}

/** Saves a screenshot of a second browser into the run folder with the runner's numbering. */
export async function shotOf(qa, page, slug, who = 'second') {
  return qa.shot(slug, { page, who })
}

export async function closeSeconds(qa) {
  for (const ctx of qa.__seconds ?? []) await ctx.close().catch(() => undefined)
  qa.__seconds = []
}

/** Runs SQL on the local database (read-only checks: outbox, audit). Returns rows as arrays of strings. */
export function sql(query) {
  const out = execFileSync(
    'docker',
    ['exec', 'trustcabinet-postgres', 'psql', '-U', 'trustcabinet', '-d', 'trustcabinet', '-At', '-F', '\t', '-c', query],
    { encoding: 'utf8' },
  )
  return out
    .split('\n')
    .filter((l) => l.length)
    .map((l) => l.split('\t'))
}

/** Emails written to the outbox for a meeting since `sinceIso`: [{ to, subject, html }]. */
export function emailsFor(meetingId, sinceIso = '1970-01-01') {
  return sql(
    `select to_address, subject, coalesce(html_body, body), (position('/guest/' in coalesce(html_body, body)) > 0)::text from email_messages where meeting_id = '${meetingId}' and created_at >= '${sinceIso}' order by created_at`,
  ).map(([to, subject, html, guestLink]) => ({ to, subject, html, guestLink: guestLink === 'true' }))
}

/** Emails written to the outbox to an address since `sinceIso`. */
export function emailsTo(address, sinceIso = '1970-01-01') {
  return sql(
    `select subject, coalesce(left(html_body, 4000), left(body, 4000)), meeting_id from email_messages where to_address = '${address}' and created_at >= '${sinceIso}' order by created_at`,
  ).map(([subject, html, meetingId]) => ({ subject, html, meetingId }))
}

export const nowIso = () => new Date().toISOString()

// ---------------------------------------------------------------------------------------------------------------------
// Guest by email. A guest's one-time code is mailed straight over SMTP and only its hash is stored, so these helpers
// need the backend's SMTP pointed at the local Mailpit (Email__Smtp__Host=127.0.0.1, Email__Smtp__Port=1025,
// Email__Smtp__Security=None, empty Username, Email__UndeliverableDomains__0=undeliverable.invalid). Nothing leaves the
// machine: Mailpit keeps every message it receives.

export const MAILPIT = process.env.QA_MAILPIT ?? 'http://127.0.0.1:8025'

/** The first match of `pattern` in the newest mail to `to` since `sinceMs`, read from Mailpit (the recipient's mailbox). */
export async function mailedMatch(to, sinceMs, pattern, timeoutMs = 45_000) {
  const end = Date.now() + timeoutMs
  while (Date.now() < end) {
    const res = await fetch(`${MAILPIT}/api/v1/search?query=${encodeURIComponent(`to:"${to}"`)}&limit=10`)
    for (const m of (await res.json()).messages ?? []) {
      if (Date.parse(m.Created) < sinceMs - 2000) continue
      const msg = await (await fetch(`${MAILPIT}/api/v1/message/${m.ID}`)).json()
      const hit = pattern.exec(`${msg.Text ?? ''}
${msg.HTML ?? ''}`)
      if (hit) return { match: hit[1] ?? hit[0], subject: m.Subject, id: m.ID, view: `${MAILPIT}/view/${m.ID}.html` }
    }
    await new Promise((r) => setTimeout(r, 1500))
  }
  throw new Error(`nothing matching ${pattern} mailed to ${to} (is the backend's SMTP pointed at Mailpit?)`)
}

/** The newest 6-digit code mailed to `to` since `sinceMs`, read from Mailpit the way the guest reads their mailbox. */
export async function mailedCode(to, sinceMs, timeoutMs = 45_000) {
  const hit = await mailedMatch(to, sinceMs, /(?<!\d)(\d{6})(?!\d)/, timeoutMs)
  return { code: hit.match, subject: hit.subject, id: hit.id }
}

/** The guest link of an invitation, read from the invitation email in the outbox (waits for the mail worker). */
export async function guestLink(meetingId, email, timeoutMs = 45_000) {
  const end = Date.now() + timeoutMs
  for (;;) {
    const link = guestLinkNow(meetingId, email)
    if (link || Date.now() > end) return link
    await new Promise((r) => setTimeout(r, 1500))
  }
}
function guestLinkNow(meetingId, email) {
  return sql(
    `select substring(coalesce(html_body, body) from '(/guest/[0-9a-f-]+#token=[A-Za-z0-9_-]+)') from email_messages where to_address = '${email}' and meeting_id = '${meetingId}' order by created_at desc limit 1`,
  )[0]?.[0]
}

/**
 * Opens the guest link in a fresh browser, enters the mailed code and declares the display name. `onCodeScreen` and
 * `onProfile` run on those screens (for the shots). Returns the guest page and the guest's access token.
 */
export async function guestSession(qa, path, { email, name, onCodeScreen, onProfile } = {}) {
  const guest = await secondBrowser(qa, 'anon', { signIn: false })
  const since = Date.now()
  await guest.goto(path)
  await qa.expect(guest.getByText(/Nhập mã 6 số đã gửi tới/)).toBeVisible({ timeout: 20_000 })
  if (onCodeScreen) await onCodeScreen(guest)
  const { code } = await mailedCode(email, since)
  await guest.getByRole('textbox', { name: 'Chữ số 1' }).fill(code)
  await guest.getByRole('button', { name: 'Xác minh', exact: true }).click()
  const nameBox = guest.getByLabel('Tên hiển thị')
  await qa.expect(nameBox).toBeVisible({ timeout: 20_000 })
  await nameBox.fill(name)
  if (onProfile) await onProfile(guest)
  await guest.getByRole('button', { name: 'Vào cuộc họp', exact: true }).click()
  await qa.expect(guest.getByRole('link', { name: /Vào phòng họp/ }).or(guest.getByRole('tablist', { name: 'Cuộc họp' })).first()).toBeVisible({ timeout: 20_000 })
  const token = await guest.evaluate(() => {
    const all = JSON.parse(localStorage.getItem('trustcabinet.guest') ?? '{}')
    return Object.values(all).find((e) => e?.accessToken)?.accessToken ?? null
  })
  return { guest, token, code }
}

/** A Published meeting (precondition) with an agenda item and a guest invited by email; returns the meeting. */
export async function guestInvitedMeeting(qa, { title, guestName, guestEmail, status = 'Published' }) {
  const m = await meetingAt(qa, 'Draft', { title })
  const sec = await client(qa, 'memberB')
  const roster = await sec.get(`/api/meetings/${m.id}/participants?pageSize=100`)
  const inv = await fetch(`${qa.base}/api/meetings/${m.id}/guest-invitations`, {
    method: 'POST',
    headers: { Authorization: `Bearer ${await userToken(qa.base, PEOPLE.memberB)}`, 'Content-Type': 'application/json', 'If-Match': roster.headers.etag },
    body: JSON.stringify({ invitedName: guestName, email: guestEmail }),
  })
  qa.expect(inv.status, `guest invitation: ${await inv.text()}`).toBe(201)
  const approverId = (await person(qa, 'approver')).id
  const steps = [
    ['organizer', { action: 'SubmitForApproval', assignedApproverId: approverId }],
    ['approver', { action: 'Approve' }],
    ['organizer', { action: 'Publish' }],
    ...(status === 'InProgress' ? [['memberA', { action: 'Start' }]] : []),
  ]
  for (const [who, action] of steps) qa.expectStatus(await (await client(qa, who)).post(`/api/meetings/${m.id}/transitions`, action), 200)
  await guestLink(m.id, guestEmail)
  return (await (await client(qa, 'organizer')).get(`/api/meetings/${m.id}`)).body
}

const USER_TOKENS = {}
/** An access token of a seeded person, for calls that need a header the API client does not send (If-Match). */
export async function userToken(base, email) {
  if (!USER_TOKENS[email]) {
    const r = await fetch(`${base}/api/auth/login`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ email, password: PASSWORD }) })
    USER_TOKENS[email] = (await r.json()).accessToken
  }
  return USER_TOKENS[email]
}

/** A plain HTTP call with a bearer token (the guest's), returning status and parsed body. */
export async function bearerCall(base, token, method, path, body) {
  const res = await fetch(`${base}${path}`, {
    method,
    headers: { ...(token ? { Authorization: `Bearer ${token}` } : {}), ...(body ? { 'Content-Type': 'application/json' } : {}) },
    body: body ? JSON.stringify(body) : undefined,
  })
  const text = await res.text()
  let json = null
  try {
    json = text ? JSON.parse(text) : null
  } catch {
    json = text.slice(0, 200)
  }
  return { status: res.status, body: json }
}

/** Runs every check, then fails once with all the wrong ones. */
export function checker(qa) {
  const wrong = []
  return {
    status(label, res, status, code) {
      const ok = res.status === status && (code === undefined || res.body?.code === code)
      if (!ok) wrong.push(`${label}: want ${status}${code ? ` ${code}` : ''}, got ${res.status} ${JSON.stringify(res.body).slice(0, 200)}`)
      return ok
    },
    fact(label, ok, detail = '') {
      if (!ok) wrong.push(`${label}${detail ? `: ${detail}` : ''}`)
      return ok
    },
    done() {
      qa.expect(wrong, wrong.join('\n')).toEqual([])
    },
  }
}

// ---------------------------------------------------------------------------------------------------------------------
// Screen helpers.

export async function noSidewaysScroll(qa, page = qa.page) {
  const sideways = await page.evaluate(() => document.documentElement.scrollWidth - innerWidth)
  qa.expect(sideways, 'the page scrolls sideways').toBeLessThanOrEqual(0)
}
export async function noRawCodes(qa, page = qa.page) {
  const text = await page.locator('body').innerText()
  qa.expect(text, 'a raw MSG code is shown to the user').not.toMatch(/MSG\d+/)
}
export async function settled(page) {
  await page
    .waitForFunction(() =>
      document.getAnimations().every((a) => a.playState !== 'running' || a.effect?.getTiming().iterations === Infinity),
    )
    .catch(() => undefined)
}
export const toast = (page, text) => page.locator('.toast-stack, [role="status"]').getByText(text).first()
export const dialog = (page, name) => page.getByRole('dialog', { name })

/** Opens a meeting page and waits for its title. */
export async function openMeeting(qa, page, meeting, tab) {
  await page.goto(`/mgmt/meetings/${meeting.id}${tab ? `#tab=${encodeURIComponent(tab)}` : ''}`)
  await page.waitForLoadState('networkidle', { timeout: 10_000 }).catch(() => undefined)
  await qa.expect(page.getByRole('heading', { level: 1, name: meeting.title })).toBeVisible()
  if (tab) await openTab(qa, page, tab)
}
export async function openTab(qa, page, name) {
  const tab = page.getByRole('tab', { name: new RegExp(`^${name}`) }).first()
  await tab.click()
  await page.waitForLoadState('networkidle', { timeout: 10_000 }).catch(() => undefined)
}
export async function expectStatusChip(qa, page, label) {
  await qa.expect(page.locator('.record-title').getByText(label, { exact: true })).toBeVisible()
}

// ---------------------------------------------------------------------------------------------------------------------
// Meetings through the API (preconditions only; the steps under test run on the screen).

let slotCounter = 0
/** A slot `daysAhead` days ahead at hour `hour` (Vietnam time), stepped so two meetings of a run never share a time. */
export function slot(daysAhead = 2, hour = 9, minutes = 60) {
  slotCounter += 1
  const d = new Date(Date.now() + daysAhead * 86_400_000 + 7 * 3_600_000)
  const day = d.toISOString().slice(0, 10)
  const start = Date.parse(`${day}T${String(hour).padStart(2, '0')}:00:00+07:00`) + (slotCounter % 8) * 5 * 60_000
  return { plannedStartTime: iso(start), plannedEndTime: iso(start + minutes * 60_000) }
}

/** Creates a meeting organised by `organizer` with a roster [{ who, role }] and drives it to `status`. */
export async function meetingAt(qa, status, { title, organizer = 'organizer', approver = 'approver', roster, over = {} } = {}) {
  const parts = []
  for (const r of roster ?? [
    { who: 'memberA', role: 'Chair' },
    { who: 'memberB', role: 'Secretary' },
    { who: 'memberC', role: 'Member' },
  ])
    parts.push({ userId: (await person(qa, r.who)).id, role: r.role })
  const body = {
    title: title ?? sysName(`${status} ${rand()}`),
    ...slot(),
    mode: 'InPerson',
    location: 'Phòng họp số 1',
    onlineLink: null,
    sensitivity: 'Normal',
    description: 'Cuộc họp do bộ kiểm thử hệ thống tạo.',
    participants: parts,
    ...over,
  }
  const org = await client(qa, organizer)
  const created = await org.post('/api/meetings', body)
  qa.expectStatus(created, 201)
  const id = created.body.meeting.id
  const chair = (roster ?? [{ who: 'memberA', role: 'Chair' }]).find((r) => r.role === 'Chair')?.who ?? 'memberA'
  const path = {
    Draft: [],
    WaitingApproval: ['submit'],
    Approved: ['submit', 'approve'],
    Published: ['submit', 'approve', 'publish'],
    InProgress: ['submit', 'approve', 'publish', 'start'],
    Ended: ['submit', 'approve', 'publish', 'start', 'end'],
  }[status]
  for (const step of path) {
    const [who, action] = {
      submit: [organizer, { action: 'SubmitForApproval', assignedApproverId: (await person(qa, approver)).id }],
      approve: [approver, { action: 'Approve' }],
      publish: [organizer, { action: 'Publish' }],
      start: [chair, { action: 'Start' }],
      end: [chair, { action: 'End' }],
    }[step]
    qa.expectStatus(await (await client(qa, who)).post(`/api/meetings/${id}/transitions`, action), 200)
  }
  return (await org.get(`/api/meetings/${id}`)).body
}

/** Notifications of a person, newest first. */
export async function notificationsOf(qa, who) {
  const res = await (await client(qa, who)).get('/api/notifications?pageSize=50')
  qa.expectStatus(res, 200)
  return res.body.items ?? res.body
}

// ---------------------------------------------------------------------------------------------------------------------
// Files (copied from fe-07.mjs): a small PDF with real pages and a minimal DOCX, built here, never real documents.

/** A valid PDF with `pages` pages of Helvetica text, correct xref offsets (the server opens it strictly, ADR-0044). */
export function makePdf(pages = 3, label = 'QA FE-07') {
  const objs = ['<< /Type /Catalog /Pages 2 0 R >>']
  const kids = Array.from({ length: pages }, (_, i) => `${4 + i * 2} 0 R`)
  objs.push(`<< /Type /Pages /Kids [${kids.join(' ')}] /Count ${pages} >>`)
  objs.push('<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>')
  for (let i = 0; i < pages; i++) {
    const text = `BT /F1 28 Tf 72 700 Td (${label} - trang ${i + 1}) Tj ET`
    objs.push(`<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 3 0 R >> >> /Contents ${5 + i * 2} 0 R >>`)
    objs.push(`<< /Length ${text.length} >>\nstream\n${text}\nendstream`)
  }
  let out = '%PDF-1.4\n'
  const offsets = []
  objs.forEach((o, i) => {
    offsets.push(out.length)
    out += `${i + 1} 0 obj\n${o}\nendobj\n`
  })
  const xref = out.length
  out += `xref\n0 ${objs.length + 1}\n0000000000 65535 f \n`
  for (const off of offsets) out += `${String(off).padStart(10, '0')} 00000 n \n`
  out += `trailer\n<< /Size ${objs.length + 1} /Root 1 0 R >>\nstartxref\n${xref}\n%%EOF\n`
  return Buffer.from(out, 'latin1')
}

/** A stored (uncompressed) zip. */
function zip(entries) {
  const locals = []
  const centrals = []
  let offset = 0
  for (const [name, content] of entries) {
    const data = Buffer.from(content, 'utf8')
    const nameBuf = Buffer.from(name)
    const crc = crc32(data) >>> 0
    const lh = Buffer.alloc(30)
    lh.writeUInt32LE(0x04034b50, 0)
    lh.writeUInt16LE(20, 4)
    lh.writeUInt16LE(0x21, 12)
    lh.writeUInt32LE(crc, 14)
    lh.writeUInt32LE(data.length, 18)
    lh.writeUInt32LE(data.length, 22)
    lh.writeUInt16LE(nameBuf.length, 26)
    locals.push(lh, nameBuf, data)
    const ch = Buffer.alloc(46)
    ch.writeUInt32LE(0x02014b50, 0)
    ch.writeUInt16LE(20, 4)
    ch.writeUInt16LE(20, 6)
    ch.writeUInt16LE(0x21, 14)
    ch.writeUInt32LE(crc, 16)
    ch.writeUInt32LE(data.length, 20)
    ch.writeUInt32LE(data.length, 24)
    ch.writeUInt16LE(nameBuf.length, 28)
    ch.writeUInt32LE(offset, 42)
    centrals.push(ch, nameBuf)
    offset += 30 + nameBuf.length + data.length
  }
  const cd = Buffer.concat(centrals)
  const end = Buffer.alloc(22)
  end.writeUInt32LE(0x06054b50, 0)
  end.writeUInt16LE(entries.length, 8)
  end.writeUInt16LE(entries.length, 10)
  end.writeUInt32LE(cd.length, 12)
  end.writeUInt32LE(offset, 16)
  return Buffer.concat([...locals, cd, end])
}

/** A minimal Word document: content types, the officeDocument relationship and one paragraph. */
export function makeDocx(text = 'QA FE-07 phụ lục') {
  return zip([
    [
      '[Content_Types].xml',
      '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/></Types>',
    ],
    [
      '_rels/.rels',
      '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/></Relationships>',
    ],
    [
      'word/document.xml',
      `<?xml version="1.0" encoding="UTF-8" standalone="yes"?><w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body><w:p><w:r><w:t>${text}</w:t></w:r></w:p></w:body></w:document>`,
    ],
  ])
}

const PDF_TYPE = 'application/pdf'
const DOCX_TYPE = 'application/vnd.openxmlformats-officedocument.wordprocessingml.document'
export const pdfFile = (name, pages = 3) => ({ name, mimeType: PDF_TYPE, buffer: makePdf(pages, name.replace(/[^A-Za-z0-9 -]/g, '')) })
export const docxFile = (name) => ({ name, mimeType: DOCX_TYPE, buffer: makeDocx(name.replace(/[<>&]/g, '')) })

// ---------------------------------------------------------------------------------------------------------------------
// Notification bell (selectors from fe-13.mjs).

export const bell = (page) => page.locator('button.notif-bell')
export const notifPanel = (page) => page.locator('section.notif-panel')
export const notifRows = (page) => notifPanel(page).locator('li.notif-item')

/** Opens the bell panel on a page that is already showing the shell. */
export async function openBell(qa, page) {
  await qa.expect(bell(page)).toBeVisible()
  await bell(page).click()
  await qa.expect(notifPanel(page)).toBeVisible()
  await qa.expect(notifPanel(page).locator('.notif-skeleton')).toHaveCount(0)
}

/** The bell's unread count, read from its label ("Thông báo, N chưa đọc"). */
export async function unread(page) {
  const label = (await bell(page).getAttribute('aria-label')) ?? ''
  const m = /(\d+) chưa đọc/.exec(label)
  return m ? Number(m[1]) : 0
}

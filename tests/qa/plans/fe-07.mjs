// FE-07 — Kho tài liệu cá nhân & Tài liệu cuộc họp (QA subtask SCRUM-194): UC-37 view the library (search, type
// filter), UC-39 upload to the library, UC-40a–c delete / rename / move a document, UC-41a–d folders, UC-35a view the
// meeting documents, UC-35b the viewer and download, UC-36a add to the meeting (computer, My Documents), UC-36b attach
// to an agenda item, UC-36c remove from the meeting, UC-36d detach from an item, UC-38 comments, UC-37a the
// administrator reading a locked account's library; with the rules they cite (BR-055, BR-068, BR-070, BR-071, BR-074–BR-076, BR-069, BR-081, BR-086, BR-087, BR-091, BR-072, BR-073, BR-011, BR-077, BR-083, BR-084,
// BR-083, BR-082, BR-079, BR-080, BR-078, BR-074–BR-076, BR-086, BR-087, BR-028, BR-085, BR-011).
//
// Criteria come from docs/01-requirements/05-use-case-specifications.md, 06-business-rules.md,
// docs/03-api/11-tai-lieu-cuoc-hop.md, 14-kho-tai-lieu-ca-nhan.md, the message catalogue, and the comments of
// SCRUM-21/23/58/107/110/111/112/113/123/124. Deviations taken from there: uploads go through a browser queue, one
// request per file (ADR-0033), so "all or nothing" is per file and the tray says "Đã tải lên n tệp" instead of a
// MSG177/MSG161 toast; the storage limit is 250 GB for the whole system (ADR-0034, ADR-0084); a library document already in the
// meeting is locked with "Đã có trong cuộc họp" in the general-group dialog only (SCRUM-124); the Secretary's list of
// comments (SCRUM-106) was dropped by the PO; UC-42 (read confirmation) belongs to voting (FE-11) and is not tested.
// A Guest reader (UC-04, an OTP by email) is not tested here.
//
// Safe on a shared local dev database: every meeting, folder and file is named "[QA] FE-07 …"; meetings are created
// here through the API, eight weeks ahead, organised by thuky@ (Secretary) with chutoa@ as Chair and approver and
// thanhvien@ as Member, and reused across runs by their fixed title. Files are tiny PDFs and DOCX built in this file.
// Every criterion sweeps the "[QA] FE-07" files and folders it left in the personal libraries (the meeting copies stay,
// BR-074–BR-076). UC-37a needs a locked account: one "[QA] FE-07 kho đã khoá" account is created (random password, the
// undeliverable trustcabinet.local domain), given a file and a folder, deactivated, and reused by later runs.

import { request as playwrightRequest } from '@playwright/test'
import { randomBytes } from 'node:crypto'
import { crc32 } from 'node:zlib'

// ---------------------------------------------------------------------------------------------------------------------
// Files: a few-hundred-byte PDF with real pages and a minimal DOCX, built here, never real documents.

/** A valid PDF with `pages` pages of Helvetica text, correct xref offsets (the server opens it strictly, ADR-0044). */
function makePdf(pages = 3, label = 'QA FE-07') {
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
function makeDocx(text = 'QA FE-07 phụ lục') {
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
const pdfFile = (name, pages = 3) => ({ name, mimeType: PDF_TYPE, buffer: makePdf(pages, name.replace(/[^A-Za-z0-9 -]/g, '')) })
const docxFile = (name) => ({ name, mimeType: DOCX_TYPE, buffer: makeDocx(name.replace(/[<>&]/g, '')) })
const textFile = (name) => ({ name, mimeType: 'text/plain', buffer: Buffer.from('QA FE-07: tệp văn bản thường, không phải PDF hay DOCX.\n') })

// ---------------------------------------------------------------------------------------------------------------------
// Accounts, API clients, uploads.

const USERS = {}
async function users(qa) {
  for (const role of ['admin', 'chair', 'secretary', 'member']) {
    if (USERS[role]) continue
    const me = (await (await client(qa, role)).get('/api/auth/me')).body
    USERS[role] = { id: me.id, name: me.fullName, email: me.email }
  }
  return USERS
}

/** One signed-in JSON API client per role and criterion. */
async function client(qa, role) {
  qa.__clients ??= {}
  qa.__clients[role] ??= await qa.api(role)
  return qa.__clients[role]
}

/**
 * A bearer token of `role`, for multipart uploads (the runner's API client sends JSON only). The criterion's browser
 * context signs in as `role` for a moment and trades its refresh cookie, then signs back in as the criterion's role.
 * Called before the page is opened.
 */
async function tokenOf(qa, role) {
  qa.__tokens ??= {}
  if (qa.__tokens[role]) return qa.__tokens[role]
  const back = qa.role
  if (back !== role) await qa.signIn(role)
  const res = await qa.context.request.post('/api/auth/refresh')
  if (!res.ok()) throw new Error(`refresh as ${role}: HTTP ${res.status()}`)
  qa.__tokens[role] = (await res.json()).accessToken
  if (back !== role) await qa.signIn(back)
  return qa.__tokens[role]
}

/** Sends `files` as multipart part `files` to `path` with a bearer token; returns `{ status, body }`. */
async function sendFiles(qa, token, path, files) {
  const form = new FormData()
  for (const f of files) form.append('files', new File([f.buffer], f.name, { type: f.mimeType }))
  const res = await qa.context.request.post(path, {
    headers: token ? { Authorization: `Bearer ${token}` } : {},
    multipart: form,
    timeout: 60_000,
  })
  const text = await res.text()
  let body
  try {
    body = text ? JSON.parse(text) : undefined
  } catch {
    body = text
  }
  return { status: res.status(), body }
}
const upload = async (qa, role, path, files) => sendFiles(qa, role === 'anon' ? null : await tokenOf(qa, role), path, files)

/** Uploads to `role`'s library (root or `folderId`) and returns the new library documents. */
async function libraryUpload(qa, role, files, folderId = null) {
  const res = await upload(qa, role, `/api/documents/upload${folderId ? `?folderId=${folderId}` : ''}`, files)
  if (res.status === 503) qa.inconclusive(`Kho tệp không nhận tệp (503 ${res.body?.code}): SeaweedFS hoặc FileStorage:* chưa sẵn sàng.`)
  qa.expectStatus(res, 201)
  return res.body.documents
}

/** Uploads to a meeting's general group (or an agenda item) as `role`; returns the new meeting documents. */
async function meetingUpload(qa, role, meetingId, files, itemId = null) {
  const path = itemId ? `/api/meetings/${meetingId}/agenda/${itemId}/documents/upload` : `/api/meetings/${meetingId}/documents/upload`
  const res = await upload(qa, role, path, files)
  if (res.status === 503) qa.inconclusive(`Kho tệp không nhận tệp (503 ${res.body?.code}): SeaweedFS hoặc FileStorage:* chưa sẵn sàng.`)
  qa.expectStatus(res, itemId ? 200 : 201)
  return res.body.documents
}

async function createFolder(qa, role, name, parentId = null) {
  const res = await (await client(qa, role)).post('/api/folders', { name, parentId })
  qa.expectStatus(res, 201)
  return res.body.folder
}

/** `role`'s library at `folderId` (root when null). */
async function library(qa, role, folderId = null, extra = '') {
  const res = await (await client(qa, role)).get(`/api/documents?${folderId ? `folderId=${folderId}&` : ''}${extra}`)
  qa.expectStatus(res, 200)
  return res.body
}

/** Every "[QA] FE-07" document (in any folder) and folder of `role`'s library is deleted, deepest folder first. */
async function sweepLibrary(qa, role) {
  const c = await client(qa, role)
  const root = await library(qa, role)
  const places = [null, ...root.folders.map((f) => f.id)]
  for (const place of places) {
    const view = place === null ? root : (await c.get(`/api/documents?folderId=${place}`)).body
    for (const doc of view?.documents ?? []) if (doc.fileName.startsWith('[QA] FE-07')) await c.delete(`/api/documents/${doc.id}`)
  }
  const depth = (f) => (f.parentId ? 1 + depth(root.folders.find((p) => p.id === f.parentId) ?? {}) : 1)
  const mine = root.folders.filter((f) => f.name.startsWith('[QA] FE-07')).sort((a, b) => depth(b) - depth(a))
  for (const f of mine) await c.delete(`/api/folders/${f.id}`)
}

// ---------------------------------------------------------------------------------------------------------------------
// Meetings.

const rand = () => Math.random().toString(36).slice(2, 6)
const iso = (ms) => new Date(ms).toISOString().replace(/\.\d{3}Z$/, 'Z')
const MINUTE = 60_000
const DAY = 86_400_000
let slotCounter = Math.floor(Math.random() * 9)
/** A 45-minute slot on the day eight weeks from today, between 08:00 and 17:00 Vietnam time, varied per meeting. */
function slot() {
  slotCounter += 1
  const day = new Date(Date.now() + 56 * DAY)
  day.setUTCHours(1, 0, 0, 0) // 08:00 in Vietnam
  const start = day.getTime() + ((slotCounter * 55) % 540) * MINUTE + Math.floor(Math.random() * 4) * 5 * MINUTE
  return { plannedStartTime: iso(start), plannedEndTime: iso(start + 45 * MINUTE) }
}

async function meetingBody(qa, title, sensitivity = 'Normal') {
  const u = await users(qa)
  return {
    title,
    ...slot(),
    mode: 'InPerson',
    location: 'Phòng họp QA',
    onlineLink: null,
    sensitivity,
    description: 'Cuộc họp do bộ kiểm thử QA FE-07 tạo.',
    participants: [
      { userId: u.chair.id, role: 'Chair' },
      { userId: u.secretary.id, role: 'Secretary' },
      { userId: u.member.id, role: 'Member' },
    ],
  }
}

const PATHS = {
  Draft: [],
  Published: ['submit', 'approve', 'publish'],
  InProgress: ['submit', 'approve', 'publish', 'start'],
  Ended: ['submit', 'approve', 'publish', 'start', 'end'],
  Cancelled: ['cancel'],
}

async function transition(qa, role, id, action) {
  const u = await users(qa)
  const body = {
    submit: { action: 'SubmitForApproval', assignedApproverId: u.chair.id },
    approve: { action: 'Approve' },
    publish: { action: 'Publish' },
    start: { action: 'Start' },
    end: { action: 'End' },
    cancel: { action: 'Cancel', reason: 'QA FE-07: xếp lịch sau.' },
  }[action]
  const res = await (await client(qa, role)).post(`/api/meetings/${id}/transitions`, body)
  qa.expectStatus(res, 200)
}
const actorOf = (step, organizer) => (['approve', 'start', 'end'].includes(step) ? 'chair' : organizer)

async function addItem(qa, meetingId, title) {
  const res = await (await client(qa, 'secretary')).post(`/api/meetings/${meetingId}/agenda`, {
    title,
    description: '',
    presenterId: null,
    presenterGuestInvitationId: null,
  })
  qa.expectStatus(res, 201)
  return res.body.agendaItemId
}

async function docsOf(qa, role, meetingId) {
  const res = await (await client(qa, role)).get(`/api/meetings/${meetingId}/documents`)
  qa.expectStatus(res, 200)
  return res.body
}
const everyDoc = (list) => [...list.agendaItems.flatMap((i) => i.documents), ...list.general, ...list.votes.flatMap((v) => v.documents)]

/**
 * The meetings reused across runs, found by their fixed title and built the first time:
 * - Draft: two agenda items, no documents (criteria add and remove their own);
 * - Published: item 1 holds "Tờ trình.pdf" (3 pages), the general group "Phụ lục.docx";
 * - Secret: Published, Secret, one PDF;
 * - Ended: one PDF with a comment, one agenda item;
 * - Cancelled: one PDF, cancelled from Draft;
 * - AdminOrg: Published, organised by quantri@ (neither Chair nor Secretary), one PDF uploaded by the Secretary.
 */
const SPECS = {
  Draft: { status: 'Draft', organizer: 'secretary', sensitivity: 'Normal' },
  Published: { status: 'Published', organizer: 'secretary', sensitivity: 'Normal' },
  Secret: { status: 'Published', organizer: 'secretary', sensitivity: 'Secret' },
  Ended: { status: 'Ended', organizer: 'secretary', sensitivity: 'Normal' },
  Cancelled: { status: 'Cancelled', organizer: 'secretary', sensitivity: 'Normal' },
  AdminOrg: { status: 'Published', organizer: 'admin', sensitivity: 'Normal' },
}
const TITLES = {
  Draft: '[QA] FE-07 dùng lại Nháp',
  Published: '[QA] FE-07 dùng lại Đã công bố',
  Secret: '[QA] FE-07 dùng lại Mật',
  Ended: '[QA] FE-07 dùng lại Đã kết thúc',
  Cancelled: '[QA] FE-07 dùng lại Đã huỷ',
  AdminOrg: '[QA] FE-07 dùng lại (quản trị tổ chức)',
}
const PUB_PDF = '[QA] FE-07 Tờ trình.pdf'
const PUB_DOCX = '[QA] FE-07 Phụ lục.docx'

const REUSED = {}
async function reused(qa, key) {
  if (REUSED[key]) return REUSED[key]
  const spec = SPECS[key]
  const title = TITLES[key]
  const c = await client(qa, spec.organizer)
  const list = await c.get(`/api/meetings?q=${encodeURIComponent(title)}&status=${spec.status}&pageSize=20`)
  const hit = (list.body.items ?? []).find((m) => m.title === title)
  let id = hit?.id
  if (!id) id = await build(qa, key, title)
  const meeting = (await c.get(`/api/meetings/${id}`)).body
  const agenda = (await (await client(qa, 'secretary')).get(`/api/meetings/${id}/agenda`)).body
  REUSED[key] = { ...meeting, items: agenda.items ?? [] }
  return REUSED[key]
}

async function build(qa, key, title) {
  const spec = SPECS[key]
  const c = await client(qa, spec.organizer)
  const created = await c.post('/api/meetings', await meetingBody(qa, title, spec.sensitivity))
  qa.expectStatus(created, 201)
  const id = created.body.meeting.id
  qa.note(`Tạo cuộc họp dùng lại: ${title} (${id})`)
  const steps = PATHS[spec.status]
  if (key === 'Draft') {
    await addItem(qa, id, 'Báo cáo tình hình quý IV')
    await addItem(qa, id, 'Kế hoạch năm 2027')
  } else if (key === 'Published') {
    const item = await addItem(qa, id, 'Tờ trình dự toán')
    await addItem(qa, id, 'Ý kiến các phòng')
    await meetingUpload(qa, 'secretary', id, [pdfFile(PUB_PDF, 3)], item)
    await meetingUpload(qa, 'secretary', id, [docxFile(PUB_DOCX)])
  } else if (key === 'Ended') {
    await addItem(qa, id, 'Báo cáo tổng kết')
    const [doc] = await meetingUpload(qa, 'secretary', id, [pdfFile('[QA] FE-07 Báo cáo tổng kết.pdf', 2)])
    for (const step of steps.slice(0, -1)) await transition(qa, actorOf(step, spec.organizer), id, step)
    const posted = await (await client(qa, 'member')).post(`/api/meetings/${id}/documents/${doc.id}/comments`, {
      body: 'QA FE-07: góp ý trước khi kết thúc.',
      pageNumber: 1,
      parentCommentId: null,
    })
    qa.expectStatus(posted, 201)
    await transition(qa, 'chair', id, 'end')
    await sweepLibrary(qa, 'secretary')
    return id
  } else {
    const name = { Secret: '[QA] FE-07 Tài liệu mật.pdf', Cancelled: '[QA] FE-07 Tài liệu đã huỷ.pdf', AdminOrg: '[QA] FE-07 Tài liệu quản trị.pdf' }[key]
    if (key === 'Cancelled') await addItem(qa, id, 'Mục của cuộc họp huỷ')
    await meetingUpload(qa, 'secretary', id, [pdfFile(name, 2)])
  }
  for (const step of steps) await transition(qa, actorOf(step, spec.organizer), id, step)
  await sweepLibrary(qa, 'secretary')
  return id
}

/** Removes every document a criterion added to the reused Draft meeting, so the next criterion starts empty. */
async function clearDraft(qa, meetingId) {
  const list = await docsOf(qa, 'secretary', meetingId)
  const sec = await client(qa, 'secretary')
  for (const d of new Map(everyDoc(list).map((x) => [x.id, x])).values()) await sec.delete(`/api/meetings/${meetingId}/documents/${d.id}`)
}

// ---------------------------------------------------------------------------------------------------------------------
// Checks and screen helpers.

/** Runs every API check, then fails once with all the wrong ones. */
function checker(qa) {
  const wrong = []
  return {
    check(label, res, status, code) {
      const ok = res.status === status && (code === undefined || res.body?.code === code)
      if (!ok) wrong.push(`${label}: want ${status}${code ? ` ${code}` : ''}, got ${res.status} ${JSON.stringify(res.body).slice(0, 200)}`)
      return ok
    },
    fact(label, ok, detail = '') {
      if (!ok) wrong.push(`${label}${detail ? `: ${detail}` : ''}`)
    },
    done() {
      qa.expect(wrong, wrong.join('\n')).toEqual([])
    },
  }
}

async function noSidewaysScroll(qa) {
  const sideways = await qa.page.evaluate(() => document.documentElement.scrollWidth - innerWidth)
  qa.expect(sideways, 'the page scrolls sideways').toBeLessThanOrEqual(0)
}
async function noRawCodes(qa, scope = 'body') {
  const text = await qa.page.locator(scope).first().innerText()
  qa.expect(text, 'a raw MSG code is shown to the user').not.toMatch(/MSG\d+/)
}
async function settled(qa) {
  await qa.page.waitForFunction(() =>
    document.getAnimations().every((a) => a.playState !== 'running' || a.effect?.getTiming().iterations === Infinity),
  )
}
const toast = (qa, text) => qa.page.locator('.toast-stack').getByText(text)
/** True when `inner`'s content ends inside `outer` (nothing cut off at the right edge). */
async function fitsIn(inner, outer) {
  const [a, b] = [await inner.boundingBox(), await outer.boundingBox()]
  const overflow = await inner.evaluate((el) => el.scrollWidth - el.clientWidth)
  return a.x + a.width <= b.x + b.width + 1 && overflow <= 1
}
/** True when `item` is at least partly inside the visible part of the comments list (no scrolling done here). */
async function inCommentsView(qa, item) {
  const box = await item.boundingBox()
  const list = await qa.page.locator('.viewer-comments-list').boundingBox()
  return !!box && !!list && box.y < list.y + list.height && box.y + box.height > list.y
}
const dialog = (qa, name) => qa.page.getByRole('dialog', { name })
async function press(qa, locator) {
  if (qa.device === 'phone') await locator.tap()
  else await locator.click()
}

// Library screen.
const libRow = (qa, name) => qa.page.locator('table.library-table tbody tr').filter({ hasText: name })
async function openLibrary(qa, folderId = null, query = '') {
  const params = new URLSearchParams()
  if (folderId) params.set('folder', folderId)
  for (const [k, v] of new URLSearchParams(query)) params.set(k, v)
  const qs = params.toString()
  await qa.goto(`/mgmt/documents${qs ? `?${qs}` : ''}`)
  await qa.expect(qa.page.locator('table.library-table tbody[aria-busy="false"]')).toBeVisible()
}

// Meeting documents tab.
const docsPanel = (qa) => qa.page.locator('.documents-panel')
const docRow = (qa, name) => docsPanel(qa).locator('li.documents-row').filter({ hasText: name })
async function openDocuments(qa, meeting) {
  await qa.goto(`/mgmt/meetings/${meeting.id}?tab=documents`)
  await qa.expect(qa.page.getByRole('heading', { level: 1, name: meeting.title })).toBeVisible()
  await qa.expect(docsPanel(qa).getByRole('radiogroup', { name: 'Nhóm tài liệu' })).toBeVisible()
}
/** Picks a group of the documents tab: 'Nội dung' (agenda), 'Biểu quyết', 'Chung'. */
async function group(qa, label) {
  await press(qa, docsPanel(qa).locator('label.segmented-option').filter({ hasText: label }))
}

// Messages (catalogue texts).
const MSG = {
  161: 'Đã thêm tài liệu vào cuộc họp.',
  167: 'Đã gỡ tài liệu khỏi cuộc họp.',
  175: 'Mục này không còn trong kho tài liệu của bạn.',
  180: 'Đã xoá tài liệu khỏi kho của bạn.',
  183: 'Đã đổi tên tài liệu.',
  185: 'Đã chuyển tài liệu.',
  187: 'Đã tạo thư mục.',
  191: 'Đã đổi tên thư mục.',
  194: 'Đã chuyển thư mục.',
  197: 'Đã xoá thư mục.',
  238: 'Đã gắn tài liệu vào mục nghị sự.',
  239: 'Đã gỡ tài liệu khỏi mục nghị sự.',
  164: 'Hãy chọn tệp PDF hoặc DOCX hợp lệ, mỗi tệp không quá 10 MB.',
}

// ---------------------------------------------------------------------------------------------------------------------

export default {
  ticket: 'SCRUM-194',
  title: 'FE-07 Kho tài liệu cá nhân & Tài liệu cuộc họp: thư mục, tải lên, đổi tên, chuyển, xoá, tìm; tài liệu cuộc họp, gắn mục, gỡ, trình xem, góp ý, quyền theo vai trò',
  devices: ['desktop'],
  criteria: [
    // ------------------------------------------------------------------------------------------- UC-41a, UC-37
    {
      id: 'AC1',
      role: 'member',
      devices: ['desktop', 'phone'],
      title: 'Thành viên tạo thư mục ở gốc kho, mở thư mục, thấy đường dẫn và trạng thái rỗng',
      steps: ['Đăng nhập thanhvien@, mở Kho tài liệu (/mgmt/documents)', 'Bấm "Thư mục mới", nhập tên, bấm "Tạo thư mục"', 'Mở thư mục vừa tạo'],
      expected:
        'Toast "Đã tạo thư mục." (MSG187); thư mục hiện trong bảng với nhãn "Thư mục"; mở ra: tiêu đề là tên thư mục, đường dẫn "Kho tài liệu › tên", "Thư mục này chưa có gì."; nút "Thư mục mới" bị khoá khi tên trống; không cuộn ngang',
      async run(qa) {
        const name = qa.name(`FE-07 Hồ sơ ${rand()}`)
        try {
          await openLibrary(qa)
          await qa.shot('kho-goc')
          await press(qa, qa.page.getByRole('button', { name: 'Thư mục mới' }))
          const form = dialog(qa, 'Thư mục mới')
          await qa.expect(form).toBeVisible()
          await qa.expect(form.getByRole('button', { name: 'Tạo thư mục' })).toBeDisabled()
          await form.getByLabel('Tên thư mục').fill(name)
          await settled(qa)
          await qa.shot('hop-thoai-thu-muc-moi')
          await form.getByRole('button', { name: 'Tạo thư mục' }).click()
          await qa.expect(toast(qa, MSG[187])).toBeVisible()
          const row = libRow(qa, name)
          await qa.expect(row).toBeVisible()
          await qa.expect(row).toContainText('Thư mục')
          await settled(qa)
          await qa.shot('thu-muc-da-tao')
          await noSidewaysScroll(qa)
          await press(qa, row.getByRole('link', { name }))
          await qa.expect(qa.page.getByRole('heading', { level: 1, name })).toBeVisible()
          const trail = qa.page.getByRole('navigation', { name: 'Vị trí trong kho' })
          await qa.expect(trail.getByRole('link', { name: 'Kho tài liệu' })).toBeVisible()
          await qa.expect(qa.page.getByText('Thư mục này chưa có gì.')).toBeVisible()
          await settled(qa)
          await qa.shot('trong-thu-muc-rong')
          await noSidewaysScroll(qa)
          const folder = (await library(qa, 'member')).folders.find((f) => f.name === name)
          qa.expect(folder, 'the folder is not in the library').toBeTruthy()
          qa.expect(folder.parentId, 'the folder is not at the library root').toBeNull()
        } finally {
          await sweepLibrary(qa, 'member')
        }
      },
    },

    // ------------------------------------------------------------------------------------------- UC-39, UC-37
    {
      id: 'AC2',
      role: 'member',
      devices: ['desktop', 'phone'],
      title: 'Thành viên tải một PDF và một DOCX vào thư mục đang mở; tài liệu hiện với số trang, mở được trong trình xem của kho',
      steps: [
        'Mở một thư mục "[QA] FE-07 …" trong kho',
        'Bấm "Tải tài liệu lên", chọn một PDF 3 trang và một DOCX',
        'Bấm "Tải lên 2 tài liệu", chờ khay tải lên',
        'Bấm vào tên PDF',
      ],
      expected:
        'Hộp thoại ghi "Thư mục đích: <tên>", liệt kê 2 tệp; khay "Đã tải lên 2 tệp"; bảng có 2 dòng, PDF ghi "3 trang"; API: 2 tài liệu ở đúng thư mục, không vào cuộc họp nào; trình xem kho hiện tên tệp và nội dung PDF',
      async run(qa) {
        const folder = await createFolder(qa, 'member', qa.name(`FE-07 Tải lên ${rand()}`))
        const pdf = pdfFile(qa.name(`FE-07 Công văn ${rand()}.pdf`), 3)
        const docx = docxFile(qa.name(`FE-07 Ghi chú ${rand()}.docx`))
        try {
          await openLibrary(qa, folder.id)
          await press(qa, qa.page.getByRole('button', { name: /Tải (tài liệu )?lên/ }).first())
          const form = dialog(qa, 'Tải tài liệu lên')
          await qa.expect(form).toBeVisible()
          await qa.expect(form.getByText(folder.name)).toBeVisible()
          await form.locator('input[type=file]').setInputFiles([pdf, docx])
          await qa.expect(form.getByRole('list', { name: 'Tệp đã chọn' }).getByRole('listitem')).toHaveCount(2)
          await settled(qa)
          await qa.shot('chon-hai-tep')
          await form.getByRole('button', { name: 'Tải lên 2 tài liệu' }).click()
          const tray = qa.page.getByRole('region', { name: 'Tải lên' })
          await qa.expect(tray.getByText(/Đã tải lên 2 tệp/)).toBeVisible({ timeout: 30_000 })
          await qa.expect(libRow(qa, pdf.name)).toBeVisible()
          await qa.expect(libRow(qa, docx.name)).toBeVisible()
          await qa.expect(libRow(qa, pdf.name)).toContainText('3 trang')
          await settled(qa)
          await qa.shot('da-tai-len')
          await noSidewaysScroll(qa)
          const view = await library(qa, 'member', folder.id)
          const got = view.documents.filter((d) => [pdf.name, docx.name].includes(d.fileName))
          qa.expect(got.length, 'both files are in the folder').toBe(2)
          for (const d of got) qa.expect(d.meetingCount, `${d.fileName} is in a meeting`).toBe(0)
          qa.expect(got.find((d) => d.fileName === pdf.name).pageCount).toBe(3)
          await press(qa, libRow(qa, pdf.name).getByRole('link', { name: pdf.name }))
          await qa.expect(qa.page.getByRole('heading', { level: 1, name: pdf.name })).toBeVisible()
          await qa.expect(qa.page.locator('.viewer-stage canvas').first()).toBeVisible({ timeout: 15_000 })
          await settled(qa)
          await qa.shot('xem-pdf-trong-kho')
          await noSidewaysScroll(qa)
        } finally {
          await sweepLibrary(qa, 'member')
        }
      },
    },
    {
      id: 'AC3',
      role: 'member',
      title: 'Tải lên tệp không phải PDF/DOCX: hộp thoại không nhận; API từ chối nội dung sai loại (MSG164) và quá 10 tệp (MSG288)',
      steps: [
        'Mở "Tải tài liệu lên", chọn một tệp .txt',
        'API: POST /api/documents/upload với tệp văn bản đổi đuôi .pdf; với 11 tệp PDF',
        'API: tải vào thư mục của người khác; không đăng nhập',
      ],
      expected:
        'Hộp thoại báo "Hãy chọn tệp PDF hoặc DOCX hợp lệ, mỗi tệp không quá 10 MB." và nêu tên tệp, nút Tải lên khoá; API: 400 MSG164, 400 MSG288, 404 MSG179, 401; kho không thêm gì',
      async run(qa) {
        const bad = textFile(qa.name(`FE-07 Ghi chú ${rand()}.txt`))
        const before = (await library(qa, 'member')).documents.length
        await openLibrary(qa)
        await press(qa, qa.page.getByRole('button', { name: /Tải (tài liệu )?lên/ }).first())
        const form = dialog(qa, 'Tải tài liệu lên')
        await form.locator('input[type=file]').setInputFiles([bad])
        await qa.expect(form.getByRole('alert').getByText(MSG[164])).toBeVisible()
        await qa.expect(form.getByRole('alert').getByText(bad.name)).toBeVisible()
        await qa.expect(form.getByRole('button', { name: /^Tải lên/ })).toBeDisabled()
        await settled(qa)
        await qa.shot('tep-txt-bi-tu-choi')
        const c = checker(qa)
        const fake = { name: qa.name(`FE-07 giả ${rand()}.pdf`), mimeType: PDF_TYPE, buffer: Buffer.from('<html>không phải pdf</html>') }
        c.check('text renamed .pdf', await upload(qa, 'member', '/api/documents/upload', [fake]), 400, 'MSG164')
        const eleven = Array.from({ length: 11 }, (_, i) => pdfFile(qa.name(`FE-07 mười một ${i}.pdf`), 1))
        c.check('11 files', await upload(qa, 'member', '/api/documents/upload', eleven), 400, 'MSG288')
        const theirs = await createFolder(qa, 'secretary', qa.name(`FE-07 của thư ký ${rand()}`))
        try {
          c.check("someone else's folder", await upload(qa, 'member', `/api/documents/upload?folderId=${theirs.id}`, [pdfFile(qa.name('FE-07 lạc.pdf'), 1)]), 404, 'MSG179')
        } finally {
          await sweepLibrary(qa, 'secretary')
        }
        c.check('signed out', await upload(qa, 'anon', '/api/documents/upload', [pdfFile(qa.name('FE-07 ẩn danh.pdf'), 1)]), 401)
        c.fact('nothing was added', (await library(qa, 'member')).documents.length === before)
        await sweepLibrary(qa, 'member')
        c.done()
      },
    },
    {
      id: 'AC4',
      role: 'member',
      devices: ['desktop', 'phone'],
      title: 'Tìm theo tên và lọc theo loại chỉ trong thư mục đang mở; không khớp thì báo rỗng và cho xoá bộ lọc',
      steps: [
        'Kho có "Biên bản X.pdf" ở gốc và "Biên bản X.pdf", "Biên bản X.docx", "Khác.pdf" trong một thư mục',
        'Mở thư mục, gõ "biên bản x" vào ô tìm',
        'Chọn loại "PDF"',
        'Tìm một chuỗi không có',
      ],
      expected:
        'Tìm không phân biệt hoa thường: hiện 2 tài liệu của thư mục, không hiện tệp ở gốc; lọc PDF: còn 1; không khớp: "Không có tài liệu hoặc thư mục nào khớp bộ lọc." và nút "Xoá bộ lọc" đưa danh sách về đủ',
      async run(qa) {
        const tag = rand()
        const folder = await createFolder(qa, 'member', qa.name(`FE-07 Tìm ${tag}`))
        try {
          await libraryUpload(qa, 'member', [pdfFile(`[QA] FE-07 Biên bản ${tag}.pdf`, 1)])
          await libraryUpload(
            qa,
            'member',
            [pdfFile(`[QA] FE-07 Biên bản ${tag}.pdf`, 1), docxFile(`[QA] FE-07 Biên bản ${tag}.docx`), pdfFile(`[QA] FE-07 Khác ${tag}.pdf`, 1)],
            folder.id,
          )
          await openLibrary(qa, folder.id)
          await qa.expect(qa.page.locator('table.library-table tbody tr')).toHaveCount(3)
          await qa.page.getByRole('searchbox').fill(`biên bản ${tag.toUpperCase()}`)
          await qa.expect(qa.page.locator('table.library-table tbody tr')).toHaveCount(2)
          await qa.expect(qa.page).toHaveURL(new RegExp(`folder=${folder.id}`))
          await settled(qa)
          await qa.shot('tim-trong-thu-muc')
          await press(qa, qa.page.getByRole('button', { name: /Lọc theo loại tệp/ }))
          await qa.page.getByRole('option', { name: 'PDF' }).click()
          await qa.expect(qa.page.locator('table.library-table tbody tr')).toHaveCount(1)
          await qa.expect(libRow(qa, `Biên bản ${tag}.pdf`)).toBeVisible()
          await settled(qa)
          await qa.shot('loc-pdf')
          await qa.page.getByRole('searchbox').fill(`không-có-${tag}`)
          const empty = qa.page.getByText('Không có tài liệu hoặc thư mục nào khớp bộ lọc.')
          await qa.expect(empty).toBeVisible()
          await settled(qa)
          await qa.shot('khong-khop')
          await noSidewaysScroll(qa)
          qa.expect(await fitsIn(empty, qa.page.locator('.table-card')), 'the empty-state text is cut off at the card edge').toBe(true)
          await press(qa, qa.page.getByRole('button', { name: 'Xoá bộ lọc' }))
          await qa.expect(qa.page.locator('table.library-table tbody tr')).toHaveCount(3)
          const api = await library(qa, 'member', folder.id, `q=${encodeURIComponent(`BIÊN BẢN ${tag}`)}&type=pdf`)
          qa.expect(api.documents.map((d) => d.fileName)).toEqual([`[QA] FE-07 Biên bản ${tag}.pdf`])
        } finally {
          await sweepLibrary(qa, 'member')
        }
      },
    },

    // ------------------------------------------------------------------------------------------- UC-40b, UC-41b
    {
      id: 'AC5',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Đổi tên tài liệu đang dùng trong cuộc họp: chỉ sửa phần tên, giữ đuôi; cuộc họp vẫn giữ tên cũ',
      steps: [
        'Thư ký có tài liệu trong kho đã thêm vào cuộc họp Nháp "[QA] FE-07 dùng lại Nháp"',
        'Bấm "Đổi tên" ở dòng tài liệu, sửa tên, bấm "Lưu"',
      ],
      expected:
        'Hộp thoại "Đổi tên" ghi cuộc họp vẫn giữ tên cũ, đuôi.pdf in sau ô; toast "Đã đổi tên tài liệu." (MSG183); dòng hiện tên mới, "1 cuộc họp"; API: tài liệu trong cuộc họp vẫn tên cũ (UC-40b)',
      async run(qa) {
        const m = await reused(qa, 'Draft')
        const old = qa.name(`FE-07 Dự thảo ${rand()}.pdf`)
        const next = qa.name(`FE-07 Dự thảo bản cuối ${rand()}`)
        try {
          const [doc] = await libraryUpload(qa, 'secretary', [pdfFile(old, 2)])
          qa.expectStatus(await (await client(qa, 'secretary')).post(`/api/meetings/${m.id}/documents`, { documentIds: [doc.id] }), 201)
          await openLibrary(qa)
          const row = libRow(qa, old)
          await qa.expect(row).toContainText('1 cuộc họp')
          await press(qa, row.getByRole('button', { name: `Đổi tên ${old}` }))
          const form = dialog(qa, 'Đổi tên')
          await qa.expect(form.getByText('Cuộc họp đang dùng tài liệu này vẫn giữ tên cũ.', { exact: false })).toBeVisible()
          await qa.expect(form.locator('.name-ext')).toHaveText('.pdf')
          await qa.expect(form.getByLabel('Tên tài liệu')).toHaveValue(old.slice(0, -4))
          await form.getByLabel('Tên tài liệu').fill(next)
          await settled(qa)
          await qa.shot('doi-ten-tai-lieu')
          await form.getByRole('button', { name: 'Lưu' }).click()
          await qa.expect(toast(qa, MSG[183])).toBeVisible()
          await qa.expect(libRow(qa, `${next}.pdf`)).toBeVisible()
          await settled(qa)
          await qa.shot('ten-moi')
          await noSidewaysScroll(qa)
          const inMeeting = everyDoc(await docsOf(qa, 'secretary', m.id)).map((d) => d.fileName)
          qa.expect(inMeeting, 'the meeting copy took the new name').toContain(old)
          qa.expect(inMeeting).not.toContain(`${next}.pdf`)
        } finally {
          await clearDraft(qa, m.id)
          await sweepLibrary(qa, 'secretary')
        }
      },
    },
    {
      id: 'AC6',
      role: 'member',
      title: 'Tên không hợp lệ: đổi đuôi, chỉ có đuôi, trống (MSG347); thư mục trống (MSG188) hoặc quá 400 ký tự (MSG348); đổi tên thư mục hợp lệ (MSG191)',
      steps: [
        'API: PUT /api/documents/{id}/name với "x.docx" cho một PDF, ".pdf", "   "',
        'API: POST /api/folders tên "  ", tên 401 ký tự; PUT tên thư mục "  "',
        'Màn hình: mở "Đổi tên" một thư mục, xoá trống ô, rồi đặt tên mới và Lưu',
      ],
      expected:
        'API 400 MSG347 ×3, 400 MSG188 ×2, 400 MSG348; màn hình: nút Lưu khoá khi trống; tên mới → toast "Đã đổi tên thư mục." (MSG191)',
      async run(qa) {
        const c = checker(qa)
        const api = await client(qa, 'member')
        const folder = await createFolder(qa, 'member', qa.name(`FE-07 Đổi tên ${rand()}`))
        try {
          const [doc] = await libraryUpload(qa, 'member', [pdfFile(qa.name(`FE-07 Tên ${rand()}.pdf`), 1)])
          c.check('change the extension', await api.put(`/api/documents/${doc.id}/name`, { fileName: 'Tờ trình.docx' }), 400, 'MSG347')
          c.check('extension only', await api.put(`/api/documents/${doc.id}/name`, { fileName: '.pdf' }), 400, 'MSG347')
          c.check('blank', await api.put(`/api/documents/${doc.id}/name`, { fileName: '   ' }), 400, 'MSG347')
          c.check('blank folder', await api.post('/api/folders', { name: '   ', parentId: null }), 400, 'MSG188')
          c.check('folder 401 chars', await api.post('/api/folders', { name: `[QA] FE-07 ${'a'.repeat(390)}`, parentId: null }), 400, 'MSG348')
          c.check('rename folder blank', await api.put(`/api/folders/${folder.id}/name`, { name: ' ' }), 400, 'MSG188')
          await openLibrary(qa)
          await press(qa, libRow(qa, folder.name).getByRole('button', { name: `Đổi tên ${folder.name}` }))
          const form = dialog(qa, 'Đổi tên')
          await form.getByLabel('Tên thư mục').fill('   ')
          await qa.expect(form.getByRole('button', { name: 'Lưu' })).toBeDisabled()
          await settled(qa)
          await qa.shot('ten-trong-bi-khoa')
          const renamed = qa.name(`FE-07 Đã đổi ${rand()}`)
          await form.getByLabel('Tên thư mục').fill(renamed)
          await form.getByRole('button', { name: 'Lưu' }).click()
          await qa.expect(toast(qa, MSG[191])).toBeVisible()
          await qa.expect(libRow(qa, renamed)).toBeVisible()
          await qa.shot('thu-muc-ten-moi')
        } finally {
          await sweepLibrary(qa, 'member')
        }
        c.done()
      },
    },

    // ------------------------------------------------------------------------------------------- UC-40c, UC-41c
    {
      id: 'AC7',
      role: 'member',
      devices: ['desktop', 'phone'],
      title: 'Chuyển tài liệu vào một thư mục bằng "Chuyển đến…"; chuyển về gốc kho',
      steps: ['Tài liệu ở gốc kho, có thư mục "[QA] FE-07 Đích …"', 'Bấm "Chuyển đến" ở dòng tài liệu, chọn thư mục đích, bấm "Chuyển vào “…”"', 'Mở thư mục đích'],
      expected:
        'Toast "Đã chuyển tài liệu." (MSG185); tài liệu rời gốc, có trong thư mục đích; nút chuyển khoá khi đích là chỗ đang ở; API chuyển về gốc (folderId null) 200 MSG185',
      async run(qa) {
        const target = await createFolder(qa, 'member', qa.name(`FE-07 Đích ${rand()}`))
        try {
          const [doc] = await libraryUpload(qa, 'member', [pdfFile(qa.name(`FE-07 Cần chuyển ${rand()}.pdf`), 1)])
          await openLibrary(qa)
          await press(qa, libRow(qa, doc.fileName).getByRole('button', { name: `Chuyển đến ${doc.fileName}` }))
          const form = dialog(qa, 'Chuyển đến…')
          await qa.expect(form).toBeVisible()
          await qa.expect(form.getByRole('button', { name: /^Chuyển vào/ })).toBeDisabled()
          await press(qa, form.getByRole('list', { name: 'Thư mục con' }).getByRole('button', { name: target.name }))
          await qa.expect(form.getByRole('button', { name: `Chuyển vào “${target.name}”` })).toBeEnabled()
          await settled(qa)
          await qa.shot('chon-thu-muc-dich')
          await form.getByRole('button', { name: `Chuyển vào “${target.name}”` }).click()
          await qa.expect(toast(qa, MSG[185])).toBeVisible()
          await qa.expect(libRow(qa, doc.fileName)).toHaveCount(0)
          await openLibrary(qa, target.id)
          await qa.expect(libRow(qa, doc.fileName)).toBeVisible()
          await settled(qa)
          await qa.shot('trong-thu-muc-dich')
          await noSidewaysScroll(qa)
          const back = await (await client(qa, 'member')).put(`/api/documents/${doc.id}/folder`, { folderId: null })
          qa.expectStatus(back, 200)
          qa.expect(back.body.code).toBe('MSG185')
          qa.expect((await library(qa, 'member')).documents.some((d) => d.id === doc.id), 'not back at the root').toBe(true)
        } finally {
          await sweepLibrary(qa, 'member')
        }
      },
    },
    {
      id: 'AC8',
      role: 'member',
      title: 'Thư mục: chuyển vào chính nó / thư mục con bị chặn (MSG193), quá 5 cấp bị chặn (MSG189), chuyển hợp lệ (MSG194); ở cấp 5 nút "Thư mục mới" khoá',
      steps: [
        'Dựng chuỗi 5 cấp A›B›C›D›E và một thư mục F có con G',
        'API: chuyển A vào C; tạo thư mục trong E; chuyển F vào D',
        'Màn hình: mở E; mở "Chuyển đến…" của B',
        'API: chuyển F vào B',
      ],
      expected:
        'API 409 MSG193, 409 MSG189 ×2, 200 MSG194; ở E nút "Thư mục mới" khoá; hộp "Chuyển đến…" của B ở cấp A không liệt kê B (chính nó)',
      async run(qa) {
        const c = checker(qa)
        const api = await client(qa, 'member')
        const t = rand()
        try {
          const chain = []
          let parent = null
          for (const letter of ['A', 'B', 'C', 'D', 'E']) {
            const f = await createFolder(qa, 'member', qa.name(`FE-07 Cấp ${letter} ${t}`), parent)
            chain.push(f)
            parent = f.id
          }
          const f = await createFolder(qa, 'member', qa.name(`FE-07 F ${t}`))
          await createFolder(qa, 'member', qa.name(`FE-07 G ${t}`), f.id)
          c.check('A into its grandchild C', await api.put(`/api/folders/${chain[0].id}/parent`, { parentId: chain[2].id }), 409, 'MSG193')
          c.check('A into itself', await api.put(`/api/folders/${chain[0].id}/parent`, { parentId: chain[0].id }), 409, 'MSG193')
          c.check('a 6th level', await api.post('/api/folders', { name: qa.name(`FE-07 Cấp 6 ${t}`), parentId: chain[4].id }), 409, 'MSG189')
          c.check('F›G under D (G would be level 6)', await api.put(`/api/folders/${f.id}/parent`, { parentId: chain[3].id }), 409, 'MSG189')
          await openLibrary(qa, chain[4].id)
          await qa.expect(qa.page.getByRole('button', { name: 'Thư mục mới' })).toBeDisabled()
          await qa.shot('cap-5-khoa-thu-muc-moi')
          await openLibrary(qa, chain[0].id)
          await press(qa, libRow(qa, chain[1].name).getByRole('button', { name: `Chuyển đến ${chain[1].name}` }))
          const form = dialog(qa, 'Chuyển đến…')
          await qa.expect(form).toBeVisible()
          c.fact('the move dialog offers B into itself', (await form.getByRole('list', { name: 'Thư mục con' }).getByRole('button', { name: chain[1].name }).count()) === 0)
          await settled(qa)
          await qa.shot('khong-cho-chuyen-vao-chinh-no')
          await form.getByRole('button', { name: 'Huỷ' }).click()
          const ok = await api.put(`/api/folders/${f.id}/parent`, { parentId: chain[1].id })
          c.check('F›G under B', ok, 200, 'MSG194')
        } finally {
          await sweepLibrary(qa, 'member')
        }
        c.done()
      },
    },

    // ------------------------------------------------------------------------------------------- UC-40a, UC-41d
    {
      id: 'AC9',
      role: 'member',
      devices: ['desktop', 'phone'],
      title: 'Xoá tài liệu và thư mục rỗng sau khi xác nhận; thư mục còn thư mục con không xoá được',
      steps: [
        'Kho có thư mục P có thư mục con, thư mục rỗng R và một tài liệu ở gốc',
        'Bấm "Xoá" ở P; bấm "Xoá" ở tài liệu rồi "Xoá tài liệu"; bấm "Xoá" ở R rồi "Xoá thư mục"',
        'API: xoá thư mục còn tài liệu',
      ],
      expected:
        'P: hộp "Xoá thư mục?" báo "Chỉ xoá được thư mục rỗng." và khoá nút xoá; tài liệu: hộp nêu tên, toast "Đã xoá tài liệu khỏi kho của bạn." (MSG180); R: toast "Đã xoá thư mục." (MSG197); API 409 MSG196',
      async run(qa) {
        const t = rand()
        try {
          const p = await createFolder(qa, 'member', qa.name(`FE-07 Có con ${t}`))
          await createFolder(qa, 'member', qa.name(`FE-07 Con ${t}`), p.id)
          const r = await createFolder(qa, 'member', qa.name(`FE-07 Rỗng ${t}`))
          const withDoc = await createFolder(qa, 'member', qa.name(`FE-07 Có tệp ${t}`))
          await libraryUpload(qa, 'member', [pdfFile(qa.name(`FE-07 Trong thư mục ${t}.pdf`), 1)], withDoc.id)
          const [doc] = await libraryUpload(qa, 'member', [pdfFile(qa.name(`FE-07 Sẽ xoá ${t}.pdf`), 1)])
          await openLibrary(qa)
          await press(qa, libRow(qa, p.name).getByRole('button', { name: `Xoá ${p.name}` }))
          let confirm = dialog(qa, 'Xoá thư mục?')
          await qa.expect(confirm.getByText('Chỉ xoá được thư mục rỗng.')).toBeVisible()
          await qa.expect(confirm.getByRole('button', { name: 'Xoá thư mục' })).toBeDisabled()
          await settled(qa)
          await qa.shot('thu-muc-con-con')
          await confirm.getByRole('button', { name: 'Giữ thư mục' }).click()
          await press(qa, libRow(qa, doc.fileName).getByRole('button', { name: `Xoá ${doc.fileName}` }))
          confirm = dialog(qa, 'Xoá tài liệu?')
          await qa.expect(confirm.getByText(doc.fileName)).toBeVisible()
          await settled(qa)
          await qa.shot('xac-nhan-xoa-tai-lieu')
          await confirm.getByRole('button', { name: 'Xoá tài liệu' }).click()
          await qa.expect(toast(qa, MSG[180])).toBeVisible()
          await qa.expect(libRow(qa, doc.fileName)).toHaveCount(0)
          await press(qa, libRow(qa, r.name).getByRole('button', { name: `Xoá ${r.name}` }))
          await dialog(qa, 'Xoá thư mục?').getByRole('button', { name: 'Xoá thư mục' }).click()
          await qa.expect(toast(qa, MSG[197])).toBeVisible()
          await qa.expect(libRow(qa, r.name)).toHaveCount(0)
          await settled(qa)
          await qa.shot('da-xoa')
          await noSidewaysScroll(qa)
          const res = await (await client(qa, 'member')).delete(`/api/folders/${withDoc.id}`)
          qa.expectStatus(res, 409)
          qa.expect(res.body.code).toBe('MSG196')
        } finally {
          await sweepLibrary(qa, 'member')
        }
      },
    },
    {
      id: 'AC10',
      role: 'secretary',
      title: 'Xoá khỏi kho một tài liệu đang dùng trong cuộc họp: hộp xác nhận nói cuộc họp vẫn đọc được; Thành viên vẫn mở được bản trong cuộc họp',
      steps: [
        'Thư ký thêm một tài liệu trong kho vào cuộc họp Đã công bố "[QA] FE-07 dùng lại Đã công bố"',
        'Ở Kho, bấm "Xoá" tài liệu đó và xác nhận',
        'Thành viên mở nội dung tài liệu trong cuộc họp',
      ],
      expected:
        'Hộp "Xoá tài liệu?" ghi "Tài liệu đang dùng trong 1 cuộc họp; các cuộc họp đó vẫn đọc được."; toast MSG180; API: Thành viên GET content 200, tài liệu vẫn trong danh sách cuộc họp (BR-074–BR-076); sau đó Thư ký gỡ tài liệu khỏi cuộc họp',
      async run(qa) {
        const m = await reused(qa, 'Published')
        const name = qa.name(`FE-07 Dùng chung ${rand()}.pdf`)
        let meetingDocId
        try {
          const [doc] = await libraryUpload(qa, 'secretary', [pdfFile(name, 1)])
          const added = await (await client(qa, 'secretary')).post(`/api/meetings/${m.id}/documents`, { documentIds: [doc.id] })
          qa.expectStatus(added, 201)
          meetingDocId = added.body.documents[0].id
          await openLibrary(qa)
          await libRow(qa, name).getByRole('button', { name: `Xoá ${name}` }).click()
          const confirm = dialog(qa, 'Xoá tài liệu?')
          await qa.expect(confirm.getByText('Tài liệu đang dùng trong 1 cuộc họp; các cuộc họp đó vẫn đọc được.', { exact: false })).toBeVisible()
          await settled(qa)
          await qa.shot('xoa-tai-lieu-dang-dung')
          await confirm.getByRole('button', { name: 'Xoá tài liệu' }).click()
          await qa.expect(toast(qa, MSG[180])).toBeVisible()
          const member = await client(qa, 'member')
          const content = await member.get(`/api/meetings/${m.id}/documents/${meetingDocId}/content`)
          qa.expectStatus(content, 200)
          qa.expect(everyDoc(await docsOf(qa, 'member', m.id)).some((d) => d.id === meetingDocId), 'gone from the meeting').toBe(true)
        } finally {
          if (meetingDocId) await (await client(qa, 'secretary')).delete(`/api/meetings/${m.id}/documents/${meetingDocId}`)
          await sweepLibrary(qa, 'secretary')
        }
      },
    },
    {
      id: 'AC11',
      role: 'member',
      title: 'Kho chỉ của chủ: thư mục, tài liệu của người khác trả 404 MSG175 như không tồn tại; mở link thư mục của người khác thì báo và về gốc kho',
      steps: [
        'Thư ký có một thư mục và một tài liệu trong kho',
        'Thành viên gọi API xem thư mục, mở, đổi tên, chuyển, xoá tài liệu, đổi tên và xoá thư mục của Thư ký; chuyển tài liệu của mình vào thư mục của Thư ký',
        'Thành viên mở /mgmt/documents?folder=<thư mục của Thư ký>',
      ],
      expected:
        'API 404 MSG175 cho mọi thao tác trên mục của người khác, 404 MSG179 cho thư mục đích của người khác, 401 khi chưa đăng nhập; màn hình: toast "Mục này không còn trong kho tài liệu của bạn.", về /mgmt/documents, không lộ tên thư mục',
      async run(qa) {
        const c = checker(qa)
        const t = rand()
        try {
          const folder = await createFolder(qa, 'secretary', qa.name(`FE-07 Riêng của thư ký ${t}`))
          const [theirs] = await libraryUpload(qa, 'secretary', [pdfFile(qa.name(`FE-07 Riêng ${t}.pdf`), 1)], folder.id)
          const [mine] = await libraryUpload(qa, 'member', [pdfFile(qa.name(`FE-07 Của tôi ${t}.pdf`), 1)])
          const api = await client(qa, 'member')
          c.check('view their folder', await api.get(`/api/documents?folderId=${folder.id}`), 404, 'MSG175')
          c.check('open their file', await api.get(`/api/documents/${theirs.id}/content`), 404, 'MSG175')
          c.check('rename their file', await api.put(`/api/documents/${theirs.id}/name`, { fileName: 'x.pdf' }), 404, 'MSG175')
          c.check('move their file', await api.put(`/api/documents/${theirs.id}/folder`, { folderId: null }), 404, 'MSG175')
          c.check('delete their file', await api.delete(`/api/documents/${theirs.id}`), 404, 'MSG175')
          c.check('rename their folder', await api.put(`/api/folders/${folder.id}/name`, { name: 'x' }), 404, 'MSG175')
          c.check('delete their folder', await api.delete(`/api/folders/${folder.id}`), 404, 'MSG175')
          c.check('move mine into their folder', await api.put(`/api/documents/${mine.id}/folder`, { folderId: folder.id }), 404, 'MSG179')
          c.check('folder under theirs', await api.post('/api/folders', { name: qa.name('FE-07 lạc'), parentId: folder.id }), 404, 'MSG179')
          c.check('signed out', await (await client(qa, 'anon')).get('/api/documents'), 401)
          await qa.goto(`/mgmt/documents?folder=${folder.id}`)
          await qa.expect(toast(qa, MSG[175])).toBeVisible()
          await qa.expect(qa.page).toHaveURL(/\/mgmt\/documents$/)
          c.fact("their folder's name is shown", (await qa.page.getByText(folder.name).count()) === 0)
          await settled(qa)
          await qa.shot('thu-muc-nguoi-khac')
          c.fact('their file is still there', (await library(qa, 'secretary', folder.id)).documents.some((d) => d.id === theirs.id))
        } finally {
          await sweepLibrary(qa, 'secretary')
          await sweepLibrary(qa, 'member')
        }
        c.done()
      },
    },

    // ------------------------------------------------------------------------------------------- UC-36a
    {
      id: 'AC12',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Thư ký tải tệp từ máy vào cuộc họp Nháp ("Thêm tài liệu chung" › "Tải từ máy"); tệp vào nhóm Tài liệu chung và vào kho, trong thư mục của cuộc họp',
      steps: [
        'Mở tab Tài liệu của "[QA] FE-07 dùng lại Nháp"',
        'Bấm "Thêm tài liệu chung", tab "Tải từ máy", chọn một PDF và một DOCX, bấm "Thêm 2 tài liệu"',
      ],
      expected:
        'Hộp thoại ghi tệp cũng được lưu vào kho; khay "Đã tải lên 2 tệp"; nhóm "Tài liệu chung" hiện 2 tệp, PDF "3 trang"; API: 2 tài liệu chung, addedBy là Thư ký; kho Thư ký có thư mục gắn cuộc họp (meetingId) tên "<tên cuộc họp> – dd/mm/yyyy" chứa 2 tệp (UC-36a)',
      async run(qa) {
        const m = await reused(qa, 'Draft')
        const u = await users(qa)
        const pdf = pdfFile(qa.name(`FE-07 Báo cáo ${rand()}.pdf`), 3)
        const docx = docxFile(qa.name(`FE-07 Phụ lục ${rand()}.docx`))
        try {
          await openDocuments(qa, m)
          await press(qa, docsPanel(qa).getByRole('button', { name: 'Thêm tài liệu chung' }))
          const form = dialog(qa, 'Thêm tài liệu chung')
          await qa.expect(form).toBeVisible()
          await qa.expect(form.getByText('Tệp cũng được lưu vào Kho tài liệu của bạn', { exact: false })).toBeVisible()
          await form.locator('input[type=file]').setInputFiles([pdf, docx])
          await qa.expect(form.getByRole('list', { name: 'Tệp đã chọn' }).getByRole('listitem')).toHaveCount(2)
          await settled(qa)
          await qa.shot('chon-tep-tu-may')
          await form.getByRole('button', { name: 'Thêm 2 tài liệu' }).click()
          await qa.expect(qa.page.getByRole('region', { name: 'Tải lên' }).getByText(/Đã tải lên 2 tệp/)).toBeVisible({ timeout: 30_000 })
          await qa.expect(docRow(qa, pdf.name)).toBeVisible()
          await qa.expect(docRow(qa, docx.name)).toBeVisible()
          await qa.expect(docRow(qa, pdf.name).getByText(/3 trang/)).toBeVisible()
          await settled(qa)
          await qa.shot('tai-lieu-chung')
          await noSidewaysScroll(qa)
          await noRawCodes(qa, 'main')
          const list = await docsOf(qa, 'secretary', m.id)
          const general = list.general.filter((d) => [pdf.name, docx.name].includes(d.fileName))
          qa.expect(general.length, 'both files are general documents').toBe(2)
          for (const d of general) qa.expect(d.addedBy.userId).toBe(u.secretary.id)
          const lib = await library(qa, 'secretary')
          const mf = lib.folders.find((f) => f.meetingId === m.id)
          qa.expect(mf, 'no library folder for the meeting').toBeTruthy()
          qa.expect(mf.name).toMatch(new RegExp(`^${m.title.replace(/[[\]()]/g, '\\$&')} – \\d{2}/\\d{2}/\\d{4}$`))
          const inFolder = (await library(qa, 'secretary', mf.id)).documents.map((d) => d.fileName)
          qa.expect(inFolder).toEqual(qa.expect.arrayContaining([pdf.name, docx.name]))
        } finally {
          await clearDraft(qa, m.id)
          await sweepLibrary(qa, 'secretary')
        }
      },
    },
    {
      id: 'AC13',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Thêm tài liệu chung "Từ kho của tôi": tài liệu đã có trong cuộc họp bị khoá với chip; tài liệu khác thêm được; thêm trùng hoặc không chọn bị từ chối',
      steps: [
        'Kho Thư ký có tài liệu A (đã thêm vào cuộc họp Nháp) và B (chưa)',
        'Tab Tài liệu › "Thêm tài liệu chung" › "Từ kho của tôi"',
        'Chọn B, bấm "Thêm 1 tài liệu"',
        'API: thêm lại A; gửi mảng rỗng; gửi tài liệu kho của Thành viên',
      ],
      expected:
        'A có chip "Đã có trong cuộc họp", ô chọn khoá; toast "Đã thêm tài liệu vào cuộc họp." (MSG161); B vào nhóm chung với tên hiện tại; API 409 MSG165, 400 MSG249, 400 MSG249',
      async run(qa) {
        const m = await reused(qa, 'Draft')
        const t = rand()
        try {
          const [a, b] = await libraryUpload(qa, 'secretary', [pdfFile(qa.name(`FE-07 Đã có ${t}.pdf`), 1), docxFile(qa.name(`FE-07 Chưa có ${t}.docx`))])
          const sec = await client(qa, 'secretary')
          qa.expectStatus(await sec.post(`/api/meetings/${m.id}/documents`, { documentIds: [a.id] }), 201)
          const [theirs] = await libraryUpload(qa, 'member', [pdfFile(qa.name(`FE-07 Của thành viên ${t}.pdf`), 1)])
          await openDocuments(qa, m)
          await press(qa, docsPanel(qa).getByRole('button', { name: 'Thêm tài liệu chung' }))
          const form = dialog(qa, 'Thêm tài liệu chung')
          await press(qa, form.locator('label.segmented-option').filter({ hasText: 'Kho' }))
          const picker = form.getByRole('list', { name: 'Thư mục và tài liệu' })
          const rowA = picker.locator('label.picker-row').filter({ hasText: a.fileName })
          await qa.expect(rowA.getByText('Đã có trong cuộc họp')).toBeVisible()
          await qa.expect(rowA.getByRole('checkbox')).toBeDisabled()
          await picker.locator('label.picker-row').filter({ hasText: b.fileName }).getByRole('checkbox').check()
          await settled(qa)
          await qa.shot('chon-tu-kho')
          await form.getByRole('button', { name: 'Thêm 1 tài liệu' }).click()
          await qa.expect(toast(qa, MSG[161])).toBeVisible()
          await qa.expect(docRow(qa, b.fileName)).toBeVisible()
          await settled(qa)
          await qa.shot('da-them-tu-kho')
          await noSidewaysScroll(qa)
          const c = checker(qa)
          c.check('add A again', await sec.post(`/api/meetings/${m.id}/documents`, { documentIds: [a.id] }), 409, 'MSG165')
          c.check('nothing chosen', await sec.post(`/api/meetings/${m.id}/documents`, { documentIds: [] }), 400, 'MSG249')
          c.check("someone else's library document", await sec.post(`/api/meetings/${m.id}/documents`, { documentIds: [theirs.id] }), 400, 'MSG249')
          c.fact('B is a general document', (await docsOf(qa, 'secretary', m.id)).general.some((d) => d.fileName === b.fileName))
          c.done()
        } finally {
          await clearDraft(qa, m.id)
          await sweepLibrary(qa, 'secretary')
          await sweepLibrary(qa, 'member')
        }
      },
    },

    // ------------------------------------------------------------------------------------------- UC-36b, UC-36d
    {
      id: 'AC14',
      role: 'chair',
      devices: ['desktop', 'phone'],
      title: 'Chủ toạ gắn một tài liệu chung vào mục nghị sự 1 (kẹp giấy › "Trong cuộc họp"); tài liệu rời nhóm chung, hiện dưới mục',
      steps: [
        'Cuộc họp Nháp có tài liệu chung D',
        'Chủ toạ mở tab Tài liệu, nhóm "Theo nội dung họp", bấm kẹp giấy "Thêm tài liệu cho mục 1"',
        'Tab "Trong cuộc họp", chọn D, bấm "Gắn 1 tài liệu"',
        'API: gắn D lần nữa; gắn từ kho (AF-2) một tài liệu mới vào mục 2',
      ],
      expected:
        'Toast "Đã gắn tài liệu vào mục nghị sự." (MSG238); D hiện dưới mục 1, nhóm "Tài liệu chung" không còn D (UC-36b, UC-36d); API 409 MSG243; from-library 200 MSG238, tài liệu mới vào cuộc họp và mục 2',
      async run(qa) {
        const m = await reused(qa, 'Draft')
        const t = rand()
        try {
          const [d] = await meetingUpload(qa, 'secretary', m.id, [pdfFile(qa.name(`FE-07 Gắn mục ${t}.pdf`), 2)])
          await openDocuments(qa, m)
          await press(qa, docsPanel(qa).getByRole('button', { name: 'Thêm tài liệu cho mục 1' }))
          const form = dialog(qa, 'Thêm tài liệu cho mục 1')
          await qa.expect(form).toBeVisible()
          const list = form.getByRole('list', { name: 'Tài liệu của cuộc họp' })
          await list.locator('label.picker-row').filter({ hasText: d.fileName }).getByRole('checkbox').check()
          await settled(qa)
          await qa.shot('chon-tai-lieu-cuoc-hop')
          await form.getByRole('button', { name: 'Gắn 1 tài liệu' }).click()
          await qa.expect(toast(qa, MSG[238])).toBeVisible()
          const item1 = docsPanel(qa).locator('.documents-group').first()
          await qa.expect(item1.locator('li.documents-row').filter({ hasText: d.fileName })).toBeVisible()
          await settled(qa)
          await qa.shot('da-gan-muc-1')
          await noSidewaysScroll(qa)
          await group(qa, 'Chung')
          await qa.expect(docRow(qa, d.fileName)).toHaveCount(0)
          await qa.shot('nhom-chung-khong-con')
          const c = checker(qa)
          const chair = await client(qa, 'chair')
          const [item1Id, item2Id] = m.items.map((i) => i.id)
          c.check('attach again', await chair.post(`/api/meetings/${m.id}/agenda/${item1Id}/documents`, { documentIds: [d.id] }), 409, 'MSG243')
          const [lib] = await libraryUpload(qa, 'chair', [docxFile(qa.name(`FE-07 Từ kho chủ toạ ${t}.docx`))])
          const linked = await chair.post(`/api/meetings/${m.id}/agenda/${item2Id}/documents/from-library`, { documentIds: [lib.id] })
          c.check('attach from the library', linked, 200, 'MSG238')
          const after = await docsOf(qa, 'chair', m.id)
          c.fact('the library file is under item 2', after.agendaItems.find((i) => i.id === item2Id)?.documents.some((x) => x.fileName === lib.fileName))
          c.fact('D is not general', !after.general.some((x) => x.id === d.id))
          c.done()
        } finally {
          await clearDraft(qa, m.id)
          await sweepLibrary(qa, 'secretary')
          await sweepLibrary(qa, 'chair')
        }
      },
    },
    {
      id: 'AC15',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Thư ký gỡ tài liệu khỏi một mục ("Gỡ khỏi mục này"); tài liệu vẫn trong cuộc họp, về nhóm chung; gỡ lần nữa báo liên kết không còn',
      steps: ['Tài liệu D gắn mục 1 của cuộc họp Nháp', 'Tab Tài liệu, ở dòng D dưới mục 1 bấm "Gỡ … khỏi mục này", bấm "Gỡ khỏi mục"', 'API: gỡ lần nữa'],
      expected:
        'Hộp "Gỡ tài liệu khỏi mục này?" nói D vẫn còn trong cuộc họp; toast "Đã gỡ tài liệu khỏi mục nghị sự." (MSG239); D về "Tài liệu chung" (UC-36b, UC-36d); API 404 MSG246',
      async run(qa) {
        const m = await reused(qa, 'Draft')
        try {
          const [d] = await meetingUpload(qa, 'secretary', m.id, [pdfFile(qa.name(`FE-07 Gỡ mục ${rand()}.pdf`), 1)], m.items[0].id)
          await openDocuments(qa, m)
          const row = docsPanel(qa).locator('.documents-group').first().locator('li.documents-row').filter({ hasText: d.fileName })
          await press(qa, row.getByRole('button', { name: `Gỡ ${d.fileName} khỏi mục này` }))
          const confirm = dialog(qa, 'Gỡ tài liệu khỏi mục này?')
          await qa.expect(confirm.getByText('vẫn còn trong cuộc họp', { exact: false })).toBeVisible()
          await settled(qa)
          await qa.shot('xac-nhan-go-khoi-muc')
          await confirm.getByRole('button', { name: 'Gỡ khỏi mục' }).click()
          await qa.expect(toast(qa, MSG[239])).toBeVisible()
          await qa.expect(row).toHaveCount(0)
          await group(qa, 'Chung')
          await qa.expect(docRow(qa, d.fileName)).toBeVisible()
          await settled(qa)
          await qa.shot('ve-nhom-chung')
          await noSidewaysScroll(qa)
          const again = await (await client(qa, 'secretary')).delete(`/api/meetings/${m.id}/agenda/${m.items[0].id}/documents/${d.id}`)
          qa.expectStatus(again, 404)
          qa.expect(again.body.code).toBe('MSG246')
        } finally {
          await clearDraft(qa, m.id)
          await sweepLibrary(qa, 'secretary')
        }
      },
    },

    // ------------------------------------------------------------------------------------------- UC-36c
    {
      id: 'AC16',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Thư ký gỡ tài liệu khỏi cuộc họp; tài liệu biến mất khỏi mọi nhóm, không mở được nữa, bản trong kho giữ nguyên; thêm lại là bản mới không có góp ý cũ',
      steps: [
        'Tài liệu D (từ kho Thư ký) gắn mục 1 và có một góp ý',
        'Tab Tài liệu, bấm "Gỡ … khỏi cuộc họp", bấm "Gỡ khỏi cuộc họp"',
        'API: mở D; gỡ lần nữa; thêm lại cùng tài liệu kho',
      ],
      expected:
        'Hộp "Gỡ tài liệu khỏi cuộc họp?" nêu tên, nói bản trong kho giữ nguyên; toast "Đã gỡ tài liệu khỏi cuộc họp." (MSG167); D không còn ở mục 1 hay nhóm chung; API content 404 MSG156, gỡ lại 404 MSG170; kho vẫn có tài liệu; thêm lại 201, id mới, commentCount 0 (BR-074–BR-076)',
      async run(qa) {
        const m = await reused(qa, 'Draft')
        try {
          const [lib] = await libraryUpload(qa, 'secretary', [pdfFile(qa.name(`FE-07 Gỡ cuộc họp ${rand()}.pdf`), 1)])
          const sec = await client(qa, 'secretary')
          const linked = await sec.post(`/api/meetings/${m.id}/agenda/${m.items[0].id}/documents/from-library`, { documentIds: [lib.id] })
          qa.expectStatus(linked, 200)
          const d = linked.body.documents[0]
          qa.expectStatus(await sec.post(`/api/meetings/${m.id}/documents/${d.id}/comments`, { body: 'QA FE-07: góp ý sẽ thành lịch sử.', pageNumber: null, parentCommentId: null }), 201)
          await openDocuments(qa, m)
          const row = docsPanel(qa).locator('.documents-group').first().locator('li.documents-row').filter({ hasText: d.fileName })
          await press(qa, row.getByRole('button', { name: `Gỡ ${d.fileName} khỏi cuộc họp` }))
          const confirm = dialog(qa, 'Gỡ tài liệu khỏi cuộc họp?')
          await qa.expect(confirm.getByText(d.fileName)).toBeVisible()
          await qa.expect(confirm.getByText('Bản trong Kho tài liệu của người đã tải lên vẫn giữ nguyên.', { exact: false })).toBeVisible()
          await settled(qa)
          await qa.shot('xac-nhan-go-khoi-cuoc-hop')
          await confirm.getByRole('button', { name: 'Gỡ khỏi cuộc họp' }).click()
          await qa.expect(toast(qa, MSG[167])).toBeVisible()
          await qa.expect(docRow(qa, d.fileName)).toHaveCount(0)
          await group(qa, 'Chung')
          await qa.expect(docRow(qa, d.fileName)).toHaveCount(0)
          await settled(qa)
          await qa.shot('da-go-khoi-cuoc-hop')
          await noSidewaysScroll(qa)
          const c = checker(qa)
          c.check('open the removed document', await sec.get(`/api/meetings/${m.id}/documents/${d.id}/content`), 404, 'MSG156')
          c.check('remove again', await sec.delete(`/api/meetings/${m.id}/documents/${d.id}`), 404, 'MSG170')
          c.fact('the library copy is gone', (await library(qa, 'secretary')).documents.some((x) => x.id === lib.id))
          const again = await sec.post(`/api/meetings/${m.id}/documents`, { documentIds: [lib.id] })
          c.check('add it again', again, 201, 'MSG161')
          const readded = (await docsOf(qa, 'secretary', m.id)).general.find((x) => x.fileName === lib.fileName)
          c.fact('re-added as a new row', readded && readded.id !== d.id)
          c.fact('old comments came back', readded?.commentCount === 0, `commentCount ${readded?.commentCount}`)
          c.done()
        } finally {
          await clearDraft(qa, m.id)
          await sweepLibrary(qa, 'secretary')
        }
      },
    },

    // ------------------------------------------------------------------------------------------- UC-35a, UC-35b
    {
      id: 'AC17',
      role: 'member',
      devices: ['desktop', 'phone'],
      title: 'Thành viên xem tab Tài liệu của cuộc họp Đã công bố: theo mục và tài liệu chung, chỉ đọc; mở PDF và DOCX trong trình xem, có Tải về',
      steps: [
        'Thành viên mở tab Tài liệu của "[QA] FE-07 dùng lại Đã công bố"',
        'Xem nhóm "Theo nội dung họp" (mục 1 có Tờ trình.pdf, mục 2 rỗng) rồi "Tài liệu chung"',
        'Bấm Tờ trình.pdf; quay lại; bấm Phụ lục.docx',
      ],
      expected:
        'Mục 1 hiện Tờ trình.pdf "3 trang", mục 2 "Mục này chưa có tài liệu."; nhóm chung có Phụ lục.docx; không có "Thêm tài liệu chung", kẹp giấy hay nút gỡ; có nút "Tải về"; trình xem PDF mở phủ trên tab (ADR-0066, URL không đổi): tên tệp, tên cuộc họp, "Trang 1 / 3", nút "Góp ý" và "Tải về", "Đóng" về lại danh sách; trình xem DOCX (đường dẫn riêng) hiện nội dung; tải về trả đúng byte (API 200)',
      async run(qa) {
        const m = await reused(qa, 'Published')
        await openDocuments(qa, m)
        const groups = docsPanel(qa).locator('.documents-group')
        await qa.expect(groups.first().locator('li.documents-row').filter({ hasText: PUB_PDF })).toBeVisible()
        await qa.expect(groups.first().getByText(/3 trang/)).toBeVisible()
        const second = groups.nth(1)
        await press(qa, second.locator('.documents-group-head'))
        await qa.expect(second.getByText('Mục này chưa có tài liệu.')).toBeVisible()
        await qa.expect(docsPanel(qa).getByRole('button', { name: 'Thêm tài liệu chung' })).toHaveCount(0)
        await qa.expect(docsPanel(qa).getByRole('button', { name: /^Thêm tài liệu cho mục/ })).toHaveCount(0)
        await qa.expect(docsPanel(qa).getByRole('button', { name: /^Gỡ / })).toHaveCount(0)
        await qa.expect(docsPanel(qa).getByRole('button', { name: `Tải về ${PUB_PDF}` })).toBeVisible()
        await settled(qa)
        await qa.shot('theo-noi-dung-hop')
        await noSidewaysScroll(qa)
        await group(qa, 'Chung')
        await qa.expect(docRow(qa, PUB_DOCX)).toBeVisible()
        await qa.shot('tai-lieu-chung')
        await group(qa, 'Nội dung')
        await press(qa, docRow(qa, PUB_PDF).getByRole('link'))
        // ADR-0066: the viewer opens over the tab (same URL), not as a page of its own; the list stays underneath.
        const viewer = qa.page.getByRole('dialog', { name: 'Xem tài liệu' })
        await qa.expect(viewer).toBeVisible()
        await qa.expect(qa.page).toHaveURL(new RegExp(`/mgmt/meetings/${m.id}\\?tab=documents`))
        await qa.expect(viewer.getByRole('heading', { level: 1, name: PUB_PDF })).toBeVisible()
        await qa.expect(viewer.getByText('Trang 1 / 3')).toBeVisible({ timeout: 15_000 })
        await qa.expect(viewer.getByRole('button', { name: /Góp ý/ })).toBeVisible()
        await qa.expect(viewer.getByRole('button', { name: /Tải về/ })).toBeVisible()
        await qa.expect(viewer.locator('.viewer-meeting')).toHaveText(m.title)
        await settled(qa)
        await qa.shot('trinh-xem-pdf')
        await noSidewaysScroll(qa)
        await press(qa, viewer.getByRole('button', { name: 'Đóng', exact: true }))
        await qa.expect(viewer).toHaveCount(0)
        await qa.expect(docRow(qa, PUB_PDF)).toBeVisible()
        const list = await docsOf(qa, 'member', m.id)
        const docx = list.general.find((d) => d.fileName === PUB_DOCX)
        await qa.goto(`/mgmt/meetings/${m.id}/documents/${docx.id}`)
        await qa.expect(qa.page.getByRole('heading', { level: 1, name: PUB_DOCX })).toBeVisible()
        await qa.expect(qa.page.locator('.viewer-stage').getByText('FE-07 Phụ lục', { exact: false }).first()).toBeVisible({ timeout: 15_000 })
        await settled(qa)
        await qa.shot('trinh-xem-docx')
        await noSidewaysScroll(qa)
        const dl = await (await client(qa, 'member')).get(`/api/meetings/${m.id}/documents/${docx.id}/content?download=true`)
        qa.expectStatus(dl, 200)
        qa.expect(dl.headers['content-disposition'] ?? '').toContain('attachment')
      },
    },
    {
      id: 'AC18',
      role: 'member',
      devices: ['desktop', 'phone'],
      title: 'Cuộc họp Mật: Thành viên xem được trực tuyến nhưng không có nút Tải về; API tải về bị chặn (MSG159)',
      steps: ['Thành viên mở tab Tài liệu và trình xem của "[QA] FE-07 dùng lại Mật"', 'API: GET content và content?download=true'],
      expected:
        'Dòng tài liệu không có nút Tải về; trình xem có nhãn mức Mật và "Chỉ xem trực tuyến" thay cho "Tải về"; API xem 200, tải về 403 MSG159',
      async run(qa) {
        const m = await reused(qa, 'Secret')
        const list = await docsOf(qa, 'member', m.id)
        const doc = everyDoc(list)[0]
        qa.expect(doc, 'the Secret meeting has no document').toBeTruthy()
        await openDocuments(qa, m)
        await group(qa, 'Chung')
        await qa.expect(docRow(qa, doc.fileName)).toBeVisible()
        await qa.expect(docsPanel(qa).getByRole('button', { name: /^Tải về/ })).toHaveCount(0)
        await qa.shot('mat-khong-tai-ve')
        await press(qa, docRow(qa, doc.fileName).getByRole('link'))
        await qa.expect(qa.page.getByRole('heading', { level: 1, name: doc.fileName })).toBeVisible()
        await qa.expect(qa.page.getByText('Chỉ xem trực tuyến')).toBeVisible()
        await qa.expect(qa.page.getByRole('button', { name: /Tải về/ })).toHaveCount(0)
        await qa.expect(qa.page.locator('.viewer-stage canvas').first()).toBeVisible({ timeout: 15_000 })
        await settled(qa)
        await qa.shot('trinh-xem-mat')
        await noSidewaysScroll(qa)
        const c = checker(qa)
        const api = await client(qa, 'member')
        c.check('view', await api.get(`/api/meetings/${m.id}/documents/${doc.id}/content`), 200)
        c.check('download', await api.get(`/api/meetings/${m.id}/documents/${doc.id}/content?download=true`), 403, 'MSG159')
        c.done()
      },
    },
    {
      id: 'AC19',
      role: 'member',
      title: 'Người không xem được cuộc họp không thấy tài liệu: trình xem hiện "Không tìm thấy trang"; API 404 MSG156 cùng một body với ID không tồn tại',
      steps: [
        'Cuộc họp Nháp có một tài liệu (Thành viên chưa xem được cuộc họp Nháp)',
        'Thành viên mở /mgmt/meetings/{id}/documents/{docId}',
        'API: danh sách, nội dung, góp ý; một ID tài liệu bịa trên cuộc họp Đã công bố; chưa đăng nhập',
      ],
      expected: 'Trang "Không tìm thấy trang", không lộ tên tệp; API 404 MSG156 ×4 với cùng body; 401 khi chưa đăng nhập',
      async run(qa) {
        const m = await reused(qa, 'Draft')
        const pub = await reused(qa, 'Published')
        try {
          const [d] = await meetingUpload(qa, 'secretary', m.id, [pdfFile(qa.name(`FE-07 Nháp kín ${rand()}.pdf`), 1)])
          await qa.goto(`/mgmt/meetings/${m.id}/documents/${d.id}`)
          await qa.expect(qa.page.getByRole('heading', { name: 'Không tìm thấy trang' })).toBeVisible()
          qa.expect(await qa.page.getByText(d.fileName).count(), 'the file name leaks').toBe(0)
          await qa.shot('khong-tim-thay')
          const c = checker(qa)
          const api = await client(qa, 'member')
          const list = await api.get(`/api/meetings/${m.id}/documents`)
          c.check('list of a Draft meeting', list, 404, 'MSG156')
          c.check('content', await api.get(`/api/meetings/${m.id}/documents/${d.id}/content`), 404, 'MSG156')
          c.check('comments', await api.get(`/api/meetings/${m.id}/documents/${d.id}/comments`), 404, 'MSG156')
          const ghost = await api.get(`/api/meetings/${pub.id}/documents/01a00000-0000-7000-8000-000000000000/content`)
          c.check('made-up document id', ghost, 404, 'MSG156')
          c.fact('different bodies for hidden and missing', JSON.stringify(ghost.body) === JSON.stringify(list.body), `${JSON.stringify(ghost.body)} vs ${JSON.stringify(list.body)}`)
          c.check('signed out', await (await client(qa, 'anon')).get(`/api/meetings/${pub.id}/documents`), 401)
          c.done()
        } finally {
          await clearDraft(qa, m.id)
          await sweepLibrary(qa, 'secretary')
        }
      },
    },

    // ------------------------------------------------------------------------------------------- permission table
    {
      id: 'AC20',
      role: 'member',
      title: 'Bảng quyền ghi: Thành viên, Người tổ chức không giữ vai trò Chủ toạ/Thư ký, cuộc họp đã kết thúc và đã huỷ đều bị từ chối đúng mã',
      steps: [
        'Thành viên trên cuộc họp Đã công bố: tải lên, thêm từ kho, gỡ khỏi cuộc họp, gắn vào mục, gỡ khỏi mục',
        'quantri@ (Người tổ chức, không là Chủ toạ/Thư ký) tải lên cuộc họp của mình',
        'Thành viên tải lên cuộc họp Nháp (không xem được)',
        'Thư ký trên cuộc họp Đã kết thúc và Đã huỷ: tải lên, gỡ, gắn mục, góp ý',
      ],
      expected:
        'Thành viên: 403 MSG162, 403 MSG162, 403 MSG169, 403 MSG240, 403 MSG241; quantri@ 403 MSG162; cuộc họp Nháp 404 MSG174; Đã kết thúc: 409 MSG172, 409 MSG248, 409 MSG247, góp ý 409 MSG251; Đã huỷ: 409 MSG349 ×4; không gì thay đổi',
      async run(qa) {
        const pub = await reused(qa, 'Published')
        const adminOrg = await reused(qa, 'AdminOrg')
        const draft = await reused(qa, 'Draft')
        const ended = await reused(qa, 'Ended')
        const cancelled = await reused(qa, 'Cancelled')
        const caps = await qa.capabilities('member')
        qa.note(`thanhvien@: ${caps.roleName} [${caps.capabilities.join(', ')}]`)
        const c = checker(qa)
        const one = () => [pdfFile(qa.name(`FE-07 bị từ chối ${rand()}.pdf`), 1)]
        try {
          const member = await client(qa, 'member')
          const pubList = await docsOf(qa, 'member', pub.id)
          const pdf = pubList.agendaItems[0].documents[0]
          const item1 = pubList.agendaItems[0].id
          const [mine] = await libraryUpload(qa, 'member', one())
          const before = JSON.stringify(everyDoc(pubList).map((d) => d.id).sort())
          c.check('member uploads', await upload(qa, 'member', `/api/meetings/${pub.id}/documents/upload`, one()), 403, 'MSG162')
          c.check('member adds from library', await member.post(`/api/meetings/${pub.id}/documents`, { documentIds: [mine.id] }), 403, 'MSG162')
          c.check('member removes', await member.delete(`/api/meetings/${pub.id}/documents/${pdf.id}`), 403, 'MSG169')
          c.check('member attaches', await member.post(`/api/meetings/${pub.id}/agenda/${pubList.agendaItems[1].id}/documents`, { documentIds: [pdf.id] }), 403, 'MSG240')
          c.check('member detaches', await member.delete(`/api/meetings/${pub.id}/agenda/${item1}/documents/${pdf.id}`), 403, 'MSG241')
          c.fact('the meeting documents changed', JSON.stringify(everyDoc(await docsOf(qa, 'member', pub.id)).map((d) => d.id).sort()) === before)
          c.check('organizer (not Chair/Secretary) uploads', await upload(qa, 'admin', `/api/meetings/${adminOrg.id}/documents/upload`, one()), 403, 'MSG162')
          c.check('member uploads to a Draft meeting', await upload(qa, 'member', `/api/meetings/${draft.id}/documents/upload`, one()), 404, 'MSG174')
          const sec = await client(qa, 'secretary')
          const endedList = await docsOf(qa, 'secretary', ended.id)
          const ed = everyDoc(endedList)[0]
          c.check('upload after the end', await upload(qa, 'secretary', `/api/meetings/${ended.id}/documents/upload`, one()), 409, 'MSG172')
          c.check('remove after the end', await sec.delete(`/api/meetings/${ended.id}/documents/${ed.id}`), 409, 'MSG248')
          c.check('attach after the end', await sec.post(`/api/meetings/${ended.id}/agenda/${endedList.agendaItems[0].id}/documents`, { documentIds: [ed.id] }), 409, 'MSG247')
          c.check('comment after the end', await sec.post(`/api/meetings/${ended.id}/documents/${ed.id}/comments`, { body: 'QA: sau khi kết thúc', pageNumber: null, parentCommentId: null }), 409, 'MSG251')
          const cl = await docsOf(qa, 'secretary', cancelled.id)
          const cd = everyDoc(cl)[0]
          c.check('upload when cancelled', await upload(qa, 'secretary', `/api/meetings/${cancelled.id}/documents/upload`, one()), 409, 'MSG349')
          c.check('remove when cancelled', await sec.delete(`/api/meetings/${cancelled.id}/documents/${cd.id}`), 409, 'MSG349')
          c.check('attach when cancelled', await sec.post(`/api/meetings/${cancelled.id}/agenda/${cl.agendaItems[0].id}/documents`, { documentIds: [cd.id] }), 409, 'MSG349')
          c.check('comment when cancelled', await sec.post(`/api/meetings/${cancelled.id}/documents/${cd.id}/comments`, { body: 'QA: khi đã huỷ', pageNumber: null, parentCommentId: null }), 409, 'MSG349')
          c.fact('the ended meeting still lists its document', everyDoc(await docsOf(qa, 'member', ended.id)).some((d) => d.id === ed.id))
        } finally {
          await sweepLibrary(qa, 'member')
          await sweepLibrary(qa, 'secretary')
        }
        c.done()
      },
    },
    {
      id: 'AC21',
      role: 'admin',
      devices: ['desktop', 'phone'],
      title: 'Người tổ chức không giữ vai trò Chủ toạ/Thư ký chỉ đọc tài liệu: không có nút thêm/gỡ, nhưng vẫn góp ý được (BR-082)',
      steps: ['quantri@ mở tab Tài liệu của "[QA] FE-07 dùng lại (quản trị tổ chức)"', 'Mở tài liệu, viết góp ý, bấm "Gửi góp ý"'],
      expected: 'Không có "Thêm tài liệu chung", kẹp giấy, nút gỡ; góp ý mới hiện trong bảng Góp ý với tên quantri@; API 201 MSG199',
      async run(qa) {
        const m = await reused(qa, 'AdminOrg')
        const u = await users(qa)
        const caps = await qa.capabilities('admin')
        qa.note(`quantri@: ${caps.roleName} [${caps.capabilities.join(', ')}]; myMeetingRole ${m.myMeetingRole ?? 'null'}`)
        await openDocuments(qa, m)
        await group(qa, 'Chung')
        const doc = everyDoc(await docsOf(qa, 'admin', m.id))[0]
        await qa.expect(docRow(qa, doc.fileName)).toBeVisible()
        await qa.expect(docsPanel(qa).getByRole('button', { name: 'Thêm tài liệu chung' })).toHaveCount(0)
        await qa.expect(docsPanel(qa).getByRole('button', { name: /^Gỡ / })).toHaveCount(0)
        await settled(qa)
        await qa.shot('nguoi-to-chuc-chi-doc')
        await press(qa, docRow(qa, doc.fileName).getByRole('link'))
        await qa.expect(qa.page.getByRole('heading', { level: 1, name: doc.fileName })).toBeVisible()
        await qa.expect(qa.page.locator('.viewer-stage canvas').first()).toBeVisible({ timeout: 15_000 })
        if (qa.device === 'phone') await press(qa, qa.page.getByRole('button', { name: /Góp ý/ }))
        const panel = qa.page.getByRole('complementary', { name: 'Góp ý' })
        const text = `QA FE-07: người tổ chức góp ý ${rand()}`
        await panel.getByLabel('Nội dung góp ý').fill(text)
        await panel.getByRole('button', { name: 'Gửi góp ý' }).click()
        const posted = panel.locator('article.comment').filter({ hasText: text })
        await qa.expect(posted.getByText(u.admin.name)).toBeAttached()
        await settled(qa)
        const seen = await inCommentsView(qa, posted)
        await qa.shot('ngay-sau-khi-gui')
        await posted.scrollIntoViewIfNeeded()
        await qa.expect(posted.getByText(u.admin.name)).toBeVisible()
        await settled(qa)
        await qa.shot('gop-y-cua-nguoi-to-chuc')
        await noSidewaysScroll(qa)
        qa.expect(seen, 'after sending, the new comment is below the visible part of the list (the user sees no result)').toBe(true)
      },
    },

    // ------------------------------------------------------------------------------------------- UC-38
    {
      id: 'AC22',
      role: 'member',
      devices: ['desktop', 'phone'],
      title: 'Thành viên góp ý gắn trang trên PDF, Chủ toạ trả lời trong cùng luồng; số góp ý tăng',
      steps: [
        'Thành viên mở Tờ trình.pdf của cuộc họp Đã công bố, mở bảng Góp ý',
        'Nhập góp ý, để "Gắn trang 1", bấm "Gửi góp ý"',
        'Chủ toạ trả lời qua API (parentCommentId); Thành viên bấm "Làm mới góp ý"',
        'API: góp ý trống, quá 4.000 ký tự; trả lời một câu trả lời',
      ],
      expected:
        'Góp ý hiện với tên Thành viên, "Trang 1", giờ gửi; câu trả lời của Chủ toạ hiện dưới góp ý; số trên nút Góp ý tăng; API 400 MSG200, 400 MSG348; trả lời một câu trả lời vào luồng của góp ý gốc (UC-38); MSG199 hiện cho người gửi (UC-38 bước 6)',
      async run(qa) {
        const m = await reused(qa, 'Published')
        const u = await users(qa)
        const pdf = (await docsOf(qa, 'member', m.id)).agendaItems[0].documents[0]
        const before = pdf.commentCount
        await qa.goto(`/mgmt/meetings/${m.id}/documents/${pdf.id}`)
        await qa.expect(qa.page.getByText('Trang 1 / 3')).toBeVisible({ timeout: 15_000 })
        if (qa.device === 'phone') await press(qa, qa.page.getByRole('button', { name: /Góp ý/ }))
        const panel = qa.page.getByRole('complementary', { name: 'Góp ý' })
        await qa.expect(panel).toBeVisible()
        const text = `QA FE-07: xem lại số liệu trang này ${rand()}`
        await panel.getByLabel('Nội dung góp ý').fill(text)
        await qa.expect(panel.getByLabel('Gắn trang 1')).toBeChecked()
        await settled(qa)
        await qa.shot('soan-gop-y')
        await panel.getByRole('button', { name: 'Gửi góp ý' }).click()
        const mine = panel.locator('li.comment-thread').filter({ hasText: text })
        await qa.expect(mine.getByText(u.member.name).first()).toBeAttached()
        const toastShown = await toast(qa, 'Đã gửi góp ý.').isVisible().catch(() => false)
        await settled(qa)
        const seen = await inCommentsView(qa, mine)
        await qa.shot('gop-y-da-gui')
        await mine.scrollIntoViewIfNeeded()
        await qa.expect(mine.getByText(u.member.name).first()).toBeVisible()
        await qa.expect(mine.getByText('Trang 1').first()).toBeVisible()
        await settled(qa)
        await qa.shot('gop-y-moi-sau-khi-cuon')
        const comments = (await (await client(qa, 'member')).get(`/api/meetings/${m.id}/documents/${pdf.id}/comments`)).body.comments
        const root = comments.find((x) => x.body === text)
        qa.expect(root?.pageNumber).toBe(1)
        const chair = await client(qa, 'chair')
        const reply = await chair.post(`/api/meetings/${m.id}/documents/${pdf.id}/comments`, { body: `QA FE-07: đã ghi nhận ${rand()}`, pageNumber: null, parentCommentId: root.id })
        qa.expectStatus(reply, 201)
        await panel.getByRole('button', { name: 'Làm mới góp ý' }).click()
        await qa.expect(mine.locator('.comment-replies').getByText(reply.body.comment.body)).toBeAttached()
        await mine.scrollIntoViewIfNeeded()
        await qa.expect(mine.locator('.comment-replies').getByText(reply.body.comment.body)).toBeVisible()
        await qa.expect(mine.locator('.comment-replies').getByText(u.chair.name)).toBeVisible()
        await settled(qa)
        await qa.shot('tra-loi-trong-luong')
        await noSidewaysScroll(qa)
        const c = checker(qa)
        const member = await client(qa, 'member')
        const nested = await member.post(`/api/meetings/${m.id}/documents/${pdf.id}/comments`, { body: `QA FE-07: trả lời câu trả lời ${rand()}`, pageNumber: null, parentCommentId: reply.body.comment.id })
        c.check('reply to a reply', nested, 201, 'MSG199')
        const thread = (await member.get(`/api/meetings/${m.id}/documents/${pdf.id}/comments`)).body.comments.find((x) => x.id === root.id)
        c.fact('the reply to a reply joined the root thread', thread?.replies.some((r) => r.id === nested.body?.comment?.id))
        c.check('blank', await member.post(`/api/meetings/${m.id}/documents/${pdf.id}/comments`, { body: '   ', pageNumber: null, parentCommentId: null }), 400, 'MSG200')
        c.check('4001 chars', await member.post(`/api/meetings/${m.id}/documents/${pdf.id}/comments`, { body: 'x'.repeat(4001), pageNumber: null, parentCommentId: null }), 400, 'MSG348')
        const count = (await docsOf(qa, 'member', m.id)).agendaItems[0].documents[0].commentCount
        c.fact('comment count grew by 3', count === before + 3, `${before} → ${count}`)
        c.fact('MSG199 "Đã gửi góp ý." is shown after sending (UC-38 step 6)', toastShown)
        c.fact('after sending, the new comment is in the visible part of the list', seen)
        c.done()
      },
    },
    {
      id: 'AC23',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Cuộc họp Đã kết thúc: tài liệu và góp ý chỉ đọc; không có thêm/gỡ; góp ý cũ vẫn đọc được',
      steps: ['Thư ký mở tab Tài liệu của "[QA] FE-07 dùng lại Đã kết thúc"', 'Mở tài liệu, mở bảng Góp ý'],
      expected:
        'Không có "Thêm tài liệu chung", kẹp giấy, nút gỡ; trình xem: góp ý cũ hiện, không có ô soạn, ghi "Cuộc họp đã kết thúc nên không góp ý thêm được."; API GET comments canComment false',
      async run(qa) {
        const m = await reused(qa, 'Ended')
        const doc = everyDoc(await docsOf(qa, 'secretary', m.id))[0]
        await openDocuments(qa, m)
        await group(qa, 'Chung')
        await qa.expect(docRow(qa, doc.fileName)).toBeVisible()
        await qa.expect(docsPanel(qa).getByRole('button', { name: 'Thêm tài liệu chung' })).toHaveCount(0)
        await qa.expect(docsPanel(qa).getByRole('button', { name: /^Gỡ / })).toHaveCount(0)
        await group(qa, 'Nội dung')
        await qa.expect(docsPanel(qa).getByRole('button', { name: /^Thêm tài liệu cho mục/ })).toHaveCount(0)
        await settled(qa)
        await qa.shot('da-ket-thuc-chi-doc')
        await qa.goto(`/mgmt/meetings/${m.id}/documents/${doc.id}`)
        await qa.expect(qa.page.getByRole('heading', { level: 1, name: doc.fileName })).toBeVisible()
        if (qa.device === 'phone') await press(qa, qa.page.getByRole('button', { name: /Góp ý/ }))
        const panel = qa.page.getByRole('complementary', { name: 'Góp ý' })
        await qa.expect(panel.getByText('QA FE-07: góp ý trước khi kết thúc.')).toBeVisible()
        await qa.expect(panel.getByText('Cuộc họp đã kết thúc nên không góp ý thêm được.')).toBeVisible()
        await qa.expect(panel.getByLabel('Nội dung góp ý')).toHaveCount(0)
        await qa.expect(panel.getByRole('button', { name: 'Trả lời' })).toHaveCount(0)
        await settled(qa)
        await qa.shot('gop-y-chi-doc')
        await noSidewaysScroll(qa)
        const res = await (await client(qa, 'secretary')).get(`/api/meetings/${m.id}/documents/${doc.id}/comments`)
        qa.expect(res.body.canComment).toBe(false)
      },
    },

    // ------------------------------------------------------------------------------------------- UC-37a
    {
      id: 'AC24',
      role: 'admin',
      devices: ['desktop', 'phone'],
      title: 'Quản trị viên xem kho của tài khoản đã khoá, chỉ đọc; nút xem kho của tài khoản đang hoạt động bị khoá; API từ chối kho đang hoạt động và người không có quyền',
      steps: [
        'Tài khoản "[QA] FE-07 kho đã khoá" có một thư mục và một PDF, đã bị vô hiệu hoá',
        'quantri@ mở Người dùng, tìm tài khoản đó, bấm "Xem kho tài liệu"',
        'Tìm thanhvien@ (đang hoạt động): nút "Xem kho tài liệu" khoá',
        'API: kho của thanhvien@ (quantri@); kho tài khoản đã khoá (thanhvien@)',
      ],
      expected:
        'Trang "Kho tài liệu của <tên>": thư mục và PDF, chỉ có Tải về, không có Tải lên/Thư mục mới/Đổi tên/Chuyển/Xoá/ô tìm; nút của tài khoản đang hoạt động bị khoá; API 404 MSG175, 403',
      async run(qa) {
        const caps = await qa.capabilities('admin')
        if (!caps.capabilities.includes('AdminUsers')) qa.inconclusive(`quantri@ thiếu AdminUsers (${caps.roleName}).`)
        const admin = await client(qa, 'admin')
        const u = await users(qa)
        const email = 'qa-fe07-locked@trustcabinet.local'
        let locked = (await admin.get(`/api/users?q=${encodeURIComponent(email)}&pageSize=10`)).body
        locked = (Array.isArray(locked) ? locked : (locked.items ?? [])).find((x) => x.email === email)
        if (!locked || locked.isActive || (await admin.get(`/api/users/${locked.id}/library`)).body?.documents?.length === 0) {
          const roles = (await admin.get('/api/roles')).body
          const memberRole = roles.find((r) => r.isDefault) ?? roles.find((r) => r.name === 'Thành viên')
          const password = `Qa-${randomBytes(12).toString('base64url')}-9`
          let id = locked?.id
          const fresh = !id
          const mail = fresh ? email : `qa-fe07-locked-${rand()}@trustcabinet.local`
          const created = await admin.post('/api/users', { fullName: qa.name('FE-07 kho đã khoá'), email: mail, password, roleId: memberRole.id })
          qa.expectStatus(created, 201)
          id = created.body.user.id
          qa.note(`Tạo tài khoản ${mail} (${id}), vô hiệu hoá sau khi có tài liệu`)
          const own = await playwrightRequest.newContext({ baseURL: qa.base })
          try {
            const signed = await own.post('/api/auth/login', { data: { email: mail, password } })
            const token = (await signed.json()).accessToken
            const f = await own.post('/api/folders', { headers: { Authorization: `Bearer ${token}` }, data: { name: qa.name('FE-07 Hồ sơ bàn giao'), parentId: null } })
            qa.expect(f.status()).toBe(201)
            const up = await sendFiles(qa, token, '/api/documents/upload', [pdfFile(qa.name('FE-07 Bàn giao.pdf'), 1)])
            if (up.status === 503) qa.inconclusive('Kho tệp không nhận tệp (503).')
            qa.expectStatus(up, 201)
          } finally {
            await own.dispose()
          }
          qa.expectStatus(await admin.put(`/api/users/${id}/status`, { isActive: false }), 200)
          locked = (await admin.get(`/api/users/${id}`)).body
          locked = locked.user ?? locked
        }
        await qa.goto(`/mgmt/admin/users?q=${encodeURIComponent(locked.email)}`)
        const open = qa.page.getByRole('link', { name: `Xem kho tài liệu của ${locked.fullName}` })
        await qa.expect(open).toBeVisible()
        await press(qa, open)
        await qa.expect(qa.page.getByRole('heading', { level: 1, name: `Kho tài liệu của ${locked.fullName}` })).toBeVisible()
        await qa.expect(qa.page.locator('table.library-table tbody tr').filter({ hasText: '[QA] FE-07 Bàn giao.pdf' })).toBeVisible()
        await qa.expect(qa.page.locator('table.library-table tbody tr').filter({ hasText: '[QA] FE-07 Hồ sơ bàn giao' })).toBeVisible()
        for (const name of ['Thư mục mới', /Tải (tài liệu )?lên/, /^Đổi tên/, /^Chuyển đến/, /^Xoá/]) await qa.expect(qa.page.getByRole('button', { name })).toHaveCount(0)
        await qa.expect(qa.page.getByRole('searchbox')).toHaveCount(0)
        await settled(qa)
        await qa.shot('kho-tai-khoan-da-khoa')
        await noSidewaysScroll(qa)
        await qa.goto(`/mgmt/admin/users?q=${encodeURIComponent(u.member.email)}`)
        await qa.expect(qa.page.getByText(u.member.email).first()).toBeVisible()
        // An active account's library cannot be opened: a disabled button with a tooltip on a desktop; a phone has no
        // hover for the tooltip, so the users table hides disabled row actions there (global.css, .users-table).
        if (qa.device === 'phone') {
          await qa.expect(qa.page.getByRole('button', { name: `Xem kho tài liệu của ${u.member.name}` })).toBeHidden()
          qa.note('Điện thoại: nút "Xem kho tài liệu" của tài khoản đang hoạt động bị ẩn (không phải hiện và khoá như UC-37a bước 1), vì bảng Người dùng ẩn mọi nút bị khoá trên màn cảm ứng — tooltip lý do không xem được bằng chạm.')
        } else await qa.expect(qa.page.getByRole('button', { name: `Xem kho tài liệu của ${u.member.name}` }).first()).toBeDisabled()
        await qa.expect(qa.page.getByRole('link', { name: `Xem kho tài liệu của ${u.member.name}` })).toHaveCount(0)
        await qa.shot('tai-khoan-hoat-dong-nut-khoa')
        const c = checker(qa)
        c.check("an active account's library", await admin.get(`/api/users/${u.member.id}/library`), 404, 'MSG175')
        c.check('a member reads a locked library', await (await client(qa, 'member')).get(`/api/users/${locked.id}/library`), 403)
        c.done()
      },
    },
  ],
}

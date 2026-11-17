// FE-12 — Biên bản & Duyệt, ban hành (QA subtask SCRUM-199): the minutes of a meeting from the one fixed template
// (UC-58 view, UC-59 generate, UC-60 edit and download), the AI draft that fills sections I–III (UC-47 generate, UC-48
// view, UC-49 edit, UC-50 approve), and the approval (UC-61 submit, UC-64 approve and issue, UC-65 return, UC-83 correct
// issued minutes), with BR-373–BR-396, BR-404–BR-413 and BR-446.
//
// Criteria come from docs/01-requirements/05-use-case-specifications.md, 06-business-rules.md,
// docs/03-api/17-du-thao-bien-ban.md, 18-may-ai.md, the message catalogue and the comments of SCRUM-32, SCRUM-33 and
// SCRUM-36 (one approver, approving issues at once, the meeting stays Ended; a reader may download issued minutes, PO
// 08/11; a Guest never sees the minutes, PO 07/11; no recall of a submitted revision and no email on issue, PO 11/11).
// Signing the minutes, even simulated (UC-62, UC-63, SCRUM-37), was dropped by the PO on 14/11 and is not tested.
//
// AI: the server is set to write Normal meetings through a paid outside API (ADR-0056). This plan therefore never asks
// for an AI draft of a Normal meeting — not through the API and not by pressing "Tạo dự thảo" — and never changes the
// AI settings. A draft is only asked for on Internal meetings, which always stay on the in-house machine (BR-381); when
// that machine is off, the request fails with a reason the Secretary can act on, which is what is checked here.
//
// Safe on a shared local database: every meeting is created here through the API, titled "[QA] FE-12 …", nine weeks
// ahead, organised by thuky@ (Secretary) with chutoa@ as Chair and approver and thanhvien@ as Member. Meetings whose
// state a criterion does not move for good are reused across runs ("[QA] FE-12 dùng lại …"); the approval flow seals
// minutes for ever, so each run makes a few fresh Ended meetings for it (one per device for the screens, three for the
// API rules).

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

const rand = () => Math.random().toString(36).slice(2, 6)
const iso = (ms) => new Date(ms).toISOString().replace(/\.\d{3}Z$/, 'Z')
const MINUTE = 60_000
const DAY = 86_400_000
let slotCounter = Math.floor(Math.random() * 7)
/** A 45-minute slot on the day nine weeks from today, between 07:30 and 17:30 Vietnam time, varied per meeting. */
function slot() {
  slotCounter += 1
  const day = new Date(Date.now() + 63 * DAY)
  day.setUTCHours(0, 30, 0, 0) // 07:30 in Vietnam
  const start = day.getTime() + ((slotCounter * 55) % 600) * MINUTE + Math.floor(Math.random() * 6) * 5 * MINUTE
  return { plannedStartTime: iso(start), plannedEndTime: iso(start + 45 * MINUTE) }
}

async function meetingBody(qa, title, sensitivity) {
  const u = await users(qa)
  return {
    title,
    ...slot(),
    mode: 'InPerson',
    location: 'Phòng họp QA 12',
    onlineLink: null,
    sensitivity,
    description: 'Cuộc họp do bộ kiểm thử QA FE-12 tạo.',
    participants: [
      { userId: u.chair.id, role: 'Chair' },
      { userId: u.secretary.id, role: 'Secretary' },
      { userId: u.member.id, role: 'Member' },
    ],
  }
}

const PATHS = {
  Draft: [],
  InProgress: ['submit', 'approve', 'publish', 'start'],
  Ended: ['submit', 'approve', 'publish', 'start', 'end'],
}

async function transition(qa, role, id, action) {
  const u = await users(qa)
  const body = {
    submit: { action: 'SubmitForApproval', assignedApproverId: u.chair.id },
    approve: { action: 'Approve' },
    publish: { action: 'Publish' },
    start: { action: 'Start' },
    end: { action: 'End' },
  }[action]
  const res = await (await client(qa, role)).post(`/api/meetings/${id}/transitions`, body)
  qa.expectStatus(res, 200)
}
const actorOf = (step) => (['approve', 'start', 'end'].includes(step) ? 'chair' : 'secretary')

const A = (meetingId) => `/api/meetings/${meetingId}/agenda`

/** Item titles and the conclusion every meeting of this plan carries, so the minutes have something to restate. */
const ITEM_DONE = 'Báo cáo tiến độ dự án quý IV'
const ITEM_CONCLUSION = 'Thống nhất thông qua báo cáo tiến độ quý IV.'
const ITEM_PENDING = 'Kế hoạch đào tạo năm 2027'

/**
 * Creates a meeting titled `title` and drives it to `status` through the API. An InProgress or Ended meeting gets an
 * agenda: one item started, concluded and closed by the Chair, one never discussed.
 */
async function createAt(qa, status, title, sensitivity = 'Normal') {
  const sec = await client(qa, 'secretary')
  const created = await sec.post('/api/meetings', await meetingBody(qa, title, sensitivity))
  qa.expectStatus(created, 201)
  const id = created.body.meeting.id
  const steps = PATHS[status]
  for (const step of steps.filter((s) => s !== 'end')) await transition(qa, actorOf(step), id, step)
  if (status !== 'Draft') {
    const done = await sec.post(A(id), { title: ITEM_DONE, description: '', presenterId: null, presenterGuestInvitationId: null })
    qa.expectStatus(done, 201)
    const pending = await sec.post(A(id), { title: ITEM_PENDING, description: '', presenterId: null, presenterGuestInvitationId: null })
    qa.expectStatus(pending, 201)
    const chair = await client(qa, 'chair')
    const item = done.body.agendaItemId
    qa.expectStatus(await chair.post(`${A(id)}/${item}/current`), 200)
    qa.expectStatus(await chair.put(`${A(id)}/${item}/conclusion`, { conclusion: ITEM_CONCLUSION }), 200)
    qa.expectStatus(await chair.post(`${A(id)}/${item}/complete`), 200)
  }
  if (steps.includes('end')) await transition(qa, 'chair', id, 'end')
  const got = (await sec.get(`/api/meetings/${id}`)).body
  qa.expect(got.status).toBe(status)
  return got
}

/** Fresh meetings shared by the criteria of one run, keyed (e.g. one flow meeting per device). */
const FRESH = {}
async function fresh(qa, key, status, label, sensitivity = 'Normal') {
  if (FRESH[key]) return FRESH[key]
  const m = await createAt(qa, status, qa.name(`FE-12 ${label} ${rand()}`), sensitivity)
  qa.note(`Tạo: ${m.title} (${m.id})`)
  FRESH[key] = m
  return m
}
/** The meeting a device's screen flow runs on: Normal, Ended, no minutes at first. */
const flow = (qa) => fresh(qa, `flow-${qa.device}`, 'Ended', `luồng biên bản ${qa.device === 'phone' ? 'điện thoại' : 'máy tính'}`)

/** Meetings reused across runs, found by their fixed title; created the first time. */
const REUSED = {}
async function reused(qa, key) {
  if (REUSED[key]) return REUSED[key]
  const spec = {
    inProgress: ['InProgress', 'Normal', 'dùng lại đang họp'],
    notStarted: ['Draft', 'Internal', 'dùng lại chưa họp (Nội bộ)'],
    secret: ['Ended', 'Secret', 'dùng lại họp Mật'],
    ai: ['Ended', 'Internal', 'dùng lại dự thảo AI (Nội bộ)'],
  }[key]
  const [status, sensitivity, label] = spec
  const title = qa.name(`FE-12 ${label}`)
  const c = await client(qa, 'secretary')
  const list = await c.get(`/api/meetings?q=${encodeURIComponent(title)}&status=${status}&pageSize=20`)
  const hit = (list.body.items ?? []).find((m) => m.title === title)
  const m = hit ? (await c.get(`/api/meetings/${hit.id}`)).body : await createAt(qa, status, title, sensitivity)
  qa.expect(m.sensitivity).toBe(sensitivity)
  REUSED[key] = m
  return m
}

// ---------------------------------------------------------------------------------------------------------------------
// Helpers: minutes API.

const M = (meetingId) => `/api/meetings/${meetingId}/minutes`
const R = (minutesId) => `/api/minutes/${minutesId}`

async function minutesState(qa, role, meetingId) {
  const res = await (await client(qa, role)).get(M(meetingId))
  qa.expectStatus(res, 200)
  return res.body
}

/**
 * Drives the latest revision of a meeting's minutes to `target` through the API, along the one path the workflow has:
 * none → Draft → WaitingApproval → Rejected → WaitingApproval → Issued → (correction) Draft → …
 */
async function driveTo(qa, meetingId, target) {
  const sec = await client(qa, 'secretary')
  const chair = await client(qa, 'chair')
  for (let i = 0; i < 8; i++) {
    const m = (await minutesState(qa, 'secretary', meetingId)).minutes
    if (m?.status === target) return m
    let res
    if (!m) res = await sec.post(M(meetingId), { rebuild: false })
    else if (m.status === 'Draft' || m.status === 'Rejected') res = await sec.post(`${R(m.id)}/submit`, { updatedAt: m.updatedAt })
    else if (m.status === 'WaitingApproval')
      res =
        target === 'Rejected'
          ? await chair.post(`${R(m.id)}/reject`, { reason: 'QA: bổ sung kết luận mục 2.', updatedAt: m.updatedAt })
          : await chair.post(`${R(m.id)}/approve`, { updatedAt: m.updatedAt })
    else if (m.status === 'Issued') res = await sec.post(`${R(m.id)}/corrections`)
    else throw new Error(`cannot drive minutes from ${m.status} to ${target}`)
    qa.expect([200, 201], `drive to ${target}: HTTP ${res.status} ${JSON.stringify(res.body).slice(0, 200)}`).toContain(res.status)
  }
  throw new Error(`minutes never reached ${target}`)
}

/** Notifications of `role` of `type` about `meetingId`. */
async function notificationsOf(qa, role, meetingId, type) {
  const res = await (await client(qa, role)).get('/api/notifications?page=1&pageSize=50')
  qa.expectStatus(res, 200)
  return (res.body.items ?? []).filter((n) => n.meetingId === meetingId && n.type === type)
}

/** Audit rows `minutes.issued` for one revision, read by the administrator. */
async function issuedAudit(qa, minutesId) {
  const res = await (await client(qa, 'admin')).get('/api/audit-log?action=minutes.issued&pageSize=100')
  qa.expectStatus(res, 200)
  return (res.body.items ?? []).filter((r) => r.objectId === minutesId)
}

/** Runs every API check, then fails once with all the wrong ones. */
function checker(qa) {
  const wrong = []
  return {
    check(label, res, status, code) {
      const ok = res.status === status && (code === undefined || (code === null ? !res.body?.code : res.body?.code === code))
      if (!ok)
        wrong.push(`${label}: want ${status}${code ? ` ${code}` : code === null ? ' (no code)' : ''}, got ${res.status} ${JSON.stringify(res.body).slice(0, 220)}`)
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
const card = (qa) => qa.page.getByRole('region', { name: 'Biên bản', exact: true })
const draftCard = (qa) => qa.page.getByRole('region', { name: 'Dự thảo nội dung', exact: true })
const badge = (qa, label) => card(qa).locator('.minutes-card-head').getByText(label, { exact: true })
const paper = (qa) => card(qa).locator('article.minutes-paper')
const btn = (qa, name) => card(qa).getByRole('button', { name, exact: true })
async function press(qa, locator) {
  if (qa.device === 'phone') await locator.tap()
  else await locator.click()
}

/**
 * Opens the meeting's "Biên bản" tab and waits for its cards to load. A Member sees the draft card only once a draft is
 * approved, so for a Member it waits only for the minutes card; a criterion that expects the draft card checks it.
 */
async function openMinutes(qa, meeting, { draft = qa.role !== 'member' } = {}) {
  await qa.goto(`/mgmt/meetings/${meeting.id}?tab=minutes`)
  await qa.expect(qa.page.getByRole('heading', { level: 1, name: meeting.title })).toBeVisible()
  await qa.expect(card(qa)).toBeVisible()
  await qa.expect(card(qa)).not.toHaveAttribute('aria-busy', 'true')
  if (draft) await qa.expect(draftCard(qa)).toBeVisible()
}

/**
 * Live notification toasts ("toast-info": other criteria and other QA runs act on the same seed accounts) stack over
 * the page, four deep on a phone. They are closed before a shot so the shot shows the card; the result toast of the
 * action under test (success, error) is kept.
 */
async function snap(qa, slug) {
  const closers = qa.page.locator('.toast-stack .toast-info .toast-close')
  // A DOM click only: a pointer press outside an open dropdown would close it.
  for (let i = await closers.count(); i > 0; i--) await closers.first().evaluate((b) => b.click()).catch(() => undefined)
  await settled(qa)
  return qa.shot(slug)
}

/**
 * Puts the caret at the end of the closing paragraph ("Cuộc họp kết thúc vào …") and types a new paragraph after it.
 * Keyboard shortcuts for "end of document" differ per platform and the document ends inside the signature table.
 */
async function typeNewParagraph(qa, editor, text) {
  await editor.evaluate((el) => {
    el.focus()
    const p = [...el.querySelectorAll('p')].reverse().find((x) => x.textContent.includes('Cuộc họp kết thúc vào'))
    const range = document.createRange()
    range.selectNodeContents(p ?? el)
    range.collapse(false)
    const sel = getSelection()
    sel.removeAllRanges()
    sel.addRange(range)
  })
  await qa.page.keyboard.press('Enter')
  await qa.page.keyboard.type(text)
}

/** The meta line under the paper ("… gửi duyệt lúc …"). */
const meta = (qa) => card(qa).locator('.minutes-meta')

// ---------------------------------------------------------------------------------------------------------------------

export default {
  ticket: 'SCRUM-199',
  title: 'FE-12 Biên bản & Duyệt, ban hành: bản nháp từ mẫu, dự thảo AI, sửa, gửi duyệt, trả lại, duyệt ban hành, đính chính',
  devices: ['desktop'],
  criteria: [
    // ------------------------------------------------------------------------------------------- UC-59 Generate
    {
      id: 'AC1',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Thư ký tạo bản nháp biên bản từ mẫu cố định; bản nháp điền sẵn hồ sơ cuộc họp, mục chưa có dự thảo để "……"',
      steps: [
        'Đăng nhập thuky@, mở tab "Biên bản" của cuộc họp đã kết thúc chưa có biên bản (chưa có dự thảo AI)',
        'Bấm "Tạo bản nháp"',
      ],
      expected:
        'Trước: "Bản nháp điền sẵn thông tin cuộc họp…", báo "Chưa có dự thảo AI nên mục I–III để trống."; sau: toast "Đã tạo bản nháp biên bản." (MSG395), chip "Bản nháp", tờ biên bản có tên cuộc họp, Chủ toạ, Thư ký, mục nghị sự và kết luận Chủ toạ đã ghi, chỗ "……"; nút Sửa, Gửi duyệt, Tạo lại, Tải xuống; API Draft, bản số 1; không cuộn ngang',
      async run(qa) {
        const u = await users(qa)
        let m = await flow(qa)
        if ((await minutesState(qa, 'secretary', m.id)).minutes) {
          FRESH[`flow-${qa.device}`] = undefined
          m = await flow(qa)
        }
        await openMinutes(qa, m)
        await qa.expect(card(qa).getByText('Bản nháp điền sẵn thông tin cuộc họp và dự thảo vào mẫu biên bản.')).toBeVisible()
        await qa.expect(card(qa).getByText('Chưa có dự thảo AI nên mục I–III để trống.')).toBeVisible()
        await qa.expect(paper(qa)).toHaveCount(0)
        await snap(qa, 'chua-co-bien-ban')
        await press(qa, btn(qa, 'Tạo bản nháp'))
        await qa.expect(toast(qa, 'Đã tạo bản nháp biên bản.')).toBeVisible()
        await qa.expect(badge(qa, 'Bản nháp')).toBeVisible()
        const p = paper(qa)
        await qa.expect(p).toBeVisible()
        for (const text of [m.title, u.chair.name, u.secretary.name, ITEM_DONE, ITEM_CONCLUSION])
          await qa.expect(p, `the paper misses "${text}"`).toContainText(text)
        await qa.expect(p).toContainText('……')
        for (const name of ['Sửa', 'Gửi duyệt', 'Tạo lại', 'Tải xuống']) await qa.expect(btn(qa, name)).toBeVisible()
        await qa.expect(meta(qa)).toContainText('Lưu lần cuối')
        await snap(qa, 'da-tao-ban-nhap')
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
        await p.scrollIntoViewIfNeeded()
        await snap(qa, 'to-bien-ban')
        const st = await minutesState(qa, 'secretary', m.id)
        qa.expect(st.minutes.status).toBe('Draft')
        qa.expect(st.minutes.revisionNumber).toBe(1)
        qa.expect(st.minutes.builtFromDraftAt).toBeNull()
        qa.expect(st.minutes.createdBy?.id ?? st.minutes.createdBy).toBeTruthy()
      },
    },
    {
      id: 'AC2',
      role: 'chair',
      title: 'API tạo bản nháp: đã có thì 409 MSG399; tạo lại thay toàn bộ nội dung (MSG395); chỉ khi đang họp hoặc đã kết thúc (MSG398); người khác 403 MSG397',
      steps: [
        'Chủ toạ POST …/minutes trên cuộc họp đã kết thúc, rồi POST lần nữa không rebuild',
        'Thư ký sửa nội dung (PUT) rồi Chủ toạ POST rebuild: true',
        'POST trên cuộc họp đang họp; trên cuộc họp Nháp (chưa họp)',
        'thanhvien@ POST trước và sau khi có bản nháp',
      ],
      expected:
        '201 MSG395; 409 MSG399 bản cũ giữ nguyên; tạo lại 201 MSG395, đoạn đã sửa mất (BR-388); đang họp 201 hoặc 409 MSG399 (đã có từ lần chạy trước); chưa họp 409 MSG398; thành viên 403 MSG397 cả hai lần (không lộ bản nháp)',
      async run(qa) {
        const c = checker(qa)
        const chair = await client(qa, 'chair')
        const sec = await client(qa, 'secretary')
        const member = await client(qa, 'member')
        const m = await fresh(qa, 'api-draft', 'Ended', 'API bản nháp')
        const before = await member.post(M(m.id), { rebuild: false })
        const first = await chair.post(M(m.id), { rebuild: false })
        if (c.check('tạo lần đầu', first, 201, 'MSG395')) c.fact('message', first.body.message === 'Đã tạo bản nháp biên bản.', first.body.message)
        const again = await chair.post(M(m.id), { rebuild: false })
        c.check('tạo lần hai', again, 409, 'MSG399')
        const after = await member.post(M(m.id), { rebuild: false })
        c.check('thành viên khi chưa có', before, 403, 'MSG397')
        c.check('thành viên khi đã có bản nháp', after, 403, 'MSG397')
        const cur = (await minutesState(qa, 'secretary', m.id)).minutes
        const marker = `Đoạn QA sẽ mất khi tạo lại ${rand()}`
        const saved = await sec.put(R(cur.id), { content: `${cur.content}<p>${marker}</p>`, updatedAt: cur.updatedAt })
        c.check('thư ký lưu', saved, 200, 'MSG396')
        const rebuilt = await chair.post(M(m.id), { rebuild: true })
        if (c.check('tạo lại', rebuilt, 201, 'MSG395')) {
          c.fact('tạo lại xoá phần đã sửa', !rebuilt.body.minutes.content.includes(marker))
          c.fact('vẫn là bản số 1, cùng id', rebuilt.body.minutes.revisionNumber === 1 && rebuilt.body.minutes.id === cur.id, JSON.stringify(rebuilt.body.minutes).slice(0, 120))
        }
        const ip = await reused(qa, 'inProgress')
        const live = await chair.post(M(ip.id), { rebuild: false })
        c.fact('đang họp: tạo được', live.status === 201 || (live.status === 409 && live.body.code === 'MSG399'), `${live.status} ${live.body?.code}`)
        const ns = await reused(qa, 'notStarted')
        c.check('cuộc họp chưa họp', await chair.post(M(ns.id), { rebuild: false }), 409, 'MSG398')
        c.done()
      },
    },

    // ------------------------------------------------------------------------------------------- UC-60 Edit
    {
      id: 'AC3',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Thư ký sửa bản nháp trong trình soạn kiểu Word và lưu; tờ biên bản hiện nội dung mới',
      steps: ['Mở tab Biên bản của bản nháp', 'Bấm "Sửa"', 'Thêm một đoạn ở cuối', 'Bấm "Lưu"'],
      expected:
        'Trình soạn có thanh "Định dạng biên bản" và ô "Nội dung biên bản"; toast "Đã lưu biên bản." (MSG396); đóng trình soạn, tờ biên bản có đoạn mới; API lưu đoạn đó; trên điện thoại thanh công cụ không làm trang cuộn ngang',
      async run(qa) {
        const m = await flow(qa)
        const before = await driveTo(qa, m.id, 'Draft')
        const cols = (html) => [...html.matchAll(/<colgroup>.*?<\/colgroup>/g)].map((x) => x[0])
        await openMinutes(qa, m)
        await press(qa, btn(qa, 'Sửa'))
        const editor = card(qa).getByRole('textbox', { name: 'Nội dung biên bản' })
        await qa.expect(editor).toBeVisible()
        await qa.expect(card(qa).getByRole('toolbar', { name: 'Định dạng biên bản' })).toBeVisible()
        await snap(qa, 'trinh-soan-mo')
        await noSidewaysScroll(qa)
        // The ribbon must let every tool be reached: wrapped, or scrolled sideways inside itself.
        const ribbon = await card(qa).getByRole('toolbar', { name: 'Định dạng biên bản' }).evaluate((el) => {
          const hidden = [...el.querySelectorAll('button')].filter((b) => {
            const r = b.getBoundingClientRect()
            return r.width > 0 && (r.right > innerWidth || r.left < 0)
          }).length
          let scroller = null
          for (let n = el.querySelector('button')?.parentElement; n && n !== el.parentElement; n = n.parentElement)
            if (['auto', 'scroll'].includes(getComputedStyle(n).overflowX) && n.scrollWidth > n.clientWidth) scroller = n
          return { hidden, scrolls: !!scroller }
        })
        if (ribbon.hidden > 0 && !ribbon.scrolls) qa.expect(ribbon.hidden, 'toolbar buttons fall off the screen with no way to scroll to them').toBe(0)
        if (ribbon.hidden > 0) qa.note(`Điện thoại: ${ribbon.hidden} nút của thanh công cụ nằm ngoài màn hình, vuốt ngang trong thanh để tới`)
        const line = `Thư ký bổ sung: giao Phòng Kế hoạch tổng hợp số liệu ${rand()}.`
        await typeNewParagraph(qa, editor, line)
        await qa.expect(editor).toContainText(line)
        await snap(qa, 'da-go-doan-moi')
        await press(qa, card(qa).getByRole('button', { name: 'Lưu', exact: true }))
        await qa.expect(toast(qa, 'Đã lưu biên bản.')).toBeVisible()
        await qa.expect(editor).toHaveCount(0)
        await qa.expect(paper(qa)).toContainText(line)
        await snap(qa, 'da-luu')
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
        const st = await minutesState(qa, 'secretary', m.id)
        qa.expect(st.minutes.content).toContain(line)
        qa.expect(st.minutes.content.indexOf(line), 'the new paragraph is not after the closing line').toBeGreaterThan(st.minutes.content.indexOf('Cuộc họp kết thúc vào'))
        qa.expect(st.minutes.status).toBe('Draft')
        // Only a paragraph was added: the template's tables (header, signature block) keep their column widths, which
        // the page, the Word file and the PDF print from (ADR-0055).
        qa.expect(cols(st.minutes.content), 'saving from the editor changed the column widths of the template tables').toEqual(cols(before.content))
      },
    },
    {
      id: 'AC4',
      role: 'secretary',
      title: 'API lưu bản nháp: trống, quá 300.000 ký tự → 400 MSG402; bản cũ → 409 MSG401; HTML bị lọc (BR-385); thành viên 404',
      steps: [
        'PUT nội dung "<p> </p><br>"; nội dung 300.001 ký tự',
        'Chủ toạ lưu, rồi Thư ký lưu với updatedAt cũ',
        'Lưu nội dung có <script>, onclick, <img>, href',
        'thanhvien@ PUT và GET /api/minutes/{id} của bản nháp',
      ],
      expected:
        '400 MSG402 hai lần, không lưu; 409 MSG401 "Biên bản vừa được người khác sửa…", bản Chủ toạ giữ nguyên; lưu 200 MSG396, nội dung trả về không còn script/onclick/img/href; thành viên 404 không mã',
      async run(qa) {
        const c = checker(qa)
        const sec = await client(qa, 'secretary')
        const chair = await client(qa, 'chair')
        const member = await client(qa, 'member')
        const m = await fresh(qa, 'api-draft', 'Ended', 'API bản nháp')
        const cur = await driveTo(qa, m.id, 'Draft')
        c.check('trống', await sec.put(R(cur.id), { content: '<p> </p><br>', updatedAt: cur.updatedAt }), 400, 'MSG402')
        c.check('quá dài', await sec.put(R(cur.id), { content: `<p>${'a'.repeat(300_001)}</p>`, updatedAt: cur.updatedAt }), 400, 'MSG402')
        const chairLine = `Chủ toạ sửa ${rand()}`
        const byChair = await chair.put(R(cur.id), { content: `${cur.content}<p>${chairLine}</p>`, updatedAt: cur.updatedAt })
        c.check('chủ toạ lưu', byChair, 200, 'MSG396')
        const stale = await sec.put(R(cur.id), { content: '<p>Bản cũ của thư ký</p>', updatedAt: cur.updatedAt })
        if (c.check('bản cũ', stale, 409, 'MSG401'))
          c.fact('message MSG401', stale.body.message === 'Biên bản vừa được người khác sửa. Tải lại để xem bản mới nhất.', stale.body.message)
        const now = (await minutesState(qa, 'secretary', m.id)).minutes
        c.fact('bản Chủ toạ giữ nguyên', now.content.includes(chairLine))
        const dirty =
          '<p onclick="alert(1)">Đoạn có sự kiện</p><script>alert(2)</script><p><a href="https://evil.invalid">liên kết</a></p><img src="x" onerror="alert(3)"><p>Kết thúc QA</p>'
        const filtered = await sec.put(R(cur.id), { content: dirty, updatedAt: now.updatedAt })
        if (c.check('lưu HTML bẩn', filtered, 200, 'MSG396')) {
          const html = filtered.body.minutes.content
          for (const bad of ['<script', 'onclick', 'onerror', '<img', 'href', 'alert(2)'])
            c.fact(`đã lọc ${bad}`, !html.includes(bad), html.slice(0, 200))
          c.fact('giữ chữ', html.includes('Đoạn có sự kiện') && html.includes('liên kết') && html.includes('Kết thúc QA'), html.slice(0, 200))
        }
        c.check('thành viên PUT bản nháp', await member.put(R(cur.id), { content: '<p>x</p>', updatedAt: now.updatedAt }), 404, null)
        c.check('thành viên GET bản nháp', await member.get(R(cur.id)), 404, null)
        c.done()
      },
    },
    {
      id: 'AC5',
      role: 'secretary',
      title: 'Tải bản nháp dạng Word và PDF (Thư ký); định dạng khác 400 MSG406; thành viên không tải được bản nháp',
      steps: [
        'Mở tab Biên bản của bản nháp, bấm "Tải xuống" → "Word"',
        'GET …/export, …/export?format=pdf, …/export?format=xlsx',
        'thanhvien@ GET …/export của bản nháp',
      ],
      expected:
        'Menu có Word và PDF; trình duyệt nhận file "Biên bản - <tên cuộc họp>.docx"; API docx và pdf 200 (application/pdf); xlsx 400 MSG406; thành viên 404',
      async run(qa) {
        const c = checker(qa)
        const sec = await client(qa, 'secretary')
        const member = await client(qa, 'member')
        const m = await fresh(qa, 'api-draft', 'Ended', 'API bản nháp')
        const cur = await driveTo(qa, m.id, 'Draft')
        await openMinutes(qa, m)
        await btn(qa, 'Tải xuống').click()
        const menu = qa.page.getByRole('menu', { name: 'Tải xuống' })
        await qa.expect(menu.getByRole('menuitem', { name: /Word/ })).toBeVisible()
        await qa.expect(menu.getByRole('menuitem', { name: /PDF/ })).toBeVisible()
        await snap(qa, 'menu-tai-xuong')
        const [download] = await Promise.all([qa.page.waitForEvent('download'), menu.getByRole('menuitem', { name: /Word/ }).click()])
        c.fact('tên file Word', download.suggestedFilename() === `Biên bản - ${m.title}.docx`, download.suggestedFilename())
        const docx = await sec.get(`${R(cur.id)}/export`)
        c.check('docx', docx, 200)
        c.fact('docx content-type', /wordprocessingml/.test(docx.headers['content-type'] ?? ''), docx.headers['content-type'])
        const pdf = await sec.get(`${R(cur.id)}/export?format=pdf`)
        c.check('pdf', pdf, 200)
        c.fact('pdf content-type', (pdf.headers['content-type'] ?? '').includes('application/pdf'), pdf.headers['content-type'])
        const bad = await sec.get(`${R(cur.id)}/export?format=xlsx`)
        if (c.check('xlsx', bad, 400, 'MSG406')) c.fact('message MSG406', bad.body.message === 'Chỉ tải biên bản dạng Word (docx) hoặc PDF.', bad.body.message)
        c.check('thành viên tải bản nháp', await member.get(`${R(cur.id)}/export`), 404, null)
        c.done()
      },
    },

    // ------------------------------------------------------------------------------------------- UC-61 Submit
    {
      id: 'AC6',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Thư ký gửi duyệt bản nháp; màn hình cảnh báo chỗ "……" còn trống; biên bản khoá, Chủ toạ nhận thông báo',
      steps: ['Mở tab Biên bản của bản nháp (cuộc họp đã kết thúc)', 'Bấm "Gửi duyệt"', 'Bấm "Gửi duyệt" trong hộp thoại'],
      expected:
        'Hộp thoại "Gửi biên bản cho Chủ toạ duyệt?" có câu "Biên bản còn chỗ “……” chưa điền."; toast "Đã gửi biên bản cho Chủ toạ duyệt." (MSG424); chip "Chờ duyệt"; không còn Sửa, Tạo lại, Gửi duyệt; dòng "<Thư ký> gửi duyệt lúc …"; API WaitingApproval, submittedBy Thư ký; cuộc họp vẫn Đã kết thúc; Chủ toạ có thông báo MinutesWaitingApproval',
      async run(qa) {
        const u = await users(qa)
        const m = await flow(qa)
        const cur = await driveTo(qa, m.id, 'Draft')
        await openMinutes(qa, m)
        await press(qa, btn(qa, 'Gửi duyệt'))
        const d = dialog(qa, 'Gửi biên bản cho Chủ toạ duyệt?')
        await qa.expect(d).toBeVisible()
        if (cur.content.includes('……')) await qa.expect(d.getByText('Biên bản còn chỗ “……” chưa điền.')).toBeVisible()
        else qa.note('Bản nháp không còn "……" nên không có cảnh báo chỗ trống')
        await snap(qa, 'hoi-gui-duyet')
        await press(qa, d.getByRole('button', { name: 'Gửi duyệt', exact: true }))
        await qa.expect(toast(qa, 'Đã gửi biên bản cho Chủ toạ duyệt.')).toBeVisible()
        await qa.expect(badge(qa, 'Chờ duyệt')).toBeVisible()
        for (const name of ['Sửa', 'Tạo lại', 'Gửi duyệt', 'Duyệt và ban hành', 'Trả lại']) await qa.expect(btn(qa, name)).toHaveCount(0)
        await qa.expect(meta(qa)).toContainText(`${u.secretary.name} gửi duyệt lúc`)
        await snap(qa, 'cho-duyet')
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
        const st = await minutesState(qa, 'secretary', m.id)
        qa.expect(st.minutes.status).toBe('WaitingApproval')
        qa.expect(st.minutes.submittedBy?.id).toBe(u.secretary.id)
        qa.expect((await (await client(qa, 'secretary')).get(`/api/meetings/${m.id}`)).body.status).toBe('Ended')
        qa.expect((await notificationsOf(qa, 'chair', m.id, 'MinutesWaitingApproval')).length, 'the Chair has no MinutesWaitingApproval').toBeGreaterThan(0)
      },
    },
    {
      id: 'AC7',
      role: 'secretary',
      title: 'Gửi duyệt sai người, sai lúc: Chủ toạ 403 MSG425; đang họp 409 MSG426 (màn hình chặn trước); bản cũ 409 MSG401; đã gửi 409 MSG400; thành viên 404',
      steps: [
        'Chủ toạ POST submit bản nháp',
        'Thư ký POST submit với updatedAt cũ; rồi đúng; rồi lần nữa',
        'Khi chờ duyệt: Thư ký PUT nội dung, Chủ toạ POST rebuild',
        'thanhvien@ POST submit',
        'Cuộc họp đang họp: API submit; trên màn hình bấm Gửi duyệt',
      ],
      expected:
        '403 MSG425; 409 MSG401; 200 MSG424; 409 MSG400 (gửi lại, sửa, tạo lại); thành viên 404 không mã; đang họp 409 MSG426, hộp thoại báo "Chỉ gửi duyệt biên bản sau khi cuộc họp kết thúc." và nút xác nhận bị khoá',
      async run(qa) {
        const c = checker(qa)
        const sec = await client(qa, 'secretary')
        const chair = await client(qa, 'chair')
        const member = await client(qa, 'member')
        const m = await fresh(qa, 'api-approval', 'Ended', 'API duyệt')
        const cur = await driveTo(qa, m.id, 'Draft')
        c.check('chủ toạ gửi duyệt', await chair.post(`${R(cur.id)}/submit`, { updatedAt: cur.updatedAt }), 403, 'MSG425')
        c.check('thành viên gửi duyệt', await member.post(`${R(cur.id)}/submit`, { updatedAt: cur.updatedAt }), 404, null)
        const saved = await chair.put(R(cur.id), { content: `${cur.content}<p>Chủ toạ thêm ${rand()}</p>`, updatedAt: cur.updatedAt })
        c.check('chủ toạ lưu', saved, 200, 'MSG396')
        c.check('bản cũ', await sec.post(`${R(cur.id)}/submit`, { updatedAt: cur.updatedAt }), 409, 'MSG401')
        const ok = await sec.post(`${R(cur.id)}/submit`, { updatedAt: saved.body.minutes.updatedAt })
        if (c.check('gửi duyệt', ok, 200, 'MSG424')) c.fact('message MSG424', ok.body.message === 'Đã gửi biên bản cho Chủ toạ duyệt.', ok.body.message)
        const waiting = (await minutesState(qa, 'secretary', m.id)).minutes
        c.check('gửi lần nữa', await sec.post(`${R(cur.id)}/submit`, { updatedAt: waiting.updatedAt }), 409, 'MSG400')
        c.check('sửa khi chờ duyệt', await sec.put(R(cur.id), { content: '<p>x</p>', updatedAt: waiting.updatedAt }), 409, 'MSG400')
        c.check('tạo lại khi chờ duyệt', await chair.post(M(m.id), { rebuild: true }), 409, 'MSG400')
        const ip = await reused(qa, 'inProgress')
        await chair.post(M(ip.id), { rebuild: false })
        const live = (await minutesState(qa, 'secretary', ip.id)).minutes
        c.fact('cuộc họp đang họp có bản nháp', live?.status === 'Draft', live?.status)
        if (live?.status === 'Draft') {
          c.check('gửi duyệt khi đang họp', await sec.post(`${R(live.id)}/submit`, { updatedAt: live.updatedAt }), 409, 'MSG426')
          await openMinutes(qa, ip)
          await btn(qa, 'Gửi duyệt').click()
          const d = dialog(qa, 'Gửi biên bản cho Chủ toạ duyệt?')
          await qa.expect(d.getByText('Chỉ gửi duyệt biên bản sau khi cuộc họp kết thúc.')).toBeVisible()
          await qa.expect(d.getByRole('button', { name: 'Gửi duyệt', exact: true })).toBeDisabled()
          await snap(qa, 'dang-hop-chua-gui-duoc')
          c.fact('vẫn là bản nháp', (await minutesState(qa, 'secretary', ip.id)).minutes.status === 'Draft')
        }
        c.done()
      },
    },

    // ------------------------------------------------------------------------------------------- UC-65 Return
    {
      id: 'AC8',
      role: 'chair',
      devices: ['desktop', 'phone'],
      title: 'Chủ toạ trả lại biên bản chờ duyệt kèm lý do; bỏ trống lý do thì báo tại ô; Thư ký nhận thông báo',
      steps: [
        'Đăng nhập chutoa@, mở tab Biên bản đang chờ duyệt',
        'Bấm "Trả lại", để trống lý do, bấm "Trả lại"',
        'Nhập lý do, bấm "Trả lại"',
      ],
      expected:
        'Có "Trả lại" và "Duyệt và ban hành", không có Sửa; lý do trống báo "Nhập lý do trả lại, tối đa 2.000 ký tự." và không gửi gì; toast "Đã trả lại biên bản cho Thư ký." (MSG430); chip "Bị trả lại"; API Rejected, rejectionReason đúng, returnedBy Chủ toạ; Thư ký có thông báo MinutesReturned',
      async run(qa) {
        const u = await users(qa)
        const m = await flow(qa)
        await driveTo(qa, m.id, 'WaitingApproval')
        await openMinutes(qa, m)
        await qa.expect(badge(qa, 'Chờ duyệt')).toBeVisible()
        await qa.expect(btn(qa, 'Duyệt và ban hành')).toBeVisible()
        await qa.expect(btn(qa, 'Sửa')).toHaveCount(0)
        await qa.expect(btn(qa, 'Gửi duyệt')).toHaveCount(0)
        await snap(qa, 'chu-toa-thay-cho-duyet')
        await noSidewaysScroll(qa)
        await press(qa, btn(qa, 'Trả lại'))
        const d = dialog(qa, 'Trả lại biên bản')
        await qa.expect(d).toBeVisible()
        let posts = 0
        qa.page.on('request', (r) => r.method() === 'POST' && r.url().endsWith('/reject') && posts++)
        await press(qa, d.getByRole('button', { name: 'Trả lại', exact: true }))
        await qa.expect(d.getByText('Nhập lý do trả lại, tối đa 2.000 ký tự.')).toBeVisible()
        await qa.expect(d.getByRole('textbox')).toHaveAttribute('aria-invalid', 'true')
        await snap(qa, 'thieu-ly-do')
        qa.expect(posts, 'an empty reason was sent').toBe(0)
        const reason = `Bổ sung kết luận mục 2 và danh sách vắng mặt ${rand()}.`
        await d.getByRole('textbox').fill(reason)
        await press(qa, d.getByRole('button', { name: 'Trả lại', exact: true }))
        await qa.expect(toast(qa, 'Đã trả lại biên bản cho Thư ký.')).toBeVisible()
        await qa.expect(d).toHaveCount(0)
        await qa.expect(badge(qa, 'Bị trả lại')).toBeVisible()
        await snap(qa, 'da-tra-lai')
        await noRawCodes(qa)
        const st = await minutesState(qa, 'secretary', m.id)
        qa.expect(st.minutes.status).toBe('Rejected')
        qa.expect(st.minutes.rejectionReason).toBe(reason)
        qa.expect(st.minutes.returnedBy?.id).toBe(u.chair.id)
        qa.expect((await notificationsOf(qa, 'secretary', m.id, 'MinutesReturned')).length, 'the Secretary has no MinutesReturned').toBeGreaterThan(0)
      },
    },
    {
      id: 'AC9',
      role: 'secretary',
      title: 'Thư ký thấy lý do trả lại phía trên tờ biên bản, sửa và gửi duyệt lại',
      steps: ['Mở tab Biên bản bị trả lại', 'Đọc lý do', 'Bấm Sửa, thêm một đoạn, Lưu', 'Bấm Gửi duyệt và xác nhận'],
      expected:
        'Khung "<Chủ toạ> trả lại: <lý do>"; Sửa và Gửi duyệt có; lưu MSG396, chip vẫn "Bị trả lại"; gửi duyệt MSG424, chip "Chờ duyệt", khung lý do biến mất',
      async run(qa) {
        const u = await users(qa)
        const m = await flow(qa)
        const cur = await driveTo(qa, m.id, 'Rejected')
        await openMinutes(qa, m)
        await qa.expect(badge(qa, 'Bị trả lại')).toBeVisible()
        const banner = card(qa).locator('.minutes-returned')
        await qa.expect(banner).toContainText(`${u.chair.name} trả lại:`)
        await qa.expect(banner).toContainText(cur.rejectionReason)
        await snap(qa, 'thay-ly-do-tra-lai')
        await btn(qa, 'Sửa').click()
        const editor = card(qa).getByRole('textbox', { name: 'Nội dung biên bản' })
        await qa.expect(editor).toBeVisible()
        const line = `Đã bổ sung kết luận mục 2 ${rand()}.`
        await typeNewParagraph(qa, editor, line)
        await card(qa).getByRole('button', { name: 'Lưu', exact: true }).click()
        await qa.expect(toast(qa, 'Đã lưu biên bản.')).toBeVisible()
        await qa.expect(paper(qa)).toContainText(line)
        await qa.expect(badge(qa, 'Bị trả lại')).toBeVisible()
        await btn(qa, 'Gửi duyệt').click()
        await dialog(qa, 'Gửi biên bản cho Chủ toạ duyệt?').getByRole('button', { name: 'Gửi duyệt', exact: true }).click()
        await qa.expect(toast(qa, 'Đã gửi biên bản cho Chủ toạ duyệt.')).toBeVisible()
        await qa.expect(badge(qa, 'Chờ duyệt')).toBeVisible()
        await qa.expect(card(qa).locator('.minutes-returned')).toHaveCount(0)
        await snap(qa, 'gui-duyet-lai')
        qa.expect((await minutesState(qa, 'secretary', m.id)).minutes.status).toBe('WaitingApproval')
      },
    },
    {
      id: 'AC10',
      role: 'chair',
      title: 'API duyệt, trả lại: Thư ký 403 MSG428; lý do trống/quá 2.000 → 400 MSG431; không chờ duyệt 409 MSG429; bản cũ 409 MSG401; lý do mới thay lý do cũ; thành viên 404',
      steps: [
        'Thư ký POST approve và reject bản chờ duyệt; thanhvien@ POST approve',
        'Chủ toạ reject với lý do "   " và 2.001 ký tự; approve với updatedAt cũ',
        'Chủ toạ trả lại (lý do A), Thư ký gửi lại, Chủ toạ trả lại (lý do B)',
        'Chủ toạ approve và reject bản đang Bị trả lại',
      ],
      expected:
        '403 MSG428 hai lần; thành viên 404 không mã; 400 MSG431 hai lần; 409 MSG401; sau lần trả lại thứ hai rejectionReason là B; approve/reject bản bị trả lại 409 MSG429',
      async run(qa) {
        const c = checker(qa)
        const sec = await client(qa, 'secretary')
        const chair = await client(qa, 'chair')
        const member = await client(qa, 'member')
        const m = await fresh(qa, 'api-approval', 'Ended', 'API duyệt')
        const cur = await driveTo(qa, m.id, 'WaitingApproval')
        c.check('thư ký duyệt', await sec.post(`${R(cur.id)}/approve`, { updatedAt: cur.updatedAt }), 403, 'MSG428')
        c.check('thư ký trả lại', await sec.post(`${R(cur.id)}/reject`, { reason: 'x', updatedAt: cur.updatedAt }), 403, 'MSG428')
        c.check('thành viên duyệt', await member.post(`${R(cur.id)}/approve`, { updatedAt: cur.updatedAt }), 404, null)
        c.check('thành viên trả lại', await member.post(`${R(cur.id)}/reject`, { reason: 'x', updatedAt: cur.updatedAt }), 404, null)
        const blank = await chair.post(`${R(cur.id)}/reject`, { reason: '   ', updatedAt: cur.updatedAt })
        if (c.check('lý do trống', blank, 400, 'MSG431')) c.fact('message MSG431', blank.body.message === 'Nhập lý do trả lại, tối đa 2.000 ký tự.', blank.body.message)
        c.check('lý do 2.001 ký tự', await chair.post(`${R(cur.id)}/reject`, { reason: 'a'.repeat(2001), updatedAt: cur.updatedAt }), 400, 'MSG431')
        c.check('duyệt bản cũ', await chair.post(`${R(cur.id)}/approve`, { updatedAt: '2020-01-01T00:00:00Z' }), 409, 'MSG401')
        const first = await chair.post(`${R(cur.id)}/reject`, { reason: 'Lý do A: thiếu kết luận.', updatedAt: cur.updatedAt })
        if (c.check('trả lại lần 1', first, 200, 'MSG430')) c.fact('message MSG430', first.body.message === 'Đã trả lại biên bản cho Thư ký.', first.body.message)
        const rejected = (await minutesState(qa, 'chair', m.id)).minutes
        c.check('duyệt bản bị trả lại', await chair.post(`${R(cur.id)}/approve`, { updatedAt: rejected.updatedAt }), 409, 'MSG429')
        c.check('trả lại bản bị trả lại', await chair.post(`${R(cur.id)}/reject`, { reason: 'x', updatedAt: rejected.updatedAt }), 409, 'MSG429')
        c.check('gửi lại', await sec.post(`${R(cur.id)}/submit`, { updatedAt: rejected.updatedAt }), 200, 'MSG424')
        const again = (await minutesState(qa, 'chair', m.id)).minutes
        c.check('trả lại lần 2', await chair.post(`${R(cur.id)}/reject`, { reason: 'Lý do B: sửa giờ kết thúc.', updatedAt: again.updatedAt }), 200, 'MSG430')
        const last = (await minutesState(qa, 'secretary', m.id)).minutes
        c.fact('lý do mới thay lý do cũ', last.rejectionReason === 'Lý do B: sửa giờ kết thúc.', last.rejectionReason)
        c.done()
      },
    },

    // ------------------------------------------------------------------------------------------- UC-64 Approve & issue
    {
      id: 'AC11',
      role: 'chair',
      devices: ['desktop', 'phone'],
      title: 'Chủ toạ duyệt và ban hành; biên bản khoá vĩnh viễn, người dự họp (trừ Chủ toạ) nhận thông báo, nhật ký ghi lại',
      steps: ['Mở tab Biên bản đang chờ duyệt', 'Bấm "Duyệt và ban hành"', 'Xác nhận "Duyệt và ban hành"'],
      expected:
        'Toast "Đã duyệt và ban hành biên bản." (MSG427); chip "Đã ban hành"; chỉ còn Tải xuống và Tạo bản đính chính; dòng "<Chủ toạ> duyệt, ban hành lúc …"; API Issued, approvedBy Chủ toạ, issuedAt; cuộc họp vẫn Đã kết thúc; Thư ký và Thành viên có MinutesIssued, Chủ toạ không; nhật ký minutes.issued; duyệt lại 409 MSG429, sửa/tạo lại 409 MSG400',
      async run(qa) {
        const u = await users(qa)
        const m = await flow(qa)
        await driveTo(qa, m.id, 'WaitingApproval')
        await openMinutes(qa, m)
        await press(qa, btn(qa, 'Duyệt và ban hành'))
        const d = dialog(qa, 'Duyệt và ban hành biên bản?')
        await qa.expect(d).toBeVisible()
        await snap(qa, 'hoi-duyet')
        await press(qa, d.getByRole('button', { name: 'Duyệt và ban hành', exact: true }))
        await qa.expect(toast(qa, 'Đã duyệt và ban hành biên bản.')).toBeVisible()
        await qa.expect(badge(qa, 'Đã ban hành')).toBeVisible()
        for (const name of ['Sửa', 'Tạo lại', 'Gửi duyệt', 'Duyệt và ban hành', 'Trả lại']) await qa.expect(btn(qa, name)).toHaveCount(0)
        await qa.expect(btn(qa, 'Tạo bản đính chính')).toBeVisible()
        await qa.expect(btn(qa, 'Tải xuống')).toBeVisible()
        await qa.expect(meta(qa)).toContainText(`${u.chair.name} duyệt, ban hành lúc`)
        await snap(qa, 'da-ban-hanh')
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
        const c = checker(qa)
        const st = await minutesState(qa, 'chair', m.id)
        c.fact('Issued', st.minutes.status === 'Issued', st.minutes.status)
        c.fact('approvedBy', st.minutes.approvedBy?.id === u.chair.id, JSON.stringify(st.minutes.approvedBy))
        c.fact('issuedAt', !!st.minutes.issuedAt)
        c.fact('cuộc họp vẫn Ended', (await (await client(qa, 'chair')).get(`/api/meetings/${m.id}`)).body.status === 'Ended')
        c.fact('Thư ký nhận MinutesIssued', (await notificationsOf(qa, 'secretary', m.id, 'MinutesIssued')).length > 0)
        c.fact('Thành viên nhận MinutesIssued', (await notificationsOf(qa, 'member', m.id, 'MinutesIssued')).length > 0)
        c.fact('Chủ toạ không tự nhận MinutesIssued', (await notificationsOf(qa, 'chair', m.id, 'MinutesIssued')).length === 0)
        c.fact('nhật ký minutes.issued', (await issuedAudit(qa, st.minutes.id)).length === 1)
        const chair = await client(qa, 'chair')
        c.check('duyệt lại', await chair.post(`${R(st.minutes.id)}/approve`, { updatedAt: st.minutes.updatedAt }), 409, 'MSG429')
        c.check('sửa bản đã ban hành', await chair.put(R(st.minutes.id), { content: '<p>x</p>', updatedAt: st.minutes.updatedAt }), 409, 'MSG400')
        c.check('tạo lại bản đã ban hành', await chair.post(M(m.id), { rebuild: true }), 409, 'MSG400')
        c.done()
      },
    },

    // ------------------------------------------------------------------------------------------- UC-58 What members see
    {
      id: 'AC12',
      role: 'member',
      devices: ['desktop', 'phone'],
      title: 'Thành viên không thấy bản nháp, bản chờ duyệt hay bị trả lại, cũng không thấy dự thảo AI chưa duyệt',
      steps: [
        'Cuộc họp đã kết thúc có biên bản đang chờ duyệt (lần gửi thứ hai, đã từng bị trả lại)',
        'Đăng nhập thanhvien@, mở tab Biên bản',
        'API: GET …/minutes, GET /api/minutes/{id}, GET …/summary',
      ],
      expected:
        'Thẻ Biên bản: "Chưa có biên bản ban hành.", không tờ biên bản, không nút nào; không có thẻ Dự thảo (chưa có dự thảo đã duyệt); API minutes null, history [], GET bản đó 404 không mã; summary null, canManage false',
      async run(qa) {
        const m = await fresh(qa, 'api-approval', 'Ended', 'API duyệt')
        const cur = await driveTo(qa, m.id, 'WaitingApproval')
        await openMinutes(qa, m)
        await qa.expect(card(qa).getByText('Chưa có biên bản ban hành.')).toBeVisible()
        await qa.expect(paper(qa)).toHaveCount(0)
        await qa.expect(card(qa).getByRole('button')).toHaveCount(0)
        for (const label of ['Bản nháp', 'Chờ duyệt', 'Bị trả lại']) await qa.expect(badge(qa, label)).toHaveCount(0)
        await qa.expect(draftCard(qa)).toHaveCount(0)
        await snap(qa, 'thanh-vien-chua-thay-gi')
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
        const c = checker(qa)
        const st = await minutesState(qa, 'member', m.id)
        c.fact('minutes null', st.minutes === null, JSON.stringify(st.minutes)?.slice(0, 100))
        c.fact('history rỗng', Array.isArray(st.history) && st.history.length === 0, JSON.stringify(st.history))
        c.fact('canManage, canSubmit, canApprove false', !st.canManage && !st.canSubmit && !st.canApprove)
        c.fact('draftChangedAt null', st.draftChangedAt === null)
        c.check('GET bản chờ duyệt', await (await client(qa, 'member')).get(R(cur.id)), 404, null)
        const sum = await (await client(qa, 'member')).get(`/api/meetings/${m.id}/summary`)
        if (c.check('GET summary', sum, 200)) c.fact('summary ẩn', sum.body.summary === null && sum.body.canManage === false && sum.body.readiness === null, JSON.stringify(sum.body).slice(0, 160))
        c.done()
      },
    },
    {
      id: 'AC13',
      role: 'member',
      devices: ['desktop', 'phone'],
      title: 'Thành viên đọc và tải biên bản đã ban hành; không có nút soạn, duyệt hay đính chính',
      steps: ['Biên bản cuộc họp đã ban hành', 'Đăng nhập thanhvien@, mở tab Biên bản', 'Bấm "Tải xuống" → "PDF"'],
      expected:
        'Chip "Đã ban hành", tờ biên bản, dòng "<Chủ toạ> duyệt, ban hành lúc …", chỉ có nút Tải xuống; tải được "Biên bản - <tên>.pdf"; API: submittedBy, returnedBy, rejectionReason null; history chỉ có bản Issued; POST corrections 403 MSG397',
      async run(qa) {
        const u = await users(qa)
        const m = await flow(qa)
        const issued = await driveTo(qa, m.id, 'Issued')
        await openMinutes(qa, m)
        await qa.expect(badge(qa, 'Đã ban hành')).toBeVisible()
        await qa.expect(paper(qa)).toContainText(ITEM_CONCLUSION)
        await qa.expect(meta(qa)).toContainText(`${u.chair.name} duyệt, ban hành lúc`)
        for (const name of ['Sửa', 'Tạo lại', 'Gửi duyệt', 'Duyệt và ban hành', 'Trả lại', 'Tạo bản đính chính']) await qa.expect(btn(qa, name)).toHaveCount(0)
        await snap(qa, 'thanh-vien-doc-ban-hanh')
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
        await press(qa, btn(qa, 'Tải xuống'))
        const menu = qa.page.getByRole('menu', { name: 'Tải xuống' })
        await qa.expect(menu).toBeVisible()
        await snap(qa, 'thanh-vien-menu-tai')
        const [download] = await Promise.all([qa.page.waitForEvent('download'), press(qa, menu.getByRole('menuitem', { name: /PDF/ }))])
        qa.expect(download.suggestedFilename()).toBe(`Biên bản - ${m.title}.pdf`)
        const c = checker(qa)
        const st = await minutesState(qa, 'member', m.id)
        c.fact('member đọc bản Issued', st.minutes?.id === issued.id && st.minutes?.status === 'Issued', JSON.stringify(st.minutes)?.slice(0, 100))
        c.fact('ẩn người gửi, người trả lại, lý do', st.minutes?.submittedBy === null && st.minutes?.returnedBy === null && st.minutes?.rejectionReason === null && st.minutes?.submittedAt === null, JSON.stringify({ s: st.minutes?.submittedBy, r: st.minutes?.returnedBy, why: st.minutes?.rejectionReason }))
        c.fact('history chỉ Issued/Superseded', st.history.every((h) => ['Issued', 'Superseded'].includes(h.status)), JSON.stringify(st.history))
        c.check('thành viên đính chính', await (await client(qa, 'member')).post(`${R(issued.id)}/corrections`), 403, 'MSG397')
        c.done()
      },
    },

    // ------------------------------------------------------------------------------------------- UC-83 Correct
    {
      id: 'AC14',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Thư ký tạo bản đính chính từ biên bản đã ban hành; bản cũ giữ nguyên và người khác vẫn đọc bản cũ',
      steps: ['Mở tab Biên bản đã ban hành', 'Bấm "Tạo bản đính chính", xác nhận', 'Thành viên mở tab Biên bản'],
      expected:
        'Toast "Đã tạo bản đính chính." (MSG432); chip "Bản nháp", ô "Phiên bản biên bản" có Bản gốc (Đã ban hành) và Đính chính 1 (Bản nháp); Sửa, Gửi duyệt có, không có Tạo lại (BR-413); API: bản số 2, supersedesId = bản gốc, nội dung bằng bản gốc; thành viên vẫn đọc bản gốc, history không có bản đính chính, không nhận thông báo nào mới',
      async run(qa) {
        const m = await flow(qa)
        const latest = (await minutesState(qa, 'secretary', m.id)).minutes
        // A correction already made (an earlier criterion, a rerun) is driven to Issued so a new one can start.
        const issued = await driveTo(qa, m.id, 'Issued')
        const memberNotesBefore = (await notificationsOf(qa, 'member', m.id, 'MinutesIssued')).length
        await openMinutes(qa, m)
        await press(qa, btn(qa, 'Tạo bản đính chính'))
        const d = dialog(qa, 'Tạo bản đính chính?')
        await qa.expect(d).toBeVisible()
        await snap(qa, 'hoi-dinh-chinh')
        await press(qa, d.getByRole('button', { name: 'Tạo bản đính chính', exact: true }))
        await qa.expect(toast(qa, 'Đã tạo bản đính chính.')).toBeVisible()
        await qa.expect(badge(qa, 'Bản nháp')).toBeVisible()
        await qa.expect(card(qa).getByRole('button', { name: /^Phiên bản biên bản/ })).toBeVisible()
        await qa.expect(btn(qa, 'Tạo lại')).toHaveCount(0)
        await qa.expect(btn(qa, 'Gửi duyệt')).toBeVisible()
        const editorOpen = await card(qa).getByRole('textbox', { name: 'Nội dung biên bản' }).count()
        if (!editorOpen) qa.note('UC-83 bước 4 nói hệ thống "mở bản mới để sửa (UC-60)"; màn hình chỉ hiện bản nháp đính chính với nút Sửa, chưa mở trình soạn')
        await snap(qa, 'da-tao-dinh-chinh')
        await noSidewaysScroll(qa)
        await press(qa, card(qa).getByRole('button', { name: /^Phiên bản biên bản/ }))
        const list = qa.page.getByRole('listbox', { name: 'Phiên bản biên bản' })
        await qa.expect(list.getByRole('option', { name: /Bản gốc/ })).toBeVisible()
        await qa.expect(list.getByRole('option', { name: /Đính chính/ }).first()).toBeVisible()
        await snap(qa, 'danh-sach-phien-ban')
        await qa.page.keyboard.press('Escape')
        const c = checker(qa)
        const st = await minutesState(qa, 'secretary', m.id)
        c.fact('bản đính chính là Draft', st.minutes.status === 'Draft', st.minutes.status)
        c.fact('số bản = bản đã ban hành + 1', st.minutes.revisionNumber === issued.revisionNumber + 1, `${st.minutes.revisionNumber} vs ${issued.revisionNumber}`)
        c.fact('supersedesId', st.minutes.supersedesId === issued.id, st.minutes.supersedesId)
        c.fact('nội dung chép từ bản đã ban hành', st.minutes.content === issued.content)
        const asMember = await minutesState(qa, 'member', m.id)
        c.fact('thành viên vẫn đọc bản đã ban hành', asMember.minutes?.id === issued.id && asMember.minutes?.status === 'Issued', JSON.stringify(asMember.minutes)?.slice(0, 100))
        c.fact('thành viên không thấy bản đính chính trong history', !asMember.history.some((h) => h.id === st.minutes.id), JSON.stringify(asMember.history))
        c.fact('không ai nhận thông báo khi mới tạo đính chính', (await notificationsOf(qa, 'member', m.id, 'MinutesIssued')).length === memberNotesBefore)
        c.check('thành viên GET bản đính chính', await (await client(qa, 'member')).get(R(st.minutes.id)), 404, null)
        if (latest && latest.status !== 'Issued') qa.note(`Trước tiêu chí này biên bản ở trạng thái ${latest.status}; đã đưa về Đã ban hành qua API`)
        c.done()
      },
    },
    {
      id: 'AC15',
      role: 'chair',
      title: 'API đính chính: đang có bản đính chính 409 MSG434; bản không phải Issued 409 MSG433; tạo lại bản đính chính 409 MSG456; ban hành đính chính thì bản cũ thành Đã thay thế, vẫn đọc và tải được',
      steps: [
        'Biên bản đã ban hành: Chủ toạ POST corrections (201), rồi POST lần nữa trên bản đã ban hành',
        'POST corrections trên bản đính chính (Draft); POST …/minutes rebuild: true',
        'Thư ký gửi duyệt bản đính chính, Chủ toạ duyệt',
        'POST corrections trên bản cũ (Superseded); thành viên GET và tải bản cũ',
      ],
      expected:
        '201 MSG432; 409 MSG434; 409 MSG433; 409 MSG456; duyệt 200 MSG427, bản cũ Superseded, bản mới Issued; nhật ký có minutes.issued cho bản mới; thành viên nhận MinutesIssued; bản cũ: corrections 409 MSG433, thành viên GET 200 Superseded, tải docx 200',
      async run(qa) {
        const c = checker(qa)
        const sec = await client(qa, 'secretary')
        const chair = await client(qa, 'chair')
        const member = await client(qa, 'member')
        const m = await fresh(qa, 'api-correction', 'Ended', 'API đính chính')
        const original = await driveTo(qa, m.id, 'Issued')
        const corr = await chair.post(`${R(original.id)}/corrections`)
        if (!c.check('tạo đính chính', corr, 201, 'MSG432')) return c.done()
        c.fact('message MSG432', corr.body.message === 'Đã tạo bản đính chính.', corr.body.message)
        const draft = corr.body.minutes
        const second = await sec.post(`${R(original.id)}/corrections`)
        if (c.check('đính chính lần hai', second, 409, 'MSG434')) c.fact('message MSG434', second.body.message === 'Biên bản đang có một bản đính chính chưa ban hành.', second.body.message)
        c.check('đính chính bản nháp', await chair.post(`${R(draft.id)}/corrections`), 409, 'MSG433')
        c.check('tạo lại bản đính chính', await chair.post(M(m.id), { rebuild: true }), 409, 'MSG456')
        const notesBefore = (await notificationsOf(qa, 'member', m.id, 'MinutesIssued')).length
        c.check('gửi duyệt đính chính', await sec.post(`${R(draft.id)}/submit`, { updatedAt: draft.updatedAt }), 200, 'MSG424')
        const waiting = (await minutesState(qa, 'chair', m.id)).minutes
        const ok = await chair.post(`${R(draft.id)}/approve`, { updatedAt: waiting.updatedAt })
        if (c.check('duyệt đính chính', ok, 200, 'MSG427')) c.fact('message MSG427', ok.body.message === 'Đã duyệt và ban hành biên bản.', ok.body.message)
        const old = await chair.get(R(original.id))
        c.fact('bản gốc thành Superseded', old.body?.minutes?.status === 'Superseded', old.body?.minutes?.status)
        c.fact('bản gốc giữ nguyên nội dung', old.body?.minutes?.content === original.content)
        const latest = (await minutesState(qa, 'member', m.id)).minutes
        c.fact('thành viên đọc bản đính chính đã ban hành', latest?.id === draft.id && latest?.status === 'Issued', JSON.stringify(latest)?.slice(0, 100))
        c.fact('thành viên nhận MinutesIssued khi ban hành đính chính', (await notificationsOf(qa, 'member', m.id, 'MinutesIssued')).length > notesBefore)
        c.fact('nhật ký ghi ban hành đính chính', (await issuedAudit(qa, draft.id)).length === 1)
        c.check('đính chính bản đã thay thế', await chair.post(`${R(original.id)}/corrections`), 409, 'MSG433')
        const memberOld = await member.get(R(original.id))
        if (c.check('thành viên đọc bản đã thay thế', memberOld, 200)) c.fact('Superseded', memberOld.body.minutes?.status === 'Superseded')
        c.check('thành viên tải bản đã thay thế', await member.get(`${R(original.id)}/export`), 200)
        c.check('thành viên đính chính bản mới', await member.post(`${R(draft.id)}/corrections`), 403, 'MSG397')
        c.done()
      },
    },
    {
      id: 'AC16',
      role: 'member',
      devices: ['desktop', 'phone'],
      title: 'Thành viên mở bản đã thay thế từ ô "Phiên bản biên bản" và quay lại bản mới nhất',
      steps: ['Biên bản đã có bản đính chính được ban hành', 'thanhvien@ mở tab Biên bản', 'Chọn "Bản gốc" trong "Phiên bản biên bản"', 'Bấm "Xem bản mới nhất"'],
      expected:
        'Danh sách chỉ có Bản gốc (Đã thay thế) và Đính chính 1 (Đã ban hành); chọn Bản gốc: chip "Đã thay thế", khung "Đang xem bản gốc, đã thay thế."; "Xem bản mới nhất" về chip "Đã ban hành"; không nút soạn hay đính chính',
      async run(qa) {
        const m = await fresh(qa, 'api-correction', 'Ended', 'API đính chính')
        const st = await minutesState(qa, 'secretary', m.id)
        if (!st.history.some((h) => h.status === 'Superseded')) {
          // Without AC15 in this run: issue, correct and issue again through the API.
          await driveTo(qa, m.id, 'Issued')
          const sec = await client(qa, 'secretary')
          const cur = (await minutesState(qa, 'secretary', m.id)).minutes
          qa.expectStatus(await sec.post(`${R(cur.id)}/corrections`), 201)
          await driveTo(qa, m.id, 'Issued')
        }
        await openMinutes(qa, m)
        await qa.expect(badge(qa, 'Đã ban hành')).toBeVisible()
        const select = card(qa).getByRole('button', { name: /^Phiên bản biên bản/ })
        await press(qa, select)
        const list = qa.page.getByRole('listbox', { name: 'Phiên bản biên bản' })
        await qa.expect(list.getByRole('option')).toHaveCount((await minutesState(qa, 'member', m.id)).history.length)
        await qa.expect(list.getByRole('option', { name: /Bản gốc/ })).toContainText('Đã thay thế')
        await snap(qa, 'thanh-vien-ds-phien-ban')
        await press(qa, list.getByRole('option', { name: /Bản gốc/ }))
        await qa.expect(badge(qa, 'Đã thay thế')).toBeVisible()
        await qa.expect(card(qa).getByText('Đang xem bản gốc, đã thay thế.')).toBeVisible()
        await qa.expect(paper(qa)).toBeVisible()
        await qa.expect(btn(qa, 'Tạo bản đính chính')).toHaveCount(0)
        await snap(qa, 'thanh-vien-xem-ban-cu')
        await noSidewaysScroll(qa)
        await press(qa, card(qa).getByRole('button', { name: 'Xem bản mới nhất' }))
        await qa.expect(badge(qa, 'Đã ban hành')).toBeVisible()
        await snap(qa, 'thanh-vien-ve-ban-moi')
        await noRawCodes(qa)
      },
    },

    // ------------------------------------------------------------------------------------------- BR-390 Secret meeting
    {
      id: 'AC17',
      role: 'chair',
      devices: ['desktop', 'phone'],
      title: 'Biên bản cuộc họp Mật chỉ xem trên màn hình: không có nút Tải xuống, API tải về 403 MSG405 với cả Chủ toạ và Thành viên',
      steps: ['Cuộc họp Mật đã kết thúc, biên bản đã ban hành', 'chutoa@ mở tab Biên bản', 'API: Chủ toạ, Thư ký, Thành viên GET …/export (docx, pdf)'],
      expected: 'Tờ biên bản hiện, không có "Tải xuống"; mọi lần tải 403 MSG405 "Biên bản của cuộc họp Mật không được tải về."; thành viên vẫn đọc trên màn hình (API 200)',
      async run(qa) {
        const m = await reused(qa, 'secret')
        const issued = await driveTo(qa, m.id, 'Issued')
        await openMinutes(qa, m)
        await qa.expect(badge(qa, 'Đã ban hành')).toBeVisible()
        await qa.expect(paper(qa)).toBeVisible()
        await qa.expect(btn(qa, 'Tải xuống')).toHaveCount(0)
        await snap(qa, 'hop-mat-khong-tai')
        await noSidewaysScroll(qa)
        const c = checker(qa)
        for (const role of ['chair', 'secretary', 'member'])
          for (const q of ['', '?format=pdf']) {
            const res = await (await client(qa, role)).get(`${R(issued.id)}/export${q}`)
            if (c.check(`${role} tải ${q || 'docx'}`, res, 403, 'MSG405'))
              c.fact('message MSG405', res.body.message === 'Biên bản của cuộc họp Mật không được tải về.', res.body.message)
          }
        const asMember = await minutesState(qa, 'member', m.id)
        c.fact('thành viên đọc bản đã ban hành', asMember.minutes?.status === 'Issued')
        c.done()
      },
    },

    // ------------------------------------------------------------------------------------------- Role table (API)
    {
      id: 'AC18',
      role: 'admin',
      title: 'Bảng quyền: chưa đăng nhập 401; người không đọc được cuộc họp 404 cho mọi thao tác biên bản và dự thảo, không lộ gì',
      steps: [
        'Không token: GET …/minutes, GET …/summary',
        'quantri@ (không có trong thành phần cuộc họp): GET, POST …/minutes, GET /api/minutes/{id}, export, submit, approve, reject, corrections; GET, PUT …/summary',
      ],
      expected: '401; quantri@ 404 không mã cho tất cả (nếu quantri@ đọc được cuộc họp thì tiêu chí chưa kết luận)',
      async run(qa) {
        const c = checker(qa)
        const anon = await client(qa, 'anon')
        const admin = await client(qa, 'admin')
        const caps = await qa.capabilities('admin')
        const m = await fresh(qa, 'api-correction', 'Ended', 'API đính chính')
        const issued = await driveTo(qa, m.id, 'Issued')
        c.check('anon GET minutes', await anon.get(M(m.id)), 401)
        c.check('anon GET summary', await anon.get(`/api/meetings/${m.id}/summary`), 401)
        const canRead = (await admin.get(`/api/meetings/${m.id}`)).status === 200
        if (canRead) qa.inconclusive(`quantri@ (${caps.roleName}: ${caps.capabilities.join(', ')}) đọc được cuộc họp không có mình; cần một tài khoản ngoài cuộc họp để kiểm`)
        c.check('GET minutes', await admin.get(M(m.id)), 404, null)
        c.check('POST minutes', await admin.post(M(m.id), { rebuild: false }), 404, null)
        c.check('GET minutes/{id}', await admin.get(R(issued.id)), 404, null)
        c.check('export', await admin.get(`${R(issued.id)}/export`), 404, null)
        c.check('submit', await admin.post(`${R(issued.id)}/submit`, { updatedAt: issued.updatedAt }), 404, null)
        c.check('approve', await admin.post(`${R(issued.id)}/approve`, { updatedAt: issued.updatedAt }), 404, null)
        c.check('reject', await admin.post(`${R(issued.id)}/reject`, { reason: 'x', updatedAt: issued.updatedAt }), 404, null)
        c.check('corrections', await admin.post(`${R(issued.id)}/corrections`), 404, null)
        c.check('GET summary', await admin.get(`/api/meetings/${m.id}/summary`), 404, null)
        c.check('PUT summary', await admin.put(`/api/meetings/${m.id}/summary`, { objectives: 'a', discussion: 'b', conclusions: 'c' }), 404, null)
        c.done()
      },
    },

    // ------------------------------------------------------------------------------------------- UC-47–UC-50 AI draft
    {
      id: 'AC19',
      role: 'secretary',
      title: 'Dự thảo AI — quyền và trạng thái (chỉ trên họp Nội bộ, không gọi API trả phí): thành viên 403 MSG385; chưa họp 409 MSG386; chưa có dự thảo 409 MSG390; họp Thường chỉ đọc aiReady',
      steps: [
        'Đọc cấu hình AI (quantri@ GET /api/admin/ai), không đổi gì',
        'Cuộc họp Nội bộ đã kết thúc: thanhvien@ POST, PUT …/summary, POST …/summary/review',
        'Cuộc họp Nội bộ chưa họp: Thư ký POST …/summary; PUT …/summary; POST …/summary/review',
        'Cuộc họp Thường đã kết thúc: chỉ GET …/summary',
        'Đọc lại cấu hình AI',
      ],
      expected:
        'Thành viên 403 MSG385 ba lần; chưa họp: POST 409 MSG386, PUT/review 409 MSG390 (không có gì vào hàng chờ); họp Thường: aiReady true, canManage true, readiness có; cấu hình AI trước và sau giống hệt',
      async run(qa) {
        const c = checker(qa)
        const admin = await client(qa, 'admin')
        const before = await admin.get('/api/admin/ai')
        c.check('đọc cấu hình AI', before, 200)
        qa.note(`Cấu hình AI: mode ${before.body.mode}, localConfigured ${before.body.localConfigured}, model local ${before.body.localModel}`)
        const S = (id) => `/api/meetings/${id}/summary`
        const member = await client(qa, 'member')
        const sec = await client(qa, 'secretary')
        const ai = await reused(qa, 'ai')
        const edit = { objectives: 'Mục tiêu', discussion: '1. Nội dung', conclusions: '1. Kết luận' }
        const denied = await member.post(S(ai.id))
        if (c.check('thành viên POST', denied, 403, 'MSG385')) c.fact('message MSG385', denied.body.message === 'Chỉ Chủ toạ hoặc Thư ký được tạo, sửa và duyệt dự thảo.', denied.body.message)
        c.check('thành viên PUT', await member.put(S(ai.id), edit), 403, 'MSG385')
        c.check('thành viên duyệt', await member.post(`${S(ai.id)}/review`), 403, 'MSG385')
        const ns = await reused(qa, 'notStarted')
        const early = await sec.post(S(ns.id))
        if (c.check('chưa họp POST', early, 409, 'MSG386')) c.fact('message MSG386', early.body.message === 'Chỉ tạo dự thảo khi cuộc họp đang diễn ra hoặc đã kết thúc.', early.body.message)
        c.check('chưa có dự thảo PUT', await sec.put(S(ns.id), edit), 409, 'MSG390')
        c.check('chưa có dự thảo duyệt', await sec.post(`${S(ns.id)}/review`), 409, 'MSG390')
        const nsState = await sec.get(S(ns.id))
        c.fact('không có gì vào hàng chờ', nsState.body?.summary === null, JSON.stringify(nsState.body?.summary))
        const normal = await fresh(qa, 'api-draft', 'Ended', 'API bản nháp')
        const n = await sec.get(S(normal.id))
        if (c.check('họp Thường GET', n, 200)) c.fact('aiReady, canManage, readiness', n.body.aiReady === true && n.body.canManage === true && !!n.body.readiness, JSON.stringify(n.body).slice(0, 200))
        const after = await admin.get('/api/admin/ai')
        c.fact('cấu hình AI không đổi', JSON.stringify(after.body) === JSON.stringify(before.body), JSON.stringify(after.body))
        c.done()
      },
    },
    {
      id: 'AC20',
      role: 'member',
      devices: ['desktop', 'phone'],
      title: 'Thẻ Dự thảo nội dung: Thành viên không thấy dự thảo chưa duyệt và không có nút; Thư ký thấy nút và mức sẵn sàng (không bấm trên họp Thường)',
      steps: ['thanhvien@ mở tab Biên bản của cuộc họp Thường đã kết thúc', 'Đăng nhập lại thuky@, mở cùng tab'],
      expected:
        'Thành viên: không có thẻ Dự thảo (chưa có dự thảo đã duyệt); Thư ký: nút "Tạo dự thảo" bật (máy AI sẵn sàng), dòng "Chưa có dự thảo. Bấm “Tạo dự thảo”…", ghi chú "Chưa có lời thoại nào…"',
      async run(qa) {
        const m = await fresh(qa, 'api-draft', 'Ended', 'API bản nháp')
        await openMinutes(qa, m)
        await qa.expect(draftCard(qa)).toHaveCount(0)
        await snap(qa, 'thanh-vien-the-du-thao')
        await qa.signIn('secretary')
        await openMinutes(qa, m)
        const ask = draftCard(qa).getByRole('button', { name: 'Tạo dự thảo', exact: true })
        await qa.expect(ask).toBeEnabled()
        await qa.expect(draftCard(qa).getByText(/Chưa có dự thảo\. Bấm “Tạo dự thảo”/)).toBeVisible()
        await qa.expect(draftCard(qa).getByText('Chưa có lời thoại nào: dự thảo chỉ dựa vào chương trình và biểu quyết.')).toBeVisible()
        await snap(qa, 'thu-ky-the-du-thao')
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
      },
    },
    {
      id: 'AC21',
      role: 'secretary',
      title: 'Thư ký yêu cầu dự thảo AI cho cuộc họp Nội bộ: vào hàng chờ (MSG384), luôn chạy trên máy AI local; máy tắt thì thẻ báo lỗi dễ hiểu, không gọi API ngoài',
      steps: [
        'Mở tab Biên bản của "[QA] FE-12 dùng lại dự thảo AI (Nội bộ)" (đã kết thúc)',
        'Bấm "Tạo dự thảo" (hoặc "Tạo lại" và xác nhận khi lần trước đã lỗi)',
        'Chờ tối đa 90 giây',
      ],
      expected:
        'Toast "Đã gửi yêu cầu. Dự thảo sẽ tự hiện khi AI viết xong." (MSG384), chip "Đang chờ"/"AI đang viết"; POST lần nữa khi đang chờ 409 MSG387; kết thúc: chip "Lỗi" + lý do của máy local ("Không kết nối được máy AI…", không phải "API AI"), hoặc dự thảo 3 mục có nhãn "Nội dung do AI tạo — cần rà soát", model là model local',
      async run(qa) {
        const S = (id) => `/api/meetings/${id}/summary`
        const sec = await client(qa, 'secretary')
        const ai = await reused(qa, 'ai')
        qa.expect(ai.sensitivity, 'never ask for a draft of a Normal meeting (it would call the paid API)').toBe('Internal')
        const settings = (await (await client(qa, 'admin')).get('/api/admin/ai')).body
        const state0 = (await sec.get(S(ai.id))).body
        const status0 = state0.summary?.status
        await openMinutes(qa, ai)
        if (status0 === 'Ready') {
          qa.note('Dự thảo đã viết xong ở lần chạy trước: không yêu cầu lại')
        } else if (status0 === 'Queued' || status0 === 'Running') {
          qa.note(`Dự thảo đang ở ${status0} từ lần chạy trước: không yêu cầu lại`)
        } else {
          const card = draftCard(qa)
          const label = state0.summary?.draft ? 'Tạo lại' : 'Tạo dự thảo'
          await card.getByRole('button', { name: label, exact: true }).first().click()
          if (label === 'Tạo lại') await dialog(qa, 'Tạo lại dự thảo?').getByRole('button', { name: 'Tạo lại', exact: true }).click()
          await qa.expect(toast(qa, 'Đã gửi yêu cầu. Dự thảo sẽ tự hiện khi AI viết xong.')).toBeVisible()
          await snap(qa, 'da-gui-yeu-cau')
          const busy = await sec.post(S(ai.id))
          if (busy.status === 409) qa.expect(busy.body.code).toBe('MSG387')
          else qa.note(`POST lần hai trả ${busy.status} ${busy.body?.code}: dự thảo đã xong trước khi kịp hỏi lại`)
        }
        // Wait for the in-house machine: it fails at once when it is off, writes in minutes when it is on.
        let state = state0
        const until = Date.now() + 90_000
        while (Date.now() < until) {
          state = (await sec.get(S(ai.id))).body
          if (!['Queued', 'Running'].includes(state.summary?.status)) break
          await new Promise((r) => setTimeout(r, 3000))
        }
        const s = state.summary
        if (['Queued', 'Running'].includes(s?.status)) {
          await qa.shot('van-dang-viet')
          qa.inconclusive(
            'Máy AI local chưa trả lời sau 90 giây (dự thảo vẫn đang viết). Bước tay: bật máy AI local, chờ dự thảo xong trên "[QA] FE-12 dùng lại dự thảo AI (Nội bộ)" rồi chạy lại --only AC21,AC22',
          )
        }
        await qa.page.reload()
        await qa.expect(draftCard(qa)).toBeVisible()
        if (s.status === 'Failed') {
          await qa.expect(draftCard(qa).locator('.minutes-head, .minutes-card-head').getByText('Lỗi', { exact: true })).toBeVisible()
          const alert = draftCard(qa).getByRole('alert')
          await qa.expect(alert).toContainText(s.errorMessage)
          await snap(qa, 'may-ai-tat-bao-loi')
          qa.expect(s.errorMessage, 'the failure reason is empty').not.toBe('')
          qa.expect(s.errorMessage, 'an Internal meeting went to the outside API (ADR-0056)').not.toMatch(/API AI|Nhà cung cấp/)
          qa.note(`Máy AI local không viết được: "${s.errorMessage}". Phần viết dự thảo thật cần bật máy AI local (xem AC22)`)
        } else {
          qa.expect(s.status).toBe('Ready')
          qa.expect(s.model, 'an Internal meeting was written by another model than the local one').toBe(settings.localModel)
          await qa.expect(draftCard(qa).getByText('Nội dung do AI tạo — cần rà soát')).toBeVisible()
          await snap(qa, 'du-thao-da-viet')
        }
        await noRawCodes(qa)
      },
    },
    {
      id: 'AC22',
      role: 'secretary',
      title: 'Sửa và duyệt dự thảo AI (UC-49, UC-50); biên bản lấy mục I–III từ dự thảo, báo khi dự thảo chưa duyệt (UC-59 AF-4)',
      steps: [
        'Cần dự thảo đã viết xong trên "[QA] FE-12 dùng lại dự thảo AI (Nội bộ)" (máy AI local bật)',
        'PUT mục I trống → MSG389; PUT hợp lệ → MSG388, chưa duyệt',
        'Tạo/tạo lại bản nháp biên bản: báo "Biên bản đang dùng dự thảo AI chưa duyệt."',
        'POST review → MSG391; thành viên đọc được dự thảo',
      ],
      expected: 'MSG389 400; MSG388 200 reviewedAt null; biên bản chứa mục I đã sửa; MSG391 200; thành viên GET summary thấy dự thảo đã duyệt',
      async run(qa) {
        const S = (id) => `/api/meetings/${id}/summary`
        const sec = await client(qa, 'secretary')
        const ai = await reused(qa, 'ai')
        const state = (await sec.get(S(ai.id))).body
        if (state.summary?.status !== 'Ready' || !state.summary?.draft)
          qa.inconclusive(
            `Chưa có dự thảo AI viết xong (trạng thái ${state.summary?.status ?? 'chưa có'}). Bước tay: bật máy AI local, chạy --only AC21 cho tới khi dự thảo xong, rồi chạy --only AC22`,
          )
        const c = checker(qa)
        const d = state.summary.draft
        c.check('mục I trống', await sec.put(S(ai.id), { objectives: '  ', discussion: d.discussion, conclusions: d.conclusions }), 400, 'MSG389')
        const objectives = `Mục tiêu QA đã sửa ${rand()}`
        const saved = await sec.put(S(ai.id), { objectives, discussion: d.discussion, conclusions: d.conclusions })
        if (c.check('lưu dự thảo', saved, 200, 'MSG388')) c.fact('chưa duyệt sau khi sửa', saved.body.summary.reviewedAt === null)
        const member = await client(qa, 'member')
        c.fact('thành viên chưa thấy', (await member.get(S(ai.id))).body.summary === null)
        const built = await sec.post(M(ai.id), { rebuild: true })
        const builtFirst = built.status === 409 && built.body.code === 'MSG399' ? built : null
        if (builtFirst) c.fact('tạo lại', false, 'unexpected MSG399 on rebuild')
        const latest = (await minutesState(qa, 'secretary', ai.id)).minutes ?? (await sec.post(M(ai.id), { rebuild: false })).body.minutes
        c.fact('biên bản có mục I đã sửa', latest?.content?.includes(objectives), latest?.status)
        await openMinutes(qa, ai)
        if (latest?.status === 'Draft' && !latest.supersedesId) await qa.expect(card(qa).getByText('Biên bản đang dùng dự thảo AI chưa duyệt.')).toBeVisible()
        await snap(qa, 'bien-ban-dung-du-thao-chua-duyet')
        const reviewed = await sec.post(`${S(ai.id)}/review`)
        c.check('duyệt dự thảo', reviewed, 200, 'MSG391')
        const seen = (await member.get(S(ai.id))).body.summary
        c.fact('thành viên thấy dự thảo đã duyệt', seen?.draft?.objectives === objectives, JSON.stringify(seen)?.slice(0, 120))
        c.done()
      },
    },
  ],
}

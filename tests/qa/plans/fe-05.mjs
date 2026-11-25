// FE-05 — Quản lý chương trình nghị sự (QA subtask SCRUM-192): UC-23a create, UC-23b update, UC-23c delete, UC-23d
// reorder, UC-24 set current, UC-25 complete, UC-26 view, UC-27 item conclusion, and the rules they cite (BR-058–BR-062,
// BR-063–BR-067, BR-026 for End Meeting).
//
// Criteria come from docs/01-requirements/05-use-case-specifications.md, 06-business-rules.md,
// docs/03-api/08-chuong-trinh-nghi-su.md, the message catalogue, and the SCRUM-25 comments: since 04/11/2026 (PO
// 02/11, PR #256) the current Chair plans the agenda exactly as the Secretary does (create, edit, delete, reorder,
// editor-options); only the Chair starts and completes items (BR-060–BR-062). Attaching documents to an item is UC-36b (FE-07)
// and is not tested here; neither is a Guest presenter or Guest reader (needs an OTP by email, UC-04).
//
// Safe on a shared local dev database: every meeting is created here through the API, titled "[QA] FE-05 …", five
// weeks ahead, organised by thuky@ (Secretary) with chutoa@ as Chair and approver and thanhvien@ as Member; one is
// organised by quantri@ to check that an Organizer who is neither Chair nor Secretary only reads. Meetings whose state
// a criterion does not change are reused across runs ("[QA] FE-05 dùng lại …"); a criterion that runs the live agenda
// gets its own meeting and ends it. Pending items a criterion adds to a reused meeting are deleted again.

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

/** An access token for the context's own signed-in role, from its refresh cookie. */
async function bearer(qa) {
  const res = await qa.context.request.post('/api/auth/refresh')
  qa.expect(res.status(), 'refresh for a bearer token').toBe(200)
  return (await res.json()).accessToken
}

const rand = () => Math.random().toString(36).slice(2, 6)
const iso = (ms) => new Date(ms).toISOString().replace(/\.\d{3}Z$/, 'Z')
const MINUTE = 60_000
const DAY = 86_400_000
let slotCounter = Math.floor(Math.random() * 7)
/** A 30-minute slot on the day five weeks from today, between 08:00 and 18:00 Vietnam time, varied per meeting. */
function slot() {
  slotCounter += 1
  const day = new Date(Date.now() + 35 * DAY)
  day.setUTCHours(1, 0, 0, 0) // 08:00 in Vietnam
  const start = day.getTime() + ((slotCounter * 35) % 600) * MINUTE + Math.floor(Math.random() * 6) * 5 * MINUTE
  return { plannedStartTime: iso(start), plannedEndTime: iso(start + 30 * MINUTE) }
}

async function meetingBody(qa, title) {
  const u = await users(qa)
  return {
    title,
    ...slot(),
    mode: 'InPerson',
    location: 'Phòng họp QA',
    onlineLink: null,
    sensitivity: 'Normal',
    description: 'Cuộc họp do bộ kiểm thử QA FE-05 tạo.',
    participants: [
      { userId: u.chair.id, role: 'Chair' },
      { userId: u.secretary.id, role: 'Secretary' },
      { userId: u.member.id, role: 'Member' },
    ],
  }
}

const PATHS = {
  Draft: [],
  WaitingApproval: ['submit'],
  Rejected: ['submit', 'reject'],
  Approved: ['submit', 'approve'],
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
    reject: { action: 'Reject', reason: 'QA: cần bổ sung nội dung chương trình.' },
    publish: { action: 'Publish' },
    start: { action: 'Start' },
    end: { action: 'End' },
    cancel: { action: 'Cancel', reason: 'QA: xếp lịch sau.' },
  }[action]
  const res = await (await client(qa, role)).post(`/api/meetings/${id}/transitions`, body)
  qa.expectStatus(res, 200)
}
const actorOf = (step, organizer) => (['approve', 'reject', 'start', 'end'].includes(step) ? 'chair' : organizer)

/** Creates a meeting titled `title`, organised by `organizer`, and drives it to `status` through the API. */
async function createAt(qa, status, title, organizer = 'secretary') {
  const created = await (await client(qa, organizer)).post('/api/meetings', await meetingBody(qa, title))
  qa.expectStatus(created, 201)
  const id = created.body.meeting.id
  for (const step of PATHS[status]) await transition(qa, actorOf(step, organizer), id, step)
  const got = (await (await client(qa, organizer)).get(`/api/meetings/${id}`)).body
  qa.expect(got.status).toBe(status)
  return got
}

/** A fresh meeting for a criterion that changes its state (the live agenda). */
async function freshMeeting(qa, status, label) {
  const m = await createAt(qa, status, qa.name(`FE-05 ${label} ${rand()}`))
  qa.note(`Tạo: ${m.title} (${m.id}, ${status})`)
  return m
}

/**
 * The meeting at `status` reused across runs, found by its fixed title; created (and for Ended, given a concluded
 * item) the first time.
 */
const REUSED = {}
async function reused(qa, status, organizer = 'secretary') {
  const key = `${status}-${organizer}`
  if (REUSED[key]) return REUSED[key]
  const title = qa.name(`FE-05 dùng lại ${status}${organizer === 'admin' ? ' (quản trị tổ chức)' : ''}`)
  const c = await client(qa, organizer)
  const list = await c.get(`/api/meetings?q=${encodeURIComponent(title)}&status=${status}&pageSize=20`)
  const hit = (list.body.items ?? []).find((m) => m.title === title)
  let m
  if (hit) m = (await c.get(`/api/meetings/${hit.id}`)).body
  else if (status === 'Ended') m = await buildEnded(qa, title)
  else m = await createAt(qa, status, title, organizer)
  REUSED[key] = m
  return m
}

/** An Ended meeting whose agenda has a Done item with a conclusion and a Pending item, built through the API. */
async function buildEnded(qa, title) {
  const m = await createAt(qa, 'InProgress', title)
  const done = await addItem(qa, 'secretary', m.id, 'Báo cáo kết quả quý III', { description: 'Phòng Kế hoạch trình bày.' })
  await addItem(qa, 'secretary', m.id, 'Kế hoạch quý IV (chưa bàn)')
  const chair = await client(qa, 'chair')
  qa.expectStatus(await chair.post(`${A(m.id)}/${done}/current`), 200)
  qa.expectStatus(await chair.put(`${A(m.id)}/${done}/conclusion`, { conclusion: 'Thống nhất thông qua báo cáo quý III.' }), 200)
  qa.expectStatus(await chair.post(`${A(m.id)}/${done}/complete`), 200)
  await transition(qa, 'chair', m.id, 'end')
  return (await (await client(qa, 'secretary')).get(`/api/meetings/${m.id}`)).body
}

// ---------------------------------------------------------------------------------------------------------------------
// Helpers: agenda API.

const A = (meetingId) => `/api/meetings/${meetingId}/agenda`
const itemBody = (title, over = {}) => ({ title, description: '', presenterId: null, presenterGuestInvitationId: null, ...over })

async function addItem(qa, role, meetingId, title, over = {}) {
  const res = await (await client(qa, role)).post(A(meetingId), itemBody(`${title} ${rand()}`, over))
  qa.expectStatus(res, 201)
  return res.body.agendaItemId
}
async function snapshot(qa, role, meetingId) {
  const res = await (await client(qa, role)).get(A(meetingId))
  qa.expectStatus(res, 200)
  return res.body
}
const itemOf = (snap, id) => snap.items.find((i) => i.id === id)

/** Deletes the Pending items a criterion added to a reused meeting (InProgress/Done ones cannot be deleted). */
async function cleanup(qa, meetingId, ids) {
  const sec = await client(qa, 'secretary')
  for (const id of ids) await sec.delete(`${A(meetingId)}/${id}`)
}

/** Completes any item left under way in a reused InProgress meeting, so a new one can start. */
async function clearCurrent(qa, meetingId) {
  const snap = await snapshot(qa, 'chair', meetingId)
  for (const item of snap.items.filter((i) => i.status === 'InProgress'))
    await (await client(qa, 'chair')).post(`${A(meetingId)}/${item.id}/complete`)
}

/** AgendaPresenterAssigned notifications of `role` for the item titled `itemTitle` (the body names the item). */
async function notifications(qa, role, meetingId, itemTitle) {
  const res = await (await client(qa, role)).get('/api/notifications?page=1&pageSize=50')
  return (res.body.items ?? []).filter(
    (n) => n.type === 'AgendaPresenterAssigned' && n.meetingId === meetingId && `${n.title} ${n.body}`.includes(itemTitle),
  )
}

/** Runs every API check, then fails once with all the wrong ones. */
function checker(qa) {
  const wrong = []
  return {
    check(label, res, status, code) {
      const ok = res.status === status && (code === undefined || res.body?.code === code)
      if (!ok) wrong.push(`${label}: want ${status}${code ? ` ${code}` : ''}, got ${res.status} ${JSON.stringify(res.body).slice(0, 220)}`)
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

/** Closes every toast on screen (the app's toasts stay until dismissed and would match the next assertion). */
async function dismissToasts(qa) {
  for (let i = 0; i < 5; i++) {
    const closers = qa.page.locator('.toast-stack').getByRole('button', { name: 'Đóng' })
    if (!(await closers.count())) break
    await closers.first().click()
    await qa.page.waitForTimeout(300)
  }
}
const panel = (qa) => qa.page.getByRole('region', { name: 'Nội dung cuộc họp' })
const row = (qa, title) => panel(qa).locator('li.agenda-item').filter({ hasText: title })
async function press(qa, locator) {
  if (qa.device === 'phone') await locator.tap()
  else await locator.click()
}

/** Opens the meeting's "Nội dung cuộc họp" tab and waits for the agenda to load. */
async function openAgenda(qa, meeting) {
  await qa.goto(`/mgmt/meetings/${meeting.id}?tab=agenda`)
  await qa.expect(qa.page.getByRole('heading', { level: 1, name: meeting.title })).toBeVisible()
  await qa.expect(panel(qa)).toBeVisible()
}

/** Picks a presenter in the item dialog's "Người trình bày" Select. */
async function pickPresenter(qa, form, name) {
  await form.getByRole('button', { name: /^Người trình bày/ }).click()
  await qa.page.getByRole('listbox', { name: 'Người trình bày' }).getByRole('option', { name }).click()
  await qa.expect(form.getByRole('button', { name: `Người trình bày: ${name}` })).toBeVisible()
}


// ---------------------------------------------------------------------------------------------------------------------

export default {
  ticket: 'SCRUM-192',
  title: 'FE-05 Quản lý chương trình nghị sự: thêm, sửa, xoá, sắp xếp mục, bắt đầu/hoàn thành mục, kết luận, xem theo vai trò',
  devices: ['desktop'],
  criteria: [
    // ----------------------------------------------------------------------------------------------- UC-23a Create
    {
      id: 'AC1',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Thư ký thêm mục nghị sự (tên, người trình bày, ghi chú) vào cuộc họp Nháp; mục mới ở cuối, trạng thái chờ',
      steps: [
        'Đăng nhập thuky@, mở tab "Nội dung cuộc họp" của cuộc họp Nháp có sẵn một mục',
        'Bấm "Thêm nội dung", nhập nội dung cần bàn, chọn người trình bày Lê An (thanhvien@), nhập ghi chú',
        'Bấm "Lưu"',
      ],
      expected:
        'Toast "Tạo mục nghị sự thành công." (MSG60); mục mới hiện cuối danh sách với tên người trình bày, chưa có chip trạng thái; API: Pending, position lớn nhất, vị trí mục cũ không đổi; không cuộn ngang',
      async run(qa) {
        const u = await users(qa)
        const m = await reused(qa, 'Draft')
        const first = await addItem(qa, 'secretary', m.id, 'Mục có sẵn')
        const created = [first]
        try {
          const before = await snapshot(qa, 'secretary', m.id)
          await openAgenda(qa, m)
          const add = panel(qa).getByRole('button', { name: 'Thêm nội dung' })
          await qa.expect(add).toBeVisible()
          await press(qa, add)
          const form = dialog(qa, 'Thêm nội dung')
          await qa.expect(form).toBeVisible()
          const title = `Dự toán ngân sách 2027 ${rand()}`
          await form.getByPlaceholder('VD: Dự toán ngân sách năm 2027').fill(title)
          await qa.expect(form.getByRole('button', { name: /^Người trình bày/ })).toBeEnabled()
          await pickPresenter(qa, form, u.member.name)
          await form.getByLabel('Ghi chú').fill('Phòng Tài chính chuẩn bị số liệu.')
          await settled(qa)
          await qa.shot('form-them-noi-dung')
          await form.getByRole('button', { name: 'Lưu' }).click()
          await qa.expect(toast(qa, 'Tạo mục nghị sự thành công.')).toBeVisible()
          await qa.expect(form).toHaveCount(0)
          const r = row(qa, title)
          await qa.expect(r).toBeVisible()
          await qa.expect(r.getByText(u.member.name)).toBeVisible()
          await qa.expect(r.locator('.agenda-status')).toHaveCount(0)
          const titles = await panel(qa).locator('li.agenda-item .agenda-title').allInnerTexts()
          qa.expect(titles.at(-1), 'the new item is not last').toContain(title)
          await settled(qa)
          await qa.shot('muc-moi-o-cuoi')
          await noSidewaysScroll(qa)
          await noRawCodes(qa)
          const after = await snapshot(qa, 'secretary', m.id)
          const item = after.items.find((i) => i.title === title)
          qa.expect(item, 'the item is not in the API snapshot').toBeTruthy()
          created.push(item.id)
          qa.expect(item.status).toBe('Pending')
          qa.expect(item.presenterId).toBe(u.member.id)
          qa.expect(item.description).toBe('Phòng Tài chính chuẩn bị số liệu.')
          qa.expect(item.position).toBe(Math.max(...after.items.map((i) => i.position)))
          for (const old of before.items) qa.expect(itemOf(after, old.id)?.position, `position of ${old.title} changed`).toBe(old.position)
        } finally {
          await cleanup(qa, m.id, created)
        }
      },
    },
    {
      id: 'AC2',
      role: 'secretary',
      title: 'Hộp thoại thêm mục: bỏ trống nội dung thì báo lỗi tại ô và không tạo gì; đóng khi đã nhập thì hỏi bỏ thay đổi',
      steps: ['Mở "Thêm nội dung" trên cuộc họp Nháp', 'Bấm "Lưu" khi ô nội dung trống', 'Nhập ghi chú rồi bấm "Huỷ"'],
      expected:
        'Ô nội dung báo "Nhập nội dung cần bàn." (aria-invalid), không có POST, số mục không đổi; Huỷ khi đã nhập mở "Bỏ thay đổi chưa lưu?"',
      async run(qa) {
        const m = await reused(qa, 'Draft')
        const before = (await snapshot(qa, 'secretary', m.id)).items.length
        await openAgenda(qa, m)
        let posts = 0
        qa.page.on('request', (r) => r.method() === 'POST' && r.url().endsWith(`/agenda`) && posts++)
        await panel(qa).getByRole('button', { name: 'Thêm nội dung' }).click()
        const form = dialog(qa, 'Thêm nội dung')
        await qa.expect(form).toBeVisible()
        await qa.expect(form.getByRole('button', { name: 'Lưu' })).toBeEnabled()
        await form.getByRole('button', { name: 'Lưu' }).click()
        await qa.expect(form.getByText('Nhập nội dung cần bàn.')).toBeVisible()
        await qa.expect(form.getByPlaceholder('VD: Dự toán ngân sách năm 2027')).toHaveAttribute('aria-invalid', 'true')
        await settled(qa)
        await qa.shot('loi-thieu-noi-dung')
        qa.expect(posts, 'an empty title was sent to the server').toBe(0)
        await form.getByLabel('Ghi chú').fill('Ghi chú dở dang')
        await form.getByRole('button', { name: 'Huỷ' }).click()
        await qa.expect(dialog(qa, 'Bỏ thay đổi chưa lưu?')).toBeVisible()
        await settled(qa)
        await qa.shot('hoi-bo-thay-doi')
        await dialog(qa, 'Bỏ thay đổi chưa lưu?').getByRole('button', { name: 'Bỏ thay đổi' }).click()
        await qa.expect(form).toHaveCount(0)
        qa.expect((await snapshot(qa, 'secretary', m.id)).items.length).toBe(before)
      },
    },
    {
      id: 'AC3',
      role: 'secretary',
      title: 'API tạo mục: kiểm tra dữ liệu (MSG66) và người trình bày phải là thành phần của cuộc họp (BR-064, BR-065); hợp lệ 201 MSG60',
      steps: [
        'POST agenda: tên trống; tên 301 ký tự; ghi chú 4001 ký tự; người trình bày không thuộc cuộc họp (quantri@); cả hai loại người trình bày; trường cũ plannedDurationMinutes',
        'POST hợp lệ, người trình bày chutoa@',
      ],
      expected:
        'Mỗi trường hợp lỗi 400 MSG66 và không có mục nào được tạo; hợp lệ 201 MSG60 "Tạo mục nghị sự thành công.", có agendaItemId, mục Pending ở cuối',
      async run(qa) {
        const u = await users(qa)
        const m = await reused(qa, 'Draft')
        const sec = await client(qa, 'secretary')
        const c = checker(qa)
        const before = (await snapshot(qa, 'secretary', m.id)).items.length
        const cases = [
          ['tên trống', itemBody('   ')],
          ['tên 301 ký tự', itemBody('a'.repeat(301))],
          ['ghi chú 4001 ký tự', itemBody('Ghi chú dài', { description: 'b'.repeat(4001) })],
          ['người trình bày ngoài cuộc họp', itemBody('Người ngoài trình bày', { presenterId: u.admin.id })],
          ['hai loại người trình bày', itemBody('Hai người', { presenterId: u.member.id, presenterGuestInvitationId: u.member.id })],
          ['trường cũ plannedDurationMinutes', { ...itemBody('Có thời lượng'), plannedDurationMinutes: 15 }],
        ]
        for (const [label, body] of cases) c.check(label, await sec.post(A(m.id), body), 400, 'MSG66')
        c.fact('không tạo mục nào khi lỗi', (await snapshot(qa, 'secretary', m.id)).items.length === before)
        const ok = await sec.post(A(m.id), itemBody(`Mục hợp lệ ${rand()}`, { presenterId: u.chair.id }))
        if (c.check('hợp lệ', ok, 201, 'MSG60')) {
          c.fact('message', ok.body.message === 'Tạo mục nghị sự thành công.', ok.body.message)
          const snap = await snapshot(qa, 'secretary', m.id)
          const item = itemOf(snap, ok.body.agendaItemId)
          c.fact('Pending', item?.status === 'Pending', item?.status)
          c.fact('ở cuối', item?.position === Math.max(...snap.items.map((i) => i.position)))
          c.fact('presenter', item?.presenterId === u.chair.id && item?.presenterName === u.chair.name, JSON.stringify(item))
          await cleanup(qa, m.id, [ok.body.agendaItemId])
        }
        c.done()
      },
    },

    // ----------------------------------------------------------------------------------------------- UC-23b Update
    {
      id: 'AC4',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Thư ký sửa tên, người trình bày, ghi chú của một mục; vị trí và trạng thái giữ nguyên',
      steps: ['Mở tab Nội dung cuộc họp (Nháp) có hai mục', 'Bấm bút chì "Sửa: <tên>" ở mục thứ nhất', 'Đổi tên, chọn người trình bày Chủ toạ, bấm "Lưu"'],
      expected:
        'Hộp thoại "Sửa nội dung" điền sẵn giá trị cũ; toast "Cập nhật mục nghị sự thành công." (MSG61); dòng hiện tên mới và người trình bày mới, vẫn ở vị trí cũ, vẫn chờ',
      async run(qa) {
        const u = await users(qa)
        const m = await reused(qa, 'Draft')
        const a = await addItem(qa, 'secretary', m.id, 'Mục cần sửa', { description: 'Ghi chú cũ', presenterId: u.member.id })
        const b = await addItem(qa, 'secretary', m.id, 'Mục bên dưới')
        try {
          const before = await snapshot(qa, 'secretary', m.id)
          const old = itemOf(before, a)
          await openAgenda(qa, m)
          await press(qa, row(qa, old.title).getByRole('button', { name: `Sửa: ${old.title}` }))
          const form = dialog(qa, 'Sửa nội dung')
          await qa.expect(form).toBeVisible()
          await qa.expect(form.getByPlaceholder('VD: Dự toán ngân sách năm 2027')).toHaveValue(old.title)
          await qa.expect(form.getByLabel('Ghi chú')).toHaveValue('Ghi chú cũ')
          await qa.expect(form.getByRole('button', { name: `Người trình bày: ${u.member.name}` })).toBeVisible()
          await settled(qa)
          await qa.shot('form-sua-dien-san')
          const renamed = `${old.title} (đã sửa)`
          await form.getByPlaceholder('VD: Dự toán ngân sách năm 2027').fill(renamed)
          await qa.expect(form.getByRole('button', { name: /^Người trình bày/ })).toBeEnabled()
          await pickPresenter(qa, form, u.chair.name)
          await form.getByRole('button', { name: 'Lưu' }).click()
          await qa.expect(toast(qa, 'Cập nhật mục nghị sự thành công.')).toBeVisible()
          const r = row(qa, renamed)
          await qa.expect(r).toBeVisible()
          await qa.expect(r.getByText(u.chair.name)).toBeVisible()
          await settled(qa)
          await qa.shot('da-sua-muc')
          await noSidewaysScroll(qa)
          const after = await snapshot(qa, 'secretary', m.id)
          const item = itemOf(after, a)
          qa.expect(item.title).toBe(renamed)
          qa.expect(item.presenterId).toBe(u.chair.id)
          qa.expect(item.status).toBe('Pending')
          qa.expect(item.position).toBe(old.position)
          qa.expect(itemOf(after, b).position).toBe(itemOf(before, b).position)
        } finally {
          await cleanup(qa, m.id, [a, b])
        }
      },
    },

    // ----------------------------------------------------------------------------------------------- UC-23c Delete
    {
      id: 'AC5',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Thư ký xoá một mục chưa bắt đầu sau khi xác nhận; các mục còn lại giữ vị trí',
      steps: ['Mở tab Nội dung cuộc họp (Nháp) có ba mục', 'Bấm thùng rác "Xoá: <tên>" ở mục giữa', 'Bấm "Xoá" trong hộp thoại xác nhận'],
      expected:
        'Hộp thoại "Xoá nội dung" nêu tên mục; toast "Xoá mục nghị sự thành công." (MSG62); dòng biến mất; API: hai mục còn lại giữ nguyên position (UC-23c)',
      async run(qa) {
        const m = await reused(qa, 'Draft')
        const ids = [
          await addItem(qa, 'secretary', m.id, 'Mục đầu'),
          await addItem(qa, 'secretary', m.id, 'Mục sẽ xoá'),
          await addItem(qa, 'secretary', m.id, 'Mục cuối'),
        ]
        try {
          const before = await snapshot(qa, 'secretary', m.id)
          const victim = itemOf(before, ids[1])
          await openAgenda(qa, m)
          await press(qa, row(qa, victim.title).getByRole('button', { name: `Xoá: ${victim.title}` }))
          const confirm = dialog(qa, 'Xoá nội dung')
          await qa.expect(confirm).toBeVisible()
          await qa.expect(confirm.getByText(victim.title)).toBeVisible()
          // AGENDA_FT16 E1: the dialog says the attached documents stay in the Documents tab.
          await qa.expect(confirm.getByText('Tài liệu đã gắn vẫn còn trong tab Tài liệu.')).toBeVisible()
          await settled(qa)
          await qa.shot('xac-nhan-xoa')
          await confirm.getByRole('button', { name: 'Xoá', exact: true }).click()
          await qa.expect(toast(qa, 'Xoá mục nghị sự thành công.')).toBeVisible()
          await qa.expect(row(qa, victim.title)).toHaveCount(0)
          await settled(qa)
          await qa.shot('da-xoa')
          await noSidewaysScroll(qa)
          const after = await snapshot(qa, 'secretary', m.id)
          qa.expect(itemOf(after, ids[1])).toBeUndefined()
          for (const id of [ids[0], ids[2]]) qa.expect(itemOf(after, id).position).toBe(itemOf(before, id).position)
        } finally {
          await cleanup(qa, m.id, ids)
        }
      },
    },

    // ----------------------------------------------------------------------------------------------- UC-23d Reorder
    {
      id: 'AC6',
      role: 'secretary',
      title: 'Thư ký kéo tay nắm để đổi thứ tự mục; thứ tự mới được lưu',
      steps: ['Mở tab Nội dung cuộc họp (Nháp) có các mục', 'Kéo tay nắm "Kéo để đổi vị trí" của mục áp chót xuống dưới mục cuối bằng chuột', 'Tải lại trang'],
      expected: 'Mục đó xuống cuối ngay trên màn hình và vẫn ở cuối sau khi tải lại; API đúng thứ tự mới; MSG63 theo UC-23d bước 5',
      async run(qa) {
        const m = await reused(qa, 'Draft')
        const ids = [await addItem(qa, 'secretary', m.id, 'Mục di chuyển'), await addItem(qa, 'secretary', m.id, 'Mục đứng yên')]
        try {
          const snap = await snapshot(qa, 'secretary', m.id)
          const mover = itemOf(snap, ids[0])
          await openAgenda(qa, m)
          await qa.shot('truoc-khi-keo')
          const grip = row(qa, mover.title).getByRole('button', { name: `Kéo để đổi vị trí: ${mover.title}` })
          await qa.expect(grip).toBeVisible()
          // Drag with the mouse: press on the grip, move in steps below the last row, release.
          const from = await grip.boundingBox()
          const to = await row(qa, itemOf(snap, ids[1]).title).boundingBox()
          await qa.page.mouse.move(from.x + from.width / 2, from.y + from.height / 2)
          await qa.page.mouse.down()
          await qa.page.mouse.move(from.x + from.width / 2, from.y + from.height / 2 + 8, { steps: 4 })
          await qa.page.mouse.move(from.x + from.width / 2, to.y + to.height * 0.8, { steps: 12 })
          await qa.page.mouse.up()
          await qa.expect(panel(qa).locator('li.agenda-item .agenda-title').last()).toContainText(mover.title)
          const toastShown = await toast(qa, 'Sắp xếp mục nghị sự thành công.').isVisible().catch(() => false)
          await qa.page.waitForResponse((r) => r.url().endsWith('/agenda') && r.request().method() === 'GET', { timeout: 5000 }).catch(() => undefined)
          await settled(qa)
          await qa.shot('sau-khi-keo')
          if (!toastShown) qa.note('Kéo thả lưu thành công nhưng không hiện MSG63 "Sắp xếp mục nghị sự thành công." như UC-23d bước 5 (FE cố ý im lặng, không thấy ghi lệch trong ticket).')
          await qa.goto(`/mgmt/meetings/${m.id}?tab=agenda`)
          await qa.expect(panel(qa).locator('li.agenda-item .agenda-title').last()).toContainText(mover.title)
          await qa.shot('sau-khi-tai-lai')
          const after = await snapshot(qa, 'secretary', m.id)
          qa.expect(after.items.at(-1).id).toBe(ids[0])
        } finally {
          await cleanup(qa, m.id, ids)
        }
      },
    },
    {
      id: 'AC7',
      role: 'secretary',
      title: 'API sắp xếp: phải gửi đủ mỗi mục một lần với orderVersion mới nhất (MSG143), hợp lệ 200 MSG63',
      steps: ['PUT agenda/order thiếu một mục; trùng một mục; có ID lạ; orderVersion cũ', 'PUT hợp lệ (đảo hai mục)'],
      expected: 'Thiếu/trùng/ID lạ: 400 MSG143; orderVersion cũ: 409 MSG143; thứ tự không đổi; hợp lệ 200 MSG63 và GET trả đúng thứ tự mới',
      async run(qa) {
        const m = await reused(qa, 'Draft')
        const ids = [await addItem(qa, 'secretary', m.id, 'Sắp xếp A'), await addItem(qa, 'secretary', m.id, 'Sắp xếp B')]
        const sec = await client(qa, 'secretary')
        const c = checker(qa)
        try {
          const snap = await snapshot(qa, 'secretary', m.id)
          const all = snap.items.map((i) => i.id)
          const put = (orderedItemIds, orderVersion = snap.orderVersion) => sec.put(`${A(m.id)}/order`, { orderVersion, orderedItemIds })
          c.check('thiếu một mục', await put(all.slice(1)), 400, 'MSG143')
          c.check('trùng một mục', await put([...all, all[0]]), 400, 'MSG143')
          c.check('ID lạ', await put([...all.slice(0, -1), '0199ffff-ffff-7fff-bfff-ffffffffffff']), 400, 'MSG143')
          const swapped = [...all.slice(0, -2), all.at(-1), all.at(-2)]
          c.check('orderVersion cũ', await put(swapped, 'khong-con-dung'), 409, 'MSG143')
          c.fact('thứ tự không đổi khi lỗi', JSON.stringify((await snapshot(qa, 'secretary', m.id)).items.map((i) => i.id)) === JSON.stringify(all))
          const ok = await put(swapped)
          if (c.check('hợp lệ', ok, 200, 'MSG63')) {
            c.fact('message', ok.body.message === 'Sắp xếp mục nghị sự thành công.', ok.body.message)
            const after = (await snapshot(qa, 'secretary', m.id)).items.map((i) => i.id)
            c.fact('thứ tự mới', JSON.stringify(after) === JSON.stringify(swapped), JSON.stringify(after))
          }
          c.done()
        } finally {
          await cleanup(qa, m.id, ids)
        }
      },
    },

    // ----------------------------------------------------------------------------------------------- BR-058, BR-059 states and roles
    {
      id: 'AC8',
      role: 'secretary',
      title: 'API: Thư ký và Chủ toạ soạn được (tạo, sửa, sắp xếp, xoá) ở Nháp, Chờ duyệt, Bị từ chối, Đã duyệt, Đã công bố, Đang diễn ra; khoá ở Đã kết thúc và Đã huỷ',
      steps: [
        'Ở mỗi trạng thái cho soạn: thuky@ rồi chutoa@ POST, PUT, PUT order, DELETE một mục; GET editor-options',
        'Ở Ended và Cancelled: POST, PUT, DELETE, PUT order, editor-options',
      ],
      expected:
        'Trạng thái cho soạn: 201 MSG60, 200 MSG61, 200 MSG63, 200 MSG62, options 200; permissions.canCreate = true. Ended/Cancelled: 409 MSG67, canCreate = false, không đổi gì',
      async run(qa) {
        const c = checker(qa)
        for (const status of ['Draft', 'WaitingApproval', 'Rejected', 'Approved', 'Published', 'InProgress']) {
          const m = await reused(qa, status)
          for (const role of ['secretary', 'chair']) {
            const api = await client(qa, role)
            const label = `${status}/${role}`
            const made = []
            for (const n of [1, 2]) {
              const res = await api.post(A(m.id), itemBody(`Soạn ${label} ${n} ${rand()}`))
              if (c.check(`${label} POST`, res, 201, 'MSG60')) made.push(res.body.agendaItemId)
            }
            if (made.length < 2) continue
            c.check(`${label} PUT`, await api.put(`${A(m.id)}/${made[0]}`, itemBody(`Đã sửa ${label} ${rand()}`)), 200, 'MSG61')
            const snap = (await api.get(A(m.id))).body
            c.fact(`${label} canCreate`, snap.permissions?.canCreate === true && snap.permissions?.canReorder === true, JSON.stringify(snap.permissions))
            const order = snap.items.map((i) => i.id)
            const i0 = order.indexOf(made[0])
            const i1 = order.indexOf(made[1])
            ;[order[i0], order[i1]] = [order[i1], order[i0]]
            c.check(`${label} PUT order`, await api.put(`${A(m.id)}/order`, { orderVersion: snap.orderVersion, orderedItemIds: order }), 200, 'MSG63')
            c.check(`${label} editor-options`, await api.get(`${A(m.id)}/editor-options`), 200)
            for (const id of made) c.check(`${label} DELETE`, await api.delete(`${A(m.id)}/${id}`), 200, 'MSG62')
          }
        }
        for (const status of ['Ended', 'Cancelled']) {
          const m = await reused(qa, status)
          for (const role of ['secretary', 'chair']) {
            const api = await client(qa, role)
            const label = `${status}/${role}`
            const snap = (await api.get(A(m.id))).body
            c.fact(`${label} canCreate false`, snap.permissions?.canCreate === false, JSON.stringify(snap.permissions))
            c.check(`${label} POST`, await api.post(A(m.id), itemBody('Không được thêm')), 409, 'MSG67')
            c.check(`${label} editor-options`, await api.get(`${A(m.id)}/editor-options`), 409, 'MSG67')
            const item = snap.items?.[0]
            if (item) {
              c.check(`${label} PUT`, await api.put(`${A(m.id)}/${item.id}`, itemBody('Không được sửa')), 409, 'MSG67')
              c.check(`${label} DELETE`, await api.delete(`${A(m.id)}/${item.id}`), 409, 'MSG67')
              if (snap.items.length >= 2)
                c.check(`${label} PUT order`, await api.put(`${A(m.id)}/order`, { orderVersion: snap.orderVersion, orderedItemIds: snap.items.map((i) => i.id).reverse() }), 409, 'MSG67')
              const again = (await api.get(A(m.id))).body
              c.fact(`${label} không đổi`, again.orderVersion === snap.orderVersion && again.items[0].title === item.title)
            }
          }
        }
        c.done()
      },
    },
    {
      id: 'AC9',
      role: 'chair',
      devices: ['desktop', 'phone'],
      title: 'Chủ toạ thêm và sửa mục trên màn hình ở cuộc họp Đã công bố và giao người trình bày; người được giao nhận thông báo, tự giao mình thì không (UC-23a, UC-23b, AGENDA_FT12)',
      steps: [
        'Đăng nhập chutoa@, mở tab Nội dung cuộc họp của cuộc họp Đã công bố',
        'Thêm mục, người trình bày là chính mình (Chủ toạ), Lưu',
        'Thêm mục thứ hai, người trình bày Lê An (thanhvien@), Lưu',
        'Sửa mục thứ hai: đổi tên, Lưu',
      ],
      expected:
        'Chủ toạ thấy "Thêm nội dung", bút chì và thùng rác (SCRUM-25, PO 02/11); ba toast MSG60/MSG61; thanhvien@ nhận một thông báo AgendaPresenterAssigned của cuộc họp; chutoa@ không nhận thông báo nào cho mục tự giao',
      async run(qa) {
        const u = await users(qa)
        const m = await reused(qa, 'Published')
        const made = []
        try {
          await openAgenda(qa, m)
          for (const [label, who] of [
            ['tự giao', u.chair.name],
            ['giao thành viên', u.member.name],
          ]) {
            await press(qa, panel(qa).getByRole('button', { name: 'Thêm nội dung' }))
            const form = dialog(qa, 'Thêm nội dung')
            await qa.expect(form).toBeVisible()
            const title = `Chủ toạ thêm ${label} ${rand()}`
            await form.getByPlaceholder('VD: Dự toán ngân sách năm 2027').fill(title)
            await qa.expect(form.getByRole('button', { name: /^Người trình bày/ })).toBeEnabled()
            await pickPresenter(qa, form, who)
            await form.getByRole('button', { name: 'Lưu' }).click()
            await qa.expect(toast(qa, 'Tạo mục nghị sự thành công.')).toBeVisible()
            await qa.expect(row(qa, title)).toBeVisible()
            made.push(title)
          }
          const r = row(qa, made[1])
          await qa.expect(r.getByRole('button', { name: `Sửa: ${made[1]}` })).toBeVisible()
          await qa.expect(r.getByRole('button', { name: `Xoá: ${made[1]}` })).toBeVisible()
          // AGENDA_FT12 E1: the Chair edits like the Secretary (200 MSG61).
          await press(qa, r.getByRole('button', { name: `Sửa: ${made[1]}` }))
          const edit = dialog(qa, 'Sửa nội dung')
          const renamed = `${made[1]} (chủ toạ sửa)`
          await edit.getByPlaceholder('VD: Dự toán ngân sách năm 2027').fill(renamed)
          await edit.getByRole('button', { name: 'Lưu' }).click()
          await qa.expect(toast(qa, 'Cập nhật mục nghị sự thành công.')).toBeVisible()
          await qa.expect(row(qa, renamed)).toBeVisible()
          await settled(qa)
          await qa.shot('chu-toa-them-muc')
          await noSidewaysScroll(qa)
          // The member's notification arrives after commit; wait for it, then the chair must have none.
          // (The notification names the title as added — before the Chair's edit below.)
          await qa.expect
            .poll(async () => (await notifications(qa, 'member', m.id, made[1])).length, { timeout: 15_000, message: 'thanhvien@ got no AgendaPresenterAssigned' })
            .toBeGreaterThanOrEqual(1)
          const memberGot = await notifications(qa, 'member', m.id, made[1])
          qa.expect(memberGot.length, 'thanhvien@ got more than one notification for the item').toBe(1)
          const chairGot = await notifications(qa, 'chair', m.id, made[0])
          qa.expect(chairGot.length, 'chutoa@ was notified for assigning themselves').toBe(0)
          qa.note(`Thông báo của thanhvien@: "${memberGot[0].title}" — ${memberGot[0].body}`)
        } finally {
          const snap = await snapshot(qa, 'secretary', m.id)
          await cleanup(qa, m.id, snap.items.filter((i) => made.includes(i.title)).map((i) => i.id))
        }
      },
    },
    {
      id: 'AC10',
      role: 'member',
      devices: ['desktop', 'phone'],
      title: 'Thành viên xem chương trình nghị sự của cuộc họp Đã công bố chỉ đọc; API chặn mọi thao tác ghi',
      steps: ['Đăng nhập thanhvien@, mở tab Nội dung cuộc họp của cuộc họp Đã công bố có hai mục', 'Gọi POST, PUT, DELETE, PUT order, editor-options, current, complete, conclusion'],
      expected:
        'Thấy hai mục, người trình bày; không có "Thêm nội dung", bút chì, thùng rác, tay nắm kéo, "Bắt đầu"; API: ghi 403 MSG67, options 403 MSG67, current 403 MSG73, complete 403 MSG74, conclusion 403 MSG273',
      async run(qa) {
        const u = await users(qa)
        const m = await reused(qa, 'Published')
        const ids = [
          await addItem(qa, 'secretary', m.id, 'Báo cáo tình hình', { presenterId: u.secretary.id }),
          await addItem(qa, 'secretary', m.id, 'Ý kiến các phòng'),
        ]
        try {
          const snap = await snapshot(qa, 'member', m.id)
          await openAgenda(qa, m)
          for (const id of ids) await qa.expect(row(qa, itemOf(snap, id).title)).toBeVisible()
          await qa.expect(row(qa, itemOf(snap, ids[0]).title).getByText(u.secretary.name)).toBeVisible()
          const p = panel(qa)
          await qa.expect(p.getByRole('button', { name: 'Thêm nội dung' })).toHaveCount(0)
          await qa.expect(p.getByRole('button', { name: /^(Sửa|Xoá|Kéo để đổi vị trí):/ })).toHaveCount(0)
          await qa.expect(p.getByRole('button', { name: 'Bắt đầu' })).toHaveCount(0)
          await settled(qa)
          await qa.shot('thanh-vien-chi-doc')
          await noSidewaysScroll(qa)
          await noRawCodes(qa)
          qa.expect(snap.permissions).toEqual({ canCreate: false, canReorder: false })
          if (qa.device !== 'desktop') return
          const api = await client(qa, 'member')
          const c = checker(qa)
          const id = ids[0]
          c.check('POST', await api.post(A(m.id), itemBody('Thành viên thêm')), 403, 'MSG67')
          c.check('PUT', await api.put(`${A(m.id)}/${id}`, itemBody('Thành viên sửa')), 403, 'MSG67')
          c.check('DELETE', await api.delete(`${A(m.id)}/${id}`), 403, 'MSG67')
          c.check('PUT order', await api.put(`${A(m.id)}/order`, { orderVersion: snap.orderVersion, orderedItemIds: snap.items.map((i) => i.id).reverse() }), 403, 'MSG67')
          c.check('editor-options', await api.get(`${A(m.id)}/editor-options`), 403, 'MSG67')
          c.check('current', await api.post(`${A(m.id)}/${id}/current`), 403, 'MSG73')
          c.check('complete', await api.post(`${A(m.id)}/${id}/complete`), 403, 'MSG74')
          c.check('conclusion', await api.put(`${A(m.id)}/${id}/conclusion`, { conclusion: 'Thành viên ghi.' }), 403, 'MSG273')
          c.fact('vẫn đủ hai mục', ids.every((x) => itemOf(snap, x)))
          c.done()
        } finally {
          await cleanup(qa, m.id, ids)
        }
      },
    },
    {
      id: 'AC11',
      role: 'member',
      devices: ['desktop', 'phone'],
      title: 'Người không được xem cuộc họp không thấy chương trình nghị sự: thành viên với cuộc họp Nháp, người ngoài, chưa đăng nhập',
      steps: [
        'thanhvien@ mở tab Nội dung cuộc họp của cuộc họp Nháp (chưa công bố) mà mình có tên',
        'API: thanhvien@ GET agenda cuộc họp Nháp; quantri@ (không thuộc cuộc họp) GET/POST agenda cuộc họp Đã công bố; chưa đăng nhập GET',
      ],
      expected: 'Màn hình như đường dẫn không có, không lộ nội dung; thanhvien@ GET 404; quantri@ GET 404, POST 404; chưa đăng nhập 401',
      async run(qa) {
        const draft = await reused(qa, 'Draft')
        const id = await addItem(qa, 'secretary', draft.id, 'Mục bí mật của bản nháp')
        try {
          const title = itemOf(await snapshot(qa, 'secretary', draft.id), id).title
          await qa.goto(`/mgmt/meetings/${draft.id}?tab=agenda`)
          await qa.expect(qa.page.getByRole('heading', { level: 1, name: draft.title })).toHaveCount(0)
          await qa.expect(qa.page.getByText(title)).toHaveCount(0)
          await qa.expect(qa.page.getByRole('heading', { name: /Không tìm thấy/ }).first()).toBeVisible()
          await settled(qa)
          await qa.shot('thanh-vien-ban-nhap-404')
          await noSidewaysScroll(qa)
          if (qa.device !== 'desktop') return
          const c = checker(qa)
          const pub = await reused(qa, 'Published')
          c.check('thanhvien@ GET Nháp', await (await client(qa, 'member')).get(A(draft.id)), 404)
          const { capabilities } = await qa.capabilities('admin')
          qa.note(`quantri@ có ${capabilities.join(', ')}; không thuộc cuộc họp, không tạo, không duyệt nó.`)
          c.check('quantri@ GET', await (await client(qa, 'admin')).get(A(pub.id)), 404)
          c.check('quantri@ POST', await (await client(qa, 'admin')).post(A(pub.id), itemBody('Người ngoài thêm')), 404)
          c.check('chưa đăng nhập GET', await (await client(qa, 'anon')).get(A(pub.id)), 401)
          c.done()
        } finally {
          await cleanup(qa, draft.id, [id])
        }
      },
    },
    {
      id: 'AC12',
      role: 'anon',
      title: 'Người tổ chức không phải Chủ toạ/Thư ký chỉ đọc chương trình nghị sự (không tự có quyền ghi)',
      steps: [
        'Tài khoản [QA] mang vai trò chỉ có "Tạo cuộc họp" tổ chức cuộc họp Đã công bố (Chủ toạ chutoa@, Thư ký thuky@)',
        'Tài khoản đó mở tab Nội dung cuộc họp; gọi GET/POST agenda',
      ],
      expected: 'Đọc được (GET 200, canCreate false); không có "Thêm nội dung"; POST 403 MSG67',
      async run(qa) {
        const admin = await qa.api('admin')
        // A reusable organizer whose only capability is creating meetings (quantri@ cannot organise: no MeetingCreator).
        const roleName = qa.name('FE-05 vai trò tổ chức')
        let role = (await admin.get('/api/roles')).body.find((r) => r.name === roleName)
        if (!role) {
          const mk = await admin.post('/api/roles', { name: roleName, description: null, capabilities: ['MeetingCreator'] })
          qa.expectStatus(mk, 201)
          role = mk.body.role
        }
        const orgEmail = `qa-fe05-tochuc@trustcabinet.local`
        const orgPassword = process.env.QA_PASSWORD ?? 'TrustCabinet@2026'
        let orgUser = (await admin.get('/api/users')).body.find((u) => u.email === orgEmail)
        if (!orgUser) {
          const mk = await admin.post('/api/users', {
            fullName: qa.name('FE-05 người tổ chức'),
            email: orgEmail,
            password: orgPassword,
            roleId: role.id,
          })
          qa.expectStatus(mk, 201)
          orgUser = mk.body.user
        }
        await qa.signIn(orgEmail)
        const org = await qa.api(orgEmail)
        const chairId = (await (await qa.api('chair')).get('/api/auth/me')).body.id
        const secId = (await (await qa.api('secretary')).get('/api/auth/me')).body.id
        const title = qa.name('FE-05 dùng lại (người tổ chức)')
        let m = (await org.get(`/api/meetings?q=${encodeURIComponent(title)}&status=Published&pageSize=20`)).body.items?.find(
          (i) => i.title === title,
        )
        if (!m) {
          const u = await users(qa)
          const created = await org.post('/api/meetings', {
            ...(await meetingBody(qa, title)),
            participants: [
              { userId: chairId, role: 'Chair' },
              { userId: secId, role: 'Secretary' },
              { userId: u.member.id, role: 'Member' },
            ],
          })
          qa.expectStatus(created, 201)
          const id = created.body.meeting.id
          qa.expectStatus(await org.post(`/api/meetings/${id}/transitions`, { action: 'SubmitForApproval', assignedApproverId: chairId }), 200)
          qa.expectStatus(await (await qa.api('chair')).post(`/api/meetings/${id}/transitions`, { action: 'Approve' }), 200)
          qa.expectStatus(await org.post(`/api/meetings/${id}/transitions`, { action: 'Publish' }), 200)
          m = (await org.get(`/api/meetings/${id}`)).body
        }
        await openAgenda(qa, m)
        await qa.expect(panel(qa).getByRole('button', { name: 'Thêm nội dung' })).toHaveCount(0)
        await qa.expect(panel(qa).getByText('Chưa có nội dung nào').or(panel(qa).locator('li.agenda-item').first())).toBeVisible()
        await settled(qa)
        await qa.shot('nguoi-to-chuc-chi-doc')
        const snap = await snapshot(qa, orgEmail, m.id)
        qa.expect(snap.permissions.canCreate).toBe(false)
        qa.expectStatus(await org.post(A(m.id), itemBody('Người tổ chức thêm')), 403)
        qa.expect((await org.post(A(m.id), itemBody('Người tổ chức thêm'))).body.code).toBe('MSG67')
      },
    },

    // ----------------------------------------------------------------------------------------------- UC-24 / UC-25 live
    {
      id: 'AC13',
      role: 'chair',
      devices: ['desktop', 'phone'],
      title: 'Chủ toạ bắt đầu một mục bất kỳ (không phải mục đầu) rồi bấm Xong; không mục nào tự bắt đầu',
      steps: [
        'Cuộc họp Đang diễn ra có ba mục chờ; chutoa@ mở tab Nội dung cuộc họp',
        'Bấm "Bắt đầu" ở mục thứ hai',
        'Bấm "Xong" ở mục đó',
        'Mở mục đó',
      ],
      expected:
        'Toast "Bắt đầu mục nghị sự thành công." (MSG64), mục có chip "Đang bàn", các mục khác không còn nút "Bắt đầu"; toast "Hoàn thành mục nghị sự thành công." (MSG65), chip "Đã xong", các mục còn lại có lại "Bắt đầu", cuộc họp vẫn Đang diễn ra; API ghi actualStartedAt/actualEndedAt; mở mục đã xong thấy "Bàn từ hh:mm đến hh:mm" (UC-26 bước 3), cả trên điện thoại; mục đang bàn không có nút Xoá (AGENDA_FT17), mục chờ không có nút Xong (AGENDA_FT35 E1)',
      async run(qa) {
        const m = await freshMeeting(qa, 'InProgress', `trực tiếp ${qa.device}`)
        const ids = []
        for (const t of ['Khai mạc', 'Thảo luận dự án', 'Bế mạc']) ids.push(await addItem(qa, 'secretary', m.id, t))
        try {
          const snap = await snapshot(qa, 'chair', m.id)
          const [first, second, third] = ids.map((id) => itemOf(snap, id).title)
          await openAgenda(qa, m)
          await qa.expect(row(qa, first).getByRole('button', { name: 'Bắt đầu' })).toBeVisible()
          await settled(qa)
          await qa.shot('ba-muc-cho')
          await press(qa, row(qa, second).getByRole('button', { name: 'Bắt đầu' }))
          await qa.expect(toast(qa, 'Bắt đầu mục nghị sự thành công.')).toBeVisible()
          await qa.expect(row(qa, second).getByText('Đang bàn')).toBeVisible()
          await qa.expect(panel(qa).getByRole('button', { name: 'Bắt đầu' })).toHaveCount(0)
          await qa.expect(row(qa, second).getByRole('button', { name: 'Xong' })).toBeVisible()
          // AGENDA_FT17: no delete button on the item under way.
          await qa.expect(row(qa, second).getByRole('button', { name: /^Xoá:/ })).toHaveCount(0)
          // AGENDA_FT35 E1: no "Xong" on a Pending item (the Chair's view).
          await qa.expect(row(qa, first).getByRole('button', { name: 'Xong' })).toHaveCount(0)
          await qa.expect(row(qa, third).getByRole('button', { name: 'Xong' })).toHaveCount(0)
          await settled(qa)
          await qa.shot('dang-ban-muc-2')
          await noSidewaysScroll(qa)
          await press(qa, row(qa, second).getByRole('button', { name: 'Xong' }))
          await qa.expect(toast(qa, 'Hoàn thành mục nghị sự thành công.')).toBeVisible()
          await qa.expect(row(qa, second).getByText('Đã xong')).toBeVisible()
          await qa.expect(row(qa, first).getByRole('button', { name: 'Bắt đầu' })).toBeVisible()
          await qa.expect(row(qa, third).getByRole('button', { name: 'Bắt đầu' })).toBeVisible()
          await qa.expect(panel(qa).getByText('Đang bàn')).toHaveCount(0)
          await settled(qa)
          await qa.shot('muc-2-da-xong')
          await noSidewaysScroll(qa)
          await noRawCodes(qa)
          const after = await snapshot(qa, 'chair', m.id)
          const done = itemOf(after, ids[1])
          qa.expect(done.status).toBe('Done')
          qa.expect(done.actualStartedAt).toBeTruthy()
          qa.expect(done.actualEndedAt).toBeTruthy()
          qa.expect(itemOf(after, ids[0]).status).toBe('Pending')
          qa.expect(itemOf(after, ids[2]).status).toBe('Pending')
          qa.expect(after.meetingStatus).toBe('InProgress')
          // UC-26 step 3: the recorded actual times are shown, on a touch screen too (no hover): opening the item says them.
          await press(qa, row(qa, second).locator('.agenda-title'))
          const times = row(qa, second).locator('.agenda-body').getByText(/^Bàn từ \d{2}:\d{2} đến \d{2}:\d{2}$/)
          await qa.expect(times).toBeVisible()
          await settled(qa)
          await qa.shot('muc-2-gio-thuc-te')
        } finally {
          await transition(qa, 'chair', m.id, 'end').catch(() => undefined)
        }
      },
    },
    {
      id: 'AC14',
      role: 'chair',
      title: 'API điều hành mục: một mục đang bàn tại một thời điểm (MSG72), chỉ Chủ toạ (MSG73/74), chỉ xoá mục chờ (MSG142), sửa và sắp xếp mục đã bắt đầu giữ trạng thái',
      steps: [
        'Cuộc họp Đã công bố: Chủ toạ bắt đầu một mục',
        'Cuộc họp Đang diễn ra: thuky@ bắt đầu/hoàn thành; Chủ toạ hoàn thành mục chờ; bắt đầu mục A; bắt đầu mục B khi A đang bàn',
        'Xoá A (đang bàn); sửa tên A; hoàn thành A; xoá A (đã xong); sắp xếp đưa A xuống cuối; mục của cuộc họp khác',
      ],
      expected:
        'Published: 409 MSG73; thuky@ 403 MSG73 / 403 MSG74; hoàn thành mục chờ 409 MSG74; bắt đầu A 200 MSG64; bắt đầu B 409 MSG72; xoá A 409 MSG142 (cả khi Done); sửa A 200 MSG61, vẫn InProgress, giữ actualStartedAt và position; hoàn thành 200 MSG65; sắp xếp 200 MSG63, A vẫn Done; mục của cuộc họp khác 404',
      async run(qa) {
        const c = checker(qa)
        const chair = await client(qa, 'chair')
        const sec = await client(qa, 'secretary')
        const pub = await reused(qa, 'Published')
        const pubItem = await addItem(qa, 'secretary', pub.id, 'Chưa họp')
        c.check('bắt đầu khi Published', await chair.post(`${A(pub.id)}/${pubItem}/current`), 409, 'MSG73')
        await cleanup(qa, pub.id, [pubItem])
        const m = await freshMeeting(qa, 'InProgress', 'điều hành API')
        try {
          const a = await addItem(qa, 'secretary', m.id, 'Mục A')
          const b = await addItem(qa, 'secretary', m.id, 'Mục B')
          c.check('thuky@ bắt đầu', await sec.post(`${A(m.id)}/${a}/current`), 403, 'MSG73')
          c.check('Chủ toạ hoàn thành mục chờ', await chair.post(`${A(m.id)}/${a}/complete`), 409, 'MSG74')
          const started = await chair.post(`${A(m.id)}/${a}/current`)
          if (c.check('bắt đầu A', started, 200, 'MSG64')) c.fact('message MSG64', started.body.message === 'Bắt đầu mục nghị sự thành công.', started.body.message)
          c.check('thuky@ hoàn thành', await sec.post(`${A(m.id)}/${a}/complete`), 403, 'MSG74')
          c.check('bắt đầu B khi A đang bàn', await chair.post(`${A(m.id)}/${b}/current`), 409, 'MSG72')
          c.check('xoá A đang bàn', await sec.delete(`${A(m.id)}/${a}`), 409, 'MSG142')
          const s1 = await snapshot(qa, 'chair', m.id)
          const before = itemOf(s1, a)
          c.fact('B vẫn chờ', itemOf(s1, b).status === 'Pending')
          c.fact('canSetCurrent B false khi có mục đang bàn', itemOf(s1, b).actions.canSetCurrent === false)
          c.check('sửa A đang bàn', await sec.put(`${A(m.id)}/${a}`, itemBody('Mục A (sửa khi đang bàn)')), 200, 'MSG61')
          const s2 = await snapshot(qa, 'chair', m.id)
          const edited = itemOf(s2, a)
          c.fact('sửa giữ trạng thái/giờ/vị trí', edited.status === 'InProgress' && edited.actualStartedAt === before.actualStartedAt && edited.position === before.position, JSON.stringify(edited))
          const done = await chair.post(`${A(m.id)}/${a}/complete`)
          if (c.check('hoàn thành A', done, 200, 'MSG65')) c.fact('message MSG65', done.body.message === 'Hoàn thành mục nghị sự thành công.', done.body.message)
          c.check('hoàn thành A lần nữa', await chair.post(`${A(m.id)}/${a}/complete`), 409, 'MSG74')
          c.check('xoá A đã xong', await sec.delete(`${A(m.id)}/${a}`), 409, 'MSG142')
          const s3 = await snapshot(qa, 'chair', m.id)
          c.fact('không tự bắt đầu mục kế', itemOf(s3, b).status === 'Pending')
          c.check('sắp xếp có mục đã xong', await sec.put(`${A(m.id)}/order`, { orderVersion: s3.orderVersion, orderedItemIds: [b, a] }), 200, 'MSG63')
          const s4 = await snapshot(qa, 'chair', m.id)
          c.fact('A xuống cuối, vẫn Done, giữ giờ', s4.items.at(-1).id === a && itemOf(s4, a).status === 'Done' && itemOf(s4, a).actualEndedAt === itemOf(s3, a).actualEndedAt)
          const other = await reused(qa, 'InProgress')
          c.check('mục của cuộc họp khác (current)', await chair.post(`${A(other.id)}/${a}/current`), 404)
          c.check('mục của cuộc họp khác (PUT)', await sec.put(`${A(other.id)}/${a}`, itemBody('Lạc cuộc họp')), 404)
        } finally {
          await transition(qa, 'chair', m.id, 'end').catch(() => undefined)
        }
        c.done()
      },
    },

    // ----------------------------------------------------------------------------------------------- UC-27 Conclusion
    {
      id: 'AC15',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Thư ký ghi và sửa kết luận cho mục đang bàn; kết luận hiện kèm người lưu và thời điểm; mục chờ không ghi được (AGENDA_FT40, AGENDA_FT42)',
      steps: [
        'Cuộc họp Đang diễn ra, Chủ toạ đã bắt đầu một mục; thuky@ mở tab Nội dung cuộc họp',
        'Ở mục đang bàn (mở sẵn), bấm "Ghi kết luận", nhập, bấm "Lưu kết luận"',
        'Bấm "Sửa kết luận": form điền sẵn + đếm n/2.000, nút khoá khi trống/không đổi; sửa và lưu',
        'Nhập 2.100 ký tự vào form: giữ 2.000 + đếm "2.000/2.000", lưu được',
        'Mở một mục còn chờ',
      ],
      expected:
        'Toast "Lưu kết luận mục nghị sự thành công." (MSG271); hộp Kết luận hiện nội dung và "Trần Việt Hải · <ngày giờ>", dòng có chip kết luận; "Sửa kết luận" mở form điền sẵn, đếm "n/2.000", nút "Lưu kết luận" khoá khi trống hoặc không đổi (AGENDA_FT40); dán 2.100 ký tự chỉ giữ 2.000, đếm "2.000/2.000", lưu 200 (AGENDA_FT42); mục chờ không có nút "Ghi kết luận"',
      async run(qa) {
        const u = await users(qa)
        const m = await reused(qa, 'InProgress')
        await clearCurrent(qa, m.id)
        const live = await addItem(qa, 'secretary', m.id, 'Mục ghi kết luận')
        const pending = await addItem(qa, 'secretary', m.id, 'Mục còn chờ')
        try {
          qa.expectStatus(await (await client(qa, 'chair')).post(`${A(m.id)}/${live}/current`), 200)
          const snap = await snapshot(qa, 'secretary', m.id)
          const liveTitle = itemOf(snap, live).title
          const pendingTitle = itemOf(snap, pending).title
          await openAgenda(qa, m)
          const r = row(qa, liveTitle)
          const write = r.getByRole('button', { name: 'Ghi kết luận', exact: true })
          await qa.expect(write).toBeVisible()
          await press(qa, write)
          const text = `Thống nhất triển khai phương án ${rand()}.`
          await r.getByLabel(`Kết luận: ${liveTitle}`).fill(text)
          await settled(qa)
          await qa.shot('dang-ghi-ket-luan')
          await press(qa, r.getByRole('button', { name: 'Lưu kết luận', exact: true }))
          await qa.expect(toast(qa, 'Lưu kết luận mục nghị sự thành công.')).toBeVisible()
          await qa.expect(r.getByText(text)).toBeVisible()
          await qa.expect(r.locator('.agenda-stamp')).toContainText(u.secretary.name)
          await qa.expect(r.locator('.agenda-concluded')).toBeVisible()
          await dismissToasts(qa)
          await settled(qa)
          await qa.shot('da-luu-ket-luan')
          await noSidewaysScroll(qa)
          // AGENDA_FT40: editing reopens the form with the current text, a live n/2.000 counter,
          // and a save button locked while empty or unchanged.
          await press(qa, r.getByRole('button', { name: 'Sửa kết luận', exact: true }))
          const editor = r.getByLabel(`Kết luận: ${liveTitle}`)
          await qa.expect(editor).toHaveValue(text)
          await qa.expect(r.getByText(`${text.length}/2.000`)).toBeVisible()
          const saveConclusion = r.getByRole('button', { name: 'Lưu kết luận', exact: true })
          await qa.expect(saveConclusion).toBeDisabled()
          await editor.fill('')
          await qa.expect(saveConclusion).toBeDisabled()
          const edited = `Thống nhất điều chỉnh ${rand()}.`
          await editor.fill(edited)
          await qa.expect(r.getByText(`${edited.length}/2.000`)).toBeVisible()
          await qa.expect(saveConclusion).toBeEnabled()
          await settled(qa)
          await qa.shot('dang-sua-ket-luan')
          await press(qa, saveConclusion)
          await qa.expect(toast(qa, 'Lưu kết luận mục nghị sự thành công.')).toBeVisible()
          await qa.expect(r.getByText(edited)).toBeVisible()
          await qa.expect(r.locator('.agenda-stamp')).toContainText(u.secretary.name)
          await dismissToasts(qa)
          // AGENDA_FT42: pasting 2.100 characters keeps 2.000 and the counter reads "2.000/2.000".
          await press(qa, r.getByRole('button', { name: 'Sửa kết luận', exact: true }))
          await editor.fill('k'.repeat(2100))
          await qa.expect(editor).toHaveValue('k'.repeat(2000))
          await qa.expect(r.getByText('2.000/2.000')).toBeVisible()
          await settled(qa)
          await qa.shot('dem-2000-2000')
          await press(qa, r.getByRole('button', { name: 'Lưu kết luận', exact: true }))
          await qa.expect(toast(qa, 'Lưu kết luận mục nghị sự thành công.')).toBeVisible()
          await dismissToasts(qa)
          await press(qa, row(qa, pendingTitle).locator('button.agenda-title'))
          await qa.expect(row(qa, pendingTitle).getByText('Chưa có kết luận.')).toBeVisible()
          await qa.expect(row(qa, pendingTitle).getByRole('button', { name: 'Ghi kết luận', exact: true })).toHaveCount(0)
          await settled(qa)
          await qa.shot('muc-cho-khong-ghi-ket-luan')
          const after = itemOf(await snapshot(qa, 'secretary', m.id), live)
          qa.expect(after.conclusion).toBe('k'.repeat(2000))
          qa.expect(after.conclusionUpdatedById).toBe(u.secretary.id)
          qa.expect(after.status).toBe('InProgress')
        } finally {
          await (await client(qa, 'chair')).post(`${A(m.id)}/${live}/complete`)
          await cleanup(qa, m.id, [pending])
        }
      },
    },
    {
      id: 'AC16',
      role: 'chair',
      title: 'API kết luận: 1–2.000 ký tự (MSG272), chỉ mục đã bắt đầu (MSG273), Chủ toạ hoặc Thư ký, người ngoài 404 MSG174; ghi lại thay kết luận cũ',
      steps: [
        'PUT conclusion trên mục chờ; rỗng; 2.001 ký tự; đúng 2.000 ký tự (Chủ toạ); lưu lại bởi thuky@',
        'quantri@ (ngoài cuộc họp) PUT; cuộc họp Đã kết thúc chưa có biên bản: Chủ toạ PUT',
      ],
      expected:
        'Mục chờ 409 MSG273; rỗng/2.001: 400 MSG272; 2.000: 200 MSG271; lưu lại: 200, người lưu đổi thành thuky@; quantri@ 404 MSG174; Ended chưa có biên bản 200 MSG271',
      async run(qa) {
        const u = await users(qa)
        const c = checker(qa)
        const chair = await client(qa, 'chair')
        const m = await reused(qa, 'InProgress')
        await clearCurrent(qa, m.id)
        const live = await addItem(qa, 'secretary', m.id, 'Mục kết luận API')
        const pending = await addItem(qa, 'secretary', m.id, 'Mục chờ API')
        const url = (mid, id) => `${A(mid)}/${id}/conclusion`
        try {
          c.check('mục chờ', await chair.put(url(m.id, pending), { conclusion: 'Không được.' }), 409, 'MSG273')
          qa.expectStatus(await chair.post(`${A(m.id)}/${live}/current`), 200)
          c.check('rỗng', await chair.put(url(m.id, live), { conclusion: '   ' }), 400, 'MSG272')
          c.check('2.001 ký tự', await chair.put(url(m.id, live), { conclusion: 'k'.repeat(2001) }), 400, 'MSG272')
          const ok = await chair.put(url(m.id, live), { conclusion: 'k'.repeat(2000) })
          if (c.check('2.000 ký tự', ok, 200, 'MSG271')) c.fact('message', ok.body.message === 'Lưu kết luận mục nghị sự thành công.', ok.body.message)
          c.check('thuky@ lưu lại', await (await client(qa, 'secretary')).put(url(m.id, live), { conclusion: 'Kết luận cuối cùng.' }), 200, 'MSG271')
          const item = itemOf(await snapshot(qa, 'chair', m.id), live)
          c.fact('thay kết luận cũ', item.conclusion === 'Kết luận cuối cùng.' && item.conclusionUpdatedById === u.secretary.id, JSON.stringify(item))
          c.check('quantri@ ngoài cuộc họp', await (await client(qa, 'admin')).put(url(m.id, live), { conclusion: 'Người ngoài.' }), 404, 'MSG174')
          const ended = await reused(qa, 'Ended')
          const doneItem = (await snapshot(qa, 'chair', ended.id)).items.find((i) => i.status === 'Done')
          if (doneItem) c.check('Ended chưa có biên bản', await chair.put(url(ended.id, doneItem.id), { conclusion: 'Thống nhất thông qua báo cáo quý III.' }), 200, 'MSG271')
          else c.fact('cuộc họp Ended có mục đã xong', false)
        } finally {
          await chair.post(`${A(m.id)}/${live}/complete`)
          await cleanup(qa, m.id, [pending])
        }
        c.done()
      },
    },

    // ----------------------------------------------------------------------------------------------- End meeting, Ended view
    {
      id: 'AC17',
      role: 'chair',
      devices: ['desktop', 'phone'],
      title: 'Kết thúc cuộc họp thì mục đang bàn thành Đã xong, mục chờ giữ nguyên; sau đó chương trình bị khoá trên màn hình',
      steps: [
        'Cuộc họp Đang diễn ra có mục đang bàn và mục chờ; Chủ toạ kết thúc cuộc họp (API)',
        'chutoa@ mở tab Nội dung cuộc họp',
      ],
      expected:
        'API: mục đang bàn Done có actualEndedAt, mục chờ vẫn Pending (BR-026); màn hình: không còn "Thêm nội dung", bút chì, thùng rác, tay nắm kéo, "Bắt đầu"/"Xong"; mục đã xong vẫn ghi được kết luận (chưa có biên bản)',
      async run(qa) {
        const m = await freshMeeting(qa, 'InProgress', `kết thúc ${qa.device}`)
        const live = await addItem(qa, 'secretary', m.id, 'Mục đang bàn lúc kết thúc')
        const pending = await addItem(qa, 'secretary', m.id, 'Mục chưa kịp bàn')
        qa.expectStatus(await (await client(qa, 'chair')).post(`${A(m.id)}/${live}/current`), 200)
        await transition(qa, 'chair', m.id, 'end')
        const snap = await snapshot(qa, 'chair', m.id)
        qa.expect(snap.meetingStatus).toBe('Ended')
        qa.expect(itemOf(snap, live).status).toBe('Done')
        qa.expect(itemOf(snap, live).actualEndedAt).toBeTruthy()
        qa.expect(itemOf(snap, pending).status).toBe('Pending')
        const ended = (await (await client(qa, 'chair')).get(`/api/meetings/${m.id}`)).body
        await openAgenda(qa, { ...m, title: ended.title })
        const p = panel(qa)
        const liveTitle = itemOf(snap, live).title
        await qa.expect(row(qa, liveTitle).getByText('Đã xong')).toBeVisible()
        await qa.expect(p.getByRole('button', { name: 'Thêm nội dung' })).toHaveCount(0)
        await qa.expect(p.getByRole('button', { name: /^(Sửa|Xoá|Kéo để đổi vị trí):/ })).toHaveCount(0)
        await qa.expect(p.getByRole('button', { name: /^(Bắt đầu|Xong)$/ })).toHaveCount(0)
        await press(qa, row(qa, liveTitle).locator('button.agenda-title'))
        await qa.expect(row(qa, liveTitle).getByRole('button', { name: 'Ghi kết luận', exact: true })).toBeVisible()
        await settled(qa)
        await qa.shot('da-ket-thuc-khoa')
        await noSidewaysScroll(qa)
      },
    },

    // ----------------------------------------------------------------------------------------------- UC-26 View
    {
      id: 'AC18',
      role: 'member',
      devices: ['desktop', 'phone'],
      title: 'Thành viên xem chương trình của cuộc họp đã kết thúc và mục chờ chỉ đọc: thứ tự, người trình bày, trạng thái, kết luận kèm người lưu và thời điểm (AGENDA_FT44)',
      steps: ['thanhvien@ mở tab Nội dung cuộc họp của cuộc họp Đã kết thúc', 'Mở mục đã xong', 'Mở mục chưa bàn'],
      expected:
        'Mục đã xong có chip "Đã xong" và chip kết luận; mở ra thấy ghi chú, kết luận với tên người lưu và thời điểm, không có nút "Ghi kết luận"; mục chưa bàn không có chip, mở ra "Chưa có kết luận." và không có nút "Ghi kết luận"/"Sửa kết luận" (AGENDA_FT44 E1); mục chờ không có nút; không có nút sửa',
      async run(qa) {
        const m = await reused(qa, 'Ended')
        const snap = await snapshot(qa, 'member', m.id)
        const done = snap.items.find((i) => i.status === 'Done')
        const pending = snap.items.find((i) => i.status === 'Pending')
        qa.expect(done, 'the reused Ended meeting has no Done item').toBeTruthy()
        await openAgenda(qa, m)
        const r = row(qa, done.title)
        await qa.expect(r.getByText('Đã xong')).toBeVisible()
        // The chip is icon-only on a wide screen, its words in the tooltip.
        await qa.expect(r.locator('.agenda-concluded')).toBeVisible()
        await qa.expect(r.locator('.agenda-concluded')).toHaveAttribute('data-tooltip', 'Đã có kết luận')
        if (pending) await qa.expect(row(qa, pending.title).locator('.agenda-status')).toHaveCount(0)
        await press(qa, r.locator('button.agenda-title'))
        await qa.expect(r.getByText(done.conclusion)).toBeVisible()
        await qa.expect(r.locator('.agenda-stamp')).toContainText(done.conclusionUpdatedByName)
        await qa.expect(r.getByText('Phòng Kế hoạch trình bày.')).toBeVisible()
        await qa.expect(r.getByRole('button', { name: /kết luận/ })).toHaveCount(0)
        await qa.expect(panel(qa).getByRole('button', { name: /^(Sửa|Xoá):/ })).toHaveCount(0)
        // AGENDA_FT44 E1: a Pending item opens read-only for the Member too.
        if (pending) {
          await press(qa, row(qa, pending.title).locator('button.agenda-title'))
          await qa.expect(row(qa, pending.title).getByText('Chưa có kết luận.')).toBeVisible()
          await qa.expect(row(qa, pending.title).getByRole('button', { name: /kết luận/ })).toHaveCount(0)
          await settled(qa)
          await qa.shot('thanh-vien-muc-cho-chi-doc')
        }
        await settled(qa)
        await qa.shot('thanh-vien-xem-ket-luan')
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
      },
    },
    {
      id: 'AC19',
      role: 'member',
      title: 'Đang mở chương trình nghị sự thì tự cập nhật khi Thư ký thêm, sửa, xoá mục, không cần tải lại trang',
      steps: ['thanhvien@ mở tab Nội dung cuộc họp của cuộc họp Đã công bố', 'thuky@ (API) thêm một mục, đổi tên mục đó, rồi xoá'],
      expected: 'Mục mới hiện ra, tên mới thay tên cũ, rồi mục biến mất — trên trang của thanhvien@ mà không tải lại',
      async run(qa) {
        const m = await reused(qa, 'Published')
        await openAgenda(qa, m)
        let navigations = 0
        qa.page.on('framenavigated', (f) => f === qa.page.mainFrame() && navigations++)
        const sec = await client(qa, 'secretary')
        const title = `Mục thêm trực tiếp ${rand()}`
        const created = await sec.post(A(m.id), itemBody(title))
        qa.expectStatus(created, 201)
        const id = created.body.agendaItemId
        try {
          await qa.expect(row(qa, title)).toBeVisible({ timeout: 10_000 })
          await settled(qa)
          await qa.shot('tu-hien-muc-moi')
          const renamed = `${title} (đổi tên)`
          qa.expectStatus(await sec.put(`${A(m.id)}/${id}`, itemBody(renamed)), 200)
          await qa.expect(row(qa, renamed)).toBeVisible({ timeout: 10_000 })
          await qa.shot('tu-doi-ten')
          qa.expectStatus(await sec.delete(`${A(m.id)}/${id}`), 200)
          await qa.expect(row(qa, renamed)).toHaveCount(0, { timeout: 10_000 })
          await qa.shot('tu-mat-muc')
          qa.expect(navigations, 'the page reloaded').toBe(0)
        } finally {
          await cleanup(qa, m.id, [id])
        }
      },
    },
    {
      id: 'AC20',
      role: 'member',
      devices: ['desktop', 'phone'],
      title: 'Không tải được chương trình nghị sự thì báo lỗi và cho thử lại, không hiện như chương trình trống (UC-26 EX-2)',
      steps: ['thanhvien@ mở tab Nội dung cuộc họp của cuộc họp Đã kết thúc khi GET agenda trả 500 MSG128', 'Bấm "Thử lại" khi server đã trả bình thường'],
      expected: 'Thông báo "Không tải được chương trình nghị sự." và nút "Thử lại", không có "Chưa có nội dung nào"; thử lại thì danh sách hiện',
      async run(qa) {
        const m = await reused(qa, 'Ended')
        const snap = await snapshot(qa, 'member', m.id)
        const pattern = `**/api/meetings/${m.id}/agenda`
        await qa.page.route(pattern, (route) =>
          route.request().method() === 'GET'
            ? route.fulfill({ status: 500, contentType: 'application/json', body: JSON.stringify({ code: 'MSG128', message: 'Không tải được chương trình nghị sự.' }) })
            : route.continue(),
        )
        await qa.goto(`/mgmt/meetings/${m.id}?tab=agenda`)
        const alert = qa.page.getByRole('alert').filter({ hasText: 'Không tải được chương trình nghị sự.' })
        await qa.expect(alert).toBeVisible()
        await qa.expect(qa.page.getByText('Chưa có nội dung nào')).toHaveCount(0)
        await settled(qa)
        await qa.shot('loi-tai-chuong-trinh')
        await noSidewaysScroll(qa)
        await qa.page.unroute(pattern)
        await press(qa, alert.getByRole('button', { name: 'Thử lại' }))
        await qa.expect(row(qa, snap.items[0].title)).toBeVisible()
        await qa.expect(alert).toHaveCount(0)
        await qa.shot('thu-lai-thanh-cong')
      },
    },
    // ------------------------------------------------------------ AC bổ sung cho Round 1 (R1-FE05)
    {
      id: 'AC21',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Xoá mục có tài liệu gắn kèm: tài liệu vẫn còn trong tab Tài liệu (AGENDA_FT16 E3)',
      steps: [
        'Tải một tệp lên mục nghị sự của cuộc họp Nháp (API)',
        'Thư ký mở tab Nội dung cuộc họp, xoá mục đó sau khi xác nhận',
        'Mở tab Tài liệu',
      ],
      expected:
        'Sau khi xoá: toast MSG62, mục biến mất; tab Tài liệu vẫn còn tệp đã gắn (không bị xoá theo mục)',
      async run(qa) {
        const m = await reused(qa, 'Draft')
        const itemId = await addItem(qa, 'secretary', m.id, 'Mục có tài liệu gắn kèm')
        try {
          await openAgenda(qa, m)
          const base = new URL(qa.page.url()).origin
          const token = await bearer(qa)
          // A real PDF: the server parses the header and counts the pages, so a fake byte stream is refused.
          // Build a minimal one-page PDF inline (same shape as fe-11 makePdf); no fixture file needed.
          const pdfText = 'QA FE-05 gan kem'
          const pdfStream = `BT /F1 20 Tf 72 760 Td (${pdfText}) Tj ET`
          const pdfObjs = [
            '<< /Type /Catalog /Pages 2 0 R >>',
            '<< /Type /Pages /Kids [3 0 R] /Count 1 >>',
            '<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>',
            `<< /Length ${pdfStream.length} >>\nstream\n${pdfStream}\nendstream`,
            '<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>',
          ]
          let pdfOut = '%PDF-1.4\n'
          const pdfOffsets = []
          pdfObjs.forEach((o, i) => {
            pdfOffsets.push(pdfOut.length)
            pdfOut += `${i + 1} 0 obj\n${o}\nendobj\n`
          })
          const pdfXref = pdfOut.length
          pdfOut += `xref\n0 ${pdfObjs.length + 1}\n0000000000 65535 f \n`
          pdfOut += pdfOffsets.map((o) => `${String(o).padStart(10, '0')} 00000 n \n`).join('')
          pdfOut += `trailer\n<< /Size ${pdfObjs.length + 1} /Root 1 0 R >>\nstartxref\n${pdfXref}\n%%EOF\n`
          const pdf = Buffer.from(pdfOut, 'latin1')
          const fileName = `qa-fe05-gan-kem-${Date.now().toString(36)}.pdf`
          const up = await qa.context.request.post(`${base}/api/meetings/${m.id}/agenda/${itemId}/documents/upload`, {
            headers: { Authorization: `Bearer ${token}` },
            multipart: {
              files: { name: fileName, mimeType: 'application/pdf', buffer: pdf },
            },
          })
          qa.expect(up.status(), `tải tệp vào mục: ${await up.text()}`).toBe(200)
          const snap = await snapshot(qa, 'secretary', m.id)
          const title = itemOf(snap, itemId).title
          const r = row(qa, title)
          await qa.expect(r.getByText('1 tài liệu')).toBeVisible()
          await settled(qa)
          await qa.shot('muc-co-tai-lieu-gan-kem')
          await press(qa, r.getByRole('button', { name: `Xoá: ${title}` }))
          await dialog(qa, 'Xoá nội dung').getByRole('button', { name: 'Xoá', exact: true }).click()
          await qa.expect(toast(qa, 'Xoá mục nghị sự thành công.')).toBeVisible()
          await qa.expect(row(qa, title)).toHaveCount(0)
          await qa.page.getByRole('tab', { name: 'Tài liệu' }).click()
          await qa.expect(qa.page.getByText(fileName).first()).toBeVisible({ timeout: 10_000 })
          await settled(qa)
          await qa.shot('tai-lieu-con-sau-khi-xoa-muc')
          await noSidewaysScroll(qa)
          await noRawCodes(qa)
        } finally {
          await cleanup(qa, m.id, [itemId]).catch(() => undefined)
        }
      },
    },
    {
      id: 'AC22',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Cuộc họp Nháp trống: gợi ý khác nhau theo vai trò; tải lỗi thì báo và thử lại được (AGENDA_FT02, AGENDA_FT06)',
      steps: [
        'Thư ký mở tab Nội dung cuộc họp của cuộc họp Nháp trống',
        'Chủ toạ và Thành viên mở cùng tab (API: Member GET Nháp 404)',
        'Thư ký mở lại khi GET agenda 500 MSG128, bấm "Thử lại"',
      ],
      expected:
        'Thư ký/Chủ toạ: "Chưa có nội dung nào" + "Thêm các nội dung cần bàn trong cuộc họp." + nút "Thêm nội dung"; Thành viên: "Chưa có nội dung nào" + "Các nội dung cần bàn sẽ hiện ở đây khi được lập.", không có nút Thêm, API 404; tải lỗi: "Không tải được chương trình nghị sự." + "Thử lại", thử lại thì gợi ý trống hiện',
      async run(qa) {
        const m = await reused(qa, 'Draft')
        // The Draft meeting is shared across criteria: drain leftovers from an earlier failed run first.
        for (const it of (await snapshot(qa, 'secretary', m.id)).items) await cleanup(qa, m.id, [it.id]).catch(() => undefined)
        qa.expect((await snapshot(qa, 'secretary', m.id)).items.length, 'cuộc họp Nháp dùng lại không trống').toBe(0)
        const emptyHint = async (role, canCreate) => {
          await openAgenda(qa, m)
          await qa.expect(panel(qa).getByText('Chưa có nội dung nào')).toBeVisible()
          await qa.expect(
            panel(qa).getByText(
              canCreate ? 'Thêm các nội dung cần bàn trong cuộc họp.' : 'Các nội dung cần bàn sẽ hiện ở đây khi được lập.',
            ),
          ).toBeVisible()
          await qa.expect(panel(qa).getByRole('button', { name: 'Thêm nội dung' })).toHaveCount(canCreate ? 1 : 0)
          await settled(qa)
          await qa.shot(`nhap-trong-${role}`)
          await noSidewaysScroll(qa)
        }
        await emptyHint('secretary', true)
        // Chair và Member xem trên cùng một cuộc họp Nháp.
        const chairClient = await client(qa, 'chair')
        qa.expectStatus(await chairClient.get(A(m.id)), 200)
        // Member không được xem Nháp: màn hình 404, API 404 (AC11 đã phủ, nhắc lại ở đây cho đủ vai trò).
        qa.expectStatus(await (await client(qa, 'member')).get(A(m.id)), 404)
        // AGENDA_FT06: cùng một cuộc họp Nháp, lỗi tải và thử lại (cơ chế như AC20 nhưng ở Nháp và vai trò soạn).
        const pattern = `**/api/meetings/${m.id}/agenda`
        await qa.page.route(pattern, (route) =>
          route.request().method() === 'GET'
            ? route.fulfill({ status: 500, contentType: 'application/json', body: JSON.stringify({ code: 'MSG128', message: 'Không tải được chương trình nghị sự.' }) })
            : route.continue(),
        )
        await qa.goto(`/mgmt/meetings/${m.id}?tab=agenda`)
        const alert = qa.page.getByRole('alert').filter({ hasText: 'Không tải được chương trình nghị sự.' })
        await qa.expect(alert).toBeVisible()
        await qa.expect(panel(qa).getByText('Chưa có nội dung nào')).toHaveCount(0)
        await settled(qa)
        await qa.shot('nhap-loi-tai')
        await qa.page.unroute(pattern)
        await press(qa, alert.getByRole('button', { name: 'Thử lại' }))
        await qa.expect(panel(qa).getByText('Chưa có nội dung nào')).toBeVisible()
        await qa.expect(alert).toHaveCount(0)
        await qa.shot('nhap-thu-lai-duoc')
      },
    },
    {
      id: 'AC23',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Hộp Thêm nội dung: danh sách người trình bày, gợi ý và lưu không bắt buộc (AGENDA_FT10)',
      steps: ['Mở "Thêm nội dung" trên cuộc họp Nháp', 'Mở danh sách "Người trình bày"', 'Lưu mà không chọn người trình bày'],
      expected:
        'Danh sách có "Chưa phân công" và các thành viên nội bộ đang hoạt động (Chủ toạ, Thư ký, Thành viên); gợi ý "Không bắt buộc, có thể chọn sau."; lưu được, mục mới presenterId null',
      async run(qa) {
        const u = await users(qa)
        const m = await reused(qa, 'Draft')
        await openAgenda(qa, m)
        await press(qa, panel(qa).getByRole('button', { name: 'Thêm nội dung' }))
        const form = dialog(qa, 'Thêm nội dung')
        const title = `Mục không giao ai ${rand()}`
        await form.getByPlaceholder('VD: Dự toán ngân sách năm 2027').fill(title)
        await form.getByRole('button', { name: /^Người trình bày/ }).click()
        const list = qa.page.getByRole('listbox', { name: 'Người trình bày' })
        await qa.expect(list.getByRole('option', { name: 'Chưa phân công', exact: true })).toBeVisible()
        await qa.expect(list.getByRole('option', { name: u.chair.name })).toBeVisible()
        await qa.expect(list.getByRole('option', { name: u.secretary.name })).toBeVisible()
        await qa.expect(list.getByRole('option', { name: u.member.name })).toBeVisible()
        qa.note('Danh sách người trình bày = Chưa phân công + Chủ toạ + Thư ký + Thành viên đang hoạt động.')
        await qa.expect(form.getByText('Không bắt buộc, có thể chọn sau.')).toBeVisible()
        await settled(qa)
        await qa.shot('danh-sach-nguoi-trinh-bay')
        await qa.page.keyboard.press('Escape')
        qa.note('Khách "(khách)" trong danh sách chưa phủ: cuộc họp Nháp dùng lại không có lời mời Khách.')
        await form.getByRole('button', { name: 'Lưu' }).click()
        await qa.expect(toast(qa, 'Tạo mục nghị sự thành công.')).toBeVisible()
        const r = row(qa, title)
        // Layout hiện tại không còn cột người trình bày ở dòng (chỉ số + tên + tài liệu + thao tác):
        // mục không giao ai hiện đúng như các dòng khác, không nhãn nào thêm.
        await qa.expect(r).toBeVisible()
        await dismissToasts(qa)
        await dismissToasts(qa)
        const snap = await snapshot(qa, 'secretary', m.id)
        qa.expect(itemOf(snap, snap.items.find((i) => i.title === title).id).presenterId).toBeNull()
        await settled(qa)
        await qa.shot('muc-khong-giao-ai')
        await noSidewaysScroll(qa)
        await cleanup(qa, m.id, [snap.items.find((i) => i.title === title).id])
      },
    },
    {
      id: 'AC24',
      role: 'secretary',
      title: 'Người trình bày bị gỡ khỏi cuộc họp: presenter bị xoá hẳn, không giữ tên cũ (AGENDA_FT14 — case cũ đòi giữ tên)',
      steps: [
        'Tạo mục, giao cho Thành viên, rồi gỡ Thành viên khỏi thành phần (API)',
        'Mở tab Nội dung cuộc họp, xem dòng và hộp Sửa',
      ],
      expected:
        'Sau khi gỡ: dòng không còn tên người cũ (backend xoá presenterId và đặt tên cũ thành rỗng, event PresenterCleared), tooltip "Chưa có người trình bày"; hộp Sửa không nêu "Trước đây:" — case cũ đòi giữ tên cũ, đề nghị BA sửa',
      async run(qa) {
        const u = await users(qa)
        const sec = await client(qa, 'secretary')
        // A dedicated Draft meeting: removing the Member here must not disturb the reused one.
        const m = await freshMeeting(qa, 'Draft', 'người cũ')
        const id = await addItem(qa, 'secretary', m.id, 'Mục người cũ', { presenterId: u.member.id })
        try {
          await qa.goto('/calendar')
          // Gỡ Thành viên khỏi cuộc họp: displayName trong roster mất đi, presenterId thành tên cũ đã lưu.
          const parts = await sec.get(`/api/meetings/${m.id}/participants?pageSize=100`)
          const etag = parts.headers.etag ?? `"participants-${parts.body.revision}"`
          const memberPart = parts.body.items.find((p) => p.userId === u.member.id)
          const del = await qa.context.request.delete(`${new URL(qa.page.url()).origin}/api/meetings/${m.id}/participants/${memberPart.id}`, {
            headers: { Authorization: `Bearer ${await bearer(qa)}`, 'If-Match': etag },
          })
          qa.expect(del.status(), `gỡ thành viên: ${await del.text()}`).toBe(200)
          const snap = await snapshot(qa, 'secretary', m.id)
          const item = itemOf(snap, id)
          qa.expect(item.presenterId, 'presenterId còn sau khi gỡ').toBeNull()
          qa.note(`Backend xoá presenter khi gỡ (presenterId=null, legacy=${JSON.stringify(item.legacyPresenterName)}) — case cũ đòi giữ tên cũ.`)
          const title = item.title
          await openAgenda(qa, m)
          const r = row(qa, title)
          await qa.expect(r.getByText(u.member.name)).toHaveCount(0)
          await qa.expect(r.locator('.agenda-presenter-none')).toHaveAttribute('data-tooltip', 'Chưa có người trình bày')
          await settled(qa)
          await qa.shot('nguoi-trinh-bay-bi-go-mat-ten')
          await press(qa, r.getByRole('button', { name: `Sửa: ${title}` }))
          const form = dialog(qa, 'Sửa nội dung')
          await qa.expect(form.getByText(/Trước đây:/)).toHaveCount(0)
          await settled(qa)
          await qa.shot('sua-khong-con-ten-cu')
          await noSidewaysScroll(qa)
        } finally {
          await transition(qa, 'chair', m.id, 'end').catch(() => undefined)
        }
      },
    },
    {
      id: 'AC25',
      role: 'secretary',
      title: 'Huỷ form Sửa khi đã nhập thì hỏi "Bỏ thay đổi chưa lưu?" (AGENDA_FT15)',
      steps: ['Mở hộp Sửa của một mục, đổi tên', 'Bấm Huỷ → "Tiếp tục sửa" giữ chữ → Huỷ → "Bỏ thay đổi"'],
      expected: 'Hộp hỏi nêu "Những gì bạn vừa nhập sẽ không được lưu."; "Tiếp tục sửa" giữ tên đã đổi; "Bỏ thay đổi" đóng, không gửi PUT, tên mục không đổi',
      async run(qa) {
        const m = await reused(qa, 'Draft')
        const id = await addItem(qa, 'secretary', m.id, 'Mục huỷ sửa')
        let puts = 0
        qa.page.on('request', (r) => {
          if (r.method() === 'PUT' && /\/api\/meetings\/.+\/agenda\//.test(r.url())) puts += 1
        })
        try {
          const snap = await snapshot(qa, 'secretary', m.id)
          const title = itemOf(snap, id).title
          await openAgenda(qa, m)
          await press(qa, row(qa, title).getByRole('button', { name: `Sửa: ${title}` }))
          const form = dialog(qa, 'Sửa nội dung')
          const typed = `${title} (sửa dở)`
          await form.getByPlaceholder('VD: Dự toán ngân sách năm 2027').fill(typed)
          await form.getByRole('button', { name: 'Huỷ' }).click()
          const confirm = dialog(qa, 'Bỏ thay đổi chưa lưu?')
          await qa.expect(confirm).toBeVisible()
          await qa.expect(confirm).toContainText('Những gì bạn vừa nhập sẽ không được lưu.')
          await qa.shot('hoi-bo-sua-muc')
          await confirm.getByRole('button', { name: 'Tiếp tục sửa' }).click()
          await qa.expect(form.getByPlaceholder('VD: Dự toán ngân sách năm 2027')).toHaveValue(typed)
          await form.getByRole('button', { name: 'Huỷ' }).click()
          await confirm.getByRole('button', { name: 'Bỏ thay đổi' }).click()
          await qa.expect(form).toHaveCount(0)
          qa.expect(itemOf(await snapshot(qa, 'secretary', m.id), id).title).toBe(title)
          qa.expect(puts, 'a discarded edit was sent').toBe(0)
        } finally {
          await cleanup(qa, m.id, [id]).catch(() => undefined)
        }
      },
    },
    {
      id: 'AC26',
      role: 'secretary',
      title: 'Lưu bị từ chối do trạng thái đổi giữa chừng: báo trong hộp, danh sách tải lại (AGENDA_FT19)',
      steps: ['Mở "Thêm nội dung" trên cuộc họp Nháp, nhập tên', 'Cuộc họp kết thúc qua đủ bước (API), bấm "Lưu"'],
      expected: '409 MSG67; hộp báo "Trạng thái cuộc họp không cho phép thao tác này."; không tạo mục; họp đã kết thúc, mục không được tạo',
      async run(qa) {
        // A dedicated Draft meeting: ending it must not disturb the reused one.
        const m = await freshMeeting(qa, 'Draft', 'giữa chừng')
        await openAgenda(qa, m)
        await press(qa, panel(qa).getByRole('button', { name: 'Thêm nội dung' }))
        const form = dialog(qa, 'Thêm nội dung')
        await form.getByPlaceholder('VD: Dự toán ngân sách năm 2027').fill(`Mục giữa chừng ${rand()}`)
        // The meeting is driven all the way to Ended behind the open dialog.
        await transition(qa, 'secretary', m.id, 'submit')
        await transition(qa, 'chair', m.id, 'approve')
        await transition(qa, 'secretary', m.id, 'publish')
        await transition(qa, 'chair', m.id, 'start')
        await transition(qa, 'chair', m.id, 'end')
        const posted = qa.page.waitForResponse((r) => r.url().endsWith('/agenda') && r.request().method() === 'POST')
        await form.getByRole('button', { name: 'Lưu' }).click()
        qa.expect((await posted).status()).toBe(409)
        await qa.expect(form.getByText('Trạng thái cuộc họp không cho phép thao tác này.')).toBeVisible()
        await qa.expect(form).toBeVisible()
        await settled(qa)
        await qa.shot('luu-bi-tu-choi-giua-chung')
        await noRawCodes(qa)
        qa.expect((await snapshot(qa, 'secretary', m.id)).items.length, 'mục vẫn được tạo').toBe(0)
      },
    },
    {
      id: 'AC27',
      role: 'chair',
      title: 'Chủ toạ ở cuộc họp Đã công bố không thấy "Bắt đầu" (AGENDA_FT30 một phần)',
      steps: ['Đăng nhập chutoa@, mở tab Nội dung cuộc họp của cuộc họp Đã công bố'],
      expected: 'Không có nút "Bắt đầu" nào trên màn hình (API 409 MSG73 đã phủ ở AC14)',
      async run(qa) {
        const m = await reused(qa, 'Published')
        await openAgenda(qa, m)
        await qa.expect(panel(qa).getByRole('button', { name: 'Bắt đầu' })).toHaveCount(0)
        await settled(qa)
        await qa.shot('chu-toa-cong-bo-khong-bat-dau')
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
      },
    },
    {
      id: 'AC28',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Thư ký ở cuộc họp Đang diễn ra không thấy "Bắt đầu"/"Xong" (AGENDA_FT30, AGENDA_FT35 góc Thư ký)',
      steps: ['Cuộc họp Đang diễn ra (không có mục nào đang bàn); thuky@ mở tab Nội dung cuộc họp'],
      expected: 'Không có nút "Bắt đầu" và "Xong" nào trên màn hình; mục chờ hiển thị bình thường (API 403 đã phủ ở AC10/AC14)',
      async run(qa) {
        const m = await reused(qa, 'InProgress')
        await clearCurrent(qa, m.id)
        await openAgenda(qa, m)
        await qa.expect(panel(qa).getByRole('button', { name: 'Bắt đầu' })).toHaveCount(0)
        await qa.expect(panel(qa).getByRole('button', { name: 'Xong' })).toHaveCount(0)
        await settled(qa)
        await qa.shot('thu-ky-dang-dien-ra-chi-doc')
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
      },
    },
    {
      id: 'AC29',
      role: 'chair',
      title: 'REMOVED: bấm Bắt đầu/Xong trên trang cũ cần hai trình duyệt thật (AGENDA_FT29, FT31) — xem R1.md',
      steps: ['REMOVED'],
      expected: 'REMOVED',
      async run(qa) {
        qa.inconclusive('Cần hai trình duyệt thật: runner một phiên tự nhận realtime nên nút biến mất trước khi bấm. FT29/FT31 để trống (AX).')
      },
    },
    {
      id: 'AC30',
      role: 'chair',
      title: 'REMOVED: bấm Xong trên trang cũ cần hai trình duyệt thật (AGENDA_FT36) — xem R1.md',
      steps: ['REMOVED'],
      expected: 'REMOVED',
      async run(qa) {
        qa.inconclusive('Cần hai trình duyệt thật: runner một phiên tự nhận realtime nên nút Xong biến mất trước khi bấm. FT36 để trống (AX).')
      },
    },
    {
      id: 'AC31',
      role: 'chair',
      devices: ['desktop', 'phone'],
      title: 'Chủ toạ ghi kết luận cho mục đang bàn trên màn hình (AGENDA_FT39)',
      steps: ['Cuộc họp Đang diễn ra, mục đang bàn; chutoa@ mở tab Nội dung cuộc họp', 'Bấm "Ghi kết luận", nhập, "Lưu kết luận"'],
      expected: 'Toast MSG271; hộp Kết luận hiện nội dung và "<tên chủ toạ> · <ngày giờ>", dòng có chip "Đã kết luận"; giữ sau tải lại',
      async run(qa) {
        const u = await users(qa)
        const m = await reused(qa, 'InProgress')
        await clearCurrent(qa, m.id)
        const live = await addItem(qa, 'secretary', m.id, 'Mục chủ toạ ghi')
        try {
          qa.expectStatus(await (await client(qa, 'chair')).post(`${A(m.id)}/${live}/current`), 200)
          const snap = await snapshot(qa, 'chair', m.id)
          const title = itemOf(snap, live).title
          await openAgenda(qa, m)
          const r = row(qa, title)
          await press(qa, r.getByRole('button', { name: 'Ghi kết luận', exact: true }))
          const text = `Chủ toạ kết luận ${rand()}.`
          await r.getByLabel(`Kết luận: ${title}`).fill(text)
          await settled(qa)
          await qa.shot('chu-toa-dang-ghi-ket-luan')
          await press(qa, r.getByRole('button', { name: 'Lưu kết luận', exact: true }))
          await qa.expect(toast(qa, 'Lưu kết luận mục nghị sự thành công.')).toBeVisible()
          await qa.expect(r.getByText(text)).toBeVisible()
          await qa.expect(r.locator('.agenda-stamp')).toContainText(u.chair.name)
          await qa.expect(r.locator('.agenda-concluded')).toBeVisible()
          await qa.goto(`/mgmt/meetings/${m.id}?tab=agenda`)
          await qa.expect(row(qa, title).getByText(text)).toBeVisible()
          await qa.shot('chu-toa-da-luu-ket-luan')
          await noSidewaysScroll(qa)
          const after = itemOf(await snapshot(qa, 'chair', m.id), live)
          qa.expect(after.conclusionUpdatedById).toBe(u.chair.id)
        } finally {
          await (await client(qa, 'chair')).post(`${A(m.id)}/${live}/complete`)
        }
      },
    },
    {
      id: 'AC32',
      role: 'secretary',
      title: 'Huỷ editor kết luận giữ nguyên kết luận đã lưu (AGENDA_FT43)',
      steps: ['Mục đang bàn đã có kết luận; bấm "Sửa kết luận", gõ thêm, bấm "Huỷ"', 'Mở editor ở mục chưa có kết luận, gõ, bấm "Huỷ"'],
      expected: 'Bấm "Huỷ" đóng editor ngay (editor kết luận không hỏi Bỏ thay đổi), kết luận cũ còn nguyên; mục chưa có kết luận vẫn "Chưa có kết luận."',
      async run(qa) {
        const m = await reused(qa, 'InProgress')
        await clearCurrent(qa, m.id)
        const live = await addItem(qa, 'secretary', m.id, 'Mục huỷ editor')
        const bare = await addItem(qa, 'secretary', m.id, 'Mục chưa ghi')
        try {
          const chair = await client(qa, 'chair')
          qa.expectStatus(await chair.post(`${A(m.id)}/${live}/current`), 200)
          qa.expectStatus(await (await client(qa, 'secretary')).put(`${A(m.id)}/${live}/conclusion`, { conclusion: 'Kết luận đã lưu.' }), 200)
          const snap = await snapshot(qa, 'secretary', m.id)
          const [liveTitle, bareTitle] = [itemOf(snap, live).title, itemOf(snap, bare).title]
          await openAgenda(qa, m)
          const openRow = async (title) => {
            // Mục đang bàn tự mở; mục khác thì đóng — chỉ bấm khi đang đóng.
            if (!(await row(qa, title).locator('.agenda-body').count())) {
              await press(qa, row(qa, title).locator('button.agenda-title'))
            }
          }
          await openRow(liveTitle)
          const editorRow = row(qa, liveTitle)
          await editorRow.scrollIntoViewIfNeeded()
          await press(qa, editorRow.getByRole('button', { name: 'Sửa kết luận', exact: true }))
          await row(qa, liveTitle).getByLabel(`Kết luận: ${liveTitle}`).fill('Kết luận đã lưu. Gõ dở.')
          await row(qa, liveTitle).getByRole('button', { name: 'Huỷ', exact: true }).click()
          await qa.expect(row(qa, liveTitle).getByText('Kết luận đã lưu.')).toBeVisible()
          await qa.expect(row(qa, liveTitle).getByText('Gõ dở')).toHaveCount(0)
          await settled(qa)
          await qa.shot('huy-editor-giu-ket-luan')
          // Mục chưa có kết luận cũng phải đang bàn mới có nút Ghi: hoàn thành live rồi bắt đầu bare.
          await (await client(qa, 'chair')).post(`${A(m.id)}/${live}/complete`)
          qa.expectStatus(await (await client(qa, 'chair')).post(`${A(m.id)}/${bare}/current`), 200)
          await qa.goto(`/mgmt/meetings/${m.id}?tab=agenda`)
          await openRow(bareTitle)
          await press(qa, row(qa, bareTitle).getByRole('button', { name: 'Ghi kết luận', exact: true }))
          await row(qa, bareTitle).getByLabel(`Kết luận: ${bareTitle}`).fill('Gõ dở rồi bỏ.')
          await row(qa, bareTitle).getByRole('button', { name: 'Huỷ', exact: true }).click()
          await qa.expect(row(qa, bareTitle).getByText('Chưa có kết luận.')).toBeVisible()
        } finally {
          await (await client(qa, 'chair')).post(`${A(m.id)}/${bare}/complete`)
          await cleanup(qa, m.id, [bare])
        }
      },
    },
    {
      id: 'AC33',
      role: 'secretary',
      title: 'Mất quyền xem giữa chừng: PUT kết luận 404, tab hiện thông báo mất quyền (AGENDA_FT46 — case đòi giữ editor/inline, sản phẩm thay cả tab)',
      steps: ['Mục đang bàn; thuky@ mở editor và gõ kết luận (chặn realtime để editor không bị gỡ trước)', 'Chủ toạ gỡ thuky@ khỏi cuộc họp (API)', 'Bấm "Lưu kết luận"'],
      expected: 'PUT 404 MSG174; editor báo lỗi rồi tab tải lại thành "Bạn không còn xem được nội dung của cuộc họp này." (không giữ editor/chữ như E1 FT46 — case cũ, đề nghị BA sửa)',
      async run(qa) {
        const u = await users(qa)
        const { request: playwrightRequest } = await import('@playwright/test')
        const chairCtx = await playwrightRequest.newContext()
        // FT46 needs a meeting the secretary can no longer see at all: it must be organised by
        // someone else (the organiser keeps read access even after being removed from participants).
        const ORG = 'qa-fe05-tochuc@trustcabinet.local'
        let m = null
        try {
          m = await createAt(qa, 'InProgress', qa.name(`FE-05 mất quyền ${rand()}`), ORG)
          qa.note(`Tạo: ${m.title} (${m.id}, InProgress, tổ chức bởi ${ORG})`)
          const live = await addItem(qa, 'secretary', m.id, 'Mục mất quyền')
          // Chặn realtime: giữ editor mở cho tới khi bấm Lưu (không để participantsChanged gỡ editor trước).
          await qa.page.route('**/hubs/**', (route) => route.abort('connectionfailed'))
          try {
            qa.expectStatus(await (await client(qa, 'chair')).post(`${A(m.id)}/${live}/current`), 200)
            const snap = await snapshot(qa, 'secretary', m.id)
            const title = itemOf(snap, live).title
            await openAgenda(qa, m)
            const base = new URL(qa.page.url()).origin
            const r = row(qa, title)
            await press(qa, r.getByRole('button', { name: 'Ghi kết luận', exact: true }))
            const typed = `Kết luận giữa chừng ${rand()}.`
            await r.getByLabel(`Kết luận: ${title}`).fill(typed)
            // Chair removes the secretary behind the open editor, through an independent request context.
            // A meeting must keep a Secretary: quantri@ steps in first, so removing thuky@ is allowed.
            const login = await chairCtx.post(`${base}/api/auth/login`, {
              data: { email: u.chair.email, password: process.env.QA_PASSWORD ?? 'TrustCabinet@2026' },
            })
            qa.expect(login.status(), 'chair login').toBe(200)
            const chairToken = (await login.json()).accessToken
            const chead = { Authorization: `Bearer ${chairToken}` }
            const parts = await chairCtx.get(`${base}/api/meetings/${m.id}/participants?pageSize=100`, { headers: chead })
            qa.expect(parts.status()).toBe(200)
            const body = await parts.json()
            const adminId = (await (await client(qa, 'admin')).get('/api/auth/me')).body.id
            const added = await chairCtx.post(`${base}/api/meetings/${m.id}/participants`, {
              headers: { ...chead, 'If-Match': parts.headers()['etag'] ?? `"participants-${body.revision}"` },
              data: { userIds: [adminId], role: 'Secretary' },
            })
            qa.expect(added.status(), `thêm thư ký thay thế: ${await added.text()}`).toBe(201)
            const parts2 = await chairCtx.get(`${base}/api/meetings/${m.id}/participants?pageSize=100`, { headers: chead })
            const body2 = await parts2.json()
            const secPart = body2.items.find((p) => p.userId === u.secretary.id)
            const del = await chairCtx.delete(`${base}/api/meetings/${m.id}/participants/${secPart.id}`, {
              headers: { Authorization: `Bearer ${chairToken}`, 'If-Match': parts2.headers()['etag'] ?? `"participants-${body2.revision}"` },
            })
            qa.expect(del.status(), `gỡ thư ký: ${await del.text()}`).toBe(200)
            const failed = qa.page.waitForResponse((r) => r.url().endsWith(`/agenda/${live}/conclusion`))
            await press(qa, r.getByRole('button', { name: 'Lưu kết luận', exact: true }))
            const failedRes = await failed
            let failedBody = ''
            try { failedBody = JSON.stringify(await failedRes.json()).slice(0, 300) } catch { failedBody = '(no json body)' }
            qa.note(`PUT conclusion sau khi mất quyền: HTTP ${failedRes.status()} ${failedBody}`)
            qa.expect(failedRes.status()).toBe(404)
            qa.expect(failedBody).toContain('MSG174')
            // Sản phẩm thay cả tab bằng thông báo mất quyền (không giữ editor/inline như E1 FT46).
            await qa.expect(qa.page.getByText('Bạn không còn xem được nội dung của cuộc họp này.')).toBeVisible({ timeout: 15000 })
            await settled(qa)
            await qa.shot('mat-quyen-gone')
            await noRawCodes(qa)
          } finally {
            await transition(qa, 'chair', m.id, 'end').catch(() => undefined)
          }
        } finally {
          await qa.page.unroute('**/hubs/**').catch(() => undefined)
          await chairCtx.dispose()
        }
      },
    },
    {
      id: 'AC34',
      role: 'secretary',
      title: 'Máy chủ lỗi 503 giữa chừng: hộp ở lại, giá trị và thứ tự giữ nguyên (AGENDA_FT20, FT27, FT32, FT47)',
      steps: [
        'Nháp: chặn POST agenda 503, bấm Lưu trong hộp Thêm',
        'Nháp: chặn PUT agenda 503, bấm Lưu trong hộp Sửa',
        'Nháp: chặn PUT order 503, kéo tay nắm đổi thứ tự',
        'InProgress: chặn PUT conclusion 503, bấm Lưu kết luận (lỗi hiện inline trong editor)',
      ],
      expected: 'Mỗi lần: hộp/editor ở lại, đúng mã MSG68/MSG71/MSG274 "Không lưu được thao tác nghị sự. Vui lòng thử lại.", giá trị giữ nguyên; thứ tự và trạng thái trên API không đổi, tải lại về đúng (FT32)',
      async run(qa) {
        const notSaved = 'Không lưu được thao tác nghị sự. Vui lòng thử lại.'
        const fail503 = async (pattern, method, code) => {
          await qa.page.route(pattern, (route) =>
            route.request().method() === method
              ? route.fulfill({ status: 503, contentType: 'application/json', body: JSON.stringify({ code, message: notSaved }) })
              : route.continue(),
          )
        }
        // A dedicated Draft meeting: leftovers from earlier runs must not disturb this criterion.
        const m = await freshMeeting(qa, 'Draft', 'lỗi 503 nháp')
        qa.note('AC34: thêm mục pending')
        const sec34 = await client(qa, 'secretary')
        const mkPending = await sec34.post(A(m.id), itemBody('Mục 503'))
        qa.note(`AC34 add pending: HTTP ${mkPending.status}`)
        qa.expectStatus(mkPending, 201)
        const pending = mkPending.body.agendaItemId
        try {
          const snap = await snapshot(qa, 'secretary', m.id)
          const title = itemOf(snap, pending).title
          await openAgenda(qa, m)
          // FT20: POST 503 MSG68 keeps the create dialog and its values.
          await fail503(`**/api/meetings/${m.id}/agenda`, 'POST', 'MSG68')
          await press(qa, panel(qa).getByRole('button', { name: 'Thêm nội dung' }))
          const create = dialog(qa, 'Thêm nội dung')
          const typed = `Mục lỗi 503 ${rand()}`
          await create.getByPlaceholder('VD: Dự toán ngân sách năm 2027').fill(typed)
          await create.getByRole('button', { name: 'Lưu' }).click()
          await qa.expect(create.getByText(notSaved)).toBeVisible()
          await qa.expect(create.getByPlaceholder('VD: Dự toán ngân sách năm 2027')).toHaveValue(typed)
          await qa.expect(create).toBeVisible()
          await qa.shot('post-503-hop-o-lai')
          await create.getByRole('button', { name: 'Huỷ' }).click()
          await dialog(qa, 'Bỏ thay đổi chưa lưu?').getByRole('button', { name: 'Bỏ thay đổi' }).click()
          await qa.page.unroute(`**/api/meetings/${m.id}/agenda`)
          // FT27: PUT 503 MSG68 keeps the edit dialog and its values.
          await fail503(`**/api/meetings/${m.id}/agenda/${pending}`, 'PUT', 'MSG68')
          await press(qa, row(qa, title).getByRole('button', { name: `Sửa: ${title}` }))
          const edit = dialog(qa, 'Sửa nội dung')
          await edit.getByPlaceholder('VD: Dự toán ngân sách năm 2027').fill(`${title} (sửa lỗi)`)
          await edit.getByRole('button', { name: 'Lưu' }).click()
          await qa.expect(edit.getByText(notSaved)).toBeVisible()
          await qa.expect(edit.getByPlaceholder('VD: Dự toán ngân sách năm 2027')).toHaveValue(`${title} (sửa lỗi)`)
          await qa.expect(edit).toBeVisible()
          await qa.shot('put-503-hop-o-lai')
          await edit.getByRole('button', { name: 'Huỷ' }).click()
          await dialog(qa, 'Bỏ thay đổi chưa lưu?').getByRole('button', { name: 'Bỏ thay đổi' }).click()
          await qa.page.unroute(`**/api/meetings/${m.id}/agenda/${pending}`)
          // FT32: PUT order 503 MSG71 — the screen keeps the dragged order, the API keeps the saved one;
          // reloading shows the saved order again.
          qa.note('AC34: thêm mục other')
          const other = await addItem(qa, 'secretary', m.id, 'Mục đứng yên 503')
          try {
            const before = (await snapshot(qa, 'secretary', m.id)).items.map((i) => i.id)
            const moverTitle = itemOf(await snapshot(qa, 'secretary', m.id), pending).title
            await qa.goto(`/mgmt/meetings/${m.id}?tab=agenda`)
            await fail503(`**/api/meetings/${m.id}/agenda/order`, 'PUT', 'MSG71')
            const grip = row(qa, moverTitle).getByRole('button', { name: `Kéo để đổi vị trí: ${moverTitle}` })
            const from = await grip.boundingBox()
            await qa.page.mouse.move(from.x + from.width / 2, from.y + from.height / 2)
            await qa.page.mouse.down()
            await qa.page.mouse.move(from.x + from.width / 2, from.y + from.height / 2 + 220, { steps: 12 })
            await qa.page.mouse.up()
            await qa.expect(panel(qa).locator('li.agenda-item .agenda-title').first()).toContainText(moverTitle, { timeout: 10_000 })
            await qa.page.unroute(`**/api/meetings/${m.id}/agenda/order`)
            await qa.expect(toast(qa, notSaved)).toBeVisible()
            await dismissToasts(qa)
            qa.expect((await snapshot(qa, 'secretary', m.id)).items.map((i) => i.id)).toEqual(before)
            const reloaded = qa.page.waitForResponse((r) => r.url().endsWith('/agenda') && r.request().method() === 'GET')
            await qa.page.reload()
            await reloaded
            const firstTitle = itemOf(await snapshot(qa, 'secretary', m.id), before[0]).title
            await qa.expect(panel(qa).locator('li.agenda-item .agenda-title').first()).toContainText(firstTitle, { timeout: 15_000 })
            await qa.shot('order-503-tra-lai-thu-tu')
          } finally {
            await cleanup(qa, m.id, [other])
          }
          // FT47: PUT conclusion 503 MSG274 keeps the editor with the text (needs an InProgress item).
          qa.note('AC34: thêm mục conc')
          const mLive = await freshMeeting(qa, 'InProgress', 'lỗi 503 kết luận')
          const conc = await addItem(qa, 'secretary', mLive.id, 'Mục lỗi kết luận 503')
          const mid = mLive.id
          try {
            const chair = await client(qa, 'chair')
            qa.expectStatus(await chair.post(`${A(mid)}/${conc}/current`), 200)
            const snap = await snapshot(qa, 'secretary', mid)
            const tConc = itemOf(snap, conc).title
            await qa.goto(`/mgmt/meetings/${mid}?tab=agenda`)
            await fail503(`**/api/meetings/${mid}/agenda/${conc}/conclusion`, 'PUT', 'MSG274')
            await press(qa, row(qa, tConc).getByRole('button', { name: 'Ghi kết luận', exact: true }))
            await row(qa, tConc).getByLabel(`Kết luận: ${tConc}`).fill('Kết luận lỗi 503.')
            await press(qa, row(qa, tConc).getByRole('button', { name: 'Lưu kết luận', exact: true }))
            // The conclusion editor reports the failure inline (field-error), not as a toast.
            await qa.expect(row(qa, tConc).locator('.field-error', { hasText: notSaved })).toBeVisible()
            await qa.expect(row(qa, tConc).getByLabel(`Kết luận: ${tConc}`)).toHaveValue('Kết luận lỗi 503.')
            await qa.expect(row(qa, tConc).getByRole('button', { name: 'Lưu kết luận', exact: true })).toBeVisible()
            await qa.page.unroute(`**/api/meetings/${mid}/agenda/${conc}/conclusion`)
            await settled(qa)
            await qa.shot('conclusion-503-giu-editor')
            await noRawCodes(qa)
          } finally {
            await cleanup(qa, mid, [conc]).catch(() => undefined)
            await transition(qa, 'chair', mid, 'end').catch(() => undefined)
          }
        } finally {
          await cleanup(qa, m.id, [pending]).catch(() => undefined)
        }
      },
    },
    {
      id: 'AC39',
      role: 'chair',
      title: 'Máy chủ lỗi 503 giữa chừng ở Đang diễn ra: mục giữ trạng thái, nút bấm lại được (AGENDA_FT32, FT38)',
      steps: [
        'InProgress: chặn POST current 503 MSG75, bấm Bắt đầu',
        'Chặn POST complete 503 MSG76, bấm Xong ở mục đang bàn',
      ],
      expected:
        '503: toast "Không lưu được thao tác nghị sự. Vui lòng thử lại."; mục vẫn chờ/đang bàn, nút "Bắt đầu"/"Xong" còn đó bấm lại được; API giữ nguyên',
      async run(qa) {
        const notSaved = 'Không lưu được thao tác nghị sự. Vui lòng thử lại.'
        const fail503 = async (pattern, method, code) => {
          await qa.page.route(pattern, (route) =>
            route.request().method() === method
              ? route.fulfill({ status: 503, contentType: 'application/json', body: JSON.stringify({ code, message: notSaved }) })
              : route.continue(),
          )
        }
        const live2 = await freshMeeting(qa, 'InProgress', 'lỗi 503 trực tiếp')
        const a = await addItem(qa, 'secretary', live2.id, 'Mục lỗi current')
        const b = await addItem(qa, 'secretary', live2.id, 'Mục lỗi complete')
        try {
          const chair = await client(qa, 'chair')
          qa.expectStatus(await chair.post(`${A(live2.id)}/${b}/current`), 200)
          const snap = await snapshot(qa, 'chair', live2.id)
          const [tA, tB] = [a, b].map((id) => itemOf(snap, id).title)
          await openAgenda(qa, live2)
          await qa.expect(panel(qa).locator('li.agenda-item').first()).toBeVisible()
          // Complete B 503 trước (B đang bàn), rồi hoàn thành thật để test current A.
          await fail503(`**/api/meetings/${live2.id}/agenda/${b}/complete`, 'POST', 'MSG76')
          await press(qa, row(qa, tB).getByRole('button', { name: 'Xong' }))
          await qa.expect(row(qa, tB).getByText('Đã xong')).toHaveCount(0)
          await qa.expect(toast(qa, notSaved)).toBeVisible()
          await dismissToasts(qa)
          await qa.expect(row(qa, tB).getByRole('button', { name: 'Xong' })).toBeVisible()
          await qa.page.unroute(`**/api/meetings/${live2.id}/agenda/${b}/complete`)
          qa.expect(itemOf(await snapshot(qa, 'chair', live2.id), b).status).toBe('InProgress')
          await qa.shot('complete-503-muc-dang-ban-nut-lai')
          qa.expectStatus(await chair.post(`${A(live2.id)}/${b}/complete`), 200)
          await fail503(`**/api/meetings/${live2.id}/agenda/${a}/current`, 'POST', 'MSG75')
          await press(qa, row(qa, tA).getByRole('button', { name: 'Bắt đầu' }))
          await qa.expect(row(qa, tA).getByText('Đang bàn')).toHaveCount(0)
          await qa.expect(toast(qa, notSaved)).toBeVisible()
          await dismissToasts(qa)
          await qa.expect(row(qa, tA).getByRole('button', { name: 'Bắt đầu' })).toBeVisible()
          await qa.page.unroute(`**/api/meetings/${live2.id}/agenda/${a}/current`)
          qa.expect(itemOf(await snapshot(qa, 'chair', live2.id), a).status).toBe('Pending')
          await settled(qa)
          await qa.shot('current-503-muc-cho-nut-lai')
          await noSidewaysScroll(qa)
          await noRawCodes(qa)
        } finally {
          await transition(qa, 'chair', live2.id, 'end').catch(() => undefined)
        }
      },
    },
    {
      id: 'AC35',
      role: 'secretary',
      title: 'Kéo-thả mục đã xong và sắp xếp từ danh sách cũ (AGENDA_FT24, AGENDA_FT25)',
      steps: [
        'InProgress có một mục Đã xong và hai mục chờ; kéo mục Đã xong xuống cuối',
        'Lấy orderVersion, đổi thứ tự nơi khác (API), PUT order với orderVersion cũ',
      ],
      expected:
        'Kéo-thả: 200 MSG63, mục Đã xong xuống cuối vẫn Done, giờ làm việc giữ nguyên (FT24); orderVersion cũ: 409 MSG143 + toast, danh sách tải lại đúng thứ tự mới (FT25)',
      async run(qa) {
        const m = await freshMeeting(qa, 'InProgress', 'sắp xếp đặc biệt')
        const done = await addItem(qa, 'secretary', m.id, 'Mục đã xong kéo')
        const p1 = await addItem(qa, 'secretary', m.id, 'Mục chờ một')
        const p2 = await addItem(qa, 'secretary', m.id, 'Mục chờ hai')
        const chair = await client(qa, 'chair')
        const sec = await client(qa, 'secretary')
        // Chặn realtime từ đầu: màn hình giữ đúng orderVersion đã đọc cho tới khi re-read qua GET.
        await qa.page.route('**/hubs/**', (route) => route.abort('connectionfailed'))
        try {
          qa.expectStatus(await chair.post(`${A(m.id)}/${done}/current`), 200)
          qa.expectStatus(await chair.post(`${A(m.id)}/${done}/complete`), 200)
          let snap = await snapshot(qa, 'secretary', m.id)
          const staleVersion = snap.orderVersion
          const [tDone, tP1, tP2] = [done, p1, p2].map((id) => itemOf(snap, id).title)
          await openAgenda(qa, m)
          // FT24: drag the Done item below the Pending ones on screen.
          const grip = row(qa, tDone).getByRole('button', { name: `Kéo để đổi vị trí: ${tDone}` })
          await qa.expect(grip).toBeVisible()
          const from = await grip.boundingBox()
          const to = await row(qa, tP2).boundingBox()
          await qa.page.mouse.move(from.x + from.width / 2, from.y + from.height / 2)
          await qa.page.mouse.down()
          await qa.page.mouse.move(from.x + from.width / 2, to.y + to.height * 0.8, { steps: 12 })
          await qa.page.mouse.up()
          await qa.expect(panel(qa).locator('li.agenda-item .agenda-title').last()).toContainText(tDone)
          await settled(qa)
          await qa.shot('keo-muc-da-xong')
          snap = await snapshot(qa, 'secretary', m.id)
          qa.expect(snap.items.at(-1).id).toBe(done)
          qa.expect(itemOf(snap, done).status).toBe('Done')
          qa.expect(itemOf(snap, done).actualEndedAt).toBeTruthy()
          // FT25: reorder from a stale list is refused and the list reloads.
          // staleVersion was read before the drag above, so it is already old here.
          // Mimic the second browser without realtime: block the hub, reorder via API,
          // then drag on screen — the screen sends the old orderVersion and gets 409 + toast.
          await qa.page.route('**/hubs/**', (route) => route.abort('connectionfailed'))
          try {
            const fresh = await snapshot(qa, 'secretary', m.id)
            const all = fresh.items.map((i) => i.id)
            const rotated = [...all.slice(1), all[0]]
            const okRot = await sec.put(`${A(m.id)}/order`, { orderVersion: fresh.orderVersion, orderedItemIds: rotated })
            qa.expectStatus(okRot, 200)
            const dragFailed = qa.page
              .waitForResponse((r) => r.url().endsWith('/agenda/order') && r.request().method() === 'PUT')
              .catch(() => null)
            const grip2 = row(qa, tP1).getByRole('button', { name: `Kéo để đổi vị trí: ${tP1}` })
            const from2 = await grip2.boundingBox()
            const to2 = await row(qa, tP2).boundingBox()
            await qa.page.mouse.move(from2.x + from2.width / 2, from2.y + from2.height / 2)
            await qa.page.mouse.down()
            await qa.page.mouse.move(from2.x + from2.width / 2, to2.y + to2.height * 0.8, { steps: 12 })
            await qa.page.mouse.up()
            const putRes = await dragFailed
            qa.expect(putRes?.status?.(), 'PUT /agenda/order never answered').toBe(409)
            await qa.expect(toast(qa, 'Chương trình nghị sự đã thay đổi. Vui lòng tải lại.')).toBeVisible()
            await qa.expect(panel(qa).locator('li.agenda-item .agenda-title').first()).toContainText(rotated[0] ? itemOf(await snapshot(qa, 'secretary', m.id), rotated[0]).title : '')
            await settled(qa)
            await qa.shot('stale-order-409-reload')
            await noRawCodes(qa)
          } finally {
            await qa.page.unroute('**/hubs/**').catch(() => undefined)
          }
        } finally {
          await transition(qa, 'chair', m.id, 'end').catch(() => undefined)
        }
      },
    },
    {
      id: 'AC36',
      role: 'secretary',
      title: 'Hai người sắp xếp cùng lúc: người sau bị 409 và thấy thứ tự của người trước (AGENDA_FT26)',
      steps: ['Nháp hai mục; thuky@ mở tab Nội dung cuộc họp', 'Đảo thứ tự qua API (như người khác), kéo-thả trên màn hình'],
      expected: 'Kéo-thả trả 409 MSG143 + toast "Chương trình nghị sự đã thay đổi. Vui lòng tải lại."; danh sách tải lại đúng thứ tự của API',
      async run(qa) {
        const m = await freshMeeting(qa, 'Draft', 'sắp xếp hai người')
        const a = await addItem(qa, 'secretary', m.id, 'Mục A hai người')
        const b = await addItem(qa, 'secretary', m.id, 'Mục B hai người')
        const sec = await client(qa, 'secretary')
        // Block the realtime hub: without it the screen keeps the old orderVersion after the API reorder below.
        await qa.page.route('**/hubs/**', (route) => route.abort('connectionfailed'))
        try {
          const snap = await snapshot(qa, 'secretary', m.id)
          const [tA, tB] = [itemOf(snap, a).title, itemOf(snap, b).title]
          await openAgenda(qa, m)
          // Someone else reorders first (the screen still holds the old orderVersion).
          qa.expectStatus(await sec.put(`${A(m.id)}/order`, { orderVersion: snap.orderVersion, orderedItemIds: [b, a] }), 200)
          const failed = qa.page
            .waitForResponse((r) => r.url().endsWith('/agenda/order') && r.request().method() === 'PUT')
            .catch(() => null)
          const grip = row(qa, tA).getByRole('button', { name: `Kéo để đổi vị trí: ${tA}` })
          const from = await grip.boundingBox()
          const to = await row(qa, tB).boundingBox()
          await qa.page.mouse.move(from.x + from.width / 2, from.y + from.height / 2)
          await qa.page.mouse.down()
          await qa.page.mouse.move(from.x + from.width / 2, to.y + to.height * 0.8, { steps: 12 })
          await qa.page.mouse.up()
          const putRes = await failed
          qa.expect(putRes?.status?.(), 'PUT /agenda/order never answered').toBe(409)
          await qa.expect(toast(qa, 'Chương trình nghị sự đã thay đổi. Vui lòng tải lại.')).toBeVisible()
          await qa.expect(panel(qa).locator('li.agenda-item .agenda-title').first()).toContainText(tB, { timeout: 10_000 })
          await settled(qa)
          await qa.shot('hai-nguoi-sap-xep-409')
          await noRawCodes(qa)
          await qa.page.unroute('**/hubs/**')
        } finally {
          await transition(qa, 'chair', m.id, 'end').catch(() => undefined)
        }
      },
    },
    {
      id: 'AC37',
      role: 'member',
      title: 'Mất quyền xem giữa chừng: toàn trang về 404, backend 404 (AGENDA_FT05 — case đòi câu trong tab, sản phẩm 404 toàn trang)',
      steps: ['Thành viên mở tab Nội dung cuộc họp của cuộc họp Đã công bố', 'Chủ toạ gỡ Thành viên khỏi cuộc họp (API)'],
      expected: 'GET agenda 404; realtime đưa toàn trang về "Không tìm thấy trang" (không phải câu trong tab — case cũ, đề nghị BA sửa)',
      async run(qa) {
        const u = await users(qa)
        const { request: playwrightRequest } = await import('@playwright/test')
        const chairCtx = await playwrightRequest.newContext()
        let m = null
        try {
          m = await freshMeeting(qa, 'Published', 'mất quyền xem')
          await openAgenda(qa, m)
          await qa.expect(panel(qa)).toBeVisible()
          const base = new URL(qa.page.url()).origin
          const login = await chairCtx.post(`${base}/api/auth/login`, {
            data: { email: u.chair.email, password: process.env.QA_PASSWORD ?? 'TrustCabinet@2026' },
          })
          qa.expect(login.status(), 'chair login').toBe(200)
          const chairToken = (await login.json()).accessToken
          const chead = { Authorization: `Bearer ${chairToken}` }
          const parts = await chairCtx.get(`${base}/api/meetings/${m.id}/participants?pageSize=100`, { headers: chead })
          const body = await parts.json()
          const memberPart = body.items.find((p) => p.userId === u.member.id)
          const del = await chairCtx.delete(`${base}/api/meetings/${m.id}/participants/${memberPart.id}`, {
            headers: { Authorization: `Bearer ${chairToken}`, 'If-Match': parts.headers()['etag'] ?? `"participants-${body.revision}"` },
          })
          qa.expect(del.status(), `gỡ thành viên: ${await del.text()}`).toBe(200)
          const afterGet = await (await client(qa, 'member')).get(A(m.id))
          qa.note(`GET agenda sau khi gỡ: HTTP ${afterGet.status} ${JSON.stringify(afterGet.body).slice(0, 200)}`)
          qa.expect(afterGet.status).toBe(404)
          // Realtime đưa toàn trang về 404 (route guard theo quyền đọc cuộc họp), không phải câu trong tab.
          await qa.expect(qa.page.getByRole('heading', { name: 'Không tìm thấy trang' })).toBeVisible({ timeout: 20_000 })
          await settled(qa)
          await qa.shot('mat-quyen-xem-404-toan-trang')
          await noRawCodes(qa)
        } finally {
          await chairCtx.dispose().catch(() => undefined)
          await transition(qa, 'chair', m.id, 'end').catch(() => undefined)
        }
      },
    },
    {
      id: 'AC38',
      role: 'member',
      title: 'Đang mở chương trình thì cuộc họp kết thúc: màn hình khoá mà không cần tải lại (AGENDA_FT37)',
      steps: ['thanhvien@ mở tab Nội dung cuộc họp của cuộc họp Đang diễn ra', 'Chủ toạ kết thúc cuộc họp (API)'],
      expected: 'Mục đang bàn thành "Đã xong" ngay trên trang của Thành viên mà không tải lại; nút "Thêm/Bắt đầu/Xong" biến mất',
      async run(qa) {
        const m = await freshMeeting(qa, 'InProgress', 'kết thúc trực tiếp')
        const live = await addItem(qa, 'secretary', m.id, 'Mục kết thúc trực tiếp')
        const chair = await client(qa, 'chair')
        try {
          qa.expectStatus(await chair.post(`${A(m.id)}/${live}/current`), 200)
          const snap = await snapshot(qa, 'member', m.id)
          const title = itemOf(snap, live).title
          await openAgenda(qa, m)
          await qa.expect(row(qa, title).getByText('Đang bàn')).toBeVisible()
          qa.expectStatus(await chair.post(`/api/meetings/${m.id}/transitions`, { action: 'End' }), 200)
          await qa.expect(row(qa, title).getByText('Đã xong')).toBeVisible({ timeout: 15_000 })
          await qa.expect(panel(qa).getByRole('button', { name: 'Thêm nội dung' })).toHaveCount(0)
          await qa.expect(panel(qa).getByRole('button', { name: /^(Bắt đầu|Xong)$/ })).toHaveCount(0)
          let navigations = 0
          qa.page.on('framenavigated', (f) => f === qa.page.mainFrame() && navigations++)
          await qa.page.waitForTimeout(2000)
          qa.expect(navigations, 'the page reloaded').toBe(0)
          await settled(qa)
          await qa.shot('ket-thuc-tu-khoa')
          await noSidewaysScroll(qa)
        } finally {
          await transition(qa, 'chair', m.id, 'end').catch(() => undefined)
        }
      },
    },
    {
      id: 'AC40',
      role: 'secretary',
      title: 'Không có tay nắm kéo với một mục duy nhất; Thành viên cũng không thấy (AGENDA_FT23)',
      steps: ['Cuộc họp Nháp một mục; thuky@ mở tab Nội dung cuộc họp', 'PUT order một mục (API)', 'Member xem Published (AC10 đã phủ)'],
      expected: 'Secretary không thấy nút "Kéo để đổi vị trí"; API 400 MSG143; Member không thấy tay nắm (AC10)',
      async run(qa) {
        const sec = await client(qa, 'secretary')
        const m = await freshMeeting(qa, 'Draft', 'một mục duy nhất')
        const only = await addItem(qa, 'secretary', m.id, 'Mục duy nhất')
        try {
          const snap = await snapshot(qa, 'secretary', m.id)
          const title = itemOf(snap, only).title
          await openAgenda(qa, m)
          await qa.expect(panel(qa).locator('li.agenda-item')).toHaveCount(1)
          await qa.expect(panel(qa).getByRole('button', { name: /Kéo để đổi vị trí/ })).toHaveCount(0)
          qa.note('Nhánh Thành viên: AC10 kiểm Member không thấy tay nắm trên Published nhiều mục.')
          await settled(qa)
          await qa.shot('mot-muc-khong-tay-nam')
          await noSidewaysScroll(qa)
          const c = checker(qa)
          c.check('order một mục', await sec.put(`${A(m.id)}/order`, { orderVersion: snap.orderVersion, orderedItemIds: [only] }), 400, 'MSG143')
          c.done()
          await noRawCodes(qa)
        } finally {
          await cleanup(qa, m.id, [only]).catch(() => undefined)
        }
      },
    },
    {
      id: 'AC41',
      role: 'secretary',
      title: 'Kéo-thả bằng bàn phím: Space nhấc, mũi tên di chuyển, Space thả, Esc huỷ (AGENDA_FT22)',
      steps: ['Cuộc họp Nháp ba mục; focus tay nắm mục 3, Space, ArrowUp, Space', 'Focus tay nắm, Space, Esc'],
      expected: 'Thông báo đọc màn hình nhắc tới mục ("... đang ở vị trí 2 trên 3.", "Đã chuyển ... tới vị trí 2 trên 3."); thứ tự mới lưu (API), tải lại giữ; Esc: "Đã huỷ, ... giữ nguyên vị trí.", thứ tự không đổi; gợi ý "Nhấn phím cách để nhấc nội dung lên..." có trong trang',
      async run(qa) {
        const m = await freshMeeting(qa, 'Draft', 'kéo bằng bàn phím')
        const ids = []
        for (const t of ['Mục bàn phím một', 'Mục bàn phím hai', 'Mục bàn phím ba']) ids.push(await addItem(qa, 'secretary', m.id, t))
        try {
          const snap = await snapshot(qa, 'secretary', m.id)
          const [t1, t2, t3] = ids.map((id) => itemOf(snap, id).title)
          await openAgenda(qa, m)
          const live = qa.page.locator('[aria-live="assertive"]')
          const grip = row(qa, t3).getByRole('button', { name: `Kéo để đổi vị trí: ${t3}` })
          await grip.focus()
          await qa.page.keyboard.press('Space')
          // Wait for the drag to actually start before moving: ArrowUp sent mid-activation is swallowed
          // and the item never leaves position 3 (flaky without this gate).
          await qa.expect(row(qa, t3)).toHaveClass(/agenda-item-dragging/)
          // Thông báo "Đã chọn..." hiện thoáng qua rồi bị ghi đè bởi dragOver: chỉ cần live region nhắc tới mục.
          await qa.expect(live).toContainText(`"${t3}"`, { timeout: 10_000 })
          await qa.page.keyboard.press('ArrowUp')
          await qa.expect(live).toContainText(`"${t3}" đang ở vị trí 2 trên 3.`, { timeout: 10_000 })
          await settled(qa)
          await qa.shot('ban-phim-dang-nhac')
          await qa.page.keyboard.press('Space')
          await qa.expect(live).toContainText(`Đã chuyển "${t3}" tới vị trí 2 trên 3.`, { timeout: 10_000 })
          await qa.expect(panel(qa).locator('li.agenda-item .agenda-title').nth(1)).toContainText(t3)
          // The drop fires an unawaited PUT: poll the server until the new order lands instead of
          // snapshotting mid-flight (a single read right after drop races the request).
          await qa.expect
            .poll(async () => (await snapshot(qa, 'secretary', m.id)).items.map((i) => i.title)[1], { timeout: 15_000 })
            .toBe(t3)
          const after = (await snapshot(qa, 'secretary', m.id)).items.map((i) => i.title)
          await settled(qa)
          await qa.shot('ban-phim-da-tha')
          // Esc huỷ: nhấc rồi huỷ, thứ tự giữ nguyên.
          const grip2 = row(qa, t3).getByRole('button', { name: `Kéo để đổi vị trí: ${t3}` })
          await grip2.focus()
          await qa.page.keyboard.press('Space')
          await qa.expect(row(qa, t3)).toHaveClass(/agenda-item-dragging/)
          // The live region still holds the previous drag's "đang ở vị trí 2 trên 3", so it cannot gate the
          // pick-up; dnd-kit's KeyboardSensor binds keydown in a setTimeout after activation and swallows a
          // key sent before that. Retry Esc (harmless when nothing is lifted) until the cancel is announced.
          await qa.expect(async () => {
            await qa.page.keyboard.press('Escape')
            await qa.expect(live).toContainText(`Đã huỷ, "${t3}" giữ nguyên vị trí.`, { timeout: 1_000 })
          }).toPass({ timeout: 10_000 })
          const kept = (await snapshot(qa, 'secretary', m.id)).items.map((i) => i.title)
          qa.expect(kept).toEqual(after)
          await qa.expect(qa.page.getByText('Nhấn phím cách để nhấc nội dung lên', { exact: false })).toHaveCount(1)
          await settled(qa)
          await qa.shot('ban-phim-huy')
          await noSidewaysScroll(qa)
          await noRawCodes(qa)
        } finally {
          await cleanup(qa, m.id, ids).catch(() => undefined)
        }
      },
    },
  ],
}

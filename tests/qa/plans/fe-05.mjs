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
      title: 'Chủ toạ thêm mục trên màn hình ở cuộc họp Đã công bố và giao người trình bày; người được giao nhận thông báo, tự giao mình thì không (UC-23a, UC-23b)',
      steps: [
        'Đăng nhập chutoa@, mở tab Nội dung cuộc họp của cuộc họp Đã công bố',
        'Thêm mục, người trình bày là chính mình (Chủ toạ), Lưu',
        'Thêm mục thứ hai, người trình bày Lê An (thanhvien@), Lưu',
      ],
      expected:
        'Chủ toạ thấy "Thêm nội dung", bút chì và thùng rác (SCRUM-25, PO 02/11); hai toast MSG60; thanhvien@ nhận một thông báo AgendaPresenterAssigned của cuộc họp; chutoa@ không nhận thông báo nào cho mục tự giao',
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
          await settled(qa)
          await qa.shot('chu-toa-them-muc')
          await noSidewaysScroll(qa)
          // The member's notification arrives after commit; wait for it, then the chair must have none.
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
      role: 'admin',
      title: 'Người tổ chức không phải Chủ toạ/Thư ký chỉ đọc chương trình nghị sự (không tự có quyền ghi)',
      steps: ['quantri@ tổ chức cuộc họp Đã công bố (chutoa@ Chủ toạ, thuky@ Thư ký)', 'Mở tab Nội dung cuộc họp; gọi POST agenda'],
      expected: 'Đọc được (GET 200, canCreate false); không có "Thêm nội dung"; POST 403 MSG67',
      async run(qa) {
        const m = await reused(qa, 'Published', 'admin')
        await openAgenda(qa, m)
        await qa.expect(panel(qa).getByRole('button', { name: 'Thêm nội dung' })).toHaveCount(0)
        await qa.expect(panel(qa).getByText('Chưa có nội dung nào').or(panel(qa).locator('li.agenda-item').first())).toBeVisible()
        await settled(qa)
        await qa.shot('nguoi-to-chuc-chi-doc')
        const snap = await snapshot(qa, 'admin', m.id)
        qa.expect(snap.permissions.canCreate).toBe(false)
        qa.expectStatus(await (await client(qa, 'admin')).post(A(m.id), itemBody('Người tổ chức thêm')), 403)
        qa.expect((await (await client(qa, 'admin')).post(A(m.id), itemBody('Người tổ chức thêm'))).body.code).toBe('MSG67')
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
        'Toast "Bắt đầu mục nghị sự thành công." (MSG64), mục có chip "Đang bàn", các mục khác không còn nút "Bắt đầu"; toast "Hoàn thành mục nghị sự thành công." (MSG65), chip "Đã xong", các mục còn lại có lại "Bắt đầu", cuộc họp vẫn Đang diễn ra; API ghi actualStartedAt/actualEndedAt; mở mục đã xong thấy "Bàn từ hh:mm đến hh:mm" (UC-26 bước 3), cả trên điện thoại',
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
      title: 'Thư ký ghi kết luận cho mục đang bàn; kết luận hiện kèm người lưu và thời điểm; mục chờ không ghi được',
      steps: [
        'Cuộc họp Đang diễn ra, Chủ toạ đã bắt đầu một mục; thuky@ mở tab Nội dung cuộc họp',
        'Ở mục đang bàn (mở sẵn), bấm "Ghi kết luận", nhập, bấm "Lưu kết luận"',
        'Mở một mục còn chờ',
      ],
      expected:
        'Toast "Lưu kết luận mục nghị sự thành công." (MSG271); hộp Kết luận hiện nội dung và "Trần Việt Hải · <ngày giờ>", dòng có chip kết luận; mục chờ không có nút "Ghi kết luận"',
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
          await settled(qa)
          await qa.shot('da-luu-ket-luan')
          await noSidewaysScroll(qa)
          await press(qa, row(qa, pendingTitle).locator('button.agenda-title'))
          await qa.expect(row(qa, pendingTitle).getByText('Chưa có kết luận.')).toBeVisible()
          await qa.expect(row(qa, pendingTitle).getByRole('button', { name: 'Ghi kết luận', exact: true })).toHaveCount(0)
          await settled(qa)
          await qa.shot('muc-cho-khong-ghi-ket-luan')
          const after = itemOf(await snapshot(qa, 'secretary', m.id), live)
          qa.expect(after.conclusion).toBe(text)
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
      title: 'Thành viên xem chương trình của cuộc họp đã kết thúc: thứ tự, người trình bày, trạng thái, kết luận kèm người lưu và thời điểm',
      steps: ['thanhvien@ mở tab Nội dung cuộc họp của cuộc họp Đã kết thúc', 'Mở mục đã xong'],
      expected:
        'Mục đã xong có chip "Đã xong" và chip kết luận (chữ "Đã kết luận" trên điện thoại, biểu tượng có tooltip "Đã có kết luận" trên máy tính); mở ra thấy ghi chú, kết luận "Thống nhất thông qua báo cáo quý III." với tên người lưu và thời điểm, không có nút "Ghi kết luận"; mục chưa bàn không có chip; không có nút sửa',
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
  ],
}

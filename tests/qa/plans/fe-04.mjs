// FE-04 — Quản lý cuộc họp (QA subtask SCRUM-191): UC-13 create, UC-14 update / reschedule, UC-15 detail, UC-16 list,
// UC-17 submit, UC-18 approve / reject, UC-19 publish, UC-20 start, UC-21 end, UC-22 cancel, and the lifecycle rules
// (BR-013, BR-014, BR-016, BR-017, BR-015, BR-030, BR-021, BR-028, UC-22, UC-13).
//
// Criteria come from docs/01-requirements/05-use-case-specifications.md, 06-business-rules.md,
// docs/02-design/01-vong-doi-cuoc-hop.md, docs/03-api/05-quan-ly-cuoc-hop.md and the message catalogue.
//
// Safe on a shared local dev database: every meeting is created here through the API, titled "[QA] …", dated three or
// more days ahead, organised by thuky@ with chutoa@ as Chair and approver, thuky@ as Secretary and thanhvien@ as Member.
// No meeting the plan did not create is ever edited, cancelled or started. The [QA] meetings stay behind (Draft,
// Cancelled, Ended…) and are listed in the report.

const ROLE_LABEL = { admin: 'Quản trị', chair: 'Chủ toạ', secretary: 'Thư ký', member: 'Thành viên' }

// ---------------------------------------------------------------------------------------------------------------------
// Helpers: accounts, API clients, meetings driven to a status through the API.

/** id and full name of each seed account, read once per run from /api/auth/me. */
const USERS = {}
async function users(qa) {
  for (const role of ['admin', 'chair', 'secretary', 'member']) {
    if (USERS[role]) continue
    const me = (await (await client(qa, role)).get('/api/auth/me')).body
    USERS[role] = { id: me.id, name: me.fullName, email: me.email, capabilities: me.capabilities ?? [] }
  }
  return USERS
}

/** One signed-in API client per role and criterion (a sign-in per call would be slow). */
async function client(qa, role) {
  qa.__clients ??= {}
  qa.__clients[role] ??= await qa.api(role)
  return qa.__clients[role]
}

const rand = () => Math.random().toString(36).slice(2, 7)
const iso = (ms) => new Date(ms).toISOString().replace(/\.\d{3}Z$/, 'Z')
let slotCounter = 0
/** A one-hour slot three days ahead (plus a step per meeting), so no two QA meetings share a time. */
function slot() {
  slotCounter += 1
  const hour = 3_600_000
  const start = Math.ceil((Date.now() + 3 * 24 * hour) / hour) * hour + slotCounter * 15 * 60_000
  return { plannedStartTime: iso(start), plannedEndTime: iso(start + hour) }
}
/** YYYY-MM-DD of a day `days` from now in Vietnam, for `?create=<day>`. */
function dayAhead(days) {
  return new Date(Date.now() + days * 86_400_000 + 7 * 3_600_000).toISOString().slice(0, 10)
}

async function roster(qa, { secretary = true } = {}) {
  const u = await users(qa)
  return [
    { userId: u.chair.id, role: 'Chair' },
    ...(secretary ? [{ userId: u.secretary.id, role: 'Secretary' }] : []),
    { userId: u.member.id, role: 'Member' },
  ]
}

/** The create body the plan uses; `over` replaces any field. */
async function meetingBody(qa, title, over = {}) {
  return {
    title: qa.name(title),
    ...slot(),
    mode: 'InPerson',
    location: 'Phòng họp QA',
    onlineLink: null,
    sensitivity: 'Normal',
    description: 'Cuộc họp do bộ kiểm thử QA tạo.',
    participants: await roster(qa),
    ...over,
  }
}

async function transition(qa, role, id, body) {
  return (await client(qa, role)).post(`/api/meetings/${id}/transitions`, body)
}

/**
 * Creates a meeting organised by `organizer` and drives it to `status` through the API, the way the actors would:
 * secretary submits to the chair, chair approves (or rejects), secretary publishes, chair starts and ends. `Cancelled`
 * cancels from `cancelFrom` (default Draft). Returns the meeting as GET returns it to the organizer.
 */
async function meetingAt(qa, status, { title, organizer = 'secretary', over = {}, cancelFrom = 'Draft' } = {}) {
  const u = await users(qa)
  const body = await meetingBody(qa, title ?? `FE-04 ${status} ${rand()}`, over)
  const created = await (await client(qa, organizer)).post('/api/meetings', body)
  qa.expectStatus(created, 201)
  const id = created.body.meeting.id
  const path = {
    Draft: [],
    WaitingApproval: ['submit'],
    Rejected: ['submit', 'reject'],
    Approved: ['submit', 'approve'],
    Published: ['submit', 'approve', 'publish'],
    InProgress: ['submit', 'approve', 'publish', 'start'],
    Ended: ['submit', 'approve', 'publish', 'start', 'end'],
  }
  const steps = status === 'Cancelled' ? [...path[cancelFrom], 'cancel'] : path[status]
  for (const step of steps) {
    const [role, action] = {
      submit: [organizer, { action: 'SubmitForApproval', assignedApproverId: u.chair.id }],
      approve: ['chair', { action: 'Approve' }],
      reject: ['chair', { action: 'Reject', reason: 'QA: cần bổ sung nội dung chương trình.' }],
      publish: [organizer, { action: 'Publish' }],
      start: ['chair', { action: 'Start' }],
      end: ['chair', { action: 'End' }],
      cancel: [organizer, { action: 'Cancel', reason: 'QA: lãnh đạo đi công tác, xếp lịch sau.' }],
    }[step]
    const res = await transition(qa, role, id, action)
    qa.expectStatus(res, 200)
  }
  const got = await (await client(qa, organizer)).get(`/api/meetings/${id}`)
  qa.expectStatus(got, 200)
  qa.expect(got.body.status).toBe(status)
  return got.body
}

/** Runs every API check, then fails once with all the wrong ones, so one bad answer does not hide the next. */
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
  // Endless motion (the pinging call-to-action buttons, ADR-0043) never finishes: wait only for finite transitions.
  await qa.page.waitForFunction(() =>
    document.getAnimations().every((a) => a.playState !== 'running' || a.effect?.getTiming().iterations === Infinity),
  )
}
const toast = (qa, text) => qa.page.locator('.toast-stack').getByText(text)
const dialog = (qa, name) => qa.page.getByRole('dialog', { name })
const head = (qa) => qa.page.locator('.record-title')
const headerActions = (qa) => qa.page.locator('.record-head')

/** Opens a meeting's page and waits for its title. */
async function openMeeting(qa, meeting) {
  await qa.goto(`/mgmt/meetings/${meeting.id}`)
  await qa.expect(qa.page.getByRole('heading', { level: 1, name: meeting.title })).toBeVisible()
}

/** The status chip beside the title shows `label`. */
async function expectStatusChip(qa, label) {
  await qa.expect(head(qa).getByText(label, { exact: true })).toBeVisible()
}

/** Clicks, or taps on a touch screen. */
async function press(qa, locator) {
  if (qa.device === 'phone') await locator.tap()
  else await locator.click()
}

/** Skips a denial criterion when the account holds the capability it is denied (a hand-edited local role). */
async function requireLacks(qa, role, capability) {
  const { roleName, capabilities } = await qa.capabilities(role)
  if (capabilities.includes(capability))
    qa.inconclusive(
      `${role} giữ vai trò "${roleName}" có ${capability} trên database này, nên không phải vai trò bị chặn. Người kiểm: trả tài khoản về vai trò seed rồi chạy lại.`,
    )
}

// ---------------------------------------------------------------------------------------------------------------------

export default {
  ticket: 'FE-04',
  title: 'Quản lý cuộc họp: tạo, sửa, gửi duyệt, duyệt/từ chối, công bố, dời lịch, bắt đầu/kết thúc, huỷ, danh sách và chi tiết',
  devices: ['desktop'],
  criteria: [
    // ----------------------------------------------------------------------------------------------- UC-13 Create
    {
      id: 'AC1',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Thư ký (MeetingCreator) tạo cuộc họp nháp từ màn danh sách, chọn Chủ toạ, Thư ký, Thành viên',
      steps: [
        'Đăng nhập thuky@, mở Danh sách cuộc họp, bấm "Tạo cuộc họp"',
        'Mở form trên ngày cách hôm nay 3 ngày (08:00–09:00), nhập tên',
        'Thêm chutoa@ vai trò Chủ toạ, thuky@ vai trò Thư ký, thanhvien@ (Thành viên)',
        'Bấm "Tạo cuộc họp"',
      ],
      expected:
        'Toast "Tạo cuộc họp thành công." (MSG29), mở trang chi tiết cuộc họp mới, trạng thái Nháp; API: Draft, người tạo là Organizer, 3 thành phần Pending, lịch sử có dòng tạo; không cuộn ngang',
      async run(qa) {
        const u = await users(qa)
        await qa.goto('/mgmt/meetings')
        const create = qa.page.getByRole('link', { name: 'Tạo cuộc họp' }).first()
        await qa.expect(create).toBeVisible()
        await press(qa, create)
        await qa.expect(dialog(qa, 'Tạo cuộc họp')).toBeVisible()
        await settled(qa)
        await qa.shot('form-tao-cuoc-hop')
        // Same dialog, opened on a day three days ahead the way the calendar opens it: 08:00–09:00 prefilled.
        await qa.goto(`/mgmt/meetings?create=${dayAhead(3)}`)
        const form = dialog(qa, 'Tạo cuộc họp')
        await qa.expect(form).toBeVisible()
        const title = qa.name(`FE-04 tạo qua giao diện ${rand()}`)
        await form.getByPlaceholder('Ví dụ: Giao ban tuần 40').fill(title)
        // The form starts with the organizer (thuky@) as Chair; add the Chair-to-be and the member.
        await qa.expect(form.getByRole('button', { name: `Vai trò của ${u.secretary.name}: Chủ toạ` })).toBeVisible()
        const search = form.getByRole('combobox', { name: 'Tìm người dự họp' })
        for (const role of ['chair', 'member']) {
          await search.click()
          await search.fill(u[role].email.split('@')[0])
          const option = qa.page.getByRole('option').filter({ hasText: u[role].email })
          await qa.expect(option).toBeVisible()
          await option.click()
        }
        await form.getByPlaceholder('Ví dụ: Giao ban tuần 40').click() // closes the people menu
        for (const [role, label] of [
          ['chair', 'Chủ toạ'],
          ['secretary', 'Thư ký'],
        ]) {
          await form.getByRole('button', { name: new RegExp(`^Vai trò của ${u[role].name}`) }).click()
          await qa.page.getByRole('listbox', { name: `Vai trò của ${u[role].name}` }).getByRole('option', { name: label }).click()
        }
        await qa.expect(form.getByRole('button', { name: `Vai trò của ${u.chair.name}: Chủ toạ` })).toBeVisible()
        await qa.expect(form.getByRole('button', { name: `Vai trò của ${u.secretary.name}: Thư ký` })).toBeVisible()
        await qa.expect(form.getByRole('button', { name: `Vai trò của ${u.member.name}: Thành viên` })).toBeVisible()
        await settled(qa)
        await qa.shot('form-da-dien', { fullPage: true })
        await form.getByRole('button', { name: 'Tạo cuộc họp' }).click()
        await qa.expect(qa.page).toHaveURL(/\/mgmt\/meetings\/[0-9a-f-]{36}$/)
        await qa.expect(toast(qa, 'Tạo cuộc họp thành công.')).toBeVisible()
        await qa.expect(qa.page.getByRole('heading', { level: 1, name: title })).toBeVisible()
        await expectStatusChip(qa, 'Nháp')
        await settled(qa)
        await qa.shot('chi-tiet-cuoc-hop-moi')
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
        const id = qa.page.url().split('/').pop()
        const sec = await client(qa, 'secretary')
        const m = (await sec.get(`/api/meetings/${id}`)).body
        qa.expect(m.status).toBe('Draft')
        qa.expect(m.createdById).toBe(u.secretary.id)
        qa.expect(m.chair?.userId).toBe(u.chair.id)
        qa.expect(m.secretaries.map((s) => s.userId)).toEqual([u.secretary.id])
        const history = (await sec.get(`/api/meetings/${id}/history`)).body
        qa.expect(history.items.length).toBe(1)
        qa.expect(history.items[0].resultingStatus).toBe('Draft')
        qa.note(`Tạo: ${title} (${id})`)
      },
    },
    {
      // MEET_FT01 E2/E4, as a person does it: one press on "Tạo cuộc họp", no URL typed in between (AC1 opens the form
      // a second time by URL to pick a day, which leaves an extra "?create=" entry in the history).
      id: 'AC1B',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Tạo cuộc họp từ nút trên danh sách, Back không mở lại form trống, tải lại trang chi tiết vẫn là cuộc họp đó',
      steps: [
        'Mở Danh sách cuộc họp, bấm "Tạo cuộc họp"',
        'Nhập tên (giờ điền sẵn), bấm "Tạo cuộc họp"',
        'Bấm Back của trình duyệt, rồi Forward và tải lại trang',
      ],
      expected:
        'Mở trang chi tiết cuộc họp mới; Back về danh sách và không có hộp "Tạo cuộc họp"; Forward và tải lại vẫn là trang chi tiết đó, trạng thái Nháp',
      async run(qa) {
        await qa.goto('/mgmt/meetings')
        await press(qa, qa.page.getByRole('link', { name: 'Tạo cuộc họp' }).first())
        const form = dialog(qa, 'Tạo cuộc họp')
        await qa.expect(form).toBeVisible()
        // From 22:00 the form starts with no time (createMeetingPrefill), and this criterion types no time.
        if ((await qa.page.evaluate(() => new Date().getHours())) >= 22)
          qa.inconclusive('Sau 22:00 form không điền sẵn giờ; chạy lại trong ngày')
        const title = qa.name(`FE-04 tạo rồi Back ${rand()}`)
        await form.getByPlaceholder('Ví dụ: Giao ban tuần 40').fill(title)
        await form.getByRole('button', { name: 'Tạo cuộc họp' }).click()
        await qa.expect(qa.page).toHaveURL(/\/mgmt\/meetings\/[0-9a-f-]{36}$/)
        await qa.expect(qa.page.getByRole('heading', { level: 1, name: title })).toBeVisible()
        const detail = qa.page.url()
        await qa.page.goBack()
        await qa.expect(qa.page).toHaveURL(/\/mgmt\/meetings$/)
        await qa.expect(qa.page.getByRole('heading', { level: 1, name: 'Danh sách cuộc họp' })).toBeVisible()
        await qa.page.waitForTimeout(500)
        await qa.expect(dialog(qa, 'Tạo cuộc họp')).toHaveCount(0)
        await settled(qa)
        await qa.shot('back-ve-danh-sach-khong-mo-form')
        await qa.page.goForward()
        await qa.expect(qa.page).toHaveURL(detail)
        await qa.page.reload()
        await qa.expect(qa.page.getByRole('heading', { level: 1, name: title })).toBeVisible()
        await expectStatusChip(qa, 'Nháp')
        await qa.expect(dialog(qa, 'Tạo cuộc họp')).toHaveCount(0)
        qa.note(`Tạo: ${title} (${detail.split('/').pop()})`)
      },
    },
    {
      id: 'AC2',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Form tạo cuộc họp báo lỗi theo ô khi thiếu tên và chưa chọn người dự họp; không tạo gì',
      steps: ['Mở form tạo cuộc họp', 'Xoá trống tên, không chọn ai', 'Bấm "Tạo cuộc họp"'],
      expected:
        'Ô Tên báo "Tên cuộc họp không được để trống.", ô Người dự họp báo lỗi; form vẫn mở, không có cuộc họp mới (UC-13 EX-1, EX-3)',
      async run(qa) {
        const before = (await (await client(qa, 'secretary')).get('/api/meetings?status=Draft&pageSize=1')).body.totalCount
        await qa.goto(`/mgmt/meetings?create=${dayAhead(3)}`)
        const form = dialog(qa, 'Tạo cuộc họp')
        await qa.expect(form).toBeVisible()
        // The form starts with the organizer picked; take them off so nobody is chosen.
        const u = await users(qa)
        await form.getByRole('button', { name: `Bỏ ${u.secretary.name}` }).click()
        await qa.expect(form.getByText('Chưa chọn ai.')).toBeVisible()
        await form.getByRole('button', { name: 'Tạo cuộc họp' }).click()
        await qa.expect(form.getByText('Tên cuộc họp không được để trống.')).toBeVisible()
        await qa.expect(form.getByPlaceholder('Ví dụ: Giao ban tuần 40')).toHaveAttribute('aria-invalid', 'true')
        await qa.expect(form.getByRole('combobox', { name: 'Tìm người dự họp' })).toHaveAttribute('aria-invalid', 'true')
        await settled(qa)
        await qa.shot('loi-thieu-ten-thanh-phan', { fullPage: true })
        await qa.expect(form).toBeVisible()
        const after = (await (await client(qa, 'secretary')).get('/api/meetings?status=Draft&pageSize=1')).body.totalCount
        qa.expect(after).toBe(before)
      },
    },
    {
      id: 'AC3',
      role: 'secretary',
      title: 'API tạo cuộc họp: kiểm tra thông tin và danh sách thành phần (MSG31, MSG277), tạo đúng thì 201 MSG29',
      steps: [
        'POST /api/meetings: tên trống; giờ bắt đầu đã qua; kết thúc trước bắt đầu',
        'POST: không có ai; hai Chủ toạ; không có Chủ toạ; một người hai lần; người không tồn tại',
        'POST hợp lệ',
      ],
      expected:
        '400 MSG31 với errors.title / plannedStartTime / plannedEndTime; 400 MSG277 cho từng lỗi thành phần; hợp lệ: 201 MSG29, Location, status Draft',
      async run(qa) {
        const u = await users(qa)
        const sec = await client(qa, 'secretary')
        const c = checker(qa)
        const now = Date.now()
        const cases = [
          ['tên trống', { title: '   ' }, 'MSG31', 'title'],
          ['giờ bắt đầu đã qua', { plannedStartTime: iso(now - 3_600_000), plannedEndTime: iso(now + 3_600_000) }, 'MSG31', 'plannedStartTime'],
          ['kết thúc trước bắt đầu', { plannedStartTime: iso(now + 4 * 86_400_000), plannedEndTime: iso(now + 4 * 86_400_000 - 60_000) }, 'MSG31', 'plannedEndTime'],
          ['không có ai', { participants: [] }, 'MSG277'],
          ['hai Chủ toạ', { participants: [{ userId: u.chair.id, role: 'Chair' }, { userId: u.member.id, role: 'Chair' }] }, 'MSG277'],
          ['không có Chủ toạ', { participants: [{ userId: u.member.id, role: 'Member' }] }, 'MSG277'],
          ['một người hai lần', { participants: [{ userId: u.chair.id, role: 'Chair' }, { userId: u.chair.id, role: 'Member' }] }, 'MSG277'],
          ['người không tồn tại', { participants: [{ userId: '0199ffff-ffff-7fff-bfff-ffffffffffff', role: 'Chair' }] }, 'MSG277'],
        ]
        for (const [label, over, code, field] of cases) {
          const res = await sec.post('/api/meetings', await meetingBody(qa, `FE-04 không được tạo ${label}`, over))
          if (c.check(label, res, 400, code) && field)
            c.fact(`${label}: errors.${field}`, Object.keys(res.body.errors ?? {}).some((k) => k.toLowerCase() === field.toLowerCase()), JSON.stringify(res.body.errors))
          if (res.status === 201) qa.note(`Đã tạo nhầm "${label}": ${res.body.meeting.id}`)
        }
        const ok = await sec.post('/api/meetings', await meetingBody(qa, `FE-04 tạo qua API ${rand()}`))
        if (c.check('hợp lệ', ok, 201, 'MSG29')) {
          c.fact('message', ok.body.message === 'Tạo cuộc họp thành công.', ok.body.message)
          c.fact('Location', (ok.headers.location ?? '').endsWith(`/api/meetings/${ok.body.meeting.id}`), ok.headers.location)
          c.fact('status Draft', ok.body.meeting.status === 'Draft', ok.body.meeting.status)
        }
        c.done()
      },
    },
    {
      id: 'AC4',
      role: 'member',
      devices: ['desktop', 'phone'],
      title: 'Thành viên (không có MeetingCreator) không tạo được cuộc họp: không có nút, ?create không mở form, API 403',
      steps: ['Đăng nhập thanhvien@', 'Mở Danh sách cuộc họp', 'Mở /mgmt/meetings?create=1', 'POST /api/meetings'],
      expected: 'Không có nút "Tạo cuộc họp", không có form; POST 403',
      async run(qa) {
        await requireLacks(qa, 'member', 'MeetingCreator')
        await qa.goto(`/mgmt/meetings?create=${dayAhead(3)}`)
        await qa.expect(qa.page.getByRole('heading', { name: 'Danh sách cuộc họp' })).toBeVisible()
        await qa.expect(qa.page.getByRole('link', { name: 'Tạo cuộc họp' })).toHaveCount(0)
        await qa.expect(dialog(qa, 'Tạo cuộc họp')).toHaveCount(0)
        await qa.shot('khong-co-nut-tao')
        await noSidewaysScroll(qa)
        if (qa.device !== 'desktop') return
        const res = await (await client(qa, 'member')).post('/api/meetings', await meetingBody(qa, 'FE-04 thành viên tạo'))
        qa.expectStatus(res, 403)
      },
    },

    // ----------------------------------------------------------------------------------------------- UC-14 Update
    {
      id: 'AC5',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Người tổ chức sửa cuộc họp Nháp; lưu mà không đổi gì thì báo "Không có thay đổi cần lưu."',
      steps: ['Mở cuộc họp Nháp do mình tạo', 'Bấm "Chỉnh sửa", bấm Lưu ngay', 'Đổi tên, bấm "Lưu thay đổi"'],
      expected:
        'Lưu không đổi: "Không có thay đổi cần lưu." (MSG154), không gọi PUT. Đổi tên: toast "Cập nhật cuộc họp thành công." (MSG30), tên mới trên trang, vẫn Nháp',
      async run(qa) {
        const m = await meetingAt(qa, 'Draft')
        await openMeeting(qa, m)
        await press(qa, headerActions(qa).getByRole('link', { name: 'Chỉnh sửa' }))
        const form = dialog(qa, 'Chỉnh sửa cuộc họp')
        await qa.expect(form).toBeVisible()
        let puts = 0
        qa.page.on('request', (r) => r.method() === 'PUT' && r.url().includes(`/api/meetings/${m.id}`) && puts++)
        await form.getByRole('button', { name: 'Lưu thay đổi' }).click()
        await qa.expect(form.getByText('Không có thay đổi cần lưu.')).toBeVisible()
        qa.expect(puts, 'an unchanged save called PUT').toBe(0)
        await settled(qa)
        await qa.shot('khong-co-thay-doi')
        const renamed = `${m.title} (đã sửa)`
        await form.getByPlaceholder('Ví dụ: Giao ban tuần 40').fill(renamed)
        await form.getByRole('button', { name: 'Lưu thay đổi' }).click()
        await qa.expect(toast(qa, 'Cập nhật cuộc họp thành công.')).toBeVisible()
        await qa.expect(qa.page.getByRole('heading', { level: 1, name: renamed })).toBeVisible()
        await expectStatusChip(qa, 'Nháp')
        await settled(qa)
        await qa.shot('da-luu-ten-moi')
        await noSidewaysScroll(qa)
        const got = (await (await client(qa, 'secretary')).get(`/api/meetings/${m.id}`)).body
        qa.expect(got.title).toBe(renamed)
        qa.expect(got.status).toBe('Draft')
      },
    },
    {
      id: 'AC6',
      role: 'secretary',
      title: 'API sửa cuộc họp: chỉ Draft/Rejected sửa được, Published chỉ dời lịch; trạng thái khác 409 MSG33; người khác bị chặn',
      steps: [
        'PUT cuộc họp Draft và Rejected (đổi mô tả)',
        'PUT cuộc họp WaitingApproval, Approved, InProgress, Ended, Cancelled',
        'PUT bằng chutoa@ (có MeetingCreator nhưng không phải người tạo), thanhvien@ (không có MeetingCreator)',
      ],
      expected:
        'Draft/Rejected: 200 MSG30, trạng thái giữ nguyên; trạng thái khác: 409 MSG33 và không đổi gì; chutoa@: 404 (không lộ quyền sở hữu); thanhvien@: 403',
      async run(qa) {
        const sec = await client(qa, 'secretary')
        const c = checker(qa)
        const put = (m, over = {}) => {
          const { title, plannedStartTime, plannedEndTime, mode, location, onlineLink, sensitivity, description } = m
          return { title, plannedStartTime, plannedEndTime, mode, location, onlineLink, sensitivity, description, ...over }
        }
        for (const status of ['Draft', 'Rejected']) {
          const m = await meetingAt(qa, status)
          const res = await sec.put(`/api/meetings/${m.id}`, put(m, { description: 'QA đã sửa mô tả.' }))
          if (c.check(`PUT ${status}`, res, 200, 'MSG30')) c.fact(`${status} giữ trạng thái`, res.body.meeting.status === status, res.body.meeting.status)
        }
        for (const status of ['WaitingApproval', 'Approved', 'InProgress', 'Ended', 'Cancelled']) {
          const m = await meetingAt(qa, status)
          const res = await sec.put(`/api/meetings/${m.id}`, put(m, { description: 'QA không được sửa.' }))
          c.check(`PUT ${status}`, res, 409, 'MSG33')
          const after = (await sec.get(`/api/meetings/${m.id}`)).body
          c.fact(`${status} không đổi`, after.description === m.description && after.status === status)
        }
        const draft = await meetingAt(qa, 'Draft')
        c.check('PUT bởi chutoa@ (không phải người tạo)', await (await client(qa, 'chair')).put(`/api/meetings/${draft.id}`, put(draft, { description: 'x' })), 404)
        c.check('PUT bởi thanhvien@', await (await client(qa, 'member')).put(`/api/meetings/${draft.id}`, put(draft, { description: 'x' })), 403)
        c.done()
      },
    },
    {
      id: 'AC7',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Trạng thái không cho sửa thì không có nút: Chờ duyệt, Đã duyệt, Đang diễn ra, Đã kết thúc, Đã huỷ; ?edit=1 không mở form',
      steps: ['Người tổ chức mở cuộc họp ở từng trạng thái trên', 'Mở thêm đường dẫn ?edit=1 của cuộc họp Đã huỷ'],
      expected:
        'Không có "Chỉnh sửa" hay "Dời lịch" ở cả năm trạng thái; "Huỷ cuộc họp" chỉ có ở Chờ duyệt, Đã duyệt; Đã huỷ chỉ đọc: thanh "Cuộc họp đã huỷ" với lý do, ?edit=1 không mở form',
      async run(qa) {
        for (const [status, label] of [
          ['WaitingApproval', 'Chờ duyệt'],
          ['Approved', 'Đã duyệt'],
          ['InProgress', 'Đang diễn ra'],
          ['Ended', 'Đã kết thúc'],
          ['Cancelled', 'Đã huỷ'],
        ]) {
          const m = await meetingAt(qa, status)
          await openMeeting(qa, m)
          await expectStatusChip(qa, label)
          const actions = headerActions(qa)
          await qa.expect(actions.getByRole('link', { name: 'Chỉnh sửa' })).toHaveCount(0)
          await qa.expect(actions.getByRole('link', { name: 'Dời lịch' })).toHaveCount(0)
          const cancellable = ['WaitingApproval', 'Approved'].includes(status)
          await qa.expect(actions.getByRole('button', { name: 'Huỷ cuộc họp' })).toHaveCount(cancellable ? 1 : 0)
          if (status === 'Cancelled') {
            await qa.expect(qa.page.getByRole('heading', { name: 'Cuộc họp đã huỷ' })).toBeVisible()
            await qa.expect(qa.page.getByRole('region', { name: 'Cuộc họp đã huỷ' }).getByText('QA: lãnh đạo đi công tác, xếp lịch sau.')).toBeVisible()
            await qa.goto(`/mgmt/meetings/${m.id}?edit=1`)
            await qa.expect(qa.page.getByRole('heading', { level: 1, name: m.title })).toBeVisible()
            await qa.expect(qa.page.getByRole('dialog')).toHaveCount(0)
          }
          await settled(qa)
          await qa.shot(`khong-sua-${status}`)
          await noSidewaysScroll(qa)
        }
      },
    },

    // ----------------------------------------------------------------------------------------------- UC-17 Submit
    {
      id: 'AC8',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Người tổ chức gửi duyệt cuộc họp Nháp cho một người duyệt chọn từ danh sách',
      steps: ['Mở cuộc họp Nháp', 'Thanh trạng thái "Bản nháp — chưa gửi duyệt", bấm "Gửi duyệt"', 'Chọn chutoa@, bấm "Gửi duyệt"'],
      expected:
        'Toast "Đã gửi cuộc họp để duyệt." (MSG78), trạng thái Chờ duyệt, thanh "Đang chờ <tên> duyệt", không còn nút Chỉnh sửa; lịch sử có dòng gửi duyệt',
      async run(qa) {
        const u = await users(qa)
        const m = await meetingAt(qa, 'Draft')
        await openMeeting(qa, m)
        await qa.expect(qa.page.getByRole('heading', { name: 'Bản nháp — chưa gửi duyệt' })).toBeVisible()
        const send = qa.page.getByRole('button', { name: 'Gửi duyệt', exact: true })
        await qa.expect(send).toBeEnabled()
        await press(qa, send)
        const form = dialog(qa, 'Gửi duyệt cuộc họp')
        await qa.expect(form).toBeVisible()
        // Submitting without a pick is refused in the dialog.
        await form.getByRole('button', { name: 'Gửi duyệt' }).click()
        await qa.expect(form.getByText('Vui lòng chọn người duyệt.')).toBeVisible()
        await form.getByRole('button', { name: 'Chọn người duyệt' }).click()
        await qa.page.getByRole('option', { name: new RegExp(u.chair.name) }).click()
        await settled(qa)
        await qa.shot('chon-nguoi-duyet')
        await form.getByRole('button', { name: 'Gửi duyệt' }).click()
        await qa.expect(toast(qa, 'Đã gửi cuộc họp để duyệt.')).toBeVisible()
        await expectStatusChip(qa, 'Chờ duyệt')
        await qa.expect(qa.page.getByRole('heading', { name: `Đang chờ ${u.chair.name} duyệt` })).toBeVisible()
        await qa.expect(headerActions(qa).getByRole('link', { name: 'Chỉnh sửa' })).toHaveCount(0)
        await settled(qa)
        await qa.shot('da-gui-duyet', { fullPage: true })
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
        const got = (await (await client(qa, 'secretary')).get(`/api/meetings/${m.id}`)).body
        qa.expect(got.status).toBe('WaitingApproval')
        qa.expect(got.assignedApproverId).toBe(u.chair.id)
      },
    },
    {
      id: 'AC9',
      role: 'secretary',
      title: 'API gửi duyệt: không tự duyệt, người duyệt phải có quyền, sai trạng thái 409 MSG41, người khác bị chặn',
      steps: [
        'GET /api/meetings/approvers bằng thuky@ và chutoa@',
        'chutoa@ tạo cuộc họp rồi gửi duyệt cho chính mình',
        'thuky@ gửi duyệt cho thanhvien@ (không có MeetingApprover)',
        'Gửi duyệt cuộc họp đang Chờ duyệt, Đã duyệt, Đã huỷ',
        'chutoa@ (Chủ toạ, đọc được) và thanhvien@ (chưa đọc được Nháp) gửi duyệt cuộc họp của thuky@',
      ],
      expected:
        'Danh sách người duyệt chỉ có tài khoản có MeetingApprover, không có người gọi; tự duyệt và người không có quyền: 400 MSG82, vẫn Nháp; sai trạng thái: 409 MSG41; chutoa@ 403 MSG85; thanhvien@ 404',
      async run(qa) {
        const u = await users(qa)
        const c = checker(qa)
        const listSec = await (await client(qa, 'secretary')).get('/api/meetings/approvers')
        if (c.check('approvers (thuky@)', listSec, 200)) {
          const ids = listSec.body.map?.((a) => a.id) ?? listSec.body.items?.map((a) => a.id) ?? []
          c.fact('approvers có chutoa@', ids.includes(u.chair.id))
          c.fact('approvers không có thanhvien@', !ids.includes(u.member.id))
          c.fact('approvers không có thuky@', !ids.includes(u.secretary.id))
        }
        const listChair = await (await client(qa, 'chair')).get('/api/meetings/approvers')
        if (c.check('approvers (chutoa@)', listChair, 200)) {
          const ids = listChair.body.map?.((a) => a.id) ?? listChair.body.items?.map((a) => a.id) ?? []
          c.fact('approvers không có chính người gọi', !ids.includes(u.chair.id))
        }
        const own = await meetingAt(qa, 'Draft', { organizer: 'chair' })
        c.check('tự duyệt', await transition(qa, 'chair', own.id, { action: 'SubmitForApproval', assignedApproverId: u.chair.id }), 400, 'MSG82')
        const draft = await meetingAt(qa, 'Draft')
        c.check('người duyệt không có quyền', await transition(qa, 'secretary', draft.id, { action: 'SubmitForApproval', assignedApproverId: u.member.id }), 400, 'MSG82')
        c.fact('vẫn Nháp', (await (await client(qa, 'secretary')).get(`/api/meetings/${draft.id}`)).body.status === 'Draft')
        c.check('chutoa@ gửi duyệt thay', await transition(qa, 'chair', draft.id, { action: 'SubmitForApproval', assignedApproverId: u.admin.id }), 403, 'MSG85')
        c.check('thanhvien@ gửi duyệt', await transition(qa, 'member', draft.id, { action: 'SubmitForApproval', assignedApproverId: u.chair.id }), 404)
        for (const status of ['WaitingApproval', 'Approved', 'Cancelled']) {
          const m = await meetingAt(qa, status)
          c.check(`gửi duyệt khi ${status}`, await transition(qa, 'secretary', m.id, { action: 'SubmitForApproval', assignedApproverId: u.chair.id }), 409, 'MSG41')
        }
        c.done()
      },
    },

    // ----------------------------------------------------------------------------------------------- UC-18 Review
    {
      id: 'AC10',
      role: 'chair',
      devices: ['desktop', 'phone'],
      title: 'Người duyệt được phân công duyệt cuộc họp đang chờ',
      steps: ['Đăng nhập chutoa@, mở cuộc họp đang chờ mình duyệt', 'Bấm "Duyệt", xác nhận "Duyệt"'],
      expected:
        'Thanh "Cuộc họp đang chờ bạn duyệt" có nút Duyệt và Từ chối; sau khi duyệt: toast "Đã duyệt cuộc họp." (MSG43), trạng thái Đã duyệt, thanh "Bạn đã duyệt cuộc họp này"; người tổ chức có thông báo MeetingApproved',
      async run(qa) {
        const m = await meetingAt(qa, 'WaitingApproval')
        await openMeeting(qa, m)
        await qa.expect(qa.page.getByRole('heading', { name: 'Cuộc họp đang chờ bạn duyệt' })).toBeVisible()
        await qa.expect(qa.page.getByRole('button', { name: 'Từ chối' })).toBeVisible()
        await settled(qa)
        await qa.shot('cho-ban-duyet')
        await press(qa, qa.page.getByRole('button', { name: 'Duyệt', exact: true }))
        const confirm = dialog(qa, 'Duyệt cuộc họp')
        await qa.expect(confirm).toBeVisible()
        await confirm.getByRole('button', { name: 'Duyệt', exact: true }).click()
        await qa.expect(toast(qa, 'Đã duyệt cuộc họp.')).toBeVisible()
        await expectStatusChip(qa, 'Đã duyệt')
        await qa.expect(qa.page.getByRole('heading', { name: 'Bạn đã duyệt cuộc họp này' })).toBeVisible()
        await settled(qa)
        await qa.shot('da-duyet')
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
        const notes = (await (await client(qa, 'secretary')).get('/api/notifications?page=1&pageSize=50')).body.items
        qa.expect(notes.some((n) => n.meetingId === m.id && n.type === 'MeetingApproved'), 'organizer has no MeetingApproved notification').toBe(true)
      },
    },
    {
      id: 'AC11',
      role: 'chair',
      devices: ['desktop', 'phone'],
      title: 'Người duyệt từ chối phải nêu lý do; người tổ chức thấy lý do và gửi duyệt lại cho người duyệt cũ được chọn sẵn',
      steps: [
        'chutoa@ mở cuộc họp chờ duyệt, bấm "Từ chối", bấm "Từ chối" khi lý do trống',
        'Nhập lý do, bấm "Từ chối"',
        'thuky@ mở cuộc họp: đọc lý do, bấm "Gửi duyệt lại"',
      ],
      expected:
        'Lý do trống: "Cần nhập lý do từ chối." (MSG83), vẫn Chờ duyệt. Có lý do: toast "Đã từ chối cuộc họp." (MSG44), Bị từ chối. Người tổ chức: thanh "Cuộc họp bị từ chối" kèm lý do, có "Chỉnh sửa"; dialog gửi lại chọn sẵn chutoa@; gửi lại thành Chờ duyệt',
      async run(qa) {
        const u = await users(qa)
        const m = await meetingAt(qa, 'WaitingApproval')
        const reason = `QA từ chối ${rand()}: thiếu báo cáo tài chính.`
        await openMeeting(qa, m)
        await press(qa, qa.page.getByRole('button', { name: 'Từ chối' }))
        const form = dialog(qa, 'Từ chối cuộc họp')
        await qa.expect(form).toBeVisible()
        await form.getByRole('button', { name: 'Từ chối' }).click()
        await qa.expect(form.getByText('Cần nhập lý do từ chối.')).toBeVisible()
        await settled(qa)
        await qa.shot('ly-do-trong')
        await form.getByRole('textbox').fill(reason)
        await form.getByRole('button', { name: 'Từ chối' }).click()
        await qa.expect(toast(qa, 'Đã từ chối cuộc họp.')).toBeVisible()
        await expectStatusChip(qa, 'Bị từ chối')
        await qa.expect(qa.page.getByRole('region', { name: 'Bạn đã từ chối cuộc họp này' }).getByText(reason)).toBeVisible()
        await settled(qa)
        await qa.shot('da-tu-choi')
        await noSidewaysScroll(qa)

        await qa.signIn('secretary')
        await openMeeting(qa, m)
        await qa.expect(qa.page.getByRole('heading', { name: 'Cuộc họp bị từ chối' })).toBeVisible()
        await qa.expect(qa.page.getByRole('region', { name: 'Cuộc họp bị từ chối' }).getByText(reason)).toBeVisible()
        await qa.expect(headerActions(qa).getByRole('link', { name: 'Chỉnh sửa' })).toBeVisible()
        await settled(qa)
        await qa.shot('nguoi-to-chuc-thay-ly-do', { fullPage: true })
        await press(qa, qa.page.getByRole('button', { name: 'Gửi duyệt lại' }))
        const resend = dialog(qa, 'Gửi duyệt lại cuộc họp')
        await qa.expect(resend).toBeVisible()
        await qa.expect(resend.getByRole('button', { name: `Chọn người duyệt: ${u.chair.name}` })).toBeVisible()
        await settled(qa)
        await qa.shot('gui-lai-chon-san')
        await resend.getByRole('button', { name: 'Gửi duyệt lại' }).click()
        await qa.expect(toast(qa, 'Đã gửi cuộc họp để duyệt.')).toBeVisible()
        await expectStatusChip(qa, 'Chờ duyệt')
        await settled(qa)
        await qa.shot('da-gui-lai')
      },
    },
    {
      id: 'AC12',
      role: 'chair',
      title: 'API duyệt/từ chối: lý do 1–1.000 ký tự, chỉ người duyệt được phân công, không tự duyệt, sai trạng thái 409',
      steps: [
        'Reject lý do chỉ khoảng trắng; lý do 1.001 ký tự',
        'Approve bởi người tổ chức thuky@, bởi quantri@ (có MeetingApprover nhưng không được phân công), bởi thanhvien@',
        'Approve/Reject cuộc họp Nháp, Đã duyệt, Đã huỷ',
      ],
      expected:
        'MSG83 / MSG84 (400), vẫn Chờ duyệt; thuky@ 403 MSG85; quantri@, thanhvien@ 404 (không đọc được); sai trạng thái 409 MSG132',
      async run(qa) {
        const c = checker(qa)
        const m = await meetingAt(qa, 'WaitingApproval')
        c.check('lý do khoảng trắng', await transition(qa, 'chair', m.id, { action: 'Reject', reason: '   ' }), 400, 'MSG83')
        c.check('lý do 1.001 ký tự', await transition(qa, 'chair', m.id, { action: 'Reject', reason: 'x'.repeat(1001) }), 400, 'MSG84')
        c.check('người tổ chức tự duyệt', await transition(qa, 'secretary', m.id, { action: 'Approve' }), 403, 'MSG85')
        c.check('quantri@ không được phân công', await transition(qa, 'admin', m.id, { action: 'Approve' }), 404)
        c.check('thanhvien@', await transition(qa, 'member', m.id, { action: 'Approve' }), 404)
        c.fact('vẫn Chờ duyệt', (await (await client(qa, 'chair')).get(`/api/meetings/${m.id}`)).body.status === 'WaitingApproval')
        const long = await transition(qa, 'chair', m.id, { action: 'Reject', reason: `  ${'y'.repeat(1000)}  ` })
        c.check('lý do đúng 1.000 ký tự sau trim', long, 200, 'MSG44')
        for (const status of ['Approved', 'Cancelled']) {
          const x = await meetingAt(qa, status, { cancelFrom: 'WaitingApproval' })
          c.check(`Approve khi ${status}`, await transition(qa, 'chair', x.id, { action: 'Approve' }), 409, 'MSG132')
          c.check(`Reject khi ${status}`, await transition(qa, 'chair', x.id, { action: 'Reject', reason: 'QA' }), 409, 'MSG132')
        }
        c.done()
      },
    },

    // ----------------------------------------------------------------------------------------------- UC-19 Publish
    {
      id: 'AC13',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Người tổ chức công bố cuộc họp đã duyệt; thành phần tham dự nhận thông báo',
      steps: ['Mở cuộc họp Đã duyệt', 'Thanh "Cuộc họp đã được duyệt", bấm "Công bố", xác nhận'],
      expected:
        'Toast "Đã công bố cuộc họp." (MSG45), trạng thái Đã công bố, thanh duyệt biến mất, nút "Chỉnh sửa" thành "Dời lịch"; thanhvien@ có thông báo "Cuộc họp đã được công bố" (MeetingInvited), người công bố không có',
      async run(qa) {
        const m = await meetingAt(qa, 'Approved')
        await openMeeting(qa, m)
        await qa.expect(qa.page.getByRole('heading', { name: 'Cuộc họp đã được duyệt' })).toBeVisible()
        await settled(qa)
        await qa.shot('da-duyet-cho-cong-bo')
        await press(qa, qa.page.getByRole('button', { name: 'Công bố', exact: true }))
        const confirm = dialog(qa, 'Công bố cuộc họp')
        await qa.expect(confirm).toBeVisible()
        await confirm.getByRole('button', { name: 'Công bố', exact: true }).click()
        await qa.expect(toast(qa, 'Đã công bố cuộc họp.')).toBeVisible()
        await expectStatusChip(qa, 'Đã công bố')
        await qa.expect(qa.page.getByRole('heading', { name: 'Cuộc họp đã được duyệt' })).toHaveCount(0)
        await qa.expect(headerActions(qa).getByRole('link', { name: 'Dời lịch' })).toBeVisible()
        await qa.expect(headerActions(qa).getByRole('link', { name: 'Chỉnh sửa' })).toHaveCount(0)
        await settled(qa)
        await qa.shot('da-cong-bo')
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
        const memberNotes = (await (await client(qa, 'member')).get('/api/notifications?page=1&pageSize=50')).body.items
        qa.expect(memberNotes.some((n) => n.meetingId === m.id && n.type === 'MeetingInvited'), 'member has no publish notification (MeetingInvited)').toBe(true)
        const ownNotes = (await (await client(qa, 'secretary')).get('/api/notifications?page=1&pageSize=50')).body.items
        qa.expect(ownNotes.some((n) => n.meetingId === m.id && n.type === 'MeetingInvited'), 'publisher notified of own publish').toBe(false)
      },
    },
    {
      id: 'AC14',
      role: 'secretary',
      title: 'API công bố: cần đúng một Chủ toạ và ít nhất một Thư ký; chỉ người tổ chức; chỉ từ Đã duyệt',
      steps: [
        'Cuộc họp Đã duyệt không có Thư ký: Publish',
        'chutoa@ (Chủ toạ, người duyệt) Publish cuộc họp của thuky@',
        'Publish cuộc họp Nháp, Đã công bố, Đã huỷ',
      ],
      expected: 'Thiếu Thư ký: 409 MSG137, vẫn Đã duyệt; chutoa@: 403 MSG85; sai trạng thái: 409 MSG46',
      async run(qa) {
        const c = checker(qa)
        const noSec = await meetingAt(qa, 'Approved', { over: { participants: await roster(qa, { secretary: false }) } })
        c.check('không có Thư ký', await transition(qa, 'secretary', noSec.id, { action: 'Publish' }), 409, 'MSG137')
        c.fact('vẫn Đã duyệt', (await (await client(qa, 'secretary')).get(`/api/meetings/${noSec.id}`)).body.status === 'Approved')
        const ok = await meetingAt(qa, 'Approved')
        c.check('chutoa@ công bố thay', await transition(qa, 'chair', ok.id, { action: 'Publish' }), 403, 'MSG85')
        for (const status of ['Draft', 'Published', 'Cancelled']) {
          const m = await meetingAt(qa, status)
          c.check(`Publish khi ${status}`, await transition(qa, 'secretary', m.id, { action: 'Publish' }), 409, 'MSG46')
        }
        c.done()
      },
    },

    // ----------------------------------------------------------------------------------------------- UC-14 AF-1 Reschedule
    {
      id: 'AC15',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Cuộc họp đã công bố chỉ dời được giờ và địa điểm; lưu thì báo đã dời lịch và mọi người được mời nhận thông báo',
      steps: ['Mở cuộc họp Đã công bố, bấm "Dời lịch"', 'Đổi Địa điểm, bấm "Lưu và thông báo"'],
      expected:
        'Form "Dời lịch cuộc họp" khoá Tên, Mô tả, Hình thức, Mức độ mật; toast "Đã dời lịch cuộc họp. Người được mời sẽ nhận thông báo." (MSG138); vẫn Đã công bố, địa điểm mới; thanhvien@ có MeetingRescheduled, người dời lịch không có',
      async run(qa) {
        const m = await meetingAt(qa, 'Published')
        await openMeeting(qa, m)
        await press(qa, headerActions(qa).getByRole('link', { name: 'Dời lịch' }))
        const form = dialog(qa, 'Dời lịch cuộc họp')
        await qa.expect(form).toBeVisible()
        await qa.expect(form.getByPlaceholder('Ví dụ: Giao ban tuần 40')).toBeDisabled()
        await qa.expect(form.locator('textarea[name="description"]')).toBeDisabled()
        await qa.expect(form.getByRole('radio', { name: /Mật/ })).toBeDisabled()
        const place = `Phòng họp QA mới ${rand()}`
        await form.getByPlaceholder('Ví dụ: Phòng họp A — Tầng 4').fill(place)
        await settled(qa)
        await qa.shot('form-doi-lich', { fullPage: true })
        await form.getByRole('button', { name: 'Lưu và thông báo' }).click()
        await qa.expect(toast(qa, 'Đã dời lịch cuộc họp. Người được mời sẽ nhận thông báo.')).toBeVisible()
        await expectStatusChip(qa, 'Đã công bố')
        await qa.expect(qa.page.locator('.record-facts').getByText(place)).toBeVisible()
        await settled(qa)
        await qa.shot('da-doi-lich')
        await noSidewaysScroll(qa)
        const memberNotes = (await (await client(qa, 'member')).get('/api/notifications?page=1&pageSize=50')).body.items
        qa.expect(memberNotes.some((n) => n.meetingId === m.id && n.type === 'MeetingRescheduled'), 'member has no MeetingRescheduled').toBe(true)
        const ownNotes = (await (await client(qa, 'secretary')).get('/api/notifications?page=1&pageSize=50')).body.items
        qa.expect(ownNotes.some((n) => n.meetingId === m.id && n.type === 'MeetingRescheduled'), 'organizer notified of own reschedule').toBe(false)
      },
    },
    {
      id: 'AC16',
      role: 'secretary',
      title: 'API dời lịch: đổi tên/hình thức/mức độ/mô tả bị từ chối; giờ bắt đầu phải ở tương lai; ghi lịch sử Reschedule',
      steps: [
        'PUT cuộc họp Đã công bố đổi tên, mức độ mật',
        'PUT giờ bắt đầu đã qua',
        'PUT đổi giờ hợp lệ; PUT lại y hệt',
        'chutoa@ PUT dời lịch',
      ],
      expected:
        '400 MSG31 với errors đúng trường (title, sensitivity / plannedStartTime); hợp lệ 200 MSG138, vẫn Published, lịch sử có dòng Reschedule; gửi lại y hệt 200 MSG30 không thêm lịch sử; chutoa@ 404',
      async run(qa) {
        const sec = await client(qa, 'secretary')
        const c = checker(qa)
        const m = await meetingAt(qa, 'Published')
        const body = (over = {}) => ({
          title: m.title, plannedStartTime: m.plannedStartTime, plannedEndTime: m.plannedEndTime, mode: m.mode,
          location: m.location, onlineLink: m.onlineLink, sensitivity: m.sensitivity, description: m.description, ...over,
        })
        const bad = await sec.put(`/api/meetings/${m.id}`, body({ title: `${m.title} x`, sensitivity: 'Secret' }))
        if (c.check('đổi tên và mức độ', bad, 400, 'MSG31')) {
          const keys = Object.keys(bad.body.errors ?? {}).map((k) => k.toLowerCase())
          c.fact('errors.title và errors.sensitivity', keys.includes('title') && keys.includes('sensitivity'), JSON.stringify(bad.body.errors))
        }
        const now = Date.now()
        const past = await sec.put(`/api/meetings/${m.id}`, body({ plannedStartTime: iso(now - 3_600_000), plannedEndTime: iso(now + 3_600_000) }))
        if (c.check('giờ bắt đầu đã qua', past, 400, 'MSG31'))
          c.fact('errors.plannedStartTime', Object.keys(past.body.errors ?? {}).some((k) => k.toLowerCase() === 'plannedstarttime'), JSON.stringify(past.body.errors))
        const later = { plannedStartTime: iso(Date.parse(m.plannedStartTime) + 86_400_000), plannedEndTime: iso(Date.parse(m.plannedEndTime) + 86_400_000) }
        const historyBefore = (await sec.get(`/api/meetings/${m.id}/history`)).body.totalCount
        const ok = await sec.put(`/api/meetings/${m.id}`, body(later))
        if (c.check('dời lịch hợp lệ', ok, 200, 'MSG138')) c.fact('vẫn Published', ok.body.meeting.status === 'Published')
        const history = (await sec.get(`/api/meetings/${m.id}/history?pageSize=100`)).body
        c.fact('lịch sử có Reschedule', history.items.some((h) => h.action === 'Reschedule'), JSON.stringify(history.items.map((h) => h.action)))
        c.fact('thêm đúng một dòng lịch sử', history.totalCount === historyBefore + 1, `${historyBefore} → ${history.totalCount}`)
        c.check('gửi lại y hệt', await sec.put(`/api/meetings/${m.id}`, body(later)), 200, 'MSG30')
        c.fact('không thêm lịch sử', (await sec.get(`/api/meetings/${m.id}/history`)).body.totalCount === historyBefore + 1)
        c.check('chutoa@ dời lịch', await (await client(qa, 'chair')).put(`/api/meetings/${m.id}`, body(later)), 404)
        c.done()
      },
    },

    // ----------------------------------------------------------------------------------------------- UC-20 / UC-21
    {
      id: 'AC17',
      role: 'chair',
      devices: ['desktop', 'phone'],
      title: 'Chủ toạ bắt đầu cuộc họp đã công bố rồi kết thúc (có xác nhận)',
      steps: [
        'chutoa@ mở cuộc họp Đã công bố (lịch còn 3 ngày), bấm "Bắt đầu họp"',
        'Hộp "Bắt đầu sớm hơn lịch?" → "Vẫn bắt đầu"',
        'Bấm "Kết thúc họp" → xác nhận "Kết thúc họp"',
      ],
      expected:
        'Toast "Bắt đầu cuộc họp thành công." (MSG110), Đang diễn ra, có giờ bắt đầu thực tế; nút thành "Kết thúc họp". Sau xác nhận: toast "Kết thúc cuộc họp thành công." (MSG111), Đã kết thúc, có giờ kết thúc thực tế, không còn nút bắt đầu/kết thúc',
      async run(qa) {
        const m = await meetingAt(qa, 'Published')
        await openMeeting(qa, m)
        await press(qa, headerActions(qa).getByRole('button', { name: 'Bắt đầu họp' }))
        const early = dialog(qa, 'Bắt đầu sớm hơn lịch?')
        await qa.expect(early).toBeVisible()
        await settled(qa)
        await qa.shot('hoi-bat-dau-som')
        await early.getByRole('button', { name: 'Vẫn bắt đầu' }).click()
        await qa.expect(toast(qa, 'Bắt đầu cuộc họp thành công.')).toBeVisible()
        await expectStatusChip(qa, 'Đang diễn ra')
        const end = headerActions(qa).getByRole('button', { name: 'Kết thúc họp' })
        await qa.expect(end).toBeVisible()
        await settled(qa)
        await qa.shot('dang-dien-ra')
        const running = (await (await client(qa, 'chair')).get(`/api/meetings/${m.id}`)).body
        qa.expect(running.actualStartedAt).toBeTruthy()
        await press(qa, end)
        const confirm = dialog(qa, 'Kết thúc cuộc họp?')
        await qa.expect(confirm).toBeVisible()
        await settled(qa)
        await qa.shot('xac-nhan-ket-thuc')
        await confirm.getByRole('button', { name: 'Kết thúc họp' }).click()
        await qa.expect(toast(qa, 'Kết thúc cuộc họp thành công.')).toBeVisible()
        await expectStatusChip(qa, 'Đã kết thúc')
        await qa.expect(headerActions(qa).getByRole('button', { name: /Bắt đầu họp|Kết thúc họp/ })).toHaveCount(0)
        await settled(qa)
        await qa.shot('da-ket-thuc')
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
        const ended = (await (await client(qa, 'chair')).get(`/api/meetings/${m.id}`)).body
        qa.expect(ended.status).toBe('Ended')
        qa.expect(ended.actualEndedAt).toBeTruthy()
        qa.expect(ended.actualStartedAt).toBe(running.actualStartedAt)
      },
    },
    {
      id: 'AC18',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Người không phải Chủ toạ không thấy nút Bắt đầu/Kết thúc; API: không phải Chủ toạ 403, sai trạng thái 409',
      steps: [
        'thuky@ (người tổ chức, Thư ký) mở cuộc họp Đã công bố và Đang diễn ra',
        'API: thuky@, thanhvien@ Start/End; chutoa@ Start cuộc họp Nháp, Đã duyệt, Đang diễn ra; End cuộc họp Đã công bố, Đã kết thúc',
      ],
      expected:
        'Không có "Bắt đầu họp"/"Kết thúc họp" với thuky@; Start bởi người không phải Chủ toạ 403 MSG112, End 403 MSG114; sai trạng thái 409 MSG112 / MSG114, trạng thái không đổi',
      async run(qa) {
        const published = await meetingAt(qa, 'Published')
        const running = await meetingAt(qa, 'InProgress')
        for (const m of [published, running]) {
          await openMeeting(qa, m)
          await qa.expect(headerActions(qa).getByRole('button', { name: /Bắt đầu họp|Kết thúc họp/ })).toHaveCount(0)
          await settled(qa)
          await qa.shot(`khong-nut-${m.status}`)
        }
        if (qa.device !== 'desktop') return
        const c = checker(qa)
        c.check('thuky@ Start', await transition(qa, 'secretary', published.id, { action: 'Start' }), 403, 'MSG112')
        c.check('thanhvien@ Start', await transition(qa, 'member', published.id, { action: 'Start' }), 403, 'MSG112')
        c.check('thuky@ End', await transition(qa, 'secretary', running.id, { action: 'End' }), 403, 'MSG114')
        c.check('thanhvien@ End', await transition(qa, 'member', running.id, { action: 'End' }), 403, 'MSG114')
        for (const status of ['Draft', 'Approved']) {
          const m = await meetingAt(qa, status)
          c.check(`Start khi ${status}`, await transition(qa, 'chair', m.id, { action: 'Start' }), 409, 'MSG112')
        }
        c.check('Start khi InProgress', await transition(qa, 'chair', running.id, { action: 'Start' }), 409, 'MSG112')
        c.check('End khi Published', await transition(qa, 'chair', published.id, { action: 'End' }), 409, 'MSG114')
        const ended = await meetingAt(qa, 'Ended')
        c.check('End khi Ended', await transition(qa, 'chair', ended.id, { action: 'End' }), 409, 'MSG114')
        const sec = await client(qa, 'secretary')
        c.fact('Published không đổi', (await sec.get(`/api/meetings/${published.id}`)).body.status === 'Published')
        c.fact('InProgress không đổi', (await sec.get(`/api/meetings/${running.id}`)).body.status === 'InProgress')
        c.done()
      },
    },

    // ----------------------------------------------------------------------------------------------- UC-22 Cancel
    {
      id: 'AC19',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Người tổ chức huỷ cuộc họp chưa bắt đầu, bắt buộc lý do; "Giữ cuộc họp" không đổi gì',
      steps: [
        'Mở cuộc họp Đã công bố, bấm "Huỷ cuộc họp", rồi "Giữ cuộc họp"',
        'Bấm lại "Huỷ cuộc họp", bấm "Huỷ cuộc họp" khi lý do trống',
        'Nhập lý do, bấm "Huỷ cuộc họp"',
      ],
      expected:
        'Giữ: vẫn Đã công bố. Lý do trống: "Cần nhập lý do huỷ cuộc họp." (MSG262). Có lý do: toast "Đã huỷ cuộc họp." (MSG261), Đã huỷ, thanh "Cuộc họp đã huỷ" có người huỷ, giờ, lý do; không còn nút Dời lịch/Huỷ; thanhvien@ nhận MeetingCancelled và vẫn thấy cuộc họp',
      async run(qa) {
        const m = await meetingAt(qa, 'Published')
        const reason = `QA huỷ ${rand()}: lãnh đạo đi công tác.`
        await openMeeting(qa, m)
        const cancel = headerActions(qa).getByRole('button', { name: 'Huỷ cuộc họp' })
        await press(qa, cancel)
        let form = dialog(qa, 'Huỷ cuộc họp?')
        await qa.expect(form).toBeVisible()
        await qa.expect(form.getByText('Mọi người được mời sẽ nhận thông báo và email kèm lý do.', { exact: false })).toBeVisible()
        await form.getByRole('button', { name: 'Giữ cuộc họp' }).click()
        await qa.expect(form).toHaveCount(0)
        await expectStatusChip(qa, 'Đã công bố')
        await press(qa, cancel)
        form = dialog(qa, 'Huỷ cuộc họp?')
        await form.getByRole('button', { name: 'Huỷ cuộc họp' }).click()
        await qa.expect(form.getByText('Cần nhập lý do huỷ cuộc họp.')).toBeVisible()
        await settled(qa)
        await qa.shot('ly-do-huy-trong')
        await form.getByRole('textbox').fill(reason)
        await form.getByRole('button', { name: 'Huỷ cuộc họp' }).click()
        await qa.expect(toast(qa, 'Đã huỷ cuộc họp.')).toBeVisible()
        await expectStatusChip(qa, 'Đã huỷ')
        await qa.expect(qa.page.getByRole('heading', { name: 'Cuộc họp đã huỷ' })).toBeVisible()
        const bar = qa.page.getByRole('region', { name: 'Cuộc họp đã huỷ' })
        await qa.expect(bar.getByText(/Bạn huỷ lúc/)).toBeVisible()
        await qa.expect(bar.getByText(reason)).toBeVisible()
        await qa.expect(headerActions(qa).getByRole('button', { name: 'Huỷ cuộc họp' })).toHaveCount(0)
        await qa.expect(headerActions(qa).getByRole('link', { name: /Dời lịch|Chỉnh sửa/ })).toHaveCount(0)
        await settled(qa)
        await qa.shot('da-huy')
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
        const member = await client(qa, 'member')
        const notes = (await member.get('/api/notifications?page=1&pageSize=50')).body.items
        qa.expect(notes.some((n) => n.meetingId === m.id && n.type === 'MeetingCancelled'), 'member has no MeetingCancelled').toBe(true)
        const seen = await member.get(`/api/meetings/${m.id}`)
        qa.expectStatus(seen, 200)
        qa.expect(seen.body.cancellation?.reason).toBe(reason)
      },
    },
    {
      id: 'AC20',
      role: 'secretary',
      title: 'API huỷ: chỉ người tổ chức, chỉ trước khi bắt đầu, lý do 1–1.000 ký tự; cuộc họp đã huỷ là trạng thái cuối',
      steps: [
        'Cancel lý do khoảng trắng; 1.001 ký tự',
        'chutoa@ huỷ cuộc họp Nháp của thuky@ (đọc được, không phải người tổ chức); thanhvien@ huỷ (không đọc được)',
        'Cancel cuộc họp Đang diễn ra, Đã kết thúc, Đã huỷ',
        'Với cuộc họp Đã huỷ: PUT, SubmitForApproval, Approve, Publish, Start',
        'Huỷ khi Chờ duyệt: người duyệt nhận thông báo',
      ],
      expected:
        '400 MSG262 / MSG345; chutoa@ 403 MSG85, thanhvien@ 404; sai trạng thái 409 MSG263; cuộc họp Đã huỷ: PUT 409 MSG33, Submit 409 MSG41, Approve 409 MSG132, Publish 409 MSG46, Start 409 MSG112; huỷ khi Chờ duyệt: chutoa@ có MeetingCancelled',
      async run(qa) {
        const u = await users(qa)
        const c = checker(qa)
        const draft = await meetingAt(qa, 'Draft')
        c.check('lý do khoảng trắng', await transition(qa, 'secretary', draft.id, { action: 'Cancel', reason: '  ' }), 400, 'MSG262')
        c.check('lý do 1.001 ký tự', await transition(qa, 'secretary', draft.id, { action: 'Cancel', reason: 'z'.repeat(1001) }), 400, 'MSG345')
        c.check('chutoa@ huỷ', await transition(qa, 'chair', draft.id, { action: 'Cancel', reason: 'QA' }), 403, 'MSG85')
        c.check('thanhvien@ huỷ', await transition(qa, 'member', draft.id, { action: 'Cancel', reason: 'QA' }), 404)
        c.fact('vẫn Nháp', (await (await client(qa, 'secretary')).get(`/api/meetings/${draft.id}`)).body.status === 'Draft')
        for (const status of ['InProgress', 'Ended', 'Cancelled']) {
          const m = await meetingAt(qa, status)
          c.check(`Cancel khi ${status}`, await transition(qa, 'secretary', m.id, { action: 'Cancel', reason: 'QA' }), 409, 'MSG263')
        }
        const gone = await meetingAt(qa, 'Cancelled', { cancelFrom: 'Approved' })
        const sec = await client(qa, 'secretary')
        c.check('PUT khi Cancelled', await sec.put(`/api/meetings/${gone.id}`, { title: gone.title, plannedStartTime: gone.plannedStartTime, plannedEndTime: gone.plannedEndTime, mode: gone.mode, location: 'x', onlineLink: null, sensitivity: gone.sensitivity, description: gone.description }), 409, 'MSG33')
        c.check('Submit khi Cancelled', await transition(qa, 'secretary', gone.id, { action: 'SubmitForApproval', assignedApproverId: u.chair.id }), 409, 'MSG41')
        c.check('Approve khi Cancelled', await transition(qa, 'chair', gone.id, { action: 'Approve' }), 409, 'MSG132')
        c.check('Publish khi Cancelled', await transition(qa, 'secretary', gone.id, { action: 'Publish' }), 409, 'MSG46')
        c.check('Start khi Cancelled', await transition(qa, 'chair', gone.id, { action: 'Start' }), 409, 'MSG112')
        const waiting = await meetingAt(qa, 'Cancelled', { cancelFrom: 'WaitingApproval' })
        const notes = (await (await client(qa, 'chair')).get('/api/notifications?page=1&pageSize=50')).body.items
        c.fact('người duyệt nhận MeetingCancelled', notes.some((n) => n.meetingId === waiting.id && n.type === 'MeetingCancelled'))
        c.done()
      },
    },

    // ----------------------------------------------------------------------------------------------- UC-15 / UC-16 / BR-016, BR-017
    {
      id: 'AC21',
      role: 'member',
      devices: ['desktop', 'phone'],
      title: 'Thành viên chỉ thấy cuộc họp từ khi công bố: chi tiết đọc được, không có nút quản lý; bản nháp/chờ duyệt/đã duyệt như không tồn tại',
      steps: [
        'thanhvien@ mở trang chi tiết cuộc họp Đã công bố có mình là Thành viên',
        'Mở đường dẫn cuộc họp Nháp, Chờ duyệt, Đã duyệt có mình trong thành phần',
        'Tìm các cuộc họp đó trên Danh sách (Mọi trạng thái); GET chi tiết qua API',
      ],
      expected:
        'Đã công bố: tên, giờ, hình thức, địa điểm, trạng thái, mức độ; không có Chỉnh sửa/Dời lịch/Huỷ/Bắt đầu; ba cuộc họp chưa công bố: trang "Không tìm thấy trang", không có trong danh sách, API 404',
      async run(qa) {
        const tag = rand()
        const published = await meetingAt(qa, 'Published', { title: `FE-04 hiển thị ${tag} công bố` })
        const hidden = []
        for (const status of ['Draft', 'WaitingApproval', 'Approved']) hidden.push(await meetingAt(qa, status, { title: `FE-04 hiển thị ${tag} ${status}` }))
        await openMeeting(qa, published)
        await expectStatusChip(qa, 'Đã công bố')
        await qa.expect(qa.page.locator('.record-facts').getByText('Phòng họp QA')).toBeVisible()
        await qa.expect(headerActions(qa).getByRole('link', { name: /Chỉnh sửa|Dời lịch/ })).toHaveCount(0)
        await qa.expect(headerActions(qa).getByRole('button', { name: /Huỷ cuộc họp|Bắt đầu họp/ })).toHaveCount(0)
        await settled(qa)
        await qa.shot('chi-tiet-da-cong-bo', { fullPage: true })
        await noSidewaysScroll(qa)
        await qa.goto(`/mgmt/meetings/${hidden[0].id}`)
        await qa.expect(qa.page.getByRole('heading', { name: 'Không tìm thấy trang' })).toBeVisible()
        await qa.shot('nhap-khong-tim-thay')
        for (const m of hidden.slice(1)) {
          await qa.goto(`/mgmt/meetings/${m.id}`)
          await qa.expect(qa.page.getByRole('heading', { name: 'Không tìm thấy trang' })).toBeVisible()
        }
        await qa.goto(`/mgmt/meetings?status=all&when=all&q=${encodeURIComponent(`hiển thị ${tag}`)}`)
        const rows = qa.page.locator('table.meetings-table tbody tr')
        await qa.expect(rows).toHaveCount(1)
        await qa.expect(rows.first()).toContainText(published.title)
        await settled(qa)
        await qa.shot('danh-sach-chi-co-cong-bo')
        await noSidewaysScroll(qa)
        if (qa.device !== 'desktop') return
        const member = await client(qa, 'member')
        for (const m of hidden) qa.expectStatus(await member.get(`/api/meetings/${m.id}`), 404)
        qa.expectStatus(await member.get(`/api/meetings/${published.id}`), 200)
      },
    },
    {
      id: 'AC22',
      role: 'chair',
      title: 'Chủ toạ và người duyệt đọc cuộc họp đúng theo BR-016, BR-017, BR-030; quản trị không tự đọc được cuộc họp không liên quan',
      steps: [
        'chutoa@ (Chủ toạ) GET cuộc họp Nháp của thuky@',
        'quantri@ (đủ capability, không trong thành phần, không được phân công) GET cuộc họp Nháp, Chờ duyệt, Đã công bố của thuky@ và tìm trên danh sách',
      ],
      expected: 'chutoa@ 200 (Chủ toạ đọc mọi trạng thái); quantri@ 404 cả ba, danh sách không có',
      async run(qa) {
        const c = checker(qa)
        const tag = rand()
        const draft = await meetingAt(qa, 'Draft', { title: `FE-04 quyền đọc ${tag} nháp` })
        c.check('chutoa@ đọc Nháp', await (await client(qa, 'chair')).get(`/api/meetings/${draft.id}`), 200)
        const admin = await client(qa, 'admin')
        const others = [draft, await meetingAt(qa, 'WaitingApproval', { title: `FE-04 quyền đọc ${tag} chờ` }), await meetingAt(qa, 'Published', { title: `FE-04 quyền đọc ${tag} công bố` })]
        for (const m of others) c.check(`quantri@ đọc ${m.status}`, await admin.get(`/api/meetings/${m.id}`), 404)
        const list = await admin.get(`/api/meetings?q=${encodeURIComponent(`quyền đọc ${tag}`)}`)
        if (c.check('quantri@ danh sách', list, 200)) c.fact('quantri@ không thấy', list.body.totalCount === 0, `totalCount ${list.body.totalCount}`)
        c.done()
      },
    },
    {
      id: 'AC23',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Danh sách cuộc họp mở sẵn thẻ "Họp hôm nay": mọi trạng thái, Thời điểm Hôm nay (PO, PR #287)',
      steps: ['Đăng nhập thuky@', 'Bấm "Cuộc họp" trên menu (mở /mgmt/meetings không tham số)'],
      expected:
        'Thẻ "Họp hôm nay" được chọn, Trạng thái "Mọi trạng thái", Thời điểm "Hôm nay" (PO chọn khi đổi sang bốn thẻ AI-eCabinet, PR #287; docs/03-api/05). UC-16 bước 1 còn ghi "Đang diễn ra": BA cập nhật',
      async run(qa) {
        await qa.goto('/mgmt/meetings')
        if (qa.device === 'phone') {
          const toggle = qa.page.getByRole('button', { name: /Bộ lọc/ })
          if ((await toggle.getAttribute('aria-expanded')) === 'false') await toggle.tap()
        }
        await qa.expect(qa.page.getByRole('button', { name: /^Trạng thái:/ })).toBeVisible()
        await settled(qa)
        await qa.shot('bo-loc-mac-dinh')
        const status = await qa.page.getByRole('button', { name: /^Trạng thái:/ }).getAttribute('aria-label')
        const when = await qa.page.getByRole('button', { name: /^Thời điểm:/ }).getAttribute('aria-label')
        qa.note(`Mặc định: ${status} · ${when} · URL ${qa.page.url()}`)
        qa.expect(status, `the list opens with "${status}" / "${when}"`).toBe('Trạng thái: Mọi trạng thái')
        qa.expect(when, `the list opens with "${status}" / "${when}"`).toBe('Thời điểm: Hôm nay')
        await qa.expect(qa.page.getByRole('button', { name: /Họp hôm nay/ })).toHaveAttribute('aria-pressed', 'true')
      },
    },
    {
      id: 'AC23b',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Danh sách cuộc họp: đổi giá trị bộ lọc không làm ô lọc co giãn hay xô các ô bên cạnh',
      steps: ['Mở Danh sách cuộc họp', 'Lần lượt chọn từng giá trị của Trạng thái, Mức độ, Thời điểm'],
      expected:
        'Mỗi ô lọc giữ một bề rộng với mọi giá trị (rộng bằng nhãn dài nhất); các ô sau nó không dịch chỗ. Khánh báo lỗi 15/11: "Hôm nay" → "Mọi thời điểm" làm ô dài ra',
      async run(qa) {
        await qa.goto('/mgmt/meetings')
        if (qa.device === 'phone') {
          const toggle = qa.page.getByRole('button', { name: /Bộ lọc/ })
          if ((await toggle.getAttribute('aria-expanded')) === 'false') await toggle.tap()
        }
        for (const name of ['Trạng thái', 'Mức độ', 'Thời điểm']) {
          const trigger = qa.page.getByRole('button', { name: new RegExp(`^${name}:`) })
          await press(qa, trigger)
          const options = await qa.page.getByRole('listbox', { name }).getByRole('option').allInnerTexts()
          await qa.page.keyboard.press('Escape')
          const widths = new Set()
          for (const option of options) {
            await press(qa, trigger)
            await qa.page.getByRole('listbox', { name }).getByRole('option', { name: option.trim(), exact: true }).click()
            widths.add(Math.round((await trigger.boundingBox()).width))
          }
          qa.note(`${name}: ${[...widths].join('/')}px qua ${options.length} giá trị`)
          qa.expect(widths.size, `${name} changes width: ${[...widths].join('/')}px`).toBe(1)
        }
        await settled(qa)
        await qa.shot('bo-loc-giu-be-rong')
        await noSidewaysScroll(qa)
      },
    },
    {
      id: 'AC27',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Danh sách cuộc họp: tìm không phân biệt hoa thường và dấu, lọc Đã huỷ, không khớp thì báo',
      steps: [
        'Mở Danh sách cuộc họp, chọn Trạng thái "Mọi trạng thái", Thời điểm "Mọi thời điểm"',
        'Tìm "fe-04 kiem thu dac biet <mã>" (không dấu, chữ thường)',
        'Chọn Trạng thái "Đã huỷ"',
        'Tìm một chuỗi không có',
      ],
      expected:
        'Tìm không dấu ra đúng 2 cuộc họp "[QA] FE-04 Kiểm Thử Đặc Biệt <mã> …"; lọc Đã huỷ chỉ còn cuộc họp đã huỷ, có nhãn Đã huỷ (BR-028); không khớp: "Không có cuộc họp nào khớp bộ lọc."; không cuộn ngang',
      async run(qa) {
        const tag = rand()
        const live = await meetingAt(qa, 'Draft', { title: `FE-04 Kiểm Thử Đặc Biệt ${tag} còn` })
        const gone = await meetingAt(qa, 'Cancelled', { title: `FE-04 Kiểm Thử Đặc Biệt ${tag} huỷ` })
        await qa.goto('/mgmt/meetings')
        if (qa.device === 'phone') {
          const toggle = qa.page.getByRole('button', { name: /Bộ lọc/ })
          if ((await toggle.getAttribute('aria-expanded')) === 'false') await toggle.tap()
        }
        await press(qa, qa.page.getByRole('button', { name: /^Thời điểm:/ }))
        await qa.page.getByRole('listbox', { name: 'Thời điểm' }).getByRole('option', { name: 'Mọi thời điểm' }).click()
        await press(qa, qa.page.getByRole('button', { name: /^Trạng thái:/ }))
        await qa.page.getByRole('listbox', { name: 'Trạng thái' }).getByRole('option', { name: 'Mọi trạng thái' }).click()
        await qa.page.getByRole('searchbox', { name: 'Tìm theo tên cuộc họp' }).fill(`fe-04 kiem thu dac biet ${tag}`)
        const rows = qa.page.locator('table.meetings-table tbody tr')
        await qa.expect(rows).toHaveCount(2)
        await qa.expect(qa.page.getByRole('link', { name: live.title, exact: true })).toBeVisible()
        await qa.expect(qa.page.getByRole('link', { name: gone.title, exact: true })).toBeVisible()
        await settled(qa)
        await qa.shot('tim-khong-dau')
        await noSidewaysScroll(qa)
        await press(qa, qa.page.getByRole('button', { name: 'Trạng thái: Mọi trạng thái' }))
        await qa.page.getByRole('listbox', { name: 'Trạng thái' }).getByRole('option', { name: 'Đã huỷ' }).click()
        await qa.expect(rows).toHaveCount(1)
        await qa.expect(rows.first()).toContainText(gone.title)
        await qa.expect(rows.first()).toContainText('Đã huỷ')
        await settled(qa)
        await qa.shot('loc-da-huy')
        await qa.page.getByRole('searchbox', { name: 'Tìm theo tên cuộc họp' }).fill(`khong-ton-tai-${tag}-zz`)
        await qa.expect(qa.page.getByText('Không có cuộc họp nào khớp bộ lọc.')).toBeVisible()
        await settled(qa)
        await qa.shot('khong-khop')
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
      },
    },
    {
      id: 'AC24',
      role: 'secretary',
      title: 'API danh sách: tiêu chí sai 400 MSG32; lọc Cancelled; tìm không dấu; người khác không thấy bản nháp',
      steps: [
        'GET /api/meetings?status=Khac, ?status=Archived, ?pageSize=0, ?sortBy=startsAt, ?from=2026-11-20T00:00 (thiếu múi giờ)',
        'GET ?status=Cancelled&q=<mã>, ?q=<tên không dấu>',
        'thanhvien@ GET ?q=<mã> với bản nháp của thuky@',
      ],
      expected: 'Mỗi tiêu chí sai: 400 MSG32 "Tiêu chí danh sách cuộc họp không hợp lệ."; lọc và tìm trả đúng; thanhvien@ totalCount 0',
      async run(qa) {
        const sec = await client(qa, 'secretary')
        const c = checker(qa)
        for (const q of ['status=Khac', 'status=Archived', 'pageSize=0', 'sortBy=startsAt', 'from=2026-11-20T00:00']) {
          const res = await sec.get(`/api/meetings?${q}`)
          if (c.check(q, res, 400, 'MSG32')) c.fact(`${q} message`, res.body.message === 'Tiêu chí danh sách cuộc họp không hợp lệ.', res.body.message)
        }
        const tag = rand()
        const draft = await meetingAt(qa, 'Draft', { title: `FE-04 Lọc Đúng ${tag} nháp` })
        const gone = await meetingAt(qa, 'Cancelled', { title: `FE-04 Lọc Đúng ${tag} huỷ` })
        const cancelled = (await sec.get(`/api/meetings?status=Cancelled&q=${tag}`)).body
        c.fact('status=Cancelled', cancelled.totalCount === 1 && cancelled.items[0]?.id === gone.id, JSON.stringify(cancelled.items?.map((i) => i.title)))
        const plain = (await sec.get(`/api/meetings?q=${encodeURIComponent(`LOC DUNG ${tag}`)}`)).body
        c.fact('tìm không dấu', plain.totalCount === 2, `totalCount ${plain.totalCount}`)
        const member = (await (await client(qa, 'member')).get(`/api/meetings?q=${tag}`)).body
        c.fact('thanhvien@ không thấy nháp/huỷ trước công bố', member.totalCount === 0, `totalCount ${member.totalCount}`)
        qa.note(`Nháp ${draft.id}, huỷ ${gone.id}`)
        c.done()
      },
    },
    {
      id: 'AC25',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Lịch sử trạng thái: người tổ chức thấy đủ các bước kèm lý do từ chối; thành viên thấy dòng thời gian nhưng không có lý do từ chối',
      steps: [
        'Cuộc họp: gửi duyệt → từ chối → gửi lại → duyệt → công bố',
        'thuky@ mở chi tiết, xem "Lịch sử trạng thái"',
        'API: GET history bằng thuky@ và thanhvien@',
      ],
      expected:
        'Thẻ "Lịch sử trạng thái" có các dòng theo thứ tự cũ trước, dòng từ chối có lý do; API người tổ chức có rejectionReason, thanhvien@ có cùng số dòng nhưng rejectionReason null',
      async run(qa) {
        const u = await users(qa)
        const m = await meetingAt(qa, 'Rejected')
        qa.expectStatus(await transition(qa, 'secretary', m.id, { action: 'SubmitForApproval', assignedApproverId: u.chair.id }), 200)
        qa.expectStatus(await transition(qa, 'chair', m.id, { action: 'Approve' }), 200)
        qa.expectStatus(await transition(qa, 'secretary', m.id, { action: 'Publish' }), 200)
        await openMeeting(qa, m)
        const card = qa.page.locator('section, div').filter({ has: qa.page.getByRole('heading', { name: 'Lịch sử trạng thái' }) }).last()
        await qa.expect(card.getByText('QA: cần bổ sung nội dung chương trình.')).toBeVisible()
        await card.scrollIntoViewIfNeeded()
        await settled(qa)
        await qa.shot('lich-su-trang-thai', { fullPage: true })
        await noSidewaysScroll(qa)
        if (qa.device !== 'desktop') return
        const own = (await (await client(qa, 'secretary')).get(`/api/meetings/${m.id}/history`)).body.items
        qa.expect(own.map((h) => h.resultingStatus)).toEqual(['Draft', 'WaitingApproval', 'Rejected', 'WaitingApproval', 'Approved', 'Published'])
        qa.expect(own.find((h) => h.resultingStatus === 'Rejected').rejectionReason).toBe('QA: cần bổ sung nội dung chương trình.')
        const seen = await (await client(qa, 'member')).get(`/api/meetings/${m.id}/history`)
        qa.expectStatus(seen, 200)
        qa.expect(seen.body.items.length).toBe(own.length)
        qa.expect(seen.body.items.every((h) => h.rejectionReason == null), 'member sees the rejection reason').toBe(true)
      },
    },
    {
      id: 'AC26',
      role: 'anon',
      title: 'Chưa đăng nhập: trang cuộc họp đưa về Đăng nhập, API 401',
      steps: ['Không đăng nhập, mở /mgmt/meetings', 'GET /api/meetings, POST /api/meetings, POST transitions không token'],
      expected: 'Chuyển tới /login; API 401',
      async run(qa) {
        await qa.goto('/mgmt/meetings')
        await qa.expect(qa.page).toHaveURL(/\/login/)
        await qa.shot('ve-dang-nhap')
        const api = await qa.api('anon')
        qa.expectStatus(await api.get('/api/meetings'), 401)
        qa.expectStatus(await api.post('/api/meetings', { title: 'x' }), 401)
        qa.expectStatus(await api.post('/api/meetings/0199ac54-d000-7000-8000-000000000019/transitions', { action: 'Publish' }), 401)
      },
    },
  ],
}

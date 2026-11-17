// FE-06 — Quản lý thành phần tham dự (QA subtask SCRUM-193): UC-27 view roster, UC-28a add / invite a guest, UC-28b
// meeting roles, UC-28c remove / revoke, UC-29 answer the invitation, UC-30 response labels, UC-31 attendance, UC-73
// hand the chair over. Business rules BR-79/80/82/88/105/106/115/121/122/126/170/172/176/181/182/294/295/321.
//
// Criteria come from docs/01-requirements/05-use-case-specifications.md (FE-06), 06-business-rules.md,
// docs/03-api/09-thanh-phan-tham-du.md, 10-diem-danh.md, the message catalogue, and the Jira comments of SCRUM-20,
// SCRUM-26, SCRUM-97 and SCRUM-147 (deviations: answer once, the Chair never answers, declining asks first and only
// hides the meeting from the calendar, no response or attendance totals, the chair moves from the row's "Đổi vai trò",
// MSG258 never shown, a Secretary replaces the Chair after publication only when the Chair's account is locked).
// Dropped by the PO and not tested: delegation, per-participant grants, organizer secretary-recovery.
//
// Role × action (what the plan checks):
//
// | Vai trò (trong cuộc họp)       | Hành động                                              | Mong đợi                          |
// |--------------------------------|--------------------------------------------------------|-----------------------------------|
// | Thư ký                         | thêm nội bộ / mời khách (Nháp…Đang họp)                | 201 MSG96                         |
// | Thư ký                         | thêm khi Chờ duyệt / Đã kết thúc / Đã huỷ              | 409 MSG98, không có nút           |
// | Thư ký                         | đổi vai trò, thay Chủ toạ trước công bố                | 200 MSG100                        |
// | Thư ký                         | thay Chủ toạ còn hoạt động sau công bố                 | 409 MSG256, không có lựa chọn     |
// | Thư ký                         | thay Chủ toạ bị khoá sau công bố                       | 200 MSG100                        |
// | Thư ký                         | gỡ thành viên / thu hồi khách                          | 200 MSG103                        |
// | Thư ký                         | gỡ Chủ toạ, gỡ/hạ Thư ký cuối sau công bố             | 409 MSG109                        |
// | Thư ký                         | điểm danh khi Đang họp                                 | 200 MSG147                        |
// | Thư ký                         | điểm danh khi Đã công bố / Đã kết thúc                 | 409 MSG149                        |
// | Chủ toạ                        | giao Chủ toạ (Đã công bố/Đang họp)                     | 200 MSG257                        |
// | Chủ toạ                        | trả lời lời mời                                        | 409 MSG286, không có câu hỏi      |
// | Chủ toạ / Thành viên           | điểm danh                                              | 403                               |
// | Thành viên                     | thêm / đổi vai trò / gỡ / editor-options               | 403 MSG85, không có nút           |
// | Thành viên                     | trả lời Tham dự / Không tham dự (một lần)              | 200 MSG105, lần khác 409 MSG285   |
// | Người tổ chức không tham dự    | đọc thành phần / quản lý                               | 200 / 403 MSG85                   |
// | Người ngoài cuộc họp           | đọc thành phần, điểm danh                              | 404 rỗng                          |
// | Chưa đăng nhập                 | mọi endpoint                                           | 401                               |
// | Khách (OTP)                    | xem thành phần không thấy email người khác, trả lời    | INCONCLUSIVE (cần OTP thật)       |
//
// Safe on a shared local dev database: every meeting is "[QA] FE-06 …", created here through the API, dated six weeks
// ahead at varied hours. Read-only criteria reuse one meeting per state (found by its exact title); criteria that change
// a meeting create their own and cancel it (or end it, once started) when done. Extra people are the load-test accounts
// load41–load44, used only as participants, and one account the plan owns, qa-fe06-chutoa-khoa@trustcabinet.local, that
// it locks and unlocks. Guests are invited only to @trustcabinet.local, only on Draft meetings (no invitation email).

const DOMAIN = 'trustcabinet.local'
const LOCKED_CHAIR_EMAIL = `qa-fe06-chutoa-khoa@${DOMAIN}`
const PASSWORD = process.env.QA_PASSWORD ?? 'TrustCabinet@2026'
const ACCOUNTS = {
  admin: 'quantri@trustcabinet.local',
  chair: 'chutoa@trustcabinet.local',
  secretary: 'thuky@trustcabinet.local',
  member: 'thanhvien@trustcabinet.local',
}

const MSG = {
  MSG96: 'Thêm thành phần tham dự thành công.',
  MSG96_GUEST: 'Đã mời khách. Thư mời sẽ được gửi tới email của khách.',
  MSG100: 'Cập nhật vai trò cuộc họp thành công.',
  MSG103: 'Gỡ thành phần tham dự thành công.',
  MSG103_GUEST: 'Đã thu hồi lời mời khách.',
  MSG105: 'Đã ghi nhận phản hồi lời mời.',
  MSG147: 'Lưu điểm danh thành công.',
  MSG257: 'Giao vai trò Chủ toạ thành công.',
}

// ---------------------------------------------------------------------------------------------------------------------
// HTTP: the runner's client sends no custom header, and every roster change needs If-Match, so the plan keeps its own.

const TOKENS = {}
async function login(base, role) {
  const res = await fetch(`${base}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email: ACCOUNTS[role] ?? role, password: PASSWORD }),
  })
  if (!res.ok) throw new Error(`sign-in as ${role}: HTTP ${res.status}`)
  TOKENS[role] = (await res.json()).accessToken
  return TOKENS[role]
}

/** `{ status, body, etag }`; `role` 'anon' sends no token. A stale token is renewed once. */
async function http(qa, role, method, path, { body, ifMatch, retried = false } = {}) {
  const headers = {}
  if (role !== 'anon') headers.Authorization = `Bearer ${TOKENS[role] ?? (await login(qa.base, role))}`
  if (body !== undefined) headers['Content-Type'] = 'application/json'
  if (ifMatch) headers['If-Match'] = ifMatch
  const res = await fetch(`${qa.base}${path}`, {
    method,
    headers,
    body: body === undefined ? undefined : JSON.stringify(body),
  })
  if (res.status === 401 && role !== 'anon' && !retried) {
    await login(qa.base, role)
    return http(qa, role, method, path, { body, ifMatch, retried: true })
  }
  const text = await res.text()
  let json
  try {
    json = text ? JSON.parse(text) : undefined
  } catch {
    json = undefined
  }
  return { status: res.status, body: json ?? text, etag: res.headers.get('etag') }
}
const as = (qa, role) => ({
  get: (p, o) => http(qa, role, 'GET', p, o),
  post: (p, body, o = {}) => http(qa, role, 'POST', p, { ...o, body: body ?? {} }),
  put: (p, body, o = {}) => http(qa, role, 'PUT', p, { ...o, body: body ?? {} }),
  delete: (p, o) => http(qa, role, 'DELETE', p, o),
})
const tag = (revision) => `"participants-${revision}"`

// ---------------------------------------------------------------------------------------------------------------------
// Accounts and meetings.

const USERS = {}
async function users(qa) {
  for (const role of ['admin', 'chair', 'secretary', 'member']) {
    if (USERS[role]) continue
    const me = (await as(qa, role).get('/api/auth/me')).body
    USERS[role] = { id: me.id, name: me.fullName, email: me.email, role }
  }
  if (!USERS.extra) {
    USERS.extra = []
    for (const n of [41, 42, 43, 44]) {
      const res = await as(qa, 'secretary').get(`/api/meetings/participant-options?q=load${n}%40`)
      const hit = res.body?.items?.find((i) => i.email === `load${n}@${DOMAIN}`)
      if (hit) USERS.extra.push({ id: hit.id, name: hit.fullName, email: hit.email })
    }
    if (USERS.extra.length < 4) throw new Error('load41–load44 not found: the plan needs four extra active accounts')
  }
  return USERS
}

/** The account the plan locks to test UC-28b AF-1 after publication. Created once, active when returned. */
async function lockedChairUser(qa) {
  const admin = as(qa, 'admin')
  const list = await admin.get('/api/users')
  let user = Array.isArray(list.body) ? list.body.find((u) => u.email === LOCKED_CHAIR_EMAIL) : undefined
  if (!user) {
    const roles = (await admin.get('/api/roles')).body
    const memberRole = roles.find((r) => r.isDefault) ?? roles.find((r) => r.name === 'Thành viên')
    const password = `Qa-${Math.random().toString(36).slice(2)}${Math.random().toString(36).slice(2)}-9X`
    const res = await admin.post('/api/users', {
      fullName: qa.name('FE-06 Chủ toạ bị khoá'),
      email: LOCKED_CHAIR_EMAIL,
      password,
      roleId: memberRole.id,
    })
    qa.expectStatus(res, 201)
    user = res.body.user
  } else if (!user.isActive) {
    qa.expectStatus(await admin.put(`/api/users/${user.id}/status`, { isActive: true }), 200)
  }
  return { id: user.id, name: user.fullName, email: LOCKED_CHAIR_EMAIL }
}

const rand = () => Math.random().toString(36).slice(2, 7)
const iso = (ms) => new Date(ms).toISOString().replace(/\.\d{3}Z$/, 'Z')
let slotCounter = Math.floor(Math.random() * 20)
/** A 45-minute slot six weeks from today, between 07:00 and 17:40 Vietnam time, a different one per meeting. */
function slot() {
  slotCounter += 1
  const day = new Date(Date.now() + 42 * 86_400_000 + 7 * 3_600_000).toISOString().slice(0, 10)
  const minutes = 7 * 60 + ((slotCounter * 50) % (10 * 60 + 40))
  const start = Date.parse(`${day}T00:00:00+07:00`) + minutes * 60_000
  return { plannedStartTime: iso(start), plannedEndTime: iso(start + 45 * 60_000) }
}

async function defaultRoster(qa) {
  const u = await users(qa)
  return [
    { userId: u.chair.id, role: 'Chair' },
    { userId: u.secretary.id, role: 'Secretary' },
    { userId: u.member.id, role: 'Member' },
    { userId: u.extra[0].id, role: 'Member' },
  ]
}

const CREATED = []
/** Creates a meeting through the API and drives it to `status` the way the actors would. */
async function createMeeting(qa, title, status, { organizer = 'secretary', sensitivity = 'Normal', participants } = {}) {
  const u = await users(qa)
  const created = await as(qa, organizer).post('/api/meetings', {
    title,
    ...slot(),
    mode: 'InPerson',
    location: 'Phòng họp QA',
    onlineLink: null,
    sensitivity,
    description: 'Cuộc họp do bộ kiểm thử QA FE-06 tạo.',
    participants: participants ?? (await defaultRoster(qa)),
  })
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
    Cancelled: ['cancel'],
  }[status]
  for (const step of path) {
    const [role, body] = {
      submit: [organizer, { action: 'SubmitForApproval', assignedApproverId: u.chair.id }],
      approve: ['chair', { action: 'Approve' }],
      reject: ['chair', { action: 'Reject', reason: 'QA FE-06: cần bổ sung chương trình.' }],
      publish: [organizer, { action: 'Publish' }],
      start: ['chair', { action: 'Start' }],
      end: ['chair', { action: 'End' }],
      cancel: [organizer, { action: 'Cancel', reason: 'QA FE-06: dọn dữ liệu kiểm thử.' }],
    }[step]
    const res = await as(qa, role).post(`/api/meetings/${id}/transitions`, body)
    qa.expectStatus(res, 200)
  }
  const meeting = { id, title, status, organizer }
  CREATED.push(meeting)
  return meeting
}

/** A fresh meeting for a criterion that changes it; `done()` cancels it (or ends it) afterwards. */
async function fresh(qa, key, status, opts) {
  return createMeeting(qa, qa.name(`FE-06 ${key} ${rand()}`), status, opts)
}

/** One meeting per state for read-only criteria, found again by its exact title on later runs. */
const SHARED = {}
async function shared(qa, key, status, opts = {}) {
  const title = qa.name(`FE-06 ${key}`)
  if (SHARED[title]) return SHARED[title]
  const organizer = opts.organizer ?? 'secretary'
  const res = await as(qa, organizer).get(
    `/api/meetings?q=${encodeURIComponent(title)}&status=${status}&pageSize=20`,
  )
  const hit = res.status === 200 ? res.body.items?.find((m) => m.title === title && m.status === status) : undefined
  SHARED[title] = hit ? { id: hit.id, title, status, organizer, reused: true } : await createMeeting(qa, title, status, opts)
  return SHARED[title]
}

/** Leaves nothing open behind a criterion: cancels a meeting not started yet, ends one in progress. */
async function done(qa, meeting) {
  if (!meeting) return
  const got = await as(qa, meeting.organizer).get(`/api/meetings/${meeting.id}`)
  const status = got.body?.status
  if (['Draft', 'WaitingApproval', 'Rejected', 'Approved', 'Published'].includes(status)) {
    await as(qa, meeting.organizer).post(`/api/meetings/${meeting.id}/transitions`, {
      action: 'Cancel',
      reason: 'QA FE-06: dọn dữ liệu kiểm thử.',
    })
  } else if (status === 'InProgress') {
    // The Chair may have changed hands during the criterion: end it as whoever holds the chair now.
    const chairId = got.body.chair?.id ?? got.body.chair?.userId
    const u = await users(qa)
    const role = ['chair', 'secretary', 'member'].find((r) => u[r].id === chairId) ?? 'chair'
    await as(qa, role).post(`/api/meetings/${meeting.id}/transitions`, { action: 'End' })
  }
}

async function roster(qa, role, id) {
  const res = await as(qa, role).get(`/api/meetings/${id}/participants?pageSize=100`)
  qa.expectStatus(res, 200)
  return res.body
}
const rowFor = (r, userIdOrEmail) =>
  r.items.find((i) => i.userId === userIdOrEmail || i.email?.toLowerCase() === String(userIdOrEmail).toLowerCase())

/** Runs every API check, then fails once listing all the wrong ones, so one bad answer does not hide the next. */
function checker(qa) {
  const wrong = []
  return {
    check(label, res, status, code) {
      const ok = res.status === status && (code === undefined || res.body?.code === code)
      if (!ok)
        wrong.push(`${label}: want ${status}${code ? ` ${code}` : ''}, got ${res.status} ${JSON.stringify(res.body).slice(0, 240)}`)
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
// Screen helpers.

async function noSidewaysScroll(qa) {
  const sideways = await qa.page.evaluate(() => document.documentElement.scrollWidth - innerWidth)
  qa.expect(sideways, 'the page scrolls sideways').toBeLessThanOrEqual(0)
}
async function noRawCodes(qa) {
  const text = await qa.page.locator('body').innerText()
  qa.expect(text, 'a raw MSG code is shown to the user').not.toMatch(/MSG\d+/)
}
const toast = (qa, text) => qa.page.locator('.toast-stack').getByText(text, { exact: false })
const dialog = (qa, name) => qa.page.getByRole('dialog', { name })
const panel = (qa) => qa.page.getByRole('region', { name: 'Thành phần tham dự' })
const rowOf = (qa, name) => panel(qa).locator('tbody tr').filter({ hasText: name })
async function press(qa, locator) {
  if (qa.device === 'phone') await locator.tap()
  else await locator.click()
}
/** Opens the meeting's "Thành phần tham dự" tab and waits for its rows. */
async function openRoster(qa, meeting) {
  await qa.goto(`/mgmt/meetings/${meeting.id}?tab=attendees`)
  await qa.expect(qa.page.getByRole('heading', { level: 1, name: meeting.title })).toBeVisible()
  await qa.expect(panel(qa).locator('tbody tr').first()).toBeVisible()
  await qa.expect(panel(qa).locator('tbody[aria-busy="true"]')).toHaveCount(0)
}
async function openMeeting(qa, meeting) {
  await qa.goto(`/mgmt/meetings/${meeting.id}`)
  await qa.expect(qa.page.getByRole('heading', { level: 1, name: meeting.title })).toBeVisible()
}

// ---------------------------------------------------------------------------------------------------------------------

export default {
  ticket: 'SCRUM-193',
  title: 'FE-06 Quản lý thành phần tham dự: xem, thêm, mời khách, đổi vai trò, giao Chủ toạ, gỡ, trả lời lời mời, điểm danh',
  devices: ['desktop'],
  criteria: [
    // ------------------------------------------------------------------------------------- UC-27 + UC-28a internal
    {
      id: 'AC1',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Thư ký xem thành phần và thêm người nội bộ vào cuộc họp Nháp',
      steps: [
        'Đăng nhập thuky@, mở cuộc họp Nháp, tab "Thành phần tham dự"',
        'Bấm "Thêm người dự họp", tìm load42, tick, vai trò Thành viên, bấm "Thêm 1 người"',
      ],
      expected:
        'Bảng có Chủ toạ, Thư ký, Thành viên kèm email và nút Thao tác; người đã có không nằm trong danh sách chọn; toast "Thêm thành phần tham dự thành công." (MSG96); dòng mới vai trò Thành viên, phản hồi Pending; không cuộn ngang',
      async run(qa) {
        const u = await users(qa)
        const meeting = await fresh(qa, 'thêm nội bộ', 'Draft')
        try {
          await openRoster(qa, meeting)
          for (const p of [u.chair, u.secretary, u.member]) await qa.expect(rowOf(qa, p.name)).toContainText(p.email)
          await qa.expect(rowOf(qa, u.chair.name)).toContainText('Chủ toạ')
          await qa.expect(rowOf(qa, u.secretary.name)).toContainText('Thư ký')
          await qa.expect(panel(qa).getByRole('columnheader', { name: 'Thao tác' })).toHaveCount(1)
          await noSidewaysScroll(qa)
          await qa.shot('thanh-phan-nhap')

          await press(qa, panel(qa).getByRole('button', { name: 'Thêm người dự họp' }))
          const add = dialog(qa, 'Thêm thành phần tham dự')
          await qa.expect(add).toBeVisible()
          await add.getByLabel('Người dự họp').fill('thanhvien@')
          await qa.expect(add.getByText(/đã có trong danh sách/)).toBeVisible()
          await add.getByLabel('Người dự họp').fill(u.extra[1].email)
          const option = add.locator('label.pick-row').filter({ hasText: u.extra[1].email }).getByRole('checkbox')
          await qa.expect(option).toBeVisible({ timeout: 15_000 })
          await press(qa, option)
          await qa.expect(add.getByRole('radio', { name: 'Thành viên' })).toBeChecked()
          await qa.shot('chon-nguoi')
          await press(qa, add.getByRole('button', { name: 'Thêm 1 người' }))
          await qa.expect(toast(qa, MSG.MSG96)).toBeVisible()
          await qa.expect(rowOf(qa, u.extra[1].name)).toContainText('Thành viên')
          await noSidewaysScroll(qa)
          await noRawCodes(qa)
          await qa.shot('da-them')

          const r = await roster(qa, 'secretary', meeting.id)
          const added = rowFor(r, u.extra[1].id)
          qa.expect(added?.role, 'added as Member').toBe('Member')
          qa.expect(added?.response, 'new invitation starts Pending (BR-88)').toBe('Pending')
        } finally {
          await done(qa, meeting)
        }
      },
    },

    // ------------------------------------------------------------------------------------------- UC-28a AF-1 guest
    {
      id: 'AC2',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Thư ký mời khách vào cuộc họp Mật: cảnh báo ngay trong form, kiểm tra tên/email, trùng email bị chặn',
      steps: [
        'Mở tab Thành phần của cuộc họp Mật (Nháp), bấm "Mời khách ngoài hệ thống"',
        'Bấm Mời khi để trống; nhập email sai; rồi tên + email @trustcabinet.local hợp lệ, bấm Mời',
        'Mời lại đúng email đó',
        'Gõ tên rồi bấm Huỷ',
      ],
      expected:
        'Cảnh báo "khách dự họp như Thành viên và đọc được tài liệu" không thêm bước xác nhận (BR-182); "Nhập tên khách." / "Nhập email hợp lệ."; toast "Đã mời khách. Thư mời sẽ được gửi tới email của khách." (MSG96), dòng mới chip Khách; email trùng báo lỗi ở ô Email, không thêm; Huỷ khi đang soạn hỏi "Bỏ lời mời đang soạn?"',
      async run(qa) {
        const meeting = await fresh(qa, 'mời khách mật', 'Draft', { sensitivity: 'Secret' })
        const email = `qa-fe06-khach-${rand()}@${DOMAIN}`
        try {
          await openRoster(qa, meeting)
          await press(qa, panel(qa).getByRole('button', { name: 'Mời khách ngoài hệ thống' }))
          const form = dialog(qa, 'Mời khách ngoài hệ thống')
          await qa.expect(form).toBeVisible()
          await qa.expect(form.getByRole('note')).toContainText('khách dự họp như Thành viên và đọc được tài liệu cuộc họp')
          await press(qa, form.getByRole('button', { name: 'Mời' }))
          await qa.expect(form.getByText('Nhập tên khách.')).toBeVisible()
          await qa.expect(form.getByText('Nhập email hợp lệ.')).toBeVisible()
          await form.getByLabel('Tên khách').fill('Khách QA FE-06')
          await form.getByLabel('Email').fill('khong-phai-email')
          await press(qa, form.getByRole('button', { name: 'Mời' }))
          await qa.expect(form.getByText('Nhập email hợp lệ.')).toBeVisible()
          await qa.shot('loi-nhap')
          await form.getByLabel('Email').fill(email)
          await press(qa, form.getByRole('button', { name: 'Mời' }))
          await qa.expect(toast(qa, MSG.MSG96_GUEST)).toBeVisible()
          await qa.expect(rowOf(qa, 'Khách QA FE-06')).toContainText('Khách')
          await qa.expect(rowOf(qa, 'Khách QA FE-06')).toContainText(email)
          await noSidewaysScroll(qa)
          await qa.shot('da-moi-khach')

          await press(qa, panel(qa).getByRole('button', { name: 'Mời khách ngoài hệ thống' }))
          await form.getByLabel('Tên khách').fill('Khách trùng')
          await form.getByLabel('Email').fill(email.toUpperCase())
          await press(qa, form.getByRole('button', { name: 'Mời' }))
          await qa.expect(form.locator('.field-error, .alert-error').first()).toBeVisible()
          await qa.expect(form).toBeVisible()
          await noRawCodes(qa)
          await qa.shot('email-trung')
          await press(qa, form.getByRole('button', { name: 'Huỷ' }))
          await qa.expect(dialog(qa, 'Bỏ lời mời đang soạn?')).toBeVisible()
          await qa.shot('hoi-bo-loi-moi')
          await press(qa, dialog(qa, 'Bỏ lời mời đang soạn?').getByRole('button', { name: 'Bỏ lời mời' }))
          await qa.expect(form).toHaveCount(0)

          const r = await roster(qa, 'secretary', meeting.id)
          const guests = r.items.filter((i) => i.kind === 'Guest')
          qa.expect(guests.length, 'exactly one guest invitation').toBe(1)
          qa.expect(guests[0].role, 'a guest has no meeting role (BR-88/POST-3)').toBeNull()
          qa.expect(guests[0].response).toBe('Pending')
        } finally {
          await done(qa, meeting)
        }
      },
    },

    {
      id: 'AC3',
      role: 'secretary',
      title: 'API thêm/mời: If-Match bắt buộc, danh sách cũ 409 MSG275, người đã có / sai vai trò / email trùng 400 MSG97, thêm Chủ toạ thứ hai bị chặn',
      steps: [
        'POST /participants không If-Match; với revision cũ; với người đã có; role "chair"; role "Chair"',
        'POST /guest-invitations email sai, email trùng người nội bộ, email trùng khách (khác hoa thường)',
      ],
      expected:
        '400 MSG276; 409 MSG275; 400 MSG97; 400 MSG97/MSG101 (vai trò sai); Chủ toạ thứ hai 409 MSG109 (UC-28a EX-5); khách 400 MSG97; không lưu gì (revision không đổi)',
      async run(qa) {
        const u = await users(qa)
        const meeting = await fresh(qa, 'api thêm', 'Draft')
        const api = as(qa, 'secretary')
        const c = checker(qa)
        try {
          const base = `/api/meetings/${meeting.id}`
          const r0 = await roster(qa, 'secretary', meeting.id)
          const now = tag(r0.revision)
          c.check('no If-Match', await api.post(`${base}/participants`, { userIds: [u.extra[1].id], role: 'Member' }), 400, 'MSG276')
          c.check(
            'malformed If-Match',
            await api.post(`${base}/participants`, { userIds: [u.extra[1].id], role: 'Member' }, { ifMatch: 'abc' }),
            400,
            'MSG276',
          )
          c.check(
            'already current',
            await api.post(`${base}/participants`, { userIds: [u.member.id], role: 'Member' }, { ifMatch: now }),
            400,
            'MSG97',
          )
          const badRole = await api.post(`${base}/participants`, { userIds: [u.extra[1].id], role: 'chair' }, { ifMatch: now })
          c.fact('role in wrong case is refused 400', badRole.status === 400 && ['MSG97', 'MSG101'].includes(badRole.body?.code), JSON.stringify(badRole.body).slice(0, 200))
          c.check(
            'second Chair (UC-28a EX-5)',
            await api.post(`${base}/participants`, { userIds: [u.extra[1].id], role: 'Chair' }, { ifMatch: now }),
            409,
            'MSG109',
          )
          c.check(
            'guest: invalid email',
            await api.post(`${base}/guest-invitations`, { invitedName: 'Khách', email: 'sai@' }, { ifMatch: now }),
            400,
            'MSG97',
          )
          c.check(
            'guest: email of an internal participant',
            await api.post(`${base}/guest-invitations`, { invitedName: 'Khách', email: u.member.email }, { ifMatch: now }),
            400,
            'MSG97',
          )
          c.check(
            'guest: empty name',
            await api.post(`${base}/guest-invitations`, { invitedName: '  ', email: `qa-fe06-x-${rand()}@${DOMAIN}` }, { ifMatch: now }),
            400,
            'MSG97',
          )
          const r1 = await roster(qa, 'secretary', meeting.id)
          c.fact('refused requests saved nothing', r1.revision === r0.revision && r1.items.length === r0.items.length)

          const guestEmail = `qa-fe06-api-${rand()}@${DOMAIN}`
          const ok = await api.post(`${base}/guest-invitations`, { invitedName: 'Khách API', email: guestEmail }, { ifMatch: now })
          c.check('guest invited', ok, 201, 'MSG96')
          c.check(
            'stale revision',
            await api.post(`${base}/participants`, { userIds: [u.extra[1].id], role: 'Member' }, { ifMatch: now }),
            409,
            'MSG275',
          )
          const next = tag(ok.body?.revision ?? (await roster(qa, 'secretary', meeting.id)).revision)
          c.check(
            'guest: same email in other case',
            await api.post(`${base}/guest-invitations`, { invitedName: 'Khách 2', email: guestEmail.toUpperCase() }, { ifMatch: next }),
            400,
            'MSG97',
          )
          const many = await api.post(`${base}/participants`, { userIds: [u.extra[1].id, u.extra[2].id], role: 'Secretary' }, { ifMatch: next })
          c.check('two people in one batch as Secretary', many, 201, 'MSG96')
          const r2 = await roster(qa, 'secretary', meeting.id)
          c.fact('both added as Secretary', rowFor(r2, u.extra[1].id)?.role === 'Secretary' && rowFor(r2, u.extra[2].id)?.role === 'Secretary')
          c.done()
        } finally {
          await done(qa, meeting)
        }
      },
    },

    // ------------------------------------------------------------------------------------------------- UC-28b roles
    {
      id: 'AC4',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Thư ký đổi vai trò Thành viên thành Thư ký; API: không đổi gì MSG154, vai trò sai MSG101, hạ Thư ký cuối sau công bố MSG109',
      steps: [
        'Cuộc họp Nháp: bấm "Đổi vai trò" ở dòng load41, chọn Thư ký, Lưu',
        'API: PUT assignments cùng vai trò; role "Boss"; trên cuộc họp đã công bố hạ Thư ký duy nhất xuống Thành viên',
      ],
      expected:
        'Hộp thoại có Chủ toạ/Thư ký/Thành viên (trước công bố, BR-294), toast "Cập nhật vai trò cuộc họp thành công." (MSG100), chip Thư ký; 200 MSG154; 400 MSG101; 409 MSG109',
      async run(qa) {
        const u = await users(qa)
        const meeting = await fresh(qa, 'đổi vai trò', 'Draft')
        const c = checker(qa)
        try {
          await openRoster(qa, meeting)
          await press(qa, rowOf(qa, u.extra[0].name).getByRole('button', { name: `Đổi vai trò của ${u.extra[0].name}` }))
          const d = dialog(qa, 'Đổi vai trò')
          await qa.expect(d).toBeVisible()
          await qa.expect(d.getByRole('button', { name: 'Lưu' })).toBeDisabled()
          await press(qa, d.getByRole('radio', { name: 'Thư ký' }))
          await qa.shot('chon-thu-ky')
          await press(qa, d.getByRole('button', { name: 'Lưu' }))
          await qa.expect(toast(qa, MSG.MSG100)).toBeVisible()
          await qa.expect(rowOf(qa, u.extra[0].name)).toContainText('Thư ký')
          await noSidewaysScroll(qa)
          await qa.shot('da-doi')
          if (qa.device === 'phone') return

          const api = as(qa, 'secretary')
          const base = `/api/meetings/${meeting.id}`
          let r = await roster(qa, 'secretary', meeting.id)
          const target = rowFor(r, u.extra[0].id)
          c.check(
            'same role again (no-op)',
            await api.put(`${base}/participants/assignments`, { changes: [{ participantId: target.id, role: 'Secretary' }] }, { ifMatch: tag(r.revision) }),
            200,
            'MSG154',
          )
          r = await roster(qa, 'secretary', meeting.id)
          c.check(
            'invalid role',
            await api.put(`${base}/participants/assignments`, { changes: [{ participantId: target.id, role: 'Boss' }] }, { ifMatch: tag(r.revision) }),
            400,
            'MSG101',
          )
          const pub = await shared(qa, 'đã công bố', 'Published')
          const pr = await roster(qa, 'secretary', pub.id)
          const sec = pr.items.filter((i) => i.role === 'Secretary')
          c.fact('shared Published meeting has exactly one Secretary', sec.length === 1, `${sec.length}`)
          if (sec.length === 1)
            c.check(
              'demote the last Secretary after publication (BR-122)',
              await api.put(`/api/meetings/${pub.id}/participants/assignments`, { changes: [{ participantId: sec[0].id, role: 'Member' }] }, { ifMatch: tag(pr.revision) }),
              409,
              'MSG109',
            )
          c.done()
        } finally {
          await done(qa, meeting)
        }
      },
    },

    {
      id: 'AC5',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Trước công bố, Thư ký thay Chủ toạ: hỏi lại, nêu tên người thành Thành viên, đổi cả hai trong một lần',
      steps: [
        'Cuộc họp Nháp: "Đổi vai trò" ở dòng Lê An, chọn Chủ toạ',
        'Bấm "Giao Chủ toạ", hộp xác nhận bấm Huỷ, rồi bấm lại và xác nhận',
      ],
      expected:
        'Dòng Chủ toạ ghi "<Chủ toạ hiện tại> sẽ thành Thành viên"; hộp "Giao Chủ toạ cho Lê An?" nêu Chủ toạ cũ; Huỷ không đổi gì; xác nhận: toast MSG100, Lê An là Chủ toạ, Chủ toạ cũ là Thành viên (BR-294, UC-28b AF-1)',
      async run(qa) {
        const u = await users(qa)
        const meeting = await fresh(qa, 'thay chủ toạ nháp', 'Draft')
        try {
          await openRoster(qa, meeting)
          await qa.expect(rowOf(qa, u.chair.name).getByRole('button')).toHaveCount(0)
          await press(qa, rowOf(qa, u.member.name).getByRole('button', { name: `Đổi vai trò của ${u.member.name}` }))
          const d = dialog(qa, 'Đổi vai trò')
          await press(qa, d.getByRole('radio', { name: 'Chủ toạ' }))
          await qa.expect(d.getByText(`${u.chair.name} sẽ thành Thành viên.`)).toBeVisible()
          await qa.shot('chon-chu-toa')
          await press(qa, d.getByRole('button', { name: 'Giao Chủ toạ' }))
          const confirm = dialog(qa, `Giao Chủ toạ cho ${u.member.name}?`)
          await qa.expect(confirm).toContainText(`${u.chair.name} sẽ thành Thành viên`)
          await qa.shot('xac-nhan')
          await press(qa, confirm.getByRole('button', { name: 'Huỷ' }))
          await qa.expect(confirm).toHaveCount(0)
          await qa.expect(d.getByRole('radio', { name: 'Chủ toạ' })).toBeChecked()
          let r = await roster(qa, 'secretary', meeting.id)
          qa.expect(rowFor(r, u.chair.id)?.role, 'cancel changed nothing').toBe('Chair')
          await press(qa, d.getByRole('button', { name: 'Giao Chủ toạ' }))
          await press(qa, confirm.getByRole('button', { name: 'Giao Chủ toạ' }))
          await qa.expect(toast(qa, MSG.MSG100)).toBeVisible()
          await qa.expect(rowOf(qa, u.member.name)).toContainText('Chủ toạ')
          await qa.expect(rowOf(qa, u.chair.name)).toContainText('Thành viên')
          await noSidewaysScroll(qa)
          await qa.shot('da-thay')
          r = await roster(qa, 'secretary', meeting.id)
          qa.expect(r.items.filter((i) => i.role === 'Chair').map((i) => i.userId), 'exactly one Chair, the new one').toEqual([u.member.id])
          qa.expect(rowFor(r, u.chair.id)?.role).toBe('Member')
        } finally {
          await done(qa, meeting)
        }
      },
    },

    {
      id: 'AC6',
      role: 'secretary',
      title: 'Sau công bố, Thư ký không thay được Chủ toạ còn hoạt động: không có lựa chọn Chủ toạ, API 409 MSG256',
      steps: ['Cuộc họp Đã công bố: "Đổi vai trò" ở dòng Lê An', 'API: PUT assignments thay Chủ toạ có xác nhận'],
      expected: 'Hộp thoại chỉ có Thư ký và Thành viên; dòng Chủ toạ không có nút; API 409 MSG256, Chủ toạ giữ nguyên (BR-126)',
      async run(qa) {
        const u = await users(qa)
        const pub = await shared(qa, 'đã công bố', 'Published')
        await openRoster(qa, pub)
        await qa.expect(rowOf(qa, u.chair.name).getByRole('button')).toHaveCount(0)
        await press(qa, rowOf(qa, u.member.name).getByRole('button', { name: `Đổi vai trò của ${u.member.name}` }))
        const d = dialog(qa, 'Đổi vai trò')
        await qa.expect(d.getByRole('radio', { name: 'Thành viên' })).toBeVisible()
        await qa.expect(d.getByRole('radio', { name: 'Chủ toạ' })).toHaveCount(0)
        await qa.shot('khong-co-chu-toa')
        await press(qa, d.getByRole('button', { name: 'Huỷ' }))
        const r = await roster(qa, 'secretary', pub.id)
        const chair = rowFor(r, u.chair.id)
        const incoming = rowFor(r, u.member.id)
        const res = await as(qa, 'secretary').put(
          `/api/meetings/${pub.id}/participants/assignments`,
          {
            changes: [
              { participantId: chair.id, role: 'Member' },
              { participantId: incoming.id, role: 'Chair' },
            ],
            chairReplacement: { outgoingParticipantId: chair.id, incomingParticipantId: incoming.id, confirmed: true },
          },
          { ifMatch: tag(r.revision) },
        )
        qa.expect(res.status).toBe(409)
        qa.expect(res.body?.code).toBe('MSG256')
        qa.expect(rowFor(await roster(qa, 'secretary', pub.id), u.chair.id)?.role).toBe('Chair')
      },
    },

    // ---------------------------------------------------------------------------------------------- UC-73 hand over
    {
      id: 'AC7',
      role: 'chair',
      devices: ['desktop', 'phone'],
      title: 'Chủ toạ giao vai trò Chủ toạ cho một Thành viên ngay trên dòng của người đó (UC-73)',
      steps: [
        'Đăng nhập chutoa@, cuộc họp Đã công bố, tab Thành phần',
        '"Đổi vai trò" ở dòng Thư ký: không có Chủ toạ',
        '"Đổi vai trò" ở dòng Lê An, chọn Chủ toạ, "Giao Chủ toạ", Huỷ ở hộp xác nhận, rồi bấm lại và xác nhận',
      ],
      expected:
        'Dòng Chủ toạ ghi "Bạn sẽ thành Thành viên"; hộp "Giao Chủ toạ cho Lê An?" — "Bạn sẽ thành Thành viên và không còn điều hành cuộc họp."; Huỷ giữ hộp thoại; xác nhận: toast "Giao vai trò Chủ toạ thành công." (MSG257), Lê An Chủ toạ, Chủ toạ cũ Thành viên và hết nút quản lý',
      async run(qa) {
        const u = await users(qa)
        const meeting = await fresh(qa, 'giao chủ toạ', 'Published')
        try {
          await openRoster(qa, meeting)
          await press(qa, rowOf(qa, u.secretary.name).getByRole('button', { name: `Đổi vai trò của ${u.secretary.name}` }))
          const d = dialog(qa, 'Đổi vai trò')
          await qa.expect(d.getByRole('radio', { name: 'Thành viên' })).toBeVisible()
          await qa.expect(d.getByRole('radio', { name: 'Chủ toạ' }), 'the Secretary cannot take the chair (BR-295)').toHaveCount(0)
          await press(qa, d.getByRole('button', { name: 'Huỷ' }))
          await press(qa, rowOf(qa, u.member.name).getByRole('button', { name: `Đổi vai trò của ${u.member.name}` }))
          await press(qa, d.getByRole('radio', { name: 'Chủ toạ' }))
          await qa.expect(d.getByText('Bạn sẽ thành Thành viên.')).toBeVisible()
          await qa.shot('chon-chu-toa')
          await press(qa, d.getByRole('button', { name: 'Giao Chủ toạ' }))
          const confirm = dialog(qa, `Giao Chủ toạ cho ${u.member.name}?`)
          await qa.expect(confirm).toContainText('Bạn sẽ thành Thành viên và không còn điều hành cuộc họp.')
          await qa.shot('xac-nhan')
          await press(qa, confirm.getByRole('button', { name: 'Huỷ' }))
          await qa.expect(d).toBeVisible()
          await press(qa, d.getByRole('button', { name: 'Giao Chủ toạ' }))
          await press(qa, confirm.getByRole('button', { name: 'Giao Chủ toạ' }))
          await qa.expect(toast(qa, MSG.MSG257)).toBeVisible()
          await qa.expect(rowOf(qa, u.member.name)).toContainText('Chủ toạ')
          await qa.expect(rowOf(qa, u.chair.name)).toContainText('Thành viên')
          await qa.expect(panel(qa).getByRole('button', { name: 'Thêm người dự họp' })).toHaveCount(0)
          await noSidewaysScroll(qa)
          await noRawCodes(qa)
          await qa.shot('da-giao')
        } finally {
          await done(qa, meeting)
        }
      },
    },

    {
      id: 'AC8',
      role: 'chair',
      title: 'API giao Chủ toạ: người nhận chỉ là Thành viên khác mình, Thư ký 400 MSG259, Thư ký gọi / Nháp / Chủ toạ cũ 409 MSG260',
      steps: [
        'GET chair-handover-candidates; POST chair-handover cho Thư ký; Thư ký gọi POST; Chủ toạ gọi trên cuộc họp Nháp',
        'POST không If-Match; giao cho Lê An; Chủ toạ cũ gọi lại',
      ],
      expected:
        'candidates chỉ có Thành viên, không có mình/Thư ký; 400 MSG259; 409 MSG260; 409 MSG260; 400 MSG276; 200 MSG257 một Chủ toạ, Thư ký giữ nguyên; 409 MSG260',
      async run(qa) {
        const u = await users(qa)
        const meeting = await fresh(qa, 'api giao chủ toạ', 'Published')
        const draft = await fresh(qa, 'api giao chủ toạ nháp', 'Draft')
        const c = checker(qa)
        try {
          const base = `/api/meetings/${meeting.id}`
          const chair = as(qa, 'chair')
          const cand = await chair.get(`${base}/participants/chair-handover-candidates`)
          c.check('candidates', cand, 200)
          const ids = (cand.body?.items ?? []).map((i) => i.userId)
          c.fact(
            'candidates are the active Members other than the caller',
            ids.includes(u.member.id) && ids.includes(u.extra[0].id) && !ids.includes(u.chair.id) && !ids.includes(u.secretary.id),
            JSON.stringify(ids),
          )
          let r = await roster(qa, 'chair', meeting.id)
          const sec = rowFor(r, u.secretary.id)
          const mem = rowFor(r, u.member.id)
          c.check(
            'to the Secretary',
            await chair.post(`${base}/participants/chair-handover`, { newChairParticipantId: sec.id, confirmed: true }, { ifMatch: tag(r.revision) }),
            400,
            'MSG259',
          )
          c.check(
            'Secretary calls it',
            await as(qa, 'secretary').post(`${base}/participants/chair-handover`, { newChairParticipantId: mem.id, confirmed: true }, { ifMatch: tag(r.revision) }),
            409,
            'MSG260',
          )
          const dr = await roster(qa, 'chair', draft.id)
          c.check(
            'before publication',
            await chair.post(`/api/meetings/${draft.id}/participants/chair-handover`, { newChairParticipantId: rowFor(dr, u.member.id).id, confirmed: true }, { ifMatch: tag(dr.revision) }),
            409,
            'MSG260',
          )
          c.check(
            'no If-Match',
            await chair.post(`${base}/participants/chair-handover`, { newChairParticipantId: mem.id, confirmed: true }),
            400,
            'MSG276',
          )
          c.check(
            'confirmed missing',
            await chair.post(`${base}/participants/chair-handover`, { newChairParticipantId: mem.id }, { ifMatch: tag(r.revision) }),
            400,
            'MSG101',
          )
          c.check(
            'hand over',
            await chair.post(`${base}/participants/chair-handover`, { newChairParticipantId: mem.id, confirmed: true }, { ifMatch: tag(r.revision) }),
            200,
            'MSG257',
          )
          r = await roster(qa, 'chair', meeting.id)
          c.fact('one Chair, the new one', JSON.stringify(r.items.filter((i) => i.role === 'Chair').map((i) => i.userId)) === JSON.stringify([u.member.id]))
          c.fact('former Chair is a Member', rowFor(r, u.chair.id)?.role === 'Member')
          c.fact('Secretary unchanged', rowFor(r, u.secretary.id)?.role === 'Secretary')
          c.check(
            'former Chair tries again',
            await chair.post(`${base}/participants/chair-handover`, { newChairParticipantId: rowFor(r, u.extra[0].id).id, confirmed: true }, { ifMatch: tag(r.revision) }),
            409,
            'MSG260',
          )
          const notes = await as(qa, 'member').get('/api/notifications?pageSize=20')
          const items = notes.body?.items ?? notes.body ?? []
          c.fact(
            'new Chair gets an in-app notification (POST-3)',
            Array.isArray(items) && items.some((n) => JSON.stringify(n).includes(meeting.title)),
            `status ${notes.status}`,
          )
          c.done()
        } finally {
          await done(qa, meeting)
          await done(qa, draft)
        }
      },
    },

    {
      id: 'AC9',
      role: 'secretary',
      title: 'Sau công bố, Thư ký thay Chủ toạ có tài khoản bị khoá (UC-28b AF-1, BR-126 ngoại lệ)',
      steps: [
        'Cuộc họp Đã công bố do một tài khoản QA làm Chủ toạ; quản trị khoá tài khoản đó',
        'Thư ký: "Đổi vai trò" ở dòng Lê An, chọn Chủ toạ, Giao Chủ toạ, xác nhận',
      ],
      expected:
        'Có lựa chọn Chủ toạ, ghi "<Chủ toạ bị khoá> sẽ thành Thành viên"; dòng Chủ toạ có chip "Đã vô hiệu hoá"; toast MSG100; Lê An Chủ toạ',
      async run(qa) {
        const u = await users(qa)
        const locked = await lockedChairUser(qa)
        const meeting = await fresh(qa, 'chủ toạ bị khoá', 'Published', {
          participants: [
            { userId: locked.id, role: 'Chair' },
            { userId: u.secretary.id, role: 'Secretary' },
            { userId: u.member.id, role: 'Member' },
          ],
        })
        try {
          qa.expectStatus(await as(qa, 'admin').put(`/api/users/${locked.id}/status`, { isActive: false }), 200)
          await openRoster(qa, meeting)
          await qa.expect(rowOf(qa, locked.name)).toContainText('Đã vô hiệu hoá')
          await press(qa, rowOf(qa, u.member.name).getByRole('button', { name: `Đổi vai trò của ${u.member.name}` }))
          const d = dialog(qa, 'Đổi vai trò')
          await press(qa, d.getByRole('radio', { name: 'Chủ toạ' }))
          await qa.expect(d.getByText(`${locked.name} sẽ thành Thành viên.`)).toBeVisible()
          await qa.shot('chon-chu-toa')
          await press(qa, d.getByRole('button', { name: 'Giao Chủ toạ' }))
          await press(qa, dialog(qa, `Giao Chủ toạ cho ${u.member.name}?`).getByRole('button', { name: 'Giao Chủ toạ' }))
          await qa.expect(toast(qa, MSG.MSG100)).toBeVisible()
          await qa.expect(rowOf(qa, u.member.name)).toContainText('Chủ toạ')
          await qa.shot('da-thay')
        } finally {
          await as(qa, 'admin').put(`/api/users/${locked.id}/status`, { isActive: true })
          await done(qa, meeting)
        }
      },
    },

    // ---------------------------------------------------------------------------------------------- UC-28c remove
    {
      id: 'AC10',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Thư ký gỡ một Thành viên và thu hồi lời mời khách, có xác nhận',
      steps: ['Cuộc họp Nháp có một khách: bấm thùng rác ở dòng load41, xác nhận', 'Bấm "Thu hồi lời mời" ở dòng khách, xác nhận'],
      expected:
        '"Gỡ khỏi cuộc họp?" → toast "Gỡ thành phần tham dự thành công." (MSG103), dòng biến mất; "Thu hồi lời mời khách?" → toast "Đã thu hồi lời mời khách.", dòng khách biến mất; dòng Chủ toạ không có nút gỡ',
      async run(qa) {
        const u = await users(qa)
        const meeting = await fresh(qa, 'gỡ thành phần', 'Draft')
        try {
          const r = await roster(qa, 'secretary', meeting.id)
          qa.expectStatus(
            await as(qa, 'secretary').post(
              `/api/meetings/${meeting.id}/guest-invitations`,
              { invitedName: 'Khách QA thu hồi', email: `qa-fe06-thuhoi-${rand()}@${DOMAIN}` },
              { ifMatch: tag(r.revision) },
            ),
            201,
          )
          await openRoster(qa, meeting)
          await qa.expect(rowOf(qa, u.chair.name).getByRole('button')).toHaveCount(0)
          await press(qa, rowOf(qa, u.extra[0].name).getByRole('button', { name: `Gỡ ${u.extra[0].name}` }))
          const confirm = dialog(qa, 'Gỡ khỏi cuộc họp?')
          await qa.expect(confirm).toContainText(`${u.extra[0].name} không còn xem được cuộc họp này`)
          await qa.shot('xac-nhan-go')
          await press(qa, confirm.getByRole('button', { name: 'Gỡ khỏi cuộc họp' }))
          await qa.expect(toast(qa, MSG.MSG103)).toBeVisible()
          await qa.expect(rowOf(qa, u.extra[0].name)).toHaveCount(0)
          await press(qa, rowOf(qa, 'Khách QA thu hồi').getByRole('button', { name: 'Thu hồi lời mời của Khách QA thu hồi' }))
          const revoke = dialog(qa, 'Thu hồi lời mời khách?')
          await qa.expect(revoke).toBeVisible()
          await qa.shot('xac-nhan-thu-hoi')
          await press(qa, revoke.getByRole('button', { name: 'Thu hồi lời mời' }))
          await qa.expect(toast(qa, MSG.MSG103_GUEST)).toBeVisible()
          await qa.expect(rowOf(qa, 'Khách QA thu hồi')).toHaveCount(0)
          await noSidewaysScroll(qa)
          await qa.shot('da-go')
        } finally {
          await done(qa, meeting)
        }
      },
    },

    {
      id: 'AC11',
      role: 'secretary',
      title: 'API gỡ: không gỡ Chủ toạ, không gỡ Thư ký cuối sau công bố (409 MSG109), Nháp được gỡ Thư ký duy nhất, người bị gỡ mất quyền đọc (404)',
      steps: [
        'DELETE Chủ toạ (Nháp); DELETE id không tồn tại; DELETE revision cũ',
        'Cuộc họp Đã công bố: DELETE Thư ký duy nhất; DELETE Lê An rồi Lê An mở cuộc họp',
        'Cuộc họp Nháp: DELETE Thư ký duy nhất',
      ],
      expected: '409 MSG109; 404; 409 MSG275; 409 MSG109; 200 MSG103 rồi Lê An nhận 404 (chi tiết và thành phần); Nháp 200 MSG103 (BR-122)',
      async run(qa) {
        const u = await users(qa)
        const draft = await fresh(qa, 'api gỡ nháp', 'Draft')
        const pub = await fresh(qa, 'api gỡ công bố', 'Published')
        const api = as(qa, 'secretary')
        const c = checker(qa)
        try {
          let r = await roster(qa, 'secretary', draft.id)
          c.check('remove the Chair', await api.delete(`/api/meetings/${draft.id}/participants/${rowFor(r, u.chair.id).id}`, { ifMatch: tag(r.revision) }), 409, 'MSG109')
          c.check(
            'unknown participant',
            await api.delete(`/api/meetings/${draft.id}/participants/01a00000-0000-7000-8000-000000000000`, { ifMatch: tag(r.revision) }),
            404,
          )
          let p = await roster(qa, 'secretary', pub.id)
          c.check(
            'remove the last Secretary after publication',
            await api.delete(`/api/meetings/${pub.id}/participants/${rowFor(p, u.secretary.id).id}`, { ifMatch: tag(p.revision) }),
            409,
            'MSG109',
          )
          const before = await as(qa, 'member').get(`/api/meetings/${pub.id}`)
          c.check('member reads before removal', before, 200)
          c.check('remove Lê An', await api.delete(`/api/meetings/${pub.id}/participants/${rowFor(p, u.member.id).id}`, { ifMatch: tag(p.revision) }), 200, 'MSG103')
          c.check('stale revision', await api.delete(`/api/meetings/${pub.id}/participants/${rowFor(p, u.extra[0].id).id}`, { ifMatch: tag(p.revision) }), 409, 'MSG275')
          c.check('removed member: meeting', await as(qa, 'member').get(`/api/meetings/${pub.id}`), 404)
          c.check('removed member: roster', await as(qa, 'member').get(`/api/meetings/${pub.id}/participants`), 404)
          p = await roster(qa, 'secretary', pub.id)
          c.fact('removed member is not listed', !rowFor(p, u.member.id))
          r = await roster(qa, 'secretary', draft.id)
          c.check('remove the only Secretary of a Draft', await api.delete(`/api/meetings/${draft.id}/participants/${rowFor(r, u.secretary.id).id}`, { ifMatch: tag(r.revision) }), 200, 'MSG103')
          c.done()
        } finally {
          await done(qa, pub)
          // The organizer still reads the Draft after leaving its roster, so it can be cancelled.
          await done(qa, draft)
        }
      },
    },

    // -------------------------------------------------------------------------------------------- statuses (BR-80)
    {
      id: 'AC12',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Thành phần khoá ở Chờ duyệt, Đã kết thúc, Đã huỷ (không nút, API 409 MSG98); sửa được ở Bị từ chối, Đã duyệt, Đang họp',
      steps: [
        'Thư ký mở tab Thành phần của cuộc họp Chờ duyệt, Đã kết thúc, Đã huỷ',
        'API: POST /participants ở từng trạng thái; ở Bị từ chối / Đã duyệt / Đang họp thêm rồi gỡ lại',
      ],
      expected:
        'Không có nút Thêm/Mời và cột Thao tác; context canManageParticipants=false; 409 MSG98. Ba trạng thái còn lại: 201 MSG96, gỡ 200 MSG103',
      async run(qa) {
        const u = await users(qa)
        const c = checker(qa)
        const locked = {
          'Chờ duyệt': await shared(qa, 'chờ duyệt', 'WaitingApproval'),
          'Đã kết thúc': await shared(qa, 'đã kết thúc', 'Ended'),
          'Đã huỷ': await shared(qa, 'đã huỷ', 'Cancelled'),
        }
        for (const [label, meeting] of Object.entries(locked)) {
          await openRoster(qa, meeting)
          await qa.expect(panel(qa).getByRole('button', { name: 'Thêm người dự họp' }), label).toHaveCount(0)
          await qa.expect(panel(qa).getByRole('button', { name: 'Mời khách ngoài hệ thống' }), label).toHaveCount(0)
          await qa.expect(panel(qa).getByRole('columnheader', { name: 'Thao tác' }), label).toHaveCount(0)
          await noSidewaysScroll(qa)
          await qa.shot(`khoa-${label}`)
          if (qa.device === 'phone') continue
          const ctx = await as(qa, 'secretary').get(`/api/meetings/${meeting.id}/participant-context`)
          c.fact(`${label}: canManageParticipants false`, ctx.body?.permissions?.canManageParticipants === false, JSON.stringify(ctx.body))
          const r = await roster(qa, 'secretary', meeting.id)
          c.check(
            `${label}: add`,
            await as(qa, 'secretary').post(`/api/meetings/${meeting.id}/participants`, { userIds: [u.extra[3].id], role: 'Member' }, { ifMatch: tag(r.revision) }),
            409,
            'MSG98',
          )
        }
        if (qa.device === 'desktop') {
          const open = {
            'Bị từ chối': await shared(qa, 'bị từ chối', 'Rejected'),
            'Đã duyệt': await shared(qa, 'đã duyệt', 'Approved'),
            'Đang họp': await shared(qa, 'đang họp', 'InProgress'),
          }
          for (const [label, meeting] of Object.entries(open)) {
            const r = await roster(qa, 'secretary', meeting.id)
            const add = await as(qa, 'secretary').post(`/api/meetings/${meeting.id}/participants`, { userIds: [u.extra[3].id], role: 'Member' }, { ifMatch: tag(r.revision) })
            c.check(`${label}: add`, add, 201, 'MSG96')
            if (add.status === 201) {
              const r2 = await roster(qa, 'secretary', meeting.id)
              c.check(
                `${label}: remove again`,
                await as(qa, 'secretary').delete(`/api/meetings/${meeting.id}/participants/${rowFor(r2, u.extra[3].id).id}`, { ifMatch: tag(r2.revision) }),
                200,
                'MSG103',
              )
            }
          }
        }
        c.done()
      },
    },

    // ------------------------------------------------------------------------------------- read-only roles (UC-27)
    {
      id: 'AC13',
      role: 'member',
      devices: ['desktop', 'phone'],
      title: 'Thành viên xem và tìm trong danh sách thành phần, không có nút quản lý; API quản lý 403 MSG85',
      steps: [
        'Đăng nhập thanhvien@, cuộc họp Đã công bố, tab Thành phần',
        'Tìm "chutoa"; tìm "khongcoai"; bấm "Xoá tìm kiếm"',
        'API: POST /participants, PUT assignments, DELETE, GET editor-options',
      ],
      expected:
        'Thấy tên, email, vai trò, dòng của mình "(Tôi)"; không có nút Thêm/Mời/Thao tác; tìm theo email lọc đúng; không khớp: "Không có thành phần nào khớp “khongcoai”."; API 403 MSG85, không có cột điểm danh trước khi họp',
      async run(qa) {
        const u = await users(qa)
        const pub = await shared(qa, 'đã công bố', 'Published')
        await openRoster(qa, pub)
        await qa.expect(rowOf(qa, u.member.name)).toContainText('(Tôi)')
        await qa.expect(rowOf(qa, u.chair.name)).toContainText(u.chair.email)
        await qa.expect(panel(qa).getByRole('button', { name: 'Thêm người dự họp' })).toHaveCount(0)
        await qa.expect(panel(qa).getByRole('button', { name: 'Mời khách ngoài hệ thống' })).toHaveCount(0)
        await qa.expect(panel(qa).getByRole('columnheader', { name: 'Thao tác' })).toHaveCount(0)
        await qa.expect(panel(qa).getByRole('columnheader', { name: 'Điểm danh' })).toHaveCount(0)
        await qa.expect(panel(qa).locator('tbody').getByRole('button')).toHaveCount(0)
        await noSidewaysScroll(qa)
        await qa.shot('thanh-vien-xem')
        const search = panel(qa).getByRole('searchbox', { name: 'Tìm thành phần' })
        await search.fill('chutoa')
        await qa.expect(panel(qa).locator('tbody tr')).toHaveCount(1)
        await qa.expect(panel(qa).locator('tbody tr').first()).toContainText(u.chair.name)
        await qa.shot('tim-email')
        await search.fill('khongcoai')
        await qa.expect(panel(qa).getByText('Không có thành phần nào khớp “khongcoai”.')).toBeVisible()
        await qa.shot('khong-khop')
        await press(qa, panel(qa).getByRole('button', { name: 'Xoá tìm kiếm' }))
        await qa.expect(rowOf(qa, u.secretary.name)).toBeVisible()
        if (qa.device === 'phone') return

        const c = checker(qa)
        const api = as(qa, 'member')
        const r = await roster(qa, 'member', pub.id)
        const base = `/api/meetings/${pub.id}`
        c.check('member: add', await api.post(`${base}/participants`, { userIds: [u.extra[3].id], role: 'Member' }, { ifMatch: tag(r.revision) }), 403, 'MSG85')
        c.check('member: invite guest', await api.post(`${base}/guest-invitations`, { invitedName: 'X', email: `qa-fe06-y-${rand()}@${DOMAIN}` }, { ifMatch: tag(r.revision) }), 403, 'MSG85')
        c.check('member: change role', await api.put(`${base}/participants/assignments`, { changes: [{ participantId: rowFor(r, u.extra[0].id).id, role: 'Secretary' }] }, { ifMatch: tag(r.revision) }), 403, 'MSG85')
        c.check('member: remove', await api.delete(`${base}/participants/${rowFor(r, u.extra[0].id).id}`, { ifMatch: tag(r.revision) }), 403, 'MSG85')
        c.check('member: editor-options', await api.get(`${base}/participants/editor-options`), 403, 'MSG85')
        const ctx = await api.get(`${base}/participant-context`)
        c.fact('member context: no manage, no replace', ctx.body?.permissions?.canManageParticipants === false && ctx.body?.permissions?.canReplaceChair === false && ctx.body?.myRole === 'Member', JSON.stringify(ctx.body))
        c.done()
      },
    },

    {
      id: 'AC14',
      role: 'admin',
      title: 'Người tổ chức không tham dự đọc được nhưng không quản lý được (403 MSG85); người ngoài và Thành viên trước công bố nhận 404; chưa đăng nhập 401',
      steps: [
        'quantri@ tạo cuộc họp không có mình trong thành phần, công bố; mở tab Thành phần',
        'API: quantri@ POST /participants; quantri@ đọc thành phần cuộc họp của người khác',
        'thanhvien@ đọc thành phần cuộc họp Chờ duyệt; không token đọc thành phần',
      ],
      expected: 'Bảng hiện, không có nút quản lý; 403 MSG85 (BR-79); 404 rỗng (BR-121); 404; 401',
      async run(qa) {
        const u = await users(qa)
        const own = await shared(qa, 'người tổ chức ngoài', 'Published', { organizer: 'admin' })
        await openRoster(qa, own)
        await qa.expect(rowOf(qa, u.chair.name)).toBeVisible()
        await qa.expect(panel(qa).getByRole('button', { name: 'Thêm người dự họp' })).toHaveCount(0)
        await qa.expect(panel(qa).getByRole('columnheader', { name: 'Thao tác' })).toHaveCount(0)
        await qa.shot('nguoi-to-chuc-xem')
        const c = checker(qa)
        const admin = as(qa, 'admin')
        const r = await roster(qa, 'admin', own.id)
        c.check('organizer outside the roster: add', await admin.post(`/api/meetings/${own.id}/participants`, { userIds: [u.extra[3].id], role: 'Member' }, { ifMatch: tag(r.revision) }), 403, 'MSG85')
        const other = await shared(qa, 'đã công bố', 'Published')
        const out = await admin.get(`/api/meetings/${other.id}/participants`)
        c.check('outsider: roster', out, 404)
        c.fact('outsider: 404 has no body', !out.body || (typeof out.body === 'object' && !out.body.items), JSON.stringify(out.body).slice(0, 120))
        c.check('outsider: context', await admin.get(`/api/meetings/${other.id}/participant-context`), 404)
        const waiting = await shared(qa, 'chờ duyệt', 'WaitingApproval')
        c.check('member before publication: roster', await as(qa, 'member').get(`/api/meetings/${waiting.id}/participants`), 404)
        c.check('anonymous: roster', await as(qa, 'anon').get(`/api/meetings/${other.id}/participants`), 401)
        await qa.goto(`/mgmt/meetings/${other.id}?tab=attendees`)
        await qa.expect(qa.page.getByRole('heading', { name: other.title })).toHaveCount(0)
        await qa.shot('nguoi-ngoai-mo-cuoc-hop')
        c.done()
      },
    },

    // ------------------------------------------------------------------------------------------ UC-29 / UC-30 RSVP
    {
      id: 'AC15',
      role: 'member',
      devices: ['desktop', 'phone'],
      title: 'Thành viên xác nhận Tham dự ở đầu trang chi tiết; nhãn hiện cạnh tên; trả lời một lần',
      steps: [
        'Đăng nhập thanhvien@, mở cuộc họp Đã công bố chưa trả lời',
        'Bấm "Tham dự"; mở tab Thành phần',
        'API: gửi lại Accepted; gửi Declined',
      ],
      expected:
        '"Bạn có tham dự cuộc họp này?" với Tham dự / Không tham dự; toast "Đã ghi nhận phản hồi lời mời." (MSG105); câu hỏi biến mất, chip "Đã xác nhận tham dự" cạnh tiêu đề và cạnh tên mình; Chủ toạ và người chưa trả lời không có nhãn, không có bộ đếm; 200 MSG154; 409 MSG285',
      async run(qa) {
        const u = await users(qa)
        const meeting = await fresh(qa, 'trả lời tham dự', 'Published')
        try {
          await openMeeting(qa, meeting)
          const ask = qa.page.getByRole('region', { name: 'Bạn có tham dự cuộc họp này?' })
          await qa.expect(ask).toBeVisible()
          await qa.expect(ask.getByRole('button', { name: 'Không tham dự' })).toBeVisible()
          await noSidewaysScroll(qa)
          await qa.shot('cau-hoi')
          await press(qa, ask.getByRole('button', { name: 'Tham dự', exact: true }))
          await qa.expect(toast(qa, MSG.MSG105)).toBeVisible()
          await qa.expect(ask).toHaveCount(0)
          await qa.expect(qa.page.locator('.record-title .rsvp-chip')).toHaveText('Đã xác nhận tham dự')
          await qa.shot('da-tra-loi')
          await press(qa, qa.page.getByRole('tab', { name: 'Thành phần tham dự' }))
          await qa.expect(rowOf(qa, u.member.name)).toContainText('Đã xác nhận tham dự')
          await qa.expect(rowOf(qa, u.chair.name).locator('.rsvp-chip')).toHaveCount(0)
          await qa.expect(rowOf(qa, u.secretary.name).locator('.rsvp-chip')).toHaveCount(0)
          await qa.expect(panel(qa).getByText(/Chờ phản hồi|Đã chấp nhận: \d|Tổng/)).toHaveCount(0)
          await noSidewaysScroll(qa)
          await qa.shot('nhan-canh-ten')
          if (qa.device === 'phone') return
          const c = checker(qa)
          const api = as(qa, 'member')
          const first = (await api.get(`/api/meetings/${meeting.id}/invitation-response`)).body
          c.check('same answer again', await api.put(`/api/meetings/${meeting.id}/invitation-response`, { response: 'Accepted' }), 200, 'MSG154')
          c.check('different answer', await api.put(`/api/meetings/${meeting.id}/invitation-response`, { response: 'Declined' }), 409, 'MSG285')
          const after = (await api.get(`/api/meetings/${meeting.id}/invitation-response`)).body
          c.fact('answer and time unchanged', after?.response === 'Accepted' && after?.respondedAt === first?.respondedAt && after?.canRespond === false, JSON.stringify(after))
          c.done()
        } finally {
          await done(qa, meeting)
        }
      },
    },

    {
      id: 'AC16',
      role: 'member',
      devices: ['desktop', 'phone'],
      title: 'Thành viên chọn Không tham dự: hỏi lại trước, "Quay lại" không gửi gì; sau đó cuộc họp ẩn khỏi lịch nhưng vẫn mở được',
      steps: [
        'Cuộc họp Đã công bố: bấm "Không tham dự", bấm "Quay lại"',
        'Bấm lại "Không tham dự", xác nhận',
        'API: lịch (excludeDeclined=true) và danh sách cuộc họp; mở lại chi tiết',
      ],
      expected:
        'Hộp "Không tham dự cuộc họp?" — "Bạn chỉ trả lời được một lần, sau đó không đổi được. Cuộc họp sẽ không còn hiện trên lịch của bạn."; Quay lại: vẫn còn câu hỏi, chưa ghi; xác nhận: toast MSG105 + "Cuộc họp không còn hiện trên lịch của bạn.", chip "Không tham dự"; lịch không có cuộc họp, danh sách vẫn có, chi tiết vẫn mở (BR-321)',
      async run(qa) {
        const meeting = await fresh(qa, 'từ chối lời mời', 'Published')
        try {
          await openMeeting(qa, meeting)
          const ask = qa.page.getByRole('region', { name: 'Bạn có tham dự cuộc họp này?' })
          await press(qa, ask.getByRole('button', { name: 'Không tham dự' }))
          const confirm = dialog(qa, 'Không tham dự cuộc họp?')
          await qa.expect(confirm).toContainText('Bạn chỉ trả lời được một lần, sau đó không đổi được. Cuộc họp sẽ không còn hiện trên lịch của bạn.')
          await qa.shot('hoi-lai')
          await press(qa, confirm.getByRole('button', { name: 'Quay lại' }))
          await qa.expect(confirm).toHaveCount(0)
          await qa.expect(ask).toBeVisible()
          const still = (await as(qa, 'member').get(`/api/meetings/${meeting.id}/invitation-response`)).body
          qa.expect(still?.response, 'Quay lại sends nothing (AF-2)').toBe('Pending')
          await press(qa, ask.getByRole('button', { name: 'Không tham dự' }))
          await press(qa, confirm.getByRole('button', { name: 'Không tham dự' }))
          await qa.expect(toast(qa, 'Cuộc họp không còn hiện trên lịch của bạn.')).toBeVisible()
          await qa.expect(toast(qa, MSG.MSG105)).toBeVisible()
          await qa.expect(qa.page.locator('.record-title .rsvp-chip')).toHaveText('Không tham dự')
          await noSidewaysScroll(qa)
          await qa.shot('da-tu-choi')
          if (qa.device === 'phone') return
          const got = (await as(qa, 'secretary').get(`/api/meetings/${meeting.id}`)).body
          const from = encodeURIComponent(new Date(Date.parse(got.plannedStartTime) - 3_600_000).toISOString())
          const to = encodeURIComponent(new Date(Date.parse(got.plannedEndTime) + 3_600_000).toISOString())
          const cal = await as(qa, 'member').get(`/api/meetings?from=${from}&to=${to}&excludeDeclined=true&pageSize=100`)
          const list = await as(qa, 'member').get(`/api/meetings?q=${encodeURIComponent(meeting.title)}&pageSize=20`)
          const c = checker(qa)
          c.check('calendar query', cal, 200)
          c.fact('declined meeting is off the calendar', !(cal.body?.items ?? []).some((m) => m.id === meeting.id))
          c.fact('declined meeting stays in the meeting list', (list.body?.items ?? []).some((m) => m.id === meeting.id), `${list.status}`)
          c.check('detail still opens', await as(qa, 'member').get(`/api/meetings/${meeting.id}`), 200)
          c.done()
        } finally {
          await done(qa, meeting)
        }
      },
    },

    {
      id: 'AC17',
      role: 'chair',
      title: 'Chủ toạ không được hỏi xác nhận tham dự (409 MSG286); ngoài Đã công bố/Đang họp 409 MSG107; giá trị sai 400 MSG106',
      steps: [
        'Đăng nhập chutoa@, mở cuộc họp Đã công bố',
        'API: Chủ toạ PUT invitation-response; Thư ký PUT trên cuộc họp Đã duyệt và Đã kết thúc; Thành viên gửi "Maybe"',
      ],
      expected: 'Không có câu hỏi "Bạn có tham dự…"; 409 MSG286; 409 MSG107; 409 MSG107; 400 MSG106, không ghi gì',
      async run(qa) {
        const pub = await shared(qa, 'đã công bố', 'Published')
        await openMeeting(qa, pub)
        await qa.expect(qa.page.getByRole('region', { name: 'Bạn có tham dự cuộc họp này?' })).toHaveCount(0)
        await qa.shot('chu-toa-khong-hoi')
        const c = checker(qa)
        c.check('Chair answers', await as(qa, 'chair').put(`/api/meetings/${pub.id}/invitation-response`, { response: 'Accepted' }), 409, 'MSG286')
        const ctx = await as(qa, 'chair').get(`/api/meetings/${pub.id}/participant-context`)
        c.fact('Chair context: canRespond false', ctx.body?.permissions?.canRespond === false)
        const approved = await shared(qa, 'đã duyệt', 'Approved')
        c.check('before publication', await as(qa, 'secretary').put(`/api/meetings/${approved.id}/invitation-response`, { response: 'Accepted' }), 409, 'MSG107')
        const ended = await shared(qa, 'đã kết thúc', 'Ended')
        c.check('after the end', await as(qa, 'secretary').put(`/api/meetings/${ended.id}/invitation-response`, { response: 'Accepted' }), 409, 'MSG107')
        c.check('invalid value', await as(qa, 'member').put(`/api/meetings/${pub.id}/invitation-response`, { response: 'Maybe' }), 400, 'MSG106')
        const own = (await as(qa, 'member').get(`/api/meetings/${pub.id}/invitation-response`)).body
        c.fact('invalid value recorded nothing', own?.response === 'Pending', JSON.stringify(own))
        c.done()
      },
    },

    // ---------------------------------------------------------------------------------------------- UC-31 attendance
    {
      id: 'AC18',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Thư ký điểm danh trong bảng thành phần khi cuộc họp đang diễn ra: chọn là lưu, sửa lại được, không có bộ đếm',
      steps: [
        'Cuộc họp Đã công bố: tab Thành phần không có cột Điểm danh',
        'Cuộc họp Đang họp: ô "Điểm danh Lê An" chọn "Có mặt"; rồi đổi sang "Vắng"',
      ],
      expected:
        'Cột Điểm danh chỉ có khi Đang họp; toast "Lưu điểm danh thành công." (MSG147) mỗi lần; ô hiện giá trị mới; người chưa chọn vẫn "Điểm danh"; không có tổng Có mặt/Vắng; không cuộn ngang',
      async run(qa) {
        const u = await users(qa)
        const pub = await shared(qa, 'đã công bố', 'Published')
        await openRoster(qa, pub)
        await qa.expect(panel(qa).getByRole('columnheader', { name: 'Điểm danh' })).toHaveCount(0)
        const meeting = await fresh(qa, 'điểm danh', 'InProgress')
        try {
          await openRoster(qa, meeting)
          const picker = panel(qa).getByRole('button', { name: new RegExp(`^Điểm danh ${u.member.name}`) }).locator('visible=true')
          await qa.expect(picker).toHaveCount(1)
          await noSidewaysScroll(qa)
          await qa.shot('chua-diem-danh')
          await press(qa, picker)
          await press(qa, qa.page.getByRole('option', { name: 'Có mặt' }))
          await qa.expect(toast(qa, MSG.MSG147)).toBeVisible()
          await qa.expect(picker).toHaveAccessibleName(`Điểm danh ${u.member.name}: Có mặt`)
          await qa.shot('co-mat')
          await press(qa, picker)
          await press(qa, qa.page.getByRole('option', { name: 'Vắng' }))
          await qa.expect(picker).toHaveAccessibleName(`Điểm danh ${u.member.name}: Vắng`)
          const other = panel(qa).getByRole('button', { name: new RegExp(`^Điểm danh ${u.extra[0].name}`) }).locator('visible=true')
          await qa.expect(other).toHaveAccessibleName(`Điểm danh ${u.extra[0].name}`)
          await qa.expect(panel(qa).getByText(/Có mặt\s*\d+\s*\/|\d+\s*có mặt/i)).toHaveCount(0)
          await noSidewaysScroll(qa)
          await noRawCodes(qa)
          await qa.shot('vang')
          const att = await as(qa, 'secretary').get(`/api/meetings/${meeting.id}/attendance?pageSize=100`)
          const row = att.body?.items?.find((i) => i.displayName === u.member.name)
          qa.expect(row?.attendance).toBe('Absent')
          qa.expect(row?.recordedBy?.id, 'recorded by the Secretary').toBe(u.secretary.id)
        } finally {
          await done(qa, meeting)
        }
      },
    },

    {
      id: 'AC19',
      role: 'member',
      devices: ['desktop', 'phone'],
      title: 'Thành viên xem điểm danh, không sửa được',
      steps: ['Thư ký đã điểm danh Lê An Có mặt (API); thanhvien@ mở tab Thành phần của cuộc họp Đang họp'],
      expected: 'Cột Điểm danh: "Có mặt" cho mình, "Chưa điểm danh" cho Chủ toạ chưa được ghi; không có ô chọn',
      async run(qa) {
        const u = await users(qa)
        const live = await shared(qa, 'đang họp', 'InProgress')
        const att = await as(qa, 'secretary').get(`/api/meetings/${live.id}/attendance?pageSize=100`)
        const mine = att.body.items.find((i) => i.displayName === u.member.name)
        if (mine.attendance !== 'Present')
          qa.expectStatus(
            await as(qa, 'secretary').post(`/api/meetings/${live.id}/attendance`, {
              entries: [{ id: mine.id, kind: 'Internal', attendance: 'Present', expectedVersion: mine.rowVersion }],
            }),
            200,
          )
        await openRoster(qa, live)
        await qa.expect(panel(qa).getByRole('columnheader', { name: 'Điểm danh' }).locator('visible=true').or(panel(qa).locator('.attendance-inline').first())).not.toHaveCount(0)
        await qa.expect(rowOf(qa, u.member.name).getByText('Có mặt').locator('visible=true')).toHaveCount(1)
        // Nobody records the Chair in this plan (AC20 changes load41 and the Secretary), so that row stays unmarked.
        await qa.expect(rowOf(qa, u.chair.name).getByText('Chưa điểm danh').locator('visible=true')).toHaveCount(1)
        await qa.expect(panel(qa).getByRole('button', { name: /^Điểm danh / })).toHaveCount(0)
        await noSidewaysScroll(qa)
        await qa.shot('thanh-vien-xem-diem-danh')
      },
    },

    {
      id: 'AC20',
      role: 'secretary',
      title: 'API điểm danh: chỉ Thư ký khi Đang họp; Chủ toạ/Thành viên 403; giá trị sai 400 MSG148; version cũ 409 MSG151; ngoài Đang họp 409 MSG149; người ngoài 404',
      steps: [
        'POST attendance bởi Chủ toạ, Thành viên; giá trị "Late"; version cũ; trên cuộc họp Đã công bố và Đã kết thúc',
        'GET attendance bởi quantri@ (ngoài cuộc họp) và không token',
      ],
      expected: '403; 403; 400 MSG148; 409 MSG151, không lưu phần nào; 409 MSG149; 409 MSG149; 404; 401',
      async run(qa) {
        const u = await users(qa)
        const live = await shared(qa, 'đang họp', 'InProgress')
        const c = checker(qa)
        const path = `/api/meetings/${live.id}/attendance`
        const att = (await as(qa, 'secretary').get(`${path}?pageSize=100`)).body
        const row = att.items.find((i) => i.displayName === u.extra[0].name)
        const row2 = att.items.find((i) => i.displayName === u.secretary.name)
        const entry = (value, version = row.rowVersion, r = row) => ({ id: r.id, kind: 'Internal', attendance: value, expectedVersion: version })
        c.check('Chair records', await as(qa, 'chair').post(path, { entries: [entry('Present')] }), 403)
        c.check('Member records', await as(qa, 'member').post(path, { entries: [entry('Present')] }), 403)
        c.check('invalid value', await as(qa, 'secretary').post(path, { entries: [entry('Late')] }), 400, 'MSG148')
        const target = row.attendance === 'Present' ? 'Absent' : 'Present'
        const ok = await as(qa, 'secretary').post(path, { entries: [entry(target)] })
        c.check('Secretary records', ok, 200, 'MSG147')
        c.check(
          'stale version in a batch',
          await as(qa, 'secretary').post(path, { entries: [entry('Present', row2.rowVersion, row2), entry(target === 'Present' ? 'Absent' : 'Present')] }),
          409,
          'MSG151',
        )
        const after = (await as(qa, 'secretary').get(`${path}?pageSize=100`)).body
        c.fact('batch with a stale entry saved nothing', after.items.find((i) => i.id === row2.id)?.rowVersion === row2.rowVersion && after.items.find((i) => i.id === row.id)?.attendance === target)
        for (const [label, key, status] of [
          ['Published', 'đã công bố', 'Published'],
          ['Ended', 'đã kết thúc', 'Ended'],
        ]) {
          const m = await shared(qa, key, status)
          const a = (await as(qa, 'secretary').get(`/api/meetings/${m.id}/attendance?pageSize=100`)).body
          const r = a.items.find((i) => i.displayName === u.member.name)
          c.check(`${label}: record`, await as(qa, 'secretary').post(`/api/meetings/${m.id}/attendance`, { entries: [{ id: r.id, kind: 'Internal', attendance: 'Present', expectedVersion: r.rowVersion }] }), 409, 'MSG149')
        }
        c.check('outsider reads', await as(qa, 'admin').get(path), 404)
        c.check('anonymous reads', await as(qa, 'anon').get(path), 401)
        c.done()
      },
    },

    // ---------------------------------------------------------------------------------------------- guest (UC-04)
    {
      id: 'AC21',
      role: 'secretary',
      title: 'Khách đã xác minh xem thành phần không thấy email người khác và tự trả lời lời mời',
      steps: [
        'Thư ký mời khách vào cuộc họp Đã công bố',
        'Khách mở link trong thư, nhập OTP, mở trang cuộc họp của Khách',
      ],
      expected: 'Danh sách có tên và vai trò, chỉ email của chính khách; tìm theo tên; "Bạn có tham dự cuộc họp này?" trả lời được một lần (BR-176, UC-29)',
      async run(qa) {
        qa.inconclusive(
          'Cần một hộp thư thật nhận link mời và mã OTP (UC-04), không làm được trên máy local mà không gửi thư ra ngoài. Người kiểm: mời một địa chỉ khách thật trên server test, mở link, nhập OTP, kiểm tab Thành phần (chỉ thấy email của mình, ô tìm "Tìm theo tên") và trả lời Tham dự / Không tham dự.',
        )
      },
    },
  ],
}

export { CREATED }

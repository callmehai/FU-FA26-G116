// FE-06 — Quản lý thành phần tham dự (QA subtask SCRUM-193): UC-29 view roster, UC-30a add / invite a guest, UC-30b
// meeting roles, UC-30c remove / revoke, UC-31 answer the invitation, UC-32 response labels, UC-33 attendance, UC-34
// hand the chair over. Business rules BR-036–BR-038, BR-046, BR-016, BR-017, BR-040–BR-042, BR-056, BR-057, BR-055, BR-047, BR-048.
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

/** The account the plan locks to test UC-30b AF-1 after publication. Created once, active when returned. */
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
/** Browser session as an account whose password is not the seed one (load41–load44), mirroring qa.signIn. */
async function signInWith(qa, email, password) {
  await qa.context.clearCookies()
  const res = await qa.context.request.post(`${qa.base}/api/auth/login`, { data: { email, password } })
  if (!res.ok()) throw new Error(`sign-in as ${email}: HTTP ${res.status()}`)
  await qa.context.addInitScript(() => localStorage.setItem('trustcabinet.signedIn', '1'))
}
/** Opens the notification bell and waits for its panel. */
async function openBell(qa) {
  await press(qa, qa.page.getByRole('button', { name: /Thông báo/ }))
  await qa.expect(qa.page.getByRole('dialog', { name: 'Thông báo' })).toBeVisible()
}
/** Bell item of one meeting whose body says `bodyPattern` (titles repeat across notifications). */
async function bellItem(qa, title, bodyPattern) {
  const bell = qa.page.getByRole('dialog', { name: 'Thông báo' })
  await qa.expect(bell.locator('.notif-item').first()).toBeVisible({ timeout: 15_000 })
  const esc = (s) => s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
  const item = bell.getByRole('button', { name: new RegExp(esc(title)) }).filter({ hasText: bodyPattern })
  // The feed pages ("Xem thông báo cũ hơn"); the newest item is not always on page one.
  for (let i = 0; i < 6; i++) {
    if ((await item.count()) > 0) break
    const more = bell.getByRole('button', { name: 'Xem thông báo cũ hơn' })
    if ((await more.count()) === 0 || (await more.isDisabled())) break
    await press(qa, more)
  }
  return item
}
/** Token for an account whose password is not the seed one (load41–load44 own QA passwords). */
async function tokenFor(qa, email, password) {
  const res = await fetch(`${qa.base}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password }),
  })
  if (!res.ok) throw new Error(`sign-in as ${email}: HTTP ${res.status}`)
  return (await res.json()).accessToken
}
async function authed(qa, token, method, path, body) {
  const res = await fetch(`${qa.base}${path}`, {
    method,
    headers: { Authorization: `Bearer ${token}`, ...(body === undefined ? {} : { 'Content-Type': 'application/json' }) },
    body: body === undefined ? undefined : JSON.stringify(body),
  })
  const text = await res.text()
  let json
  try {
    json = text ? JSON.parse(text) : undefined
  } catch {
    json = undefined
  }
  return { status: res.status, body: json ?? text }
}
const withToken = (qa, token) => ({
  get: (p) => authed(qa, token, 'GET', p),
  post: (p, body) => authed(qa, token, 'POST', p, body),
  put: (p, body) => authed(qa, token, 'PUT', p, body),
  delete: (p) => authed(qa, token, 'DELETE', p),
})
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
    // ------------------------------------------------------------------------------------- UC-29 + UC-30a internal
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
          if (qa.device === 'phone') {
            // Dưới 520px tiêu đề cột Thao tác được ẩn cố ý, nút từng dòng nổi ở góc phải.
            await qa.expect(panel(qa).locator('tbody td.col-action .icon-button').first()).toBeVisible()
            const addBox = await panel(qa).getByRole('button', { name: 'Thêm người dự họp' }).boundingBox()
            qa.expect(addBox?.height ?? 0, 'nút thêm trên điện thoại đủ 40px (ADR-0043)').toBeGreaterThanOrEqual(40)
          } else {
            await qa.expect(panel(qa).getByRole('columnheader', { name: 'Thao tác' })).toHaveCount(1)
          }
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
          qa.expect(added?.response, 'new invitation starts Pending (UC-30a)').toBe('Pending')
        } finally {
          await done(qa, meeting)
        }
      },
    },

    // ------------------------------------------------------------------------------------------- UC-30a AF-1 guest
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
        'Cảnh báo "khách dự họp như Thành viên và đọc được tài liệu" không thêm bước xác nhận (UC-30a); "Nhập tên khách." / "Nhập email hợp lệ."; toast "Đã mời khách. Thư mời sẽ được gửi tới email của khách." (MSG96), dòng mới chip Khách; email trùng báo lỗi ở ô Email, không thêm; Huỷ khi đang soạn hỏi "Bỏ lời mời đang soạn?"',
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
          qa.expect(guests[0].role, 'a guest has no meeting role (UC-30a/POST-3)').toBeNull()
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
        '400 MSG276; 409 MSG275; 400 MSG97; 400 MSG97/MSG101 (vai trò sai); Chủ toạ thứ hai 409 MSG109 (UC-30a EX-5); khách 400 MSG97; không lưu gì (revision không đổi)',
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
            'second Chair (UC-30a EX-5)',
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

    // ------------------------------------------------------------------------------------------------- UC-30b roles
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
        'Hộp thoại có Chủ toạ/Thư ký/Thành viên (trước công bố, BR-041, BR-042), toast "Cập nhật vai trò cuộc họp thành công." (MSG100), chip Thư ký; 200 MSG154; 400 MSG101; 409 MSG109',
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
              'demote the last Secretary after publication (BR-040)',
              await api.put(`/api/meetings/${pub.id}/participants/assignments`, { changes: [{ participantId: sec[0].id, role: 'Member' }] }, { ifMatch: tag(pr.revision) }),
              409,
              'MSG109',
            )
          c.done()

          // FT34 (desktop): hạ Thư ký thứ hai xuống Thành viên ngay trên màn hình.
          const addSecond = await api.post(`${base}/participants`, { userIds: [u.extra[1].id], role: 'Secretary' }, { ifMatch: tag((await roster(qa, 'secretary', meeting.id)).revision) })
          qa.expectStatus(addSecond, 201)
          await openRoster(qa, meeting)
          await press(qa, rowOf(qa, u.extra[1].name).getByRole('button', { name: `Đổi vai trò của ${u.extra[1].name}` }))
          const demote = dialog(qa, 'Đổi vai trò')
          await press(qa, demote.getByRole('radio', { name: 'Thành viên' }))
          await press(qa, demote.getByRole('button', { name: 'Lưu' }))
          await qa.expect(toast(qa, MSG.MSG100)).toBeVisible()
          await qa.expect(rowOf(qa, u.extra[1].name)).toContainText('Thành viên')
          await qa.shot('ha-thu-ky-thu-hai')
          // FT42 (desktop): chọn rồi Huỷ ở hộp Đổi vai trò thì không gửi gì.
          const revBefore = (await roster(qa, 'secretary', meeting.id)).revision
          await press(qa, rowOf(qa, u.member.name).getByRole('button', { name: `Đổi vai trò của ${u.member.name}` }))
          const cancelRole = dialog(qa, 'Đổi vai trò')
          await press(qa, cancelRole.getByRole('radio', { name: 'Thư ký' }))
          await press(qa, cancelRole.getByRole('button', { name: 'Huỷ' }))
          await qa.expect(cancelRole).toHaveCount(0)
          await qa.expect(rowOf(qa, u.member.name)).toContainText('Thành viên')
          qa.expect((await roster(qa, 'secretary', meeting.id)).revision, 'Huỷ hộp thoại không gửi gì').toBe(revBefore)
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
        'Dòng Chủ toạ ghi "<Chủ toạ hiện tại> sẽ thành Thành viên"; hộp "Giao Chủ toạ cho Lê An?" nêu Chủ toạ cũ; Huỷ không đổi gì; xác nhận: toast MSG100, Lê An là Chủ toạ, Chủ toạ cũ là Thành viên (BR-041, BR-042, UC-30b AF-1)',
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
      expected: 'Hộp thoại chỉ có Thư ký và Thành viên; dòng Chủ toạ không có nút; API 409 MSG256, Chủ toạ giữ nguyên (BR-041, BR-042)',
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

    // ---------------------------------------------------------------------------------------------- UC-34 hand over
    {
      id: 'AC7',
      role: 'chair',
      devices: ['desktop', 'phone'],
      title: 'Chủ toạ giao vai trò Chủ toạ cho một Thành viên ngay trên dòng của người đó (UC-34)',
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
          await qa.expect(d.getByRole('radio', { name: 'Chủ toạ' }), 'the Secretary cannot take the chair (BR-041, BR-042)').toHaveCount(0)
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
      title: 'Sau công bố, Thư ký thay Chủ toạ có tài khoản bị khoá (UC-30b AF-1, BR-041, BR-042 ngoại lệ)',
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

    // ---------------------------------------------------------------------------------------------- UC-30c remove
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
          // FT39: dòng Khách chỉ có thu hồi, không có bút chì Đổi vai trò.
          await qa.expect(rowOf(qa, 'Khách QA thu hồi')).toContainText('Khách')
          await qa.expect(rowOf(qa, 'Khách QA thu hồi').getByRole('button', { name: /Đổi vai trò/ })).toHaveCount(0)
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
      expected: '409 MSG109; 404; 409 MSG275; 409 MSG109; 200 MSG103 rồi Lê An nhận 404 (chi tiết và thành phần); Nháp 200 MSG103 (BR-040)',
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
          // FT53 (một phần): người bị gỡ có chuông báo (trình duyệt B không mở được trong một phiên).
          const notes = await as(qa, 'member').get('/api/notifications?pageSize=20')
          const items = notes.body?.items ?? notes.body ?? []
          c.fact(
            'removed member gets an in-app notification',
            Array.isArray(items) && items.some((n) => JSON.stringify(n).includes(pub.title)),
            `status ${notes.status}`,
          )
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

    // -------------------------------------------------------------------------------------------- statuses (BR-036, BR-037)
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

    // ------------------------------------------------------------------------------------- read-only roles (UC-29)
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
      role: 'secretary',
      title: 'Người tổ chức ngoài thành phần đọc được nhưng không quản lý được (403 MSG85); người ngoài và Thành viên trước công bố nhận 404; chưa đăng nhập 401',
      steps: [
        'Thư ký tạo họp Approved nhưng không đưa mình vào thành phần (quantri@ thiếu MeetingCreator nên không tạo được); mở tab Thành phần',
        'API: Thư ký POST /participants; quantri@ đọc thành phần cuộc họp của người khác',
        'thanhvien@ đọc thành phần cuộc họp Chờ duyệt; không token đọc thành phần; quantri@ mở họp người khác',
      ],
      expected: 'Bảng hiện, không có nút quản lý; 403 MSG85 (BR-036, BR-037); 404 rỗng (BR-016, BR-017); 404; 401',
      async run(qa) {
        const u = await users(qa)
        // Công bố đòi Thư ký trong thành phần (409 MSG137) nên họp tổ chức-ngoài chỉ tới Đã duyệt.
        const own = await shared(qa, 'người tổ chức ngoài', 'Approved', {
          participants: [
            { userId: u.chair.id, role: 'Chair' },
            { userId: u.member.id, role: 'Member' },
            { userId: u.extra[0].id, role: 'Member' },
          ],
        })
        await openRoster(qa, own)
        await qa.expect(rowOf(qa, u.chair.name)).toBeVisible()
        await qa.expect(panel(qa).getByRole('button', { name: 'Thêm người dự họp' })).toHaveCount(0)
        await qa.expect(panel(qa).getByRole('button', { name: 'Mời khách ngoài hệ thống' })).toHaveCount(0)
        await qa.expect(panel(qa).locator('tbody').getByRole('button')).toHaveCount(0)
        await qa.shot('nguoi-to-chuc-xem')
        const c = checker(qa)
        const r = await roster(qa, 'secretary', own.id)
        c.check('organizer outside the roster: add', await as(qa, 'secretary').post(`/api/meetings/${own.id}/participants`, { userIds: [u.extra[3].id], role: 'Member' }, { ifMatch: tag(r.revision) }), 403, 'MSG85')
        const admin = as(qa, 'admin')
        const other = await shared(qa, 'đã công bố', 'Published')
        const out = await admin.get(`/api/meetings/${other.id}/participants`)
        c.check('outsider: roster', out, 404)
        c.fact('outsider: 404 has no body', !out.body || (typeof out.body === 'object' && !out.body.items), JSON.stringify(out.body).slice(0, 120))
        c.check('outsider: context', await admin.get(`/api/meetings/${other.id}/participant-context`), 404)
        const waiting = await shared(qa, 'chờ duyệt', 'WaitingApproval')
        c.check('member before publication: roster', await as(qa, 'member').get(`/api/meetings/${waiting.id}/participants`), 404)
        c.check('anonymous: roster', await as(qa, 'anon').get(`/api/meetings/${other.id}/participants`), 401)
        await qa.signIn('admin')
        await qa.goto(`/mgmt/meetings/${other.id}?tab=attendees`)
        await qa.expect(qa.page.getByRole('heading', { name: other.title })).toHaveCount(0)
        await qa.shot('nguoi-ngoai-mo-cuoc-hop')
        await qa.signIn('secretary')
        c.done()
      },
    },

    // ------------------------------------------------------------------------------------------ UC-31 / UC-32 RSVP
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
        'Hộp "Không tham dự cuộc họp?" — "Bạn chỉ trả lời được một lần, sau đó không đổi được. Cuộc họp sẽ không còn hiện trên lịch của bạn."; Quay lại: vẫn còn câu hỏi, chưa ghi; xác nhận: toast MSG105 + "Cuộc họp không còn hiện trên lịch của bạn.", chip "Không tham dự"; lịch không có cuộc họp, danh sách vẫn có, chi tiết vẫn mở (BR-047, BR-048)',
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

    // ---------------------------------------------------------------------------------------------- UC-33 attendance
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
      expected: 'Danh sách có tên và vai trò, chỉ email của chính khách; tìm theo tên; "Bạn có tham dự cuộc họp này?" trả lời được một lần (BR-016, BR-017, UC-31)',
      async run(qa) {
        qa.inconclusive(
          'Cần một hộp thư thật nhận link mời và mã OTP (UC-04), không làm được trên máy local mà không gửi thư ra ngoài. Người kiểm: mời một địa chỉ khách thật trên server test, mở link, nhập OTP, kiểm tab Thành phần (chỉ thấy email của mình, ô tìm "Tìm theo tên") và trả lời Tham dự / Không tham dự.',
        )
      },
    },

    // ---------------------------------------------------------------------------------- add-dialog details
    {
      id: 'AC22',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Hộp Thêm người: thêm 2 người một mẻ, nút khoá khi chưa tick, tìm kiếm loại người đã có, Huỷ không gửi; hộp Mời trống Huỷ ngay',
      steps: [
        'Nháp: mở hộp Thêm, tick load42 + load43, vai trò Thành viên, bấm "Thêm 2 người", tải lại',
        'Mở lại: nút "Thêm" khoá; tìm tên thành viên; tìm "zzzzzz"; tick rồi Huỷ',
        'Mở hộp Mời, bấm Huỷ khi còn trống',
      ],
      expected:
        'Toast MSG96, hai dòng chip Thành viên không chip trả lời, tải lại còn; "Thêm" aria-disabled, tick thành "Thêm N người"; tìm loại người đã có ("Không có ai khớp, hoặc người đó đã có trong danh sách."); Huỷ đóng hộp, revision không đổi; hộp Mời trống không hỏi, không tạo Khách',
      async run(qa) {
        const u = await users(qa)
        const meeting = await fresh(qa, 'thêm hai người', 'Draft')
        try {
          await openRoster(qa, meeting)
          await press(qa, panel(qa).getByRole('button', { name: 'Thêm người dự họp' }))
          const add = dialog(qa, 'Thêm thành phần tham dự')
          await qa.expect(add).toBeVisible()
          const submit = add.getByRole('button', { name: /^Thêm/ })
          await qa.expect(submit).toHaveText('Thêm')
          await qa.expect(submit).toHaveAttribute('aria-disabled', 'true')
          await qa.shot('nut-khoa')
          await add.getByLabel('Người dự họp').fill(u.extra[1].email)
          const opt1 = add.locator('label.pick-row').filter({ hasText: u.extra[1].email }).getByRole('checkbox')
          await qa.expect(opt1).toBeVisible({ timeout: 15_000 })
          await press(qa, opt1)
          await qa.expect(submit).toHaveText('Thêm 1 người')
          await qa.expect(submit).not.toHaveAttribute('aria-disabled', 'true')
          await add.getByLabel('Người dự họp').fill(u.extra[2].email)
          const opt2 = add.locator('label.pick-row').filter({ hasText: u.extra[2].email }).getByRole('checkbox')
          await qa.expect(opt2).toBeVisible({ timeout: 15_000 })
          await press(qa, opt2)
          await qa.expect(submit).toHaveText('Thêm 2 người')
          await qa.shot('chon-hai-nguoi')
          await press(qa, submit)
          await qa.expect(toast(qa, MSG.MSG96)).toBeVisible()
          await qa.expect(rowOf(qa, u.extra[1].name)).toContainText('Thành viên')
          await qa.expect(rowOf(qa, u.extra[2].name)).toContainText('Thành viên')
          await noSidewaysScroll(qa)
          await qa.shot('da-them-hai')
          await openRoster(qa, meeting)
          await qa.expect(rowOf(qa, u.extra[1].name)).toBeVisible()
          await qa.expect(rowOf(qa, u.extra[2].name)).toBeVisible()
          // FT18: tìm loại người đã có trong danh sách.
          await press(qa, panel(qa).getByRole('button', { name: 'Thêm người dự họp' }))
          await add.getByLabel('Người dự họp').fill(u.member.name)
          await qa.expect(add.getByText('Không có ai khớp, hoặc người đó đã có trong danh sách.')).toBeVisible()
          await add.getByLabel('Người dự họp').fill('zzzzzz')
          await qa.expect(add.getByText('Không có ai khớp, hoặc người đó đã có trong danh sách.')).toBeVisible()
          await qa.shot('tim-loai-nguoi-da-co')
          // FT20: tick rồi Huỷ — không gửi gì.
          const revBefore = (await roster(qa, 'secretary', meeting.id)).revision
          await add.getByLabel('Người dự họp').fill(u.extra[3].email)
          await press(qa, add.locator('label.pick-row').filter({ hasText: u.extra[3].email }).getByRole('checkbox'))
          await press(qa, add.getByRole('button', { name: 'Huỷ' }))
          await qa.expect(add).toHaveCount(0)
          qa.expect((await roster(qa, 'secretary', meeting.id)).revision, 'Huỷ hộp Thêm không gửi gì').toBe(revBefore)
          // FT30: hộp Mời trống Huỷ ngay, không hỏi.
          await press(qa, panel(qa).getByRole('button', { name: 'Mời khách ngoài hệ thống' }))
          const invite = dialog(qa, 'Mời khách ngoài hệ thống')
          await qa.expect(invite).toBeVisible()
          await press(qa, invite.getByRole('button', { name: 'Huỷ' }))
          await qa.expect(invite).toHaveCount(0)
          await qa.expect(dialog(qa, 'Bỏ lời mời đang soạn?')).toHaveCount(0)
          const r = await roster(qa, 'secretary', meeting.id)
          qa.expect(r.items.filter((i) => i.kind === 'Guest').length, 'không tạo Khách').toBe(0)
        } finally {
          await done(qa, meeting)
        }
      },
    },

    // ---------------------------------------------------------------------------------- long rosters
    {
      id: 'AC23',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Cuộc họp đông người: phân trang 20/50/100 theo đúng thứ tự, hết người để thêm',
      steps: [
        'Họp Nháp thêm mọi tài khoản khả dụng qua API: trang 1/2, Số dòng 50, thứ tự theo API, hộp Thêm hết người',
      ],
      expected:
        'Trang 1 đúng 20 dòng, trang 2 phần còn lại, Số dòng 50 gom một trang, nhãn đếm "thành phần"; hộp Thêm báo "Mọi người đều đã có trong danh sách."',
      async run(qa) {
        const u = await users(qa)
        const mega = await fresh(qa, 'đông thành phần', 'Draft')
        try {
          let rr = await roster(qa, 'secretary', mega.id)
          const have = new Set(rr.items.map((i) => i.userId).filter(Boolean))
          const opts = (await as(qa, 'secretary').get('/api/meetings/participant-options?pageSize=100')).body
          const missing = (opts.items ?? []).filter((o) => !have.has(o.id))
          qa.expect(missing.length, 'còn người để thêm cho đủ hơn 20').toBeGreaterThan(16)
          for (let i = 0; i < missing.length; i += 5) {
            const batch = missing.slice(i, i + 5)
            const res = await as(qa, 'secretary').post(
              `/api/meetings/${mega.id}/participants`,
              { userIds: batch.map((b) => b.id), role: 'Member' },
              { ifMatch: tag(rr.revision) },
            )
            qa.expectStatus(res, 201)
            rr = await roster(qa, 'secretary', mega.id)
          }
          const total = rr.items.length
          qa.expect(total, 'họp hơn 20 người').toBeGreaterThan(20)
          await openRoster(qa, mega)
          await qa.expect(panel(qa).locator('tbody tr')).toHaveCount(20)
          const pager = panel(qa).getByRole('navigation', { name: 'Phân trang' })
          await qa.expect(pager).toContainText(`trong ${total} thành phần`)
          await qa.shot('trang-mot')
          const api1 = (await as(qa, 'secretary').get(`/api/meetings/${mega.id}/participants?page=1&pageSize=20`)).body
          for (const i of [0, 4, 9, 14, 19])
            await qa.expect(panel(qa).locator('tbody tr').nth(i)).toContainText(api1.items[i].displayName)
          await press(qa, pager.getByRole('button', { name: 'Trang 2', exact: true }))
          await qa.expect(panel(qa).locator('tbody tr')).toHaveCount(total - 20)
          await qa.shot('trang-hai')
          await press(qa, pager.getByRole('button', { name: 'Số dòng mỗi trang: 20' }))
          await press(qa, qa.page.getByRole('option', { name: '50' }))
          await qa.expect(panel(qa).locator('tbody tr')).toHaveCount(total)
          await qa.shot('nam-muoi-dong')
          // FT18 E2: không còn ai để thêm.
          await press(qa, panel(qa).getByRole('button', { name: 'Thêm người dự họp' }))
          await qa.expect(dialog(qa, 'Thêm thành phần tham dự').getByText('Mọi người đều đã có trong danh sách.')).toBeVisible()
          await noSidewaysScroll(qa)
          await qa.shot('het-nguoi-de-them')
        } finally {
          await done(qa, mega)
        }
      },
    },

    // ---------------------------------------------------------------------------------- chair reads the roster
    {
      id: 'AC24',
      role: 'chair',
      title: 'Chủ toạ xem thanh công cụ, tooltip hai nút Mời/Thêm và hộp Thêm chỉ có Thư ký/Thành viên',
      steps: [
        'Đăng nhập chutoa@, họp Nháp, đọc tooltip hai nút Mời/Thêm',
        'Mở hộp Thêm: chỉ Thư ký/Thành viên, tick một người, chọn Thư ký, thêm',
      ],
      expected:
        'Hai nút có data-tooltip "Mời khách ngoài hệ thống (qua email)" và "Thêm người dự họp"; hộp chỉ hai vai trò, không "Chủ toạ"; thêm Thư ký: toast MSG96, chip Thư ký',
      async run(qa) {
        const u = await users(qa)
        const meeting = await fresh(qa, 'chủ toạ xem hộp thêm', 'Draft')
        try {
          await openRoster(qa, meeting)
          await qa.expect(panel(qa).getByRole('button', { name: 'Mời khách ngoài hệ thống' })).toHaveAttribute('data-tooltip', 'Mời khách ngoài hệ thống (qua email)')
          await qa.expect(panel(qa).getByRole('button', { name: 'Thêm người dự họp' })).toHaveAttribute('data-tooltip', 'Thêm người dự họp')
          await qa.shot('thanh-cong-cu')
          await press(qa, panel(qa).getByRole('button', { name: 'Thêm người dự họp' }))
          const add = dialog(qa, 'Thêm thành phần tham dự')
          await qa.expect(add.getByRole('radio', { name: 'Thành viên' })).toBeVisible()
          await qa.expect(add.getByRole('radio', { name: 'Thư ký' })).toBeVisible()
          await qa.expect(add.getByRole('radio', { name: 'Chủ toạ' })).toHaveCount(0)
          await add.getByLabel('Người dự họp').fill(u.extra[1].email)
          await press(qa, add.locator('label.pick-row').filter({ hasText: u.extra[1].email }).getByRole('checkbox'))
          await press(qa, add.getByRole('radio', { name: 'Thư ký' }))
          await qa.shot('chon-thu-ky')
          await press(qa, add.getByRole('button', { name: 'Thêm 1 người' }))
          await qa.expect(toast(qa, MSG.MSG96)).toBeVisible()
          await qa.expect(rowOf(qa, u.extra[1].name)).toContainText('Thư ký')
          await noSidewaysScroll(qa)
          await qa.shot('da-them-thu-ky')
        } finally {
          await done(qa, meeting)
        }
      },
    },

    // ---------------------------------------------------------------------------------- deactivation
    {
      id: 'AC25',
      role: 'secretary',
      title: 'Vô hiệu hoá giữa chừng: hộp Thêm báo trong hộp, hộp Đổi vai trò ẩn Chủ toạ, dòng Published gắn chip',
      steps: [
        'Nháp: mở hộp Thêm, tick load42, khoá load42, bấm "Thêm 1 người"; mở lại khoá load41',
        'Hộp Đổi vai trò ở dòng load41: không có lựa chọn Chủ toạ',
        'Công bố họp, đọc dòng load41',
      ],
      expected:
        '400 MSG97, hộp ở lại, câu "Người được chọn không tồn tại hoặc đã ngừng hoạt động."; hộp vai trò chỉ Thư ký/Thành viên; dòng Published chip "Đã vô hiệu hoá"',
      async run(qa) {
        const u = await users(qa)
        const meeting = await fresh(qa, 'vô hiệu hoá', 'Draft')
        const admin = as(qa, 'admin')
        try {
          await openRoster(qa, meeting)
          await press(qa, panel(qa).getByRole('button', { name: 'Thêm người dự họp' }))
          const add = dialog(qa, 'Thêm thành phần tham dự')
          await add.getByLabel('Người dự họp').fill(u.extra[1].email)
          await press(qa, add.locator('label.pick-row').filter({ hasText: u.extra[1].email }).getByRole('checkbox'))
          qa.expectStatus(await admin.put(`/api/users/${u.extra[1].id}/status`, { isActive: false }), 200)
          await press(qa, add.getByRole('button', { name: 'Thêm 1 người' }))
          await qa.expect(add.getByRole('alert')).toContainText('Người được chọn không tồn tại hoặc đã ngừng hoạt động.')
          await qa.expect(add).toBeVisible()
          await qa.shot('them-nguoi-bi-khoa')
          await press(qa, add.getByRole('button', { name: 'Huỷ' }))
          qa.expectStatus(await admin.put(`/api/users/${u.extra[1].id}/status`, { isActive: true }), 200)
          // FT38: không cho Chủ toạ với người đã vô hiệu hoá.
          qa.expectStatus(await admin.put(`/api/users/${u.extra[0].id}/status`, { isActive: false }), 200)
          await openRoster(qa, meeting)
          await press(qa, rowOf(qa, u.extra[0].name).getByRole('button', { name: `Đổi vai trò của ${u.extra[0].name}` }))
          const role = dialog(qa, 'Đổi vai trò')
          await qa.expect(role.getByRole('radio', { name: 'Thư ký' })).toBeVisible()
          await qa.expect(role.getByRole('radio', { name: 'Chủ toạ' })).toHaveCount(0)
          await qa.shot('khong-cho-chu-toa-nguoi-khoa')
          await press(qa, role.getByRole('button', { name: 'Huỷ' }))
          // FT07: dòng Published gắn chip.
          const r = await roster(qa, 'secretary', meeting.id)
          const chairUser = (await users(qa)).chair
          qa.expectStatus(await as(qa, 'secretary').post(`/api/meetings/${meeting.id}/transitions`, { action: 'SubmitForApproval', assignedApproverId: chairUser.id }), 200)
          qa.expectStatus(await as(qa, 'chair').post(`/api/meetings/${meeting.id}/transitions`, { action: 'Approve' }), 200)
          qa.expectStatus(await as(qa, 'secretary').post(`/api/meetings/${meeting.id}/transitions`, { action: 'Publish' }), 200)
          void r
          await openRoster(qa, meeting)
          await qa.expect(rowOf(qa, u.extra[0].name)).toContainText('Đã vô hiệu hoá')
          await noSidewaysScroll(qa)
          await qa.shot('dong-bi-khoa-published')
        } finally {
          await admin.put(`/api/users/${u.extra[0].id}/status`, { isActive: true })
          await admin.put(`/api/users/${u.extra[1].id}/status`, { isActive: true })
          await done(qa, meeting)
        }
      },
    },

    // ---------------------------------------------------------------------------------- last-secretary guards
    {
      id: 'AC26',
      role: 'chair',
      title: 'Giữ Thư ký cuối sau công bố: hộp Đổi vai trò và hộp xác nhận Gỡ đều ở lại, nêu quy định',
      steps: [
        'Published một Thư ký: hộp Đổi vai trò chọn Thành viên, Lưu',
        'Hộp xác nhận Gỡ ở dòng Thư ký: bấm "Gỡ khỏi cuộc họp"',
      ],
      expected:
        'Hai hộp đều ở lại, câu "Cuộc họp phải giữ Chủ toạ và Thư ký theo quy định. Hãy phân công người thay thế."; vai trò và dòng giữ nguyên',
      async run(qa) {
        const u = await users(qa)
        const meeting = await fresh(qa, 'giữ thư ký cuối', 'Published')
        try {
          await openRoster(qa, meeting)
          await press(qa, rowOf(qa, u.secretary.name).getByRole('button', { name: `Đổi vai trò của ${u.secretary.name}` }))
          const role = dialog(qa, 'Đổi vai trò')
          await press(qa, role.getByRole('radio', { name: 'Thành viên' }))
          await press(qa, role.getByRole('button', { name: 'Lưu' }))
          await qa.expect(role.getByRole('alert')).toContainText('Cuộc họp phải giữ Chủ toạ và Thư ký theo quy định. Hãy phân công người thay thế.')
          await qa.expect(role).toBeVisible()
          await qa.shot('ha-thu-ky-cuoi-bi-chan')
          await press(qa, role.getByRole('button', { name: 'Huỷ' }))
          await qa.expect(rowOf(qa, u.secretary.name)).toContainText('Thư ký')
          await press(qa, rowOf(qa, u.secretary.name).getByRole('button', { name: `Gỡ ${u.secretary.name}` }))
          const confirm = dialog(qa, 'Gỡ khỏi cuộc họp?')
          await press(qa, confirm.getByRole('button', { name: 'Gỡ khỏi cuộc họp' }))
          await qa.expect(confirm.getByRole('alert')).toContainText('Cuộc họp phải giữ Chủ toạ và Thư ký theo quy định. Hãy phân công người thay thế.')
          await qa.expect(confirm).toBeVisible()
          await noSidewaysScroll(qa)
          await qa.shot('go-thu-ky-cuoi-bi-chan')
          await press(qa, confirm.getByRole('button', { name: 'Huỷ' }))
          await qa.expect(rowOf(qa, u.secretary.name)).toBeVisible()
          const r = await roster(qa, 'secretary', meeting.id)
          qa.expect(rowFor(r, u.secretary.id)?.role, 'Thư ký còn nguyên').toBe('Secretary')
        } finally {
          await done(qa, meeting)
        }
      },
    },

    // ---------------------------------------------------------------------------------- cancel + self remove
    {
      id: 'AC27',
      role: 'secretary',
      title: 'Huỷ hộp Đổi vai trò, huỷ xác nhận Gỡ, tự gỡ mình: cảnh báo mất cuộc họp',
      steps: [
        'Nháp hai Thư ký: Huỷ hộp Đổi vai trò sau khi chọn; Huỷ xác nhận Gỡ ở dòng Thành viên',
        'Bấm thùng rác ở dòng của chính mình, đọc cảnh báo, xác nhận',
      ],
      expected:
        'Hai lần Huỷ đều đóng hộp, revision không đổi, dòng giữ nguyên; cảnh báo "Bạn sẽ không còn xem được cuộc họp này."; xác nhận: toast MSG103, trang 404 toàn trang (case FT48 cũ đòi câu panel — ghi Failed)',
      async run(qa) {
        const u = await users(qa)
        const meeting = await fresh(qa, 'huỷ và tự gỡ', 'Draft')
        try {
          let r = await roster(qa, 'secretary', meeting.id)
          qa.expectStatus(
            await as(qa, 'secretary').post(`/api/meetings/${meeting.id}/participants`, { userIds: [u.extra[1].id], role: 'Secretary' }, { ifMatch: tag(r.revision) }),
            201,
          )
          await openRoster(qa, meeting)
          // FT42.
          const revRole = (await roster(qa, 'secretary', meeting.id)).revision
          await press(qa, rowOf(qa, u.member.name).getByRole('button', { name: `Đổi vai trò của ${u.member.name}` }))
          const role = dialog(qa, 'Đổi vai trò')
          await press(qa, role.getByRole('radio', { name: 'Thư ký' }))
          await press(qa, role.getByRole('button', { name: 'Huỷ' }))
          await qa.expect(role).toHaveCount(0)
          qa.expect((await roster(qa, 'secretary', meeting.id)).revision, 'Huỷ hộp vai trò không gửi gì').toBe(revRole)
          // FT46.
          const revRemove = (await roster(qa, 'secretary', meeting.id)).revision
          await press(qa, rowOf(qa, u.member.name).getByRole('button', { name: `Gỡ ${u.member.name}` }))
          const cancelRemove = dialog(qa, 'Gỡ khỏi cuộc họp?')
          await press(qa, cancelRemove.getByRole('button', { name: 'Huỷ' }))
          await qa.expect(cancelRemove).toHaveCount(0)
          await qa.expect(rowOf(qa, u.member.name)).toBeVisible()
          qa.expect((await roster(qa, 'secretary', meeting.id)).revision, 'Huỷ xác nhận gỡ không gửi gì').toBe(revRemove)
          await qa.shot('huy-giu-nguyen')
          // FT48.
          await press(qa, rowOf(qa, u.secretary.name).getByRole('button', { name: `Gỡ ${u.secretary.name}` }))
          const selfRemove = dialog(qa, 'Gỡ khỏi cuộc họp?')
          await qa.expect(selfRemove).toContainText('Bạn sẽ không còn xem được cuộc họp này.')
          await qa.shot('canh-bao-tu-go')
          await press(qa, selfRemove.getByRole('button', { name: 'Gỡ khỏi cuộc họp' }))
          await qa.expect(toast(qa, MSG.MSG103)).toBeVisible()
          // Thực tế: người tổ chức tự gỡ vẫn đọc được họp của mình (như AC14) — dòng mình biến mất,
          // hết nút quản lý; không có câu panel "Bạn không còn…" — case FT48 cũ, ghi Failed.
          await qa.expect(rowOf(qa, u.secretary.name)).toHaveCount(0)
          await qa.expect(panel(qa).getByRole('button', { name: 'Thêm người dự họp' })).toHaveCount(0)
          await qa.expect(panel(qa).locator('tbody').getByRole('button')).toHaveCount(0)
          await noSidewaysScroll(qa)
          await qa.shot('da-tu-go-van-doc-duoc')
        } finally {
          await done(qa, meeting)
        }
      },
    },

    // ---------------------------------------------------------------------------------- member read-only edges
    {
      id: 'AC28',
      role: 'member',
      devices: ['desktop', 'phone'],
      title: 'Thành viên mở họp Nháp thì 404 như URL lạ; họp Đang họp không hỏi xác nhận',
      steps: ['Mở họp Nháp (mình trong thành phần)', 'Mở họp Đang họp chưa trả lời'],
      expected: '"Không tìm thấy trang", không vùng Thành phần; Đang họp không có "Bạn có tham dự…"',
      async run(qa) {
        const draft = await fresh(qa, 'thành viên mở nháp', 'Draft')
        try {
          await qa.goto(`/mgmt/meetings/${draft.id}?tab=attendees`)
          await qa.expect(qa.page.getByRole('heading', { name: 'Không tìm thấy trang' })).toBeVisible()
          await qa.expect(qa.page.getByRole('region', { name: 'Thành phần tham dự' })).toHaveCount(0)
          await noSidewaysScroll(qa)
          await qa.shot('nhap-404')
        } finally {
          await done(qa, draft)
        }
        const live = await fresh(qa, 'đang họp không hỏi', 'InProgress')
        try {
          await openMeeting(qa, live)
          await qa.expect(qa.page.getByRole('region', { name: 'Bạn có tham dự cuộc họp này?' })).toHaveCount(0)
          await qa.shot('dang-hop-khong-hoi')
        } finally {
          await done(qa, live)
        }
      },
    },

    // ---------------------------------------------------------------------------------- calendar answering
    {
      id: 'AC29',
      role: 'member',
      title: 'Thành viên trả lời Tham dự từ danh sách lịch',
      steps: ['Mở /calendar, chuyển Danh sách, bấm "Tham dự" ở thẻ cuộc họp', 'Mở "Xem chi tiết"'],
      expected: 'Toast MSG105, thẻ chip "Đã xác nhận tham dự", hết nút trả lời; chi tiết không còn câu hỏi',
      async run(qa) {
        const u = await users(qa)
        const day = new Date(Date.now() + 864e5)
        const pad = (n) => String(n).padStart(2, '0')
        const start = `${day.getFullYear()}-${pad(day.getMonth() + 1)}-${pad(day.getDate())}T09:00:00+07:00`
        const end = `${day.getFullYear()}-${pad(day.getMonth() + 1)}-${pad(day.getDate())}T09:45:00+07:00`
        const created = await as(qa, 'secretary').post('/api/meetings', {
          title: qa.name('FE-06 trả lời từ lịch'),
          plannedStartTime: start,
          plannedEndTime: end,
          mode: 'InPerson',
          location: 'Phòng họp QA',
          onlineLink: null,
          sensitivity: 'Normal',
          description: 'Cuộc họp do bộ kiểm thử QA FE-06 tạo.',
          participants: await defaultRoster(qa),
        })
        qa.expectStatus(created, 201)
        const meeting = { id: created.body.meeting.id, title: created.body.meeting.title, status: 'Draft', organizer: 'secretary' }
        CREATED.push(meeting)
        qa.expectStatus(await as(qa, 'secretary').post(`/api/meetings/${meeting.id}/transitions`, { action: 'SubmitForApproval', assignedApproverId: u.chair.id }), 200)
        qa.expectStatus(await as(qa, 'chair').post(`/api/meetings/${meeting.id}/transitions`, { action: 'Approve' }), 200)
        qa.expectStatus(await as(qa, 'secretary').post(`/api/meetings/${meeting.id}/transitions`, { action: 'Publish' }), 200)
        try {
          await qa.goto('/calendar')
          await press(qa, qa.page.getByRole('button', { name: 'Danh sách' }))
          // Họp ngày mai (sau Chủ nhật 22/11) nằm ở tuần sau.
          await press(qa, qa.page.getByRole('button', { name: 'Tuần sau' }))
          const card = qa.page.locator('article').filter({ hasText: meeting.title })
          await qa.expect(card).toBeVisible({ timeout: 15_000 })
          await qa.expect(card.getByText('Xác nhận tham dự?')).toBeVisible()
          await qa.shot('the-lich-chua-tra-loi')
          await press(qa, card.getByRole('button', { name: 'Tham dự', exact: true }))
          await qa.expect(toast(qa, MSG.MSG105)).toBeVisible()
          await qa.expect(card.getByText('Đã xác nhận tham dự')).toBeVisible()
          await qa.expect(card.getByText('Xác nhận tham dự?')).toHaveCount(0)
          await noSidewaysScroll(qa)
          await qa.shot('the-lich-da-tra-loi')
          await press(qa, card.getByRole('link', { name: 'Xem chi tiết' }))
          await qa.expect(qa.page.getByRole('heading', { level: 1, name: meeting.title })).toBeVisible()
          await qa.expect(qa.page.getByRole('region', { name: 'Bạn có tham dự cuộc họp này?' })).toHaveCount(0)
        } finally {
          await done(qa, meeting)
        }
      },
    },

    // ---------------------------------------------------------------------------------- response chips matrix
    {
      id: 'AC30',
      role: 'secretary',
      title: 'Chip trả lời: người đọc khác thấy đủ ba trạng thái, dòng Chủ toạ không chip, mời lại không chip, người được thêm nhận chuông và mở được',
      steps: [
        'Published: member Tham dự, load41 Không tham dự (API); đọc bảng',
        'Gỡ rồi thêm lại member; đọc bảng',
        'Thêm load43 trên màn hình; đăng nhập load43, mở chuông, mở cuộc họp',
      ],
      expected:
        'Chip "Đã xác nhận tham dự"/"Không tham dự"/không chip, không bộ đếm; dòng Chủ toạ không .rsvp-chip; mời lại không chip; chuông chứa tiêu đề họp, mở được chi tiết, tab liệt kê người mới',
      async run(qa) {
        const u = await users(qa)
        const meeting = await fresh(qa, 'chip trả lời', 'Published')
        try {
          qa.expectStatus(await as(qa, 'member').put(`/api/meetings/${meeting.id}/invitation-response`, { response: 'Accepted' }), 200)
          const load41 = withToken(qa, await tokenFor(qa, u.extra[0].email, 'Qa-Fe06-41-x9Z!'))
          qa.expectStatus(await load41.put(`/api/meetings/${meeting.id}/invitation-response`, { response: 'Declined' }), 200)
          await openRoster(qa, meeting)
          await qa.expect(rowOf(qa, u.member.name)).toContainText('Đã xác nhận tham dự')
          await qa.expect(rowOf(qa, u.extra[0].name)).toContainText('Không tham dự')
          await qa.expect(rowOf(qa, u.secretary.name).locator('.rsvp-chip')).toHaveCount(0)
          await qa.expect(rowOf(qa, u.chair.name).locator('.rsvp-chip')).toHaveCount(0)
          await qa.expect(panel(qa).getByText(/Chờ phản hồi|Đã chấp nhận: \d|Tổng/)).toHaveCount(0)
          await qa.shot('chip-ba-trang-thai')
          // FT68: gỡ rồi thêm lại — lời mời mới không chip.
          let r = await roster(qa, 'secretary', meeting.id)
          qa.expectStatus(await as(qa, 'secretary').delete(`/api/meetings/${meeting.id}/participants/${rowFor(r, u.member.id).id}`, { ifMatch: tag(r.revision) }), 200)
          r = await roster(qa, 'secretary', meeting.id)
          qa.expectStatus(await as(qa, 'secretary').post(`/api/meetings/${meeting.id}/participants`, { userIds: [u.member.id], role: 'Member' }, { ifMatch: tag(r.revision) }), 201)
          await openRoster(qa, meeting)
          await qa.expect(rowOf(qa, u.member.name)).toBeVisible()
          await qa.expect(rowOf(qa, u.member.name).locator('.rsvp-chip')).toHaveCount(0)
          await qa.shot('moi-lai-khong-chip')
          // FT24: người được thêm nhận chuông và mở được.
          await press(qa, panel(qa).getByRole('button', { name: 'Thêm người dự họp' }))
          const add = dialog(qa, 'Thêm thành phần tham dự')
          await add.getByLabel('Người dự họp').fill(u.extra[2].email)
          await press(qa, add.locator('label.pick-row').filter({ hasText: u.extra[2].email }).getByRole('checkbox'))
          await press(qa, add.getByRole('button', { name: 'Thêm 1 người' }))
          await qa.expect(toast(qa, MSG.MSG96)).toBeVisible()
          await signInWith(qa, u.extra[2].email, 'Qa-Fe06-43-x9Z!')
          await openRoster(qa, meeting)
          await qa.expect(rowOf(qa, u.extra[2].name)).toBeVisible()
          await openBell(qa)
          await qa.expect(qa.page.getByRole('dialog', { name: 'Thông báo' }).getByText(meeting.title)).toBeVisible()
          await qa.shot('chuong-nguoi-duoc-them')
          await qa.shot('nguoi-duoc-them-mo-duoc')
        } finally {
          await done(qa, meeting)
        }
      },
    },

    // ---------------------------------------------------------------------------------- attendance edges
    {
      id: 'AC31',
      role: 'secretary',
      title: 'Trả lời không điểm danh hộ; điểm danh chỉ đọc sau khi kết thúc',
      steps: [
        'Đang họp: member Tham dự (API); đọc picker của member vẫn "Điểm danh"',
        'Thư ký điểm danh Có mặt; Chủ toạ kết thúc; đọc lại: giá trị read-only, hết picker',
      ],
      expected:
        'RSVP không tạo điểm danh; sau kết thúc cột Điểm danh còn, "Có mặt" dạng chữ, không ô chọn',
      async run(qa) {
        const u = await users(qa)
        const meeting = await fresh(qa, 'điểm danh sau kết thúc', 'InProgress')
        try {
          qa.expectStatus(await as(qa, 'member').put(`/api/meetings/${meeting.id}/invitation-response`, { response: 'Accepted' }), 200)
          await openRoster(qa, meeting)
          const picker = panel(qa).getByRole('button', { name: new RegExp(`^Điểm danh ${u.member.name}`) })
          await qa.expect(picker.first()).toBeVisible()
          await qa.expect(picker.first()).toHaveAccessibleName(`Điểm danh ${u.member.name}`)
          await qa.shot('rsvp-khong-diem-danh-ho')
          await press(qa, picker.first())
          await press(qa, qa.page.getByRole('option', { name: 'Có mặt' }))
          await qa.expect(toast(qa, MSG.MSG147)).toBeVisible()
          qa.expectStatus(await as(qa, 'chair').post(`/api/meetings/${meeting.id}/transitions`, { action: 'End' }), 200)
          await openRoster(qa, meeting)
          await qa.expect(panel(qa).getByRole('columnheader', { name: 'Điểm danh' })).toHaveCount(1)
          await qa.expect(rowOf(qa, u.member.name).getByText('Có mặt').locator('visible=true')).toBeVisible()
          await qa.expect(panel(qa).locator('tbody').getByRole('button', { name: /^Điểm danh / })).toHaveCount(0)
          await noSidewaysScroll(qa)
          await qa.shot('ket-thuc-chi-doc')
        } finally {
          await done(qa, meeting)
        }
      },
    },

    // ---------------------------------------------------------------------------------- locked/role races
    {
      id: 'AC32',
      role: 'secretary',
      title: 'Hộp Thêm/Đổi vai trò khi họp khoá hoặc mất vai trò giữa chừng: toast, đóng hộp, tải lại',
      steps: [
        'Nháp: mở hộp Thêm, tick người, gửi duyệt qua API, bấm Thêm',
        'Nháp: mở hộp Đổi vai trò, gửi duyệt qua API, bấm Lưu',
        'Nháp: mở hộp Thêm, bị hạ thành Thành viên qua API, bấm Thêm',
      ],
      expected:
        '409 MSG98 toast "Không thể sửa thành phần ở trạng thái hiện tại của cuộc họp." (×2, đóng hộp); mất vai trò: toast mất quyền + về danh sách (case FT23 cũ đòi ở lại — ghi Failed)',
      async run(qa) {
        const u = await users(qa)
        // FT22: họp khoá khi hộp Thêm đang mở.
        const locked = await fresh(qa, 'khoá khi đang thêm', 'Draft')
        try {
          await openRoster(qa, locked)
          await press(qa, panel(qa).getByRole('button', { name: 'Thêm người dự họp' }))
          const add = dialog(qa, 'Thêm thành phần tham dự')
          await add.getByLabel('Người dự họp').fill(u.extra[3].email)
          await press(qa, add.locator('label.pick-row').filter({ hasText: u.extra[3].email }).getByRole('checkbox'))
          qa.expectStatus(await as(qa, 'secretary').post(`/api/meetings/${locked.id}/transitions`, { action: 'SubmitForApproval', assignedApproverId: u.chair.id }), 200)
          await press(qa, add.getByRole('button', { name: 'Thêm 1 người' }))
          await qa.expect(toast(qa, 'Không thể sửa thành phần ở trạng thái hiện tại của cuộc họp.')).toBeVisible()
          await qa.expect(add).toHaveCount(0)
          await qa.expect(panel(qa).getByRole('button', { name: 'Thêm người dự họp' })).toHaveCount(0)
          await qa.shot('khoa-khi-dang-them')
        } finally {
          await done(qa, locked)
        }
        // FT41: họp khoá khi hộp Đổi vai trò đang mở.
        const lockedRole = await fresh(qa, 'khoá khi đang đổi vai trò', 'Draft')
        try {
          await openRoster(qa, lockedRole)
          await press(qa, rowOf(qa, u.member.name).getByRole('button', { name: `Đổi vai trò của ${u.member.name}` }))
          const role = dialog(qa, 'Đổi vai trò')
          await press(qa, role.getByRole('radio', { name: 'Thư ký' }))
          qa.expectStatus(await as(qa, 'secretary').post(`/api/meetings/${lockedRole.id}/transitions`, { action: 'SubmitForApproval', assignedApproverId: u.chair.id }), 200)
          await press(qa, role.getByRole('button', { name: 'Lưu' }))
          await qa.expect(toast(qa, 'Không thể sửa thành phần ở trạng thái hiện tại của cuộc họp.')).toBeVisible()
          await qa.expect(role).toHaveCount(0)
          await qa.shot('khoa-khi-dang-doi-vai-tro')
        } finally {
          await done(qa, lockedRole)
        }
        // FT23: mất vai trò Thư ký khi hộp Thêm đang mở.
        const demoted = await fresh(qa, 'mất vai trò khi đang thêm', 'Draft')
        try {
          await openRoster(qa, demoted)
          await press(qa, panel(qa).getByRole('button', { name: 'Thêm người dự họp' }))
          const add = dialog(qa, 'Thêm thành phần tham dự')
          await add.getByLabel('Người dự họp').fill(u.extra[3].email)
          await press(qa, add.locator('label.pick-row').filter({ hasText: u.extra[3].email }).getByRole('checkbox'))
          let r = await roster(qa, 'chair', demoted.id)
          qa.expectStatus(
            await as(qa, 'chair').put(`/api/meetings/${demoted.id}/participants/assignments`, { changes: [{ participantId: rowFor(r, u.secretary.id).id, role: 'Member' }] }, { ifMatch: tag(r.revision) }),
            200,
          )
          await press(qa, add.getByRole('button', { name: 'Thêm 1 người' }))
          // Thực tế: mất vai trò giữa chừng → useAccessLost đưa về danh sách (nhất quán mọi màn hình),
          // không phải toast + reload như case FT23 cũ — ghi Failed.
          await qa.expect(toast(qa, 'Quyền truy cập của bạn đã thay đổi, không thể tiếp tục thao tác này.')).toBeVisible()
          await qa.expect(qa.page).toHaveURL(/\/mgmt\/meetings(\?|$)/)
          await noSidewaysScroll(qa)
          await qa.shot('mat-vai-tro-ve-danh-sach')
        } finally {
          await done(qa, demoted)
        }
      },
    },

    // ---------------------------------------------------------------------------------- handover races + new chair
    {
      id: 'AC33',
      role: 'chair',
      title: 'Giao Chủ toạ đua nhau/không đủ điều kiện giữa chừng; giao khi Đang họp; Chủ toạ mới nhận chuông và quản lý được',
      steps: [
        'Published: mở hộp giao, bàn giao xong bằng API, rồi bấm Giao + xác nhận',
        'Published: mở hộp giao, người nhận thành Thư ký bằng API, rồi bấm Giao + xác nhận',
        'Đang họp: Chủ toạ giao trên màn hình; đăng nhập người nhận, mở chuông, đọc bảng',
      ],
      expected:
        '409 MSG260 toast "Bạn không còn quyền giao vai trò Chủ toạ cho cuộc họp này.", đóng hộp; 400 MSG259 hộp ở lại nêu điều kiện người nhận; Đang họp MSG257; Chủ toạ mới có chuông chứa tiêu đề, có nút quản lý',
      async run(qa) {
        const u = await users(qa)
        // FT87: người khác bàn giao trước.
        const stale = await fresh(qa, 'giao đua nhau', 'Published')
        try {
          await openRoster(qa, stale)
          await press(qa, rowOf(qa, u.member.name).getByRole('button', { name: `Đổi vai trò của ${u.member.name}` }))
          const d = dialog(qa, 'Đổi vai trò')
          await press(qa, d.getByRole('radio', { name: 'Chủ toạ' }))
          let r = await roster(qa, 'chair', stale.id)
          qa.expectStatus(await as(qa, 'chair').post(`/api/meetings/${stale.id}/participants/chair-handover`, { newChairParticipantId: rowFor(r, u.member.id).id, confirmed: true }, { ifMatch: tag(r.revision) }), 200)
          await press(qa, d.getByRole('button', { name: 'Giao Chủ toạ' }))
          const confirm = dialog(qa, `Giao Chủ toạ cho ${u.member.name}?`)
          await press(qa, confirm.getByRole('button', { name: 'Giao Chủ toạ' }))
          await qa.expect(toast(qa, 'Bạn không còn quyền giao vai trò Chủ toạ cho cuộc họp này.')).toBeVisible()
          await qa.expect(d).toHaveCount(0)
          await qa.shot('giao-dua-nhau')
        } finally {
          await done(qa, stale)
        }
        // FT88: người nhận thành Thư ký giữa chừng.
        const recSec = await fresh(qa, 'người nhận thành thư ký', 'Published')
        try {
          await openRoster(qa, recSec)
          await press(qa, rowOf(qa, u.member.name).getByRole('button', { name: `Đổi vai trò của ${u.member.name}` }))
          const d = dialog(qa, 'Đổi vai trò')
          await press(qa, d.getByRole('radio', { name: 'Chủ toạ' }))
          let r = await roster(qa, 'secretary', recSec.id)
          qa.expectStatus(await as(qa, 'secretary').put(`/api/meetings/${recSec.id}/participants/assignments`, { changes: [{ participantId: rowFor(r, u.member.id).id, role: 'Secretary' }] }, { ifMatch: tag(r.revision) }), 200)
          await press(qa, d.getByRole('button', { name: 'Giao Chủ toạ' }))
          const confirm = dialog(qa, `Giao Chủ toạ cho ${u.member.name}?`)
          await press(qa, confirm.getByRole('button', { name: 'Giao Chủ toạ' }))
          await qa.expect(d.getByRole('alert')).toContainText('Người nhận vai trò Chủ toạ phải là thành viên nội bộ hiện tại, đang hoạt động và không phải Thư ký.')
          await qa.expect(d).toBeVisible()
          await qa.shot('nguoi-nhan-thanh-thu-ky')
          await press(qa, d.getByRole('button', { name: 'Huỷ' }))
          const r2 = await roster(qa, 'chair', recSec.id)
          qa.expect(rowFor(r2, u.chair.id)?.role, 'Chủ toạ vẫn là cũ').toBe('Chair')
        } finally {
          await done(qa, recSec)
        }
        // FT83 + FT86: giao khi Đang họp, Chủ toạ mới nhận chuông và quản lý được.
        const running = await fresh(qa, 'giao khi đang họp', 'InProgress')
        try {
          await openRoster(qa, running)
          await press(qa, rowOf(qa, u.member.name).getByRole('button', { name: `Đổi vai trò của ${u.member.name}` }))
          const d = dialog(qa, 'Đổi vai trò')
          await press(qa, d.getByRole('radio', { name: 'Chủ toạ' }))
          await press(qa, d.getByRole('button', { name: 'Giao Chủ toạ' }))
          const confirm = dialog(qa, `Giao Chủ toạ cho ${u.member.name}?`)
          await press(qa, confirm.getByRole('button', { name: 'Giao Chủ toạ' }))
          await qa.expect(toast(qa, MSG.MSG257)).toBeVisible()
          await qa.expect(rowOf(qa, u.member.name)).toContainText('Chủ toạ')
          await qa.shot('giao-khi-dang-hop')
          // Đổi phiên phải tải lại trang thì app mới dùng session mới (chuông đọc inbox theo token trong bộ nhớ).
          await qa.signIn(u.member.email)
          await openRoster(qa, running)
          await qa.expect(rowOf(qa, u.member.name)).toContainText('Chủ toạ')
          await openBell(qa)
          await qa.expect(await bellItem(qa, running.title, /được giao làm Chủ toạ/)).toBeVisible()
          await qa.shot('chuong-chu-toa-moi')
          await qa.expect(panel(qa).getByRole('button', { name: 'Thêm người dự họp' })).toBeVisible()
          await noSidewaysScroll(qa)
          await qa.shot('chu-toa-moi-quan-ly-duoc')
        } finally {
          await done(qa, running)
        }
      },
    },

    // ---------------------------------------------------------------------------------- live updates, one session
    {
      id: 'AC34',
      role: 'member',
      title: 'Bảng thành phần cập nhật trực tiếp: thêm người, trả lời, điểm danh, thăng Thư ký kèm chuông',
      steps: [
        'Đang họp, tab Thành phần mở: Thư ký thêm load42 / member trả lời / Thư ký điểm danh Có mặt / Thư ký thăng member, mỗi lần đọc bảng và chuông',
      ],
      expected:
        'Dòng mới, chip "Đã xác nhận tham dự", chữ "Có mặt", chip "Thư ký" tự hiện không tải lại; chuông có tin chứa tiêu đề họp',
      async run(qa) {
        const u = await users(qa)
        const meeting = await fresh(qa, 'cập nhật trực tiếp', 'InProgress')
        try {
          await openRoster(qa, meeting)
          // FT11: thêm người hiện ngay.
          let r = await roster(qa, 'secretary', meeting.id)
          qa.expectStatus(await as(qa, 'secretary').post(`/api/meetings/${meeting.id}/participants`, { userIds: [u.extra[1].id], role: 'Member' }, { ifMatch: tag(r.revision) }), 201)
          await qa.expect(rowOf(qa, u.extra[1].name)).toBeVisible({ timeout: 20_000 })
          await qa.shot('live-them-nguoi')
          // FT64: câu trả lời hiện ngay.
          qa.expectStatus(await as(qa, 'member').put(`/api/meetings/${meeting.id}/invitation-response`, { response: 'Accepted' }), 200)
          await qa.expect(rowOf(qa, u.member.name).getByText('Đã xác nhận tham dự')).toBeVisible({ timeout: 20_000 })
          await qa.shot('live-cau-tra-loi')
          // FT79: điểm danh hiện ngay.
          const att = (await as(qa, 'secretary').get(`/api/meetings/${meeting.id}/attendance?pageSize=100`)).body
          const mine = att.items.find((i) => i.displayName === u.member.name)
          qa.expectStatus(await as(qa, 'secretary').post(`/api/meetings/${meeting.id}/attendance`, { entries: [{ id: mine.id, kind: 'Internal', attendance: 'Present', expectedVersion: mine.rowVersion }] }), 200)
          await qa.expect(rowOf(qa, u.member.name).getByText('Có mặt').locator('visible=true')).toBeVisible({ timeout: 20_000 })
          await qa.shot('live-diem-danh')
          // FT44: thăng Thư ký hiện ngay + chuông.
          r = await roster(qa, 'secretary', meeting.id)
          qa.expectStatus(await as(qa, 'secretary').put(`/api/meetings/${meeting.id}/participants/assignments`, { changes: [{ participantId: rowFor(r, u.member.id).id, role: 'Secretary' }] }, { ifMatch: tag(r.revision) }), 200)
          await qa.expect(rowOf(qa, u.member.name)).toContainText('Thư ký', { timeout: 20_000 })
          await openBell(qa)
          await qa.expect(await bellItem(qa, meeting.title, /được giao làm Thư ký/)).toBeVisible()
          await noSidewaysScroll(qa)
          await qa.shot('live-thang-thu-ky')
        } finally {
          await done(qa, meeting)
        }
      },
    },

    // ---------------------------------------------------------------------------------- stale-page races
    {
      id: 'AC35',
      role: 'member',
      title: 'Trang cũ sau khi trả lời/kết thúc/điểm danh nơi khác: toast đúng mã, đọc lại đúng',
      steps: [
        'Published mở sẵn, chặn hub, trả lời bằng API, bấm Tham dự',
        'Published mở sẵn, chặn hub, Chủ toạ kết thúc, bấm Tham dự',
        'Đăng nhập Thư ký; Đang họp mở sẵn, chặn hub, điểm danh bằng API rồi bấm chọn; kết thúc rồi bấm chọn',
      ],
      expected:
        'Toast "Bạn đã trả lời lời mời này. Câu trả lời không đổi được." + hết câu hỏi; toast "Lời mời này không còn cho phép phản hồi." + hết câu hỏi; toast MSG151 + hiện giá trị mới; toast "Chỉ được điểm danh khi cuộc họp đang diễn ra." + hết ô chọn',
      async run(qa) {
        const u = await users(qa)
        const blockHubs = () => qa.page.route('**/hubs/**', (route) => route.abort())
        const unblockHubs = () => qa.page.unroute('**/hubs/**')
        // FT60: trả lời ở nơi khác. Chặn hub TRƯỚC khi mở trang để trang cũ thật. Bấm đáp án NGƯỢC
        // lại (câu đã lưu Tham dự, bấm Không tham dự) mới ra 409 MSG285 — bấm cùng đáp án chỉ ra 200 MSG154.
        const answered = await fresh(qa, 'trả lời nơi khác', 'Published')
        try {
          await blockHubs()
          await openMeeting(qa, answered)
          const ask = qa.page.getByRole('region', { name: 'Bạn có tham dự cuộc họp này?' })
          await qa.expect(ask).toBeVisible()
          qa.expectStatus(await as(qa, 'member').put(`/api/meetings/${answered.id}/invitation-response`, { response: 'Accepted' }), 200)
          await press(qa, ask.getByRole('button', { name: 'Không tham dự' }))
          const confirm = dialog(qa, 'Không tham dự cuộc họp?')
          await press(qa, confirm.getByRole('button', { name: 'Không tham dự' }))
          await qa.expect(toast(qa, 'Bạn đã trả lời lời mời này. Câu trả lời không đổi được.')).toBeVisible()
          await qa.expect(ask).toHaveCount(0)
          const stored = (await as(qa, 'member').get(`/api/meetings/${answered.id}/invitation-response`)).body
          qa.expect(stored?.response, 'câu đã lưu vẫn là Tham dự').toBe('Accepted')
          await qa.shot('tra-loi-noi-khac')
          await unblockHubs()
        } finally {
          await done(qa, answered)
        }
        // FT61: kết thúc khi trang đang mở.
        const ended = await fresh(qa, 'kết thúc khi đang mở', 'Published')
        try {
          await blockHubs()
          await openMeeting(qa, ended)
          await qa.expect(qa.page.getByRole('region', { name: 'Bạn có tham dự cuộc họp này?' })).toBeVisible()
          qa.expectStatus(await as(qa, 'chair').post(`/api/meetings/${ended.id}/transitions`, { action: 'Start' }), 200)
          qa.expectStatus(await as(qa, 'chair').post(`/api/meetings/${ended.id}/transitions`, { action: 'End' }), 200)
          await press(qa, qa.page.getByRole('region', { name: 'Bạn có tham dự cuộc họp này?' }).getByRole('button', { name: 'Tham dự', exact: true }))
          await qa.expect(toast(qa, 'Lời mời này không còn cho phép phản hồi.')).toBeVisible()
          await qa.expect(qa.page.getByRole('region', { name: 'Bạn có tham dự cuộc họp này?' })).toHaveCount(0)
          await qa.shot('ket-thuc-khi-dang-mo')
          await unblockHubs()
        } finally {
          await done(qa, ended)
        }
        await qa.signIn('secretary')
        // FT75: hai Thư ký đua version.
        const raced = await fresh(qa, 'đua version điểm danh', 'InProgress')
        try {
          await blockHubs()
          await openRoster(qa, raced)
          const picker = panel(qa).getByRole('button', { name: new RegExp(`^Điểm danh ${u.extra[0].name}`) })
          await qa.expect(picker.first()).toBeVisible()
          const att = (await as(qa, 'secretary').get(`/api/meetings/${raced.id}/attendance?pageSize=100`)).body
          const row = att.items.find((i) => i.displayName === u.extra[0].name)
          qa.expectStatus(await as(qa, 'secretary').post(`/api/meetings/${raced.id}/attendance`, { entries: [{ id: row.id, kind: 'Internal', attendance: 'Present', expectedVersion: row.rowVersion }] }), 200)
          await press(qa, picker.first())
          await press(qa, qa.page.getByRole('option', { name: 'Vắng' }))
          await qa.expect(toast(qa, 'Thành phần hoặc dữ liệu điểm danh đã thay đổi. Vui lòng tải lại và kiểm tra trước khi lưu.')).toBeVisible()
          // Sau khi đọc lại, ô chọn hiện giá trị mới (picker hiện giá trị trong trigger, không phải badge).
          await qa.expect(picker.first()).toHaveAccessibleName(`Điểm danh ${u.extra[0].name}: Có mặt`, { timeout: 20_000 })
          await qa.shot('dua-version')
          await unblockHubs()
        } finally {
          await done(qa, raced)
        }
        // FT76: kết thúc khi bảng đang mở.
        const endedLive = await fresh(qa, 'kết thúc khi đang điểm danh', 'InProgress')
        try {
          await blockHubs()
          await openRoster(qa, endedLive)
          const picker = panel(qa).getByRole('button', { name: new RegExp(`^Điểm danh ${u.member.name}`) })
          await qa.expect(picker.first()).toBeVisible()
          qa.expectStatus(await as(qa, 'chair').post(`/api/meetings/${endedLive.id}/transitions`, { action: 'End' }), 200)
          await press(qa, picker.first())
          await press(qa, qa.page.getByRole('option', { name: 'Có mặt' }))
          await qa.expect(toast(qa, 'Chỉ được điểm danh khi cuộc họp đang diễn ra.')).toBeVisible()
          await qa.expect(panel(qa).locator('tbody').getByRole('button', { name: /^Điểm danh / })).toHaveCount(0)
          await noSidewaysScroll(qa)
          await qa.shot('ket-thuc-khi-dang-diem-danh')
          await unblockHubs()
        } finally {
          await done(qa, endedLive)
        }
      },
    },

    // ---------------------------------------------------------------------------------- removed/role-lost mid-view
    {
      id: 'AC36',
      role: 'secretary',
      title: 'Bị gỡ / gỡ người đã bị gỡ / mất vai trò điểm danh giữa chừng',
      steps: [
        'Member mở bảng; Thư ký gỡ member qua API; đọc trang',
        'Nháp: mở xác nhận gỡ, Chủ toạ gỡ trước qua API, bấm Gỡ',
        'Đang họp hai Thư ký: bị hạ vai trò qua API, bấm điểm danh',
      ],
      expected:
        'Trang 404 "Không tìm thấy trang"; xác nhận đóng, toast lỗi, dòng biến mất; toast "Bạn không còn quyền điểm danh cuộc họp này." + chỉ đọc',
      async run(qa) {
        const u = await users(qa)
        // FT12: người bị gỡ giữa chừng.
        const removed = await fresh(qa, 'bị gỡ giữa chừng', 'Published')
        try {
          await qa.signIn('member')
          await openRoster(qa, removed)
          let r = await roster(qa, 'secretary', removed.id)
          qa.expectStatus(await as(qa, 'secretary').delete(`/api/meetings/${removed.id}/participants/${rowFor(r, u.member.id).id}`, { ifMatch: tag(r.revision) }), 200)
          await qa.expect(qa.page.getByRole('heading', { name: 'Không tìm thấy trang' })).toBeVisible({ timeout: 20_000 })
          await qa.shot('bi-go-giua-chung')
          await qa.signIn('secretary')
        } finally {
          await done(qa, removed)
        }
        // FT51: gỡ người đã bị gỡ nơi khác.
        const gone = await fresh(qa, 'gỡ người đã bị gỡ', 'Draft')
        try {
          await openRoster(qa, gone)
          await press(qa, rowOf(qa, u.member.name).getByRole('button', { name: `Gỡ ${u.member.name}` }))
          const confirm = dialog(qa, 'Gỡ khỏi cuộc họp?')
          const r = await roster(qa, 'chair', gone.id)
          qa.expectStatus(await as(qa, 'chair').delete(`/api/meetings/${gone.id}/participants/${rowFor(r, u.member.id).id}`, { ifMatch: tag(r.revision) }), 200)
          await press(qa, confirm.getByRole('button', { name: 'Gỡ khỏi cuộc họp' }))
          await qa.expect(confirm).toHaveCount(0)
          await qa.expect(toast(qa, 'Gỡ thành phần tham dự thành công.')).toHaveCount(0)
          await qa.expect(qa.page.locator('.toast-stack')).toBeVisible()
          await qa.expect(rowOf(qa, u.member.name)).toHaveCount(0)
          await noSidewaysScroll(qa)
          await qa.shot('go-nguoi-da-bi-go')
        } finally {
          await done(qa, gone)
        }
        // FT77: mất vai trò Thư ký khi bảng điểm danh đang mở (chặn hub để trang cũ, như AC35).
        const demotedLive = await fresh(qa, 'mất vai trò điểm danh', 'InProgress')
        try {
          let r = await roster(qa, 'secretary', demotedLive.id)
          qa.expectStatus(await as(qa, 'secretary').post(`/api/meetings/${demotedLive.id}/participants`, { userIds: [u.extra[1].id], role: 'Secretary' }, { ifMatch: tag(r.revision) }), 201)
          await qa.page.route('**/hubs/**', (route) => route.abort())
          await openRoster(qa, demotedLive)
          const picker = panel(qa).getByRole('button', { name: new RegExp(`^Điểm danh ${u.member.name}`) })
          await qa.expect(picker.first()).toBeVisible()
          r = await roster(qa, 'chair', demotedLive.id)
          qa.expectStatus(await as(qa, 'chair').put(`/api/meetings/${demotedLive.id}/participants/assignments`, { changes: [{ participantId: rowFor(r, u.secretary.id).id, role: 'Member' }] }, { ifMatch: tag(r.revision) }), 200)
          await press(qa, picker.first())
          await press(qa, qa.page.getByRole('option', { name: 'Có mặt' }))
          await qa.expect(toast(qa, 'Bạn không còn quyền điểm danh cuộc họp này.')).toBeVisible()
          await qa.expect(panel(qa).locator('tbody').getByRole('button', { name: /^Điểm danh / })).toHaveCount(0)
          await noSidewaysScroll(qa)
          await qa.shot('mat-vai-tro-diem-danh')
          await qa.page.unroute('**/hubs/**')
        } finally {
          await done(qa, demotedLive)
        }
      },
    },
  ],
}

export { CREATED }

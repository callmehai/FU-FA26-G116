// FE-01 — Xác thực & Bảo mật tài khoản (QA subtask SCRUM-188): UC-01 sign in with email and password, UC-02 sign in
// with Google, UC-03 sign out, UC-04 access a meeting through a guest invitation. Criteria come from the UC
// specifications (docs/01-requirements/05-use-case-specifications.md), BR-01..BR-09, BR-148..BR-157, BR-180, BR-371,
// BR-444, docs/03-api/01-xac-thuc.md, 16-truy-cap-khach-uc04.md and the message-code catalogue.
//
// Safe on the shared local dev app:
// - Seed accounts are only signed in through the runner (never a wrong password, never a lockout, never a role or
//   status change). Failed and deactivated sign-ins run on two throwaway "[QA] FE-01 …" accounts this plan creates
//   with a random password, on the undeliverable trustcabinet.local domain, so no mail leaves the machine.
// - UC-04 runs on one "[QA] FE-01 lời mời Khách" meeting (Published) with one guest invited at an address on the same
//   undeliverable domain; it is found and reused on the next run. The OTP itself is never obtainable here.
import { randomBytes, randomUUID } from 'node:crypto'

const RUN = Date.now().toString(36)
const DOMAIN = 'trustcabinet.local'
const MSG01 = 'Email hoặc mật khẩu không đúng.'
const MSG01_REFRESH = 'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.'
const MSG02 = 'Tài khoản không khả dụng. Vui lòng liên hệ quản trị viên.'
const MSG03 = 'Đăng nhập bằng Google không thành công.'
const MSG06 = 'Không thể đăng xuất. Vui lòng thử lại.'
const MSG371 = 'Lời mời không còn hiệu lực. Vui lòng liên hệ Thư ký cuộc họp.'
const GUEST_MEETING = '[QA] FE-01 lời mời Khách'
const GUEST_EMAIL = `qa-khach-fe01@${DOMAIN}`

/** Two throwaway accounts per run: one active, one deactivated. Cached for the whole run (one plan import). */
let qaUsers = null

async function ensureUsers(qa) {
  if (qaUsers) return qaUsers
  const admin = await qa.api('admin')
  const roles = await admin.get('/api/roles')
  qa.expectStatus(roles, 200)
  const memberRole = roles.body.find((r) => r.isDefault) ?? roles.body.find((r) => r.name === 'Thành viên')
  const make = async (slug) => {
    const email = `qa-fe01-${slug}-${RUN}@${DOMAIN}`
    const password = `Qa-${randomBytes(12).toString('base64url')}-9`
    const res = await admin.post('/api/users', { fullName: qa.name(`FE-01 ${slug} ${RUN}`), email, password, roleId: memberRole.id })
    qa.expectStatus(res, 201)
    return { id: res.body.user.id, email, password, fullName: res.body.user.fullName }
  }
  const active = await make('active')
  const inactive = await make('inactive')
  qa.expectStatus(await admin.put(`/api/users/${inactive.id}/status`, { isActive: false }), 200)
  qaUsers = { active, inactive }
  return qaUsers
}

async function noSidewaysScroll(qa) {
  const sideways = await qa.page.evaluate(() => document.documentElement.scrollWidth - innerWidth)
  qa.expect(sideways, 'the page scrolls sideways').toBeLessThanOrEqual(0)
}

async function noRawCodes(qa) {
  const text = await qa.page.locator('body').innerText()
  qa.expect(text, 'a raw MSG code is shown to the user').not.toMatch(/MSG\d+/)
}

async function settled(qa) {
  await qa.page.waitForFunction(() => document.getAnimations().every((a) => a.playState !== 'running'))
}

const emailField = (qa) => qa.page.getByLabel('Email')
const passwordField = (qa) => qa.page.getByLabel('Mật khẩu', { exact: true })
const submitButton = (qa) => qa.page.getByRole('button', { name: 'Đăng nhập bằng mật khẩu' })

async function signInOnScreen(qa, email, password) {
  await qa.expect(submitButton(qa)).toBeVisible()
  await emailField(qa).fill(email)
  await passwordField(qa).fill(password)
  await submitButton(qa).click()
}

/** Opens the account menu at the sidebar's foot and asks to sign out. */
async function askSignOut(qa, fullName) {
  await qa.page.getByLabel(`Tài khoản ${fullName}`).click()
  await qa.page.getByRole('button', { name: 'Đăng xuất' }).click()
  const dialog = qa.page.getByRole('dialog', { name: 'Đăng xuất' })
  await qa.expect(dialog).toBeVisible()
  return dialog
}

/** An access token for the context's own signed-in role, from its refresh cookie (the runner's client sends no If-Match). */
async function bearer(qa) {
  const res = await qa.context.request.post('/api/auth/refresh')
  qa.expect(res.status(), 'refresh for a bearer token').toBe(200)
  return (await res.json()).accessToken
}

/** A Published meeting the QA account (as Member) can open, created once per run. */
let memberMeeting = null
async function ensureMemberMeeting(qa, memberEmail) {
  if (memberMeeting) return memberMeeting
  const sec = await qa.api('secretary')
  const chair = await qa.api('chair')
  const admin = await qa.api('admin')
  const users = await admin.get('/api/users')
  qa.expectStatus(users, 200)
  const member = users.body.find((u) => u.email === memberEmail)
  const chairMe = (await chair.get('/api/auth/me')).body
  const secMe = (await sec.get('/api/auth/me')).body
  const title = qa.name('FE-01 họp cho QA')
  const start = new Date(Date.now() + 86_400_000)
  start.setUTCMinutes(0, 0, 0)
  const created = await sec.post('/api/meetings', {
    title,
    plannedStartTime: start.toISOString(),
    plannedEndTime: new Date(start.getTime() + 3_600_000).toISOString(),
    mode: 'Online',
    location: '',
    sensitivity: 'Normal',
    description: 'QA FE-01',
    participants: [
      { userId: chairMe.id, role: 'Chair' },
      { userId: secMe.id, role: 'Secretary' },
      { userId: member.id, role: 'Member' },
    ],
  })
  qa.expectStatus(created, 201)
  const id = created.body.meeting.id
  qa.expectStatus(await sec.post(`/api/meetings/${id}/transitions`, { action: 'SubmitForApproval', assignedApproverId: chairMe.id }), 200)
  qa.expectStatus(await chair.post(`/api/meetings/${id}/transitions`, { action: 'Approve' }), 200)
  qa.expectStatus(await sec.post(`/api/meetings/${id}/transitions`, { action: 'Publish' }), 200)
  memberMeeting = { id, title }
  return memberMeeting
}

/** Finds the reusable Published "[QA]" meeting and its guest invitation: { meetingId, invitationId } or null. */
async function findGuestInvitation(qa) {
  const sec = await qa.api('secretary')
  const list = await sec.get(`/api/meetings?q=${encodeURIComponent(GUEST_MEETING)}&status=Published&pageSize=20`)
  qa.expectStatus(list, 200)
  for (const m of list.body.items.filter((i) => i.title === GUEST_MEETING)) {
    const parts = await sec.get(`/api/meetings/${m.id}/participants?pageSize=100`)
    const guest = parts.body?.items?.find((p) => p.kind === 'Guest' && p.email === GUEST_EMAIL)
    if (guest) return { meetingId: m.id, invitationId: guest.id }
  }
  return null
}

/** Ensures the Published "[QA]" meeting with one guest exists. Needs a criterion signed in as the secretary. */
async function ensureGuestInvitation(qa) {
  const found = await findGuestInvitation(qa)
  if (found) return found
  const sec = await qa.api('secretary')
  const chair = await qa.api('chair')
  const secId = (await sec.get('/api/auth/me')).body.id
  const chairId = (await chair.get('/api/auth/me')).body.id
  let list = await sec.get(`/api/meetings?q=${encodeURIComponent(GUEST_MEETING)}&status=Published&pageSize=20`)
  let meetingId = list.body.items.find((i) => i.title === GUEST_MEETING)?.id
  if (!meetingId) {
    const start = new Date(Date.now() + 3 * 86_400_000)
    start.setUTCMinutes(0, 0, 0)
    const created = await sec.post('/api/meetings', {
      title: GUEST_MEETING,
      plannedStartTime: start.toISOString(),
      plannedEndTime: new Date(start.getTime() + 3_600_000).toISOString(),
      mode: 'Online',
      location: '',
      sensitivity: 'Normal',
      description: 'QA FE-01',
      participants: [
        { userId: chairId, role: 'Chair' },
        { userId: secId, role: 'Secretary' },
      ],
    })
    qa.expectStatus(created, 201)
    meetingId = created.body.meeting.id
    qa.expectStatus(await sec.post(`/api/meetings/${meetingId}/transitions`, { action: 'SubmitForApproval', assignedApproverId: chairId }), 200)
    qa.expectStatus(await chair.post(`/api/meetings/${meetingId}/transitions`, { action: 'Approve' }), 200)
    qa.expectStatus(await sec.post(`/api/meetings/${meetingId}/transitions`, { action: 'Publish' }), 200)
  }
  const parts = await sec.get(`/api/meetings/${meetingId}/participants?pageSize=100`)
  const etag = parts.headers.etag ?? `"participants-${parts.body.revision}"`
  const res = await qa.context.request.post(`/api/meetings/${meetingId}/guest-invitations`, {
    headers: { Authorization: `Bearer ${await bearer(qa)}`, 'If-Match': etag },
    data: { invitedName: qa.name('Khách FE-01'), email: GUEST_EMAIL },
  })
  qa.expect(res.status(), `guest invitation: ${await res.text()}`).toBe(201)
  const again = await findGuestInvitation(qa)
  qa.expect(again, 'the guest invitation is listed').not.toBeNull()
  return again
}

export default {
  ticket: 'SCRUM-188',
  title: 'FE-01 Xác thực & Bảo mật tài khoản: đăng nhập mật khẩu, Google, đăng xuất, Khách qua lời mời',
  devices: ['desktop'],
  criteria: [
    // ---------------------------------------------------------------- UC-01
    {
      id: 'AC1',
      role: 'anon',
      devices: ['desktop', 'phone'],
      title: 'UC-01: đăng nhập bằng email và mật khẩu đúng thì vào Lịch làm việc; màn đăng nhập không có chỗ chọn vai trò; nút khoá khi đang gửi; Thành viên không thấy mục Quản trị',
      steps: ['Mở /login', 'Nhập email, mật khẩu của tài khoản [QA] đang hoạt động (chặn /api/auth/login 400ms)', 'Bấm "Đăng nhập bằng mật khẩu"', 'Xem sidebar ở Lịch làm việc'],
      expected:
        'Màn đăng nhập chỉ có Email, Mật khẩu (BR-08: không chọn vai trò); khi đang gửi nút hiện "Đang đăng nhập…" và bị khoá (AUTH_FT01 E1); sau khi bấm vào /calendar, sidebar có tài khoản vừa đăng nhập; Thành viên không thấy mục "Quản trị"/"Người dùng" (AUTH_FT01 E4); không cuộn ngang; trên điện thoại ô nhập và nút cao ≥ 40px',
      async run(qa) {
        const { active } = await ensureUsers(qa)
        await qa.goto('/login')
        await qa.expect(submitButton(qa)).toBeVisible()
        await qa.expect(qa.page.getByRole('combobox')).toHaveCount(0)
        await qa.expect(qa.page.getByRole('radio')).toHaveCount(0)
        await qa.shot('man-dang-nhap', { fullPage: true })
        await noSidewaysScroll(qa)
        if (qa.device === 'phone') {
          for (const [what, loc] of [['email', emailField(qa)], ['password', passwordField(qa)], ['submit', submitButton(qa)]]) {
            const box = await loc.boundingBox()
            qa.expect(box?.height ?? 0, `${what} is shorter than 40px on touch`).toBeGreaterThanOrEqual(40)
          }
        }
        // AUTH_FT01 E1: while the request is in flight the button locks with "Đang đăng nhập…".
        await qa.page.route('**/api/auth/login', async (route) => {
          await new Promise((r) => setTimeout(r, 1500))
          await route.continue()
        })
        await emailField(qa).fill(active.email)
        await passwordField(qa).fill(active.password)
        await submitButton(qa).click()
        const busy = qa.page.getByRole('button', { name: 'Đang đăng nhập…', exact: true })
        await qa.expect(busy).toBeVisible()
        await qa.expect(busy).toBeDisabled()
        await qa.page.unroute('**/api/auth/login')
        await qa.expect(qa.page).toHaveURL(/\/calendar(\?|$)/)
        await qa.expect(qa.page.getByLabel(`Tài khoản ${active.fullName}`)).toBeVisible()
        // AUTH_FT01 E4: a Member is offered no administration entry.
        await qa.expect(qa.page.getByRole('button', { name: 'Quản trị' })).toHaveCount(0)
        await qa.expect(qa.page.getByRole('link', { name: 'Người dùng', exact: true })).toHaveCount(0)
        // The calendar's own toolbar, not the "Đang tải…" placeholder of its lazy chunk.
        await qa.expect(qa.page.getByRole('button', { name: 'Hôm nay' })).toBeVisible()
        await settled(qa)
        await qa.shot('vao-lich-lam-viec')
        await noSidewaysScroll(qa)
      },
    },
    {
      id: 'AC2',
      role: 'anon',
      devices: ['desktop', 'phone'],
      title: 'UC-01 EX-1: sai mật khẩu và email không tồn tại hiện cùng một câu "Email hoặc mật khẩu không đúng.", ở lại màn đăng nhập',
      steps: ['Mở /login', 'Nhập email [QA] có thật + mật khẩu sai, bấm Đăng nhập', 'Nhập email không tồn tại + mật khẩu bất kỳ, bấm Đăng nhập'],
      expected: 'Cả hai lần: thông báo MSG01 "Email hoặc mật khẩu không đúng." giống hệt nhau (BR-02), vẫn ở /login, không lộ mã MSG',
      async run(qa) {
        const { active } = await ensureUsers(qa)
        await qa.goto('/login')
        const answer1 = qa.page.waitForResponse((r) => r.url().endsWith('/api/auth/login'))
        await signInOnScreen(qa, active.email, 'Sai-mat-khau-QA-123')
        qa.expect((await answer1).status()).toBe(401)
        const alert = qa.page.getByRole('alert')
        await qa.expect(alert).toHaveText(MSG01)
        await qa.expect(qa.page).toHaveURL(/\/login/)
        await qa.shot('sai-mat-khau')
        const wrongPassword = await alert.innerText()
        const answer2 = qa.page.waitForResponse((r) => r.url().endsWith('/api/auth/login'))
        await signInOnScreen(qa, `qa-khong-ton-tai-${RUN}@${DOMAIN}`, 'Sai-mat-khau-QA-123')
        qa.expect((await answer2).status()).toBe(401)
        await qa.expect(alert).toHaveText(MSG01)
        qa.expect(await alert.innerText(), 'the two failures read differently (BR-02)').toBe(wrongPassword)
        await qa.expect(qa.page).toHaveURL(/\/login/)
        await qa.shot('email-khong-ton-tai')
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
      },
    },
    {
      id: 'AC3',
      role: 'anon',
      devices: ['desktop', 'phone'],
      title: 'UC-01 EX-2: tài khoản đã bị vô hiệu hoá đăng nhập đúng mật khẩu thì bị từ chối với "Tài khoản không khả dụng…"',
      steps: ['Quản trị tạo tài khoản [QA] rồi vô hiệu hoá', 'Mở /login, nhập email + mật khẩu đúng của tài khoản đó'],
      expected: 'MSG02 "Tài khoản không khả dụng. Vui lòng liên hệ quản trị viên.", vẫn ở /login, không vào được hệ thống',
      async run(qa) {
        const { inactive } = await ensureUsers(qa)
        await qa.goto('/login')
        const answer = qa.page.waitForResponse((r) => r.url().endsWith('/api/auth/login'))
        await signInOnScreen(qa, inactive.email, inactive.password)
        const res = await answer
        qa.expect(res.status()).toBe(401)
        qa.expect((await res.json()).code).toBe('MSG02')
        await qa.expect(qa.page.getByRole('alert')).toHaveText(MSG02)
        await qa.expect(qa.page).toHaveURL(/\/login/)
        await qa.shot('tai-khoan-vo-hieu-hoa')
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
        // Opening a protected page afterwards still lands on the login screen.
        await qa.goto('/calendar')
        await qa.expect(qa.page).toHaveURL(/\/login/)
      },
    },
    {
      id: 'AC4',
      role: 'anon',
      title: 'UC-01 API: /login trả đúng mã cho từng nhánh, không lộ email có tồn tại, refresh token chỉ nằm trong cookie HttpOnly',
      steps: [
        'POST /api/auth/login: mật khẩu đúng; mật khẩu sai; email không tồn tại; để trống; tài khoản vô hiệu hoá + mật khẩu sai; + mật khẩu đúng',
      ],
      expected:
        '200 có accessToken + user (roleName, capabilities), không có refreshToken trong body, cookie tc_refresh_token HttpOnly/Secure/SameSite=Strict/Path=/api/auth; sai/không tồn tại/trống → 401 MSG01 cùng câu; vô hiệu hoá + sai mật khẩu → MSG01 (kiểm mật khẩu trước); vô hiệu hoá + đúng mật khẩu → 401 MSG02',
      async run(qa) {
        const { active, inactive } = await ensureUsers(qa)
        const login = (email, password) => qa.context.request.post('/api/auth/login', { data: { email, password } })
        const ok = await login(active.email, active.password)
        qa.expect(ok.status()).toBe(200)
        const body = await ok.json()
        qa.expect(Object.keys(body)).toEqual(qa.expect.arrayContaining(['accessToken', 'accessTokenExpiresAt', 'user']))
        qa.expect(JSON.stringify(body)).not.toMatch(/refreshToken/i)
        qa.expect(body.user.email).toBe(active.email)
        qa.expect(body.user.roleName).toBeTruthy()
        qa.expect(Array.isArray(body.user.capabilities)).toBe(true)
        const cookie = ok.headers()['set-cookie'] ?? ''
        for (const part of ['tc_refresh_token=', 'httponly', 'secure', 'samesite=strict', 'path=/api/auth'])
          qa.expect(cookie.toLowerCase(), `cookie lacks ${part}`).toContain(part)
        const wrong = []
        const cases = [
          ['mật khẩu sai', active.email, 'Sai-mat-khau-QA-123', 'MSG01', MSG01],
          ['email không tồn tại', `qa-khong-ton-tai-${RUN}@${DOMAIN}`, 'Sai-mat-khau-QA-123', 'MSG01', MSG01],
          ['để trống', '', '', 'MSG01', MSG01],
          ['vô hiệu hoá + mật khẩu sai', inactive.email, 'Sai-mat-khau-QA-123', 'MSG01', MSG01],
          ['vô hiệu hoá + mật khẩu đúng', inactive.email, inactive.password, 'MSG02', MSG02],
        ]
        for (const [label, email, password, code, message] of cases) {
          const res = await login(email, password)
          const json = await res.json().catch(() => ({}))
          if (res.status() !== 401 || json.code !== code || json.message !== message || 'accessToken' in json)
            wrong.push(`${label}: ${res.status()} ${JSON.stringify(json).slice(0, 160)}`)
        }
        qa.expect(wrong, wrong.join('\n')).toEqual([])
      },
    },
    {
      id: 'AC5',
      role: 'admin',
      title: 'UC-01 / BR-444: nhật ký ghi đăng nhập thành công và thất bại vào tài khoản có thật, kèm lý do, không ghi mật khẩu; email không tồn tại không được ghi',
      steps: [
        'Đăng nhập đúng, sai mật khẩu bằng tài khoản [QA] đang hoạt động; đúng mật khẩu bằng tài khoản [QA] bị vô hiệu hoá; một email không tồn tại',
        'Quản trị GET /api/audit-log?actor=<tài khoản>&action=…',
      ],
      expected:
        'auth.signed_in method=password; auth.sign_in_failed reason=invalid_password, người thực hiện là tài khoản bị thử; account_deactivated cho tài khoản bị vô hiệu hoá; details không chứa mật khẩu; tìm email không tồn tại → 0 dòng',
      async run(qa) {
        const { active, inactive } = await ensureUsers(qa)
        const anon = await qa.api('anon')
        const badPassword = `Sai-QA-${RUN}-xyz`
        const ghost = `qa-ma-${RUN}@${DOMAIN}`
        qa.expectStatus(await anon.post('/api/auth/login', { email: active.email, password: active.password }), 200)
        qa.expectStatus(await anon.post('/api/auth/login', { email: active.email, password: badPassword }), 401)
        qa.expectStatus(await anon.post('/api/auth/login', { email: inactive.email, password: inactive.password }), 401)
        qa.expectStatus(await anon.post('/api/auth/login', { email: ghost, password: badPassword }), 401)
        const admin = await qa.api()
        const rows = async (actor, action) => {
          const res = await admin.get(`/api/audit-log?actor=${actor}&action=${action}&pageSize=20`)
          qa.expectStatus(res, 200)
          return res.body.items
        }
        const signedIn = await rows(active.id, 'auth.signed_in')
        qa.expect(signedIn.length, 'no auth.signed_in row for the account').toBeGreaterThan(0)
        qa.expect(JSON.stringify(signedIn[0].details).toLowerCase()).toContain('password')
        const failed = await rows(active.id, 'auth.sign_in_failed')
        qa.expect(failed.length, 'no auth.sign_in_failed row for the account').toBeGreaterThan(0)
        qa.expect(failed[0].actor?.id, 'a failed sign-in names the account as actor').toBe(active.id)
        qa.expect(JSON.stringify(failed[0].details)).toContain('invalid_password')
        const deactivated = await rows(inactive.id, 'auth.sign_in_failed')
        qa.expect(deactivated.length, 'no failed row for the deactivated account').toBeGreaterThan(0)
        qa.expect(JSON.stringify(deactivated[0].details)).toContain('account_deactivated')
        const all = JSON.stringify([...signedIn, ...failed, ...deactivated])
        for (const secret of [badPassword, active.password, inactive.password]) qa.expect(all, 'a password reached the audit log').not.toContain(secret)
        const ghostRows = await admin.get(`/api/audit-log?q=${encodeURIComponent(ghost)}&pageSize=20`)
        qa.expectStatus(ghostRows, 200)
        qa.expect(ghostRows.body.totalCount, 'a sign-in to an email with no account was recorded').toBe(0)
        qa.note(`signed_in.details=${JSON.stringify(signedIn[0].details)}; failed.details=${JSON.stringify(failed[0].details)}`)
      },
    },
    {
      id: 'AC6',
      role: 'anon',
      title: 'UC-01 POST-3: mở một trang khi chưa đăng nhập thì sau khi đăng nhập vào đúng trang đó; trang tài khoản không mở được thì về Lịch làm việc; link chi tiết hiện lời mời đăng nhập',
      steps: [
        'Chưa đăng nhập, mở /mgmt/meetings → bị đưa về /login → đăng nhập [QA]',
        'Phiên mới: chưa đăng nhập, mở /mgmt/admin/ai (thành viên không có quyền) → đăng nhập [QA]',
        'Phiên mới: chưa đăng nhập, mở /mgmt/meetings/{id} của cuộc họp mình được xem → đăng nhập [QA]',
      ],
      expected: 'Lần 1 vào "Danh sách cuộc họp" (/mgmt/meetings). Lần 2 vào /calendar, không thấy "Không tìm thấy trang". Lần 3: /login hiện "Đăng nhập để mở cuộc họp." (AUTH_FT07 E1), sau đăng nhập vào đúng trang chi tiết cuộc họp (AUTH_FT07 E2)',
      async run(qa) {
        const { active } = await ensureUsers(qa)
        await qa.goto('/mgmt/meetings')
        await qa.expect(qa.page).toHaveURL(/\/login/)
        await qa.shot('chua-dang-nhap-ve-login')
        await signInOnScreen(qa, active.email, active.password)
        await qa.expect(qa.page).toHaveURL(/\/mgmt\/meetings(\?|$)/)
        await qa.expect(qa.page.getByRole('heading', { name: 'Danh sách cuộc họp' })).toBeVisible()
        await settled(qa)
        await qa.shot('vao-dung-trang-da-mo')
        await qa.context.clearCookies()
        await qa.page.evaluate(() => localStorage.clear())
        await qa.goto('/mgmt/admin/ai')
        await qa.expect(qa.page).toHaveURL(/\/login/)
        await signInOnScreen(qa, active.email, active.password)
        await qa.expect(qa.page).toHaveURL(/\/calendar(\?|$)/)
        await qa.expect(qa.page.getByRole('heading', { name: 'Không tìm thấy trang' })).toHaveCount(0)
        await settled(qa)
        await qa.shot('khong-co-quyen-ve-lich')
        // AUTH_FT07: a meeting detail link asks to sign in, then lands on that meeting.
        const meeting = await ensureMemberMeeting(qa, active.email)
        await qa.context.clearCookies()
        await qa.page.evaluate(() => localStorage.clear())
        await qa.goto(`/mgmt/meetings/${meeting.id}`)
        await qa.expect(qa.page).toHaveURL(/\/login/)
        await qa.expect(qa.page.getByText('Đăng nhập để mở cuộc họp.')).toBeVisible()
        await settled(qa)
        await qa.shot('link-cuoc-hop-moi-dang-nhap')
        await signInOnScreen(qa, active.email, active.password)
        await qa.expect(qa.page).toHaveURL(new RegExp(`/mgmt/meetings/${meeting.id}`))
        await qa.expect(qa.page.getByRole('heading', { level: 1, name: meeting.title })).toBeVisible()
        await settled(qa)
        await qa.shot('vao-dung-chi-tiet-cuoc-hop')
      },
    },
    {
      id: 'AC7',
      role: 'admin',
      title: 'UC-01 theo vai trò: Quản trị, Chủ toạ, Thư ký, Thành viên đều đăng nhập được; vai trò và quyền do hệ thống trả (BR-08)',
      steps: ['Đăng nhập từng tài khoản seed qua API', 'GET /api/auth/me'],
      expected: 'Mỗi vai trò: 200, roleName khác rỗng, capabilities là danh sách (Thành viên có thể rỗng); chưa đăng nhập GET /me → 401',
      async run(qa) {
        const seen = []
        for (const role of ['admin', 'chair', 'secretary', 'member']) {
          const me = await qa.capabilities(role)
          qa.expect(me.roleName, `${role} has no role name`).toBeTruthy()
          qa.expect(Array.isArray(me.capabilities)).toBe(true)
          seen.push(`${role}=${me.roleName} [${me.capabilities.join(', ')}]`)
        }
        qa.note(seen.join('; '))
        qa.expectStatus(await (await qa.api('anon')).get('/api/auth/me'), 401)
      },
    },
    // ---------------------------------------------------------------- UC-02
    {
      id: 'AC8',
      role: 'anon',
      devices: ['desktop', 'phone'],
      title: 'UC-02: màn đăng nhập có nút "Tiếp tục với Google" của Google; trang không gọi ra host nào ngoài Google Sign-In',
      steps: ['Mở /login', 'Xem dưới chữ "hoặc"'],
      expected: 'Nút Google hiện (khung của accounts.google.com), không có câu "Không tải được dịch vụ đăng nhập của Google"',
      async run(qa) {
        await qa.goto('/login')
        await qa.expect(qa.page.getByText('hoặc', { exact: true })).toBeVisible()
        // Google Identity Services draws the button into the page (its iframe is a 0x0 helper).
        const button = qa.page.getByRole('button', { name: /bằng Google/ })
        await qa.expect(button).toBeVisible({ timeout: 15_000 })
        await qa.expect(qa.page.getByText('Không tải được dịch vụ đăng nhập của Google')).toHaveCount(0)
        await settled(qa)
        await qa.shot('nut-google', { fullPage: true })
        await noSidewaysScroll(qa)
      },
    },
    {
      id: 'AC9',
      role: 'anon',
      title: 'UC-02 EX-1/EX-2 API: ID token Google sai, sai định dạng hoặc thiếu bị từ chối 401 MSG03, không bao giờ 500',
      steps: ['POST /api/auth/google với idToken rác, "a.b.c", rỗng, không có body'],
      expected: 'Mỗi lần 401 {code:"MSG03", message:"Đăng nhập bằng Google không thành công."}, không có accessToken, không đặt cookie',
      async run(qa) {
        const wrong = []
        for (const data of [{ idToken: 'khong-phai-token' }, { idToken: 'a.b.c' }, { idToken: '' }, {}]) {
          const res = await qa.context.request.post('/api/auth/google', { data })
          const json = await res.json().catch(() => ({}))
          if (res.status() !== 401 || json.code !== 'MSG03' || json.message !== MSG03 || res.headers()['set-cookie']?.includes('tc_refresh_token=') && !/max-age=0/i.test(res.headers()['set-cookie']))
            wrong.push(`${JSON.stringify(data)} → ${res.status()} ${JSON.stringify(json).slice(0, 160)}`)
        }
        qa.expect(wrong, wrong.join('\n')).toEqual([])
      },
    },
    {
      id: 'AC10',
      role: 'anon',
      title: 'UC-02 luồng chính và EX-3/EX-4 bằng tài khoản Google thật',
      steps: [
        'Bấm "Tiếp tục với Google", chọn tài khoản Google có email trùng một tài khoản nội bộ đang hoạt động',
        'Lặp lại với một Gmail chưa có tài khoản nội bộ; rồi với Gmail của một tài khoản đã bị vô hiệu hoá; huỷ cửa sổ Google',
      ],
      expected:
        'Vào /calendar, nhật ký có auth.signed_in method=google; Gmail lạ → MSG05 "Tài khoản Google này chưa được cấp quyền truy cập hệ thống." kèm gợi ý; vô hiệu hoá → MSG02; huỷ → không vào, không lỗi lạ',
      async run(qa) {
        qa.inconclusive(
          'Cần đăng nhập Google thật (không tự động được). Người kiểm: trên http://localhost:5174/login bấm "Tiếp tục với Google" bằng một Gmail trùng email tài khoản nội bộ đang hoạt động (mong đợi vào Lịch làm việc, nhật ký có auth.signed_in method=google); bằng một Gmail chưa có tài khoản (mong đợi MSG05 kèm gợi ý liên hệ quản trị); bằng Gmail của tài khoản đã vô hiệu hoá (mong đợi MSG02); và huỷ cửa sổ Google (ở lại màn đăng nhập).',
        )
      },
    },
    // ---------------------------------------------------------------- UC-03
    {
      id: 'AC11',
      role: 'anon',
      devices: ['desktop', 'phone'],
      title: 'UC-03: Đăng xuất hỏi xác nhận; Huỷ thì giữ phiên; xác nhận thì nút báo đang xử lý rồi về /login; Back không mở lại trang cũ; trang bảo vệ không mở lại được, phiên ở server đã bị huỷ',
      steps: [
        'Đăng nhập [QA], mở Danh sách cuộc họp',
        'Bấm tài khoản ở chân sidebar → Đăng xuất → Huỷ',
        'Lại Đăng xuất → Đăng xuất (chặn /api/auth/logout 400ms)',
        'Đăng nhập lại ngay trên màn đăng nhập',
        'Đăng xuất lần nữa, mở lại /mgmt/meetings; gọi /api/auth/refresh bằng cookie cũ; bấm Back',
      ],
      expected:
        'Hộp "Đăng xuất" hỏi "Bạn có chắc muốn đăng xuất?…"; Huỷ: vẫn ở trang; xác nhận: nút hiện "Đang đăng xuất…" và bị khoá (AUTH_FT17 E2), rồi về /login; đăng nhập lại vào /calendar (đăng xuất chủ động không giữ trang cũ); sau khi đăng xuất mở trang bảo vệ → /login; refresh → 401 MSG01; Back không mở lại dữ liệu cũ (AUTH_FT17 E3)',
      async run(qa) {
        const { active } = await ensureUsers(qa)
        await qa.goto('/login')
        await signInOnScreen(qa, active.email, active.password)
        await qa.expect(qa.page).toHaveURL(/\/calendar/)
        await qa.goto('/mgmt/meetings')
        await qa.expect(qa.page.getByRole('heading', { name: 'Danh sách cuộc họp' })).toBeVisible()
        let dialog = await askSignOut(qa, active.fullName)
        await qa.expect(dialog).toContainText('Bạn có chắc muốn đăng xuất?')
        await settled(qa)
        await qa.shot('hoi-xac-nhan-dang-xuat')
        if (qa.device === 'phone') {
          for (const name of ['Huỷ', 'Đăng xuất']) {
            const box = await dialog.getByRole('button', { name, exact: true }).boundingBox()
            qa.expect(box?.height ?? 0, `dialog button ${name} is shorter than 40px on touch`).toBeGreaterThanOrEqual(40)
          }
        }
        await dialog.getByRole('button', { name: 'Huỷ' }).click()
        await qa.expect(dialog).toHaveCount(0)
        await qa.expect(qa.page).toHaveURL(/\/mgmt\/meetings/)
        dialog = await askSignOut(qa, active.fullName)
        await qa.page.route('**/api/auth/logout', async (route) => {
          await new Promise((r) => setTimeout(r, 400))
          await route.continue()
        })
        await dialog.getByRole('button', { name: 'Đăng xuất', exact: true }).click()
        // AUTH_FT17 E2: the confirm button reports the work in progress.
        await qa.expect(dialog.getByRole('button', { name: 'Đang đăng xuất…', exact: true })).toBeVisible()
        await qa.expect(dialog.getByRole('button', { name: 'Đang đăng xuất…', exact: true })).toBeDisabled()
        await qa.page.unroute('**/api/auth/logout')
        await qa.expect(qa.page).toHaveURL(/\/login/)
        await qa.expect(submitButton(qa)).toBeVisible()
        await qa.expect(qa.page.getByRole('button', { name: /bằng Google/ })).toBeVisible({ timeout: 15_000 })
        await settled(qa)
        await qa.shot('da-dang-xuat')
        await noSidewaysScroll(qa)
        // A deliberate sign-out leaves no destination: signing in again from here lands on the calendar, not the meetings list.
        await signInOnScreen(qa, active.email, active.password)
        await qa.expect(qa.page).toHaveURL(/\/calendar(\?|$)/)
        // Sign out again, then the protected page and the server-side session are gone.
        dialog = await askSignOut(qa, active.fullName)
        await dialog.getByRole('button', { name: 'Đăng xuất', exact: true }).click()
        await qa.expect(qa.page).toHaveURL(/\/login/)
        await qa.goto('/mgmt/meetings')
        await qa.expect(qa.page).toHaveURL(/\/login/)
        await qa.expect(qa.page.getByRole('heading', { name: 'Danh sách cuộc họp' })).toHaveCount(0)
        const refresh = await qa.context.request.post('/api/auth/refresh')
        qa.expect(refresh.status(), 'the refresh cookie still works after sign-out').toBe(401)
        qa.expect((await refresh.json()).message).toBe(MSG01_REFRESH)
        // AUTH_FT17 E3: Back does not bring the previous screen's data back.
        await qa.page.goBack()
        await qa.expect(qa.page).toHaveURL(/\/login/)
        await qa.expect(qa.page.getByRole('heading', { name: 'Danh sách cuộc họp' })).toHaveCount(0)
        await qa.expect(submitButton(qa)).toBeVisible()
        await settled(qa)
        await qa.shot('back-khong-mo-lai-trang-cu')
      },
    },
    {
      id: 'AC12',
      role: 'member',
      devices: ['desktop', 'phone'],
      title: 'UC-03 EX-1: không tới được server khi đăng xuất thì báo "Không thể đăng xuất. Vui lòng thử lại." và giữ phiên; thử lại được',
      steps: ['Đăng nhập thanhvien@', 'Mạng tới /api/auth/logout bị cắt', 'Đăng xuất → Đăng xuất', 'Mạng trở lại, bấm Đăng xuất lần nữa'],
      expected: 'Lần 1: hộp vẫn mở, báo MSG06, vẫn ở trang cũ và còn phiên; lần 2: về /login',
      async run(qa) {
        const me = (await (await qa.api()).get('/api/auth/me')).body
        await qa.goto('/calendar')
        await qa.expect(qa.page.getByLabel(`Tài khoản ${me.fullName}`)).toBeVisible()
        await qa.page.route('**/api/auth/logout', (route) => route.abort('connectionfailed'))
        const dialog = await askSignOut(qa, me.fullName)
        await dialog.getByRole('button', { name: 'Đăng xuất', exact: true }).click()
        await qa.expect(dialog.getByRole('alert')).toHaveText(MSG06)
        await qa.expect(qa.page).toHaveURL(/\/calendar/)
        await settled(qa)
        await qa.shot('khong-dang-xuat-duoc')
        await noRawCodes(qa)
        // The session is kept: the account is still shown behind the dialog.
        await qa.expect(qa.page.getByLabel(`Tài khoản ${me.fullName}`)).toBeVisible()
        await qa.page.unroute('**/api/auth/logout')
        await dialog.getByRole('button', { name: 'Đăng xuất', exact: true }).click()
        await qa.expect(qa.page).toHaveURL(/\/login/)
      },
    },
    // ---------------------------------------------------------------- UC-04
    {
      id: 'AC13',
      role: 'secretary',
      title: 'UC-04 EX-1 API: lời mời Khách có thật nhưng sai token, hoặc mã lời mời bịa, đều bị từ chối bằng cùng 404 MSG371; không gửi mã',
      steps: [
        'Thư ký mời Khách vào cuộc họp [QA] đã công bố (email trên domain không gửi thư)',
        'POST /api/guest-access/invitations/{id}, …/code, …/verify với token sai',
        'Lặp lại với một invitationId ngẫu nhiên',
      ],
      expected: 'Mọi lời gọi 404 {code:"MSG371", message:"Lời mời không còn hiệu lực. Vui lòng liên hệ Thư ký cuộc họp."}, không cho biết nguyên nhân',
      async run(qa) {
        const { invitationId } = await ensureGuestInvitation(qa)
        const anon = await qa.api('anon')
        const wrong = []
        for (const id of [invitationId, randomUUID()]) {
          for (const [suffix, body] of [
            ['', { token: 'token-sai-qa' }],
            ['/code', { token: 'token-sai-qa' }],
            ['/verify', { token: 'token-sai-qa', code: '123456' }],
            ['', {}],
          ]) {
            const res = await anon.post(`/api/guest-access/invitations/${id}${suffix}`, body)
            if (res.status !== 404 || res.body?.code !== 'MSG371' || res.body?.message !== MSG371)
              wrong.push(`${id === invitationId ? 'real' : 'random'}${suffix} ${JSON.stringify(body)} → ${res.status} ${JSON.stringify(res.body).slice(0, 160)}`)
          }
        }
        qa.expect(wrong, wrong.join('\n')).toEqual([])
      },
    },
    {
      id: 'AC14',
      role: 'anon',
      devices: ['desktop', 'phone'],
      title: 'UC-04 EX-1 màn hình: mở link mời sai token, mã lời mời bịa hoặc hỏng thì thấy "Không mở được lời mời" kèm MSG371, không có sidebar',
      steps: [
        'Không đăng nhập, mở /guest/{lời mời thật}#token=sai',
        'Mở /guest/{GUID ngẫu nhiên}#token=sai',
        'Mở /guest/khong-phai-guid',
      ],
      expected: 'Cả ba: tiêu đề "Không mở được lời mời", câu "Lời mời không còn hiệu lực. Vui lòng liên hệ Thư ký cuộc họp.", không có ô nhập mã, không có sidebar nội bộ; không cuộn ngang',
      async run(qa) {
        const found = await findGuestInvitation(qa)
        if (!found) qa.inconclusive('Chưa có cuộc họp [QA] có lời mời Khách: chạy AC13 trước (nó tạo dữ liệu).')
        const paths = [
          [`/guest/${found.invitationId}#token=token-sai-qa`, 'loi-moi-that-sai-token'],
          [`/guest/${randomUUID()}#token=token-sai-qa`, 'ma-loi-moi-bia'],
          ['/guest/khong-phai-guid', 'ma-loi-moi-hong'],
        ]
        for (const [path, slug] of paths) {
          await qa.goto(path)
          await qa.expect(qa.page.getByRole('heading', { name: 'Không mở được lời mời' })).toBeVisible()
          await qa.expect(qa.page.getByRole('alert')).toHaveText(MSG371)
          await qa.expect(qa.page.getByRole('complementary')).toHaveCount(0)
          await qa.expect(qa.page.getByRole('textbox')).toHaveCount(0)
          await settled(qa)
          await qa.shot(slug)
          await noSidewaysScroll(qa)
          await noRawCodes(qa)
        }
      },
    },
    {
      id: 'AC15',
      role: 'secretary',
      title: 'UC-04 quyền: API phiên Khách từ chối người chưa có token (401) và token nội bộ (403)',
      steps: ['GET /api/guest-access/session, PUT …/session/profile, DELETE …/session không token', 'Lặp lại bằng token của Thư ký'],
      expected: 'Không token → 401; token nội bộ → 403 (BR-154, contract §4); không gì bị ghi',
      async run(qa) {
        const anon = await qa.api('anon')
        const sec = await qa.api()
        const profile = { displayName: qa.name('Khách'), jobTitle: null }
        const wrong = []
        for (const [label, client, expected] of [['anon', anon, 401], ['secretary', sec, 403]]) {
          for (const [method, path, body] of [
            ['get', '/api/guest-access/session'],
            ['put', '/api/guest-access/session/profile', profile],
            ['delete', '/api/guest-access/session'],
          ]) {
            const res = await client[method](path, body)
            if (res.status !== expected) wrong.push(`${label} ${method.toUpperCase()} ${path} → ${res.status} (expected ${expected})`)
          }
        }
        qa.expect(wrong, wrong.join('\n')).toEqual([])
      },
    },
    {
      id: 'AC16',
      role: 'anon',
      title: 'UC-04 luồng chính: Khách nhận mã qua email, xác minh, khai tên, vào cuộc họp; AF-1/AF-2/AF-3, EX-2/EX-4/EX-5/EX-7/EX-9',
      steps: [
        'Thư ký mời một hộp thư thật vào cuộc họp đã công bố',
        'Mở link trong thư, nhập mã 6 số trong thư OTP, khai tên hiển thị',
        'Nhập mã sai, sai 5 lần, gửi lại mã trong 1 phút, khai tên khác lần 2, Thoát',
      ],
      expected:
        'MSG375 khi gửi mã; MSG126 khi sai (từ lần 3 có "Còn N lần thử."); MSG372 sau 5 lần sai; MSG373 khi gửi lại trong 1 phút; MSG376 khi khai tên; MSG381 khi đổi tên; mở lại link còn phiên vào thẳng; Thoát → "Bạn đã thoát khỏi cuộc họp" + "Vào lại cuộc họp"; nhật ký có guest.verified',
      async run(qa) {
        qa.inconclusive(
          'Mã OTP chỉ gửi tới email được mời và link mời chứa token ký bằng khoá bí mật của server, nên không lấy được trên máy local (domain trustcabinet.local không gửi thư). Người kiểm: trên cuộc họp đã công bố, Thư ký mời một hộp thư thật (ví dụ hộp thư tạm), mở link trong thư, rồi kiểm từng mong đợi ở cột Mong đợi.',
        )
      },
    },
    // ------------------------------------------------------------ AC bổ sung cho Round 1 (R1-FE01)
    {
      id: 'AC17',
      role: 'member',
      devices: ['desktop', 'phone'],
      title: 'Vai trò không capability (Thành viên seed): chỉ thấy màn hình chung; URL quản trị hiện đúng trang 404 như đường dẫn lạ (AUTH_FT05)',
      steps: ['Đăng nhập thanhvien@ (capabilities rỗng)', 'Xem sidebar', 'Mở /mgmt/admin/users và /mgmt/khong-co-trang-nay'],
      expected:
        'Vào /calendar; không có mục "Quản trị"; hai đường dẫn hiện cùng một trang "Không tìm thấy trang" (không lộ chữ "không có quyền"); không cuộn ngang',
      async run(qa) {
        const { capabilities } = await qa.capabilities()
        qa.expect(capabilities, 'seed member gained capabilities').toEqual([])
        await qa.goto('/calendar')
        await qa.expect(qa.page).toHaveURL(/\/calendar/)
        await qa.expect(qa.page.getByRole('button', { name: 'Quản trị' })).toHaveCount(0)
        await qa.expect(qa.page.getByRole('link', { name: 'Người dùng', exact: true })).toHaveCount(0)
        await settled(qa)
        await qa.shot('sidebar-khong-co-quan-tri')
        const texts = []
        for (const [path, slug] of [['/mgmt/admin/users', 'url-quan-tri-404'], ['/mgmt/khong-co-trang-nay', 'url-la-404']]) {
          await qa.goto(path)
          await qa.expect(qa.page.getByRole('heading', { name: 'Không tìm thấy trang' })).toBeVisible()
          texts.push(await qa.page.locator('main').innerText())
          await settled(qa)
          await qa.shot(slug)
          await noSidewaysScroll(qa)
        }
        qa.expect(texts[0]).toBe(texts[1])
      },
    },
    {
      id: 'AC18',
      role: 'anon',
      devices: ['desktop', 'phone'],
      title: 'Bấm đăng nhập khi để trống: báo lỗi từng ô, không gửi request, ở lại /login (AUTH_FT02 — case đòi một câu gộp, sản phẩm báo riêng từng ô)',
      steps: ['Mở /login', 'Bấm "Đăng nhập bằng mật khẩu" khi cả hai ô trống'],
      expected:
        'Ô Email: "Vui lòng nhập email."; ô Mật khẩu: "Vui lòng nhập mật khẩu."; không có POST /api/auth/login; vẫn ở /login',
      async run(qa) {
        await qa.goto('/login')
        await qa.expect(submitButton(qa)).toBeVisible()
        let posts = 0
        qa.page.on('request', (r) => {
          if (r.url().endsWith('/api/auth/login')) posts += 1
        })
        await submitButton(qa).click()
        await qa.expect(qa.page.getByText('Vui lòng nhập email.')).toBeVisible()
        await qa.expect(qa.page.getByText('Vui lòng nhập mật khẩu.')).toBeVisible()
        await qa.expect(qa.page).toHaveURL(/\/login/)
        await settled(qa)
        await qa.shot('form-trong-bao-tung-o')
        await noSidewaysScroll(qa)
        qa.expect(posts, 'an empty form was sent to the server').toBe(0)
        qa.note('AUTH_FT02 E1 đòi một câu "Vui lòng nhập email và mật khẩu." — sản phẩm báo riêng từng ô như ResetPasswordPage.')
      },
    },
    {
      id: 'AC19',
      role: 'anon',
      devices: ['desktop', 'phone'],
      title: 'Không tải được dịch vụ Google thì báo rõ và vẫn đăng nhập bằng mật khẩu được (AUTH_FT12)',
      steps: ['Chặn accounts.google.com', 'Mở /login', 'Đăng nhập bằng tài khoản [QA]'],
      expected:
        'Vùng Google hiện "Không tải được dịch vụ đăng nhập của Google. Vui lòng thử lại sau."; đăng nhập mật khẩu vẫn vào /calendar; không cuộn ngang',
      async run(qa) {
        const { active } = await ensureUsers(qa)
        await qa.page.route(/accounts\.google\.com/, (route) => route.abort('connectionfailed'))
        await qa.goto('/login')
        await qa.expect(qa.page.getByText('Không tải được dịch vụ đăng nhập của Google. Vui lòng thử lại sau.')).toBeVisible()
        await settled(qa)
        await qa.shot('google-khong-tai-duoc')
        await signInOnScreen(qa, active.email, active.password)
        await qa.expect(qa.page).toHaveURL(/\/calendar(\?|$)/)
        await noSidewaysScroll(qa)
      },
    },
    {
      id: 'AC20',
      role: 'member',
      title: 'Tải lại trang và mở tab mới giữ phiên đăng nhập (AUTH_FT13)',
      steps: ['Đăng nhập thanhvien@, mở /calendar', 'Tải lại trang', 'Mở /calendar trong tab mới cùng trình duyệt'],
      expected:
        'Sau tải lại vẫn ở /calendar với cùng tài khoản, không thấy form đăng nhập; tab mới vào thẳng /calendar mà không qua /login',
      async run(qa) {
        const me = (await (await qa.api()).get('/api/auth/me')).body
        await qa.goto('/calendar')
        await qa.expect(qa.page.getByLabel(`Tài khoản ${me.fullName}`)).toBeVisible()
        await qa.page.reload()
        await qa.expect(qa.page).toHaveURL(/\/calendar/)
        await qa.expect(qa.page.getByLabel(`Tài khoản ${me.fullName}`)).toBeVisible()
        await qa.expect(submitButton(qa)).toHaveCount(0)
        await settled(qa)
        await qa.shot('tai-lai-van-phien')
        const tab2 = await qa.page.context().newPage()
        try {
          await tab2.goto(`${new URL(qa.page.url()).origin}/calendar`)
          await qa.expect(tab2.getByLabel(`Tài khoản ${me.fullName}`)).toBeVisible()
          await qa.expect(tab2).toHaveURL(/\/calendar/)
        } finally {
          await tab2.close()
        }
      },
    },
    {
      id: 'AC21',
      role: 'anon',
      title: 'Sai mật khẩu 5 lần thì lần 6 bị chặn 429, hết một phút lại đăng nhập được (AUTH_FT06)',
      steps: [
        'Tạo tài khoản [QA] riêng, POST /api/auth/login sai mật khẩu 5 lần',
        'Lần 6 dùng đúng mật khẩu qua API, rồi trên màn hình',
        'Chờ 65 giây, đăng nhập lại bằng đúng mật khẩu',
      ],
      expected:
        '5 lần đầu 401 MSG01; lần 6: 429 MSG414 "Bạn đã thử quá nhiều lần. Vui lòng thử lại sau ít phút.", không đăng nhập; màn hình hiện đúng câu đó, vẫn ở /login; sau ~1 phút đăng nhập đúng vào /calendar',
      async run(qa) {
        const admin = await qa.api('admin')
        const roles = (await admin.get('/api/roles')).body
        const memberRole = roles.find((r) => r.isDefault) ?? roles.find((r) => r.name === 'Thành viên')
        const email = `qa-fe01-chan-${RUN}@${DOMAIN}`
        const password = `Qa-${randomBytes(12).toString('base64url')}-9`
        let userId = null
        try {
          const mk = await admin.post('/api/users', { fullName: qa.name(`FE-01 chờ chặn ${RUN}`), email, password, roleId: memberRole.id })
          qa.expectStatus(mk, 201)
          userId = mk.body.user.id
          const anon = await qa.api('anon')
          for (let i = 0; i < 5; i++) {
            const r = await anon.post('/api/auth/login', { email, password: 'Sai-mat-khau-QA-123' })
            qa.expectStatus(r, 401)
            qa.expect(r.body.code, `lần ${i + 1} không phải MSG01`).toBe('MSG01')
          }
          const sixth = await anon.post('/api/auth/login', { email, password })
          qa.expectStatus(sixth, 429)
          qa.expect(sixth.body.code).toBe('MSG414')
          qa.expect(sixth.body.message).toBe('Bạn đã thử quá nhiều lần. Vui lòng thử lại sau ít phút.')
          qa.expect(sixth.body, 'lần 6 vẫn trả accessToken').not.toHaveProperty('accessToken')
          await qa.goto('/login')
          await signInOnScreen(qa, email, password)
          await qa.expect(qa.page).toHaveURL(/\/login/)
          await qa.expect(qa.page.getByRole('alert')).toHaveText('Bạn đã thử quá nhiều lần. Vui lòng thử lại sau ít phút.')
          await settled(qa)
          await qa.shot('bi-chan-429')
          await noRawCodes(qa)
          await qa.page.waitForTimeout(65_000)
          await signInOnScreen(qa, email, password)
          await qa.expect(qa.page).toHaveURL(/\/calendar(\?|$)/)
          await qa.expect(qa.page.getByLabel(`Tài khoản ${mk.body.user.fullName}`)).toBeVisible()
          await settled(qa)
          await qa.shot('het-chan-lai-vao-duoc')
        } finally {
          if (userId) await admin.put(`/api/users/${userId}/status`, { isActive: false })
        }
      },
    },
    {
      id: 'AC22',
      role: 'anon',
      title: 'Vô hiệu hoá giữa phiên: trang hiện tại đưa về đăng nhập kèm lời nhắn, đăng nhập lại bị từ chối (AUTH_FT15)',
      steps: [
        'Tạo tài khoản [QA] riêng, đăng nhập trên màn hình, mở /calendar',
        'Quản trị vô hiệu hoá tài khoản đó, tải lại trang',
        'Đăng nhập lại bằng tài khoản đó',
      ],
      expected:
        'Sau khi bị vô hiệu hoá, tải lại đưa về /login kèm "Phiên đăng nhập đã kết thúc. Vui lòng đăng nhập lại."; đăng nhập lại báo MSG02 "Tài khoản không khả dụng…"',
      async run(qa) {
        const admin = await qa.api('admin')
        const roles = (await admin.get('/api/roles')).body
        const memberRole = roles.find((r) => r.isDefault) ?? roles.find((r) => r.name === 'Thành viên')
        const email = `qa-fe01-giua-phien-${RUN}@${DOMAIN}`
        const password = `Qa-${randomBytes(12).toString('base64url')}-9`
        let userId = null
        try {
          const mk = await admin.post('/api/users', { fullName: qa.name(`FE-01 giữa phiên ${RUN}`), email, password, roleId: memberRole.id })
          qa.expectStatus(mk, 201)
          userId = mk.body.user.id
          await qa.goto('/login')
          await signInOnScreen(qa, email, password)
          await qa.expect(qa.page).toHaveURL(/\/calendar(\?|$)/)
          qa.expectStatus(await admin.put(`/api/users/${userId}/status`, { isActive: false }), 200)
          await qa.page.reload()
          await qa.expect(qa.page).toHaveURL(/\/login/)
          await qa.expect(qa.page.getByText('Phiên đăng nhập đã kết thúc. Vui lòng đăng nhập lại.')).toBeVisible()
          await settled(qa)
          await qa.shot('giua-phien-ve-dang-nhap')
          await signInOnScreen(qa, email, password)
          await qa.expect(qa.page.getByRole('alert')).toHaveText(MSG02)
          await qa.expect(qa.page).toHaveURL(/\/login/)
        } finally {
          if (userId) await admin.put(`/api/users/${userId}/status`, { isActive: true })
        }
      },
    },
    {
      id: 'AC23',
      role: 'member',
      title: 'Đăng xuất ở tab này thì tab kia cùng trình duyệt cũng hết phiên (AUTH_FT20)',
      steps: ['Tab 1 mở /calendar', 'Tab 2 cùng trình duyệt mở /calendar', 'Tab 1 đăng xuất qua menu tài khoản', 'Tab 2 tải lại'],
      expected: 'Tab 1 về /login; tab 2 tải lại cũng về /login, không còn dữ liệu cũ',
      async run(qa) {
        const me = (await (await qa.api()).get('/api/auth/me')).body
        await qa.goto('/calendar')
        await qa.expect(qa.page.getByLabel(`Tài khoản ${me.fullName}`)).toBeVisible()
        const tab2 = await qa.page.context().newPage()
        try {
          await tab2.goto(`${new URL(qa.page.url()).origin}/calendar`)
          await qa.expect(tab2.getByLabel(`Tài khoản ${me.fullName}`)).toBeVisible()
          const dialog = await askSignOut(qa, me.fullName)
          await dialog.getByRole('button', { name: 'Đăng xuất', exact: true }).click()
          await qa.expect(qa.page).toHaveURL(/\/login/)
          await tab2.reload()
          await qa.expect(tab2).toHaveURL(/\/login/)
          await qa.expect(tab2.getByRole('heading', { name: 'Danh sách cuộc họp' })).toHaveCount(0)
          await tab2.getByRole('heading', { name: 'TrustCabinet' }).first().waitFor().catch(() => undefined)
          await settled(qa).catch(() => undefined)
          await qa.shot('tab2-cung-ve-login')
        } finally {
          await tab2.close()
        }
      },
    },
    {
      id: 'AC24',
      role: 'member',
      title: 'Đăng xuất ở trình duyệt thì phiên API của thiết bị khác vẫn còn (AUTH_FT21)',
      steps: ['Thiết bị B: đăng nhập thanhvien@ qua API riêng', 'Thiết bị A (trình duyệt) đăng xuất qua menu tài khoản', 'Thiết bị B gọi /api/auth/me và refresh'],
      expected:
        'Trình duyệt về /login; thiết bị B vẫn gọi /api/auth/me 200 và refresh 200 — đăng xuất một nơi không đá nơi khác',
      async run(qa) {
        const me = (await (await qa.api()).get('/api/auth/me')).body
        const other = await qa.api('member')
        qa.expectStatus(await other.get('/api/auth/me'), 200)
        await qa.goto('/calendar')
        await qa.expect(qa.page.getByLabel(`Tài khoản ${me.fullName}`)).toBeVisible()
        const dialog = await askSignOut(qa, me.fullName)
        await dialog.getByRole('button', { name: 'Đăng xuất', exact: true }).click()
        await qa.expect(qa.page).toHaveURL(/\/login/)
        qa.expectStatus(await other.get('/api/auth/me'), 200)
        qa.expectStatus(await other.post('/api/auth/refresh'), 200)
        await settled(qa)
        await qa.shot('trinh-duyet-ve-login-thiet-bi-khac-con')
        qa.note('Thiết bị B là một API client độc lập (cookie refresh riêng), tương đương một trình duyệt khác trên máy khác.')
      },
    },
    {
      id: 'AC25',
      role: 'secretary',
      title: 'Lời mời Khách đã bị thu hồi: mở link cũng chỉ thấy "Không mở được lời mời" kèm MSG371 (AUTH_FT33 nhánh thu hồi)',
      steps: [
        'Thư ký mời một Khách phụ vào cuộc họp [QA] đã công bố',
        'Thu hồi lời mời đó (DELETE …/guest-invitations)',
        'Mở /guest/{lời mời đã thu hồi}#token=sai',
      ],
      expected:
        'Tiêu đề "Không mở được lời mời", câu "Lời mời không còn hiệu lực. Vui lòng liên hệ Thư ký cuộc họp.", không có ô nhập mã; không phân biệt lý do thu hồi hay mã bịa',
      async run(qa) {
        const { invitationId, meetingId } = await ensureGuestInvitation(qa)
        qa.expect(invitationId, 'không có lời mời [QA] để mượn cuộc họp').toBeTruthy()
        await qa.goto('/calendar')
        const token = await bearer(qa)
        const base = new URL(qa.page.url()).origin
        const post = (path, data, etag) =>
          qa.context.request.post(`${base}${path}`, {
            headers: { Authorization: `Bearer ${token}`, 'If-Match': etag },
            data,
          })
        const parts = await (await qa.api()).get(`/api/meetings/${meetingId}/participants?pageSize=100`)
        const etag = parts.headers.etag ?? `"participants-${parts.body.revision}"`
        const email = `qa-khach-thuhoi-${RUN}@${DOMAIN}`
        const invited = await post(`/api/meetings/${meetingId}/guest-invitations`, { invitedName: qa.name('Khách bị thu hồi'), email }, etag)
        qa.expect(invited.status(), `mời khách phụ: ${await invited.text()}`).toBe(201)
        const extraId = (await invited.json()).createdIds?.[0]
        qa.expect(extraId, 'POST guest-invitations trả thiếu createdIds').toBeTruthy()
        const parts2 = await (await qa.api()).get(`/api/meetings/${meetingId}/participants?pageSize=100`)
        const etag2 = parts2.headers.etag ?? `"participants-${parts2.body.revision}"`
        const revoked = await qa.context.request.delete(`${base}/api/meetings/${meetingId}/guest-invitations/${extraId}`, {
          headers: { Authorization: `Bearer ${token}`, 'If-Match': etag2 },
        })
        qa.expect(revoked.status(), `thu hồi lời mời: ${await revoked.text()}`).toBe(200)
        await qa.goto(`/guest/${extraId}#token=token-sai-qa`)
        await qa.expect(qa.page.getByRole('heading', { name: 'Không mở được lời mời' })).toBeVisible()
        await qa.expect(qa.page.getByRole('alert')).toHaveText(MSG371)
        await qa.expect(qa.page.getByRole('textbox')).toHaveCount(0)
        await settled(qa)
        await qa.shot('loi-moi-da-thu-hoi')
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
      },
    },
  ],
}

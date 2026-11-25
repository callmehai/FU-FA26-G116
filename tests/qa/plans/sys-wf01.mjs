// Report 5.3 System Test — WF-01 System Access and Administration (BF-01).
// docs/08-reports/test-reports/cases/system/WF-01.json describes each case; this plan runs them on the app, in order:
// the account TC01 creates is the subject of every later case.
//
// Safe on a shared local database: it creates one account "[SYS] WF-01 Người dùng mới <run>" on @trustcabinet.local and
// one role "[SYS] WF-01 Kiểm soát viên <run>"; the role is deleted and the account left deactivated at the end.

import {
  PASSWORD,
  RUN,
  as,
  checker,
  closeSeconds,
  dialog,
  mailedMatch,
  noRawCodes,
  noSidewaysScroll,
  secondBrowser,
  settled,
  shotOf,
  sql,
  toast,
} from './sys-lib.mjs'

const state = {
  email: `sys-wf01-${RUN}@trustcabinet.local`,
  fullName: `[SYS] WF-01 Người dùng mới ${RUN}`,
  roleName: `[SYS] WF-01 Kiểm soát viên ${RUN}`,
}

const userRow = (page, email) => page.locator('tbody tr').filter({ has: page.locator('.cell-sub', { hasText: email }) })
const roleRow = (page, name) => page.locator('tbody tr').filter({ has: page.locator('td.cell-title', { hasText: name }) })

async function openUsers(qa, page = qa.page) {
  await page.goto(`/mgmt/admin/users?pageSize=100&q=${encodeURIComponent('sys-wf01')}`)
  await page.waitForLoadState('networkidle').catch(() => undefined)
  await qa.expect(page.getByRole('heading', { name: 'Người dùng', exact: true })).toBeVisible()
  await qa.expect(page.locator('tbody tr.row-skeleton')).toHaveCount(0)
}

async function signInOnScreen(page, email, password) {
  await page.goto('/login')
  await page.getByLabel('Email').fill(email)
  await page.getByLabel('Mật khẩu', { exact: true }).fill(password)
  await page.getByRole('button', { name: 'Đăng nhập bằng mật khẩu' }).click()
}

async function rawLogin(qa, email, password) {
  return (await qa.api('anon')).post('/api/auth/login', { email, password })
}

export default {
  ticket: 'SYS-WF01',
  title: 'WF-01 System Access and Administration',
  devices: ['desktop'],
  criteria: [
    {
      id: 'TC01',
      role: 'admin',
      title: 'Administrator creates an account with role Thành viên',
      async run(qa) {
        await openUsers(qa)
        await qa.page.getByRole('button', { name: 'Tạo tài khoản' }).click()
        const form = dialog(qa.page, 'Tạo tài khoản')
        await form.getByLabel('Họ tên').fill(state.fullName)
        await form.getByRole('textbox', { name: /^Email/ }).fill(state.email)
        await form.getByRole('button', { name: 'Tạo mật khẩu' }).click()
        state.password = await form.getByLabel('Mật khẩu', { exact: true }).inputValue()
        await qa.expect(form.getByRole('button', { name: 'Vai trò: Thành viên' })).toBeVisible()
        await settled(qa.page)
        await qa.shot('form-tao-tai-khoan')
        const created = qa.page.waitForResponse((r) => r.url().endsWith('/api/users') && r.request().method() === 'POST')
        await form.getByRole('button', { name: 'Tạo tài khoản' }).click()
        const res = await created
        qa.expect(res.status()).toBe(201)
        state.userId = (await res.json()).user.id
        await qa.expect(toast(qa.page, 'Tạo tài khoản thành công.')).toBeVisible()
        const row = userRow(qa.page, state.email)
        await qa.expect(row.getByText('Hoạt động')).toBeVisible()
        await qa.expect(row.getByText('Thành viên').filter({ visible: true })).toBeVisible()
        await qa.shot('dong-tai-khoan-moi')
        const audit = sql(`select count(*) from audit_log where action = 'user.created' and object_id = '${state.userId}'`)
        qa.expect(Number(audit[0][0]), 'audit user.created').toBe(1)
        qa.note(`Account ${state.email} (${state.userId})`)
      },
    },
    {
      id: 'TC02',
      role: 'anon',
      title: 'New user signs in by password for the first time and sees only Member functions',
      async run(qa) {
        qa.expect(state.password, 'TC01 did not pass').toBeTruthy()
        await signInOnScreen(qa.page, state.email, state.password)
        await qa.expect(qa.page).toHaveURL(/\/calendar/)
        await qa.expect(qa.page.getByRole('navigation', { name: 'Điều hướng chính' })).toBeVisible()
        await qa.expect(qa.page.getByRole('button', { name: 'Quản trị' })).toHaveCount(0)
        await settled(qa.page)
        await qa.shot('dang-nhap-lan-dau-lich')
        await qa.page.goto('/mgmt/meetings')
        await qa.expect(qa.page.getByRole('link', { name: 'Tạo cuộc họp' }).first()).toBeVisible()
        await qa.page.goto('/mgmt/admin/users')
        await qa.expect(qa.page.getByRole('heading', { name: 'Không tìm thấy trang' })).toBeVisible()
        await qa.shot('quan-tri-404')
        await noSidewaysScroll(qa)
      },
    },
    {
      id: 'TC03',
      role: 'anon',
      title: 'Wrong password is refused with a message, and no session is opened',
      async run(qa) {
        await signInOnScreen(qa.page, state.email, 'SaiMatKhau@2026')
        const alert = qa.page.getByRole('alert').first()
        await qa.expect(alert).toBeVisible()
        state.wrongPasswordText = (await alert.innerText()).trim()
        await qa.expect(qa.page).toHaveURL(/\/login/)
        await qa.shot('sai-mat-khau')
        await noRawCodes(qa)
        qa.note(`Message: ${state.wrongPasswordText}`)
      },
    },
    {
      id: 'TC04',
      role: 'anon',
      title: 'Sign-in with Google (needs a real Google account)',
      async run(qa) {
        await qa.page.goto('/login')
        await qa.shot('trang-dang-nhap-nut-google')
        qa.inconclusive('Google sign-in needs a real Google account in the browser; not run by the automated tester.')
      },
    },
    {
      id: 'TC05',
      role: 'admin',
      title: 'Administrator creates a role without capabilities and assigns it; the signed-in user is told at once',
      async run(qa) {
        const admin = await qa.api('admin')
        // Role screen: create the role with no capability.
        await qa.page.goto('/mgmt/admin/roles')
        await qa.page.getByRole('button', { name: 'Tạo vai trò' }).click()
        const form = dialog(qa.page, 'Tạo vai trò')
        await form.getByLabel('Tên vai trò').fill(state.roleName)
        await form.getByLabel('Mô tả').fill('Vai trò kiểm thử hệ thống WF-01')
        await form.getByRole('button', { name: 'Tạo vai trò' }).click()
        await qa.expect(toast(qa.page, 'Tạo vai trò thành công.')).toBeVisible()
        const roles = (await admin.get('/api/roles')).body
        state.roleId = (roles.items ?? roles).find((r) => r.name === state.roleName).id
        await qa.shot('vai-tro-moi')
        // The user is signed in elsewhere.
        const user = await secondBrowser(qa, state.email, { password: state.password })
        try {
          await user.goto('/calendar')
          await qa.expect(user.getByRole('navigation', { name: 'Điều hướng chính' })).toBeVisible()
          // Assign the role in the account form.
          await openUsers(qa)
          await userRow(qa.page, state.email).getByRole('button', { name: `Sửa tài khoản ${state.fullName}` }).click()
          const edit = dialog(qa.page, 'Sửa tài khoản')
          await edit.getByRole('button', { name: 'Vai trò: Thành viên' }).click()
          await qa.page.getByRole('listbox', { name: 'Vai trò' }).getByRole('option', { name: state.roleName, exact: true }).click()
          await edit.getByRole('button', { name: 'Lưu thay đổi' }).click()
          await qa.expect(toast(qa.page, 'Cập nhật tài khoản thành công.')).toBeVisible()
          await qa.expect(userRow(qa.page, state.email).getByText(state.roleName).filter({ visible: true })).toBeVisible()
          await qa.shot('gan-vai-tro')
          await qa.expect(toast(user, 'Quyền truy cập của bạn vừa được cập nhật.')).toBeVisible({ timeout: 15_000 })
          await shotOf(qa, user, 'nguoi-dung-duoc-bao-doi-quyen', 'user')
          await user.goto('/mgmt/meetings')
          await qa.expect(user.getByRole('heading', { name: 'Danh sách cuộc họp' })).toBeVisible()
          await qa.expect(user.getByRole('link', { name: 'Tạo cuộc họp' })).toHaveCount(0)
          await shotOf(qa, user, 'khong-con-nut-tao-cuoc-hop', 'user')
        } finally {
          await closeSeconds(qa)
        }
      },
    },
    {
      id: 'TC06',
      role: 'admin',
      title: 'Administrator grants a capability on the matrix; the user gains it without signing in again, then loses it',
      async run(qa) {
        qa.expect(state.roleId, 'TC05 did not pass').toBeTruthy()
        const user = await secondBrowser(qa, state.email, { password: state.password })
        try {
          await user.goto('/mgmt/meetings')
          await qa.expect(user.getByRole('heading', { name: 'Danh sách cuộc họp' })).toBeVisible()
          await qa.page.goto('/mgmt/admin/roles')
          await qa.page.getByRole('tab', { name: /Ma trận phân quyền/ }).click()
          const box = qa.page.getByRole('checkbox', { name: `${state.roleName} — Tạo cuộc họp` })
          await box.scrollIntoViewIfNeeded()
          await box.check()
          const row = roleRow(qa.page, state.roleName)
          await row.getByRole('button', { name: 'Lưu' }).click()
          await qa.expect(toast(qa.page, 'Cập nhật phân quyền thành công.')).toBeVisible()
          await qa.shot('ma-tran-cap-tao-cuoc-hop')
          await qa.expect(user.getByRole('link', { name: 'Tạo cuộc họp' }).first()).toBeVisible({ timeout: 15_000 })
          await shotOf(qa, user, 'hien-nut-tao-cuoc-hop-khong-tai-lai', 'user')
          // Remove it again.
          await box.uncheck()
          await row.getByRole('button', { name: 'Lưu' }).click()
          await qa.expect(toast(qa.page, 'Cập nhật phân quyền thành công.')).toBeVisible()
          await qa.expect(user.getByRole('link', { name: 'Tạo cuộc họp' })).toHaveCount(0, { timeout: 15_000 })
          await shotOf(qa, user, 'mat-nut-tao-cuoc-hop', 'user')
          const login = await user.request.post('/api/auth/login', { data: { email: state.email, password: state.password } })
          const token = (await login.json()).accessToken
          const res = await user.request.post('/api/meetings', { headers: { Authorization: `Bearer ${token}` }, data: {} })
          qa.expect(res.status(), 'POST /api/meetings without MeetingCreator').toBe(403)
          const audit = sql(`select count(*) from audit_log where action = 'role.permissions.updated' and object_id = '${state.roleId}'`)
          qa.expect(Number(audit[0][0]), 'two role.permissions.updated entries').toBe(2)
        } finally {
          await closeSeconds(qa)
        }
      },
    },
    {
      id: 'TC07',
      role: 'admin',
      title: 'Administrator deactivates the account; the open session ends and sign-in is refused',
      async run(qa) {
        qa.expect(state.userId, 'TC01 did not pass').toBeTruthy()
        const user = await secondBrowser(qa, state.email, { password: state.password })
        try {
          await user.goto('/calendar')
          await qa.expect(user.getByRole('navigation', { name: 'Điều hướng chính' })).toBeVisible()
          await openUsers(qa)
          const row = userRow(qa.page, state.email)
          await row.getByRole('button', { name: `Vô hiệu hoá tài khoản ${state.fullName}` }).click()
          const confirm = dialog(qa.page, 'Vô hiệu hoá tài khoản')
          await qa.expect(confirm).toContainText(state.fullName)
          await qa.shot('hoi-lai-vo-hieu-hoa')
          await confirm.getByRole('button', { name: 'Vô hiệu hoá', exact: true }).click()
          await qa.expect(toast(qa.page, 'Đổi trạng thái tài khoản thành công.')).toBeVisible()
          await qa.expect(row.getByText('Đã vô hiệu hoá').filter({ visible: true })).toBeVisible()
          await qa.shot('da-vo-hieu-hoa')
          // The user's browser: the next screen asks for sign-in again.
          await user.goto('/mgmt/meetings')
          await qa.expect(user).toHaveURL(/\/login/, { timeout: 15_000 })
          await shotOf(qa, user, 'phien-cu-ve-dang-nhap', 'user')
          await signInOnScreen(user, state.email, state.password)
          const alert = user.getByRole('alert').first()
          await qa.expect(alert).toContainText('Tài khoản không khả dụng. Vui lòng liên hệ quản trị viên.')
          await shotOf(qa, user, 'dang-nhap-bi-tu-choi', 'user')
        } finally {
          await closeSeconds(qa)
        }
      },
    },
    {
      id: 'TC08',
      role: 'admin',
      title: 'Administrator reactivates the account; the user signs in again',
      async run(qa) {
        await openUsers(qa)
        const row = userRow(qa.page, state.email)
        await row.getByRole('button', { name: `Kích hoạt lại tài khoản ${state.fullName}` }).click()
        const again = dialog(qa.page, 'Kích hoạt lại tài khoản')
        await again.getByRole('button', { name: 'Kích hoạt lại', exact: true }).click()
        await qa.expect(toast(qa.page, 'Đổi trạng thái tài khoản thành công.')).toBeVisible()
        await qa.expect(row.getByText('Hoạt động')).toBeVisible()
        await qa.shot('da-kich-hoat-lai')
        const user = await secondBrowser(qa, state.email, { signIn: false })
        try {
          await signInOnScreen(user, state.email, state.password)
          await qa.expect(user).toHaveURL(/\/calendar/)
          await shotOf(qa, user, 'dang-nhap-lai-duoc', 'user')
        } finally {
          await closeSeconds(qa)
        }
        const c = checker(qa)
        const audit = sql(
          `select action from audit_log where object_id = '${state.userId}' and action in ('user.deactivated','user.reactivated') order by created_at`,
        ).map((r) => r[0])
        c.fact('audit deactivated then reactivated', JSON.stringify(audit) === JSON.stringify(['user.deactivated', 'user.reactivated']), JSON.stringify(audit))
        c.done()
      },
    },
    {
      id: 'TC09',
      role: 'anon',
      title: 'Sixth sign-in attempt for one email within a minute is throttled; the correct password works after the window',
      async run(qa) {
        // Development raises the limits to 1000 (appsettings.Development.json) so automated runs never trip them; this
        // case runs on a second instance of the same build with the shipped limits (5 per email, 60 per IP, 60 s).
        const base = process.env.SYS_LIMITED_BASE ?? 'http://localhost:5226'
        const page = await secondBrowser(qa, state.email, { signIn: false, base })
        try {
          const seen = []
          const t0 = Date.now()
          for (let i = 1; i <= 6; i++) {
            await signInOnScreen(page, state.email, `Sai${i}@MatKhau2026`)
            const alert = page.getByRole('alert').first()
            await qa.expect(alert).toBeVisible()
            seen.push((await alert.innerText()).trim())
            if (i === 5) await shotOf(qa, page, 'lan-thu-5-sai-mat-khau', 'anon')
          }
          await shotOf(qa, page, 'lan-thu-6-bi-chan', 'anon')
          const elapsed = Math.round((Date.now() - t0) / 1000)
          qa.note(`5th: ${seen[4]} | 6th: ${seen[5]} | 6 attempts in ${elapsed}s`)
          qa.expect(seen.slice(0, 5).every((t) => t === 'Email hoặc mật khẩu không đúng.'), JSON.stringify(seen)).toBe(true)
          qa.expect(seen[5]).toContain('Bạn đã thử quá nhiều lần')
          const res = await page.request.post(`${base}/api/auth/login`, { data: { email: state.email, password: state.password } })
          qa.expect(res.status(), 'correct password inside the window').toBe(429)
          qa.expect((await res.json()).code).toBe('MSG414')
          const retryAfter = Number(res.headers()['retry-after'])
          qa.expect(retryAfter).toBeGreaterThan(0)
          qa.note(`Correct password inside the window: 429 MSG414, Retry-After ${retryAfter}s`)
          await new Promise((r) => setTimeout(r, (retryAfter + 2) * 1000))
          await signInOnScreen(page, state.email, state.password)
          await qa.expect(page).toHaveURL(/\/calendar/)
          await shotOf(qa, page, 'sau-khi-het-cua-so-dang-nhap-duoc', 'anon')
        } finally {
          await closeSeconds(qa)
        }
      },
    },
    {
      id: 'CLEANUP',
      role: 'admin',
      title: 'Cleanup: account back to Thành viên and deactivated, role deleted',
      async run(qa) {
        const admin = await qa.api('admin')
        const roles = (await admin.get('/api/roles')).body
        const member = (roles.items ?? roles).find((r) => r.name === 'Thành viên')
        if (state.userId) {
          await admin.put(`/api/users/${state.userId}`, { fullName: state.fullName, email: state.email, roleId: member.id })
          await admin.put(`/api/users/${state.userId}/status`, { isActive: false })
        }
        if (state.roleId) qa.expectStatus(await admin.delete(`/api/roles/${state.roleId}`), 200)
      },
    },
    {
      id: 'TC10',
      role: 'admin',
      title: 'The Administrator sends a reset link; the user sets a new password from the email; the link works once',
      async run(qa) {
        // Precondition (API): an account of its own, so resetting it changes no seed password. The reset email reaches
        // the local Mailpit inbox (sys-lib, the backend's SMTP pointed at it), the way the user reads their mailbox.
        const admin = await qa.api('admin')
        const roles = (await admin.get('/api/roles')).body
        const member = (roles.items ?? roles).find((r) => r.name === 'Thành viên')
        const user = { fullName: `Trần Đặt Lại ${RUN}`, email: `sys-wf01-reset-${RUN}@trustcabinet.local` }
        qa.expectStatus(await admin.post('/api/users', { ...user, password: PASSWORD, roleId: member.id }), 201)
        const NEW = `Mật khẩu mới ${RUN} 2026!`
        await qa.goto(`/mgmt/admin/users?q=${encodeURIComponent(user.email)}`)
        const send = qa.page.getByRole('button', { name: `Gửi liên kết đặt lại mật khẩu cho ${user.fullName}` })
        await qa.expect(send).toBeVisible()
        await send.click()
        const d = dialog(qa.page, 'Đặt lại mật khẩu')
        await qa.expect(d).toContainText(user.email)
        await qa.expect(d).toContainText('Liên kết dùng được một lần trong 60 phút')
        await qa.shot('hoi-gui-lien-ket')
        const since = Date.now()
        const sent = qa.page.waitForResponse((r) => r.url().includes('/password-reset') && r.request().method() === 'POST')
        await d.getByRole('button', { name: 'Gửi liên kết', exact: true }).click()
        const res = await sent
        qa.expect((await res.json()).code).toBe('MSG13')
        await qa.expect(toast(qa.page, 'Đã gửi email đặt lại mật khẩu.')).toBeVisible()
        await qa.shot('da-gui-email')
        const mail = await mailedMatch(user.email, since, /(\/reset-password#token=[A-Za-z0-9_-]+)/)
        const page = await secondBrowser(qa, 'anon', { signIn: false })
        try {
          await page.goto(mail.view)
          await qa.expect(page.getByText('Liên kết có hiệu lực trong 60 phút và chỉ dùng được một lần.')).toBeVisible()
          await shotOf(qa, page, 'email-dat-lai-mat-khau', 'user')
          await page.goto(mail.match)
          await qa.expect(page.getByRole('heading', { level: 1, name: 'Đặt lại mật khẩu' })).toBeVisible()
          await page.getByLabel('Mật khẩu mới', { exact: true }).fill(NEW)
          await page.getByLabel('Nhập lại mật khẩu mới').fill(NEW)
          await shotOf(qa, page, 'nhap-mat-khau-moi', 'user')
          const reset = page.waitForResponse((r) => r.url().endsWith('/api/auth/password-reset') && r.request().method() === 'POST')
          await page.getByRole('button', { name: 'Đặt mật khẩu mới', exact: true }).click()
          qa.expect((await (await reset).json()).code).toBe('MSG92')
          await qa.expect(toast(page, 'Đặt lại mật khẩu thành công. Vui lòng đăng nhập bằng mật khẩu mới.')).toBeVisible()
          await shotOf(qa, page, 'dat-lai-thanh-cong', 'user')
          const oldLogin = await rawLogin(qa, user.email, PASSWORD)
          const newLogin = await rawLogin(qa, user.email, NEW)
          qa.expect(oldLogin.status, 'the old password still signs in').not.toBe(200)
          qa.expect(newLogin.status, 'the new password does not sign in').toBe(200)
          // The same link a second time.
          await page.goto('about:blank')
          await page.goto(mail.match)
          await page.getByLabel('Mật khẩu mới', { exact: true }).fill(`${NEW}x`)
          await page.getByLabel('Nhập lại mật khẩu mới').fill(`${NEW}x`)
          const again = page.waitForResponse((r) => r.url().endsWith('/api/auth/password-reset') && r.request().method() === 'POST')
          await page.getByRole('button', { name: 'Đặt mật khẩu mới', exact: true }).click()
          const reused = await again
          qa.expect(reused.status()).toBe(400)
          qa.expect((await reused.json()).code).toBe('MSG93')
          await qa.expect(page.getByRole('alert')).toBeVisible()
          await shotOf(qa, page, 'lien-ket-dung-lai-bi-tu-choi', 'user')
          qa.note(`Old password sign-in ${oldLogin.status}; new password ${newLogin.status}; reused link 400 MSG93`)
        } finally {
          await closeSeconds(qa)
        }
      },
    },
  ],
}

// FE-02 — Quản lý Người dùng & Hồ sơ (QA subtask SCRUM-189): UC-05 xem danh sách tài khoản, UC-06a tạo, UC-06b sửa,
// UC-06c vô hiệu hoá / kích hoạt lại, UC-07 đặt lại mật khẩu (gửi liên kết và trang /reset-password), UC-08 gán / đổi
// vai trò, UC-09 cũ mở khoá tài khoản (docs/uc-map.html maps exactly these UCs to FE-02).
//
// Criteria come from docs/01-requirements/05-use-case-specifications.md (UC-05..UC-08), BR-005, BR-006, BR-009, BR-001, BR-008,
// the API docs 02-quan-ly-nguoi-dung.md (+ note cho FE), the message catalogue and the SCRUM-13 comments (BA Final
// Review 03/10: a Google-only account cannot reset; an email change ends the sessions; 403 on /api/users is intended).
//
// Safe on the shared local app: it never edits a seed account or a built-in role. It works on its own accounts,
// qa-fe02-a@ / qa-fe02-b@ / qa-fe02-c@trustcabinet.local, reused across runs and put back to a known state at the start
// of each criterion. AC3 creates one new account per run (accounts cannot be deleted) and deactivates it at once.
// Every address is @trustcabinet.local, which the backend never mails (Email:UndeliverableDomains), so UC-07's send
// answers MSG15 locally and the success path is left to a human with a real mailbox.

const PASSWORD = process.env.QA_PASSWORD ?? 'TrustCabinet@2026'
const USERS_PATH = '/mgmt/admin/users'
const RUN = Date.now().toString(36).slice(-5)
const UNKNOWN_ID = '01a0ce78-0000-7000-8000-000000000000'
// Fixed ids of the built-in roles (docs/03-api/04-quan-ly-vai-tro.md §2).
const ROLE = {
  chair: { id: '01a0ce78-7202-7945-be13-d2022cf25203', name: 'Chủ toạ' },
  secretary: { id: '01a0ce78-7203-7c7f-8326-7a0baebf4c22', name: 'Thư ký' },
  member: { id: '01a0ce78-7204-7558-b181-e908a08e83a3', name: 'Thành viên' },
}
// The plan's own accounts. `name` goes through qa.name(), so it reads "[QA] FE-02 …".
const QA_USERS = {
  a: { email: 'qa-fe02-a@trustcabinet.local', label: 'FE-02 Người dùng A' },
  b: { email: 'qa-fe02-b@trustcabinet.local', label: 'FE-02 Người dùng B' },
  c: { email: 'qa-fe02-c@trustcabinet.local', label: 'FE-02 Người dùng C' },
}

// ---------------------------------------------------------------------------------------------------------------------
// helpers

async function listUsers(qa, api) {
  const res = await api.get('/api/users')
  qa.expectStatus(res, 200)
  return res.body
}

/**
 * Puts one of the plan's accounts in a known state — this name, Thành viên (or `roleId`), active or not — creating it
 * the first time. Returns the account as GET /api/users shows it.
 */
async function ensureUser(qa, api, key, { active = true, roleId = ROLE.member.id } = {}) {
  const { email, label } = QA_USERS[key]
  const fullName = qa.name(label)
  let user = (await listUsers(qa, api)).find((u) => u.email === email)
  if (!user) {
    const res = await api.post('/api/users', { fullName, email, password: PASSWORD, roleId })
    qa.expectStatus(res, 201)
    user = res.body.user
  }
  if (user.fullName !== fullName || user.roleId !== roleId) {
    const res = await api.put(`/api/users/${user.id}`, { fullName, email, roleId })
    qa.expectStatus(res, 200)
    user = res.body.user
  }
  if (user.isActive !== active) {
    const res = await api.put(`/api/users/${user.id}/status`, { isActive: active })
    qa.expectStatus(res, 200)
    user = res.body.user
  }
  return user
}

/** Audit entries about one account (BR-009), newest first. */
async function auditFor(qa, api, userId, action) {
  const params = new URLSearchParams({ action, pageSize: '50' })
  const res = await api.get(`/api/audit-log?${params}`)
  qa.expectStatus(res, 200)
  return res.body.items.filter((e) => e.objectId === userId)
}

/** A raw sign-in, so a criterion can see the answer (MSG02…) instead of the runner throwing. */
async function rawLogin(qa, email, password = PASSWORD) {
  const anon = await qa.api('anon')
  return anon.post('/api/auth/login', { email, password })
}

async function noSidewaysScroll(qa) {
  const sideways = await qa.page.evaluate(() => document.documentElement.scrollWidth - innerWidth)
  qa.expect(sideways, 'the page scrolls sideways').toBeLessThanOrEqual(0)
}

async function settled(qa) {
  await qa.page.waitForFunction(() => document.getAnimations().every((a) => a.playState !== 'running'))
}

async function noRawCodes(qa) {
  const text = await qa.page.locator('body').innerText()
  qa.expect(text, 'a raw MSG code is shown to the user').not.toMatch(/MSG\d+/)
}

/** The element's box lies inside the viewport horizontally (reachable without a sideways scroll). */
async function inViewportX(qa, locator) {
  const box = await locator.boundingBox()
  qa.expect(box, 'element has no box').not.toBeNull()
  const width = qa.page.viewportSize().width
  qa.expect(box.x, 'element starts left of the screen').toBeGreaterThanOrEqual(0)
  qa.expect(box.x + box.width, 'element runs past the right edge').toBeLessThanOrEqual(width + 1)
}

const toast = (qa, text) => qa.page.getByRole('status').getByText(text)

/** Opens the users screen and resolves to the GET /api/users body the page itself received. */
async function openUsers(qa, search) {
  const response = qa.page.waitForResponse((r) => r.url().endsWith('/api/users') && r.request().method() === 'GET')
  response.catch(() => undefined)
  // 100 rows a page (the largest choice), so every QA account sits on the first page of the local data.
  await qa.goto(`${USERS_PATH}?pageSize=100${search ? `&${search}` : ''}`)
  const body = await (await response).json()
  await qa.expect(qa.page.getByRole('heading', { name: 'Người dùng', exact: true })).toBeVisible()
  await qa.expect(qa.page.locator('tbody tr.row-skeleton')).toHaveCount(0)
  return body
}

/** The data row of the users table for an email. */
const userRow = (qa, email) => qa.page.locator('tbody tr').filter({ has: qa.page.locator('.cell-sub', { hasText: email }) })

/** The page's search box: typing filters at once, the URL follows after a pause. */
const searchBox = (qa) => qa.page.getByRole('searchbox', { name: 'Tìm theo họ tên hoặc email' })

async function pickOption(qa, triggerName, listName, option) {
  await qa.page.getByRole('button', { name: triggerName }).click()
  await qa.page.getByRole('listbox', { name: listName }).getByRole('option', { name: option, exact: true }).click()
}

/** Signs `who` in through the API in a separate client and returns it: its refresh cookie lives in that client. */
async function sessionOf(qa, email) {
  const client = await qa.api(email)
  qa.expectStatus(await client.get('/api/auth/me'), 200)
  return client
}

const DENIED = ['chair', 'secretary', 'member']

// ---------------------------------------------------------------------------------------------------------------------

export default {
  ticket: 'SCRUM-189',
  title: 'FE-02 Quản lý Người dùng & Hồ sơ: danh sách, tạo, sửa, vô hiệu hoá, đặt lại mật khẩu, gán vai trò, mở khoá',
  devices: ['desktop'],
  criteria: [
    // ---------------------------------------------------------------- UC-05
    {
      id: 'AC1',
      role: 'admin',
      devices: ['desktop', 'phone'],
      title: 'Quản trị mở Người dùng: mọi tài khoản hiện đúng họ tên, email, vai trò, trạng thái, theo thứ tự backend (UC-05)',
      steps: ['Đăng nhập quantri@ (có AdminUsers)', 'Bấm "Người dùng" ở sidebar (máy tính) hoặc mở /mgmt/admin/users'],
      expected:
        'Bảng có đúng số dòng như GET /api/users trang nhận, cùng thứ tự; dòng đầu trang nói "N tài khoản, M đang hoạt động"; mỗi dòng có họ tên, email, vai trò, trạng thái; không lộ mật khẩu hay mã băm (NFR); điện thoại: không cuộn ngang, vai trò hiện dưới email, nút thao tác trong màn hình',
      async run(qa) {
        const api = await qa.api()
        await ensureUser(qa, api, 'a')
        await ensureUser(qa, api, 'c', { active: false })
        let users
        if (qa.device === 'desktop') {
          await qa.goto('/calendar')
          const link = qa.page.getByRole('link', { name: 'Người dùng', exact: true })
          if (!(await link.isVisible())) await qa.page.getByRole('button', { name: 'Quản trị' }).click()
          const response = qa.page.waitForResponse((r) => r.url().endsWith('/api/users') && r.request().method() === 'GET')
          response.catch(() => undefined)
          await link.click()
          await qa.expect(qa.page).toHaveURL(new RegExp(`${USERS_PATH}$`))
          users = await (await response).json()
          await qa.expect(qa.page.locator('tbody tr.row-skeleton')).toHaveCount(0)
        } else {
          // The default page size (20), as a person opening the list sees it.
          const response = qa.page.waitForResponse((r) => r.url().endsWith('/api/users') && r.request().method() === 'GET')
          response.catch(() => undefined)
          await qa.goto(USERS_PATH)
          users = await (await response).json()
          await qa.expect(qa.page.locator('tbody tr.row-skeleton')).toHaveCount(0)
        }
        for (const u of users) {
          qa.expect(Object.keys(u).filter((k) => /hash|subject|token/i.test(k) || /^password$/i.test(k)), 'credential field on the wire').toEqual([])
        }
        const active = users.filter((u) => u.isActive).length
        await qa.expect(qa.page.getByText(`${users.length} tài khoản, ${active} đang hoạt động`)).toBeVisible()
        // The list pages in the browser (all accounts come in one response): 20 rows by default, in the backend's order.
        const firstPage = users.slice(0, 20)
        await qa.expect(qa.page.locator('tbody tr')).toHaveCount(firstPage.length)
        const emails = await qa.page.locator('tbody tr .cell-sub').allInnerTexts()
        qa.expect(emails, 'rows are not in the order the backend sent').toEqual(firstPage.map((u) => u.email))
        if (users.length > 20) {
          await qa.expect(qa.page.getByRole('navigation', { name: 'Phân trang' })).toContainText(`trong ${users.length.toLocaleString('vi-VN')} tài khoản`)
        }
        users = await openUsers(qa)
        if (qa.device === 'desktop') {
          for (const col of ['Họ tên', 'Vai trò', 'Đăng nhập', 'Trạng thái', 'Ngày tạo', 'Thao tác']) {
            await qa.expect(qa.page.getByRole('columnheader', { name: col })).toBeVisible()
          }
        }
        const a = users.find((u) => u.email === QA_USERS.a.email)
        const rowA = userRow(qa, QA_USERS.a.email)
        await qa.expect(rowA.getByText(a.fullName)).toBeVisible()
        await qa.expect(rowA.getByText(ROLE.member.name).filter({ visible: true })).toBeVisible()
        const rowC = userRow(qa, QA_USERS.c.email)
        await qa.expect(rowC.getByText('Đã vô hiệu hoá').filter({ visible: true })).toBeVisible()
        await qa.shot('danh-sach')
        await rowA.scrollIntoViewIfNeeded()
        // The actions column must be on screen without scrolling the table sideways (check-responsive).
        const table = await qa.page.locator('.table-card').evaluate((card) => ({
          hidden: card.scrollWidth - card.clientWidth,
          widest: [...card.querySelectorAll('tbody td.cell-title .cell-sub')]
            .map((el) => el.textContent)
            .sort((x, y) => y.length - x.length)[0],
        }))
        qa.expect(
          table.hidden,
          `the users table scrolls sideways by ${table.hidden}px at ${qa.page.viewportSize().width}px, pushing "Ngày tạo" and "Thao tác" out of view (longest email: ${table.widest})`,
        ).toBeLessThanOrEqual(0)
        await inViewportX(qa, rowA.getByRole('button', { name: `Sửa tài khoản ${a.fullName}` }))
        await inViewportX(qa, rowA.getByRole('button', { name: `Gửi liên kết đặt lại mật khẩu cho ${a.fullName}` }))
        await qa.shot('dong-qa-a')
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
      },
    },
    {
      id: 'AC2',
      role: 'admin',
      devices: ['desktop', 'phone'],
      title: 'Tìm theo tên/email, lọc theo trạng thái và vai trò, không khớp thì báo "không có tài khoản nào khớp" + "Xoá bộ lọc"; bộ lọc giữ khi tải lại (UC-05 AF-1..3)',
      steps: [
        'Gõ "nguoi dung a" (không dấu) vào ô tìm',
        'Xoá ô tìm, lọc "Đã vô hiệu hoá", rồi lọc thêm vai trò Thành viên',
        'Tải lại trang',
        'Gõ một chuỗi không khớp ai, bấm "Xoá bộ lọc"',
      ],
      expected:
        'Tìm không dấu ra đúng tài khoản [QA] FE-02 Người dùng A; lọc trạng thái/vai trò ra đúng số tài khoản như dữ liệu trang nhận; tải lại vẫn giữ bộ lọc (URL); không khớp: "Không có tài khoản nào khớp bộ lọc." chứ không phải lỗi; "Xoá bộ lọc" trả lại danh sách, xoá ô tìm và hết tham số lọc trên URL (USER_FT05 E2)',
      async run(qa) {
        const api = await qa.api()
        await ensureUser(qa, api, 'a')
        await ensureUser(qa, api, 'c', { active: false })
        let users = await openUsers(qa)

        await searchBox(qa).fill('nguoi dung a')
        await qa.expect(userRow(qa, QA_USERS.a.email)).toBeVisible()
        const fold = (s) => s.normalize('NFD').replace(/\p{M}/gu, '').replace(/đ/gi, 'd').toLowerCase()
        const expectSearch = users.filter((u) => fold(u.fullName).includes('nguoi dung a') || fold(u.email).includes('nguoi dung a'))
        await qa.expect(qa.page.locator('tbody tr')).toHaveCount(expectSearch.length)
        await qa.shot('tim-khong-dau')

        await searchBox(qa).fill('')
        await pickOption(qa, /^Lọc theo trạng thái/, 'Lọc theo trạng thái', 'Đã vô hiệu hoá')
        const inactive = users.filter((u) => !u.isActive)
        await qa.expect(qa.page.locator('tbody tr')).toHaveCount(inactive.length)
        await qa.expect(userRow(qa, QA_USERS.c.email)).toBeVisible()
        await pickOption(qa, /^Lọc theo vai trò/, 'Lọc theo vai trò', ROLE.member.name)
        const inactiveMembers = inactive.filter((u) => u.roleId === ROLE.member.id)
        await qa.expect(qa.page.locator('tbody tr')).toHaveCount(inactiveMembers.length)
        await qa.expect(qa.page).toHaveURL(/status=inactive/)
        // The box was cleared before the filters were picked: the URL (what a reload or a shared link restores) must agree.
        qa.expect(new URL(qa.page.url()).searchParams.get('q'), 'the cleared search is still in the URL').toBeNull()
        await qa.expect(qa.page).toHaveURL(new RegExp(`role=${ROLE.member.id}`))
        await qa.shot('loc-trang-thai-vai-tro')

        const reloaded = qa.page.waitForResponse((r) => r.url().endsWith('/api/users') && r.request().method() === 'GET')
        await qa.page.reload()
        users = await (await reloaded).json()
        await qa.expect(qa.page.locator('tbody tr.row-skeleton')).toHaveCount(0)
        await qa.expect(qa.page.getByRole('button', { name: /^Lọc theo trạng thái: Đã vô hiệu hoá/ })).toBeVisible()
        await qa.expect(qa.page.locator('tbody tr')).toHaveCount(
          users.filter((u) => !u.isActive && u.roleId === ROLE.member.id).length,
        )

        await searchBox(qa).fill(`khong-ai-co-ten-nay-${RUN}`)
        await qa.expect(qa.page.getByText('Không có tài khoản nào khớp bộ lọc.')).toBeVisible()
        await qa.shot('khong-khop')
        // USER_FT05 E2: "Xoá bộ lọc" restores everything and clears the URL.
        await qa.page.getByRole('button', { name: 'Xoá bộ lọc' }).click()
        await qa.expect(searchBox(qa)).toHaveValue('')
        await qa.expect(qa.page.locator('tbody tr')).toHaveCount(users.length)
        const params = new URL(qa.page.url()).searchParams
        for (const key of ['q', 'role', 'status']) qa.expect(params.get(key), `?${key} survived "Xoá bộ lọc"`).toBeNull()
        await qa.shot('da-xoa-bo-loc')
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
      },
    },

    // ---------------------------------------------------------------- UC-06a
    {
      id: 'AC3',
      role: 'admin',
      title: 'Tạo tài khoản qua form: báo "Tạo tài khoản thành công.", dòng mới ở trạng thái Hoạt động, đăng nhập được, có nhật ký (UC-06a, BR-006, BR-009)',
      steps: [
        'Bấm "Tạo tài khoản"',
        'Nhập họ tên, email mới, bấm "Tạo mật khẩu", giữ vai trò mặc định',
        'Bấm "Tạo tài khoản"',
        'Đăng nhập bằng email và mật khẩu vừa tạo',
      ],
      expected:
        'Toast MSG07 "Tạo tài khoản thành công."; hộp thoại đóng; danh sách có dòng mới "Hoạt động", vai trò Thành viên; đăng nhập được; nhật ký có user.created',
      async run(qa) {
        const api = await qa.api()
        const fullName = qa.name(`FE-02 Tạo ${RUN}`)
        const email = `qa-fe02-tao-${RUN}@trustcabinet.local`
        let createdId = null
        try {
          await openUsers(qa)
          await qa.page.getByRole('button', { name: 'Tạo tài khoản' }).click()
          const dialog = qa.page.getByRole('dialog', { name: 'Tạo tài khoản' })
          await qa.expect(dialog).toBeVisible()
          await dialog.getByLabel('Họ tên').fill(fullName)
          await dialog.getByRole('textbox', { name: /^Email/ }).fill(email)
          await dialog.getByRole('button', { name: 'Tạo mật khẩu' }).click()
          const password = await dialog.getByLabel('Mật khẩu', { exact: true }).inputValue()
          qa.expect(password.length, 'generated password is shorter than 12').toBeGreaterThanOrEqual(12)
          await qa.expect(dialog.getByRole('button', { name: `Vai trò: ${ROLE.member.name}` })).toBeVisible()
          // USER_FT11 E2/E3: the generated password shows in plain text, can be hidden, copied, and has a hint.
          await qa.expect(dialog.getByLabel('Mật khẩu', { exact: true })).toHaveAttribute('type', 'text')
          await qa.expect(dialog.getByText('Tối thiểu 12 ký tự.')).toBeVisible()
          await dialog.getByRole('button', { name: 'Ẩn mật khẩu' }).click()
          await qa.expect(dialog.getByLabel('Mật khẩu', { exact: true })).toHaveAttribute('type', 'password')
          await dialog.getByRole('button', { name: 'Hiện mật khẩu' }).click()
          await qa.expect(dialog.getByLabel('Mật khẩu', { exact: true })).toHaveAttribute('type', 'text')
          await qa.page.context().grantPermissions(['clipboard-read', 'clipboard-write'])
          await dialog.getByRole('button', { name: 'Sao chép mật khẩu' }).click()
          await qa.expect(toast(qa, 'Đã sao chép mật khẩu.')).toBeVisible()
          await qa.shot('mat-khau-hien-sao-chep')
          await qa.shot('form-tao')
          const created = qa.page.waitForResponse((r) => r.url().endsWith('/api/users') && r.request().method() === 'POST')
          await dialog.getByRole('button', { name: 'Tạo tài khoản' }).click()
          const res = await created
          qa.expect(res.status()).toBe(201)
          createdId = (await res.json()).user.id
          await qa.expect(toast(qa, 'Tạo tài khoản thành công.')).toBeVisible()
          await qa.expect(dialog).toBeHidden()
          const row = userRow(qa, email)
          await qa.expect(row).toBeVisible()
          await qa.expect(row.getByText('Hoạt động')).toBeVisible()
          await qa.expect(row.getByText(ROLE.member.name).filter({ visible: true })).toBeVisible()
          await row.scrollIntoViewIfNeeded()
          await qa.shot('dong-moi')

          const login = await rawLogin(qa, email, password)
          qa.expectStatus(login, 200)
          const entries = await auditFor(qa, api, createdId, 'user.created')
          qa.expect(entries.length, 'no user.created audit entry').toBeGreaterThan(0)
          qa.expect(JSON.stringify(entries[0].details ?? {}), 'password in the audit details').not.toContain(password)
        } finally {
          // Accounts cannot be deleted: leave this one deactivated.
          if (createdId) await api.put(`/api/users/${createdId}/status`, { isActive: false })
        }
      },
    },
    {
      id: 'AC4',
      role: 'admin',
      devices: ['desktop', 'phone'],
      title: 'Tạo tài khoản sai: mọi ô sai báo cùng lúc; email đã có (khác hoa thường) báo "Email đã được sử dụng.", không tạo gì (UC-06a EX-1, EX-2, BR-005, UC-06b)',
      steps: [
        'Mở "Tạo tài khoản", để trống, bấm "Tạo tài khoản"',
        'Nhập họ tên, email = QA-FE02-A@… (đã có, viết hoa), mật khẩu 12 ký tự, bấm "Tạo tài khoản"',
        'API: POST /api/users thiếu hết; POST trùng email',
      ],
      expected:
        'Ba lỗi dưới ô: "Họ tên không được để trống.", "Email không được để trống.", "Mật khẩu phải có ít nhất 12 ký tự."; trùng email: "Email đã được sử dụng." dưới ô Email, hộp thoại còn mở; API 400 MSG09 kèm errors theo field, 409 MSG08; số tài khoản không đổi; điện thoại: hộp thoại vừa màn hình, nút bấm được',
      async run(qa) {
        const api = await qa.api()
        await ensureUser(qa, api, 'a')
        await openUsers(qa)
        await qa.page.getByRole('button', { name: 'Tạo tài khoản' }).click()
        const dialog = qa.page.getByRole('dialog', { name: 'Tạo tài khoản' })
        await dialog.getByRole('button', { name: 'Tạo tài khoản' }).click()
        await qa.expect(dialog.getByText('Họ tên không được để trống.')).toBeVisible()
        await qa.expect(dialog.getByText('Email không được để trống.')).toBeVisible()
        await qa.expect(dialog.getByText('Mật khẩu phải có ít nhất 12 ký tự.')).toBeVisible()
        await qa.shot('loi-tung-o')

        await dialog.getByLabel('Họ tên').fill(qa.name(`FE-02 Trùng ${RUN}`))
        await dialog.getByRole('textbox', { name: /^Email/ }).fill(QA_USERS.a.email.toUpperCase())
        await dialog.getByLabel('Mật khẩu', { exact: true }).fill('MotHaiBaBon@12')
        const posted = qa.page.waitForResponse((r) => r.url().endsWith('/api/users') && r.request().method() === 'POST')
        await dialog.getByRole('button', { name: 'Tạo tài khoản' }).click()
        qa.expect((await posted).status()).toBe(409)
        await qa.expect(dialog.getByText('Email đã được sử dụng.')).toBeVisible()
        await qa.expect(dialog).toBeVisible()
        const submit = dialog.getByRole('button', { name: 'Tạo tài khoản' })
        await submit.scrollIntoViewIfNeeded()
        await inViewportX(qa, submit)
        await qa.shot('email-trung')
        await noSidewaysScroll(qa)
        await noRawCodes(qa)

        // The administrator must read the generated password to pass it on: the field has to show all of it.
        await dialog.getByRole('button', { name: 'Tạo mật khẩu' }).click()
        const field = dialog.getByLabel('Mật khẩu', { exact: true })
        await qa.expect(field).toHaveAttribute('type', 'text')
        await qa.shot('mat-khau-tao-san')
        // USER_FT12 E2: an invalid email is marked, focused, and clears as soon as it is edited.
        const emailBox = dialog.getByRole('textbox', { name: /^Email/ })
        await emailBox.fill('khong-phai-email')
        await dialog.getByRole('button', { name: 'Tạo tài khoản' }).click()
        await qa.expect(dialog.getByText('Email không hợp lệ.')).toBeVisible()
        await qa.expect(emailBox).toBeFocused()
        await qa.shot('email-sai-dinh-dang')
        await emailBox.fill('khong-phai-email@')
        await qa.expect(dialog.getByText('Email không hợp lệ.')).toHaveCount(0)
        const fit = await field.evaluate((el) => ({ width: Math.round(el.clientWidth), needed: el.scrollWidth, value: el.value.length }))
        qa.expect(
          fit.needed,
          `the generated password (${fit.value} characters) is clipped: the field is ${fit.width}px wide, the text needs ${fit.needed}px`,
        ).toBeLessThanOrEqual(fit.width)
        await dialog.getByRole('button', { name: 'Huỷ' }).click()

        const bad = await api.post('/api/users', { fullName: ' ', email: 'khong-phai-email', password: 'ngan', roleId: 'x' })
        qa.expectStatus(bad, 400)
        qa.expect(bad.body.code).toBe('MSG09')
        qa.expect(Object.keys(bad.body.errors ?? {}).sort()).toEqual(['email', 'fullName', 'password', 'roleId'])
        const dup = await api.post('/api/users', {
          fullName: qa.name(`FE-02 Trùng ${RUN}`),
          email: QA_USERS.a.email.toUpperCase(),
          password: 'MotHaiBaBon@12',
          roleId: ROLE.member.id,
        })
        qa.expectStatus(dup, 409)
        qa.expect(dup.body.code).toBe('MSG08')
        const same = (await listUsers(qa, api)).filter((u) => u.email.toLowerCase() === QA_USERS.a.email)
        qa.expect(same.length, 'a duplicate account was created').toBe(1)
      },
    },

    // ---------------------------------------------------------------- UC-06b
    {
      id: 'AC5',
      role: 'admin',
      devices: ['desktop', 'phone'],
      title: 'Sửa tài khoản: form điền sẵn, đổi họ tên báo "Cập nhật tài khoản thành công.", dòng đổi theo, nhật ký có cũ/mới; họ tên trống hoặc email trùng bị chặn (UC-06b, EX-1, EX-2)',
      steps: [
        'Bấm Sửa ở dòng [QA] FE-02 Người dùng A',
        'Xoá họ tên, lưu; nhập email chutoa@trustcabinet.local, lưu',
        'Trả email cũ, đổi họ tên, lưu',
      ],
      expected:
        'Form có họ tên, email, vai trò hiện tại, không có ô mật khẩu; họ tên trống: "Họ tên không được để trống."; email trùng: cảnh báo đổi email sẽ đăng xuất, rồi "Email đã được sử dụng."; lưu đúng: toast MSG10, dòng hiện tên mới; nhật ký user.updated có giá trị cũ và mới',
      async run(qa) {
        const api = await qa.api()
        const a = await ensureUser(qa, api, 'a')
        const renamed = `${a.fullName} (sửa ${qa.device} ${RUN})`
        try {
          await openUsers(qa)
          const row = userRow(qa, QA_USERS.a.email)
          await row.scrollIntoViewIfNeeded()
          await row.getByRole('button', { name: `Sửa tài khoản ${a.fullName}` }).click()
          const dialog = qa.page.getByRole('dialog', { name: 'Sửa tài khoản' })
          await qa.expect(dialog.getByLabel('Họ tên')).toHaveValue(a.fullName)
          await qa.expect(dialog.getByRole('textbox', { name: /^Email/ })).toHaveValue(QA_USERS.a.email)
          await qa.expect(dialog.getByRole('button', { name: `Vai trò: ${ROLE.member.name}` })).toBeVisible()
          await qa.expect(dialog.getByLabel('Mật khẩu', { exact: true })).toHaveCount(0)
          await qa.shot('form-sua')

          await dialog.getByLabel('Họ tên').fill('')
          await dialog.getByRole('button', { name: 'Lưu thay đổi' }).click()
          await qa.expect(dialog.getByText('Họ tên không được để trống.')).toBeVisible()
          await dialog.getByLabel('Họ tên').fill(renamed)
          await dialog.getByRole('textbox', { name: /^Email/ }).fill('chutoa@trustcabinet.local')
          await qa.expect(dialog.getByText('Đổi email sẽ đăng xuất người này khỏi mọi thiết bị.')).toBeVisible()
          await dialog.getByRole('button', { name: 'Lưu thay đổi' }).click()
          await qa.expect(dialog.getByText('Email đã được sử dụng.')).toBeVisible()
          await qa.shot('email-trung')
          // USER_FT22 E1: an invalid email on edit is marked per field and sends nothing.
          await dialog.getByRole('textbox', { name: /^Email/ }).fill('khong-phai-email')
          let editPosts = 0
          qa.page.on('request', (r) => {
            if (r.method() === 'PUT' && /\/api\/users\//.test(r.url())) editPosts += 1
          })
          await dialog.getByRole('button', { name: 'Lưu thay đổi' }).click()
          await qa.expect(dialog.getByText('Email không hợp lệ.')).toBeVisible()
          await qa.expect(dialog).toBeVisible()
          qa.expect(editPosts, 'an invalid edit was sent to the server').toBe(0)

          await dialog.getByRole('textbox', { name: /^Email/ }).fill(QA_USERS.a.email)
          await dialog.getByRole('button', { name: 'Lưu thay đổi' }).click()
          await qa.expect(toast(qa, 'Cập nhật tài khoản thành công.')).toBeVisible()
          await qa.expect(dialog).toBeHidden()
          await qa.expect(row.getByText(renamed)).toBeVisible()
          await qa.shot('da-luu')
          await noSidewaysScroll(qa)
          await noRawCodes(qa)

          const entries = await auditFor(qa, api, a.id, 'user.updated')
          const details = JSON.stringify(entries[0]?.details ?? {})
          qa.expect(details, 'audit entry lacks the old name').toContain(a.fullName)
          qa.expect(details, 'audit entry lacks the new name').toContain(renamed)
          // The seed account was not touched by the refused save.
          const chair = (await listUsers(qa, api)).filter((u) => u.email === 'chutoa@trustcabinet.local')
          qa.expect(chair.length).toBe(1)
          qa.note('UC-06b bước 3 nói sửa được Chức danh và Thông tin liên hệ; form và API chỉ có Họ tên, Email, Vai trò.')
        } finally {
          await ensureUser(qa, api, 'a')
        }
      },
    },
    {
      id: 'AC6',
      role: 'admin',
      title: 'API: đổi email kết thúc phiên của người đó, đổi họ tên thì không (SCRUM-13 BA Final Review); PUT không nhận isActive',
      steps: [
        'qa-fe02-a đăng nhập (phiên có refresh cookie)',
        'Quản trị đổi họ tên → phiên đó refresh',
        'Quản trị đổi email sang qa-fe02-a-moi@… → phiên đó refresh',
        'PUT /api/users/{id} kèm isActive:false',
      ],
      expected:
        'Đổi tên: refresh 200; đổi email: refresh 401; PUT kèm isActive:false trả 200 MSG10 nhưng tài khoản vẫn hoạt động',
      async run(qa) {
        const api = await qa.api()
        const a = await ensureUser(qa, api, 'a')
        const newEmail = `qa-fe02-a-moi@trustcabinet.local`
        try {
          const session = await sessionOf(qa, QA_USERS.a.email)
          qa.expectStatus(await api.put(`/api/users/${a.id}`, { fullName: `${a.fullName} ·`, email: QA_USERS.a.email, roleId: ROLE.member.id }), 200)
          qa.expectStatus(await session.post('/api/auth/refresh'), 200)
          const changed = await api.put(`/api/users/${a.id}`, { fullName: a.fullName, email: newEmail, roleId: ROLE.member.id })
          qa.expectStatus(changed, 200)
          qa.expect(changed.body.code).toBe('MSG10')
          const refresh = await session.post('/api/auth/refresh')
          qa.expectStatus(refresh, 401)
          const sneaky = await api.put(`/api/users/${a.id}`, {
            fullName: a.fullName,
            email: QA_USERS.a.email,
            roleId: ROLE.member.id,
            isActive: false,
          })
          qa.expectStatus(sneaky, 200)
          qa.expect(sneaky.body.user.isActive, 'PUT /api/users/{id} deactivated the account').toBe(true)
        } finally {
          const now = (await listUsers(qa, api)).find((u) => u.id === a.id)
          if (now && now.email !== QA_USERS.a.email) {
            await api.put(`/api/users/${a.id}`, { fullName: a.fullName, email: QA_USERS.a.email, roleId: ROLE.member.id })
          }
          await ensureUser(qa, api, 'a')
        }
      },
    },

    // ---------------------------------------------------------------- UC-06c
    {
      id: 'AC7',
      role: 'admin',
      devices: ['desktop', 'phone'],
      title: 'Vô hiệu hoá có hỏi lại, báo "Đổi trạng thái tài khoản thành công.", người đó bị đăng xuất và không đăng nhập được; kích hoạt lại thì đăng nhập lại được (UC-06c, BR-001)',
      steps: [
        'qa-fe02-b đăng nhập (một phiên riêng)',
        'Quản trị bấm Vô hiệu hoá ở dòng [QA] FE-02 Người dùng B, đọc hộp xác nhận, bấm Vô hiệu hoá',
        'Phiên của B refresh; B đăng nhập lại',
        'Quản trị bấm Kích hoạt lại, xác nhận; B đăng nhập lại',
      ],
      expected:
        'Hộp xác nhận nêu tên và hậu quả; toast MSG11; dòng hiện "Đã vô hiệu hoá"; refresh của B 401, đăng nhập 401 MSG02; nhật ký user.deactivated; kích hoạt lại: toast MSG11, dòng "Hoạt động", B đăng nhập được, nhật ký user.reactivated',
      async run(qa) {
        const api = await qa.api()
        const b = await ensureUser(qa, api, 'b')
        try {
          const session = await sessionOf(qa, QA_USERS.b.email)
          await openUsers(qa)
          const row = userRow(qa, QA_USERS.b.email)
          await row.scrollIntoViewIfNeeded()
          await row.getByRole('button', { name: `Vô hiệu hoá tài khoản ${b.fullName}` }).click()
          const confirm = qa.page.getByRole('dialog', { name: 'Vô hiệu hoá tài khoản' })
          await qa.expect(confirm).toContainText(b.fullName)
          await qa.expect(confirm).toContainText('đăng xuất khỏi mọi thiết bị')
          const confirmButton = confirm.getByRole('button', { name: 'Vô hiệu hoá', exact: true })
          await inViewportX(qa, confirmButton)
          await qa.shot('hoi-lai-vo-hieu-hoa')
          await confirmButton.click()
          await qa.expect(toast(qa, 'Đổi trạng thái tài khoản thành công.')).toBeVisible()
          await qa.expect(row.getByText('Đã vô hiệu hoá').filter({ visible: true })).toBeVisible()
          await qa.shot('da-vo-hieu-hoa')

          qa.expectStatus(await session.post('/api/auth/refresh'), 401)
          const denied = await rawLogin(qa, QA_USERS.b.email)
          qa.expectStatus(denied, 401)
          qa.expect(denied.body.code).toBe('MSG02')
          qa.expect((await auditFor(qa, api, b.id, 'user.deactivated')).length).toBeGreaterThan(0)

          await row.getByRole('button', { name: `Kích hoạt lại tài khoản ${b.fullName}` }).click()
          const again = qa.page.getByRole('dialog', { name: 'Kích hoạt lại tài khoản' })
          await qa.expect(again).toContainText(b.fullName)
          await again.getByRole('button', { name: 'Kích hoạt lại', exact: true }).click()
          await qa.expect(toast(qa, 'Đổi trạng thái tài khoản thành công.')).toBeVisible()
          await qa.expect(row.getByText('Đã vô hiệu hoá')).toHaveCount(0)
          await qa.shot('da-kich-hoat-lai')
          qa.expectStatus(await rawLogin(qa, QA_USERS.b.email), 200)
          qa.expect((await auditFor(qa, api, b.id, 'user.reactivated')).length).toBeGreaterThan(0)
          await noSidewaysScroll(qa)
          await noRawCodes(qa)
        } finally {
          await ensureUser(qa, api, 'b')
        }
      },
    },
    {
      id: 'AC8',
      role: 'admin',
      title: 'Không đổi được trạng thái: nút của chính mình bị khoá, tự vô hiệu hoá qua API 409 MSG12, danh sách cũ báo "đã bị vô hiệu hoá từ trước", thiếu isActive 400 MSG09 (UC-06c EX-1, EX-2)',
      steps: [
        'Xem dòng có nhãn "Bạn"',
        'PUT /api/users/{quantri}/status {isActive:false}',
        'Mở danh sách khi B đang hoạt động; B bị vô hiệu hoá sau lưng trang; bấm Vô hiệu hoá B, xác nhận',
        'PUT /api/users/{B}/status {}',
      ],
      expected:
        'Nút đổi trạng thái của chính mình disable; API 409 MSG12, quantri vẫn hoạt động; hộp xác nhận báo "Tài khoản này đã bị vô hiệu hoá từ trước. Danh sách đã được cập nhật."; body rỗng 400 MSG09 errors.isActive, trạng thái không đổi',
      async run(qa) {
        const api = await qa.api()
        const b = await ensureUser(qa, api, 'b')
        const me = (await api.get('/api/auth/me')).body
        try {
          await openUsers(qa)
          const selfRow = userRow(qa, me.email)
          await qa.expect(selfRow.getByText('Bạn')).toBeVisible()
          await qa.expect(selfRow.getByRole('button', { name: /^(Vô hiệu hoá|Kích hoạt lại) tài khoản/ })).toBeDisabled()
          // USER_FT27 E1: the disabled button explains itself without opening anything.
          await qa.expect(selfRow.getByRole('button', { name: /^(Vô hiệu hoá|Kích hoạt lại) tài khoản/ })).toHaveAttribute(
            'data-tooltip',
            'Không thể tự đổi trạng thái tài khoản của mình',
          )
          await selfRow.scrollIntoViewIfNeeded()
          await qa.shot('dong-cua-minh')

          const self = await api.put(`/api/users/${me.id}/status`, { isActive: false })
          qa.expectStatus(self, 409)
          qa.expect(self.body.code).toBe('MSG12')
          qa.expect((await listUsers(qa, api)).find((u) => u.id === me.id).isActive).toBe(true)

          qa.expectStatus(await api.put(`/api/users/${b.id}/status`, { isActive: false }), 200)
          const row = userRow(qa, QA_USERS.b.email)
          await row.scrollIntoViewIfNeeded()
          await row.getByRole('button', { name: `Vô hiệu hoá tài khoản ${b.fullName}` }).click()
          const confirm = qa.page.getByRole('dialog', { name: 'Vô hiệu hoá tài khoản' })
          await confirm.getByRole('button', { name: 'Vô hiệu hoá', exact: true }).click()
          await qa.expect(confirm.getByText('Tài khoản này đã bị vô hiệu hoá từ trước. Danh sách đã được cập nhật.')).toBeVisible()
          await qa.shot('da-vo-hieu-hoa-tu-truoc')
          await noRawCodes(qa)

          const empty = await api.put(`/api/users/${b.id}/status`, {})
          qa.expectStatus(empty, 400)
          qa.expect(empty.body.code).toBe('MSG09')
          qa.expect(Object.keys(empty.body.errors ?? {})).toContain('isActive')
          qa.expect((await listUsers(qa, api)).find((u) => u.id === b.id).isActive).toBe(false)
        } finally {
          await ensureUser(qa, api, 'b')
        }
      },
    },

    // ---------------------------------------------------------------- UC-07
    {
      id: 'AC9',
      role: 'admin',
      devices: ['desktop', 'phone'],
      title: 'Gửi liên kết đặt lại mật khẩu: hỏi lại kèm email nhận; máy chủ mail không gửi được thì báo MSG15 trong hộp, thử lại được, mật khẩu cũ vẫn dùng được, không ghi nhật ký (UC-07 bước 1–3, EX-2)',
      steps: [
        'Bấm chìa khoá ở dòng [QA] FE-02 Người dùng A',
        'Đọc hộp xác nhận, bấm "Gửi liên kết" (địa chỉ @trustcabinet.local không bao giờ nhận thư)',
      ],
      expected:
        'Hộp xác nhận nêu qa-fe02-a@trustcabinet.local, 60 phút, liên kết cũ hết hiệu lực; lỗi "Không gửi được email đặt lại mật khẩu. Vui lòng thử lại." hiện trong hộp, nút "Gửi liên kết" vẫn bấm được; A vẫn đăng nhập bằng mật khẩu cũ; không có dòng user.password_reset_requested mới',
      async run(qa) {
        const api = await qa.api()
        const a = await ensureUser(qa, api, 'a')
        const before = (await auditFor(qa, api, a.id, 'user.password_reset_requested')).length
        await openUsers(qa)
        const row = userRow(qa, QA_USERS.a.email)
        await row.scrollIntoViewIfNeeded()
        await row.getByRole('button', { name: `Gửi liên kết đặt lại mật khẩu cho ${a.fullName}` }).click()
        const confirm = qa.page.getByRole('dialog', { name: 'Đặt lại mật khẩu' })
        await qa.expect(confirm).toContainText(QA_USERS.a.email)
        await qa.expect(confirm).toContainText('60 phút')
        await qa.shot('hoi-lai-dat-lai')
        const sent = qa.page.waitForResponse((r) => r.url().endsWith(`/api/users/${a.id}/password-reset`))
        await confirm.getByRole('button', { name: 'Gửi liên kết' }).click()
        const res = await sent
        if (res.status() === 200) {
          qa.inconclusive('Máy chủ local đã gửi được thư tới @trustcabinet.local (Email:UndeliverableDomains không bật) — kiểm lại cấu hình trước khi chạy tiếp')
        }
        qa.expect(res.status()).toBe(503)
        await qa.expect(confirm.getByText('Không gửi được email đặt lại mật khẩu. Vui lòng thử lại.')).toBeVisible()
        const retry = confirm.getByRole('button', { name: 'Gửi liên kết' })
        await qa.expect(retry).toBeEnabled()
        await inViewportX(qa, retry)
        await qa.shot('msg15-trong-hop')
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
        qa.expectStatus(await rawLogin(qa, QA_USERS.a.email), 200)
        qa.expect((await auditFor(qa, api, a.id, 'user.password_reset_requested')).length, 'audit written for an unsent email').toBe(before)
      },
    },
    {
      id: 'AC10',
      role: 'admin',
      devices: ['desktop', 'phone'],
      title: 'Tài khoản không đủ điều kiện: nút đặt lại mật khẩu của tài khoản đã vô hiệu hoá bị khoá; API 409 MSG14, tài khoản không có 404 (UC-07 EX-1, EX-3)',
      steps: [
        'Xem dòng [QA] FE-02 Người dùng C (đã vô hiệu hoá)',
        'POST /api/users/{C}/password-reset; POST /api/users/{id lạ}/password-reset',
      ],
      expected:
        'Nút chìa khoá disable, lý do "kích hoạt lại trước" (tooltip); API 409 MSG14 cho C, 404 không body cho id lạ',
      async run(qa) {
        const api = await qa.api()
        const c = await ensureUser(qa, api, 'c', { active: false })
        await openUsers(qa)
        const row = userRow(qa, QA_USERS.c.email)
        await row.scrollIntoViewIfNeeded()
        const key = row.getByRole('button', { name: `Gửi liên kết đặt lại mật khẩu cho ${c.fullName}` })
        if (qa.device === 'desktop') {
          await qa.expect(key).toBeDisabled()
          await qa.expect(key).toHaveAttribute('data-tooltip', /kích hoạt lại trước/)
          await key.hover({ force: true })
          await qa.page.waitForTimeout(600) // the tooltip's own 400 ms delay
        } else {
          // On a touch screen an unavailable action is left out rather than explained by a hover tooltip;
          // the row still says "Đã vô hiệu hoá" next to the role.
          await qa.expect(key).toBeHidden()
          await qa.expect(row.getByText('Đã vô hiệu hoá').filter({ visible: true })).toBeVisible()
          await qa.expect(row.getByRole('button', { name: `Kích hoạt lại tài khoản ${c.fullName}` })).toBeVisible()
        }
        await qa.shot('nut-khoa')
        await noSidewaysScroll(qa)

        const res = await api.post(`/api/users/${c.id}/password-reset`)
        qa.expectStatus(res, 409)
        qa.expect(res.body.code).toBe('MSG14')
        qa.expectStatus(await api.post(`/api/users/${UNKNOWN_ID}/password-reset`), 404)
        if (!(await listUsers(qa, api)).some((u) => !u.hasPassword)) {
          qa.note('Không có tài khoản chỉ đăng nhập bằng Google (hasPassword=false) trong dữ liệu local, và UI/API không tạo được loại này: nhánh MSG14 "chỉ Google" chưa kiểm.')
        }
      },
    },
    {
      id: 'AC11',
      role: 'anon',
      devices: ['desktop', 'phone'],
      title: 'Trang /reset-password: không có mã thì MSG129; nhập lại không khớp và mật khẩu ngắn báo ở ô; liên kết sai/hết hạn thì ẩn form, MSG93 (UC-07 bước 10–14, EX-4, EX-5, EX-6)',
      steps: [
        'Mở /reset-password (không có #token)',
        'Mở /reset-password#token=<mã giả>; nhập mật khẩu ngắn; nhập lại không khớp; rồi mật khẩu hợp lệ khớp nhau, bấm "Đặt mật khẩu mới"',
      ],
      expected:
        '"Không tìm thấy mã đặt lại mật khẩu. Hãy bấm lại liên kết trong email." + nút "Về trang đăng nhập"; mã bị xoá khỏi thanh địa chỉ; "Mật khẩu phải có ít nhất 12 ký tự."; lỗi ở ô nhập lại; "Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn. Vui lòng liên hệ quản trị viên.", form ẩn; API 400 MSG93',
      async run(qa) {
        let posts = 0
        qa.page.on('request', (r) => {
          if (r.url().endsWith('/api/auth/password-reset')) posts += 1
        })
        await qa.goto('/reset-password')
        await qa.expect(qa.page.getByRole('heading', { name: 'Đặt lại mật khẩu' })).toBeVisible()
        await qa.expect(qa.page.getByText('Không tìm thấy mã đặt lại mật khẩu. Hãy bấm lại liên kết trong email.')).toBeVisible()
        // USER_FT44 E1: the page explains a still-valid link stays usable, and sends nothing.
        await qa.expect(qa.page.getByText(/liên kết vẫn dùng được khi chưa được dùng, chưa quá 60 phút/)).toBeVisible()
        await qa.expect(qa.page.getByRole('link', { name: 'Về trang đăng nhập' })).toBeVisible()
        qa.expect(posts, 'the page called the API without a token').toBe(0)
        await qa.shot('khong-co-ma')
        await noSidewaysScroll(qa)

        const fake = `qa${RUN}`.padEnd(43, 'x')
        await qa.goto(`/reset-password#token=${fake}`)
        await qa.expect(qa.page).not.toHaveURL(/token=/)
        const fresh = qa.page.getByLabel('Mật khẩu mới', { exact: true })
        const again = qa.page.getByLabel('Nhập lại mật khẩu mới', { exact: true })
        const submit = qa.page.getByRole('button', { name: 'Đặt mật khẩu mới' })
        await fresh.fill('ngan')
        await again.fill('ngan')
        let calls = 0
        qa.page.on('request', (r) => {
          if (r.url().endsWith('/api/auth/password-reset')) calls += 1
        })
        await submit.click()
        await qa.expect(qa.page.getByText('Mật khẩu phải có ít nhất 12 ký tự.')).toBeVisible()
        await fresh.fill('MatKhauMoiQA@2026')
        await again.fill('MatKhauKhacQA@2026')
        await submit.click()
        await qa.expect(again).toHaveAttribute('aria-invalid', 'true')
        await qa.shot('loi-o-mat-khau')
        qa.expect(calls, 'the page called the API with an invalid form').toBe(0)

        await again.fill('MatKhauMoiQA@2026')
        await submit.click()
        await qa.expect(
          qa.page.getByText('Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn. Vui lòng liên hệ quản trị viên.'),
        ).toBeVisible()
        // USER_FT43 E1 (một phần): the dead-link notice explains the 60-minute single-use rule.
        await qa.expect(qa.page.getByText('Liên kết chỉ dùng được một lần trong 60 phút, và hết hiệu lực khi có liên kết mới hơn được gửi.')).toBeVisible()
        await qa.expect(fresh).toHaveCount(0)
        await qa.expect(qa.page.getByRole('link', { name: 'Về trang đăng nhập' })).toBeVisible()
        await qa.shot('lien-ket-khong-hop-le')
        await noSidewaysScroll(qa)
        await noRawCodes(qa)

        const res = await (await qa.api('anon')).post('/api/auth/password-reset', { token: fake, newPassword: 'MatKhauMoiQA@2026' })
        qa.expectStatus(res, 400)
        qa.expect(res.body.code).toBe('MSG93')
      },
    },
    {
      id: 'AC12',
      role: 'admin',
      title: 'Gửi liên kết thành công và người dùng đặt mật khẩu mới bằng liên kết trong email (UC-07 bước 4–9, 10–19, MSG13, MSG92)',
      steps: ['Cần hộp thư thật nhận được liên kết'],
      expected: 'Toast MSG13; email tới; đặt mật khẩu mới → MSG92, về /login, mật khẩu cũ bị từ chối, mọi phiên kết thúc; liên kết dùng lần hai → MSG93',
      async run(qa) {
        qa.inconclusive(
          'Cần một người: trên một máy có SMTP thật, tạo tài khoản với email hộp thư thử (không phải hộp thư thật của ai), bấm chìa khoá → Gửi liên kết (mong đợi toast "Đã gửi email đặt lại mật khẩu."), mở thư, đặt mật khẩu mới (mong đợi "Đặt lại mật khẩu thành công. Vui lòng đăng nhập bằng mật khẩu mới." và về /login), thử mật khẩu cũ (bị từ chối) và mở lại liên kết (MSG93). Local không gửi được thư tới @trustcabinet.local nên không làm tự động.',
        )
      },
    },

    // ---------------------------------------------------------------- UC-08
    {
      id: 'AC13',
      role: 'admin',
      devices: ['desktop', 'phone'],
      title: 'Đổi vai trò trong form sửa: báo "Cập nhật tài khoản thành công.", dòng hiện vai trò mới, người đó không bị đăng xuất và nhận quyền mới, nhật ký có vai trò cũ/mới (UC-08, AF-1, BR-008)',
      steps: [
        'qa-fe02-a đăng nhập (một phiên riêng)',
        'Quản trị bấm Sửa ở dòng A, chọn vai trò Thư ký, Lưu thay đổi',
        'Phiên của A refresh và đọc /api/auth/me',
      ],
      expected:
        'Toast MSG10; dòng A hiện "Thư ký"; refresh của A 200 (không bị đăng xuất), quyền mới có MeetingCreator; nhật ký user.updated nêu Thành viên và Thư ký',
      async run(qa) {
        const api = await qa.api()
        const a = await ensureUser(qa, api, 'a')
        try {
          const session = await sessionOf(qa, QA_USERS.a.email)
          await openUsers(qa)
          const row = userRow(qa, QA_USERS.a.email)
          await row.scrollIntoViewIfNeeded()
          await row.getByRole('button', { name: `Sửa tài khoản ${a.fullName}` }).click()
          const dialog = qa.page.getByRole('dialog', { name: 'Sửa tài khoản' })
          await qa.expect(dialog.getByLabel('Họ tên')).toHaveValue(a.fullName)
          await dialog.getByRole('button', { name: `Vai trò: ${ROLE.member.name}` }).click()
          await qa.page.getByRole('listbox', { name: 'Vai trò' }).getByRole('option', { name: ROLE.secretary.name, exact: true }).click()
          await qa.expect(dialog.getByRole('button', { name: `Vai trò: ${ROLE.secretary.name}` })).toBeVisible()
          await qa.expect(dialog.getByText('Đổi email sẽ đăng xuất')).toHaveCount(0)
          await qa.shot('chon-vai-tro')
          await dialog.getByRole('button', { name: 'Lưu thay đổi' }).click()
          await qa.expect(toast(qa, 'Cập nhật tài khoản thành công.')).toBeVisible()
          await qa.expect(row.getByText(ROLE.secretary.name).filter({ visible: true })).toBeVisible()
          await qa.shot('vai-tro-moi')
          await noSidewaysScroll(qa)
          await noRawCodes(qa)

          const refreshed = await session.post('/api/auth/refresh')
          qa.expectStatus(refreshed, 200)
          const caps = refreshed.body.user?.capabilities ?? []
          qa.expect(caps, 'the new role did not apply').toContain('MeetingCreator')
          const entries = await auditFor(qa, api, a.id, 'user.updated')
          const details = JSON.stringify(entries[0]?.details ?? {})
          qa.expect(details).toContain(ROLE.member.name)
          qa.expect(details).toContain(ROLE.secretary.name)
        } finally {
          await ensureUser(qa, api, 'a')
        }
      },
    },
    {
      id: 'AC14',
      role: QA_USERS.a.email,
      title: 'Người được đổi vai trò đang mở app: thấy "Quyền truy cập của bạn vừa được cập nhật." và menu đổi theo, không phải tải lại hay đăng nhập lại (UC-08 POST-4, BR-008, MSG287)',
      steps: ['qa-fe02-a (Thành viên) mở Lịch làm việc', 'Quản trị đổi A sang Thư ký (API)', 'Quản trị đổi A về Thành viên'],
      expected:
        'Toast MSG287 hiện trong trang đang mở; A vẫn ở trong app; /api/auth/me của trang đổi vai trò theo',
      async run(qa) {
        const admin = await qa.api('admin')
        const a = await ensureUser(qa, admin, 'a')
        try {
          await qa.goto('/calendar')
          await qa.expect(qa.page).toHaveURL(/\/calendar/)
          await qa.shot('truoc-khi-doi')
          qa.expectStatus(await admin.put(`/api/users/${a.id}`, { fullName: a.fullName, email: QA_USERS.a.email, roleId: ROLE.secretary.id }), 200)
          await qa.expect(toast(qa, 'Quyền truy cập của bạn vừa được cập nhật.')).toBeVisible({ timeout: 15_000 })
          await qa.expect(qa.page).toHaveURL(/\/calendar/)
          await qa.shot('nhan-thong-bao')
          await noRawCodes(qa)
        } finally {
          await ensureUser(qa, admin, 'a')
        }
      },
    },
    {
      id: 'AC15',
      role: 'admin',
      title: 'Vai trò vừa bị xoá khi đang sửa hoặc đang tạo: báo "Vai trò không tồn tại." ở ô Vai trò, vai trò không đổi (UC-08 EX-1, USER_FT14)',
      steps: [
        'Tạo vai trò [QA] FE02 tạm (API), mở form sửa A',
        'Xoá vai trò đó (API), chọn nó trong form, Lưu thay đổi',
        'Tạo vai trò tạm thứ hai, mở form Tạo, xoá nó, chọn nó, bấm "Tạo tài khoản"',
      ],
      expected: 'Cả hai form: lỗi "Vai trò không tồn tại." dưới ô Vai trò, hộp thoại còn mở; API 400 MSG09 errors.roleId; A vẫn là Thành viên, không có tài khoản nào được tạo',
      async run(qa) {
        const api = await qa.api()
        const a = await ensureUser(qa, api, 'a')
        const roleName = qa.name(`FE02 tạm ${RUN}`)
        const created = await api.post('/api/roles', { name: roleName, description: null, capabilities: [] })
        qa.expectStatus(created, 201)
        const roleId = created.body.role.id
        let deleted = false
        try {
          await openUsers(qa)
          const row = userRow(qa, QA_USERS.a.email)
          await row.scrollIntoViewIfNeeded()
          await row.getByRole('button', { name: `Sửa tài khoản ${a.fullName}` }).click()
          const dialog = qa.page.getByRole('dialog', { name: 'Sửa tài khoản' })
          await qa.expect(dialog.getByLabel('Họ tên')).toHaveValue(a.fullName)
          qa.expectStatus(await api.delete(`/api/roles/${roleId}`), 200)
          deleted = true
          await dialog.getByRole('button', { name: `Vai trò: ${ROLE.member.name}` }).click()
          await qa.page.getByRole('listbox', { name: 'Vai trò' }).getByRole('option', { name: roleName, exact: true }).click()
          await dialog.getByRole('button', { name: 'Lưu thay đổi' }).click()
          await qa.expect(dialog.getByText('Vai trò không tồn tại.')).toBeVisible()
          await qa.expect(dialog).toBeVisible()
          await qa.shot('vai-tro-khong-ton-tai')
          await noRawCodes(qa)
          qa.expect((await listUsers(qa, api)).find((u) => u.id === a.id).roleId).toBe(ROLE.member.id)
          const res = await api.put(`/api/users/${a.id}`, { fullName: a.fullName, email: QA_USERS.a.email, roleId })
          qa.expectStatus(res, 400)
          qa.expect(res.body.code).toBe('MSG09')
          qa.expect(res.body.errors?.roleId).toBe('Vai trò không tồn tại.')
          await dialog.getByRole('button', { name: 'Huỷ' }).click()
          // The edited form asks before discarding.
          await qa.page.getByRole('dialog', { name: 'Bỏ thay đổi chưa lưu?' }).getByRole('button', { name: 'Bỏ thay đổi' }).click()
          await qa.expect(dialog).toHaveCount(0)
          // USER_FT14: the same race in the create dialog — the deleted role is refused the same way.
          const role2Name = qa.name(`FE02 tạm tạo ${RUN}`)
          const created2 = await api.post('/api/roles', { name: role2Name, description: null, capabilities: [] })
          qa.expectStatus(created2, 201)
          const role2Id = created2.body.role.id
          try {
            await qa.page.getByRole('button', { name: 'Tạo tài khoản' }).click()
            const create = qa.page.getByRole('dialog', { name: 'Tạo tài khoản' })
            await create.getByLabel('Họ tên').fill(qa.name(`FE-02 vai trò mất ${RUN}`))
            await create.getByRole('textbox', { name: /^Email/ }).fill(`qa-fe02-mat-role-${RUN}@trustcabinet.local`)
            await create.getByRole('button', { name: 'Tạo mật khẩu' }).click()
            qa.expectStatus(await api.delete(`/api/roles/${role2Id}`), 200)
            await create.getByRole('button', { name: `Vai trò: ${ROLE.member.name}` }).click()
            await qa.page.getByRole('listbox', { name: 'Vai trò' }).getByRole('option', { name: role2Name, exact: true }).click()
            const posted = qa.page.waitForResponse((r) => r.url().endsWith('/api/users') && r.request().method() === 'POST')
            await create.getByRole('button', { name: 'Tạo tài khoản' }).click()
            qa.expect((await posted).status()).toBe(400)
            await qa.expect(create.getByText('Vai trò không tồn tại.')).toBeVisible()
            await qa.expect(create).toBeVisible()
            await qa.shot('tao-vai-tro-khong-ton-tai')
            qa.expect((await listUsers(qa, api)).some((u) => u.email === `qa-fe02-mat-role-${RUN}@trustcabinet.local`), 'an account was created with a deleted role').toBe(false)
            await create.getByRole('button', { name: 'Huỷ' }).click()
            // The discarded create asks first.
            await qa.page.getByRole('dialog', { name: 'Bỏ thay đổi chưa lưu?' }).getByRole('button', { name: 'Bỏ thay đổi' }).click()
            await qa.expect(create).toHaveCount(0)
          } finally {
            await api.delete(`/api/roles/${role2Id}`)
          }
        } finally {
          if (!deleted) await api.delete(`/api/roles/${roleId}`)
          await ensureUser(qa, api, 'a')
        }
      },
    },
    {
      id: 'AC16',
      role: 'admin',
      title: 'Hạ vai trò quản trị viên cuối cùng bị từ chối 409 MSG12 (UC-08 EX-2)',
      steps: ['Cần một cơ sở dữ liệu riêng'],
      expected: '409 MSG12, vai trò giữ nguyên; form báo "Không thể đổi vai trò: … không còn tài khoản quản trị nào đang hoạt động."',
      async run(qa) {
        const api = await qa.api()
        const matrix = (await api.get('/api/roles/capabilities')).body
        const adminRoles = new Set(matrix.roles.filter((r) => r.capabilities.includes('AdminUsers')).map((r) => r.id))
        const admins = (await listUsers(qa, api)).filter((u) => u.isActive && adminRoles.has(u.roleId))
        qa.inconclusive(
          `Hiện có ${admins.length} tài khoản quản trị đang hoạt động (${admins.map((u) => u.email).join(', ')}). Thử hạ vai trò người cuối cùng trên cơ sở dữ liệu dùng chung có thể khoá cả nhóm khỏi trang quản trị nếu một agent khác vừa tạo thêm quản trị viên. Cần một người kiểm trên cơ sở dữ liệu riêng: khi chỉ còn một quản trị viên, sửa người đó sang Thành viên → mong đợi lỗi trong form và 409 MSG12.`,
        )
      },
    },

    // ---------------------------------------------------------------- former UC-09
    {
      id: 'AC17',
      role: 'admin',
      title: 'Mở khoá tài khoản bị khoá do đăng nhập sai (UC-09 cũ)',
      steps: ['Tìm trạng thái "bị khoá" và thao tác "Mở khoá" trên danh sách tài khoản và trong API'],
      expected: 'Tài khoản bị khoá hiện trạng thái khoá; có thao tác Mở khoá, xác nhận rồi xoá khoá và bộ đếm, ghi nhật ký',
      async run(qa) {
        const users = await openUsers(qa)
        const lockFields = [...new Set(users.flatMap((u) => Object.keys(u)))].filter((k) => /lock/i.test(k))
        const unlockButtons = await qa.page.getByRole('button', { name: /Mở khoá/ }).count()
        await qa.shot('khong-co-mo-khoa')
        qa.inconclusive(
          `UC-09 cũ "Đã đặc tả" và không bị PO bỏ, nhưng hệ thống không có trạng thái khoá tài khoản: API trả ${lockFields.length ? lockFields.join(', '): 'không trường nào về khoá'}, danh sách có ${unlockButtons} nút "Mở khoá"; giới hạn đăng nhập sai (SCRUM-132) chỉ chặn 429 theo IP/email trong một phút rồi tự hết, không để lại khoá nào cho quản trị viên mở. Cần PO/BA quyết: bỏ UC-09 cũ khỏi phạm vi hay làm cơ chế khoá + mở khoá.`,
        )
      },
    },

    // ---------------------------------------------------------------- permissions
    ...DENIED.map((role, i) => ({
      id: `AC${18 + i}`,
      role,
      devices: role === 'member' ? ['desktop', 'phone'] : ['desktop'],
      title: `${{ chair: 'Chủ toạ', secretary: 'Thư ký', member: 'Thành viên' }[role]} không có AdminUsers: không thấy mục Người dùng, mở URL thấy 404 mà không gọi /api/users, mọi API /api/users trả 403 (USER_FT08)`,
      steps: [
        `Đăng nhập ${role} (kiểm capability trước)`,
        'Xem sidebar; mở /mgmt/admin/users (đếm GET /api/users của trang)',
        'GET/POST /api/users, GET/PUT /api/users/{A}, PUT …/status, POST …/password-reset',
      ],
      expected: 'Không có mục "Người dùng"; "Không tìm thấy trang"; trang 404 không gửi GET /api/users nào (USER_FT08); mọi lời gọi API 403 (endpoint cố định, AGENTS.md); A không đổi',
      async run(qa) {
        const { roleName, capabilities } = await qa.capabilities()
        if (capabilities.includes('AdminUsers')) {
          qa.inconclusive(`${role} (vai trò "${roleName}") đang có AdminUsers trên DB local — trả vai trò mặc định cho tài khoản seed rồi chạy lại`)
        }
        const admin = await qa.api('admin')
        const a = await ensureUser(qa, admin, 'a')
        await qa.goto('/calendar')
        await qa.expect(qa.page.getByRole('link', { name: 'Người dùng', exact: true })).toHaveCount(0)
        // USER_FT08: the capability-less 404 page sends no GET /api/users at all.
        let listCalls = 0
        qa.page.on('request', (r) => {
          if (r.method() === 'GET' && new URL(r.url()).pathname === '/api/users') listCalls += 1
        })
        await qa.goto(USERS_PATH)
        await qa.expect(qa.page.getByRole('heading', { name: 'Không tìm thấy trang' })).toBeVisible()
        await qa.shot('404')
        await noSidewaysScroll(qa)
        qa.expect(listCalls, 'the 404 page called GET /api/users').toBe(0)
        const api = await qa.api()
        const body = { fullName: a.fullName, email: QA_USERS.a.email, roleId: ROLE.chair.id }
        const calls = [
          ['GET /api/users', api.get('/api/users')],
          ['POST /api/users', api.post('/api/users', { ...body, email: `qa-fe02-cam-${RUN}@trustcabinet.local`, password: PASSWORD })],
          ['GET /api/users/{id}', api.get(`/api/users/${a.id}`)],
          ['PUT /api/users/{id}', api.put(`/api/users/${a.id}`, body)],
          ['PUT /api/users/{id}/status', api.put(`/api/users/${a.id}/status`, { isActive: false })],
          ['POST /api/users/{id}/password-reset', api.post(`/api/users/${a.id}/password-reset`)],
        ]
        for (const [label, call] of calls) {
          const res = await call
          qa.expect(res.status, `${label} as ${role}`).toBe(403)
        }
        const after = (await listUsers(qa, admin)).find((u) => u.id === a.id)
        qa.expect([after.roleId, after.isActive]).toEqual([ROLE.member.id, true])
      },
    })),
    {
      id: 'AC21',
      role: 'anon',
      title: 'Chưa đăng nhập: /api/users trả 401, mở /mgmt/admin/users bị đưa về đăng nhập',
      steps: ['GET /api/users, PUT /api/users/{id}/status không token', 'Mở /mgmt/admin/users khi chưa đăng nhập'],
      expected: '401; trang đăng nhập',
      async run(qa) {
        const anon = await qa.api('anon')
        qa.expectStatus(await anon.get('/api/users'), 401)
        qa.expectStatus(await anon.put(`/api/users/${UNKNOWN_ID}/status`, { isActive: false }), 401)
        qa.expectStatus(await anon.post(`/api/users/${UNKNOWN_ID}/password-reset`), 401)
        await qa.goto(USERS_PATH)
        await qa.expect(qa.page).toHaveURL(/\/login/)
        await qa.shot('dang-nhap')
      },
    },
    // ------------------------------------------------------------ AC bổ sung cho Round 1 (R1-FE02)
    {
      id: 'AC22',
      role: 'admin',
      devices: ['desktop', 'phone'],
      title: 'Danh sách tải chậm hiện 6 dòng skeleton; tải lỗi thì báo và "Thử lại" tải được (USER_FT02)',
      steps: ['Mở Người dùng khi GET /api/users lần đầu treo 1,5 giây rồi 500', 'Bấm "Thử lại" khi máy chủ đã trả bình thường'],
      expected:
        'Lúc đang tải: đúng 6 dòng ".row-skeleton"; lỗi: câu lỗi tiếng Việt trong bảng, nút "Thử lại"; bấm Thử lại thì danh sách hiện',
      async run(qa) {
        // Warm the lazy chunk first: the skeleton assertion below runs while only the API is held.
        await qa.goto(USERS_PATH)
        await qa.expect(qa.page.getByRole('heading', { name: 'Người dùng', exact: true })).toBeVisible()
        // Hold every list request without answering: the skeleton must show for as long as nothing arrives.
        // (Vite serves its own /src/api/users.ts chunk under a URL that also matches — let that one through.)
        const held = []
        await qa.page.route('**/api/users*', (route) => {
          const isList = new URL(route.request().url()).pathname === '/api/users'
          if (route.request().method() !== 'GET' || !isList) return route.continue()
          held.push(route)
        })
        await qa.page.reload()
        await qa.expect(qa.page.locator('tbody tr.row-skeleton')).toHaveCount(6, { timeout: 20_000 })
        await qa.shot('dang-tai-skeleton')
        for (const route of held.splice(0)) {
          await route.fulfill({ status: 500, contentType: 'application/json', body: '{}' })
        }
        const retry = qa.page.getByRole('button', { name: 'Thử lại' })
        await qa.expect(retry).toBeVisible()
        await qa.expect(qa.page.locator('.cell-error')).not.toBeEmpty()
        await noRawCodes(qa)
        await qa.shot('loi-tai-danh-sach')
        await qa.page.unroute('**/api/users*')
        await retry.click()
        await qa.expect(qa.page.getByRole('heading', { name: 'Người dùng', exact: true })).toBeVisible()
        await qa.expect(qa.page.locator('tbody tr.row-skeleton')).toHaveCount(0)
        await qa.expect(qa.page.locator('tbody tr').first()).toBeVisible()
        await settled(qa)
        await qa.shot('thu-lai-thanh-cong')
        await noSidewaysScroll(qa)
      },
    },
    {
      id: 'AC23',
      role: 'admin',
      title: 'Cỡ trang và trang trong URL; ?role lạ thành "Tất cả vai trò"; vai trò mới hiện trong lọc và hộp thoại (USER_FT06, FT07, FT36)',
      steps: [
        'Mở /mgmt/admin/users?pageSize=20&page=2',
        'Đổi "Số dòng mỗi trang" lên 50 rồi về 20',
        'Mở ?page=999 rồi ?role=<id lạ>',
        'Tạo vai trò [QA], xem ô lọc và hộp Sửa',
      ],
      expected:
        'Trang 2 đúng 20 dòng, URL giữ pageSize/page; lên 50 rồi về 20 thì hết ?pageSize/?page (20 là mặc định); ?page=999 hiện trang cuối; ?role lạ: ô lọc hiện "Tất cả vai trò", vẫn đủ dòng; vai trò mới hiện trong ô lọc và hộp Sửa',
      async run(qa) {
        const api = await qa.api()
        await ensureUser(qa, api, 'a')
        const users = await openUsers(qa)
        await qa.goto(`${USERS_PATH}?pageSize=20&page=2`)
        await qa.expect(qa.page).toHaveURL(/pageSize=20/)
        await qa.expect(qa.page).toHaveURL(/page=2/)
        await qa.expect(qa.page.locator('tbody tr')).toHaveCount(Math.min(20, Math.max(0, users.length - 20)))
        await qa.expect(userRow(qa, users[20].email)).toBeVisible()
        await qa.expect(qa.page.getByRole('navigation', { name: 'Phân trang' })).toContainText(`trong ${users.length.toLocaleString('vi-VN')} tài khoản`)
        await qa.shot('trang-2-co-20-dong')
        await pickOption(qa, /^Số dòng mỗi trang/, 'Số dòng mỗi trang', '50')
        await qa.expect(qa.page).toHaveURL(/pageSize=50/)
        await qa.expect(qa.page).not.toHaveURL(/page=/)
        await qa.expect(qa.page.locator('tbody tr')).toHaveCount(Math.min(50, users.length))
        await pickOption(qa, /^Số dòng mỗi trang/, 'Số dòng mỗi trang', '20')
        await qa.expect(qa.page).not.toHaveURL(/pageSize=/)
        await qa.expect(qa.page).not.toHaveURL(/page=/)
        await qa.expect(qa.page.locator('tbody tr')).toHaveCount(Math.min(users.length, 20))
        await qa.shot('ve-co-mac-dinh')
        await qa.goto(`${USERS_PATH}?page=999`)
        await qa.expect(userRow(qa, users[users.length - 1].email)).toBeVisible()
        await qa.expect(qa.page.getByRole('navigation', { name: 'Phân trang' })).toContainText(`trong ${users.length.toLocaleString('vi-VN')} tài khoản`)
        await qa.shot('trang-qua-lon-ve-cuoi')
        // USER_FT06 E1: a role the URL names but the data has not becomes "Tất cả vai trò".
        await qa.goto(`${USERS_PATH}?role=${UNKNOWN_ID}`)
        await qa.expect(qa.page.getByRole('button', { name: 'Lọc theo vai trò: Tất cả vai trò' })).toBeVisible()
        await qa.expect(qa.page.locator('tbody tr')).toHaveCount(Math.min(users.length, 20))
        // USER_FT36: the filter list matches the roles on file, new ones included.
        const roleName = qa.name(`FE-02 lọc ${RUN}`)
        const created = await api.post('/api/roles', { name: roleName, description: null, capabilities: [] })
        qa.expectStatus(created, 201)
        try {
          await qa.goto(USERS_PATH)
          await qa.page.getByRole('button', { name: /^Lọc theo vai trò/ }).click()
          await qa.expect(
            qa.page.getByRole('listbox', { name: 'Lọc theo vai trò' }).getByRole('option', { name: roleName, exact: true }),
          ).toBeVisible()
          await qa.page.keyboard.press('Escape')
          const a = (await listUsers(qa, api)).find((u) => u.email === QA_USERS.a.email)
          const rowA = userRow(qa, QA_USERS.a.email)
          await rowA.scrollIntoViewIfNeeded()
          await rowA.getByRole('button', { name: `Sửa tài khoản ${a.fullName}` }).click()
          const dialog = qa.page.getByRole('dialog', { name: 'Sửa tài khoản' })
          await dialog.getByRole('button', { name: /^Vai trò:/ }).click()
          await qa.expect(
            qa.page.getByRole('listbox', { name: 'Vai trò' }).getByRole('option', { name: roleName, exact: true }),
          ).toBeVisible()
          await qa.page.keyboard.press('Escape')
          await dialog.getByRole('button', { name: 'Huỷ' }).click()
          await qa.shot('vai-tro-moi-trong-loc')
          await noSidewaysScroll(qa)
        } finally {
          await api.delete(`/api/roles/${created.body.role.id}`)
        }
      },
    },
    {
      id: 'AC24',
      role: 'admin',
      title: 'Đóng hộp Tạo/Sửa khi đã nhập thì hỏi "Bỏ thay đổi chưa lưu?"; không nhập thì đóng ngay (USER_FT15, USER_FT22 E2)',
      steps: [
        'Mở hộp Tạo, nhập họ tên, bấm Huỷ → "Tiếp tục sửa" giữ chữ → Huỷ → "Bỏ thay đổi"',
        'Mở hộp Sửa A, đổi họ tên, bấm Huỷ → "Bỏ thay đổi", dòng không đổi',
        'Mở hộp Tạo, bấm Huỷ ngay',
      ],
      expected:
        'Hộp hỏi nêu "Những gì bạn vừa nhập sẽ không được lưu."; "Tiếp tục sửa" giữ chữ đã nhập; "Bỏ thay đổi" đóng, không gửi request, dòng Sửa không đổi; hộp chưa nhập đóng ngay không hỏi',
      async run(qa) {
        const api = await qa.api()
        const a = await ensureUser(qa, api, 'a')
        let posts = 0
        let puts = 0
        qa.page.on('request', (r) => {
          if (r.url().endsWith('/api/users') && r.method() === 'POST') posts += 1
          if (/\/api\/users\//.test(r.url()) && r.method() === 'PUT') puts += 1
        })
        await openUsers(qa)
        await qa.page.getByRole('button', { name: 'Tạo tài khoản' }).click()
        let dialog = qa.page.getByRole('dialog', { name: 'Tạo tài khoản' })
        const typed = qa.name(`FE-02 dở ${RUN}`)
        await dialog.getByLabel('Họ tên').fill(typed)
        await dialog.getByRole('button', { name: 'Huỷ' }).click()
        let confirm = qa.page.getByRole('dialog', { name: 'Bỏ thay đổi chưa lưu?' })
        await qa.expect(confirm).toBeVisible()
        await qa.expect(confirm).toContainText('Những gì bạn vừa nhập sẽ không được lưu.')
        await qa.shot('hoi-bo-thay-doi-tao')
        await confirm.getByRole('button', { name: 'Tiếp tục sửa' }).click()
        await qa.expect(dialog.getByLabel('Họ tên')).toHaveValue(typed)
        await dialog.getByRole('button', { name: 'Huỷ' }).click()
        await confirm.getByRole('button', { name: 'Bỏ thay đổi' }).click()
        await qa.expect(dialog).toHaveCount(0)
        const row = userRow(qa, QA_USERS.a.email)
        await row.scrollIntoViewIfNeeded()
        await row.getByRole('button', { name: `Sửa tài khoản ${a.fullName}` }).click()
        dialog = qa.page.getByRole('dialog', { name: 'Sửa tài khoản' })
        await dialog.getByLabel('Họ tên').fill(`${a.fullName} (sửa dở)`)
        await dialog.getByRole('button', { name: 'Huỷ' }).click()
        confirm = qa.page.getByRole('dialog', { name: 'Bỏ thay đổi chưa lưu?' })
        await qa.expect(confirm).toBeVisible()
        await confirm.getByRole('button', { name: 'Bỏ thay đổi' }).click()
        await qa.expect(dialog).toHaveCount(0)
        qa.expect((await listUsers(qa, api)).find((u) => u.id === a.id).fullName).toBe(a.fullName)
        await qa.shot('bo-thay-doi-sua')
        await qa.page.getByRole('button', { name: 'Tạo tài khoản' }).click()
        dialog = qa.page.getByRole('dialog', { name: 'Tạo tài khoản' })
        await dialog.getByRole('button', { name: 'Huỷ' }).click()
        await qa.expect(dialog).toHaveCount(0)
        await qa.expect(qa.page.getByRole('dialog', { name: 'Bỏ thay đổi chưa lưu?' })).toHaveCount(0)
        qa.expect(posts, 'a discarded create was sent').toBe(0)
        qa.expect(puts, 'a discarded edit was sent').toBe(0)
        await noSidewaysScroll(qa)
      },
    },
    {
      id: 'AC25',
      role: 'admin',
      title: 'Tự đổi email của chính mình: cảnh báo trước, sau lưu về đăng nhập và đăng nhập lại được (USER_FT19 E2)',
      steps: [
        'Mở hộp Sửa của chính mình (dòng "Bạn"), đổi email sang địa chỉ [QA] mới',
        'Lưu, theo về /login, đăng nhập bằng email mới',
        'Trả email cũ (API), đăng nhập lại',
      ],
      expected:
        'Cảnh báo "Bạn đang đổi email của chính mình. Sau khi lưu, bạn sẽ bị đăng xuất và cần đăng nhập lại."; lưu xong về /login; đăng nhập email mới được; đăng nhập lại về đúng trang cũ',
      async run(qa) {
        const api = await qa.api()
        const me = (await api.get('/api/auth/me')).body
        const adminRoleId = (await listUsers(qa, api)).find((u) => u.id === me.id).roleId
        const newEmail = `qa-fe02-toi-moi@trustcabinet.local`
        try {
          await openUsers(qa)
          const selfRow = userRow(qa, me.email)
          await selfRow.scrollIntoViewIfNeeded()
          await selfRow.getByRole('button', { name: `Sửa tài khoản ${me.fullName}` }).click()
          const dialog = qa.page.getByRole('dialog', { name: 'Sửa tài khoản' })
          await dialog.getByRole('textbox', { name: /^Email/ }).fill(newEmail)
          await qa.expect(dialog.getByText('Bạn đang đổi email của chính mình. Sau khi lưu, bạn sẽ bị đăng xuất và cần đăng nhập lại.')).toBeVisible()
          await qa.shot('canh-bao-doi-email-minh')
          await dialog.getByRole('button', { name: 'Lưu thay đổi' }).click()
          await qa.expect(toast(qa, 'Cập nhật tài khoản thành công.')).toBeVisible()
          await qa.expect(qa.page).toHaveURL(/\/login/)
          await qa.shot('doi-email-minh-ve-login')
        } finally {
          const now = (await listUsers(qa, api)).find((u) => u.id === me.id)
          if (now && now.email !== me.email) {
            qa.expectStatus(await api.put(`/api/users/${me.id}`, { fullName: now.fullName, email: me.email, roleId: now.roleId }), 200)
          }
        }
        // The new email signs in; the old one does not.
        qa.expectStatus(await rawLogin(qa, newEmail), 401)
        qa.expectStatus(await rawLogin(qa, me.email), 200)
        // Sign back in on screen to continue as the administrator (the app returns to the page it left).
        await qa.goto('/login')
        await qa.page.getByLabel('Email').fill(me.email)
        await qa.page.getByLabel('Mật khẩu', { exact: true }).fill(PASSWORD)
        await qa.page.getByRole('button', { name: 'Đăng nhập bằng mật khẩu' }).click()
        await qa.expect(qa.page).not.toHaveURL(/\/login/)
        await qa.expect(qa.page.getByRole('heading', { name: 'Người dùng', exact: true })).toBeVisible()
        await settled(qa)
        await qa.shot('dang-nhap-lai-ve-dung-trang')
        await noSidewaysScroll(qa)
      },
    },
    {
      id: 'AC26',
      role: 'admin',
      title: 'Đổi email người khác giữa chừng: phiên của người đó hết, mở lại trang về đăng nhập (USER_FT20)',
      steps: ['Đăng nhập B trên màn hình', 'Quản trị (API riêng) đổi email B sang địa chỉ mới', 'Tải lại trang rồi đăng nhập lại'],
      expected:
        'Tải lại về /login; email cũ không đăng nhập được, email mới đăng nhập được; trả email cũ xong B lại vào được',
      async run(qa) {
        const api = await qa.api('admin')
        const b = await ensureUser(qa, api, 'b')
        const newEmail = `qa-fe02-b-moi@trustcabinet.local`
        // B drives the browser; the administrator only acts through its own API client.
        await qa.context.clearCookies()
        try {
          await qa.goto('/login')
          await qa.page.getByLabel('Email').fill(QA_USERS.b.email)
          await qa.page.getByLabel('Mật khẩu', { exact: true }).fill(PASSWORD)
          await qa.page.getByRole('button', { name: 'Đăng nhập bằng mật khẩu' }).click()
          await qa.expect(qa.page).toHaveURL(/\/calendar(\?|$)/)
          qa.expectStatus(await api.put(`/api/users/${b.id}`, { fullName: b.fullName, email: newEmail, roleId: ROLE.member.id }), 200)
          await qa.page.reload()
          await qa.expect(qa.page).toHaveURL(/\/login/)
          await qa.shot('b-bi-doi-email-ve-login')
          qa.expectStatus(await rawLogin(qa, QA_USERS.b.email), 401)
          qa.expectStatus(await rawLogin(qa, newEmail), 200)
        } finally {
          await ensureUser(qa, api, 'b')
        }
      },
    },
    {
      id: 'AC27',
      role: 'admin',
      title: 'Vô hiệu hoá người khác giữa chừng: phiên của người đó hết, mở lại trang về đăng nhập kèm lời nhắn (USER_FT25)',
      steps: ['Đăng nhập B trên màn hình', 'Quản trị (API riêng) vô hiệu hoá B', 'Tải lại trang rồi đăng nhập lại; kích hoạt lại B'],
      expected:
        'Tải lại về /login kèm "Phiên đăng nhập đã kết thúc. Vui lòng đăng nhập lại."; đăng nhập báo MSG02; kích hoạt lại thì B đăng nhập được',
      async run(qa) {
        const api = await qa.api('admin')
        const b = await ensureUser(qa, api, 'b')
        await qa.context.clearCookies()
        try {
          await qa.goto('/login')
          await qa.page.getByLabel('Email').fill(QA_USERS.b.email)
          await qa.page.getByLabel('Mật khẩu', { exact: true }).fill(PASSWORD)
          await qa.page.getByRole('button', { name: 'Đăng nhập bằng mật khẩu' }).click()
          await qa.expect(qa.page).toHaveURL(/\/calendar(\?|$)/)
          qa.expectStatus(await api.put(`/api/users/${b.id}/status`, { isActive: false }), 200)
          await qa.page.reload()
          await qa.expect(qa.page).toHaveURL(/\/login/)
          await qa.expect(qa.page.getByText('Phiên đăng nhập đã kết thúc. Vui lòng đăng nhập lại.')).toBeVisible()
          await qa.shot('b-bi-vo-hieu-hoa-ve-login')
          const denied = await rawLogin(qa, QA_USERS.b.email)
          qa.expectStatus(denied, 401)
          qa.expect(denied.body.code).toBe('MSG02')
        } finally {
          await ensureUser(qa, api, 'b')
        }
        qa.expectStatus(await rawLogin(qa, QA_USERS.b.email), 200)
      },
    },
    {
      id: 'AC28',
      role: 'admin',
      title: 'Hộp Sửa khi không tải được tài khoản thì báo lỗi và cho thử lại, không hiện dữ liệu cũ (USER_FT23)',
      steps: ['Mở hộp Sửa A khi GET /api/users/{id} 500 lần đầu', 'Bấm "Thử lại" khi máy chủ đã trả bình thường'],
      expected:
        'Hộp hiện câu lỗi, không có giá trị cũ, nút "Lưu thay đổi" bị khoá; bấm Thử lại thì form điền sẵn như cũ',
      async run(qa) {
        const api = await qa.api()
        const a = await ensureUser(qa, api, 'a')
        // Hold the account read without answering, StrictMode or not: the dialog must show its loading state first.
        const held = []
        await qa.page.route(`**/api/users/${a.id}`, (route) => {
          if (route.request().method() !== 'GET') return route.continue()
          held.push(route)
        })
        await openUsers(qa)
        const row = userRow(qa, QA_USERS.a.email)
        await row.scrollIntoViewIfNeeded()
        await row.getByRole('button', { name: `Sửa tài khoản ${a.fullName}` }).click()
        const dialog = qa.page.getByRole('dialog', { name: 'Sửa tài khoản' })
        await qa.expect(dialog.getByText('Đang tải thông tin tài khoản…')).toBeVisible()
        await qa.expect(dialog.getByRole('button', { name: 'Lưu thay đổi' })).toBeDisabled()
        await qa.shot('sua-dang-tai')
        for (const route of held.splice(0)) {
          await route.abort('failed')
        }
        await qa.expect(dialog.getByRole('button', { name: 'Thử lại' })).toBeVisible()
        await qa.expect(dialog.getByLabel('Họ tên')).toHaveCount(0)
        await qa.expect(dialog.getByRole('button', { name: 'Lưu thay đổi' })).toBeDisabled()
        await noRawCodes(qa)
        await qa.shot('sua-loi-tai')
        await qa.page.unroute(`**/api/users/${a.id}`)
        await dialog.getByRole('button', { name: 'Thử lại' }).click()
        await qa.expect(dialog.getByLabel('Họ tên')).toHaveValue(a.fullName)
        await qa.expect(dialog.getByRole('button', { name: 'Lưu thay đổi' })).toBeEnabled()
        await qa.shot('sua-thu-lai-duoc')
        await dialog.getByRole('button', { name: 'Huỷ' }).click()
      },
    },
    {
      id: 'AC29',
      role: 'admin',
      title: 'Huỷ xác nhận vô hiệu hoá thì dòng không đổi, không gửi gì (USER_FT28)',
      steps: ['Bấm Vô hiệu hoá ở dòng B, bấm Huỷ trong hộp xác nhận'],
      expected: 'Hộp đóng, không có PUT /api/users/{id}/status, dòng B vẫn "Hoạt động"',
      async run(qa) {
        const api = await qa.api()
        const b = await ensureUser(qa, api, 'b')
        let puts = 0
        qa.page.on('request', (r) => {
          if (/\/api\/users\/.+\/status/.test(r.url()) && r.method() === 'PUT') puts += 1
        })
        try {
          await openUsers(qa)
          const row = userRow(qa, QA_USERS.b.email)
          await row.scrollIntoViewIfNeeded()
          await row.getByRole('button', { name: `Vô hiệu hoá tài khoản ${b.fullName}` }).click()
          const confirm = qa.page.getByRole('dialog', { name: 'Vô hiệu hoá tài khoản' })
          await qa.expect(confirm).toBeVisible()
          await confirm.getByRole('button', { name: 'Huỷ' }).click()
          await qa.expect(confirm).toHaveCount(0)
          await qa.expect(row.getByText('Hoạt động')).toBeVisible()
          qa.expect(puts, 'a cancelled deactivation was sent').toBe(0)
          await qa.shot('huy-vo-hieu-hoa')
        } finally {
          await ensureUser(qa, api, 'b')
        }
      },
    },
    {
      id: 'AC30',
      role: 'admin',
      title: 'Hộp đặt lại mật khẩu: cảnh báo khi tự đặt cho chính mình; tài khoản bị vô hiệu hoá giữa chừng thì 409 và danh sách cập nhật (USER_FT39, USER_FT40)',
      steps: [
        'Mở hộp đặt lại cho chính mình',
        'Mở hộp đặt lại cho B, vô hiệu hoá B (API), bấm "Gửi liên kết"',
        'Kích hoạt lại B',
      ],
      expected:
        'Hộp của chính mình thêm "Đây là tài khoản của bạn: khi đặt mật khẩu mới qua liên kết, mọi phiên đăng nhập của bạn sẽ kết thúc."; hộp của B báo "Tài khoản này đã bị vô hiệu hoá nên không đặt lại mật khẩu được. Danh sách đã được cập nhật." (409 MSG14), nút "Gửi liên kết" bị khoá, dòng B hiện "Đã vô hiệu hoá"',
      async run(qa) {
        const api = await qa.api()
        const me = (await api.get('/api/auth/me')).body
        const b = await ensureUser(qa, api, 'b')
        try {
          await openUsers(qa)
          const selfRow = userRow(qa, me.email)
          await selfRow.scrollIntoViewIfNeeded()
          await selfRow.getByRole('button', { name: `Gửi liên kết đặt lại mật khẩu cho ${me.fullName}` }).click()
          let dialog = qa.page.getByRole('dialog', { name: 'Đặt lại mật khẩu' })
          await qa.expect(dialog).toContainText('Đây là tài khoản của bạn: khi đặt mật khẩu mới qua liên kết, mọi phiên đăng nhập của bạn sẽ kết thúc.')
          await qa.shot('canh-bao-tu-dat-cho-minh')
          await dialog.getByRole('button', { name: 'Huỷ' }).click()
          const row = userRow(qa, QA_USERS.b.email)
          await row.scrollIntoViewIfNeeded()
          await row.getByRole('button', { name: `Gửi liên kết đặt lại mật khẩu cho ${b.fullName}` }).click()
          dialog = qa.page.getByRole('dialog', { name: 'Đặt lại mật khẩu' })
          await qa.expect(dialog).toContainText(QA_USERS.b.email)
          qa.expectStatus(await api.put(`/api/users/${b.id}/status`, { isActive: false }), 200)
          const posted = qa.page.waitForResponse((r) => r.url().endsWith(`/api/users/${b.id}/password-reset`))
          await dialog.getByRole('button', { name: 'Gửi liên kết' }).click()
          qa.expect((await posted).status()).toBe(409)
          await qa.expect(dialog.getByText('Tài khoản này đã bị vô hiệu hoá nên không đặt lại mật khẩu được. Danh sách đã được cập nhật.')).toBeVisible()
          await qa.expect(dialog.getByRole('button', { name: 'Gửi liên kết' })).toBeDisabled()
          await qa.expect(row.getByText('Đã vô hiệu hoá').filter({ visible: true })).toBeVisible()
          await qa.shot('vo-hieu-hoa-giua-chung')
          await noRawCodes(qa)
        } finally {
          await ensureUser(qa, api, 'b')
        }
      },
    },
    {
      id: 'AC31',
      role: 'admin',
      title: 'Nút "Xem kho" ở dòng: tài khoản hoạt động bị khoá kèm lý do, tài khoản đã vô hiệu hoá mở được kho (USER_FT30)',
      steps: ['Xem dòng A (hoạt động)', 'Vô hiệu hoá C, bấm "Xem kho tài liệu" ở dòng C'],
      expected:
        'Dòng hoạt động: nút chìa khoá disable, tooltip "Chỉ xem được kho của tài khoản đã vô hiệu hoá"; dòng đã vô hiệu hoá: bấm vào /mgmt/admin/users/{id}/library, trang kho mở được (chỉ đọc)',
      async run(qa) {
        const api = await qa.api()
        const a = await ensureUser(qa, api, 'a')
        const c = await ensureUser(qa, api, 'c', { active: false })
        await openUsers(qa)
        const rowA = userRow(qa, QA_USERS.a.email)
        await rowA.scrollIntoViewIfNeeded()
        const folderA = rowA.getByRole('button', { name: `Xem kho tài liệu của ${a.fullName}` })
        await qa.expect(folderA).toBeDisabled()
        await qa.expect(folderA).toHaveAttribute('data-tooltip', 'Chỉ xem được kho của tài khoản đã vô hiệu hoá')
        await qa.shot('nut-kho-bi-khoa')
        const rowC = userRow(qa, QA_USERS.c.email)
        await rowC.scrollIntoViewIfNeeded()
        await rowC.getByRole('link', { name: `Xem kho tài liệu của ${c.fullName}` }).click()
        await qa.expect(qa.page).toHaveURL(new RegExp(`/mgmt/admin/users/${c.id}/library`))
        await qa.expect(qa.page.getByRole('heading', { name: /Kho tài liệu|Tài liệu/ }).first()).toBeVisible()
        await settled(qa)
        await qa.shot('kho-cua-tai-khoan-vo-hieu-hoa')
        await noSidewaysScroll(qa)
      },
    },
  ],
}

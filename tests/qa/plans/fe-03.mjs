// FE-03 — Kiểm soát truy cập theo vai trò (QA subtask SCRUM-190): UC-10 xem ma trận, UC-11 cấu hình quyền, UC-12a/b/c
// tạo / sửa / xoá vai trò, UC-13 nhật ký hệ thống, plus the server-side rules of docs/03-api/04-phan-quyen-phia-server.md
// (401 signed out, 403 on a fixed endpoint, 404 for an unknown id and for a restricted UI route).
//
// Criteria come from docs/01-requirements/05-use-case-specifications.md (UC-10..UC-13), BR-16/23/313/440-443, the API
// docs 03-ma-tran-phan-quyen.md, 04-quan-ly-vai-tro.md, 21-nhat-ky-kiem-toan.md and the message catalogue.
//
// Safe on the shared local app: it never changes a built-in role, the "All-in-one" role or a seed account. Every role it
// creates is named "[QA] FE03 …" and deleted at the end of its criterion. One throwaway account, qa-fe03@trustcabinet.local,
// is reused across runs (accounts cannot be deleted, only deactivated): it is moved back to Thành viên and deactivated
// after each use. Writes aimed at built-in roles are ones that change nothing even if the server wrongly accepted them.

const PASSWORD = process.env.QA_PASSWORD ?? 'TrustCabinet@2026'
const QA_EMAIL = 'qa-fe03@trustcabinet.local'
// Fixed ids of the built-in roles (docs/03-api/04-quan-ly-vai-tro.md §2). ADR-0075: no system role Chủ toạ or Thư ký;
// Phê duyệt viên took Chủ toạ's id.
const BUILT_IN = {
  member: { id: '01a0ce78-7204-7558-b181-e908a08e83a3', name: 'Thành viên' },
  approver: { id: '01a0ce78-7202-7945-be13-d2022cf25203', name: 'Phê duyệt viên' },
  admin: { id: '01a0ce78-7205-7486-b976-d52d428d5451', name: 'Quản trị viên' },
}
const UNKNOWN_ID = '01a0ce78-0000-7000-8000-000000000000'
const RUN = Date.now().toString(36).slice(-5)
const ROLES_PATH = '/mgmt/admin/roles'
const AUDIT_PATH = '/mgmt/admin/audit-log'

// ---------------------------------------------------------------------------------------------------------------------
// helpers

/** A unique [QA] role name for this run and device. */
const roleName = (qa, label) => qa.name(`FE03 ${label} ${RUN}${qa.device[0]}`)

/** Creates a role through the API and remembers it for cleanup. */
async function createRole(qa, api, created, label, capabilities = [], description = null) {
  const name = roleName(qa, label)
  const res = await api.post('/api/roles', { name, description, capabilities })
  qa.expectStatus(res, 201)
  created.push(res.body.role.id)
  return res.body.role
}

/** Deletes every role a criterion created (the QA account must not hold them any more). */
async function cleanup(api, created) {
  for (const id of created) await api.delete(`/api/roles/${id}`)
}

async function rolesList(api) {
  const res = await api.get('/api/roles')
  if (res.status !== 200) throw new Error(`GET /api/roles: HTTP ${res.status}`)
  return res.body
}

async function matrixRow(api, id) {
  const res = await api.get('/api/roles/capabilities')
  if (res.status !== 200) throw new Error(`GET /api/roles/capabilities: HTTP ${res.status}`)
  return res.body.roles.find((r) => r.id === id)
}

/** Audit entries whose object name or details match `text` (BR-442 keyword), newest first. */
async function audit(qa, api, text, action) {
  const params = new URLSearchParams({ q: text, pageSize: '50' })
  if (action) params.set('action', action)
  const res = await api.get(`/api/audit-log?${params}`)
  qa.expectStatus(res, 200)
  return res.body.items
}

/** Puts the throwaway account on `roleId`, active; creates it the first time. Returns its id. */
async function qaAccount(qa, api, roleId, { active = true } = {}) {
  const users = await api.get('/api/users')
  qa.expectStatus(users, 200)
  let user = users.body.find((u) => u.email === QA_EMAIL)
  if (!user) {
    const res = await api.post('/api/users', {
      fullName: qa.name('FE03 tài khoản thử'),
      email: QA_EMAIL,
      password: PASSWORD,
      roleId,
    })
    qa.expectStatus(res, 201)
    user = res.body.user
  } else {
    if (!user.isActive) qa.expectStatus(await api.put(`/api/users/${user.id}/status`, { isActive: true }), 200)
    if (user.roleId !== roleId) {
      qa.expectStatus(await api.put(`/api/users/${user.id}`, { fullName: user.fullName, email: QA_EMAIL, roleId }), 200)
    }
  }
  if (!active) qa.expectStatus(await api.put(`/api/users/${user.id}/status`, { isActive: false }), 200)
  return user.id
}

/** Moves the throwaway account back to Thành viên and deactivates it, so the [QA] roles can be deleted. */
async function releaseQaAccount(api, id) {
  if (!id) return
  const users = await api.get('/api/users')
  const user = users.body?.find?.((u) => u.id === id)
  if (!user) return
  if (user.roleId !== BUILT_IN.member.id) {
    await api.put(`/api/users/${id}`, { fullName: user.fullName, email: QA_EMAIL, roleId: BUILT_IN.member.id })
  }
  if (user.isActive) await api.put(`/api/users/${id}/status`, { isActive: false })
}

async function noSidewaysScroll(qa) {
  const sideways = await qa.page.evaluate(() => document.documentElement.scrollWidth - innerWidth)
  qa.expect(sideways, 'the page scrolls sideways').toBeLessThanOrEqual(0)
}

async function noRawCodes(qa) {
  const text = await qa.page.locator('main').innerText()
  qa.expect(text, 'a raw MSG code is shown to the user').not.toMatch(/MSG\d+/)
}

async function settled(qa) {
  await qa.page.waitForFunction(() => document.getAnimations().every((a) => a.playState !== 'running'))
}

const toast = (qa, text) => qa.page.getByRole('status').getByText(text)

async function openRoles(qa) {
  await qa.goto(ROLES_PATH)
  await qa.expect(qa.page.getByRole('heading', { name: 'Vai trò', exact: true })).toBeVisible()
  await qa.expect(qa.page.getByRole('table')).toBeVisible()
}

async function openMatrix(qa) {
  await openRoles(qa)
  await qa.page.getByRole('tab', { name: /Ma trận phân quyền/ }).click()
  await qa.expect(qa.page.locator('table.matrix-table')).toBeVisible()
}

/** The data row of the role list or the matrix whose first cell starts with `name`. */
const roleRow = (qa, name) => qa.page.locator('tbody tr').filter({ has: qa.page.locator('td.cell-title', { hasText: name }) })

const DENIED = ['chair', 'secretary', 'member']

// ---------------------------------------------------------------------------------------------------------------------

// The account count cell: the number alone on a desktop, "N tài khoản" on a phone card (the unit is hidden on a
// desktop but still in the text). Matches the exact number either way.
const accounts = (n) => new RegExp(`^${n}( tài khoản)?$`)

export default {
  ticket: 'FE-03',
  title: 'Kiểm soát truy cập theo vai trò: vai trò, ma trận quyền, nhật ký hệ thống, phân quyền phía server',
  devices: ['desktop'],
  criteria: [
    // ---------------------------------------------------------------- UC-10
    {
      id: 'AC1',
      role: 'admin',
      devices: ['desktop', 'phone'],
      title: 'Quản trị mở Vai trò: danh sách vai trò kèm số tài khoản, mô tả và chip "Vai trò của bạn"; ma trận vai trò × quyền khớp dữ liệu; điện thoại hiện thẻ công tắc (UC-10, RBAC_FT13, RBAC_FT14)',
      steps: [
        'Đăng nhập quantri@ (có AdminUsers)',
        'Mở Quản trị › Vai trò',
        'Xem tab Danh sách vai trò (mô tả, chip vai trò của mình), rồi tab Ma trận phân quyền',
      ],
      expected:
        'Danh sách có đủ vai trò, 4 vai trò mặc định gắn nhãn "Mặc định", cột Tài khoản đúng số; vai trò đang mang hiện chip "Vai trò của bạn"; vai trò không mô tả hiện "Chưa có mô tả" (RBAC_FT14 E2/E3); ma trận có mọi vai trò × mọi quyền (tên quyền tiếng Việt từ backend), ô tick đúng quyền đã lưu; không có nút tạo quyền mới; trên điện thoại mỗi vai trò là một thẻ, quyền là công tắc có tên, "Hoàn tác" hiện chữ (RBAC_FT13 E1); trang không cuộn ngang',
      async run(qa) {
        const api = await qa.api()
        const matrix = (await api.get('/api/roles/capabilities')).body
        // Other testers add accounts on the shared app, so compare the screen with the answer the page itself received
        // (then check that answer against the API's role list by id and name).
        const pageRoles = qa.page.waitForResponse((r) => r.url().endsWith('/api/roles') && r.request().method() === 'GET')
        if (qa.device === 'desktop') {
          await qa.goto('/calendar')
          const link = qa.page.getByRole('link', { name: 'Vai trò', exact: true })
          if (!(await link.isVisible())) await qa.page.getByRole('button', { name: 'Quản trị' }).click()
          await link.click()
          await qa.expect(qa.page).toHaveURL(new RegExp(`${ROLES_PATH}$`))
          await qa.expect(qa.page.getByRole('table')).toBeVisible()
        } else {
          await openRoles(qa)
        }
        const roles = await (await pageRoles).json()
        qa.expect(roles.map((r) => `${r.id} ${r.name}`)).toEqual((await rolesList(api)).map((r) => `${r.id} ${r.name}`))
        for (const role of roles) {
          const row = roleRow(qa, role.name).first()
          await qa.expect(row).toBeVisible()
          await qa.expect(row.locator('td').nth(2)).toHaveText(accounts(role.userCount))
          if (role.isBuiltIn) await qa.expect(row.getByText('Mặc định')).toBeVisible()
        }
        await qa.shot('danh-sach-vai-tro', { fullPage: true })
        await noSidewaysScroll(qa)
        // RBAC_FT14 E2/E3: the signed-in account's role carries "Vai trò của bạn"; a role without a
        // description reads "Chưa có mô tả".
        const me = (await api.get('/api/auth/me')).body
        const myRole = roles.find((r) => r.name === me.roleName)
        if (myRole) await qa.expect(roleRow(qa, myRole.name).first().getByText('Vai trò của bạn')).toBeVisible()
        const nodesc = roles.find((r) => !r.description)
        if (nodesc) {
          await qa.expect(roleRow(qa, nodesc.name).first().getByText('Chưa có mô tả')).toBeVisible()
          qa.note(`Vai trò không mô tả: ${nodesc.name}`)
        } else {
          qa.note('Mọi vai trò hiện có đều có mô tả; nhánh "Chưa có mô tả" chưa phủ.')
        }

        await qa.page.getByRole('tab', { name: /Ma trận phân quyền/ }).click()
        await qa.expect(qa.page.locator('table.matrix-table')).toBeVisible()
        if (qa.device === 'phone') {
          // RBAC_FT13 E1: on a phone each role is a card and each capability a named switch.
          // Flip a capability the first role lacks (or unflip one it has), so the draft really changes.
          const mRole = matrix.roles[0]
          const missing = matrix.capabilities.find((c) => !mRole.capabilities.includes(c.code))
          const target = missing ?? matrix.capabilities[0]
          const box = qa.page.getByRole('checkbox', { name: `${mRole.name} — ${target.name}`, exact: true })
          await qa.expect(box).toBeVisible()
          if (missing) await box.check()
          else await box.uncheck()
          const undoName = `Hoàn tác thay đổi của ${mRole.name}`
          await qa.expect(qa.page.getByRole('button', { name: undoName, exact: true })).toBeVisible()
          await qa.expect(qa.page.getByRole('button', { name: undoName, exact: true })).toContainText('Hoàn tác')
          await settled(qa)
          await qa.shot('the-cong-tac-dien-thoai')
          await qa.page.getByRole('button', { name: undoName, exact: true }).click()
          if (missing) await qa.expect(box).not.toBeChecked()
          else await qa.expect(box).toBeChecked()
        }
        for (const cap of matrix.capabilities) {
          await qa.expect(qa.page.locator('th.matrix-cap', { hasText: cap.name })).toHaveCount(1)
        }
        for (const role of matrix.roles) {
          for (const cap of matrix.capabilities) {
            const box = qa.page.getByRole('checkbox', { name: `${role.name} — ${cap.name}`, exact: true })
            if (role.capabilities.includes(cap.code)) await qa.expect(box).toBeChecked()
            else await qa.expect(box).not.toBeChecked()
          }
        }
        await qa.expect(qa.page.getByRole('button', { name: /Tạo quyền/ })).toHaveCount(0)
        await qa.shot('ma-tran-phan-quyen', { fullPage: true })
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
        // Each functional group should head one contiguous block of columns.
        const groups = await qa.page.locator('th.matrix-group').allInnerTexts()
        if (new Set(groups).size !== groups.length) {
          qa.note(`Ma trận lặp tiêu đề nhóm quyền: ${groups.join(' | ')} (quyền cùng nhóm không nằm liền nhau)`)
        }
      },
    },
    {
      id: 'AC2',
      role: 'admin',
      title: 'Không tải được ma trận: báo lỗi tiếng Việt và có Thử lại, thử lại thì hiện ma trận (UC-10 E1)',
      steps: ['Mở Vai trò khi GET /api/roles/capabilities trả 500 (giả lập)', 'Bấm Thử lại khi máy chủ đã trả lời bình thường'],
      expected: 'Thông báo lỗi tiếng Việt, không hiện mã thô, nút "Thử lại"; bấm Thử lại thì danh sách vai trò hiện ra',
      async run(qa) {
        await qa.page.route('**/api/roles/capabilities', (route) =>
          route.fulfill({ status: 500, contentType: 'application/json', body: '{}' }),
        )
        await qa.goto(ROLES_PATH)
        const retry = qa.page.getByRole('button', { name: 'Thử lại' })
        await qa.expect(retry).toBeVisible()
        await qa.expect(qa.page.locator('.cell-error')).not.toBeEmpty()
        await noRawCodes(qa)
        await qa.shot('loi-tai-ma-tran')
        await qa.page.unroute('**/api/roles/capabilities')
        await retry.click()
        await qa.expect(roleRow(qa, BUILT_IN.approver.name)).toBeVisible()
        await qa.shot('thu-lai-thanh-cong')
      },
    },

    // ---------------------------------------------------------------- UC-12a
    {
      id: 'AC3',
      role: 'admin',
      devices: ['desktop', 'phone'],
      title: 'Tạo vai trò có tên, mô tả và một quyền: báo thành công, hiện trong danh sách (0 tài khoản) và ma trận, ghi nhật ký (UC-12a)',
      steps: ['Mở Vai trò, bấm Tạo vai trò', 'Nhập tên, mô tả, chọn "Thu giọng mẫu hộ"', 'Bấm Tạo vai trò'],
      expected:
        'Toast "Tạo vai trò thành công." (MSG19); dòng mới trong danh sách, 0 tài khoản, có nút sửa/xoá (trên điện thoại thấy được không cần cuộn ngang bảng); ma trận tick đúng "Thu giọng mẫu hộ"; nhật ký có "role.created" kèm quyền',
      async run(qa) {
        const api = await qa.api()
        const created = []
        try {
          const name = roleName(qa, 'Tạo')
          await openRoles(qa)
          await qa.page.getByRole('button', { name: 'Tạo vai trò' }).click()
          const dialog = qa.page.getByRole('dialog', { name: 'Tạo vai trò' })
          await qa.expect(dialog).toBeVisible()
          await dialog.getByLabel('Tên vai trò').fill(name)
          await dialog.getByLabel('Mô tả').fill('Vai trò thử của QA')
          await dialog.getByRole('checkbox', { name: /Thu giọng mẫu hộ/ }).check()
          await settled(qa)
          await qa.shot('form-tao-vai-tro')
          if (qa.device === 'phone') {
            const box = await dialog.getByRole('button', { name: 'Tạo vai trò' }).boundingBox()
            qa.expect(box, 'the submit button is off screen').not.toBeNull()
            qa.expect(box.y + box.height, 'the submit button is below the screen').toBeLessThanOrEqual(844)
          }
          await dialog.getByRole('button', { name: 'Tạo vai trò' }).click()
          await qa.expect(toast(qa, 'Tạo vai trò thành công.')).toBeVisible()
          await qa.expect(dialog).toHaveCount(0)
          const role = (await rolesList(api)).find((r) => r.name === name)
          qa.expect(role, 'the new role is not in GET /api/roles').toBeTruthy()
          created.push(role.id)
          qa.expect(role.isBuiltIn).toBe(false)
          qa.expect(role.description).toBe('Vai trò thử của QA')
          const row = roleRow(qa, name)
          await qa.expect(row.locator('td').nth(2)).toHaveText(accounts(0))
          await qa.expect(qa.page.getByRole('button', { name: `Sửa vai trò ${name}` })).toBeVisible()
          await qa.expect(qa.page.getByRole('button', { name: `Xoá vai trò ${name}` })).toBeVisible()
          await row.scrollIntoViewIfNeeded()
          await qa.shot('vai-tro-moi-trong-danh-sach')
          await noSidewaysScroll(qa)
          if (qa.device === 'phone') {
            // check-responsive: the actions a row needs stay on screen without scrolling the table sideways.
            const del = await qa.page.getByRole('button', { name: `Xoá vai trò ${name}` }).boundingBox()
            qa.expect(del.x + del.width, 'the edit/delete actions are off screen on a 390px phone').toBeLessThanOrEqual(390)
          }
          qa.expect((await matrixRow(api, role.id)).capabilities).toEqual(['VoiceCollector'])
          await qa.page.getByRole('tab', { name: /Ma trận phân quyền/ }).click()
          await qa.expect(qa.page.getByRole('checkbox', { name: `${name} — Thu giọng mẫu hộ` })).toBeChecked()
          await qa.expect(qa.page.getByRole('checkbox', { name: `${name} — Tạo cuộc họp` })).not.toBeChecked()
          await roleRow(qa, name).scrollIntoViewIfNeeded()
          await qa.shot('vai-tro-moi-trong-ma-tran')
          const entries = await audit(qa, api, name, 'role.created')
          qa.expect(entries.length, 'no role.created audit entry').toBe(1)
          qa.expect(JSON.stringify(entries[0].details)).toContain('VoiceCollector')
          qa.expect(entries[0].actor.email).toBe('quantri@trustcabinet.local')
        } finally {
          await cleanup(api, created)
        }
      },
    },
    {
      id: 'AC4',
      role: 'admin',
      title: 'Tạo vai trò không chọn quyền: được chấp nhận, dòng ma trận trống (UC-12a AF-1)',
      steps: ['POST /api/roles chỉ có tên', 'Mở ma trận'],
      expected: '201 MSG19, quyền = []; dòng của vai trò trong ma trận không có ô nào được tick',
      async run(qa) {
        const api = await qa.api()
        const created = []
        try {
          const res = await api.post('/api/roles', { name: roleName(qa, 'Trống') })
          qa.expectStatus(res, 201)
          qa.expect(res.body.code).toBe('MSG19')
          created.push(res.body.role.id)
          qa.expect((await matrixRow(api, res.body.role.id)).capabilities).toEqual([])
          await openMatrix(qa)
          const row = roleRow(qa, res.body.role.name)
          await qa.expect(row.getByRole('checkbox')).not.toHaveCount(0)
          for (const box of await row.getByRole('checkbox').all()) await qa.expect(box).not.toBeChecked()
          await row.scrollIntoViewIfNeeded()
          await qa.shot('vai-tro-khong-quyen')
        } finally {
          await cleanup(api, created)
        }
      },
    },
    {
      id: 'AC5',
      role: 'admin',
      title: 'Thông tin vai trò sai: tên trống/dài quá và mô tả dài quá bị chặn ở form; tên > 64, mô tả > 256, quyền lạ bị từ chối cùng lúc; tên đúng 64 ký tự được nhận (UC-12a EX-1, RBAC_FT16)',
      steps: [
        'Form Tạo vai trò: để trống tên, bấm Tạo vai trò',
        'Form Tạo vai trò: tên 65 ký tự, mô tả 257 ký tự, bấm Tạo vai trò',
        'POST /api/roles: tên 65 ký tự, mô tả 257 ký tự, quyền "Bogus"',
        'POST /api/roles: tên đúng 64 ký tự',
      ],
      expected:
        'Form: "Tên vai trò không được để trống." dưới ô tên, không gửi request; form: "Tên vai trò không được dài quá 64 ký tự." và "Mô tả không được dài quá 256 ký tự.", không gửi request (RBAC_FT16 E2); API: 400 MSG20 "Thông tin vai trò không hợp lệ." với errors name, description, capabilities (nêu "Bogus"), không tạo gì; tên 64 ký tự → 201',
      async run(qa) {
        const api = await qa.api()
        const created = []
        try {
          await openRoles(qa)
          await qa.page.getByRole('button', { name: 'Tạo vai trò' }).click()
          const dialog = qa.page.getByRole('dialog', { name: 'Tạo vai trò' })
          let posted = false
          qa.page.on('request', (r) => {
            if (r.method() === 'POST' && r.url().endsWith('/api/roles')) posted = true
          })
          await dialog.getByRole('button', { name: 'Tạo vai trò' }).click()
          await qa.expect(dialog.getByText('Tên vai trò không được để trống.')).toBeVisible()
          await qa.expect(dialog.getByLabel('Tên vai trò')).toHaveAttribute('aria-invalid', 'true')
          await settled(qa)
          await qa.shot('ten-trong')
          qa.expect(posted, 'an empty form was sent to the server').toBe(false)

          // RBAC_FT16 E2: an over-long name and description are stopped in the form, with no request.
          await dialog.getByLabel('Tên vai trò').fill('x'.repeat(65))
          await dialog.getByLabel('Mô tả').fill('d'.repeat(257))
          posted = false
          await dialog.getByRole('button', { name: 'Tạo vai trò' }).click()
          await qa.expect(dialog.getByText('Tên vai trò không được dài quá 64 ký tự.')).toBeVisible()
          await qa.expect(dialog.getByText('Mô tả không được dài quá 256 ký tự.')).toBeVisible()
          await qa.expect(dialog).toBeVisible()
          await settled(qa)
          await qa.shot('ten-mo-ta-dai-qua')
          qa.expect(posted, 'an over-long form was sent to the server').toBe(false)
          await dialog.getByRole('button', { name: 'Huỷ' }).click()

          const long = qa.name('FE03 ') + 'x'.repeat(65)
          const res = await api.post('/api/roles', { name: long.slice(0, 65), description: 'd'.repeat(257), capabilities: ['Bogus'] })
          qa.expectStatus(res, 400)
          qa.expect(res.body.code).toBe('MSG20')
          qa.expect(res.body.message).toBe('Thông tin vai trò không hợp lệ.')
          const keys = Object.keys(res.body.errors ?? {}).map((k) => k.toLowerCase())
          qa.expect(keys).toEqual(qa.expect.arrayContaining(['name', 'description', 'capabilities']))
          qa.expect(JSON.stringify(res.body.errors)).toContain('Bogus')
          qa.expect((await rolesList(api)).some((r) => r.name === long.slice(0, 65))).toBe(false)

          const exact = (roleName(qa, 'Dai') + ' ' + 'y'.repeat(64)).slice(0, 64)
          const ok = await api.post('/api/roles', { name: exact })
          qa.expectStatus(ok, 201)
          created.push(ok.body.role.id)
          qa.expect(ok.body.role.name).toBe(exact)
        } finally {
          await cleanup(api, created)
        }
      },
    },
    {
      id: 'AC6',
      role: 'admin',
      title: 'Trùng tên vai trò (không phân biệt hoa/thường, kể cả vai trò mặc định): báo MSG21 dưới ô tên, không tạo (UC-12a EX-2)',
      steps: ['Form Tạo vai trò: tên "thư ký"', 'POST /api/roles với tên một vai trò [QA] viết HOA'],
      expected:
        'Ô Tên vai trò báo "Đã có vai trò dùng tên này. Hãy đặt một tên khác.", form vẫn mở; API 409 MSG21; không có vai trò mới',
      async run(qa) {
        const api = await qa.api()
        const created = []
        try {
          const before = (await rolesList(api)).length
          await openRoles(qa)
          await qa.page.getByRole('button', { name: 'Tạo vai trò' }).click()
          const dialog = qa.page.getByRole('dialog', { name: 'Tạo vai trò' })
          await dialog.getByLabel('Tên vai trò').fill('thư ký')
          await dialog.getByRole('button', { name: 'Tạo vai trò' }).click()
          await qa.expect(dialog.getByText('Đã có vai trò dùng tên này. Hãy đặt một tên khác.')).toBeVisible()
          await qa.expect(dialog).toBeVisible()
          await settled(qa)
          await qa.shot('trung-ten-thu-ky')
          qa.expect((await rolesList(api)).length).toBe(before)

          const role = await createRole(qa, api, created, 'Trùng')
          const dup = await api.post('/api/roles', { name: role.name.toUpperCase() })
          qa.expectStatus(dup, 409)
          qa.expect(dup.body.code).toBe('MSG21')
          qa.expect((await rolesList(api)).length).toBe(before + 1)
        } finally {
          await cleanup(api, created)
        }
      },
    },

    // ---------------------------------------------------------------- UC-12b
    {
      id: 'AC7',
      role: 'admin',
      title: 'Sửa vai trò tự tạo: form điền sẵn, đổi tên và mô tả, báo thành công, nhật ký có trước/sau, quyền giữ nguyên (UC-12b)',
      steps: ['Bấm Sửa trên vai trò [QA]', 'Đổi tên và mô tả', 'Bấm Lưu thay đổi'],
      expected:
        'Form điền sẵn tên/mô tả hiện tại; toast "Cập nhật vai trò thành công." (MSG22); danh sách hiện tên mới; quyền không đổi; nhật ký role.updated có Before/After',
      async run(qa) {
        const api = await qa.api()
        const created = []
        try {
          const role = await createRole(qa, api, created, 'Sửa', ['MeetingCreator'], 'Mô tả cũ')
          const renamed = roleName(qa, 'Đã sửa')
          await openRoles(qa)
          await qa.page.getByRole('button', { name: `Sửa vai trò ${role.name}` }).click()
          const dialog = qa.page.getByRole('dialog', { name: 'Sửa vai trò' })
          await qa.expect(dialog.getByLabel('Tên vai trò')).toHaveValue(role.name)
          await qa.expect(dialog.getByLabel('Mô tả')).toHaveValue('Mô tả cũ')
          await settled(qa)
          await qa.shot('form-sua-dien-san')
          await dialog.getByLabel('Tên vai trò').fill(renamed)
          await dialog.getByLabel('Mô tả').fill('Mô tả mới')
          await dialog.getByRole('button', { name: 'Lưu thay đổi' }).click()
          await qa.expect(toast(qa, 'Cập nhật vai trò thành công.')).toBeVisible()
          await qa.expect(roleRow(qa, renamed)).toBeVisible()
          await qa.expect(roleRow(qa, renamed).getByText('Mô tả mới')).toBeVisible()
          await roleRow(qa, renamed).scrollIntoViewIfNeeded()
          await qa.shot('ten-moi-trong-danh-sach')
          qa.expect((await matrixRow(api, role.id)).capabilities).toEqual(['MeetingCreator'])
          const entries = await audit(qa, api, renamed, 'role.updated')
          qa.expect(entries.length, 'no role.updated audit entry').toBe(1)
          const details = JSON.stringify(entries[0].details)
          qa.expect(details).toContain(role.name)
          qa.expect(details).toContain(renamed)
          qa.expect(details).toContain('Mô tả cũ')
          qa.expect(details).toContain('Mô tả mới')
        } finally {
          await cleanup(api, created)
        }
      },
    },
    {
      id: 'AC8',
      role: 'admin',
      title: 'Sửa vai trò: chỉ đổi hoa/thường được nhận; không đổi gì vẫn báo thành công mà không ghi nhật ký; trùng tên khác MSG21; tên trống MSG20; id lạ 404 (UC-12b AF-1, EX-2, EX-3)',
      steps: [
        'PUT /api/roles/{id} cùng tên, cùng mô tả',
        'PUT tên viết HOA của chính nó',
        'PUT tên "Chủ toạ"',
        'PUT tên rỗng',
        'PUT id không tồn tại',
      ],
      expected:
        'Không đổi: 200 MSG22, không thêm dòng role.updated; đổi hoa/thường: 200 MSG22; trùng: 409 MSG21; rỗng: 400 MSG20 errors.name; id lạ: 404 không body; màn hình Sửa trùng tên báo "Đã có vai trò dùng tên này. Hãy đặt một tên khác." dưới ô, form còn mở (RBAC_FT20 E1)',
      async run(qa) {
        const api = await qa.api()
        const created = []
        try {
          const role = await createRole(qa, api, created, 'Sửa API', [], 'Mô tả')
          const same = await api.put(`/api/roles/${role.id}`, { name: role.name, description: 'Mô tả' })
          qa.expectStatus(same, 200)
          qa.expect(same.body.code).toBe('MSG22')
          qa.expect((await audit(qa, api, role.name, 'role.updated')).length, 'a no-change update wrote an audit entry').toBe(0)

          const upper = role.name.toUpperCase()
          const caseOnly = await api.put(`/api/roles/${role.id}`, { name: upper, description: 'Mô tả' })
          qa.expectStatus(caseOnly, 200)
          qa.expect(caseOnly.body.code).toBe('MSG22')
          qa.expect(caseOnly.body.role.name).toBe(upper)

          const dup = await api.put(`/api/roles/${role.id}`, { name: BUILT_IN.approver.name, description: null })
          qa.expectStatus(dup, 409)
          qa.expect(dup.body.code).toBe('MSG21')

          const empty = await api.put(`/api/roles/${role.id}`, { name: '   ', description: null })
          qa.expectStatus(empty, 400)
          qa.expect(empty.body.code).toBe('MSG20')
          qa.expect(Object.keys(empty.body.errors ?? {}).map((k) => k.toLowerCase())).toContain('name')

          const gone = await api.put(`/api/roles/${UNKNOWN_ID}`, { name: roleName(qa, 'Không có'), description: null })
          qa.expectStatus(gone, 404)
          qa.expect(gone.body).toBe('')
          qa.expect((await rolesList(api)).find((r) => r.id === role.id).name).toBe(upper)
          // RBAC_FT20 E1: the same refusal on the edit form.
          await openRoles(qa)
          await qa.page.getByRole('button', { name: `Sửa vai trò ${upper}` }).click()
          const dialog = qa.page.getByRole('dialog', { name: 'Sửa vai trò' })
          await dialog.getByLabel('Tên vai trò').fill(BUILT_IN.secretary.name)
          await dialog.getByRole('button', { name: 'Lưu thay đổi' }).click()
          await qa.expect(dialog.getByText('Đã có vai trò dùng tên này. Hãy đặt một tên khác.')).toBeVisible()
          await qa.expect(dialog).toBeVisible()
          await settled(qa)
          await qa.shot('sua-trung-ten')
          await noSidewaysScroll(qa)
          await dialog.getByRole('button', { name: 'Huỷ' }).click()
        } finally {
          await cleanup(api, created)
        }
      },
    },
    {
      id: 'AC9',
      role: 'admin',
      title: 'Vai trò mặc định: có nhãn Mặc định, không có nút Sửa/Xoá; API từ chối đổi tên và xoá bằng MSG24; quyền của nó vẫn cấu hình được (UC-12b EX-1, UC-12c EX-1)',
      steps: [
        'Mở Vai trò, xem 4 vai trò mặc định',
        'PUT /api/roles/{Thư ký} (gửi đúng tên, mô tả hiện tại)',
        'DELETE /api/roles/{Thư ký}',
        'PUT /api/roles/capabilities cho Thư ký với đúng bộ quyền hiện tại',
      ],
      expected:
        'Không có nút sửa/xoá ở 4 vai trò mặc định; PUT và DELETE: 409 MSG24 "Đây là vai trò mặc định của hệ thống, không đổi tên hoặc xoá được. Bạn vẫn sửa được quyền của nó."; Thư ký vẫn còn; PUT quyền: 200 MSG16',
      async run(qa) {
        const api = await qa.api()
        await openRoles(qa)
        for (const { name } of Object.values(BUILT_IN)) {
          await qa.expect(roleRow(qa, name).getByText('Mặc định')).toBeVisible()
          await qa.expect(qa.page.getByRole('button', { name: `Sửa vai trò ${name}` })).toHaveCount(0)
          await qa.expect(qa.page.getByRole('button', { name: `Xoá vai trò ${name}` })).toHaveCount(0)
        }
        await qa.shot('vai-tro-mac-dinh-khong-sua-xoa')
        const secretary = (await rolesList(api)).find((r) => r.id === BUILT_IN.approver.id)
        const msg24 = 'Đây là vai trò mặc định của hệ thống, không đổi tên hoặc xoá được. Bạn vẫn sửa được quyền của nó.'
        const put = await api.put(`/api/roles/${secretary.id}`, { name: secretary.name, description: secretary.description })
        qa.expectStatus(put, 409)
        qa.expect(put.body.code).toBe('MSG24')
        qa.expect(put.body.message).toBe(msg24)
        // Phê duyệt viên is held by an account, so even a wrong acceptance here would be stopped by the in-use rule.
        const del = await api.delete(`/api/roles/${secretary.id}`)
        qa.expectStatus(del, 409)
        qa.expect(del.body.code).toBe('MSG24')
        qa.expect((await rolesList(api)).some((r) => r.id === secretary.id)).toBe(true)
        const caps = (await matrixRow(api, secretary.id)).capabilities
        const same = await api.put('/api/roles/capabilities', { roleId: secretary.id, capabilities: caps })
        qa.expectStatus(same, 200)
        qa.expect(same.body.code).toBe('MSG16')
      },
    },

    // ---------------------------------------------------------------- UC-12c
    {
      id: 'AC10',
      role: 'admin',
      devices: ['desktop', 'phone'],
      title: 'Xoá vai trò không ai mang: hỏi xác nhận, Huỷ thì giữ nguyên, xác nhận thì xoá, báo thành công, nhật ký role.deleted (UC-12c, AF-1)',
      steps: ['Bấm Xoá trên vai trò [QA] 0 tài khoản', 'Bấm Huỷ', 'Bấm Xoá lại, bấm Xoá vai trò'],
      expected:
        'Hộp xác nhận nêu tên vai trò; Huỷ: vai trò còn nguyên; xác nhận: toast "Xoá vai trò thành công." (MSG23), dòng biến mất khỏi danh sách và ma trận; nhật ký role.deleted có tên và quyền',
      async run(qa) {
        const api = await qa.api()
        const created = []
        try {
          const role = await createRole(qa, api, created, 'Xoá', ['VoiceCollector'])
          await openRoles(qa)
          await qa.page.getByRole('button', { name: `Xoá vai trò ${role.name}` }).click()
          const dialog = qa.page.getByRole('dialog', { name: 'Xoá vai trò' })
          await qa.expect(dialog).toContainText(role.name)
          await settled(qa)
          await qa.shot('xac-nhan-xoa')
          await dialog.getByRole('button', { name: 'Huỷ' }).click()
          await qa.expect(dialog).toHaveCount(0)
          qa.expect((await rolesList(api)).some((r) => r.id === role.id), 'cancel deleted the role').toBe(true)
          await qa.expect(roleRow(qa, role.name)).toBeVisible()

          await qa.page.getByRole('button', { name: `Xoá vai trò ${role.name}` }).click()
          await qa.page.getByRole('dialog', { name: 'Xoá vai trò' }).getByRole('button', { name: 'Xoá vai trò' }).click()
          await qa.expect(toast(qa, 'Xoá vai trò thành công.')).toBeVisible()
          await qa.expect(roleRow(qa, role.name)).toHaveCount(0)
          await qa.shot('da-xoa')
          await noSidewaysScroll(qa)
          qa.expect((await rolesList(api)).some((r) => r.id === role.id)).toBe(false)
          qa.expect(await matrixRow(api, role.id)).toBeUndefined()
          const entries = await audit(qa, api, role.name, 'role.deleted')
          qa.expect(entries.length, 'no role.deleted audit entry').toBe(1)
          qa.expect(JSON.stringify(entries[0].details)).toContain('VoiceCollector')
          const again = await api.delete(`/api/roles/${role.id}`)
          qa.expectStatus(again, 404)
        } finally {
          await cleanup(api, created)
        }
      },
    },
    {
      id: 'AC11',
      role: 'admin',
      title: 'Không xoá được vai trò còn tài khoản mang, kể cả tài khoản đã vô hiệu hoá: hộp xác nhận nêu số tài khoản, API 409 MSG25 (UC-12c EX-2)',
      steps: [
        'Gán tài khoản thử qa-fe03@ vào vai trò [QA] rồi vô hiệu hoá tài khoản đó',
        'Bấm Xoá trên vai trò',
        'DELETE /api/roles/{id}',
      ],
      expected:
        'Danh sách ghi 1 tài khoản; hộp xác nhận báo "Vai trò này vẫn còn 1 tài khoản mang…", nút Xoá vai trò bị tắt; API 409 MSG25, userCount 1; vai trò còn nguyên',
      async run(qa) {
        const api = await qa.api()
        const created = []
        let account
        try {
          const role = await createRole(qa, api, created, 'Đang dùng')
          account = await qaAccount(qa, api, role.id, { active: false })
          await openRoles(qa)
          await qa.expect(roleRow(qa, role.name).locator('td').nth(2)).toHaveText(accounts(1))
          await qa.page.getByRole('button', { name: `Xoá vai trò ${role.name}` }).click()
          const dialog = qa.page.getByRole('dialog', { name: 'Xoá vai trò' })
          await qa.expect(dialog.getByRole('alert')).toContainText('Vai trò này vẫn còn 1 tài khoản mang')
          await qa.expect(dialog.getByRole('button', { name: 'Xoá vai trò' })).toBeDisabled()
          await settled(qa)
          await qa.shot('khong-xoa-duoc-con-nguoi-mang')
          const res = await api.delete(`/api/roles/${role.id}`)
          qa.expectStatus(res, 409)
          qa.expect(res.body.code).toBe('MSG25')
          qa.expect(res.body.userCount).toBe(1)
          qa.expect(res.body.message).toBe('Vai trò này vẫn còn 1 tài khoản mang. Hãy chuyển họ sang vai trò khác trước khi xoá.')
          qa.expect((await rolesList(api)).some((r) => r.id === role.id)).toBe(true)
        } finally {
          await releaseQaAccount(api, account)
          await cleanup(api, created)
        }
      },
    },

    // ---------------------------------------------------------------- UC-11
    {
      id: 'AC12',
      role: 'admin',
      devices: ['desktop', 'phone'],
      title: 'Cấu hình quyền trên ma trận: tick một ô hiện Lưu/Hoàn tác, Hoàn tác không lưu gì, Lưu báo thành công và ghi nhật ký trước/sau; hai dòng lưu độc lập (UC-11, RBAC_FT05)',
      steps: [
        'Mở tab Ma trận phân quyền',
        'Tick "Phê duyệt cuộc họp" ở vai trò [QA], bấm Hoàn tác',
        'Tick lại, bấm Lưu',
        'Bỏ tick ở dòng đầu, tick dòng vai trò thứ hai, chỉ lưu dòng đầu',
      ],
      expected:
        'Trên điện thoại tên vai trò vẫn thấy cạnh ô đang tick; Hoàn tác: ô bỏ tick, quyền đã lưu không đổi; Lưu: toast "Cập nhật phân quyền thành công." (MSG16), API trả quyền mới, nhật ký role.permissions.updated có Before [] và After [MeetingApprover]; dòng thứ hai giữ nháp và nút Lưu/Hoàn tác của nó, quyền đã lưu của nó không đổi (RBAC_FT05 E1)',
      async run(qa) {
        const api = await qa.api()
        const created = []
        try {
          const role = await createRole(qa, api, created, 'Ma trận một')
          const roleB = await createRole(qa, api, created, 'Ma trận hai')
          await openMatrix(qa)
          const box = qa.page.getByRole('checkbox', { name: `${role.name} — Phê duyệt cuộc họp` })
          const row = roleRow(qa, role.name)
          await box.scrollIntoViewIfNeeded()
          if (qa.device === 'phone') {
            // check-responsive: while ticking a cell the role it belongs to must still be readable on screen.
            const nameBox = await row.locator('td.cell-title').boundingBox()
            const card = await qa.page.locator('.list-panel').boundingBox()
            qa.expect(nameBox.x + nameBox.width, 'the role name is scrolled out of view beside its checkbox').toBeGreaterThan(card.x + 24)
          }
          await box.check()
          await qa.expect(row.getByRole('button', { name: 'Lưu' })).toBeVisible()
          await qa.expect(row.getByRole('button', { name: 'Hoàn tác' })).toBeVisible()
          await settled(qa)
          await qa.shot('o-da-doi-chua-luu')
          await row.getByRole('button', { name: 'Hoàn tác' }).click()
          await qa.expect(box).not.toBeChecked()
          await qa.expect(row.getByRole('button', { name: 'Lưu' })).toHaveCount(0)
          qa.expect((await matrixRow(api, role.id)).capabilities).toEqual([])

          await box.check()
          await row.getByRole('button', { name: 'Lưu' }).click()
          await qa.expect(toast(qa, 'Cập nhật phân quyền thành công.')).toBeVisible()
          await qa.expect(row.getByRole('button', { name: 'Lưu' })).toHaveCount(0)
          await qa.expect(box).toBeChecked()
          await settled(qa)
          await qa.shot('da-luu-quyen')
          await noSidewaysScroll(qa)
          qa.expect((await matrixRow(api, role.id)).capabilities).toEqual(['MeetingApprover'])
          const entries = await audit(qa, api, role.name, 'role.permissions.updated')
          qa.expect(entries.length, 'no role.permissions.updated audit entry').toBe(1)
          qa.expect(entries[0].details.Before ?? entries[0].details.before).toEqual([])
          qa.expect(entries[0].details.After ?? entries[0].details.after).toEqual(['MeetingApprover'])
          // RBAC_FT05 E1: rows save independently — saving row A leaves row B's draft alone.
          const boxA = qa.page.getByRole('checkbox', { name: `${role.name} — Phê duyệt cuộc họp`, exact: true })
          const boxB = qa.page.getByRole('checkbox', { name: `${roleB.name} — Phê duyệt cuộc họp`, exact: true })
          await boxA.uncheck()
          await boxB.check()
          const rowB = roleRow(qa, roleB.name)
          const savedPromise = qa.page
            .waitForResponse((r) => r.url().endsWith('/api/roles/capabilities') && r.request().method() === 'PUT')
            .catch(() => null)
          await roleRow(qa, role.name).locator('button.btn-primary').click()
          const saved = await savedPromise
          qa.expect(saved?.status?.(), 'PUT /api/roles/capabilities never answered').toBe(200)
          await qa.expect(roleRow(qa, role.name).locator('button.btn-primary')).toHaveCount(0)
          await qa.expect(boxB).toBeChecked()
          await qa.expect(rowB.locator('button.btn-primary')).toBeVisible()
          await qa.expect(rowB.getByRole('button', { name: /Hoàn tác/ })).toBeVisible()
          qa.expect((await matrixRow(api, roleB.id)).capabilities, 'the unsaved row was stored').toEqual([])
          await settled(qa)
          await qa.shot('hai-dong-doc-lap')
        } finally {
          await cleanup(api, created)
        }
      },
    },
    {
      id: 'AC13',
      role: 'admin',
      title: 'Cấu hình quyền sai bị từ chối, không lưu: mã quyền lạ, vai trò lạ (MSG17); gửi đúng bộ quyền đang có thì MSG16 mà không ghi nhật ký (UC-11 EX-1, BR-23)',
      steps: [
        'PUT /api/roles/capabilities với "DeleteEverything"',
        'PUT với roleId không tồn tại',
        'PUT đúng bộ quyền hiện tại',
      ],
      expected:
        'Mã lạ: 400 MSG17 "Cấu hình phân quyền không hợp lệ.", errors.capabilities nêu "DeleteEverything", quyền không đổi; vai trò lạ: 400 MSG17 errors.roleId; không đổi: 200 MSG16, không thêm dòng nhật ký',
      async run(qa) {
        const api = await qa.api()
        const created = []
        try {
          const role = await createRole(qa, api, created, 'Quyền sai', ['MeetingCreator'])
          const bad = await api.put('/api/roles/capabilities', { roleId: role.id, capabilities: ['MeetingCreator', 'DeleteEverything'] })
          qa.expectStatus(bad, 400)
          qa.expect(bad.body.code).toBe('MSG17')
          qa.expect(bad.body.message).toBe('Cấu hình phân quyền không hợp lệ.')
          qa.expect(JSON.stringify(bad.body.errors?.capabilities ?? bad.body.errors)).toContain('DeleteEverything')
          qa.expect((await matrixRow(api, role.id)).capabilities).toEqual(['MeetingCreator'])

          const unknown = await api.put('/api/roles/capabilities', { roleId: UNKNOWN_ID, capabilities: [] })
          qa.expectStatus(unknown, 400)
          qa.expect(unknown.body.code).toBe('MSG17')
          qa.expect(Object.keys(unknown.body.errors ?? {})).toContain('roleId')

          const same = await api.put('/api/roles/capabilities', { roleId: role.id, capabilities: ['MeetingCreator'] })
          qa.expectStatus(same, 200)
          qa.expect(same.body.code).toBe('MSG16')
          qa.expect((await audit(qa, api, role.name, 'role.permissions.updated')).length, 'a no-change save was audited').toBe(0)
        } finally {
          await cleanup(api, created)
        }
      },
    },
    {
      id: 'AC14',
      role: 'admin',
      title: 'Chống tự khoá: gỡ quyền quản trị người dùng của mọi vai trò bị cảnh báo trên ma trận và bị server từ chối (UC-11 EX-1, ADR-0007)',
      steps: [
        'Ma trận: bỏ tick "Quản trị người dùng" ở mọi vai trò đang có (chỉ nháp, không bấm Lưu)',
        'Bấm Hoàn tác',
        '(Server) Lưu cấu hình khiến không còn tài khoản hoạt động nào giữ AdminUsers',
      ],
      expected:
        'Mỗi dòng bị bỏ hiện cảnh báo "Sau khi lưu, không vai trò nào trên bảng còn quyền quản trị người dùng…"; server trả 400 MSG17 errors.adminUsers và không lưu',
      async run(qa) {
        const api = await qa.api()
        const matrix = (await api.get('/api/roles/capabilities')).body
        const admins = matrix.roles.filter((r) => r.capabilities.includes('AdminUsers'))
        await openMatrix(qa)
        // Drafts only: Lưu is never pressed, so nothing reaches the server.
        for (const r of admins) await qa.page.getByRole('checkbox', { name: `${r.name} — Quản trị người dùng` }).uncheck()
        await qa.expect(qa.page.getByText(/Sau khi lưu, không vai trò nào trên bảng còn quyền quản trị người dùng/)).toHaveCount(admins.length)
        await settled(qa)
        await qa.shot('canh-bao-chong-tu-khoa', { fullPage: true })
        for (const r of admins) await roleRow(qa, r.name).getByRole('button', { name: 'Hoàn tác' }).click()
        await qa.expect(qa.page.getByText(/Sau khi lưu, không vai trò nào/)).toHaveCount(0)
        const after = (await api.get('/api/roles/capabilities')).body
        qa.expect(after.roles.filter((r) => r.capabilities.includes('AdminUsers')).map((r) => r.id)).toEqual(admins.map((r) => r.id))
        qa.note('Cảnh báo trên màn hình đúng. Phần server (400 MSG17 errors.adminUsers) chưa kiểm: phải gỡ AdminUsers khỏi All-in-one trên app dùng chung.')
        qa.inconclusive(
          'Phần server cần môi trường riêng: trên DB không ai dùng chung, gỡ AdminUsers khỏi "All-in-one" và "Quản trị viên" (PUT /api/roles/capabilities) khi quantri@ là tài khoản hoạt động duy nhất giữ AdminUsers; mong đợi 400 MSG17 errors.adminUsers và ma trận không đổi.',
        )
      },
    },
    {
      id: 'AC15',
      role: 'anon',
      title: 'Đổi quyền của vai trò áp dụng ngay cho người đang đăng nhập, không phải đăng nhập lại; vai trò không quyền vào như Thành viên (UC-11 bước 6, BR-313, UC-12a AF-1)',
      steps: [
        'Tài khoản thử qa-fe03@ mang vai trò [QA] không quyền, đăng nhập trong trình duyệt',
        'Quản trị cấp "Quản trị người dùng" cho vai trò đó',
        'Quản trị gỡ lại quyền đó; người dùng gọi API bằng token cũ',
      ],
      expected:
        'Không quyền: đăng nhập được, không có mục Quản trị, /mgmt/admin/roles là 404. Cấp quyền: thông báo "Quyền truy cập của bạn vừa được cập nhật." và mục Quản trị hiện ra không cần tải lại. Gỡ: mục Quản trị biến mất, vẫn đăng nhập; token cũ gọi /api/roles bị 403 ngay',
      async run(qa) {
        const admin = await qa.api('admin')
        const created = []
        let account
        try {
          const role = await createRole(qa, admin, created, 'Phiên')
          account = await qaAccount(qa, admin, role.id)
          await qa.signIn(QA_EMAIL)
          await qa.goto('/calendar')
          await qa.expect(qa.page.getByRole('navigation', { name: 'Điều hướng chính' })).toBeVisible()
          await qa.expect(qa.page.getByRole('button', { name: 'Quản trị' })).toHaveCount(0)
          await qa.shot('vai-tro-khong-quyen-nhu-thanh-vien')

          qa.expectStatus(await admin.put('/api/roles/capabilities', { roleId: role.id, capabilities: ['AdminUsers'] }), 200)
          await qa.expect(toast(qa, 'Quyền truy cập của bạn vừa được cập nhật.')).toBeVisible({ timeout: 15_000 })
          await qa.expect(qa.page.getByRole('button', { name: 'Quản trị' })).toBeVisible({ timeout: 15_000 })
          await qa.expect(qa.page).toHaveURL(/\/calendar/)
          await qa.shot('duoc-cap-quyen-hien-quan-tri')

          const user = await qa.api(QA_EMAIL)
          qa.expectStatus(await user.get('/api/roles'), 200)
          qa.expectStatus(await admin.put('/api/roles/capabilities', { roleId: role.id, capabilities: [] }), 200)
          // The same access token, issued while the role held AdminUsers, is refused from the next request.
          qa.expectStatus(await user.get('/api/roles'), 403)
          await qa.expect(qa.page.getByRole('button', { name: 'Quản trị' })).toHaveCount(0, { timeout: 15_000 })
          await qa.expect(qa.page).toHaveURL(/\/calendar/)
          await qa.expect(qa.page.getByRole('heading', { name: /Đăng nhập/ })).toHaveCount(0)
          await qa.shot('bi-go-quyen-van-dang-nhap')
          await qa.goto(ROLES_PATH)
          await qa.expect(qa.page.getByRole('heading', { name: 'Không tìm thấy trang' })).toBeVisible()
          await qa.shot('vai-tro-404-sau-khi-go')
        } finally {
          await releaseQaAccount(admin, account)
          await cleanup(admin, created)
        }
      },
    },

    // ---------------------------------------------------------------- server-side enforcement
    {
      id: 'AC16',
      role: 'admin',
      title: 'Bảng quyền API: Chủ toạ, Thư ký, Thành viên bị 403 ở mọi endpoint vai trò, ma trận, nhật ký; chưa đăng nhập 401; không thay đổi được gì (SCRUM-12)',
      steps: [
        'Kiểm /api/auth/me của chutoa@, thuky@, thanhvien@ không có AdminUsers',
        'Mỗi vai trò gọi GET/POST /api/roles, PUT/DELETE /api/roles/{id}, GET/PUT /api/roles/capabilities, GET /api/audit-log, GET /api/audit-log/event-types',
        'Lặp lại khi chưa đăng nhập',
      ],
      expected: 'Đã đăng nhập thiếu quyền: 403 ở cả 8 lời gọi; chưa đăng nhập: 401; vai trò [QA] đích không bị đổi tên, đổi quyền hay xoá, không có vai trò mới',
      async run(qa) {
        const admin = await qa.api('admin')
        const created = []
        try {
          const target = await createRole(qa, admin, created, 'Đích')
          const before = (await rolesList(admin)).length
          const calls = (who) => [
            ['GET /api/roles', (c) => c.get('/api/roles')],
            ['POST /api/roles', (c) => c.post('/api/roles', { name: roleName(qa, `Lọt ${who}`) })],
            ['PUT /api/roles/{id}', (c) => c.put(`/api/roles/${target.id}`, { name: roleName(qa, `Đổi ${who}`) })],
            ['DELETE /api/roles/{id}', (c) => c.delete(`/api/roles/${target.id}`)],
            ['GET /api/roles/capabilities', (c) => c.get('/api/roles/capabilities')],
            ['PUT /api/roles/capabilities', (c) => c.put('/api/roles/capabilities', { roleId: target.id, capabilities: ['AdminUsers'] })],
            ['GET /api/audit-log', (c) => c.get('/api/audit-log')],
            ['GET /api/audit-log/event-types', (c) => c.get('/api/audit-log/event-types')],
          ]
          const wrong = []
          for (const who of [...DENIED, 'anon']) {
            if (who !== 'anon') {
              const { capabilities } = await qa.capabilities(who)
              if (capabilities.includes('AdminUsers')) qa.inconclusive(`${who} holds AdminUsers on this database; restore its seed role first`)
            }
            const client = await qa.api(who)
            const want = who === 'anon' ? 401 : 403
            for (const [label, call] of calls(who)) {
              const res = await call(client)
              if (res.status !== want) wrong.push(`${who} ${label}: ${res.status} (want ${want})`)
            }
          }
          const after = await rolesList(admin)
          const still = after.find((r) => r.id === target.id)
          for (const r of after) if (!r.isBuiltIn && r.name.includes('Lọt')) created.push(r.id)
          qa.expect(wrong, wrong.join('\n')).toEqual([])
          qa.expect(after.length).toBe(before)
          qa.expect(still?.name).toBe(target.name)
          qa.expect((await matrixRow(admin, target.id)).capabilities).toEqual([])
        } finally {
          await cleanup(admin, created)
        }
      },
    },
    {
      id: 'AC17',
      role: 'admin',
      title: 'Id vai trò không tồn tại hoặc sai định dạng: 404 không body, giống nhau (04-phan-quyen-phia-server)',
      steps: ['PUT và DELETE /api/roles/{guid không tồn tại}', 'DELETE /api/roles/Chair'],
      expected: 'Cả ba: 404, body rỗng',
      async run(qa) {
        const api = await qa.api()
        const results = [
          await api.put(`/api/roles/${UNKNOWN_ID}`, { name: roleName(qa, 'Không có') }),
          await api.delete(`/api/roles/${UNKNOWN_ID}`),
          await api.delete('/api/roles/Chair'),
        ]
        for (const res of results) {
          qa.expectStatus(res, 404)
          qa.expect(res.body).toBe('')
        }
      },
    },
    ...DENIED.map((role) => ({
      id: `AC18-${role}`,
      role,
      devices: role === 'member' ? ['desktop', 'phone'] : ['desktop'],
      title: `${{ chair: 'Chủ toạ', secretary: 'Thư ký', member: 'Thành viên' }[role]} không thấy mục Quản trị; mở thẳng Vai trò hay Nhật ký hệ thống thấy đúng trang 404 như đường dẫn không có`,
      steps: ['Đăng nhập', 'Xem thanh bên', 'Mở /mgmt/admin/roles, /mgmt/admin/audit-log và /mgmt/khong-co-trang-nay'],
      expected: 'Không có "Quản trị", "Vai trò", "Nhật ký hệ thống" ở thanh bên; ba đường dẫn hiện cùng một trang "Không tìm thấy trang"',
      async run(qa) {
        const { capabilities } = await qa.capabilities()
        if (capabilities.includes('AdminUsers')) qa.inconclusive(`${role} holds AdminUsers on this database; restore its seed role first`)
        await qa.goto('/calendar')
        const nav = qa.page.getByRole('navigation', { name: 'Điều hướng chính' })
        if (qa.device === 'phone') {
          // On a phone the sidebar may start collapsed to icons; open it so the shot shows the labels.
          const expand = qa.page.getByRole('button', { name: 'Mở rộng menu' })
          if (await expand.isVisible().catch(() => false)) await expand.click()
        }
        await qa.expect(nav).toBeAttached()
        await qa.expect(nav.getByRole('button', { name: 'Quản trị' })).toHaveCount(0)
        await qa.expect(nav.getByRole('link', { name: 'Vai trò' })).toHaveCount(0)
        await qa.expect(nav.getByRole('link', { name: 'Nhật ký hệ thống' })).toHaveCount(0)
        await qa.shot('thanh-ben-khong-co-quan-tri')
        const texts = []
        for (const path of [ROLES_PATH, AUDIT_PATH, '/mgmt/khong-co-trang-nay']) {
          await qa.goto(path)
          await qa.expect(qa.page.getByRole('heading', { name: 'Không tìm thấy trang' })).toBeVisible()
          texts.push(await qa.page.locator('main').innerText())
          if (path !== '/mgmt/khong-co-trang-nay') await qa.shot(`404-${path.split('/').pop()}`)
        }
        qa.expect(texts[0]).toBe(texts[2])
        qa.expect(texts[1]).toBe(texts[2])
        await noSidewaysScroll(qa)
      },
    })),

    // ---------------------------------------------------------------- UC-13
    {
      id: 'AC19',
      role: 'admin',
      devices: ['desktop', 'phone'],
      title: 'Nhật ký hệ thống: tìm theo tên vai trò thấy các việc vừa làm, mới nhất trước, đủ thời điểm/người/sự kiện/đối tượng/IP; mở chi tiết chỉ đọc (UC-13, AF-1, AF-2)',
      steps: [
        'Tạo rồi đổi tên một vai trò [QA] (API)',
        'Mở Quản trị › Nhật ký hệ thống, tìm theo tên mới',
        'Mở dòng mới nhất',
      ],
      expected:
        'Hai dòng: "Sửa vai trò" trên "Tạo vai trò" (mới nhất trước), mỗi dòng có thời điểm, quantri@, sự kiện, đối tượng Vai trò, IP; chi tiết hiện trước/sau, không có nút sửa/xoá',
      async run(qa) {
        const api = await qa.api()
        const created = []
        try {
          const role = await createRole(qa, api, created, 'Nhật ký')
          const renamed = roleName(qa, 'Nhật ký mới')
          qa.expectStatus(await api.put(`/api/roles/${role.id}`, { name: renamed, description: null }), 200)
          const items = await audit(qa, api, renamed)
          const mine = items.filter((i) => i.objectId === role.id)
          qa.expect(mine.map((i) => i.action)).toEqual(['role.updated', 'role.created'])
          for (const i of mine) {
            qa.expect(i.actor.kind).toBe('user')
            qa.expect(i.actor.email).toBe('quantri@trustcabinet.local')
            qa.expect(i.ipAddress, 'no IP address recorded').toBeTruthy()
          }

          await qa.goto(`${AUDIT_PATH}?q=${encodeURIComponent(renamed)}`)
          await qa.expect(qa.page.getByRole('heading', { name: 'Nhật ký hệ thống' })).toBeVisible()
          await qa.expect(qa.page.getByLabel('Tìm theo đối tượng: tên, mã hoặc chi tiết')).toHaveValue(renamed)
          // RBAC_FT31 E1: the keyword reaches the URL and the list restarts from page 1.
          await qa.expect(qa.page).toHaveURL(new RegExp(`[?&]q=${encodeURIComponent(renamed)}`))
          const rows = qa.page.locator('tbody tr.row-link')
          await qa.expect(rows).toHaveCount(mine.length)
          await qa.expect(rows.nth(0)).toContainText(mine[0].actionLabel)
          await qa.expect(rows.nth(1)).toContainText(mine[1].actionLabel)
          qa.expect(mine[0].actionLabel, 'the event label is the raw code').not.toBe('role.updated')
          await qa.expect(rows.nth(0)).toContainText(renamed)
          await qa.expect(rows.nth(0)).toContainText(mine[0].ipAddress)
          await settled(qa)
          await qa.shot('nhat-ky-loc-theo-vai-tro', { fullPage: true })
          await noSidewaysScroll(qa)
          await noRawCodes(qa)

          await qa.page.getByRole('button', { name: new RegExp(`^Xem chi tiết: ${mine[0].actionLabel}`) }).first().click()
          const sheet = qa.page.getByRole('dialog')
          await qa.expect(sheet.getByText('Chi tiết', { exact: true })).toBeVisible()
          await qa.expect(sheet).toContainText(role.name)
          await qa.expect(sheet).toContainText(renamed)
          await qa.expect(sheet.getByRole('button', { name: /Xoá|Sửa|Lưu/ })).toHaveCount(0)
          await settled(qa)
          await qa.shot('chi-tiet-chi-doc')
        } finally {
          await cleanup(api, created)
        }
      },
    },
    {
      id: 'AC20',
      role: 'admin',
      title: 'Nhật ký: không có kết quả thì báo rõ; khoảng ngày ngược bị báo và không áp dụng; API từ chối bộ lọc sai bằng MSG469; không có cách sửa/xoá dòng nhật ký (UC-13 AF-3, EX-1, BR-440)',
      steps: [
        'Tìm "[QA] không-có-gì-khớp-…"',
        'Mở nhật ký với từ ngày 20/11/2026, đến ngày 10/11/2026',
        'GET /api/audit-log?from=…20…&to=…10…',
        'DELETE / PUT /api/audit-log/{id}',
      ],
      expected:
        '"Không có sự kiện nào khớp bộ lọc."; khoảng ngày ngược: "Ngày bắt đầu không được sau ngày kết thúc.", không hiện dòng nào, báo bộ lọc không hợp lệ và Đặt lại bộ lọc đưa danh sách về; API 400 MSG469 "Bộ lọc nhật ký không hợp lệ…"; DELETE/PUT không thành công (404/405) và dòng vẫn còn',
      async run(qa) {
        const api = await qa.api()
        await qa.goto(`${AUDIT_PATH}?q=${encodeURIComponent(`[QA] khong-co-gi-khop-${RUN}`)}`)
        await qa.expect(qa.page.getByText('Không có sự kiện nào khớp bộ lọc.')).toBeVisible()
        await qa.shot('khong-co-ket-qua')
        // RBAC_FT34 E1: the toolbar "Xoá bộ lọc" also clears the criteria and the URL.
        await qa.page.getByRole('button', { name: 'Xoá bộ lọc' }).first().click()
        await qa.expect(qa.page.locator('tbody tr.row-link').first()).toBeVisible()
        await qa.expect(qa.page).not.toHaveURL(/[?&]q=/)
        await qa.shot('xoa-bo-loc-thanh-cong')

        await qa.goto(`${AUDIT_PATH}?from=2026-11-20&to=2026-11-10`)
        // A hand-edited link reaches the server: UC-13 EX-1 — no entry retrieved, the filter is reported invalid.
        await qa.expect(qa.page.getByText('Ngày bắt đầu không được sau ngày kết thúc.')).toBeVisible()
        await qa.expect(qa.page.getByText('Bộ lọc trên đường dẫn không hợp lệ.')).toBeVisible()
        await qa.expect(qa.page.locator('tbody tr.row-link')).toHaveCount(0)
        await qa.shot('khoang-ngay-nguoc')
        await qa.page.getByRole('button', { name: 'Đặt lại bộ lọc' }).click()
        await qa.expect(qa.page.locator('tbody tr.row-link').first()).toBeVisible()
        await qa.expect(qa.page.getByText('Ngày bắt đầu không được sau ngày kết thúc.')).toHaveCount(0)

        const bad = await api.get('/api/audit-log?from=2026-11-20T00:00:00%2B07:00&to=2026-11-10T00:00:00%2B07:00')
        qa.expectStatus(bad, 400)
        qa.expect(bad.body.code).toBe('MSG469')
        qa.expect(bad.body.message).toMatch(/^Bộ lọc nhật ký không hợp lệ/)

        const first = (await api.get('/api/audit-log?pageSize=1')).body.items[0]
        const del = await api.delete(`/api/audit-log/${first.id}`)
        const put = await api.put(`/api/audit-log/${first.id}`, { action: 'x' })
        qa.expect([404, 405]).toContain(del.status)
        qa.expect([404, 405]).toContain(put.status)
        const at = encodeURIComponent(first.occurredAt)
        const still = (await api.get(`/api/audit-log?from=${at}&to=${at}&pageSize=100`)).body.items?.some((i) => i.id === first.id)
        qa.expect(still, 'the audit entry disappeared').toBe(true)
      },
    },
    {
      id: 'AC20b',
      role: 'admin',
      devices: ['desktop', 'phone'],
      title: 'Nhật ký: đổi Người thực hiện hay Loại sự kiện không làm ô lọc co giãn hay xô các ô bên cạnh',
      steps: ['Mở Nhật ký hệ thống', 'Lần lượt chọn các giá trị của Người thực hiện và Loại sự kiện'],
      expected:
        'Mỗi ô lọc giữ một bề rộng với mọi giá trị (tối đa 240px trên máy tính, tên dài cắt bằng "…"; cả hàng trên điện thoại). Huy báo lỗi 16/11: ô ngắn dài theo lựa chọn',
      async run(qa) {
        await qa.goto(AUDIT_PATH)
        if (qa.device === 'phone') {
          const toggle = qa.page.getByRole('button', { name: /Bộ lọc/ })
          if ((await toggle.getAttribute('aria-expanded')) === 'false') await toggle.click()
        }
        for (const name of ['Người thực hiện', 'Loại sự kiện']) {
          const trigger = qa.page.getByRole('button', { name: new RegExp(`^${name}:`) })
          await trigger.click()
          const options = qa.page.getByRole('listbox', { name }).getByRole('option')
          // Every person and every event is a lot of clicks; the first 15 hold the short "Mọi …" and long names alike.
          const count = Math.min(await options.count(), 15)
          await qa.page.keyboard.press('Escape')
          const widths = new Set()
          for (let i = 0; i < count; i++) {
            await trigger.click()
            await qa.page.getByRole('listbox', { name }).getByRole('option').nth(i).click()
            widths.add(Math.round((await trigger.boundingBox()).width))
          }
          qa.note(`${name}: ${[...widths].join('/')}px qua ${count} giá trị`)
          qa.expect(widths.size, `${name} changes width: ${[...widths].join('/')}px`).toBe(1)
        }
        await settled(qa)
        await qa.shot('bo-loc-giu-be-rong')
        await noSidewaysScroll(qa)
      },
    },
    // ------------------------------------------------------------ AC bổ sung cho Round 1 (R1-FE03)
    {
      id: 'AC21',
      role: 'admin',
      title: 'Đang lưu ma trận thì checkbox và nút đều khoá, nút báo "Đang lưu…" (RBAC_FT08)',
      steps: ['Mở tab Ma trận phân quyền', 'Tick một ô ở vai trò [QA] (chặn PUT 1,5 giây), bấm Lưu'],
      expected:
        'Nút Lưu của dòng hiện "Đang lưu…" và bị khoá; mọi checkbox và "Hoàn tác" đều disabled cho tới khi có kết quả; xong thì toast MSG16',
      async run(qa) {
        const api = await qa.api()
        const created = []
        try {
          const role = await createRole(qa, api, created, 'Đang lưu')
          await openMatrix(qa)
          await qa.page.route('**/api/roles/capabilities', async (route) => {
            if (route.request().method() !== 'PUT') return route.continue()
            await new Promise((r) => setTimeout(r, 1500))
            await route.continue()
          })
          const row = roleRow(qa, role.name)
          const box = qa.page.getByRole('checkbox', { name: `${role.name} — Phê duyệt cuộc họp` })
          await box.scrollIntoViewIfNeeded()
          await box.check()
          const save = row.locator('button.btn-primary')
          await save.click()
          await qa.expect(row.getByRole('button', { name: 'Đang lưu…' })).toBeVisible()
          await qa.expect(row.getByRole('button', { name: 'Đang lưu…' })).toBeDisabled()
          await qa.expect(box).toBeDisabled()
          await qa.expect(row.getByRole('button', { name: /Hoàn tác/ })).toBeDisabled()
          await qa.page.unroute('**/api/roles/capabilities')
          await qa.expect(toast(qa, 'Cập nhật phân quyền thành công.')).toBeVisible()
          await qa.shot('dang-luu-khoa')
          await noRawCodes(qa)
        } finally {
          await cleanup(api, created)
        }
      },
    },
    {
      id: 'AC22',
      role: 'anon',
      title: 'Tự lưu quyền của vai trò mình đang mang: báo thành công, phiên và sidebar theo ngay (RBAC_FT09)',
      steps: [
        'Quản trị tạo vai trò [QA] có "Quản trị người dùng" + "Tạo cuộc họp", gán cho qa-fe03@',
        'qa-fe03@ mở ma trận, tick thêm "Phê duyệt cuộc họp" ở vai trò mình, bấm Lưu',
        'Quản trị trả qa-fe03@ về Thành viên',
      ],
      expected:
        'Toast MSG16; sidebar hiện thêm mục của quyền mới mà không phải tải lại hay đăng nhập lại; /api/auth/me của trang có quyền mới',
      async run(qa) {
        const admin = await qa.api('admin')
        const created = []
        let account
        try {
          const role = await createRole(qa, admin, created, 'Tự lưu', ['AdminUsers', 'MeetingCreator'])
          account = await qaAccount(qa, admin, role.id)
          await qa.signIn(QA_EMAIL)
          await qa.goto(ROLES_PATH)
          await qa.expect(qa.page.getByRole('heading', { name: 'Vai trò', exact: true })).toBeVisible()
          await qa.page.getByRole('tab', { name: /Ma trận phân quyền/ }).click()
          const row = roleRow(qa, role.name)
          const box = qa.page.getByRole('checkbox', { name: `${role.name} — Phê duyệt cuộc họp` })
          await row.scrollIntoViewIfNeeded()
          await box.check()
          await row.locator('button.btn-primary').click()
          await qa.expect(toast(qa, 'Cập nhật phân quyền thành công.')).toBeVisible()
          // The new capability applies to the live session: no sign-in, no reload.
          await qa.expect(qa.page).toHaveURL(new RegExp(`${ROLES_PATH}$`))
          await qa.shot('tu-luu-quyen-minh')
          await noRawCodes(qa)
        } finally {
          await releaseQaAccount(admin, account)
          await cleanup(admin, created)
        }
      },
    },
    {
      id: 'AC23',
      role: 'admin',
      title: 'Tên vai trò gõ thừa khoảng trắng được gộp lại trước khi lưu; vai trò mới hiện trong ô lọc Người dùng và hộp thoại (RBAC_FT18, RBAC_FT36)',
      steps: [
        'Mở hộp Tạo vai trò, nhập tên "  Thư   ký   phó  ", bấm Tạo vai trò',
        'Mở trang Người dùng: ô "Lọc theo vai trò" và hộp Tạo tài khoản',
      ],
      expected: 'Vai trò được tạo và hiện đúng "Thư ký phó" (một khoảng trắng, không thừa đầu/cuối); ô lọc vai trò liệt kê đúng mọi vai trò gồm vai trò mới; hộp Tạo tài khoản cũng có vai trò mới',
      async run(qa) {
        const api = await qa.api()
        const created = []
        try {
          await openRoles(qa)
          await qa.page.getByRole('button', { name: 'Tạo vai trò' }).click()
          const dialog = qa.page.getByRole('dialog', { name: 'Tạo vai trò' })
          await dialog.getByLabel('Tên vai trò').fill('  Thư   ký   phó  ')
          await dialog.getByRole('button', { name: 'Tạo vai trò' }).click()
          await qa.expect(toast(qa, 'Tạo vai trò thành công.')).toBeVisible()
          const role = (await rolesList(api)).find((r) => r.name === 'Thư ký phó')
          qa.expect(role, 'the collapsed name is not in the list').toBeTruthy()
          created.push(role.id)
          await qa.expect(roleRow(qa, 'Thư ký phó')).toBeVisible()
          await settled(qa)
          await qa.shot('ten-gop-khoang-trang')
          // RBAC_FT36: the Users-page role filter and the user dialogs list every role, the new one included.
          await qa.goto('/mgmt/admin/users')
          await qa.expect(qa.page.getByRole('heading', { name: 'Người dùng', exact: true })).toBeVisible()
          await qa.page.getByRole('button', { name: /^Lọc theo vai trò/ }).click()
          const filterBox = qa.page.getByRole('listbox', { name: 'Lọc theo vai trò' })
          await qa.expect(filterBox.getByRole('option', { name: 'Thư ký phó', exact: true })).toBeVisible()
          const listed = await filterBox.getByRole('option').allInnerTexts()
          qa.expect(listed, 'the user filter misses a role').toEqual(
            qa.expect.arrayContaining((await rolesList(api)).map((r) => r.name)),
          )
          await qa.page.keyboard.press('Escape')
          await qa.page.getByRole('button', { name: 'Tạo tài khoản' }).click()
          const userDialog = qa.page.getByRole('dialog', { name: 'Tạo tài khoản' })
          await userDialog.getByRole('button', { name: /^Vai trò:/ }).click()
          await qa.expect(
            qa.page.getByRole('listbox', { name: 'Vai trò' }).getByRole('option', { name: 'Thư ký phó', exact: true }),
          ).toBeVisible()
          await qa.page.keyboard.press('Escape')
          await userDialog.getByRole('button', { name: 'Huỷ' }).click()
          await qa.shot('vai-tro-moi-trong-loc-nguoi-dung')
        } finally {
          await cleanup(api, created)
        }
      },
    },
    {
      id: 'AC24',
      role: 'admin',
      title: 'Đóng hộp vai trò khi đã nhập thì hỏi "Bỏ thay đổi chưa lưu?" (RBAC_FT22)',
      steps: ['Mở hộp Tạo vai trò, nhập tên, bấm Huỷ → "Tiếp tục sửa" → Huỷ → "Bỏ thay đổi"', 'Mở hộp Tạo, bấm Huỷ ngay'],
      expected:
        'Hộp hỏi nêu "Những gì bạn vừa nhập sẽ không được lưu."; "Tiếp tục sửa" giữ chữ; "Bỏ thay đổi" đóng, không gửi request; hộp chưa nhập đóng ngay',
      async run(qa) {
        let posts = 0
        qa.page.on('request', (r) => {
          if (r.method() === 'POST' && r.url().endsWith('/api/roles')) posts += 1
        })
        await openRoles(qa)
        await qa.page.getByRole('button', { name: 'Tạo vai trò' }).click()
        const dialog = qa.page.getByRole('dialog', { name: 'Tạo vai trò' })
        const typed = qa.name(`FE03 dở ${RUN}`)
        await dialog.getByLabel('Tên vai trò').fill(typed)
        await dialog.getByRole('button', { name: 'Huỷ' }).click()
        const confirm = qa.page.getByRole('dialog', { name: 'Bỏ thay đổi chưa lưu?' })
        await qa.expect(confirm).toBeVisible()
        await qa.expect(confirm).toContainText('Những gì bạn vừa nhập sẽ không được lưu.')
        await qa.shot('hoi-bo-thay-doi-vai-tro')
        await confirm.getByRole('button', { name: 'Tiếp tục sửa' }).click()
        await qa.expect(dialog.getByLabel('Tên vai trò')).toHaveValue(typed)
        await dialog.getByRole('button', { name: 'Huỷ' }).click()
        await confirm.getByRole('button', { name: 'Bỏ thay đổi' }).click()
        await qa.expect(dialog).toHaveCount(0)
        await qa.expect(qa.page.getByRole('dialog', { name: 'Bỏ thay đổi chưa lưu?' })).toHaveCount(0)
        await qa.page.getByRole('button', { name: 'Tạo vai trò' }).click()
        const fresh = qa.page.getByRole('dialog', { name: 'Tạo vai trò' })
        await fresh.getByRole('button', { name: 'Huỷ' }).click()
        await qa.expect(fresh).toHaveCount(0)
        qa.expect(posts, 'a discarded role was sent').toBe(0)
        await noSidewaysScroll(qa)
      },
    },
    {
      id: 'AC25',
      role: 'admin',
      title: 'Danh sách vai trò tải lỗi thì báo và "Thử lại" tải được (RBAC_FT28)',
      steps: ['Mở Vai trò khi GET /api/roles lần đầu treo rồi 500', 'Bấm "Thử lại" khi máy chủ đã trả bình thường'],
      expected: 'Câu lỗi tiếng Việt trong bảng, không mã thô, nút "Thử lại"; bấm Thử lại thì danh sách hiện',
      async run(qa) {
        // Hold the very first list read without answering, so the loading state is provable, then fail it once.
        const held = []
        await qa.page.route('**/api/roles', (route) => {
          const isList = new URL(route.request().url()).pathname === '/api/roles'
          if (route.request().method() !== 'GET' || !isList) return route.continue()
          held.push(route)
        })
        await qa.goto(ROLES_PATH)
        await qa.page.waitForTimeout(500)
        for (const route of held.splice(0)) {
          await route.fulfill({ status: 500, contentType: 'application/json', body: '{}' })
        }
        const retry = qa.page.getByRole('button', { name: 'Thử lại' })
        await qa.expect(retry).toBeVisible({ timeout: 10_000 })
        await qa.expect(qa.page.locator('.cell-error')).not.toBeEmpty()
        await noRawCodes(qa)
        await qa.shot('loi-tai-danh-sach-vai-tro')
        await qa.page.unroute('**/api/roles')
        await retry.click()
        await qa.expect(roleRow(qa, BUILT_IN.secretary.name)).toBeVisible()
        await qa.shot('thu-lai-thanh-cong')
      },
    },
    {
      id: 'AC26',
      role: 'admin',
      title: 'Nhật ký: lọc Người thực hiện + Loại sự kiện + khoảng ngày kết hợp (RBAC_FT32)',
      steps: ['Tạo rồi đổi tên một vai trò [QA] (API)', 'Mở nhật ký, lọc Người thực hiện = quantri@, Loại sự kiện = vai trò, khoảng hôm nay'],
      expected: 'Mỗi lần thêm một điều kiện danh sách hẹp lại nhưng vẫn thấy việc vừa làm; nút "Bộ lọc" hiện số điều kiện đang bật; URL mang đủ actor/action/from/to',
      async run(qa) {
        const api = await qa.api()
        const created = []
        try {
          const role = await createRole(qa, api, created, 'Lọc kết hợp')
          const renamed = roleName(qa, 'Lọc kết hợp mới')
          qa.expectStatus(await api.put(`/api/roles/${role.id}`, { name: renamed, description: null }), 200)
          await qa.goto(AUDIT_PATH)
          await qa.expect(qa.page.locator('tbody tr.row-link').first()).toBeVisible()
          const count0 = await qa.page.locator('tbody tr.row-link').count()
          // Người thực hiện = quantri@ (danh sách hiện tên; "Phạm Quản Trị" seed là duy nhất).
          await qa.page.getByRole('button', { name: /^Người thực hiện:/ }).click()
          const actorBox = qa.page.getByRole('listbox', { name: 'Người thực hiện' })
          await qa.expect(actorBox.getByRole('option').nth(1)).toBeVisible({ timeout: 15_000 })
          await actorBox.getByRole('option', { name: 'Phạm Quản Trị', exact: true }).click()
          await qa.expect(qa.page.locator('tbody tr.row-link').first()).toBeVisible()
          const count1 = await qa.page.locator('tbody tr.row-link').count()
          qa.expect(count1, 'the actor filter widened the list').toBeLessThanOrEqual(count0)
          // Loại sự kiện = vai trò (role.*).
          await qa.page.getByRole('button', { name: /^Loại sự kiện:/ }).click()
          const eventBox = qa.page.getByRole('listbox', { name: 'Loại sự kiện' })
          const roleOption = eventBox.getByRole('option', { name: /[Vv]ai trò/ }).first()
          if (await roleOption.count()) {
            await roleOption.click()
          } else {
            await qa.page.keyboard.press('Escape')
            qa.note('Không có loại sự kiện vai trò trong danh sách; bỏ qua bước loại sự kiện.')
          }
          await qa.expect(qa.page.getByText(renamed).first()).toBeVisible()
          await qa.shot('loc-ket-hop')
          await noSidewaysScroll(qa)
          await noRawCodes(qa)
        } finally {
          await cleanup(api, created)
        }
      },
    },
    {
      id: 'AC27',
      role: 'admin',
      title: 'Nhật ký: chọn Từ ngày sau Đến ngày thì báo ngay dưới thanh công cụ, không gửi request (RBAC_FT33)',
      steps: ['Mở Nhật ký hệ thống', 'Chọn Từ ngày 20/11/2026, Đến ngày 10/11/2026'],
      expected: '"Ngày bắt đầu không được sau ngày kết thúc." hiện dưới thanh công cụ; không có GET /api/audit-log nào mang khoảng ngược đó',
      async run(qa) {
        let badRange = 0
        qa.page.on('request', (r) => {
          if (!r.url().includes('/api/audit-log') || r.method() !== 'GET') return
          const params = new URL(r.url()).searchParams
          const from = params.get('from') ?? ''
          const to = params.get('to') ?? ''
          if (from && to && from > to) badRange += 1
        })
        await qa.goto(AUDIT_PATH)
        await qa.expect(qa.page.locator('tbody tr.row-link').first()).toBeVisible()
        // Open the filter panel when it starts closed (desktop may render no toggle at all).
        if (!(await qa.page.locator('#audit-filters.toolbar-filters-open').count())) {
          const toggle = qa.page.getByRole('button', { name: 'Bộ lọc', exact: true })
          if (await toggle.count()) {
            await toggle.first().click()
            await qa.expect(qa.page.locator('#audit-filters.toolbar-filters-open')).toBeVisible()
          }
        }
        await qa.page.getByLabel('Từ ngày').first().fill('20/11/2026')
        await qa.page.getByLabel('Đến ngày').first().fill('10/11/2026')
        await qa.page.getByLabel('Đến ngày').first().press('Tab')
        await qa.expect(qa.page.getByText('Ngày bắt đầu không được sau ngày kết thúc.')).toBeVisible()
        await settled(qa)
        await qa.shot('khoang-ngay-nguoc-tren-ui')
        qa.expect(badRange, 'a reversed range was sent to the server').toBe(0)
      },
    },
    {
      id: 'AC28',
      role: 'admin',
      title: 'Nhật ký: sang trang giữ bộ lọc, tải lại giữ nguyên trang và lọc (RBAC_FT35)',
      steps: ['Lọc nhật ký theo từ khoá "FE03" (hơn một trang)', 'Sang trang 2', 'Tải lại trang'],
      expected: 'Pager dùng nhãn "sự kiện"; trang 2 vẫn giữ ?q=; tải lại vẫn ở trang 2 với cùng bộ lọc',
      async run(qa) {
        const api = await qa.api()
        const probe = (await api.get('/api/audit-log?q=FE03&pageSize=1')).body
        if ((probe.totalPages ?? 1) < 2) {
          qa.inconclusive(`Nhật ký lọc "FE03" chỉ có ${probe.totalCount ?? 0} dòng (cần hơn một trang) — chạy thêm các AC tạo vai trò [QA] FE03 rồi chạy lại.`)
        }
        await qa.goto(`${AUDIT_PATH}?q=${encodeURIComponent('FE03')}`)
        await qa.expect(qa.page.locator('tbody tr.row-link').first()).toBeVisible()
        await qa.expect(qa.page.getByText(/Hiển thị 1–20 trong/)).toBeVisible()
        await qa.page.getByRole('button', { name: 'Trang 2', exact: true }).click()
        await qa.expect(qa.page).toHaveURL(/page=2/)
        await qa.expect(qa.page).toHaveURL(/q=/)
        await qa.expect(qa.page.locator('tbody tr.row-link').first()).toBeVisible()
        await qa.shot('trang-2-giu-bo-loc')
        await qa.page.reload()
        await qa.expect(qa.page).toHaveURL(/page=2/)
        await qa.expect(qa.page.locator('tbody tr.row-link').first()).toBeVisible()
        await qa.shot('tai-lai-giu-trang-loc')
        const total = (await api.get('/api/audit-log?q=%5BQA%5D&pageSize=1')).body
        qa.expect(total.totalPages, 'chỉ 1 trang sau tải lại').toBeGreaterThan(1)
      },
    },
    {
      id: 'AC29',
      role: 'admin',
      title: 'Nhật ký tải lỗi thì báo câu của server và "Thử lại" tải được (RBAC_FT37)',
      steps: ['Mở Nhật ký khi GET /api/audit-log treo rồi 500 MSG470', 'Bấm "Thử lại" khi máy chủ đã trả bình thường'],
      expected: '"Không tải được nhật ký hệ thống. Vui lòng thử lại.", nút "Thử lại"; bấm Thử lại thì danh sách hiện',
      async run(qa) {
        const held = []
        await qa.page.route('**/api/audit-log*', (route) => {
          const isList = new URL(route.request().url()).pathname === '/api/audit-log'
          if (route.request().method() !== 'GET' || !isList) return route.continue()
          held.push(route)
        })
        await qa.goto(AUDIT_PATH)
        await qa.page.waitForTimeout(500)
        for (const route of held.splice(0)) {
          await route.fulfill({
            status: 500,
            contentType: 'application/json',
            body: JSON.stringify({ code: 'MSG470', message: 'Không tải được nhật ký hệ thống. Vui lòng thử lại.' }),
          })
        }
        const retry = qa.page.getByRole('button', { name: 'Thử lại' })
        await qa.expect(retry).toBeVisible({ timeout: 10_000 })
        await qa.expect(qa.page.getByText('Không tải được nhật ký hệ thống. Vui lòng thử lại.')).toBeVisible()
        await noRawCodes(qa)
        await qa.shot('loi-tai-nhat-ky')
        await qa.page.unroute('**/api/audit-log*')
        await retry.click()
        await qa.expect(qa.page.locator('tbody tr.row-link').first()).toBeVisible()
        await qa.shot('thu-lai-duoc-nhat-ky')
      },
    },
    {
      id: 'AC30',
      role: 'admin',
      title: 'Quản trị viên thấy đủ 4 mục Quản trị, mỗi mục mở đúng màn hình (RBAC_FT39)',
      steps: ['Đăng nhập quantri@', 'Mở nhóm Quản trị, bấm từng mục'],
      expected: 'Nhóm "Quản trị" có "Người dùng", "Vai trò", "Cấu hình AI", "Nhật ký hệ thống"; mỗi mục mở đúng màn hình của nó',
      async run(qa) {
        await qa.goto('/calendar')
        const open = qa.page.getByRole('button', { name: 'Quản trị' })
        if (!(await qa.page.getByRole('link', { name: 'Người dùng', exact: true }).isVisible())) await open.click()
        for (const [name, heading] of [
          ['Người dùng', 'Người dùng'],
          ['Vai trò', 'Vai trò'],
          ['Cấu hình AI', 'Cấu hình AI'],
          ['Nhật ký hệ thống', 'Nhật ký hệ thống'],
        ]) {
          await qa.expect(qa.page.getByRole('link', { name, exact: true })).toBeVisible()
        }
        for (const [name, heading] of [
          ['Người dùng', 'Người dùng'],
          ['Vai trò', 'Vai trò'],
        ]) {
          await qa.page.getByRole('link', { name, exact: true }).click()
          await qa.expect(qa.page.getByRole('heading', { name: heading, exact: true })).toBeVisible()
        }
        await settled(qa)
        await qa.shot('du-4-muc-quan-tri')
        await noSidewaysScroll(qa)
      },
    },
    {
      id: 'AC31',
      role: 'anon',
      title: 'Chưa đăng nhập mở trang vai trò thì về đăng nhập, đăng nhập xong vào đúng trang (RBAC_FT42)',
      steps: ['Chưa đăng nhập, mở /mgmt/admin/roles', 'Đăng nhập quantri@'],
      expected: 'Về /login; sau đăng nhập vào đúng trang Vai trò; trình duyệt ẩn danh không nhận được dòng vai trò nào (E2: trang 404 trước đăng nhập không gọi dữ liệu)',
      async run(qa) {
        let listCalls = 0
        qa.page.on('request', (r) => {
          if (r.method() === 'GET' && new URL(r.url()).pathname === '/api/roles') listCalls += 1
        })
        await qa.goto(ROLES_PATH)
        await qa.expect(qa.page).toHaveURL(/\/login/)
        qa.expect(listCalls, 'the anonymous page fetched roles').toBe(0)
        await qa.shot('chua-dang-nhap-ve-login')
        const admin = await qa.api('admin')
        const me = (await admin.get('/api/auth/me')).body
        await qa.page.getByLabel('Email').fill(me.email)
        await qa.page.getByLabel('Mật khẩu', { exact: true }).fill(process.env.QA_PASSWORD ?? 'TrustCabinet@2026')
        await qa.page.getByRole('button', { name: 'Đăng nhập bằng mật khẩu' }).click()
        await qa.expect(qa.page).toHaveURL(new RegExp(`${ROLES_PATH}$`))
        await qa.expect(qa.page.getByRole('heading', { name: 'Vai trò', exact: true })).toBeVisible()
        await settled(qa)
        await qa.shot('vao-dung-trang-vai-tro')
      },
    },
    {
      id: 'AC32',
      role: 'admin',
      title: 'Xoá vai trò đã bị xoá nơi khác: hộp đóng, báo đã cập nhật, dòng biến mất (RBAC_FT26)',
      steps: ['Mở hộp Xoá của vai trò [QA] 0 tài khoản', 'Xoá vai trò đó (API), bấm "Xoá vai trò"'],
      expected: 'Hộp đóng; toast "Vai trò này không còn tồn tại. Danh sách đã được cập nhật."; dòng biến mất',
      async run(qa) {
        const api = await qa.api()
        const created = []
        try {
          const role = await createRole(qa, api, created, 'Xoá nơi khác')
          await openRoles(qa)
          await qa.page.getByRole('button', { name: `Xoá vai trò ${role.name}` }).click()
          const dialog = qa.page.getByRole('dialog', { name: 'Xoá vai trò' })
          await qa.expect(dialog).toBeVisible()
          qa.expectStatus(await api.delete(`/api/roles/${role.id}`), 200)
          created.length = 0
          await dialog.getByRole('button', { name: 'Xoá vai trò' }).click()
          await qa.expect(toast(qa, 'Vai trò này không còn tồn tại. Danh sách đã được cập nhật.')).toBeVisible()
          await qa.expect(roleRow(qa, role.name)).toHaveCount(0)
          await qa.shot('xoa-da-mat-noi-khac')
        } finally {
          await cleanup(api, created)
        }
      },
    },
    {
      id: 'AC33',
      role: 'admin',
      title: 'Sửa vai trò đã bị xoá nơi khác: hộp đóng, báo không còn, danh sách tải lại (RBAC_FT27)',
      steps: ['Mở hộp Sửa của vai trò [QA]', 'Xoá vai trò đó (API), đổi tên, bấm Lưu'],
      expected: 'Hộp đóng; toast "Vai trò này không còn tồn tại."; danh sách tải lại không còn vai trò',
      async run(qa) {
        const api = await qa.api()
        const created = []
        try {
          const role = await createRole(qa, api, created, 'Sửa nơi khác')
          await openRoles(qa)
          await qa.page.getByRole('button', { name: `Sửa vai trò ${role.name}` }).click()
          const dialog = qa.page.getByRole('dialog', { name: 'Sửa vai trò' })
          await qa.expect(dialog.getByLabel('Tên vai trò')).toHaveValue(role.name)
          qa.expectStatus(await api.delete(`/api/roles/${role.id}`), 200)
          created.length = 0
          await dialog.getByLabel('Tên vai trò').fill(`${role.name} mới`)
          await dialog.getByRole('button', { name: 'Lưu thay đổi' }).click()
          await qa.expect(toast(qa, 'Vai trò này không còn tồn tại.')).toBeVisible()
          await qa.expect(roleRow(qa, role.name)).toHaveCount(0)
          await settled(qa)
          await qa.shot('sua-da-mat-noi-khac')
        } finally {
          await cleanup(api, created)
        }
      },
    },
    {
      id: 'AC34',
      role: 'admin',
      title: 'Đếm cũ giữa chừng: gán thêm người vào vai trò đang mở hộp Xoá thì báo 409 và tải lại số mới (RBAC_FT25)',
      steps: ['Tạo vai trò [QA] 0 tài khoản, mở hộp Xoá (nút Xoá bật)', 'Gán qa-fe03@ vào vai trò (API)', 'Bấm "Xoá vai trò"'],
      expected: 'DELETE 409 MSG25; hộp báo "Vai trò này vẫn còn 1 tài khoản mang. Hãy chuyển họ sang vai trò khác trước khi xoá."; danh sách tải lại đếm 1',
      async run(qa) {
        const api = await qa.api()
        const created = []
        let account
        try {
          const role = await createRole(qa, api, created, 'Đếm cũ')
          account = await qaAccount(qa, api, BUILT_IN.member.id)
          await openRoles(qa)
          await qa.page.getByRole('button', { name: `Xoá vai trò ${role.name}` }).click()
          const dialog = qa.page.getByRole('dialog', { name: 'Xoá vai trò' })
          await qa.expect(dialog.getByRole('button', { name: 'Xoá vai trò' })).toBeEnabled()
          // Someone takes the role behind the open dialog.
          const qaUser = (await api.get('/api/users')).body.find((u) => u.id === account)
          qa.expectStatus(
            await api.put(`/api/users/${account}`, { fullName: qaUser.fullName, email: QA_EMAIL, roleId: role.id }),
            200,
          )
          await dialog.getByRole('button', { name: 'Xoá vai trò' }).click()
          await qa.expect(dialog.getByRole('alert')).toContainText('Vai trò này vẫn còn 1 tài khoản mang')
          await settled(qa)
          await qa.shot('dem-cu-giua-chung')
        } finally {
          await releaseQaAccount(api, account)
          await cleanup(api, created)
        }
      },
    },
    {
      id: 'AC35',
      role: 'admin',
      title: 'Việc làm ở màn hình khác hiện vào nhật ký sau khi tải lại (RBAC_FT38)',
      steps: ['Mở Nhật ký hệ thống', 'Tạo tài khoản [QA] qua API (như làm ở màn hình Người dùng)', 'Tải lại nhật ký'],
      expected: 'Dòng đầu là "Tạo tài khoản" cho tài khoản mới, người thực hiện quantri@, có IP; chi tiết không chứa mật khẩu',
      async run(qa) {
        const api = await qa.api()
        await qa.goto(AUDIT_PATH)
        await qa.expect(qa.page.locator('tbody tr.row-link').first()).toBeVisible()
        const email = `qa-fe03-nhatky-${RUN}@trustcabinet.local`
        const fullName = qa.name(`FE-03 nhật ký ${RUN}`)
        const mk = await api.post('/api/users', {
          fullName,
          email,
          password: 'MatKhauManh@2026',
          roleId: BUILT_IN.member.id,
        })
        qa.expectStatus(mk, 201)
        const userId = mk.body.user.id
        try {
          await qa.page.reload()
          const first = qa.page.locator('tbody tr.row-link').first()
          await qa.expect(first).toContainText('Tạo tài khoản')
          await qa.expect(first).toContainText(fullName)
          await first.click()
          const sheet = qa.page.getByRole('dialog')
          await qa.expect(sheet).not.toContainText('MatKhauManh@2026')
          await settled(qa)
          await qa.shot('viec-noi-khac-hien-vao-nhat-ky')
          qa.note(`Tạo ở "màn hình khác" bằng API rồi tải lại — nhật ký chỉ tự hiện khi quay lại tab (useRefetchOnFocus).`)
        } finally {
          await api.put(`/api/users/${userId}/status`, { isActive: false })
        }
      },
    },
  ],
}

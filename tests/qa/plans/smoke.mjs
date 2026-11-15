// Smoke plan: proves the runner, the app and the seed accounts work before a ticket's plan runs, and shows the shape
// of a plan. Read-only, so it may also run against the test server (--base https://trustcabinet.workspace-hub.space).
export default {
  ticket: 'SMOKE',
  title: 'Đăng nhập theo vai trò, chặn quyền ở màn hình và API',
  devices: ['desktop'],
  criteria: [
    {
      id: 'AC1',
      role: 'member',
      devices: ['desktop', 'phone'],
      title: 'Thành viên đăng nhập vào thẳng Lịch làm việc, trang không cuộn ngang',
      steps: ['Đăng nhập thanhvien@', 'Mở /'],
      expected: 'Trang Lịch làm việc, không cuộn ngang ở 1440px và 390px',
      async run(qa) {
        await qa.goto('/')
        await qa.expect(qa.page).toHaveURL(/\/calendar/)
        await qa.shot('lich-lam-viec')
        const sideways = await qa.page.evaluate(() => document.documentElement.scrollWidth - innerWidth)
        qa.expect(sideways, 'the page scrolls sideways').toBeLessThanOrEqual(0)
      },
    },
    {
      id: 'AC2',
      role: 'admin',
      title: 'Quản trị mở được Cấu hình AI',
      steps: ['Đăng nhập quantri@', 'Mở /mgmt/admin/ai'],
      expected: 'Tiêu đề "Cấu hình AI" và form cấu hình',
      async run(qa) {
        await qa.goto('/mgmt/admin/ai')
        await qa.expect(qa.page.getByRole('heading', { name: 'Cấu hình AI' })).toBeVisible()
        await qa.expect(qa.page.locator('form.ai-settings')).toBeVisible()
        await qa.shot('cau-hinh-ai')
      },
    },
    {
      id: 'AC3',
      role: 'member',
      title: 'Thành viên mở Cấu hình AI thấy trang 404, không lộ màn hình có tồn tại',
      steps: ['Đăng nhập thanhvien@', 'Mở /mgmt/admin/ai'],
      expected: '"Không tìm thấy trang", như một đường dẫn không có',
      async run(qa) {
        await qa.goto('/mgmt/admin/ai')
        await qa.expect(qa.page.getByRole('heading', { name: 'Không tìm thấy trang' })).toBeVisible()
        await qa.shot('404')
      },
    },
    {
      id: 'AC4',
      role: 'member',
      title: 'API: thành viên gọi danh sách người dùng bị 403, chưa đăng nhập bị 401',
      steps: ['GET /api/users bằng token thanhvien@', 'GET /api/users không token'],
      expected: '403 rồi 401 (AGENTS.md: endpoint cố định trả 403, không phải 404)',
      async run(qa) {
        qa.expectStatus(await (await qa.api('member')).get('/api/users'), 403)
        qa.expectStatus(await (await qa.api('anon')).get('/api/users'), 401)
        qa.expectStatus(await (await qa.api('admin')).get('/api/users'), 200)
      },
    },
  ],
}

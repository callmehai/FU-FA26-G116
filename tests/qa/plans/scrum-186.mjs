// SCRUM-186 — the administrator's "Cấu hình AI" screen (ADR-0056, docs/03-api/18-may-ai.md): choose the in-house model
// or an OpenAI-compatible API for the drafts of Normal meetings, and "Thử kết nối" for what the picked tab shows.
//
// Safe to re-run on the local dev app: it never presses "Lưu", never sends a valid PUT, never changes the stored key,
// and never calls the paid API. The API test button is only exercised with invalid input (400 MSG409, checked before any
// call) or with a throwaway key against an unreachable `.invalid` host (502 MSG413), so the stored key never leaves the
// server. Every criterion that touches the form re-reads GET /api/admin/ai afterwards to prove nothing was saved.

const PATH = '/mgmt/admin/ai'
const UNREACHABLE = 'https://qa-khong-ton-tai.invalid/v1'
const DUMMY_KEY = 'qa-dummy-key-0000'

/** What GET returns, reduced to the fields a save would change. */
async function stored(qa) {
  const res = await (await qa.api('admin')).get('/api/admin/ai')
  qa.expectStatus(res, 200)
  const { mode, apiBaseUrl, apiModel, hasApiKey, apiKeyHint, updatedAt } = res.body
  return { mode, apiBaseUrl, apiModel, hasApiKey, apiKeyHint, updatedAt }
}

async function noSidewaysScroll(qa) {
  const sideways = await qa.page.evaluate(() => document.documentElement.scrollWidth - innerWidth)
  qa.expect(sideways, 'the page scrolls sideways').toBeLessThanOrEqual(0)
}

/** No raw message code or English error leaks onto the screen. */
async function noRawCodes(qa) {
  const text = await qa.page.locator('main').innerText()
  qa.expect(text, 'a raw MSG code is shown to the user').not.toMatch(/MSG\d+/)
}

/** Waits until CSS transitions end, so a shot taken right after a click shows the final state, not a fade. */
async function settled(qa) {
  await qa.page.waitForFunction(() => document.getAnimations().every((a) => a.playState !== 'running'))
}

const modeGroup = (qa) => qa.page.getByRole('radiogroup', { name: 'Viết dự thảo bằng' })
// The test result line inside the form; the "Chưa lưu …" indicator is also a status there, and the toast stack outside
// the form is one too, so scope to the form and leave the indicator out.
const testResult = (qa) => qa.page.locator('form').getByRole('status').filter({ hasNotText: 'Chưa lưu' })

async function openPage(qa) {
  await qa.goto(PATH)
  await qa.expect(qa.page.getByRole('heading', { name: 'Cấu hình AI' })).toBeVisible()
  await qa.expect(modeGroup(qa)).toBeVisible()
}

/** Pick the API tab and fill the form; `key` undefined leaves the key field empty. */
async function fillApi(qa, { url, model, key }) {
  await modeGroup(qa).getByRole('radio', { name: 'API' }).check()
  if (url !== undefined) await qa.page.getByLabel('Địa chỉ API').fill(url)
  if (model !== undefined) await qa.page.getByLabel('Model').fill(model)
  if (key !== undefined) await qa.page.getByLabel('Key').fill(key)
}

const DENIED = ['chair', 'secretary', 'member']

export default {
  ticket: 'SCRUM-186',
  title: 'Cấu hình AI: quản trị chọn máy local hoặc API cho dự thảo, Thử kết nối',
  devices: ['desktop'],
  criteria: [
    {
      id: 'AC1',
      role: 'admin',
      devices: ['desktop', 'phone'],
      title: 'Quản trị mở Cấu hình AI: thấy cấu hình đang dùng, key chỉ hiện 4 ký tự cuối, Lưu tắt khi chưa đổi gì',
      steps: ['Đăng nhập quantri@', 'Mở Cấu hình AI (/mgmt/admin/ai)'],
      expected:
        'Chọn "Máy local" / "API", tên model máy local và trạng thái cấu hình, dòng "Đang dùng …"; ô Key trống, gợi ý "Đã lưu ••••" + 4 ký tự cuối; nút Lưu bị tắt; không cuộn ngang; nút cao ≥ 40px trên điện thoại',
      async run(qa) {
        const before = await stored(qa)
        if (qa.device === 'desktop') {
          await qa.goto('/')
          // The entry sits under the collapsible "Quản trị" group of the sidebar.
          const link = qa.page.getByRole('link', { name: 'Cấu hình AI' })
          if (!(await link.isVisible())) await qa.page.getByRole('button', { name: 'Quản trị' }).click()
          await link.click()
          await qa.expect(qa.page).toHaveURL(new RegExp(`${PATH}$`))
          await qa.expect(modeGroup(qa)).toBeVisible()
        } else {
          await openPage(qa)
        }
        // The tab matches what is stored.
        const checked = before.mode === 'Api' ? 'API' : 'Máy local'
        await qa.expect(modeGroup(qa).getByRole('radio', { name: checked })).toBeChecked()
        await qa.expect(qa.page.getByText('Đang dùng', { exact: false }).first()).toBeVisible()
        await qa.expect(qa.page.getByRole('button', { name: 'Lưu' })).toBeDisabled()
        if (before.mode === 'Api') {
          const key = qa.page.getByLabel('Key')
          await qa.expect(key).toHaveValue('')
          await qa.expect(key).toHaveAttribute('type', 'password')
          if (before.hasApiKey) await qa.expect(key).toHaveAttribute('placeholder', `Đã lưu ••••${before.apiKeyHint}`)
          await qa.expect(qa.page.getByLabel('Địa chỉ API')).toHaveValue(before.apiBaseUrl)
          await qa.expect(qa.page.getByLabel('Model')).toHaveValue(before.apiModel)
        }
        await qa.shot('cau-hinh-ai', { fullPage: true })
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
        if (qa.device === 'phone') {
          for (const name of ['Lưu', /Thử kết nối/]) {
            const box = await qa.page.getByRole('button', { name }).boundingBox()
            qa.expect(box?.height ?? 0, `button ${name} is shorter than 40px on touch`).toBeGreaterThanOrEqual(40)
          }
        }
      },
    },
    {
      id: 'AC2',
      role: 'admin',
      devices: ['desktop', 'phone'],
      title: 'Màn hình nói rõ chỉ cuộc họp Thường dùng API; giải thích mở được bằng chạm, không chỉ hover',
      steps: ['Mở Cấu hình AI', 'Chạm/bấm (i) cạnh "Viết dự thảo bằng"'],
      expected: 'Hiện câu "Chỉ cuộc họp Thường dùng API. Cuộc họp Nội bộ và Mật luôn dùng máy local…"',
      async run(qa) {
        await openPage(qa)
        const tip = qa.page.getByRole('button', { name: /Chỉ cuộc họp Thường dùng API/ })
        await qa.expect(tip).toBeVisible()
        if (qa.device === 'phone') await tip.tap()
        else await tip.click()
        await qa.expect(qa.page.getByText(/Chỉ cuộc họp Thường dùng API\. Cuộc họp Nội bộ và Mật luôn dùng máy local/)).toBeVisible()
        await qa.shot('giai-thich-chi-hop-thuong')
      },
    },
    {
      id: 'AC3',
      role: 'admin',
      devices: ['desktop', 'phone'],
      title: 'Đổi tab Máy local / API không lưu gì: hiện "Chưa lưu · đang dùng …", tải lại thì về cấu hình cũ',
      steps: ['Mở Cấu hình AI', 'Chọn tab còn lại (không bấm Lưu)', 'Tải lại trang'],
      expected:
        'Sau khi chọn: dòng "Chưa lưu · đang dùng <cấu hình cũ>", nút Lưu bật; tab API mới có ô Địa chỉ/Model/Key. Tải lại: tab cũ, GET /api/admin/ai không đổi',
      async run(qa) {
        const before = await stored(qa)
        await openPage(qa)
        const other = before.mode === 'Api' ? 'Máy local' : 'API'
        await modeGroup(qa).getByRole('radio', { name: other }).check()
        await qa.expect(qa.page.getByText(/Chưa lưu · đang dùng/)).toBeVisible()
        await qa.expect(qa.page.getByRole('button', { name: 'Lưu' })).toBeEnabled()
        if (other === 'Máy local') await qa.expect(qa.page.getByLabel('Địa chỉ API')).toHaveCount(0)
        else await qa.expect(qa.page.getByLabel('Địa chỉ API')).toBeVisible()
        await settled(qa)
        await qa.shot('doi-tab-chua-luu')
        await noSidewaysScroll(qa)
        await qa.page.reload()
        await qa.expect(modeGroup(qa).getByRole('radio', { name: other === 'API' ? 'Máy local' : 'API' })).toBeChecked()
        await qa.expect(qa.page.getByText(/Chưa lưu/)).toHaveCount(0)
        await qa.shot('tai-lai-van-cau-hinh-cu')
        qa.expect(await stored(qa)).toEqual(before)
      },
    },
    {
      id: 'AC4',
      role: 'admin',
      devices: ['desktop', 'phone'],
      title: 'Thử API với địa chỉ không phải https: báo "Cấu hình AI không hợp lệ.", ô Địa chỉ API báo lỗi, không lưu',
      steps: ['Mở Cấu hình AI, tab API', 'Địa chỉ API: http://… , Key: một key thử', 'Bấm Thử kết nối'],
      expected: '400 MSG409 "Cấu hình AI không hợp lệ.", ô Địa chỉ API đỏ kèm lý do, cấu hình đã lưu không đổi',
      async run(qa) {
        const before = await stored(qa)
        await openPage(qa)
        await fillApi(qa, { url: 'http://qa-khong-ton-tai.invalid/v1', model: 'qa-model', key: DUMMY_KEY })
        const answer = qa.page.waitForResponse((r) => r.url().endsWith('/api/admin/ai/test'))
        await qa.page.getByRole('button', { name: 'Thử kết nối API' }).click()
        const res = await answer
        qa.expect(res.status()).toBe(400)
        qa.expect((await res.json()).code).toBe('MSG409')
        await qa.expect(testResult(qa)).toHaveText(/Cấu hình AI không hợp lệ\./)
        const url = qa.page.getByLabel('Địa chỉ API')
        await qa.expect(url).toHaveAttribute('aria-invalid', 'true')
        await url.scrollIntoViewIfNeeded()
        await qa.shot('http-bi-tu-choi', { fullPage: true })
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
        qa.expect(await stored(qa)).toEqual(before)
      },
    },
    {
      id: 'AC5',
      role: 'admin',
      devices: ['desktop', 'phone'],
      title: 'Thử API thiếu model và key có khoảng trắng: báo lỗi theo từng ô, không gọi ra ngoài, không lưu',
      steps: ['Mở Cấu hình AI, tab API', 'Xoá Model, Key: "qa key co khoang trang"', 'Bấm Thử kết nối'],
      expected: '400 MSG409 "Cấu hình AI không hợp lệ.", ô Model và ô Key đỏ kèm lý do; cấu hình đã lưu không đổi',
      async run(qa) {
        const before = await stored(qa)
        await openPage(qa)
        await fillApi(qa, { url: UNREACHABLE, model: '', key: 'qa key co khoang trang' })
        const answer = qa.page.waitForResponse((r) => r.url().endsWith('/api/admin/ai/test'))
        await qa.page.getByRole('button', { name: 'Thử kết nối API' }).click()
        const res = await answer
        qa.expect(res.status()).toBe(400)
        const body = await res.json()
        qa.expect(body.code).toBe('MSG409')
        qa.expect(Object.keys(body.errors ?? {}).map((k) => k.toLowerCase())).toEqual(
          qa.expect.arrayContaining(['apimodel', 'apikey']),
        )
        await qa.expect(testResult(qa)).toHaveText(/Cấu hình AI không hợp lệ\./)
        await qa.expect(qa.page.getByLabel('Model')).toHaveAttribute('aria-invalid', 'true')
        await qa.expect(qa.page.getByLabel('Key')).toHaveAttribute('aria-invalid', 'true')
        await qa.shot('thieu-model-key-khoang-trang', { fullPage: true })
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
        // Typing into a field clears its error.
        await qa.page.getByLabel('Model').fill('qa-model')
        await qa.expect(qa.page.getByLabel('Model')).toHaveAttribute('aria-invalid', 'false')
        qa.expect(await stored(qa)).toEqual(before)
      },
    },
    {
      id: 'AC6',
      role: 'admin',
      devices: ['desktop', 'phone'],
      title: 'Thử API không kết nối được: báo lý do bằng tiếng Việt, không lưu gì',
      steps: ['Mở Cấu hình AI, tab API', `Địa chỉ API: ${UNREACHABLE}, Model: qa-model, Key: một key thử`, 'Bấm Thử kết nối'],
      expected: '502 MSG413, dòng lỗi nói rõ lý do (không kết nối được), nút trở lại "Thử kết nối"; cấu hình đã lưu không đổi',
      async run(qa) {
        const before = await stored(qa)
        await openPage(qa)
        await fillApi(qa, { url: UNREACHABLE, model: 'qa-model', key: DUMMY_KEY })
        const answer = qa.page.waitForResponse((r) => r.url().endsWith('/api/admin/ai/test'), { timeout: 70_000 })
        await qa.page.getByRole('button', { name: 'Thử kết nối API' }).click()
        await qa.expect(qa.page.getByRole('button', { name: 'Thử kết nối API' })).toHaveText(/Đang thử…/)
        const res = await answer
        qa.expect(res.status()).toBe(502)
        const body = await res.json()
        qa.expect(body.code).toBe('MSG413')
        qa.note(`MSG413: ${body.message}`)
        await qa.expect(testResult(qa)).toBeVisible()
        await qa.expect(testResult(qa)).toHaveText(body.message)
        await qa.expect(qa.page.getByRole('button', { name: 'Thử kết nối API' })).toHaveText('Thử kết nối')
        await qa.shot('api-khong-ket-noi-duoc', { fullPage: true })
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
        qa.expect(await stored(qa)).toEqual(before)
      },
    },
    {
      id: 'AC7',
      role: 'admin',
      devices: ['desktop', 'phone'],
      title: 'Thử kết nối máy local từ tab Máy local khi đang dùng API: báo "Kết nối được <model>" và thời gian, không lưu',
      steps: ['Mở Cấu hình AI', 'Chọn tab Máy local (không bấm Lưu)', 'Bấm Thử kết nối (chờ tối đa 90 giây)'],
      expected:
        'Dòng xanh "Kết nối được qwen2.5:7b-instruct · N,N giây" (MSG412); máy tắt thì dòng đỏ nêu lý do (MSG413). Cấu hình đã lưu không đổi',
      async run(qa) {
        const before = await stored(qa)
        await openPage(qa)
        const local = (await (await qa.api('admin')).get('/api/admin/ai')).body
        if (!local.localConfigured) qa.inconclusive('Server chưa cấu hình máy local (Ai:BaseUrl): không có nút thử máy local')
        await modeGroup(qa).getByRole('radio', { name: 'Máy local' }).check()
        const button = qa.page.getByRole('button', { name: 'Thử kết nối máy local' })
        const answer = qa.page.waitForResponse((r) => r.url().endsWith('/api/admin/ai/test'), { timeout: 100_000 })
        await button.click()
        const res = await answer
        await qa.expect(testResult(qa)).toBeVisible()
        await qa.shot('thu-may-local', { fullPage: true })
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
        qa.expect(await stored(qa)).toEqual(before)
        const body = await res.json()
        if (res.status() === 502 && body.code === 'MSG413') {
          await qa.expect(testResult(qa)).toHaveText(body.message)
          qa.inconclusive(
            `Máy local không trả lời (${body.message}). Người kiểm: bật máy AI và Tailscale, bấm lại Thử kết nối ở tab Máy local, mong đợi "Kết nối được ${local.localModel} · N giây".`,
          )
        }
        qa.expect(res.status()).toBe(200)
        qa.expect(body.code).toBe('MSG412')
        const model = local.localModel.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
        await qa.expect(testResult(qa)).toHaveText(new RegExp(`^Kết nối được ${model} · \\d+,\\d giây$`))
      },
    },
    {
      id: 'AC8',
      role: 'admin',
      title: 'API: GET trả cấu hình nhưng không bao giờ trả key, chỉ hasApiKey và 4 ký tự cuối',
      steps: ['GET /api/admin/ai bằng token quantri@'],
      expected: '200 với mode, apiBaseUrl, apiModel, hasApiKey, apiKeyHint (≤ 4 ký tự), localModel, localConfigured; không có trường key',
      async run(qa) {
        const res = await (await qa.api()).get('/api/admin/ai')
        qa.expectStatus(res, 200)
        const keys = Object.keys(res.body)
        qa.expect(keys).toEqual(
          qa.expect.arrayContaining(['mode', 'apiBaseUrl', 'apiModel', 'hasApiKey', 'apiKeyHint', 'localModel', 'localConfigured', 'updatedAt']),
        )
        qa.expect(keys.filter((k) => /key/i.test(k) && !['hasApiKey', 'apiKeyHint'].includes(k))).toEqual([])
        qa.expect(['Local', 'Api']).toContain(res.body.mode)
        qa.expect(String(res.body.apiKeyHint ?? '').length).toBeLessThanOrEqual(4)
        qa.note(`mode=${res.body.mode}, model=${res.body.apiModel}, local=${res.body.localModel}`)
      },
    },
    {
      id: 'AC9',
      role: 'admin',
      title: 'API: dữ liệu sai bị từ chối 400 MSG409 kèm lỗi theo trường, không lưu, không gọi ra ngoài',
      steps: [
        'PUT /api/admin/ai mode "Khac"',
        'POST /api/admin/ai/test: mode "api" (sai hoa thường); địa chỉ có user:pass; key < 8 ký tự; model có khoảng trắng',
      ],
      expected: 'Mỗi lời gọi 400 MSG409 với errors đúng trường (mode, apiBaseUrl, apiKey, apiModel); GET sau đó không đổi',
      async run(qa) {
        const before = await stored(qa)
        const api = await qa.api()
        const ok = { mode: 'Api', apiBaseUrl: UNREACHABLE, apiModel: 'qa-model', apiKey: DUMMY_KEY }
        const cases = [
          ['put', { ...ok, mode: 'Khac' }, 'mode'],
          ['post', { ...ok, mode: 'api' }, 'mode'],
          ['post', { ...ok, apiBaseUrl: 'https://user:pass@qa-khong-ton-tai.invalid/v1' }, 'apiBaseUrl'],
          ['post', { ...ok, apiKey: 'short' }, 'apiKey'],
          ['post', { ...ok, apiModel: 'qa model' }, 'apiModel'],
        ]
        // Run every case, then report all the wrong ones together.
        const wrong = []
        for (const [method, body, field] of cases) {
          const res = method === 'put' ? await api.put('/api/admin/ai', body) : await api.post('/api/admin/ai/test', body)
          const fields = Object.keys(res.body?.errors ?? {}).map((k) => k.toLowerCase())
          const okCase =
            res.status === 400 &&
            res.body.code === 'MSG409' &&
            res.body.message === 'Cấu hình AI không hợp lệ.' &&
            fields.includes(field.toLowerCase())
          if (!okCase) wrong.push(`${method.toUpperCase()} ${field}=${JSON.stringify(body[field])} → ${res.status} ${JSON.stringify(res.body).slice(0, 200)}`)
        }
        qa.expect(await stored(qa)).toEqual(before)
        qa.expect(wrong, wrong.join('\n')).toEqual([])
      },
    },
    ...DENIED.map((role, i) => ({
      id: `AC${10 + i}`,
      role,
      devices: ['desktop', 'phone'],
      title: `${{ chair: 'Chủ toạ', secretary: 'Thư ký', member: 'Thành viên' }[role]} không thấy Cấu hình AI: mục ẩn trên menu, đường dẫn báo 404, API 403`,
      steps: [`Đăng nhập ${role}`, 'Xem menu', 'Mở /mgmt/admin/ai', 'GET, PUT /api/admin/ai và POST /api/admin/ai/test'],
      expected: 'Không có mục "Cấu hình AI"; trang "Không tìm thấy trang" như đường dẫn không có; API 403, không lưu gì',
      async run(qa) {
        const before = await stored(qa)
        // The seed role may have been edited on this database (roles are data): a role holding AdminUsers is allowed.
        const me = (await (await qa.api()).get('/api/auth/me')).body
        if (me.capabilities?.includes('AdminUsers'))
          qa.inconclusive(
            `${me.email} giữ vai trò "${me.roleName}" có AdminUsers trên database này, nên không phải vai trò bị chặn. Người kiểm: chạy lại trên database seed sạch, hoặc trả ${me.email} về vai trò gốc.`,
          )
        await qa.goto('/')
        if (qa.device === 'desktop') await qa.expect(qa.page.getByRole('link', { name: 'Cấu hình AI' })).toHaveCount(0)
        await qa.goto(PATH)
        await qa.expect(qa.page.getByRole('heading', { name: 'Không tìm thấy trang' })).toBeVisible()
        await qa.expect(modeGroup(qa)).toHaveCount(0)
        await qa.shot('404')
        await noSidewaysScroll(qa)
        if (qa.device !== 'desktop') return
        const api = await qa.api()
        // An invalid body: if authorization were missing the answer is 400, never a save.
        const bad = { mode: 'Khac', apiBaseUrl: '', apiModel: '', apiKey: null }
        qa.expectStatus(await api.get('/api/admin/ai'), 403)
        qa.expectStatus(await api.put('/api/admin/ai', bad), 403)
        qa.expectStatus(await api.post('/api/admin/ai/test', bad), 403)
        qa.expect(await stored(qa)).toEqual(before)
      },
    })),
    {
      id: 'AC13',
      role: 'anon',
      title: 'Chưa đăng nhập: trang đưa về Đăng nhập, API 401',
      steps: ['Không đăng nhập, mở /mgmt/admin/ai', 'GET, PUT /api/admin/ai và POST /api/admin/ai/test không token'],
      expected: 'Chuyển tới /login; API 401',
      async run(qa) {
        await qa.goto(PATH)
        await qa.expect(qa.page).toHaveURL(/\/login/)
        await qa.expect(modeGroup(qa)).toHaveCount(0)
        await qa.shot('ve-dang-nhap')
        const api = await qa.api('anon')
        const bad = { mode: 'Khac', apiBaseUrl: '', apiModel: '', apiKey: null }
        qa.expectStatus(await api.get('/api/admin/ai'), 401)
        qa.expectStatus(await api.put('/api/admin/ai', bad), 401)
        qa.expectStatus(await api.post('/api/admin/ai/test', bad), 401)
      },
    },
  ],
}

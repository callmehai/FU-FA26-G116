// Report 5.3 System Test — WF-08 Documents Across the Meeting (BF-02).
// docs/08-reports/test-reports/cases/system/WF-08.json describes each case. Two Published meetings are made here
// (precondition), organised by thanhvien@ with Chủ toạ duc.hoang@, Thư ký lan.vu@, Member bao.dang@: one Thường, one Mật.
// The Thư ký's files go from her personal library into both meetings.

import {
  PEOPLE,
  RUN,
  client,
  closeSeconds,
  dialog,
  docxFile,
  meetingAt,
  noSidewaysScroll,
  pdfFile,
  person,
  secondBrowser,
  settled,
  shotOf,
  toast,
} from './sys-lib.mjs'

const state = {}
const PDF = pdfFile(`to-trinh-ngan-sach-${RUN}.pdf`, 3)
const DOCX = docxFile(`phu-luc-du-toan-${RUN}.docx`)
const COMMENT = 'Đề nghị làm rõ số liệu chi đào tạo ở trang này.'
const REPLY = 'Đã ghi nhận, phòng Tài chính sẽ bổ sung.'

const libRow = (page, name) => page.locator('table.library-table tbody tr').filter({ hasText: name })
const docs = (page) => page.locator('.documents-panel')
const docRow = (page, name) => docs(page).locator('li.documents-row').filter({ hasText: name })

async function setup(qa) {
  if (state.normal) return
  state.normal = await meetingAt(qa, 'Published', { title: `[SYS] WF-08 Họp tài liệu ${RUN}` })
  state.secret = await meetingAt(qa, 'Published', { title: `[SYS] WF-08 Họp Mật ${RUN}`, over: { sensitivity: 'Secret' } })
}

async function openDocs(qa, page, m) {
  await page.goto(`/mgmt/meetings/${m.id}?tab=documents`)
  await qa.expect(page.getByRole('heading', { level: 1, name: m.title })).toBeVisible()
  await qa.expect(docs(page).getByRole('radiogroup', { name: 'Nhóm tài liệu' })).toBeVisible()
}

/** The Thư ký adds her two library files to a meeting from "Kho". */
async function addFromLibrary(qa, page, m, names) {
  await openDocs(qa, page, m)
  await docs(page).getByRole('button', { name: 'Thêm tài liệu chung' }).click()
  const form = dialog(page, 'Thêm tài liệu chung')
  await form.locator('label.segmented-option').filter({ hasText: 'Kho' }).click()
  const picker = form.getByRole('list', { name: 'Thư mục và tài liệu' })
  for (const n of names) await picker.locator('label.picker-row').filter({ hasText: n }).getByRole('checkbox').check()
  await settled(page)
  return form
}

async function meetingDocId(qa, who, m, name) {
  const list = (await (await client(qa, who)).get(`/api/meetings/${m.id}/documents`)).body
  return list.general.find((d) => d.fileName === name)?.id
}

export default {
  ticket: 'SYS-WF08',
  title: 'WF-08 Documents Across the Meeting',
  devices: ['desktop'],
  criteria: [
    {
      id: 'TC01',
      role: PEOPLE.memberB,
      title: 'The Secretary uploads a PDF and a DOCX to her personal library and opens the PDF',
      async run(qa) {
        await setup(qa)
        await qa.goto('/mgmt/documents')
        await qa.expect(qa.page.locator('table.library-table tbody[aria-busy="false"]')).toBeVisible()
        await qa.page.getByRole('button', { name: /Tải (tài liệu )?lên/ }).first().click()
        const form = dialog(qa.page, 'Tải tài liệu lên')
        await form.locator('input[type=file]').setInputFiles([PDF, DOCX])
        await qa.expect(form.getByRole('list', { name: 'Tệp đã chọn' }).getByRole('listitem')).toHaveCount(2)
        await qa.shot('chon-hai-tep')
        await form.getByRole('button', { name: 'Tải lên 2 tài liệu' }).click()
        await qa.expect(qa.page.getByRole('region', { name: 'Tải lên' }).getByText(/Đã tải lên 2 tệp/)).toBeVisible({ timeout: 30_000 })
        await qa.expect(libRow(qa.page, PDF.name)).toContainText('3 trang')
        await qa.expect(libRow(qa.page, DOCX.name)).toBeVisible()
        await settled(qa.page)
        await qa.shot('kho-co-hai-tep')
        await libRow(qa.page, PDF.name).getByRole('link', { name: PDF.name }).click()
        await qa.expect(qa.page.getByRole('heading', { level: 1, name: PDF.name })).toBeVisible()
        await qa.expect(qa.page.locator('.viewer-stage canvas').first()).toBeVisible({ timeout: 15_000 })
        await settled(qa.page)
        await qa.shot('xem-pdf-trong-kho')
        await noSidewaysScroll(qa)
      },
    },
    {
      id: 'TC02',
      role: PEOPLE.memberB,
      title: 'The Secretary attaches both library files to the meeting; a member opens the DOCX',
      async run(qa) {
        await setup(qa)
        const form = await addFromLibrary(qa, qa.page, state.normal, [PDF.name, DOCX.name])
        await qa.shot('chon-tu-kho')
        await form.getByRole('button', { name: 'Thêm 2 tài liệu' }).click()
        await qa.expect(toast(qa.page, 'Đã thêm tài liệu vào cuộc họp.')).toBeVisible()
        await qa.expect(docRow(qa.page, PDF.name)).toBeVisible()
        await qa.expect(docRow(qa.page, DOCX.name)).toBeVisible()
        await settled(qa.page)
        await qa.shot('tai-lieu-chung-cua-cuoc-hop')
        const c = await secondBrowser(qa, 'memberC')
        try {
          await openDocs(qa, c, state.normal)
          await qa.expect(docRow(c, PDF.name)).toBeVisible()
          await docRow(c, DOCX.name).getByRole('link').first().click()
          await qa.expect(c.getByRole('heading', { level: 1, name: DOCX.name })).toBeVisible()
          await qa.expect(c.getByRole('button', { name: /Tải về/ }).first()).toBeVisible({ timeout: 15_000 })
          await c.waitForTimeout(1500)
          await shotOf(qa, c, 'thanh-vien-mo-docx', 'memberC')
        } finally {
          await closeSeconds(qa)
        }
      },
    },
    {
      id: 'TC03',
      role: PEOPLE.memberC,
      title: 'A member comments on page 1 of the PDF; the Chair replies in the same thread',
      async run(qa) {
        await setup(qa)
        const [a, c] = [await person(qa, 'memberA'), await person(qa, 'memberC')]
        const id = await meetingDocId(qa, 'memberC', state.normal, PDF.name)
        qa.expect(id, 'TC02 did not attach the PDF').toBeTruthy()
        await qa.goto(`/mgmt/meetings/${state.normal.id}/documents/${id}`)
        await qa.expect(qa.page.getByText('Trang 1 / 3')).toBeVisible({ timeout: 15_000 })
        const panel = qa.page.getByRole('complementary', { name: 'Góp ý' })
        await panel.getByLabel('Nội dung góp ý').fill(COMMENT)
        await qa.expect(panel.getByLabel('Gắn trang 1')).toBeChecked()
        await panel.getByRole('button', { name: 'Gửi góp ý' }).click()
        const mine = panel.locator('li.comment-thread').filter({ hasText: COMMENT })
        await qa.expect(mine.getByText(c.name).first()).toBeAttached()
        await mine.scrollIntoViewIfNeeded()
        await settled(qa.page)
        await qa.shot('thanh-vien-gop-y-trang-1')
        const chair = await secondBrowser(qa, 'memberA')
        try {
          await chair.goto(`/mgmt/meetings/${state.normal.id}/documents/${id}`)
          const cp = chair.getByRole('complementary', { name: 'Góp ý' })
          const thread = cp.locator('li.comment-thread').filter({ hasText: COMMENT })
          await qa.expect(thread).toBeVisible({ timeout: 15_000 })
          await thread.getByRole('button', { name: /Trả lời/ }).first().click()
          await cp.getByLabel(`Trả lời ${c.name}`).fill(REPLY)
          await cp.getByRole('button', { name: 'Gửi trả lời' }).click()
          await qa.expect(thread.locator('.comment-replies').getByText(REPLY)).toBeAttached()
          await settled(chair)
          await shotOf(qa, chair, 'chu-toa-tra-loi', 'memberA')
        } finally {
          await closeSeconds(qa)
        }
        // The reply reaches the open viewer without a reload; the refresh button only shows when the live link is down.
        const refresh = panel.getByRole('button', { name: 'Làm mới góp ý' })
        if (await refresh.isVisible().catch(() => false)) await refresh.click()
        await qa.expect(mine.locator('.comment-replies').getByText(REPLY)).toBeAttached({ timeout: 15_000 })
        await qa.expect(mine.locator('.comment-replies').getByText(a.name)).toBeAttached()
        await mine.scrollIntoViewIfNeeded()
        await settled(qa.page)
        await qa.shot('thay-tra-loi-trong-luong')
        const comments = (await (await client(qa, 'memberC')).get(`/api/meetings/${state.normal.id}/documents/${id}/comments`)).body.comments
        const root = comments.find((x) => x.body === COMMENT)
        qa.expect(root?.pageNumber).toBe(1)
      },
    },
    {
      id: 'TC04',
      role: PEOPLE.memberC,
      title: 'In the Mật meeting the same file is view-only for a member; an outsider cannot reach it at all',
      async run(qa) {
        await setup(qa)
        const sec = await secondBrowser(qa, 'memberB')
        try {
          const form = await addFromLibrary(qa, sec, state.secret, [PDF.name])
          await form.getByRole('button', { name: 'Thêm 1 tài liệu' }).click()
          await qa.expect(toast(sec, 'Đã thêm tài liệu vào cuộc họp.')).toBeVisible()
        } finally {
          await closeSeconds(qa)
        }
        const id = await meetingDocId(qa, 'memberC', state.secret, PDF.name)
        await openDocs(qa, qa.page, state.secret)
        await qa.expect(docRow(qa.page, PDF.name)).toBeVisible()
        await qa.expect(docs(qa.page).getByRole('button', { name: /^Tải về/ })).toHaveCount(0)
        await qa.shot('hop-mat-khong-co-tai-ve')
        await docRow(qa.page, PDF.name).getByRole('link').first().click()
        await qa.expect(qa.page.getByText('Chỉ xem trực tuyến')).toBeVisible()
        await qa.expect(qa.page.getByRole('button', { name: /Tải về/ })).toHaveCount(0)
        await qa.expect(qa.page.locator('.viewer-stage canvas').first()).toBeVisible({ timeout: 15_000 })
        await settled(qa.page)
        await qa.shot('chi-xem-truc-tuyen')
        const member = await client(qa, 'memberC')
        const dl = await member.get(`/api/meetings/${state.secret.id}/documents/${id}/content?download=true`)
        qa.note(`Member download in the Mật meeting: ${dl.status} ${dl.body?.code}`)
        qa.expect(dl.status).toBe(403)
        qa.expect(dl.body?.code).toBe('MSG159')
        const normalId = await meetingDocId(qa, 'memberC', state.normal, PDF.name)
        const okDl = await member.get(`/api/meetings/${state.normal.id}/documents/${normalId}/content?download=true`)
        qa.expect(okDl.status, 'download in the Thường meeting').toBe(200)
        const out = await client(qa, 'memberD')
        const o = await out.get(`/api/meetings/${state.secret.id}/documents/${id}/content`)
        qa.note(`Outsider reads the file: ${o.status} ${o.body?.code}`)
        qa.expect(o.status).toBe(404)
        const page = await secondBrowser(qa, 'memberD')
        try {
          await page.goto(`/mgmt/meetings/${state.secret.id}/documents/${id}`)
          await qa.expect(page.getByRole('heading', { name: 'Không tìm thấy trang' })).toBeVisible({ timeout: 15_000 })
          await shotOf(qa, page, 'nguoi-ngoai-khong-mo-duoc', 'memberD')
        } finally {
          await closeSeconds(qa)
        }
      },
    },
  ],
}

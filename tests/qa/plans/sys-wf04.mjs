// Report 5.3 System Test — WF-04 Post-Meeting Processing and Formal Minutes (BF-04).
// docs/08-reports/test-reports/cases/system/WF-04.json describes each case. This plan runs the minutes cases (TC05 to
// TC08) on the Ended meeting WF-03 left behind; TC01 to TC04 (transcription, AI summary, AI draft) need the speech worker
// and the local language model, which this machine does not run, and are reported as not run.
// TC09 (correcting issued minutes) makes its own Ended meeting with issued minutes, so it runs on its own.
//
// Never asks for an AI draft: the local server may be set to write Normal meetings through a paid outside API.

import {
  PEOPLE,
  RUN,
  client,
  closeSeconds,
  dialog,
  meetingAt,
  noRawCodes,
  noSidewaysScroll,
  person,
  secondBrowser,
  settled,
  shotOf,
  sql,
  toast,
} from './sys-lib.mjs'

const TITLE = '[SYS] Họp giao ban tuần 49'
const ITEM_CONCLUSION = 'Thống nhất dự toán ngân sách năm 2027 như tờ trình.'
const RETURN_REASON = 'Bổ sung danh sách vắng mặt và ý kiến của phòng Tài chính.'
const ADDED_LINE = 'Vắng mặt: Trần Khách (khách mời). Phòng Tài chính thống nhất số liệu dự toán.'
const state = {}

const card = (page) => page.getByRole('region', { name: 'Biên bản', exact: true })
const badge = (page, label) => card(page).locator('.minutes-card-head').getByText(label, { exact: true })
const paper = (page) => card(page).locator('article.minutes-paper')
const btn = (page, name) => card(page).getByRole('button', { name, exact: true })
const meta = (page) => card(page).locator('.minutes-meta')

async function meeting(qa) {
  if (state.id) return state
  const list = await (await client(qa, 'organizer')).get(`/api/meetings?q=${encodeURIComponent(TITLE)}&pageSize=50`)
  const m = (list.body.items ?? []).filter((i) => i.title === TITLE && i.status === 'Ended')
  m.sort((x, y) => (x.id < y.id ? 1 : -1))
  for (const x of m) {
    const st = (await (await client(qa, 'memberB')).get(`/api/meetings/${x.id}/minutes`)).body
    if (!st.minutes) {
      state.id = x.id
      return state
    }
  }
  throw new Error('no Ended meeting from WF-03 without minutes')
}

async function openMinutes(qa, page) {
  await page.goto(`/mgmt/meetings/${state.id}?tab=minutes`)
  await qa.expect(page.getByRole('heading', { level: 1, name: TITLE })).toBeVisible()
  await qa.expect(card(page)).toBeVisible()
  await qa.expect(card(page)).not.toHaveAttribute('aria-busy', 'true')
}

async function minutes(qa, who) {
  return (await (await client(qa, who)).get(`/api/meetings/${state.id}/minutes`)).body
}

async function notes(qa, who, type) {
  const res = await (await client(qa, who)).get('/api/notifications?page=1&pageSize=50')
  return (res.body.items ?? []).filter((n) => n.meetingId === state.id && n.type === type)
}

/** Closes live notification toasts so a shot shows the card. */
async function quiet(page) {
  const closers = page.locator('.toast-stack .toast-info .toast-close')
  for (let i = await closers.count(); i > 0; i--) await closers.first().evaluate((b) => b.click()).catch(() => undefined)
  await settled(page)
}

const notRun = (reason) => async (qa) => qa.inconclusive(reason)
const NO_WORKER = 'Needs the speech worker and the local language model, which do not run on this test machine.'

export default {
  ticket: 'SYS-WF04',
  title: 'WF-04 Post-Meeting Processing and Formal Minutes',
  devices: ['desktop'],
  criteria: [
    { id: 'TC01', role: PEOPLE.memberB, title: 'Upload the meeting audio and transcribe it', run: notRun(NO_WORKER) },
    { id: 'TC02', role: PEOPLE.memberB, title: 'Edit the transcript', run: notRun(NO_WORKER) },
    { id: 'TC03', role: PEOPLE.memberB, title: 'AI summary of the meeting', run: notRun(NO_WORKER) },
    { id: 'TC04', role: PEOPLE.memberB, title: 'AI draft of the minutes', run: notRun(NO_WORKER) },
    {
      id: 'TC05',
      role: PEOPLE.memberB,
      title: 'Secretary makes the minutes from the fixed template and submits them to the Chair',
      async run(qa) {
        await meeting(qa)
        const [a, b] = [await person(qa, 'memberA'), await person(qa, 'memberB')]
        await openMinutes(qa, qa.page)
        await qa.expect(card(qa.page).getByText('Chưa có dự thảo AI nên mục I–III để trống.')).toBeVisible()
        await btn(qa.page, 'Tạo bản nháp').click()
        await qa.expect(toast(qa.page, 'Đã tạo bản nháp biên bản.')).toBeVisible()
        await qa.expect(badge(qa.page, 'Bản nháp')).toBeVisible()
        for (const text of [TITLE, a.name, b.name, ITEM_CONCLUSION]) await qa.expect(paper(qa.page), `paper misses ${text}`).toContainText(text)
        await quiet(qa.page)
        await qa.shot('ban-nhap-tu-mau')
        await btn(qa.page, 'Gửi duyệt').click()
        const d = dialog(qa.page, 'Gửi biên bản cho Chủ toạ duyệt?')
        await qa.expect(d).toBeVisible()
        await qa.shot('hoi-gui-duyet')
        await d.getByRole('button', { name: 'Gửi duyệt', exact: true }).click()
        await qa.expect(toast(qa.page, 'Đã gửi biên bản cho Chủ toạ duyệt.')).toBeVisible()
        await qa.expect(badge(qa.page, 'Chờ duyệt')).toBeVisible()
        for (const name of ['Sửa', 'Tạo lại', 'Gửi duyệt']) await qa.expect(btn(qa.page, name)).toHaveCount(0)
        await qa.expect(meta(qa.page)).toContainText(`${b.name} gửi duyệt lúc`)
        await quiet(qa.page)
        await qa.shot('cho-duyet')
        await noSidewaysScroll(qa)
        const st = await minutes(qa, 'memberB')
        qa.expect(st.minutes.status).toBe('WaitingApproval')
        qa.expect((await notes(qa, 'memberA', 'MinutesWaitingApproval')).length, 'Chair has no notification').toBeGreaterThan(0)
        // A plain member does not see minutes that are not issued.
        const c = await minutes(qa, 'memberC')
        qa.expect(c.minutes, 'member C sees unissued minutes').toBeFalsy()
      },
    },
    {
      id: 'TC06',
      role: PEOPLE.memberA,
      title: 'Chair returns the minutes with a reason; Secretary corrects and resubmits',
      async run(qa) {
        await meeting(qa)
        const a = await person(qa, 'memberA')
        await openMinutes(qa, qa.page)
        await qa.expect(badge(qa.page, 'Chờ duyệt')).toBeVisible()
        await qa.expect(btn(qa.page, 'Duyệt và ban hành')).toBeVisible()
        await btn(qa.page, 'Trả lại').click()
        const d = dialog(qa.page, 'Trả lại biên bản')
        await d.getByRole('button', { name: 'Trả lại', exact: true }).click()
        await qa.expect(d.getByText('Nhập lý do trả lại, tối đa 2.000 ký tự.')).toBeVisible()
        await d.getByRole('textbox').fill(RETURN_REASON)
        await d.getByRole('button', { name: 'Trả lại', exact: true }).click()
        await qa.expect(toast(qa.page, 'Đã trả lại biên bản cho Thư ký.')).toBeVisible()
        await qa.expect(badge(qa.page, 'Bị trả lại')).toBeVisible()
        await quiet(qa.page)
        await qa.shot('chu-toa-tra-lai')
        qa.expect((await notes(qa, 'memberB', 'MinutesReturned')).length, 'Secretary has no notification').toBeGreaterThan(0)
        const sec = await secondBrowser(qa, 'memberB')
        try {
          await openMinutes(qa, sec)
          const banner = card(sec).locator('.minutes-returned')
          await qa.expect(banner).toContainText(`${a.name} trả lại:`)
          await qa.expect(banner).toContainText(RETURN_REASON)
          await quiet(sec)
          await shotOf(qa, sec, 'thu-ky-thay-ly-do', 'memberB')
          await btn(sec, 'Sửa').click()
          const editor = card(sec).getByRole('textbox', { name: 'Nội dung biên bản' })
          await qa.expect(editor).toBeVisible()
          await editor.evaluate((el) => {
            el.focus()
            const p = [...el.querySelectorAll('p')].reverse().find((x) => x.textContent.includes('Cuộc họp kết thúc vào'))
            const range = document.createRange()
            range.selectNodeContents(p ?? el)
            range.collapse(false)
            const sel = getSelection()
            sel.removeAllRanges()
            sel.addRange(range)
          })
          await sec.keyboard.press('Enter')
          await sec.keyboard.type(ADDED_LINE)
          await card(sec).getByRole('button', { name: 'Lưu', exact: true }).click()
          await qa.expect(toast(sec, 'Đã lưu biên bản.')).toBeVisible()
          await qa.expect(paper(sec)).toContainText(ADDED_LINE)
          await btn(sec, 'Gửi duyệt').click()
          await dialog(sec, 'Gửi biên bản cho Chủ toạ duyệt?').getByRole('button', { name: 'Gửi duyệt', exact: true }).click()
          await qa.expect(toast(sec, 'Đã gửi biên bản cho Chủ toạ duyệt.')).toBeVisible()
          await qa.expect(badge(sec, 'Chờ duyệt')).toBeVisible()
          await qa.expect(card(sec).locator('.minutes-returned')).toHaveCount(0)
          await quiet(sec)
          await shotOf(qa, sec, 'da-sua-gui-lai', 'memberB')
        } finally {
          await closeSeconds(qa)
        }
        qa.expect((await minutes(qa, 'memberB')).minutes.status).toBe('WaitingApproval')
      },
    },
    {
      id: 'TC07',
      role: PEOPLE.memberA,
      title: 'Chair approves and issues the minutes; they are locked and participants are notified',
      async run(qa) {
        await meeting(qa)
        const a = await person(qa, 'memberA')
        await openMinutes(qa, qa.page)
        await qa.expect(paper(qa.page)).toContainText(ADDED_LINE)
        await btn(qa.page, 'Duyệt và ban hành').click()
        const d = dialog(qa.page, 'Duyệt và ban hành biên bản?')
        await qa.expect(d).toBeVisible()
        await qa.shot('hoi-duyet-ban-hanh')
        await d.getByRole('button', { name: 'Duyệt và ban hành', exact: true }).click()
        await qa.expect(toast(qa.page, 'Đã duyệt và ban hành biên bản.')).toBeVisible()
        await qa.expect(badge(qa.page, 'Đã ban hành')).toBeVisible()
        for (const name of ['Sửa', 'Tạo lại', 'Gửi duyệt', 'Duyệt và ban hành', 'Trả lại']) await qa.expect(btn(qa.page, name)).toHaveCount(0)
        await qa.expect(meta(qa.page)).toContainText(`${a.name} duyệt, ban hành lúc`)
        await quiet(qa.page)
        await qa.shot('da-ban-hanh')
        await noRawCodes(qa)
        const st = await minutes(qa, 'memberA')
        qa.expect(st.minutes.status).toBe('Issued')
        state.minutesId = st.minutes.id
        for (const who of ['memberB', 'memberC']) qa.expect((await notes(qa, who, 'MinutesIssued')).length, `${who} not notified`).toBeGreaterThan(0)
        qa.expect((await notes(qa, 'memberA', 'MinutesIssued')).length, 'Chair notified of own approval').toBe(0)
        const audit = sql(`select count(*) from audit_log where action = 'minutes.issued' and object_id = '${st.minutes.id}'`)
        qa.expect(Number(audit[0][0])).toBe(1)
        const chair = await client(qa, 'memberA')
        const edit = await chair.put(`/api/minutes/${st.minutes.id}`, { content: '<p>x</p>', updatedAt: st.minutes.updatedAt })
        qa.expect(edit.status, 'issued minutes accepted an edit').toBe(409)
        qa.note(`Edit of issued minutes: ${edit.status} ${edit.body?.code}`)
      },
    },
    {
      id: 'TC08',
      role: PEOPLE.memberC,
      title: 'A member reads and downloads the issued minutes; a user outside the meeting cannot',
      async run(qa) {
        await meeting(qa)
        const a = await person(qa, 'memberA')
        await openMinutes(qa, qa.page)
        await qa.expect(badge(qa.page, 'Đã ban hành')).toBeVisible()
        await qa.expect(paper(qa.page)).toContainText(ITEM_CONCLUSION)
        await qa.expect(paper(qa.page)).toContainText(ADDED_LINE)
        await qa.expect(meta(qa.page)).toContainText(`${a.name} duyệt, ban hành lúc`)
        for (const name of ['Sửa', 'Gửi duyệt', 'Duyệt và ban hành', 'Trả lại', 'Tạo bản đính chính']) await qa.expect(btn(qa.page, name)).toHaveCount(0)
        await quiet(qa.page)
        await qa.shot('thanh-vien-doc-bien-ban')
        await btn(qa.page, 'Tải xuống').click()
        const menu = qa.page.getByRole('menu', { name: 'Tải xuống' })
        const [download] = await Promise.all([qa.page.waitForEvent('download'), menu.getByRole('menuitem', { name: /PDF/ }).click()])
        qa.expect(download.suggestedFilename()).toBe(`Biên bản - ${TITLE}.pdf`)
        const st = await minutes(qa, 'memberC')
        qa.expect(st.minutes.rejectionReason).toBeNull()
        const out = await client(qa, 'memberD')
        const r1 = await out.get(`/api/meetings/${state.id}/minutes`)
        qa.expect(r1.status, 'outsider reads the minutes').toBe(404)
        const page = await secondBrowser(qa, 'memberD')
        try {
          await page.goto(`/mgmt/meetings/${state.id}?tab=minutes`)
          await qa.expect(page.getByRole('heading', { name: 'Không tìm thấy trang' }).or(page.getByText(/Không tìm thấy/).first())).toBeVisible()
          await shotOf(qa, page, 'nguoi-ngoai-khong-mo-duoc', 'memberD')
        } finally {
          await closeSeconds(qa)
        }
      },
    },
    {
      id: 'TC09',
      role: PEOPLE.memberA,
      title: 'Issued minutes are corrected; members read the issued version until the correction is issued',
      async run(qa) {
        // Precondition (API): a meeting of its own with minutes issued from the fixed template, so this case does not
        // depend on the TC05–TC08 chain.
        const title = `[SYS] WF-04 Đính chính biên bản ${RUN}`
        const m = await meetingAt(qa, 'Ended', { title })
        const sec = await client(qa, 'memberB')
        qa.expectStatus(await sec.post(`/api/meetings/${m.id}/minutes`, {}), 201)
        let st = (await sec.get(`/api/meetings/${m.id}/minutes`)).body
        qa.expectStatus(await sec.post(`/api/minutes/${st.minutes.id}/submit`, { updatedAt: st.minutes.updatedAt }), 200)
        st = (await sec.get(`/api/meetings/${m.id}/minutes`)).body
        qa.expectStatus(await (await client(qa, 'memberA')).post(`/api/minutes/${st.minutes.id}/approve`, { updatedAt: st.minutes.updatedAt }), 200)
        const original = st.minutes.id
        const LINE = 'Đính chính: bổ sung ý kiến của phòng Tài chính về số liệu dự toán.'
        const open = async (page) => {
          await page.goto(`/mgmt/meetings/${m.id}?tab=minutes`)
          await qa.expect(page.getByRole('heading', { level: 1, name: title })).toBeVisible()
          await qa.expect(card(page)).toBeVisible()
          await qa.expect(card(page)).not.toHaveAttribute('aria-busy', 'true')
        }
        const versions = (page) => card(page).getByRole('button', { name: /^Phiên bản biên bản/ })
        // 1. The Chair starts a correction.
        await open(qa.page)
        await qa.expect(badge(qa.page, 'Đã ban hành')).toBeVisible()
        await btn(qa.page, 'Tạo bản đính chính').click()
        const d = dialog(qa.page, 'Tạo bản đính chính?')
        await qa.expect(d).toBeVisible()
        await qa.shot('hoi-tao-dinh-chinh')
        await d.getByRole('button', { name: 'Tạo bản đính chính', exact: true }).click()
        await qa.expect(toast(qa.page, 'Đã tạo bản đính chính.')).toBeVisible()
        await qa.expect(badge(qa.page, 'Đính chính · bản nháp')).toBeVisible()
        await qa.expect(paper(qa.page)).toContainText(title)
        await versions(qa.page).click()
        const list = qa.page.getByRole('listbox', { name: 'Phiên bản biên bản' })
        await qa.expect(list.getByRole('option', { name: /Đính chính 1/ })).toBeVisible()
        await qa.expect(list.getByRole('option', { name: /Bản gốc/ })).toBeVisible()
        await qa.shot('dinh-chinh-ban-nhap')
        await qa.page.keyboard.press('Escape')
        // 2. The Secretary edits and sends the correction.
        const b = await secondBrowser(qa, 'memberB')
        const c = await secondBrowser(qa, 'memberC')
        try {
          await open(b)
          await btn(b, 'Sửa').click()
          const editor = card(b).getByRole('textbox', { name: 'Nội dung biên bản' })
          await qa.expect(editor).toBeVisible()
          await editor.evaluate((el) => {
            el.focus()
            const p = [...el.querySelectorAll('p')].reverse().find((x) => x.textContent.includes('Cuộc họp kết thúc vào'))
            const range = document.createRange()
            range.selectNodeContents(p ?? el)
            range.collapse(false)
            const sel = getSelection()
            sel.removeAllRanges()
            sel.addRange(range)
          })
          await b.keyboard.press('Enter')
          await b.keyboard.type(LINE)
          await card(b).getByRole('button', { name: 'Lưu', exact: true }).click()
          await qa.expect(toast(b, 'Đã lưu biên bản.')).toBeVisible()
          await btn(b, 'Gửi duyệt').click()
          await dialog(b, 'Gửi biên bản cho Chủ toạ duyệt?').getByRole('button', { name: 'Gửi duyệt', exact: true }).click()
          await qa.expect(badge(b, 'Đính chính · chờ duyệt')).toBeVisible()
          await quiet(b)
          await shotOf(qa, b, 'thu-ky-gui-dinh-chinh', 'memberB')
          // 3. A member still reads the issued original.
          await open(c)
          await qa.expect(badge(c, 'Đã ban hành')).toBeVisible()
          await qa.expect(paper(c)).not.toContainText(LINE)
          await quiet(c)
          await shotOf(qa, c, 'thanh-vien-van-doc-ban-goc', 'memberC')
          // 4. The Chair issues the correction; the member reads it, and the original is kept as superseded.
          await open(qa.page)
          await qa.expect(paper(qa.page)).toContainText(LINE)
          await btn(qa.page, 'Duyệt và ban hành').click()
          await dialog(qa.page, 'Duyệt và ban hành biên bản?').getByRole('button', { name: 'Duyệt và ban hành', exact: true }).click()
          await qa.expect(toast(qa.page, 'Đã duyệt và ban hành biên bản.')).toBeVisible()
          await quiet(qa.page)
          await qa.shot('ban-hanh-dinh-chinh')
          await open(c)
          await qa.expect(badge(c, 'Đính chính · đã ban hành').or(badge(c, 'Đã ban hành'))).toBeVisible()
          await qa.expect(paper(c)).toContainText(LINE)
          await qa.expect(versions(c)).toHaveAccessibleName(/Đính chính 1/)
          await quiet(c)
          await shotOf(qa, c, 'thanh-vien-doc-dinh-chinh', 'memberC')
          await versions(c).click()
          await c.getByRole('listbox', { name: 'Phiên bản biên bản' }).getByRole('option', { name: /Bản gốc/ }).click()
          await qa.expect(badge(c, 'Đã thay thế')).toBeVisible()
          await qa.expect(paper(c)).not.toContainText(LINE)
          await quiet(c)
          await shotOf(qa, c, 'ban-goc-da-thay-the', 'memberC')
          const notified = await (await client(qa, 'memberC')).get('/api/notifications?page=1&pageSize=50')
          const issued = (notified.body.items ?? []).filter((n) => n.meetingId === m.id && n.type === 'MinutesIssued')
          qa.note(`Original ${original}; member C MinutesIssued notifications for this meeting: ${issued.length}`)
          qa.expect(issued.length, 'member C not notified of the correction').toBeGreaterThan(1)
        } finally {
          await closeSeconds(qa)
        }
      },
    },
  ],
}

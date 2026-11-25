// Report 5.3 System Test — WF-03 Meeting Execution and Decision Making (BF-03).
// docs/08-reports/test-reports/cases/system/WF-03.json describes each case; this plan runs them on the app, in order,
// on the Published meeting WF-02 left behind ("[SYS] Họp giao ban tuần 49": Chủ toạ duc.hoang@, Thư ký lan.vu@,
// Member bao.dang@, a guest; agenda "Dự toán ngân sách năm 2027", "Báo cáo tiến độ quý IV"; bao-cao-quy-iv.pdf).

import {
  PEOPLE,
  client,
  closeSeconds,
  dialog,
  expectStatusChip,
  noRawCodes,
  noSidewaysScroll,
  person,
  secondBrowser,
  settled,
  shotOf,
  toast,
} from './sys-lib.mjs'

const TITLE = '[SYS] Họp giao ban tuần 49'
const ITEM1 = 'Dự toán ngân sách năm 2027'
const ITEM2 = 'Báo cáo tiến độ quý IV'
const VOTE = 'Thông qua dự toán ngân sách năm 2027'
const PDF = 'bao-cao-quy-iv.pdf'
const VOTE_NOTE = 'Thông qua dự toán, giao phòng Tài chính hoàn thiện hồ sơ.'
const ITEM_CONCLUSION = 'Thống nhất dự toán ngân sách năm 2027 như tờ trình.'
const state = {}

const head = (page) => page.locator('.record-head')
const attendees = (page) => page.getByRole('region', { name: 'Thành phần tham dự' })
const agenda = (page) => page.getByRole('region', { name: 'Nội dung cuộc họp' })
const agendaRow = (page, title) => agenda(page).locator('li.agenda-item').filter({ hasText: title })
const votes = (page) => page.locator('section.vote-panel')
const voteCard = (page) =>
  votes(page).locator('li.vote-item').filter({ has: page.getByRole('button', { name: VOTE, exact: true }) })

/** The meeting WF-02 published (newest first), found once per run. */
async function meeting(qa) {
  if (state.id) return state
  const list = await (await client(qa, 'organizer')).get(`/api/meetings?q=${encodeURIComponent(TITLE)}&pageSize=50`)
  const m = (list.body.items ?? []).filter((i) => i.title === TITLE && i.status === 'Published')
  qa.expect(m.length, 'no Published meeting from WF-02').toBeGreaterThan(0)
  m.sort((x, y) => (x.id < y.id ? 1 : -1))
  state.id = m[0].id
  return state
}

async function open(qa, page, tab) {
  await page.goto(`/mgmt/meetings/${state.id}${tab ? `?tab=${tab}` : ''}`)
  await page.waitForLoadState('networkidle', { timeout: 10_000 }).catch(() => undefined)
  await qa.expect(page.getByRole('heading', { level: 1, name: TITLE })).toBeVisible()
}

export default {
  ticket: 'SYS-WF03',
  title: 'WF-03 Meeting Execution and Decision Making',
  devices: ['desktop'],
  criteria: [
    {
      id: 'TC01',
      role: PEOPLE.memberA,
      title: 'Chair starts the published meeting; a member sees it start without reloading',
      async run(qa) {
        await meeting(qa)
        const c = await secondBrowser(qa, 'memberC')
        try {
          await open(qa, c)
          await qa.expect(c.locator('.record-title').getByText('Đã công bố', { exact: true })).toBeVisible()
          await c.waitForTimeout(1500)
          await open(qa, qa.page)
          await head(qa.page).getByRole('button', { name: 'Bắt đầu họp' }).click()
          const early = dialog(qa.page, 'Bắt đầu sớm hơn lịch?')
          await qa.expect(early).toBeVisible()
          await settled(qa.page)
          await qa.shot('hoi-bat-dau-som')
          await early.getByRole('button', { name: 'Vẫn bắt đầu' }).click()
          await qa.expect(toast(qa.page, 'Bắt đầu cuộc họp thành công.')).toBeVisible()
          await expectStatusChip(qa, qa.page, 'Đang diễn ra')
          await qa.expect(head(qa.page).getByRole('button', { name: 'Kết thúc họp' })).toBeVisible()
          await settled(qa.page)
          await qa.shot('dang-dien-ra')
          await qa.expect(c.locator('.record-title').getByText('Đang diễn ra', { exact: true })).toBeVisible({ timeout: 15_000 })
          await settled(c)
          await shotOf(qa, c, 'thanh-vien-thay-dang-dien-ra', 'memberC')
          const m = (await (await client(qa, 'memberA')).get(`/api/meetings/${state.id}`)).body
          qa.expect(m.status).toBe('InProgress')
          qa.expect(m.actualStartedAt).toBeTruthy()
        } finally {
          await closeSeconds(qa)
        }
      },
    },
    {
      id: 'TC02',
      role: PEOPLE.memberB,
      title: 'Secretary records attendance; a member sees their own status',
      async run(qa) {
        await meeting(qa)
        const [a, b, c] = [await person(qa, 'memberA'), await person(qa, 'memberB'), await person(qa, 'memberC')]
        await open(qa, qa.page, 'attendees')
        await qa.expect(attendees(qa.page).getByRole('columnheader', { name: 'Điểm danh' })).toHaveCount(1)
        for (const [p, value] of [
          [a, 'Có mặt'],
          [b, 'Có mặt'],
          [c, 'Có mặt'],
          [{ name: 'Trần Khách' }, 'Vắng'],
        ]) {
          const picker = attendees(qa.page).getByRole('button', { name: new RegExp(`^Điểm danh ${p.name}`) }).locator('visible=true')
          await picker.click()
          await qa.page.getByRole('option', { name: value }).click()
          await qa.expect(toast(qa.page, 'Lưu điểm danh thành công.')).toBeVisible()
          await qa.expect(picker).toHaveAccessibleName(`Điểm danh ${p.name}: ${value}`)
        }
        await settled(qa.page)
        await qa.shot('da-diem-danh')
        await noSidewaysScroll(qa)
        const page = await secondBrowser(qa, 'memberC')
        try {
          await open(qa, page, 'attendees')
          const row = attendees(page).locator('tbody tr').filter({ hasText: c.name })
          await qa.expect(row).toContainText('Có mặt')
          await qa.expect(attendees(page).getByRole('button', { name: /^Điểm danh/ })).toHaveCount(0)
          await settled(page)
          await shotOf(qa, page, 'thanh-vien-thay-co-mat', 'memberC')
        } finally {
          await closeSeconds(qa)
        }
        const att = (await (await client(qa, 'memberB')).get(`/api/meetings/${state.id}/attendance?pageSize=100`)).body
        const rows = att.items ?? att
        qa.note(`Attendance: ${rows.map((r) => `${r.fullName ?? r.name}=${r.attendance}`).join(', ')}`)
      },
    },
    {
      id: 'TC03',
      role: PEOPLE.memberA,
      title: 'Chair starts the first agenda item; participants see it under discussion',
      async run(qa) {
        await meeting(qa)
        const c = await secondBrowser(qa, 'memberC')
        try {
          await open(qa, c, 'agenda')
          await open(qa, qa.page, 'agenda')
          await agendaRow(qa.page, ITEM1).getByRole('button', { name: 'Bắt đầu' }).click()
          await qa.expect(toast(qa.page, 'Bắt đầu mục nghị sự thành công.')).toBeVisible()
          await qa.expect(agendaRow(qa.page, ITEM1).getByText('Đang bàn')).toBeVisible()
          await qa.expect(agenda(qa.page).getByRole('button', { name: 'Bắt đầu' })).toHaveCount(0)
          await settled(qa.page)
          await qa.shot('muc-1-dang-ban')
          await qa.expect(agendaRow(c, ITEM1).getByText('Đang bàn')).toBeVisible({ timeout: 15_000 })
          await settled(c)
          await shotOf(qa, c, 'thanh-vien-thay-muc-dang-ban', 'memberC')
        } finally {
          await closeSeconds(qa)
        }
      },
    },
    {
      id: 'TC04',
      role: PEOPLE.memberA,
      title: 'Chair opens the prepared vote; a member must read the attached document before voting',
      async run(qa) {
        await meeting(qa)
        // Precondition: the Secretary prepared a vote draft with the meeting PDF attached.
        const sec = await client(qa, 'memberB')
        const docs = (await sec.get(`/api/meetings/${state.id}/documents`)).body
        state.docId = docs.general.find((d) => d.fileName === PDF).id
        const created = await sec.post(`/api/meetings/${state.id}/votes`, {
          title: VOTE,
          description: 'Biểu quyết cho mục "Dự toán ngân sách năm 2027".',
          options: ['Đồng ý', 'Không đồng ý', 'Không ý kiến'],
          isAnonymous: false,
          eligibleRoles: ['Chair', 'Secretary', 'Member', 'Guest'],
          openImmediately: false,
        })
        qa.expectStatus(created, 201)
        state.voteId = created.body.voteId
        qa.expectStatus(await sec.post(`/api/meetings/${state.id}/votes/${state.voteId}/documents`, { meetingDocumentIds: [state.docId] }), 200)
        const c = await secondBrowser(qa, 'memberC')
        try {
          await open(qa, c, 'votes')
          await open(qa, qa.page, 'votes')
          await voteCard(qa.page).getByRole('button', { name: 'Mở', exact: true }).click()
          const dlg = dialog(qa.page, 'Mở biểu quyết')
          await qa.expect(dlg).toContainText('chốt ngay lúc mở')
          await settled(qa.page)
          await qa.shot('xac-nhan-mo-bieu-quyet')
          await dlg.getByRole('button', { name: 'Mở biểu quyết' }).click()
          await qa.expect(toast(qa.page, 'Mở biểu quyết thành công.')).toBeVisible()
          await qa.expect(voteCard(qa.page).locator('.vote-status')).toHaveText('Đang mở')
          await qa.expect(voteCard(qa.page).locator('.vote-turnout-text')).toHaveText('0/4')
          await qa.shot('dang-mo-0-4')
          // Member C: the open vote reaches the open tab; the document must be read first.
          const card = voteCard(c)
          await qa.expect(card.locator('.vote-status')).toHaveText('Đang mở', { timeout: 15_000 })
          await qa.expect(card.locator('.vote-docs li').filter({ hasText: PDF })).toContainText('Chưa đọc')
          await card.getByRole('radio', { name: 'Đồng ý', exact: true }).check()
          await qa.expect(card.getByRole('button', { name: 'Gửi phiếu' })).toBeDisabled()
          await qa.expect(card.getByText('Mở từng tài liệu, đọc tới trang cuối rồi xác nhận.')).toBeVisible()
          await settled(c)
          await shotOf(qa, c, 'chua-doc-khoa-gui-phieu', 'memberC')
          await card.getByRole('link', { name: new RegExp(PDF.replace('.', '\\.')) }).click()
          await qa.expect(c.getByRole('button', { name: 'Đọc tới trang cuối' })).toBeDisabled({ timeout: 20_000 })
          await c.locator('.viewer-scroll .pdf-page').first().waitFor()
          await c.locator('.viewer-scroll').evaluate((el) => el.scrollTo(0, el.scrollHeight))
          const confirm = c.getByRole('button', { name: 'Xác nhận đã đọc' })
          await qa.expect(confirm).toBeEnabled({ timeout: 20_000 })
          await confirm.click()
          await qa.expect(toast(c, 'Đã xác nhận đọc tài liệu.')).toBeVisible()
          const viewer = c.getByRole('dialog', { name: 'Xem tài liệu' })
          await viewer.getByRole('button', { name: 'Đóng', exact: true }).click()
          await qa.expect(voteCard(c).locator('.vote-docs li').filter({ hasText: PDF })).toContainText('Đã đọc')
          await settled(c)
          await shotOf(qa, c, 'da-doc-tai-lieu', 'memberC')
        } finally {
          await closeSeconds(qa)
        }
      },
    },
    {
      id: 'TC05',
      role: PEOPLE.memberA,
      title: 'Members cast their ballots; the Chair sees the turnout and closes the vote',
      async run(qa) {
        await meeting(qa)
        qa.expect(state.voteId, 'TC04 did not open the vote').toBeTruthy()
        const [b, c] = [await person(qa, 'memberB'), await person(qa, 'memberC')]
        const cp = await secondBrowser(qa, 'memberC')
        const bp = await secondBrowser(qa, 'memberB')
        try {
          await open(qa, qa.page, 'votes')
          for (const [page, who, option] of [
            [cp, 'memberC', 'Đồng ý'],
            [bp, 'memberB', 'Không ý kiến'],
          ]) {
            await open(qa, page, 'votes')
            const card = voteCard(page)
            if (who === 'memberB') {
              // The Secretary reads the document too, through the same viewer.
              await card.getByRole('link', { name: new RegExp(PDF.replace('.', '\\.')) }).click()
              await page.locator('.viewer-scroll .pdf-page').first().waitFor()
              await page.locator('.viewer-scroll').evaluate((el) => el.scrollTo(0, el.scrollHeight))
              await page.getByRole('button', { name: 'Xác nhận đã đọc' }).click()
              await qa.expect(toast(page, 'Đã xác nhận đọc tài liệu.')).toBeVisible()
              await page.getByRole('dialog', { name: 'Xem tài liệu' }).getByRole('button', { name: 'Đóng', exact: true }).click()
            }
            await voteCard(page).getByRole('radio', { name: option, exact: true }).check()
            await voteCard(page).getByRole('button', { name: 'Gửi phiếu' }).click()
            await qa.expect(toast(page, 'Đã ghi nhận phiếu của bạn.')).toBeVisible()
            await qa.expect(voteCard(page).getByText('Bạn đã gửi phiếu.')).toBeVisible()
            await settled(page)
            await shotOf(qa, page, `${who}-da-gui-phieu`, who)
          }
          const card = voteCard(qa.page)
          await qa.expect(card.locator('.vote-turnout-text')).toHaveText('2/4', { timeout: 15_000 })
          await qa.expect(card.locator('.vote-result')).toHaveCount(0)
          await qa.shot('chu-toa-thay-2-4')
          await card.getByRole('button', { name: 'Đóng phiếu' }).click()
          const dlg = dialog(qa.page, 'Đóng biểu quyết')
          await qa.expect(dlg).toContainText('Đã bỏ 2/4; đóng rồi không ai bỏ thêm được.')
          await dlg.getByRole('button', { name: 'Đóng phiếu' }).click()
          await qa.expect(toast(qa.page, 'Đã đóng biểu quyết. Kết quả đã sẵn sàng.')).toBeVisible()
          await qa.expect(card.locator('.vote-status')).toHaveText(/Đã đóng/)
          const results = card.locator('.vote-result li')
          await qa.expect(results.nth(0).locator('.vote-result-count')).toHaveText('1 · 50%')
          await qa.expect(results.nth(1).locator('.vote-result-count')).toHaveText('0 · 0%')
          await qa.expect(results.nth(2).locator('.vote-result-count')).toHaveText('1 · 50%')
          const voters = card.locator('details.vote-voters')
          await voters.locator('summary').click()
          await qa.expect(voters.locator('li').filter({ hasText: c.name })).toContainText('Đồng ý')
          await qa.expect(voters.locator('li').filter({ hasText: b.name })).toContainText('Không ý kiến')
          await card.scrollIntoViewIfNeeded()
          await settled(qa.page)
          await qa.shot('ket-qua-sau-khi-dong', { fullPage: true })
          await noRawCodes(qa)
          // The closed vote reaches the members' screens without a reload.
          await qa.expect(voteCard(cp).locator('.vote-status')).toHaveText(/Đã đóng/, { timeout: 15_000 })
          await shotOf(qa, cp, 'thanh-vien-thay-ket-qua', 'memberC')
        } finally {
          await closeSeconds(qa)
        }
      },
    },
    {
      id: 'TC06',
      role: PEOPLE.memberA,
      title: 'Chair records the vote conclusion; Secretary records the item conclusion; both items are completed',
      async run(qa) {
        await meeting(qa)
        await open(qa, qa.page, 'votes')
        const card = voteCard(qa.page)
        const form = card.locator('form.vote-conclusion-form')
        await form.getByRole('radio', { name: 'Thông qua', exact: true }).check()
        await form.getByLabel('Ghi chú').fill(VOTE_NOTE)
        await form.getByRole('button', { name: 'Ghi kết luận' }).click()
        await qa.expect(toast(qa.page, 'Ghi kết luận biểu quyết thành công.')).toBeVisible()
        await qa.expect(card.locator('.vote-box .vote-conclusion-chip')).toHaveText('Thông qua')
        await qa.expect(card.getByText(VOTE_NOTE)).toBeVisible()
        await qa.expect(card.locator('form.vote-conclusion-form')).toHaveCount(0)
        await card.scrollIntoViewIfNeeded()
        await settled(qa.page)
        await qa.shot('ket-luan-bieu-quyet')
        const b = await person(qa, 'memberB')
        const sec = await secondBrowser(qa, 'memberB')
        try {
          await open(qa, sec, 'agenda')
          const r = agendaRow(sec, ITEM1)
          await r.getByRole('button', { name: 'Ghi kết luận', exact: true }).click()
          await r.getByLabel(`Kết luận: ${ITEM1}`).fill(ITEM_CONCLUSION)
          await r.getByRole('button', { name: 'Lưu kết luận', exact: true }).click()
          await qa.expect(toast(sec, 'Lưu kết luận mục nghị sự thành công.')).toBeVisible()
          await qa.expect(r.locator('.agenda-stamp')).toContainText(b.name)
          await settled(sec)
          await shotOf(qa, sec, 'ket-luan-muc-1', 'memberB')
        } finally {
          await closeSeconds(qa)
        }
        await open(qa, qa.page, 'agenda')
        await agendaRow(qa.page, ITEM1).getByRole('button', { name: 'Xong' }).click()
        await qa.expect(toast(qa.page, 'Hoàn thành mục nghị sự thành công.')).toBeVisible()
        await qa.expect(agendaRow(qa.page, ITEM1).getByText('Đã xong')).toBeVisible()
        await agendaRow(qa.page, ITEM2).getByRole('button', { name: 'Bắt đầu' }).click()
        await qa.expect(toast(qa.page, 'Bắt đầu mục nghị sự thành công.')).toBeVisible()
        await agendaRow(qa.page, ITEM2).getByRole('button', { name: 'Xong' }).click()
        await qa.expect(toast(qa.page, 'Hoàn thành mục nghị sự thành công.')).toBeVisible()
        await qa.expect(agendaRow(qa.page, ITEM2).getByText('Đã xong')).toBeVisible()
        await settled(qa.page)
        await qa.shot('hai-muc-da-xong')
        const snap = (await (await client(qa, 'memberA')).get(`/api/meetings/${state.id}/agenda`)).body
        const items = snap.items ?? snap
        const one = items.find((i) => i.title === ITEM1)
        qa.expect(one.conclusion).toBe(ITEM_CONCLUSION)
        qa.expect(items.every((i) => i.status === 'Done'), JSON.stringify(items.map((i) => i.status))).toBe(true)
      },
    },
    {
      id: 'TC07',
      role: PEOPLE.memberA,
      title: 'Chair ends the meeting; everyone sees it ended and the results stay read-only',
      async run(qa) {
        await meeting(qa)
        const c = await secondBrowser(qa, 'memberC')
        try {
          await open(qa, c)
          await open(qa, qa.page)
          await head(qa.page).getByRole('button', { name: 'Kết thúc họp' }).click()
          const confirm = dialog(qa.page, 'Kết thúc cuộc họp?')
          await qa.expect(confirm).toBeVisible()
          await qa.shot('xac-nhan-ket-thuc')
          await confirm.getByRole('button', { name: 'Kết thúc họp' }).click()
          await qa.expect(toast(qa.page, 'Kết thúc cuộc họp thành công.')).toBeVisible()
          await expectStatusChip(qa, qa.page, 'Đã kết thúc')
          await qa.expect(head(qa.page).getByRole('button', { name: /Bắt đầu họp|Kết thúc họp/ })).toHaveCount(0)
          await settled(qa.page)
          await qa.shot('da-ket-thuc')
          await qa.expect(c.locator('.record-title').getByText('Đã kết thúc', { exact: true })).toBeVisible({ timeout: 15_000 })
          await open(qa, c, 'votes')
          await qa.expect(voteCard(c)).toContainText('Thông qua')
          await qa.expect(voteCard(c).locator('.vote-status')).toHaveText(/Đã đóng/)
          await qa.expect(voteCard(c).getByRole('radio')).toHaveCount(0)
          await settled(c)
          await shotOf(qa, c, 'thanh-vien-thay-ket-qua-chi-doc', 'memberC')
          const m = (await (await client(qa, 'memberA')).get(`/api/meetings/${state.id}`)).body
          qa.expect(m.status).toBe('Ended')
          qa.expect(m.actualEndedAt).toBeTruthy()
          const h = (await (await client(qa, 'organizer')).get(`/api/meetings/${state.id}/history`)).body.items
          qa.note(`History: ${h.map((x) => x.resultingStatus).join(' → ')}`)
        } finally {
          await closeSeconds(qa)
        }
      },
    },
  ],
}

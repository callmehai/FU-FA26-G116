// Report 5.3 System Test — WF-02 Meeting Preparation and Approval (BF-02).
// docs/08-reports/test-reports/cases/system/WF-02.json describes each case; this plan runs them on the app, in order:
// the meeting TC01 creates on the screen is the subject of every later case.
//
// Actors (ADR-0075): Organizer = thanhvien@ (Thành viên), Approver = chutoa@ (Phê duyệt viên), Member A = duc.hoang@
// (Chủ toạ of the meeting), Member B = lan.vu@ (Thư ký of the meeting), Member C = bao.dang@, and a guest on
// @trustcabinet.local (never mailed: the invitation is checked in the outbox).

import {
  PEOPLE,
  RUN,
  as,
  bell,
  closeSeconds,
  dialog,
  emailsFor,
  emailsTo,
  expectStatusChip,
  noRawCodes,
  noSidewaysScroll,
  notifRows,
  nowIso,
  openBell,
  pdfFile,
  person,
  client,
  secondBrowser,
  settled,
  shotOf,
  toast,
  unread,
} from './sys-lib.mjs'

const TITLE = '[SYS] Họp giao ban tuần 49'
const GUEST = { name: 'Trần Khách', email: `sys-guest-${RUN}@trustcabinet.local` }
const ITEM1 = 'Báo cáo tiến độ quý IV'
const ITEM2 = 'Dự toán ngân sách năm 2027'
const REASON = 'Cần bổ sung báo cáo tài chính.'
const state = {}

/** Tomorrow in Vietnam, YYYY-MM-DD. */
const tomorrow = () => new Date(Date.now() + 86_400_000 + 7 * 3_600_000).toISOString().slice(0, 10)
const vnTime = (isoText) => new Date(Date.parse(isoText) + 7 * 3_600_000).toISOString().slice(11, 16)

const head = (page) => page.locator('.record-head')
const attendees = (page) => page.getByRole('region', { name: 'Thành phần tham dự' })
const attendeeRow = (page, name) => attendees(page).locator('tbody tr').filter({ hasText: name })
const agenda = (page) => page.getByRole('region', { name: 'Nội dung cuộc họp' })
const agendaRow = (page, title) => agenda(page).locator('li.agenda-item').filter({ hasText: title })
const docs = (page) => page.locator('.documents-panel')
const docRow = (page, name) => docs(page).locator('li.documents-row').filter({ hasText: name })

async function open(qa, page, tab) {
  await page.goto(`/mgmt/meetings/${state.id}${tab ? `?tab=${tab}` : ''}`)
  await page.waitForLoadState('networkidle', { timeout: 10_000 }).catch(() => undefined)
  await qa.expect(page.getByRole('heading', { level: 1, name: TITLE })).toBeVisible()
}

async function need(qa) {
  qa.expect(state.id, 'TC01 did not create the meeting').toBeTruthy()
}

async function history(qa) {
  const res = await (await client(qa, 'organizer')).get(`/api/meetings/${state.id}/history`)
  qa.expectStatus(res, 200)
  return res.body.items
}

async function pickPresenter(qa, page, form, name) {
  await form.getByRole('button', { name: /^Người trình bày/ }).click()
  await page.getByRole('listbox', { name: 'Người trình bày' }).getByRole('option', { name }).click()
  await qa.expect(form.getByRole('button', { name: `Người trình bày: ${name}` })).toBeVisible()
}

export default {
  ticket: 'SYS-WF02',
  title: 'WF-02 Meeting Preparation and Approval',
  devices: ['desktop'],
  criteria: [
    {
      id: 'TC01',
      role: PEOPLE.organizer,
      title: 'Organizer creates a Draft meeting with a chair and a secretary',
      async run(qa) {
        const [org, a, b] = [await person(qa, 'organizer'), await person(qa, 'memberA'), await person(qa, 'memberB')]
        state.org = org
        await qa.goto('/mgmt/meetings')
        await qa.page.getByRole('link', { name: 'Tạo cuộc họp' }).first().click()
        await qa.expect(dialog(qa.page, 'Tạo cuộc họp')).toBeVisible()
        // The same dialog, opened on tomorrow (as the calendar opens it).
        await qa.goto(`/mgmt/meetings?create=${tomorrow()}`)
        const form = dialog(qa.page, 'Tạo cuộc họp')
        await form.getByPlaceholder('Ví dụ: Giao ban tuần 40').fill(TITLE)
        await form.locator('#meeting-start').fill('09:00')
        await form.locator('#meeting-start').press('Tab')
        await form.locator('#meeting-end').fill('10:00')
        await form.locator('#meeting-end').press('Tab')
        // The form starts with the organizer as Chủ toạ: take them off, add Member A and B.
        await form.getByRole('button', { name: `Bỏ ${org.name}` }).click()
        const search = form.getByRole('combobox', { name: 'Tìm người dự họp' })
        for (const p of [a, b]) {
          await search.click()
          await search.fill(p.email.split('@')[0])
          await qa.page.getByRole('option').filter({ hasText: p.email }).click()
        }
        await form.getByPlaceholder('Ví dụ: Giao ban tuần 40').click()
        for (const [p, label] of [
          [a, 'Chủ toạ'],
          [b, 'Thư ký'],
        ]) {
          await form.getByRole('button', { name: new RegExp(`^Vai trò của ${p.name}`) }).click()
          await qa.page.getByRole('listbox', { name: `Vai trò của ${p.name}` }).getByRole('option', { name: label }).click()
        }
        await qa.expect(form.getByRole('button', { name: `Vai trò của ${a.name}: Chủ toạ` })).toBeVisible()
        await qa.expect(form.getByRole('button', { name: `Vai trò của ${b.name}: Thư ký` })).toBeVisible()
        await settled(qa.page)
        await qa.shot('form-tao-cuoc-hop', { fullPage: true })
        await form.getByRole('button', { name: 'Tạo cuộc họp' }).click()
        await qa.expect(qa.page).toHaveURL(/\/mgmt\/meetings\/[0-9a-f-]{36}$/)
        await qa.expect(toast(qa.page, 'Tạo cuộc họp thành công.')).toBeVisible()
        state.id = qa.page.url().split('/').pop()
        state.createdAt = nowIso()
        await qa.page.reload()
        await qa.expect(qa.page.getByRole('heading', { level: 1, name: TITLE })).toBeVisible()
        await expectStatusChip(qa, qa.page, 'Nháp')
        await qa.expect(qa.page.getByRole('heading', { name: 'Bản nháp — chưa gửi duyệt' })).toBeVisible()
        await settled(qa.page)
        await qa.shot('chi-tiet-nhap-sau-tai-lai')
        const m = (await (await client(qa, 'organizer')).get(`/api/meetings/${state.id}`)).body
        qa.expect(m.chair?.userId).toBe(a.id)
        qa.expect(m.secretaries.map((s) => s.userId)).toEqual([b.id])
        qa.expect(vnTime(m.plannedStartTime)).toBe('09:00')
        qa.expect(vnTime(m.plannedEndTime)).toBe('10:00')
        qa.expect((await history(qa)).length).toBe(1)
        for (const who of ['memberA', 'memberB']) {
          const items = (await (await client(qa, who)).get('/api/notifications?pageSize=50')).body.items ?? []
          qa.expect(JSON.stringify(items).includes(state.id), `${who} already has a notification`).toBe(false)
        }
        qa.note(`Meeting ${state.id}`)
      },
    },
    {
      id: 'TC02',
      role: PEOPLE.memberB,
      title: 'Secretary adds Member C and invites an external guest',
      async run(qa) {
        await need(qa)
        const c = await person(qa, 'memberC')
        await open(qa, qa.page, 'attendees')
        await qa.expect(attendees(qa.page).locator('tbody tr').first()).toBeVisible()
        await attendees(qa.page).getByRole('button', { name: 'Thêm người dự họp' }).click()
        const add = dialog(qa.page, 'Thêm thành phần tham dự')
        await add.getByLabel('Người dự họp').fill(c.email)
        await add.locator('label.pick-row').filter({ hasText: c.email }).getByRole('checkbox').click()
        await qa.expect(add.getByRole('radio', { name: 'Thành viên' })).toBeChecked()
        await add.getByRole('button', { name: 'Thêm 1 người' }).click()
        await qa.expect(toast(qa.page, 'Thêm thành phần tham dự thành công.')).toBeVisible()
        await qa.expect(attendeeRow(qa.page, c.name)).toContainText('Thành viên')
        await attendees(qa.page).getByRole('button', { name: 'Mời khách ngoài hệ thống' }).click()
        const inv = dialog(qa.page, 'Mời khách ngoài hệ thống')
        await inv.getByLabel('Tên khách').fill(GUEST.name)
        await inv.getByLabel('Email').fill(GUEST.email)
        await inv.getByRole('button', { name: 'Mời' }).click()
        await qa.expect(toast(qa.page, 'Đã mời khách. Thư mời sẽ được gửi tới email của khách.')).toBeVisible()
        await qa.expect(attendeeRow(qa.page, GUEST.name)).toContainText('Khách')
        await settled(qa.page)
        await qa.shot('thanh-phan-sau-khi-them')
        await noSidewaysScroll(qa)
        qa.expect(emailsTo(GUEST.email).length, 'guest mailed before publication').toBe(0)
      },
    },
    {
      id: 'TC03',
      role: PEOPLE.memberB,
      title: 'Secretary builds a two-item agenda and reorders it',
      async run(qa) {
        await need(qa)
        const [a, c] = [await person(qa, 'memberA'), await person(qa, 'memberC')]
        await open(qa, qa.page, 'agenda')
        for (const [title, presenter] of [
          [ITEM1, a.name],
          [ITEM2, c.name],
        ]) {
          await agenda(qa.page).getByRole('button', { name: 'Thêm nội dung' }).click()
          const form = dialog(qa.page, 'Thêm nội dung')
          await form.getByPlaceholder('VD: Dự toán ngân sách năm 2027').fill(title)
          await pickPresenter(qa, qa.page, form, presenter)
          await form.getByRole('button', { name: 'Lưu' }).click()
          await qa.expect(toast(qa.page, 'Tạo mục nghị sự thành công.')).toBeVisible()
          await qa.expect(agendaRow(qa.page, title).getByText(presenter)).toBeVisible()
        }
        await settled(qa.page)
        await qa.shot('hai-muc-nghi-su')
        const grip = agendaRow(qa.page, ITEM2).getByRole('button', { name: `Kéo để đổi vị trí: ${ITEM2}` })
        const from = await grip.boundingBox()
        const to = await agendaRow(qa.page, ITEM1).boundingBox()
        await qa.page.mouse.move(from.x + from.width / 2, from.y + from.height / 2)
        await qa.page.mouse.down()
        await qa.page.mouse.move(from.x + from.width / 2, from.y + from.height / 2 - 8, { steps: 4 })
        await qa.page.mouse.move(from.x + from.width / 2, to.y + to.height * 0.2, { steps: 12 })
        await qa.page.mouse.up()
        await qa.expect(agenda(qa.page).locator('li.agenda-item .agenda-title').first()).toContainText(ITEM2)
        await qa.page.waitForTimeout(1500)
        await open(qa, qa.page, 'agenda')
        const titles = await agenda(qa.page).locator('li.agenda-item .agenda-title').allInnerTexts()
        qa.expect(titles.map((t) => t.trim().replace(/^\d+\.\s*/, ''))).toEqual([ITEM2, ITEM1])
        await settled(qa.page)
        await qa.shot('thu-tu-sau-tai-lai')
      },
    },
    {
      id: 'TC04',
      role: PEOPLE.memberB,
      title: 'Secretary uploads a PDF; another participant opens it',
      async run(qa) {
        await need(qa)
        const b = await person(qa, 'memberB')
        const pdf = pdfFile('bao-cao-quy-iv.pdf', 3)
        await open(qa, qa.page, 'documents')
        await docs(qa.page).getByRole('button', { name: 'Thêm tài liệu chung' }).click()
        const form = dialog(qa.page, 'Thêm tài liệu chung')
        await form.locator('input[type=file]').setInputFiles([pdf])
        await form.getByRole('button', { name: 'Thêm 1 tài liệu' }).click()
        await qa.expect(qa.page.getByRole('region', { name: 'Tải lên' }).getByText(/Đã tải lên 1 tệp/)).toBeVisible({ timeout: 30_000 })
        const row = docRow(qa.page, pdf.name)
        await qa.expect(row).toBeVisible()
        await qa.expect(row.getByText(/3 trang/)).toBeVisible()
        await settled(qa.page)
        await qa.shot('thu-ky-da-tai-len')
        const list = (await (await client(qa, 'memberB')).get(`/api/meetings/${state.id}/documents`)).body
        const doc = JSON.stringify(list)
        qa.expect(doc).toContain(pdf.name)
        qa.expect(doc).toContain(b.id)
        // Before publication only the organizer, the Chủ toạ and the Thư ký can open the meeting (MeetingReadPolicy).
        const other = await secondBrowser(qa, 'memberA')
        try {
          await open(qa, other, 'documents')
          await qa.expect(docRow(other, pdf.name)).toBeVisible()
          await docRow(other, pdf.name).getByRole('link', { name: pdf.name }).click()
          await qa.expect(other.getByRole('heading', { level: 1, name: pdf.name })).toBeVisible()
          await qa.expect(other.locator('.viewer-stage canvas').first()).toBeVisible({ timeout: 15_000 })
          await settled(other)
          await shotOf(qa, other, 'chu-toa-mo-pdf', 'memberA')
        } finally {
          await closeSeconds(qa)
        }
      },
    },
    {
      id: 'TC05',
      role: PEOPLE.organizer,
      title: 'Organizer submits to the Approver; the Approver is notified in real time',
      async run(qa) {
        await need(qa)
        const approver = await person(qa, 'approver')
        const ap = await secondBrowser(qa, 'approver')
        try {
          await ap.goto('/mgmt/meetings')
          await qa.expect(bell(ap)).toBeVisible()
          await ap.waitForTimeout(1500) // hub connected
          const before = await unread(ap)
          await open(qa, qa.page)
          await qa.page.getByRole('button', { name: 'Gửi duyệt', exact: true }).click()
          const form = dialog(qa.page, 'Gửi duyệt cuộc họp')
          await form.getByRole('button', { name: 'Chọn người duyệt' }).click()
          await qa.page.getByRole('option', { name: new RegExp(approver.name) }).click()
          await form.getByRole('button', { name: 'Gửi duyệt' }).click()
          await qa.expect(toast(qa.page, 'Đã gửi cuộc họp để duyệt.')).toBeVisible()
          await expectStatusChip(qa, qa.page, 'Chờ duyệt')
          await qa.expect(qa.page.getByRole('heading', { name: `Đang chờ ${approver.name} duyệt` })).toBeVisible()
          await qa.expect(head(qa.page).getByRole('link', { name: 'Chỉnh sửa' })).toHaveCount(0)
          await settled(qa.page)
          await qa.shot('da-gui-duyet')
          await qa.expect.poll(() => unread(ap), { timeout: 15_000 }).toBeGreaterThan(before)
          await openBell(qa, ap)
          await qa.expect(notifRows(ap).first()).toContainText(TITLE)
          await shotOf(qa, ap, 'nguoi-duyet-co-thong-bao', 'approver')
          await notifRows(ap).first().click()
          await qa.expect(ap).toHaveURL(new RegExp(state.id))
          await qa.expect(ap.getByRole('heading', { name: 'Cuộc họp đang chờ bạn duyệt' })).toBeVisible()
          await shotOf(qa, ap, 'mo-tu-thong-bao', 'approver')
          const h = await history(qa)
          qa.expect(h.map((x) => x.resultingStatus)).toContain('WaitingApproval')
        } finally {
          await closeSeconds(qa)
        }
      },
    },
    {
      id: 'TC06',
      role: PEOPLE.approver,
      title: 'Approver rejects with a reason; the Organizer edits and resubmits',
      async run(qa) {
        await need(qa)
        const org = await secondBrowser(qa, 'organizer')
        try {
          await org.goto('/mgmt/meetings')
          await qa.expect(bell(org)).toBeVisible()
          await org.waitForTimeout(1500)
          const before = await unread(org)
          await open(qa, qa.page)
          await qa.page.getByRole('button', { name: 'Từ chối' }).click()
          const form = dialog(qa.page, 'Từ chối cuộc họp')
          await form.getByRole('textbox').fill(REASON)
          await form.getByRole('button', { name: 'Từ chối' }).click()
          await qa.expect(toast(qa.page, 'Đã từ chối cuộc họp.')).toBeVisible()
          await expectStatusChip(qa, qa.page, 'Bị từ chối')
          await settled(qa.page)
          await qa.shot('da-tu-choi')
          // Organizer side.
          await qa.expect.poll(() => unread(org), { timeout: 15_000 }).toBeGreaterThan(before)
          await openBell(qa, org)
          await qa.expect(notifRows(org).first()).toContainText(TITLE)
          await shotOf(qa, org, 'thong-bao-bi-tu-choi', 'organizer')
          await notifRows(org).first().click()
          await qa.expect(org.getByRole('region', { name: 'Cuộc họp bị từ chối' }).getByText(REASON)).toBeVisible()
          await shotOf(qa, org, 'thanh-bi-tu-choi-co-ly-do', 'organizer')
          await head(org).getByRole('link', { name: 'Chỉnh sửa' }).click()
          const edit = dialog(org, 'Chỉnh sửa cuộc họp')
          await edit.locator('#meeting-end').fill('10:30')
          await edit.locator('#meeting-end').press('Tab')
          await edit.getByRole('button', { name: 'Lưu thay đổi' }).click()
          await qa.expect(toast(org, 'Cập nhật cuộc họp thành công.')).toBeVisible()
          const apBefore = (await (await client(qa, 'approver')).get('/api/notifications?pageSize=1')).body
          await org.getByRole('button', { name: 'Gửi duyệt lại' }).click()
          const resend = dialog(org, 'Gửi duyệt lại cuộc họp')
          await resend.getByRole('button', { name: 'Gửi duyệt lại' }).click()
          await qa.expect(toast(org, 'Đã gửi cuộc họp để duyệt.')).toBeVisible()
          await qa.expect(org.locator('.record-title').getByText('Chờ duyệt', { exact: true })).toBeVisible()
          await settled(org)
          await shotOf(qa, org, 'da-sua-va-gui-lai', 'organizer')
          const m = (await (await client(qa, 'organizer')).get(`/api/meetings/${state.id}`)).body
          qa.expect(vnTime(m.plannedEndTime)).toBe('10:30')
          const apAfter = (await (await client(qa, 'approver')).get('/api/notifications?pageSize=1')).body
          qa.expect(apAfter.unreadCount ?? apAfter.items?.[0]?.id, 'approver has a new notification').not.toEqual(
            apBefore.unreadCount ?? apBefore.items?.[0]?.id,
          )
          const h = (await history(qa)).map((x) => x.resultingStatus)
          qa.note(`History: ${h.join(' → ')}`)
          const tail = h.slice(-3)
          qa.expect(tail).toEqual(['WaitingApproval', 'Rejected', 'WaitingApproval'])
          const rejected = (await history(qa)).find((x) => x.resultingStatus === 'Rejected')
          qa.expect(JSON.stringify(rejected)).toContain(REASON)
        } finally {
          await closeSeconds(qa)
        }
      },
    },
    {
      id: 'TC07',
      role: PEOPLE.approver,
      title: 'Approver approves; Organizer publishes; participants and the guest are invited',
      async run(qa) {
        await need(qa)
        const since = nowIso()
        await open(qa, qa.page)
        await qa.page.getByRole('button', { name: 'Duyệt', exact: true }).click()
        await dialog(qa.page, 'Duyệt cuộc họp').getByRole('button', { name: 'Duyệt', exact: true }).click()
        await qa.expect(toast(qa.page, 'Đã duyệt cuộc họp.')).toBeVisible()
        await expectStatusChip(qa, qa.page, 'Đã duyệt')
        await settled(qa.page)
        await qa.shot('da-duyet')
        const org = await secondBrowser(qa, 'organizer')
        const c = await secondBrowser(qa, 'memberC')
        try {
          const orgNotes = (await (await client(qa, 'organizer')).get('/api/notifications?pageSize=5')).body.items
          qa.expect(JSON.stringify(orgNotes[0])).toContain(state.id)
          await open(qa, org)
          await org.getByRole('button', { name: 'Công bố', exact: true }).click()
          await dialog(org, 'Công bố cuộc họp').getByRole('button', { name: 'Công bố', exact: true }).click()
          await qa.expect(toast(org, 'Đã công bố cuộc họp.')).toBeVisible()
          await qa.expect(org.locator('.record-title').getByText('Đã công bố', { exact: true })).toBeVisible()
          await qa.expect(head(org).getByRole('link', { name: 'Chỉnh sửa' })).toHaveCount(0)
          await qa.expect(head(org).getByRole('link', { name: 'Dời lịch' })).toBeVisible()
          await settled(org)
          await shotOf(qa, org, 'da-cong-bo', 'organizer')
          // Member C: list, bell, calendar.
          await c.goto(`/mgmt/meetings?q=${encodeURIComponent(TITLE)}`)
          await qa.expect(c.getByRole('link', { name: TITLE }).first()).toBeVisible()
          await openBell(qa, c)
          await qa.expect(notifRows(c).filter({ hasText: TITLE }).first()).toBeVisible()
          await shotOf(qa, c, 'thanh-vien-c-co-loi-moi', 'memberC')
          await c.goto('/calendar')
          const event = c.locator('main').getByText(TITLE).first()
          if (!(await event.isVisible().catch(() => false))) {
            // On a Sunday the working week shown is the one just ended: step to the next week.
            await c.getByRole('button', { name: /^Tuần sau$/ }).click()
          }
          await qa.expect(c.locator('main').getByText(TITLE).first()).toBeVisible({ timeout: 10_000 })
          await settled(c)
          await shotOf(qa, c, 'lich-co-cuoc-hop', 'memberC')
          for (const who of ['memberA', 'memberB', 'memberC']) {
            const items = (await (await client(qa, who)).get('/api/notifications?pageSize=20')).body.items
            qa.expect(JSON.stringify(items).includes(state.id), `${who} has no invitation notification`).toBe(true)
          }
          await new Promise((r) => setTimeout(r, 6000)) // email outbox worker polls every 5 s
          const mails = emailsFor(state.id, since)
          const to = mails.map((m) => m.to)
          qa.note(`Emails: ${mails.map((m) => `${m.to}: ${m.subject}`).join(' | ')}`)
          for (const who of ['memberA', 'memberB', 'memberC']) qa.expect(to).toContain(PEOPLE[who])
          const guestMail = mails.find((m) => m.to === GUEST.email)
          qa.expect(guestMail, 'no invitation email to the guest').toBeTruthy()
          qa.expect(guestMail.guestLink, 'guest email has no /guest/ link').toBe(true)
          const m = (await (await client(qa, 'organizer')).get(`/api/meetings/${state.id}`)).body
          qa.expect(`${vnTime(m.plannedStartTime)}-${vnTime(m.plannedEndTime)}`).toBe('09:00-10:30')
        } finally {
          await closeSeconds(qa)
        }
      },
    },
    {
      id: 'TC08',
      role: PEOPLE.memberC,
      title: 'Members answer the invitation; the Organizer sees the answers',
      async run(qa) {
        await need(qa)
        const [a, b, c] = [await person(qa, 'memberA'), await person(qa, 'memberB'), await person(qa, 'memberC')]
        await open(qa, qa.page)
        const ask = qa.page.getByRole('region', { name: 'Bạn có tham dự cuộc họp này?' })
        await qa.expect(ask).toBeVisible()
        await settled(qa.page)
        await qa.shot('hoi-tham-du')
        await ask.getByRole('button', { name: 'Tham dự', exact: true }).click()
        await qa.expect(toast(qa.page, 'Đã ghi nhận phản hồi lời mời.')).toBeVisible()
        await qa.expect(ask).toHaveCount(0)
        await qa.page.reload()
        await qa.expect(qa.page.getByRole('heading', { level: 1, name: TITLE })).toBeVisible()
        await qa.expect(qa.page.getByRole('region', { name: 'Bạn có tham dự cuộc họp này?' })).toHaveCount(0)
        await qa.shot('sau-tai-lai-khong-hoi-lai')
        const chair = await secondBrowser(qa, 'memberA')
        const phone = await secondBrowser(qa, 'memberB', { phone: true })
        try {
          await open(qa, chair)
          await qa.expect(chair.getByRole('region', { name: 'Bạn có tham dự cuộc họp này?' })).toHaveCount(0)
          await shotOf(qa, chair, 'chu-toa-khong-duoc-hoi', 'memberA')
          await open(qa, phone)
          const askA = phone.getByRole('region', { name: 'Bạn có tham dự cuộc họp này?' })
          await qa.expect(askA).toBeVisible()
          await askA.scrollIntoViewIfNeeded()
          await noSidewaysScroll(qa, phone)
          const box = await askA.getByRole('button', { name: 'Tham dự', exact: true }).boundingBox()
          qa.expect(box.x + box.width, 'button off screen at 390px').toBeLessThanOrEqual(390)
          await settled(phone)
          await shotOf(qa, phone, 'dien-thoai-hoi-tham-du', 'memberB')
          await askA.getByRole('button', { name: 'Tham dự', exact: true }).tap()
          await qa.expect(toast(phone, 'Đã ghi nhận phản hồi lời mời.')).toBeVisible()
          await shotOf(qa, phone, 'dien-thoai-da-tra-loi', 'memberB')
          const org = await secondBrowser(qa, 'organizer')
          await open(qa, org, 'attendees')
          await qa.expect(attendeeRow(org, b.name)).toContainText('Đã xác nhận tham dự')
          await qa.expect(attendeeRow(org, c.name)).toContainText('Đã xác nhận tham dự')
          await qa.expect(attendeeRow(org, a.name)).not.toContainText('Đã xác nhận tham dự')
          await settled(org)
          await shotOf(qa, org, 'nguoi-to-chuc-thay-phan-hoi', 'organizer')
        } finally {
          await closeSeconds(qa)
        }
      },
    },
  ],
  state,
}

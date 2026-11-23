// Report 5.3 System Test — WF-09 Notifications and Calendar (BF-02).
// docs/08-reports/test-reports/cases/system/WF-09.json describes each case. A Draft meeting is made here (precondition),
// organised by thanhvien@ with Chủ toạ duc.hoang@, Thư ký lan.vu@, Member bao.dang@, three days ahead at 17:00 (an hour no other test meeting uses, so the week grid does not fold it);
// the steps under test (submit, approve, publish) run on the screen while the receivers have the app open.

import {
  PEOPLE,
  RUN,
  bell,
  client,
  closeSeconds,
  dialog,
  meetingAt,
  notifRows,
  nowIso,
  openBell,
  person,
  secondBrowser,
  settled,
  shotOf,
  slot,
  sql,
  toast,
  unread,
} from './sys-lib.mjs'

const state = {}
const head = (page) => page.locator('.record-head')

async function setup(qa) {
  if (state.m) return
  // Earlier runs left their WF-09 meeting at the same hour; the week grid would fold them together. Cancel them first
  // (a cancelled meeting is hidden from the calendar by default).
  const org = await client(qa, 'organizer')
  const old = (await org.get(`/api/meetings?q=${encodeURIComponent('[SYS] WF-09 Họp thông báo')}&pageSize=100`)).body.items ?? []
  for (const m of old.filter((x) => ['Draft', 'Published', 'Approved', 'WaitingApproval', 'Rejected'].includes(x.status)))
    await org.post(`/api/meetings/${m.id}/transitions`, { action: 'Cancel', reason: 'Dọn dữ liệu kiểm thử hệ thống WF-09.' })
  state.m = await meetingAt(qa, 'Draft', { title: `[SYS] WF-09 Họp thông báo ${RUN}`, over: slot(3, 17) })
}

async function open(qa, page) {
  await page.goto(`/mgmt/meetings/${state.m.id}`)
  await qa.expect(page.getByRole('heading', { level: 1, name: state.m.title })).toBeVisible()
}

/** Waits until the bell's unread count rises above `before`; returns the milliseconds it took from `t0`. */
async function bellRises(qa, page, before, t0) {
  await qa.expect.poll(() => unread(page), { timeout: 20_000, intervals: [100] }).toBeGreaterThan(before)
  return Date.now() - t0
}

export default {
  ticket: 'SYS-WF09',
  title: 'WF-09 Notifications and Calendar',
  devices: ['desktop'],
  criteria: [
    {
      id: 'TC01',
      role: PEOPLE.organizer,
      title: 'Submit, approve and publish reach the right people on the bell in real time',
      async run(qa) {
        await setup(qa)
        const approver = await person(qa, 'approver')
        const ap = await secondBrowser(qa, 'approver')
        const c = await secondBrowser(qa, 'memberC')
        try {
          await ap.goto('/calendar')
          await c.goto('/calendar')
          await qa.expect(bell(ap)).toBeVisible()
          await qa.expect(bell(c)).toBeVisible()
          await ap.waitForTimeout(2000)
          // Submit → the Approver.
          let before = await unread(ap)
          const cBefore = await unread(c)
          await open(qa, qa.page)
          await qa.page.getByRole('button', { name: 'Gửi duyệt', exact: true }).click()
          const form = dialog(qa.page, 'Gửi duyệt cuộc họp')
          await form.getByRole('button', { name: 'Chọn người duyệt' }).click()
          await qa.page.getByRole('option', { name: new RegExp(approver.name) }).click()
          let t0 = Date.now()
          await form.getByRole('button', { name: 'Gửi duyệt' }).click()
          const submitMs = await bellRises(qa, ap, before, t0)
          await qa.expect(toast(qa.page, 'Đã gửi cuộc họp để duyệt.')).toBeVisible()
          await openBell(qa, ap)
          await qa.expect(notifRows(ap).first()).toContainText(state.m.title)
          await shotOf(qa, ap, 'nguoi-duyet-nhan-ngay', 'approver')
          // Approve → the Organizer.
          before = await unread(qa.page)
          await notifRows(ap).first().click()
          await qa.expect(ap.getByRole('heading', { name: 'Cuộc họp đang chờ bạn duyệt' })).toBeVisible()
          await ap.getByRole('button', { name: 'Duyệt', exact: true }).click()
          t0 = Date.now()
          await dialog(ap, 'Duyệt cuộc họp').getByRole('button', { name: 'Duyệt', exact: true }).click()
          const approveMs = await bellRises(qa, qa.page, before, t0)
          await openBell(qa, qa.page)
          await qa.expect(notifRows(qa.page).first()).toContainText(state.m.title)
          await qa.shot('nguoi-to-chuc-nhan-da-duyet')
          await qa.page.keyboard.press('Escape')
          // Publish → the participants; the member did not hear of the meeting before.
          qa.expect(await unread(c), 'member told before publication').toBe(cBefore)
          await open(qa, qa.page)
          await qa.page.getByRole('button', { name: 'Công bố', exact: true }).click()
          t0 = Date.now()
          await dialog(qa.page, 'Công bố cuộc họp').getByRole('button', { name: 'Công bố', exact: true }).click()
          state.publishedAt = nowIso()
          const publishMs = await bellRises(qa, c, cBefore, t0)
          await openBell(qa, c)
          await qa.expect(notifRows(c).first()).toContainText(state.m.title)
          await shotOf(qa, c, 'thanh-vien-nhan-loi-moi', 'memberC')
          state.latency = { submitMs, approveMs, publishMs }
          qa.note(`Bell latency: submit→approver ${submitMs} ms, approve→organizer ${approveMs} ms, publish→member ${publishMs} ms`)
          for (const who of ['organizer', 'approver'])
            qa.expect(
              ((await (await client(qa, who)).get('/api/notifications?pageSize=50')).body.items ?? []).filter(
                (n) => n.meetingId === state.m.id && n.type === 'MeetingInvited',
              ).length,
              `${who} got an invitation`,
            ).toBe(0)
        } finally {
          await closeSeconds(qa)
        }
      },
    },
    {
      id: 'TC02',
      role: PEOPLE.memberC,
      title: 'Each participant gets an invitation email with the meeting link and a calendar file',
      async run(qa) {
        await setup(qa)
        await new Promise((r) => setTimeout(r, 6000))
        const rows = sql(
          `select to_address, subject, (calendar_attachment is not null)::text, (position('/mgmt/meetings/${state.m.id}' in coalesce(html_body, body)) > 0)::text, status, left(last_error, 90) from email_messages where meeting_id = '${state.m.id}' order by to_address`,
        )
        qa.note(`Outbox: ${rows.map((r) => `${r[0]} | ${r[1]} | ics=${r[2]} | link=${r[3]} | ${r[4]} ${r[5]}`).join(' ;; ')}`)
        const by = Object.fromEntries(rows.map((r) => [r[0], r]))
        for (const who of ['memberA', 'memberB', 'memberC']) {
          const r = by[PEOPLE[who]]
          qa.expect(r, `no email to ${PEOPLE[who]}`).toBeTruthy()
          qa.expect(r[1]).toBe(`Lời mời tham dự cuộc họp: ${state.m.title}`)
          qa.expect(r[2], 'no calendar file').toBe('true')
          qa.expect(r[3], 'no meeting link').toBe('true')
        }
        qa.expect(by[PEOPLE.organizer], 'organizer mailed an invitation').toBeFalsy()
        // The tester reads the HTML email as the member would.
        const html = sql(`select html_body from email_messages where meeting_id = '${state.m.id}' and to_address = '${PEOPLE.memberC}'`)[0][0]
        await qa.page.setContent(html)
        await qa.expect(qa.page.getByText(state.m.title).first()).toBeVisible()
        await qa.shot('email-loi-moi-cua-thanh-vien', { fullPage: true })
      },
    },
    {
      id: 'TC03',
      role: PEOPLE.memberC,
      title: 'The calendar shows the meeting in the week and the month, and the list gives each meeting its status',
      async run(qa) {
        await setup(qa)
        await qa.goto('/calendar')
        // The meeting is the day after tomorrow: this week, or next week when that day falls past Friday.
        const day = new Date(Date.parse(state.m.plannedStartTime) + 7 * 3_600_000).toISOString().slice(0, 10)
        await qa.goto(`/calendar?week=${day}`)
        const ev = qa.page.locator('main').getByText(state.m.title).first()
        await qa.expect(ev).toBeAttached({ timeout: 15_000 })
        await ev.scrollIntoViewIfNeeded()
        await settled(qa.page)
        await qa.shot('lich-tuan')
        await qa.page.getByRole('button', { name: /Kiểu lịch/ }).click()
        await qa.page.getByRole('listbox', { name: 'Kiểu lịch' }).getByRole('option', { name: 'Tháng' }).click()
        await qa.expect(qa.page).toHaveURL(/month|thang|span=/)
        await qa.expect(qa.page.locator('main').getByText(state.m.title).first()).toBeVisible({ timeout: 10_000 })
        await settled(qa.page)
        await qa.shot('lich-thang')
        await qa.page.getByRole('button', { name: 'Danh sách', exact: true }).click()
        const rowText = await qa.page
          .locator('main')
          .getByText(state.m.title)
          .first()
          .evaluate((el) => {
            let n = el
            // Walk up to the list row: the element that holds the time and the status chip.
            for (let i = 0; i < 8 && n.parentElement; i++) {
              n = n.parentElement
              if (/\d{2}:\d{2}/.test(n.innerText) && /Đã công bố|Chờ duyệt|Đã duyệt|Đang diễn ra|Đã kết thúc|Đã huỷ/.test(n.innerText)) return n.innerText
            }
            return ''
          })
        qa.note(`List row: ${rowText.replace(/\s+/g, ' ').slice(0, 160)}`)
        qa.expect(rowText).toContain('17:')
        qa.expect(rowText).toContain('Đã công bố')
        await settled(qa.page)
        await qa.shot('lich-danh-sach-trang-thai', { fullPage: true })
      },
    },
  ],
}

// Report 5.3 System Test — WF-07 Changes Outside the Main Flow (BF-02).
// docs/08-reports/test-reports/cases/system/WF-07.json describes each case. Two Published meetings are made here
// (precondition), organised by thanhvien@ with Chủ toạ duc.hoang@, Thư ký lan.vu@ and Member bao.dang@.

import {
  PEOPLE,
  RUN,
  client,
  closeSeconds,
  dialog,
  emailsFor,
  expectStatusChip,
  meetingAt,
  noSidewaysScroll,
  nowIso,
  person,
  secondBrowser,
  settled,
  shotOf,
  sql,
  toast,
} from './sys-lib.mjs'

const state = {}
const head = (page) => page.locator('.record-head')
const attendees = (page) => page.getByRole('region', { name: 'Thành phần tham dự' })
const row = (page, name) => attendees(page).locator('tbody tr').filter({ hasText: name })
const vnTime = (isoText) => new Date(Date.parse(isoText) + 7 * 3_600_000).toISOString().slice(11, 16)

async function setup(qa) {
  if (state.m1) return
  state.m1 = await meetingAt(qa, 'Published', { title: `[SYS] WF-07 Họp giao ban A ${RUN}` })
  state.m2 = await meetingAt(qa, 'Published', { title: `[SYS] WF-07 Họp giao ban B ${RUN}` })
}

async function open(qa, page, m, tab) {
  await page.goto(`/mgmt/meetings/${m.id}${tab ? `?tab=${tab}` : ''}`)
  await page.waitForLoadState('networkidle', { timeout: 10_000 }).catch(() => undefined)
  await qa.expect(page.getByRole('heading', { level: 1, name: m.title })).toBeVisible()
}

async function notesOf(qa, who, meetingId) {
  const items = (await (await client(qa, who)).get('/api/notifications?page=1&pageSize=50')).body.items ?? []
  return items.filter((n) => n.meetingId === meetingId)
}

/**
 * The change emails: each invitee should get one whose subject names the change. Reads the outbox and the invitation
 * dispatch queue (where a change email waits before it is written to the outbox), and reports both.
 */
async function mailsAbout(qa, m, since, subject) {
  await new Promise((r) => setTimeout(r, 8000))
  const mails = emailsFor(m.id, since).filter((x) => subject.test(x.subject))
  const queue = sql(
    `select event_kind, state, left(last_error, 80), count(*) from meeting_invitation_dispatches where meeting_id = '${m.id}' group by 1, 2, 3`,
  )
  qa.note(`Change emails: ${mails.map((x) => `${x.to}: ${x.subject}`).join(' | ') || 'none'} · Dispatch queue: ${queue.map((r) => r.join(' ')).join(' | ')}`)
  for (const who of ['memberA', 'memberB', 'memberC'])
    qa.expect(mails.map((x) => x.to), `no change email to ${PEOPLE[who]}`).toContain(PEOPLE[who])
}

export default {
  ticket: 'SYS-WF07',
  title: 'WF-07 Changes Outside the Main Flow',
  devices: ['desktop'],
  criteria: [
    {
      id: 'TC01',
      role: PEOPLE.organizer,
      title: 'Organizer reschedules a published meeting; invitees are told by bell and email',
      async run(qa) {
        await setup(qa)
        const m = state.m1
        const since = nowIso()
        await open(qa, qa.page, m)
        await head(qa.page).getByRole('link', { name: 'Dời lịch' }).click()
        const form = dialog(qa.page, 'Dời lịch cuộc họp')
        await qa.expect(form.getByPlaceholder('Ví dụ: Giao ban tuần 40')).toBeDisabled()
        await form.locator('#meeting-start').fill('14:00')
        await form.locator('#meeting-start').press('Tab')
        await form.locator('#meeting-end').fill('15:30')
        await form.locator('#meeting-end').press('Tab')
        await form.getByPlaceholder('Ví dụ: Phòng họp A — Tầng 4').fill('Phòng họp số 2 — Tầng 3')
        await settled(qa.page)
        await qa.shot('form-doi-lich', { fullPage: true })
        await form.getByRole('button', { name: 'Lưu và thông báo' }).click()
        await qa.expect(toast(qa.page, 'Đã dời lịch cuộc họp. Người được mời sẽ nhận thông báo.')).toBeVisible()
        await expectStatusChip(qa, qa.page, 'Đã công bố')
        await qa.expect(qa.page.locator('.record-facts').getByText('Phòng họp số 2 — Tầng 3')).toBeVisible()
        await settled(qa.page)
        await qa.shot('da-doi-lich')
        const got = (await (await client(qa, 'organizer')).get(`/api/meetings/${m.id}`)).body
        qa.expect(`${vnTime(got.plannedStartTime)}-${vnTime(got.plannedEndTime)}`).toBe('14:00-15:30')
        for (const who of ['memberA', 'memberB', 'memberC'])
          qa.expect((await notesOf(qa, who, m.id)).some((n) => n.type === 'MeetingRescheduled'), `${who} not told`).toBe(true)
        qa.expect((await notesOf(qa, 'organizer', m.id)).some((n) => n.type === 'MeetingRescheduled'), 'organizer told of own change').toBe(false)
        const c = await secondBrowser(qa, 'memberC')
        try {
          // Many test meetings share this slot, so the week grid folds them; the calendar's list view shows each one.
          await c.goto('/calendar')
          await c.getByRole('button', { name: 'Danh sách', exact: true }).click()
          const item = c.locator('main').getByText(m.title).first()
          const shown = await item.waitFor({ timeout: 6000 }).then(() => true, () => false)
          if (!shown) await c.getByRole('button', { name: /sau$/ }).first().click()
          await qa.expect(item).toBeVisible({ timeout: 10_000 })
          await item.scrollIntoViewIfNeeded()
          // The row of the list that holds this meeting shows its new time.
          const rowText = await item.evaluate((el) => {
            let n = el
            for (let i = 0; i < 6 && n.parentElement; i++) {
              n = n.parentElement
              if (/\d{2}:\d{2}/.test(n.innerText)) return n.innerText
            }
            return ''
          })
          qa.note(`Calendar list row: ${rowText.replace(/\s+/g, ' ').slice(0, 160)}`)
          qa.expect(rowText).toContain('14:00')
          await settled(c)
          await shotOf(qa, c, 'lich-gio-moi', 'memberC')
        } finally {
          await closeSeconds(qa)
        }
        await mailsAbout(qa, m, since, /^Cuộc họp đã dời lịch/)
      },
    },
    {
      id: 'TC02',
      role: PEOPLE.memberA,
      title: 'The Chair hands the chair over to a member of the published meeting',
      async run(qa) {
        await setup(qa)
        const m = state.m1
        const [a, c] = [await person(qa, 'memberA'), await person(qa, 'memberC')]
        await open(qa, qa.page, m, 'attendees')
        await qa.expect(attendees(qa.page).locator('tbody tr').first()).toBeVisible()
        await row(qa.page, c.name).getByRole('button', { name: `Đổi vai trò của ${c.name}` }).click()
        const d = dialog(qa.page, 'Đổi vai trò')
        await d.getByRole('radio', { name: 'Chủ toạ' }).check()
        await qa.expect(d.getByText('Bạn sẽ thành Thành viên và không còn điều hành cuộc họp.')).toBeVisible()
        await qa.shot('chon-chu-toa-moi')
        await d.getByRole('button', { name: 'Giao Chủ toạ' }).click()
        const confirm = dialog(qa.page, `Giao Chủ toạ cho ${c.name}?`)
        await qa.expect(confirm).toBeVisible()
        await confirm.getByRole('button', { name: 'Giao Chủ toạ' }).click()
        await qa.expect(toast(qa.page, 'Giao vai trò Chủ toạ thành công.')).toBeVisible()
        await qa.expect(row(qa.page, c.name)).toContainText('Chủ toạ')
        await qa.expect(row(qa.page, a.name)).toContainText('Thành viên')
        await settled(qa.page)
        await qa.shot('da-giao-chu-toa')
        await noSidewaysScroll(qa)
        const got = (await (await client(qa, 'organizer')).get(`/api/meetings/${m.id}`)).body
        qa.expect(got.chair?.userId).toBe(c.id)
        const types = (await notesOf(qa, 'memberC', m.id)).map((n) => n.type)
        qa.note(`Member C notifications: ${types.join(', ')}`)
        const page = await secondBrowser(qa, 'memberC')
        try {
          await open(qa, page, m)
          await qa.expect(page.locator('.record-head').getByRole('button', { name: 'Bắt đầu họp' })).toBeVisible()
          await settled(page)
          await shotOf(qa, page, 'chu-toa-moi-co-nut-bat-dau', 'memberC')
        } finally {
          await closeSeconds(qa)
        }
      },
    },
    {
      id: 'TC03',
      role: PEOPLE.organizer,
      title: 'Organizer cancels another published meeting with a reason; invitees still see it as cancelled',
      async run(qa) {
        await setup(qa)
        const m = state.m2
        const since = nowIso()
        const reason = 'Lãnh đạo đi công tác đột xuất, sẽ xếp lịch sau.'
        await open(qa, qa.page, m)
        await head(qa.page).getByRole('button', { name: 'Huỷ cuộc họp' }).click()
        const form = dialog(qa.page, 'Huỷ cuộc họp?')
        await qa.expect(form.getByText('Mọi người được mời sẽ nhận thông báo và email kèm lý do.', { exact: false })).toBeVisible()
        await form.getByRole('button', { name: 'Huỷ cuộc họp' }).click()
        await qa.expect(form.getByText('Cần nhập lý do huỷ cuộc họp.')).toBeVisible()
        await form.getByRole('textbox').fill(reason)
        await qa.shot('hoi-huy-co-ly-do')
        await form.getByRole('button', { name: 'Huỷ cuộc họp' }).click()
        await qa.expect(toast(qa.page, 'Đã huỷ cuộc họp.')).toBeVisible()
        await expectStatusChip(qa, qa.page, 'Đã huỷ')
        await qa.expect(qa.page.getByRole('region', { name: 'Cuộc họp đã huỷ' }).getByText(reason)).toBeVisible()
        await qa.expect(head(qa.page).getByRole('link', { name: /Dời lịch|Chỉnh sửa/ })).toHaveCount(0)
        await settled(qa.page)
        await qa.shot('da-huy')
        for (const who of ['memberA', 'memberB', 'memberC'])
          qa.expect((await notesOf(qa, who, m.id)).some((n) => n.type === 'MeetingCancelled'), `${who} not told`).toBe(true)
        const c = await secondBrowser(qa, 'memberC')
        try {
          await open(qa, c, m)
          await qa.expect(c.locator('.record-title').getByText('Đã huỷ', { exact: true })).toBeVisible()
          await qa.expect(c.getByRole('region', { name: 'Cuộc họp đã huỷ' })).toContainText(reason)
          await settled(c)
          await shotOf(qa, c, 'thanh-vien-thay-da-huy', 'memberC')
        } finally {
          await closeSeconds(qa)
        }
        await mailsAbout(qa, m, since, /^Cuộc họp đã bị huỷ/)
      },
    },
  ],
}

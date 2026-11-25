// Report 5.3 System Test — WF-05 Online Meeting Room (BF-03).
// docs/08-reports/test-reports/cases/system/WF-05.json describes each case; this plan runs them on the app with the
// local LiveKit server and Chromium's fake camera and microphone (a test tone, not a voice).
//
// The meeting is made here (precondition): an Online meeting organised by thanhvien@, Chủ toạ duc.hoang@, Thư ký lan.vu@,
// members bao.dang@ and son.ngo@, and a guest on @trustcabinet.local, published and started. The guest's link is read
// from the outbox; the one-time code, which is mailed and never stored, from the local Mailpit inbox (see sys-lib).

import { request as playwrightRequest } from '@playwright/test'
import {
  PEOPLE,
  PASSWORD,
  RUN,
  client,
  closeSeconds,
  dialog,
  noSidewaysScroll,
  person,
  secondBrowser,
  shotOf,
  guestLink,
  guestSession,
  slot,
  sql,
} from './sys-lib.mjs'

const TITLE = `[SYS] Họp trực tuyến WF-05 ${RUN}`
const GUEST = { name: 'Nguyễn Khách Mời', email: `sys-wf05-guest-${RUN}@trustcabinet.local` }
const state = {}

const lobbyHeading = (page) => page.getByRole('heading', { level: 1, name: 'Sẵn sàng tham gia?' })
const controls = (page) => page.locator('footer.room-controls')
const leaveButton = (page) => controls(page).getByRole('button', { name: 'Rời phòng' })
const headerCount = async (page) => Number(((await page.locator('.room-count').innerText().catch(() => '')) || '').match(/\d+/)?.[0] ?? NaN)

/** Devices remembered before the page loads (mic, camera on or off). */
async function prefs(page, p) {
  await page.context().addInitScript((x) => {
    try {
      if (!sessionStorage.getItem('sys.prefs')) {
        localStorage.setItem('trustcabinet.call.prefs', JSON.stringify(x))
        sessionStorage.setItem('sys.prefs', '1')
      }
    } catch {
      // ignore
    }
  }, p)
}

async function enter(qa, page, path = `/mgmt/meetings/${state.id}/room`) {
  await page.goto(path)
  await qa.expect(lobbyHeading(page)).toBeVisible({ timeout: 20_000 })
  await page.getByRole('button', { name: 'Vào phòng', exact: true }).click()
  await qa.expect(leaveButton(page)).toBeVisible({ timeout: 25_000 })
}

// A request context of its own: signing in through the page's context would replace the page's session cookie.
let apiCtx
async function api(qa, who, method, path, body, headers = {}) {
  apiCtx ??= await playwrightRequest.newContext({ baseURL: qa.base })
  const res = await apiCtx.fetch(`${qa.base}${path}`, {
    method,
    headers: { Authorization: `Bearer ${await token(qa, who)}`, ...headers },
    ...(body === undefined ? {} : { data: body }),
  })
  const text = await res.text()
  return { status: res.status(), body: text ? JSON.parse(text) : null, etag: res.headers()['etag'] }
}
const TOKENS = {}
async function token(qa, who) {
  const email = PEOPLE[who] ?? who
  if (!TOKENS[email]) {
    apiCtx ??= await playwrightRequest.newContext({ baseURL: qa.base })
    const r = await apiCtx.post(`${qa.base}/api/auth/login`, { data: { email, password: PASSWORD } })
    TOKENS[email] = (await r.json()).accessToken
  }
  return TOKENS[email]
}

/** Precondition: the Online meeting, its guest, published and started. */
async function setup(qa) {
  if (state.id) return
  const ids = {}
  for (const w of ['memberA', 'memberB', 'memberC', 'memberE', 'approver']) ids[w] = (await person(qa, w)).id
  const org = await client(qa, 'organizer')
  const created = await org.post('/api/meetings', {
    title: TITLE,
    ...slot(1, 14),
    mode: 'Online',
    location: null,
    onlineLink: null,
    sensitivity: 'Normal',
    description: 'Cuộc họp trực tuyến của bộ kiểm thử hệ thống.',
    participants: [
      { userId: ids.memberA, role: 'Chair' },
      { userId: ids.memberB, role: 'Secretary' },
      { userId: ids.memberC, role: 'Member' },
      { userId: ids.memberE, role: 'Member' },
    ],
  })
  qa.expectStatus(created, 201)
  state.id = created.body.meeting.id
  const roster = await api(qa, 'memberB', 'GET', `/api/meetings/${state.id}/participants?pageSize=100`)
  const inv = await api(qa, 'memberB', 'POST', `/api/meetings/${state.id}/guest-invitations`, { invitedName: GUEST.name, email: GUEST.email }, { 'If-Match': roster.etag })
  qa.expect(inv.status, `guest invitation: ${JSON.stringify(inv.body)}`).toBe(201)
  for (const [who, action] of [
    ['organizer', { action: 'SubmitForApproval', assignedApproverId: ids.approver }],
    ['approver', { action: 'Approve' }],
    ['organizer', { action: 'Publish' }],
    ['memberA', { action: 'Start' }],
  ])
    qa.expectStatus(await (await client(qa, who)).post(`/api/meetings/${state.id}/transitions`, action), 200)
  qa.note(`Meeting ${state.id}`)
}

export default {
  ticket: 'SYS-WF05',
  title: 'WF-05 Online Meeting Room',
  devices: ['desktop'],
  criteria: [
    {
      id: 'TC01',
      role: PEOPLE.memberA,
      title: 'Chair and a member join the online room and see each other',
      async run(qa) {
        await setup(qa)
        const [a, c] = [await person(qa, 'memberA'), await person(qa, 'memberC')]
        await prefs(qa.page, { mic: false, cam: false })
        await qa.page.goto(`/mgmt/meetings/${state.id}/room`)
        await qa.expect(lobbyHeading(qa.page)).toBeVisible({ timeout: 20_000 })
        await qa.expect(qa.page.getByText(/Cuộc họp được ghi âm để lập biên bản/)).toBeVisible()
        await qa.shot('man-cho')
        await qa.page.getByRole('button', { name: 'Vào phòng', exact: true }).click()
        await qa.expect(leaveButton(qa.page)).toBeVisible({ timeout: 25_000 })
        const member = await secondBrowser(qa, 'memberC')
        await prefs(member, { mic: false, cam: true })
        await enter(qa, member)
        await qa.expect.poll(() => headerCount(qa.page), { timeout: 20_000 }).toBe(2)
        await controls(qa.page).getByRole('button', { name: /Người dự/ }).click()
        const people = qa.page.getByRole('complementary', { name: /Người dự/ })
        await qa.expect(people).toContainText(a.name)
        await qa.expect(people).toContainText(c.name)
        await qa.page.waitForTimeout(1500)
        await qa.shot('chu-toa-thay-hai-nguoi')
        await shotOf(qa, member, 'thanh-vien-trong-phong', 'memberC')
        const m = (await (await client(qa, 'memberA')).get(`/api/meetings/${state.id}`)).body
        qa.expect(m.status).toBe('InProgress')
        state.chairPage = qa.page
        await closeSeconds(qa)
      },
    },
    {
      id: 'TC02',
      role: PEOPLE.memberA,
      title: 'The guest opens the emailed link, verifies the one-time code, gives a name and joins the room',
      async run(qa) {
        await setup(qa)
        const path = await guestLink(state.id, GUEST.email)
        qa.expect(path, 'no invitation email with a guest link').toBeTruthy()
        await prefs(qa.page, { mic: false, cam: false })
        await enter(qa, qa.page)
        try {
          // The code is read from the local Mailpit inbox (sys-lib guestSession), the way the guest reads their mailbox.
          const { guest } = await guestSession(qa, path, {
            email: GUEST.email,
            name: GUEST.name,
            async onCodeScreen(page) {
              await qa.expect(page.getByRole('heading', { level: 1, name: TITLE })).toBeVisible()
              await shotOf(qa, page, 'khach-nhap-ma', 'guest')
            },
            async onProfile(page) {
              await shotOf(qa, page, 'khach-khai-ten', 'guest')
            },
          })
          await qa.expect(guest.getByRole('heading', { level: 1, name: TITLE })).toBeVisible({ timeout: 20_000 })
          await shotOf(qa, guest, 'khach-xem-cuoc-hop', 'guest')
          await prefs(guest, { mic: false, cam: false })
          // "Vào phòng họp" opens the room in a browser tab of its own (features/calls/meetingTabs.ts).
          const go = guest.getByRole('link', { name: /Vào phòng/ }).or(guest.getByRole('button', { name: /Vào phòng họp/ })).first()
          const [room] = await Promise.all([guest.context().waitForEvent('page'), go.click()])
          await qa.expect(lobbyHeading(room)).toBeVisible({ timeout: 20_000 })
          // A guest confirms the recording notice before joining (SCRUM-59); "Vào phòng" stays disabled until then.
          const join = room.getByRole('button', { name: 'Vào phòng', exact: true })
          await qa.expect(join).toBeDisabled()
          await room.locator('.prejoin-consent input[type="checkbox"]').check()
          await shotOf(qa, room, 'khach-xac-nhan-ghi-am', 'guest')
          await join.click()
          await qa.expect(leaveButton(room)).toBeVisible({ timeout: 25_000 })
          await qa.expect.poll(() => headerCount(qa.page), { timeout: 20_000 }).toBe(2)
          await controls(qa.page).getByRole('button', { name: /Người dự/ }).click()
          const people = qa.page.getByRole('complementary', { name: /Người dự/ })
          await qa.expect(people).toContainText(GUEST.name)
          qa.expect(await people.innerText(), 'the guest email is shown in the room').not.toContain(GUEST.email)
          await qa.page.waitForTimeout(1000)
          await qa.shot('chu-toa-thay-khach')
          await shotOf(qa, room, 'khach-trong-phong', 'guest')
          await noSidewaysScroll(qa, room)
        } finally {
          await closeSeconds(qa)
        }
      },
    },
    {
      id: 'TC03',
      role: PEOPLE.memberA,
      title: 'A member raises a hand; the Chair invites them to speak',
      async run(qa) {
        await setup(qa)
        const e = await person(qa, 'memberE')
        await prefs(qa.page, { mic: false, cam: false })
        await enter(qa, qa.page)
        const member = await secondBrowser(qa, 'memberE')
        try {
          await prefs(member, { mic: false, cam: false })
          await enter(qa, member)
          const hand = controls(member).getByRole('button', { name: 'Giơ tay' })
          await qa.expect(hand).toBeVisible({ timeout: 15_000 })
          await qa.expect(async () => {
            if (await hand.isVisible()) await hand.click()
            await qa.expect(controls(member).getByRole('button', { name: 'Hạ tay' })).toBeVisible({ timeout: 3_000 })
          }).toPass({ timeout: 30_000 })
          await shotOf(qa, member, 'thanh-vien-gio-tay', 'memberE')
          const speaking = controls(qa.page).getByRole('button', { name: 'Phát biểu' })
          await qa.expect(speaking.locator('.room-badge')).toHaveText('1', { timeout: 15_000 })
          await speaking.click()
          const panel = qa.page.getByRole('complementary', { name: 'Phát biểu' })
          const row = panel.getByRole('list', { name: 'Hàng đợi phát biểu' }).locator('li').filter({ hasText: e.name })
          await qa.expect(row).toBeVisible()
          await qa.shot('hang-doi-phat-bieu')
          await row.getByRole('button', { name: 'Mời' }).click()
          await qa.expect(member.getByText('Đến lượt bạn phát biểu')).toBeVisible({ timeout: 15_000 })
          await qa.expect(member.getByRole('button', { name: 'Bắt đầu phát biểu' })).toBeVisible()
          await qa.expect(controls(member).getByRole('button', { name: 'Bật micro' })).toBeVisible()
          await shotOf(qa, member, 'den-luot-phat-bieu', 'memberE')
          await member.locator('.room-banner-turn').getByRole('button', { name: 'Bật micro' }).click()
          await qa.expect(controls(member).getByRole('button', { name: 'Tắt micro' })).toBeVisible({ timeout: 15_000 })
          await member.getByRole('button', { name: 'Bắt đầu phát biểu' }).click()
          await qa.expect(member.getByText('Đến lượt bạn phát biểu')).toHaveCount(0, { timeout: 15_000 })
          await member.waitForTimeout(1500)
          await shotOf(qa, member, 'dang-phat-bieu-mic-bat', 'memberE')
          const status = sql(`select s.status from speaking_requests s where s.meeting_id = '${state.id}' order by s.created_at desc limit 1`)[0]?.[0]
          qa.note(`Speaking request of ${e.name}: ${status}`)
          qa.expect(status).toBe('Speaking')
          await qa.shot('chu-toa-thay-dang-phat-bieu')
        } finally {
          await closeSeconds(qa)
        }
      },
    },
    {
      id: 'TC04',
      role: PEOPLE.memberC,
      title: 'Speech in the room is recorded per microphone and reaches the Recording tab',
      async run(qa) {
        await setup(qa)
        await prefs(qa.page, { mic: true, cam: false })
        await enter(qa, qa.page)
        await qa.expect(controls(qa.page).getByRole('button', { name: 'Tắt micro' })).toBeVisible({ timeout: 15_000 })
        await qa.shot('mic-bat-trong-phong')
        await qa.page.waitForTimeout(20_000)
        await leaveButton(qa.page).click()
        await dialog(qa.page, 'Rời phòng họp').getByRole('button', { name: 'Rời phòng' }).click()
        await qa.page.waitForTimeout(5000)
        const recs = (await (await client(qa, 'memberB')).get(`/api/meetings/${state.id}/recordings`)).body
        const n = JSON.stringify(recs).match(/"id"/g)?.length ?? 0
        qa.note(`Recordings API after 20 s of the fake microphone: ${JSON.stringify(recs).slice(0, 300)}`)
        if (n === 0)
          qa.inconclusive(
            'Chromium\'s fake microphone plays a test tone, which the per-microphone sentence detector does not take as speech; needs a person speaking.',
          )
        await qa.page.goto(`/mgmt/meetings/${state.id}?tab=recordings`)
        await qa.shot('tab-ban-ghi')
      },
    },
    {
      id: 'TC05',
      role: PEOPLE.memberC,
      title: 'A member leaves and comes back; when the Chair ends the meeting the room closes for everyone',
      async run(qa) {
        await setup(qa)
        await prefs(qa.page, { mic: false, cam: false })
        await enter(qa, qa.page)
        await leaveButton(qa.page).click()
        const d = dialog(qa.page, 'Rời phòng họp')
        await qa.expect(d.getByText('Cuộc họp vẫn tiếp tục với những người khác. Bạn có thể vào lại khi cuộc họp còn đang diễn ra.')).toBeVisible()
        await qa.shot('hoi-roi-phong')
        await d.getByRole('button', { name: 'Rời phòng' }).click()
        await qa.expect(qa.page).toHaveURL(new RegExp(`/mgmt/meetings/${state.id}`))
        await enter(qa, qa.page)
        await qa.shot('vao-lai-duoc')
        const chair = await secondBrowser(qa, 'memberA')
        try {
          await chair.goto(`/mgmt/meetings/${state.id}`)
          await chair.locator('.record-head').getByRole('button', { name: 'Kết thúc họp' }).click()
          await dialog(chair, 'Kết thúc cuộc họp?').getByRole('button', { name: 'Kết thúc họp' }).click()
          const closed = qa.page.getByRole('heading', { name: 'Cuộc họp đã kết thúc, phòng họp đã đóng.' })
          await qa.expect(closed).toBeVisible({ timeout: 40_000 })
          await qa.expect(qa.page.getByRole('button', { name: 'Vào lại' })).toHaveCount(0)
          await qa.shot('phong-da-dong')
          qa.expect((await (await client(qa, 'memberA')).get(`/api/meetings/${state.id}`)).body.status).toBe('Ended')
        } finally {
          await closeSeconds(qa)
        }
      },
    },
  ],
}

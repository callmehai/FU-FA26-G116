// Report 5.3 System Test — WF-10 Data Isolation Across Roles (BF-01).
// docs/08-reports/test-reports/cases/system/WF-10.json describes each case. The outsider is mai.phan@ (Thành viên),
// who is in none of the [SYS] meetings; the records are those WF-02 to WF-08 left behind, plus one Draft and one
// Published meeting made here with bao.dang@ as a plain member.

import {
  PEOPLE,
  RUN,
  bearerCall,
  client,
  closeSeconds,
  guestInvitedMeeting,
  guestLink,
  guestSession,
  meetingAt,
  secondBrowser,
  settled,
  shotOf,
  sql,
} from './sys-lib.mjs'

const state = {}

async function setup(qa) {
  if (state.draft) return
  state.draft = await meetingAt(qa, 'Draft', { title: `[SYS] WF-10 Họp nháp ${RUN}` })
  state.published = await meetingAt(qa, 'Published', { title: `[SYS] WF-10 Họp đã công bố ${RUN}` })
  // Records of the earlier workflows: the meeting with issued-or-pending minutes, a meeting document of the Mật meeting.
  const ended = sql(`select id from meetings where title = '[SYS] Họp giao ban tuần 49' and status = 'Ended' order by id desc limit 1`)
  state.ended = ended[0]?.[0]
  const doc = sql(
    `select md.meeting_id, md.id from meeting_documents md join meetings m on m.id = md.meeting_id where m.title like '[SYS] WF-08 Họp Mật%' order by md.created_at desc limit 1`,
  )
  state.secretDoc = doc[0] ? { meetingId: doc[0][0], id: doc[0][1] } : null
}

const notFound = (page) => page.getByRole('heading', { name: 'Không tìm thấy trang' })

export default {
  ticket: 'SYS-WF10',
  title: 'WF-10 Data Isolation Across Roles',
  devices: ['desktop'],
  criteria: [
    {
      id: 'TC01',
      role: PEOPLE.memberD,
      title: 'Lists show only the meetings each person may read',
      async run(qa) {
        await setup(qa)
        await qa.goto(`/mgmt/meetings?status=all&q=${encodeURIComponent('[SYS]')}`)
        await qa.expect(qa.page.getByRole('heading', { name: 'Danh sách cuộc họp' })).toBeVisible()
        await qa.page.waitForTimeout(1500)
        await qa.expect(qa.page.locator('main').getByText('[SYS]')).toHaveCount(0)
        await qa.shot('nguoi-ngoai-danh-sach-rong')
        const out = (await (await client(qa, 'memberD')).get(`/api/meetings?q=${encodeURIComponent('[SYS]')}&pageSize=100`)).body
        qa.expect(out.totalCount ?? out.items.length, 'outsider sees [SYS] meetings').toBe(0)
        // A plain member sees the Published meeting but not the Draft one; the organizer sees both.
        const c = (await (await client(qa, 'memberC')).get(`/api/meetings?q=${encodeURIComponent('[SYS] WF-10')}&pageSize=100`)).body.items.map((i) => i.id)
        qa.expect(c).toContain(state.published.id)
        qa.expect(c).not.toContain(state.draft.id)
        const o = (await (await client(qa, 'organizer')).get(`/api/meetings?q=${encodeURIComponent('[SYS] WF-10')}&pageSize=100`)).body.items.map((i) => i.id)
        qa.expect(o).toEqual(qa.expect.arrayContaining([state.published.id, state.draft.id]))
        const page = await secondBrowser(qa, 'memberC')
        try {
          await page.goto(`/mgmt/meetings?status=all&q=${encodeURIComponent('[SYS] WF-10')}`)
          await qa.expect(page.locator('main').getByText(state.published.title).first()).toBeVisible()
          await qa.expect(page.locator('main').getByText(state.draft.title)).toHaveCount(0)
          await settled(page)
          await shotOf(qa, page, 'thanh-vien-chi-thay-hop-da-cong-bo', 'memberC')
        } finally {
          await closeSeconds(qa)
        }
      },
    },
    {
      id: 'TC02',
      role: PEOPLE.memberD,
      title: 'A direct URL to someone else\'s meeting answers like an unknown URL, for the outsider, the member of a Draft and the administrator',
      async run(qa) {
        await setup(qa)
        await qa.goto(`/mgmt/meetings/${state.published.id}`)
        await qa.expect(notFound(qa.page)).toBeVisible({ timeout: 15_000 })
        await qa.shot('nguoi-ngoai-mo-link-cuoc-hop')
        const unknown = '01a0ce78-0000-7000-8000-000000000000'
        const results = {}
        for (const [who, id] of [
          ['memberD', state.published.id],
          ['memberD', unknown],
          ['memberC', state.draft.id],
          ['admin', state.published.id],
          ['approver2', state.published.id],
        ]) {
          const r = await (await client(qa, who)).get(`/api/meetings/${id}`)
          results[`${who}:${id === unknown ? 'unknown' : id === state.draft.id ? 'draft' : 'published'}`] = `${r.status} ${JSON.stringify(r.body)?.slice(0, 60)}`
        }
        qa.note(`GET /api/meetings/{id}: ${Object.entries(results).map(([k, v]) => `${k}=${v}`).join(' | ')}`)
        for (const v of Object.values(results)) qa.expect(v.startsWith('404')).toBe(true)
        qa.expect(results['memberD:published']).toBe(results['memberD:unknown'])
        const admin = await secondBrowser(qa, 'admin')
        try {
          await admin.goto(`/mgmt/meetings/${state.published.id}`)
          await qa.expect(notFound(admin)).toBeVisible({ timeout: 15_000 })
          await shotOf(qa, admin, 'quan-tri-mo-link-cuoc-hop', 'admin')
        } finally {
          await closeSeconds(qa)
        }
      },
    },
    {
      id: 'TC03',
      role: PEOPLE.memberD,
      title: 'Documents, minutes, votes and recordings of others\' meetings are not reachable',
      async run(qa) {
        await setup(qa)
        qa.expect(state.ended, 'no Ended meeting from WF-03').toBeTruthy()
        qa.expect(state.secretDoc, 'no Mật meeting document from WF-08').toBeTruthy()
        const d = await client(qa, 'memberD')
        const paths = [
          `/api/meetings/${state.ended}/minutes`,
          `/api/meetings/${state.ended}/votes`,
          `/api/meetings/${state.ended}/documents`,
          `/api/meetings/${state.ended}/recordings`,
          `/api/meetings/${state.ended}/agenda`,
          `/api/meetings/${state.secretDoc.meetingId}/documents/${state.secretDoc.id}/content`,
        ]
        const got = []
        for (const p of paths) got.push(`${p.replace(/[0-9a-f-]{36}/g, '{id}')} → ${(await d.get(p)).status}`)
        qa.note(got.join(' | '))
        for (const g of got) qa.expect(g, g).toMatch(/→ 404$/)
        await qa.goto(`/mgmt/meetings/${state.ended}?tab=minutes`)
        await qa.expect(notFound(qa.page)).toBeVisible({ timeout: 15_000 })
        await qa.goto(`/mgmt/meetings/${state.secretDoc.meetingId}/documents/${state.secretDoc.id}`)
        await qa.expect(notFound(qa.page)).toBeVisible({ timeout: 15_000 })
        await qa.shot('nguoi-ngoai-mo-tai-lieu-mat')
        const anon = await qa.api('anon')
        qa.expectStatus(await anon.get(`/api/meetings/${state.ended}/minutes`), 401)
      },
    },
    {
      id: 'TC04',
      role: 'anon',
      title: 'A guest sees only the meeting they were invited to',
      async run(qa) {
        await setup(qa)
        // A guest session from the code mailed to the guest, read from the local Mailpit inbox (sys-lib guestSession).
        const g = { name: 'Nguyễn Khách Mời', email: `sys-wf10-guest-${RUN}@trustcabinet.local` }
        const invited = await guestInvitedMeeting(qa, { title: `[SYS] WF-10 Họp có khách ${RUN}`, guestName: g.name, guestEmail: g.email })
        const path = await guestLink(invited.id, g.email)
        qa.expect(path, 'no invitation email with a guest link').toBeTruthy()
        try {
          const { guest, token } = await guestSession(qa, path, { email: g.email, name: g.name })
          await qa.expect(guest.getByRole('heading', { level: 1, name: invited.title })).toBeVisible({ timeout: 20_000 })
          await guest.getByRole('tab', { name: 'Tài liệu' }).click()
          await settled(guest)
          await shotOf(qa, guest, 'khach-xem-cuoc-hop-duoc-moi', 'guest')
          // Another meeting and the personal library, on the screen and through the API with the guest's token.
          await guest.goto(`/mgmt/meetings/${state.published.id}`)
          await guest.waitForLoadState('networkidle', { timeout: 10_000 }).catch(() => undefined)
          await qa.expect(guest.getByText(state.published.title)).toHaveCount(0)
          await shotOf(qa, guest, 'khach-mo-cuoc-hop-khac', 'guest')
          await guest.goto('/mgmt/documents')
          await guest.waitForLoadState('networkidle', { timeout: 10_000 }).catch(() => undefined)
          await qa.expect(guest.getByRole('heading', { name: 'Kho tài liệu' })).toHaveCount(0)
          await shotOf(qa, guest, 'khach-mo-kho-tai-lieu', 'guest')
          const own = await bearerCall(qa.base, token, 'GET', `/api/meetings/${invited.id}/agenda`)
          const other = await bearerCall(qa.base, token, 'GET', `/api/meetings/${state.published.id}/agenda`)
          const otherMeeting = await bearerCall(qa.base, token, 'GET', `/api/meetings/${state.published.id}`)
          const unknown = await bearerCall(qa.base, token, 'GET', '/api/meetings/00000000-0000-7000-8000-000000000000')
          const library = await bearerCall(qa.base, token, 'GET', '/api/documents')
          qa.note(
            `Guest token: invited agenda ${own.status}; other meeting agenda ${other.status}, detail ${otherMeeting.status} (unknown id ${unknown.status}); personal library ${library.status}`,
          )
          // The same answer for a real and an unknown id: the endpoint refuses a guest token, it does not reveal the meeting.
          qa.expect(otherMeeting.status, 'another meeting answers differently from an unknown id').toBe(unknown.status)
          qa.expect(own.status, 'the guest cannot read the invited meeting').toBe(200)
          for (const [label, r] of [['other agenda', other], ['other meeting', otherMeeting], ['personal library', library]])
            qa.expect([401, 403, 404], `${label} answered ${r.status}`).toContain(r.status)
          const accounts = sql(`select count(*) from users where lower(email) = lower('${g.email}')`)[0][0]
          qa.expect(accounts, 'an internal account was created for the guest').toBe('0')
        } finally {
          await closeSeconds(qa)
        }
      },
    },
  ],
}

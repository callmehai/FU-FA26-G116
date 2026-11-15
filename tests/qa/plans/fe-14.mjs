// FE-14 — Dashboard cá nhân & Không gian họp (QA subtask SCRUM-201). docs/uc-map.html maps FE-14 to one use case, UC-69
// "View Personal Dashboard & Meeting Workspace", whose specification is still a placeholder (05-use-case-specifications.md,
// Report 3 §4.2 row UC-69) and which cites no business rule and no message code. What the feature should show is in
// Report 3 §5 screen descriptions 14.1 Personal Dashboard [Tổng quan] and 14.2 Meeting Workspace, the wireframe
// (docs/wireframe/TrustCabinet-wireframe-v12.html, FE-14) and the permission matrix row "Dashboard — own items only"
// (Report 3, note ¹²). No Jira ticket carries UC-69.
//
// The PO dropped UC-69 on 15/11/2026 (QA round 3 found neither screen; FE-14 was Should-have and never had a ticket), so
// the plan has no criteria for 14.1 / 14.2: AC1 and AC2, which recorded the missing screens, were removed.
//
// The nearest thing that runs is the four count cards over the meeting list (PO decision, PR #287: Họp hôm nay — the
// list's default — Đã công bố, Cần xử lý, Bản nháp; contract in docs/03-api/05-quan-ly-cuoc-hop.md, "scope" and the FE
// notes). AC3 → AC9 check them per role. Other QA agents create meetings in parallel, so a number on screen is only ever
// compared with the API response the page itself received, never with a fixed number.
//
// Data, all "[QA] FE-14 …", organised by thuky@ with chutoa@ as Chair and approver, thuky@ as Secretary, thanhvien@ as
// Member; reused across runs (found by exact title and status), never cancelled:
// - today (Vietnam) late: "họp tối nay dd/mm" 22:00–23:00 Published; "họp khuya dd/mm chờ duyệt" 23:05–23:50 WaitingApproval;
// - 11 weeks ahead: "chờ Chủ toạ duyệt" 08:30 WaitingApproval; "bản nháp" 10:00 Draft; "đã duyệt chờ công bố" 13:30
//   Approved; "không gian họp" 15:00 InProgress (started early by the Chair) with two agenda items, the first one current,
//   and one open vote.

const PASSWORD = process.env.QA_PASSWORD ?? 'TrustCabinet@2026'
const EMAIL = {
  admin: 'quantri@trustcabinet.local',
  chair: 'chutoa@trustcabinet.local',
  secretary: 'thuky@trustcabinet.local',
  member: 'thanhvien@trustcabinet.local',
}

// ---- dates, in Vietnam's calendar ----
const vnToday = () =>
  new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Ho_Chi_Minh', year: 'numeric', month: '2-digit', day: '2-digit' }).format(
    new Date(),
  )
const addDays = (day, n) => new Date(Date.UTC(+day.slice(0, 4), +day.slice(5, 7) - 1, +day.slice(8, 10) + n)).toISOString().slice(0, 10)
const dm = (day) => `${day.slice(8, 10)}/${day.slice(5, 7)}`
const at = (day, hhmm) => `${day}T${hhmm}:00+07:00`
const TODAY = vnToday()
const FAR = addDays(TODAY, 77) // eleven weeks ahead

/** The plan's meetings: key → title (without "[QA] "), day, hours, the status it must be in. */
const MEETINGS = {
  tonight: { title: `FE-14 họp tối nay ${dm(TODAY)}`, day: TODAY, from: '22:00', to: '23:00', status: 'Published' },
  lateWaiting: { title: `FE-14 họp khuya ${dm(TODAY)} chờ duyệt`, day: TODAY, from: '23:05', to: '23:50', status: 'WaitingApproval' },
  waiting: { title: 'FE-14 chờ Chủ toạ duyệt', day: FAR, from: '08:30', to: '09:30', status: 'WaitingApproval' },
  draft: { title: 'FE-14 bản nháp', day: FAR, from: '10:00', to: '11:00', status: 'Draft' },
  approved: { title: 'FE-14 đã duyệt chờ công bố', day: FAR, from: '13:30', to: '14:15', status: 'Approved' },
  live: { title: 'FE-14 không gian họp', day: FAR, from: '15:00', to: '16:30', status: 'InProgress' },
}
const ITEM_NOW = 'FE-14 mục đang bàn: báo cáo quý IV'
const ITEM_NEXT = 'FE-14 mục tiếp theo: kế hoạch 2027'
const VOTE_TITLE = 'FE-14 thông qua kế hoạch 2027'

// ---- API helpers ----
const USERS = {}
async function users(qa) {
  for (const role of ['admin', 'chair', 'secretary', 'member']) {
    if (USERS[role]) continue
    const me = (await (await client(qa, role)).get('/api/auth/me')).body
    USERS[role] = { id: me.id, name: me.fullName, capabilities: me.capabilities ?? [] }
  }
  return USERS
}
async function client(qa, role) {
  qa.__clients ??= {}
  qa.__clients[role] ??= await qa.api(role)
  return qa.__clients[role]
}
async function transition(qa, role, id, action) {
  const res = await (await client(qa, role)).post(`/api/meetings/${id}/transitions`, action)
  qa.expectStatus(res, 200)
}

/** Finds the plan's meeting by exact title among the organizer's, in the wanted status. */
async function findMeeting(qa, spec) {
  const title = qa.name(spec.title)
  const res = await (await client(qa, 'secretary')).get(`/api/meetings?q=${encodeURIComponent(spec.title)}&pageSize=100`)
  qa.expectStatus(res, 200)
  return res.body.items.find((m) => m.title === title && m.status === spec.status) ?? null
}

const made = {}
/** The meeting of `key`, created and driven to its status through the API when this database has none yet. */
async function meeting(qa, key) {
  if (made[key]) return made[key]
  const spec = MEETINGS[key]
  const found = await findMeeting(qa, spec)
  if (found) return (made[key] = found)
  if (new Date(at(spec.day, spec.from)).getTime() < Date.now() + 5 * 60_000)
    qa.inconclusive(`Đã quá giờ để tạo cuộc họp "${spec.title}" trong hôm nay (${spec.from}). Chạy lại trước 21:55.`)
  const u = await users(qa)
  const sec = await client(qa, 'secretary')
  const created = await sec.post('/api/meetings', {
    title: qa.name(spec.title),
    plannedStartTime: at(spec.day, spec.from),
    plannedEndTime: at(spec.day, spec.to),
    mode: 'InPerson',
    location: 'Phòng họp QA',
    onlineLink: null,
    sensitivity: 'Normal',
    description: 'Dữ liệu kiểm thử FE-14 (dashboard cá nhân, bốn thẻ đếm).',
    participants: [
      { userId: u.chair.id, role: 'Chair' },
      { userId: u.secretary.id, role: 'Secretary' },
      { userId: u.member.id, role: 'Member' },
    ],
  })
  qa.expectStatus(created, 201)
  const id = created.body.meeting.id
  if (key === 'live') {
    for (const title of [ITEM_NOW, ITEM_NEXT]) {
      const r = await sec.post(`/api/meetings/${id}/agenda`, { title: qa.name(title), description: '', presenterId: u.member.id, presenterGuestInvitationId: null })
      qa.expectStatus(r, 201)
    }
  }
  const steps = { Draft: 0, WaitingApproval: 1, Approved: 2, Published: 3, InProgress: 4 }[spec.status]
  const path = [
    ['secretary', { action: 'SubmitForApproval', assignedApproverId: u.chair.id }],
    ['chair', { action: 'Approve' }],
    ['secretary', { action: 'Publish' }],
    ['chair', { action: 'Start' }],
  ]
  for (const [role, action] of path.slice(0, steps)) await transition(qa, role, id, action)
  const got = await sec.get(`/api/meetings/${id}`)
  qa.expectStatus(got, 200)
  return (made[key] = got.body)
}

/** Makes the running meeting look like the workspace mock: the first item under way, one open vote. */
async function liveMeeting(qa) {
  const m = await meeting(qa, 'live')
  const chair = await client(qa, 'chair')
  const agenda = await chair.get(`/api/meetings/${m.id}/agenda`)
  qa.expectStatus(agenda, 200)
  const items = agenda.body.items ?? []
  const now = items.find((i) => i.title === qa.name(ITEM_NOW))
  if (now && now.status === 'Pending' && !items.some((i) => i.status === 'InProgress'))
    qa.expectStatus(await chair.post(`/api/meetings/${m.id}/agenda/${now.id}/current`), 200)
  const votes = await chair.get(`/api/meetings/${m.id}/votes`)
  qa.expectStatus(votes, 200)
  const list = votes.body.items ?? votes.body.votes ?? (Array.isArray(votes.body) ? votes.body : [])
  if (!list.some((v) => v.title === qa.name(VOTE_TITLE) && v.status === 'Open')) {
    const r = await chair.post(`/api/meetings/${m.id}/votes`, {
      title: qa.name(VOTE_TITLE),
      description: '',
      options: ['Đồng ý', 'Không đồng ý', 'Bỏ phiếu trắng'],
      isAnonymous: false,
      eligibleRoles: ['Chair', 'Secretary', 'Member'],
      openImmediately: true,
    })
    qa.expect([200, 201], `opening the vote: HTTP ${r.status} ${JSON.stringify(r.body).slice(0, 200)}`).toContain(r.status)
  }
  return m
}

/** Every TODAY meeting the plan uses, created first so the list has them when the page loads. */
async function prepareAll(qa) {
  for (const key of Object.keys(MEETINGS)) await (key === 'live' ? liveMeeting(qa) : meeting(qa, key))
}

// ---- screen helpers ----
async function noSidewaysScroll(qa) {
  const sideways = await qa.page.evaluate(() => document.documentElement.scrollWidth - innerWidth)
  qa.expect(sideways, 'the page scrolls sideways').toBeLessThanOrEqual(0)
}
async function noRawCodes(qa) {
  const text = await qa.page.locator('main').innerText()
  qa.expect(text, 'a raw MSG code is shown to the user').not.toMatch(/MSG\d+/)
}
async function settled(qa) {
  await qa.page.waitForFunction(() =>
    document.getAnimations().every((a) => a.playState !== 'running' || a.effect?.getTiming().iterations === Infinity),
  )
}
async function press(qa, locator) {
  if (qa.device === 'phone') await locator.tap()
  else await locator.click()
}

const CARD_LABEL = { today: 'Họp hôm nay', published: 'Đã công bố', todo: 'Cần xử lý', draft: 'Bản nháp' }
const stats = (qa) => qa.page.getByRole('region', { name: 'Thống kê cuộc họp' }).or(qa.page.locator('section[aria-label="Thống kê cuộc họp"]')).first()
const card = (qa, key) => stats(qa).getByRole('button', { name: new RegExp(CARD_LABEL[key]) })
const listRows = (qa) => qa.page.locator('table.meetings-table tbody tr')
const rowLink = (qa, title) => qa.page.locator('table.meetings-table').getByRole('link', { name: title, exact: true })
const parseCount = (text) => Number(String(text).replace(/\./g, '').trim())

/**
 * Records every GET /api/meetings the page makes: the one-row requests behind the cards (by their criteria) and the
 * list's own. Registered before the page loads; the page's numbers are judged against these, never a fixed number.
 */
function watchMeetings(qa) {
  const seen = { cards: {}, lists: [] }
  qa.page.on('response', async (r) => {
    const u = new URL(r.url())
    if (u.pathname !== '/api/meetings' || r.request().method() !== 'GET') return
    let body
    try {
      body = await r.json()
    } catch {
      return
    }
    const p = u.searchParams
    if (p.get('pageSize') === '1') {
      const key =
        p.get('scope') === 'todo'
          ? 'todo'
          : p.get('status') === 'Published'
            ? 'published'
            : p.get('status') === 'Draft'
              ? 'draft'
              : p.get('status') === 'InProgress'
                ? 'live'
                : p.get('from')
                  ? 'today'
                  : 'other'
      ;(seen.cards[key] ??= []).push({ status: r.status(), total: body?.totalCount })
    } else {
      seen.lists.push({ search: u.search, params: Object.fromEntries(p), status: r.status(), total: body?.totalCount, items: body?.items ?? [] })
    }
  })
  return seen
}

/** Opens the meeting list and waits for the cards and the list to load. */
async function openList(qa, path = '/mgmt/meetings') {
  const seen = watchMeetings(qa)
  await qa.goto(path)
  await qa.expect(stats(qa)).toHaveAttribute('aria-busy', 'false', { timeout: 15_000 })
  await qa.expect(listRows(qa).first()).toBeVisible()
  await qa.page.waitForFunction(() => !document.querySelector('.row-skeleton'))
  return seen
}

/** The number a card shows equals one of the totals the page received for that card's request. */
async function expectCardMatches(qa, seen, key) {
  const shown = parseCount(await card(qa, key).locator('.stat-value').innerText())
  const totals = (seen.cards[key] ?? []).filter((x) => x.status === 200).map((x) => x.total)
  qa.expect(totals.length, `no API answer seen for the "${CARD_LABEL[key]}" card`).toBeGreaterThan(0)
  qa.expect(totals, `"${CARD_LABEL[key]}" shows ${shown}, the page's API answers were ${totals.join(', ')}`).toContain(shown)
  return shown
}

/** The "Họp hôm nay" sub line matches the InProgress count the page received. */
async function expectLiveLine(qa, seen) {
  const sub = (await card(qa, 'today').locator('.stat-sub').innerText()).trim()
  const lives = (seen.cards.live ?? []).map((x) => x.total)
  qa.expect(lives.length, 'no InProgress count request seen').toBeGreaterThan(0)
  const expected = lives.map((n) => (n > 0 ? `${n.toLocaleString('vi-VN')} phiên đang diễn ra` : 'Không có phiên đang diễn ra'))
  qa.expect(expected, `sub line "${sub}"`).toContain(sub)
}

/** The list response the page received after an action, matching `pred` on its query. */
async function nextList(qa, seen, pred, timeout = 10_000) {
  const end = Date.now() + timeout
  while (Date.now() < end) {
    const hit = [...seen.lists].reverse().find((l) => pred(l.params))
    if (hit) return hit
    await qa.page.waitForTimeout(100)
  }
  throw new Error('the list request after the action was not seen')
}

/** Status labels of the rows on screen. */
async function rowStatuses(qa) {
  return qa.page.locator('table.meetings-table tbody tr td.cell-status').allInnerTexts()
}

async function search(qa, seen, text) {
  seen.lists.length = 0
  await qa.page.getByRole('searchbox', { name: 'Tìm theo tên cuộc họp' }).fill(text)
  await nextList(qa, seen, (p) => p.q === text)
  await qa.page.waitForFunction(() => !document.querySelector('.data-table-stale, .row-skeleton'))
}

async function requireLacks(qa, role, capability) {
  const { roleName, capabilities } = await qa.capabilities(role)
  if (capabilities.includes(capability))
    qa.inconclusive(`${role} giữ vai trò "${roleName}" có ${capability} trên database này. Trả tài khoản về vai trò seed rồi chạy lại.`)
}
async function requireHas(qa, role, capability) {
  const { roleName, capabilities } = await qa.capabilities(role)
  if (!capabilities.includes(capability))
    qa.inconclusive(`${role} giữ vai trò "${roleName}" không có ${capability} trên database này. Trả tài khoản về vai trò seed rồi chạy lại.`)
}

// ---------------------------------------------------------------------------------------------------------------------

export default {
  ticket: 'SCRUM-201',
  title: 'FE-14 Dashboard cá nhân & Không gian họp (UC-69) và bốn thẻ đếm trên danh sách cuộc họp',
  devices: ['desktop'],
  criteria: [
    {
      id: 'AC0',
      role: 'secretary',
      title: 'Chuẩn bị dữ liệu [QA] FE-14 (qua API)',
      steps: ['Tìm hoặc tạo 6 cuộc họp [QA] FE-14: hai cuộc hôm nay tối muộn, bốn cuộc 11 tuần sau ở các trạng thái cần thử'],
      expected: 'Đủ 6 cuộc họp đúng trạng thái; cuộc "không gian họp" Đang diễn ra có mục nghị sự đang bàn và biểu quyết đang mở',
      async run(qa) {
        await prepareAll(qa)
        for (const [key, m] of Object.entries(made)) qa.note(`${key}: ${m.title} · ${m.status} · ${m.id}`)
      },
    },

    // ------------------------------------------------------------------------- the four cards (PR #287)
    {
      id: 'AC3',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Thư ký mở danh sách: thẻ "Họp hôm nay" được chọn, bốn thẻ có số đúng với API, danh sách hôm nay có cuộc họp tối nay',
      steps: ['Đăng nhập thuky@ (MeetingCreator), mở /mgmt/meetings không tham số'],
      expected:
        'Bốn thẻ Họp hôm nay, Đã công bố, Cần xử lý, Bản nháp; mỗi số bằng totalCount API trả cho chính trang đó; dòng phụ "N phiên đang diễn ra" bằng số InProgress; "Họp hôm nay" được chọn, danh sách có đúng số cuộc họp của thẻ, gồm "[QA] FE-14 họp tối nay" và "… họp khuya … chờ duyệt"; không cuộn ngang',
      async run(qa) {
        await requireHas(qa, 'secretary', 'MeetingCreator')
        await prepareAll(qa)
        const seen = await openList(qa)
        await qa.expect(stats(qa).getByRole('button')).toHaveCount(4)
        await qa.expect(card(qa, 'today')).toHaveAttribute('aria-pressed', 'true')
        for (const key of ['today', 'published', 'todo', 'draft']) await expectCardMatches(qa, seen, key)
        await expectLiveLine(qa, seen)
        const today = parseCount(await card(qa, 'today').locator('.stat-value').innerText())
        const list = await nextList(qa, seen, (p) => p.from && !p.status && p.pageSize !== '1')
        qa.expect(list.total, `the today list holds ${list.total}, the card says ${today}`).toBe(today)
        await settled(qa)
        await qa.shot('bon-the-va-danh-sach-hom-nay')
        if (list.total > 20) await search(qa, seen, 'FE-14')
        await qa.expect(rowLink(qa, made.tonight.title)).toBeVisible()
        await qa.expect(rowLink(qa, made.lateWaiting.title)).toBeVisible()
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
      },
    },
    {
      id: 'AC4',
      role: 'member',
      devices: ['desktop', 'phone'],
      title: 'Thành viên: không có thẻ Bản nháp, số đúng với API, chỉ thấy cuộc họp hôm nay đã công bố, Cần xử lý trống có câu báo',
      steps: [
        'Đăng nhập thanhvien@ (không có quyền nào), mở /mgmt/meetings',
        'Bấm thẻ "Cần xử lý"',
        'Bấm "Xem mọi cuộc họp"',
      ],
      expected:
        'Ba thẻ (không Bản nháp — chỉ người có MeetingCreator); số bằng API; danh sách hôm nay có "[QA] FE-14 họp tối nay" (Đã công bố), không có cuộc "chờ duyệt" (Thành viên chỉ thấy từ khi công bố); Cần xử lý = 0 vì không có quyền duyệt/tạo, danh sách "Không có việc chờ bạn xử lý." với nút "Xem mọi cuộc họp"; bấm nút: bỏ chọn mọi thẻ',
      async run(qa) {
        await requireLacks(qa, 'member', 'MeetingCreator')
        await requireLacks(qa, 'member', 'MeetingApprover')
        await prepareAll(qa)
        const seen = await openList(qa)
        await qa.expect(stats(qa).getByRole('button')).toHaveCount(3)
        await qa.expect(card(qa, 'draft')).toHaveCount(0)
        qa.expect(seen.cards.draft ?? [], 'the page asked for a Draft count it does not show').toEqual([])
        for (const key of ['today', 'published', 'todo']) await expectCardMatches(qa, seen, key)
        await expectLiveLine(qa, seen)
        const list = await nextList(qa, seen, (p) => p.from && !p.status && p.pageSize !== '1')
        if (list.total > 20) await search(qa, seen, 'FE-14')
        await qa.expect(rowLink(qa, made.tonight.title)).toBeVisible()
        await qa.expect(rowLink(qa, made.lateWaiting.title)).toHaveCount(0)
        await settled(qa)
        await qa.shot('ba-the-thanh-vien')
        const todo = parseCount(await card(qa, 'todo').locator('.stat-value').innerText())
        qa.expect(todo, 'a member with no capability has something "to do"').toBe(0)
        seen.lists.length = 0
        await press(qa, card(qa, 'todo'))
        await qa.expect(qa.page).toHaveURL(/\?scope=todo$/)
        await qa.expect(qa.page.getByText('Không có việc chờ bạn xử lý.')).toBeVisible()
        await settled(qa)
        await qa.shot('can-xu-ly-trong')
        await noSidewaysScroll(qa)
        await press(qa, qa.page.getByRole('button', { name: 'Xem mọi cuộc họp' }))
        await qa.expect(qa.page).not.toHaveURL(/scope=todo/)
        await qa.expect(stats(qa).locator('[aria-pressed="true"]')).toHaveCount(0)
        // Read rights behind the cards: a member never sees a draft or a meeting waiting for approval.
        const api = await client(qa, 'member')
        const drafts = await api.get(`/api/meetings?status=Draft&q=${encodeURIComponent('FE-14')}&pageSize=100`)
        qa.expectStatus(drafts, 200)
        qa.expect(drafts.body.items.map((m) => m.title)).not.toContain(made.draft.title)
        const waiting = await api.get(`/api/meetings?status=WaitingApproval&q=${encodeURIComponent('FE-14')}&pageSize=100`)
        qa.expect(waiting.body.items.map((m) => m.title)).not.toContain(made.waiting.title)
      },
    },
    {
      id: 'AC5',
      role: 'chair',
      devices: ['desktop', 'phone'],
      title: 'Chủ toạ (người duyệt) bấm "Cần xử lý": đúng các cuộc họp chờ mình duyệt, bấm lại thì bỏ lọc',
      steps: ['Đăng nhập chutoa@, mở danh sách', 'Bấm "Cần xử lý"', 'Tìm "FE-14" (thẻ vẫn bật)', 'Bấm lại "Cần xử lý"'],
      expected:
        'URL ?scope=todo, thẻ được chọn, danh sách có đúng số của thẻ; mọi dòng Chờ duyệt (được giao cho mình) hoặc Đã duyệt (mình tổ chức); có "[QA] FE-14 chờ Chủ toạ duyệt", không có "đã duyệt chờ công bố" (thuky@ tổ chức); tìm kiếm giữ scope=todo; bấm lại: bỏ scope, thẻ tắt',
      async run(qa) {
        await requireHas(qa, 'chair', 'MeetingApprover')
        await prepareAll(qa)
        const seen = await openList(qa)
        for (const key of ['today', 'published', 'todo', 'draft']) await expectCardMatches(qa, seen, key)
        seen.lists.length = 0
        await press(qa, card(qa, 'todo'))
        await qa.expect(qa.page).toHaveURL(/\/mgmt\/meetings\?scope=todo$/)
        await qa.expect(card(qa, 'todo')).toHaveAttribute('aria-pressed', 'true')
        const list = await nextList(qa, seen, (p) => p.scope === 'todo' && p.pageSize !== '1')
        const todo = parseCount(await card(qa, 'todo').locator('.stat-value').innerText())
        qa.expect(list.total, `"Cần xử lý" list holds ${list.total}, card says ${todo}`).toBe(todo)
        qa.expect(list.params.from, 'Cần xử lý must not be limited to today').toBeUndefined()
        await qa.page.waitForFunction(() => !document.querySelector('.data-table-stale, .row-skeleton'))
        await settled(qa)
        await qa.shot('can-xu-ly-chu-toa')
        for (const s of await rowStatuses(qa)) qa.expect(['Chờ duyệt', 'Đã duyệt']).toContain(s.trim())
        await search(qa, seen, 'FE-14')
        await qa.expect(qa.page).toHaveURL(/scope=todo/)
        await qa.expect(card(qa, 'todo')).toHaveAttribute('aria-pressed', 'true')
        await qa.expect(rowLink(qa, made.waiting.title)).toBeVisible()
        await qa.expect(rowLink(qa, made.lateWaiting.title)).toBeVisible()
        await qa.expect(rowLink(qa, made.approved.title)).toHaveCount(0)
        await qa.expect(rowLink(qa, made.draft.title)).toHaveCount(0)
        await settled(qa)
        await qa.shot('can-xu-ly-tim-fe-14')
        await press(qa, card(qa, 'todo'))
        await qa.expect(qa.page).not.toHaveURL(/scope=todo/)
        await qa.expect(card(qa, 'todo')).toHaveAttribute('aria-pressed', 'false')
        await noSidewaysScroll(qa)
      },
    },
    {
      id: 'AC6',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Thư ký (người tổ chức): "Cần xử lý" có cuộc họp đã duyệt chờ mình công bố; "Bản nháp", "Đã công bố" đưa tới đúng danh sách lọc',
      steps: [
        'Đăng nhập thuky@, mở danh sách, gõ tìm "abc" rồi bấm "Bản nháp"',
        'Tìm "FE-14"',
        'Bấm "Đã công bố"',
        'Bấm "Cần xử lý", tìm "FE-14"',
      ],
      expected:
        'Bấm thẻ xoá ô tìm và bộ lọc khác: URL đúng ?status=Draft / ?status=Published / ?scope=todo; số dòng bằng số trên thẻ; mọi dòng đúng trạng thái của thẻ; Bản nháp có "[QA] FE-14 bản nháp"; Cần xử lý có "đã duyệt chờ công bố" mà không có "chờ Chủ toạ duyệt" (mình là người tổ chức, không phải người duyệt)',
      async run(qa) {
        await requireHas(qa, 'secretary', 'MeetingCreator')
        await prepareAll(qa)
        const seen = await openList(qa)
        await search(qa, seen, 'abc')
        // Bản nháp
        seen.lists.length = 0
        await press(qa, card(qa, 'draft'))
        await qa.expect(qa.page).toHaveURL(/\/mgmt\/meetings\?status=Draft$/)
        await qa.expect(qa.page.getByRole('searchbox', { name: 'Tìm theo tên cuộc họp' })).toHaveValue('')
        let list = await nextList(qa, seen, (p) => p.status === 'Draft' && p.pageSize !== '1')
        qa.expect(list.total).toBe(parseCount(await card(qa, 'draft').locator('.stat-value').innerText()))
        await qa.page.waitForFunction(() => !document.querySelector('.data-table-stale, .row-skeleton'))
        for (const s of await rowStatuses(qa)) qa.expect(s.trim()).toBe('Nháp')
        await search(qa, seen, 'FE-14')
        await qa.expect(rowLink(qa, made.draft.title)).toBeVisible()
        await settled(qa)
        await qa.shot('ban-nhap')
        // Đã công bố
        seen.lists.length = 0
        await press(qa, card(qa, 'published'))
        await qa.expect(qa.page).toHaveURL(/\/mgmt\/meetings\?status=Published$/)
        list = await nextList(qa, seen, (p) => p.status === 'Published' && p.pageSize !== '1')
        qa.expect(list.total).toBe(parseCount(await card(qa, 'published').locator('.stat-value').innerText()))
        await qa.page.waitForFunction(() => !document.querySelector('.data-table-stale, .row-skeleton'))
        for (const s of await rowStatuses(qa)) qa.expect(s.trim()).toBe('Đã công bố')
        await settled(qa)
        await qa.shot('da-cong-bo')
        // Cần xử lý
        seen.lists.length = 0
        await press(qa, card(qa, 'todo'))
        await qa.expect(qa.page).toHaveURL(/\/mgmt\/meetings\?scope=todo$/)
        list = await nextList(qa, seen, (p) => p.scope === 'todo' && p.pageSize !== '1')
        qa.expect(list.total).toBe(parseCount(await card(qa, 'todo').locator('.stat-value').innerText()))
        await search(qa, seen, 'FE-14')
        await qa.expect(rowLink(qa, made.approved.title)).toBeVisible()
        await qa.expect(rowLink(qa, made.waiting.title)).toHaveCount(0)
        for (const s of await rowStatuses(qa)) qa.expect(s.trim()).toBe('Đã duyệt')
        await settled(qa)
        await qa.shot('can-xu-ly-thu-ky')
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
      },
    },
    {
      id: 'AC7',
      role: 'admin',
      title: 'Quản trị (vai trò All-in-one): bốn thẻ khớp API, không thấy cuộc họp [QA] FE-14 mình không liên quan',
      steps: ['Đăng nhập quantri@, mở danh sách', 'Tìm "FE-14" ở mọi thời điểm'],
      expected:
        'Số trên thẻ bằng API; quantri@ không có trong thành phần nên không thấy cuộc họp [QA] FE-14 nào (Report 3 ³: chỉ cuộc họp trong quan hệ của mình; ¹²: dashboard chỉ việc của mình)',
      async run(qa) {
        const caps = await qa.capabilities('admin')
        qa.note(`quantri@: ${caps.roleName} · ${caps.capabilities.join(', ')}`)
        await prepareAll(qa)
        const seen = await openList(qa, '/mgmt/meetings?when=all')
        const keys = caps.capabilities.includes('MeetingCreator') ? ['today', 'published', 'todo', 'draft'] : ['today', 'published', 'todo']
        await qa.expect(stats(qa).getByRole('button')).toHaveCount(keys.length)
        for (const key of keys) await expectCardMatches(qa, seen, key)
        await search(qa, seen, 'FE-14')
        for (const m of Object.values(made)) await qa.expect(rowLink(qa, m.title)).toHaveCount(0)
        await settled(qa)
        await qa.shot('quan-tri-khong-thay-fe-14')
        const todo = await (await client(qa, 'admin')).get(`/api/meetings?scope=todo&q=${encodeURIComponent('FE-14')}&pageSize=100`)
        qa.expectStatus(todo, 200)
        qa.expect(todo.body.items.filter((m) => m.title.startsWith('[QA] FE-14'))).toEqual([])
      },
    },
    {
      id: 'AC8',
      role: 'member',
      devices: ['desktop', 'phone'],
      title: 'Một ô đếm không tải được: ô đó báo "Không tải được số lượng", không hiện số sai; các ô khác vẫn có số',
      steps: ['Đăng nhập thanhvien@', 'Cho request đếm "Đã công bố" trả 500, mở danh sách'],
      expected: 'Ô Đã công bố hiện "—" và "Không tải được số lượng" (không hiện 0); Họp hôm nay, Cần xử lý vẫn có số; không hiện mã MSG thô',
      async run(qa) {
        await qa.page.route(
          (url) => url.pathname === '/api/meetings' && url.searchParams.get('pageSize') === '1' && url.searchParams.get('status') === 'Published',
          (route) => route.fulfill({ status: 500, contentType: 'application/json', body: JSON.stringify({ code: 'MSG00', message: 'QA: lỗi giả lập' }) }),
        )
        const seen = await openList(qa)
        await qa.expect(card(qa, 'published').locator('.stat-value')).toHaveText('—')
        await qa.expect(card(qa, 'published').locator('.stat-sub')).toHaveText('Không tải được số lượng')
        for (const key of ['today', 'todo']) await expectCardMatches(qa, seen, key)
        await settled(qa)
        await qa.shot('o-dem-loi')
        await noRawCodes(qa)
        await noSidewaysScroll(qa)
      },
    },
    {
      id: 'AC9',
      role: 'anon',
      title: 'Chưa đăng nhập: không đọc được số đếm (401), mở danh sách thì về trang đăng nhập; scope lạ bị từ chối 400 MSG32',
      steps: ['GET /api/meetings?scope=todo&pageSize=1 không token', 'Mở /mgmt/meetings?scope=todo khi chưa đăng nhập', 'GET /api/meetings?scope=mine bằng token thuky@'],
      expected: '401; trang đăng nhập; 400 MSG32 (docs/03-api/05: giá trị scope khác all/todo, kể cả mine, trả 400 MSG32)',
      async run(qa) {
        qa.expectStatus(await (await qa.api('anon')).get('/api/meetings?scope=todo&pageSize=1'), 401)
        const bad = await (await qa.api('secretary')).get('/api/meetings?scope=mine&pageSize=1')
        qa.expectStatus(bad, 400)
        qa.expect(bad.body.code).toBe('MSG32')
        await qa.goto('/mgmt/meetings?scope=todo')
        await qa.expect(qa.page).toHaveURL(/\/login/)
        await qa.expect(qa.page.getByRole('button', { name: 'Đăng nhập bằng mật khẩu' })).toBeVisible()
        await qa.shot('chua-dang-nhap')
      },
    },
  ],
}

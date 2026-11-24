// FE-16 — Lịch làm việc & tìm giờ họp (QA subtask SCRUM-203): the working calendar (UC-16 AF-4, Report 3 §4.1–4.2),
// calendar sharing (UC-80a → UC-80f), the shared week (UC-81c) and finding a time (UC-81a, UC-81b). Criteria come from
// docs/01-requirements/05-use-case-specifications.md, BR-140, BR-141, BR-144–BR-147, BR-142, BR-143, BR-010, BR-148, docs/03-api/23-chia-se-lich.md and
// docs/03-api/24-tim-gio-hop.md; ADR-0058.
//
// The criteria are stateful and run in order (the share flow is one story told by two people). Each one reads the state
// it needs from the API, so a criterion run alone with --only does its own preparation where it can.
//
// Data the plan creates, all its own:
// - four "[QA] FE-16 …" meetings next Tuesday, created by the secretary seed (thuky@) with quantri@ as Chair and
//   thanhvien@ as Member: Normal 09:00–10:00, Draft 10:30–11:30, Internal 13:30–14:30, Secret 15:30–16:30; the three
//   non-draft ones are submitted for approval (WaitingApproval counts as busy, BR-146, BR-147);
// - one draft meeting created from a slot by chutoa@ (UC-81b);
// - calendar shares thanhvien@ → chutoa@ and quantri@ → chutoa@ (no such pair is open before the plan runs);
// - one account "[QA] FE-16 người xem tạm" (qa-fe16-viewer@trustcabinet.local, role Thư ký) to see a share stop when an
//   account is deactivated (BR-142, BR-143). Accounts are never deleted; the plan leaves it deactivated and re-uses it.
// CLEANUP cancels every meeting and ends or withdraws every share the plan opened.

const PASSWORD = process.env.QA_PASSWORD ?? 'TrustCabinet@2026'
const EMAIL = {
  admin: 'quantri@trustcabinet.local',
  chair: 'chutoa@trustcabinet.local',
  secretary: 'thuky@trustcabinet.local',
  member: 'thanhvien@trustcabinet.local',
}
const TEMP_VIEWER = 'qa-fe16-viewer@trustcabinet.local'

// ---- dates, in Vietnam's calendar as the server counts them ----
const vnToday = () =>
  new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Ho_Chi_Minh', year: 'numeric', month: '2-digit', day: '2-digit' }).format(
    new Date(),
  )
const addDays = (day, n) => new Date(Date.UTC(+day.slice(0, 4), +day.slice(5, 7) - 1, +day.slice(8, 10) + n)).toISOString().slice(0, 10)
const mondayOf = (day) => {
  const wd = new Date(`${day}T00:00:00Z`).getUTCDay()
  return addDays(day, wd === 0 ? -6 : 1 - wd)
}
const TODAY = vnToday()
// Four weeks ahead: always in the future, and clear of next week, where other QA runs park their meetings.
const MON = addDays(mondayOf(TODAY), 28)
const TUE = addDays(MON, 1)
const WED = addDays(MON, 2)
const THU = addDays(MON, 3)
const FRI = addDays(MON, 4)
const SUN = addDays(MON, 6)
const dm = (day) => `${day.slice(8, 10)}/${day.slice(5, 7)}`
const at = (day, hhmm) => `${day}T${hhmm}:00+07:00`

// The four meetings of the owner (thanhvien@) next Tuesday.
const MEETINGS = [
  { key: 'normal', title: 'FE-16 họp Thường', sensitivity: 'Normal', from: '09:00', to: '10:00', submit: true },
  { key: 'draft', title: 'FE-16 họp Nháp', sensitivity: 'Normal', from: '10:30', to: '11:30', submit: false },
  { key: 'internal', title: 'FE-16 họp Nội bộ', sensitivity: 'Internal', from: '13:30', to: '14:30', submit: true },
  { key: 'secret', title: 'FE-16 họp Mật', sensitivity: 'Secret', from: '15:30', to: '16:30', submit: true },
]

// Cross-criterion memory: ids of what this run created, for CLEANUP.
const made = { meetings: new Map(), slotDraftId: null }

// ---- helpers ----
/** Types a dd/mm/yyyy day into a DateField and commits it (blur). */
async function typeDay(locator, day) {
  await locator.fill(`${day.slice(8, 10)}/${day.slice(5, 7)}/${day.slice(0, 4)}`)
  await locator.press('Tab')
}

/** Sets the find page's date range to the test week (the settings fold away on a phone). */
async function findWeek(qa) {
  if (qa.device === 'phone') await qa.page.getByRole('button', { name: 'Sửa điều kiện' }).click()
  await typeDay(qa.page.getByLabel('Từ ngày'), MON)
  await typeDay(qa.page.getByLabel('Đến ngày'), FRI)
  if (qa.device === 'phone') await qa.page.getByRole('button', { name: 'Thu gọn điều kiện' }).click()
}

async function me(qa, role) {
  const res = await (await qa.api(role)).get('/api/auth/me')
  qa.expectStatus(res, 200)
  return res.body
}

async function noSidewaysScroll(qa) {
  const sideways = await qa.page.evaluate(() => document.documentElement.scrollWidth - innerWidth)
  qa.expect(sideways, 'the page scrolls sideways').toBeLessThanOrEqual(0)
}

async function noRawCodes(qa) {
  const text = await qa.page.locator('main').innerText()
  qa.expect(text, 'a raw MSG code is shown to the user').not.toMatch(/MSG\d+/)
}

/** The page shows none of the owner's meeting titles: a shared view is busy/free only (BR-140, BR-141). */
async function noMeetingContent(qa) {
  const text = await qa.page.locator('main').innerText()
  for (const m of MEETINGS) qa.expect(text, `meeting title "${m.title}" leaked`).not.toContain(m.title)
  qa.expect(text).not.toMatch(/\[QA\] FE-16 họp/)
}

async function settled(qa) {
  await qa.page.waitForFunction(() => document.getAnimations().every((a) => a.playState !== 'running'))
}

/** The toast stack's message (scoped outside the dialogs and the page body). */
const toast = (qa, text) => qa.page.locator('.toast, [role="status"]').filter({ hasText: text }).first()

/** All shares the role takes part in. */
async function shares(qa, role) {
  const res = await (await qa.api(role)).get('/api/calendar/shares')
  qa.expectStatus(res, 200)
  return [...res.body.asOwner, ...res.body.asViewer]
}

/** The newest share of the pair, or undefined. `open` keeps only Pending / Effective. */
async function pairShare(qa, role, ownerEmail, viewerEmail, { open = false } = {}) {
  const all = await shares(qa, role)
  return all.find(
    (s) => s.owner.email === ownerEmail && s.viewer.email === viewerEmail && (!open || ['Pending', 'Effective'].includes(s.status)),
  )
}

/** Closes an open share of the pair, whoever may: withdraw a Pending one as its sender, else end it. */
async function closePair(qa, ownerRole, viewerRole) {
  const s = await pairShare(qa, ownerRole, EMAIL[ownerRole] ?? ownerRole, EMAIL[viewerRole] ?? viewerRole, { open: true })
  if (!s) return null
  const sender = s.initiatedBy === 'Viewer' ? viewerRole : ownerRole
  const asked = sender === viewerRole ? ownerRole : viewerRole
  if (s.status === 'Pending') {
    const r = await (await qa.api(sender)).post(`/api/calendar/shares/${s.id}/withdraw`)
    if (r.status !== 200) await (await qa.api(asked)).post(`/api/calendar/shares/${s.id}/decline`)
  } else {
    await (await qa.api(ownerRole)).post(`/api/calendar/shares/${s.id}/end`)
  }
  return s.id
}

/** Cancels every open [QA] meeting matching one of the title queries (all pages). */
async function sweepMeetings(qa, api, queries, reason) {
  for (const q of queries) {
    for (let page = 1; ; page++) {
      const found = await api.get(`/api/meetings?q=${encodeURIComponent(q)}&pageSize=50&page=${page}`)
      qa.expectStatus(found, 200)
      for (const m of found.body.items ?? []) {
        if (['Cancelled', 'Ended'].includes(m.status)) continue
        const r = await api.post(`/api/meetings/${m.id}/transitions`, { action: 'Cancel', reason })
        if (r.status !== 200) qa.note(`Không huỷ được cuộc họp sót ${m.id} (${m.title}, ${r.status}); để lại.`)
      }
      if (page >= (found.body.totalPages ?? 1)) break
    }
  }
}

/** Creates the owner's four meetings once per run (or re-finds this run's ones). */
async function ensureMeetings(qa) {
  if (made.meetings.size === MEETINGS.length) return made.meetings
  const sec = await qa.api('secretary')
  // Titles are stable across runs: cancel leftovers of earlier runs first (--only reruns skip CLEANUP).
  await sweepMeetings(qa, sec, ['FE-16 họp', 'nháp từ khung giờ', 'chan khung', 'doi chu toa', 'bo soan', 'chap nhan', 'FE-16 probe'], 'Dọn kiểm thử FE-16 (sót lần trước).')
  const admin = await me(qa, 'admin')
  const member = await me(qa, 'member')
  const chair = await me(qa, 'chair')
  for (const m of MEETINGS) {
    const res = await sec.post('/api/meetings', {
      title: qa.name(m.title),
      plannedStartTime: at(TUE, m.from),
      plannedEndTime: at(TUE, m.to),
      mode: 'InPerson',
      location: 'Phòng QA',
      onlineLink: null,
      sensitivity: m.sensitivity,
      description: 'Dữ liệu kiểm thử FE-16, sẽ huỷ sau khi chạy.',
      participants: [
        { userId: admin.id, role: 'Chair' },
        { userId: member.id, role: 'Member' },
      ],
    })
    qa.expectStatus(res, 201)
    const id = res.body.meeting.id
    made.meetings.set(m.key, id)
    if (m.submit) {
      const sub = await sec.post(`/api/meetings/${id}/transitions`, { action: 'SubmitForApproval', assignedApproverId: chair.id })
      qa.expectStatus(sub, 200)
      qa.expect(sub.body.status ?? sub.body.meeting?.status).toBe('WaitingApproval')
    }
  }
  return made.meetings
}

/** Makes sure thanhvien@ → chutoa@ is Effective at `level`, ending on WED (for criteria run alone). */
async function ensureMemberShare(qa, level = 'Level1') {
  let s = await pairShare(qa, 'member', EMAIL.member, EMAIL.chair, { open: true })
  if (!s) {
    const chair = await me(qa, 'chair')
    const r = await (await qa.api('member')).post('/api/calendar/shares/direct', { viewerId: chair.id, startDate: TODAY, endDate: WED })
    qa.expectStatus(r, 201)
    s = r.body.share
  }
  if (s.status === 'Pending') qa.expectStatus(await (await qa.api('chair')).post(`/api/calendar/shares/${s.id}/accept`), 200)
  const owner = await qa.api('member')
  s = await pairShare(qa, 'member', EMAIL.member, EMAIL.chair, { open: true })
  // A crashed earlier run may have left a change waiting: apply it first so the PATCH below is valid.
  if (s.pendingChange) {
    qa.expectStatus(await owner.post(`/api/calendar/shares/${s.id}/accept`), 200)
    s = await pairShare(qa, 'member', EMAIL.member, EMAIL.chair, { open: true })
  }
  const patch = {}
  if (s.endDate !== WED) patch.endDate = WED
  if (s.level === 'Level2' && level === 'Level1') patch.level = 'Level1'
  if (Object.keys(patch).length) qa.expectStatus(await owner.patch(`/api/calendar/shares/${s.id}`, patch), 200)
  if (s.level === 'Level1' && level === 'Level2') {
    qa.expectStatus(await (await qa.api('chair')).patch(`/api/calendar/shares/${s.id}`, { level: 'Level2' }), 201)
    qa.expectStatus(await owner.post(`/api/calendar/shares/${s.id}/accept`), 200)
  }
  return pairShare(qa, 'member', EMAIL.member, EMAIL.chair, { open: true })
}

/** POST /api/calendar/slots as the chair. */
async function slots(qa, body) {
  return (await qa.api('chair')).post('/api/calendar/slots', body)
}
const startsOn = (res, day) => (res.body.days.find((d) => d.date === day)?.slots ?? []).map((s) => s.start.slice(11, 16))

/**
 * The working-calendar stage for AC25/AC26: five overlapping meetings on WED 09:00–10:00
 * (the first one Online) plus one cancelled, all WaitingApproval/Cancelled, created by the
 * secretary seed. Ids go to `made.ac25`; AC26 cancels what is still open.
 */
async function ensureAc25(qa) {
  if (made.ac25?.length === 6) return made.ac25
  made.ac25 = []
  const sec = await qa.api('secretary')
  const admin = await me(qa, 'admin')
  const member = await me(qa, 'member')
  const chair = await me(qa, 'chair')
  const secretary = await me(qa, 'secretary')
  const adminApi = await qa.api('admin')
  // Sweep leftovers of earlier runs (titles are stable across runs; CLEANUP only
  // covers the current run, and --only reruns skip it). Page through: dozens accumulate.
  for (const q of ['FE-16 chong', 'FE-16 huy']) {
    for (let page = 1; ; page++) {
      const found = await sec.get(`/api/meetings?q=${encodeURIComponent(q)}&pageSize=50&page=${page}`)
      qa.expectStatus(found, 200)
      for (const m of found.body.items ?? []) {
        if (['Cancelled', 'Ended'].includes(m.status)) continue
        if (m.status === 'InProgress') {
          // Only the meeting's Chair (here quantri@) ends it; cancelling a live meeting fails.
          const r = await adminApi.post(`/api/meetings/${m.id}/transitions`, { action: 'End' })
          if (r.status !== 200) qa.note(`Không kết thúc được cuộc họp sót ${m.id} (${r.status}); để lại.`)
          continue
        }
        const r = await sec.post(`/api/meetings/${m.id}/transitions`, { action: 'Cancel', reason: 'Dọn kiểm thử FE-16 AC25 (sót lần trước).' })
        if (r.status !== 200) qa.note(`Không huỷ được cuộc họp sót ${m.id} (${r.status}); để lại.`)
      }
      if (page >= (found.body.totalPages ?? 1)) break
    }
  }
  const mk = async (title, from, to, mode) => {
    const res = await sec.post('/api/meetings', {
      title: qa.name(title),
      plannedStartTime: at(WED, from),
      plannedEndTime: at(WED, to),
      mode,
      location: mode === 'InPerson' ? 'Phòng QA' : null,
      onlineLink: null,
      sensitivity: 'Normal',
      description: 'Dữ liệu kiểm thử FE-16 AC25, sẽ huỷ sau khi chạy.',
      participants: [
        { userId: admin.id, role: 'Chair' },
        { userId: member.id, role: 'Member' },
        // A Secretary participant: Publish requires one active Chair and one active
        // Secretary (MeetingService), and the live-tile check starts as the secretary.
        { userId: secretary.id, role: 'Secretary' },
      ],
    })
    qa.expectStatus(res, 201)
    return res.body.meeting.id
  }
  for (let i = 1; i <= 5; i++) made.ac25.push(await mk(`FE-16 chong ${i}`, '09:00', '10:00', i === 1 ? 'Online' : 'InPerson'))
  for (const id of made.ac25) {
    const sub = await sec.post(`/api/meetings/${id}/transitions`, { action: 'SubmitForApproval', assignedApproverId: chair.id })
    qa.expectStatus(sub, 200)
  }
  const cancelled = await mk('FE-16 huy', '11:00', '12:00', 'InPerson')
  qa.expectStatus(await sec.post(`/api/meetings/${cancelled}/transitions`, { action: 'Cancel', reason: 'Dọn kiểm thử FE-16 AC25.' }), 200)
  made.ac25.push(cancelled)
  return made.ac25
}

/** The keys of every object in a JSON tree: a shared view must carry no meeting field (BR-140, BR-141). */
function allKeys(value, out = new Set()) {
  if (Array.isArray(value)) value.forEach((v) => allKeys(v, out))
  else if (value && typeof value === 'object')
    for (const [k, v] of Object.entries(value)) {
      out.add(k)
      allKeys(v, out)
    }
  return out
}
const MEETING_FIELDS = ['title', 'location', 'sensitivity', 'meetingId', 'participants', 'attendees', 'description', 'onlineLink']

/**
 * Safety prologue for the role-mutating ACs (AC31/AC32): a crashed earlier run may have left
 * the chair role without MeetingCreator. Restores it (union, never removing anything else) and
 * returns the role row for the caller to strip and restore.
 */
async function ensureChairCreator(qa) {
  const adminApi = await qa.api('admin')
  const users = await adminApi.get('/api/users?page=1&pageSize=100')
  qa.expectStatus(users, 200)
  const list = Array.isArray(users.body) ? users.body : users.body.items
  const chairUser = list.find((u) => u.email === EMAIL.chair)
  const roleId = chairUser.roleId ?? chairUser.role?.id
  const matrix = await adminApi.get('/api/roles/capabilities')
  qa.expectStatus(matrix, 200)
  const role = matrix.body.roles.find((r) => r.id === roleId)
  if (!role.capabilities.includes('MeetingCreator')) {
    qa.expectStatus(
      await adminApi.put('/api/roles/capabilities', { roleId, capabilities: [...role.capabilities, 'MeetingCreator'] }),
      200,
    )
    qa.note('Đã trả MeetingCreator cho vai trò Chủ toạ còn thiếu từ lần chạy trước.')
    const fresh = await adminApi.get('/api/roles/capabilities')
    qa.expectStatus(fresh, 200)
    return fresh.body.roles.find((r) => r.id === roleId)
  }
  return role
}

/** Opens the "Chia sẻ lịch" page from the account menu at the foot of the sidebar (UC-80f step 1). */
async function openSharingFromMenu(qa, fullName) {
  await qa.goto('/calendar')
  if (qa.device === 'phone') {
    // Below 720px the sidebar is a drawer: open it first when it has a toggle.
    const burger = qa.page.getByRole('button', { name: /Mở menu|Mở rộng menu|Menu/ }).first()
    if (await burger.isVisible().catch(() => false)) await burger.click()
  }
  await qa.page.getByLabel(`Tài khoản ${fullName}`).click()
  await qa.page.getByRole('link', { name: 'Chia sẻ lịch' }).click()
  await qa.expect(qa.page).toHaveURL(/\/calendar\/sharing/)
  await qa.expect(qa.page.getByRole('heading', { name: 'Chia sẻ lịch', level: 1 })).toBeVisible()
}

const section = (qa, title) => qa.page.locator('section.share-section').filter({ has: qa.page.getByRole('heading', { name: title }) })

export default {
  ticket: 'SCRUM-203',
  title: 'FE-16 Lịch làm việc & tìm giờ họp: lịch tuần, chia sẻ lịch theo mốc, lịch được chia sẻ, tìm giờ họp',
  devices: ['desktop'],
  criteria: [
    // ------------------------------------------------------------------ UC-80f, the page for everyone
    {
      id: 'AC1',
      role: 'member',
      devices: ['desktop', 'phone'],
      title: 'Thành viên (không có quyền tạo cuộc họp) mở Chia sẻ lịch từ menu tài khoản: chỉ có danh sách người xem lịch của mình',
      steps: ['Đăng nhập thanhvien@', 'Menu tài khoản → Chia sẻ lịch'],
      expected:
        'Trang "Chia sẻ lịch" có thẻ "Người xem được lịch của tôi" và nút "Chia sẻ lịch"; không có thẻ "Lịch được chia sẻ với tôi", không có nút "Xin xem lịch", không có tab "Lịch được chia sẻ" (UC-80f AF-2, BR-140, BR-141); không cuộn ngang',
      async run(qa) {
        const caps = await qa.capabilities()
        if (caps.capabilities.includes('MeetingCreator'))
          qa.inconclusive(`thanhvien@ giữ vai trò "${caps.roleName}" có MeetingCreator trên database này; trả về vai trò Thành viên rồi chạy lại.`)
        const user = await me(qa, 'member')
        await openSharingFromMenu(qa, user.fullName)
        await qa.expect(qa.page.getByRole('heading', { name: /Người xem được lịch của tôi/ })).toBeVisible()
        await qa.expect(qa.page.getByRole('button', { name: 'Chia sẻ lịch', exact: true })).toBeVisible()
        await qa.expect(qa.page.getByRole('heading', { name: /Lịch được chia sẻ với tôi/ })).toHaveCount(0)
        await qa.expect(qa.page.getByRole('button', { name: 'Xin xem lịch' })).toHaveCount(0)
        await qa.expect(qa.page.getByRole('radiogroup', { name: 'Chia sẻ lịch' })).toHaveCount(0)
        await settled(qa)
        await qa.shot('chia-se-lich-thanh-vien', { fullPage: true })
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
      },
    },
    // ------------------------------------------------------------------ UC-80b
    {
      id: 'AC2',
      role: 'member',
      title: 'Chủ lịch tự chia sẻ lịch cho người tổ chức họp: luôn Mốc 1 (Họp Thường), chờ bên nhận đồng ý',
      steps: [
        'Đăng nhập thanhvien@, mở Chia sẻ lịch',
        'Chuẩn bị: chia sẻ trực tiếp cho thuky@ (API) để có người "Đã có"',
        'Bấm "Chia sẻ lịch", tìm "thuky": người đã có chia sẻ bị làm mờ "Đã có", không chọn được; rút chia sẻ đó',
        'Tìm "chutoa", chọn Chủ toạ, giữ thời gian mặc định, bấm "Chia sẻ"',
      ],
      expected:
        'Hộp thoại không có chọn mức (luôn Họp Thường); thuky@ (đã có chia sẻ) hiện "Đã có", không chọn được; toast MSG441 "Đã chia sẻ lịch. Đang chờ bên nhận đồng ý."; dòng chutoa@ "Chờ trả lời", "Họp Thường"; API: Pending, Level1',
      async run(qa) {
        // Start clean: an open share of this pair can only be this plan's own, left by an earlier run.
        const old = await closePair(qa, 'member', 'chair')
        if (old) qa.note(`Đã đóng chia sẻ thanhvien→chutoa còn mở từ lần chạy trước (${old}).`)
        const chair = await me(qa, 'chair')
        const secretary = await me(qa, 'secretary')
        // The "Đã có" dimming (BR-142, BR-143) needs someone with an open share: share with thuky@ first,
        // withdraw it right after the dimming check, then continue with chutoa@.
        let setupShare = await pairShare(qa, 'member', EMAIL.member, EMAIL.secretary, { open: true })
        if (!setupShare) {
          const created = await (await qa.api('member')).post('/api/calendar/shares/direct', { viewerId: secretary.id, startDate: TODAY, endDate: addDays(TODAY, 6) })
          qa.expectStatus(created, 201)
          setupShare = created.body.share
        }
        await qa.goto('/calendar/sharing')
        await section(qa, 'Người xem được lịch của tôi').getByRole('button', { name: 'Chia sẻ lịch', exact: true }).click()
        const dialog = qa.page.getByRole('dialog', { name: 'Chia sẻ lịch của tôi' })
        await qa.expect(dialog).toBeVisible()
        await qa.expect(dialog.getByRole('group', { name: 'Mức xem' })).toHaveCount(0)
        await qa.expect(dialog.getByText('Mức xem', { exact: true })).toHaveCount(0)
        // CAL_FT21: the Member without MeetingCreator is never listed (BR-140, BR-141 UC-80b EX-1).
        // Full email: a bare name could also match other QA accounts on a shared database.
        await dialog.getByRole('searchbox').fill(EMAIL.member)
        await qa.expect(dialog.getByText('Không có ai khớp.')).toBeVisible()
        await qa.shot('thanh-vien-khong-duoc-chia-se')
        // A person with an open share to them is listed but cannot be picked (BR-142, BR-143).
        await dialog.getByRole('searchbox').fill('thuky')
        const thuky = dialog.locator('label.pick-row').filter({ hasText: EMAIL.secretary })
        await qa.expect(thuky).toBeVisible()
        await qa.expect(thuky.getByRole('radio')).toBeDisabled()
        await qa.expect(thuky.getByText('Đã có')).toBeVisible()
        await qa.shot('da-co-chia-se-bi-lam-mo')
        qa.expectStatus(await (await qa.api('member')).post(`/api/calendar/shares/${setupShare.id}/withdraw`), 200)
        await dialog.getByRole('searchbox').fill('chutoa')
        const row = dialog.locator('label.pick-row').filter({ hasText: EMAIL.chair })
        await qa.expect(row).toBeVisible()
        await row.getByRole('radio').check()
        await settled(qa)
        await qa.shot('chon-nguoi-nhan')
        await dialog.getByRole('button', { name: 'Chia sẻ', exact: true }).click()
        await qa.expect(toast(qa, 'Đã chia sẻ lịch. Đang chờ bên nhận đồng ý.')).toBeVisible()
        await qa.expect(dialog).toHaveCount(0)
        const s = await pairShare(qa, 'member', EMAIL.member, EMAIL.chair, { open: true })
        const line = section(qa, 'Người xem được lịch của tôi').locator(`#share-${s.id}`)
        await qa.expect(line).toContainText(chair.fullName)
        await qa.expect(line.getByText('Chờ trả lời', { exact: true })).toBeVisible()
        await qa.expect(line.getByText('Họp Thường')).toBeVisible()
        await qa.shot('da-chia-se-cho-tra-loi')
        await noRawCodes(qa)
        qa.expect(s?.status).toBe('Pending')
        qa.expect(s?.level).toBe('Level1')
        qa.expect(s?.initiatedBy).toBe('Owner')
        qa.expect(s?.actions).toEqual(['withdraw'])
      },
    },
    // ------------------------------------------------------------------ UC-80c
    {
      id: 'AC3',
      role: 'chair',
      title: 'Người tổ chức nhận chia sẻ: thấy "Chờ bạn trả lời", Đồng ý → Đang chia sẻ; người gửi được báo',
      steps: ['Đăng nhập chutoa@, mở Chia sẻ lịch', 'Thẻ "Lịch được chia sẻ với tôi": dòng Lê An → Đồng ý → Đồng ý'],
      expected:
        'Dòng "Chờ bạn trả lời" có Từ chối / Đồng ý; hộp thoại "Nhận chia sẻ lịch" chỉ hiện Mức xem và Thời gian; toast MSG436 "Đã chấp nhận chia sẻ lịch."; dòng chuyển "Đang chia sẻ"; thanhvien@ có thông báo CalendarShareAccepted (BR-010); ?share=<id> tô sáng đúng dòng; ?share lạ: "Chia sẻ này không còn." và hết ?share',
      async run(qa) {
        let s = await pairShare(qa, 'chair', EMAIL.member, EMAIL.chair, { open: true })
        if (!s) {
          const chair = await me(qa, 'chair')
          const r = await (await qa.api('member')).post('/api/calendar/shares/direct', { viewerId: chair.id, startDate: TODAY, endDate: addDays(TODAY, 29) })
          qa.expectStatus(r, 201)
          s = r.body.share
        }
        qa.expect(s.status, 'the share is not waiting for an answer').toBe('Pending')
        const owner = s.owner
        // CAL_FT23 (deep link): a notification lands on ?share=<id>, scrolls to the row and highlights it.
        await qa.goto(`/calendar/sharing?share=${s.id}`)
        const focusRow = qa.page.locator(`#share-${s.id}`)
        await qa.expect(focusRow).toContainText(owner.email)
        await qa.expect(focusRow).toHaveClass(/share-row-focus/)
        await settled(qa)
        await qa.shot('mo-tu-chia-se-focus')
        await qa.goto('/calendar/sharing')
        const card = section(qa, 'Lịch được chia sẻ với tôi')
        const line = card.locator(`#share-${s.id}`)
        await qa.expect(line).toContainText(owner.email)
        await qa.expect(line.getByText('Chờ bạn trả lời')).toBeVisible()
        await qa.expect(line.getByRole('button', { name: 'Từ chối' })).toBeVisible()
        await qa.shot('cho-ban-tra-loi')
        await line.getByRole('button', { name: 'Đồng ý' }).click()
        const dialog = qa.page.getByRole('dialog', { name: 'Nhận chia sẻ lịch' })
        await qa.expect(dialog).toBeVisible()
        await qa.expect(dialog.getByText('Họp Thường')).toBeVisible()
        await settled(qa)
        await qa.shot('hop-thoai-nhan-chia-se')
        await dialog.getByRole('button', { name: 'Đồng ý' }).click()
        await qa.expect(toast(qa, 'Đã chấp nhận chia sẻ lịch.')).toBeVisible()
        await qa.expect(line.getByText('Đang chia sẻ')).toBeVisible()
        await qa.shot('dang-chia-se')
        await noRawCodes(qa)
        const after = await pairShare(qa, 'chair', EMAIL.member, EMAIL.chair, { open: true })
        qa.expect(after.status).toBe('Effective')
        qa.expect(after.actions.sort()).toEqual(['end', 'requestChange'])
        const notes = await (await qa.api('member')).get('/api/notifications?page=1&pageSize=50')
        qa.expectStatus(notes, 200)
        const hit = notes.body.items.find((n) => n.type === 'CalendarShareAccepted' && n.targetId === s.id)
        qa.expect(hit, 'thanhvien@ got no CalendarShareAccepted notification for this share').toBeTruthy()
        // CAL_FT37 E2: a link to a share the lists do not have says so and drops ?share from the URL.
        await qa.goto('/calendar/sharing?share=00000000-0000-0000-0000-000000000000')
        await qa.expect(toast(qa, 'Chia sẻ này không còn.')).toBeVisible()
        await qa.expect(new URL(qa.page.url()).searchParams.get('share')).toBeNull()
        await qa.shot('chia-se-khong-con')
        await noRawCodes(qa)
      },
    },
    // ------------------------------------------------------------------ UC-80d AF-1, validation, duplicates
    {
      id: 'AC4',
      role: 'member',
      title: 'Chủ lịch đổi ngay khoảng ngày; dữ liệu sai bị từ chối đúng mã, không lưu',
      steps: [
        `Chủ lịch PATCH đặt ngày kết thúc ${WED}`,
        'Chia sẻ lại cho cùng người khi đang hiệu lực',
        'Chia sẻ cho người không có quyền tạo cuộc họp / cho chính mình',
        'Chia sẻ với ngày bắt đầu hôm qua; với khoảng 366 ngày',
        'Bên xem PATCH không tăng gì; bên xem hạ mốc',
      ],
      expected:
        '200 MSG442 "Đã cập nhật chia sẻ lịch." và bên xem thấy ngày mới; trùng → 409 MSG449; người không nhận được → 400 MSG455; khoảng sai → 400 MSG443; bên xem không tăng gì → 400 MSG443',
      async run(qa) {
        const s = await ensureMemberShare(qa, 'Level1')
        const owner = await qa.api('member')
        const viewer = await qa.api('chair')
        const chair = await me(qa, 'chair')
        const secretaryOfNothing = await me(qa, 'member')
        const wrong = []
        const check = (label, res, status, code) => {
          if (res.status !== status || (code && res.body?.code !== code))
            wrong.push(`${label}: ${res.status} ${JSON.stringify(res.body).slice(0, 200)} (mong đợi ${status} ${code ?? ''})`)
        }
        // The owner shortened it in ensureMemberShare when it was not WED yet; set it again to see the answer.
        const other = s.endDate === WED ? addDays(WED, 1) : WED
        check('PATCH owner endDate', await owner.patch(`/api/calendar/shares/${s.id}`, { endDate: other }), 200, 'MSG442')
        check('PATCH owner endDate back', await owner.patch(`/api/calendar/shares/${s.id}`, { endDate: WED }), 200, 'MSG442')
        const seen = await pairShare(qa, 'chair', EMAIL.member, EMAIL.chair, { open: true })
        if (seen.endDate !== WED) wrong.push(`bên xem thấy endDate ${seen.endDate}, mong đợi ${WED}`)
        const ok = { startDate: TODAY, endDate: addDays(TODAY, 6) }
        check('direct trùng', await owner.post('/api/calendar/shares/direct', { viewerId: chair.id, ...ok }), 409, 'MSG449')
        const admin = await qa.api('admin')
        const adminMe = await me(qa, 'admin')
        // admin → member: the member has no MeetingCreator, cannot receive a share (UC-80b EX-1).
        check('direct cho Thành viên', await admin.post('/api/calendar/shares/direct', { viewerId: secretaryOfNothing.id, ...ok }), 400, 'MSG455')
        check('direct cho chính mình', await admin.post('/api/calendar/shares/direct', { viewerId: adminMe.id, ...ok }), 400, 'MSG455')
        check(
          'direct bắt đầu hôm qua',
          await admin.post('/api/calendar/shares/direct', { viewerId: chair.id, startDate: addDays(TODAY, -1), endDate: addDays(TODAY, 6) }),
          400,
          'MSG443',
        )
        check(
          'direct 366 ngày',
          await admin.post('/api/calendar/shares/direct', { viewerId: chair.id, startDate: TODAY, endDate: addDays(TODAY, 365) }),
          400,
          'MSG443',
        )
        check('viewer PATCH không tăng gì', await viewer.patch(`/api/calendar/shares/${s.id}`, { endDate: WED }), 400, 'MSG443')
        const msg = (await owner.patch(`/api/calendar/shares/${s.id}`, { endDate: addDays(WED, 2) })).body
        if (msg?.message !== 'Đã cập nhật chia sẻ lịch.') wrong.push(`MSG442 message: ${JSON.stringify(msg?.message)}`)
        await owner.patch(`/api/calendar/shares/${s.id}`, { endDate: WED })
        // Nothing from the refused calls was saved: admin has no share to chutoa.
        const adminShare = await pairShare(qa, 'admin', EMAIL.admin, EMAIL.chair, { open: true })
        if (adminShare) wrong.push(`một chia sẻ quantri→chutoa đang mở sau các lời gọi bị từ chối: ${adminShare.status}`)
        qa.expect(wrong, wrong.join('\n')).toEqual([])
      },
    },
    // ------------------------------------------------------------------ privacy: a third person
    {
      id: 'AC5',
      role: 'secretary',
      title: 'Người thứ ba (không phải hai bên của chia sẻ) không thấy, không thao tác được chia sẻ: 404 không body',
      steps: ['thuky@ gọi accept / decline / withdraw / end / PATCH trên chia sẻ thanhvien→chutoa', 'thuky@ GET /api/calendar/shares'],
      expected: 'Mỗi lời gọi 404 không body (không lộ chia sẻ hay tài khoản, POST-3); danh sách của thuky@ không có chia sẻ này; chia sẻ không đổi',
      async run(qa) {
        const s = await ensureMemberShare(qa, 'Level1')
        const api = await qa.api()
        const wrong = []
        for (const [method, path, body] of [
          ['post', `/api/calendar/shares/${s.id}/accept`],
          ['post', `/api/calendar/shares/${s.id}/decline`],
          ['post', `/api/calendar/shares/${s.id}/withdraw`],
          ['post', `/api/calendar/shares/${s.id}/end`],
          ['patch', `/api/calendar/shares/${s.id}`, { level: 'Level2' }],
        ]) {
          const res = await api[method](path, body)
          if (res.status !== 404 || (res.body && String(res.body).trim() !== '' && typeof res.body === 'object'))
            wrong.push(`${method.toUpperCase()} ${path} → ${res.status} ${JSON.stringify(res.body).slice(0, 150)}`)
        }
        const mine = await shares(qa, 'secretary')
        if (mine.some((x) => x.id === s.id)) wrong.push('thuky@ thấy chia sẻ của người khác trong danh sách')
        const after = await pairShare(qa, 'member', EMAIL.member, EMAIL.chair, { open: true })
        if (after.status !== 'Effective' || after.level !== s.level || after.endDate !== s.endDate) wrong.push('chia sẻ bị đổi')
        qa.expect(wrong, wrong.join('\n')).toEqual([])
      },
    },
    // ------------------------------------------------------------------ UC-81c at Mốc 1
    {
      id: 'AC6',
      role: 'chair',
      devices: ['desktop', 'phone'],
      title: 'Lịch được chia sẻ (Mốc 1): chỉ giờ bận họp Thường; họp Nội bộ, Mật, Nháp không hiện; ngày ngoài khoảng là "Chưa biết"; không lộ tên cuộc họp',
      steps: [
        `Chủ lịch thanhvien@ có 4 cuộc họp ${dm(TUE)}: Thường 09:00–10:00 (chờ duyệt), Nháp 10:30–11:30, Nội bộ 13:30–14:30, Mật 15:30–16:30`,
        `Chia sẻ Mốc 1 đến ${dm(WED)}`,
        `chutoa@ mở Chia sẻ lịch → tab "Lịch được chia sẻ", tuần ${dm(MON)} – ${dm(SUN)} (đủ 7 ngày)`,
      ],
      expected: `Chip "Lê An · Họp Thường · đến ${dm(WED)}"; T3 ${dm(TUE)} chỉ có khối "bận 09:00–10:00"; không có khối 10:30, 13:30, 15:30; T5 ${dm(THU)}, T6 ${dm(FRI)} ghi "Chưa biết"; không có tên, địa điểm cuộc họp (BR-140, BR-141, BR-144–BR-147); API không có trường cuộc họp; không cuộn ngang`,
      async run(qa) {
        await ensureMeetings(qa)
        const s = await ensureMemberShare(qa, 'Level1')
        const name = s.owner.fullName
        const week = await (await qa.api()).get(`/api/calendar/shared-week?week=${TUE}`)
        qa.expectStatus(week, 200)
        const keys = [...allKeys(week.body)]
        qa.expect(keys.filter((k) => MEETING_FIELDS.includes(k)), 'shared-week carries a meeting field').toEqual([])
        const row = week.body.people.find((p) => p.shareId === s.id)
        qa.expect(row?.level).toBe('Level1')
        const tue = row.days.find((d) => d.date === TUE)
        const busy = tue.busy.map((b) => `${b.start.slice(11, 16)}–${b.end.slice(11, 16)}`)
        qa.expect(busy, 'the Normal meeting is not busy').toContain('09:00–10:00')
        for (const hidden of ['10:30–11:30', '13:30–14:30', '15:30–16:30']) qa.expect(busy, `${hidden} must not be busy at Mốc 1`).not.toContain(hidden)
        qa.expect(row.days.find((d) => d.date === THU)?.known).toBe(false)
        qa.expect(row.days.find((d) => d.date === FRI)?.known).toBe(false)

        await qa.goto(`/calendar/sharing?tab=week&week=${TUE}`)
        // The shared week is the whole week, Monday to Sunday (Hải 19/11, PR #422).
        await qa.expect(qa.page.getByRole('heading', { name: new RegExp(`${dm(MON)} – ${dm(SUN)}`) })).toBeVisible()
        const chip = qa.page.getByRole('group', { name: 'Người chia sẻ' }).getByRole('button', { name: new RegExp(name) })
        await qa.expect(chip).toContainText(`Họp Thường · đến ${dm(WED)}`)
        if (qa.device === 'phone') {
          // Seven days across a phone leave room for the weekday only (PR #422); the date heads the grid under it.
          await qa.page.getByRole('radiogroup', { name: 'Ngày trong tuần' }).getByRole('radio', { name: 'T3', exact: true }).check()
        }
        const column = qa.page.getByRole('group', { name: `T3 ${dm(TUE)}` })
        await qa.expect(column.getByRole('img', { name: `${name} bận 09:00–10:00` })).toBeVisible()
        // On a phone there is no hover: the name and the hours are written in the block itself.
        await qa.expect(column.getByRole('img', { name: `${name} bận 09:00–10:00` })).toContainText('09:00–10:00')
        for (const t of ['10:30', '13:30', '15:30']) await qa.expect(column.getByRole('img', { name: new RegExp(`bận ${t}`) })).toHaveCount(0)
        await settled(qa)
        await qa.shot('tuan-chia-se-moc-1', { fullPage: true })
        await noMeetingContent(qa)
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
        if (qa.device === 'phone') await qa.page.getByRole('radiogroup', { name: 'Ngày trong tuần' }).getByRole('radio', { name: 'T5', exact: true }).check()
        await qa.expect(qa.page.getByText(new RegExp(`Chưa biết: .*${name}`)).first()).toBeVisible()
        await settled(qa)
        await qa.shot('ngay-ngoai-khoang-chua-biet', { fullPage: true })
      },
    },
    // ------------------------------------------------------------------ UC-81a in the UI
    {
      id: 'AC7',
      role: 'chair',
      devices: ['desktop', 'phone'],
      title: 'Tìm giờ họp: chọn người đã chia sẻ, Tìm giờ → khung theo ngày; giờ bận họp Thường bị tránh, ngày "Chưa biết" không được đề xuất',
      steps: ['chutoa@ mở Tìm giờ họp (/calendar/find)', 'Chọn nhanh "Lê An" (Người đã chia sẻ lịch với bạn)', `Khoảng ngày ${dm(MON)} – ${dm(FRI)}, còn lại mặc định (2 giờ, đệm 30 phút)`, 'Bấm Tìm giờ'],
      expected: `Dòng Lê An "Xem được: Họp Thường"; ${dm(TUE)} có giờ bắt đầu 15:00 (trùng họp Mật không được tiết lộ, BR-140, BR-141) và không có 08:00, 08:30, 09:00, 09:30, 10:00; không có ngày ${dm(THU)}, ${dm(FRI)} (ngoài khoảng chia sẻ, BR-144, BR-145); không lộ tên cuộc họp; không cuộn ngang`,
      async run(qa) {
        await ensureMeetings(qa)
        const s = await ensureMemberShare(qa, 'Level1')
        const name = s.owner.fullName
        await qa.goto('/calendar/find')
        await qa.expect(qa.page.getByRole('heading', { name: 'Tìm giờ họp', level: 1 })).toBeVisible()
        await qa.page.locator('.find-quick').getByRole('button', { name, exact: true }).click()
        const picked = qa.page.getByRole('list', { name: 'Người dự đã chọn' })
        await qa.expect(picked.getByText('Xem được: Họp Thường')).toBeVisible()
        await findWeek(qa)
        await settled(qa)
        await qa.shot('da-chon-nguoi-du')
        await qa.page.getByRole('button', { name: 'Tìm giờ', exact: true }).first().click()
        const day = qa.page.getByRole('region', { name: `Thứ Ba, ${dm(TUE)}` })
        await qa.expect(day).toBeVisible()
        await qa.expect(day.getByRole('button', { name: /^15:00/ })).toBeVisible()
        for (const t of ['08:00', '08:30', '09:00', '09:30', '10:00']) await qa.expect(day.getByRole('button', { name: new RegExp(`^${t}`) })).toHaveCount(0)
        await qa.expect(qa.page.getByRole('region', { name: `Thứ Năm, ${dm(THU)}` })).toHaveCount(0)
        await qa.expect(qa.page.getByRole('region', { name: `Thứ Sáu, ${dm(FRI)}` })).toHaveCount(0)
        await day.getByRole('button', { name: /^15:00/ }).click()
        const who = day.getByRole('list', { name: 'Ai rảnh ở khung này' })
        await qa.expect(who.getByText(name)).toBeVisible()
        await qa.expect(who.getByText('Rảnh')).toBeVisible()
        await day.scrollIntoViewIfNeeded()
        await settled(qa)
        await qa.shot('ket-qua-tim-gio', { fullPage: true })
        await noMeetingContent(qa)
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
      },
    },
    // ------------------------------------------------------------------ UC-81a rules on the API
    {
      id: 'AC8',
      role: 'chair',
      title: 'Tìm khung giờ (API, Mốc 1): Nháp không tính bận, Nội bộ và Mật không được đọc, đệm 30 phút; người chưa chia sẻ là NotShared; ngày ngoài khoảng không có khung',
      steps: [
        `POST /api/calendar/slots: Lê An, ${TUE}, 60 phút, đệm 30, bước 30`,
        'Thêm quantri@ (chưa chia sẻ cho chutoa@)',
        `Tìm ${THU}–${FRI} (ngoài khoảng chia sẻ)`,
      ],
      expected:
        'Có 10:30 (Nháp, BR-146, BR-147), 13:30 (Nội bộ ở Mốc 1, BR-144, BR-145), 15:30 (Mật, BR-140, BR-141); không có 08:00–10:00 (họp 09:00–10:00 + đệm, UC-81a); allFree; quantri@ status NotShared; ngoài khoảng → days rỗng; không trường cuộc họp nào',
      async run(qa) {
        await ensureMeetings(qa)
        const s = await ensureMemberShare(qa, 'Level1')
        const admin = await me(qa, 'admin')
        const base = { userIds: [s.owner.id], durationMinutes: 60, fromDate: TUE, toDate: TUE, bufferMinutes: 30, stepMinutes: 30 }
        const res = await slots(qa, base)
        qa.expectStatus(res, 200)
        const starts = startsOn(res, TUE)
        qa.note(`Mốc 1, ${TUE}: ${starts.join(' ')}`)
        for (const t of ['10:30', '13:30', '15:30']) qa.expect(starts, `${t} should be proposed`).toContain(t)
        for (const t of ['08:00', '08:30', '09:00', '09:30', '10:00']) qa.expect(starts, `${t} overlaps the Normal meeting or its buffer`).not.toContain(t)
        qa.expect(res.body.allFree).toBe(true)
        qa.expect(res.body.people[0]).toMatchObject({ id: s.owner.id, status: 'Shared', level: 'Level1' })
        qa.expect([...allKeys(res.body)].filter((k) => MEETING_FIELDS.includes(k))).toEqual([])
        const two = await slots(qa, { ...base, userIds: [s.owner.id, admin.id] })
        qa.expectStatus(two, 200)
        qa.expect(two.body.people.find((p) => p.id === admin.id)?.status).toBe('NotShared')
        qa.expect(startsOn(two, TUE)).toEqual(starts)
        const out = await slots(qa, { ...base, fromDate: THU, toDate: FRI })
        qa.expectStatus(out, 200)
        qa.expect(out.body.days).toEqual([])
      },
    },
    // ------------------------------------------------------------------ UC-81b
    {
      id: 'AC9',
      role: 'chair',
      title: 'Tạo nháp từ khung giờ (trùng họp Mật không được tiết lộ): cuộc họp Nháp, người đầu làm Chủ toạ, người tìm làm Thư ký',
      steps: ['Tìm giờ họp với Lê An', `Chọn ${dm(TUE)} 15:00 → Tạo nháp`, `Tên "${'[QA] FE-16 nháp từ khung giờ'}" → Tạo nháp`],
      expected:
        'Hộp thoại "Tạo cuộc họp nháp" điền sẵn ngày giờ, Lê An "Chủ toạ", tôi "Thư ký"; không có cảnh báo nào về họp Mật; toast MSG29 "Tạo cuộc họp thành công." và mở trang cuộc họp; API: trạng thái Draft, thanhvien Chair, chutoa Secretary (BR-148, UC-81b)',
      async run(qa) {
        await ensureMeetings(qa)
        const s = await ensureMemberShare(qa, 'Level1')
        const name = s.owner.fullName
        await qa.goto('/calendar/find')
        await qa.page.locator('.find-quick').getByRole('button', { name, exact: true }).click()
        await findWeek(qa)
        await qa.page.getByRole('button', { name: 'Tìm giờ', exact: true }).first().click()
        const day = qa.page.getByRole('region', { name: `Thứ Ba, ${dm(TUE)}` })
        await day.getByRole('button', { name: /^15:00/ }).click()
        await day.getByRole('button', { name: 'Tạo nháp' }).click()
        const dialog = qa.page.getByRole('dialog', { name: 'Tạo cuộc họp nháp' })
        await qa.expect(dialog).toBeVisible()
        await qa.expect(dialog.getByText(/15:00 – 17:00/)).toBeVisible()
        const roster = dialog.getByRole('list', { name: 'Thành phần tham dự' })
        await qa.expect(roster.getByRole('listitem').filter({ hasText: name })).toContainText('Chủ toạ')
        await qa.expect(roster.getByRole('listitem').filter({ hasText: '(Tôi)' })).toContainText('Thư ký')
        const title = qa.name('FE-16 nháp từ khung giờ')
        await dialog.getByRole('textbox', { name: /Tên cuộc họp/ }).fill(title)
        await settled(qa)
        await qa.shot('hop-thoai-tao-nhap', { fullPage: false })
        const created = qa.page.waitForResponse((r) => r.url().endsWith('/api/calendar/slots/draft') && r.request().method() === 'POST')
        await dialog.getByRole('button', { name: 'Tạo nháp' }).click()
        const res = await created
        qa.expect(res.status()).toBe(201)
        const body = await res.json()
        made.slotDraftId = body.meeting.id
        qa.expect(body.code).toBe('MSG29')
        await qa.expect(toast(qa, 'Tạo cuộc họp thành công.')).toBeVisible()
        await qa.expect(qa.page).toHaveURL(new RegExp(`/mgmt/meetings/${body.meeting.id}`))
        await qa.expect(qa.page.getByText(title).first()).toBeVisible()
        await settled(qa)
        await qa.shot('cuoc-hop-nhap-da-tao')
        await noRawCodes(qa)
        const meeting = await (await qa.api()).get(`/api/meetings/${body.meeting.id}`)
        qa.expectStatus(meeting, 200)
        qa.expect((meeting.body.meeting ?? meeting.body).status).toBe('Draft')
        const roster2 = await (await qa.api()).get(`/api/meetings/${body.meeting.id}/participants`)
        qa.expectStatus(roster2, 200)
        const text = JSON.stringify(roster2.body)
        const chairMe = await me(qa, 'chair')
        // Look the two people up in whatever shape the roster has.
        const rows = []
        const walk = (v) => {
          if (Array.isArray(v)) v.forEach(walk)
          else if (v && typeof v === 'object') {
            const uid = v.userId ?? v.user?.id
            const role = v.role ?? v.meetingRole
            if (uid && role) rows.push({ uid, role })
            Object.values(v).forEach(walk)
          }
        }
        walk(roster2.body)
        qa.expect(rows.find((r) => r.uid === s.owner.id)?.role, text.slice(0, 300)).toBe('Chair')
        qa.expect(rows.find((r) => r.uid === chairMe.id)?.role, text.slice(0, 300)).toBe('Secretary')
      },
    },
    // ------------------------------------------------------------------ UC-81b EX-1, UC-81a EX-1
    {
      id: 'AC10',
      role: 'chair',
      title: 'Khung không còn trống → 409 MSG446, không tạo gì; tham số tìm sai → 400 MSG444 kèm lỗi theo trường',
      steps: [
        `POST /api/calendar/slots/draft ${TUE} 09:00–10:00 với Lê An, acceptedBusyUserIds rỗng`,
        'POST /api/calendar/slots: thời lượng 10 phút; khoảng 40 ngày; không có người dự; đệm 300',
      ],
      expected:
        '409 MSG446 "Khung giờ này không còn trống. Hãy chọn khung khác." và không có cuộc họp mới; 400 MSG444 "Thông tin tìm giờ họp không hợp lệ. Kiểm tra các trường được đánh dấu." với errors durationMinutes / toDate / userIds / bufferMinutes',
      async run(qa) {
        await ensureMeetings(qa)
        const s = await ensureMemberShare(qa, 'Level1')
        const api = await qa.api()
        const title = qa.name('FE-16 khung đã bận')
        const res = await api.post('/api/calendar/slots/draft', {
          title,
          plannedStartTime: at(TUE, '09:00'),
          plannedEndTime: at(TUE, '10:00'),
          mode: 'InPerson',
          location: '',
          onlineLink: null,
          sensitivity: 'Normal',
          description: '',
          userIds: [s.owner.id],
          includeMe: false,
          bufferMinutes: 30,
          acceptedBusyUserIds: [],
        })
        if (res.status === 201) made.extraDraftId = res.body.meeting?.id
        qa.expectStatus(res, 409)
        qa.expect(res.body.code).toBe('MSG446')
        qa.expect(res.body.message).toBe('Khung giờ này không còn trống. Hãy chọn khung khác.')
        const list = await api.get(`/api/meetings?q=${encodeURIComponent('khung đã bận')}&pageSize=50`)
        qa.expect(JSON.stringify(list.body)).not.toContain(title)
        const base = { userIds: [s.owner.id], fromDate: TUE, toDate: TUE }
        const wrong = []
        for (const [body, field] of [
          [{ ...base, durationMinutes: 10 }, 'durationMinutes'],
          [{ ...base, toDate: addDays(TUE, 39) }, 'toDate'],
          [{ ...base, userIds: [] }, 'userIds'],
          [{ ...base, bufferMinutes: 300 }, 'bufferMinutes'],
        ]) {
          const r = await slots(qa, body)
          const ok =
            r.status === 400 &&
            r.body.code === 'MSG444' &&
            r.body.message === 'Thông tin tìm giờ họp không hợp lệ. Kiểm tra các trường được đánh dấu.' &&
            Object.keys(r.body.errors ?? {}).includes(field)
          if (!ok) wrong.push(`${field}: ${r.status} ${JSON.stringify(r.body).slice(0, 200)}`)
        }
        qa.expect(wrong, wrong.join('\n')).toEqual([])
      },
    },
    // ------------------------------------------------------------------ UC-80d
    {
      id: 'AC11',
      role: 'chair',
      title: 'Bên xem xin nâng lên Mốc 2 (Thường và Nội bộ) và gia hạn: yêu cầu chờ chủ lịch, chia sẻ giữ như cũ',
      steps: ['chutoa@ mở Chia sẻ lịch', 'Dòng Lê An → "…" → Xin thay đổi', 'Chọn "Thường và Nội bộ", kéo dài ngày kết thúc 7 ngày → Gửi yêu cầu'],
      expected: 'Toast MSG454 "Đã gửi yêu cầu thay đổi chia sẻ."; dòng vẫn "Đang chia sẻ", "Họp Thường", có dòng "Bạn xin đổi: Thường và Nội bộ"; API pendingChange.level Level2 và endDate dài hơn 7 ngày; rút yêu cầu thay đổi: "Chia sẻ giữ như hiện tại.", vẫn "Đang chia sẻ", hết "Bạn xin đổi" (để AC12 xin lại)',
      async run(qa) {
        const s = await ensureMemberShare(qa, 'Level1')
        if (s.pendingChange) await (await qa.api()).post(`/api/calendar/shares/${s.id}/withdraw`)
        await qa.goto('/calendar/sharing')
        const line = section(qa, 'Lịch được chia sẻ với tôi').locator(`#share-${s.id}`)
        await line.getByRole('button', { name: 'Thao tác khác' }).click()
        await qa.page.getByRole('menuitem', { name: 'Xin thay đổi' }).click()
        const dialog = qa.page.getByRole('dialog', { name: 'Xin thay đổi' })
        await qa.expect(dialog).toBeVisible()
        await dialog.getByRole('radio', { name: 'Thường và Nội bộ' }).check()
        const longerEnd = addDays(s.endDate, 7)
        // getByLabel also matches the calendar picker button: scope to the textbox.
        await typeDay(dialog.getByRole('textbox', { name: 'Ngày kết thúc' }), longerEnd)
        await qa.expect(dialog.getByText(/Ngày bắt đầu giữ nguyên; chỉ kéo dài được ngày kết thúc\./)).toBeVisible()
        await settled(qa)
        await qa.shot('xin-nang-moc-2')
        await dialog.getByRole('button', { name: 'Gửi yêu cầu' }).click()
        await qa.expect(toast(qa, 'Đã gửi yêu cầu thay đổi chia sẻ.')).toBeVisible()
        await qa.expect(line.getByText(/Bạn xin đổi: Thường và Nội bộ/)).toBeVisible()
        await qa.expect(line.getByText(new RegExp(dm(longerEnd)))).toBeVisible()
        await qa.expect(line.getByText('Đang chia sẻ')).toBeVisible()
        await qa.expect(line.getByText('Họp Thường')).toBeVisible()
        await qa.shot('dang-cho-chu-lich-duyet')
        const after = await pairShare(qa, 'chair', EMAIL.member, EMAIL.chair, { open: true })
        qa.expect(after.level).toBe('Level1')
        qa.expect(after.pendingChange?.level).toBe('Level2')
        qa.expect(after.pendingChange?.endDate).toBe(longerEnd)
        // CAL_FT43: the viewer withdraws only the pending change; the share stays as it was.
        await line.getByRole('button', { name: 'Thao tác khác' }).click()
        await qa.page.getByRole('menuitem', { name: 'Rút yêu cầu thay đổi' }).click()
        const unask = qa.page.getByRole('dialog', { name: 'Rút yêu cầu thay đổi?' })
        await qa.expect(unask).toBeVisible()
        await qa.expect(unask.getByText('Chia sẻ giữ như hiện tại.')).toBeVisible()
        await settled(qa)
        await qa.shot('rut-yeu-cau-thay-doi')
        await unask.getByRole('button', { name: 'Rút yêu cầu', exact: true }).click()
        await qa.expect(toast(qa, 'Đã rút yêu cầu chia sẻ lịch.')).toBeVisible()
        await qa.expect(line.getByText('Đang chia sẻ')).toBeVisible()
        await qa.expect(line.getByText(/Bạn xin đổi/)).toHaveCount(0)
        qa.expect((await pairShare(qa, 'chair', EMAIL.member, EMAIL.chair, { open: true })).pendingChange ?? null).toBeNull()
        await qa.shot('da-rut-yeu-cau-thay-doi')
        await noRawCodes(qa)
      },
    },
    // ------------------------------------------------------------------ UC-80c AF-1
    {
      id: 'AC12',
      role: 'member',
      devices: ['desktop', 'phone'],
      title: 'Chủ lịch duyệt thay đổi đang chờ (API từ chối giữ nguyên) và đổi chia sẻ ngay: chia sẻ lên Thường và Nội bộ rồi hạ Mốc 1',
      steps: ['thanhvien@ mở Chia sẻ lịch', 'API: từ chối một yêu cầu thay đổi (giữ nguyên) rồi xin lại', 'Dòng chutoa@ "Xin đổi: Thường và Nội bộ" → Đồng ý → Đồng ý', '"…" → Đổi chia sẻ: hạ "Họp Thường" → Lưu'],
      expected: 'Từ chối thay đổi: 200 MSG452, chia sẻ giữ nguyên, hết chờ; hộp thoại "Đồng ý thay đổi" hiện "Họp Thường → Thường và Nội bộ"; toast MSG442 "Đã cập nhật chia sẻ lịch."; chip mức "Thường và Nội bộ"; Đổi ngay: PATCH 200 MSG442, "Đã cập nhật chia sẻ lịch.", dòng "Họp Thường", Chủ toạ được báo CalendarShareUpdated; nút chạm cao ≥ 40px trên điện thoại',
      async run(qa) {
        let s = await pairShare(qa, 'member', EMAIL.member, EMAIL.chair, { open: true })
        if (!s?.pendingChange) {
          s = await ensureMemberShare(qa, 'Level1')
          qa.expectStatus(await (await qa.api('chair')).patch(`/api/calendar/shares/${s.id}`, { level: 'Level2' }), 201)
        }
        // CAL_FT30 E3: declining a change keeps the old values (API), then ask again for the UI approval.
        {
          const pid = (await pairShare(qa, 'member', EMAIL.member, EMAIL.chair, { open: true })).id
          const dec = await (await qa.api('member')).post(`/api/calendar/shares/${pid}/decline`)
          qa.expectStatus(dec, 200)
          qa.expect(dec.body.code).toBe('MSG452')
          qa.expect(dec.body.message).toBe('Đã từ chối thay đổi chia sẻ.')
          const kept = await pairShare(qa, 'member', EMAIL.member, EMAIL.chair, { open: true })
          qa.expect(kept.level).toBe('Level1')
          qa.expect(kept.pendingChange).toBeNull()
          qa.note('Đã từ chối một yêu cầu thay đổi qua API (MSG452) rồi xin lại để duyệt trên màn hình.')
          qa.expectStatus(await (await qa.api('chair')).patch(`/api/calendar/shares/${pid}`, { level: 'Level2' }), 201)
          s = await pairShare(qa, 'member', EMAIL.member, EMAIL.chair, { open: true })
        }
        await qa.goto('/calendar/sharing')
        const line = qa.page.locator(`#share-${s.id}`)
        await qa.expect(line).toContainText(EMAIL.chair)
        await qa.expect(line.getByText(/Xin đổi: Thường và Nội bộ/)).toBeVisible()
        await qa.shot('chu-lich-thay-yeu-cau-doi', { fullPage: true })
        const accept = line.getByRole('button', { name: 'Đồng ý' })
        if (qa.device === 'phone') {
          const box = await accept.boundingBox()
          qa.expect(box?.height ?? 0, 'Đồng ý is shorter than 40px on touch').toBeGreaterThanOrEqual(40)
        }
        await accept.click()
        const dialog = qa.page.getByRole('dialog', { name: 'Đồng ý thay đổi' })
        await qa.expect(dialog).toContainText(/Họp Thường\s*→\s*Thường và Nội bộ/)
        await settled(qa)
        await qa.shot('dong-y-thay-doi')
        await noSidewaysScroll(qa)
        await dialog.getByRole('button', { name: 'Đồng ý' }).click()
        await qa.expect(toast(qa, 'Đã cập nhật chia sẻ lịch.')).toBeVisible()
        await qa.expect(line.getByText('Thường và Nội bộ')).toBeVisible()
        await qa.shot('da-len-moc-2')
        const after = await pairShare(qa, 'member', EMAIL.member, EMAIL.chair, { open: true })
        qa.expect(after.level).toBe('Level2')
        qa.expect(after.pendingChange).toBeNull()
        // CAL_FT41: the owner changes the share at once (lower the level).
        await line.getByRole('button', { name: 'Thao tác khác' }).click()
        await qa.page.getByRole('menuitem', { name: 'Đổi chia sẻ' }).click()
        const edit = qa.page.getByRole('dialog', { name: 'Đổi chia sẻ' })
        await qa.expect(edit).toBeVisible()
        await edit.getByRole('radio', { name: 'Họp Thường' }).check()
        await settled(qa)
        await qa.shot('doi-chia-se-truc-tiep')
        await edit.getByRole('button', { name: 'Lưu thay đổi' }).click()
        await qa.expect(toast(qa, 'Đã cập nhật chia sẻ lịch.')).toBeVisible()
        await qa.expect(line.getByText('Họp Thường')).toBeVisible()
        const lowered = await pairShare(qa, 'member', EMAIL.member, EMAIL.chair, { open: true })
        qa.expect(lowered.level).toBe('Level1')
        qa.expect(lowered.pendingChange).toBeNull()
        const notes = await (await qa.api('chair')).get('/api/notifications?page=1&pageSize=50')
        qa.expectStatus(notes, 200)
        qa.expect(notes.body.items.find((n) => n.type === 'CalendarShareUpdated' && n.targetId === after.id), 'no CalendarShareUpdated for chutoa@').toBeTruthy()
        await qa.shot('da-ha-moc-1')
      },
    },
    // ------------------------------------------------------------------ privacy at Mốc 2
    {
      id: 'AC13',
      role: 'chair',
      title: 'Mốc 2 (Thường và Nội bộ): thấy thêm giờ bận họp Nội bộ, họp Mật vẫn không bao giờ hiện, kể cả khi tìm giờ',
      steps: [`chutoa@ mở tab Lịch được chia sẻ tuần ${dm(MON)}`, `POST /api/calendar/slots ${TUE}, 60 phút`],
      expected: `T3 ${dm(TUE)}: bận 09:00–10:00 và 13:30–14:30; không có 15:30–16:30 (Mật) hay 10:30–11:30 (Nháp); tìm giờ: không có 13:30, vẫn có 15:30 (BR-140, BR-141, BR-144, BR-145)`,
      async run(qa) {
        await ensureMeetings(qa)
        const s = await ensureMemberShare(qa, 'Level2')
        const name = s.owner.fullName
        await qa.goto(`/calendar/sharing?tab=week&week=${TUE}`)
        const column = qa.page.getByRole('group', { name: `T3 ${dm(TUE)}` })
        await qa.expect(column.getByRole('img', { name: `${name} bận 09:00–10:00` })).toBeVisible()
        await qa.expect(column.getByRole('img', { name: `${name} bận 13:30–14:30` })).toBeVisible()
        await qa.expect(column.getByRole('img', { name: /bận 15:30/ })).toHaveCount(0)
        await qa.expect(column.getByRole('img', { name: /bận 10:30/ })).toHaveCount(0)
        await qa.expect(qa.page.getByRole('group', { name: 'Người chia sẻ' })).toContainText('Thường và Nội bộ')
        await settled(qa)
        await qa.shot('tuan-chia-se-moc-2', { fullPage: true })
        await noMeetingContent(qa)
        const res = await slots(qa, { userIds: [s.owner.id], durationMinutes: 60, fromDate: TUE, toDate: TUE, bufferMinutes: 30, stepMinutes: 30 })
        qa.expectStatus(res, 200)
        const starts = startsOn(res, TUE)
        qa.note(`Mốc 2, ${TUE}: ${starts.join(' ')}`)
        qa.expect(starts, 'the Internal meeting is busy at Mốc 2').not.toContain('13:30')
        qa.expect(starts, 'a Secret meeting is never shared').toContain('15:30')
      },
    },
    // ------------------------------------------------------------------ UC-80a
    {
      id: 'AC14',
      role: 'chair',
      title: 'Người tổ chức xin xem lịch một người ở Mốc 2; xin trùng, xin lịch của chính mình, khoảng sai bị từ chối',
      steps: [
        'chutoa@ → Chia sẻ lịch → "Xin xem lịch"',
        'Danh sách không có chính mình; tìm "quantri", chọn "Thường và Nội bộ", bấm 1 tuần → Gửi yêu cầu',
        'API: xin lại quantri@; xin lịch của chính mình; ngày kết thúc trước ngày bắt đầu',
      ],
      expected:
        'Toast MSG435 "Đã gửi yêu cầu xem lịch."; dòng Phạm Quản Trị "Chờ trả lời", "Thường và Nội bộ"; trùng → 409 MSG449; chính mình → 404; khoảng sai → 400 MSG443',
      async run(qa) {
        const old = await closePair(qa, 'admin', 'chair')
        if (old) qa.note(`Đã đóng chia sẻ quantri→chutoa còn mở từ lần chạy trước (${old}).`)
        const self = await me(qa, 'chair')
        const admin = await me(qa, 'admin')
        await qa.goto('/calendar/sharing')
        await section(qa, 'Lịch được chia sẻ với tôi').getByRole('button', { name: 'Xin xem lịch' }).click()
        const dialog = qa.page.getByRole('dialog', { name: 'Xin xem lịch' })
        await qa.expect(dialog).toBeVisible()
        // The full email: a bare "chutoa" also matches QA accounts such as qa-fe06-chutoa-khoa@ on a shared database.
        await dialog.getByRole('searchbox').fill(EMAIL.chair)
        await qa.expect(dialog.getByText('Không có ai khớp.')).toBeVisible()
        await dialog.getByRole('searchbox').fill('quantri')
        await dialog.locator('label.pick-row').filter({ hasText: EMAIL.admin }).getByRole('radio').check()
        await dialog.getByRole('radio', { name: 'Thường và Nội bộ' }).check()
        await dialog.getByRole('button', { name: '1 tuần' }).click()
        await settled(qa)
        await qa.shot('xin-xem-lich', { fullPage: false })
        await dialog.getByRole('button', { name: 'Gửi yêu cầu' }).click()
        await qa.expect(toast(qa, 'Đã gửi yêu cầu xem lịch.')).toBeVisible()
        const sent = await pairShare(qa, 'chair', EMAIL.admin, EMAIL.chair, { open: true })
        const line = section(qa, 'Lịch được chia sẻ với tôi').locator(`#share-${sent.id}`)
        await qa.expect(line).toContainText(EMAIL.admin)
        await qa.expect(line.getByText('Chờ trả lời', { exact: true })).toBeVisible()
        await qa.expect(line.getByText('Thường và Nội bộ')).toBeVisible()
        await qa.shot('da-gui-yeu-cau')
        const api = await qa.api()
        const ok = { level: 'Level1', startDate: TODAY, endDate: addDays(TODAY, 6) }
        const dup = await api.post('/api/calendar/shares', { ownerId: admin.id, ...ok })
        qa.expectStatus(dup, 409)
        qa.expect(dup.body.code).toBe('MSG449')
        qa.expectStatus(await api.post('/api/calendar/shares', { ownerId: self.id, ...ok }), 404)
        const bad = await api.post('/api/calendar/shares', { ownerId: (await me(qa, 'secretary')).id, level: 'Level1', startDate: addDays(TODAY, 5), endDate: TODAY })
        qa.expectStatus(bad, 400)
        qa.expect(bad.body.code).toBe('MSG443')
        qa.expect(bad.body.message).toBe('Khoảng ngày không đúng.')
        const s = await pairShare(qa, 'chair', EMAIL.admin, EMAIL.chair, { open: true })
        qa.expect(s).toMatchObject({ status: 'Pending', level: 'Level2', initiatedBy: 'Viewer', endDate: addDays(TODAY, 6) })
        qa.expect(await pairShare(qa, 'chair', EMAIL.secretary, EMAIL.chair, { open: true })).toBeUndefined()
      },
    },
    // ------------------------------------------------------------------ UC-80c decline
    {
      id: 'AC15',
      role: 'admin',
      title: 'Chủ lịch từ chối yêu cầu xem lịch ("Để sau" giữ nguyên, "Từ chối" hỏi xác nhận): dòng chuyển "Đã từ chối" và mờ đi',
      steps: ['quantri@ → Chia sẻ lịch', 'Dòng Lê An "Chờ bạn trả lời": mở Đồng ý, đặt ngày vượt khoảng xin → bị chặn', 'Từ chối → "Để sau" (giữ nguyên)', 'Từ chối → Từ chối'],
      expected:
        'Khoảng rộng hơn: "Chỉ chọn trong khoảng đã xin: ..." + nút Đồng ý khoá, không gửi; nút "Để sau" đóng hộp, không gửi gì, dòng vẫn "Chờ bạn trả lời"; hộp xác nhận "Từ chối cho xem lịch?"; toast MSG437 "Đã từ chối yêu cầu chia sẻ."; dòng "Đã từ chối", mờ, không còn nút; API Declined, actions []',
      async run(qa) {
        let s = await pairShare(qa, 'admin', EMAIL.admin, EMAIL.chair, { open: true })
        if (!s) {
          const admin = await me(qa, 'admin')
          const r = await (await qa.api('chair')).post('/api/calendar/shares', { ownerId: admin.id, level: 'Level2', startDate: TODAY, endDate: addDays(TODAY, 6) })
          qa.expectStatus(r, 201)
          s = r.body.share
        }
        await qa.goto('/calendar/sharing')
        const line = section(qa, 'Người xem được lịch của tôi').locator(`#share-${s.id}`)
        await qa.expect(line.getByText('Chờ bạn trả lời')).toBeVisible()
        // CAL_FT24: the dialog refuses a range wider than the one requested.
        await line.getByRole('button', { name: 'Đồng ý' }).click()
        const accept = qa.page.getByRole('dialog', { name: 'Cho xem lịch' })
        await qa.expect(accept).toBeVisible()
        await typeDay(accept.getByRole('textbox', { name: 'Ngày kết thúc' }), addDays(TODAY, 9))
        await qa.expect(accept.getByText(/Chỉ chọn trong khoảng đã xin:/)).toBeVisible()
        await qa.expect(accept.getByRole('button', { name: 'Đồng ý' })).toBeDisabled()
        await settled(qa)
        await qa.shot('chap-nhan-vuot-khoang-bi-chan')
        qa.expect((await shares(qa, 'admin')).find((x) => x.id === s.id)?.status).toBe('Pending')
        await accept.getByRole('button', { name: 'Đóng' }).first().click()
        await line.getByRole('button', { name: 'Từ chối' }).click()
        const confirm = qa.page.getByRole('dialog', { name: 'Từ chối cho xem lịch?' })
        await qa.expect(confirm).toBeVisible()
        await settled(qa)
        await qa.shot('xac-nhan-tu-choi')
        // CAL_FT26: "Để sau" closes the dialog, sends nothing, the row is unchanged.
        await confirm.getByRole('button', { name: 'Để sau' }).click()
        await qa.expect(confirm).toHaveCount(0)
        await qa.expect(line.getByText('Chờ bạn trả lời')).toBeVisible()
        await qa.expect(line.getByRole('button', { name: 'Từ chối' })).toBeVisible()
        qa.expect((await shares(qa, 'admin')).find((x) => x.id === s.id)?.status).toBe('Pending')
        await qa.shot('de-sau-giu-nguyen')
        await line.getByRole('button', { name: 'Từ chối' }).click()
        const confirm2 = qa.page.getByRole('dialog', { name: 'Từ chối cho xem lịch?' })
        await qa.expect(confirm2).toBeVisible()
        await confirm2.getByRole('button', { name: 'Từ chối' }).click()
        await qa.expect(toast(qa, 'Đã từ chối yêu cầu chia sẻ.')).toBeVisible()
        await qa.expect(line.getByText('Đã từ chối')).toBeVisible()
        await qa.expect(line).toHaveClass(/share-row-closed/)
        await qa.expect(line.getByRole('button')).toHaveCount(0)
        await qa.shot('da-tu-choi')
        const after = (await shares(qa, 'admin')).find((x) => x.id === s.id)
        qa.expect(after).toMatchObject({ status: 'Declined', actions: [] })
      },
    },
    // ------------------------------------------------------------------ UC-80e withdraw
    {
      id: 'AC16',
      role: 'chair',
      title: 'Bên xin rút yêu cầu đang chờ: hỏi xác nhận, "Đã rút"; chủ lịch không còn gì để trả lời',
      steps: ['chutoa@ xin lại xem lịch quantri@ (API, được vì yêu cầu cũ đã từ chối)', 'Chia sẻ lịch → dòng Phạm Quản Trị → "…" → Rút yêu cầu → Rút yêu cầu'],
      expected: 'Xin lại 201 MSG435; toast MSG438 "Đã rút yêu cầu chia sẻ lịch."; dòng "Đã rút"; quantri@ gọi accept → 409 MSG450',
      async run(qa) {
        const admin = await me(qa, 'admin')
        let s = await pairShare(qa, 'chair', EMAIL.admin, EMAIL.chair, { open: true })
        if (!s) {
          const r = await (await qa.api()).post('/api/calendar/shares', { ownerId: admin.id, level: 'Level1', startDate: TODAY, endDate: addDays(TODAY, 6) })
          qa.expectStatus(r, 201)
          qa.expect(r.body.code).toBe('MSG435')
          s = r.body.share
        }
        await qa.goto('/calendar/sharing')
        const line = qa.page.locator(`#share-${s.id}`)
        await line.getByRole('button', { name: 'Thao tác khác' }).click()
        await qa.page.getByRole('menuitem', { name: 'Rút yêu cầu' }).click()
        const confirm = qa.page.getByRole('dialog', { name: 'Rút yêu cầu?' })
        await qa.expect(confirm).toBeVisible()
        await settled(qa)
        await qa.shot('xac-nhan-rut')
        await confirm.getByRole('button', { name: 'Rút yêu cầu' }).click()
        await qa.expect(toast(qa, 'Đã rút yêu cầu chia sẻ lịch.')).toBeVisible()
        await qa.expect(line.getByText('Đã rút')).toBeVisible()
        await qa.shot('da-rut')
        const late = await (await qa.api('admin')).post(`/api/calendar/shares/${s.id}/accept`)
        qa.expectStatus(late, 409)
        qa.expect(late.body.code).toBe('MSG450')
      },
    },
    // ------------------------------------------------------------------ UC-80c narrowing, UC-80e AF-1 on the API
    {
      id: 'AC17',
      role: 'admin',
      title: 'Chủ lịch đồng ý nhưng cho xem ít hơn (hạ mốc, thu hẹp khoảng); rộng hơn khoảng đã xin bị từ chối; ngừng chia sẻ',
      steps: [
        'chutoa@ xin xem lịch quantri@ Mốc 2, 7 ngày',
        'quantri@ accept với khoảng 10 ngày (rộng hơn)',
        'quantri@ accept Mốc 1, 3 ngày',
        'quantri@ end; rồi end lại',
      ],
      expected: 'Rộng hơn → 400 MSG443, vẫn Pending; hạ mốc → 200 MSG436, Effective Level1 3 ngày; end → 200 MSG451 "Đã ngừng chia sẻ lịch."; end lại → 409 MSG450',
      async run(qa) {
        await closePair(qa, 'admin', 'chair')
        const admin = await me(qa, 'admin')
        const r = await (await qa.api('chair')).post('/api/calendar/shares', { ownerId: admin.id, level: 'Level2', startDate: TODAY, endDate: addDays(TODAY, 6) })
        qa.expectStatus(r, 201)
        const id = r.body.share.id
        const api = await qa.api()
        const wide = await api.post(`/api/calendar/shares/${id}/accept`, { level: 'Level2', startDate: TODAY, endDate: addDays(TODAY, 9) })
        qa.expectStatus(wide, 400)
        qa.expect(wide.body.code).toBe('MSG443')
        qa.expect((await shares(qa, 'admin')).find((x) => x.id === id)?.status).toBe('Pending')
        const ok = await api.post(`/api/calendar/shares/${id}/accept`, { level: 'Level1', startDate: TODAY, endDate: addDays(TODAY, 2) })
        qa.expectStatus(ok, 200)
        qa.expect(ok.body.code).toBe('MSG436')
        qa.expect(ok.body.share).toMatchObject({ status: 'Effective', level: 'Level1', endDate: addDays(TODAY, 2) })
        const end = await api.post(`/api/calendar/shares/${id}/end`)
        qa.expectStatus(end, 200)
        qa.expect(end.body).toMatchObject({ code: 'MSG451', message: 'Đã ngừng chia sẻ lịch.' })
        const again = await api.post(`/api/calendar/shares/${id}/end`)
        qa.expectStatus(again, 409)
        qa.expect(again.body.code).toBe('MSG450')
      },
    },
    // ------------------------------------------------------------------ UC-80e AF-1 from the viewer, in the UI
    {
      id: 'AC18',
      role: 'chair',
      devices: ['desktop', 'phone'],
      title: 'Bên xem "Thôi xem lịch": chia sẻ ngừng, tuần hết giờ bận (chip "Đã dừng"); tuần chia sẻ đổi khoảng bằng nút',
      steps: ['chutoa@ → Chia sẻ lịch → dòng Lê An → "…" → Thôi xem lịch → Thôi xem', 'Mở tab Lịch được chia sẻ: tuần hết giờ bận, chip "Đã dừng"; Tuần sau/trước đổi URL', 'Tìm giờ với Lê An (API)'],
      expected:
        'Hộp xác nhận "Thôi xem lịch của Lê An?"; toast MSG451 "Đã ngừng chia sẻ lịch."; dòng "Đã ngừng", mờ; chip người trên tab tuần chuyển "Đã dừng", tuần hết giờ bận; Tuần sau/trước đổi week= trong URL; tìm giờ: Lê An NotShared; đổi chia sẻ đã ngừng → 409 MSG450',
      async run(qa) {
        const s = await ensureMemberShare(qa, 'Level2')
        const name = s.owner.fullName
        await qa.goto('/calendar/sharing')
        const line = qa.page.locator(`#share-${s.id}`)
        await line.getByRole('button', { name: 'Thao tác khác' }).click()
        await qa.page.getByRole('menuitem', { name: 'Thôi xem lịch' }).click()
        const confirm = qa.page.getByRole('dialog', { name: `Thôi xem lịch của ${name}?` })
        await qa.expect(confirm).toBeVisible()
        await settled(qa)
        await qa.shot('xac-nhan-thoi-xem')
        await confirm.getByRole('button', { name: 'Thôi xem' }).click()
        await qa.expect(toast(qa, 'Đã ngừng chia sẻ lịch.')).toBeVisible()
        await qa.expect(line.getByText('Đã ngừng')).toBeVisible()
        await qa.expect(line).toHaveClass(/share-row-closed/)
        await qa.shot('da-thoi-xem')
        const weekTab = qa.page.getByRole('radiogroup', { name: 'Chia sẻ lịch' }).getByRole('radio', { name: /Lịch được chia sẻ/ })
        await weekTab.click()
        await qa.expect(weekTab).toBeChecked()
        await qa.expect(qa.page.getByRole('heading', { name: new RegExp(`${dm(MON)}|${dm(mondayOf(TODAY))}`) })).toBeVisible()
        await qa.goto(`/calendar/sharing?tab=week&week=${TUE}`)
        await qa.expect(qa.page.locator('.shared-grid, .share-empty').first()).toBeVisible()
        await qa.expect(qa.page.getByRole('img', { name: new RegExp(`${name} bận`) })).toHaveCount(0)
        // The person stays on the week as a "Đã dừng" chip (CAL_FT49), only the busy time is gone.
        const chip = qa.page.getByRole('group', { name: 'Người chia sẻ' })
        await qa.expect(chip.getByText(name)).toBeVisible()
        await qa.expect(chip.getByText('Đã dừng')).toBeVisible()
        // CAL_FT50: the shared week moves with previous/next and keeps the week in the URL.
        await qa.page.getByRole('button', { name: 'Tuần sau' }).click()
        await qa.expect(qa.page).toHaveURL(new RegExp(`week=${addDays(MON, 7)}`))
        await qa.page.getByRole('button', { name: 'Tuần trước' }).click()
        await qa.expect(qa.page).toHaveURL(new RegExp(`week=${MON}`))
        await settled(qa)
        await qa.shot('tuan-khong-con-nguoi-do', { fullPage: true })
        const res = await slots(qa, { userIds: [s.owner.id], durationMinutes: 60, fromDate: TUE, toDate: TUE })
        qa.expectStatus(res, 200)
        qa.expect(res.body.people[0].status).toBe('NotShared')
        qa.expect(res.body.days).toEqual([])
        const patch = await (await qa.api('member')).patch(`/api/calendar/shares/${s.id}`, { level: 'Level1' })
        qa.expectStatus(patch, 409)
        qa.expect(patch.body.code).toBe('MSG450')
      },
    },
    // ------------------------------------------------------------------ BR-142, BR-143 stop
    {
      id: 'AC19',
      role: 'member',
      devices: ['desktop', 'phone'],
      title: 'Chia sẻ dừng hẳn khi tài khoản bên xem bị vô hiệu hoá: chủ lịch thấy "Đã dừng" kèm giải thích MSG439, không còn nút',
      steps: [
        `Quản trị tạo/mở lại tài khoản ${TEMP_VIEWER} (vai trò Thư ký)`,
        'thanhvien@ chia sẻ trực tiếp cho tài khoản đó, tài khoản đó đồng ý',
        'Quản trị vô hiệu hoá tài khoản đó',
        'thanhvien@ mở Chia sẻ lịch, chạm (i) cạnh "Đã dừng"',
      ],
      expected:
        'API: Stopped, actions []; dòng "Đã dừng" (đỏ), không có nút; (i) mở được bằng chạm, câu MSG439 "Chia sẻ lịch đã dừng vì người xem không còn quyền tạo cuộc họp hoặc tài khoản bị vô hiệu hoá. Hãy xin chia sẻ mới."',
      async run(qa) {
        const admin = await qa.api('admin')
        let shareId = qa.device === 'phone' ? made.stoppedShareId : null
        if (!shareId) {
          const users = await admin.get('/api/users')
          qa.expectStatus(users, 200)
          const list = Array.isArray(users.body) ? users.body : users.body.items
          let temp = list.find((u) => u.email === TEMP_VIEWER)
          if (!temp) {
            const roles = (await admin.get('/api/roles')).body
            const roleId = roles.find((r) => r.name === 'Thư ký')?.id
            const created = await admin.post('/api/users', { fullName: qa.name('FE-16 người xem tạm'), email: TEMP_VIEWER, password: PASSWORD, roleId })
            qa.expectStatus(created, 201)
            temp = created.body.user
            qa.note(`Đã tạo tài khoản ${TEMP_VIEWER} (sẽ để ở trạng thái vô hiệu hoá).`)
          } else if (!temp.isActive) {
            qa.expectStatus(await admin.put(`/api/users/${temp.id}/status`, { isActive: true }), 200)
          }
          const r = await (await qa.api()).post('/api/calendar/shares/direct', { viewerId: temp.id, startDate: TODAY, endDate: addDays(TODAY, 6) })
          qa.expectStatus(r, 201)
          shareId = r.body.share.id
          qa.expectStatus(await (await qa.api(TEMP_VIEWER)).post(`/api/calendar/shares/${shareId}/accept`), 200)
          qa.expectStatus(await admin.put(`/api/users/${temp.id}/status`, { isActive: false }), 200)
          made.stoppedShareId = shareId
        }
        const s = (await shares(qa, 'member')).find((x) => x.id === shareId)
        qa.expect(s).toMatchObject({ status: 'Stopped', actions: [] })
        await qa.goto('/calendar/sharing')
        // A share that had already ended when the page opened is folded under "Chia sẻ đã kết thúc (N)".
        const fold = qa.page.getByRole('button', { name: /^Chia sẻ đã kết thúc/ })
        for (const b of await fold.all()) if ((await b.getAttribute('aria-expanded')) === 'false') await b.click()
        const line = qa.page.locator(`#share-${shareId}`)
        await qa.expect(line.getByText('Đã dừng')).toBeVisible()
        await qa.expect(line.getByRole('button', { name: /Đồng ý|Từ chối|Thao tác khác/ })).toHaveCount(0)
        const tip = line.locator('.share-row-meta').getByRole('button').first()
        if (qa.device === 'phone') await tip.tap()
        else await tip.click()
        await qa.expect(
          qa.page.getByText('Chia sẻ lịch đã dừng vì người xem không còn quyền tạo cuộc họp hoặc tài khoản bị vô hiệu hoá. Hãy xin chia sẻ mới.').first(),
        ).toBeVisible()
        await settled(qa)
        await qa.shot('chia-se-da-dung')
        await noSidewaysScroll(qa)
      },
    },
    // ------------------------------------------------------------------ BR-140, BR-141 on the API
    {
      id: 'AC20',
      role: 'member',
      title: 'Thành viên không có quyền tạo cuộc họp: các thao tác của người tổ chức bị từ chối 403 MSG448 ở server',
      steps: [
        'GET /api/calendar/shared-week; GET share-candidates?purpose=request; POST /api/calendar/shares',
        'POST /api/calendar/slots; POST /api/calendar/slots/draft',
        'GET /api/calendar/shares; GET share-candidates?purpose=direct',
      ],
      expected:
        'Năm lời gọi đầu 403 MSG448 "Bạn cần quyền tạo cuộc họp để thực hiện thao tác này."; GET shares 200 với asViewer rỗng; candidates direct 200 chỉ người có quyền tạo cuộc họp, không có chính mình',
      async run(qa) {
        const caps = await qa.capabilities()
        if (caps.capabilities.includes('MeetingCreator'))
          qa.inconclusive(`thanhvien@ giữ "${caps.roleName}" có MeetingCreator trên database này; trả về vai trò Thành viên rồi chạy lại.`)
        const api = await qa.api()
        const admin = await me(qa, 'admin')
        const self = await me(qa, 'member')
        const wrong = []
        for (const [label, res] of [
          ['shared-week', await api.get('/api/calendar/shared-week')],
          ['candidates request', await api.get('/api/calendar/share-candidates?purpose=request')],
          ['POST shares', await api.post('/api/calendar/shares', { ownerId: admin.id, level: 'Level1', startDate: TODAY, endDate: addDays(TODAY, 6) })],
          ['POST slots', await api.post('/api/calendar/slots', { userIds: [admin.id] })],
          [
            'POST slots/draft',
            await api.post('/api/calendar/slots/draft', {
              title: qa.name('FE-16 không được tạo'),
              plannedStartTime: at(WED, '08:00'),
              plannedEndTime: at(WED, '09:00'),
              userIds: [admin.id],
            }),
          ],
        ]) {
          if (res.status !== 403 || res.body?.code !== 'MSG448' || res.body?.message !== 'Bạn cần quyền tạo cuộc họp để thực hiện thao tác này.')
            wrong.push(`${label}: ${res.status} ${JSON.stringify(res.body).slice(0, 160)}`)
        }
        const list = await api.get('/api/calendar/shares')
        if (list.status !== 200 || list.body.asViewer.length !== 0) wrong.push(`GET shares: ${list.status} asViewer=${list.body?.asViewer?.length}`)
        const direct = await api.get('/api/calendar/share-candidates?purpose=direct&pageSize=100')
        if (direct.status !== 200) wrong.push(`candidates direct: ${direct.status}`)
        else {
          if (direct.body.items.some((p) => p.id === self.id)) wrong.push('candidates direct có chính mình')
          if (direct.body.items.some((p) => p.email === EMAIL.member)) wrong.push('candidates direct có thanhvien@')
          if (!direct.body.items.some((p) => p.email === EMAIL.chair)) wrong.push('candidates direct thiếu chutoa@')
        }
        qa.expect(wrong, wrong.join('\n')).toEqual([])
      },
    },
    // ------------------------------------------------------------------ BR-140, BR-141 in the UI
    {
      id: 'AC21',
      role: 'member',
      devices: ['desktop', 'phone'],
      title: 'Thành viên mở Tìm giờ họp thấy 404 như đường dẫn không có; ?tab=week vẫn chỉ hiện danh sách của mình',
      steps: ['thanhvien@ mở /calendar/find', 'Mở /calendar/sharing?tab=week'],
      expected: '"Không tìm thấy trang"; không có mục Tìm giờ họp trên menu; /calendar/sharing?tab=week hiện thẻ "Người xem được lịch của tôi", không có lưới tuần',
      async run(qa) {
        const caps = await qa.capabilities()
        if (caps.capabilities.includes('MeetingCreator')) qa.inconclusive(`thanhvien@ giữ "${caps.roleName}" có MeetingCreator trên database này.`)
        await qa.goto('/calendar')
        if (qa.device === 'desktop') await qa.expect(qa.page.getByRole('link', { name: 'Tìm giờ họp' })).toHaveCount(0)
        await qa.goto('/calendar/find')
        await qa.expect(qa.page.getByRole('heading', { name: 'Không tìm thấy trang' })).toBeVisible()
        await qa.shot('tim-gio-404')
        await qa.goto(`/calendar/sharing?tab=week&week=${TUE}`)
        await qa.expect(qa.page.getByRole('heading', { name: /Người xem được lịch của tôi/ })).toBeVisible()
        await qa.expect(qa.page.locator('.shared-grid')).toHaveCount(0)
        await qa.shot('tab-tuan-khong-mo')
        await noSidewaysScroll(qa)
      },
    },
    {
      id: 'AC22',
      role: 'anon',
      title: 'Chưa đăng nhập: các trang lịch đưa về Đăng nhập, API 401',
      steps: ['Không đăng nhập, mở /calendar/sharing và /calendar/find', 'Gọi các API lịch không token'],
      expected: 'Chuyển tới /login; GET shares, shared-week, share-candidates, POST slots → 401',
      async run(qa) {
        await qa.goto('/calendar/sharing')
        await qa.expect(qa.page).toHaveURL(/\/login/)
        await qa.goto('/calendar/find')
        await qa.expect(qa.page).toHaveURL(/\/login/)
        await qa.shot('ve-dang-nhap')
        const api = await qa.api('anon')
        for (const res of [
          await api.get('/api/calendar/shares'),
          await api.get('/api/calendar/shared-week'),
          await api.get('/api/calendar/share-candidates?purpose=direct'),
          await api.post('/api/calendar/slots', {}),
        ])
          qa.expectStatus(res, 401)
      },
    },
    // ------------------------------------------------------------------ the working calendar (UC-16 AF-4)
    {
      id: 'AC23',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Lịch làm việc: tuần theo giờ, mặc định ẩn nhóm Chờ duyệt (bộ lọc 3 nhóm), trạng thái trống có nút hiện tất cả; bật Chờ duyệt thì thấy cuộc họp, họp Mật có nhãn "Mật"; đổi sang Danh sách giữ trong URL',
      steps: [
        `thuky@ mở Lịch làm việc tuần ${dm(MON)}`,
        'Mở "Bộ lọc": chỉ "Đã duyệt/Đã công bố/Đang diễn ra" bật; bấm "Xem mọi cuộc họp" thì hiện tất cả (status=all)',
        'Bật nhóm Chờ duyệt (?status=waiting)',
        'Bấm Danh sách',
      ],
      expected:
        'Thanh công cụ: Hôm nay, Tuần trước/sau, Lịch/Danh sách, Kiểu lịch, Tạo cuộc họp; URL không có status; mặc định không có 3 cuộc họp chờ duyệt (nhóm mặc định Đã duyệt, Đã công bố, Đang diễn ra); "Xem mọi cuộc họp" hiện tất cả (?status=all); bật Chờ duyệt thì có, khối họp Mật có "Mật"; Danh sách: URL ?mode=list, nhóm theo ngày; không cuộn ngang',
      async run(qa) {
        await ensureMeetings(qa)
        await qa.goto(`/calendar?week=${TUE}`)
        for (const name of ['Hôm nay', 'Tuần trước', 'Tuần sau']) await qa.expect(qa.page.getByRole('button', { name, exact: true })).toBeVisible()
        await qa.expect(qa.page.getByRole('group', { name: 'Chế độ xem' })).toBeVisible()
        await qa.expect(qa.page.getByRole('link', { name: 'Tạo cuộc họp' })).toBeVisible()
        await qa.expect(qa.page.getByText(qa.name('FE-16 họp Thường'))).toHaveCount(0)
        await settled(qa)
        await qa.shot('lich-mac-dinh')
        await noSidewaysScroll(qa)
        // CAL_FT05: the filter opens with only the three default groups on.
        await qa.expect(new URL(qa.page.url()).searchParams.get('status')).toBeNull()
        await qa.page.getByRole('button', { name: 'Bộ lọc' }).click()
        const filterPanel = qa.page.getByRole('group', { name: 'Bộ lọc lịch' })
        await qa.expect(filterPanel).toBeVisible()
        for (const [chip, on] of [
          ['Chờ duyệt', 'false'],
          ['Đã duyệt', 'true'],
          ['Đã công bố', 'true'],
          ['Đang diễn ra', 'true'],
          ['Đã kết thúc, nháp', 'false'],
          ['Bị từ chối', 'false'],
          ['Đã huỷ', 'false'],
        ])
          await qa.expect(filterPanel.getByRole('button', { name: new RegExp(chip) }).first()).toHaveAttribute('aria-pressed', on)
        await qa.shot('bo-loc-mac-dinh')
        await qa.page.keyboard.press('Escape')
        // CAL_FT08: the empty state offers to show everything (labelled "Xem mọi cuộc họp", not "Bỏ lọc").
        await qa.page.getByRole('button', { name: 'Xem mọi cuộc họp' }).click()
        await qa.expect(qa.page.getByText(qa.name('FE-16 họp Thường')).first()).toBeVisible()
        await qa.expect(qa.page).toHaveURL(/status=all/)
        await qa.shot('xem-moi-cuoc-hop')
        await qa.goto(`/calendar?week=${TUE}&status=waiting`)
        const secret = qa.page.getByRole('link', { name: new RegExp(qa.name('FE-16 họp Mật').replace(/[[\]]/g, '\\$&')) }).first()
        if (qa.device === 'desktop') {
          await qa.expect(qa.page.getByRole('link', { name: /\[QA\] FE-16 họp Thường/ }).first()).toBeVisible()
          await qa.expect(secret).toContainText('Mật')
        } else {
          await qa.expect(qa.page.getByText(/\[QA\] FE-16 họp/).first()).toBeVisible()
        }
        await settled(qa)
        await qa.shot('lich-bat-cho-duyet', { fullPage: qa.device === 'phone' })
        await noSidewaysScroll(qa)
        await qa.page.getByRole('group', { name: 'Chế độ xem' }).getByRole('button', { name: 'Danh sách' }).click()
        await qa.expect(qa.page).toHaveURL(/mode=list/)
        await qa.expect(qa.page.getByText(/\[QA\] FE-16 họp Thường/).first()).toBeVisible()
        await settled(qa)
        await qa.shot('lich-danh-sach', { fullPage: true })
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
      },
    },
    {
      id: 'AC24',
      role: 'member',
      title: 'Lịch làm việc chỉ hiện cuộc họp người xem được: Thành viên không thấy cuộc họp chưa công bố mà mình có tên; không có nút Tạo cuộc họp',
      steps: [`thanhvien@ mở /calendar?week=${TUE}&status=all`, 'Đổi sang Danh sách'],
      expected: 'Không có "[QA] FE-16 họp …" (Thành viên chỉ thấy từ khi Đã công bố, BR-016, BR-017 / UC-16); không có nút "Tạo cuộc họp"',
      async run(qa) {
        await ensureMeetings(qa)
        await qa.goto(`/calendar?week=${TUE}&status=all`)
        await qa.expect(qa.page.getByRole('button', { name: 'Hôm nay', exact: true })).toBeVisible()
        await qa.expect(qa.page.getByText(/\[QA\] FE-16 họp/)).toHaveCount(0)
        await qa.expect(qa.page.getByRole('link', { name: 'Tạo cuộc họp' })).toHaveCount(0)
        await settled(qa)
        await qa.shot('lich-thanh-vien')
        await qa.goto(`/calendar?week=${TUE}&status=all&mode=list`)
        await qa.expect(qa.page.getByText(/\[QA\] FE-16 họp/)).toHaveCount(0)
        await qa.shot('danh-sach-thanh-vien', { fullPage: true })
      },
    },
    // ------------------------------------------------------------------ new: working-calendar views, nav, filter
    {
      id: 'AC25',
      role: 'secretary',
      devices: ['desktop'],
      title: 'Lịch làm việc: vạch giờ hiện tại; Tuần trước/sau/Hôm nay đổi khoảng; Kiểu lịch Tuần làm việc/Cả tuần/Tháng; bộ lọc mặc định; Đã huỷ gạch ngang; lọc Mật + Đặt lại + giữ lọc sau tải lại',
      steps: [
        'Dựng 5 cuộc họp chồng giờ T4 09:00–10:00 (1 trực tuyến) + 1 cuộc đã huỷ (API)',
        'Mở /calendar (tuần hiện tại): vạch giờ hiện tại + "hôm nay"',
        'Bấm Tuần sau / Tuần trước / Hôm nay, đọc URL và tiêu đề khoảng',
        'Kiểu lịch: Cả tuần (có T7/CN) → Tháng (lưới + "+N cuộc họp") → Tuần làm việc',
        `Mở Bộ lọc tuần ${dm(TUE)}: chỉ 3 nhóm mặc định bật; bật Chờ duyệt + Mật → "Bộ lọc, 2 bộ lọc khác mặc định"; Đặt lại; tải lại giữ lọc trong URL`,
        'Bật Đã huỷ: khối gạch ngang, URL có cancelled; tắt thì mất',
      ],
      expected:
        'Vạch giờ hiện tại + "hôm nay" khi xem tuần này; các nút đổi khoảng đổi URL week; Cả tuần vẽ T7/CN (?span=full), Tháng vẽ lưới với ô chứa cuộc họp và "+N cuộc họp" (?span=month + month=), quay lại 5 cột; Đã huỷ: khối cal-cancelled, bật/tắt theo chip; Mật: chỉ họp Mật, nút đếm 2, Đặt lại về mặc định, tải lại giữ ?status + sensitivity',
      async run(qa) {
        const ids = await ensureAc25(qa)
        // FT07 needs the four WaitingApproval meetings (one Mật) of the plan itself.
        await ensureMeetings(qa)
        // CAL_FT01 rest: the current week draws the current-time line and marks today.
        // Today (Saturday) is outside the work week, so open the full week.
        await qa.goto('/calendar?span=full')
        await qa.expect(qa.page.getByRole('button', { name: 'Hôm nay', exact: true })).toBeVisible()
        await qa.expect(qa.page.locator('.calendar-now-line')).toBeVisible()
        await qa.expect(qa.page.getByText(/hôm nay/).first()).toBeVisible()
        await qa.expect(new URL(qa.page.url()).searchParams.get('week')).toBeNull()
        await settled(qa)
        await qa.shot('lich-tuan-hien-tai')
        await noSidewaysScroll(qa)

        // CAL_FT03: previous / next / today move the range and the URL carries it.
        await qa.goto(`/calendar?week=${TUE}`)
        const rangeBtn = () => qa.page.getByRole('button', { name: new RegExp(`${dm(MON)}.*${dm(FRI)}`) }).first()
        // The app's range label has no leading zero ("7/12 – 11/12/2026").
        const bare = (day) => `${Number(day.slice(8, 10))}/${Number(day.slice(5, 7))}`
        const nextWeek = async (label, monday) => {
          await qa.page.getByRole('button', { name: label }).click()
          await qa.expect(qa.page).toHaveURL(new RegExp(`week=${monday}`))
          await qa.expect(qa.page.getByRole('button', { name: new RegExp(bare(monday).replace('/', '\\/')) }).first()).toBeVisible()
        }
        await nextWeek('Tuần sau', addDays(MON, 7))
        await qa.shot('lich-tuan-sau')
        await nextWeek('Tuần trước', MON)
        await nextWeek('Tuần trước', addDays(MON, -7))
        await qa.page.getByRole('button', { name: 'Hôm nay', exact: true }).click()
        await qa.expect(new URL(qa.page.url()).searchParams.get('week')).toBeNull()
        await qa.shot('lich-ve-hom-nay')
        await qa.goto(`/calendar?week=${TUE}`)

        // CAL_FT02: the kind switch draws the work week, the full week and the month.
        // The waiting group must be on: the staged meetings are WaitingApproval.
        await qa.goto(`/calendar?week=${TUE}&status=waiting`)
        const kindBtn = () => qa.page.getByRole('button', { name: /Kiểu lịch/ })
        await qa.expect(kindBtn()).toContainText('Tuần làm việc')
        await qa.expect(qa.page.getByText('Thứ Bảy').first()).toHaveCount(0)
        await kindBtn().click()
        await qa.page.getByRole('option', { name: 'Cả tuần' }).click()
        await qa.expect(qa.page).toHaveURL(/span=full/)
        await qa.expect(qa.page.getByText('Thứ Bảy').first()).toBeVisible()
        await qa.expect(qa.page.getByText('Chủ Nhật').first()).toBeVisible()
        await settled(qa)
        await qa.shot('lich-ca-tuan')
        await noSidewaysScroll(qa)
        await kindBtn().click()
        await qa.page.getByRole('option', { name: 'Tháng' }).click()
        await qa.expect(qa.page).toHaveURL(/span=month/)
        await qa.expect(qa.page).toHaveURL(/2026-12/)
        await qa.expect(qa.page.getByRole('button', { name: /Tháng 12\/2026/ })).toBeVisible()
        // The overlapped day folds its meetings behind "+N cuộc họp" (or lists them all).
        // Cell height (hence fit) varies with the window: assert shown + hidden add up to the five.
        const wedCell = qa.page.getByRole('group', { name: /Thứ Tư, 16\/12/ })
        await qa.expect(wedCell.getByText(/FE-16 chong 1/).first()).toBeVisible()
        const cellShown = (await wedCell.getByText(/FE-16 chong [1-5]/).allInnerTexts()).map((t) => t.trim())
        const monthMore = wedCell.getByRole('button', { name: /cuộc họp.*xem danh sách/ })
        if ((await monthMore.count()) > 0) {
          await monthMore.first().click()
          // The month list is titled "<day> · N cuộc họp" (the week one says "N cuộc họp khác").
          const listPanel = qa.page.locator('.more-panel')
          await qa.expect(listPanel.locator('.more-title')).toContainText('cuộc họp')
          const listed = (await listPanel.getByText(/FE-16 chong [1-5]/).allInnerTexts()).map((t) => t.trim())
          qa.expect(listed.length).toBe(5 - cellShown.length)
          qa.expect(new Set([...cellShown, ...listed]).size).toBe(5)
          await qa.shot('thang-danh-sach-chong-lan')
          await qa.page.keyboard.press('Escape')
        } else {
          qa.expect(cellShown.length).toBe(5)
        }
        await settled(qa)
        await qa.shot('lich-thang')
        await noSidewaysScroll(qa)
        await kindBtn().click()
        await qa.page.getByRole('option', { name: 'Tuần làm việc' }).click()
        await qa.expect(qa.page).not.toHaveURL(/span=/)
        await qa.expect(qa.page.getByText('Thứ Bảy').first()).toHaveCount(0)

        // CAL_FT05: the filter opens with only the three default groups on.
        await qa.goto(`/calendar?week=${TUE}`)
        await qa.expect(new URL(qa.page.url()).searchParams.get('status')).toBeNull()
        await qa.page.getByRole('button', { name: 'Bộ lọc' }).click()
        const panel = qa.page.getByRole('group', { name: 'Bộ lọc lịch' })
        await qa.expect(panel).toBeVisible()
        for (const [chip, on] of [
          ['Chờ duyệt', 'false'],
          ['Đã duyệt', 'true'],
          ['Đã công bố', 'true'],
          ['Đang diễn ra', 'true'],
          ['Đã kết thúc, nháp', 'false'],
          ['Bị từ chối', 'false'],
          ['Đã huỷ', 'false'],
        ])
          await qa.expect(panel.getByRole('button', { name: new RegExp(chip) }).first()).toHaveAttribute('aria-pressed', on)
        await qa.shot('bo-loc-tuan-lam-viec')
        // CAL_FT07: one more status group + one level → "Bộ lọc, 2 bộ lọc khác mặc định".
        await panel.getByRole('button', { name: /Chờ duyệt/ }).first().click()
        await panel.getByRole('button', { name: /^Mật/ }).first().click()
        await panel.getByRole('button', { name: 'Xong' }).click()
        await qa.expect(qa.page.getByRole('button', { name: 'Bộ lọc, 2 bộ lọc khác mặc định' })).toBeVisible()
        await qa.expect(qa.page.getByRole('link', { name: /FE-16 họp Mật/ }).first()).toBeVisible()
        await qa.expect(qa.page.getByRole('link', { name: /FE-16 họp Thường/ })).toHaveCount(0)
        await settled(qa)
        await qa.shot('lich-loc-mat')
        await noSidewaysScroll(qa)
        await qa.page.getByRole('button', { name: 'Bộ lọc, 2 bộ lọc khác mặc định' }).click()
        const panel2 = qa.page.getByRole('group', { name: 'Bộ lọc lịch' })
        await panel2.getByRole('button', { name: 'Đặt lại' }).click()
        await qa.expect(qa.page.getByRole('button', { name: 'Bộ lọc' })).toBeVisible()
        await qa.expect(new URL(qa.page.url()).searchParams.get('status')).toBeNull()
        await qa.expect(qa.page.getByText(/FE-16 (họp|chong)/)).toHaveCount(0)
        await qa.shot('lich-dat-lai-bo-loc')
        // A reload keeps the filter the URL carries.
        await qa.goto(`/calendar?week=${TUE}&status=waiting&sensitivity=Secret`)
        await qa.page.reload()
        await qa.expect(qa.page.getByRole('link', { name: /FE-16 họp Mật/ }).first()).toBeVisible()
        await qa.expect(qa.page.getByRole('link', { name: /FE-16 họp Thường/ })).toHaveCount(0)
        await qa.shot('lich-loc-mat-sau-tai-lai')

        // CAL_FT06: the cancelled meeting appears struck through only with its group on.
        await qa.goto(`/calendar?week=${TUE}&status=waiting`)
        const cancelledTile = () => qa.page.getByRole('link', { name: /FE-16 huy/ })
        await qa.expect(cancelledTile()).toHaveCount(0)
        await qa.page.getByRole('button', { name: 'Bộ lọc' }).click()
        const panel3 = qa.page.getByRole('group', { name: 'Bộ lọc lịch' })
        await panel3.getByRole('button', { name: /Đã huỷ/ }).first().click()
        await panel3.getByRole('button', { name: 'Xong' }).click()
        await qa.expect(cancelledTile().first()).toBeVisible()
        await qa.expect(cancelledTile().first()).toHaveClass(/cal-cancelled/)
        await qa.expect(qa.page).toHaveURL(/cancelled/)
        await settled(qa)
        await qa.shot('lich-da-huy')
        await qa.page.getByRole('button', { name: /Bộ lọc, \d+ bộ lọc khác mặc định/ }).click()
        const panel4 = qa.page.getByRole('group', { name: 'Bộ lọc lịch' })
        await panel4.getByRole('button', { name: /Đã huỷ/ }).first().click()
        await panel4.getByRole('button', { name: 'Xong' }).click()
        await qa.expect(cancelledTile()).toHaveCount(0)
        await qa.expect(qa.page).not.toHaveURL(/cancelled/)
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
        // CAL_FT33: empty sections show their lines (Thư ký has no open shares of her own;
        // closed ones fold below, so only the owner card is fully empty).
        await qa.goto('/calendar/sharing')
        await qa.expect(qa.page.getByText('Bạn chưa chia sẻ lịch với ai.')).toBeVisible()
        qa.expect(await pairShare(qa, 'secretary', EMAIL.secretary, EMAIL.chair, { open: true })).toBeUndefined()
        await settled(qa)
        await qa.shot('chia-se-trong')
        // CAL_FT53 E1: the shared week with nobody sharing shows no rows and offers "Xin xem lịch".
        await qa.goto(`/calendar/sharing?tab=week&week=${TUE}`)
        await qa.expect(qa.page.locator('.share-empty').first()).toBeVisible()
        await qa.expect(qa.page.getByText('Chưa ai chia sẻ lịch với bạn.')).toBeVisible()
        await qa.expect(qa.page.getByRole('button', { name: 'Xin xem lịch' }).first()).toBeVisible()
        await qa.shot('tuan-chia-se-trong')
        qa.note(`AC25 dùng các cuộc họp ${ids.join(', ')}; AC26 dọn.`)
      },
    },
    {
      id: 'AC26',
      role: 'secretary',
      devices: ['desktop'],
      title: 'Lịch làm việc: bấm khối xem nhanh (pop-over + Xem chi tiết) và "+N cuộc họp" khi chồng giờ; họp trực tuyến đang diễn ra có "Vào phòng họp"',
      steps: [
        `Mở /calendar?week=${TUE}&status=waiting`,
        'Bấm "+N" ở T4 09:00: đọc danh sách chồng giờ',
        'Bấm khối trực tuyến: đọc Thời gian/Hình thức/Chủ toạ/Nội dung/Thành phần → Xem chi tiết mở trang họp',
        'Duyệt → công bố → bắt đầu họp trực tuyến: khối có "Vào phòng họp"; kết thúc thì mất; huỷ các cuộc còn lại',
      ],
      expected:
        'Danh sách "+N cuộc họp" ghi tên, giờ, trạng thái từng cuộc; pop-over đủ 6 dòng + Xem chi tiết mở /mgmt/meetings/<id>; InProgress: dải "Vào phòng họp" trên khối trực tuyến, Ended thì mất; các cuộc chờ duyệt còn lại bị huỷ',
      async run(qa) {
        const ids = await ensureAc25(qa)
        const sec = await qa.api('secretary')
        const chairApi = await qa.api('chair')
        const adminApi = await qa.api('admin')
        await qa.goto(`/calendar?week=${TUE}&status=waiting`)
        // CAL_FT10: overlapping meetings fold into "+N cuộc họp" and open as a list.
        // Scope to Wednesday: Tuesday also folds (the 15:00 draft overlaps the 15:30 secret meeting).
        const wed = `${WED.slice(8, 10)}/${WED.slice(5, 7)}/${WED.slice(0, 4)}`
        const more = qa.page.getByRole('button', { name: new RegExp(`cuộc họp khác trùng giờ ngày ${wed.replace(/\//g, '\\/')}`) }).first()
        await qa.expect(more).toBeVisible()
        await qa.shot('lich-chong-gio')
        await more.click()
        await qa.expect(qa.page.getByText(/cuộc họp khác/).first()).toBeVisible()
        for (let i = 1; i <= 5; i++) await qa.expect(qa.page.getByText(new RegExp(`FE-16 chong ${i}`)).first()).toBeVisible()
        await qa.expect(qa.page.getByText(/09:00.{0,3}10:00/).first()).toBeVisible()
        await settled(qa)
        await qa.shot('danh-sach-chong-gio')
        await qa.page.keyboard.press('Escape')
        // CAL_FT09: the quick look behind a tile, then its detail page.
        await qa.page.getByRole('link', { name: /FE-16 chong 1/ }).first().click()
        const pop = qa.page.locator('.overview-pane')
        await qa.expect(pop).toBeVisible()
        for (const row of ['Thời gian', 'Hình thức', 'Chủ toạ', 'Nội dung', /Thành phần tham dự/])
          await qa.expect(pop.getByText(row).first()).toBeVisible()
        await qa.expect(pop.getByText('Phòng họp trực tuyến TrustCabinet')).toBeVisible()
        await settled(qa)
        await qa.shot('xem-nhanh-cuoc-hop')
        await pop.getByRole('link', { name: 'Xem chi tiết' }).click()
        await qa.expect(qa.page).toHaveURL(new RegExp(`/mgmt/meetings/${ids[0]}`))
        await qa.shot('chi-tiet-tu-lich')
        await qa.page.goBack()
        // A running online meeting offers "Vào phòng họp" on its tile (needs InProgress, staged via API).
        qa.expectStatus(await chairApi.post(`/api/meetings/${ids[0]}/transitions`, { action: 'Approve' }), 200)
        const pub = await sec.post(`/api/meetings/${ids[0]}/transitions`, { action: 'Publish' })
        if (pub.status !== 200) {
          qa.note(`Công bố bằng Thư ký → ${pub.status}; thử bằng Chủ toạ.`)
          qa.expectStatus(await chairApi.post(`/api/meetings/${ids[0]}/transitions`, { action: 'Publish' }), 200)
        }
        qa.expectStatus(await sec.post(`/api/meetings/${ids[0]}/transitions`, { action: 'Start' }), 200)
        await qa.goto(`/calendar?week=${TUE}`)
        await qa.expect(qa.page.getByText('Vào phòng họp').first()).toBeVisible()
        // No settled() here: the call-to-action band pings forever by design (global.css).
        await qa.shot('khoi-dang-dien-ra')
        // Only the meeting's Chair (here quantri@) ends it: chutoa@ is neither creator nor Chair.
        qa.expectStatus(await adminApi.post(`/api/meetings/${ids[0]}/transitions`, { action: 'End' }), 200)
        await qa.page.reload()
        await qa.expect(qa.page.getByText('Vào phòng họp')).toHaveCount(0)
        // Clean up: cancel what is still WaitingApproval (the live one ended, the cancelled one is gone).
        for (const id of ids.slice(1, 5)) {
          qa.expectStatus(await sec.post(`/api/meetings/${id}/transitions`, { action: 'Cancel', reason: 'Dọn kiểm thử FE-16 AC26.' }), 200)
        }
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
      },
    },
    // ------------------------------------------------------------------ new: find-page validations
    {
      id: 'AC27',
      role: 'chair',
      devices: ['desktop'],
      title: 'Tìm giờ họp: thiếu người dự bị chặn; người chưa chia sẻ bị gắn nhãn; ngày sai bị chặn ở client; khoảng ngoài chia sẻ và khoảng hết khung hiện câu trống riêng; tick T7/CN tìm được cuối tuần',
      steps: [
        'Mở /calendar/find, bấm Tìm giờ khi chưa chọn ai',
        'Thêm Thư ký (không chia sẻ) + Chủ toạ (quick pick): đọc nhãn "Chưa được chia sẻ lịch"/"Xem được: Họp Thường"',
        `Khoảng ${dm(THU)}–${dm(FRI)} (ngoài chia sẻ): tìm → câu không phủ`,
        `Khoảng ${dm(TUE)}–${dm(TUE)} thời lượng 4 giờ: tìm → câu hết khung`,
        'Mở rộng chia sẻ tới CN, tick T7/CN, khoảng cả tuần: tìm → có khung Thứ Bảy',
        'Bỏ hết ngày trong tuần: tìm → câu "Chọn ít nhất một ngày trong tuần", không gửi',
      ],
      expected:
        'Thiếu người: "Chọn ít nhất một người dự." + panel "Chọn người dự rồi bấm Tìm giờ", không request; Thư ký: nhãn "Chưa được chia sẻ lịch" + nút "Yêu cầu chia sẻ lịch"; ngoài khoảng: "Lịch được chia sẻ không phủ khoảng ngày này" + gợi ý xin gia hạn; hết khung: "Không còn khung nào trong khoảng đã chọn" + gợi ý (chữ "giãn cách họp" thay "cách cuộc họp khác" trong case); T7/CN tick được, Thứ Bảy có khung sau khi gia hạn chia sẻ; bỏ hết ngày: chặn ở client, không request',
      async run(qa) {
        // Level 2: the Internal meeting counts as busy, so no 4-hour slot fits Tuesday (for E2 below).
        const s = await ensureMemberShare(qa, 'Level2')
        const name = s.owner.fullName
        const secretary = await me(qa, 'secretary')
        await qa.goto('/calendar/find')
        await qa.expect(qa.page.getByRole('heading', { name: 'Tìm giờ họp', level: 1 })).toBeVisible()
        // CAL_FT55: searching with nobody picked is blocked before any request.
        await qa.page.getByRole('button', { name: 'Tìm giờ', exact: true }).first().click()
        await qa.expect(qa.page.getByText('Chọn ít nhất một người dự.')).toBeVisible()
        await qa.expect(qa.page.getByText('Chọn người dự rồi bấm Tìm giờ')).toBeVisible()
        await qa.expect(qa.page.locator('.find-results')).toHaveCount(0)
        await qa.shot('tim-gio-thieu-nguoi-du')
        // CAL_FT56: someone never shared is flagged; quick picks offer those who shared.
        const memberBtn = qa.page.locator('.find-quick').getByRole('button', { name, exact: true })
        await qa.expect(memberBtn).toBeVisible()
        await qa.expect(qa.page.getByText('Người đã chia sẻ lịch với bạn:')).toBeVisible()
        await qa.page.getByRole('combobox', { name: 'Tìm người dự' }).fill(secretary.email)
        await qa.page.getByRole('option', { name: new RegExp(secretary.fullName) }).first().click()
        const picked = qa.page.getByRole('list', { name: 'Người dự đã chọn' })
        await qa.expect(picked.getByText('Chưa được chia sẻ lịch')).toBeVisible()
        await qa.expect(picked.getByRole('button', { name: 'Yêu cầu chia sẻ lịch' })).toBeVisible()
        // The search menu stays open over the quick picks: close it first.
        await qa.page.keyboard.press('Escape')
        await memberBtn.click()
        await qa.expect(picked.getByText('Xem được: Thường và Nội bộ')).toBeVisible()
        await settled(qa)
        await qa.shot('nguoi-du-da-chia-se-va-chua')
        // CAL_FT58 E1: a range the shares do not reach explains itself.
        await typeDay(qa.page.getByLabel('Từ ngày'), THU)
        await typeDay(qa.page.getByLabel('Đến ngày'), FRI)
        await qa.page.getByRole('button', { name: 'Tìm giờ', exact: true }).first().click()
        await qa.expect(qa.page.getByText('Lịch được chia sẻ không phủ khoảng ngày này')).toBeVisible()
        await qa.expect(qa.page.getByText('Chọn khoảng ngày nằm trong thời gian chia sẻ, hoặc xin gia hạn chia sẻ.')).toBeVisible()
        await qa.shot('khoang-ngay-ngoai-chia-se')
        // CAL_FT58 E2: a covered range with no slot left says so (at Mốc 2 the Nội bộ meeting
        // fills Tuesday, so no 4-hour meeting fits).
        await typeDay(qa.page.getByLabel('Từ ngày'), TUE)
        await typeDay(qa.page.getByLabel('Đến ngày'), TUE)
        await qa.page.getByRole('button', { name: /Thời lượng họp/ }).click()
        await qa.page.getByRole('option', { name: '4 giờ' }).click()
        await qa.page.getByRole('button', { name: 'Tìm giờ', exact: true }).first().click()
        await qa.expect(qa.page.getByText('Không còn khung nào trong khoảng đã chọn')).toBeVisible()
        await qa.expect(qa.page.getByText(/giãn cách họp ít hơn/).first()).toBeVisible()
        await qa.shot('khoang-ngay-het-khung')
        await noMeetingContent(qa)
        // CAL_FT60: Saturday and Sunday can be ticked; the weekend is searched once the share reaches it.
        const pair = await pairShare(qa, 'chair', EMAIL.member, EMAIL.chair, { open: true })
        qa.expectStatus(await (await qa.api('member')).patch(`/api/calendar/shares/${pair.id}`, { endDate: SUN }), 200)
        const days = qa.page.getByRole('group', { name: 'Ngày trong tuần' })
        await days.getByRole('button', { name: 'T7' }).click()
        await days.getByRole('button', { name: 'CN' }).click()
        await typeDay(qa.page.getByLabel('Từ ngày'), MON)
        await typeDay(qa.page.getByLabel('Đến ngày'), SUN)
        await qa.page.getByRole('button', { name: /Thời lượng họp/ }).click()
        await qa.page.getByRole('option', { name: '2 giờ' }).click()
        await qa.page.getByRole('button', { name: 'Tìm giờ', exact: true }).first().click()
        const sat = qa.page.getByRole('region', { name: `Thứ Bảy, ${dm(addDays(MON, 5))}` })
        await qa.expect(sat).toBeVisible()
        await qa.expect(sat.getByRole('button', { name: /^08:00/ })).toBeVisible()
        await settled(qa)
        await qa.shot('tim-gio-cuoi-tuan')
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
        qa.expectStatus(await (await qa.api('member')).patch(`/api/calendar/shares/${pair.id}`, { endDate: WED }), 200)
        // CAL_FT59: invalid settings are blocked in the client with its own sentences, no request.
        for (const day of ['T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'CN']) {
          const btn = days.getByRole('button', { name: day, exact: true })
          if ((await btn.getAttribute('aria-pressed')) === 'true') await btn.click()
        }
        await qa.page.getByRole('button', { name: 'Tìm giờ', exact: true }).first().click()
        await qa.expect(qa.page.getByText('Chọn ít nhất một ngày trong tuần.')).toBeVisible()
        // The client blocks before any request: the previous answer stays on screen untouched.
        await qa.shot('ngay-trong-tuan-trong')
        qa.note('FT59: case đòi 400 MSG444 từ server, nhưng FE chặn ở client bằng câu riêng; 400 MSG444 đã phủ ở AC10 (API).')
      },
    },
    {
      id: 'AC28',
      role: 'chair',
      devices: ['desktop'],
      title: 'Tìm giờ họp: gửi yêu cầu thay đổi khi không đổi gì bị khoá im lặng; hộp nháp đổi Chủ toạ, chặn tên trống, xác nhận bỏ soạn, 409 khi khung bị chiếm, chấp nhận người bận',
      steps: [
        'Mở "Xin thay đổi" trên chia sẻ hiệu lực → bấm "Gửi yêu cầu" ngay',
        'Tìm giờ, thêm Thư ký + Chủ toạ, mở khung 15:00 T3: đưa Thư ký xuống rồi lên (đổi Chủ toạ) → Tạo nháp',
        'Mở khung lại: xoá tên → Tạo nháp (tên trống); gõ tên → đóng (xác nhận bỏ soạn)',
        'Tạo+duyệt họp 15:00–16:00 T3 (API) → Tạo nháp lại (409): hộp đóng, toast + tìm lại',
        'POST draft khung bận 09:00 với acceptedBusyUserIds=[Lê An] → 201; huỷ các nháp đã tạo',
      ],
      expected:
        'Không đổi gì: nút "Gửi yêu cầu" khoá, không câu báo nào, không request (case đòi câu báo — lệch sản phẩm); reorder đổi Chủ toạ, họp tạo ra ghi đúng vai; tên trống: "Tên cuộc họp không được để trống.", ở lại hộp; đóng khi đang soạn: "Bỏ cuộc họp đang soạn?" (Tiếp tục soạn giữ, Bỏ cuộc họp đóng, không tạo gì); khung bị chiếm: toast "Khung giờ này không còn trống. Hãy chọn khung khác.", hộp đóng, tìm lại, giữ nguyên bản nháp ở kết quả mới; acceptedBusy: 201 MSG29',
      async run(qa) {
        const s = await ensureMemberShare(qa, 'Level1')
        const name = s.owner.fullName
        const secretary = await me(qa, 'secretary')
        const admin = await me(qa, 'admin')
        // CAL_FT40: sending a change request without any change is blocked silently (no message).
        await qa.goto('/calendar/sharing')
        const line = section(qa, 'Lịch được chia sẻ với tôi').locator(`#share-${s.id}`)
        await line.getByRole('button', { name: 'Thao tác khác' }).click()
        await qa.page.getByRole('menuitem', { name: 'Xin thay đổi' }).click()
        const change = qa.page.getByRole('dialog', { name: 'Xin thay đổi' })
        await qa.expect(change).toBeVisible()
        await qa.expect(change.getByRole('button', { name: 'Gửi yêu cầu' })).toBeDisabled()
        await qa.expect(change.getByText('Chọn mức xem cao hơn hoặc ngày kết thúc muộn hơn.')).toHaveCount(0)
        await settled(qa)
        await qa.shot('xin-thay-doi-khong-doi')
        qa.expect((await pairShare(qa, 'chair', EMAIL.member, EMAIL.chair, { open: true }))?.pendingChange ?? null).toBeNull()
        await change.getByRole('button', { name: 'Huỷ' }).click()
        // Open a slot with two invitees so the roster can be reordered (CAL_FT63).
        await qa.goto('/calendar/find')
        await qa.page.getByRole('combobox', { name: 'Tìm người dự' }).fill(secretary.email)
        await qa.page.getByRole('option', { name: new RegExp(secretary.fullName) }).first().click()
        await qa.page.keyboard.press('Escape')
        await qa.page.locator('.find-quick').getByRole('button', { name, exact: true }).click()
        await findWeek(qa)
        await qa.page.getByRole('button', { name: 'Tìm giờ', exact: true }).first().click()
        const day = qa.page.getByRole('region', { name: `Thứ Ba, ${dm(TUE)}` })
        await qa.expect(day).toBeVisible()
        await day.getByRole('button', { name: /^15:00/ }).click()
        await day.getByRole('button', { name: 'Tạo nháp' }).click()
        const dialog = qa.page.getByRole('dialog', { name: 'Tạo cuộc họp nháp' })
        await qa.expect(dialog).toBeVisible()
        const roster = dialog.getByRole('list', { name: 'Thành phần tham dự' })
        const rows = roster.getByRole('listitem')
        await qa.expect(rows.first()).toContainText(secretary.fullName)
        await qa.expect(rows.first()).toContainText('Chủ toạ')
        // Move Lê An up: he becomes first, hence Chair (UC-81b).
        await roster.getByRole('button', { name: `Đưa ${name} lên` }).click()
        await qa.expect(rows.first()).toContainText(name)
        await qa.expect(rows.first()).toContainText('Chủ toạ')
        const draftTitle = qa.name('FE-16 doi chu toa')
        await dialog.getByRole('textbox', { name: /Tên cuộc họp/ }).fill(draftTitle)
        await settled(qa)
        await qa.shot('doi-chu-toa-trong-hop-thoai')
        const created = qa.page.waitForResponse((r) => r.url().endsWith('/api/calendar/slots/draft') && r.request().method() === 'POST')
        await dialog.getByRole('button', { name: 'Tạo nháp', exact: true }).click()
        const createdRes = await created
        qa.expect(createdRes.status()).toBe(201)
        made.ac28draft = (await createdRes.json()).meeting.id
        // The created meeting records the swapped roster (UC-81b).
        const rosterApi = await (await qa.api()).get(`/api/meetings/${made.ac28draft}/participants`)
        qa.expectStatus(rosterApi, 200)
        const dump = JSON.stringify(rosterApi.body)
        const seen = []
        const walk = (v) => {
          if (Array.isArray(v)) v.forEach(walk)
          else if (v && typeof v === 'object') {
            const uid = v.userId ?? v.user?.id
            const role = v.role ?? v.meetingRole
            if (uid && role) seen.push({ uid, role })
            Object.values(v).forEach(walk)
          }
        }
        walk(rosterApi.body)
        // The reorder stuck: the member (moved to first) chairs, the secretary is a Member (UC-81b).
        qa.expect(seen.find((r) => r.uid === s.owner.id)?.role, dump.slice(0, 300)).toBe('Chair')
        qa.expect(seen.find((r) => r.uid === secretary.id)?.role, dump.slice(0, 300)).toBe('Member')
        await qa.expect(qa.page).toHaveURL(new RegExp(`/mgmt/meetings/${made.ac28draft}`))
        // CAL_FT64 + FT67 need the dialog again: back to the search, reopen the slot.
        await qa.goto('/calendar/find')
        await qa.page.getByRole('combobox', { name: 'Tìm người dự' }).fill(secretary.email)
        await qa.page.getByRole('option', { name: new RegExp(secretary.fullName) }).first().click()
        await qa.page.keyboard.press('Escape')
        await qa.page.locator('.find-quick').getByRole('button', { name, exact: true }).click()
        await findWeek(qa)
        await qa.page.getByRole('button', { name: 'Tìm giờ', exact: true }).first().click()
        const day2 = qa.page.getByRole('region', { name: `Thứ Ba, ${dm(TUE)}` })
        await day2.getByRole('button', { name: /^15:00/ }).click()
        await day2.getByRole('button', { name: 'Tạo nháp' }).click()
        const dialog2 = qa.page.getByRole('dialog', { name: 'Tạo cuộc họp nháp' })
        await qa.expect(dialog2).toBeVisible()
        // CAL_FT64: an empty title is refused inside the dialog, nothing is sent.
        await dialog2.getByRole('textbox', { name: /Tên cuộc họp/ }).fill('')
        await dialog2.getByRole('button', { name: 'Tạo nháp', exact: true }).click()
        await qa.expect(dialog2.getByText('Tên cuộc họp không được để trống.')).toBeVisible()
        await qa.expect(dialog2).toBeVisible()
        await settled(qa)
        await qa.shot('ten-hop-trong-bi-chan')
        // CAL_FT67: closing a touched dialog asks first.
        await dialog2.getByRole('textbox', { name: /Tên cuộc họp/ }).fill(qa.name('FE-16 bo soan'))
        await dialog2.getByRole('button', { name: 'Huỷ' }).click()
        const discard = qa.page.getByRole('dialog', { name: 'Bỏ cuộc họp đang soạn?' })
        await qa.expect(discard).toBeVisible()
        await settled(qa)
        await qa.shot('xac-nhan-bo-soan')
        await discard.getByRole('button', { name: 'Tiếp tục soạn' }).click()
        await qa.expect(discard).toHaveCount(0)
        await qa.expect(dialog2).toBeVisible()
        await dialog2.getByRole('button', { name: 'Huỷ' }).click()
        await qa.expect(qa.page.getByRole('dialog', { name: 'Bỏ cuộc họp đang soạn?' })).toBeVisible()
        await qa.page.getByRole('button', { name: 'Bỏ cuộc họp' }).click()
        await qa.expect(dialog2).toHaveCount(0)
        await qa.expect(qa.page).toHaveURL(/\/calendar\/find/)
        await qa.shot('da-bo-soan')
        // CAL_FT65: meanwhile the slot is taken (API): the dialog closes, a toast says so, the page searches again.
        // The earlier discard may have left the slot selected (a second click would deselect it).
        const slotBtn = day2.getByRole('button', { name: /^15:00/ })
        await slotBtn.click()
        if ((await day2.getByRole('button', { name: 'Tạo nháp' }).count()) === 0) await slotBtn.click()
        await day2.getByRole('button', { name: 'Tạo nháp' }).click()
        const dialog3 = qa.page.getByRole('dialog', { name: 'Tạo cuộc họp nháp' })
        await qa.expect(dialog3).toBeVisible()
        await dialog3.getByRole('textbox', { name: /Tên cuộc họp/ }).fill(qa.name('FE-16 khung bi chiem'))
        const blocker = await (await qa.api('secretary')).post('/api/meetings', {
          title: qa.name('FE-16 chan khung'),
          plannedStartTime: at(TUE, '15:00'),
          plannedEndTime: at(TUE, '16:00'),
          mode: 'InPerson',
          location: 'Phòng QA',
          onlineLink: null,
          sensitivity: 'Normal',
          description: 'Chặn khung kiểm thử FE-16.',
          // A direct meeting needs its own Chair; the member's busyness is what blocks the slot.
          participants: [
            { userId: admin.id, role: 'Chair' },
            { userId: s.owner.id, role: 'Member' },
          ],
        })
        qa.expectStatus(blocker, 201)
        const blockerId = blocker.body.meeting.id
        const blockerSub = await (await qa.api('secretary')).post(`/api/meetings/${blockerId}/transitions`, { action: 'SubmitForApproval', assignedApproverId: (await me(qa, 'chair')).id })
        qa.expectStatus(blockerSub, 200)
        await dialog3.getByRole('button', { name: 'Tạo nháp', exact: true }).click()
        await qa.expect(toast(qa, 'Khung giờ này không còn trống. Hãy chọn khung khác.')).toBeVisible()
        await qa.expect(dialog3).toHaveCount(0)
        await qa.expect(day2.getByRole('button', { name: /^15:00/ })).toHaveCount(0)
        await settled(qa)
        await qa.shot('khung-bi-chiem-tim-lai')
        qa.expectStatus(await (await qa.api('secretary')).post(`/api/meetings/${blockerId}/transitions`, { action: 'Cancel', reason: 'Dọn kiểm thử FE-16 AC28.' }), 200)
        // CAL_FT66: a still-busy person seen before is accepted explicitly (API).
        const busy = await (await qa.api()).post('/api/calendar/slots/draft', {
          title: qa.name('FE-16 chap nhan ban'),
          plannedStartTime: at(TUE, '09:00'),
          plannedEndTime: at(TUE, '10:00'),
          mode: 'InPerson',
          location: '',
          onlineLink: null,
          sensitivity: 'Normal',
          description: '',
          userIds: [s.owner.id],
          includeMe: false,
          bufferMinutes: 30,
          acceptedBusyUserIds: [s.owner.id],
        })
        qa.expectStatus(busy, 201)
        qa.expect(busy.body.code).toBe('MSG29')
        qa.expectStatus(await (await qa.api('chair')).post(`/api/meetings/${busy.body.meeting.id}/transitions`, { action: 'Cancel', reason: 'Dọn kiểm thử FE-16 AC28.' }), 200)
        // Clean up the reorder draft.
        qa.expectStatus(await (await qa.api('chair')).post(`/api/meetings/${made.ac28draft}/transitions`, { action: 'Cancel', reason: 'Dọn kiểm thử FE-16 AC28.' }), 200)
        made.ac28draft = null
        await noRawCodes(qa)
      },
    },
    {
      id: 'AC29',
      role: 'chair',
      devices: ['desktop'],
      title: 'Trả lời muộn (chia sẻ đã bị rút): hộp đóng, toast đỏ MSG450, danh sách tải lại không còn yêu cầu',
      steps: [
        'Lê An xin xem lịch Chủ toạ (API) → Chủ toạ mở Chia sẻ lịch → Đồng ý',
        'Trong lúc hộp "Cho xem lịch" mở, Lê An rút yêu cầu (API)',
        'Bấm Đồng ý trong hộp',
      ],
      expected:
        '409 MSG450 "Không thể thực hiện ở trạng thái hiện tại của chia sẻ." (toast đỏ); hộp đóng; danh sách tải lại, dòng chuyển "Đã rút" (case đòi "không còn yêu cầu" — lệch thiết kế giữ dòng đã kết thúc tại chỗ, xem FT34); API không còn chia sẻ mở (chạy một phiên + API thay hai trình duyệt, cùng đường stale như case)',
      async run(qa) {
        // CAL_FT29 (AX): the two-browser race is staged with one browser + roles over the API.
        // The requester needs MeetingCreator (a Member cannot ask, 403 MSG448), so Thư ký asks.
        await closePair(qa, 'chair', 'secretary')
        const chairSelf = await me(qa, 'chair')
        const req = await (await qa.api('secretary')).post('/api/calendar/shares', {
          ownerId: chairSelf.id,
          level: 'Level1',
          startDate: TODAY,
          endDate: addDays(TODAY, 6),
        })
        qa.expectStatus(req, 201)
        const id = req.body.share.id
        await qa.goto('/calendar/sharing')
        const line = qa.page.locator(`#share-${id}`)
        await qa.expect(line.getByText('Chờ bạn trả lời')).toBeVisible()
        await line.getByRole('button', { name: 'Đồng ý' }).click()
        const dialog = qa.page.getByRole('dialog', { name: 'Cho xem lịch' })
        await qa.expect(dialog).toBeVisible()
        await settled(qa)
        await qa.shot('hop-thoai-tra-loi-muon')
        qa.expectStatus(await (await qa.api('secretary')).post(`/api/calendar/shares/${id}/withdraw`), 200)
        await dialog.getByRole('button', { name: 'Đồng ý' }).click()
        await qa.expect(toast(qa, 'Không thể thực hiện ở trạng thái hiện tại của chia sẻ.')).toBeVisible()
        await qa.expect(dialog).toHaveCount(0)
        await qa.expect(line.getByText('Đã rút')).toBeVisible()
        await settled(qa)
        await qa.shot('tra-loi-muon-tai-lai')
        qa.expect(await pairShare(qa, 'chair', EMAIL.chair, EMAIL.secretary, { open: true })).toBeUndefined()
        await noRawCodes(qa)
      },
    },
    {
      id: 'AC30',
      role: 'chair',
      devices: ['desktop'],
      title: 'Hộp "Xin xem lịch": ngày điền sẵn/mặc định 30 ngày, nút nhanh 1 tuần/1 tháng/3 tháng; câu báo ngày sai; gửi khi chưa chọn ai',
      steps: [
        'Chủ toạ → Chia sẻ lịch → "Xin xem lịch": đọc ngày điền sẵn + mức mặc định',
        'Bấm "1 tuần", "1 tháng", "3 tháng": đọc ngày kết thúc',
        'Ngày bắt đầu hôm qua; kết thúc trước bắt đầu; khoảng 366 ngày; gửi khi chưa chọn ai',
      ],
      expected:
        'Mặc định từ hôm nay đến +29 (30 ngày), mức Họp Thường; 1 tuần → +6, 1 tháng → +29, 3 tháng → +89; "Ngày bắt đầu không được trước hôm nay.", "Ngày kết thúc phải từ ngày bắt đầu trở đi.", "Chia sẻ dài tối đa 365 ngày.", nút Gửi khoá khi sai; chưa chọn ai: "Chọn một người.", không request',
      async run(qa) {
        await closePair(qa, 'admin', 'chair')
        await closePair(qa, 'secretary', 'chair')
        await qa.goto('/calendar/sharing')
        await section(qa, 'Lịch được chia sẻ với tôi').getByRole('button', { name: 'Xin xem lịch' }).click()
        const dialog = qa.page.getByRole('dialog', { name: 'Xin xem lịch' })
        await qa.expect(dialog).toBeVisible()
        const dd = (day) => `${day.slice(8, 10)}/${day.slice(5, 7)}/${day.slice(0, 4)}`
        // getByLabel also matches the calendar picker button: scope to the textboxes.
        const fromInput = dialog.getByRole('textbox', { name: 'Ngày bắt đầu' })
        const toInput = dialog.getByRole('textbox', { name: 'Ngày kết thúc' })
        // CAL_FT16: the start is today, the end moves 7/30/90 days out; the default is 30 days.
        await qa.expect(fromInput).toHaveValue(dd(TODAY))
        await qa.expect(toInput).toHaveValue(dd(addDays(TODAY, 29)))
        await qa.expect(dialog.getByRole('radio', { name: 'Họp Thường' })).toBeChecked()
        await settled(qa)
        await qa.shot('xin-xem-lich-mac-dinh')
        await dialog.getByRole('button', { name: '1 tuần' }).click()
        await qa.expect(toInput).toHaveValue(dd(addDays(TODAY, 6)))
        await dialog.getByRole('button', { name: '1 tháng' }).click()
        await qa.expect(toInput).toHaveValue(dd(addDays(TODAY, 29)))
        await dialog.getByRole('button', { name: '3 tháng' }).click()
        await qa.expect(toInput).toHaveValue(dd(addDays(TODAY, 89)))
        await qa.shot('xin-xem-lich-3-thang')
        // CAL_FT15: wrong dates are refused before any request, with the field's own sentence.
        await typeDay(fromInput, addDays(TODAY, -1))
        await qa.expect(dialog.getByText('Ngày bắt đầu không được trước hôm nay.')).toBeVisible()
        await qa.expect(dialog.getByRole('button', { name: 'Gửi yêu cầu' })).toBeDisabled()
        await qa.shot('loi-ngay-bat-dau')
        await typeDay(fromInput, TODAY)
        await typeDay(toInput, addDays(TODAY, -1))
        await qa.expect(dialog.getByText('Ngày kết thúc phải từ ngày bắt đầu trở đi.')).toBeVisible()
        await qa.expect(dialog.getByRole('button', { name: 'Gửi yêu cầu' })).toBeDisabled()
        await typeDay(toInput, addDays(TODAY, 365))
        await qa.expect(dialog.getByText('Chia sẻ dài tối đa 365 ngày.')).toBeVisible()
        await qa.expect(dialog.getByRole('button', { name: 'Gửi yêu cầu' })).toBeDisabled()
        await typeDay(toInput, addDays(TODAY, 6))
        await dialog.getByRole('button', { name: 'Gửi yêu cầu' }).click()
        await qa.expect(dialog.getByText('Chọn một người.')).toBeVisible()
        qa.expect(await pairShare(qa, 'chair', EMAIL.admin, EMAIL.chair, { open: true })).toBeUndefined()
        await qa.shot('xin-xem-lich-chua-chon-ai')
        await dialog.getByRole('button', { name: 'Huỷ' }).click()
        await noRawCodes(qa)
      },
    },
    {
      id: 'AC31',
      role: 'chair',
      devices: ['desktop'],
      title: 'Người gửi bị vô hiệu hoá chỉ còn Từ chối; mất quyền tạo cuộc họp thì chia sẻ thành "Đã dừng"',
      steps: [
        'Thư ký xin xem lịch Chủ toạ (API) → vô hiệu hoá Thư ký → Chủ toạ mở: chỉ Từ chối → từ chối → kích hoạt lại, đăng nhập được',
        'Dựng chia sẻ hiệu lực Lê An→Chủ toạ → gỡ MeetingCreator khỏi vai trò Chủ toạ → Chủ toạ mất thẻ xem, chia sẻ "Đã dừng" + báo chủ lịch → trả quyền',
      ],
      expected:
        'Vô hiệu hoá: dòng "Tài khoản người gửi đã bị vô hiệu hoá, chỉ từ chối được.", không nút Đồng ý; từ chối 200 MSG437; kích hoạt lại, /api/auth/me 200. Mất quyền: thẻ "Lịch được chia sẻ với tôi" mất, bên xem hết thấy chia sẻ, bên chủ thấy "Đã dừng" + CalendarShareStopped cho chủ; trả quyền xong matrix có MeetingCreator',
      async run(qa) {
        const adminApi = await qa.api('admin')
        // Prologue: a crashed earlier run may have left the chair role stripped or Thư ký deactivated.
        await ensureChairCreator(qa)
        const bootstrapped = await me(qa, 'secretary')
        const asAdmin = await adminApi.get(`/api/users/${bootstrapped.id}`)
        qa.expectStatus(asAdmin, 200)
        if (asAdmin.body.isActive === false)
          qa.expectStatus(await adminApi.put(`/api/users/${bootstrapped.id}/status`, { isActive: true }), 200)
        // CAL_FT28: a request whose sender was deactivated can only be declined.
        // The requester needs MeetingCreator, so Thư ký (not the Member) asks here.
        await closePair(qa, 'chair', 'secretary')
        const chair = await me(qa, 'chair')
        const secretary = await me(qa, 'secretary')
        const req = await (await qa.api('secretary')).post('/api/calendar/shares', {
          ownerId: chair.id,
          level: 'Level1',
          startDate: TODAY,
          endDate: addDays(TODAY, 6),
        })
        qa.expectStatus(req, 201)
        qa.expectStatus(await adminApi.put(`/api/users/${secretary.id}/status`, { isActive: false }), 200)
        await qa.goto('/calendar/sharing')
        const line = qa.page.locator(`#share-${req.body.share.id}`)
        await qa.expect(line.getByText('Tài khoản người gửi đã bị vô hiệu hoá, chỉ từ chối được.')).toBeVisible()
        await qa.expect(line.getByRole('button', { name: 'Đồng ý' })).toHaveCount(0)
        await settled(qa)
        await qa.shot('nguoi-gui-bi-vo-hieu-hoa')
        await line.getByRole('button', { name: 'Từ chối' }).click()
        const confirm = qa.page.getByRole('dialog', { name: 'Từ chối cho xem lịch?' })
        await qa.expect(confirm).toBeVisible()
        await confirm.getByRole('button', { name: 'Từ chối' }).click()
        await qa.expect(toast(qa, 'Đã từ chối yêu cầu chia sẻ.')).toBeVisible()
        qa.expectStatus(await adminApi.put(`/api/users/${secretary.id}/status`, { isActive: true }), 200)
        qa.expectStatus(await (await qa.api('secretary')).get('/api/auth/me'), 200)
        // CAL_FT46: losing MeetingCreator turns the viewer's shares into "Đã dừng".
        const s = await ensureMemberShare(qa, 'Level1')
        const role = await ensureChairCreator(qa)
        const reduced = role.capabilities.filter((c) => c !== 'MeetingCreator')
        qa.expect(reduced.length).toBeLessThan(role.capabilities.length)
        qa.expectStatus(await adminApi.put('/api/roles/capabilities', { roleId: role.id, capabilities: reduced }), 200)
        await qa.goto('/calendar/sharing')
        await qa.expect(qa.page.getByRole('heading', { name: /Lịch được chia sẻ với tôi/ })).toHaveCount(0)
        await qa.expect(qa.page.getByRole('radiogroup', { name: 'Chia sẻ lịch' })).toHaveCount(0)
        await settled(qa)
        await qa.shot('mat-quyen-tao-hop')
        // The viewer no longer sees the share anywhere; the owner still sees it, Stopped.
        qa.expect(await pairShare(qa, 'chair', EMAIL.member, EMAIL.chair, { open: true })).toBeUndefined()
        qa.expect((await shares(qa, 'chair')).find((x) => x.id === s.id)).toBeUndefined()
        const seen = (await shares(qa, 'member')).find((x) => x.id === s.id)
        qa.expect(seen).toMatchObject({ status: 'Stopped', actions: [] })
        const notes = await (await qa.api('member')).get('/api/notifications?page=1&pageSize=50')
        qa.expectStatus(notes, 200)
        qa.expect(notes.body.items.find((n) => n.type === 'CalendarShareStopped' && n.targetId === s.id), 'no CalendarShareStopped for Lê An').toBeTruthy()
        const restore = await adminApi.put('/api/roles/capabilities', { roleId: role.id, capabilities: role.capabilities })
        qa.expectStatus(restore, 200)
        qa.expect(restore.body.code).toBe('MSG16')
        const matrix2 = await adminApi.get('/api/roles/capabilities')
        qa.expect(matrix2.body.roles.find((r) => r.name === role.name).capabilities).toContain('MeetingCreator')
        await noRawCodes(qa)
      },
    },
    {
      id: 'AC32',
      role: 'member',
      devices: ['desktop'],
      title: 'Chia sẻ cho người vừa mất quyền tạo cuộc họp: 400 MSG455, hộp ở lại với dữ liệu đã nhập',
      steps: [
        'Lê An mở "Chia sẻ lịch", chọn Chủ toạ',
        'Quản trị gỡ MeetingCreator khỏi vai trò Chủ toạ (API)',
        'Bấm "Chia sẻ" → câu MSG455, hộp ở lại → trả quyền, không chia sẻ nào được tạo',
      ],
      expected:
        '400 MSG455 "Người này không nhận được chia sẻ lịch." hiện trong hộp; hộp vẫn mở, người đã chọn còn đó; sau khi trả quyền, không có chia sẻ mở nào Lê An→Chủ toạ',
      async run(qa) {
        // CAL_FT22 (needs a staged role change: the viewer loses MeetingCreator mid-dialog).
        await closePair(qa, 'member', 'chair')
        await ensureChairCreator(qa)
        const adminApi = await qa.api('admin')
        const role = await ensureChairCreator(qa)
        await qa.goto('/calendar/sharing')
        await section(qa, 'Người xem được lịch của tôi').getByRole('button', { name: 'Chia sẻ lịch', exact: true }).click()
        const dialog = qa.page.getByRole('dialog', { name: 'Chia sẻ lịch của tôi' })
        await qa.expect(dialog).toBeVisible()
        await dialog.getByRole('searchbox').fill(EMAIL.chair)
        const row = dialog.locator('label.pick-row').filter({ hasText: EMAIL.chair })
        await qa.expect(row).toBeVisible()
        await row.getByRole('radio').check()
        // chutoa's role row (ensured complete above): strip MeetingCreator, restore after the refusal.
        qa.expect(role.capabilities.includes('MeetingCreator'), 'prologue left the chair role stripped').toBe(true)
        const reduced = role.capabilities.filter((c) => c !== 'MeetingCreator')
        qa.expect(reduced.length).toBeLessThan(role.capabilities.length)
        qa.expectStatus(await adminApi.put('/api/roles/capabilities', { roleId: role.id, capabilities: reduced }), 200)
        await dialog.getByRole('button', { name: 'Chia sẻ', exact: true }).click()
        await qa.expect(dialog.getByText('Người này không nhận được chia sẻ lịch.')).toBeVisible()
        await qa.expect(dialog).toBeVisible()
        await qa.expect(row.getByRole('radio')).toBeChecked()
        await settled(qa)
        await qa.shot('chia-se-nguoi-mat-quyen')
        qa.expectStatus(await adminApi.put('/api/roles/capabilities', { roleId: role.id, capabilities: role.capabilities }), 200)
        qa.expect(await pairShare(qa, 'member', EMAIL.member, EMAIL.chair, { open: true })).toBeUndefined()
        await noRawCodes(qa)
      },
    },
    {
      id: 'AC33',
      role: 'member',
      devices: ['desktop'],
      title: 'Chủ lịch ngừng chia sẻ: hỏi xác nhận, dòng "Đã ngừng", bên xem hết tuần và được báo',
      steps: ['Lê An mở Chia sẻ lịch: dòng Chủ toạ → "…" → Ngừng chia sẻ → Ngừng chia sẻ'],
      expected:
        'Hộp "Ngừng chia sẻ lịch?" ("Trần Chủ Toạ sẽ không còn thấy giờ bận của bạn."); 200 MSG451 toast "Đã ngừng chia sẻ lịch.", dòng "Đã ngừng" mờ; Chủ toạ có CalendarShareEnded; tìm giờ: Lê An NotShared; đổi chia sẻ đã ngừng → 409 MSG450',
      async run(qa) {
        // CAL_FT44: the owner ends a share (the viewer side is AC18).
        const s = await ensureMemberShare(qa, 'Level1')
        const name = (await me(qa, 'chair')).fullName
        await qa.goto('/calendar/sharing')
        const line = section(qa, 'Người xem được lịch của tôi').locator(`#share-${s.id}`)
        await line.getByRole('button', { name: 'Thao tác khác' }).click()
        await qa.page.getByRole('menuitem', { name: 'Ngừng chia sẻ' }).click()
        const confirm = qa.page.getByRole('dialog', { name: 'Ngừng chia sẻ lịch?' })
        await qa.expect(confirm).toBeVisible()
        await qa.expect(confirm.getByText(`${name} sẽ không còn thấy giờ bận của bạn.`)).toBeVisible()
        await settled(qa)
        await qa.shot('xac-nhan-ngung-chia-se')
        await confirm.getByRole('button', { name: 'Ngừng chia sẻ' }).click()
        await qa.expect(toast(qa, 'Đã ngừng chia sẻ lịch.')).toBeVisible()
        await qa.expect(line.getByText('Đã ngừng')).toBeVisible()
        await qa.expect(line).toHaveClass(/share-row-closed/)
        await qa.shot('da-ngung-chia-se')
        const closed = await pairShare(qa, 'member', EMAIL.member, EMAIL.chair, { open: true })
        qa.expect(closed).toBeUndefined()
        const notes = await (await qa.api('chair')).get('/api/notifications?page=1&pageSize=50')
        qa.expectStatus(notes, 200)
        qa.expect(notes.body.items.find((n) => n.type === 'CalendarShareEnded' && n.targetId === s.id), 'no CalendarShareEnded for chutoa@').toBeTruthy()
        const res = await slots(qa, { userIds: [s.owner.id], durationMinutes: 60, fromDate: TUE, toDate: TUE })
        qa.expectStatus(res, 200)
        qa.expect(res.body.people[0].status).toBe('NotShared')
        qa.expectStatus(await (await qa.api('member')).patch(`/api/calendar/shares/${s.id}`, { level: 'Level1' }), 409)
        await noRawCodes(qa)
      },
    },
    // ------------------------------------------------------------------ cleanup
    // ------------------------------------------------------------------ CAL_FT11 E2: decline from the overview
    {
      id: 'AC34',
      role: 'member',
      devices: ['desktop'],
      title: 'Thành viên "Không tham dự" từ pop-over trên lịch: cuộc họp biến mất ngay, không hiện lại sau tải lại; cuộc họp còn lại và cuộc họp không liên quan giữ nguyên',
      steps: [
        'Dựng (API) 2 cuộc họp Đã công bố T5 có thanhvien@ và 1 cuộc họp Đã công bố T5 không có thanhvien@',
        `thanhvien@ mở /calendar?week=${TUE}: thấy 2 cuộc có tên mình, không thấy cuộc thứ ba`,
        'Bấm khối cuộc họp A → pop-over "Xác nhận tham dự?" → Không tham dự → hộp "Không tham dự cuộc họp?" → xác nhận',
        'Đọc lịch; tải lại; mở Danh sách',
      ],
      expected:
        'Hộp xác nhận ghi "Cuộc họp sẽ không còn hiện trên lịch của bạn."; sau khi xác nhận khối A biến mất, khối B còn; tải lại và chế độ Danh sách vẫn không có A; cuộc thứ ba không bao giờ hiện; API GET /api/meetings?excludeDeclined=true không trả A, invitation-response của A là Declined',
      async run(qa) {
        const sec = await qa.api('secretary')
        const chairApi = await qa.api('chair')
        const memberApi = await qa.api('member')
        const admin = await me(qa, 'admin')
        const member = await me(qa, 'member')
        const chair = await me(qa, 'chair')
        const secretary = await me(qa, 'secretary')
        await sweepMeetings(qa, sec, ['FE-16 tu choi'], 'Dọn kiểm thử FE-16 AC34 (sót lần trước).')
        const ids = []
        try {
          const mk = async (title, from, to, withMember) => {
            const participants = [
              { userId: admin.id, role: 'Chair' },
              { userId: secretary.id, role: 'Secretary' },
              ...(withMember ? [{ userId: member.id, role: 'Member' }] : []),
            ]
            const res = await sec.post('/api/meetings', {
              title: qa.name(title),
              plannedStartTime: at(THU, from),
              plannedEndTime: at(THU, to),
              mode: 'InPerson',
              location: 'Phòng QA',
              onlineLink: null,
              sensitivity: 'Normal',
              description: 'Dữ liệu kiểm thử FE-16 AC34, sẽ huỷ sau khi chạy.',
              participants,
            })
            qa.expectStatus(res, 201)
            const id = res.body.meeting.id
            ids.push(id)
            qa.expectStatus(await sec.post(`/api/meetings/${id}/transitions`, { action: 'SubmitForApproval', assignedApproverId: chair.id }), 200)
            qa.expectStatus(await chairApi.post(`/api/meetings/${id}/transitions`, { action: 'Approve' }), 200)
            qa.expectStatus(await sec.post(`/api/meetings/${id}/transitions`, { action: 'Publish' }), 200)
            return id
          }
          const a = await mk('FE-16 tu choi A', '08:00', '09:00', true)
          const b = await mk('FE-16 tu choi B', '10:00', '11:00', true)
          await mk('FE-16 tu choi C khong lien quan', '13:00', '14:00', false)
          const link = (name) => qa.page.getByText(qa.name(name), { exact: false }).first()

          await qa.goto(`/calendar?week=${TUE}`)
          await qa.expect(link('FE-16 tu choi A')).toBeVisible()
          await qa.expect(link('FE-16 tu choi B')).toBeVisible()
          await qa.expect(qa.page.getByText(/FE-16 tu choi C/)).toHaveCount(0)
          await settled(qa)
          await qa.shot('truoc-khi-tu-choi')

          await link('FE-16 tu choi A').click()
          const pop = qa.page.getByRole('dialog').first()
          await qa.expect(pop.getByText('Xác nhận tham dự?')).toBeVisible()
          await settled(qa)
          await qa.shot('pop-over-xac-nhan-tham-du')
          await pop.getByRole('button', { name: 'Không tham dự' }).click()
          const confirm = qa.page.getByRole('dialog', { name: 'Không tham dự cuộc họp?' })
          await qa.expect(confirm).toBeVisible()
          await qa.expect(confirm.getByText('Cuộc họp sẽ không còn hiện trên lịch của bạn.')).toBeVisible()
          await qa.shot('hop-xac-nhan-khong-tham-du')
          await confirm.getByRole('button', { name: 'Không tham dự' }).click()

          await qa.expect(link('FE-16 tu choi A')).toHaveCount(0)
          await qa.expect(link('FE-16 tu choi B')).toBeVisible()
          await settled(qa)
          await qa.shot('sau-khi-tu-choi')

          await qa.page.reload()
          await qa.expect(link('FE-16 tu choi B')).toBeVisible()
          await qa.expect(link('FE-16 tu choi A')).toHaveCount(0)
          await qa.expect(qa.page.getByText(/FE-16 tu choi C/)).toHaveCount(0)
          await qa.goto(`/calendar?week=${TUE}&mode=list`)
          await qa.expect(qa.page.getByText(/FE-16 tu choi B/).first()).toBeVisible()
          await qa.expect(qa.page.getByText(/FE-16 tu choi A/)).toHaveCount(0)
          await settled(qa)
          await qa.shot('danh-sach-sau-tai-lai', { fullPage: true })

          const listed = await memberApi.get('/api/meetings?excludeDeclined=true&pageSize=50&q=FE-16%20tu%20choi')
          qa.expectStatus(listed, 200)
          const idsListed = (listed.body.items ?? []).map((m) => m.id)
          qa.expect(idsListed.includes(a), 'A còn trong danh sách đã loại họp từ chối').toBe(false)
          qa.expect(idsListed.includes(b), 'B phải còn').toBe(true)
          const answer = await memberApi.get(`/api/meetings/${a}/invitation-response`)
          qa.expectStatus(answer, 200)
          qa.expect(JSON.stringify(answer.body)).toContain('Declined')
        } finally {
          for (const id of ids) await sec.post(`/api/meetings/${id}/transitions`, { action: 'Cancel', reason: 'Dọn dữ liệu kiểm thử FE-16 AC34.' })
        }
      },
    },
    {
      id: 'CLEANUP',
      role: 'secretary',
      title: 'Dọn dữ liệu QA: huỷ các cuộc họp [QA] FE-16 của lần chạy, đóng các chia sẻ đã mở',
      steps: ['Huỷ các cuộc họp đã tạo', 'Đóng chia sẻ thanhvien→chutoa và quantri→chutoa nếu còn mở'],
      expected: 'Mỗi cuộc họp 200 MSG261 (Đã huỷ); không còn chia sẻ mở nào do kế hoạch tạo',
      async run(qa) {
        const left = []
        const sec = await qa.api('secretary')
        for (const [key, id] of made.meetings) {
          const r = await sec.post(`/api/meetings/${id}/transitions`, { action: 'Cancel', reason: 'Dọn dữ liệu kiểm thử FE-16.' })
          if (r.status !== 200) left.push(`meeting ${key} ${id}: ${r.status}`)
        }
        for (const id of [made.slotDraftId, made.extraDraftId].filter(Boolean)) {
          const r = await (await qa.api('chair')).post(`/api/meetings/${id}/transitions`, { action: 'Cancel', reason: 'Dọn dữ liệu kiểm thử FE-16.' })
          if (r.status !== 200) left.push(`slot draft ${id}: ${r.status}`)
        }
        // Best effort for AC25/AC28 leftovers (AC26 cancels its own; a crashed AC may leave some).
        for (const id of [...(made.ac25 ?? []), made.ac28draft].filter(Boolean)) {
          const r = await sec.post(`/api/meetings/${id}/transitions`, { action: 'Cancel', reason: 'Dọn dữ liệu kiểm thử FE-16.' })
          if (r.status !== 200 && r.status !== 409) left.push(`ac25/28 ${id}: ${r.status}`)
        }
        await closePair(qa, 'member', 'chair')
        await closePair(qa, 'admin', 'chair')
        if (await pairShare(qa, 'member', EMAIL.member, EMAIL.chair, { open: true })) left.push('thanhvien→chutoa còn mở')
        if (await pairShare(qa, 'admin', EMAIL.admin, EMAIL.chair, { open: true })) left.push('quantri→chutoa còn mở')
        qa.note(`Đã huỷ ${made.meetings.size + (made.slotDraftId ? 1 : 0)} cuộc họp [QA]; tài khoản ${TEMP_VIEWER} để ở trạng thái vô hiệu hoá.`)
        qa.expect(left, left.join('\n')).toEqual([])
      },
    },
  ],
}

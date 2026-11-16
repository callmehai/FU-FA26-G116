// FE-16 — Lịch làm việc & tìm giờ họp (QA subtask SCRUM-203): the working calendar (UC-17 AF-4, Report 3 §4.1–4.2),
// calendar sharing (UC-84a → UC-84f), the shared week (UC-85c) and finding a time (UC-85a, UC-85b). Criteria come from
// docs/01-requirements/05-use-case-specifications.md, BR-414 → BR-431, docs/03-api/23-chia-se-lich.md and
// docs/03-api/24-tim-gio-hop.md; ADR-0058.
//
// The criteria are stateful and run in order (the share flow is one story told by two people). Each one reads the state
// it needs from the API, so a criterion run alone with --only does its own preparation where it can.
//
// Data the plan creates, all its own:
// - four "[QA] FE-16 …" meetings next Tuesday, created by the secretary seed (thuky@) with quantri@ as Chair and
//   thanhvien@ as Member: Normal 09:00–10:00, Draft 10:30–11:30, Internal 13:30–14:30, Secret 15:30–16:30; the three
//   non-draft ones are submitted for approval (WaitingApproval counts as busy, BR-420);
// - one draft meeting created from a slot by chutoa@ (UC-85b);
// - calendar shares thanhvien@ → chutoa@ and quantri@ → chutoa@ (no such pair is open before the plan runs);
// - one account "[QA] FE-16 người xem tạm" (qa-fe16-viewer@trustcabinet.local, role Thư ký) to see a share stop when an
//   account is deactivated (BR-424). Accounts are never deleted; the plan leaves it deactivated and re-uses it.
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

/** The page shows none of the owner's meeting titles: a shared view is busy/free only (BR-427). */
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

/** Creates the owner's four meetings once per run (or re-finds this run's ones). */
async function ensureMeetings(qa) {
  if (made.meetings.size === MEETINGS.length) return made.meetings
  const sec = await qa.api('secretary')
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

/** The keys of every object in a JSON tree: a shared view must carry no meeting field (BR-427). */
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

/** Opens the "Chia sẻ lịch" page from the account menu at the foot of the sidebar (UC-84f step 1). */
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
    // ------------------------------------------------------------------ UC-84f, the page for everyone
    {
      id: 'AC1',
      role: 'member',
      devices: ['desktop', 'phone'],
      title: 'Thành viên (không có quyền tạo cuộc họp) mở Chia sẻ lịch từ menu tài khoản: chỉ có danh sách người xem lịch của mình',
      steps: ['Đăng nhập thanhvien@', 'Menu tài khoản → Chia sẻ lịch'],
      expected:
        'Trang "Chia sẻ lịch" có thẻ "Người xem được lịch của tôi" và nút "Chia sẻ lịch"; không có thẻ "Lịch được chia sẻ với tôi", không có nút "Xin xem lịch", không có tab "Lịch được chia sẻ" (UC-84f AF-2, BR-430); không cuộn ngang',
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
    // ------------------------------------------------------------------ UC-84b
    {
      id: 'AC2',
      role: 'member',
      title: 'Chủ lịch tự chia sẻ lịch cho người tổ chức họp: luôn Mốc 1 (Họp Thường), chờ bên nhận đồng ý',
      steps: [
        'Đăng nhập thanhvien@, mở Chia sẻ lịch',
        'Bấm "Chia sẻ lịch", tìm "chutoa"',
        'Xem danh sách: chỉ người có quyền tạo cuộc họp, người đã có chia sẻ bị làm mờ "Đã có"',
        'Chọn Chủ toạ, giữ thời gian mặc định, bấm "Chia sẻ"',
      ],
      expected:
        'Hộp thoại không có chọn mức (luôn Họp Thường); thuky@ (đã có chia sẻ) hiện "Đã có", không chọn được; toast MSG441 "Đã chia sẻ lịch. Đang chờ bên nhận đồng ý."; dòng chutoa@ "Chờ trả lời", "Họp Thường"; API: Pending, Level1',
      async run(qa) {
        // Start clean: an open share of this pair can only be this plan's own, left by an earlier run.
        const old = await closePair(qa, 'member', 'chair')
        if (old) qa.note(`Đã đóng chia sẻ thanhvien→chutoa còn mở từ lần chạy trước (${old}).`)
        const chair = await me(qa, 'chair')
        await qa.goto('/calendar/sharing')
        await section(qa, 'Người xem được lịch của tôi').getByRole('button', { name: 'Chia sẻ lịch', exact: true }).click()
        const dialog = qa.page.getByRole('dialog', { name: 'Chia sẻ lịch của tôi' })
        await qa.expect(dialog).toBeVisible()
        await qa.expect(dialog.getByRole('group', { name: 'Mức xem' })).toHaveCount(0)
        await qa.expect(dialog.getByText('Mức xem', { exact: true })).toHaveCount(0)
        // A person with an open share to them is listed but cannot be picked (BR-423).
        await dialog.getByRole('searchbox').fill('thuky')
        const thuky = dialog.locator('label.pick-row').filter({ hasText: EMAIL.secretary })
        await qa.expect(thuky).toBeVisible()
        await qa.expect(thuky.getByRole('radio')).toBeDisabled()
        await qa.expect(thuky.getByText('Đã có')).toBeVisible()
        await qa.shot('da-co-chia-se-bi-lam-mo')
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
    // ------------------------------------------------------------------ UC-84c
    {
      id: 'AC3',
      role: 'chair',
      title: 'Người tổ chức nhận chia sẻ: thấy "Chờ bạn trả lời", Đồng ý → Đang chia sẻ; người gửi được báo',
      steps: ['Đăng nhập chutoa@, mở Chia sẻ lịch', 'Thẻ "Lịch được chia sẻ với tôi": dòng Lê An → Đồng ý → Đồng ý'],
      expected:
        'Dòng "Chờ bạn trả lời" có Từ chối / Đồng ý; hộp thoại "Nhận chia sẻ lịch" chỉ hiện Mức xem và Thời gian; toast MSG436 "Đã chấp nhận chia sẻ lịch."; dòng chuyển "Đang chia sẻ"; thanhvien@ có thông báo CalendarShareAccepted (BR-426)',
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
      },
    },
    // ------------------------------------------------------------------ UC-84d AF-1, validation, duplicates
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
        // admin → member: the member has no MeetingCreator, cannot receive a share (UC-84b EX-1).
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
    // ------------------------------------------------------------------ UC-85c at Mốc 1
    {
      id: 'AC6',
      role: 'chair',
      devices: ['desktop', 'phone'],
      title: 'Lịch được chia sẻ (Mốc 1): chỉ giờ bận họp Thường; họp Nội bộ, Mật, Nháp không hiện; ngày ngoài khoảng là "Chưa biết"; không lộ tên cuộc họp',
      steps: [
        `Chủ lịch thanhvien@ có 4 cuộc họp ${dm(TUE)}: Thường 09:00–10:00 (chờ duyệt), Nháp 10:30–11:30, Nội bộ 13:30–14:30, Mật 15:30–16:30`,
        `Chia sẻ Mốc 1 đến ${dm(WED)}`,
        `chutoa@ mở Chia sẻ lịch → tab "Lịch được chia sẻ", tuần ${dm(MON)}`,
      ],
      expected: `Chip "Lê An · Họp Thường · đến ${dm(WED)}"; T3 ${dm(TUE)} chỉ có khối "bận 09:00–10:00"; không có khối 10:30, 13:30, 15:30; T5 ${dm(THU)}, T6 ${dm(FRI)} ghi "Chưa biết"; không có tên, địa điểm cuộc họp (BR-414, BR-415, BR-417, BR-420, BR-427); API không có trường cuộc họp; không cuộn ngang`,
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
        await qa.expect(qa.page.getByRole('heading', { name: new RegExp(`${dm(MON)} – ${dm(FRI)}`) })).toBeVisible()
        const chip = qa.page.getByRole('group', { name: 'Người chia sẻ' }).getByRole('button', { name: new RegExp(name) })
        await qa.expect(chip).toContainText(`Họp Thường · đến ${dm(WED)}`)
        if (qa.device === 'phone') {
          await qa.page.getByRole('radiogroup', { name: 'Ngày trong tuần' }).getByRole('radio', { name: `T3 ${TUE.slice(8, 10)}` }).check()
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
        if (qa.device === 'phone') await qa.page.getByRole('radiogroup', { name: 'Ngày trong tuần' }).getByRole('radio', { name: `T5 ${THU.slice(8, 10)}` }).check()
        await qa.expect(qa.page.getByText(new RegExp(`Chưa biết: .*${name}`)).first()).toBeVisible()
        await settled(qa)
        await qa.shot('ngay-ngoai-khoang-chua-biet', { fullPage: true })
      },
    },
    // ------------------------------------------------------------------ UC-85a in the UI
    {
      id: 'AC7',
      role: 'chair',
      devices: ['desktop', 'phone'],
      title: 'Tìm giờ họp: chọn người đã chia sẻ, Tìm giờ → khung theo ngày; giờ bận họp Thường bị tránh, ngày "Chưa biết" không được đề xuất',
      steps: ['chutoa@ mở Tìm giờ họp (/calendar/find)', 'Chọn nhanh "Lê An" (Người đã chia sẻ lịch với bạn)', `Khoảng ngày ${dm(MON)} – ${dm(FRI)}, còn lại mặc định (2 giờ, đệm 30 phút)`, 'Bấm Tìm giờ'],
      expected: `Dòng Lê An "Xem được: Họp Thường"; ${dm(TUE)} có giờ bắt đầu 15:00 (trùng họp Mật không được tiết lộ, BR-414) và không có 08:00, 08:30, 09:00, 09:30, 10:00; không có ngày ${dm(THU)}, ${dm(FRI)} (ngoài khoảng chia sẻ, BR-415); không lộ tên cuộc họp; không cuộn ngang`,
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
    // ------------------------------------------------------------------ UC-85a rules on the API
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
        'Có 10:30 (Nháp, BR-420), 13:30 (Nội bộ ở Mốc 1, BR-417), 15:30 (Mật, BR-414); không có 08:00–10:00 (họp 09:00–10:00 + đệm, BR-422); allFree; quantri@ status NotShared; ngoài khoảng → days rỗng; không trường cuộc họp nào',
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
    // ------------------------------------------------------------------ UC-85b
    {
      id: 'AC9',
      role: 'chair',
      title: 'Tạo nháp từ khung giờ (trùng họp Mật không được tiết lộ): cuộc họp Nháp, người đầu làm Chủ toạ, người tìm làm Thư ký',
      steps: ['Tìm giờ họp với Lê An', `Chọn ${dm(TUE)} 15:00 → Tạo nháp`, `Tên "${'[QA] FE-16 nháp từ khung giờ'}" → Tạo nháp`],
      expected:
        'Hộp thoại "Tạo cuộc họp nháp" điền sẵn ngày giờ, Lê An "Chủ toạ", tôi "Thư ký"; không có cảnh báo nào về họp Mật; toast MSG29 "Tạo cuộc họp thành công." và mở trang cuộc họp; API: trạng thái Draft, thanhvien Chair, chutoa Secretary (BR-428, BR-429)',
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
    // ------------------------------------------------------------------ UC-85b EX-1, UC-85a EX-1
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
    // ------------------------------------------------------------------ UC-84d
    {
      id: 'AC11',
      role: 'chair',
      title: 'Bên xem xin nâng lên Mốc 2 (Thường và Nội bộ): yêu cầu chờ chủ lịch, chia sẻ giữ như cũ',
      steps: ['chutoa@ mở Chia sẻ lịch', 'Dòng Lê An → "…" → Xin thay đổi', 'Chọn "Thường và Nội bộ" → Gửi yêu cầu'],
      expected: 'Toast MSG454 "Đã gửi yêu cầu thay đổi chia sẻ."; dòng vẫn "Đang chia sẻ", "Họp Thường", có dòng "Bạn xin đổi: Thường và Nội bộ"; API pendingChange.level Level2',
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
        await settled(qa)
        await qa.shot('xin-nang-moc-2')
        await dialog.getByRole('button', { name: 'Gửi yêu cầu' }).click()
        await qa.expect(toast(qa, 'Đã gửi yêu cầu thay đổi chia sẻ.')).toBeVisible()
        await qa.expect(line.getByText(/Bạn xin đổi: Thường và Nội bộ/)).toBeVisible()
        await qa.expect(line.getByText('Đang chia sẻ')).toBeVisible()
        await qa.expect(line.getByText('Họp Thường')).toBeVisible()
        await qa.shot('dang-cho-chu-lich-duyet')
        const after = await pairShare(qa, 'chair', EMAIL.member, EMAIL.chair, { open: true })
        qa.expect(after.level).toBe('Level1')
        qa.expect(after.pendingChange?.level).toBe('Level2')
      },
    },
    // ------------------------------------------------------------------ UC-84c AF-1
    {
      id: 'AC12',
      role: 'member',
      devices: ['desktop', 'phone'],
      title: 'Chủ lịch duyệt thay đổi đang chờ: chia sẻ lên Thường và Nội bộ',
      steps: ['thanhvien@ mở Chia sẻ lịch', 'Dòng chutoa@ "Xin đổi: Thường và Nội bộ" → Đồng ý → Đồng ý'],
      expected: 'Hộp thoại "Đồng ý thay đổi" hiện "Họp Thường → Thường và Nội bộ"; toast MSG442 "Đã cập nhật chia sẻ lịch."; chip mức "Thường và Nội bộ"; nút chạm cao ≥ 40px trên điện thoại',
      async run(qa) {
        let s = await pairShare(qa, 'member', EMAIL.member, EMAIL.chair, { open: true })
        if (!s?.pendingChange) {
          s = await ensureMemberShare(qa, 'Level1')
          qa.expectStatus(await (await qa.api('chair')).patch(`/api/calendar/shares/${s.id}`, { level: 'Level2' }), 201)
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
      },
    },
    // ------------------------------------------------------------------ privacy at Mốc 2
    {
      id: 'AC13',
      role: 'chair',
      title: 'Mốc 2 (Thường và Nội bộ): thấy thêm giờ bận họp Nội bộ, họp Mật vẫn không bao giờ hiện, kể cả khi tìm giờ',
      steps: [`chutoa@ mở tab Lịch được chia sẻ tuần ${dm(MON)}`, `POST /api/calendar/slots ${TUE}, 60 phút`],
      expected: `T3 ${dm(TUE)}: bận 09:00–10:00 và 13:30–14:30; không có 15:30–16:30 (Mật) hay 10:30–11:30 (Nháp); tìm giờ: không có 13:30, vẫn có 15:30 (BR-414, BR-417)`,
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
    // ------------------------------------------------------------------ UC-84a
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
    // ------------------------------------------------------------------ UC-84c decline
    {
      id: 'AC15',
      role: 'admin',
      title: 'Chủ lịch từ chối yêu cầu xem lịch: hỏi xác nhận, dòng chuyển "Đã từ chối" và mờ đi',
      steps: ['quantri@ → Chia sẻ lịch', 'Dòng Nguyễn Hoàng Thanh Hương "Chờ bạn trả lời" → Từ chối → Từ chối'],
      expected: 'Hộp xác nhận "Từ chối cho xem lịch?"; toast MSG437 "Đã từ chối yêu cầu chia sẻ."; dòng "Đã từ chối", mờ, không còn nút; API Declined, actions []',
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
        await line.getByRole('button', { name: 'Từ chối' }).click()
        const confirm = qa.page.getByRole('dialog', { name: 'Từ chối cho xem lịch?' })
        await qa.expect(confirm).toBeVisible()
        await settled(qa)
        await qa.shot('xac-nhan-tu-choi')
        await confirm.getByRole('button', { name: 'Từ chối' }).click()
        await qa.expect(toast(qa, 'Đã từ chối yêu cầu chia sẻ.')).toBeVisible()
        await qa.expect(line.getByText('Đã từ chối')).toBeVisible()
        await qa.expect(line).toHaveClass(/share-row-closed/)
        await qa.expect(line.getByRole('button')).toHaveCount(0)
        await qa.shot('da-tu-choi')
        const after = (await shares(qa, 'admin')).find((x) => x.id === s.id)
        qa.expect(after).toMatchObject({ status: 'Declined', actions: [] })
      },
    },
    // ------------------------------------------------------------------ UC-84e withdraw
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
    // ------------------------------------------------------------------ UC-84c narrowing, UC-84e AF-1 on the API
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
    // ------------------------------------------------------------------ UC-84e AF-1 from the viewer, in the UI
    {
      id: 'AC18',
      role: 'chair',
      title: 'Bên xem "Thôi xem lịch": chia sẻ ngừng, không còn giờ bận nào của người đó trong tuần và khi tìm giờ',
      steps: ['chutoa@ → Chia sẻ lịch → dòng Lê An → "…" → Thôi xem lịch → Thôi xem', 'Mở tab Lịch được chia sẻ', 'Tìm giờ với Lê An (API)'],
      expected:
        'Hộp xác nhận "Thôi xem lịch của Lê An?"; toast MSG451 "Đã ngừng chia sẻ lịch."; dòng "Đã ngừng", mờ; tab tuần không còn Lê An; tìm giờ: Lê An NotShared; đổi chia sẻ đã ngừng → 409 MSG450',
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
        await qa.expect(qa.page.getByRole('group', { name: 'Người chia sẻ' }).getByText(name)).toHaveCount(0)
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
    // ------------------------------------------------------------------ BR-424 stop
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
    // ------------------------------------------------------------------ BR-430 on the API
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
    // ------------------------------------------------------------------ BR-430 in the UI
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
    // ------------------------------------------------------------------ the working calendar (UC-17 AF-4)
    {
      id: 'AC23',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Lịch làm việc: tuần theo giờ, mặc định ẩn nhóm Chờ duyệt; bật nhóm thì thấy cuộc họp, họp Mật có nhãn "Mật"; đổi sang Danh sách giữ trong URL',
      steps: [
        `thuky@ mở Lịch làm việc tuần ${dm(MON)}`,
        'Bật nhóm Chờ duyệt (?status=waiting)',
        'Bấm Danh sách',
      ],
      expected:
        'Thanh công cụ: Hôm nay, Tuần trước/sau, Lịch/Danh sách, Kiểu lịch, Tạo cuộc họp; mặc định không có 3 cuộc họp chờ duyệt (nhóm mặc định Đã duyệt, Đã công bố, Đang diễn ra); bật Chờ duyệt thì có, khối họp Mật có "Mật"; Danh sách: URL ?mode=list, nhóm theo ngày; không cuộn ngang',
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
      expected: 'Không có "[QA] FE-16 họp …" (Thành viên chỉ thấy từ khi Đã công bố, BR-29 / UC-17); không có nút "Tạo cuộc họp"',
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
    // ------------------------------------------------------------------ cleanup
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

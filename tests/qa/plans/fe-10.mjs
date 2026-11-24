// FE-10 — Gỡ băng, nhận diện người nói và tóm tắt (QA subtask SCRUM-197): UC-54a view a transcript, UC-54b export it,
// UC-54c transcribe (automatic after an upload, rerun), UC-55a edit a sentence, UC-55b name a voice and change the
// speaker of one sentence ("chỉ câu này", MSG407), UC-55c exclude / restore a per-mic sentence (MSG351–354), UC-56a–f
// the person's own Voice ID (consent, samples, withdrawal), UC-57a–d the Secretary collecting samples on someone's behalf
// (ADR-0060, MSG473–477), and of UC-58–UC-60 only what FE-10 feeds: the draft card's count of unnamed voices (read-only —
// this plan never asks for an AI draft; the draft itself is FE-12).
//
// Criteria come from docs/01-requirements/05-use-case-specifications.md, 06-business-rules.md (BR-108, BR-111–BR-113, BR-109, UC-53, UC-45a, UC-51a, BR-114, BR-115, UC-54b, UC-54c, BR-116, BR-124, BR-118, BR-119, BR-117, BR-120, BR-123, NFR,
// BR-117, UC-55b, BR-118, BR-119, BR-124, UC-57a, UC-57b, BR-009–BR-011), docs/03-api/12-ban-ghi-cuoc-hop.md, 13-nhan-dien-giong-noi.md, 17-du-thao-bien-ban.md,
// ADR-0026/0027/0060, the message catalogue, and the Jira comments of SCRUM-31, SCRUM-35 and SCRUM-152 (one Voice ID
// consent, notice 1.3, covers both sample sets; the speaker of one sentence moves to the chosen person's voice; the
// machine names a voice itself at ≥ 0.75; guests have no Voice ID, PO 14/11/2026).
//
// Role × action (who may do what, as the criteria check it):
//
//   | Role                         | Action                                         | Expected                               |
//   |------------------------------|------------------------------------------------|----------------------------------------|
//   | Thư ký (thuky@)              | xem / sửa câu / gán tên / đổi người nói 1 câu  | 200; MSG302, MSG316, MSG407            |
//   | Thư ký                       | chuyển chữ lại, tải bản gỡ băng                | 202 MSG300 (409 MSG301 khi đang chạy); |
//   |                              |                                                | .txt (409 MSG357 khi chưa xong)        |
//   | Thư ký                       | loại / đưa lại câu theo micro                  | 200 MSG351 / MSG352                    |
//   | Thư ký có VoiceCollector     | thu, nghe, xoá mẫu thu hộ ở cuộc họp của mình  | 201 MSG473, 200, 200 MSG474            |
//   | Chủ toạ (chutoa@)            | xem, tải .txt, loại / đưa lại câu              | 200; MSG351 / MSG352                   |
//   | Chủ toạ                      | sửa câu, gán tên, đổi người nói                | 403 MSG295                             |
//   | Chủ toạ (không VoiceCollector)| xem danh sách thu hộ                          | 403                                    |
//   | Thành viên (thanhvien@)      | xem bản gỡ băng (không gợi ý, không nút sửa)   | 200, suggested* = null, people = []    |
//   | Thành viên                   | tải .txt / loại câu / sửa / gán                | 403 MSG355 / MSG353 / MSG295 / MSG295  |
//   | Quản trị (quantri@, ngoài DS)| bản ghi, bản gỡ băng, câu theo micro, thu hộ   | 404                                    |
//   | Chưa đăng nhập               | bản gỡ băng                                    | 401                                    |
//   | Ai cũng vậy, cuộc họp Mật    | tải .txt; thu hộ                               | 403 MSG356; 404                        |
//   | Người dùng nội bộ (chính chủ)| đồng ý, ghi / nghe / xoá mẫu, rút đồng ý       | MSG305, MSG307, 200, MSG311, MSG306    |
//   | Người khác                   | nghe / xoá mẫu của người khác                  | 404                                    |
//
// Safe on a shared local dev database. Two accounts of the plan's own, qa-fe10-a@ / qa-fe10-b@trustcabinet.local
// ("[QA] FE-10 Người nói A/B", Thành viên), are the only ones whose Voice ID is ever touched; every criterion that
// enrols them withdraws again, and the last step of each such criterion checks the samples are gone. Four meetings are
// built once through the API and reused by title, all organised by thuky@ (Secretary) with chutoa@ as Chair and
// approver, 14 weeks ahead:
//   "[QA] FE-10 Họp trực tiếp gỡ băng"   InPerson, Ended — two uploaded recordings (main, Voice ID) and a guest
//   "[QA] FE-10 Họp thu giọng mẫu"       InPerson, Published — where the Secretary collects samples, and a guest
//   "[QA] FE-10 Họp trực tuyến theo micro" Online, Ended — three per-mic sentences
//   "[QA] FE-10 Họp Mật gỡ băng"         InPerson, Secret, Ended — one recording
// Test audio is synthetic: macOS `say -v Linh` (Vietnamese) and the same voice pitched down as a second speaker,
// converted to 16 kHz WAV with ffmpeg, written to QA_AUDIO_DIR (default: <tmp>/trustcabinet-qa-fe10). No recording of
// a real person is used. The speech worker runs on CPU, so a transcript takes tens of seconds.

import { execFileSync } from 'node:child_process'
import { randomUUID } from 'node:crypto'
import { existsSync, mkdirSync, readFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

const PASSWORD = process.env.QA_PASSWORD ?? 'TrustCabinet@2026'
const SEED = {
  admin: 'quantri@trustcabinet.local',
  chair: 'chutoa@trustcabinet.local',
  secretary: 'thuky@trustcabinet.local',
  member: 'thanhvien@trustcabinet.local',
}
const QA_USERS = {
  a: { email: 'qa-fe10-a@trustcabinet.local', label: 'FE-10 Người nói A' },
  b: { email: 'qa-fe10-b@trustcabinet.local', label: 'FE-10 Người nói B' },
}
const GUEST = { invitedName: '[QA] FE-10 Khách mời', email: 'qa-fe10-khach@trustcabinet.local' }
const TITLES = {
  A: 'FE-10 Họp trực tiếp gỡ băng',
  B: 'FE-10 Họp thu giọng mẫu',
  C: 'FE-10 Họp trực tuyến theo micro',
  D: 'FE-10 Họp Mật gỡ băng',
}
const FILES = { main: 'fe10-hop-chinh.wav', voice: 'fe10-voice-id.wav', secret: 'fe10-hop-mat.wav' }
const MSG = {
  MSG290: 'Đã lưu bản ghi.',
  MSG300: 'Đã đưa bản ghi vào hàng chờ chuyển thành chữ.',
  MSG302: 'Đã lưu đoạn gỡ băng.',
  MSG305: 'Đã bật nhận diện giọng nói cho tài khoản của bạn.',
  MSG306: 'Đã tắt nhận diện giọng nói và xoá toàn bộ giọng mẫu.',
  MSG307: 'Đã lưu giọng mẫu.',
  MSG311: 'Đã xoá giọng mẫu.',
  MSG316: 'Đã gán người nói.',
  MSG351: 'Đã loại câu khỏi bản ghi.',
  MSG352: 'Đã đưa câu trở lại bản ghi.',
  MSG407: 'Đã đổi người nói của câu này.',
  MSG473: 'Đã lưu giọng mẫu thu hộ.',
  MSG474: 'Đã xoá giọng mẫu thu hộ.',
}

// ---------------------------------------------------------------------------------------------------------------------
// Audio: generated once per machine with `say` and ffmpeg (never a real person's voice).

const AUDIO_DIR = process.env.QA_AUDIO_DIR ?? join(tmpdir(), 'trustcabinet-qa-fe10')
const LINES = {
  s1a: 'Kính thưa các đồng chí, hôm nay chúng ta họp để thông qua kế hoạch ngân sách năm hai nghìn không trăm hai mươi bảy của cơ quan.',
  s2a: 'Tôi đề nghị bổ sung thêm phần kinh phí cho đào tạo cán bộ và nâng cấp hệ thống máy tính.',
  s1b: 'Cảm ơn ý kiến của đồng chí. Chúng ta sẽ biểu quyết vào cuối buổi họp.',
  enrol:
    'Hôm nay trời đẹp, tôi đọc đoạn văn mẫu này để hệ thống nhận ra giọng nói của tôi trong các cuộc họp. Tôi nói chậm rãi, rõ ràng và tự nhiên như khi phát biểu tại phòng họp của cơ quan.',
  short: 'Xin chào các bạn.',
}
function ff(args) {
  execFileSync('ffmpeg', ['-y', '-loglevel', 'error', ...args])
}
function buildAudio() {
  mkdirSync(AUDIO_DIR, { recursive: true })
  const p = (n) => join(AUDIO_DIR, n)
  for (const [name, text] of Object.entries(LINES)) {
    if (existsSync(p(`${name}.wav`))) continue
    execFileSync('say', ['-v', 'Linh', '-o', p(`${name}.aiff`), text])
    // The second speaker: the same voice pitched down, tempo kept.
    const filter = name === 's2a' ? ['-af', 'asetrate=22050*0.74,aresample=22050,atempo=1.35'] : []
    ff(['-i', p(`${name}.aiff`), ...filter, '-ar', '16000', '-ac', '1', '-c:a', 'pcm_s16le', p(`${name}.wav`)])
  }
  // The second speaker's own enrolment reading, pitched like the second speaker.
  if (!existsSync(p('enrol2.wav')))
    ff(['-i', p('enrol.aiff'), '-af', 'asetrate=22050*0.74,aresample=22050,atempo=1.35', '-ar', '16000', '-ac', '1', '-c:a', 'pcm_s16le', p('enrol2.wav')])
  if (!existsSync(p('sil.wav'))) ff(['-f', 'lavfi', '-i', 'anullsrc=r=16000:cl=mono', '-t', '1.2', '-c:a', 'pcm_s16le', p('sil.wav')])
  if (!existsSync(p('meeting.wav')))
    ff([...['s1a', 'sil', 's2a', 'sil', 's1b'].flatMap((n) => ['-i', p(`${n}.wav`)]),
      '-filter_complex', '[0][1][2][3][4]concat=n=5:v=0:a=1', '-ar', '16000', '-ac', '1', '-c:a', 'pcm_s16le', p('meeting.wav')])
  // About a minute: long enough for the "being transcribed" state to be seen on a CPU worker.
  if (!existsSync(p('long.wav')))
    ff([...[0, 1, 2].flatMap(() => ['-i', p('meeting.wav')]),
      '-filter_complex', '[0][1][2]concat=n=3:v=0:a=1', '-ar', '16000', '-ac', '1', '-c:a', 'pcm_s16le', p('long.wav')])
}
function audio(qa, name) {
  try {
    buildAudio()
  } catch (error) {
    qa.inconclusive(`Không tạo được âm thanh thử (cần macOS "say -v Linh" và ffmpeg): ${error.message}`)
  }
  return readFileSync(join(AUDIO_DIR, `${name}.wav`))
}

// ---------------------------------------------------------------------------------------------------------------------
// HTTP: the plan's own client, since it sends multipart forms, raw audio and If-Match, which the runner's does not.

const TOKENS = {}
const emailOf = (who) => SEED[who] ?? QA_USERS[who]?.email ?? who
async function login(base, who) {
  const res = await fetch(`${base}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email: emailOf(who), password: PASSWORD }),
  })
  if (!res.ok) throw new Error(`sign-in as ${who}: HTTP ${res.status}`)
  TOKENS[who] = (await res.json()).accessToken
  return TOKENS[who]
}
/** `{ status, body, text, headers }`. `json`, `form` (FormData) or `raw` ({ data, type }) is the body. */
async function http(qa, who, method, path, { json, form, raw, ifMatch, retried = false } = {}) {
  const headers = {}
  if (who !== 'anon') headers.Authorization = `Bearer ${TOKENS[who] ?? (await login(qa.base, who))}`
  let body
  if (json !== undefined) {
    headers['Content-Type'] = 'application/json'
    body = JSON.stringify(json)
  } else if (form) body = form
  else if (raw) {
    headers['Content-Type'] = raw.type
    body = raw.data
  }
  if (ifMatch) headers['If-Match'] = ifMatch
  const res = await fetch(`${qa.base}${path}`, { method, headers, body })
  if (res.status === 401 && who !== 'anon' && !retried) {
    await login(qa.base, who)
    return http(qa, who, method, path, { json, form, raw, ifMatch, retried: true })
  }
  const text = await res.text()
  let parsed
  try {
    parsed = text ? JSON.parse(text) : undefined
  } catch {
    parsed = undefined
  }
  return { status: res.status, body: parsed ?? text, text, headers: res.headers }
}
const as = (qa, who) => ({
  get: (p) => http(qa, who, 'GET', p),
  post: (p, json, o = {}) => http(qa, who, 'POST', p, { ...o, json: json ?? {} }),
  put: (p, json, o = {}) => http(qa, who, 'PUT', p, { ...o, json: json ?? {} }),
  delete: (p, o = {}) => http(qa, who, 'DELETE', p, o),
  form: (p, form) => http(qa, who, 'POST', p, { form }),
})
const wavForm = (buffer, name, extra = {}) => {
  const form = new FormData()
  form.append('file', new Blob([buffer], { type: 'audio/wav' }), name)
  for (const [k, v] of Object.entries(extra)) form.append(k, v)
  return form
}

/** Runs every API check, then fails once with all the wrong ones. */
function checker(qa) {
  const wrong = []
  return {
    check(label, res, status, code) {
      const ok = res.status === status && (code === undefined || res.body?.code === code)
      if (!ok) wrong.push(`${label}: want ${status}${code ? ` ${code}` : ''}, got ${res.status} ${String(res.text ?? '').slice(0, 220)}`)
      return ok
    },
    fact(label, ok, detail = '') {
      if (!ok) wrong.push(`${label}${detail ? `: ${detail}` : ''}`)
      return ok
    },
    done() {
      qa.expect(wrong, wrong.join('\n')).toEqual([])
    },
  }
}

// ---------------------------------------------------------------------------------------------------------------------
// Accounts.

const USERS = {}
async function users(qa) {
  for (const who of ['admin', 'chair', 'secretary', 'member']) {
    if (USERS[who]) continue
    const me = (await as(qa, who).get('/api/auth/me')).body
    USERS[who] = { id: me.id, name: me.fullName, email: me.email, capabilities: me.capabilities ?? [] }
  }
  for (const key of ['a', 'b']) USERS[key] ??= await ensureUser(qa, key)
  return USERS
}
/** The plan's own account `key`, active, Thành viên, created the first time. */
async function ensureUser(qa, key) {
  const { email, label } = QA_USERS[key]
  const fullName = qa.name(label)
  const admin = as(qa, 'admin')
  const list = await admin.get('/api/users')
  qa.expectStatus(list, 200)
  let user = list.body.find((u) => u.email === email)
  if (!user) {
    const roles = await admin.get('/api/roles')
    const member = roles.body.find((r) => r.name === 'Thành viên')
    const res = await admin.post('/api/users', { fullName, email, password: PASSWORD, roleId: member.id })
    qa.expectStatus(res, 201)
    user = res.body.user
  }
  if (!user.isActive) qa.expectStatus(await admin.put(`/api/users/${user.id}/status`, { isActive: true }), 200)
  return { id: user.id, name: user.fullName, email }
}

// ---------------------------------------------------------------------------------------------------------------------
// Meetings, built once and reused by title.

const iso = (ms) => new Date(ms).toISOString().replace(/\.\d{3}Z$/, 'Z')
const DAY = 86_400_000
const HOURS = { A: 8, B: 10, C: 14, D: 16 }
/** A one-hour slot 14 weeks from today, at a different hour (Vietnam time) for each meeting. */
function slot(key) {
  const day = new Date(Date.now() + 98 * DAY)
  day.setUTCHours(HOURS[key] - 7, 15, 0, 0)
  return { plannedStartTime: iso(day.getTime()), plannedEndTime: iso(day.getTime() + 3_600_000) }
}
const ORDER = ['Draft', 'WaitingApproval', 'Approved', 'Published', 'InProgress', 'Ended']
const STEP = {
  WaitingApproval: ['secretary', 'submit'],
  Approved: ['chair', 'approve'],
  Published: ['secretary', 'publish'],
  InProgress: ['chair', 'start'],
  Ended: ['chair', 'end'],
}
async function transition(qa, id, to) {
  const u = await users(qa)
  const [who, action] = STEP[to]
  const body = {
    submit: { action: 'SubmitForApproval', assignedApproverId: u.chair.id },
    approve: { action: 'Approve' },
    publish: { action: 'Publish' },
    start: { action: 'Start' },
    end: { action: 'End' },
  }[action]
  const res = await as(qa, who).post(`/api/meetings/${id}/transitions`, body)
  qa.expectStatus(res, 200)
}
const getMeeting = async (qa, id, who = 'secretary') => (await as(qa, who).get(`/api/meetings/${id}`)).body

/** Drives meeting `id` forward to `status`, calling `whileLive` once it is InProgress (before it ends). */
async function driveTo(qa, id, status, whileLive) {
  let m = await getMeeting(qa, id)
  while (ORDER.indexOf(m.status) < ORDER.indexOf(status)) {
    const next = ORDER[ORDER.indexOf(m.status) + 1]
    if (next === 'Ended' && whileLive) await whileLive(m)
    await transition(qa, id, next)
    m = await getMeeting(qa, id)
  }
  return m
}

const SPEC = {
  A: { mode: 'InPerson', sensitivity: 'Normal', status: 'Ended', guest: true, extra: ['a', 'b'] },
  B: { mode: 'InPerson', sensitivity: 'Normal', status: 'Published', guest: true, extra: ['a', 'b'] },
  C: { mode: 'Online', sensitivity: 'Normal', status: 'Ended', guest: false, extra: ['a'] },
  D: { mode: 'InPerson', sensitivity: 'Secret', status: 'Ended', guest: false, extra: [] },
}
const MEETINGS = {}
/** Meeting `key` (A–D) in its target state with its recordings / sentences, built the first time. */
async function meeting(qa, key) {
  if (MEETINGS[key]) return MEETINGS[key]
  const u = await users(qa)
  const spec = SPEC[key]
  const title = qa.name(TITLES[key])
  const sec = as(qa, 'secretary')
  const found = await sec.get(`/api/meetings?q=${encodeURIComponent(title)}&pageSize=50`)
  let m = (found.body.items ?? []).find((x) => x.title === title && x.status !== 'Cancelled')
  if (!m) {
    const created = await sec.post('/api/meetings', {
      title,
      ...slot(key),
      mode: spec.mode,
      location: spec.mode === 'Online' ? '' : 'Phòng họp QA FE-10',
      onlineLink: null,
      sensitivity: spec.sensitivity,
      description: 'Cuộc họp do bộ kiểm thử QA FE-10 tạo (âm thanh tổng hợp, không phải giọng người thật).',
      participants: [
        { userId: u.chair.id, role: 'Chair' },
        { userId: u.secretary.id, role: 'Secretary' },
        { userId: u.member.id, role: 'Member' },
        ...spec.extra.map((k) => ({ userId: u[k].id, role: 'Member' })),
      ],
    })
    qa.expectStatus(created, 201)
    m = created.body.meeting
    if (spec.guest) {
      const roster = await sec.get(`/api/meetings/${m.id}/participants?pageSize=100`)
      const res = await sec.post(`/api/meetings/${m.id}/guest-invitations`, GUEST, {
        ifMatch: `"participants-${roster.body.revision}"`,
      })
      qa.expectStatus(res, 201)
    }
  }
  const whileLive = {
    A: async (live) => {
      await ensureRecording(qa, live.id, FILES.main, 'meeting')
      await ensureRecording(qa, live.id, FILES.voice, 'meeting')
    },
    C: async (live) => {
      const have = await as(qa, 'chair').get(`/api/meetings/${live.id}/speech-clips`)
      if ((have.body.items ?? []).length >= 3) return
      await sendClip(qa, 'member', live.id, 's1a')
      await sendClip(qa, 'a', live.id, 's2a')
      await sendClip(qa, 'member', live.id, 's1b')
    },
    D: async (live) => ensureRecording(qa, live.id, FILES.secret, 's1b'),
  }[key]
  m = await driveTo(qa, m.id, spec.status, whileLive)
  // A meeting that stopped half-way on an earlier run still gets what it needs (an Ended meeting takes uploads).
  if (key === 'A') {
    m.main = await ensureRecording(qa, m.id, FILES.main, 'meeting')
    m.voice = await ensureRecording(qa, m.id, FILES.voice, 'meeting')
  }
  if (key === 'D') m.rec = await ensureRecording(qa, m.id, FILES.secret, 's1b')
  if (spec.guest) {
    const roster = await sec.get(`/api/meetings/${m.id}/participants?pageSize=100`)
    m.guest = roster.body.items.find((i) => i.kind === 'Guest')
  }
  MEETINGS[key] = m
  return m
}

/** The kept recording named `fileName` of the meeting, uploaded from `audioName` when missing; waits for its text. */
async function ensureRecording(qa, meetingId, fileName, audioName) {
  const sec = as(qa, 'secretary')
  const list = await sec.get(`/api/meetings/${meetingId}/recordings`)
  qa.expectStatus(list, 200)
  let rec = list.body.items.find((r) => r.fileName === fileName && r.status === 'Available')
  if (!rec) {
    const res = await sec.form(
      `/api/meetings/${meetingId}/recordings`,
      wavForm(audio(qa, audioName), fileName, { consentConfirmed: 'true', consentPurpose: 'Lập biên bản cuộc họp (QA FE-10)' }),
    )
    if (res.status === 503) qa.inconclusive(`Kho bản ghi không nhận tệp (503 ${res.body?.code}).`)
    qa.expectStatus(res, 201)
    rec = { id: res.body.recordingId, fileName }
  }
  await waitTranscript(qa, rec.id)
  return rec
}

/** Polls the transcript until it is Completed (or Failed: inconclusive, the speech worker may be off or stale). */
async function waitTranscript(qa, recordingId, timeoutMs = 300_000) {
  const until = Date.now() + timeoutMs
  for (;;) {
    const t = await as(qa, 'secretary').get(`/api/recordings/${recordingId}/transcript`)
    qa.expectStatus(t, 200)
    if (t.body.status === 'Completed') return t.body
    if (t.body.status === 'Failed')
      qa.inconclusive(`Bản gỡ băng lỗi: "${t.body.errorMessage}". Máy chuyển chữ (127.0.0.1:8090) có thể tắt hoặc là image cũ.`)
    if (t.body.status === 'None') await as(qa, 'secretary').post(`/api/recordings/${recordingId}/transcribe`)
    if (Date.now() > until) qa.inconclusive(`Bản gỡ băng chưa xong sau ${timeoutMs / 1000} giây (đang ${t.body.status}).`)
    await new Promise((r) => setTimeout(r, 3000))
  }
}

/** One per-mic sentence of `who` (their own mic), as the call room sends it. */
async function sendClip(qa, who, meetingId, audioName) {
  const data = audio(qa, audioName)
  const durationMs = Math.round(((data.length - 44) / 32000) * 1000)
  for (let attempt = 0; attempt < 5; attempt++) {
    const res = await http(
      qa,
      who,
      'PUT',
      `/api/meetings/${meetingId}/speech-clips/${randomUUID()}?startedAt=${encodeURIComponent(new Date().toISOString())}&durationMs=${durationMs}`,
      { raw: { data, type: 'audio/wav' } },
    )
    if (res.status === 200) return res.body.clip
    if (res.status !== 503) qa.expectStatus(res, 200)
    await new Promise((r) => setTimeout(r, 5000))
  }
  qa.inconclusive('Máy chuyển chữ không nhận câu theo micro (503 MSG312 năm lần).')
}

const transcript = async (qa, who, recordingId) => {
  const res = await as(qa, who).get(`/api/recordings/${recordingId}/transcript`)
  qa.expectStatus(res, 200)
  return res.body
}
const clips = async (qa, who, meetingId) => {
  const res = await as(qa, who).get(`/api/meetings/${meetingId}/speech-clips`)
  qa.expectStatus(res, 200)
  return res.body
}
/** The segment whose text matches `re`, and the voice it belongs to. */
function segmentOf(t, re) {
  const segment = t.segments.find((s) => re.test(s.text))
  return segment ? { segment, speaker: t.speakers.find((s) => s.label === segment.speakerLabel) } : {}
}
const speakerName = (label) => `Người nói ${Number(/SPEAKER_(\d+)/.exec(label)?.[1] ?? 0) + 1}`
const formatDuration = (ms) => {
  const total = Math.round(ms / 1000)
  return `${String(Math.floor(total / 60)).padStart(2, '0')}:${String(total % 60).padStart(2, '0')}`
}

/** Clears every name on the recording's voices (so a criterion starts from "Người nói N"). */
async function clearNames(qa, recordingId) {
  const t = await transcript(qa, 'secretary', recordingId)
  for (const s of t.speakers.filter((x) => x.assignedUserId || x.assignedGuestId))
    await as(qa, 'secretary').put(`/api/recordings/${recordingId}/speakers/${s.label}`, { userId: null, guestId: null })
}

// ---------------------------------------------------------------------------------------------------------------------
// Voice ID of the plan's own accounts.

async function voice(qa, who) {
  const res = await as(qa, who).get('/api/me/voice')
  qa.expectStatus(res, 200)
  return res.body
}
/** `who` (a / b) has consented to the current notice and holds exactly `samples` self-recorded samples. */
async function enrolled(qa, who, samples = 0, reading = 'enrol') {
  let v = await voice(qa, who)
  if (!v.consent || v.consent.noticeVersion !== v.noticeVersion) {
    qa.expectStatus(await as(qa, who).post('/api/me/voice/consent', { noticeVersion: v.noticeVersion }), 200)
    v = await voice(qa, who)
  }
  for (const s of v.samples.slice(samples)) await as(qa, who).delete(`/api/me/voice/samples/${s.id}`)
  for (let i = v.samples.length; i < samples; i++) {
    const res = await as(qa, who).form('/api/me/voice/samples', wavForm(audio(qa, reading), 'giong-mau.wav'))
    if (res.status === 503) qa.inconclusive('Máy nhận dạng giọng nói chưa sẵn sàng (503 MSG312).')
    qa.expectStatus(res, 201)
  }
  return voice(qa, who)
}
/** `who` has no Voice ID: consent withdrawn, every sample of both sets gone. */
async function withdrawn(qa, who) {
  const v = await voice(qa, who)
  if (v.consent) qa.expectStatus(await as(qa, who).delete('/api/me/voice/consent'), 200)
  const after = await voice(qa, who)
  qa.expect(after.consent, `${who} still has a consent`).toBeNull()
  qa.expect(after.samples.length + after.proxy.samples.length, `${who} still has samples`).toBe(0)
  return after
}
/** Deletes every sample collected for `userId` at meeting B (as its Secretary). */
async function clearCollected(qa, meetingId, userId) {
  const res = await as(qa, 'secretary').get(`/api/meetings/${meetingId}/voice-collection`)
  const person = res.body?.people?.find((p) => p.userId === userId)
  for (const s of person?.samples ?? [])
    await as(qa, 'secretary').delete(`/api/meetings/${meetingId}/voice-collection/samples/${s.id}`)
}
async function collect(qa, meetingId, userId) {
  const res = await as(qa, 'secretary').form(
    `/api/meetings/${meetingId}/voice-collection/${userId}/samples`,
    wavForm(audio(qa, 'enrol'), 'thu-ho.wav'),
  )
  if (res.status === 503) qa.inconclusive('Máy nhận dạng giọng nói chưa sẵn sàng (503 MSG312).')
  return res
}

// ---------------------------------------------------------------------------------------------------------------------
// Screen helpers.

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
const toast = (qa, text) => qa.page.locator('.toast-stack').getByText(text)
const esc = (s) => s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
async function press(qa, locator) {
  if (qa.device === 'phone') await locator.tap()
  else await locator.click()
}
/** Opens the meeting's Bản ghi tab. */
async function openRecordings(qa, m) {
  await qa.goto(`/mgmt/meetings/${m.id}?tab=recordings`)
  await qa.expect(qa.page.getByRole('heading', { level: 1, name: m.title })).toBeVisible({ timeout: 15_000 })
}
const recordingsRegion = (qa) => qa.page.getByRole('region', { name: 'Bản ghi', exact: true })
const recordingRow = (qa, fileName) => recordingsRegion(qa).locator('li.recording-row').filter({ hasText: fileName })
const lines = (row) => row.getByRole('list', { name: 'Lời thoại theo thời điểm nói' })
const collectionPanel = (qa) => qa.page.getByRole('region', { name: 'Giọng mẫu cho mic phòng' })
const personRow = (qa, name) => collectionPanel(qa).locator('li.voice-person').filter({ hasText: name })
async function pick(qa, trigger, optionName) {
  await press(qa, trigger)
  const list = qa.page.getByRole('listbox').last()
  await qa.expect(list).toBeVisible()
  await press(qa, list.getByRole('option', { name: new RegExp(`^${esc(optionName)}`) }).first())
}
/** Opens My voice and waits until it has loaded. */
async function openMyVoice(qa, query = '') {
  await qa.goto(`/mgmt/my-voice${query}`)
  await qa.expect(qa.page.getByRole('heading', { level: 1, name: 'Giọng nói của tôi' })).toBeVisible()
  await qa.expect(qa.page.locator('.voice-skeleton')).toHaveCount(0)
}

// ---------------------------------------------------------------------------------------------------------------------

export default {
  ticket: 'SCRUM-197',
  title: 'FE-10 Gỡ băng, nhận diện người nói (Voice ID, thu hộ) và phần tóm tắt FE-10 cung cấp',
  devices: ['desktop'],
  criteria: [
    // ------------------------------------------------------------------------------------------- UC-54a View
    {
      id: 'AC1',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Thư ký xem bản gỡ băng đã xong: mốc giờ, người nói, nhãn "Máy nhận dạng", thẻ người nói, công cụ sửa',
      steps: [
        'Đăng nhập thuky@, mở tab "Bản ghi" của "[QA] FE-10 Họp trực tiếp gỡ băng" (bản ghi tải lên, đã chuyển chữ)',
        'Xem dòng bản ghi "fe10-hop-chinh.wav" và bản gỡ băng dưới nó',
      ],
      expected:
        'Dòng bản ghi có chip "Có bản gỡ băng"; bản gỡ băng có nhãn "Máy nhận dạng", mỗi câu có mốc giờ và tên người nói (Người nói N hoặc tên đã gán), cột "Người nói" có ít nhất 2 giọng; Thư ký có ô chọn tên và nút sửa; số câu trên màn bằng số câu API trả; không cuộn ngang, không lộ mã MSG',
      async run(qa) {
        const m = await meeting(qa, 'A')
        await clearNames(qa, m.main.id)
        const t = await transcript(qa, 'secretary', m.main.id)
        await openRecordings(qa, m)
        const row = recordingRow(qa, FILES.main)
        await qa.expect(row.getByText('Có bản gỡ băng')).toBeVisible()
        await qa.expect(row.getByText('Máy nhận dạng')).toBeVisible()
        await qa.expect(lines(row)).toBeVisible()
        await qa.expect(lines(row).locator('.tl-say')).toHaveCount(t.segments.length)
        qa.expect(t.speakers.length, 'the worker heard one voice only').toBeGreaterThanOrEqual(2)
        const speakers = row.locator('aside.transcript-speakers')
        await qa.expect(speakers.getByRole('heading', { name: /Người nói/ })).toBeVisible()
        await qa.expect(speakers.getByRole('button', { name: /^Tên của Người nói/ })).toHaveCount(t.speakers.length)
        await qa.expect(lines(row).getByText(/ngân sách/i).first()).toBeVisible()
        await qa.expect(lines(row).getByRole('button', { name: /^Người nói câu lúc 00:00/ })).toBeVisible()
        qa.expect(t.segments.every((s) => s.endMs > s.startMs), 'a segment without a time span').toBe(true)
        await row.scrollIntoViewIfNeeded()
        await settled(qa)
        await qa.shot('ban-go-bang-thu-ky')
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
      },
    },

    // ------------------------------------------------------------------------------------------- UC-54c Transcribe
    {
      id: 'AC2',
      role: 'secretary',
      title: 'Tải tệp lên thì tự chuyển chữ: trạng thái "Đang chuyển chữ" hiện trong lúc chạy, chạy lại bị chặn (MSG301), tải .txt bị chặn (MSG357), xong thì hiện chữ',
      steps: [
        'Thư ký mở tab "Bản ghi" của cuộc họp đã kết thúc, bấm "Tải tệp lên", chọn tệp WAV khoảng 1 phút, đánh dấu đã thông báo, bấm "Tải lên"',
        'Trong lúc đang chuyển chữ: API POST …/transcribe và GET …/transcript.txt',
        'Chờ chuyển chữ xong',
      ],
      expected:
        'Hộp xác nhận thông báo ghi âm trước khi lưu, nút "Tải lên" khoá tới khi đánh dấu; dòng mới có chip "Đang chuyển chữ" và dòng trạng thái "Đang chờ/Đang chuyển thành chữ…"; API: 409 MSG301 và 409 MSG357; khi xong chip "Có bản gỡ băng" và lời thoại hiện ra (UC-54c AF-1, UC-54a AF-1, UC-54b EX-3)',
      async run(qa) {
        const m = await meeting(qa, 'A')
        const c = checker(qa)
        const fileName = `fe10-chuyen-chu-${Date.now().toString(36)}.wav`
        let createdId = null
        try {
          await openRecordings(qa, m)
          const region = recordingsRegion(qa)
          await region.locator('input[type=file]').setInputFiles({ name: fileName, mimeType: 'audio/wav', buffer: audio(qa, 'long') })
          const dialog = qa.page.getByRole('dialog', { name: 'Lưu tệp ghi âm' })
          await qa.expect(dialog).toBeVisible()
          await qa.expect(dialog.getByRole('button', { name: 'Tải lên' })).toBeDisabled()
          await dialog.getByRole('checkbox').check()
          await settled(qa)
          await qa.shot('xac-nhan-thong-bao-ghi-am')
          await dialog.getByRole('button', { name: 'Tải lên' }).click()
          const row = recordingRow(qa, fileName)
          await qa.expect(row).toBeVisible({ timeout: 30_000 })
          await qa.expect(row.getByText('Đang chuyển chữ')).toBeVisible({ timeout: 15_000 })
          await qa.expect(row.getByRole('status').filter({ hasText: /Đang (chờ )?chuyển thành chữ/ })).toBeVisible()
          await row.scrollIntoViewIfNeeded()
          await settled(qa)
          await qa.shot('dang-chuyen-chu')
          const list = await as(qa, 'secretary').get(`/api/meetings/${m.id}/recordings`)
          createdId = list.body.items.find((r) => r.fileName === fileName)?.id
          c.fact('the upload is listed', !!createdId)
          const state = (await transcript(qa, 'secretary', createdId)).status
          if (state === 'Pending' || state === 'Processing') {
            c.check('rerun while running', await as(qa, 'secretary').post(`/api/recordings/${createdId}/transcribe`), 409, 'MSG301')
            c.check('export while running', await as(qa, 'chair').get(`/api/recordings/${createdId}/transcript.txt`), 409, 'MSG357')
          } else qa.note(`Bản gỡ băng đã ${state} trước khi kịp gọi API: MSG301/MSG357 không kiểm được ở lượt này.`)
          await waitTranscript(qa, createdId)
          await qa.expect(row.getByText('Có bản gỡ băng')).toBeVisible({ timeout: 30_000 })
          await qa.expect(lines(row).locator('.tl-sentence').first()).toBeVisible()
          await row.scrollIntoViewIfNeeded()
          await settled(qa)
          await qa.shot('chuyen-chu-xong')
          await noRawCodes(qa)
          c.done()
        } finally {
          if (createdId) await as(qa, 'secretary').delete(`/api/recordings/${createdId}`)
        }
      },
    },

    // ------------------------------------------------------------------------------------------- UC-54a as Member
    {
      id: 'AC3',
      role: 'member',
      devices: ['desktop', 'phone'],
      title: 'Thành viên đọc bản gỡ băng nhưng không thấy gợi ý, không có nút sửa / gán / tải / chạy lại; API từ chối mọi thao tác ghi',
      steps: [
        'Đăng nhập thanhvien@, mở tab "Bản ghi" của "[QA] FE-10 Họp trực tiếp gỡ băng"',
        'API (thanhvien@): GET transcript, GET transcript.txt, PUT sửa câu, PUT gán tên, PUT đổi người nói một câu, POST transcribe',
      ],
      expected:
        'Lời thoại và tên người nói hiện như chữ thường (không ô chọn), không nút "Sửa nội dung", "Tải bản gỡ băng (.txt)", "Chuyển thành chữ lại", "Tải tệp lên"; API: canEdit/canExport false, people [], suggested* null (BR-120, BR-123); .txt 403 MSG355; sửa / gán / đổi 403 MSG295; transcribe 403',
      async run(qa) {
        const m = await meeting(qa, 'A')
        const c = checker(qa)
        const api = as(qa, 'member')
        const t = await transcript(qa, 'member', m.main.id)
        c.fact('canEdit is false', t.canEdit === false)
        c.fact('canExport is false', t.canExport === false)
        c.fact('people is empty for a Member', Array.isArray(t.people) && t.people.length === 0)
        c.fact('no suggestion reaches a Member', t.speakers.every((s) => s.suggestedUserId == null && s.suggestedName == null && s.suggestionScore == null))
        const seg = t.segments[0]
        c.check('export', await api.get(`/api/recordings/${m.main.id}/transcript.txt`), 403, 'MSG355')
        c.check('edit text', await api.put(`/api/transcript-segments/${seg.id}`, { text: 'Thành viên sửa' }), 403, 'MSG295')
        c.check('name a voice', await api.put(`/api/recordings/${m.main.id}/speakers/${seg.speakerLabel}`, { userId: null, guestId: null }), 403, 'MSG295')
        c.check('move one sentence', await api.put(`/api/transcript-segments/${seg.id}/speaker`, { label: seg.speakerLabel }), 403, 'MSG295')
        const rerun = await api.post(`/api/recordings/${m.main.id}/transcribe`)
        c.fact('transcribe as Member is refused', rerun.status === 403, `got ${rerun.status} ${rerun.text.slice(0, 120)}`)

        await openRecordings(qa, m)
        const row = recordingRow(qa, FILES.main)
        await qa.expect(lines(row).locator('.tl-say')).toHaveCount(t.segments.length)
        await qa.expect(lines(row).locator('strong.tl-name').first()).toBeVisible()
        await qa.expect(row.getByRole('button', { name: /^Tên của/ })).toHaveCount(0)
        await qa.expect(row.getByRole('button', { name: /^Người nói câu lúc/ })).toHaveCount(0)
        await qa.expect(row.getByRole('button', { name: 'Sửa nội dung' })).toHaveCount(0)
        await qa.expect(row.getByRole('button', { name: 'Tải bản gỡ băng (.txt)' })).toHaveCount(0)
        await qa.expect(row.getByRole('button', { name: 'Chuyển thành chữ lại' })).toHaveCount(0)
        await qa.expect(recordingsRegion(qa).getByRole('button', { name: 'Tải tệp lên' })).toHaveCount(0)
        await qa.expect(qa.page.getByRole('region', { name: 'Giọng mẫu cho mic phòng' })).toHaveCount(0)
        await row.scrollIntoViewIfNeeded()
        await settled(qa)
        await qa.shot('thanh-vien-chi-doc')
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
        c.done()
      },
    },

    // ------------------------------------------------------------------------------------------- UC-55a Edit text
    {
      id: 'AC4',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Thư ký sửa chữ một câu tại chỗ (MSG302, dấu "đã sửa" ghi người sửa); rỗng hoặc quá 8000 ký tự bị chặn (MSG303); Chủ toạ không sửa được (MSG295)',
      steps: [
        'Thư ký bấm vào câu "Tôi đề nghị bổ sung…" để chọn, bấm "Sửa nội dung" của đoạn, sửa thành chữ mới, nhấn Enter',
        'API: PUT câu với chữ toàn khoảng trắng, với 8001 ký tự; Chủ toạ PUT; quantri@ (ngoài danh sách) PUT',
      ],
      expected:
        'Toast "Đã lưu đoạn gỡ băng."; câu hiện chữ mới kèm "đã sửa"; API: editedByName là Thư ký, editedAt có giá trị; 400 MSG303 hai lần; Chủ toạ 403 MSG295; quantri@ 404; chữ cũ được trả lại sau khi kiểm',
      async run(qa) {
        const m = await meeting(qa, 'A')
        const u = await users(qa)
        const c = checker(qa)
        const before = await transcript(qa, 'secretary', m.main.id)
        const { segment } = segmentOf(before, /đề nghị|kinh phí/i)
        qa.expect(segment, 'no sentence "Tôi đề nghị…" in the transcript').toBeTruthy()
        const original = segment.text
        const changed = `${original.replace(/[.\s]+$/, '')} và phần mềm quản lý văn bản.`
        try {
          await openRecordings(qa, m)
          const row = recordingRow(qa, FILES.main)
          // PR #424: one speaker's consecutive sentences read as one paragraph; picking a sentence points the
          // paragraph's tools at it, and "Sửa nội dung" opens it for editing.
          const sayOf = qa.page.locator('.tl-say').filter({ hasText: original.slice(0, 20) })
          const paragraph = lines(row).locator('.tl-sentence').filter({ has: sayOf })
          const say = paragraph.locator('.tl-say').filter({ hasText: original.slice(0, 20) })
          await say.scrollIntoViewIfNeeded()
          await press(qa, say)
          await press(qa, paragraph.getByRole('button', { name: /^Sửa nội dung/ }))
          const box = row.getByRole('textbox', { name: 'Nội dung đoạn' })
          await qa.expect(box).toBeVisible()
          await box.fill(changed)
          await settled(qa)
          await qa.shot('dang-sua-cau')
          await box.press('Enter')
          await qa.expect(toast(qa, MSG.MSG302)).toBeVisible()
          const edited = lines(row).locator('.tl-sentence').filter({ hasText: 'phần mềm quản lý văn bản' })
          await qa.expect(edited).toBeVisible()
          await qa.expect(edited.getByText('đã sửa')).toBeVisible()
          await settled(qa)
          await qa.shot('cau-da-sua')
          const after = (await transcript(qa, 'secretary', m.main.id)).segments.find((s) => s.id === segment.id)
          c.fact('text saved', after.text === changed, after.text)
          c.fact('editor recorded', after.editedByName === u.secretary.name && !!after.editedAt, `${after.editedByName} ${after.editedAt}`)
          const sec = as(qa, 'secretary')
          c.check('blank text', await sec.put(`/api/transcript-segments/${segment.id}`, { text: '   ' }), 400, 'MSG303')
          c.check('8001 characters', await sec.put(`/api/transcript-segments/${segment.id}`, { text: 'a'.repeat(8001) }), 400, 'MSG303')
          c.check('Chair edits', await as(qa, 'chair').put(`/api/transcript-segments/${segment.id}`, { text: 'Chủ toạ sửa' }), 403, 'MSG295')
          c.check('outsider edits', await as(qa, 'admin').put(`/api/transcript-segments/${segment.id}`, { text: 'Quản trị sửa' }), 404)
          await noRawCodes(qa)
          c.done()
        } finally {
          await as(qa, 'secretary').put(`/api/transcript-segments/${segment.id}`, { text: original })
        }
      },
    },

    // ------------------------------------------------------------------------------------------- UC-55b Name a voice
    {
      id: 'AC5',
      role: 'secretary',
      title: 'Thư ký gán tên một giọng từ thẻ người nói (MSG316): mọi câu của giọng đó mang tên, Thành viên cũng thấy tên; người ngoài danh sách / gửi cả hai bị chặn (MSG317)',
      steps: [
        'Thư ký mở ô "Tên của Người nói N" của giọng nói câu "Tôi đề nghị…", chọn "[QA] FE-10 Người nói A"',
        'Thành viên mở cùng bản gỡ băng',
        'API: gán quantri@ (không có trong cuộc họp); gửi cả userId và guestId; gán Khách mời của cuộc họp; Chủ toạ gán; nhãn SPEAKER_99; bỏ gán',
      ],
      expected:
        'Toast "Đã gán người nói."; ô và tiêu đề các câu của giọng đó hiện tên mới; Thành viên thấy tên (UC-54a POST-2) mà không thấy gợi ý; quantri@ 400 MSG317; cả hai 400 MSG317; Khách 200 MSG316 (Khách có trong danh sách chọn, nhãn "Khách"); Chủ toạ 403 MSG295; nhãn lạ 404; bỏ gán 200 MSG316',
      async run(qa) {
        const m = await meeting(qa, 'A')
        const u = await users(qa)
        const c = checker(qa)
        await clearNames(qa, m.main.id)
        const t = await transcript(qa, 'secretary', m.main.id)
        const { speaker } = segmentOf(t, /đề nghị|kinh phí/i)
        qa.expect(speaker, 'no voice for "Tôi đề nghị…"').toBeTruthy()
        const label = speaker.label
        c.fact('the guest is offered to the Secretary', t.people.some((p) => p.guest && p.id === m.guest?.id))
        try {
          await openRecordings(qa, m)
          const row = recordingRow(qa, FILES.main)
          const trigger = row.locator('aside.transcript-speakers').getByRole('button', { name: `Tên của ${speakerName(label)}` })
          await trigger.scrollIntoViewIfNeeded()
          await press(qa, trigger)
          const list = qa.page.getByRole('listbox', { name: `Tên của ${speakerName(label)}` })
          await qa.expect(list).toBeVisible()
          await qa.expect(list.getByRole('option', { name: new RegExp(`^${esc(GUEST.invitedName)}.*Khách`) })).toBeVisible()
          await settled(qa)
          await qa.shot('chon-ten-giong')
          await press(qa, list.getByRole('option', { name: new RegExp(`^${esc(u.a.name)}`) }))
          await qa.expect(toast(qa, MSG.MSG316)).toBeVisible()
          await qa.expect(row.locator('aside.transcript-speakers').getByRole('button', { name: `Tên của ${u.a.name}: ${u.a.name}` })).toBeVisible()
          await qa.expect(lines(row).getByRole('button', { name: new RegExp(`^Người nói câu lúc .*: ${esc(u.a.name)}$`) }).first()).toBeVisible()
          await settled(qa)
          await qa.shot('giong-da-co-ten')
          const after = (await transcript(qa, 'secretary', m.main.id)).speakers.find((s) => s.label === label)
          c.fact('assigned', after.assignedUserId === u.a.id && after.autoAssigned === false, JSON.stringify(after))
          const asMember = await transcript(qa, 'member', m.main.id)
          const seen = asMember.speakers.find((s) => s.label === label)
          c.fact('a Member reads the name', seen?.assignedName === u.a.name, JSON.stringify(seen))

          const sec = as(qa, 'secretary')
          const path = `/api/recordings/${m.main.id}/speakers/${label}`
          c.check('outsider', await sec.put(path, { userId: u.admin.id, guestId: null }), 400, 'MSG317')
          c.check('both user and guest', await sec.put(path, { userId: u.a.id, guestId: m.guest.id }), 400, 'MSG317')
          c.check('guest of the meeting', await sec.put(path, { userId: null, guestId: m.guest.id }), 200, 'MSG316')
          c.check('Chair names', await as(qa, 'chair').put(path, { userId: u.b.id, guestId: null }), 403, 'MSG295')
          c.check('unknown label', await sec.put(`/api/recordings/${m.main.id}/speakers/SPEAKER_99`, { userId: u.b.id, guestId: null }), 404)
          c.check('clear', await sec.put(path, { userId: null, guestId: null }), 200, 'MSG316')

          await qa.signIn('member')
          await sec.put(path, { userId: u.a.id, guestId: null })
          await openRecordings(qa, m)
          await qa.expect(lines(recordingRow(qa, FILES.main)).locator('strong.tl-name').filter({ hasText: u.a.name }).first()).toBeVisible()
          await recordingRow(qa, FILES.main).scrollIntoViewIfNeeded()
          await settled(qa)
          await qa.shot('thanh-vien-thay-ten')
          c.done()
        } finally {
          await clearNames(qa, m.main.id)
        }
      },
    },

    // ------------------------------------------------------------------------------------------- UC-55b AF-2 one line
    {
      id: 'AC6',
      role: 'secretary',
      title: 'Thư ký đổi người nói của riêng một câu ("chỉ câu này", MSG407): câu sang giọng mới SPEAKER_NN của người được chọn, các câu khác giữ giọng; trả về được',
      steps: [
        'Thư ký mở ô "Người nói câu lúc …" của câu "Cảm ơn ý kiến…", chọn "[QA] FE-10 Người nói B"',
        'API: chuyển câu về nhãn cũ; gửi {} ; gửi cả userId và label; nhãn SPEAKER_77; quantri@; Chủ toạ; Thành viên',
      ],
      expected:
        'Toast "Đã đổi người nói của câu này."; chỉ câu đó mang tên "[QA] FE-10 Người nói B", câu "Kính thưa…" của cùng giọng cũ giữ nguyên; API: câu có nhãn mới (giọng mới gán người B), thời lượng chuyển theo câu (UC-55b); trả về nhãn cũ 200 MSG407 và giọng mới không còn trong speakers; {} và hai trường 400 MSG317; nhãn lạ 404; quantri@ 400 MSG317; Chủ toạ, Thành viên 403 MSG295',
      async run(qa) {
        const m = await meeting(qa, 'A')
        const u = await users(qa)
        const c = checker(qa)
        await clearNames(qa, m.main.id)
        const before = await transcript(qa, 'secretary', m.main.id)
        const { segment, speaker } = segmentOf(before, /Cảm ơn ý kiến|biểu quyết/i)
        const first = segmentOf(before, /Kính thưa|ngân sách/i).segment
        qa.expect(segment && first, 'the sentences "Cảm ơn…" / "Kính thưa…" are missing').toBeTruthy()
        const original = segment.speakerLabel
        const path = `/api/transcript-segments/${segment.id}/speaker`
        try {
          await openRecordings(qa, m)
          const row = recordingRow(qa, FILES.main)
          const group = lines(row).locator('li.tl-group').filter({ hasText: segment.text.slice(0, 15) })
          const trigger = group.getByRole('button', { name: new RegExp(`^Người nói câu lúc ${esc(formatDuration(segment.startMs))}`) })
          await trigger.scrollIntoViewIfNeeded()
          await press(qa, trigger)
          const list = qa.page.getByRole('listbox', { name: `Người nói câu lúc ${formatDuration(segment.startMs)}` })
          await qa.expect(list).toBeVisible()
          await settled(qa)
          await qa.shot('chon-nguoi-noi-mot-cau')
          await press(qa, list.getByRole('option', { name: new RegExp(`^${esc(u.b.name)}`) }))
          await qa.expect(toast(qa, MSG.MSG407)).toBeVisible()
          await qa.expect(lines(row).locator('li.tl-group').filter({ hasText: segment.text.slice(0, 15) }).getByRole('button', { name: new RegExp(`: ${esc(u.b.name)}$`) })).toBeVisible()
          await settled(qa)
          await qa.shot('cau-da-doi-nguoi-noi')
          const after = await transcript(qa, 'secretary', m.main.id)
          const moved = after.segments.find((s) => s.id === segment.id)
          const voice = after.speakers.find((s) => s.label === moved.speakerLabel)
          c.fact('the sentence left its voice', moved.speakerLabel !== original, moved.speakerLabel)
          c.fact('its new voice is named B', voice?.assignedUserId === u.b.id, JSON.stringify(voice))
          c.fact('the other sentence kept its voice', after.segments.find((s) => s.id === first.id).speakerLabel === first.speakerLabel)
          const oldVoice = after.speakers.find((s) => s.label === original)
          if (oldVoice && speaker)
            c.fact('speech time moved with the sentence', oldVoice.speechMs < speaker.speechMs, `${speaker.speechMs} → ${oldVoice.speechMs}`)
          const sec = as(qa, 'secretary')
          c.check('nothing named', await sec.put(path, {}), 400, 'MSG317')
          c.check('two targets', await sec.put(path, { userId: u.a.id, label: original }), 400, 'MSG317')
          c.check('label of no voice here', await sec.put(path, { label: 'SPEAKER_77' }), 404)
          c.check('outsider', await sec.put(path, { userId: u.admin.id }), 400, 'MSG317')
          c.check('Chair', await as(qa, 'chair').put(path, { label: original }), 403, 'MSG295')
          c.check('Member', await as(qa, 'member').put(path, { label: original }), 403, 'MSG295')
          c.check('move back', await sec.put(path, { label: original }), 200, 'MSG407')
          const back = await transcript(qa, 'secretary', m.main.id)
          c.fact('back on its voice', back.segments.find((s) => s.id === segment.id).speakerLabel === original)
          c.fact('an empty voice is hidden', !back.speakers.some((s) => s.label === moved.speakerLabel), JSON.stringify(back.speakers.map((s) => s.label)))
          c.fact('speech time restored', back.speakers.find((s) => s.label === original)?.speechMs === speaker?.speechMs)
          c.done()
        } finally {
          const now = await transcript(qa, 'secretary', m.main.id)
          if (now.segments.find((s) => s.id === segment.id)?.speakerLabel !== original)
            await as(qa, 'secretary').put(path, { label: original })
        }
      },
    },

    // ------------------------------------------------------------------------------------------- UC-54b Export
    {
      id: 'AC7',
      role: 'chair',
      title: 'Chủ toạ tải bản gỡ băng .txt (đầu tệp, mỗi câu một dòng [HH:MM:SS] Tên: chữ), không có nút sửa; cuộc họp Mật không ai tải được (MSG356)',
      steps: [
        'Đăng nhập chutoa@, mở tab "Bản ghi" của "[QA] FE-10 Họp trực tiếp gỡ băng", bấm "Tải bản gỡ băng (.txt)"',
        'Mở tab "Bản ghi" của "[QA] FE-10 Họp Mật gỡ băng"; API GET transcript.txt (Chủ toạ, Thư ký)',
      ],
      expected:
        'Tệp .txt tải về có tên cuộc họp và các dòng "[00:00:00] Người nói 1: …"; Chủ toạ không có ô chọn tên / nút sửa; họp Mật: không có nút tải, API 403 MSG356 cho cả Chủ toạ và Thư ký',
      async run(qa) {
        const m = await meeting(qa, 'A')
        const d = await meeting(qa, 'D')
        const c = checker(qa)
        await openRecordings(qa, m)
        const row = recordingRow(qa, FILES.main)
        await qa.expect(row.getByRole('button', { name: /^Tên của/ })).toHaveCount(0)
        await qa.expect(row.getByRole('button', { name: 'Sửa nội dung' })).toHaveCount(0)
        const button = row.getByRole('button', { name: 'Tải bản gỡ băng (.txt)' })
        await button.scrollIntoViewIfNeeded()
        await settled(qa)
        await qa.shot('chu-toa-co-nut-tai')
        const [download] = await Promise.all([qa.page.waitForEvent('download'), button.click()])
        const text = readFileSync(await download.path(), 'utf8')
        c.fact('file name', /bản gỡ băng\.txt$/.test(download.suggestedFilename()), download.suggestedFilename())
        c.fact('header names the meeting', text.includes(m.title), text.slice(0, 200))
        c.fact('one line per sentence', /^\[\d\d:\d\d:\d\d\] [^:\n]+: .+/m.test(text), text.slice(0, 400))
        c.fact('the words are there', /ngân sách/i.test(text))
        qa.note(`Đầu tệp: ${text.split('\n').slice(0, 6).join(' | ').slice(0, 300)}`)
        c.check('Chair, Secret meeting', await as(qa, 'chair').get(`/api/recordings/${d.rec.id}/transcript.txt`), 403, 'MSG356')
        c.check('Secretary, Secret meeting', await as(qa, 'secretary').get(`/api/recordings/${d.rec.id}/transcript.txt`), 403, 'MSG356')
        await openRecordings(qa, d)
        const secretRow = recordingRow(qa, FILES.secret)
        await qa.expect(lines(secretRow)).toBeVisible()
        await qa.expect(secretRow.getByRole('button', { name: 'Tải bản gỡ băng (.txt)' })).toHaveCount(0)
        await qa.expect(secretRow.getByRole('button', { name: 'Tải tệp ghi âm' })).toHaveCount(0)
        await settled(qa)
        await qa.shot('hop-mat-khong-tai')
        c.done()
      },
    },

    // ------------------------------------------------------------------------------------------- outsiders
    {
      id: 'AC8',
      role: 'admin',
      title: 'Người không có trong danh sách cuộc họp (quantri@, kể cả All-in-one) không thấy tab Bản ghi và API trả 404; chưa đăng nhập 401',
      steps: [
        'Đăng nhập quantri@ (không có trong danh sách), mở cuộc họp "[QA] FE-10 Họp trực tiếp gỡ băng" với ?tab=recordings',
        'API (quantri@): recordings, transcript, transcript.txt, speech-clips của cuộc họp trực tuyến, voice-collection; API chưa đăng nhập: transcript',
      ],
      expected: 'Không có tab "Bản ghi" (mở thẳng link thì về tab khác hoặc trang không tìm thấy); API 404 mọi đường; chưa đăng nhập 401',
      async run(qa) {
        const m = await meeting(qa, 'A')
        const cMeeting = await meeting(qa, 'C')
        const b = await meeting(qa, 'B')
        const c = checker(qa)
        const caps = await qa.capabilities('admin')
        qa.note(`quantri@: ${caps.roleName} — ${caps.capabilities.join(', ')}`)
        const api = as(qa, 'admin')
        c.check('recordings', await api.get(`/api/meetings/${m.id}/recordings`), 404)
        c.check('transcript', await api.get(`/api/recordings/${m.main.id}/transcript`), 404)
        c.check('export', await api.get(`/api/recordings/${m.main.id}/transcript.txt`), 404)
        c.check('per-mic sentences', await api.get(`/api/meetings/${cMeeting.id}/speech-clips`), 404)
        c.check('voice collection (has VoiceCollector, not the Secretary)', await api.get(`/api/meetings/${b.id}/voice-collection`), 404)
        c.check('anonymous transcript', await as(qa, 'anon').get(`/api/recordings/${m.main.id}/transcript`), 401)
        await qa.goto(`/mgmt/meetings/${m.id}?tab=recordings`)
        await qa.page.waitForLoadState('networkidle').catch(() => undefined)
        await qa.expect(qa.page.getByRole('tab', { name: 'Bản ghi' })).toHaveCount(0)
        await qa.expect(qa.page.getByRole('region', { name: 'Bản ghi', exact: true })).toHaveCount(0)
        await settled(qa)
        await qa.shot('nguoi-ngoai-khong-co-tab-ban-ghi')
        c.done()
      },
    },

    // ------------------------------------------------------------------------------------------- UC-55c Exclude
    {
      id: 'AC9',
      role: 'chair',
      devices: ['desktop', 'phone'],
      title: 'Chủ toạ bỏ một câu theo micro khỏi biên bản (MSG351) rồi khôi phục (MSG352); tệp.txt không có câu đã bỏ (UC-54b)',
      steps: [
        'Đăng nhập chutoa@, mở tab "Bản ghi" của "[QA] FE-10 Họp trực tuyến theo micro"',
        'Ở câu "Tôi đề nghị…" của "[QA] FE-10 Người nói A", bấm "Bỏ khỏi biên bản"; API tải speech-clips.txt',
        'Bấm "Khôi phục"; API tải lại .txt',
      ],
      expected:
        'Toast "Đã loại câu khỏi bản ghi."; câu mang nhãn "Đã bỏ khỏi biên bản", vẫn đọc được với Chủ toạ; API excluded=true, có lý do, người bỏ là Chủ toạ; .txt không có câu đó; khôi phục: toast "Đã đưa câu trở lại bản ghi.", .txt có lại câu',
      async run(qa) {
        const cm = await meeting(qa, 'C')
        const u = await users(qa)
        const c = checker(qa)
        const all = await clips(qa, 'chair', cm.id)
        const target = all.items.find((x) => x.speakerId === u.a.id)
        qa.expect(target, 'no sentence of "[QA] FE-10 Người nói A"').toBeTruthy()
        const words = target.text.split(/\s+/).slice(1, 4).join(' ')
        try {
          if (target.excluded) await as(qa, 'chair').put(`/api/speech-clips/${target.id}/exclusion`, { excluded: false })
          await openRecordings(qa, cm)
          const card = qa.page.locator('section.tl-card')
          await qa.expect(card).toBeVisible()
          // PR #424: the sentence sits in its speaker's paragraph; picking it points the paragraph's tools at it.
          const say = card.locator(`[data-clip="${target.id}"]`)
          const sentence = card.locator('.tl-sentence').filter({ has: qa.page.locator(`[data-clip="${target.id}"]`) })
          await say.scrollIntoViewIfNeeded()
          await say.focus()
          if (qa.device === 'desktop') await sentence.hover()
          const drop = sentence.getByRole('button', { name: /^Bỏ khỏi biên bản/ })
          await qa.expect(drop).toBeVisible()
          await settled(qa)
          await qa.shot('truoc-khi-bo-cau')
          await press(qa, drop)
          await qa.expect(toast(qa, MSG.MSG351)).toBeVisible()
          const group = card.locator('li.tl-group').filter({ has: qa.page.locator(`[data-clip="${target.id}"]`) })
          await qa.expect(group.getByText('Đã bỏ khỏi biên bản')).toBeVisible()
          await settled(qa)
          await qa.shot('cau-da-bo')
          const after = (await clips(qa, 'chair', cm.id)).items.find((x) => x.id === target.id)
          c.fact('excluded', after.excluded === true && ['Leak', 'Other'].includes(after.exclusionReason), JSON.stringify(after).slice(0, 200))
          c.fact('excluded by the Chair', after.excludedByName === u.chair.name, after.excludedByName)
          c.fact('the Chair still reads it', after.text === target.text)
          const txt = await as(qa, 'chair').get(`/api/meetings/${cm.id}/speech-clips.txt`)
          c.check('export', txt, 200)
          c.fact('the excluded sentence is left out of the file', !txt.text.includes(words), words)
          const restore = group.getByRole('button', { name: /^Khôi phục/ })
          if (qa.device === 'desktop') await group.locator('.tl-sentence').hover()
          await press(qa, restore)
          await qa.expect(toast(qa, MSG.MSG352)).toBeVisible()
          await qa.expect(group.getByText('Đã bỏ khỏi biên bản')).toHaveCount(0)
          await settled(qa)
          await qa.shot('cau-da-khoi-phuc')
          const again = await as(qa, 'chair').get(`/api/meetings/${cm.id}/speech-clips.txt`)
          c.fact('the restored sentence is back in the file', again.text.includes(words), words)
          await noSidewaysScroll(qa)
          await noRawCodes(qa)
          c.done()
        } finally {
          const now = (await clips(qa, 'chair', cm.id)).items.find((x) => x.id === target.id)
          if (now?.excluded) await as(qa, 'chair').put(`/api/speech-clips/${target.id}/exclusion`, { excluded: false })
        }
      },
    },
    {
      id: 'AC10',
      role: 'chair',
      title: 'Bỏ câu khỏi biên bản: hệ thống hỏi lý do "Lọt tiếng" hoặc "Khác" trước khi bỏ (UC-55c bước 2–3)',
      steps: ['Chủ toạ bấm "Bỏ khỏi biên bản" ở một câu theo micro'],
      expected: 'Hiện lựa chọn lý do (Lọt tiếng / Khác) để chọn và xác nhận, rồi mới bỏ câu (UC-55c Normal Flow bước 2–3)',
      async run(qa) {
        const cm = await meeting(qa, 'C')
        const u = await users(qa)
        const target = (await clips(qa, 'chair', cm.id)).items.find((x) => x.speakerId === u.member.id)
        try {
          if (target.excluded) await as(qa, 'chair').put(`/api/speech-clips/${target.id}/exclusion`, { excluded: false })
          await openRecordings(qa, cm)
          const say = qa.page.locator(`[data-clip="${target.id}"]`)
          const sentence = qa.page.locator('.tl-sentence').filter({ has: say })
          await say.scrollIntoViewIfNeeded()
          await say.focus()
          await sentence.hover()
          await sentence.getByRole('button', { name: /^Bỏ khỏi biên bản/ }).click()
          const asked = await qa.page
            .getByRole('dialog')
            .waitFor({ state: 'visible', timeout: 2500 })
            .then(() => true)
            .catch(() => false)
          await settled(qa)
          await qa.shot(asked ? 'hoi-ly-do' : 'khong-hoi-ly-do')
          const after = (await clips(qa, 'chair', cm.id)).items.find((x) => x.id === target.id)
          qa.expect(
            asked,
            `Không hỏi lý do: câu bị bỏ ngay (excluded=${after.excluded}, lý do tự chọn "${after.exclusionReason}") — UC-55c bước 2 yêu cầu hỏi "Lọt tiếng" hoặc "Khác"`,
          ).toBe(true)
        } finally {
          const now = (await clips(qa, 'chair', cm.id)).items.find((x) => x.id === target.id)
          if (now?.excluded) await as(qa, 'chair').put(`/api/speech-clips/${target.id}/exclusion`, { excluded: false })
        }
      },
    },
    {
      id: 'AC11',
      role: 'member',
      devices: ['desktop', 'phone'],
      title: 'Thành viên chỉ thấy chỗ trống "Câu đã được bỏ khỏi biên bản.", không có nút bỏ / khôi phục / tải; API: 403 MSG353, lý do sai 400 MSG354, Thư ký bỏ được',
      steps: [
        'Chủ toạ (API) bỏ câu của "[QA] FE-10 Người nói A"; thanhvien@ mở tab "Bản ghi" của cuộc họp trực tuyến',
        'API: thanhvien@ PUT exclusion, GET speech-clips.txt; Chủ toạ PUT exclusion reason "Noise"; Thư ký bỏ rồi khôi phục',
      ],
      expected:
        'Ở chỗ câu đã bỏ chỉ có "Câu đã được bỏ khỏi biên bản."; không có nút "Bỏ khỏi biên bản", "Khôi phục", "Tải lời thoại (.txt)"; API thành viên nhận text "" cho câu đó; 403 MSG353; 403 MSG355; 400 MSG354; Thư ký 200 MSG351 rồi 200 MSG352',
      async run(qa) {
        const cm = await meeting(qa, 'C')
        const u = await users(qa)
        const c = checker(qa)
        const target = (await clips(qa, 'chair', cm.id)).items.find((x) => x.speakerId === u.a.id)
        const path = `/api/speech-clips/${target.id}/exclusion`
        try {
          c.check('Chair excludes', await as(qa, 'chair').put(path, { excluded: true, reason: 'Other' }), 200, 'MSG351')
          const seen = (await clips(qa, 'member', cm.id)).items.find((x) => x.id === target.id)
          c.fact('a Member gets an empty entry', seen?.excluded === true && seen.text === '' && !seen.hasAudio, JSON.stringify(seen).slice(0, 200))
          await openRecordings(qa, cm)
          const card = qa.page.locator('section.tl-card')
          await qa.expect(card.getByText('Câu đã được bỏ khỏi biên bản.')).toBeVisible()
          await qa.expect(card.getByRole('button', { name: 'Bỏ khỏi biên bản' })).toHaveCount(0)
          await qa.expect(card.getByRole('button', { name: 'Khôi phục' })).toHaveCount(0)
          await qa.expect(card.getByRole('button', { name: 'Tải lời thoại (.txt)' })).toHaveCount(0)
          await card.getByText('Câu đã được bỏ khỏi biên bản.').scrollIntoViewIfNeeded()
          await settled(qa)
          await qa.shot('thanh-vien-thay-cho-trong')
          await noSidewaysScroll(qa)
          await noRawCodes(qa)
          c.check('Member excludes', await as(qa, 'member').put(path, { excluded: false }), 403, 'MSG353')
          c.check('Member exports', await as(qa, 'member').get(`/api/meetings/${cm.id}/speech-clips.txt`), 403, 'MSG355')
          c.check('Chair restores', await as(qa, 'chair').put(path, { excluded: false }), 200, 'MSG352')
          c.check('invalid reason', await as(qa, 'chair').put(path, { excluded: true, reason: 'Noise' }), 400, 'MSG354')
          c.check('Secretary excludes', await as(qa, 'secretary').put(path, { excluded: true, reason: 'Leak' }), 200, 'MSG351')
          c.check('Secretary restores', await as(qa, 'secretary').put(path, { excluded: false }), 200, 'MSG352')
          c.done()
        } finally {
          await as(qa, 'chair').put(path, { excluded: false })
        }
      },
    },

    // ------------------------------------------------------------------------------------------- UC-56b Consent
    {
      id: 'AC12',
      role: 'admin',
      devices: ['desktop', 'phone'],
      title: 'Người dùng đọc thông báo Voice ID rồi tự đồng ý (MSG305): ô đồng ý không đánh sẵn, nút khoá tới khi đánh dấu, nêu mục đích, bên kiểm soát, dữ liệu nhạy cảm',
      steps: [
        'Đăng nhập qa-fe10-a@ (chưa bật Voice ID), mở "Giọng nói của tôi"',
        'Đọc thông báo, đánh dấu ô đồng ý, bấm "Đồng ý và bật"',
        'API: đồng ý với phiên bản thông báo "1.0"; đồng ý lại với phiên bản hiện hành',
      ],
      expected:
        'Thông báo có Mục đích, Ai thu (gồm Thư ký thu bằng micro phòng họp), Lưu gì, Quyền (rút lại thì xoá cả mẫu Thư ký thu), Bên kiểm soát dữ liệu, câu "dữ liệu cá nhân nhạy cảm"; ô chưa đánh, nút khoá; sau khi bấm: toast "Đã bật nhận diện giọng nói cho tài khoản của bạn.", chip "Đang bật", tab "Tự thu" / "Thư ký thu hộ", nhắc ghi giọng mẫu; API: noticeVersion 1.3; "1.0" 400 MSG313; gọi lại 200 MSG305, vẫn một đồng ý; cuối cùng rút lại, không còn mẫu',
      async run(qa) {
        const u = await users(qa)
        const c = checker(qa)
        await withdrawn(qa, 'a')
        try {
          await qa.signIn(u.a.email)
          await openMyVoice(qa)
          const card = qa.page.getByRole('region', { name: 'Bật nhận diện giọng nói' })
          await qa.expect(card).toBeVisible()
          for (const text of ['Mục đích:', 'Ai thu:', 'Lưu gì:', 'Quyền của bạn:', 'Bên kiểm soát dữ liệu:'])
            await qa.expect(card.getByText(text)).toBeVisible()
          await qa.expect(card.getByText(/micro phòng họp/).first()).toBeVisible()
          await qa.expect(card.getByText(/dữ liệu cá nhân nhạy cảm/)).toBeVisible()
          const box = card.getByRole('checkbox')
          await qa.expect(box).not.toBeChecked()
          const enable = card.getByRole('button', { name: 'Đồng ý và bật' })
          await qa.expect(enable).toBeDisabled()
          await settled(qa)
          await qa.shot('thong-bao-truoc-khi-dong-y', { fullPage: true })
          await press(qa, box)
          await qa.expect(enable).toBeEnabled()
          await press(qa, enable)
          await qa.expect(toast(qa, MSG.MSG305)).toBeVisible()
          await qa.expect(qa.page.getByText('Đang bật')).toBeVisible()
          await qa.expect(qa.page.getByRole('tab', { name: /Tự thu/ })).toBeVisible()
          await qa.expect(qa.page.getByRole('tab', { name: /Thư ký thu hộ/ })).toBeVisible()
          await qa.expect(qa.page.getByText(/Chưa có giọng mẫu nào/)).toBeVisible()
          await settled(qa)
          await qa.shot('da-bat-voice-id')
          await noSidewaysScroll(qa)
          await noRawCodes(qa)
          const v = await voice(qa, 'a')
          c.fact('consent on the current notice', v.consent?.noticeVersion === v.noticeVersion && v.noticeVersion === '1.3', JSON.stringify(v.consent))
          c.fact('proxy collection allowed under 1.3', v.proxy.allowed === true)
          c.check('stale notice version', await as(qa, 'a').post('/api/me/voice/consent', { noticeVersion: '1.0' }), 400, 'MSG313')
          c.check('consent again', await as(qa, 'a').post('/api/me/voice/consent', { noticeVersion: v.noticeVersion }), 200, 'MSG305')
          c.fact('still the same consent', (await voice(qa, 'a')).consent?.grantedAt === v.consent.grantedAt)
          c.done()
        } finally {
          await withdrawn(qa, 'a')
        }
      },
    },

    // ------------------------------------------------------------------------------------------- UC-56c/d Samples
    {
      id: 'AC13',
      role: 'admin',
      title: 'Ghi giọng mẫu: hộp thoại đọc đoạn văn, dừng, nghe lại rồi mới gửi; mẫu không phải tiếng nói bị từ chối bằng lời tiếng Việt; mẫu đạt lưu (MSG307), nghe lại được; giới hạn 5 mẫu (MSG310); lỗi tệp',
      steps: [
        'qa-fe10-a@ đã đồng ý, chưa có mẫu: bấm "Ghi giọng mẫu", "Bắt đầu ghi" (micro giả của trình duyệt phát tiếng bíp), chờ 8 giây, "Dừng", "Lưu giọng mẫu"',
        'API: gửi mẫu đọc 12 giây; tải lại trang, bấm "Nghe lại giọng mẫu 1"',
        'API: mẫu 1 giây; tệp văn bản; thanhvien@ nghe / xoá mẫu của A; qa-fe10-b@ (chưa đồng ý) gửi mẫu; gửi tới đủ 5 mẫu rồi mẫu thứ 6',
      ],
      expected:
        'Không gửi gì trước khi bấm Lưu (có trình phát nghe lại và nút "Ghi lại"); tiếng bíp bị từ chối, hộp thoại báo lý do tiếng Việt (MSG309), không lưu mẫu; mẫu đọc: 201 MSG307, playable, speechMs ≥ 6000, trang hiện "Giọng mẫu 1" và phát được (200); 1 giây 422 MSG309 too_short; văn bản 415 MSG292; người khác 404 / 404; chưa đồng ý 409 MSG308; mẫu thứ 6 409 MSG310 và nút "Ghi giọng mẫu" khoá, "5/5"; cuối cùng rút lại, không còn mẫu',
      async run(qa) {
        const u = await users(qa)
        const c = checker(qa)
        await withdrawn(qa, 'b')
        await enrolled(qa, 'a', 0)
        try {
          await qa.signIn(u.a.email)
          await openMyVoice(qa)
          await qa.page.getByRole('button', { name: 'Ghi giọng mẫu' }).click()
          const dialog = qa.page.getByRole('dialog', { name: 'Ghi giọng mẫu' })
          await qa.expect(dialog.locator('blockquote.voice-passage')).toBeVisible()
          await dialog.getByRole('button', { name: 'Bắt đầu ghi' }).click()
          await qa.expect(dialog.getByRole('status').filter({ hasText: 'Đang ghi' })).toBeVisible()
          await settled(qa)
          await qa.shot('dang-ghi-giong-mau')
          const stop = dialog.getByRole('button', { name: 'Dừng' })
          await qa.expect(stop).toBeEnabled({ timeout: 15_000 })
          await stop.click()
          await qa.expect(dialog.getByLabel('Nghe lại giọng mẫu vừa ghi')).toBeVisible()
          await qa.expect(dialog.getByRole('button', { name: 'Ghi lại' })).toBeVisible()
          c.fact('nothing sent before saving', (await voice(qa, 'a')).samples.length === 0)
          await settled(qa)
          await qa.shot('nghe-lai-truoc-khi-luu')
          const answer = qa.page.waitForResponse((r) => r.url().endsWith('/api/me/voice/samples') && r.request().method() === 'POST', { timeout: 60_000 })
          await dialog.getByRole('button', { name: 'Lưu giọng mẫu' }).click()
          const res = await answer
          if (res.status() === 201) {
            qa.note('Micro giả (tiếng bíp) được nhận là giọng mẫu hợp lệ: cổng chất lượng BR-118, BR-119 không chặn âm không phải tiếng người.')
            c.fact('a beep is not a voice sample (BR-118, BR-119)', false)
          } else {
            const body = await res.json()
            c.fact('refused as MSG309', res.status() === 422 && body.code === 'MSG309', `${res.status()} ${JSON.stringify(body)}`)
            await qa.expect(dialog.getByRole('alert')).toBeVisible()
            const said = await dialog.getByRole('alert').innerText()
            c.fact('the reason reads in Vietnamese', /giọng|mẫu|tiếng/i.test(said) && !/MSG\d/.test(said), said)
            await settled(qa)
            await qa.shot('mau-khong-dat')
          }
          await dialog.getByRole('button', { name: /Đóng/ }).first().click().catch(() => qa.page.keyboard.press('Escape'))

          const a = as(qa, 'a')
          await enrolled(qa, 'a', 0)
          const saved = await a.form('/api/me/voice/samples', wavForm(audio(qa, 'enrol'), 'giong-mau.wav'))
          c.check('a 12-second reading', saved, 201, 'MSG307')
          const sample = saved.body?.data
          c.fact('kept playable, speech counted', sample?.playable === true && sample.speechMs >= 6000, JSON.stringify(sample))
          await openMyVoice(qa)
          const item = qa.page.locator('li.voice-sample').filter({ hasText: 'Giọng mẫu 1' })
          await qa.expect(item).toBeVisible()
          const heard = qa.page.waitForResponse((r) => r.url().includes(`/api/me/voice/samples/${sample.id}/audio`))
          await item.getByRole('button', { name: 'Nghe lại giọng mẫu 1' }).click()
          c.fact('the owner hears it back', (await heard).status() === 200)
          await settled(qa)
          await qa.shot('giong-mau-1')
          c.check('1-second sample', await a.form('/api/me/voice/samples', wavForm(audio(qa, 'short'), 'ngan.wav')), 422, 'MSG309')
          const textForm = new FormData()
          textForm.append('file', new Blob(['không phải âm thanh'], { type: 'text/plain' }), 'ghi-chu.txt')
          c.check('a text file', await a.form('/api/me/voice/samples', textForm), 415, 'MSG292')
          c.check('someone else plays it', await as(qa, 'member').get(`/api/me/voice/samples/${sample.id}/audio`), 404)
          c.check('someone else deletes it', await as(qa, 'member').delete(`/api/me/voice/samples/${sample.id}`), 404)
          c.check('without consent', await as(qa, 'b').form('/api/me/voice/samples', wavForm(audio(qa, 'enrol'), 'b.wav')), 409, 'MSG308')
          for (let i = 2; i <= 5; i++)
            c.check(`sample ${i}`, await a.form('/api/me/voice/samples', wavForm(audio(qa, 'enrol'), `mau-${i}.wav`)), 201, 'MSG307')
          c.check('sample 6', await a.form('/api/me/voice/samples', wavForm(audio(qa, 'enrol'), 'mau-6.wav')), 409, 'MSG310')
          await openMyVoice(qa)
          await qa.expect(qa.page.locator('.voice-count').first()).toHaveText('5/5')
          await qa.expect(qa.page.getByRole('button', { name: 'Ghi giọng mẫu' })).toBeDisabled()
          await settled(qa)
          await qa.shot('du-5-mau')
          await noRawCodes(qa)
          c.done()
        } finally {
          await withdrawn(qa, 'a')
        }
      },
    },

    // ------------------------------------------------------------------------------------------- UC-56e Delete
    {
      id: 'AC14',
      role: 'admin',
      devices: ['desktop', 'phone'],
      title: 'Xoá một giọng mẫu của mình (tự thu và Thư ký thu hộ): hệ thống hỏi xác nhận trước (UC-56e bước 2), rồi xoá (MSG311)',
      steps: [
        'qa-fe10-a@ có 1 mẫu tự thu và 1 mẫu Thư ký thu ở "[QA] FE-10 Họp thu giọng mẫu"',
        'Ở tab "Thư ký thu hộ", xem tên cuộc họp và người thu, bấm xoá mẫu thu hộ',
        'Ở tab "Tự thu", bấm "Xoá giọng mẫu 1"',
      ],
      expected:
        'Tab thu hộ nêu cuộc họp (liên kết) và người thu (Thư ký); mỗi lần xoá đều có hộp xác nhận trước; xong: toast "Đã xoá giọng mẫu.", mẫu biến mất, API không còn mẫu; cuối cùng rút lại',
      async run(qa) {
        const u = await users(qa)
        const b = await meeting(qa, 'B')
        const c = checker(qa)
        await enrolled(qa, 'a', 1)
        await clearCollected(qa, b.id, u.a.id)
        c.check('Secretary collects for A', await collect(qa, b.id, u.a.id), 201, 'MSG473')
        const asked = []
        try {
          await qa.signIn(u.a.email)
          await openMyVoice(qa, '?tab=proxy')
          const proxyItem = qa.page.locator('li.voice-sample').filter({ hasText: b.title })
          await qa.expect(proxyItem.getByRole('link', { name: b.title })).toBeVisible()
          await qa.expect(proxyItem.getByText(u.secretary.name)).toBeVisible()
          await settled(qa)
          await qa.shot('tab-thu-ho')
          for (const label of ['proxy', 'self']) {
            if (label === 'self') await qa.page.getByRole('tab', { name: /Tự thu/ }).click()
            const button =
              label === 'proxy'
                ? proxyItem.getByRole('button', { name: 'Xoá mẫu thu hộ 1' })
                : qa.page.getByRole('button', { name: 'Xoá giọng mẫu 1' })
            await button.click()
            const dialog = await qa.page
              .getByRole('dialog')
              .waitFor({ state: 'visible', timeout: 2000 })
              .then(() => true)
              .catch(() => false)
            asked.push(`${label}: ${dialog ? 'có hỏi' : 'không hỏi'}`)
            await settled(qa)
            await qa.shot(`xoa-${label}-${dialog ? 'co-hoi' : 'khong-hoi'}`)
            if (dialog) await qa.page.getByRole('dialog').getByRole('button', { name: /Xoá/ }).last().click()
            await qa.expect(toast(qa, MSG.MSG311).first()).toBeVisible()
          }
          await qa.expect(qa.page.locator('li.voice-sample')).toHaveCount(0)
          const v = await voice(qa, 'a')
          c.fact('both samples gone', v.samples.length === 0 && v.proxy.samples.length === 0)
          c.fact('asked before each delete (UC-56e step 2)', asked.every((x) => x.endsWith('có hỏi')), asked.join('; '))
          c.done()
        } finally {
          await clearCollected(qa, b.id, u.a.id)
          await withdrawn(qa, 'a')
        }
      },
    },

    // ------------------------------------------------------------------------------------------- UC-57a–d Collect
    {
      id: 'AC15',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Thư ký (có quyền "Thu giọng mẫu hộ") xem danh sách thu hộ, thu cho người đã đồng ý (MSG473), nghe lại, xoá có xác nhận (MSG474); người chưa đồng ý chỉ hiện "Chưa đồng ý", Khách không có trong danh sách',
      steps: [
        'qa-fe10-a@ đã đồng ý (1.3), qa-fe10-b@ chưa; thuky@ mở tab "Bản ghi" của "[QA] FE-10 Họp thu giọng mẫu" (Đã công bố)',
        'Bấm "Thu" ở dòng A (xem hộp thoại), đóng; API thu một mẫu cho A; tải lại, bấm nghe mẫu',
        'A: xem thông báo và tab "Thư ký thu hộ"; Thư ký bấm xoá mẫu, xác nhận',
      ],
      expected:
        'Thẻ "Giọng mẫu cho mic phòng" liệt kê người nội bộ trong danh sách, không có Khách mời; dòng A có "n/5" và nút "Thu"; dòng B "Chưa đồng ý", không nút; hộp thoại "Thu giọng mẫu: …" nhắc dùng micro phòng họp; API 201 MSG473; dòng A hiện "Mẫu 1", nghe được (200); A nhận thông báo VoiceSampleCollected, tab thu hộ có cuộc họp và tên Thư ký; xoá: hộp "Xoá mẫu thu hộ?", toast "Đã xoá giọng mẫu thu hộ.", A không còn mẫu thu hộ',
      async run(qa) {
        const u = await users(qa)
        const b = await meeting(qa, 'B')
        const c = checker(qa)
        const caps = await qa.capabilities('secretary')
        if (!caps.capabilities.includes('VoiceCollector')) qa.inconclusive('thuky@ thiếu quyền VoiceCollector: khôi phục vai trò Thư ký.')
        await withdrawn(qa, 'b')
        await enrolled(qa, 'a', 0)
        await clearCollected(qa, b.id, u.a.id)
        try {
          const list = (await as(qa, 'secretary').get(`/api/meetings/${b.id}/voice-collection`)).body
          c.fact('collection open while Published', list.canCollect === true)
          c.fact('guests are not listed', !list.people.some((p) => p.fullName === GUEST.invitedName || p.userId === b.guest?.id))
          c.fact('internal roster listed', [u.chair, u.secretary, u.member, u.a, u.b].every((x) => list.people.some((p) => p.userId === x.id)))
          await openRecordings(qa, b)
          const panel = collectionPanel(qa)
          await qa.expect(panel).toBeVisible()
          await qa.expect(panel.getByText(GUEST.invitedName)).toHaveCount(0)
          const rowA = personRow(qa, u.a.name)
          const rowB = personRow(qa, u.b.name)
          await qa.expect(rowA.getByText(/^\d\/5$/)).toBeVisible()
          await qa.expect(rowA.getByRole('button', { name: 'Thu', exact: true })).toBeVisible()
          await qa.expect(rowB.getByText('Chưa đồng ý')).toBeVisible()
          await qa.expect(rowB.getByRole('button', { name: 'Thu', exact: true })).toHaveCount(0)
          await panel.scrollIntoViewIfNeeded()
          await settled(qa)
          await qa.shot('danh-sach-thu-ho')
          await press(qa, rowA.getByRole('button', { name: 'Thu', exact: true }))
          const dialog = qa.page.getByRole('dialog', { name: `Thu giọng mẫu: ${u.a.name}` })
          await qa.expect(dialog).toBeVisible()
          await qa.expect(dialog.getByText(/micro phòng họp/)).toBeVisible()
          await settled(qa)
          await qa.shot('hop-thoai-thu-ho')
          await qa.page.keyboard.press('Escape')
          await qa.expect(dialog).toHaveCount(0)

          const res = await collect(qa, b.id, u.a.id)
          c.check('collect for A', res, 201, 'MSG473')
          const sampleId = res.body?.data?.id
          await openRecordings(qa, b)
          const item = personRow(qa, u.a.name).locator('li.voice-sample').filter({ hasText: 'Mẫu 1' })
          await qa.expect(item).toBeVisible()
          const heard = qa.page.waitForResponse((r) => r.url().includes(`/voice-collection/samples/${sampleId}/audio`))
          await press(qa, item.getByRole('button', { name: `Nghe lại mẫu 1 của ${u.a.name}` }))
          c.fact('the Secretary plays it back', (await heard).status() === 200)
          await item.scrollIntoViewIfNeeded()
          await settled(qa)
          await qa.shot('da-thu-mau-1')
          const notes = await as(qa, 'a').get('/api/notifications?page=1&pageSize=20')
          c.fact('A is notified', (notes.body.items ?? []).some((n) => n.type === 'VoiceSampleCollected'), JSON.stringify((notes.body.items ?? []).slice(0, 2)).slice(0, 200))
          const mine = await voice(qa, 'a')
          const proxy = mine.proxy.samples.find((s) => s.id === sampleId)
          c.fact('A sees where and by whom', proxy?.meetingTitle === b.title && proxy.collectedByName === u.secretary.name, JSON.stringify(proxy))

          await press(qa, item.getByRole('button', { name: `Xoá mẫu 1 của ${u.a.name}` }))
          const confirm = qa.page.getByRole('dialog', { name: 'Xoá mẫu thu hộ?' })
          await qa.expect(confirm).toBeVisible()
          await settled(qa)
          await qa.shot('xac-nhan-xoa-mau-thu-ho')
          await press(qa, confirm.getByRole('button', { name: 'Xoá', exact: true }))
          await qa.expect(toast(qa, MSG.MSG474)).toBeVisible()
          await qa.expect(personRow(qa, u.a.name).locator('li.voice-sample')).toHaveCount(0)
          await settled(qa)
          await qa.shot('da-xoa-mau-thu-ho')
          c.fact('A has no proxy sample left', (await voice(qa, 'a')).proxy.samples.length === 0)
          await noSidewaysScroll(qa)
          await noRawCodes(qa)
          c.done()
        } finally {
          await clearCollected(qa, b.id, u.a.id)
          await withdrawn(qa, 'a')
        }
      },
    },
    {
      id: 'AC16',
      role: 'secretary',
      title: 'Thu hộ bị chặn đúng chỗ: chưa đồng ý MSG475, đủ 5 mẫu MSG476, cuộc họp đã kết thúc MSG477; Mật / trực tuyến / người ngoài danh sách 404; Chủ toạ, Thành viên không có quyền',
      steps: [
        'API thu cho qa-fe10-b@ (chưa đồng ý); thu 5 mẫu cho qa-fe10-a@ rồi mẫu thứ 6',
        'Thư ký mở tab "Bản ghi" của "[QA] FE-10 Họp trực tiếp gỡ băng" (đã kết thúc); API thu cho A ở đó',
        'API: danh sách thu hộ của cuộc họp Mật, cuộc họp trực tuyến; thu cho quantri@ (ngoài danh sách); Chủ toạ, Thành viên, quantri@, chưa đăng nhập gọi danh sách; Thư ký nghe / xoá mẫu tự thu của A qua đường thu hộ',
      ],
      expected:
        'B: 409 MSG475; mẫu thứ 6: 409 MSG476 và dòng A "5/5" không nút "Thu"; cuộc họp kết thúc: thẻ vẫn hiện, ghi "Chỉ thu thêm được khi cuộc họp đã công bố hoặc đang họp.", không nút "Thu", API 409 MSG477; Mật 404, trực tuyến 404, ngoài danh sách 404; Chủ toạ 403, Thành viên 403 (thiếu quyền), quantri@ 404, ẩn danh 401; mẫu tự thu qua đường thu hộ 404; Chủ toạ không thấy thẻ thu hộ (tab Bản ghi của Chủ toạ khoá tới khi cuộc họp bắt đầu)',
      async run(qa) {
        const u = await users(qa)
        const [a, b, cm, d] = [await meeting(qa, 'A'), await meeting(qa, 'B'), await meeting(qa, 'C'), await meeting(qa, 'D')]
        const c = checker(qa)
        const caps = { chair: await qa.capabilities('chair'), member: await qa.capabilities('member') }
        await withdrawn(qa, 'b')
        const mine = await enrolled(qa, 'a', 1)
        await clearCollected(qa, b.id, u.a.id)
        const sec = as(qa, 'secretary')
        try {
          c.check('B has not agreed', await collect(qa, b.id, u.b.id), 409, 'MSG475')
          for (let i = 1; i <= 5; i++) c.check(`collect ${i}`, await collect(qa, b.id, u.a.id), 201, 'MSG473')
          c.check('collect 6', await collect(qa, b.id, u.a.id), 409, 'MSG476')
          await openRecordings(qa, b)
          await qa.expect(personRow(qa, u.a.name).locator('.voice-person-count')).toHaveText('5/5')
          await qa.expect(personRow(qa, u.a.name).getByRole('button', { name: 'Thu', exact: true })).toHaveCount(0)
          await personRow(qa, u.a.name).scrollIntoViewIfNeeded()
          await settled(qa)
          await qa.shot('du-5-mau-thu-ho')
          await clearCollected(qa, b.id, u.a.id)

          c.check('Ended meeting', await collect(qa, a.id, u.a.id), 409, 'MSG477')
          await openRecordings(qa, a)
          const panel = collectionPanel(qa)
          await qa.expect(panel).toBeVisible()
          await qa.expect(panel.getByText('Chỉ thu thêm được khi cuộc họp đã công bố hoặc đang họp.')).toBeVisible()
          await qa.expect(panel.getByRole('button', { name: 'Thu', exact: true })).toHaveCount(0)
          await panel.scrollIntoViewIfNeeded()
          await settled(qa)
          await qa.shot('hop-da-ket-thuc-khong-thu')

          c.check('Secret meeting', await sec.get(`/api/meetings/${d.id}/voice-collection`), 404)
          c.check('Online meeting', await sec.get(`/api/meetings/${cm.id}/voice-collection`), 404)
          c.check('person not on the roster', await collect(qa, b.id, u.admin.id), 404)
          const chairList = await as(qa, 'chair').get(`/api/meetings/${b.id}/voice-collection`)
          if (caps.chair.capabilities.includes('VoiceCollector')) qa.note('chutoa@ đang có VoiceCollector: bỏ qua kiểm 403 của Chủ toạ.')
          else c.check('Chair (no VoiceCollector)', chairList, 403)
          if (caps.member.capabilities.includes('VoiceCollector')) qa.note('thanhvien@ đang có VoiceCollector.')
          else c.check('Member', await as(qa, 'member').get(`/api/meetings/${b.id}/voice-collection`), 403)
          c.check('admin, not the Secretary', await as(qa, 'admin').get(`/api/meetings/${b.id}/voice-collection`), 404)
          c.check('anonymous', await as(qa, 'anon').get(`/api/meetings/${b.id}/voice-collection`), 401)
          const own = mine.samples[0].id
          c.check('self sample through the collection (play)', await sec.get(`/api/meetings/${b.id}/voice-collection/samples/${own}/audio`), 404)
          c.check('self sample through the collection (delete)', await sec.delete(`/api/meetings/${b.id}/voice-collection/samples/${own}`), 404)
          c.fact('the self sample is untouched', (await voice(qa, 'a')).samples.some((s) => s.id === own))

          await qa.signIn('chair')
          // PR #427: before the start the Bản ghi tab opens only to the Secretary collecting voice samples; for the
          // Chair it is locked ("Mở khi cuộc họp bắt đầu") and ?tab=recordings opens Thông tin chung.
          await qa.goto(`/mgmt/meetings/${b.id}?tab=recordings`)
          await qa.expect(qa.page.getByRole('heading', { level: 1, name: b.title })).toBeVisible({ timeout: 15_000 })
          const tab = qa.page.getByRole('tab', { name: 'Bản ghi' })
          await qa.expect(tab).toHaveAttribute('aria-disabled', 'true')
          await qa.expect(tab).toHaveAccessibleDescription('Mở khi cuộc họp bắt đầu')
          await qa.expect(qa.page.getByRole('tab', { name: 'Thông tin chung' })).toHaveAttribute('aria-selected', 'true')
          await qa.expect(recordingsRegion(qa)).toHaveCount(0)
          await qa.expect(collectionPanel(qa)).toHaveCount(0)
          await settled(qa)
          await qa.shot('chu-toa-khong-co-the-thu-ho')
          c.done()
        } finally {
          await clearCollected(qa, b.id, u.a.id)
          await withdrawn(qa, 'a')
        }
      },
    },

    // ------------------------------------------------------------------------------------------- UC-56f Withdraw
    {
      id: 'AC17',
      role: 'admin',
      devices: ['desktop', 'phone'],
      title: 'Rút đồng ý Voice ID xoá ngay mọi giọng mẫu cả hai bộ (MSG306), Thư ký không còn thấy mẫu thu hộ, bản ghi mẫu không còn phát được (Luật 91/2025)',
      steps: [
        'qa-fe10-a@ có 1 mẫu tự thu và 1 mẫu Thư ký thu; mở "Giọng nói của tôi", bấm "Tắt và xoá giọng mẫu"',
        'Đọc hộp xác nhận, bấm "Tắt và xoá"',
        'API: hồ sơ Voice ID của A; Thư ký xem danh sách thu hộ và nghe mẫu cũ; A nghe mẫu cũ',
      ],
      expected:
        'Hộp "Tắt nhận diện giọng nói?" nói rõ 1 giọng mẫu tự thu và 1 mẫu Thư ký thu hộ bị xoá, tên Thư ký đã gán giữ nguyên; toast "Đã tắt nhận diện giọng nói và xoá toàn bộ giọng mẫu."; trang về thẻ "Bật nhận diện giọng nói" với ô chưa đánh; API: consent null, samples [] và proxy.samples []; Thư ký: A "Chưa đồng ý", không mẫu, nghe mẫu cũ 404; A nghe mẫu cũ 404',
      async run(qa) {
        const u = await users(qa)
        const b = await meeting(qa, 'B')
        const c = checker(qa)
        const before = await enrolled(qa, 'a', 1)
        await clearCollected(qa, b.id, u.a.id)
        const collected = await collect(qa, b.id, u.a.id)
        qa.expectStatus(collected, 201)
        const selfId = before.samples[0].id
        const proxyId = collected.body.data.id
        try {
          await qa.signIn(u.a.email)
          await openMyVoice(qa)
          await press(qa, qa.page.getByRole('button', { name: 'Tắt và xoá giọng mẫu' }))
          const dialog = qa.page.getByRole('dialog', { name: 'Tắt nhận diện giọng nói?' })
          await qa.expect(dialog).toBeVisible()
          await qa.expect(dialog.getByText(/1 giọng mẫu tự thu và 1 mẫu Thư ký thu hộ/)).toBeVisible()
          await qa.expect(dialog.getByText(/Tên Thư ký đã gán giữ nguyên/)).toBeVisible()
          await settled(qa)
          await qa.shot('xac-nhan-rut-dong-y')
          await press(qa, dialog.getByRole('button', { name: 'Tắt và xoá' }))
          await qa.expect(toast(qa, MSG.MSG306)).toBeVisible()
          const card = qa.page.getByRole('region', { name: 'Bật nhận diện giọng nói' })
          await qa.expect(card).toBeVisible()
          await qa.expect(card.getByRole('checkbox')).not.toBeChecked()
          await settled(qa)
          await qa.shot('da-rut-dong-y')
          await noSidewaysScroll(qa)
          await noRawCodes(qa)
          const v = await voice(qa, 'a')
          c.fact('consent gone', v.consent === null)
          c.fact('both sets erased', v.samples.length === 0 && v.proxy.samples.length === 0, JSON.stringify({ s: v.samples.length, p: v.proxy.samples.length }))
          const list = (await as(qa, 'secretary').get(`/api/meetings/${b.id}/voice-collection`)).body
          const person = list.people.find((p) => p.userId === u.a.id)
          c.fact('the Secretary sees "not agreed" and no sample', person?.consented === false && person.samples.length === 0 && person.sampleCount === 0, JSON.stringify(person))
          c.check('Secretary plays the old proxy sample', await as(qa, 'secretary').get(`/api/meetings/${b.id}/voice-collection/samples/${proxyId}/audio`), 404)
          c.check('A plays the old self sample', await as(qa, 'a').get(`/api/me/voice/samples/${selfId}/audio`), 404)
          c.check('A plays the old proxy sample', await as(qa, 'a').get(`/api/me/voice/samples/${proxyId}/audio`), 404)
          c.done()
        } finally {
          await clearCollected(qa, b.id, u.a.id)
          await withdrawn(qa, 'a')
        }
      },
    },

    // ------------------------------------------------------------------------------------------- UC-54c + Voice ID
    {
      id: 'AC18',
      role: 'secretary',
      title: 'Chuyển chữ lại (hỏi trước, MSG300) so giọng với người đã đồng ý trong cuộc họp: chỉ Thư ký thấy gợi ý "Khớp giọng nhất", giọng khớp rõ được tự gán; người đó rút đồng ý thì gợi ý và tên máy gán bị xoá',
      steps: [
        'qa-fe10-b@ đồng ý 1.3 và ghi 2 giọng mẫu bằng giọng của người nói thứ hai trong bản ghi (giọng Linh hạ tông; giọng Linh gốc không dùng vì thuky@ đã có giọng mẫu nghe như nó, nên máy không phân định được người)',
        'Thư ký mở bản ghi "fe10-voice-id.wav", bấm "Chuyển thành chữ lại", đọc cảnh báo, bấm "Chuyển lại"; chờ xong',
        'Thư ký mở ô tên của giọng nói câu "Tôi đề nghị…"; Thành viên đọc bản gỡ băng (API)',
        'qa-fe10-b@ rút đồng ý; Thư ký đọc lại bản gỡ băng',
      ],
      expected:
        'Hộp "Chuyển thành chữ lại?" cảnh báo chữ đã sửa và tên đã gán sẽ bị thay; toast "Đã đưa bản ghi vào hàng chờ chuyển thành chữ."; khi xong giọng của "Tôi đề nghị…" có gợi ý là B (và được tự gán B, autoAssigned=true, vì từ 22/11 mọi gợi ý đều được tự gán); ô chọn đưa B lên đầu với nhãn "Khớp giọng nhất"; Thành viên: mọi suggested* null (tên tự gán thì vẫn đọc được); không giọng nào gợi ý Khách; rút đồng ý 200 MSG306, sau đó không giọng nào gợi ý hay được máy gán tên B (BR-117)',
      async run(qa) {
        const m = await meeting(qa, 'A')
        const u = await users(qa)
        const c = checker(qa)
        await clearNames(qa, m.voice.id)
        await enrolled(qa, 'b', 2, 'enrol2')
        try {
          await openRecordings(qa, m)
          const row = recordingRow(qa, FILES.voice)
          const redo = row.getByRole('button', { name: 'Chuyển thành chữ lại' })
          await redo.scrollIntoViewIfNeeded()
          await redo.click()
          const dialog = qa.page.getByRole('dialog', { name: 'Chuyển thành chữ lại?' })
          await qa.expect(dialog.getByText(/chữ đã sửa và tên đã gán/)).toBeVisible()
          await settled(qa)
          await qa.shot('canh-bao-chuyen-chu-lai')
          await dialog.getByRole('button', { name: 'Chuyển lại' }).click()
          await qa.expect(toast(qa, MSG.MSG300)).toBeVisible()
          await waitTranscript(qa, m.voice.id)
          const t = await transcript(qa, 'secretary', m.voice.id)
          const { speaker } = segmentOf(t, /đề nghị|kinh phí/i)
          const firstVoice = segmentOf(t, /Kính thưa|ngân sách/i).speaker
          qa.note(`Giọng Linh gốc ("Kính thưa…"): gợi ý ${firstVoice?.suggestedName ?? 'không có'}, tên ${firstVoice?.assignedName ?? 'chưa gán'}`)
          qa.note(`Giọng của "Tôi đề nghị…": ${JSON.stringify({ label: speaker?.label, suggested: speaker?.suggestedName, score: speaker?.suggestionScore, assigned: speaker?.assignedName, auto: speaker?.autoAssigned })}`)
          c.fact('B is suggested or named for the first voice', speaker?.suggestedUserId === u.b.id || (speaker?.assignedUserId === u.b.id && speaker.autoAssigned), JSON.stringify(speaker))
          c.fact('no guest is ever suggested', t.speakers.every((s) => s.suggestedUserId !== m.guest?.id))
          const asMember = await transcript(qa, 'member', m.voice.id)
          c.fact('a Member sees no suggestion', asMember.speakers.every((s) => s.suggestedUserId == null && s.suggestedName == null))
          if (speaker?.autoAssigned)
            c.fact('a Member reads the name the machine filled in', asMember.speakers.find((s) => s.label === speaker.label)?.assignedName === u.b.name)

          await openRecordings(qa, m)
          const row2 = recordingRow(qa, FILES.voice)
          const trigger = row2.locator('aside.transcript-speakers').getByRole('button', { name: new RegExp(`^Tên của (${esc(speakerName(speaker.label))}|${esc(u.b.name)})`) })
          await trigger.scrollIntoViewIfNeeded()
          await trigger.click()
          const list = qa.page.getByRole('listbox', { name: /^Tên của/ })
          await qa.expect(list.getByRole('option').first()).toContainText(u.b.name)
          await qa.expect(list.getByRole('option').first()).toContainText('Khớp giọng nhất')
          await settled(qa)
          await qa.shot('goi-y-khop-giong-nhat')
          await qa.page.keyboard.press('Escape')

          c.check('B withdraws', await as(qa, 'b').delete('/api/me/voice/consent'), 200, 'MSG306')
          const after = await transcript(qa, 'secretary', m.voice.id)
          c.fact('no suggestion points to B any more', after.speakers.every((s) => s.suggestedUserId !== u.b.id), JSON.stringify(after.speakers.map((s) => [s.label, s.suggestedName, s.assignedName])))
          c.fact('no machine-filled name of B is left', after.speakers.every((s) => !(s.assignedUserId === u.b.id && s.autoAssigned)))
          if (speaker?.autoAssigned)
            c.fact('the voice reads "Người nói N" again', after.speakers.find((s) => s.label === speaker.label)?.assignedUserId == null)
          await openRecordings(qa, m)
          await recordingRow(qa, FILES.voice).scrollIntoViewIfNeeded()
          await settled(qa)
          await qa.shot('sau-khi-rut-dong-y')
          c.done()
        } finally {
          await withdrawn(qa, 'b')
          await clearNames(qa, m.voice.id)
        }
      },
    },

    // ------------------------------------------------------------------------------------------- UC-59 readiness
    {
      id: 'AC19',
      role: 'chair',
      title: 'Thẻ dự thảo (chỉ đọc, không tạo dự thảo AI) đếm giọng chưa có tên: gán tên một giọng thì số giảm 1 và mốc "lời thoại đổi" mới hơn; Thành viên không thấy phần chuẩn bị',
      steps: [
        'API GET /api/meetings/{id}/summary của "[QA] FE-10 Họp trực tiếp gỡ băng" (Chủ toạ, Thư ký, Thành viên, quantri@)',
        'Thư ký gán tên một giọng chưa có tên của bản ghi chính; Chủ toạ đọc lại; bỏ gán',
      ],
      expected:
        'Chủ toạ / Thư ký: canManage true, readiness có unnamedVoices; Thành viên: readiness null, canManage false, không thấy dự thảo chưa duyệt; quantri@ 404; sau khi gán: unnamedVoices giảm 1 và transcriptChangedAt muộn hơn; bỏ gán thì trở lại',
      async run(qa) {
        const m = await meeting(qa, 'A')
        const u = await users(qa)
        const c = checker(qa)
        await clearNames(qa, m.main.id)
        await clearNames(qa, m.voice.id)
        const read = async (who) => (await as(qa, who).get(`/api/meetings/${m.id}/summary`)).body
        const chair = await read('chair')
        c.fact('Chair manages and sees readiness', chair.canManage === true && chair.readiness && typeof chair.readiness.unnamedVoices === 'number', JSON.stringify(chair).slice(0, 300))
        c.fact('Secretary sees readiness', (await read('secretary')).readiness != null)
        const member = await read('member')
        c.fact('Member sees no readiness and no unreviewed draft', member.readiness == null && member.canManage === false && (member.summary == null || member.summary.reviewedAt != null), JSON.stringify(member).slice(0, 300))
        c.check('outsider', await as(qa, 'admin').get(`/api/meetings/${m.id}/summary`), 404)
        const t = await transcript(qa, 'secretary', m.main.id)
        const label = t.speakers[0].label
        try {
          c.check('name one voice', await as(qa, 'secretary').put(`/api/recordings/${m.main.id}/speakers/${label}`, { userId: u.member.id, guestId: null }), 200, 'MSG316')
          const named = await read('chair')
          c.fact('one fewer unnamed voice', named.readiness.unnamedVoices === chair.readiness.unnamedVoices - 1, `${chair.readiness.unnamedVoices} → ${named.readiness.unnamedVoices}`)
          c.fact('transcript changed later', !chair.readiness.transcriptChangedAt || Date.parse(named.readiness.transcriptChangedAt) > Date.parse(chair.readiness.transcriptChangedAt), `${chair.readiness.transcriptChangedAt} → ${named.readiness.transcriptChangedAt}`)
          await as(qa, 'secretary').put(`/api/recordings/${m.main.id}/speakers/${label}`, { userId: null, guestId: null })
          c.fact('back to the count', (await read('chair')).readiness.unnamedVoices === chair.readiness.unnamedVoices)
          c.done()
        } finally {
          await clearNames(qa, m.main.id)
        }
      },
    },
  ],
}

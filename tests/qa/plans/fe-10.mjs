// FE-10 — Gỡ băng, nhận diện người nói và tóm tắt (QA subtask SCRUM-197): UC-45a view a transcript, UC-45b export it,
// UC-45c transcribe (automatic after an upload, rerun), UC-46a edit a sentence, UC-46b name a voice and change the
// speaker of one sentence ("chỉ câu này", MSG407), UC-46c exclude / restore a per-mic sentence (MSG351–354), UC-81a–f
// the person's own Voice ID (consent, samples, withdrawal), UC-86a–d the Secretary collecting samples on someone's behalf
// (ADR-0060, MSG473–477), and of UC-47–49 only what FE-10 feeds: the draft card's count of unnamed voices (read-only —
// this plan never asks for an AI draft; the draft itself is FE-12).
//
// Criteria come from docs/01-requirements/05-use-case-specifications.md, 06-business-rules.md (BR-341, BR-350–BR-368,
// BR-372, BR-395, BR-448–BR-454), docs/03-api/12-ban-ghi-cuoc-hop.md, 13-nhan-dien-giong-noi.md, 17-du-thao-bien-ban.md,
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
//   | Quản trị (quantri@, ngoài DS)| bản ghi, bản gỡ băng, câu theo micro      | 404                                    |
//   |                               | thu hộ (thiếu VoiceCollector)      | 403 (đúng luật capability)             |
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
// Test audio is synthetic: Vietnamese speech recorded for QA (`qa-fixtures/`, no real meeting content) and the same
// voice pitched down as a second speaker, converted to 16 kHz WAV with ffmpeg, written to QA_AUDIO_DIR (default:
// <tmp>/trustcabinet-qa-fe10). No recording of a real person is used.

import { execFileSync } from 'node:child_process'
import { randomBytes, randomUUID } from 'node:crypto'
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

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
  E: 'FE-10 Họp trực tuyến trống câu',
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
  MSG384: 'Đã gửi yêu cầu. Dự thảo sẽ tự hiện khi AI viết xong.',
  MSG388: 'Đã lưu dự thảo.',
  MSG391: 'Đã duyệt dự thảo.',
  MSG407: 'Đã đổi người nói của câu này.',
  MSG408: 'Đã lưu cấu hình AI.',
  MSG473: 'Đã lưu giọng mẫu thu hộ.',
  MSG474: 'Đã xoá giọng mẫu thu hộ.',
}

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..')

/** Stops the local AI server (ollama) so the next draft fails; the QA lead's "stop the AI server" step. */
function stopOllama() {
  try {
    execFileSync('taskkill', ['/IM', 'ollama.exe', '/F'], { stdio: 'ignore' })
  } catch { /* already stopped */ }
}
/** Starts the local AI server again and waits until it answers. */
async function startOllama(qa, timeoutMs = 120_000) {
  execFileSync(
    'powershell',
    ['-NoProfile', '-Command', "$env:OLLAMA_HOST='127.0.0.1:11434'; Start-Process -FilePath 'C:\\Users\\admin\\AppData\\Local\\Programs\\Ollama\\ollama.exe' -ArgumentList 'serve'"],
    { stdio: 'ignore' },
  )
  const until = Date.now() + timeoutMs
  for (;;) {
    try {
      const res = await fetch('http://localhost:11434/api/tags')
      if (res.ok) return
    } catch { /* not up yet */ }
    if (Date.now() > until) qa.inconclusive('Máy AI local không khởi động lại sau 120 giây.')
    await new Promise((r) => setTimeout(r, 3000))
  }
}
/** PID holding the QA AI stub (:11439), or NaN. */
function stubPid() {
  try {
    const out = execFileSync(
      'powershell',
      ['-NoProfile', '-Command', "Get-NetTCPConnection -LocalPort 11439 -State Listen -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess | Select-Object -Unique | Select-Object -First 1"],
      { encoding: 'utf8' },
    )
    const n = Number(String(out).trim().split(/\s+/)[0])
    return Number.isFinite(n) && n > 0 ? n : NaN
  } catch {
    return NaN
  }
}
/** Starts the QA OpenAI-compatible stub (trusted per-user HTTPS cert) and waits for it. */
async function startStub(qa, timeoutMs = 60_000) {
  if (Number.isFinite(stubPid())) return
  execFileSync(
    'powershell',
    ['-NoProfile', '-Command', `Start-Process -FilePath 'node' -ArgumentList 'ai-stub.mjs' -WorkingDirectory '${join(ROOT, 'tests', 'qa')}'`],
    { stdio: 'ignore' },
  )
  const until = Date.now() + timeoutMs
  for (;;) {
    try {
      execFileSync('curl.exe', ['-k', '-s', '-o', 'NUL', '-w', '%{http_code}', 'https://127.0.0.1:11439/chat/completions'], { encoding: 'utf8', timeout: 10000 })
      if (Number.isFinite(stubPid())) return
    } catch { /* not up yet */ }
    if (Date.now() > until) qa.inconclusive('Stub API QA không mở được sau 60 giây.')
    await new Promise((r) => setTimeout(r, 2000))
  }
}
/** Stops the QA AI stub. */
function stopStub() {
  const pid = stubPid()
  if (!Number.isFinite(pid)) return
  try {
    execFileSync('taskkill', ['/PID', String(pid), '/T', '/F'], { stdio: 'ignore' })
  } catch { /* already stopped */ }
}
/** PID holding :5225, or NaN. A port can list several connections: take the first owner. */
function backendPid() {
  try {
    const out = execFileSync(
      'powershell',
      ['-NoProfile', '-Command', "Get-NetTCPConnection -LocalPort 5225 -State Listen -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess | Select-Object -Unique | Select-Object -First 1"],
      { encoding: 'utf8' },
    )
    const n = Number(String(out).trim().split(/\s+/)[0])
    return Number.isFinite(n) && n > 0 ? n : NaN
  } catch {
    return NaN
  }
}
/** Restarts the backend (a config change needs it: IOptions snapshot) and waits for health. */
async function restartBackend(qa, timeoutMs = 180_000) {
  const oldPid = backendPid()
  if (Number.isFinite(oldPid)) {
    try {
      execFileSync('taskkill', ['/PID', String(oldPid), '/T', '/F'], { stdio: 'ignore' })
    } catch { /* already stopped */ }
  }
  let freeUntil = Date.now() + 60_000
  for (;;) {
    const p = backendPid()
    if (!Number.isFinite(p) || p !== oldPid) break
    if (Date.now() > freeUntil) qa.inconclusive('Backend cũ trên :5225 không dừng sau 60 giây.')
    await new Promise((r) => setTimeout(r, 2000))
  }
  const env = readFileSync(join(ROOT, '..', 'TrustCabinet', '.env'), 'utf8')
  const pw = /^POSTGRES_PASSWORD=(.*)$/m.exec(env)?.[1]?.trim()
  if (!pw) qa.inconclusive('Không đọc được POSTGRES_PASSWORD để khởi động lại backend.')
  execFileSync(
    'powershell',
    [
      '-NoProfile',
      '-Command',
      `$env:ConnectionStrings__TrustCabinet='Host=localhost;Port=55620;Database=trustcabinet_round1;Username=trustcabinet;Password=${pw}'; ` +
        `$env:ASPNETCORE_ENVIRONMENT='Development'; $env:ASPNETCORE_URLS='http://localhost:5225'; ` +
        `Start-Process -FilePath 'dotnet' -ArgumentList 'run','--project','backend/TrustCabinet.Web','--no-launch-profile' -WorkingDirectory '${ROOT}'`,
    ],
    { stdio: 'ignore' },
  )
  const until = Date.now() + timeoutMs
  let newPid = NaN
  for (;;) {
    try {
      const res = await fetch(`${qa.base}/api/health`)
      newPid = backendPid()
      if (res.ok && Number.isFinite(newPid) && newPid !== oldPid) return
    } catch { /* not up yet */ }
    if (Date.now() > until) qa.inconclusive(`Backend không khởi động lại sau ${timeoutMs / 1000} giây (pid cũ ${oldPid}, pid mới ${newPid}).`)
    await new Promise((r) => setTimeout(r, 5000))
  }
}

// ---------------------------------------------------------------------------------------------------------------------
// Audio: synthesized on any machine with ffmpeg from `qa-fixtures/` (real Vietnamese speech recorded for QA,
// R1 24/11/2026 — the macOS-only `say -v Linh` pipeline is gone): p0 "Kính thưa các đồng chí.", p1 "Hôm nay chúng ta
// họp…" (pitched down as the second speaker), t "Xin chào.", silence 5 s. meeting.wav diarizes into two voices.

const QA_SRC = join(dirname(fileURLToPath(import.meta.url)), '..', '..', 'qa-fixtures')

const AUDIO_DIR = process.env.QA_AUDIO_DIR ?? join(tmpdir(), 'trustcabinet-qa-fe10')
// What each fixture says (for the matchers below): s1a "Kính thưa các đồng chí.", s1b and s2a "Hôm nay chúng ta
// họp để thông qua kế hoạch đào tạo cán bộ…", s2a pitched down as the second voice, short "Xin chào.".
function ff(args) {
  execFileSync('ffmpeg', ['-y', '-loglevel', 'error', ...args])
}
function buildAudio() {
  mkdirSync(AUDIO_DIR, { recursive: true })
  const p = (n) => join(AUDIO_DIR, n)
  const q = (n) => join(QA_SRC, n)
  const conv = (src, dst, filter = []) => {
    if (existsSync(p(dst))) return
    ff(['-i', q(src), ...filter, '-ar', '16000', '-ac', '1', '-c:a', 'pcm_s16le', p(dst)])
  }
  conv('p0.mp3', 's1a.wav')
  conv('p1.mp3', 's1b.wav')
  // The second speaker: the same voice pitched down, tempo kept.
  const low = ['-af', 'asetrate=22050*0.74,aresample=22050,atempo=1.35']
  conv('p1.mp3', 's2a.wav', low)
  // The second speaker's own enrolment reading, pitched like the second speaker.
  conv('said.wav', 'enrol.wav')
  conv('said.wav', 'enrol2.wav', low)
  conv('t.mp3', 'short.wav')
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
    qa.inconclusive(`Không tạo được âm thanh thử (cần ffmpeg và qa-fixtures/): ${error.message}`)
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
/** Voice samples for the API: opus when the machine has it, else wav.
 * R1/FE-10 (Windows 22/11/2026): this laptop's backend cannot spool a multipart section over 64 KB
 * to a temp file (500 UnauthorizedAccessException on ASPNETCORE_*.tmp inside FileBufferingReadStream —
 * the same family as the FE-12 QuestPDF temp failure), so the QA audio script also writes small
 * opus files and the plan prefers them; the product accepts audio/ogg (RecordingRules.Extensions),
 * and machines with a working temp dir keep using wav exactly as before. */
const sampleForm = (qa, name, fileName) => {
  const opus = join(AUDIO_DIR, `${name}.opus`)
  if (existsSync(opus) && readFileSync(opus).length < 65536) {
    const form = new FormData()
    form.append('file', new Blob([readFileSync(opus)], { type: 'audio/ogg' }), fileName)
    return form
  }
  return wavForm(audio(qa, name), fileName)
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
const HOURS = { A: 8, B: 10, C: 14, D: 16, E: 12 }
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
  E: { mode: 'Online', sensitivity: 'Normal', status: 'InProgress', guest: false, extra: ['a'] },
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
    const res = await as(qa, who).form('/api/me/voice/samples', sampleForm(qa, reading, 'giong-mau.wav'))
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
/** ADR-0075 — the collector path: a holder of VoiceCollector records one person, no meeting. */
async function personCollection(qa, userId) {
  const res = await as(qa, 'admin').get(`/api/users/${userId}/voice-collection`)
  qa.expectStatus(res, 200)
  return res.body
}
/** Records a proxy sample for `userId` (the collector confirms the person agreed to the take). */
async function collectFor(qa, userId) {
  const form = sampleForm(qa, 'enrol', 'thu-ho.wav')
  form.append('personAgreed', 'true')
  const res = await as(qa, 'admin').form(`/api/users/${userId}/voice-collection/samples`, form)
  if (res.status === 503) qa.inconclusive('Máy nhận dạng giọng nói chưa sẵn sàng (503 MSG312).')
  return res
}
/** Deletes every proxy sample kept for `userId`. */
async function clearPersonSamples(qa, userId) {
  const admin = as(qa, 'admin')
  const col = await admin.get(`/api/users/${userId}/voice-collection`)
  for (const s of col.body?.samples ?? [])
    await admin.delete(`/api/users/${userId}/voice-collection/samples/${s.id}`)
}
/** Sets the standing consent of `userId` back to an older notice (precondition only reachable in the DB). */
function backdateConsent(userId, noticeVersion) {
  execFileSync('docker', ['exec', 'trustcabinet-postgres', 'psql', '-U', 'trustcabinet', '-d', 'trustcabinet_round1',
    '-c', `UPDATE voice_consents SET notice_version = '${noticeVersion}' WHERE user_id = '${userId}' AND withdrawn_at IS NULL`],
    { stdio: 'ignore' })
}
/** Opens the user's voice-collection dialog from the user list. */
async function openCollection(qa, fullName) {
  await qa.goto('/mgmt/admin/users')
  await qa.expect(qa.page.getByRole('heading', { level: 1, name: 'Người dùng' })).toBeVisible({ timeout: 15_000 })
  const row = qa.page.locator('tr').filter({ hasText: fullName }).first()
  await press(qa, row.getByRole('button', { name: `Quản lý giọng nói thu hộ của ${fullName}` }))
  const dialog = qa.page.getByRole('dialog', { name: `Giọng nói thu hộ: ${fullName}` })
  await qa.expect(dialog).toBeVisible()
  return dialog
}
async function collect(qa, meetingId, userId) {
  const res = await as(qa, 'secretary').form(
    `/api/meetings/${meetingId}/voice-collection/${userId}/samples`,
    sampleForm(qa, 'enrol', 'thu-ho.wav'),
  )
  if (res.status === 503) qa.inconclusive('Máy nhận dạng giọng nói chưa sẵn sàng (503 MSG312).')
  return res
}

// ---------------------------------------------------------------------------------------------------------------------
/** A recording whose bytes no decoder reads: transcribing it must fail, never hang. */
const garbageAudio = () => randomBytes(20000)
/** Polls until the transcript fails (waitTranscript only waits for Completed). */
async function waitFailed(qa, recordingId, timeoutMs = 180_000) {
  const until = Date.now() + timeoutMs
  for (;;) {
    const t = await transcript(qa, 'secretary', recordingId)
    if (t.status === 'Failed') return t
    if (t.status === 'Completed') qa.expect('Completed', 'a garbage recording to fail, not complete').toBe('Failed')
    if (Date.now() > until) qa.expect('still running', 'a garbage transcript to fail').toBe('Failed')
    await new Promise((r) => setTimeout(r, 3000))
  }
}
/** Uploads a garbage recording to an Ended meeting and waits until its transcript fails. */
async function ensureFailed(qa, meetingId, tag) {
  const sec = as(qa, 'secretary')
  const fileName = `fe10-hong-${tag}-${Date.now().toString(36)}.wav`
  const up = await sec.form(
    `/api/meetings/${meetingId}/recordings`,
    wavForm(garbageAudio(), fileName, { consentConfirmed: 'true', consentPurpose: 'Lập biên bản cuộc họp (QA FE-10)' }),
  )
  qa.expectStatus(up, 201)
  const list = await sec.get(`/api/meetings/${meetingId}/recordings`)
  const id = list.body.items.find((r) => r.fileName === fileName)?.id
  qa.expect(id, 'the garbage recording is listed').toBeTruthy()
  await waitFailed(qa, id)
  return { fileName, id }
}

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
/** A blocked (aria-disabled) button: Playwright treats it as disabled, so fire the click directly. */
async function pressBlocked(qa, locator) {
  await locator.dispatchEvent('click')
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
    // ------------------------------------------------------------------------------------------- UC-45a View
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
        'Dòng bản ghi có chip "Có bản gỡ băng"; bản gỡ băng có nhãn "Máy nhận dạng", mỗi câu có mốc giờ và tên người nói (Người nói N hoặc tên đã gán), cột "Người nói" có ít nhất 2 giọng; Thư ký có ô chọn tên và nút sửa; số câu trên màn bằng số câu API trả; tải lại trang vẫn hiện đủ (TRANS_FT12 E1); không cuộn ngang, không lộ mã MSG',
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
        // R1/FE-10 (22/11/2026): TRANS_FT12 E1 — tải lại trang, bản gỡ băng còn nguyên.
        await qa.page.reload()
        await qa.expect(qa.page.getByRole('heading', { level: 1, name: m.title })).toBeVisible({ timeout: 15_000 })
        const again = await transcript(qa, 'secretary', m.main.id)
        qa.expect(again.segments.length, 'the transcript changed over a reload').toBe(t.segments.length)
        await qa.expect(lines(recordingRow(qa, FILES.main)).locator('.tl-say')).toHaveCount(t.segments.length)
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

    // ------------------------------------------------------------------------------------------- UC-45c Transcribe
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
        'Hộp xác nhận thông báo ghi âm trước khi lưu, nút "Tải lên" khoá tới khi đánh dấu; dòng mới có chip "Đang chuyển chữ" và dòng trạng thái "Đang chờ/Đang chuyển thành chữ…"; API: 409 MSG301 và 409 MSG357; khi xong chip "Có bản gỡ băng" và lời thoại hiện ra (UC-45c AF-1, UC-45a AF-1, UC-45b EX-3); tải lại trang vẫn hiện đủ (TRANS_FT13 E2)',
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
          // R1/FE-10 (22/11/2026): TRANS_FT13 E2 — tải lại trang vẫn thấy bản gỡ băng đã xong.
          await qa.page.reload()
          await qa.expect(qa.page.getByRole('heading', { level: 1, name: m.title })).toBeVisible({ timeout: 15_000 })
          await openRecordings(qa, m)
          const rowAgain = recordingRow(qa, fileName)
          await qa.expect(rowAgain.getByText('Có bản gỡ băng')).toBeVisible({ timeout: 30_000 })
          await qa.expect(lines(rowAgain).locator('.tl-sentence').first()).toBeVisible()
          await rowAgain.scrollIntoViewIfNeeded()
          await settled(qa)
          await qa.shot('tai-lai-van-thay-ban-go-bang')
          await noRawCodes(qa)
          c.done()
        } finally {
          if (createdId) await as(qa, 'secretary').delete(`/api/recordings/${createdId}`)
        }
      },
    },

    // ------------------------------------------------------------------------------------------- UC-45a as Member
    {
      id: 'AC3',
      role: 'member',
      devices: ['desktop', 'phone'],
      title: 'Đọc bản gỡ băng theo vai trò: Thành viên và Chủ toạ đều không thấy gợi ý, nút sửa / gán / chạy lại; API từ chối mọi thao tác ghi',
      steps: [
        'Đăng nhập thanhvien@, mở tab "Bản ghi" của "[QA] FE-10 Họp trực tiếp gỡ băng"',
        'Đăng nhập chutoa@, mở cùng thẻ',
        'API (thanhvien@): GET transcript, transcript.txt, PUT sửa câu, PUT gán tên, PUT đổi người nói một câu, POST transcribe; API (chutoa@): POST transcribe',
      ],
      expected:
        'Thành viên: lời thoại và tên hiện như chữ thường (không ô chọn), không nút "Sửa nội dung", "Tải bản gỡ băng (.txt)", "Chuyển thành chữ lại", "Tải tệp lên"; Chủ toạ: cũng không có các nút sửa/gán/chạy lại (được tải .txt); API: canEdit/canExport false, people [], suggested* null (BR-366); .txt 403 MSG355; sửa / gán / đổi 403 MSG295; transcribe 403 (Member và Chair)',
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
        // R1/FE-10 (22/11/2026): TRANS_FT18 — Chủ toạ cũng không thấy các nút sửa/gán/chạy lại.
        const rerunChair = await as(qa, 'chair').post(`/api/recordings/${m.main.id}/transcribe`)
        c.fact('transcribe as Chair is refused', rerunChair.status === 403, `got ${rerunChair.status} ${rerunChair.text.slice(0, 120)}`)
        await qa.signIn('chair')
        await openRecordings(qa, m)
        const chairRow = recordingRow(qa, FILES.main)
        await qa.expect(lines(chairRow).locator('.tl-say')).toHaveCount(t.segments.length)
        await qa.expect(chairRow.getByRole('button', { name: /^Tên của/ })).toHaveCount(0)
        await qa.expect(chairRow.getByRole('button', { name: 'Sửa nội dung' })).toHaveCount(0)
        await qa.expect(chairRow.getByRole('button', { name: 'Chuyển thành chữ lại' })).toHaveCount(0)
        await qa.expect(recordingsRegion(qa).getByRole('button', { name: 'Tải tệp lên' })).toHaveCount(0)
        await chairRow.scrollIntoViewIfNeeded()
        await settled(qa)
        await qa.shot('chu-toa-khong-nut-sua')
        c.done()
      },
    },

    // ------------------------------------------------------------------------------------------- UC-46a Edit text
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
        'Toast "Đã lưu đoạn gỡ băng."; câu hiện chữ mới kèm "đã sửa"; API: editedByName là Thư ký, editedAt có giá trị; 400 MSG303 hai lần; Chủ toạ 403 MSG295; quantri@ 404; chữ cũ được trả lại sau khi kiểm; Esc và "Huỷ" giữ nguyên câu không gửi gì (TRANS_FT22); ô rỗng khoá nút Lưu, Enter không gửi (TRANS_FT23); ô nhập tối đa 8000 ký tự, lưu đúng 8000 ký tự vẫn đạt (TRANS_FT24)',
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
          // paragraph's tools at it, and "Sửa nội dung" opens it for editing. The open editor replaces
          // the sentence in the DOM, so every open re-queries from the sentence's current text.
          const editorBox = () => row.getByRole('textbox', { name: 'Nội dung đoạn' })
          const editorBtn = (name) => row.locator('.transcript-editor').getByRole('button', { name })
          const openEditor = async () => {
            const live = (await transcript(qa, 'secretary', m.main.id)).segments.find((s) => s.id === segment.id).text
            const sOf = qa.page.locator('.tl-say').filter({ hasText: live.slice(0, 20) })
            const para = lines(row).locator('.tl-sentence').filter({ has: sOf })
            const s = para.locator('.tl-say').filter({ hasText: live.slice(0, 20) })
            await s.scrollIntoViewIfNeeded()
            await press(qa, s)
            await press(qa, para.getByRole('button', { name: /^Sửa nội dung/ }))
            await qa.expect(editorBox()).toBeVisible()
          }
          await openEditor()
          // R1/FE-10 (22/11/2026): TRANS_FT22 — Esc và "Huỷ" giữ nguyên câu, không gửi gì.
          await editorBox().fill(`${changed} nháp`)
          await editorBox().press('Escape')
          await qa.expect(editorBox()).toHaveCount(0)
          await openEditor()
          await editorBox().fill(`${changed} nháp`)
          await press(qa, editorBtn('Huỷ'))
          await qa.expect(editorBox()).toHaveCount(0)
          // R1/FE-10: TRANS_FT23 — ô rỗng thì nút Lưu khoá, Enter không gửi.
          await openEditor()
          await editorBox().fill('')
          await qa.expect(editorBtn('Lưu')).toBeDisabled()
          await editorBox().press('Enter')
          await qa.expect(editorBox()).toBeVisible()
          await editorBox().fill(changed)
          await settled(qa)
          await qa.shot('dang-sua-cau')
          await editorBox().press('Enter')
          // Toast tồn tại dai (phải tự bấm × mới tắt) nên chờ editor đóng mới chắc chắn lần lưu này xong.
          await qa.expect(row.locator('.transcript-editor')).toHaveCount(0, { timeout: 15_000 })
          await qa.expect(toast(qa, MSG.MSG302).last()).toBeVisible()
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
          // R1/FE-10 (22/11/2026): TRANS_FT24 — ô nhập tối đa 8000 ký tự, lưu đúng 8000 ký tự vẫn đạt.
          await openEditor()
          const boxDai = editorBox()
          await boxDai.fill('a'.repeat(8001))
          qa.expect((await boxDai.inputValue()).length, 'the editor caps at 8000 characters').toBe(8000)
          await settled(qa)
          await qa.shot('sua-cau-8000-ky-tu')
          await press(qa, editorBtn('Lưu'))
          await qa.expect(row.locator('.transcript-editor')).toHaveCount(0, { timeout: 15_000 })
          await qa.expect(toast(qa, MSG.MSG302).last()).toBeVisible()
          c.fact('8000 characters saved', (await transcript(qa, 'secretary', m.main.id)).segments.find((s) => s.id === segment.id).text.length === 8000)
          await noRawCodes(qa)
          c.done()
        } finally {
          await as(qa, 'secretary').put(`/api/transcript-segments/${segment.id}`, { text: original })
        }
      },
    },

    // ------------------------------------------------------------------------------------------- UC-46b Name a voice
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
        'Toast "Đã gán người nói."; ô và tiêu đề các câu của giọng đó hiện tên mới; Thành viên thấy tên (UC-45a POST-2) mà không thấy gợi ý; quantri@ 400 MSG317; cả hai 400 MSG317; Khách 200 MSG316 (Khách có trong danh sách chọn, nhãn "Khách"); Chủ toạ 403 MSG295; nhãn lạ 404; bỏ gán 200 MSG316',
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

    // ------------------------------------------------------------------------------------------- UC-46b AF-2 one line
    {
      id: 'AC6',
      role: 'secretary',
      title: 'Thư ký đổi người nói của riêng một câu ("chỉ câu này", MSG407): câu sang giọng mới SPEAKER_NN của người được chọn, các câu khác giữ giọng; trả về được',
      steps: [
        'Thư ký mở ô "Người nói câu lúc …" của câu "Cảm ơn ý kiến…", chọn "[QA] FE-10 Người nói B"',
        'API: chuyển câu về nhãn cũ; gửi {} ; gửi cả userId và label; nhãn SPEAKER_77; quantri@; Chủ toạ; Thành viên',
      ],
      expected:
        'Toast "Đã đổi người nói của câu này."; chỉ câu đó mang tên "[QA] FE-10 Người nói B", câu "Kính thưa…" của cùng giọng cũ giữ nguyên; API: câu có nhãn mới (giọng mới gán người B), thời lượng chuyển theo câu (BR-395); trả về nhãn cũ 200 MSG407 và giọng mới không còn trong speakers; {} và hai trường 400 MSG317; nhãn lạ 404; quantri@ 400 MSG317; Chủ toạ, Thành viên 403 MSG295',
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

    // ------------------------------------------------------------------------------------------- UC-45b Export
    {
      id: 'AC7',
      role: 'chair',
      title: 'Chủ toạ tải bản gỡ băng .txt; Thư ký tải bản đã gán tên (TRANS_FT08); cuộc họp Mật không ai tải được (MSG356)',
      steps: [
        'Đăng nhập chutoa@, mở tab "Bản ghi" của "[QA] FE-10 Họp trực tiếp gỡ băng", bấm "Tải bản gỡ băng (.txt)"',
        'Thư ký gán tên một giọng (API), tải .txt bằng nút của mình, kiểm tên trong tệp, bỏ gán',
        'Mở tab "Bản ghi" của "[QA] FE-10 Họp Mật gỡ băng"; API GET transcript.txt (Chủ toạ, Thư ký)',
      ],
      expected:
        'Tệp .txt tải về có tên cuộc họp và các dòng "[00:00:00] Người nói 1: …"; Chủ toạ không có ô chọn tên / nút sửa; Thư ký tải tệp "<tên bản ghi> - bản gỡ băng.txt", đầu tệp có tên cuộc họp và các dòng mang tên đã gán (TRANS_FT08); họp Mật: không có nút tải, API 403 MSG356 cho cả Chủ toạ và Thư ký',
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
        // R1/FE-10 (22/11/2026): TRANS_FT08 — Thư ký tải bản đã gán tên.
        const u = await users(qa)
        const t = await transcript(qa, 'secretary', m.main.id)
        const label = t.speakers[0].label
        try {
          qa.expectStatus(await as(qa, 'secretary').put(`/api/recordings/${m.main.id}/speakers/${label}`, { userId: u.a.id, guestId: null }), 200)
          await qa.signIn('secretary')
          await openRecordings(qa, m)
          const secRow = recordingRow(qa, FILES.main)
          const secButton = secRow.getByRole('button', { name: 'Tải bản gỡ băng (.txt)' })
          await secButton.scrollIntoViewIfNeeded()
          await settled(qa)
          await qa.shot('thu-ky-tai-ban-go-bang')
          const [secDownload] = await Promise.all([qa.page.waitForEvent('download'), secButton.click()])
          const stext = readFileSync(await secDownload.path(), 'utf8')
          c.fact('secretary file name', secDownload.suggestedFilename().includes('fe10-hop-chinh') && /bản gỡ băng\.txt$/.test(secDownload.suggestedFilename()), secDownload.suggestedFilename())
          c.fact('secretary header names the meeting and file', stext.includes(m.title) && stext.includes('Bản gỡ băng — bản ghi'), stext.slice(0, 200))
          c.fact('the lines carry the name the Secretary gave', new RegExp(`^\\[\\d\\d:\\d\\d:\\d\\d\\] ${esc(u.a.name)}: `, 'm').test(stext), stext.slice(0, 400))
          qa.note(`Đầu tệp (Thư ký): ${stext.split('\n').slice(0, 6).join(' | ').slice(0, 300)}`)
        } finally {
          await clearNames(qa, m.main.id)
        }
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
      expected: 'Không có tab "Bản ghi" (mở thẳng link thì về tab khác hoặc trang không tìm thấy); API 404 mọi đường, riêng voice-collection 403 vì quantri@ thiếu quyền "Thu giọng mẫu hộ" (đúng luật 403/404); chưa đăng nhập 401',
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
        // R1/FE-10 (22/11/2026): quantri@ chỉ có AdminUsers, không có VoiceCollector (seed chỉ gán
        // VoiceCollector cho vai trò Thư ký) nên cổng nhóm capability trả 403 — đúng luật 403/404 (endpoint
        // cố định thiếu capability → 403; tài nguyên không đọc được → 404 như 4 đường trên).
        c.check('voice collection (no VoiceCollector → 403)', await api.get(`/api/meetings/${b.id}/voice-collection`), 403)
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

    // ------------------------------------------------------------------------------------------- UC-46c Exclude
    {
      id: 'AC9',
      role: 'chair',
      devices: ['desktop', 'phone'],
      title: 'Chủ toạ bỏ một câu theo micro khỏi biên bản (MSG351) rồi khôi phục (MSG352); tệp .txt không có câu đã bỏ (BR-358)',
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
      title: 'Bỏ câu khỏi biên bản: hệ thống hỏi lý do "Lọt tiếng" hoặc "Khác" trước khi bỏ (UC-46c bước 2–3)',
      steps: ['Chủ toạ bấm "Bỏ khỏi biên bản" ở một câu theo micro'],
      expected: 'Hiện lựa chọn lý do (Lọt tiếng / Khác) để chọn và xác nhận, rồi mới bỏ câu (UC-46c Normal Flow bước 2–3)',
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
            `Không hỏi lý do: câu bị bỏ ngay (excluded=${after.excluded}, lý do tự chọn "${after.exclusionReason}") — UC-46c bước 2 yêu cầu hỏi "Lọt tiếng" hoặc "Khác"`,
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
        'Ở chỗ câu đã bỏ chỉ có "Câu đã được bỏ khỏi biên bản."; không có nút "Bỏ khỏi biên bản", "Khôi phục", "Tải lời thoại (.txt)"; API thành viên nhận text "" cho câu đó; 403 MSG353; 403 MSG355; 400 MSG354; Thư ký 200 MSG351 rồi 200 MSG352; Thành viên không thấy nút "Xoá câu" trên câu của người khác, gọi thẳng 403 MSG323 (TRANS_FT38)',
      async run(qa) {
        const cm = await meeting(qa, 'C')
        const u = await users(qa)
        const c = checker(qa)
        const target = (await clips(qa, 'chair', cm.id)).items.find((x) => x.speakerId === u.a.id)
        const path = `/api/speech-clips/${target.id}/exclusion`
        try {
          // R1/FE-10 (22/11/2026): TRANS_FT38 — Thành viên không thấy nút xoá trên câu của người khác.
          const other = (await clips(qa, 'member', cm.id)).items.find((x) => x.speakerId === u.a.id)
          qa.expect(other, 'no sentence of "[QA] FE-10 Người nói A"').toBeTruthy()
          await openRecordings(qa, cm)
          const otherGroup = qa.page.locator('.tl-sentence').filter({ has: qa.page.locator(`[data-clip="${other.id}"]`) })
          await otherGroup.scrollIntoViewIfNeeded()
          await qa.expect(otherGroup.getByRole('button', { name: /^Xoá câu/ })).toHaveCount(0)
          c.check("Member deletes another person's sentence", await as(qa, 'member').delete(`/api/speech-clips/${other.id}`), 403, 'MSG323')
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

    // ------------------------------------------------------------------------------------------- UC-81b Consent
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
        'Thông báo có Mục đích, Ai thu (gồm Thư ký thu bằng micro phòng họp), Lưu gì, Dùng ở đâu, Quyền của tôi (rút lại thì xoá cả mẫu Thư ký thu), Bên kiểm soát dữ liệu, câu "dữ liệu cá nhân nhạy cảm"; ô chưa đánh, nút khoá; sau khi bấm: toast "Đã bật nhận diện giọng nói cho tài khoản của bạn.", chip "Đang bật", tab "Tự thu" / "Thư ký thu hộ", nhắc ghi giọng mẫu; API: noticeVersion 1.3; "1.0" 400 MSG313; gọi lại 200 MSG305, vẫn một đồng ý; cuối cùng rút lại, không còn mẫu',
      async run(qa) {
        const u = await users(qa)
        const c = checker(qa)
        await withdrawn(qa, 'a')
        try {
          await qa.signIn(u.a.email)
          await openMyVoice(qa)
          const card = qa.page.getByRole('region', { name: 'Bật nhận diện giọng nói' })
          await qa.expect(card).toBeVisible()
          for (const text of ['Mục đích:', 'Ai thu:', 'Lưu gì:', 'Dùng ở đâu:', 'Quyền của tôi:', 'Bên kiểm soát dữ liệu:'])
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
          await openMyVoice(qa)
          await qa.expect(qa.page.getByText('Đang bật')).toBeVisible()
          await noRawCodes(qa)
          c.done()
        } finally {
          await withdrawn(qa, 'a')
        }
      },
    },

    // ------------------------------------------------------------------------------------------- UC-81c/d Samples
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
        'Không gửi gì trước khi bấm Lưu (có trình phát nghe lại và nút "Ghi lại"); tiếng bíp bị từ chối, hộp thoại báo lý do tiếng Việt (MSG309), không lưu mẫu; mẫu đọc: 201 MSG307, playable, speechMs ≥ 6000, trang hiện "Giọng mẫu 1" và phát được (200); 1 giây 422 MSG309 too_short; văn bản 415 MSG292; người khác 404 / 404; chưa đồng ý 409 MSG308; mẫu thứ 6 409 MSG310, nút "Ghi giọng mẫu" khoá với gợi ý "Đã đủ 5 giọng mẫu — xoá bớt để ghi lại", "5/5"; xoá bớt một mẫu thì nút mở lại, "4/5" (TRANS_FT64); cuối cùng rút lại, không còn mẫu',
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
            qa.note('Micro giả (tiếng bíp) được nhận là giọng mẫu hợp lệ: cổng chất lượng BR-363 không chặn âm không phải tiếng người.')
            c.fact('a beep is not a voice sample (BR-363)', false)
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
          const saved = await a.form('/api/me/voice/samples', sampleForm(qa, 'enrol', 'giong-mau.wav'))
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
          c.check('1-second sample', await a.form('/api/me/voice/samples', sampleForm(qa, 'short', 'ngan.wav')), 422, 'MSG309')
          const textForm = new FormData()
          textForm.append('file', new Blob(['không phải âm thanh'], { type: 'text/plain' }), 'ghi-chu.txt')
          c.check('a text file', await a.form('/api/me/voice/samples', textForm), 415, 'MSG292')
          c.check('someone else plays it', await as(qa, 'member').get(`/api/me/voice/samples/${sample.id}/audio`), 404)
          c.check('someone else deletes it', await as(qa, 'member').delete(`/api/me/voice/samples/${sample.id}`), 404)
          c.check('without consent', await as(qa, 'b').form('/api/me/voice/samples', sampleForm(qa, 'enrol', 'b.wav')), 409, 'MSG308')
          for (let i = 2; i <= 5; i++)
            c.check(`sample ${i}`, await a.form('/api/me/voice/samples', sampleForm(qa, 'enrol', `mau-${i}.wav`)), 201, 'MSG307')
          c.check('sample 6', await a.form('/api/me/voice/samples', sampleForm(qa, 'enrol', 'mau-6.wav')), 409, 'MSG310')
          await openMyVoice(qa)
          await qa.expect(qa.page.locator('.voice-count').first()).toHaveText('5/5')
          const full = qa.page.getByRole('button', { name: 'Ghi giọng mẫu' })
          await qa.expect(full).toBeDisabled()
          // R1/FE-10 (22/11/2026): TRANS_FT64 — gợi ý khi đã đủ và mở lại nút sau khi xoá bớt.
          await qa.expect(full).toHaveAttribute('data-tooltip', 'Đã đủ 5 giọng mẫu — xoá bớt để ghi lại')
          await settled(qa)
          await qa.shot('du-5-mau')
          c.check('delete one to make room', await as(qa, 'a').delete(`/api/me/voice/samples/${sample.id}`), 200, 'MSG311')
          await openMyVoice(qa)
          await qa.expect(qa.page.locator('.voice-count').first()).toHaveText('4/5')
          await qa.expect(qa.page.getByRole('button', { name: 'Ghi giọng mẫu' })).toBeEnabled()
          await noRawCodes(qa)
          c.done()
        } finally {
          await withdrawn(qa, 'a')
        }
      },
    },

    // ------------------------------------------------------------------------------------------- UC-81e Delete
    {
      id: 'AC14',
      role: 'admin',
      devices: ['desktop', 'phone'],
      title: 'Xoá một giọng mẫu của mình (tự thu và Thư ký thu hộ): hệ thống hỏi xác nhận trước (UC-81e bước 2), rồi xoá (MSG311)',
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
          c.fact('asked before each delete (UC-81e step 2)', asked.every((x) => x.endsWith('có hỏi')), asked.join('; '))
          c.done()
        } finally {
          await clearCollected(qa, b.id, u.a.id)
          await withdrawn(qa, 'a')
        }
      },
    },

    // ------------------------------------------------------------------------------------------- UC-86a–d Collect
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
        'Thẻ "Giọng mẫu cho mic phòng" liệt kê người nội bộ trong danh sách, không có Khách mời; dòng A có "n/5" và nút "Thu"; dòng B "Chưa đồng ý", không nút; hộp thoại "Thu giọng mẫu: …" nhắc dùng micro phòng họp; tiếng bíp ghi bằng micro giả bị từ chối bằng lời tiếng Việt, hộp thoại ở lại, không lưu mẫu (TRANS_FT78); API 201 MSG473; dòng A hiện "Mẫu 1", nghe được (200); A nhận thông báo VoiceSampleCollected, tab thu hộ có cuộc họp và tên Thư ký; xoá: hộp "Xoá mẫu thu hộ?", toast "Đã xoá giọng mẫu thu hộ.", A không còn mẫu thu hộ',
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
          // R1/FE-10 (22/11/2026): TRANS_FT78 — tiếng bíp (không phải giọng người) bị từ chối
          // bằng lời tiếng Việt, hộp thoại ở lại để ghi lại, không lưu mẫu nào.
          await press(qa, dialog.getByRole('button', { name: 'Bắt đầu ghi' }))
          await qa.expect(dialog.getByRole('status').filter({ hasText: 'Đang ghi' })).toBeVisible()
          const proxyStop = dialog.getByRole('button', { name: 'Dừng' })
          await qa.expect(proxyStop).toBeEnabled({ timeout: 15_000 })
          await proxyStop.click()
          await qa.expect(dialog.getByLabel('Nghe lại giọng mẫu vừa ghi')).toBeVisible()
          const badAnswer = qa.page.waitForResponse(
            (r) => r.url().includes(`/voice-collection/${u.a.id}/samples`) && r.request().method() === 'POST', { timeout: 60_000 })
          await press(qa, dialog.getByRole('button', { name: 'Lưu giọng mẫu' }))
          const badRes = await badAnswer
          c.fact('a beep collected for someone else is refused', badRes.status() === 422, `HTTP ${badRes.status()}`)
          await qa.expect(dialog.getByRole('alert')).toBeVisible()
          const said = await dialog.getByRole('alert').innerText()
          c.fact('the reason reads in Vietnamese', /giọng|mẫu|tiếng/i.test(said) && !/MSG\d/.test(said), said)
          await settled(qa)
          await qa.shot('mau-thu-ho-khong-dat')
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
        'B: 409 MSG475; mẫu thứ 6: 409 MSG476 và dòng A "5/5" không nút "Thu"; cuộc họp kết thúc: thẻ vẫn hiện, ghi "Chỉ thu thêm được khi cuộc họp đã công bố hoặc đang họp.", không nút "Thu", API 409 MSG477; Mật 404, trực tuyến 404, ngoài danh sách 404; Chủ toạ 403, Thành viên 403, quantri@ 403 (đều thiếu quyền), ẩn danh 401; mẫu tự thu qua đường thu hộ 404; Chủ toạ không thấy thẻ thu hộ (tab Bản ghi của Chủ toạ khoá tới khi cuộc họp bắt đầu)',
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
          // R1/FE-10 (22/11/2026): quantri@ thiếu VoiceCollector nên cổng capability trả 403
          // (đúng luật 403/404) — AC8 kiểm tương tự.
          c.check('admin, not the Secretary (no VoiceCollector → 403)', await as(qa, 'admin').get(`/api/meetings/${b.id}/voice-collection`), 403)
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

    // ------------------------------------------------------------------------------------------- UC-81f Withdraw
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

    // ------------------------------------------------------------------------------------------- UC-45c + Voice ID
    {
      id: 'AC18',
      role: 'secretary',
      title: 'Chuyển chữ lại (hỏi trước, MSG300) so giọng với người đã đồng ý trong cuộc họp: chỉ Thư ký thấy gợi ý "Khớp giọng nhất", giọng khớp rõ được tự gán; người đó rút đồng ý thì gợi ý và tên máy gán bị xoá',
      steps: [
        'qa-fe10-b@ đồng ý 1.3 và ghi 2 giọng mẫu bằng giọng của người nói thứ hai trong bản ghi (giọng Linh hạ tông; giọng Linh gốc không dùng vì thuky@ đã có giọng mẫu nghe như nó, nên máy không phân định được người)',
        'Thư ký mở bản ghi "fe10-voice-id.wav", bấm "Chuyển thành chữ lại": đọc cảnh báo rồi bấm "Giữ bản hiện có" (giữ nguyên, không gửi gì); mở lại, bấm "Chuyển lại"; chờ xong',
        'Thư ký mở ô tên của giọng nói câu "Tôi đề nghị…"; Thành viên đọc bản gỡ băng (API)',
        'qa-fe10-b@ rút đồng ý; Thư ký đọc lại bản gỡ băng',
      ],
      expected:
        'Hộp "Chuyển thành chữ lại?" cảnh báo chữ đã sửa và tên đã gán sẽ bị thay; "Giữ bản hiện có" thì giữ nguyên, không gửi gì (TRANS_FT16); toast "Đã đưa bản ghi vào hàng chờ chuyển thành chữ."; trong lúc chạy lại chữ cũ vẫn đọc được kèm dòng "Đang chuyển chữ lại cả bản ghi… là bản trước." (TRANS_FT15 E2); khi xong giọng của "Tôi đề nghị…" có gợi ý là B (và được tự gán B, autoAssigned=true, vì từ 22/11 mọi gợi ý đều được tự gán); ô chọn đưa B lên đầu với nhãn "Khớp giọng nhất"; Thành viên: mọi suggested* null (tên tự gán thì vẫn đọc được); không giọng nào gợi ý Khách; rút đồng ý 200 MSG306, sau đó không giọng nào gợi ý hay được máy gán tên B (BR-365)',
      async run(qa) {
        const m = await meeting(qa, 'A')
        const u = await users(qa)
        const c = checker(qa)
        await clearNames(qa, m.voice.id)
        await enrolled(qa, 'b', 2, 'enrol2')
        try {
          await openRecordings(qa, m)
          const row0 = recordingRow(qa, FILES.voice)
          // R1/FE-10 (22/11/2026): TRANS_FT16 — huỷ làm lại thì giữ nguyên bản gỡ băng, không gửi gì.
          await row0.getByRole('button', { name: 'Chuyển thành chữ lại' }).click()
          const cancelDialog = qa.page.getByRole('dialog', { name: 'Chuyển thành chữ lại?' })
          await qa.expect(cancelDialog).toBeVisible()
          await cancelDialog.getByRole('button', { name: 'Giữ bản hiện có' }).click()
          await qa.expect(cancelDialog).toHaveCount(0)
          const stayed = await transcript(qa, 'secretary', m.voice.id)
          c.fact('still Completed, nothing queued', stayed.status === 'Completed', stayed.status)
          await qa.expect(lines(row0).locator('.tl-say')).toHaveCount(stayed.segments.length)
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
          // R1/FE-10 (22/11/2026): TRANS_FT15 E2 — chữ cũ vẫn đọc được trong lúc chạy lại.
          await qa.expect(row.getByText('Đang chuyển chữ lại cả bản ghi, có lượt ghi mới. Chữ dưới đây là bản trước.')).toBeVisible({ timeout: 15_000 })
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
          // R1/FE-10 (22/11/2026): TRANS_FT27 — ô chọn nêu gợi ý của máy ("Khớp giọng nhất" / "Máy tự gán").
          const tip = await trigger.getAttribute('data-tooltip')
          qa.note(`Gợi ý trên ô tên: ${tip}`)
          c.fact('the picker names the match on hover', !!tip && (/Khớp giọng nhất: /.test(tip) || /Máy tự gán vì giọng khớp rõ/.test(tip)), tip ?? '(không có gợi ý)')
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

    // ------------------------------------------------------------------------------------------- UC-48 readiness
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

    // ------------------------------------------------------------------------------------------- UC-47 AI settings
    // R1/FE-10 (22/11/2026): AC mới — TRANS_FT79 (xem), TRANS_FT81 (từng ô sai), TRANS_FT83
    // (thử kết nối hỏng), TRANS_FT84 (không quyền). Không lưu gì, không gọi API trả phí.
    {
      id: 'AC20',
      role: 'admin',
      devices: ['desktop', 'phone'],
      title: 'Quản trị xem "Cấu hình AI", biểu mẫu báo lỗi từng ô (MSG409), thử kết nối hỏng báo đỏ (MSG413); người không quyền thấy 404',
      steps: [
        'Đăng nhập quantri@, bấm "Cấu hình AI" ở sidebar (/mgmt/admin/ai)',
        'Chọn "API", nhập "http://x" vào Địa chỉ API, để trống Mô hình và Khoá, bấm Lưu',
        'Nhập địa chỉ https không tới được + model + key sai, bấm "Thử kết nối"',
        'Đăng nhập thanhvien@, mở /mgmt/admin/ai; API của thanhvien@',
      ],
      expected:
        'Trang "Cấu hình AI" ("Máy viết dự thảo biên bản") có "Viết dự thảo bằng" (Máy local/API), dòng "Máy local" + model + chip cấu hình, "Đang dùng …" và gợi ý chỉ họp Thường dùng API; biểu mẫu sai: toast "Cấu hình AI không hợp lệ." (400 MSG409) + từng lỗi ô, gõ lại xoá lỗi, không lưu gì; bấm "Thử kết nối" với địa chỉ không tới được: dòng đỏ lý do (502 MSG413), biểu mẫu giữ nguyên, cấu hình không đổi; thanhvien@: sidebar không có "Cấu hình AI", URL hiện "Không tìm thấy trang", API 403',
      async run(qa) {
        const c = checker(qa)
        const admin = as(qa, 'admin')
        const before = await admin.get('/api/admin/ai')
        c.check('read settings', before, 200)
        qa.note(`Cấu hình AI: mode ${before.body?.mode}, localConfigured ${before.body?.localConfigured}, model local ${before.body?.localModel}`)
        await qa.signIn('admin')
        await qa.goto('/mgmt/meetings')
        await qa.expect(qa.page.getByRole('heading', { level: 1, name: 'Cuộc họp' })).toBeVisible({ timeout: 15_000 })
        const aiLink = qa.page.getByRole('link', { name: 'Cấu hình AI' })
        if (!(await aiLink.isVisible())) await press(qa, qa.page.getByRole('button', { name: 'Quản trị', exact: true }))
        await press(qa, aiLink)
        const form = qa.page.locator('form.ai-settings')
        await qa.expect(qa.page.getByRole('heading', { level: 1, name: 'Cấu hình AI' })).toBeVisible()
        await qa.expect(form.getByText('Viết dự thảo bằng')).toBeVisible()
        await qa.expect(form.getByRole('radio', { name: 'Máy local' })).toBeVisible()
        await qa.expect(form.getByRole('radio', { name: 'API' })).toBeVisible()
        await qa.expect(form.locator('p.ai-settings-local')).toBeVisible()
        await qa.expect(form.locator('p.ai-settings-local').getByText(/Đã cấu hình|Chưa cấu hình/)).toBeVisible()
        await qa.expect(form.getByText(/Đang dùng/)).toBeVisible()
        await qa.expect(form.locator('button.info-tip')).toHaveAttribute('data-tooltip', /Chỉ cuộc họp Thường dùng API/)
        await settled(qa)
        await qa.shot('cau-hinh-ai')
        await press(qa, form.getByRole('radio', { name: 'API' }))
        await form.getByLabel('Địa chỉ API').fill('http://x')
        await form.getByLabel('Mô hình').fill('')
        await form.getByLabel('Khoá API').fill('')
        await settled(qa)
        await qa.shot('bieu-mau-sai')
        await press(qa, form.getByRole('button', { name: 'Lưu' }))
        // Toast hiện ở khung chung, ngoài form.
        await qa.expect(toast(qa, 'Cấu hình AI không hợp lệ.')).toBeVisible()
        await qa.expect(form.getByText('Địa chỉ API phải là https://… và không quá 500 ký tự.')).toBeVisible()
        await qa.expect(form.getByText('Nhập tên model.')).toBeVisible()
        await qa.expect(form.getByText('Nhập key của API.')).toBeVisible()
        c.check('invalid settings', await admin.put('/api/admin/ai', { mode: 'Api', apiBaseUrl: 'http://x', apiModel: '', apiKey: '' }), 400, 'MSG409')
        await form.getByLabel('Mô hình').fill('x')
        await qa.expect(form.getByText('Nhập tên model.')).toHaveCount(0)
        await settled(qa)
        await qa.shot('loi-o-da-xoa')
        await form.getByLabel('Địa chỉ API').fill('https://127.0.0.1:9/')
        await form.getByLabel('Mô hình').fill('khong-co-model')
        await form.getByLabel('Khoá API').fill('khoa-sai-12345678')
        const testing = form.getByRole('button', { name: 'Thử kết nối API' })
        await qa.expect(testing).toBeEnabled()
        const answer = qa.page.waitForResponse((r) => r.url().endsWith('/api/admin/ai/test') && r.request().method() === 'POST')
        await press(qa, testing)
        const res = await answer
        // "Đang thử…" chỉ thoáng qua khi máy chủ từ chối ngay nên không bắt trạng thái đó.
        c.fact('unreachable API fails the test', res.status() === 502, `HTTP ${res.status()}`)
        await qa.expect(form.locator('p.alert-error[role="status"]')).toBeVisible({ timeout: 60_000 })
        qa.expect(await form.getByLabel('Địa chỉ API').inputValue()).toBe('https://127.0.0.1:9/')
        qa.expect(await form.getByLabel('Mô hình').inputValue()).toBe('khong-co-model')
        await settled(qa)
        await qa.shot('thu-ket-noi-hong')
        const tested = await admin.post('/api/admin/ai/test', { mode: 'Api', apiBaseUrl: 'https://127.0.0.1:9/', apiModel: 'khong-co-model', apiKey: 'khoa-sai-12345678' })
        c.check('test call fails 502/MSG413', tested, 502, 'MSG413')
        const kept = await admin.get('/api/admin/ai')
        c.fact('nothing saved by trying', JSON.stringify(kept.body) === JSON.stringify(before.body))
        await qa.signIn('member')
        await qa.goto('/mgmt/meetings')
        await qa.expect(qa.page.getByRole('heading', { level: 1, name: 'Cuộc họp' })).toBeVisible({ timeout: 15_000 })
        await qa.expect(qa.page.getByRole('link', { name: 'Cấu hình AI' })).toHaveCount(0)
        await qa.goto('/mgmt/admin/ai')
        await qa.expect(qa.page.getByText('Không tìm thấy trang')).toBeVisible({ timeout: 15_000 })
        await settled(qa)
        await qa.shot('thanh-vien-khong-thay-cau-hinh-ai')
        c.check('Member reads settings', await as(qa, 'member').get('/api/admin/ai'), 403)
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
        c.done()
      },
    },

    // ------------------------------------------------------------------------------------------- UC-46a/46d per-mic
    // R1/FE-10 (22/11/2026): AC mới — TRANS_FT31 (Thư ký sửa câu theo micro, Thành viên thấy chữ
    // đã sửa nhưng không có bút), TRANS_FT36 (Thư ký xoá câu sau khi xác nhận).
    {
      id: 'AC21',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Thư ký sửa chữ một câu theo micro (MSG302, "đã sửa" ghi người sửa) và xoá câu sau khi xác nhận (MSG322); Thành viên chỉ đọc',
      steps: [
        'Thư ký mở tab "Bản ghi" của "[QA] FE-10 Họp trực tuyến theo micro", bấm bút "Sửa nội dung" ở một câu của Thành viên, đổi chữ, bấm Lưu',
        'Thành viên mở cùng thẻ',
        'Thư ký gửi thêm một câu tạm (API), bấm "Xoá câu" ở câu đó: đọc hộp xác nhận, bấm "Giữ lại", rồi bấm lại "Xoá câu" và xác nhận "Xoá câu"',
      ],
      expected:
        'Ô "Nội dung câu" + nút Huỷ/Lưu; Lưu: toast "Đã lưu đoạn gỡ băng.", câu hiện "đã sửa" (gợi ý "Sửa bởi <Thư ký> lúc …"), API editedBy là Thư ký; Thành viên thấy chữ mới + "đã sửa", không có bút "Sửa nội dung" nào; hộp "Xoá câu này?" hiện câu trong ngoặc + "Câu và âm thanh của nó bị xoá khỏi cuộc họp."; "Giữ lại" giữ câu; xác nhận "Xoá câu": toast "Đã xoá câu nói.", câu biến mất, các câu khác còn nguyên',
      async run(qa) {
        const cm = await meeting(qa, 'C')
        const u = await users(qa)
        const c = checker(qa)
        const sec = as(qa, 'secretary')
        const target = (await clips(qa, 'secretary', cm.id)).items.find((x) => x.speakerId === u.member.id && !x.excluded)
        qa.expect(target, 'no sentence of the Member').toBeTruthy()
        const original = target.text
        const changed = `${original.replace(/[.\s]+$/, '')} và phần mềm quản lý văn bản.`
        try {
          await openRecordings(qa, cm)
          const card = qa.page.locator('section.tl-card')
          await qa.expect(card).toBeVisible()
          const say = card.locator(`[data-clip="${target.id}"]`)
          // filter() cần locator page-scope bên trong (card-scope trả 0 — như AC9).
          const sentence = card.locator('.tl-sentence').filter({ has: qa.page.locator(`[data-clip="${target.id}"]`) })
          await say.scrollIntoViewIfNeeded()
          await say.focus()
          // Công cụ hiện theo câu đã chọn (không cần hover — headless không có chuột).
          const editBtn = sentence.getByRole('button', { name: /^Sửa nội dung/ })
          await qa.expect(editBtn).toBeVisible({ timeout: 15_000 })
          await press(qa, editBtn)
          const box = card.getByRole('textbox', { name: 'Nội dung câu' })
          await qa.expect(box).toBeVisible()
          await box.fill(changed)
          await settled(qa)
          await qa.shot('sua-cau-theo-micro')
          // Mở editor đã thay câu bằng ô nhập (data-clip không còn) nên bấm Lưu theo khung editor.
          const perMicEditor = () => card.locator('.transcript-editor')
          await press(qa, perMicEditor().getByRole('button', { name: 'Lưu' }))
          await qa.expect(perMicEditor()).toHaveCount(0, { timeout: 15_000 })
          await qa.expect(toast(qa, MSG.MSG302).last()).toBeVisible()
          const fixed = card.locator('.tl-sentence').filter({ hasText: 'phần mềm quản lý văn bản' })
          await qa.expect(fixed.getByText('đã sửa')).toBeVisible()
          await qa.expect(fixed.getByText('đã sửa')).toHaveAttribute('data-tooltip', new RegExp(`Sửa bởi ${esc(u.secretary.name)}`))
          await settled(qa)
          await qa.shot('cau-theo-micro-da-sua')
          const after = (await clips(qa, 'secretary', cm.id)).items.find((x) => x.id === target.id)
          c.fact('text saved', after.text === changed, after.text)
          c.fact('editor recorded', after.editedByName === u.secretary.name && !!after.editedAt, `${after.editedByName} ${after.editedAt}`)
          await noRawCodes(qa)

          await qa.signIn('member')
          await openRecordings(qa, cm)
          const cardM = qa.page.locator('section.tl-card')
          await qa.expect(cardM.locator('.tl-sentence').filter({ hasText: 'phần mềm quản lý văn bản' }).getByText('đã sửa')).toBeVisible()
          await qa.expect(cardM.getByRole('button', { name: /^Sửa nội dung/ })).toHaveCount(0)
          await cardM.locator('.tl-sentence').filter({ hasText: 'phần mềm quản lý văn bản' }).scrollIntoViewIfNeeded()
          await settled(qa)
          await qa.shot('thanh-vien-doc-cau-da-sua')
          await qa.signIn('secretary')

          const temp = await sendClip(qa, 'member', cm.id, 'short')
          const tempFresh = (await clips(qa, 'secretary', cm.id)).items.find((x) => x.id === temp.id)
          const tempText = temp.text ?? tempFresh?.text ?? ''
          qa.expect(tempText, 'the temporary sentence has no text').not.toBe('')
          await openRecordings(qa, cm)
          const card2 = qa.page.locator('section.tl-card')
          const tempSay = card2.locator(`[data-clip="${temp.id}"]`)
          const tempSentence = card2.locator('.tl-sentence').filter({ has: qa.page.locator(`[data-clip="${temp.id}"]`) })
          await tempSay.scrollIntoViewIfNeeded()
          await tempSay.focus()
          const trashBtn = tempSentence.getByRole('button', { name: /^Xoá câu/ })
          await qa.expect(trashBtn).toBeVisible({ timeout: 15_000 })
          await press(qa, trashBtn)
          const dialog = qa.page.getByRole('dialog', { name: 'Xoá câu này?' })
          await qa.expect(dialog).toBeVisible()
          await qa.expect(dialog.getByText(tempText)).toBeVisible()
          await qa.expect(dialog.getByText('Câu và âm thanh của nó bị xoá khỏi cuộc họp.')).toBeVisible()
          await settled(qa)
          await qa.shot('xac-nhan-xoa-cau')
          await press(qa, dialog.getByRole('button', { name: 'Giữ lại' }))
          await qa.expect(dialog).toHaveCount(0)
          await qa.expect(card2.locator(`[data-clip="${temp.id}"]`)).toHaveCount(1)
          await tempSay.focus()
          const trashAgain = tempSentence.getByRole('button', { name: /^Xoá câu/ })
          await qa.expect(trashAgain).toBeVisible({ timeout: 15_000 })
          await press(qa, trashAgain)
          await qa.expect(qa.page.getByRole('dialog', { name: 'Xoá câu này?' })).toBeVisible()
          const gone = qa.page.waitForResponse((r) => r.url().includes(`/api/speech-clips/${temp.id}`) && r.request().method() === 'DELETE')
          await press(qa, qa.page.getByRole('dialog', { name: 'Xoá câu này?' }).getByRole('button', { name: 'Xoá câu', exact: true }))
          const deleted = await gone
          c.fact('deleted as MSG322', deleted.status() === 200, `HTTP ${deleted.status()}`)
          await qa.expect(toast(qa, 'Đã xoá câu nói.')).toBeVisible()
          await qa.expect(card2.locator(`[data-clip="${temp.id}"]`)).toHaveCount(0)
          const left = (await clips(qa, 'secretary', cm.id)).items
          c.fact('the other sentences are untouched', left.some((x) => x.id === target.id) && !left.some((x) => x.id === temp.id))
          await settled(qa)
          await qa.shot('cau-da-bi-xoa')
          c.done()
        } finally {
          const now = (await clips(qa, 'secretary', cm.id)).items.find((x) => x.id === target?.id)
          if (now && now.text !== original) await sec.put(`/api/speech-clips/${target.id}/text`, { text: original })
        }
      },
    },

    // ------------------------------------------------------------------------------------------- UC-45 empty + silence
    // R1/FE-10 (22/11/2026): AC mới — TRANS_FT11 (thẻ trống khi chưa có câu nào), TRANS_FT05
    // (bản ghi im lặng, bản ghi chưa chuyển chữ).
    {
      id: 'AC22',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Thẻ trống khi chưa có câu nào (kèm gợi ý lúc đang họp); bản ghi im lặng và bản ghi chưa chuyển chữ có thông điệp riêng',
      steps: [
        'Thư ký mở tab "Bản ghi" của "[QA] FE-10 Họp trực tuyến trống câu" (đang họp, không câu nào)',
        'Tải "fe10-im-lang-<giờ>.wav" (im lặng) lên "[QA] FE-10 Họp trực tiếp gỡ băng" bằng API, chờ chuyển chữ xong',
      ],
      expected:
        'Thẻ hiện "Cuộc họp này chưa có câu nào được ghi" + "Lời nói trong phòng họp trực tuyến hiện ở đây theo từng câu." (đang họp); không nút "Tải lời thoại (.txt)"; bản ghi im lặng tự chuyển chữ rồi hiện "Không nghe thấy lời nói nào trong bản ghi."; dọn bản ghi tạm sau khi kiểm. Ghi nhận: chữ "Bản ghi này chưa được chuyển thành chữ." không tới được qua tải lên — upload luôn tạo transcript Pending (TranscriptionStatus không có None; API chỉ trả None khi thiếu dòng transcript), xem TRANS_FT05',
      async run(qa) {
        const c = checker(qa)
        const em = await meeting(qa, 'E')
        await openRecordings(qa, em)
        const card = qa.page.locator('section.tl-card')
        await qa.expect(card.getByText('Cuộc họp này chưa có câu nào được ghi')).toBeVisible()
        await qa.expect(card.getByText('Lời nói trong phòng họp trực tuyến hiện ở đây theo từng câu.')).toBeVisible()
        await qa.expect(card.getByRole('button', { name: 'Tải lời thoại (.txt)' })).toHaveCount(0)
        await settled(qa)
        await qa.shot('the-trong-chua-co-cau')

        const m = await meeting(qa, 'A')
        const sec = as(qa, 'secretary')
        const fileName = `fe10-im-lang-${Date.now().toString(36)}.wav`
        let createdId = null
        try {
          const up = await sec.form(
            `/api/meetings/${m.id}/recordings`,
            wavForm(audio(qa, 'sil'), fileName, { consentConfirmed: 'true', consentPurpose: 'Lập biên bản cuộc họp (QA FE-10)' }),
          )
          qa.expectStatus(up, 201)
          const list = await sec.get(`/api/meetings/${m.id}/recordings`)
          createdId = list.body.items.find((r) => r.fileName === fileName)?.id
          c.fact('the silent recording is listed', !!createdId)
          await openRecordings(qa, m)
          const row = recordingRow(qa, fileName)
          await qa.expect(row).toBeVisible({ timeout: 30_000 })
          // Upload tự xếp hàng chuyển chữ (transcript Pending từ lúc tạo) nên không qua
          // trạng thái "chưa được chuyển thành chữ"; chờ xong rồi kiểm thông điệp im lặng.
          await waitTranscript(qa, createdId)
          const t = await transcript(qa, 'secretary', createdId)
          c.fact('no segments heard', t.status === 'Completed' && t.segments.length === 0, `${t.status} ${t.segments.length}`)
          await row.scrollIntoViewIfNeeded()
          await qa.expect(row.getByText('Không nghe thấy lời nói nào trong bản ghi.')).toBeVisible({ timeout: 30_000 })
          await row.scrollIntoViewIfNeeded()
          await settled(qa)
          await qa.shot('ban-ghi-im-lang')
          await noRawCodes(qa)
          c.done()
        } finally {
          if (createdId) await sec.delete(`/api/recordings/${createdId}`)
        }
      },
    },

    // ------------------------------------------------------------------------------------------- UC-45a playback
    // R1/FE-10 (22/11/2026): AC mới — TRANS_FT02 (bấm mốc giờ thì nghe từ câu đó, tô sáng câu
    // đang nghe, Tạm dừng) + TRANS_FT49 E2 (người ngoài cuộc họp mở tab Biên bản thì 404).
    {
      id: 'AC23',
      role: 'secretary',
      devices: ['desktop'],
      title: 'Bấm mốc giờ thì nghe từ câu đó (nút thành "Tạm dừng", câu đang nghe tô sáng); người ngoài cuộc họp mở tab Biên bản thì 404',
      steps: [
        'Thư ký mở tab "Bản ghi" của "[QA] FE-10 Họp trực tiếp gỡ băng", bấm mốc giờ của lượt thứ hai',
        'Bấm "Tạm dừng"',
        'Đăng nhập quantri@, mở /mgmt/meetings/{id}?tab=minutes; API summary của quantri@',
      ],
      expected:
        'Bấm mốc giờ ("Nghe từ <time>"): câu đang nghe mang class tô sáng, nút của lượt thành "Tạm dừng" (aria-pressed true); bấm lại thì dừng, nút về "Nghe từ <time>"; tab Biên bản của người ngoài: "Không tìm thấy trang", API 404',
      async run(qa) {
        const m = await meeting(qa, 'A')
        const c = checker(qa)
        await openRecordings(qa, m)
        const row = recordingRow(qa, FILES.main)
        await qa.expect(lines(row).locator('.tl-say').first()).toBeVisible()
        const second = lines(row).locator('li.tl-group').nth(1).locator('button.tl-play')
        await second.scrollIntoViewIfNeeded()
        await press(qa, second)
        const pausing = lines(row).locator('li.tl-group').nth(1).locator('button.tl-play')
        await qa.expect(pausing).toHaveAccessibleName('Tạm dừng', { timeout: 15_000 })
        await qa.expect(pausing).toHaveAttribute('aria-pressed', 'true')
        await qa.expect(lines(row).locator('.tl-say.is-active').first()).toBeVisible({ timeout: 15_000 })
        await row.scrollIntoViewIfNeeded()
        await settled(qa)
        await qa.shot('dang-nghe-tu-moc-gio')
        await press(qa, pausing)
        await qa.expect(lines(row).locator('li.tl-group').nth(1).locator('button.tl-play')).toHaveAccessibleName(/^Nghe từ/, { timeout: 15_000 })
        await settled(qa)
        await qa.shot('da-tam-dung')
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
        await qa.signIn('admin')
        await qa.goto(`/mgmt/meetings/${m.id}?tab=minutes`)
        await qa.expect(qa.page.getByText('Không tìm thấy trang')).toBeVisible({ timeout: 15_000 })
        await settled(qa)
        await qa.shot('nguoi-ngoai-mo-tab-bien-ban')
        c.check('outsider summary', await as(qa, 'admin').get(`/api/meetings/${m.id}/summary`), 404)
        c.done()
      },
    },

    // ------------------------------------------------------------------------------------------- UC-47 save API
    // R1/FE-10 (24/11/2026): AC mới — TRANS_FT59 (chuyển sang API và lưu bằng key mới: key không bao giờ hiện lại).
    {
      id: 'AC24',
      role: 'admin',
      devices: ['desktop', 'phone'],
      title: 'Chuyển sang API và lưu bằng key mới: key không bao giờ hiện lại, chân trang đổi sang API (TRANS_FT59)',
      steps: [
        'Đăng nhập quantri@, mở "/mgmt/admin/ai", chọn "API", nhập địa chỉ https, model và key rồi bấm "Lưu"',
        'Đọc lại ô "Khoá API" và chân trang, tải lại trang',
        'GET /api/admin/ai, rồi trả cấu hình về Máy local',
      ],
      expected:
        'Trước khi lưu chân trang "Chưa lưu · đang dùng Máy local"; bấm Lưu: toast "Đã lưu cấu hình AI." (200 MSG408); ô Khoá API trống với placeholder "Đã lưu ••••5678"; chân trang "Đang dùng API · qa-stub-model"; tải lại vẫn chọn API, key vẫn không hiện; API không bao giờ trả key (chỉ hasApiKey + hint); trả về Máy local xong GET lại mode Local',
      async run(qa) {
        const c = checker(qa)
        const admin = as(qa, 'admin')
        const before = await admin.get('/api/admin/ai')
        c.check('read settings', before, 200)
        qa.note(`Cấu hình trước: mode ${before.body?.mode}, model local ${before.body?.localModel}`)
        try {
          await qa.signIn('admin')
          await qa.goto('/mgmt/admin/ai')
          await qa.expect(qa.page.getByRole('heading', { level: 1, name: 'Cấu hình AI' })).toBeVisible({ timeout: 15_000 })
          const form = qa.page.locator('form.ai-settings')
          await qa.expect(form.getByText('Viết dự thảo bằng')).toBeVisible()
          await press(qa, form.getByRole('radio', { name: 'API' }))
          await form.getByLabel('Địa chỉ API').fill('https://api.example.invalid/v1/')
          await form.getByLabel('Mô hình').fill('qa-stub-model')
          await form.getByLabel('Khoá API').fill('qa-throwaway-key-12345678')
          await qa.expect(form.getByText('Chưa lưu · đang dùng')).toBeVisible()
          await settled(qa)
          await qa.shot('chon-api-chua-luu')
          await noSidewaysScroll(qa)
          const saved = qa.page.waitForResponse(
            (r) => r.url().endsWith('/api/admin/ai') && r.request().method() === 'PUT',
          )
          await press(qa, form.getByRole('button', { name: 'Lưu', exact: true }))
          const res = await saved
          qa.expect(res.status(), 'PUT /api/admin/ai').toBe(200)
          await qa.expect(toast(qa, 'Đã lưu cấu hình AI.')).toBeVisible()
          const key = form.getByLabel('Khoá API')
          await qa.expect(key).toHaveValue('')
          await qa.expect(key).toHaveAttribute('placeholder', 'Đã lưu ••••5678')
          await qa.expect(form.locator('.ai-settings-foot')).toContainText('Đang dùng')
          await qa.expect(form.locator('.ai-settings-foot')).toContainText('API · qa-stub-model')
          await settled(qa)
          await qa.shot('da-luu-api')
          await qa.page.reload()
          await qa.expect(qa.page.getByRole('heading', { level: 1, name: 'Cấu hình AI' })).toBeVisible({ timeout: 15_000 })
          await qa.expect(form.getByRole('radio', { name: 'API' })).toBeChecked()
          await qa.expect(form.getByLabel('Khoá API')).toHaveValue('')
          await qa.expect(form.getByLabel('Khoá API')).toHaveAttribute('placeholder', 'Đã lưu ••••5678')
          await qa.expect(form.locator('.ai-settings-foot')).toContainText('API · qa-stub-model')
          await settled(qa)
          await qa.shot('tai-lai-van-dung-api')
          await noSidewaysScroll(qa)
          await noRawCodes(qa)
          const got = await admin.get('/api/admin/ai')
          if (c.check('saved settings', got, 200)) {
            c.fact('mode Api', got.body.mode === 'Api', got.body.mode)
            c.fact('base url kept', got.body.apiBaseUrl === 'https://api.example.invalid/v1/', got.body.apiBaseUrl)
            c.fact('model kept', got.body.apiModel === 'qa-stub-model', got.body.apiModel)
            c.fact('key stored, hint only', got.body.hasApiKey === true && got.body.apiKeyHint === '5678', JSON.stringify({ hasApiKey: got.body.hasApiKey, apiKeyHint: got.body.apiKeyHint }))
            c.fact('key never returned', !('apiKey' in got.body) || got.body.apiKey == null, Object.keys(got.body).join(','))
          }
        } finally {
          const restored = await admin.put('/api/admin/ai', { mode: 'Local', apiBaseUrl: '', apiModel: '', apiKey: '' })
          c.check('restore Local', restored, 200, 'MSG408')
        }
        const after = await admin.get('/api/admin/ai')
        if (c.check('back to Local', after, 200)) c.fact('mode Local', after.body.mode === 'Local', after.body.mode)
        c.done()
      },
    },
    // ------------------------------------------------------------------------------------------- UC-47 test connection
    // R1/FE-10 (24/11/2026): AC mới — TRANS_FT61 (nút "Thử kết nối" cho API và máy local: báo xanh, không lưu gì).
    {
      id: 'AC25',
      role: 'admin',
      devices: ['desktop', 'phone'],
      title: 'Thử kết nối API rồi máy local: nút báo "Đang thử…", dòng xanh "Kết nối được <model> · <n> giây" (200 MSG412), không lưu gì (TRANS_FT61)',
      steps: [
        'Mở "/mgmt/admin/ai", chọn "API", nhập địa chỉ stub local http://127.0.0.1:12121/v1/, model và key',
        'Bấm "Thử kết nối" của API, đọc dòng kết quả và chân trang',
        'Chuyển sang "Máy local", bấm "Thử kết nối" của máy local, đọc dòng kết quả và chân trang',
      ],
      expected:
        'Mỗi lần bấm: nút hiện "Đang thử…" rồi dòng xanh "Kết nối được <model> · <n,n> giây" (backend POST /api/admin/ai/test 200 MSG412); không có PUT /api/admin/ai nào (chân trang vẫn "Chưa lưu…" khi form khác, "Đang dùng Máy local" khi khớp)',
      async run(qa) {
        let puts = 0
        await qa.page.route('**/api/admin/ai', (route) => {
          if (route.request().method() === 'PUT') puts += 1
          route.continue()
        })
        try {
          await qa.signIn('admin')
          await qa.goto('/mgmt/admin/ai')
          await qa.expect(qa.page.getByRole('heading', { level: 1, name: 'Cấu hình AI' })).toBeVisible({ timeout: 15_000 })
          const form = qa.page.locator('form.ai-settings')
          await press(qa, form.getByRole('radio', { name: 'API' }))
          await form.getByLabel('Địa chỉ API').fill('http://127.0.0.1:12121/v1/')
          await form.getByLabel('Mô hình').fill('qa-stub-model')
          await form.getByLabel('Khoá API').fill('qa-stub-key')
          await qa.expect(form.locator('.ai-settings-foot')).toContainText('Chưa lưu')
          // Step 1: the API in the form is a local stub speaking OpenAI (qa-fixtures/stub_api.py).
          const apiTest = qa.page.waitForResponse(
            (r) => r.url().endsWith('/api/admin/ai/test') && r.request().method() === 'POST',
          )
          await press(qa, form.getByRole('button', { name: 'Thử kết nối API' }))
          const apiRes = await apiTest
          qa.expect(apiRes.status(), 'POST /api/admin/ai/test (Api)').toBe(200)
          qa.expect((await apiRes.json()).code, 'test-connection code (Api)').toBe('MSG412')
          const apiLine = form.getByRole('status').filter({ hasText: 'Kết nối được' })
          await qa.expect(apiLine).toContainText(/Kết nối được qa-stub-model · [\d,]+ giây/, { timeout: 30_000 })
          await qa.expect(apiLine).toHaveClass(/alert-success/)
          qa.expect(puts, 'a PUT /api/admin/ai was sent by "Thử kết nối"').toBe(0)
          await qa.expect(form.locator('.ai-settings-foot')).toContainText('Chưa lưu')
          await settled(qa)
          await qa.shot('thu ket noi api')
          // Step 2: the in-house machine (Ollama qwen2.5:7b-instruct, started for round 1).
          await press(qa, form.getByRole('radio', { name: 'Máy local' }))
          const localTest = qa.page.waitForResponse(
            (r) => r.url().endsWith('/api/admin/ai/test') && r.request().method() === 'POST',
          )
          await press(qa, form.getByRole('button', { name: 'Thử kết nối máy local' }))
          const localRes = await localTest
          qa.expect(localRes.status(), 'POST /api/admin/ai/test (Local)').toBe(200)
          const localBody = await localRes.json()
          qa.expect(localBody.code, 'test-connection code').toBe('MSG412')
          qa.note(`Máy local trả model: ${localBody.model}`)
          const localLine = form.getByRole('status').filter({ hasText: 'Kết nối được' })
          await qa.expect(localLine).toContainText(/Kết nối được .+ · [\d,]+ giây/, { timeout: 60_000 })
          await qa.expect(localLine).toHaveClass(/alert-success/)
          qa.expect(puts, 'a PUT /api/admin/ai was sent by "Thử kết nối"').toBe(0)
          await qa.expect(form.locator('.ai-settings-foot')).toContainText('Máy local')
          await settled(qa)
          await qa.shot('thu ket noi local')
          await noSidewaysScroll(qa)
          await noRawCodes(qa)
        } finally {
          await qa.page.unroute('**/api/admin/ai')
        }
      },
    },
    // ------------------------------------------------------------------------------------------- R1/FE-10 (24/11/2026)
    // AC mới — 11 case TRANS còn trống: FT01 xem bản gỡ băng (AC26), FT04 im lặng + chưa chuyển chữ (AC27),
    // FT05 lỗi chuyển chữ (AC28), FT13 chạy lại bản lỗi (AC29), FT06 Thành viên chỉ đọc (AC30), FT09 bản ghi
    // theo micro trực tiếp + xuất (AC31), FT22 Thành viên không có công cụ gán/tên (AC32), FT27 người nói rút
    // câu của mình (AC33), FT29 xin dự thảo AI (AC34), FT30 ghi chú sẵn sàng (AC35), FT31 gán tên từ thẻ (AC36).
    // Âm thanh từ qa-fixtures/ (không cần macOS say, không cần QA_TTS_DIR): meeting.wav tách được 2 giọng,
    // garbage (byte ngẫu nhiên) ép transcript Failed, cục im lặng ép Completed rỗng.
    {
      id: 'AC26',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Thư ký mở bản ghi và đọc bản gỡ băng: chip, mốc giờ, người nói, nhãn máy, thẻ người nói (TRANS_FT01)',
      steps: [
        'Đăng nhập thuky@, mở tab "Bản ghi" của "[QA] FE-10 Họp trực tiếp gỡ băng", bấm "Nghe và xem bản gỡ băng" ở dòng "fe10-hop-chinh.wav"',
        'Đọc các lượt thoại, thẻ "Người nói" và nhãn "Máy nhận dạng"',
      ],
      expected:
        'Dòng bản ghi có chip "Có bản gỡ băng"; bản gỡ băng có nhãn "Máy nhận dạng" và chú thích "Chữ do máy tạo, có thể sai — bấm vào một câu để nghe lại." (chữ trong code; case ghi "mốc giờ/đoạn đó" là diễn đạt lại); mỗi lượt có ô chọn tên người nói ("Người nói câu lúc mm:ss", "Người nói 1/2" khi chưa gán ở thẻ bên); cột "Người nói" có 2 giọng, số câu trên màn bằng số câu API trả (backend GET transcript 200)',
      async run(qa) {
        const m = await meeting(qa, 'A')
        await clearNames(qa, m.main.id)
        const t = await transcript(qa, 'secretary', m.main.id)
        qa.expect(t.segments.length, 'the fixture reads as lines').toBeGreaterThanOrEqual(3)
        qa.expect(t.speakers.length, 'the worker hears two voices').toBeGreaterThanOrEqual(2)
        await openRecordings(qa, m)
        const row = recordingRow(qa, FILES.main)
        await qa.expect(row.getByText('Có bản gỡ băng')).toBeVisible()
        await qa.expect(row.getByText('Máy nhận dạng')).toBeVisible()
        await qa.expect(row.getByText('Chữ do máy tạo, có thể sai — bấm vào một câu để nghe lại.')).toBeVisible()
        await qa.expect(lines(row).locator('.tl-say')).toHaveCount(t.segments.length)
        for (let i = 0; i < t.segments.length; i++) {
          const say = lines(row).locator('.tl-say').nth(i)
          await qa.expect(say).toBeVisible()
          await qa.expect(say).toHaveAttribute('aria-label', /^Nghe từ \d\d:\d\d/)
        }
        // Thư ký: tên người nói của từng câu là một ô chọn ("Người nói câu lúc mm:ss"); tên "Người nói N" đọc ở thẻ bên.
        await qa.expect(lines(row).getByRole('button', { name: /^Người nói câu lúc/ })).toHaveCount(t.segments.length)
        const speakers = row.locator('aside.transcript-speakers')
        await qa.expect(speakers.getByRole('heading', { name: /Người nói/ })).toBeVisible()
        await qa.expect(speakers.locator('span.select-text', { hasText: 'Người nói 1' })).toBeVisible()
        await qa.expect(speakers.locator('span.select-text', { hasText: 'Người nói 2' })).toBeVisible()
        await qa.expect(speakers.getByRole('button', { name: /^Tên của Người nói/ })).toHaveCount(t.speakers.length)
        qa.expect(t.segments.every((s) => s.endMs > s.startMs), 'a segment without a time span').toBe(true)
        await row.scrollIntoViewIfNeeded()
        await settled(qa)
        await qa.shot('thu-ky-doc-ban-go-bang')
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
      },
    },
    {
      id: 'AC27',
      role: 'secretary',
      title: 'Bản ghi im lặng và bản ghi chưa chuyển chữ có thông điệp riêng (TRANS_FT04)',
      steps: [
        'Thư ký tải "fe10-im-lang-<giờ>.wav" (im lặng) lên "[QA] FE-10 Họp trực tiếp gỡ băng" bằng API, chờ chuyển chữ xong, mở dòng bản ghi',
        'API: đọc transcript của mọi bản ghi trong cuộc họp',
      ],
      expected:
        'Bản im lặng tự chuyển chữ rồi hiện "Không nghe thấy lời nói nào trong bản ghi."; API Completed với 0 câu. Ghi nhận: "Bản ghi này chưa được chuyển thành chữ." không tới được qua tải lên — upload luôn tạo transcript Pending (API không bản ghi nào ở trạng thái None), xem Deviation trong note của case',
      async run(qa) {
        const m = await meeting(qa, 'A')
        const sec = as(qa, 'secretary')
        const c = checker(qa)
        const fileName = `fe10-im-lang-${Date.now().toString(36)}.wav`
        let createdId = null
        try {
          const up = await sec.form(
            `/api/meetings/${m.id}/recordings`,
            wavForm(audio(qa, 'sil'), fileName, { consentConfirmed: 'true', consentPurpose: 'Lập biên bản cuộc họp (QA FE-10)' }),
          )
          qa.expectStatus(up, 201)
          const list = await sec.get(`/api/meetings/${m.id}/recordings`)
          createdId = list.body.items.find((r) => r.fileName === fileName)?.id
          c.fact('the silent recording is listed', !!createdId)
          await waitTranscript(qa, createdId)
          const t = await transcript(qa, 'secretary', createdId)
          c.fact('silent and completed with no lines', t.status === 'Completed' && t.segments.length === 0, `${t.status} ${t.segments.length}`)
          // Mọi bản ghi đã tải lên đều có dòng transcript (Pending trở đi), không bao giờ ở None.
          for (const r of list.body.items) {
            const rt = await transcript(qa, 'secretary', r.id)
            c.fact(`${r.fileName} has a transcript row`, rt.status !== 'None', rt.status)
          }
          await openRecordings(qa, m)
          const row = recordingRow(qa, fileName)
          await qa.expect(row).toBeVisible({ timeout: 30_000 })
          await qa.expect(row.getByText('Không nghe thấy lời nói nào trong bản ghi.')).toBeVisible({ timeout: 30_000 })
          await row.scrollIntoViewIfNeeded()
          await settled(qa)
          await qa.shot('ban-ghi-im-lang')
          await noRawCodes(qa)
          c.done()
        } finally {
          if (createdId) await sec.delete(`/api/recordings/${createdId}`)
        }
      },
    },
    {
      id: 'AC28',
      role: 'secretary',
      title: 'Bản ghi lỗi hiện lý do và nút "Chạy lại" cho Thư ký; Thành viên thấy lỗi nhưng không có nút (TRANS_FT05)',
      steps: [
        'Thư ký tải "fe10-hong-<giờ>.wav" (byte ngẫu nhiên, ffmpeg không đọc được) lên "[QA] FE-10 Họp trực tiếp gỡ băng", chờ chuyển chữ lỗi, mở dòng bản ghi',
        'Đăng nhập thanhvien@, mở cùng dòng; API: thanhvien@ và chutoa@ POST …/transcribe',
      ],
      expected:
        'Dòng bản ghi có chip "Lỗi chuyển chữ"; panel đỏ "Chưa chuyển được thành chữ: Không đọc được tệp âm thanh." với nút "Chạy lại" cho Thư ký; Thành viên thấy cùng câu báo nhưng không có nút; API: Thành viên và Chủ toạ POST transcribe 403 (Chủ toạ MSG295)',
      async run(qa) {
        const m = await meeting(qa, 'A')
        const sec = as(qa, 'secretary')
        const c = checker(qa)
        const { fileName, id } = await ensureFailed(qa, m.id, 'ft05')
        try {
          await openRecordings(qa, m)
          const row = recordingRow(qa, fileName)
          await qa.expect(row).toBeVisible({ timeout: 30_000 })
          await qa.expect(row.getByText('Lỗi chuyển chữ')).toBeVisible({ timeout: 60_000 })
          await qa.expect(row.getByText('Chưa chuyển được thành chữ: Không đọc được tệp âm thanh.')).toBeVisible()
          await qa.expect(row.getByRole('button', { name: 'Chạy lại' })).toBeVisible()
          await row.scrollIntoViewIfNeeded()
          await settled(qa)
          await qa.shot('ban-ghi-loi-thu-ky')
          await qa.signIn('member')
          await openRecordings(qa, m)
          const mrow = recordingRow(qa, fileName)
          await qa.expect(mrow.getByText('Lỗi chuyển chữ')).toBeVisible({ timeout: 30_000 })
          await qa.expect(mrow.getByText('Chưa chuyển được thành chữ: Không đọc được tệp âm thanh.')).toBeVisible()
          await qa.expect(mrow.getByRole('button', { name: 'Chạy lại' })).toHaveCount(0)
          await mrow.scrollIntoViewIfNeeded()
          await settled(qa)
          await qa.shot('ban-ghi-loi-thanh-vien')
          c.check('Member reruns', await as(qa, 'member').post(`/api/recordings/${id}/transcribe`), 403)
          c.check('Chair reruns', await as(qa, 'chair').post(`/api/recordings/${id}/transcribe`), 403, 'MSG295')
          await noRawCodes(qa)
          c.done()
        } finally {
          await sec.delete(`/api/recordings/${id}`)
        }
      },
    },
    {
      id: 'AC29',
      role: 'secretary',
      title: 'Bấm "Chạy lại" ở bản ghi lỗi xếp hàng lại: toast MSG300, panel "Đang chờ…" (TRANS_FT13)',
      steps: [
        'Thư ký tải "fe10-hong-<giờ>.wav" (byte ngẫu nhiên) lên "[QA] FE-10 Họp trực tiếp gỡ băng", chờ lỗi, bấm "Chạy lại" ở dòng bản ghi',
        'Trong lúc đang chạy lại: API POST …/transcribe; chờ xong, xoá bản ghi tạm',
      ],
      expected:
        'Bấm "Chạy lại": nút đọc "Đang gửi…", toast "Đã đưa bản ghi vào hàng chờ chuyển thành chữ." (202 MSG300), panel chuyển "Đang chờ chuyển thành chữ…"; API POST lúc đang chạy 409 MSG301; bản hỏng lỗi lại như cũ rồi bị xoá',
      async run(qa) {
        const m = await meeting(qa, 'A')
        const sec = as(qa, 'secretary')
        const c = checker(qa)
        const { fileName, id } = await ensureFailed(qa, m.id, 'ft13')
        try {
          await openRecordings(qa, m)
          const row = recordingRow(qa, fileName)
          await qa.expect(row.getByText('Lỗi chuyển chữ')).toBeVisible({ timeout: 30_000 })
          const retry = row.getByRole('button', { name: 'Chạy lại' })
          await retry.scrollIntoViewIfNeeded()
          await press(qa, retry)
          await qa.expect(toast(qa, MSG.MSG300)).toBeVisible()
          // Pending mới đọc "Đang chờ lượt chuyển chữ" (TranscribeProgress, PO 23/11).
          await qa.expect(row.getByRole('status').filter({ hasText: 'Đang chờ lượt chuyển chữ' })).toBeVisible({ timeout: 15_000 })
          await settled(qa)
          await qa.shot('chay-lai-ban-loi')
          c.check('rerun while running', await sec.post(`/api/recordings/${id}/transcribe`), 409, 'MSG301')
          await waitFailed(qa, id)
          await qa.expect(row.getByText('Lỗi chuyển chữ')).toBeVisible({ timeout: 60_000 })
          await noRawCodes(qa)
          c.done()
        } finally {
          await sec.delete(`/api/recordings/${id}`)
        }
      },
    },
    {
      id: 'AC30',
      role: 'member',
      devices: ['desktop', 'phone'],
      title: 'Thành viên đọc bản gỡ băng như chữ thường: không bút, không ô chọn, không gợi ý máy, không nút tải (TRANS_FT06)',
      steps: [
        'Đăng nhập thanhvien@, mở tab "Bản ghi" của "[QA] FE-10 Họp trực tiếp gỡ băng", bấm "Nghe và xem bản gỡ băng" ở dòng "fe10-hop-chinh.wav"',
        'API (thanhvien@): GET transcript; PUT sửa câu, gán tên, đổi người nói một câu',
      ],
      expected:
        'Các câu và tên hiện như chữ thường ("Người nói 1/2", không gợi ý máy — suggested* null, people []), không nút "Sửa nội dung", không ô "Tên của…", không nút "Tải bản gỡ băng (.txt)"; API: canEdit/canExport false; sửa / gán / đổi 403 MSG295',
      async run(qa) {
        const m = await meeting(qa, 'A')
        await clearNames(qa, m.main.id)
        const c = checker(qa)
        const t = await transcript(qa, 'member', m.main.id)
        c.fact('canEdit is false', t.canEdit === false)
        c.fact('canExport is false', t.canExport === false)
        c.fact('people is empty for a Member', Array.isArray(t.people) && t.people.length === 0)
        c.fact('no machine guess reaches a Member', t.speakers.every((s) => s.suggestedUserId == null && s.suggestedName == null && s.suggestionScore == null))
        c.fact('unnamed voices read "Người nói N"', t.speakers.every((s) => /^Người nói \d+$/.test(s.assignedName ?? `Người nói ${Number(/SPEAKER_(\d+)/.exec(s.label)?.[1] ?? 0) + 1}`)))
        const seg = t.segments[0]
        c.check('edit text', await as(qa, 'member').put(`/api/transcript-segments/${seg.id}`, { text: 'Thành viên sửa' }), 403, 'MSG295')
        c.check('name a voice', await as(qa, 'member').put(`/api/recordings/${m.main.id}/speakers/${seg.speakerLabel}`, { userId: null, guestId: null }), 403, 'MSG295')
        await openRecordings(qa, m)
        const row = recordingRow(qa, FILES.main)
        await qa.expect(lines(row).locator('.tl-say')).toHaveCount(t.segments.length)
        await qa.expect(lines(row).locator('strong.tl-name').filter({ hasText: /Người nói \d/ }).first()).toBeVisible()
        await qa.expect(row.getByRole('button', { name: /^Tên của/ })).toHaveCount(0)
        await qa.expect(row.getByRole('button', { name: 'Sửa nội dung' })).toHaveCount(0)
        await qa.expect(row.getByRole('button', { name: 'Tải bản gỡ băng (.txt)' })).toHaveCount(0)
        await qa.expect(row.getByRole('button', { name: 'Chuyển thành chữ lại' })).toHaveCount(0)
        await row.scrollIntoViewIfNeeded()
        await settled(qa)
        await qa.shot('thanh-vien-chi-doc-ban-go-bang')
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
        c.done()
      },
    },
    {
      id: 'AC31',
      role: 'secretary',
      title: 'Bản ghi theo micro của cuộc họp trực tuyến: xem trực tiếp, lọc theo người nói, xuất .txt thiếu câu đã bỏ; Thành viên không có nút tải (TRANS_FT09)',
      steps: [
        'Thư ký mở tab "Bản ghi" của "[QA] FE-10 Họp trực tuyến theo micro" (đã kết thúc, 3 câu 2 người nói)',
        'Bấm "Xem <n> lượt của <tên>" rồi "Bỏ lọc" ở danh sách "Người nói"',
        'Chủ toạ (API) bỏ một câu, Thư ký tải "Tải lời thoại (.txt)", rồi khôi phục; Thành viên mở cùng thẻ',
      ],
      expected:
        'Thẻ "Bản ghi cuộc họp" đếm số câu; mỗi câu có mốc giờ, người nói, chữ; lọc theo người chỉ hiện câu của người đó, "Bỏ lọc" hiện lại đủ; tệp .txt thiếu câu đã bỏ, khôi phục thì đủ lại; Thành viên thấy thẻ nhưng không có nút "Tải lời thoại (.txt)"',
      async run(qa) {
        const cm = await meeting(qa, 'C')
        const u = await users(qa)
        const c = checker(qa)
        const all = await clips(qa, 'secretary', cm.id)
        c.fact('three sentences', all.items.length >= 3, String(all.items.length))
        c.fact('two speakers', new Set(all.items.map((x) => x.speakerId)).size >= 2)
        const target = all.items.find((x) => x.speakerId === u.a.id)
        qa.expect(target, 'no sentence of "[QA] FE-10 Người nói A"').toBeTruthy()
        try {
          if (target.excluded) await as(qa, 'chair').put(`/api/speech-clips/${target.id}/exclusion`, { excluded: false })
          await openRecordings(qa, cm)
          const card = qa.page.locator('section.tl-card')
          await qa.expect(card.getByRole('heading', { name: 'Bản ghi cuộc họp' })).toBeVisible()
          const panel = card.locator('aside.tl-speakers')
          await qa.expect(panel.getByRole('heading', { name: /Người nói/ })).toBeVisible()
          await qa.expect(card.locator('.tl-count')).toHaveText(String(all.items.filter((x) => !x.excluded).length))
          const filterBtn = panel.getByRole('button', { name: new RegExp(`^Xem \\d+ lượt của ${esc(u.a.name)}$`) })
          await qa.expect(filterBtn).toBeVisible()
          await press(qa, filterBtn)
          await qa.expect(card.locator('li.tl-group').first()).toBeVisible()
          qa.expect(await card.locator('li.tl-group').count(), 'the filter shows only the chosen paragraphs').toBeLessThanOrEqual(2)
          await qa.expect(card.getByText(target.text.slice(0, 20))).toBeVisible()
          await settled(qa)
          await qa.shot('loc-theo-nguoi-noi')
          await press(qa, panel.getByRole('button', { name: 'Bỏ lọc' }))
          await qa.expect(card.locator('li.tl-group').first()).toBeVisible()
          c.check('Chair excludes', await as(qa, 'chair').put(`/api/speech-clips/${target.id}/exclusion`, { excluded: true, reason: 'Other' }), 200, 'MSG351')
          // Toàn văn câu (kể cả cách viết số) mới định danh được nó: Thành viên có một câu khác gần như y hệt.
          const words = target.text
          const txt = await as(qa, 'secretary').get(`/api/meetings/${cm.id}/speech-clips.txt`)
          c.check('export', txt, 200)
          c.fact('the excluded sentence is left out of the file', !txt.text.includes(words), words)
          c.check('Chair restores', await as(qa, 'chair').put(`/api/speech-clips/${target.id}/exclusion`, { excluded: false }), 200, 'MSG352')
          const back = await as(qa, 'secretary').get(`/api/meetings/${cm.id}/speech-clips.txt`)
          c.fact('the restored sentence is back in the file', back.text.includes(words), words.slice(0, 60))
          await qa.signIn('member')
          await openRecordings(qa, cm)
          await qa.expect(qa.page.locator('section.tl-card').getByRole('button', { name: 'Tải lời thoại (.txt)' })).toHaveCount(0)
          await settled(qa)
          await qa.shot('thanh-vien-khong-nut-tai-loi-thoai')
          c.done()
        } finally {
          await as(qa, 'chair').put(`/api/speech-clips/${target.id}/exclusion`, { excluded: false })
        }
      },
    },
    {
      id: 'AC32',
      role: 'member',
      devices: ['desktop', 'phone'],
      title: 'Thành viên không chạm được vào tên/giọng: danh sách tên là chữ, câu không bút không biểu tượng người; gọi thẳng 403 MSG295 (TRANS_FT22)',
      steps: [
        'Đăng nhập thanhvien@, mở tab "Bản ghi" của "[QA] FE-10 Họp trực tiếp gỡ băng", bấm "Nghe và xem bản gỡ băng", nhìn thẻ người nói và các câu',
        'API (thanhvien@): PUT gán tên một giọng, PUT đổi người nói một câu',
      ],
      expected:
        'Thẻ người nói hiện tên/chữ ("Người nói N") không có ô chọn; câu không có bút "Sửa nội dung", không có nút "Đổi người nói của riêng câu này"; API gán/đổi đều 403 MSG295',
      async run(qa) {
        const m = await meeting(qa, 'A')
        await clearNames(qa, m.main.id)
        const c = checker(qa)
        const t = await transcript(qa, 'secretary', m.main.id)
        const seg = t.segments[0]
        await openRecordings(qa, m)
        const row = recordingRow(qa, FILES.main)
        await qa.expect(lines(row).locator('.tl-say')).toHaveCount(t.segments.length)
        await qa.expect(row.locator('aside.transcript-speakers').getByRole('button', { name: /^Tên của/ })).toHaveCount(0)
        await qa.expect(row.getByRole('button', { name: 'Sửa nội dung' })).toHaveCount(0)
        await qa.expect(row.getByRole('button', { name: /^Đổi người nói/ })).toHaveCount(0)
        await qa.expect(lines(row).locator('strong.tl-name').filter({ hasText: /Người nói \d/ }).first()).toBeVisible()
        await row.scrollIntoViewIfNeeded()
        await settled(qa)
        await qa.shot('thanh-vien-khong-cong-cu-ten')
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
        c.check('Member names a voice', await as(qa, 'member').put(`/api/recordings/${m.main.id}/speakers/${seg.speakerLabel}`, { userId: (await users(qa)).member.id, guestId: null }), 403, 'MSG295')
        c.check('Member moves one sentence', await as(qa, 'member').put(`/api/transcript-segments/${seg.id}/speaker`, { label: seg.speakerLabel }), 403, 'MSG295')
        c.done()
      },
    },
    {
      id: 'AC33',
      role: 'member',
      title: 'Người nói rút câu của mình từ "Ghi âm của tôi": xác nhận rồi xoá, câu biến mất với mọi người và không còn nghe lại được (TRANS_FT27)',
      steps: [
        'thanhvien@ gửi một câu tạm ("Xin chào.") vào "[QA] FE-10 Họp trực tuyến theo micro" bằng API, mở tab "Bản ghi", bấm "Ghi âm của tôi"',
        'Bấm thùng rác ở câu tạm: đọc hộp "Xoá câu này?", bấm "Giữ lại" (câu còn), bấm lại rồi xác nhận "Xoá câu"',
        'API: danh sách câu của Thành viên, Chủ toạ, Thư ký; nghe lại câu đã xoá',
      ],
      expected:
        'Hộp "Ghi âm của tôi" liệt kê câu tạm với chip "Đã chuyển chữ"; "Giữ lại" giữ câu; xác nhận "Xoá câu": toast "Đã xoá câu nói." (200 MSG322), câu biến mất khỏi hộp và khỏi bản ghi của mọi vai trò, nghe lại 404; các câu khác còn nguyên. Ghi nhận: trạng thái trống "Chưa có câu nào" không tới được — nút "Ghi âm của tôi" chỉ hiện khi đã có câu/pending của mình, xem Deviation trong note của case',
      async run(qa) {
        const cm = await meeting(qa, 'C')
        const c = checker(qa)
        const temp = await sendClip(qa, 'member', cm.id, 'short')
        const tempFresh = (await clips(qa, 'member', cm.id)).items.find((x) => x.id === temp.id)
        const tempText = (temp.text ?? tempFresh?.text ?? '').trim()
        qa.expect(tempText, 'the temporary sentence has no text').not.toBe('')
        try {
          await openRecordings(qa, cm)
          const mineBtn = qa.page.locator('section.tl-card').getByRole('button', { name: /Ghi âm của tôi/ })
          await qa.expect(mineBtn).toBeVisible()
          await press(qa, mineBtn)
          const dialog = qa.page.getByRole('dialog', { name: 'Ghi âm của tôi' })
          await qa.expect(dialog).toBeVisible()
          const item = dialog.locator('li.my-clip').filter({ hasText: tempText.slice(0, 15) })
          await qa.expect(item.getByText('Đã chuyển chữ')).toBeVisible()
          await settled(qa)
          await qa.shot('ghi-am-cua-toi')
          await press(qa, item.getByRole('button', { name: 'Xoá câu này' }))
          const confirm = qa.page.getByRole('dialog', { name: 'Xoá câu này?' })
          await qa.expect(confirm).toBeVisible()
          await qa.expect(confirm.getByText('Câu và âm thanh của nó bị xoá khỏi cuộc họp.')).toBeVisible()
          await settled(qa)
          await qa.shot('xac-nhan-xoa-cau-cua-minh')
          await press(qa, confirm.getByRole('button', { name: 'Giữ lại' }))
          await qa.expect(confirm).toHaveCount(0)
          await qa.expect(dialog.locator('li.my-clip').filter({ hasText: tempText.slice(0, 15) })).toHaveCount(1)
          await press(qa, item.getByRole('button', { name: 'Xoá câu này' }))
          await qa.expect(qa.page.getByRole('dialog', { name: 'Xoá câu này?' })).toBeVisible()
          const gone = qa.page.waitForResponse((r) => r.url().includes(`/api/speech-clips/${temp.id}`) && r.request().method() === 'DELETE')
          await press(qa, qa.page.getByRole('dialog', { name: 'Xoá câu này?' }).getByRole('button', { name: 'Xoá câu', exact: true }))
          c.fact('deleted as MSG322', (await gone).status() === 200, `HTTP ${(await gone).status()}`)
          await qa.expect(toast(qa, 'Đã xoá câu nói.')).toBeVisible()
          await qa.expect(dialog.locator('li.my-clip').filter({ hasText: tempText.slice(0, 15) })).toHaveCount(0)
          await settled(qa)
          await qa.shot('cau-cua-minh-da-xoa')
          for (const who of ['member', 'chair', 'secretary'])
            c.fact(`gone for ${who}`, !(await clips(qa, who, cm.id)).items.some((x) => x.id === temp.id))
          c.check('playback after delete', await as(qa, 'member').get(`/api/speech-clips/${temp.id}/playback`), 404)
          const left = (await clips(qa, 'secretary', cm.id)).items
          c.fact('the other sentences are untouched', left.length >= 3, String(left.length))
          c.done()
        } finally {
          await as(qa, 'secretary').delete(`/api/speech-clips/${temp.id}`).catch(() => undefined)
        }
      },
    },
    {
      id: 'AC34',
      role: 'secretary',
      title: 'Thư ký xin dự thảo AI lần đầu: toast MSG384, chip "Đang chờ"/"AI đang viết", dự thảo hiện với nhãn rà soát và "Chưa duyệt" (TRANS_FT29)',
      steps: [
        'Thư ký mở tab "Biên bản" của "[QA] FE-10 Họp trực tiếp gỡ băng" (đã kết thúc, có lời thoại), bấm "Tạo dự thảo" ở thẻ "Dự thảo nội dung"',
        'Chờ AI viết xong (poll API); Thành viên gọi POST …/summary',
      ],
      expected:
        'Bấm "Tạo dự thảo": toast "Đã gửi yêu cầu. Dự thảo sẽ tự hiện khi AI viết xong." (202 MSG384), chip "Đang chờ" rồi "AI đang viết"; khi xong thẻ hiện nhãn "Nội dung do AI tạo — cần rà soát" và chip "Chưa duyệt"; Thành viên POST 403 MSG385. Ghi nhận: code chỉ cho cuộc họp đã kết thúc (409 MSG386) và tab chỉ mở khi đã kết thúc — case ghi InProgress là chưa đúng code, xem Deviation trong note của case',
      async run(qa) {
        const m = await meeting(qa, 'A')
        const c = checker(qa)
        const sec = as(qa, 'secretary')
        const before = (await sec.get(`/api/meetings/${m.id}/summary`)).body
        c.fact('the Secretary manages and the AI is ready', before.canManage === true && before.aiReady === true, JSON.stringify({ canManage: before.canManage, aiReady: before.aiReady }))
        c.check('Member asks for a draft', await as(qa, 'member').post(`/api/meetings/${m.id}/summary`, {}), 403, 'MSG385')
        if (!before.summary || before.summary.status === 'Failed') {
          await qa.goto(`/mgmt/meetings/${m.id}?tab=minutes`)
          await qa.expect(qa.page.getByRole('heading', { level: 1, name: m.title })).toBeVisible({ timeout: 15_000 })
          const card = qa.page.getByRole('region', { name: 'Dự thảo nội dung' })
          await qa.expect(card).toBeVisible()
          const ask = card.getByRole('button', { name: 'Tạo dự thảo' })
          await qa.expect(ask).toBeEnabled()
          await press(qa, ask)
          await qa.expect(toast(qa, MSG.MSG384)).toBeVisible()
          await qa.expect(card.getByText('Đang chờ')).toBeVisible({ timeout: 15_000 })
          await settled(qa)
          await qa.shot('da-gui-yeu-cau-du-thao')
        } else qa.note(`Dự thảo đã có sẵn (${before.summary.status}): bỏ qua bước bấm nút, kiểm phần hiện.`)
        const until = Date.now() + 900_000
        let state = before
        for (;;) {
          state = (await sec.get(`/api/meetings/${m.id}/summary`)).body
          if (state.summary?.status !== 'Queued' && state.summary?.status !== 'Running') break
          if (Date.now() > until) qa.inconclusive('AI chưa viết xong sau 15 phút.')
          await new Promise((r) => setTimeout(r, 10000))
        }
        c.fact('the draft is ready', state.summary?.status === 'Ready', state.summary?.status)
        c.fact('the draft has three sections', !!(state.summary?.draft?.objectives && state.summary?.draft?.discussion && state.summary?.draft?.conclusions))
        await qa.goto(`/mgmt/meetings/${m.id}?tab=minutes`)
        await qa.expect(qa.page.getByRole('heading', { level: 1, name: m.title })).toBeVisible({ timeout: 15_000 })
        const card = qa.page.getByRole('region', { name: 'Dự thảo nội dung' })
        await qa.expect(card.getByText('Nội dung do AI tạo — cần rà soát')).toBeVisible({ timeout: 60_000 })
        await qa.expect(card.getByText('Chưa duyệt')).toBeVisible()
        await card.scrollIntoViewIfNeeded()
        await settled(qa)
        await qa.shot('du-thao-ai-dau-tien')
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
        c.done()
      },
    },
    {
      id: 'AC35',
      role: 'secretary',
      title: 'Ghi chú sẵn sàng liệt kê điều dự thảo sẽ thiếu: giọng chưa gán tên, bản ghi lỗi; kèm nút "Gán tên người nói" (TRANS_FT30)',
      steps: [
        'Thư ký mở tab "Biên bản" của "[QA] FE-10 Họp Mật gỡ băng" (đã kết thúc, 1 giọng chưa gán tên), đọc ghi chú dưới thẻ "Dự thảo nội dung"',
        'API GET …/summary (đối chiếu readiness); tải một bản ghi hỏng lên D, chờ lỗi, đọc lại ghi chú, rồi xoá bản hỏng',
      ],
      expected:
        'Ghi chú có biểu tượng cảnh báo: "1 giọng chưa gán tên sẽ hiện là “Người nói N” trong dự thảo." với nút "Gán tên người nói"; API readiness: sentences > 0, unnamedVoices = 1, transcribing = 0, failedTranscripts = 0; thêm bản hỏng: "1 bản ghi chuyển chữ lỗi, không được tính."; xoá bản hỏng thì dòng đó mất. Ghi nhận: các ghi chú khác chỉ hiện khi đúng điều kiện (đang chuyển chữ / có nội dung chưa bắt đầu / chưa có lời thoại), xem Deviation trong note của case',
      async run(qa) {
        const d = await meeting(qa, 'D')
        const sec = as(qa, 'secretary')
        const c = checker(qa)
        await clearNames(qa, d.rec.id)
        const read = (await sec.get(`/api/meetings/${d.id}/summary`)).body
        c.fact('the Secretary manages and sees readiness', read.canManage === true && read.readiness != null)
        c.fact('sentences said', read.readiness.sentences > 0, String(read.readiness.sentences))
        c.fact('one unnamed voice', read.readiness.unnamedVoices === 1, String(read.readiness.unnamedVoices))
        c.fact('nothing transcribing or failed', read.readiness.transcribing === 0 && read.readiness.failedTranscripts === 0)
        await qa.goto(`/mgmt/meetings/${d.id}?tab=minutes`)
        await qa.expect(qa.page.getByRole('heading', { level: 1, name: d.title })).toBeVisible({ timeout: 15_000 })
        const card = qa.page.getByRole('region', { name: 'Dự thảo nội dung' })
        const readiness = card.locator('.minutes-readiness')
        await qa.expect(readiness.getByText('1 giọng chưa gán tên sẽ hiện là “Người nói N” trong dự thảo.')).toBeVisible()
        await qa.expect(readiness.getByRole('button', { name: 'Gán tên người nói' })).toBeVisible()
        await card.scrollIntoViewIfNeeded()
        await settled(qa)
        await qa.shot('ghi-chu-san-sang')
        const bad = await ensureFailed(qa, d.id, 'ft30')
        try {
          await qa.page.reload()
          await qa.expect(qa.page.getByRole('heading', { level: 1, name: d.title })).toBeVisible({ timeout: 15_000 })
          const again = qa.page.getByRole('region', { name: 'Dự thảo nội dung' }).locator('.minutes-readiness')
          await qa.expect(again.getByText('1 bản ghi chuyển chữ lỗi, không được tính.')).toBeVisible({ timeout: 30_000 })
          await settled(qa)
          await qa.shot('ghi-chu-ban-loi')
          c.fact('failedTranscripts = 1', ((await sec.get(`/api/meetings/${d.id}/summary`)).body.readiness.failedTranscripts) === 1)
        } finally {
          await sec.delete(`/api/recordings/${bad.id}`)
        }
        await noRawCodes(qa)
        c.done()
      },
    },
    {
      id: 'AC36',
      role: 'secretary',
      title: 'Thư ký gán tên các giọng ngay từ thẻ dự thảo rồi mới tạo: đếm "Còn <n>…", "Để sau" không tạo, Chủ toạ chỉ đọc (TRANS_FT31)',
      steps: [
        'Thư ký mở tab "Biên bản" của "[QA] FE-10 Họp Mật gỡ băng", bấm "Gán tên người nói": đọc sheet rồi bấm "Để sau" (không tạo)',
        'Chủ toạ mở cùng sheet (chỉ đọc)',
        'Thư ký mở lại, nghe thử "Nghe câu này", chọn đúng người cho giọng, bấm "Tạo dự thảo"',
      ],
      expected:
        'Sheet "Gán tên người nói" ghi "Nghe lời mẫu dưới mỗi giọng rồi chọn đúng người…", đếm "Còn 1 giọng chưa có tên"; "Để sau" đóng sheet mà không tạo dự thảo; Chủ toạ thấy "Chỉ Thư ký cuộc họp gán tên được."; Thư ký chọn tên → "Mọi giọng đã có tên.", "Tạo dự thảo" xếp hàng (toast MSG384, API Queued/Running/Ready)',
      async run(qa) {
        const d = await meeting(qa, 'D')
        const u = await users(qa)
        const sec = as(qa, 'secretary')
        const c = checker(qa)
        await clearNames(qa, d.rec.id)
        try {
          await qa.goto(`/mgmt/meetings/${d.id}?tab=minutes`)
          await qa.expect(qa.page.getByRole('heading', { level: 1, name: d.title })).toBeVisible({ timeout: 15_000 })
          const card = qa.page.getByRole('region', { name: 'Dự thảo nội dung' })
          await press(qa, card.getByRole('button', { name: 'Gán tên người nói' }))
          const sheet = qa.page.getByRole('dialog', { name: 'Gán tên người nói' })
          await qa.expect(sheet).toBeVisible()
          await qa.expect(sheet.getByText(/Nghe lời mẫu dưới mỗi giọng rồi chọn đúng người/)).toBeVisible()
          await qa.expect(sheet.getByText('Còn 1 giọng chưa có tên')).toBeVisible()
          await qa.expect(sheet.getByRole('button', { name: 'Nghe câu này' }).first()).toBeVisible()
          await settled(qa)
          await qa.shot('sheet-gan-ten-nguoi-noi')
          // "Để sau" đóng sheet mà không tạo dự thảo (kiểm trước khi gán, vì gán xong nút mở sheet không còn).
          await press(qa, sheet.getByRole('button', { name: 'Để sau' }))
          await qa.expect(sheet).toHaveCount(0)
          c.fact('no draft queued by "Để sau"', ((await sec.get(`/api/meetings/${d.id}/summary`)).body.summary) == null)
          // Chủ toạ chỉ đọc: còn 1 giọng chưa tên nên gợi ý hiện.
          await qa.signIn('chair')
          await qa.goto(`/mgmt/meetings/${d.id}?tab=minutes`)
          await qa.expect(qa.page.getByRole('heading', { level: 1, name: d.title })).toBeVisible({ timeout: 15_000 })
          await press(qa, qa.page.getByRole('region', { name: 'Dự thảo nội dung' }).getByRole('button', { name: 'Gán tên người nói' }))
          const chairSheet = qa.page.getByRole('dialog', { name: 'Gán tên người nói' })
          await qa.expect(chairSheet.getByText('Chỉ Thư ký cuộc họp gán tên được.')).toBeVisible()
          await settled(qa)
          await qa.shot('chu-toa-chi-doc-sheet')
          // Thư ký gán tên rồi tạo dự thảo ngay trong sheet.
          await qa.signIn('secretary')
          await qa.goto(`/mgmt/meetings/${d.id}?tab=minutes`)
          await qa.expect(qa.page.getByRole('heading', { level: 1, name: d.title })).toBeVisible({ timeout: 15_000 })
          const card2 = qa.page.getByRole('region', { name: 'Dự thảo nội dung' })
          await press(qa, card2.getByRole('button', { name: 'Gán tên người nói' }))
          const sheet2 = qa.page.getByRole('dialog', { name: 'Gán tên người nói' })
          const trigger = sheet2.getByRole('button', { name: /^Tên của Người nói/ })
          await press(qa, trigger)
          const list = qa.page.getByRole('listbox', { name: /^Tên của/ }).last()
          await qa.expect(list).toBeVisible()
          await press(qa, list.getByRole('option', { name: new RegExp(`^${esc(u.member.name)}`) }))
          await qa.expect(sheet2.getByText('Mọi giọng đã có tên.')).toBeVisible({ timeout: 15_000 })
          await settled(qa)
          await qa.shot('moi-giong-da-co-ten')
          await press(qa, sheet2.getByRole('button', { name: 'Tạo dự thảo' }))
          await qa.expect(toast(qa, MSG.MSG384)).toBeVisible()
          const queued = (await sec.get(`/api/meetings/${d.id}/summary`)).body.summary
          c.fact('the draft is queued', ['Queued', 'Running', 'Ready'].includes(queued?.status), queued?.status)
          c.done()
        } finally {
          await clearNames(qa, d.rec.id)
        }
      },
    },
    {
      id: 'AC37',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Thư ký đọc dự thảo 3 mục gấp: tiêu đề I/II/III, mở tại chỗ, dòng "Lấy từ bản ghi", chân "AI viết lúc" (TRANS_FT36)',
      steps: [
        'Thư ký mở tab "Biên bản" của "[QA] FE-10 Họp trực tiếp gỡ băng" (đã kết thúc, dự thảo Ready), đọc thẻ "Dự thảo nội dung"',
        'Bấm từng tiêu đề mục, đọc dòng nguồn và chân trang; đối chiếu API GET …/summary',
      ],
      expected:
        'Tiêu đề "I. Mục tiêu và phạm vi cuộc họp", "II. Nội dung thảo luận và đánh giá", "III. Kết luận và phân công thực hiện", mỗi mục gấp ở dòng đầu (hoặc "Chưa có nội dung"), bấm mở tại chỗ. Dưới mục II dòng "Lấy từ bản ghi:" liệt kê từng nội dung kèm khoảng giờ (tooltip "<n> câu nói"). Chân trang "AI viết lúc <time> (<model>)". API: status Ready, đủ 3 mục.',
      async run(qa) {
        const m = await meeting(qa, 'A')
        const c = checker(qa)
        const state = (await as(qa, 'secretary').get(`/api/meetings/${m.id}/summary`)).body
        c.fact('the draft is Ready', state.summary?.status === 'Ready', state.summary?.status)
        c.fact('the draft has three sections', !!(state.summary?.draft?.objectives && state.summary?.draft?.discussion && state.summary?.draft?.conclusions))
        await qa.goto(`/mgmt/meetings/${m.id}?tab=minutes`)
        await qa.expect(qa.page.getByRole('heading', { level: 1, name: m.title })).toBeVisible({ timeout: 15_000 })
        const card = qa.page.getByRole('region', { name: 'Dự thảo nội dung' })
        await qa.expect(card.getByText('I. Mục tiêu và phạm vi cuộc họp')).toBeVisible()
        await qa.expect(card.getByText('II. Nội dung thảo luận và đánh giá')).toBeVisible()
        await qa.expect(card.getByText('III. Kết luận và phân công thực hiện')).toBeVisible()
        await settled(qa)
        await qa.shot('du-thao-3-muc-gap')
        for (const h of ['I. Mục tiêu và phạm vi cuộc họp', 'II. Nội dung thảo luận và đánh giá', 'III. Kết luận và phân công thực hiện']) {
          await press(qa, card.getByRole('button', { name: new RegExp(esc(h.split('. ')[1])) }))
          await settled(qa)
        }
        await qa.expect(card.getByText('Lấy từ bản ghi:')).toBeVisible()
        const src = card.locator('.minutes-source[data-tooltip]').first()
        await qa.expect(src).toBeVisible()
        c.fact('a source names its sentence count', /câu nói/.test(await src.getAttribute('data-tooltip') ?? ''))
        await qa.expect(card.getByText(/AI viết lúc/)).toBeVisible()
        c.fact('the footer names the model', /qwen2\.5/.test(await card.locator('.minutes-meta').innerText()))
        await settled(qa)
        await qa.shot('du-thao-mo-tai-cho-nguon')
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
        c.done()
      },
    },
    {
      id: 'AC38',
      role: 'secretary',
      title: 'Thư ký sửa mục II: thanh lưu hiện, "Lưu dự thảo" toast MSG388, chân thêm người sửa, chip "Chưa duyệt" (TRANS_FT38)',
      steps: [
        'Thư ký mở tab "Biên bản" của "[QA] FE-10 Họp trực tiếp gỡ băng", mở mục II, thêm chữ',
        'Bấm "Lưu dự thảo"; đối chiếu API PUT …/summary (200 MSG388) và tải lại',
      ],
      expected:
        'Đang sửa: thanh ghim "Có thay đổi chưa lưu" với "Hoàn tác" và "Lưu dự thảo". "Lưu dự thảo" hiện "Đang lưu…" rồi toast "Đã lưu dự thảo." (200 MSG388); thanh ẩn, chân trang thêm ", <name> sửa lúc <time>", chip "Chưa duyệt". Tải lại vẫn là chữ đã sửa.',
      async run(qa) {
        const m = await meeting(qa, 'A')
        const sec = as(qa, 'secretary')
        const c = checker(qa)
        const before = (await sec.get(`/api/meetings/${m.id}/summary`)).body.summary
        c.fact('the draft is Ready and unreviewed', before?.status === 'Ready' && !before?.reviewedAt, before?.status)
        await qa.goto(`/mgmt/meetings/${m.id}?tab=minutes`)
        await qa.expect(qa.page.getByRole('heading', { level: 1, name: m.title })).toBeVisible({ timeout: 15_000 })
        const card = qa.page.getByRole('region', { name: 'Dự thảo nội dung' })
        await press(qa, card.getByRole('button', { name: /Nội dung thảo luận và đánh giá/ }))
        const box = card.locator('textarea#outline-discussion')
        await qa.expect(box).toBeVisible()
        const orig = await box.inputValue()
        await box.fill(`${orig} (QA kiểm thử)`)
        const bar = qa.page.getByRole('region', { name: 'Thay đổi dự thảo' })
        await qa.expect(bar.getByText('Có thay đổi chưa lưu')).toBeVisible()
        await settled(qa)
        await qa.shot('thanh-luu-du-thao')
        await press(qa, bar.getByRole('button', { name: 'Lưu dự thảo' }))
        await qa.expect(toast(qa, MSG.MSG388)).toBeVisible({ timeout: 15_000 })
        await qa.expect(bar.getByText('Có thay đổi chưa lưu')).toHaveCount(0)
        await qa.expect(card.getByText('Chưa duyệt')).toBeVisible()
        const after = (await sec.get(`/api/meetings/${m.id}/summary`)).body.summary
        c.fact('the edit is saved with the editor named', after?.draft?.discussion?.endsWith('(QA kiểm thử)') === true && !!after?.editedBy)
        await qa.expect(card.getByText(/sửa lúc/)).toBeVisible()
        await qa.page.reload()
        await qa.expect(qa.page.getByRole('heading', { level: 1, name: m.title })).toBeVisible({ timeout: 15_000 })
        const again = qa.page.getByRole('region', { name: 'Dự thảo nội dung' })
        await press(qa, again.getByRole('button', { name: /Nội dung thảo luận và đánh giá/ }))
        await qa.expect(again.locator('textarea#outline-discussion')).toHaveValue(/\(QA kiểm thử\)$/)
        await settled(qa)
        await qa.shot('du-thao-da-luu')
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
        c.done()
      },
    },
    {
      id: 'AC39',
      role: 'secretary',
      title: 'Thư ký "Hoàn tác": hộp "Bỏ các thay đổi?" rồi chữ về như cũ, không gửi gì (TRANS_FT40)',
      steps: [
        'Thư ký mở tab "Biên bản" của "[QA] FE-10 Họp trực tiếp gỡ băng", mở mục I, thêm chữ',
        'Bấm "Hoàn tác" → hộp xác nhận → "Bỏ thay đổi"; đối chiếu API (chữ và editedAt không đổi)',
      ],
      expected:
        'Hộp "Bỏ các thay đổi?" ghi "Dự thảo trở về bản đã lưu lần cuối.". "Bỏ thay đổi" đóng hộp, ô soạn về đúng chữ đã lưu, thanh lưu ẩn, không request nào gửi (API: discussion và editedAt giữ nguyên).',
      async run(qa) {
        const m = await meeting(qa, 'A')
        const sec = as(qa, 'secretary')
        const c = checker(qa)
        const before = (await sec.get(`/api/meetings/${m.id}/summary`)).body.summary
        c.fact('the draft is Ready', before?.status === 'Ready', before?.status)
        await qa.goto(`/mgmt/meetings/${m.id}?tab=minutes`)
        await qa.expect(qa.page.getByRole('heading', { level: 1, name: m.title })).toBeVisible({ timeout: 15_000 })
        const card = qa.page.getByRole('region', { name: 'Dự thảo nội dung' })
        await press(qa, card.getByRole('button', { name: /Mục tiêu và phạm vi/ }))
        const box = card.locator('textarea#outline-objectives')
        await qa.expect(box).toBeVisible()
        const orig = await box.inputValue()
        await box.fill(`${orig} (nháp sẽ hoàn tác)`)
        const bar = qa.page.getByRole('region', { name: 'Thay đổi dự thảo' })
        await qa.expect(bar.getByText('Có thay đổi chưa lưu')).toBeVisible()
        await press(qa, bar.getByRole('button', { name: 'Hoàn tác' }))
        const dialog = qa.page.getByRole('dialog', { name: 'Bỏ các thay đổi?' })
        await qa.expect(dialog.getByText('Dự thảo trở về bản đã lưu lần cuối.')).toBeVisible()
        await settled(qa)
        await qa.shot('xac-nhan-bo-thay-doi')
        await press(qa, dialog.getByRole('button', { name: 'Bỏ thay đổi' }))
        await qa.expect(bar.getByText('Có thay đổi chưa lưu')).toHaveCount(0)
        await qa.expect(box).toHaveValue(orig)
        const after = (await sec.get(`/api/meetings/${m.id}/summary`)).body.summary
        c.fact('nothing was sent', after?.draft?.objectives === before?.draft?.objectives && after?.editedAt === before?.editedAt)
        await settled(qa)
        await qa.shot('du-thao-ve-nhu-cu')
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
        c.done()
      },
    },
    {
      id: 'AC40',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Thành viên chỉ thấy dự thảo sau khi duyệt: trước không có thẻ, sau đọc 3 mục thuần túy không nguồn (TRANS_FT37)',
      steps: [
        'Đảm bảo "[QA] FE-10 Họp Mật gỡ băng" có dự thảo Ready chưa duyệt (duyệt rồi thì "Tạo lại" qua API); Thành viên mở tab "Biên bản"',
        'Thư ký bấm "Duyệt"; Thành viên tải lại, đọc thẻ đã duyệt',
      ],
      expected:
        'Chưa duyệt: Thành viên không thấy thẻ "Dự thảo nội dung". Thư ký "Duyệt" → toast "Đã duyệt dự thảo." (200 MSG391), chip "Đã duyệt" (tooltip "<name> duyệt lúc <time>"). Thành viên tải lại: thẻ hiện chip "Đã duyệt", 3 mục là chữ thuần (tiêu đề phụ số in đậm), không ô soạn, không dòng "Lấy từ bản ghi:", không nút "Tạo lại".',
      async run(qa) {
        const m = await meeting(qa, 'D')
        const sec = as(qa, 'secretary')
        const c = checker(qa)
        let state = (await sec.get(`/api/meetings/${m.id}/summary`)).body
        if (!state.summary || state.summary.status !== 'Ready') qa.inconclusive('Cuộc họp Mật chưa có dự thảo Ready để duyệt.')
        if (state.summary.reviewedAt) {
          c.fact('re-ask to get an unreviewed draft', (await sec.post(`/api/meetings/${m.id}/summary`, {})).status === 202)
          const until = Date.now() + 900_000
          for (;;) {
            state = (await sec.get(`/api/meetings/${m.id}/summary`)).body
            if (state.summary?.status !== 'Queued' && state.summary?.status !== 'Running') break
            if (Date.now() > until) qa.inconclusive('AI chưa viết xong sau 15 phút.')
            await new Promise((r) => setTimeout(r, 10000))
          }
        }
        c.fact('the draft is Ready and unreviewed', state.summary?.status === 'Ready' && !state.summary?.reviewedAt)
        await qa.signIn('member')
        await qa.goto(`/mgmt/meetings/${m.id}?tab=minutes`)
        await qa.expect(qa.page.getByRole('heading', { level: 1, name: m.title })).toBeVisible({ timeout: 15_000 })
        await qa.expect(qa.page.getByRole('region', { name: 'Dự thảo nội dung' })).toHaveCount(0)
        await settled(qa)
        await qa.shot('thanh-vien-chua-thay-du-thao')
        await qa.signIn('secretary')
        await qa.goto(`/mgmt/meetings/${m.id}?tab=minutes`)
        await qa.expect(qa.page.getByRole('heading', { level: 1, name: m.title })).toBeVisible({ timeout: 15_000 })
        const card = qa.page.getByRole('region', { name: 'Dự thảo nội dung' })
        await press(qa, card.getByRole('button', { name: 'Duyệt' }))
        await qa.expect(toast(qa, MSG.MSG391)).toBeVisible({ timeout: 15_000 })
        await qa.expect(card.getByText('Đã duyệt')).toBeVisible()
        c.fact('the chip names who approved and when', /duyệt lúc/.test(await card.locator('span[data-tooltip]').first().getAttribute('data-tooltip') ?? ''))
        await settled(qa)
        await qa.shot('thu-ky-da-duyet')
        await qa.signIn('member')
        await qa.goto(`/mgmt/meetings/${m.id}?tab=minutes`)
        await qa.expect(qa.page.getByRole('heading', { level: 1, name: m.title })).toBeVisible({ timeout: 15_000 })
        const read = qa.page.getByRole('region', { name: 'Dự thảo nội dung' })
        await qa.expect(read.getByText('Đã duyệt')).toBeVisible({ timeout: 15_000 })
        await qa.expect(read.getByText('I. Mục tiêu và phạm vi cuộc họp')).toBeVisible()
        for (const h of ['Mục tiêu và phạm vi', 'Nội dung thảo luận và đánh giá', 'Kết luận và phân công']) {
          await press(qa, read.getByRole('button', { name: new RegExp(esc(h)) }))
        }
        await qa.expect(read.locator('.minutes-outline-text').first()).toBeVisible()
        c.fact('no editor for the member', (await read.locator('textarea').count()) === 0)
        c.fact('no sources line for the member', (await read.getByText('Lấy từ bản ghi:').count()) === 0)
        c.fact('no re-ask button for the member', (await read.getByRole('button', { name: 'Tạo lại' }).count()) === 0)
        await settled(qa)
        await qa.shot('thanh-vien-doc-du-thao-da-duyet')
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
        c.done()
      },
    },
    {
      id: 'AC41',
      role: 'secretary',
      title: 'Sửa dự thảo đã duyệt phải duyệt lại: hộp "Lưu thay đổi?", chip về "Chưa duyệt", Thành viên mất thẻ (TRANS_FT39)',
      steps: [
        'Đảm bảo "[QA] FE-10 Họp Mật gỡ băng" có dự thảo đã duyệt (chưa thì duyệt qua API); Thư ký sửa mục III, bấm "Lưu dự thảo"',
        'Xác nhận hộp "Lưu thay đổi?"; Thành viên tải lại tab "Biên bản"',
      ],
      expected:
        'Hộp "Lưu thay đổi?" ghi "Dự thảo đã duyệt sẽ phải duyệt lại sau khi sửa.". "Lưu" → toast "Đã lưu dự thảo." (200 MSG388), chip về "Chưa duyệt", nút "Duyệt" hiện lại; Thành viên tải lại không còn thẻ "Dự thảo nội dung".',
      async run(qa) {
        const m = await meeting(qa, 'D')
        const sec = as(qa, 'secretary')
        const c = checker(qa)
        let state = (await sec.get(`/api/meetings/${m.id}/summary`)).body
        if (!state.summary || state.summary.status !== 'Ready') qa.inconclusive('Cuộc họp Mật chưa có dự thảo Ready để sửa.')
        if (!state.summary.reviewedAt) {
          c.fact('review via API first', (await sec.post(`/api/meetings/${m.id}/summary/review`, {})).status === 200)
        }
        await qa.goto(`/mgmt/meetings/${m.id}?tab=minutes`)
        await qa.expect(qa.page.getByRole('heading', { level: 1, name: m.title })).toBeVisible({ timeout: 15_000 })
        const card = qa.page.getByRole('region', { name: 'Dự thảo nội dung' })
        await qa.expect(card.getByText('Đã duyệt')).toBeVisible()
        await press(qa, card.getByRole('button', { name: /Kết luận và phân công/ }))
        const box = card.locator('textarea#outline-conclusions')
        const orig = await box.inputValue()
        await box.fill(`${orig} (QA sửa bản đã duyệt)`)
        const bar = qa.page.getByRole('region', { name: 'Thay đổi dự thảo' })
        await press(qa, bar.getByRole('button', { name: 'Lưu dự thảo' }))
        const dialog = qa.page.getByRole('dialog', { name: 'Lưu thay đổi?' })
        await qa.expect(dialog.getByText('Dự thảo đã duyệt sẽ phải duyệt lại sau khi sửa.')).toBeVisible()
        await settled(qa)
        await qa.shot('xac-nhan-luu-ban-da-duyet')
        await press(qa, dialog.getByRole('button', { name: 'Lưu' }))
        await qa.expect(toast(qa, MSG.MSG388)).toBeVisible({ timeout: 15_000 })
        await qa.expect(card.getByText('Chưa duyệt')).toBeVisible()
        await qa.expect(card.getByRole('button', { name: 'Duyệt' })).toBeVisible()
        c.fact('the review is taken back', ((await sec.get(`/api/meetings/${m.id}/summary`)).body.summary.reviewedAt) == null)
        await qa.signIn('member')
        await qa.goto(`/mgmt/meetings/${m.id}?tab=minutes`)
        await qa.expect(qa.page.getByRole('heading', { level: 1, name: m.title })).toBeVisible({ timeout: 15_000 })
        await qa.expect(qa.page.getByRole('region', { name: 'Dự thảo nội dung' })).toHaveCount(0)
        await settled(qa)
        await qa.shot('thanh-vien-mat-the-sau-sua')
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
        c.done()
      },
    },
    {
      id: 'AC42',
      role: 'secretary',
      title: 'Thư ký duyệt dự thảo: đang sửa thì "Duyệt" khoá, xong bấm duyệt toast MSG391, Thành viên đọc được (TRANS_FT41)',
      steps: [
        'Đảm bảo "[QA] FE-10 Họp Mật gỡ băng" có dự thảo Ready chưa duyệt; Thư ký sửa một mục (chưa lưu), rê vào "Duyệt"',
        'Hoàn tác, bấm "Duyệt"; Thành viên tải lại đọc thẻ đã duyệt',
      ],
      expected:
        'Đang sửa: "Duyệt" khoá với tooltip "Lưu thay đổi trước khi duyệt". Hoàn tác xong bấm "Duyệt" → toast "Đã duyệt dự thảo." (200 MSG391), chip "Đã duyệt", nút "Duyệt" mất. Thành viên tải lại thấy thẻ dự thảo đã duyệt.',
      async run(qa) {
        const m = await meeting(qa, 'D')
        const sec = as(qa, 'secretary')
        const c = checker(qa)
        let state = (await sec.get(`/api/meetings/${m.id}/summary`)).body
        if (!state.summary || state.summary.status !== 'Ready') qa.inconclusive('Cuộc họp Mật chưa có dự thảo Ready để duyệt.')
        if (state.summary.reviewedAt) {
          c.fact('re-ask to get an unreviewed draft', (await sec.post(`/api/meetings/${m.id}/summary`, {})).status === 202)
          const until = Date.now() + 900_000
          for (;;) {
            state = (await sec.get(`/api/meetings/${m.id}/summary`)).body
            if (state.summary?.status !== 'Queued' && state.summary?.status !== 'Running') break
            if (Date.now() > until) qa.inconclusive('AI chưa viết xong sau 15 phút.')
            await new Promise((r) => setTimeout(r, 10000))
          }
        }
        c.fact('the draft is Ready and unreviewed', state.summary?.status === 'Ready' && !state.summary?.reviewedAt)
        await qa.goto(`/mgmt/meetings/${m.id}?tab=minutes`)
        await qa.expect(qa.page.getByRole('heading', { level: 1, name: m.title })).toBeVisible({ timeout: 15_000 })
        const card = qa.page.getByRole('region', { name: 'Dự thảo nội dung' })
        await press(qa, card.getByRole('button', { name: /Mục tiêu và phạm vi/ }))
        const box = card.locator('textarea#outline-objectives')
        await box.fill(`${await box.inputValue()} (nháp)`)
        const review = card.getByRole('button', { name: 'Duyệt' })
        c.fact('review is blocked while dirty', (await review.getAttribute('aria-disabled')) === 'true' || (await review.isDisabled()))
        await qa.expect(card.getByText('Chưa duyệt')).toBeVisible()
        const bar = qa.page.getByRole('region', { name: 'Thay đổi dự thảo' })
        await press(qa, bar.getByRole('button', { name: 'Hoàn tác' }))
        await press(qa, qa.page.getByRole('dialog', { name: 'Bỏ các thay đổi?' }).getByRole('button', { name: 'Bỏ thay đổi' }))
        await qa.expect(bar.getByText('Có thay đổi chưa lưu')).toHaveCount(0)
        await press(qa, card.getByRole('button', { name: 'Duyệt' }))
        await qa.expect(toast(qa, MSG.MSG391)).toBeVisible({ timeout: 15_000 })
        await qa.expect(card.getByText('Đã duyệt')).toBeVisible()
        c.fact('the review button is gone', (await card.getByRole('button', { name: 'Duyệt' }).count()) === 0)
        await qa.signIn('member')
        await qa.goto(`/mgmt/meetings/${m.id}?tab=minutes`)
        await qa.expect(qa.page.getByRole('heading', { level: 1, name: m.title })).toBeVisible({ timeout: 15_000 })
        await qa.expect(qa.page.getByRole('region', { name: 'Dự thảo nội dung' }).getByText('Đã duyệt')).toBeVisible({ timeout: 15_000 })
        await settled(qa)
        await qa.shot('thanh-vien-thay-du-thao-sau-duyet')
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
        c.done()
      },
    },
    {
      id: 'AC43',
      role: 'secretary',
      title: 'Thư ký "Tạo lại": hộp xác nhận thay bản hiện tại, bản cũ đọc được khi "AI đang viết", xong chip "Chưa duyệt" (TRANS_FT32)',
      steps: [
        'Thư ký mở tab "Biên bản" của "[QA] FE-10 Họp trực tiếp gỡ băng" (dự thảo Ready), bấm "Tạo lại"',
        'Xác nhận hộp "Tạo lại dự thảo?"; chờ AI viết xong, đọc bản mới',
      ],
      expected:
        'Hộp "Tạo lại dự thảo?" ghi "AI viết lại từ đầu. Khi xong, dự thảo mới thay dự thảo hiện tại, kể cả phần đã sửa và đã duyệt.". "Tạo lại" xếp hàng (toast "Đã gửi yêu cầu. Dự thảo sẽ tự hiện khi AI viết xong.", 202 MSG384); bản cũ vẫn đọc được khi chip "AI đang viết" (AI local viết xong trong vài giây nên có thể chỉ thấy thoáng qua); xong bản mới thay bản cũ, chip "Chưa duyệt".',
      async run(qa) {
        const m = await meeting(qa, 'A')
        const sec = as(qa, 'secretary')
        const c = checker(qa)
        const old = (await sec.get(`/api/meetings/${m.id}/summary`)).body.summary
        c.fact('a draft is there to replace', old?.status === 'Ready' && !!old?.draft, old?.status)
        await qa.goto(`/mgmt/meetings/${m.id}?tab=minutes`)
        await qa.expect(qa.page.getByRole('heading', { level: 1, name: m.title })).toBeVisible({ timeout: 15_000 })
        const card = qa.page.getByRole('region', { name: 'Dự thảo nội dung' })
        await press(qa, card.getByRole('button', { name: 'Tạo lại' }))
        const dialog = qa.page.getByRole('dialog', { name: 'Tạo lại dự thảo?' })
        await qa.expect(dialog.getByText('AI viết lại từ đầu. Khi xong, dự thảo mới thay dự thảo hiện tại, kể cả phần đã sửa và đã duyệt.')).toBeVisible()
        await settled(qa)
        await qa.shot('xac-nhan-tao-lai-du-thao')
        await press(qa, dialog.getByRole('button', { name: 'Tạo lại' }))
        await qa.expect(toast(qa, MSG.MSG384)).toBeVisible({ timeout: 15_000 })
        const running = card.getByText('AI đang viết')
        if ((await running.count()) > 0) {
          c.fact('the old draft stays readable while rewriting', (await card.getByText('I. Mục tiêu và phạm vi cuộc họp').count()) > 0)
          await qa.shot('ban-cu-khi-dang-viet-lai')
        } else qa.note('AI local viết xong trong vài giây: không bắt được trạng thái "AI đang viết", kiểm bản mới thay bản cũ.')
        const until = Date.now() + 900_000
        let state = old
        for (;;) {
          state = (await sec.get(`/api/meetings/${m.id}/summary`)).body.summary
          if (state?.status !== 'Queued' && state?.status !== 'Running') break
          if (Date.now() > until) qa.inconclusive('AI chưa viết xong sau 15 phút.')
          await new Promise((r) => setTimeout(r, 10000))
        }
        c.fact('the new draft is Ready and unreviewed', state?.status === 'Ready' && !state?.reviewedAt, state?.status)
        c.fact('the new draft replaced the old one', state?.generatedAt && old?.generatedAt && state.generatedAt !== old.generatedAt)
        await qa.goto(`/mgmt/meetings/${m.id}?tab=minutes`)
        await qa.expect(qa.page.getByRole('heading', { level: 1, name: m.title })).toBeVisible({ timeout: 15_000 })
        const fresh = qa.page.getByRole('region', { name: 'Dự thảo nội dung' })
        await qa.expect(fresh.getByText('Nội dung do AI tạo — cần rà soát')).toBeVisible({ timeout: 60_000 })
        await qa.expect(fresh.getByText('Chưa duyệt')).toBeVisible()
        await settled(qa)
        await qa.shot('du-thao-moi-sau-tao-lai')
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
        c.done()
      },
    },
    {
      id: 'AC44',
      role: 'secretary',
      title: 'Máy AI tắt giữa chừng: dự thảo lỗi chip "Lỗi", giữ bản trước, "Tạo lại" vẫn bấm được (TRANS_FT33)',
      steps: [
        'Thư ký mở tab "Biên bản" của "[QA] FE-10 Họp trực tiếp gỡ băng" (dự thảo Ready), bấm "Tạo lại", xác nhận',
        'Tắt máy AI local (QA lead) ngay khi đã xếp hàng; chờ worker báo lỗi; khởi động lại máy AI và xin lại bản mới',
      ],
      expected:
        'Xác nhận xong toast MSG384, chip "Đang chờ"/"AI đang viết". Máy tắt → chip "Lỗi", dòng đỏ nêu lý do máy đưa (hoặc "AI không viết được dự thảo."), kèm "Đang giữ bản trước." vì bản cũ còn, nút "Tạo lại" bấm được. Máy mở lại + "Tạo lại" → bản Ready mới.',
      async run(qa) {
        const m = await meeting(qa, 'A')
        const sec = as(qa, 'secretary')
        const c = checker(qa)
        let state = (await sec.get(`/api/meetings/${m.id}/summary`)).body
        if (!state.summary || state.summary.status !== 'Ready') qa.inconclusive('Cuộc họp trực tiếp chưa có dự thảo Ready để làm lỗi.')
        await qa.goto(`/mgmt/meetings/${m.id}?tab=minutes`)
        await qa.expect(qa.page.getByRole('heading', { level: 1, name: m.title })).toBeVisible({ timeout: 15_000 })
        const card = qa.page.getByRole('region', { name: 'Dự thảo nội dung' })
        await press(qa, card.getByRole('button', { name: 'Tạo lại' }))
        await press(qa, qa.page.getByRole('dialog', { name: 'Tạo lại dự thảo?' }).getByRole('button', { name: 'Tạo lại' }))
        await qa.expect(toast(qa, MSG.MSG384)).toBeVisible({ timeout: 15_000 })
        stopOllama()
        try {
          const until = Date.now() + 300_000
          for (;;) {
            state = (await sec.get(`/api/meetings/${m.id}/summary`)).body
            if (state.summary?.status !== 'Queued' && state.summary?.status !== 'Running') break
            if (Date.now() > until) qa.inconclusive('Dự thảo không lỗi sau 5 phút (worker chưa gặp máy tắt?).')
            await new Promise((r) => setTimeout(r, 5000))
          }
          c.fact('the draft failed while the AI was down', state.summary?.status === 'Failed', state.summary?.status)
          await qa.goto(`/mgmt/meetings/${m.id}?tab=minutes`)
          await qa.expect(qa.page.getByRole('heading', { level: 1, name: m.title })).toBeVisible({ timeout: 15_000 })
          const failed = qa.page.getByRole('region', { name: 'Dự thảo nội dung' })
          await qa.expect(failed.getByText('Lỗi')).toBeVisible({ timeout: 30_000 })
          const alert = failed.locator('.minutes-failed')
          await qa.expect(alert).toBeVisible()
          c.fact('a reason is shown', /AI không viết được dự thảo|Không kết nối|chưa được cấu hình|dự phòng/.test(await alert.innerText()))
          await qa.expect(alert.getByText(/Đang giữ bản trước/)).toBeVisible()
          await qa.expect(failed.getByRole('button', { name: 'Tạo lại' })).toBeEnabled()
          await settled(qa)
          await qa.shot('du-thao-loi-giu-ban-truoc')
          await noRawCodes(qa)
          c.done()
        } finally {
          await startOllama(qa)
          const req = await sec.post(`/api/meetings/${m.id}/summary`, {})
          if (req.status === 202) {
            const until = Date.now() + 900_000
            for (;;) {
              const s = (await sec.get(`/api/meetings/${m.id}/summary`)).body.summary
              if (s?.status !== 'Queued' && s?.status !== 'Running') break
              if (Date.now() > until) qa.inconclusive('AI chưa viết lại xong sau 15 phút.')
              await new Promise((r) => setTimeout(r, 10000))
            }
          }
        }
      },
    },
    {
      id: 'AC45',
      role: 'secretary',
      title: 'Nút xin dự thảo khoá đúng lý do: "Máy AI chưa sẵn sàng" khi máy chưa cấu hình, "Lưu hoặc hoàn tác…" khi đang sửa (TRANS_FT34)',
      steps: [
        'Thư ký mở tab "Biên bản" của "[QA] FE-10 Họp trực tiếp gỡ băng", sửa một mục (chưa lưu), rê vào "Tạo lại" rồi hoàn tác',
        'QA lead bỏ cấu hình máy AI local + khởi động lại backend; Thư ký mở "[QA] FE-10 Họp Mật gỡ băng", rê vào "Tạo lại", gọi thẳng POST (503 MSG394); trả cấu hình + khởi động lại backend',
      ],
      expected:
        'Đang sửa: "Tạo lại" aria-disabled với tooltip "Lưu hoặc hoàn tác thay đổi trước". Máy chưa cấu hình: họp Mật (luôn viết local) hiện tooltip "Máy AI chưa sẵn sàng", nút khoá, POST thẳng 503 MSG394 "Máy AI chưa sẵn sàng. Liên hệ quản trị hệ thống.". Trả cấu hình xong aiReady lại true.',
      async run(qa) {
        const m = await meeting(qa, 'A')
        const d = await meeting(qa, 'D')
        const sec = as(qa, 'secretary')
        const c = checker(qa)
        const st = (await sec.get(`/api/meetings/${m.id}/summary`)).body
        c.fact('the Secretary manages and the AI is ready', st.canManage === true && st.aiReady === true)
        await qa.goto(`/mgmt/meetings/${m.id}?tab=minutes`)
        await qa.expect(qa.page.getByRole('heading', { level: 1, name: m.title })).toBeVisible({ timeout: 15_000 })
        const card = qa.page.getByRole('region', { name: 'Dự thảo nội dung' })
        await press(qa, card.getByRole('button', { name: /Mục tiêu và phạm vi/ }))
        const box = card.locator('textarea#outline-objectives')
        await box.fill(`${await box.inputValue()} (nháp khoá nút)`)
        const ask = card.getByRole('button', { name: 'Tạo lại' })
        c.fact('the ask button is blocked while dirty', (await ask.getAttribute('aria-disabled')) === 'true')
        c.fact('the dirty reason', (await ask.getAttribute('data-tooltip')) === 'Lưu hoặc hoàn tác thay đổi trước')
        await pressBlocked(qa, ask)
        await qa.expect(qa.page.getByText('Lưu hoặc hoàn tác thay đổi trước').first()).toBeVisible({ timeout: 10_000 })
        await settled(qa)
        await qa.shot('nut-khoa-khi-dang-sua')
        const bar = qa.page.getByRole('region', { name: 'Thay đổi dự thảo' })
        await press(qa, bar.getByRole('button', { name: 'Hoàn tác' }))
        await press(qa, qa.page.getByRole('dialog', { name: 'Bỏ các thay đổi?' }).getByRole('button', { name: 'Bỏ thay đổi' }))
        await qa.expect(bar.getByText('Có thay đổi chưa lưu')).toHaveCount(0)
        // Half (a): unconfigure the local machine. IOptions is a snapshot, so the backend restarts twice.
        const settingsPath = join(ROOT, 'backend', 'TrustCabinet.Web', 'appsettings.Development.json')
        const original = readFileSync(settingsPath, 'utf8')
        if (!original.includes('"Model": "qwen2.5:7b-instruct"')) qa.inconclusive('appsettings.Development.json không còn cấu hình AI mẫu để thử.')
        try {
          writeFileSync(settingsPath, original.replace('"Model": "qwen2.5:7b-instruct"', '"Model": ""'))
          await restartBackend(qa)
          const dark = (await sec.get(`/api/meetings/${d.id}/summary`)).body
          c.fact('the Secret meeting reads as not ready', dark.aiReady === false, String(dark.aiReady))
          await qa.goto(`/mgmt/meetings/${d.id}?tab=minutes`)
          await qa.expect(qa.page.getByRole('heading', { level: 1, name: d.title })).toBeVisible({ timeout: 15_000 })
          const secret = qa.page.getByRole('region', { name: 'Dự thảo nội dung' })
          const askSecret = secret.getByRole('button', { name: 'Tạo lại' })
          c.fact('the ask button is blocked when the AI is down', (await askSecret.getAttribute('aria-disabled')) === 'true')
          c.fact('the not-ready reason', (await askSecret.getAttribute('data-tooltip')) === 'Máy AI chưa sẵn sàng')
          await pressBlocked(qa, askSecret)
          await qa.expect(qa.page.getByText('Máy AI chưa sẵn sàng').first()).toBeVisible({ timeout: 10_000 })
          await settled(qa)
          await qa.shot('nut-khoa-khi-may-chua-san-sang')
        await qa.shot('nut-khoa-khi-may-chua-san-sang')
          c.check('direct ask answers 503 MSG394', await sec.post(`/api/meetings/${d.id}/summary`, {}), 503, 'MSG394')
          await noRawCodes(qa)
          c.done()
        } finally {
          writeFileSync(settingsPath, original)
          await restartBackend(qa)
          const back = (await sec.get(`/api/meetings/${m.id}/summary`)).body
          qa.expect(back.aiReady, 'the AI is ready again after the config is restored').toBe(true)
        }
      },
    },
    {
      id: 'AC46',
      role: 'admin',
      title: 'Người dùng bật Voice ID: đánh dấu đồng ý, toast MSG305, thẻ "Giọng mẫu — Đang bật", 0/5, hai tab, tải lại vẫn bật (TRANS_FT43)',
      steps: [
        'Rút đồng ý của qa-fe10-a@ (API); đăng nhập qa-fe10-a@, mở "Giọng nói của tôi"',
        'Đánh dấu ô đồng ý, bấm "Đồng ý và bật"; tải lại trang',
      ],
      expected:
        'Toast "Đã bật nhận diện giọng nói cho tài khoản của bạn." (200 MSG305); trang hiện "Giọng mẫu" với chip "Đang bật", "từ <date>" (tooltip "Đồng ý lúc <time>"), "0/5" và nhắc "Chưa có giọng mẫu nào — hãy ghi ít nhất 2 mẫu để hệ thống nhận ra bạn."; hai tab "Tự thu" và "Thư ký thu hộ" hiện số lượng; tải lại vẫn bật.',
      async run(qa) {
        const u = await users(qa)
        const c = checker(qa)
        await withdrawn(qa, 'a')
        await qa.signIn(u.a.email)
        await openMyVoice(qa)
        const enable = qa.page.getByRole('button', { name: 'Đồng ý và bật' })
        const card = qa.page.getByRole('region', { name: 'Bật nhận diện giọng nói' })
        await qa.expect(card).toBeVisible()
        await press(qa, card.getByRole('checkbox'))
        await qa.expect(enable).toBeEnabled()
        await press(qa, enable)
        await qa.expect(toast(qa, MSG.MSG305)).toBeVisible({ timeout: 15_000 })
        const page2 = qa.page.getByRole('region', { name: 'Giọng mẫu' })
        await qa.expect(page2.getByText('Đang bật')).toBeVisible()
        await qa.expect(page2.getByText(/từ /)).toBeVisible()
        c.fact('the consent time tooltip', /Đồng ý lúc/.test(await page2.locator('[data-tooltip]').first().getAttribute('data-tooltip') ?? ''))
        await qa.expect(page2.getByText('0/5')).toBeVisible()
        await qa.expect(page2.getByText(/Chưa có giọng mẫu nào/)).toBeVisible()
        await qa.expect(qa.page.getByRole('tab', { name: /Tự thu/ })).toBeVisible()
        await qa.expect(qa.page.getByRole('tab', { name: /Người khác thu hộ/ })).toBeVisible()
        await settled(qa)
        await qa.shot('da-bat-voice-id')
        await qa.page.reload()
        await openMyVoice(qa)
        await qa.expect(qa.page.getByRole('region', { name: 'Giọng mẫu' }).getByText('Đang bật')).toBeVisible()
        c.fact('consent on the current notice', ((await voice(qa, 'a')).consent != null))
        c.fact('no samples yet', (await voice(qa, 'a')).samples.length === 0)
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
        c.done()
      },
    },
    {
      id: 'AC47',
      role: 'admin',
      title: 'Người dùng ghi giọng mẫu: hộp thoại đọc đoạn văn, chờ đủ 8 giây, dừng, nghe lại rồi mới giữ (TRANS_FT44)',
      steps: [
        'qa-fe10-a@ đã đồng ý, chưa mẫu: mở "Ghi giọng mẫu", "Bắt đầu ghi" (micro giả phát tiếng bíp), chờ nút "Dừng" mở, "Dừng", nghe thử, đóng mà không lưu',
        'Giữ một mẫu đọc thật qua API (201 MSG307); tải lại, đọc "Giọng mẫu 1" kèm độ dài, ngày, nút nghe/xoá',
      ],
      expected:
        'Hộp "Ghi giọng mẫu" nhắc ngồi yên tĩnh, đọc đoạn văn; nút "Đọc tiếp… (<n> giây)" khoá tới đủ 8 giây, tự dừng ở 30 giây. Sau dừng có trình phát "Nghe lại giọng mẫu vừa ghi" với "Ghi lại" và "Lưu giọng mẫu" ("Đang kiểm tra…" khi gửi); chưa bấm Lưu thì không mẫu nào được gửi. Giữ mẫu đọc thật: 201 MSG307; danh sách hiện "Giọng mẫu 1" kèm độ dài, ngày, nút "Nghe lại" và "Xoá giọng mẫu". Ghi nhận: micro giả của trình duyệt phát tiếng bíp, không phải tiếng người, nên mẫu giữ lại lấy từ đoạn đọc QA qua API (cùng pipeline kiểm tra của worker).',
      async run(qa) {
        const u = await users(qa)
        const c = checker(qa)
        await enrolled(qa, 'a', 0)
        try {
          await qa.signIn(u.a.email)
          await openMyVoice(qa)
          await qa.page.getByRole('button', { name: 'Ghi giọng mẫu' }).click()
          const dialog = qa.page.getByRole('dialog', { name: 'Ghi giọng mẫu' })
          await qa.expect(dialog).toBeVisible()
          await qa.expect(dialog.getByText(/Ngồi ở nơi yên tĩnh/)).toBeVisible()
          await qa.expect(dialog.locator('blockquote.voice-passage')).toBeVisible()
          await dialog.getByRole('button', { name: 'Bắt đầu ghi' }).click()
          await qa.expect(dialog.getByRole('status').filter({ hasText: 'Đang ghi' })).toBeVisible({ timeout: 15_000 })
          const stop = dialog.getByRole('button', { name: 'Dừng' })
          await qa.expect(stop).toBeEnabled({ timeout: 20_000 })
          await settled(qa)
          await qa.shot('dang-ghi-giong-mau')
          await stop.click()
          await qa.expect(dialog.getByLabel('Nghe lại giọng mẫu vừa ghi')).toBeVisible({ timeout: 15_000 })
          await qa.expect(dialog.getByRole('button', { name: 'Ghi lại' })).toBeVisible()
          await qa.expect(dialog.getByRole('button', { name: 'Lưu giọng mẫu' })).toBeVisible()
          c.fact('nothing sent before saving', (await voice(qa, 'a')).samples.length === 0)
          await settled(qa)
          await qa.shot('nghe-lai-truoc-khi-luu')
          await dialog.getByRole('button', { name: /Đóng/ }).first().click().catch(() => qa.page.keyboard.press('Escape'))
          const saved = await as(qa, 'a').form('/api/me/voice/samples', sampleForm(qa, 'enrol', 'giong-mau.wav'))
          c.check('a real reading is kept', saved, 201, 'MSG307')
          const sample = saved.body?.data
          await openMyVoice(qa)
          const item = qa.page.locator('li.voice-sample').filter({ hasText: 'Giọng mẫu 1' })
          await qa.expect(item).toBeVisible()
          c.fact('length and date shown', /·/.test(await item.locator('.field-hint').innerText()))
          await qa.expect(item.getByRole('button', { name: 'Nghe lại giọng mẫu 1' })).toBeVisible()
          await qa.expect(item.getByRole('button', { name: 'Xoá giọng mẫu 1' })).toBeVisible()
          c.fact('the kept sample plays', sample?.playable === true)
          await settled(qa)
          await qa.shot('giong-mau-1')
          await noSidewaysScroll(qa)
          await noRawCodes(qa)
          c.done()
        } finally {
          await withdrawn(qa, 'a')
        }
      },
    },
    {
      id: 'AC48',
      role: 'admin',
      title: 'Mẫu không đạt bị từ chối: im lặng 9 giây 422 MSG309, micro giả cũng vậy, máy nhận dạng tắt 503 MSG312 (TRANS_FT45)',
      steps: [
        'qa-fe10-a@ đã đồng ý: tải 9 giây im lặng qua API → 422; ghi bằng micro giả trong hộp thoại rồi "Lưu giọng mẫu" → hộp báo đỏ',
        'Dừng container speech, tải mẫu đọc → 503 MSG312; mở lại container, chờ khoẻ, tải lại → 201',
      ],
      expected:
        'Im lặng: cảnh báo đỏ nêu lý do của máy (ví dụ "Tiếng quá nhỏ — hãy nói to hơn hoặc ngồi gần micro hơn.", 422 MSG309); không mẫu nào thêm, "Ghi lại" cho thử lại. Máy tắt: "Máy chủ nhận dạng giọng nói chưa sẵn sàng. Vui lòng thử lại sau." (503 MSG312). Máy mở lại: tải mẫu đọc 201, worker khoẻ.',
      async run(qa) {
        const u = await users(qa)
        const c = checker(qa)
        await enrolled(qa, 'a', 0)
        try {
          try {
            execFileSync('ffmpeg', ['-y', '-loglevel', 'error', '-f', 'lavfi', '-i', 'anullsrc=r=16000:cl=mono', '-t', '9', '-c:a', 'pcm_s16le', join(AUDIO_DIR, 'sil9.wav')])
          } catch (error) {
            qa.inconclusive(`Không tạo được âm im lặng (ffmpeg): ${error.message}`)
          }
          const sil = readFileSync(join(AUDIO_DIR, 'sil9.wav'))
          const silForm = new FormData()
          silForm.append('file', new Blob([sil], { type: 'audio/wav' }), 'im-lang-9s.wav')
          const refused = await as(qa, 'a').form('/api/me/voice/samples', silForm)
          c.check('9 seconds of silence', refused, 422, 'MSG309')
          c.fact('the reason reads in Vietnamese', /tiếng|nhỏ|im lặng|giọng|mẫu/i.test(refused.body?.message ?? ''))
          c.fact('nothing kept', (await voice(qa, 'a')).samples.length === 0)
          await qa.signIn(u.a.email)
          await openMyVoice(qa)
          await qa.page.getByRole('button', { name: 'Ghi giọng mẫu' }).click()
          const dialog = qa.page.getByRole('dialog', { name: 'Ghi giọng mẫu' })
          await dialog.getByRole('button', { name: 'Bắt đầu ghi' }).click()
          await qa.expect(dialog.getByRole('button', { name: 'Dừng' })).toBeEnabled({ timeout: 20_000 })
          await dialog.getByRole('button', { name: 'Dừng' }).click()
          await qa.expect(dialog.getByLabel('Nghe lại giọng mẫu vừa ghi')).toBeVisible({ timeout: 15_000 })
          await dialog.getByRole('button', { name: 'Lưu giọng mẫu' }).click()
          const alert = dialog.getByRole('alert')
          await qa.expect(alert).toBeVisible({ timeout: 60_000 })
          c.fact('the beep is refused in Vietnamese', /giọng|mẫu|tiếng|im lặng/i.test(await alert.innerText()))
          await qa.expect(dialog.getByRole('button', { name: 'Ghi lại' })).toBeVisible()
          await settled(qa)
          await qa.shot('mau-bi-tu-choi')
          await dialog.getByRole('button', { name: /Đóng/ }).first().click().catch(() => qa.page.keyboard.press('Escape'))
          c.fact('still nothing kept', (await voice(qa, 'a')).samples.length === 0)
          execFileSync('docker', ['stop', 'trustcabinet-speech'], { stdio: 'ignore' })
          try {
            const down = await as(qa, 'a').form('/api/me/voice/samples', sampleForm(qa, 'enrol', 'giong-mau.wav'))
            c.check('the worker down', down, 503, 'MSG312')
          } finally {
            execFileSync('docker', ['start', 'trustcabinet-speech'], { stdio: 'ignore' })
          }
          const until = Date.now() + 120_000
          for (;;) {
            try {
              const res = await fetch('http://localhost:8090/health')
              if (res.ok) break
            } catch { /* not up yet */ }
            if (Date.now() > until) qa.inconclusive('Máy nhận dạng giọng nói không mở lại sau 120 giây.')
            await new Promise((r) => setTimeout(r, 3000))
          }
          c.check('recovered', await as(qa, 'a').form('/api/me/voice/samples', sampleForm(qa, 'enrol', 'giong-mau.wav')), 201, 'MSG307')
          await noRawCodes(qa)
          c.done()
        } finally {
          try {
            execFileSync('docker', ['start', 'trustcabinet-speech'], { stdio: 'ignore' })
          } catch { /* already running */ }
          await withdrawn(qa, 'a')
        }
      },
    },
    {
      id: 'AC49',
      role: 'admin',
      title: 'Người dùng nghe lại giọng mẫu đã giữ: phát thì thành "Dừng", bấm nữa thì thôi (TRANS_FT47)',
      steps: [
        'Giữ 1 mẫu đọc thật cho qa-fe10-a@ (API); đăng nhập qa-fe10-a@, mở "Giọng nói của tôi"',
        'Bấm "Nghe lại giọng mẫu 1" rồi bấm lại ("Dừng giọng mẫu 1")',
      ],
      expected:
        'Bấm phát thì bản ghi phát, nút thành "Dừng giọng mẫu 1" (aria-pressed); bấm nữa thì dừng, nút về "Nghe lại giọng mẫu 1". Không có mẫu thời thông báo cũ trong bản dựng này: đồng ý bản 1.0 đã bị từ chối (400 MSG313) nên mọi mẫu còn lại đều nghe được; xem Deviation.',
      async run(qa) {
        const u = await users(qa)
        const c = checker(qa)
        const kept = await enrolled(qa, 'a', 1)
        try {
          await qa.signIn(u.a.email)
          await openMyVoice(qa)
          const item = qa.page.locator('li.voice-sample').filter({ hasText: 'Giọng mẫu 1' })
          await qa.expect(item).toBeVisible()
          const play = item.getByRole('button', { name: 'Nghe lại giọng mẫu 1' })
          const heard = qa.page.waitForResponse((r) => r.url().includes(`/api/me/voice/samples/${kept.samples[0].id}/audio`), { timeout: 30_000 })
          await press(qa, play)
          c.fact('the owner hears it back', (await heard).status() === 200)
          await qa.expect(item.getByRole('button', { name: 'Dừng giọng mẫu 1' })).toBeVisible()
          await settled(qa)
          await qa.shot('dang-nghe-giong-mau-1')
          await press(qa, item.getByRole('button', { name: 'Dừng giọng mẫu 1' }))
          await qa.expect(item.getByRole('button', { name: 'Nghe lại giọng mẫu 1' })).toBeVisible()
          c.fact('every kept sample is playable', kept.samples.every((s) => s.playable === true))
          await noRawCodes(qa)
          c.done()
        } finally {
          await withdrawn(qa, 'a')
        }
      },
    },
    {
      id: 'AC50',
      role: 'admin',
      devices: ['desktop', 'phone'],
      title: 'Người dùng tắt Voice ID: giữ lại thì không gì đổi, tắt thì xoá hết mẫu hai bộ, bật lại trắng (TRANS_FT49)',
      steps: [
        'qa-fe10-a@ có 1 mẫu tự thu và 1 mẫu thu hộ; mở "Giọng nói của tôi", bấm "Tắt và xoá giọng mẫu" rồi "Giữ lại"',
        'Bấm lại rồi "Tắt và xoá"; bật lại Voice ID, đọc số mẫu',
      ],
      expected:
        'Hộp "Tắt nhận diện giọng nói?" nêu "Toàn bộ <n> giọng mẫu tự thu và <m> mẫu thu hộ, mọi gợi ý và tên máy đã điền từ giọng của bạn bị xoá ngay. Tên Thư ký đã gán giữ nguyên. Bạn có thể bật lại sau.". "Giữ lại" không đổi gì. "Tắt và xoá" → toast "Đã tắt nhận diện giọng nói và xoá toàn bộ giọng mẫu." (200 MSG306), trang về thẻ đồng ý, không còn mẫu; bật lại vẫn trắng.',
      async run(qa) {
        const u = await users(qa)
        const c = checker(qa)
        await enrolled(qa, 'a', 1)
        await clearPersonSamples(qa, u.a.id)
        c.check('a sample collected for A', await collectFor(qa, u.a.id), 201, 'MSG473')
        try {
          await qa.signIn(u.a.email)
          await openMyVoice(qa)
          await qa.page.getByRole('button', { name: 'Tắt và xoá giọng mẫu' }).click()
          const dialog = qa.page.getByRole('dialog', { name: 'Tắt nhận diện giọng nói?' })
          await qa.expect(dialog.getByText(/bị xoá ngay/)).toBeVisible()
          await qa.expect(dialog.getByText(/Tên Thư ký đã gán giữ nguyên/)).toBeVisible()
          await settled(qa)
          await qa.shot('xac-nhan-tat-voice-id')
          await press(qa, dialog.getByRole('button', { name: 'Giữ lại' }))
          await qa.expect(dialog).toHaveCount(0)
          c.fact('keeping changes nothing', (await voice(qa, 'a')).samples.length === 1)
          await qa.page.getByRole('button', { name: 'Tắt và xoá giọng mẫu' }).click()
          const again = qa.page.getByRole('dialog', { name: 'Tắt nhận diện giọng nói?' })
          await press(qa, again.getByRole('button', { name: 'Tắt và xoá' }))
          await qa.expect(toast(qa, MSG.MSG306)).toBeVisible({ timeout: 15_000 })
          await qa.expect(qa.page.getByRole('region', { name: 'Bật nhận diện giọng nói' })).toBeVisible()
          const gone = await voice(qa, 'a')
          c.fact('consent withdrawn', gone.consent == null)
          c.fact('every sample erased', gone.samples.length === 0 && gone.proxy.samples.length === 0)
          await openMyVoice(qa)
          await qa.page.getByRole('checkbox').check()
          await qa.page.getByRole('button', { name: 'Đồng ý và bật' }).click()
          await qa.expect(toast(qa, MSG.MSG305)).toBeVisible({ timeout: 15_000 })
          const fresh = await voice(qa, 'a')
          c.fact('turning it on again restores nothing', fresh.samples.length === 0 && fresh.proxy.samples.length === 0)
          await settled(qa)
          await qa.shot('bat-lai-trang')
          await noSidewaysScroll(qa)
          await noRawCodes(qa)
          c.done()
        } finally {
          await withdrawn(qa, 'a')
          await clearPersonSamples(qa, u.a.id)
        }
      },
    },
    {
      id: 'AC51',
      role: 'admin',
      title: 'Người giữ quyền mở sổ thu hộ của một người: đếm "<n>/5", nút "Thu" khi đã đồng ý, vắng khi chưa (TRANS_FT52)',
      steps: [
        'qa-fe10-a@ đã đồng ý (1.5), qa-fe10-b@ chưa; admin mở "/mgmt/admin/users", bấm "Quản lý giọng nói thu hộ" ở dòng từng người',
        'Đối chiếu API GET /api/users/{id}/voice-collection',
      ],
      expected:
        'Hộp "Giọng nói thu hộ: <name>" hiện "<n>/5 mẫu thu hộ" (tooltip "Mọi mẫu thu hộ của người này"), nút "Thu" khi người đó đã đồng ý bản hiện hành; mỗi mẫu ghi "Ngoài cuộc họp" hoặc cuộc họp với "<người thu> · <ngày>". Người chưa đồng ý: "Người này chưa đồng ý điều khoản Voice ID mới. Họ bật ở trang Giọng nói của tôi." và không nút "Thu". Ghi nhận: giới hạn thu hộ hiện là 5 mẫu/người, case ghi 50 đã cũ (thay đổi 23/11, Lộc đồng ý).',
      async run(qa) {
        const u = await users(qa)
        const c = checker(qa)
        await enrolled(qa, 'a', 0)
        await withdrawn(qa, 'b')
        await clearPersonSamples(qa, u.a.id)
        const col = await personCollection(qa, u.a.id)
        c.fact('consented and collectible', col.consented === true && col.canCollect === true)
        c.fact('the proxy limit is 5', col.maxSamples === 5, String(col.maxSamples))
        await qa.signIn('admin')
        const dialog = await openCollection(qa, u.a.name)
        await qa.expect(dialog.getByText('0/5 mẫu thu hộ')).toBeVisible()
        c.fact('the count tooltip', (await dialog.locator('.voice-person-count').getAttribute('data-tooltip')) === 'Mọi mẫu thu hộ của người này')
        await qa.expect(dialog.getByRole('button', { name: 'Thu' })).toBeVisible()
        await settled(qa)
        await qa.shot('so-thu-ho-co-the-thu')
        await qa.page.keyboard.press('Escape')
        const dialogB = await openCollection(qa, u.b.name)
        await qa.expect(dialogB.getByText('Người này chưa đồng ý điều khoản Voice ID mới. Họ bật ở trang Giọng nói của tôi.')).toBeVisible()
        c.fact('no record button for a non-consenting person', (await dialogB.getByRole('button', { name: 'Thu' }).count()) === 0)
        const colB = await personCollection(qa, u.b.id)
        c.fact('not consented, not collectible', colB.consented === false && colB.canCollect === false)
        await settled(qa)
        await qa.shot('so-thu-ho-chua-dong-y')
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
        c.done()
      },
    },
    {
      id: 'AC52',
      role: 'admin',
      title: 'Người giữ quyền thu cho người đã đồng ý: đánh dấu hỏi ý, ghi, lưu; người đó nhận thông báo và thấy mẫu ở thẻ thu hộ (TRANS_FT53)',
      steps: [
        'qa-fe10-a@ đã đồng ý; admin mở sổ thu hộ, bấm "Thu", đánh dấu "Đã hỏi, <name> đồng ý…", ghi micro giả 8 giây, dừng, nghe thử',
        'Lưu mẫu thật qua API (201 MSG473); đọc lại sổ (số lượng tăng, "Ngoài cuộc họp"); phía qa-fe10-a@: thông báo + thẻ "Người khác thu hộ"',
      ],
      expected:
        'Hộp "Thu giọng mẫu: <name>" nhắc dùng micro phòng họp; "Lưu giọng mẫu" khoá tới khi đánh dấu. Toast "Đã lưu giọng mẫu thu hộ." (201 MSG473, API trả cùng mã và lời); sổ hiện mẫu mới "Ngoài cuộc họp", số lượng tăng. Người đó nhận thông báo về mẫu và thấy ở thẻ "Người khác thu hộ" của "/mgmt/my-voice". Ghi nhận: micro giả chỉ phát tiếng bíp nên mẫu trong trình duyệt bị từ chối như AC48, mẫu giữ lại lấy từ đoạn đọc QA qua API.',
      async run(qa) {
        const u = await users(qa)
        const c = checker(qa)
        await enrolled(qa, 'a', 0)
        await clearPersonSamples(qa, u.a.id)
        await qa.signIn('admin')
        const dialog = await openCollection(qa, u.a.name)
        await press(qa, dialog.getByRole('button', { name: 'Thu' }))
        const rec = qa.page.getByRole('dialog', { name: `Thu giọng mẫu: ${u.a.name}` })
        await qa.expect(rec.getByText(/Nên dùng micro phòng họp/)).toBeVisible()
        const save = rec.getByRole('button', { name: 'Lưu giọng mẫu' })
        c.fact('no take yet: only the record button', (await save.count()) === 0)
        const ask = rec.getByRole('checkbox')
        await press(qa, ask)
        await qa.expect(ask).toBeChecked()
        await rec.getByRole('button', { name: 'Bắt đầu ghi' }).click()
        await qa.expect(rec.getByRole('button', { name: 'Dừng' })).toBeEnabled({ timeout: 20_000 })
        await rec.getByRole('button', { name: 'Dừng' }).click()
        await qa.expect(rec.getByLabel('Nghe lại giọng mẫu vừa ghi')).toBeVisible({ timeout: 15_000 })
        await qa.page.keyboard.press('Escape')
        const kept = await collectFor(qa, u.a.id)
        c.check('a proxy sample is kept', kept, 201, 'MSG473')
        c.fact('the toast words', kept.body?.message === 'Đã lưu giọng mẫu thu hộ.')
        const sampleId = kept.body?.data?.id
        const dialog2 = await openCollection(qa, u.a.name)
        await qa.expect(dialog2.getByText('1/5 mẫu thu hộ')).toBeVisible()
        const row = dialog2.locator('li.voice-sample').first()
        await qa.expect(row.getByText('Ngoài cuộc họp')).toBeVisible()
        await qa.expect(row.getByText(new RegExp(esc(u.admin.name)))).toBeVisible()
        await settled(qa)
        await qa.shot('so-thu-ho-co-mau-moi')
        const notis = await as(qa, 'a').get('/api/notifications?pageSize=50')
        c.fact('the person is told', JSON.stringify(notis.body).includes('VoiceSampleCollected'))
        await qa.signIn(u.a.email)
        await openMyVoice(qa, '?tab=proxy')
        const proxy = qa.page.getByRole('region', { name: 'Người khác thu hộ' })
        await qa.expect(proxy.getByText('1/5')).toBeVisible()
        await qa.expect(proxy.getByText('Ngoài cuộc họp')).toBeVisible()
        await qa.expect(proxy.getByRole('button', { name: 'Nghe lại mẫu thu hộ 1' })).toBeVisible()
        await qa.expect(proxy.getByRole('button', { name: 'Xoá mẫu thu hộ 1' })).toBeVisible()
        await settled(qa)
        await qa.shot('nguoi-duoc-thu-thay-mau')
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
        c.done()
      },
    },
    {
      id: 'AC53',
      role: 'admin',
      title: 'Mẫu thu hộ không đạt: quá ngắn 422 MSG309, hộp thoại ở lại cho "Ghi lại" (TRANS_FT54)',
      steps: [
        'qa-fe10-a@ đã đồng ý; tải mẫu 1 giây qua API thu hộ → 422; trong hộp "Thu giọng mẫu" ghi micro giả rồi "Lưu giọng mẫu"',
      ],
      expected:
        'API: "Giọng mẫu quá ngắn — cần ít nhất 6 giây có tiếng nói. Hãy đọc hết đoạn văn mẫu.", 422 MSG309, hộp thoại ở lại cho "Ghi lại". Trình duyệt: tiếng bíp cũng bị từ chối với lời đó, hộp không đóng.',
      async run(qa) {
        const u = await users(qa)
        const c = checker(qa)
        await enrolled(qa, 'a', 0)
        await clearPersonSamples(qa, u.a.id)
        const short = await as(qa, 'admin').form(
          `/api/users/${u.a.id}/voice-collection/samples`,
          (() => {
            const f = sampleForm(qa, 'short', 'ngan.wav')
            f.append('personAgreed', 'true')
            return f
          })(),
        )
        c.check('a too-short take', short, 422, 'MSG309')
        c.fact('nothing kept', (await personCollection(qa, u.a.id)).samples.length === 0)
        await qa.signIn('admin')
        const dialog = await openCollection(qa, u.a.name)
        await press(qa, dialog.getByRole('button', { name: 'Thu' }))
        const rec = qa.page.getByRole('dialog', { name: `Thu giọng mẫu: ${u.a.name}` })
        await press(qa, rec.getByRole('checkbox'))
        await rec.getByRole('button', { name: 'Bắt đầu ghi' }).click()
        await qa.expect(rec.getByRole('button', { name: 'Dừng' })).toBeEnabled({ timeout: 20_000 })
        await rec.getByRole('button', { name: 'Dừng' }).click()
        await qa.expect(rec.getByLabel('Nghe lại giọng mẫu vừa ghi')).toBeVisible({ timeout: 15_000 })
        await rec.getByRole('button', { name: 'Lưu giọng mẫu' }).click()
        const alert = rec.getByRole('alert')
        await qa.expect(alert).toBeVisible({ timeout: 60_000 })
        c.fact('the beep is refused like a short take', /quá ngắn|6 giây/.test(await alert.innerText()))
        await qa.expect(rec.getByRole('button', { name: 'Ghi lại' })).toBeVisible()
        await qa.expect(rec).toBeVisible()
        await settled(qa)
        await qa.shot('mau-thu-ho-khong-dat')
        c.fact('still nothing kept', (await personCollection(qa, u.a.id)).samples.length === 0)
        await noRawCodes(qa)
        c.done()
      },
    },
    {
      id: 'AC54',
      role: 'admin',
      title: 'Người giữ quyền nghe lại mẫu thu hộ: phát thành "Dừng", bấm nữa thôi, mỗi lượt nghe ghi nhật ký (TRANS_FT55)',
      steps: [
        'Thu 1 mẫu cho qa-fe10-a@ (API); admin mở sổ, bấm "Nghe lại mẫu 1 của <name>" rồi bấm lại',
        'Đối chiếu nhật ký kiểm toán (action voice.sample_played, người nghe là admin)',
      ],
      expected:
        'Mẫu phát, nút thành "Dừng" (aria-pressed), bấm nữa thì dừng, nút về "Nghe lại". Nhật ký có một dòng VoiceSamplePlayed cho mẫu đó, người thực hiện là admin. Ghi nhận: theo dõi bước này trên giao diện, không phải API.',
      async run(qa) {
        const u = await users(qa)
        const c = checker(qa)
        await enrolled(qa, 'a', 0)
        await clearPersonSamples(qa, u.a.id)
        const kept = await collectFor(qa, u.a.id)
        c.check('a proxy sample is kept', kept, 201, 'MSG473')
        const sampleId = kept.body?.data?.id
        c.fact('a sample id is returned', !!sampleId, JSON.stringify(kept.body?.data))
        await qa.signIn('admin')
        const dialog = await openCollection(qa, u.a.name)
        const play = dialog.getByRole('button', { name: `Nghe lại mẫu 1 của ${u.a.name}` })
        const heard = qa.page.waitForResponse((r) => r.url().includes(`/api/users/${u.a.id}/voice-collection/samples/${sampleId}/audio`), { timeout: 30_000 })
        await press(qa, play)
        c.fact('the collector hears it back', (await heard).status() === 200)
        await qa.expect(dialog.getByRole('button', { name: `Dừng mẫu 1 của ${u.a.name}` })).toBeVisible()
        await settled(qa)
        await qa.shot('nguoi-thu-nghe-lai-mau')
        await press(qa, dialog.getByRole('button', { name: `Dừng mẫu 1 của ${u.a.name}` }))
        await qa.expect(dialog.getByRole('button', { name: `Nghe lại mẫu 1 của ${u.a.name}` })).toBeVisible()
        const audit = await as(qa, 'admin').get(`/api/audit-log?action=voice.sample_played&actor=${u.admin.id}&pageSize=100`)
        const items = audit.body?.items ?? audit.body?.data ?? []
        c.fact('each play is audited', JSON.stringify(items).includes(sampleId), `audit rows: ${JSON.stringify(items).length}`)
        await noRawCodes(qa)
        c.done()
      },
    },
    {
      id: 'AC55',
      role: 'admin',
      title: 'Người giữ quyền xoá mẫu thu hộ: "Giữ lại" không đổi, "Xoá" thì mất, vẫn thu tiếp được (TRANS_FT56)',
      steps: [
        'Thu 1 mẫu cho qa-fe10-a@ (API); admin mở sổ, bấm thùng rác "Xoá mẫu 1 của <name>" → "Giữ lại"',
        'Bấm rác lại → "Xoá"; đọc lại sổ và số lượng',
      ],
      expected:
        'Hộp "Xoá mẫu thu hộ?" ghi "Mẫu này của <name> bị xoá hẳn. Muốn thay thì thu lại mẫu mới.". "Giữ lại" đóng hộp, mẫu và "1/5 mẫu thu hộ" giữ nguyên. "Xoá" → toast thành công (200 MSG474), mẫu khỏi danh sách, còn "0/5 mẫu thu hộ"; nút "Thu" vẫn thu được mẫu mới.',
      async run(qa) {
        const u = await users(qa)
        const c = checker(qa)
        await enrolled(qa, 'a', 0)
        await clearPersonSamples(qa, u.a.id)
        c.check('a proxy sample is kept', await collectFor(qa, u.a.id), 201, 'MSG473')
        await qa.signIn('admin')
        const dialog = await openCollection(qa, u.a.name)
        await qa.expect(dialog.getByText('1/5 mẫu thu hộ')).toBeVisible()
        await press(qa, dialog.getByRole('button', { name: `Xoá mẫu 1 của ${u.a.name}` }))
        const confirm = qa.page.getByRole('dialog', { name: 'Xoá mẫu thu hộ?' })
        await qa.expect(confirm.getByText(`Mẫu này của ${u.a.name} bị xoá hẳn. Muốn thay thì thu lại mẫu mới.`)).toBeVisible()
        await settled(qa)
        await qa.shot('xac-nhan-xoa-mau-thu-ho')
        await press(qa, confirm.getByRole('button', { name: 'Giữ lại' }))
        await qa.expect(confirm).toHaveCount(0)
        c.fact('keeping changes nothing', (await personCollection(qa, u.a.id)).samples.length === 1)
        await press(qa, dialog.getByRole('button', { name: `Xoá mẫu 1 của ${u.a.name}` }))
        const again = qa.page.getByRole('dialog', { name: 'Xoá mẫu thu hộ?' })
        await press(qa, again.getByRole('button', { name: 'Xoá' }))
        await qa.expect(toast(qa, MSG.MSG474)).toBeVisible({ timeout: 15_000 })
        await qa.expect(dialog.getByText('0/5 mẫu thu hộ')).toBeVisible()
        c.fact('the sample is gone', (await personCollection(qa, u.a.id)).samples.length === 0)
        c.fact('recording still works', (await dialog.getByRole('button', { name: 'Thu' }).count()) === 1)
        await settled(qa)
        await qa.shot('so-thu-ho-sau-xoa')
        await noRawCodes(qa)
        c.done()
      },
    },
    {
      id: 'AC56',
      role: 'admin',
      title: 'Tài khoản thiếu "Thu giọng mẫu hộ" mà vẫn quản trị người dùng: dòng người dùng không nút "Giọng nói", gọi thẳng 403 (TRANS_FT57)',
      steps: [
        'QA lead gỡ "Thu giọng mẫu hộ" khỏi vai Quản trị viên (API ma trận), khôi phục cuối bài; admin mở "/mgmt/admin/users"',
        'Gọi thẳng GET /api/users/{id}/voice-collection (403); trả quyền, đọc lại sổ thu hộ',
      ],
      expected:
        'Không dòng nào hiện nút "Giọng nói" hay "Quản lý giọng nói thu hộ của <name>"; các tác vụ khác nguyên vẹn. Gọi thẳng thiếu quyền 403. Trả quyền xong sổ thu hộ mở lại bình thường.',
      async run(qa) {
        const u = await users(qa)
        const admin = as(qa, 'admin')
        const c = checker(qa)
        const matrix = (await admin.get('/api/roles/capabilities')).body
        const adminRole = matrix.roles.find((r) => r.name === 'Quản trị viên')
        const kept = adminRole.capabilities.filter((x) => x !== 'VoiceCollector')
        try {
          c.fact('VoiceCollector removed', (await admin.put('/api/roles/capabilities', { roleId: adminRole.id, capabilities: kept })).status === 200)
          await qa.signIn('admin')
          await qa.goto('/mgmt/admin/users')
          await qa.expect(qa.page.getByRole('heading', { level: 1, name: 'Người dùng' })).toBeVisible({ timeout: 15_000 })
          const row = qa.page.locator('tr').filter({ hasText: u.a.name }).first()
          await qa.expect(row).toBeVisible()
          c.fact('no voice button on the row', (await row.getByRole('button', { name: /Quản lý giọng nói thu hộ/ }).count()) === 0)
          c.fact('no voice label on the row', (await row.getByText('Giọng nói').count()) === 0)
          c.fact('other row actions stay', (await row.getByRole('button', { name: /đặt lại mật khẩu/i }).count()) > 0)
          await settled(qa)
          await qa.shot('dong-nguoi-dung-thieu-nut-giong-noi')
          c.check('direct call without the capability', await admin.get(`/api/users/${u.a.id}/voice-collection`), 403)
          await noSidewaysScroll(qa)
          await noRawCodes(qa)
          c.done()
        } finally {
          const back = await admin.put('/api/roles/capabilities', { roleId: adminRole.id, capabilities: adminRole.capabilities })
          qa.expect(back.status, 'the VoiceCollector capability is restored').toBe(200)
        }
      },
    },
    {
      id: 'AC57',
      role: 'admin',
      title: 'Người được thu hộ đọc, nghe và xoá mẫu ở thẻ "Người khác thu hộ": đếm, "Ngoài cuộc họp", hỏi trước khi xoá (TRANS_FT50)',
      steps: [
        'Thu 1 mẫu cho qa-fe10-a@ (API, ngoài cuộc họp); đăng nhập qa-fe10-a@, mở "/mgmt/my-voice?tab=proxy"',
        'Bấm "Nghe lại mẫu thu hộ 1", rồi "Xoá mẫu thu hộ 1" (xác nhận); đọc thẻ khi trống',
      ],
      expected:
        'Thẻ "Người khác thu hộ" hiện "1/5" và mỗi mẫu với "Ngoài cuộc họp", "<người thu> · <ngày>" (tooltip kèm độ dài và giờ). Bấm nghe thì phát, nút thành "Dừng". Xoá hỏi "Xoá mẫu thu hộ?" — "Mẫu này bị xoá hẳn, không lấy lại được."; xong thẻ về "Chưa có mẫu nào.". Ghi nhận: giới hạn thu hộ hiện là 5 mẫu/người, case ghi 50 đã cũ.',
      async run(qa) {
        const u = await users(qa)
        const c = checker(qa)
        await enrolled(qa, 'a', 0)
        await clearPersonSamples(qa, u.a.id)
        c.check('a proxy sample is kept', await collectFor(qa, u.a.id), 201, 'MSG473')
        await qa.signIn(u.a.email)
        await openMyVoice(qa, '?tab=proxy')
        const card = qa.page.getByRole('region', { name: 'Người khác thu hộ' })
        await qa.expect(card.getByText('1/5')).toBeVisible()
        await qa.expect(card.getByText('Ngoài cuộc họp')).toBeVisible()
        await qa.expect(card.getByText(new RegExp(esc(u.admin.name)))).toBeVisible()
        c.fact('the play hint names length and time', /giọng nói, thu lúc/.test(await card.locator('.field-hint[data-tooltip]').first().getAttribute('data-tooltip') ?? ''))
        const heard = qa.page.waitForResponse((r) => r.url().includes('/api/me/voice/samples/'), { timeout: 30_000 })
        await press(qa, card.getByRole('button', { name: 'Nghe lại mẫu thu hộ 1' }))
        c.fact('the person hears the proxy sample', (await heard).status() === 200)
        await qa.expect(card.getByRole('button', { name: 'Dừng mẫu thu hộ 1' })).toBeVisible()
        await settled(qa)
        await qa.shot('the-thu-ho-co-mau')
        await press(qa, card.getByRole('button', { name: 'Dừng mẫu thu hộ 1' }))
        await press(qa, card.getByRole('button', { name: 'Xoá mẫu thu hộ 1' }))
        const confirm = qa.page.getByRole('dialog', { name: 'Xoá mẫu thu hộ?' })
        await qa.expect(confirm.getByText('Mẫu này bị xoá hẳn, không lấy lại được.')).toBeVisible()
        await press(qa, confirm.getByRole('button', { name: 'Xoá' }))
        await qa.expect(toast(qa, 'Đã xoá giọng mẫu.')).toBeVisible({ timeout: 15_000 })
        await qa.expect(card.getByText('Chưa có mẫu nào.')).toBeVisible()
        await settled(qa)
        await qa.shot('the-thu-ho-trong')
        const gone = await voice(qa, 'a')
        c.fact('the proxy set is empty', gone.proxy.samples.length === 0)
        await noSidewaysScroll(qa)
        await noRawCodes(qa)
        c.done()
      },
    },
    {
      id: 'AC58',
      role: 'admin',
      title: 'Người đồng ý bản cũ được mời đồng ý lại: đọc thay đổi, "Để sau" giữ nguyên, đồng ý thì giữ mẫu (TRANS_FT51)',
      steps: [
        'Giữ 1 mẫu cho qa-fe10-b@ rồi lùi đồng ý của họ về 1.2 trong DB (điều kiện chỉ dựng được ở DB); đăng nhập qa-fe10-b@, bấm "Xem và đồng ý"',
        'Đọc hộp "Điều khoản Voice ID mới", bấm "Để sau"; mở lại, bấm "Đồng ý điều khoản mới"',
      ],
      expected:
        'Băng "Điều khoản Voice ID đã thay đổi.". Hộp "Điều khoản Voice ID mới" liệt kê thay đổi (ví dụ "Thư ký cuộc họp không còn thu giọng mẫu hộ. Người thu hộ hỏi ý bạn trước mỗi lần thu.") và "Chưa đồng ý thì mọi thứ giữ như cũ.". "Đồng ý điều khoản mới" gia hạn (200 MSG305) giữ nguyên mẫu; "Để sau" không đổi gì.',
      async run(qa) {
        const u = await users(qa)
        const c = checker(qa)
        const kept = await enrolled(qa, 'b', 1)
        backdateConsent(u.b.id, '1.2')
        try {
          await qa.signIn(u.b.email)
          await openMyVoice(qa)
          await qa.expect(qa.page.getByText('Điều khoản Voice ID đã thay đổi.')).toBeVisible()
          await settled(qa)
          await qa.shot('thong-bao-dieu-khoan-moi')
          await press(qa, qa.page.getByRole('button', { name: 'Xem và đồng ý' }))
          const dialog = qa.page.getByRole('dialog', { name: 'Điều khoản Voice ID mới' })
          await qa.expect(dialog.getByText(/Thư ký cuộc họp không còn thu giọng mẫu hộ/)).toBeVisible()
          await qa.expect(dialog.getByText('Chưa đồng ý thì mọi thứ giữ như cũ.')).toBeVisible()
          await settled(qa)
          await qa.shot('hop-dieu-khoan-moi')
          await press(qa, dialog.getByRole('button', { name: 'Để sau' }))
          await qa.expect(dialog).toHaveCount(0)
          c.fact('waiting changes nothing', (await voice(qa, 'b')).consent?.noticeVersion === '1.2')
          await press(qa, qa.page.getByRole('button', { name: 'Xem và đồng ý' }))
          const again = qa.page.getByRole('dialog', { name: 'Điều khoản Voice ID mới' })
          await press(qa, again.getByRole('button', { name: 'Đồng ý điều khoản mới' }))
          await qa.expect(toast(qa, MSG.MSG305)).toBeVisible({ timeout: 15_000 })
          const after = await voice(qa, 'b')
          c.fact('the consent is renewed', after.consent?.noticeVersion === after.noticeVersion)
          c.fact('the sample is kept', after.samples.length === 1 && after.samples[0].id === kept.samples[0].id)
          c.fact('proxy collection allowed again', after.proxy.allowed === true)
          await noSidewaysScroll(qa)
          await noRawCodes(qa)
          c.done()
        } finally {
          await withdrawn(qa, 'b')
        }
      },
    },
    {
      id: 'AC59',
      role: 'admin',
      title: 'Quản trị viên "Thử kết nối": API mẫu viết stub trả xanh kèm giây, máy local cũng vậy, không lưu gì (TRANS_FT61)',
      steps: [
        'Mở stub API QA (https 127.0.0.1:11439, chứng chỉ per-user); admin mở "/mgmt/admin/ai"',
        'Chọn "API", điền địa chỉ/model/key stub, bấm "Thử kết nối"; chọn "Máy local", bấm "Thử kết nối"',
      ],
      expected:
        'Mỗi lần bấm: nút hiện "Đang thử…" rồi dòng xanh "Kết nối được <model> · <n,n> giây" (200 MSG412). Chân vẫn "Chưa lưu" khi biểu mẫu khác cấu hình đang dùng; không gì được lưu (GET lại vẫn Máy local). Ghi nhận: API thật do QA lead cấp; ở đây stub OpenAI-compatible per-user dựng cho bài kiểm.',
      async run(qa) {
        const c = checker(qa)
        await startStub(qa)
        try {
          await qa.signIn('admin')
          await qa.goto('/mgmt/admin/ai')
          await qa.expect(qa.page.getByRole('heading', { level: 1, name: 'Cấu hình AI' })).toBeVisible({ timeout: 15_000 })
          await qa.expect(qa.page.getByText('Máy viết dự thảo biên bản')).toBeVisible()
          await qa.expect(qa.page.getByRole('radio', { name: 'Máy local' })).toBeVisible()
          await qa.expect(qa.page.getByRole('radio', { name: 'API' })).toBeVisible()
          const form = qa.page.locator('form.ai-settings')
          await press(qa, qa.page.getByRole('radio', { name: 'API' }))
          await form.getByPlaceholder('https://opencode.ai/inference/go/v1').fill('https://127.0.0.1:11439')
          await form.getByPlaceholder('deepseek-v4.1-flash').fill('qa-stub-model')
          await form.getByPlaceholder(/Dán khoá API|Đã lưu/).fill('qa-key-1234')
          const tryApi = qa.page.getByRole('button', { name: 'Thử kết nối API' })
          await press(qa, tryApi)
          const okApi = qa.page.locator('p.alert-success[role="status"]')
          await qa.expect(okApi).toBeVisible({ timeout: 60_000 })
          c.fact('the stub API answers', /Kết nối được qa-stub-model · [\d,]+ giây/.test(await okApi.innerText()), await okApi.innerText())
          await qa.expect(qa.page.getByText(/Chưa lưu · đang dùng/)).toBeVisible()
          await settled(qa)
          await qa.shot('thu-ket-noi-api')
          c.fact('testing saves nothing', (await as(qa, 'admin').get('/api/admin/ai')).body?.mode !== 'Api')
          await press(qa, qa.page.getByRole('radio', { name: 'Máy local' }))
          const tryLocal = qa.page.getByRole('button', { name: 'Thử kết nối máy local' })
          await press(qa, tryLocal)
          const okLocal = qa.page.locator('p.alert-success[role="status"]')
          await qa.expect(okLocal).toBeVisible({ timeout: 120_000 })
          c.fact('the in-house model answers', /Kết nối được qwen2\.5.*· [\d,]+ giây/.test(await okLocal.innerText()), await okLocal.innerText())
          await settled(qa)
          await qa.shot('thu-ket-noi-may-local')
          await noSidewaysScroll(qa)
          await noRawCodes(qa)
          c.done()
        } finally {
          stopStub()
        }
      },
    },
    {
      id: 'AC60',
      role: 'admin',
      title: 'Cấu hình đã lưu dẫn dự thảo: họp Thường viết bằng API stub, họp Mật vẫn máy local, tải lại giữ chọn (TRANS_FT64)',
      steps: [
        'Mở stub API QA; admin chọn "API", điền stub, "Lưu"; tải lại trang cấu hình',
        'Thư ký xin dự thảo họp Thường (API viết, model qa-stub-model) và họp Mật (máy local viết); trả cấu hình về Máy local, xin lại cả hai',
      ],
      expected:
        'Trước lưu chân "Chưa lưu · đang dùng Máy local"; "Lưu" hiện "Đang lưu…" rồi toast "Đã lưu cấu hình AI." (200 MSG408). Ô key trống với giữ chỗ "Đã lưu ••••1234"; tải lại không thấy key, chân "Đang dùng API · qa-stub-model". Họp Thường: dự thảo do API viết (chân nêu qa-stub-model). Họp Mật: vẫn máy local (chân nêu model local). Cuối bài trả về Máy local và xin lại cả hai bản.',
      async run(qa) {
        const admin = as(qa, 'admin')
        const sec = as(qa, 'secretary')
        const c = checker(qa)
        const mA = await meeting(qa, 'A')
        const mD = await meeting(qa, 'D')
        c.fact('the admin starts on Local', ((await admin.get('/api/admin/ai')).body?.mode ?? 'Local') === 'Local')
        await startStub(qa)
        try {
          await qa.signIn('admin')
          await qa.goto('/mgmt/admin/ai')
          await qa.expect(qa.page.getByRole('heading', { level: 1, name: 'Cấu hình AI' })).toBeVisible({ timeout: 15_000 })
          const form = qa.page.locator('form.ai-settings')
          await press(qa, qa.page.getByRole('radio', { name: 'API' }))
          await form.getByPlaceholder('https://opencode.ai/inference/go/v1').fill('https://127.0.0.1:11439')
          await form.getByPlaceholder('deepseek-v4.1-flash').fill('qa-stub-model')
          await form.getByPlaceholder(/Dán khoá API|Đã lưu/).fill('qa-key-1234')
          await qa.expect(qa.page.getByText(/Chưa lưu · đang dùng/)).toBeVisible()
          await press(qa, form.getByRole('button', { name: 'Lưu' }))
          await qa.expect(toast(qa, MSG.MSG408)).toBeVisible({ timeout: 15_000 })
          await qa.expect(form.getByPlaceholder('Đã lưu ••••1234')).toBeVisible()
          await qa.expect(qa.page.getByText(/Đang dùng API · qa-stub-model/)).toBeVisible()
          await qa.page.reload()
          await qa.expect(qa.page.getByRole('heading', { level: 1, name: 'Cấu hình AI' })).toBeVisible({ timeout: 15_000 })
          await qa.expect(qa.page.locator('form.ai-settings').getByPlaceholder('Đã lưu ••••1234')).toBeVisible()
          await qa.expect(qa.page.getByText(/Đang dùng API · qa-stub-model/)).toBeVisible()
          const keyValue = await qa.page.locator('form.ai-settings').getByPlaceholder('Đã lưu ••••1234').inputValue()
          c.fact('the key itself never comes back', keyValue === '')
          await settled(qa)
          await qa.shot('da-luu-api-tai-lai-van-dung')
          for (const [m, wantApi] of [[mA, true], [mD, false]]) {
            c.fact('queued on the stub API', (await sec.post(`/api/meetings/${m.id}/summary`, {})).status === 202)
            const until = Date.now() + 900_000
            let state
            for (;;) {
              state = (await sec.get(`/api/meetings/${m.id}/summary`)).body.summary
              if (state?.status !== 'Queued' && state?.status !== 'Running') break
              if (Date.now() > until) qa.inconclusive('Dự thảo API chưa xong sau 15 phút.')
              await new Promise((r) => setTimeout(r, 10000))
            }
            c.fact(`${m.title}: Ready`, state?.status === 'Ready', state?.status)
            if (wantApi) c.fact('the Normal meeting is written by the API', state?.model === 'qa-stub-model', state?.model)
            else c.fact('the Secret meeting stays on the local machine', /qwen2\.5/.test(state?.model ?? ''), state?.model)
          }
          await qa.signIn('secretary')
          await qa.goto(`/mgmt/meetings/${mA.id}?tab=minutes`)
          await qa.expect(qa.page.getByRole('heading', { level: 1, name: mA.title })).toBeVisible({ timeout: 15_000 })
          await qa.expect(qa.page.getByRole('region', { name: 'Dự thảo nội dung' }).getByText(/\(qa-stub-model\)/)).toBeVisible({ timeout: 30_000 })
          await settled(qa)
          await qa.shot('du-thao-hop-thuong-do-api-viet')
          await noRawCodes(qa)
          c.done()
        } finally {
          stopStub()
          const back = await admin.put('/api/admin/ai', { mode: 'Local', apiBaseUrl: '', apiModel: '', apiKey: null })
          if (back.status !== 200) qa.note(`Không trả được cấu hình về Máy local: HTTP ${back.status}. Cần trả tay ở /mgmt/admin/ai.`)
          for (const m of [mA, mD]) {
            if ((await sec.post(`/api/meetings/${m.id}/summary`, {})).status === 202) {
              const until = Date.now() + 900_000
              for (;;) {
                const s = (await sec.get(`/api/meetings/${m.id}/summary`)).body.summary
                if (s?.status !== 'Queued' && s?.status !== 'Running') break
                if (Date.now() > until) break
                await new Promise((r) => setTimeout(r, 10000))
              }
            }
          }
        }
      },
    },
  ],
}

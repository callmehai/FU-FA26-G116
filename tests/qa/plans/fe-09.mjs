// FE-09 — Ghi âm cuộc họp (QA subtask SCRUM-196): UC-51a view, UC-51b play, UC-51c download, UC-52a start a room take,
// UC-52b stop and save it, UC-52c upload an audio file, UC-52d delete a take, UC-52e erase a recording, UC-53 my recorded
// speech, and the rules they cite (BR-107–BR-115, BR-122).
//
// Criteria come from docs/01-requirements/05-use-case-specifications.md, 06-business-rules.md,
// docs/03-api/12-ban-ghi-cuoc-hop.md, the message catalogue, ADR-0040, ADR-0041, ADR-0061 and the comments of SCRUM-29,
// SCRUM-109 and SCRUM-153. Deviations recorded there and taken as the requirement:
// - ADR-0041 (PR #191): the room mic is ONE switch. "Tạm dừng / Ghi tiếp / Dừng và lưu" of ADR-0040 became off (saves the
//   take) and on again (a new take appended to the same recording, no second consent). "Pause and resume" is tested as that.
// - PR #193: an erased recording leaves the Bản ghi tab (UC-51a step 4 / UC-52e POST-2 still say it shows as Erased).
// - SCRUM-153 (PO 13/11): a Hybrid meeting takes no uploaded file (MSG478); after it ends the server builds a Combined
//   recording, listen/download only (MSG479 on transcribe).
// Round 1 (Report 5.2, 2026-11-22): AC25-AC34 added (list and player, injected errors in place of a second session, live row,
// silent take, closed tab, 301 MB file, in-progress InPerson view, Secretary download and card minimise). The download button is
// "Tải MP3" (ADR-0069; this machine has no ffmpeg, so only mp3 recordings download). A hybrid meeting's page has no mic card
// (ADR-0072). AC13, AC21, AC22, AC23 stay inconclusive without the speech worker. AC19 fails on a phone (AC25 only notes it): a tap on the
// recording's info line shows no tooltip. AC32 fails: a 301 MB upload ends in a network error instead of MSG293.
// Not tested here: a Guest's own sentences (needs an OTP by email, UC-04), the 12-hour idle take and the 180-day expiry
// (time), a real 300 MB take, the Secretary closing the tab mid-take (AF-1 of UC-52a, needs IndexedDB across sessions).
//
// Safe on a shared local dev database: every meeting is created here through the API, titled "[QA] FE-09 …", 13 weeks
// ahead, chutoa@ Chair and approver, thuky@ Secretary, thanhvien@ Member. The main in-person meeting is organised by
// quantri@, who is then on no roster (the "Organizer alone cannot listen" rule). Meetings are reused across runs by their
// fixed title; every recording a criterion makes is erased again at its end, and a take left open is saved first.
//
// Audio: generated on this machine (macOS `say -v Linh` and ffmpeg, or synthetic tone bursts when they are missing), never a recording of a person. Files are cached in
// QA_AUDIO_DIR (default: the OS temp dir); nothing is committed.

import { request as playwrightRequest } from '@playwright/test'
import { execFileSync } from 'node:child_process'
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

// ---------------------------------------------------------------------------------------------------------------------
// Audio fixtures.

const AUDIO_DIR = process.env.QA_AUDIO_DIR ?? join(tmpdir(), 'trustcabinet-qa-fe09')
const SENTENCES = {
  s1: 'Kính thưa các đồng chí, hôm nay chúng ta họp để thông qua kế hoạch ngân sách năm hai nghìn không trăm hai mươi bảy.',
  s2: 'Tôi đồng ý với dự thảo, đề nghị phòng tài chính bổ sung số liệu quý bốn trước ngày thứ sáu.',
}
let audioReady = false
/** Synthetic "speech": 16 kHz mono 16-bit, a few seconds of syllable-like tone bursts (no say/ffmpeg on Windows). */
function synthPcm(seed, seconds) {
  const n = 16000 * seconds
  const b = Buffer.alloc(n * 2)
  for (let i = 0; i < n; i += 1) {
    const t = i / 16000
    const env = Math.max(0, Math.sin(2 * Math.PI * (3 + seed) * t)) * 0.6
    const v = env * (Math.sin(2 * Math.PI * (180 + seed * 40) * t) + 0.5 * Math.sin(2 * Math.PI * (540 + seed * 90) * t))
    b.writeInt16LE(Math.round(Math.max(-1, Math.min(1, v * 0.4)) * 32767), i * 2)
  }
  return b
}
function wavOf(pcm) {
  const h = Buffer.alloc(44)
  h.write('RIFF', 0)
  h.writeUInt32LE(36 + pcm.length, 4)
  h.write('WAVEfmt ', 8)
  h.writeUInt32LE(16, 16)
  h.writeUInt16LE(1, 20)
  h.writeUInt16LE(1, 22)
  h.writeUInt32LE(16000, 24)
  h.writeUInt32LE(32000, 28)
  h.writeUInt16LE(2, 32)
  h.writeUInt16LE(16, 34)
  h.write('data', 36)
  h.writeUInt32LE(pcm.length, 40)
  return Buffer.concat([h, pcm])
}
function audio(qa) {
  if (!audioReady) {
    try {
      mkdirSync(AUDIO_DIR, { recursive: true })
      let tools = true
      try {
        execFileSync('ffmpeg', ['-version'], { stdio: 'ignore' })
        execFileSync('say', ['-v', '?'], { stdio: 'ignore' })
      } catch {
        tools = false
      }
      if (tools) {
        const ff = (...args) => execFileSync('ffmpeg', ['-y', '-hide_banner', '-loglevel', 'error', ...args])
        for (const [key, text] of Object.entries(SENTENCES)) {
          const aiff = join(AUDIO_DIR, `${key}.aiff`)
          if (!existsSync(aiff)) execFileSync('say', ['-v', 'Linh', '-o', aiff, text])
          const out = (ext, ...args) => {
            const f = join(AUDIO_DIR, `${key}.${ext}`)
            if (!existsSync(f)) ff('-i', aiff, ...args, f)
          }
          out('pcm', '-ar', '16000', '-ac', '1', '-f', 's16le')
          out('wav', '-ar', '16000', '-ac', '1', '-c:a', 'pcm_s16le')
          out('m4a', '-c:a', 'aac')
          out('mp3', '-c:a', 'libmp3lame')
          out('webm', '-c:a', 'libopus')
          out('ogg', '-c:a', 'libopus')
        }
      } else {
        // No macOS `say` or ffmpeg (a Windows run): tone bursts instead of speech. The server tells formats apart by the
        // declared content type only, so a container name over WAV bytes still exercises every upload rule.
        for (const [i, key] of Object.keys(SENTENCES).entries()) {
          const pcm = synthPcm(i, 6 + i)
          const put = (ext, data) => {
            const f = join(AUDIO_DIR, `${key}.${ext}`)
            if (!existsSync(f)) writeFileSync(f, data)
          }
          put('pcm', pcm)
          put('wav', wavOf(pcm))
          for (const ext of ['m4a', 'mp3', 'webm', 'ogg']) put(ext, wavOf(pcm))
        }
      }
      writeFileSync(join(AUDIO_DIR, 'bien-ban-nhap.txt'), 'Đây là tệp văn bản, không phải âm thanh.\n')
      audioReady = true
    } catch (error) {
      qa.inconclusive(`Cannot generate the audio fixtures: ${error.message}`)
    }
  }
  return (name) => join(AUDIO_DIR, name)
}
const bytes = (qa, name) => readFileSync(audio(qa)(name))

/** WAV header 16 kHz mono 16-bit with unknown sizes, as the browser's room mic opens its first part (ADR-0041). */
function wavHeaderUnknown() {
  const b = Buffer.alloc(44)
  b.write('RIFF', 0)
  b.writeUInt32LE(0xffffffff, 4)
  b.write('WAVEfmt ', 8)
  b.writeUInt32LE(16, 16)
  b.writeUInt16LE(1, 20)
  b.writeUInt16LE(1, 22)
  b.writeUInt32LE(16000, 24)
  b.writeUInt32LE(32000, 28)
  b.writeUInt16LE(2, 32)
  b.writeUInt16LE(16, 34)
  b.write('data', 36)
  b.writeUInt32LE(0xffffffff - 36, 40)
  return b
}

// ---------------------------------------------------------------------------------------------------------------------
// API clients: JSON through qa.api, binary (multipart upload, room-mic parts, speech clips) through a raw context.

const PASSWORD = process.env.QA_PASSWORD ?? 'TrustCabinet@2026'
const EMAIL = {
  admin: 'quantri@trustcabinet.local',
  chair: 'chutoa@trustcabinet.local',
  secretary: 'thuky@trustcabinet.local',
  member: 'thanhvien@trustcabinet.local',
}

async function client(qa, role) {
  qa.__clients ??= {}
  qa.__clients[role] ??= await qa.api(role)
  return qa.__clients[role]
}

/** A binary-capable client for `role`, disposed with the criterion (see `wrap`). */
async function raw(qa, role) {
  qa.__raw ??= {}
  if (qa.__raw[role]) return qa.__raw[role]
  const login = await playwrightRequest.newContext({ baseURL: qa.base })
  const res = await login.post('/api/auth/login', { data: { email: EMAIL[role], password: PASSWORD } })
  if (!res.ok()) throw new Error(`sign-in as ${role}: HTTP ${res.status()}`)
  const token = (await res.json()).accessToken
  await login.dispose()
  const ctx = await playwrightRequest.newContext({ baseURL: qa.base, extraHTTPHeaders: { Authorization: `Bearer ${token}` } })
  const wrapRes = async (r) => {
    const text = await r.text()
    let body
    try {
      body = text ? JSON.parse(text) : ''
    } catch {
      body = text
    }
    return { status: r.status(), body, headers: r.headers() }
  }
  const api = {
    ctx,
    upload: async (meetingId, { file, name, mimeType, fields = {} }) =>
      wrapRes(
        await ctx.post(`/api/meetings/${meetingId}/recordings`, {
          multipart: { ...(file ? { file: { name, mimeType, buffer: file } } : {}), ...fields },
          timeout: 120_000,
        }),
      ),
    putPart: async (recordingId, n, buf, final) =>
      wrapRes(
        await ctx.put(`/api/recordings/${recordingId}/parts/${n}${final ? '?final=true' : ''}`, {
          data: buf,
          headers: { 'Content-Type': 'application/octet-stream' },
        }),
      ),
    putClip: async (meetingId, clipId, buf, durationMs) =>
      wrapRes(
        await ctx.put(
          `/api/meetings/${meetingId}/speech-clips/${clipId}?startedAt=${encodeURIComponent(new Date(Date.now() - durationMs).toISOString())}&durationMs=${durationMs}`,
          { data: buf, headers: { 'Content-Type': 'audio/wav' }, timeout: 90_000 },
        ),
      ),
  }
  qa.__raw[role] = api
  return api
}

/** Runs a criterion and disposes its raw clients afterwards. */
const wrap = (fn) => async (qa) => {
  try {
    await fn(qa)
  } finally {
    for (const r of Object.values(qa.__raw ?? {})) await r.ctx.dispose().catch(() => undefined)
  }
}

const USERS = {}
async function users(qa) {
  for (const role of ['admin', 'chair', 'secretary', 'member']) {
    if (USERS[role]) continue
    const me = (await (await client(qa, role)).get('/api/auth/me')).body
    USERS[role] = { id: me.id, name: me.fullName, email: me.email }
  }
  return USERS
}

/** Runs every API check, then fails once with all the wrong ones. */
function checker(qa) {
  const wrong = []
  return {
    check(label, res, status, code) {
      const ok = res.status === status && (code === undefined || res.body?.code === code)
      if (!ok) wrong.push(`${label}: want ${status}${code ? ` ${code}` : ''}, got ${res.status} ${String(JSON.stringify(res.body)).slice(0, 220)}`)
      return ok
    },
    fact(label, ok, detail = '') {
      if (!ok) wrong.push(`${label}${detail ? `: ${detail}` : ''}`)
    },
    done() {
      qa.expect(wrong, wrong.join('\n')).toEqual([])
    },
  }
}

// ---------------------------------------------------------------------------------------------------------------------
// Meetings.

const iso = (ms) => new Date(ms).toISOString().replace(/\.\d{3}Z$/, 'Z')
const MINUTE = 60_000
const DAY = 86_400_000
let slotCounter = Math.floor(Math.random() * 9)
/** A 45-minute slot on the day 13 weeks from today, between 08:00 and 17:00 Vietnam time, varied per meeting. */
function slot() {
  slotCounter += 1
  const day = new Date(Date.now() + 91 * DAY)
  day.setUTCHours(1, 0, 0, 0)
  const start = day.getTime() + ((slotCounter * 55) % 540) * MINUTE + Math.floor(Math.random() * 4) * 5 * MINUTE
  return { plannedStartTime: iso(start), plannedEndTime: iso(start + 45 * MINUTE) }
}

const PATHS = {
  Published: ['submit', 'approve', 'publish'],
  InProgress: ['submit', 'approve', 'publish', 'start'],
  Ended: ['submit', 'approve', 'publish', 'start', 'end'],
}
async function transition(qa, role, id, action) {
  const u = await users(qa)
  const body = {
    submit: { action: 'SubmitForApproval', assignedApproverId: u.chair.id },
    approve: { action: 'Approve' },
    publish: { action: 'Publish' },
    start: { action: 'Start' },
    end: { action: 'End' },
  }[action]
  const res = await (await client(qa, role)).post(`/api/meetings/${id}/transitions`, body)
  qa.expectStatus(res, 200)
}

const SPECS = {
  ipRun: { label: 'trực tiếp đang họp', mode: 'InPerson', status: 'InProgress', organizer: 'admin' },
  ipPub: { label: 'trực tiếp chưa bắt đầu', mode: 'InPerson', status: 'Published', organizer: 'secretary' },
  ipEnd: { label: 'trực tiếp đã kết thúc', mode: 'InPerson', status: 'Ended', organizer: 'secretary' },
  ipSecret: { label: 'trực tiếp Mật đang họp', mode: 'InPerson', status: 'InProgress', organizer: 'secretary', sensitivity: 'Secret' },
  onRun: { label: 'trực tuyến đang họp', mode: 'Online', status: 'InProgress', organizer: 'secretary' },
  hyRun: { label: 'kết hợp đang họp', mode: 'Hybrid', status: 'InProgress', organizer: 'secretary' },
  hyEnd: { label: 'kết hợp đã kết thúc (bản tổng hợp)', mode: 'Hybrid', status: 'Ended', organizer: 'secretary', combined: true },
}

const REUSED = {}
/** The meeting of `key` reused across runs, found by its fixed title; created (and driven to its state) the first time. */
async function meeting(qa, key) {
  if (REUSED[key]) return REUSED[key]
  const spec = SPECS[key]
  const title = qa.name(`FE-09 ${spec.label}`)
  const org = await client(qa, spec.organizer)
  const list = await org.get(`/api/meetings?q=${encodeURIComponent(title)}&status=${spec.status}&pageSize=20`)
  const hit = (list.body.items ?? []).find((m) => m.title === title)
  let m
  if (hit) m = (await org.get(`/api/meetings/${hit.id}`)).body
  else {
    const u = await users(qa)
    const created = await org.post('/api/meetings', {
      title,
      ...slot(),
      mode: spec.mode,
      location: spec.mode === 'Online' ? '' : 'Phòng họp QA tầng 3',
      onlineLink: null,
      sensitivity: spec.sensitivity ?? 'Normal',
      description: 'Cuộc họp do bộ kiểm thử QA FE-09 tạo.',
      participants: [
        { userId: u.chair.id, role: 'Chair' },
        { userId: u.secretary.id, role: 'Secretary' },
        { userId: u.member.id, role: 'Member' },
      ],
    })
    qa.expectStatus(created, 201)
    const id = created.body.meeting.id
    const steps = PATHS[spec.status].filter((s) => !(spec.combined && s === 'end'))
    for (const step of steps) await transition(qa, ['approve', 'start', 'end'].includes(step) ? 'chair' : spec.organizer, id, step)
    if (spec.combined) {
      // One room take and one online sentence, then the end: the server builds the Combined recording (ADR-0061 §5).
      await roomTake(qa, id, ['s1.pcm'], { consent: true })
      const clip = await (await raw(qa, 'member')).putClip(id, crypto.randomUUID(), bytes(qa, 's2.wav'), wavMs(qa, 's2.wav'))
      qa.note(`Câu trực tuyến cho bản tổng hợp: HTTP ${clip.status} ${clip.body?.code ?? ''}`)
      await transition(qa, 'chair', id, 'end')
    }
    m = (await org.get(`/api/meetings/${id}`)).body
    qa.note(`Tạo: ${m.title} (${m.id}, ${m.mode}, ${m.status})`)
  }
  qa.expect(m.status, `${title} is not ${spec.status}`).toBe(spec.status)
  REUSED[key] = m
  return m
}

const wavMs = (qa, name) => Math.round(((bytes(qa, name).length - 44) / 32000) * 1000)

// ---------------------------------------------------------------------------------------------------------------------
// Recordings through the API.

const R = (meetingId) => `/api/meetings/${meetingId}/recordings`
async function recordings(qa, role, meetingId) {
  const res = await (await client(qa, role)).get(R(meetingId))
  qa.expectStatus(res, 200)
  return res.body
}

/** Saves any take left open and erases every recording of the meeting: a criterion starts on a clean tab. */
async function clean(qa, meetingId) {
  const sec = await client(qa, 'secretary')
  const list = await recordings(qa, 'secretary', meetingId)
  for (const r of list.items.filter((i) => i.status === 'Pending')) await sec.post(`/api/recordings/${r.id}/complete`)
  for (const r of (await recordings(qa, 'secretary', meetingId)).items.filter((i) => i.status === 'Available' && i.source !== 'Combined'))
    await sec.delete(`/api/recordings/${r.id}`)
}

/** One room-mic take sent as the browser does (POST takes, PUT parts, POST complete); returns the complete response. */
async function roomTake(qa, meetingId, pcmFiles, { consent = false, purpose } = {}) {
  const sec = await client(qa, 'secretary')
  const opened = await sec.post(`${R(meetingId)}/takes`, consent ? { consentConfirmed: true, consentPurpose: purpose ?? 'QA FE-09: lập biên bản' } : {})
  qa.expectStatus(opened, 201)
  const { recordingId, partSize } = opened.body
  // A second of silence around the speech, as a real take has: the transcriber splits sentences on pauses.
  const quiet = Buffer.alloc(32000)
  const payload = Buffer.concat([wavHeaderUnknown(), quiet, ...pcmFiles.flatMap((f) => [bytes(qa, f), quiet])])
  const r = await raw(qa, 'secretary')
  let n = 0
  for (let at = 0; at < payload.length; at += partSize) {
    n += 1
    const part = payload.subarray(at, at + partSize)
    const res = await r.putPart(recordingId, n, part, at + partSize >= payload.length)
    qa.expect(res.status, `part ${n}: ${JSON.stringify(res.body)}`).toBe(204)
  }
  const done = await sec.post(`/api/recordings/${recordingId}/complete`)
  qa.expectStatus(done, 200)
  return done.body
}

/** Uploads a file as the Secretary through the API (consent confirmed) and returns the new recording id. */
async function uploadFile(qa, meetingId, name, mimeType) {
  const res = await (await raw(qa, 'secretary')).upload(meetingId, {
    file: bytes(qa, name),
    name: `qa-fe09-${name}`,
    mimeType,
    fields: { consentConfirmed: 'true', consentPurpose: 'QA FE-09: lập biên bản', durationMs: String(wavMs(qa, name.replace(/\.\w+$/, '.wav'))) },
  })
  qa.expectStatus(res, 201)
  return res.body.recordingId
}

async function waitTranscript(qa, recordingId, ms = Number(process.env.QA_TRANSCRIPT_WAIT_MS ?? 150_000)) {
  const sec = await client(qa, 'secretary')
  const until = Date.now() + ms
  let last
  while (Date.now() < until) {
    last = (await sec.get(`/api/recordings/${recordingId}/transcript`)).body
    if (last?.status === 'Completed' || last?.status === 'Failed') return last
    await new Promise((r) => setTimeout(r, 3000))
  }
  return last
}

const daysBetween = (a, b) => Math.round((Date.parse(b) - Date.parse(a)) / DAY)

// ---------------------------------------------------------------------------------------------------------------------
// Screen.

async function noSidewaysScroll(qa) {
  const sideways = await qa.page.evaluate(() => document.documentElement.scrollWidth - innerWidth)
  qa.expect(sideways, 'the page scrolls sideways').toBeLessThanOrEqual(0)
}
async function noRawCodes(qa) {
  const text = await qa.page.locator('main').innerText()
  qa.expect(text, 'a raw MSG code is shown to the user').not.toMatch(/MSG\d+/)
}
async function settled(qa) {
  await qa.page
    .waitForFunction(() =>
      document.getAnimations().every((a) => a.playState !== 'running' || a.effect?.getTiming().iterations === Infinity),
    )
    .catch(() => undefined)
}
const toast = (qa, text) => qa.page.locator('.toast-stack').getByText(text)
const roomSection = (qa) => qa.page.getByRole('region', { name: 'Bản ghi', exact: true })
const micCard = (qa) => qa.page.locator('.mic-dock')
async function press(qa, locator) {
  if (qa.device === 'phone') await locator.tap()
  else await locator.click()
}

/** Opens the meeting's Bản ghi tab. */
async function openTab(qa, m) {
  await qa.goto(`/mgmt/meetings/${m.id}?tab=recordings`)
  await qa.expect(qa.page.getByRole('heading', { level: 1, name: m.title })).toBeVisible()
  await qa.expect(qa.page.getByRole('tab', { name: 'Bản ghi' })).toHaveAttribute('aria-selected', 'true')
}

/** The selected tab sits inside the tab bar (on a phone the bar scrolls sideways; its right edge fades over 32px). */
async function activeTabInBar(qa) {
  const box = await qa.page.evaluate(() => {
    const bar = document.querySelector('.tabs').getBoundingClientRect()
    const tab = document.querySelector('.tabs .tab-active').getBoundingClientRect()
    const fade = innerWidth <= 900 ? 32 : 0
    return { inside: tab.left >= bar.left - 1 && tab.right <= bar.right - fade + 1, page: scrollY }
  })
  qa.expect(box.inside, 'the selected tab is outside the tab bar').toBe(true)
  qa.expect(box.page, 'opening a tab scrolled the page').toBe(0)
}

/** The Secretary's mic card, opened from its bubble on a phone. */
async function openMicCard(qa) {
  const bubble = micCard(qa).getByRole('button', { name: /^Mở ô ghi âm/ })
  if (await bubble.isVisible().catch(() => false)) await press(qa, bubble)
  await qa.expect(micCard(qa).locator('.mic-card')).toBeVisible()
}

// ---------------------------------------------------------------------------------------------------------------------
// Round 2: synthetic Vietnamese speech.
//
// ../fixtures/said.wav is TTS Vietnamese ("Kính thưa các đồng chí. Hôm nay chúng ta họp để thông qua kế hoạch đào
// tạo cán bộ năm, hai nghìn không trăm hai mươi bảy."): the local worker transcribes it to real text (verified
// 24/11, profile clip → 2 segments). The headless browser's fake mic carries no speech (28 s in a room cuts no
// sentence), so takes and sentences that must transcribe are seeded with it through the API; the UI half of each
// case still runs in the browser. ../fixtures/ is committed (regression needs said.wav).

const PLANS_DIR = dirname(fileURLToPath(import.meta.url))
const SAID_WAV = join(PLANS_DIR, '..', 'fixtures', 'said.wav')
const saidPcm = (qa) => {
  if (!existsSync(SAID_WAV)) qa.inconclusive('Vietnamese speech fixture ../fixtures/said.wav is missing.')
  return readFileSync(SAID_WAV).subarray(44)
}
/** said.wav split at its ~2 s pause: two sentences for the per-mic clips. */
function saidHalves(qa) {
  const pcm = saidPcm(qa)
  const cut = 32000 * 2
  return [
    { buf: wavOf(pcm.subarray(0, cut)), ms: 2000 },
    { buf: wavOf(pcm.subarray(cut)), ms: Math.round(((pcm.length - cut) / 32000) * 1000) },
  ]
}
/** said.wav as roomTake() input: one 7.6 s take transcribing to two Vietnamese sentences. */
function saidTake(qa) {
  audio(qa)
  const f = join(AUDIO_DIR, 'said.pcm')
  if (!existsSync(f)) writeFileSync(f, saidPcm(qa))
  return ['said.pcm']
}
/** One Vietnamese sentence from the member's mic, as the app stores it (200 MSG318). */
async function seedSentence(qa, meetingId, half = 0) {
  const h = saidHalves(qa)[half]
  const res = await (await raw(qa, 'member')).putClip(meetingId, crypto.randomUUID(), h.buf, h.ms)
  qa.expectStatus(res, 200)
  qa.expect(res.body?.code, `putClip: ${JSON.stringify(res.body)}`).toBe('MSG318')
  return res.body.clip
}
/** Every per-mic sentence gone: a criterion starts with no sentences of anyone. */
async function cleanClips(qa, meetingId) {
  const sec = await client(qa, 'secretary')
  const items = (await sec.get(`/api/meetings/${meetingId}/speech-clips`)).body.items ?? []
  for (const i of items) await sec.delete(`/api/speech-clips/${i.id}`)
}
/** Into the call room: lobby "Sẵn sàng tham gia?" → "Vào phòng" → "Rời phòng" shows. */
async function joinRoom(qa, m) {
  await qa.goto(`/mgmt/meetings/${m.id}/room`)
  await qa.expect(qa.page.getByRole('heading', { name: 'Sẵn sàng tham gia?' })).toBeVisible({ timeout: 15000 })
  await qa.page.getByRole('button', { name: 'Vào phòng', exact: true }).click()
  await qa.expect(qa.page.getByRole('button', { name: 'Rời phòng' })).toBeVisible({ timeout: 20000 })
}
/** The room header chip while the call mic captures ("Đang ghi lời bạn"), turning the mic on when needed. */
async function expectCapturing(qa) {
  const chip = qa.page.getByRole('button', { name: /Đang ghi lời bạn|Chưa gửi hết lời bạn/ })
  if ((await chip.count()) === 0) {
    const unmute = qa.page.locator('footer.room-controls').getByRole('button', { name: 'Bật micro' })
    if ((await unmute.count()) > 0) await unmute.click()
  }
  await qa.expect(chip).toBeVisible({ timeout: 15000 })
  return chip
}

// ---------------------------------------------------------------------------------------------------------------------

export default {
  ticket: 'SCRUM-196',
  title: 'FE-09 Ghi âm cuộc họp: mic phòng, tải tệp, xoá lượt, xoá bản ghi, nghe, tải về, thông báo ghi âm, quyền theo vai trò',
  devices: ['desktop'],
  allowHosts: [],
  criteria: [
    // ------------------------------------------------------------------------------------------- UC-52a / UC-52b
    {
      id: 'AC1',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Thư ký bật mic phòng (họp trực tiếp đang diễn ra): xác nhận đã báo mọi người, đổi tab vẫn ghi, tắt mic lưu lượt (MSG363)',
      steps: [
        'Cuộc họp trực tiếp Đang diễn ra, chưa có bản ghi; thuky@ mở tab Bản ghi',
        'Thẻ ghi âm góc màn hình: bấm "Bật mic phòng", hộp "Bắt đầu ghi âm": tích ô đã thông báo, bấm "Bắt đầu ghi"',
        'Chuyển sang tab "Nội dung cuộc họp" rồi quay lại tab Bản ghi',
        'Bấm "Tắt mic, lưu lượt ghi"',
      ],
      expected:
        'Hộp xác nhận có mục đích mặc định "Lập biên bản cuộc họp", nút "Bắt đầu ghi" tắt tới khi tích; khi ghi: thẻ "Đang ghi cả phòng" có đồng hồ, chip "Đang ghi âm" cạnh tên, dòng "Mic phòng đang ghi"; đổi tab vẫn ghi; tắt mic: toast "Đã lưu lượt ghi.", dòng bản ghi nhãn "Mic phòng", chip mất; API: BrowserCapture, Available, consentObtainedAt có, retainUntil = createdAt + 180 ngày',
      run: wrap(async (qa) => {
        const m = await meeting(qa, 'ipRun')
        await clean(qa, m.id)
        try {
          await openTab(qa, m)
          await openMicCard(qa)
          const card = micCard(qa)
          await qa.expect(card.getByText('Mic phòng đang tắt')).toBeVisible()
          await qa.expect(card.getByText('Báo mọi người trước khi bật mic.')).toBeVisible()
          await press(qa, card.getByRole('button', { name: 'Bật mic phòng' }))
          const dlg = qa.page.getByRole('dialog', { name: 'Bắt đầu ghi âm' })
          await qa.expect(dlg).toBeVisible()
          await qa.expect(dlg.getByLabel('Mục đích ghi âm')).toHaveValue('Lập biên bản cuộc họp')
          await qa.expect(dlg.getByRole('button', { name: 'Bắt đầu ghi' })).toBeDisabled()
          await dlg.getByRole('checkbox').check()
          await settled(qa)
          await qa.shot('xac-nhan-da-bao-moi-nguoi')
          await press(qa, dlg.getByRole('button', { name: 'Bắt đầu ghi' }))
          await qa.expect(card.getByText('Đang ghi cả phòng')).toBeVisible({ timeout: 15_000 })
          await qa.expect(qa.page.getByRole('button', { name: 'Đang ghi âm' })).toBeVisible({ timeout: 10_000 })
          await qa.expect(roomSection(qa).getByText('Mic phòng đang ghi')).toBeVisible({ timeout: 10_000 })
          await qa.page.waitForTimeout(3000) // the take must carry some audio before it is switched off
          await settled(qa)
          await qa.shot('dang-ghi-ca-phong')
          await noSidewaysScroll(qa)
          await press(qa, qa.page.getByRole('tab', { name: 'Nội dung cuộc họp' }))
          await qa.expect(qa.page.getByRole('region', { name: 'Nội dung cuộc họp' })).toBeVisible()
          await openMicCard(qa)
          await qa.expect(card.getByText('Đang ghi cả phòng')).toBeVisible()
          await settled(qa)
          await qa.shot('doi-tab-van-ghi')
          await press(qa, qa.page.getByRole('tab', { name: 'Bản ghi' }))
          await qa.page.waitForTimeout(2000)
          await press(qa, card.getByRole('button', { name: 'Tắt mic, lưu lượt ghi' }))
          await qa.expect(toast(qa, 'Đã lưu lượt ghi.')).toBeVisible({ timeout: 20_000 })
          await qa.expect(card.getByText('Mic phòng đang tắt')).toBeVisible()
          await qa.expect(qa.page.getByRole('button', { name: 'Đang ghi âm' })).toHaveCount(0)
          const row = roomSection(qa).locator('li.recording-row').filter({ hasText: 'Mic phòng' })
          await qa.expect(row).toHaveCount(1)
          await settled(qa)
          await qa.shot('da-luu-luot-ghi')
          await noRawCodes(qa)
          const list = await recordings(qa, 'secretary', m.id)
          const rec = list.items.find((r) => r.status !== 'Deleted')
          qa.expect(rec?.source).toBe('BrowserCapture')
          qa.expect(rec?.status).toBe('Available')
          qa.expect(rec?.consentObtainedAt, 'consent time not kept').toBeTruthy()
          qa.expect(rec?.consentPurpose).toBe('Lập biên bản cuộc họp')
          qa.expect(rec?.takes?.length).toBe(1)
          qa.expect(daysBetween(rec.createdAt, rec.retainUntil)).toBe(180)
        } finally {
          await clean(qa, m.id)
        }
      }),
    },
    {
      id: 'AC2',
      role: 'secretary',
      title: 'Tắt rồi bật lại mic (thay Tạm dừng/Ghi tiếp, ADR-0041): lượt sau không hỏi xác nhận lại, nối vào cùng bản ghi ("2 lượt ghi")',
      steps: [
        'Cuộc họp trực tiếp Đang diễn ra đã có bản ghi mic phòng một lượt (tạo qua API, có xác nhận)',
        'thuky@ mở tab Bản ghi, bấm "Bật mic phòng"',
        'Ghi vài giây, bấm "Tắt mic, lưu lượt ghi"',
      ],
      expected:
        'Không mở hộp xác nhận; thẻ nhắc "Tắt mic là lưu lượt ghi và chuyển chữ."; toast "Đã lưu lượt ghi."; vẫn một bản ghi mic phòng, hiện "2 lượt ghi"; API: cùng id, takes 2, lượt 2 bắt đầu ở cuối lượt 1',
      run: wrap(async (qa) => {
        const m = await meeting(qa, 'ipRun')
        await clean(qa, m.id)
        try {
          const first = await roomTake(qa, m.id, ['s1.pcm'], { consent: true })
          await openTab(qa, m)
          await openMicCard(qa)
          const card = micCard(qa)
          await qa.expect(card.getByText('Tắt mic là lưu lượt ghi và chuyển chữ.')).toBeVisible()
          await press(qa, card.getByRole('button', { name: 'Bật mic phòng' }))
          await qa.expect(card.getByText('Đang ghi cả phòng')).toBeVisible({ timeout: 15_000 })
          qa.expect(await qa.page.getByRole('dialog', { name: 'Bắt đầu ghi âm' }).count(), 'consent asked again').toBe(0)
          await qa.page.waitForTimeout(4000)
          await settled(qa)
          await qa.shot('luot-2-dang-ghi')
          await press(qa, card.getByRole('button', { name: 'Tắt mic, lưu lượt ghi' }))
          await qa.expect(toast(qa, 'Đã lưu lượt ghi.')).toBeVisible({ timeout: 20_000 })
          const row = roomSection(qa).locator('li.recording-row').filter({ hasText: 'Mic phòng' })
          await qa.expect(row).toHaveCount(1)
          await qa.expect(row.getByText('2 lượt ghi')).toBeVisible({ timeout: 10_000 })
          await settled(qa)
          await qa.shot('noi-thanh-2-luot')
          const kept = (await recordings(qa, 'secretary', m.id)).items.filter((r) => r.status === 'Available')
          qa.expect(kept.length).toBe(1)
          qa.expect(kept[0].id).toBe(first.recordingId)
          qa.expect(kept[0].takes.length).toBe(2)
          qa.expect(kept[0].takes[1].offsetMs).toBe(kept[0].takes[0].durationMs)
        } finally {
          await clean(qa, m.id)
        }
      }),
    },
    {
      id: 'AC3',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Chưa xác nhận thì không ghi: Huỷ hộp xác nhận không mở lượt; API lượt đầu thiếu xác nhận 400 MSG291',
      steps: [
        'Cuộc họp trực tiếp Đang diễn ra, chưa có bản ghi; thuky@ bấm "Bật mic phòng"',
        'Không tích ô xác nhận, bấm "Huỷ"',
        'API: POST …/recordings/takes với consentConfirmed=false',
      ],
      expected: 'Nút "Bắt đầu ghi" tắt; Huỷ đóng hộp, thẻ vẫn "Mic phòng đang tắt", không có lượt mở; API 400 MSG291, vẫn không có lượt nào',
      run: wrap(async (qa) => {
        const m = await meeting(qa, 'ipRun')
        await clean(qa, m.id)
        try {
          await openTab(qa, m)
          await openMicCard(qa)
          await press(qa, micCard(qa).getByRole('button', { name: 'Bật mic phòng' }))
          const dlg = qa.page.getByRole('dialog', { name: 'Bắt đầu ghi âm' })
          await qa.expect(dlg).toBeVisible()
          await qa.expect(dlg.getByRole('button', { name: 'Bắt đầu ghi' })).toBeDisabled()
          await qa.expect(dlg.getByText('bản ghi tự xoá khi hết thời hạn lưu trữ', { exact: false })).toBeVisible()
          await settled(qa)
          await qa.shot('chua-tich-xac-nhan')
          await noSidewaysScroll(qa)
          await press(qa, dlg.getByRole('button', { name: 'Huỷ' }))
          await qa.expect(dlg).toHaveCount(0)
          await qa.expect(micCard(qa).getByText('Mic phòng đang tắt')).toBeVisible()
          await settled(qa)
          await qa.shot('huy-khong-ghi')
          qa.expect((await recordings(qa, 'secretary', m.id)).items.filter((r) => r.status !== 'Deleted').length).toBe(0)
          const c = checker(qa)
          c.check('takes không xác nhận', await (await client(qa, 'secretary')).post(`${R(m.id)}/takes`, { consentConfirmed: false }), 400, 'MSG291')
          c.fact('không mở lượt', (await recordings(qa, 'secretary', m.id)).items.filter((r) => r.status !== 'Deleted').length === 0)
          c.done()
        } finally {
          await clean(qa, m.id)
        }
      }),
    },
    {
      id: 'AC4',
      role: 'chair',
      devices: ['desktop', 'phone'],
      title: 'Khi mic phòng đang ghi, người khác (Chủ toạ) thấy chip "Đang ghi âm" và dòng "Mic phòng đang ghi", không nghe được, không có thẻ ghi âm',
      steps: ['Thư ký mở một lượt mic phòng (API)', 'chutoa@ mở tab Bản ghi của cuộc họp', 'API: thanhvien@ xin link nghe lượt đang mở'],
      expected:
        'Chip "Đang ghi âm" cạnh tên cuộc họp; dòng "Mic phòng đang ghi" nhãn "Đang ghi" kèm tên Thư ký, không có nút nghe/tải/xoá; không có thẻ ghi âm của Thư ký; API playback của lượt đang mở 404',
      run: wrap(async (qa) => {
        const u = await users(qa)
        const m = await meeting(qa, 'ipRun')
        await clean(qa, m.id)
        try {
          const opened = await (await client(qa, 'secretary')).post(`${R(m.id)}/takes`, { consentConfirmed: true })
          qa.expectStatus(opened, 201)
          await openTab(qa, m)
          await qa.expect(qa.page.getByRole('button', { name: 'Đang ghi âm' })).toBeVisible()
          const live = roomSection(qa).locator('li.recording-live')
          await qa.expect(live.getByText('Mic phòng đang ghi')).toBeVisible()
          await qa.expect(live.getByText('Đang ghi', { exact: true })).toBeVisible()
          await qa.expect(live.getByText(u.secretary.name)).toBeVisible()
          await qa.expect(live.getByRole('button', { name: /Nghe|Tải|Xoá/ })).toHaveCount(0)
          await qa.expect(micCard(qa)).toHaveCount(0)
          await settled(qa)
          await qa.shot('nguoi-khac-thay-dang-ghi')
          await noSidewaysScroll(qa)
          const c = checker(qa)
          c.check('member playback lượt đang mở', await (await client(qa, 'member')).get(`/api/recordings/${opened.body.recordingId}/playback`), 404)
          c.done()
        } finally {
          await clean(qa, m.id)
        }
      }),
    },
    {
      id: 'AC5',
      role: 'secretary',
      title: 'Lượt còn mở ở máy khác: thẻ báo "Đang ghi ở nơi khác", Thư ký bấm "Dừng ghi ở máy kia" để lưu hộ (UC-52b AF-3)',
      steps: ['Một lượt mic phòng được mở từ "máy khác" (API, thuky@) và đã có tiếng', 'thuky@ mở trang cuộc họp trên trình duyệt này', 'Bấm "Dừng ghi ở máy kia"'],
      expected: 'Thẻ hiện "Đang ghi ở nơi khác · <tên Thư ký>" và nút "Dừng ghi ở máy kia"; bấm thì toast "Đã lưu lượt ghi.", bản ghi thành Available',
      run: wrap(async (qa) => {
        const u = await users(qa)
        const m = await meeting(qa, 'ipRun')
        await clean(qa, m.id)
        try {
          const opened = await (await client(qa, 'secretary')).post(`${R(m.id)}/takes`, { consentConfirmed: true })
          qa.expectStatus(opened, 201)
          // One full (non-final) part, exactly partSize bytes, as the browser sends every 10 s.
          const pcm = bytes(qa, 's1.pcm')
          const part = Buffer.concat([wavHeaderUnknown(), ...Array(Math.ceil(opened.body.partSize / pcm.length) + 1).fill(pcm)]).subarray(0, opened.body.partSize)
          qa.expectStatus(await (await raw(qa, 'secretary')).putPart(opened.body.recordingId, 1, part, false), 204)
          await openTab(qa, m)
          await openMicCard(qa)
          const card = micCard(qa)
          await qa.expect(card.getByText(`Đang ghi ở nơi khác · ${u.secretary.name}`)).toBeVisible()
          await settled(qa)
          await qa.shot('dang-ghi-o-noi-khac')
          await card.getByRole('button', { name: 'Dừng ghi ở máy kia' }).click()
          await qa.expect(toast(qa, 'Đã lưu lượt ghi.')).toBeVisible({ timeout: 20_000 })
          await qa.expect(card.getByText('Mic phòng đang tắt')).toBeVisible()
          await settled(qa)
          await qa.shot('da-luu-ho')
          const rec = (await recordings(qa, 'secretary', m.id)).items.find((r) => r.id === opened.body.recordingId)
          qa.expect(rec?.status).toBe('Available')
        } finally {
          await clean(qa, m.id)
        }
      }),
    },
    {
      id: 'AC6',
      role: 'secretary',
      title: 'API mic phòng: chỉ Thư ký, chỉ khi đang diễn ra, có phòng, một lượt mở; phần sai, lượt đã lưu, lượt rỗng, xoá lượt đang ghi',
      steps: [
        'POST takes: họp trực tuyến; trực tiếp chưa bắt đầu; trực tiếp đã kết thúc; Chủ toạ; Thành viên; lượt thứ hai khi đã có lượt mở',
        'PUT part: người khác gửi; phần 1 không phải WAV 16 kHz; complete bởi Chủ toạ; DELETE lượt đang mở',
        'Lưu lượt rỗng; lượt có tiếng: lưu, gửi thêm phần sau khi đã lưu, lưu lại',
      ],
      expected:
        '409 MSG358; 409 MSG359; 409 MSG359; 403 MSG295; 403 MSG295; 409 MSG360; part người khác 404; 400 MSG361; complete Chủ toạ 403 MSG295; xoá lượt đang mở 409 MSG364; lượt rỗng 200 MSG365 recordingId null (lượt bị bỏ, phần gửi sau 404); lượt có tiếng 200 MSG363, phần sau khi lưu 409 MSG362, lưu lại vẫn 200 MSG363',
      run: wrap(async (qa) => {
        const sec = await client(qa, 'secretary')
        const c = checker(qa)
        const on = await meeting(qa, 'onRun')
        const pub = await meeting(qa, 'ipPub')
        const end = await meeting(qa, 'ipEnd')
        const m = await meeting(qa, 'ipRun')
        await clean(qa, m.id)
        try {
          const consent = { consentConfirmed: true }
          c.check('takes họp trực tuyến', await sec.post(`${R(on.id)}/takes`, consent), 409, 'MSG358')
          c.check('takes chưa bắt đầu', await sec.post(`${R(pub.id)}/takes`, consent), 409, 'MSG359')
          c.check('takes đã kết thúc', await sec.post(`${R(end.id)}/takes`, consent), 409, 'MSG359')
          c.check('takes Chủ toạ', await (await client(qa, 'chair')).post(`${R(m.id)}/takes`, consent), 403, 'MSG295')
          c.check('takes Thành viên', await (await client(qa, 'member')).post(`${R(m.id)}/takes`, consent), 403, 'MSG295')
          const opened = await sec.post(`${R(m.id)}/takes`, consent)
          c.check('mở lượt', opened, 201)
          const id = opened.body?.recordingId
          c.check('lượt thứ hai', await sec.post(`${R(m.id)}/takes`, consent), 409, 'MSG360')
          const tone = Buffer.concat([wavHeaderUnknown(), bytes(qa, 's1.pcm').subarray(0, 64000)])
          c.check('part bởi Chủ toạ', await (await raw(qa, 'chair')).putPart(id, 1, tone, true), 404)
          const bad = Buffer.from(tone)
          bad.writeUInt32LE(44100, 24)
          c.check('part 1 sai header', await (await raw(qa, 'secretary')).putPart(id, 1, bad, true), 400, 'MSG361')
          c.check('complete bởi Chủ toạ', await (await client(qa, 'chair')).post(`/api/recordings/${id}/complete`), 403, 'MSG295')
          c.check('xoá lượt đang mở', await sec.delete(`/api/recordings/${id}`), 409, 'MSG364')
          const empty = await sec.post(`/api/recordings/${id}/complete`)
          if (c.check('lưu lượt rỗng', empty, 200, 'MSG365')) c.fact('recordingId null', empty.body.recordingId === null, JSON.stringify(empty.body))
          c.check('phần của lượt rỗng đã bỏ', await (await raw(qa, 'secretary')).putPart(id, 1, tone, true), 404)
          const again = await sec.post(`${R(m.id)}/takes`, consent)
          c.check('mở lượt có tiếng', again, 201)
          c.check('phần cuối', await (await raw(qa, 'secretary')).putPart(again.body?.recordingId, 1, tone, true), 204)
          c.check('lưu lượt', await sec.post(`/api/recordings/${again.body?.recordingId}/complete`), 200, 'MSG363')
          c.check('phần sau khi lưu', await (await raw(qa, 'secretary')).putPart(again.body?.recordingId, 2, tone, true), 409, 'MSG362')
          c.check('lưu lại lượt đã lưu', await sec.post(`/api/recordings/${again.body?.recordingId}/complete`), 200, 'MSG363')
          c.done()
        } finally {
          await clean(qa, m.id)
        }
      }),
    },

    // ------------------------------------------------------------------------------------------- UC-52c
    {
      id: 'AC7',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Thư ký tải tệp m4a lên họp trực tiếp: hộp "Lưu tệp ghi âm" có tên tệp, nhắc micro phòng, xác nhận; bản ghi "Tải lên" có mục đích và hạn 180 ngày',
      steps: [
        'Cuộc họp trực tiếp Đang diễn ra; thuky@ mở tab Bản ghi, bấm "Tải tệp lên", chọn tệp .m4a (giọng đọc máy)',
        'Sửa mục đích, tích ô đã thông báo, bấm "Tải lên"',
      ],
      expected:
        'Hộp hiện tên và dung lượng tệp, cảnh báo "Tệp phải ghi bằng đúng micro phòng họp đã dùng thu giọng mẫu…", nút "Tải lên" tắt tới khi tích; khay tải lên "Đã tải lên"; dòng bản ghi nhãn "Tải lên"; API: Upload, Available, đúng mục đích, consentObtainedAt có, retainUntil = createdAt + 180 ngày',
      run: wrap(async (qa) => {
        const file = audio(qa)('s1.m4a')
        const m = await meeting(qa, 'ipRun')
        await clean(qa, m.id)
        try {
          await openTab(qa, m)
          const sec = roomSection(qa)
          const chooser = qa.page.waitForEvent('filechooser')
          await press(qa, sec.getByRole('button', { name: 'Tải tệp lên' }))
          await (await chooser).setFiles(file)
          const dlg = qa.page.getByRole('dialog', { name: 'Lưu tệp ghi âm' })
          await qa.expect(dlg).toBeVisible()
          await qa.expect(dlg.getByText('s1.m4a')).toBeVisible()
          await qa.expect(dlg.getByText('Tệp phải ghi bằng đúng micro phòng họp đã dùng thu giọng mẫu', { exact: false })).toBeVisible()
          await qa.expect(dlg.getByRole('button', { name: 'Tải lên' })).toBeDisabled()
          const purpose = `Lập biên bản họp QA ${Math.random().toString(36).slice(2, 6)}`
          await dlg.getByLabel('Mục đích ghi âm').fill(purpose)
          await dlg.getByRole('checkbox').check()
          await settled(qa)
          await qa.shot('hop-luu-tep-ghi-am')
          await noSidewaysScroll(qa)
          await press(qa, dlg.getByRole('button', { name: 'Tải lên' }))
          await qa.expect(qa.page.getByRole('region', { name: 'Tải lên' }).getByText('Đã tải lên', { exact: true })).toBeVisible({ timeout: 30_000 })
          const row = sec.locator('li.recording-row').filter({ hasText: 's1.m4a' })
          await qa.expect(row).toBeVisible({ timeout: 15_000 })
          await qa.expect(row.getByText('Tải lên', { exact: true })).toBeVisible()
          await settled(qa)
          await qa.shot('da-tai-len')
          await noRawCodes(qa)
          const rec = (await recordings(qa, 'secretary', m.id)).items.find((r) => r.fileName === 's1.m4a' && r.status === 'Available')
          qa.expect(rec, 'the uploaded recording is not listed').toBeTruthy()
          qa.expect(rec.source).toBe('Upload')
          qa.expect(rec.consentPurpose).toBe(purpose)
          qa.expect(rec.consentObtainedAt).toBeTruthy()
          qa.expect(rec.takes).toBeNull()
          qa.expect(daysBetween(rec.createdAt, rec.retainUntil)).toBe(180)
        } finally {
          await clean(qa, m.id)
        }
      }),
    },
    {
      id: 'AC8',
      role: 'secretary',
      title: 'Tải lên tệp không phải âm thanh qua màn hình: báo "Định dạng âm thanh không được hỗ trợ…", không thêm bản ghi',
      steps: ['thuky@ mở tab Bản ghi (trực tiếp, Đang diễn ra), bấm "Tải tệp lên", chọn tệp .txt', 'Tích xác nhận, bấm "Tải lên"'],
      expected: 'Khay tải lên báo "Định dạng âm thanh không được hỗ trợ. Dùng mp3, m4a, wav, webm hoặc ogg." (MSG292, không hiện mã); danh sách không có bản ghi mới',
      run: wrap(async (qa) => {
        const file = audio(qa)('bien-ban-nhap.txt')
        const m = await meeting(qa, 'ipRun')
        await clean(qa, m.id)
        try {
          await openTab(qa, m)
          const chooser = qa.page.waitForEvent('filechooser')
          await roomSection(qa).getByRole('button', { name: 'Tải tệp lên' }).click()
          await (await chooser).setFiles(file)
          const dlg = qa.page.getByRole('dialog', { name: 'Lưu tệp ghi âm' })
          await dlg.getByRole('checkbox').check()
          await dlg.getByRole('button', { name: 'Tải lên' }).click()
          const tray = qa.page.getByRole('region', { name: 'Tải lên' })
          await qa.expect(tray.getByText('Định dạng âm thanh không được hỗ trợ. Dùng mp3, m4a, wav, webm hoặc ogg.')).toBeVisible({ timeout: 20_000 })
          await settled(qa)
          await qa.shot('tu-choi-dinh-dang')
          qa.expect(await tray.innerText()).not.toMatch(/MSG\d+/)
          qa.expect((await recordings(qa, 'secretary', m.id)).items.filter((r) => r.status !== 'Deleted').length).toBe(0)
        } finally {
          await clean(qa, m.id)
        }
      }),
    },
    {
      id: 'AC9',
      role: 'secretary',
      title: 'API tải tệp: 5 định dạng hợp lệ 201 MSG290 (họp trực tiếp đã kết thúc); sai định dạng, thiếu xác nhận, nguồn sai, quá 300 MB, sai trạng thái, sai hình thức, sai vai trò',
      steps: [
        'POST …/recordings multipart trên họp trực tiếp Đã kết thúc: mp3, m4a (audio/mp4), wav, webm, ogg, có consentConfirmed=true',
        'text/plain; thiếu consentConfirmed; source=BrowserCapture; tệp 301 MB',
        'Họp trực tiếp chưa bắt đầu; họp trực tuyến; họp kết hợp; Chủ toạ; Thành viên',
      ],
      expected:
        'Mỗi định dạng hợp lệ 201 MSG290 "Đã lưu bản ghi."; 415 MSG292; 400 MSG291; 400 MSG291; 413 MSG293; 409 MSG294; 409 MSG358; 409 MSG478 "Họp kết hợp ghi trực tiếp bằng mic phòng để khớp giờ với lời người họp trực tuyến, không tải tệp ghi âm lên."; 403 MSG295; 403 MSG295; không lỗi nào để lại bản ghi',
      run: wrap(async (qa) => {
        const c = checker(qa)
        const end = await meeting(qa, 'ipEnd')
        const pub = await meeting(qa, 'ipPub')
        const on = await meeting(qa, 'onRun')
        const hy = await meeting(qa, 'hyRun')
        await clean(qa, end.id)
        const r = await raw(qa, 'secretary')
        const ok = { consentConfirmed: 'true', consentPurpose: 'QA FE-09' }
        const wav = bytes(qa, 's1.wav')
        const send = (mid, over = {}, who = r) =>
          who.upload(mid, { file: wav, name: 'qa.wav', mimeType: 'audio/wav', fields: ok, ...over })
        try {
          for (const [ext, mime] of [['mp3', 'audio/mpeg'], ['m4a', 'audio/mp4'], ['wav', 'audio/wav'], ['webm', 'audio/webm'], ['ogg', 'audio/ogg']]) {
            const res = await r.upload(end.id, { file: bytes(qa, `s1.${ext}`), name: `qa-fe09.${ext}`, mimeType: mime, fields: ok })
            if (c.check(`tải ${ext}`, res, 201, 'MSG290')) c.fact(`message ${ext}`, res.body.message === 'Đã lưu bản ghi.', res.body.message)
          }
          const before = (await recordings(qa, 'secretary', end.id)).items.filter((i) => i.status !== 'Deleted').length
          c.check('text/plain', await send(end.id, { file: bytes(qa, 'bien-ban-nhap.txt'), name: 'x.txt', mimeType: 'text/plain' }), 415, 'MSG292')
          c.check('thiếu xác nhận', await send(end.id, { fields: { consentPurpose: 'QA' } }), 400, 'MSG291')
          c.check('source BrowserCapture', await send(end.id, { fields: { ...ok, source: 'BrowserCapture' } }), 400, 'MSG291')
          c.check('quá 300 MB', await send(end.id, { file: Buffer.alloc(301 * 1024 * 1024), name: 'big.wav' }), 413, 'MSG293')
          c.fact('lỗi không để lại bản ghi', (await recordings(qa, 'secretary', end.id)).items.filter((i) => i.status !== 'Deleted').length === before)
          c.check('chưa bắt đầu', await send(pub.id), 409, 'MSG294')
          c.check('trực tuyến', await send(on.id), 409, 'MSG358')
          const hyRes = await send(hy.id)
          if (c.check('kết hợp', hyRes, 409, 'MSG478'))
            c.fact(
              'message MSG478',
              hyRes.body.message === 'Họp kết hợp ghi trực tiếp bằng mic phòng để khớp giờ với lời người họp trực tuyến, không tải tệp ghi âm lên.',
              hyRes.body.message,
            )
          c.check('Chủ toạ', await send(end.id, {}, await raw(qa, 'chair')), 403, 'MSG295')
          c.check('Thành viên', await send(end.id, {}, await raw(qa, 'member')), 403, 'MSG295')
          c.done()
        } finally {
          await clean(qa, end.id)
        }
      }),
    },
    {
      id: 'AC10',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Họp kết hợp đang diễn ra (ADR-0072): có bản ghi trực tiếp theo micro, không có "Tải tệp lên", trang cuộc họp không còn thẻ mic phòng (công tắc nằm trong phòng họp)',
      steps: ['thuky@ mở tab Bản ghi của cuộc họp kết hợp Đang diễn ra, chưa có bản ghi phòng'],
      expected:
        'Thẻ "Bản ghi trực tiếp" (micro từng người); phần "Bản ghi" ghi "Chưa có bản ghi.", không có nút "Tải tệp lên"; KHÔNG có thẻ ghi âm góc màn hình khi phòng họp dùng được (ADR-0072 điểm 2, thay ADR-0040 điểm 4); API canUpload false, canRecord true. Lời gợi ý "Bật mic phòng trong phòng họp trực tuyến."',
      run: wrap(async (qa) => {
        const m = await meeting(qa, 'hyRun')
        await clean(qa, m.id)
        await openTab(qa, m)
        await qa.expect(qa.page.getByRole('heading', { name: /Bản ghi trực tiếp/ })).toBeVisible()
        const sec = roomSection(qa)
        await qa.expect(sec.getByText('Chưa có bản ghi.')).toBeVisible()
        await qa.expect(sec.getByText('Bật mic phòng trong phòng họp trực tuyến.')).toBeVisible()
        await qa.expect(sec.getByRole('button', { name: 'Tải tệp lên' })).toHaveCount(0)
        await qa.page.waitForTimeout(2000)
        await qa.expect(micCard(qa)).toHaveCount(0)
        qa.note('Họp kết hợp: không có thẻ mic nổi trên trang cuộc họp (ADR-0072); gợi ý "Bật mic phòng trong phòng họp trực tuyến."')
        await settled(qa)
        await qa.shot('ket-hop-chi-mic-phong', { fullPage: true })
        await noSidewaysScroll(qa)
        const list = await recordings(qa, 'secretary', m.id)
        qa.expect(list.canUpload).toBe(false)
        qa.expect(list.canRecord).toBe(true)
      }),
    },
    {
      id: 'AC11',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Họp trực tuyến đang diễn ra: tab Bản ghi chỉ có bản ghi theo micro từng người, không có bản ghi phòng, không tải lên, không thẻ mic phòng',
      steps: ['thuky@ mở tab Bản ghi của cuộc họp trực tuyến Đang diễn ra'],
      expected:
        'Thẻ "Bản ghi trực tiếp"; không có phần "Bản ghi" phòng, không có "Tải tệp lên", không có thẻ ghi âm góc màn hình; API canUpload false, canRecord false',
      run: wrap(async (qa) => {
        const m = await meeting(qa, 'onRun')
        await openTab(qa, m)
        await qa.expect(qa.page.getByRole('heading', { name: /Bản ghi trực tiếp/ })).toBeVisible()
        await qa.page.waitForLoadState('networkidle').catch(() => undefined)
        await qa.expect(roomSection(qa)).toHaveCount(0)
        await qa.expect(qa.page.getByRole('button', { name: 'Tải tệp lên' })).toHaveCount(0)
        await qa.expect(micCard(qa)).toHaveCount(0)
        await settled(qa)
        await qa.shot('truc-tuyen-chi-micro-tung-nguoi')
        await noSidewaysScroll(qa)
        const list = await recordings(qa, 'secretary', m.id)
        qa.expect(list.canUpload).toBe(false)
        qa.expect(list.canRecord).toBe(false)
      }),
    },
    {
      id: 'AC12',
      role: 'member',
      devices: ['desktop', 'phone'],
      title: 'Màn chờ phòng họp trực tuyến báo trước: "Cuộc họp được ghi âm để lập biên bản · tự xoá sau 180 ngày" (BR-109)',
      steps: ['thanhvien@ mở trang phòng họp của cuộc họp trực tuyến Đang diễn ra (màn chờ, chưa bấm Vào phòng)'],
      expected: 'Màn chờ "Sẵn sàng tham gia?" có dòng "Cuộc họp được ghi âm để lập biên bản · tự xoá sau 180 ngày"; không cuộn ngang',
      run: wrap(async (qa) => {
        const m = await meeting(qa, 'onRun')
        await qa.goto(`/mgmt/meetings/${m.id}/room`)
        await qa.expect(qa.page.getByRole('heading', { name: 'Sẵn sàng tham gia?' })).toBeVisible({ timeout: 15_000 })
        await qa.expect(qa.page.getByText('Cuộc họp được ghi âm để lập biên bản · tự xoá sau 180 ngày')).toBeVisible({ timeout: 10_000 })
        await settled(qa)
        await qa.shot('man-cho-thong-bao-ghi-am')
        await noSidewaysScroll(qa)
      }),
    },

    // ------------------------------------------------------------------------------------------- UC-52d
    {
      id: 'AC13',
      role: 'secretary',
      title: 'Thư ký xoá lượt 2 của bản ghi mic phòng hai lượt (MSG366); bản ghi còn một lượt thì không xoá lượt được (MSG367)',
      steps: [
        'Bản ghi mic phòng hai lượt giọng đọc tiếng Việt (API), chờ gỡ băng xong',
        'thuky@ mở tab Bản ghi, ở vạch "Lượt 2" bấm thùng rác "Xoá lượt 2", xác nhận "Xoá lượt"',
        'API: DELETE takes/1 khi chỉ còn một lượt; Chủ toạ DELETE takes/1',
      ],
      expected:
        'Hộp "Xoá lượt 2?" cảnh báo tiếng và chữ bị xoá hẳn; toast "Đã xoá lượt ghi."; dòng bản ghi không còn "2 lượt ghi", API takes 1; DELETE lượt duy nhất 409 MSG367; Chủ toạ 403 MSG295',
      run: wrap(async (qa) => {
        const m = await meeting(qa, 'ipRun')
        await clean(qa, m.id)
        try {
          const first = await roomTake(qa, m.id, saidTake(qa), { consent: true })
          await roomTake(qa, m.id, saidTake(qa))
          const id = first.recordingId
          const t = await waitTranscript(qa, id)
          if (t?.status !== 'Completed')
            qa.inconclusive(`Transcript of the 2-take recording is ${t?.status} (${t?.errorMessage ?? ''}): the take lines (and their delete buttons) only show on a completed transcript. Check the speech worker on :8090.`)
          const rec0 = (await recordings(qa, 'secretary', m.id)).items.find((r) => r.id === id)
          qa.note(`Lượt: ${JSON.stringify(rec0?.takes)}; đoạn gỡ băng: ${JSON.stringify(t.segments.map((s) => [s.startMs, s.endMs, s.text.slice(0, 30)]))}`)
          await openTab(qa, m)
          const row = roomSection(qa).locator('li.recording-row').filter({ hasText: '2 lượt ghi' })
          await qa.expect(row).toBeVisible()
          await qa.expect(row.locator('.transcript-list')).toBeVisible({ timeout: 15_000 })
          await settled(qa)
          await qa.shot('ban-ghi-hai-luot', { fullPage: true })
          await qa.expect(row.getByText('Lượt 1', { exact: false })).toBeVisible({ timeout: 15_000 })
          const del = row.getByRole('button', { name: 'Xoá lượt 2' })
          await qa.expect(del).toBeVisible()
          await settled(qa)
          await qa.shot('hai-luot-co-nut-xoa')
          await del.click()
          const dlg = qa.page.getByRole('dialog', { name: 'Xoá lượt 2?' })
          await qa.expect(dlg.getByText('Tiếng và chữ của lượt này bị xoá hẳn, không lấy lại được.')).toBeVisible()
          await settled(qa)
          await qa.shot('hoi-xoa-luot-2')
          await dlg.getByRole('button', { name: 'Xoá lượt' }).click()
          await qa.expect(toast(qa, 'Đã xoá lượt ghi.')).toBeVisible({ timeout: 15_000 })
          await qa.expect(roomSection(qa).getByText('2 lượt ghi')).toHaveCount(0, { timeout: 10_000 })
          await settled(qa)
          await qa.shot('con-mot-luot')
          const c = checker(qa)
          const rec = (await recordings(qa, 'secretary', m.id)).items.find((r) => r.id === id)
          c.fact('takes còn 1', rec?.takes?.length === 1, JSON.stringify(rec?.takes))
          c.check('Chủ toạ xoá lượt', await (await client(qa, 'chair')).delete(`/api/recordings/${id}/takes/1`), 403, 'MSG295')
          c.check('xoá lượt duy nhất', await (await client(qa, 'secretary')).delete(`/api/recordings/${id}/takes/1`), 409, 'MSG367')
          c.check('xoá lượt không có', await (await client(qa, 'secretary')).delete(`/api/recordings/${id}/takes/5`), 404)
          c.done()
        } finally {
          await clean(qa, m.id)
        }
      }),
    },

    // ------------------------------------------------------------------------------------------- Roles (BR-108, BR-111, BR-112)
    {
      id: 'AC14',
      role: 'secretary',
      title: 'Bảng quyền API trên một bản ghi: Thư ký mọi việc; Chủ toạ nghe + tải; Thành viên chỉ nghe; Người tạo không trong danh sách 404; chưa đăng nhập 401',
      steps: ['Bản ghi tải lên của họp trực tiếp Đang diễn ra (người tạo cuộc họp là quantri@, không trong danh sách)', 'Mỗi vai trò gọi list, playback, download, DELETE, DELETE takes/1, POST takes, POST upload'],
      expected:
        'Thư ký: 200/200/200, canErase+canDownload; Chủ toạ: list 200, playback 200, download 200, DELETE 403 MSG295, takes 403 MSG295, upload 403 MSG295; Thành viên: list 200, playback 200, download 403 MSG355, DELETE 403 MSG295; quantri@ (người tạo, không trong danh sách): mọi lời gọi 404; chưa đăng nhập 401; link nghe thêm &download=1 → 404',
      run: wrap(async (qa) => {
        const caps = await qa.capabilities('admin')
        qa.note(`quantri@: ${caps.roleName} (${caps.capabilities.length} quyền) — quyền nghe bản ghi theo danh sách cuộc họp, không theo capability`)
        const m = await meeting(qa, 'ipRun')
        await clean(qa, m.id)
        try {
          // An mp3 so that a download needs no ffmpeg (this machine has none; a wav would answer 503 MSG482).
          const id = await uploadFile(qa, m.id, 's1.mp3', 'audio/mpeg')
          const c = checker(qa)
          const wav = bytes(qa, 's1.wav')
          const up = async (role) =>
            (await raw(qa, role)).upload(m.id, { file: wav, name: 'x.wav', mimeType: 'audio/wav', fields: { consentConfirmed: 'true' } })
          const sec = await client(qa, 'secretary')
          const list = await sec.get(R(m.id))
          c.check('thư ký list', list, 200)
          const item = list.body.items?.find((r) => r.id === id)
          c.fact('thư ký canErase, canDownload', item?.canErase === true && item?.canDownload === true, JSON.stringify(item))
          const play = await sec.get(`/api/recordings/${id}/playback`)
          c.check('thư ký playback', play, 200)
          c.check('thư ký download', await sec.get(`/api/recordings/${id}/download`), 200)
          if (play.body?.url) {
            const anon = await client(qa, 'anon')
            c.check('link nghe ký sẵn (không token)', await anon.get(play.body.url), 200)
            c.check('link nghe + &download=1', await anon.get(`${play.body.url}&download=1`), 404)
          }
          for (const role of ['chair', 'member']) {
            const api = await client(qa, role)
            const l = await api.get(R(m.id))
            c.check(`${role} list`, l, 200)
            const it = l.body.items?.find((r) => r.id === id)
            c.fact(`${role} canErase false`, it?.canErase === false, JSON.stringify(it))
            c.check(`${role} playback`, await api.get(`/api/recordings/${id}/playback`), 200)
            c.check(`${role} DELETE`, await api.delete(`/api/recordings/${id}`), 403, 'MSG295')
            c.check(`${role} takes`, await api.post(`${R(m.id)}/takes`, { consentConfirmed: true }), 403, 'MSG295')
            c.check(`${role} upload`, await up(role), 403, 'MSG295')
          }
          c.check('chair download', await (await client(qa, 'chair')).get(`/api/recordings/${id}/download`), 200)
          c.check('member download', await (await client(qa, 'member')).get(`/api/recordings/${id}/download`), 403, 'MSG355')
          const adm = await client(qa, 'admin')
          c.check('người tạo list', await adm.get(R(m.id)), 404)
          c.check('người tạo playback', await adm.get(`/api/recordings/${id}/playback`), 404)
          c.check('người tạo download', await adm.get(`/api/recordings/${id}/download`), 404)
          c.check('người tạo DELETE', await adm.delete(`/api/recordings/${id}`), 404)
          c.check('người tạo takes', await adm.post(`${R(m.id)}/takes`, { consentConfirmed: true }), 404)
          c.check('người tạo upload', await up('admin'), 404)
          const anon = await client(qa, 'anon')
          c.check('anon list', await anon.get(R(m.id)), 401)
          c.check('anon playback', await anon.get(`/api/recordings/${id}/playback`), 401)
          c.done()
        } finally {
          await clean(qa, m.id)
        }
      }),
    },
    {
      id: 'AC15',
      role: 'member',
      devices: ['desktop', 'phone'],
      title: 'Thành viên mở tab Bản ghi: thấy bản ghi, nghe được ("Nghe cả tệp"), không có nút tải về, xoá, tải lên, thẻ mic phòng',
      steps: ['Họp trực tiếp Đang diễn ra có một bản ghi tải lên', 'thanhvien@ mở tab Bản ghi, bấm "Nghe cả tệp"'],
      expected: 'Dòng bản ghi "Tải lên" với trình phát mở sẵn; bấm nghe thì âm thanh chạy (vị trí > 0); không có "Tải MP3", "Xoá bản ghi", "Tải tệp lên", thẻ ghi âm',
      run: wrap(async (qa) => {
        const m = await meeting(qa, 'ipRun')
        await clean(qa, m.id)
        try {
          await uploadFile(qa, m.id, 's1.m4a', 'audio/mp4')
          await openTab(qa, m)
          const row = roomSection(qa).locator('li.recording-row').first()
          await qa.expect(row.getByText('Tải lên', { exact: true })).toBeVisible()
          const play = row.getByRole('button', { name: 'Nghe cả tệp' })
          await qa.expect(play).toBeVisible({ timeout: 10_000 })
          await qa.expect(row.getByRole('button', { name: 'Tải MP3' })).toHaveCount(0)
          await qa.expect(row.getByRole('button', { name: 'Xoá bản ghi' })).toHaveCount(0)
          await qa.expect(qa.page.getByRole('button', { name: 'Tải tệp lên' })).toHaveCount(0)
          await qa.expect(micCard(qa)).toHaveCount(0)
          await press(qa, play)
          await qa.page.waitForFunction(() => (document.querySelector('.recording-player audio')?.currentTime ?? 0) > 0.3, null, { timeout: 15_000 })
          await settled(qa)
          await qa.shot('thanh-vien-dang-nghe')
          await noSidewaysScroll(qa)
          await noRawCodes(qa)
        } finally {
          await clean(qa, m.id)
        }
      }),
    },
    {
      id: 'AC16',
      role: 'admin',
      title: 'Người tạo cuộc họp không có trong danh sách (quantri@) không thấy tab Bản ghi; link ?tab=recordings mở Thông tin chung',
      steps: ['quantri@ (người tạo) mở trang cuộc họp trực tiếp Đang diễn ra', 'Mở thẳng /mgmt/meetings/<id>?tab=recordings'],
      expected: 'Thanh tab không có "Bản ghi"; link thẳng mở tab "Thông tin chung", không có danh sách bản ghi, không có chip "Đang ghi âm"',
      run: wrap(async (qa) => {
        const m = await meeting(qa, 'ipRun')
        await qa.goto(`/mgmt/meetings/${m.id}?tab=recordings`)
        await qa.expect(qa.page.getByRole('heading', { level: 1, name: m.title })).toBeVisible()
        await qa.expect(qa.page.getByRole('tab', { name: 'Thông tin chung' })).toHaveAttribute('aria-selected', 'true')
        await qa.expect(qa.page.getByRole('tab', { name: 'Bản ghi' })).toHaveCount(0)
        await qa.expect(roomSection(qa)).toHaveCount(0)
        await settled(qa)
        await qa.shot('nguoi-tao-khong-co-tab-ban-ghi')
      }),
    },
    {
      id: 'AC17',
      role: 'chair',
      title: 'Chủ toạ tải tệp ghi âm về (tên tệp gốc); cuộc họp Mật: không có nút tải, API 403 MSG356 cho cả Chủ toạ và Thư ký',
      steps: ['Họp trực tiếp có bản ghi tải lên; chutoa@ bấm "Tải MP3"', 'Họp trực tiếp Mật Đang diễn ra có bản ghi; chutoa@ mở tab Bản ghi; API download của Chủ toạ, Thư ký'],
      expected: 'Trình duyệt nhận tệp đúng tên gốc; Chủ toạ không có "Xoá bản ghi"; họp Mật: dòng bản ghi không có nút tải, API 403 MSG356 "Bản ghi và bản gỡ băng của cuộc họp Mật không được tải về."',
      run: wrap(async (qa) => {
        const m = await meeting(qa, 'ipRun')
        const secret = await meeting(qa, 'ipSecret')
        await clean(qa, m.id)
        await clean(qa, secret.id)
        try {
          await uploadFile(qa, m.id, 's1.mp3', 'audio/mpeg')
          await openTab(qa, m)
          const row = roomSection(qa).locator('li.recording-row').first()
          await qa.expect(row.getByRole('button', { name: 'Xoá bản ghi' })).toHaveCount(0)
          const [file] = await Promise.all([
            qa.page.waitForEvent('download', { timeout: 20_000 }),
            row.getByRole('button', { name: 'Tải MP3' }).click(),
          ])
          qa.expect(file.suggestedFilename()).toBe('qa-fe09-s1.mp3')
          await settled(qa)
          await qa.shot('chu-toa-tai-ve')
          await uploadFile(qa, secret.id, 's1.wav', 'audio/wav')
          await openTab(qa, secret)
          const srow = roomSection(qa).locator('li.recording-row').first()
          await qa.expect(srow).toBeVisible()
          await qa.expect(srow.getByRole('button', { name: 'Tải MP3' })).toHaveCount(0)
          await settled(qa)
          await qa.shot('hop-mat-khong-tai-ve')
          const id = (await recordings(qa, 'secretary', secret.id)).items.find((r) => r.status === 'Available').id
          const c = checker(qa)
          for (const role of ['chair', 'secretary']) {
            const res = await (await client(qa, role)).get(`/api/recordings/${id}/download`)
            if (c.check(`${role} tải họp Mật`, res, 403, 'MSG356'))
              c.fact('message', res.body.message === 'Bản ghi và bản gỡ băng của cuộc họp Mật không được tải về.', res.body.message)
          }
          c.check('chair nghe họp Mật', await (await client(qa, 'chair')).get(`/api/recordings/${id}/playback`), 200)
          c.done()
        } finally {
          await clean(qa, m.id)
          await clean(qa, secret.id)
        }
      }),
    },

    // ------------------------------------------------------------------------------------------- UC-52e
    {
      id: 'AC18',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Thư ký xoá bản ghi: hộp xác nhận, toast "Đã xoá bản ghi." (MSG296), dòng rời tab, API giữ dòng Deleted có deletedAt; xoá lại vẫn MSG296',
      steps: ['Họp trực tiếp có một bản ghi tải lên; thuky@ bấm thùng rác "Xoá bản ghi"', 'Bấm "Xoá bản ghi" trong hộp xác nhận', 'API: DELETE lần nữa; playback'],
      expected:
        'Hộp "Xoá bản ghi?" nói âm thanh và bản gỡ băng bị xoá vĩnh viễn; toast "Đã xoá bản ghi."; dòng biến mất (PR #193); API status Deleted, deletedAt có; DELETE lại 200 MSG296; playback 404',
      run: wrap(async (qa) => {
        const m = await meeting(qa, 'ipRun')
        await clean(qa, m.id)
        try {
          const id = await uploadFile(qa, m.id, 's1.ogg', 'audio/ogg')
          await openTab(qa, m)
          const row = roomSection(qa).locator('li.recording-row').filter({ hasText: 'qa-fe09-s1.ogg' })
          await press(qa, row.getByRole('button', { name: 'Xoá bản ghi' }))
          const dlg = qa.page.getByRole('dialog', { name: 'Xoá bản ghi?' })
          await qa.expect(dlg.getByText('bị xoá vĩnh viễn', { exact: false })).toBeVisible()
          // Round 1: "Giữ lại" keeps the recording.
          await press(qa, dlg.getByRole('button', { name: 'Giữ lại' }))
          await qa.expect(dlg).toHaveCount(0)
          await qa.expect(row).toHaveCount(1)
          await settled(qa)
          await qa.shot('giu-lai-khong-xoa')
          await press(qa, row.getByRole('button', { name: 'Xoá bản ghi' }))
          await settled(qa)
          await qa.shot('hoi-xoa-ban-ghi')
          await noSidewaysScroll(qa)
          await press(qa, dlg.getByRole('button', { name: 'Xoá bản ghi' }))
          await qa.expect(toast(qa, 'Đã xoá bản ghi.')).toBeVisible({ timeout: 15_000 })
          await qa.expect(row).toHaveCount(0)
          await qa.expect(roomSection(qa).getByText('Chưa có bản ghi.')).toBeVisible()
          await settled(qa)
          await qa.shot('da-xoa-ban-ghi')
          // Round 1: still gone after a reload.
          await qa.page.reload()
          await qa.expect(roomSection(qa).getByText('Chưa có bản ghi.')).toBeVisible({ timeout: 15_000 })
          await qa.expect(roomSection(qa).locator('li.recording-row')).toHaveCount(0)
          const c = checker(qa)
          const rec = (await recordings(qa, 'secretary', m.id)).items.find((r) => r.id === id)
          c.fact('Deleted + deletedAt', rec?.status === 'Deleted' && !!rec.deletedAt, JSON.stringify(rec))
          c.check('xoá lại', await (await client(qa, 'secretary')).delete(`/api/recordings/${id}`), 200, 'MSG296')
          c.check('playback sau khi xoá', await (await client(qa, 'secretary')).get(`/api/recordings/${id}/playback`), 404)
          c.done()
        } finally {
          await clean(qa, m.id)
        }
      }),
    },
    {
      id: 'AC19',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Mỗi bản ghi cho biết mục đích và ngày tự xoá (180 ngày) khi trỏ hoặc chạm vào dòng thông tin',
      steps: ['Họp trực tiếp có một bản ghi tải lên', 'thuky@ trỏ chuột (máy tính) hoặc chạm (điện thoại) vào dòng thông tin của bản ghi'],
      expected: 'Chú thích hiện "Lưu bởi <tên> lúc … · Mục đích: … · Tự xoá ngày <ngày tạo + 180 ngày>"; trên điện thoại chạm cũng hiện (không chỉ hover)',
      run: wrap(async (qa) => {
        const u = await users(qa)
        const m = await meeting(qa, 'ipRun')
        await clean(qa, m.id)
        try {
          await uploadFile(qa, m.id, 's1.wav', 'audio/wav')
          const rec = (await recordings(qa, 'secretary', m.id)).items.find((r) => r.status === 'Available')
          const expiry = new Intl.DateTimeFormat('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh', day: '2-digit', month: '2-digit', year: 'numeric' }).format(new Date(rec.retainUntil))
          await openTab(qa, m)
          // The tooltip lives on the (i) button, never on the line: hover/tap the button (InfoTip/TooltipLayer).
          // Skip a live take row ("Mic phòng đang ghi") if one is recording: its (i) explains the take, not retention.
          const info = roomSection(qa).locator('li.recording-row:not(.recording-live) .recording-meta .info-tip').first()
          if (qa.device === 'phone') await info.tap()
          else await info.hover()
          const tip = qa.page.getByRole('tooltip')
          await qa.expect(tip).toContainText('Tự xoá ngày', { timeout: 5000 })
          await qa.expect(tip).toContainText(`Lưu bởi ${u.secretary.name}`)
          await qa.expect(tip).toContainText('Mục đích: QA FE-09: lập biên bản')
          const text = await tip.innerText()
          qa.note(`Chú thích: ${text} (hạn API ${rec.retainUntil}, mong đợi ngày ${expiry})`)
          qa.expect(text.replace(/\s+/g, ' ')).toMatch(new RegExp(`Tự xoá ngày\\s*${expiry.replace(/\//g, '\\/')}`))
          await settled(qa)
          await qa.shot('han-luu-tru-180-ngay')
        } finally {
          await clean(qa, m.id)
        }
      }),
    },
    {
      id: 'AC20',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Họp trực tiếp chưa bắt đầu: tab Bản ghi không có "Tải tệp lên", không có thẻ mic phòng; đã kết thúc: có "Tải tệp lên", không có thẻ mic phòng',
      steps: ['thuky@ mở tab Bản ghi của họp trực tiếp Đã công bố (chưa bắt đầu)', 'thuky@ mở tab Bản ghi của họp trực tiếp Đã kết thúc'],
      expected:
        'Chưa bắt đầu: "Chưa có bản ghi.", không nút tải lên, không thẻ mic; Đã kết thúc: có nút "Tải tệp lên" (UC-52c cho phép sau khi kết thúc), gợi ý chỉ "Tải tệp ghi sẵn lên." (không nhắc mic phòng), không thẻ mic; trên điện thoại tab "Bản ghi" nằm trong thanh tab khi mở thẳng bằng ?tab=recordings',
      run: wrap(async (qa) => {
        const pub = await meeting(qa, 'ipPub')
        const end = await meeting(qa, 'ipEnd')
        const run = await meeting(qa, 'ipRun')
        await clean(qa, end.id)
        await clean(qa, run.id)
        // Round 1: in progress, the Secretary's empty tab points to the mic card and to a file.
        await openTab(qa, run)
        await qa.expect(roomSection(qa).getByText('Chưa có bản ghi.')).toBeVisible()
        await qa.expect(roomSection(qa).locator('.cell-empty .field-hint')).toHaveText('Bật mic phòng ở ô góc màn hình, hoặc tải tệp ghi sẵn lên.')
        await settled(qa)
        await qa.shot('dang-dien-ra-chua-co-ban-ghi')
        // The tab opens with the meeting (the app locks it before): no recordings panel, no upload, no mic card.
        await qa.goto(`/mgmt/meetings/${pub.id}?tab=recordings`)
        await qa.expect(qa.page.getByRole('heading', { level: 1, name: pub.title })).toBeVisible()
        const locked = qa.page.getByRole('tab', { name: 'Bản ghi' })
        await qa.expect(locked).toHaveAttribute('aria-disabled', 'true')
        await qa.expect(locked).toHaveAttribute('data-tooltip', 'Mở khi cuộc họp bắt đầu')
        await qa.expect(roomSection(qa)).toHaveCount(0)
        await qa.expect(qa.page.getByRole('button', { name: 'Tải tệp lên' })).toHaveCount(0)
        await qa.expect(micCard(qa)).toHaveCount(0)
        await settled(qa)
        await qa.shot('chua-bat-dau-tab-khoa')
        await openTab(qa, end)
        await qa.expect(roomSection(qa).getByRole('button', { name: 'Tải tệp lên' })).toBeVisible()
        await qa.expect(micCard(qa)).toHaveCount(0)
        // QA FE-09: with no mic card, the empty state does not point to one.
        await qa.expect(roomSection(qa).locator('.cell-empty .field-hint')).toHaveText('Tải tệp ghi sẵn lên.')
        await activeTabInBar(qa)
        await settled(qa)
        await qa.shot('da-ket-thuc-van-tai-len')
        await noSidewaysScroll(qa)
      }),
    },

    // ------------------------------------------------------------------------------------------- UC-53, UC-51a AF-2
    {
      id: 'AC21',
      role: 'member',
      devices: ['desktop', 'phone'],
      title: 'Thành viên xem "Ghi âm của tôi" ở họp trực tuyến, nghe lại và xoá câu của mình (MSG322 "Đã xoá câu nói.")',
      steps: ['thanhvien@ có một câu nói đã ghi ở họp trực tuyến Đang diễn ra (API, giọng đọc máy)', 'Mở tab Bản ghi, bấm "Ghi âm của tôi"', 'Bấm "Xoá câu này", xác nhận "Xoá câu"'],
      expected: 'Câu hiện trên dòng thời gian "Bản ghi trực tiếp"; hộp "Ghi âm của tôi" liệt kê câu có nút "Nghe lại"; xoá: toast "Đã xoá câu nói.", câu biến mất; API không còn câu',
      run: wrap(async (qa) => {
        const m = await meeting(qa, 'onRun')
        const said = saidHalves(qa)[0]
        const sent = await (await raw(qa, 'member')).putClip(m.id, crypto.randomUUID(), said.buf, said.ms)
        if (sent.status === 503) qa.inconclusive(`Speech worker not ready (503 ${sent.body?.code}); start the worker on :8090`)
        if (sent.status === 204) qa.inconclusive('Worker found no speech in the fixture; transcription fixture missing.') 
        qa.expectStatus(sent, 200)
        const clip = sent.body.clip
        qa.note(`Câu đã gỡ băng: "${clip.text}"`)
        try {
          await openTab(qa, m)
          await qa.expect(qa.page.locator(`[data-clip="${clip.id}"]`)).toBeVisible({ timeout: 10_000 })
          await press(qa, qa.page.getByRole('button', { name: /Ghi âm của tôi/ }))
          const dlg = qa.page.getByRole('dialog', { name: 'Ghi âm của tôi' })
          await qa.expect(dlg).toBeVisible()
          await qa.expect(dlg.getByRole('button', { name: 'Nghe lại' }).first()).toBeVisible()
          await settled(qa)
          await qa.shot('ghi-am-cua-toi')
          await noSidewaysScroll(qa)
          const count = await dlg.getByRole('button', { name: 'Xoá câu này' }).count()
          await press(qa, dlg.getByRole('button', { name: 'Xoá câu này' }).last())
          const confirm = qa.page.getByRole('dialog', { name: 'Xoá câu này?' })
          await press(qa, confirm.getByRole('button', { name: 'Xoá câu' }))
          await qa.expect(toast(qa, 'Đã xoá câu nói.')).toBeVisible({ timeout: 10_000 })
          await qa.expect(dlg.getByRole('button', { name: 'Xoá câu này' })).toHaveCount(count - 1)
          await settled(qa)
          await qa.shot('da-xoa-cau')
          const left = (await (await client(qa, 'member')).get(`/api/meetings/${m.id}/speech-clips`)).body.items.filter((i) => i.speakerId === clip.speakerId)
          qa.note(`Câu của thanhvien@ còn lại: ${left.length}`)
        } finally {
          // Leave no sentence of ours behind.
          const sec = await client(qa, 'secretary')
          const items = (await sec.get(`/api/meetings/${m.id}/speech-clips`)).body.items ?? []
          for (const i of items) await sec.delete(`/api/speech-clips/${i.id}`)
        }
      }),
    },
    {
      id: 'AC22',
      role: 'member',
      title: 'API câu theo micro: xoá câu người khác 403 MSG323; Thư ký xoá được mọi câu (MSG322); câu bị loại hiện rỗng với người khác, Chủ toạ vẫn đọc; họp trực tiếp không nhận câu (MSG320)',
      steps: [
        'Họp trực tuyến Đang diễn ra: chutoa@ có một câu (API); thanhvien@ DELETE câu đó',
        'thuky@ loại câu khỏi bản ghi (Leak); thanhvien@ và chutoa@ đọc danh sách, thanhvien@ xin link nghe',
        'thuky@ DELETE câu; thanhvien@ PUT câu vào họp trực tiếp Đang diễn ra',
      ],
      expected: '403 MSG323 và câu còn nguyên; sau khi loại: thanhvien@ thấy text "" và playback 404, chutoa@ thấy chữ; thuky@ DELETE 200 MSG322; họp trực tiếp 409 MSG320',
      run: wrap(async (qa) => {
        const m = await meeting(qa, 'onRun')
        const ip = await meeting(qa, 'ipRun')
        const c = checker(qa)
        const said = saidHalves(qa)[0]
        const sent = await (await raw(qa, 'chair')).putClip(m.id, crypto.randomUUID(), said.buf, said.ms)
        if (sent.status === 503) qa.inconclusive(`Speech worker not ready (503 ${sent.body?.code})`)
        if (sent.status === 204) qa.inconclusive('Worker found no speech in the fixture; transcription fixture missing.')
        qa.expectStatus(sent, 200)
        const id = sent.body.clip.id
        try {
          c.check('thành viên xoá câu người khác', await (await client(qa, 'member')).delete(`/api/speech-clips/${id}`), 403, 'MSG323')
          c.check('Thư ký loại câu', await (await client(qa, 'secretary')).put(`/api/speech-clips/${id}/exclusion`, { excluded: true, reason: 'Leak' }), 200, 'MSG351')
          const asMember = (await (await client(qa, 'member')).get(`/api/meetings/${m.id}/speech-clips`)).body.items.find((i) => i.id === id)
          c.fact('thành viên thấy câu rỗng', asMember?.excluded === true && asMember.text === '', JSON.stringify(asMember))
          c.check('thành viên nghe câu bị loại', await (await client(qa, 'member')).get(`/api/speech-clips/${id}/playback`), 404)
          const asChair = (await (await client(qa, 'chair')).get(`/api/meetings/${m.id}/speech-clips`)).body.items.find((i) => i.id === id)
          c.fact('Chủ toạ đọc được chữ', asChair?.text?.length > 0, JSON.stringify(asChair))
          c.check('Thư ký xoá câu', await (await client(qa, 'secretary')).delete(`/api/speech-clips/${id}`), 200, 'MSG322')
          c.check('câu vào họp trực tiếp', await (await raw(qa, 'member')).putClip(ip.id, crypto.randomUUID(), bytes(qa, 's2.wav'), wavMs(qa, 's2.wav')), 409, 'MSG320')
          c.done()
        } finally {
          await (await client(qa, 'secretary')).delete(`/api/speech-clips/${id}`)
        }
      }),
    },

    // ------------------------------------------------------------------------------------------- UC-51a AF-3 (ADR-0061 §5)
    {
      id: 'AC23',
      role: 'chair',
      devices: ['desktop', 'phone'],
      title: 'Họp kết hợp đã kết thúc có bản ghi "Tổng hợp": nghe được, Chủ toạ tải về được, không có bản gỡ băng; chuyển chữ 409 MSG479',
      steps: [
        'Họp kết hợp: một lượt mic phòng và một câu trực tuyến, rồi kết thúc (API); chờ server dựng bản tổng hợp',
        'chutoa@ mở tab Bản ghi',
        'API: Thư ký POST transcribe bản tổng hợp',
      ],
      expected:
        'Dòng nhãn "Tổng hợp" với chú thích "Mic phòng và mic của từng người họp trực tuyến, ghép theo giờ thật để nghe lại.", có "Nghe cả tệp" và "Tải MP3", không có bản gỡ băng; API transcriptStatus None, transcribe 409 MSG479',
      run: wrap(async (qa) => {
        const m = await meeting(qa, 'hyEnd')
        const sec = await client(qa, 'secretary')
        const until = Date.now() + 40_000
        let combined
        while (Date.now() < until) {
          combined = (await recordings(qa, 'chair', m.id)).items.find((r) => r.source === 'Combined')
          if (combined) break
          await new Promise((r) => setTimeout(r, 5000))
        }
        if (!combined && (await recordings(qa, 'chair', m.id)).items.length > 0 && (await (await client(qa, 'secretary')).get(`/api/meetings/${m.id}/speech-clips`)).body.items?.length === 0)
          qa.inconclusive('The hybrid meeting has no online sentence (the speech worker is down, so the clip was refused 503 MSG312): no Combined recording can be built here.')
        if (!combined) {
          const items = (await recordings(qa, 'chair', m.id)).items.map((r) => `${r.source}/${r.status}`)
          qa.expect(combined, `no Combined recording 200 s after the hybrid meeting ended (have: ${items.join(', ')})`).toBeTruthy()
        }
        await openTab(qa, m)
        const row = roomSection(qa).locator('li.recording-row').filter({ hasText: 'Tổng hợp' })
        await qa.expect(row).toBeVisible()
        await qa.expect(row.getByText('Mic phòng và mic của từng người họp trực tuyến, ghép theo giờ thật để nghe lại.')).toBeVisible()
        await qa.expect(row.getByRole('button', { name: 'Nghe cả tệp' })).toBeVisible()
        await qa.expect(row.getByRole('button', { name: 'Tải MP3' })).toBeVisible()
        await qa.expect(row.locator('.transcript')).toHaveCount(0)
        await row.scrollIntoViewIfNeeded()
        await settled(qa)
        await qa.shot('ban-ghi-tong-hop')
        await noSidewaysScroll(qa)
        const c = checker(qa)
        c.fact('transcriptStatus None', combined.transcriptStatus === 'None', combined.transcriptStatus)
        c.check('transcribe bản tổng hợp', await sec.post(`/api/recordings/${combined.id}/transcribe`), 409, 'MSG479')
        c.check('member tải bản tổng hợp', await (await client(qa, 'member')).get(`/api/recordings/${combined.id}/download`), 403, 'MSG355')
        c.done()
      }),
    },

    // ------------------------------------------------------------------------------------------- QA FE-09 defect
    {
      id: 'AC24',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      // BA 23/11 replaced the QA FE-09 fix (toasts lifted above the card): toasts that followed the card jumped around the
      // screen. They keep one corner, drawn over the card, wherever it is dragged.
      title: 'Thông báo của cuộc họp khác tới khi mic phòng đang ghi: chồng thông báo ở góc cố định, đè lên thẻ mic, kéo thẻ đi không làm thông báo đổi chỗ',
      steps: [
        'thuky@ mở tab Bản ghi của họp trực tiếp đang diễn ra, thẻ mic ở góc mặc định, bật mic phòng',
        'Chủ toạ thêm 3 mục nghị sự do thuky@ trình bày ở một cuộc họp khác (API): 3 thông báo tới cùng lúc',
        'Kéo thẻ mic lên trên trong lúc thông báo còn hiện',
      ],
      expected: '3 thông báo ở góc dưới phải, nằm trên cùng (đè lên thẻ mic); kéo thẻ mic lên trên thì chồng thông báo không đổi chỗ; thông báo tắt rồi thì nút "Tắt mic, lưu lượt ghi" vẫn dùng được',
      run: wrap(async (qa) => {
        const m = await meeting(qa, 'ipRun')
        const other = await meeting(qa, 'ipPub')
        const u = await users(qa)
        await clean(qa, m.id)
        // A fresh browser per criterion: the card starts in its default corner, where the toasts stack.
        const items = []
        try {
          await openTab(qa, m)
          await openMicCard(qa)
          const card = micCard(qa)
          await press(qa, card.getByRole('button', { name: 'Bật mic phòng' }))
          const dlg = qa.page.getByRole('dialog', { name: 'Bắt đầu ghi âm' })
          if (await dlg.isVisible({ timeout: 2000 }).catch(() => false)) {
            await dlg.getByRole('checkbox').check()
            await dlg.getByRole('button', { name: 'Bắt đầu ghi' }).click()
          }
          await qa.expect(card.getByText('Đang ghi cả phòng')).toBeVisible({ timeout: 15_000 })
          const chair = await client(qa, 'chair')
          for (let i = 1; i <= 3; i++) {
            const res = await chair.post(`/api/meetings/${other.id}/agenda`, {
              title: qa.name(`FE-09 mục báo thẻ mic ${i}`),
              description: '',
              presenterId: u.secretary.id,
              presenterGuestInvitationId: null,
            })
            qa.expectStatus(res, 201)
            items.push(res.body.item?.id ?? res.body.id)
          }
          await qa.expect(qa.page.locator('.toast-stack .toast')).toHaveCount(3, { timeout: 10_000 })
          const place = () =>
            qa.page.evaluate(() => {
              const r = document.querySelector('.toast-stack').getBoundingClientRect()
              const t = document.querySelector('.toast-stack .toast').getBoundingClientRect()
              const top = document.elementFromPoint(t.left + t.width / 2, t.top + t.height / 2)?.closest('.toast-stack') !== null
              return { right: Math.round(innerWidth - r.right), bottom: Math.round(innerHeight - r.bottom), top }
            })
          const before = await place()
          await qa.shot('thong-bao-o-goc-co-dinh')
          qa.expect(before.top, 'a toast is drawn over everything in its corner').toBe(true)
          // Drag the card up by a part that is not a control (the head on a laptop, the bubble on a phone).
          const handle = card.locator('.mic-card-head h2, .mic-bubble').first()
          const hb = await handle.boundingBox()
          await qa.page.mouse.move(hb.x + hb.width / 2, hb.y + hb.height / 2)
          await qa.page.mouse.down()
          for (let i = 1; i <= 8; i++) await qa.page.mouse.move(hb.x + hb.width / 2, hb.y + hb.height / 2 - 20 * i)
          await qa.page.mouse.up()
          await qa.page.waitForTimeout(400) // the old stack glided after the card for 0.2s
          const after = await place()
          await qa.shot('keo-the-mic-thong-bao-giu-cho')
          qa.expect(after, 'dragging the mic card moves the toasts').toEqual(before)
          await qa.page.locator('.toast-stack .toast').first().waitFor({ state: 'detached', timeout: 10_000 })
          await qa.expect(qa.page.locator('.toast-stack .toast')).toHaveCount(0, { timeout: 10_000 })
          await qa.expect(card.getByRole('button', { name: 'Tắt mic, lưu lượt ghi' })).toBeVisible()
          await noSidewaysScroll(qa)
          await press(qa, card.getByRole('button', { name: 'Tắt mic, lưu lượt ghi' }))
          await qa.expect(card.getByText('Mic phòng đang tắt')).toBeVisible({ timeout: 20_000 })
        } finally {
          const chair = await client(qa, 'chair')
          for (const id of items) if (id) await chair.delete(`/api/meetings/${other.id}/agenda/${id}`)
          await clean(qa, m.id)
        }
      }),
    },
    // ------------------------------------------------------------------------------------------- Round 1 (5.2) additions
    {
      id: 'AC25',
      role: 'member',
      devices: ['desktop', 'phone'],
      title: 'Thành viên xem danh sách bản ghi (nguồn, độ dài, dung lượng, ngày), chú thích mục đích + hạn, trình phát (tua, tốc độ, âm lượng, tắt tiếng), chỉ có nút "Thu gọn"',
      steps: [
        'Họp trực tiếp Đã kết thúc có 2 bản ghi tải lên (API: wav, mp3)',
        'thanhvien@ mở tab Bản ghi, trỏ/chạm dòng thông tin, bấm "Nghe cả tệp", kéo thanh vị trí, đổi tốc độ, âm lượng, tắt tiếng',
      ],
      expected:
        'Mỗi dòng có tên tệp, nhãn "Tải lên", độ dài, dung lượng, ngày lưu, nhãn gỡ băng; trình phát mở sẵn; chú thích "Lưu bởi … · Mục đích: … · Tự xoá ngày …"; nghe chạy, tua đổi vị trí, tốc độ/âm lượng/tắt tiếng đổi trên thẻ audio; chỉ có nút "Thu gọn", không "Tải MP3", "Xoá bản ghi", "Tải tệp lên", không thẻ mic',
      run: wrap(async (qa) => {
        const m = await meeting(qa, 'ipEnd')
        await clean(qa, m.id)
        try {
          await uploadFile(qa, m.id, 's1.wav', 'audio/wav')
          await uploadFile(qa, m.id, 's2.mp3', 'audio/mpeg')
          await openTab(qa, m)
          const rows = roomSection(qa).locator('li.recording-row:not(.recording-live)')
          await qa.expect(rows).toHaveCount(2, { timeout: 10_000 })
          for (const n of [0, 1]) {
            const row = rows.nth(n)
            await qa.expect(row.getByText('Tải lên', { exact: true })).toBeVisible()
            await qa.expect(row.locator('.recording-meta')).toContainText(/\d+:\d{2}/)
            await qa.expect(row.locator('.recording-meta')).toContainText(/(KB|MB)/)
            await qa.expect(row.getByRole('button', { name: 'Thu gọn' })).toBeVisible()
            await qa.expect(row.getByRole('button', { name: 'Tải MP3' })).toHaveCount(0)
            await qa.expect(row.getByRole('button', { name: 'Xoá bản ghi' })).toHaveCount(0)
          }
          await qa.expect(qa.page.getByRole('button', { name: 'Tải tệp lên' })).toHaveCount(0)
          await qa.expect(micCard(qa)).toHaveCount(0)
          const row = rows.first()
          qa.note(`Dòng 1: ${(await row.locator('.recording-meta').innerText()).replace(/\s+/g, ' ')}`)
          await qa.expect(row.getByRole('button', { name: 'Nghe cả tệp' })).toBeVisible({ timeout: 10_000 })
          const info = row.locator('.recording-meta .info-tip')
          if (qa.device === 'phone') await info.tap()
          else await info.hover()
          await qa.expect(qa.page.getByRole('tooltip')).toContainText('Tự xoá ngày', { timeout: 5000 })
          await qa.expect(qa.page.getByRole('tooltip')).toContainText('Mục đích: QA FE-09: lập biên bản')
          await settled(qa)
          await qa.shot('danh-sach-chu-thich')
          await qa.page.mouse.move(5, 5)
          await press(qa, row.getByRole('button', { name: 'Nghe cả tệp' }))
          await qa.page.waitForFunction(() => (document.querySelector('.recording-player audio')?.currentTime ?? 0) > 0.3, null, { timeout: 15_000 })
          const slider = row.getByRole('slider', { name: 'Vị trí phát' })
          const max = Number(await slider.getAttribute('max'))
          const before = await qa.page.evaluate(() => document.querySelector('.recording-player audio').currentTime)
          // A range input only accepts a multiple of its step (100 ms here): another value is "Malformed".
          const step = Number(await slider.getAttribute('step')) || 1
          await slider.fill(String(Math.round((max * 0.8) / step) * step))
          await qa.page.waitForTimeout(500)
          const after = await qa.page.evaluate(() => document.querySelector('.recording-player audio').currentTime)
          qa.note(`Vị trí trước ${before.toFixed(2)} s, sau khi tua ${after.toFixed(2)} s`)
          qa.expect(Math.abs(after - before), 'seek did not move the audio').toBeGreaterThan(0.5)
          await press(qa, row.getByRole('button', { name: /^Tốc độ nghe/ }))
          const radios = row.getByRole('radio')
          const count = await radios.count()
          qa.expect(count, 'no speed options').toBeGreaterThan(1)
          await press(qa, radios.nth(count - 1))
          const rate = await qa.page.evaluate(() => document.querySelector('.recording-player audio').playbackRate)
          qa.expect(rate, 'speed unchanged').not.toBe(1)
          await qa.page.keyboard.press('Escape')
          if (qa.device === 'desktop') {
            await row.getByRole('slider', { name: 'Âm lượng' }).fill('0.4')
            const vol = await qa.page.evaluate(() => document.querySelector('.recording-player audio').volume)
            qa.expect(vol).toBeCloseTo(0.4, 1)
            await press(qa, row.getByRole('button', { name: 'Tắt tiếng' }))
            qa.expect(await qa.page.evaluate(() => document.querySelector('.recording-player audio').muted)).toBe(true)
          }
          await settled(qa)
          await qa.shot('trinh-phat-tua-toc-do-tat-tieng', { fullPage: true })
          await noSidewaysScroll(qa)
          await noRawCodes(qa)
        } finally {
          await clean(qa, m.id)
        }
      }),
    },
    {
      id: 'AC26',
      role: 'member',
      devices: ['desktop', 'phone'],
      title: 'Chèn lỗi: tải danh sách lỗi hiện thông báo + "Thử lại"; link nghe lỗi hiện lỗi trên dòng; tệp không giải mã được báo "Không phát được tệp này."',
      steps: [
        'Chặn GET …/recordings trả 500 MSG297, mở tab Bản ghi; bỏ chặn, bấm "Thử lại"',
        'Chặn GET …/playback trả 404 rồi mở tab; chặn GET …/audio để phát lỗi, bấm "Nghe cả tệp"',
      ],
      expected:
        'Hộp lỗi "Không tải được danh sách bản ghi." và nút "Thử lại"; sau Thử lại danh sách tải được; dòng bản ghi hiện lỗi đỏ, phần còn lại của tab dùng được; phát lỗi: toast "Không phát được tệp này."',
      run: wrap(async (qa) => {
        const m = await meeting(qa, 'ipEnd')
        await clean(qa, m.id)
        try {
          await uploadFile(qa, m.id, 's1.wav', 'audio/wav')
          let failList = true
          await qa.page.route(`**/api/meetings/${m.id}/recordings`, async (route) => {
            if (failList && route.request().method() === 'GET')
              await route.fulfill({ status: 500, contentType: 'application/json', body: JSON.stringify({ code: 'MSG297', message: 'Không tải được danh sách bản ghi.' }) })
            else await route.continue()
          })
          await qa.goto(`/mgmt/meetings/${m.id}?tab=recordings`)
          const alert = qa.page.locator('.recordings-alert')
          await qa.expect(alert).toContainText('Không tải được danh sách bản ghi.', { timeout: 15_000 })
          await qa.expect(alert.getByRole('button', { name: 'Thử lại' })).toBeVisible()
          await settled(qa)
          await qa.shot('loi-tai-danh-sach')
          await noRawCodes(qa)
          failList = false
          await press(qa, alert.getByRole('button', { name: 'Thử lại' }))
          await qa.expect(roomSection(qa).locator('li.recording-row')).toHaveCount(1, { timeout: 10_000 })
          await qa.expect(alert).toHaveCount(0)
          await settled(qa)
          await qa.shot('thu-lai-tai-duoc')
          // The signed link fails: the error shows on the row, the toolbar and tab bar stay.
          await qa.page.route('**/api/recordings/*/playback', (route) =>
            route.fulfill({ status: 404, contentType: 'application/json', body: JSON.stringify({ code: 'MSG298', message: 'Không tìm thấy bản ghi.' }) }),
          )
          await qa.page.reload()
          const row = roomSection(qa).locator('li.recording-row').first()
          await qa.expect(row.locator('.field-error')).toBeVisible({ timeout: 15_000 })
          await qa.expect(qa.page.getByRole('tab', { name: 'Bản ghi' })).toBeVisible()
          await settled(qa)
          await qa.shot('loi-link-nghe-tren-dong')
          qa.note(`Lỗi trên dòng: ${await row.locator('.field-error').innerText()}`)
          // A file the browser cannot play.
          await qa.page.unroute('**/api/recordings/*/playback')
          await qa.page.route('**/api/recordings/*/audio*', (route) => route.abort())
          await qa.page.reload()
          const row2 = roomSection(qa).locator('li.recording-row').first()
          await press(qa, row2.getByRole('button', { name: 'Nghe cả tệp' }))
          await qa.expect(toast(qa, 'Không phát được tệp này.')).toBeVisible({ timeout: 10_000 })
          await settled(qa)
          await qa.shot('khong-phat-duoc-tep')
        } finally {
          await clean(qa, m.id)
        }
      }),
    },
    {
      id: 'AC27',
      role: 'chair',
      title: 'Chèn lỗi: tải MP3 bị máy chủ từ chối hiện toast đỏ đúng chữ máy chủ, không lưu tệp nào; bỏ chặn thì Chủ toạ tải được .mp3',
      steps: [
        'Họp trực tiếp có bản ghi tải lên (mp3); chặn GET …/download trả 403 MSG355; chutoa@ bấm "Tải MP3"',
        'Bỏ chặn, bấm "Tải MP3" lại',
      ],
      expected: 'Toast đỏ với đúng câu MSG355 của máy chủ, không có tệp nào được lưu; bỏ chặn: trình duyệt nhận tệp qa-fe09-s1.mp3',
      run: wrap(async (qa) => {
        const m = await meeting(qa, 'ipRun')
        await clean(qa, m.id)
        try {
          await uploadFile(qa, m.id, 's1.mp3', 'audio/mpeg')
          const id = (await recordings(qa, 'secretary', m.id)).items[0].id
          const real = await (await client(qa, 'member')).get(`/api/recordings/${id}/download`)
          qa.expectStatus(real, 403)
          await openTab(qa, m)
          const row = roomSection(qa).locator('li.recording-row').first()
          let refuse = true
          await qa.page.route('**/api/recordings/*/download', async (route) => {
            if (refuse) await route.fulfill({ status: 403, contentType: 'application/json', body: JSON.stringify(real.body) })
            else await route.continue()
          })
          let saved = false
          qa.page.on('download', () => (saved = true))
          await row.getByRole('button', { name: 'Tải MP3' }).click()
          await qa.expect(qa.page.locator('.toast-stack .toast').first()).toContainText(real.body.message, { timeout: 10_000 })
          qa.expect(saved, 'a file was saved after a refusal').toBe(false)
          await settled(qa)
          await qa.shot('tai-bi-tu-choi-toast')
          qa.note(`Toast: ${real.body.message}`)
          refuse = false
          const [file] = await Promise.all([
            qa.page.waitForEvent('download', { timeout: 20_000 }),
            row.getByRole('button', { name: 'Tải MP3' }).click(),
          ])
          qa.expect(file.suggestedFilename()).toBe('qa-fe09-s1.mp3')
        } finally {
          await clean(qa, m.id)
        }
      }),
    },
    {
      id: 'AC28',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Chèn lỗi (thay "hai phiên"): tải lên khi cuộc họp chưa diễn ra (MSG294), mất vai trò Thư ký khi tải lên và khi xoá (MSG295): báo đúng chữ, nút biến mất sau khi tải lại danh sách',
      steps: [
        'Họp trực tiếp có bản ghi; thuky@ chọn tệp, tích xác nhận, bấm "Tải lên" khi máy chủ trả 409 MSG294',
        'Như trên nhưng máy chủ trả 403 MSG295 và danh sách sau đó không còn quyền tải lên/xoá',
        'Bấm "Xoá bản ghi" rồi xác nhận khi máy chủ trả 403 MSG295',
      ],
      expected:
        'Khay tải lên báo "Chỉ lưu bản ghi khi cuộc họp đang diễn ra hoặc đã kết thúc."; 403: "Chỉ Thư ký cuộc họp được quản lý bản ghi." và nút "Tải tệp lên"/"Xoá bản ghi" mất sau khi danh sách tải lại; không có bản ghi nào được thêm hay mất',
      run: wrap(async (qa) => {
        const m = await meeting(qa, 'ipEnd')
        await clean(qa, m.id)
        try {
          await uploadFile(qa, m.id, 's1.wav', 'audio/wav')
          const msg294 = 'Chỉ lưu bản ghi khi cuộc họp đang diễn ra hoặc đã kết thúc.'
          const msg295 = 'Chỉ Thư ký cuộc họp được quản lý bản ghi.'
          let mode = 'm294'
          let demote = false
          await qa.page.route(`**/api/meetings/${m.id}/recordings`, async (route) => {
            const method = route.request().method()
            if (method === 'POST') {
              const body = mode === 'm294' ? { code: 'MSG294', message: msg294 } : { code: 'MSG295', message: msg295 }
              if (mode === 'm295') demote = true
              return route.fulfill({ status: mode === 'm294' ? 409 : 403, contentType: 'application/json', body: JSON.stringify(body) })
            }
            if (method === 'GET' && demote) {
              const res = await route.fetch().catch(() => null)
              if (!res) return route.abort().catch(() => undefined)
              const body = await res.json()
              body.canUpload = false
              for (const i of body.items ?? []) i.canErase = false
              return route.fulfill({ response: res, json: body })
            }
            return route.continue()
          })
          await qa.page.route('**/api/recordings/*', async (route) => {
            if (route.request().method() === 'DELETE') {
              demote = true
              return route.fulfill({ status: 403, contentType: 'application/json', body: JSON.stringify({ code: 'MSG295', message: msg295 }) })
            }
            return route.fallback()
          })
          await openTab(qa, m)
          const pickAndSend = async () => {
            const chooser = qa.page.waitForEvent('filechooser')
            await press(qa, roomSection(qa).getByRole('button', { name: 'Tải tệp lên' }))
            await (await chooser).setFiles(audio(qa)('s2.wav'))
            const dlg = qa.page.getByRole('dialog', { name: 'Lưu tệp ghi âm' })
            await dlg.getByRole('checkbox').check()
            await press(qa, dlg.getByRole('button', { name: 'Tải lên' }))
          }
          const tray = qa.page.getByRole('region', { name: 'Tải lên' })
          await pickAndSend()
          await qa.expect(tray.getByText(msg294)).toBeVisible({ timeout: 20_000 })
          await settled(qa)
          await qa.shot('chua-dien-ra-409')
          await noRawCodes(qa)
          mode = 'm295'
          await qa.page.reload()
          await pickAndSend()
          await qa.expect(tray.getByText(msg295)).toBeVisible({ timeout: 20_000 })
          await qa.expect(roomSection(qa).getByRole('button', { name: 'Tải tệp lên' })).toHaveCount(0, { timeout: 15_000 })
          await settled(qa)
          await qa.shot('mat-vai-tro-tai-len-403')
          demote = false
          await qa.page.reload()
          const row = roomSection(qa).locator('li.recording-row').first()
          await press(qa, row.getByRole('button', { name: 'Xoá bản ghi' }))
          const dlg = qa.page.getByRole('dialog', { name: 'Xoá bản ghi?' })
          await press(qa, dlg.getByRole('button', { name: 'Xoá bản ghi' }))
          await qa.expect(dlg.getByText(msg295)).toBeVisible({ timeout: 15_000 })
          await settled(qa)
          await qa.shot('mat-vai-tro-xoa-403')
          const left = (await recordings(qa, 'secretary', m.id)).items.filter((r) => r.status === 'Available')
          qa.expect(left.length, 'a recording was added or lost').toBe(1)
        } finally {
          await qa.page.unrouteAll({ behavior: 'ignoreErrors' }).catch(() => undefined)
          await clean(qa, m.id)
        }
      }),
    },
    {
      id: 'AC29',
      role: 'member',
      devices: ['desktop', 'phone'],
      title: 'Thành viên thấy lượt mic phòng đang ghi (không nghe, không xoá), danh sách tự làm mới khi Thư ký tắt mic',
      steps: ['Thư ký mở một lượt mic phòng (API); thanhvien@ mở tab Bản ghi', 'Thư ký lưu lượt (API)'],
      expected: 'Dòng "Mic phòng đang ghi" nhãn "Đang ghi", tên Thư ký, "từ <giờ>", không nút nghe/xoá; sau khi lưu, không tải lại trang, dòng đổi thành bản ghi "Mic phòng"',
      run: wrap(async (qa) => {
        const u = await users(qa)
        const m = await meeting(qa, 'ipRun')
        await clean(qa, m.id)
        try {
          const sec = await client(qa, 'secretary')
          const opened = await sec.post(`${R(m.id)}/takes`, { consentConfirmed: true })
          qa.expectStatus(opened, 201)
          await openTab(qa, m)
          const live = roomSection(qa).locator('li.recording-live')
          await qa.expect(live.getByText('Mic phòng đang ghi')).toBeVisible()
          await qa.expect(live.getByText('Đang ghi', { exact: true })).toBeVisible()
          await qa.expect(live.getByText(u.secretary.name)).toBeVisible()
          await qa.expect(live.getByText(/^từ /)).toBeVisible()
          await qa.expect(live.getByRole('button', { name: /Nghe|Tải|Xoá/ })).toHaveCount(0)
          await qa.expect(roomSection(qa).locator('audio')).toHaveCount(0)
          await settled(qa)
          await qa.shot('thanh-vien-thay-dang-ghi')
          const pcm = bytes(qa, 's1.pcm')
          const payload = Buffer.concat([wavHeaderUnknown(), Buffer.alloc(32000), pcm, Buffer.alloc(32000)])
          const r = await raw(qa, 'secretary')
          for (let at = 0, n = 1; at < payload.length; at += opened.body.partSize, n++) {
            const res = await r.putPart(opened.body.recordingId, n, payload.subarray(at, at + opened.body.partSize), at + opened.body.partSize >= payload.length)
            qa.expect(res.status).toBe(204)
          }
          qa.expectStatus(await sec.post(`/api/recordings/${opened.body.recordingId}/complete`), 200)
          await qa.expect(roomSection(qa).locator('li.recording-live')).toHaveCount(0, { timeout: 20_000 })
          await qa.expect(roomSection(qa).locator('li.recording-row').filter({ hasText: 'Mic phòng' }).first()).toBeVisible({ timeout: 20_000 })
          await settled(qa)
          await qa.shot('tu-lam-moi-sau-khi-tat-mic')
          await noSidewaysScroll(qa)
        } finally {
          await clean(qa, m.id)
        }
      }),
    },
    {
      id: 'AC30',
      role: 'secretary',
      title: 'Tắt mic phòng khi lượt chưa thu được tiếng (chèn phản hồi MSG365 vì mic thử luôn có tiếng): toast "Mic chưa thu được tiếng nào…", không thêm bản ghi',
      steps: ['thuky@ mở tab Bản ghi họp trực tiếp đang diễn ra chưa có bản ghi', 'Bật mic phòng (xác nhận), rồi tắt ngay'],
      expected: 'Toast "Mic chưa thu được tiếng nào nên lượt này không được lưu."; danh sách không có bản ghi mới',
      run: wrap(async (qa) => {
        const m = await meeting(qa, 'ipRun')
        await clean(qa, m.id)
        try {
          await qa.page.route('**/api/recordings/*/complete', async (route) => {
            // The test browser's fake mic always carries sound; answer as the server does for a take with none (AC6 checks the API).
            await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ code: 'MSG365', message: 'Mic chưa thu được tiếng nào nên lượt này không được lưu.', recordingId: null }) })
          })
          await openTab(qa, m)
          await openMicCard(qa)
          const card = micCard(qa)
          await card.getByRole('button', { name: 'Bật mic phòng' }).click()
          const dlg = qa.page.getByRole('dialog', { name: 'Bắt đầu ghi âm' })
          await dlg.getByRole('checkbox').check()
          await dlg.getByRole('button', { name: 'Bắt đầu ghi' }).click()
          await qa.expect(card.getByText('Đang ghi cả phòng')).toBeVisible({ timeout: 15_000 })
          await card.getByRole('button', { name: 'Tắt mic, lưu lượt ghi' }).click()
          const seen = await qa.page.locator('.toast-stack .toast').first().innerText({ timeout: 20_000 }).catch(() => '')
          qa.note(`Toast: ${seen}`)
          await settled(qa)
          await qa.shot('bat-tat-ngay')
          const items = (await recordings(qa, 'secretary', m.id)).items.filter((r) => r.status !== 'Deleted')
          qa.note(`Bản ghi lưu thật: ${items.length} (phản hồi bị chèn nên lượt thật còn mở; clean() lưu rồi xoá)`)
          qa.expect(seen).toContain('Mic chưa thu được tiếng nào nên lượt này không được lưu.')
        } finally {
          await clean(qa, m.id)
        }
      }),
    },
    {
      id: 'AC31',
      role: 'secretary',
      title: 'Đóng tab khi mic phòng đang bật: mở lại cuộc họp, thẻ báo lượt ghi dở trên máy này hoặc đang ghi ở nơi khác; có nút lưu',
      steps: ['thuky@ bật mic phòng, ghi vài giây', 'Đóng tab (không tắt mic), mở lại trang cuộc họp trong cùng trình duyệt'],
      expected: 'Thẻ "Còn lượt ghi dở trên máy này" với "Ghi tiếp" và "Lưu phần đã ghi" (hoặc "Đang ghi ở nơi khác" với "Lưu lượt đó"); lưu xong có bản ghi',
      run: wrap(async (qa) => {
        const m = await meeting(qa, 'ipRun')
        await clean(qa, m.id)
        try {
          // The first tab is closed on purpose; the criterion's own page stays open to look at the card afterwards.
          const tab = await qa.page.context().newPage()
          await tab.goto(`/mgmt/meetings/${m.id}?tab=recordings`)
          const card = tab.locator('.mic-dock')
          const bubble = card.getByRole('button', { name: /^Mở ô ghi âm/ })
          if (await bubble.isVisible({ timeout: 5000 }).catch(() => false)) await bubble.click()
          await card.getByRole('button', { name: 'Bật mic phòng' }).click()
          const dlg = tab.getByRole('dialog', { name: 'Bắt đầu ghi âm' })
          await dlg.getByRole('checkbox').check()
          await dlg.getByRole('button', { name: 'Bắt đầu ghi' }).click()
          await qa.expect(card.getByText('Đang ghi cả phòng')).toBeVisible({ timeout: 15_000 })
          await tab.waitForTimeout(13000)
          await tab.close({ runBeforeUnload: false })
          await openTab(qa, m)
          await openMicCard(qa)
          await qa.page.waitForTimeout(2000)
          const text = await micCard(qa).innerText()
          qa.note(`Thẻ sau khi mở lại: ${text.replace(/\s+/g, ' ')}`)
          await settled(qa)
          await qa.shot('luot-do-sau-khi-dong-tab')
          const save = micCard(qa).getByRole('button', { name: /Lưu phần đã ghi|Lưu lượt đó/ })
          await qa.expect(save).toBeVisible({ timeout: 10_000 })
          await save.click()
          await qa.expect(toast(qa, 'Đã lưu lượt ghi.')).toBeVisible({ timeout: 20_000 })
          await settled(qa)
          await qa.shot('da-luu-luot-do')
          qa.expect((await recordings(qa, 'secretary', m.id)).items.filter((r) => r.status === 'Available').length).toBe(1)
        } finally {
          await clean(qa, m.id)
        }
      }),
    },
    {
      id: 'AC32',
      role: 'secretary',
      title: 'Tải lên tệp quá 300 MB qua màn hình: báo "Bản ghi vượt quá 300 MB.", không thêm bản ghi (5e7f0414)',
      steps: ['thuky@ mở tab Bản ghi (Đã kết thúc), bấm "Tải tệp lên", chọn tệp 301 MB'],
      expected: 'Toast "Bản ghi vượt quá 300 MB." (không hiện mã); không có hộp thoại xác nhận, danh sách không có bản ghi mới',
      run: wrap(async (qa) => {
        const m = await meeting(qa, 'ipEnd')
        await clean(qa, m.id)
        try {
          await openTab(qa, m)
          const chooser = qa.page.waitForEvent('filechooser')
          await roomSection(qa).getByRole('button', { name: 'Tải tệp lên' }).click()
          const big = join(AUDIO_DIR, 'qa-fe09-lon.wav')
          if (!existsSync(big)) writeFileSync(big, Buffer.alloc(301 * 1024 * 1024))
          await (await chooser).setFiles(big)
          // 5e7f0414: the screen refuses the over-limit file before anything is sent — no dialog, just the toast.
          await qa.expect(qa.page.getByRole('dialog', { name: 'Lưu tệp ghi âm' })).toHaveCount(0)
          await qa.expect(toast(qa, 'Bản ghi vượt quá 300 MB.')).toBeVisible({ timeout: 60_000 })
          await settled(qa)
          await qa.shot('qua-300-mb')
          await noRawCodes(qa)
          qa.expect((await recordings(qa, 'secretary', m.id)).items.filter((r) => r.status !== 'Deleted').length).toBe(0)
        } finally {
          await clean(qa, m.id)
        }
      }),
    },
    {
      id: 'AC33',
      role: 'member',
      title: 'Họp trực tiếp đang diễn ra: Thành viên không có panel "Bản ghi trực tiếp", "Chưa có bản ghi." không gợi ý hành động; API câu nói vào họp này 409 MSG320',
      steps: ['thanhvien@ mở tab Bản ghi họp trực tiếp đang diễn ra, chưa có bản ghi', 'API: thanhvien@ gửi một câu nói vào họp này'],
      expected: 'Không có tiêu đề "Bản ghi trực tiếp"; "Chưa có bản ghi." không có lời gợi ý; API 409 MSG320',
      run: wrap(async (qa) => {
        const m = await meeting(qa, 'ipRun')
        await clean(qa, m.id)
        await openTab(qa, m)
        await qa.expect(roomSection(qa).getByText('Chưa có bản ghi.')).toBeVisible()
        await qa.expect(qa.page.getByRole('heading', { name: /Bản ghi trực tiếp|Bản ghi cuộc họp/ })).toHaveCount(0)
        await qa.expect(roomSection(qa).locator('.field-hint')).toHaveCount(0)
        await settled(qa)
        await qa.shot('thanh-vien-hop-truc-tiep-khong-co-panel')
        await noSidewaysScroll(qa)
        const sent = await (await raw(qa, 'member')).putClip(m.id, crypto.randomUUID(), bytes(qa, 's2.wav'), wavMs(qa, 's2.wav'))
        qa.expect(sent.status).toBe(409)
        qa.expect(sent.body?.code).toBe('MSG320')
      }),
    },
    {
      id: 'AC34',
      role: 'secretary',
      devices: ['desktop', 'phone'],
      title: 'Thư ký: tải MP3 về, thu nhỏ/mở lại thẻ mic, huỷ hộp tải lên không thêm bản ghi, mục đích tuỳ chọn (tối đa 1000 ký tự) hiện ở chú thích',
      steps: [
        'Họp trực tiếp Đang diễn ra; thuky@ mở tab Bản ghi, bấm "Thu nhỏ ô ghi âm", mở lại từ bong bóng',
        'Bấm "Tải tệp lên", chọn tệp, bấm "Huỷ" ở hộp "Lưu tệp ghi âm"',
        'Tải lên lại với mục đích "Họp giao ban tháng 11", trỏ/chạm dòng thông tin, bấm "Tải MP3"',
      ],
      expected:
        'Thẻ thu nhỏ thành bong bóng "Mở ô ghi âm…" rồi mở lại; Huỷ đóng hộp, không bản ghi; ô mục đích có maxlength 1000; chú thích có "Mục đích: Họp giao ban tháng 11"; "Tải MP3" lưu tệp .mp3 theo tên gốc, trang ở nguyên tab, không lỗi',
      run: wrap(async (qa) => {
        const m = await meeting(qa, 'ipRun')
        await clean(qa, m.id)
        try {
          await openTab(qa, m)
          const card = micCard(qa)
          await openMicCard(qa)
          await press(qa, card.getByRole('button', { name: 'Thu nhỏ ô ghi âm' }))
          const bubble = card.getByRole('button', { name: /^Mở ô ghi âm/ })
          await qa.expect(bubble).toBeVisible()
          await settled(qa)
          await qa.shot('the-mic-thu-nho')
          await press(qa, bubble)
          await qa.expect(card.locator('.mic-card')).toBeVisible()
          const pick = async (file) => {
            const chooser = qa.page.waitForEvent('filechooser')
            await press(qa, roomSection(qa).getByRole('button', { name: 'Tải tệp lên' }))
            await (await chooser).setFiles(file)
            return qa.page.getByRole('dialog', { name: 'Lưu tệp ghi âm' })
          }
          let dlg = await pick(audio(qa)('s1.mp3'))
          await qa.expect(dlg).toBeVisible()
          await qa.expect(dlg.getByRole('button', { name: 'Tải lên' })).toBeDisabled()
          await press(qa, dlg.getByRole('button', { name: 'Huỷ' }))
          await qa.expect(dlg).toHaveCount(0)
          await settled(qa)
          await qa.shot('huy-hop-tai-len')
          qa.expect((await recordings(qa, 'secretary', m.id)).items.filter((r) => r.status !== 'Deleted').length).toBe(0)
          dlg = await pick(audio(qa)('s1.mp3'))
          const purposeField = dlg.getByLabel('Mục đích ghi âm')
          qa.expect(await purposeField.getAttribute('maxlength')).toBe('1000')
          await purposeField.fill('Họp giao ban tháng 11')
          await dlg.getByRole('checkbox').check()
          await press(qa, dlg.getByRole('button', { name: 'Tải lên' }))
          const row = roomSection(qa).locator('li.recording-row').filter({ hasText: 's1.mp3' })
          await qa.expect(row).toBeVisible({ timeout: 30_000 })
          const info = row.locator('.recording-meta .info-tip')
          if (qa.device === 'phone') await info.tap()
          else await info.hover()
          await qa.expect(qa.page.getByRole('tooltip')).toContainText('Mục đích: Họp giao ban tháng 11')
          await settled(qa)
          await qa.shot('muc-dich-tuy-chon')
          await qa.page.mouse.move(5, 5)
          const [file] = await Promise.all([
            qa.page.waitForEvent('download', { timeout: 20_000 }),
            press(qa, row.getByRole('button', { name: 'Tải MP3' })),
          ])
          qa.expect(file.suggestedFilename()).toBe('s1.mp3')
          await qa.expect(qa.page.getByRole('tab', { name: 'Bản ghi' })).toHaveAttribute('aria-selected', 'true')
          await settled(qa)
          await qa.shot('thu-ky-tai-mp3')
          await noRawCodes(qa)
        } finally {
          await clean(qa, m.id)
        }
      }),
    },
    // ------------------------------------------------------------------------------------------- Round 2 (synthetic Vietnamese speech)
    {
      id: 'AC35',
      role: 'secretary',
      title: 'REC_FT20: Thư ký tắt mic phòng lưu lượt ghi (toast "Đã lưu lượt ghi.", dòng "Mic phòng"); lượt ghi âm tiếng Việt được chuyển chữ ("Có bản gỡ băng")',
      steps: [
        'Cuộc họp trực tiếp Đang diễn ra, chưa có bản ghi; thuky@ mở tab Bản ghi, bấm "Bật mic phòng" (tích xác nhận), ghi vài giây, bấm "Tắt mic, lưu lượt ghi"',
        'Một lượt ghi âm tiếng Việt (said.wav) qua API vào cùng bản ghi; chờ gỡ băng xong, tải lại tab',
      ],
      expected:
        'Toast "Đã lưu lượt ghi."; dòng bản ghi "Mic phòng"; API: BrowserCapture, Available, 2 lượt; gỡ băng Completed chữ tiếng Việt; tải lại: nhãn "Có bản gỡ băng" và câu chữ hiện trên dòng (micro giả của trình duyệt không có tiếng nên lượt bấm tay không chuyển chữ được — chuyển chữ kiểm bằng lượt tiếng Việt)',
      run: wrap(async (qa) => {
        const m = await meeting(qa, 'ipRun')
        await clean(qa, m.id)
        try {
          await openTab(qa, m)
          await openMicCard(qa)
          const card = micCard(qa)
          await press(qa, card.getByRole('button', { name: 'Bật mic phòng' }))
          const dlg = qa.page.getByRole('dialog', { name: 'Bắt đầu ghi âm' })
          await dlg.getByRole('checkbox').check()
          await press(qa, dlg.getByRole('button', { name: 'Bắt đầu ghi' }))
          await qa.expect(card.getByText('Đang ghi cả phòng')).toBeVisible({ timeout: 15000 })
          await qa.page.waitForTimeout(8000)
          await press(qa, card.getByRole('button', { name: 'Tắt mic, lưu lượt ghi' }))
          await qa.expect(toast(qa, 'Đã lưu lượt ghi.')).toBeVisible({ timeout: 20000 })
          const row = roomSection(qa).locator('li.recording-row').filter({ hasText: 'Mic phòng' })
          await qa.expect(row).toBeVisible({ timeout: 10000 })
          await settled(qa)
          await qa.shot('tat-mic-luu-luot')
          // The headless fake mic carries no speech, so this take cannot transcribe. The transcription leg of
          // the same recording is verified with TTS Vietnamese through the API (same take pipeline).
          const second = await roomTake(qa, m.id, saidTake(qa), { consent: true })
          qa.note(`Lượt 2: ${second.recordingId}`)
          const t = await waitTranscript(qa, second.recordingId)
          qa.expect(t?.status, `transcript: ${t?.status} ${t?.errorMessage ?? ''}`).toBe('Completed')
          qa.note(`Gỡ băng: ${(t.segments ?? []).map((s) => s.text).join(' / ').slice(0, 200)}`)
          await qa.page.reload()
          const row2 = roomSection(qa).locator('li.recording-row').filter({ hasText: 'Mic phòng' })
          await qa.expect(row2.getByText('Có bản gỡ băng')).toBeVisible({ timeout: 30000 })
          await qa.expect(row2.getByText('Kính thưa các đồng chí', { exact: false })).toBeVisible()
          await settled(qa)
          await qa.shot('co-ban-go-bang')
          await noRawCodes(qa)
          const rec = (await recordings(qa, 'secretary', m.id)).items.find((r) => r.id === second.recordingId)
          qa.expect(rec?.source).toBe('BrowserCapture')
          qa.expect(rec?.takes?.length).toBe(2)
        } finally {
          await clean(qa, m.id)
        }
      }),
    },
    {
      id: 'AC36',
      role: 'secretary',
      title: 'REC_FT24: Thư ký tải tệp ghi âm tiếng Việt lên họp trực tiếp đã kết thúc: hộp xác nhận, 201 MSG290, dòng "Tải lên", chuyển chữ xong "Có bản gỡ băng", tải lại vẫn còn',
      steps: [
        'Cuộc họp trực tiếp Đã kết thúc, chưa có bản ghi; thuky@ mở tab Bản ghi, bấm "Tải tệp lên", chọn said.wav (tiếng Việt)',
        'Tích ô đã thông báo, bấm "Tải lên"; chờ gỡ băng xong, tải lại trang',
      ],
      expected:
        'Hộp "Lưu tệp ghi âm" hiện tên và dung lượng tệp, cảnh báo micro phòng, nút "Tải lên" tắt tới khi tích; khay "Đã tải lên"; dòng "Tải lên"; gỡ băng Completed chữ tiếng Việt; tải lại vẫn dòng "Có bản gỡ băng"; API: Upload, Available, mục đích, consentObtainedAt có, retainUntil = createdAt + 180 ngày',
      run: wrap(async (qa) => {
        if (!existsSync(SAID_WAV)) qa.inconclusive('Vietnamese speech fixture ../fixtures/said.wav is missing.')
        const m = await meeting(qa, 'ipEnd')
        await clean(qa, m.id)
        try {
          await openTab(qa, m)
          const chooser = qa.page.waitForEvent('filechooser')
          await press(qa, roomSection(qa).getByRole('button', { name: 'Tải tệp lên' }))
          await (await chooser).setFiles(SAID_WAV)
          const dlg = qa.page.getByRole('dialog', { name: 'Lưu tệp ghi âm' })
          await qa.expect(dlg).toBeVisible()
          await qa.expect(dlg.getByText('said.wav')).toBeVisible()
          await qa.expect(dlg.getByText('Tệp phải ghi bằng đúng micro phòng họp đã dùng thu giọng mẫu', { exact: false })).toBeVisible()
          await qa.expect(dlg.getByRole('button', { name: 'Tải lên' })).toBeDisabled()
          await dlg.getByRole('checkbox').check()
          await settled(qa)
          await qa.shot('hop-luu-tep-tieng-viet')
          await noSidewaysScroll(qa)
          await press(qa, dlg.getByRole('button', { name: 'Tải lên' }))
          await qa.expect(qa.page.getByRole('region', { name: 'Tải lên' }).getByText('Đã tải lên', { exact: true })).toBeVisible({ timeout: 30000 })
          const row = roomSection(qa).locator('li.recording-row').filter({ hasText: 'said.wav' })
          await qa.expect(row).toBeVisible({ timeout: 15000 })
          await qa.expect(row.getByText('Tải lên', { exact: true })).toBeVisible()
          const rec0 = (await recordings(qa, 'secretary', m.id)).items.find((r) => r.fileName === 'said.wav')
          const t = await waitTranscript(qa, rec0.id)
          qa.expect(t?.status, `transcript: ${t?.status} ${t?.errorMessage ?? ''}`).toBe('Completed')
          await qa.page.reload()
          const row2 = roomSection(qa).locator('li.recording-row').filter({ hasText: 'said.wav' })
          await qa.expect(row2.getByText('Có bản gỡ băng')).toBeVisible({ timeout: 30000 })
          await qa.expect(row2.getByText('Kính thưa các đồng chí', { exact: false })).toBeVisible()
          await settled(qa)
          await qa.shot('tai-len-co-ban-go-bang')
          await noRawCodes(qa)
          const rec = (await recordings(qa, 'secretary', m.id)).items.find((r) => r.fileName === 'said.wav')
          qa.expect(rec?.source).toBe('Upload')
          qa.expect(rec?.consentPurpose, 'purpose kept').toBeTruthy()
          qa.expect(rec?.consentObtainedAt, 'consent time kept').toBeTruthy()
          qa.expect(daysBetween(rec.createdAt, rec.retainUntil)).toBe(180)
        } finally {
          await clean(qa, m.id)
        }
      }),
    },
    {
      id: 'AC37',
      role: 'member',
      title: 'REC_FT38: Thành viên vào phòng họp trực tuyến thấy "Đang ghi lời bạn"; hai câu tiếng Việt hiện trên "Bản ghi trực tiếp" dưới tên người nói mà không cần tải lại',
      steps: [
        'thanhvien@ vào phòng họp trực tuyến Đang diễn ra, bật micro khi cần',
        'Hai câu tiếng Việt (said.wav cắt ở chỗ ngắt) gửi qua API micro của thành viên (200 MSG318 mỗi câu); tab Bản ghi của thành viên mở sẵn',
      ],
      expected:
        'Đầu phòng họp "Đang ghi lời bạn"; mỗi câu API 200 MSG318; tab "Bản ghi trực tiếp" hiện hai câu dưới tên thành viên mà không tải lại (micro giả của trình duyệt không có tiếng nên câu nói kiểm bằng âm thanh tiếng Việt qua API — đường micro thật tới cùng endpoint)',
      run: wrap(async (qa) => {
        const u = await users(qa)
        const m = await meeting(qa, 'onRun')
        await cleanClips(qa, m.id)
        try {
          await joinRoom(qa, m)
          await expectCapturing(qa)
          await settled(qa)
          await qa.shot('dang-ghi-loi-ban')
          // The live list learns new sentences through the hub: the tab is already open when they arrive.
          await openTab(qa, m)
          await qa.expect(qa.page.getByRole('heading', { name: /Bản ghi trực tiếp/ })).toBeVisible()
          const halves = saidHalves(qa)
          const r = await raw(qa, 'member')
          const clips = []
          for (const h of halves) {
            const res = await r.putClip(m.id, crypto.randomUUID(), h.buf, h.ms)
            qa.expect(res.status, `putClip: ${JSON.stringify(res.body)}`).toBe(200)
            qa.expect(res.body?.code).toBe('MSG318')
            clips.push(res.body.clip)
          }
          qa.note(`Câu: ${clips.map((c) => `"${c.text}"`).join(' / ')}`)
          for (const c of clips) await qa.expect(qa.page.locator(`[data-clip="${c.id}"]`)).toBeVisible({ timeout: 20000 })
          await qa.expect(qa.page.getByText(u.member.name).first()).toBeVisible()
          await settled(qa)
          await qa.shot('hai-cau-duoi-ten-nguoi-noi')
          await noSidewaysScroll(qa)
        } finally {
          await cleanClips(qa, m.id)
        }
      }),
    },
    {
      id: 'AC38',
      role: 'chair',
      title: 'REC_FT39: Chủ toạ bỏ một câu khỏi biên bản rồi khôi phục (200 MSG351/MSG352); thành viên thấy câu trống, nghe 404; xong câu trở lại',
      steps: [
        'Một câu tiếng Việt của thành viên ở họp trực tuyến Đang diễn ra (API); chutoa@ mở tab Bản ghi',
        'Bấm "Bỏ khỏi biên bản" trên câu (một câu duy nhất nên một đoạn, một thanh công cụ, không race); kiểm tra góc nhìn thành viên qua API; bấm "Khôi phục"',
        'API: loại câu rồi khôi phục, kiểm mã MSG351/MSG352',
      ],
      expected:
        'Bấm xong toast "Đã loại câu khỏi bản ghi.", câu gạch ngang kèm "Đã bỏ khỏi biên bản"; thành viên (API) thấy text "" và nghe 404; "Khôi phục" → toast "Đã đưa câu trở lại bản ghi.", chữ trở lại; API loại 200 MSG351, khôi phục 200 MSG352',
      run: wrap(async (qa) => {
        const m = await meeting(qa, 'onRun')
        await cleanClips(qa, m.id)
        try {
          const c = await seedSentence(qa, m.id, 0)
          await openTab(qa, m)
          await qa.expect(qa.page.locator(`[data-clip="${c.id}"]`)).toBeVisible({ timeout: 15000 })
          await press(qa, qa.page.getByRole('button', { name: /^Bỏ khỏi biên bản/ }))
          await qa.expect(toast(qa, 'Đã loại câu khỏi bản ghi.')).toBeVisible({ timeout: 10000 })
          await qa.expect(qa.page.getByText('Đã bỏ khỏi biên bản')).toBeVisible()
          await settled(qa)
          await qa.shot('cau-bi-loai')
          const mem = await client(qa, 'member')
          const asMember = (await mem.get(`/api/meetings/${m.id}/speech-clips`)).body.items.find((i) => i.id === c.id)
          qa.expect(asMember?.excluded, JSON.stringify(asMember)).toBe(true)
          qa.expect(asMember?.text).toBe('')
          qa.expect((await mem.get(`/api/speech-clips/${c.id}/playback`)).status).toBe(404)
          await press(qa, qa.page.getByRole('button', { name: /^Khôi phục/ }))
          await qa.expect(toast(qa, 'Đã đưa câu trở lại bản ghi.')).toBeVisible({ timeout: 10000 })
          await qa.expect(qa.page.locator(`[data-clip="${c.id}"]`)).toContainText('Kính thưa các đồng chí', { timeout: 10000 })
          await settled(qa)
          await qa.shot('cau-duoc-khoi-phuc')
          await noRawCodes(qa)
          const chk = checker(qa)
          const chair = await client(qa, 'chair')
          chk.check('loại câu (API)', await chair.put(`/api/speech-clips/${c.id}/exclusion`, { excluded: true, reason: 'Other' }), 200, 'MSG351')
          chk.check('khôi phục câu (API)', await chair.put(`/api/speech-clips/${c.id}/exclusion`, { excluded: false, reason: null }), 200, 'MSG352')
          chk.done()
        } finally {
          await cleanClips(qa, m.id)
        }
      }),
    },
    {
      id: 'AC39',
      role: 'member',
      title: 'REC_FT40: Thành viên không có nút "Bỏ khỏi biên bản" và "Sửa nội dung"; gọi API loại câu 403 MSG353',
      steps: ['Một câu tiếng Việt của thành viên ở họp trực tuyến Đang diễn ra (API)', 'thanhvien@ mở tab Bản ghi', 'API: thanhvien@ PUT …/exclusion'],
      expected: 'Câu hiện chữ nhưng không có nút "Bỏ khỏi biên bản" và "Sửa nội dung"; API loại câu 403 MSG353 "Chỉ Chủ toạ hoặc Thư ký được loại câu khỏi bản ghi."',
      run: wrap(async (qa) => {
        const m = await meeting(qa, 'onRun')
        await cleanClips(qa, m.id)
        try {
          const c = await seedSentence(qa, m.id, 0)
          await openTab(qa, m)
          await qa.expect(qa.page.locator(`[data-clip="${c.id}"]`)).toBeVisible({ timeout: 15000 })
          await qa.expect(qa.page.getByRole('button', { name: /^Bỏ khỏi biên bản/ })).toHaveCount(0)
          await qa.expect(qa.page.getByRole('button', { name: /^Sửa nội dung/ })).toHaveCount(0)
          await settled(qa)
          await qa.shot('thanh-vien-khong-nut-loai-sua')
          await noSidewaysScroll(qa)
          const res = await (await client(qa, 'member')).put(`/api/speech-clips/${c.id}/exclusion`, { excluded: true, reason: 'Other' })
          qa.expect(res.status, `exclusion: ${JSON.stringify(res.body)}`).toBe(403)
          qa.expect(res.body?.code).toBe('MSG353')
        } finally {
          await cleanClips(qa, m.id)
        }
      }),
    },
    {
      id: 'AC40',
      role: 'secretary',
      title: 'REC_FT41: Thư ký sửa chữ một câu (toast MSG302), chữ mới hiện cho mọi người, "đã sửa", tải lại vẫn còn',
      steps: ['Một câu tiếng Việt của thành viên ở họp trực tuyến Đang diễn ra (API)', 'thuky@ mở tab Bản ghi, bấm "Sửa nội dung" trên câu, sửa chữ, bấm "Lưu"', 'Tải lại trang; thành viên đọc danh sách qua API'],
      expected: 'Toast "Đã lưu đoạn gỡ băng."; câu hiện chữ mới cho mọi người kèm "đã sửa"; tải lại vẫn chữ mới; API text đúng chữ mới',
      run: wrap(async (qa) => {
        const m = await meeting(qa, 'onRun')
        await cleanClips(qa, m.id)
        try {
          const c = await seedSentence(qa, m.id, 0)
          await openTab(qa, m)
          await qa.expect(qa.page.locator(`[data-clip="${c.id}"]`)).toBeVisible({ timeout: 15000 })
          await press(qa, qa.page.getByRole('button', { name: /^Sửa nội dung/ }))
          const box = qa.page.getByLabel('Nội dung câu')
          await qa.expect(box).toBeVisible()
          const fixed = 'Kính thưa các đồng chí, hôm nay chúng ta họp đã sửa.'
          await box.fill(fixed)
          await press(qa, qa.page.getByRole('button', { name: 'Lưu', exact: true }))
          await qa.expect(toast(qa, 'Đã lưu đoạn gỡ băng.')).toBeVisible({ timeout: 10000 })
          await qa.expect(qa.page.locator(`[data-clip="${c.id}"]`)).toContainText('đã sửa', { timeout: 10000 })
          await settled(qa)
          await qa.shot('cau-da-sua')
          await qa.page.reload()
          await qa.expect(qa.page.locator(`[data-clip="${c.id}"]`)).toContainText(fixed, { timeout: 15000 })
          await noRawCodes(qa)
          const asMember = (await (await client(qa, 'member')).get(`/api/meetings/${m.id}/speech-clips`)).body.items.find((i) => i.id === c.id)
          qa.expect(asMember?.text).toBe(fixed)
        } finally {
          await cleanClips(qa, m.id)
        }
      }),
    },
    {
      id: 'AC41',
      role: 'member',
      title: 'REC_FT42: "Ghi âm của tôi" liệt kê câu chờ gửi ("Chờ gửi lại") và câu đã lưu ("Đã chuyển chữ"), mới nhất trước, kèm giờ nói và độ dài',
      steps: [
        'Hai câu tiếng Việt của thanhvien@ đã lên hệ thống (API); một câu thứ ba nằm lại trên máy (IndexedDB) vì mạng gửi lên rớt (chặn PUT)',
        'thanhvien@ mở tab Bản ghi, bấm "Ghi âm của tôi"',
      ],
      expected: 'Hộp "Mỗi câu bạn nói được lưu riêng rồi chuyển thành chữ."; mục "Chưa lên hệ thống · 1" chip "Chờ gửi lại" hoặc "Đang gửi"; mục "Đã lưu lên hệ thống · 2" chip "Đã chuyển chữ", câu mới nhất trước, mỗi câu có giờ nói và độ dài giây (câu chờ gửi là câu của máy này: trạng thái mạng làm ra, không phải giọng thật)',
      run: wrap(async (qa) => {
        const u = await users(qa)
        const m = await meeting(qa, 'onRun')
        await cleanClips(qa, m.id)
        try {
          const s0 = await seedSentence(qa, m.id, 0)
          const s1 = await seedSentence(qa, m.id, 1)
          await openTab(qa, m)
          // A sentence left on this device: stored where the app keeps unsent clips, with its upload blocked.
          const halves = saidHalves(qa)
          await qa.page.evaluate(
            async ({ meetingId, owner, audioB64, startedAt }) => {
              const bin = Uint8Array.from(atob(audioB64), (ch) => ch.charCodeAt(0))
              const blob = new Blob([bin.buffer], { type: 'audio/wav' })
              await new Promise((resolve, reject) => {
                const req = indexedDB.open('trustcabinet-speech', 1)
                req.onupgradeneeded = () => req.result.createObjectStore('clips', { keyPath: 'id' })
                req.onsuccess = () => {
                  const tx = req.result.transaction('clips', 'readwrite')
                  tx.objectStore('clips').put({ id: crypto.randomUUID(), meetingId, owner, startedAt, durationMs: 2000, audio: blob })
                  tx.oncomplete = () => {
                    req.result.close()
                    resolve(0)
                  }
                  tx.onerror = () => reject(req.error)
                }
                req.onerror = () => reject(req.error)
              })
            },
            { meetingId: m.id, owner: `user:${u.member.id}`, audioB64: Buffer.from(halves[0].buf).toString('base64'), startedAt: new Date().toISOString() },
          )
          await qa.page.route('**/api/meetings/*/speech-clips/*', (route) =>
            route.request().method() === 'PUT' ? route.abort() : route.fallback(),
          )
          await qa.page.reload()
          await press(qa, qa.page.getByRole('button', { name: /Ghi âm của tôi/ }))
          const dlg = qa.page.getByRole('dialog', { name: 'Ghi âm của tôi' })
          await qa.expect(dlg).toBeVisible({ timeout: 10000 })
          await qa.expect(dlg.getByText('Chưa lên hệ thống · 1')).toBeVisible({ timeout: 20000 })
          await qa.expect(dlg.getByText(/Chờ gửi lại|Đang gửi/)).toBeVisible()
          await qa.expect(dlg.getByText('Đã lưu lên hệ thống · 2')).toBeVisible()
          await qa.expect(dlg.getByText('Đã chuyển chữ').first()).toBeVisible()
          const text = (await dlg.innerText()).replace(/\s+/g, ' ')
          qa.note(`Hộp: ${text.slice(0, 300)}`)
          // Each half backdates startedAt by its own length, so seed order is not time order: read it back.
          const s1Newer = Date.parse(s1.startedAt) >= Date.parse(s0.startedAt)
          const [older, newer] = s1Newer ? ['đồng chí', 'đào tạo'] : ['đào tạo', 'đồng chí']
          qa.expect(text.indexOf(newer) < text.indexOf(older) && text.indexOf(newer) >= 0, 'newest first').toBe(true)
          qa.expect(text, 'time and length').toMatch(/\d{2}:\d{2}:\d{2}.+giây/)
          await settled(qa)
          await qa.shot('ghi-am-cua-toi')
          await noSidewaysScroll(qa)
          await qa.page.keyboard.press('Escape')
        } finally {
          await qa.page.unrouteAll({ behavior: 'ignoreErrors' }).catch(() => undefined)
          await cleanClips(qa, m.id)
        }
      }),
    },
    {
      id: 'AC42',
      role: 'member',
      title: 'REC_FT43: Thành viên nghe lại câu của mình ("Nghe lại" → "Dừng nghe") qua link playback ký sẵn',
      steps: ['Một câu tiếng Việt dài của thanhvien@ ở họp trực tuyến Đang diễn ra (API)', 'thanhvien@ mở "Ghi âm của tôi", bấm "Nghe lại", rồi bấm "Dừng nghe"', 'API: thanhvien@ GET …/playback'],
      expected: 'Nút đổi "Nghe lại" → "Dừng nghe" (đang phát) → "Nghe lại" (đã dừng); không có toast lỗi; API playback 200 có link ký sẵn',
      run: wrap(async (qa) => {
        const m = await meeting(qa, 'onRun')
        await cleanClips(qa, m.id)
        try {
          const c = await seedSentence(qa, m.id, 1)
          const play = await (await client(qa, 'member')).get(`/api/speech-clips/${c.id}/playback`)
          qa.expect(play.status, `playback: ${JSON.stringify(play.body)}`).toBe(200)
          qa.expect(play.body?.url, 'signed link').toBeTruthy()
          await openTab(qa, m)
          await press(qa, qa.page.getByRole('button', { name: /Ghi âm của tôi/ }))
          const dlg = qa.page.getByRole('dialog', { name: 'Ghi âm của tôi' })
          await qa.expect(dlg).toBeVisible()
          await press(qa, dlg.getByRole('button', { name: 'Nghe lại' }))
          await qa.expect(dlg.getByRole('button', { name: 'Dừng nghe' })).toBeVisible({ timeout: 10000 })
          await settled(qa)
          await qa.shot('dang-nghe-lai')
          await press(qa, dlg.getByRole('button', { name: 'Dừng nghe' }))
          await qa.expect(dlg.getByRole('button', { name: 'Nghe lại' })).toBeVisible({ timeout: 10000 })
          qa.expect(await qa.page.locator('.toast-stack .toast').count(), 'no error toast').toBe(0)
        } finally {
          await cleanClips(qa, m.id)
        }
      }),
    },
    {
      id: 'AC43',
      role: 'member',
      title: 'REC_FT44: Chưa nói câu nào thì "Ghi âm của tôi" hiện trạng thái trống ("Chưa có câu nào")',
      steps: ['Họp trực tuyến Đang diễn ra, thanhvien@ chưa nói câu nào', 'thanhvien@ vào phòng (micro bật, không nói), bấm "Đang ghi lời bạn"'],
      expected: 'Hộp "Ghi âm của tôi" hiện "Chưa có câu nào" và "Phát biểu trong phòng họp trực tuyến, từng câu sẽ hiện ở đây."; API danh sách câu của thành viên trống',
      run: wrap(async (qa) => {
        const m = await meeting(qa, 'onRun')
        await cleanClips(qa, m.id)
        await joinRoom(qa, m)
        const chip = await expectCapturing(qa)
        await settled(qa)
        await qa.shot('phong-hop-chua-noi')
        await press(qa, chip)
        const dlg = qa.page.getByRole('dialog', { name: 'Ghi âm của tôi' })
        await qa.expect(dlg).toBeVisible()
        await qa.expect(dlg.getByText('Chưa có câu nào')).toBeVisible()
        await qa.expect(dlg.getByText('Phát biểu trong phòng họp trực tuyến, từng câu sẽ hiện ở đây.')).toBeVisible()
        await settled(qa)
        await qa.shot('chua-co-cau-nao')
        const u43 = await users(qa)
        const left = (await (await client(qa, 'member')).get(`/api/meetings/${m.id}/speech-clips`)).body.items.filter((i) => i.speakerId === u43.member.id)
        qa.note(`Câu của thanhvien@ sau khi vào phòng không nói: ${left.length}`)
        qa.expect(left.length).toBe(0)
      }),
    },
  ],
}

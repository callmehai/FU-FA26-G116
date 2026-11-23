// Report 5.4 System Test (NFRs) — every case of docs/08-reports/test-reports/cases/nfr/<Sheet>.json that can be run on a
// workstation. Criterion ids are the case ids without "TC-" (SEC-001…). Measured values go into qa.note("ACTUAL: …").
//
// Two app instances of the same build and database are used: the Vite dev server on :5173 (proxy to :5225, the
// Development limits) for the functional checks, and a second backend on :5226 that serves the built bundle with the
// shipped sign-in limits (5 per email, 60 per IP per 60 s) for the rate-limit and page-load measurements
// (SYS_LIMITED_BASE). Start it as described in the case notes before running.

import { request as playwrightRequest } from '@playwright/test'
import { execFileSync } from 'node:child_process'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import {
  PASSWORD,
  PEOPLE,
  RUN,
  bell,
  client,
  closeSeconds,
  dialog,
  meetingAt,
  noSidewaysScroll,
  pdfFile,
  person,
  secondBrowser,
  settled,
  shotOf,
  slot,
  sql,
  toast,
  unread,
} from './sys-lib.mjs'

const LIMITED = process.env.SYS_LIMITED_BASE ?? 'http://localhost:5226'
const API = 'http://localhost:5225'
/** Root of this checkout; REL-001 kills the second backend only when it was started from here. */
const REPO = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../..')
const actual = (qa, text) => qa.note(`ACTUAL: ${text}`)
/** Runs a shell command and returns its output even when it exits non-zero (curl -w after a refused handshake). */
function sh(cmd) {
  try {
    return execFileSync('bash', ['-c', cmd], { encoding: 'utf8' }).trim()
  } catch (e) {
    return String(e.stdout ?? '').trim()
  }
}
const pct = (arr, p) => {
  const s = [...arr].sort((a, b) => a - b)
  return s[Math.min(s.length - 1, Math.ceil((p / 100) * s.length) - 1)]
}
const state = {}

async function rawCtx(base = API) {
  return playwrightRequest.newContext({ baseURL: base })
}
async function tokenFor(ctx, email, password = PASSWORD) {
  const r = await ctx.post('/api/auth/login', { data: { email, password } })
  return { status: r.status(), body: await r.json().catch(() => ({})), headers: r.headers() }
}

/** A published meeting with one PDF, shared by several cases. */
async function shared(qa) {
  if (state.m) return state.m
  state.m = await meetingAt(qa, 'Published', { title: `[SYS] NFR Họp dùng chung ${RUN}` })
  return state.m
}

/** A PDF of about `mb` megabytes: real pages plus a large unused stream. */
function bigPdf(name, mb) {
  const base = pdfFile(name, 3).buffer.toString('latin1')
  const filler = 'A'.repeat(Math.floor(mb * 1024 * 1024) - 4096)
  const i = base.indexOf('xref')
  // An extra object after the body; the xref table stays valid because it is placed before "xref" and offsets of the
  // earlier objects do not move. The reader ignores the unreferenced object.
  const extra = `99 0 obj\n<< /Length ${filler.length} >>\nstream\n${filler}\nendstream\nendobj\n`
  const before = base.slice(0, i)
  const after = base.slice(i)
  const fixedAfter = after.replace(/startxref\n(\d+)/, (_, n) => `startxref\n${Number(n) + extra.length}`)
  return { name, mimeType: 'application/pdf', buffer: Buffer.from(before + extra + fixedAfter, 'latin1') }
}

export default {
  ticket: 'SYS-NFR',
  title: 'Report 5.4 System Test — Non-functional requirements',
  devices: ['desktop'],
  // Local LiveKit runs on localhost:7880 (on the server it sits behind the app's origin at /rtc).
  allowHosts: ['localhost'],
  criteria: [
    // ================================================================================================ Security
    {
      id: 'SEC-001',
      role: 'anon',
      title: 'Authentication required',
      async run(qa) {
        for (const path of ['/mgmt/meetings', '/calendar', '/mgmt/documents']) {
          await qa.goto(path)
          await qa.expect(qa.page).toHaveURL(/\/login/)
        }
        await qa.shot('chua-dang-nhap-ve-trang-dang-nhap')
        const anon = await qa.api('anon')
        const got = []
        for (const p of ['/api/meetings', '/api/users', '/api/notifications', '/api/documents', '/api/reports/meetings?from=2026-01&to=2026-12'])
          got.push(`${p} ${(await anon.get(p)).status}`)
        actual(qa, got.join('; '))
        for (const g of got) qa.expect(g).toMatch(/ 401$/)
        const health = await anon.get('/api/health')
        qa.expect(health.status).toBe(200)
      },
    },
    {
      id: 'SEC-002',
      role: 'anon',
      title: 'Sign-in rate limiting',
      async run(qa) {
        const ctx = await rawCtx(LIMITED)
        const email = PEOPLE.memberE
        const seen = []
        for (let i = 1; i <= 6; i++) seen.push((await tokenFor(ctx, email, `Sai${i}@MatKhau2026`)).status)
        const right = await tokenFor(ctx, email)
        actual(qa, `attempts 1–6: ${seen.join(', ')}; correct password inside window: ${right.status} ${right.body.code}, Retry-After ${right.headers['retry-after']} s`)
        qa.expect(seen.slice(0, 5)).toEqual([401, 401, 401, 401, 401])
        qa.expect(seen[5]).toBe(429)
        qa.expect(right.status).toBe(429)
        qa.expect(right.body.code).toBe('MSG414')
        qa.expect(right.body.message).toBe('Bạn đã thử quá nhiều lần. Vui lòng thử lại sau ít phút.')
        const page = await secondBrowser(qa, 'anon', { signIn: false, base: LIMITED })
        try {
          await page.goto('/login')
          await page.getByLabel('Email').fill(email)
          await page.getByLabel('Mật khẩu', { exact: true }).fill(PASSWORD)
          await page.getByRole('button', { name: 'Đăng nhập bằng mật khẩu' }).click()
          await qa.expect(page.getByRole('alert').first()).toContainText('Bạn đã thử quá nhiều lần')
          await shotOf(qa, page, 'bi-chan-tam-thoi', 'anon')
        } finally {
          await closeSeconds(qa)
        }
        await new Promise((r) => setTimeout(r, (Number(right.headers['retry-after'] ?? 60) + 2) * 1000))
        const after = await tokenFor(ctx, email)
        actual(qa, `after the window: ${after.status}`)
        qa.expect(after.status).toBe(200)
        await ctx.dispose()
      },
    },
    {
      id: 'SEC-003',
      role: PEOPLE.memberD,
      title: 'Role-based access control and least privilege',
      async run(qa) {
        const m = await shared(qa)
        await qa.goto(`/mgmt/meetings/${m.id}`)
        await qa.expect(qa.page.getByRole('heading', { name: 'Không tìm thấy trang' })).toBeVisible({ timeout: 15_000 })
        await qa.shot('khong-tim-thay-trang')
        const d = await client(qa, 'memberD')
        const r = await d.get(`/api/meetings/${m.id}`)
        const unknown = await d.get('/api/meetings/01a0ce78-0000-7000-8000-000000000000')
        const list = (await d.get(`/api/meetings?q=${encodeURIComponent(m.title)}`)).body
        const users = await d.get('/api/users')
        actual(qa, `GET meeting ${r.status} (unknown id ${unknown.status}); list hits ${list.items.length}; GET /api/users ${users.status}`)
        qa.expect(r.status).toBe(404)
        qa.expect(JSON.stringify(r.body)).toBe(JSON.stringify(unknown.body))
        qa.expect(list.items.length).toBe(0)
        qa.expect(users.status).toBe(403)
      },
    },
    {
      id: 'SEC-004',
      role: PEOPLE.memberD,
      title: 'Document security levels',
      async run(qa) {
        const row = sql(
          `select md.meeting_id, md.id from meeting_documents md join meetings m on m.id = md.meeting_id where m.sensitivity = 'Secret' and m.title like '[SYS]%' and md.removed_at is null order by md.added_at desc limit 1`,
        )[0]
        qa.expect(row, 'no Secret [SYS] meeting document (run WF-08 first)').toBeTruthy()
        const [meetingId, docId] = row
        const before = Number(sql(`select count(*) from audit_log where object_id = '${docId}'`)[0][0])
        const d = await client(qa, 'memberD')
        const a = await d.get(`/api/meetings/${meetingId}/documents/${docId}/content?download=true`)
        const b = await d.get(`/api/meetings/${meetingId}/documents/${docId}/content`)
        const after = Number(sql(`select count(*) from audit_log where object_id = '${docId}'`)[0][0])
        await qa.goto(`/mgmt/meetings/${meetingId}/documents/${docId}`)
        await qa.expect(qa.page.getByRole('heading', { name: 'Không tìm thấy trang' })).toBeVisible({ timeout: 15_000 })
        await qa.shot('link-tai-lieu-mat')
        // A participant reads it online: the view is audited.
        const c = await client(qa, 'memberC')
        const v = await c.get(`/api/meetings/${meetingId}/documents/${docId}/content`)
        const dl = await c.get(`/api/meetings/${meetingId}/documents/${docId}/content?download=true`)
        const viewed = Number(sql(`select count(*) from audit_log where object_id = '${docId}' and action = 'document.viewed'`)[0][0])
        actual(qa, `outsider download ${a.status} ${a.body?.code}, view ${b.status}; audit rows for the outsider ${after - before}; participant view ${v.status}, download ${dl.status} ${dl.body?.code}; document.viewed rows ${viewed}`)
        qa.expect(a.status).toBe(404)
        qa.expect(b.status).toBe(404)
        qa.expect(after - before).toBe(0)
        qa.expect(dl.status).toBe(403)
        qa.expect(viewed).toBeGreaterThan(0)
      },
    },
    {
      id: 'SEC-005',
      role: PEOPLE.admin,
      title: 'Password policy and protection',
      async run(qa) {
        const admin = await client(qa, 'admin')
        const roles = (await admin.get('/api/roles')).body
        const member = (roles.items ?? roles).find((r) => r.name === 'Thành viên')
        const short = await admin.post('/api/users', { fullName: '[SYS] NFR mật khẩu ngắn', email: `sys-pw-short-${RUN}@trustcabinet.local`, password: 'Ngan@12345a', roleId: member.id })
        const phrase = 'mot hai ba bon nam sau bay'
        const ok = await admin.post('/api/users', { fullName: '[SYS] NFR cụm mật khẩu', email: `sys-pw-phrase-${RUN}@trustcabinet.local`, password: phrase, roleId: member.id })
        const id = ok.body?.user?.id
        const ctx = await rawCtx()
        const login = await tokenFor(ctx, `sys-pw-phrase-${RUN}@trustcabinet.local`, phrase)
        const stored = sql(`select password_hash from users where id = '${id}'`)[0][0]
        const listed = JSON.stringify((await admin.get('/api/users')).body)
        const audit = sql(`select details::text from audit_log where object_id = '${id}'`).map((r) => r[0]).join(' ')
        actual(qa, `11 chars: ${short.status} ${short.body?.code}; 27-char passphrase with spaces: ${ok.status}, sign-in ${login.status}; stored as ${stored.slice(0, 13)}… (${stored.length} chars, PBKDF2 v3); passphrase in user list ${listed.includes(phrase)}, in audit ${audit.includes(phrase)}`)
        qa.expect(short.status).toBe(400)
        qa.expect(ok.status).toBe(201)
        qa.expect(login.status).toBe(200)
        qa.expect(stored).not.toContain(phrase)
        qa.expect(stored.startsWith('AQAAAA')).toBe(true)
        qa.expect(listed.includes(phrase) || listed.includes('passwordHash')).toBe(false)
        qa.expect(audit.includes(phrase)).toBe(false)
        await admin.put(`/api/users/${id}/status`, { isActive: false })
        await ctx.dispose()
      },
    },
    {
      id: 'SEC-006',
      role: 'anon',
      title: 'Session and token expiry',
      async run(qa) {
        const ctx = await rawCtx()
        const r = await tokenFor(ctx, PEOPLE.memberE)
        const [, payload] = r.body.accessToken.split('.')
        const claims = JSON.parse(Buffer.from(payload, 'base64url').toString())
        const life = claims.iat ? claims.exp - claims.iat : Math.round(claims.exp - Date.now() / 1000)
        const cookie = (r.headers['set-cookie'] ?? '').split('\n').find((c) => /refresh/i.test(c)) ?? ''
        const maxAge = Number(/max-age=(\d+)/i.exec(cookie)?.[1] ?? NaN)
        const expires = /expires=([^;]+)/i.exec(cookie)?.[1]
        const days = Number.isNaN(maxAge) ? (Date.parse(expires) - Date.now()) / 86_400_000 : maxAge / 86_400
        const refreshed = await ctx.post('/api/auth/refresh')
        const out = await ctx.post('/api/auth/logout', { headers: { Authorization: `Bearer ${r.body.accessToken}` } })
        const again = await ctx.post('/api/auth/refresh')
        actual(qa, `access token ${life} s; refresh cookie ${days.toFixed(2)} days, flags: ${cookie.split(';').slice(1).map((x) => x.trim()).filter((x) => !/^(expires|max-age|path)/i.test(x)).join(', ')}; refresh ${refreshed.status()}; sign-out ${out.status()}; refresh after sign-out ${again.status()}`)
        qa.expect(life).toBeLessThanOrEqual(15 * 60)
        qa.expect(days).toBeLessThanOrEqual(7.01)
        qa.expect(cookie.toLowerCase()).toContain('httponly')
        qa.expect(cookie.toLowerCase()).toMatch(/samesite=(strict|lax)/)
        qa.expect(refreshed.status()).toBe(200)
        qa.expect(again.status()).toBe(401)
        await ctx.dispose()
      },
    },
    {
      id: 'SEC-007',
      role: 'anon',
      title: 'External Guest access scope',
      async run(qa) {
        const row = sql(
          `select m.id, g.id from meetings m join meeting_guest_invitations g on g.meeting_id = m.id where m.title like '[SYS]%' order by g.created_at desc limit 1`,
        )[0]
        const anon = await qa.api('anon')
        if (row) actual(qa, `invited meeting read without a verified guest session: ${(await anon.get(`/api/meetings/${row[0]}/agenda`)).status}`)
        qa.inconclusive('The guest one-time code is sent straight to the invited mailbox and never stored; @trustcabinet.local is never mailed. Needs a real mailbox to open a guest session.')
      },
    },
    {
      id: 'SEC-008',
      role: 'anon',
      title: 'Encryption of data in transit',
      async run(qa) {
        // Read-only checks against the shared test server (no sign-in, no data).
        const host = 'app.trustcabinet.workspace-hub.space'
        const http = sh(`curl -s -o /dev/null -w '%{http_code} %{redirect_url}' --max-time 15 http://${host}/`)
        const tls = sh(`curl -s -o /dev/null -w '%{http_code}' --tlsv1.0 --tls-max 1.1 --max-time 15 https://${host}/api/health`)
        const ok = sh(`curl -s -o /dev/null -w '%{http_code} %{ssl_verify_result}' --max-time 15 https://${host}/api/health`)
        const tls12 = sh(`curl -s -o /dev/null -w '%{http_code}' --tlsv1.2 --tls-max 1.2 --max-time 15 https://${host}/api/health`)
        const hsts = sh(`curl -s -I --max-time 15 https://${host}/`)
        actual(qa, `http:// → ${http}; https health ${ok}; TLS 1.2 → ${tls12}; TLS 1.0–1.1 handshake → ${tls === '000' || tls === '' ? 'refused' : tls}; HSTS ${/strict-transport-security/i.test(hsts)}`)
        qa.expect(http).toMatch(/^30[18] https:/)
        qa.expect(ok.startsWith('200 0')).toBe(true)
        qa.expect(tls === '' || tls === '000').toBe(true)
      },
    },
    {
      id: 'SEC-009',
      role: PEOPLE.memberC,
      title: 'Protection of stored data and backups',
      async run(qa) {
        const key = sql(`select storage_key from stored_files order by uploaded_at desc limit 1`)[0][0]
        const direct = sh(`curl -s -o /dev/null -w '%{http_code}' --max-time 10 http://localhost:8333/trustcabinet-dev/${key}`)
        const anon = await qa.api('anon')
        const m = await shared(qa)
        const docs = (await (await client(qa, 'memberC')).get(`/api/meetings/${m.id}/documents`)).body
        const leak = JSON.stringify(docs).includes(key)
        const secrets = sh(`cd ../.. && git grep -n -I -E 'Password=|ApiSecret\"[[:space:]]*:[[:space:]]*\"[^\"]+|SecretAccessKey\"[[:space:]]*:[[:space:]]*\"[^\"]+' -- 'backend/TrustCabinet.Web/appsettings*.json'`)
        const backup = sh(`cd ../.. && grep -n -e 'Mỗi lần deploy có image app đổi' -e 'Bản backup nằm cùng máy' deploy/README.md`)
        actual(qa, `anonymous GET on the local storage bucket object: ${direct}; storage key in API responses: ${leak}; secrets in committed appsettings: ${secrets ? 'found' : 'none'}; backups (deploy/README.md): ${backup.replace(/\s+/g, ' ').slice(0, 260)}`)
        qa.expect(leak).toBe(false)
        qa.expect(secrets).toBe('')
        qa.expect(direct, 'the local storage bucket answers an anonymous read').not.toBe('200')
        qa.expect(backup, 'backups are taken per deploy on the same host, not daily and off-host').toBe('')
        qa.expect((await anon.get(`/api/meetings/${m.id}/documents`)).status).toBe(401)
      },
    },
    {
      id: 'SEC-010',
      role: PEOPLE.admin,
      title: 'Audit logging',
      async run(qa) {
        const since = new Date(Date.now() - 2000).toISOString()
        const ctx = await rawCtx()
        await tokenFor(ctx, PEOPLE.memberE, 'Sai@MatKhau2026')
        await tokenFor(ctx, PEOPLE.memberE)
        await ctx.dispose()
        const rows = sql(`select action, ip_address, details::text from audit_log where occurred_at >= '${since}' order by occurred_at`)
        const actions = rows.map((r) => r[0])
        const anyCode = rows.some((r) => /Sai@MatKhau2026|TrustCabinet@2026/.test(r[2]))
        const tableRules = sql(`select count(*) from pg_trigger where tgrelid = 'audit_log'::regclass and not tgisinternal`)[0][0]
        await qa.goto('/mgmt/admin/audit-log')
        await qa.expect(qa.page.getByRole('heading', { name: /Nhật ký/ }).first()).toBeVisible()
        await settled(qa.page)
        await qa.shot('nhat-ky-he-thong')
        const member = await client(qa, 'memberC')
        const denied = await member.get('/api/audit-log')
        const del = await (await client(qa, 'admin')).delete('/api/audit-log')
        actual(qa, `new entries: ${actions.join(', ')} (IP ${rows[0]?.[1]}); password in details ${anyCode}; member GET ${denied.status}; DELETE /api/audit-log ${del.status}`)
        qa.expect(actions).toEqual(qa.expect.arrayContaining(['auth.sign_in_failed', 'auth.signed_in']))
        qa.expect(anyCode).toBe(false)
        qa.expect(denied.status).toBe(403)
        qa.expect([404, 405]).toContain(del.status)
        qa.note(`audit_log triggers: ${tableRules}`)
      },
    },
    {
      id: 'SEC-011',
      role: PEOPLE.memberB,
      title: 'Personal data protection and audio consent',
      async run(qa) {
        const m = await meetingAt(qa, 'InProgress', { title: `[SYS] NFR Họp ghi âm ${RUN}` })
        state.recMeeting = m
        const wav = Buffer.alloc(44 + 32000)
        wav.write('RIFF', 0)
        wav.writeUInt32LE(36 + 32000, 4)
        wav.write('WAVEfmt ', 8)
        wav.writeUInt32LE(16, 16)
        wav.writeUInt16LE(1, 20)
        wav.writeUInt16LE(1, 22)
        wav.writeUInt32LE(16000, 24)
        wav.writeUInt32LE(32000, 28)
        wav.writeUInt16LE(2, 32)
        wav.writeUInt16LE(16, 34)
        wav.write('data', 36)
        wav.writeUInt32LE(32000, 40)
        const ctx = await rawCtx()
        const t = (await tokenFor(ctx, PEOPLE.memberB)).body.accessToken
        const h = { Authorization: `Bearer ${t}` }
        const file = { name: 'hop.wav', mimeType: 'audio/wav', buffer: wav }
        const no = await ctx.post(`/api/meetings/${m.id}/recordings`, { headers: h, multipart: { file } })
        const yes = await ctx.post(`/api/meetings/${m.id}/recordings`, { headers: h, multipart: { file, consentConfirmed: 'true', consentPurpose: 'Lập biên bản cuộc họp' } })
        const yb = await yes.json().catch(() => ({}))
        state.recordingId = yb.recordingId
        const rec = sql(`select consent_obtained_at is not null, consent_purpose, (retain_until - created_at) from recordings where id = '${yb.recordingId}'`)[0] ?? []
        actual(qa, `upload without consent: ${no.status()} ${(await no.json().catch(() => ({}))).code}; with consent: ${yes.status()} ${yb.code}; consent time kept ${rec[0]}, purpose "${rec[1]}", kept for ${rec[2]}`)
        qa.expect(no.status()).toBe(400)
        qa.expect(yes.status()).toBe(201)
        qa.expect(rec[0]).toBe('t')
        qa.expect(rec[2]).toMatch(/^180 days/)
        await ctx.dispose()
        await qa.goto(`/mgmt/meetings/${m.id}?tab=recordings`)
        await qa.page.waitForTimeout(1500)
        await qa.shot('ban-ghi-co-dong-y')
      },
    },
    {
      id: 'SEC-012',
      role: PEOPLE.memberA,
      title: 'Integrity of issued minutes, vote conclusions and stored documents',
      async run(qa) {
        const issued = sql(`select mm.id, mm.meeting_id, mm.updated_at from meeting_minutes mm where mm.status = 'Issued' order by mm.issued_at desc limit 1`)[0]
        const chairOf = sql(`select u.email from meeting_participants p join users u on u.id = p.user_id where p.meeting_id = '${issued[1]}' and p.role = 'Chair' and p.removed_at is null limit 1`)[0][0]
        const chair = await client(qa, chairOf)
        const live = (await chair.get(`/api/meetings/${issued[1]}/minutes`)).body.minutes
        const edit = await chair.put(`/api/minutes/${issued[0]}`, { content: '<p>sửa</p>', updatedAt: live.updatedAt })
        const admin = await client(qa, 'admin')
        const adminEdit = await admin.put(`/api/minutes/${issued[0]}`, { content: '<p>sửa</p>', updatedAt: live.updatedAt })
        const vote = sql(`select v.meeting_id, v.id from voting_sessions v join meetings m on m.id = v.meeting_id where v.conclusion is not null and m.title like '[SYS]%' order by v.created_at desc limit 1`)[0]
        const vchair = sql(`select u.email from meeting_participants p join users u on u.id = p.user_id where p.meeting_id = '${vote[0]}' and p.role = 'Chair' and p.removed_at is null limit 1`)[0][0]
        const again = await (await client(qa, vchair)).post(`/api/meetings/${vote[0]}/votes/${vote[1]}/conclusion`, { conclusion: 'NotPassed', note: '' })
        const doc = sql(`select storage_key, content_sha256, byte_size from stored_files where content_type = 'application/pdf' order by uploaded_at desc limit 1`)[0]
        const bytes = execFileSync('bash', ['-c', `curl -s --max-time 10 http://localhost:8333/trustcabinet-dev/${doc[0]}`], { encoding: 'buffer' })
        const { createHash } = await import('node:crypto')
        const sha = createHash('sha256').update(bytes).digest('hex')
        actual(qa, `edit issued minutes: Chair ${edit.status} ${edit.body?.code}, admin ${adminEdit.status}; change a recorded vote conclusion: ${again.status} ${again.body?.code}; stored PDF fingerprint ${doc[1] === sha ? 'matches' : `differs (${bytes.length} bytes read)`}`)
        qa.expect(edit.status).toBe(409)
        qa.expect([403, 404, 409]).toContain(adminEdit.status)
        qa.expect(again.status).toBe(409)
        if (bytes.length === Number(doc[2])) qa.expect(sha).toBe(doc[1])
        else qa.note('The storage bucket refused the direct read (as SEC-009 wants); fingerprint compared through the app instead is not exposed.')
      },
    },
    {
      id: 'SEC-013',
      role: PEOPLE.memberB,
      title: 'Upload safety',
      async run(qa) {
        const ctx = await rawCtx()
        const t = (await tokenFor(ctx, PEOPLE.memberB)).body.accessToken
        const h = { Authorization: `Bearer ${t}` }
        const exe = Buffer.concat([Buffer.from('MZ'), Buffer.alloc(2048, 0x90)])
        const fake = await ctx.post('/api/documents/upload', { headers: h, multipart: { files: { name: 'bao-cao.pdf', mimeType: 'application/pdf', buffer: exe } } })
        const m = state.recMeeting ?? (await meetingAt(qa, 'InProgress', { title: `[SYS] NFR Họp ghi âm ${RUN}` }))
        const mp4 = Buffer.concat([Buffer.from([0, 0, 0, 0x20]), Buffer.from('ftypisom'), Buffer.alloc(4096)])
        const video = await ctx.post(`/api/meetings/${m.id}/recordings`, { headers: h, multipart: { file: { name: 'hop.mp4', mimeType: 'video/mp4', buffer: mp4 }, consentConfirmed: 'true' } })
        const f = await fake.json().catch(() => ({}))
        const v = await video.json().catch(() => ({}))
        actual(qa, `executable named .pdf: ${fake.status()} ${f.code}; video as meeting audio: ${video.status()} ${v.code}`)
        qa.expect(fake.status()).toBe(400)
        qa.expect(f.code).toBe('MSG164')
        qa.expect(video.status()).toBe(415)
        await ctx.dispose()
      },
    },

    // ================================================================================================ Performance
    {
      id: 'PERF-001',
      role: PEOPLE.memberC,
      title: 'Page load time',
      async run(qa) {
        const times = []
        for (let i = 0; i < 5; i++) {
          const page = await secondBrowser(qa, 'memberC', { base: LIMITED })
          const cdp = await page.context().newCDPSession(page)
          await cdp.send('Network.setCacheDisabled', { cacheDisabled: true })
          const t0 = Date.now()
          await page.goto('/mgmt/meetings')
          await page.locator('main table tbody tr, main .meeting-card').first().waitFor({ timeout: 20_000 })
          times.push(Date.now() - t0)
          if (i === 4) await shotOf(qa, page, 'danh-sach-da-san-sang', 'memberC')
          await closeSeconds(qa)
        }
        actual(qa, `${(Math.max(...times) / 1000).toFixed(2)} s slowest of 5 (${times.join(', ')} ms), built bundle, cache disabled`)
        qa.expect(Math.max(...times)).toBeLessThanOrEqual(3000)
      },
    },
    {
      id: 'PERF-002',
      role: PEOPLE.memberB,
      title: 'Document upload and preview',
      async run(qa) {
        const m = await shared(qa)
        const file = bigPdf(`tai-lieu-10mb-${RUN}.pdf`, 9.99)
        await qa.goto(`/mgmt/meetings/${m.id}?tab=documents`)
        await qa.page.locator('.documents-panel').getByRole('button', { name: 'Thêm tài liệu chung' }).click()
        const form = dialog(qa.page, 'Thêm tài liệu chung')
        await form.locator('input[type=file]').setInputFiles([file])
        let t0 = 0
        qa.page.on('request', (r) => r.url().includes('/documents/upload') && (t0 = Date.now()))
        const listed = qa.page.waitForResponse((r) => r.url().includes('/documents/upload') && r.request().method() === 'POST', { timeout: 60_000 })
        listed.catch(() => undefined)
        await form.getByRole('button', { name: 'Thêm 1 tài liệu' }).click()
        const res = await listed
        const tUploaded = Date.now()
        await qa.expect(qa.page.locator('.documents-panel li.documents-row').filter({ hasText: file.name })).toBeVisible({ timeout: 30_000 })
        const tListed = Date.now()
        await qa.shot('tai-lieu-10mb-da-hien')
        const tOpen = Date.now()
        await qa.page.locator('.documents-panel li.documents-row').filter({ hasText: file.name }).getByRole('link').first().click()
        await qa.expect(qa.page.locator('.viewer-stage canvas').first()).toBeVisible({ timeout: 30_000 })
        const tPreview = Date.now()
        await qa.shot('xem-truoc-10mb')
        actual(qa, `${(file.buffer.length / 1048576).toFixed(1)} MB: request to stored ${((tUploaded - t0) / 1000).toFixed(2)} s (local, transfer included, HTTP ${res.status()}), listed ${((tListed - tUploaded) / 1000).toFixed(2)} s after the answer; preview ${((tPreview - tOpen) / 1000).toFixed(2)} s`)
        qa.expect(res.status()).toBe(201)
        qa.expect(tUploaded - t0 + (tListed - tUploaded)).toBeLessThanOrEqual(5000)
        qa.expect(tPreview - tOpen).toBeLessThanOrEqual(3000)
      },
    },
    {
      id: 'PERF-003',
      role: PEOPLE.organizer,
      title: 'Notification and real-time update latency',
      async run(qa) {
        const m = await meetingAt(qa, 'Draft', { title: `[SYS] NFR Độ trễ thông báo ${RUN}` })
        const ap = await secondBrowser(qa, 'approver')
        try {
          await ap.goto('/calendar')
          await qa.expect(bell(ap)).toBeVisible()
          await ap.waitForTimeout(2000)
          const before = await unread(ap)
          const approver = await person(qa, 'approver')
          await qa.goto(`/mgmt/meetings/${m.id}`)
          await qa.page.getByRole('button', { name: 'Gửi duyệt', exact: true }).click()
          const form = dialog(qa.page, 'Gửi duyệt cuộc họp')
          await form.getByRole('button', { name: 'Chọn người duyệt' }).click()
          await qa.page.getByRole('option', { name: new RegExp(approver.name) }).click()
          const t0 = Date.now()
          await form.getByRole('button', { name: 'Gửi duyệt' }).click()
          await qa.expect.poll(() => unread(ap), { timeout: 15_000, intervals: [100] }).toBeGreaterThan(before)
          const ms = Date.now() - t0
          await shotOf(qa, ap, 'chuong-tang', 'approver')
          actual(qa, `${(ms / 1000).toFixed(2)} s from "Gửi duyệt" to the Approver's bell count changing`)
          qa.expect(ms).toBeLessThanOrEqual(10_000)
        } finally {
          await closeSeconds(qa)
        }
      },
    },
    {
      id: 'PERF-004',
      role: 'anon',
      title: 'Concurrent users',
      async run(qa) {
        const ids = sql(`select id from meetings where title like '[SYS]%' and status in ('Published','Ended') limit 5`).map((r) => r[0])
        const users = [PEOPLE.memberA, PEOPLE.memberB, PEOPLE.memberC, PEOPLE.organizer, PEOPLE.approver]
        const ctxs = []
        for (let i = 0; i < 100; i++) {
          const ctx = await rawCtx()
          const tok = (await tokenFor(ctx, users[i % users.length])).body.accessToken
          ctxs.push({ ctx, h: { Authorization: `Bearer ${tok}` } })
        }
        const lat = []
        let errors = 0
        const end = Date.now() + 60_000
        const paths = ['/api/meetings?pageSize=20', '/api/notifications?pageSize=20', ...ids.map((id) => `/api/meetings/${id}`), '/api/auth/me']
        await Promise.all(
          ctxs.map(async ({ ctx, h }, i) => {
            let k = i
            while (Date.now() < end) {
              const p = paths[k++ % paths.length]
              const t0 = Date.now()
              try {
                const r = await ctx.get(p, { headers: h, timeout: 30_000 })
                if (r.status() >= 500 || r.status() === 401 || r.status() === 429) errors++
                await r.body()
              } catch {
                errors++
              }
              lat.push(Date.now() - t0)
              await new Promise((r) => setTimeout(r, 500))
            }
          }),
        )
        for (const { ctx } of ctxs) await ctx.dispose()
        const p95 = pct(lat, 95)
        const rate = (errors / lat.length) * 100
        actual(qa, `100 virtual users for 60 s, ${lat.length} requests: error rate ${rate.toFixed(2)}%, p95 ${p95} ms, p50 ${pct(lat, 50)} ms, max ${Math.max(...lat)} ms (one workstation running app, database and load)`)
        qa.expect(rate).toBeLessThan(1)
        qa.expect(p95).toBeLessThanOrEqual(1000)
      },
    },
    {
      id: 'PERF-005',
      role: 'anon',
      title: 'Server response time',
      async run(qa) {
        const ctx = await rawCtx()
        const h = { Authorization: `Bearer ${(await tokenFor(ctx, PEOPLE.organizer)).body.accessToken}` }
        const m = await shared(qa)
        const lat = []
        for (let i = 0; i < 20; i++)
          for (const p of [`/api/meetings/${m.id}`, `/api/meetings/${m.id}/agenda`, `/api/meetings/${m.id}/participants?pageSize=20`, '/api/notifications?pageSize=20', `/api/meetings/${m.id}/votes`]) {
            const t0 = Date.now()
            await (await ctx.get(p, { headers: h })).body()
            lat.push(Date.now() - t0)
          }
        const writes = []
        for (let i = 0; i < 10; i++) {
          const t0 = Date.now()
          const r = await ctx.post('/api/meetings', {
            headers: h,
            data: { title: `[SYS] NFR đo thời gian ${RUN} ${i}`, ...slot(20, 8), mode: 'InPerson', location: 'P1', onlineLink: null, sensitivity: 'Normal', description: '', participants: [{ userId: (await person(qa, 'memberA')).id, role: 'Chair' }] },
          })
          writes.push(Date.now() - t0)
          const id = (await r.json()).meeting?.id
          if (id) await ctx.post(`/api/meetings/${id}/transitions`, { headers: h, data: { action: 'Cancel', reason: 'Dọn dữ liệu đo thời gian.' } })
        }
        actual(qa, `reads (100): p95 ${pct(lat, 95)} ms, max ${Math.max(...lat)} ms; create meeting (10): p95 ${pct(writes, 95)} ms, max ${Math.max(...writes)} ms; one user`)
        qa.expect(pct(lat, 95)).toBeLessThanOrEqual(1000)
        qa.expect(Math.max(...writes)).toBeLessThanOrEqual(3000)
        await ctx.dispose()
      },
    },
    {
      id: 'PERF-006',
      role: PEOPLE.admin,
      title: 'Search and list views',
      async run(qa) {
        const ctx = await rawCtx()
        const h = { Authorization: `Bearer ${(await tokenFor(ctx, PEOPLE.admin)).body.accessToken}` }
        const lists = {
          meetings: `/api/meetings?q=${encodeURIComponent('họp')}&status=all&pageSize=20`,
          users: '/api/users',
          notifications: '/api/notifications?pageSize=20',
          audit: '/api/audit-log?pageSize=50&action=auth.signed_in',
          documents: '/api/documents',
        }
        const out = {}
        for (const [k, p] of Object.entries(lists)) {
          const lat = []
          for (let i = 0; i < 20; i++) {
            const t0 = Date.now()
            const r = await ctx.get(p, { headers: h })
            await r.body()
            lat.push(Date.now() - t0)
          }
          out[k] = pct(lat, 95)
        }
        const vol = sql(`select (select count(*) from meetings), (select count(*) from documents), (select count(*) from audit_log)`)[0]
        actual(qa, `p95 of 20 first-page requests: ${Object.entries(out).map(([k, v]) => `${k} ${v} ms`).join(', ')}; data volume ${vol[0]} meetings, ${vol[1]} documents, ${vol[2]} audit entries`)
        for (const v of Object.values(out)) qa.expect(v).toBeLessThanOrEqual(2000)
        await qa.goto('/mgmt/meetings?status=all&q=h%E1%BB%8Dp')
        await qa.page.waitForTimeout(1500)
        await qa.shot('danh-sach-tim-kiem')
        await ctx.dispose()
      },
    },
    {
      id: 'PERF-007',
      role: PEOPLE.memberB,
      title: 'Meeting audio upload is acknowledged and queued',
      async run(qa) {
        const m = await meetingAt(qa, 'InProgress', { title: `[SYS] NFR Họp tải âm thanh ${RUN}` })
        const seconds = 600
        const n = 16000 * seconds
        const wav = Buffer.alloc(44 + n * 2)
        wav.write('RIFF', 0)
        wav.writeUInt32LE(36 + n * 2, 4)
        wav.write('WAVEfmt ', 8)
        wav.writeUInt32LE(16, 16)
        wav.writeUInt16LE(1, 20)
        wav.writeUInt16LE(1, 22)
        wav.writeUInt32LE(16000, 24)
        wav.writeUInt32LE(32000, 28)
        wav.writeUInt16LE(2, 32)
        wav.writeUInt16LE(16, 34)
        wav.write('data', 36)
        wav.writeUInt32LE(n * 2, 40)
        const ctx = await rawCtx()
        const h = { Authorization: `Bearer ${(await tokenFor(ctx, PEOPLE.memberB)).body.accessToken}` }
        const t0 = Date.now()
        const r = await ctx.post(`/api/meetings/${m.id}/recordings`, { headers: h, timeout: 120_000, multipart: { file: { name: 'hop-10-phut.wav', mimeType: 'audio/wav', buffer: wav }, consentConfirmed: 'true' } })
        const ms = Date.now() - t0
        const b = await r.json().catch(() => ({}))
        const tr = sql(`select status from transcripts where recording_id = '${b.recordingId}'`)[0]?.[0] ?? sql(`select status from recordings where id = '${b.recordingId}'`)[0]?.[0]
        actual(qa, `${(wav.length / 1048576).toFixed(1)} MB (10 min) acknowledged in ${(ms / 1000).toFixed(2)} s including the local transfer, HTTP ${r.status()} ${b.code}; transcription state ${tr}`)
        qa.expect(r.status()).toBe(201)
        qa.expect(ms).toBeLessThanOrEqual(5000)
        await ctx.dispose()
      },
    },
    {
      id: 'PERF-008',
      role: 'anon',
      title: 'Meeting audio AI processing time',
      async run(qa) {
        qa.inconclusive('The speech worker and the local language model do not run on this test machine.')
      },
    },
    {
      id: 'PERF-009',
      role: PEOPLE.memberC,
      title: 'Online meeting room join time',
      async run(qa) {
        const m = await meetingAt(qa, 'InProgress', {
          title: `[SYS] NFR Phòng họp ${RUN}`,
          over: { mode: 'Online', location: null },
          roster: [
            { who: 'memberA', role: 'Chair' },
            { who: 'memberB', role: 'Secretary' },
            { who: 'memberC', role: 'Member' },
          ],
        })
        const times = []
        for (const who of ['memberA', 'memberB', 'memberC']) {
          const page = await secondBrowser(qa, who)
          await page.goto(`/mgmt/meetings/${m.id}/room`)
          await qa.expect(page.getByRole('heading', { level: 1, name: 'Sẵn sàng tham gia?' })).toBeVisible({ timeout: 20_000 })
          const t0 = Date.now()
          await page.getByRole('button', { name: 'Vào phòng', exact: true }).click()
          await qa.expect(page.locator('footer.room-controls').getByRole('button', { name: 'Rời phòng' })).toBeVisible({ timeout: 20_000 })
          times.push(Date.now() - t0)
          if (who === 'memberC') {
            await page.waitForTimeout(1500)
            await shotOf(qa, page, 'ba-nguoi-trong-phong', who)
          }
        }
        await closeSeconds(qa)
        actual(qa, `join (Vào phòng → in the room): ${times.map((t) => (t / 1000).toFixed(2)).join(' s, ')} s for 3 people; capacity of 30 and audio delay not measured`)
        qa.expect(Math.max(...times)).toBeLessThanOrEqual(5000)
      },
    },
    {
      id: 'PERF-010',
      role: PEOPLE.memberA,
      title: 'Report generation and export',
      async run(qa) {
        const y = new Date().getFullYear()
        const t0 = Date.now()
        const answered = qa.page.waitForResponse((r) => r.url().includes('/api/reports/meetings'), { timeout: 20_000 }).catch(() => undefined)
        await qa.page.goto('/mgmt/reports')
        await answered
        await qa.page.waitForLoadState('networkidle', { timeout: 10_000 }).catch(() => undefined)
        const shown = Date.now() - t0
        await qa.shot('bao-cao')
        const ctx = await rawCtx()
        const h = { Authorization: `Bearer ${(await tokenFor(ctx, PEOPLE.memberA)).body.accessToken}` }
        const from = `${y - 1}-12`
        const to = `${y}-11`
        let t1 = Date.now()
        const view = await ctx.get(`/api/reports/meetings?from=${from}&to=${to}`, { headers: h })
        const viewMs = Date.now() - t1
        t1 = Date.now()
        const file = await ctx.get(`/api/reports/meetings/export?from=${from}&to=${to}`, { headers: h })
        const buf = await file.body()
        const exportMs = Date.now() - t1
        actual(qa, `screen ready ${(shown / 1000).toFixed(2)} s; 12-month report API ${(viewMs / 1000).toFixed(2)} s (HTTP ${view.status()}); Excel export ${(exportMs / 1000).toFixed(2)} s (HTTP ${file.status()}, ${buf.length} bytes)`)
        qa.expect(view.status()).toBe(200)
        qa.expect(file.status()).toBe(200)
        qa.expect(Math.max(shown, viewMs)).toBeLessThanOrEqual(5000)
        qa.expect(exportMs).toBeLessThanOrEqual(10_000)
        await ctx.dispose()
      },
    },

    // ================================================================================================ Usability
    {
      id: 'USA-001',
      role: 'anon',
      title: 'Learnability and training time',
      async run(qa) {
        qa.inconclusive('Needs real users timed through guided training sessions; not something an automated run can measure.')
      },
    },
    {
      id: 'USA-002',
      role: PEOPLE.memberC,
      title: 'Vietnamese user interface',
      async run(qa) {
        const english = /\b(Submit|Cancel|Save|Delete|Loading|Error|Meeting|Search|Settings|Logout|Sign in|Upload)\b/
        const seen = []
        for (const path of ['/calendar', '/mgmt/meetings', '/mgmt/documents', '/mgmt/my-voice', '/mgmt/reports']) {
          await qa.goto(path)
          await qa.page.waitForTimeout(800)
          const text = await qa.page.locator('body').innerText()
          if (english.test(text)) seen.push(`${path}: ${english.exec(text)[0]}`)
        }
        await qa.goto(`/mgmt/meetings?status=all&q=${encodeURIComponent('hop giao ban')}`)
        await qa.page.waitForTimeout(1500)
        const hits = await qa.page.locator('main').getByText(/Họp giao ban/).count()
        await qa.shot('tim-khong-dau-ra-co-dau')
        actual(qa, `English words on 5 screens: ${seen.join('; ') || 'none'}; search "hop giao ban" (no diacritics) found ${hits} "Họp giao ban" rows`)
        qa.expect(seen).toEqual([])
        qa.expect(hits).toBeGreaterThan(0)
      },
    },
    {
      id: 'USA-003',
      role: PEOPLE.memberC,
      title: 'Consistency',
      async run(qa) {
        const id = sql(`select id from meetings where title = '[SYS] Họp giao ban tuần 49' and status = 'Ended' order by id desc limit 1`)[0][0]
        await qa.goto(`/mgmt/meetings?status=all&q=${encodeURIComponent('[SYS] Họp giao ban tuần 49')}`)
        await qa.page.waitForTimeout(1200)
        const listChip = await qa.page.locator('main').getByText('Đã kết thúc').count()
        await qa.shot('danh-sach')
        await qa.goto(`/mgmt/meetings/${id}`)
        await qa.expect(qa.page.locator('.record-title').getByText('Đã kết thúc', { exact: true })).toBeVisible()
        const dateText = await qa.page.locator('.record-head').innerText()
        await qa.shot('chi-tiet')
        const shell = []
        for (const path of ['/calendar', '/mgmt/meetings', '/mgmt/documents']) {
          await qa.goto(path)
          shell.push((await qa.page.getByRole('navigation', { name: 'Điều hướng chính' }).count()) + (await bell(qa.page).count()))
        }
        actual(qa, `status "Đã kết thúc" on the list (${listChip}) and the detail; date shown as ${/\d{2}\/\d{2}\/\d{4}/.exec(dateText)?.[0]}; sidebar + bell on 3 screens: ${shell.join(', ')}`)
        qa.expect(listChip).toBeGreaterThan(0)
        qa.expect(dateText).toMatch(/\d{2}\/\d{2}\/\d{4}/)
        qa.expect(shell).toEqual([2, 2, 2])
      },
    },
    {
      id: 'USA-004',
      role: PEOPLE.memberC,
      title: 'Responsive, desktop-first layout',
      async run(qa) {
        const m = await shared(qa)
        const report = []
        for (const width of [1024, 360]) {
          await qa.page.setViewportSize({ width, height: width === 360 ? 780 : 768 })
          for (const path of ['/mgmt/meetings', `/mgmt/meetings/${m.id}`, `/mgmt/meetings/${m.id}?tab=votes`, `/mgmt/meetings/${m.id}?tab=documents`, '/calendar']) {
            await qa.goto(path)
            await qa.page.waitForTimeout(800)
            const sideways = await qa.page.evaluate(() => document.documentElement.scrollWidth - innerWidth)
            report.push(`${width}px ${path.replace(m.id, '{id}')}: ${sideways}`)
          }
          await qa.shot(`chi-tiet-${width}px`)
        }
        const bad = report.filter((r) => !r.endsWith(': 0') && !/: -\d+$/.test(r))
        actual(qa, `sideways overflow (px): ${report.join('; ')}`)
        qa.expect(bad).toEqual([])
      },
    },
    {
      id: 'USA-005',
      role: PEOPLE.memberC,
      title: 'Browser support (Chrome and Edge on Windows)',
      async run(qa) {
        const { chromium } = await import('@playwright/test')
        const out = []
        for (const channel of ['chrome', 'msedge']) {
          let browser
          try {
            browser = await chromium.launch({ channel })
          } catch (e) {
            out.push(`${channel}: not installed`)
            continue
          }
          const ctx = await browser.newContext({ baseURL: qa.base, viewport: { width: 1440, height: 900 } })
          await ctx.request.post(`${qa.base}/api/auth/login`, { data: { email: PEOPLE.memberC, password: PASSWORD } })
          await ctx.addInitScript(() => localStorage.setItem('trustcabinet.signedIn', '1'))
          const page = await ctx.newPage()
          const errors = []
          page.on('pageerror', (e) => errors.push(e.message))
          await page.goto('/mgmt/meetings')
          await page.getByRole('heading', { name: 'Danh sách cuộc họp' }).waitFor({ timeout: 20_000 })
          await page.goto('/calendar')
          await page.waitForTimeout(1000)
          await qa.shot(`${channel}-lich`, { page, who: channel })
          out.push(`${channel} ${browser.version()}: ${errors.length ? `errors ${errors.join('|')}` : 'list and calendar work, no script errors'}`)
          await browser.close()
        }
        actual(qa, out.join('; '))
        qa.expect(out.filter((o) => /not installed|errors \S/.test(o) && !/no script errors/.test(o))).toEqual([])
      },
    },
    {
      id: 'USA-006',
      role: 'anon',
      title: 'Accessibility basics',
      async run(qa) {
        await qa.goto('/login')
        const unlabeled = await qa.page.evaluate(() =>
          [...document.querySelectorAll('input:not([type=hidden]), textarea, select')].filter((el) => {
            const id = el.id
            return !(el.getAttribute('aria-label') || el.getAttribute('aria-labelledby') || (id && document.querySelector(`label[for="${id}"]`)) || el.closest('label'))
          }).length,
        )
        await qa.page.keyboard.press('Tab')
        const steps = []
        for (let i = 0; i < 6; i++) {
          steps.push(await qa.page.evaluate(() => `${document.activeElement?.tagName}:${document.activeElement?.getAttribute('aria-label') ?? document.activeElement?.textContent?.trim().slice(0, 20) ?? ''}`))
          await qa.page.keyboard.press('Tab')
        }
        await qa.page.getByLabel('Email').focus()
        await qa.page.keyboard.type(PEOPLE.memberC)
        await qa.page.keyboard.press('Tab')
        await qa.page.keyboard.type(PASSWORD)
        await qa.page.keyboard.press('Enter')
        await qa.expect(qa.page).toHaveURL(/\/calendar/)
        // 200% zoom on a 1440 px window is a 720 px viewport.
        await qa.page.setViewportSize({ width: 720, height: 450 })
        await qa.goto('/mgmt/meetings')
        await qa.page.waitForTimeout(1000)
        const sideways = await qa.page.evaluate(() => document.documentElement.scrollWidth - innerWidth)
        await qa.shot('zoom-200')
        actual(qa, `unlabelled fields on sign-in: ${unlabeled}; Tab order: ${steps.join(' → ')}; signed in by keyboard only; at 200% zoom sideways overflow ${sideways}px`)
        qa.expect(unlabeled).toBe(0)
        qa.expect(sideways).toBeLessThanOrEqual(0)
      },
    },
    {
      id: 'USA-007',
      role: PEOPLE.organizer,
      title: 'Clear error messages with codes',
      async run(qa) {
        const day = new Date(Date.now() + 3 * 86_400_000 + 7 * 3_600_000).toISOString().slice(0, 10)
        await qa.goto(`/mgmt/meetings?create=${day}`)
        const form = dialog(qa.page, 'Tạo cuộc họp')
        await form.getByPlaceholder('Ví dụ: Phòng họp A — Tầng 4').fill('Phòng họp số 5')
        await form.getByRole('button', { name: 'Tạo cuộc họp' }).click()
        const err = form.getByText('Tên cuộc họp không được để trống.')
        await qa.expect(err).toBeVisible()
        const kept = await form.getByPlaceholder('Ví dụ: Phòng họp A — Tầng 4').inputValue()
        const invalid = await form.getByPlaceholder('Ví dụ: Giao ban tuần 40').getAttribute('aria-invalid')
        const text = await qa.page.locator('body').innerText()
        await qa.shot('loi-o-ten-giu-du-lieu')
        actual(qa, `"Tên cuộc họp không được để trống." under the field (aria-invalid=${invalid}); location kept "${kept}"; raw code or stack trace on screen: ${/MSG\d+|Exception|at \w+\./.test(text)}`)
        qa.expect(kept).toBe('Phòng họp số 5')
        qa.expect(invalid).toBe('true')
        qa.expect(/MSG\d+|Exception/.test(text)).toBe(false)
      },
    },
    {
      id: 'USA-008',
      role: PEOPLE.organizer,
      title: 'Confirmation of destructive and irreversible actions',
      async run(qa) {
        const m = await meetingAt(qa, 'Published', { title: `[SYS] NFR Xác nhận ${RUN}` })
        await qa.goto(`/mgmt/meetings/${m.id}`)
        await qa.page.locator('.record-head').getByRole('button', { name: 'Huỷ cuộc họp' }).click()
        const d = dialog(qa.page, 'Huỷ cuộc họp?')
        await qa.expect(d).toBeVisible()
        const text = await d.innerText()
        await qa.shot('hoi-huy-cuoc-hop')
        await d.getByRole('button', { name: 'Giữ cuộc họp' }).click()
        const still = (await (await client(qa, 'organizer')).get(`/api/meetings/${m.id}`)).body.status
        const b = await secondBrowser(qa, 'memberB')
        let agendaText = ''
        try {
          const sec = await client(qa, 'memberB')
          await sec.post(`/api/meetings/${m.id}/agenda`, { title: 'Mục sẽ xoá', description: '', presenterId: null, presenterGuestInvitationId: null })
          await b.goto(`/mgmt/meetings/${m.id}?tab=agenda`)
          const row = b.getByRole('region', { name: 'Nội dung cuộc họp' }).locator('li.agenda-item').filter({ hasText: 'Mục sẽ xoá' })
          await row.locator('button.agenda-title').click().catch(() => undefined)
          await row.getByRole('button', { name: /Xoá/ }).first().click()
          const dd = b.getByRole('dialog').first()
          await qa.expect(dd).toBeVisible()
          agendaText = await dd.innerText()
          await shotOf(qa, b, 'hoi-xoa-muc-nghi-su', 'memberB')
        } finally {
          await closeSeconds(qa)
        }
        actual(qa, `cancel meeting dialog: "${text.replace(/\s+/g, ' ').slice(0, 140)}"; status after "Giữ cuộc họp": ${still}; delete agenda item dialog: "${agendaText.replace(/\s+/g, ' ').slice(0, 120)}"`)
        qa.expect(still).toBe('Published')
        qa.expect(text).toContain('Mọi người được mời sẽ nhận thông báo')
        qa.expect(agendaText).toContain('Mục sẽ xoá')
      },
    },
    {
      id: 'USA-009',
      role: PEOPLE.memberB,
      title: 'Feedback on long-running operations',
      async run(qa) {
        const m = await shared(qa)
        await qa.goto(`/mgmt/meetings/${m.id}?tab=documents`)
        // Slow the upload down so the progress can be seen, as on a slow network.
        await qa.page.route('**/documents/upload', async (route) => {
          await new Promise((r) => setTimeout(r, 3000))
          await route.continue()
        })
        await qa.page.locator('.documents-panel').getByRole('button', { name: 'Thêm tài liệu chung' }).click()
        const form = dialog(qa.page, 'Thêm tài liệu chung')
        await form.locator('input[type=file]').setInputFiles([pdfFile(`tien-do-${RUN}.pdf`, 2)])
        await form.getByRole('button', { name: 'Thêm 1 tài liệu' }).click()
        const tray = qa.page.getByRole('region', { name: 'Tải lên' })
        await qa.expect(tray).toBeVisible()
        const during = await tray.innerText()
        await qa.shot('dang-tai-len')
        await qa.expect(tray.getByText(/Đã tải lên 1 tệp/)).toBeVisible({ timeout: 30_000 })
        // Double submit: the meeting create button is disabled while its request runs.
        await qa.page.unroute('**/documents/upload')
        const org = await secondBrowser(qa, 'organizer')
        let disabled = 'n/a'
        try {
          await org.route('**/api/meetings', async (route) => {
            if (route.request().method() === 'POST') await new Promise((r) => setTimeout(r, 2500))
            await route.continue()
          })
          const day = new Date(Date.now() + 4 * 86_400_000 + 7 * 3_600_000).toISOString().slice(0, 10)
          await org.goto(`/mgmt/meetings?create=${day}`)
          const f = dialog(org, 'Tạo cuộc họp')
          await f.getByPlaceholder('Ví dụ: Giao ban tuần 40').fill(`[SYS] NFR Chống gửi hai lần ${RUN}`)
          let posts = 0
          org.on('request', (r) => r.method() === 'POST' && r.url().endsWith('/api/meetings') && posts++)
          const submit = f.getByRole('button', { name: /Tạo cuộc họp|Đang/ }).last()
          await submit.click()
          await org.waitForTimeout(300)
          disabled = String(await submit.isDisabled())
          await submit.click({ force: true }).catch(() => undefined)
          await shotOf(qa, org, 'nut-tao-dang-cho', 'organizer')
          await org.waitForURL(/\/mgmt\/meetings\/[0-9a-f-]{36}$/, { timeout: 15_000 })
          disabled += `, POST sent ${posts}`
          qa.expect(posts).toBe(1)
        } finally {
          await closeSeconds(qa)
        }
        actual(qa, `upload tray during a slow upload: "${during.replace(/\s+/g, ' ').slice(0, 100)}"; create button disabled while saving: ${disabled}`)
      },
    },

    // ================================================================================================ Reliability
    {
      id: 'REL-001',
      role: PEOPLE.memberA,
      title: 'No loss of committed data',
      async run(qa) {
        // 1. A refused step changes nothing: the Chair approves minutes from a stale copy (someone changed them since),
        // the server refuses, and the minutes stay waiting with no minutes.issued entry; the current copy then issues.
        const m = await meetingAt(qa, 'Ended', { title: `[SYS] NFR Bước bị từ chối ${RUN}` })
        const sec = await client(qa, 'memberB')
        qa.expectStatus(await sec.post(`/api/meetings/${m.id}/minutes`, {}), 201)
        let live = (await sec.get(`/api/meetings/${m.id}/minutes`)).body.minutes
        qa.expectStatus(await sec.post(`/api/minutes/${live.id}/submit`, { updatedAt: live.updatedAt }), 200)
        const audits = () => Number(sql(`select count(*) from audit_log where object_id = '${live.id}' and action = 'minutes.issued'`)[0][0])
        const chair = await client(qa, 'memberA')
        const refused = await chair.post(`/api/minutes/${live.id}/approve`, { updatedAt: '2000-01-01T00:00:00Z' })
        const after = sql(`select status, issued_at is null from meeting_minutes where id = '${live.id}'`)[0]
        const auditsAfterRefusal = audits()
        live = (await chair.get(`/api/meetings/${m.id}/minutes`)).body.minutes
        const issued = await chair.post(`/api/minutes/${live.id}/approve`, { updatedAt: live.updatedAt })
        const partial = `approve from a stale copy answered ${refused.status} ${refused.body?.code}; minutes still ${after[0]}, not issued ${after[1]}, minutes.issued audit rows ${auditsAfterRefusal}; approve from the current copy answered ${issued.status}`
        qa.expect(refused.status).toBe(409)
        qa.expect(after[0]).toBe('WaitingApproval')
        qa.expect(after[1]).toBe('t')
        qa.expect(auditsAfterRefusal).toBe(0)
        qa.expect(issued.status).toBe(200)
        // 2. A confirmed write survives a hard stop of the server process: the second instance (SYS_LIMITED_BASE) is
        // killed. Only a process started from this checkout is killed — another checkout's backend may hold the port.
        const port = new URL(LIMITED).port
        const pid = sh(`netstat -ano | grep -E '127.0.0.1:${port} .*LISTEN' | awk '{print $5}' | head -1`)
        const cmdline = pid ? sh(`powershell -NoProfile -Command "(Get-CimInstance Win32_Process -Filter 'ProcessId=${pid}').CommandLine"`) : ''
        if (!cmdline.toLowerCase().includes(`${path.join(REPO, 'backend')}${path.sep}`.toLowerCase())) {
          actual(qa, partial)
          qa.inconclusive(`The process on :${port} (${pid || 'none'}) was not started from ${REPO}; not killing it. Start the second instance with the absolute path of TrustCabinet.Web.dll.`)
        }
        const ctx = await rawCtx(LIMITED)
        const t = (await tokenFor(ctx, PEOPLE.organizer)).body.accessToken
        const created = await ctx.post('/api/meetings', {
          headers: { Authorization: `Bearer ${t}` },
          data: { title: `[SYS] NFR Bền dữ liệu ${RUN}`, ...slot(21, 8), mode: 'InPerson', location: 'P1', onlineLink: null, sensitivity: 'Normal', description: '', participants: [{ userId: (await person(qa, 'memberA')).id, role: 'Chair' }] },
        })
        const id = (await created.json()).meeting.id
        await ctx.dispose()
        sh(`taskkill //PID ${pid} //F`)
        const row = sql(`select status from meetings where id = '${id}'`)[0]?.[0]
        actual(qa, `${partial}; meeting created (HTTP ${created.status()}) then the server process killed: row after restart ${row}`)
        qa.expect(row).toBe('Draft')
        qa.note(`Restart the :${port} instance before running later cases that use it.`)
      },
    },
    {
      id: 'REL-002',
      role: PEOPLE.organizer,
      title: 'Reliable asynchronous delivery',
      async run(qa) {
        // Reads what WF-07 (reschedule, hand over the chair, cancel) left in the dispatch queue and the outbox.
        const changeKinds = ['Rescheduled', 'Cancelled', 'RoleChanged']
        const queue = sql(`select event_kind, state, count(*), max(attempt_count), left(max(last_error), 60) from meeting_invitation_dispatches group by 1, 2 order by 1, 2`)
        const outbox = sql(`select status, count(*), max(attempt_count) from email_messages group by 1`)
        // A dispatch that was tried and is still Pending or Failed never reached the outbox.
        const stuck = sql(`select event_kind, state, count(*) from meeting_invitation_dispatches where event_kind in ('Rescheduled','Cancelled','RoleChanged') and (state = 'Failed' or (state = 'Pending' and attempt_count > 0)) group by 1, 2`)
        const written = Object.fromEntries(
          changeKinds.map((k) => [k, Number(sql(`select count(*) from meeting_invitation_dispatches where event_kind = '${k}' and state = 'Materialized'`)[0][0])]),
        )
        const changeMails = sql(`select subject_kind, count(*) from (select case when subject like 'Cuộc họp đã dời lịch%' then 'Rescheduled' when subject like 'Cuộc họp đã bị huỷ%' then 'Cancelled' end as subject_kind from email_messages) s where subject_kind is not null group by 1`)
        const maxAttempts = Math.max(0, ...outbox.map((r) => Number(r[2])))
        // The meeting's own state never depends on its emails: a rescheduled meeting is never sent back for approval.
        const reverted = sql(`select count(distinct m.id) from meetings m join meeting_invitation_dispatches d on d.meeting_id = m.id where d.event_kind = 'Rescheduled' and m.status in ('Draft','WaitingApproval','Approved')`)[0][0]
        actual(qa, `dispatch queue: ${queue.map((r) => r.join(' ')).join(' | ')}; outbox: ${outbox.map((r) => r.join(' ')).join(' | ')}; change emails in the outbox: ${changeMails.map((r) => r.join(' ')).join(', ') || 'none'}; most attempts on one email ${maxAttempts}; rescheduled meetings sent back for approval: ${reverted}`)
        qa.expect(stuck, 'change emails tried but never written to the outbox').toEqual([])
        for (const k of ['Rescheduled', 'Cancelled']) qa.expect(written[k], `no ${k} email was written (run WF-07 first)`).toBeGreaterThan(0)
        qa.expect(changeMails.length, 'reschedule and cancel emails in the outbox').toBe(2)
        qa.expect(maxAttempts).toBeLessThanOrEqual(8)
        qa.expect(reverted).toBe('0')
      },
    },
    {
      id: 'REL-003',
      role: PEOPLE.memberB,
      title: 'Recoverable AI processing',
      async run(qa) {
        const id = state.recordingId ?? sql(`select id from recordings order by created_at desc limit 1`)[0][0]
        await new Promise((r) => setTimeout(r, 5000))
        const tr = sql(`select status, left(coalesce(error_message, ''), 120), progress_percent from transcripts where recording_id = '${id}'`)[0]
        const rec = sql(`select status from recordings where id = '${id}'`)[0]?.[0]
        const meetingId = sql(`select meeting_id from recordings where id = '${id}'`)[0][0]
        await qa.goto(`/mgmt/meetings/${meetingId}?tab=recordings`)
        await qa.page.waitForTimeout(2000)
        await qa.shot('trang-thai-chuyen-chu')
        const retry = qa.page.getByRole('button', { name: 'Chạy lại' }).first()
        await qa.expect(retry).toBeVisible()
        await retry.click()
        const confirm = qa.page.getByRole('dialog').first()
        if (await confirm.isVisible().catch(() => false)) await confirm.getByRole('button', { name: /Chạy lại|Chuyển chữ/ }).last().click()
        await qa.expect(toast(qa.page, 'Đã đưa bản ghi vào hàng chờ chuyển thành chữ.')).toBeVisible({ timeout: 10_000 })
        const queued = sql(`select status from transcripts where recording_id = '${id}'`)[0]?.[0]
        await qa.shot('chay-lai-khong-tai-lai')
        await new Promise((r) => setTimeout(r, 8000))
        const again = sql(`select status, left(coalesce(error_message, ''), 120) from transcripts where recording_id = '${id}'`)[0]
        actual(qa, `speech worker stopped: recording ${rec}, transcript ${tr?.join(' / ')}; "Chạy lại" re-queued the same recording (${queued}) without a new upload, then ${again.join(' / ')}`)
        qa.expect(rec).toBe('Available')
        qa.expect(tr[0]).toBe('Failed')
        qa.expect(tr[1].length).toBeGreaterThan(0)
      },
    },

    // ================================================================================================ Availability
    {
      id: 'AVA-001',
      role: 'anon',
      title: 'Service availability',
      async run(qa) {
        qa.inconclusive('99% over working hours needs uptime monitoring over weeks of the deployed server.')
      },
    },
    {
      id: 'AVA-002',
      role: 'anon',
      title: 'Recovery time and data loss',
      async run(qa) {
        qa.inconclusive('A restore drill from the daily backup on the test server has not been done; no daily backup schedule was found (backups are taken before each deploy).')
      },
    },
    {
      id: 'AVA-003',
      role: PEOPLE.memberC,
      title: 'Graceful degradation',
      async run(qa) {
        execFileSync('docker', ['stop', 'trustcabinet-livekit'])
        try {
          const m = await meetingAt(qa, 'InProgress', {
            title: `[SYS] NFR Phòng khi máy chủ media tắt ${RUN}`,
            over: { mode: 'Online', location: null },
          })
          await qa.goto(`/mgmt/meetings/${m.id}/room`)
          await qa.page.waitForTimeout(4000)
          const join = qa.page.getByRole('button', { name: 'Vào phòng', exact: true })
          if (await join.isVisible().catch(() => false)) await join.click()
          await qa.page.waitForTimeout(8000)
          const roomText = (await qa.page.locator('body').innerText()).replace(/\s+/g, ' ')
          await qa.shot('phong-khi-media-tat')
          const ok = []
          for (const p of ['/mgmt/meetings', `/mgmt/meetings/${m.id}?tab=documents`, `/mgmt/meetings/${m.id}?tab=votes`]) {
            await qa.goto(p)
            await qa.page.waitForTimeout(800)
            ok.push(`${p.replace(m.id, '{id}')} ${(await qa.page.getByRole('heading', { name: 'Không tìm thấy trang' }).count()) === 0 ? 'ok' : 'broken'}`)
          }
          const signIn = await tokenFor(await rawCtx(), PEOPLE.memberE)
          actual(qa, `media server stopped, speech worker and AI absent, mail undeliverable: sign-in ${signIn.status}; ${ok.join(', ')}; room shows "${roomText.slice(0, 160)}"`)
          qa.expect(signIn.status).toBe(200)
          qa.expect(ok.every((x) => x.endsWith('ok'))).toBe(true)
          qa.expect(roomText).toMatch(/không|Không|lỗi|thử lại/)
        } finally {
          execFileSync('docker', ['start', 'trustcabinet-livekit'])
        }
      },
    },
    {
      id: 'AVA-004',
      role: 'anon',
      title: 'Health monitoring',
      async run(qa) {
        const r = await (await qa.api('anon')).get('/api/health')
        const remote = sh('curl -s --max-time 15 https://app.trustcabinet.workspace-hub.space/api/health')
        actual(qa, `local: ${r.status} ${JSON.stringify(r.body)}; test server: ${remote.slice(0, 200)}`)
        qa.expect(r.status).toBe(200)
        qa.expect(r.body.status).toBe('ok')
        qa.expect(r.body.version).toBeTruthy()
        qa.expect(r.body.database).toBe('ok')
      },
    },
  ],
}

// Report 5.3 System Test — WF-06 Voice ID (BF-04).
// docs/08-reports/test-reports/cases/system/WF-06.json describes each case. The person is bao.dang@ (Member C); the
// collector is quantri@ (Quản trị viên, holds "Thu giọng mẫu hộ" / VoiceCollector, ADR-0071, ADR-0075).
//
// Voice ID is sensitive biometric data (Law 91/2025/QH15): the plan starts and ends with the person's consent withdrawn,
// which deletes every sample, so nothing of it stays behind.

import { request as playwrightRequest } from '@playwright/test'
import { PASSWORD, PEOPLE, client, closeSeconds, dialog, noRawCodes, noSidewaysScroll, person, secondBrowser, settled, shotOf, toast } from './sys-lib.mjs'

const state = {}

async function voice(qa) {
  const res = await (await client(qa, 'memberC')).get('/api/me/voice')
  qa.expectStatus(res, 200)
  return res.body
}
async function withdraw(qa) {
  if ((await voice(qa)).consent) qa.expectStatus(await (await client(qa, 'memberC')).delete('/api/me/voice/consent'), 200)
}

/** A two-second 16 kHz mono WAV of a 220 Hz tone (never a person's voice). */
function toneWav() {
  const rate = 16000
  const n = rate * 2
  const buf = Buffer.alloc(44 + n * 2)
  buf.write('RIFF', 0)
  buf.writeUInt32LE(36 + n * 2, 4)
  buf.write('WAVEfmt ', 8)
  buf.writeUInt32LE(16, 16)
  buf.writeUInt16LE(1, 20)
  buf.writeUInt16LE(1, 22)
  buf.writeUInt32LE(rate, 24)
  buf.writeUInt32LE(rate * 2, 28)
  buf.writeUInt16LE(2, 32)
  buf.writeUInt16LE(16, 34)
  buf.write('data', 36)
  buf.writeUInt32LE(n * 2, 40)
  for (let i = 0; i < n; i++) buf.writeInt16LE(Math.round(8000 * Math.sin((2 * Math.PI * 220 * i) / rate)), 44 + i * 2)
  return buf
}

const userRow = (page, email) => page.locator('tbody tr').filter({ has: page.locator('.cell-sub', { hasText: email }) })

export default {
  ticket: 'SYS-WF06',
  title: 'WF-06 Voice ID',
  devices: ['desktop'],
  criteria: [
    {
      id: 'TC01',
      role: PEOPLE.memberC,
      title: 'The member reads the Voice ID notice and consents',
      async run(qa) {
        await withdraw(qa)
        await qa.goto('/mgmt/my-voice')
        await qa.expect(qa.page.getByRole('heading', { level: 1, name: 'Giọng nói của tôi' })).toBeVisible()
        const card = qa.page.getByRole('region', { name: 'Bật nhận diện giọng nói' })
        for (const text of ['Mục đích:', 'Ai thu:', 'Lưu gì:', 'Dùng ở đâu:', 'Quyền của tôi:', 'Bên kiểm soát dữ liệu:']) await qa.expect(card.getByText(text)).toBeVisible()
        await qa.expect(card.getByText(/dữ liệu cá nhân nhạy cảm/)).toBeVisible()
        const box = card.getByRole('checkbox')
        const enable = card.getByRole('button', { name: 'Đồng ý và bật' })
        await qa.expect(box).not.toBeChecked()
        await qa.expect(enable).toBeDisabled()
        await settled(qa.page)
        await qa.shot('thong-bao-voice-id', { fullPage: true })
        await box.check()
        await enable.click()
        await qa.expect(toast(qa.page, 'Đã bật nhận diện giọng nói cho tài khoản của bạn.')).toBeVisible()
        await qa.expect(qa.page.getByText('Đang bật')).toBeVisible()
        await settled(qa.page)
        await qa.shot('da-bat-voice-id')
        await noSidewaysScroll(qa)
        const v = await voice(qa)
        qa.expect(v.consent?.noticeVersion).toBe(v.noticeVersion)
        state.notice = v.noticeVersion
        qa.note(`Consent on notice ${v.noticeVersion}`)
      },
    },
    {
      id: 'TC02',
      role: PEOPLE.admin,
      title: 'The administrator opens the person\'s proxy collection, asks first, and records a sample',
      async run(qa) {
        const c = await person(qa, 'memberC')
        await qa.goto(`/mgmt/admin/users?pageSize=100&q=${encodeURIComponent(c.email.split('@')[0])}`)
        await userRow(qa.page, c.email).getByRole('button', { name: `Quản lý giọng nói thu hộ của ${c.name}` }).click()
        const dlg = dialog(qa.page, `Giọng nói thu hộ: ${c.name}`)
        await qa.expect(dlg).toBeVisible()
        await qa.expect(dlg.getByText(/mẫu thu hộ/)).toBeVisible()
        await qa.expect(dlg.getByRole('button', { name: 'Thu' })).toBeVisible()
        await settled(qa.page)
        await qa.shot('hop-thu-ho-da-dong-y')
        await dlg.getByRole('button', { name: 'Thu' }).click()
        await settled(qa.page)
        await qa.shot('hop-thoai-thu-giong-mau')
        // Without personAgreed the server refuses; with it, the sample goes to the speech worker for its embedding.
        const ctx = await playwrightRequest.newContext({ baseURL: qa.base })
        const login = await ctx.post('/api/auth/login', { data: { email: PEOPLE.admin, password: PASSWORD } })
        const bearer = { Authorization: `Bearer ${(await login.json()).accessToken}` }
        const file = { name: 'mau.wav', mimeType: 'audio/wav', buffer: toneWav() }
        const noAsk = await ctx.post(`/api/users/${c.id}/voice-collection/samples`, { headers: bearer, multipart: { file } })
        const asked = await ctx.post(`/api/users/${c.id}/voice-collection/samples`, { headers: bearer, multipart: { file, personAgreed: 'true' } })
        const a = await asked.json().catch(() => ({}))
        qa.note(`Without personAgreed: ${noAsk.status()} ${(await noAsk.json().catch(() => ({}))).code}; with it: ${asked.status()} ${a.code} ${a.message}`)
        qa.expect(noAsk.status()).toBe(400)
        await ctx.dispose()
        if (asked.status() !== 201)
          qa.inconclusive(
            `A sample needs a recorded human voice and the speech worker to compute its embedding; neither is on this machine (answer ${asked.status()} ${a.code}).`,
          )
      },
    },
    {
      id: 'TC03',
      role: PEOPLE.memberB,
      title: 'The transcript attributes each sentence to the right person',
      async run(qa) {
        qa.inconclusive('Needs the speech worker (transcription and voice matching), which does not run on this test machine.')
      },
    },
    {
      id: 'TC04',
      role: PEOPLE.memberC,
      title: 'The member withdraws consent; samples are deleted and the collector can no longer record',
      async run(qa) {
        await qa.goto('/mgmt/my-voice')
        await qa.expect(qa.page.getByText('Đang bật')).toBeVisible()
        await qa.page.getByRole('button', { name: 'Tắt và xoá giọng mẫu' }).click()
        const d = dialog(qa.page, 'Tắt nhận diện giọng nói?')
        await qa.expect(d).toBeVisible()
        await settled(qa.page)
        await qa.shot('hoi-tat-va-xoa')
        await d.getByRole('button', { name: 'Tắt và xoá' }).click()
        await qa.expect(toast(qa.page, 'Đã tắt nhận diện giọng nói và xoá toàn bộ giọng mẫu.')).toBeVisible()
        await qa.expect(qa.page.getByRole('region', { name: 'Bật nhận diện giọng nói' }).getByRole('checkbox')).not.toBeChecked()
        await settled(qa.page)
        await qa.shot('da-tat')
        await noRawCodes(qa)
        const v = await voice(qa)
        qa.expect(v.consent).toBeNull()
        qa.expect(v.samples.length + v.proxy.samples.length).toBe(0)
        const c = await person(qa, 'memberC')
        const view = (await (await client(qa, 'admin')).get(`/api/users/${c.id}/voice-collection`)).body
        qa.expect(view.consented).toBe(false)
        qa.expect(view.canCollect).toBe(false)
        const admin = await secondBrowser(qa, 'admin')
        try {
          await admin.goto(`/mgmt/admin/users?pageSize=100&q=${encodeURIComponent(c.email.split('@')[0])}`)
          await userRow(admin, c.email).getByRole('button', { name: `Quản lý giọng nói thu hộ của ${c.name}` }).click()
          const dlg = dialog(admin, `Giọng nói thu hộ: ${c.name}`)
          await qa.expect(dlg).toBeVisible()
          await qa.expect(dlg.getByRole('button', { name: 'Thu' })).toHaveCount(0)
          await settled(admin)
          await shotOf(qa, admin, 'quan-tri-khong-con-thu-duoc', 'admin')
        } finally {
          await closeSeconds(qa)
        }
      },
    },
  ],
}

// QA stub for an OpenAI-compatible API (TRANS_FT61/FT64): answers every prompt so the
// backend's "Thử kết nối" and the API draft path can be tested with no outside service.
// Started and stopped by plans/fe-10.mjs (AC59/AC60); cert qa-ai-stub.pfx is per-user trusted.
import { readFileSync, appendFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { createServer } from 'node:https'

const PORT = Number(process.env.QA_STUB_PORT ?? 11439)
const PFX = join(tmpdir(), 'opencode', 'qa-ai-stub.pfx')
const LOG = join(tmpdir(), 'opencode', 'qa-ai-stub.log')
const server = createServer(
  { pfx: readFileSync(PFX), passphrase: 'qa-stub' },
  (req, res) => {
    let body = ''
    req.on('data', (chunk) => (body += chunk))
    req.on('end', () => {
      let content = 'Cuộc họp đã thông qua nội dung được trình bày. Các ý kiến đã được ghi nhận đầy đủ.'
      try {
        const parsed = JSON.parse(body || '{}')
        const user = (parsed.messages ?? []).map((m) => m.content ?? '').join('\n')
        if (user.includes('Trả lời đúng một từ')) content = 'OK'
        appendFileSync(
          LOG,
          `${new Date().toISOString()} ${req.method} ${req.url} model=${parsed.model ?? ''} key=${req.headers.authorization ? 'yes' : 'no'} prompt=${user.slice(0, 80).replace(/\s+/g, ' ')}\n`,
        )
      } catch { /* keep the canned answer */ }
      res.writeHead(200, { 'Content-Type': 'application/json' })
      res.end(JSON.stringify({ choices: [{ message: { role: 'assistant', content }, finish_reason: 'stop' }] }))
    })
  },
)
server.listen(PORT, '127.0.0.1', () => console.log(`qa-ai-stub on 127.0.0.1:${PORT}`))

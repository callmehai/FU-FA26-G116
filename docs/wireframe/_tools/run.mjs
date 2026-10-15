// Mở file wireframe bằng Chrome headless (giao thức CDP), bật "Kích thước thật" rồi:
//   node run.mjs extract <html> <out.json>        trích cây layout của mọi màn (định dạng plugin Figma)
//   node run.mjs shots   <html> <thư mục> <no,…>  chụp PNG từng màn (tỉ lệ 1.5, giống ảnh trong screens/)
import { spawn } from 'node:child_process'
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'

const [mode, html, out, list] = process.argv.slice(2)
if (!['extract', 'shots'].includes(mode) || !html || !out || (mode === 'shots' && !list)) {
  console.error('Cách dùng: node run.mjs extract <html> <out.json> | node run.mjs shots <html> <thư mục> <8.5,11.1,…>')
  process.exit(1)
}
const CHROME = process.env.CHROME || ['C:/Program Files/Google/Chrome/Application/chrome.exe', 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'].find((p) => fs.existsSync(p))
const port = 9333, prof = fs.mkdtempSync(path.join(os.tmpdir(), 'wf-chrome-'))
const chrome = spawn(CHROME, ['--headless=new', `--remote-debugging-port=${port}`, `--user-data-dir=${prof}`, '--window-size=1600,1000', '--hide-scrollbars', 'about:blank'], { stdio: 'ignore' })
const sleep = (ms) => new Promise((r) => setTimeout(r, ms))
let tabs
for (let i = 0; i < 50; i++) { try { tabs = await (await fetch(`http://127.0.0.1:${port}/json`)).json(); if (tabs.length) break } catch {} await sleep(200) }
const ws = new WebSocket(tabs.find((t) => t.type === 'page').webSocketDebuggerUrl)
await new Promise((r) => ws.addEventListener('open', r))
let id = 0; const pend = new Map()
ws.addEventListener('message', (m) => { const d = JSON.parse(m.data); if (d.id && pend.has(d.id)) { pend.get(d.id)(d); pend.delete(d.id) } })
const send = (method, params = {}) => new Promise((r) => { const k = ++id; pend.set(k, r); ws.send(JSON.stringify({ id: k, method, params })) })
const ev = async (expr) => { const r = await send('Runtime.evaluate', { expression: expr, awaitPromise: true, returnByValue: true }); if (r.result.exceptionDetails) throw new Error(JSON.stringify(r.result.exceptionDetails).slice(0, 800)); return r.result.result.value }
try {
  await send('Page.enable')
  await send('Emulation.setDeviceMetricsOverride', { width: 1600, height: 1000, deviceScaleFactor: mode === 'shots' ? 1.5 : 1, mobile: false })
  await send('Page.navigate', { url: 'file:///' + path.resolve(html).split(path.sep).join('/') })
  for (let i = 0; i < 100 && (await ev('document.readyState').catch(() => '')) !== 'complete'; i++) await sleep(200)
  // Font Be Vietnam Pro tải từ Google Fonts: cần mạng, nếu không số đo chữ sẽ lệch.
  await ev(`(async()=>{ if(!document.getElementById('board').classList.contains('real')) document.getElementById('real-toggle').click(); await document.fonts.ready; await new Promise(r=>setTimeout(r,800)); return 1 })()`)
  if (mode === 'extract') {
    const res = await ev(fs.readFileSync(new URL('./extract.js', import.meta.url), 'utf8'))
    fs.writeFileSync(out, JSON.stringify(res)); console.log('Đã trích', res.reduce((a, g) => a + g.screens.length, 0), 'màn →', out)
  } else {
    fs.mkdirSync(out, { recursive: true })
    for (const no of list.split(',').map((s) => s.trim()).filter(Boolean)) {
      const box = await ev(`(()=>{const f=[...document.querySelectorAll('figure.frame')].find(f=>f.querySelector('.cap .no').textContent.trim()===${JSON.stringify(no)}); if(!f) return null; const w=f.querySelector('.wf'); w.scrollIntoView(); const r=w.getBoundingClientRect(); return {x:r.left+scrollX,y:r.top+scrollY,w:r.width,h:r.height}})()`)
      if (!box) { console.error('Không thấy màn', no); continue }
      const r = await send('Page.captureScreenshot', { format: 'png', captureBeyondViewport: true, clip: { x: box.x, y: box.y, width: box.w, height: box.h, scale: 1 } })
      const file = path.join(out, 'screen-' + no.replace('.', '_') + '.png')
      fs.writeFileSync(file, Buffer.from(r.result.data, 'base64')); console.log('Đã chụp', file)
    }
  }
} finally { ws.close(); chrome.kill() }

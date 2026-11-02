// One PNG per lo-fi sheet via headless Chrome. Run from the repository root: node docs/wireframe/lofi/shots.mjs
import { execFileSync } from 'node:child_process'
import { existsSync, mkdirSync } from 'node:fs'
import { resolve } from 'node:path'

const chrome = [process.env.CHROME, 'C:/Program Files/Google/Chrome/Application/chrome.exe', 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'].find((p) => p && existsSync(p))
if (!chrome) throw new Error('Không tìm thấy Chrome: đặt biến môi trường CHROME')
const html = resolve('docs/wireframe/lofi/TrustCabinet-wireframe-lofi.html').replace(/\\/g, '/')
const out = resolve('docs/wireframe/lofi/screens')
mkdirSync(out, { recursive: true })
for (let i = 0; i < 21; i++) {
  const id = String(i).padStart(2, '0')
  execFileSync(chrome, ['--headless=new', '--disable-gpu', '--hide-scrollbars', '--force-device-scale-factor=1.5', '--window-size=1240,660', '--virtual-time-budget=4000', `--screenshot=${out}\\lofi-${id}.png`, `file:///${html}?only=${id}`], { stdio: 'ignore' })
}
console.log('Đã chụp 21 tờ vào', out)

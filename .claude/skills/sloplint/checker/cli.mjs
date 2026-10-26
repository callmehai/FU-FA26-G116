#!/usr/bin/env node
// sloplint CLI.
//   sloplint scan <url...> [--viewports 1440,390] [--scope app|landing|both] [--config .slop.json]
//                              [--out slop-report] [--hover] [--storage-state auth.json] [--setup login.mjs] [--wait 600]
// --setup: a module whose default export `async ({ context, page, url, viewport }) => {}` runs in every
// fresh browser context before the page loads (sign in, seed localStorage, accept cookies). An optional
// named export `ready({ page, url, viewport })` runs after the page loads, before it is measured
// (open a tab, a panel or a dialog the screen only shows after a click).
// Each scan also keeps an unmarked screenshot and an element snapshot per page (--no-snapshot skips them).
//   sloplint diff <before-report-dir> <after-report-dir> [--out slop-diff] [--by order] [--fail-on-new]
//   sloplint static <dir> [--config .slop.json] [--json]
import { existsSync, mkdirSync, readFileSync, writeFileSync, realpathSync } from 'node:fs'
import { dirname, join, resolve, relative } from 'node:path'
import { fileURLToPath, pathToFileURL } from 'node:url'
import { scanStatic } from './static.mjs'
import { diffReports } from './diff.mjs'
import { checkForUpdate } from './update.mjs'

const HERE = dirname(fileURLToPath(import.meta.url))
const SKILL = resolve(HERE, '..')
const registry = JSON.parse(readFileSync(join(SKILL, 'tells.json'), 'utf8'))
const TELLS = Object.fromEntries(registry.tells.map((t) => [t.id, t]))
const DETECT = readFileSync(join(HERE, 'detect.js'), 'utf8')
const SNAPSHOT = readFileSync(join(HERE, 'snapshot.js'), 'utf8')

const BANDS = [
  [2, 'clean', 'No pattern, or one weak one.'],
  [6, 'some slop', 'A few tells together: fix them before shipping.'],
  [Infinity, 'slop', 'Reads as generated: many tells at once.'],
]
export const band = (score) => BANDS.find(([max]) => score <= max)

/** Weighted sum of the distinct tells present (allowed ones and out-of-scope ones excluded). */
export function score(findings, scope = 'both') {
  const live = findings.filter((f) => !f.allowed && inScope(f.id, scope))
  const count = {}
  for (const f of live) count[f.id] = (count[f.id] ?? 0) + 1
  // A tell counts once it reaches its `min` occurrences, or when one finding is marked strong.
  const ids = new Set(live.filter((f) => f.strong || count[f.id] >= (TELLS[f.id]?.min ?? 1)).map((f) => f.id))
  for (const f of findings) if (!f.allowed && !ids.has(f.id)) f.belowMin = true
  // Weight 0: calibration found the pattern as often on professional sites; reported as advice.
  const scored = [...ids].filter((id) => (TELLS[id]?.weight ?? 1) > 0).sort()
  const advice = [...ids].filter((id) => (TELLS[id]?.weight ?? 1) === 0).sort()
  return { score: scored.reduce((s, id) => s + TELLS[id].weight, 0), tells: scored, advice }
}
const inScope = (id, scope) => scope === 'both' || !TELLS[id] || TELLS[id].scope === 'both' || TELLS[id].scope === scope

function copyLists() {
  // references/copy.md ends with a fenced json block the checker uses for word lists.
  const p = join(SKILL, 'references', 'copy.md')
  if (!existsSync(p)) return undefined
  const m = readFileSync(p, 'utf8').match(/```json\s*([\s\S]*?)```(?![\s\S]*```json)/)
  try { return m ? JSON.parse(m[1]) : undefined } catch { return undefined }
}

function parseArgs(argv) {
  const out = { _: [] }
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i]
    if (!a.startsWith('--')) { out._.push(a); continue }
    const key = a.slice(2)
    const next = argv[i + 1]
    if (next === undefined || next.startsWith('--')) out[key] = true
    else { out[key] = next; i++ }
  }
  return out
}

function viewport(spec) {
  const width = Number(spec)
  const mobile = width < 768
  return { tag: String(width), width, height: mobile ? 844 : 900, mobile }
}

async function loadPlaywright() {
  for (const name of ['playwright', '@playwright/test', 'playwright-core']) {
    try { const m = await import(name); return m.chromium ?? m.default?.chromium } catch { /* try next */ }
  }
  console.error('sloplint scan needs Playwright: npm i -D playwright && npx playwright install chromium')
  process.exit(2)
}

/** Moves the pointer over likely cards and buttons and reports the ones that move (M2). */
async function hoverPass(page) {
  const handles = await page.$$('a, button, [role="button"], [onclick], [class*="card"], li, article')
  const out = []
  for (const h of handles.slice(0, 60)) {
    const info = await h.evaluate((e) => {
      const r = e.getBoundingClientRect()
      const s = getComputedStyle(e)
      return { ok: r.width >= 80 && r.height >= 32 && r.top >= 0 && r.bottom <= innerHeight * 3 && s.visibility !== 'hidden', t: `${s.transform}|${s.translate}|${s.scale}` }
    }).catch(() => ({ ok: false }))
    if (!info.ok) continue
    try {
      await h.hover({ timeout: 500, force: true })
      await page.waitForTimeout(260)
      const after = await h.evaluate((e) => { const s = getComputedStyle(e); return `${s.transform}|${s.translate}|${s.scale}` })
      if (after !== info.t && /matrix|translate|scale|\d/.test(after.replace(/none/g, ''))) {
        out.push(await h.evaluate((e) => {
          const r = e.getBoundingClientRect()
          return { id: 'M2', detail: 'moves on hover (measured)', label: `${e.tagName.toLowerCase()} «${(e.innerText || '').trim().slice(0, 40)}»`, rect: { x: Math.round(r.left + scrollX), y: Math.round(r.top + scrollY), w: Math.round(r.width), h: Math.round(r.height) } }
        }))
      }
    } catch { /* detached or covered */ }
  }
  await page.mouse.move(1, 1)
  return out
}

async function annotate(page, findings) {
  await page.evaluate((list) => {
    const layer = document.createElement('div')
    layer.id = '__slop_layer'
    layer.style.cssText = 'position:absolute;inset:0;pointer-events:none;z-index:2147483647'
    for (const f of list) {
      if (!f.rect || f.rect.w < 2) continue
      // Red: scored tells. Grey dashed: advice (weight 0, as common on professional sites).
      const color = f.advice ? '#6e7781' : '#d0342c'
      const box = document.createElement('div')
      box.style.cssText = `position:absolute;left:${f.rect.x}px;top:${f.rect.y}px;width:${f.rect.w}px;height:${f.rect.h}px;outline:2px ${f.advice ? 'dashed' : 'solid'} ${color};outline-offset:1px`
      const tag = document.createElement('span')
      tag.textContent = f.id
      tag.style.cssText = `position:absolute;left:-2px;top:-20px;padding:1px 4px;background:${color};color:#fff;font:600 12px/16px system-ui,sans-serif`
      box.append(tag)
      layer.append(box)
    }
    document.body.style.position ||= 'relative'
    document.body.append(layer)
  }, findings.filter((f) => !f.allowed && !f.belowMin).map((f) => ({ ...f, advice: (TELLS[f.id]?.weight ?? 1) === 0 })))
}

/** Removes the annotation layer and lets every scrolling panel, and the boxes that clip it, grow to its content. */
function unclip() {
  document.getElementById('__slop_layer')?.remove()
  const open = (e) => {
    for (const [p, v] of [['overflow', 'visible'], ['max-height', 'none'], ['height', 'auto']]) e.style.setProperty(p, v, 'important')
  }
  const scrollers = [...document.querySelectorAll('body *')].filter((e) => {
    const s = getComputedStyle(e)
    return /auto|scroll/.test(`${s.overflowX} ${s.overflowY}`) && (e.scrollHeight > e.clientHeight + 1 || e.scrollWidth > e.clientWidth + 1)
  })
  for (const e of scrollers) {
    open(e)
    for (let p = e.parentElement; p; p = p.parentElement) if (getComputedStyle(p).overflow !== 'visible' || p === document.body || p === document.documentElement) open(p)
  }
}

async function scan(urls, args) {
  const chromium = await loadPlaywright()
  const cfgPath = args.config ?? '.slop.json'
  const config = existsSync(cfgPath) ? JSON.parse(readFileSync(cfgPath, 'utf8')) : {}
  const scope = args.scope ?? config.scope ?? 'both'
  const viewports = String(args.viewports ?? '1440,390').split(',').map(viewport)
  const outDir = resolve(args.out ?? 'slop-report')
  mkdirSync(join(outDir, 'shots'), { recursive: true })
  const snapshots = !args['no-snapshot']
  const options = { ignore: config.ignore ?? [], allow: config.allow ?? {}, copy: copyLists(), mark: snapshots }
  const hooks = args.setup ? await import(pathToFileURL(resolve(args.setup)).href) : {}
  const setup = hooks.default ?? null
  const ready = hooks.ready ?? null
  const browser = await chromium.launch()
  const results = []
  const slugs = new Set()
  for (const raw of urls) {
    const url = /^[a-z]+:\/\//i.test(raw) ? raw : pathToFileURL(resolve(raw)).href
    for (const vp of viewports) {
      const context = await browser.newContext({
        viewport: { width: vp.width, height: vp.height }, isMobile: vp.mobile, hasTouch: vp.mobile,
        // Sharp crops in `sloplint diff`; detection works in CSS pixels and is unaffected.
        deviceScaleFactor: snapshots ? 2 : 1,
        storageState: args['storage-state'] && existsSync(args['storage-state']) ? args['storage-state'] : undefined,
      })
      const page = await context.newPage()
      let error = null
      try {
        if (setup) await setup({ context, page, url, viewport: vp })
        await page.goto(url, { waitUntil: 'networkidle', timeout: 45000 }).catch(() => page.goto(url, { waitUntil: 'load', timeout: 45000 }))
        await page.waitForTimeout(Number(args.wait ?? 600))
        // Close a newsletter/cookie modal that opens on load, so it does not dim what is measured.
        await page.keyboard.press('Escape').catch(() => {})
        if (ready) { await ready({ page, url, viewport: vp }); await page.waitForTimeout(Number(args.wait ?? 600)) }
        await page.mouse.move(1, 1)
        const res = await page.evaluate(`(${DETECT})(${JSON.stringify(options)})`)
        // The query and hash are part of the name: /list and /list?status=all are different screens.
        const u = new URL(url)
        let slug = `${u.hostname || 'file'}${u.pathname}${u.search}${u.hash}`.replace(/[^\w]+/g, '-').replace(/^-|-$/g, '').slice(0, 60) || 'page'
        for (let i = 2; slugs.has(`${slug}-${vp.tag}`); i++) slug = `${slug.replace(/-\d+$/, '')}-${i}`
        slugs.add(`${slug}-${vp.tag}`)
        if (args.hover && !vp.mobile) {
          const seen = new Set(res.findings.filter((f) => f.id === 'M2').map((f) => `${f.rect?.x},${f.rect?.y}`))
          for (const f of await hoverPass(page)) if (!seen.has(`${f.rect.x},${f.rect.y}`)) res.findings.push(f)
        }
        res.findings = res.findings.filter((f) => inScope(f.id, scope))
        const s = score(res.findings, scope)
        const shot = join('shots', `${slug}-${vp.tag}.jpg`)
        await annotate(page, res.findings)
        await page.screenshot({ path: join(outDir, shot), fullPage: true, type: 'jpeg', quality: 70, clip: undefined }).catch(() => page.screenshot({ path: join(outDir, shot), type: 'jpeg', quality: 70 }))
        // For `sloplint diff`: every visible element with the tells found on it, and an unmarked
        // screenshot. Scrolling panels are opened first, so rows a panel hides are in the picture.
        const extra = {}
        if (snapshots) {
          extra.snap = join('shots', `${slug}-${vp.tag}.dom.json`)
          extra.clean = join('shots', `${slug}-${vp.tag}.clean.jpg`)
          await page.evaluate(unclip)
          await page.waitForTimeout(150)
          writeFileSync(join(outDir, extra.snap), JSON.stringify(await page.evaluate(`(${SNAPSHOT})()`)))
          await page.screenshot({ path: join(outDir, extra.clean), fullPage: true, type: 'jpeg', quality: 85 })
        }
        // `url` stays the address asked for, so pages pair across scans; a redirect shows in `finalUrl`.
        results.push({ ...res, url, ...(res.url !== url ? { finalUrl: res.url } : {}), viewport: vp, ...s, band: band(s.score)[1], shot, ...extra })
      } catch (e) {
        error = String(e?.message ?? e).split('\n')[0]
        results.push({ url, viewport: vp, error, findings: [], score: 0, tells: [], band: 'error' })
      }
      await context.close()
      const last = results.at(-1)
      console.log(`${last.band.padEnd(9)} ${String(last.score).padStart(3)}  ${vp.tag.padStart(4)}  ${url}${last.tells.length ? `  [${last.tells.join(' ')}]` : ''}${error ? `  ${error}` : ''}`)
    }
  }
  await browser.close()
  const report = { tool: 'sloplint', version: registry.version, scope, generated: new Date().toISOString(), results }
  writeFileSync(join(outDir, 'report.json'), JSON.stringify(report, null, 2))
  writeFileSync(join(outDir, 'report.html'), html(report, outDir))
  console.log(`\nReport: ${join(outDir, 'report.html')}`)
  return results
}

const esc = (s) => String(s ?? '').replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' })[c])

function html(report, outDir) {
  const docs = pathToFileURL(join(SKILL, 'references', 'tells.md')).href
  const rows = report.results.map((r, i) => `<tr><td><a href="#r${i}">${esc(r.url)}</a></td><td class="num">${r.viewport.tag}</td><td class="num">${r.score}</td><td><span class="band band-${r.band.replace(' ', '-')}">${esc(r.band)}</span></td><td>${r.tells.join(' ')}${r.advice?.length ? ` <span class="meta">+ advice ${r.advice.join(' ')}</span>` : ''}</td></tr>`).join('')
  const sections = report.results.map((r, i) => {
    if (r.error) return `<section id="r${i}"><h2>${esc(r.url)} <small>${r.viewport.tag}px</small></h2><p class="error">${esc(r.error)}</p></section>`
    const groups = {}
    for (const f of r.findings) (groups[f.id] ??= []).push(f)
    const order = Object.keys(groups).sort((a, b) => (TELLS[b]?.weight ?? 1) - (TELLS[a]?.weight ?? 1) || a.localeCompare(b))
    const items = order.map((id) => {
      const t = TELLS[id] ?? { title: id, short: '', fix: '', weight: 1, slug: '' }
      const list = groups[id]
      const allowed = list.every((f) => f.allowed)
      const weak = !allowed && list.every((f) => f.belowMin)
      const adviceOnly = !allowed && !weak && t.weight === 0
      return `<details${allowed || weak ? '' : ' open'}><summary><b>${id}</b> ${esc(t.title)} <span class="meta">weight ${t.weight} · ${list.length} ${list.length === 1 ? 'place' : 'places'}${allowed ? ` · allowed: ${esc(list[0].reason)}` : ''}${weak ? ' · below threshold, not scored' : ''}${adviceOnly ? ' · advice: common on professional sites too, not scored' : ''}</span></summary>
<p>${esc(t.short)}</p><p class="fix"><b>Fix.</b> ${esc(t.fix)} <a href="${docs}#${t.slug}">Details</a></p>
<ul>${list.slice(0, 12).map((f) => `<li><code>${esc(f.selector ?? f.label)}</code> ${esc(f.detail)}${f.allowed ? ' <i>(allowed)</i>' : ''}</li>`).join('')}${list.length > 12 ? `<li>… ${list.length - 12} more</li>` : ''}</ul></details>`
    }).join('')
    return `<section id="r${i}"><h2>${esc(r.url)} <small>${r.viewport.tag}px · score ${r.score} · ${esc(r.band)}</small></h2>
<div class="split"><div class="findings">${items || '<p>No tells found.</p>'}</div><figure><img loading="lazy" src="${esc(relative(outDir, join(outDir, r.shot)))}" alt="Annotated screenshot of ${esc(r.url)} at ${r.viewport.tag}px"></figure></div></section>`
  }).join('')
  return `<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>sloplint report</title>
<style>
:root { --ink:#1b1f24; --muted:#59636e; --line:#d8dee4; --bg:#fff; --soft:#f6f8fa; --accent:#0b57d0; --ok:#1a7f37; --warn:#9a6700; --bad:#cf222e; }
* { box-sizing:border-box } body { margin:0; background:var(--bg); color:var(--ink); font:14px/1.5 system-ui,-apple-system,"Segoe UI",sans-serif }
main { max-width:1280px; margin:0 auto; padding:24px 16px 64px } h1 { margin:0 0 4px; font-size:22px } h2 { margin:0 0 12px; font-size:16px; overflow-wrap:anywhere }
h2 small, .meta, .lead { color:var(--muted); font-weight:400 } a { color:var(--accent) }
table { width:100%; border-collapse:collapse; margin:16px 0 32px } th, td { padding:8px 10px; border-bottom:1px solid var(--line); text-align:left; vertical-align:top; overflow-wrap:anywhere } th { font-weight:600; color:var(--muted) }
.num { text-align:right; font-variant-numeric:tabular-nums } .band { font-weight:600 } .band-clean { color:var(--ok) } .band-some-slop { color:var(--warn) } .band-slop, .band-error, .error { color:var(--bad) }
section { padding:24px 0; border-top:1px solid var(--line) } .split { display:grid; grid-template-columns:minmax(0,1fr) minmax(0,1fr); gap:24px; align-items:start }
details { border:1px solid var(--line); border-radius:6px; padding:8px 12px; margin-bottom:8px } summary { cursor:pointer } details p { margin:6px 0 } .fix { background:var(--soft); padding:6px 8px; border-radius:4px }
ul { margin:6px 0 0; padding-left:18px } code { font-size:12px; overflow-wrap:anywhere } figure { margin:0; border:1px solid var(--line); border-radius:6px; overflow:auto; max-height:80vh } figure img { display:block; width:100% }
@media (max-width: 860px) { .split { grid-template-columns:1fr } }
</style></head><body><main>
<h1>sloplint report</h1><p class="lead">Scope ${esc(report.scope)} · ${esc(report.generated)} · score = sum of weights of distinct tells present; 0–2 clean, 3–6 some slop, 7+ slop.</p>
<table><thead><tr><th>Page</th><th class="num">Width</th><th class="num">Score</th><th>Verdict</th><th>Tells</th></tr></thead><tbody>${rows}</tbody></table>
${sections}</main></body></html>`
}

async function main() {
  const [cmd, ...rest] = process.argv.slice(2)
  // Asked while the command runs; printed after it, on stderr, so JSON output stays clean.
  const update = cmd ? checkForUpdate() : Promise.resolve()
  const args = parseArgs(rest)
  if (cmd === 'scan' && args._.length) {
    const results = await scan(args._, args)
    process.exitCode = results.some((r) => r.band === 'slop') ? 1 : 0
  } else if (cmd === 'diff' && args._.length === 2) {
    const out = resolve(args.out ?? 'slop-diff')
    const pages = diffReports(args._[0], args._[1], out, { byOrder: args.by === 'order' })
    for (const p of pages) {
      console.log(`${String(p.score[0]).padStart(3)} → ${String(p.score[1]).padEnd(3)} ${p.viewport.padStart(4)}  ${p.path}${p.before ? ` (paired by order with ${p.before})` : ''}  ${p.skipped ? p.skipped : `${p.items.length} changes${p.fixed.length ? `, gone ${p.fixed.join(' ')}` : ''}${p.new.length ? `, new ${p.new.join(' ')}` : ''}`}`)
    }
    console.log(`\nDiff: ${join(out, 'diff.html')}`)
    // --fail-on-new: exit 1 when the after scan has a tell the before scan did not.
    if (args['fail-on-new'] && pages.some((p) => p.new?.length)) process.exitCode = 1
  } else if (cmd === 'static' && args._.length) {
    const dir = resolve(args._[0])
    // The scanned folder's own .slop.json, else the one in the working directory (the project root).
    const cfgPath = args.config ?? [join(dir, '.slop.json'), '.slop.json'].find((p) => existsSync(p)) ?? '.slop.json'
    const config = existsSync(cfgPath) ? JSON.parse(readFileSync(cfgPath, 'utf8')) : {}
    const found = scanStatic(dir, config)
    if (args.json) console.log(JSON.stringify(found, null, 2))
    else {
      const ids = [...new Set(found.map((f) => f.id))].sort()
      for (const id of ids) {
        const list = found.filter((f) => f.id === id)
        console.log(`\n${id} ${TELLS[id]?.title ?? ''} (${list.length})\n  fix: ${TELLS[id]?.fix ?? ''}`)
        for (const f of list.slice(0, 15)) console.log(`  ${f.file}:${f.line}  ${f.msg}${f.selector ? `  [${f.selector}]` : ''}`)
        if (list.length > 15) console.log(`  … ${list.length - 15} more`)
      }
      console.log(`\n${found.length} findings, ${ids.length} distinct tells`)
    }
    process.exitCode = found.length ? 1 : 0
  } else {
    console.log(`Usage:
  sloplint scan <url|file...> [--viewports 1440,390] [--scope app|landing|both] [--config .slop.json]
                    [--out slop-report] [--hover] [--storage-state auth.json] [--setup login.mjs] [--wait 600] [--no-snapshot]
  sloplint diff <before-report-dir> <after-report-dir> [--out slop-diff] [--by order] [--fail-on-new]
  sloplint static <dir> [--config .slop.json] [--json]`)
    process.exitCode = cmd ? 2 : 0
  }
  const notice = await update
  if (notice) console.error(`\n${notice}`)
}

// npx and npm link start the CLI through a symlink in node_modules/.bin.
if (process.argv[1] && realpathSync(process.argv[1]) === realpathSync(fileURLToPath(import.meta.url))) main()

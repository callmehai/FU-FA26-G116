// `sloplint diff`: compares two scan reports of the same screens, taken before and after a change,
// and shows every place that changed as a numbered card: one line on what changed, in words, and
// the same region cropped from both screenshots. A score that went from 9 to 4 says little; the
// cards show which tells went away, which appeared, and every other visible change (a colour, a
// text, a removed chip), so a reviewer can check each fix and see none is missing.
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { dirname, join, relative, resolve } from 'node:path'
import { fileURLToPath, pathToFileURL } from 'node:url'

const SKILL = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const registry = JSON.parse(readFileSync(join(SKILL, 'tells.json'), 'utf8'))
const TELLS = Object.fromEntries(registry.tells.map((t) => [t.id, t]))
const MAX_ITEMS = 80

const parentKey = (k) => (k.includes('>') ? k.slice(0, k.lastIndexOf('>')) : null)
const counted = (r) => new Set([...(r.tells ?? []), ...(r.advice ?? [])])
const isIcon = (el) => /^(.* > )?(svg|img)\b/.test(el.label.split(' > ').pop())
const describe = (el) => (isIcon(el) ? 'icon' : `${el.label.split(' > ').pop()}${el.text ? ` «${el.text.slice(0, 40)}»` : ''}`)

/**
 * Compares two scan results of one screen (with their element snapshots) and returns numbered items:
 * { n, kind: 'fixed'|'new'|'changed'|'removed'|'added', label, text, fixed, added, changes, before: [rect], after: [rect], ctx }.
 * An element added or removed inside one that exists on both sides is reported as a change of that
 * parent ("icon added", "span.chip removed"), so one visual change is one item.
 */
export function diffPage(b, a) {
  const B = new Map(b.snapshot.elements.map((e) => [e.key, e]))
  const A = new Map(a.snapshot.elements.map((e) => [e.key, e]))
  const props = b.snapshot.props
  const inherited = new Set(props.slice(0, b.snapshot.inherited))
  const cb = counted(b), ca = counted(a)
  const tellsOf = (e, set) => (e?.tells ?? []).filter((id) => set.has(id))

  // 1. Per matched element: what a reader can see that differs.
  const entries = new Map()
  for (const [k, eb] of B) {
    const ea = A.get(k)
    if (!ea) continue
    const list = []
    if (eb.text !== ea.text) list.push(['text', eb.text, ea.text])
    props.forEach((p, i) => {
      if (eb.style[i] === ea.style[i]) return
      // A border colour follows the text colour by default; it only shows when the border has a width.
      const side = p.match(/^border-(top|left)-color$/)?.[1]
      if (side && [eb, ea].every((e) => e.style[props.indexOf(`border-${side}-width`)] === '0px')) return
      list.push([p, eb.style[i], ea.style[i]])
    })
    for (const p of new Set([...Object.keys(eb.pseudo ?? {}), ...Object.keys(ea.pseudo ?? {})])) {
      const x = eb.pseudo?.[p] ?? 'none', y = ea.pseudo?.[p] ?? 'none'
      if (x !== y) list.push([p, x, y])
    }
    if (eb.icon !== ea.icon) list.push(['icon', eb.icon ?? 'none', ea.icon ?? 'none'])
    entries.set(k, list)
  }
  // An inherited change shows on every descendant: keep it on the topmost element only.
  const same = (x, y) => x[0] === y[0] && x[1] === y[1] && x[2] === y[2]
  for (const [k, list] of entries) {
    const pl = entries.get(parentKey(k))
    if (pl) entries.set(k, list.filter((x) => !(inherited.has(x[0]) && pl.some((y) => same(x, y)))))
  }

  // 2. Elements only on one side. The topmost one is folded into its parent when the parent exists
  // on both sides; otherwise it is an item of its own.
  const raw = []
  const fold = new Map()
  const topmost = (k, own, other) => !other.has(k) && !(parentKey(k) && own.has(parentKey(k)) && !other.has(parentKey(k)))
  for (const [k, eb] of B) {
    if (!topmost(k, B, A)) continue
    const pk = parentKey(k)
    if (pk && entries.has(pk)) (fold.get(pk) ?? fold.set(pk, { removed: [], added: [] }).get(pk)).removed.push(eb)
    else raw.push({ el: eb, fixed: tellsOf(eb, cb), added: [], changes: [['removed', describe(eb), '']], before: eb.rect, gone: 'removed' })
  }
  for (const [k, ea] of A) {
    if (!topmost(k, A, B)) continue
    const pk = parentKey(k)
    if (pk && entries.has(pk)) (fold.get(pk) ?? fold.set(pk, { removed: [], added: [] }).get(pk)).added.push(ea)
    else raw.push({ el: ea, fixed: [], added: tellsOf(ea, ca), changes: [['added', '', describe(ea)]], after: ea.rect, gone: 'added', ctx: B.get(parentKey(k) ?? '')?.rect })
  }
  // The before side of an added element and the after side of a removed one show the parent.
  for (const r of raw) if (r.gone === 'removed') r.ctx = A.get(parentKey(r.el.key) ?? '')?.rect

  for (const [k, list0] of entries) {
    const f = fold.get(k)
    const list = [...list0, ...(f?.removed ?? []).map((e) => ['removed', describe(e), '']), ...(f?.added ?? []).map((e) => ['added', '', describe(e)])]
    const eb = B.get(k), ea = A.get(k)
    const tb = tellsOf(eb, cb), ta = tellsOf(ea, ca)
    const fixed = [...new Set([...tb.filter((id) => !ta.includes(id)), ...(f?.removed ?? []).flatMap((e) => tellsOf(e, cb))])]
    const added = [...new Set([...ta.filter((id) => !tb.includes(id)), ...(f?.added ?? []).flatMap((e) => tellsOf(e, ca))])]
    if (!list.length && !fixed.length && !added.length) continue
    // When the parent only lost or gained a child, show the child, not the whole parent.
    const child = !list0.length && (f?.removed[0] ?? f?.added[0])
    raw.push({ el: ea, fixed, added, changes: list, before: eb.rect, after: ea.rect, ...(child ? { gone: f.removed.length ? 'removed' : 'added', ctx: child.rect, child } : {}) })
  }

  // 3. The same change on many elements (a chip on every row) is one item with many boxes.
  const groups = new Map()
  for (const r of raw) {
    // A folded child: its own box on its side, its position on the other.
    if (r.child) { if (r.gone === 'removed') { r.before = r.child.rect; r.after = undefined } else { r.after = r.child.rect; r.before = undefined } }
    const sig = [r.gone ?? '', r.el.label, r.changes.map((c) => (c[0] === 'text' ? 'text' : c.join('|'))).join(';')].join('#')
    const g = groups.get(sig) ?? { label: r.el.label, text: r.el.text, fixed: [], added: [], changes: r.changes, gone: r.gone, before: [], after: [], ctx: r.ctx }
    g.fixed = [...new Set([...g.fixed, ...r.fixed])]
    g.added = [...new Set([...g.added, ...r.added])]
    if (r.before) g.before.push(r.before)
    if (r.after) g.after.push(r.after)
    groups.set(sig, g)
  }
  for (const g of groups.values()) g.kind = g.fixed.length ? 'fixed' : g.added.length ? 'new' : g.gone && !raw.find((r) => r.el.label === g.label && r.child) ? g.gone : 'changed'
  const ORDER = { fixed: 0, new: 1, removed: 2, added: 2, changed: 3 }
  const top = (g) => Math.min(...[...g.before, ...g.after].map((r) => r.y))
  const items = [...groups.values()].sort((x, y) => ORDER[x.kind] - ORDER[y.kind] || top(x) - top(y))
  items.forEach((g, i) => { g.n = i + 1; g.summary = summarize(g.changes) })

  const pageFixed = [...cb].filter((id) => !ca.has(id)).sort()
  const pageNew = [...ca].filter((id) => !cb.has(id)).sort()
  const matched = [...B.keys()].filter((k) => A.has(k)).length
  return {
    items: items.slice(0, MAX_ITEMS), more: Math.max(0, items.length - MAX_ITEMS), pageFixed, pageNew,
    // Inserting one element shifts the keys of its later siblings; a low match rate means the
    // structure changed so much that the element pairing is unreliable.
    matchRate: matched / Math.max(B.size, A.size, 1),
    size: { before: [b.snapshot.width, b.snapshot.height], after: [a.snapshot.width, a.snapshot.height] },
  }
}

/** Property changes in words, most visible first: { t, from?, to? } (from/to only for colours). */
function summarize(changes) {
  const none = (v) => !v || v === 'none' || /^rgba\([^)]*,\s*0\)$/.test(v)
  const has = (p) => changes.some((c) => c[0] === p)
  const out = []
  const add = (rank, t, from, to) => { if (!out.some((o) => o.t === t)) out.push({ rank, t, from, to }) }
  for (const [p, x, y] of changes) {
    if (p === 'removed') add(0, `${x} removed`)
    else if (p === 'added') add(0, `${y} added`)
    else if (p === 'text') add(1, `text «${x}» → «${y}»`)
    else if (p === '::before' || p === '::after') add(2, `${p} marker ${none(y.split(' | ')[0]) ? 'removed' : none(x.split(' | ')[0]) ? 'added' : 'changed'}`)
    else if (p === 'icon') add(2, 'icon changed')
    else if (p === 'background-color') add(3, none(y) ? 'background removed' : none(x) ? 'background added' : 'background colour', none(x) || none(y) ? undefined : x, none(x) || none(y) ? undefined : y)
    else if (p === 'background-image') add(3, none(y) ? 'gradient or image removed' : none(x) ? 'gradient or image added' : 'gradient or image changed')
    else if (p === 'border-radius') add(4, `corners ${x} → ${y}`)
    else if (p === 'box-shadow') add(4, none(y) ? 'shadow or ring removed' : none(x) ? 'shadow or ring added' : 'shadow changed')
    else if (/^border-/.test(p)) add(4, 'border')
    else if (p === 'color') add(5, 'text colour', x, y)
    else if (p === 'font-size') add(5, `text size ${x} → ${y}`)
    else if (p === 'font-weight') add(5, `weight ${x} → ${y}`)
    else if (p === 'line-height' && has('font-size')) continue
    else if (/^(padding|margin|gap)$/.test(p)) add(6, 'spacing')
    else add(7, p)
  }
  return out.sort((x, y) => x.rank - y.rank).map(({ t, from, to }) => (from ? { t, from, to } : { t }))
}

/**
 * Pairs results of two reports by path and viewport, falling back to order for renamed paths.
 * `byOrder` pairs the n-th page of one scan with the n-th of the other (two builds on different
 * paths or ports, scanned from parallel URL lists).
 */
function pair(before, after, byOrder = false) {
  if (byOrder) return before.results.map((b, i) => [b, after.results[i]]).filter((p) => p[1])
  const key = (r) => { try { const u = new URL(r.url); return `${u.pathname}${u.search}${u.hash}@${r.viewport.tag}` } catch { return `${r.url}@${r.viewport.tag}` } }
  const left = [...after.results]
  const pairs = []
  for (const b of before.results) {
    const i = left.findIndex((a) => key(a) === key(b))
    if (i >= 0) pairs.push([b, left.splice(i, 1)[0]])
    else pairs.push([b, null])
  }
  for (const p of pairs) if (!p[1]) { const i = left.findIndex((a) => a.viewport.tag === p[0].viewport.tag); if (i >= 0) { p[1] = left.splice(i, 1)[0]; p[2] = true } }
  return pairs.filter((p) => p[1])
}

const esc = (s) => String(s ?? '').replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' })[c])
const swatch = (v) => (/^rgba?\(/.test(v) ? `<i class="sw" style="background:${esc(v)}"></i>` : '')

export function diffReports(beforeDir, afterDir, outDir, { byOrder = false } = {}) {
  const load = (d) => {
    const p = join(resolve(d), 'report.json')
    if (!existsSync(p)) throw new Error(`no report.json in ${d}: run sloplint scan --out ${d} first`)
    return JSON.parse(readFileSync(p, 'utf8'))
  }
  const before = load(beforeDir), after = load(afterDir)
  const out = resolve(outDir)
  mkdirSync(out, { recursive: true })
  const pages = []
  for (const [b, a, guessed] of pair(before, after, byOrder)) {
    if (b.error || a.error || !b.snap || !a.snap) { pages.push({ b, a, skipped: b.error || a.error || 'scanned without element snapshots: rescan with this version' }); continue }
    b.snapshot = JSON.parse(readFileSync(join(resolve(beforeDir), b.snap), 'utf8'))
    a.snapshot = JSON.parse(readFileSync(join(resolve(afterDir), a.snap), 'utf8'))
    pages.push({ b, a, byOrder: guessed, ...diffPage(b, a), shot: { before: relative(out, join(resolve(beforeDir), b.clean)), after: relative(out, join(resolve(afterDir), a.clean)) } })
    delete b.snapshot; delete a.snapshot
  }
  const summary = pages.map((p) => ({ path: p.a.url, before: p.byOrder ? p.b.url : undefined, viewport: p.a.viewport.tag, score: [p.b.score, p.a.score], fixed: p.pageFixed, new: p.pageNew, skipped: p.skipped,
    items: p.items?.map((g) => ({ n: g.n, kind: g.kind, label: g.label, text: g.text, fixes: g.fixed, adds: g.added, summary: g.summary.map((s) => s.t), changes: g.changes, before: g.before.length, after: g.after.length })) ?? [] }))
  writeFileSync(join(out, 'diff.json'), JSON.stringify({ tool: 'sloplint', generated: new Date().toISOString(), pages: summary }, null, 2))
  writeFileSync(join(out, 'diff.html'), html(pages))
  writeFileSync(join(out, 'diff.md'), markdown(summary))
  return summary
}

/** A short summary for a pull-request comment or a CI job summary. */
function markdown(summary) {
  const ids = (l) => (l?.length ? l.join(' ') : '–')
  // A page by its path: no origin for a URL, relative to the working directory for a file.
  const where = (u) => (u.startsWith('file:') ? relative(process.cwd(), fileURLToPath(u)) : u.replace(/^https?:\/\/[^/]+/, ''))
  const rows = summary.map((p) => `| ${where(p.path)} | ${p.viewport} | ${p.skipped ? '–' : `${p.score[0]} → ${p.score[1]}`} | ${ids(p.fixed)} | ${ids(p.new)} | ${p.skipped ? 'skipped' : p.items.length} |`)
  const cards = summary.filter((p) => p.items.length).map((p) => `**${where(p.path)} at ${p.viewport}px**\n\n${p.items.map((g) => {
    const n = Math.max(g.before, g.after)
    const tells = [g.fixes.length ? `fixes ${g.fixes.join(' ')}` : '', g.adds.length ? `**adds ${g.adds.join(' ')}**` : ''].filter(Boolean).join(', ')
    return `${g.n}. ${KIND[g.kind]} \`${g.label.split(' > ').pop()}\`${g.text ? ` «${g.text.slice(0, 40)}»` : ''}${n > 1 ? ` on ${n} elements` : ''}: ${g.summary.slice(0, 4).join(', ')}${tells ? `. ${tells}` : ''}`
  }).join('\n')}`).join('\n\n')
  const added = summary.flatMap((p) => p.new ?? [])
  return `### sloplint diff

${added.length ? `New tells: **${[...new Set(added)].join(' ')}**. ` : 'No new tells. '}Open \`diff/diff.html\` from the run's \`sloplint\` artifact for each change cropped before and after.

| Page | Width | Score | Tells gone | New tells | Changes |
|---|---:|---:|---|---|---:|
${rows.join('\n')}
${cards ? `\n<details><summary>Every change</summary>\n\n${cards}\n\n</details>\n` : ''}`
}

const KIND = { fixed: 'Fixed', new: 'New tell', changed: 'Changed', removed: 'Removed', added: 'Added' }

/** The region around `r` (page px) cut from a screenshot, scaled to fit a card, with the element outlined. */
function crop(src, size, r, ctx, cls) {
  const [W, H] = size
  const focus = r ?? ctx
  if (!focus) return '<div class="crop-none">not on this side</div>'
  if (focus.y >= H || focus.x >= W) return '<div class="crop-none">outside the screenshot</div>'
  const w = Math.min(Math.max(focus.w + 96, 280), 720, W), h = Math.min(Math.max(focus.h + 48, 72), 360)
  const x = Math.max(0, Math.min(W - w, focus.x + focus.w / 2 - w / 2)), y = Math.max(0, Math.min(H - h, focus.y + focus.h / 2 - h / 2))
  const s = Math.min(2, 400 / w)
  const px = (v) => `${Math.round(v * s)}px`
  const box = r ? `<i class="mark ${cls}" style="left:${px(r.x - x)};top:${px(r.y - y)};width:${px(Math.min(r.w, w))};height:${px(Math.min(r.h, h))}"></i>` : ''
  return `<div class="crop" style="width:${px(w)};height:${px(h)};background-image:url('${esc(src)}');background-size:${px(W)} ${px(H)};background-position:-${px(x)} -${px(y)}">${box}</div>`
}

function html(pages) {
  const docs = pathToFileURL(join(SKILL, 'references', 'tells.md')).href
  const tell = (id) => `<a href="${docs}#${TELLS[id]?.slug ?? ''}">${id}</a> ${esc(TELLS[id]?.title)}`
  const tellIds = (ids) => ids.map((id) => `<a href="${docs}#${TELLS[id]?.slug ?? ''}" title="${esc(TELLS[id]?.title)}">${id}</a>`).join(' ')
  const rows = pages.map((p, i) => `<tr><td><a href="#p${i}">${esc(p.a.url)}</a></td><td class="num">${p.a.viewport.tag}</td><td class="num">${p.b.score} → ${p.a.score}</td><td>${tellIds(p.pageFixed ?? []) || '–'}</td><td>${tellIds(p.pageNew ?? []) || '–'}</td><td class="num">${p.skipped ? '–' : p.items.length + (p.more ? `+${p.more}` : '')}</td></tr>`).join('')

  const card = (p, i, g) => {
    const n = Math.max(g.before.length, g.after.length)
    // The four most visible changes as one sentence; the rest are under "Computed values".
    const shown = g.summary.slice(0, 4).map((s) => `${esc(s.t)}${s.from ? ` ${swatch(s.from)}→${swatch(s.to)}` : ''}`)
    const what = `${shown.join(', ')}${g.summary.length > 4 ? `, and ${g.summary.length - 4} more` : ''}.`
    // Capitalise a sentence, never a selector ("span.chip removed").
    const what0 = /^[a-z]+[\s:]/.test(what) ? what.charAt(0).toUpperCase() + what.slice(1) : what
    const tells = [g.fixed.length ? `<p class="tells ok">Fixes ${g.fixed.map(tell).join(', ')}</p>` : '', g.added.length ? `<p class="tells bad">Adds ${g.added.map(tell).join(', ')}</p>` : ''].join('')
    const raw = g.changes.map((c) => `<tr><th>${esc(c[0])}</th><td>${swatch(c[1])}<code>${esc(c[1] || '–')}</code></td><td>${swatch(c[2])}<code>${esc(c[2] || '–')}</code></td></tr>`).join('')
    const before = g.before[0], after = g.after[0]
    const markB = g.kind === 'fixed' ? 'bad' : 'chg', markA = g.kind === 'fixed' ? 'ok' : g.kind === 'new' ? 'bad' : 'chg'
    return `<article class="card" id="p${i}-${g.n}">
<div class="what"><h3><span class="n k-${g.kind}">${g.n}</span>${KIND[g.kind]}: <code>${esc(g.label.split(' > ').pop())}</code>${g.text ? ` «${esc(g.text.slice(0, 50))}»` : ''}${n > 1 ? ` <span class="meta">on ${n} elements</span>` : ''}</h3>
<p class="sentence">${what0}</p>${tells}<details><summary>Computed values</summary><table class="raw"><tr><th></th><th>Before</th><th>After</th></tr>${raw}</table><p class="meta"><code>${esc(g.label)}</code></p></details></div>
<div class="crops"><figure><figcaption>Before</figcaption>${crop(p.shot.before, p.size.before, before, g.gone === 'added' ? g.ctx : undefined, markB)}</figure><span class="arrow" aria-hidden="true">→</span><figure><figcaption>After</figcaption>${crop(p.shot.after, p.size.after, after, g.gone === 'removed' ? g.ctx : undefined, markA)}</figure></div></article>`
  }
  const boxes = (p, side) => [...p.items].reverse().flatMap((g) => g[side].map((r, j) => {
    const cls = g.kind === 'fixed' ? (side === 'before' ? 'bad' : 'ok') : g.kind === 'new' && side === 'after' ? 'bad' : 'chg'
    return `<a class="box ${cls}" href="#p${pages.indexOf(p)}-${g.n}" data-r="${r.x},${r.y},${r.w},${r.h}">${j === 0 ? `<span>${g.n}</span>` : ''}</a>`
  })).join('')
  const sections = pages.map((p, i) => {
    const head = `<h2>${esc(p.a.url)} <small>${p.a.viewport.tag}px · score ${p.b.score} → ${p.a.score}</small></h2>`
    if (p.skipped) return `<section id="p${i}">${head}<p class="error">${esc(p.skipped)}</p></section>`
    const warn = (p.byOrder ? `<p class="error">Paired by order: no page at ${esc(p.b.url)} in the after scan. If these are different screens, rescan with the same URLs.</p>` : '')
      + (p.matchRate < 0.6 ? `<p class="error">Only ${Math.round(p.matchRate * 100)}% of elements could be paired: the structure changed a lot, so some cards may be moves, not changes.</p>` : '')
    return `<section id="p${i}">${head}
<p class="meta">Tells gone: ${tellIds(p.pageFixed) || 'none'} · new: ${tellIds(p.pageNew) || 'none'}</p>${warn}
${p.items.map((g) => card(p, i, g)).join('') || '<p>No visible change.</p>'}${p.more ? `<p class="meta">… ${p.more} more changes not shown</p>` : ''}
<details class="full"><summary>Full screenshots, every change boxed</summary><div class="pair">
<figure><figcaption>Before</figcaption><div class="shot" data-size="${p.size.before.join(',')}"><img src="${esc(p.shot.before)}" alt="Before, ${esc(p.b.url)} at ${p.b.viewport.tag}px">${boxes(p, 'before')}</div></figure>
<figure><figcaption>After</figcaption><div class="shot" data-size="${p.size.after.join(',')}"><img src="${esc(p.shot.after)}" alt="After, ${esc(p.a.url)} at ${p.a.viewport.tag}px">${boxes(p, 'after')}</div></figure></div></details></section>`
  }).join('')
  return `<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>sloplint diff</title>
<style>
:root { --ink:#1b1f24; --muted:#59636e; --line:#d8dee4; --bg:#fff; --soft:#f6f8fa; --accent:#0b57d0; --ok:#1a7f37; --bad:#cf222e; --chg:#0b57d0; }
* { box-sizing:border-box } body { margin:0; background:var(--soft); color:var(--ink); font:15px/1.55 system-ui,-apple-system,"Segoe UI",sans-serif }
main { max-width:1240px; margin:0 auto; padding:24px 16px 64px } h1 { margin:0 0 4px; font-size:22px } h2 { margin:0 0 2px; font-size:17px; overflow-wrap:anywhere }
h2 small, .meta, .lead { color:var(--muted); font-weight:400 } a { color:var(--accent) } .error { color:var(--bad) }
table { border-collapse:collapse } .summary { width:100%; margin:16px 0 8px; background:var(--bg); border:1px solid var(--line); border-radius:8px }
.summary th, .summary td { padding:8px 12px; border-bottom:1px solid var(--line); text-align:left; vertical-align:top; overflow-wrap:anywhere } .summary th { font-weight:600; color:var(--muted) }
.num { text-align:right; font-variant-numeric:tabular-nums }
section { padding:32px 0 8px } code { font-size:13px; overflow-wrap:anywhere }
.card { display:grid; grid-template-columns:minmax(260px,1fr) auto; gap:24px; margin-top:12px; padding:16px 20px; background:var(--bg); border:1px solid var(--line); border-radius:8px; scroll-margin-top:12px }
.card:target { outline:3px solid #f2c94c }
.card h3 { display:flex; flex-wrap:wrap; align-items:baseline; gap:6px; margin:0 0 8px; font-size:15px; font-weight:600 }
.sentence { margin:0 0 6px }
.n { display:inline-block; min-width:24px; padding:0 6px; color:#fff; font:600 13px/22px system-ui,sans-serif; text-align:center; border-radius:4px; background:var(--chg) }
.n.k-fixed { background:var(--ok) } .n.k-new { background:var(--bad) }
.tells { margin:4px 0 } .tells.ok { color:var(--ok) } .tells.bad { color:var(--bad) }
.sw { display:inline-block; width:12px; height:12px; margin:0 2px; border:1px solid var(--line); border-radius:2px; vertical-align:-1px }
details summary { cursor:pointer; color:var(--muted) } .raw { margin:8px 0; font-size:13px } .raw th, .raw td { padding:3px 8px 3px 0; text-align:left; vertical-align:top } .raw th { font-weight:600 }
.crops { display:flex; align-items:center; gap:12px } .crops figure { margin:0 } figcaption { margin-bottom:4px; color:var(--muted); font-size:13px; font-weight:600 }
.arrow { color:var(--muted); font-size:20px }
.crop { position:relative; background-repeat:no-repeat; border:1px solid var(--line); border-radius:6px; overflow:hidden }
.crop-none { display:grid; place-items:center; width:200px; height:72px; color:var(--muted); font-size:13px; border:1px dashed var(--line); border-radius:6px }
.mark { position:absolute; border:2px solid var(--chg); border-radius:3px; margin:-3px 0 0 -3px; padding:2px; box-sizing:content-box } .mark.bad { border-color:var(--bad) } .mark.ok { border-color:var(--ok) }
.full { margin-top:16px } .pair { display:grid; grid-template-columns:1fr 1fr; gap:16px; margin-top:8px } .pair figure { margin:0; min-width:0 }
.shot { position:relative; border:1px solid var(--line); border-radius:6px; overflow:hidden; background:var(--bg) } .shot img { display:block; width:100% }
.box { position:absolute; border:2px solid var(--chg); text-decoration:none } .box.bad { border-color:var(--bad) } .box.ok { border-color:var(--ok) }
.box span { position:absolute; left:-2px; top:-18px; padding:0 4px; color:#fff; background:var(--chg); font:600 11px/16px system-ui,sans-serif } .box.bad span { background:var(--bad) } .box.ok span { background:var(--ok) }
@media (max-width: 960px) { .card { grid-template-columns:1fr } .crops { flex-wrap:wrap } }
@media (max-width: 640px) { .pair { grid-template-columns:1fr } .crop { max-width:100% } }
</style></head><body><main>
<h1>sloplint diff</h1><p class="lead">Every visible change between two scans of the same screens. Each card says what changed and shows the same region before and after; <span style="color:var(--ok)">green</span> marks a tell that was fixed, <span style="color:var(--bad)">red</span> one that appeared, <span style="color:var(--chg)">blue</span> any other change.</p>
<table class="summary"><thead><tr><th>Page</th><th class="num">Width</th><th class="num">Score</th><th>Tells gone</th><th>New tells</th><th class="num">Changes</th></tr></thead><tbody>${rows}</tbody></table>
${sections}</main>
<script>
// Boxes are stored in page pixels; scale them to the screenshot as drawn.
function place(shot) {
  const [w, h] = shot.dataset.size.split(',').map(Number)
  const s = shot.clientWidth / w
  for (const b of shot.querySelectorAll('.box')) {
    const [x, y, bw, bh] = b.dataset.r.split(',').map(Number)
    b.hidden = y >= h || x >= w
    Object.assign(b.style, { left: x * s + 'px', top: y * s + 'px', width: Math.max(bw * s, 4) + 'px', height: Math.max(bh * s, 4) + 'px' })
  }
}
const all = () => document.querySelectorAll('.full[open] .shot').forEach(place)
document.querySelectorAll('.full').forEach((d) => d.addEventListener('toggle', all))
addEventListener('resize', all)
</script></body></html>`
}

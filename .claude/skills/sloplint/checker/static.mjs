// sloplint static scanner: finds the source-level tells (see tells.json, detect includes "static")
// in CSS/SCSS/LESS and in markup (TSX/JSX/HTML/Vue/Svelte/Astro, including Tailwind class strings).
// Usage: node static.mjs <dir> [--config .slop.json] [--json]
import { readFileSync, readdirSync, statSync, existsSync, realpathSync } from 'node:fs'
import { extname, join, relative, resolve, dirname } from 'node:path'
import { fileURLToPath } from 'node:url'

const HERE = dirname(fileURLToPath(import.meta.url))
const STYLE = new Set(['.css', '.scss', '.sass', '.less', '.pcss'])
const MARKUP = new Set(['.tsx', '.jsx', '.ts', '.js', '.html', '.vue', '.svelte', '.astro', '.mdx'])
const SKIP_DIRS = new Set(['node_modules', '.git', 'dist', 'build', 'out', '.next', '.nuxt', '.svelte-kit', 'coverage', 'vendor', 'wwwroot', '.turbo', '.cache'])

/** Blank comments out but keep every newline, so line numbers stay true. */
function stripComments(src, ext) {
  const blank = (m) => m.replace(/[^\n]/g, ' ')
  let out = src.replace(/\/\*[\s\S]*?\*\//g, blank)
  if (!STYLE.has(ext) || ext === '.scss' || ext === '.less') out = out.replace(/(^|[^:"'`\\])\/\/[^\n]*/g, (m, p) => p + blank(m.slice(p.length)))
  out = out.replace(/<!--[\s\S]*?-->/g, blank)
  return out
}

const lineOf = (src, index) => src.slice(0, index).split('\n').length
/** The selector of the CSS block a match sits in ('' outside a block). */
const selectorAt = (src, index) => {
  const open = src.lastIndexOf('{', index)
  if (open < 0 || src.lastIndexOf('}', index) > open) return ''
  const prev = Math.max(src.lastIndexOf('}', open), src.lastIndexOf(';', open))
  return src.slice(prev + 1, open).trim()
}
const LOADING = /skeleton|shimmer|spin|loader|loading|progress|busy/i

// Each rule: id, where it applies ('style' | 'markup' | 'any'), a regex (global), and a message.
// `test` lets a rule reject a raw match after looking at it.
const RULES = [
  { id: 'C1', where: 'style', re: /(?<!repeating-)(linear|radial|conic)-gradient\(([^;]*)/g, msg: 'gradient fill',
    test: (m, sel) => !/transparent|rgba\([^)]*,\s*0\)|#[0-9a-f]{6}00\b/i.test(m[2]) && !LOADING.test(sel) },
  { id: 'C1', where: 'markup', re: /\b(bg-gradient-to-[trbl]{1,2}|bg-linear-to-[trbl]{1,2}|bg-radial|bg-conic)\b/g, msg: 'Tailwind gradient' },
  { id: 'C2', where: 'any', re: /background-clip:\s*text|\bbg-clip-text\b/g, msg: 'gradient text' },
  { id: 'C3', where: 'any', re: /#(6366f1|8b5cf6|7c3aed|a855f7|4f46e5|6d28d9|9333ea|818cf8|a78bfa)\b|\b(?:bg|from|to|via|text|border|ring)-(indigo|violet|purple)-[4-7]00\b/gi, msg: 'default indigo/violet' },
  { id: 'C6', where: 'markup', re: /\b(?:bg|text|border|ring|from|via|to|fill|stroke)-(slate|gray|zinc|neutral|stone|red|orange|amber|yellow|lime|green|emerald|teal|cyan|sky|blue|indigo|violet|purple|fuchsia|pink|rose)-(50|[1-9]00|950)\b/g, msg: 'stock palette class', perFile: 8 },
  { id: 'C6', where: 'style', re: /--(primary|background|foreground|card|muted|accent|ring):\s*(222\.2 47\.4% 11\.2%|240 5\.9% 10%|221\.2 83\.2% 53\.3%|262\.1 83\.3% 57\.8%|0 0% 100%|222\.2 84% 4\.9%|210 40% 96\.1%)|#(0f172a|1e293b|334155|64748b|94a3b8|e2e8f0|f1f5f9|f8fafc)\b/gi, msg: 'stock shadcn/Tailwind value', perFile: 3 },
  { id: 'T3', where: 'markup', re: /["'`][^"'`]*\buppercase\b[^"'`]*\btracking-(wide|wider|widest|\[[^\]]+\])[^"'`]*["'`]|["'`][^"'`]*\btracking-(wide|wider|widest|\[[^\]]+\])[^"'`]*\buppercase\b[^"'`]*["'`]/g, msg: 'uppercase + tracking' },
  { id: 'S3', where: 'style', re: /border-(left|right|top)(-width)?:\s*[3-9]px|box-shadow:\s*inset\s+-?[3-9]px\s+0(px)?\s+0/g, msg: 'one-sided thick border' },
  { id: 'S3', where: 'markup', re: /\bborder-[lrt]-(4|8|\[[3-9]px\])\b/g, msg: 'Tailwind side stripe' },
  // A small dark text-shadow that keeps captions legible over video or photos is not a glow.
  { id: 'S6', where: 'style', re: /text-shadow:[^;]*(\b[1-9]\d+px|#(?!000|0000)[0-9a-f]{3,8}\b(?<!#[0-9a-f]{3}[0-9]|#000[0-9a-f])|rgba?\((?!\s*0\s*,\s*0\s*,\s*0)[^)]*\))[^;]*|box-shadow:[^;]*\b0(px)?\s+0(px)?\s+([1-9]\d+)px[^;]*(#(?!000)[0-9a-f]{3,8}|rgba?\((?!\s*0\s*,\s*0\s*,\s*0)[^)]*\))/gi, msg: 'glow / text-shadow' },
  { id: 'S6', where: 'markup', re: /\bshadow-\[0_0_[^\]]+\]|\bshadow-(indigo|violet|purple|blue|cyan|pink|fuchsia|emerald|sky)-\d+(\/\d+)?\b|\bdrop-shadow-\[[^\]]+\]|\bdrop-shadow-glow\b/g, msg: 'Tailwind glow' },
  { id: 'S7', where: 'any', re: /backdrop-filter:\s*blur|\bbackdrop-blur(-\w+)?\b/g, msg: 'backdrop blur' },
  { id: 'S9', where: 'style', re: /filter:\s*blur\(\s*([2-9]\d|\d{3,})px/g, msg: 'large blur (blob)' },
  { id: 'S9', where: 'markup', re: /\bblur-(2xl|3xl)\b|\bblur-\[(\d{2,})px\]/g, msg: 'Tailwind large blur (blob)' },
  { id: 'L8', where: 'style', re: /content:\s*["'][^"']*\p{Extended_Pictographic}/gu, msg: 'emoji in CSS content' },
  { id: 'L8', where: 'markup', re: /["'`][^"'`\n]{0,40}\p{Extended_Pictographic}[^"'`\n]{0,40}["'`]|\p{Extended_Pictographic}\s*\{/gu, msg: 'emoji in a string or before an expression', test: (m) => !/[©®™]/.test(m[0]) },
  { id: 'L11', where: 'style', re: /content:\s*["'][^"']*(→|\\2192|·|\\b7)/g, msg: 'arrow or middle dot in CSS content' },
  { id: 'S9', where: 'markup', re: /\bbg-\[radial-gradient[^\]]*\]|\bblur-\[\d{2,}px\]/g, msg: 'Tailwind radial blob' },
  { id: 'M4', where: 'style', re: /(\.fade-?(in-?)?up|\.reveal|\[data-aos[^\]]*\]|\.aos-[\w-]+|\.slide-?up)\b[^{]*\{/g, msg: 'scroll-reveal class in CSS' },
  { id: 'S6', where: 'style', re: /box-shadow:[^;]*\b\d+(px)?\s+([4-9]\d|\d{3,})px\s+([6-9]\d|\d{3,})px/g, msg: 'dramatic drop shadow' },
  { id: 'A2', where: 'style', re: /([^{}]+)\{[^{}]*outline-style:\s*none[^{}]*\}/g, msg: 'outline-style removed', test: (m, _sel, css) => !/:focus-visible/.test(m[1]) && !focusDrawn(css, m[1]) },
  { id: 'L8', where: 'markup', re: /(?<![\w$])(Sparkles?|WandSparkles|SparklesIcon)(?![\w$])/g, msg: 'sparkle icon' },
  { id: 'L8', where: 'markup', re: />[^<{}]*\p{Extended_Pictographic}[^<{}]*</gu, msg: 'emoji in markup text', test: (m) => !/[©®™]/.test(m[0]) },
  { id: 'L11', where: 'markup', re: /['"`]\s·\s['"`]|\}\s·\s\{|\s·\s\$\{|>\s*[^<{]*\s·\s[^<{]*</g, msg: 'middle-dot join' },
  { id: 'L11', where: 'markup', re: />[^<{]*\S\s*(→|-&gt;)\s*(<|\{)|['"][^'"]*\S\s(→)['"]|>\s*(→)\s*</g, msg: 'arrow glued to label' },
  { id: 'M1', where: 'style', re: /animation(-iteration-count)?:[^;]*\binfinite\b/g, msg: 'infinite animation', test: (m, sel) => !LOADING.test(sel) },
  { id: 'M1', where: 'markup', re: /\banimate-(pulse|ping|bounce)\b/g, msg: 'Tailwind looping animation' },
  { id: 'M2', where: 'markup', re: /\b(?:group-)?hover:(scale-1\d\d|-translate-y-[\w[\]]+|translate-y-[\w[\]]+)\b/g, msg: 'hover lift/scale' },
  { id: 'M3', where: 'any', re: /transition(-property)?:\s*all\b|\btransition-all\b/g, msg: 'transition: all' },
  { id: 'M4', where: 'markup', re: /\bdata-aos=|\bwhileInView\b|\buseInView\b|\banimate-(fade-in-up|fade-up|slide-up)\b|\bfade-?(in-?)?up\b/g, msg: 'scroll reveal' },
  { id: 'A2', where: 'style', re: /([^{}]+)\{[^{}]*outline:\s*(none|0)\b[^{}]*\}/g, msg: 'outline removed',
    test: (m, _sel, css) => !/:focus-visible/.test(m[1]) && !focusDrawn(css, m[1]) },
  { id: 'A2', where: 'markup', re: /["'`][^"'`]*\b(focus:)?outline-none\b[^"'`]*["'`]/g, msg: 'outline-none',
    test: (m) => !/focus-visible:(ring|outline)/.test(m[0]) },
]

// M2 in CSS needs the selector and the block together.
function hoverLifts(src) {
  const out = []
  for (const m of src.matchAll(/([^{}]+)\{([^{}]*)\}/g)) {
    if (!m[1].includes(':hover')) continue
    if (/transform:\s*[^;]*(translateY\(\s*-|translate\([^,]+,\s*-|scale\(\s*1\.[0-9])|translate:\s*0\s+-|scale:\s*1\.[0-9]/.test(m[2])) out.push({ index: m.index + m[0].indexOf('{'), sel: m[1].trim() })
  }
  return out
}
// T3 in CSS: uppercase and letter-spacing in the same block.
function capsBlocks(src) {
  const out = []
  for (const m of src.matchAll(/([^{}]+)\{([^{}]*)\}/g)) {
    if (/text-transform:\s*uppercase/.test(m[2]) && /letter-spacing:\s*(0?\.\d+|[1-9][\d.]*)(em|px|rem)/.test(m[2]) && !/letter-spacing:\s*-/.test(m[2])) out.push({ index: m.index + m[0].indexOf('{'), sel: m[1].trim() })
  }
  return out
}

function* files(dir) {
  for (const name of readdirSync(dir)) {
    if (SKIP_DIRS.has(name) || name.startsWith('.') && name !== '.storybook') continue
    const p = join(dir, name)
    const st = statSync(p)
    if (st.isDirectory()) yield* files(p)
    else if ((STYLE.has(extname(p)) || MARKUP.has(extname(p))) && st.size < 2_000_000 && !/\.min\.(css|js)$/.test(p)) yield p
  }
}

/** Whether the stylesheet draws focus for these selectors another way (a :focus-visible ring or shadow). */
function focusDrawn(css, selectors) {
  const esc = (x) => x.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
  return selectors.split(',').map((x) => x.trim()).filter(Boolean).every((sel) =>
    new RegExp(`${esc(sel)}:focus(-visible)?[^{]*\\{[^}]*(box-shadow|outline|border-color)`).test(css))
}

export function scanStatic(root, { allow = {} } = {}) {
  const findings = []
  let anyMotion = false
  let anyReduced = false
  // An exception with a selector applies to a rule whose selector names one of the listed parts.
  const allowed = (id, sel = '') => {
    const a = allow[id]
    if (!a) return false
    if (typeof a !== 'object' || !a.selector) return true
    return a.selector.split(',').map((x) => x.replace(/\s*\*\s*$/, '').trim()).filter(Boolean)
      .some((part) => new RegExp(`(^|[\\s>+~,(])${part.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}(?![\\w-])`).test(sel))
  }
  for (const file of files(root)) {
    const ext = extname(file)
    const raw = readFileSync(file, 'utf8')
    const src = stripComments(raw, ext)
    const kind = STYLE.has(ext) ? 'style' : 'markup'
    // Vue/Svelte/Astro/HTML carry <style> blocks: scan those as style too.
    const styleChunks = kind === 'style' ? [{ text: src, offset: 0 }] : [...src.matchAll(/<style[^>]*>([\s\S]*?)<\/style>/g)].map((m) => ({ text: m[1], offset: m.index + m[0].indexOf(m[1]) }))
    if (/@keyframes|animation:|\banimate-|transition:[^;]*transform|whileInView|data-aos/.test(src)) anyMotion = true
    if (/prefers-reduced-motion|motion-reduce:|motion-safe:|useReducedMotion/.test(src)) anyReduced = true
    const push = (id, index, msg, text) => {
      const sel = selectorAt(src, index)
      if (allowed(id, sel.split('\n').pop())) return
      findings.push({ id, file: relative(root, file), line: lineOf(src, index), msg, selector: sel.split('\n').pop().slice(0, 80), text: text.trim().replace(/\s+/g, ' ').slice(0, 100) })
    }
    const perFile = {}
    for (const rule of RULES) {
      const targets = rule.where === 'style' ? styleChunks : rule.where === 'markup' ? (kind === 'markup' ? [{ text: src, offset: 0 }] : []) : [{ text: src, offset: 0 }]
      for (const t of targets) {
        for (const m of t.text.matchAll(rule.re)) {
          if (rule.test && !rule.test(m, selectorAt(t.text, m.index), t.text)) continue
          // A match that starts with whitespace before its selector points at the selector's line.
          const at = t.offset + m.index + (m[0].length - m[0].trimStart().length)
          if (rule.perFile) { perFile[rule.id] = perFile[rule.id] ?? []; perFile[rule.id].push(at); continue }
          push(rule.id, at, rule.msg, m[0])
        }
      }
    }
    for (const rule of RULES.filter((r) => r.perFile)) {
      const hits = perFile[rule.id] ?? []
      if (hits.length >= rule.perFile) push(rule.id, hits[0], `${hits.length} ${rule.msg}es in this file`, '')
    }
    for (const t of styleChunks) {
      for (const h of hoverLifts(t.text)) push('M2', t.offset + h.index, 'hover lift/scale', h.sel)
      for (const h of capsBlocks(t.text)) push('T3', t.offset + h.index, 'uppercase + letter-spacing', h.sel)
    }
  }
  if (anyMotion && !anyReduced && !allowed('M5')) findings.push({ id: 'M5', file: '(project)', line: 0, msg: 'animations but no prefers-reduced-motion anywhere', text: '' })
  return findings
}

// CLI
// npx and npm link start the CLI through a symlink in node_modules/.bin.
if (process.argv[1] && realpathSync(process.argv[1]) === realpathSync(fileURLToPath(import.meta.url))) {
  const args = process.argv.slice(2)
  const dir = resolve(args.find((a) => !a.startsWith('--')) ?? '.')
  const cfgPath = args.includes('--config') ? args[args.indexOf('--config') + 1] : join(dir, '.slop.json')
  const config = existsSync(cfgPath) ? JSON.parse(readFileSync(cfgPath, 'utf8')) : {}
  const registry = JSON.parse(readFileSync(join(HERE, '..', 'tells.json'), 'utf8'))
  const byId = Object.fromEntries(registry.tells.map((t) => [t.id, t]))
  const found = scanStatic(dir, config)
  if (args.includes('--json')) { console.log(JSON.stringify(found, null, 2)); process.exit(0) }
  const grouped = Object.groupBy ? Object.groupBy(found, (f) => f.id) : found.reduce((m, f) => ((m[f.id] ??= []).push(f), m), {})
  for (const [id, list] of Object.entries(grouped).sort()) {
    const t = byId[id]
    console.log(`\n${id} ${t?.title ?? ''} (${list.length})  → ${t?.fix ?? ''}`)
    for (const f of list.slice(0, 15)) console.log(`  ${f.file}:${f.line}  ${f.msg}${f.selector ? `  [${f.selector}]` : ''}${f.text ? `  ${f.text}` : ''}`)
    if (list.length > 15) console.log(`  … ${list.length - 15} more`)
  }
  console.log(`\n${found.length} findings, ${Object.keys(grouped).length} distinct tells`)
  process.exit(found.length ? 1 : 0)
}

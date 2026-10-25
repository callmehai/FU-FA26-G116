// sloplint in-page checker. One arrow function with no imports, so it runs wherever a page can
// evaluate a function:
//   Playwright:       await page.evaluate(`(${source})(${JSON.stringify(options)})`)
//   chrome-devtools:  evaluate_script with this file's text as `function`
// It reads computed styles of what is rendered, in the spirit of Adrian Krebs' design-slop study
// (deterministic DOM checks rather than a model judging a screenshot). Tell ids match tells.json.
//
// options: {
//   ignore?: string[]                       selectors whose subtree is never reported
//   allow?: { [id]: string | true | { selector?: string, reason?: string } }
//                                           findings that a project has sanctioned (kept, marked allowed)
//   copy?: { buzzwords?: {en,vi}, genericCta?: {en,vi}, templateLeak?: string[] }
//   mark?: boolean                          set data-slop-found="ID ..." on reported, not allowed elements (tests, diff)
//   limit?: number                          max findings per tell (default 25)
// }
// returns { url, viewport, findings: [{ id, label, selector, rect, detail, allowed?, reason? }], stats }
(options = {}) => {
  const opts = { ignore: [], allow: {}, copy: {}, mark: false, limit: 25, ...options }
  const findings = []
  const counts = {}

  // ---------- helpers ----------
  const cs = (e, pseudo) => getComputedStyle(e, pseudo)
  const px = (v) => parseFloat(v) || 0
  const ignored = (e) => opts.ignore.some((s) => { try { return e.closest(s) } catch { return false } })
  const hidden = (e) => {
    const s = cs(e)
    if (s.display === 'none' || s.visibility === 'hidden') return true
    // Opacity 0 is hidden, unless it is waiting for a scroll-reveal (M4 needs to see those).
    if (s.opacity === '0' && s.animationName === 'none' && !e.matches('[data-aos], [data-animate], [data-reveal], [class*="fade"], [class*="reveal"]')) return true
    // An ancestor at opacity 0 or visibility hidden (a closed menu) hides it too; reveal-on-scroll waits are still content.
    if (e.checkVisibility && !e.checkVisibility({ checkOpacity: true, checkVisibilityCSS: true }) &&
      !e.closest('[data-aos], [data-animate], [data-reveal], [class*="fade"], [class*="reveal"]')) return true
    const r = e.getBoundingClientRect()
    if (r.width < 1 || r.height < 1) return true
    // visually-hidden pattern: clipped 1px boxes
    if (r.width <= 2 && r.height <= 2) return true
    return false
  }
  const all = [...document.querySelectorAll('body *')].filter(
    (e) => !['SCRIPT', 'STYLE', 'NOSCRIPT', 'TEMPLATE', 'META', 'LINK', 'BR'].includes(e.tagName) && !hidden(e) && !ignored(e),
  )
  const ownText = (e) =>
    [...e.childNodes].filter((n) => n.nodeType === 3).map((n) => n.textContent).join('').replace(/\s+/g, ' ').trim()
  const text = (e) => (e.innerText ?? e.textContent ?? '').replace(/\s+/g, ' ').trim()
  const short = (t, n = 48) => (t.length > n ? `${t.slice(0, n - 1)}…` : t)
  const cssPath = (e) => {
    const parts = []
    let n = e
    while (n && n.nodeType === 1 && parts.length < 4 && n !== document.body) {
      if (n.id && /^[A-Za-z][\w-]*$/.test(n.id)) { parts.unshift(`#${n.id}`); break }
      let p = n.tagName.toLowerCase()
      const cls = [...n.classList].filter((c) => /^[A-Za-z_][\w-]*$/.test(c)).slice(0, 2)
      if (cls.length) p += `.${cls.join('.')}`
      const sibs = n.parentElement ? [...n.parentElement.children].filter((c) => c.tagName === n.tagName) : []
      if (sibs.length > 1) p += `:nth-of-type(${sibs.indexOf(n) + 1})`
      parts.unshift(p)
      n = n.parentElement
    }
    return parts.join(' > ')
  }
  const rectOf = (e) => {
    const r = e.getBoundingClientRect()
    return { x: Math.round(r.left + scrollX), y: Math.round(r.top + scrollY), w: Math.round(r.width), h: Math.round(r.height) }
  }
  const allowFor = (id, e) => {
    const a = opts.allow[id]
    if (!a) return null
    if (a === true || typeof a === 'string') return { reason: a === true ? 'allowed' : a }
    if (!a.selector) return { reason: a.reason ?? 'allowed' }
    try { return e && e.closest(a.selector) ? { reason: a.reason ?? 'allowed' } : null } catch { return null }
  }
  const add = (id, e, detail = '', strong = false) => {
    counts[id] = (counts[id] ?? 0) + 1
    if (counts[id] > opts.limit) return
    const f = { id, detail }
    if (strong) f.strong = true
    if (e) {
      f.label = `${e.tagName.toLowerCase()}${e.classList.length ? '.' + [...e.classList].slice(0, 2).join('.') : ''} «${short(text(e), 40)}»`
      f.selector = cssPath(e)
      f.rect = rectOf(e)
    } else {
      f.label = 'page'
    }
    const a = allowFor(id, e)
    if (a) { f.allowed = true; f.reason = a.reason }
    else if (e && opts.mark) e.setAttribute('data-slop-found', `${e.getAttribute('data-slop-found') ?? ''} ${id}`.trim())
    findings.push(f)
  }

  // colour maths
  const parseColor = (c) => {
    const m = c.match(/rgba?\(([^)]+)\)/)
    if (!m) return null
    const [r, g, b, a = 1] = m[1].split(/[\s,/]+/).filter(Boolean).map(Number)
    return { r, g, b, a }
  }
  const hsl = ({ r, g, b }) => {
    const R = r / 255, G = g / 255, B = b / 255
    const max = Math.max(R, G, B), min = Math.min(R, G, B), l = (max + min) / 2
    if (max === min) return { h: 0, s: 0, l }
    const d = max - min
    const s = d / (1 - Math.abs(2 * l - 1))
    let h = max === R ? ((G - B) / d) % 6 : max === G ? (B - R) / d + 2 : (R - G) / d + 4
    return { h: (h * 60 + 360) % 360, s, l }
  }
  const lum = ({ r, g, b }) => {
    const f = (v) => { v /= 255; return v <= 0.03928 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4 }
    return 0.2126 * f(r) + 0.7152 * f(g) + 0.0722 * f(b)
  }
  const contrast = (a, b) => { const x = lum(a), y = lum(b); return (Math.max(x, y) + 0.05) / (Math.min(x, y) + 0.05) }
  const solid = (c) => { const p = parseColor(c); return p && p.a > 0.05 ? p : null }
  const blend = (top, under) => ({
    r: top.r * top.a + under.r * (1 - top.a), g: top.g * top.a + under.g * (1 - top.a), b: top.b * top.a + under.b * (1 - top.a), a: 1,
  })
  // Effective background behind an element; null when an image or gradient is in the way (cannot know).
  // A gradient background reads as the average of its stops; an image cannot be judged.
  const gradientAverage = (bg) => {
    if (/url\(/.test(bg)) return null
    const stops = (bg.match(/rgba?\([^)]*\)/g) ?? []).map(parseColor).filter((c) => c && c.a > 0.05)
    if (!stops.length) return undefined
    const n = stops.length
    return { r: stops.reduce((a, c) => a + c.r, 0) / n, g: stops.reduce((a, c) => a + c.g, 0) / n, b: stops.reduce((a, c) => a + c.b, 0) / n, a: Math.min(...stops.map((c) => c.a)) }
  }
  const bgBehind = (e) => {
    const layers = []
    for (let n = e; n; n = n.parentElement) {
      const s = cs(n)
      if (s.backgroundImage !== 'none') {
        const avg = gradientAverage(s.backgroundImage)
        if (avg === null) return null
        if (avg) { layers.push(avg); if (avg.a >= 0.99) break; continue }
      }
      const c = solid(s.backgroundColor)
      if (c) { layers.push(c); if (c.a >= 0.99) break }
    }
    let base = { r: 255, g: 255, b: 255, a: 1 }
    for (const c of layers.reverse()) base = blend(c, base)
    return base
  }
  // Background under a point, from the real paint stack: null when an image, gradient or media is there.
  const bgAt = (e, skipSelf = false) => {
    const r = e.getBoundingClientRect()
    const x = r.left + Math.min(r.width / 2, 12), y = r.top + r.height / 2
    if (x < 0 || y < 0 || x > vw || y > vh) return undefined
    const stack = document.elementsFromPoint(x, y)
    // The element is not what is painted at that point (covered, pointer-events): use its ancestors.
    if (!stack.includes(e)) return undefined
    const start = stack.indexOf(e) + (skipSelf ? 1 : 0)
    const layers = []
    for (const n of start >= 0 ? stack.slice(start) : stack) {
      const s = cs(n)
      if (n.matches('img, video, canvas, picture, iframe')) return null
      if (s.backgroundImage !== 'none') {
        const avg = gradientAverage(s.backgroundImage)
        if (avg === null) return null
        if (avg) { layers.push(avg); if (avg.a >= 0.99) break; continue }
      }
      const c = solid(s.backgroundColor)
      if (c) { layers.push(c); if (c.a >= 0.99) break }
    }
    let base = { r: 255, g: 255, b: 255, a: 1 }
    for (const c of layers.reverse()) base = blend(c, base)
    return base
  }
  const painted = (s) => !!solid(s.backgroundColor) || s.backgroundImage !== 'none'
  const bordered = (s) => ['Top', 'Right', 'Bottom', 'Left'].every((k) => px(s[`border${k}Width`]) >= 1 && s[`border${k}Style`] !== 'none')
  const shadows = (v) =>
    v === 'none' ? [] : v.split(/,(?![^(]*\))/).map((part) => {
      const color = part.match(/rgba?\([^)]*\)/)?.[0] ?? 'rgb(0,0,0)'
      const nums = part.replace(/rgba?\([^)]*\)/, '').replace('inset', '').trim().split(/\s+/).map(px)
      const [x = 0, y = 0, blur = 0, spread = 0] = nums
      return { inset: part.includes('inset'), x, y, blur, spread, color: parseColor(color) ?? { r: 0, g: 0, b: 0, a: 1 } }
    })
  const dropShadow = (v) => shadows(v).some((s) => !s.inset && s.color.a > 0.02 && (s.x || s.y || s.blur))
  const interactive = (e) => e.matches('button, a, input, select, textarea, summary, [role="button"], [role="link"], [role="tab"], [role="option"], [role="menuitem"], [tabindex]:not([tabindex="-1"])')
  const overlay = (e) => {
    const s = cs(e)
    return e.matches('dialog, [popover], [role="dialog"], [role="menu"], [role="listbox"], [role="tooltip"], [aria-modal="true"]') ||
      s.position === 'fixed' || ((s.position === 'absolute' || s.position === 'sticky') && s.zIndex !== 'auto' && Number(s.zIndex) > 1)
  }
  const loading = (e) => !!e.closest('[aria-busy="true"], [role="progressbar"], [class*="skeleton"], [class*="spinner"], [class*="loader"], [class*="loading"]')
  const family = (e) => cs(e).fontFamily.split(',')[0].replace(/["']/g, '').trim().toLowerCase()
  const isMono = (e) => /mono|courier|consolas|menlo|monaco|sfmono|jetbrains|fira code|ibm plex mono|source code/.test(cs(e).fontFamily.toLowerCase())
  const textEls = all.filter((e) => ownText(e).length > 0)
  const vw = innerWidth, vh = innerHeight
  const inFirstScreen = (e) => { const r = e.getBoundingClientRect(); return r.top < vh && r.bottom > 0 }

  // ---------- colour ----------
  const gradientStops = (bg) => (bg.match(/rgba?\([^)]*\)/g) ?? []).map(parseColor).filter(Boolean)
  const decorativeGradient = (bg) => {
    if (!/(linear|radial|conic)-gradient\(/.test(bg) || /^repeating-/.test(bg.trim())) return false
    const stops = gradientStops(bg)
    if (stops.length < 2) return false
    // A fade to transparent is a mask (scroll shadow, text fade), not decoration.
    if (stops.some((c) => c.a < 0.05)) return false
    const hs = stops.map(hsl)
    const spread = Math.max(...hs.map((h) => h.l)) - Math.min(...hs.map((h) => h.l))
    const hueSpread = Math.max(...hs.map((h) => h.h)) - Math.min(...hs.map((h) => h.h))
    return spread > 0.04 || (hueSpread > 10 && hs.some((h) => h.s > 0.2))
  }
  for (const e of all) {
    if (loading(e)) continue
    for (const pseudo of [null, '::before', '::after']) {
      const s = cs(e, pseudo)
      if (pseudo && (s.content === 'none' || s.content === 'normal' || s.display === 'none' || s.visibility === 'hidden')) continue
      const bg = s.backgroundImage
      // C2 gradient text
      if (!pseudo && (s.backgroundClip === 'text' || s.webkitBackgroundClip === 'text') && bg.includes('gradient')) { add('C2', e, short(bg, 60)); continue }
      // S9 blob: a big blurred coloured shape, or a radial wash behind content
      const blurPx = px((s.filter.match(/blur\(([\d.]+)px\)/) ?? [])[1])
      const r = e.getBoundingClientRect()
      if (blurPx >= 16 && (painted(s)) && (pseudo || r.width >= 80)) { add('S9', e, `filter: blur(${blurPx}px)${pseudo ? ' on ' + pseudo : ''}`); continue }
      if (/radial-gradient\(/.test(bg) && decorativeGradient(bg) && (pseudo || s.position === 'absolute' || r.width >= vw * 0.6)) {
        const stops = gradientStops(bg).map(hsl)
        if (stops.some((h) => h.s > 0.3)) { add('S9', e, `radial wash${pseudo ? ' on ' + pseudo : ''}`); continue }
      }
      if (!pseudo && bg.includes('gradient') && /gradient/.test(s.maskImage || s.webkitMaskImage || '') && r.width >= vw * 0.5) { add('S9', e, 'masked grid/glow backdrop'); continue }
      if (decorativeGradient(bg) && !e.closest('[role="progressbar"], [role="meter"], progress, meter, [class*="progress"], [class*="meter"], [class*="chart"], [class*="graph"], [class*="heatmap"]')) {
        const area = (r.width * r.height) / (vw * vh)
        add('C1', e, `${area >= 0.2 ? 'large surface; ' : ''}${pseudo ?? ''} ${short(bg.replace(/\s+/g, ' '), 70)}`.trim(), area >= 0.2)
      }
    }
  }

  // C3 default indigo/violet on filled controls; C4 accent sprawl
  const hueBuckets = new Map()
  for (const e of all) {
    const s = cs(e)
    const c = solid(s.backgroundColor)
    if (!c) continue
    const h = hsl(c)
    const r = e.getBoundingClientRect()
    if ((interactive(e) || e.matches('.btn, [class*="button"]')) && h.h >= 235 && h.h <= 295 && h.s > 0.45 && h.l > 0.35 && h.l < 0.72 && c.a > 0.9) add('C3', e, s.backgroundColor)
    if (h.s > 0.5 && h.l > 0.25 && h.l < 0.75 && c.a > 0.8 && r.width * r.height > 150) {
      const bucket = Math.round(h.h / 30) % 12
      if (!hueBuckets.has(bucket)) hueBuckets.set(bucket, e)
    }
  }
  if (hueBuckets.size >= 5) add('C4', null, `${hueBuckets.size} saturated hue families used as fills`)

  // C5 neon on dark: a large near-black region holding a glow or a neon accent
  const neon = (c) => { const h = hsl(c); return h.s > 0.75 && h.l > 0.45 && h.l < 0.75 }
  for (const e of all) {
    const r = e.getBoundingClientRect()
    if (r.width < vw * 0.6 || r.height < 300) continue
    const c = solid(cs(e).backgroundColor)
    if (!c || lum(c) > 0.03) continue
    const inner = [...e.querySelectorAll('*')].slice(0, 400)
    const glowing = inner.some((n) => shadows(cs(n).boxShadow).some((sh) => !sh.inset && sh.blur >= 12 && neon(sh.color)) || cs(n).textShadow !== 'none')
    const accent = inner.some((n) => { const f = parseColor(cs(n).color); const b = solid(cs(n).backgroundColor); return (f && neon(f)) || (b && neon(b)) })
    if (glowing && accent) { add('C5', e, 'near-black region with neon accent and glow'); break }
  }

  // C7 cream + serif + terracotta/sage
  const serif = (e) => { const f = cs(e).fontFamily.toLowerCase(); return /serif/.test(f) && !/sans-serif/.test(f.split(',')[0]) || /(instrument serif|fraunces|playfair|dm serif|newsreader|lora|garamond|georgia|times)/.test(f) }
  for (const e of [document.body, ...all]) {
    const c = solid(cs(e).backgroundColor)
    if (!c) continue
    const r = e.getBoundingClientRect()
    if (r.width < vw * 0.6 || r.height < 200) continue
    const warmCream = c.r >= 238 && c.g >= 230 && c.b >= 210 && c.r - c.b >= 8 && c.r - c.b <= 40
    if (!warmCream) continue
    const heads = [...e.querySelectorAll('h1, h2, h3')].filter((h) => !hidden(h))
    if (!heads.some(serif)) continue
    const earthy = [...e.querySelectorAll('*')].slice(0, 400).some((n) => {
      for (const v of [cs(n).color, cs(n).backgroundColor]) {
        const p = solid(v); if (!p) continue
        const h = hsl(p)
        if ((h.h >= 5 && h.h <= 32 && h.s > 0.4 && h.l > 0.35 && h.l < 0.65) || (h.h >= 80 && h.h <= 160 && h.s > 0.1 && h.s < 0.4 && h.l > 0.3 && h.l < 0.6)) return true
      }
      return false
    })
    if (earthy) { add('C7', e === document.body ? null : e, 'cream ground, serif display, terracotta/sage accent'); break }
  }

  // ---------- typography ----------
  const DEFAULT_FACES = /^(inter|inter var|roboto|arial|helvetica|helvetica neue|open sans|lato|system-ui|-apple-system|blinkmacsystemfont|segoe ui|ui-sans-serif|sans-serif|geist|geist sans)$/
  const families = new Set(textEls.map(family))
  if (families.size > 0 && [...families].every((f) => DEFAULT_FACES.test(f) || /mono|courier|consolas|menlo/.test(f))) add('T1', null, `only ${[...families].join(', ')}`)
  const TREND = /^(space grotesk|instrument serif|fraunces|syne|geist|playfair display|dm serif display|clash display|cabinet grotesk|satoshi|general sans|bricolage grotesque|outfit)$/
  for (const h of all.filter((e) => e.matches('h1, h2'))) if (TREND.test(family(h))) { add('T2', h, family(h)); break }

  for (const e of textEls) {
    const s = cs(e)
    const t = ownText(e)
    const letters = t.replace(/[^\p{L}]/gu, '')
    if (letters.length < 2) continue
    const tracking = px(s.letterSpacing) / px(s.fontSize)
    const caps = s.textTransform === 'uppercase' || (letters.length >= 4 && letters === letters.toUpperCase() && letters !== letters.toLowerCase())
    if (caps && tracking >= 0.03) add('T3', e, `uppercase, letter-spacing ${s.letterSpacing}`)
  }

  for (const h of all.filter((e) => e.matches('h1, h2, h3'))) {
    const hs = cs(h)
    const full = text(h)
    for (const c of h.querySelectorAll('em, i, span, strong, b, mark')) {
      const t = text(c)
      if (!t || t.split(/\s+/).length > 3 || t.length > full.length * 0.5) continue
      const s = cs(c)
      const differs = (s.fontStyle === 'italic' && hs.fontStyle !== 'italic') || s.fontFamily !== hs.fontFamily ||
        (s.color !== hs.color && (hsl(parseColor(s.color) ?? { r: 0, g: 0, b: 0 }).s > 0.3)) || s.backgroundClip === 'text' || s.webkitBackgroundClip === 'text'
      if (differs) { add('T4', h, `«${t}» styled apart`); break }
    }
  }

  for (const e of textEls) {
    if (!isMono(e) || e.closest('code, pre, kbd, samp, var, [class*="code"]')) continue
    const t = ownText(e)
    const words = t.split(/\s+/).filter((w) => /\p{L}{2,}/u.test(w))
    const codeLike = /[{}();=<>]|^[\w.-]+$|^#?[0-9a-f-]{6,}$/i.test(t) || /\d/.test(t) && words.length < 2
    if (words.length >= 2 && !codeLike) add('T5', e, `monospace label «${short(t, 30)}»`)
  }

  const sizes = new Map()
  for (const e of textEls) { const v = Math.round(px(cs(e).fontSize) * 2) / 2; sizes.set(v, (sizes.get(v) ?? 0) + 1) }
  const body = [...sizes.keys()].filter((v) => v >= 11 && v <= 24 && sizes.get(v) >= 2).sort((a, b) => a - b)
  let close = 0
  for (let i = 1; i < body.length; i++) if (body[i] - body[i - 1] <= 1) close++
  if (body.length >= 8 && close >= 4) add('T6', null, `${body.length} sizes between 11 and 24px: ${body.join(', ')}`)

  for (const e of textEls) if (px(cs(e).fontSize) < 12 && ownText(e).replace(/[^\p{L}\p{N}]/gu, '').length >= 3 && !e.closest('sup, sub, svg, kbd')) add('T7', e, cs(e).fontSize)

  for (const h of all.filter((e) => e.matches('h1, h2'))) {
    const walker = document.createTreeWalker(h, NodeFilter.SHOW_TEXT)
    const words = []
    for (let n; (n = walker.nextNode());) {
      for (const m of n.textContent.matchAll(/\S+/g)) words.push({ node: n, start: m.index, end: m.index + m[0].length })
    }
    if (words.length < 4) continue
    const top = (w) => { const r = document.createRange(); r.setStart(w.node, w.start); r.setEnd(w.node, w.end); return r.getClientRects()[0]?.top ?? 0 }
    const first = top(words[0]), last = top(words[words.length - 1]), prev = top(words[words.length - 2])
    if (last - first > 4 && last - prev > 4) add('T8', h, `last line: «${h.textContent.trim().split(/\s+/).pop()}»`)
  }

  // ---------- surfaces ----------
  const boxes = all.filter((e) => { const s = cs(e); return (painted(s) || bordered(s)) && !e.matches('input, textarea, select, img, svg, video, canvas') })
  const radii = new Map()
  let rounded = 0
  for (const e of boxes) {
    const r = e.getBoundingClientRect()
    const v = px(cs(e).borderTopLeftRadius)
    if (v <= 0 || v >= Math.min(r.width, r.height) / 2 - 1) continue // pills and circles are shapes, not the box radius
    rounded++
    radii.set(v, (radii.get(v) ?? 0) + 1)
  }
  const [topR, topN] = [...radii].sort((a, b) => b[1] - a[1])[0] ?? [0, 0]
  if (rounded >= 8 && topR >= 12 && topN / rounded >= 0.7) add('S1', null, `${topR}px on ${Math.round((topN / rounded) * 100)}% of ${rounded} rounded boxes`)

  const chipLike = (e) => {
    if (interactive(e) && !e.matches('[role="status"]')) return false
    const r = e.getBoundingClientRect()
    const s = cs(e)
    const t = text(e)
    const parentW = e.parentElement?.getBoundingClientRect().width ?? Infinity
    return r.height >= 16 && r.height <= 32 && r.width <= 240 && r.width > r.height && r.width < parentW * 0.8 &&
      t.length > 0 && t.length <= 28 && painted(s)
  }
  const chips = all.filter(chipLike)
  const pill = (e) => px(cs(e).borderTopLeftRadius) >= e.getBoundingClientRect().height / 2 - 1
  const byParent = new Map()
  for (const c of chips) {
    const key = c.closest('tr, li, [role="row"]')?.parentElement ?? c.parentElement
    if (!byParent.has(key)) byParent.set(key, [])
    byParent.get(key).push(c)
  }
  for (const [, group] of byParent) {
    const shapes = new Set(group.map(pill))
    const heights = group.map((c) => Math.round(c.getBoundingClientRect().height))
    if (group.length >= 2 && (shapes.size > 1 || Math.max(...heights) - Math.min(...heights) >= 4)) add('S2', group[0], shapes.size > 1 ? 'pill and square chips side by side' : `chip heights ${[...new Set(heights)].join('/')}px`)
  }
  for (const e of all.filter((n) => n.matches('button, a, [role="button"]'))) {
    const r = e.getBoundingClientRect()
    if (r.height >= 28 && r.width > r.height * 1.8 && pill(e) && painted(cs(e)) && document.querySelectorAll('table, [role="grid"], form').length > 0) { add('S2', e, 'pill-shaped button in a dense UI'); break }
  }

  for (const e of all) {
    if (e.matches('[aria-current], [aria-selected="true"], [aria-checked="true"], [role="tab"], blockquote, hr, td, th, tr, [class*="selected"], [class*="active"], [class*="current"], [class*="today"]')) continue
    const s = cs(e)
    const sides = ['Left', 'Right', 'Top', 'Bottom'].map((k) => ({ k, w: px(s[`border${k}Width`]), c: parseColor(s[`border${k}Color`]), style: s[`border${k}Style`] }))
    const thick = sides.filter((x) => x.w >= 3 && x.style !== 'none')
    const r = e.getBoundingClientRect()
    if (thick.length === 1 && thick[0].c && hsl(thick[0].c).s > 0.25 && sides.filter((x) => x.w >= 1).length >= 1 && r.height >= 24 && r.width >= 80) { add('S3', e, `${thick[0].k.toLowerCase()} ${thick[0].w}px`); continue }
    const ins = shadows(s.boxShadow).find((x) => x.inset && x.blur === 0 && ((Math.abs(x.x) >= 3 && x.y === 0) || (Math.abs(x.y) >= 3 && x.x === 0)) && hsl(x.color).s > 0.25)
    if (ins && r.height >= 24 && r.width >= 80) { add('S3', e, 'inset shadow stripe'); continue }
    for (const pseudo of ['::before', '::after']) {
      const p = cs(e, pseudo)
      if (p.content === 'none' || p.display === 'none' || p.position !== 'absolute') continue
      const w = px(p.width), h = px(p.height)
      const c = solid(p.backgroundColor)
      if (c && hsl(c).s > 0.25 && ((w >= 2 && w <= 6 && h >= r.height * 0.6) || (h >= 2 && h <= 6 && w >= r.width * 0.6)) && r.height >= 24 && r.width >= 80) { add('S3', e, `${pseudo} stripe`); break }
    }
  }

  const cards = all.filter((e) => {
    const r = e.getBoundingClientRect()
    const s = cs(e)
    return r.width >= 160 && r.height >= 72 && r.width < vw * 0.9 && text(e).length > 0 && (painted(s) || bordered(s)) && !overlay(e) &&
      !/blur\(/.test(s.filter) && !e.matches('body, main, html, header, nav, aside, footer, table, tr, td, th, thead, tbody, form, img, video, button, a')
  })
  const lifted = cards.filter((e) => dropShadow(cs(e).boxShadow))
  if (lifted.length >= 3 && lifted.length / cards.length >= 0.5) for (const e of lifted) add('S4', e, short(cs(e).boxShadow, 60))

  for (const e of all) {
    const s = cs(e)
    if (bordered(s) && dropShadow(s.boxShadow) && !overlay(e) && !e.matches('input, textarea, select')) add('S5', e, short(s.boxShadow, 60))
  }

  for (const e of all) {
    const s = cs(e)
    if (loading(e)) continue
    if (s.textShadow !== 'none' && ownText(e)) { add('S6', e, `text-shadow ${short(s.textShadow, 40)}`); continue }
    const sh = shadows(s.boxShadow)
    const glow = sh.find((x) => !x.inset && x.blur >= 8 && hsl(x.color).s > 0.35 && x.color.a > 0.15 && !(x.blur === 0 && x.x === 0 && x.y === 0))
    const dramatic = sh.find((x) => !x.inset && x.blur >= 48 && x.color.a > 0.12)
    const filterGlow = /drop-shadow\(/.test(s.filter) && (s.filter.match(/rgba?\([^)]*\)/g) ?? []).some((c) => hsl(parseColor(c)).s > 0.35)
    if ((glow || filterGlow) && !overlay(e)) add('S6', e, 'coloured blurred shadow')
    else if (dramatic && !overlay(e)) add('S6', e, `drop shadow blur ${dramatic.blur}px`)
  }

  for (const e of all) {
    const s = cs(e)
    if (!/blur\(/.test(s.backdropFilter ?? s.webkitBackdropFilter ?? '')) continue
    // Dialogs and menus may blur what is behind them; a glass nav, card or button is the trend.
    if (e.matches('dialog, [role="dialog"], [role="menu"], [role="listbox"], [aria-modal="true"]')) continue
    add('S7', e, `${s.backdropFilter}${s.position === 'fixed' || s.position === 'sticky' ? ' (floating bar)' : ''}`)
  }

  const boxed = (e) => {
    const s = cs(e)
    const r = e.getBoundingClientRect()
    if (r.width < 120 || r.height < 56 || e.matches('table, tr, td, th, input, textarea, select, button, a, img, svg, body, html, main, dialog')) return false
    const bg = solid(s.backgroundColor)
    const parentBg = e.parentElement ? bgBehind(e.parentElement) : null
    const differs = bg && parentBg && contrast(blend(bg, parentBg), parentBg) > 1.04
    return bordered(s) || dropShadow(s.boxShadow) || (differs && px(s.borderTopLeftRadius) > 0)
  }
  const framed = (e) => { const s = cs(e); return bordered(s) || dropShadow(s.boxShadow) }
  for (const e of all) {
    if (!boxed(e)) continue
    if (!framed(e)) continue
    let depth = 1
    let framedDepth = 1
    for (let n = e.parentElement; n && n !== document.body; n = n.parentElement) if (boxed(n)) { depth++; if (framed(n)) framedDepth++ }
    if (depth >= 3 && framedDepth >= 3 && ![...e.children].some(boxed)) add('S8', e, `${depth} levels of boxes`)
  }

  // ---------- layout & components ----------
  const iconOnly = (e) => {
    const kids = [...e.children]
    return text(e) === '' && kids.length >= 1 && kids.length <= 2 && (e.querySelector(':scope > svg, :scope > i, :scope > img, :scope > span > svg') !== null)
  }
  const tile = (e) => {
    const r = e.getBoundingClientRect()
    const s = cs(e)
    if (r.width < 24 || r.width > 72 || Math.abs(r.width - r.height) > 3) return false
    if (!iconOnly(e) || interactive(e) || e.closest('button, a') || e.matches('[class*="avatar"], [class*="brand"], [class*="logo"]') || e.closest('[class*="avatar"], [class*="brand"], [class*="logo"]')) return false
    const bg = solid(s.backgroundColor)
    const under = e.parentElement ? bgBehind(e.parentElement) : null
    return (bg && under && contrast(blend(bg, under), under) > 1.05) || s.backgroundImage.includes('gradient')
  }
  const tiles = all.filter(tile)
  for (const e of tiles) add('L2', e, cs(e).backgroundColor)

  for (const e of all) {
    const r = e.getBoundingClientRect()
    if (r.height > 30 || interactive(e) || e.closest('button, a')) continue
    if (!e.querySelector(':scope > svg') || !ownText(e) && !text(e)) continue
    if (text(e).length === 0 || text(e).length > 30) continue
    const s = cs(e)
    if (painted(s) && (bordered(s) || solid(s.backgroundColor))) add('L3', e, 'icon + text + tint on one chip')
  }

  const sameShape = (kids) => new Set(kids.map((k) => `${k.tagName}.${k.classList[0] ?? ''}`)).size === 1
  const parents = new Set(all.map((e) => e.parentElement).filter(Boolean))
  for (const p of parents) {
    const kids = [...p.children].filter((k) => !hidden(k))
    if (kids.length < 3 || kids.length > 6 || !sameShape(kids)) continue
    const kpi = kids.every((k) => {
      const s = cs(k)
      if (!(painted(s) || bordered(s) || dropShadow(s.boxShadow))) return false
      const nums = [...k.querySelectorAll('*')].filter((n) => /^\s*[$€£¥₫]?\s*[\d.,]+\s*[%kKmM+]?\s*$/.test(ownText(n)) && px(cs(n).fontSize) >= 20)
      if (!nums.length) return false
      const deco = k.querySelector('svg') || /[↑↓▲▼]|[+-]\d+(\.\d+)?\s?%/.test(text(k)) || nums.some((n) => hsl(parseColor(cs(n).color) ?? { r: 0, g: 0, b: 0 }).s > 0.4)
      return !!deco
    })
    if (kpi) add('L1', p, `${kids.length} stat tiles with icon/trend/coloured number`)
  }

  const h1 = all.find((e) => e.matches('h1'))
  if (h1) {
    const centred = cs(h1).textAlign === 'center' || cs(h1.parentElement).textAlign === 'center' || cs(h1.parentElement).alignItems === 'center'
    if (centred) {
      const h1Bottom = h1.getBoundingClientRect().bottom
      for (const p of parents) {
        const kids = [...p.children].filter((k) => !hidden(k))
        if (![3, 4, 6].includes(kids.length) || !sameShape(kids)) continue
        if (p.getBoundingClientRect().top < h1Bottom) continue
        if (kids.every((k) => (k.querySelector('svg, img, [class*="icon"]') || /\p{Extended_Pictographic}/u.test(text(k).slice(0, 4))) && k.querySelector('h2, h3, h4, strong, [class*="title"]'))) { add('L4', p, `centred hero then ${kids.length} icon cards`); break }
      }
    }
    // L5 pill right above the H1
    const hr = h1.getBoundingClientRect()
    const above = all.filter((e) => { const r = e.getBoundingClientRect(); return r.bottom <= hr.top + 2 && hr.top - r.bottom < 48 && r.height <= 40 && r.height >= 16 && text(e).length > 0 && text(e).length <= 70 })
    const pillAbove = above.find((e) => pill(e) && painted(cs(e)) && e.getBoundingClientRect().width > e.getBoundingClientRect().height * 2)
    if (pillAbove) add('L5', pillAbove, 'pill badge above the H1')
  }

  const idx = textEls.filter((e) => /^(0[1-9]|1[0-2])([./)\s—–-].*)?$/.test(ownText(e)) && ownText(e).length <= 40)
  if (idx.length >= 3) for (const e of idx) add('L6', e, ownText(e))

  for (const p of parents) {
    const dots = [...p.children].filter((k) => {
      const r = k.getBoundingClientRect()
      return r.width >= 6 && r.width <= 16 && Math.abs(r.width - r.height) <= 1 && px(cs(k).borderTopLeftRadius) >= r.width / 2 - 0.5 && solid(cs(k).backgroundColor)
    })
    if (dots.length !== 3) continue
    const hues = dots.map((d) => hsl(solid(cs(d).backgroundColor)).h)
    const red = hues.some((h) => h <= 15 || h >= 345), yellow = hues.some((h) => h >= 35 && h <= 60), green = hues.some((h) => h >= 90 && h <= 150)
    if (red && yellow && green) add('L7', p, 'traffic-light dots')
  }

  const EMOJI = /\p{Extended_Pictographic}/u
  // Emoji used as an icon: in chrome (headings, nav, controls, labels, toasts) or leading/trailing a line.
  for (const e of textEls) {
    const t = ownText(e)
    // Emoji presentation only: typographic symbols such as ↗ ▶ ✓ are text, not emoji icons.
    const m = t.match(/\p{Emoji_Presentation}|\p{Extended_Pictographic}\uFE0F/gu)
    if (!m) continue
    const chrome = e.closest('h1, h2, h3, h4, h5, h6, nav, li, button, a, label, th, [role="tab"], [role="menuitem"], [role="status"], [role="alert"], summary, dt')
    const iconLike = /^\s*(\p{Emoji_Presentation}|\p{Extended_Pictographic}\uFE0F)/u.test(t) || /(\p{Emoji_Presentation}|\p{Extended_Pictographic}\uFE0F)\s*$/u.test(t) || t.replace(/[\p{Extended_Pictographic}\uFE0F\u200D\s]/gu, '') === ''
    if (chrome || iconLike) add('L8', e, m.join(' '))
  }
  // An inline four-point star: sample the outline and count the peaks of its distance from the centre.
  for (const svg of document.querySelectorAll('svg')) {
    if (hidden(svg) || ignored(svg)) continue
    const r = svg.getBoundingClientRect()
    const shapes = svg.querySelectorAll('path, polygon')
    if (r.width > 48 || shapes.length !== 1 || typeof shapes[0].getTotalLength !== 'function') continue
    // A sparkle is a filled star; stroked line icons (close, plus, globe) are not.
    const ss = cs(shapes[0])
    if (ss.fill === 'none' || (ss.stroke !== 'none' && px(ss.strokeWidth) > 0.5 && ss.fill === 'none')) continue
    try {
      const shape = shapes[0]
      const len = shape.getTotalLength()
      const pts = Array.from({ length: 96 }, (_, i) => shape.getPointAtLength((len * i) / 96))
      const cx = pts.reduce((a, p) => a + p.x, 0) / pts.length, cy = pts.reduce((a, p) => a + p.y, 0) / pts.length
      const d = pts.map((p) => Math.hypot(p.x - cx, p.y - cy))
      const max = Math.max(...d), min = Math.min(...d)
      const peaks = d.filter((v, i) => v > d[(i + 95) % 96] && v >= d[(i + 1) % 96] && v > max * 0.8).length
      // Pointed tips: little of the outline sits near the maximum radius (a plus sign has flat arm ends).
      const nearTip = d.filter((v) => v > max * 0.9).length / d.length
      if (peaks === 4 && nearTip <= 0.14 && max / Math.max(min, 0.01) > 2.5) add('L8', svg.parentElement ?? svg, 'four-point sparkle')
    } catch { /* not measurable */ }
  }
  for (const e of document.querySelectorAll('svg[class*="sparkle"], [class*="sparkle"] > svg, svg.lucide-sparkles, svg.lucide-wand-sparkles, svg.lucide-sparkle'))
    if (!hidden(e) && !ignored(e)) add('L8', e.parentElement ?? e, 'sparkle icon')

  for (const e of all) {
    const r = e.getBoundingClientRect()
    if (r.width < 5 || r.width > 12 || Math.abs(r.width - r.height) > 1 || text(e)) continue
    const s = cs(e)
    const c = solid(s.backgroundColor)
    if (!c || px(s.borderTopLeftRadius) < r.width / 2 - 0.5) continue
    const host = e.parentElement
    if (host && text(host).length > 0 && text(host).length <= 30 && (painted(cs(host)) || s.animationName !== 'none')) add('L9', host, s.animationName !== 'none' ? 'pulsing dot' : 'dot inside a labelled chip')
  }
  for (const e of all) {
    for (const pseudo of ['::before']) {
      const p = cs(e, pseudo)
      if (p.content === 'none' || p.display === 'none' || p.visibility === 'hidden') continue
      const w = px(p.width), h = px(p.height)
      if (w >= 5 && w <= 12 && Math.abs(w - h) <= 1 && solid(p.backgroundColor) && px(p.borderTopLeftRadius) >= w / 2 - 0.5 && text(e).length > 0 && text(e).length <= 30 && painted(cs(e))) add('L9', e, '::before dot inside a labelled chip')
    }
  }

  for (const table of all.filter((e) => e.matches('table, [role="table"], [role="grid"]'))) {
    const rows = [...table.querySelectorAll('tbody tr, [role="row"]')].filter((r) => !hidden(r) && !r.querySelector('th'))
    if (rows.length < 4) continue
    const cols = Math.max(...rows.map((r) => r.children.length))
    for (let i = 0; i < cols; i++) {
      const cellChips = rows.map((r) => r.children[i]).filter(Boolean).map((cell) => [...cell.querySelectorAll('*')].find(chipLike))
      if (cellChips.some((c) => !c)) continue
      const labels = cellChips.map((c) => text(c))
      const top = Math.max(...[...new Set(labels)].map((l) => labels.filter((x) => x === l).length))
      const common = labels.find((l) => labels.filter((x) => x === l).length === top)
      // Every row the same because a filter on the page selects that value: the data, not the design.
      const filteredTo = all.some((n) => n !== table && !table.contains(n) && n.matches('button, select, [role="combobox"], [aria-pressed="true"], [aria-selected="true"], [class*="filter"], [class*="select"]') && text(n) === common)
      if (top / labels.length >= 0.75 && !filteredTo) { add('L10', cellChips[0], `column ${i + 1}: «${labels[0]}» on ${top}/${labels.length} rows`); break }
    }
  }

  for (const e of textEls) {
    if (e.closest('[data-tooltip], [role="tooltip"], title, code, pre')) continue
    const t = ownText(e)
    if (/\S\s·\s\S/.test(t)) add('L11', e, 'middle-dot join')
    else if (/(^|\s)(→|->|⟶)\s*$|^\s*(→|->)\s/.test(t) || (e.matches('a, button') && /→|->/.test(text(e)))) add('L11', e, 'arrow glued to label')
    else if (/^[\p{Lu}\s]{3,}\s[—–]\s\S/u.test(t)) add('L11', e, '"WORD — fragment" label')
  }

  // A muted dash in a dense table is a recognised convention (Cloudscape); elsewhere say it in words.
  for (const e of all.filter((n) => n.matches('td, dd, [role="cell"], [role="gridcell"], [class*="value"]'))) {
    const t = text(e)
    const inTable = !!e.closest('td, [role="cell"], [role="gridcell"]')
    if (/^(n\/a|null|none|undefined)$/i.test(t) || (!inTable && /^(—|–|-)$/.test(t))) add('L12', e, `«${t}»`)
  }

  const filled = all.filter((e) => {
    if (!e.matches('button, a, [role="button"], input[type="submit"]')) return false
    // A link counts as a button only when it is shaped like one (not a calendar block or a card link).
    const br = e.getBoundingClientRect()
    if (e.matches('a:not([class*="btn"]):not([class*="button"]):not([class*="cta"]):not([role="button"])') && (br.height > 56 || br.width > 360 || text(e).length > 40)) return false
    if (e.matches('[aria-pressed="true"], [aria-selected="true"], [aria-checked="true"], [aria-current], [role="tab"], [role="radio"], [disabled]')) return false
    const r = e.getBoundingClientRect()
    if (r.width < 48 || r.height < 28 || !text(e)) return false
    const s = cs(e)
    if (decorativeGradient(s.backgroundImage)) return true
    const c = solid(s.backgroundColor)
    if (!c || c.a < 0.9) return false
    // Filled = its fill stands out from what is behind it (blue on white, white on navy, black on cream).
    const at = bgAt(e, true)
    const behind = at === undefined ? (e.parentElement ? bgBehind(e.parentElement) : null) : at
    const h = hsl(c)
    return (h.s > 0.3 && h.l < 0.75) || (behind ? contrast(c, behind) >= 2.2 : false)
  })
  const areaOf = (e) => e.closest('nav, section, form, dialog, header, footer, aside, [role="region"], [role="dialog"], [class*="card"], [class*="panel"]') ?? document.body
  const perArea = new Map()
  for (const b of filled) { const a = areaOf(b); perArea.set(a, [...(perArea.get(a) ?? []), b]) }
  for (const [, group] of perArea) if (group.length >= 2) for (const b of group.slice(1)) add('L13', b, `${group.length} filled buttons in one area`)
  const firstScreen = filled.filter(inFirstScreen)
  if (firstScreen.length >= 3 && ![...perArea.values()].some((g) => g.length >= 2)) add('L13', firstScreen[2], `${firstScreen.length} filled buttons on the first screen`)

  // ---------- motion ----------
  for (const e of all) {
    const s = cs(e)
    if (s.animationName !== 'none' && s.animationIterationCount === 'infinite' && !loading(e) && !e.closest('[role="status"], [aria-live]')) add('M1', e, s.animationName)
  }
  for (const e of all) {
    for (const pseudo of ['::before', '::after']) {
      const p = cs(e, pseudo)
      if (p.content !== 'none' && p.display !== 'none' && p.animationName !== 'none' && p.animationIterationCount === 'infinite' && !loading(e)) add('M1', e, `${pseudo} ${p.animationName}`)
    }
  }

  const rules = []
  let sheetsReadable = true
  const walk = (list, media) => {
    for (const r of list) {
      if (r.cssRules && (r.media || r.conditionText)) walk(r.cssRules, `${media} ${r.conditionText ?? r.media?.mediaText ?? ''}`)
      else if (r.selectorText) rules.push({ sel: r.selectorText, style: r.style, media })
      else if (r.cssRules) walk(r.cssRules, media)
    }
  }
  for (const sheet of document.styleSheets) { try { walk(sheet.cssRules, '') } catch { sheetsReadable = false } }
  const hoverLift = new Set()
  for (const r of rules) {
    if (!r.sel.includes(':hover')) continue
    const tf = `${r.style.transform} ${r.style.translate} ${r.style.scale}`
    if (!/translate|scale|\d/.test(tf.replace(/none|undefined/g, '').trim())) continue
    if (/translate(X)?\(\s*[^,)]*\)\s*$/.test(r.style.transform) && !/translateY|translate\([^,]+,/.test(r.style.transform) && !r.style.scale) continue
    const base = r.sel.split(',').filter((s) => s.includes(':hover')).map((s) => s.replace(/:hover/g, '').trim()).filter(Boolean)
    for (const b of base) { try { for (const e of document.querySelectorAll(b)) if (!hidden(e) && !ignored(e)) hoverLift.add(e) } catch { /* invalid after strip */ } }
  }
  for (const e of all) if ([...e.classList].some((c) => /^(hover|group-hover):(scale-1\d\d|-?translate-y-|-translate-y-)/.test(c))) hoverLift.add(e)
  for (const e of [...hoverLift]) { const r = e.getBoundingClientRect(); if (r.width >= 40 && r.height >= 24) add('M2', e, 'moves on hover') }

  const tAll = all.filter((e) => { const s = cs(e); return /(^|,\s*)all\b/.test(s.transitionProperty) && s.transitionDuration.split(',').some((d) => px(d) > 0) })
  for (const e of tAll) add('M3', e, `transition: all ${cs(e).transitionDuration}`)

  const reveal = all.filter((e) => e.hasAttribute('data-aos') || e.hasAttribute('data-animate') || e.hasAttribute('data-reveal') || [...e.classList].some((c) => /fade-?(in-?)?up|slide-?up|animate-(fade|slide)|^reveal|^aos-|in-view/i.test(c)))
  const fadeNames = new Map()
  for (const e of all) { const n = cs(e).animationName; if (/fade|slide|rise|reveal|up/i.test(n) && n !== 'none') fadeNames.set(n, (fadeNames.get(n) ?? 0) + 1) }
  if (reveal.length >= 3) for (const e of reveal) add('M4', e, 'scroll-reveal class/attribute')
  else for (const [n, c] of fadeNames) if (c >= 4) add('M4', null, `${c} elements share entrance animation "${n}"`)

  const animates = all.some((e) => cs(e).animationName !== 'none') || rules.some((r) => /translate|scale/.test(r.style.transform ?? '') && r.sel.includes(':hover'))
  const reduced = rules.some((r) => /prefers-reduced-motion/.test(r.media))
  if (sheetsReadable && animates && !reduced) add('M5', null, 'animations with no prefers-reduced-motion rule')

  // ---------- copy ----------
  const copy = {
    buzzwords: {
      en: ['unlock', 'supercharge', 'seamless', 'seamlessly', 'elevate', 'unleash', 'revolutionize', 'revolutionise', 'game-changer', 'game-changing', 'next-gen', 'next-level', 'cutting-edge', 'all-in-one', 'effortless', 'effortlessly', 'empower', 'leverage', 'synergy', 'transform your', 'take your', 'to the next level', 'like never before', 'reimagined', 'redefine', 'world-class', 'best-in-class', 'blazing fast', 'lightning-fast'],
      vi: ['nâng tầm', 'giải pháp toàn diện', 'trải nghiệm vượt trội', 'đột phá', 'liền mạch', 'kỷ nguyên số', 'mở khoá tiềm năng', 'mở khóa tiềm năng', 'tối ưu hoá mọi', 'tối ưu hóa mọi', 'hàng đầu', 'chuyển đổi số toàn diện', 'cách mạng hoá', 'vượt trội'],
    },
    genericCta: { en: ['get started', 'learn more', 'submit', 'click here', 'read more', 'start now', 'try it now', 'explore', 'discover more'], vi: ['bắt đầu ngay', 'tìm hiểu thêm', 'xem thêm', 'gửi', 'khám phá', 'nhấn vào đây'] },
    ...opts.copy,
  }
  const main = document.querySelector('main') ?? document.body
  const textNodes = []
  const tw = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT)
  for (let n; (n = tw.nextNode());) {
    const p = n.parentElement
    if (!p || !n.textContent.trim() || p.closest('script, style, noscript, code, pre') || hidden(p) || ignored(p)) continue
    textNodes.push(n)
  }
  const esc = (s) => s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
  const wordRe = (list) => new RegExp(`(^|[^\\p{L}])(${list.map(esc).join('|')})(?=$|[^\\p{L}])`, 'iu')
  const buzz = wordRe([...(copy.buzzwords.en ?? []), ...(copy.buzzwords.vi ?? [])])
  // A buzzword in long marketing copy is normal; a page dense with them is the tell.
  const buzzHits = []
  for (const n of textNodes) { const m = n.textContent.match(buzz); if (m) buzzHits.push([n.parentElement, m[2].toLowerCase()]) }
  const pageWords = textNodes.reduce((c, n) => c + n.textContent.split(/\s+/).filter(Boolean).length, 0)
  const distinctBuzz = new Set(buzzHits.map(([, w]) => w))
  const buzzStrong = distinctBuzz.size >= 3 && (buzzHits.length / Math.max(pageWords, 1)) * 1000 >= 4
  for (const [e, w] of buzzHits) add('W1', e, `«${w}»${buzzStrong ? '' : ' (low density)'}`, buzzStrong)
  const ctaSet = new Set([...(copy.genericCta.en ?? []), ...(copy.genericCta.vi ?? [])].map((s) => s.toLowerCase()))
  const ctas = all.filter((n) => n.matches('button, [role="button"], input[type="submit"], a[class*="btn"], a[class*="button"], a[class*="cta"]') || (n.matches('a') && ctaSet.has(text(n).toLowerCase().replace(/[→\->›»]+/g, '').trim())))
  const generic = ctas.filter((e) => ctaSet.has((text(e) || e.value || '').toLowerCase().replace(/[→\->›»]+/g, '').replace(/\s+/g, ' ').trim()))
  const ctaStrong = generic.length >= 2 && generic.length / Math.max(ctas.length, 1) >= 0.4
  for (const e of generic) add('W2', e, `«${text(e) || e.value}»${ctaStrong ? '' : ' (minority of CTAs)'}`, ctaStrong)
  const DEV = /\b(GET|POST|PUT|PATCH|DELETE)\s+\/[\w/{}.-]+|\b(TODO|FIXME|XXX):|\bcoming soon\b|\bplaceholder text\b|sẽ (hiện thực|làm|có) khi/
  for (const n of textNodes) if (DEV.test(n.textContent)) add('W3', n.parentElement, `dev text «${short(n.textContent.trim(), 40)}»`)
  if (h1) {
    const next = h1.nextElementSibling
    if (next && next.matches('p') && !hidden(next)) {
      const words = text(next).split(/\s+/).length
      const titleWords = new Set(text(h1).toLowerCase().split(/\s+/).filter((w) => w.length > 3))
      const overlap = text(next).toLowerCase().split(/\s+/).filter((w) => titleWords.has(w)).length
      const explains = /^(here you can|this page|use this|manage|view|browse|quản lý|xem|danh sách|đây là|tại đây)/i.test(text(next))
      // Error and not-found pages must explain what happened and what to do next.
      const errorPage = /\b(404|500|not found|error|went wrong|không tìm thấy|không tồn tại|lỗi|có lỗi)\b/i.test(text(h1)) || !!document.querySelector('[role="alert"]')
      if (words >= 6 && !/\d/.test(text(next)) && (overlap >= 2 || explains) && !errorPage) add('W3', next, `${words}-word subtitle restating the title`)
    }
  }
  for (const n of textNodes) {
    const t = n.textContent
    if (/[^\s!]!(\s|$)/.test(t) && !/!=/.test(t) || /\b(oops|whoops|yay|woohoo|awesome!)\b/i.test(t)) { add('W4', n.parentElement, `«${short(t.trim(), 40)}»`) }
  }
  const leak = wordRe(['lorem ipsum', 'dolor sit amet', 'john doe', 'jane doe', 'acme inc', 'acme corp', 'acme corporation', 'nguyễn văn a', 'công ty abc', 'công ty xyz'])
  for (const n of textNodes) {
    const t = n.textContent.trim()
    const m = t.match(leak) ?? t.match(/\{\{[^}]*\}\}|\$\{[^}]*\}|\[(name|company|title|your [a-z]+)\]|\[object Object\]/i) ?? t.match(/^(undefined|null|NaN)$/)
    if (m) add('W5', n.parentElement, `«${(m[2] ?? m[0]).trim()}»`)
  }
  for (const e of all.filter((n) => n.matches('input:not([type="hidden"]):not([type="password"]), textarea'))) {
    const m = (e.value ?? '').match(leak)
    if (m) add('W5', e, `value «${m[2]}»`)
  }
  const PROOF = /\b\d+(\.\d+)?\s?[kKmM]\+?\s+(happy\s+)?(users|customers|teams|developers|companies|businesses|downloads)\b|\b99\.9+\s?%|\b4\.[5-9]\s?(★|\/\s?5|stars?)|★{4,5}|⭐{3,}|\btrusted by\b|\bloved by\b|\bused by (thousands|millions)\b|hơn\s+\d+[\d.,]*\s*\+?\s*(khách hàng|doanh nghiệp|người dùng)|được tin dùng bởi/i
  for (const n of textNodes) if (PROOF.test(n.textContent)) add('W6', n.parentElement, `«${short(n.textContent.trim(), 40)}»`)

  // ---------- accessibility ----------
  for (const e of textEls) {
    if (e.closest('[disabled], [aria-disabled="true"], button:disabled')) continue
    const s = cs(e)
    const fg = parseColor(s.color)
    // Gradient text (C2) paints with a transparent colour: contrast cannot be read from it.
    if (!fg || fg.a < 0.1 || s.backgroundClip === 'text' || s.webkitBackgroundClip === 'text') continue
    const under = bgAt(e)
    const bg = under === undefined ? bgBehind(e) : under
    if (!fg || !bg) continue
    const ratio = contrast(blend(fg, bg), bg)
    const large = px(s.fontSize) >= 24 || (px(s.fontSize) >= 18.66 && Number(s.fontWeight) >= 700)
    if (ratio < (large ? 3 : 4.5)) add('A1', e, `${ratio.toFixed(2)}:1`)
  }

  const ids = [...new Set(findings.filter((f) => !f.allowed).map((f) => f.id))]
  return {
    url: location.href,
    viewport: { width: innerWidth, height: innerHeight },
    findings,
    stats: { elements: all.length, words: text(main).split(/\s+/).filter(Boolean).length, tellsPresent: ids, truncated: Object.fromEntries(Object.entries(counts).filter(([, c]) => c > opts.limit)), sheetsReadable },
  }
}

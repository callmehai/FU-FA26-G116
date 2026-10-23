// Element snapshot for `sloplint diff`. Runs in the page after detect.js({ mark: true }), and returns
// every visible element with a structural key, its box in page coordinates, its own text, the
// computed styles a reader can see, and the tells reported on it. Two snapshots of the same screen,
// taken before and after a change, are compared element by element, so every visible change gets a
// box, including the ones no tell names.
() => {
  // The first INHERITED properties are inherited: a change to one of them is reported on the topmost
  // element only. Spacing is compared as declared values, not as resulting boxes, so one changed
  // padding does not report every element it pushed down.
  const PROPS = ['color', 'font-size', 'font-weight', 'font-style', 'font-family', 'text-transform', 'letter-spacing', 'text-align', 'line-height',
    'background-color', 'background-image', 'border-top-width', 'border-top-style', 'border-top-color', 'border-left-width',
    'border-left-color', 'border-radius', 'box-shadow', 'opacity', 'text-decoration-line', 'outline-style', 'filter', 'backdrop-filter',
    'padding', 'margin', 'gap', 'max-width']
  const INHERITED = 9
  const PSEUDO = ['content', 'background-color', 'background-image', 'width', 'height', 'border-radius']
  const SKIP = new Set(['SCRIPT', 'STYLE', 'NOSCRIPT', 'TEMPLATE', 'HEAD', 'META', 'LINK', 'BR', 'WBR'])
  const MAX = 8000
  const out = []

  // Path of tag, first class and position among siblings with both: stable when styles or text
  // change, and when a sibling of another kind (an icon tile) is added or removed.
  const keys = new Map()
  const first = (e) => (e.getAttribute('class') ?? '').trim().split(/\s+/)[0] ?? ''
  const keyOf = (e) => {
    if (keys.has(e)) return keys.get(e)
    let i = 1
    for (let s = e.previousElementSibling; s; s = s.previousElementSibling) if (s.tagName === e.tagName && first(s) === first(e)) i++
    const own = `${e.tagName.toLowerCase()}${first(e) ? `.${first(e)}` : ''}${i > 1 ? `:${i}` : ''}`
    const k = e.parentElement && e.parentElement !== document.documentElement ? `${keyOf(e.parentElement)}>${own}` : own
    keys.set(e, k)
    return k
  }
  const label = (e, up = true) => {
    // SVG elements have an SVGAnimatedString className; read the attribute instead.
    const cls = (e.getAttribute('class') ?? '').trim().split(/\s+/).filter((c) => c && !c.startsWith('__slop')).slice(0, 2)
    const own = `${e.tagName.toLowerCase()}${e.id ? `#${e.id}` : ''}${cls.map((c) => `.${c}`).join('')}`
    // A bare tag ("svg", "td") says little on its own: name it by its parent.
    return up && !e.id && !cls.length && e.parentElement && e.parentElement !== document.body ? `${label(e.parentElement, false)} > ${own}` : own
  }
  const ownText = (e) => {
    if (e.matches('input, textarea')) return (e.value || e.placeholder || '').slice(0, 80)
    let t = ''
    for (const n of e.childNodes) if (n.nodeType === 3) t += n.textContent
    return t.replace(/\s+/g, ' ').trim().slice(0, 80)
  }
  // A cheap hash of an inline SVG, so swapping one icon for another counts as a change.
  const hash = (s) => { let h = 0; for (let i = 0; i < s.length; i++) h = (h * 31 + s.charCodeAt(i)) | 0; return (h >>> 0).toString(36) }

  const walk = (e) => {
    if (out.length >= MAX || SKIP.has(e.tagName) || e.id === '__slop_layer') return
    const s = getComputedStyle(e)
    if (s.display === 'none') return
    const r = e.getBoundingClientRect()
    const shown = r.width > 0 && r.height > 0 && s.visibility !== 'hidden' && (!e.checkVisibility || e.checkVisibility({ checkOpacity: true }))
    if (shown && e !== document.body) {
      const item = {
        key: keyOf(e), label: label(e), text: ownText(e),
        rect: { x: Math.round(r.left + scrollX), y: Math.round(r.top + scrollY), w: Math.round(r.width), h: Math.round(r.height) },
        style: PROPS.map((p) => s.getPropertyValue(p)),
      }
      for (const p of ['::before', '::after']) {
        const ps = getComputedStyle(e, p)
        if (ps.content && ps.content !== 'none' && ps.display !== 'none') (item.pseudo ??= {})[p] = PSEUDO.map((q) => ps.getPropertyValue(q)).join(' | ')
      }
      if (e.tagName === 'svg') item.icon = hash(e.innerHTML)
      if (e.tagName === 'IMG') item.icon = e.currentSrc.split('/').pop()?.slice(0, 60)
      const found = e.getAttribute('data-slop-found')
      if (found) item.tells = [...new Set(found.split(/\s+/))]
      out.push(item)
    }
    if (e.tagName === 'svg') return // icon internals are covered by the hash
    for (const c of e.children) walk(c)
  }
  walk(document.body)
  return { props: PROPS, inherited: INHERITED, width: document.documentElement.scrollWidth, height: document.documentElement.scrollHeight, elements: out, truncated: out.length >= MAX }
}

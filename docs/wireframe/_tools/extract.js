// Runs inside the wireframe page (real-size mode). Returns [{code,name,screens:[{no,w,h,root}]}]
// in the node format the figma-v4 plugin's build() understands.
(() => {
  const R = (v) => Math.round(v * 100) / 100
  const rgba = (s) => { const m = s && s.match(/rgba?\(([^)]+)\)/); if (!m) return null; const p = m[1].split(/[ ,/]+/).filter(Boolean).map(Number); return { r: p[0], g: p[1], b: p[2], a: p.length > 3 ? p[3] : 1 } }
  const hex = (c) => '#' + [c.r, c.g, c.b].map((v) => Math.round(v).toString(16).padStart(2, '0')).join('')
  const paint = (c) => ({ c: hex(c), a: R(c.a) })
  const splitTop = (s) => { const out = []; let d = 0, cur = ''; for (const ch of s) { if (ch === '(') d++; if (ch === ')') d--; if (ch === ',' && !d) { out.push(cur.trim()); cur = '' } else cur += ch } if (cur.trim()) out.push(cur.trim()); return out }

  // Materialise ::before/::after as real <span>s so they get geometry (the .x cross is drawn by the plugin instead).
  const st = document.createElement('style'); st.textContent = '.__np::before,.__np::after{content:none!important}'; document.head.appendChild(st)
  const pseudo = (root) => {
    for (const el of [...root.querySelectorAll('*')]) {
      if (el.classList.contains('x')) continue
      const add = []
      for (const which of ['::before', '::after']) {
        const cs = getComputedStyle(el, which)
        if (!cs.content || cs.content === 'none' || cs.content === 'normal' || cs.display === 'none') continue
        const sp = document.createElement('span')
        for (let i = 0; i < cs.length; i++) { const p = cs[i]; sp.style.setProperty(p, cs.getPropertyValue(p)) }
        const m = cs.content.match(/^"(.*)"$/); if (m) sp.textContent = m[1]
        add.push([which, sp])
      }
      if (!add.length) continue
      el.classList.add('__np')
      for (const [w, sp] of add) w === '::before' ? el.insertBefore(sp, el.firstChild) : el.appendChild(sp)
    }
  }

  const grad = (bg, w, h) => {
    const m = bg.match(/^linear-gradient\((.*)\)$/); if (!m) return null
    const parts = splitTop(m[1]); let ang = 180
    if (/^-?[\d.]+deg$/.test(parts[0])) ang = parseFloat(parts.shift())
    else if (/^to /.test(parts[0])) { const k = parts.shift(); ang = { 'to top': 0, 'to right': 90, 'to bottom': 180, 'to left': 270, 'to top right': 45, 'to right top': 45, 'to bottom right': 135, 'to right bottom': 135, 'to bottom left': 225, 'to left bottom': 225, 'to top left': 315, 'to left top': 315 }[k] ?? 180 }
    const th = ang * Math.PI / 180, dx = Math.sin(th), dy = -Math.cos(th), len = Math.abs(w * dx) + Math.abs(h * dy)
    const stops = []
    for (const p of parts) {
      const c = rgba(p); if (!c) return null
      const pos = p.replace(/rgba?\([^)]*\)/, '').trim().split(/\s+/).filter(Boolean).map((v) => v.endsWith('%') ? parseFloat(v) / 100 : parseFloat(v) / len)
      if (!pos.length) stops.push([c, null]); else pos.forEach((v) => stops.push([c, v]))
    }
    if (stops[0][1] == null) stops[0][1] = 0
    if (stops[stops.length - 1][1] == null) stops[stops.length - 1][1] = 1
    for (let i = 1; i < stops.length; i++) { if (stops[i][1] == null) { let j = i; while (stops[j][1] == null) j++; for (let k = i; k < j; k++) stops[k][1] = stops[i - 1][1] + (stops[j][1] - stops[i - 1][1]) * (k - i + 1) / (j - i + 1) } stops[i][1] = Math.max(stops[i][1], stops[i - 1][1]) }
    return { g: 1, T: [[R(dx), R(dy), R(0.5 - 0.5 * dx - 0.5 * dy)], [R(-dy), R(dx), R(0.5 + 0.5 * dy - 0.5 * dx)]], st: stops.map(([c, p]) => ({ c: hex(c), a: R(c.a), p: R(Math.min(1, Math.max(0, p))) })) }
  }

  const frameProps = (el, cs, w, h) => {
    const o = {}
    const fl = []
    const bgc = rgba(cs.backgroundColor); if (bgc && bgc.a > 0) fl.push(paint(bgc))
    if (cs.backgroundImage && cs.backgroundImage !== 'none') for (const b of splitTop(cs.backgroundImage)) { const g = grad(b, w, h); if (g) fl.push(g) }
    if (fl.length) o.fl = fl
    if (/hidden|clip|auto|scroll/.test(cs.overflowX + cs.overflowY)) o.cl = 1
    const rad = ['TopLeft', 'TopRight', 'BottomRight', 'BottomLeft'].map((k) => { const v = cs['border' + k + 'Radius'].split(' ')[0]; return Math.min(v.endsWith('%') ? parseFloat(v) / 100 * Math.min(w, h) : parseFloat(v) || 0, Math.min(w, h) / 2) })
    if (rad.some((v) => v > 0)) o.r = rad.map(R)
    const sides = ['Top', 'Right', 'Bottom', 'Left'].map((k) => { const s = cs['border' + k + 'Style'], c = rgba(cs['border' + k + 'Color']), bw = parseFloat(cs['border' + k + 'Width']) || 0; return s !== 'none' && s !== 'hidden' && c && c.a > 0 && bw > 0 ? { bw, c, s } : null })
    const first = sides.find(Boolean)
    if (first) {
      // a side in another colour (accent edge) becomes its own filled child, as in v4
      const same = (s) => s && hex(s.c) === hex(first.c) && s.c.a === first.c.a
      o.st = Object.assign(paint(first.c), { w: sides.map((s) => same(s) ? R(s.bw) : 0) }); if (/dash|dot/.test(first.s)) o.st.d = 1
      o.edges = sides.map((s, i) => s && !same(s) ? { i, s } : null).filter(Boolean).map(({ i, s }) => {
        const nm = ['top', 'right', 'bottom', 'left'][i], bw = R(s.bw), rr = o.r || [0, 0, 0, 0]
        const g = [[0, 0, w, bw, [rr[0], rr[1], 0, 0]], [w - bw, 0, bw, h, [0, rr[1], rr[2], 0]], [0, h - bw, w, bw, [0, 0, rr[2], rr[3]]], [0, 0, bw, h, [rr[0], 0, 0, rr[3]]]][i]
        return { t: 'F', n: 'border-' + nm, fl: [paint(s.c)], x: R(g[0]), y: R(g[1]), w: R(g[2]), h: R(g[3]), r: g[4] }
      })
      if (!o.edges.length) delete o.edges
      if (o.st.w.every((v) => !v)) delete o.st
    }
    const rad2 = cs.backgroundImage && cs.backgroundImage.match(/^radial-gradient\((?:circle, )?(rgba?\([^)]*\)) ([\d.]+)px/)
    if (rad2) {
      const c = rgba(rad2[1]), rr2 = parseFloat(rad2[2]), [tw, th] = cs.backgroundSize.split(' ').map(parseFloat)
      o.dots = []
      for (let y = th / 2; y + rr2 <= h + 0.01; y += th) for (let x = tw / 2; x + rr2 <= w + 0.01; x += tw) o.dots.push({ t: 'F', n: 'dot', x: R(x - rr2), y: R(y - rr2), w: R(2 * rr2), h: R(2 * rr2), fl: [paint(c)], r: [rr2, rr2, rr2, rr2].map(R) })
      if (!o.dots.length) delete o.dots
    }
    if (cs.boxShadow && cs.boxShadow !== 'none') {
      const fx = []
      for (const s of splitTop(cs.boxShadow)) { const c = rgba(s); if (!c || c.a === 0) continue; const n = s.replace(/rgba?\([^)]*\)/, '').replace('inset', '').trim().split(/\s+/).map(parseFloat); fx.push({ i: /inset/.test(s) ? 1 : 0, x: n[0] || 0, y: n[1] || 0, b: n[2] || 0, s: n[3] || 0, c: hex(c), a: R(c.a) }) }
      if (fx.length) o.fx = fx
    }
    const op = parseFloat(cs.opacity); if (op < 1) o.op = R(op)
    if (cs.clipPath && cs.clipPath.startsWith('polygon(')) o.poly = splitTop(cs.clipPath.slice(8, -1)).map((p) => p.trim().split(/\s+/).map((v, i) => R(v.endsWith('%') ? parseFloat(v) / 100 * (i ? h : w) : parseFloat(v))))
    if (el.classList.contains('x')) o.xx = 1
    return o
  }
  const isFrame = (o) => o.fl || o.cl || o.st || o.fx || o.op != null || o.poly || o.xx || o.edges || o.dots

  const lineText = (node, rects) => {
    // Split a wrapped text node into its visual lines by probing each character's rect.
    const r = document.createRange(), s = node.data, lines = []; let cur = null
    for (let i = 0; i < s.length; i++) {
      r.setStart(node, i); r.setEnd(node, i + 1); const rc = r.getClientRects()[0]; if (!rc) { if (cur) cur.s += s[i]; continue }
      if (!cur || Math.abs(rc.top - cur.top) > 2 && rc.width > 0) { cur = { s: '', top: rc.top, left: rc.left }; lines.push(cur) }
      cur.s += s[i]
    }
    return lines
  }

  const walk = (el, frame, origin, out) => {
    for (const ch of el.childNodes) {
      if (ch.nodeType === 3) {
        const raw = ch.data; if (!raw.trim()) continue
        const pe = ch.parentElement, cs = getComputedStyle(pe)
        if (cs.visibility === 'hidden') continue
        const col = rgba(cs.color); if (!col || col.a === 0) continue
        const r = document.createRange(); r.selectNodeContents(ch)
        const rects = [...r.getClientRects()].filter((q) => q.width > 0.5)
        if (!rects.length) continue
        const tt = (t) => cs.textTransform === 'uppercase' ? t.toLocaleUpperCase('vi') : cs.textTransform === 'lowercase' ? t.toLocaleLowerCase('vi') : t
        const pre = /pre/.test(cs.whiteSpace)
        const clean = (t) => tt(pre ? t : t.replace(/\s+/g, ' ')).trim()
        const fs = parseFloat(cs.fontSize), lhRaw = cs.lineHeight
        const t = { t: 'T', s: clean(raw), x: 0, y: 0, w: 0, f: /mono/i.test(cs.fontFamily.split(',')[0]) ? 'm' : 'v', wt: Math.min(800, +cs.fontWeight), sz: R(fs), lh: R(lhRaw === 'normal' ? rects[0].height : parseFloat(lhRaw)), c: hex(col) }
        if (col.a < 1) t.a = R(col.a)
        const ls = parseFloat(cs.letterSpacing); if (ls) t.ls = R(ls)
        if (cs.fontStyle === 'italic') t.it = 1
        if (/line-through/.test(cs.textDecorationLine)) t.d = 'S'; else if (/underline/.test(cs.textDecorationLine)) t.d = 'U'
        const tops = [...new Set(rects.map((q) => Math.round(q.top)))]
        const al = /center/.test(cs.textAlign) ? 'C' : /right|end/.test(cs.textAlign) ? 'R' : null
        // y: centre the line box on the glyph rect, which is what Figma's fixed line height does
        const yOf = (q) => R(q.top + (q.height - t.lh) / 2 - origin.y)
        if (tops.length === 1) {
          const L = Math.min(...rects.map((q) => q.left)), Rr = Math.max(...rects.map((q) => q.right))
          Object.assign(t, { x: R(L - origin.x), y: yOf(rects[0]), w: R(Rr - L) }); if (al) t.al = al
        } else {
          // block that the text wraps inside
          let blk = pe; while (blk && getComputedStyle(blk).display === 'inline') blk = blk.parentElement
          const bc = getComputedStyle(blk), br = blk.getBoundingClientRect()
          const cl = br.left + parseFloat(bc.borderLeftWidth) + parseFloat(bc.paddingLeft), cw = br.width - parseFloat(bc.borderLeftWidth) - parseFloat(bc.borderRightWidth) - parseFloat(bc.paddingLeft) - parseFloat(bc.paddingRight)
          const lefts = rects.map((q) => q.left)
          const sameLeft = Math.max(...lefts) - Math.min(...lefts) < 1.5
          if (al || (sameLeft && Math.abs(lefts[0] - cl) < 1.5)) {
            Object.assign(t, { x: R(al ? cl : lefts[0]) - origin.x, y: yOf(rects[0]), w: R(cw + 0.5), wr: 1 }); t.x = R(t.x); if (al) t.al = al
          } else {
            const lines = lineText(ch).filter((l) => l.s.trim())
            Object.assign(t, { x: R(rects[0].left - origin.x), y: yOf(rects[0]), w: R(Math.max(...rects.map((q) => q.width))), wr: 1 })
            t.split = lines.map((l) => ({ s: clean(l.s), x: R(l.left - origin.x), y: R(l.top + (rects[0].height - t.lh) / 2 - origin.y) }))
          }
        }
        out.push(t)
        continue
      }
      if (ch.nodeType !== 1) continue
      const cs = getComputedStyle(ch)
      if (cs.display === 'none' || ch.tagName === 'BR' || ch.tagName === 'SCRIPT' || ch.tagName === 'STYLE') continue
      const rect = ch.getBoundingClientRect()
      const tfm = cs.transform && cs.transform !== 'none' ? cs.transform.match(/matrix\(([^)]+)\)/) : null
      const tf = tfm && tfm[1].split(',').map(Number).slice(0, 4).join() !== '1,0,0,1' ? tfm : null
      const w = tf ? ch.offsetWidth : rect.width, h = tf ? ch.offsetHeight : rect.height
      const o = cs.visibility === 'hidden' ? {} : frameProps(ch, cs, w, h)
      if (isFrame(o) && w > 0 && h > 0) {
        const n = { t: 'F', n: [...ch.classList].filter((c) => c !== '__np').join(' ') || ch.tagName.toLowerCase(), x: R(rect.left - origin.x), y: R(rect.top - origin.y), w: R(w), h: R(h) }
        const extra = [...(o.edges || []), ...(o.dots || [])]; delete o.edges; delete o.dots
        Object.assign(n, o)
        if (tf) {
          const [a, b, c, d] = tf[1].split(',').map(Number)
          const cx = rect.left + rect.width / 2 - origin.x, cy = rect.top + rect.height / 2 - origin.y
          delete n.x; delete n.y
          n.rt = [[a, c, R(cx - (a * w / 2 + c * h / 2))], [b, d, R(cy - (b * w / 2 + d * h / 2))]]
        }
        const kids = [...extra]
        walk(ch, n, { x: tf ? rect.left : rect.left, y: tf ? rect.top : rect.top }, kids)
        if (kids.length) n.ch = kids
        // keep key order like v4: ch last
        out.push(n)
      } else walk(ch, frame, origin, out)
    }
  }

  pseudo(document.getElementById('board') || document.body)
  const groups = []
  for (const sec of document.querySelectorAll('section.group[id^="fe-"]')) {
    const g = { code: sec.querySelector('.group-head .code').textContent.trim(), name: '', screens: [] }
    g.name = g.code + ' · ' + sec.querySelector('.group-head h2').textContent.trim()
    for (const fig of sec.querySelectorAll('figure.frame')) {
      const no = fig.querySelector('.cap .no').textContent.trim(), nm = fig.querySelector('.cap h3').textContent.trim()
      const wf = fig.querySelector('.wf'), rr = wf.getBoundingClientRect(), cs = getComputedStyle(wf)
      const root = Object.assign({ t: 'F', n: no + ' ' + nm, x: 0, y: 0, w: R(rr.width), h: R(rr.height) }, frameProps(wf, cs, rr.width, rr.height), { cl: 1 })
      const kids = []; walk(wf, root, { x: rr.left, y: rr.top }, kids); root.ch = kids
      g.screens.push({ no, w: Math.round(rr.width), h: Math.round(rr.height), rb: 0, root })
    }
    groups.push(g)
  }
  return groups
})()

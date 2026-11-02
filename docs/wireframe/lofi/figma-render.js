// Paste after the contents of screens.js (without its last line) into the Figma MCP use_figma tool.
// Draws every sheet as one frame on the current page, using the same shapes as the HTML page.
const fonts = (await figma.listAvailableFontsAsync()).filter((f) => f.fontName.family === 'Mali').map((f) => f.fontName.style)
const REG = { family: 'Mali', style: 'Regular' }
const BOLD = { family: 'Mali', style: fonts.includes('SemiBold') ? 'SemiBold' : 'Bold' }
await figma.loadFontAsync(REG)
await figma.loadFontAsync(BOLD)
const COL = { k: { r: 0, g: 0, b: 0 }, w: { r: 1, g: 1, b: 1 } }
const paint = (c, opacity = 1) => [{ type: 'SOLID', color: COL[c], opacity }]
const OY = 30
const page = figma.currentPage
page.name = 'Wireframe phác thảo'

function addText(f, s, x, y, o = {}) {
  const t = figma.createText()
  f.appendChild(t)
  t.fontName = o.b ? BOLD : REG
  t.fontSize = o.size || 13
  t.characters = s
  t.lineHeight = { value: Math.round((o.size || 13) * (o.lh || 1.25)), unit: 'PIXELS' }
  t.fills = paint(o.c || 'k')
  t.name = s.slice(0, 40)
  if (o.w) {
    t.resize(o.w, t.height)
    t.textAutoResize = 'HEIGHT'
    if (o.align === 'c') t.textAlignHorizontal = 'CENTER'
  }
  t.x = x
  t.y = y
  return t
}

const ids = {}
SCREENS.forEach((s, i) => {
  const f = figma.createFrame()
  page.appendChild(f)
  f.name = `${s.id} ${s.name}`
  f.resize(SHEET.w, SHEET.h + OY)
  f.fills = paint('w')
  f.clipsContent = true
  f.x = 100 + (i % 4) * (SHEET.w + 80)
  f.y = 100 + Math.floor(i / 4) * (SHEET.h + OY + 80)
  const d = makeDraw()
  d.text(0, -30, `${s.id}  ${s.name}`, { size: 18, b: 1 })
  s.draw(d)
  for (const n of d.nodes) {
    if (n.t === 'x') {
      addText(f, n.s, n.x, n.y + OY, n)
      continue
    }
    if (n.t === 'l') {
      // A rotated line is unreliable through the plugin API, so every line is a vector.
      const x = Math.min(n.x1, n.x2), y = Math.min(n.y1, n.y2)
      const v = figma.createVector()
      f.appendChild(v)
      v.vectorPaths = [{ windingRule: 'NONE', data: `M ${n.x1 - x} ${n.y1 - y} L ${n.x2 - x} ${n.y2 - y}` }]
      v.strokes = paint('k')
      v.strokeWeight = n.sw || 1.5
      v.x = x
      v.y = y + OY
      v.name = 'Đường'
      continue
    }
    const r = figma.createRectangle()
    f.appendChild(r)
    r.resize(n.w, n.h)
    r.x = n.x
    r.y = n.y + OY
    r.cornerRadius = n.r || 0
    r.name = 'Khung'
    if (n.fill === 'dim') {
      r.fills = paint('w', 0.8)
      continue
    }
    r.fills = n.fill ? paint(n.fill) : []
    r.strokes = paint(n.stroke || 'k')
    r.strokeWeight = n.sw || 1.5
    r.strokeAlign = 'INSIDE'
    if (n.dash) r.dashPattern = [5, 4]
  }
  let y = OY + 8
  y += addText(f, 'Ghi chú', 924, y, { size: 14, b: 1, lh: 1.4 }).height + 10
  s.notes.forEach((t, k) => {
    const mark = d.marks.has(k + 1) ? `${k + 1}. ` : '– '
    y += addText(f, mark + t, 924, y, { size: 14, w: 296, lh: 1.4 }).height + 10
  })
  ids[s.id] = f.id
})
return { ids }

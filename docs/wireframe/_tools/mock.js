// Chạy thử code.js của plugin trên một Figma giả lập (không cần Figma):
//   node mock.js ../figma-v6/code.js
// Lần 1 trên trang trống (dựng mọi màn), lần 2 trên chính kết quả đó (chỉ dựng lại màn rb:1).
const fs = require('fs'), vm = require('vm')
const code = fs.readFileSync(process.argv[2] || '', 'utf8')
let uid = 0
const node = (type) => { const n = { id: String(++uid), type, name: '', children: [], x: 0, y: 0, width: 0, height: 0, parent: null,
  appendChild(c) { if (c.parent) c.parent.children.splice(c.parent.children.indexOf(c), 1); c.parent = this; this.children.push(c) },
  remove() { if (this.parent) this.parent.children.splice(this.parent.children.indexOf(this), 1); this.parent = null },
  resize(w, h) { this.width = w; this.height = h }, resizeWithoutConstraints(w, h) { this.width = w; this.height = h },
  findAll(f) { const o = []; const w = (n) => n.children.forEach((c) => { if (f(c)) o.push(c); w(c) }); w(this); return o },
  findAllWithCriteria({ types }) { return this.findAll((c) => types.includes(c.type)) } }
  if (type === 'TEXT') Object.defineProperty(n, 'characters', { get() { return this._c || '' }, set(v) { this._c = v; this.width = v.length * 6; this.height = 16 } })
  return n }
const run = async (page) => {
  let msg; const figma = { root: { children: [page], getSharedPluginDataKeys: () => [], setSharedPluginData() {} }, currentPage: page,
    setCurrentPageAsync: async () => {}, loadFontAsync: async () => {}, createFrame: () => node('FRAME'), createText: () => node('TEXT'),
    createVector: () => node('VECTOR'), createSection: () => node('SECTION'), viewport: { scrollAndZoomIntoView() {} }, closePlugin: (m) => { msg = m } }
  vm.runInNewContext(code, { figma, console })
  for (let i = 0; i < 300 && !msg; i++) await new Promise((r) => setTimeout(r, 10))
  return msg }
;(async () => {
  const page = node('PAGE'); page.name = 'Wireframe'
  console.log('Trang trống :', await run(page))
  console.log('Chạy lần 2  :', await run(page))
})()

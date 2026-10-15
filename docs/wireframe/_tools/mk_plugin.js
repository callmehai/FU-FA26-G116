// Tạo figma-v<mới>/ (manifest.json + code.js) từ plugin bản trước.
//   node mk_plugin.js <bản cũ> <bản mới> "<ghi chú đầu file>" [FE-xx để Figma cuộn tới]
// Cần sẵn out/v<cũ>.json và out/v<mới>.json (node run.mjs extract …).
// Màn nào khác giữa hai lần trích (hoặc mới có) → rb:1, plugin dựng lại; màn còn lại giữ dữ liệu của plugin cũ.
const fs = require('fs'), path = require('path')
const [prev, next, note, focus = ''] = process.argv.slice(2)
if (!prev || !next || !note) { console.error('Cách dùng: node mk_plugin.js 5 6 "ghi chú" [FE-11]'); process.exit(1) }
const W = path.resolve(__dirname, '..') + '/', OUT = __dirname + '/out/'
const base = fs.readFileSync(W + `figma-v${prev}/code.js`, 'utf8')
const i = base.indexOf('const LAYOUT='), j = base.indexOf('\n', i)
const plugOld = {}; JSON.parse(base.slice(i + 13, j).replace(/;\s*$/, '')).forEach((g) => g.screens.forEach((s) => (plugOld[s.no] = s)))
const exOld = {}; JSON.parse(fs.readFileSync(OUT + `v${prev}.json`, 'utf8')).forEach((g) => g.screens.forEach((s) => (exOld[s.no] = s.root)))
// Bỏ qua lệch làm tròn: vị trí khung trên trang đổi khi thêm nội dung phía trên, toạ độ con lệch ±0.01px.
const same = (a, b) => typeof a === 'number' && typeof b === 'number' ? Math.abs(a - b) < 0.05
  : a && b && typeof a === 'object' && typeof b === 'object' ? Object.keys(a).length === Object.keys(b).length && Object.keys(a).every((k) => same(a[k], b[k])) : a === b
const changed = [], added = []
const layout = JSON.parse(fs.readFileSync(OUT + `v${next}.json`, 'utf8')).map((g) => ({ code: g.code, name: g.name, screens: g.screens.map((s) => {
  if (!plugOld[s.no]) { added.push(s.no); return Object.assign({}, s, { rb: 1 }) }
  if (!same(exOld[s.no], s.root)) { changed.push(s.no); return Object.assign({}, s, { rb: 1 }) }
  return Object.assign({}, plugOld[s.no], { rb: 0 })
}) }))
let code = `// figma v${next} – TrustCabinet: ${note}\nconst LAYOUT=${JSON.stringify(layout)};` + base.slice(j)
// Phần dưới LAYOUT giữ nguyên từ bản cũ, chỉ đổi tên bản trong thông báo và section được cuộn tới.
code = code.replace(/figma\.closePlugin\('figma v\d+:/, `figma.closePlugin('figma v${next}:`)
if (focus) code = code.replace(/s\.name\.startsWith\('FE-\d\d '\)\);if\(f7\)/, `s.name.startsWith('${focus} '));if(f7)`)
fs.mkdirSync(W + `figma-v${next}`, { recursive: true })
fs.writeFileSync(W + `figma-v${next}/code.js`, code)
const man = JSON.parse(fs.readFileSync(W + `figma-v${prev}/manifest.json`, 'utf8'))
man.name = `figma v${next} – TrustCabinet`; man.id = `trustcabinet-figma-v${next}`
fs.writeFileSync(W + `figma-v${next}/manifest.json`, JSON.stringify(man, null, 2) + '\n')
console.log('Màn sửa:', changed.join(', ') || '(không)')
console.log('Màn mới:', added.join(', ') || '(không)')
console.log(`Đã ghi figma-v${next}/ — chụp lại ảnh bằng:\n  node run.mjs shots ../TrustCabinet-wireframe-v${next}.html ../screens ${[...changed, ...added].join(',')}`)

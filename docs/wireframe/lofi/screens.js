// Lo-fi wireframe source, black and white only. Each screen pushes plain shapes, so the HTML page and
// the Figma script draw the same thing. Coordinates are px on a 1240×660 sheet: screen left, notes right.
const SHEET = { w: 1240, h: 660, screenW: 900 }

function makeDraw() {
  const n = []
  const marks = new Set()
  const d = {
    marks,
    nodes: n,
    rect: (x, y, w, h, o = {}) => n.push({ t: 'r', x, y, w, h, ...o }),
    text: (x, y, s, o = {}) => n.push({ t: 'x', x, y, s, size: 13, ...o }),
    line: (x1, y1, x2, y2, o = {}) => n.push({ t: 'l', x1, y1, x2, y2, ...o }),
    ph: (x, y, w, h) => { d.rect(x, y, w, h); d.line(x, y, x + w, y + h); d.line(x + w, y, x, y + h) },
    ic: (x, y, s = 14) => d.rect(x, y, s, s, { r: 2 }),
    av: (x, y, s = 26) => d.rect(x, y, s, s, { r: s / 2 }),
    lines: (x, y, w, c = 3) => { for (let i = 0; i < c; i++) d.line(x, y + i * 14, x + w * (i === c - 1 ? 0.6 : 1 - (i % 2) * 0.1), y + i * 14, { sw: 1 }) },
    btn: (x, y, w, s, o = {}) => {
      const h = o.h || 32
      d.rect(x, y, w, h, { r: 4, fill: o.main ? 'k' : undefined, dash: o.off })
      const ix = o.icon ? 12 : 0
      if (o.icon) d.rect(x + 9, y + h / 2 - 6, 12, 12, { r: 2, stroke: o.main ? 'w' : 'k' })
      d.text(x + ix * 2, y + h / 2 - 8, s, { w: w - ix * 2, align: 'c', size: 12, c: o.main ? 'w' : 'k' })
    },
    input: (x, y, w, label, val = '', o = {}) => {
      if (label) d.text(x, y, label + (o.req ? ' *' : ''), { size: 12 })
      const top = label ? y + 18 : y, h = o.h || 32
      d.rect(x, top, w, h, { r: 4 })
      if (val) d.text(x + 9, top + 7, val, { size: 12 })
      if (o.sel) d.text(x + w - 18, top + 6, '⌄')
      if (o.ic) d.ic(x + w - 22, top + 9)
    },
    seg: (x, y, w, items, on) => {
      const c = w / items.length
      items.forEach((s, i) => { d.rect(x + i * c, y, c + (i < items.length - 1 ? 1.5 : 0), 30, { fill: i === on ? 'k' : undefined }); d.text(x + i * c, y + 6, s, { w: c, align: 'c', size: 12, c: i === on ? 'w' : 'k' }) })
    },
    note: (x, y, k) => { marks.add(k); d.rect(x, y, 20, 20, { r: 10, fill: 'k' }); d.text(x, y + 2, String(k), { w: 20, align: 'c', size: 12, c: 'w', b: 1 }) },
    table: (x, y, w, cols, rows, o = {}) => {
      const rh = o.rh || 34; let cx = x
      d.rect(x, y, w, 30)
      cols.forEach(([s, cw]) => { d.text(cx + 10, y + 6, s, { size: 12, b: 1 }); cx += cw })
      rows.forEach((row, r) => {
        const ry = y + 30 + r * rh; d.rect(x, ry - 1.5, w, rh + 1.5); cx = x
        row.forEach((s, c) => { if (s === '#i') { d.ic(cx + 10, ry + 10) } else if (s === '#a' || s === '#4') { Array.from({ length: s === '#4' ? 4 : 3 }, (_, k) => d.ic(cx + 10 + k * 20, ry + 10)) } else d.text(cx + 10, ry + 8, s, { size: 12 }); cx += cols[c][1] })
      })
    },
  }
  return d
}

const NAV = ['Lịch làm việc', 'Cuộc họp', 'Kho tài liệu', 'Giọng nói của tôi']
function shell(d, on, o = {}) {
  d.rect(0, 0, 900, 620); d.rect(0, 0, 170, 620)
  d.rect(14, 14, 28, 28, { r: 14 }); d.text(50, 19, 'TrustCabinet', { b: 1 })
  d.text(14, 62, 'Nghiệp vụ', { size: 11 })
  NAV.forEach((s, i) => {
    const y = 82 + i * 36
    if (i === on) d.rect(8, y, 154, 32, { r: 4, fill: 'k' })
    d.rect(18, y + 9, 14, 14, { r: 2, stroke: i === on ? 'w' : 'k' }); d.text(40, y + 7, s, { size: 12, c: i === on ? 'w' : 'k' })
  })
  if (o.admin) {
    d.text(14, 240, 'Hệ thống', { size: 11 }); d.ic(18, 266); d.text(40, 264, 'Quản trị', { size: 12 })
    ;['Người dùng & Quyền', 'Vai trò'].forEach((s, i) => { const y = 292 + i * 32; if (i === o.admin - 1) d.rect(28, y, 134, 28, { r: 4, fill: 'k' }); d.text(38, y + 5, s, { size: 12, c: i === o.admin - 1 ? 'w' : 'k' }) })
  }
  d.line(0, 574, 170, 574); d.av(12, 584); d.text(46, 582, o.who || 'Tên người dùng', { size: 12 }); d.text(46, 598, o.role || 'Vai trò', { size: 10 }); d.ic(144, 590)
}
const TABS = ['Thông tin chung', 'Thành phần', 'Nội dung họp', 'Tài liệu', 'Biểu quyết', 'Bản ghi', 'Biên bản']
function detail(d, tab, o = {}) {
  shell(d, 1, o)
  d.text(190, 16, '‹ Cuộc họp', { size: 12 })
  d.text(190, 34, 'Tên cuộc họp', { size: 20, b: 1 }); d.text(340, 41, o.st || '○ Trạng thái', { size: 12, b: o.live })
  d.ic(190, 66, 12); d.text(206, 62, 'Thứ, ngày, giờ', { size: 12 }); d.ic(310, 66, 12); d.text(326, 62, 'Hình thức, địa điểm', { size: 12 })
  ;(o.actions || []).forEach(([s, w, main], i, all) => { const right = 880 - all.slice(i + 1).reduce((a, b) => a + b[1] + 8, 0); d.btn(right - w, 30, w, s, { main, icon: 1 }) })
  let y = 86
  if (o.bar) { d.rect(190, y, 690, 50, { r: 4 }); d.ic(202, y + 18); d.text(226, y + 7, o.bar[0], { b: 1, size: 12 }); d.text(226, y + 26, o.bar[1], { size: 11 }); (o.bar[2] || []).forEach(([s, w, main], i, all) => { const right = 870 - all.slice(i + 1).reduce((a, b) => a + b[1] + 8, 0); d.btn(right - w, y + 9, w, s, { main, icon: 1 }) }); y += 60 }
  let tx = 190
  TABS.forEach((s) => { const w = s.length * 7 + 16; d.text(tx, y + 4, s, { w, align: 'c', size: 12, b: s === TABS[tab] }); if (s === TABS[tab]) d.rect(tx, y + 24, w, 3, { fill: 'k' }); tx += w })
  d.line(190, y + 27, 880, y + 27)
  y += 38
  if (o.rail === false) return y
  d.rect(710, y, 170, 150, { r: 4 }); d.text(720, y + 8, 'Lịch sử trạng thái', { size: 12, b: 1 })
  ;['Tạo cuộc họp', 'Gửi duyệt', 'Duyệt', 'Công bố'].forEach((s, i) => { d.rect(720, y + 36 + i * 26, 10, 10, { r: 5 }); d.text(736, y + 31 + i * 26, s, { size: 11 }) })
  return y
}
function dialog(d, w, h, title, foot) {
  d.rect(0, 0, 900, 620, { fill: 'dim' })
  const x = (900 - w) / 2, y = (620 - h) / 2
  d.rect(x, y, w, h, { r: 6, fill: 'w' }); d.text(x + 18, y + 14, title, { size: 16, b: 1 }); d.text(x + w - 28, y + 12, '×', { size: 16 })
  d.line(x, y + h - 52, x + w, y + h - 52)
  let right = x + w - 18
  foot.slice().reverse().forEach(([s, bw, main]) => { d.btn(right - bw, y + h - 42, bw, s, { main }); right -= bw + 8 })
  return { x: x + 18, y: y + 48, w: w - 36 }
}

const FLOW = [
  ['Đăng nhập', 'Lịch làm việc', 'Danh sách cuộc họp', 'Tạo cuộc họp'],
  ['Chi tiết + gửi duyệt', 'Duyệt rồi công bố', 'Bắt đầu họp', 'Phòng họp'],
  ['Biểu quyết', 'Kết thúc họp', 'Bản ghi, gỡ băng', 'Biên bản, ký'],
]

const SCREENS = [
  { id: '00', name: 'Luồng màn hình', notes: ['Vẽ luồng trước, chốt có bao nhiêu màn rồi mới vẽ từng màn.', 'Ba hàng là ba chặng: tạo cuộc họp, duyệt và vào họp, họp và sau họp.', 'Số trong ô là số màn ở các trang sau.', 'Các màn phụ như hộp xác nhận không vẽ riêng, ghi ở ghi chú.'],
    draw(d) {
      d.rect(0, 0, 900, 620)
      const num = [['01', '02', '03', '04'], ['05', '05', '05', '19, 12'], ['10', '05', '11, 20', '15']]
      ;['Tạo cuộc họp', 'Duyệt và vào họp', 'Họp và sau họp'].forEach((s, r) => {
        const y = 70 + r * 170
        d.text(30, y - 34, s, { size: 16, b: 1 })
        FLOW[r].forEach((b, c) => {
          const x = 30 + c * 215
          d.rect(x, y, 175, 80, { r: 6 }); d.text(x, y + 20, b, { w: 175, align: 'c' }); d.text(x + 8, y + 56, '#' + num[r][c], { size: 11 })
          if (c < 3) { d.line(x + 175, y + 40, x + 215, y + 40); d.text(x + 200, y + 29, '›', { size: 16 }) }
        })
      })
    } },
  { id: '01', name: 'Đăng nhập', notes: ['Email + mật khẩu, hoặc Google.', 'Hỏng thì báo ngay trên form.', 'Mở link cuộc họp khi chưa đăng nhập: hiện một dòng nhắc, đăng nhập xong quay lại đúng trang.'],
    draw(d) {
      d.rect(0, 0, 900, 620); d.rect(300, 80, 300, 440, { r: 6 })
      d.rect(430, 104, 40, 40, { r: 20 }); d.text(300, 152, 'TrustCabinet', { w: 300, align: 'c', size: 18, b: 1 })
      d.input(330, 200, 240, 'Email'); d.input(330, 266, 240, 'Mật khẩu', '', { ic: 1 })
      d.btn(330, 338, 240, 'Đăng nhập bằng mật khẩu', { main: 1, icon: 1, h: 36 }); d.text(300, 386, 'hoặc', { w: 300, align: 'c', size: 12 })
      d.btn(330, 412, 240, 'Tiếp tục bằng Google', { icon: 1, h: 36 }); d.note(576, 344, 1)
    } },
  { id: '02', name: 'Lịch làm việc', notes: ['Trang đầu tiên sau đăng nhập.', 'Bộ lọc mở sẵn cuộc họp sắp diễn ra.', 'Bấm một cuộc họp để vào chi tiết.', 'Có thêm kiểu Tháng và Danh sách, dùng cùng thanh công cụ.'],
    draw(d) {
      shell(d, 0)
      d.btn(190, 16, 70, 'Hôm nay'); d.btn(268, 16, 32, '‹'); d.btn(304, 16, 32, '›'); d.text(348, 21, 'Khoảng ngày ⌄', { size: 15, b: 1 })
      d.seg(480, 17, 150, ['Lịch', 'Danh sách'], 0); d.btn(638, 16, 76, 'Tuần ⌄'); d.btn(720, 16, 80, 'Bộ lọc', { icon: 1 }); d.btn(806, 16, 74, 'Tạo', { main: 1 })
      d.rect(190, 60, 690, 520); ['T2', 'T3', 'T4', 'T5', 'T6'].forEach((s, i) => { d.text(240 + i * 128, 66, s, { w: 128, align: 'c', b: 1 }); d.line(240 + i * 128, 60, 240 + i * 128, 580) })
      d.line(190, 92, 880, 92); for (let i = 0; i < 9; i++) { d.text(198, 96 + i * 54, (8 + i) + ':00', { size: 11 }); d.line(240, 146 + i * 54, 880, 146 + i * 54, { sw: 1 }) }
      ;[[248, 150, 90], [376, 260, 70], [632, 204, 110]].forEach(([x, y, h]) => { d.rect(x, y, 112, h, { r: 4 }); d.lines(x + 8, y + 16, 90, 2) })
      d.note(784, 6, 2); d.note(350, 148, 3); d.note(702, 6, 4)
      d.text(190, 590, '■ Trạng thái  □ Trạng thái  □ Trạng thái  □ Trạng thái (chú giải)', { size: 11 })
    } },
  { id: '03', name: 'Danh sách cuộc họp', notes: ['Bốn ô đếm vừa là số liệu vừa là nút lọc nhanh.', 'Mở trang là lọc Đang diễn ra.', 'Không thấy gì thì có nút Xem mọi cuộc họp.', 'Nút Tạo chỉ hiện với người có quyền.'],
    draw(d) {
      shell(d, 1); d.text(190, 22, 'Danh sách cuộc họp', { size: 20, b: 1 }); d.btn(760, 18, 120, 'Tạo cuộc họp', { main: 1, icon: 1, h: 36 })
      ;['Đang diễn ra', 'Đã công bố', 'Chờ duyệt', 'Bản nháp'].forEach((s, i) => { d.rect(190 + i * 175, 66, 165, 60, { r: 4 }); d.text(202 + i * 175, 74, s, { size: 12 }); d.text(202 + i * 175, 92, '0', { size: 20, b: 1 }) })
      d.input(190, 142, 280, '', 'Tìm theo tên…'); d.input(480, 142, 130, '', 'Trạng thái', { sel: 1 }); d.input(620, 142, 120, '', 'Mức độ', { sel: 1 }); d.input(750, 142, 130, '', 'Thời điểm', { sel: 1 })
      d.table(190, 188, 690, [['STT', 50], ['Tên cuộc họp', 230], ['Thời gian', 130], ['Hình thức', 100], ['Trạng thái', 90], ['Mức độ', 60], ['', 30]], [['1', 'Tên cuộc họp', 'Giờ, ngày', 'Trực tiếp', 'Trạng thái', 'Mức', '#i'], ['2', 'Tên cuộc họp', 'Giờ, ngày', 'Trực tuyến', 'Trạng thái', 'Mức', '#i'], ['3', 'Tên cuộc họp', 'Giờ, ngày', 'Kết hợp', 'Trạng thái', 'Mức', '#i']], { rh: 44 })
      d.text(190, 590, 'Hiển thị 1–3   Số dòng [20]', { size: 12 }); d.text(800, 590, '‹ 1 2 3 ›', { size: 13 })
      d.note(344, 58, 1); d.note(600, 134, 2); d.note(750, 10, 4)
    } },
  { id: '04', name: 'Tạo cuộc họp', notes: ['Hộp thoại, không phải trang riêng. Form dài thì cuộn trong hộp.', 'Hình thức quyết định ô hiện ra: Trực tiếp có Địa điểm, Trực tuyến có Link, Kết hợp có cả hai.', 'Có dòng báo cách ghi âm theo hình thức.', 'Thành phần: tìm người, mỗi người một vai trò, đúng một Chủ tọa. Sửa cuộc họp dùng lại form này.'],
    draw(d) {
      shell(d, 1); const b = dialog(d, 620, 570, 'Tạo cuộc họp', [['Huỷ', 70], ['Tạo cuộc họp', 120, 1]])
      d.input(b.x, b.y, b.w, 'Tên cuộc họp', '', { req: 1 }); d.input(b.x, b.y + 58, b.w, 'Mô tả', '', { h: 44 })
      const c = (b.w - 20) / 3
      ;['Ngày họp', 'Bắt đầu', 'Kết thúc'].forEach((s, i) => d.input(b.x + i * (c + 10), b.y + 128, c, s, '', { req: 1, ic: 1 }))
      d.text(b.x, b.y + 186, 'Hình thức', { size: 12 }); d.seg(b.x, b.y + 204, 280, ['Trực tiếp', 'Trực tuyến', 'Kết hợp'], 2); d.text(b.x + 300, b.y + 186, 'Mức độ mật', { size: 12 }); d.seg(b.x + 300, b.y + 204, 284, ['Thường', 'Nội bộ', 'Hạn chế', 'Mật'], 0)
      d.input(b.x, b.y + 242, 280, 'Địa điểm'); d.input(b.x + 300, b.y + 242, 284, 'Link trực tuyến')
      d.ic(b.x, b.y + 300, 12); d.text(b.x + 18, b.y + 296, 'Dòng báo cách ghi âm', { size: 12 })
      d.input(b.x, b.y + 326, b.w, 'Người dự họp', 'Tìm theo tên hoặc email', { req: 1 })
      d.rect(b.x, b.y + 382, b.w, 56); d.av(b.x + 8, b.y + 396); d.text(b.x + 42, b.y + 400, 'Tên (Tôi)', { size: 12 }); d.input(b.x + 430, b.y + 394, 110, '', 'Chủ tọa', { sel: 1 }); d.ic(b.x + 556, b.y + 403)
      d.note(b.x + 262, b.y + 182, 2); d.note(b.x + 150, b.y + 294, 3); d.note(b.x + 566, b.y + 326, 4)
    } },
  { id: '05', name: 'Chi tiết – Thông tin chung', notes: ['Khung chung cho mọi tab: đầu trang, tab, cột Lịch sử trạng thái.', 'Thanh trạng thái đổi theo bước: Bản nháp, Chờ duyệt, Bị từ chối, Đã duyệt. Mỗi vai trò thấy nút của mình.', 'Nút đầu trang đổi theo trạng thái: Chỉnh sửa, Dời lịch, Huỷ cuộc họp, Bắt đầu họp, Kết thúc họp, Vào phòng họp.', 'Không áp dụng thì ẩn, không làm mờ.', 'Từ chối, huỷ: bắt buộc nhập lý do.'],
    draw(d) {
      const y = detail(d, 0, { actions: [['Chỉnh sửa', 100], ['Huỷ cuộc họp', 120]], bar: ['Bản nháp — chưa gửi duyệt', 'Thông tin đã đủ. Gửi cho người duyệt khi sẵn sàng.', [['Gửi duyệt', 100, 1]]] })
      d.rect(190, y, 510, 330, { r: 4 })
      ;[['Trạng thái', 0, 0], ['Mức độ', 1, 0], ['Thời gian', 0, 1], ['Hình thức', 0, 2], ['Địa điểm', 1, 2], ['Link trực tuyến', 2, 2], ['Chủ tọa', 0, 3], ['Thư ký', 1, 3], ['Người duyệt', 2, 3], ['Mô tả', 0, 4]].forEach(([s, c, r]) => { d.text(204 + c * 165, y + 14 + r * 60, s, { size: 11 }); d.line(204 + c * 165, y + 42 + r * 60, 330 + c * 165, y + 42 + r * 60, { sw: 1 }) })
      d.note(872, 80, 2); d.note(872, 20, 3)
    } },
  { id: '06', name: 'Thành phần tham dự', notes: ['Hai nút icon: Mời khách ngoài hệ thống, Thêm người dự họp.', 'Mỗi dòng: Đổi vai trò, Gỡ. Dòng Chủ tọa không có nút.', 'Cột Điểm danh chỉ có khi đang họp hoặc đã kết thúc. Thư ký chọn, chọn là lưu; người khác chỉ đọc.', 'Phản hồi lời mời là nhãn cạnh tên.'],
    draw(d) {
      const y = detail(d, 1, { st: 'Đang diễn ra', live: 1 })
      d.rect(190, y, 510, 280, { r: 4 }); d.input(200, y + 10, 240, '', 'Tìm theo tên hoặc email'); d.rect(630, y + 10, 30, 32); d.rect(664, y + 10, 30, 32)
      d.table(200, y + 54, 490, [['Thành phần', 220], ['Vai trò', 90], ['Điểm danh', 110], ['', 70]], [['Tên (Tôi)', 'Thư ký', 'Điểm danh ⌄', '#a'], ['Tên', 'Chủ tọa', 'Điểm danh ⌄', ''], ['Tên [Tham dự]', 'Thành viên', 'Có mặt ⌄', '#a'], ['Khách', 'Khách', 'Vắng ⌄', '#a']], { rh: 40 })
      d.note(686, y + 2, 1); d.note(590, y + 58, 3); d.note(692, y + 128, 2)
    } },
  { id: '07', name: 'Nội dung cuộc họp', notes: ['Mỗi mục một thẻ, kéo chấm bên trái để đổi thứ tự.', 'Nút trên thẻ: Thêm tài liệu, Sửa, Xoá (mục đã bàn không xoá).', 'Chủ tọa thấy Bắt đầu, Xong để điều hành; hệ thống ghi giờ thực tế.', 'Mở thẻ: khối Tài liệu và khối Kết luận.'],
    draw(d) {
      const y = detail(d, 2, { st: 'Đang diễn ra', live: 1, actions: [['Vào phòng họp', 120, 1], ['Kết thúc họp', 110]] })
      d.rect(190, y, 510, 380, { r: 4 }); d.btn(566, y + 8, 124, 'Thêm nội dung', { icon: 1 })
      ;[0, 1, 2].forEach((i) => {
        const ty = y + 50 + (i === 2 ? 220 : i * 70), h = i === 1 ? 140 : 60
        d.rect(200, ty, 490, h, { r: 4, sw: i === 1 ? 2 : 1 }); d.text(208, ty + 8, '⋮⋮', { size: 12 }); d.rect(226, ty + 8, 22, 22, { r: 11 }); d.text(226, ty + 11, String(i + 1), { w: 22, align: 'c', size: 11 })
        d.text(258, ty + 9, i === 1 ? '⌄  Tên nội dung' : '›  Tên nội dung', { size: 13, b: 1 }); d.av(258, ty + 34, 18); d.text(282, ty + 34, 'Người trình bày', { size: 11 }); d.text(392, ty + 34, ['Đã xong', 'Đang bàn', ''][i], { size: 11, b: 1 })
        d.ic(640, ty + 12); d.ic(664, ty + 12)
        if (i === 1) { d.rect(258, ty + 62, 205, 68, { r: 4 }); d.text(266, ty + 68, 'Tài liệu', { size: 11, b: 1 }); d.lines(266, ty + 92, 180, 2); d.rect(473, ty + 62, 205, 68, { r: 4 }); d.text(481, ty + 68, 'Kết luận', { size: 11, b: 1 }); d.lines(481, ty + 92, 180, 2); d.btn(560, ty + 6, 64, 'Xong', { main: 1, h: 26 }) }
      })
      d.note(196, y + 48, 1); d.note(692, y + 62, 2); d.note(536, y + 128, 3); d.note(248, y + 172, 4)
    } },
  { id: '08', name: 'Tài liệu cuộc họp', notes: ['Hai nhóm: theo nội dung họp, tài liệu chung.', 'Một tệp gắn được nhiều mục, không tải lên hai lần.', 'Thêm từ máy hoặc từ kho cá nhân. Nút kẹp giấy ở từng mục thêm được từ tài liệu đã có.', 'Dòng tệp: Tải về, Gỡ khỏi mục, Gỡ khỏi cuộc họp.'],
    draw(d) {
      const y = detail(d, 3, { st: '○ Đã công bố' })
      d.seg(190, y, 260, ['Theo nội dung họp', 'Tài liệu chung'], 0); d.btn(530, y - 1, 170, 'Thêm tài liệu chung', { main: 1, icon: 1 })
      ;[[1, 2], [2, 1], [3, 0]].forEach(([k, c], i, a) => {
        const top = y + 42 + a.slice(0, i).reduce((s, x) => s + 38 + x[1] * 42, 0)
        d.rect(190, top - (i ? 1.5 : 0), 510, 38 + (i ? 1.5 : 0)); d.text(200, top + 10, (c ? '⌄  ' : '›  ') + k + '. Tên nội dung', { size: 12, b: 1 }); d.text(610, top + 10, c + ' tài liệu', { size: 11 }); d.ic(676, top + 12)
        for (let f = 0; f < c; f++) { const fy = top + 38 + f * 42; d.rect(190, fy - 1.5, 510, 43.5); d.rect(216, fy + 9, 20, 24); d.text(244, fy + 5, 'Tên-tệp.pdf', { size: 12, b: 1 }); d.text(244, fy + 22, 'Số trang, số góp ý', { size: 10 }); [0, 1, 2].forEach((n) => d.ic(632 + n * 20, fy + 14)) }
      })
      d.note(442, y - 10, 1); d.note(692, y - 12, 3); d.note(606, y + 90, 4)
    } },
  { id: '09', name: 'Xem tài liệu & góp ý', notes: ['Mở toàn màn, không có sidebar.', 'Thu phóng, số trang, bật/tắt cột Góp ý, Tải về.', 'Góp ý gắn với trang đang xem (PDF), trả lời một cấp.', 'Cuộc họp Mật: không tải về, in mờ tên người xem.'],
    draw(d) {
      d.rect(0, 0, 900, 620); d.line(0, 48, 900, 48); d.text(14, 14, '‹ Tài liệu', { size: 12 }); d.rect(84, 12, 20, 24); d.text(112, 13, 'Tên-tệp.pdf', { b: 1 }); d.text(214, 15, 'Tên cuộc họp', { size: 11 })
      d.text(560, 15, 'Trang 1 / 6', { size: 12 }); d.ic(634, 17); d.text(652, 15, '100%', { size: 12 }); d.ic(690, 17); d.btn(714, 8, 80, 'Góp ý', { icon: 1 }); d.btn(800, 8, 88, 'Tải về', { icon: 1 })
      d.ph(120, 66, 400, 536); d.line(640, 48, 640, 620); d.text(652, 60, 'Góp ý', { b: 1 }); d.ic(850, 62); d.ic(870, 62)
      ;[92, 182].forEach((y) => { d.av(652, y); d.text(684, y - 2, 'Tên · Trang 2', { size: 11 }); d.lines(684, y + 18, 190, 2); d.text(684, y + 44, 'Trả lời', { size: 11 }) })
      d.rect(652, 510, 236, 56, { r: 4 }); d.text(660, 516, 'Viết góp ý…', { size: 12 }); d.ic(652, 582); d.text(672, 579, 'Gắn trang 1', { size: 11 }); d.btn(796, 576, 92, 'Gửi góp ý', { h: 28, main: 1 })
      d.note(534, 14, 2); d.note(642, 500, 3)
    } },
  { id: '10', name: 'Biểu quyết', notes: ['Chủ tọa, Thư ký tạo nháp (viền đứt). Chỉ Chủ tọa mở và đóng phiếu.', 'Mỗi lúc một phiếu mở, có sọc đậm. Đang có phiếu mở thì nháp không có nút Mở.', 'Bỏ phiếu một lần, không đổi.', 'Đóng phiếu: thanh kết quả, phiếu kín chỉ có tổng. Chủ tọa ghi kết luận, không chọn sẵn.'],
    draw(d) {
      const y = detail(d, 4, { st: 'Đang diễn ra', live: 1 })
      d.rect(190, y, 510, 400, { r: 4 }); d.btn(566, y + 8, 124, 'Tạo biểu quyết', { icon: 1 })
      d.rect(200, y + 50, 490, 58, { r: 4 }); d.text(210, y + 56, '›  Tên biểu quyết', { b: 1, size: 12 }); d.text(226, y + 80, '○ Đã đóng   ○ Công khai   5/5   ✓ Thông qua', { size: 11 })
      d.rect(200, y + 116, 490, 200, { r: 4, sw: 2 }); d.rect(200, y + 116, 5, 200, { fill: 'k' }); d.text(214, y + 122, '⌄  Tên biểu quyết', { b: 1, size: 12 }); d.btn(600, y + 120, 82, 'Đóng phiếu', { h: 26 })
      d.text(226, y + 146, '[Đang mở]   ○ Công khai   3/5', { size: 11 }); d.rect(226, y + 168, 450, 6); d.rect(226, y + 168, 270, 6, { fill: 'k' })
      ;['Đồng ý', 'Không đồng ý', 'Không ý kiến'].forEach((s, i) => { d.rect(226, y + 182 + i * 32, 450, 28, { r: 4 }); d.rect(236, y + 189 + i * 32, 14, 14, { r: 7 }); d.text(258, y + 187 + i * 32, s, { size: 12 }) })
      d.text(226, y + 284, 'Gửi rồi không đổi, không rút được.', { size: 11 }); d.btn(596, y + 280, 80, 'Gửi phiếu', { h: 28, main: 1 })
      d.rect(200, y + 324, 490, 58, { r: 4, dash: 1 }); d.text(210, y + 330, '›  Tên biểu quyết', { b: 1, size: 12 }); d.text(226, y + 354, '○ Nháp   ○ Kín', { size: 11 }); d.ic(640, y + 334); d.ic(664, y + 334)
      d.note(190, y + 106, 2); d.note(690, y + 314, 1); d.note(572, y + 284, 3); d.note(690, y + 40, 4)
    } },
  { id: '11', name: 'Bản ghi', notes: ['Trực tiếp: Thư ký ghi cả phòng bằng ô góc màn hình, chuyển tab không dừng.', 'Trực tuyến: mỗi người ghi micro của mình trong phòng họp.', 'Bản ghi trực tiếp: lời nói theo từng câu.', 'Mở bản ghi: trình phát, bản gỡ băng do máy tạo, Thư ký sửa và gán tên người nói.', 'Lưu hoặc bật ghi đều phải xác nhận đã báo người dự (dữ liệu cá nhân).'],
    draw(d) {
      const y = detail(d, 5, { st: 'Đang diễn ra', live: 1 })
      d.rect(190, y, 510, 130, { r: 4 }); d.text(202, y + 8, 'Bản ghi trực tiếp', { b: 1 }); d.line(190, y + 34, 700, y + 34)
      ;[0, 1].forEach((i) => { d.text(202, y + 42 + i * 42, '08:32', { size: 11 }); d.text(250, y + 40 + i * 42, 'Tên người nói', { size: 11, b: 1 }); d.lines(250, y + 62 + i * 42, 420, 1) })
      d.rect(190, y + 142, 510, 200, { r: 4 }); d.text(202, y + 150, 'Bản ghi', { b: 1 }); d.btn(584, y + 148, 106, 'Tải tệp lên', { icon: 1, h: 28 })
      d.text(202, y + 186, 'ten-ban-ghi.wav', { size: 12, b: 1 }); d.text(202, y + 204, '[Mic phòng] [Có bản gỡ băng]  thời lượng, dung lượng, giờ', { size: 11 }); [0, 1, 2].forEach((n) => d.ic(640 + n * 20, y + 192))
      d.rect(202, y + 226, 486, 30, { r: 4 }); d.text(212, y + 232, '0:00 / 1:09  ———●———  ‹5  ▶  5›  1×', { size: 11 }); d.lines(202, y + 272, 470, 3)
      d.rect(620, 490, 250, 110, { r: 6, fill: 'w', sw: 2 }); d.rect(620, 490, 250, 28, { fill: 'k' }); d.text(630, 495, 'Tên cuộc họp', { size: 12, c: 'w' }); d.text(632, 526, 'Mic phòng đang tắt', { size: 12, b: 1 }); d.rect(632, 548, 26, 26, { r: 13 }); d.lines(668, 561, 190, 1); d.text(632, 578, 'Báo mọi người trước khi bật mic.', { size: 10 })
      d.note(608, 480, 1); d.note(188, y - 8, 3); d.note(188, y + 136, 4)
    } },
  { id: '12', name: 'Phòng họp trực tuyến', notes: ['Toàn màn, có màn chờ chọn micro, loa, camera trước khi vào.', 'Thanh giữa đáy: Micro, Camera, Chia sẻ, Cửa sổ nổi, Rời phòng.', 'Góc phải mở panel: Người dự, Nội dung họp, Điểm danh, Tài liệu, Biểu quyết, Chat.', 'Rời phòng không kết thúc cuộc họp. Chat không lưu lại.'],
    draw(d) {
      d.rect(0, 0, 900, 620); d.line(0, 44, 900, 44); d.text(14, 12, '‹  Tên cuộc họp   ○ n người trong phòng', { size: 13, b: 1 })
      ;[[14, 58], [316, 58], [14, 300], [316, 300]].forEach(([x, y], i) => { d.ph(x, y, 290, 232); d.text(x + 8, y + 208, i ? 'Tên' : 'Tên [Chủ toạ]', { size: 12 }) })
      d.rect(620, 58, 266, 474, { r: 6 }); d.text(632, 68, 'Người dự (n)', { b: 1 })
      ;[0, 1, 2, 3].forEach((i) => { d.av(632, 100 + i * 40); d.text(666, 98 + i * 40, 'Tên', { size: 12 }); d.text(666, 114 + i * 40, 'Vai trò', { size: 10 }); d.ic(860, 106 + i * 40) })
      ;[0, 1, 2, 3, 4].forEach((i) => d.rect(190 + i * 56, 556, 44, 40, { r: 8, fill: i === 4 ? 'k' : undefined }))
      ;[0, 1, 2, 3, 4, 5].forEach((i) => d.rect(620 + i * 44, 562, 28, 28, { r: 14 }))
      d.note(172, 548, 2); d.note(860, 540, 3)
    } },
  { id: '13', name: 'Kho tài liệu', notes: ['Kho riêng của từng người. Bấm thư mục để mở, không có cây thư mục.', 'Tìm trong thư mục đang mở, lọc loại tệp, vạch bộ nhớ bên phải.', 'Thư mục: Đổi tên, Chuyển đến, Xoá. Tệp thêm Tải về.', 'Nhận PDF, DOCX. Xoá không ảnh hưởng cuộc họp đang dùng tệp.'],
    draw(d) {
      shell(d, 2); d.text(190, 22, 'Kho tài liệu', { size: 20, b: 1 }); d.btn(620, 18, 116, 'Thư mục mới', { icon: 1, h: 36 }); d.btn(744, 18, 136, 'Tải tài liệu lên', { main: 1, icon: 1, h: 36 })
      d.rect(190, 70, 690, 500); d.input(200, 80, 280, '', 'Tìm trong thư mục này…'); d.input(490, 80, 120, '', 'Loại tệp', { sel: 1 }); d.rect(740, 92, 70, 6); d.text(816, 86, 'x / 10 GB', { size: 11 })
      d.table(190, 124, 690, [['Tên', 300], ['Kích thước', 100], ['Đang dùng', 110], ['Ngày tạo', 100], ['', 80]], [['▭ Tên thư mục', 'Thư mục', '', 'Ngày', '#a'], ['▭ Tên thư mục', 'Thư mục', '', 'Ngày', '#a'], ['▯ Tên-tệp.pdf', '1 MB', '2 cuộc họp', 'Ngày', '#a'], ['▯ Tên-tệp.docx', '1 MB', 'Chưa dùng', 'Ngày', '#a']], { rh: 42 })
      d.note(180, 150, 1); d.note(866, 74, 2); d.note(870, 192, 3)
    } },
  { id: '14', name: 'Người dùng & Vai trò', notes: ['Chỉ quản trị viên thấy. Vai trò có tab ma trận phân quyền, tick rồi Lưu từng dòng.', 'Bốn nút mỗi dòng: Sửa, Vô hiệu hoá, Gửi liên kết đặt lại mật khẩu, Xem kho (chỉ tài khoản đã vô hiệu hoá).', 'Nút không dùng được thì mờ, rê chuột thấy lý do.', 'Không tự khoá mình, không khoá quản trị viên cuối cùng.'],
    draw(d) {
      shell(d, -1, { admin: 1 }); d.text(190, 16, 'Người dùng & Quyền', { size: 20, b: 1 }); d.text(190, 46, 'n tài khoản, n đang hoạt động', { size: 12 }); d.btn(756, 20, 124, 'Tạo tài khoản', { main: 1, icon: 1, h: 36 })
      d.rect(190, 74, 690, 496); d.input(200, 84, 280, '', 'Tìm theo tên hoặc email…'); d.input(490, 84, 130, '', 'Vai trò', { sel: 1 }); d.input(630, 84, 130, '', 'Trạng thái', { sel: 1 })
      d.table(190, 128, 690, [['Họ tên', 240], ['Vai trò', 110], ['Đăng nhập', 110], ['Trạng thái', 110], ['', 120]], [['Tên / email', 'Chủ toạ', 'Mật khẩu', 'Hoạt động', '#4'], ['Tên / email', 'Thư ký', 'Google', 'Hoạt động', '#4'], ['Tên / email', 'Thành viên', 'Mật khẩu', 'Đã vô hiệu hoá', '#4']], { rh: 46 })
      d.note(150, 322, 1); d.note(858, 136, 2)
    } },
  { id: '15', name: 'Biên bản & ký', notes: ['Thư ký soạn từ thông tin cuộc họp và kết luận biểu quyết, rồi gửi ký.', 'Người ký: Ký hoặc Yêu cầu sửa. Sửa thì huỷ chữ ký, ký lại.', 'Đủ chữ ký thì niêm phong, hết sửa được.', 'Dừng ở biên bản: không theo dõi việc sau họp.'],
    draw(d) {
      const y = detail(d, 6, { st: '○ Đã kết thúc', actions: [['Yêu cầu sửa', 110], ['Ký biên bản', 110, 1]] })
      d.rect(190, y, 510, 420, { r: 4 }); d.text(190, y + 14, 'BIÊN BẢN CUỘC HỌP', { w: 510, align: 'c', b: 1 }); d.lines(220, y + 52, 450, 4); d.text(220, y + 116, 'Thành phần', { size: 12, b: 1 }); d.lines(220, y + 140, 450, 3); d.text(220, y + 190, 'Kết luận biểu quyết', { size: 12, b: 1 }); d.lines(220, y + 214, 450, 4)
      d.rect(710, y + 160, 170, 130, { r: 4 }); d.text(720, y + 168, 'Chữ ký · 1/2', { size: 12, b: 1 }); d.av(720, y + 194, 20); d.text(746, y + 194, 'Tên  [Chờ ký]', { size: 11 }); d.av(720, y + 224, 20); d.text(746, y + 224, 'Tên  [Đã ký]', { size: 11 }); d.btn(720, y + 252, 150, 'Niêm phong', { h: 28, off: 1 })
      d.note(872, 20, 2); d.note(700, y + 262, 3)
    } },
  { id: '16', name: 'Giọng nói của tôi', notes: ['Đồng ý riêng, rút lại được. Nút chỉ bấm được khi đã tích ô.', 'Sau khi bật, trang chuyển sang danh sách giọng mẫu (khối dưới).', 'Mỗi mẫu: Nghe lại, Xoá.', 'Ghi giọng mẫu: hộp thoại đọc một đoạn văn, nói đủ thời lượng tối thiểu. Không đạt thì Ghi lại.', 'Tắt và xoá: hỏi lại, rồi xoá ngay toàn bộ giọng mẫu và mọi gợi ý tên.', 'Giọng nói là dữ liệu sinh trắc, thuộc dữ liệu cá nhân nhạy cảm. Đặc trưng giọng chỉ lưu mã hoá. Không bật vẫn dùng được mọi chức năng khác.'],
    draw(d) {
      shell(d, 3); d.text(190, 16, 'Giọng nói của tôi', { size: 20, b: 1 }); d.lines(190, 54, 420, 1)
      d.rect(190, 70, 690, 262, { r: 4 }); d.text(204, 80, 'Bật nhận diện giọng nói', { b: 1 })
      ;['Mục đích', 'Lưu gì', 'Dùng ở đâu', 'Quyền của bạn', 'Bên kiểm soát dữ liệu'].forEach((s, i) => { d.text(204, 106 + i * 28, s, { size: 12, b: 1 }); d.line(350, 122 + i * 28, 860, 122 + i * 28, { sw: 1 }) })
      d.ic(204, 256); d.text(226, 253, 'Tôi đã đọc và đồng ý cho hệ thống lưu đặc trưng giọng nói của tôi cho mục đích trên.', { size: 12 })
      d.btn(750, 284, 116, 'Đồng ý và bật', { h: 32, off: 1 })
      d.rect(190, 346, 690, 224, { r: 4 }); d.text(204, 356, 'Giọng mẫu', { b: 1 }); d.text(286, 357, '[Đang bật]', { size: 12 }); d.text(358, 357, 'từ ngày', { size: 12 }); d.text(842, 357, '1/5', { size: 12 })
      d.rect(204, 386, 662, 34, { r: 4 }); d.text(214, 394, 'Ghi thêm một mẫu nữa để nhận diện chắc hơn.', { size: 12 })
      d.rect(204, 430, 662, 38, { r: 4 }); d.text(214, 440, 'Giọng mẫu 1', { size: 12 }); d.ic(820, 442); d.ic(844, 442)
      d.btn(204, 526, 150, 'Tắt và xoá giọng mẫu'); d.btn(736, 526, 130, 'Ghi giọng mẫu', { main: 1, icon: 1 })
      d.note(732, 290, 1); d.note(868, 336, 2); d.note(796, 442, 3); d.note(712, 532, 4); d.note(360, 532, 5)
    } },
  { id: '17', name: 'Ma trận phân quyền', notes: ['Bấm ô để bật, tắt quyền. Ô vừa đổi được đánh dấu.', 'Cột Thao tác hiện Hoàn tác, Lưu cho đúng dòng đó. Mỗi dòng lưu riêng.', 'Bỏ quyền Quản trị người dùng của vai trò quản trị cuối cùng thì bị chặn, báo lỗi trên ô.', 'Rê chuột lên tên cột để xem mô tả quyền.'],
    draw(d) {
      shell(d, -1, { admin: 2 }); d.text(190, 22, 'Vai trò', { size: 20, b: 1 }); d.btn(770, 18, 110, 'Tạo vai trò', { main: 1, icon: 1, h: 36 })
      d.text(190, 72, 'Danh sách vai trò', { w: 130, align: 'c', size: 12 }); d.text(320, 72, 'Ma trận phân quyền', { w: 140, align: 'c', size: 12, b: 1 }); d.rect(320, 92, 140, 3, { fill: 'k' }); d.line(190, 95, 880, 95)
      const xs = [320, 425, 530, 635], top = 110, rows = [['Chủ toạ', [1, 1, 0, 0]], ['Thư ký', [1, 1, 1, 0]], ['Thành viên', [0, 0, 0, 0]], ['Quản trị viên', [0, 0, 0, 1]], ['Kiểm soát viên', [0, 0, 0, 0]]]
      d.rect(190, top, 690, 60 + rows.length * 44); d.line(190, top + 60, 880, top + 60)
      d.text(200, top + 22, 'Vai trò', { size: 12, b: 1 }); d.text(750, top + 22, 'Thao tác', { size: 12, b: 1 })
      ;[[320, 210, 'Cuộc họp'], [530, 105, 'Biên bản'], [635, 105, 'Quản trị']].forEach(([x, w, s]) => { d.text(x, top + 7, s, { w, align: 'c', size: 12, b: 1 }); d.line(x, top, x, top + 60 + rows.length * 44) })
      d.line(740, top, 740, top + 60 + rows.length * 44); d.line(320, top + 30, 740, top + 30)
      ;['Tạo cuộc họp', 'Phê duyệt cuộc họp', 'Soạn biên bản', 'Quản trị người dùng'].forEach((s, i) => d.text(xs[i], top + 38, s, { w: 105, align: 'c', size: 10 }))
      rows.forEach(([s, caps], r) => {
        const y = top + 60 + r * 44
        if (r) d.line(190, y, 880, y, { sw: 1 })
        d.text(200, y + 13, s, { size: 12 })
        caps.forEach((v, c) => d.rect(xs[c] + 45, y + 15, 14, 14, { r: 2, fill: v ? 'k' : undefined }))
      })
      d.rect(430, top + 110, 95, 38, { dash: 1, sw: 2 })
      d.btn(746, top + 112, 72, 'Hoàn tác', { h: 28 }); d.btn(824, top + 112, 50, 'Lưu', { h: 28, main: 1 })
      d.note(516, top + 100, 1); d.note(866, top + 100, 2); d.note(726, top + 240, 3); d.note(716, top + 5, 4)
    } },
  { id: '18', name: 'Panel thông báo', notes: ['Mở từ chuông ở chân sidebar. Panel neo ở đáy, ngay bên phải sidebar.', 'Bấm một thông báo: đánh dấu đã đọc và mở đúng cuộc họp.', 'Chấm đen là chưa đọc. Số trên chuông cập nhật tức thì.'],
    draw(d) {
      const y = detail(d, 0, { st: '○ Đã công bố' }); d.rect(190, y, 510, 330, { r: 4 }); d.lines(204, y + 20, 470, 4)
      d.rect(178, 288, 400, 324, { r: 6, fill: 'w', sw: 2 }); d.text(192, 300, 'Thông báo', { size: 15, b: 1 }); d.rect(284, 300, 48, 20, { r: 10, fill: 'k' }); d.text(284, 303, '2 mới', { w: 48, align: 'c', size: 11, c: 'w' })
      d.ic(404, 303); d.text(424, 301, 'Đánh dấu tất cả đã đọc', { size: 12 }); d.line(178, 332, 578, 332)
      ;['Cuộc họp đã được công bố', 'Cuộc họp đã được duyệt', 'Cuộc họp bị từ chối'].forEach((s, i) => {
        const ry = 332 + i * 92
        if (i) d.line(178, ry, 578, ry, { sw: 1 })
        d.rect(192, ry + 14, 22, 22, { r: 2 }); d.text(226, ry + 10, s, { size: 13, b: i < 2 }); d.lines(226, ry + 42, 300, 2); d.text(226, ry + 66, 'Thời gian', { size: 11 })
        if (i < 2) d.rect(554, ry + 16, 10, 10, { r: 5, fill: 'k' })
      })
      d.note(150, 568, 1); d.note(530, 358, 2); d.note(566, 382, 3)
    } },
  { id: '19', name: 'Phòng chờ', notes: ['Camera mặc định tắt, micro mặc định bật. Trình duyệt nhớ lựa chọn cho lần sau.', 'Ba nút chọn Micro, Loa, Camera mở danh sách thiết bị. Chưa cấp quyền thì nút báo lý do và bị khoá.', 'Nhãn mức mật chỉ hiện với cuộc họp Hạn chế, Mật.', 'Ngoài giờ họp, đã kết thúc hoặc họp trực tiếp: màn này chỉ còn một câu báo và nút Về trang cuộc họp.'],
    draw(d) {
      d.rect(0, 0, 900, 620); d.rect(14, 12, 28, 28, { r: 14 }); d.text(50, 17, 'TrustCabinet', { b: 1 }); d.text(760, 10, 'Tên người dùng', { size: 12 }); d.text(760, 26, 'email', { size: 10 }); d.av(860, 14)
      d.rect(50, 90, 500, 310, { r: 8 }); d.text(50, 234, 'Camera đang tắt', { w: 500, align: 'c' }); d.rect(64, 354, 32, 32, { r: 16 }); d.text(64, 359, '⋯', { w: 32, align: 'c' })
      d.rect(250, 344, 44, 44, { r: 22 }); d.rect(306, 344, 44, 44, { r: 22, fill: 'k' })
      ;['Micro', 'Loa', 'Camera'].forEach((s, i) => { const x = 50 + i * 170; d.rect(x, 416, 160, 36, { r: 18 }); d.ic(x + 12, 427); d.text(x + 32, 425, s, { size: 12 }); d.text(x + 138, 424, '⌄') })
      d.text(610, 140, 'Sẵn sàng tham gia?', { size: 20, b: 1 }); d.text(610, 180, 'Tên cuộc họp', { b: 1 }); d.rect(610, 206, 54, 24, { r: 4 }); d.text(610, 210, 'Mật', { w: 54, align: 'c', size: 12 }); d.text(610, 242, 'n người trong phòng', { size: 12 })
      d.ic(610, 270, 12); d.text(628, 266, 'Cuộc họp được ghi âm để lập biên bản', { size: 11 }); d.text(628, 282, 'tự xoá sau 180 ngày', { size: 11 })
      d.btn(610, 316, 240, 'Vào phòng', { main: 1, h: 44 }); d.text(610, 374, '‹ Quay lại trang cuộc họp', { w: 240, align: 'c', size: 12 })
      d.note(358, 336, 1); d.note(556, 424, 2); d.note(670, 208, 3)
    } },
  { id: '20', name: 'Bản gỡ băng', notes: ['Người nói N là giọng chưa gán. Thư ký chọn tên ở ô trên hoặc ngay tại dòng; chọn một lần là đặt tên cho cả giọng đó.', 'Ô chọn đưa tên Khớp giọng nhất lên đầu. Máy chỉ gợi ý, Thư ký quyết định.', 'Bấm mốc giờ hoặc nút phát để nghe lại đoạn đó.', 'Bấm vào chữ để sửa: Enter lưu, Esc huỷ. Đoạn đã sửa có nhãn đã sửa.', 'Hai nút: Tải bản gỡ băng (.txt), Chuyển thành chữ lại (thay chữ đã sửa và tên đã gán).'],
    draw(d) {
      const y = detail(d, 5, { st: '○ Đã kết thúc', rail: false })
      d.rect(190, y, 690, 46, { r: 4 }); d.rect(202, y + 12, 20, 22); d.text(232, y + 6, 'ten-ban-ghi.wav', { size: 12, b: 1 }); d.text(232, y + 24, '[Có bản gỡ băng]  thời lượng, dung lượng, giờ', { size: 10 }); [0, 1, 2].forEach((n) => d.ic(816 + n * 20, y + 16))
      d.rect(190, y + 54, 690, 32, { r: 4 }); d.text(202, y + 60, '0:00 / 1:09  ———●———  ‹5  ▶  5›  1×', { size: 11 })
      d.rect(190, y + 96, 90, 24, { r: 4 }); d.text(190, y + 100, 'Máy nhận dạng', { w: 90, align: 'c', size: 10 }); d.text(290, y + 100, 'Chữ do máy tạo, có thể sai — bấm vào mốc giờ để nghe lại đoạn đó.', { size: 11 }); d.ic(840, y + 101); d.ic(864, y + 101)
      ;['Người nói 1', 'Tên', 'Người nói 3', 'Tên'].forEach((s, i) => { const x = 190 + i * 174; d.rect(x, y + 130, 168, 32, { r: 4 }); d.rect(x + 8, y + 141, 10, 10, { r: 5 }); d.text(x + 24, y + 137, s + ' ⌄', { size: 12 }); d.text(x + 128, y + 138, 'mm:ss', { size: 10 }) })
      d.line(262, y + 180, 262, 610)
      const run = (top, name, cnt) => { d.text(196, top + 3, 'hh:mm:ss', { size: 10 }); d.rect(257, top + 5, 10, 10, { r: 5, fill: 'k' }); d.text(280, top, name + ' ⌄', { size: 12, b: 1 }); d.ic(372, top + 2); for (let i = 0; i < cnt; i++) { if (i) d.text(196, top + 8 + i * 22, 'hh:mm:ss', { size: 10 }); d.line(280, top + 32 + i * 22, 820, top + 32 + i * 22, { sw: 1 }) } }
      run(y + 178, 'Tên', 2)
      run(y + 244, 'Người nói 3', 0); d.rect(280, y + 268, 590, 54, { r: 4, sw: 2 }); d.lines(290, y + 284, 560, 2); d.text(280, y + 330, 'Enter để lưu · Esc để huỷ', { size: 11 }); d.btn(760, y + 328, 52, 'Huỷ', { h: 26 }); d.btn(818, y + 328, 52, 'Lưu', { h: 26, main: 1 })
      run(y + 372, 'Tên', 2); d.text(826, y + 396, 'đã sửa', { size: 10 }); d.ic(866, y + 396)
      d.note(530, y + 122, 1); d.note(396, y + 242, 2); d.note(392, y + 176, 3); d.note(734, y + 331, 4); d.note(816, y + 98, 5)
    } },
]

if (typeof module !== 'undefined') module.exports = { SHEET, SCREENS, makeDraw }

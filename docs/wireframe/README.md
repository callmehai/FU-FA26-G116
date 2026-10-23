# Wireframe màn hình

Bản thiết kế các màn TrustCabinet để dựng trong Figma và lấy ảnh cho report. Đây không phải
frontend chạy thật: không gọi API và không lưu dữ liệu.

Prototype HTML luồng cuộc họp nằm riêng ở [assets/wireframe](../assets/wireframe/README.md).

Một phiên bản mới (ví dụ v5 → v6) nộp đủ ba phần:

1. **File HTML** `TrustCabinet-wireframe-v<N>.html` — bản thiết kế gốc, mọi thay đổi bắt đầu ở đây.
2. **Plugin Figma** `figma-v<N>/` (`manifest.json` + `code.js`) — dựng lại các màn đã đổi trong file Figma mà không cần Figma MCP.
3. **Ảnh màn hình** `screens/screen-<nhóm>_<số>.png` — ảnh PNG từng màn của phiên bản mới nhất, dùng trong report.

Chỉ sửa HTML bằng tay. Plugin và ảnh luôn sinh ra từ HTML bằng script trong `_tools/`. Giữ file HTML và thư mục `figma-v<N>` của bản cũ.

## Bản hiện có — v7

| Thành phần | Đường dẫn | Vai trò |
|---|---|---|
| HTML | [TrustCabinet-wireframe-v7.html](TrustCabinet-wireframe-v7.html) | Bản thiết kế gốc. |
| Plugin Figma | [figma-v7/](figma-v7/) (`manifest.json`, `code.js`) | Dựng lại màn trong Figma Desktop. |
| Ảnh | [screens/](screens/) | PNG từng màn của v7. |
| Tool | [_tools/](_tools/) | Sinh plugin và ảnh từ HTML. |

## 1. Thư mục

```
docs/wireframe/
  TrustCabinet-wireframe-v5.html …              mỗi phiên bản một file, không sửa bản cũ
  figma-v5/ …                                   plugin Figma của từng phiên bản
    manifest.json                               khai báo plugin cho Figma Desktop
    code.js                                     dữ liệu các màn (LAYOUT) + code dựng màn
  screens/                                      ảnh PNG của phiên bản mới nhất
  _tools/                                       script sinh plugin và ảnh
    run.mjs                                     mở HTML bằng Chrome headless: trích layout / chụp ảnh
    extract.js                                  chạy trong trang: đọc DOM → cây layout cho Figma
    mk_plugin.js                                tạo figma-v<mới>/ từ plugin bản trước
    mock.js                                     chạy thử plugin trên Figma giả lập
    out/v5.json …                               layout đã trích của từng phiên bản (dùng để so sánh)
```

## 2. Chuẩn bị

| Cần | Ghi chú |
|---|---|
| Node.js ≥ 22 | Script dùng `fetch` và `WebSocket` có sẵn của Node 22. Kiểm tra: `node -v` |
| Google Chrome (hoặc Edge) | Script tự tìm ở đường dẫn cài mặc định. Cài chỗ khác thì đặt biến môi trường `CHROME` trỏ tới file `.exe` |
| Kết nối mạng | Font Be Vietnam Pro tải từ Google Fonts. Không có mạng thì chữ đo sai, Figma sẽ lệch |
| Figma Desktop | Plugin dạng *development* chỉ import được trên bản Desktop, không chạy trên trình duyệt |

Mọi lệnh bên dưới chạy trong thư mục `_tools`:

```powershell
cd docs/wireframe/_tools
```

## 3. Quy trình tóm tắt

Ví dụ nâng từ v5 lên v6:

| # | Việc | Lệnh / thao tác |
|---|---|---|
| 1 | Sao HTML bản cũ thành bản mới, đổi số phiên bản | copy file, sửa 4 chỗ (mục 4) |
| 2 | Sửa / thêm màn trong HTML | sửa tay (mục 5) |
| 3 | Xem trước trong Chrome | mở file, bấm **Kích thước thật** |
| 4 | Trích layout bản mới | `node run.mjs extract ..\TrustCabinet-wireframe-v6.html out\v6.json` |
| 5 | Tạo `figma-v6/` | `node mk_plugin.js 5 6 "(dd/mm/yyyy) nội dung thay đổi" FE-xx` |
| 6 | Chạy thử plugin | `node mock.js ..\figma-v6\code.js` |
| 7 | Chụp lại ảnh vào `screens/` | lệnh `shots` do bước 5 in ra |
| 8 | Đưa vào Figma | Import plugin từ `figma-v6/manifest.json` rồi chạy (mục 9) |

---

## 4. Bước 1 — Tạo file HTML phiên bản mới

1. Sao `TrustCabinet-wireframe-v5.html` thành `TrustCabinet-wireframe-v6.html`. **Không sửa file bản cũ**: nó là
   mốc để script tìm ra màn nào đã đổi.
2. Trong file mới, sửa 4 chỗ ở đầu file:
   - `<title>Wireframe TrustCabinet v6</title>`
   - `<h1>Wireframe TrustCabinet v6</h1>`
   - Trong `<header class="intro">`, thêm một đoạn `<p>` cho v6: ngày, nguồn thay đổi (UC, ADR, note bàn giao),
     danh sách màn đổi và màn mới. Giữ nguyên các đoạn của bản cũ để có lịch sử.
   - Trong khối `<div class="howto">`, đổi `figma-v5/manifest.json` thành `figma-v6/manifest.json`, và sửa câu
     mô tả plugin dựng lại những màn nào.

## 5. Bước 2 — Sửa màn hình trong HTML

### 5.1 Cấu trúc file

| Phần | Nội dung |
|---|---|
| `<style>` → `:root` | Token màu (`--brand`, `--ink`, `--mute`, `--line`, `--danger`…) và font |
| `<style>` sau dòng `/* ---------- Wireframe primitives` | Class vẽ màn (`.b`, `.chip`, `.card`, `.in`…). CSS thêm ở mỗi bản đặt trong khối có chú thích `/* v6: … */` |
| `<script>` đầu | Builder: hàm nhỏ trả về chuỗi HTML (`B`, `C`, `F`, `T`, `DL`, `SH`, `SPR`…) |
| Dữ liệu mẫu | `M`, `MS` (cuộc họp mẫu), `WHO` (người mẫu), `DTABS` (tab trang chi tiết), `VOTE`, `ROLES`… |
| `const LUONG = [...]` | Luồng cuộc họp S01–S17. Mỗi bước có `frames`, mỗi frame có `fig: 'x.y'`: số màn trong Figma |
| `const GROUPS = [...]` | 15 nhóm FE-01 → FE-15. Mỗi phần tử trong `screens` là **một màn** (một frame trong Figma, một file PNG) |
| Cuối script | Render bảng màn, nút **Kích thước thật** |

Một màn được khai báo theo một trong hai cách:

- **Viết thẳng trong `GROUPS`:** `{ no: '11.1', name: '…', ucs: '…', html: …, notes: [...] }`.
  Tìm bằng chuỗi `no: '11.1'`.
- **Lấy từ `LUONG`:** `FROM('11.2', 'Tên màn', { ucs, notes })`, còn HTML nằm ở frame `{ fig: '11.2', html: … }`
  trong `LUONG`. Tìm bằng chuỗi `fig: '11.2'`. Sửa giao diện thì sửa trong `LUONG`; sửa tên, UC, ghi chú thì sửa
  trong `FROM(...)`.

### 5.2 Các trường của một màn

| Trường | Ý nghĩa |
|---|---|
| `no` | Số màn: `'11.1'`, `'11.1b'`. Dùng làm tên frame Figma và tên ảnh (`screen-11_1b.png`) |
| `name` | Tên màn, hiện ở chú thích và trong tên frame Figma (`11.1 Tab Biểu quyết – đang họp`) |
| `ucs` | Các UC màn phục vụ, ví dụ `'UC-52a · UC-53'` |
| `html` | Nội dung màn, vẽ ở tỉ lệ 1:1 trong khung 1280×800 |
| `notes` | Mảng ghi chú hành vi. Mã thông báo (MSGxxx) ghi ở đây, không đưa vào giao diện |
| `phone: 1` | Màn điện thoại 390×780. Với màn lấy từ `LUONG`, đặt `phone: 1` trong frame của `LUONG` |
| `built: 1` | Đánh dấu màn đã có trong app (chỉ là thông tin) |

### 5.3 Builder hay dùng

| Builder | Tạo ra | Ví dụ |
|---|---|---|
| `B(text, kiểu, icon)` | Nút. Kiểu: `p` (chính), `d` (đỏ), `gh` (không viền), `lg` (cao 40), `sq` (vuông) | `B('Lưu nháp')`, `B('Tạo biểu quyết', 'p', true)` |
| `OFF(text)` | Nút chính bị mờ (disabled) | `OFF('Mở biểu quyết')` |
| `C(text, kiểu)` | Chip. Kiểu: `brand`, `soft`… | `C('Chủ trì', 'brand')` |
| `S(k)` / `TS(text, tone)` | Chip trạng thái cuộc họp (`draft`, `pub`, `live`, `end`…) / chip trạng thái tuỳ ý (`green`, `amber`, `red`, `slate`, `live`, `ended`, `violet`) | `S('live')`, `TS('Đã đóng', 'ended')` |
| `L(k)` | Chip mức độ: `n` Thường, `i` Nội bộ, `c` Hạn chế, `s` Mật | `L('s')` |
| `P(tên, phụ)` / `A(tên)` | Người (avatar + tên + dòng phụ) / chỉ avatar | `P('Trần Quốc Bảo', 'Chủ trì')` |
| `F(nhãn, giá trị, o)` | Ô nhập. `o`: `req`, `ph` (placeholder), `area`, `sel` (dropdown), `hint`, `err`, `span` | `F('Ghi chú', '…', { hint: 'Không bắt buộc.' })` |
| `T(cột, dòng)` | Bảng | `T(['Tên', 'Vai trò'], [[P('…'), 'Thư ký']])` |
| `TABS(list, on)` / `SEG(list, on)` | Thanh tab / nút chọn liền khối | `SEG(['Công khai', 'Bỏ phiếu kín'], 'Công khai')` |
| `DL(tiêu đề, thân, nút, rộng)` | Hộp thoại phủ màn (truyền vào tham số `over` của `SH`/`SPR`) | `DL('Đóng biểu quyết?', …, B('Huỷ','gh') + B('Đóng phiếu','p'), 500)` |
| `KV([[k, v], …])` | Bảng hai cột nhãn / giá trị | |
| `HEAD(tiêu đề, phụ, nút)` | Đầu trang danh sách | |
| `SH(mục sidebar, thân, over, who)` | Khung app: sidebar + vùng chính | `SH('meet', …)` |
| `DETAIL(tab, thân, over, o)` | Trang chi tiết cuộc họp "Họp HĐQT quý IV" | |
| `SPR(tab, thân, over, o)` | Trang chi tiết cuộc họp mẫu "Họp kế hoạch Sprint 6". `o`: `st` (trạng thái), `lv`, `meta` (2 dòng phụ), `who` (người đăng nhập), `actions` (nút góc phải) | `SPR('Biểu quyết', body, '', { who: WHO.chair, st: 'live' })` |
| `ROOM(sân khấu, panel, o)` / `TILE(...)` | Phòng họp trực tuyến (FE-08) | |
| `X(cao, rộng)` | Ô gạch chéo: ảnh, video, vùng nội dung không cần vẽ | `X(300)` |
| `LN(...%)` | Các dòng chữ giả | `LN(60, 80, 40)` |
| `I(k)` | Icon giả (ô vuông); `I('lg')` to hơn, `I('r')` tròn | |
| `TURN(n, tổng)`, `RCHIPS(vai trò)` | Riêng FE-11: thanh "Đã bỏ phiếu n/tổng", dòng chip vai trò được bỏ phiếu | |

Class bố cục dùng thẳng trong HTML: `row` (ngang, căn giữa), `col` (dọc), `grow` (giãn hết chỗ), `wrap`,
`split` (hai cột), `card` / `card pad` / `panel`, `sub` (chữ phụ xám), `ttl` (tiêu đề), `eb` (nhãn nhỏ in hoa),
`num` (số), `alert info|warn|err|ok`, `bar` (thanh tiến độ, con `<i style="width:60%">`), `radio on`,
`check on`, `pick on`, `drag`. Màu lấy từ biến `var(--…)`, không gõ mã màu mới nếu biến đã có.

### 5.4 Thêm màn mới

- **Đánh số:** biến thể của một màn có sẵn thì thêm hậu tố chữ (`11.1b`, `7.4b`); màn mới hẳn lấy số tiếp
  theo trong nhóm (`11.5`). **Không đánh số lại màn đã có**: Figma, tên ảnh và tài liệu đều tham chiếu theo
  số. Plugin chỉ nhận dạng `số.số` hoặc `số.số` + một chữ thường.
- **Vị trí:** đặt màn vào đúng chỗ trong mảng `screens` của nhóm. Thứ tự trong mảng là thứ tự frame trong
  section Figma.
- **Màn điện thoại:** thêm `phone: 1`. Khung 390×780, nội dung thường là `<div class="ph">…</div>`.
- **Bỏ một màn:** xoá khỏi mảng. Plugin sẽ xoá frame tương ứng trong Figma; ảnh trong `screens/` phải xoá tay.

### 5.5 Quy tắc khi sửa

- **Chỉ sửa màn cần đổi.** Sửa builder dùng chung (`SB`, `DH`, `DETAIL`, `SPR`, `B`…), dữ liệu mẫu dùng chung
  (`M`, `MS`, `WHO`…) hoặc CSS primitive sẽ làm đổi hàng chục màn. Plugin sẽ dựng lại hết các màn đó và mọi
  chỗ chỉnh tay trong Figma ở các màn ấy bị mất. Cần biến thể thì viết builder mới (như `OFF`, `TURN`) hoặc
  dùng `style="…"` tại chỗ.
- **Câu chữ bằng tiếng Việt**, dùng đúng thuật ngữ của đặc tả UC. Nút, nhãn, thông báo viết như trong app thật.
- **Nội dung phải nằm gọn trong khung** 1280×800 (điện thoại 390×780). Phần tràn bị cắt cả trên HTML lẫn Figma.
- **Chỉ vẽ bằng HTML/CSS mà bộ trích hiểu**, vì đó là thứ sang được Figma:
  - Hiểu: màu nền, `linear-gradient`, viền (cạnh khác màu thành một dải riêng), bo góc, `box-shadow`,
    `opacity`, `overflow: hidden`, `clip-path: polygon`, xoay bằng `transform`, chấm `radial-gradient` (như
    `.drag`), ô `.x`, `::before` / `::after`, chữ (đậm, nghiêng, gạch, in hoa, giãn chữ).
  - Không hiểu (Figma sẽ thiếu): `<img>`, `<svg>`, `background-image: url(...)`, `repeating-linear-gradient`,
    `filter`, `text-shadow`, font khác Be Vietnam Pro / JetBrains Mono.

### 5.6 Xem trước

Mở file HTML bằng Chrome, bấm **Kích thước thật** để mỗi màn hiện đúng 1280×800. Các lỗi hay gặp:

- chữ xuống dòng ngoài ý muốn: rút ngắn câu hoặc giảm độ rộng cột bên cạnh;
- nút hoặc thanh `SEG` bị kéo dài hết chiều ngang: thêm `align-items:flex-start` cho khối `.fld` chứa nó;
- nội dung tràn khỏi khung: bớt nội dung hoặc tách thành màn biến thể (`b`).

## 6. Bước 3 — Trích layout

```powershell
node run.mjs extract ..\TrustCabinet-wireframe-v6.html out\v6.json
```

Kết quả mong đợi: `Đã trích 58 màn → out\v6.json` (số màn của bản mới).

Script mở HTML bằng Chrome headless, bật **Kích thước thật**, chờ font tải xong rồi chạy `extract.js` trên
từng màn. `extract.js` đi qua DOM và chỉ giữ những phần tử nhìn thấy được (có nền, viền, bóng, bo góc…) làm
khung. `div` chỉ để bố cục bị gộp vào khung cha, nhờ vậy Figma ít layer.

`out\v5.json` của bản trước phải có sẵn (hiện có `out\v4.json`, `out\v5.json` và `out\v6.json`). Nếu đã sửa `extract.js`,
trích lại cả bản cũ để hai bản so được với nhau:

```powershell
node run.mjs extract ..\TrustCabinet-wireframe-v5.html out\v5.json
```

## 7. Bước 4 — Tạo `figma-v6/` (manifest.json + code.js)

```powershell
node mk_plugin.js 5 6 "(16/10/2026) cập nhật FE-xx theo UC-…: …" FE-xx
```

| Tham số | Ý nghĩa |
|---|---|
| `5` | Bản cũ: đọc `..\figma-v5\code.js` và `out\v5.json` |
| `6` | Bản mới: đọc `out\v6.json`, ghi `..\figma-v6\` |
| `"…"` | Ghi chú đặt ở dòng đầu `code.js`: ngày và tóm tắt thay đổi |
| `FE-xx` | (không bắt buộc) section Figma tự cuộn tới sau khi chạy plugin |

Script làm những việc sau:

1. So từng màn trong `out\v6.json` với `out\v5.json`. Lệch nhỏ hơn 0,05px do làm tròn được bỏ qua.
2. Màn **khác** hoặc **mới** được đánh dấu `rb: 1` và dùng dữ liệu vừa trích.
3. Màn **không đổi** được đánh dấu `rb: 0` và giữ nguyên dữ liệu của `figma-v5/code.js`.
4. Ghi `figma-v6/code.js`: dòng ghi chú, `const LAYOUT = …`, và phần code dựng màn chép nguyên từ bản cũ.
5. Ghi `figma-v6/manifest.json`, chép từ bản cũ và chỉ đổi `name` / `id`.
6. In danh sách **Màn sửa**, **Màn mới** và lệnh chụp ảnh cho bước 7.

**Đối chiếu danh sách "Màn sửa"** với những gì bạn định đổi. Có màn lạ nghĩa là bạn đã đụng vào thứ dùng chung
(mục 5.5): sửa lại HTML rồi chạy lại từ bước 3.

`manifest.json` có dạng sau. Không cần sửa tay; chỉ khi phải tự tạo thì chép đúng mẫu này và đổi `name`, `id`:

```json
{
  "name": "figma v6 – TrustCabinet",
  "id": "trustcabinet-figma-v6",
  "api": "1.0.0",
  "main": "code.js",
  "editorType": ["figma"],
  "documentAccess": "dynamic-page",
  "networkAccess": { "allowedDomains": ["none"] }
}
```

| Trường | Ý nghĩa |
|---|---|
| `name` | Tên hiện trong menu Plugins → Development |
| `id` | Mã plugin. Mỗi phiên bản một mã để các bản không đè nhau |
| `main` | File code chạy, nằm cùng thư mục |
| `editorType` | Chỉ chạy trong Figma Design (không phải FigJam) |
| `documentAccess: "dynamic-page"` | Bắt buộc với plugin mới; code đã dùng `setCurrentPageAsync` cho phù hợp |
| `networkAccess: none` | Plugin không gọi mạng; mọi dữ liệu nằm sẵn trong `code.js` |

### Chạy thử plugin

```powershell
node mock.js ..\figma-v6\code.js
```

Lệnh chạy plugin trên một Figma giả lập hai lần. Kết quả mong đợi:

```
Trang trống : figma v6: dựng lại 58 màn, giữ 0 màn, … (… layer).
Chạy lần 2  : figma v6: dựng lại <số màn rb:1> màn, giữ <số còn lại> màn, …
```

Dòng bắt đầu bằng `Lỗi:` nghĩa là `code.js` hỏng: xem lại bước 4–5.

## 8. Bước 5 — Cập nhật thư mục `screens/`

Chạy lệnh `shots` mà `mk_plugin.js` vừa in ra, ví dụ:

```powershell
node run.mjs shots ..\TrustCabinet-wireframe-v6.html ..\screens 8.5,11.1,11.1b
```

- Tên file: `screen-<nhóm>_<số>.png` (`11.1b` → `screen-11_1b.png`). Ảnh màn sửa bị ghi đè, ảnh màn mới được thêm.
- Kích thước: màn desktop 1920×1200, màn điện thoại 585×1170 (tỉ lệ 1,5 như các ảnh đã có).
- Màn bị bỏ khỏi HTML thì xoá tay ảnh của nó.
- Mở vài ảnh vừa chụp để soát lần cuối. Ảnh bản cũ không cần giữ: chạy `shots` trên HTML bản cũ là tạo lại được.
- Muốn chụp vào thư mục khác để xem trước thì thay `..\screens` bằng thư mục khác.

## 9. Bước 6 — Đưa vào Figma

1. Mở **Figma Desktop**, mở file wireframe. Plugin làm việc trên trang tên **Wireframe**; không có trang đó
   thì nó dùng trang đang mở.
2. Menu **Plugins → Development → Import plugin from manifest…**, chọn
   `docs/wireframe/figma-v6/manifest.json`. Mỗi phiên bản chỉ cần import một lần.
3. Chạy **Plugins → Development → figma v6 – TrustCabinet**.
4. Khi xong, Figma báo một dòng tổng kết: `figma v6: dựng lại … màn, giữ … màn, … xoá … màn cũ …` và cuộn tới
   section đã chọn ở tham số `FE-xx`.

Plugin xử lý như sau:

- Mỗi nhóm FE-xx là một section; mỗi màn là một frame tên `<số> <tên màn>`.
- Màn `rb: 1` bị xoá và dựng lại từ dữ liệu mới, nên **mọi chỉnh tay trong frame đó sẽ mất**. Muốn giữ phần
  chỉnh tay thì nhân bản frame ra ngoài section trước khi chạy.
- Màn `rb: 0` đã có trong Figma được giữ nguyên, chỉ xếp lại vị trí. Màn chưa có thì được dựng mới.
- Frame của màn đã bỏ khỏi HTML bị xoá.
- Chạy được trên file đang ở bất kỳ bản cũ nào (v2 → v5), kể cả file trống.
- Chạy sai thì bấm **Ctrl+Z** để hoàn tác.

Font cần có trong Figma: **Be Vietnam Pro** (Regular, Medium, SemiBold, Bold, ExtraBold, các bản Italic) và
**JetBrains Mono**. Cả hai là Google Fonts nên Figma có sẵn.

---

## 10. Tham khảo: định dạng `LAYOUT` trong `code.js`

```
LAYOUT = [ { code: 'FE-11', name: 'FE-11 · Biểu quyết',
             screens: [ { no: '11.1', w: 1280, h: 800, rb: 1, root: <node> }, … ] }, … ]
```

| Node khung (`t: 'F'`) | | Node chữ (`t: 'T'`) | |
|---|---|---|---|
| `n` | tên (class CSS của phần tử) | `s` | nội dung |
| `x, y, w, h` | vị trí so với khung cha, kích thước | `x, y, w` | vị trí, độ rộng |
| `fl` | nền: `{c, a}` màu hoặc `{g:1, T, st}` gradient | `f` | `v` = Be Vietnam Pro, `m` = JetBrains Mono |
| `st` | viền `{c, a, w:[trên, phải, dưới, trái], d}` (`d` = nét đứt) | `wt, sz, lh` | độ đậm, cỡ chữ, chiều cao dòng |
| `r` | bo góc `[tl, tr, br, bl]` | `c, a` | màu, độ trong |
| `fx` | bóng `{i, x, y, b, s, c, a}` (`i` = bóng trong) | `al` | căn `C` giữa / `R` phải |
| `op` | opacity | `wr` | tự xuống dòng trong độ rộng `w` |
| `cl` | cắt phần tràn | `split` | các đoạn dòng có vị trí riêng |
| `poly` | đa giác (ngôi sao logo) | `ls, it, d` | giãn chữ, nghiêng, gạch (`S` ngang / `U` dưới) |
| `rt` | ma trận xoay (thay cho `x, y`) | | |
| `xx` | vẽ hai đường chéo (ô `.x`) | | |
| `ch` | các node con | | |

## 11. Lỗi thường gặp

| Hiện tượng | Cách xử lý |
|---|---|
| "Màn sửa" có màn không định đổi | Đã sửa builder, dữ liệu mẫu hoặc CSS dùng chung (mục 5.5). Hoàn lại rồi làm bằng builder hoặc style riêng |
| Chữ trong Figma lệch, xuống dòng khác HTML | Lúc trích không có mạng nên font không tải được. Có mạng rồi chạy lại bước 3 → 5 |
| `run.mjs` treo hoặc báo lỗi kết nối | Cổng 9333 bị một Chrome headless cũ chiếm. Tắt nó (`taskkill /IM chrome.exe /F` — tắt luôn Chrome đang mở) rồi chạy lại |
| Không tìm thấy Chrome | `$env:CHROME = "đường\dẫn\chrome.exe"` (PowerShell) rồi chạy lại |
| Plugin báo `Lỗi: thiếu dữ liệu màn …` | `code.js` thiếu `root` của một màn. Kiểm tra `out\v<N>.json` có màn đó, rồi chạy lại `mk_plugin.js` |
| Plugin báo lỗi font | Figma thiếu một kiểu chữ trong danh sách ở mục 9; bật font đó rồi chạy lại |
| `Không thấy màn x.y` khi chụp | Số màn gõ sai, hoặc màn chưa có trong HTML bản đang chụp |

## 12. Lịch sử phiên bản

| Bản | Ngày | Thay đổi chính |
|---|---|---|
| v4 | 14/10/2026 | ADR-0018: tài liệu không phiên bản, kho cá nhân có thư mục; chọn vai trò bỏ phiếu. Dựng lại 1.3, 5.3, 7.x, 11.1, 11.2, 11.4, 14.2 |
| v5 | 15/10/2026 | FE-11 theo UC-52a–UC-57 và ADR-0019. Sửa 8.5, 11.1–11.4, 14.2; thêm 11.1b, 11.2b, 11.3b, 11.5 |
| v6 | 19/10/2026 | SCRUM-47: lịch tháng và bộ lọc lịch theo trạng thái, mức độ (luồng phụ UC-17), tham khảo AI-eCabinet. Sửa 4.1, 4.2 (nút Bộ lọc, tiêu đề mở bảng chọn ngày, UC-17); thêm 4.1b, 4.1c, 4.1d |

| v7 | 22/10/2026 | SCRUM-21/22/23: tài liệu cuộc họp theo UC-32a, UC-32c, UC-35 (sửa 22/10/2026), tham khảo AI-eCabinet. Sửa 7.3 (nhóm theo mục / biểu quyết / chung, menu gỡ khỏi mục hoặc cuộc họp), 7.5 (trả lời góp ý, khách mời góp ý); thêm 8.6 (panel Tài liệu trong phòng họp). Vẽ lại phòng họp FE-08 (8.1–8.5) cho khớp bản đã chạy trên server test 22/10/2026; số UC mục 8 sửa theo danh mục UC hiện hành (UC-37–UC-42) |

Ra bản mới thì thêm một dòng vào bảng này.

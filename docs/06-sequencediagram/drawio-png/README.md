# Ảnh sequence từ Draw.io

Thư mục này giữ một ảnh `.drawio.png` cho mỗi tab Use Case trong
[`TrustCabinet_Sequence_Diagrams.drawio`](../TrustCabinet_Sequence_Diagrams.drawio).

## Cách xuất khuyến nghị (từ `.txt`)

Khi sửa source SequenceDiagram.org, dùng một lệnh — dựng tab theo style agenda (UC-23a) rồi xuất PNG
từ **file 1 trang** (không `--embed-diagram`, không `--page-index` trên file gộp — hai cách đó dễ treo
CLI / PNG phình to):

```powershell
python scripts/txt_to_sequence_drawio_png.py docs/06-sequencediagram/<feature>/UC….txt
```

Từ 07/11/2026 script cho draw.io CLI xuất **SVG**, rồi Edge/Chrome headless chụp SVG thành PNG (ép nền trắng,
chế độ sáng). PNG do draw.io CLI tự vẽ thì mỗi máy hỏng một kiểu với trang lớn: cắt nửa dưới, trắng, treo, hoặc vẽ
hình thu nhỏ ~2/3 ở góc trên trái (Windows, ảnh từ ~10 triệu pixel). Scale tự chọn ≤ 2 sao cho ảnh không quá 28 triệu
pixel, như các ảnh cũ. Máy không có Edge/Chrome (hoặc chưa đặt `BROWSER_EXECUTABLE`) thì script quay về PNG của CLI,
giới hạn 9 triệu pixel — khi đó mở ảnh kiểm tra. Script không còn `taskkill` mọi `draw.io.exe`: cửa sổ draw.io đang mở
không bị đóng. Thêm `--no-splice` nếu chỉ muốn PNG, không ghi vào file draw.io gộp. `--scale 0.5` để ép scale.

Xuất lại ảnh của một tab có sẵn trong file gộp (không cần `.txt`, không sửa file gộp):
`python scripts/txt_to_sequence_drawio_png.py --tab uc14_createmeeting --tab uc55_castvote`.

**07/11/2026:** rà cả 116 ảnh, xuất lại 25 ảnh hỏng bằng `--tab`. Có 9 ảnh vẽ thu nhỏ hoặc cắt dưới (uc09, uc18, uc19,
uc20, uc24, uc28a, uc28c, uc33b, uc74), và 16 ảnh nhúng sơ đồ (`--embed-diagram`) có chunk `zTXt` sai CRC, nên một số
trình xem không mở được (uc14, uc15, uc17, uc21, uc28b, uc29, uc30, uc31, uc52a, uc52b, uc53–uc57, uc73). Sau đợt này
không ảnh nào còn nhúng sơ đồ; nguồn là file gộp và `.txt`.

## Đồng bộ toàn bộ từ file gộp (cũ, chậm hơn)

```powershell
python scripts/sync_sequence_drawio_png.py
```

Cần Draw.io CLI (`DRAWIO_EXECUTABLE` nếu chưa có trong `PATH`). Script này xuất từng tab từ file gộp
ở scale 2 + `--embed-diagram` — với trang lớn dễ chậm hoặc cắt trắng; ưu tiên script `.txt` ở trên.

## Xuất ảnh các trang lớn (27/10/2026)

> Ghi chép cũ, trước khi script chụp bằng trình duyệt (07/11/2026). Các cờ `--disable-gpu --disable-software-rasterizer`
> không còn dùng.

Draw.io CLI 32.6.1 trên máy này chỉ vẽ trọn một trang khi chạy với `--disable-gpu --disable-software-rasterizer`, và cắt
phần dưới hoặc bên phải nếu ảnh ra quá khoảng 2900 px mỗi chiều (ví dụ scale 2 cho trang rộng 4000 đơn vị). Bốn ảnh sau được
xuất lại ngày 27/10/2026 ở scale nhỏ hơn 2 để vẫn trọn vẹn, mở kiểm tra bằng cách so vùng có nét vẽ với kích thước ảnh:

| Ảnh | Scale |
|---|---|
| `uc33a_uploaddocumentstomeeting` | 0.6 |
| `uc33b_attachdocumentstoagendaitem` | 0.5 (bản scale 2 cũ trắng hoàn toàn) |
| `uc34b_viewlockedaccountlibrary` | 0.9 |
| `uc36_uploadtopersonallibrary` | 0.7 |

Muốn nét to hơn thì phóng to trực tiếp trong file Draw.io; ảnh chỉ để xem nhanh.

Máy khác có thể ngược lại (02/11/2026, Windows, Draw.io 32.6.1): xuất `uc04_accessmeetingviaguestinvitation` kèm hai cờ trên thì
treo quá 180 giây, `txt_to_sequence_drawio_png.py` báo lỗi sau khi đã ghép tab; bỏ hai cờ thì xuất trọn trong 15 giây (scale 1.1,
4243×6194). Khi đó chạy lại bước xuất bằng tay: `draw.io --export --format png --scale <s> --output <ảnh> <file 1 trang>`.

## Ảnh đang chờ xuất lại (19/10/2026)

Đợt đồng bộ theo Report 3 ngày 18/10/2026 đã sửa source `.txt`, ảnh SequenceDiagram.org và tab trong file Draw.io gộp,
nhưng **chưa** xuất lại ảnh trong thư mục này. Draw.io CLI 31.7.0 trên máy hiện tại render dở các trang lớn (từ khoảng
29 triệu pixel ở scale 2 trở lên, phần dưới bị trắng) và mất khoảng 3 phút cho mỗi trang. Các ảnh sau còn là bản cũ
hoặc chưa có, cần chạy lại `python scripts/sync_sequence_drawio_png.py` và kiểm tra ảnh không bị cắt:

- Ảnh cũ, tab đã đổi: UC-16, UC-17, UC-22, UC-23a, UC-23b, UC-23c, UC-23d, UC-24, UC-25, UC-26, UC-28b, UC-28c.
- Chưa có ảnh, tab mới: UC-73, UC-74, UC-75, UC-76a, UC-76b, UC-77.

Trong lúc chờ, nguồn đúng là file `.txt` và ảnh `.png` cùng tên trong thư mục của từng feature.

**23/10/2026:** đã xuất ảnh cho tab mới UC-32a, UC-32c, UC-35. Riêng UC-32c (khoảng 36 triệu pixel ở scale 2) bị
cắt phần dưới nên xuất ở **scale 1.5**; chạy lại script toàn bộ sẽ đưa nó về scale 2 — kiểm tra ảnh không bị cắt.

**23/10/2026 (SCRUM-107):** thêm tab UC-33a AF-1 *Upload Documents to Meeting* và xuất ảnh. Cũng xuất ở **scale 1.5**
vì scale 2 bị cắt nửa dưới, như UC-32c.

**27/10/2026 (FE-07):** dựng lại tab UC-32a, UC-32c, UC-33a (tải lên) và thêm 13 tab UC-33a (thư viện), UC-33b, UC-33c, UC-33d, UC-34, UC-36, UC-71a/b/c, UC-72a/b/c/d từ các `.txt` trong `document-management/`; xuất lại 16 ảnh. Cả 16 ảnh xuất ở scale 2 bằng draw.io 32.6.1 (bản 29.6.1 không vẽ biểu tượng actor/boundary/control/database).

**28/10/2026 (BA Final Review FE-07):** dựng lại 9 tab as-built đã đổi MSG/Controller (UC-33a×2, UC-33b–d, UC-35, UC-71b, UC-72a/b) bằng `scripts/txt_to_sequence_drawio_png.py` (style agenda: actor/boundary/control/database, title UC, message 13px). Scale tự chọn — UC-33b ~0.4 vì ~177 message / 35 nhánh `else`.

**30/10/2026 (BA Final Review FE-09/FE-10):** thêm 24 tab (UC-43a → UC-46d, UC-80, UC-81a → UC-81f, UC-39a ghi lời theo micro) và
dựng lại tab UC-39a Manage Microphone. Sửa `txt_to_sequence_drawio_png.py` cho tab dựng từ `.txt` giống tab biểu quyết / agenda:
nhãn Mermaid không bọc dấu nháy (trước đó dấu nháy hiện thành chữ), khi chèn tiêu đề chỉ dời cell cấp trên cùng và điểm của edge
(trước đó activation và guard bị dời lệch 50 px, mũi tên gọi / trả về chồng nhau, guard trôi giữa khung), khung `alt`/`opt` viền
mảnh, guard 13px căn trái, nhãn self-message 13px, và scale chọn theo diện tích ảnh (ảnh 3600–7000 px thay vì ≤ 2900 px). Các tab
FE-07 / FE-08 dựng trước bằng bản script cũ vẫn còn dấu nháy; chạy lại script cho `.txt` tương ứng nếu muốn đồng bộ. Ảnh tab biểu
quyết, agenda… xuất bằng draw.io 29.6.1 nên không có biểu tượng actor / boundary / control (lỗi đã ghi ở mục 27/10); ảnh mới xuất
bằng 32.6.1 nên có biểu tượng, cùng style lifeline trong file gộp.

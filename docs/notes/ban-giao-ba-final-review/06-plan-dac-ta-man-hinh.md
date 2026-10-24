# Plan chụp lại và đặc tả lại màn hình trong Report 3 — BA Final Review 24/10/2026

**Phạm vi:** Report 3 mục **III. Functional Requirements** (đặc tả từng màn) và **I.5.1 Screen
Inventory** (screen flow, screen list), cho 6 note bàn giao. Phần UC, BR, message, diagram ở
[05-plan-sua-uc-br-msg-diagram.md](05-plan-sua-uc-br-msg-diagram.md).

**Trạng thái (24/10/2026):** phần màn hình SCRUM-18/20, SCRUM-97, SCRUM-26 xong và **đã vào Report 3 DOCX** (chữ I.5.1.b, III.4.1–4.5, III.6.1, 6.3, 6.4, 8.3; 7 ảnh thay, 9 ảnh chèn; đã sync). Nguồn: wireframe `TrustCabinet-wireframe-v8.html`, `figma-v8/`, 16 ảnh trong `docs/wireframe/screens/`, chữ ở [patch-dac-ta-man-hinh](note-may-nha/patch-dac-ta-man-hinh.md). Chưa làm: hình Screen Flow I.5.1.a (không có nguồn), Change Log, III.5.x (chờ PR #135 SCRUM-98), Voice ID (FE chưa có màn). Ảnh lấy từ wireframe v8, không chụp từ app; câu hỏi mục 5 dùng mặc định (III.6.4 giữ mục riêng, dialog Đổi vai trò trong III.6.1).

## 0. Cách làm

- Ảnh chụp chỉ nằm trong **DOCX** (`docs/08-reports/source/Report_3_Software_Requirement_Specification.docx`);
  bản Markdown không có ảnh. Sửa DOCX → `python scripts/sync_reports.py`.
- Chụp trên app chạy thật (local hoặc server test) bằng tài khoản seed, **1440px**, giao diện tiếng
  Việt, dữ liệu mẫu gọn (không lộ email thật). Theo [verify-ui-in-browser](../../../.claude/skills/verify-ui-in-browser/SKILL.md).
- Mỗi màn: thay ảnh → sửa đoạn mô tả đầu ("This screen allows…") → sửa bảng field/nút
  (tên tiếng Việt trong `[...]`, UC, MSG) → kiểm tra mã MSG khớp plan 05.
- Chụp sau khi plan 05 đã chốt UC/MSG, để đặc tả màn dẫn đúng số.
- Lưu ảnh gốc vào thư mục tạm cục bộ, đặt tên `III-<mục>-<tên>.png`, chèn vào DOCX.

## 1. Danh sách màn cần chụp và đặc tả lại

### SCRUM-18 / SCRUM-20

| Mục Report 3 | Màn | Chụp | Sửa đặc tả |
|---|---|---|---|
| III.4.1 Meeting Calendar – Calendar View | Lịch dạng tháng/tuần | Lịch của người đã từ chối 1 cuộc họp (cuộc đó biến mất) | Cuộc họp đã từ chối không hiện trên lịch của mình. |
| III.4.2 Meeting Calendar – List View | Lịch dạng danh sách | Thẻ cuộc họp có nút Tham dự / Không tham dự; thẻ đã trả lời | Trả lời lời mời ngay trên thẻ; một lần; Chủ tọa không có nút. |
| III.4.3 Meeting List | Danh sách cuộc họp | Mở mặc định (bộ lọc Đang diễn ra); dropdown thời gian mở | Bộ lọc mặc định Đang diễn ra; thời gian = Hôm nay / Tuần này / Chưa diễn ra / Đã diễn ra; bỏ chọn khoảng ngày (đoạn hiện ghi "time range (month)"). Vẫn hiện cuộc họp đã từ chối. |
| III.4.4 Create / Edit Meeting | Popup tạo / sửa | Popup tạo có ô chọn thành phần; popup sửa | Popup thay trang riêng; thêm field Thành phần tham dự (chọn người, vai trò) và lỗi tương ứng. |
| III.4.5 Meeting Detail – General Information | Chi tiết, tab Thông tin chung | Thanh hỏi "Bạn có tham dự?" ở đầu màn; trạng thái sau khi trả lời; góc nhìn Chủ tọa (không có) | Trả lời ở đầu màn chi tiết, MSG105/MSG154/MSG285/MSG286. |
| III.6.1 Meeting Detail – Participants Tab | Tab Thành phần | Bảng có nhãn phản hồi cạnh tên | Bỏ bộ đếm phản hồi; không trả lời trong tab; nhãn cạnh tên, Chủ tọa không nhãn. |
| III.6.4 Invitation Response | Màn/khối trả lời lời mời | (dùng lại ảnh III.4.5 / III.4.2) | Viết lại: điểm vào ở chi tiết + lịch; một lần; hoặc gộp mục này vào III.4.5 — chọn khi viết. |

### SCRUM-97

| Mục Report 3 | Màn | Chụp | Sửa đặc tả |
|---|---|---|---|
| III.6.1 Participants Tab | Tab Thành phần, menu dòng | Menu dòng có "Đổi vai trò"; tên người đăng nhập có "(Tôi)" | Hành động Đổi vai trò trên từng dòng. |
| (mục mới hoặc trong III.6.1) | Dialog Đổi vai trò | Radio các vai trò kèm dòng mô tả quyền; chọn Chủ tọa → nút **Giao Chủ tọa**; trường hợp không có lựa chọn Chủ tọa | Bảng field cho dialog: radio, mô tả, nút Giao Chủ tọa / Lưu, MSG257/259/260/102, MSG256 (Thư ký). Không có MSG258 trên UI. |

### SCRUM-26

| Mục Report 3 | Màn | Chụp | Sửa đặc tả |
|---|---|---|---|
| III.6.3 Attendance Check | Cột điểm danh trong tab Thành phần | Bảng thành phần khi cuộc họp InProgress, góc nhìn Thư ký (cột điểm danh) và Thành viên (chỉ xem) | Điểm danh là cột trong bảng, không phải panel; không có bộ đếm có mặt/vắng; 409 MSG151 → báo lỗi, tải lại, chọn lại (không giữ nháp); MSG150 câu trung tính; MSG289 lỗi đọc. |
| III.8.3 In-Room Participants Panel | Panel người tham gia trong phòng họp | Panel điểm danh trong phòng | Panel điểm danh chỉ ở phòng họp; cùng quy tắc như trên. |

### Chương trình nghị sự (SCRUM-98, UC-23, UC-77 — note 08)

Chụp sau khi PR SCRUM-98 / UC-77 merge (ngày 24/10/2026 code còn thời lượng).

| Mục Report 3 | Màn | Chụp | Sửa đặc tả |
|---|---|---|---|
| III.5.1 Meeting Content Tab | Tab Nội dung cuộc họp | Danh sách mục nghị sự (có item Done), cột người trình bày | Bỏ cột Thời lượng và dòng tóm tắt "4 mục · 60 phút / khung 60 phút"; người trình bày là khách có nhãn "(khách)"; sắp xếp được cả item Done. |
| III.5.2 Add / Edit Agenda Item Drawer | Drawer thêm/sửa mục | Drawer thêm mới; drawer sửa item đang InProgress/Done | Bỏ ô Thời lượng và gợi ý giờ dự kiến; người trình bày chọn từ danh sách, không nhập tự do; sửa được ở mọi trạng thái; chọn tài liệu chỉ đổi liên kết của item đó. |
| III.5.3 Meeting Facilitation | Tab nội dung khi InProgress | Chủ toạ bắt đầu item bất kỳ; item đang chạy; ô kết luận | Bắt đầu item Pending bất kỳ; bỏ "regardless of planned duration"; khối Kết luận (UC-77, tối đa 2.000 ký tự, MSG271–274), khoá khi đã có biên bản. |

### SCRUM-107

PR #128 chỉ làm backend; FE chưa gọi `POST …/documents/upload`, nên tab "Từ máy tính" của dialog
chưa chụp được bản thật. **Chỉ sửa chữ, giữ ảnh cũ** đến khi FE nối API.

| Mục Report 3 | Màn | Chụp | Sửa đặc tả |
|---|---|---|---|
| III.7.4 Add Document to Meeting Dialog | Dialog thêm tài liệu, tab Từ máy tính | Chưa — chụp lại khi FE nối API (vùng thả tệp, danh sách tệp lỗi, thông báo MSG288) | Vùng tải lên: đuôi `.pdf`/`.docx` phải khớp nội dung; MSG288 cho > 10 tệp hoặc > 200 MB; MSG164 cho tệp sai loại hoặc request không có tệp; người không còn quyền nhận MSG162 dù tệp sai (hệ thống kiểm quyền trước); mất quyền xem cuộc họp nhận MSG174. [x] Đã sửa chữ trong DOCX 24/10/2026 ([patch-chu-man-hinh](note-may-nha/patch-chu-man-hinh.md)); ảnh chờ FE. |
| III.7.3 Meeting Detail – Documents Tab | Tab Tài liệu | Không chụp lại | Dòng tài liệu: số trang chỉ hiện khi biết (PDF có `pageCount`), không rõ thì bỏ trống. [x] Phần chữ đã sửa trong DOCX 24/10/2026. |

### Voice ID (SCRUM-31, 35, 102, 103)

FE chưa có màn Voice ID, màn bản ghi, transcript. **Chưa chụp được.**

- [x] III.9.2 Upload Audio Dialog (đã sửa chữ trong DOCX 24/10/2026): sửa chữ câu đồng ý ghi âm từ NĐ 13/2023 sang Luật 91/2025/QH15 + NĐ 356/2025/NĐ-CP (chỉ sửa chữ, giữ wireframe đến khi có màn).
- [x] III.10.1 Speaker-Attributed Transcript (đã sửa chữ trong DOCX 24/10/2026): ghi "hệ thống gợi ý tên người nói, Thư ký xác nhận" khi viết lại.
- [ ] Màn đăng ký giọng mẫu / đồng ý Voice ID: thêm mục mới khi FE xong; tạm thời giữ chỗ.

## 2. Screen Inventory (Report 3 I.5.1)

- [ ] a. Screen Flow (patch mục 2.a; chưa làm: không có nguồn hình, giữ hình cũ): Tạo/Sửa cuộc họp là popup từ Danh sách/Chi tiết, không còn màn riêng; thêm dialog Đổi vai trò.
- [x] b. Screen List (patch mục 2.b, thêm dòng 6.1b): sửa dòng Create/Edit Meeting (popup), Invitation Response (gộp hoặc đổi mô tả), Attendance Check (cột trong tab), thêm Change Role Dialog; Voice ID để sau.

## 3. Danh sách ảnh cần chụp (checklist chụp một lượt)

Tài khoản seed: Thư ký, Chủ tọa, Thành viên, Người tổ chức. Chuẩn bị 1 cuộc họp Published (có người
chưa trả lời, đã đồng ý, đã từ chối) và 1 cuộc họp InProgress.

- [x] Lịch tháng — Thành viên đã từ chối 1 cuộc họp (chỉ ghi chú ở 4.1, khung không đổi)
- [x] Lịch danh sách — thẻ có nút trả lời; thẻ đã trả lời (4.2)
- [x] Danh sách cuộc họp — mặc định Đang diễn ra; dropdown thời gian mở (4.3, 4.3b)
- [x] Popup tạo cuộc họp — có ô thành phần; popup sửa (4.4, 4.4b)
- [x] Chi tiết — thanh trả lời (Thành viên); sau khi trả lời; góc nhìn Chủ tọa (4.5b, 4.5c; Chủ tọa = ghi chú, không nhãn)
- [x] Tab Thành phần — nhãn phản hồi; "(Tôi)"; menu Đổi vai trò (6.1; FE là nút bút, không phải menu)
- [x] Dialog Đổi vai trò — Chủ tọa chọn "Chủ tọa"; không có lựa chọn Chủ tọa; Thư ký mở (6.1b, 6.1c)
- [x] Tab Thành phần InProgress — cột điểm danh (Thư ký); chỉ xem (Thành viên); thông báo 409 (6.3, 6.3b; 409/MSG151 ghi trong notes, không vẽ)
- [x] Phòng họp — panel điểm danh (8.7)

## 4. Thứ tự

Plan này là **lượt 6** trong bảng thứ tự ở
[plan 05 mục 8](05-plan-sua-uc-br-msg-diagram.md#8-thứ-tự-và-cách-chạy-agent-đọc-mục-này-trước);
cách chạy một lượt (file patch, không sửa DOCX, một commit, dừng báo Khánh) theo mục 8.2 đó.
Agent chụp ảnh và viết chữ vào `patch-dac-ta-man-hinh.md`; Khánh chèn ảnh và dán chữ vào DOCX.

1. Chốt UC/MSG theo plan 05 (lượt 1–5: SCRUM-26, 107, 97, 18/20, chương trình nghị sự).
2. Dựng dữ liệu mẫu, chụp một lượt theo mục 3.
3. Sửa đặc tả từng mục theo bảng mục 1, rồi Screen Inventory.
4. `sync_reports.py`, rà bản Markdown, ghi Change Log.
5. SCRUM-107 (III.7.4) và Voice ID: sửa chữ trước, chụp sau khi FE có màn.

## 5. Câu hỏi

1. III.6.4 Invitation Response: giữ mục riêng hay gộp vào III.4.5?
2. Dialog Đổi vai trò: mục mới (III.6.x) hay trong III.6.1?
3. Chụp trên local hay server test (dữ liệu server test có thật hơn)?

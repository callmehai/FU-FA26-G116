# Note cho máy nhà

Patch Report 3 của đợt BA Final Review. Ngày 24/10/2026 cả 4 patch đã dán vào DOCX và xuất PNG trên
máy có draw.io; bảng dưới ghi trạng thái và phần còn lại.

| File | Trạng thái (24/10/2026) |
|---|---|
| [patch-scrum-26.md](patch-scrum-26.md) | SCRUM-26 / UC-31: đã vào Report 3 DOCX từ lượt 1. PNG `uc31_recordattendance` đã xuất (draw.io `--scale 1.5`, 4972×4184, không nhúng diagram); nhãn MSG150/MSG151 nằm trong khung. |
| [patch-scrum-107.md](patch-scrum-107.md) | SCRUM-107 / UC-33a: đã dán vào DOCX (II.7.2.1, BR-215, V.2 MSG164/MSG174/MSG288) và chạy `sync_reports.py --report 3`. PNG `uc33a_uploaddocumentstomeeting` đã xuất (`--scale 1.5`, 5624×5072) |
| [patch-scrum-97.md](patch-scrum-97.md) | SCRUM-97 / UC-73, UC-28b: đã dán vào DOCX (II.6.6, II.6.2.2, BR-126, BR-295, V.2 MSG256/MSG258) và sync. PNG `uc28b_assignchangemeetingrole` (5743×4739) và `uc73_handoverchair` (5263×2839) đã xuất (`--scale 1.5`) |
| [patch-scrum-18-20.md](patch-scrum-18-20.md) | SCRUM-18/20 / UC-14, UC-17, UC-28a, UC-29, UC-30: đã dán vào DOCX (I.4.2, II.4.1, II.4.4, II.6.2.1, II.6.3, II.6.4, BR-30/105/106/115/176, thêm BR-320/BR-321, MSG154, thêm MSG285/MSG286/MSG277). PNG xuất bởi session lượt 4 |
| [patch-chu-man-hinh.md](patch-chu-man-hinh.md) | Lượt 6, chỉ sửa chữ: III.7.3 (số trang chỉ hiện khi biết), III.7.4 (SCRUM-107: đuôi khớp nội dung, MSG162/164/174/288), III.9.2 (Luật 91/2025/QH15 + NĐ 356/2025/NĐ-CP), III.10.1 (gợi ý, Thư ký xác nhận). Đã dán 7 dòng vào DOCX, không đổi ảnh, đã sync; 0 dòng không khớp |
| [patch-dac-ta-man-hinh.md](patch-dac-ta-man-hinh.md) | Lượt 6, đặc tả màn hình SCRUM-18/20, SCRUM-97, SCRUM-26: đã dán vào DOCX (I.5.1.b Screen List, III.4.1–4.5, III.6.1 kèm bảng Change Role Dialog, III.6.3, III.6.4, III.8.3), thay 7 ảnh và chèn 9 ảnh từ wireframe v8, rồi sync. 63/67 câu cũ khớp (4 còn lại là hình Screen Flow và một dòng gộp, không phải lỗi) |

`sync_sequence_drawio_png.py` dùng `--scale 2` làm ảnh bị cắt nửa dưới trắng và chạy cả 51 tab mất hơn 5 phút, nên PNG được xuất từng tab bằng draw.io CLI `--scale 1.5`. Không dùng `--embed-diagram`: bản draw.io hiện tại nhúng cả 54 tab vào mỗi PNG (3,9 MB, CRC chunk `zTXt` sai).

## Chưa dán (ngoài phạm vi 4 mục)

Chỉ dán §I.4.2, §II, §V.1, §V.2 theo yêu cầu. Các dòng sau còn nằm trong patch:

| Patch | Mục Report 3 | Nội dung |
|---|---|---|
| 107 | I.3.2 Entity 19 StoredFile, field `PageCount` | Mô tả mới: đếm trang bởi trình duyệt, chỉ lưu cho PDF khi là số nguyên dương |
| 26, 107, 97, 18-20 | Change Log (cuối bảng) | Bốn dòng 24/10/2026 (mục Change Log của mỗi patch) |
| 18-20 | Mục 10 của patch (I.3.2 InvitationResponse, Permission Matrix I.4.4, ảnh I.4.3.4, danh mục màn hình I.4.5, §III Meeting List / Create / Participants / Invitation Response, email lời mời) | Ghi Jira / plan 06, không có câu dán |
| 26, 107 | §III đặc tả màn hình "Attendance Check", "Add Document to Meeting Dialog" | Thuộc plan 06 |
| 6 (màn hình) | I.5.1.a Screen Flow (hình) | Chưa làm: không có file nguồn của hình; giữ hình cũ. Các thay đổi cần vẽ ghi ở mục 2.a của patch-dac-ta-man-hinh |
| 6 (màn hình) | Change Log | Một dòng 24/10/2026 cho lượt đặc tả màn hình chưa dán |
| 6 (màn hình) | III.5.x chương trình nghị sự | Chờ PR #135 (SCRUM-98) |

## Dòng patch không khớp nguyên văn

Không có: 32 dòng áp dụng (SCRUM-107: 13, SCRUM-97: 19, tính cả ô bảng BR/V.2; SCRUM-26 đã vào DOCX từ lượt 1) đều khớp đúng một chỗ. SCRUM-18/20: 40 dòng thay thế, 3 khối chèn (EX-3 UC-14, AF-4 UC-17, EX-4/EX-5 UC-29), 4 ô bảng, 5 dòng bảng mới, đều khớp một chỗ; ba ô UC-29 AF-1 / UC-14 bước 6 / I.4.2 UC-14 bị tách bởi `lastRenderedPageBreak` hoặc dính liền một ô nên đã nối lại mới khớp.

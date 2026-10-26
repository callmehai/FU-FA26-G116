# Bàn giao FE-07 cho Lộc — phần tài liệu chưa code

**Ngày:** 27/10/2026. **Người viết:** KhanhPG. **Nguồn:** Jira SCRUM-21/22/23/58/91/92/96/107 đọc ngày 27/10/2026,
Report 3 (Markdown), `docs/06-sequencediagram/document-management/`, bản đồ UC–BR–MSG sinh bằng
`scripts/trace_br.py`.

Mục tiêu: Lộc code tiếp phần FE-07 còn lại mà không phải tự dò UC, BR, MSG và tự đoán chỗ nào còn thiếu sequence hay ticket.

## 1. Hiện trạng

| Nhóm chức năng | Ticket | Trạng thái Jira | Code | UC có đặc tả | Sequence | Việc còn thiếu |
|---|---|---|---|---|---|---|
| Tải tệp từ máy lên cuộc họp | SCRUM-107 | BA Final Review | Đã merge | UC-33a | UC33a (AF-1) | Không |
| Xem danh sách, đọc tệp | SCRUM-21, 22 | BA Final Review / Backend | Đã merge | UC-32a, 32c | UC32a, UC32c | Không |
| Gắn tệp vào mục nghị sự | SCRUM-25 / 98 | — | Đã merge | UC-33b | **Chưa có** | Sequence UC-33b |
| Bình luận tài liệu | SCRUM-23 | Backend | Chưa code | UC-35 | UC35 | Không: ticket đủ tiêu chí, doc đã xong |
| Xác nhận đọc tài liệu biểu quyết | **SCRUM-96** (xem mục 2) | To Do (Huy) | Chưa code | UC-74 | UC74 | Ticket riêng cho phần tài liệu, hoặc ghi rõ trong SCRUM-96 |
| Kho cá nhân, thư mục, gắn/gỡ | SCRUM-58 | Business Analyst | Chưa code | UC-34, 36, 71a–c, 72a–d, 33c, 33d | **Chưa có** (12 UC) | Sequence, tách ticket, class diagram |

## 2. Chỗ lệch cần sửa trước khi bàn giao

1. **SCRUM-74 không phải ticket xác nhận đọc.** Trên Jira SCRUM-74 là subtask
   "[DOCS-04][FE-04] Sequence Diagram — Quản lý cuộc họp (UC-14–22)". UC-74 thuộc **SCRUM-96**
   (đồng bộ Biểu quyết, UC-52–55 và UC-74, Huy giữ, To Do). Bảng cũ ghi nhầm số ticket. Đổi lại thành SCRUM-96.
2. **UC-33d chưa nằm trong mô tả SCRUM-58.** Mô tả chỉ liệt kê UC-33a, 33c, 34, 36, 71, 72. UC-33d (gỡ khỏi một mục
   nghị sự) có spec và MSG239/241/245/246, nhưng chưa có ticket nào nhận. Thêm vào SCRUM-58 hoặc vào ticket con "gắn/gỡ".
3. **SCRUM-58 quá lớn cho một story:** 12 UC, ba cụm khác nhau (thư mục, tài liệu kho, gắn/gỡ cuộc họp), chưa có subtask.
   Tách như mục 3.
4. **Sequence FE-07:** `docs/06-sequencediagram/document-management/README.md` đã ghi các UC trên "chưa có sequence".
   Subtask SCRUM-91 (To Do) là chỗ đặt việc này; SCRUM-92 là class diagram FE-07. Không cần mở ticket mới cho diagram.

## 3. Cách tách ticket và nhánh

Thứ tự code đề xuất cho Lộc: SCRUM-23 chạy độc lập ngay; SCRUM-58 theo ba phần dưới, phần sau dùng lại phần trước.

| # | Ticket con của SCRUM-58 | UC | BR | MSG |
|---|---|---|---|---|
| 58a | Thư mục kho cá nhân | UC-72a, 72b, 72c, 72d | 191, 192, 204, 219–223, 227, 228, 242 | 175, 179, 187–198 |
| 58b | Tài liệu trong kho cá nhân | UC-34, 36, 71a, 71b, 71c | 183, 186, 187, 204, 214–216, 219, 224, 225, 229, 232, 242, 245, 251, 276, 286 | 164, 175–186 |
| 58c | Gắn và gỡ tài liệu của cuộc họp | UC-33c, 33d | 184, 185, 197, 211, 212, 231, 277–281, 284 | 167, 169–171, 174, 239, 241, 242, 245–248 |

Lý do thứ tự: 58a cần trước vì 58b tải lên "vào thư mục đang chọn" (UC-36) và đổi thư mục (UC-71c). 58c dùng tài liệu
đã có trong kho. UC-33b đã merge; chỉ còn thiếu sequence.

Nhánh docs, một nhánh một cụm, cắt từ `origin/main`:

| Nhánh | Nội dung | Ticket |
|---|---|---|
| `docs/scrum-58-ban-giao-kho-tai-lieu` | Note này, link ở README notes | SCRUM-58 |
| `docs/scrum-91-sequence-thu-muc` | Sequence UC-72a–d | SCRUM-91 |
| `docs/scrum-91-sequence-kho-tai-lieu` | Sequence UC-34, 36, 71a–c | SCRUM-91 |
| `docs/scrum-91-sequence-gan-go` | Sequence UC-33b, 33c, 33d | SCRUM-91 |
| `docs/scrum-92-class-diagram-fe07` | Hoàn chỉnh class diagram FE-07 cho phần kho | SCRUM-92 |

Mỗi nhánh sequence kèm `.txt` và `.png` cùng tên, dòng mới trong README của thư mục, và đối chiếu mã MSG theo bảng trên.
Dòng MSG nào code đang dùng mà Report 3 chưa có thì ghi chú vào ticket, không tự thêm vào Report 3 (xem AGENTS.md, mục
"BA settles the requirement docs last").

## 4. Nháp ticket (chưa tạo trên Jira)

### 58a — Thư mục kho cá nhân
- **UC:** UC-72a tạo, 72b đổi tên, 72c chuyển, 72d xoá. **BR:** BR-191 (lồng tối đa 5 cấp, không chuyển vào chính nó hay
  thư mục con), BR-192 (thư mục tự tạo `<tên cuộc họp> – dd/mm/yyyy`), BR-204, BR-219–223, BR-227, BR-228, BR-242.
- **Nghiệm thu:** tên không trống, không bắt duy nhất; xoá chỉ khi thư mục rỗng (ràng buộc `Restrict` đã có ở
  `DocumentConfiguration`); chỉ chủ sở hữu thấy thư mục của mình; thư mục tự tạo từ UC-33a hiện như mọi thư mục.
- **Endpoint gợi ý:** `GET/POST/PATCH/DELETE /api/folders`. Entity `Folder` đã có từ SCRUM-107, không migration lại.
- **Phụ thuộc:** không.

### 58b — Tài liệu trong kho cá nhân
- **UC:** UC-34 xem, UC-36 tải lên, UC-71a xoá, 71b đổi tên, 71c chuyển thư mục.
- **Nghiệm thu:** dùng lại kiểm tra tệp và `FileStorage` của SCRUM-107 (PDF/DOCX, 50 MB, 10 tệp / 200 MB mỗi lần);
  xoá chỉ xoá dòng kho, cuộc họp đang dùng vẫn đọc bình thường (BR-186); màn kho hiện "Đang dùng trong n cuộc họp";
  chỉ chủ sở hữu đọc.
- **Endpoint gợi ý:** `GET/POST/PATCH/DELETE /api/documents`.
- **Phụ thuộc:** 58a.
- **Chưa chốt (từ SCRUM-58):** hạn mức dung lượng kho, quyền đọc phần chưa gắn của người đã rời tổ chức.

### 58c — Gắn và gỡ tài liệu của cuộc họp
- **UC:** UC-33c gỡ khỏi cuộc họp, UC-33d gỡ khỏi một mục nghị sự.
- **Nghiệm thu:** chỉ Chủ toạ hoặc Thư ký của chính cuộc họp (`MeetingRole`, BR-184); chặn sau khi cuộc họp kết thúc
  (BR-185); gỡ là đặt `RemovedAt`, góp ý giữ làm lịch sử nhưng không hiện (BR-284); gỡ khỏi một mục không gỡ khỏi cuộc họp.
- **Endpoint gợi ý:** `DELETE /api/meetings/{id}/documents/{meetingDocumentId}` và gỡ liên kết mục nghị sự.
- **Phụ thuộc:** không bắt buộc 58a/b, nhưng test cần tài liệu đã gắn.

### Tài liệu biểu quyết (UC-74)
Nếu SCRUM-96 không bao phần tài liệu: ticket riêng "Xác nhận đã đọc tài liệu biểu quyết" cho UC-74 (BR-181, 275, 287,
289–291, 293; MSG156, 253–255), Chair, Secretary, Participant và Guest, sau khi UC-76a/b (gắn tài liệu vào phiếu bầu) xong.

## 5. Độ phủ BR/MSG hiện có trong code (để Lộc biết phần nào đã có tag)

| UC | BR có code / tổng | BR có test |
|---|---|---|
| UC-33b | 7 / 15 | 3 |
| UC-33c | 2 / 9 | 1 |
| UC-33d | 3 / 6 | 1 |
| UC-34 | 0 / 4 | 0 |
| UC-36 | 7 / 9 | 4 |
| UC-71a / b / c | 0 / 4, 1 / 5, 0 / 4 | 0, 1, 0 |
| UC-72a / b / c / d | 0 / 6, 1 / 6, 1 / 6, 2 / 6 | 0 |
| UC-35 | 3 / 10 | 0 |
| UC-74 | 2 / 7 | 0 |

Số đếm bằng tag `BR-nn` trong comment, chỉ là gợi ý chỗ đã có; xem bản đồ chi tiết bằng
`python scripts/trace_br.py --html` rồi mở `docs/uc-map.html`.

## 6. Việc cần chốt với PO / BA

1. Đổi số ticket UC-74 từ SCRUM-74 sang SCRUM-96 trong mọi chỗ đang ghi.
2. Tách SCRUM-58 thành ba ticket con như mục 3.
3. Thêm UC-33d vào phạm vi.
4. Hạn mức dung lượng kho cá nhân.

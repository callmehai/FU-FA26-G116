# SCRUM-20, 25, 26, 69, 97, 98 — đối chiếu code `main` và việc BA Final Review (FE-05/FE-06)

**Người ghi:** thành viên nhóm (tin nhắn bàn giao cho KhánhPG/BA), 04/11/2026. **Nguồn:** tin nhắn đối chiếu 6 ticket đang `In Review`
(SCRUM-20, 25, 26, 69, 97, 98) với code trên `main` (commit `33260fbd`). **Kết luận của người đối chiếu:** code chạy đúng
những gì Jira đã chốt; chỉ tài liệu còn lệch, nhờ BA xử lý lúc BA Final Review. Note này gom việc BA, không thay Jira hay đặc tả.

> **Ràng buộc nhánh:** Report 3 DOCX đang được sửa trong PR #254. DOCX không merge được (gộp 3 chiều theo đoạn),
> nên các mục dưới đây **sửa sau khi #254 merge vào `main`, hoặc sửa luôn trên nhánh của #254**.

## 1. FE-06: còn "quyền bổ sung" và "Organizer khôi phục Thư ký"

PO đã bỏ hai thứ này ngày 20/10. Code không còn bảng `grants`, `MSG152`/`MSG153` đã ngừng dùng. Ai xem được cuộc họp
thì xem được danh sách và phản hồi. Người đối chiếu đã ghi "rà ở lượt riêng" trong comment SCRUM-20 ngày 24/10,
nhưng trên `main` vẫn chưa sửa.

### 1.1 UC

- **UC-28a:** actor đổi thành Secretary, Chair; bỏ Organizer; bỏ "additional permissions"; xoá AF-2 Restore Secretary;
  EX-2 (`MSG152`) đổi thành nhánh `MSG275`; EX-4 bỏ "Secretary recovery".
- **UC-28b:**
  - Đổi tên thành Manage Meeting Role, kể cả các dòng `MSG100`/`MSG101`/`MSG102`/`MSG109` ở §V.2.
  - Bỏ "additional permissions".
  - Xoá AF-2 Change Guest permissions.
  - Bỏ `MSG152` và `MSG153`.
- **UC-28c, UC-04 POST-2, UC-29 POST-2:** bỏ "additional permissions".
- **UC-30:** PRE-2 đổi thành "ai xem được cuộc họp thì xem được phản hồi"; bỏ câu "Response-view permission…" ở bước 3
  và phần "permission" ở EX-1.
- **UC-32a, UC-32c:** bỏ tham chiếu BR-200 (quy tắc cấm tải tài liệu Mật vẫn nằm ở BR-188).

### 1.2 BR

- Cho ngừng dùng BR-78, 174, 175, 177, 199, 200.
- Viết lại BR-79 (Chủ tọa và Thư ký quản lý danh sách, Organizer không có quyền riêng), BR-122 và BR-176.
- Bỏ "additional meeting permissions" ở BR-92, 96, 97, 168, 173.

### 1.3 Ma trận quyền (Report 3 §I.4.4)

- Dòng xem thành phần (ô Thành viên và Khách).
- Đổi tên dòng "Manage meeting role & additional permissions".
- Bỏ chú thích "Additional Meeting permissions…".

### 1.4 ERD và từ điển dữ liệu

- Bỏ câu `MeetingParticipantPermission` trong chú thích ERD, xoá Entity 7a.
- Ảnh ERD trong Report 3 cũng vẫn còn vẽ bảng này (sửa cùng đợt ảnh).

### 1.5 Màn hình (Report 3 §III.6.1) và hộp thoại Thêm thành phần

- Bỏ hai dòng "Quyền bổ sung", mục "Assign or Restore Secretary" và nút "Add Secretary".
- Nhãn phản hồi hiện cho mọi người xem được cuộc họp.

### 1.6 Sequence FE-06

- Viết lại UC28a và UC28b, vì còn `MSG152`/`MSG153` và nhánh khôi phục Thư ký.
- UC27, 28c, 29, 30, 31: sửa dòng "roles and additional permissions".
- README FE-06: sửa đoạn BR-78.

### 1.7 Tên cũ còn sót

- Tên UC-28b cũ còn ở dòng UC-28 của master inventory và ở dòng `MSG100` trong catalogue
  (`docs/03-api/00-danh-muc-ma-thong-bao.md`).

## 2. FE-06: ai quản lý danh sách, và các nhánh lỗi

Đặc tả ghi Chủ tọa chỉ quản lý danh sách trước khi công bố. Code (`ParticipantRules.IsManager`) cho Chủ tọa và Thư ký
quản lý ở mọi trạng thái còn sửa được, đúng baseline PO ngày 20/10. Nếu PO muốn giới hạn lại thì báo BE sửa code
(không phải việc BA Final Review đợt này).

Các nhánh code trả mà đặc tả chưa ghi, hoặc ghi khác:

| # | Code | Đặc tả hiện tại | Sửa |
|---|---|---|---|
| 1 | Xem được cuộc họp nhưng không phải Chủ tọa hay Thư ký: `MSG85` | UC-28a/b/c đang ghi `MSG98` | Sửa UC-28a/b/c theo code |
| 2 | Danh sách vừa bị người khác sửa: `MSG275` | Chưa có | Thêm EX vào UC-28a/b/c và UC-73 |
| 3 | Thêm người làm Chủ tọa khi đã có Chủ tọa: `MSG109` | UC-28a đang ghi `MSG97` | Sửa UC-28a |
| 4 | Người được chọn không còn trong danh sách, hoặc là Khách: `404` rỗng | Đang ghi `MSG98` hoặc `MSG101` | Sửa theo code (404 rỗng, không mã) |
| 5 | Sau khi công bố, Thư ký thay Chủ tọa bị khoá nhưng chọn người nhận không hợp lệ: `MSG259` | UC-28b chưa có nhánh này | Thêm vào UC-28b |
| 6 | Chủ tọa tự giao vai trò qua `PUT` sau khi công bố: `MSG257` (đặc tả ghi `MSG100`); không có vai trò nào đổi: `MSG154` | UC-28b ghi `MSG100`, thiếu `MSG154` | Sửa UC-28b theo code |
| 7 | Gỡ Chủ tọa bị chặn (`MSG109`) ở mọi trạng thái | UC-28c ghi chỉ Published/InProgress | Sửa UC-28c: chặn ở mọi trạng thái |

## 3. FE-05: chương trình nghị sự

- **UC-24 EX-1 và UC-25 EX-1:** mục nghị sự không thuộc cuộc họp thì code trả `404` rỗng, không phải `MSG73`/`MSG74`.
- **"Thời lượng" còn sót:** [note 18 §2.1](18-sua-wireframe-va-viec-ngoai-pham-vi.md) đã theo dõi phần này. Bổ sung thêm:
  màn phòng họp "Current item" (còn "planned duration", ví dụ "40 phút"), dòng `MSG66` ở §V.2 (`plannedDurationMinutes`),
  và `docs/06-sequencediagram/README.md` dòng 242, 459, 466.

## 4. Mẹo khi xuất lại sequence trên Windows

Script `txt_to_sequence_drawio_png.py` với draw.io trên máy Windows sinh tab lệch kiểu so với các tab khác:

- lặp hộp participant ở đáy sơ đồ;
- mũi tên `elbowEdgeStyle` thay vì `sequenceEdgeStyle`;
- nét đứt `2 2` thay vì `3 3`;
- đổi cả file gộp sang CRLF.

Chạy xong nhớ so lại với các tab cũ và đưa file về LF trước khi commit.

## 5. Phần người đối chiếu đã làm trong PR

- `MSG272` đổi thành "Nhập kết luận tối đa 2.000 ký tự.", `MSG273` đổi thành "Không thể sửa kết luận của mục nghị sự này nữa."
  cho khớp UC-77. Mã không đổi.

## 6. Việc BA còn lại

- [x] Mục 1 (FE-06 quyền bổ sung + Organizer): UC-28a/b/c, UC-04 POST-2, UC-29 POST-2, UC-30, UC-32a/c, BR (§1.2),
  ma trận §I.4.4, ERD + Entity 7a, README FE-06, inventory + catalogue. Còn pending: màn §III.6.1 (đặc tả màn hình, theo yêu cầu không sửa), sequence FE-06 (đã sửa .txt và xuất PNG). Ảnh ERD đã thay bằng bản muc-tieu mới.
- [x] Mục 2 (FE-06 nhánh lỗi): UC-28a/b/c và UC-73 đã cập nhật các nhánh MSG85, MSG109, MSG275, 404 rỗng, MSG259, MSG257/MSG154.
- [x] Mục 3 (FE-05): UC-24 EX-1, UC-25 EX-1 (`404` rỗng) và các chỗ "thời lượng" còn sót (MSG66, README sequence) đã sửa. Còn pending: màn phòng họp "Current item" (đặc tả màn hình, không sửa theo yêu cầu).
- [ ] Comment kết quả vào SCRUM-20, 25, 26, 69, 97, 98 sau khi docs vào `main` (theo `DEVELOPMENT_WORKFLOW.md` §2.2) — **06/11/2026:**
  - [x] SCRUM-20: xong 05/11, comment `10442` (UC-28a thêm/mời, UC-29/30 theo code); đang `In Review`, không cần thêm.
  - [x] SCRUM-26: comment `10445` 06/11 (UC-31 chỉ sửa câu chữ quyền bổ sung); đang `In Review`, giữ nguyên.
  - [x] SCRUM-97: comment `10446` 06/11 (UC-73/UC-28b: BR-79, MSG85/275/109/259/257/154); đang `In Review`, giữ nguyên.
  - [x] SCRUM-98: comment `10447` 06/11, bổ sung sau `10335` (UC-24/25 EX-1 → 404 rỗng, nốt "thời lượng"); đang `In Review`, giữ nguyên.
  - [x] SCRUM-69: không cần comment — BA Final Review xong từ 29/10 (`10332`), note 32 không đổi UC-21/22.
  - [ ] SCRUM-25: **CHƯA (Jira chưa xong)** — ticket đang `Frontend`, BA còn việc mở theo comment `10438` (sửa BR-65/34/74, Actor UC-23a–d, editor-options vào sequence); gộp phần EX-1 (UC-24/25 → 404 rỗng, PR #259) vào đợt đó, chưa ghi "xong".

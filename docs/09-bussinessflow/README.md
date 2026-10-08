# Business Flow (BF)

Bốn swimlane mô tả luồng nghiệp vụ chính. File `.drawio` là nguồn để sửa; file `.drawio.png` có
nhúng XML nên mở bằng draw.io vẫn sửa tiếp được.

| File | Nội dung |
|---|---|
| BF-01_System_Access_Administration | Quản trị tài khoản, đăng nhập |
| BF-02_Meeting_Preparation_Approval | Tạo, chuẩn bị, gửi duyệt, công bố, phản hồi lời mời, dời lịch |
| BF-03_Meeting_Execution_Decision_Making | Bắt đầu, điểm danh, vòng agenda và biểu quyết, kết thúc |
| BF-04_Post_Meeting_Processing_Formal_Records | Upload audio, transcript, tóm tắt, biên bản, ký, niêm phong |

## Thay đổi 09/10/2026 — PO chốt, chưa đồng bộ vào docs

BF-02/03/04 đã vẽ lại theo luồng chính của AI-eCabinet. Các tài liệu dưới đây **chưa** sửa theo;
danh sách này là việc cần làm.

### 1. Bỏ Meeting cha, `MeetingSession` đổi thành `Meeting`

AI-eCabinet không có cấp nào trên cuộc họp. Chữ "phiên họp" trên UI của họ là **agenda item**
(`meeting_agenda_items`), không phải cuộc họp con. Từ nay một cuộc họp = một buổi họp, tự mang lịch,
vòng đời, agenda, thành phần, tài liệu, biểu quyết và biên bản.

UC bị ảnh hưởng — **FE-05 Meeting Session Management không còn là feature riêng**:

| UC | Xử lý đề xuất |
|---|---|
| UC-26 Create Meeting Session | Bỏ — lịch nhập ngay trong UC-15 Create Meeting |
| UC-27 Update Meeting Session | Bỏ — gộp vào UC-16 Update Meeting; sau Published chỉ được dời giờ/địa điểm/link (Reschedule) |
| UC-28 Cancel Meeting Session | Bỏ — eCabinet không có hủy cuộc họp. **PO cần chốt** có thêm Cancel Meeting hay không |
| UC-29 Start Meeting Session | Bỏ — trùng UC-43 Start Meeting |
| UC-30 End Meeting Session | Bỏ — trùng UC-44 End Meeting |
| UC-53 View Meeting Sessions | Bỏ |
| UC-54 View Meeting Agenda | Giữ, đổi quan hệ: `<<extend>>` UC-17 thay vì UC-53 |
| UC-31 → UC-36 Agenda | Giữ; Agenda Item thuộc Meeting, không thuộc Session |

UC-26 → UC-30 và UC-53 nằm trong dải ID đóng băng (UC-01 → UC-54): đánh dấu "không còn dùng",
không xoá số, không đánh lại số.

Tài liệu cần sửa: Report 1 (FE-05, FE-06), Report 3, Master UC Inventory, UC Specification,
Business Rules, ERD (`MeetingSession`, `AgendaItem.MeetingSessionId`), vòng đời cuộc họp,
`docs/02-design/04-luong-nghiep-vu-chinh.md`, sequence diagram `meeting-session-management/`,
class diagram FE-05, API `05-quan-ly-cuoc-hop.md`, danh mục mã thông báo (mã của Session đánh dấu
không còn dùng). Code: `MeetingSession` entity/config/migration, `meetingSessions.ts` ở FE.

### 2. Bỏ biểu quyết đặc biệt (time-boxed, quorum, Resolution bất biến)

eCabinet biểu quyết ngay trong cuộc họp: tạo, mở, bỏ phiếu, đóng, rồi **chủ trì ghi kết luận**
Thông qua / Không thông qua / Hoãn. Không có phiếu trước họp hay sau họp, không có quorum, không có
đối tượng "Resolution" riêng. BF đã bỏ "Shared Voting Sub-flow", nhánh vote trước họp (BF-02) và sau
họp (BF-04).

**Không UC nào bắt buộc phải xoá** — UC-70 → UC-76 vẫn dùng cho biểu quyết trong họp, chỉ sửa nội dung:

| UC | Xử lý đề xuất |
|---|---|
| UC-70 View Meeting Votes | Giữ |
| UC-71 Create / Configure Vote | Giữ; bỏ cấu hình quorum và deadline time-boxed |
| UC-72 Open / Publish Vote | Đổi tên thành **Open Vote**; chỉ mở khi cuộc họp `InProgress` |
| UC-73 Cast Vote | Giữ |
| UC-74 View Vote Status | Giữ; bao gồm xem kết quả đếm phiếu |
| UC-75 Close Vote | Giữ |
| UC-76 View Resolution | **PO cần chốt**: đổi thành **Record Vote Conclusion** (Chair ghi Thông qua / Không thông qua / Hoãn — bước có trong eCabinet mà danh mục đang thiếu), hoặc bỏ hẳn vì xem kết quả đã nằm trong UC-74 |

Tài liệu cần sửa: Report 1 (FE-12 bỏ "time-boxed vote… before or after", "quorum rule",
"cannot be altered"; GAP-05 "immutable record of resolutions"; FE-16 "resolutions"), Report 2
(iteration voting: "both voting modes", "resolution"), Report 3 §12, Master UC Inventory (FE-12,
dòng "Resolution là business outcome…", luồng lifecycle "→ Resolution"), FE-13 (biên bản lấy
"kết quả biểu quyết" thay cho "Resolution"), `04-luong-nghiep-vu-chinh.md` (mục 3.8, 4, 6.5).

### 3. Các thay đổi khác trong BF

- **Audio:** Secretary tự upload (UC-62), không còn vai trò Audio Uploader — khớp Inventory.
- **Dời lịch sau Published (Reschedule):** Organizer chỉ đổi giờ, địa điểm, link; hệ thống báo lại
  cho người được mời. Theo eCabinet. **PO cần chốt** người được dời lịch (Organizer hay cả Chair).
- **Yêu cầu sửa biên bản (UC-93):** Secretary sửa → hệ thống huỷ các chữ ký đã có → gửi ký lại.

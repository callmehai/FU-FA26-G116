# 14 · Use Case Diagram theo Actor

Nguồn: [Master Use Case Inventory](../01-requirements/00-master-use-case-inventory.md), đồng bộ với bản
02/11/2026 (81 UC chính UC-01 → UC-81 và UC-34b, đã gom UC nhỏ). Cùng bộ hình dùng trong Report 3 §I.4.3.
Đây là nguồn local; thư mục `UC diagram` trên Drive chỉ nhận bản đẩy lên theo
[DIAGRAM_DRIVE_SYNC.md](../DIAGRAM_DRIVE_SYNC.md).

[`trustcabinet-use-cases.drawio`](trustcabinet-use-cases.drawio) là file editable duy nhất, mỗi actor một trang
(System Admin, Meeting Organizer, Meeting Approver, Chair, Secretary, Participant, Guest). PNG xuất từ từng trang.

| Actor | Ảnh |
|---|---|
| System Admin | [uc-system-admin.png](uc-system-admin.png) |
| Meeting Organizer | [uc-meeting-organizer.png](uc-meeting-organizer.png) |
| Meeting Approver | [uc-meeting-approver.png](uc-meeting-approver.png) |
| Chair | [uc-chair.png](uc-chair.png) |
| Secretary | [uc-secretary.png](uc-secretary.png) |
| Participant | [uc-participant.png](uc-participant.png) |
| Guest | [uc-guest.png](uc-guest.png) |

**Quy ước:** nhãn UC trên hình là tên tiếng Anh của UC gom (ví dụ *Manage User Accounts*, *Manage Meeting Room
Controls*), không ghi số; tra số ở inventory. Quan hệ `<<extend>>` / `<<include>>` theo inventory §1 và §4. Bản
cũ theo baseline 05/10/2026 (94 UC, file `.drawio` + `.pdf` riêng từng actor) đã được thay thế ngày 10/10/2026.
Id của ô UC trong file `.drawio` vẫn là số cũ (`uc-45` là *Join Meeting Room*, tức UC-37); UC thêm từ 02/11/2026 dùng
id có tên (`uc-handover-chair`, `uc-my-voice-id`…) để không trùng số cũ.

## Đồng bộ với inventory (02/11/2026)

FE-12 theo luồng duyệt–ban hành mới (SCRUM-33, 11/11/2026; khớp `MinutesService.SubmitAsync/ApproveAsync/ReturnAsync/CorrectAsync`):
Thư ký giữ *Generate*, *Edit*, *Submit Meeting Minutes for Approval* (UC-61) và *Correct Issued Meeting Minutes* (UC-83),
bỏ *Submit … for Signing*, *Sign*, *Finalize*, *Request Revision*;
Chủ tọa có *Approve and Issue Meeting Minutes* (UC-64), *Return Meeting Minutes for Revision* (UC-65) và UC-83, bỏ *Sign*;
Người dự họp chỉ còn *View Meeting Minutes*. *View Signature Status & Audit Trail* giữ nguyên (placeholder UC-63, SCRUM-37).

Trước đợt này file còn ở baseline 10/10/2026 (70 UC). Đã bổ sung:

- **UC mới:** UC-34b (System Admin); UC-73, UC-74, UC-76, UC-77, UC-78, UC-79, UC-80, UC-81 và UC-75 vào trang
  các actor có trong cột Actor của inventory. UC-81 có mặt trên cả 6 trang actor người nội bộ (kể cả Meeting Approver).
- **FE-16 (SCRUM-137/SCRUM-142):** *Find Meeting Time* và *Manage Calendar Sharing* trên trang Meeting Organizer;
  *Manage Calendar Sharing* `<<extend>>` *Find Meeting Time* (ở mức UC gom: UC-84a `<<extend>>` UC-85a).
  Các UC con 84b/84c/84d/84e là thao tác trong cùng oval chia sẻ, không vẽ oval riêng.
- **Đổi tên theo FE-09/FE-10:** *View & Play Meeting Audio* → *View & Play Meeting Recordings* (UC-43),
  *Upload Meeting Audio* → *Record Room Audio* (UC-44), *View Speaker-Attributed Transcript* → *View Meeting
  Transcript* (UC-45).
- **Actor còn thiếu trên trang:** UC-27 và UC-30 (Chair, Participant, Guest), UC-55 (Chair, Secretary), UC-45
  (Chair, Participant).
- **Hai actor ngoài mới:** *Resend Email* (UC-04, UC-07, UC-20, UC-61, UC-75) và *Cloudflare R2* (UC-36, và UC-33 trên
  trang Chair, Secretary). Hai actor này luôn nằm ngoài khung hệ thống.
- **Quan hệ mới:** UC-73 `<<extend>>` UC-27; UC-75 `<<extend>>` UC-16; UC-76 `<<extend>>` UC-53; UC-77 `<<extend>>`
  UC-26; UC-78, UC-79 `<<extend>>` UC-37; UC-80 `<<extend>>` UC-37 và UC-43a; UC-81, UC-34b độc lập.

Chỗ hình khác chữ trong inventory, để BA chốt:

- **UC-74 → UC-55:** inventory chỉ ghi "được gọi trước UC-55". Hình vẽ `Cast Vote` `<<include>>` `Confirm Document
  Read` vì BR-289/BR-292 bắt buộc xác nhận đọc trước khi gửi phiếu (khi phiếu có tài liệu). Inventory §1 lại dặn không
  dùng `include` cho UC "tạo dữ liệu cho UC sau"; nếu BA muốn chặt hơn thì đổi quan hệ này.
- **UC-45 → UC-43a:** inventory ghi UC-45 `<<extend>>` UC-43a. Cạnh có sẵn trên trang Secretary từng nối UC-45 với
  UC-16; đã nối lại sang *View & Play Meeting Recordings*, các trang Chair và Participant vẽ theo cách đó.
- **Thư ký ↔ *Create / Configure Vote* (UC-53):** hình nối Secretary với UC-53 vì code cho Chair **và** Secretary
  tạo, sửa, xoá phiếu nháp (`VoteRules.CanManageDraft`, khi họp `Approved`/`Published`/`InProgress`); mở, đóng phiếu
  và ghi kết luận vẫn chỉ Chair. Inventory và đặc tả UC-53 còn ghi Chair, BA chỉnh ở bước BA Final Review.

Đã vẽ thêm theo cột Actor của inventory: UC-35 (*Comment on Meeting Document*, `<<extend>>` UC-32) trên trang System
Admin và Meeting Approver; UC-46 (*Edit Transcript & Speaker Attribution*, `<<extend>>` *View Meeting Transcript*) trên
trang Chair. Đặc tả UC-46a chỉ ghi Secretary, nên Chair có thể cần BA chốt lại.

Chưa vẽ vì inventory và đặc tả không khớp nhau:

- UC-45 và UC-78 trên trang Meeting Organizer: đặc tả ghi Primary Actor là Chair, Secretary, Participant (UC-78 thêm
  Guest), không có Organizer.

Xuất lại PNG sau khi sửa: mở `.drawio` bằng draw.io, *File → Export as → PNG* từng trang, hoặc
`draw.io -x -f png -p <số trang> -o uc-<actor>.png trustcabinet-use-cases.drawio`.

Trên Drive, ảnh giữ tên `trustcabinet-use-cases-<Actor>.drawio.png`; script map từ `uc-<actor>.png` sang
tên đó, nên không cần đổi tên file local.

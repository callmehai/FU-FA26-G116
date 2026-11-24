# 14 · Use Case Diagram theo Actor

Nguồn: [Master Use Case Inventory](../01-requirements/00-master-use-case-inventory.md), đồng bộ với bản
02/11/2026 (81 UC chính UC-01 → UC-81 và UC-34b theo số cũ, đã gom UC nhỏ; số mới ở [bảng 10](../01-requirements/10-bang-anh-xa-uc-dot-1.md)). Cùng bộ hình dùng trong Report 3 §I.4.3.
Đây là nguồn local; thư mục `UC diagram` trên Drive chỉ nhận bản đẩy lên theo
[DIAGRAM_DRIVE_SYNC.md](../DIAGRAM_DRIVE_SYNC.md).

[`trustcabinet-use-cases.drawio`](trustcabinet-use-cases.drawio) là file editable duy nhất, mỗi actor một trang
(System Admin, Member, Approver, Chair, Secretary, Participant, Guest). PNG xuất từ từng trang.

| Actor | Ảnh |
|---|---|
| System Admin | [uc-system-admin.png](uc-system-admin.png) |
| Member | [uc-member.png](uc-member.png) |
| Approver | [uc-approver.png](uc-approver.png) |
| Chair | [uc-chair.png](uc-chair.png) |
| Secretary | [uc-secretary.png](uc-secretary.png) |
| Participant | [uc-participant.png](uc-participant.png) |
| Guest | [uc-guest.png](uc-guest.png) |

**Quy ước:** nhãn UC trên hình là tên tiếng Anh của UC gom (ví dụ *Manage User Accounts*, *Manage Meeting Room
Controls*), không ghi số; tra số ở inventory. Quan hệ `<<extend>>` / `<<include>>` theo inventory §1 và §4. Bản
cũ theo baseline 05/10/2026 (94 UC, file `.drawio` + `.pdf` riêng từng actor) đã được thay thế ngày 10/10/2026.
Id của ô UC trong file `.drawio` vẫn là số cũ (`uc-45` là *Join Meeting Room*, tức UC-43); UC thêm từ 02/11/2026 dùng
id có tên (`uc-handover-chair`, `uc-my-voice-id`…) để không trùng số cũ.

## Theo UML chặt (24/11/2026)

Chỉ giữ `<<extend>>` khi UC mở rộng chèn hành vi vào lúc UC nền đang chạy và không phải mục tiêu riêng của actor
([inventory §1](../01-requirements/00-master-use-case-inventory.md#1-quy-ước-quan-hệ-uml)). Còn 7 quan hệ: *Keep Meeting in a
Floating Window*, *Register to Speak*, *Manage My Recorded Speech* `<<extend>>` *Join Meeting Room*;
*Manage My Recorded Speech* `<<extend>>` *View & Play Meeting Recordings*; *Comment on Meeting Document* `<<extend>>` *View
Meeting Documents*; *View Participant Response Status*
`<<extend>>` *View Meeting Participants*; *Mark Notifications as Read* `<<extend>>` *View Notifications*.

- *Confirm Document Read* (UC-42) nối thẳng actor (25/11/2026, trước là `extend` *Cast Vote*): người bỏ phiếu xác nhận
  đọc từ danh sách tài liệu, trước khi bỏ phiếu; UC-65 giữ điều kiện ở PRE-5. Trên trang Chair, Secretary, Participant,
  Guest ô này chuyển vào cột phải, ngay dưới *Cast Vote*.
- *Edit Transcript & Speaker Attribution* (UC-55) và *Manage Vote Documents* (UC-68) nối thẳng actor, cùng dạng với *Edit
  Meeting Summary* và *Edit Meeting Minutes* (sửa theo review PR #531); trên trang Chair và Secretary hai ô được chuyển vào
  cột phải, ngay dưới *View Meeting Transcript* và *Create / Configure Vote*.
- Bỏ mọi `extend` kiểu "mở thao tác từ màn hình" (sửa, gửi duyệt, công bố, quản lý…) và kiểu chuyển màn hình / tab
  (danh sách → chi tiết, chi tiết → các tab, thông báo → đối tượng). Các UC đó nối thẳng với actor của trang.
- *Record Room Audio* nối thẳng Thư ký (trước là `extend` *View & Play Meeting Recordings*): họp trực tiếp ghi âm mà không
  vào phòng trực tuyến (UC-52a PRE-2, UC-52c), nên cũng không `extend` *Join Meeting Room* (sửa theo review PR #531). *Take
  Meeting Notes* nối thẳng Thư ký. *Cast Vote* `<<include>>` *Confirm Document Read* đổi thành
  *Confirm Document Read* `<<extend>>` *Cast Vote* (ngày 25/11/2026 đổi tiếp thành nối thẳng actor, xem trên). Hai mục "để BA chốt" về UC-42 → UC-65
  và UC-54 → UC-51a ở dưới vì vậy đã xong.
- Bố cục mới, mọi trang giống nhau, để ảnh gần vuông và đọc được khi đưa vào Report: actor của trang vẽ **hai lần**, bên
  trái và bên phải khung hệ thống (UML cho phép một actor xuất hiện nhiều lần; bản bên phải có id `<id>-2`). UC nối thẳng
  chia hai cột theo số UC — cột trái đọc từ trên xuống, cột phải đọc từ dưới lên, nên nhóm phòng họp và bản ghi nằm cạnh
  *Join Meeting Room* — mỗi cột nối với actor cùng bên. UC mở rộng ở cột giữa, gần UC nền. Actor ngoài (OAuth Provider,
  Resend Email, File Storage) đặt ngoài khung, gần UC của nó. Id ô giữ nguyên; cạnh `extend` bị bỏ thì xoá, cạnh nối actor
  thêm mới có id `assoc-<id actor>-<id UC>`, ví dụ `assoc-actor-uc-62`.
- HuyNQ chỉnh tay bố cục trong draw.io (24/11/2026): chữ to hơn (14–16), dời ô cho thoáng, nối thẳng actor ngoài. Sửa tiếp
  bằng tay trong draw.io rồi xuất lại PNG theo lệnh ở cuối file và thay ảnh trong Report 3 §I.4.3.

## Cập nhật 24/11/2026

Đổi tên actor theo Report 3: Meeting Organizer → **Member**, Meeting Approver → **Approver** (tên trang, nhãn actor,
tiêu đề khung; id trang `meeting-organizer`, `meeting-approver` giữ nguyên). Ảnh đổi tên thành `uc-member.png`,
`uc-approver.png`. Xuất lại PNG các trang System Admin, Member, Approver, Chair, Secretary, Guest theo bố cục đã sửa
trong draw.io; trang Participant không đổi nên giữ ảnh cũ (sau đó vẫn xuất lại khi chuyển sang UML chặt, xem trên). Số UC trong README theo [bảng 10](../01-requirements/10-bang-anh-xa-uc-dot-1.md).

## Đồng bộ với inventory (02/11/2026)

FE-12 theo luồng duyệt–ban hành mới (SCRUM-33, 11/11/2026; khớp `MinutesService.SubmitAsync/ApproveAsync/ReturnAsync/CorrectAsync`):
Thư ký giữ *Generate*, *Edit*, *Submit Meeting Minutes for Approval* (UC-72) và *Correct Issued Meeting Minutes* (UC-75),
bỏ *Submit … for Signing*, *Sign*, *Finalize*, *Request Revision*;
Chủ tọa có *Approve and Issue Meeting Minutes* (UC-73), *Return Meeting Minutes for Revision* (UC-74) và UC-75, bỏ *Sign*;
Người dự họp chỉ còn *View Meeting Minutes*. *View Signature Status & Audit Trail* đã gỡ khỏi cả 6 trang nội bộ (PO 14/11/2026 bỏ ký số kể cả mô phỏng, SCRUM-37 đóng — UC-62 cũ, UC-63 cũ xoá hẳn).

Trước đợt này file còn ở baseline 10/10/2026 (70 UC). Đã bổ sung:

- **UC mới:** UC-37a (System Admin); UC-34, UC-42, UC-68, UC-27, UC-49, UC-50, UC-53, UC-56, UC-22 vào trang
  các actor có trong cột Actor của inventory. UC-56 có mặt trên cả 6 trang actor người nội bộ (kể cả Approver).
  **UC-57** *Collect Voice Samples for Attendees* (SCRUM-152) chỉ có trên trang Secretary, độc lập.
- **FE-16 (SCRUM-137/SCRUM-142):** *Find Meeting Time* và *Manage Calendar Sharing* trên trang Member;
  *Manage Calendar Sharing* `<<extend>>` *Find Meeting Time* (ở mức UC gom: UC-80a `<<extend>>` UC-81a).
  Các UC con 84b/84c/84d/84e là thao tác trong cùng oval chia sẻ, không vẽ oval riêng.
- **Đổi tên theo FE-09/FE-10:** *View & Play Meeting Audio* → *View & Play Meeting Recordings* (UC-51),
  *Upload Meeting Audio* → *Record Room Audio* (UC-52), *View Speaker-Attributed Transcript* → *View Meeting
  Transcript* (UC-54).
- **Actor còn thiếu trên trang:** UC-29, UC-32 (Chair, Participant, Guest), UC-65 (Chair, Secretary), UC-54
  (Chair, Participant).
- **Hai actor ngoài mới:** *Resend Email* (UC-04, UC-07, UC-19, UC-72, UC-22) và *File Storage* (UC-39, và UC-36 trên
  trang Chair, Secretary; trước 24/11/2026 tên *Cloudflare R2*, ADR-0084). Hai actor này luôn nằm ngoài khung hệ thống.
- **Quan hệ mới:** UC-34 `<<extend>>` UC-29; UC-22 `<<extend>>` UC-15; UC-68 `<<extend>>` UC-63; UC-27 `<<extend>>`
  UC-26; UC-49, UC-50 `<<extend>>` UC-43; UC-53 `<<extend>>` UC-43, UC-51a; UC-56, UC-37a độc lập.

Chỗ hình khác chữ trong inventory, để BA chốt:

- **UC-42 → UC-65:** inventory chỉ ghi "được gọi trước UC-65". Hình vẽ `Cast Vote` `<<include>>` `Confirm Document
  Read` vì BR-094, BR-095 và UC-65 bắt buộc xác nhận đọc trước khi gửi phiếu (khi phiếu có tài liệu). Inventory §1 lại dặn không
  dùng `include` cho UC "tạo dữ liệu cho UC sau"; nếu BA muốn chặt hơn thì đổi quan hệ này.
- **UC-54 → UC-51a:** inventory ghi UC-54 `<<extend>>` UC-51a. Cạnh có sẵn trên trang Secretary từng nối UC-54 với
  UC-15; đã nối lại sang *View & Play Meeting Recordings*, các trang Chair và Participant vẽ theo cách đó.
- **Thư ký ↔ *Create / Configure Vote* (UC-63):** hình nối Secretary với UC-63 vì code cho Chair **và** Secretary
  tạo, sửa, xoá phiếu nháp (`VoteRules.CanManageDraft`, khi họp `Approved`/`Published`/`InProgress`); mở, đóng phiếu
  và ghi kết luận vẫn chỉ Chair. Inventory và đặc tả UC-63 còn ghi Chair, BA chỉnh ở bước BA Final Review.

Đã vẽ thêm theo cột Actor của inventory: UC-38 (*Comment on Meeting Document*, `<<extend>>` UC-35) trên trang System
Admin và Approver; UC-55 (*Edit Transcript & Speaker Attribution*, `<<extend>>` *View Meeting Transcript*) trên
trang Chair. Đặc tả UC-55a chỉ ghi Secretary, nên Chair có thể cần BA chốt lại.

Chưa vẽ vì inventory và đặc tả không khớp nhau:

- UC-54, UC-49 trên trang Member: đặc tả ghi Primary Actor là Chair, Secretary, Participant (UC-49 thêm
  Guest), không có Member.

Xuất lại PNG sau khi sửa: mở `.drawio` bằng draw.io, *File → Export as → PNG* từng trang, hoặc
`draw.io -x -f png -p <số trang> -o uc-<actor>.png trustcabinet-use-cases.drawio`.

Trên Drive, ảnh giữ tên `trustcabinet-use-cases-<Actor>.drawio.png`; script map từ `uc-<actor>.png` sang
tên đó, nên không cần đổi tên file local.

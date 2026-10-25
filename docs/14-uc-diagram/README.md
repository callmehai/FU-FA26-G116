# 14 · Use Case Diagram theo Actor

Nguồn: [Master Use Case Inventory](../01-requirements/00-master-use-case-inventory.md), baseline
10/10/2026 (70 UC, đã gom UC nhỏ). Cùng bộ hình dùng trong Report 3 §I.4.3. Đây là nguồn local;
thư mục `UC diagram` trên Drive chỉ nhận bản đẩy lên theo
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

Xuất lại PNG sau khi sửa: mở `.drawio` bằng draw.io, *File → Export as → PNG* từng trang, hoặc
`draw.io -x -f png -p <số trang> -o uc-<actor>.png trustcabinet-use-cases.drawio`.

Trên Drive, ảnh giữ tên `trustcabinet-use-cases-<Actor>.drawio.png`; script map từ `uc-<actor>.png` sang
tên đó, nên không cần đổi tên file local.

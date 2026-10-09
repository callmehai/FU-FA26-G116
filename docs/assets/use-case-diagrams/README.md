# Use Case Diagram theo Actor

Nguồn: [Master Use Case Inventory](../../01-requirements/00-master-use-case-inventory.md), baseline 05/10/2026. Nhãn trong sơ đồ dùng tiếng Anh. Mỗi actor có đúng một sơ đồ và một ảnh duy nhất chứa toàn bộ UC của actor đó; không tách trang theo Feature. Bố cục theo mẫu tham khảo: actor bên ngoài, system boundary bao quanh các UC, quan hệ `<<extend>>` / `<<include>>` nét đứt. PNG và PDF đều là toàn bộ sơ đồ.

**File Draw.io tổng hợp:** [trustcabinet-use-cases.drawio](trustcabinet-use-cases.drawio) chứa đủ 7 sơ đồ actor dưới dạng 7 trang, giữ nguyên từng sơ đồ. Dùng file này để mở và chỉnh tất cả trang trong cùng một tài liệu. Các file `.drawio` theo từng actor bên dưới vẫn được giữ riêng; thay đổi trong file tổng hợp không tự đồng bộ ngược sang chúng.

| Actor | PDF đầy đủ | Sơ đồ editable | Ảnh đầy đủ (PNG) |
|---|---|---|
| System Admin | [PDF](uc-system-admin.pdf) | [uc-system-admin.drawio](uc-system-admin.drawio) | [PNG](uc-system-admin.png) |
| Meeting Organizer | [PDF](uc-meeting-organizer.pdf) | [uc-meeting-organizer.drawio](uc-meeting-organizer.drawio) | [PNG](uc-meeting-organizer.png) |
| Meeting Approver | [PDF](uc-meeting-approver.pdf) | [uc-meeting-approver.drawio](uc-meeting-approver.drawio) | [PNG](uc-meeting-approver.png) |
| Chair | [PDF](uc-chair.pdf) | [uc-chair.drawio](uc-chair.drawio) | [PNG](uc-chair.png) |
| Secretary | [PDF](uc-secretary.pdf) | [uc-secretary.drawio](uc-secretary.drawio) | [PNG](uc-secretary.png) |
| Participant | [PDF](uc-participant.pdf) | [uc-participant.drawio](uc-participant.drawio) | [PNG](uc-participant.png) |
| Guest | [PDF](uc-guest.pdf) | [uc-guest.drawio](uc-guest.drawio) | [PNG](uc-guest.png) |

**Quy ước:** quan hệ `<<extend>>` / `<<include>>` đi theo baseline. Actor được nối trực tiếp vào các UC độc lập/base; các UC mở rộng nối với UC nền bằng quan hệ UML. UC có actor mô tả chung như “internal user” hoặc “user có quyền” được ánh xạ theo role/capability. Google Identity Provider được nối vào UC-02; AI / Speech Processing Provider được nối vào UC-90. Master inventory chưa chỉ rõ UC nào gọi Email / SMTP Service, nên actor này chưa được gắn vào sơ đồ.


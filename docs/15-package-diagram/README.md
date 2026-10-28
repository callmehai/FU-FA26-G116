# 15 · Package Diagram

Package diagram của TrustCabinet, vẽ theo **code hiện có** trên `main` (28/10/2026), không phải
thiết kế mục tiêu. Phụ thuộc lấy từ chính source:

- backend và tests: `ProjectReference` trong `.csproj` và các dòng `using TrustCabinet.*` theo
  namespace (bỏ qua `bin/`, `obj/`, `Migrations/`);
- frontend: các `import` tương đối, kể cả `import()` của route lazy, gom theo thư mục cấp một của
  `frontend/src` (riêng `features/` tách theo từng feature).

Mỗi góc nhìn là một cặp file cùng tên: `.drawio` là source editable, `.png` xuất từ đúng source đó.

| Góc nhìn | Nội dung | Source / Ảnh |
|---|---|---|
| Tổng quan | `frontend/src` theo lớp, ba project backend, project test và phụ thuộc giữa chúng | [drawio](package-tong-quan.drawio) · [PNG](package-tong-quan.png) |
| Backend | Package con của `TrustCabinet.Web`, `.Infrastructure`, `.Domain`; class ở namespace gốc của Web và Infrastructure | [drawio](package-backend.drawio) · [PNG](package-backend.png) |
| Domain | Phụ thuộc giữa các package nghiệp vụ trong `TrustCabinet.Domain` | [drawio](package-domain.drawio) · [PNG](package-domain.png) |
| Frontend | `frontend/src` với 14 feature trong `features/` | [drawio](package-frontend.drawio) · [PNG](package-frontend.png) |

## Quy ước

- Mũi tên nét đứt đầu mở là phụ thuộc UML. `«import»` = `ProjectReference`; `«use»` = dùng trực
  tiếp namespace của project khác mà không tham chiếu project đó (ví dụ Web → Domain đi qua
  Infrastructure); `«use» HTTP · SignalR` = `api/` gọi `/api` và `/hubs` của backend.
- Mũi tên không nhãn là phụ thuộc giữa các package trong cùng một project/thư mục.
- Mũi tên gắn vào khung một package bao gồm mọi package con bên trong.
- Package vẽ ngoài package chứa nó thì ghi tên đầy đủ `chứa::tên` (ví dụ `backend::TrustCabinet.Web`
  trên trang Frontend).
- Mũi tên kết thúc ở thân package, không cắm vào tab.

## Những gì cố ý không vẽ

Để hình đọc được, các phụ thuộc sau tồn tại trong code nhưng không có mũi tên:

- **Trang Domain** không vẽ phụ thuộc tới `Entities` và `Enums`. Package dùng `Entities`: Access,
  Agenda, Authentication, Calls, Documents, Meetings, Notifications, Participants, Recordings,
  Transcription, Users, VoiceId. Package dùng `Enums`: Access, Agenda, Attendance, Authentication,
  Calls, Documents, Entities, Meetings, Participants, Recordings, Users. `Audit` không phụ thuộc
  package nào.
- **Trang Backend** chỉ vẽ phụ thuộc giữa các package trong Infrastructure và từ `Endpoints` sang
  Infrastructure; từng package Infrastructure → Domain được gộp vào mũi tên `«import»` giữa hai
  khung. Phụ thuộc giữa các class ở namespace gốc của Web (hub, worker, publisher) với các project
  khác cũng gộp vào mũi tên giữa khung. `MeetingHub`, `NotificationHub` và `NotificationWorker`
  dùng `Endpoints`; phụ thuộc này cũng không vẽ.
- **Trang Frontend** không vẽ phụ thuộc giữa các feature với nhau (`meetings`, `calls` dùng gần
  như mọi feature khác; mọi feature dùng `auth`). `main.tsx` và `styles/` (CSS) không có trên hình.
- Thư viện ngoài (NuGet, npm) không có trên hình; danh sách ở các `.csproj` và `frontend/package.json`.

## Sửa và xuất lại

Sửa trực tiếp trên `.drawio` theo [quy tắc chỉnh draw.io](../../DRAWIO_EDITING_RULES.md) và
[drawio-skill](../../.agents/skills/drawio-skill/SKILL.md). Khi thêm hoặc bỏ project, namespace hay
thư mục `features/` thì cập nhật hình và mục "Những gì cố ý không vẽ" ở trên. Xuất lại PNG của
đúng file vừa sửa:

```bash
draw.io -x -f png -s 2 -o package-backend.png package-backend.drawio
```

Bản trên Drive nằm ở thư mục `Package diagram`, được tải tay lên; `scripts/push_diagrams.py` chưa
có nhóm cho thư mục này (xem [DIAGRAM_DRIVE_SYNC.md](../DIAGRAM_DRIVE_SYNC.md)).

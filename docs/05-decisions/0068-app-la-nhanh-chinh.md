# ADR-0068: `app` là nhánh chính, `main` đóng băng

- **Trạng thái:** Đề xuất — PO (Hải) 20/11/2026
- **Ngày:** 2026-11-20
- **Người quyết định:** Hải (PO)
- **Thay thế một phần:** [ADR-0065](0065-hai-nhanh-hai-server.md) — mục 5 ("`main` chảy sang `app`")
- **Liên quan:** ADR-0064 (backend bốn project), ADR-0047 (deploy an toàn)

## Bối cảnh

ADR-0065 giữ hai nhánh: `main` là bản demo ba project, `app` là backend bốn project, và mọi sửa lỗi làm trên `main`
rồi merge sang `app`. Mỗi lần merge phải gỡ xung đột ở các file đã chuyển project (20/11 đã ba lần: #434, #439, #443,
và một lần cuối cho #442). Buổi demo với giảng viên ngày 20/11 đã xong; PO quyết định làm tiếp trên `app`.

## Quyết định

1. **`app` là nhánh chính**: nhánh mặc định của repo trên GitHub, mọi nhánh mới tách từ `app`, mọi PR vào `app`.
2. **`main` đóng băng** ở commit merge #442 (`0bbab4d1`): không nhận thay đổi mới, không còn PR merge `main` vào `app`.
   Server demo (trustcabinet.workspace-hub.space) vẫn chạy bản đó cho tới khi xoá máy.
3. Các quy tắc trước đây gắn với `main` (migration bất biến khi đã lên nhánh, mã MSG không đánh số lại, ticket chỉ
   `Done` khi nội dung đã lên nhánh) nay áp dụng cho `app`.
4. CI và deploy giữ nguyên: push lên `app` vẫn tự deploy lên server `app`; `deploy.yml` vẫn cho `main` deploy nếu cần
   khôi phục bản demo.

## Hệ quả

- Hết việc sửa hai lần và gỡ xung đột giữa hai nhánh.
- Tài liệu, báo cáo và skill nhắc `main` như nhánh làm việc được đổi sang `app` (AGENTS.md, DEVELOPMENT_WORKFLOW.md,
  `.claude/skills/`, `deploy/README.md`).
- Báo cáo nộp hội đồng mô tả backend bốn project theo `app`.

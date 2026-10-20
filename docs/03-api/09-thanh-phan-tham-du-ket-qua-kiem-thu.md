# SCRUM-20 / UC-73 — kết quả kiểm thử Backend

## Trạng thái ngày 21/10/2026

Báo cáo do AI hỗ trợ soạn cho thay đổi PO Hải chốt ngày 20/10/2026. Kiểm thử working tree trên branch `feat/scrum20-Backend-Invite-participants-and-acept`, nền HEAD `adca53b`. Các thay đổi mới chưa commit/push; CI xanh của HEAD cũ không xác nhận working tree này. Chưa merge hoặc deploy.

## Kết quả thực chạy

| Kiểm tra | Kết quả |
|---|---|
| Backend Release build, `-warnaserror` | Pass, 0 warning/error |
| Toàn bộ backend suite Release | **1.114 pass, 0 fail, 0 skip** |
| Frontend `npm run lint` | Pass |
| Frontend `npm run build` | Pass |
| EF `migrations has-pending-model-changes` | Không có pending model changes |
| `git diff --check` | Pass |

Suite dùng PostgreSQL 17 và Redis 8 riêng tại localhost 5439/6389; không dùng database server test. TRX: `%TEMP%/scrum20-po-results/po-full.trx`; log: `%TEMP%/scrum20-full.log`. Không thay đổi source frontend.

## Phạm vi regression

- Tạo meeting cùng roster trong transaction: đúng một Chair, user active/không trùng, role hợp lệ, revision 0; lỗi không lưu dữ liệu dở dang.
- Chair/Secretary quản lý roster; Chair đọc từ Draft; người đọc không có vai trò quản lý nhận 403/MSG85 khi mutation.
- Không gỡ Chair; thay Chair atomic có confirmation; UC-73 bàn giao từ Published, chặn Secretary thay Chair active; ngoại lệ Chair inactive; người nhận không phải Secretary/inactive/removed.
- Response thuộc chính người mời, không đổi attendance; totals toàn roster độc lập search/paging; strict JSON từ chối additionalPermissions; endpoint đã bỏ không còn dùng.
- Concurrency/revision/rollback, audit chỉ ghi dòng thay đổi, durable dispatch publication/reschedule/role change và deduplication.
- SignalR thực qua long polling: người đọc trước/sau mutation cùng nhận participantsChanged, kể cả người bị gỡ; rollback không phát event; publisher lỗi không rollback dữ liệu đã commit.
- Guest dependency backoff 1m/5m/30m/6h không khóa meeting khi chưa sẵn sàng, tách dependency retry khỏi technical attempt.
- Migration preflight, constraints, backfill Chair khi thiếu (creator đã/chưa có assignment), bảo toàn response/attendance, downgrade/upgrade và guard tránh mất dữ liệu mới.

## Bàn giao và giới hạn

API dùng MSG275/MSG276; lỗi roster khi tạo dùng MSG277. UC-73 dùng MSG256/MSG257/MSG259; MSG152/MSG153 ngừng dùng. Đã bỏ grants, Secretary recovery và đường invitation queue cũ.

Cách test thủ công gồm 15 kịch bản trong [bàn giao FE/Tester](09-thanh-phan-tham-du-ban-giao.md); request mẫu ở [Postman](postman/scrum20-participants.postman_collection.json). Frontend cần cập nhật payload tạo meeting, context/roster và xử lý participantsChanged trước nghiệm thu tích hợp.

Chưa kiểm SMTP thật, Guest OTP/link/session production (SCRUM-59), room disconnect (SCRUM-41), UI end-to-end hoặc dữ liệu server dùng chung. Migration trong PR chưa lên main được viết lại cùng ID: database đã apply bản cũ không tự apply lại; dùng database thử mới hoặc phương án nâng cấp do owner quản lý. Không mặc định Down an toàn sau khi có dữ liệu mới.

Cần commit/push theo chỉ đạo owner, CI xanh cho commit mới và Hải review lại trước merge. UC/BR/sequence giữ nguyên để BA đối chiếu change note Jira tại Final Review.

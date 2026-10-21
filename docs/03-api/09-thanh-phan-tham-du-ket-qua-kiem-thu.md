# SCRUM-20 / UC-73 — kết quả kiểm thử Backend

## Trả lời lời mời theo AI-eCabinet + tích hợp FE (21/10/2026)

- Trả lời một lần (MSG285), Chủ tọa không trả lời (MSG286), bỏ `totals`, thêm `myResponse`/`canRespond` vào chi tiết và danh sách, `excludeDeclined` cho lịch, địa điểm Hybrid không có phòng chỉ ghi link.
- Full suite: **1.173 pass, 0 fail, 0 skip** trên PostgreSQL 17/Redis 8 local (thêm 3 test: trả lời một lần/Chủ tọa, lịch ẩn cuộc họp đã từ chối, địa điểm Hybrid). Build `-warnaserror` 0 warning/error; FE lint/build pass.
- Kiểm thử giao diện bằng Playwright (14 kịch bản): tạo họp có thành phần, trả lời trên chi tiết và trên lịch, Chủ tọa không được hỏi, thêm/đổi vai trò/giao Chủ tọa/gỡ, realtime giữa hai trình duyệt, 409 MSG275, lỗi tải 500 rồi "Thử lại", MSG277 hiện đúng dòng, thanh công cụ một dòng ở 1200px.

## Sửa tích hợp phòng và lookup — working tree trên 04d3a5c (21/10/2026)

- CallRepository dùng ActiveExpression cho cấp quyền phòng và danh sách đối soát; lịch sử Removed không cấp quyền, mời lại không lỗi nhiều assignment. Bỏ helper IsSecretary không còn dùng.
- Thêm GET /api/meetings/participant-options, quyền MeetingCreator kiểm tra lại từ database; user active, search/paging và chỉ id/fullName/email. Dùng MSG95/MSG276 hiện có, không thêm migration hay sửa frontend source.
- Full suite: **1.170 pass, 0 fail, 0 skip** trên PostgreSQL17/Redis8 riêng; TRX `%TEMP%/scrum20-final-fixes/full.trx`. Bao gồm 3 regression phòng và 1 test luồng lookup/tạo họp, query/paging/authorization.
- Release build -warnaserror: 0 warning/error. Frontend lint/build pass sau npm ci để đồng bộ dependency LiveKit từ main. Build FE còn cảnh báo kích thước chunk phòng họp. Không sửa package/lockfile.
- Published head 04d3a5c có [CI #370](https://github.com/callmehai/TrustCabinet/actions/runs/39586386830) pass. Các sửa đổi tích hợp/lookup này chưa commit/push, CI đó chưa bao gồm chúng. Sau bổ sung mapping lỗi đọc ở capability gate, chạy lại build và regression/catalô trên binary cuối.
- Chưa chạy SMTP/LiveKit thật, Guest production, UI E2E hoặc preflight database dùng chung. Vẫn giữ gate FE gửi participants trước merge/deploy phối hợp; không commit/push/merge trong phiên này.

Các mục bên dưới là kết quả lịch sử theo từng baseline, không phải trạng thái published hiện tại.


## Sửa review 21/10/2026 — working tree trên 487ca24

Báo cáo do AI hỗ trợ soạn. Phần sửa review này chưa commit/push; CI #354 chỉ xác nhận published head 487ca24. Không merge/deploy hoặc tác động database dùng chung.

- Full backend Release suite: **1.126 pass, 0 fail, 0 skip**, trên PostgreSQL17/Redis8 riêng. TRX `%TEMP%/scrum20-po-results/review2-full.trx`; log `%TEMP%/scrum20-review2-full.log`.
- Release build `-warnaserror`: 0 warning/error; frontend lint/build và diff check pass.
- Removed không materialize khi lời mời cùng owner chưa thành email; vẫn hỗ trợ initial delivery key của dữ liệu cũ. Test Guest chờ UC-04, Member thêm/gỡ ngay, đã materialize và legacy EmailMessage không có dispatch.
- Exception ở IsAvailableAsync/PrepareAsync (kể cả OperationCanceledException khi host chưa hủy) tăng attempt/backoff, Failed khi hết budget; dispatch sau vẫn materialize. Host cancellation propagate.
- Worker dùng try/catch riêng cho dispatch; lỗi không cản email queue ở scope mới. Không log exception message chứa credential.
- Regression khóa Chair giữa snapshot và FOR SHARE: được thay theo policy inactive mà không phát UPDATE users. Trùng Guest email trả errors.userIds.
- Dùng expression participant chung, identity/resolver Guest chung cho Agenda/Participants/hub; không thay quyền workflow/Agenda.

**Gate merge:** chờ FE do Hải triển khai gửi participants khi tạo meeting, đưa FE/BE lên main cùng lúc. Sau khi owner commit/push bản sửa cần CI mới và review lại. SMTP thật và Guest production chưa nghiệm thu; hướng dẫn ở [bàn giao](09-thanh-phan-tham-du-ban-giao.md).

## Kiểm thử bản PO trước review

Bản PO 20/10/2026 đã commit/push tại `487ca24dd4ad5368e5c74ace7aaacb66926b2c5d`; [CI #354](https://github.com/callmehai/TrustCabinet/actions/runs/39496645545) pass. Bảng bên dưới ghi kết quả của bản PO trước review. Phần sửa review mới được ghi riêng ở đầu báo cáo.

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

# SCRUM-20 — bàn giao Frontend và test thủ công

## Phần Backend đã có

Đọc [API contract](09-thanh-phan-tham-du.md) trước khi nối UI. Thực hiện types/API wrappers trong frontend, dùng client hiện có và relative URL. Backend task này không sửa source Frontend. Guest OTP/session production chưa có; xem [UC-04](09-thanh-phan-tham-du-guest-uc04.md).

FE cần xử lý:

1. Context trước, render controls theo flags. Tách roster/summary và không preload view bị cấm. System role không xác định quyền meeting.
2. Recovery cho Organizer có thể hiện dù roster bị404. Options mode RecoverSecretary chỉ chọn một user active. Reload context sau recovery.
3. Internal add nhiều user/shared role và Guest form riêng. Guest không có role dropdown; Confidential/Secret cảnh báo inline theo UC, không thêm dialog ngoài baseline.
4. Chair swap có confirmation outgoing/incoming, gửi atomic batch. Nếu mất Secretary cuối thì bổ sung replacement cùng batch. Không demote rồi promote bằng hai HTTP request.
5. Lưu ETag và If-Match ở wrapper. 409/MSG238 tải lại, để người dùng xác nhận; không tự resend payload cũ. Revision giữ dạng string.
6. Grant omit = giữ, [] = xóa, array = tập cuối. Hiện chỉ hai grant trong catalogue, đừng hardcode giới hạn vĩnh viễn.
7. Save thành công chỉ xác nhận dữ liệu đã lưu. Không báo “đã gửi email”. Sau mutation tải lại context vì người thao tác có thể tự mất quyền. Xóa cache PII khi401/403/404.
8. Accepted/Declined là phản hồi, không phải điểm danh. Chưa làm attendance, delegation, room SDK hoặc UI Guest giả.
9. Lỗi500/MSG95 khác empty list. Phân biệt loading, read error, denied, no-op và all-Pending. Summary-only không gọi roster để search. Match MSG, không match text.
10. Panel dùng chung theo ADR-0011, layout/skills frontend của repo. Route UI thiếu quyền trả404. Không thêm CORS/VITE_API_BASE_URL.

**Dependency FE cần xử lý riêng:** `npm ci`/lint/build pass nhưng `npm audit` báo `source-map-js` 1.2.1, mức high, [GHSA-68fv-2mgg-jv7q](https://github.com/advisories/GHSA-68fv-2mgg-jv7q). Bản vá1.2.2; FE owner cập nhật lockfile và kiểm lại chuỗi dependency. Backend task không chạy `npm audit fix` hoặc sửa source/lockfile Frontend.

## Chuẩn bị môi trường riêng

1. Chạy PostgreSQL/Redis và cấu hình connection strings, signing key theo README; không chép secrets vào Postman export hoặc commit.
2. Trước migration xem [preflight](09-thanh-phan-tham-du.md#6-migration). Nếu legacy Guest/delegation/multiple Chair bị chặn, báo người sở hữu DB; không xóa dữ liệu để qua migration.
3. `dotnet run --project backend/TrustCabinet.Web` trong Development sẽ migrate/seed theo cấu hình hiện có. Seed mới insert-only, không reset kịch bản đã sửa. Để `Email:Smtp:Enabled=false` trong bước test API; chỉ bật SMTP sandbox được nhóm cho phép khi test mail. Không gửi tới người thật.
4. Demo users có `thuky@trustcabinet.local`, `chutoa@trustcabinet.local`, `thanhvien@trustcabinet.local`; mật khẩu demo trong DevelopmentDataSeeder, chỉ dùng database giả. Đăng nhập qua `/api/auth/login`, giữ token ở local environment của Postman.
5. Import [collection](postman/scrum20-participants.postman_collection.json). `baseUrl=http://localhost:5225` chỉ là địa chỉ gọi trực tiếp từ công cụ test Backend, không dùng làm FE base URL. Điền `token` đúng actor, `meetingId`, `userId`, assignment IDs. Request Context tự lưu ETag; chạy lại sau mỗi mutation thành công.

Fixtures `[SCRUM20-DEV]`:

| ID suffix (prefix `20000000-0000-7000-8000-`) | State | Roster ban đầu |
|---|---|---|
| `000000000001` | Draft | Chair + Member; Organizer cần recovery Secretary |
| `000000000002` | Rejected | Như Draft |
| `000000000003` | Approved | Như Draft |
| `000000000004` | WaitingApproval | Chair + Secretary + Member, khóa sửa |
| `000000000005` | Published | Chair + Secretary + Member |
| `000000000006` | InProgress | Chair + Secretary + Member |
| `000000000007` | Ended | Chair + Secretary + Member, chỉ đọc |

Organizer là tài khoản thuky. Fixture seed không tạo Guest, credential, notification hoặc dispatch; khi bật SMTP, legacy reconciliation có thể bổ sung email cho fixture Published/InProgress. Nếu cần reset, dùng **database test mới**; không sửa lịch sử hoặc schema database nhóm bằng SQL.

## Các bước test API thủ công

| Bước | Thao tác | Kỳ vọng |
|---|---|---|
| 1 | Token thuky, meeting Draft, Context/Options | CanRecoverSecretary=true, mode RecoverSecretary; roster404 |
| 2 | Recovery userId của thuky, If-Match từ Context | 201/MSG96, revision tăng; recovery lần nữa400/MSG97 |
| 3 | Roster + GET own response ở Draft | Roster200, không field response/attendance; own response404 |
| 4 | Add internal user chưa current; thử ID trùng/inactive/unknown trong batch | Batch hợp lệ201; batch lỗi400/MSG97 không thêm một phần |
| 5 | Add Guest tên/email; thử email hoa thường trùng internal/Guest | Lần đầu201; trùng400/MSG97; không tạo User |
| 6 | Published, Member không grants đọc roster và summary | Cả hai404; own response200 |
| 7 | Secretary cấp riêng ViewInvitationResponses cho Member | Summary200, roster404; thêm q vào summary400/MSG239 |
| 8 | Thay grant bằng ViewMeetingParticipants | Roster200, summary404; roster không lộ attendance/response/grants của người khác |
| 9 | Member Accepted → Accepted → Declined | MSG105 → MSG154 → MSG105; no-op giữ respondedAt; attendance Unknown không đổi |
| 10 | Thử response Pending/Delegated, role trong body, sai casing/duplicate JSON | 400; không đổi persisted response |
| 11 | Chair replacement outgoing Chair → Member, incoming Member → Chair | Thiếu confirmation400/MSG101; đủ confirmation200/MSG100; chỉ một Chair |
| 12 | Remove Chair hoặc Secretary cuối ở Published | 409/MSG109; dùng atomic role batch để thay Secretary |
| 13 | Hai tab giữ cùng ETag rồi save lần lượt | Lần hai409/MSG238; không ghi đè |
| 14 | Gỡ Member đang Accepted và có grant | MSG103; Member mất Meeting/Agenda/roster/response/notification-target qua assignment; removal notification không link |
| 15 | Mời lại cùng Member | Assignment ID mới, Pending, grants rỗng; history cũ giữ phản hồi/điểm danh |
| 16 | Thử quản lý ở WaitingApproval/Ended, respond ở Ended | Quản lý409/MSG98; respond409/MSG107; view có quyền vẫn đọc Ended |
| 17 | Caller không liên quan / token hết hạn / account inactive | 404 / 401 / 403; không lộ roster/options |
| 18 | Bỏ If-Match; query lạ; text/plain; JSON >64KiB | 400/MSG239; 400/MSG239; 415/MSG34; 413/MSG87 |
| 19 | SMTP disabled, add vào Published; sau đó bật sandbox/worker | Domain lưu thành công; intent Pending rồi Materialized + EmailMessage; không gửi lại toàn roster |
| 20 | Revoke Guest trước materialize | Initial dispatch Suppressed, removal mail không link; Guest consumer mất quyền. OTP/session E2E chờ UC-04 |
| 21 | Deactivate Chair hiện tại của Published/InProgress; Secretary thay Chair bằng Member active, có confirmation | 200/MSG100; Chair cũ thành Member, giữ Secretary. Thiếu confirmation hoặc incoming inactive → 400/MSG101 và không đổi role |
| 22 | Guest current đã nhận lời mời; Organizer dời lịch Published; chạy worker với adapter UC-04 thật và SMTP sandbox | Có dispatch Rescheduled theo history + Guest; email giờ mới, không lộ room link. Guest revoked không được tạo dispatch. Save lại lịch y hệt không tạo event/email mới; UC-04 chưa có → BlockedDependency |

Role/permission/revoke rollback, concurrent writers và migration preflight được chạy tự động trên database riêng; không fault-inject database dùng chung. Test hub nội bộ có test kết nối thật qua TestServer rồi remove bằng API và xác minh không nhận event mới. Guest thật làm theo gate UC-04; không ghi pass bằng test token giả.

## Change note Jira đã gửi

Theo yêu cầu sửa review, đã gửi [comment 10221 trong SCRUM-20](https://sep490g116.atlassian.net/browse/SCRUM-20?focusedCommentId=10221) ngày 15/10/2026: scope iteration hai grant, tách UC-04, adapters và điều kiện nghiệm thu Guest production. Ticket Guest verification/session hiện có là [SCRUM-59](https://sep490g116.atlassian.net/browse/SCRUM-59), assignee khi kiểm tra là Nguyễn Quang Huy; chi tiết profile tự khai cần owner chốt khi tích hợp. Không đổi status/assignee Jira.

FE dùng **MSG238** cho stale revision và **MSG239** cho query/If-Match sai. MSG156/MSG157 đã được dành cho tài liệu trên main; không dùng chúng cho Participants. Postman collection và catalogue đã cập nhật theo phân bổ mới; mã tiếp theo MSG240.

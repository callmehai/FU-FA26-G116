# Kết quả kiểm thử Backend SCRUM-98

Ngày 24/10/2026 (Asia/Saigon). Branch `refactor/Synchronize-Agenda-according-to-PO-remove-duration-remove-sequence-constraints`; thay đổi chưa commit. [Contract](08-chuong-trinh-nghi-su.md), [bàn giao FE/BA](08-scrum98-ban-giao.md). Đây là evidence mới, không thay báo cáo SCRUM-25.

## Môi trường và kết quả cuối

Windows 10.0.26300, 16 logical CPU, .NET SDK 10.0.402/runtime 10.0.13, Node 25.8.0. PostgreSQL 17 từ Docker; mỗi fixture dùng database tên riêng và tự dọn. TestServer, test Guest adapters, SMTP fake/disabled; không gửi email thật. Redis 8 container tạm `trustcabinet-scrum98-redis`, loopback port 16398, chỉ dành cho kiểm thử.

| Kiểm tra thực sự chạy | Kết quả |
|---|---|
| `dotnet build TrustCabinet.sln --configuration Release -warnaserror` | Qua, 0 warning, 0 error |
| `dotnet test TrustCabinet.sln --no-build --configuration Release` (thêm logger console/trx) | **1407 passed / 0 failed / 0 skipped**, 1 phút 50 giây |
| `npm run lint` trong frontend | Qua |
| `npm run build` trong frontend | Qua; Vite cảnh báo chunk >500 kB, không sửa Frontend |
| EF `has-pending-model-changes` (Infrastructure/Web, Release, no-build) | Không còn model changes |
| `git diff --check` | Qua |

Lệnh full suite dùng `$env:TRUSTCABINET_TEST_REDIS = '127.0.0.1:16398'` trong process test. Không sửa cấu hình Redis của ứng dụng. Các artifact local (gitignored): `scrum98-build.log`, `scrum98-final-test.log`, `tests/TrustCabinet.Web.Tests/TestResults/scrum98-final.trx`, `tests/TrustCabinet.Web.Tests/bin/Release/net10.0/performance-results/agenda-performance.json`.

Trong tổng suite: Agenda 140, Participants 152, Meetings 339, Attendance 60, Documents 48, Notifications 14, Email 25, Authentication 166 test đều passed; phần còn lại gồm access, catalogue, calls và các nhóm khác. Không cộng các nhóm này thành tổng riêng vì bảng chỉ liệt kê phần regression liên quan.

## Phạm vi được kiểm chứng

- Validation/title-only, strict JSON, field cũ duration/presenterName/pendingItemIds, unknown/duplicate/case/type/Unicode, giới hạn body/chunked, phân quyền và concealment.
- Chỉnh presenter/document ở cả ba item status, reorder hỗn hợp có gap và no-op, start Pending bất kỳ, một current, complete không cần conclusion, actual timestamp không bị planning sửa.
- UC-77: Chair/Secretary; Pending và trạng thái Meeting bị khóa; null/whitespace/length 1/2000/2001, UTF-16 emoji, newline/trim, metadata từ server, mọi MSG271–274/174, anonymous/Guest/reader, rollback khi persistence lỗi, sự tồn tại biên bản với mọi MinutesStatus và content rỗng.
- Presenter current Internal/Guest (kể cả Declined), foreign/revoked/missing/inactive, tên legacy không dùng làm assignment. Notification đúng kênh/trạng thái, không gửi khi chỉ đổi title/bỏ presenter, A→Guest→A, dedup cùng event concurrent, recheck recipient, lỗi enqueue không undo Agenda.
- Remove/revoke clear presenter ở Pending/InProgress/Done cùng transaction; failure rollback cả roster/presenter; giữ position/timestamps/conclusion/document links. Event PresenterCleared chỉ sau commit.
- Many-to-many document: hai item cùng tài liệu, bỏ/xóa một link giữ tài liệu/StoredFile/link khác; organizer đọc metadata. Regression upload/storage/access chạy trong full suite.
- Concurrency dùng meeting lock và quan sát PostgreSQL lock waiter: start/reorder, mutation chờ start/end/mất role, stale order sau create/delete/start, assign chờ removal/revoke, conclusion chờ role/removal/minutes writer; hai writer conclusion lưu nguyên khối content/author/time.
- Failure injection: save/link multi-document/conclusion/removal rollback, không success event; cancellation trước commit; publisher timeout drain scoped work; post-commit enqueue/push failure không đổi thành công.
- Realtime transport: ConclusionUpdated tới viewer đang join và refetch dữ liệu đã commit; tests sẵn có kiểm quyền lại/revoke/reconnect/resync/no-op. Guest tests là consumer contract, không nghiệm thu Guest login production.
- End Meeting/helper: current hoàn thành cùng transaction/history, Pending giữ nguyên, không thay Vote/attendance; full regression meeting/room/notification/roster/Chair handover.

## Migration

Một migration mới `20261024061104_SynchronizeAgendaWithPoRequirements` + Designer/ModelSnapshot. Chỉ đổi schema Agenda: bỏ duration; thêm Guest presenter và conclusion/author/time, index/FK Restrict/check. Giữ cột presenter_name, index một current, Position gap và bảng nối tài liệu.

Test upgrade từ migration ngay trước SCRUM-98 với cả Pending/InProgress/Done kiểm ID/Position/actual/created/updated timestamps, User/Guest invitation, tài liệu/StoredFile/links, tên legacy, conclusion ban đầu null và duration column đã mất. Test mới kiểm XOR presenter, đồng bộ metadata conclusion, FK, unique current và Down bị chặn. Các fixture migrate fresh schema đầy đủ. Các test downgrade của migration lịch sử được giới hạn trước SCRUM-98 hoặc dùng database cũ riêng để tiếp tục kiểm đúng migration của chúng; không nới assertion hoặc thay migration cũ.

Chưa áp migration trên database dùng chung. Down không phục hồi dữ liệu duration; rollout cần backup và restore/forward fix.

## Performance

Đo cuối lúc **13:28:20 ngày 24/10/2026**. Snapshot query-count bằng **6** ở cả 1 item và 50 item/200 tài liệu, kèm Guest presenter, conclusion author và document liên kết nhiều mục. Không N+1 theo item.

Benchmark TestServer/SignalR LongPolling/PostgreSQL: 100 viewer, 50 item, 100 tài liệu; 300 mẫu GET và 100 mẫu event → GET:

| Đo | p95 | Mục tiêu | Kết quả |
|---|---:|---:|---|
| API snapshot | 224.3689 ms | 1000 ms | Đạt trong môi trường test |
| Realtime kèm refetch | 456.5058 ms | 2000 ms | Đạt trong môi trường test |

Đây không phải chứng nhận tải server triển khai. Transport/máy/latency thực tế có thể khác.

## Lỗi phát hiện và xử lý trong quá trình chạy

- Lượt targeted đầu: 318/322 passed; thiếu assertion MSG mới và các test downgrade cũ đi qua migration mới. Đã bổ sung test UC-77, tách phạm vi historical migration; các lỗi này đã hết.
- Test race mới ban đầu tạo removed/revoked row thiếu actor/time nên vi phạm check có sẵn. Đã sửa fixture để mô phỏng đúng evidence removal; không sửa constraint.
- Full suite đầu: 1376/1402 passed; 25 test auth kết nối nhầm Redis 3.0.504 ở cổng localhost:6379 và 1 test workflow migration downgrade qua SCRUM-98. Probe TCP/StackExchange xác nhận Redis cũ; dùng Redis 8 cổng test riêng. Test workflow chuyển sang database lịch sử riêng. Không đổi auth implementation hay nới timeout.
- Một lần build test bị file lock do suite trước còn chạy; đợi hoàn tất rồi chạy tuần tự, build cuối không warning/error.
- Full suite sau sửa: 1402/1402 passed. Bổ sung thêm 5 ca negative Guest/reorder/minutes rồi chạy lại toàn bộ: kết quả cuối **1407/1407** phía trên.

## Giới hạn nghiệm thu

Frontend không sửa; lint/build không chứng minh tương thích breaking API. Postman collection đã cập nhật/parse JSON, chưa chạy collection thủ công. Không browser/visual UI test vì ngoài phạm vi BE.

UC-04 Guest login production và UC-59 sinh nháp chưa triển khai; có consumer tests và simulated minutes writer cùng lock. Email worker/retry có regression tests, không gửi SMTP thật. Enqueue best effort sau commit không có outbox mới, event chưa persist bị lỗi chỉ log. Các giới hạn và nghĩa vụ integration được ghi trong handoff.

Không commit, push, merge, deploy hoặc chuyển Jira Done. Container Redis tạm được dọn sau suite; không tác động Redis/PostgreSQL/server dùng chung ngoài database test tách riêng.

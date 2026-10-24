# Bàn giao Backend SCRUM-98 — 24/10/2026

Branch `refactor/Synchronize-Agenda-according-to-PO-remove-duration-remove-sequence-constraints`. Không commit/push/merge/deploy, không sửa Frontend. [Contract](08-chuong-trinh-nghi-su.md), [test evidence](08-scrum98-ket-qua-kiem-thu.md), [Postman](postman/SCRUM-25-Agenda.postman_collection.json).

## Thay đổi đã triển khai

- Bỏ duration khỏi model/input/snapshot; giữ actual timestamps, complete độc lập kết luận và thời lượng.
- Secretary sửa mọi field ở Pending/InProgress/Done khi Meeting còn cho planning. Chỉ xóa Pending. Reorder gồm cả Done, giữ gap/token/no-op. Chair bắt đầu bất kỳ Pending; database vẫn chặn nhiều current.
- Presenter chỉ dùng current internal User ID hoặc Guest invitation ID chưa revoked. Declined vẫn current; Guest chưa cần OTP. Tên tự nhập cũ giữ lịch sử; PUT thay assignment và clear tên lịch sử. Remove/revoke clear assignment tất cả item cùng transaction roster/audit.
- Gán/đổi ở Published/InProgress enqueue thông báo sau commit bằng scope riêng. Internal Notification type AgendaPresenterAssigned target Meeting; Guest EmailMessage, không OTP/session/token/attachment. Dedup theo event/recipient. Giữ presenter, bỏ presenter hoặc assignment trước publish không thông báo.
- UC-77 lưu kết luận và author/time từ server, meeting lock, 1–2000 UTF-16 code units sau trim. Có bất kỳ MeetingMinutes thì khóa ghi, bất kể status/content.
- Bảng nối tài liệu hiện có hỗ trợ nhiều item; chỉ thay links của item hiện tại, bảo toàn file và các link khác. Metadata tài liệu hiển thị cho mọi reader được Meeting policy cho phép.
- Thêm event invalidation ConclusionUpdated/PresenterCleared; không đưa kết luận/email vào payload. Guest adapter vẫn chỉ là consumer integration.

## Việc thành viên Frontend cần làm

1. Cập nhật types và API wrappers qua HTTP client hiện có theo contract mới. Đây là breaking API change; TypeScript build xanh không chứng minh tích hợp đúng.
2. Bỏ input duration, elapsed/cumulative duration và phần so sánh duration. Giữ actualStartedAt/actualEndedAt.
3. Bỏ presenter free text; options có kind Internal/Guest, dùng đúng User ID/invitation ID; chỉ một ID có giá trị. Hiển thị legacyPresenterName riêng như lịch sử, không đưa vào selector hiện tại.
4. Cho sửa thông tin/tài liệu theo canEdit/canEditPlanningFields mọi trạng thái item; planning vẫn khóa sau Ended.
5. Reorder gửi toàn bộ orderedItemIds và orderVersion lấy GET gần nhất; không gửi pendingItemIds. Start dùng canSetCurrent cho mọi Pending.
6. Thêm form conclusion qua PUT /conclusion, tối đa 2000 theo JavaScript string.length sau trim; giữ xuống dòng; không cho clear rỗng. Hiển thị conclusionUpdatedByName/conclusionUpdatedAt. Sau Ended chỉ mở conclusion theo canRecordConclusion.
7. Bỏ xử lý MSG141/144/250; hỗ trợ MSG271–274 và 404/MSG174 cho conclusion. Inactive/Guest mutation vẫn 403 theo gate.
8. Editor documents có agendaItemIds[]; tài liệu có thể đã gắn nhiều mục, không disable vì mục khác.
9. Refetch sau mutation/event; reconnect join lại rồi GET. Notification AgendaPresenterAssigned mở target Meeting hiện có.
10. Kiểm UI theo skills dự án ở 390px touch, 1200px, 1440px trong phần việc FE.

## Database và rollout

Một migration mới `20261024061104_SynchronizeAgendaWithPoRequirements`: bỏ planned_duration_minutes; giữ presenter_name dưới property LegacyPresenterName; thêm presenter_guest_invitation_id, conclusion, conclusion_updated_by_id, conclusion_updated_at; thêm index/FK Restrict/check. Không thay migration cũ hoặc bảng ngoài Agenda.

Down bị chặn có chủ đích vì không phục hồi được duration đã xóa. Cần backup kiểm chứng restore trước rollout, dùng backup/forward fix. Chỉ database test riêng đã được migrate trong lượt triển khai; không chạy migration server chung. Seed giữ insert-only, không sửa dữ liệu người dùng.

## Giới hạn và BA Final Review

- UC-04/SCRUM-59 Guest login production chưa triển khai. Test dùng adapter giả, không nghiệm thu OTP/session thật.
- UC-59 sinh nháp biên bản chưa triển khai. Writer tương lai phải lấy cùng meeting lock, đọc presenter/kết luận và tạo MeetingMinutes trong cùng transaction. Không tạo/xóa nháp tạm để mở lại kết luận.
- Enqueue sau commit là best effort. Nếu chưa lưu notification/email thì chỉ log, không có outbox tự phục hồi. Email đã queue dùng retry worker hiện có; SMTP tắt thì còn Queued. Lỗi enqueue/SMTP/SignalR không rollback Agenda.
- Source Report 3 và DOCX không chỉnh. Chỉ đồng bộ trích FE-05/UC-77 và POST-4 UC-28c từ Report 3; BR-306–312 đã có nên không tạo định nghĩa riêng.
- Class diagram FE-05 còn liên kết tài liệu đơn; Report 3 phần màn hình có thể còn duration/free-text presenter. BA/FE đối chiếu contract và implementation sau tích hợp theo workflow, không ép source diagram giống tên C#.
- Không thêm gate Vote/attendance/kết luận bắt buộc. End Meeting vẫn complete current cùng transaction; các Pending giữ nguyên.
- Backend xong là đầu vào FE → BA Final Review → Tester → Review, không tự chuyển Jira Done.

Comment bàn giao đã đăng: [SCRUM-98, comment 10258](https://sep490g116.atlassian.net/browse/SCRUM-98?focusedCommentId=10258). Không chuyển trạng thái ticket.

# SCRUM-69 — bàn giao Backend UC-21/UC-22

Phạm vi: Start Meeting và End Meeting. Backend trên branch `feat/SCRUM-69-BACKEND-Starting-and-ending-a-meeting`; Frontend do thành viên khác tích hợp. Không có migration. Không bao gồm voting SCRUM-28, agenda SCRUM-98, tự kết thúc/hủy SCRUM-99, recording hoặc transcript.

## Nguồn và quyết định

- [UC-21/22](../01-requirements/05-use-case-specifications.md), [sequence UC-21](../06-sequencediagram/meeting-management/UC21_StartMeeting.txt), [sequence UC-22](../06-sequencediagram/meeting-management/UC22_EndMeeting.txt).
- BR-38/72/73/127/130/131/132/143/159/164, ADR-0013/0017 và [workflow](../DEVELOPMENT_WORKFLOW.md).
- UC-21/22 có đủ spec/sequence. Chi tiết UC-37–42 còn placeholder; task này chỉ nối đóng phòng theo UC-22/BR-143 và contract Calls, không đặc tả toàn bộ phòng họp.
- Lịch sử bất biến; status/timestamp/actor do server quyết định. System role Admin/Organizer/Approver không cấp quyền Start/End; current Chair không cần capability MeetingCreator.
- Test concurrency phát hiện User được active-user gate track trước khóa. Repository làm mới giá trị User chưa bị sửa từ snapshot đã khóa, tránh dùng IsActive cũ khi tài khoản bị vô hiệu hóa đồng thời.

## Contract để FE triển khai

Dùng API wrapper hiện có cho `POST /api/meetings/{id}/transitions`:

```json
{"action":"Start"}
```

Hoặc `{"action":"End"}`; chỉ property action, đúng casing. Không gửi reason, actorId, timestamp, status, approver hoặc organizer, kể cả null.

Response giữ nguyên `{ code, message, meeting, history }`. Start trả `200/MSG110`, Meeting InProgress và actualStartedAt; End trả `200/MSG111`, Meeting Ended và actualEndedAt. History action là Start hoặc End. Timestamp UTC microsecond; End giữ actualStartedAt. Mapping đầy đủ ở [contract Meetings](05-quan-ly-cuoc-hop.md#scrum-69--startend-uc-2122) và [catalogue](00-danh-muc-ma-thong-bao.md).

| Trường hợp | HTTP / code | Cách FE xử lý |
|---|---|---|
| Thành công | 200 / MSG110 hoặc MSG111 | Dùng response, GET lại history/agenda/call state |
| Đọc được nhưng không là active current Chair | 403 / MSG112 hoặc MSG114 | Hiện thông báo và reload quyền |
| Sai trạng thái, Start thiếu roster; request lặp | 409 / MSG112 hoặc MSG114 | Reload trạng thái; không tự retry liên tục |
| Không tồn tại/không đọc được | 404 body rỗng | NotFound như hiện tại |
| Lỗi trước commit | 503 / MSG115 hoặc MSG116 | Hiện lỗi; GET trạng thái trước khi retry |
| Lỗi active-user lookup | 503 / MSG131 | Theo gate hiện có |
| JSON/action/property sai | 400 / MSG80 | Sửa request |
| Content-Type/body size | 415 / MSG34; 413 / MSG87 | application/json, tối đa 64 KiB |
| Token không hợp lệ/tài khoản inactive | 401 / 403 theo gate hiện có | Theo auth/session hiện có |

Checklist FE:

- Bổ sung Start/End vào transition request và history action types; thêm nhãn timeline.
- Nút Start: Chair ở Published; End: Chair ở InProgress. UI chỉ là hướng dẫn, server vẫn re-authorize.
- Hiện confirmation End trước request; disable/loading khi đang gửi. Không optimistic status mutation.
- Xử lý bằng code, không so sánh message. Network error/mất response: GET Meeting/history trước khi retry.
- Start cần đúng một active current Chair và ít nhất một active current Secretary; Pending/Declined/Unknown attendance không cản Start, không kiểm giờ dự kiến hay agenda rỗng.
- End không kiểm Secretary, attendance, Pending agenda hoặc Vote Open. Không kỳ vọng Vote tự đóng; SCRUM-28 xử lý voting riêng.
- Thêm loại notification MeetingStarted. Start không có event lifecycle mới trên hub Meetings.
- Nhận `agendaChanged` với changeType `MeetingEnded`: GET lại agenda và Meeting để cập nhật quyền. Khi không có current item, agendaItemId/title đều null.
- Reconnect phải GET snapshot; SignalR không phải hàng đợi durable.

## Side effect và giới hạn nhất quán

Start lưu Meeting và history cùng transaction rồi mới lưu batch notification. Recipient là active current internal participants, distinct UserId, trừ Chủ tọa vừa bấm Start (không ai nhận thông báo về thao tác của chính mình, như Publish), vẫn gồm Declined; loại Guest/Removed/Delegated/inactive. Notification `MeetingStarted`, target Meeting, OriginHistoryId và khóa `meeting-started:{historyId}:{userId}`. Unique index hiện có chống trùng. Notification đã lưu được worker push tiếp; lỗi tạo notification sau commit là best effort, **không có outbox/replay bền vững mới**, không gửi email Start.

End hoàn tất current agenda trong cùng transaction; không có current item vẫn thành công. Pending/Done khác và Vote/ballot/attendance/response giữ nguyên. Timestamp agenda không hợp lệ làm rollback toàn bộ với MSG116, không sửa hộ dữ liệu.

Sau commit End, publisher phát agendaChanged và service đóng room Online/Hybrid. Mỗi side effect có budget 2 giây độc lập RequestAborted, được await; lỗi publisher không ngăn thử đóng room. Room không tồn tại là đã đóng; không cần active CallSession mới gọi DeleteRoom, không tạo session giả. Sau provider xác nhận mới đóng session/stint đang mở. InPerson không gọi provider; LiveKit chưa cấu hình không chặn Start/End.

MSG111 xác nhận Meeting đã Ended. Khi provider/bookkeeping lỗi, job đối soát (chu kỳ 30 giây) thử lại, kể cả orphan room không có CallSession; thời gian thực tế còn phụ thuộc provider và các lần retry. Join ticket mới bị từ chối sau End. JWT đã cấp không thu hồi được: LiveKit vẫn cho client dùng lại token để tái tạo room. Webhook `participant_joined` chặn việc này — Meeting không còn InProgress thì lượt vào được ghi lại rồi room bị xóa và session kết thúc ngay; job đối soát chỉ còn là lưới an toàn khi webhook bị mất. Xem số đo thực tế trong [báo cáo kiểm thử](10-scrum69-kiem-thu.md).

## Tester và trạng thái bàn giao

[Collection Postman](postman/SCRUM-69-Meeting-Lifecycle.postman_collection.json) cần một Meeting Published riêng, Chair và Secretary active, token Chair và một participant không phải Chair; chạy tuần tự sẽ kết thúc Meeting đó. Không dùng dữ liệu production.

[Smoke LiveKit](../../scripts/scrum69-livekit-clients.mjs) sử dụng hai Chrome headless client không bật microphone/camera, database test riêng; cách chạy ở [README Postman](postman/README.md).

Kết quả build, full regression, coverage, concurrency, hiệu năng và giới hạn được ghi trong [báo cáo kiểm thử](10-scrum69-kiem-thu.md). Backend sẵn sàng bàn giao không đồng nghĩa SCRUM-69 đã Done: còn FE → BA Final Review → Tester theo workflow. Source Frontend không được sửa trong task này.

## Bổ sung sau review — thời gian đóng CallSession

Giờ End của Meeting/history là mốc nghiệp vụ; CallSession dùng giờ server sau khi provider xác nhận DeleteRoom. Không truyền mốc End của Meeting vào CloseMeetingRoomAsync nữa. Repository lấy mốc kết thúc hiệu lực không sớm hơn StartedAt, mọi JoinedAt và LeftAt đã lưu, rồi dùng cùng mốc đó để đóng các stint còn mở; không sửa stint đã đóng. Quy tắc này cũng bảo vệ khi timestamp provider lệch về tương lai hoặc sự kiện đến trễ. Reconciliation lấy giờ sau DeleteRoom cho session cần đóng. Không thay schema hoặc ghi đè ActualEndedAt/history của Meeting.

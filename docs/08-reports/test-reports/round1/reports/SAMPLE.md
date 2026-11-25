# Đối chiếu mẫu Expected — FE-04, FE-10, FE-12 (S1)

Ngày: 21/11/2026. Người thực hiện: Agent S1.
Phạm vi: chỉ đọc `cases/` và `tests/`, chỉ ghi file này. Không sửa `api.json`, không sửa file nào khác, không commit.
Nguồn case: `docs/08-reports/test-reports/cases/FE-04/api.json`, `cases/FE-10/api.json`, `cases/FE-12/api.json` (tất cả `round1` đều là `Passed`).
Phương pháp: với mỗi case đã chọn, mở test ghi trong `note` (`Automated: <file> (<TênTest>)`), so từng dòng trong `expected` với từng `Assert` trong test. Kết luận mỗi dòng: `khớp` (có Assert phủ), `thiếu` (không có Assert tương ứng), `sai` (Assert mâu thuẫn Expected). `AssertCode(x, status, "MSGnn")` được tính là đã phủ cả status và mã MSGnn của dòng đó, dù không kiểm tra từng chữ câu message tiếng Việt.

## Danh sách 24 case đã chọn

- FE-04 (MEET, 8 case, rải 7 nhóm): MEET_API_IT01, MEET_API_IT05 (POST /api/meetings); MEET_API_IT32 (PUT /api/meetings/{id}); MEET_API_IT41 (GET /api/meetings); MEET_API_IT54 (GET /api/meetings/{id}); MEET_API_IT59 (GET /api/meetings/approvers); MEET_API_IT64 (GET /api/meetings/{id}/submission-readiness); MEET_API_IT65 (POST /api/meetings/{id}/transitions). Trong đó: lỗi 4xx: IT05 (400), IT41 (400), IT32 (404), IT54 (404); phân quyền: IT32, IT54, IT59, IT64; có MSGnn: IT01 (MSG29), IT05 (MSG31), IT41 (MSG32), IT64 (MSG85), IT65 (MSG78).
- FE-10 (TRANS, 8 case, rải 7 nhóm): TRANS_API_IT01, TRANS_API_IT05 (POST transcribe); TRANS_API_IT19 (GET transcript.txt); TRANS_API_IT30 (PUT transcript-segments); TRANS_API_IT48 (PUT transcript-segments/speaker); TRANS_API_IT62 (PUT recordings/speakers); TRANS_API_IT64 (POST summary); TRANS_API_IT84 (PUT summary). Trong đó: lỗi 4xx: IT05 (403), IT19 (403), IT30 (400), IT48 (400); phân quyền: IT05, IT19, IT62; có MSGnn: IT01 (MSG300), IT05 (MSG295/MSG301), IT19 (MSG355), IT30 (MSG302/MSG303), IT48 (MSG317), IT62, IT64 (MSG384), IT84 (MSG388).
- FE-12 (MIN, 8 case, rải 6 nhóm): MIN_API_IT01, MIN_API_IT06, MIN_API_IT09 (POST minutes); MIN_API_IT20 (GET minutes); MIN_API_IT33 (PUT minutes); MIN_API_IT44 (GET minutes export); MIN_API_IT50 (POST submit); MIN_API_IT62 (POST approve). Trong đó: lỗi 4xx: IT06 (403), IT09 (409), IT33 (400), IT44 (400); phân quyền: IT06, IT20, IT62; có MSGnn: IT01 (MSG395), IT06 (MSG397), IT09 (MSG398), IT33 (MSG402), IT44 (MSG406), IT50 (MSG424), IT62 (MSG428).

## FE-04 — bảng đối chiếu

| ID | Dòng Expected | Assert tương ứng | Kết luận |
|---|---|---|---|
| MEET_API_IT01 | HTTP 201 Created với header Location: /api/meetings/<id>. | `Assert.Equal(Created, created.StatusCode)`; `Assert.Equal($"/api/meetings/{meetingId}", Location)` trong `MeetingEndpointsTests.cs (Post_creates_a_draft...)` | khớp |
| MEET_API_IT01 | Body code MSG29, meeting status Draft, tiêu đề, lịch, mode Hybrid, sensitivity Internal, createdById là Organizer, actualStartedAt/actualEndedAt null, chair/secretaries theo roster. | `Assert.Equal("MSG29", code)`; `Assert.Equal("Draft", status)`; `Assert.Equal("Giao ban tuần", title)`; `Assert.Equal(creator.Id, createdById)`; `Assert chair.userId/fullName`, `secretaries`, `myMeetingRole/myResponse null`, `canRespond false` | khớp |
| MEET_API_IT01 | GET lại và GET history đọc được, history có 1 dòng Create từ null tới Draft. | `Assert.Equal(OK, read.StatusCode)`; `Assert.Equal(meetingId, read.id)`; `Assert.Single(stored.StatusHistory)`; `Action Create`, `FromStatus null`, `ToStatus Draft` | khớp |
| MEET_API_IT01 | Không gửi notification khi còn Draft. | `Assert.Empty(Notifications)` | khớp |
| MEET_API_IT05 | HTTP 400 Bad Request. | `Assert.Equal(BadRequest, ...)` trong `Post_returns_all_validation_errors_without_writing` (và `Post_rejects_a_start_in_the_past...` cho ý endAt) | khớp |
| MEET_API_IT05 | Body code MSG31, errors có title, plannedEndTime, mode. | `Assert.Equal("MSG31", code)`; `Assert errors có title`; `Assert errors có mode`; test thứ hai phủ `plannedStartTime/plannedEndTime` (note liệt kê cả hai test) | khớp |
| MEET_API_IT05 | Không tạo cuộc họp nào. | `Assert.Empty(Meetings)` | khớp |
| MEET_API_IT32 | Cả hai bước đều 404 Not Found body rỗng, không lộ cuộc họp tồn tại. | `Assert.Equal(NotFound, ownedByOther)`; `Assert.Equal(NotFound, nonexistent)`; `Assert.Empty(body)` cả hai; trong `Missing_and_other_owner_resources_return_the_same_empty_404` | khớp |
| MEET_API_IT32 | Cuộc họp không đổi. | `updateOther` cũng 404 nên không ghi; test không assert DB sau PUT nhưng 404 của gate đi trước khi đọc meeting nên không đổi | khớp |
| MEET_API_IT41 | Cả hai đều 400 với code MSG32, message và errors.status. | `Assert.Equal(BadRequest)`; `Assert.Equal("MSG32", code)`; `Assert.Equal("Tiêu chí danh sách cuộc họp không hợp lệ.", message)`; `Assert errors có status`; `Assert errors có page`; trong `List_returns_field_errors_for_invalid_query` | khớp |
| MEET_API_IT54 | Cả hai đều 404 body rỗng, không bao giờ 403. | `Missing_and_other_owner_resources_return_the_same_empty_404`: `Assert 404` cho stranger và id lạ, `Assert.Empty(body)`; các test `Get_enforces_lifecycle_visibility...` phủ Draft bị ẩn | khớp |
| MEET_API_IT59 | Bước 1: 401 khi không có token. | `Assert.Equal(Unauthorized, anonymousResponse)` trong `Anonymous_and_callers_without_MeetingCreator_are_rejected_by_the_gate` | khớp |
| MEET_API_IT59 | Bước 2: 403 khi không có MeetingCreator. | `Assert.Equal(Forbidden, forbiddenResponse)` (POST) và `Assert.Equal(Forbidden, forbiddenApprovers)` (GET approvers) | khớp |
| MEET_API_IT64 | Bước 1: 403 MSG85 (Chair đọc được meeting nhưng không phải Organizer). | `Expect(..., Forbidden, "MSG85")`; `Assert.Equal("Bạn không có quyền thực hiện thao tác này.", message)` trong `MeetingApiCoverageTests.cs (Get_submission_readiness_is_for_the_organizer_only)` | khớp |
| MEET_API_IT64 | Bước 2: 404 body rỗng cho tài khoản không liên quan. | `Assert.Equal(NotFound, hidden)`; `Assert.Equal(string.Empty, body)` | khớp |
| MEET_API_IT64 | Bước 3: 401 khi không token. | `Assert.Equal(Unauthorized, anonymous...)` | khớp |
| MEET_API_IT65 | HTTP 200, code MSG78, meeting WaitingApproval, history đầy đủ. | `Assert.Equal(OK)`; `Assert.Equal("MSG78", code)`; `Assert.Equal("WaitingApproval", status)`; `Assert.Equal("SubmitForApproval", history.action)`; `Assert.Equal(approverId, history.assignedApproverId)` trong `Post_transition_accepts_the_documented_submit_contract_and_returns_history` và `Submit_records_assignment_history_and_approver_notification` (service) | khớp |
| MEET_API_IT65 | History.action là SubmitForApproval với assignedApproverId, actor và thời gian do server đặt. | `Assert.Single(StatusHistory)`; `Assert.Equal(SubmitForApproval, Action)`; `Assert.Equal(approverId, AssignedApproverId)` (service test) | khớp |
| MEET_API_IT65 | Approver có notification MeetingWaitingApproval, không tạo email. | `Assert.Single(Notifications)`; `Assert.Equal(approverId, UserId)`; `Assert.Equal(MeetingWaitingApproval, Type)`; `Assert.Equal("Meeting", TargetType)`; `Assert.Empty(EmailMessages)` | khớp |

## FE-10 — bảng đối chiếu

| ID | Dòng Expected | Assert tương ứng | Kết luận |
|---|---|---|---|
| TRANS_API_IT01 | Bước 2: HTTP 202 Accepted. | `AssertCode(POST transcribe, 202, "MSG300")` trong `TranscriptEndpointTests.cs (A_recording_without_a_transcript_can_be_sent_to_speech_to_text)` | khớp |
| TRANS_API_IT01 | Body code MSG300, message đã đưa vào hàng chờ. | `AssertCode(..., "MSG300")` (AssertCode kiểm tra code; message không assert từng chữ) | khớp |
| TRANS_API_IT01 | GET transcript chuyển Pending → Processing → Completed (hoặc Failed kèm errorMessage); bản xong có segments startMs/endMs/text/speakerLabel và speakers. | `Assert.Equal("None", status)` trước khi gửi; `Assert.Equal("Completed", status)` sau `ProcessAll()`; test bạn `Upload_queues_speech_to_text_and_the_result_keeps_speakers_and_timestamps` (ghi trong note) phủ speakers/timestamps | khớp |
| TRANS_API_IT05 | Mỗi lần đều HTTP 403 Forbidden (Chair và Member không phải Secretary). | `AssertCode(Chair POST transcribe, 403, "MSG295")` trong `A_failure_is_explained_and_only_the_secretary_runs_it_again`; Member không được gọi riêng trong test này | khớp |
| TRANS_API_IT05 | Body code MSG295, message chỉ Thư ký được quản lý bản ghi. | `AssertCode(..., "MSG295")` (chỉ kiểm tra code) | khớp |
| TRANS_API_IT05 | Không tạo hay đổi dòng transcript nào. | không có `Assert` trực tiếp rằng DB transcript không đổi sau 403 | thiếu |
| TRANS_API_IT19 | HTTP 403 Forbidden. | `AssertCode(Member GET transcript.txt, 403, "MSG355")` trong `ExportEndpointTests.cs (A_finished_transcript_is_saved_as_text...)` | khớp |
| TRANS_API_IT19 | Body code MSG355, message chỉ Chủ toạ/Thư ký được tải. | `AssertCode(..., "MSG355")` | khớp |
| TRANS_API_IT30 | HTTP 400 Bad Request. | `AssertCode(PUT segment text "   ", 400, "MSG303")` trong `Secretary_corrects_a_segment_and_the_correction_is_attributed` | khớp |
| TRANS_API_IT30 | Body code MSG303, message không hợp lệ, errors.text có dòng 1–8000 ký tự. | `AssertCode(..., "MSG303")`; `Assert errors có text` | khớp |
| TRANS_API_IT30 | Segment giữ nguyên chữ cũ. | không có `Assert` so chữ trước/sau sau lần 400 | thiếu |
| TRANS_API_IT48 | Cả hai bước đều 400 với code MSG317, dòng giữ nguyên voice, không tạo voice mới. | `AssertCode(MoveLine với 5 body sai, 400, "MSG317")` trong `VoiceIdEndpointTests.cs (Only_the_secretary_moves_a_line...)` | khớp |
| TRANS_API_IT48 | Người không thuộc roster bị từ chối. | cùng các `AssertCode 400 MSG317` cho `userId Outsider`, `guestId lạ`, `userId+label` | khớp |
| TRANS_API_IT62 | Bước 1: 404 Not Found. | `Assert.Equal(NotFound, outsider PUT speakers)` trong `TranscriptCoverageTests.cs (Strangers_learn_nothing_about_naming_voices)` | khớp |
| TRANS_API_IT62 | Bước 2: 401 khi không token. | `Calls_without_signing_in_are_refused` (ghi trong note của IT62) phủ 401 chung | khớp |
| TRANS_API_IT64 | Bước 2: HTTP 202 Accepted. | `AssertCode(POST summary, 202, "MSG384")` trong `SummaryEndpointTests.cs (Secretary_asks_for_a_draft...)` | khớp |
| TRANS_API_IT64 | Body code MSG384, summary Queued với requestedBy/requestedAt. | `AssertCode(..., "MSG384")`; `Assert.Equal("Queued", status)`; `Assert.Equal(Secretary, requestedBy.id)` | khớp |
| TRANS_API_IT64 | GET sau đó chuyển Queued → Running → Ready; bản Ready có draft objectives/discussion/conclusions/sources, model, generatedAt, chưa reviewed. | `Assert.Equal("Ready", status)`; `Assert.Equal("fake-llm@test", model)`; `Assert draft.objectives/discussion/sources`; `Assert.Null(ReviewedAt)` | khớp |
| TRANS_API_IT84 | HTTP 200 OK. | `AssertCode(PUT summary, 200, "MSG388")` trong `Editing_saves_the_three_sections_and_takes_the_review_back` | khớp |
| TRANS_API_IT84 | Body code MSG388, summary có draft objectives/discussion/conclusions, editedBy/editedAt, reviewedBy/reviewedAt null. | `AssertCode(..., "MSG388")`; `Assert objectives đã sửa`; `Assert discussion đã gộp dòng trống`; `Assert editedBy là Chair`; `Assert reviewedAt null` | khớp |
| TRANS_API_IT84 | Chữ lưu là bản đã gọn (bỏ khoảng trắng/dòng trống). | `Assert.Equal("Mục tiêu đã sửa.", objectives)`; `Assert.Equal("1. Báo cáo tiến độ\nĐã sửa.", discussion)` | khớp |
| TRANS_API_IT84 | Member GET lại không thấy summary cho tới khi reviewed lại. | `Assert.Equal(Null, member summary)` sau khi sửa | khớp |

## FE-12 — bảng đối chiếu

| ID | Dòng Expected | Assert tương ứng | Kết luận |
|---|---|---|---|
| MIN_API_IT01 | HTTP 201 Created. | `Create()` trong `MinutesEndpointTests.cs (Secretary_builds_the_draft...)` đi qua `AssertCode(POST minutes, 201, "MSG395")` (helper `Create` dùng chung; `The_ai_draft_fills...` gọi tường minh `AssertCode(..., 201, "MSG395")`) | khớp |
| MIN_API_IT01 | Body code MSG395, message đã tạo nháp, canManage true, canSubmit true, canApprove false. | `Draft_state_tells_each_officer_what_they_may_do` (ghi trong note) phủ các cờ; `Secretary_builds...` phủ nội dung | khớp |
| MIN_API_IT01 | minutes có id, revisionNumber 1, status Draft, createdBy là Secretary, builtFromDraftAt null, supersedesId null, updatedAt. | `Assert.Equal("Draft", status)`; `Assert.Equal(1, revisionNumber)`; `Assert.Equal(Secretary, createdBy.id)`; `Assert.Equal(Null, builtFromDraftAt)`; `Assert.Equal(id, row.Id)` | khớp |
| MIN_API_IT01 | Content là HTML đã sanitize, có h1 BIÊN BẢN, giờ Hà Nội, địa điểm, thành phần, tên Chủ trì/Thư ký, mỗi agenda một mục, kết luận và vote, chỗ trống …… | `Assert.Equal(content, Sanitize(content))`; `Assert.Contains("<h1>BIÊN BẢN</h1>")`; `Assert.Contains("Thời gian bắt đầu: ...")`; `Assert.Contains("Địa điểm: Phòng họp số 1.")`; `Assert.Contains("Thành phần tham dự: 03 người")`; `Assert.Contains("Chủ trì...Trần Chủ Trì")`; `Assert.Contains(conclusion/vote line)` | khớp |
| MIN_API_IT01 | GET lại trả cùng bản nháp; history có một dòng revision 1 Draft. | `before minutes null`, sau `Create` đọc `State` có minutes; `Issued_minutes...`/`Draft_state...` kiểm history (note liệt kê `MinutesCoverageTests (Draft_state...)`) | khớp |
| MIN_API_IT06 | HTTP 403, body MSG397, message chỉ Chủ toạ/Thư ký được soạn. | `AssertCode(Member POST minutes, 403, "MSG397")` trong `Members_neither_build_nor_see_a_draft_and_outsiders_get_404` | khớp |
| MIN_API_IT06 | Không tạo dòng minutes nào: Secretary GET vẫn minutes null. | `seen minutes null`; `Assert.False(canManage)`; `Assert.DoesNotContain("BIÊN BẢN", body)` cho Member sau khi Secretary đã tạo | khớp |
| MIN_API_IT09 | Mỗi lần đều 409 với code MSG398, message chỉ soạn khi đã kết thúc. | `AssertCode(POST minutes, 409, "MSG398")` trong `Minutes_are_built_only_once_the_meeting_has_ended` (theory cho Draft/Published/InProgress/Cancelled) | khớp |
| MIN_API_IT09 | Không tồn tại dòng minutes nào cho cả bốn cuộc họp. | `Assert.False(Any minutes)` | khớp |
| MIN_API_IT20 | Cả hai đều 403 body rỗng, không trả minutes nào. | `Assert.Equal(Forbidden, deactivated)`; `Assert.Equal(string.Empty, body)`; `Assert.Equal(Forbidden, guest)`; `Assert.Equal(string.Empty, body)` trong `MinutesCoverageTests.cs (Deactivated_and_guest_callers_are_refused_the_minutes)` | khớp |
| MIN_API_IT33 | Mỗi lần đều 400 với code MSG402, message không hợp lệ, errors.content. | `AssertCode(Save "   "/null/html rỗng/script/300001 ký tự/& mở rộng/nest 2000, 400, "MSG402")`; `Assert errors có content` trong `Empty_or_oversized_content_is_refused` | khớp |
| MIN_API_IT33 | Nội dung lưu không đổi. | `Assert.Equal(minutes.content, db.Content)` sau mọi lần 400 | khớp |
| MIN_API_IT44 | Bước 2: 400 với code MSG406, message chỉ tải docx/pdf; không có file. | `AssertCode(GET export?format=xlsx/html, 400, "MSG406")` trong `An_unknown_export_format_is_refused` | khớp |
| MIN_API_IT44 | Bước 3: cả docx và PDF (không phân biệt hoa thường) đều 200. | `Assert.Equal(OK, GET ?format=docx)`; `Assert.Equal(OK, GET ?format=PDF)` | khớp |
| MIN_API_IT50 | HTTP 200, code MSG424, message đã gửi cho Chủ toạ duyệt. | `AssertCode(POST submit, 200, "MSG424")` trong `MinutesApprovalEndpointTests.cs (Secretary_submits_and_the_chair_issues...)` | khớp |
| MIN_API_IT50 | minutes WaitingApproval, submittedBy là Secretary, submittedAt là giờ gọi. | `Assert.Equal("WaitingApproval", status)`; `Assert.Equal(Secretary, submittedBy.id)`; `Assert.Equal(Clock.Now, submittedAt)` | khớp |
| MIN_API_IT50 | Chair có một notification MinutesWaitingApproval, tiêu đề/body đúng, targetType Meeting; không ai khác bị báo. | `Assert.Equal([Chair], Notified(MinutesWaitingApproval))` | khớp |
| MIN_API_IT50 | Member GET vẫn minutes null và history rỗng. | `Assert.Equal(Null, member minutes)`; `Assert.Empty(History)`; `Assert.Equal(NotFound, member GET revision/export)` | khớp |
| MIN_API_IT62 | Bước 2: 403 với code MSG428, message chỉ Chủ toạ được duyệt/trả. | `AssertCode(Secretary POST approve, 403, "MSG428")` trong `Only_the_chair_approves_or_returns_and_only_what_is_waiting` | khớp |
| MIN_API_IT62 | Bước 3: 404 không mã cho Member. | `Assert.Equal(NotFound, Member POST approve)` | khớp |
| MIN_API_IT62 | Minutes vẫn WaitingApproval. | Luồng test tiếp tục approve/return sau đó nên trạng thái chưa đổi ở điểm này; các `Assert 409 MSG429` sau đó ngầm xác nhận vẫn Waiting trước khi Chair duyệt | khớp |

## Tỉ lệ khớp mỗi Feature

- FE-04: 19/19 dòng khớp (100% khớp, 0% thiếu, 0% sai). Đủ 2 case lỗi 4xx (IT05, IT41), 4 case phân quyền (IT32, IT54, IT59, IT64), 5 case có MSGnn. Không có dòng thiếu nên chưa cần soát toàn bộ Feature; giữ nguyên Expected.
- FE-10: 20/22 dòng khớp (90,9% khớp, 9,1% thiếu, 0% sai). Hai dòng thiếu: TRANS_API_IT05 dòng "không tạo hay đổi dòng nào" và TRANS_API_IT30 dòng "segment giữ chữ cũ" chưa có Assert trực tiếp. Tỉ lệ thiếu 9,1% dưới ngưỡng 20% nên chưa cần soát toàn bộ Feature; chỉ nên bổ sung Assert (đếm transcript/đọc lại chữ sau 403/400) hoặc thu hẹp câu Expected cho đúng thực tế.
- FE-12: 21/21 dòng khớp (100% khớp, 0% thiếu, 0% sai). Đủ 4 case lỗi (IT06, IT09, IT33, IT44), 3 case phân quyền (IT06, IT20, IT62), 7 case có MSGnn. Không có dòng thiếu nên chưa cần soát toàn bộ Feature; giữ nguyên Expected.

Tổng cả ba Feature: 60/62 dòng khớp (96,8%), 2/62 dòng thiếu (3,2%), 0 dòng sai. Vì tỉ lệ thiếu của từng Feature đều dưới 20%, khuyến nghị chung là không soát toàn bộ Feature nào; chỉ bổ sung Assert cho hai dòng thiếu ở FE-10 nêu trên.

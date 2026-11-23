# Kế hoạch chạy Round 1 — Report 5.2.2 (test API)

> Ghi chú ngày 23/11/2026: bộ case đã thu gọn còn **API 819 case, 15 Feature** (commit `03594e25`). Con số 1537 case và bảng ở mục 1 trong tài liệu này là số trước thu gọn, chỉ giữ lại để tham khảo cách chia nhóm B1/B2. Nguồn chính xác là `cases/FE-xx/api.json` và `sheets.json` (`integration_cases.py check` 0 lỗi).

Tài liệu giao việc cho agent. Nguồn là `cases/FE-xx/api.json` (1537 case, 15 Feature). Tester Round 1: **Phạm Gia Khánh**. Làm theo ba giai đoạn: **0 soát case**, **1 chạy test tự động và đối chiếu**, **2 viết test tự động cho các case chưa có test**. Quy tắc chung: không sửa code production để case xanh; lỗi thì ghi `Failed` và mở ticket; không đánh `Passed` khi chưa chạy thật.

## 1. Tổng quan

| Feature | Sheet | Tổng | Có test tự động | Chưa có test | Chèn lỗi | Dịch vụ ngoài | Chờ thời gian |
|---|---|---:|---:|---:|---:|---:|---:|
| FE-01 | Authentication | 57 | 47 | 10 | 2 | 10 | 7 |
| FE-02 | User Management | 61 | 56 | 5 | 2 | 7 | 5 |
| FE-03 | Access Control | 63 | 63 | 0 | 5 | 0 | 4 |
| FE-04 | Meeting Management | 116 | 100 | 16 | 11 | 1 | 7 |
| FE-05 | Agenda | 83 | 77 | 6 | 9 | 1 | 6 |
| FE-06 | Participants | 195 | 145 | 50 | 4 | 5 | 13 |
| FE-07 | Documents | 217 | 183 | 34 | 27 | 11 | 7 |
| FE-08 | Online Room | 136 | 109 | 27 | 10 | 93 | 10 |
| FE-09 | Recording | 126 | 111 | 15 | 7 | 3 | 9 |
| FE-10 | Transcription & Summary | 181 | 133 | 48 | 3 | 51 | 9 |
| FE-11 | Voting | 53 | 38 | 15 | 8 | 1 | 2 |
| FE-12 | Minutes | 83 | 69 | 14 | 9 | 0 | 1 |
| FE-13 | Notifications | 47 | 26 | 21 | 4 | 12 | 2 |
| FE-15 | Reporting | 21 | 18 | 3 | 3 | 0 | 0 |
| FE-16 | Calendar | 98 | 61 | 37 | 6 | 0 | 2 |
| | **Tổng** | **1537** | **1236** | **301** | 110 | 195 | 84 |

Ba cột cuối là nhãn đoán từ chữ trong case (có thể trùng nhau), dùng để biết case nào cần chuẩn bị thêm.

- **Có test tự động**: ghi chú case có `Automated: <file> (<Test>; …)`, tức đã có xUnit chạy đúng việc đó trên PostgreSQL thật. Phần lớn case thuộc nhóm này nên chạy rất nhanh.
- **Chưa có test** (ghi chú `Manual only`): chỉ có nghĩa là chưa ai viết test xUnit cho case đó, không phải case không tự động hoá được. Endpoint đã có sẵn. Hướng xử lý: agent viết test xUnit rồi đổi `note` sang `Automated:` (mục 4).

## 2. Giai đoạn 0 — Soát case trước khi chạy

Chạy test trên case sai chỉ cho kết quả sai. Mỗi Feature soát theo bảng dưới, sửa JSON ngay nếu case sai (hoặc báo người viết nếu nhiều).

| # | Kiểm tra | Cách làm |
|---|---|---|
| 1 | Chạy `python scripts/test_reports/integration_cases.py check` | Phải 0 lỗi: test được trích dẫn tồn tại, mã `MSGnn` có trong danh mục |
| 2 | Case tự động: đọc test được trích dẫn | Đối chiếu từng dòng *Expected* với `Assert` trong test: mã HTTP, `MSG`, nội dung thông báo, thay đổi trong DB. Expected nói nhiều hơn test thì ghi rõ phần chưa có test hoặc đổi case thành `Manual only` cho phần đó |
| 3 | Case chưa có test: so với code | Đọc endpoint trong `backend/TrustCabinet.Web/Endpoints/` và `docs/03-api/`: đường dẫn, phương thức, body, header (`If-Match`), mã trả về |
| 4 | Bước tự đứng được | Có bước đăng nhập, header, body mẫu; tài khoản và dữ liệu nằm trong *Pre-conditions*; ai đọc cũng chạy được |
| 5 | Hành vi đã đổi ở họp ngày 19–20/11/2026 | Giới hạn tải tệp 10 MB / 100 MB (MSG164, MSG288); dự thảo AI và biên bản chỉ tạo khi họp đã kết thúc (MSG386, MSG398); Thư ký được bắt đầu họp, chỉ Chủ trì kết thúc; tìm kiếm không phân biệt dấu; lịch chia sẻ 7 ngày 00:00–24:00, ô tìm giờ chọn được thứ Bảy/Chủ nhật; nhật ký có tên đối tượng, họp Mật bị ẩn |
| 6 | Ranh giới 403 và 404 | Tài nguyên cụ thể không được đọc thì `404`; thiếu quyền ở endpoint cố định thì `403` (xem AGENTS.md của repo) |
| 7 | Trùng lặp | Hai case cùng một tình huống thì giữ một |

Soát xong một Feature thì chạy `check`; có sửa thì chạy `build`.

## 3. Giai đoạn 1 — Chạy test tự động và đối chiếu

Một lần chạy cho toàn bộ case có test tự động:

1. Bật PostgreSQL: `docker compose up -d`. Test dùng PostgreSQL thật (xem `tests/TrustCabinet.Web.Tests/PostgresTestDatabase.cs`; đặt `POSTGRES_PASSWORD` nếu cần).
2. `dotnet test TrustCabinet.sln --logger "trx;LogFileName=round1.trx"`.
3. Với mỗi case tự động, lấy tên test trong ngoặc ở `note`. Kết quả case là **Passed** nếu mọi test trích dẫn đều pass **và** bước soát số 2 xác nhận Expected được test phủ; **Failed** nếu một test fail (ghi tên test và dòng lỗi vào `note`); để trống nếu test bị skip.
4. Điền `round1`, `date1`, `tester1` vào JSON bằng một script đọc file `.trx`, khớp tên test với `note`, và ghi JSON giữ nguyên định dạng và xuống dòng CRLF. Đây là việc đầu tiên của agent.

## 4. Giai đoạn 2 — Viết test tự động cho các case chưa có test

Quyết định ngày 20/11/2026: không chạy tay bằng `curl`; viết test xUnit để chạy lại được bằng một lệnh và CI bảo vệ về sau. Danh sách ở mục 7, chia hai nhóm.

**Nhóm B1: gọi API bình thường (viết test ngay).**

1. Dùng agent `integration-test-writer` (xem `.claude/agents/`). Test đặt cạnh test cùng Feature trong `tests/TrustCabinet.Web.Tests/<Feature>/`, theo cùng kiểu (PostgreSQL thật, fixture sẵn có).
2. Mỗi test lấy *Procedure* và *Expected* của case làm kịch bản: cùng người dùng, cùng dữ liệu, cùng mã HTTP, `MSGnn`, nội dung và thay đổi trong DB. Đặt tên test mô tả tình huống.
3. Mỗi mã `MSGnn` backend trả ra phải có ít nhất một test khẳng định nó (`MessageCodeCatalogueTests` kiểm tra).
4. Chạy `dotnet test` cho Feature đó. Test pass thì đổi `note` của case từ `Manual only` sang `Automated: tests/TrustCabinet.Web.Tests/<đường dẫn>.cs (<TênTest>)`, rồi chạy `integration_cases.py check`.
5. Test fail vì **code sai** thì không sửa test cho xanh: ghi case `Failed`, mở ticket Jira, giữ test lại. Test fail vì **case sai** thì sửa case.
6. Sau đó case được xử lý như nhóm có test tự động (mục 3).

**Nhóm B2: cần điều kiện riêng (nhãn trong mục 7).**

- **Chèn lỗi** (500/503 do DB hoặc kho tệp hỏng): xem các test hiện có chèn lỗi thế nào và viết theo cách đó nếu được.
- **Dịch vụ ngoài** (LiveKit, worker giọng nói, AI, Google, email): dùng bản giả (fake) trong test nếu hạ tầng đã có; nếu không thì ghi `N/A` kèm lý do ở Round 1.
- **Chờ thời gian**: dùng đồng hồ giả (`fixture.Clock`) thay vì chờ thật.

## 5. Cách ghi kết quả

Trong JSON của case: `"round1": "Passed"` hoặc `"Failed"` hoặc `"N/A"`; `"date1": "YYYY-MM-DD"`; `"tester1": "Phạm Gia Khánh"`. Bằng chứng, mã ticket hoặc lý do `N/A` thêm vào cuối `note` (cột *Actual result / Evidence / Defect*). Xong một Feature thì chạy `integration_cases.py check`, rồi `build`. Không gõ vào Excel.

## 6. Phân việc theo Feature

Mỗi Feature là một gói cho một agent. Thứ tự khuyên dùng: FE-04, FE-12, FE-10, FE-07, FE-16 (có nhiều sửa ngày 20/11), rồi các Feature còn lại.

| Gói | Feature | Việc | Case cần viết test |
|---|---|---|---:|
| FE-01 | Authentication | soát 57 case, đối chiếu test, viết test cho 10 | 10 |
| FE-02 | User Management | soát 61 case, đối chiếu test, viết test cho 5 | 5 |
| FE-03 | Access Control | soát 63 case, đối chiếu test, viết test cho 0 | 0 |
| FE-04 | Meeting Management | soát 116 case, đối chiếu test, viết test cho 16 | 16 |
| FE-05 | Agenda | soát 83 case, đối chiếu test, viết test cho 6 | 6 |
| FE-06 | Participants | soát 195 case, đối chiếu test, viết test cho 50 | 50 |
| FE-07 | Documents | soát 217 case, đối chiếu test, viết test cho 34 | 34 |
| FE-08 | Online Room | soát 136 case, đối chiếu test, viết test cho 27 | 27 |
| FE-09 | Recording | soát 126 case, đối chiếu test, viết test cho 15 | 15 |
| FE-10 | Transcription & Summary | soát 181 case, đối chiếu test, viết test cho 48 | 48 |
| FE-11 | Voting | soát 53 case, đối chiếu test, viết test cho 15 | 15 |
| FE-12 | Minutes | soát 83 case, đối chiếu test, viết test cho 14 | 14 |
| FE-13 | Notifications | soát 47 case, đối chiếu test, viết test cho 21 | 21 |
| FE-15 | Reporting | soát 21 case, đối chiếu test, viết test cho 3 | 3 |
| FE-16 | Calendar | soát 98 case, đối chiếu test, viết test cho 37 | 37 |

## 7. Case chưa có test tự động

Nhóm B1 là case không nhãn; nhóm B2 có nhãn: `chèn lỗi`, `ngoài` (dịch vụ ngoài), `thời gian`, `giới hạn tốc độ`.

Tổng: 259 case nhóm B1, 42 case nhóm B2.

**FE-01 Authentication** (10)

- AUTH_API_IT04: Verify POST /api/auth/login rejects empty credentials.
- AUTH_API_IT06: Verify POST /api/auth/login does not reveal a deactivated account to a wrong password.
- AUTH_API_IT23: Verify a capability change reaches the user at the next refresh without signing in again. _[thời gian]_
- AUTH_API_IT27: Verify signing out on one device leaves the other devices signed in.
- AUTH_API_IT31: Verify GET /api/auth/me refuses a Guest access token.
- AUTH_API_IT35: Verify a revoked invitation is refused.
- AUTH_API_IT43: Verify a code that is not 6 digits is refused.
- AUTH_API_IT53: Verify an empty display name is rejected.
- AUTH_API_IT54: Verify a display name or job title over 200 characters, or with control characters, is rejected.
- AUTH_API_IT55: Verify sending the same profile again is accepted.

**FE-02 User Management** (5)

- USER_API_IT08: Verify GET /api/users/{id} without a token answers 401.
- USER_API_IT09: Verify GET /api/users/{id} answers 403 for a caller without AdminUsers.
- USER_API_IT14: Verify POST /api/users rejects a full name over 200 characters and an email over 256 characters.
- USER_API_IT19: Verify POST /api/users without a token answers 401.
- USER_API_IT59: Verify POST /api/auth/password-reset answers MSG93 once the account was deactivated after the link was sent. _[thời gian]_

**FE-04 Meeting Management** (16)

- MEET_API_IT03: Verify POST /api/meetings stores a blank online link as null.
- MEET_API_IT08: Verify POST /api/meetings accepts a title of exactly 300 characters and rejects 301.
- MEET_API_IT10: Verify POST /api/meetings rejects an over-long location and description.
- MEET_API_IT13: Verify POST /api/meetings answers 400 for a body that is not valid JSON.
- MEET_API_IT18: Verify PUT /api/meetings/{id} with unchanged data succeeds without writing.
- MEET_API_IT27: Verify PUT /api/meetings/{id} allows a Draft start that is already past.
- MEET_API_IT35: Verify GET /api/meetings searches the title without case or accents.
- MEET_API_IT36: Verify GET /api/meetings filters by the overlap of the planned time with from and to. _[thời gian]_
- MEET_API_IT37: Verify GET /api/meetings sorts and pages as asked.
- MEET_API_IT42: Verify GET /api/meetings splits upcoming from past with timing. _[thời gian]_
- MEET_API_IT44: Verify GET /api/meetings?excludeDeclined=true hides meetings the caller declined.
- MEET_API_IT46: Verify GET /api/meetings is open to any signed-in account without MeetingCreator.
- MEET_API_IT53: Verify GET /api/meetings/{id} shows the caller their invitation answer.
- MEET_API_IT54: Verify GET /api/meetings/{id} requires a token.
- MEET_API_IT62: Verify GET /api/meetings/{id}/submission-readiness is for the Organizer only.
- MEET_API_IT112: Verify GET /api/meetings/{id}/history requires a token and hides meetings the caller cannot read.

**FE-05 Agenda** (6)

- AGENDA_API_IT07: Verify GET agenda requires a token.
- AGENDA_API_IT14: Verify editor-options answers 404 for a stranger and 401 without a token.
- AGENDA_API_IT15: Verify editor-options answers 500 MSG128 when it cannot be read. _[chèn lỗi]_
- AGENDA_API_IT41: Verify PUT item answers 404 for an item that is not in the meeting.
- AGENDA_API_IT48: Verify DELETE item answers 404 for an item that is not in the meeting and 401 without a token.
- AGENDA_API_IT63: Verify Start answers 404 for an item that is not in the meeting and 401 without a token.

**FE-06 Participants** (50)

- PART_API_IT02: Verify GET /api/meetings/participant-options never lists a deactivated account.
- PART_API_IT03: Verify GET /api/meetings/participant-options matches q on name or email, ignoring case and treating % and _ as plain characters.
- PART_API_IT04: Verify GET /api/meetings/participant-options returns an empty page past the last page.
- PART_API_IT05: Verify GET /api/meetings/participant-options accepts pageSize 100 and a 200-character q.
- PART_API_IT06: Verify GET /api/meetings/participant-options rejects pageSize 101, page 0 and a 201-character q.
- PART_API_IT07: Verify GET /api/meetings/participant-options rejects an unknown or repeated query key.
- PART_API_IT08: Verify GET /api/meetings/participant-options without a token answers 401.
- PART_API_IT09: Verify GET /api/meetings/participant-options answers 403 for an account without MeetingCreator.
- PART_API_IT21: Verify participant-context answers 404 for an unknown meeting id.
- PART_API_IT22: Verify participant-context without a token answers 401.
- PART_API_IT24: Verify participant-context rejects any query string.
- PART_API_IT29: Verify GET participants searches name, email and invited name.
- PART_API_IT31: Verify GET participants accepts pageSize 100 and rejects 101.
- PART_API_IT32: Verify GET participants rejects page 0, a 201-character q and an unknown query key.
- PART_API_IT37: Verify GET participants without a token answers 401.
- PART_API_IT40: Verify editor-options searches name and email and pages the result.
- PART_API_IT41: Verify editor-options rejects pageSize 101 and an unknown query key.
- PART_API_IT44: Verify editor-options answers 404 for someone who may not read the meeting.
- PART_API_IT45: Verify editor-options without a token answers 401.
- PART_API_IT55: Verify POST participants refuses a second Chair.
- PART_API_IT59: Verify POST participants rejects a query string.
- PART_API_IT65: Verify POST participants answers 404 for someone who may not read the meeting.
- PART_API_IT66: Verify POST participants without a token answers 401.
- PART_API_IT70: Verify PUT assignments sending the current role again is a no-op.
- PART_API_IT76: Verify PUT assignments lets a Draft meeting have no Secretary.
- PART_API_IT81: Verify PUT assignments rejects empty changes, duplicate ids, an unknown role and more than 100 changes.
- PART_API_IT84: Verify PUT assignments answers 404 for a participantId that is not a current participant of the meeting.
- PART_API_IT87: Verify PUT assignments answers 404 for someone who may not read the meeting, and 401 without a token.
- PART_API_IT88: Verify PUT assignments writes one audit row and notifies the new role holder only on a published meeting.
- PART_API_IT90: Verify DELETE participant clears the removed person as agenda presenter and from the speaking queue.
- PART_API_IT94: Verify DELETE participant answers 404 for an assignment that is not current in this meeting.
- PART_API_IT95: Verify DELETE participant rejects a request body.
- PART_API_IT99: Verify DELETE participant answers 404 for an unrelated account and 401 without a token.
- PART_API_IT100: Verify removing a person from a Published meeting notifies them and writes one audit row.
- PART_API_IT103: Verify POST guest-invitations trims the name and accepts the 200-character name and 256-character email limits.
- PART_API_IT107: Verify POST guest-invitations rejects the email of a current internal participant.
- PART_API_IT112: Verify POST guest-invitations answers 404 for someone who may not read the meeting.
- PART_API_IT113: Verify POST guest-invitations without a token answers 401.
- PART_API_IT115: Verify DELETE guest-invitations clears the Guest as agenda presenter.
- PART_API_IT117: Verify DELETE guest-invitations answers 404 for an unknown or already revoked invitation.
- PART_API_IT120: Verify DELETE guest-invitations answers 403/MSG85 for a Member, 404 for an unrelated account and 401 without a token.
- PART_API_IT124: Verify GET invitation-response answers 404 while the meeting is not yet published.
- PART_API_IT125: Verify GET invitation-response answers 404 for a reader with no invitation, such as an Organizer not on the roster.
- PART_API_IT126: Verify GET invitation-response answers 404 for someone who may not read the meeting, and 401 without a token.
- PART_API_IT127: Verify GET invitation-response rejects any query string.
- PART_API_IT135: Verify PUT invitation-response answers 404 for a reader who is not invited, such as the Organizer outside the roster.
- PART_API_IT136: Verify PUT invitation-response answers 404 for an unrelated account and 401 without a token.
- PART_API_IT143: Verify GET attendance pages the list and returns an empty page past the end.
- PART_API_IT150: Verify POST attendance can mark a Guest.
- PART_API_IT173: Verify chair-handover-candidates answers 403 with an empty body for a deactivated Chair.

**FE-07 Documents** (34)

- DOC_API_IT05: Verify GET /api/documents returns an empty documents array, not an error, for an empty location.
- DOC_API_IT10: Verify GET /api/documents without a token answers 401.
- DOC_API_IT11: Verify GET /api/documents refuses a Guest token with an empty 403.
- DOC_API_IT21: Verify POST /api/documents/upload without a token answers 401.
- DOC_API_IT23: Verify GET /api/documents/{documentId}/content?download=true also streams the file for the Owner.
- DOC_API_IT27: Verify GET /api/documents/{documentId}/content?download=true answers 503 MSG160 when the file store fails. _[chèn lỗi · ngoài]_
- DOC_API_IT28: Verify GET /api/documents/{documentId}/content without a token answers 401.
- DOC_API_IT33: Verify PUT /api/documents/{documentId}/name rejects a name longer than 255 characters (boundary: 256).
- DOC_API_IT36: Verify PUT /api/documents/{documentId}/name without a token answers 401.
- DOC_API_IT42: Verify PUT /api/documents/{documentId}/folder without a token answers 401.
- DOC_API_IT46: Verify DELETE /api/documents/{documentId} answers 404 MSG175 for a document already deleted.
- DOC_API_IT48: Verify DELETE /api/documents/{documentId} without a token answers 401.
- DOC_API_IT56: Verify POST /api/folders without a token answers 401.
- DOC_API_IT62: Verify PUT /api/folders/{folderId}/name without a token answers 401.
- DOC_API_IT72: Verify PUT /api/folders/{folderId}/parent without a token answers 401.
- DOC_API_IT79: Verify DELETE /api/folders/{folderId} without a token answers 401.
- DOC_API_IT84: Verify GET /api/users/{userId}/library answers 401 without a token.
- DOC_API_IT88: Verify the content of a missing object answers 410 MSG382. _[ngoài]_
- DOC_API_IT90: Verify the content endpoint answers 401 without a token.
- DOC_API_IT101: Verify GET /api/meetings/{meetingId}/documents answers 401 without a token.
- DOC_API_IT114: Verify POST /api/meetings/{meetingId}/documents answers 401 without a token.
- DOC_API_IT131: Verify POST /api/meetings/{meetingId}/documents/upload without a token answers 401.
- DOC_API_IT142: Verify GET .../content?download=maybe answers 400 without a code.
- DOC_API_IT143: Verify GET .../content answers 401 without a token.
- DOC_API_IT152: Verify DELETE without a token answers 401.
- DOC_API_IT161: Verify POST without a token answers 401.
- DOC_API_IT168: Verify POST .../from-library without a token answers 401.
- DOC_API_IT172: Verify POST .../upload rejects 11 files (boundary).
- DOC_API_IT176: Verify POST .../upload answers 503 MSG166 when the file store fails. _[chèn lỗi · ngoài]_
- DOC_API_IT177: Verify POST .../upload without a token answers 401.
- DOC_API_IT184: Verify DELETE without a token answers 401.
- DOC_API_IT192: Verify GET .../comments answers 401 without a token.
- DOC_API_IT207: Verify POST .../comments answers 401 without a token.
- DOC_API_IT217: Verify POST .../read-confirmation answers 401 without a token.

**FE-08 Online Room** (27)

- ROOM_API_IT06: Verify GET /api/meetings/{id}/call reports available false when LiveKit is not configured. _[ngoài]_
- ROOM_API_IT09: Verify a member who delegated attendance cannot read the room. _[ngoài]_
- ROOM_API_IT11: Verify a deactivated account is refused with 403. _[ngoài]_
- ROOM_API_IT22: Verify a deactivated account cannot join. _[ngoài]_
- ROOM_API_IT32: Verify the former Chair is refused right after handing the Chair role over. _[thời gian · ngoài]_
- ROOM_API_IT38: Verify a signed-in user not on the roster gets 404. _[ngoài]_
- ROOM_API_IT39: Verify the mute endpoint without a token answers 401.
- ROOM_API_IT40: Verify muting when LiveKit is not configured answers 503 MSG281. _[chèn lỗi · ngoài]_
- ROOM_API_IT45: Verify a webhook body over 1 MB answers 413. _[ngoài]_
- ROOM_API_IT56: Verify the Organizer who is not on the roster still reads the queue.
- ROOM_API_IT72: Verify an invited person can still lower the hand before starting. _[ngoài]_
- ROOM_API_IT75: Verify lowering a hand during one's own speaking turn answers 409 MSG460. _[ngoài]_
- ROOM_API_IT76: Verify lowering one's hand from outside the room answers 409 MSG458. _[ngoài]_
- ROOM_API_IT77: Verify DELETE speaking-requests without a token answers 401.
- ROOM_API_IT79: Verify the Chair can invite from the meeting page without being in the room. _[ngoài]_
- ROOM_API_IT82: Verify inviting a request that is no longer queued answers 409 MSG460. _[ngoài]_
- ROOM_API_IT86: Verify starting a turn that was not invited answers 409 MSG460. _[ngoài]_
- ROOM_API_IT88: Verify starting one's turn from outside the room answers 409 MSG458. _[ngoài]_
- ROOM_API_IT89: Verify the start endpoint without a token answers 401.
- ROOM_API_IT91: Verify finishing an invited turn that never started cancels it. _[ngoài]_
- ROOM_API_IT96: Verify the finish endpoint without a token answers 401.
- ROOM_API_IT98: Verify declining an invited person withdraws the invitation and frees the floor. _[ngoài]_
- ROOM_API_IT101: Verify the decline endpoint without a token answers 401.
- ROOM_API_IT106: Verify the move-up endpoint without a token answers 401.
- ROOM_API_IT111: Verify the move-down endpoint without a token answers 401.
- ROOM_API_IT120: Verify a deactivated Secretary is refused with 403.
- ROOM_API_IT135: Verify PUT notes without a token answers 401.

**FE-09 Recording** (15)

- REC_API_IT04: Verify GET /api/meetings/{meetingId}/recordings without a token answers 401.
- REC_API_IT15: Verify POST /api/meetings/{meetingId}/recordings rejects a request that is not multipart or carries no file.
- REC_API_IT24: Verify POST /api/meetings/{meetingId}/recordings without a token answers 401.
- REC_API_IT35: Verify PUT /api/recordings/{recordingId}/parts/{part} without a token answers 401.
- REC_API_IT43: Verify POST /api/recordings/{recordingId}/complete answers 404 for a user who is not on the participant list and 401 without a token.
- REC_API_IT52: Verify POST /api/meetings/{meetingId}/recordings/takes without a token answers 401.
- REC_API_IT57: Verify DELETE /api/recordings/{recordingId}/takes/{take} answers 404 for a user who is not on the participant list and 401 without a token.
- REC_API_IT62: Verify DELETE /api/recordings/{recordingId} without a token answers 401.
- REC_API_IT69: Verify GET /api/recordings/{recordingId}/playback without a token answers 401 and with a Guest token answers 403.
- REC_API_IT82: Verify GET /api/recordings/{recordingId}/download answers 404 for an erased recording and 401 without a token.
- REC_API_IT90: Verify GET /api/meetings/{meetingId}/speech-clips without a token answers 401.
- REC_API_IT101: Verify PUT /api/meetings/{meetingId}/speech-clips/{clientClipId} answers 404 for a user who is not on the participant list and 401 without a token.
- REC_API_IT108: Verify GET /api/speech-clips/{clipId}/playback without a token answers 401.
- REC_API_IT120: Verify PUT /api/speech-clips/{clipId}/exclusion answers 404 for a user who is not on the participant list and 401 without a token.
- REC_API_IT125: Verify DELETE /api/speech-clips/{clipId} answers 404 for a user who is not on the participant list and 401 without a token.

**FE-10 Transcription & Summary** (48)

- TRANS_API_IT03: Verify POST /api/recordings/{recordingId}/transcribe redoes a finished transcript and records the rerun.
- TRANS_API_IT06: Verify POST /api/recordings/{recordingId}/transcribe answers 404 for a recording the caller may not hear.
- TRANS_API_IT08: Verify POST /api/recordings/{recordingId}/transcribe without a token answers 401.
- TRANS_API_IT09: Verify POST /api/recordings/{recordingId}/transcribe refuses a deactivated account whose token is still valid.
- TRANS_API_IT12: Verify GET /api/recordings/{recordingId}/transcript lets the Chair export but not edit.
- TRANS_API_IT13: Verify GET /api/recordings/{recordingId}/transcript reports status None for a recording not yet sent to speech to text.
- TRANS_API_IT15: Verify GET /api/recordings/{recordingId}/transcript answers 404 for a user who is not on the roster and for an unknown recording.
- TRANS_API_IT16: Verify GET /api/recordings/{recordingId}/transcript without a token answers 401.
- TRANS_API_IT18: Verify GET /api/recordings/{recordingId}/transcript.txt is also open to the Chair.
- TRANS_API_IT22: Verify GET /api/recordings/{recordingId}/transcript.txt answers 404 for a user not on the roster.
- TRANS_API_IT23: Verify GET /api/recordings/{recordingId}/transcript.txt without a token answers 401.
- TRANS_API_IT27: Verify GET /api/meetings/{meetingId}/speech-clips.txt answers 404 for a user not on the roster.
- TRANS_API_IT28: Verify GET /api/meetings/{meetingId}/speech-clips.txt without a token answers 401.
- TRANS_API_IT31: Verify PUT /api/transcript-segments/{segmentId} accepts a text of exactly 8000 characters.
- TRANS_API_IT32: Verify PUT /api/transcript-segments/{segmentId} rejects a text of 8001 characters.
- TRANS_API_IT34: Verify PUT /api/transcript-segments/{segmentId} answers 404 for an unknown segment and for a user not on the roster.
- TRANS_API_IT35: Verify PUT /api/transcript-segments/{segmentId} without a token answers 401.
- TRANS_API_IT38: Verify PUT /api/speech-clips/{clipId}/text accepts 8000 characters and rejects 8001.
- TRANS_API_IT41: Verify PUT /api/speech-clips/{clipId}/text answers 404 for an unknown sentence and for a user not on the roster.
- TRANS_API_IT42: Verify PUT /api/speech-clips/{clipId}/text without a token answers 401.
- TRANS_API_IT47: Verify PUT /api/transcript-segments/{segmentId}/speaker changes nothing when the line already belongs to that voice.
- TRANS_API_IT50: Verify PUT /api/transcript-segments/{segmentId}/speaker answers 400 when all hundred voice labels are taken.
- TRANS_API_IT51: Verify PUT /api/transcript-segments/{segmentId}/speaker answers 404 for a label that is not a voice of the recording.
- TRANS_API_IT53: Verify PUT /api/transcript-segments/{segmentId}/speaker answers 404 for an unknown segment and 401 without a token.
- TRANS_API_IT57: Verify PUT /api/recordings/{recordingId}/speakers/{label} clears a name when both userId and guestId are null.
- TRANS_API_IT60: Verify PUT /api/recordings/{recordingId}/speakers/{label} answers 404 for a label the recording does not have.
- TRANS_API_IT62: Verify PUT /api/recordings/{recordingId}/speakers/{label} answers 404 for a user not on the roster and 401 without a token.
- TRANS_API_IT102: Verify GET /api/me/voice without a token answers 401 and refuses a deactivated account.
- TRANS_API_IT109: Verify POST /api/me/voice/consent without a token answers 401.
- TRANS_API_IT112: Verify DELETE /api/me/voice/consent answers 200 even when no consent stands.
- TRANS_API_IT117: Verify DELETE /api/me/voice/consent without a token answers 401.
- TRANS_API_IT124: Verify POST /api/me/voice/samples rejects a recording with more than one speaker or unreadable audio. _[ngoài]_
- TRANS_API_IT125: Verify POST /api/me/voice/samples rejects a recording longer than 60 seconds or larger than 10 MB. _[ngoài]_
- TRANS_API_IT126: Verify POST /api/me/voice/samples refuses an audio type that is not supported.
- TRANS_API_IT127: Verify POST /api/me/voice/samples refuses a request without a file or that is not multipart.
- TRANS_API_IT128: Verify POST /api/me/voice/samples answers 503 when the speech worker is not running. _[chèn lỗi · ngoài]_
- TRANS_API_IT130: Verify POST /api/me/voice/samples without a token answers 401.
- TRANS_API_IT135: Verify GET /api/me/voice/samples/{sampleId}/audio answers 404 for an unknown id and 401 without a token.
- TRANS_API_IT137: Verify DELETE /api/me/voice/samples/{sampleId} frees a place under the limit of 5. _[ngoài]_
- TRANS_API_IT140: Verify DELETE /api/me/voice/samples/{sampleId} without a token answers 401.
- TRANS_API_IT151: Verify POST /api/users/{userId}/voice-collection/samples applies the same quality checks as a person's own sample. _[ngoài]_
- TRANS_API_IT152: Verify POST /api/users/{userId}/voice-collection/samples answers 503 when the speech worker is not running. _[chèn lỗi · ngoài]_
- TRANS_API_IT162: Verify GET /api/admin/ai without a token answers 401.
- TRANS_API_IT163: Verify GET /api/admin/ai refuses an Administrator whose role lost AdminUsers after the token was issued. _[thời gian]_
- TRANS_API_IT168: Verify PUT /api/admin/ai refuses a missing address when the API is chosen and an invalid mode.
- TRANS_API_IT169: Verify PUT /api/admin/ai refuses a key with spaces or outside 8 to 500 characters and a model name with spaces or over 100 characters.
- TRANS_API_IT170: Verify PUT /api/admin/ai refuses removing the key while the API is in use.
- TRANS_API_IT176: Verify POST /api/admin/ai/test reports the in-house machine as not configured or not answering. _[ngoài]_

**FE-11 Voting** (15)

- VOTE_API_IT02: Verify GET /api/meetings/{meetingId}/votes answers 404 for a caller who may not read the meeting.
- VOTE_API_IT03: Verify GET /api/meetings/{meetingId}/votes without a token answers 401.
- VOTE_API_IT06: Verify GET /api/meetings/{meetingId}/votes/{voteId} answers 404 for an unknown vote or another meeting's vote.
- VOTE_API_IT19: Verify attaching a document that is not in the meeting answers 404.
- VOTE_API_IT20: Verify attaching an already linked document answers 409.
- VOTE_API_IT23: Verify attaching from the library with no documents answers 400.
- VOTE_API_IT25: Verify uploading a non-PDF/DOCX file answers 400.
- VOTE_API_IT26: Verify uploading more than 10 files or 100 MB in one request answers 400.
- VOTE_API_IT27: Verify uploading to a draft answers 409 MSG346 when the system document storage is full.
- VOTE_API_IT30: Verify detaching a document that was never linked answers 404.
- VOTE_API_IT32: Verify opening a draft answers 409 when the caller is not the current Chair.
- VOTE_API_IT39: Verify voting on a vote that is no longer open answers 409.
- VOTE_API_IT47: Verify closing an open vote answers 409 for anyone but the current Chair.
- VOTE_API_IT50: Verify a conclusion outside Passed/NotPassed/Deferred answers 400.
- VOTE_API_IT51: Verify recording a conclusion answers 409 when the vote is not closed.

**FE-12 Minutes** (14)

- MIN_API_IT05: Verify POST /api/meetings/{meetingId}/minutes rebuilds a returned revision and keeps it returned.
- MIN_API_IT20: Verify GET /api/meetings/{meetingId}/minutes refuses a deactivated account and a Guest token.
- MIN_API_IT24: Verify GET /api/minutes/{minutesId} lets the Chair and the Secretary open any revision.
- MIN_API_IT26: Verify GET /api/minutes/{minutesId} answers 404 for an outsider or an unknown id.
- MIN_API_IT27: Verify GET /api/minutes/{minutesId} without a token answers 401.
- MIN_API_IT28: Verify GET /api/minutes/{minutesId} answers MSG403 when the revision cannot be read. _[chèn lỗi]_
- MIN_API_IT58: Verify POST /api/minutes/{minutesId}/submit without a token answers 401.
- MIN_API_IT65: Verify POST /api/minutes/{minutesId}/approve issues nothing when the state cannot be saved. _[chèn lỗi]_
- MIN_API_IT66: Verify POST /api/minutes/{minutesId}/approve without a token answers 401.
- MIN_API_IT73: Verify POST /api/minutes/{minutesId}/reject keeps only the reason of the latest return.
- MIN_API_IT74: Verify POST /api/minutes/{minutesId}/reject changes nothing when the state cannot be saved. _[chèn lỗi]_
- MIN_API_IT75: Verify POST /api/minutes/{minutesId}/reject without a token answers 401.
- MIN_API_IT82: Verify POST /api/minutes/{minutesId}/corrections creates nothing when the revision cannot be saved. _[chèn lỗi]_
- MIN_API_IT83: Verify POST /api/minutes/{minutesId}/corrections without a token answers 401.

**FE-13 Notifications** (21)

- NOTI_API_IT02: Verify GET /api/notifications pages the list with page and pageSize.
- NOTI_API_IT03: Verify GET /api/notifications accepts the maximum pageSize of 100.
- NOTI_API_IT05: Verify GET /api/notifications rejects pageSize above 100 with MSG39.
- NOTI_API_IT06: Verify GET /api/notifications rejects non-numeric paging values with MSG39.
- NOTI_API_IT11: Verify GET /api/notifications returns an empty page for an account with no notification.
- NOTI_API_IT14: Verify GET /api/notifications/unread-count counts only the caller's notifications.
- NOTI_API_IT15: Verify GET /api/notifications/unread-count answers 401 without a token.
- NOTI_API_IT16: Verify GET /api/notifications/unread-count answers 403 for a deactivated account.
- NOTI_API_IT20: Verify POST /api/notifications/{id}/read answers 404 for an unknown id.
- NOTI_API_IT21: Verify POST /api/notifications/{id}/read answers 404 for an id that is not a GUID.
- NOTI_API_IT22: Verify POST /api/notifications/{id}/read answers 401 without a token.
- NOTI_API_IT24: Verify POST /api/notifications/read-all with nothing unread changes nothing.
- NOTI_API_IT26: Verify POST /api/notifications/read-all answers 401 without a token.
- NOTI_API_IT28: Verify GET /api/notifications/{id}/target does not mark the notification read.
- NOTI_API_IT31: Verify GET /api/notifications/{id}/target answers 404 for a notification with no target.
- NOTI_API_IT32: Verify GET /api/notifications/{id}/target returns the sharing page for a calendar-share notification.
- NOTI_API_IT33: Verify GET /api/notifications/{id}/target answers 404 for a calendar-share notice of someone who is not a party.
- NOTI_API_IT34: Verify GET /api/notifications/{id}/target returns the Voice ID page for a voice-sample notification.
- NOTI_API_IT35: Verify GET /api/notifications/{id}/target answers 401 without a token.
- NOTI_API_IT39: Verify POST /api/email/resend/webhook ignores an event older than the stored delivery state. _[ngoài]_
- NOTI_API_IT45: Verify POST /api/email/resend/webhook answers 413 for a body above 256 KB. _[ngoài]_

**FE-15 Reporting** (3)

- RPT_API_IT03: Verify GET /api/reports/meetings counts a Cancelled meeting.
- RPT_API_IT05: Verify GET /api/reports/meetings counts only the meetings the caller may read, administrator included.
- RPT_API_IT16: Verify GET /api/reports/meetings/export without a period exports the twelve months ending this month.

**FE-16 Calendar** (37)

- CAL_API_IT04: Verify GET /api/calendar/shares answers 401 without a token.
- CAL_API_IT05: Verify GET /api/calendar/shares answers 403 without a body for a deactivated account.
- CAL_API_IT10: Verify GET /api/calendar/share-candidates rejects a bad query with 400 and no body.
- CAL_API_IT12: Verify GET /api/calendar/share-candidates answers 401 without a token.
- CAL_API_IT13: Verify GET /api/calendar/share-candidates answers 500 MSG467 when the database cannot be read. _[chèn lỗi]_
- CAL_API_IT16: Verify POST /api/calendar/shares rejects a range longer than 365 days with MSG443.
- CAL_API_IT17: Verify POST /api/calendar/shares rejects a start date in the past with MSG443.
- CAL_API_IT18: Verify POST /api/calendar/shares rejects an end date before the start date with MSG443.
- CAL_API_IT19: Verify POST /api/calendar/shares rejects a missing or unknown level with MSG443.
- CAL_API_IT24: Verify POST /api/calendar/shares answers 401 without a token.
- CAL_API_IT28: Verify POST /api/calendar/shares/direct answers 409 MSG449 for a duplicate open share.
- CAL_API_IT29: Verify POST /api/calendar/shares/direct rejects a bad range with MSG443.
- CAL_API_IT30: Verify POST /api/calendar/shares/direct works for a Member who is not an Organizer.
- CAL_API_IT31: Verify POST /api/calendar/shares/direct answers 401 without a token.
- CAL_API_IT33: Verify POST /api/calendar/shares/{id}/accept refuses a wider range or higher level than requested with MSG443. _[thời gian]_
- CAL_API_IT39: Verify POST /api/calendar/shares/{id}/accept answers 409 MSG450 for a closed share.
- CAL_API_IT41: Verify POST /api/calendar/shares/{id}/accept answers 401 without a token.
- CAL_API_IT46: Verify POST /api/calendar/shares/{id}/decline answers 409 MSG450 for a closed share.
- CAL_API_IT47: Verify POST /api/calendar/shares/{id}/decline answers 401 without a token.
- CAL_API_IT49: Verify PATCH /api/calendar/shares/{id} refuses a viewer request that lowers or does not raise anything with MSG443.
- CAL_API_IT50: Verify PATCH /api/calendar/shares/{id} refuses a total range over 365 days from the start date.
- CAL_API_IT52: Verify PATCH /api/calendar/shares/{id} answers 409 MSG450 when a change is already pending.
- CAL_API_IT54: Verify PATCH /api/calendar/shares/{id} answers 404 for someone who is not a party of the share.
- CAL_API_IT55: Verify PATCH /api/calendar/shares/{id} answers 401 without a token.
- CAL_API_IT58: Verify POST /api/calendar/shares/{id}/withdraw answers 409 MSG450 when nothing is pending.
- CAL_API_IT59: Verify POST /api/calendar/shares/{id}/withdraw answers 404 for a stranger.
- CAL_API_IT60: Verify POST /api/calendar/shares/{id}/withdraw answers 401 without a token.
- CAL_API_IT62: Verify POST /api/calendar/shares/{id}/end answers 409 MSG450 for a share that is not Effective.
- CAL_API_IT66: Verify POST /api/calendar/shares/{id}/end answers 404 for a stranger and 401 without a token.
- CAL_API_IT68: Verify GET /api/calendar/shared-week without a week shows the current week in Vietnam time.
- CAL_API_IT69: Verify GET /api/calendar/shared-week lists a Stopped share without days.
- CAL_API_IT71: Verify GET /api/calendar/shared-week rejects a malformed week with 400 and no body.
- CAL_API_IT73: Verify GET /api/calendar/shared-week answers 401 without a token and 500 MSG453 when the database fails. _[chèn lỗi]_
- CAL_API_IT81: Verify POST /api/calendar/slots accepts 10 people and rejects 11.
- CAL_API_IT82: Verify POST /api/calendar/slots enforces the duration and date-range limits.
- CAL_API_IT83: Verify POST /api/calendar/slots refuses a duration longer than one working session.
- CAL_API_IT97: Verify POST /api/calendar/slots/draft answers 401 without a token.

## 8. Mẫu lệnh giao cho một agent

> Đọc `docs/08-reports/test-reports/round1/plan-api.md` và `docs/08-reports/test-reports/AGENTS.md`. Làm gói **FE-xx**: (0) soát các case trong `cases/FE-xx/api.json` theo mục 2, sửa JSON nếu sai; (1) đối chiếu kết quả `dotnet test` với các case có `Automated:`; (2) viết test xUnit cho các case `Manual only` theo mục 4 (nhóm B1 trước, B2 nếu làm được) và đổi `note` sang `Automated:`. Điền `round1`/`date1`/`tester1` (`Phạm Gia Khánh`) theo mục 5. Không sửa code production để test xanh, không commit. Cuối cùng báo: số case Passed / Failed / N/A / để trống, danh sách case đã sửa nội dung và lý do, và các bug nghi là thật kèm bằng chứng.

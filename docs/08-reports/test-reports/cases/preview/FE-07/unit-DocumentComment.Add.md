# DocumentComment.Add — Unit (FE-07, DOC)

Description: Document comments: who may read and add, text limits.

Precondition: Document readable by the actor.

## DocumentCommentService.AddAsync

Layer: Application · `backend/TrustCabinet.Application/Documents/DocumentCommentService.cs:56`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Meeting Published; actor "An" is a Member; M1 is a PDF with 10 pages in the meeting. | body: "Đề nghị bổ sung số liệu quý 3."; pageNumber: 4; parentCommentId: null. | Return: Success; comment returned with body, page 4, author An, no replies; endpoint answers MSG199.<br>Exception: N/A<br>Log Message: DocumentComment row saved with AuthorId An, PageNumber 4, CreatedAt = now; a share lock taken on the meeting row. | Normal | UC-35 · BR-248 · BR-249 · MSG199 |
| UTC002 | Same document. | body: "   " (spaces only). | Return: Failure Blank (HTTP 400, MSG200); nothing is read from the database.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-35 · MSG200 |
| UTC003 | Same document. | body: null. | Return: Failure Blank (HTTP 400, MSG200).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-35 · MSG200 |
| UTC004 | Same document. | body: 4,000 characters of "a". | Return: Success; the comment is saved with all 4,000 characters.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-35 · MSG199 |
| UTC005 | Same document. | body: 4,001 characters of "a". | Return: Failure TooLong (HTTP 400, MSG348).<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-35 · MSG348 |
| UTC006 | Same document. | body: 4,000 characters of "a" surrounded by 5 spaces on each side. | Return: Success; the body is trimmed before the length check so it is 4,000 characters.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-35 · MSG199 |
| UTC007 | Actor cannot read the meeting (not a participant). | body: "Góp ý". | Return: Failure NotFound (HTTP 404, MSG156).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-35 · MSG156 |
| UTC008 | Meeting does not exist. | meetingId: unknown. | Return: Failure NotFound (HTTP 404, MSG156).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-35 · MSG156 |
| UTC009 | M1 was removed from the meeting. | meetingDocumentId: M1. | Return: Failure NotFound (HTTP 404, MSG156).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-35 · BR-211 · BR-284 · MSG156 |
| UTC010 | M3 is attached only to a draft vote; actor is a Member. | meetingDocumentId: M3. | Return: Failure NotFound (HTTP 404, MSG156).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-35 · BR-253 · MSG156 |
| UTC011 | Same draft-vote document; actor is the Chair. | meetingDocumentId: M3; body: "Ok". | Return: Success; managers see draft-vote documents.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-35 · BR-253 · MSG199 |
| UTC012 | Meeting Ended. | body: "Thêm ý kiến". | Return: Failure Ended (HTTP 409, MSG251); comments are read-only after the meeting ends.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-35 · BR-282 · MSG251 |
| UTC013 | Meeting Cancelled (it had been Published, so participants can still open it). | body: "Thêm ý kiến". | Return: Failure Cancelled (HTTP 409, MSG349).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-35 · BR-299 · MSG349 |
| UTC014 | Meeting InProgress; thread T1 (top-level comment) exists on M1. | body: "Đồng ý."; parentCommentId: T1; pageNumber: 3. | Return: Success; reply attached to T1, page number dropped (null) because a reply has no page.<br>Exception: N/A<br>Log Message: DocumentComment row with ParentCommentId = T1. | Normal | UC-35 · BR-248 · MSG199 |
| UTC015 | R1 is a reply to T1. | body: "Tôi cũng vậy."; parentCommentId: R1. | Return: Success; the new comment joins thread T1 (ParentCommentId = T1), one level deep.<br>Exception: N/A<br>Log Message: DocumentComment row with ParentCommentId = T1, not R1. | Boundary | UC-35 · BR-317 · MSG199 |
| UTC016 | Comment C9 belongs to another document. | parentCommentId: C9. | Return: Failure NotFound (HTTP 404, MSG156).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-35 · MSG156 |
| UTC017 | M1 is a PDF with 10 pages. | pageNumber: 10; top-level. | Return: Success; page 10 kept (last page).<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-35 · BR-248 |
| UTC018 | M1 is a PDF with 10 pages. | pageNumber: 11; top-level. | Return: Success; comment saved without a page (null).<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-35 · BR-248 |
| UTC019 | M1 is a PDF with 10 pages. | pageNumber: 0; top-level. | Return: Success; page dropped (null) because pages start at 1.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-35 · BR-248 |
| UTC020 | M1 is a PDF whose page count is unknown (null). | pageNumber: 50; top-level. | Return: Success; page 50 kept as the document has no known limit.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-35 · BR-248 |
| UTC021 | M2 is a DOCX. | pageNumber: 2; top-level. | Return: Success; page dropped (null) because only PDF comments carry a page.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-35 · BR-248 |
| UTC022 | Meeting Published; actor is a verified guest with a current session of this meeting, invitation I1, display name "Khách A". | actor: Guest; body: "Xin ý kiến". | Return: Success; author shown as a guest named "Khách A"; endpoint answers MSG199.<br>Exception: N/A<br>Log Message: DocumentComment row with GuestInvitationId I1 and no AuthorId; a share lock taken on the guest session row. | Normal | UC-35 · BR-181 · MSG199 |
| UTC023 | Guest whose session has been signed out or has expired. | actor: Guest with IsValid false. | Return: Failure NotFound (HTTP 404, MSG156).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-35 · BR-181 · MSG156 |
| UTC024 | Guest whose invitation was revoked. | actor: Guest. | Return: Failure NotFound (HTTP 404, MSG156); rechecked on every call.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-35 · BR-181 · MSG156 |
| UTC025 | Guest of meeting X posting to meeting Y. | actor: Guest of X; meetingId: Y. | Return: Failure NotFound (HTTP 404, MSG156).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-35 · MSG156 |
| UTC026 | Guest session valid but the meeting is Ended. | actor: Guest. | Return: Failure NotFound (HTTP 404, MSG156), not MSG251: the guest has lost the meeting altogether.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-35 · BR-181 · MSG156 |
| UTC027 | Database error while saving. | body: "Góp ý". | Return: Failure SaveFailed (HTTP 503, MSG202); no comment is saved.<br>Exception: N/A<br>Log Message: Error log "Failed to save a comment on document". | Abnormal | UC-35 · MSG202 |

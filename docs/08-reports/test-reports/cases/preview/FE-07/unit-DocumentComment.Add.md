# DocumentComment.Add — Unit (FE-07, DOC)

Description: Document comments: who may read and add, text limits.

Precondition: Document readable by the actor.

## DocumentCommentService.AddAsync

Layer: Application · `backend/TrustCabinet.Application/Documents/DocumentCommentService.cs:56`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Meeting Published; actor "An" is a Member; M1 is a PDF with 10 pages in the meeting. | body: "Đề nghị bổ sung số liệu quý 3."; pageNumber: 4; parentCommentId: null. | Return: Success; comment returned with body, page 4, author An, no replies; endpoint answers MSG199.<br>Exception: N/A<br>Log Message: DocumentComment row saved with AuthorId An, PageNumber 4, CreatedAt = now; a share lock taken on the meeting row. | Normal | UC-35 · BR-248 · BR-249 · MSG199 |
| UTC002 | Same document. | body: "   " (spaces only). | Return: Failure Blank (HTTP 400, MSG200); nothing is read from the database.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-35 · MSG200 |
| UTC003 | Same document. | body: 4,000 characters of "a". | Return: Success; the comment is saved with all 4,000 characters.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-35 · MSG199 |
| UTC004 | Same document. | body: 4,001 characters of "a". | Return: Failure TooLong (HTTP 400, MSG348).<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-35 · MSG348 |
| UTC005 | One candidate of 10 MB plus 1 byte. | candidates: [{FileName "lon.pdf", Length 10,485,761}]. | Return: Failure Invalid (MSG164); errors["files[0]"] = ["lon.pdf: tệp lớn hơn 10 MB."]; the bytes are not read.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-35 · BR-282 · MSG251 |

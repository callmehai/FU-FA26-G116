# DocumentFile.Validate — Unit (FE-07, DOC)

Description: Meeting document upload gate: access, file count, sizes, type agreement and hostile content.

Precondition: Active user who may read the meeting; candidate files with names and bytes.

## DocumentFileRules.ValidateAsync

Layer: Domain · `backend/TrustCabinet.Domain/Documents/DocumentFileRules.cs:94`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | One candidate: a 2 MB file "Bao-cao-Q3.pdf" whose bytes are a valid 12-page PDF; page counter returns 12. | candidates: [{FileName "Bao-cao-Q3.pdf", Length 2,097,152}]. | Return: Outcome with no failure and one file: name "Bao-cao-Q3.pdf", content type application/pdf, 2,097,152 bytes, 12 pages, lowercase hex SHA-256 of the bytes.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-36a · BR-069 · UC-36a, UC-39, NFR |
| UTC002 | Eleven candidates submitted together. | candidates: 11 files of 1 MB each. | Return: Failure TooMany (HTTP 400, MSG288); no file is inspected.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-36a · UC-36a, UC-36b, UC-39 · MSG288 |
| UTC003 | Ten files whose lengths add to more than 100 MB. | candidates: 10 files of 11 MB each (115,343,360 bytes; each also exceeds the 10 MB per-file limit, but the submission-total gate fires first). | Return: Failure TooMany (HTTP 400, MSG288).<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-36a · UC-36a, UC-36b, UC-39 · MSG288 |
| UTC004 | One candidate of 10 MB plus 1 byte. | candidates: [{FileName "lon.pdf", Length 10,485,761}]. | Return: Failure Invalid (MSG164); errors["files[0]"] = ["lon.pdf: tệp lớn hơn 10 MB."]; the bytes are not read.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-36a · UC-36a, UC-39, NFR · MSG164 |

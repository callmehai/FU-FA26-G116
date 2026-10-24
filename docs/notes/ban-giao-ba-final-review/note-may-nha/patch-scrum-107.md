# Patch Report 3 — SCRUM-107 (tải tài liệu từ máy lên cuộc họp)

**Ngày:** 24/10/2026 · **Lượt 2** theo [05-plan](../05-plan-sua-uc-br-msg-diagram.md) mục 8 · **Nguồn:** [07](../07-scrum-107-tai-len-tai-lieu.md),
[API tài liệu cuộc họp](../../../03-api/11-tai-lieu-cuoc-hop.md), [ADR-0024](../../../05-decisions/0024-luu-file-tai-lieu-tren-cloudflare-r2.md),
catalogue MSG164/MSG174/MSG288.

**Chưa áp dụng vào DOCX.** Khánh dán các dòng dưới vào `Report_3_...docx`, rồi chạy `python scripts/sync_reports.py --report 3`.
Bản Markdown `05-use-case-specifications.md`, `06-business-rules.md` và bảng tiếng Anh `00-danh-muc-ma-thong-bao.md` đã sửa tay
cho khớp patch này, nên sau khi sync, `git diff` của hai file đầu phải trống (nếu không, bản dán lệch bảng dưới).
Các đoạn không ghi ở đây giữ nguyên.

Tên cột ở bảng dưới: **Mục** (vị trí trong Report 3) · **Ô / bước** · **Câu cũ** · **Câu mới**.

## 1. II.7.2.1 UC-33a – Add Document to Meeting

Ý chính: AF-1 đảo bước. Kiểm quyền, quyền xem và trạng thái cuộc họp lên **Step 2**, kiểm số tệp / dung lượng / loại tệp xuống **Step 3**.
Người không được tải không phải đẩy tệp lên server, và câu trả lời "có được tải không" không phụ thuộc nội dung đã gửi. Hệ quả: người dùng
không còn là Chủ tọa/Thư ký gửi tệp sai nhận MSG162, không phải MSG164. Mọi điểm vào của EX đổi theo số bước mới.

| Mục | Ô / bước | Câu cũ | Câu mới |
|---|---|---|---|
| II.7.2.1 | Alternative Flows, AF-1, bước 2 | `2. The system checks every file is a valid PDF or DOCX and each file is no larger than 50 MB, and that the submission has at most 10 files totalling at most 200 MB. It identifies any file that fails validation. (BR-187, BR-214, BR-215, BR-319)` | `2. Before reading any file, the system performs the authority, Meeting access and status checks from Step 4 of the Main Flow. An actor who is not the current Chair or Secretary receives MSG162 whatever the files are. (BR-184, BR-195)` |
| II.7.2.1 | AF-1, bước 3 | `3. The system performs the authority, Meeting access and status checks from Step 4 of the Main Flow. (BR-184, BR-195)` | `3. The system checks that the submission has at most 10 files totalling at most 200 MB, and that every file is no larger than 50 MB and is a valid PDF or DOCX: its extension is .pdf or .docx and matches its actual content. It identifies any file that fails validation. (BR-187, BR-214, BR-215, BR-319)` |
| II.7.2.1 | AF-1, bước 4 và 5 | — | Giữ nguyên |
| II.7.2.1 | EX-1, bước 1 | `1. At Step 4, or the corresponding check in AF-1, the actor is no longer the current Chair or Secretary.` | `1. At Step 4, or at Step 2 of AF-1, the actor can still view the Meeting but is no longer its current Chair or Secretary.` |
| II.7.2.1 | EX-3, bước 1 | `1. At Step 2 of AF-1, no file was selected or one or more files are not valid PDF/DOCX files or exceed 50 MB each. (BR-187, BR-214, BR-215)` | `1. At Step 3 of AF-1, no file was received, the request is not a valid file upload, or one or more files are empty, exceed 50 MB each, or are not valid PDF/DOCX files: the extension must be .pdf or .docx and must match the file's content, and a PDF must begin with its PDF header. (BR-187, BR-214, BR-215)` |
| II.7.2.1 | EX-6, bước 1 | `1. At Step 4, or the corresponding check in AF-1, the Meeting has ended, including while the actor was selecting files. (BR-195)` | `1. At Step 4, or at Step 2 of AF-1, the Meeting has ended, including while the actor was selecting or uploading files. (BR-195)` |
| II.7.2.1 | EX-7, bước 1 | `1. Before displaying or completing the addition, the Meeting cannot be accessed.` | `1. Before displaying or completing the addition, including at Step 2 of AF-1, the Meeting cannot be accessed: it does not exist, it is hidden from the actor, or the actor held the Chair or Secretary role but can no longer view the Meeting.` |
| II.7.2.1 | EX-7, bước 2 | `2. No Meeting information is disclosed and no submitted documents are added. The system displays MSG174 – “This Meeting is not accessible.”` | `2. No Meeting information is disclosed and no submitted documents are added. The system displays MSG174 – “This Meeting is not accessible.” The message is the same whether the Meeting is hidden or does not exist, and an actor who can no longer view the Meeting receives MSG174, not MSG162.` |
| II.7.2.1 | EX-8, bước 1 | `1. At Step 2 of AF-1, the actor submits more than 10 files, or the files total more than 200 MB. (BR-319)` | `1. At Step 3 of AF-1, the actor submits more than 10 files, or the files total more than 200 MB, including when the declared size of the request already exceeds the limit. (BR-319)` |
| II.7.2.1 | EX-8, bước 2 | `2. None of the submitted documents are added to the Meeting or personal library. The system displays MSG288 – “Upload up to 10 files at a time, no larger than 200 MB in total.”.` | `2. None of the submitted documents are added to the Meeting or personal library. When the declared size is already over the limit, the system rejects the submission without receiving the files. The system displays MSG288 – “Upload up to 10 files at a time, no larger than 200 MB in total.”.` |

Không đổi: Description, Preconditions, Postconditions, Normal Flow, EX-2, EX-4, EX-5, EX-8 bước 3.
EX-3 bước 2–3 giữ nguyên (bước 3 vẫn "resumes at Step 1 of AF-1").

**Không đổi UC-33b AF-1 và UC-36:** UC-33b AF-1 bước 2 (dòng ~1472) cũng kiểm tệp rồi mới kiểm quyền và dẫn "UC-33a EX-3 và EX-5"; endpoint
SCRUM-107 chỉ phục vụ UC-33a, nên chưa sửa. UC-33b (SCRUM-58) khi làm sẽ quyết thứ tự.

## 2. V.1 Business Rules

| Mục | Ô / bước | Câu cũ | Câu mới |
|---|---|---|---|
| V.1 | BR-215, cột mô tả | `An uploaded document's type is determined from its actual content.` | `An uploaded document's type is determined from its actual content, and its file extension must be .pdf or .docx and match that content.` |

BR-215 chọn thay vì BR-187: BR-187 chỉ nói "PDF and DOCX", BR-215 là quy tắc nhận diện theo nội dung. Cột UC (`UC-33a, UC-36`), nguồn, nhóm: giữ nguyên.
BR-319: không đổi; câu "at most 10 files with a total size of at most 200 MB" khớp EX-8 mới.
Thứ tự "kiểm quyền trước khi kiểm tệp" **không** thành BR mới (mặc định theo plan): chỉ nằm ở UC-33a AF-1 và sequence.

## 3. V.2 System Messages

| Mục | Ô / bước | Câu cũ | Câu mới |
|---|---|---|---|
| V.2 | Dòng `159 / MSG164`, cột *Where* | `UC-33a / EX-3; UC-36 / EX-1` | `UC-33a / EX-3 (also no file or an invalid request); UC-36 / EX-1` |
| V.2 | Dòng `169 / MSG174`, cột *Where* | `UC-33a / EX-7; UC-33b / EX-7; UC-33c / EX-5; UC-33d / EX-7; UC-77 / EX-4` | `UC-33a / EX-7 (also a former Chair or Secretary who can no longer view the Meeting); UC-33b / EX-7; UC-33c / EX-5; UC-33d / EX-7; UC-77 / EX-4` |
| V.2 | Dòng `256 / MSG288`, cột loại hiển thị | `Dành sẵn, chưa triển khai` | `Form error` |

Câu của MSG164, MSG174, MSG288 giữ nguyên. Dòng `256 / MSG288` cột *Where* (`UC-33a / EX-8`) và câu giữ nguyên.
MSG162, MSG166, MSG172 (cũng UC-33a, cũng đã triển khai) vẫn ghi "Dành sẵn" trong catalogue; ngoài phạm vi lượt này.

## 4. I.3.2 Entity 19: StoredFile (không phải Document)

Plan ghi "entity Document", nhưng `PageCount` nằm ở **StoredFile** (Entity 19, dòng ~405 Report 3); `Document` không có trường này.

| Mục | Ô / bước | Câu cũ | Câu mới |
|---|---|---|---|
| I.3.2 | Entity 19 StoredFile, field 6 `PageCount`, cột *Description* | `Number of pages of a PDF; null for a DOCX, which is previewed as one continuous document.` | `Number of pages of a PDF, as counted by the browser; stored only for a PDF and only when it is a positive whole number. Null for a DOCX, which is previewed as one continuous document, and when the page count is unknown; a missing or invalid count never rejects the upload.` |

## 5. Change Log (Report 3, cuối bảng)

| Date | A/M/D | Person in charge | Description |
|---|---|---|---|
| 24/10/2026 | M | KhanhPG | Align UC-33a and BR-215 with SCRUM-107 as implemented: in AF-1 the authority, Meeting access and status checks run before the file checks (a non-Chair/Secretary receives MSG162, not MSG164), EX-1/3/6/7/8 renumbered to Step 2 and Step 3 of AF-1, the file extension must be .pdf or .docx and match the content, a malformed request is MSG164, a declared size over the limit is MSG288, a former Chair or Secretary who can no longer view the Meeting receives MSG174; MSG288 is no longer reserved; StoredFile.PageCount is stored only for a PDF with a positive whole number. |

(Nếu dùng chung một dòng Change Log với các lượt sau cho ngày 24/10/2026, gộp mô tả; plan 05 mục 7 ghi mỗi việc một dòng.)

## 6. Chưa nằm trong patch này (cố ý)

- **Đặc tả màn hình** "Add Document to Meeting Dialog" (Report 3 ~dòng 2868–2889): thuộc [06-plan-dac-ta-man-hinh](../06-plan-dac-ta-man-hinh.md). FE chưa nối API tải lên.
- Điểm 7 (một tài liệu một mục nghị sự), điểm 2 (SeaweedFS), điểm 8 (fixture dev) của [note 07](../07-scrum-107-tai-len-tai-lieu.md): không sửa đặc tả, xem mục 7.

## 7. Câu hỏi đã dùng mặc định / điểm lệch cần Khánh xem

1. **Quy tắc đuôi tệp** vào **BR-215**, không vào BR-187 (câu hỏi 7 của plan mục 9, dùng mặc định).
2. **Thứ tự kiểm quyền → kiểm tệp** chỉ ghi ở UC-33a AF-1 và sequence, không tạo BR (câu hỏi 6, dùng mặc định). Nếu PO muốn BR, lấy BR-320.
3. **Điểm 7** (lập nghị sự vẫn "mỗi tài liệu một mục", trong khi I.3.2 và ADR-0020 cho phép nhiều mục): lệch của UC-33b / SCRUM-58, chưa làm. Không sửa spec; cần ghi comment vào Jira SCRUM-58.
4. **Điểm 2** (kho dev là SeaweedFS): ADR-0024 và `03-co-so-du-lieu.md` đã sửa trong PR; Report 3 không nhắc MinIO nên không sửa.
5. **Điểm 8** (fixture dev của agenda mất object trên kho nên không mở được, DB dev và server test mất hai tài liệu fixture mỗi cuộc họp `[SCRUM-25]`): vấn đề dữ liệu dev, báo leader/BE, không đưa vào Report 3.
6. **Plan ghi "entity Document"**; trường nằm ở StoredFile (mục 4).
7. **MSG162 (403)** khác MSG174 (404) theo quy tắc 403/404 của dự án: "xem được nhưng không còn quyền" là 403; "không xem được" là 404. Người từng quản lý mất vai trò rồi không còn xem được cuộc họp (ví dụ Nháp) nhận MSG174. EX-1 và EX-7 viết đúng ranh giới này.
8. **UC-33b AF-1 bước 2** có cùng thứ tự cũ (kiểm tệp trước quyền) — xem mục 1, cuối.

## 8. Việc còn lại cho máy nhà (Khánh)

Xem trạng thái xuất PNG của `UC33a_UploadDocumentsToMeeting` trong [README](README.md).

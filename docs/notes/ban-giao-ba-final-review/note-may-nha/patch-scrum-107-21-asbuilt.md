# Patch Report 3 — đối chiếu as-built SCRUM-107 + SCRUM-21 (27/10/2026)

**Ngày:** 27/10/2026 · **Lượt:** 6c (BA Final Review) · **Nguồn:** [13 — đối chiếu SCRUM-107/21](../13-doi-chieu-scrum-107-21.md),
[patch-scrum-107](patch-scrum-107.md), [patch-chu-man-hinh](patch-chu-man-hinh.md), catalogue `MSG`,
code `MeetingDocumentEndpoints.cs` / `MeetingDocumentService.cs` / `DocumentFileRules.cs` / `DocumentsPanel.tsx`.

**Đã áp dụng vào `Report_3_Software_Requirement_Specification.docx`** bằng script `python-docx` (chỉ sửa chữ, **không đổi ảnh**),
rồi `python scripts/sync_reports.py --report 3`. `git diff` của `Report-3_...md` có **41 dòng thay**, đều nằm trong các mục dưới
đây; `05-use-case-specifications.md` đổi 7 dòng (do script trích lại từ chính DOCX), `06-business-rules.md` không đổi.
Zip DOCX hợp lệ, mở lại bằng `python-docx` đọc đúng câu mới.

Nguyên tắc: phần **đã ghi ở Jira/PO** thì sửa tài liệu theo code; phần **chưa xây** (nhóm Biểu quyết, khách mời, bình luận,
gắn/bỏ gắn mục nghị sự) thì ghi rõ "not built yet … (UC-xx, SCRUM-yy)" thay vì tả như đã có.

## 1. Đặc tả màn hình

| Mục | Ô / dòng | Câu cũ (phần sửa) | Câu mới |
|---|---|---|---|
| III.7.3 | Đoạn mở đầu | "lists the documents … in three groups, as in AI-eCabinet: By meeting content … Voting … and General documents" | "in two groups: By meeting content … and General documents. A Voting group … is planned and appears once documents can be attached to a Vote (UC-74, SCRUM-96)" |
| III.7.3 | "This screen allows users …" | "… Participant, verified Guest) to:" | "… Participant, and a Guest once UC-04 is built) to:" |
| III.7.3 | View Meeting Documents | "grouped by Agenda Item, Vote or general" | "grouped by Agenda Item or general" |
| III.7.3 | Switch Grouping | "choose By meeting content, Voting or General documents" | "choose By meeting content or General documents; a Voting grouping is planned" |
| III.7.3 | Add Document | "click “Add document” [Thêm tài liệu]" | "click “Thêm tài liệu chung” [Add general document]" |
| III.7.3 bảng | Grouping | "By meeting content (default), Voting, General documents" | "By meeting content (default) and General documents … A Voting segment is planned" |
| III.7.3 bảng | Agenda Item block | "in agenda order with order number, title …" | "numbered by its place in the agenda list rather than the stored position, with title …" |
| III.7.3 bảng | Attach to item | "uploaded here with “Add document”" | "uploaded here with “Thêm tài liệu chung”" |
| III.7.3 bảng | Vote documents | "In the Voting group, each Vote lists its documents …" | "In the planned Voting group … not built yet, it arrives with UC-74 (SCRUM-96)" |
| III.7.3 bảng | Add document | "Button, Chair and Secretary only" | "Button “Thêm tài liệu chung” [Add general document], Chair and Secretary only" |
| III.7.3 bảng | Access rules | "including verified Guests and the Meeting Approver" | "including the Meeting Approver; a Guest reads them once UC-04 is built" |
| III.7.4 | Đoạn mở đầu | "opens from “Add document” … uploads files from the computer. “From my library” …" | "opens from “Thêm tài liệu chung” … It has no tabs: … drops or chooses files …" |
| III.7.4 | Upload from Computer | "on the “From computer” tab drop or choose up to 10 PDF or DOCX files" | "drop or choose up to 10 PDF or DOCX files … extension must match the content; wrong type, empty, over 50 MB or unusable name is named in the error (MSG164)" |
| III.7.4 | Cancel | "click “Cancel” to close the dialog without adding anything" | "click “Huỷ” [Cancel] … Once every byte has been sent the button is disabled … the dialog closes by itself when the result arrives" |
| III.7.4 bảng | Dialog title | "“Add documents” [Thêm tài liệu]" | "“Thêm tài liệu chung” [Add general document]" |
| III.7.4 bảng | Tabs | "From computer / From my library. The library tab is not built yet …" | "There are no tabs: the dialog has one upload area. “From my library” … waits for SCRUM-58" |
| III.7.4 bảng | Upload area | "From computer tab: drop zone … at most 50 MB each …" | bỏ "From computer tab", thêm thứ tự kiểm: quyền trước (MSG162/MSG174), rồi kích thước khai báo > 201 MB (MSG288), rồi kiểm tệp (MSG164) |
| III.7.4 bảng | Access notice | "“Everyone in the Meeting, including verified Guests, can read the added documents.”" | "“Ai xem được cuộc họp đều đọc được tài liệu đã thêm.” [Anyone who can view the Meeting …] (BR-275)" |
| III.7.4 bảng | Cancel | "Text button; closes the dialog." | "Text button “Huỷ” [Cancel] … disabled once every byte has been sent" |
| III.7.5 | Đoạn mở đầu | "… with zoom, and the comment panel is planned" | "… with zoom; the comment panel is planned and arrives with SCRUM-23" |
| III.7.5 bảng | Preview pane | "For a Secret document the viewer’s name and viewing time are printed as a watermark on every page. Each view … of a Restricted or Secret document …" | "No name or time is stamped on the document, and a Secret document is not given a watermark. Each view or download of a Confidential or Secret … (BR-318)" |
| III.7.5 bảng | Comments tab | "… a “Reply” [Trả lời] link; replies are indented under it. A Guest’s comment …" | giữ nguyên phần mô tả, thêm "The comment panel is not built yet and arrives with SCRUM-23 (UC-35)" |
| III.7.5 bảng | Confidentiality chip | "Shown only for a Restricted or Secret Meeting." | "Shown only for a Confidential or Secret Meeting." |
| III.8.6 | Đoạn mở đầu | "The In-Room Documents Panel is planned (SCRUM-21) and not part of the current version. It adds a Documents icon next to Participants and Chat …" | "… is built (SCRUM-21) and opens from the Documents icon in the bottom bar … current Agenda Item comes first with its documents open and the other items folded … on a phone the panel sits behind the ⋯ menu" |
| III.8.6 | "This screen allows users …" | "… (Chair, Secretary, Participant, Guest) to:" | "… (Chair, Secretary, Participant, and a Guest once UC-04 is built) to:" |
| III.8.6 | Open Document | "click a document to read and comment on it in a new tab" | "click a document to read it in a new tab … comments arrive with SCRUM-23" |
| I.5.1.b Screen List | dòng 8.6 | "Planned: allows users in the online room to read the meeting documents …" | "Built (SCRUM-21): allows users in the online room to read the meeting documents …" |

## 2. Mức mật — dùng đúng tên level trong code

Code dùng `Normal / Internal / Confidential / Secret`, nhãn UI `Thường / Nội bộ / Hạn chế / Mật`. Bảng dưới đổi tên level;
**không** đổi chữ "Restricted" trong ma trận quyền (Full/Restricted/No — nghĩa khác).

| Ô | Câu mới |
|---|---|
| III.1.1 Confidentiality chip | `Normal [Thường] / Internal [Nội bộ] / Confidential [Hạn chế] / Secret [Mật].` |
| III.1.2 Confidentiality filter | `Dropdown: “Mọi mức độ” … Confidential [Hạn chế] …` |
| III.1.2 Confidentiality | `Column chip: … Confidential [Hạn chế] …` |
| III.1.3 Confidentiality level | `Segmented control: … Confidential [Hạn chế] …` |
| III.1.4 Confidentiality | `Read-only chip: Normal / Internal / Confidential / Secret.` |
| III.8.1 Room header | "… confidentiality chip for a **Confidential** or Secret Meeting." |
| V.1 Document security levels | "Every document carries one security level: Normal, Internal, **Confidential** or Secret …" |
| V.1 Audit logging | "… access to **Confidential/Secret** documents …" |

## 3. Đặc tả Use Case (mục II, đồng bộ sang `05-use-case-specifications.md`)

| UC | Ô | Câu cũ (phần sửa) | Câu mới |
|---|---|---|---|
| UC-32a | Normal Flow bước 2 | "… there is no separate document-reading permission." | thêm "Guest access through a verified invitation belongs to UC-04 and is not built in this version." |
| UC-32a | Normal Flow bước 3 | "Removed documents are excluded. After the Meeting ends …" | thêm "Agenda Items with no documents are listed as well … (UC-33b). The list refreshes by itself when the system announces that the Meeting's document list has changed." |
| UC-32a | AF-1 bước 1 | "… attached to Votes, or the general Meeting documents." | thêm "The Vote grouping is the target and is not built in this version … (UC-74, SCRUM-96)." |
| UC-32a | AF-3 bước 2 | "… with those of the current Agenda Item first." | "… starting with the current Agenda Item: the server marks which Item is current and the panel places it first, leaving the order the server returns unchanged." |
| UC-32c | Description | "Comments are covered separately in UC-35." | thêm "Guest access through a verified invitation belongs to UC-04 and is not built in this version." |
| UC-32c | POST-3 | "… this enables read confirmation in UC-74." | thêm "This recording is not built in this version: it belongs to UC-74 (SCRUM-96)." |
| UC-32c | Normal Flow bước 3 | "… displays … the Meeting sensitivity. … records the view in the audit log." | "… sensitivity, which the screen takes from the Meeting details … records the view in the audit log **before it sends the file**; if that entry cannot be written, no content is sent and EX-2 applies." |
| UC-32c | Normal Flow bước 4 | "… offers read confirmation through UC-74." | thêm "… that recording is not built in this version (UC-74, SCRUM-96)." |
| UC-33a | AF-1 bước 5 | "… no document from this submission is added to either place; EX-5 applies." | thêm "A file that fails the checks of Step 3 follows EX-3 instead." |
| UC-33a | EX-3 bước 1 | "… one or more files are empty, exceed 50 MB each, or are not valid PDF/DOCX files: the extension …" | "… are not valid PDF/DOCX files, **or carry a file name that cannot be used**: the name is empty, longer than 255 characters, or contains control or invisible formatting characters. The extension …" |

## 4. Catalogue thông báo (V.2)

| Mã | Cột | Câu mới |
|---|---|---|
| `MSG164` | Tình huống | "UC-33a / EX-3 (also no file, an invalid request **or a file name that cannot be used**); UC-36 / EX-1" |
| `MSG288` | Tình huống | "UC-33a / EX-8 (**more than 10 files, more than 200 MB, or a request that declares more than 201 MB**)" |

`MSG156`–`MSG160` giữ nguyên thông điệp; trạng thái "đã triển khai SCRUM-21" ghi ở
[`docs/03-api/00-danh-muc-ma-thong-bao.md`](../../../03-api/00-danh-muc-ma-thong-bao.md) §V.2 (catalogue không có cột trạng thái
trong Report 3).

## 5. Kiểm tra

- `python scripts/sync_reports.py --report 3`: Master Inventory khớp 100%, danh mục MSG khớp 100%.
- Ảnh trong Report 3 **không đổi**: cả 6 mục sửa chỉ là chữ. Wireframe 7.3/7.4/7.5 và 8.6 vẫn là ảnh cũ; nếu cần ảnh khớp chữ
  (nút "Thêm tài liệu chung", hộp thoại một vùng tải lên) thì phải chụp lại màn hình — chưa làm trong lượt này.

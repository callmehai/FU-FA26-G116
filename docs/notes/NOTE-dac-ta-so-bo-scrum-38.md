# Đặc tả sơ bộ SCRUM-38 (báo cáo và thống kê)

**Ngày:** 2026-11-09 · **Người viết:** BE (Huy Nguyễn Trường) · **Trạng thái:** đã chốt, đã đưa vào Report 3 ngày 09/11/2026

| Jira | Tên | Trạng thái lúc viết |
|---|---|---|
| [SCRUM-38](https://sep490g116.atlassian.net/browse/SCRUM-38) | Báo cáo và thống kê | To Do, Sprint 4 |

Đây là bước *BE: basic UC Specification + Sequence Diagram* của
[DEVELOPMENT_WORKFLOW.md §2](../DEVELOPMENT_WORKFLOW.md). Note này chưa phải Report 3: UC, BR ở đây là
**đề xuất**. BA đưa vào DOCX ở `08-reports/source/` lúc BA Final Review, rồi sinh lại
`05-use-case-specifications.md` và `06-business-rules.md`. Không chép tay sang hai file đó.

**09/11/2026 — đã chốt và đưa vào Report 3 DOCX** nguyên văn §3–§5 dưới đây, giữ nguyên số: UC-70a, UC-70b thay hai
placeholder; BR-432 → BR-438 (cột *Rationale* lấy từ cột *Nguồn*); `MSG463`–`MSG466` ở §V.2, loại *Dành sẵn, chưa triển
khai*. Chưa sửa: chú thích ¹⁸ của Permission Matrix (vẫn ghi Quản trị thấy toàn hệ thống, ngược BR-432 — chờ Q1) và đặc tả
màn 15.1 ở §III (vẫn ghi chọn ngày và "Export PDF", D3 — BA sửa khi FE có màn chạy thật).

- Không thêm số UC mới: SCRUM-38 là **UC-70a** và **UC-70b**, đang là *Placeholder* trong Report 3 (FE-15).
- Số BR-432 → BR-438 là số đề xuất, nối tiếp BR-431 (cao nhất trên mọi nhánh lúc viết). BA giữ hoặc đổi số khi đưa
  vào Report 3.
- Mã `MSG463`–`MSG466` đã **dành sẵn** trong [danh mục](../03-api/00-danh-muc-ma-thong-bao.md) cùng commit với note
  này. Bỏ qua `MSG457`–`MSG462` vì PR #292 (SCRUM-27, đăng ký phát biểu) đã nhận dải đó nhưng chưa vào `main`.

## 1. Hiện trạng code (09/11/2026)

| Phần | Đã có | Chưa có |
|---|---|---|
| Quyền đọc cuộc họp | `MeetingReadPolicy.ForUser` — cùng luật với danh sách cuộc họp: người tạo, người duyệt được giao, Chủ toạ/Thư ký hiện tại, thành phần hiện tại từ khi `Published`. **Quản trị không có quyền đọc thêm** | Màn, endpoint báo cáo |
| Số liệu cuộc họp | `Status`, `Sensitivity` (Thường / Nội bộ / Mật), `PlannedStartTime`, `ActualEndedAt` | — |
| Điểm danh | `Attendance` trên `meeting_participants` và `meeting_guest_invitations` (Present / Absent; Excused, Delegated là dữ liệu cũ) | — |
| Ban hành biên bản | Cột `IssuedAt` trên `meeting_minutes` có sẵn trong model | Luồng ban hành (SCRUM-33, PR #297 đang mở). Trên `main` chưa biên bản nào có `IssuedAt` |
| Xuất tệp | `MinutesDocx` tự dựng OpenXML bằng `ZipArchive`, không thư viện ngoài | Tệp `.xlsx` |

Không cần migration: mọi số liệu đọc từ bảng đã có.

## 2. Chỗ lệch khỏi ticket và Report 3, cần ghi lên Jira

| # | Nguồn | Đặc tả này | Lý do |
|---|---|---|---|
| D1 | SCRUM-38: "Là quản trị viên hoặc lãnh đạo" | Mọi người dùng nội bộ đã đăng nhập đều mở được báo cáo; Khách thì không. Không thêm capability mới | Số liệu chỉ tính trên cuộc họp người xem đọc được (tiêu chí của chính ticket), nên mở cho mọi người không lộ gì thêm. Hệ thống không có vai trò "lãnh đạo". Report 3 §I.4.4 cũng cho mọi vai trò nội bộ *Restricted ¹⁸* |
| D2 | Report 3 §I.4.4 chú thích ¹⁸: "System Admin sees system-wide data" | Quản trị cũng chỉ thấy số liệu của cuộc họp mình đọc được | Ticket ghi rõ "chỉ tính trên các cuộc họp người xem có quyền truy cập". `MeetingReadPolicy` không cho Quản trị đọc mọi cuộc họp; báo cáo đếm cả cuộc họp Mật mà Quản trị không được mở thì lộ sự tồn tại của chúng. Xem Q1 |
| D3 | Report 3 màn 15.1: "average duration, timely sealing … export to Excel or PDF" | ~~Không có thời lượng trung bình~~ **Có** thời lượng họp trung bình (PO chốt Q2 ngày 09/11/2026, BR-439 đề xuất); chỉ xuất Excel, không PDF. "Timely sealing" thay bằng thời gian trung bình từ khi kết thúc tới khi ban hành biên bản | Theo tiêu chí nghiệm thu của ticket và Q2 |
| D4 | SCRUM-38: "Thời gian trung bình từ khi kết thúc họp tới khi ban hành biên bản" | Chỉ số này luôn "chưa có dữ liệu" cho tới khi SCRUM-33 (ban hành biên bản) vào `main` | Chưa có biên bản nào được ban hành trên `main` |

## 3. Use Case đề xuất

### UC-70 – View & Export Reports (Xem và xuất báo cáo) — SCRUM-38

Thuộc FE-15.

_UC gom — đặc tả nằm ở các UC con UC-70a, UC-70b bên dưới._

#### UC-70a – View Reports & Statistics (Xem báo cáo, thống kê)

Sequence Diagram:
[UC70a_ViewReportsAndStatistics.txt](../06-sequencediagram/reporting/UC70a_ViewReportsAndStatistics.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | System Admin, Meeting Organizer, Meeting Approver, Chair, Secretary, Participant |
| **Secondary Actors** | N/A |
| **Description** | Allows a signed-in internal user to see aggregate figures about the meetings they may read over a chosen period — meetings per month, by status and by sensitivity, the average attendance rate and the average time from the end of a meeting to the issue of its minutes — so that meeting activity can be followed without opening each meeting. (BR-432, BR-437) |
| **Preconditions** | ● PRE-1: The actor is signed in with an internal account. (BR-432) |
| **Postconditions** | ● POST-1: The actor sees the figures of the chosen period, computed only from the Meetings the actor may read. (BR-432)<br>● POST-2: No meeting title, participant or content is shown. (BR-437)<br>● POST-3: Nothing in the system is changed. |
| **Normal Flow** | View Reports & Statistics<br>1. The actor opens the Báo cáo [Reports] page.<br>2. The system selects the 12 months ending with the current month. (BR-433)<br>3. The system checks the period, then counts the Meetings the actor may read whose planned start falls within it. (BR-432, BR-433, BR-434)<br>4. The system shows the number of Meetings per month, by status and by sensitivity; the average attendance rate with the number of Meetings it is based on (BR-435); and the average time from the end of a Meeting to the issue of its minutes, with the number of Meetings it is based on. (BR-436) |
| **Alternative Flows** | AF-1 – Choose another period<br>1. At Step 2 or after Step 4, the actor chooses a start month and an end month.<br>2. The flow resumes at Step 3 of the Main Flow with that period.<br>AF-2 – No Meeting in the period<br>1. At Step 3, the actor may read no Meeting whose planned start falls within the period.<br>2. The system shows zero counts and no averages, and displays MSG466 – “There are no meetings you can view in this period.”<br>3. The use case ends.<br>AF-3 – An average has no data<br>1. At Step 4, no Ended Meeting in the period had attendance taken, or none has issued minutes.<br>2. The system shows that average as having no data instead of a number. (BR-435, BR-436)<br>3. The use case ends. |
| **Exception Flows** | EX-1 – The period is not valid<br>1. At Step 3, the start month is after the end month, or the period is longer than 24 months. (BR-433)<br>2. No figure is computed. The system displays MSG463 – “The report period is invalid. Choose up to 24 months, with the start month not after the end month.”<br>3. The flow resumes at Step 1 of AF-1.<br>EX-2 – The report cannot be loaded<br>1. At Step 3, the system cannot read the data.<br>2. No figure is shown. The system displays MSG464 – “Unable to load the report. Please try again.”<br>3. The use case ends. |

#### UC-70b – Export Report (Xuất báo cáo)

`<<extend>>` UC-70a. Sequence Diagram: [UC70b_ExportReport.txt](../06-sequencediagram/reporting/UC70b_ExportReport.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | System Admin, Meeting Organizer, Meeting Approver, Chair, Secretary, Participant |
| **Secondary Actors** | N/A |
| **Description** | Allows the actor to download the figures of UC-70a for the chosen period as an Excel file, to keep them or to work on them outside the system. (BR-438) |
| **Preconditions** | ● PRE-1: The actor is signed in with an internal account. (BR-432)<br>● PRE-2: The actor is on the Báo cáo page with a period chosen. (UC-70a) |
| **Postconditions** | ● POST-1: The actor receives an Excel file holding the same figures as the page for that period, computed at the time of export. (BR-438)<br>● POST-2: The file holds no meeting title, participant or content. (BR-437)<br>● POST-3: The export is recorded with the actor, the period and the time. (BR-438) |
| **Normal Flow** | Export Report<br>1. The actor selects Xuất Excel [Export to Excel].<br>2. The system checks the period and computes the figures again from the Meetings the actor may read. (BR-432, BR-433)<br>3. The system builds the Excel file and records the export. (BR-438)<br>4. The browser saves the file. |
| **Alternative Flows** | AF-1 – No Meeting in the period<br>1. At Step 2, the actor may read no Meeting in the period.<br>2. The file is still built, with zero counts and no averages.<br>3. The flow resumes at Step 3 of the Main Flow. |
| **Exception Flows** | EX-1 – The period is not valid<br>1. At Step 2, the period is not valid. (BR-433)<br>2. No file is built. The system displays MSG463 – “The report period is invalid. Choose up to 24 months, with the start month not after the end month.”<br>3. The use case ends.<br>EX-2 – The file cannot be built<br>1. At Step 2 or Step 3, the system cannot read the data, build the file or record the export.<br>2. No file is downloaded and no export is recorded. The system displays MSG465 – “Unable to export the report. Please try again.”<br>3. The use case ends. |

### Thay đổi ở các UC đã đặc tả

Không có. Báo cáo chỉ đọc, không đổi luồng nào khác.

## 4. Business Rules đề xuất

| BR | Rule Statement | UC | Nguồn |
|---|---|---|---|
| `BR-432` | Every signed-in internal user may view and export the meeting report; a Guest may not. The figures count only the Meetings the user may read under the Meeting list rules — for a System Admin as for anyone else. | UC-70a, UC-70b | SCRUM-38 — "chỉ tính trên các cuộc họp người xem có quyền truy cập"; D1, D2 |
| `BR-433` | A report period is a whole number of calendar months, in Vietnam time. The start month must not be after the end month, and the period covers at most 24 months. By default it is the 12 months ending with the current month. | UC-70a, UC-70b | SCRUM-38 — số cuộc họp theo tháng |
| `BR-434` | A Meeting belongs to the month of its planned start. It is counted under its current status and its sensitivity; Cancelled Meetings are counted too. | UC-70a, UC-70b | SCRUM-38 — theo tháng, theo trạng thái, theo mức mật |
| `BR-435` | The attendance rate of an Ended Meeting is the number of invitees marked present divided by its current internal participants and its Guests whose invitation was not revoked. A Meeting where nobody was marked present or absent is left out. The average attendance rate is the mean of those rates. | UC-70a, UC-70b | SCRUM-38 — tỷ lệ tham dự trung bình |
| `BR-436` | The minutes turnaround of an Ended Meeting is the time from its actual end to the first issue of its minutes; corrections issued later do not count. A Meeting whose minutes are not issued yet is left out. The average turnaround is the mean of those times. | UC-70a, UC-70b | SCRUM-38 — thời gian trung bình từ khi kết thúc họp tới khi ban hành biên bản |
| `BR-437` | The report shows only counts, rates and averages. It never shows the title, participants or content of a Meeting, on the page or in the exported file. | UC-70a, UC-70b | Cuộc họp Mật — số liệu tổng hợp không được thành đường vòng để đọc cuộc họp |
| `BR-438` | An exported report is an Excel file holding exactly the figures of UC-70a for the same period, computed at the time of export. Every export is recorded in the audit log with the user, the period and the time. | UC-70b | SCRUM-38 — xuất Excel; tệp mang số liệu ra khỏi hệ thống |
| `BR-439` *(đề xuất, PO 09/11/2026, chưa vào Report 3)* | The duration of an Ended Meeting is the time from its actual start to its actual end. A Meeting without both times is left out. The average duration is the mean of those durations. | UC-70a, UC-70b | Q2 — PO thêm thời lượng họp trung bình |

## 5. Mã thông báo (dành sẵn)

| Mã | HTTP dự kiến | UC | Thông điệp đặc tả | Câu tiếng Việt cho UI |
|---|---|---|---|---|
| `MSG463` | 400 | UC-70a EX-1, UC-70b EX-1 | *The report period is invalid. Choose up to 24 months, with the start month not after the end month.* | `Khoảng thời gian không hợp lệ. Chọn tối đa 24 tháng, tháng bắt đầu không sau tháng kết thúc.` |
| `MSG464` | 500 | UC-70a EX-2 | *Unable to load the report. Please try again.* | `Không tải được báo cáo. Vui lòng thử lại.` |
| `MSG465` | 500 | UC-70b EX-2 | *Unable to export the report. Please try again.* | `Không xuất được báo cáo. Vui lòng thử lại.` |
| `MSG466` | FE | UC-70a AF-2 | *There are no meetings you can view in this period.* | `Không có cuộc họp nào bạn xem được trong khoảng này.` |

## 6. API dự kiến (cho BE, chưa phải contract)

Contract chính thức viết vào `docs/03-api/` trong PR code. Mọi endpoint kiểm tra lại quyền ở server và lọc cuộc họp
bằng `MeetingReadPolicy.ForUser` **trong câu truy vấn**, trước khi đếm. Khách không có phiên đăng nhập nội bộ nên gọi
tới là `401` như mọi endpoint `RequireAuthorization()`, không có mã.

| Method | Đường dẫn | Ghi chú |
|---|---|---|
| `GET` | `/api/reports/meetings?from=2026-01&to=2026-12` | Bỏ `from`/`to` thì lấy 12 tháng tới tháng hiện tại. `200` trả JSON bên dưới; `400 MSG463`; `500 MSG464` |
| `GET` | `/api/reports/meetings/export?from=…&to=…` | `200` tệp `bao-cao-cuoc-hop_2026-01_2026-12.xlsx`; `400 MSG463`; `500 MSG465` |

```json
{
  "from": "2026-01", "to": "2026-12", "generatedAt": "2026-11-09T10:00:00+07:00",
  "total": 42,
  "byMonth": [{ "month": "2026-01", "count": 3 }],
  "byStatus": [{ "status": "Ended", "count": 20 }],
  "bySensitivity": [{ "sensitivity": "Secret", "count": 4 }],
  "attendance": { "averageRate": 0.87, "meetings": 18, "meetingsWithoutAttendance": 2 },
  "minutesTurnaround": { "averageHours": 30.5, "meetings": 0, "meetingsWithoutIssuedMinutes": 20 }
}
```

- `byMonth` có đủ mọi tháng trong khoảng, kể cả tháng `0`; `byStatus` và `bySensitivity` có đủ mọi giá trị enum,
  để FE không phải tự điền.
- `averageRate`, `averageHours` là `null` khi không có cuộc họp nào để tính (AF-3).
- Đếm bằng `GROUP BY` trên database; không kéo danh sách cuộc họp về server rồi đếm, và không bao giờ trả `id` hay
  tiêu đề cuộc họp (BR-437).
- Tháng tính theo giờ Việt Nam (`+07:00`, không có giờ mùa hè), như `DocumentFileRules`.
- Tệp `.xlsx` dựng tay bằng `ZipArchive` như `MinutesDocx`, không thêm package: một sheet, ba bảng (theo tháng, theo
  trạng thái, theo mức mật) và hai dòng trung bình, ghi khoảng thời gian và thời điểm xuất ở đầu sheet.
- Ghi audit `ReportExported` (người xuất, `from`, `to`) cùng lúc trả tệp (BR-438). Xem báo cáo không ghi audit.
- FE: mục *Báo cáo* trên sidebar cho mọi người dùng nội bộ; route `/reports`.

## 7. Câu hỏi cần PO / BA chốt

PO chốt cả ba ngày 09/11/2026.

| # | Câu hỏi | Đề xuất trong note | PO chốt (09/11/2026) |
|---|---|---|---|
| Q1 | Quản trị có thấy số liệu toàn hệ thống (Report 3 chú thích ¹⁸) không? | Không, theo ticket (D2). Nếu PO muốn thì cần quyết định cho Quản trị đọc mọi cuộc họp — một ADR, vì nó đổi cả danh sách cuộc họp | **Không.** Quản trị cũng chỉ thấy số liệu của cuộc họp mình được xem, như mọi người (BR-432 giữ nguyên) |
| Q2 | Có cần thời lượng họp trung bình và xuất PDF như màn 15.1 của Report 3 không? | Không trong SCRUM-38 (D3). Thời lượng tính được ngay từ `ActualStartedAt`/`ActualEndedAt` nếu PO muốn | **Thêm** thời lượng họp trung bình (BR-439 đề xuất, đã có ở BE: `duration`). **Không** xuất PDF |
| Q3 | Có giới hạn báo cáo cho một nhóm người (capability *Xem báo cáo*) như "quản trị viên hoặc lãnh đạo" của ticket không? | Không (D1). Nếu cần thì thêm capability `ReportViewer` và một migration gán cho vai trò Quản trị | **Không giới hạn.** Ai đăng nhập bằng tài khoản nội bộ cũng xem và xuất được; Khách vẫn không (BR-432) |

## 8. Việc còn lại

- [ ] Ghi D1 → D4 lên SCRUM-38 bằng comment (skill `jira-deviation-note`).
- [x] Xuất PNG cho hai sequence `.txt` và thêm tab vào `TrustCabinet_Sequence_Diagrams.drawio` (09/11/2026). Xuất lại
      lúc BA Final Review nếu đặc tả còn đổi.
- [x] BA: đưa UC-70a, UC-70b, BR-432 → BR-438 và `MSG463`–`MSG466` vào Report 3 DOCX, cập nhật inventory (09/11/2026;
      `check_docx.py` OK, `sync_requirement_extracts.py --check` UP TO DATE). UC diagram đã có "View & Export Reports"
      cho đúng 6 actor nên không sửa.
- [ ] BA: sửa chú thích ¹⁸ của Permission Matrix theo Q1 (PO chốt 09/11/2026: Quản trị như mọi người).
- [ ] BA: đưa BR-439 (thời lượng họp trung bình, Q2) vào Report 3; thêm thời lượng vào Description/Normal Flow bước 4 và
      AF-3 của UC-70a.
- [ ] BA: sửa đặc tả màn 15.1 (§III) theo D3 — giữ thời lượng trung bình, bỏ "Export PDF", chọn theo tháng — khi FE có
      màn chạy thật.
- [x] BE: `GET /api/reports/meetings`, `GET /api/reports/meetings/export`, audit `report.exported`; contract ở
      [`21-bao-cao-thong-ke.md`](../03-api/21-bao-cao-thong-ke.md); `MSG463`–`MSG465` sang *BE* (09/11/2026).
- [ ] FE: trang Báo cáo, chọn khoảng tháng, nút Xuất Excel; `MSG466`.

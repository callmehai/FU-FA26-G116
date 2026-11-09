# Note 40 — Đặc tả màn Danh sách cuộc họp: mở mặc định "Hôm nay"

**Ngày:** 09/11/2026 · **Ticket:** SCRUM-18 (UC-17) · **Người viết:** Khánh (BA) · **Trạng thái:** bản nháp, chưa đưa vào Report 3 DOCX

Ghi tạm phần đặc tả màn 4.3 *Meeting List* theo hành vi mới. Khi chốt thì sửa DOCX
`docs/08-reports/source/` rồi chạy `scripts/sync_reports.py --report 3`; **không** sửa
`Report-3_….md` hay `05-use-case-specifications.md` bằng tay.

## 1. Code đang chạy (nguồn để đối chiếu)

Commit `90793dbf` trên `main` (08/11/2026) — `frontend/src/features/meetings/listParams.ts`,
`MeetingsPage.tsx`, `MeetingStats.tsx`:

| Tình huống | Code làm gì |
|---|---|
| Mở màn từ sidebar, URL không có `status`, `scope`, `when` | Lọc **Thời điểm = Hôm nay**, mọi trạng thái; thẻ *Họp hôm nay* đang bật. |
| Bấm thẻ *Đã công bố*, *Bản nháp* (một trạng thái) hoặc *Cần xử lý* (phạm vi) | Bỏ lọc ngày: danh sách là đúng tiêu chí của thẻ, mọi thời điểm; ô tìm kiếm và các lọc khác cũng xoá để danh sách khớp số trên thẻ. |
| Link cũ `?status=Draft`, `?scope=todo` | Đọc là mọi thời điểm (không ghép thêm Hôm nay). |
| Chọn trạng thái ở **dropdown** Trạng thái | **Giữ** Thời điểm = Hôm nay đang chọn (URL ghi `when=today`). Chỉ thẻ và link mới bỏ lọc ngày. → xem câu hỏi Q1. |
| Bấm nút *Xoá bộ lọc* (icon ✕ cuối thanh lọc), hoặc bấm lại thẻ đang bật | Về **tất cả** cuộc họp, mọi thời điểm (URL `?when=all`); giữ cách sắp xếp và số dòng/trang. |
| Lọc Hôm nay (không thêm tiêu chí nào) mà không có dòng | Bảng hiện "Hôm nay không có cuộc họp nào." + nút **Xem mọi cuộc họp** (về tất cả). |
| Chỉ *Cần xử lý* mà không có dòng | "Không có việc chờ bạn xử lý." + nút **Xem mọi cuộc họp**. |
| Tiêu chí khác không khớp dòng nào | "Không có cuộc họp nào khớp bộ lọc." + nút **Xoá bộ lọc**. |

Yêu cầu gọi nút này là "Bỏ lọc"; trên màn nó là icon ✕ có tooltip **Xoá bộ lọc**, và
nút trong bảng trống là **Xem mọi cuộc họp** / **Xoá bộ lọc**. Đặc tả dưới đây dùng đúng nhãn trên màn.

## 2. Report 3 đang lệch ở đâu

Report 3 vẫn mô tả bản cũ: **mở sẵn lọc Trạng thái = Đang diễn ra**, lời nhắn "Hiện không có cuộc họp nào
đang diễn ra." Các chỗ phải sửa:

| Vị trí trong Report 3 | Nội dung cũ |
|---|---|
| III. Screen list, dòng 4.3 Meeting List | "The list opens filtered to In Progress Meetings." |
| Bảng UC list, dòng UC-17 | như trên |
| UC-17 Normal Flow bước 1 | "The system opens the list with the Status filter preset to In Progress…" |
| UC-17 AF-3 | chỉ có "No meetings match the filter." |
| Màn 4.3 — gạch đầu dòng *Filter Meetings* | "the list opens filtered to In Progress Meetings" |
| Màn 4.3 — Field *Status filter*, *Time filter*, *Messages* | preset Đang diễn ra; Thời điểm mặc định "Mọi thời điểm" |
| Bảng thông báo, dòng 58 (UC-17) | chỉ có "No meetings match the filter." |

## 3. Nội dung đề xuất thay vào Report 3 (tiếng Anh, giữ nhãn tiếng Việt trong `[ ]`)

### 3.1 Screen list — dòng 4.3 và dòng UC-17

> … and page through the Meetings s/he organizes, is invited to or must approve, and open a Meeting.
> The list opens on today's Meetings.

### 3.2 UC-17 — Normal Flow bước 1

> 1. The user selects Meetings from the navigation menu. The system opens the list filtered to Today
>    (every status); the user may change or clear the filter.

### 3.3 UC-17 — AF-3 (thêm nhánh) và AF-5 mới

> AF-3 – No Matching Meetings
> 1. The applied search or filter criteria produce no matching meetings within the meetings available to the user.
> 2. The system displays an empty meeting list and the message "No meetings match the filter."
>    When only the opening Today filter is applied, the message is "Hôm nay không có cuộc họp nào."
>    [There are no Meetings today], with a button to view all Meetings.
> 3. The user may change or clear the current search/filter criteria.
>
> AF-5 – Clear Filters
> 1. At Step 4 of the Main Flow, the user clears the filters.
> 2. The system displays every Meeting available to the user, at any time, keeping the current sort and page size.
> 3. The flow resumes at Step 4 of the Main Flow.

(AF-5 chỉ cần nếu BA muốn tách rõ; nếu không, ghi câu "Clearing the filters shows every Meeting at any
time." vào AF-2 bước 3 cũng đủ.)

### 3.4 Màn 4.3 Meeting List — gạch đầu dòng chức năng

> - Filter Meetings: filter the list by status, confidentiality level and time; the list opens on
>   today's Meetings, and the time filter offers Today, This week, Not yet held and Already held (UC-17).
> - Summary cards: four cards above the list — Meetings today [Họp hôm nay], Published [Đã công bố],
>   To do [Cần xử lý] and Drafts [Bản nháp] — each show a count and, when clicked, the list of exactly
>   those Meetings (UC-17).

### 3.5 Màn 4.3 — Field Description (các dòng thay/thêm)

| Field Name | Description |
| --- | --- |
| Page header | Title "Meeting list" [Danh sách cuộc họp]; "Create meeting" [Tạo cuộc họp] on the right for users who may create Meetings. |
| Summary cards | Four cards: Meetings today [Họp hôm nay] (sub line: number of sessions in progress, or "Không có phiên đang diễn ra"), Published [Đã công bố], To do [Cần xử lý] ("Chờ bạn duyệt, công bố") and Drafts [Bản nháp] ("Chưa gửi duyệt"; shown only to users who may create Meetings). Each count covers only the Meetings the user may see (BR-29) and ignores the search and filters. Clicking a card replaces the search and filters with that card's criteria, at any time; clicking the active card again shows every Meeting. Meetings today is active when the page opens. |
| Status filter [Trạng thái] | Dropdown: "Mọi trạng thái" [All statuses] (default) and Draft, Pending Approval, Approved, Rejected, Published, In Progress, Ended, Cancelled. |
| Time filter [Thời điểm] | Dropdown preset to Today [Hôm nay] when the page opens; "Mọi thời điểm" [Any time] clears it. Other choices: This week [Tuần này], Not yet held [Chưa diễn ra], Already held [Đã diễn ra]. There is no date-range or month input. Matching rules as BR-30; combined criteria must all match (BR-31). Opening the list from a status or To do card shows that set at any time. |
| Clear filters [Xoá bộ lọc] | Icon button (✕) at the end of the filter bar, shown while any criterion is applied. Shows every Meeting at any time; the sort and page size are kept. |
| Messages | "Hôm nay không có cuộc họp nào." [There are no Meetings today] with the button "Xem mọi cuộc họp" [View all Meetings] when the opening Today filter finds nothing; "Không có việc chờ bạn xử lý." [Nothing is waiting for you] with the same button when To do alone finds nothing; "No meetings match the filter." with "Xoá bộ lọc" when other criteria match no row; MSG38 on load failure; MSG32 for invalid criteria. A Meeting the user has declined still appears in the list (BR-321). |

Hai lời nhắn bảng trống mới là chữ FE tự hiện, **không có mã MSG** (giống dòng 58 hiện có) → thêm 2 dòng
"—" vào bảng thông báo của Report 3, cạnh dòng 58. Không đụng catalogue `MSGnn`.

### 3.6 Không cần đổi

- BR-30, BR-31, BR-32 vẫn đúng (BR-30 đã nói cách khớp Hôm nay). Không thêm BR mới — "mở mặc định Hôm nay" là hành vi màn, không phải luật nghiệp vụ.
- Không đổi API: FE gửi `from`/`to` của ngày hôm nay như lọc Thời điểm thường.

## 4. Câu hỏi cho PO / FE

- **Q1.** Ticket nói "chọn trạng thái thì bỏ lọc ngày". Code chỉ làm vậy khi bấm **thẻ** (và link `?status=`);
  chọn ở **dropdown** Trạng thái thì vẫn giữ Hôm nay (ra "Đã công bố hôm nay"). Giữ như code (đề xuất — dropdown
  là thêm điều kiện, BR-31) hay FE sửa cho dropdown cũng bỏ lọc ngày? Đặc tả mục 3.5 đang viết theo code.
- **Q2.** Đặc tả cũ thiếu cả 4 thẻ thống kê (có từ `c55e6ba9`); note này đã thêm vào 3.4/3.5. Wireframe màn 4.3
  (`screens/*.png`) cũng chưa có thẻ — cần vẽ lại wireframe trước khi thay ảnh trong DOCX.

## 5. Việc còn lại

- [ ] PO trả lời Q1.
- [ ] Sửa Report 3 DOCX theo mục 3, sync `--report 3`.
- [ ] Vẽ lại wireframe 4.3 (có thẻ, Thời điểm = Hôm nay), xuất `screens/*.png`.
- [ ] Comment lên SCRUM-18: UC-17 bước 1, AF-3, màn 4.3, 2 lời nhắn không mã.

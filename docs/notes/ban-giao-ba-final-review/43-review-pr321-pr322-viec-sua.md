# Review PR #321 và #322 — việc sửa và cách chia cho agent

**Người ghi:** KhánhPG/BA, 11/11/2026 (thêm §1b cùng ngày: leader giao BA Final Review SCRUM-16, SCRUM-38, SCRUM-142). **Nguồn:** review `CHANGES_REQUESTED` của callmehai trên
[#321](https://github.com/callmehai/FU-FA26-G116/pull/321) (SCRUM-33, SCRUM-16) và
[#322](https://github.com/callmehai/FU-FA26-G116/pull/322) (SCRUM-142); note [41](41-pr297-scrum-33-duyet-ban-hanh-bien-ban.md),
[42](42-pr311-scrum-16-nhat-ky-kiem-toan.md), [NOTE-dac-ta-so-bo-scrum-137-142](../NOTE-dac-ta-so-bo-scrum-137-142.md).

## 0. Luật chung

- **Không đụng nhánh #321** (`docs/ba-final-review-pr297-pr311`). Mọi sửa của cả hai PR là **commit mới trên nhánh #322**
  (`docs/ba-final-review-scrum-137-142`), vì hai PR cùng sửa DOCX Report 3 và `TrustCabinet_Sequence_Diagrams.drawio` (git không gộp được).
- File nhị phân / gộp chỉ **một agent** sửa tại một thời điểm: DOCX Report 3 (agent A), `TrustCabinet_Sequence_Diagrams.drawio` (agent C).
- Report 3: sửa DOCX trong `docs/08-reports/source/`, rồi `sync_reports.py --report 3` + `check_docx.py` **một lần** ở cuối. Không sửa tay snapshot.
- Mọi câu sửa phải khớp **code**, có dẫn file:dòng trong mô tả commit. Không sửa code.
- Không push, không comment GitHub/Jira — người giao việc làm ở §3.

## 1. Danh sách việc

`[B]` = reviewer bắt buộc, `[K]` = không bắt buộc nhưng làm luôn vì cùng lượt sync, `[PO]` = quyết định PO 11/11 chưa vào DOCX.

| # | Nguồn | Việc | File | Agent |
|---|---|---|---|---|
| 1 | #321 [B] | UC-15: bỏ Main Flow bước 6 và POST-4 "records the update in the audit log"; thay bằng "Khi đổi lịch cuộc họp Published (AF-1), thay đổi được ghi nhật ký (BR-445)". Kiểm `MeetingService.UpdateAsync` vs `RescheduleAsync` | DOCX | A |
| 2 | #321 [B] | Sơ đồ use case FE-12 theo luồng mới: Thư ký Submit for Approval; Chủ toạ Approve and Issue, Return; Correct Issued Minutes (UC-83); bỏ Sign / Finalize / Request Revision của Thư ký và Người dự họp. Xuất lại `uc-*.png` | `docs/14-uc-diagram/` | B |
| 3 | #321 [B] | Thay ảnh sơ đồ use case trong Report 3 §I.4.3 bằng PNG mới của #2 | DOCX | A (sau B) |
| 4 | #322 [B] | UC-85c theo `docs/03-api/23-chia-se-lich.md` §8 và `SharedCalendarService.cs:39-40`: tab **Lịch được chia sẻ** của trang **Chia sẻ lịch** (`/calendar/sharing?tab=week`), chỉ người có quyền tạo họp; **mỗi người một hàng**, mỗi ngày một thanh giờ bận; ngày ngoài khoảng chia sẻ hiện **"Chưa biết"** (BR-415); chỉ lấy `Effective` hoặc `Stopped` còn hạn; sửa AF-1 tương ứng; thêm EX cho `MSG448` (403 không quyền tạo họp) | DOCX | A |
| 5 | #322 [B] | Sequence `UC85c_ViewSharedCalendar.txt` (dòng ~74) theo #4 | sequence | C |
| 6 | #322 [B] | BR-426: thay câu "not audited and notifies no one" bằng: hết ngày kết thúc thì không ghi nhật ký, không báo ai; tạo chia sẻ mới cùng cặp thì đóng dòng cũ thành Ended **có ghi nhật ký** `calendar_share.closed_after_end_date`, không báo ai (`CalendarShareCloser.CloseAfterEndDateAsync`) | DOCX | A |
| 7 | #322 [B] | UC-84c EX-2 và BR-424: thêm "hoặc người xem không còn quyền Meeting Organizer" → `MSG450` (`AcceptAsync`). Kiểm sequence UC-84c có nhánh này chưa | DOCX + sequence | A, C |
| 8 | [PO] | UC-61: ngoài phạm vi — Thư ký không rút lại được bản đã gửi duyệt (Q5). UC-64: ngoài phạm vi — ban hành không gửi email (Q6) | DOCX | A |
| 9 | #321 [K] | UC-01 EX-2, UC-02 EX-4/POST-5: đăng nhập thất bại vì tài khoản vô hiệu hoá cũng ghi nhật ký (`account_deactivated`, `AuthenticationService.cs`) | DOCX | A |
| 10 | #321 [K] | UC-58 bước 4: `ApprovedBy/ApprovedAt/IssuedAt` ai đọc cũng thấy; chỉ người gửi, người trả lại, lý do mới giới hạn. UC-61 EX-5: "stays a draft" → "stays a draft or returned revision" | DOCX | A |
| 11 | #321 [K] | Màn 12.1 (DOCX) và dòng UC-60 trong `00-master-use-case-inventory.md`: "văn bản thuần" → trình soạn HTML (BR-385) | DOCX + inventory | A |
| 12 | #321 [K] | Tiêu đề "FE-12 — Meeting Minutes & Digital Signing" còn trong snapshot: sửa **script** `scripts/sync_reports.py` (chỗ giữ tiêu đề FE cũ), không sửa tay; Report 1, 2 nếu cùng nguồn | script | D (trước A sync) |
| 13 | #322 [K] | UC-84f AF-2: thẻ "Lịch được chia sẻ với tôi" **không hiện** (không phải rỗng) khi không có quyền tạo họp; bước 3: Stopped vào nhóm đã đóng; UC-84c bước 4, UC-84e: "sau khi xác nhận" | DOCX | A |
| 14 | #321/#322 [K] | Sequence: ":Minutes Controller" → ":Minutes Endpoints" (UC-58–65, UC-83); UC-58 AF-3 thêm nhánh 404; UC-84f: tài khoản không hoạt động là **403** (`RequireActiveUser`), thay hai chỗ "(empty state wording pending BA)" bằng "Bạn chưa chia sẻ lịch với ai." / "Chưa ai chia sẻ lịch với bạn.", bỏ hai khối `opt … end` rỗng. Xuất lại bằng `scripts/export_sequence.py` | sequence | C |
| 15 | #322 [K] | `scripts/export_sequence.py`: docstring/comment sang tiếng Anh, đổi tên biến `rel` (đang giữ đường dẫn tuyệt đối); `docs/06-sequencediagram/README.md`: bỏ lời khuyên chạy `sync_sequence_drawio_png.py` cho mọi PNG, trỏ sang `export_sequence.py` | script + README | D |
| 16 | #322 [K] | Note 137-142: tick B1–B4; NOTE-dac-ta-so-bo-scrum-38: tick BR-447, chú thích ¹⁸, màn 15.1 sau #18–#20; sửa dòng ~411 "Report 3 chưa có gì…"; note 41: mục 33-11 giữ tick sau khi B xong; note 42/41 checklist theo các việc trên | notes | D (cuối) |

### 1b. Việc thêm — leader nhắn 11/11/2026 (ba ticket sang BA Final Review)

Làm chung lượt với §1 vì cùng sửa DOCX Report 3 và cùng nhánh #322. Nguồn Jira: SCRUM-16 `10497`, `10517`; SCRUM-38 `10491`, `10499`,
`10500`; SCRUM-142 `10512`, `10515`, `10516`.

| # | Ticket | Việc | File | Agent |
|---|---|---|---|---|
| 17 | SCRUM-16 | Đối chiếu UC-13, BR-440 → BR-446, sequence UC-13, class diagram FE-03 với bản đang chạy: phần lớn đã làm ở #321 (note 42). Lượt này chỉ: kiểm lại sau #1, #9; xác nhận PO `10517` (Q-A, Q-B) đã có. Đặc tả màn 3.3 vẫn **PENDING** (note 42 §7) trừ khi leader yêu cầu — FE đã lên main nên mở khoá được, hỏi leader trước | DOCX | A |
| 18 | SCRUM-38 | Thêm **BR-447** vào Report 3 (bản đề xuất ở [NOTE-dac-ta-so-bo-scrum-38](../NOTE-dac-ta-so-bo-scrum-38.md) dòng ~100): thời lượng một cuộc họp Ended = giờ bắt đầu thực tế → giờ kết thúc thực tế; thiếu một trong hai thì không tính; trung bình tính bằng phút, làm tròn 1 chữ số (`ReportService.cs:72-117`). BR-447 còn trống trong Report 3 | DOCX | A |
| 19 | SCRUM-38 | UC-70a: thêm thời lượng trung bình vào Description, Normal Flow bước 4, AF-3 (chưa có số liệu); UC-70b: file Excel có ba dòng trung bình (`MeetingReportXlsx.cs:121-125`), chỉ Excel, không PDF. Đối chiếu cả UC-70a/70b với FE `/mgmt/reports` (#314) và `docs/03-api/22-bao-cao-thong-ke.md` | DOCX | A |
| 20 | SCRUM-38 | Chú thích ¹⁸ ma trận quyền: Quản trị **không** xem toàn hệ thống, như mọi người (PO Q1, BR-432). Màn 15.1 (§III): giữ thời lượng trung bình, bỏ "Export PDF", chọn kỳ theo tháng | DOCX | A |
| 21 | SCRUM-38 | Sequence UC-70a/UC-70b: kiểm có `duration`; sửa nếu thiếu | sequence | C |
| 22 | SCRUM-142 | Ba chỗ khác eCab (`10512`) vào UC-84f, UC-85c: (a) quản lý chia sẻ và lịch tuần **một màn hai tab** `/calendar/sharing`, mở từ menu tài khoản; (b) lịch tuần **mỗi người một hàng**, ngày ngoài khoảng ghi **"Chưa biết"**, chia sẻ đã dừng ghi "Đã dừng" + MSG439 (gộp với #4); (c) Từ chối, Rút, Ngừng **hỏi xác nhận trước** (gộp với #13). Thêm: chia sẻ đã đóng vẫn hiện, làm mờ | DOCX | A |
| 23 | SCRUM-142 | UC-84c AF-1 và dòng MSG450 (`10515`): duyệt thay đổi chờ mà khoảng xin không còn hợp ngày bắt đầu **hiện tại** (chủ lịch đã dời ngày, hoặc ngày xin đã qua) → không có nút Đồng ý; nếu vẫn gửi → `409 MSG450` | DOCX + sequence UC-84c | A, C |
| 24 | SCRUM-142 | Mở thông báo `?share={id}` mà dòng không còn (`10516`): toast MSG439 (người xem mất quyền tạo họp) hoặc "Chia sẻ này không còn." — đưa vào UC-84f (hoặc UC-68 nếu đó là chỗ mở thông báo) | DOCX | A |

## 2. Thứ tự chạy agent

```
Lượt 1 (song song):  B (sơ đồ use case)   C (sequence)   D (script, README)
Lượt 2:              A (DOCX Report 3 — cần PNG của B, script của D)  → sync + check_docx một lần
Lượt 3:              D tick notes (#16) → người giao việc rà diff, push, nhắn "đã sửa"
```

Mỗi agent: session mới, đọc root `AGENTS.md`, note này, và review gốc (`gh api repos/callmehai/FU-FA26-G116/pulls/<321|322>/reviews`).
Mỗi agent **commit riêng phần mình** (commit tiếng Việt, tiền tố `[BA]`), không push.

### Prompt mẫu

**A — DOCX Report 3**
> Đọc `docs/notes/ban-giao-ba-final-review/43-review-pr321-pr322-viec-sua.md`. Bạn là agent A: làm việc #1, #3, #4, #6, #7 (phần DOCX), #8–#11, #13,
> #17–#20, #22, #23 (phần DOCX), #24 trên nhánh `docs/ba-final-review-scrum-137-142`. Sửa chỉ `docs/08-reports/source/Report_3_Software_Requirement_Specification.docx`
> (theo `docs/08-reports/README.md`, giữ định dạng) và `docs/01-requirements/00-master-use-case-inventory.md`. Mỗi câu kiểm với code trước
> khi viết. Cuối cùng chạy `python scripts/sync_reports.py --source docs/08-reports/source/Report_3_Software_Requirement_Specification.docx --report 3`
> và `python scripts/check_docx.py`, thêm một dòng Change Log 11/11. Commit, không push. Báo: việc đã làm, việc bỏ (lý do), kết quả check.

**B — Sơ đồ use case FE-12**
> Đọc note 43 và `DRAWIO_EDITING_RULES.md`, dùng skill drawio. Làm việc #2: sửa `docs/14-uc-diagram/trustcabinet-use-cases.drawio` phần FE-12
> theo UC-58–65, UC-83 trong `docs/01-requirements/05-use-case-specifications.md` và `MinutesEndpoints`. Giữ id, sửa có mục tiêu, validate,
> xuất lại các `uc-*.png` bị ảnh hưởng, mở ảnh kiểm tra. Chỉ sửa `docs/14-uc-diagram/`. Commit, không push.

**C — Sequence**
> Đọc note 43, `docs/06-sequencediagram/README.md`, `DRAWIO_EDITING_RULES.md`. Làm việc #5, #7 (phần sequence), #14, #21, #23 (phần sequence). Sửa `.txt` theo code
> (`SharedCalendarService`, `CalendarSharingService`, `MinutesEndpoints`, `ReportService`), cập nhật tab tương ứng trong `TrustCabinet_Sequence_Diagrams.drawio`
> (giữ id), xuất ảnh bằng `python scripts/export_sequence.py <UC…>`. Chỉ sửa `docs/06-sequencediagram/`. Commit, không push.

**D — Script, README, notes**
> Đọc note 43. Lượt 1: việc #12 (sửa `scripts/sync_reports.py` để tiêu đề FE-12 lấy tên mới, chạy khô kiểm) và #15. Không chạy sync ghi file —
> agent A chạy. Lượt 3 (sau A): việc #16. Commit, không push.

## 3. Người giao việc làm sau cùng

1. Rà `git log` / `git diff main...` trên nhánh #322: không có commit nào trên nhánh #321; snapshot Report 3 chỉ đổi qua sync.
2. `git push` nhánh `docs/ba-final-review-scrum-137-142`; sửa mô tả #322 liệt kê #1–#24 (ghi rõ #17–#24 là BA Final Review SCRUM-16, SCRUM-38, SCRUM-142 theo leader 11/11).
3. Nhắn "đã sửa" ở kênh pull-request (Discord) và trả lời hai review trên GitHub.
4. Sau khi #321, #322 vào `main`: comment Jira SCRUM-33, SCRUM-16 (kèm trả lời comment `10353` ở SCRUM-123), SCRUM-142 theo note 41 §6, 42 §5; SCRUM-38 (BR-447, UC-70a/70b, chú thích ¹⁸, màn 15.1) và tick checklist trong [NOTE-dac-ta-so-bo-scrum-38](../NOTE-dac-ta-so-bo-scrum-38.md). Chuyển trạng thái theo [DEVELOPMENT_WORKFLOW §2.2](../../DEVELOPMENT_WORKFLOW.md#22-closing-a-review-which-tickets-to-update).

## 4. Checklist

- [ ] Lượt 1: B (#2), C (#5, #7, #14, #21, #23), D (#12, #15)
- [ ] Lượt 2: A (#1, #3, #4, #6–#11, #13, #17–#20, #22–#24), sync + check_docx
- [ ] Lượt 3: D (#16), push, "đã sửa"
- [ ] Jira sau merge

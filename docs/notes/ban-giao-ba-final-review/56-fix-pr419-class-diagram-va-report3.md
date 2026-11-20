# Fix PR #419 (class diagram FE-04–16, Report 3) theo review của Hải

**Ngày:** 20/11/2026 · **Nhánh:** `docs/report3-uc51-bo-uc82-fe05-class` (PR #419 → `main`, xếp trên #417 đã merge `bd954709`)
**Người ghi:** agent, theo review `CHANGES_REQUESTED` của Hải ngày 20/11/2026 trên PR #419.
**Trạng thái:** đã sửa xong 3 điểm chặn + 6 điểm không chặn; chờ Hải re-review.
**Nguồn chuẩn:** DOCX trong `docs/08-reports/source/` (snapshot sinh bằng `scripts/sync_reports.py`); class diagram
trong `docs/07-classdiagram/`, sửa theo code thật.

> Note 56 gộp #418 + #419 (file `56-fix-pr418-wireframe-v13-va-pr419-class-diagram.md` trên nhánh #418)
> đã được tách: phần #418 nằm ngoài PR này, chỉ §B của #419 được chép vào đây.

## 1. Kết quả theo từng điểm review

### B1. Chặn — PNG xuất RGB không nén, +10.4 MB

- Chỉ giữ PNG của diagram thật sự đổi: **FE-01, FE-02, FE-03, FE-04** đã trả cả `.drawio` (nếu có
  thay đổi) lẫn PNG về bản `main`; PR chỉ còn FE-05/06/07/08/09/10/11/12/13/15/16.
- Chạy `python scripts/compress_png.py` cho toàn bộ PNG của PR, gồm 7 ảnh `14-uc-diagram/` và hai ảnh
  sequence UC-82.
- Kết quả: tổng PNG thay đổi **2.6 MB → 3.4 MB**, tăng **~0,75 MB** (trước review là 4.1 → 14.5 MB).
  Ảnh vẫn mở được và đọc được sau khi nén palette.

### B2. Chặn — UC-51 “retired” tự mâu thuẫn

- DOCX: dưới heading `10.8 UC-51 – Publish Meeting Summary (retired)` đã thay dòng placeholder bằng
  bảng retired đầy đủ — `Description` ghi rõ **bỏ ngày 19/11/2026, lý do UC-50 duyệt xong là mọi người
  đọc được, giữ số tạm và sẽ đánh lại số**, các flow/pre/post ghi `N/A`. Bảng UC trong Report 3 và
  inventory dòng UC-51 cũng đã đổi theo.
- Sau `sync_reports.py --report 3`, `05-use-case-specifications.md` và `Report-3_*.md` không còn dòng
  “Placeholder only – detailed Use Case Specification will be added later”; `check_docx.py` báo `OK`.
- Note 55 đã ghi **ai chốt**: PO Hải chốt ngày 19/11/2026; không có ticket Jira riêng cho UC-51, ticket
  gần nhất là SCRUM-36 (UC-47→UC-51) nơi UC-51 vẫn ghi “chưa làm”.

### B3. Chặn — số BR ghi chú lệch giữa Report 3 và contract

- Đã đánh lại toàn bộ tham chiếu trong `docs/03-api/19-ghi-chu-cuoc-hop.md` (các dòng 20, 38, 40, 48,
  55, 56, 57, 124) và catalogue `MSG417/418/419/423` sang số chính thức của Report 3:
  BR-396→BR-398, BR-397→BR-399, BR-398→BR-400, BR-399→BR-401, BR-400→BR-403, BR-401→BR-459,
  BR-403→BR-461 (BR-402 giữ nguyên).
- Bảng đối chiếu số tạm → chính thức đã chép vào note 55 §4 để người sau tra.
- Kiểm tra: contract không còn BR-396/397 (mốc khoá, mục đang bàn) hay BR-399/400/401 với nghĩa
  cũ; `BR-403` chỉ còn đúng nghĩa “tối đa 20.000 ký tự”, `BR-461` cho nháp trên máy. Các số mới
  BR-398–401 trùng dải số nên phải đọc theo nghĩa, không grep theo số đơn thuần.

### B4. Không chặn — class FE-16 sai entity thật

- `CalendarShare` trong `feature-16-calendar-sharing-class-diagram.drawio` đã sửa theo
  `Domain/Entities/CalendarShare.cs`: `ownerId`, `viewerId`, `initiatedBy`, `level`, `startDate`,
  `endDate`, `status`, `pendingLevel?`, `pendingEndDate?`, `pendingRequestedAt?`, `decidedAt?`,
  `closedAt?`, `closedById?`; bỏ `requesterId`/`createdAt` không có trong entity.
- UC-85a–c chưa có class diagram nên bảng §1 của `docs/07-classdiagram/README.md` ghi **UC-84**, kèm
  ghi chú “UC-85a–c chưa vẽ trong sơ đồ này”.

### B5. Không chặn — FE-12 thiếu `revisionNumber`, `supersedesId`

- `MeetingMinutes` trong `feature-12-minutes-management-class-diagram.drawio` đã thêm hai trường
  `revisionNumber: int`, `supersedesId?: UUID`; hộp cao thêm hai dòng, không cắt chữ.

### B6. Không chặn — FE-05 vẽ repository hai lần

- Đã bỏ `meetingRepoUsed`, `minutesRepoUsed`; FE-05 chỉ còn một `MeetingRepository`, một
  `MinutesRepository` dùng chung cho cả làn Agenda và làn Ghi chú (UC-82). Hai cạnh `uc82_n3`/`uc82_n4`
  nối lại vào box chung, đi vòng dưới dải repository để không xuyên class.

### B7. Không chặn — README class diagram còn câu cũ

- `docs/07-classdiagram/README.md` §1 đã bỏ câu “FE-08, FE-12, FE-14 và FE-15 chưa có class diagram”:
  FE-08/12/15/16 đã có trong đợt này, FE-14 bỏ theo UC-69.
- Thêm §6i ghi các thay đổi FE-05/12/16 và kết quả kiểm tra.

### B8. Không chặn — note 55 thiếu trong README + dòng trạng thái mâu thuẫn

- Đã thêm hàng note 55 và note 56 vào `docs/notes/ban-giao-ba-final-review/README.md`.
- Dòng trạng thái note 55 đã đổi từ “chưa sửa DOCX/diagram … uncommitted” sang “đã sửa DOCX + sync
  snapshot + class diagram, chờ Hải re-review”, kèm kết quả §4.

### B9. Không chặn — script xuất PNG thiếu yêu cầu môi trường

- Đầu `scripts/export_class_diagram_png.sh` đã ghi yêu cầu: bash, draw.io desktop CLI (mặc định
  `/c/Program Files/draw.io/draw.io.exe`, đổi bằng `DRAWIO`), Python 3 + Pillow; cờ
  `--disable-gpu --disable-software-rasterizer` và cách hạ scale.
- `docs/07-classdiagram/README.md` §5 đã thêm cùng hướng dẫn.

## 2. Kiểm tra đã chạy

```powershell
python scripts/check_docx.py docs/08-reports/source/Report_3_Software_Requirement_Specification.docx   # OK
python scripts/sync_reports.py --source docs/08-reports/source/Report_3_Software_Requirement_Specification.docx --report 3
python .agents/skills/drawio-skill/scripts/validate.py docs/07-classdiagram/feature-05-agenda-management-class-diagram.drawio --score
# FE-05: 0 error, 0 through-vertex; FE-12/FE-16: 0 error, 0 warning
```

Đã render và xem ảnh FE-05, FE-12, FE-16 sau khi sửa: không cắt chữ, không chồng class, không có cạnh
xuyên class. PNG được xuất lại scale 1.2 rồi nén palette.

## 3. Việc còn lại

- Hải re-review PR #419; sau khi vào `main` mới comment Jira theo
  [DEVELOPMENT_WORKFLOW §2.2](../../DEVELOPMENT_WORKFLOW.md#22-closing-a-review-which-tickets-to-update).
- Không đẩy Drive (`push_reports.py`) khi chưa được yêu cầu.

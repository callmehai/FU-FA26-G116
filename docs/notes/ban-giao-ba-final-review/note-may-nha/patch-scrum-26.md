# Patch Report 3 — SCRUM-26 (điểm danh)

**Ngày:** 24/10/2026 · **Lượt 1** theo [05-plan](../05-plan-sua-uc-br-msg-diagram.md) mục 8 · **Nguồn:** [04](../04-scrum-26-diem-danh.md),
[ADR-0025](../../../05-decisions/0025-version-rieng-cho-diem-danh.md), [API điểm danh](../../../03-api/10-diem-danh.md),
catalogue MSG150/151/289.

**Đã áp dụng thẳng vào DOCX** `Report_3_...docx` (chỉ `word/document.xml`, không đụng ảnh), chạy `sync_reports.py --report 3`,
rồi đồng bộ tay `05-use-case-specifications.md`, `06-business-rules.md` và bảng tiếng Anh `00-danh-muc-ma-thong-bao.md`. Bảng dưới là bản ghi những gì đã đổi — Khánh **không cần dán lại**.
Các đoạn không ghi ở đây giữ nguyên.

Tên cột ở bảng dưới: **Mục** (vị trí trong Report 3) · **Ô / bước** · **Câu cũ** · **Câu mới**.

## 1. II.6.5 UC-31 – Record Attendance

| Mục | Ô / bước | Câu cũ | Câu mới |
|---|---|---|---|
| II.6.5 | Normal Flow, bước 1 | `1. The Secretary opens Attendance for an InProgress Meeting.` | `1. The Secretary opens Attendance for an InProgress Meeting: the Attendance column of the participant table on the Meeting detail page, or the Attendance panel in the online meeting room.` |
| II.6.5 | Normal Flow, bước 2 | `2. The system displays current internal participants and Guests with saved attendance; never-recorded people remain unmarked.` | `2. The system displays current internal participants and Guests with saved attendance; never-recorded people remain unmarked. Each person’s attendance carries its own version. No Present/Absent totals are shown. (BR-172)` |
| II.6.5 | Normal Flow, bước 4 | `4. The system rechecks current authority, Meeting status, submitted people’s current membership and each submitted value. Omitted people are not treated as validation errors. (BR-170–BR-172)` | `4. The system rechecks current authority, Meeting status, submitted people’s current membership, that each submitted person’s attendance version is still current, and each submitted value. Omitted people are not treated as validation errors. (BR-170–BR-172)` |
| II.6.5 | Exception Flows, EX-4, tên | `EX-4 – A submitted person is no longer current` | `EX-4 – A submitted person is no longer current or their attendance has changed` |
| II.6.5 | EX-4, bước 1 | `1. At Step 4, one or more submitted internal participants were removed or the Guest invitation was revoked after the roster was displayed.` | `1. At Step 4, one or more submitted internal participants were removed, the Guest invitation was revoked, or another Secretary changed the person’s attendance after the roster was displayed.` |
| II.6.5 | EX-4, bước 2 | `2. No submitted change is saved. The system displays MSG151 – “The meeting participant list has changed. Refresh it and try again.” The Secretary refreshes the roster and resumes at Step 2. Unrelated new roster entries do not invalidate the submission.` | `2. No submitted change is saved and no unsaved selection is kept. The system displays MSG151 – “The participant or attendance data has changed. Please reload and review before saving.” The system reloads the roster and the Secretary selects again, resuming at Step 2. Unrelated new roster entries, and changes to a person’s role or invitation response, do not invalidate the submission. (BR-172)` |
| II.6.5 | EX-5, bước 2 | `2. Every prior attendance value remains unchanged; no partial attendance changes are saved.` | `2. Attendance is never saved in part. If the connection was lost while saving, the outcome cannot be confirmed.` |
| II.6.5 | EX-5, bước 3 | `3. The system displays MSG150 – “Unable to save attendance. No changes were saved.” The use case ends unsuccessfully.` | `3. The system displays MSG150 – “Unable to confirm the attendance save result. Please reload the data before trying again.” The Secretary reloads attendance to see what was saved. The use case ends unsuccessfully.` |
| II.6.5 | Exception Flows, thêm sau EX-5 | *(chưa có)* | `EX-6 – Attendance cannot be loaded`<br>`1. At Step 2, the system cannot read the attendance of the Meeting.`<br>`2. No attendance is changed. The system displays MSG289 – “Unable to load attendance data.” The failed load is not presented as an empty or unmarked roster. The use case ends unsuccessfully.` |

Không đổi: Postconditions, AF-1, AF-2, EX-1–EX-3. Các cuộc gọi vào MSG278 (query phân trang sai) chỉ xảy ra khi gọi API
trực tiếp, FE không gửi query sai — **không** thêm vào UC (xem mục 6).

## 2. V.1 Business Rules

| Mục | Ô / bước | Câu cũ | Câu mới |
|---|---|---|---|
| V.1 | BR-172, cột mô tả, cuối câu | `…unrelated roster additions do not require attendance for those people. Attendance completeness never prevents End Meeting.` | `…unrelated roster additions do not require attendance for those people. Each person’s attendance has its own version, which changes only when that attendance changes, not when the person’s role or invitation response changes; a submission carrying an out-of-date version is rejected as a whole, nothing is saved, and the Secretary reloads and selects again — unsaved selections are not kept (ADR-0025). Attendance completeness never prevents End Meeting.` |

BR-172 không có câu nào hứa giữ bản nháp, nên không phải bỏ câu cũ. Cột UC, nguồn, nhóm: giữ nguyên.

## 3. V.2 System Messages

| Mục | Ô / bước | Câu cũ | Câu mới |
|---|---|---|---|
| V.2 | Dòng `145 / MSG150`, cột *Where* | `UC-31 / EX-3` | `UC-31 / EX-5` (khớp số EX trong II.6.5) |
| V.2 | Dòng `145 / MSG150`, cột *Message* | `Unable to save attendance. No changes were saved.` | `Unable to confirm the attendance save result. Please reload the data before trying again.` |
| V.2 | Dòng `146 / MSG151`, cột *Where* | `UC-31 / EX-4, BR-172 / Changed submitted participant` | `UC-31 / EX-4, BR-172 / Changed submitted participant or attendance` |
| V.2 | Dòng `146 / MSG151`, cột *Message* | `The meeting participant list has changed. Refresh it and try again.` | `The participant or attendance data has changed. Please reload and review before saving.` |
| V.2 | **Thêm dòng** sau dòng `256 / MSG288` (cuối bảng); số thứ tự `257` | *(chưa có)* | `257 \| MSG289 \| Toast \| UC-31 / EX-6 \| Unable to load attendance data.` |

Giữ nguyên MSG147, MSG148, MSG149.

## 4. Change Log (Report 3, cuối bảng)

| Date | A/M/D | Person in charge | Description |
|---|---|---|---|
| 24/10/2026 | M | KhanhPG | Align UC-31 and BR-172 with SCRUM-26 as implemented: attendance is a column of the participant table (a panel only in the online meeting room), no Present/Absent totals, per-person attendance version (ADR-0025), a conflict (MSG151) keeps no unsaved selection and reloads, neutral wording for MSG150 and MSG151, add EX-6 and MSG289 for a failed load. |

(Nếu dùng chung một dòng Change Log với các lượt sau cho ngày 24/10/2026, gộp mô tả; plan 05 mục 7 ghi mỗi việc một dòng.)

## 5. Chưa nằm trong patch này (cố ý)

- **Đặc tả màn hình** "Attendance Check" (Report 3 ~dòng 2708–2731, có nút "Save attendance" và bảng riêng): thuộc
  [06-plan-dac-ta-man-hinh](../06-plan-dac-ta-man-hinh.md). Màn thật không có nút Lưu, chọn là lưu.
- Danh sách màn hình, bảng Screen List.

## 6. Câu hỏi đã dùng mặc định / điểm lệch cần Khánh xem

1. **MSG278** không thêm vào Report 3 V.2 và không có EX: chỉ API trực tiếp mới chạm tới, FE luôn gửi `page`/`pageSize` hợp lệ.
   Catalogue đã có MSG278; nếu muốn đủ mã thì thêm một dòng `Form error | UC-31 / EX-6 (API) | Attendance paging parameters are invalid.` — mặc định: không thêm.
2. **Bộ đếm "Có mặt x/y"**: ticket nói FE không có bộ đếm, nhưng [10-diem-danh.md](../../../03-api/10-diem-danh.md) mục 6 vẫn mô tả chip
   "Có mặt x/y" trên thanh công cụ (lấy từ `totals`). Patch theo ticket (không bộ đếm). Cần xác nhận với FE rồi sửa một trong hai chỗ.
3. **Bản nháp**: [10-diem-danh.md](../../../03-api/10-diem-danh.md) mục 5 hàng 409/MSG151 còn ghi "so sánh draft"; mục 6 nói không có bản nháp.
   Patch theo mục 6. Hai dòng đó trong docs API nên thống nhất (ngoài phạm vi lượt này, chỉ tài liệu API).
4. MSG150 ở Report 3 cũ ghi `UC-31 / EX-3` nhưng UC-31 dùng EX-5 cho lỗi lưu; `05-use-case-specifications.md` và catalogue vẫn đánh EX-3 (UC bản Markdown có thứ tự EX khác Report 3). Patch sửa V.2 theo Report 3; sync sẽ đồng bộ lại các số này.

## 7. Việc còn lại cho máy nhà (Khánh)

Lượt này **không** sinh lại PNG (sequence) và không đụng `SM-06`. Chạy trên máy có draw.io:

```bash
python scripts/sync_sequence_drawio_png.py
```

Rồi xem `docs/06-sequencediagram/drawio-png/uc31_recordattendance.drawio.png` (nhãn MSG150/MSG151 dài hơn, kiểm có tràn khung) và commit PNG.

Chưa làm trong sequence UC-31 (plan 05 mục 5.4): nhánh GET attendance có version, 500/MSG289 — sơ đồ chỉ vẽ POST, thêm nhánh cần thêm ô và chỉnh bố cục. Đã sửa: chữ MSG150, MSG151, nhãn nhánh stale, bước validate (kèm version).
`SM-06_Attendance`: không có trạng thái "nháp" → không cần sửa. `grep ADR-0024 docs/`: không chỗ nào nói về điểm danh → không đổi.

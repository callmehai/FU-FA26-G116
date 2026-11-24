# Báo cáo truy vấn (PR #488) và chuyển chữ lại giữ phần sửa tay (ADR-0077): cập nhật Report 3

**Người ghi:** agent, theo 2 tin của bot kênh `note-gửi-ba` ngày 22/11/2026 (17:24 và 18:57 UTC). **Ngày:** 23/11/2026.
**Trạng thái: ✅ XONG — chờ duyệt (chưa commit, push, comment Jira, react Discord).** Code đã vào `app`: PR #483 (ADR-0077, `d24745e7`) và PR #488 (`77bdf128`).
**Nhánh:** `docs/note-59-bao-cao-go-bang-lai`, tách từ `origin/app` `e7dc0e7a`, PR đích `app`.
**Nguồn chuẩn:** DOCX `docs/08-reports/source/Report_3_Software_Requirement_Specification.docx`, sinh lại bằng `python scripts/sync_reports.py --source docs/08-reports/source/Report_3_Software_Requirement_Specification.docx --report 3`. Không sửa tay `Report-3_*.md`, `05-use-case-specifications.md`, `06-business-rules.md`.

Docs API và ADR đã có sẵn trên `app` (`docs/03-api/22-bao-cao-thong-ke.md`, `12-ban-ghi-cuoc-hop.md`, `13-nhan-dien-giong-noi.md`, `docs/05-decisions/0077-giu-phan-sua-tay-khi-go-bang-lai.md`, catalogue có `MSG484`). Phần còn thiếu là tài liệu yêu cầu: Report 3, inventory, sequence.

## 1. Màn Báo cáo làm lại theo ý thầy — PR #488, SCRUM-38 (UC-70a, UC-70b, BR-432–BR-438)

Nguồn chi tiết: mục "For BA" của PR #488.

- [x] **UC-70a** luồng chính mới: dải số liệu chính cho người quản lý ở trên, rồi phần truy vấn: chọn bảng dữ liệu, nhóm theo, top N, bộ lọc, sắp theo cột, dòng Tổng.
- [x] **UC-70a EX mới:** tham số truy vấn sai → **`400 MSG484`** (MSG463 kiểm trước MSG484).
- [x] **BR-437 nới cho phần truy vấn:** nhóm theo người hoặc theo cuộc họp thì dòng có tên người / mã và tiêu đề cuộc họp — chỉ cuộc họp người xem đã được đọc, không lộ nội dung họp; khách chỉ hiện tên, không email. `GET /api/reports/meetings` vẫn chỉ trả số đếm.
- [x] **BR mới về phạm vi nhìn thấy trong báo cáo** (giống các tab cuộc họp): biểu quyết nháp và biên bản chưa ban hành chỉ đếm cho Chủ toạ/Thư ký đang hoạt động; bản ghi chỉ đếm cho người tham dự đang hoạt động; bản ghi tổng hợp không cộng vào số giờ ghi.
- [x] **UC-70b:** *Xuất Excel* xuất đúng bảng đang xem, tên tệp `bao-cao-<bảng>_<từ>_<đến>.xlsx`, đúng một dòng audit; nhãn audit "Xuất báo cáo cuộc họp" → "Xuất báo cáo". `GET /api/reports/meetings/export` còn nhưng màn không gọi nữa.
- [x] MSG trong DOCX: thêm `MSG484`.
- [x] Sequence UC-70a / UC-70b (`docs/06-sequencediagram/`): thêm bước truy vấn theo tham số, nhánh `MSG484`, xuất theo bảng đang xem.
- [x] Inventory dòng UC-70 (`00-master-use-case-inventory.md`): ghi chú luồng truy vấn.
- **Đặc tả màn hình Báo cáo: PENDING — không làm trong note này.** Màn đã đổi (dải số liệu chính, ô Dữ liệu/Nhóm theo/Hiển thị, bộ lọc, dòng Tổng, Xuất Excel bảng đang xem) nên đặc tả màn trong Report 3 đang lệch; để note riêng, làm qua wireframe → `screens/*.png`, không dùng ảnh chụp app. Note này chỉ sửa đặc tả UC, BR, MSG, ERD và diagram.

## 2. Chuyển chữ lại giữ phần Thư ký sửa tay — PR #483, ADR-0077, SCRUM-35 và SCRUM-31

Nguồn chi tiết: ADR-0077 và comment Jira SCRUM-35, SCRUM-31 (PR #483 không có mục "For BA").

- [x] **UC gỡ băng / gán người nói** (nhóm UC-44, UC-45): khi chuyển chữ lại (mic phòng lưu lượt mới, hoặc bấm làm lại) hệ thống **giữ** phần Thư ký sửa tay:
  - tên gán tay cho một giọng đi theo giọng mới trùng ≥ 60% lời (≥ 2 giây) với giọng cũ, và thắng tên máy gán;
  - câu sửa chữ và câu chuyển người đi theo câu mới cùng lượt, lệch ≤ 500 ms hoặc trùng ≥ 80%.
- [x] Phần không khớp được: hệ thống ghi nhật ký (không ghi nội dung chữ). Đưa vào UC dưới dạng hậu điều kiện / luồng phụ, không ghi tên sự kiện kỹ thuật trong SRS.
- [x] Xoá lượt khi đang chờ chạy lại vẫn cắt câu cũ.
- [x] **BR mới** cho quy tắc giữ phần sửa tay (các ngưỡng 60% / 2 giây / 500 ms / 80%).
- [x] **ERD** trong Report 3: bảng câu gỡ băng thêm 2 cột người chuyển người nói và lúc chuyển (`docs/02-design/02-erd.md` đã có).
- [x] Sequence chuyển chữ lại / gán người nói: thêm bước mang phần sửa tay sang bản mới.

## 3. Tồn của note 58 — sequence UC-86a–86d (PR #478 ghi Pending)

`.txt` trong `docs/06-sequencediagram/transcription-voice-id/` đã sửa 22/11 (`e32b2224`), nhưng `.png` vẫn bản 15/11 (`f7a21b4a`): còn đường Thư ký trong cuộc họp, giới hạn 5 mẫu, `MSG477`, thiếu `personAgreed` và nhánh `400 MSG483`.

- [x] Xuất lại 4 PNG từ `.txt`: UC86a_ViewVoiceCollection, UC86b_RecordVoiceSampleForAttendee, UC86c_PlayCollectedVoiceSample, UC86d_DeleteCollectedVoiceSample.
- [x] Vẽ lại 4 tab `UC-86a … UC-86d` trong `TrustCabinet_Sequence_Diagrams.drawio` và PNG tương ứng trong `drawio-png/` theo [DRAWIO_EDITING_RULES.md](../../../DRAWIO_EDITING_RULES.md).
- [x] Thay hình UC-86 trong DOCX Report 3 nếu có nhúng.

## 4. Bảng file cần sửa

| File | Mục 1 Báo cáo | Mục 2 Gỡ băng lại | Mục 3 UC-86 |
|---|---|---|---|
| DOCX Report 3 (rồi sinh lại extract + `uc-map.html`) | UC-70a, UC-70b, BR-437 + BR mới phạm vi, MSG484 (đặc tả màn Báo cáo: PENDING) | UC-45c, UC-46a, UC-46b, UC-44d, BR mới giữ sửa tay, ERD 2 cột | hình sequence UC-86 |
| `docs/01-requirements/00-master-use-case-inventory.md` | dòng UC-70 | dòng UC-45c/UC-46 (ghi chú) | — |
| `docs/06-sequencediagram/reporting/UC70a_*.txt`, `UC70b_*.txt` + PNG | ✔ | — | — |
| `docs/06-sequencediagram/transcription-voice-id/UC45c_TranscribeMeetingRecording.txt`, `UC46b_AssignSpeaker.txt`; `meeting-recording/UC44d_DeleteRecordingTake.txt` + PNG | — | ✔ | — |
| `docs/06-sequencediagram/transcription-voice-id/UC86a–d.png` | — | — | ✔ |
| `TrustCabinet_Sequence_Diagrams.drawio` + `drawio-png/` | tab UC-70a, UC-70b | tab UC-45c, UC-46b, UC-44d | tab UC-86a–d |
| `docs/07-classdiagram/` FE-10 (nếu thể hiện cột) | — | `TranscriptSegment` thêm 2 cột người/lúc chuyển | — |

Không đụng: `docs/03-api/`, ADR, catalogue MSG — đã có trên `app`.

## 5. Xong thì

Đánh dấu ✅ dòng 59 trong [README](README.md), mở PR đích `app`. Sau khi PR vào `app`: comment Jira SCRUM-38, SCRUM-35, SCRUM-31 theo [DEVELOPMENT_WORKFLOW §2.2](../../DEVELOPMENT_WORKFLOW.md#22-closing-a-review-which-tickets-to-update), rồi đổi ❌ → ✅ trên 2 tin của kênh `note-gửi-ba`.

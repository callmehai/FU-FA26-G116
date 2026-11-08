# Việc BA còn lại sau 07–08/11/2026 — mức mật 3 mức, chốt Q9/Q10, note refactor BE

**Người ghi:** KhánhPG/BA, 08/11/2026. **Nguồn:** kênh Discord `note-gửi-ba` (ba tin đánh ❌ ngày 08/11/2026),
[ADR-0057](../../05-decisions/0057-ba-muc-mat-nhu-ai-ecabinet.md) (PR #278), [ADR-0058](../../05-decisions/0058-chia-se-lich-va-tim-gio-hop.md)
(PR #284), [đặc tả sơ bộ SCRUM-137/142](../NOTE-dac-ta-so-bo-scrum-137-142.md) (PR #282). Nối tiếp
[note 36](36-chuan-bi-dac-ta-chia-se-lich-va-tim-gio-hop.md). Note này chỉ gom việc còn dở, không thay Jira hay đặc tả.

## 0. Tóm tắt

| # | Việc | Ticket / PR | Việc nặng nhất | Làm trước |
|---|---|---|---|---|
| A | Chốt Q9 và Q10 của #282 thành "đã chốt" | SCRUM-137, SCRUM-142 (PR #282) | Sửa chữ ở note + DOCX, sync Report 3 | Làm đầu tiên, nhỏ nhất |
| B | Mức mật còn 3 mức: Report 3, sequence, class diagram | PR #278 (ADR-0057) | Sửa DOCX nhiều chỗ, sync | Sau A |
| C | Đưa note refactor BE vào `docs/notes` bằng PR nhỏ | SCRUM-143 | Chỉ commit + PR | **Đã xong 08/11/2026** (xem §3); phần refactor để sau |

Trạng thái đã đọc ngày 08/11/2026 trên `main` `b9882663`. Report 3 chưa có bản Markdown nào được sửa tay: **mọi sửa đổi ở A và B
đi vào DOCX trong `docs/08-reports/source/`, rồi chạy `python scripts/sync_reports.py --source docs/08-reports/source/<file>.docx --report 3`**
(quy tắc ở [AGENTS.md](../../../AGENTS.md#documentation)).

## 1. A — Chốt Q9 và Q10 của #282

Hải trả lời lúc 12:23 ngày 08/11/2026: **câu 1, tính bận: giữ như Khánh đề xuất; câu 2, chia sẻ lịch: tối đa 365 ngày.**

| Câu | Nội dung chốt | Chỗ phải sửa |
|---|---|---|
| Q9 | Trạng thái tính bận ở BR-420: Chờ duyệt, Đã duyệt, Đã công bố, Đang họp, Đã kết thúc (đến giờ kết thúc thật) tính bận; Nháp, Bị từ chối, Đã huỷ không tính | [NOTE-dac-ta-so-bo-scrum-137-142.md](../NOTE-dac-ta-so-bo-scrum-137-142.md): dòng BR-419 ("Q9" ở cột nguồn), BR-420 ("chờ PO xác nhận Q9"), bảng câu hỏi Q9, checklist cuối note |
| Q10 | Khoảng ngày chia sẻ tối đa 365 ngày tính cả hai đầu, chia sẻ mới bắt đầu từ hôm nay (BR-431, D16) | Cùng note: dòng 61 ("chờ PO xác nhận Q10"), bảng Q10, mục D16; BR-431 trong Report 3 |

Việc làm:

1. Trong note SCRUM-137/142: đổi "Chờ PO xác nhận" thành "Đã chốt 08/11/2026 (Hải)" ở Q9 và Q10; bỏ cụm "chờ PO xác nhận" ở
   BR-420, D16 và dòng 61; chuyển hai câu từ bảng "câu hỏi mở" sang bảng quyết định (số D kế tiếp sau D16).
2. Trong Report 3 (DOCX): BR-420 và BR-431 không còn ghi chú chờ xác nhận; dòng Change Log mới ghi quyết định PO 08/11/2026.
   Nếu Report 3 không có chữ "chờ" ở hai BR này (grep không thấy `Q9`/`Q10` trong bản Markdown), chỉ cần thêm Change Log.
3. Comment Jira SCRUM-142 và SCRUM-137: ghi Q9, Q10 đã chốt (không có thay đổi nghiệp vụ so với đề xuất).

## 2. B — Mức mật còn 3 mức (Thường · Nội bộ · Mật)

Hải chốt phương án A ngày 07/11/2026: bỏ `Confidential` (Hạn chế); họp đang Hạn chế thành Mật qua migration
`SensitivityThreeLevels`. Mọi luật trước đây "Hạn chế **hoặc** Mật" nay chỉ còn **Mật**. Bỏ hành động kiểm toán
`document.downloaded`. API gửi `Confidential` bị từ chối như giá trị lạ. Code, `docs/03-api` và ADR đã xong ở #278;
phần dưới là việc của BA.

### B1. Report 3 (sửa DOCX, không sửa Markdown)

Quy tắc chữ: mọi chỗ "Confidential or Secret" / "Confidential/Secret" thành "Secret"; chip, bộ lọc, cột, ô chọn còn 3 giá trị.

| Chỗ | Dòng trong bản Markdown (08/11/2026) | Sửa |
|---|---|---|
| Change Log 22/10/2026 (UC-32a/32c/35) và 27/10/2026 (UC-34b) | 41, 44 | Giữ nguyên là lịch sử; thêm một dòng Change Log mới 08/11/2026 ghi ADR-0057 |
| Bảng Meeting, `Sensitivity` | 196 | Values: Normal, Internal, Secret |
| UC-28a AF-1 (mời Khách) | 1499 | Bước 2: "For a Secret Meeting" |
| BR-182 | 169 trong `06-business-rules.md` | "Inviting a Guest into a Secret Meeting…" |
| UC-32c bước 3, AF-1, ghi chú rủi ro | 1594–1595 | Bỏ nhánh "Confidential": bước 3 chỉ còn "For a Secret Meeting, the system records the view…"; AF-1 bước 3 bỏ "for a Confidential Meeting, records the download in the audit log" (không còn nhật ký tải xuống) |
| BR-318 | 290 trong `06-business-rules.md` | "Each view of a document of a Secret Meeting is recorded…"; **bỏ "or download"** vì Mật không tải được và `document.downloaded` đã bỏ |
| UC-34b mô tả và bước 4 | 1659, 1662 | "the files of a Secret Meeting" |
| BR-334 | 306 trong `06-business-rules.md` | "the files of a Secret Meeting, nor that Meeting’s upload folder" |
| Đặc tả màn hình: chip, bộ lọc, cột, ô tạo cuộc họp, ô chỉ đọc | 2891, 2926, 2931, 2960, 3003 | Bỏ giá trị "Confidential [Hạn chế]"; chip còn Normal [Thường] / Internal [Nội bộ] / Secret [Mật] |
| Màn mời Khách | 3307 | "Secret shows inline warning" |
| Màn tab Tài liệu | 3547, 3581 | "Each view of a Secret Meeting’s document is recorded"; chip "Shown only for a Secret Meeting" |

Sau khi sửa, `grep -n "Confidential" Report-3_…md` chỉ còn hai dòng Change Log cũ. Bản `01-requirements/05-use-case-specifications.md`
và `06-business-rules.md` tự cập nhật khi sync Report 3 (không sửa tay).

### B2. Sequence và sơ đồ lớp

| File | Việc |
|---|---|
| [UC32c_PreviewMeetingDocument.txt](../../06-sequencediagram/document-management/UC32c_PreviewMeetingDocument.txt) dòng 72 | Khối `opt Meeting is Confidential or Secret` thành `opt Meeting is Secret`; bỏ nhánh ghi nhật ký tải xuống nếu có |
| [UC34b_ViewLockedAccountLibrary.txt](../../06-sequencediagram/document-management/UC34b_ViewLockedAccountLibrary.txt) dòng 53 | "without the files of Secret Meetings" |
| [UC28a_AddMeetingParticipant.txt](../../06-sequencediagram/meeting-participant-management/UC28a_AddMeetingParticipant.txt) dòng 54 | "Secret form displays inline warning only" |
| `UC16_ViewMeetingDetails.txt` dòng 51 | Chỉ nói "Confidentiality Level": giữ nguyên, không đổi |
| `document-management/README.md` | Rà dòng nhắc Confidential |
| [feature-07-document-management-class-diagram.drawio](../../07-classdiagram/feature-07-document-management-class-diagram.drawio) | Bỏ `Confidential` khỏi enum; làm theo [DRAWIO_EDITING_RULES.md](../../../DRAWIO_EDITING_RULES.md): sửa đúng ô, giữ ID, render lại và kiểm |

Sequence phải sửa cả file `.txt` lẫn bản ảnh PNG nếu có (xem README thư mục). Đã đúng, không cần sửa:
`02-design/08-ke-hoach-phong-hop-truc-tuyen.md` (đã ghi "nay là `Secret`, ADR-0057"), `docs/03-api`, ADR-0057.

### B3. Còn nhắc tới "bốn mức"

Note 36 không còn "bốn mức" (đã sửa trong PR #282). Rà thêm [NOTE-dac-ta-so-bo-scrum-137-142.md](../NOTE-dac-ta-so-bo-scrum-137-142.md)
và `docs/03-api/` bằng `grep -rniE "bốn mức|4 mức|Hạn chế"` trước khi xong.

### B4. Jira

Comment lên các ticket có UC bị sửa: SCRUM-22 (UC-32c, BR-318), SCRUM-58 (UC-34b, BR-334), SCRUM-20 hoặc ticket mời Khách của UC-28a
(BR-182); và SCRUM-142 (hai mốc chia sẻ, Mật không chia sẻ). Mỗi comment: UC/BR đã đổi, PR, chỗ còn hoãn.

## 3. C — Đưa note refactor BE vào `docs/notes`

Hải (bot) trả lời ngày 08/11/2026: **giữ file, đưa vào `docs/notes` thành note "đề xuất" qua PR nhỏ; Hải gắn link vào SCRUM-143.**
Ba câu hỏi ở mục 7 của note **chưa chốt** — chốt khi bắt đầu làm, sau khi phần biên bản (SCRUM-33/37) vào `main`.

> **08/11/2026 (Khánh):** refactor BE **để sau**, không làm trong đợt này. Chỉ đưa note "đề xuất" vào `docs/notes`
> (commit cùng note này, merge thẳng `main`) để Hải gắn link vào SCRUM-143. Ba câu hỏi ở mục 7 của note vẫn để mở; chốt khi
> bắt đầu làm, sau khi phần biên bản (SCRUM-33/37) vào `main` và không còn PR BE nào mở.

- File [NOTE-refactor-clean-architecture.md](../NOTE-refactor-clean-architecture.md) và dòng trong [docs/notes/README.md](../README.md) đã viết;
  đã commit ngày 08/11/2026 cùng note này.
- Không đổi code, không đổi API, không đổi mã MSG. Đây là việc **sau** khi tính năng xong và không còn PR BE nào mở (SCRUM-143, ADR trước).

## 4. Việc nhỏ khác còn từ review #275 (bot nhắc 08/11/2026)

Không nằm trong ba tin ❌ nhưng cùng loại, làm cùng lượt cho tiện:

1. BR-395/396 của SCRUM-30 trùng số với đề xuất trên `main`: khi đưa SCRUM-30 vào Report 3 thì đổi sang BR-403+.
2. Note giao BE FE-08 còn ghi "mã tiếp theo MSG408" và file `03-api/18-…` (số 18 đã có).
3. Câu MSG406/MSG407 trong luồng UC còn tiếng Việt.
4. Đặc tả SCRUM-30 (#271/#281) và SCRUM-33 (#279) đã ở `main` dưới dạng note + sequence, **chưa vào Report 3** (không có `BR-404`,
   `MSG424`; UC-82 và UC-83 chưa có mục). Đây là việc BA Final Review riêng, nên mở note sau khi xong A và B.

## 5. Thứ tự làm và kiểm

1. A (nhỏ, một lượt DOCX + note) → sync Report 3 → comment Jira.
2. B1 + B2 trong cùng một lượt DOCX và sequence → sync Report 3 → `grep` kiểm → comment Jira.
3. C bất cứ lúc nào, PR riêng.
4. Mỗi lượt: `python scripts/sync_reports.py --source … --report 3`, rồi chạy lại để chắc bản Markdown khớp từng byte; commit tiếng Việt,
   PR tiếng Anh, gửi link vào kênh `pull-request`.
5. Khi xong từng việc, đổi ❌ thành ✅ ở tin tương ứng trong `note-gửi-ba`.

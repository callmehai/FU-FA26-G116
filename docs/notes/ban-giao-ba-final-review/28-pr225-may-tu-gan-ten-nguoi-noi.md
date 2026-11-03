# PR #225 — Máy tự gán tên người nói khi giọng khớp rõ (SCRUM-35, ADR-0045): việc BA Final Review

**Người ghi:** KhanhPG (BA), 03/11/2026. **Nguồn:** tin TrustCabinet Bot 02/11/2026 21:55, [PR #225](https://github.com/callmehai/FU-FA26-G116/pull/225)
(merge 02/11/2026), [ADR-0045](../05-decisions/0045-may-tu-gan-ten-khi-giong-khop-ro.md), PO chốt 01/11/2026, Jira
[SCRUM-35](https://sep490g116.atlassian.net/browse/SCRUM-35) comment 10406, contract
[API 13 — nhận diện giọng nói](../../03-api/13-nhan-dien-giong-noi.md). Note này gom việc BA, không thay Jira hay đặc tả.

> **Tiến độ 03/11/2026: CHƯA LÀM GÌ.** Mới ghi note.

## 1. Code đã đổi (ADR-0045, PO 01/11/2026)

- **Máy tự gán tên** khi điểm so với giọng mẫu ≥ `VoiceId:AutoAssignThreshold` = **0,75**: chuyển chữ xong là giọng có
  `assignedUserId`, API trả `autoAssigned: true`; nhật ký `transcript.speaker_assigned` ghi `automatic: true` kèm điểm, không có
  người thực hiện. Thư ký **vẫn đổi hoặc bỏ** được; đổi tay thì `autoAssigned` thành `false`.
- **Dưới 0,75** vẫn chỉ **gợi ý** như cũ (`SuggestThreshold` 0,6). Tag "Khớp giọng nhất" **luôn hiện** trong ô chọn tên, kể cả khi
  giọng đã được gán (trước đây biến mất sau khi chọn).
- **Giọng thứ hai của cùng một người** phải khớp giọng mẫu ≥ `SecondVoiceThreshold` = **0,7**, nếu không thì đứng riêng. Lý do: vụ
  "[Test FE] Mic phòng – Trường Huy" 31/10 gộp hai người chung mic phòng làm một (người lạ 0,645).
- **Rút đồng ý Voice ID** xoá luôn **tên do máy gán** (cùng mọi gợi ý và giọng mẫu); **tên Thư ký gán vẫn giữ**.
- **Máy không bao giờ gán Khách** (Khách không có giọng mẫu). Chỉ gán người **đã đồng ý thông báo 1.2** — xem [note 29](29-scrum-130-docs-review-repo-uc06c-voiceid-1-2.md).
- Mỗi lần chuyển chữ lại (thêm lượt ghi) máy gán lại từ đầu, như mọi tên khác (ADR-0041/BR-345, BR-360).

## 2. Chỗ tài liệu còn nói ngược

| # | Tài liệu hiện tại | Code / ADR nói | Chỗ BA sửa |
|---|---|---|---|
| 1 | `BR-366`: "…and **never assign a name by themselves**" | Máy tự gán khi ≥ 0,75, Thư ký đổi/bỏ được | Viết lại BR-366: gợi ý luôn hiện; máy tự gán từ ngưỡng cấu hình; Thư ký quyết định cuối; không gán Khách; chỉ gán người đồng ý bản 1.2 |
| 2 | `BR-365`: rút đồng ý xoá giọng mẫu + mọi gợi ý, **giữ tên Thư ký gán** | Nay xoá thêm **tên máy gán** | Thêm vế "và mọi tên do máy tự gán"; vẫn giữ câu tên Thư ký gán được giữ |
| 3 | `BR-367`: một người có thể được gợi ý cho nhiều giọng, gộp làm một người nói | Chỉ gộp khi giọng sau khớp ≥ 0,7; dưới ngưỡng thì tách riêng | Thêm ngưỡng giọng thứ hai vào BR-367 |
| 4 | `UC-46b` AF-1: "The system **never assigns a name by itself**"; Description/NF chỉ nói gợi ý | Máy tự gán ≥ 0,75 | Sửa Description + thêm bước "sau khi chuyển chữ, giọng có điểm ≥ ngưỡng được gán tên tự động"; AF-1 thêm ý tên tự động vẫn đổi/bỏ được |
| 5 | `UC-46b` NF-1: "…and any suggested name" | Tag "Khớp giọng nhất" luôn hiện, kể cả khi đã gán | Ghi rõ tag luôn hiện và tô màu |
| 6 | `UC-46a` (xem bản gỡ băng) | Chỉ Thư ký mới thấy gợi ý; tên máy gán trông như tên đã gán | Thêm câu: tên do máy gán hiện như tên đã gán cho tới khi Thư ký đổi; chỉ Thư ký thấy tag gợi ý |
| 7 | Sequence `UC-46b`, `UC-45c` | Có bước máy gán sau khi chuyển chữ | Thêm bước tự gán + `autoAssigned`/nhật ký tự động (nếu chưa có) |
| 8 | ERD `TranscriptSpeaker` (vừa thêm 03/11) | `AssignedById` rỗng = máy gán | Đã có cột `AssignedById` nullable; chỉ cần câu trong `02-erd.md` §7: rỗng nghĩa là máy tự gán |

**Không phải sửa:** `docs/03-api/13` đã ghi đúng (mục "Máy tự gán", bảng ngưỡng, `autoAssigned`, nhật ký `automatic`); MSG không đổi.

## 3. Việc BA

- [ ] BR-366, BR-365, BR-367 theo bảng trên.
- [ ] UC-46b (Description, NF-1, AF-1) và UC-46a nếu cần; thêm ngưỡng 0,75 / 0,7 dưới dạng "ngưỡng cấu hình" chứ không ghi cứng.
- [ ] Sequence UC-46b/UC-45c: thêm bước tự gán (nếu thiếu), xuất lại PNG.
- [ ] `02-erd.md` §7: một câu `AssignedById` rỗng = máy gán; (tuỳ chọn) ghi `autoAssigned` chỉ là cờ API, không lưu.
- [ ] Sync Report 3, Change Log.
- [ ] Ghi kết quả vào Jira SCRUM-35.

## 4. Hỏi PO / Hải

- Ngưỡng 0,75 / 0,7 là **cấu hình** hay **luật nghiệp vụ**? Đặc tả nên ghi "ngưỡng cấu hình (mặc định 0,75 / 0,7)" để không phải sửa BR mỗi lần tinh chỉnh.
- Có cần mã MSG/thông báo nào khi máy gán tên không? Hiện không có (im lặng, chỉ hiện tên) — PO xác nhận là chủ đích.

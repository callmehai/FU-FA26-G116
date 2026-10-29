# Sửa wireframe và việc ngoài phạm vi sau note 17 (29/10/2026)

**Người ghi:** KhanhPG (BA). Gom những việc agent làm lượt 1–5 của [note 17](17-plan-sua-con-lai-sau-note-14.md) gặp
nhưng không sửa, vì nằm ngoài phạm vi UC/BR/MSG (xem memory "Report 3 edit scope": màn hình chỉ sửa qua wireframe).

## 1. Trạng thái Jira đã chuyển ngày 29/10/2026

| Ticket | Nội dung | Từ → sang | Căn cứ |
|---|---|---|---|
| SCRUM-23, 58, 110, 111, 112, 113 | FE-07 kho tài liệu, góp ý | BA Final Review → **In Review** | PR #186 đã merge |
| SCRUM-99 | Huỷ cuộc họp, tự huỷ quá hạn | BA Final Review → **In Review** | Note 17 lượt 1–2 |
| SCRUM-69 | Bắt đầu, kết thúc cuộc họp | BA Final Review → **In Review** | Note 17 lượt 3, đối chiếu không lệch |
| SCRUM-15 | Gửi email | BA Final Review → **In Review** | Note 17 lượt 4 |
| SCRUM-25, SCRUM-98 | Chương trình nghị sự | BA Final Review → **In Review** | Note 17 lượt 5 |

Còn ở BA Final Review: SCRUM-41 (chờ PR #147), SCRUM-29, 31, 35, 109 (ghi âm, [note 16](16-ghi-am-voice-id-28-29-10-2026.md)).

## 2. Sửa wireframe (Report 3 phần III — đặc tả màn hình)

Quy trình: sửa wireframe trong `docs/wireframe/`, xuất ảnh vào `docs/wireframe/screens/`, rồi sửa đoạn đặc tả màn hình và
thay ảnh trong DOCX Report 3, chạy `sync_reports.py`. Không dùng ảnh chụp app thật.

### 2.1 FE-05 — Chương trình nghị sự (bỏ thời lượng, PO 16/10)

- [ ] **Bảng danh mục màn hình, dòng 5.1:** bỏ "duration" khỏi mô tả.
- [ ] **Bảng danh mục, dòng 5.2 và màn 5.2 Add / Edit Agenda Item Drawer:** "Draft or Rejected meeting" sai, đúng là mọi trạng
      thái trước Ended, kể cả đang họp (BR-65, BR-74); bỏ ô thời lượng nếu wireframe còn.
- [ ] **Màn Meeting Facilitation (5.3):** bỏ "regardless of its planned duration" và "duration" ở thao tác Complete Agenda Item
      và nút "Hoàn thành mục"; chỉ ghi giờ bắt đầu, kết thúc thực tế.
- [ ] Kiểm wireframe các màn trên còn cột hoặc ô thời lượng thì xoá, xuất lại ảnh.
- [ ] Màn agenda có ô ghi kết luận (UC-77) và người trình bày chọn từ thành viên hoặc khách mời: kiểm wireframe đã có chưa.

### 2.2 FE-04 — Huỷ cuộc họp (SCRUM-99)

- [ ] Hộp thoại "Huỷ cuộc họp" có ô lý do (bắt buộc, tối đa 1.000 ký tự): kiểm Report 3 đã có đặc tả màn chưa, chưa có thì thêm.
- [ ] Màn Chi tiết cuộc họp hiện lý do, người huỷ, thời điểm huỷ (người huỷ "Hệ thống" khi tự huỷ).
- [ ] Màn Lịch: nhóm lọc "Đã huỷ" tắt mặc định, bật thì tên cuộc họp gạch ngang. Màn danh sách Cuộc họp: lọc "Đã huỷ".

## 3. Việc ngoài phạm vi khác

- [ ] **ERD `muc-tieu`:** `AgendaItem` còn `PlannedDurationMinutes`, `PresenterName`. Sửa theo `docs/02-design/02-erd.md`
      (`PresenterId`, `PresenterGuestInvitationId`, `LegacyPresenterName`, `Conclusion`, `ConclusionUpdatedById`,
      `ConclusionUpdatedAt`), xuất lại PNG.
- [ ] **Ảnh ERD trong Report 3 (mục 3.1):** thay bằng ảnh `muc-tieu` mới sau khi sửa ý trên.
- [ ] **ERD `hien-tai`:** sinh lại từ DB sau khi chạy migration mới nhất (README thư mục cấm sửa tay; bản hiện tại còn
      `starts_at`/`ends_at`).
- [ ] **Permission Matrix:** kiểm có cần dòng "Huỷ cuộc họp" (Người tổ chức) không.
- [ ] **`MSG345` trong Report 3:** câu tiếng Anh agent tự viết theo mẫu `MSG84`; đối chiếu với câu trong catalogue.
- [ ] **Sequence UC-75** (huỷ và tự huỷ): đối chiếu as-built.
- [ ] **Hỏi PO:** UC-26 đã thêm Khách mời đọc agenda (chỉ đọc, khi cuộc họp Published, InProgress, Ended) theo code và
      `docs/03-api/08-agenda-contract-tich-hop-guest-uc04.md`. PO xác nhận là chủ đích?

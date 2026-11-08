# ADR-0057: Cuộc họp chỉ có 3 mức mật như AI-eCabinet — Thường, Nội bộ, Mật; bỏ mức Hạn chế

- **Trạng thái:** Đề xuất — PO (Hải) 07/11/2026, nhóm chọn phương án A (Lộc)
- **Ngày:** 2026-11-07
- **Người quyết định:** Hải (PO)
- **Liên quan:** BR-188 (không tải tài liệu Mật), BR-318 (ghi nhật ký khi xem), UC-28a AF-1 (cảnh báo mời Khách),
  UC-34b (Quản trị xem kho tài khoản đã khoá), [ADR-0056](0056-ai-du-thao-qua-api-cho-hop-thuong.md) (dự thảo AI qua API),
  SCRUM-142 (chia sẻ lịch)

## Bối cảnh

App có 4 mức: Thường · Nội bộ · Hạn chế · Mật (`Normal`, `Internal`, `Confidential`, `Secret`). AI-eCabinet chỉ có 3:
Thường · Nội bộ · Mật. Mức Hạn chế do nhóm tự thêm. Mỗi mức khác nhau ở:

| Mỗi cuộc họp | Thường | Nội bộ | Hạn chế | Mật |
|---|---|---|---|---|
| Tải tài liệu, ghi âm, biên bản về máy | ✅ | ✅ | ✅ | ❌ |
| Ghi nhật ký mỗi lần xem tài liệu | — | — | ✅ | ✅ |
| Tài liệu ẩn khỏi kho khi Quản trị xem tài khoản đã khoá | — | — | ✅ | ✅ |
| Cảnh báo khi mời Khách | — | — | ✅ | ✅ |
| Dự thảo AI được gửi ra API ngoài | ✅ | ❌ | ❌ | ❌ |

Hạn chế chỉ khác Mật ở một điểm: vẫn tải về được. Bốn mức khó nhớ: chính tài liệu và màn Cấu hình AI đã ghi nhầm thành
"Mật, Tối mật" (ADR-0056 mục Bối cảnh và điểm 2: "Nội bộ, Mật, Tối mật" nghĩa là Nội bộ, Hạn chế, Mật).

## Quyết định

1. **Còn 3 mức: Thường, Nội bộ, Mật** (`Normal`, `Internal`, `Secret`). Bỏ `Confidential`.
2. **Mọi luật trước áp cho "Hạn chế hoặc Mật" nay chỉ áp cho Mật:** ghi nhật ký `document.viewed` mỗi lần xem tài liệu
   (BR-318), ẩn khỏi kho khi Quản trị xem tài khoản đã khoá (UC-34b), cảnh báo khi mời Khách (UC-28a AF-1). Mật vẫn không
   tải tài liệu, ghi âm, biên bản (BR-188).
3. **Bỏ hành động kiểm toán `document.downloaded`.** Nó chỉ ghi cho tài liệu Hạn chế được tải về; Mật không tải được, Thường
   và Nội bộ không ghi nhật ký.
4. **Nội bộ giữ nguyên:** như Thường, chỉ khác dự thảo AI luôn ở máy local (ADR-0056).
5. **Dữ liệu cũ:** migration `SensitivityThreeLevels` đổi mọi cuộc họp và mọi lời mời Khách đang `Confidential` thành
   `Secret`, mức chặt hơn kế bên, để không có gì đang giấu bị lộ ra. Quay lại migration thì chúng vẫn là `Secret`.
6. **API:** `sensitivity` = `Confidential` nay bị từ chối như mọi giá trị lạ (lỗi trường `sensitivity`).
7. **Chia sẻ lịch (SCRUM-142):** còn 2 mốc — mốc 1 thấy họp Thường, mốc 2 thấy Thường và Nội bộ; họp Mật không bao giờ
   được chia sẻ, kể cả chỉ hiện giờ bận.

## Phương án đã cân nhắc

| Phương án | Ưu | Nhược | Kết luận |
|---|---|---|---|
| A — 3 mức như AI-eCabinet | Theo đúng nghiệp vụ tham chiếu; Nội bộ vẫn giữ lời thoại ở máy local | Nội bộ gần như chỉ là nhãn | **Chọn** |
| B — 2 mức: Thường và Mật | Đơn giản nhất | Nội bộ thành Thường thì lời thoại cuộc họp nội bộ được gửi ra API ngoài; lệch AI-eCabinet | Bỏ |
| Giữ 4 mức | Không phải sửa gì | Hạn chế gần trùng Mật, khó nhớ, đã gây ghi nhầm | Bỏ |

## Hệ quả

- Enum, migration, FE (chọn mức, bộ lọc, chip, phòng họp, form mời Khách), test và `docs/03-api` sửa cùng PR.
- Report 3 (BR-188, BR-318, UC-28a, UC-32c, UC-34b, bảng dữ liệu Meeting, đặc tả màn hình), sơ đồ tuần tự và class diagram
  tài liệu do BA (Khánh) cập nhật trong BA Final Review.
- Ghi chú và wireframe cũ trong `docs/notes/`, `docs/wireframe/` giữ nguyên như bản ghi lịch sử.

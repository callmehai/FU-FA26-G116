# ADR-0043: Tài liệu chung vẫn ở nhóm chung khi gắn vào mục nghị sự; gỡ khỏi nhóm chung là một thao tác riêng

- **Trạng thái:** Đề xuất — PR #201, 30/10/2026
- **Ngày:** 2026-10-30
- **Người quyết định:** Lộc, yêu cầu nghiệp vụ trong PR #201 (chờ BA Final Review)
- **Thay:** phần "nhóm chung" của [ADR-0020](0020-tai-lieu-theo-quyen-xem-cuoc-hop.md) điểm 4 (UC-33d: "thành tài
  liệu chung, hoặc vẫn gắn các mục khác") và cách hiểu BR-280 "tài liệu chung = tài liệu không gắn mục nào".

## Bối cảnh

Từ SCRUM-21, danh sách tài liệu cuộc họp xếp vào nhóm "Tài liệu chung" **chỉ** những tài liệu chưa gắn mục nào.
Gắn một tài liệu chung vào một mục nghị sự nên làm nó **rời** nhóm chung. Người dùng thấy như tài liệu bị mất khỏi
nhóm chung, dù vẫn còn trong cuộc họp.

Nghiệp vụ người dùng mong muốn:

- Tệp tải thẳng vào một mục thì hiện ở mục đó **và** ở nhóm chung.
- Tệp đã ở nhóm chung, gắn thêm vào một mục, chỉ là **thêm một tham chiếu** dưới mục. Nó vẫn ở nhóm chung.
- Người dùng tự quyết bỏ tham chiếu ở nhóm chung (nút riêng), hoặc bỏ tham chiếu ở mục (nút "Gỡ khỏi mục này" đã có,
  UC-33d).

## Quyết định

1. **Mọi tài liệu cuộc họp mặc định ở nhóm chung.** Cách thêm không quan trọng: thêm chung, tải vào mục, chọn từ kho
   vào mục đều vậy. Gắn hay bỏ gắn mục không đổi điều này.
2. **Cột `meeting_documents.excluded_from_general`** (bool, mặc định `false`): Chủ toạ hoặc Thư ký đã gỡ tài liệu khỏi
   nhóm chung. Dữ liệu có sẵn nhận `false`, nên tài liệu đang gắn mục cũng hiện lại ở nhóm chung.
3. **Nhóm chung** = tài liệu chưa bị gỡ khỏi nhóm chung **hoặc** không gắn mục nào. Một tài liệu không bao giờ biến mất
   khỏi mọi nhóm khi còn trong cuộc họp (giữ đúng tinh thần BR-280).
4. **`DELETE /api/meetings/{id}/documents/{meetingDocumentId}/general`** — gỡ khỏi nhóm chung.
   - Cùng quyền và cùng mốc khoá với UC-33d: Chủ toạ hoặc Thư ký hiện tại, tới khi cuộc họp kết thúc hoặc bị huỷ.
   - Chỉ gỡ được khi tài liệu còn gắn ít nhất một mục; nếu không, `409 MSG369` (muốn bỏ hẳn thì gỡ khỏi cuộc họp,
     UC-33c).
   - Đã gỡ rồi: `404 MSG370`. Thành công: `200 MSG368`, phát `documentsChanged`.
5. **Gỡ liên kết cuối cùng** (UC-33d) xoá luôn cờ: tài liệu về nhóm chung và vẫn ở đó nếu sau này được gắn lại.

## Hệ quả

- Nhóm "Tài liệu chung" dài hơn: nó liệt kê cả tài liệu đang gắn mục, trừ những tài liệu người dùng đã gỡ.
- Chưa có thao tác "đưa lại vào nhóm chung" cho tài liệu đang gắn mục. Muốn thế thì bỏ gắn hết các mục, hoặc thêm một
  thao tác sau nếu cần. Chọn lại cùng tệp từ kho vào nhóm chung vẫn bị chặn `409 MSG165` (tệp đã có trong cuộc họp).
- BA cần sửa BR-280, UC-32a (nhóm chung), UC-33d, và thêm một luồng "gỡ khỏi tài liệu chung" vào Report 3.

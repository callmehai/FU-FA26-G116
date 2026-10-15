# ADR-0020: Tài liệu đi theo quyền xem cuộc họp; biểu quyết không gắn mục nghị sự

- **Trạng thái:** Đã chấp nhận
- **Ngày:** 2026-10-15
- **Người quyết định:** PO
- **Thay thế:** quy tắc đọc tài liệu cuộc họp tách riêng theo vai trò và theo lúc publish
  (BR-198, BR-233, BR-234, tinh chỉnh sau [ADR-0006](0006-kho-tai-lieu-ca-nhan-va-quyen-doc.md));
  phần khoá gỡ tài liệu khi đã có biên bản, một `AgendaItemId` trên dòng gắn cuộc họp, và câu
  “không tái cấp UC-33b” của [ADR-0018](0018-tai-lieu-khong-phien-ban-chi-giu-tham-chieu.md).
  UC-32b vẫn bỏ, không tái cấp. Kho cá nhân, file bất biến và thư mục trong ADR-0018 vẫn giữ.
  Biểu quyết thuộc cuộc họp đã có ở mục 5 của [ADR-0019](0019-chuan-bi-bieu-quyet-truoc-khi-hop.md);
  ADR này chỉ ghi nốt các chỗ tài liệu còn mô tả phiếu gắn mục nghị sự.

## Bối cảnh

Ngày 15/10/2026 PO chốt bảng hỏi của Khánh cho FE-07 và một câu của FE-11. Danh mục đang mô tả
quyền đọc tài liệu riêng, khoá gỡ theo biên bản, khoá gắn theo trạng thái mục nghị sự, và mỗi
tài liệu cuộc họp chỉ có một mục nghị sự. Wireframe biểu quyết cũ vẫn gắn phiếu với mục nghị sự
dù ADR-0019 đã bỏ liên kết đó khỏi đặc tả UC.

## Quyết định

### Tài liệu cuộc họp

1. **Ai xem được cuộc họp thì đọc được tài liệu của cuộc họp đó.** Tài liệu không có quyền đọc
   riêng. Bỏ BR-198, BR-233 và BR-234; thay bằng một BR: *Anyone who can view a Meeting can read
   its documents.* BR-249 giữ nguyên: ai xem được tài liệu thì bình luận được.
2. **Chủ toạ và Thư ký được thêm tài liệu.** Quyền thêm đi theo quyền vào cuộc họp, từ lúc người
   đó vào được cuộc họp cho tới khi cuộc họp kết thúc. Tài liệu không có trạng thái riêng. Sửa
   BR-195.
3. **Kết thúc họp khoá cả thêm lẫn gỡ**, kể cả khi chưa có biên bản. Sửa BR-185: điều kiện khoá
   đổi từ “đã có biên bản” thành “cuộc họp đã kết thúc”.
4. **Tách bốn việc, bốn UC:**
   - UC-33a — thêm tài liệu vào cuộc họp.
   - UC-33b — gắn tài liệu vào một mục nghị sự. PO tái cấp số UC-33b (ADR-0018 đã bỏ số này cho
     *Thêm phiên bản mới*). UC-32b vẫn không tái cấp.
   - UC-33c — gỡ tài liệu khỏi cuộc họp. Tài liệu biến mất khỏi cuộc họp và khỏi mọi mục.
   - UC-33d — bỏ gắn khỏi một mục. Tài liệu vẫn nằm trong cuộc họp: thành tài liệu chung, hoặc
     vẫn gắn các mục khác.
5. **Một tài liệu gắn được nhiều mục trong cùng cuộc họp.** Ví dụ `Báo cáo tài chính Q3.pdf` dùng
   cho mục 1 và mục 3 thì gắn cả hai, không tải lên hai lần. Bỏ cột `AgendaItemId` đơn; dùng bảng
   liên kết tài liệu cuộc họp–mục nghị sự.
6. **Mục đang bàn hoặc đã bàn xong vẫn thêm, gỡ và gắn tài liệu được**, cho tới khi cả cuộc họp
   kết thúc. Bỏ BR-196 và mọi chỗ còn khoá liên kết tài liệu theo trạng thái mục, kể cả luồng
   UC-23b.
7. **Thư mục kho cá nhân tự tạo** mang tên `<tên cuộc họp> – dd/mm/yyyy`, với ngày là **ngày bắt
   đầu dự kiến của cuộc họp, giờ Việt Nam**, kể cả khi tạo lại thư mục đã bị xoá. PO xác nhận
   dùng ngày dự kiến vì tài liệu có thể được tải trước khi họp, lúc chưa có ngày bắt đầu thực tế.
   Không dùng ngày tải tài liệu hoặc ngày bắt đầu thực tế. Thư mục đang tồn tại vẫn được dùng lại
   theo BR-227, không tự đổi tên khi tải thêm. Sửa BR-192 và BR-228.
8. **Đổi tên trong kho cá nhân không đổi tên bản trong cuộc họp.** Tên được chép lại lúc gắn vào
   cuộc họp. Thêm một BR mới; cấp số trống tiếp theo khi sửa danh mục (dải hiện tới BR-274), không
   tái cấp mã đã ngừng.

### Biểu quyết

9. **Biểu quyết chỉ thuộc cuộc họp, không gắn mục nghị sự.** Ảnh wireframe đã gửi là bản cũ.
   Quyết định này đã nằm ở ADR-0019; các bề mặt còn sót được liệt ở hệ quả.

## Phương án đã cân nhắc

| Phương án | Ưu | Nhược | Vì sao không chọn |
|---|---|---|---|
| Giữ quyền đọc tài liệu theo vai trò và theo lúc publish | Khớp BR-198, BR-233, BR-234 | Thêm một lớp quyền lệch với quyền xem cuộc họp | PO: ai xem được cuộc họp thì đọc được tài liệu |
| Khoá gỡ khi đã có biên bản, vẫn thêm/gỡ được sau khi họp kết thúc nếu chưa có biên bản | Giữ BR-185 hiện tại | Hồ sơ cuộc họp đã kết thúc còn đổi | PO: kết thúc họp khoá cả thêm lẫn gỡ |
| Mỗi tài liệu cuộc họp một mục nghị sự | Một cột `AgendaItemId` | Muốn dùng một file cho hai mục thì phải tải hai lần | PO: gắn nhiều mục, một file |
| Khoá gắn tài liệu khi mục InProgress hoặc Done | Giữ BR-196 | Quên gắn tài liệu sau khi bàn xong mục thì không gắn được | PO: tự do tới khi cuộc họp kết thúc |
| Giữ UC-33b bỏ vĩnh viễn, cấp số UC mới cho “gắn vào mục” | Khớp câu “không tái cấp” của ADR-0018 | Lệch số PO vừa chốt (33a–33d) | PO tái cấp UC-33b cho việc gắn mục |
| Biểu quyết gắn một mục nghị sự, như wireframe cũ | Khớp ảnh cũ và cột `AgendaItemId` | Lệch ADR-0019 | PO: phiếu chỉ thuộc cuộc họp |

## Hệ quả

Tài liệu chưa sửa trong lần ghi ADR này. Khi sửa, lấy ADR này làm nguồn:

- [Danh mục UC](../01-requirements/00-master-use-case-inventory.md): thêm UC-33b (gắn mục) và
  UC-33d (bỏ gắn một mục); ghi rõ tái cấp UC-33b, UC-32b vẫn bỏ.
- [Business Rules](../01-requirements/06-business-rules.md) và Report 3: bỏ BR-196, BR-198,
  BR-233, BR-234; sửa BR-185, BR-192, BR-195, BR-228; thêm BR tên được chép lúc gắn; đổi bảng
  mô tả tài liệu cuộc họp từ một `AgendaItemId` sang liên kết nhiều–nhiều.
- UC-23b: bỏ chỗ chỉ cho sửa liên kết tài liệu khi mục còn Pending.
- **Hải:** wireframe biểu quyết, và các màn 6.2, 5.3, 11.2 trong Report 3 — bỏ gắn phiếu với mục.
- **Khánh:** bảng mô tả dữ liệu Vote trong Report 3 — bỏ cột `AgendaItemId`.
- **BE:** bỏ cột `AgendaItemId` của Vote trong code, ticket SCRUM-28.

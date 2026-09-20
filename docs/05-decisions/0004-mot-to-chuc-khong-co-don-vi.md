# ADR-0004: Một tổ chức, không có đơn vị trực thuộc

- **Trạng thái:** Đã chấp nhận
- **Ngày:** 2026-09-20
- **Người quyết định:** Nhóm SEP490-G116

## Bối cảnh

Mô hình dữ liệu ban đầu ([ADR-0003](0003-chon-postgresql-thay-cho-sql-server.md), migration
`InitialCreate`) có bảng `org_units` — đơn vị trực thuộc, tự tham chiếu cha–con, được `users` và
`meetings` trỏ tới. Ý tưởng mượn từ AI-eCabinet: gán người dùng vào phòng ban, cuộc họp có "đơn vị
chủ trì", và "mời cả đơn vị" thì duyệt cây xuống các đơn vị con.

Khi BA tách Use Case cho `SCRUM-13`, câu hỏi bật ra: có cần một tab riêng để CRUD đơn vị không,
vì cây cha–con phải có màn quản trị mới sửa được. Rà lại thì phần này kéo theo nhiều việc hơn giá
trị nó mang lại cho đồ án:

- **Cây đơn vị không tự nó đứng được.** Có `ParentId` thì phải có màn hình thêm/sửa/chuyển đơn vị
  cha, phải chặn chu trình (A là cha của B, B là cha của A), phải xử lý việc giải thể một đơn vị
  còn người và còn cuộc họp cũ. Đó là một use case cỡ vừa, trong khi nghiệp vụ lõi của
  TrustCabinet là vòng đời cuộc họp.
- **"Mời cả đơn vị" là đường vòng.** Duyệt cây để ra danh sách người rồi mời, so với chọn thẳng
  người trong danh sách — kết quả giống nhau, chỉ khác số bước bấm, với quy mô một tổ chức.
- **Lý do tồn tại của nó ở hệ thống gốc không áp dụng ở đây.** AI-eCabinet là sản phẩm đi demo cho
  nhiều bên nên phải dựng nhiều tổ chức và phân cấp bên trong. TrustCabinet triển khai cho **đúng
  một tổ chức**.

## Quyết định

Chúng tôi sẽ bỏ hẳn khái niệm đơn vị khỏi hệ thống:

- Không có bảng `org_units`, không có `users.org_unit_id`, không có `meetings.org_unit_id`
  (migration `RemoveOrgUnits`).
- Không có màn hình CRUD đơn vị và không có nhóm endpoint `/api/units`.
- Thành phần tham dự chọn theo **từng người**, không có "mời cả đơn vị".
- Không có bảng tổ chức và không multi-tenant: hệ thống phục vụ một tổ chức, danh tính của tổ chức
  đó là cấu hình hiển thị, không phải dữ liệu nghiệp vụ.

Kèm theo, chốt luôn phương thức xác thực cho `SCRUM-11`: **email + mật khẩu, hoặc đăng nhập
Google**. Không dựng IdP riêng (Zitadel hay tương đương) như hệ thống gốc — nó là một hệ thống
phải vận hành riêng, ngoài phạm vi một kỳ đồ án.

## Phương án đã cân nhắc

| Phương án | Ưu | Nhược | Vì sao không chọn |
|---|---|---|---|
| Giữ nguyên cây đơn vị cha–con | Sát hệ thống gốc; mời cả phòng ban trong một thao tác | Kéo theo một use case quản trị riêng: chặn chu trình, chuyển cha, giải thể đơn vị còn dữ liệu | Chi phí không tương xứng với giá trị ở quy mô một tổ chức |
| Giữ `org_units` nhưng bỏ `ParentId`, danh sách phẳng | Rẻ hơn cây; vẫn lọc và thống kê được theo phòng ban | Vẫn cần chỗ quản trị danh sách, vẫn phải quyết định chuyện gì xảy ra khi phòng ban giải thể | Bỏ nửa vời: giữ chi phí mà không giữ được lợi ích chính là "mời cả đơn vị" |
| Bỏ hẳn (đã chọn) | Mô hình gọn đi một bảng và hai khoá ngoại; `SCRUM-13` thu về đúng phần quản lý tài khoản | Mất chiều "theo đơn vị" ở báo cáo và bộ lọc lịch | — |

## Hệ quả

**Phải làm:**

- Migration `RemoveOrgUnits` xoá bảng và hai cột. Ai đã chạy database cũ ở máy mình thì
  `dotnet ef database update` là xong — hàm `Down` dựng lại được cấu trúc, nhưng **dữ liệu đơn vị
  đã có thì mất**, và đó là dữ liệu seed nên không tiếc.
- Cập nhật Jira: `SCRUM-13` bỏ phần đơn vị và `/api/units`; `SCRUM-20` chỉ mời theo cá nhân;
  `SCRUM-38` và `SCRUM-47` bỏ chiều "theo đơn vị".
- Test `Nobody_belongs_to_an_organisational_unit` trong `DatabaseModelTests` chặn việc thêm lại
  cột `OrgUnit*` hay `Department*` vào bất kỳ entity nào — cùng kiểu với test chặn voiceprint.

**Mất gì:** báo cáo (`SCRUM-38`) và bộ lọc lịch (`SCRUM-47`) không còn cắt số liệu theo phòng ban;
chỉ còn theo tháng, theo trạng thái, theo mức mật và theo vai trò tham dự.

**Nếu sau này cần lại:** thêm `users.department` dạng **chuỗi tự do** trước đã — đủ để hiển thị và
nhóm số liệu mà không kéo theo màn quản trị nào. Chỉ dựng lại bảng khi có nhu cầu thật sự cần khoá
ngoại, và khi đó viết ADR mới thay thế ADR này.

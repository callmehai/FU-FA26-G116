# ADR-0003: Chọn PostgreSQL thay cho SQL Server

- **Trạng thái:** Đã chấp nhận
- **Ngày:** 2026-09-19
- **Người quyết định:** Nhóm SEP490-G116

## Bối cảnh

README và bảng Công nghệ từ đầu ghi "SQL Server + EF Core", và tiêu chí nghiệm thu của `SCRUM-45`
cũng vậy. Đó là lựa chọn mặc định vì backend là ASP.NET Core, chưa từng được cân nhắc thành một
quyết định riêng.

Khi chuẩn bị dựng cơ sở dữ liệu thật, ba ràng buộc lộ ra:

- **Chi phí host.** Nhóm có $100 credit AWS, và tài khoản mở sau 15/07/2025 nên thuộc Free Plan
  mới: hết hạn sau 6 tháng hoặc khi hết credit, không còn free tier 12 tháng. SQL Server cần
  khoảng 2 GB RAM chỉ cho riêng nó, buộc phải thuê VM 4 GB.
- **Máy của nhóm.** Ảnh Docker của SQL Server chỉ có bản `linux/amd64`. Máy Apple Silicon phải
  chạy qua emulation — lần đầu `docker compose up` mất khoảng 10 phút.
- **Hệ thống tham chiếu.** AI-eCabinet chạy PostgreSQL. Mọi ý tưởng schema mượn từ nó đều phải
  dịch kiểu dữ liệu nếu dùng SQL Server.

## Quyết định

Chúng tôi sẽ dùng **PostgreSQL 17** làm cơ sở dữ liệu, qua Npgsql cho EF Core 10, vẫn giữ
code-first và migration.

Kèm theo ba lựa chọn cụ thể để tránh bẫy khi chuyển từ SQL Server sang:

- **snake_case** cho tên bảng và cột (`EFCore.NamingConventions`). PostgreSQL hạ chữ thường mọi
  identifier không đóng ngoặc kép, nên giữ PascalCase sẽ buộc mọi câu SQL viết tay phải gõ
  `SELECT * FROM "Meetings"`.
- **`citext`** cho `users.email`. PostgreSQL so sánh chuỗi phân biệt hoa thường, khác SQL Server —
  nếu không đổi kiểu, unique index vẫn cho phép tồn tại cả `Anh@x.vn` lẫn `anh@x.vn` thành hai tài
  khoản riêng.
- Đổi bảng lịch sử migration thành `__ef_migrations_history` cho đồng bộ với phần còn lại.

## Phương án đã cân nhắc

| Phương án | Ưu | Nhược | Vì sao không chọn |
|---|---|---|---|
| **PostgreSQL** | ~256 MB RAM, VM 2 GB là đủ; ảnh arm64 native; nhiều bậc free ngoài AWS; trùng hệ tham chiếu | Nhóm quen SQL Server hơn; vài khác biệt phải học (hoa thường, `timestamptz`) | **Đã chọn** |
| SQL Server Express | Quen thuộc; T-SQL và SSMS nhiều người biết | Cần ~2 GB RAM nên VM phải 4 GB, gấp đôi tiền; emulation trên Apple Silicon; giới hạn 10 GB | Chi phí host gấp đôi mà không đổi lại được gì cho đồ án |
| SQL Server Developer Edition trên VM | Miễn phí license, đủ tính năng | License chỉ cho phép dùng ở môi trường dev/test, không dùng cho hệ thống chạy thật | Ranh giới pháp lý không rõ khi demo trước hội đồng |
| SQLite | Không cần server, nhẹ nhất | Kém khi nhiều người ghi đồng thời; thiếu kiểu dữ liệu và ràng buộc cần cho nghiệp vụ | Sẽ phải đổi lần nữa khi có người dùng thật |

## Hệ quả

**Được:**

- VM 2 GB (~$12/tháng) thay vì 4 GB (~$24/tháng) cho cùng một hạ tầng, nên $100 credit đủ phủ hết
  trần 6 tháng của Free Plan. Đẩy database sang một bậc free bên ngoài thì còn rẻ hơn nữa.
- `docker compose up` xong trong vài giây trên mọi máy trong nhóm.
- Schema của AI-eCabinet đọc thẳng được, không phải dịch kiểu dữ liệu.

**Mất và phải làm thêm:**

- Nhóm phải học vài khác biệt. Ba cái hay vấp nhất — `DateTimeOffset` phải có offset 0, so sánh
  chuỗi phân biệt hoa thường, `LIKE` phải đổi thành `ILIKE` — đã ghi trong
  [03-co-so-du-lieu.md](../02-design/03-co-so-du-lieu.md).
- `SCRUM-45` và bảng Công nghệ trong README phải sửa lại.
- Rủi ro "bị yêu cầu dùng stack Microsoft" đã được loại: nhóm xác nhận ngày 2026-09-19 rằng đồ án
  không bị ràng buộc công nghệ. Nếu điều đó thay đổi thì phải quay lại, và chi phí chuyển đổi tăng
  theo số bảng — ở thời điểm quyết định mới có 6 bảng và không có dòng SQL thô nào.

Quyết định này chỉ nói về cơ sở dữ liệu. Backend vẫn là ASP.NET Core 10 và EF Core, không đổi.

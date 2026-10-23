# ADR-0024: Lưu file tài liệu trên Cloudflare R2, mọi byte đi qua backend

- **Trạng thái:** Đề xuất — SCRUM-107
- **Ngày:** 2026-10-23
- **Người quyết định:** BA (Lộc), chờ PO duyệt

## Bối cảnh

[ADR-0018](0018-tai-lieu-khong-phien-ban-chi-giu-tham-chieu.md) chốt rằng mỗi lần tải lên tạo một `StoredFile`
bất biến, byte nằm trong "kho đối tượng", nhưng chưa nói kho đó là gì. SCRUM-21/22/23 (xem, kiểm quyền, góp ý)
đều cần cuộc họp đã có file, nên phần tải lên và lưu file được tách thành SCRUM-107.

Ràng buộc:

- Server test là một máy Lightsail 1 GB, đĩa nhỏ ([ADR-0015](0015-server-test-tren-lightsail.md)). Mỗi file tối đa
  50 MB, mỗi lần tải tối đa 10 file / 200 MB (BR-214, BR-319). Để file trên đĩa máy đó thì nhanh đầy và mất khi dựng
  lại máy.
- Loại file phải xác định theo nội dung thật (BR-215), và lưu đúng byte đã nhận (BR-216).
- Quyền đọc phải kiểm lại ở **mọi** request, kể cả link tải: mất quyền xem cuộc họp thì request kế tiếp bị từ chối
  (SCRUM-22, BR-275).
- Server test có Content-Security-Policy chỉ cho `connect-src 'self'` (`deploy/Caddyfile`).

## Quyết định

1. **Byte lưu trên Cloudflare R2**, một bucket private, gọi qua API tương thích S3. Không bật public URL, không dùng
   custom domain cho bucket.
2. **Mọi byte đi qua backend, cả hai chiều.** Trình duyệt chỉ gọi `/api/...` trên chính tên miền của app.
   - Tải lên: trình duyệt → backend → R2, đọc theo luồng (không giữ cả file trong RAM). Backend kiểm loại file theo
     nội dung và tính SHA-256 trên chính luồng đó.
   - Xem trước, tải về: backend kiểm quyền, đọc object từ R2 rồi trả về theo luồng. **Không phát presigned URL** cho
     trình duyệt.
3. **Khoá object là `stored-files/{storedFileId}`**. Khoá không chứa tên file, ID cuộc họp hay người tải, vì tên
   object có thể lộ qua log hay bảng điều khiển của R2. Tên file chỉ nằm trong DB.
4. **Tất cả hoặc không (BR-194, BR-286).** Backend đẩy mọi object của một lần tải lên R2 trước, rồi ghi DB trong
   một transaction. Transaction lỗi thì xoá lại các object vừa đẩy; xoá không được thì ghi log cảnh báo kèm khoá
   object. Không thêm trạng thái "đang tải" vào `StoredFile`.
5. **Job dọn file mồ côi (BR-213)** xoá object trên R2 rồi ghi `PurgedAt`, giữ dòng `StoredFile` như ADR-0018 §7.
6. **Cấu hình và bí mật.** Endpoint, tên bucket, access key và secret key đọc từ cấu hình (`dotnet user-secrets` khi
   dev, biến môi trường khi deploy), không đặt trong `appsettings.json`. Access key chỉ có quyền đọc/ghi đúng bucket
   đó.
7. **Dev local dùng SeaweedFS** (`chrislusf/seaweedfs`, Apache-2.0) trong `docker-compose.yml`, cùng giao thức S3,
   nên dev và server test chạy chung một đoạn code. Test tự động (cả trên CI) thay kho file bằng bản giả trong bộ
   nhớ, không cần kho thật. *Sửa khi làm SCRUM-107:* bản đề xuất ghi MinIO, nhưng MinIO đã ngừng phát hành image
   công khai (Docker Hub, quay.io đều không kéo được nữa), nên đổi sang SeaweedFS.

## Phương án đã cân nhắc

| Phương án | Ưu | Nhược | Vì sao không chọn |
|---|---|---|---|
| **R2, byte đi qua backend** | Không phí egress; kiểm nội dung và quyền ở server; CSP giữ nguyên | Băng thông và CPU của Lightsail phải gánh mỗi lượt tải lên và xem | **Chọn** |
| R2, trình duyệt tải thẳng bằng presigned URL | Server nhẹ tải | Phải mở CSP `connect-src` và CORS của bucket; server chỉ kiểm được nội dung sau khi file đã lên; link tải còn dùng được trong thời hạn TTL kể cả khi người dùng vừa mất quyền (trái SCRUM-22) | Đổi tính đúng của quyền đọc lấy hiệu năng mà đồ án chưa cần |
| Lưu trên đĩa của máy Lightsail | Không thêm dịch vụ | Đĩa nhỏ, mất khi dựng lại máy, khó sao lưu | Không chịu được 200 MB mỗi lần tải |
| Lưu byte trong PostgreSQL (`bytea`) | Một nơi sao lưu, transaction thật cho cả byte | DB phình nhanh, backup và migration chậm, tốn RAM của máy 1 GB | Không hợp với file 50 MB |
| AWS S3 | Cùng hệ AWS với Lightsail | Phí egress mỗi lượt xem/tải | R2 không tính egress, cùng API S3 nên đổi lại về sau không tốn |

## Hệ quả

- **Không đổi ERD vì R2.** `StoredFile.StorageKey` đã độc lập với nơi lưu; một bucket lấy từ cấu hình nên không cần
  cột bucket. Cùng đợt rà này, ERD bỏ `Document.Category` (PO bỏ "Phân loại" ngày 22/10/2026) và thêm
  `StoredFile.OriginalFileName` (ADR-0018 §7 đã nói `StoredFile` giữ tên file) — hai điểm này không do R2.
- **Giới hạn request.** Kestrel mặc định chỉ nhận body 30 MB; endpoint tải lên phải nâng giới hạn cho đủ 200 MB.
  Caddy mặc định không giới hạn body.
- **CSP giữ nguyên**, vì trình duyệt không gọi thẳng R2.
- **Có thể sót object.** Khi transaction lỗi mà xoá object cũng lỗi, object đó không có dòng `StoredFile` nên job dọn
  không thấy. Số lượng nhỏ, có log để dọn tay. Cần dọn tự động thì job duyệt danh sách object trên R2 và so với DB.
- **Dữ liệu cá nhân (Nghị định 13/2023/NĐ-CP).** Tài liệu cuộc họp có thể chứa dữ liệu cá nhân, mà R2 lưu ở trung tâm
  dữ liệu ngoài Việt Nam, tức là chuyển dữ liệu cá nhân ra nước ngoài. Server test của đồ án chỉ dùng dữ liệu giả. Đưa
  vào dùng thật thì đơn vị vận hành phải lập hồ sơ đánh giá tác động chuyển dữ liệu ra nước ngoài, hoặc chuyển sang kho
  đối tượng đặt trong nước có API S3 — code không phải đổi, chỉ đổi cấu hình.
- **Việc phát sinh:** tạo bucket và access key R2 cho server test; thêm biến môi trường vào `deploy/` và
  [deploy/README.md](../../deploy/README.md); thêm dịch vụ SeaweedFS vào `docker-compose.yml` và hướng dẫn trong
  [02-design/03-co-so-du-lieu.md](../02-design/03-co-so-du-lieu.md) — làm cùng SCRUM-107.

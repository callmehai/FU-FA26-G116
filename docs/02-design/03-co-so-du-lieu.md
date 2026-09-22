# Cơ sở dữ liệu — cài đặt và migration

PostgreSQL + EF Core 10 (Npgsql), **code-first**: mô hình nằm trong code C#, schema sinh ra từ
migration. Không sửa database bằng tay — sửa entity rồi tạo migration.

Mô hình dữ liệu: [02-erd.md](02-erd.md). Vì sao PostgreSQL chứ không phải SQL Server:
[ADR-0003](../05-decisions/0003-chon-postgresql-thay-cho-sql-server.md).

## Lần đầu trên máy mới

### 1. Dựng PostgreSQL

**Cách nhanh nhất — Docker** (repo có sẵn [docker-compose.yml](../../docker-compose.yml)):

```bash
cp .env.example .env        # rồi mở .env đặt mật khẩu của riêng bạn
docker compose up -d
docker compose ps           # chờ STATUS là "healthy" — thường dưới 10 giây
```

`.env` đã nằm trong `.gitignore`. Đừng commit nó, và đừng dùng lại mật khẩu này ở đâu khác.

Ảnh `postgres:17-alpine` có sẵn bản arm64 nên chạy native trên cả máy Apple Silicon lẫn Intel,
không phải emulation.

**Hoặc cài trực tiếp**: PostgreSQL 15 trở lên, bằng installer chính thức, Homebrew hay apt đều
được. Nhớ tạo database và user trùng với `docker-compose.yml`, hoặc sửa chuỗi kết nối cho khớp.

### 2. Khai báo chuỗi kết nối

Chuỗi kết nối **không nằm trong repo**. `appsettings.json` để trống có chủ đích — mỗi người tự đặt
bằng user-secrets:

```bash
dotnet user-secrets set "ConnectionStrings:TrustCabinet" \
  "Host=localhost;Port=5432;Database=trustcabinet;Username=trustcabinet;Password=<mật khẩu trong .env>" \
  --project backend/TrustCabinet.Web
```

user-secrets lưu ngoài thư mục repo nên không thể commit nhầm. `UserSecretsId` đã nằm sẵn trong
`TrustCabinet.Web.csproj` nên không cần chạy `dotnet user-secrets init`. Khi triển khai thật thì
dùng biến môi trường `ConnectionStrings__TrustCabinet`.

Chưa đặt mà chạy app thì backend dừng ngay với thông báo chỉ đúng vào lệnh trên — đó là cố ý, chạy
với database sai còn tệ hơn là không chạy.

### 3. Chạy ứng dụng

```bash
dotnet run --project backend/TrustCabinet.Web
```

Ở môi trường Development, ứng dụng **tự chạy migration và seed dữ liệu mẫu khi khởi động**, nên
không cần nhớ lệnh nào sau khi `git pull` về một migration mới. Muốn tự kiểm soát thì đặt
`Database:MigrateAndSeedOnStartup` thành `false` trong `appsettings.Development.json`.

Ngoài Development thì **không bao giờ** tự migrate — triển khai thật chạy `dotnet ef database
update` như một bước có người duyệt.

## Dữ liệu tham chiếu — bắt buộc phải seed

Khác với dữ liệu mẫu ở mục dưới (chỉ có ở Development), **ma trận `role_capabilities` là dữ liệu
tham chiếu mà mọi môi trường đều phải có**. Bảng này quyết định vai trò nào làm được gì.

Từ `SCRUM-60`, model **không còn khai báo các dòng này bằng `HasData`** nữa. Quản trị viên sửa
được ma trận lúc chạy, mà `HasData` thì khiến migration sau sinh `UPDATE`/`DELETE` đè lên đúng
những sửa đổi đó — lý do đầy đủ ở
[ADR-0007](../05-decisions/0007-ma-tran-phan-quyen-sua-duoc-luc-chay.md).

**Database migrate từ đầu vẫn có sẵn ma trận**, vì migration `InitialCreate` có lệnh `InsertData`
chèn đúng 5 dòng đó, và migration đã lên `main` thì không sửa được nữa. Nên luồng thông thường
không phải làm gì thêm.

[`ReferenceDataSeeder`](../../backend/TrustCabinet.Infrastructure/Seed/ReferenceDataSeeder.cs) là
**lưới an toàn** cho trường hợp bảng rỗng: restore một bản dump không kèm dữ liệu của migration đó,
hoặc dựng schema bằng cách khác. Bảng rỗng nghĩa là không vai trò nào có `AdminUsers` → **không ai
đăng nhập vào sửa được**, chỉ còn cách `INSERT` thẳng vào database.

| Môi trường | Cách seed |
|---|---|
| Development | Tự động, cùng lúc với migrate và dữ liệu mẫu. Không phải làm gì thêm. |
| Ngoài Development | Bật `Database:SeedReferenceDataOnStartup` nếu muốn có lưới an toàn lúc khởi động. |

Seeder chỉ chèn khi bảng **rỗng hoàn toàn**, nên nó không bao giờ đè lên chỉnh sửa của quản trị
viên — kể cả khi chạy ở mỗi lần khởi động. Bù từng dòng thiếu thì không phân biệt được với một
dòng quản trị viên đã cố ý gỡ, nên seeder không làm việc đó.

Kiểm tra nhanh xem đã seed chưa:

```sql
SELECT role, capability FROM role_capabilities ORDER BY role;
```

## Dữ liệu mẫu

Seed chỉ chạy khi bảng `users` rỗng, nên khởi động lại không nhân bản hay đè lên dữ liệu bạn đang
làm dở. Muốn làm lại từ đầu: `docker compose down -v && docker compose up -d` rồi chạy lại app.

Gồm 4 người dùng (mỗi vai trò một người) và 5 cuộc họp giống hệt dữ liệu cứng mà giao
diện đang dùng, để màn hình nhìn không khác gì trước khi có backend.

Cả 4 tài khoản dùng chung một mật khẩu: **`TrustCabinet@2026`** (hằng số `DemoPassword` trong
`DevelopmentDataSeeder`). Mật khẩu được băm lúc seed chứ không dán sẵn hash vào source — một hash
trong repo public thì vẫn là credential, và nó sẽ âm thầm hết khớp nếu tham số của hasher đổi.

> **Mật khẩu biết trước này chỉ tồn tại trong Development.** `DevelopmentDataSeeder` không bao giờ
> chạy ngoài Development (xem `MigrateAndSeedDevelopmentAsync`), nên không có triển khai thật nào
> mang tài khoản mật khẩu công khai. Bản ghi trước đây của tài liệu này — *"để repo public mà seed
> sẵn mật khẩu biết trước là mời người ta vào"* — nói về việc seed mật khẩu cho **mọi** môi trường;
> ranh giới Development mới là thứ làm cho việc này an toàn, chứ không phải việc giấu mật khẩu đi.

Cơ sở dữ liệu Development dựng từ trước `SCRUM-11` có `password_hash` là chuỗi đánh dấu
`SET-BY-SCRUM-11`. Seeder tự thay giá trị đó bằng hash thật khi khởi động, nên không cần dựng lại
DB; nó chỉ đụng đúng những dòng mang chuỗi đó, không đè mật khẩu ai đặt tay.

## Quy ước đặt tên

Bảng và cột dùng **snake_case** (`meeting_status_history`, `starts_at`), sinh tự động từ
`UseSnakeCaseNamingConvention()`. Lý do: PostgreSQL hạ chữ thường mọi identifier không đóng ngoặc
kép, nên để PascalCase thì mọi câu SQL viết tay đều phải gõ `SELECT * FROM "Meetings"`. Trong code
C# tên vẫn là `Meeting.StartsAt` như bình thường — EF lo phần ánh xạ.

Bảng lịch sử migration cũng đổi thành `__ef_migrations_history` cho đồng bộ, thay vì
`__EFMigrationsHistory` mặc định của EF.

## Xem dữ liệu bằng DBeaver

Khi cần nhìn thẳng vào bảng — kiểm tra migration chạy đúng chưa, seed vào chưa, cột kiểu gì.

1. **Database → New Database Connection → PostgreSQL**.
   Lần đầu DBeaver sẽ hỏi tải driver, bấm *Download*.
2. Điền:

   | Trường | Giá trị |
   |---|---|
   | Host | `localhost` |
   | Port | `5432` |
   | Database | `trustcabinet` |
   | Username | `trustcabinet` |
   | Password | mật khẩu trong `.env` của bạn |

3. **Test Connection** → *Finish*. Không phải chỉnh gì về SSL: container dev không bật TLS và
   driver PostgreSQL không ép, khác hẳn SQL Server.

Duyệt tới bảng: `trustcabinet` → **Schemas** → `public` → **Tables**. Chuột phải một bảng →
*View Data*, hoặc mở SQL Editor (`Ctrl/Cmd + ]`) rồi chạy:

```sql
SELECT title, status, sensitivity, mode, starts_at FROM meetings ORDER BY starts_at;
SELECT full_name, email, role FROM users ORDER BY role;
SELECT role, capability FROM role_capabilities ORDER BY role;
SELECT from_status, to_status, occurred_at FROM meeting_status_history ORDER BY occurred_at;

-- EF ghi lại các migration đã áp ở đây
SELECT * FROM __ef_migrations_history;
```

`__ef_migrations_history` là chỗ đầu tiên nên nhìn khi nghi ngờ database lệch với code: nó liệt kê
đúng những migration đã chạy.

> **Chỉ đọc, đừng sửa.** Database là code-first — sửa bảng bằng tay sẽ lệch với migration, và
> người tiếp theo chạy `database update` sẽ gặp lỗi khó hiểu. Muốn đổi schema thì sửa entity rồi
> tạo migration.

Không dùng DBeaver cũng được — pgAdmin kết nối y hệt, hoặc gõ thẳng trong container:

```bash
docker exec -it trustcabinet-postgres psql -U trustcabinet -d trustcabinet -c "\dt"
```

## Vòng đời migration

```bash
# Sau khi sửa entity hoặc configuration
dotnet dotnet-ef migrations add TenMigrationRoRang \
  --project backend/TrustCabinet.Infrastructure \
  --startup-project backend/TrustCabinet.Web \
  --output-dir Migrations

# Áp lên database (Development thì app tự làm khi khởi động)
dotnet dotnet-ef database update \
  --project backend/TrustCabinet.Infrastructure \
  --startup-project backend/TrustCabinet.Web

# Gỡ migration mới tạo nhưng CHƯA commit và CHƯA ai áp
dotnet dotnet-ef migrations remove \
  --project backend/TrustCabinet.Infrastructure \
  --startup-project backend/TrustCabinet.Web
```

`dotnet-ef` được ghim phiên bản trong [dotnet-tools.json](../../dotnet-tools.json), chạy
`dotnet tool restore` một lần là có. Không cần cài global.

`migrations add` không cần database đang chạy: EF chỉ đọc mô hình, thông qua
`TrustCabinetDbContextFactory` trong project Infrastructure.

### Quy tắc

- **Migration đã merge vào `main` là bất biến.** Muốn sửa thì thêm migration mới, không sửa file cũ
  — người khác đã áp nó lên database của họ rồi.
- Commit cả file migration lẫn `TrustCabinetDbContextModelSnapshot.cs`. Thiếu snapshot thì
  migration kế tiếp sinh ra sai.
- Đặt tên migration theo việc nó làm (`AddDocumentTables`), không phải theo ngày.
- Hai người cùng thêm migration trong một sprint thì người merge sau phải rebase và sinh lại
  migration của mình — EF không tự trộn hai snapshot.

## Ba cái bẫy riêng của PostgreSQL

Ai từng làm SQL Server rất dễ vấp:

- **`DateTimeOffset` phải có offset bằng 0.** Npgsql ánh xạ nó sang `timestamptz` và ném lỗi nếu
  offset khác 0. Luôn dùng `DateTimeOffset.UtcNow`, và khi nhận giờ từ client thì
  `.ToUniversalTime()` trước khi lưu.
- **So sánh chuỗi phân biệt hoa thường.** Khác SQL Server. Cột `users.email` dùng kiểu `citext` nên
  so sánh không phân biệt hoa thường — cột nào khác cần điều đó thì phải khai báo rõ, đừng cho là
  mặc định.
- **`LIKE` cũng phân biệt hoa thường** — tìm kiếm cho người dùng phải dùng `ILIKE`. Việc này sẽ
  đụng ngay ở `SCRUM-18` (tìm kiếm danh sách cuộc họp).

## Cấu trúc project

| Project | Chứa gì |
|---|---|
| `TrustCabinet.Domain` | Entity và enum. Không tham chiếu EF Core, không biết gì về database |
| `TrustCabinet.Infrastructure` | `DbContext`, cấu hình bảng, migration, seed |
| `TrustCabinet.Web` | API. Tham chiếu Infrastructure, gọi `AddTrustCabinetDatabase()` |

Domain tách khỏi EF để quy tắc nghiệp vụ về sau không bị buộc vào một ORM cụ thể. Nếu lớp nghiệp vụ
lớn thêm thì tách tiếp `TrustCabinet.Application` như README đã dự tính.

# Cơ sở dữ liệu — cài đặt và migration

SQL Server + EF Core 10, **code-first**: mô hình nằm trong code C#, schema sinh ra từ migration.
Không sửa database bằng tay — sửa entity rồi tạo migration.

Mô hình dữ liệu: [02-erd.md](02-erd.md).

## Lần đầu trên máy mới

### 1. Dựng SQL Server

**Cách nhanh nhất — Docker** (repo có sẵn [docker-compose.yml](../../docker-compose.yml)):

```bash
cp .env.example .env        # rồi mở .env đặt mật khẩu của riêng bạn
docker compose up -d
docker compose ps           # chờ tới khi cột STATUS là "healthy"
```

`.env` đã nằm trong `.gitignore`. Đừng commit nó, và đừng dùng lại mật khẩu này ở đâu khác.

> Máy Apple Silicon: ảnh SQL Server chỉ có bản `linux/amd64` nên chạy qua emulation của Docker
> Desktop — khởi động chậm hơn, lần đầu có thể mất vài phút. Đó là bình thường.

**Hoặc cài trực tiếp**: SQL Server Developer Edition (Windows) hay SQL Server trên WSL2. Phiên bản
nào cũng được miễn là từ 2019 trở lên.

### 2. Khai báo chuỗi kết nối

Chuỗi kết nối **không nằm trong repo**. `appsettings.json` để trống có chủ đích — mỗi người tự đặt
bằng user-secrets:

```bash
dotnet user-secrets set "ConnectionStrings:TrustCabinet" \
  "Server=localhost,1433;Database=TrustCabinet;User Id=sa;Password=<mật khẩu trong .env>;TrustServerCertificate=True" \
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

## Dữ liệu mẫu

Seed chỉ chạy khi bảng `Users` rỗng, nên khởi động lại không nhân bản hay đè lên dữ liệu bạn đang
làm dở. Muốn làm lại từ đầu: xoá database rồi chạy lại app.

Gồm 3 đơn vị, 4 người dùng (mỗi vai trò một người) và 5 cuộc họp giống hệt dữ liệu cứng mà giao
diện đang dùng, để màn hình nhìn không khác gì trước khi có backend.

> **Chưa đăng nhập được bằng các tài khoản này.** `PasswordHash` của chúng là chuỗi đánh dấu
> `SET-BY-SCRUM-11`, không phải hash của mật khẩu nào cả. Đăng nhập nội bộ là việc của `SCRUM-11`;
> người làm story đó thay giá trị này bằng hash thật. Để repo public mà seed sẵn mật khẩu biết
> trước là mời người ta vào.

## Xem dữ liệu bằng DBeaver

Khi cần nhìn thẳng vào bảng — kiểm tra migration chạy đúng chưa, seed vào chưa, cột kiểu gì.

1. **Database → New Database Connection → SQL Server**.
   Chọn driver **Microsoft Driver** (không phải jTDS). Lần đầu DBeaver sẽ hỏi tải driver, bấm
   *Download*.
2. Điền:

   | Trường | Giá trị |
   |---|---|
   | Host | `localhost` |
   | Port | `1433` |
   | Database | `TrustCabinet` |
   | Authentication | SQL Server Authentication |
   | Username | `sa` |
   | Password | mật khẩu trong `.env` của bạn |

3. **Bật "Trust server certificate"** — nằm ngay dưới ô mật khẩu ở bản DBeaver mới, hoặc ở tab
   *Driver properties* → đặt `trustServerCertificate` = `true`.

   Bỏ qua bước này là lỗi ngay. SQL Server 2022 bắt buộc mã hoá kết nối, mà container dev chỉ có
   chứng chỉ tự ký, nên driver từ chối với thông báo kiểu *"The driver could not establish a secure
   connection… PKIX path building failed"*. Đây là nguyên nhân gần như mọi ca kết nối hỏng.

4. Muốn thấy cả `master`, `tempdb`: tab *SQL Server* → tick **Show all databases**.
5. **Test Connection** → *Finish*.

Duyệt tới bảng: `TrustCabinet` → **Schemas** → `dbo` → **Tables**. Chuột phải một bảng → *View
Data* để xem dòng, hoặc mở SQL Editor (`Ctrl/Cmd + ]`) rồi chạy:

```sql
SELECT Title, Status, Sensitivity, Mode, StartsAt FROM Meetings ORDER BY StartsAt;
SELECT FullName, Email, Role FROM Users;
SELECT Role, Capability FROM RoleCapabilities ORDER BY Role;
SELECT * FROM MeetingStatusHistory ORDER BY OccurredAt;

-- EF ghi lại các migration đã áp ở đây
SELECT * FROM __EFMigrationsHistory;
```

Bảng `__EFMigrationsHistory` là chỗ đầu tiên nên nhìn khi nghi ngờ database lệch với code: nó liệt
kê đúng những migration đã chạy.

> **Chỉ đọc, đừng sửa.** Database là code-first — sửa bảng bằng tay sẽ lệch với migration, và
> người tiếp theo chạy `database update` sẽ gặp lỗi khó hiểu. Muốn đổi schema thì sửa entity rồi
> tạo migration.

Không dùng DBeaver cũng được: Azure Data Studio kết nối y hệt, hoặc gõ thẳng trong container:

```bash
docker exec -it trustcabinet-sqlserver /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -d TrustCabinet \
  -Q "SELECT name FROM sys.tables ORDER BY name"
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

## Cấu trúc project

| Project | Chứa gì |
|---|---|
| `TrustCabinet.Domain` | Entity và enum. Không tham chiếu EF Core, không biết gì về database |
| `TrustCabinet.Infrastructure` | `DbContext`, cấu hình bảng, migration, seed |
| `TrustCabinet.Web` | API. Tham chiếu Infrastructure, gọi `AddTrustCabinetDatabase()` |

Domain tách khỏi EF để quy tắc nghiệp vụ về sau không bị buộc vào một ORM cụ thể. Nếu lớp nghiệp vụ
lớn thêm thì tách tiếp `TrustCabinet.Application` như README đã dự tính.

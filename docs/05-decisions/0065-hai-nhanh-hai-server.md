# ADR-0065: Hai nhánh, hai server — `main` là bản demo, `app` là backend bốn project

- **Trạng thái:** Đề xuất — PO (Hải) 14/11 và 18/11/2026
- **Ngày:** 2026-11-18
- **Người quyết định:** Hải (PO)
- **Bổ sung:** [ADR-0047](0047-server-test-chay-staging-deploy-an-toan.md) (chỉ `main` được deploy),
  [ADR-0015](0015-server-test-tren-lightsail.md) (server test trên Lightsail)
- **Liên quan:** SCRUM-143, SCRUM-185 (CA-20); ADR-0064 (backend bốn project, chỉ có trên nhánh `app`)

## Bối cảnh

Việc tách backend thành bốn project (SCRUM-143, ADR-0064) làm trên nhánh tích hợp `refactor/clean-architecture`, và
quy chuẩn của việc tách định merge nhánh đó vào `main` một lần ở CA-20. Ngày 14/11 PO quyết định khác: `main` giữ
nguyên làm bản demo cho hội đồng, bản tách đi riêng, chạy trên server riêng. Ngày 18/11 PO quyết định giữ hẳn hai
nhánh thay vì merge: bản bốn project có thể thành bản chính sau này, nhưng bản demo không được đổi theo.

ADR-0047 chỉ cho `main` deploy, vì server dùng chung một database: deploy một nhánh có migration chưa merge rồi quay
về `main` để lại schema mà `main` không biết.

## Quyết định

1. **Nhánh `refactor/clean-architecture` đổi tên thành `app`** và không merge vào `main`. Hai nhánh cùng sống.
2. **Mỗi nhánh một server**, dựng giống nhau theo `deploy/README.md`, mỗi máy một database, một bộ secret và một kho
   tệp:

   | Nhánh | Server Lightsail | URL |
   |---|---|---|
   | `main` | `trustcabinet-4gb` | https://trustcabinet.workspace-hub.space |
   | `app` | `trustcabinet-refactor` | https://app.trustcabinet.workspace-hub.space |

3. **Cả hai nhánh tự deploy** bằng cùng `deploy.yml`: CI chạy khi push lên `main` hoặc `app`, job deploy chọn
   environment GitHub theo nhánh (`server-test`, `server-app`), mỗi environment giữ ba secret `DEPLOY_*` của máy nó.
   Lý do của ADR-0047 vẫn đúng cho từng server: mỗi server chỉ nhận đúng một nhánh, nên database của nó chỉ thấy
   migration của nhánh đó.
4. **Server `app` không dùng chung bucket R2 với server demo.** Việc dọn tệp mồ côi xoá mọi tệp mà database của server
   không biết, nên chung bucket là server này xoá tài liệu của server kia. Server `app` chạy kho S3 tự host (service
   `files`, SeaweedFS, profile `files`) trên đĩa của chính nó.
5. **`main` chảy sang `app`, không ngược lại.** Sửa lỗi hay tính năng làm trên `main` thì đưa sang `app` bằng một PR
   merge `main` vào `app` (hoặc một PR sửa riêng trên `app` khi code hai bên đã khác). Code chỉ có trên `app` không
   về `main`.

## Hệ quả

- Mỗi lỗi chung phải sửa hai lần hoặc merge `main` vào `app` rồi gỡ xung đột ở các file đã chuyển project. Càng để
  lâu, hai nhánh càng khác nhau.
- Thêm một máy $24/tháng. Credit AWS còn đủ tới hết tháng 12/2026; sau buổi bảo vệ xoá cả hai máy.
- Đăng nhập Google trên `app.` chỉ chạy khi origin của nó có trong OAuth client của Google.
- Báo cáo (Report 4) và tài liệu thiết kế trên `main` vẫn vẽ ba project; bản bốn project chỉ nằm trên nhánh `app`.

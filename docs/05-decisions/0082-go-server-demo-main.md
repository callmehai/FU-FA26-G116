# ADR-0082: Gỡ server demo của nhánh `main`, chỉ còn server `app`

- **Trạng thái:** Chấp nhận — Hải 24/11/2026
- **Ngày:** 2026-11-24
- **Người quyết định:** Hải
- **Thay:** phần "hai nhánh, hai server" của [ADR-0065](0065-hai-nhanh-hai-server.md)
- **Liên quan:** [ADR-0068](0068-app-la-nhanh-chinh.md) (`app` là nhánh chính, `main` đóng băng), [ADR-0047](0047-server-test-chay-staging-deploy-an-toan.md)

## Bối cảnh

Từ 20/11 mọi việc làm trên `app` và `main` đóng băng (ADR-0068), nhưng server demo của `main` (Lightsail
`trustcabinet-4gb`, https://trustcabinet.workspace-hub.space) vẫn chạy và vẫn tốn credit AWS ($24/tháng). Không ai
còn dùng nó, và credit cần dồn cho server `app` để chuyển chữ nhanh hơn trước buổi ra hội đồng.

## Quyết định

1. **Xoá server demo:** Hải xoá máy `trustcabinet-4gb`, IP tĩnh của nó và bản ghi DNS `trustcabinet` trên Namecheap
   ngày 24/11. Lightsail chỉ còn `trustcabinet-refactor` và IP tĩnh `trustcabinet-refactor-ip`.
2. **Nhánh `main` giữ nguyên trên GitHub** ở commit đóng băng (tag `demo-2026-11-18` vẫn còn), chỉ không deploy nữa.
3. **Workflow chỉ deploy `app`:** `deploy.yml` luôn dùng environment `server-app` và từ chối nhánh khác; job deploy
   trong `ci.yml` chỉ chạy khi push lên `app`. CI vẫn chạy trên `main` nếu có push.
4. **Gỡ secret của server cũ:** environment `server-test` và ba secret `DEPLOY_*` cấp repo đã xoá.

## Hệ quả

- Link https://trustcabinet.workspace-hub.space không còn mở được; README và `deploy/README.md` trỏ sang
  https://app.trustcabinet.workspace-hub.space.
- Muốn dựng lại bản demo của `main` thì làm theo `deploy/README.md` trên một máy mới, tạo lại environment riêng cho nó
  và thêm lại nhánh vào `deploy.yml`.
- Dữ liệu trên server demo (database, tệp trên Cloudflare R2) không chép sang server `app`; đó là dữ liệu test.

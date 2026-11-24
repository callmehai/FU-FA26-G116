# ADR-0082: Gỡ server demo của nhánh `main`, server `app` lên máy 4 vCPU

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
5. **Server `app` lên máy compute-optimized 4 vCPU, 8 GB** (bundle `c_xlarge_1_0`, $84/tháng), thay máy $24 có 2 vCPU
   burstable (thực chất một nhân, chạy lâu thì bị hạ tốc). Hải thêm $60 credit cho việc này. Máy mới tên
   `trustcabinet-app`, dựng từ snapshot `trustcabinet-refactor-2026-11-24` ngày 24/11, giữ IP tĩnh
   `trustcabinet-refactor-ip` nên tên miền không đổi; secret `DEPLOY_HOST_KEY` của `server-app` đã đổi theo host
   key mới. Worker chuyển chữ trên server chạy 4 luồng, giới hạn 4500m. Nó là máy dự phòng; máy chính là Mac
   ([ADR-0081](0081-chuyen-chu-chay-may-mac-truoc-server-du-phong.md)).

## Hệ quả

- Link https://trustcabinet.workspace-hub.space không còn mở được; README và `deploy/README.md` trỏ sang
  https://app.trustcabinet.workspace-hub.space.
- Muốn dựng lại bản demo của `main` thì làm theo `deploy/README.md` trên một máy mới, tạo lại environment riêng cho nó
  và thêm lại nhánh vào `deploy.yml`.
- Đo trước khi đổi máy (24/11, một đoạn 6,4 phút giọng máy đọc, hồ sơ `room` có tách người nói): server cũ 83 giây,
  Mac M1 22,6 giây.
- Credit: một máy $84/tháng thay hai máy $24.
- Dữ liệu trên server demo (database, tệp trên Cloudflare R2) không chép sang server `app`; đó là dữ liệu test.

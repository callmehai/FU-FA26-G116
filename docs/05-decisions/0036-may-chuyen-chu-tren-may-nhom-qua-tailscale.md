# ADR-0036: Server test gọi máy chuyển chữ chạy trên máy của nhóm, qua Tailscale

- **Trạng thái:** Đề xuất — SCRUM-31, PO 28/10/2026
- **Ngày:** 2026-10-28
- **Người quyết định:** Hải (PO)

## Bối cảnh

Worker nhận dạng giọng nói ([ADR-0026](0026-tu-chay-nhan-dang-giong-noi.md)) cần khoảng 2,5 GB RAM. Server test là máy
Lightsail 1 GB ([ADR-0015](0015-server-test-tren-lightsail.md)), nên trên link chung, chuyển chữ, Voice ID và ghi theo micro
từng người đều trả `MSG312`. Cách đã ghi trong `deploy/README.md` §10 là lên máy 4 GB ($24/tháng), nhưng tài khoản AWS mới
bị chặn các gói lớn và yêu cầu mở gói gửi AWS Support từ khoảng 26/10/2026 vẫn chưa có trả lời. Nhóm cần demo với giảng viên hướng
dẫn ngay.

## Quyết định

1. **Server test gọi worker chạy trên một máy của nhóm (MacBook M1 16 GB của PO), qua Tailscale.** Server và máy đó cùng ở
   một tailnet; app trên server gọi `http://<ip Tailscale của máy>:8090`. Không máy nào mở cổng ra internet.
2. **Worker ngoài server bắt buộc có khoá.** `SPEECH_API_KEY` đặt ở cả hai đầu; worker từ chối mọi request không có khoá,
   trừ `/health`. Worker chỉ nghe trên IP Tailscale của máy, không nghe trên wifi.
3. **Máy chạy worker chạy thẳng bằng venv, không qua Docker.** Trên M1, Docker (máy ảo Linux) chậm hơn khoảng 3 lần
   (RTF 0,22–0,26 so với 0,07–0,10, `workers/speech/README.md`).
4. **Bản ghi nằm trên volume `recordings` của server**, không còn trong container (trước đây mỗi lần deploy là mất bản ghi).
5. Khi AWS cho lên máy 4 GB thì quay về cách A (worker trên server). Hai cách chỉ khác `SPEECH_URL` trong `deploy/.env`.

## Phương án đã cân nhắc

| Phương án | Ưu | Nhược | Vì sao không chọn |
|---|---|---|---|
| Đợi AWS mở gói 4 GB | Âm thanh không rời server | Chưa biết bao giờ, $24/tháng | Không kịp buổi báo cáo |
| Đường hầm SSH ngược (`ssh -R`) từ máy nhóm lên server | Không cần tài khoản mới | Đứt khi đổi wifi hoặc máy ngủ, phải tự nối lại; sửa `sshd_config` của server | Tailscale tự nối lại |
| Cloudflare Tunnel | Không cần cài gì trên server | Tên miền phải chuyển DNS sang Cloudflare, hoặc dùng URL tạm đổi mỗi lần chạy; worker lộ ra internet, chỉ còn khoá bảo vệ | Lộ ra ngoài, rắc rối DNS |
| Thuê máy EC2 riêng chạy worker theo giờ | Không phụ thuộc laptop | Tính tiền theo giờ, phải bật tắt tay | Nhóm chỉ còn khoảng 100 USD credit |

## Hệ quả

- **Âm thanh cuộc họp rời server**, đi qua kênh WireGuard mã hoá của Tailscale đến máy của nhóm, máy chịu trách nhiệm vận hành
  bản thử nghiệm. Worker không lưu gì: tệp tạm xoá khi xong request, không ghi nội dung vào log (ADR-0026). Chỉ dùng
  dữ liệu thử, không đưa bản ghi thật của người ngoài nhóm lên link chung khi đang chạy cách này.
- **Máy đó tắt, ngủ hay mất mạng thì chức năng giọng nói trên link chung trả `MSG312`**, như lúc chưa bật. Câu nói theo
  micro được trình duyệt giữ và gửi lại khi máy chạy lại; bản ghi cả phòng thì xếp hàng chờ.
- Tốc độ theo máy của nhóm: M1 chạy venv RTF khoảng 0,1, tức một giờ họp chuyển chữ trong khoảng 6 phút.
- Thêm `Speech__ApiKey`, `VoiceId__EncryptionKey` và volume `recordings` vào `deploy/docker-compose.yml`; cách bật nằm
  ở `deploy/README.md` §10.

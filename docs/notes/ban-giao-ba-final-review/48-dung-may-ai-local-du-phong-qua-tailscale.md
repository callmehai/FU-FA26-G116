# Dựng máy AI local dự phòng cho TrustCabinet (Ollama qua Tailscale)

**Người ghi:** KhánhPG/BA, 15/11/2026. **Nguồn:** Hải nhờ Khánh (nhắn 15/11/2026) — cho agent chạy
trên máy dự phòng 8 bước dưới đây, xong gửi Hải dòng IP agent báo lại.

**Đã kiểm trùng:** chưa có note nào về việc dựng máy AI dự phòng. Trong repo chỉ có quyết định và hợp đồng:
[ADR-0054](../../05-decisions/0054-ai-du-thao-bien-ban-chay-tai-cho.md) (AI chạy tại chỗ, gọi qua
`POST {Ai:BaseUrl}/chat/completions`), [ADR-0056](../../05-decisions/0056-ai-du-thao-qua-api-cho-hop-thuong.md),
[ADR-0036](../../05-decisions/0036-may-chuyen-chu-tren-may-nhom-qua-tailscale.md) (mẫu gọi qua Tailscale),
[18-may-ai.md](../../03-api/18-may-ai.md) (cấu hình `Ai:BaseUrl`/`Ai:Model`, nút Thử kết nối, `MSG413`).
Note này là việc vận hành máy, không sửa Report 3.

## 1. Việc cần làm (nguyên văn 8 bước Hải gửi — agent tự chạy trên máy dự phòng)

Trước đó chỉ cần mở link mời Tailscale Hải gửi và đăng nhập.

1. Kiểm RAM (cần từ 16 GB) và ổ trống (cần khoảng 6 GB). Không đủ thì dừng và báo tôi.
2. Cài Ollama nếu chưa có. Tải model `qwen2.5:7b-instruct`.
3. Kiểm Tailscale đã cài và đang kết nối. Lấy IPv4 Tailscale của máy (dạng `100.x.x.x`).
   Chưa kết nối thì dừng và bảo tôi mở link mời.
4. Cho Ollama chỉ nghe trên IP Tailscale đó, cổng 11434 (`OLLAMA_HOST=<ip>:11434`). Đặt biến này
   cố định để khởi động lại máy vẫn giữ. Không dùng `0.0.0.0`, không mở ra mạng LAN. Chỉ để một
   tiến trình `ollama serve` chạy.
5. Cho Ollama tự chạy khi đăng nhập máy. Tắt chế độ ngủ khi cắm sạc.
6. Nếu tường lửa chặn cổng 11434 trên giao diện Tailscale thì mở cho giao diện đó thôi.
7. Kiểm bằng: `curl http://<ip>:11434/v1/models` (phải thấy `qwen2.5:7b-instruct`), rồi gửi thử một
   câu tới `http://<ip>:11434/v1/chat/completions` và đo thời gian trả lời.
8. Báo lại: IP Tailscale, thời gian trả lời, và cách bật lại nếu Ollama tắt.

Không cài thêm gì ngoài Ollama và model. Không đổi cài đặt mạng khác.

## 2. Nghiệm thu và bàn giao

- [ ] `curl http://<ip>:11434/v1/models` thấy `qwen2.5:7b-instruct`.
- [ ] Gọi thử `/v1/chat/completions` thành công, ghi lại thời gian trả lời.
- [ ] Chỉ một `ollama serve`, chỉ nghe `<ip>:11434` (không `0.0.0.0`, không LAN).
- [ ] Khởi động lại máy vẫn giữ cấu hình; biết cách bật lại khi Ollama tắt.
- [ ] Gửi Hải một dòng: IP Tailscale agent báo lại (+ thời gian trả lời để đối chiếu với số đo
      máy chính ở `18-may-ai.md`).

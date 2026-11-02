# Đo tải phòng họp trực tuyến và ghi âm

Bộ script đo xem server chịu được bao nhiêu khi nhiều người cùng vào phòng gọi, cùng nói (ghi âm theo micro từng
người, SCRUM-109) và cùng bật mic phòng (ADR-0041). Không chạy trong CI; chạy tay khi cần số liệu, rồi dọn dữ liệu.

| Script | Đo cái gì | Công cụ |
|---|---|---|
| [`setup.py`](setup.py) | Tạo `loadNN@…` và các cuộc họp `[TEST] Đo tải …` đang diễn ra, ghi `out/fixture.json` | Python (thư viện chuẩn) |
| [`make_audio.sh`](make_audio.sh) | Sinh giọng nói tổng hợp (giọng *Linh* của macOS) nên không cần giọng ai thật | `say`, `ffmpeg` |
| [`call.js`](call.js) | Nhiều người vào phòng cùng lúc: đọc phòng, lấy token LiveKit, giữ hub SignalR | [k6](https://k6.io) |
| [`speech-clips.js`](speech-clips.js) | Số câu nói mỗi phút tăng dần; mỗi câu được gỡ băng trước khi request trả về | k6 |
| [`room_mic.py`](room_mic.py) | Mic phòng: gửi từng phần 10 giây, tắt mic, chờ gỡ băng; nhiều cuộc họp cùng lúc hoặc nhiều lượt nối tiếp | Python |
| `lk load-test` | Media (SFU): người phát hình/tiếng, người xem; k6 không nói được WebRTC | [LiveKit CLI](https://docs.livekit.io/home/cli/) |
| [`monitor.py`](monitor.py) | CPU, RAM từng container trong lúc đo (qua `ssh` nếu là server) | Python |
| [`cleanup.py`](cleanup.py) | Xoá bản ghi, kết thúc cuộc họp, vô hiệu hoá `loadNN` | Python |

## Chạy

```bash
brew install k6 livekit-cli ffmpeg
tests/load/make_audio.sh                                   # out/audio/*.wav
python3 tests/load/setup.py --base http://localhost:5225 --users 30 \
  --modes Online,Hybrid,Hybrid,Hybrid,Hybrid > tests/load/out/fixture.json

cd tests/load
python3 monitor.py --out out/stats.csv --pid "$(pgrep -f TrustCabinet.Web | head -1)" &
k6 run -e TABS=4 -e SPREAD=10 -e HOLD=60 call.js           # 30 người × 4 tab vào trong 10 giây
k6 run -e PEAK=180 -e STEP=2m --out csv=out/clips.csv.gz speech-clips.js
python3 room_mic.py --fixture out/fixture.json --audio out/audio/room-300s.wav --takes 3 --speed 10
python3 room_mic.py --fixture out/fixture.json --audio out/audio/room-60s.wav --rounds 4 --offset 3 --speed 10
lk load-test --url ws://localhost:7880 --api-key trustcabinet-dev --video-publishers 8 --subscribers 16 \
  --layout 3x3 --duration 1m                               # LIVEKIT_API_SECRET lấy từ .env
python3 cleanup.py --fixture out/fixture.json
```

Trên server test:

- **Base URL:** dùng `--base https://trustcabinet.workspace-hub.space`. Python bản python.org cần `SSL_CERT_FILE=/etc/ssl/cert.pem`.
- **Mật khẩu `loadNN`:** đặt một mật khẩu ngẫu nhiên qua `LOAD_MEMBER_PASSWORD`.
- **Theo dõi tài nguyên:** chạy `monitor.py --ssh tc-server --stop-mem 85`.
- **`speech-clips.js`:** tự dừng khi p95 vượt 30 giây.
- **`lk load-test`:** chạy trong một container cạnh LiveKit trên chính server, vì mạng nhà chỉ tải xuống khoảng 10 Mbps, đo từ máy nhà thì chỉ đo được mạng nhà. Secret không ra khỏi server:

  ```bash
  ssh tc-server "docker run --rm --network trustcabinet-test_default \
    --env-file <(grep '^LIVEKIT_API_SECRET=' ~/TrustCabinet/deploy/.env) livekit/livekit-cli:latest \
    load-test --url ws://livekit:7880 --api-key trustcabinet-test --video-publishers 8 --subscribers 16 --layout 3x3 --duration 1m"
  ```

  Bộ tạo tải ăn CPU ngang LiveKit, nên số đo này là mức **thấp**. Server thật không phải gánh bộ tạo tải.

## Kết quả 03/11/2026

Server test: Lightsail 2 vCPU, 4 GB. Máy chuyển chữ dùng `SPEECH_THREADS=2`, giới hạn 2,5 GB.
Local: MacBook Apple Silicon, máy chuyển chữ giới hạn 2 CPU giống server. Commit `1ad9ec32`.

### Vào phòng họp (`call.js`)

| Nơi | Tải | `GET call` + `POST call/join`, p95 | Hub sẵn sàng, p95 | Lỗi |
|---|---|---|---|---|
| Server | 30 người trong 10 giây | 169 ms | 355 ms | 0 |
| Server | 120 kết nối (30 × 4 tab) trong 10 giây | 1,53 s | 1,26 s | 0 |
| Local | 500 kết nối trong 5 giây | 64 ms | 33 ms | 0 |

Lúc 120 kết nối ập vào, PostgreSQL có một mẫu CPU 178%. Mẫu này chỉ xuất hiện một lần, chưa rõ truy vấn nào gây ra.

### Ghi âm theo micro từng người (`speech-clips.js`, server)

Mỗi câu dài 1–6 giây. Thời gian tính từ lúc gửi câu tới lúc nhận chữ.

| Câu/phút (toàn server) | p50 | p95 |
|---|---|---|
| 10–60 | 0,9–1,3 s | 1,2–2,3 s |
| 68 | 0,9 s | 1,5 s |
| 81 | 1,1 s | 1,7 s |
| 88 | 7,7 s | 12,7 s |
| 86 (phút kế tiếp) | 20,3 s | 27,6 s → tự dừng; 8 câu nhận `503 MSG312` (trình duyệt giữ câu và gửi lại) |

**Ngưỡng của server là khoảng 85 câu/phút**, tương đương 5–6 người nói liên tục cùng lúc trên cả server. Một cuộc họp có một
người nói tại một thời điểm tạo khoảng 10–15 câu/phút. Phòng hybrid mà ba laptop cùng thu một người nói thì gấp ba.
Máy local tới 60 câu/phút p95 0,5 s; 300 câu/phút thì p95 13,7 s.

### Mic phòng (`room_mic.py`, server)

| Tải | Gửi xong | Phần chậm nhất | Chờ trong hàng | Gỡ băng | Hệ số thời gian thực |
|---|---|---|---|---|---|
| 3 cuộc họp, mỗi cuộc 5 phút, cùng lúc | 32–33 s (gửi nhanh gấp 10) | 1,7 s | 3 s / 66 s / 124 s | 58–63 s | 0,19–0,21 |
| 1 cuộc họp, 4 lượt 1 phút nối nhau | 7 s mỗi lượt | 0,6 s | 2–3 s | 15 → 27 → 38 → 51 s | 0,21–0,25 |

- **Hàng chờ:** máy chuyển chữ nhận từng bản ghi một, nên bản thứ K phải chờ K−1 bản trước.
- **Chi phí mỗi lượt:** mỗi lượt mới chạy lại cả băng (quyết định PO 01/11, như AI-eCabinet), nên lượt thứ n tốn thời gian của n lượt.
- **Ước tính cho cuộc họp 2 giờ chia 12 lượt 10 phút:**
  - tổng việc khoảng 0,2 × 10 phút × 78 ≈ 2,6 giờ máy;
  - bản cuối xong khoảng 24 phút sau lượt cuối;
  - trong lúc đó máy chuyển chữ bận, các câu nói của cuộc họp trực tuyến khác phải xếp hàng sau.

### Media LiveKit (`lk load-test`, bộ tạo tải trên chính server, mỗi bậc 1 phút)

| Người phát / người xem | CPU LiveKit (200% = 2 vCPU) | CPU cả máy | Luồng nhận được | Mất gói |
|---|---|---|---|---|
| 4 hình / 8 | 32% | 41% | 32/32 | 0% |
| 8 hình / 16 | 53% | 71% | 128/128 | 0% |
| 12 hình / 24 | 80% | 86% → dừng tăng | 216/288 | 0% |
| 10 tiếng / 40 (50 người, không camera) | 71% | 84% | 360/400 | 0% |
| 6 hình / 44 (50 người) | 172% | bão hoà | 264/264 | 0% |

Đo qua mạng thật (3 trình duyệt headless ở Việt Nam, camera 360p và mic giả, token lấy từ `/call/join`):

| Chỉ số | Kết quả |
|---|---|
| Thời gian vào phòng | 1–2,4 s |
| RTT | 59–66 ms |
| Mất gói | 0% |
| Jitter | ≤ 4 ms |
| Video | 20 fps |
| Đường truyền | UDP |

### Kết luận

- **Một cuộc họp:** tới 25–30 người, bật dưới 8 camera, kèm ghi âm theo micro từng người, chạy thoải mái. 50 người
  (`max_participants`) thì chạm trần CPU 2 vCPU, nhất là khi máy chuyển chữ cũng đang chạy.
- **Máy chuyển chữ dùng chung:** đây là giới hạn chính.
  - Nó dùng chung 2 vCPU với LiveKit.
  - Sau đợt đo nó giữ 1,74 GB RAM (lúc rảnh khoảng 400 MB), để lại khoảng 900 MB cho phần còn lại.
  - Muốn vài cuộc họp lớn cùng lúc thì cần máy 4 vCPU, hoặc tách máy chuyển chữ sang máy riêng
    ([ADR-0036](../../docs/05-decisions/0036-may-chuyen-chu-tren-may-nhom-qua-tailscale.md) đã có đường đi).
- **Mic phòng nhiều lượt:** tốn thời gian máy theo bình phương số lượt.
- **Join API và hub:** không phải điểm nghẽn ở quy mô này.

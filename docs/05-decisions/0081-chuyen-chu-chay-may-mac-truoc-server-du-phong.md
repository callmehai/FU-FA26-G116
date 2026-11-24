# ADR-0081: Chuyển chữ chạy máy Mac trước, server dự phòng

- **Trạng thái:** Đề xuất — PO (Hải) 24/11/2026
- **Ngày:** 2026-11-24
- **Người quyết định:** Hải (PO)
- **Bổ sung:** [ADR-0026](0026-tu-chay-nhan-dang-giong-noi.md) (worker nhận dạng giọng nói nội bộ),
  [ADR-0036](0036-may-chuyen-chu-tren-may-nhom-qua-tailscale.md) (cách B: worker trên máy của nhóm qua Tailscale);
  cùng cách làm với [ADR-0063](0063-may-ai-du-phong.md) (máy AI dự phòng)

## Bối cảnh

Từ 01/11/2026 server test chạy worker chuyển chữ ngay trên server (cách A ở `deploy/README.md` §10): máy Lightsail
2 vCPU, RTF khoảng 0,2–0,26, tức một giờ họp mất 12–15 phút, và trong lúc đó phòng họp có thể giật vì CPU là loại
*burst*. MacBook M1 của PO chạy worker thẳng bằng venv nhanh hơn 5–10 lần (RTF 0,07–0,10, `workers/speech/README.md`),
nhưng máy đó không bật suốt: dùng nó làm worker duy nhất như cách B (ADR-0036) thì máy ngủ là mọi chức năng giọng nói
báo `MSG312`.

PO muốn: máy nào nhanh hơn thì chạy trước, Mac tắt thì về server. Backend lúc này chỉ biết một địa chỉ worker
(`Speech:BaseUrl`).

## Quyết định

1. **Thêm một worker dự phòng:** `Speech:BackupBaseUrl` (`SPEECH_BACKUP_URL` trong `deploy/.env`). Kế hoạch trên server:
   `SPEECH_URL=http://<IP Tailscale của Mac>:8090` (máy chính, có `SPEECH_API_KEY`), `SPEECH_BACKUP_URL=http://speech:8090`
   (worker trong Docker, giữ `COMPOSE_PROFILES=speech`). Cả hai nhận cùng khoá; worker trong Docker không đặt khoá nên
   nhận mọi request, khoá gửi kèm không hại gì. Để trống `SPEECH_BACKUP_URL` thì mọi thứ như cũ.
2. **Chỉ chuyển khi không tới được máy chính.** Ba trường hợp chuyển sang máy kia:
   - không mở được kết nối (máy từ chối, không có đường, hết thời gian chờ kết nối): máy chính chưa hề nhận request;
   - kết nối đứt trước khi có câu trả lời, kể cả khi đang chạy giữa chừng (Mac ngủ, rời Tailscale). Worker tự dừng khi
     mất kết nối; cùng tệp âm thanh, cùng tham số thì máy kia cho cùng loại kết quả, nên chạy lại từ đầu là đúng. Mã lượt
     chạy (`run`) giữ nguyên, thanh tiến độ đi theo máy mới;
   - không có trường hợp thứ ba: **máy đã trả lời thì đó là câu trả lời**, kể cả 4xx/5xx. `400 unreadable_audio`,
     `401` sai khoá, `422` mẫu giọng bị từ chối, `500` lỗi trong worker đều báo người dùng như trước, không thử máy kia:
     lỗi đó do request hay cấu hình, máy kia sẽ báo y như vậy hoặc che mất lỗi cần sửa (cùng lý do với ADR-0063). Worker
     không bao giờ trả `503` (việc đến khi bận thì xếp hàng, trạng thái `queued`), nên `503` chỉ có thể từ thứ gì đứng
     trước worker; giữa server và worker không có gì, nên cũng không chuyển.

   Hết thời hạn cả request (`Speech:TimeoutMinutes`, 120 phút) sau khi đã gửi âm thanh thì **không** chuyển: máy đó đã
   nhận việc và chạy suốt thời gian ấy, chạy lại trên server còn chậm hơn.
3. **Chờ kết nối tối đa 5 giây** (`Speech:ConnectTimeoutSeconds`). Worker đang chạy nhận kết nối ngay cả khi đang bận
   (mỗi request một luồng), Tailscale đi vòng qua relay cũng dưới một giây. Ngắn hơn 10 giây của máy AI vì câu nói theo
   micro là thời gian thực: người nói chờ câu hiện chữ. Đo trên máy thật: máy chính không có đường (IP bỏ đi) cộng máy
   dự phòng từ chối, sau 2,03 giây (chờ kết nối đặt 2 giây) đã báo cả hai không kết nối được.
4. **Máy chính không tới được thì 2 phút sau mới hỏi lại.** Trong 2 phút đó máy dự phòng được hỏi trước, nên mỗi câu
   nói không phải chờ 5 giây. Máy dự phòng cũng không tới được thì máy chính được hỏi tiếp; cả hai đều không được thì
   bản gỡ băng báo lỗi "Không kết nối được máy chuyển chữ chính lẫn máy dự phòng"; câu nói theo micro và mẫu giọng vẫn
   trả `MSG312` như khi worker tắt (không thêm mã thông báo mới).
   Trạng thái này nằm trong bộ nhớ của app, khởi động lại app thì lần gọi đầu lại thử Mac trước.
5. **Âm thanh gửi lại từ đầu.** Mọi chỗ gọi đều đưa stream tua lại được (tệp bản ghi `FileStream`, câu nói và mẫu giọng
   `MemoryStream`), nên lần thử thứ hai tua về vị trí cũ. Stream không tua được (chưa có chỗ nào) chỉ được gửi lại khi
   lần đầu chưa đọc byte nào, tức kết nối chưa từng mở.
6. **Hỏi tiến độ đúng máy đang chạy.** Backend nhớ lượt chạy nào đang ở máy nào trong lúc chạy và hỏi `/v1/progress`
   máy đó; lượt không biết thì hỏi máy chính.
7. **Mỗi lần chuyển ghi một dòng log** ("Speech primary unreachable, using backup"), chỉ tên loại lỗi, không có nội
   dung cuộc họp hay tên người.

## Voice ID: vector giọng của hai máy so được với nhau

Voice ID so vector giọng từ mẫu giọng với vector từng người nói trong bản ghi. Một mẫu thu lúc Mac bật, một bản ghi
chuyển chữ lúc Mac tắt thì hai vector đến từ hai máy khác nhau. Chúng vẫn cùng một không gian:

- **Cùng tệp model.** `models.manifest` ghim mã băm SHA-256 từng tệp; `fetch_models.py` tải và kiểm tra, worker kiểm
  tra lại lúc khởi động và không chạy nếu lệch. Tên model gửi về kèm 12 ký tự đầu mã băm
  (`eres2net-base-200k@e2d2048292e0`), và backend chỉ so mẫu giọng cùng tên model (`ListMatchableSamplesAsync`): nếu
  Mac chạy bản code cũ với model khác thì mẫu giọng chỉ không được so, không bao giờ so nhầm.
- **Cùng thư viện, cùng cách chạy.** `requirements.txt` ghim `sherpa-onnx==1.13.8`, `onnxruntime==1.31.1`; cả hai máy chạy
  sherpa-onnx trên CPU (code không đặt `provider`, mặc định là `cpu`; không bật CoreML trên Mac), cùng Python 3.12, cùng
  ngưỡng mặc định.
- **Khác nhau còn lại** là CPU (ARM của M1, x86 của Lightsail), số luồng (`SPEECH_THREADS` 4 so với 2) và bản `ffmpeg`
  giải mã âm thanh (Homebrew so với Debian). Chúng chỉ làm lệch số thực ở các chữ số rất nhỏ, không đổi model hay cách
  tính, nên độ giống cosin giữa hai máy không đổi đáng kể so với ngưỡng Voice ID (0,6, ADR-0074). Chưa đo riêng con số
  lệch này.

Việc phải giữ: Mac chạy đúng bản `workers/speech` của nhánh `app` đang deploy (pull rồi chạy lại `fetch_models.py` mỗi
khi `models.manifest` đổi).

## Phương án đã cân nhắc

| Phương án | Ưu | Nhược | Vì sao không chọn |
|---|---|---|---|
| Chỉ Mac (cách B, ADR-0036) | Nhanh | Mac ngủ là hết chuyển chữ | Không chịu được buổi họp lúc Mac tắt |
| Chỉ server (cách A, đang chạy) | Luôn có | Chậm 5–10 lần, phòng họp giật khi chuyển bản ghi dài | Đây là cái đang muốn cải thiện |
| Server làm chính, Mac dự phòng | Âm thanh ở server khi có thể | Mac gần như không bao giờ được dùng: server luôn tới được | Ngược mục đích |
| Hỏi `/health` cả hai máy rồi chọn | Biết trước máy nào sống | Thêm một vòng hỏi mỗi câu nói; máy sống lúc hỏi vẫn có thể ngủ ngay sau đó, nên vẫn cần chuyển khi gửi | Chuyển khi gửi là đủ, ít phức tạp hơn |
| Chuyển cả khi máy chính trả 5xx | Che được worker lỗi | Che mất lỗi cấu hình, chạy lại việc máy chính đã làm hỏng | Giống ADR-0063: chỉ chuyển khi không tới được |
| Chạy song song trên cả hai, lấy kết quả đến trước | Luôn nhanh nhất | Server vẫn gánh mọi việc, phòng họp vẫn giật | Không đỡ được CPU của server |

## Hệ quả

- **Âm thanh mọi cuộc họp (kể cả Mật) và mẫu giọng đi sang Mac khi Mac bật**, qua kênh WireGuard mã hoá của Tailscale,
  như cách B của ADR-0036 nhưng nay là đường mặc định. Worker không lưu gì: tệp tạm xoá khi xong request, không ghi nội
  dung vào log, vector giọng chỉ trả về backend (ADR-0026). Việc lưu, mã hoá, đồng ý và xoá dữ liệu cá nhân, kể cả dữ
  liệu sinh trắc học theo Luật 91/2025/QH15, vẫn chỉ ở backend và không đổi: rút đồng ý vẫn xoá mọi mẫu giọng, không có
  bản nào nằm lại trên Mac. Chủ máy chịu trách nhiệm giữ máy như server: worker chỉ nghe trên IP Tailscale (không
  `0.0.0.0` trên wifi quán cà phê), có `SPEECH_API_KEY`, máy có mật khẩu.
- Server vẫn phải là máy 4 GB và giữ `COMPOSE_PROFILES=speech`: worker dự phòng chạy sẵn, chờ việc. Lúc rảnh nó dùng
  khoảng 700 MB RAM và gần như không dùng CPU.
- Mac đang chạy dở một bản ghi dài mà ngủ thì bản ghi chạy lại từ đầu trên server: chậm hơn, nhưng có kết quả thay vì báo
  lỗi.
- Câu nói theo micro đầu tiên sau khi Mac tắt chờ thêm tối đa 5 giây; các câu trong 2 phút sau đó đi thẳng sang server.
- Bản gỡ băng không ghi máy nào đã chạy; hai máy chạy cùng model nên kết quả cùng loại.
- Cách chạy worker trên Mac và các dòng `deploy/.env`: `deploy/README.md` §10.

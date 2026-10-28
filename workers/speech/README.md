# Worker nhận dạng giọng nói (`workers/speech`)

Dịch vụ HTTP nhỏ, viết bằng Python, chạy trên **CPU** bằng [sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx).
Backend TrustCabinet gọi nó để làm ba việc:

1. **Chuyển giọng nói tiếng Việt thành chữ** (SCRUM-31).
2. **Tách người nói** (*speaker diarization*): chia bản ghi thành "Người nói 1, 2, 3…" (SCRUM-35).
3. **Tạo vector giọng** (*voice embedding*) cho Voice ID: từ mẫu giọng một người tự ghi, và từ
   từng người nói trong bản ghi, để backend so khớp (SCRUM-35).

Worker **không lưu gì**. Bản ghi gửi lên được ghi ra một file tạm và xoá ngay khi request kết thúc;
chữ, vector giọng và kết quả chỉ trả về cho backend, không ghi ra đĩa, không ghi vào log. Việc
lưu trữ, đồng ý và xoá dữ liệu cá nhân (Luật Bảo vệ dữ liệu cá nhân 91/2025/QH15) do backend lo.

**Âm thanh không rời khỏi máy chủ của nhóm.** Không gọi dịch vụ AI bên ngoài nào. Model được tải và
kiểm tra mã băm lúc build image, nên container chạy không cần internet.

## Model

| Vai trò | Model | License | Nguồn |
|---|---|---|---|
| Chuyển giọng nói thành chữ | Gipformer 1.5 — RNN-T Zipformer 68M tham số, int8 | MIT | [g-group-ai-lab/gipformer1.5-68M-rnnt](https://huggingface.co/g-group-ai-lab/gipformer1.5-68M-rnnt) |
| Vector giọng (512 chiều) — dùng cho cả tách người nói lẫn Voice ID | 3D-Speaker ERes2Net base, huấn luyện trên 200k người nói | Apache-2.0 | bản ONNX của k2-fsa |
| Lọc khoảng lặng (VAD) | Silero VAD | MIT | bản ONNX của k2-fsa |
| Dấu câu và chữ hoa | viBERT-capu — viBERT (FPT AI) tinh chỉnh bởi dragonSwing, int8 | CC BY-SA 4.0 (ghi công dragonSwing và FPT AI) | [dragonSwing/vibert-capu](https://huggingface.co/dragonSwing/vibert-capu), bản ONNX [welcomyou/vibert-capu-onnx](https://huggingface.co/welcomyou/vibert-capu-onnx) |

Danh sách file, mã SHA-256 và URL nằm trong [models.manifest](models.manifest). Server kiểm tra lại
mã băm mỗi lần khởi động; file bị thay hay thiếu thì **từ chối chạy**. Mỗi kết quả kèm mã model dạng
`gipformer-1.5-68m-int8@b528768939c7` (12 ký tự đầu của SHA-256), để biết transcript nào làm bằng
model nào.

## Chạy cục bộ

**Cách 1: Docker** (giống server).

```bash
docker compose --profile speech up -d --build   # lần đầu tải ~230 MB model khi build, image ~950 MB
curl http://127.0.0.1:8090/health
```

**Cách 2: venv**, tiện khi sửa code worker.

```bash
cd workers/speech
uv venv -p 3.12 .venv && uv pip install -p .venv -r requirements.txt
.venv/bin/python fetch_models.py          # tải model vào ~/.cache/trustcabinet-speech/models
.venv/bin/python -m speech                # nghe ở 127.0.0.1:8090
```

Cần `ffmpeg` trong `PATH` (`brew install ffmpeg`) để giải mã mp3, m4a, webm.

### Biến môi trường

| Biến | Mặc định | Ý nghĩa |
|---|---|---|
| `SPEECH_HOST` | `127.0.0.1` (image: `0.0.0.0`) | Địa chỉ lắng nghe |
| `SPEECH_PORT` | `8090` | Cổng |
| `SPEECH_MODELS_DIR` | `~/.cache/trustcabinet-speech/models` (image: `/models`) | Thư mục model |
| `SPEECH_API_KEY` | trống | Nếu đặt, mọi route trừ `/health` phải có `Authorization: Bearer <key>` |
| `SPEECH_THREADS` | `min(4, số CPU)` | Số luồng cho mỗi model |
| `SPEECH_CLUSTER_THRESHOLD` | `0.5` | Ngưỡng gộp giọng khi tách người nói, xem [Hiệu chỉnh](#hiệu-chỉnh) |
| `SPEECH_MIN_CONSISTENCY` | `0.40` | Ngưỡng "chỉ một người nói" của mẫu giọng, xem [Hiệu chỉnh](#hiệu-chỉnh) |
| `SPEECH_PUNCTUATION` | `1` | `0` tắt bước thêm dấu câu (để so sánh); model vẫn phải có và đúng mã băm |
| `SPEECH_LOG_LEVEL` | `INFO` | Mức log |

## HTTP API

JSON dùng camelCase. Thân request là **byte âm thanh thô**, định dạng nào `ffmpeg` đọc được cũng
được (wav, mp3, m4a, webm/opus, ogg). Thân tối đa 512 MiB.

### `GET /health`

Không cần xác thực.

```json
{"status":"ok","models":{"asr":"gipformer-1.5-68m-int8@b528768939c7","embedding":"eres2net-base-200k@e2d2048292e0","vad":"silero-vad@9e2449e10874"}}
```

### `POST /v1/transcribe?diarize=true|false[&speakers=N]`

- `diarize` mặc định `true`. Với `false`, worker chỉ cắt theo khoảng lặng, không tách người nói,
  `speaker` là `null` và `speakers` rỗng. Dùng cho bản ghi micro của riêng một người, khi đã biết
  ai nói.
- `speakers=N` (1–20): biết trước số người nói thì báo, tách sẽ chính xác hơn. Bỏ trống thì
  worker tự đoán.

```json
{
  "durationMs": 23382, "language": "vi",
  "asrModel": "gipformer-1.5-68m-int8@b528768939c7",
  "embeddingModel": "eres2net-base-200k@e2d2048292e0",
  "segments": [
    {"startMs": 31, "endMs": 5802, "speaker": "SPEAKER_00", "text": "Kính thưa các đồng chí hôm nay chúng ta họp để thông qua kế hoạch ngân sách quý bốn."}
  ],
  "speakers": [
    {"label": "SPEAKER_00", "speechMs": 11846, "embedding": [-0.051461, 0.011607, "… 512 số, độ dài 1"]}
  ]
}
```

- Nhãn `SPEAKER_00`, `SPEAKER_01`… đánh số theo thứ tự xuất hiện. Nhãn chỉ có nghĩa **trong một
  bản ghi**. Việc nhãn nào là ai do backend so với giọng mẫu, rồi thư ký xác nhận.
- Mỗi đoạn là một quãng nói liền, dài tối đa 25 giây, cắt tại chỗ ngắt hơi. Một người nói lâu thì ra
  nhiều đoạn liên tiếp cùng nhãn.
- Vector của mỗi người nói lấy từ các lượt nói dài nhất của người đó, tối đa 30 giây.
- Bản ghi im lặng trả về `segments` rỗng.
- Chữ từ model là CHỮ HOA không dấu câu. Worker thêm dấu phẩy, dấu chấm, dấu hỏi và viết hoa đầu câu
  bằng viBERT-capu (bước 6 ở [Cách chạy bên trong](#cách-chạy-bên-trong)). Một đoạn nối tiếp câu của
  đoạn trước thì bắt đầu bằng chữ thường. Tên riêng model chỉ viết hoa được một phần; còn lại thư ký
  sửa tay.

### `POST /v1/embed`

Mẫu giọng để đăng ký Voice ID: một người đọc một đoạn văn, dài tối đa 60 giây.

`200`:

```json
{"embedding":[-0.057129, 0.017537, "… 512 số"],"model":"eres2net-base-200k@e2d2048292e0","speechMs":9658,"rmsDbfs":-17.3,"clippedRatio":0.0,"consistency":0.823}
```

`422`: mẫu không đạt. `error` cho biết lý do, kèm các số đo được, chỉ số nào chưa đo tới thì là `null`:

| `error` | Nghĩa | Điều kiện |
|---|---|---|
| `too_long` | Dài quá | > 60 giây âm thanh |
| `too_short` | Nói ít quá | < 6 giây có tiếng nói (theo VAD) |
| `too_quiet` | Nhỏ quá | Âm lượng phần nói < −40 dBFS |
| `clipped` | Bị rè, vỡ tiếng | > 1% mẫu phần nói chạm ngưỡng ±0.99 |
| `multiple_speakers` | Có giọng người khác | Độ nhất quán < `SPEECH_MIN_CONSISTENCY` |

### Lỗi chung

| HTTP | `error` | Khi nào |
|---|---|---|
| 400 | `unreadable_audio` | `ffmpeg` không đọc được file |
| 400 | `invalid_parameter` | Tham số query sai |
| 401 | `unauthorized` | Đã đặt `SPEECH_API_KEY` nhưng thiếu hoặc sai bearer |
| 404 / 405 | `not_found` / `method_not_allowed` | Sai route hoặc sai method |
| 411 | `length_required` | Không có `Content-Length` và không dùng chunked |
| 413 | `too_large` | Thân request > 512 MiB |
| 500 | `internal` | Lỗi trong worker. Chi tiết chỉ nằm trong log |

## Cách chạy bên trong

Các bước xử lý một bản ghi:

1. `ffmpeg` giải mã về 16 kHz mono.
2. **Tăng âm trượt**: mỗi lúc, âm lượng của 1.5 giây xung quanh được kéo về khoảng −20 dBFS. Mức
   tăng tối đa 30 dB, mức giảm tối đa 12 dB. Chỗ nhỏ hơn −60 dBFS coi là im lặng nên không được
   tăng. Nhờ vậy người ngồi xa máy ghi hoặc nói qua loa ngoài không bị bước sau bỏ qua.
3. **VAD** tìm các quãng có tiếng nói. Ngưỡng để thấp (0.2) và nới mỗi quãng 0.1 giây hai bên:
   ở ngưỡng 0.5, VAD bắt đầu câu trễ và làm mất chữ đầu câu; ở 0.3, nó bỏ gần hết lời của người
   nói nhỏ.
4. **Tách người nói**: cắt mỗi quãng thành các khung 3 giây, chồng nhau 1.5 giây. Mỗi khung
   được tạo một vector giọng, rồi các khung được gom nhóm bằng FastClustering của sherpa-onnx.
   - Nhóm nào có dưới 2 giây tiếng (ho, cười, tiếng ồn) được nhập vào người gần nhất.
   - Một khung lẻ khác nhãn với cả hai khung bên cạnh thì lấy theo nhãn của chúng.
   - Các khung liền nhau cùng nhãn ghép thành một lượt nói.
   - **Gộp lần hai**: mỗi nhóm được tạo một vector giọng tổng thể (tối đa 30 giây âm thanh của
     nhóm). Hai nhóm có cosine từ 0.85 trở lên là cùng một người bị tách đôi, nên được gộp lại,
     rồi các lượt liền nhau của cùng người được nối, để câu không bị cắt giữa chừng.
5. **Chuyển chữ** từng lượt nói bằng Gipformer. Lượt dài hơn 25 giây được chia thành các phần
   bằng nhau.
6. **Dấu câu và chữ hoa** bằng viBERT-capu. Model gắn cho mỗi từ một thao tác (giữ nguyên, viết hoa,
   thêm `,` `.` `:` `?`) và đọc lại tối đa ba lần. Các đoạn liền nhau của cùng một người, cách nhau
   không quá 5 giây, được đọc chung thành một đoạn văn, nên chỗ ngắt hơi giữa câu không thành dấu
   chấm. Model chỉ sửa từ tại chỗ, không thêm hay bớt từ, nên mốc giờ của từng đoạn giữ nguyên;
   lỡ số từ đổi thì đoạn đó quay về cách cũ (viết hoa chữ đầu, dấu chấm cuối).
7. **Vector cho mỗi người nói**, để backend so với giọng mẫu.

Không dùng bộ tách người nói có sẵn của sherpa-onnx (pyannote + embedding cửa sổ 10 giây). Đo trên
cùng máy, bộ đó tốn RTF 0.3–0.5 khi chạy trực tiếp và 1.34 trong Docker. Cách trên chỉ tốn khoảng
0.06 khi chạy trực tiếp.

- Model nằm trong **một process con**, mỗi lần chạy một việc. Tách người nói giữ GIL của Python
  trong lúc chạy, nên nếu để chung process thì cả `/health` cũng bị treo. Process HTTP chỉ chuyển
  đường dẫn file tạm và kết quả JSON qua lại. Process con chết (ví dụ hết RAM) thì request đó
  nhận `500`, request sau chạy trên process con mới.
- Request thứ hai đến khi đang bận sẽ **xếp hàng**, không bị từ chối.

## Kiểm thử

```bash
python -m unittest discover -s workers/speech/tests   # không cần model, chỉ cần numpy
```

CI chạy lệnh này (job *Speech worker — unit tests*) mỗi khi PR đổi `workers/speech/`.

Kiểm tra đầu cuối với model thật, chạy trên máy dev, CI không có model:

```bash
cd workers/speech && .venv/bin/python selfcheck.py <thư mục audio>
```

Thư mục cần `enrolA.wav` và `enrolB.wav` (mỗi file một giọng, 8–30 giây) và `meeting.wav` (hai giọng
đó nói xen kẽ). Selfcheck kiểm tra:

- tách ra đúng 2 người nói;
- mỗi người khớp đúng một mẫu giọng;
- mẫu ghép hai giọng bị từ chối là `multiple_speakers`.

Đo chất lượng trên giọng thật (VIVOS, giọng của nhóm, một cuộc họp có bản gỡ băng): xem
[eval/](eval/README.md). Các số trong mục dưới đều đo bằng các script đó.

## Hiệu chỉnh

Độ nhất quán của mẫu giọng được tính như sau:

1. Cắt phần có tiếng nói thành các khung khoảng 2 giây, tối đa 6 khung, rải đều.
2. Tạo vector giọng cho từng khung.
3. Lấy cosine **thấp nhất** giữa hai khung bất kỳ.

Đo trên giọng thật của tập test VIVOS (19 người) và trên giọng tổng hợp (macOS `say`). Mỗi mẫu
thật ghép 4 câu, khoảng 15 giây ([eval/](eval/README.md)):

| Loại mẫu | Số mẫu | Thấp nhất | Trung vị | Cao nhất | Lọt qua ở 0.40 | Lọt qua ở 0.60 |
|---|---|---|---|---|---|---|
| Thật, một người | 94 | 0.462 | 0.623 | 0.741 | **100%** | 62% |
| Thật, hai người mỗi người một nửa | 50 | −0.124 | 0.123 | 0.468 | **2%** | 0% |
| Thật, người thứ hai chen 1 câu (~25%) | 50 | −0.035 | 0.135 | 0.693 | 8% | 8% |
| Tổng hợp, một giọng | 9 | 0.732 | 0.798 | 0.857 | 100% | 100% |
| Tổng hợp, hai giọng | 9 | 0.265 | 0.361 | 0.584 | 44% | 0% |

Ngưỡng ban đầu **0.60** được chỉnh trên giọng tổng hợp. Trên giọng thật, nó từ chối 38% mẫu của
người nói một mình: 5/19 người VIVOS không đăng ký được. Ngưỡng **0.40** giữ đủ mẫu thật, và chỉ một
trong 50 mẫu hai người lọt qua.

Trường hợp người thứ hai chỉ chen một câu thì lọt 8% ở mọi ngưỡng. Lý do là 6 khung đo không phải
lúc nào cũng rơi trúng câu chen. Một câu lẫn như vậy không đổi được vector của cả mẫu: vector vẫn là
của người chủ. Giọng mẫu ghi qua màn "Giọng nói của tôi" đọc một đoạn 20–30 giây, dài hơn mẫu
VIVOS, nên điểm sẽ cao hơn. Nếu mẫu thật của nhóm vẫn bị từ chối nhiều, chỉnh
`SPEECH_MIN_CONSISTENCY` mà không phải sửa code.

**Voice ID trên giọng thật.** Đo trên 24 cuộc họp giả lập từ tập test VIVOS (19 người, 33 phút mỗi bộ,
[eval/](eval/README.md)). Bộ "khác kênh" cho cuộc họp đi qua tiếng vọng phòng, tiếng ồn và nén mp3,
còn giọng mẫu để sạch. Gợi ý theo luật của backend (`SpeakerMatcher`, khoảng cách 0.1):

| Bộ | WER | Gán đúng người | Ngưỡng gợi ý | Lời người đã đăng ký được gọi đúng tên | Gọi sai tên | Người lạ bị gán tên |
|---|---|---|---|---|---|---|
| Sạch | 5.0% | 99.8% | 0.5 | 97.3% | 3.3% | 15.6% |
| Sạch | | | **0.6** | **97.3%** | **0.1%** | **0%** |
| Khác kênh | 6.0% | 99.9% | 0.5 | 98.4% | 3.1% | 15.1% |
| Khác kênh | | | **0.6** | **98.4%** | **0%** | **0%** |
| Khác kênh | | | 0.65 | 90.2% | 0% | 0% |
| Khác kênh | | | 0.7 | 71.0% | 0% | 0% |

"Người lạ" là người nói nhưng không có trong danh sách được gợi ý. Ở 0.5 thì người lạ bị gọi bằng tên
người khác; ở 0.6 thì không, và cũng không bỏ sót thêm ai. Từ 0.65 trở lên, bộ "khác kênh" bắt đầu bỏ
sót người. Vì vậy backend mặc định dùng `VoiceId:SuggestThreshold = 0.6`.

**Tách người nói và chuyển chữ** được đo trên hai loại âm thanh:

- 20 đoạn tổng hợp: 9 đoạn một giọng, 9 đoạn hai giọng, `meeting.wav` và 1 đoạn bốn giọng. Một
  đoạn tính là đúng khi số nhóm có từ 2 giây tiếng trở lên bằng đúng số người nói.
- Một bản ghi cuộc họp thật 5 phút, 4 người, thu bằng một máy (mp3 16 kHz), kèm bản gỡ băng có
  mốc giờ và nhãn người nói dùng làm đáp án. Hai người ngồi xa máy ghi, nói nhỏ hơn người chủ trì
  15–20 dB. Bản ghi không nằm trong repo vì là dữ liệu cá nhân.

| Bước | Đoạn tổng hợp đúng | Cuộc họp thật: WER | Lời của hai người nói nhỏ được chuyển chữ | Số nhãn cho 4 người |
|---|---|---|---|---|
| Khung 1.5 giây, ngưỡng 0.5, VAD 0.3 (bản đầu) | 20/20 | — | — | 22 |
| Khung 3 giây, gộp lần hai ở 0.85 | 17/20 | 34.9% | 10–12% | 4, nhưng chỉ nhận ra 2 người thật |
| **Thêm tăng âm trượt, VAD 0.2** | **17/20** | **16.1%** | **84–91%** | **6, nhận ra cả 4 người** |

- WER tính trên chữ thường, bỏ dấu câu và các tiếng đệm "ờ, ừ, ừm, à, ạ". Chữ số trong đáp án
  được đọc thành chữ.
- Với cấu hình cuối, phần thời gian được gán đúng người (theo đáp án) là 68%. Gần hết phần sai là
  một đoạn liền 63 giây ở cuối: đáp án ghi là người chủ trì, còn worker gán cho người phụ trách
  pháp lý.
- Tăng âm trượt không biến tiếng ồn thành chữ: tệp im lặng và tiếng ồn trắng ở −50 dBFS và
  −35 dBFS đều ra 0 đoạn. `meeting.wav` trộn ồn ở SNR 15 dB vẫn ra đúng 2 người, 4 câu.

Bản đầu chỉ chỉnh trên giọng tổng hợp, và giọng thật không giống vậy. Trong cuộc họp thật mọi
người nói cùng một phòng, qua cùng một micro, nên giọng người này với người kia gần nhau hơn hẳn.
Với khung 1.5 giây, cosine giữa hai người khác nhau có trung vị 0.53, so với 0.32 ở giọng tổng
hợp. Khung 3 giây cho vector ổn định hơn: hai khung liền nhau của cùng một người đạt cosine 0.89.

Ở bước gộp lần hai, hai nhóm lớn của cùng một người có cosine 0.94. Với các nhóm ngắn (4–10 giây)
thì vector không còn tin được: hai nhóm cùng một người chỉ đạt 0.36–0.81, trong khi hai người
khác nhau có cặp tới 0.73. Ngưỡng 0.85 vì vậy chỉ gộp khi gần như chắc chắn. Tách thừa một người
thành hai nhãn ít hại hơn gộp nhầm hai người: thư ký gán được cả hai nhãn cho cùng một người.

Ba đoạn tổng hợp còn sai đều là các cặp giọng nữ tiếng Anh của macOS (Samantha, Karen, Moira),
gần như giống hệt nhau.

## Hiệu năng

Đo trên MacBook M1 (16 GB), 4 luồng:

| Việc | Âm thanh | Chạy trực tiếp (venv) | Trong Docker (Linux arm64) |
|---|---|---|---|
| Chuyển chữ + tách người nói | 23 s wav, tổng hợp | 2.1 s (RTF 0.09) | — |
| Chuyển chữ + tách người nói | 94 s m4a | 9.3 s (RTF 0.10) | — |
| Chuyển chữ + tách người nói | 300 s mp3, cuộc họp thật | 23 s (RTF 0.08) | 78 s (RTF 0.26) |
| Chuyển chữ + tách người nói | 608 s mp3 | 43 s (RTF 0.07) | 132 s (RTF 0.22), RAM đỉnh 1.5 GB |
| Chỉ chuyển chữ | 23 s | 0.5 s (RTF 0.02) | — |
| Vector mẫu giọng | ~10 s | ~0.4 s | — |
| Riêng bước dấu câu | 880 từ | 1.3 s (~680 từ/giây) | — |

RTF là thời gian xử lý chia cho độ dài âm thanh. Thời gian tăng tuyến tính theo độ dài. Bước dấu câu
thêm khoảng 2% thời gian (cuộc họp thật 300 s: 20.7 s → 21.2 s) và khoảng 270 MB RAM, WER không đổi
(16.1%) vì model không đổi từ nào. Với RTF
0.25, một giờ họp mất khoảng 15 phút. Container lúc rảnh dùng khoảng 260 MB RAM. Process con giữ
lại bộ nhớ đã dùng, nên sau vài bản ghi 10 phút liên tiếp, container giữ khoảng 2.4 GB. Docker ở
đây chạy trong máy ảo Linux của Docker Desktop (8 CPU), chậm hơn chạy trực tiếp khoảng 3 lần.

Chưa đo trên máy chủ x86.

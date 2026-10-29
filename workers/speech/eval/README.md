# Đo chất lượng worker giọng nói (`workers/speech/eval`)

Bộ công cụ đo chuyển chữ, tách người nói và Voice ID trên một worker đang chạy. Có ba nguồn âm
thanh:

1. Một corpus công khai (VIVOS).
2. Giọng các bạn trong nhóm tự ghi.
3. Một cuộc họp thật đã có bản gỡ băng.

Số liệu trong [README của worker](../README.md#hiệu-chỉnh) và trong báo cáo cho hội đồng đều đo bằng
các script này.

**Âm thanh và bản gỡ băng là dữ liệu cá nhân, không commit vào repo**, kể cả khi là giọng của chính
nhóm. Để chúng trong một thư mục ngoài repo.

| Script | Việc | Cần |
|---|---|---|
| `evaluate.py` | Gọi worker với một bộ dữ liệu có đáp án. Báo WER, độ đúng của tách người nói, và độ đúng của gợi ý tên theo từng ngưỡng | Python 3 chuẩn |
| `vivos_dataset.py` | Dựng bộ dữ liệu từ VIVOS: giọng mẫu, các cuộc họp giả lập, có biến thể "khác kênh" | numpy, ffmpeg |
| `make_meeting.py` | Dựng một cuộc họp có đáp án từ các câu mỗi người tự ghi riêng | numpy, ffmpeg |
| `score_transcript.py` | Chấm một bản ghi thật theo bản gỡ băng có nhãn `[mm:ss] Người nói N: …` | Python 3 chuẩn |
| `bleed.py` | Cuộc họp tổng hợp theo micro từng người, mỗi micro nghe lọt người khác; đo từ trùng trước và sau khi đánh dấu (`fold.py`, bản Python của `SpeechClipFold`) | numpy, Node 22.18+ |

Chạy worker trước (xem [README của worker](../README.md#chạy-cục-bộ)). Các script nhận `--url`, mặc
định `http://127.0.0.1:8090`. Script cần numpy thì chạy bằng venv của worker
(`../.venv/bin/python -I …`).

## 1. VIVOS

VIVOS là corpus tiếng Việt của AILAB, ĐH KHTN TP.HCM: 15 giờ đọc, giấy phép CC BY-NC-SA 4.0, chỉ
dùng cho học thuật. Tải bản chính thức ở [Zenodo](https://zenodo.org/records/7068130)
(`vivos.tar.gz`, 1.4 GB). Chỉ cần tập `test`: 19 người (7 nữ, 12 nam), 760 câu, 45 phút. Tập này
nằm ở đầu file nén, nên chỉ cần tải phần đầu là đủ để giải nén.

```bash
tar xzf vivos.tar.gz vivos/test
../.venv/bin/python -I vivos_dataset.py vivos/test /tmp/vivos-clean --meetings 12 --seed 1
../.venv/bin/python -I vivos_dataset.py vivos/test /tmp/vivos-room  --meetings 12 --seed 1 --channel room
python3 -I evaluate.py /tmp/vivos-room --cache /tmp/eval-cache \
  --threshold 0.4 0.5 0.6 0.7 --margin 0.05 0.1 --voice-margin 0 0.1 0.2
```

Cách dựng bộ dữ liệu:

- Mỗi người có hai giọng mẫu, mỗi mẫu ghép 3–4 câu. Các câu dùng làm mẫu không xuất hiện lại
  trong cuộc họp.
- Mỗi cuộc họp có 4–6 người nói xen kẽ, mỗi lượt 1–3 câu.
- Một người nói nhưng không có trong danh sách được gợi ý ("người lạ"), đại diện cho người chưa
  đồng ý Voice ID. Tên người này không bao giờ được gợi ý.
- Một người có trong danh sách và đã đăng ký giọng, nhưng không nói câu nào ("người vắng").
  Không giọng nào được gợi ý thành người này.

`--channel room` cho cuộc họp đi qua tiếng vọng phòng, lọc dải tần, ồn 20 dB và nén mp3 32 kbps.
Giọng mẫu vẫn để sạch. Đây là tình huống thật: mỗi người đăng ký giọng trên laptop của mình, còn
cuộc họp được ghi bằng một máy đặt giữa bàn.

`--cache` giữ kết quả của worker theo mã băm âm thanh, để thử ngưỡng khác không phải chạy lại.
**Đổi code worker thì xoá thư mục cache.**

## 2. Giọng của nhóm

Mỗi người tự ghi, mỗi câu một file, ghi bằng laptop hoặc điện thoại:

- **2 lần đọc đoạn văn mẫu** (lấy từ màn "Giọng nói của tôi"), mỗi lần 20–30 giây. Đặt vào
  `<dataset>/enrol/<tên>/1.wav` và `2.wav`.
- **Khoảng 10 câu nói tự nhiên**, như khi phát biểu trong cuộc họp, mỗi câu 3–10 giây. Đặt vào
  `<clips>/<tên>/<n>.wav`. Có thể kèm `<n>.txt` ghi đúng câu đã nói để tính WER.

Rồi ghép thành các cuộc họp và đo:

```bash
../.venv/bin/python -I make_meeting.py <clips> <dataset>/meetings/m01 --people lan minh hoa --seed 1
../.venv/bin/python -I make_meeting.py <clips> <dataset>/meetings/m02 --people lan minh hoa --roster lan minh --seed 2 --noise 20
python3 -I evaluate.py <dataset> --threshold 0.4 0.5 0.6 --margin 0.1 --voice-margin 0 0.1
```

Ở lệnh thứ hai, `--roster lan minh` để `hoa` nói mà không có trong danh sách được gợi ý, tức là
"người lạ".

## 3. Một cuộc họp thật đã có bản gỡ băng

Đây là bài kiểm tra sát thực tế nhất: một máy ghi chung cho cả phòng, có người ngồi xa micro, có
người chen ngang.

```bash
textutil -convert txt bien-ban.docx        # macOS; hoặc lưu .docx thành .txt
python3 -I score_transcript.py hop.mp3 bien-ban.txt
```

Script báo các chỉ số sau:

- WER.
- Phần lời của từng người được chuyển chữ. Nếu chỉ số này thấp, VAD đã bỏ sót người đó.
- Độ đúng của tách người nói.
- Bảng nhầm lẫn giữa các người nói.
- Từng đoạn worker gán khác bản gỡ băng.

Nếu bản gỡ băng do một công cụ AI khác tạo ra, các đoạn "khác" có thể là lỗi của bản gỡ băng.
Hãy nghe lại trước khi kết luận.

## 4. Tiếng lọt giữa các micro riêng

```bash
../.venv/bin/python -I bleed.py vivos/test --cache /tmp/bleed --profile room --profile clip           # bộ chính, seed 0–4
../.venv/bin/python -I bleed.py vivos/test --cache /tmp/bleed --profile clip --seeds 300 301 302 303 304  # bộ kiểm chứng
../.venv/bin/python -I bleed.py vivos/test --cache /tmp/bleed --profile clip --own-dbfs -38           # người nói nhỏ
```

Ba kịch bản: S1 từng người nói lần lượt, S3 lượt sau chen vào lượt trước 1–4 giây, S4 đáp nhanh dưới
1,2 giây (ngắt câu của trình duyệt). Mỗi micro được cắt câu bằng `cut_clips.mjs`, tức chính
`frontend/src/features/speech/clipCutter.ts`. Chỉ số theo từng từ chuẩn: **từ trùng** (bản trên micro
người khác chưa bị đánh dấu), **lạc chỗ** (như trên nhưng micro chủ không có từ đó), **mất lời thật**
(lời chủ micro bị đánh dấu), **recall** (lời chủ micro có trên micro của họ). Chọn ngưỡng trên seed
100–104, báo cáo trên seed 0–4, kiểm chứng trên seed 300–304.

`fold.py` phải đánh dấu đúng như backend: `tests/test_fold_port.py` chạy chung các ca trong
`tests/TrustCabinet.Web.Tests/Recordings/speech-fold-cases.json` với `SpeechClipFoldTests`.

## Đọc kết quả của `evaluate.py`

| Chỉ số | Ý nghĩa | Muốn |
|---|---|---|
| named right | Phần thời gian nói được gợi ý đúng tên | Cao |
| named wrong | Phần thời gian nói bị gợi ý **sai** tên. Đây là lỗi đắt nhất: thư ký có thể bấm "Đúng" theo thói quen | Gần 0 |
| stranger named | Phần lời của "người lạ" bị gán một cái tên bất kỳ | 0 |
| absent named | Số cuộc họp mà "người vắng" vẫn được gợi ý | 0 |
| precision | Số giọng được gợi ý đúng người trên tổng số giọng được gợi ý | Cao |
| speaker accuracy | Phần thời gian nói mà nhãn của worker ứng đúng người, không tính tên | Cao |

Ngưỡng được chọn theo thứ tự sau:

1. Giữ named wrong, stranger named và absent named bằng 0 trên bộ "khác kênh".
2. Trong các ngưỡng thoả bước 1, chọn ngưỡng có named right cao nhất.

Gợi ý thiếu thì thư ký gán tay. Gợi ý sai thì thành sai biên bản.

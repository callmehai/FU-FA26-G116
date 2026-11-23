# Đo độ chính xác dự thảo AI trên cuộc họp thật

Bộ công cụ so độ chính xác của dự thảo biên bản do AI viết (SCRUM-36) giữa máy local (`qwen2.5:7b-instruct`) và API
(`deepseek-v4.1-flash`) trên các cuộc họp thật của nhóm, theo cách hội đồng chấp nhận được: có căn cứ đúng sai độc lập,
chấm mù, hai người chấm độc lập và đo mức đồng thuận. Bộ đo trên cuộc họp mẫu ở [`../README.md`](../README.md) vẫn
dùng khi đổi prompt; bộ này trả lời câu "trên họp thật thì AI viết đúng bao nhiêu".

**Repo không chứa dữ liệu cuộc họp nào.** Bản gỡ băng là dữ liệu cá nhân (Luật 91/2025/QH15) và repo công khai: mọi
file xuất, dự thảo, sổ chấm và kết quả nằm trong `tests/ai/out/real/`, đã bị `.gitignore` chặn (kiểm bằng
`git check-ignore tests/ai/out/real/x`). Không dán lời thoại vào PR, commit hay kênh chat; chỉ trích số đếm.

## Phương pháp

| Phần | Cách làm |
|---|---|
| Căn cứ đúng sai | Dữ liệu hệ thống (chương trình, giờ, thành phần và điểm danh, biểu quyết và kết luận, tài liệu) và bản ghi âm. Bản gỡ băng cũng có thể sai, nên mỗi lỗi ghi rõ nguồn: **Gỡ băng** (AI chép đúng một câu nhận dạng sai) hay **AI** |
| Ý chuẩn | BA nghe bản ghi và ghi danh sách ý chính của từng cuộc họp **trước khi xem dự thảo** (file Excel mẫu, mỗi cuộc họp một sheet: `# \| Ý chính \| Loại \| Người nói \| Mốc giờ \| Mức quan trọng \| Ghi chú`, mức Chính/Phụ) |
| Dự thảo | Mỗi nhà cung cấp viết 3 bản mỗi cuộc họp, cùng một bản gỡ băng đã đóng băng, qua chính API của app |
| Chấm mù | Tên nhà cung cấp thay bằng A/B, xáo riêng từng cuộc họp; khoá giữ trong `key.json`, không đưa người chấm |
| Hai người chấm | Lộc và Khánh chấm độc lập từng ý: Đúng / Sai lệch / Bịa / Không kiểm được, kèm bằng chứng (mốc giờ bản ghi hoặc bản ghi hệ thống), nguồn lỗi và sai tên/số; đánh dấu dự thảo có nêu từng ý chuẩn không (Có / Một phần / Không) |

| Chỉ số | Công thức |
|---|---|
| Độ chính xác | Đúng / (tổng ý − Không kiểm được), kèm khoảng tin cậy 95% Wilson |
| Tỉ lệ bịa | Bịa / tổng ý đã gán nhãn |
| Độ phủ ý chuẩn | (Có + 0,5 × Một phần) / số ý chuẩn; tính thêm bản chỉ đếm Có, và bản chỉ cho ý Chính |
| Sai tên, sai số | Số ý người chấm đánh dấu Tên / Số |
| Nguồn lỗi | Số lỗi (Sai lệch + Bịa) do gỡ băng, do AI |
| Thời gian, chi phí | Giây mỗi bản (đo ở trình duyệt, từ lúc gửi đến lúc thấy Ready); USD mỗi bản lấy từ `costUsd` trong file meta, nếu không có thì ước tính theo số ký tự và `--price` |
| Đồng thuận | Tỉ lệ đồng ý và Cohen's kappa giữa hai người chấm, cho nhãn, nguồn lỗi và độ phủ |
| Dao động | Trung bình ± độ lệch chuẩn của các chỉ số trên 3 lần chạy |

## Chạy

Cần `uv` (chạy Python với openpyxl: `uv run --with openpyxl …`) và quyền SSH vào server app.

### 1. Xuất dữ liệu và đóng băng bản gỡ băng

```bash
python3 tests/ai/real/export.py \
  01a1fc63-0818-7007-9e26-8532a29914d7:test-mic \
  01a1fce5-d8f0-7293-97f6-4ead9928272b:thu-ho \
  01a2021e-e2cc-7153-92e2-c2ed5d986a87:hop-voi-thay
```

Mỗi cuộc họp ra `tests/ai/out/real/<slug>.json` (dữ liệu hệ thống + bản gỡ băng, mỗi dòng có giờ thực tính như file
.txt của app: giờ bắt đầu lượt ghi + độ lệch, và mốc trong bản ghi để tua), `<slug>.txt` và bản dự thảo đang lưu trong
`stored/`. Script chỉ chạy `SELECT` trong phiên `default_transaction_read_only`, qua
`ssh … docker exec <container> psql`, dùng tài khoản database có sẵn trong container nên máy mình không cần biết. Cuộc họp có bản gỡ băng chưa xong
(đang nhận dạng lại) bị bỏ qua, chạy lại sau.

Dòng lời thoại giống hệt những gì AI đọc (`MeetingTranscriptReader`): câu của mic riêng, đoạn của mic phòng/file tải
lên đã gỡ băng xong, bỏ câu đã loại và tiếng vọng của người họp online trong phòng hybrid (ADR-0061). Khác một điểm: app
đổi số đọc thành chữ số trước khi gửi AI ("năm phút" → "5 phút"), file xuất giữ nguyên chữ.

### 2. BA điền ý chuẩn

BA điền file Excel mẫu (mỗi cuộc họp một sheet, tên bắt đầu bằng ngày `DD-MM`) khi chưa đọc dự thảo nào, rồi để file
vào `tests/ai/out/real/`. `kit.py` tự ghép sheet với cuộc họp theo ngày và chữ trong tên; ghép sai thì thêm
`--sheet thu-ho="21-11 Test thu ho"`.

### 3. Sinh dự thảo

Mở tab app đã đăng nhập (Thư ký/Chủ toạ của các cuộc họp, và quản trị để đổi Local/API), dán
[`generate.js`](generate.js) vào DevTools Console, rồi:

```js
await tcEval.getMode()        // ghi lại chế độ hiện tại
await tcEval.campaign([
  { id: '01a1fc63-0818-7007-9e26-8532a29914d7', slug: 'test-mic' },
  { id: '01a1fce5-d8f0-7293-97f6-4ead9928272b', slug: 'thu-ho' },
], { runs: 3 })               // Local × 3 rồi Api × 3 mỗi cuộc họp, xong trả lại chế độ cũ
```

Trình duyệt tải về `<slug>-local-1.md`, `<slug>-local-1.json`… (Chrome hỏi cho phép tải nhiều file: chọn Cho phép).
Chuyển hết vào `tests/ai/out/real/drafts/`. Từng bước lẻ: `tcEval.setMode('Api')`, `tcEval.draft(id)`,
`tcEval.save(r, 'test-mic-api-1')`.

- Token lấy đúng như app: `POST /api/auth/refresh` bằng cookie có sẵn, trong Web Lock `trustcabinet.refresh`; snippet
  không chứa bí mật nào.
- Đổi chế độ gửi lại địa chỉ và model API đang lưu cùng `apiKey: null` (giữ nguyên key): `PUT /api/admin/ai` lưu đúng
  những gì gửi lên, nên gửi mỗi `{ mode }` sẽ xoá địa chỉ và model API.
- **Mỗi lần sinh thay dự thảo đang lưu của cuộc họp** (kể cả phần Thư ký đã sửa hay đã duyệt). Bản trước đó được
  `export.py` giữ ở `stored/`.
- **Cuộc họp mức Nội bộ hoặc Mật luôn dùng máy local** (ADR-0056), dù chế độ là API: `campaign` bỏ qua phần API của
  cuộc họp đó và báo trong console. Muốn so cả hai trên cuộc họp đó thì cả nhóm phải quyết đổi mức mật về Thường.
- Đừng sửa lời thoại hay gán tên người nói trong lúc chạy: file meta ghi `transcriptChangedAt` và `kit.py` cảnh báo nếu
  các bản không cùng một phiên bản lời thoại.

### 4. Tạo sổ chấm

```bash
uv run --with openpyxl tests/ai/real/kit.py --drafts tests/ai/out/real/drafts --ref tests/ai/out/real/<file BA>.xlsx
```

Ra `tests/ai/out/real/kit/cham-loc.xlsx`, `cham-khanh.xlsx` (giống nhau) và `key.json` (giữ lại, không gửi người
chấm). Mỗi sổ có: sheet hướng dẫn; mỗi cuộc họp một sheet dữ liệu hệ thống và một sheet lời thoại; mỗi bản dự thảo một
sheet ý (`M1-A2` = cuộc họp 1, nhà cung cấp A, lần 2) và một sheet `M1-A2 Ý chuẩn`.

Mỗi dòng là một ý: tách theo gạch đầu dòng và câu, rồi theo `; `, rồi theo `, đồng thời` và ` và ` khi cả hai vế đều có
động từ và từ ba chữ trở lên. Tiêu đề mục không tính là ý. Người chấm được chèn dòng `5a`, `5b` khi một dòng còn hai ý.

- **Tự kiểm**: "Khớp dữ liệu" / "Không khớp: …" cho giờ họp, ngày, tên người (so danh sách tham dự và người nói, báo
  cả sai dấu), số người có mặt, biểu quyết (số phiếu, kết luận), kết luận đã ghi của mục, tên mục, tài liệu. Chỉ là
  gợi ý; trống nghĩa là máy không kiểm được.
- **Lời thoại gần nhất**: 3 dòng có nhiều từ chung nhất (bỏ dấu, tính cả cặp từ, số và tên nặng gấp đôi), kèm giờ,
  mốc bản ghi và người nói.

### 5. Tính kết quả

```bash
# vòng 1: chấm độc lập — giữ nguyên kết quả này, đồng thuận trích từ đây
uv run --with openpyxl tests/ai/real/score.py --kit tests/ai/out/real/kit --drafts tests/ai/out/real/drafts \
  --price api=<USD/1M token vào>,<USD/1M token ra> --out tests/ai/out/real/ket-qua-vong-1
# hai người chấm thống nhất các dòng ở sheet "Bất đồng", sửa sổ của mình, rồi:
uv run --with openpyxl tests/ai/real/score.py --kit tests/ai/out/real/kit --drafts tests/ai/out/real/drafts \
  --out tests/ai/out/real/ket-qua-vong-2
```

`ket-qua.md` có bảng theo nhà cung cấp, theo cuộc họp, từng bản, đồng thuận (kèm mức theo Landis & Koch) và một đoạn
phương pháp để dán vào báo cáo; `ket-qua.xlsx` có thêm danh sách bất đồng. Chi phí ước tính lấy số ký tự lời thoại
và dự thảo chia 3 ký tự/token (`--chars-per-token`); có số thật từ trang usage của nhà cung cấp thì ghi `"costUsd"` vào
file meta của từng bản.

## Kiểm thử

Phần tất định (tách ý, tự kiểm, chọn lời thoại, kappa, chỉ số) có test, chỉ cần thư viện chuẩn:

```bash
python3 -m unittest discover -s tests/ai/real/tests
```

## Giới hạn

- Tách ý theo luật cố định nên có dòng còn gộp hai ý hoặc tách ra mất chủ ngữ; người chấm chèn dòng, không gộp.
- Tự kiểm và lời thoại gần nhất là gợi ý; nhãn cuối cùng dựa trên nghe lại bản ghi.
- Thời gian đo ở trình duyệt nên gồm cả lúc chờ trong hàng đợi, sai số bằng chu kỳ hỏi (2 giây).
- Ba cuộc họp × ba lần chạy là mẫu nhỏ: báo cáo nên nêu khoảng tin cậy và độ lệch giữa các lần, không chỉ một con số.

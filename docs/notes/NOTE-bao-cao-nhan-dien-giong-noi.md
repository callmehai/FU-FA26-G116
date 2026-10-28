# Báo cáo: nhận diện giọng nói theo góp ý hội đồng

> **Ngày 23/10/2026.** Trả lời mục 1, 2 và 3 trong [góp ý hội đồng lần review 1](NOTE-gop-y-hoi-dong-lan-1.md).
> Tài liệu cho giảng viên hướng dẫn và hội đồng. Code nằm trên các branch `feat/scrum-29-recordings`,
> `feat/scrum-31-transcription` và `feat/scrum-35-voice-id`, chưa merge vào `main`.

## Tóm tắt

- **Hệ thống biết ai đang nói.** Mỗi người tự ghi giọng mẫu sau khi đồng ý. Hệ thống tách bản ghi
  thành từng người nói, so với giọng mẫu và gợi ý tên. Thư ký xác nhận bằng một cú bấm. Tên đã xác
  nhận thay cho "Người nói N" trên toàn bộ bản gỡ băng.
- **Đo trên giọng thật, ngưỡng mặc định:**
  - Trên 24 cuộc họp giả lập từ corpus VIVOS (19 người): **97–98%** lời của người đã đăng ký được gợi ý
    đúng tên; **không lần nào** gợi ý sai người, và không lần nào gọi người chưa đăng ký bằng tên
    người khác. Tỉ lệ sai từ (WER) là 5–6%.
  - Trên một cuộc họp thật có hai người ngồi xa micro: WER **16,1%**.
- **Tự chạy hết trên CPU, âm thanh không ra khỏi máy chủ.** Một giờ họp xử lý mất khoảng 4 phút
  (chạy trực tiếp) đến 15 phút (trong Docker), trên một máy 4 nhân. Không phát sinh tiền theo phút,
  và không phải chuyển dữ liệu ra nước ngoài.
- **Đúng Luật Bảo vệ dữ liệu cá nhân 91/2025.** Giọng dùng để nhận ra người là dữ liệu sinh trắc học,
  tức dữ liệu nhạy cảm. Người dùng tự đồng ý và rút đồng ý được. Hệ thống chỉ lưu vector đã mã hoá,
  không lưu âm thanh mẫu. Rút đồng ý là xoá hết trong một thao tác.
- **Chưa làm:** chuyển chữ ngay trong khi họp, tóm tắt bằng AI, và đo trên giọng của chính nhóm.
  Mục 6 có kế hoạch cho từng việc.

## 1. Hội đồng yêu cầu gì

| Ý hội đồng | Trả lời trong báo cáo |
|---|---|
| Hệ thống chưa biết ai phát biểu, biên bản chưa gắn lời với từng người | Mục 2, mục 3 |
| Hướng có AI: Voice ID cho từng người, kèm tách nhiều người nói trong một file | Mục 2 |
| Tính chi phí AI (chuyển chữ, nhận dạng người nói, tóm tắt) để chứng minh khả thi | Mục 4 |
| Lưu trữ và xoá dữ liệu theo quy định về dữ liệu cá nhân | Mục 5 |
| Chuyển thành văn bản ngay trong khi họp | Mục 6 |

## 2. Hệ thống làm gì

```
Thư ký ghi âm trên trình duyệt hoặc tải tệp lên  (SCRUM-29)
        │   xác nhận đã thông báo cho người dự; ghi mục đích; hạn lưu 180 ngày
        ▼
Worker giọng nói trong mạng nội bộ  (SCRUM-31)
        │   tăng âm chỗ nói nhỏ → lọc khoảng lặng → tách người nói → chuyển chữ
        ▼
Bản gỡ băng: "Người nói 1, 2, 3…" kèm mốc giờ; bấm mốc để nghe lại đoạn đó
        │
        ▼   so vector giọng từng người nói với giọng mẫu của người trong cuộc họp (SCRUM-35)
"Có thể là Nguyễn Văn A · 92%"  →  Thư ký bấm "Đúng" hoặc chọn người khác
```

- **Voice ID có đồng ý.** Ở màn "Giọng nói của tôi", người dùng đọc thông báo: mục đích, thứ được
  lưu, phạm vi dùng, quyền của mình và bên kiểm soát dữ liệu. Sau đó họ tự đánh dấu đồng ý (ô không
  đánh dấu sẵn), rồi đọc một đoạn văn mẫu 20–30 giây. Worker kiểm tra mẫu: đủ dài, đủ to, không rè,
  chỉ một người nói. Mỗi người lưu tối đa 5 mẫu.
- **Máy chỉ gợi ý, người quyết định.** Hệ thống chỉ so với những người có tên trong cuộc họp đó và
  đã đồng ý. Chỉ gợi ý khi điểm đủ cao (≥ 0,6) và cách người xếp sau ít nhất 0,1. Mỗi người được gợi
  ý cho tối đa một giọng trong một bản ghi. Không ai bị gán tên nếu Thư ký không xác nhận.
- **Mô hình** (chi tiết và lý do chọn ở [ADR-0026](../05-decisions/0026-tu-chay-nhan-dang-giong-noi.md)):

  | Việc | Mô hình | Giấy phép |
  |---|---|---|
  | Chuyển chữ tiếng Việt | Gipformer 1.5, Zipformer RNN-T 68M tham số, int8 | MIT |
  | Vector giọng 512 chiều (tách người nói và Voice ID) | 3D-Speaker ERes2Net base | Apache-2.0 |
  | Lọc khoảng lặng | Silero VAD | MIT |

  Cả ba chạy trên CPU bằng sherpa-onnx, ghim mã băm SHA-256, image Docker khoảng 770 MB.

## 3. Độ chính xác

### 3.1. Cách đo

Có ba bộ dữ liệu. Công cụ đo nằm trong repo ([`workers/speech/eval/`](../../workers/speech/eval/README.md)),
chạy lại được. Không bộ âm thanh nào được commit.

| Bộ | Gồm gì | Đo được gì |
|---|---|---|
| **VIVOS** (AILAB, ĐH KHTN TP.HCM; CC BY-NC-SA 4.0) | Tập test: 19 người (7 nữ, 12 nam), 760 câu đọc. Dựng 12 cuộc họp, mỗi cuộc 4–6 người nói xen kẽ, tổng 33 phút | WER, tách người nói, Voice ID |
| **VIVOS "khác kênh"** | 12 cuộc họp trên, cho đi qua tiếng vọng phòng, lọc dải tần, tiếng ồn 20 dB và nén mp3 32 kbps. Giọng mẫu vẫn sạch | Tình huống thật: đăng ký giọng trên laptop, họp ghi bằng một máy giữa bàn |
| **Cuộc họp thật** | 5 phút, 4 người, ghi bằng một máy chung. Hai người ngồi xa micro, nói nhỏ hơn người chủ trì 15–20 dB. Có bản gỡ băng kèm nhãn người nói | WER, phần lời bị bỏ sót, tách người nói |

Trong mỗi cuộc họp VIVOS có hai người đặc biệt. Đây là hai tình huống dễ gợi ý sai nhất:

- **Người lạ:** có nói nhưng không có trong danh sách được gợi ý, tức người chưa đồng ý Voice ID.
- **Người vắng:** đã đăng ký giọng nhưng không nói câu nào.

### 3.2. Kết quả trên VIVOS

| Chỉ số | Sạch | Khác kênh |
|---|---|---|
| Tỉ lệ sai từ (WER) | 5,0% | 6,0% |
| Thời gian nói được gán đúng người (tách người nói, chưa tính tên) | 99,8% | 99,9% |
| Số cuộc họp tìm đúng số người nói | 7/12 | 8/12 |
| Gộp nhầm hai người thành một | 0 | 0 |
| Giọng mẫu được nhận | 36/38 (2 mẫu quá ngắn) | 36/38 |

Mọi lần sai số người nói đều là **tách thừa**: một người ra hai nhãn. Thư ký gán được cả hai nhãn cho
cùng một người, nên lỗi này ít hại hơn gộp nhầm hai người.

**Gợi ý tên** theo ngưỡng (khoảng cách 0,1):

| Ngưỡng | Lời người đã đăng ký được gợi ý đúng tên | Gợi ý **sai** người | Người lạ bị gọi tên | Người vắng bị gợi ý | Số gợi ý đúng |
|---|---|---|---|---|---|
| 0,5 | 97,3% / 98,4% | 3,3% / 3,1% | 15,6% / 15,1% | 1 / 1 | 49/50 / 49/50 |
| **0,6 (mặc định)** | **97,3% / 98,4%** | **0,1% / 0%** | **0% / 0%** | **0 / 0** | **49/49 / 49/49** |
| 0,65 | 97,3% / 90,2% | 0% / 0% | 0% / 0% | 0 / 0 | 49/49 / 43/43 |
| 0,7 | 97,3% / 71,0% | 0% / 0% | 0% / 0% | 0 / 0 | 49/49 / 33/33 |

Mỗi ô ghi "sạch / khác kênh". Ở 0,5, người lạ bị gọi bằng tên người khác: đó là lỗi đắt nhất, vì
Thư ký có thể bấm "Đúng" theo thói quen. Ở 0,6, lỗi đó biến mất mà không bỏ sót thêm ai. Từ 0,65,
bộ "khác kênh" bắt đầu bỏ sót. Vì vậy hệ thống mặc định dùng **0,6**.

### 3.3. Kết quả trên cuộc họp thật

| Chỉ số | Bản đầu | Hiện tại |
|---|---|---|
| WER | 34,9% | **16,1%** |
| Phần lời của hai người ngồi xa được chuyển chữ | 10–12% | **84–91%** |
| Số nhãn người nói cho 4 người | 22 | 6 (tách thừa, không gộp nhầm) |
| Thời gian nói gán đúng người, so với bản gỡ băng | — | 68% (xem dưới) |

- Gần hết phần 32% "sai" là **một đoạn liền 63 giây**. Bản gỡ băng ghi đoạn này cho người chủ trì,
  còn worker gán cho một người khác. Theo cách xưng hô và nội dung, worker có thể đúng; đoạn này
  đang được nghe lại để xác nhận. Nếu worker đúng thì tỉ lệ gán đúng người là khoảng **92%**.
- Từ tiếng Anh trong câu tiếng Việt hay bị sai ("production", "ready", "UAT"). Câu không có dấu
  phẩy. Thư ký sửa được từng đoạn trên giao diện; hệ thống ghi ai sửa và lúc nào.

### 3.4. Những gì đã sửa nhờ đo trên giọng thật

Bốn ngưỡng ban đầu được chỉnh trên giọng máy đọc (macOS `say`). Cả bốn đều sai trên giọng thật và đã
được sửa:

| Vấn đề trên giọng thật | Nguyên nhân | Sửa | Kết quả |
|---|---|---|---|
| Một người bị xé thành nhiều người (22 nhãn cho 4 người) | Khung 1,5 giây quá ngắn: trong cùng một phòng, mọi người nghe giống nhau | Khung 3 giây; gộp lần hai các nhóm có giọng tổng thể giống nhau ≥ 0,85 | 6 nhãn, không gộp nhầm |
| Mất gần hết lời người ngồi xa | Bộ lọc khoảng lặng coi tiếng nhỏ là im lặng | Tăng âm trượt (cửa sổ 1,5 giây, tối đa +30 dB) trước khi lọc; hạ ngưỡng lọc 0,3 → 0,2 | WER 34,9% → 16,1%; tiếng ồn vẫn không thành chữ |
| 38% người thật không đăng ký được giọng | Ngưỡng "chỉ một người nói" 0,60 quá chặt | Hạ xuống 0,40, đo trên 94 mẫu một người và 50 mẫu hai người | 100% mẫu thật đạt; 1/50 mẫu hai người lọt |
| Người chưa đăng ký bị gọi tên người khác | Ngưỡng gợi ý 0,5 quá thấp | Nâng lên 0,6 | Không còn gợi ý sai |

### 3.5. Tốc độ và tài nguyên

Đo trên MacBook M1, 4 luồng. Chưa đo trên máy chủ thật; sẽ đo khi chuyển server test lên EC2.

| Âm thanh | Chạy trực tiếp | Trong Docker (Linux arm64) |
|---|---|---|
| 5 phút, cuộc họp thật | 23 giây | 78 giây |
| 10 phút | 43 giây | 132 giây; RAM đỉnh 1,5 GB |
| Một giờ (ước tính) | khoảng 4–5 phút | khoảng 13–16 phút |

Worker xử lý từng bản một, theo hàng đợi. Lúc rảnh, container dùng khoảng 260 MB RAM; sau vài bản ghi
dài liên tiếp, nó giữ tới khoảng 2,4 GB.

## 4. Chi phí

Đơn giá lấy ngày 23/10/2026 từ trang chính thức của từng nhà cung cấp (nguồn ở cuối). Mỗi cuộc họp
tính trung bình 1,5 giờ. Ba kịch bản theo tháng:

- **A:** 40 cuộc họp, 60 giờ.
- **B:** 200 cuộc họp, 300 giờ.
- **C:** 1.000 cuộc họp, 1.500 giờ.

### 4.1. Chuyển chữ và tách người nói

| Phương án | Đơn giá | A (60 h) | B (300 h) | C (1.500 h) | Voice ID |
|---|---|---|---|---|---|
| Google Speech-to-Text V2, chuẩn | 0,016 USD/phút | 58 USD | 288 USD | 1.440 USD | Không có |
| Google, dynamic batch (trả kết quả chậm, tới 24 giờ) | 0,003 USD/phút | 11 USD | 54 USD | 270 USD | Không có |
| Amazon Transcribe, batch | 0,006 USD/phút (trang giá); một số nguồn ghi 0,024 | 22–86 USD | 108–432 USD | 540–2.160 USD | Không có |
| FPT.AI | 540–700 đ/phút (gói 2.000–10.000 phút) | 1,9–2,5 triệu đ | 9,7–12,6 triệu đ | ~48,6 triệu đ (gói lớn phải đàm phán) | Không có |
| **Tự chạy, một máy EC2 c7g.xlarge riêng** (4 vCPU, 8 GB, Singapore), bật 24/7 | 0,1666 USD/giờ máy × 730 giờ = 122 USD/tháng, **cố định** | **122 USD** | **122 USD** | **122 USD** | **Có** |
| Tự chạy, chỉ tính thời gian máy xử lý (RTF 0,25) | ~0,042 USD/giờ họp | 2,5 USD | 12,5 USD | 63 USD | Có |

**Máy riêng có giá cố định.** EC2 tính tiền theo giờ máy bật, không theo số giờ họp, nên máy bật 24/7
tốn 122 USD mỗi tháng dù họp ít hay nhiều. Nếu máy chủ đạt tốc độ như Docker trên M1 (xử lý một giờ họp
mất 15 phút, RTF 0,25), một máy c7g.xlarge xử lý được khoảng 2.900 giờ họp mỗi tháng (730 giờ × 4). Cả
ba kịch bản đều nằm trong sức một máy, nên cùng giá; quá mức đó mới cần thêm máy. Vì giá cố định, ở
kịch bản A thuê dịch vụ còn rẻ hơn; máy riêng chỉ rẻ hơn khi họp nhiều (mốc hoà vốn ở mục 4.3).
Tốc độ trên EC2 chưa đo (mục 3.5).
Nếu chạy chung máy với ứng dụng, chi phí thêm chỉ là phần nâng cấu hình máy (worker cần thêm khoảng
2–3 GB RAM). Dòng cuối là giới hạn dưới, khi chỉ trả tiền cho thời gian máy thực sự xử lý.

**Dịch vụ đám mây không thay được phần Voice ID.** Microsoft đã ngừng Azure AI Speaker Recognition
từ 30/09/2025. Amazon ngừng Amazon Connect Voice ID từ 20/05/2026. Các dịch vụ chuyển chữ ở trên chỉ
tách "người nói 1, 2" trong một file, không biết đó là ai. Dù chọn dịch vụ nào để chuyển chữ, phần
nhận ra người vẫn phải tự chạy.

**Dùng dịch vụ nước ngoài là chuyển dữ liệu cá nhân xuyên biên giới** (Luật 91/2025, Điều 20 khoản
1 điểm c). Đơn vị triển khai khi đó phải lập hồ sơ đánh giá tác động chuyển dữ liệu xuyên biên giới
và nộp trong 60 ngày (Điều 20 khoản 2). Tự chạy thì không phát sinh nghĩa vụ này.

### 4.2. Tóm tắt nội dung (chưa làm)

Một giờ họp ra khoảng 15.000 từ; ước tính 20–40 nghìn token đầu vào và khoảng 1.500 token đầu ra.

| Phương án | Đơn giá (đầu vào / đầu ra, mỗi 1 triệu token) | Một giờ họp | Kịch bản C |
|---|---|---|---|
| Claude Haiku 5.5 | 0,10 / 0,50 USD | dưới 0,01 USD | khoảng 4–7 USD |
| Claude Sonnet 5.5 | 2 / 10 USD | 0,06–0,10 USD | khoảng 80–145 USD |
| Mô hình ngôn ngữ tự chạy | — | Chưa đo | — |

Chi phí tóm tắt nhỏ so với chuyển chữ. Nhưng gửi bản gỡ băng ra API nước ngoài lại là chuyển dữ liệu
xuyên biên giới như ở mục 4.1. Vì vậy cần chốt hướng (API có hồ sơ đánh giá, hay tự chạy) trước khi
làm.

### 4.3. Kết luận về chi phí

- **Tự chạy (một máy riêng) rẻ hơn từ khoảng 130 giờ họp mỗi tháng** so với Google bản chuẩn, từ
  khoảng 340 giờ so với Amazon, và từ khoảng 680 giờ so với Google dynamic batch. Với tổ chức nhỏ,
  chi phí ngang dịch vụ đám mây, hoặc thấp hơn nhiều nếu worker chạy chung máy với ứng dụng.
- Tự chạy là phương án **duy nhất** có Voice ID và giữ âm thanh trong tổ chức. Đó là hai điều hội
  đồng và luật cùng yêu cầu.

## 5. Tuân thủ Luật Bảo vệ dữ liệu cá nhân

Căn cứ: Luật Bảo vệ dữ liệu cá nhân 91/2025/QH15 (hiệu lực 01/01/2026) và Nghị định 356/2025/NĐ-CP.
Hai văn bản này thay chế độ của Nghị định 13/2023/NĐ-CP mà Report 3 đang trích.

**Ghi âm và Voice ID là hai lớp khác nhau.**

- **Bản ghi cuộc họp là dữ liệu cá nhân thông thường.** Luật không coi im lặng là đồng ý (Điều 9
  khoản 4 điểm d), nên ghi âm không dựa vào việc "không ai phản đối". Căn cứ ghi âm thuộc về đơn vị
  triển khai: hoạt động của cơ quan nhà nước (Điều 19 khoản 1 điểm c), hoặc thoả thuận, nội quy mà
  người lao động đã biết (Điều 19 khoản 1 điểm d, Điều 25 khoản 3). Hệ thống lo phần còn lại:
  thông báo cho người dự, ghi mục đích, và xoá khi hết hạn.
- **Giọng dùng để nhận ra người là dữ liệu sinh trắc học** (Điều 31 khoản 2), thuộc danh mục dữ liệu
  nhạy cảm (NĐ 356, Điều 4 khoản 1 điểm đ). Lớp này bắt buộc có sự đồng ý riêng của từng người.

| Nghĩa vụ | Điều khoản | Hệ thống làm gì |
|---|---|---|
| Đồng ý tự nguyện, biết rõ loại dữ liệu, mục đích, bên kiểm soát, quyền của mình | Luật Đ.9 k.2 | Màn "Giọng nói của tôi" nêu đủ trước khi đồng ý; tên bên kiểm soát lấy từ cấu hình của đơn vị triển khai |
| Không đặt sẵn đồng ý; báo rõ là dữ liệu nhạy cảm | NĐ 356 Đ.6 k.3, k.4 | Ô đồng ý trống; thông báo ghi "dữ liệu cá nhân nhạy cảm" |
| Đồng ý kiểm chứng được, bên kiểm soát lưu lại | NĐ 356 Đ.6 k.1, k.2 | Bảng `voice_consents`: ai, lúc nào, phiên bản thông báo, mục đích; kèm nhật ký kiểm toán |
| Rút đồng ý | Luật Đ.10 | Một nút. Trong một transaction: xoá mọi giọng mẫu và mọi gợi ý trỏ tới người đó |
| Lưu đúng thời hạn, xoá an toàn | Luật Đ.3 k.3, Đ.14 | Bản ghi tự xoá sau 180 ngày (cấu hình được); xoá theo yêu cầu thì giữ một dòng làm bằng chứng đã xoá |
| Bảo vệ dữ liệu sinh trắc học: hạn chế truy cập, có theo dõi | Luật Đ.31 k.4 | Chỉ lưu vector, mã hoá AES-256-GCM gắn với người, mẫu và mô hình; không endpoint nào trả vector ra ngoài; mọi thao tác có nhật ký |
| Thu thập tối thiểu | Luật Đ.3 | Không lưu âm thanh mẫu. Vector của các giọng trong bản ghi chỉ dùng để so một lần rồi bỏ |
| Hồ sơ đánh giá tác động xử lý dữ liệu, nộp trong 60 ngày | Luật Đ.21; miễn trừ của Đ.38 k.2 không áp dụng khi xử lý dữ liệu nhạy cảm | Việc của đơn vị triển khai. Hệ thống cung cấp đủ thông tin cho hồ sơ: [tài liệu API 13](../03-api/13-nhan-dien-giong-noi.md) |
| Chuyển dữ liệu xuyên biên giới | Luật Đ.20 | Không phát sinh: mô hình chạy trong hệ thống |

Quyết định và lý do nằm ở [ADR-0027](../05-decisions/0027-voice-id-co-dong-y.md).

## 6. Hạn chế và việc tiếp theo

| Việc | Hiện trạng | Hướng làm |
|---|---|---|
| Chuyển chữ **ngay trong khi họp** | Hiện chạy sau khi lưu bản ghi. Đã đo được là khả thi trên CPU: một câu 10 giây giải mã trong 0,16–0,23 giây. Giải mã lại câu đang nói mỗi 0,5 giây để hiện chữ xem trước tốn khoảng 0,3 lần thời gian thực của **một** nhân CPU (M1, chạy trực tiếp; trong Docker có thể chậm khoảng 3 lần) | Hiện chữ xem trước trong phòng họp, không lưu. Bản lưu vẫn là bản gỡ băng sau buổi họp. Họp online: mỗi trình duyệt tự ghi mic của mình (SCRUM-44), nên biết ngay ai nói mà không cần Voice ID |
| Đo trên giọng của nhóm, ghi bằng laptop và điện thoại | Chưa có | Mỗi người ghi 2 giọng mẫu và khoảng 10 câu ([hướng dẫn](../../workers/speech/eval/README.md#2-giọng-của-nhóm)) |
| Đo trên máy chủ thật | Chưa có | Đo khi chuyển server test lên EC2 |
| Hai người nói cùng lúc | Không tách được, chỗ chuyển người nói lệch tới khoảng 0,75 giây | Chấp nhận cho bản đầu; Thư ký sửa tay |
| Từ tiếng Anh, dấu câu | Sai nhiều | Danh sách từ chuyên ngành (hotwords) của sherpa-onnx; chưa thử |
| Tóm tắt bằng AI | Chưa làm | Chốt hướng ở mục 4.2 |
| Khách mời chưa có tài khoản | Không đăng ký Voice ID được | Chờ SCRUM-59 |
| Màn cho người phụ trách bảo vệ dữ liệu | Chưa có | Xem danh sách đồng ý, rút thay khi có đơn giấy |

## Nguồn (truy cập 23/10/2026)

- Luật Bảo vệ dữ liệu cá nhân 91/2025/QH15, toàn văn: [congluatviet.vn](https://congluatviet.vn/van-ban/luat-bao-ve-du-lieu-ca-nhan-91-2025-qh15).
- Nghị định 356/2025/NĐ-CP: [hethongphapluat.com](https://hethongphapluat.com/nghi-dinh-356-2025-nd-cp-huong-dan-luat-bao-ve-du-lieu-ca-nhan.html).
- VIVOS: [Zenodo](https://zenodo.org/records/7068130). Luong & Vu, *A non-expert Kaldi recipe for
  Vietnamese Speech Recognition System*, WLSI/OIAF4HLT 2016.
- Google Speech-to-Text pricing: [cloud.google.com/speech-to-text/pricing](https://cloud.google.com/speech-to-text/pricing).
- Amazon Transcribe pricing: [aws.amazon.com/transcribe/pricing](https://aws.amazon.com/transcribe/pricing/).
- FPT.AI STT pricing: [docs.fpt.ai](https://docs.fpt.ai/docs/en/speech/documentation/stt-pricing).
- Giá EC2 vùng Singapore (tham khảo, tổng hợp từ AWS): [DoiT c7g.xlarge](https://www.doit.com/compute/spot/ap-southeast-1/c7g.xlarge).
- Claude pricing: [anthropic.com/pricing](https://www.anthropic.com/pricing).
- Azure AI Speaker Recognition ngừng từ 30/09/2025: [Microsoft Learn](https://learn.microsoft.com/en-us/azure/cognitive-services/speaker-recognition/home).
- Amazon Connect Voice ID ngừng từ 20/05/2026: [AWS docs](https://docs.aws.amazon.com/connect/latest/adminguide/amazonconnect-voiceid-end-of-support.html).

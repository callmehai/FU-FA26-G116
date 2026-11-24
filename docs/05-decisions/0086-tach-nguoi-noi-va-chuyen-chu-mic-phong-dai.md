# ADR-0085: Tách người nói và chuyển chữ bản ghi mic phòng dài

- **Trạng thái:** Đề xuất — PO (Hải) 24/11/2026 ("làm cho ASR ngon nhất")
- **Ngày:** 2026-11-25
- **Người quyết định:** chờ PO duyệt
- **Bổ sung:** [ADR-0074](0074-nguong-tach-nguoi-noi-va-voice-id-sau-buoi-thu-21-11.md) (giữ ngưỡng gom nhóm 0,72 và
  gộp lần hai 0,8, thêm một luật gộp nhóm nhỏ), [ADR-0026](0026-tu-chay-nhan-dang-giong-noi.md) (vẫn Gipformer 1.5, đổi
  bản int8 sang bản gốc fp32, vẫn ghim mã băm)

## Bối cảnh

Cuộc họp kết hợp ngày 24/11/2026: mic phòng 62 phút, một laptop đặt trên bàn (âm lượng trung vị −40 dBFS, so với −30
của buổi 23/11), nói chuyện tự nhiên, chen ngang, nhiều từ dự án tiếng Anh. Worker trả **20–25 "Người nói"** trong khi
chỉ 6–7 người nói; chỉ khoảng 7 giọng có hơn một phút lời, còn lại là mẩu vài giây. 16% số từ có độ chắc dưới 0,5 (8,8%
ngày 22/11, 10,8% ngày 23/11). Đã loại hai nghi vấn: từ khoá ưu tiên (bật/tắt chỉ đổi 51/650 dòng, vẫn 25 giọng) và
code mới (bản ghi 23/11 chạy bằng code 93431737 và code hiện tại ra cùng 14 giọng, 429 đoạn).

Cuộc họp thật không có đáp án chính xác ai nói lúc nào, nên lần đo này dựng các bộ có đáp án chính xác bằng
`workers/speech/eval/` (cách dựng ở [README của eval](../../workers/speech/eval/README.md#6-cuộc-họp-dài-mic-xa-tách-người-nói-và-chuyển-chữ-cả-buổi)):

| Bộ | Âm thanh | Dùng để |
|---|---|---|
| far-long | 3 cuộc họp 30–36 phút, 7 người (VIVOS train), mic xa: tiếng vọng, ồn hồng 15 dB, rì rầm người khác, một người qua loa laptop, 15% lượt nói chồng, một người chỉ nói vài giây, −40 dBFS | chỉnh |
| far-hard | 2 cuộc họp 50–52 phút, 6 người, ồn 10 dB, 25% lượt nói chồng, hai người chỉ nói vài giây, −42 dBFS | kiểm chứng |
| far-test | 3 cuộc họp 8–9 phút, 6 người, từ VIVOS test (người chưa từng dùng để chỉnh) | kiểm chứng |
| vivos-room, vivos-clean | 12 cuộc họp ngắn 2–4 phút, 4–6 người (`vivos_dataset.py`, có và không qua phòng) | cuộc họp ngắn không được tệ đi |
| VIVOS test, VietMed test | 760 câu đọc; 573 câu nói tự nhiên (một giờ, MIT), để nguyên và phát qua phòng xa (ồn 12 dB, −40 dBFS) | so model chuyển chữ |

Nguyên nhân tách thừa, đọc từ các nhóm của worker so với đáp án: ngoài các người thật, gom nhóm để lại một đuôi nhóm nhỏ
(3–124 giây). Phần lớn có độ thuần chỉ khoảng 0,5: đó là các khung hai người nói chồng nhau, giọng "nửa người này nửa người
kia", nên không giống hẳn ai để gộp lần hai (cosine 0,8) nhận vào. Phần còn lại là mẩu cười, mẩu của người qua loa
laptop. Luật cũ chỉ gộp nhóm dưới 2 giây.

## Quyết định

1. **Nhóm có dưới 2% tổng thời lượng lời của bản ghi không thành người nói riêng** (`MIN_SPEAKER_SHARE = 0.02`): khung
   của nó về người gần nhất, như nhóm dưới 2 giây trước đây. 2% của buổi họp một giờ là khoảng một phút; của cuộc họp
   3 phút là dưới 2 giây nên luật cũ vẫn quyết định. Không đổi ngưỡng gom nhóm 0,72 và gộp lần hai 0,8 của ADR-0074,
   vì hai ngưỡng đó được chỉnh trên giọng thật cùng phòng (hai người khác nhau giống nhau tới 0,73–0,745), còn các bộ
   tổng hợp có giọng khác nhau rõ hơn thật.
2. **Chuyển chữ bằng Gipformer bản gốc fp32** thay bản int8 (`gipformer/encoder.onnx`, `decoder.onnx`, `joiner.onnx`,
   ghim mã băm ở commit `dd9227dc` của kho Hugging Face). Mã model trong mỗi kết quả đổi thành
   `gipformer-1.5-68m-fp32@96b3c83c9c39`. Model vector giọng không đổi, nên Voice ID và mẫu giọng đã lưu không ảnh hưởng.

## Số đo

Trước: code của `app` lúc tách nhánh. Sau: cả hai thay đổi. Cả quy trình của hồ sơ `room` (tăng âm, VAD, tách người
nói, chuyển chữ, có từ khoá ưu tiên), WER/CER trên chữ của máy chuyển chữ (bỏ dấu câu, số viết bằng chữ). "Giọng" là số
"Người nói" worker trả, so với số người thật (trong ngoặc: số người nói từ 30 giây trở lên). DER kiểu NIST, không chừa
biên, mỗi giọng ứng nhiều nhất một người, nên một người bị tách đôi thì phần nhỏ hơn tính là sai.

| Bộ | WER trước → sau | CER trước → sau | DER trước → sau | Giọng trước → sau / người |
|---|--:|--:|--:|---|
| far-long (3 × 30–36 phút) | 9,5% → **8,4%** | 7,2% → 6,4% | 16,2% → **13,3%** | 42 → **18** / 21 (18) |
| far-hard (2 × 50–52 phút) | 10,8% → **10,2%** | 8,2% → 7,9% | 18,1% → **16,5%** | 26 → **10** / 12 (8) |
| far-test (3 × 8–9 phút) | 12,0% → **11,1%** | 8,8% → 8,3% | 21,0% → 20,4% | 26 → 18 / 18 (14) |
| vivos-room (12 × 2–4 phút) | 6,2% → **5,9%** | 3,7% → 3,5% | 27,4% → 27,4% | 52 → 52 / 61 |
| vivos-clean (12 × 2–4 phút) | 5,1% → 5,1% | 3,1% → 3,1% | 19,6% → 19,6% | 60 → 60 / 61 |

DER của hai bộ `vivos-*` cao vì đáp án tính cả khoảng lặng hai đầu mỗi câu VIVOS (VAD bỏ đúng); không đổi giữa trước và
sau. Ở các cuộc họp dài, mỗi cuộc họp ra đúng số người nói từ 30 giây trở lên (far-long: 6, 6, 6; far-hard: 5, 5 cho 4 và
4 người, phần thừa là một nhóm nói chồng lớn).

Riêng model chuyển chữ (cùng các đoạn cắt sẵn, chỉ đổi model):

| Bộ | Gipformer int8 (trước) | **Gipformer fp32** | k2-fsa zipformer-vi 2025-04-20 (70 nghìn giờ, int8) | Zipformer 30M (hynt, int8) |
|---|--:|--:|--:|--:|
| VIVOS test, 760 câu đọc (hồ sơ `clip`) | 4,9% | **4,7%** | 6,8% | 5,0% |
| VietMed, 573 câu nói tự nhiên (hồ sơ `clip`) | 24,0% | **23,6%** | 24,2% | 24,3% |
| VietMed phát qua phòng xa (hồ sơ `room`) | 23,0% | **22,1%** | | |
| far-test (hồ sơ `room`) | 11,6% | **10,9%** | 13,5% | 11,8% |
| RTF riêng bước chuyển chữ, 4 luồng | 0,030 | 0,038–0,045 | 0,030 | 0,028 |

Bản int8 mất từ nhiều hơn ở chỗ xa và ồn: chênh 0,2 điểm trên câu đọc sạch, 0,7–0,9 điểm trên tiếng phòng xa.

Tốc độ và bộ nhớ: xem *Hệ quả*.

## Phương án đã thử, không chọn

| Phương án | Kết quả | Vì sao không chọn |
|---|---|---|
| Ngưỡng cố định 10 / 20 / 30 giây thay cho 2% | far-long 33 / 23 / 18 giọng; nhưng 20 giây làm cuộc họp ngắn gộp người (vivos-room DER 27% → 52%) | Phải theo độ dài bản ghi |
| 1% hoặc 1,5% thời lượng lời; 2% nhưng tối đa 20 giây | còn 23 / 19 / 23 giọng ở far-long, far-hard 1,5% tệ hơn trước (DER 19,1%) | Còn sót nhóm nói chồng 20–60 giây |
| Gộp lần hai ở 0,75 / 0,7 | far-long 42 → 36 / 28 giọng, DER 14,0% | Hai người khác nhau trong cùng phòng thật giống nhau tới 0,73–0,745 (ADR-0074); giọng VIVOS khác nhau rõ hơn giọng thật nên bộ tổng hợp không chứng minh được là an toàn |
| Ngưỡng gom nhóm 0,65 / 0,78 | 63 / 29 giọng ở far-long, DER 17,3% / 15,0% | Kém hơn 2%; 0,78 gộp nhầm người ở phòng thật 21/11 |
| Gom nhóm trung bình (average linkage, tự viết) 0,4 / 0,5, kèm 2% | DER gần như 2% (far-long 14,2% / 14,0%, vivos-room 0,4 tệ hẳn 48%) | Không hơn rõ, và đổi cách gom nhóm đã chỉnh trên giọng thật |
| Chia lại khung theo tâm nhóm (k-means, 3 vòng) | không đổi hoặc tệ hơn (far-hard DER 19,6%) | Không lợi |
| Chuyển chữ cả quãng lời VAD rồi chia từ cho người nói theo mốc từ, thay vì cắt âm thanh ở chỗ đổi người | far-test 11,8% → 11,7%, far-long 8,2% → 8,8% (int8); 7,6% → 8,1% (fp32) | Không lợi, có chỗ tệ hơn |
| Một mức tăng âm cho cả đoạn thay tăng âm trượt trước khi chuyển chữ | chỉ far-test 11,7% → 11,4%, bộ khác không đổi | Lợi quá nhỏ để đổi bước đã chỉnh trên cuộc họp thật |
| Nới VAD 0,25 giây mỗi bên (đang 0,1) | far không đổi, vivos-room 6,2% → 5,9% nhưng đổi tách người nói | Không rõ lợi |
| Model k2-fsa zipformer-vi (70 nghìn giờ), Zipformer 30M | tệ hơn hoặc ngang Gipformer (bảng trên) | |

## Hệ quả

- Cuộc họp dài ra gần đúng số người nói thật thay vì gấp 2–3 lần; Thư ký không phải gán tên cho hàng chục "Người nói" vụn.
  Cuộc họp ngắn (vài phút) không đổi.
- **Người nói rất ít bị ghi dưới giọng người gần nhất**: ai nói dưới 2% thời lượng lời (dưới khoảng một phút trong buổi
  họp một giờ) không còn nhãn riêng. Trên các bộ tổng hợp, người chỉ nói 2–15 giây giữ được nhãn riêng ở 5/10 trường hợp
  trước, 1/10 sau. Thư ký sửa người nói của câu đó bằng tay như với mọi câu gán nhầm.
- Chữ đúng hơn 0,3–1,1 điểm WER trên mic phòng; mã model trong bản gỡ băng mới là `gipformer-1.5-68m-fp32@96b3c83c9c39`.
  Bản gỡ băng cũ giữ nguyên; chuyển chữ lại thì dùng model mới. Model vector giọng không đổi: Voice ID, mẫu giọng đã lưu và
  ngưỡng `VoiceId:*` không ảnh hưởng.
- Tệp model nặng hơn khoảng 190 MB (Gipformer 73 MB → 270 MB), image Docker của worker khoảng 1,15 GB. Máy Mac chạy worker
  chính phải chạy lại `fetch_models.py` sau khi kéo code (ADR-0081: worker tự kiểm mã băm và từ chối chạy nếu thiếu tệp);
  server build lại image như mọi lần đổi `workers/speech/`.
- Tốc độ và bộ nhớ: TODO
- Chưa đo trên bản ghi thật vì không có đáp án; cần chạy `meeting_bench.py` (chế độ không đáp án: chỉ đếm giọng) trên bản ghi
  mic phòng 21/11, 23/11, 24/11 trước và sau khi gộp, và xem lại sau buổi họp thật kế tiếp.

## Nghĩa vụ theo Luật 91/2025/QH15

Không đổi dữ liệu nào được lưu hay gửi đi: worker vẫn không lưu gì, vector giọng vẫn chỉ trả cho backend. Lần đo không dùng
bản ghi thật nào của nhóm: chỉ dùng corpus công khai (VIVOS, CC BY-NC-SA 4.0, học thuật; VietMed, MIT), ở thư mục tạm trên
máy dev, xoá sau khi đo, không đưa vào repo.

# ADR-0076: Viết số bằng chữ số trong bản gỡ băng (ITN tiếng Việt, tự viết theo luật)

- **Trạng thái:** Đề xuất — buổi thử 21/11/2026
- **Ngày:** 2026-11-22
- **Người quyết định:** chờ PO duyệt
- **Bổ sung:** [ADR-0037](0037-them-dau-cau-bang-vibert-capu.md) (bước dấu câu không đổi số từ; bước này thì có, và
  mang mốc từ, độ chắc theo)

## Bối cảnh

Gipformer viết mọi con số bằng chữ: "hai nghìn không trăm hai mươi sáu", "năm phẩy năm", "mười lăm phần trăm",
"ngày hai mươi hai tháng mười một". Biên bản cần "2026", "5,5", "15%", "ngày 22 tháng 11". Buổi thử 21/11/2026 nhóm đọc
thử "hai hai, hai ba, hai tư", "hai không hai sáu bảy triệu", "hai nghìn không trăm hai sáu" để xem máy viết ra sao.

Có hai ràng buộc. Mỗi từ của `text` đi kèm một mốc bắt đầu (`wordStartsMs`) và một độ chắc (`wordConfidence`, PR #468),
khớp từng từ một; FE dựa vào đó để bấm từ thì phát và tô vàng từ máy không chắc. Và tiếng Việt có nhiều chữ số mà
không phải số: "thứ hai" (thứ trong tuần), "một số", "một người", "hai ba người" (hai hoặc ba người), "có một không hai",
"hai mươi năm" (hai mươi năm trời, vì 25 đọc là "hai mươi lăm").

## Quyết định

Worker viết số bằng chữ số **sau** bước dấu câu (bước dấu câu cần đúng số từ của model), bằng một module tự viết,
thuần Python, không thêm thư viện: `workers/speech/speech/numbers.py`. Áp dụng cho mọi đoạn trả về: bản ghi mic phòng
và câu từ micro riêng đi chung một đường.

Luật, thận trọng, vì viết sai một con số còn tệ hơn để nguyên chữ:

1. Số **từ hai từ trở lên** đúng ngữ pháp số tiếng Việt thì đổi: "hai mươi ba" → 23, "một trăm linh năm" → 105,
   "hai nghìn không trăm hai sáu" → 2026. Hiểu "mốt", "tư", "lăm"/"nhăm", "linh"/"lẻ", "nghìn"/"ngàn", "triệu",
   "tỷ"/"tỉ", và cách nói tắt sau hàng ("hai nghìn hai" → 2200, "một trăm hai" → 120).
2. Số **một từ** giữ chữ, trừ khi từ sau cho biết đó là số lượng: thập phân ("năm phẩy năm" → 5,5), phần trăm ("năm
   phần trăm" → 5%), giờ ("ba giờ" → 3 giờ, riêng "một phút"/"một giây" giữ chữ), ngày tháng ("ngày hai tháng chín"
   → ngày 2 tháng 9).
3. Hai chữ số liền nhau là một khoảng ("hai ba người", "sáu bảy triệu"), giữ chữ; trừ dạng tắt có hàng đơn vị chỉ hàng
   chục mới có ("hai mốt" → 21, "hai tư" → 24, "ba lăm" → 35), hoặc nằm trong số lớn hơn, hoặc là ngày ("ngày hai hai
   tháng mười một" → ngày 22 tháng 11).
4. Từ bốn chữ số đọc rời trở lên là một mã hay một năm: "hai không hai sáu" → 2026.
5. Sau "thứ", một từ giữ nguyên ("thứ hai", "thứ năm"); "năm" chỉ là số khi ngữ pháp cần ("năm mươi", "năm trăm",
   "năm triệu"), nên "hai mươi năm" → "20 năm", "năm hai nghìn" → "năm 2000".
6. Một dấu câu bước trước đặt giữa hai từ thì số dừng ở đó.

Cách viết: từ 10.000 trở lên có dấu chấm phân cách hàng nghìn; số bốn chữ số không có (năm); thập phân dùng dấu phẩy.
Số tròn triệu, tròn tỷ giữ chữ "triệu", "tỷ" ("200 triệu", "2 tỷ 300 triệu"); số lớn khác viết đủ ("2.500.000").

Nhiều từ gộp thành một thì mốc của từ mới là mốc của từ đầu, độ chắc là độ chắc thấp nhất trong các từ gộp, nên
`wordStartsMs` và `wordConfidence` vẫn mỗi từ một số. `SPEECH_NUMBERS=0` tắt bước này (để so sánh, và để đo WER với
đáp án viết bằng chữ).

## Phương án đã cân nhắc

| Phương án | Ưu | Nhược | Vì sao không chọn |
|---|---|---|---|
| Thư viện ITN có sẵn (NeMo text processing, các bản WFST tiếng Việt) | Phủ rộng hơn | Kéo theo pynini/OpenFST hay cả bộ NeMo, image nặng thêm hàng trăm MB; trả chuỗi, không trả từ nào gộp từ từ nào, nên mất mốc từ và độ chắc | Phá ràng buộc khớp từng từ |
| Đổi mọi chữ số, kể cả một từ | Đơn giản | "một số", "hai người", "thứ hai" thành "1 số", "2 người", "thứ 2" | Sai nhiều hơn đúng trong lời họp |
| Để Thư ký sửa tay | Không tốn công | Mỗi biên bản có số liệu đều phải sửa | Đúng việc máy làm được |

## Hệ quả

- Bản gỡ băng và câu micro riêng mới có số bằng chữ số; bản cũ giữ nguyên tới khi chuyển chữ lại.
- "hai hai, hai ba" vẫn giữ chữ (có thể là khoảng); "hai tư" thành 24. Chuỗi rời "hai không hai sáu bảy triệu" thành
  "20267 triệu": máy không đoán được người nói định tách ở đâu. Dấu câu mà bước trước đặt nhầm giữa một con số
  ("hai nghìn. không trăm. hai sáu") làm con số đó chỉ đổi được một phần.
- Một dòng chỉ có con số ngắn đi nhiều ký tự ("Hai mươi ba." → "23."), nên dễ rơi vào luật câu ngắn (PR #467) hơn khi
  giọng đó không khớp ai.
- Đo WER bằng `workers/speech/eval/` thì chạy worker với `SPEECH_NUMBERS=0`, vì đáp án viết số bằng chữ.
- Không đổi dữ liệu cá nhân nào được lưu.

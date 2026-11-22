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
6. Một dấu câu bước trước đặt giữa hai từ thì số dừng ở đó, trừ ba chỗ chỉ có thể là một con số (xem *Bổ sung
   22/11/2026*).

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

## Bổ sung 22/11/2026: các trường hợp từ cuộc họp thật 22/11

Bản gỡ băng mic phòng của cuộc họp nhóm 22/11/2026 (80 phút) còn những con số đổi dở hoặc không đổi. Luật thêm, vẫn thận
trọng; mỗi trường hợp có test trong `workers/speech/tests/test_numbers.py`:

1. **Dấu câu bước trước đặt giữa một con số.** Chỉ bỏ ở ba mẫu, và chỉ khi bỏ xong con số đổi được qua chỗ đó (không đổi
   được thì dòng giữ nguyên như bước dấu câu viết):
   - sau "phẩy" đứng sau một từ số, trước chữ số của phần thập phân: "không phẩy. bảy hai" → 0,72, "không phẩy? bảy" →
     0,7 ("không" đứng một mình sau dấu là "không phải", không bỏ);
   - giữa "giờ" (dấu chấm, hỏi, than) và phút từ 1 đến 59: "mười bốn giờ. hai mươi lăm" → 14 giờ 25; không bỏ khi sau số
     phút là một đơn vị đếm ("ba giờ. Hai mươi người" giữ dấu);
   - trước một đơn vị đứng một mình ở cuối dòng hoặc thành một câu riêng: "một trăm năm mươi. Trang" → 150 trang. Đơn vị
     có câu chữ đi tiếp ("150. Trang nói") thì giữ, vì có thể là tên người.

   Dấu bị bỏ chuyển xuống sau con số khi từ kế tiếp viết hoa (bước dấu câu đã mở câu mới ở đó): "không phẩy? bảy Ý em"
   → "0,7? Ý em"; không thì bỏ hẳn. Số từ không đổi, nên mốc từ và độ chắc vẫn khớp từng từ.
2. **Khoảng hai chữ số "từ … đến/tới …".** "từ hai hai đến hai bảy" → từ 22 đến 27: trong khoảng có "từ … đến", hai chữ số
   rời không thể là "2 hoặc 2", nên là số hai chữ số. Đầu sau cũng đổi khi đầu trước đã là chữ số ("từ mười hai đến hai bảy"
   → từ 12 đến 27). Ngoài mẫu này hai chữ số liền nhau vẫn là khoảng (luật 3).
3. **"chấm" giữa hai chữ số đơn** là số phiên bản hay mục: "bốn chấm năm" → 4.5, "chương hai chấm ba" → 2.3 (dấu chấm, không
   phải dấu phẩy thập phân, vì người nói đọc "chấm"). Không đổi khi trước hay sau là từ số khác, khi "năm" đi với "nay",
   "ngoái", "học"… ("ba chấm năm nay"), hay khi "không" cuối câu là từ hỏi ("bốn chấm không?").
4. **Phút một từ sau giờ đã viết bằng chữ số (0–24):** "9 giờ mười" → 9 giờ 10, "13 giờ không năm" → 13 giờ 05; không đổi
   khi sau đó là đơn vị đếm ("2 giờ mười người").

Không đổi:

- "lúc mười 16 giờ, 17 giờ": máy nghe được "lúc mười mười sáu giờ, mười bảy giờ", người nói lặp lại "mười" trước khi nói
  giờ. "mười" thứ nhất là một từ số đứng một mình, không có đơn vị, nên giữ chữ theo luật 2 là đúng.
- "opers 400 năm": máy nhận dạng nghe "bốn trăm năm" (từ "400" chiếm thời lượng của hai từ), bước này chỉ viết lại đúng
  từ đó; bước này không bao giờ thêm chữ "trăm". Người nói định nói "bốn chấm năm", sai ở máy nhận dạng.
- "một trăm. năm mươi trang" vẫn là "100. 50 trang": có thể đúng là hai câu ("không quá 100. 50 trang là vừa").


# ADR-0038: Bê từ AI-eCabinet: đánh dấu tiếng lọt giữa các micro riêng, loại câu khỏi bản ghi, giữ câu chưa gửi trên máy

- **Trạng thái:** Đề xuất — SCRUM-109, PO 28/10/2026
- **Ngày:** 2026-10-28
- **Người quyết định:** Hải (PO)
- **Theo:** AI-eCabinet `origin/main` 365095c7 (2026-10-28): `modules/speech/internal/fold.go` (Fold theo đoạn),
  `exclusion.go`, migration 0087/0108, `clients/web/.../speech/idbClipStore.ts`, `clipQueue.ts`,
  `docs/research/2026-10-23-per-mic-crosstalk-ket-qua.md`.

## Bối cảnh

[ADR-0029](0029-ghi-am-theo-micro-tung-nguoi.md) chọn ghi theo micro từng người và ghi lại những gì eCab có mà TrustCabinet
chưa làm: giữ câu chưa gửi trong IndexedDB, mỗi người một chỗ ghi, đánh dấu tiếng lọt. Họp trực tiếp, laptop nào cũng nghe
người ngồi cạnh, nên cùng một câu xuất hiện hai, ba lần trong bản gỡ băng. Đo trên cuộc họp tổng hợp từ VIVOS
(`workers/speech/eval/bleed.py`), số từ trùng bằng 114 % số từ thật.

## Quyết định

1. **Worker đo độ to của từng đoạn** (`levelDbfs`, trước khi tăng âm) và có hồ sơ `clip` cho câu từ micro riêng: tăng âm tối
   đa 6 dB thay vì 30 dB, VAD 0,5. Backend lưu các đoạn của câu (thời gian, số từ, độ to) trong `speech_clips.segments`,
   không lưu âm thanh.
2. **Backend đánh dấu câu lọt khi đọc** (`SpeechClipFold`, quy tắc đoạn của eCab, bỏ phần phân xử bằng giọng mẫu): độ lệch
   đồng hồ tốt nhất trong ±2 giây, phủ ≥ 60 %, chứa ≥ 50 % số từ với ≤ 3 từ lạ, to hơn ≥ 6 dB. **Chỉ đánh dấu, không ẩn.**
   Mọi người trong cuộc họp đều thấy dấu.
3. **Chủ tọa hoặc Thư ký loại câu khỏi bản ghi** (lý do `Leak` hoặc `Other`) và đưa lại được. Không xoá gì; người khác chỉ
   thấy ở đó từng có một câu. Người nói vẫn tự xoá được câu của mình như trước.
4. **Trình duyệt giữ câu chưa gửi trong IndexedDB** cho tới khi server nhận, và gửi tiếp ở lần mở trang sau; backend nhận
   câu như thế đến **7 ngày** sau khi cuộc họp kết thúc. Quá 8 ngày thì trình duyệt tự bỏ.
5. **Mỗi người một tab ghi** cho mỗi cuộc họp (Web Lock của trình duyệt); tab sau thấy "Đang ghi ở tab khác" và có thể ghi
   thay.

## Phương án đã cân nhắc

| Phương án | Ưu | Nhược | Vì sao không chọn |
|---|---|---|---|
| Ẩn hẳn câu bị đánh dấu | Bản gỡ băng sạch ngay | Đánh dấu nhầm là mất lời thật mà không ai biết | eCab cũng chỉ đánh dấu (D3) |
| Đánh dấu cả câu như Fold cũ của eCab | Không cần lưu đoạn | eCab đo được recall 0,05; câu dài lẫn cả lời chủ và lời lọt | Theo đoạn bắt được nhiều hơn hẳn |
| Phân xử bằng giọng mẫu (G3 của eCab) | Gỡ được trường hợp hai bản to bằng nhau | Phải tính vector giọng cho mọi câu, chỉ dùng được với người đã đồng ý Voice ID | Để sau |
| Giữ chỗ ghi ở server như eCab (`speech_mic.device_id`) | Chặn được cả laptop lẫn điện thoại của một người | Thêm bảng, trạng thái, hub | Web Lock đủ cho trường hợp hay gặp là hai tab |

## Hệ quả

- Đo lại: từ trùng 114,4 % → 35,7 % (bộ chính), 103,5 % → 30,1 % (bộ kiểm chứng), **không mất lời thật**, lời chủ micro giữ
  nguyên (0,955 → 0,957). Phần còn lại phần lớn là hai laptop cách đều người nói.
- Câu đã sửa tay không được so (số từ không còn khớp với các đoạn của worker). Câu lưu trước bản này không có đoạn nên không
  được đánh dấu.
- Đọc tăng dần (`since`) trả thêm các câu cũ gần câu vừa đổi, vì dấu của câu cũ đổi khi bản to hơn đến sau.
- Âm thanh câu chưa gửi nằm trên máy người nói tối đa 8 ngày (giọng của chính họ). Thoát chế độ ẩn danh hoặc xoá dữ liệu trang
  web thì mất.
- Mã mới `MSG351`–`MSG354`. `MSG320` nay áp cả cho cuộc họp đã kết thúc quá 7 ngày.
- Chưa làm như eCab: chỗ ghi ở server (laptop và điện thoại của cùng một người), phân xử bằng giọng mẫu, gộp hay tách câu,
  chạy lại một câu (không lưu âm thanh nên không chạy lại được).

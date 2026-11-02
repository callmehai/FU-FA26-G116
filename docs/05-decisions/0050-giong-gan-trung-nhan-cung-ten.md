# ADR-0050: Giọng gần như trùng giọng đã nhận ra thì nhận cùng tên

- **Trạng thái:** Đề xuất — SCRUM-35, PO 03/11/2026
- **Ngày:** 2026-11-03
- **Người quyết định:** Hải (PO)
- **Bổ sung:** [ADR-0045](0045-may-tu-gan-ten-khi-giong-khop-ro.md)

## Bối cảnh

Cuộc họp thử trên server test ngày 03/11/2026, ghi bằng mic phòng:

1. Lượt 1: Hải hát. Bản gỡ băng có 3 đoạn, đều đúng tên Trần Việt Hải.
2. Lượt 2: Hải hát thêm một bài. Mic phòng chạy lại cả băng (như AI-eCabinet, PO 01/11). Lần chạy lại tách giọng
   Hải thành 5 giọng. Chỉ giọng chính (58 giây, khớp giọng mẫu 0,785) được gợi ý và tự gán. Các giọng còn lại thành
   "Người nói 2…5", kể cả những đoạn của lượt 1 trước đó đã đúng.

Chạy lại bản ghi đó trên server, độ giống giữa các giọng:

| Giọng | Thời lượng | Giống giọng chính |
|---|---|---|
| SPEAKER_01 | 6 giây | 0,807 |
| SPEAKER_02 | 11 giây | 0,834 |
| SPEAKER_03 | 12 giây | 0,848 |
| SPEAKER_04 ("Đây.") | 3 giây | 0,465 |

Ba giọng đầu là Hải. Máy chuyển chữ chỉ gộp hai giọng từ 0,85 (`MERGE_THRESHOLD`). Giọng hát lại khớp kém với giọng
mẫu đọc, nên các giọng phụ không qua được ngưỡng gợi ý (0,6) hoặc ngưỡng giọng thứ hai (0,7, ADR-0045).

## Quyết định

Sau bước gợi ý như cũ, mỗi giọng còn chưa có tên được so với giọng đầu tiên của từng người đã được gợi ý. Giống từ
`VoiceId:InheritVoiceThreshold` (0,8) trở lên thì giọng đó nhận người đó, cùng điểm của giọng đầu. Điểm so với giọng
mẫu không còn quyết định ở bước này.

Giọng đầu được máy tự gán (điểm ≥ 0,75) thì các giọng nhận theo cũng được tự gán, như ADR-0045.

## Vì sao 0,8

- Hai người khác nhau giống nhau nhiều nhất 0,745 (server test, 31/10/2026, lý do của ADR-0045) và 0,73 (bộ đánh giá
  `workers/speech/eval`).
- Một người bị tách giống nhau 0,81–0,85 (ở trên) và 0,94 (một cuộc họp trước đó). Một trường hợp 0,777 vẫn nằm dưới
  0,8 và giữ cách làm cũ (gợi ý nếu khớp giọng mẫu ≥ 0,7).
- Không chạy lại được bộ đánh giá VIVOS: dữ liệu đã bị xoá khỏi máy. `evaluate.py --inherit` đã có để đo khi dựng lại
  bộ dữ liệu.

## Hệ quả

- Một người bị máy tách làm nhiều giọng thì mọi giọng mang cùng tên, Thư ký không phải gán từng giọng.
- Nếu một ngày hai người khác nhau giống nhau từ 0,8, người sau sẽ bị gợi ý (và có thể tự gán) nhầm tên người trước.
  Thư ký vẫn sửa được; tên máy gán có ghi chú khi rê chuột.
- Không đổi máy chuyển chữ: số giọng trong danh sách người nói giữ nguyên, chỉ tên được điền.

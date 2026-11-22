# ADR-0074: Ngưỡng tách người nói và Voice ID sau buổi thử 21/11/2026

- **Trạng thái:** Đề xuất — buổi thử 21/11/2026 (mục 13 của `tests/ai/out/meeting-2111/BUGS.md`)
- **Ngày:** 2026-11-22
- **Người quyết định:** chờ PO duyệt
- **Thay:** việc hạ mọi ngưỡng `VoiceId:*` xuống 0,5 (biên 0,05) ngày 21/11/2026 (PR #463, không có ADR). Đổi ngưỡng tự gán
  0,75 của [ADR-0045](0045-may-tu-gan-ten-khi-giong-khop-ro.md) thành 0,7. Giữ ngưỡng 0,8 của
  [ADR-0050](0050-giong-gan-trung-nhan-cung-ten.md).

## Bối cảnh

Buổi thử ngày 21/11/2026 có hai bản ghi mic phòng:

1. *test mic* (15 phút, 7 lượt): Hải, Lộc, Trường Huy, Khánh ngồi cùng phòng, có tiếng quán cà phê. Ngưỡng lúc đó là
   0,6 / 0,75 / 0,7 / 0,7 / 0,8. Máy ra **41 "Người nói N"** cho 4 người.
2. *họp kết hợp* (9 phút, 7 lượt): Hải, Lộc, Khánh cùng phòng, Nguyễn Quang Huy nói qua loa laptop. Ngưỡng đã hạ hết xuống
   0,5. Máy ra 15 giọng, và **tên Hải nhận 7 giọng máy**.

Hạ ngưỡng Voice ID không chữa được nguyên nhân: máy tách người nói (worker) gom các khung giọng ở ngưỡng 0,5, được chỉnh
trên giọng tổng hợp. Trong một phòng thật, mọi người nói qua cùng một micro, nên giọng người này gần giọng người kia hơn
hẳn, và một người bị cắt thành nhiều giọng nhỏ. Hạ ngưỡng gợi ý xuống 0,5 chỉ làm các giọng nhỏ đó nhận tên của ai khớp
nhỉnh hơn một chút. Đo lại: có giọng chỉ khớp Trường Huy 0,53 và Hải 0,51, cách nhau 0,02. Giọng mẫu của nhóm còn giống
nhau 0,67–0,71 (đa số thu hộ bằng cùng micro phòng họp), nên người đứng nhì thường sát người đứng đầu.

## Đo

Hai bản ghi được chép về máy dev vào thư mục tạm, xoá sau khi đo, không đưa vào repo. Máy tách người nói chạy bằng chính
code worker, bỏ bước chuyển chữ. Điểm so với giọng mẫu tính **trong container app trên server**: khoá giải mã không rời
container, chỉ điểm cosine được in ra. Ở ngưỡng cũ, kết quả khớp đúng server: 41 và 15 giọng.

**Máy tách người nói** (gom nhóm / gộp lần hai):

| Ngưỡng | *test mic*: số giọng (≥ 10 giây) | *họp kết hợp*: số giọng (≥ 10 giây) | Ghi chú |
|---|---|---|---|
| 0,5 / 0,85 (cũ) | 41 (14) | 15 (3) | |
| 0,6 / 0,8 | 19 (8) | 8 (3) | |
| 0,7 / 0,8 | 14 (7) | 6 (5) | *test mic* có một giọng trộn lời Lộc và Hải |
| **0,72 / 0,8** | **12 (6)** | **5 (4)** | mỗi giọng một người |
| 0,75 / 0,8 | 10 (6) | 4 (4) | mỗi giọng một người |
| 0,78 / 0,8 | 9 (6) | 3 (3) | Khánh bị gộp vào Hải |
| 0,8 / 0,85 | 9 (7) | 4 (4) | Khánh bị gộp vào Hải |

**Gợi ý tên** (`SpeakerMatcher`):

| Máy tách người nói | Ngưỡng gợi ý / biên / cùng giọng / giọng thứ hai / nhận theo | *test mic*: giọng có tên, nhiều nhất một người | *họp kết hợp*: giọng có tên, nhiều nhất một người |
|---|---|---|---|
| 0,5 / 0,85 (cũ) | 0,6 / 0,05 / 0,7 / 0,7 / 0,8 | 15 trong 41, Hải 6 | 7 trong 15, Hải 3 |
| 0,5 / 0,85 (cũ) | 0,5 cả năm | 39 trong 41, Hải 22 | 12 trong 15, **Hải 7** |
| **0,72 / 0,8** | **0,6 / 0,05 / 0,7 / 0,7 / 0,8** | **5 trong 12 (88% thời gian nói), Trường Huy 2** | **3 trong 5 (97%), mỗi người 1** |
| 0,72 / 0,8 | 0,5 cả năm | 11 trong 12, Trường Huy 4 | 4 trong 5, Hải 2 |

Người trong phòng khớp giọng mẫu 0,73–0,88. Mọi giọng khác (giọng nhỏ, tiếng quán, Quang Huy qua loa laptop) cao nhất 0,66.
Không lần nào tên Trường Huy, người vắng ở *họp kết hợp*, được gợi ý. Giọng của Quang Huy qua loa laptop chỉ khớp giọng mẫu
của chính anh 0,39–0,40, nên không được gợi ý ai: đúng, vì tên sai còn tệ hơn "Người nói N".

## Quyết định

1. Worker: `DEFAULT_CLUSTER_THRESHOLD` 0,5 → **0,72**, `MERGE_THRESHOLD` 0,85 → **0,8**.
   - Từ 0,78 trở lên, Khánh và Hải (giống nhau 0,73 trong phòng đó) thành một giọng. 0,75 cho ít giọng hơn mà vẫn đúng
     người, nhưng sát chỗ gộp nhầm; 0,72 chừa một khoảng.
   - Hai nửa của cùng một người giống nhau 0,75–0,82; hai người khác nhau cao nhất 0,745 (31/10). Gộp lần hai ở 0,8.
   - `SPEECH_CLUSTER_THRESHOLD` vẫn đổi được mà không sửa code.
2. Backend `VoiceId:*`: `SuggestThreshold` 0,6, `SuggestMargin` 0,05, `SameVoiceThreshold` 0,7, `SecondVoiceThreshold` 0,7,
   `InheritVoiceThreshold` 0,8, như các số đo VIVOS và ADR-0045/0050. `AutoAssignThreshold` **0,7** (trước là 0,75): Hải ở
   *test mic* chỉ khớp 0,73, giọng không phải người trong phòng cao nhất 0,66 (21/11) và 0,645 (31/10), VIVOS không gợi ý
   sai lần nào từ 0,6.

## Hệ quả

- Phòng 4 người ra khoảng 6 giọng có lời đáng kể, thay vì 40. Các giọng nhỏ còn lại phần lớn chỉ có câu ngắn và bị bỏ
  theo luật câu ngắn (PR #467). Một người bị tách đôi dưới 0,8 vẫn hiện hai giọng; Thư ký gán cùng tên là gộp lại.
- Ít giọng được gán tên hơn so với 0,5, nhưng tên nào được gán thì khớp rõ. Giọng không khớp rõ giữ "Người nói N".
- Hai bản ghi, mười mấy phút: chưa đủ để chắc chắn. Ngưỡng gom nhóm không đổi đều theo một chiều (0,65 cũng gộp Khánh vào
  Hải, 0,7 thì không). Cần đo lại sau buổi thử kế tiếp, bằng cùng cách: bản ghi phòng, điểm tính trong container app.
- Bản gỡ băng cũ giữ nguyên. Chạy lại chuyển chữ (`POST /api/recordings/{id}/transcribe`) thì dùng ngưỡng mới.

## Nghĩa vụ theo Luật 91/2025/QH15

- Không đổi dữ liệu nào được lưu: vector của giọng trong bản ghi vẫn không lưu, chỉ gợi ý được lưu.
- Lần đo dùng bản ghi của chính nhóm, ở thư mục tạm trên máy dev, xoá sau khi đo; vector giọng mẫu chỉ được giải mã trong
  container app trên server, không ra ngoài.

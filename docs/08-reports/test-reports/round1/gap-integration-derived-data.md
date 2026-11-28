# Khoảng trống test tích hợp: dữ liệu dẫn xuất sau khi nguồn thay đổi

Ngày 2026-11-28. Phát sinh từ bug [SCRUM-284](https://sep490g116.atlassian.net/browse/SCRUM-284): xóa cả một lượt ghi âm trong phòng, nhưng chữ và "Người nói n" của lượt đó vẫn nằm trong bản ghi lời và trong đầu vào của dự thảo AI. Tài liệu này trả lời: còn loại case nào có thể thiếu, trên toàn dự án.

Phạm vi và mức chắc chắn: đây là kết quả đọc tên test, `api.json` và mã nguồn, **chưa chạy từng kịch bản**. Mục nào ghi "cần xác minh" là nghi vấn, chưa phải bug.

## 1. Lớp lỗi

Các case Round 1 được viết theo từng endpoint và từng nhánh mã: một hành động, một kết quả. Bug này nằm ở chỗ khác: **hành động A thay đổi nguồn, màn B hoặc đầu ra C đọc lại dữ liệu đã dẫn xuất từ nguồn đó**. Cụ thể ở đây: xóa lượt (FE-09) → bản ghi lời, `transcript.txt`, dự thảo AI (FE-10), biên bản (FE-12).

Câu hỏi bổ sung cho mọi chức năng xóa, loại, sửa, hủy, rút, vô hiệu hóa: "ai đọc lại dữ liệu này, và họ có còn thấy bản cũ không?"

## 2. Cái đã có

| Thay đổi nguồn | Đầu ra kiểm tra | Test hiện có |
|---|---|---|
| Xóa một câu | Dự thảo hiện "đã cũ" | `SummaryEndpointTests.Deleting_a_sentence_after_the_draft_makes_the_draft_read_as_behind` |
| Xóa cả bản ghi | Dự thảo hiện "đã cũ" | `…Erasing_a_recording_after_the_draft_makes_the_draft_read_as_behind` |
| Sửa một dòng khi AI đang viết | Dự thảo hiện "đã cũ" | `…A_line_corrected_while_the_AI_writes…` |
| Loại một đoạn mic riêng | Dự thảo không đọc đoạn đó | Case đoạn bị loại trong `SummaryEndpointTests.cs` (quanh dòng 325) |
| Làm lại bản ghi lời | Giữ chỉnh sửa và tên | `TranscriptCoverageTests.Redoing_a_finished_transcript…` |

## 3. Khoảng trống

Ưu tiên theo: xác suất xảy ra khi dùng thật, và hậu quả (dữ liệu đã xóa vẫn ra ngoài).

### Nhóm A. Cắt lượt (cùng gốc với SCRUM-284)

| # | Kịch bản | Đầu ra phải kiểm tra | Tình trạng |
|---|---|---|---|
| A1 | Xóa lượt đầu / giữa / cuối, rồi tạo dự thảo | Đầu vào AI không còn chữ lượt đó, không còn người nói chỉ có ở lượt đó | Đã có 3 case đỏ (TRANS_API_IT45–47) |
| A2 | Xóa lượt, rồi tải `transcript.txt` | Tệp không còn chữ lượt đó | Đã kiểm chứng 2026-11-28: **đỏ**, cùng lỗi SCRUM-284 (TRANS_API_IT15) |
| A3 | Có dự thảo, rồi xóa lượt | Dự thảo hiện "đã cũ" (đồng hồ `UpdatedAt` đổi) | Đã kiểm chứng 2026-11-28: **xanh** (TRANS_API_IT52), `readiness.transcriptChangedAt` tăng sau khi cắt |
| A4 | Xóa lượt khi bản ghi lời đang Pending / Processing, rồi tạo dự thảo | Chạy lại từ tệp ngắn, dự thảo không đọc bản cũ | Đã kiểm chứng 2026-11-28: **xanh** (TRANS_API_IT48). Chạy lại từ tệp ngắn cho đúng; lượt bị xóa không còn chữ |
| A5 | Dòng đã được thư ký sửa tay nằm vắt qua ranh giới lượt, rồi xóa lượt | Phần sửa tay còn đúng sau cắt. Dòng đã sửa không còn `WordStartsMs` | **Thiếu**, cần xác minh |
| A6 | Người nói đã được gán tên chỉ có ở lượt bị xóa | Tên và gợi ý đi cùng, không rò sang dự thảo | **Thiếu** |
| A7 | Cuộc họp hybrid, xóa lượt, bản ghi gộp dựng lại | Bản gộp không còn âm thanh lượt đó; tạo dự thảo vẫn đúng | **Thiếu** ở tầng tích hợp |
| A8 | Xóa lượt, rồi ghi tiếp lượt mới | Mốc thời gian lượt mới không đè lên lượt cũ | **Thiếu**, cần xác minh |
| A9 | Biên bản đã dựng từ dự thảo có chữ lượt bị xóa, rồi xóa lượt | Biên bản đã dựng giữ nguyên chữ cũ: đây là **câu hỏi cho BA**, chưa phải lỗi. Nếu giữ thì phải hiện cảnh báo | Chưa có quyết định |

### Nhóm B. Cùng khuôn "đổi nguồn, đầu ra dẫn xuất" ở chỗ khác (cần xác minh từng mục)

| # | Đổi nguồn | Đầu ra nên kiểm tra |
|---|---|---|
| B1 | Đổi tên / xóa tên người nói sau khi có dự thảo | Dự thảo hiện "đã cũ"? Tên mới ra ở dự thảo tạo lại? (đồng hồ chỉ nhìn `UpdatedAt` của bản ghi lời; gán tên có chạm nó không thì chưa kiểm) |
| B2 | Loại rồi khôi phục một đoạn mic riêng | Dự thảo, `speech-clips.txt`, bản gộp cùng khớp sau khôi phục |
| B3 | Rút đồng ý Voice ID | Mẫu giọng mất, nhãn người nói đã nhận dạng trong dự thảo và biên bản còn hay mất |
| B4 | Hết hạn lưu trữ bản ghi, xóa file | Dự thảo và bản ghi lời đi theo đúng chính sách (BR liên quan) |
| B5 | Hủy cuộc họp hoặc đổi chế độ sau khi đã có bản ghi | Bản ghi, dự thảo, thông báo không còn mở được cho người không đủ quyền |
| B6 | Vô hiệu hóa tài khoản thư ký / người được nhắc tên | Dự thảo, biên bản vẫn đọc được; không rò email |
| B7 | Sửa nghị quyết / kết quả biểu quyết sau khi có dự thảo | Kết quả trong dự thảo (sao chép từ dữ liệu) khớp bản mới sau khi tạo lại |
| B8 | Dự thảo sửa tay rồi gửi duyệt, sau đó nguồn đổi | Dự thảo đã gửi duyệt không đổi âm thầm; có cờ "nguồn đã đổi" |
| B9 | Biên bản đã ban hành rồi xóa bản ghi | Biên bản giữ, tệp xuất không dẫn về bản ghi đã xóa |
| B10 | Báo cáo (FE-15): xóa lượt / bản ghi | Số liệu thời lượng, số lượt phát biểu khớp sau khi xóa |

## 4. Tình trạng case Round 1 hiện có (đếm tự động, cần đọc kỹ trước khi trích)

- `api.json` mỗi Feature: FE-05 còn 46 dòng chứa "Pending", FE-15 còn 12, FE-06 còn 7, FE-09 còn 8, FE-04 còn 3, FE-10 còn 3 (đây là 3 case đỏ mới). Đếm theo chuỗi, **chưa tách trạng thái từng case**, nên chỉ dùng làm gợi ý nơi cần rà.
- "Manual only": FE-07 hai dòng, FE-12 một dòng. Đây là chỗ kiểm tay, không có test tự động chạy lại.

## 5. Đề xuất

1. Thêm A2, A3, A4 vào `api.json` FE-10 (hoặc FE-09 nhóm "Xóa lượt"), cùng ticket SCRUM-284, trạng thái theo kết quả chạy thật. Đây là phần rẻ nhất và cùng gốc.
2. Chạy tay B1, B2, B3 trước, vì chạm thao tác thư ký làm hằng ngày; mục nào đỏ thì mở ticket riêng.
3. Hỏi BA về A9 và B8: biên bản / dự thảo đã gửi duyệt có nên cảnh báo khi nguồn đổi.
4. Thêm cột "đầu ra dẫn xuất" vào mẫu viết case API: mỗi case xóa / loại / sửa phải ghi ai đọc lại dữ liệu đó.
5. Sau khi sửa SCRUM-284 theo hướng đầy đủ (đổi cách ghi từ lúc nhận), chạy lại toàn bộ 16 unit test cắt lượt ở `RecUnitRound1Tests` (DeleteTakeAsync/UTC005–UTC020).

## 6. Unit test vừa thêm (`RecUnitRound1Tests.cs`, nhóm DeleteTakeAsync)

Lượt bị xóa 10 000–15 000 ms. Mỗi lớp vị trí của một dòng so với lượt đó:

| Test | Lớp | Kết quả |
|---|---|---|
| UTC012 | Hoàn toàn trước lượt | Xanh |
| UTC013 | Hoàn toàn trong lượt | Xanh |
| UTC014 | Hoàn toàn sau lượt | Xanh |
| UTC015 | Vắt qua cả lượt, cần cắt đúng chữ trong lượt | **Đỏ** |
| UTC016 | Bắt đầu trong lượt, kết thúc sau (mất chữ lượt giữ lại: cả dòng bị bỏ) | **Đỏ** |
| UTC017 | Bắt đầu 1 ms trước lượt | **Đỏ** |
| UTC018 | Bắt đầu đúng đầu lượt | Xanh |
| UTC019 | Bắt đầu 1 ms trước cuối lượt (mất chữ lượt giữ lại) | **Đỏ** |
| UTC020 | Bắt đầu đúng cuối lượt | Xanh |

Ghi chú: UTC016 và UTC019 là chiều ngược của SCRUM-284: dòng bắt đầu trong lượt bị xóa nhưng kéo dài sang lượt còn lại sẽ bị bỏ cả dòng, mất luôn chữ của lượt được giữ. Cần thêm vào ticket SCRUM-284 như một điểm cùng gốc.

# ADR-0027: Voice ID có đồng ý — lưu giọng mẫu đã mã hoá, máy chỉ gợi ý tên, Thư ký quyết định

- **Trạng thái:** Đề xuất — điểm 2, phần âm thanh mẫu, thay thế bởi [ADR-0042](0042-luu-ban-ghi-giong-mau-de-nghe-lai.md)
- **Ngày:** 2026-10-23
- **Người quyết định:** PO (23/10/2026), nhóm SEP490-G116
- **Liên quan:** `SCRUM-35`, [ADR-0026](0026-tu-chay-nhan-dang-giong-noi.md); nhận xét hội đồng review
  vòng 1 (17/10/2026). Đảo ngược ranh giới *"không làm voiceprint"* trước đây của SCRUM-35, README và
  test `No_voiceprint_is_stored_anywhere`

## Bối cảnh

Trước đây nhóm chỉ định làm tách người nói **trong từng bản ghi** (*Người nói 1, 2…*) và để Thư ký gán
tên bằng tay. Nhận diện cùng một người qua nhiều cuộc họp (voiceprint) bị cắt, vì đó là dữ liệu sinh trắc
học kéo theo nghĩa vụ đồng ý và xoá riêng.

Hội đồng review vòng 1 nói thẳng: hạn chế quan trọng nhất là hệ thống chưa biết ai phát biểu. Hội đồng
đề xuất xây dựng **Voice ID cho từng người**, qua đó tách và nhận diện nhiều người nói trong cùng một file
ghi âm. Giảng viên hướng dẫn giao làm xong trong tuần 23–30/10/2026.

AI-eCabinet đã có tính năng tương tự: đồng ý riêng, giọng mẫu mã hoá, chỉ so với người trong cuộc họp,
chỉ gợi ý. Nhóm làm lại theo cùng ý tưởng trên stack của mình, không chép code.

## Quyết định

Chúng tôi sẽ làm Voice ID với năm ràng buộc, mỗi ràng buộc do dữ liệu và code giữ, không chỉ do tài liệu:

1. **Đồng ý riêng, tự nguyện, của chính người đó** (`voice_consents`). Đồng ý gắn với phiên bản thông báo
   và mục đích đã đọc. Database chỉ cho một đồng ý đang hiệu lực mỗi người (unique index lọc). Không có
   API nào thao tác trên giọng của người khác.
2. **Không lưu âm thanh mẫu, chỉ lưu vector, và vector được mã hoá** AES-256-GCM, gắn với người, id mẫu
   và mô hình. Vector các giọng trong bản ghi họp **không** lưu: dùng một lần để gợi ý rồi bỏ.
3. **Chỉ so với người trong chính cuộc họp đó** đã đồng ý, và mỗi người tối đa một giọng mỗi bản ghi.
   Chỉ gợi ý khi đủ gần (ngưỡng) **và** rõ hơn người xếp sau (khoảng cách). Gần sát nhau thì để
   *Người nói N*.
4. **Máy chỉ gợi ý, Thư ký quyết định** (`TranscriptSpeaker.SuggestedUserId` khác `AssignedUserId`).
   Đúng nguyên tắc AI trong luồng biên bản: AI tạo bản nháp, người chịu trách nhiệm.
5. **Rút đồng ý là xoá ngay**, trong một transaction và dưới cùng khoá dòng mà việc thêm mẫu dùng: mọi
   giọng mẫu và mọi gợi ý trỏ tới người đó. Tên Thư ký đã gán giữ lại.

Họp trực tuyến (SCRUM-44) không cần Voice ID: trình duyệt mỗi người tự ghi micro của chính mình, nên người
nói được biết chắc từ tài khoản.

Test `No_voiceprint_is_stored_anywhere` được thay bằng `A_voice_is_stored_only_as_an_encrypted_enrolment_sample`.
Test mới vẫn hỏng nếu ai đó thêm một mảng số thực hay một cột *Embedding* ở bất kỳ bảng nào ngoài
`voice_samples`.

## Phương án đã cân nhắc

| Phương án | Ưu | Nhược | Vì sao không chọn |
|---|---|---|---|
| Giữ như cũ: chỉ tách người nói, gán tay | Không có dữ liệu sinh trắc học | Không đáp ứng yêu cầu của hội đồng | Hội đồng yêu cầu rõ Voice ID |
| Tự động gán tên khi đủ chắc | Thư ký đỡ một thao tác | Máy nhầm thì biên bản ghi sai người phát biểu mà không ai quyết định | Biên bản là văn bản có giá trị pháp lý, phải có người chịu trách nhiệm |
| So với mọi người đã đăng ký trong hệ thống | Nhận ra cả khách không có trong danh sách | Mở rộng mục đích xử lý ra ngoài cuộc họp người đó tham dự; dễ nhầm hơn khi nhiều người | Trái với mục đích đã nêu khi xin đồng ý |
| Lưu cả âm thanh mẫu để tính lại khi đổi mô hình | Đổi mô hình không phải ghi lại | Giữ nhiều dữ liệu sinh trắc học hơn cần | Thu thập tối thiểu; ghi lại 2–3 mẫu chỉ mất một phút |
| Dùng pgvector để so trong database | Nhanh với hàng nghìn người | Vector phải để dạng đọc được trong database | Số người mỗi cuộc họp nhỏ; giải mã rồi so trong bộ nhớ đủ nhanh, và giữ được mã hoá |

## Hệ quả

- Có màn **Giọng nói của tôi** để đồng ý, ghi mẫu, xoá mẫu và rút đồng ý. Tab Bản ghi hiện gợi ý tên kèm
  độ tin cậy, Thư ký bấm xác nhận hoặc chọn người khác.
- Thêm ba bảng: `voice_consents`, `voice_samples`, `transcript_speakers` (migration
  `VoiceIdConsentSamplesAndSpeakers`).
- Server phải đặt `VoiceId:EncryptionKey`. Đổi khoá thì giọng mẫu cũ không dùng được, người dùng ghi lại.
- Ngưỡng gợi ý mặc định là 0,6 / 0,1. Ngưỡng này đo trên giọng thật của corpus VIVOS (`workers/speech/eval/`).
  Ở 0,5, máy gợi ý sai người cho 3% thời gian nói. Ở 0,6, không gợi ý sai lần nào, và 97–98% lời của người
  đã đăng ký vẫn được gợi ý đúng tên. Nhóm sẽ đo lại bằng giọng của chính nhóm, và đưa
  tỉ lệ gợi ý đúng, gợi ý sai, bỏ sót vào báo cáo cho hội đồng.
- Chưa làm: màn quản trị cho người phụ trách bảo vệ dữ liệu (xem danh sách đồng ý, rút thay khi có đơn
  giấy), và nhận diện khách mời (khách chưa có tài khoản để đồng ý, chờ SCRUM-59).

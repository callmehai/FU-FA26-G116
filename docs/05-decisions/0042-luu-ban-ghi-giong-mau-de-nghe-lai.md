# ADR-0042: Lưu bản ghi giọng mẫu đã mã hoá để chính chủ nghe lại, như AI-eCabinet

- **Trạng thái:** Đề xuất — SCRUM-35, PO 29/10/2026
- **Ngày:** 2026-10-29
- **Người quyết định:** Hải (PO)
- **Thay:** điểm 2 "Không lưu âm thanh mẫu, chỉ lưu vector" của [ADR-0027](0027-voice-id-co-dong-y.md), ở phần âm
  thanh. Vector vẫn lưu và mã hoá như cũ.
- **Theo:** AI-eCabinet `origin/main`: `modules/voiceprints/internal/samples.go` (mẫu lưu kèm bản ghi đã niêm
  phong, `Playable`), `clients/web/.../voiceprint/samples/SamplesCard.tsx` (nghe lại từng mẫu, chỉ chủ mẫu)

## Bối cảnh

Người dùng ghi giọng mẫu bằng cách đọc một đoạn văn. Sau khi ghi xong, màn "Giọng nói của tôi" chỉ hiện
độ dài và ngày ghi. Người dùng không biết mẫu nào rõ, mẫu nào lẫn tiếng ồn, nên không biết nên giữ mẫu
nào, xoá mẫu nào. Một mẫu kém làm Voice ID gợi ý sai ở mọi cuộc họp sau.

AI-eCabinet giữ bản ghi của từng mẫu, niêm phong, và cho chính chủ nghe lại.

Màn đồng ý của TrustCabinet (thông báo 1.0) đang hứa "không lưu âm thanh bạn ghi". Lưu bản ghi mà không
đổi lời hứa đó là xử lý dữ liệu sinh trắc học ngoài phạm vi người dùng đã đồng ý.

## Quyết định

1. **Lưu bản ghi giọng mẫu, mã hoá.**
   - Cột `voice_samples.audio_ciphertext`: AES-256-GCM, cùng khoá với vector. AAD là người và id mẫu, khác
     AAD của vector.
   - `audio_content_type` giữ loại tệp đã gửi, để phát lại đúng như ghi.
   - Xoá mẫu, hoặc rút đồng ý (xoá mọi mẫu), là xoá luôn bản ghi.
2. **Chỉ chính chủ nghe.** `GET /api/me/voice/samples/{id}/audio` chỉ đọc mẫu của người đang đăng nhập.
   Người khác, kể cả Thư ký hay quản trị, nhận cùng một `404`. Không có route nào đọc bản ghi mẫu của
   người khác.
3. **Đổi thông báo lên 1.1.**
   - Câu "Lưu gì" thành: bản ghi giọng mẫu và đặc trưng giọng, cả hai đã mã hoá, chỉ bạn nghe lại được bản
     ghi mẫu.
   - Bản ghi chỉ được lưu khi người đó đã đồng ý thông báo 1.1 trở lên. Mẫu ghi dưới đồng ý 1.0 chỉ có
     vector, như lời hứa lúc đó.
   - Người đã đồng ý 1.0 thấy dòng "Điều khoản mới" và nút "Đồng ý điều khoản mới". Bấm là gia hạn chính dòng
     đồng ý đó lên 1.1 (mẫu cũ giữ nguyên), nhật ký ghi `voice.consent_renewed` kèm hai phiên bản.

## Nghĩa vụ theo Luật 91/2025/QH15

- **Đồng ý đúng với điều được xử lý** (Điều 9): bản ghi chỉ được lưu dưới thông báo nói rõ điều đó; đồng
  ý cũ không bị coi là đồng ý cho điều mới.
- **Thu thập tối thiểu** (Điều 3): bản ghi chỉ phục vụ việc chính chủ nghe lại để chọn mẫu tốt, không dùng
  để nhận diện; nhận diện vẫn chỉ dùng vector.
- **Bảo mật dữ liệu sinh trắc học** (Điều 31 khoản 4): mã hoá khi lưu, chỉ backend giải mã, chỉ trả cho
  chính chủ, không ghi bộ nhớ đệm (`Cache-Control: no-store`).
- **Xoá khi rút đồng ý**: không đổi, bản ghi mẫu nằm trong dòng mẫu nên bị xoá cùng.

## Hệ quả

- Người dùng nghe lại mẫu mới ghi, giữ mẫu rõ và xoá mẫu kém.
- Mỗi mẫu tốn thêm tối đa 10 MB trong database (thường dưới 1 MB cho 20–30 giây).
- Mẫu ghi trước 1.1 không nghe lại được; muốn nghe thì ghi lại.
- Đổi khoá mã hoá thì bản ghi mẫu cũ cũng không giải mã được, như vector.

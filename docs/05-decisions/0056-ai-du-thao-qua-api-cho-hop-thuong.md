# ADR-0056: Quản trị chọn máy AI viết dự thảo — máy local hoặc API, chỉ cuộc họp Thường được gửi ra API

- **Trạng thái:** Chấp nhận — PO (Hải) đề xuất 06/11/2026, chấp nhận 14/11/2026; bổ sung, không thay [ADR-0054](0054-ai-du-thao-bien-ban-chay-tai-cho.md)
- **Ngày:** 2026-11-06
- **Người quyết định:** Hải (PO)
- **Liên quan:** SCRUM-36 (dự thảo AI); SCRUM-186 (chế độ API); [ADR-0054](0054-ai-du-thao-bien-ban-chay-tai-cho.md) (AI chạy tại chỗ);
  Luật Bảo vệ dữ liệu cá nhân 91/2025/QH15

## Bối cảnh

ADR-0054 cho AI chạy tại chỗ: model 7B trên máy một thành viên, server gọi qua Tailscale. Đo ngày 05–06/11/2026 trên
Mac M1 16 GB: viết ra khoảng 8–9 token/giây, dự thảo một cuộc họp đầy đủ mất khoảng 5 phút, họp dài khoảng 8 phút, và máy
đó phải thức thì server mới tạo được dự thảo.

Nhóm có key OpenCode Zen gọi được DeepSeek V4.1 Flash qua API chuẩn OpenAI (`https://opencode.ai/zen/v1`), nhanh hơn nhiều
và không phụ thuộc máy của ai. Đổi hẳn sang API thì lời thoại mọi cuộc họp ra máy chủ nước ngoài: lời thoại là dữ liệu cá
nhân, và cuộc họp Mật, Tối mật còn có thông tin không được đưa ra ngoài.

## Quyết định

1. **Màn "Cấu hình AI" cho quản trị** (`/mgmt/admin/ai`, capability `AdminUsers`): chọn **máy local** (như ADR-0054) hoặc
   **API**; nhập địa chỉ API (bắt buộc https), tên model và key; nút **Thử kết nối** gọi API một lần mà không lưu gì.
2. **Chỉ cuộc họp Thường (`Sensitivity.Normal`) được gửi ra API.** Cuộc họp Nội bộ, Mật, Tối mật luôn dùng máy local, dù
   quản trị chọn gì. Máy local tắt thì những cuộc họp đó báo máy AI chưa sẵn sàng (`503 MSG394`), không bao giờ chuyển sang
   API. Việc chọn máy diễn ra lúc job chạy, nên cuộc họp đổi sang Mật sau khi đã xếp hàng vẫn ở lại máy local.
3. **Key nhập trên màn hình, lưu mã hoá** trong bảng `ai_provider_settings` (một dòng): AES-256-GCM, khoá là
   `Ai:EncryptionKey` nếu có, không thì suy ra từ khoá ký token cho riêng mục đích này (như Voice ID). Server không bao giờ
   trả key về trình duyệt, không ghi vào log hay nhật ký kiểm toán; màn hình chỉ biết có key hay không và 4 ký tự cuối.
   Đổi khoá ký thì key cũ không giải mã được: server dùng máy local và quản trị nhập lại key.
4. **Máy local vẫn cấu hình như cũ** (`Ai:BaseUrl`, `Ai:Model`); màn hình chỉ hiện tên model và đã cấu hình hay chưa.
5. **Nhật ký kiểm toán** `ai_settings.updated`: chế độ, địa chỉ, model, và key được giữ / thay / xoá.
6. **Cách gọi API riêng, máy local giữ nguyên.** Máy local vẫn cắt lời thoại thành đoạn 9.000 ký tự cho context 8K của model
   7B. Qua API (DeepSeek V4.1 Flash đọc được 1 triệu token): tắt phần suy luận (`reasoning_effort: none`), mỗi mục chương
   trình đọc cả lời thoại trong một lần, tối đa 3 mục viết song song, `429` thì thử lại. Cách gửi cả cuộc họp trong một lần như
   AI-eCabinet đã thử và bỏ: đo trên 8 cuộc họp mẫu nó không nhanh hơn (67,1 so với 65,9 giây, cùng 33/33 ý), lại phụ thuộc
   việc model trả lời đúng khuôn; viết theo từng mục giữ nguyên prompt đã chỉnh trên máy local.

## Phương án đã cân nhắc

| Phương án | Ưu | Nhược | Kết luận |
|---|---|---|---|
| Một công tắc cho mọi cuộc họp | Đơn giản nhất | Lời thoại họp Mật ra nước ngoài | Không chọn |
| Đang dùng API thì cấm tạo dự thảo cho họp Mật | Không cần máy local | Họp Mật mất tính năng dự thảo | Không chọn |
| Key trong biến môi trường server, màn hình chỉ bật/tắt | Không lưu bí mật trong DB | Đổi key phải vào server | Không chọn — PO muốn đổi key trên màn hình |

## Hệ quả

- Cuộc họp Thường có dự thảo trong vài giây đến vài chục giây, không cần máy của thành viên nào thức.
- Lời thoại cuộc họp Thường đi tới nhà cung cấp API. Model miễn phí của OpenCode Zen có thể được dùng để cải thiện model;
  cần ghi điều này vào thông báo ghi âm trước khi dùng API với dữ liệu thật. Server test hiện chỉ có dữ liệu giả.
  Từ 14/11/2026 phần chi tiết (i) của thông báo ghi âm trước khi vào phòng nói rõ: họp Thường thì lời thoại (chữ, không có
  tiếng) có thể được gửi tới dịch vụ AI bên ngoài; họp Nội bộ, Mật thì dự thảo do máy AI của nhóm viết. Khách không được
  biết độ mật nên đọc chính quy tắc đó.
- Thêm một bảng, một migration (`AiProviderSetting`) và mã `MSG408`–`MSG413`.

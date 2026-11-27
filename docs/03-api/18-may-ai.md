# Máy AI viết dự thảo — cấu hình server, không có endpoint

Mọi dự thảo AI ([17-du-thao-bien-ban.md](17-du-thao-bien-ban.md)) do **máy AI của nhóm** viết, với mọi cuộc họp và mọi
mức mật ([ADR-0054](../05-decisions/0054-ai-du-thao-bien-ban-chay-tai-cho.md),
[ADR-0088](../05-decisions/0088-bo-cau-hinh-ai-chi-dung-may-ai-cua-nhom.md)). Máy là một server chuẩn OpenAI
(`POST {Ai:BaseUrl}/chat/completions`: Ollama, llama.cpp `llama-server`, LM Studio, vLLM) trên máy của thành viên, server
gọi qua Tailscale. Lời thoại không ra khỏi hệ thống của nhóm.

- Cấu hình bằng các khoá `Ai:*` ở [17-du-thao-bien-ban.md § Cấu hình máy AI](17-du-thao-bien-ban.md#cấu-hình-máy-ai).
  Không có màn hình hay endpoint nào đổi máy viết dự thảo.
- `Ai:BackupBaseUrl` đặt thì có **máy dự phòng** ([ADR-0063](../05-decisions/0063-may-ai-du-phong.md)): máy chính không
  nhận kết nối trong `Ai:ConnectTimeoutSeconds` (10 giây) thì lời gọi chuyển sang máy dự phòng, và 2 phút sau mới hỏi lại
  máy chính. Cả hai không kết nối được thì dự thảo lỗi với `Không kết nối được máy AI chính lẫn máy dự phòng. Cả hai có thể
  đang tắt.`
- `aiReady` của dự thảo là `false` khi server chưa đặt `Ai:BaseUrl` và `Ai:Model`; khi đó yêu cầu dự thảo trả
  `503 MSG394` `Máy AI chưa sẵn sàng. Vui lòng thử lại sau.` Máy đã cấu hình mà đang tắt thì yêu cầu vẫn vào hàng chờ và
  dự thảo lỗi kèm lý do.
- Máy trả lỗi thì lý do kèm lời của máy nếu đọc được (`Máy AI trả lỗi 404. Máy AI báo: model "…" not found`, tối đa 200
  ký tự). Model suy luận (Qwen 3, DeepSeek R1) dùng hết `Ai:MaxTokens` trước khi trả lời thì lỗi là `Máy AI dùng hết …
  token khi đang suy luận, chưa kịp trả lời.`; phần `<think>` trong câu trả lời bị bỏ.

## Đã bỏ

Màn Cấu hình AI của quản trị (`GET`, `PUT /api/admin/ai`, `POST /api/admin/ai/test`, ADR-0056) cho cuộc họp Thường viết
dự thảo qua API bên ngoài; bỏ ngày 27/11/2026 cùng bảng `ai_provider_settings` (migration `RemoveAiProviderSetting`), sự
kiện kiểm toán `ai_settings.updated` và các khoá `Ai:EncryptionKey`, `Ai:ApiMaxTokens`, `Ai:ApiReasoningEffort`,
`Ai:ApiParallelism`. Mã `MSG408`–`MSG413` ngừng dùng, không tái cấp.

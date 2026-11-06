# Máy AI viết dự thảo — `/api/admin/ai`

[ADR-0056](../05-decisions/0056-ai-du-thao-qua-api-cho-hop-thuong.md). Quản trị chọn máy viết dự thảo AI
([17-du-thao-bien-ban.md](17-du-thao-bien-ban.md)): **máy local** cấu hình bằng `Ai:BaseUrl`, `Ai:Model` (ADR-0054), hoặc
một **API chuẩn OpenAI** (`POST {apiBaseUrl}/chat/completions`, ví dụ OpenCode Zen `https://opencode.ai/zen/v1`).

- **Chỉ cuộc họp Thường gửi ra API.** Nội bộ, Mật, Tối mật luôn dùng máy local; `aiReady` của dự thảo và lỗi `503 MSG394`
  tính theo máy mà cuộc họp đó được dùng.
- Mọi endpoint cần capability `AdminUsers` (token và database): chưa đăng nhập `401`, thiếu quyền `403`.
- Key gửi lên và **không bao giờ trả về**; server lưu mã hoá AES-256-GCM, chỉ trả `hasApiKey` và 4 ký tự cuối.

| Method | Đường dẫn | Kết quả |
|---|---|---|
| `GET` | `/api/admin/ai` | `200` cấu hình hiện hành |
| `PUT` | `/api/admin/ai` | `200` `MSG408` `{ code, message, settings }` |
| `POST` | `/api/admin/ai/test` | `200` `MSG412` `{ code, message, model, elapsedMs }`; không lưu gì |

## Cấu hình (`GET`, `settings`)

```json
{
  "mode": "Api",
  "apiBaseUrl": "https://opencode.ai/zen/v1",
  "apiModel": "deepseek-v4.1-flash-free",
  "hasApiKey": true,
  "apiKeyHint": "abcd",
  "localModel": "qwen2.5:7b-instruct",
  "localConfigured": true,
  "updatedAt": "2026-11-06T16:00:00+00:00",
  "updatedByName": "Phạm Quản Trị"
}
```

Chưa lưu lần nào thì `mode` là `Local`, các trường API rỗng, `updatedAt` `null`.

## Lưu (`PUT`) và gọi thử (`POST …/test`)

Body: `{ mode, apiBaseUrl, apiModel, apiKey }`.

- `mode`: `Local` hoặc `Api` (đúng chữ hoa thường).
- `apiBaseUrl`: `https://…`, tối đa 500 ký tự, không chứa user/password; bắt buộc khi `mode` là `Api`. Chọn `Local` thì
  địa chỉ và model vẫn được giữ cho lần sau.
- `apiModel`: không khoảng trắng, tối đa 100 ký tự; bắt buộc khi `Api`.
- `apiKey`: `null` giữ key đã lưu, `""` xoá key, chuỗi khác (8–500 ký tự, không khoảng trắng) thay key. Chọn `Api` thì
  phải có key (mới hoặc đã lưu) và không được xoá.
- Gọi thử dùng key trong body, không có thì dùng key đã lưu; gửi một câu ngắn, chờ tối đa 1 phút.

## Lỗi

| Mã | HTTP | Khi nào |
|---|---|---|
| `MSG409` | 400 | Dữ liệu sai; `errors` theo trường `mode`, `apiBaseUrl`, `apiModel`, `apiKey` |
| `MSG410` | 500 | Lỗi database khi đọc |
| `MSG411` | 503 | Lỗi database khi lưu |
| `MSG413` | 502 | Gọi thử thất bại; `message` là lý do: `API AI từ chối key (401).`, `API AI đòi thanh toán (402)…` (model trả phí, tài khoản hết tiền), `…giới hạn số lần gọi (429)…`, không kết nối được, API trả lỗi khác |

Mỗi lần lưu ghi nhật ký kiểm toán `ai_settings.updated` (chế độ, địa chỉ, model, key `kept`/`replaced`/`removed`), không
có key.

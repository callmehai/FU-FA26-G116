# Cấu hình AI viết dự thảo — `/api/admin/ai`

[ADR-0056](../05-decisions/0056-ai-du-thao-qua-api-cho-hop-thuong.md). Quản trị chọn máy viết dự thảo AI
([17-du-thao-bien-ban.md](17-du-thao-bien-ban.md)): **máy local** cấu hình bằng `Ai:BaseUrl`, `Ai:Model` (ADR-0054), hoặc
một **API chuẩn OpenAI** (`POST {apiBaseUrl}/chat/completions`, ví dụ OpenCode Zen `https://opencode.ai/zen/v1`).

- **Chỉ cuộc họp Thường gửi ra API.** Nội bộ, Mật, Tối mật luôn dùng máy local; `aiReady` của dự thảo và lỗi `503 MSG394`
  tính theo máy mà cuộc họp đó được dùng.
- Mọi endpoint cần capability `AdminUsers` (token và database): chưa đăng nhập `401`, thiếu quyền `403`.
- Key gửi lên và **không bao giờ trả về**; server lưu mã hoá AES-256-GCM, chỉ trả `hasApiKey` và 4 ký tự cuối.
- Mỗi lần gọi API gửi `User-Agent: TrustCabinet/1.0` và `x-opencode-session` (một GUID mới). OpenCode Go
  (`https://opencode.ai/inference/go/v1`, cũng nhận `https://opencode.ai/zen/go/v1`; gói trả theo tháng) từ chối lời gọi thiếu header này; OpenCode Zen
  (`https://opencode.ai/zen/v1`, trả theo số dư) và các API khác bỏ qua nó. Key Go chỉ dùng được với địa chỉ Go, key Zen
  với địa chỉ Zen: gọi nhầm thì Zen báo `402 Insufficient account funds`.
- Model suy luận (DeepSeek V4.1) tính cả phần suy luận vào `max_tokens`: API được `Ai:ApiMaxTokens` (8192), máy local
  `Ai:MaxTokens` (2048). Hết giới hạn trước khi trả lời thì dự thảo báo `API AI dùng hết … token khi đang suy luận`.
- Lời gọi API kèm `reasoning_effort` = `Ai:ApiReasoningEffort` (mặc định `none`: tắt suy luận). Bộ đo `tests/ai` với
  DeepSeek V4.1 Flash qua OpenCode Go: bật suy luận 28/33 ý, 350 giây, một cuộc họp hết token; tắt suy luận 33/33 ý
  (trừ một ý do dữ liệu mẫu đã bị gán tên giọng), 103 giây. Máy local qwen2.5 7B: 33/33, 1.082 giây (khoảng 18 phút).
- Viết qua API có cách gọi riêng ([17-du-thao-bien-ban.md](17-du-thao-bien-ban.md)): mỗi mục chương trình đọc cả lời
  thoại trong một lần và viết song song (`Ai:ApiParallelism`); máy local giữ nguyên cách cắt đoạn 9.000 ký tự cho context
  8K. Đo 07/11 trên 8 cuộc họp mẫu, thời gian từ lúc bấm tới lúc xong: 65,9 giây, họp dài 15,4 giây (trước đó 40), 33/33 ý.
  Gửi cả cuộc họp trong một lần như AI-eCabinet cũng đã thử: 67,1 giây, 33/33 ý, không nhanh hơn nên không giữ.
- Qua API, prompt có thêm một quy tắc riêng (`SummaryWriter.ApiSpellingRule`, máy local không nhận): từ tiếng Anh mà máy
  nhận dạng giọng nói chép sai thì viết lại đúng chính tả; tên riêng không chắc thì giữ nguyên. Quy tắc nêu hai kiểu chép
  sai: chép theo âm tiếng Việt ("đét lai") và chép thành chữ trông như tiếng Anh nhưng sai ("Develoment"), và nói rõ từ
  đọc lên gần trùng một từ thông dụng thì không phải tên riêng. Đo bằng `tests/ai` (case `9-noi-chem-tieng-anh` và bản gỡ
  băng thật của `Test04.mp3`, giữ ngoài repo), mỗi case 3 lần, ngày 07/11:

  | | Case mẫu (11 từ chép theo âm) | Test04.mp3 (18 từ) |
  |---|---|---|
  | Máy local qwen2.5 7B, không có quy tắc | 2/11, các từ khác bị bỏ khỏi dự thảo | 3/18 |
  | API, quy tắc chỉ nêu kiểu chép theo âm | 10–11/11 | 3–7/18: "Proction" chỉ sửa 1/4 lần |
  | API, quy tắc hiện hành | 10–11/11 | 7/18 cả 3 lần: Production, ready, upload, final luôn đúng |

  Vẫn không sửa: "UIT" (UAT), "inqui" (inquiry) và tên riêng "On Ou DINH" (One Housing), "ONE" (OneHub), "TNGC" (T&C):
  model không đủ căn cứ, và tên riêng đúng ra phải sửa ở bước gỡ băng (Whisper large-v3 nghe đúng các từ này). Bộ 8 cuộc
  họp mẫu vẫn 33/33 ý, 0 lỗi bịa.
- Khi API từ chối, `message` kèm lời của nhà cung cấp (`… Nhà cung cấp: Insufficient account funds`, tối đa 200 ký tự).

| Method | Đường dẫn | Kết quả |
|---|---|---|
| `GET` | `/api/admin/ai` | `200` cấu hình hiện hành |
| `PUT` | `/api/admin/ai` | `200` `MSG408` `{ code, message, settings }` |
| `POST` | `/api/admin/ai/test` | `200` `MSG412` `{ code, message, model, elapsedMs }`; không lưu gì |

## Cấu hình (`GET`, `settings`)

```json
{
  "mode": "Api",
  "apiBaseUrl": "https://opencode.ai/inference/go/v1",
  "apiModel": "deepseek-v4.1-flash",
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

# Dự thảo AI và bản nháp biên bản — SCRUM-36, SCRUM-32

Theo [ADR-0054](../05-decisions/0054-ai-du-thao-bien-ban-chay-tai-cho.md): làm như AI-eCabinet, gồm hai bước.

1. **Dự thảo:** AI viết 3 mục của biên bản. Chủ tọa hoặc Thư ký sửa rồi duyệt.
2. **Bản nháp biên bản:** điền mẫu Biên bản (Nghị định 30, Mẫu 1.4) từ hồ sơ cuộc họp, cộng với dự thảo hiện có, đã duyệt
   hay chưa (như `Compose` của eCab).

Mã thông báo: `MSG384`–`MSG405` ([danh mục](00-danh-muc-ma-thong-bao.md)). Kiểu dữ liệu FE:
[`frontend/src/types/summary.ts`](../../frontend/src/types/summary.ts).

## Quyền

| Ai | Dự thảo | Bản nháp biên bản |
|---|---|---|
| Chủ tọa, Thư ký hiện tại | Xem, tạo, tạo lại, sửa, duyệt | Xem, tạo, tạo lại, sửa, tải Word, in PDF |
| Người khác đọc được cuộc họp | Chỉ xem, và chỉ sau khi đã duyệt (`summary: null` trước đó) | Chỉ xem, và chỉ khi đã ban hành (SCRUM-33, chưa làm) |
| Không đọc được cuộc họp | `404` | `404` |

- Tạo dự thảo và tạo biên bản chỉ được làm khi cuộc họp đang diễn ra hoặc đã kết thúc (`MSG386`, `MSG398`).
- Server kiểm tra lại quyền ở mọi request.

## Dự thảo — `/api/meetings/{id}/summary`

| Method | Đường dẫn | Kết quả |
|---|---|---|
| `GET` | `/api/meetings/{id}/summary` | `200` `SummaryState` |
| `POST` | `/api/meetings/{id}/summary` | `202` `MSG384` + `SummaryState`. Đưa vào hàng chờ; dự thảo cũ vẫn đọc được tới khi bản mới xong |
| `PUT` | `/api/meetings/{id}/summary` | `200` `MSG388`. Body `{ objectives, discussion, conclusions }`. Bỏ trạng thái đã duyệt |
| `POST` | `/api/meetings/{id}/summary/review` | `200` `MSG391`. Từ đây người dự họp khác xem được |

```jsonc
// SummaryState
{
  "summary": {
    "status": "Queued | Running | Ready | Failed",
    "draft": {
      "objectives": "…",          // I. Mục tiêu và phạm vi cuộc họp
      "discussion": "1. …\n…",    // II. Nội dung thảo luận và đánh giá, mỗi mục nghị sự một phần "n. <tên>"
      "conclusions": "1. Kết luận của chủ trì\n…\nKết quả biểu quyết:\n- …\n2. Nhiệm vụ, sản phẩm và thời hạn\n…",
      "sources": [{ "agendaItemId": "…", "title": "…", "from": "ISO", "to": "ISO", "lines": 9 }]
    },
    "model": "qwen2.5:7b-instruct", "generatedAt": "ISO", "requestedAt": "ISO", "requestedBy": { "id": "…", "fullName": "…" },
    "errorMessage": "", "reviewedBy": null, "reviewedAt": null, "editedBy": null, "editedAt": null
  },
  "canManage": true,
  "aiReady": true,                 // server đã cấu hình máy AI
  "readiness": {                   // chỉ Chủ tọa, Thư ký; null với người khác
    "sentences": 16, "unnamedVoices": 0, "transcribing": 0, "failedTranscripts": 0,
    "agendaItems": 2, "agendaItemsTimed": 2, "closedVotes": 1
  }
}
```

- **Polling:** FE gọi lại `GET` mỗi 5 giây khi `status` là `Queued` hoặc `Running`.
- **`Failed`:** `errorMessage` ghi lý do bằng lời Thư ký hiểu được, ví dụ máy AI tắt.
- **`sources`:** khoảng giờ lời thoại của từng phần mục II, để Thư ký đối chiếu với tab Bản ghi.
- **Kết quả biểu quyết** trong `conclusions` được chép từ dữ liệu phiên biểu quyết, không qua AI.
- **Lỗi:**
  - `403` `MSG385` khi không phải Chủ tọa hay Thư ký.
  - `409` `MSG386` khi cuộc họp chưa bắt đầu hoặc đã huỷ, `409` `MSG387` khi AI đang viết, `409` `MSG390` khi chưa có
    dự thảo nào viết xong.
  - `400` `MSG389` khi một mục trống hoặc quá dài, `500` `MSG392` khi lỗi đọc, `503` `MSG393` khi lỗi lưu, `503`
    `MSG394` khi server chưa cấu hình máy AI.

## Bản nháp biên bản — `/api/meetings/{id}/minutes`, `/api/minutes/{id}`

| Method | Đường dẫn | Kết quả |
|---|---|---|
| `GET` | `/api/meetings/{id}/minutes` | `200` `{ minutes, canManage, draftChangedAt }` |
| `POST` | `/api/meetings/{id}/minutes` | `201` `MSG395`. Body `{ rebuild: false }`; đã có bản nháp thì `409` `MSG399`, gửi `rebuild: true` để thay |
| `PUT` | `/api/minutes/{minutesId}` | `200` `MSG396`. Body `{ content, updatedAt }` |
| `GET` | `/api/minutes/{minutesId}/export` | `200` file `Biên bản - <tên cuộc họp>.docx` |

- **`minutes`:** `{ id, revisionNumber, status, content, createdAt, updatedAt, createdBy, builtFromDraftAt }`, hoặc `null`.
- **`updatedAt`:** chính là giá trị server trả về lần đọc trước. Nếu đã có người lưu sau lần đó, server trả `409`
  `MSG401`, không ghi đè.
- **Mục I–III:** lấy từ dự thảo hiện có, kể cả khi chưa duyệt. Biên bản nháp cũng chỉ Chủ tọa và Thư ký xem, nên không
  ai đọc được chữ AI chưa duyệt qua biên bản. Chưa có dự thảo (ví dụ máy AI tắt) thì mục II liệt kê chương trình, mục III
  ghi kết luận Chủ toạ đã ghi và kết quả biểu quyết, còn lại để "……".
- **`draftChangedAt`:** lúc dự thảo được viết hoặc sửa lần cuối; `null` khi chưa có dự thảo, và với mọi người trừ Chủ tọa,
  Thư ký.
- **`builtFromDraftAt`:** `draftChangedAt` của dự thảo dùng lúc tạo (hoặc tạo lại) biên bản; `null` khi tạo lúc chưa có
  dự thảo. Hai giá trị khác nhau nghĩa là dự thảo đã đổi sau đó: màn hình báo và mời bấm *Tạo lại*. Lưu tay biên bản không
  đổi giá trị này. Chỉ Chủ tọa, Thư ký thấy.
- **In PDF:** không có endpoint. Nút *Tải PDF* in tờ biên bản trên trang (khổ A4, lề theo Nghị định 30), hộp in của trình
  duyệt lưu thành PDF. Cuộc họp Mật không có nút này, như *Tải Word*.
- **Lỗi:**
  - `409` `MSG400` khi biên bản đã gửi duyệt hoặc ban hành.
  - `400` `MSG402` khi nội dung trống hoặc quá 100.000 ký tự.
  - `403` `MSG405` khi tải biên bản của cuộc họp Mật.
  - `500` `MSG403` khi lỗi đọc, `503` `MSG404` khi lỗi lưu.

### Định dạng `content`

Văn bản thuần, không phải HTML. Trang web (`MinutesPaper.tsx`) và file Word (`MinutesDocx.cs`) đọc cùng các dấu sau:

| Dấu | Nghĩa |
|---|---|
| `@@ trái \| phải` | Một dòng hai cột. Các dòng liền nhau thành một bảng không viền (phần đầu, chữ ký) |
| `# ` | Tiêu đề giữa trang ("BIÊN BẢN") |
| `^ ` | Dòng căn giữa |
| `## ` | Tiêu đề mục (I, II, III) |
| `### ` | Tiêu đề nhỏ in đậm |
| `- ` | Dòng gạch đầu dòng |
| `~ ` | Dòng nhỏ sát lề trái ("Nơi nhận") |
| `**…**`, `__…__` | Chữ đậm, chữ nghiêng trong dòng |

"……" là chỗ Thư ký cần điền.

## Cấu hình máy AI

| Khoá | Ví dụ | Ghi chú |
|---|---|---|
| `Ai:BaseUrl` | `http://localhost:11434/v1` | Server có API chuẩn OpenAI: Ollama, llama.cpp `llama-server`, LM Studio, vLLM. Để trống là tắt |
| `Ai:Model` | `qwen2.5:7b-instruct` | Tên model trên server đó |
| `Ai:ApiKey` | | Tuỳ chọn, gửi kèm dạng `Bearer` |
| `Ai:TimeoutMinutes` | `15` | Thời gian chờ tối đa cho một lần gọi |
| `Ai:MaxTokens` | `2048` | Độ dài tối đa của mỗi câu trả lời |

Trên server test, đặt bằng biến môi trường `Ai__BaseUrl`, `Ai__Model`.

Bộ đo chất lượng trên 8 cuộc họp mẫu: [`tests/ai/`](../../tests/ai/README.md).

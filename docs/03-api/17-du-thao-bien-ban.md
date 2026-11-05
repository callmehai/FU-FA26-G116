# Dự thảo AI và bản nháp biên bản — SCRUM-36, SCRUM-32

Theo [ADR-0054](../05-decisions/0054-ai-du-thao-bien-ban-chay-tai-cho.md): làm như AI-eCabinet, gồm hai bước.

1. **Dự thảo:** AI viết 3 mục của biên bản. Chủ tọa hoặc Thư ký sửa rồi duyệt.
2. **Bản nháp biên bản:** điền mẫu Biên bản (Nghị định 30, Mẫu 1.4) từ hồ sơ cuộc họp, cộng với dự thảo hiện có, đã duyệt
   hay chưa (như `Compose` của eCab).

Mã thông báo: `MSG384`–`MSG406` ([danh mục](00-danh-muc-ma-thong-bao.md)). Kiểu dữ liệu FE:
[`frontend/src/types/summary.ts`](../../frontend/src/types/summary.ts).

## Quyền

| Ai | Dự thảo | Bản nháp biên bản |
|---|---|---|
| Chủ tọa, Thư ký hiện tại | Xem, tạo, tạo lại, sửa, duyệt | Xem, tạo, tạo lại, sửa, tải Word, tải PDF |
| Người khác đọc được cuộc họp | Chỉ xem, và chỉ sau khi đã duyệt (`summary: null` trước đó) | Chỉ xem, và chỉ khi đã ban hành (SCRUM-33, chưa làm) |
| Không đọc được cuộc họp | `404` | `404` |

- Tạo dự thảo và tạo biên bản chỉ được làm khi cuộc họp đang diễn ra hoặc đã kết thúc (`MSG386`, `MSG398`).
- Server kiểm tra lại quyền ở mọi request.

## Dự thảo — `/api/meetings/{id}/summary`

| Method | Đường dẫn | Kết quả |
|---|---|---|
| `GET` | `/api/meetings/{id}/summary` | `200` `SummaryState` |
| `POST` | `/api/meetings/{id}/summary` | `202` `MSG384` + `SummaryState`. Đưa vào hàng chờ; dự thảo cũ vẫn đọc được tới khi bản mới xong |
| `PUT` | `/api/meetings/{id}/summary` | `200` `MSG388`. Body `{ objectives, discussion, conclusions }`. Bỏ trạng thái đã duyệt. Server bỏ dòng trống trong mỗi mục trước khi lưu (AI viết dự thảo cũng không để dòng trống) |
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
    "agendaItems": 2, "agendaItemsTimed": 2, "closedVotes": 1,
    "transcriptChangedAt": "2026-11-05T03:12:40Z"
  }
}
```

- **Polling:** FE gọi lại `GET` mỗi 5 giây khi `status` là `Queued` hoặc `Running`.
- **`Failed`:** `errorMessage` ghi lý do bằng lời Thư ký hiểu được, ví dụ máy AI tắt.
- **`transcriptChangedAt`:** lần cuối lời thoại thay đổi (câu mới, câu được sửa, bỏ ra hay xoá, giọng được gán tên hay
  mất tên do người đó rút đồng ý Voice ID, câu chuyển sang giọng khác, bản ghi bị xoá), như `transcript_changed_at` của
  eCab; `null` khi chưa có lời thoại. Muộn hơn `generatedAt` thì FE báo dự thảo chưa có các chỗ sửa đó và mời Tạo lại.
  Câu bị xoá hẳn nên được tính theo dòng audit `speech_clip.deleted`; bản ghi bị xoá tính theo `recordings.deleted_at`.
- **`generatedAt`:** lúc AI đọc lời thoại để viết (lúc job bắt đầu chạy), không phải lúc viết xong. Lời thoại sửa trong lúc
  AI đang viết vì vậy muộn hơn `generatedAt`, và dự thảo mới ra vẫn báo là chưa có chỗ sửa đó.
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
| `PUT` | `/api/minutes/{minutesId}` | `200` `MSG396`. Body `{ content, updatedAt }`; `content` là HTML, server lọc rồi lưu và trả lại bản đã lọc |
| `GET` | `/api/minutes/{minutesId}/export` | `200` file `Biên bản - <tên cuộc họp>.docx` |
| `GET` | `/api/minutes/{minutesId}/export?format=pdf` | `200` file `Biên bản - <tên cuộc họp>.pdf` (`application/pdf`) |

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
- **`content`:** luôn là HTML đã lọc (xem [Định dạng `content`](#định-dạng-content)), kể cả biên bản lưu trước 05/11/2026
  dưới dạng văn bản có dấu: server đổi sang HTML khi đọc và khi tải về.
- **Tải PDF:** `?format=pdf`, server dựng PDF bằng QuestPDF từ cùng `content` (khổ A4, lề theo Nghị định 30, font Liberation
  Serif kèm trong ứng dụng). Không có `format` hoặc `format=docx` là file Word; giá trị khác trả `400` `MSG406`. Quyền,
  cuộc họp Mật và lỗi như file Word; cả hai không được cache.
- **Lỗi:**
  - `409` `MSG400` khi biên bản đã gửi duyệt hoặc ban hành.
  - `400` `MSG402` khi nội dung trống hoặc quá 300.000 ký tự (tính cả HTML gửi lên lẫn HTML sau khi lọc). Chỉ có đoạn
    trống, `<br>` hay đường kẻ cũng là trống; bảng có chữ thì không.
    Thẻ lồng sâu quá 64 tầng cũng bị từ chối (trình soạn thảo không bao giờ lồng sâu như vậy); bảng tối đa 63 cột như Word,
    ô vượt quá bị bỏ khi xuất file. Trong PDF, bảng lồng quá 3 tầng in phần chữ của các ô nối tiếp nhau, không kẻ lưới.
  - `403` `MSG405` khi tải biên bản của cuộc họp Mật, `400` `MSG406` khi `format` không phải `docx` hay `pdf`.
  - `500` `MSG403` khi lỗi đọc, `503` `MSG404` khi lỗi lưu.

### Định dạng `content`

HTML soạn bằng TipTap, server giữ theo một danh sách cho phép cố định
([ADR-0055](../05-decisions/0055-bien-ban-luu-html-soan-nhu-word.md), `MinutesHtml.cs`). Trang web
(`MinutesPaper.tsx`), file Word (`MinutesDocx.cs`) và file PDF (`MinutesPdf.cs`) cùng đọc HTML này.

| Thẻ | Thuộc tính, `style` được giữ |
|---|---|
| `p`, `h1`, `h2`, `h3` | `text-align` (`left`, `center`, `right`, `justify`), `text-indent` (mm/pt, −50…50 mm), `margin-left` (mm/pt, 0…150 mm), `line-height` (số, 0,8…3), `margin-top`, `margin-bottom` (pt, 0…72) |
| `ul`, `ol` | `margin-left`; `ol` thêm `start` (1…9999) |
| `li` | `text-align`, `line-height` |
| `table` | `data-layout="frame"` (bảng không viền), `width`, `min-width` (px, 0…2000) |
| `colgroup`, `col` | `col`: `width`, `min-width` (px) |
| `thead`, `tbody`, `tr` | — |
| `th`, `td` | `colspan` (1…50), `rowspan` (1…200), `colwidth` (px 1…2000, cách nhau dấu phẩy), `text-align`, `vertical-align` (`top`, `middle`, `bottom`) |
| `hr` | `data-page-break="true"` là ngắt trang, không có là đường kẻ |
| `strong`, `em`, `u`, `s`, `sub`, `sup`, `br` | — |
| `span` | `font-size` (pt, 6…40), `color` (`#000000`, `#c00000`, `#ff0000`, `#ed7d31`, `#00b050`, `#0070c0`, `#002060`, `#7030a0`), `background-color` (`#ffff00`, `#00ff00`, `#00ffff`, `#ff00ff`, `#d9d9d9`) |

- `b`, `i`, `strike`/`del`, `ins`, `h4`–`h6` đổi thành `strong`, `em`, `s`, `u`, `h3`.
- `script`, `style`, `iframe`, `object`, `embed`, `template`, `noscript`, `svg`, `math`, `img`, `video`, `audio`, `canvas`,
  `form`, `button`, `textarea`, `select`, `link`, `meta`, `base`, `head`, `title` bị bỏ cùng nội dung; chú thích HTML bị bỏ.
- Thẻ khác bị gỡ, giữ chữ bên trong (`div`, `section`… còn ngắt đoạn). Thuộc tính khác (`on*`, `href`, `src`, `class`,
  `id`…) bị bỏ. Giá trị `style` sai hoặc ngoài khoảng chỉ bỏ thuộc tính đó; màu `rgb(r, g, b)` hay chữ hoa được đổi về
  `#rrggbb` chữ thường; `span` không còn `style` bị gỡ.
- Chữ hay thẻ trong dòng nằm trực tiếp ở gốc, trong `li`, `td`, `th` được bọc vào `<p>`.
- Đầu ra viết lại theo một kiểu: thuộc tính giữ thứ tự gửi lên, `style` dạng `prop: value; prop2: value2`, thẻ rỗng
  `<br>`, `<hr>`, `<col>`, chữ thoát `&amp;`, `&lt;`, `&gt;`, `&nbsp;`. Lọc lại bản đã lọc không đổi gì.

Mặc định khi in (Word, PDF; trang web khớp theo): Times New Roman 13 pt, giãn dòng 1,2, A4, lề 20/15/20/30 mm; `p` căn đều,
thụt dòng đầu 12,7 mm, cách dưới 3 pt; `h1` căn giữa, đậm, 14 pt, cách trên 12 pt; `h2`, `h3` đậm, thụt 12,7 mm (`h2` cách
trên 6 pt); `p` trong ô bảng hay danh sách sát trái, không thụt, không cách dưới; danh sách thụt treo 6 mm mỗi cấp; bảng
rộng hết khổ chữ (165 mm), cột chia theo tỉ lệ `colwidth`, viền 0,5 pt; `th` đậm, căn giữa.

Biên bản lưu trước 05/11/2026 là văn bản có dấu đầu dòng (`# `, `## `, `### `, `^ `, `~ `, `@@ trái | phải`,
`**đậm**`, `__nghiêng__`, theo ADR-0054). Server đổi chúng sang HTML trên khi đọc. Mẫu biên bản vẫn viết bằng các dấu đó
rồi đổi sang HTML lúc tạo.

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

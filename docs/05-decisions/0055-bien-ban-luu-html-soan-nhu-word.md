# ADR-0055: Biên bản lưu HTML đã lọc, soạn bằng trình soạn thảo kiểu Word

- **Trạng thái:** Đề xuất — PO (Hải) 05/11/2026
- **Ngày:** 2026-11-05
- **Người quyết định:** Hải (PO)
- **Liên quan:** SCRUM-32 (sinh biên bản, UC-58–UC-60); thay phần "biên bản là văn bản thuần có dấu đầu dòng" (điểm 7 mục
  Quyết định và gạch đầu dòng thứ ba mục Hệ quả) của [ADR-0054](0054-ai-du-thao-bien-ban-chay-tai-cho.md)

## Bối cảnh

Theo ADR-0054, `MeetingMinutes.Content` là văn bản thuần, mỗi dòng có một dấu ở đầu (`# `, `## `, `^ `, `~ `,
`@@ trái | phải`) và `**đậm**`, `__nghiêng__` trong dòng. Trang web, file Word và file PDF cùng đọc các dấu đó.

Cách này chỉ chở được chữ đậm, chữ nghiêng, dòng căn giữa và hàng hai cột. Thư ký không gạch chân, không đổi màu hay cỡ
chữ, không làm danh sách đánh số, không chèn bảng nhiều cột, không ngắt trang được. Người dùng muốn soạn biên bản như trong
Word, giống AI-eCabinet: eCab dùng TipTap, lưu HTML rồi lọc lại ở server.

## Quyết định

1. **`Content` là HTML, giữ trong một danh sách cho phép cố định** (`MinutesHtml.cs`). FE soạn bằng TipTap và chỉ sinh ra
   đúng những thứ sau:
   - Khối: `p`, `h1`, `h2`, `h3`, `ul`, `ol` (`start` 1–9999), `li`, `hr` (`data-page-break="true"` là ngắt trang),
     `table` (`data-layout="frame"` là bảng không viền), `colgroup`, `col`, `thead`, `tbody`, `tr`, `th`, `td`
     (`colspan` 1–50, `rowspan` 1–200, `colwidth` là các số px 1–2000 cách nhau dấu phẩy).
   - Trong dòng: `strong`, `em`, `u`, `s`, `sub`, `sup`, `br`, `span`.
   - Thuộc tính `style`, mỗi thẻ chỉ được vài thuộc tính, mỗi thuộc tính trong một khoảng: căn lề (`text-align`), thụt
     dòng đầu (`text-indent`, mm/pt, ±50 mm), lề trái (`margin-left`, 0–150 mm), giãn dòng (`line-height`, 0,8–3), khoảng
     cách trên/dưới (`margin-top`, `margin-bottom`, 0–72 pt), cỡ chữ (`font-size`, 6–40 pt), màu chữ (8 màu chuẩn của
     Word) và màu tô (5 màu), căn dọc ô (`vertical-align`), độ rộng bảng/cột (`width`, `min-width`, px).
   - `b`, `i`, `strike`, `del`, `ins`, `h4`–`h6` được đổi tên; `script`, `style`, `iframe`, `img`, `form`, `svg`… bị bỏ cùng
     nội dung; thẻ lạ khác bị gỡ, giữ lại chữ bên trong; thuộc tính lạ (`on*`, `href`, `src`, `class`…) bị bỏ.
2. **Server lọc mọi lần ghi.** `PUT /api/minutes/{id}` lọc HTML gửi lên rồi mới lưu, và trả lại bản đã lọc. Không tin
   trình duyệt: một request gửi thẳng tới API cũng chỉ lưu được những gì trong danh sách. Biên bản chỉ có đoạn trống hay
   đường kẻ thì coi như trống (`400` `MSG402`). Bộ lọc dùng AngleSharp (MIT) để đọc HTML như trình duyệt đọc, còn phần chọn
   giữ gì do code của nhóm viết. Lọc hai lần cho cùng kết quả.
3. **Dòng cũ đổi sang HTML khi đọc.** `MinutesTemplate` vẫn viết văn bản có dấu như trước; lúc tạo biên bản, server đổi
   sang HTML (`MinutesHtml.FromLines`) rồi mới lưu. Dòng lưu trước quyết định này vẫn là văn bản có dấu: lúc đọc và lúc tải
   về, server đổi sang HTML. Không viết migration đổi dữ liệu, vì dữ liệu test bỏ được.
4. **File Word và PDF đọc HTML đó**, qua một bước dàn trang chung (`MinutesLayout.cs`), để hai file ra cùng một trang. Mặc
   định theo Nghị định 30/2020/NĐ-CP: Times New Roman 13 pt (PDF dùng Liberation Serif kèm sẵn, cùng cỡ chữ), giãn dòng
   1,2, khổ A4, lề trên 20, phải 15, dưới 20, trái 30 mm; đoạn văn căn đều hai bên, thụt dòng đầu 12,7 mm, cách dưới 3 pt;
   `h1` căn giữa, đậm, 14 pt; `h2`, `h3` đậm, thụt 12,7 mm. Đoạn trong ô bảng hay trong danh sách sát lề trái, không thụt.
   Bảng luôn rộng hết khổ chữ (165 mm), các cột chia theo tỉ lệ `colwidth`, viền 0,5 pt (bảng `frame` không viền). Kiểu
   ghi rõ trên thẻ luôn thắng mặc định. File Word vẫn tự viết bằng XML, không thêm thư viện.
5. **Giới hạn nội dung lên 300.000 ký tự** (trước là 100.000): HTML dài hơn văn bản nó chở. Giới hạn áp cho cả HTML gửi
   lên và HTML sau khi lọc; cột `content` đổi theo (migration `MinutesContentHtml`).
6. **Giới hạn để một request không làm sập server:** HTML lồng sâu quá 64 tầng bị từ chối khi lưu (`MSG402`), vì bộ lọc và
   bước dàn trang đi đệ quy theo từng tầng và .NET không bắt được lỗi tràn stack; nội dung lỡ sâu hơn chỉ được đọc phần
   chữ. Bảng giữ tối đa 63 cột (giới hạn của Word), ô vượt quá bị bỏ khi xuất; lề trái của đoạn trong ô hẹp không vượt quá
   ô, để file PDF luôn dàn trang được.

## Phương án đã cân nhắc

| Phương án | Ưu | Nhược | Vì sao không chọn |
|---|---|---|---|
| Giữ văn bản có dấu, thêm dấu mới cho từng định dạng | Không có HTML nào chạy được | Mỗi định dạng một dấu mới, ba trình đọc phải sửa cùng lúc; bảng nhiều cột, danh sách lồng nhau gần như không biểu diễn được | Không đáp ứng được yêu cầu soạn như Word |
| Lưu JSON của TipTap (ProseMirror) | Cấu trúc rõ, dễ duyệt | Phụ thuộc lược đồ của TipTap; server phải hiểu định dạng riêng của một thư viện FE | eCab lưu HTML; HTML là thứ trang web, Word và người đọc đều hiểu |
| Lưu HTML, chỉ lọc ở trình duyệt (DOMPurify) | Ít code server | Request gửi thẳng tới API lưu được bất cứ gì; file Word/PDF phải đọc HTML tuỳ ý | UI không bao giờ là ranh giới bảo mật (AGENTS.md) |
| Đổi HTML sang Word/PDF bằng LibreOffice | Định dạng đầy đủ | Image to thêm vài trăm MB, tốn RAM server 4 GB (lý do đã có ở ADR-0054) | Giữ cách tự viết |

## Hệ quả

- Thanh công cụ của trình soạn có thêm gạch chân, gạch ngang, chỉ số trên/dưới, màu chữ, màu tô, cỡ chữ, căn lề, thụt
  lề, giãn dòng, danh sách chấm và số, bảng, đường kẻ, ngắt trang. Thứ gì ngoài danh sách cho phép thì server bỏ khi lưu,
  nên FE không được thêm nút cho định dạng mới mà không sửa `MinutesHtml.cs`, `MinutesLayout.cs` và ADR này.
- Mọi thay đổi danh sách cho phép phải sửa cùng lúc bộ lọc, bước dàn trang của Word/PDF và CSS của trang, rồi kiểm lại ba
  nơi ra cùng một trang.
- Cách đọc một số kiểu ở file Word/PDF là xấp xỉ: `rowspan` thành ô gộp dọc của Word, PDF vẽ ô cao nhiều hàng; thụt âm
  (`text-indent` < 0) không vẽ được trong PDF nên đoạn đó sát lề; ngắt trang nằm trong ô bảng hay danh sách bị bỏ qua.
- `line-height` hiểu như "Multiple" của Word (1,2 là mặc định). PDF nhân thêm 1,15 (giãn dòng đơn của font) để ra cùng
  khoảng cách; CSS của trang làm như PDF.
- Thêm thư viện AngleSharp vào `TrustCabinet.Infrastructure`, ghim từ bản 1.5.0 trở lên (bản cũ hơn có lỗi mXSS
  GHSA-pgww-w46g-26qg).
- Lời thoại và biên bản vẫn là dữ liệu cá nhân (Luật 91/2025/QH15); quyết định này không đổi ai được đọc, sửa hay tải biên
  bản.

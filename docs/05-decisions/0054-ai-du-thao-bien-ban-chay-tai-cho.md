# ADR-0054: Dự thảo và biên bản theo AI-eCabinet, AI chạy tại chỗ qua API chuẩn OpenAI

- **Trạng thái:** Đề xuất — PO (Hải) 04/11/2026; điểm 7 và phần trình soạn ở mục Hệ quả (biên bản là văn bản thuần) thay thế bởi [ADR-0055](0055-bien-ban-luu-html-soan-nhu-word.md)
- **Ngày:** 2026-11-04
- **Người quyết định:** Hải (PO)
- **Liên quan:** SCRUM-36 (tóm tắt AI), SCRUM-32 (sinh biên bản), SCRUM-33 (duyệt, ban hành — chưa làm);
  [ADR-0026](0026-tu-chay-nhan-dang-giong-noi.md) (tự chạy nhận dạng giọng nói)

## Bối cảnh

AI-eCabinet (commit `bba2097f`) làm hai việc này thành một luồng hai bước:

1. **Dự thảo** (`workers/ai/src/ai/llm.py`, `_OUTLINE_PROMPT`): đưa chương trình, biểu quyết, tên tài liệu và bản ghi
   lời thoại cho model, model viết đúng 3 mục của biên bản (I. Mục tiêu và phạm vi, II. Nội dung thảo luận và đánh giá,
   III. Kết luận và phân công thực hiện). Kết quả biểu quyết được chép lại từ dữ liệu, không lấy chữ của model.
2. **Biên bản** (`modules/minutes/internal/templates/bien-ban-cuoc-hop.html`): mẫu Biên bản của Nghị định 30
   (Mẫu 1.4), điền thông tin cuộc họp từ hồ sơ và 3 mục từ dự thảo.

eCab chạy Llama 3.3 70B trên vLLM, gọi qua API chuẩn OpenAI. Server test của TrustCabinet chỉ có 4 GB RAM, không chạy
nổi model nào. Máy mạnh nhất nhóm có 16 GB RAM và 6 GB VRAM, chỉ chạy được model khoảng 7B.

## Quyết định

1. **Làm theo eCab**: dự thảo 3 mục, rồi biên bản theo **một mẫu cố định** (Mẫu 1.4, đúng bố cục mẫu của eCab).
   Không có màn quản lý mẫu biên bản.
2. **AI chạy tại chỗ, gọi qua `POST {Ai:BaseUrl}/chat/completions`** (API chuẩn OpenAI, như worker của eCab). Ollama,
   llama.cpp (`llama-server`), LM Studio hay vLLM đều dùng được. Đổi model hay đổi máy chỉ là đổi cấu hình
   `Ai:BaseUrl`, `Ai:Model`, `Ai:ApiKey`. Mặc định thử nghiệm: Ollama với `qwen2.5:7b-instruct`. Model chạy trên máy một
   thành viên, server gọi tới qua tunnel; máy đó tắt thì nút tạo dự thảo báo lỗi, phần còn lại vẫn chạy.
3. **Chia lời thoại theo chương trình** thay vì đưa cả cuộc họp vào một lần như eCab: model 7B yếu hơn 70B nhiều, và
   ngữ cảnh ngắn. Lời thoại được cắt theo giờ Chủ toạ bấm bắt đầu và kết thúc từng mục nghị sự. Mỗi mục được tóm tắt
   riêng; mục quá dài thì tóm tắt từng phần rồi gộp lại. Mục I và III viết sau, từ các phần tóm tắt và kết luận Chủ toạ
   đã ghi. Mỗi phần của mục II lưu kèm khoảng giờ lấy lời thoại, để Thư ký đối chiếu với bản ghi.
4. **Giữ các luật prompt của eCab** (đều rút ra từ những lần model viết sai trên dữ liệu thật): số viết bằng chữ số;
   viết hoa tên riêng; không suy ra thành phần tham dự; giữ nguyên nhãn "Người nói N", cấm đoán tên; người trình bày lấy
   từ chương trình, không lấy từ người nói; không Markdown. Nhiệt độ bằng 0. Riêng luật chữ số, model 7B hay bỏ qua,
   nên code đổi số đọc thành chữ số trước khi gửi ("chín mươi phần trăm" → "90%", `SpokenNumbers.cs`): việc nào code
   làm chắc được thì không giao cho model.
5. **AI chỉ viết nháp.** Dự thảo luôn gắn nhãn "Nội dung do AI tạo — cần rà soát". Trước khi Chủ toạ hoặc Thư ký bấm
   Duyệt, chỉ hai người này thấy dự thảo. Sửa sau khi duyệt thì phải duyệt lại. Biên bản lấy dự thảo hiện có, đã duyệt
   hay chưa, như `Compose` của eCab: biên bản nháp cũng chỉ hai người này thấy. Thẻ Biên bản báo khi dự thảo chưa duyệt
   (nút *Duyệt dự thảo*) và khi dự thảo đã đổi sau lần tạo biên bản (nút *Tạo lại*). Chưa có dự thảo thì vẫn tạo được
   biên bản, mục I–III để trống ("……") cho Thư ký viết: máy AI là laptop, có lúc tắt. (Sửa 05/11/2026: bản đầu bắt duyệt
   trước, biên bản ra "……" mà không báo vì sao.)
6. **Mục III giữ dòng "2. Nhiệm vụ, sản phẩm và thời hạn" như mẫu của eCab.** Đó chỉ là chữ trong biên bản. TrustCabinet
   không theo dõi người được giao, hạn hay tiến độ của nhiệm vụ nào (phạm vi trong README không đổi).
7. **Biên bản là văn bản thuần có vài dấu đầu dòng** (`##`, `###`, `@@ trái | phải`, `**đậm**`…, xem
   `MinutesContracts.cs`), không phải HTML. Trang web và file Word đều đọc từ đó, nên không có gì trong biên bản chạy
   được trên trình duyệt. File Word được tự viết (vài phần XML trong một file zip), không thêm thư viện. Bản PDF dựng
   trên server bằng QuestPDF từ cùng văn bản có dấu đó (khổ A4, lề Nghị định 30), kèm sẵn font Liberation Serif (SIL OFL,
   cùng cỡ chữ với Times New Roman, đủ dấu tiếng Việt) vì image không có Times New Roman: eCab đổi file Word sang PDF bằng
   LibreOffice, nhưng LibreOffice làm image to thêm vài trăm MB và tốn RAM của server 4 GB. Cuộc họp Mật không được tải biên bản về, như tài liệu và bản gỡ băng.

## Hệ quả

- Khác eCab:
  - eCab gọi model một lần cho cả cuộc họp; TrustCabinet gọi một lần cho mỗi mục nghị sự, cộng một lần cho mục I và III.
  - eCab cho model viết lại cả biên bản theo mẫu, rồi mới điền mẫu khi model viết hỏng; TrustCabinet chỉ điền mẫu, không
    cho model viết lại.
  - Cả hai soạn biên bản bằng trình soạn thảo kiểu Word (TipTap). eCab lưu HTML rồi lọc lại; TrustCabinet lưu văn bản có
    dấu đầu dòng, trình soạn đổi qua lại (`minutesDoc.ts`), nên thanh công cụ chỉ có những gì file Word và PDF in được: kiểu
    dòng (tiêu đề, tiêu đề mục, tiêu đề nhỏ, căn giữa, dòng nhỏ), đậm, nghiêng, hàng hai cột. (Sửa 05/11/2026: bản đầu là
    ô chữ thô kèm xem trước.)
- Viết dự thảo tốn CPU của máy chạy AI vài phút mỗi lần. Hàng chờ chạy lần lượt từng cuộc họp.
- `tests/ai/` có 8 cuộc họp mẫu, từ đầy đủ đến thiếu từng phần, để so model trên cùng dữ liệu.
- Lời thoại và biên bản là dữ liệu cá nhân (Luật 91/2025/QH15). Server gửi lời thoại sang máy chạy AI, nên máy đó phải
  là máy của nhóm, gọi qua kết nối mã hoá. Không được trỏ `Ai:BaseUrl` sang một dịch vụ AI bên ngoài.

# ADR-0044: Server kiểm cấu trúc PDF và tự đếm số trang bằng PdfPig

- **Trạng thái:** Đề xuất — SCRUM-107 (sau test E2E 02/11/2026)
- **Ngày:** 2026-11-02
- **Người quyết định:** Lộc (BE), chờ PO xác nhận

## Bối cảnh

SCRUM-107 nhận diện PDF chỉ bằng 5 byte đầu `%PDF-` và lưu số trang do trình duyệt đếm bằng pdf.js rồi gửi kèm
(`pageCounts`). Test E2E trên server test (26/10 và 02/11/2026) cho thấy hai hệ quả:

- Một tệp 31 byte `%PDF-1.4 … %%EOF`, không có trang nào, được nhận và hiện "1 trang". Trình xem không mở được, lại báo
  "Vui lòng thử lại" mãi (lỗi #4 và #6 trong comment SCRUM-58 ngày 26/10).
- Gọi thẳng API với `pageCounts=999` cho một PDF 3 trang thì tab Tài liệu và Kho hiện "999 trang". Số trang là thông tin
  hiển thị, nhưng client muốn ghi gì cũng được.

Kiểm thêm phía server thì phải đọc cấu trúc PDF, việc mà một đoạn kiểm byte tự viết khó làm đúng: trang có thể nằm trong
object stream nén, xref có thể là stream, tệp có thể được ghi nối nhiều lần.

## Quyết định

1. **Server đọc cấu trúc PDF bằng [PdfPig](https://github.com/UglyToad/PdfPig) 0.1.16** (gói NuGet `PdfPig`, Apache-2.0,
   thuần .NET, không native). Gói tên `UglyToad.PdfPig` trên NuGet **không** phải bản chính thức, đừng dùng.
2. Một tệp PDF hợp lệ khi: bắt đầu bằng `%PDF-`, 1 KB cuối có `startxref` và `%%EOF`, PdfPig mở được ở chế độ nghiêm
   (`UseLenientParsing = false`) và có ít nhất một trang. Không đạt → `MSG164`, như mọi tệp sai khác. Tệp cần mật khẩu
   mới mở cũng bị từ chối vì trình xem không mở được.
3. **`StoredFile.PageCount` là số server đếm.** Part `pageCounts` không còn được đọc; FE cũ vẫn gửi được, không lỗi.
4. Kiểm **1 KB cuối trước khi đưa cho PdfPig**: rác sau `%%EOF` buộc bộ đọc dò `startxref` lùi từng byte, đo được 191 giây
   cho tệp 51 MB. Còn tệp có đuôi đúng mà thân là rác (40 MB ngẫu nhiên, hay xref trỏ sai) thì PdfPig báo lỗi trong dưới
   0,5 giây. 1 KB là ngưỡng Acrobat dùng cho `%%EOF`.
5. Không chuyển đổi, không đọc nội dung trang: PdfPig chỉ mở catalog và cây trang. ADR-0018 (không chuyển đổi tệp trên
   server) vẫn giữ nguyên.
6. **PdfPig chạy trong tiến trình con, không chạy trong tiến trình của server** (sửa sau review PR #224). PdfPig đi xuống
   cây trang bằng đệ quy: một tệp 2 MB gồm 20.000 nút trang lồng nhau làm tràn stack, mà .NET không bắt được
   `StackOverflowException`, nên cả server chết. Ai đăng nhập cũng gửi được tệp như vậy (tải vào kho cá nhân). Thêm vào đó,
   một tệp 1 MB có xref stream nén tới 1 GB làm bộ đọc cấp phát khoảng 3 GB. Vì vậy `PdfPageCounter` chạy lại chính app với
   tham số `--check-pdf <tệp tạm>`:
   - heap của tiến trình con bị giới hạn 384 MB (`DOTNET_GCHeapHardLimit`), quá 15 giây thì bị kill, mỗi lúc tối đa 2 tiến
     trình con;
   - tiến trình con in `pages=N` và thoát 0, hoặc thoát 2 khi tệp không đọc được. Chết vì tràn stack, hết bộ nhớ hay quá
     giờ đều coi là PDF không hợp lệ (`MSG164`);
   - không khởi động được tiến trình con là lỗi server (500), không phải lỗi tệp.
   Tăng stack của luồng không phải cách sửa: trong 50 MB có thể nén hàng trăm nghìn tầng, nhiều hơn mọi stack.

## Hệ quả

- Tệp như trên bị chặn từ lúc tải lên, không còn lọt vào cuộc họp rồi mới hỏng ở trình xem. Số trang hiển thị đúng với tệp.
- Domain có thêm một dependency đọc dữ liệu không tin cậy; tiến trình con là ranh giới của nó. Mỗi tệp PDF tốn thêm một
  lần khởi động tiến trình .NET (khoảng 0,3–0,5 giây, gấp nhiều lần thời gian đọc một tệp họp thường). Với hàng đợi tải
  từng tệp (ADR-0033) thì người dùng không thấy khác.
- Đã thử trên 226 PDF thật từ nhiều phần mềm (Word, Foxit, sách 516 trang…): đếm đúng; chỉ tệp cần mật khẩu mở bị từ chối.
  PDF chỉ đặt mật khẩu chủ sở hữu (quyền in/sao chép) vẫn nhận.
- Tệp tạm của lần tải không còn đọc tuần tự (PdfPig nhảy trong tệp), nên bỏ `SequentialScan` khi mở.
- Dữ liệu cũ trên server (tệp đã nhận trước 02/11) giữ nguyên số trang cũ; trình xem báo `MSG383` nếu không vẽ được.
- Test dựng PDF thật trong bộ nhớ (`SampleFiles.Pdf(text, pages)`), không commit tệp nhị phân.

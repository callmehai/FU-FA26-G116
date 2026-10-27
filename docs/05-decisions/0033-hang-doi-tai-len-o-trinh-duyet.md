# ADR-0033: Hàng đợi tải lên ở trình duyệt, mỗi tệp một request

- **Trạng thái:** Đề xuất — SCRUM-58, PO 27/10/2026
- **Ngày:** 2026-10-27
- **Người quyết định:** PO (Hải)

## Bối cảnh

Trước đây mọi chỗ tải tệp lên đều chạy trong hộp thoại và chờ ở đó:

- **Tài liệu cuộc họp** (UC-33a), **tài liệu theo mục** (UC-33b) và **kho cá nhân** (UC-36): cả lần chọn đi chung một request,
  "tất cả hoặc không" (BR-286, POST-4 của UC-33a).
- **Bản ghi âm** (SCRUM-29): một request.

Đóng hộp thoại hoặc đổi trang là dừng lần tải. Một tệp lỗi làm cả lần chọn hỏng. Lỗi mạng thì người dùng phải chọn lại từ đầu.
PO yêu cầu: tải tệp ở đâu cũng phải có hàng đợi và thử lại.

BE đã có "hàng đợi" theo nghĩa khác: khoá `library:{ownerId}` trong `PersonalLibraryService`. Hai lần tải của cùng một người chờ
nhau trên server, để kiểm hạn mức 1 GB cho đúng. Người dùng không thấy khoá này, và nó không thử lại gì.

## Quyết định

1. **Hàng đợi nằm ở trình duyệt** (`features/uploads/`), trên mọi màn hình đã đăng nhập, không trong hộp thoại.
   - Hộp thoại chỉ chọn tệp và kiểm trước (đuôi, 50 MB, 10 tệp / 200 MB mỗi lần chọn, hạn mức kho), rồi đẩy vào hàng đợi và đóng.
   - Khay ở góc phải dưới cho thấy từng tệp: đang chờ, %, đang lưu, xong, lỗi, đã huỷ. Có huỷ, thử lại và bỏ.
2. **Mỗi tệp một request, chạy lần lượt từng tệp.**
   - Server vẫn "tất cả hoặc không" cho request đó: tài liệu cuộc họp và bản trong kho cùng được tạo hoặc cùng không (BR-194).
   - `MSG164` vẫn chỉ đúng tệp lỗi, ngay khi tệp đó được gửi.
   - Chạy lần lượt vì server cũng nhận lần lượt (khoá theo người), và mỗi tệp được cả đường truyền.
3. **Tự thử lại chỉ khi chắc chắn chưa lưu gì.** Có hai trường hợp:
   - trình duyệt chưa gửi hết dữ liệu;
   - server trả 5xx, 408 hoặc 429: server đã rollback trước khi trả lời.

   Khi đó hàng đợi tự thử lại 2 lần, sau 2 giây rồi 8 giây. Mất mạng thì chờ đến khi có mạng lại. Sau đó tệp dừng ở "lỗi" kèm
   nút Thử lại.
4. **Không tự thử lại khi có thể đã lưu**: mất kết nối, 502 hoặc 504 sau khi đã gửi hết dữ liệu. Khay báo "có thể tệp đã được
   lưu" và để người dùng xem danh sách trước khi bấm Thử lại, tránh tạo bản trùng.
5. **Lỗi 4xx không thử lại.** Ví dụ: cuộc họp đã huỷ, mất vai trò, sai định dạng, hết hạn mức. Màn hình đọc lại cuộc họp hoặc kho.
6. **Bản ghi âm chỉ nằm trong trình duyệt**, nên khi lỗi:
   - vẫn ở lại khay khi đóng khay;
   - có nút "Lưu về máy";
   - hỏi trước khi bỏ;
   - đóng tab thì trình duyệt hỏi lại.

## Khác với đặc tả

- **BR-286 / POST-4 của UC-33a** ("một tệp không thêm được thì không tệp nào được thêm") giờ áp **cho từng tệp**, không cho cả lần
  chọn. Các tệp khác vẫn vào cuộc họp, tệp lỗi chờ thử lại. BA cập nhật ở BA Final Review của SCRUM-58.
- **BR-319** (10 tệp / 200 MB mỗi lần) vẫn kiểm ở hộp thoại cho mỗi lần chọn. Server kiểm theo request, nên với một tệp mỗi
  request thì giới hạn này chỉ còn ở FE.
- **Thêm từ kho, gắn tài liệu có sẵn** (UC-33a từ kho, UC-33b) không có byte nào để gửi lại. Chúng vẫn là một request "tất cả hoặc
  không" (BR-304) ngay trong hộp thoại.

## Hệ quả

- Không đổi API, không cần migration. BE không phải sửa gì.
- Đóng hộp thoại, đổi trang hay vào phòng họp đều không dừng lần tải. Đóng hoặc tải lại tab thì trình duyệt hỏi trước.
- Đăng xuất thì dừng lần tải đang chạy: hàng đợi gắn với tài khoản.
- Link trong khay mở tab mới khi đang ở phòng họp, để không rớt cuộc gọi.

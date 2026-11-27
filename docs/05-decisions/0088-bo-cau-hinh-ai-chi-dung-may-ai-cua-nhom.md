# ADR-0088: Bỏ màn Cấu hình AI — mọi dự thảo do máy AI của nhóm viết

- **Trạng thái:** Đề xuất — Lộc 27/11/2026
- **Ngày:** 2026-11-27
- **Người quyết định:** Lộc
- **Thay thế:** [ADR-0056](0056-ai-du-thao-qua-api-cho-hop-thuong.md) (quản trị chọn máy local hay API);
  điểm 5 của [ADR-0063](0063-may-ai-du-phong.md) (nút "Thử kết nối" báo từng máy). Giữ
  [ADR-0054](0054-ai-du-thao-bien-ban-chay-tai-cho.md) (AI chạy tại chỗ) và phần còn lại của ADR-0063 (máy dự phòng)

## Bối cảnh

ADR-0056 cho quản trị chọn API bên ngoài (DeepSeek V4.1 qua OpenCode Zen) để viết dự thảo của cuộc họp Thường, key lưu
mã hoá trong bảng `ai_provider_settings`. Các báo cáo nộp hội đồng đã bỏ mọi chỗ nói lời thoại được gửi tới dịch vụ AI
bên ngoài (PR #557), nên code còn đường API là code làm khác tài liệu. Giữ đường API cũng có nghĩa giữ một bí mật trong
database, giữ thông báo ghi âm "lời thoại có thể được gửi tới dịch vụ AI bên ngoài" và giữ nghĩa vụ báo cho người họp về
việc chuyển dữ liệu cá nhân ra ngoài (Luật 91/2025/QH15). Máy dự phòng của ADR-0063 đã giải được việc máy chính tắt.

## Quyết định

1. **Bỏ màn Cấu hình AI** (`/mgmt/admin/ai`) và ba endpoint `GET`, `PUT /api/admin/ai`, `POST /api/admin/ai/test`.
   Mã `MSG408`–`MSG413` ngừng dùng, không tái cấp.
2. **Mọi cuộc họp, mọi mức mật, dùng máy AI của nhóm** (`Ai:BaseUrl`, `Ai:Model`, máy dự phòng `Ai:BackupBaseUrl`
   như ADR-0063). Bỏ cách gọi riêng cho API (đọc cả mục chương trình trong một lần, viết song song, thử lại khi `429`,
   `reasoning_effort`) và các khoá `Ai:EncryptionKey`, `Ai:ApiMaxTokens`, `Ai:ApiReasoningEffort`, `Ai:ApiParallelism`.
3. **Migration `RemoveAiProviderSetting` xoá bảng `ai_provider_settings`**, kể cả key API đã mã hoá: server không còn
   giữ bí mật nào của nhà cung cấp ngoài.
4. **Sự kiện kiểm toán `ai_settings.updated` không còn được ghi** và ra khỏi bộ lọc của Nhật ký hệ thống. Dòng cũ (chỉ có
   trên server test) vẫn còn, hiện bằng mã sự kiện vì không còn tên.
5. **Thông báo ghi âm ở màn chờ** nói cùng một câu cho mọi mức mật: dự thảo biên bản do máy AI của nhóm viết, lời thoại
   không ra khỏi hệ thống. Thông báo của Khách vẫn không nhắc tới AI (PO 14/11/2026).
6. **`MSG394` giữ mã, bỏ vế "Liên hệ quản trị hệ thống"**: nay là `Máy AI chưa sẵn sàng. Vui lòng thử lại sau.`, vì
   quản trị không còn màn nào để sửa. Lỗi máy chưa cấu hình ghi trên dự thảo cũng đổi thành `Máy AI chưa sẵn sàng.`
7. Không còn nút "Thử kết nối". Biết máy AI có sẵn sàng không thì xem `aiReady` của dự thảo, hoặc tạo dự thảo: máy tắt thì
   dự thảo báo lỗi kèm lý do (`Không kết nối được máy AI…`).

## Phương án đã cân nhắc

| Phương án | Ưu | Nhược | Vì sao không chọn |
|---|---|---|---|
| Giữ code, chỉ để chế độ mặc định là máy local | Không phải sửa code | Code, thông báo ghi âm và tài liệu nói khác nhau; bí mật vẫn nằm trong database | Hội đồng đọc tài liệu và chạy app: hai bên phải khớp |
| Giữ màn hình, bỏ lựa chọn API | Còn nút Thử kết nối | Một màn hình chỉ để xem tên model và bấm một nút | Không đáng một màn, một UC và ba endpoint |

## Hệ quả

- Dự thảo của cuộc họp Thường chậm như mọi cuộc họp khác (khoảng 5–8 phút trên máy 7B, ADR-0056 mục Bối cảnh) và cần máy
  chính hoặc máy dự phòng đang bật.
- Lời thoại không còn đường nào ra khỏi hệ thống của nhóm.
- Ít hơn một bảng (`ai_provider_settings`), một màn hình, ba endpoint, sáu mã thông báo.
- Wireframe lên v17 (bỏ màn 3.4, mục Cấu hình AI ở sidebar). Report 3 chụp lại 33 hình màn hình từ v17 cho hết mục Cấu hình AI
  ở sidebar và sửa câu `MSG394`; Report 3 không có UC cấu hình AI. Hướng dẫn sử dụng (Report 6) và các test case của màn
  (TRANS_API_IT92, IT94–IT101, TRANS_FT58–FT64) gỡ ở bước sau.

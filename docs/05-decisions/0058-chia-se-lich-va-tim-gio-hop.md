# ADR-0058: Có chia sẻ lịch (chỉ bận/rảnh) và Tìm giờ họp như AI-eCabinet; không có trợ lý AI

- **Trạng thái:** Đề xuất — PO (Hải) 07/11/2026
- **Ngày:** 2026-11-08
- **Người quyết định:** Hải (PO)
- **Thay:** nửa câu "Tìm giờ họp" trong mục "Những gì của eCab **không** đưa sang" của
  [ADR-0043](0043-thanh-phan-theo-ai-ecabinet-giu-sidebar.md). Phần còn lại của câu đó (trợ lý AI, Quản lý chung, sơ đồ
  chỗ ngồi, "Công việc hôm nay") vẫn giữ.
- **Liên quan:** SCRUM-142 (chia sẻ lịch), SCRUM-137 (tìm giờ họp), eCab US #352 / #353,
  [ADR-0057](0057-ba-muc-mat-nhu-ai-ecabinet.md) (3 mức mật), note
  [36](../notes/ban-giao-ba-final-review/36-chuan-bi-dac-ta-chia-se-lich-va-tim-gio-hop.md) (phạm vi D1–D11)

## Bối cảnh

ADR-0043 ghi TrustCabinet không có "Trợ lý AI, Tìm giờ họp" của AI-eCabinet. Ngày 07/11/2026 PO chốt làm Tìm giờ họp
và chia sẻ lịch (SCRUM-137, SCRUM-142), nhưng vẫn không làm trợ lý AI. Đặc tả (FE-16, UC-84, UC-85) đang ở PR #282. Nếu
ADR-0043 vẫn là quyết định duy nhất về việc này thì tài liệu tự mâu thuẫn: một bên nói không có, bên kia đặc tả nó.

Muốn tìm giờ rảnh chung thì phải biết lịch bận của người khác. Nhưng hiện `GET /api/meetings` chỉ trả cuộc họp người
gọi được xem, và cuộc họp của người khác có thể là Nội bộ hoặc Mật.

## Quyết định

1. **Có Tìm giờ họp và chia sẻ lịch**, gom vào một FE mới, *FE-16 Lịch làm việc & tìm giờ họp* (Should-have). FE-16
   gồm màn lịch hiện có, tab *Tìm khung giờ* và tab *Lịch được chia sẻ*.
2. **Không có trợ lý AI hỏi–đáp** (eCab US #353): không chat, không công cụ AI tìm người hay tạo cuộc họp.
3. **Chia sẻ lịch chỉ cho thấy bận/rảnh.** Người xem không bao giờ thấy tên, người dự hay địa điểm cuộc họp của chủ lịch.
4. **Ai xin, ai cho:** người xin phải có quyền tạo cuộc họp (`MeetingCreator`). Chủ lịch là mọi tài khoản nội bộ đang
   hoạt động, trừ chính người xin. Khách không có lịch. Không thêm capability hay vai trò mới.
5. **Hai mốc chia sẻ** theo 3 mức mật của ADR-0057:
   - mốc 1 thấy họp Thường;
   - mốc 2 thấy Thường và Nội bộ;
   - họp **Mật không bao giờ được chia sẻ**, kể cả chỉ báo bận.

   Vì vậy giờ của một cuộc họp Mật hiện là rảnh với người xem lịch, và Tìm khung giờ có thể đề xuất trùng giờ đó. PO
   chấp nhận điều này: vẫn cho tạo cuộc họp, người có họp Mật trùng giờ thì từ chối lời mời. Chặn việc tạo cuộc họp
   cũng chính là để lộ rằng người đó có họp Mật lúc ấy.
6. **Chỉ Chủ toạ, Thư ký và Thành viên của một cuộc họp tính là bận.** Người tạo, người duyệt và Khách không tính.
7. **Giữ các con số của eCab:**
   - tối đa 10 người;
   - thời lượng 15 phút đến 8 giờ, mặc định 2 giờ;
   - khoảng tìm tối đa 31 ngày;
   - thứ 2 đến thứ 6, 08:00–17:00, nghỉ 12:00–13:00;
   - đệm 0–4 giờ, mặc định 30 phút;
   - bước nhảy 5 phút đến 2 giờ, mặc định 30 phút.
8. **Ngày nằm ngoài khoảng chia sẻ là "chưa biết"**, không phải rảnh. Người chưa chia sẻ lịch hiện "Chưa được chia sẻ
   lịch".
9. **Tạo cuộc họp từ một khung giờ** mở hộp thoại tạo cuộc họp đã điền sẵn. Server kiểm lại khung giờ trước khi lưu,
   và cuộc họp vẫn đi luồng Nháp → duyệt như mọi cuộc họp khác.

## Phương án đã cân nhắc

| Phương án | Ưu | Nhược | Kết luận |
|---|---|---|---|
| A — Chia sẻ lịch có đồng ý, chỉ bận/rảnh, như eCab | Chủ lịch quyết ai thấy lịch mình; không lộ nội dung cuộc họp; theo nghiệp vụ tham chiếu | Thêm bảng chia sẻ, luồng xin/chấp nhận, thông báo hai chiều | **Chọn** |
| B — Mọi người có `MeetingCreator` thấy bận/rảnh của mọi người | Đơn giản, không cần luồng xin | Ai tạo được cuộc họp cũng dò được lịch của Chủ toạ, không cần ai đồng ý; lệch eCab | Bỏ |
| C — Giữ ADR-0043, không làm Tìm giờ họp | Không phát sinh việc | PO đã chốt làm; SCRUM-137, SCRUM-142 bỏ trống | Bỏ |
| Thêm trợ lý AI như eCab US #353 | Giống eCab hơn | Cần LLM gọi công cụ và thẻ xác nhận; tốn thời gian, không phải yêu cầu của hội đồng | Bỏ (điểm 2) |
| Có mốc thứ 3 thấy cả họp Mật (chỉ báo bận) như eCab | Tìm giờ không đề xuất trùng họp Mật | Lộ rằng người đó có họp Mật lúc ấy | Bỏ (điểm 5) |

## Hệ quả

- **Dữ liệu:** có thêm một bảng chia sẻ lịch. Tên bảng, cột và ràng buộc "một chia sẻ đang chờ hoặc đang hiệu lực mỗi
  cặp" do BE đặt khi làm, ghi vào [02-erd](../02-design/02-erd.md) cùng migration.
- **API:** có endpoint bận/rảnh riêng, lọc ở server theo mốc chia sẻ. `GET /api/meetings` không đổi.
- **Đặc tả:** FE-16, UC-84 (chia sẻ lịch), UC-85 (tìm giờ họp), BR-414 → BR-430 và `MSG435`–`MSG455` ở PR #282.
  Report 3 §I.5.1 và §III chuyển màn lịch hiện có sang FE-16.
- **Không làm:** trợ lý AI hỏi–đáp. README mục Ngoài phạm vi ghi thêm dòng này.
- **ADR-0043** đổi trạng thái thành "nửa câu Tìm giờ họp thay thế bởi ADR-0058"; nội dung không sửa.

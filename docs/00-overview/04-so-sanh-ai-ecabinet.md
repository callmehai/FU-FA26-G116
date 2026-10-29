# So sánh với AI-eCabinet

Ghi lại sau khi khảo sát bản demo nội bộ và mã nguồn tham chiếu của AI-eCabinet, ngày
2026-09-18. Đường dẫn demo và tài khoản thử không ghi ở đây vì repo này công khai — hỏi trong
nhóm nếu cần.

## Hệ thống gốc

| Khía cạnh | AI-eCabinet |
|---|---|
| Backend | Go, modular monolith, bounded context tách theo `modules/<ctx>/` |
| Frontend | React + TypeScript + Vite, shadcn/ui + Tailwind |
| Hợp đồng API | OpenAPI là nguồn duy nhất, sinh ra client TS và stub Go |
| Phân quyền | OpenFGA, một điểm quyết định (PDP) duy nhất |
| Hạ tầng | Postgres, NATS, worker Python cho AI (ASR, diarize, voiceprint, tóm tắt) |
| Xác thực | Zitadel (headless), phiên dạng opaque |
| Bảo mật | PKCS#11/TPM, chuỗi băm audit, triển khai được trong mạng cô lập |

## TrustCabinet giữ lại gì

- **Từ vựng nghiệp vụ và vòng đời cuộc họp** — xem [vòng đời](../02-design/01-vong-doi-cuoc-hop.md)
- **Mô hình cuộc họp**: một cuộc họp là một buổi họp, không có cấp phiên họp — chữ "phiên họp" trên UI của
  AI-eCabinet là mục agenda ([ADR-0013](../05-decisions/0013-mot-cuoc-hop-la-mot-buoi-hop.md)); biểu quyết ngay trong
  cuộc họp, chủ trì ghi kết luận Thông qua / Không thông qua / Hoãn ([ADR-0014](../05-decisions/0014-bieu-quyet-chi-trong-cuoc-hop.md))
- **Bố cục giao diện** ban đầu bám theo AI-eCabinet; nay là một sidebar dọc theo
  [ADR-0012](../05-decisions/0012-giao-dien-hanh-chinh-sidebar-doc.md); bên trong lấy lại thành phần của AI-eCabinet (chip trạng thái
  có chấm, nút vàng cho việc cần làm ngay, tiến trình cuộc họp, hộp thoại tạo cuộc họp 2 cột) theo
  [ADR-0043](../05-decisions/0043-thanh-phan-theo-ai-ecabinet-giu-sidebar.md)
- **Cách chia màn chi tiết cuộc họp thành tab** theo giai đoạn nghiệp vụ
- **Nguyên tắc**: giao diện không phải ranh giới bảo mật — ẩn menu không thay được kiểm tra quyền
  ở phía server; route thiếu quyền trả 404 chứ không phải 403, để không lộ sự tồn tại của màn hình

## TrustCabinet làm khác

| Khía cạnh | TrustCabinet | Vì sao |
|---|---|---|
| Backend | ASP.NET Core 10 | Yêu cầu môn học, nhóm quen C# |
| Phân quyền | RBAC đơn giản trong ứng dụng | OpenFGA quá nặng cho một kỳ đồ án |
| Gọi thoại/video | Phòng họp trực tuyến trên LiveKit SFU tự host ([ADR-0010](../05-decisions/0010-phong-hop-truc-tuyen-dung-livekit-sfu.md)) | **Điểm khác biệt chính** — bản gốc không có |
| AI | Gọi dịch vụ ASR bên ngoài | Không tự vận hành mô hình |
| Xác thực | Email + mật khẩu, hoặc đăng nhập Google | Không dựng IdP riêng như Zitadel cho một kỳ đồ án |
| Ký số | Mô phỏng | Không có HSM/USB token thật |
| Triển khai | Một máy chủ | Không làm bản air-gapped |

## Ranh giới cần giữ

Mã nguồn AI-eCabinet là repo private của Trustsoft, **không có giấy phép mở**. Nhóm dùng nó để
hiểu nghiệp vụ và tham chiếu thiết kế, rồi tự viết bản hiện thực của TrustCabinet. Không sao chép
tệp mã nguồn vào repo này — vừa là vấn đề bản quyền, vừa là vấn đề liêm chính học thuật khi đồ án
được chấm điểm.

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
- **Bố cục giao diện**: hai không gian làm việc dùng chung một khung, sidebar trái, thanh trên có
  bộ chuyển không gian
- **Cách chia màn chi tiết cuộc họp thành tab** theo giai đoạn nghiệp vụ
- **Nguyên tắc**: giao diện không phải ranh giới bảo mật — ẩn menu không thay được kiểm tra quyền
  ở phía server; route thiếu quyền trả 404 chứ không phải 403, để không lộ sự tồn tại của màn hình

## TrustCabinet làm khác

| Khía cạnh | TrustCabinet | Vì sao |
|---|---|---|
| Backend | ASP.NET Core 10 | Yêu cầu môn học, nhóm quen C# |
| Phân quyền | RBAC đơn giản trong ứng dụng | OpenFGA quá nặng cho một kỳ đồ án |
| Gọi thoại/video | WebRTC + SignalR, tích hợp sẵn | **Điểm khác biệt chính** — bản gốc không có |
| AI | Gọi dịch vụ ASR bên ngoài | Không tự vận hành mô hình |
| Ký số | Mô phỏng | Không có HSM/USB token thật |
| Triển khai | Một máy chủ | Không làm bản air-gapped |

## Ranh giới cần giữ

Mã nguồn AI-eCabinet là repo private của Trustsoft, **không có giấy phép mở**. Nhóm dùng nó để
hiểu nghiệp vụ và tham chiếu thiết kế, rồi tự viết bản hiện thực của TrustCabinet. Không sao chép
tệp mã nguồn vào repo này — vừa là vấn đề bản quyền, vừa là vấn đề liêm chính học thuật khi đồ án
được chấm điểm.

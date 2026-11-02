# ADR-0049: Vô hiệu hoá tài khoản kết thúc Voice ID; máy so giọng dưới khoá trên dòng đồng ý

- **Trạng thái:** Đề xuất — SCRUM-35, review 02/11/2026
- **Ngày:** 2026-11-02
- **Người quyết định:** Hải (PO)

## Bối cảnh

Voice ID ([ADR-0027](0027-voice-id-co-dong-y.md)) giữ giọng mẫu — dữ liệu cá nhân nhạy cảm theo Luật
91/2025/QH15 — dưới một đồng ý rút được. Review ngày 02/11/2026 tìm ra hai chỗ giọng mẫu hoặc thứ suy
ra từ nó còn sống sau khi lẽ ra phải hết:

1. **Tài khoản bị vô hiệu hoá giữ giọng mẫu mãi.** Tài khoản không bao giờ bị xoá, chỉ vô hiệu hoá
   (SCRUM-13). Người bị vô hiệu hoá không đăng nhập được nên không tự rút đồng ý được. Giọng mẫu nằm lại
   không thời hạn, và chỉ một bộ lọc `is_active` lúc so giọng ngăn nó được dùng.
2. **Rút đồng ý trong lúc máy đang chuyển chữ.** Máy đọc giọng mẫu, so, rồi mới ghi gợi ý và tên tự gán.
   Rút đồng ý chen vào giữa hai bước đó thì xoá xong trước, máy ghi sau: gợi ý và tên máy gán cho người
   vừa rút vẫn còn. Test `A_withdrawal_while_the_worker_matches_still_clears_what_it_writes` tái hiện được.

## Quyết định

1. **Vô hiệu hoá tài khoản kết thúc Voice ID của người đó, như khi họ tự rút đồng ý:** xoá mọi giọng mẫu,
   bỏ gợi ý và tên máy tự gán là người đó, đóng dòng `voice_consents`. Tên Thư ký đã gán giữ nguyên.
   Chạy trong cùng transaction với việc đổi `is_active`, nên không bao giờ có tài khoản đã vô hiệu hoá mà
   giọng mẫu còn. Nhật ký ghi `voice.consent_withdrawn` với `reason: "accountDeactivated"`, người làm là
   quản trị viên.
2. **Kích hoạt lại không khôi phục gì.** Người đó đồng ý và ghi giọng mẫu lại. Giữ giọng mẫu "phòng khi
   kích hoạt lại" là giữ dữ liệu nhạy cảm không mục đích.
3. **Rút đồng ý và việc so giọng loại trừ nhau trên dòng đồng ý.** Lúc ghi kết quả chuyển chữ, máy khoá
   chia sẻ (`FOR SHARE`) dòng đồng ý của những người nó so, rồi mới đọc giọng mẫu, và giữ khoá tới khi ghi
   xong. Rút đồng ý (và vô hiệu hoá) khoá dòng đó để sửa (`FOR UPDATE`), nên hoặc chờ máy ghi xong rồi xoá
   cả thứ máy vừa ghi, hoặc xong trước và máy không còn giọng mẫu để so. Máy so giọng trước khi xoá đoạn
   và người nói cũ của bản gỡ băng, để hai bên luôn lấy khoá theo cùng một thứ tự và không khoá chết nhau.

Một code dùng chung cho cả hai đường kết thúc: `IVoiceIdErasure`.

## Phương án đã cân nhắc

| Phương án | Ưu | Nhược | Vì sao không chọn |
|---|---|---|---|
| Chỉ lọc tài khoản không hoạt động khi so giọng (đã có) | Không đổi gì | Giọng mẫu vẫn nằm trong database không thời hạn, không ai xoá được | Luật 91/2025 đòi xoá khi hết mục đích; người bị vô hiệu hoá không tự rút được |
| Giữ giọng mẫu, xoá sau một thời hạn | Kích hoạt lại thì dùng tiếp | Thêm một job và một thời hạn phải biện minh | Không có nhu cầu dùng lại; ghi lại mẫu mất một phút |
| Kiểm tra lại đồng ý ngay trước khi ghi, không khoá | Không thêm khoá | Vẫn còn khe giữa lần kiểm và lúc commit | Không đóng được race |

## Hệ quả

- Quản trị viên vô hiệu hoá nhầm rồi kích hoạt lại thì người đó phải ghi giọng mẫu lại.
- Rút đồng ý có thể phải chờ tới khi máy ghi xong một bản gỡ băng (vài trăm ms, chỉ phần ghi kết quả;
  phần chuyển chữ ở worker chạy ngoài transaction).

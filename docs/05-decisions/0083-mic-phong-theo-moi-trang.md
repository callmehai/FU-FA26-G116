# ADR-0083: Mic phòng đang bật đi theo mọi trang

- **Trạng thái:** Đề xuất — Hải báo khi test 24/11/2026
- **Ngày:** 2026-11-24
- **Người quyết định:** Hải
- **Thay:** ý "trang khác thả mic, lưu lượt" ở điểm 3 của [ADR-0072](0072-hop-ket-hop-mot-cong-tac-mic-phong.md).
  Phần còn lại của ADR-0072 giữ nguyên.
- **Liên quan:** [ADR-0041](0041-mic-phong-ghi-tung-luot.md), [ADR-0080](0080-mic-phong-luot-30-phut.md)

## Bối cảnh

Theo ADR-0072, mic phòng chỉ sống khi tab đang ở trang cuộc họp hoặc phòng họp của cuộc họp đó; sang trang khác (Lịch,
Danh sách cuộc họp…) thì mic tắt và lượt được lưu. Khi test họp trực tiếp ngày 24/11, Thư ký chuyển sang trang khác thì
thẻ mic biến mất, không biết phòng còn được ghi hay không.

## Quyết định

1. **Mic đang bật (đang mở, đang ghi hoặc đang lưu) thì giữ nguyên khi tab sang bất kỳ trang nào**, kể cả trang của cuộc
   họp khác. Thẻ mic nổi ở góc như trên trang cuộc họp, với tên cuộc họp đang ghi, để Thư ký thấy và tắt được.
2. Mic đang tắt thì như cũ: rời trang cuộc họp là thả.
3. Tắt mic trên thẻ, cuộc họp kết thúc, đăng xuất hoặc đóng tab thì lưu lượt như trước.

## Nghĩa vụ theo Luật 91/2025/QH15

Không đổi gì: tiếng được ghi, mục đích, mốc đồng ý và thời hạn lưu giữ như ADR-0041 và ADR-0072. Thẻ nổi luôn hiện khi
mic bật, nên việc đang ghi không bị che.

## Hệ quả

- Thư ký đi xem lịch, tài liệu hay cuộc họp khác trong lúc họp mà phòng vẫn được ghi.
- Trên trang cuộc họp khác, thẻ là của cuộc họp đang ghi; cuộc họp đang xem không mở được mic riêng cho tới khi tắt mic kia.
- Không đổi backend.

# ADR-0046: Trang cá nhân vào menu tài khoản, lối tắt ghi giọng mẫu ở màn chờ vào phòng

- **Trạng thái:** Đề xuất — PO 02/11/2026
- **Ngày:** 2026-11-02
- **Người quyết định:** Hải (PO)
- **Thay:** phần nhóm **Nghiệp vụ** trong [ADR-0012](0012-giao-dien-hanh-chinh-sidebar-doc.md) (Lịch làm việc, Cuộc họp,
  Kho tài liệu). Sidebar dọc, nhóm Hệ thống, tài khoản và chuông ở chân sidebar giữ nguyên.
- **Theo:** AI-eCabinet `origin/main`:
  - `clients/web/src/components/shell/nav.ts`: thanh điều hướng chỉ có các mục nghiệp vụ;
  - `clients/web/src/components/shell/UserMenu.tsx`: những gì thuộc về một người nằm trong menu avatar.

  Lấy logic, không lấy giao diện: TrustCabinet giữ sidebar dọc, không có thanh trên.

## Bối cảnh

Sidebar có bốn mục nghiệp vụ, trong đó hai mục là của riêng từng người: **Kho tài liệu** và **Giọng nói của tôi**.
Thêm trang nào nữa thì sidebar càng dài, và việc chung với việc riêng lẫn vào nhau.

Người vào họp vội không biết giọng mẫu ghi ở đâu. Không có giọng mẫu thì bản gỡ băng không nhận ra họ, Thư ký phải
gán tay.

## Quyết định

1. **Sidebar chỉ còn việc chung:** Lịch làm việc, Cuộc họp, và nhóm Quản trị theo quyền.
2. **Menu tài khoản** ở chân sidebar gồm: tên, email, vai trò; **Kho tài liệu**, **Giọng nói của tôi**; **Đăng xuất**
   (hỏi lại trước). Đang ở một trang cá nhân thì nút tài khoản sáng lên thay cho mục sidebar. URL giữ nguyên.
3. **Màn chờ vào phòng họp trực tuyến:**
   - Thành viên chưa có giọng mẫu thấy một dòng "Chưa có giọng mẫu · Ghi ngay". Lợi ích nằm trong tooltip.
   - Bấm vào thì mở thông báo đồng ý đầy đủ (nếu chưa đồng ý), rồi hộp thoại đọc đoạn văn mẫu, ngay tại chỗ.
   - Đã có giọng mẫu thì không hiện gì. Khách không thấy dòng này, vì Khách không có Voice ID.

## Nghĩa vụ theo Luật 91/2025/QH15

Không đổi so với [ADR-0027](0027-voice-id-co-dong-y.md) và [ADR-0042](0042-luu-ban-ghi-giong-mau-de-nghe-lai.md).
Lối tắt dùng chung thông báo đồng ý và hộp thoại ghi mẫu với trang "Giọng nói của tôi". Người dùng vẫn đọc đủ thông
báo trước khi đồng ý; tooltip không thay cho thông báo. Không bật thì vẫn vào họp bình thường.

## Hệ quả

- Sidebar ngắn lại, chỉ còn việc chung. Trang cá nhân xa thêm một cú bấm, như eCab.
- Wireframe và ảnh trong Report 3 vẫn vẽ sidebar bốn mục. Cập nhật ở lần sửa wireframe sau.

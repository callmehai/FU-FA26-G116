# ADR-0030: Ghi cả cuộc gọi từ trình duyệt của Thư ký

- **Trạng thái:** Đề xuất
- **Ngày:** 2026-10-26
- **Người quyết định:** Hải (PO, FE), theo SCRUM-44

## Bối cảnh

Cuộc họp chọn **Thu chung cả phòng** (ADR-0029) thì Thư ký lưu một bản ghi chung, bằng một trong hai cách:

1. Ghi trên trình duyệt (SCRUM-29).
2. Thu bằng thiết bị khác (ví dụ điện thoại) rồi tải tệp lên (SCRUM-29).

Ghi trên trình duyệt chỉ thu **micro của Thư ký**. Trong họp trực tuyến, người khác nói qua cuộc gọi
nên giọng họ không có trong bản ghi. SCRUM-44 yêu cầu ghi được cả cuộc gọi, và mọi người trong phòng,
kể cả người vào sau, đều thấy đang ghi.

Có ba cách:

| Cách | Được | Mất |
|---|---|---|
| Bỏ, họp trực tuyến chỉ dùng micro từng người | Không phải làm gì | Không có bản ghi âm chung của cuộc gọi |
| **Trình duyệt Thư ký trộn âm thanh của cuộc gọi** | Không thêm dịch vụ nào trên server, dùng lại luồng lưu và gỡ băng của SCRUM-29 | Thư ký phải ở trong phòng suốt lúc ghi |
| LiveKit Egress ghi trên server | Không phụ thuộc máy Thư ký | Thêm một dịch vụ cần 2–4 GB RAM; server test chỉ có 1 GB |

PO chốt ngày 26/10/2026: Thư ký ghi từ máy mình, **phải thu cả micro của Thư ký lẫn giọng người khác
nói**, hoặc thu bằng điện thoại rồi tải lên.

## Quyết định

1. **Trong phòng họp, nút Ghi âm thu đúng những gì cuộc gọi nghe được.**
   - Trình duyệt Thư ký trộn bằng Web Audio: micro Thư ký đang phát vào cuộc gọi, cộng âm thanh của
     mọi người khác (`mixCall`).
   - Lấy thẳng từ các luồng của cuộc gọi, không qua loa. Không ai bị thu hai lần, không có tiếng vọng.
   - Thư ký tắt micro trong cuộc gọi thì bản ghi cũng không có giọng Thư ký.
   - Ai vào, ra hay đổi micro giữa chừng thì bản trộn tự cập nhật theo.
   - Tệp ghi ra như bản ghi trên trình duyệt hiện có (WebM/Opus), lưu và gỡ băng qua đúng luồng SCRUM-29/31.
   - Trên trang chi tiết cuộc họp (họp trực tiếp, mọi người ngồi quanh máy Thư ký), Ghi âm vẫn chỉ thu micro.
2. **Bộ ghi do phòng họp giữ, không phải panel.**
   - Đóng panel Bản ghi vẫn ghi tiếp.
   - Rời phòng trong lúc ghi thì bị chặn ở hộp thoại Rời phòng, vì bản ghi chỉ nằm trong tab này.
3. **Mọi người thấy đang ghi qua kênh dữ liệu của cuộc gọi, không qua server.**
   - Bộ ghi chạy trong tab Thư ký nên server không biết đang ghi. Trong lúc ghi, tab đó gửi `{ "recording": true }`
     trên topic `recording` mỗi 4 giây, và gửi `false` khi dừng hoặc tạm dừng.
   - Người vào sau thấy ở nhịp kế tiếp. Không nghe thêm nhịp nào sau 10 giây (tab bị đóng, mất mạng) thì
     chip tắt, để không báo "đang ghi" mãi.
   - Chỉ nhận nhịp từ người có vai trò **Thư ký** theo danh sách người dự, nên người khác không giả được.
   - Token LiveKit giữ `CanUpdateOwnMetadata = false`, vì metadata chứa vai trò. Dùng kênh dữ liệu
     (`CanPublishData`) đã có sẵn.

## Hệ quả

- Không thêm endpoint, bảng hay mã thông báo nào.
- Tab Thư ký bị đóng hoặc máy tắt thì mất phần đang ghi, như ghi trên trang chi tiết. Muốn chắc
  ăn thì thu thêm bằng điện thoại rồi tải lên.
- Thư ký rớt mạng thì không nghe được người khác trong lúc đó, và bản ghi cũng không có đoạn ấy.
- Chủ tọa kết thúc cuộc họp trong lúc Thư ký đang ghi thì phòng đóng và bản ghi mất. Thư ký cần dừng
  và lưu trước. Chưa xử lý tự động.

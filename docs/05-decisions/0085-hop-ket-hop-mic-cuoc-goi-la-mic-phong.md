# ADR-0085: Họp kết hợp: mic của cuộc gọi là mic phòng, một trạng thái; phòng họp trực tuyến không có thẻ mic nổi

- **Trạng thái:** Chấp nhận — Khánh báo lỗi, Hải chốt 24/11/2026; Hải xác nhận 25/11/2026 giữ việc chủ toạ tắt mic Thư ký là dừng ghi cả phòng
- **Ngày:** 2026-11-24
- **Người quyết định:** Hải
- **Bổ sung:** [ADR-0072](0072-hop-ket-hop-mot-cong-tac-mic-phong.md) (họp kết hợp: một công tắc mic phòng trong phòng họp
  trực tuyến). Thay các ý sau của ADR-0072: thẻ nổi trong phòng họp khi ra khỏi cuộc gọi (điểm 3, gạch thứ ba), thẻ
  "Đang ghi ở tab khác" trên trang cuộc họp (điểm 2), và "Chủ toạ tắt mic của Thư ký chỉ tắt bản sao" (Hệ quả).
- **Liên quan:** [ADR-0041](0041-mic-phong-ghi-tung-luot.md), [ADR-0083](0083-mic-phong-theo-moi-trang.md)

## Bối cảnh

Khánh test họp kết hợp ngày 24/11 (video ở kênh báo lỗi): Thư ký đang ở phòng họp trực tuyến, bật mic. Tab trang cuộc
họp bên cạnh vẫn hiện thẻ mic nổi "Đang ghi ở tab khác". Tắt mic trong cửa sổ nổi (picture-in-picture) của cuộc gọi thì
nút mic đã tắt nhưng thẻ bên ngoài vẫn báo đang ghi. Hải: "bỏ đi 1 thẻ trong room là được, như Google Meet thôi, có
picture-in-picture mà lo gì, xoá hẳn đi"; và nhắc ca "tắt mic trong phòng họp mà bên ngoài vẫn mở mic".

Kiểm lại trên `app` (Playwright, micro giả) thấy bốn chỗ nút mic của cuộc gọi và mic phòng nói khác nhau:

1. **Trang cuộc họp ở tab bên cạnh** hiện thẻ "Đang ghi ở tab khác" cho lượt do chính phòng họp của trình duyệt này ghi.
   Hai nơi cùng nói về một mic; khi vừa tắt ở cuộc gọi, thẻ còn báo đang ghi cho tới khi lượt lưu xong.
2. **Chủ toạ tắt mic Thư ký**: cuộc gọi tắt tiếng bản sao, mọi người thấy Thư ký tắt mic, nhưng nút của Thư ký vẫn là
   "Tắt mic phòng" và phòng vẫn được ghi.
3. **Màn chờ khi quay lại phòng**: công tắc micro ở màn chờ theo lựa chọn lần trước (tắt), trong khi thẻ nổi ở góc báo
   "Đang ghi cả phòng". Hai công tắc trên một màn hình, nói ngược nhau.
4. **Vào phòng ở tab thứ hai** (hoặc máy khác): tab mới vào cuộc gọi, tab cũ bị đẩy ra nhưng vẫn giữ mic phòng và ghi tiếp;
   tab mới hiện "Bật mic phòng" (tắt), bấm thì báo mic đang bật ở tab khác.

## Quyết định

1. **Trong phòng họp trực tuyến không có thẻ mic nổi.** Nút mic của cuộc gọi là chỗ duy nhất bật tắt mic phòng, như
   Google Meet. Khi Thư ký sang tab khác, cửa sổ picture-in-picture của cuộc gọi có nút mic đó.
   - Màn chờ: công tắc micro của Thư ký họp kết hợp là mic phòng ("Bật mic phòng" / "Tắt mic phòng"). Mic phòng đang bật ở
     tab này thì công tắc bật, bất kể lần trước chọn gì; tắt ở đây là tắt mic phòng và lưu lượt.
   - Ra khỏi cuộc gọi mà mic phòng vẫn bật (mất kết nối, phòng chưa vào được): trang báo bằng một dòng "Mic phòng vẫn đang
     ghi" có nút "Tắt mic phòng", nằm trong trang, không nổi ở góc.
2. **Một trạng thái.** Nút mic của cuộc gọi và mic phòng luôn cùng bật hoặc cùng tắt:
   - tắt ở cuộc gọi (nút, phím tắt, cửa sổ picture-in-picture) là tắt mic phòng, lượt được lưu; bật là bật mic phòng;
   - **chủ toạ tắt mic Thư ký trong cuộc gọi là tắt mic phòng**, lượt được lưu; Thư ký thấy nút tắt và bật lại được;
   - **vào cuộc gọi ở tab hoặc máy khác thì mic phòng đi theo cuộc gọi**: tab bị đẩy ra tắt mic phòng và lưu lượt; tab mới
     vào với micro bật thì mở mic phòng ở đó;
   - bật mic ở cuộc gọi khi một tab khác của trình duyệt này còn giữ mic phòng thì tab đó được nhờ tắt (lưu lượt) và mic
     mở ở cuộc gọi, không báo lỗi "đang bật ở tab khác" (chờ tối đa 20 giây cho tab kia lưu xong);
   - bị xoá khỏi danh sách tham dự thì mic phòng tắt.
3. **Trang cuộc họp của họp kết hợp không hiện thẻ cho lượt do phòng họp của trình duyệt này ghi** (trước đây là "Đang ghi
   ở tab khác"). Lượt mở ở máy khác vẫn hiện thẻ với nút "Dừng ghi ở máy kia". Phòng họp không dùng được (dự phòng ở điểm 2
   của ADR-0072) thì thẻ trên trang cuộc họp vẫn là công tắc như cũ.
4. **Giữ ADR-0083 cho các trang ngoài phòng họp.** "Rời phòng" khi mic phòng đang bật thì mic vẫn ghi, tab về trang cuộc họp
   và thẻ nổi ở góc hiện ở đó (và ở mọi trang khác) để thấy và tắt. Vào lại phòng họp thì nút mic của cuộc gọi hiện bật.

## Nghĩa vụ theo Luật 91/2025/QH15

- Không ghi thêm gì, không đổi mục đích, mốc đồng ý hay thời hạn lưu (ADR-0041, ADR-0072).
- Việc đang ghi không bị che: trong cuộc gọi có nút mic và chip "Đang ghi cả phòng"; ở tab khác có cửa sổ
  picture-in-picture; ra khỏi cuộc gọi thì có dòng "Mic phòng vẫn đang ghi"; ở trang khác có thẻ nổi (ADR-0083).
- Chủ toạ tắt mic Thư ký thì phòng thôi được ghi, đúng với điều mọi người thấy trên cuộc gọi.

## Phương án đã cân nhắc

| Phương án | Ưu | Nhược | Vì sao không chọn |
|---|---|---|---|
| Giữ thẻ nổi trong phòng họp, chỉ sửa cho đồng bộ | Ít đổi | Vẫn hai chỗ bật tắt một mic | PO muốn một chỗ, như Google Meet |
| Rời phòng là tắt mic phòng | Giống hẳn Meet | Mất tiếng phòng khi Thư ký lỡ rời hay đổi tab ứng dụng; trái ADR-0083 | Phòng họp thật vẫn đang họp |
| Chủ toạ tắt chỉ bản sao (như ADR-0072) | Phòng vẫn được ghi | Nút của Thư ký nói bật trong khi mọi người thấy tắt | Đó chính là ca lệch Khánh gặp |

## Hệ quả

- Họp kết hợp: một mic, một chỗ bật tắt trong phòng họp; trang cuộc họp ở tab bên cạnh không còn thẻ lặp lại.
- Không đổi backend hay cơ sở dữ liệu. Các tab của một trình duyệt nhờ nhau tắt mic qua `BroadcastChannel`, vẫn giữ Web
  Lock của ADR-0041 (mỗi trình duyệt một tab giữ mic phòng).
- Chủ toạ tắt mic Thư ký thì một lượt được lưu và chuyển chữ lại như khi Thư ký tự tắt.
- Logic quyết định nằm ở `frontend/src/features/recordings/hybridMic.ts`, có test ở `frontend/tests/unit/hybridMic.test.ts`.

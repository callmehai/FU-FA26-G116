# ADR-0072: Họp kết hợp: một công tắc mic phòng, vừa ghi lượt mic phòng vừa phát vào phòng họp trực tuyến; mic phòng không tắt khi Thư ký đi giữa trang cuộc họp và phòng họp; mic phòng tự chia lượt mỗi 5 phút

- **Trạng thái:** Đề xuất — buổi test của nhóm 21/11/2026 (Lộc, Hải, Khánh)
- **Ngày:** 2026-11-22
- **Người quyết định:** Hải (PO)
- **Bổ sung:** [ADR-0061](0061-hop-ket-hop-mot-dong-thoi-gian.md) (họp kết hợp: phòng ghi bằng mic phòng, người trực
  tuyến ghi theo micro của mình). Không đổi điểm nào của ADR-0061.
- **Thay:** với họp kết hợp, điểm 4 của [ADR-0040](0040-cach-ghi-am-theo-hinh-thuc-hop.md) (thẻ ghi âm nổi trên trang
  cuộc họp là nơi bật mic phòng). Họp trực tiếp vẫn như cũ.
- **Liên quan:** [ADR-0041](0041-mic-phong-ghi-tung-luot.md) (mic phòng ghi từng lượt)

## Bối cảnh

Trong buổi test họp kết hợp ngày 21/11, Thư ký ngồi ở phòng họp cạnh mic phòng. Muốn người trực tuyến nghe được phòng,
Thư ký phải vào cả phòng họp trực tuyến. Kết quả:

- Thư ký phải bật **hai mic ở hai nơi**: thẻ mic phòng trên trang cuộc họp (để ghi) và mic của cuộc gọi (để người trực
  tuyến nghe).
- Cùng một tiếng phòng bị ghi **hai lần**: một lần thành lượt mic phòng, một lần thành các câu theo micro của Thư ký. Bản
  dự thảo đọc lời phòng họp hai lần, lần sau dưới tên Thư ký.
- Đi từ trang cuộc họp sang phòng họp **trong cùng cửa sổ** (ứng dụng cài trên máy, trình duyệt chặn tab mới) làm mất thẻ
  mic phòng và mic tự tắt: thẻ do trang cuộc họp giữ, rời trang là dừng.

Nhóm chốt: họp kết hợp thì mic phòng nằm trong phòng họp trực tuyến, chỉ một công tắc; họp trực tiếp giữ thẻ nổi như cũ.

## Quyết định

1. **Họp kết hợp: nút mic của Thư ký trong phòng họp trực tuyến là mic phòng.**
   - Nút đổi tên thành "Bật mic phòng" / "Tắt mic phòng". Bật là mở một lượt mic phòng như ADR-0041 (hỏi xác nhận đã báo
     mọi người trước lượt đầu tiên của cuộc họp), tắt là lưu lượt đó.
   - Trong khi bật, trình duyệt gửi **một bản sao của chính tiếng mic phòng** vào cuộc gọi làm micro của Thư ký. Người
     trực tuyến nghe phòng qua đó, không cần mic thứ hai.
   - Thư ký vào phòng với micro bật ở màn chờ thì mic phòng tự mở sau khi vào. Cuộc gọi không mở micro riêng của Thư ký.
   - Ghi theo micro từng người (SCRUM-109) **tắt** với Thư ký của họp kết hợp: lời họ đã nằm trong lượt mic phòng.
   - Đầu trang phòng họp có chip "Đang ghi cả phòng" khi mic phòng bật.
   - Chọn micro khác trong phần cài đặt âm thanh của phòng thì mic phòng chuyển sang micro đó, lượt ghi vẫn tiếp tục.
2. **Trang cuộc họp của họp kết hợp không còn chỗ bật mic phòng.** Thẻ nổi chỉ hiện khi mic phòng đang bật ở tab này, khi
   máy còn lượt ghi dở, hoặc khi có lượt đang mở ở nơi khác. Lượt đang mở do tab phòng họp của cùng trình duyệt ghi thì
   thẻ ghi "Đang ghi ở tab khác" và không có nút "Lưu lượt đó" (bấm sẽ cắt mic của tab kia).
3. **Mic phòng do ứng dụng giữ, không do trang.** Bộ ghi mic phòng nằm trên các route (`RoomMicProvider`, trong
   `RequireAuth`), theo cuộc họp có trên URL: trang cuộc họp hoặc phòng họp của cuộc họp đó. Vì vậy:
   - đi giữa trang cuộc họp và phòng họp trong cùng một tab không tắt mic;
   - sang trang khác (danh sách cuộc họp, lịch…) thì mic tắt và lượt được lưu, như rời trang cuộc họp trước đây;
   - ra khỏi cuộc gọi (rời phòng, mất kết nối, màn chờ) mà mic phòng vẫn bật thì thẻ nổi hiện ở đó để thấy và tắt được;
   - cuộc họp kết thúc thì mic tự tắt và lưu lượt, kể cả khi không có trang cuộc họp nào đang mở.
4. **Tab nào giữ mic phòng thì trang cuộc họp và phòng họp dùng chung tab đó.** Trong trình duyệt thường, phòng họp mở ở
   tab riêng (PO 19/11). Một luồng âm thanh không chuyển được sang tab khác, nên khi tab đang giữ mic phòng:
   - "Rời phòng" hay "Về trang cuộc họp" mở trang cuộc họp ngay trong tab đó, không đóng tab;
   - "Vào phòng họp" từ trang cuộc họp đang giữ mic cũng mở phòng ngay trong tab đó.
   Nút "Trang cuộc họp" trên đầu phòng họp vẫn mở tab trang cuộc họp và giữ cuộc gọi: mic phòng ở lại tab phòng họp.
   Mỗi trình duyệt vẫn chỉ một tab giữ mic phòng của một cuộc họp (Web Lock của ADR-0041).
5. **Mic phòng tự chia lượt mỗi 5 phút** (Lộc, buổi test 21/11; áp dụng cả họp trực tiếp và họp kết hợp).
   - Mic còn bật thì cứ đủ 5 phút tiếng (`TAKE_SPLIT_SECONDS`) trình duyệt tự lưu lượt đang ghi và mở ngay lượt tiếp
     theo. Công tắc vẫn "bật", không hỏi lại xác nhận (chỉ hỏi trước lượt đầu), không hiện thông báo mỗi lần lưu.
   - Server chỉ cho một lượt mở mỗi cuộc họp, nên lượt sau mở khi lượt trước đã lưu. Tiếng nghe được trong khoảng chờ đó
     nằm lại trên máy rồi vào lượt sau: không mất tiếng. Giờ bắt đầu của lượt sau (giờ server mở lượt) muộn hơn câu đầu
     của nó đúng bằng khoảng chờ, thường dưới một giây.
   - Đây cũng là cách lượt tự sang lượt mới khi gần chạm giới hạn kích thước (ADR-0041 điểm 4), nay thêm điều kiện thời
     lượng. Tiếng gửi vào cuộc gọi không bị ngắt: mic không đổi, chỉ lượt ghi đổi.
   - Đồng hồ trên thẻ đếm từ lúc bật mic, không từ đầu lượt.

## Nghĩa vụ theo Luật 91/2025/QH15

- **Không ghi thêm gì.** Bản sao gửi vào cuộc gọi là đúng tiếng mic phòng đang được ghi thành lượt; người trong cuộc gọi
  vốn đã nghe tiếng phòng qua mic của Thư ký. Cái được ghi vẫn là lượt mic phòng, dưới cùng mục đích và mốc đồng ý đã
  có (hộp xác nhận "đã báo mọi người" trước lượt đầu tiên, ADR-0041). Câu theo micro của Thư ký thì **ít đi**: không còn bản
  ghi thứ hai của cùng tiếng phòng.
- Thông báo trước khi ghi giữ như cũ: màn chờ phòng họp báo cuộc họp được ghi âm, Thư ký xác nhận trước lượt đầu.
- Thời hạn lưu và xoá không đổi (`Recordings:RetentionDays`).

## Hệ quả

- Họp kết hợp: một công tắc, một bản ghi của tiếng phòng, người trực tuyến nghe được phòng.
- Không đổi endpoint, cơ sở dữ liệu hay backend: server đã nhận lượt mic phòng của họp kết hợp (ADR-0061), và không ghi câu
  theo micro nào nếu trình duyệt không gửi.
- Thư ký họp kết hợp mà ở xa (không ngồi ở phòng họp) thì "mic phòng" là micro của chính họ: lời họ vẫn được ghi, nhưng
  thành lượt mic phòng (tách người nói, gợi ý tên bằng Voice ID) thay vì câu theo tài khoản.
- Chủ toạ tắt mic của Thư ký trong cuộc gọi chỉ tắt bản sao gửi vào cuộc gọi; lượt mic phòng vẫn ghi. Thư ký tắt hẳn bằng
  nút mic phòng.
- Phòng họp trực tuyến chưa sẵn sàng (LiveKit không chạy) thì họp kết hợp không có chỗ bật mic phòng.
- Thư ký phải vào phòng họp trực tuyến mới bật được mic phòng của họp kết hợp. Đó cũng là điều họ cần để người trực tuyến
  nghe phòng.
- Chia lượt mỗi 5 phút: chữ của phòng họp có dần trong khi họp, mất mạng hay sập máy chỉ ảnh hưởng lượt đang ghi. Mỗi
  lượt mới làm server chuyển chữ lại cả bản ghi (ADR-0041 điểm 5); việc bỏ qua lần chuyển chữ đã cũ khi có lượt mới hơn
  làm ở backend, ngoài thay đổi này. Tab Bản ghi có vạch "Lượt N" mỗi 5 phút.

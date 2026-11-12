# ADR-0062: Hệ thống tự kết thúc cuộc họp bị bỏ quên 12 giờ sau giờ kết thúc dự kiến

- **Trạng thái:** Đề xuất — PO (Hải) 13/11/2026
- **Ngày:** 2026-11-13
- **Người quyết định:** Hải (PO)
- **Thay:** điểm 6 của [ADR-0032](0032-huy-cuoc-hop-va-tu-huy-khi-qua-han.md) (bỏ BR-302, người dùng tự kết thúc).
- **Liên quan:** [ADR-0061](0061-hop-ket-hop-mot-dong-thoi-gian.md) điểm 5 (bản ghi tổng hợp của họp kết hợp)

## Bối cảnh

Ngày 27/10/2026, PO bỏ BR-302 (tự `Ended` cuộc họp sau 4 giờ). Mốc 4 giờ tính từ lúc bắt đầu có thể cắt ngang một cuộc họp
dài thật, nên việc kết thúc được để cho Chủ toạ. Từ đó **không còn giới hạn nào**: cuộc họp mà Chủ toạ quên bấm "Kết thúc"
thì ở mãi trạng thái "Đang diễn ra".

Ngày 13/11/2026, một cuộc họp đo tải trên máy dev ở "Đang diễn ra" từ 03/11. Khi QA bản ghi tổng hợp (ADR-0061), bản ghi của
cuộc họp này dài 239 giờ và nặng 26 GB, vì mọi khoảng lặng giữa các lần ghi đều được giữ theo giờ thật. Bản sửa #331 đã giới
hạn chính tệp đó. Gốc của lỗi vẫn còn: một cuộc họp bị bỏ quên giữ phòng gọi, các bảng trực tiếp (chương trình, hàng chờ phát
biểu, phiếu nháp) và mọi việc chạy nền đọc cuộc họp đang mở, trong nhiều ngày.

## Quyết định

1. **Cuộc họp `InProgress` quá 12 giờ sau giờ kết thúc dự kiến thì hệ thống tự kết thúc.** Nếu cuộc họp bắt đầu muộn hơn
   giờ kết thúc dự kiến, 12 giờ tính từ lúc bắt đầu thật: cuộc họp nào cũng có trọn 12 giờ để chạy quá giờ. Mốc nằm ở
   `Meetings:AutoEndAfterHours` (mặc định 12, cho phép 1–720).
2. **Kết thúc giống hệt Chủ toạ bấm "Kết thúc"** (UC-22): mục chương trình đang chạy chốt `Done`, hàng chờ phát biểu đóng,
   phiếu nháp chưa mở bị xoá (có audit), phòng gọi đóng. Mọi người đang mở cuộc họp nhận sự kiện `MeetingEnded`. Không gửi
   thông báo, giống như khi Chủ toạ kết thúc.
3. **Lịch sử ghi `AutoEnd`**, `actorId = null`, `actorName = "Hệ thống"`, kèm lý do "Đang diễn ra quá 12 giờ sau giờ kết thúc
   dự kiến mà chưa ai kết thúc.". Audit có mã `meeting.auto_ended`.
4. `MeetingOverdueWorker` (15 phút một lần) chạy cả hai quy tắc: tự huỷ (BR-301) và tự kết thúc. Mỗi cuộc họp được xử lý trong
   transaction riêng, kiểm tra lại dưới khoá của cuộc họp, nên Chủ toạ bấm "Kết thúc" cùng lúc thì lần bấm của Chủ toạ thắng.
5. Không dùng lại số BR-302. BA đặt số BR mới cho quy tắc này.

## Hệ quả

- Cuộc họp thật không bị cắt: 12 giờ sau giờ kết thúc dự kiến là quá xa so với mọi lần chạy quá giờ.
- Một cuộc họp bị bỏ quên đóng lại trong vòng 12 giờ 15 phút. Bản ghi tổng hợp, bản dự thảo và các việc khác chờ `Ended`
  sẽ chạy như sau một lần kết thúc bình thường.
- Khi deploy, các cuộc họp test bị bỏ ở "Đang diễn ra" trên server sẽ tự kết thúc trong lần chạy đầu tiên của worker.
- Lượt ghi mic phòng còn mở không bị ảnh hưởng: lượt ghi không hoạt động đã được lưu sẵn sau `Recordings:IdleTakeHours`.

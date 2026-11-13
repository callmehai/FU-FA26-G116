# PR #332 — Hệ thống tự kết thúc cuộc họp bị bỏ quên

**Người ghi:** KhánhPG/BA, 13/11/2026. **Nguồn:** note TrustCabinet Bot gửi BA (13/11/2026 02:39), Hải chốt 13/11/2026;
[ADR-0062](../../05-decisions/0062-tu-ket-thuc-cuoc-hop-bi-bo-quen.md) (thay điểm 6 của ADR-0032); PR
[#332](https://github.com/callmehai/FU-FA26-G116/pull/332) (merge `98d2d31a`); contract
[05-quan-ly-cuoc-hop.md](../../03-api/05-quan-ly-cuoc-hop.md) dòng ~656. **Không có ticket Jira** trong PR — hỏi leader
có gắn vào ticket nào (SCRUM-99 huỷ / tự huỷ quá hạn?) để comment.

Làm **sau note 45** (số BR lấy tiếp).

## 1. Quy tắc

- Cuộc họp **Đang diễn ra** quá **24 giờ sau giờ kết thúc dự kiến** mà chưa ai kết thúc thì hệ thống tự kết thúc.
- Bắt đầu muộn hơn giờ kết thúc dự kiến thì 24 giờ tính từ **lúc bắt đầu thật** (mọi cuộc họp có trọn 24 giờ chạy quá giờ).
- Kết thúc **y như Chủ toạ bấm Kết thúc** (UC-22): mục nghị sự đang chạy chốt xong, hàng chờ phát biểu đóng, phiếu nháp chưa mở
  bị xoá (có nhật ký), phòng gọi đóng. Không gửi thông báo.
- Lịch sử ghi **"Tự kết thúc (quá giờ)"**, người làm **"Hệ thống"**; có nhật ký kiểm toán.
- Hệ thống xét 15 phút một lần, cùng lượt với tự huỷ (BR-301); Chủ toạ bấm Kết thúc cùng lúc thì lần bấm của Chủ toạ thắng.
- **BR-302** ("tự kết thúc sau 4 giờ") giữ nguyên **ngừng áp dụng**, không dùng lại số, không dùng lại mốc 4 giờ.

## 2. Việc BA

- [ ] **BR mới** (số tiếp sau note 45): nội dung §1, gạch ý 1–4. Related UC: UC-22. Nguồn: ADR-0062 / PO 13/11/2026.
      Viết kiểu BR-301 (tiếng Anh, "records System as the actor").
- [ ] **BR-302**: giữ "Retired", thêm câu trỏ sang BR mới ("replaced by BR-xxx, 24 hours after the planned end").
- [ ] **UC-22 End Meeting**: **không sửa luồng** (chốt 13/11/2026). Tự kết thúc không có người làm, nên không thêm AF;
      BR mới ghi Related UC: UC-22 là đủ.
- [ ] Sequence UC22: không đổi. (Tuỳ) một sequence riêng cho việc chạy nền, tác nhân hệ thống, không gắn số UC (như note 39).
- [ ] State machine [SM-01_Meeting](../../10-statemachine/): chuyển `InProgress → Ended` thêm sự kiện "quá 24 giờ sau giờ kết thúc dự kiến" của hệ thống. Kiểm SM-01 còn nhắc 4 giờ không.
- [ ] ERD: `MeetingStatusHistory` action thêm `AutoEnd` (enum chuỗi).
- [ ] Report 3 Change Log.
- [ ] Jira (**chốt 13/11/2026**): comment vào **SCRUM-99** (ADR-0062 thay điểm 6 ADR-0032 của SCRUM-99, cùng worker với tự
      huỷ BR-301), rồi nhắn leader một câu: "tự kết thúc #332 mình ghi ở SCRUM-99; cần ticket riêng thì báo". Không đổi
      trạng thái SCRUM-99 chỉ vì comment này.

## 3. Dự thảo luồng UC (câu chữ cho Report 3) — chờ duyệt

Câu chữ tiếng Anh, chỉ ghi **phần đổi**; phần không nhắc giữ nguyên. Quy tắc chia: cùng tác nhân + cùng mục đích + khác
bước → AF; luồng y hệt → chỉ sửa câu; khác tác nhân → UC riêng; hệ thống tự chạy không ai bấm → BR, không viết AF.
`BR-new-x` là BR chưa đánh số.

### 3.1. Tự kết thúc cuộc họp (chỉ BR, không sửa luồng UC-22)

**Chốt 13/11/2026:** tự kết thúc là hệ thống tự chạy, không có người làm, nên **không thêm AF vào UC-22** (cả AF "Chủ toạ
bấm cùng lúc" cũng bỏ). Quy tắc nằm ở BR; UC-22 chỉ được BR đó trỏ tới ở cột Related UC.

- **UC-22**: không đổi Actors, Description, luồng. (Tuỳ) POST-5 giữ nguyên.
- **BR-302**: giữ Retired, thêm "Replaced by BR-new-6."
- **BR-new-6** (Related UC: UC-22): If an In Progress Meeting has not been ended 24 hours after its planned end time — or 24 hours after its actual start, when it started after its planned end — the system ends it the same way as UC-22, records System as the actor with the reason "In progress more than 24 hours past the planned end" in its lifecycle history and the audit log, and notifies no one. (PO 13/11/2026, ADR-0062)

- [ ] Duyệt BR-302 và BR mới (UC-22 không đổi)

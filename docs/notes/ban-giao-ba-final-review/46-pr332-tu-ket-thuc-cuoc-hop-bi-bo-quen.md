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

- [x] **BR mới** (số tiếp sau note 45): nội dung §1, gạch ý 1–4. Related UC: UC-22. Nguồn: ADR-0062 / PO 13/11/2026.
      Viết kiểu BR-301 (tiếng Anh, "records System as the actor").
- [x] **BR-302**: giữ "Retired", thêm câu trỏ sang BR mới ("replaced by BR-xxx, 24 hours after the planned end").
- [x] **UC-22 End Meeting**: **không sửa luồng** (chốt 13/11/2026). Tự kết thúc không có người làm, nên không thêm AF;
      BR mới ghi Related UC: UC-22 là đủ.
- [x] Sequence UC22: không đổi. (Tuỳ) một sequence riêng cho việc chạy nền, tác nhân hệ thống, không gắn số UC (như note 39).
- [x] State machine [SM-01_Meeting](../../10-statemachine/): chuyển `InProgress → Ended` thêm sự kiện "quá 24 giờ sau giờ kết thúc dự kiến" của hệ thống. Kiểm SM-01 còn nhắc 4 giờ không.
- [x] ERD: `MeetingStatusHistory` action thêm `AutoEnd` (enum chuỗi).
- [x] Report 3 Change Log.
- [x] Jira: comment vào **SCRUM-99 sau khi PR #350 vào `main`**
      (kiểm `git log origin/main` có merge commit rồi mới comment; **giữ `Done`, không đổi trạng thái**).
      Nội dung (tiếng Việt):

      > BA Final Review PR #332 xong (Report 3, PR #350 <merge-commit>): thêm BR-460 (hệ thống tự kết thúc
      > họp Đang diễn ra quá 24 giờ sau giờ kết thúc dự kiến, actor Hệ thống, có nhật ký, không thông báo);
      > BR-302 giữ Retired, trỏ sang BR-460; UC-22 không đổi luồng; SM-01 chuyển InProgress → Ended thêm
      > sự kiện 24 giờ của hệ thống; ERD `MeetingStatusHistory.Action` thêm `AutoEnd`; sequence nền
      > SYS_AutoEndForgottenMeeting (DRAFT, không số UC). Ghi ở SCRUM-99 vì ADR-0062 thay điểm 6 ADR-0032
      > của ticket này (cùng worker tự huỷ BR-301). Không cần ticket riêng cho #332.

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

- [x] Duyệt BR-302 và BR mới (UC-22 không đổi)

## 4. Đóng sổ — vào `main` 15/11/2026

- PR #350 (`5c29ece0`) đã merge: BR-460 mới, BR-302 giữ Retired và trỏ sang BR-460, UC-22 không đổi luồng, SM-01 thêm sự kiện hệ thống "24h after planned end or actual start", ERD `MeetingStatusHistory.Action` thêm `AutoEnd`, sequence SYS_AutoEndForgottenMeeting (DRAFT), Change Log.
- Jira SCRUM-99 đã comment (PO 15/11/2026), ticket giữ `Done`.
- Không còn việc BA cho PR #332 (xem note 49).

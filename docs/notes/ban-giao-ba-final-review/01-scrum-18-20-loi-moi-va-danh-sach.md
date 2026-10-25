# SCRUM-20 và một phần SCRUM-18 — lời mời, tạo/sửa, danh sách cuộc họp

**Ngày ghi:** 24/10/2026
**Nguồn:** tin nhắn agent của leader gửi Khánh
**Khi nào sửa:** ticket sang **BA Final Review**

SCRUM-20 (PR #111) và một phần SCRUM-18 đã làm khác ticket, theo cách AI-eCabinet đang làm. Chi tiết đã ghi trong comment Jira của SCRUM-20 và SCRUM-18.

PR: https://github.com/callmehai/TrustCabinet/pull/111

## Trả lời lời mời (UC-29, UC-30)

- Trả lời lời mời chỉ là xác nhận trước (pre-confirm). Mỗi người trả lời **một lần**, sau đó không đổi được (MSG285).
- **Chủ tọa không trả lời** vì là người điều hành cuộc họp (MSG286). Ai chưa trả lời thì coi như có tham dự.
- Đã bỏ bộ đếm phản hồi. Người được mời trả lời ngay ở màn chi tiết cuộc họp và trên lịch, không trả lời trong tab Thành phần tham dự.
- Cuộc họp mình đã từ chối bị ẩn khỏi **lịch** của mình, nhưng vẫn hiện trong danh sách cuộc họp.

## Tạo / sửa cuộc họp (UC-14, UC-28)

- Chọn thành phần tham dự ngay trong form tạo cuộc họp. Form tạo và sửa là popup, không còn là trang riêng.

## Danh sách cuộc họp (UC-17, BR-30)

- Màn danh sách mở sẵn bộ lọc **Đang diễn ra**.
- Bộ lọc thời gian chỉ còn các lựa chọn sẵn: Hôm nay / Tuần này / Chưa diễn ra / Đã diễn ra. Đã bỏ ô chọn khoảng ngày.

## File cần sửa

- `docs/01-requirements/05-use-case-specifications.md`: UC-14, UC-17, UC-28, UC-29, UC-30
- `docs/01-requirements/06-business-rules.md`: BR-30 và các rule về phản hồi lời mời
- Sequence diagram: `docs/06-sequencediagram/meeting-participant-management/UC29_…`, `UC30_…`, `docs/06-sequencediagram/meeting-management/UC17_ViewMeetingList`

Docs API đã cập nhật sẵn, lấy làm nguồn:

- `docs/03-api/05-quan-ly-cuoc-hop.md`
- `docs/03-api/09-thanh-phan-tham-du.md`
- `docs/03-api/00-danh-muc-ma-thong-bao.md`

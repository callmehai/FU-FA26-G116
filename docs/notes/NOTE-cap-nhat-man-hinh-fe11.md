# Bàn giao cập nhật đặc tả màn hình FE-11

**Ngày:** 14/10/2026. **Người cập nhật UC Spec:** KhanhPG.

UC-52a, UC-52b và UC-53–UC-57 đã được đặc tả trong Report 3 DOCX local và đồng bộ sang
[Markdown](../08-reports/Report-3_Software_Requirement_Specification.md#11-fe-11--voting-management).
Các mô tả màn hình, screen inventory và ảnh minh hoạ được giữ nguyên theo yêu cầu người dùng;
người phụ trách phần màn hình cập nhật sau. Không dùng phần màn hình cũ để ghi đè quyết định mới
trong UC Spec và [ADR-0019](../05-decisions/0019-chuan-bi-bieu-quyet-truoc-khi-hop.md).

## Các điểm cần đối chiếu

- **11.1 — Voting Tab:** Chair chuẩn bị, sửa và xoá nháp ở `Approved`, `Published`, `InProgress`.
  Nháp chỉ Chair hiện hành thấy; số lượng hiển thị cũng phải loại nháp bị ẩn. Người khác chỉ xem
  Vote đã mở/đóng khi có quyền. Kết quả vẫn xem được sau `Ended`.
- **11.2 — Create & Open Vote Dialog:** Vote thuộc trực tiếp Meeting; đặc tả mới không yêu cầu
  chọn hoặc gắn Agenda Item. Lưu nháp chưa chốt danh sách người bỏ phiếu. Chỉ mở ở `InProgress`,
  chỉ một Vote `Open` mỗi Meeting và phải có ít nhất một người đủ điều kiện. Chair chọn ít nhất
  một role, mặc định chọn cả Chair / Secretary / Member / Guest.
- **11.3 — Cast Vote:** một người chọn một phương án, gửi một lần, không đổi/rút. Danh sách và
  số người đủ điều kiện chốt lúc mở; đổi role không làm đổi danh sách, nhưng mất quyền truy cập
  hiện hành thì không được gửi phiếu mới. Guest xác minh email và dùng lời mời, không dùng tên tự khai.
  Với phiếu kín, khi mở lại chỉ xác nhận đã gửi; không truy xuất lựa chọn từ danh tính người bỏ phiếu.
- **11.4 — Result & Conclusion:** hệ thống kiểm phiếu khi đóng; Chair chọn `Passed`, `NotPassed`
  hoặc `Deferred`, ghi chú tuỳ chọn. Kết luận đã ghi không sửa. Khi chưa có phiếu, số phiếu là 0
  và không hiển thị tỷ lệ; tỷ lệ có phiếu tính trên số phiếu đã gửi. Phiếu công khai cho xem lựa
  chọn sau khi đóng; phiếu kín chỉ hiện tổng.
- **Khi Meeting Ended:** ngừng nhận phiếu mới kể cả Vote còn Open. Chair vẫn đóng/kiểm phiếu cũ
  và ghi kết luận; End Meeting không tự đóng Vote.
- **8.5 — phần biểu quyết trong panel, 14.2 — nút bỏ phiếu và screen inventory FE-11:** đối chiếu
  cùng điều kiện quyền/trạng thái trên. Trong trạng thái Open, phần xem trạng thái chỉ hiển thị
  turnout; phiếu kín không thông báo danh tính người vừa bỏ hoặc liên kết người với lựa chọn.

Quyền chuẩn bị Vote ở `Approved` áp dụng riêng cho biểu quyết, không tự mở quyền đọc các hồ sơ
khác. Note này chỉ bàn giao phần màn hình biểu quyết, không yêu cầu sửa đặc tả hoặc luồng Agenda.

## Nguồn để người phụ trách sửa

- [Bản trích UC Spec FE-11](../01-requirements/05-use-case-specifications.md#fe-11--voting-management).
- [Business Rules](../01-requirements/06-business-rules.md): BR-160/161/165/189/190,
  BR-252–BR-274; các quy tắc kết thúc họp trước đó vẫn theo ADR-0017.
- [Catalogue MSG](../03-api/00-danh-muc-ma-thong-bao.md): MSG205–MSG237 dành sẵn cho FE-11.

Chưa cập nhật Drive, Jira hoặc các diagram trong lần đặc tả này.

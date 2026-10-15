# Bàn giao cập nhật đặc tả màn hình FE-11

**Ngày:** 14/10/2026. **Người cập nhật UC Spec:** KhanhPG.

**Rà soát bổ sung:** 15/10/2026, đối chiếu ADR-0016, ADR-0017, ADR-0019 và ADR-0020.

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
- [Catalogue MSG](../03-api/00-danh-muc-ma-thong-bao.md): MSG205–MSG236 dành sẵn cho FE-11;
  MSG237 ngừng dùng sau đợt rà soát 15/10/2026, không tái cấp.

Chưa cập nhật Drive, Jira hoặc các diagram trong lần đặc tả này.

## Bổ sung 15/10/2026 — các chỗ UI còn lệch

Phần UC/BR/MSG đã được sửa trong DOCX local và đồng bộ Markdown. Bảng dưới là bàn giao sửa
**đặc tả màn hình và wireframe**, không khẳng định giao diện hoặc endpoint đã được cập nhật.

| Vị trí | Nội dung cần sửa |
|---|---|
| 11.1, 11.2, 11.3 và I.5.1 Screen Inventory | Bỏ “linked/current Agenda Item”, “Mục n” khỏi ngữ cảnh sở hữu Vote, tiêu đề form và thông tin từng phiếu. Vote chỉ thuộc Meeting (BR-285). Agenda đang bàn vẫn có thể hiện ở header cuộc họp nhưng không trở thành liên kết của Vote. |
| 11.1 — Create/Edit/Delete; 11.2 — Save draft | Cho Chair chuẩn bị, sửa và xoá nháp ở Approved, Published, InProgress. Chỉ Chair hiện hành thấy nháp; người khác không thấy nháp hoặc số lượng nháp. Đổi Chair thì quyền quản lý nháp theo Chair hiện hành. |
| 11.1 — Vote status chip; 11.4 — kết luận | Dùng Draft → Open → Closed. “Đã có kết luận” là trạng thái hiển thị của Vote Closed đã ghi kết luận; không yêu cầu thêm trạng thái nghiệp vụ Concluded. Kết luận được hiển thị riêng, không thay thế thông tin Vote đã đóng. |
| 11.2 — Open vote | Chỉ bật khi InProgress. Nhấn từ form tạo/sửa đã hiển thị nội dung là xác nhận mở; không bắt lưu nháp trước hoặc xác nhận thêm một lần. Mở từ card nháp ở 11.1 thì hiển thị thông tin để Chair xác nhận. Cả hai đường cùng dùng điều kiện UC-54. |
| 11.2 — lỗi Open vote | Không có người đủ điều kiện, đã có Vote khác Open hoặc trạng thái/quyền thay đổi thì không mở. Giữ nội dung đang nhập để sửa; không tạo nháp hoặc sửa nháp đã lưu như tác dụng phụ. Chỉ báo MSG217 khi thực sự mở thành công, không báo MSG210/211 trước đó. |
| 11.3 — Submit ballot | Kiểm tra điều kiện trước khi cho bỏ phiếu và kiểm tra lại khi gửi. Dùng danh sách chốt lúc mở; không tính lại theo role hiện tại. Người được thêm vào Meeting sau lúc mở không tự được thêm vào danh sách của Vote đó. |
| 11.3 — đã gửi phiếu | Khoá gửi tiếp. Với phiếu kín, trạng thái hoàn tất chỉ xác nhận “Đã gửi phiếu”, không tải lại hoặc hiển thị lựa chọn đã lưu theo danh tính. Mở lại màn hình hoặc vào lại cuộc họp không tạo quyền bỏ lần hai. Phiếu công khai được xem lựa chọn trong kết quả sau khi đóng. |
| 11.3 — hết quyền/phiếu đóng/cuộc họp kết thúc | Phân biệt: không thuộc danh sách đã chốt → MSG224; hết nhận phiếu → MSG225; đã gửi → MSG226; mất quyền xem hoặc Vote không truy cập được → MSG205, không tiết lộ nội dung. |
| 11.4 — Result & Conclusion | View result dành cho người có quyền xem; chỉ Chair hiện hành có nút ghi kết luận khi Vote Closed và chưa có kết luận. Sau khi ghi, kết luận và ghi chú chỉ đọc. Không tự chọn Passed theo số phiếu. Không có phiếu thì số lượng là 0, không hiện phần trăm. |
| 11.1/11.3/11.4 — sau Ended | Giữ nguyên trạng thái Open nếu chưa đóng, kèm chỉ dẫn không nhận thêm phiếu. Chair vẫn đóng/kiểm phiếu rồi ghi kết luận; người có quyền vẫn xem kết quả. Không tự đóng Vote và không cho mở Vote khác sau Ended. |
| 5.3 — Meeting Facilitation | Đổi “Create a vote for this item” thành tạo Vote cho cuộc họp; không tự gắn mục đang bàn. Không yêu cầu hoàn thành mục nghị sự mới được mở Vote. |
| 8.5 — In-Room Meeting Panel; 14.2 — Meeting Workspace; các điểm mở màn Cast Vote | Dùng cùng UC-52b/55 và điều kiện với Voting tab; không có luật bỏ phiếu riêng theo room, lần reconnect hoặc thiết bị. Guest dùng lời mời đã xác minh, không dùng tên tự khai làm căn cứ bỏ phiếu. |

ADR-0020 còn nhắc màn **6.2**, nhưng III.6.2 của Report 3 hiện là **Add Participant Dialog**,
không có nội dung gắn Vote với mục nghị sự. Đối chiếu số trên wireframe, không tự sửa màn này.

## Ánh xạ hành động UI với UC

| Hành động | UC / flow | Kết quả thành công | Khi không hoàn thành |
|---|---|---|---|
| Mở Voting tab | UC-52a | Danh sách Vote được phép xem; không có thì MSG207 | Không có quyền: MSG205; tải lỗi: MSG206 |
| Xem trạng thái / kết quả | UC-52b main / AF-1 / AF-2 | Nháp: thông tin cấu hình; Open: số đã gửi/tổng đủ điều kiện; Closed: kết quả và kết luận nếu có | MSG205 hoặc MSG208; không hiển thị tải lỗi như kết quả rỗng |
| Tạo → Save draft | UC-53 main | Nháp mới, MSG210 | MSG209/213/214 hoặc MSG205 |
| Edit → Save draft | UC-53 AF-1 | Cập nhật nháp, MSG211 | MSG209/213/215 hoặc MSG205; nháp cũ giữ nguyên |
| Delete → xác nhận | UC-53 AF-2 | Xoá nháp, MSG212 | MSG213/216 hoặc MSG205; không xoá Vote Open/Closed |
| Open vote từ form tạo/sửa | UC-53 AF-3 → UC-54 AF-1 | Tạo/cập nhật và mở thành công, MSG217 | MSG209/218/219/220/221 hoặc MSG205; không lưu dở nháp |
| Open vote từ card nháp → xác nhận | UC-54 main | Vote Open, chốt danh sách và số người đủ điều kiện, MSG217 | Cùng các điều kiện mở ở trên |
| Submit ballot | UC-55 | Ghi một phiếu, cập nhật số đã gửi, MSG227 | MSG222–226/228 hoặc MSG205; không tăng số đã gửi khi chưa ghi thành công |
| Close voting → xác nhận | UC-56 | Closed và có kết quả, MSG229 | MSG230/231 hoặc MSG205; không mất phiếu đã gửi |
| Record conclusion | UC-57 | Ghi kết luận, MSG235 | MSG232/233/234/236 hoặc MSG205; không đổi kết luận đã lưu |

Đóng/huỷ form trước khi xác nhận là hành vi UI, không lưu và không hiện toast riêng. Các EX
“Action cancelled” đã bỏ; MSG237 ngừng dùng. Chưa lưu là trạng thái bình thường của form,
không phải lỗi nghiệp vụ cần thêm một nhánh sequence gọi hệ thống.

## Quyền thao tác theo trạng thái cuộc họp

| Trạng thái Meeting | Chuẩn bị/sửa/xoá nháp | Mở Vote | Gửi phiếu | Đóng/kiểm phiếu cũ | Ghi kết luận |
|---|---|---|---|---|---|
| Approved | Chair hiện hành; quyền riêng cho chuẩn bị Vote | Không | Không | Không | Không |
| Published | Chair hiện hành | Không | Không | Không | Không |
| InProgress | Chair hiện hành | Chair; nháp hợp lệ, chưa có Vote khác Open, có người đủ điều kiện | Người trong danh sách chốt, còn quyền truy cập, chưa gửi; Vote Open | Chair; Vote Open | Chair; Vote Closed, chưa có kết luận |
| Ended | Không | Không | Không, kể cả Vote còn Open | Chair; Vote còn Open | Chair; Vote Closed, chưa có kết luận |

Chỉ người có quyền xem mới được xem thông tin Vote; nháp chỉ Chair hiện hành thấy. Quyền chuẩn bị
Vote ở Approved không mở thêm quyền xem hồ sơ khác. Trong mọi trạng thái, kết luận đã ghi không sửa.

## Điểm thống nhất để dựng sequence

- Dùng tên UC, thứ tự bước và EX trong bản trích UC làm nguồn; bảng trên chỉ ánh xạ thao tác UI.
- Lưu nháp (UC-53) và mở Vote (UC-54) là hai kết quả khác nhau. Nhánh Open ngay dùng trực tiếp
  UC-54 AF-1, không vẽ thêm bước tạo nháp trung gian thành công trước khi mở.
- Khi mở, chốt người đủ điều kiện một lần. Khi gửi phiếu, kiểm tra người đó thuộc danh sách chốt
  và còn quyền truy cập; không chọn lại người đủ điều kiện theo role hoặc điểm danh hiện tại.
- Xử lý Vote/cuộc họp không truy cập được trước khi tiết lộ nội dung. Khi thao tác không lưu được,
  giữ nguyên dữ liệu trước đó; không phát thông báo thành công hoặc cộng số phiếu trước khi ghi nhận.
- Kết thúc Meeting, đóng Vote và ghi kết luận là ba hành động độc lập. Không vẽ nhánh bắt đóng
  Vote trước End Meeting, tự đóng khi End Meeting hoặc hệ thống tự suy ra kết luận theo đa số.
- Mô tả dữ liệu đích trong Report 3 đã bỏ `AgendaItemId` của Vote, bổ sung `VotingEligibleVoter`
  giữ danh sách chốt lúc mở, và hỗ trợ danh tính Guest trong VoteReceipt/VoteChoice. Phiếu kín
  không giữ liên kết người–lựa chọn. Code và ERD/class diagram hiện có chưa được cập nhật trong
  lần đồng bộ tài liệu này; không dùng chúng để khôi phục luật đã bị ADR thay thế.

## Trạng thái sau vòng review bàn giao 15/10/2026

- **Đã đồng bộ ngoài màn hình:** actor UC-52a/b gồm System Admin khi có quyền xem; ngữ cảnh MSG
  trong phụ lục Report 3 khớp catalogue, kể cả các nhánh dùng chung MSG205; danh mục UC, ma trận quyền
  và chỉ mục cập nhật. BR-159 được rút gọn, chi tiết kết thúc và kết quả lưu vẫn giữ tại UC-22.
- **Còn pending cho người sửa màn:** các bảng ở trên và I.5.1 Screen Inventory; ma trận quyền đã đồng bộ.
  Hiển thị quyền xem theo actor có quyền thực tế, gồm System Admin; quyền Chair chuẩn bị nháp ở
  Approved vẫn là ngoại lệ riêng cho Vote. Không mở quyền quản lý Vote cho các actor chỉ có quyền xem.
- Mô tả lớp/quan hệ mới phục vụ diagram và triển khai tiếp theo; chưa tạo migration, sửa endpoint,
  render lại wireframe hoặc thay file Drive.

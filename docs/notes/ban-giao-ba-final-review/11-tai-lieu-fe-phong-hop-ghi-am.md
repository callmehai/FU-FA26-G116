# SCRUM-21/107 (BE + FE), trả lời PO rà soát FE-08, ghi âm SCRUM-109/44 — cho BA Final Review

**Ngày ghi:** 26/10/2026
**Nguồn:** comment Jira SCRUM-21 (10257 BE, 10273 FE), SCRUM-107 (10274 FE); ba tin nhắn bot hôm 26/10 (PO Hải trả lời rà soát FE-08, đính chính mã, SCRUM-44)
**Khi nào sửa:** lúc **BA Final Review**

## 1. SCRUM-21: BE xong (PR #134, comment 10257)

Lệch so với đặc tả / sequence:

1. **Chưa có nhóm Biểu quyết** (UC-32a AF-1): chưa có bảng gắn tài liệu với biểu quyết; để UC-74 thêm.
2. **Thứ tự trong phòng họp** (AF-3): sequence truyền `fromRoom` để server đổi thứ tự; thực tế server trả `currentAgendaItemId`, FE tự đưa mục đang bàn lên đầu.
3. **Chỉ đọc sau khi kết thúc, ẩn Tải về khi Mật, nút Tải lên cho Chủ toạ/Thư ký:** FE lấy `status`, `sensitivity`, `myMeetingRole` từ `GET /api/meetings/{id}`. Server vẫn chặn tải lên sau khi kết thúc (MSG172) và tải về khi Mật (MSG159).
4. **Guest chưa dùng được:** chưa có xác thực Guest (UC-04).
5. **BR-188 chỉ chặn được lần tải về:** lần xem vẫn nhận byte gốc, ghi kiểm toán `document.viewed`. PO đã chấp nhận (xem [11 §2](11-quy-tac-thong-bao-agenda-po-25-10.md)); ghi vào UC-32c như rủi ro đã biết.
6. **Kiểm toán fail-closed:** không ghi được nhật ký thì không gửi tệp. UC chưa nói, cần thêm.
7. **UC-32c POST-3** (đã cuộn tới trang cuối, BR-290) chưa làm, thuộc UC-74.

Cần ticket riêng: tab Chương trình nghị sự (SCRUM-25) từng chỉ cho thành viên hiện tại thấy tài liệu của mục, trong khi tab Tài liệu theo BR-275. **PO 25/10 (comment 10265) đã sửa:** tab nghị sự hiện tài liệu cho mọi người xem được cuộc họp, kể cả Người tổ chức/Người duyệt; bỏ `AgendaRules.CanReadDocuments`. Cũng chốt: không đóng dấu tên người xem, không chuyển đổi tệp trên server. `documentsChanged` có thêm `eventId`, `occurredAt`; publish sau tải lên có giới hạn 2 giây.

Mã MSG156–MSG160, không cấp mã mới. Endpoint: `GET /api/meetings/{id}/documents`, `.../{meetingDocumentId}/content[?download=true]`; chi tiết trong `docs/03-api/11-tai-lieu-cuoc-hop.md`.

## 2. SCRUM-21 + SCRUM-107: FE xong (PR #144, comment 10273 và 10274)

**SCRUM-21, khác ticket / wireframe (7.3, 7.5, 7.5b, 8.6):**

1. Không có nhóm "Biểu quyết" vì chưa có dữ liệu.
2. Trình xem chưa có cột bình luận (SCRUM-23). Trang Mật không đóng dấu tên người xem (theo PO 25/10; wireframe 7.5 có ghi chú).
3. Tag PDF màu tím, không đỏ (đỏ chỉ cho "Mật", giờ hiện tại, cảnh báo).
4. Mục nghị sự đánh số theo thứ tự trong danh sách, không theo `position` (có thể có khoảng trống).
5. Tiêu đề nhóm trong panel phòng họp viết thường ("Đang bàn: 3. …", "Mục khác").
6. Dòng thông tin ghi "Ai xem được cuộc họp đều đọc được tài liệu đã thêm", bỏ "kể cả khách mời đã xác minh" (khách chưa đăng nhập được, UC-04).
7. Ở trang chi tiết, trình xem mở **cùng tab**; chỉ trong phòng họp mới mở tab mới (BR-315).
8. Phòng họp trên điện thoại (≤560px): Nội dung họp, Điểm danh, Tài liệu gom vào nút ⋯ (wireframe chỉ có bản máy tính).
9. PDF có ảnh JPEG2000 có thể không hiện ảnh: pdf.js cần wasm, CSP không cho `'wasm-unsafe-eval'` (đã ghi trong `docs/03-api/11`).

**SCRUM-107, khác ticket / wireframe (7.4b):**

1. Hộp thoại không có tab "Từ kho của tôi" và dòng "Gắn với mục"; menu ⋯ (Gỡ khỏi mục / cuộc họp) cũng chưa có. Tất cả thuộc SCRUM-58. Nút đổi tên thành **"Thêm tài liệu chung"**.
2. Lỗi kiểm ở FE dùng lại câu MSG164 / MSG288 kèm tên tệp bị loại, không ghi lý do riêng từng tệp như wireframe. Không thêm mã mới.
3. Gửi xong toàn bộ tệp thì nút Huỷ bị khoá (server có thể đang lưu).
4. Xử lý lỗi: MSG164 đánh dấu đúng tệp, giữ hộp thoại; MSG288, MSG166, lỗi mạng giữ tệp đã chọn; MSG162, MSG172, MSG174 đóng hộp thoại và tải lại cuộc họp. Server cắt body quá lớn giữa chừng nên FE chỉ thấy lỗi mạng, đây là câu hỏi BE/FE đã mở ở điểm lệch 3 trong comment 10260 của BA.

File cần sửa: UC-32a, UC-32c, UC-33a AF-1 trong `05-use-case-specifications.md`; mô tả màn §III 7.3, 7.4b, 7.5, 7.5b, 8.6 trong Report 3.

## 3. PO (Hải) trả lời rà soát FE-08, 26/10

1. **Giơ tay / hạ tay → thêm UC "Đăng ký phát biểu"** như eCab, có cả chế độ phát biểu theo lượt. Gắn vào **SCRUM-27** có sẵn (Huy); DB đã có bảng `speaking_requests`; không tạo ticket mới.
2. **Gọi 1–1 (SCRUM-40) → bỏ.** Ticket đổi tên "[ĐÃ BỎ]". Bỏ khỏi UC và phạm vi.
3. **Ghi âm (D9) làm theo eCab, không hỏi đồng ý từng người.** Cuộc họp có chế độ thu, chọn trước khi bắt đầu:
    * **Micro từng người** (SCRUM-109, đã xong): mỗi người tự thu micro mình, cắt theo câu, chữ hiện theo đợt sau mỗi câu, tên người nói = chủ micro. Người dự thấy mình đang được ghi và tự tắt được; Chủ toạ không bật mic người khác từ xa.
    * **Thu chung cả phòng** (SCRUM-29/31/35, SCRUM-44): Thư ký xác nhận đã có đồng ý + mục đích + hạn lưu khi lưu.
4. **BR-328** (đổi Chủ toạ giữa họp): PO chốt **không khoá** đổi vai trò khi đang họp (eCab cũng cho; Chủ toạ phải rời họp thì giao lại). Hải đang sửa để phòng tự cập nhật; giữ BR-328.
5. **Nhánh `docs/design-diagrams` có code biểu quyết + migration `VotesBelongToMeeting`:** PO hỏi code của ai, cho ticket nào. Migration không nên nằm trong PR tài liệu. **Việc cần làm: kiểm `git log` các file đó trước khi merge #136; nếu không phải của BA thì tách ra.**

## 4. Mã thông báo: số liệu cuối cùng

Tin đầu của bot nói dùng từ MSG318, rồi đính chính; đây là bản cuối (chép từ tin 26/10):

* MSG290–MSG298 là của **Bản ghi**, MSG299 của **gỡ băng**, MSG290–MSG317 là dải nhánh voice của Hải. **Dải MSG290–299 không phải của phòng họp** (note SCRUM-41 hôm 25/10 ghi nhầm).
* MSG318–MSG327 do **SCRUM-109** dùng (PR #154 đã merge).
* Mã phòng họp của BA bắt đầu từ **MSG328**. Luôn lấy mã kế tiếp ở dòng "Mã tiếp theo" trong `docs/03-api/00-danh-muc-ma-thong-bao.md` trên `main`.
* Mã thông báo FE của phòng họp sẽ cấp mới, không dùng 290–299.

## 5. Ghi âm: SCRUM-109 và SCRUM-44 sang BA Final Review

SCRUM-29, 31, 35, 109 và 44 đều đã sang BA Final Review.

**SCRUM-109 (chế độ thu), nguồn:**
* `docs/05-decisions/0029-ghi-am-theo-micro-tung-nguoi.md`: chọn chế độ ở form tạo/sửa hoặc tab Bản ghi (Chủ toạ, Thư ký), khoá khi bắt đầu họp; chữ hiện sau mỗi câu; **không lưu âm thanh**, chỉ lưu chữ + mốc giờ + chủ micro.
* API: `docs/03-api/12-ban-ghi-cuoc-hop.md` (mục micro từng người), `05-quan-ly-cuoc-hop.md` (trường `captureMode`).
* Khác eCab, đã ghi trong Jira SCRUM-109: chữ gửi sau từng câu (eCab đợi tắt micro mới gửi); không nghe lại được từng câu.
* **UC mới** cần viết: chọn chế độ thu; ghi âm micro từng người.

**SCRUM-44 (PR #161 đã merge), quyết định ở ADR-0030:**
* Cuộc họp chọn **"Thu chung cả phòng"**: Thư ký ghi âm trong phòng họp thu được cả micro mình lẫn giọng mọi người trong cuộc gọi; hoặc thu bằng điện thoại rồi tải lên như cũ.
* Ai trong phòng cũng thấy chip **"Đang ghi âm cuộc họp"**.
* Cần cập nhật **UC ghi âm + sequence diagram** theo hai cách thu này.

**Việc BA:** UC "Đăng ký phát biểu" (SCRUM-27); bỏ SCRUM-40 khỏi UC và phạm vi; UC chọn chế độ thu + micro từng người; cập nhật UC ghi âm + sequence theo ADR-0030; giữ BR-328; mã MSG theo mục 4.

# Đối chiếu SCRUM-107 và SCRUM-21 với đặc tả — báo cáo cho BA Final Review

**Ngày:** 27/10/2026
**Việc:** bước 6 của [NOTE-ban-giao-fe07](../NOTE-ban-giao-fe07-kho-tai-lieu-cho-loc.md#102-bước-6--ba-final-review-scrum-107-và-scrum-21-làm-ngay) — đọc code thay BA, **chỉ báo cáo, chưa sửa gì**.
**Nguồn:** `docs/03-api/11-tai-lieu-cuoc-hop.md`, `docs/03-api/00-danh-muc-ma-thong-bao.md`, `docs/01-requirements/05-use-case-specifications.md`,
`06-business-rules.md`, Report 3 §II.7.1–7.2 và §III 7.3–7.5/8.6, ba sequence `UC33a`, `UC32a`, `UC32c`, class diagram FE-07,
code backend của UC-33a AF-1 / UC-32a / UC-32c và code FE của tab Tài liệu, hộp thoại tải lên, trình xem.
**Nguồn comment Jira:** [note 11](11-tai-lieu-fe-phong-hop-ghi-am.md) chép lại comment SCRUM-21 (10257 BE, 10273 FE), SCRUM-107 (10274 FE),
comment BA 10260 và quyết định PO 10265 ngày 25/10.

> **Chưa đọc được Jira trực tiếp.** Session này không có kết nối Jira (không có MCP server, không có credential), nên bảng dưới
> dựa trên bản chép comment trong note 11. Nếu còn comment khác trên hai ticket thì cần dán vào trước khi chốt.

**Ký hiệu kết luận:** `Khớp` · `Lệch (đã ghi)` = Jira/PO đã ghi và chấp nhận · `Lệch (chưa ghi)` = chưa thấy trên Jira ·
`Chưa xây` = đặc tả là mục tiêu, code chưa có (có ticket riêng).

---

## 1. Kết luận trước khi xem chi tiết

1. **SCRUM-107 (UC-33a AF-1) gần như đã khớp.** Lượt BA Final Review 2 ngày 24/10 đã sửa UC-33a AF-1/EX-1/3/6/7/8, BR-215,
   MSG164/174/288 và sequence UC33a theo đúng mười điểm lệch của PR #128. Phần còn lại **không phải chỗ lệch nghiệp vụ**, mà là
   ba chỗ tài liệu chưa theo code: thứ tự kỹ thuật trong sequence, `pageCounts` + sự kiện realtime chưa vẽ, và class diagram thiếu
   phần kiểm tệp. Ngoài ra §III 7.4 còn hai câu theo wireframe cũ.
2. **SCRUM-21 (UC-32a, UC-32c) có bốn chỗ đặc tả đang mô tả như đã có, nhưng code chưa có**: nhóm Biểu quyết, khách mời,
   ghi nhận "đã tới trang cuối" (BR-290), và câu "không ghi được kiểm toán thì không gửi tệp" (đang thiếu). Cả bốn đã ghi trên Jira
   (10257/10273), nên chỉ cần **ghi rõ "chưa xây + ticket"** trong UC và Report 3, không phải mở lại ticket.
3. **Danh mục MSG tự mâu thuẫn**: cùng một file, dòng 164 nói `MSG156`–`MSG160` "đã triển khai trong SCRUM-21", còn bảng tiếng Anh
   §V.2 (dòng 610–614) vẫn ghi "Dành sẵn". Phải sửa bảng §V.2.
4. **Report 3 §III còn hai câu trái quyết định PO 25/10**: 7.5 in tên người xem lên trang Mật (PO đã bỏ đóng dấu), và 8.6 ghi panel
   Tài liệu trong phòng là "planned, not part of the current version" trong khi panel đã chạy.
5. **Không có chỗ lệch nào bắt buộc mở lại SCRUM-107.** Với SCRUM-21, chỉ còn **một câu hỏi kỹ thuật chưa đóng** (body quá lớn thì
   trình duyệt chỉ thấy lỗi mạng, không đọc được `MSG288` — câu hỏi đã mở ở comment 10260 điểm 3) và **một lỗi dữ liệu dev**
   (tài liệu fixture của chương trình nghị sự không có tệp trên kho nên hiện trong danh sách mà mở không được) — cả hai đều không sửa
   được bằng tài liệu.

---

## 2. SCRUM-107 — UC-33a AF-1 "Tải tệp từ máy lên cuộc họp"

### 2.1 Luồng thay thế AF-1, từng bước

| Bước của đặc tả | Code làm gì | Kết luận |
|---|---|---|
| 1. Chọn một hoặc nhiều tệp từ máy và gửi cùng lúc | Hộp thoại chọn tệp, gửi **một** request nhiều phần, có thanh tiến độ theo số byte đã gửi; số trang PDF đếm ngay khi chọn | Khớp |
| 2. **Trước khi đọc tệp**, kiểm quyền: xem được cuộc họp, là Chủ toạ/Thư ký hiện tại, cuộc họp chưa kết thúc; người không phải Chủ toạ/Thư ký nhận `MSG162` bất kể tệp | Server kiểm tài khoản đang hoạt động → quyền xem cuộc họp + vai trò hiện tại + chưa `Ended`, **rồi mới** đọc thân request. Không xem được → `404/MSG174`; xem được nhưng mất vai trò → `403/MSG162`; đã kết thúc → `409/MSG172`. Thứ tự ưu tiên: không xem được trước, mất vai trò sau | Khớp (đúng điểm 1, 10 của note 07) |
| 3. Tối đa 10 tệp / 200 MB; mỗi tệp ≤ 50 MB, là PDF hoặc DOCX, **đuôi khớp nội dung**; chỉ rõ tệp lỗi | Khai báo request > 201 MB → `MSG288` trước khi đọc thân; đọc nhiều phần (hỏng/không phải multipart/không có tệp → `MSG164`); quá 10 tệp hoặc quá 200 MB → `MSG288`; từng tệp: tên dùng được, không rỗng, ≤ 50 MB, đuôi `.pdf`/`.docx`, nội dung đúng loại (PDF phải bắt đầu bằng `%PDF-`; DOCX phải là gói Word, không nhận DOCM/DOTX/zip hỏng) — lỗi ghi theo `files[i]` | Khớp |
| 4. Thêm tất cả vào cuộc họp và kho cá nhân; thư mục mới tên "<tên cuộc họp> – ngày bắt đầu dự kiến (giờ Việt Nam)"; thư mục cũ dùng lại; bị xoá thì tạo lại ở gốc | Ghi dòng tệp lưu trữ trước, đẩy byte lên kho, rồi trong **một** giao dịch có khoá cuộc họp: kiểm lại quyền/trạng thái, tìm hoặc tạo thư mục, tạo tài liệu kho + tài liệu cuộc họp (tài liệu chung, chưa gắn mục nghị sự). Hai bản ghi trỏ **cùng** một tệp lưu trữ; tải lại cùng tên vẫn là tài liệu mới | Khớp (khác thứ tự kỹ thuật so với sequence — xem 2.5) |
| 5. Bất kỳ tệp nào không thêm được thì không thêm gì; EX-5 | Lỗi ở bất kỳ bước nào → xoá các tệp đã đẩy trong lần đó; giao dịch lỗi cũng xoá; dòng tệp lưu trữ còn lại do job dọn xoá sau thời gian chờ; trả `503/MSG166` | Khớp. **Lưu ý câu chữ**: đặc tả ghi "EX-5 applies" cho mọi trường hợp, nhưng tệp không hợp lệ là EX-3 (`MSG164`), không phải EX-5 |
| 6. Làm mới danh sách, hiện số tài liệu đã thêm, `MSG161` | Trả `201/MSG161` kèm `addedCount` và danh sách tài liệu; hộp thoại đóng, hiện thông báo, chuyển sang nhóm "Tài liệu chung", đọc lại danh sách | Khớp |
| — (đặc tả không có) | Sau khi thêm thành công, máy chủ báo cho mọi người đang trong cuộc họp "danh sách tài liệu đã đổi" (giới hạn 2 giây, không giữ phản hồi) | **Thiếu trong đặc tả**, chỉ có trong contract. Đề xuất: thêm vào sequence, không thành BR |

### 2.2 Các luồng ngoại lệ

| Luồng | Code | Kết luận |
|---|---|---|
| EX-1 mất vai trò | `403/MSG162` | Khớp |
| EX-2 / EX-4 (chọn từ kho, tài liệu đã có trong cuộc họp) | Không thuộc AF-1; chưa xây (SCRUM-58) | Ngoài phạm vi, đặc tả đã ghi "chờ SCRUM-58" |
| EX-3 tệp không hợp lệ | `400/MSG164`, `errors["files[i]"]` chỉ từng tệp | Khớp, trừ **tên tệp không dùng được** (ký tự điều khiển/ký tự vô hình): code trả `MSG164`, đặc tả chưa nêu → **lệch nhỏ chưa ghi** |
| EX-5 không thêm được | `503/MSG166`, không tệp nào được thêm | Khớp |
| EX-6 cuộc họp đã kết thúc | `409/MSG172`, kể cả khi kết thúc giữa lúc đang tải (kiểm lại dưới khoá) | Khớp |
| EX-7 cuộc họp không truy cập được | `404/MSG174`, cùng một body cho không tồn tại và bị giấu; người từng quản lý mà không còn xem được nhận `MSG174`, không phải `MSG162` | Khớp |
| EX-8 quá giới hạn | `400/MSG288` cho > 10 tệp, > 200 MB, và cả khi khai báo > 201 MB (từ chối trước khi nhận tệp) | Khớp |

### 2.3 Business Rules

| BR | Đối chiếu | Kết luận |
|---|---|---|
| BR-184, BR-195 | Chỉ Chủ toạ/Thư ký hiện tại, từ lúc xem được cuộc họp đến khi kết thúc | Khớp |
| BR-185, BR-282 | Chặn thêm sau khi kết thúc | Khớp |
| BR-187, BR-214, BR-215 | Chỉ PDF/DOCX, ≤ 50 MB, nhận diện theo nội dung, đuôi khớp nội dung | Khớp |
| BR-192, BR-226–BR-228 | Tên thư mục theo tên cuộc họp + ngày bắt đầu dự kiến giờ Việt Nam; dùng lại / tạo lại ở gốc | Khớp |
| BR-194, BR-286, BR-210 | Tất cả hoặc không; tài liệu kho và tài liệu cuộc họp trỏ cùng một tệp | Khớp |
| BR-197 | Ghi người thêm và thời điểm | Khớp |
| BR-209 | Tệp do chính người thao tác tải lên | Khớp |
| BR-213 | Job dọn tệp mồ côi | Khớp (có test riêng) |
| BR-183, BR-216 | Giữ nguyên byte, không chuyển đổi | Khớp |
| BR-319 | 10 tệp / 200 MB mỗi lần | Khớp |

### 2.4 Message

| Mã | Đặc tả / catalogue | Code | Kết luận |
|---|---|---|---|
| `MSG161` | 201, `addedCount` + danh sách | Đúng | Khớp |
| `MSG162` | 403, mất vai trò | Đúng | Khớp |
| `MSG164` | 400, không có tệp / không phải multipart / hỏng / đuôi hoặc nội dung sai / rỗng / > 50 MB | Đúng, thêm trường hợp tên tệp không dùng được | Khớp (thiếu một tình huống nhỏ trong câu chữ) |
| `MSG166` | 503, R2/DB lỗi hoặc chưa cấu hình kho | Đúng | Khớp |
| `MSG172` | 409, đã kết thúc | Đúng | Khớp |
| `MSG174` | 404, không xem được / không tồn tại, cùng body | Đúng | Khớp |
| `MSG288` | 400, > 10 tệp hoặc > 200 MB | Đúng; catalogue chưa nêu trường hợp khai báo > 201 MB (contract có) | Khớp, nên thêm nửa câu |

`MSG288` là mã thông báo; **không liên quan** BR-288 (BR-288 nói về tài liệu của biểu quyết) — dễ nhầm khi rà.

### 2.5 Sequence `UC33a_UploadDocumentsToMeeting`

| Nội dung | Đánh giá |
|---|---|
| Kiểm quyền/trạng thái **trước** khi kiểm tệp; các nhánh `MSG174/162/172`, `MSG164`, `MSG288`, `MSG166`, `MSG161`; xoá tệp khi lỗi; kiểm lại dưới khoá | Khớp |
| Thiếu bước **chặn theo kích thước khai báo trước khi đọc thân request** (đặc tả EX-8 đã nói, sequence chưa vẽ) | Lệch (kỹ thuật) |
| Thiếu bước **ghi dòng tệp lưu trữ trước khi đẩy byte** (ADR-0024 §4 — để không có object nào tồn tại mà không có dòng) | Lệch (kỹ thuật) |
| Vẽ "lấy thư mục" **trước** khi đẩy tệp; code tìm/tạo thư mục **trong giao dịch**, sau khi byte đã lên kho | Lệch (kỹ thuật) |
| Không có `pageCounts` (số trang PDF do trình duyệt gửi) và không có sự kiện "danh sách đã đổi" | Thiếu |
| `MSG164` chỉ vẽ một câu, không vẽ `errors["files[i]"]` | Thiếu (nhỏ) |

Kết luận: **nghiệp vụ khớp**, chỉ cần vẽ lại theo code/contract. Không đổi yêu cầu.

### 2.6 Class diagram FE-07 (phần tải lên)

| Nội dung | Đánh giá |
|---|---|
| Có `uploadDocuments` từ màn hình → điều khiển → dịch vụ; `FileStorage.write/delete`; `FolderRepository`; thực thể `Folder`, `Document`, `StoredFile`; job dọn tệp mồ côi; `detectContentType` | Khớp |
| Thiếu phần **kiểm giới hạn** (10 tệp / 200 MB / 50 MB mỗi tệp) và **kiểm tên tệp** — code gom trong một bộ quy tắc dùng chung, diagram không có | Thiếu (nên thêm một lớp «utility» hoặc ghi chú) |
| `uploadDocuments` ở dịch vụ không có `pageCounts` (ở điều khiển thì có) | Lệch (nhỏ) |
| Không có sự kiện "danh sách đã đổi" | Thiếu (có thể để ngoài diagram) |

---

## 3. SCRUM-21 — UC-32a "Xem tài liệu cuộc họp"

### 3.1 Luồng chính và luồng thay thế

| Bước | Code | Kết luận |
|---|---|---|
| 1. Mở tab Tài liệu, hoặc panel Tài liệu trong phòng họp | Có cả hai, dùng chung một màn hình với hai chế độ (trang / phòng) | Khớp |
| 2. Kiểm quyền xem cuộc họp hiện tại; không có quyền đọc tài liệu riêng; khách mời theo quyền thành viên | Cùng một luật với màn chi tiết cuộc họp; không xem được hoặc không tồn tại → `404/MSG156` cùng body. **Khách mời chưa đăng nhập được** (chưa có UC-04) | Khớp phần trong nước; **Chưa xây** phần khách mời (đã ghi Jira 10257 #4) |
| 3. Hiện tài liệu theo mục nghị sự đúng thứ tự; tên, loại, người thêm, số góp ý; một tài liệu gắn nhiều mục hiện ở từng mục; không gắn mục là tài liệu chung; tài liệu đã gỡ không hiện; sau khi kết thúc chỉ đọc | Đúng. Thêm: trả **mọi** mục nghị sự kể cả mục chưa có tài liệu; số trang (chỉ PDF); thời điểm thêm; mục đang bàn; số góp ý đếm cả trả lời. Tài liệu đã gỡ bị loại; đọc vẫn được sau khi kết thúc, nút thêm bị ẩn | Khớp (chi tiết API nhiều hơn đặc tả — không sai) |
| 4. Xem, đổi cách nhóm, chọn tài liệu mở trình xem | Có hai nhóm: "Theo nội dung họp" và "Tài liệu chung"; nhóm Biểu quyết **chưa có** | **Lệch (đã ghi)** 10257 #1, 10273 #1 |
| AF-1 xem theo Biểu quyết, kèm xác nhận đã đọc của từng người | Chưa có bảng gắn tài liệu với biểu quyết; chờ UC-74 | **Chưa xây** (đã ghi) |
| AF-2 không có tài liệu | Có câu "Cuộc họp chưa có mục nghị sự." / "Chưa có tài liệu chung." | Khớp |
| AF-3 panel trong phòng: cùng quyền, mục đang bàn lên đầu, chỉ đọc, mở tài liệu không rời phòng | Panel chỉ đọc, mục đang bàn mở đầu và các mục khác thu gọn, tài liệu mở tab mới; **máy chủ không đổi thứ tự** — máy chủ trả "mục đang bàn", trình duyệt tự đưa lên đầu | Khớp về hành vi; **sequence + class diagram đang vẽ sai** (xem 3.4) |
| EX-1 `MSG156` | Đúng, cùng body cho mọi lý do | Khớp |
| EX-2 `MSG157` | Đúng, không hiện như danh sách rỗng | Khớp |

### 3.2 Business Rules

| BR | Đối chiếu | Kết luận |
|---|---|---|
| BR-275, BR-211 | Ai xem được cuộc họp đọc được tài liệu; tài liệu đã gỡ không hiện | Khớp |
| BR-277, BR-280 | Một tài liệu nhiều mục hiện ở từng mục; không gắn mục là tài liệu chung | Khớp (dữ liệu liên kết do chương trình nghị sự tạo) |
| BR-282 | Sau khi kết thúc chỉ đọc | Khớp |
| BR-315, BR-316 | Mở từ phòng ở tab mới, không rời phòng; panel chỉ đọc | Khớp |
| BR-181, BR-200 | Khách mời đọc như thành viên | **Chưa xây** (UC-04) |
| BR-290, BR-291 | Xác nhận đã đọc sau khi tới trang cuối | **Chưa xây** (UC-74) |

### 3.3 Message

`MSG156` (404), `MSG157` (500) — đúng hợp đồng, đúng câu. Xem mục 5 về việc bảng tiếng Anh còn ghi "Dành sẵn".

### 3.4 Sequence `UC32a_ViewMeetingDocuments` và class diagram

| Chỗ vẽ | Thực tế | Kết luận |
|---|---|---|
| Lời gọi `getMeetingDocuments(..., fromRoom)` — máy chủ đổi thứ tự khi mở từ phòng | Không có tham số này; máy chủ trả `currentAgendaItemId`, trình duyệt sắp | Lệch (đã ghi 10257 #2) |
| Nhóm theo **Biểu quyết**, lấy "liên kết biểu quyết" từ cơ sở dữ liệu | Chưa có bảng đó | Lệch (đã ghi) |
| Kiểm "lời mời khách còn hiệu lực" | Chưa có xác thực khách | Lệch (đã ghi) |
| Dịch vụ trả "chỉ đọc sau khi kết thúc" | Máy chủ không trả cờ này; trình duyệt suy ra từ trạng thái cuộc họp | Lệch (nhỏ, cần sửa lời) |
| Không có bước nghe "danh sách đã đổi" | Trình duyệt nghe và đọc lại danh sách | Thiếu (nhỏ) |
| Class diagram: `switchGrouping(byItem \| byVote \| general)` | Chỉ có `byItem` / `general` | Lệch (đã ghi) |
| Class diagram: `groupDocuments(documents, agenda, votes)` | Không có `votes`; cũng không có lớp Vote nào trong diagram | Lệch (đã ghi) |
| Class diagram: `getMeetingDocuments(..., fromRoom)` | Như trên | Lệch (đã ghi) |

---

## 4. SCRUM-21 — UC-32c "Xem trước tài liệu cuộc họp"

### 4.1 Luồng chính và luồng thay thế

| Bước | Code | Kết luận |
|---|---|---|
| 1. Chọn tài liệu từ tab, từ mục nghị sự hoặc từ panel trong phòng | Có | Khớp |
| 2. Kiểm quyền xem + tài liệu còn gắn với cuộc họp | Đúng; tài liệu đã gỡ, của cuộc họp khác, không tồn tại → `404/MSG156` (không trả 403 để khỏi lộ ID) | Khớp |
| 3. Mở trình xem: tên tệp, nội dung, độ mật; mở từ phòng thì ở tab riêng; cuộc họp Mật/Tối mật ghi nhật ký kiểm toán | Trình duyệt dựng bản xem từ đúng byte gốc; mở từ phòng ở tab mới; máy chủ ghi `document.viewed` cho Tối mật/Mật **trước khi** gửi byte. Độ mật do trình duyệt lấy từ màn chi tiết cuộc họp, không nằm trong phản hồi tệp | Khớp; **sequence vẽ sai chỗ "dịch vụ trả độ mật"** |
| 3 (tiếp). Không ghi được nhật ký thì không gửi tệp | Có (trả `MSG158`/`MSG160`) | **Thiếu trong đặc tả** (đã ghi Jira 10257 #6) |
| 4. Đọc; tới trang cuối thì ghi nhận để xác nhận đã đọc (UC-74); sau khi kết thúc chỉ đọc; tải về khi không Mật | Tải về bị chặn khi Mật; đọc vẫn được sau khi kết thúc; **chưa ghi nhận trang cuối** | **Chưa xây** (đã ghi 10257 #7, thuộc UC-74/SCRUM-96) |
| Rủi ro BR-188: xem vẫn nhận byte gốc | Đúng; PO chấp nhận 25/10, đặc tả đã ghi "Known risk" | Khớp |
| AF-1 tải về: kiểm lại quyền, tài liệu, độ mật; trả đúng tệp; Tối mật ghi `document.downloaded` | Đúng | Khớp |
| EX-1 `MSG156` | Đúng, kể cả khi ID thuộc cuộc họp khác | Khớp |
| EX-2 `MSG158` | Đúng cho mọi lỗi đọc tệp/kho/DB/kiểm toán | Khớp |
| EX-3 `MSG159` (403) | Đúng, kể cả khi độ mật vừa đổi sau lúc mở trình xem | Khớp |
| EX-4 `MSG160` | Đúng | Khớp |
| POST-3 ghi nhận trang cuối | Chưa có | **Chưa xây** (đã ghi) |

### 4.2 Business Rules

BR-181 (khách mời) và BR-290/291 **chưa xây**; BR-183, BR-188, BR-211, BR-216, BR-275, BR-282, BR-315, BR-318 khớp.

### 4.3 Message

`MSG156`/`MSG157`/`MSG158`/`MSG159`/`MSG160` đúng mã, đúng HTTP, đúng câu. Xem mục 5.

### 4.4 Sequence `UC32c_PreviewMeetingDocument`

| Chỗ vẽ | Thực tế | Kết luận |
|---|---|---|
| Kiểm "lời mời khách còn hiệu lực" | Chưa có xác thực khách | Lệch (đã ghi) |
| Dịch vụ trả "nội dung, tên tệp và **độ mật**" | Không trả độ mật; trình duyệt tự lấy từ màn chi tiết | Lệch (nhỏ) |
| Ghi kiểm toán sau khi đọc tệp, trước khi gửi | Đúng | Khớp |
| Không có nhánh "ghi kiểm toán lỗi → không gửi tệp" | Có trong code | Thiếu |
| Không có bước ghi nhận trang cuối (chưa xây) | — | Cần ghi chú "chưa xây" để diagram không bị đọc như as-built |

---

## 5. Danh mục mã thông báo — một lỗi tự mâu thuẫn

Cùng file `docs/03-api/00-danh-muc-ma-thong-bao.md`:

- Dòng 164 (đoạn tổng hợp): "SCRUM-21 đã triển khai `MSG156`–`MSG160`".
- Bảng tiếng Anh §V.2, dòng 606: "Các mã dưới đây **dành sẵn** ... chưa phải contract đã triển khai", và dòng 610–614 vẫn ghi
  `MSG156`–`MSG160` là "Dành sẵn".
- Bảng tiếng Việt phía trên (dòng 148–160) lại ghi đúng là đã triển khai.

Đây là **lỗi tài liệu**, không phải lỗi code: phải đổi 5 dòng §V.2 sang "Đã triển khai SCRUM-21" và sửa câu mở đầu của mục.
Ngoài ra `MSG288` nên thêm "hoặc khai báo request quá 201 MB" cho khớp contract.

---

## 6. Report 3 §III — màn hình 7.3, 7.4, 7.5, 8.6

| Màn | Câu trong Report 3 | Thực tế | Kết luận |
|---|---|---|---|
| 7.3 | "ba nhóm ... By meeting content, Voting, General"; "Switch Grouping ... Voting"; dòng "Vote documents ... Read/Not read" | Chỉ hai nhóm; nhóm Biểu quyết ẩn vì chưa có dữ liệu | Lệch (đã ghi 10273 #1) |
| 7.3 | "verified Guests" trong quyền xem | Khách mời chưa đăng nhập được | Lệch (đã ghi 10257 #4) |
| 7.3 | Nút "Add document [Thêm tài liệu]" | Nút thật là "Thêm tài liệu chung" | Lệch (đã ghi 10274 #1) |
| 7.3 | "order number" theo thứ tự chương trình | Số thứ tự đánh theo danh sách đang hiện (1..n), có thể khác số vị trí | Lệch (đã ghi 10273 #4) |
| 7.4 | Có hai tab "Từ máy tính / Từ kho của tôi" | Không có tab nào; chỉ có vùng kéo-thả, tab kho chờ SCRUM-58 | Lệch (đã ghi 10274 #1) |
| 7.4 | Access notice "kể cả khách mời đã xác minh" | Câu thật: "Ai xem được cuộc họp đều đọc được tài liệu đã thêm." | Lệch (đã ghi 10273 #6) |
| 7.4 | Huỷ luôn đóng được hộp thoại | Khi đã gửi hết body, nút Huỷ bị khoá (máy chủ có thể đang ghi) | Lệch (đã ghi 10274 #3) |
| 7.5 | "For a Secret document the viewer's name and viewing time are printed as a watermark on every page" | **Không đóng dấu tên người xem** — PO chốt 25/10 | **Lệch, trái quyết định PO** (10265) |
| 7.5 | "Each view or download of a **Restricted** or Secret document" | Mức mật là Tối mật (Confidential) hoặc Mật (Secret); "Restricted" không phải mức mật | Lệch (sai thuật ngữ) |
| 7.5 | Tab Bình luận, chỗ bôi đậm vùng góp ý bám vào | Chưa có (SCRUM-23) | Chưa xây, đang viết như đã có |
| 8.6 | "The In-Room Documents Panel is **planned** ... not part of the current version"; Screen List ghi "Planned" | Panel đã chạy trong phòng họp, chỉ đọc, mục đang bàn lên đầu, mở tab mới | **Lệch, tài liệu đã cũ** |
| 8.6 | "This screen allows ... Guest"; "read and comment on it" | Khách mời chưa dùng được; chưa có góp ý trong trình xem | Lệch (đã ghi) |

Ngoài ra, trên điện thoại (≤ 560 px) phòng họp gom Nội dung họp, Điểm danh, Tài liệu vào nút ⋯ — 8.6 chưa ghi (đã ghi 10273 #8).

---

## 7. Bảng quyết định

### 7.1 Sửa tài liệu theo code (đã ghi Jira hoặc PO đã chốt) — không cần hỏi lại

| # | Sửa gì | Căn cứ |
|---|---|---|
| A1 | UC-32a AF-1: ghi rõ nhóm Biểu quyết **chưa có dữ liệu, ẩn tới UC-74** (giữ mục tiêu, không xoá) | 10257 #1, 10273 #1 |
| A2 | UC-32a AF-3: nói rõ **trình duyệt** đưa mục đang bàn lên đầu theo `currentAgendaItemId`; máy chủ không đổi thứ tự | 10257 #2 |
| A3 | UC-32a/32c PRE-1 và §III: khách mời **chưa xây** (UC-04) | 10257 #4 |
| A4 | UC-32c bước 3/EX-2: thêm "không ghi được nhật ký kiểm toán thì không gửi tệp" | 10257 #6 |
| A5 | UC-32c POST-3/BR-290: ghi **chưa xây, thuộc UC-74** | 10257 #7 |
| A6 | Danh mục MSG §V.2: `MSG156`–`MSG160` → "Đã triển khai SCRUM-21"; sửa câu mở đầu §V.2; `MSG288` thêm trường hợp khai báo > 201 MB | code + contract |
| A7 | Report 3 §III 7.3/7.4/7.5/8.6 + Screen List: hai nhóm; "Thêm tài liệu chung"; bỏ "verified Guest"; bỏ watermark; "Restricted" → "Tối mật"; tab Bình luận + bôi đậm = chưa xây (SCRUM-23); 8.6 = đã xây, không còn "planned"; nút Huỷ khoá sau khi gửi hết | PO 10265, 10273, 10274 |
| A8 | Sequence UC33a: chặn theo kích thước khai báo trước khi đọc thân; ghi dòng tệp lưu trữ trước khi đẩy byte; tìm/tạo thư mục trong giao dịch; thêm `pageCounts` và sự kiện "danh sách đã đổi" | contract, ADR-0024 §4 |
| A9 | Sequence UC32a/32c: bỏ `fromRoom`, bỏ nhóm Biểu quyết, bỏ kiểm lời mời khách; bỏ "dịch vụ trả độ mật"; thêm `currentAgendaItemId`, sự kiện "danh sách đã đổi", nhánh kiểm toán lỗi | code |
| A10 | Class diagram FE-07: bỏ `fromRoom`, bỏ `votes`/`byVote`; `uploadDocuments` có `pageCounts`; thêm bộ quy tắc kiểm tệp (10/200/50, tên, nhận diện nội dung) | code |

### 7.2 Cần hỏi Lộc (lệch chưa thấy ghi trên Jira)

| # | Vấn đề | Đề xuất |
|---|---|---|
| B1 | Tên tệp có ký tự điều khiển/vô hình bị từ chối bằng `MSG164`, UC-33a EX-3 chưa nêu | Thêm một mệnh đề vào EX-3 (không cần BR, không cần PO) |
| B2 | Sự kiện "danh sách tài liệu đã đổi" có trong contract nhưng không có trong UC nào | Chỉ vẽ vào sequence + ghi chú ở UC-32a; không thành BR |
| B3 | UC-32a bước 3 không nói máy chủ trả **cả mục nghị sự chưa có tài liệu** (cần cho nút thêm của SCRUM-58) | Thêm nửa câu vào bước 3 |
| B4 | UC-33a AF-1 bước 5 ghi "EX-5 applies" cho mọi lỗi, trong khi tệp sai là EX-3 | Sửa thành "EX-3 hoặc EX-5" |
| B5 | Ba chỗ sequence UC33a vẽ khác thứ tự code (đều không đổi nghiệp vụ) | Chỉ cần Lộc xác nhận "vẽ theo code" là sửa được |

### 7.3 Chấp nhận hoặc mở lại ticket

| # | Vấn đề | Đề xuất |
|---|---|---|
| C1 | Request quá lớn: máy chủ từ chối ngay khi trình duyệt còn đang gửi, nên trình duyệt thường chỉ thấy lỗi mạng, không đọc được `MSG288` (câu hỏi mở ở comment 10260 điểm 3) | **Chấp nhận + ghi chú** (giới hạn của trình duyệt). Nếu PO muốn báo rõ thì phải chia lô tải lên — việc mới, mở ticket riêng |
| C2 | Tài liệu Mật: chặn tải về nhưng lần xem vẫn nhận byte gốc | PO đã chấp nhận 25/10; đặc tả đã ghi rủi ro — không mở lại |
| C3 | Kiểm toán fail-closed: kho/DB lỗi thì người dùng không xem được tài liệu | Đã ghi; đề xuất giữ, chỉ bổ sung câu vào UC |
| C4 | Dữ liệu dev/server test: hai tài liệu fixture của chương trình nghị sự không có tệp trên kho nên hiện trong danh sách mà mở không được | Không sửa tài liệu. Báo Lộc/leader: seed object hoặc bỏ fixture trước khi Tester nhận |
| C5 | Nhóm Biểu quyết (kèm xác nhận đã đọc) | Nếu PO muốn có trong bản này thì phải mở ticket cho BE/FE; hiện chờ UC-74 |

---

## 8. Việc không phải của BA

- Contract `docs/03-api/11-tai-lieu-cuoc-hop.md` và endpoint: Lộc, cùng commit code.
- Danh mục MSG phần tiếng Việt (dòng 134–160): Lộc; phần tiếng Anh §V.2 là bản trích của Report 3 nên BA sửa cùng Report 3.
- Test report (Report 5) và kiểm thử chấp nhận: Tester.
- ERD hiện tại `docs/13-erd/hien-tai`: sinh từ database, không sửa tay.

---

## 9. Đề xuất thứ tự làm sau khi Khánh chốt

1. SCRUM-107 trước (nhỏ): A8, A10, hai câu §III 7.4, B1/B4 nếu Lộc đồng ý — một commit.
2. SCRUM-21: A1–A7, A9 — một commit riêng.
3. Sau khi sửa: chạy `python scripts/sync_reports.py --report 3` (Report 3 sửa trong DOCX, không sửa `.md`),
   `python scripts/sync_sequence_drawio_png.py`, xuất lại PNG class diagram.
4. Comment tóm tắt lên từng ticket, rồi chuyển sang **Tester** theo `docs/DEVELOPMENT_WORKFLOW.md`.

---

## 10. Đã áp dụng (Khánh duyệt "làm luôn", 27/10/2026)

Làm hết nhóm 7.1 (A1–A10) và cả 5 mục 7.2 (B1–B5); 7.3 giữ nguyên cách xử lý đã ghi (C1 ghi chú, C3 bổ sung câu vào UC,
C4 báo Lộc, C5 chờ UC-74).

| Nơi | Nội dung |
|---|---|
| `docs/01-requirements/05-use-case-specifications.md` | UC-32a (bước 2, 3, AF-1, AF-3), UC-32c (Description, POST-3, bước 3, 4), UC-33a (AF-1 bước 5, EX-3) — **sửa trong Report 3 DOCX mục II rồi để `sync_reports.py` sinh lại file này**, vì file là bản trích tự động |
| `docs/03-api/00-danh-muc-ma-thong-bao.md` | `MSG156`–`MSG160` → "Đã triển khai SCRUM-21" (kèm câu mở đầu §V.2); `MSG288` thêm trường hợp khai báo > 201 MB; `MSG164` thêm tên tệp không dùng được |
| `docs/06-sequencediagram/document-management/*.txt` | Viết lại UC-33a (thứ tự code, gộp bước kiểm cho gọn khung), UC-32a (bỏ `fromRoom`, bỏ nhóm Biểu quyết, mục nghị sự rỗng, tự đọc lại danh sách), UC-32c (bỏ "dịch vụ trả độ mật", kiểm toán fail-closed, chưa ghi nhận trang cuối) |
| `docs/06-sequencediagram/document-management/*.png` | Xuất lại cả 5 ảnh của thư mục bằng `node scripts/export-diagrams.js`; đã xem ảnh, nhãn nằm trong khung |
| `docs/07-classdiagram/feature-07-document-management-class-diagram.drawio` (+ PNG) | A10; thêm lớp `DocumentFileRules`; bỏ `FolderRepository`; PNG xuất lại `--scale 2`, 4365×3279, đã xem ảnh |
| Report 3 DOCX | 29 câu ở mục III (7.3, 7.4, 7.5, 8.6, Screen List, mức mật, V.1) + 2 ô V.2 + 10 dòng mục II. Chi tiết từng câu: [note-may-nha/patch-scrum-107-21-asbuilt.md](note-may-nha/patch-scrum-107-21-asbuilt.md) |

Còn lại: ba tab UC-32a/32c/33a trong `TrustCabinet_Sequence_Diagrams.drawio` chưa dựng lại (đã ghi ở
[README thư mục sequence](../../06-sequencediagram/document-management/README.md)); ảnh wireframe 7.3/7.4/7.5/8.6 trong
Report 3 vẫn là ảnh cũ; comment Jira + chuyển Tester chờ có phiên đăng nhập Jira.

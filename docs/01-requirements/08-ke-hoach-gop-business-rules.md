# Kế hoạch gộp Business Rules — lọc đúng loại rồi mới gộp

> Bản nháp để team duyệt. Chưa sửa DOCX, chưa đổi mã nguồn.

## 1. Cách làm

Đọc đủ 406 BR đang dùng (414 dòng, trừ 8 BR đã ngừng) và xếp từng BR vào một trong ba loại:

- **BR thật**: một quyết định nghiệp vụ (ai được làm gì, trạng thái chuyển thế nào, ràng buộc dữ liệu). Giữ, viết lại một câu, gộp các BR cùng một quyết định.
- **Không phải BR, chuyển đi**: giao diện (cột, bộ lọc, chuông, phân trang), kiểm tra dữ liệu nhập, giới hạn kích thước, nội dung và kênh thông báo, cơ chế kỹ thuật, ngưỡng nhận dạng giọng. Các thứ này chuyển vào UC Specification của UC tương ứng hoặc NFR, không còn là BR.
- **Trùng**: cùng ý với BR khác, bỏ.

Kết quả: **153 BR** (vùng mục tiêu 90–150, vượt 3 vì thêm BR của PR 491 và các BR còn thiếu; mẫu G158 có 135), 14 nhóm. Mỗi BR một ý, tối đa 30 từ, để dễ kiểm chứng và dễ viết test. Bảng chỉ 2 cột `ID | Rule`, một câu mỗi BR. **Đánh số lại BR-001…BR-153** theo [ADR-0079](../05-decisions/0079-cho-danh-so-lai-br-uc-co-bang-anh-xa.md): mã cũ ngừng dùng, bảng cố định `mã cũ → mã mới` ở [09-bang-anh-xa-br-dot-1.md](09-bang-anh-xa-br-dot-1.md); ADR và note cũ giữ mã cũ và tra bảng đó.

Đính chính: cột Rationale có ở 310 dòng, Enforced By ở 284 dòng (trước đó tôi nói gần như trống là sai). Bỏ cột thì mất nguồn gốc (ngày PO, SCRUM, ADR). Đề xuất: giữ nguồn gốc ở ADR và ghi chú Jira, không giữ trong bảng BR.

## 2. Chỗ còn gắn với BR ID — phải xử lý

| Nơi | Số lần nhắc | Sống hay lịch sử | Việc cần làm |
|---|---|---|---|
| DOCX Report 3 (bảng BR, UC spec, cột Applies To) | 1720 trong UC spec | Sống, nguồn duy nhất | Sửa DOCX: bảng BR còn 2 cột; mỗi UC spec chỉ liệt kê BR còn sống; phần chuyển đi viết thành bước, điều kiện hoặc mục Validation trong UC. Sau đó chạy `sync_reports.py --report 3` |
| `06-business-rules.md`, `05-use-case-specifications.md`, `Report-3_….md`, `uc-map.html` | sinh ra | Sinh tự động | Không sửa tay; sinh lại sau khi sửa DOCX |
| `scripts/sync_requirement_extracts.py` (dòng 57–58) | 2 regex | Sống | Đổi `RULE_ROW`, `RULE_HEADER` sang 2 cột |
| `scripts/trace_br.py` | dựa Applies To | Sống | Bỏ phần "BR không UC nào nhắc"; vẫn kiểm mã nguồn không trích BR không còn trong Report 3 |
| `scripts/sync_requirement_extracts.py` (khoảng dòng 343–364) | logic BR ngừng dùng | Sống | Sửa trong cùng đợt (ADR-0079): đọc bộ BR-001…153; mã cũ không còn nằm ở bảng chính mà ở bảng ánh xạ; 8 BR ngừng từ trước ghi trong bảng ánh xạ |
| `docs/uc-map.html` và script sinh nó | đọc mã BR và UC | Sống | Sửa cùng đợt để đọc mã mới, không gộp nhầm mã cũ và mã mới |
| Mã UC | ADR-0079 cũng cho đánh số lại UC | Sống | Nếu đợt này đổi mã UC thì thêm bảng ánh xạ UC cũ → mới cùng kiểu; kế hoạch này chưa đổi mã UC |
| Sequence diagram (drawio + PNG) | 2842 / 142 tệp | Sống | Đổi nhãn BR cũ sang BR mới hoặc UC; xuất lại PNG |
| `docs/03-api/` | 367 / 33 tệp | Sống | Đổi ID theo bảng ánh xạ |
| `docs/02-design/` | 134 / 6 tệp | Sống | Đổi ID theo bảng ánh xạ |
| Backend (chú thích XML) | 636 / 141 tệp | Sống | Đổi ID trong chú thích; không đổi hành vi |
| Test Web | 175 / 49 tệp | Sống | Đổi ID trong tên hoặc chú thích test |
| Kế hoạch QA `tests/qa/plans` | 311 / 15 tệp | Sống | Đổi ID; đây là bộ hồi quy |
| Frontend (chú thích) | 165 / 62 tệp | Sống | Đổi ID trong chú thích |
| ADR | 101 / 17 tệp | Lịch sử, bất biến | Không sửa; tra bảng ánh xạ mã cũ → mã mới |
| `docs/notes`, test-reports | 2747 / 76 và 3295 / 324 tệp | Ảnh chụp thời điểm | Không sửa; bảng ánh xạ là chỗ tra |

Cách đỡ tốn công: các chỗ "Sống" ngoài DOCX chỉ cần đổi ID theo bảng ánh xạ bằng một script thay thế có kiểm tra (`trace_br.py --check` làm cổng). BR chuyển sang UC thì chú thích đổi thành tên UC. Phần cần người làm là DOCX, vì viết lại nội dung vào UC spec.

## 3. Việc chờ duyệt

Xem mục 6 (tiêu chí review). Sau khi leader duyệt mới sửa DOCX và chạy script đổi ID.

## 4. BR giữ lại và phần chuyển đi, theo nhóm

Mã BR-001…BR-153 theo thứ tự xuất hiện. Cột cuối là BR cũ được gộp vào.

### 1. Tài khoản, đăng nhập, audit

**BR giữ lại**

| BR mới | Nội dung (chỉ luật) | Gộp từ BR cũ |
|---|---|---|
| `BR-001` | Tài khoản bị vô hiệu không đăng nhập hay dùng chức năng được; mọi phiên đang có bị chấm dứt, dữ liệu lịch sử giữ nguyên. | BR-03, BR-18, BR-19 |
| `BR-002` | Tài khoản đang hoạt động đăng nhập được kể cả khi vai trò chưa có quyền nào. | BR-04 |
| `BR-003` | Đăng nhập Google chỉ thành công khi email khớp tài khoản nội bộ có sẵn; không tự tạo tài khoản. | BR-05, BR-06, BR-07 |
| `BR-004` | Thông báo lỗi đăng nhập không cho biết email có tồn tại hay không. | BR-02 |
| `BR-005` | Email là duy nhất giữa các tài khoản. | BR-13 |
| `BR-006` | Tài khoản mới tạo có trạng thái Active. | BR-15 |
| `BR-007` | Chỉ gán được quyền do hệ thống định nghĩa cho vai trò. | BR-23 |
| `BR-008` | Đổi quyền của vai trò hoặc vai trò của người dùng có hiệu lực từ thao tác kế tiếp, không cắt phiên. | BR-313, BR-314 |
| `BR-009` | Thay đổi quản trị tài khoản, vai trò và mọi lần đăng nhập phải ghi audit. | BR-16, BR-197, BR-318, BR-335, BR-426, BR-438, BR-444, BR-446, BR-453 |
| `BR-010` | Ban hành biên bản, xem tài liệu họp Secret, xuất báo cáo và chia sẻ lịch phải ghi audit. | BR-16, BR-197, BR-318, BR-335, BR-426, BR-438, BR-444, BR-446, BR-453 |
| `BR-011` | Thu, nghe mẫu giọng, thêm hoặc gỡ tài liệu họp và đọc thư viện tài khoản bị vô hiệu phải ghi audit. | BR-16, BR-197, BR-318, BR-335, BR-426, BR-438, BR-444, BR-446, BR-453 |
| `BR-012` | Chỉ vai trò có quyền quản trị người dùng xem audit, và audit chỉ đọc, không ai sửa hay xóa. | BR-440 |

**Không phải BR — chuyển đi**

| BR cũ | Chuyển tới | Nội dung bị lấy ra |
|---|---|---|
| BR-01, BR-10, BR-21 | NFR bảo mật | Mật khẩu băm có salt, không hiển thị mật khẩu, băm, token cho ai |
| BR-08 | Trùng BR-04 | Quyền do hệ thống quyết định, không chọn khi đăng nhập |
| BR-09 | NFR bảo mật, UC-03 | Thu hồi refresh token, access token sống tối đa 15 phút |
| BR-11, BR-12, BR-30, BR-31, BR-32, BR-167, BR-439 | Quy ước chung giao diện | Phân trang, trạng thái trống, bộ lọc, tìm không phân biệt dấu, sắp xếp; mỗi màn dùng chung |
| BR-14, BR-17 | UC-06a, UC-06b | Kiểm tra dữ liệu bắt buộc, trường được sửa |
| BR-20, BR-22 | UC-07 | Hạn dùng link đặt lại, điều kiện áp dụng |
| BR-441, BR-442, BR-443 | UC-13 | Cột hiển thị, bộ lọc, cách ghi tên đối tượng của audit |

### 2. Vòng đời cuộc họp

**BR giữ lại**

| BR mới | Nội dung (chỉ luật) | Gộp từ BR cũ |
|---|---|---|
| `BR-013` | Cuộc họp mới tạo ở Draft, giờ bắt đầu sau thời điểm hiện tại và giờ kết thúc dự kiến sau giờ bắt đầu. | BR-24, BR-26, BR-62, BR-162 |
| `BR-014` | Chỉ sửa thông tin cuộc họp ở Draft hoặc Rejected; sửa không đổi trạng thái. | BR-27, BR-28, BR-158 |
| `BR-015` | Cuộc họp Published chỉ Organizer dời giờ hoặc địa điểm, giờ mới phải sau thời điểm hiện tại. | BR-75 |
| `BR-016` | Chỉ Organizer, Approver, Chair, Secretary hoặc người tham dự của cuộc họp truy cập được cuộc họp. | BR-29, BR-117, BR-118, BR-119, BR-121, BR-176 |
| `BR-017` | Danh sách tham dự và phản hồi chỉ hiện cho người xem được cuộc họp. | BR-29, BR-117, BR-118, BR-119, BR-121, BR-176 |
| `BR-018` | Gửi duyệt chuyển Draft hoặc Rejected sang WaitingApproval; trong lúc chờ không sửa thông tin chung. | BR-34 |
| `BR-019` | Mỗi lượt duyệt có một Approver hợp lệ, chỉ người đó duyệt hoặc từ chối; không có Approver thì không gửi duyệt được. | BR-35 |
| `BR-020` | Duyệt chuyển WaitingApproval sang Approved; từ chối chuyển sang Rejected và bắt buộc có lý do. | BR-36 |
| `BR-021` | Chỉ Organizer công bố cuộc họp, chỉ từ Approved, khi có đúng một Chair và ít nhất một Secretary đang hoạt động. | BR-37, BR-41, BR-124, BR-125 |
| `BR-022` | Mọi chuyển trạng thái ghi lịch sử gồm người, giờ, hành động, trạng thái mới và lý do. | BR-38, BR-445 |
| `BR-023` | Lịch sử trạng thái chỉ thêm, không sửa hay xóa. | BR-38, BR-445 |
| `BR-024` | Chỉ Chair hoặc Secretary đang hoạt động bắt đầu cuộc họp, chỉ từ Published và khi đã đủ Chair và Secretary. | BR-127, BR-129, BR-130, BR-131 |
| `BR-025` | Chỉ Chair kết thúc cuộc họp, chỉ khi InProgress; kết thúc đóng phòng họp online. | BR-132, BR-143 |
| `BR-026` | Kết thúc họp không tự đóng biểu quyết; phiếu đã bỏ giữ nguyên. | BR-159 |
| `BR-027` | Cuộc họp InProgress quá 24 giờ sau giờ kết thúc dự kiến tự kết thúc. | BR-460 |
| `BR-028` | Chỉ Organizer hủy cuộc họp, chỉ khi chưa bắt đầu; Cancelled là trạng thái cuối và chỉ đọc. | BR-297, BR-299 |
| `BR-029` | Cuộc họp chưa bắt đầu sau 24 giờ kể từ giờ dự kiến bị hệ thống tự hủy. | BR-301 |
| `BR-030` | Quyền đọc cuộc họp theo trạng thái: Member chỉ khi Published, InProgress, Ended hoặc Cancelled từ Published; Chair, Secretary, Organizer đọc mọi trạng thái; Approver đọc từ khi gửi duyệt, không đọc bản Nháp. | BR-119, BR-118 |

**Không phải BR — chuyển đi**

| BR cũ | Chuyển tới | Nội dung bị lấy ra |
|---|---|---|
| BR-25 | Trùng BR-24 | Mỗi cuộc họp một khoảng thời gian |
| BR-33 | UC-18 | Kiểm tra thông tin bắt buộc trước gửi duyệt |
| BR-76, BR-298, BR-300 | UC-15, UC-75 | Nội dung, kênh thông báo dời lịch, hủy; lý do hủy tối đa 1.000 ký tự |
| BR-163, BR-320 | UC-14 | Link online do hệ thống quản lý; chọn 1–100 người tham dự khi tạo |

### 3. Thông báo

**BR giữ lại**

| BR mới | Nội dung (chỉ luật) | Gộp từ BR cũ |
|---|---|---|
| `BR-031` | Cuộc họp đã công bố phải thông báo cho người được mời, người đổi vai trò, người bị gỡ, khi đổi lịch và khi hủy. | BR-39, BR-90, BR-93 |
| `BR-032` | Lỗi gửi thông báo không hoàn tác thao tác chính. | BR-39, BR-90, BR-93 |
| `BR-033` | Mỗi người chỉ đọc và đổi trạng thái thông báo của chính mình. | BR-47, BR-52 |
| `BR-034` | Mở liên kết trong thông báo phải kiểm tra lại quyền truy cập. | BR-47, BR-52 |
| `BR-035` | Không ai nhận thông báo trong ứng dụng về hành động của chính mình. | BR-322 |

**Không phải BR — chuyển đi**

| BR cũ | Chuyển tới | Nội dung bị lấy ra |
|---|---|---|
| BR-40, BR-42, BR-43, BR-44, BR-45, BR-91, BR-92, BR-94, BR-95, BR-164, BR-303, BR-307, BR-411 | UC-20, UC-28, UC-21, UC-61/64/65 | Nội dung, kênh, thời điểm, thử lại, link; nhắc họp trước 15 phút |
| BR-46, BR-48, BR-49, BR-50, BR-51, BR-53 | UC-66, UC-67, UC-68 | Hành vi chuông, đánh dấu đã đọc, thời gian thực |

### 4. Thành phần, vai trò, lời mời

**BR giữ lại**

| BR mới | Nội dung (chỉ luật) | Gộp từ BR cũ |
|---|---|---|
| `BR-036` | Chỉ Secretary hiện tại và Chair quản lý danh sách tham dự; Organizer không có quyền riêng. | BR-79, BR-80 |
| `BR-037` | Danh sách tham dự sửa được ở Draft, Rejected, Approved, Published, InProgress; ở WaitingApproval chỉ đọc. | BR-79, BR-80 |
| `BR-038` | Người tham dự nội bộ là tài khoản đang hoạt động, mỗi người xuất hiện một lần trong cuộc họp. | BR-81, BR-82 |
| `BR-039` | Mỗi người tham dự có đúng một vai trò họp: Chair, Secretary hoặc Member, độc lập với vai trò hệ thống. | BR-85, BR-86, BR-173 |
| `BR-040` | Mỗi cuộc họp tối đa một Chair; Published và InProgress luôn có Chair và ít nhất một Secretary. | BR-122, BR-123 |
| `BR-041` | Trước công bố Secretary gán hoặc đổi Chair. | BR-126, BR-169, BR-294, BR-295, BR-296 |
| `BR-042` | Từ công bố chỉ Chair hiện tại chuyển Chair cho người nội bộ đang hoạt động không phải Secretary, Chair cũ thành Member; Chair bị vô hiệu thì Secretary thay được. | BR-126, BR-169, BR-294, BR-295, BR-296 |
| `BR-043` | Đổi vai trò không đổi phản hồi hay điểm danh; phản hồi và điểm danh độc lập nhau. | BR-96, BR-116 |
| `BR-044` | Gỡ người tham dự hoặc thu hồi lời mời Guest chấm dứt quyền truy cập từ quan hệ đó; quyền độc lập khác còn hiệu lực. | BR-97, BR-100, BR-157 |
| `BR-045` | Phản hồi và điểm danh của người đã gỡ giữ làm lịch sử, không tính vào danh sách, quyền hay số vai trò hiện tại. | BR-102, BR-103 |
| `BR-046` | Chỉ chủ lời mời trả lời, chỉ Accepted hoặc Declined, một lần khi Published hoặc InProgress, không đổi được; Chair không trả lời. | BR-104, BR-105, BR-106 |
| `BR-047` | Chưa trả lời coi như tham dự. | BR-235, BR-321 |
| `BR-048` | Declined chỉ ẩn cuộc họp khỏi lịch của người đó, không đổi vai trò hay quyền truy cập, kể cả đọc tài liệu. | BR-235, BR-321 |

**Không phải BR — chuyển đi**

| BR cũ | Chuyển tới | Nội dung bị lấy ra |
|---|---|---|
| BR-83, BR-168 | UC-28a | Chọn từng người, thêm nhiều người một lần là một thay đổi nguyên khối |
| BR-88, BR-115 | UC-29 | Trạng thái lời mời ban đầu, cách ghi phản hồi |
| BR-101 | UC-37 | Đánh giá lại quyền vào phòng khi danh sách đổi |

### 5. Khách mời (Guest)

**BR giữ lại**

| BR mới | Nội dung (chỉ luật) | Gộp từ BR cũ |
|---|---|---|
| `BR-049` | Guest là lời mời riêng của cuộc họp, không phải người tham dự nội bộ. | BR-84, BR-87, BR-149, BR-150, BR-151, BR-201 |
| `BR-050` | Guest không có vai trò hệ thống, không giữ Chair hay Secretary. | BR-84, BR-87, BR-149, BR-150, BR-151, BR-201 |
| `BR-051` | Mỗi email có một lời mời Guest; email đã là người nội bộ thì không mời Guest. | BR-84, BR-87, BR-149, BR-150, BR-151, BR-201 |
| `BR-052` | Guest vào bằng lời mời riêng, chỉ trong cuộc họp đó, không cần tài khoản nội bộ. | BR-120, BR-152, BR-154, BR-156 |
| `BR-053` | Guest phải xác minh quyền kiểm soát email. | BR-153, BR-155 |
| `BR-054` | Truy cập Guest kéo dài 12 giờ kể từ xác minh, không gia hạn; hết sớm khi đăng xuất, thu hồi lời mời hoặc họp kết thúc. | BR-153, BR-155 |
| `BR-055` | Guest có quyền như người tham dự khi họp Published hoặc InProgress, trừ điều hành phòng, thư viện cá nhân, chia sẻ tài liệu. | BR-181, BR-203, BR-204, BR-205 |

**Không phải BR — chuyển đi**

| BR cũ | Chuyển tới | Nội dung bị lấy ra |
|---|---|---|
| BR-148, BR-180, BR-182 | UC-28a, UC-04 | Tên hiển thị tự khai, chức danh, cảnh báo khi mời vào họp Secret |
| BR-371 | NFR bảo mật | Giới hạn xin lại mã (1 lần mỗi phút, 20 mã mỗi 24 giờ) |

### 6. Điểm danh

**BR giữ lại**

| BR mới | Nội dung (chỉ luật) | Gộp từ BR cũ |
|---|---|---|
| `BR-056` | Chỉ Secretary đang hoạt động ghi hoặc sửa điểm danh chính thức, khi InProgress. | BR-170, BR-171, BR-178 |
| `BR-057` | Điểm danh áp dụng cho người nội bộ và Guest, giá trị Present hoặc Absent; người chưa được ghi để trống, không tính vắng. | BR-170, BR-171, BR-178 |

**Không phải BR — chuyển đi**

| BR cũ | Chuyển tới | Nội dung bị lấy ra |
|---|---|---|
| BR-172 | UC-31 | Phiên bản điểm danh, từ chối bản cũ, ghi nhiều người một lần |

### 7. Chương trình họp

**BR giữ lại**

| BR mới | Nội dung (chỉ luật) | Gộp từ BR cũ |
|---|---|---|
| `BR-058` | Mục chương trình thuộc một cuộc họp. | BR-64, BR-65, BR-74 |
| `BR-059` | Secretary hoặc Chair thêm, sửa, xóa, sắp xếp mục từ Draft đến InProgress; từ Ended thì khóa. | BR-64, BR-65, BR-74 |
| `BR-060` | Chỉ Chair bắt đầu hoặc hoàn thành mục chương trình. | BR-69, BR-70, BR-71, BR-72 |
| `BR-061` | Mục Pending nào cũng bắt đầu được, nhưng mỗi lúc tối đa một mục InProgress. | BR-69, BR-70, BR-71, BR-72 |
| `BR-062` | Phải bắt đầu mục mới hoàn thành được; hoàn thành không tự bắt đầu mục khác hay kết thúc họp. | BR-69, BR-70, BR-71, BR-72 |
| `BR-063` | Chỉ xóa được mục chưa bắt đầu (Pending). | BR-237 |
| `BR-064` | Presenter là người tham dự hiện tại hoặc Guest của cuộc họp. | BR-306, BR-308 |
| `BR-065` | Gỡ người khỏi cuộc họp thì mục của họ không còn Presenter. | BR-306, BR-308 |
| `BR-066` | Mỗi mục có tối đa một kết luận. | BR-309, BR-310 |
| `BR-067` | Chair hoặc Secretary ghi kết luận cho mục InProgress hoặc Done, đến khi dựng bản nháp biên bản. | BR-309, BR-310 |

**Không phải BR — chuyển đi**

| BR cũ | Chuyển tới | Nội dung bị lấy ra |
|---|---|---|
| BR-66, BR-67, BR-68, BR-238, BR-239 | UC-23a–23d | Trường bắt buộc, vị trí, thứ tự, gỡ liên kết tài liệu khi xóa mục |
| BR-73 | Quy ước chung giao diện | Tự làm mới chương trình cho người đang xem |
| BR-311 | UC-77 | Ghi người sửa và giờ sửa |
| BR-312 | Trùng BR-384 | Biên bản chứa mục và kết luận |

### 8. Tài liệu, thư viện cá nhân, góp ý

**BR giữ lại**

| BR mới | Nội dung (chỉ luật) | Gộp từ BR cũ |
|---|---|---|
| `BR-068` | Tệp tải lên không đổi nội dung và không có phiên bản; muốn đổi phải tải tệp mới thay tài liệu họp. | BR-183, BR-206, BR-208, BR-216, BR-229 |
| `BR-069` | Chỉ nhận tệp PDF và DOCX. | BR-187 |
| `BR-070` | Chỉ Chair hoặc Secretary thêm hoặc gỡ tài liệu họp và liên kết mục chương trình. | BR-184, BR-185, BR-195 |
| `BR-071` | Thêm hoặc gỡ tài liệu chỉ từ lúc xem được cuộc họp đến khi kết thúc. | BR-184, BR-185, BR-195 |
| `BR-072` | Tài liệu họp lấy từ thư viện cá nhân của người thêm hoặc từ máy; tệp tải thẳng vào họp cũng vào thư viện cá nhân. | BR-193, BR-209, BR-210 |
| `BR-073` | Một tệp chỉ có một đính kèm hoạt động trong một cuộc họp. | BR-193, BR-209, BR-210 |
| `BR-074` | Gỡ tài liệu khỏi họp không xóa khỏi thư viện cá nhân hay họp khác, và gỡ khỏi mọi mục chương trình. | BR-186, BR-211, BR-212, BR-231, BR-281, BR-284 |
| `BR-075` | Bình luận của tài liệu bị gỡ giữ làm lịch sử; thêm lại là bắt đầu mới, không có bình luận cũ. | BR-186, BR-211, BR-212, BR-231, BR-281, BR-284 |
| `BR-076` | Xóa tài liệu khỏi thư viện cá nhân không gỡ nó khỏi cuộc họp. | BR-186, BR-211, BR-212, BR-231, BR-281, BR-284 |
| `BR-077` | Tệp không còn nơi nào dùng bị dọn định kỳ; lịch sử và bình luận giữ lại. | BR-213, BR-232 |
| `BR-078` | Tài liệu chỉ gắn vào mục chương trình của cùng cuộc họp. | BR-278 |
| `BR-079` | Ai xem được cuộc họp đọc được tài liệu của họp, trừ tài liệu chỉ gắn phiếu nháp (chỉ Chair và Secretary thấy). | BR-275, BR-282 |
| `BR-080` | Sau khi họp kết thúc, tài liệu và bình luận chỉ đọc. | BR-275, BR-282 |
| `BR-081` | Tài liệu họp Secret chỉ đọc trực tuyến, không tải về. | BR-188 |
| `BR-082` | Ai xem được tài liệu bình luận được trước khi họp kết thúc. | BR-249 |
| `BR-083` | Thư viện cá nhân và thư mục thuộc một người, chỉ người đó truy cập. | BR-219, BR-242 |
| `BR-084` | Không chuyển thư mục vào chính nó hoặc thư mục con; chỉ xóa được thư mục rỗng. | BR-222, BR-223 |
| `BR-085` | Admin chỉ đọc thư viện của tài khoản bị vô hiệu, không bao giờ thấy tệp của họp Secret. | BR-334 |

**Không phải BR — chuyển đi**

| BR cũ | Chuyển tới | Nội dung bị lấy ra |
|---|---|---|
| BR-191, BR-220, BR-221, BR-224, BR-225, BR-245, BR-276 | UC-34, UC-71, UC-72 | Cấp thư mục tối đa 5, tên thư mục, đổi tên, di chuyển |
| BR-192, BR-226, BR-227, BR-228 | UC-33a | Thư mục tự tạo cho họp: tên, tạo lần đầu, dùng lại, tạo lại |
| BR-194, BR-286, BR-304, BR-319 | UC-33a, UC-33b, UC-36 | Tải thêm cả nhóm hoặc không, tối đa 10 tệp và 100 MB mỗi lần |
| BR-214, BR-215, BR-333 | UC-33a, UC-36, NFR | 10 MB mỗi tệp, kiểm tra loại theo nội dung, tổng 10 GB |
| BR-246, BR-248, BR-250, BR-317 | UC-35 | Không rỗng, ghi tác giả, DOCX không có số trang, trả lời một cấp |
| BR-251 | UC-34 | Chi tiết nơi dùng chỉ hiện họp được truy cập |
| BR-277, BR-279, BR-280, BR-283 | UC-33b, UC-33d, UC-23c | Một tài liệu nhiều mục, cách tính tài liệu chung |
| BR-315, BR-316 | UC-32a, UC-32c | Mở tài liệu trong phòng họp |

### 9. Biểu quyết

**BR giữ lại**

| BR mới | Nội dung (chỉ luật) | Gộp từ BR cũ |
|---|---|---|
| `BR-086` | Chỉ Chair hoặc Secretary tạo, cấu hình, xem và xóa phiếu nháp, khi họp ở Approved, Published hoặc InProgress; phiếu đã mở hoặc đã đóng không xóa được. | BR-189, BR-252, BR-253, BR-274, BR-285 |
| `BR-087` | Phiếu thuộc cuộc họp, không gắn mục chương trình; phiếu nháp chưa mở bị xóa khi họp kết thúc. | BR-189, BR-252, BR-253, BR-274, BR-285 |
| `BR-088` | Phiếu đã mở hoặc đóng không sửa nội dung, lựa chọn, kiểu hay vai trò. | BR-254 |
| `BR-089` | Phiếu có ít nhất hai lựa chọn khác nhau, kiểu Công khai hoặc Bí mật. | BR-256, BR-272 |
| `BR-090` | Chỉ Chair mở phiếu nháp, chỉ khi InProgress; mỗi lúc tối đa một phiếu Open và phiếu phải có cử tri hợp lệ. | BR-160, BR-266, BR-267 |
| `BR-091` | Danh sách cử tri chốt lúc mở theo vai trò đã chọn và quyền truy cập, không theo điểm danh; đổi vai trò sau đó không đổi danh sách. | BR-190, BR-268 |
| `BR-092` | Chỉ bỏ phiếu khi họp InProgress, phiếu Open và cử tri còn quyền truy cập. | BR-165, BR-258, BR-265, BR-269, BR-271 |
| `BR-093` | Mỗi cử tri một lá cho một lựa chọn của phiếu, không sửa hay rút; mất quyền chỉ chặn lá mới. | BR-165, BR-258, BR-265, BR-269, BR-271 |
| `BR-094` | Tài liệu phiếu chỉ gắn khi phiếu Draft và cố định khi mở. | BR-288, BR-289 |
| `BR-095` | Cử tri phải xác nhận đã đọc mọi tài liệu phiếu mới bỏ phiếu được. | BR-288, BR-289 |
| `BR-096` | Phiếu bí mật không nối cử tri với lựa chọn, kể cả xác nhận đã đọc; chỉ công bố tổng hợp. | BR-259, BR-273, BR-293 |
| `BR-097` | Phiếu công khai sau khi đóng hiện lựa chọn của từng người. | BR-259, BR-273, BR-293 |
| `BR-098` | Chỉ Chair đóng và kiểm phiếu, khi họp InProgress hoặc Ended; phần trăm tính trên số lá đã bỏ. | BR-261, BR-270 |
| `BR-099` | Chair ghi kết luận Passed, NotPassed hoặc Deferred cho phiếu đã đóng, ghi rồi không đổi; hệ thống không tự suy ra từ kết quả. | BR-161, BR-262, BR-263 |

**Không phải BR — chuyển đi**

| BR cũ | Chuyển tới | Nội dung bị lấy ra |
|---|---|---|
| BR-255, BR-257 | UC-53 | Nội dung không rỗng; vai trò được bỏ phiếu mặc định |
| BR-260 | UC-52b | Màn tình trạng phiếu đang mở |
| BR-264 | Trùng BR-384 | Biên bản chứa kết quả và kết luận |
| BR-287, BR-305 | UC-76a, UC-76b | Đính kèm hoặc gỡ tài liệu phiếu |
| BR-290, BR-291, BR-292 | UC-74 | Cơ chế xác nhận đã đọc: cuộn tới trang cuối, một người một tài liệu |

### 10. Phòng họp online

**BR giữ lại**

| BR mới | Nội dung (chỉ luật) | Gộp từ BR cũ |
|---|---|---|
| `BR-100` | Chỉ người được phân công tham dự hoặc Guest hợp lệ vào phòng họp; Organizer hay Approver đơn thuần chưa đủ. | BR-323 |
| `BR-101` | Rời phòng hoặc mất kết nối không đổi cuộc họp, danh sách, vai trò, điểm danh hay phiếu; kết nối phòng không phải điểm danh. | BR-324, BR-330 |
| `BR-102` | Chỉ Chair tắt mic người khác; người đó bật lại được. | BR-325 |
| `BR-103` | Tin nhắn trong phòng không được lưu. | BR-327 |
| `BR-104` | Mỗi người tối đa một yêu cầu phát biểu. | BR-337, BR-338, BR-339 |
| `BR-105` | Chỉ Chair sắp xếp, mời, từ chối và đóng lượt phát biểu. | BR-337, BR-338, BR-339 |
| `BR-106` | Lịch sử phát biểu giữ cùng cuộc họp và không phải nhiệm vụ. | BR-337, BR-338, BR-339 |

**Không phải BR — chuyển đi**

| BR cũ | Chuyển tới | Nội dung bị lấy ra |
|---|---|---|
| BR-326, BR-328, BR-329, BR-331, BR-332 | UC-37, UC-39, UC-40 | Một người trình chiếu, một kết nối mỗi tài khoản, đổi vai trò, mic loa camera, cửa sổ nổi |
| BR-336 | NFR | Tối đa 50 kết nối (do LiveKit) |

### 11. Ghi âm, bản ghi, Voice ID

**BR giữ lại**

| BR mới | Nội dung (chỉ luật) | Gộp từ BR cũ |
|---|---|---|
| `BR-107` | Cách ghi âm theo hình thức họp, người dùng không chọn; ghi phòng chỉ khi InProgress, tệp âm thanh chỉ cho họp trực tiếp. | BR-340, BR-342 |
| `BR-108` | Chỉ Secretary ghi phòng, tải tệp, xóa lần ghi hoặc xóa bản ghi. | BR-341 |
| `BR-109` | Trước khi ghi, người tham dự phải được báo; Secretary xác nhận và hệ thống lưu giờ, mục đích. | BR-343, BR-353 |
| `BR-110` | Bản ghi, bản chép và câu ghi lưu 180 ngày rồi tự xóa, tính từ lượt ghi đầu; lượt ghi sau không gia hạn; xóa bản ghi xóa cả âm thanh lẫn bản chép. | BR-347, BR-348 |
| `BR-111` | Chair, Secretary và người tham dự nghe được bản ghi. | BR-350, BR-351, BR-370 |
| `BR-112` | Chỉ Chair hoặc Secretary tải bản ghi hoặc xuất bản chép, không ai làm được với họp Secret; Guest chỉ xem lại câu mic của mình. | BR-350, BR-351, BR-370 |
| `BR-113` | Họp online chỉ ghi khi mic bật; mỗi người rút được câu của mình, Secretary xóa được mọi câu. | BR-352, BR-355 |
| `BR-114` | Chair hoặc Secretary loại câu khỏi bản ghi, có lý do, không xóa; chỉ họ đọc câu bị loại. | BR-357, BR-359 |
| `BR-115` | Chỉ Secretary sửa chữ của bản chép. | BR-357, BR-359 |
| `BR-116` | Voice ID cần đồng ý riêng, không gộp với đồng ý ghi âm; đổi thông báo thì phải đồng ý lại. | BR-361, BR-362 |
| `BR-117` | Rút đồng ý hoặc vô hiệu tài khoản xóa hết mẫu giọng, gợi ý và tên tự điền; tên Secretary đã gán giữ nguyên. | BR-365, BR-372, BR-454 |
| `BR-118` | Mỗi người có hai bộ mẫu giọng, tự ghi tối đa 5 và thu hộ tối đa 5, dưới một đồng ý; chỉ chủ nghe mẫu tự ghi. | BR-363, BR-448, BR-364, BR-449, BR-450, BR-452 |
| `BR-119` | Người có quyền thu mẫu thu, nghe, xóa mẫu thu hộ, chỉ thu của người đã đồng ý, và hỏi người được thu ở mỗi lần thu hộ. | BR-363, BR-448, BR-364, BR-449, BR-450, BR-452 |
| `BR-120` | Gợi ý người nói chỉ so với người đã đồng ý trong danh sách hiện tại, chỉ Secretary thấy, không gợi ý Guest; chỉ gán cho người trong danh sách. | BR-366, BR-368 |
| `BR-121` | Âm thanh và mẫu giọng xử lý trong hệ thống, không gửi dịch vụ ngoài. | BR-369 |
| `BR-122` | Bản ghi gộp chỉ để nghe và tải, không phiên âm. | BR-458 |
| `BR-123` | Máy tự điền tên khi giọng khớp rõ với người đồng ý trong danh sách hiện tại. | BR-366 |
| `BR-124` | Chỉ người đã đồng ý thông báo từ bản 1.2 trở lên được máy tự điền tên, người khác chỉ được gợi ý; thu hộ cần bản 1.4. | BR-362, BR-449 |
| `BR-125` | Khi chuyển chữ lại, tên gán tay, chữ sửa và câu chuyển người được mang sang câu hoặc giọng khớp; phần không khớp chỉ còn dấu vết trong nhật ký, không ghi chữ. | BR-467 |

**Không phải BR — chuyển đi**

| BR cũ | Chuyển tới | Nội dung bị lấy ra |
|---|---|---|
| BR-344, BR-345, BR-349 | UC-44a, UC-44b, UC-44d | Một lần ghi mở, nối lần ghi, phiên âm lại, xóa một lần ghi |
| BR-346, BR-456, BR-457 | UC-44b, UC-43a | 12 giờ không âm thì lưu; cách dựng bản ghi gộp |
| BR-354 | UC-80 | Câu chưa gửi giữ ở thiết bị tối đa 7 ngày sau họp |
| BR-356, BR-358, BR-360, BR-395 | UC-45, UC-46 | Đánh dấu câu trùng, xuất bản chép, phiên âm lại, đổi người nói |
| BR-367, BR-463, BR-464, BR-465, BR-466 | NFR nhận dạng giọng | Ngưỡng 0.6/0.7/0.8, lọc câu ngắn, từ đệm, độ tin cậy 40%, chữ số ITN |
| BR-451, BR-455 | UC-86, UC-44c | Màn thu mẫu, thông báo thu, nhắc micro phòng họp |

### 12. AI dự thảo, biên bản, ghi chú

**BR giữ lại**

| BR mới | Nội dung (chỉ luật) | Gộp từ BR cũ |
|---|---|---|
| `BR-126` | Chỉ Chair hoặc Secretary yêu cầu, sửa và rà dự thảo AI, chỉ khi họp đã Ended và có ít nhất một câu ghi. | BR-374, BR-375, BR-462 |
| `BR-127` | Người khác chỉ đọc dự thảo AI sau khi đã rà. | BR-374, BR-375, BR-462 |
| `BR-128` | AI chỉ viết từ hồ sơ cuộc họp; không ghi điểm danh, không đoán tên, không viết kết quả biểu quyết. | BR-378, BR-379 |
| `BR-129` | Mặc định AI chạy trên máy của nhóm; Admin có thể chọn API ngoài cho họp Normal; khoá truy cập lưu mã hoá. | BR-381 |
| `BR-130` | Biên bản theo một mẫu cố định (Mẫu 1.4, NĐ 30/2020/NĐ-CP), không quản lý mẫu. | BR-383 |
| `BR-131` | Chỉ Chair hoặc Secretary đọc, sửa bản nháp biên bản, chỉ sau khi họp Ended; người khác chỉ thấy bản đã ban hành. | BR-386, BR-387 |
| `BR-132` | Biên bản họp Secret không tải về Word hay PDF. | BR-390 |
| `BR-133` | Hệ thống không theo dõi người nhận, hạn hay tiến độ nhiệm vụ; phần Nhiệm vụ chỉ là chữ. | BR-392, BR-459 |
| `BR-134` | Chỉ Secretary viết ghi chú họp, viết khi InProgress đến khi dựng bản nháp biên bản; Chair đọc được. | BR-397, BR-398 |
| `BR-135` | Chỉ Secretary gửi biên bản duyệt, chỉ sau khi họp Ended. | BR-404, BR-405, BR-406, BR-407 |
| `BR-136` | Chỉ Chair duyệt hoặc trả lại biên bản; trả lại phải có lý do. | BR-404, BR-405, BR-406, BR-407 |
| `BR-137` | Duyệt là ban hành ngay; cuộc họp vẫn Ended. | BR-408, BR-409, BR-410, BR-413 |
| `BR-138` | Bản ban hành không đổi; sửa tạo bản hiệu chỉnh xuất phát từ bản ban hành, tối đa một bản chưa ban hành. | BR-408, BR-409, BR-410, BR-413 |
| `BR-139` | Bản ban hành và bản bị thay đọc, tải được bởi mọi người đọc được cuộc họp, trừ Guest; bản nháp chỉ Chair và Secretary. | BR-412 |

**Không phải BR — chuyển đi**

| BR cũ | Chuyển tới | Nội dung bị lấy ra |
|---|---|---|
| BR-373, BR-376, BR-377, BR-393, BR-396 | UC-47–UC-50 | Ba phần dự thảo, thay dự thảo, giới hạn ký tự, khóa khi đang viết lại |
| BR-380, BR-382 | UC-47, UC-59 | Cách viết phần II theo mốc mục; đánh dấu do AI |
| BR-384 | UC-59 | Nội dung điền từ hồ sơ họp |
| BR-385, BR-391, BR-394 | UC-60, NFR | HTML lọc, Word và PDF, 300.000 ký tự, 64 cấp thẻ, 63 cột |
| BR-388, BR-389 | UC-60 | Một bản nháp, từ chối lưu từ bản cũ |
| BR-399, BR-400, BR-401, BR-402, BR-403, BR-461 | UC-82 | Ghi chú theo mục, một ghi chú mỗi tác giả, từ chối bản cũ, 20.000 ký tự, lưu ở thiết bị |

### 13. Chia sẻ lịch, tìm lịch trống

**BR giữ lại**

| BR mới | Nội dung (chỉ luật) | Gộp từ BR cũ |
|---|---|---|
| `BR-140` | Chia sẻ lịch có hai mức: mức 1 cho họp Normal, mức 2 cho Normal và Internal; họp Secret không bao giờ chia sẻ. | BR-414, BR-427, BR-430 |
| `BR-141` | Người xem chỉ thấy bận, rảnh, chưa biết hoặc chưa chia sẻ; không thấy tiêu đề, địa điểm, người dự hay độ mật; chỉ Organizer dùng chia sẻ lịch. | BR-414, BR-427, BR-430 |
| `BR-142` | Mỗi cặp chủ lịch và người xem tối đa một chia sẻ Pending hoặc Effective. | BR-423, BR-424, BR-425 |
| `BR-143` | Chia sẻ dừng khi người xem hết là Organizer hoặc tài khoản bị vô hiệu, và hết hiệu lực khi qua ngày kết thúc. | BR-423, BR-424, BR-425 |
| `BR-144` | Ngày ngoài khoảng chia sẻ là chưa biết; người chưa chia sẻ không tính rảnh hay bận. | BR-415, BR-416, BR-417, BR-418 |
| `BR-145` | Họp vượt mức chia sẻ tính là rảnh; họp của chính người tìm luôn tính. | BR-415, BR-416, BR-417, BR-418 |
| `BR-146` | Chỉ phân công Chair, Secretary hoặc Member của họp Chờ duyệt, Đã duyệt, Đã công bố, Đang họp, Đã kết thúc tính bận. | BR-419, BR-420 |
| `BR-147` | Người từ chối và họp Nháp, Bị từ chối, Đã hủy không tính bận. | BR-419, BR-420 |
| `BR-148` | Khung chạm họp Secret vẫn tạo được bản nháp, không cảnh báo riêng. | BR-428 |

**Không phải BR — chuyển đi**

| BR cũ | Chuyển tới | Nội dung bị lấy ra |
|---|---|---|
| BR-421, BR-422, BR-431 | UC-85a, UC-84a | Tối đa 10 người, thời lượng, giờ hành chính, khoảng đệm, khoảng ngày 365 |
| BR-429 | UC-85b | Họp tạo từ khung: ai là Chair, Secretary |

### 14. Báo cáo

**BR giữ lại**

| BR mới | Nội dung (chỉ luật) | Gộp từ BR cũ |
|---|---|---|
| `BR-149` | Người dùng nội bộ xem và xuất báo cáo; Guest không. | BR-432, BR-434 |
| `BR-150` | Báo cáo chỉ tính họp người đó đọc được, theo tháng bắt đầu dự kiến, trạng thái và độ mật hiện tại. | BR-432, BR-434 |
| `BR-151` | Tỷ lệ tham dự, thời gian ra biên bản và thời lượng họp tính theo công thức; họp thiếu số liệu bị bỏ khỏi trung bình. | BR-435, BR-436, BR-447 |
| `BR-152` | Số liệu đầu trang chỉ hiện số; bảng truy vấn hiện tên người hoặc mã và tiêu đề cuộc họp, chỉ cho họp người xem được đọc; Guest chỉ có tên. | BR-437 |
| `BR-153` | Báo cáo truy vấn chỉ tính bản ghi người xem được đọc: phiếu nháp và biên bản chưa ban hành chỉ Chair và Secretary, bản ghi chỉ người tham dự, bản ghi gộp không tính vào giờ. | BR-468 |

**Không phải BR — chuyển đi**

| BR cũ | Chuyển tới | Nội dung bị lấy ra |
|---|---|---|
| BR-433 | UC-70a | Kỳ báo cáo là số tháng nguyên, tối đa 24 tháng |

## 5. Ánh xạ nhanh: BR mới sang BR cũ

| BR mới | BR cũ |
|---|---|
| `BR-001` | BR-03, BR-18, BR-19 |
| `BR-002` | BR-04 |
| `BR-003` | BR-05, BR-06, BR-07 |
| `BR-004` | BR-02 |
| `BR-005` | BR-13 |
| `BR-006` | BR-15 |
| `BR-007` | BR-23 |
| `BR-008` | BR-313, BR-314 |
| `BR-009` | BR-16, BR-197, BR-318, BR-335, BR-426, BR-438, BR-444, BR-446, BR-453 |
| `BR-010` | BR-16, BR-197, BR-318, BR-335, BR-426, BR-438, BR-444, BR-446, BR-453 |
| `BR-011` | BR-16, BR-197, BR-318, BR-335, BR-426, BR-438, BR-444, BR-446, BR-453 |
| `BR-012` | BR-440 |
| `BR-013` | BR-24, BR-26, BR-62, BR-162 |
| `BR-014` | BR-27, BR-28, BR-158 |
| `BR-015` | BR-75 |
| `BR-016` | BR-29, BR-117, BR-118, BR-119, BR-121, BR-176 |
| `BR-017` | BR-29, BR-117, BR-118, BR-119, BR-121, BR-176 |
| `BR-018` | BR-34 |
| `BR-019` | BR-35 |
| `BR-020` | BR-36 |
| `BR-021` | BR-37, BR-41, BR-124, BR-125 |
| `BR-022` | BR-38, BR-445 |
| `BR-023` | BR-38, BR-445 |
| `BR-024` | BR-127, BR-129, BR-130, BR-131 |
| `BR-025` | BR-132, BR-143 |
| `BR-026` | BR-159 |
| `BR-027` | BR-460 |
| `BR-028` | BR-297, BR-299 |
| `BR-029` | BR-301 |
| `BR-030` | BR-119, BR-118 |
| `BR-031` | BR-39, BR-90, BR-93 |
| `BR-032` | BR-39, BR-90, BR-93 |
| `BR-033` | BR-47, BR-52 |
| `BR-034` | BR-47, BR-52 |
| `BR-035` | BR-322 |
| `BR-036` | BR-79, BR-80 |
| `BR-037` | BR-79, BR-80 |
| `BR-038` | BR-81, BR-82 |
| `BR-039` | BR-85, BR-86, BR-173 |
| `BR-040` | BR-122, BR-123 |
| `BR-041` | BR-126, BR-169, BR-294, BR-295, BR-296 |
| `BR-042` | BR-126, BR-169, BR-294, BR-295, BR-296 |
| `BR-043` | BR-96, BR-116 |
| `BR-044` | BR-97, BR-100, BR-157 |
| `BR-045` | BR-102, BR-103 |
| `BR-046` | BR-104, BR-105, BR-106 |
| `BR-047` | BR-235, BR-321 |
| `BR-048` | BR-235, BR-321 |
| `BR-049` | BR-84, BR-87, BR-149, BR-150, BR-151, BR-201 |
| `BR-050` | BR-84, BR-87, BR-149, BR-150, BR-151, BR-201 |
| `BR-051` | BR-84, BR-87, BR-149, BR-150, BR-151, BR-201 |
| `BR-052` | BR-120, BR-152, BR-154, BR-156 |
| `BR-053` | BR-153, BR-155 |
| `BR-054` | BR-153, BR-155 |
| `BR-055` | BR-181, BR-203, BR-204, BR-205 |
| `BR-056` | BR-170, BR-171, BR-178 |
| `BR-057` | BR-170, BR-171, BR-178 |
| `BR-058` | BR-64, BR-65, BR-74 |
| `BR-059` | BR-64, BR-65, BR-74 |
| `BR-060` | BR-69, BR-70, BR-71, BR-72 |
| `BR-061` | BR-69, BR-70, BR-71, BR-72 |
| `BR-062` | BR-69, BR-70, BR-71, BR-72 |
| `BR-063` | BR-237 |
| `BR-064` | BR-306, BR-308 |
| `BR-065` | BR-306, BR-308 |
| `BR-066` | BR-309, BR-310 |
| `BR-067` | BR-309, BR-310 |
| `BR-068` | BR-183, BR-206, BR-208, BR-216, BR-229 |
| `BR-069` | BR-187 |
| `BR-070` | BR-184, BR-185, BR-195 |
| `BR-071` | BR-184, BR-185, BR-195 |
| `BR-072` | BR-193, BR-209, BR-210 |
| `BR-073` | BR-193, BR-209, BR-210 |
| `BR-074` | BR-186, BR-211, BR-212, BR-231, BR-281, BR-284 |
| `BR-075` | BR-186, BR-211, BR-212, BR-231, BR-281, BR-284 |
| `BR-076` | BR-186, BR-211, BR-212, BR-231, BR-281, BR-284 |
| `BR-077` | BR-213, BR-232 |
| `BR-078` | BR-278 |
| `BR-079` | BR-275, BR-282 |
| `BR-080` | BR-275, BR-282 |
| `BR-081` | BR-188 |
| `BR-082` | BR-249 |
| `BR-083` | BR-219, BR-242 |
| `BR-084` | BR-222, BR-223 |
| `BR-085` | BR-334 |
| `BR-086` | BR-189, BR-252, BR-253, BR-274, BR-285 |
| `BR-087` | BR-189, BR-252, BR-253, BR-274, BR-285 |
| `BR-088` | BR-254 |
| `BR-089` | BR-256, BR-272 |
| `BR-090` | BR-160, BR-266, BR-267 |
| `BR-091` | BR-190, BR-268 |
| `BR-092` | BR-165, BR-258, BR-265, BR-269, BR-271 |
| `BR-093` | BR-165, BR-258, BR-265, BR-269, BR-271 |
| `BR-094` | BR-288, BR-289 |
| `BR-095` | BR-288, BR-289 |
| `BR-096` | BR-259, BR-273, BR-293 |
| `BR-097` | BR-259, BR-273, BR-293 |
| `BR-098` | BR-261, BR-270 |
| `BR-099` | BR-161, BR-262, BR-263 |
| `BR-100` | BR-323 |
| `BR-101` | BR-324, BR-330 |
| `BR-102` | BR-325 |
| `BR-103` | BR-327 |
| `BR-104` | BR-337, BR-338, BR-339 |
| `BR-105` | BR-337, BR-338, BR-339 |
| `BR-106` | BR-337, BR-338, BR-339 |
| `BR-107` | BR-340, BR-342 |
| `BR-108` | BR-341 |
| `BR-109` | BR-343, BR-353 |
| `BR-110` | BR-347, BR-348 |
| `BR-111` | BR-350, BR-351, BR-370 |
| `BR-112` | BR-350, BR-351, BR-370 |
| `BR-113` | BR-352, BR-355 |
| `BR-114` | BR-357, BR-359 |
| `BR-115` | BR-357, BR-359 |
| `BR-116` | BR-361, BR-362 |
| `BR-117` | BR-365, BR-372, BR-454 |
| `BR-118` | BR-363, BR-448, BR-364, BR-449, BR-450, BR-452 |
| `BR-119` | BR-363, BR-448, BR-364, BR-449, BR-450, BR-452 |
| `BR-120` | BR-366, BR-368 |
| `BR-121` | BR-369 |
| `BR-122` | BR-458 |
| `BR-123` | BR-366 |
| `BR-124` | BR-362, BR-449 |
| `BR-125` | BR-467 |
| `BR-126` | BR-374, BR-375, BR-462 |
| `BR-127` | BR-374, BR-375, BR-462 |
| `BR-128` | BR-378, BR-379 |
| `BR-129` | BR-381 |
| `BR-130` | BR-383 |
| `BR-131` | BR-386, BR-387 |
| `BR-132` | BR-390 |
| `BR-133` | BR-392, BR-459 |
| `BR-134` | BR-397, BR-398 |
| `BR-135` | BR-404, BR-405, BR-406, BR-407 |
| `BR-136` | BR-404, BR-405, BR-406, BR-407 |
| `BR-137` | BR-408, BR-409, BR-410, BR-413 |
| `BR-138` | BR-408, BR-409, BR-410, BR-413 |
| `BR-139` | BR-412 |
| `BR-140` | BR-414, BR-427, BR-430 |
| `BR-141` | BR-414, BR-427, BR-430 |
| `BR-142` | BR-423, BR-424, BR-425 |
| `BR-143` | BR-423, BR-424, BR-425 |
| `BR-144` | BR-415, BR-416, BR-417, BR-418 |
| `BR-145` | BR-415, BR-416, BR-417, BR-418 |
| `BR-146` | BR-419, BR-420 |
| `BR-147` | BR-419, BR-420 |
| `BR-148` | BR-428 |
| `BR-149` | BR-432, BR-434 |
| `BR-150` | BR-432, BR-434 |
| `BR-151` | BR-435, BR-436, BR-447 |
| `BR-152` | BR-437 |
| `BR-153` | BR-468 |

Mọi BR cũ không có trong bảng trên: nằm ở bảng "Không phải BR — chuyển đi" của nhóm tương ứng (chuyển UC/NFR) hoặc đã ngừng từ trước (8 BR còn dòng đánh dấu ngừng: 78, 166, 174, 175, 177, 199, 200, 302; các số khác trong khoảng 1–468 không còn dòng).

## 6. Tiêu chí review (cho leader)

Xin leader chấm từng tiêu chí, ghi nhận xét theo nhóm BR để chúng tôi cải tiến:

1. **Đúng là BR?** Mỗi BR giữ lại là một quyết định nghiệp vụ (quyền, trạng thái, ràng buộc), không phải chi tiết giao diện, giới hạn kỹ thuật hay nội dung thông báo.
2. **Không mất nghiệp vụ.** Không BR cũ nào bị bỏ mà thực ra là luật nghiệp vụ. Soát mục "chuyển đi" của từng nhóm: có mục nào nên giữ làm BR không?
3. **Chuyển đúng chỗ.** Mục "chuyển đi" đã trỏ đúng UC hoặc NFR chưa?
4. **Một BR một ý.** Mỗi BR một câu, đọc một lần hiểu, kiểm chứng được. BR nào vẫn ôm nhiều ý cần tách.
5. **Gộp hợp lý.** BR gộp từ nhiều BR cũ có thật cùng một quyết định không, hay đang nhét nhiều luật khác nhau vào một.
6. **Số lượng.** 153 so với vùng 90–150; có nhóm nào quá dày hoặc quá mỏng?
7. **Cách xử lý tham chiếu.** Chấp nhận đổi toàn bộ ID (đánh số lại), giữ ADR, notes, test-reports nguyên trạng và tra bằng bảng ánh xạ.
8. **Bỏ ba cột phụ** (Applies To, Rationale, Enforced By), nguồn gốc giữ ở ADR và Jira.

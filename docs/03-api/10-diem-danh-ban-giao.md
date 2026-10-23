# Bàn giao SCRUM-26 — FE, BA và Tester

Ngày 20/10/2026. [API](10-diem-danh.md), [kết quả kiểm thử](10-diem-danh-ket-qua-kiem-thu.md).

## FE

1. Tạo types/API wrapper theo contract, dùng client chuẩn với relative URL. Backend task không thêm màn hình hoặc component FE.
2. Dùng chung AttendancePanel page/room, hỗ trợ 360px; đọc `canRecordAttendance` cho UI. Backend mới là ranh giới quyền.
3. Hiển thị Unknown là “Chưa điểm danh”, khác Absent. legacyAttendance hiển thị dữ liệu lịch sử riêng, không tính vào unmarked.
4. Chọn Present/Absent, chỉ gửi các entry cần lưu. Dùng assignment/invitation ID và kind, không dùng UserId.
5. Giữ rowVersion gốc khi bắt đầu sửa, gửi expectedVersion. Không dùng ETag roster; không tự sửa token. Khi nhận event và GET mới, không gắn token mới vào draft cũ.
6. 409/MSG151: GET lại, giữ draft để đối chiếu, để người dùng quyết định lưu lại. Không auto retry. Đổi response/role không tự stale token, nhưng quyền/state/membership có thể thay đổi.
7. Sau 200, nhận token/metadata từ entries response. No-op trả 200/MSG147, changedCount=0. 409/MSG149 khóa thao tác và đọc lại trạng thái.
8. Timeout/mất response hoặc 503/MSG150: GET trước khi thử lại; không hứa rằng lần lưu trước thất bại.
9. Join hub rồi GET; event attendanceChanged → GET. Reconnect phải join lại rồi GET. GET khi focus và poll mỗi 15 giây khi panel hiển thị, không chạy request chồng nhau. Leave khi đóng panel.
10. Không suy ra Present khi vào phòng hoặc Accepted lời mời. Không chặn End Meeting vì chưa điểm danh đủ.

## Dependency và ranh giới bàn giao

- Đã tích hợp SCRUM-20 hiện tại từ main: bỏ grants, dùng MeetingGuestSession chung và quyền đọc roster theo MeetingReadPolicy. Attendance vẫn chỉ cho Secretary ghi. MSG277 giữ nguyên nghĩa validation tạo meeting trên main; lỗi đọc attendance chuyển sang MSG289.
- SCRUM-59/UC-04 cung cấp Guest credentials/profile/session thật và hai adapter participant + agenda/hub dùng cùng identity. Mặc định production deny; test dùng fake adapter. Không tuyên bố Guest end-to-end đã hoàn thành.
- SCRUM-41 phụ trách room; FE tích hợp AttendancePanel. Start/End endpoint không được thêm trong task này; test dùng fixture trạng thái.
- Chưa commit/push/deploy hoặc áp dụng migration vào DB dùng chung. Người review cần phân biệt migration dependency và migration điểm danh.

## Nguồn đã đối chiếu và khác biệt

| Nguồn | Phạm vi đối chiếu |
|---|---|
| AGENTS.md, docs/README.md, DEVELOPMENT_WORKFLOW | Ngôn ngữ, code-first, phân quyền, BE → FE → BA → Tester |
| Jira SCRUM-26 live | Secretary only, InProgress, partial batch atomic, internal + Guest |
| requirements README, master inventory, UC-27–31, business rules | Quyền roster/response, current history, BR-116/170–172/178 |
| Sequence README, meeting-participant-management README, UC31_RecordAttendance.txt | Main/exception flows, revalidation, audit, atomic save |
| Report README/source README, Report 3 DOCX UC-31 và NFR | Tài liệu nguồn, numbering EX khác bản Markdown, mục tiêu API p95 |
| design README, ERD, database guide | Entity có sẵn, dependency migration, hai cột version |
| ADR-0013/0016/0017/0021 | Một meeting một buổi, Guest, End độc lập, current assignment |
| API README, auth/access, catalogue, Agenda/Guest, Participants, Postman README | Auth, message codes, hub/group, dependency contract |
| Class diagram/state-machine README và phần attendance liên quan | Mô hình minh họa có phần legacy chưa đồng bộ |

Các tài liệu UC-31 và sequence bắt buộc đều có. Không viết lại báo cáo hay ảnh sơ đồ trong task này. ERD Markdown phân biệt mô hình mục tiêu và schema hiện thực; thêm ghi chú kỹ thuật SCRUM-26, không tái tạo .drawio.

## Ghi chú Jira — đã đăng

Đã đăng ghi chú được người phụ trách xác nhận vào [SCRUM-26, comment 10239](https://sep490g116.atlassian.net/browse/SCRUM-26?focusedCommentId=10239), ghi nhận token từng entry, 409/MSG151, MSG150 khi kết quả commit chưa xác định và yêu cầu BA điều chỉnh câu “No changes were saved” cho trường hợp này. Comment cũng ghi nhận review độc lập 1.152 test pass, hai cột version và các dependency Guest/UI/room. Finding P3 về thiếu ghi nhận trên Jira đã được xử lý.

Tóm tắt kỹ thuật bàn giao:

Backend UC-31 bổ sung GET attendance, POST atomic theo entry; AttendanceVersion riêng trên internal assignment và Guest invitation để chống ghi đè. Response/role không đổi version. Ghi audit cùng transaction và attendanceChanged sau commit, chỉ gửi roster readers. Bổ sung query/read codes MSG289/278, hiện thực MSG147–151. MSG151 bao gồm stale điểm danh; MSG150 dùng câu trung tính khi commit chưa xác định. Legacy Unknown/Excused/Delegated được giữ, chỉ cho ghi Present/Absent. Guest production integration chờ UC-04; FE/room do task khác. Migration mới chỉ thêm hai cột; dependency SCRUM-20 giữ migration riêng.

BA cần đối chiếu: comment Jira 30/09 về self-attendance đã bị mô tả hiện tại thay thế; README sequence cũ còn nói Jira chưa cập nhật; EX numbering DOCX/Markdown khác nhau; state machine legacy còn Excused/Delegated; DCD cần thể hiện Guest recorder/version kỹ thuật nếu được đưa vào class diagram. Không coi các phần cũ đó là yêu cầu mở lại self-attendance hoặc quorum.

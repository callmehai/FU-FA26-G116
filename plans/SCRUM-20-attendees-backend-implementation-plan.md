# Kế hoạch Backend — SCRUM-20: Mời thành phần tham dự và xác nhận tham dự

Ngày: **2026-09-23**. Baseline khảo sát: **6aaeed1**. Trạng thái: **kế hoạch local; chưa triển khai, chưa commit**.

## 1. Mục tiêu và cách sử dụng

Thư ký chọn từng cá nhân, gán vai trò trong cuộc họp, xem trạng thái xác nhận; người được mời tự trả lời tham dự/không tham dự/uỷ quyền; người được thêm sau công bố vẫn được gửi thư mời.

Phạm vi Backend: policy, dữ liệu, API, transaction/concurrency, tích hợp hàng đợi email, audit, test, API docs và TS type/wrapper. FE thực hiện tab Thành phần tham dự. Không triển khai trong lượt lập kế hoạch này.

Không thêm đơn vị/phòng ban, không mời cả đơn vị, không tự tạo tài khoản từ email nhập tay. `MeetingRole.Guest` là người có tài khoản được mời với vai trò khách. Khách ngoài vào bằng link của ADR-0005 là luồng khác: không sinh meeting_participants, không RSVP ở endpoint này và không được đọc danh sách.

Kế hoạch là baseline cụ thể để review, không thay Jira/UC Specification. Mỗi bước ở mục 12 chỉ được triển khai khi đầu vào đủ. Các lựa chọn còn chờ trong mục 3 phải được cập nhật trước bước phụ thuộc; model không được tự đổi chúng thành yêu cầu chính thức.

## 2. Tài liệu và code đã đối chiếu

Đối chiếu toàn bộ bộ tài liệu đã đọc trong chuỗi lập kế hoạch; so sánh các thay đổi từ baseline trước và đọc phần mới. Danh mục bao gồm AGENTS/CLAUDE/README, DEVELOPMENT_WORKFLOW, các nhóm docs 00–05, sequence/class diagrams và ghi chú trong 07-classdiagram. Đã xem lại hai sequence diagram phân quyền mới thay đổi. Chưa có UC spec/diagram riêng cho SCRUM-20 trong repo.

Điểm vào khi đọc lại: `docs/README.md`, `docs/DEVELOPMENT_WORKFLOW.md`, `docs/02-design/01-vong-doi-cuoc-hop.md`, `docs/02-design/02-erd.md`, `docs/02-design/03-co-so-du-lieu.md`, `docs/03-api/README.md`, `docs/03-api/04-phan-quyen-phia-server.md`, `docs/03-api/04-quan-ly-vai-tro.md`, `docs/05-decisions/README.md`. Đọc các file trong từng nhóm cùng README/AGENTS, không chỉ grep phần có từ attendees. Khi BA thêm spec/diagram, cập nhật inventory và decision ledger trước code.

| Nguồn | Điều cần tuân thủ |
|---|---|
| AGENTS.md, DEVELOPMENT_WORKFLOW | BA → BE → FE → Tester; chỉ Backend xong chưa phải UC Done; server authorization; code-first; docs đồng bộ code |
| ADR-0004 | Một tổ chức; mời từng tài khoản; không org unit/multi-tenant |
| ADR-0005 | Phân biệt Guest có tài khoản và khách ngoài; quyền mời gắn với Secretary/Chair của chính cuộc họp |
| ADR-0006 | Participant đã từ chối vẫn là thành viên và có quyền tài liệu theo chính sách; không xoá membership vì Declined |
| Vòng đời cuộc họp | Draft/Rejected soạn nội dung; Published mới mời; InProgress là đã bắt đầu; hồ sơ đóng không sửa danh sách tùy ý |
| ERD | meeting_participants unique MeetingId/UserId; Response khác Attendance; EmailMessage là hàng đợi bền vững |
| API 04-phan-quyen-phia-server | Cổng quyền hai lớp; đọc lại account/capability; tài nguyên không tồn tại/không được đọc cùng 404 không body |
| ADR-0007 | Ma trận capability đang chạy là nguồn sự thật; không reset bằng seed/HasData |
| ADR-0008, 04-quan-ly-vai-tro | Chuyển SystemRole sang dữ liệu là đề xuất SCRUM-63 chưa triển khai; MeetingRole vẫn là vai trò trong từng cuộc họp |
| ADR-0009 và API auth cập nhật | Có tự liên kết Google lần đầu trong code; không xây lại auth ở task này |
| API notes, class diagram README | Một số ghi chú cũ đã bị rút lại; ưu tiên trạng thái thực tế và ghi rõ lệch tài liệu, không sửa theo nhận định cũ |

Hiện trạng quan trọng:

- Có MeetingParticipant với Role, Response, DelegatedToUserId, RespondedAt và các field Attendance. Chưa có attendee endpoint/service.
- Có EmailMessage với Queued/Sent/Failed, số lần gửi, lỗi, attachment lịch tùy chọn; chưa có sender/service invitation đã đăng ký.
- Có CapabilityGate và AddCapabilityPolicies; không còn cần viết lại gate quản trị từ đầu.
- FE api client đã gắn token, refresh và đọc error body. Không lặp lại nhận định cũ rằng FE chưa có auth thật.
- ADR-0008/0009 vẫn mang nhãn Đề xuất; riêng code Google đã thay đổi. Không tự đổi trạng thái ADR trong task này.
- Capability hiện có chỉ gồm MeetingCreator, MeetingApprover, MinutesEditor, AdminUsers. Member mặc định không có capability, nhưng phải được dự họp và trả lời thư mời.
- AuditService thực tế dùng cùng DbContext scoped và gọi SaveChanges. Bao transaction ngoài để mutation + audit + enqueue cùng rollback/commit; không suy luận từ comment unit of work riêng.
- Các kế hoạch approval/agenda/ASR trước chưa phải implementation đã merge. Chỉ reuse service khi kiểm tra thấy thật trong code.

## 3. Quyết định, giả định và điểm phải chốt

| ID | Quyết định / baseline | Trạng thái |
|---|---|---|
| D01 | Chỉ mời cá nhân có tài khoản; bốn MeetingRole Chair/Secretary/Member/Guest | Task + ADR, đã có căn cứ |
| D02 | Uỷ quyền tự thêm người nhận đang hoạt động, gửi thư; người nhận tự xác nhận; người mới có Role=Member, không kế thừa Chair/Secretary | **Người dùng đã xác nhận** |
| D03 | Thêm/gỡ trước khi bắt đầu; sau công bố cho thêm/gỡ ở Published, khóa từ InProgress | Đã hỏi; baseline đề xuất, chưa nhận xác nhận |
| D04 | Người quản lý xem bảng đầy đủ; người dự thường chỉ đọc phản hồi của mình | Đã hỏi; baseline đề xuất, chưa nhận xác nhận |
| D05 | Cổng quyền cho Member RSVP và Secretary/Chair quản lý theo cuộc họp đang mâu thuẫn quy ước RequireCapability | **Người dùng chọn giữ chờ owner phân quyền/BA chốt**; chưa duyệt phương án bổ sung gate |
| D06 | Người tạo có MeetingCreator quản lý khởi tạo; Secretary/Chair quản lý theo MeetingRole; không ngoại lệ Admin toàn cục | Đề xuất để giải quyết cuộc họp chưa có thư ký; BA xác nhận |
| D07 | RSVP được đổi trong Published; mọi thay đổi khóa khi trạng thái đã InProgress; không lấy StartsAt làm hạn ngầm | Baseline đề xuất |
| D08 | Uỷ quyền một cấp; một người nhận tối đa một uỷ quyền đang hiệu lực; không chuyển quyền quản lý/biểu quyết/quorum | Baseline v1 đề xuất, cần BA xác nhận |
| D09 | Gỡ người nhận đang được uỷ quyền phải trả conflict; không tự sửa câu trả lời của người khác | Baseline đề xuất |
| D10 | Bổ sung PUT đổi vai trò, giữ nguyên RSVP thay vì xoá-thêm lại | Đề xuất API cần ghi vào UC |
| D11 | Email dùng contract thật từ SCRUM-15; công bố và thêm muộn dùng một invitation writer | Kế thừa hướng đã chọn ở kế hoạch công bố; chưa có contract/implementation trong repo |

D03/D04 chưa được trả lời không có nghĩa là đã được đồng ý. D05 đã được trả lời là giữ chờ owner, không phải chấp thuận phương án kỹ thuật. Phần dưới mô tả một baseline để review và chỉnh sửa; chưa phải tuyên bố tất cả luật nghiệp vụ đã chốt.

### Mâu thuẫn phân quyền cần giải quyết trước P04

Tài liệu 04-phan-quyen-phia-server hiện viết mọi endpoint dùng RequireCapability. Nhưng ADR-0005 cấp quyền theo MeetingRole và tài khoản Member không có capability vẫn phải RSVP. Không được chọn bừa MeetingCreator/MinutesEditor cho mọi attendee endpoint vì sẽ chặn người dự hợp lệ.

Phương án để owner cân nhắc, **chưa được duyệt**: tách phần đọc lại account/caller dùng chung từ CapabilityGate; giữ RequireCapability nguyên nghĩa cho endpoint cần quyền toàn hệ thống; thêm gate cho caller active và resource policy theo meeting. Nhánh creator vẫn kiểm MeetingCreator ở token + DB; nhánh Secretary/Chair kiểm membership ở DB. RSVP kiểm chính membership của caller, không cần capability quản trị. Nếu được chọn, update tài liệu 04 để phân biệt hai loại gate; không tự bỏ lớp đọc lại DB hoặc làm endpoint public.

Không sửa DEVELOPMENT_WORKFLOW, không cấp capability mới cho mọi role chỉ để tránh giải quyết mâu thuẫn này. Nếu owner không chấp nhận hướng đề xuất cho D05 thì sửa thiết kế trước khi làm endpoint; không có fallback âm thầm.

## 4. Ma trận quyền và trạng thái theo baseline

### 4.1. Actor

- Manager: creator có MeetingCreator hiện tại, hoặc participant có MeetingRole.Secretary/Chair; caller luôn phải active và được đọc meeting theo shared meeting policy.
- Participant: có dòng trong cuộc họp và account active; chỉ trả lời cho chính userId lấy từ JWT sub.
- Approver toàn hệ thống chưa có quan hệ quản lý cuộc họp: không tự được sửa attendee chỉ vì có MeetingApprover. Quyền đọc phục vụ duyệt dùng shared meeting policy; không tự cấp quyền xem bảng phản hồi ở task này.
- Admin không có quan hệ với meeting: không tự vượt policy.
- External guest link: không truy cập các endpoint attendee.

Policy đọc meeting là dependency của SCRUM-17/19. Cần điều chỉnh cùng owner để Secretary/Chair đọc được phần danh sách họ phải quản lý trước công bố; không tự mở toàn meeting cho mọi người được thêm vào Draft. Các ngoại lệ creator/Secretary trong kế hoạch trước phải được ghi thống nhất, không triển khai ba bản policy khác nhau.

### 4.2. Trạng thái

| Trạng thái | Manager thêm/gỡ/đổi role | Người dự tự RSVP | Email invitation |
|---|---|---|---|
| Draft, Rejected | Có | Chưa | Không enqueue lời mời |
| WaitingApproval, Approved | Không | Chưa | Không enqueue lời mời |
| Published | Có, theo D03 | Có, theo D07 | Thêm người mới enqueue ngay trong transaction |
| InProgress | Không | Không | Sender có thể gửi nốt lời mời đã queue khi Published nếu membership còn hiệu lực |
| Ended, MinutesIssued, Archived | Không | Không | Lời mời chưa gửi phải được suppress theo contract sender |

Đọc bảng theo D04 và shared visibility policy; tài khoản bị vô hiệu hoá vẫn có thể xuất hiện trong bảng quản lý với isActive=false nhưng không tự đăng nhập/RSVP. Không âm thầm xoá họ khỏi thống kê.

Không thêm luật số lượng chính xác một Chair/một Secretary vì task/docs chưa yêu cầu. Một người chỉ có một MeetingRole trong một cuộc họp do unique membership; đổi role không đổi SystemRole, không sửa bảng quyền. Nếu BA muốn tối thiểu Chair/Secretary khi công bố, đó là điều kiện cần bổ sung vào cả lifecycle, không chỉ kiểm trong POST attendees.

## 5. Luật membership và phản hồi

### 5.1. Thêm trực tiếp

- Chọn UserId đang hoạt động từ endpoint tìm cá nhân; không nhận email để tạo tài khoản ngầm.
- Role bắt buộc; chỉ chấp nhận bốn tên enum chính xác, không nhận số enum hoặc role hệ thống.
- Khởi tạo Response=Pending, RespondedAt=null, DelegatedToUserId=null, Attendance=Unknown.
- Không tự chuyển Accepted cho creator/Chair/Secretary được thêm.
- Một dòng duy nhất cho MeetingId/UserId. Thêm lại đúng role vào membership trực tiếp hiện có là no-op 200, không email/audit mới; role khác trả 409 và yêu cầu PUT role.
- Nếu dòng hiện có chỉ được tạo bởi delegation, POST trực tiếp cùng role chuyển IsDirectInvitation=true; giữ response, không gửi lời mời membership lần hai. Role khác: dùng PUT role sau thao tác promote hoặc thao tác đã được UC chốt, không đổi role ngầm.
- Candidate search và kiểm tra lúc POST đều đọc account active từ DB. Không dùng /api/users vốn dành cho AdminUsers để làm picker.

### 5.2. Vai trò

- Đổi role bằng PUT khi được phép quản lý danh sách.
- Giữ Response, RespondedAt, DelegatedToUserId và Attendance; tăng participant Revision.
- Đổi role của một membership tự tạo bởi delegation đồng thời chuyển thành direct invitation: manager đã chủ động giữ người đó trong danh sách; không để thay đổi role bị xoá về sau do người khác rút uỷ quyền.
- Không dùng uỷ quyền như cách chuyển ghế chủ toạ/thư ký. Nếu Chair uỷ quyền việc tham dự, người nhận mới vẫn là Member. Quyền Chair của người cũ không chuyển sang người mới; thay người điều hành cần thao tác gán role có thẩm quyền.

### 5.3. Tự phản hồi

- Body chỉ nhận response, delegatedToUserId khi cần, expectedParticipantId và expectedRevision; không nhận userId người trả lời, Attendance, role hoặc respondedAt. expectedParticipantId chỉ chống request cũ, không dùng để chọn actor thay sub.
- Response chỉ Accepted/Declined/Delegated; Pending là trạng thái chưa phản hồi do hệ thống quản lý, không phải lựa chọn gửi từ client.
- Accepted/Declined phải có delegatedToUserId=null; Delegated bắt buộc có ID hợp lệ.
- Mỗi thay đổi thật set RespondedAt=server UTC, tăng Revision và ghi audit.
- Gửi lại cùng phản hồi và cùng target với revision hiện tại là no-op; không reset thời gian hoặc gửi thêm email.
- Được đổi câu trả lời trong Published. Khi rời Delegated phải xử lý cleanup quan hệ cũ trong cùng transaction.
- Response không thay Attendance. Accepted không có nghĩa Present; Declined không tự thành Absent; Delegated không tự thành Attendance.Delegated.
- Người Declined/Delegated vẫn là participant, giữ quyền tài liệu theo ADR-0006. Việc xoá membership là hành động khác của manager hoặc cleanup membership được tạo riêng để dự thay.

### 5.4. Uỷ quyền A → B

1. A là participant active, được RSVP lúc Published. B active, khác A; không chọn email hoặc khách ngoài chưa có account.
2. Baseline D08: một cấp, không vòng, mỗi B chỉ nhận một uỷ quyền hiện hành. A đang nhận uỷ quyền không thể uỷ quyền tiếp; B đã uỷ quyền người khác không thể được chọn. B không được có nguồn khác đang trỏ đến.
3. B chưa có membership: tạo Member, Pending, Attendance.Unknown, IsDirectInvitation=false, enqueue lời mời B.
4. B đã có membership: giữ Role và Response của B, không thêm dòng hoặc reset câu trả lời. Nếu B đang Declined, trạng thái dự thay cũng là Declined, A phải thấy rõ và có thể đổi người nhận.
5. Ghi A.Response=Delegated, A.DelegatedToUserId=B, RespondedAt=UTC. Không tự gán Accepted cho B.
6. Gửi thông tin được uỷ quyền cho B qua email contract; nếu B vừa được tạo thì gộp nội dung này vào một thư mời, không gửi hai thư cùng lúc.
7. B trả lời Accepted/Declined bằng endpoint /me/response. B không được uỷ quyền tiếp khi đang có nguồn A trỏ tới.
8. Bảng quản lý hiển thị cả phản hồi A và trạng thái B; A xem được thông tin B liên quan trực tiếp tới uỷ quyền của mình, không nhận toàn bộ roster.

Không tự chuyển quyền bỏ phiếu, không bỏ phiếu thay, không tăng quyền quản lý hoặc nhân số quorum. Story voting/attendance phải sử dụng contract riêng; task này chỉ ghi nhận RSVP và membership, không viết thuật toán bỏ phiếu uỷ quyền.

### 5.5. Đổi ý, đổi người nhận, gỡ người

- A đổi từ Delegated(B) sang Accepted/Declined hoặc Delegated(C): gỡ liên kết B cũ trước, cập nhật nguồn/đích mới trong cùng transaction.
- Nếu B là direct invitation: giữ B cùng response/role hiện tại.
- Nếu B chỉ được tạo để dự thay và không còn nguồn uỷ quyền nào: xoá membership B, audit, suppress thư membership chưa gửi và queue thông báo thu hồi theo D11. Không giữ quyền tài liệu chỉ vì email cũ vẫn còn.
- Manager có thể giữ B độc lập bằng POST trực tiếp trước khi A rút uỷ quyền; IsDirectInvitation giúp phân biệt hai tình huống.
- Gỡ A đang Delegated: cleanup quan hệ B như trên rồi xoá A.
- Gỡ B đang là target: baseline D09 trả 409 ATTENDEE_HAS_DELEGATION; yêu cầu xử lý uỷ quyền trước. Không tự sửa RSVP của A và không để dangling target.
- DELETE dùng participantId cụ thể, không dùng chỉ UserId. Xoá rồi mời lại tạo participantId mới, Pending mới, thư mời mới; request DELETE cũ không được xoá membership vừa được thêm lại.
- Baseline dùng hard-delete membership khi D03 cho phép, audit giữ snapshot tối thiểu trước xoá. Không soft-delete ngầm vì ADR-0006 và các policy dựa trên dòng membership hiện hữu; soft-delete sẽ đòi đổi đồng bộ mọi truy vấn quyền.
- Không xoá user, email đã gửi, audit, vote receipts hoặc lịch sử trạng thái meeting. Dữ liệu biểu quyết đã mở/chốt có snapshot riêng, không tính lại EligibleCount do sửa attendee.

## 6. Tạo thư mời nhất quán với công bố

### 6.1. Một writer dùng chung

Phụ thuộc D11/SCRUM-15: IMeetingInvitationWriter (tên dự kiến, reuse tên thực tế nếu đã có) chỉ ghi EmailMessage trong DbContext/transaction của caller; không gọi SMTP và không tự commit transaction độc lập.

Hai trigger phải dùng cùng writer và cùng khóa Meeting:

- Approved → Published: enqueue cho từng membership hợp lệ ở thời điểm công bố.
- Thêm membership khi Published: enqueue cho membership vừa tạo, gồm người được thêm do delegation.

Trước Published: không queue thư mời. Retry HTTP hoặc no-op không queue thêm. API thành công chỉ chứng minh dữ liệu và hàng đợi đã commit, không có nghĩa người nhận đã đọc/đã nhận email trong inbox.

### 6.2. Idempotency và liên kết email

Mỗi lời mời membership có business key xác định `meeting-invitation:{meetingId}:{participantId}`. Publish và Add dùng cùng key. Unique index DB trên dedup key (nullable với email legacy) chặn race; không chỉ check tồn tại rồi insert.

Các email uỷ quyền/thu hồi/cập nhật role nếu contract yêu cầu có kind riêng và key gắn source participant + revision của thay đổi; không dùng cùng key với lời mời membership.

Email giữ RecipientUserId và RecipientParticipantId (membership instance) hoặc metadata tương đương để kiểm tra trước gửi. RecipientParticipantId là snapshot tham chiếu có chủ ý nếu membership có thể xoá; không cascade xoá email theo participant. Chốt field/schema với owner email, không thêm hai bộ delivery tracking song song. Revision của source tăng khi đổi role cũng không được làm mất thư mời membership còn hợp lệ: membership invitation kiểm recipient instance; phần nội dung delegation kiểm quan hệ source/target hiện tại trước gửi. Chỉ suppress cả lời mời khi membership không còn hợp lệ; không chỉ vì metadata delegation trong thư đã cũ.

Trước gửi, sender xác nhận membership instance vẫn tồn tại, tài khoản nhận còn active và meeting vẫn Published/InProgress; thư delegation còn đúng nguồn/đích/revision theo kind. Email cũ của membership đã xoá không được hợp thức hóa chỉ vì cùng UserId được mời lại.

- Queued: chờ worker.
- Sent: hệ thống gửi đã chấp nhận thư theo contract transport; không khẳng định delivery/read receipt.
- Failed: hết retry theo SCRUM-15; phải thấy rõ trong bảng quản lý.
- Suppressed: đề xuất thêm vào contract cho thư không còn hợp lệ trước gửi; không giả Sent hoặc Failed do SMTP.
- NotQueued: giá trị DTO khi chưa Published/không có job, không nhất thiết thêm enum DB.

Khi membership bị gỡ, suppress job chưa gửi; nếu thư đã gửi/đang gửi có thể cần email thu hồi. Không cam kết thu hồi được email đã rời hệ thống. Worker không giữ khóa DB khi gọi SMTP. Race xoá trong lúc SMTP đang gửi phải được mô tả trung thực và xử lý bằng quyền truy cập hiện tại + thông báo thu hồi, không tuyên bố gửi exactly-once qua SMTP.

### 6.3. Hợp đồng phải nhận từ SCRUM-15

- Method enqueue tham gia transaction, dedup constraint và mapping duplicate.
- Recipient identity, message kind, suppressed status và kiểm tra trước gửi.
- Retry/backoff/claim chống hai sender gửi đồng thời; cách phân biệt queued/sent/failed.
- Nội dung tiếng Việt, URL cùng hệ thống đã cấu hình, thời gian/múi giờ hiển thị và .ics nếu shared invitation contract yêu cầu.
- Link email mở meeting sau đăng nhập, không dùng GET link để ghi RSVP; email scanner không được vô tình xác nhận tham dự.
- Không nhúng token đăng nhập, toàn bộ roster hoặc tài liệu mật vào thư.
- Fixture/mail transport test và bằng chứng email thật tới môi trường kiểm thử.

Chưa có contract hoặc sender thật thì phần tích hợp bị chặn có tên rõ ràng; không viết fake production rồi báo đạt AC. Không tự xây lại hạ tầng SMTP của story khác. In-app notification/SignalR không là điều kiện bắt buộc của task này; nếu có thì phát sau commit theo contract riêng.

## 7. Thay đổi dữ liệu dự kiến

| Entity | Thay đổi |
|---|---|
| Meeting | AttendeeListRevision int ≥0; mặc định 0, tăng một lần/transaction thay đổi cấu trúc roster |
| MeetingParticipant | Revision int ≥1; IsDirectInvitation bool; giữ Role/Response/DelegatedToUserId/RespondedAt và Attendance hiện tại |
| EmailMessage | DedupKey và recipient membership metadata + kind/status cần thiết theo contract SCRUM-15; không có generic job spine |

AttendeeListRevision tăng khi add/delete/role change/promote trực tiếp hoặc delegation làm thay đổi roster. RSVP Accepted/Declined đơn thuần chỉ tăng participant Revision; không làm mọi người phải reload một revision chung để trả lời. Không dùng AttendeeListRevision như version của bảng summary; GET không cache và đọc dữ liệu nhất quán.

Ràng buộc:

- Giữ unique (MeetingId, UserId).
- DelegatedToUserId có giá trị khi và chỉ khi Response=Delegated; target khác UserId.
- Pending có RespondedAt=null; mọi response khác có RespondedAt.
- Theo D08: partial unique (MeetingId, DelegatedToUserId) WHERE Response='Delegated' AND DelegatedToUserId IS NOT NULL.
- Cùng meeting của target kiểm trong service/transaction; FK UserId hiện tại không tự chứng minh target có membership.
- Mọi enum lưu string, ID Guid v7, thời gian UTC, FK tới user Restrict.
- Attendance giữ default Unknown; không lặp lại lỗi migration cũ default chuỗi rỗng.

Migration mới cùng ModelSnapshot. Dữ liệu cũ mặc định IsDirectInvitation=true để không vô tình xoá người đã được mời. Backfill revision rõ ràng. Preflight rà self-delegation, dangling target, nhiều nguồn cùng target, Response/timestamp không khớp; nếu có thì dừng và lập phương án xử lý, không đoán consent/response hoặc xoá dữ liệu để constraint pass. Email legacy có DedupKey null, không giả rằng chúng là invitation đã được đối soát.

Không thêm bảng roles của ADR-0008 trong task này. Nếu SCRUM-63 merge trước implementation thì dùng contract capability mới, nhưng MeetingRole và API attendee role vẫn giữ bốn giá trị riêng.

## 8. Contract API dự kiến để BA/BE/FE thống nhất

Tên DTO/error code dưới đây là đề xuất cho task, chưa phải contract đã ban hành. Sau P00/P01, chép bản đã chốt vào docs/03-api; model không tự gán các mã MSG của UC khác. JSON camelCase; enum là string PascalCase; ID là UUID; thời gian ISO 8601 UTC. Giới hạn HTTP/binding dùng cấu hình chung của dự án.

### 8.1. Danh sách endpoint

| Method và route | Actor theo baseline | Kết quả |
|---|---|---|
| GET /api/meetings/{id}/attendees | Manager hoặc participant được đọc meeting | 200 Full/Self theo D04 |
| POST /api/meetings/{id}/attendees | Manager | 201 tạo; 200 no-op hoặc promote |
| DELETE /api/meetings/{id}/attendees/{attendeeId}?expectedListRevision=7 | Manager | 204; attendeeId là ID membership |
| POST /api/meetings/{id}/attendees/me/response | Chính participant | 200 phản hồi đã commit |
| PUT /api/meetings/{id}/attendees/{attendeeId}/role | Manager | 200; bổ sung theo D10 |
| GET /api/meetings/{id}/attendees/candidates?purpose=Invite&q=An&page=1&pageSize=20 | Invite: manager; Delegate: participant đủ điều kiện RSVP | 200 picker cá nhân; API hỗ trợ đề xuất |

DELETE trong task ghi ở mức nhóm /attendees; proposal này đặt ID trên route để tránh body DELETE và phân biệt membership cũ/mới. PUT role và candidates cần đưa vào UC/API docs trước implementation. Không thêm bulk-invite, email public RSVP hoặc endpoint chỉnh phản hồi của người khác.

### 8.2. GET attendees và thống kê

Envelope ổn định:

```json
{
  "meetingId": "01961000-0000-7000-8000-000000000001",
  "visibility": "Full",
  "listRevision": 7,
  "permissions": { "canManage": true, "canRespond": true },
  "attendees": [
    {
      "id": "01961000-0000-7000-8000-000000000002",
      "userId": "01961000-0000-7000-8000-000000000003",
      "fullName": "Nguyễn Văn An",
      "role": "Secretary",
      "isActive": true,
      "response": "Pending",
      "respondedAt": null,
      "revision": 1,
      "delegatedTo": null,
      "isDirectInvitation": true,
      "invitationDelivery": { "status": "Queued", "sentAt": null }
    }
  ],
  "summary": {
    "total": 1,
    "pending": 1,
    "accepted": 0,
    "declined": 0,
    "delegated": 0,
    "inactive": 0,
    "activeAccepted": 0
  }
}
```

- Full trả toàn bộ current membership; thứ tự Chair → Secretary → Member → Guest, tiếp theo fullName rồi id để ổn định. Danh sách v1 không phân trang; candidates có phân trang riêng. Nếu BA yêu cầu phân trang roster, summary phải tính toàn meeting, không tính chỉ một trang.
- summary.total = pending + accepted + declined + delegated. inactive là tập con theo account, không cộng lần hai vào total. activeAccepted là số tài khoản active có Response=Accepted, không phải điểm danh/quorum hay số phiếu.
- Theo D04, Self trả đúng một row của caller, summary=null, listRevision=null; isDirectInvitation và invitationDelivery=null. Không lấy full roster gửi xuống rồi nhờ FE giấu. Nếu caller không có membership và không là manager thì không trả bảng rỗng làm lộ meeting.
- delegatedTo khi có: userId, fullName, isActive, response của người nhận trong cùng meeting. Caller chỉ đọc thông tin người mình đã chọn để biết họ đã xác nhận chưa; không được traverse roster qua ID tùy ý. Target chưa xác nhận hiển thị Pending.
- permissions là tiện ích FE, không thay server check; canManage chỉ true nếu actor được quản lý và trạng thái cho phép, canRespond chỉ true nếu có membership và đang Published.
- Đọc account, policy, roster và summary trong cùng snapshot nhất quán, ví dụ transaction RepeatableRead chỉ đọc. Phép tổng hợp dùng cùng tập dữ liệu được phép xem. Không query summary ở thời điểm khác rồi ghép với roster cũ.
- Không trả email cá nhân, capability toàn hệ thống, password/token, nội dung lỗi SMTP thô, toàn bộ audit hoặc Attendance nhầm thành RSVP. Dữ liệu delivery chỉ của thư mời membership hiện tại; email thông báo delegation là kind khác.
- Response có Cache-Control: no-store. Không dùng listRevision để quyết định 304 vì RSVP và delivery có thể đổi mà listRevision không tăng.

### 8.3. POST thêm và PUT role

POST body:

```json
{
  "userId": "01961000-0000-7000-8000-000000000004",
  "role": "Member",
  "expectedListRevision": 7
}
```

PUT role body:

```json
{ "role": "Chair", "expectedListRevision": 8 }
```

Response chung: `{ attendee, listRevision }`; attendee theo DTO manager ở 8.2. POST mới trả 201; chưa có GET single attendee nên nếu dùng Location thì trỏ collection GET hợp lệ, không tạo link tới route không tồn tại. Khi Published, delivery sau commit là Queued, không giả Sent. Draft/Rejected là NotQueued. PUT đổi thật tăng participant.revision và listRevision; cùng role trên direct membership là no-op. Cùng role trên auto membership vẫn promote thành direct và tăng revision.

expectedListRevision bắt buộc, số nguyên không âm. Không mặc định giá trị thiếu thành 0. Kiểm revision trước khi nhận no-op; request retry cũ có thể nhận 409, FE tải lại rồi hiển thị trạng thái thật. Không tự lặp mutation với revision mới vì có thể ghi đè lựa chọn người khác.

DELETE nhận expectedListRevision ở query vì api.delete hiện không định nghĩa body. Sau 204 FE GET lại. ID không thuộc meeting hoặc đã bị xoá trả 404 không body; không dùng ID cũ để tìm và xoá membership mới của cùng UserId. Không yêu cầu DELETE lần hai trả 204.

### 8.4. POST /me/response

Chấp nhận:

```json
{
  "response": "Accepted",
  "delegatedToUserId": null,
  "expectedParticipantId": "01961000-0000-7000-8000-000000000002",
  "expectedRevision": 1
}
```

Uỷ quyền:

```json
{
  "response": "Delegated",
  "delegatedToUserId": "01961000-0000-7000-8000-000000000004",
  "expectedParticipantId": "01961000-0000-7000-8000-000000000002",
  "expectedRevision": 1
}
```

Response 200: `{ attendee }` theo projection Self, có revision mới và delegatedTo hiện tại. Manager gọi endpoint này cũng chỉ nhận projection Self; muốn summary thì GET. Nếu delegation tạo/xoá membership, tăng listRevision nội bộ nhưng không trả toàn bộ roster cho caller.

expectedParticipantId bắt buộc và phải khớp membership hiện tại của sub, expectedRevision bắt buộc và ≥1. Cặp này ngăn request cũ tác động vào một membership vừa xoá rồi tạo lại có revision cũng bằng 1; sai cặp trả ATTENDEE_VERSION_CONFLICT. Validation chặn số enum, chuỗi enum sai, Guid.Empty và tổ hợp response/target sai. Dùng DTO chỉ có field được phép; không bind entity. Field ngoài contract không được ảnh hưởng dữ liệu; nếu chọn reject unknown JSON field thì cấu hình riêng DTO, không đổi serializer toàn app ngoài phạm vi.

### 8.5. Picker cá nhân

purpose chỉ Invite hoặc Delegate. q được trim, tối đa 100 ký tự; q rỗng trả trang đầu; page≥1, pageSize mặc định 20 và từ 1–50. Search theo fullName (và email chỉ để khớp nếu owner account cho phép), luôn query có giới hạn tại DB. DTO chỉ id/fullName, envelope items/page/pageSize/hasMore; không cần trả tổng số tài khoản toàn hệ thống.

Invite chỉ chạy khi manager đang được sửa roster. Delegate chỉ chạy khi caller đang được RSVP và được phép uỷ quyền theo D08. Kết quả là các account active hợp lệ theo purpose; loại caller ở Delegate, target đã uỷ quyền tiếp hoặc nhận uỷ quyền từ nguồn khác. Target hiện tại của caller vẫn hợp lệ cho no-op. Người đã có membership vẫn có thể là candidate Delegate; Invite có thể trả lại để thao tác promote/no-op, không tạo bản ghi trùng.

Không gate picker bằng AdminUsers và không trả route /api/users cho FE thay thế. Kiểm lại toàn bộ điều kiện ở mutation; kết quả tìm kiếm không là quyền ghi hoặc bảo đảm account còn active sau đó. Không đủ quyền đọc meeting thì 404, không lộ directory thông qua meetingId bất kỳ. Thiết kế quyền cụ thể phụ thuộc D05.

### 8.6. Lỗi

| HTTP | Code đề xuất / body | Trường hợp |
|---|---|---|
| 400 | ATTENDEE_VALIDATION_FAILED + errors | Thiếu/không hợp lệ field, enum số, revision thiếu/âm, query sai |
| 401 | Không body, theo auth middleware | Chưa đăng nhập/token không hợp lệ |
| 403 | Không body, theo gate/policy đã chốt | Account không còn active; hoặc đọc được meeting nhưng không có quyền thao tác |
| 404 | Không body | Meeting không tồn tại/không được đọc; participant ID không thuộc meeting; caller không có membership để /me/response |
| 409 | ATTENDEE_VERSION_CONFLICT | Revision gửi lên đã cũ |
| 409 | ATTENDEE_STATE_CONFLICT | Trạng thái không cho sửa/RSVP |
| 409 | ATTENDEE_ROLE_CONFLICT | POST trùng người nhưng khác role |
| 409 | ATTENDEE_DELEGATION_CONFLICT | Tự uỷ quyền, vòng/chuỗi, target có nguồn khác theo D08 |
| 409 | ATTENDEE_HAS_DELEGATION | Gỡ target đang được uỷ quyền |
| 422 | ATTENDEE_CANDIDATE_UNAVAILABLE | UserId không tồn tại/không active/không còn chọn được; thông báo chung, không tiết lộ lý do nhạy cảm |
| 503 | ATTENDEE_DEPENDENCY_UNAVAILABLE | Dependency bắt buộc tạm hỏng trước commit; không dùng cho SMTP trễ sau commit |

Ví dụ lỗi nghiệp vụ:

```json
{
  "code": "ATTENDEE_VERSION_CONFLICT",
  "message": "Danh sách đã thay đổi. Vui lòng tải lại trước khi thực hiện."
}
```

403/404 và inactive cần owner D05 xác nhận cùng shared policy; không trả 403 trước khi xác định caller được biết meeting tồn tại. Với mọi resource route: authenticate/active → resource visibility → action permission → validation chi tiết/state/version; không để lỗi target hoặc state tiết lộ tài nguyên bị giấu. Lỗi route/binding cơ bản có thể xảy ra trước handler theo framework nhưng không chứa thông tin DB. Không trả exception/SQL/SMTP credential.

## 9. Transaction, cạnh tranh và audit

### 9.1. Đơn vị ghi

Mỗi lệnh mutation có một transaction DB: participant changes + revisions + audit + email outbox. Dùng DbContext scoped hiện có. Kiểm tra bằng test rollback rằng AuditService.SaveChanges không commit transaction ngoài. Không sửa hành vi audit của toàn bộ user CRUD trong task này.

Trình tự thống nhất cho Add/Role/Delete/RSVP và các lifecycle transition liên quan:

1. Gate đã chốt D05 xác thực caller; bắt đầu transaction.
2. Khóa dòng Meeting bằng SELECT ... FOR UPDATE có tham số, trong repository Infrastructure, tên bảng/cột lấy từ model thực tế. Không string interpolation raw từ request.
3. Đọc lại trạng thái, quyền membership/capability cần thiết và rows liên quan; không sử dụng kết quả authorization cũ làm quyết định cuối.
4. Kiểm action permission, state, expected revision và candidate còn active. Các writer của cùng meeting phải tuân thủ khóa Meeting trước; nếu khóa thêm user/participant, dùng thứ tự ID nhất quán. Phối hợp owner lifecycle/attendance/voting, không tự sửa tất cả story đó.
5. Tính diff, apply thay đổi, tăng mỗi revision đúng quy tắc, chuẩn bị audit và outbox.
6. SaveChanges, commit; sau đó mới trả 2xx hoặc phát event. Không gọi SMTP/HTTP ngoài trong transaction.
7. Lỗi trước commit rollback hết. Map constraint đã biết sang lỗi nghiệp vụ; không bắt mọi DbUpdateException thành duplicate.

AttendeeListRevision tăng tối đa một lần trong lệnh dù delegation vừa xoá B vừa thêm C. Participant.Revision tăng một lần cho mỗi row hiện hữu thực sự thay đổi role/origin/response; row mới bắt đầu 1; row xoá được audit với revision trước xoá. Thay đổi response của B không làm tăng revision của nguồn A; GET A vẫn đọc delegatedTo từ snapshot hiện tại.

Read-check revision chỉ đủ chống cạnh tranh nếu mọi writer dùng cùng lock. Cấu hình revision concurrency token là lớp phòng vệ thêm; không thay thế transaction/lock hoặc tự sửa client revision. Một request stale phải rollback toàn bộ.

### 9.2. Các cuộc đua bắt buộc làm rõ

- Hai POST cùng meeting/UserId: unique membership là chốt DB; cùng expectedListRevision chỉ một lệnh đổi thật thắng, lệnh kia 409. Sau GET mới, POST trùng đúng role 200 no-op.
- Hai RSVP cùng source revision: một thắng, một 409; không có hai target auto bị bỏ lại.
- Hai nguồn chọn một target: khóa Meeting + partial unique giữ D08; lệnh sau thấy conflict.
- Xoá membership và mời lại: ID mới, key thư mời mới; request và email của ID cũ không tác động ID mới.
- Publish vs Add: dùng chung Meeting lock. Với baseline Approved không cho thêm, nếu Add thấy Approved trước thì 409 và không ghi gì; nếu Publish commit trước, Add thấy Published và queue lời mời mới. Không hứa cả hai thứ tự đều làm Add thành công. Người thêm hợp lệ từ Draft/Rejected trước đó được Publish queue sau.
- Start vs Add/RSVP: nếu lệnh attendee commit trước thì Start thấy dữ liệu mới; nếu Start thắng thì lệnh attendee thấy InProgress và 409. Lifecycle owner phải dùng cùng khóa; lock chỉ ở SCRUM-20 không giải quyết được race.
- Deactivate account: kiểm active ngay trước mutation trong transaction; sender kiểm lại trước gửi. Nếu cần bảo đảm tuyến tính cả với thao tác vô hiệu hoá, owner user phải thống nhất khóa account với cùng thứ tự, không tuyên bố check một lần giải quyết mọi race. Account inactive sau khi được thêm vẫn hiện ở summary và không được dùng API.

Outbox dedup phải do constraint bảo đảm. Nếu writer dùng PostgreSQL INSERT ... ON CONFLICT (dedup_key) DO NOTHING, index phải tương thích; nullable unique key cho phép legacy null. Nếu writer dùng EF exception thì rollback/savepoint đúng cách; không tiếp tục transaction PostgreSQL đã bị lỗi unique violation. Caller không tự commit để xử lý duplicate email.

Sender gọi SMTP theo contract SCRUM-15, ngoài transaction nghiệp vụ. Transport thành công rồi mất kết nối trước cập nhật Sent có thể gửi lặp khi retry; không đưa lời hứa exactly-once vào docs/AC. API đã commit queue vẫn thành công khi SMTP đang tạm lỗi; trạng thái Failed/Queued cho manager theo dõi.

### 9.3. Audit

Reuse AuditLogEntry/IAuditService, không ghi vào MeetingStatusHistory vì RSVP không chuyển trạng thái meeting. Đề xuất actions: MeetingAttendeeAdded, MeetingAttendeeRemoved, MeetingAttendeeRoleChanged, MeetingAttendeePromoted, MeetingAttendanceResponseChanged. Mỗi row audit có actor từ sub, server time, meetingId, participantId, userId liên quan, before/after role/response/target/origin cần thiết, operationId để nối các thay đổi của một lệnh.

Cleanup B do A rút uỷ quyền ghi actor A và nguyên nhân DelegationWithdrawn; không giả actor B/thư ký. Thêm B tự động có cause DelegationCreated và source participant ID. No-op không tạo lịch sử thay đổi giả. Không log JWT/password/nội dung toàn roster/email body. Không tạo endpoint sửa/xoá audit; giữ nguyên tính append-only của MeetingStatusHistory.

## 10. Bản đồ file dự kiến

Đường dẫn dưới đây là tương đối với repo. Tên mới chỉ là đề xuất; khi triển khai kiểm tra owner đã thêm abstraction tương đương chưa, rồi reuse. Không dựng project Application/CQRS/event bus mới cho task này.

| Khu vực | File / thay đổi dự kiến |
|---|---|
| Domain model | backend/TrustCabinet.Domain/Entities/Meeting.cs; MeetingParticipant.cs; EmailMessage.cs chỉ phối hợp owner email |
| Domain contracts | backend/TrustCabinet.Domain/Meetings/IMeetingAttendeeService.cs, attendee DTO/result; shared policy interface theo D05 |
| Infrastructure | backend/TrustCabinet.Infrastructure/Meetings/MeetingAttendeeService.cs; repository/query/locking nếu cần theo pattern hiện hữu |
| EF | Configurations/MeetingConfiguration.cs, MeetingParticipantConfiguration.cs; EmailMessageConfiguration.cs theo contract; Migrations + ModelSnapshot |
| Authorization | backend/TrustCabinet.Web/Endpoints/CapabilityGate.cs chỉ khi D05 owner chốt; shared meeting policy không nhân bản theo endpoint |
| HTTP | backend/TrustCabinet.Web/Endpoints/MeetingAttendeeEndpoints.cs; register MapMeetingAttendeeEndpoints và DI trong Program.cs |
| Invitation | Reuse service/writer của SCRUM-15 và gọi từ lifecycle SCRUM-19; không tự đặt một sender thứ hai |
| Tests | tests/TrustCabinet.Web.Tests/Meetings/MeetingAttendeeServiceTests.cs, MeetingAttendeeEndpointsTests.cs, MeetingAttendeeConcurrencyTests.cs; DatabaseModelTests.cs |
| FE contract | frontend/src/types/meetingAttendee.ts; frontend/src/api/meetingAttendees.ts; không triển khai tab ở task BE |
| Docs | API attendee + note cho FE trong docs/03-api và index; ERD/lifecycle nếu thay đổi; BA cập nhật spec/diagrams; ADR mới nếu quyết định thiết kế đáng kể |

Không sửa accepted ADR-0004/0005/0006 để hợp thức hóa implementation. Không sửa DEVELOPMENT_WORKFLOW. Không chỉnh wwwroot, không đổi auth client đang hoạt động, không thêm CORS/base URL FE. Không commit trong lượt lập kế hoạch này.

## 11. Phụ thuộc và điều kiện bắt đầu

| Điều kiện | Người/feature cung cấp | Nếu chưa có |
|---|---|---|
| UC spec + sequence/class diagram, D03–D10 | BA + owner phân quyền; D05 được người dùng yêu cầu giữ chờ | Chỉ hoàn thiện kế hoạch/contract review, chưa triển khai business endpoint |
| Account/capability thực tế | Auth/RBAC hiện có, cập nhật nếu SCRUM-63 merge | Không hardcode SystemRole để thay capability |
| Meeting visibility + manager bootstrap | SCRUM-17/19 + BA | Không bỏ qua CanReadMeeting hoặc tự mở Draft |
| Khóa chung Publish/Start và quy tắc roster | Owner lifecycle | Chưa nghiệm thu concurrency tích hợp |
| Invitation writer/outbox/sender thật | SCRUM-15 + owner SCRUM-19 | Chưa nghiệm thu AC gửi thư; không thay bằng log/mock production |
| PostgreSQL test tách biệt | Test infrastructure/CI | Không dùng InMemory để kết luận unique/transaction/lock đã đúng |

Workflow yêu cầu UC đủ rõ mới vào implementation. Các bước dưới là thứ tự dự kiến sau khi đầu vào được bàn giao, không là chỉ thị bỏ qua blocker hiện tại. Hạ tầng độc lập có thể được owner chuẩn bị riêng; task SCRUM-20 không tự nhận phạm vi của các task còn thiếu.

## 12. Các bước triển khai nhỏ, đầu ra và cách kiểm tra

Mỗi bước kết thúc bằng đầu ra kiểm tra được. Model triển khai ghi file đã đổi, kiểm tra đã chạy, kết quả và blocker vào báo cáo local; không đánh dấu bước sau là xong vì bước trước đã build. P00–P02 có thể hoàn thiện dưới dạng tài liệu bàn giao; P03 trở đi chỉ bắt đầu khi UC đủ điều kiện theo workflow. Các lệnh cuối mục 14 dành cho lượt implementation tương lai, chưa chạy trong lượt lập kế hoạch.

### P00 — Nhận bàn giao nghiệp vụ và giải quyết các điểm chờ

- Đầu vào: task Jira, kế hoạch này, workflow, UC Specification và diagrams BA cung cấp.
- Làm: xác nhận D03/D04/D06–D10; giữ D05 chờ owner đúng yêu cầu người dùng. Owner phải ghi rõ gate cho Member/Secretary/Chair, visibility trước công bố và mã 401/403/404. Chốt giới hạn delegation và việc rút uỷ quyền có thu hồi auto membership.
- Đầu ra: decision ledger có người chốt/ngày/nguồn; UC + diagrams thống nhất, danh sách dependency owner.
- Kiểm tra: không còn ô nghiệp vụ phụ thuộc ghi “đề xuất” mà đã bị dùng như yêu cầu bắt buộc; Member không capability có đường RSVP hợp lệ trong thiết kế được duyệt. Nếu chưa đạt, dừng phần code và cập nhật plan, không tự bổ sung quyền.

### P01 — Đóng băng contract API cho một phiên bản

- Đầu vào: P00, mục 8, auth/error conventions hiện tại.
- Làm: thống nhất route DELETE, PUT role, candidates, enum DTO, expectedParticipantId/revisions, Full/Self, summary và error codes; đưa vào API docs/index. Nếu BA không cần PUT role hoặc chọn roster visibility khác, sửa tất cả ví dụ/test cùng lúc.
- Đầu ra: API contract có request/response/error và bảng quyền/trạng thái; checklist FE dùng được.
- Kiểm tra: mọi JSON example parse được; bốn role tách biệt system role; không có orgUnitId, proxy vote, Attendance write hoặc body actorId. FE và tester xác định được request cho từng AC mà không phải đoán.

### P02 — Chốt contract tích hợp lifecycle và thư mời

- Đầu vào: P00/P01, owner SCRUM-15 và SCRUM-19/meeting Start.
- Làm: thống nhất transaction ownership, Meeting lock, dedup key, recipient membership identity, suppression/retry/kind và delivery projection. Chốt thu hồi delegation và nội dung email gộp khi target mới.
- Đầu ra: contract writer/sender được owner bàn giao, sơ đồ Publish/Add/Start dùng cùng lock; integration fixture.
- Kiểm tra: cả publish và add muộn có cùng key cho cùng membership; writer không tự commit/call SMTP; sender phân biệt membership cũ/mới. Chưa có sender thì ghi blocker AC email, không dựng placeholder production.

### P03 — Thêm model và EF configuration đã được chốt

- Đầu vào: P00–P02, entity/config hiện tại, ERD.
- Làm: thêm revisions và IsDirectInvitation, enum/string mapping, concurrency token và constraints mục 7; email fields do một owner thực hiện. DTO/contracts Domain không tham chiếu EF/ASP.NET.
- Đầu ra: model/config cập nhật, model tests và ERD diff tương ứng.
- Kiểm tra: DatabaseModelTests vẫn cấm org units; FK user Restrict, unique MeetingId/UserId, default Attendance.Unknown; kiểm model metadata cho constraints/revisions. Không đổi SystemRole→Role table trong bước này.

### P04 — Thực hiện shared authorization đúng phương án owner duyệt

- Đầu vào: quyết định D05 đã được owner phân quyền/BA chốt, shared visibility contract từ P00. **Hiện bước này bị chặn theo yêu cầu người dùng.**
- Làm: reuse CapabilityGate/account/capability repositories; triển khai gate/resource policy đúng bản duyệt, đăng ký DI. Update API 04-phan-quyen-phia-server nếu hợp đồng gate đổi. Tách quyền đọc, quản lý, tự RSVP, tìm candidate; xác định creator bootstrap.
- Đầu ra: một shared policy dùng lại được và test matrix actor; không có policy riêng mâu thuẫn ở từng route.
- Kiểm tra: active Member không capability RSVP được chính mình; Secretary/Chair được quyền theo meeting đã chốt; capability chỉ trong token nhưng DB đã thu hồi không đủ cho nhánh creator; inactive bị chặn; outsider/external guest không thấy meeting. Existing CapabilityGateTests không bị nới quyền.

### P05 — Sinh migration, kiểm dữ liệu cũ và áp dụng DB thử nghiệm

- Đầu vào: P03, migration mới chưa có trên main, PostgreSQL riêng cho test.
- Làm: preflight legacy violations, tạo migration + Snapshot; giữ legacy direct=true, revision ban đầu rõ ràng, email legacy key=null. Kiểm SQL constraint tên bảng thực tế, không đoán snake_case.
- Đầu ra: migration có thể áp lên DB rỗng và DB ở migration trước, ghi chú backfill/rollout.
- Kiểm tra: apply trên cả hai DB thử; round-trip đủ role/response; raw insert sai target/timestamp hoặc duplicate bị DB từ chối; dữ liệu legacy không bị xoá. Không sửa migration đã merge, không chạy trên DB dùng chung khi chưa có lịch phối hợp.

### P06 — Tạo lớp ghi với transaction và khóa Meeting

- Đầu vào: P02/P04/P05, scoped DbContext + AuditService.
- Làm: tạo service/repository skeleton theo pattern repo; encapsulate parameterized row lock và quy tắc transaction mục 9; phân loại lỗi nghiệp vụ bằng result hiện hữu, không throw HTTP exception từ Domain.
- Đầu ra: đường mutation có lock/rollback/revision helper; chưa thêm toàn bộ business logic một lần.
- Kiểm tra: integration test PostgreSQL hai connection chứng minh lock serialize cùng meeting; injected failure sau SaveChanges/audit rollback mọi row. Cancellation rollback, không giữ lock qua SMTP.

### P07 — Query roster, Self và summary

- Đầu vào: P01/P04/P05.
- Làm: projection DB theo Full/Self, delegation status, summary và delivery contract; một read snapshot, sắp xếp ổn định, no-store.
- Đầu ra: service GET DTO đúng mục 8.2.
- Kiểm tra: seed nhiều role/response và account inactive, so sánh total/subtotals; Self chỉ một row + target của chính mình, summary/listRevision null, không có email hoặc delivery của người khác. Response Declined không mất membership visibility theo ADR-0006.

### P08 — Query candidate cá nhân

- Đầu vào: P01/P04, account query thực tế.
- Làm: implement purpose/search/page/limit trong DB, projection tối thiểu, lọc active và điều kiện delegation đã duyệt.
- Đầu ra: picker backend cho manager/người dự mà không dùng AdminUsers.
- Kiểm tra: outsider/inactive không dùng được; pageSize 51, purpose sai/q quá dài bị 400; inactive/self/target gây chuỗi không xuất hiện ở Delegate. User đổi inactive sau search vẫn bị chặn ở mutation sau này.

### P09 — Thêm trực tiếp trước công bố

- Đầu vào: P06, luật Add ở P00/P01.
- Làm: add trong Draft/Rejected; validate active/role/listRevision; unique membership; Pending/Unknown defaults; no-op/direct promotion; audit nguyên nhân.
- Đầu ra: service add trước công bố và unit/integration tests.
- Kiểm tra: tạo đúng 1 row, không có EmailMessage; duplicate same role no-op sau reload revision; role khác 409; bất kỳ lỗi audit/save đều không để row mồ côi. WaitingApproval/Approved không ghi theo baseline.

### P10 — Đổi role và giữ nguyên phản hồi

- Đầu vào: P06/P09, D10 đã chốt.
- Làm: PUT role đúng state/manager; giữ RSVP/timestamp/delegation/attendance; auto→direct; increments/audit.
- Đầu ra: service role change, DTO mutation.
- Kiểm tra: Accepted vẫn Accepted với RespondedAt cũ; Secretary/Chair role không đổi SystemRole/capability; stale listRevision 409; role không đổi trên direct là no-op, trên auto là promote. Không xoá-thêm membership để đổi role.

### P11 — Gỡ trực tiếp và thu hồi quyền membership

- Đầu vào: P06/P09, shared access policy, suppression contract P02.
- Làm: DELETE theo participantId/meetingId và listRevision; chặn target inbound; audit trước xoá, suppress pending membership invitation; source delegation cleanup kết nối P14 khi có.
- Đầu ra: delete service không phá user/email/audit/history.
- Kiểm tra: participant cùng ID không thuộc meeting trả 404; stale ID không xoá lần mời mới; sau commit user mất quyền dựa trên membership. Nếu vẫn có creator/approver quyền độc lập thì shared policy vẫn áp dụng, không hứa mọi đường đọc bị mất. Không xoá vote/history rows.

### P12 — RSVP Accepted và Declined

- Đầu vào: P06, P01/D07, authorized self context.
- Làm: tìm row bằng meetingId + sub, so expectedParticipantId/revision; Published only; UTC RespondedAt; audit; no-op khi body cùng trạng thái/cặp revision hiện tại.
- Đầu ra: /me service nhận Accepted/Declined; chuẩn bị hook cleanup nếu trạng thái trước là Delegated cho P14.
- Kiểm tra: body userId/role/Attendance không làm sửa row khác; Accepted/Declined không đổi Attendance hoặc membership; no-op không đổi timestamp; hai request stale chỉ một thành công; xoá-mời lại rồi replay request cũ phải conflict dù revision trùng số.

### P13 — Uỷ quyền đến người mới hoặc đã có trong danh sách

- Đầu vào: P06/P12, D02/D08, writer P02.
- Làm: validate A/B/active/incoming/outgoing; tạo B nếu cần, Member/Pending/auto; giữ nguyên B hiện hữu; cập nhật A + audit và một email theo kind/gộp đã chốt.
- Đầu ra: delegation service atomic; target mới có thể tự trả lời.
- Kiểm tra: Chair A không truyền role cho B; B không auto Accepted; target hiện hữu Declined giữ nguyên và được phản ánh cho A; self/cycle/chain/two sources bị chặn theo D08; fail email enqueue rollback cả A và B. Không gọi SMTP đồng bộ.

### P14 — Đổi/rút uỷ quyền và cleanup có kiểm soát

- Đầu vào: P10/P11/P12/P13, origin flag và thu hồi contract.
- Làm: triển khai A→B đổi sang C/Accepted/Declined; chỉ xoá old auto target không còn nguồn; giữ direct/promoted B; gỡ A cleanup tương tự; revision list tăng một lần/lệnh.
- Đầu ra: không có dangling target hoặc auto membership mồ côi; audit actor thật và suppression/withdrawal theo contract.
- Kiểm tra: ma trận direct/auto/promoted × rút/đổi/xoá source; B đang inbound không bị manager xoá ngầm; rollback khi lỗi giữa cleanup và thêm C giữ nguyên A→B. Request đổi target không gửi thư dựa trên state chưa commit.

### P15 — Kết nối Add sau Published với writer thật

- Đầu vào: P02/P09/P13/P14; shared invitation implementation đã có.
- Làm: thêm khi Published enqueue trong cùng transaction; attach recipient membership identity/dedup key; expose Queued/Sent/Failed/Suppressed theo contract.
- Đầu ra: direct invite và delegation auto invite sau công bố cùng một cơ chế email.
- Kiểm tra: new Published participant + exactly một outbox row theo key; duplicate/no-op không enqueue lại; SMTP ngắt vẫn giữ queue và API không đợi; failure DB enqueue rollback membership. Test SMTP là của integration transport, không nhầm với assertion outbox.

### P16 — Đồng bộ với Publish và Start

- Đầu vào: P02/P06/P15, lifecycle implementation thật từ owner.
- Làm: publish enqueue all eligible current memberships bằng cùng writer; Start dùng khóa chung; không nhân đôi policy/sender trong service attendees.
- Đầu ra: integration tests cho liên story, contract lifecycle cập nhật.
- Kiểm tra: trước Published không gửi; publish retry không tăng outbox; Add thấy Approved trước publish thì 409/no insert; Add sau publish queue đúng một; Add/RSVP sau Start thắng lock đều 409. Thiếu lifecycle thật thì ghi blocker, không đánh dấu integration hoàn thành bằng fake.

### P17 — Map endpoint và chuẩn hóa lỗi HTTP

- Đầu vào: P04/P07–P16, API contract P01.
- Làm: MapMeetingAttendeeEndpoints đăng ký từ Program.cs; auth gate và DI; DTO binding/enum validation; map result/status/error tiếng Việt; cancellation token xuyên suốt.
- Đầu ra: sáu route hoạt động, OpenAPI nếu cấu hình dự án hỗ trợ, không lặp business rules ở handler.
- Kiểm tra: WebApplicationFactory gọi thật route cho 200/201/204 và 400/401/403/404/409/422; 404 resource không body; không lộ MeetingId hợp lệ qua lỗi phụ; missing revision không thành default 0/1. Gọi backend trực tiếp với token không quyền phải thất bại dù bỏ UI.

### P18 — Hoàn thiện kiểm thử PostgreSQL cho race và rollback

- Đầu vào: P05–P17, test DB/CI được chuẩn bị.
- Làm: chạy các race mục 9 bằng hai scope/connection với barrier phối hợp, không dựa vào Thread.Sleep để may rủi; exercise constraints bằng SQL thực.
- Đầu ra: suite concurrency + transaction + migration có thể lặp lại, test data cô lập từng case.
- Kiểm tra: hai nguồn/một target, same-source revision, same-user add, delete/reinvite, publish/add/start; mọi case assert cả participant/revision/audit/outbox, không chỉ status HTTP. InMemory test chỉ phục vụ logic, không thay chứng cứ DB thật.

### P19 — Kiểm thử sender và tình huống thư không còn hợp lệ

- Đầu vào: P02/P15/P16 và sender SCRUM-15 có retry/claim thật.
- Làm: kiểm inactive/removed/old participantId/meeting kết thúc; kiểm delegation metadata đã đổi; retry transport và lỗi sau send-before-mark.
- Đầu ra: bằng chứng contract email hoạt động, danh sách giới hạn delivery được ghi trong docs.
- Kiểm tra: thư Pending membership bị xoá không gửi; mời lại không làm job cũ hợp lệ; đổi role source không làm mất lời mời target còn hợp lệ; transport lỗi thể hiện Queued/Failed theo retry. Một email thử nghiệm đến mailbox của môi trường test cho ca thêm sau Published. Không gửi thử đến người thật ngoài fixture được phép.

### P20 — Thêm TS types và API wrappers cho FE

- Đầu vào: P01/P17, frontend/src/api/client.ts hiện tại.
- Làm: types enum/DTO/nullable/error code; wrappers GET/Add/Remove/Role/Respond/Candidates dùng api.* và relative paths; query encode qua URLSearchParams; không viết tab trong BE task.
- Đầu ra: FE import wrapper có type đúng, DELETE không body, response 204 trả void.
- Kiểm tra: npm run build/lint; field expectedParticipantId/revisions bắt buộc compile-time; Full/Self xử lý null; ApiError.body.code dùng đúng client hiện hữu. Không fetch từ component, không thêm VITE_API_BASE_URL hoặc auth mock.

### P21 — Đồng bộ docs và bàn giao FE/Tester

- Đầu vào: P17–P20, contract cuối và UC đã review.
- Làm: API docs + note FE, ERD/lifecycle/diagrams có thay đổi; ADR mới nếu cần, không chỉnh accepted ADR. Chuẩn bị hướng dẫn tab: loading/empty/error, permission flags, 409 reload và người nhận delegation cần tự xác nhận.
- Đầu ra: bộ handoff ở mục 15; trace AC → API/test; danh sách blocker còn thật.
- Kiểm tra: doc/examples/types/tests dùng cùng field và status; summary không được gọi là điểm danh; FE không nhận roster vượt quyền. Chuẩn bị thông báo bàn giao, chỉ gửi ra ngoài khi được người dùng cho phép.

### P22 — Chạy các kiểm tra bắt buộc và rà phạm vi

- Đầu vào: code/doc hoàn chỉnh, test dependencies chạy được.
- Làm: các lệnh mục 14 và CI PostgreSQL suite; review diff, secrets, migration/snapshot và schema đổi ngoài scope.
- Đầu ra: log pass/fail ngắn gọn; lỗi còn mở có nguyên nhân và tác động.
- Kiểm tra: build -warnaserror, dotnet test, FE build/lint đều pass; integration test không skip thầm. Không kết luận Done nếu chỉ health test pass. Không disable CI để có màu xanh.

### P23 — Nghiệm thu Backend và theo dõi bàn giao

- Đầu vào: P21/P22, tester chạy các ca mục 13, bằng chứng sender thật.
- Làm: demo bằng fixture user/meeting; đối chiếu năm AC, ghi rõ phần FE/Tester còn lại và rủi ro chưa giải quyết. Chuẩn bị PR theo quy trình khi người dùng giao implementation; lượt hiện tại chỉ lưu plan local.
- Đầu ra: Backend ready-for-FE hoặc danh sách blocker cụ thể; không tự cập nhật Jira thành Done.
- Kiểm tra: tất cả AC backend có bằng chứng, D05 đã có quyết định thực tế, không mock email/auth production; UC chỉ Done khi đủ BA/BE/FE/Tester/review theo workflow.

## 13. Ma trận kiểm thử tối thiểu

Fixture tối thiểu: creator có MeetingCreator; Secretary; Chair; Member không capability; Guest có account; outsider; account inactive; hai candidate active; external guest link. Tạo meeting Draft, Rejected, WaitingApproval, Approved, Published, InProgress và Archived. Token/capability/DB fixture phải tách biệt để kiểm thu hồi quyền, không gán mọi capability cho tất cả user.

Các test dưới đây áp dụng baseline sau khi quyết định được chốt; nếu D03/D04/D08 thay đổi, cập nhật expected outcome trước implementation.

| ID | Tình huống | Kết quả cụ thể cần assert |
|---|---|---|
| T01 | Không token gọi bất kỳ attendee route | 401, không mutation |
| T02 | Outsider gọi meeting có thật và meeting không tồn tại | Cùng 404 không body; không nhận summary/candidates |
| T03 | Account bị deactivate sau khi nhận token | Không đọc/ghi được theo D05; không tạo audit giả thành công |
| T04 | Creator còn token capability nhưng DB đã thu hồi | Nhánh creator không còn đủ quyền; không dùng token cũ để quản lý |
| T05 | Member không capability, có membership Published | Đọc Self và RSVP chính mình được theo thiết kế D05 đã duyệt |
| T06 | Member gửi body UserId người khác/Role/Attendance | Không thay actor, role, Attendance hoặc row người khác |
| T07 | Secretary/Chair và Admin ngoài meeting | Hai actor trong meeting theo policy được quản lý; Admin ngoài meeting không bypass |
| T08 | Guest account và external guest link | Guest account RSVP được theo membership; guest link không đọc roster/RSVP |
| T09 | Add Draft/Rejected | Một row Pending/Unknown, audit đúng actor, không email |
| T10 | POST trùng UserId cùng role sau GET revision mới | 200 no-op, số row/audit/email và timestamp giữ nguyên |
| T11 | POST trùng UserId khác role | 409 ROLE_CONFLICT, dữ liệu giữ nguyên |
| T12 | Input enum số/sai tên, UUID rỗng, thiếu revision | 400, không mutation; không ép số về enum hợp lệ |
| T13 | Candidate không có/không active, hoặc inactive sau search | 422 thông báo chung, không membership/outbox |
| T14 | PUT role participant Accepted | Giữ response/respondedAt/attendance/id; chỉ role/origin/revisions đổi đúng |
| T15 | Approved/WaitingApproval/InProgress/Archived mutation | 409 theo bảng trạng thái, không ghi thêm audit thành công/email |
| T16 | RSVP Accepted → Declined → Accepted | RespondedAt UTC cập nhật mỗi đổi thật, revision tăng; Attendance vẫn Unknown |
| T17 | Repeat RSVP cùng body và revision hiện tại | 200 no-op, timestamp/revision/audit/email không đổi |
| T18 | A uỷ quyền cho B mới | A Delegated, B Member/Pending/auto, một invitation outbox cho B, B tự xác nhận |
| T19 | Chair/Secretary A uỷ quyền B mới | B không kế thừa Chair/Secretary, không nhận capability hệ thống |
| T20 | B có sẵn và đã Accepted hoặc Declined | Giữ membership ID/role/response; A thấy response B; chỉ thông báo delegation theo kind |
| T21 | A→A, A→B→C, A↔B, hai nguồn cùng target | Conflict theo D08, không tạo auto row hoặc email dư |
| T22 | A rút/đổi delegation khi B auto | B cũ không còn source bị xoá; C mới đúng; thư B chưa gửi bị suppress; audit đủ cause |
| T23 | A rút/đổi delegation khi B direct/promoted | B giữ membership/response/quyền theo policy; không thu hồi thư mời độc lập |
| T24 | Manager xoá B đang là target | 409 HAS_DELEGATION, A/B giữ nguyên |
| T25 | Manager xoá A đang uỷ quyền | A xoá, B cleanup theo origin, không dangling target |
| T26 | Xoá/mời lại cùng UserId rồi replay DELETE/RSVP cũ | DELETE ID cũ 404; RSVP expectedParticipantId cũ 409; membership mới Pending không đổi |
| T27 | GET Full/Self với bốn response và inactive | Full totals đúng; Self không có tổng/row/delivery của người khác; delegatedTo đúng quan hệ |
| T28 | RSVP đổi khi listRevision giữ nguyên | GET vẫn trả phản hồi/summary mới, không 304 dựa trên listRevision |
| T29 | Hai POST cùng UserId/revision | Một row, một thay đổi; request sau conflict; không duplicate invitation |
| T30 | Hai RSVP cùng revision/source | Một thắng; một 409; không để lại target auto mồ côi |
| T31 | Hai nguồn đồng thời chọn B | Một uỷ quyền hiệu lực theo D08, nguồn còn lại conflict; unique constraint được kiểm trên PostgreSQL |
| T32 | Lỗi sau audit SaveChanges hoặc khi enqueue | Transaction rollback participant/revision/audit/outbox đồng thời |
| T33 | Add Published + worker bình thường | 201 với Queued; sau worker một lời mời membership được gửi, delivery Sent |
| T34 | Add Published khi SMTP tạm lỗi | API không chờ SMTP, queue bền vững; retry/Failed theo SCRUM-15, không mất membership |
| T35 | Publish/Add/Start chạy cạnh tranh | Kết quả từng thứ tự đúng mục 9; không ghi membership trái state hoặc mất lời mời của Add thành công |
| T36 | Pending email + xoá/inactive/meeting đã kết thúc | Sender suppress khi kiểm trước gửi; không dùng UserId để hợp thức hóa membership cũ |
| T37 | Thay role source delegation trước sender chạy | Lời mời target còn hợp lệ vẫn gửi, phần delegation không chứa quan hệ đã rút/sai |
| T38 | Migration trên DB rỗng và DB cũ hợp lệ | Default đúng, legacy direct=true, enum round-trip, không drop dữ liệu |
| T39 | Legacy vi phạm constraint hoặc raw SQL vi phạm mới | Preflight báo rõ/dừng; DB chặn dữ liệu mới sai, không tự sửa consent/response |
| T40 | Regression tài liệu/vote/history | Declined vẫn là participant theo ADR-0006; xoá membership không xoá audit/history/votes hoặc đổi snapshot quorum |

AC traceability:

| Tiêu chí task | Bước chính | Bằng chứng |
|---|---|---|
| Thêm theo từng cá nhân, không đơn vị | P08/P09/P17 | T09–T13; DatabaseModelTests không org unit |
| Gán Chair/Secretary/Member/Guest | P09/P10 | T08/T14/T19; API enum/type đúng |
| Tham dự/không tham dự/uỷ quyền | P12–P14 | T16–T26; người được uỷ quyền tự RSVP |
| Thư ký thấy tổng hợp | P07/P17/P20 | T27/T28; handoff Full/Self cho FE |
| Người thêm sau Published vẫn nhận thư | P15/P16/P19 | T33–T37; bằng chứng transport/mailbox test thật |

## 14. Lệnh kiểm tra khi triển khai

Chạy tại root repo trừ các lệnh FE nêu riêng. Không ghi connection string/API key vào command output hoặc file committed. Dùng DB test tách biệt; đọc docs/02-design/03-co-so-du-lieu.md cho cấu hình trước khi chạy app/EF.

```powershell
dotnet tool restore
dotnet dotnet-ef migrations add AddMeetingAttendeeResponseWorkflow --project backend/TrustCabinet.Infrastructure --startup-project backend/TrustCabinet.Web --output-dir Migrations
dotnet dotnet-ef migrations script --project backend/TrustCabinet.Infrastructure --startup-project backend/TrustCabinet.Web
dotnet build TrustCabinet.sln -warnaserror
dotnet test TrustCabinet.sln
```

Tên migration chỉ dùng nếu chưa tồn tại và các schema changes đã thống nhất owner. Nếu email migration do SCRUM-15 làm thì không thêm cùng field lần nữa. SQL script phải được review trước apply test; không chạy lại migrations add sau khi đã có migration đúng. Áp DB thử theo connection đã cấu hình cho môi trường test, không đưa credential vào plan.

Chạy hai lệnh sau với working directory frontend:

```powershell
npm run build
npm run lint
```

Integration concurrency/migration phải được cấu hình chạy trong dotnet test/CI bằng PostgreSQL riêng. Test project hiện có EF InMemory nhưng không có PostgreSQL fixture chuyên cho task: P05/P18 phải bổ sung fixture theo pattern nhóm chọn và cấu hình CI tương ứng. Không cho test skip khi thiếu DB rồi báo toàn bộ verification pass; khi môi trường thiếu dependency, ghi rõ phần chưa kiểm chứng.

Chỉ kiểm các contract/backend đã thay đổi và regression có liên quan; không thêm test UI cho tab chưa làm. Backend build phải dùng -warnaserror giống CI. Không chạy build/test trong lượt chỉ sửa plan rồi mô tả là đã kiểm chứng implementation.

## 15. Nội dung bàn giao FE/Tester

- API docs có sample Full/Self, summary, role và invitationDelivery. Chỉ dùng summary từ BE; FE không tự suy ra quorum từ Accepted hoặc nhân uỷ quyền thành hai người dự.
- Sáu wrapper và DTO typed; picker không dùng endpoint AdminUsers; luôn xử lý empty/loading/error và thông báo tiếng Việt.
- 409 phải reload và cho người dùng kiểm tra dữ liệu; không tự resubmit mutation với revision mới. Giữ thao tác đang nhập nếu hữu ích nhưng không ghi đè trạng thái mới của server.
- Trong Published, người dự có thể tự đổi RSVP theo D07; uỷ quyền hiển thị rõ “người nhận chưa xác nhận/đã từ chối/đã xác nhận”. Người được thêm tự động thấy lời mời của mình, không bị coi là đã đồng ý.
- Thư ký thấy account inactive và delivery lỗi; Sent không đồng nghĩa Accepted. Không có endpoint retry mail riêng ở task này nếu owner email chưa cung cấp; FE không dùng POST add để ép gửi lại.
- Sau mutation GET lại bảng. Có thể poll khi tab đang mở theo nhu cầu FE đã thống nhất; không yêu cầu SignalR vì AC task này không nêu realtime. Nếu dùng hub chung thì re-authorize group, phát sau commit và vẫn GET làm nguồn sự thật.
- Fixture/test instructions bao gồm account không capability, guest account, outsider và email test mailbox; không ship mật khẩu dùng chung vào source hoặc docs công khai.
- Quyền và deadline dựa trên state do BE trả; không dùng mock sampleData/AuthProvider hoặc giờ browser làm security boundary.
- Handoff nêu rõ Backend-ready khác UC Done. Lượt hiện tại chưa có API dùng được và chưa gửi thông báo cho thành viên khác.

## 16. Cách cập nhật kế hoạch khi tài liệu đến

1. Ghi commit baseline mới, tên UC/diagram/version và câu trả lời owner vào mục 2–3. D02 đã được người dùng xác nhận; D05 được người dùng yêu cầu giữ chờ, không tự chuyển sang phương án bổ sung gate.
2. Nếu D03 thay đổi: sửa ma trận state, DELETE/role/delegation cleanup, sender suppression và race Start; xem lại tác động tới voting/attendance.
3. Nếu D04 thay đổi: sửa server projection/summary, DTO nullability, FE handoff và T27; không chỉ sửa UI.
4. Khi owner chốt D05/D06: sửa policy matrix, shared gate design, HTTP precedence, P04 và T01–T08 cùng nhau; đối chiếu docs 04-phan-quyen-phia-server. Nếu cần decision thay accepted ADR thì viết ADR mới.
5. Nếu D08/D09 thay đổi: sửa unique/index, chain checks, origin/cleanup, DTO và test; không giữ partial unique “mỗi người một nguồn” khi BA đã cho phép nhiều nguồn.
6. Nếu D10 đổi: sửa API list/wrappers/steps. Nếu D11 đổi: một owner chỉnh email model/writer; cả Publish/Add sử dụng lại, cập nhật T32–T37.
7. Chạy lại kiểm tra tính nhất quán của chính plan: không mâu thuẫn field/enum/state/status; mọi P có đầu vào/đầu ra/kiểm tra; mỗi AC có test và owner dependency. Chỉ bắt đầu implementation khi workflow cho phép.

Trạng thái tại lúc lưu: **chỉ hoàn thiện kế hoạch local**. Phần tự thêm người được uỷ quyền và gửi mời đã được xác nhận. Phân quyền đang **chờ owner/BA theo lựa chọn của người dùng**; D03/D04 và các luật bổ sung còn được đánh dấu rõ để chốt. Chưa đổi code ứng dụng, chưa chạy migration, chưa gửi email, chưa commit Git.

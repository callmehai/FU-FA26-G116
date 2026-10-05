# Luồng nghiệp vụ chính (Main Business Flows)

> Tài liệu mô tả **4 Main Business Flows** và **Shared Voting Sub-flow** của TrustCabinet làm nguồn sự thật cho thiết kế swimlane / activity diagram và Report 3 §I.2.  
> **Liên kết liên quan:** [Vòng đời cuộc họp](01-vong-doi-cuoc-hop.md) · [Mô hình dữ liệu (ERD)](02-erd.md) · [Đặc tả Use Case](../01-requirements/05-use-case-specifications.md) · [Business Rules](../01-requirements/06-business-rules.md).  
> **Nguyên tắc:** Chỉ giữ **business milestone, actor hand-off, lifecycle change và decision point quan trọng**; không biến mọi Use Case thành một box.

---

# 1. Tổng quan

TrustCabinet được mô tả bằng 4 Main Business Flow:

1. **BF-01 – System Access & Administration**
2. **BF-02 – Meeting Preparation, Approval & Participant Coordination**
3. **BF-03 – Meeting Execution & Decision Making**
4. **BF-04 – Post-Meeting Processing & Formal Records**

Quan hệ tổng quát:

```text
BF-01
System Access & Administration
        ↓
Người dùng có account / role / permission phù hợp
        ↓
BF-02
Meeting Preparation, Approval & Participant Coordination
        ↓
Draft → WaitingApproval → Approved / Rejected → Published
        ↓
BF-03
Meeting Execution & Decision Making
        ↓
Published → InProgress → Ended
        ↓
BF-04
Post-Meeting Processing & Formal Records
        ↓
Audio Upload → Transcript → Transcript Review
→ AI Summary → Summary Approval / Publication
→ Minutes → Signing → Finalize / Seal
```

## Shared Voting Sub-flow

Voting không tách thành một Main Business Flow riêng vì có thể xảy ra:

- trước meeting;
- trong meeting;
- sau meeting.

Khi vote đóng và đạt quorum, business outcome chính thức là:

**Record Voting Result as Resolution (immutable)**

Trong tài liệu này, **Resolution** được hiểu là quyết nghị / kết quả chính thức của vote được gắn với meeting và không được sửa sau khi đã hình thành.

> **Scope note:** Attendance (UC-88) và Assign Audio Uploader (UC-89) được đưa vào flow để đồng bộ với Master UC Inventory đã review. Hai UC này vẫn cần PO confirmation / cập nhật Report 1 theo checklist review trước khi coi là baseline cuối cùng.

---

# 2. BF-01 – System Access & Administration

**Trigger:** Một nhân sự nội bộ cần được cấp mới hoặc thay đổi quyền truy cập TrustCabinet.

**End condition:** Người dùng có tài khoản hợp lệ, có system role / permission phù hợp và có thể truy cập các chức năng được phép.

## Swimlanes dự kiến

- **System Admin**
- **Internal User**
  - Lane tổng quát đại diện cho các actor nội bộ khi thực hiện hành vi tài khoản chung.
- **TrustCabinet**
- **Google Identity Provider**
- **Email Service**

## Mô tả luồng

System Admin tạo mới hoặc quản lý tài khoản của nhân sự nội bộ.

Admin có thể:

- tạo / cập nhật tài khoản;
- activate / deactivate tài khoản;
- gán hoặc thay đổi system role;
- tạo / sửa / xóa custom role;
- cấu hình permission của role;
- mở khóa tài khoản bị khóa;
- khởi tạo password reset.

TrustCabinet lưu các thay đổi quản trị quan trọng và ghi audit log.

Sau khi có tài khoản, Internal User truy cập TrustCabinet và chọn phương thức đăng nhập.

### Nhánh A – Email và mật khẩu

Internal User nhập email và mật khẩu.

TrustCabinet xác thực thông tin đăng nhập.

Nếu thông tin hợp lệ và tài khoản đang Active:

- người dùng được đăng nhập;
- TrustCabinet nạp role / permission hiện tại;
- người dùng được truy cập các chức năng được phép.

Nếu đăng nhập sai liên tiếp vượt ngưỡng:

- TrustCabinet chuyển tài khoản sang **Locked**;
- hệ thống từ chối các lần đăng nhập tiếp theo;
- System Admin có thể thực hiện **Unlock Account**.

### Nhánh B – Google Sign-In

Internal User chọn Google Sign-In.

Google Identity Provider xác minh Google account và trả danh tính đã xác thực về TrustCabinet.

TrustCabinet kiểm tra:

**Google account đã được liên kết với một internal account chưa?**

- **Có:** kiểm tra internal account còn Active, sau đó đăng nhập người dùng.
- **Không:** từ chối truy cập.

TrustCabinet **không tự tạo internal account mới** từ Google account.

### Password Reset

Khi System Admin khởi tạo reset password:

1. TrustCabinet tạo password-reset request.
2. Email Service gửi secure reset link đến email đã đăng ký.
3. Internal User mở link.
4. Internal User nhập mật khẩu mới.
5. TrustCabinet kiểm tra link còn hợp lệ, chưa hết hạn và chưa được sử dụng.
6. TrustCabinet lưu mật khẩu mới.
7. TrustCabinet kết thúc các active sessions cũ của tài khoản.

### Deactivate Account

Nếu System Admin deactivate một tài khoản:

- tài khoản không còn được truy cập chức năng authenticated;
- TrustCabinet kết thúc các active sessions của tài khoản đó.

BF-01 là flow nền tảng hỗ trợ tất cả business flow liên quan đến meeting.

---

# 3. BF-02 – Meeting Preparation, Approval & Participant Coordination

**Trigger:** Meeting Organizer xác định cần tổ chức một meeting mới.

**End condition:** Meeting đã **Published**, invitation đã được khởi tạo, người được mời có thể phản hồi và meeting sẵn sàng bước vào giai đoạn thực hiện.

## Swimlanes dự kiến

- **Meeting Organizer**
- **Secretary**
- **TrustCabinet**
- **Meeting Approver**
- **Internal Participant / Guest**
- **Email Service**

## Mô tả luồng

Meeting Organizer tạo meeting mới với các thông tin meeting-level cần thiết.

TrustCabinet:

- tạo meeting ở trạng thái **Draft**;
- ghi lifecycle history.

## 3.1 Gán Secretary đầu tiên

Ngay sau khi meeting được tạo, **Meeting Organizer gán Secretary đầu tiên cho meeting**.

Chỉ sau khi Secretary đã tồn tại trong meeting, Secretary mới tiếp tục các hoạt động quản lý roster, meeting role, Session và Agenda.

## 3.2 Chuẩn bị meeting

### Meeting Organizer

Meeting Organizer có thể:

- chỉnh sửa meeting-level information khi meeting ở **Draft** hoặc **Rejected**;
- cùng Secretary quản lý / upload meeting documents theo actor được phép của feature Document Management.

### Secretary

Trong các trạng thái cho phép, Secretary có thể:

- quản lý internal participants;
- mời external Guests bằng tên và email;
- gán / thay đổi meeting-specific role;
- bảo đảm meeting có Chair phù hợp;
- tạo / cập nhật / hủy Planned Meeting Sessions;
- chuẩn bị Agenda Items;
- cùng Organizer quản lý / upload meeting documents theo quyền.

### Audio Uploader

**Secretary chỉ định một Participant làm Audio Uploader** cho meeting.

Việc chỉ định này phải tồn tại trước khi post-meeting audio upload được thực hiện; nếu chưa có Audio Uploader thì BF-04 chưa thể bắt đầu nhánh audio-processing.

> Đây là UC-89 trong Inventory và hiện vẫn cần PO confirmation / cập nhật Report 1 trước khi baseline cuối cùng.

> Meeting Session và Agenda hỗ trợ việc chuẩn bị / điều hành nhưng **không phải điều kiện bắt buộc để Publish meeting**.

## 3.3 Submit for Approval

Khi meeting-level information đã sẵn sàng:

1. Meeting Organizer chọn Meeting Approver phù hợp.
2. Organizer **không được chọn chính mình làm Approver**.
3. TrustCabinet kiểm tra các thông tin bắt buộc.
4. Organizer chọn **Submit for Approval**.
5. TrustCabinet chuyển meeting sang **WaitingApproval**.
6. TrustCabinet ghi lifecycle history.
7. TrustCabinet gửi notification cho Meeting Approver.

## 3.4 Review Approval

Meeting Approver mở meeting và review.

### Reject

Nếu Approver reject:

1. Meeting Approver nhập lý do reject.
2. TrustCabinet chuyển meeting sang **Rejected**.
3. TrustCabinet ghi lifecycle history kèm lý do.
4. TrustCabinet thông báo kết quả cho Meeting Organizer.
5. Organizer chỉnh sửa meeting.
6. Organizer submit lại.
7. Flow quay lại bước Review Approval.

### Approve

Nếu Approver approve:

1. TrustCabinet chuyển meeting sang **Approved**.
2. TrustCabinet ghi lifecycle history.
3. TrustCabinet thông báo kết quả duyệt cho Meeting Organizer.

Approval **không tự động Publish meeting**.

## 3.5 Publish Meeting

Meeting Organizer chủ động chọn **Publish Meeting**.

TrustCabinet kiểm tra:

**Meeting có ít nhất một Chair và một Secretary đang hợp lệ / active không?**

- **Không:** meeting chưa được Publish; **Secretary bổ sung participant hoặc gán / thay đổi meeting role**.
- **Có:** tiếp tục Publish.

TrustCabinet:

- chuyển meeting sang **Published**;
- ghi lifecycle history;
- khởi tạo invitation / notification cho invitees.

## 3.6 Participant / Guest Response

### Internal Participant

Internal Participant mở notification hoặc meeting và có thể:

- **Accept**;
- **Decline**;
- **Delegate**, nếu trường hợp đó được phép.

### External Guest

External Guest:

1. mở secure meeting-specific invitation;
2. xác minh quyền truy cập qua email;
3. chọn **Accept** hoặc **Decline**.

Guest không có internal account và không nhận system role.

TrustCabinet lưu latest invitation response để Secretary và người có quyền theo dõi.

## 3.7 Thay đổi kế hoạch sau Publish

Sau khi meeting đã Published, Secretary vẫn có thể trong các trường hợp được phép:

- tạo thêm Planned Session;
- dời lịch Planned Session;
- hủy Planned Session.

Khi thay đổi ảnh hưởng người tham dự, TrustCabinet gửi notification cập nhật.

TrustCabinet cũng có thể gửi **pre-meeting reminder** theo cấu hình.

## 3.8 Optional – Pre-meeting Time-boxed Vote

Nếu cần biểu quyết trước meeting:

1. Chair gọi **Shared Voting Sub-flow**.
2. Eligible Participants bỏ phiếu.
3. Khi vote đóng và đạt quorum, TrustCabinet **records the Voting Result as an immutable Resolution**.
4. Resolution được lưu cùng meeting và có thể được sử dụng trong formal minutes sau này.

---

# 4. Shared Voting Sub-flow

> Sub-flow dùng chung cho BF-02, BF-03 và BF-04.

## Swimlanes dự kiến

- **Chair**
- **Eligible Participant**
- **TrustCabinet**

## Mô tả luồng

Chair cấu hình vote:

- nội dung cần biểu quyết;
- voting options;
- eligible voters;
- open / anonymous ballot;
- quorum rule;
- deadline nếu là time-boxed vote.

Chair chủ động **Open Vote**.

Eligible Participants gửi ballot.

Vote kết thúc khi:

- Chair đóng live vote; hoặc
- deadline của time-boxed vote đến hạn.

TrustCabinet kiểm tra quorum.

### Quorum đạt

TrustCabinet:

1. tổng hợp Voting Result;
2. ghi **Voting Result as Resolution**;
3. gắn Resolution với meeting / agenda context phù hợp;
4. làm Resolution trở thành **immutable**.

Business outcome trong swimlane:

**Record Voting Result as Resolution (immutable)**

### Quorum không đạt

TrustCabinet:

- ghi nhận vote không đạt quorum;
- không tạo formal Resolution từ vote đó.

Nếu cần biểu quyết lại, Chair thực hiện một vote mới theo rule của Voting feature.

---

# 5. BF-03 – Meeting Execution & Decision Making

**Trigger:** Chair quyết định bắt đầu một meeting đang ở trạng thái **Published**.

**End condition:** Meeting đã **Ended**; trạng thái Session / Agenda, official Attendance và Resolution phát sinh trong meeting đã được ghi nhận.

## Swimlanes dự kiến

- **Chair**
- **Secretary**
- **Participant / Delegate / Guest**
- **TrustCabinet**

## Mô tả luồng

Trước khi Start Meeting, TrustCabinet kiểm tra các điều kiện bắt buộc, gồm việc Chair và Secretary vẫn còn hợp lệ trong meeting.

## 5.1 Join Meeting Room – nếu áp dụng

Nếu meeting là **Online** hoặc **Hybrid**, Participant / Delegate / Guest có quyền có thể Join Online Meeting Room.

Trong room, người dùng có thể sử dụng các chức năng được phép như:

- microphone;
- camera;
- screen sharing;
- in-room chat;
- xem người đang kết nối.

Việc Join / Leave Room **không thay đổi lifecycle của Meeting hoặc Meeting Session**.

Nếu meeting là **InPerson**, nhánh Online Meeting Room được bỏ qua.

## 5.2 Start Meeting

Chair chọn **Start Meeting**.

TrustCabinet:

- chuyển Meeting **Published → InProgress**;
- ghi lifecycle history.

## 5.3 Meeting Session

Nếu meeting có Planned Meeting Session, Chair có thể Start Session.

TrustCabinet kiểm tra:

- parent Meeting đang InProgress;
- không có một Session khác đang InProgress đồng thời.

Sau đó Session chuyển sang **InProgress**.

Nếu meeting không sử dụng Meeting Session, flow có thể tiếp tục trực tiếp ở cấp Meeting.

## 5.4 Agenda Loop

Nếu Session có Agenda Items:

1. Chair chọn **Set Current Agenda Item**.
2. Participant / Delegate / Guest thảo luận theo quyền.
3. Chat, view document hoặc screen sharing có thể diễn ra song song.
4. Nếu Agenda Item cần biểu quyết, Chair gọi **Shared Voting Sub-flow**.
5. Khi vote đạt quorum, TrustCabinet **records the Voting Result as an immutable Resolution**.
6. Chair chọn **Complete Agenda Item**.
7. TrustCabinet ghi trạng thái / thời gian thực tế của Agenda Item.
8. TrustCabinet kiểm tra còn Agenda Item cần xử lý hay không:
   - **Có:** quay lại Set Current Agenda Item tiếp theo.
   - **Không:** tiếp tục End Session.

BF-03 **không giả định Secretary có chức năng note-taking trong hệ thống**. Nội dung thảo luận phục vụ AI được lấy từ audio upload sau meeting theo BF-04.

## 5.5 End Meeting Session

Chair thực hiện **End Meeting Session** đối với Session đang InProgress.

Việc End Session không tự động End Meeting.

Nếu còn Session khác cần thực hiện, Chair có thể bắt đầu Session tiếp theo.

## 5.6 Record Attendance

Trước khi hoàn tất meeting, **Secretary ghi nhận official Attendance**.

Đối với Online / Hybrid meeting:

- TrustCabinet có thể pre-fill dữ liệu từ room presence;
- Secretary kiểm tra và chỉnh trạng thái attendance chính thức.

Đối với InPerson meeting:

- Secretary ghi attendance trực tiếp.

Official Attendance:

- độc lập với invitation response;
- độc lập với online-room presence;
- là dữ liệu được dùng cho meeting record / reporting.

> Đây là UC-88 trong Inventory và hiện vẫn cần PO confirmation / cập nhật Report 1 trước khi baseline cuối cùng.

## 5.7 End Meeting

Khi Chair chọn **End Meeting**, TrustCabinet kiểm tra:

**Còn Meeting Session ở trạng thái Planned hoặc InProgress không?**

### Có Session chưa hoàn tất

Meeting chưa được End.

- Chair phải End Session đang InProgress.
- Secretary xử lý / Cancel các Planned Session còn lại theo rule cho phép.

Sau khi các Session đã được resolve, Chair thực hiện End Meeting lại.

### Không còn Session chưa hoàn tất

TrustCabinet:

- chuyển Meeting **InProgress → Ended**;
- ghi lifecycle history;
- ngừng cho phép join mới vào Online Meeting Room;
- kết thúc các room connections còn lại theo chính sách hệ thống.

Meeting, Meeting Session và Online Meeting Room là các lifecycle liên quan nhưng độc lập.

---

# 6. BF-04 – Post-Meeting Processing & Formal Records

**Trigger:** Meeting đã **Ended** và cần xử lý hồ sơ hậu kỳ.

**End condition:** Meeting có formal minutes đã hoàn tất signing / finalize / seal và hồ sơ meeting có thể được truy cập theo quyền.

## Swimlanes dự kiến

- **Participant (Audio Uploader)**
- **TrustCabinet**
- **AI / Speech Processing Provider**
- **Secretary**
- **Chair / Designated Signers**
- **Authorized User**

> `Participant (Audio Uploader)` vẫn là actor Participant; “Audio Uploader” chỉ là vai trò / điều kiện được chỉ định cho meeting, không phải actor hệ thống mới.

## 6.1 Upload Audio

Sau khi meeting Ended, TrustCabinet có thể gửi notification yêu cầu Participant đã được Secretary chỉ định làm Audio Uploader tải meeting audio lên.

TrustCabinet **không tự ghi âm meeting** và không thực hiện real-time transcription.

Participant (Audio Uploader) upload file audio đã được ghi chủ động bằng thiết bị / công cụ bên ngoài.

TrustCabinet kiểm tra:

**Audio file hợp lệ?**

### Không hợp lệ

TrustCabinet từ chối file và thông báo lý do phù hợp, ví dụ:

- sai định dạng;
- vượt giới hạn dung lượng;
- file không thể xử lý.

Participant có thể chọn file hợp lệ và upload lại.

### Hợp lệ

TrustCabinet:

- lưu audio vào Media Library của meeting;
- áp dụng access control của meeting;
- đưa file vào processing queue.

## 6.2 Speech Processing

AI / Speech Processing Provider xử lý audio:

1. Speech-to-Text.
2. Speaker attribution / diarization.
3. Tạo speaker-attributed transcript.

TrustCabinet nhận transcript draft.

### Processing successful?

- **Không:** TrustCabinet thông báo trạng thái thất bại và cho phép retry / reprocess hoặc upload lại theo rule được baseline.
- **Có:** chuyển sang Transcript Review.

## 6.3 Transcript Review

Secretary mở transcript và:

- sửa lỗi transcription;
- sửa speaker label;
- xác nhận transcript đủ chất lượng để làm nguồn cho summary.

Sau khi transcript đã được review, **Secretary chọn Generate Summary**.

AI / Speech Processing Provider sinh structured meeting summary từ transcript đã review.

Summary có thể gồm:

- discussion points;
- decisions;
- proposed action items dưới dạng nội dung văn bản.

## 6.4 Review, Approve and Publish Summary

Secretary review và chỉnh sửa draft summary.

Khi nội dung đạt yêu cầu:

1. Secretary thực hiện **Approve Summary**.
2. Sau đó Secretary chủ động thực hiện **Publish Summary** như một hành động riêng.
3. TrustCabinet công bố summary cho participants có quyền.
4. TrustCabinet gửi notification phù hợp.

Approve và Publish là hai bước riêng, không phải hệ thống tự publish ngay sau khi approve.

## 6.5 Optional – Post-meeting Time-boxed Vote

Nếu có vote sau meeting:

1. Chair gọi **Shared Voting Sub-flow**.
2. TrustCabinet chờ vote đóng / hết hạn.
3. Nếu đạt quorum, Voting Result được ghi thành **immutable Resolution**.
4. Resolution hoàn tất mới được dùng làm formal input của minutes.

Trước khi compile minutes cuối cùng, TrustCabinet kiểm tra:

**Còn vote cần đưa vào minutes nhưng chưa đóng không?**

- **Có:** chờ vote hoàn tất.
- **Không:** tiếp tục.

## 6.6 Generate Draft Minutes

Secretary thực hiện **Generate / Prepare Minutes**.

TrustCabinet compile draft minutes từ các nguồn đã chốt:

- meeting basic information;
- **approved / published summary**;
- **decisions**;
- **completed voting results / immutable Resolutions**.

Không tự động kéo thêm các nguồn mơ hồ như legacy meeting notes hoặc “các nội dung liên quan khác”.

Secretary review và chỉnh sửa draft minutes.

## 6.7 Submit for Signing

Khi draft sẵn sàng, Secretary chọn **Submit for Signing**.

TrustCabinet gửi signing request đến Chair / Designated Signers.

Mỗi signer review minutes.

### Sign

Signer thực hiện electronic signing.

TrustCabinet lưu:

- signer;
- thời điểm ký;
- signature record;
- audit trail.

### Request Revision / Refuse to Sign

Nếu signer yêu cầu sửa hoặc từ chối ký:

1. Minutes quay lại Secretary để chỉnh sửa.
2. Nếu nội dung minutes đã thay đổi sau khi một hoặc nhiều chữ ký đã được tạo, **các chữ ký hiện có phải bị vô hiệu hóa và signing cycle phải bắt đầu lại**.
3. Secretary submit bản đã sửa for signing lại.

> Nhánh này phải được đồng bộ vào UC / BR tương ứng trước khi promote chính thức vào Report 3.

## 6.8 Tất cả required signers đã ký?

- **Chưa:** tiếp tục chờ signer còn lại.
- **Rồi:** Secretary có thể thực hiện **Finalize / Seal Minutes**.

## 6.9 Finalize / Seal Minutes

Secretary chủ động thực hiện **Finalize / Seal** sau khi điều kiện signing đã đạt.

TrustCabinet:

- khóa formal minutes khỏi chỉnh sửa không hợp lệ;
- duy trì signature / audit trail;
- lưu formal minutes như meeting record chính thức.

Formal minutes sau đó có thể được Authorized User truy cập theo permission.

## 6.10 Records / Reporting

Authorized User có thể truy cập các record được phép như:

- meeting information;
- documents;
- attendance;
- transcript;
- approved / published summary;
- Voting Result / Resolution;
- sealed minutes.

Dashboard / Reporting là chức năng xuyên suốt hệ thống, không phải một bước bắt buộc ở cuối BF-04.

Nếu người dùng có reporting permission, hệ thống có thể cung cấp số liệu tổng hợp như:

- meetings held;
- attendance;
- decisions;
- resolutions;
- minutes status.

> Không đưa task tracking vào BF-04. Proposed action items chỉ là nội dung văn bản trong summary / minutes, không trở thành task có assignee, deadline và status.

> Không thêm state `MinutesIssued` hoặc `Archived` vào Main Business Flow cho tới khi lifecycle này được baseline chính thức trong SRS.

---

# 7. Quan hệ giữa 4 Main Business Flow

```text
BF-01
System Access & Administration
        ↓
User được cấp account / role / permission
        ↓
BF-02
Create Meeting
→ Assign Secretary
→ Prepare Participants / Sessions / Agenda / Documents
→ Assign Audio Uploader
→ Submit
→ Approve / Reject
→ Publish
→ Invitation / Response
→ [Optional Pre-meeting Vote → Resolution]
        ↓
BF-03
Start Meeting
→ Session / Agenda
→ Discussion
→ [Optional Live Vote → Resolution]
→ Complete Agenda
→ Resolve Sessions
→ Record Attendance
→ End Meeting
        ↓
BF-04
Upload Audio
→ Speech-to-Text / Speaker Attribution
→ Secretary Reviews Transcript
→ Generate AI Summary
→ Approve Summary
→ Publish Summary
→ [Optional Post-meeting Vote → Resolution]
→ Generate Minutes
→ Submit for Signing
→ Sign / Revision Loop
→ Finalize / Seal
```

## Approval loop của BF-02

```text
Draft
  ↓
Submit for Approval
  ↓
WaitingApproval
  ↓
Review
 ├─ Approve → Approved → Publish → Published
 └─ Reject  → Rejected
                  ↓
             Organizer edits
                  ↓
                Submit
                  └────────→ WaitingApproval
```

## Shared Voting Sub-flow

```text
Chair configures Vote
        ↓
Chair opens Vote
        ↓
Eligible Participants vote
        ↓
Chair closes Vote / Deadline reached
        ↓
Quorum met?
   ├─ No  → Record insufficient quorum → No Resolution
   └─ Yes → Record Voting Result
                 ↓
          Create/record Resolution
                 ↓
          Resolution is immutable
```

---

# 8. Quy tắc khi chuyển sang Swimlane Diagram

Không biến các hành động nhỏ sau thành box chính nếu chúng không làm thay đổi business outcome:

- Mark Notification as Read;
- Manage Microphone;
- Manage Camera;
- View Room Participants;
- Search / Filter / Pagination;
- API / technical validation chi tiết.

Diagram nên ưu tiên:

1. **Actor hand-off** – trách nhiệm chuyển từ ai sang ai.
2. **Lifecycle change** – Draft, WaitingApproval, Published, InProgress, Ended...
3. **Decision point** – Approve?, đủ Chair + Secretary?, còn Session?, quorum?, audio hợp lệ?, signer đã ký đủ?
4. **Business outcome** – Published Meeting, immutable Resolution, Published Summary, Sealed Minutes.
5. **Loop quan trọng** – Reject → Edit → Resubmit; Agenda loop; Signing revision loop.

---

# 9. Các điểm còn cần đồng bộ / xác nhận trước khi baseline cuối cùng

Các mục dưới đây không phải lỗi logic còn sót của flow, nhưng cần được đồng bộ với Inventory / Report 1 / Report 3:

1. **Attendance (UC-88)** – đã đưa vào BF-03 để khớp Inventory; vẫn cần PO confirmation / cập nhật Report 1.
2. **Assign Audio Uploader (UC-89)** – đã đưa vào BF-02 và làm prerequisite của BF-04; vẫn cần PO confirmation / cập nhật Report 1.
3. **Request Revision / Refuse to Sign** – BF-04 giữ nhánh này nhưng UC / BR signing phải bổ sung quy tắc vô hiệu hóa chữ ký cũ khi minutes bị sửa.
4. **Finalize / Seal actor** – BF-04 chốt Secretary chủ động thực hiện; Inventory UC-83 cần đồng bộ actor và bỏ TBD.
5. **Meeting status sau Ended** – chưa thêm `MinutesIssued` / `Archived` cho tới khi SRS baseline lifecycle này.
6. **AI naming** – tài liệu này dùng thống nhất **AI / Speech Processing Provider**.
7. **Reporting actor** – không tạo actor “Management”; chỉ dùng Authorized User / user có reporting permission cho tới khi actor canonical được chốt.

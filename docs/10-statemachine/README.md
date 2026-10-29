# State Machine Diagrams

Editable UML state machines with PNG previews. Draft diagrams contain transitions that still require detailed use-case confirmation.

## Ký hiệu kết thúc UML

Initial pseudostate là chấm tròn đặc. **Final State** là chấm tròn đặc có vòng tròn
bao ngoài; biểu thị hoàn tất luồng được mô hình hóa, không có nghĩa là xoá dữ liệu.
Giữ nguyên state nghiệp vụ (`Ended`, `Closed`, …), rồi nối bằng completion transition
không nhãn tới Final State. Một sơ đồ có thể có nhiều Final State, mỗi nhánh một điểm
kết thúc. Không dùng ký hiệu dấu X của terminate pseudostate.

Các điểm kết thúc được khai báo tường minh trong `state-machines.json` (`final_states`)
và được `scripts/generate-state-machines.py` giữ lại khi sinh sơ đồ:

| Sơ đồ | State nối tới Final State |
|---|---|
| SM-01 Meeting | Ended, Cancelled |
| SM-02 Agenda | Done |
| SM-03 ParticipantAssignment | Removed |
| SM-05 GuestInvitation | Expired, Revoked |
| SM-07 SpeakingRequest | Finished, Cancelled |
| SM-08 Voting | Closed, Deleted |
| SM-09 VoteConclusion | Passed, NotPassed, Deferred |
| SM-10 Recording | Failed, Deleted |
| SM-11 Transcript | Completed |
| SM-12 MeetingSummary | Published |
| SM-13 Minutes | Superseded |
| SM-14 MinutesSignature | Invalidated |
| SM-15 EmailDelivery | Sent, Failed |
| SM-16 Call | Ended, Missed |
| SM-18 NotificationRead | Read |
| SM-19 DocumentComment | Resolved |

Không bắt buộc mọi state machine phải có Final State. SM-04 InvitationResponse còn
cho đổi `Accepted ↔ Declined`; SM-17 UserAccount còn cho kích hoạt lại tài khoản.
SM-06 Attendance cũng không thêm Final State: UC-31 cho phép sửa điểm danh trong lúc
họp. Sơ đồ SM-06 hiện còn các giá trị cũ `Excused`, `Delegated` và thiếu luồng sửa;
việc đối chiếu toàn bộ nghiệp vụ này nằm ngoài chỉnh sửa ký hiệu kết thúc.

`Rejected` của Meeting và `Failed` của Transcript còn đường quay lại; `Sealed` của
Minutes còn đường tới `Superseded`; `Signed` còn có thể bị `Invalidated`, nên không
nối completion transition tại các state này. `Failed` của Recording chỉ kết thúc
lần upload theo sơ đồ draft hiện có; `Failed` của EmailDelivery kết thúc sau khi hết
số lần thử. Các điểm kết thúc của sơ đồ draft vẫn cần được xác nhận khi hoàn thiện UC.

Recording và transcript là dữ liệu cá nhân: cần đồng ý hợp lệ trước khi thu/xử lý,
và có cơ chế xoá theo yêu cầu hợp lệ hoặc hết thời hạn lưu giữ. Final State không
thay thế nghĩa vụ đồng ý và xoá dữ liệu; `Completed` chỉ kết thúc xử lý transcript.

Tham chiếu: [OMG UML — ngữ nghĩa Final State](https://issues.omg.org/issues/UML25-382).

| Diagram | Preview |
|---|---|
| [Meeting State Machine](SM-01_Meeting.drawio) | [PNG](SM-01_Meeting.png) |
| [Agenda Item State Machine](SM-02_Agenda.drawio) | [PNG](SM-02_Agenda.png) |
| [Participant Assignment State Machine](SM-03_ParticipantAssignment.drawio) | [PNG](SM-03_ParticipantAssignment.png) |
| [Invitation Response State Machine](SM-04_InvitationResponse.drawio) | [PNG](SM-04_InvitationResponse.png) |
| [Guest Invitation State Machine](SM-05_GuestInvitation.drawio) | [PNG](SM-05_GuestInvitation.png) |
| [Attendance State Machine](SM-06_Attendance.drawio) | [PNG](SM-06_Attendance.png) |
| [Speaking Request State Machine (Draft)](SM-07_SpeakingRequest.drawio) | [PNG](SM-07_SpeakingRequest.png) |
| [Voting Session State Machine](SM-08_Voting.drawio) | [PNG](SM-08_Voting.png) |
| [Vote Conclusion State Machine](SM-09_VoteConclusion.drawio) | [PNG](SM-09_VoteConclusion.png) |
| [Recording State Machine (Draft)](SM-10_Recording.drawio) | [PNG](SM-10_Recording.png) |
| [Transcript State Machine](SM-11_Transcript.drawio) | [PNG](SM-11_Transcript.png) |
| [Meeting Summary State Machine](SM-12_MeetingSummary.drawio) | [PNG](SM-12_MeetingSummary.png) |
| [Minutes Revision State Machine (Draft)](SM-13_Minutes.drawio) | [PNG](SM-13_Minutes.png) |
| [Minutes Signature State Machine](SM-14_MinutesSignature.drawio) | [PNG](SM-14_MinutesSignature.png) |
| [Email Delivery State Machine](SM-15_EmailDelivery.drawio) | [PNG](SM-15_EmailDelivery.png) |
| [Direct Call State Machine (Draft)](SM-16_Call.drawio) | [PNG](SM-16_Call.png) |
| [User Account State Machine](SM-17_UserAccount.drawio) | [PNG](SM-17_UserAccount.png) |
| [Notification Read Status State Machine](SM-18_NotificationRead.drawio) | [PNG](SM-18_NotificationRead.png) |
| [Document Comment State Machine (Draft)](SM-19_DocumentComment.drawio) | [PNG](SM-19_DocumentComment.png) |

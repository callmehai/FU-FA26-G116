# Sequence Diagram — Quản lý cuộc họp (FE-04)

Thư mục chứa source SequenceDiagram.org (`.txt`) và ảnh PNG cùng tên cho các UC-13 → UC-21, UC-22.

| Use Case | Source |
|---|---|
| UC-13 Create Meeting | [UC13_CreateMeeting.txt](UC13_CreateMeeting.txt) |
| UC-14 Update Meeting | [UC14_UpdateMeeting.txt](UC14_UpdateMeeting.txt) |
| UC-15 View Meeting Details | [UC15_ViewMeetingDetails.txt](UC15_ViewMeetingDetails.txt) |
| UC-16 View Meeting List | [UC16_ViewMeetingList.txt](UC16_ViewMeetingList.txt) |
| UC-17 Submit Meeting for Approval | [UC17_SubmitMeetingForApproval.txt](UC17_SubmitMeetingForApproval.txt) |
| UC-18 Review Meeting Approval | [UC18_ReviewMeetingApproval.txt](UC18_ReviewMeetingApproval.txt) |
| UC-19 Publish Meeting | [UC19_PublishMeeting.txt](UC19_PublishMeeting.txt) |
| UC-20 Start Meeting | [UC20_StartMeeting.txt](UC20_StartMeeting.txt) |
| UC-21 End Meeting | [UC21_EndMeeting.txt](UC21_EndMeeting.txt) |
| UC-22 Cancel Meeting | [UC22_CancelMeeting.txt](UC22_CancelMeeting.txt) |
| SYS Auto-End Forgotten Meeting (bản nháp chờ duyệt, không gắn số UC — ngoại lệ theo note 46 §2) | [SYS_AutoEndForgottenMeeting.txt](SYS_AutoEndForgottenMeeting.txt) |

Các file Mermaid `SubmitMeetingForApproval.mmd`, `ReviewMeetingApproval.mmd`, `PublishMeeting.mmd` (UC-17, UC-18, UC-19) là bản đối chiếu mã thông báo; `MessageCodeCatalogueTests` đọc chúng để kiểm tra mã và HTTP status khớp [danh mục mã thông báo](../../03-api/00-danh-muc-ma-thong-bao.md).

Đọc [convention sequence chung](../README.md), [đặc tả Use Case](../../01-requirements/05-use-case-specifications.md),
[Business Rules](../../01-requirements/06-business-rules.md) và các ADR liên quan trước khi sửa. Luồng
nghiệp vụ không có Meeting Session; lịch và vòng đời thuộc trực tiếp Meeting theo [ADR-0013](../../05-decisions/0013-mot-cuoc-hop-la-mot-buoi-hop.md).

Sau khi sửa source, xuất lại ảnh bằng `node scripts/export-diagrams.js docs/06-sequencediagram/meeting-management`.
Cập nhật đúng tab UC trong file draw.io gộp theo hướng dẫn ở README chung.

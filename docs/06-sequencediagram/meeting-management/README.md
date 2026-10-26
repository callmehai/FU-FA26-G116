# Sequence Diagram — Quản lý cuộc họp (FE-04)

Thư mục chứa source SequenceDiagram.org (`.txt`) và ảnh PNG cùng tên cho các UC-14 → UC-22 và UC-75.

| Use Case | Source |
|---|---|
| UC-14 Create Meeting | [UC14_CreateMeeting.txt](UC14_CreateMeeting.txt) |
| UC-15 Update Meeting | [UC15_UpdateMeeting.txt](UC15_UpdateMeeting.txt) |
| UC-16 View Meeting Details | [UC16_ViewMeetingDetails.txt](UC16_ViewMeetingDetails.txt) |
| UC-17 View Meeting List | [UC17_ViewMeetingList.txt](UC17_ViewMeetingList.txt) |
| UC-18 Submit Meeting for Approval | [UC18_SubmitMeetingForApproval.txt](UC18_SubmitMeetingForApproval.txt) |
| UC-19 Review Meeting Approval | [UC19_ReviewMeetingApproval.txt](UC19_ReviewMeetingApproval.txt) |
| UC-20 Publish Meeting | [UC20_PublishMeeting.txt](UC20_PublishMeeting.txt) |
| UC-21 Start Meeting | [UC21_StartMeeting.txt](UC21_StartMeeting.txt) |
| UC-22 End Meeting | [UC22_EndMeeting.txt](UC22_EndMeeting.txt) |
| UC-75 Cancel Meeting | [UC75_CancelMeeting.txt](UC75_CancelMeeting.txt) |

Các file Mermaid `SubmitMeetingForApproval.mmd`, `ReviewMeetingApproval.mmd`, `PublishMeeting.mmd` (UC-18, UC-19, UC-20) là bản đối chiếu mã thông báo; `MessageCodeCatalogueTests` đọc chúng để kiểm tra mã và HTTP status khớp [danh mục mã thông báo](../../03-api/00-danh-muc-ma-thong-bao.md).

Đọc [convention sequence chung](../README.md), [đặc tả Use Case](../../01-requirements/05-use-case-specifications.md),
[Business Rules](../../01-requirements/06-business-rules.md) và các ADR liên quan trước khi sửa. Luồng
nghiệp vụ không có Meeting Session; lịch và vòng đời thuộc trực tiếp Meeting theo [ADR-0013](../../05-decisions/0013-mot-cuoc-hop-la-mot-buoi-hop.md).

Sau khi sửa source, xuất lại ảnh bằng `node scripts/export-diagrams.js docs/06-sequencediagram/meeting-management`.
Cập nhật đúng tab UC trong file draw.io gộp theo hướng dẫn ở README chung.

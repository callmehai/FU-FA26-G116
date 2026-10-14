# Sequence Diagram Convention — TrustCabinet

> Áp dụng cho các Sequence Diagram viết bằng cú pháp của **sequencediagram.org** trong dự án TrustCabinet.

## Cấu trúc file và cách đồng bộ

- Mỗi Use Case có source `.txt` và ảnh `.png` cùng tên trong thư mục theo Feature. Nội dung `.txt` là
  bản cần sửa; sau khi sửa, xuất lại PNG tương ứng bằng `node scripts/export-diagrams.js <đường-dẫn-file-hoặc-thư-mục>`.
- [`TrustCabinet_Sequence_Diagrams.drawio`](TrustCabinet_Sequence_Diagrams.drawio) là file draw.io gộp
  các sequence thành từng tab, dùng để duyệt và chỉnh sửa sơ đồ native. Khi thay đổi một source, cập
  nhật đúng PNG và tab UC tương ứng trong file gộp; giữ nguyên các tab khác. Ảnh xuất từ các tab UC
  của file gộp được đồng bộ riêng trong [`drawio-png/`](drawio-png/README.md).
- Đọc README của thư mục Feature trước khi sửa diagram: ở đó có danh sách UC và các lưu ý riêng của
  nhóm đó. Bảng UC ở cuối trang là mục lục tổng thể.
- Quy tắc nghiệp vụ và cách vẽ nằm ở các phần bên dưới. Khi thay đổi `.drawio`, tuân theo
  [DRAWIO_EDITING_RULES.md](../../DRAWIO_EDITING_RULES.md) và render kiểm tra trước khi bàn giao.

### Style chung của file draw.io gộp

- Dùng actor và database đúng loại participant; căn giữa tiêu đề UC.
- Giữ palette mặc định, message và self-message dùng Trebuchet MS, Verdana, Arial, sans-serif ở cỡ
  13px. Nhãn `alt`, `opt`, `loop` và guard cũng dùng cùng font/cỡ.
- Nhãn message có nền sáng để connector không đè lên chữ; không mirror participant ở cuối diagram.
- Guard trong khung `alt`/`opt` căn trái và bắt đầu ngay sau marker; không căn giữa theo chiều rộng khung.
- Biểu tượng actor dùng nét mảnh; participant khác giữ shape và style mặc định của loại tương ứng.

## 1. Mục tiêu

Sequence Diagram thường thể hiện **một lần thực thi của một Use Case/business action**, bám theo Use Case Specification trong Report 3. Riêng UC-04 dùng một sequence đầu-cuối để thể hiện các tương tác liên tiếp cần thiết để hoàn thành Guest access.

Không biến Sequence Diagram thành mô tả toàn bộ thao tác UI trước khi người dùng thực hiện action chính.

## 2. Actor → System

### 2.1. Chỉ có một request từ Actor vào hệ thống

Thông thường, mỗi Sequence Diagram chỉ có **một mũi tên Actor → Boundary ở đầu luồng**. Ngoại lệ UC-04 có nhiều tương tác Guest → Boundary theo Normal Flow.

Ví dụ:

```text
Secretary->MeetingPage: Submit agenda item information
```

hoặc:

```text
Chair->MeetingPage: Start Meeting
```

Với thao tác cần xác nhận, sequence bắt đầu tại action xác nhận cuối cùng:

```text
Secretary->MeetingPage: Confirm Delete Agenda Item
```

Không cần vẽ các bước UI trước đó như mở trang, hiển thị form, nhập dữ liệu, mở dialog xác nhận.

### 2.2. Không gửi request lần hai trong cùng diagram

Nếu validation thất bại và UC nói người dùng sửa rồi submit lại, diagram chỉ thể hiện response lỗi.

Không vẽ:

```text
Page-->Actor: Show validation error
Actor->Page: Correct and resubmit
```

Lần resubmit được xem là **một execution mới của cùng Sequence Diagram**.

## 3. Lifeline convention

Thứ tự lifeline ưu tiên:

```text
Actor
→ Boundary/Page
→ Controller
→ Service
→ Repository
→ Database
```

Ví dụ:

```text
actor "Secretary" as Secretary
boundary "Meeting Management Page" as MeetingPage
control ":Agenda Controller" as AgendaController
control ":Agenda Service" as AgendaService
participant ":Meeting Repository" as MeetingRepository
participant ":Agenda Item Repository" as AgendaItemRepository
database ":Database" as Database
```

### Naming

- Actor: không có dấu `:`
- Boundary/Page: tên màn hình hoặc component cấp màn hình
- Controller: bắt đầu bằng `:`
- Service: bắt đầu bằng `:`
- Repository: bắt đầu bằng `:`
- Database: bắt đầu bằng `:`

## 4. Trách nhiệm của từng layer

**Boundary / Page**
- Nhận action từ Actor.
- Gửi request vào Controller.
- Nhận kết quả từ Controller.
- Hiển thị message/result cho Actor.
- Không chứa business rule.

**Controller**
- Nhận request từ Boundary.
- Gọi Service.
- Trả kết quả về Boundary.
- Không trực tiếp truy cập Database.

**Service**
- Điều phối business flow.
- Kiểm tra Business Rules.
- Gọi Repository cần thiết.
- Quyết định success / alternative / exception branch.

**Repository**
- Đọc hoặc ghi entity.
- Giao tiếp với Database.
- Không hiển thị message cho Actor.

**Database**
- Chỉ biểu diễn persistence cần thiết cho flow.

## 5. Message convention

### Request

Dùng mũi tên liền:

```text
MeetingPage->AgendaController: Create Agenda Item
AgendaController->AgendaService: createAgendaItem(meetingId, secretaryId, agendaInfo, documentIds)
AgendaService->MeetingRepository: Get selected Meeting
MeetingRepository->Database: Retrieve meeting
```

### Return

Dùng mũi tên nét đứt:

```text
Database-->MeetingRepository: Meeting
MeetingRepository-->AgendaService: Meeting
AgendaService-->AgendaController: Agenda Item created successfully
```

### Message hiển thị cho Actor

Phải ghi rõ **MSG code + full message content**.

Không dùng:

```text
MeetingPage-->Secretary: Show MSG68
```

Dùng:

```text
MeetingPage-->Secretary: MSG68 - Unable to create the agenda item. Please try again.
```

Success message cũng theo cùng convention:

```text
MeetingPage-->Secretary: MSG60 - Agenda item created successfully.
```

## 6. Activation bar convention

Khi lifeline bắt đầu xử lý:

```text
activate MeetingController
```

Khi xử lý xong:

```text
deactivate MeetingController
```

Pattern:

```text
MeetingPage->MeetingController: Request
activate MeetingController

MeetingController->MeetingService: Process request
activate MeetingService

...

MeetingService-->MeetingController: Result
deactivate MeetingService

MeetingController-->MeetingPage: Result
deactivate MeetingController
```

Boundary được activate sau request đầu tiên từ Actor và deactivate ở cuối diagram:

```text
Actor->Page: Business action
activate Page

...

deactivate Page
```

## 7. Business Rule convention

Ghi BR ngay tại message nơi rule được kiểm tra hoặc áp dụng.

```text
AgendaService->AgendaService: Validate required title, positive planned duration\nand selected documents (BR-66)
```

hoặc:

```text
AgendaService->AgendaItemRepository: Create Agenda Item with Pending status\nat the end of the Meeting agenda (BR-64, BR-67)
```

Không tạo lifeline riêng cho Business Rule.

## 8. Alternative / Exception Flow

### 8.1. Dùng `alt` cho các nhánh kết quả khác nhau

```text
alt Agenda Item information is invalid - EX-1
    ...
else Agenda Item information is valid
    ...
end
```

Exception branch phải khớp Exception Flow trong Report 3.

### 8.2. Không dùng `loop` để mô tả resubmit

Không dùng:

```text
loop Invalid information
    System-->User: Error
    User->System: Resubmit
end
```

Resubmit là một execution mới.

### 8.3. Alternative Flow chỉ dùng khi Use Case vẫn thành công

Ví dụ:

```text
alt Meeting has no Agenda Items - AF-1
    ...
else Meeting has Agenda Items
    ...
end
```

Nếu mục tiêu Use Case không đạt thì dùng Exception Flow.

## 9. Self-call

Service có thể self-call để biểu diễn validation/business decision ngắn gọn:

```text
MeetingService->MeetingService: Validate meeting information\nand planned time (BR-24, BR-62)
```

Không lạm dụng self-call để mô tả chi tiết implementation.

## 10. Không đưa technical detail ngoài phạm vi UC

Business Sequence Diagram không tự thêm:

- LiveKit
- SignalR
- HTTP status
- middleware
- framework internals
- ORM internals
- message bus
- background job

trừ khi chính UC hoặc thiết kế được duyệt yêu cầu participant đó.

`VotingSession` là một lượt biểu quyết; `CallSession` là một lần chạy phòng họp LiveKit (kỹ thuật). TrustCabinet **không có Meeting Session** — lịch, agenda, biểu quyết thuộc thẳng Meeting ([ADR-0013](../05-decisions/0013-mot-cuoc-hop-la-mot-buoi-hop.md)). Không vẽ lifeline Meeting Session.

## 11. Source of truth

Khi vẽ Sequence Diagram, ưu tiên:

1. Use Case Specification trong Report 3
2. [Danh mục Business Rules](../01-requirements/06-business-rules.md) được UC tham chiếu
3. System Messages được UC tham chiếu
4. Thiết kế/domain đã được nhóm chấp nhận
5. Code hiện tại nếu cần phản ánh cấu trúc lớp

Không tự thêm business behavior không có trong các nguồn trên.
Business Rule ghi ngay cạnh message nơi rule được kiểm tra/áp dụng; dùng đúng ID và ý nghĩa trong danh mục chung.

## 12. Template chuẩn

```text
title UC-XX - Use Case Name

actor "Actor" as Actor
boundary "Feature Management Page" as FeaturePage
control ":Feature Controller" as FeatureController
control ":Feature Service" as FeatureService
participant ":Entity Repository" as EntityRepository
database ":Database" as Database

Actor->FeaturePage: Submit business action
activate FeaturePage

FeaturePage->FeatureController: Execute Use Case
activate FeatureController

FeatureController->FeatureService: executeUseCase(input)
activate FeatureService

FeatureService->EntityRepository: Get required entity
activate EntityRepository

EntityRepository->Database: Retrieve entity
activate Database

Database-->EntityRepository: Entity
deactivate Database

EntityRepository-->FeatureService: Entity
deactivate EntityRepository

FeatureService->FeatureService: Validate business rules

alt Business exception - EX-1
    FeatureService-->FeatureController: MSGxx - Full exception message
    deactivate FeatureService

    FeatureController-->FeaturePage: Return failure
    deactivate FeatureController

    FeaturePage-->Actor: MSGxx - Full exception message

else Request is valid
    FeatureService->EntityRepository: Save business change
    activate EntityRepository

    EntityRepository->Database: Insert / Update / Delete entity
    activate Database

    Database-->EntityRepository: Operation completed
    deactivate Database

    EntityRepository-->FeatureService: Updated entity
    deactivate EntityRepository

    FeatureService-->FeatureController: Use Case completed successfully
    deactivate FeatureService

    FeatureController-->FeaturePage: MSGxx - Full success message
    deactivate FeatureController

    FeaturePage-->Actor: MSGxx - Full success message
end

deactivate FeaturePage
```

## 13. Reference — UC-23a Create Agenda Item

Source: [UC23a_CreateAgendaItem.txt](agenda-management/UC23a_CreateAgendaItem.txt)

```text
title UC-23a - Create Agenda Item

actor "Secretary" as Secretary
boundary "Meeting Management Page" as MeetingPage
control ":Agenda Controller" as AgendaController
control ":Agenda Service" as AgendaService
participant ":Meeting Repository" as MeetingRepository
participant ":Meeting Participant Repository" as MeetingParticipantRepository
participant ":Agenda Item Repository" as AgendaItemRepository
participant ":Document Repository" as DocumentRepository
database ":Database" as Database

Secretary->MeetingPage: Submit agenda item information
activate MeetingPage

MeetingPage->AgendaController: Create Agenda Item
activate AgendaController

AgendaController->AgendaService: createAgendaItem(meetingId, secretaryId, agendaInfo, documentIds)
activate AgendaService


AgendaService->MeetingRepository: Get selected Meeting
activate MeetingRepository
MeetingRepository->Database: Retrieve Meeting
activate Database
Database-->MeetingRepository: Meeting
deactivate Database
MeetingRepository-->AgendaService: Meeting
deactivate MeetingRepository

AgendaService->MeetingParticipantRepository: Verify Secretary assignment to the Meeting (BR-65)
activate MeetingParticipantRepository
MeetingParticipantRepository->Database: Find Secretary assignment
activate Database
Database-->MeetingParticipantRepository: Assignment result
deactivate Database
MeetingParticipantRepository-->AgendaService: Assignment result
deactivate MeetingParticipantRepository

opt Meeting documents are selected
    AgendaService->DocumentRepository: Get selected documents available to the Meeting (BR-66)
    activate DocumentRepository
    DocumentRepository->Database: Retrieve selected meeting documents
    activate Database
    Database-->DocumentRepository: Available selected documents
    deactivate Database
    DocumentRepository-->AgendaService: Available selected documents
    deactivate DocumentRepository
end

AgendaService->AgendaService: Validate required title, positive planned duration,\noptional presenter and description, and selected documents (BR-66)

alt Invalid Agenda Item information - EX-1
    AgendaService-->AgendaController: MSG66 - Invalid agenda item information.\nApplicable field message(s)
    deactivate AgendaService
    AgendaController-->MeetingPage: MSG66 - Invalid agenda item information.\nApplicable field message(s)
    deactivate AgendaController
    MeetingPage-->Secretary: MSG66 - Invalid agenda item information.\nApplicable field message(s):\nAgenda item title is required.\nPlanned duration must be greater than 0.\nOne or more selected documents are not available to this meeting.

else Agenda Item information is valid
    AgendaService->AgendaService: Verify assigned Secretary and Meeting status Draft, WaitingApproval,\nRejected, Approved, Published or InProgress (BR-64, BR-65, BR-68, BR-74)

    alt Agenda can no longer be modified - EX-2
        AgendaService-->AgendaController: MSG67 - The agenda for this meeting can no longer be modified.
        deactivate AgendaService
        AgendaController-->MeetingPage: MSG67 - The agenda for this meeting can no longer be modified.
        deactivate AgendaController
        MeetingPage-->Secretary: MSG67 - The agenda for this meeting can no longer be modified.\nAgenda Item is not created

    else Agenda modification is permitted
        AgendaService->AgendaItemRepository: Get Agenda Items of selected Meeting
        activate AgendaItemRepository
        AgendaItemRepository->Database: Retrieve Meeting agenda in explicit order
        activate Database
        Database-->AgendaItemRepository: Meeting Agenda Item list
        deactivate Database
        AgendaItemRepository-->AgendaService: Meeting Agenda Item list
        deactivate AgendaItemRepository

        AgendaService->AgendaItemRepository: Create Agenda Item for selected Meeting\nwith Pending status at the end of the agenda\nand associate selected meeting documents (BR-64, BR-67)
        activate AgendaItemRepository
        AgendaItemRepository->Database: Insert Agenda Item and document associations
        activate Database
        Database-->AgendaItemRepository: Change saved / Save failed without creating Agenda Item
        deactivate Database
        AgendaItemRepository-->AgendaService: Created Agenda Item / Save failed
        deactivate AgendaItemRepository

        alt Agenda Item cannot be created - EX-3
            AgendaService-->AgendaController: MSG68 - Unable to create the agenda item. Please try again.
            deactivate AgendaService
            AgendaController-->MeetingPage: MSG68 - Unable to create the agenda item. Please try again.
            deactivate AgendaController
            MeetingPage-->Secretary: MSG68 - Unable to create the agenda item. Please try again.\nAgenda Item is not created

        else Agenda Item is created
            AgendaService-->AgendaController: Agenda Item created successfully
            deactivate AgendaService
            AgendaController-->MeetingPage: MSG60 - Agenda item created successfully.\nUpdated Meeting agenda
            deactivate AgendaController
            MeetingPage-->Secretary: MSG60 - Agenda item created successfully.\nShow updated Meeting agenda
        end
    end
end

deactivate MeetingPage
```

## 14. Checklist trước khi chốt

- [ ] Title đúng UC ID và Use Case Name.
- [ ] Actor đúng Primary Actor trong Report 3.
- [ ] Chỉ có **một Actor → Boundary request** trong toàn diagram, trừ sequence đầu-cuối UC-04.
- [ ] Không vẽ form-open / form-display / user-resubmit không cần thiết.
- [ ] Controller / Service / Repository có dấu `:` ở đầu tên.
- [ ] Flow đi theo `Boundary → Controller → Service → Repository → Database`.
- [ ] Business Rules được gắn đúng nơi kiểm tra.
- [ ] Alternative Flow vẫn đạt success postcondition.
- [ ] Exception Flow không đạt mục tiêu UC.
- [ ] Không dùng `loop` để mô tả resubmit.
- [ ] Mọi `MSGxx` hiển thị cho Actor đều có **full message content**.
- [ ] Success message cũng có mã + nội dung đầy đủ.
- [ ] Activation/deactivation không bị treo hoặc chồng sai.
- [ ] Không có lifeline Meeting Session; không trộn `CallSession` (kỹ thuật) với nghiệp vụ cuộc họp.
- [ ] Không tự thêm hành vi không có trong Report 3 / BR / MSG / design đã duyệt.

## 15. Tên file và số UC

- Tên file: `UC<số UC>_<TênUseCase>.txt` + `.png` cùng tên, ví dụ `UC23a_CreateAgendaItem.txt`. Dòng `title` ghi `UC-23a - Create Agenda Item`.
- Số UC theo [Master Use Case Inventory](../01-requirements/00-master-use-case-inventory.md) (UC-01 → UC-70, baseline 10/10/2026).
  Ngày 10/10/2026 toàn bộ file đã đổi tên theo số mới; bảng đổi số cũ → mới ở §5 của inventory.
- Thư mục `meeting-session-management/` đã bị xoá cùng Meeting Session (ADR-0013).
- BR trong các sơ đồ vẽ trước 10/10/2026 có chỗ dùng số BR của bản dự thảo cũ. Khi chạm vào một sơ đồ, đối chiếu lại từng
  `BR-xx` với [06-business-rules.md](../01-requirements/06-business-rules.md). Các sơ đồ Meeting (UC-20 → UC-22), Agenda
  (UC-23a → UC-26) và Participant (UC-28a → UC-30) đã được đối chiếu trong đợt 10/10/2026.

| Thư mục | UC |
|---|---|
| [auth/](auth/README.md) | UC-01 → UC-04 |
| [user-management/](user-management/README.md) | UC-05, UC-06a → UC-06c, UC-07, UC-08, UC-09 |
| [role-management/](role-management/README.md) | UC-10, UC-11, UC-12a → UC-12c, UC-13 |
| [meeting-management/](meeting-management/README.md) | UC-14 → UC-22 |
| [agenda-management/](agenda-management/README.md) | UC-23a → UC-23d, UC-24 → UC-26 |
| [meeting-participant-management/](meeting-participant-management/README.md) | UC-27, UC-28a → UC-28c, UC-29, UC-30, UC-31 |
| [notification-management/](notification-management/README.md) | UC-66, UC-67a, UC-67b, UC-68 |

Xuất PNG: `node scripts/export-diagrams.js docs/06-sequencediagram/<thư mục hoặc file>` (cần truy cập `sequencediagram.org`).

Đồng bộ ảnh `.drawio.png` từ 44 tab UC của file draw.io gộp (bỏ qua tab index):
`python scripts/sync_sequence_drawio_png.py` (cần cài Draw.io; có thể đặt `DRAWIO_EXECUTABLE` nếu CLI chưa có trong PATH).

UC09 và UC13 bổ sung ngày 13/10/2026: source + PNG theo đặc tả hiện hành. Đây là thiết kế đích; unlock/lock store và audit-read endpoint chưa có code. Không tự cấp message codes mới. Quy tắc lưu lock/audit nguyên tử trong UC09 là đề xuất kỹ thuật để giữ audit evidence nhất quán khi update thất bại.

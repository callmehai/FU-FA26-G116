# Sequence Diagram Convention — TrustCabinet

> Áp dụng cho các Sequence Diagram viết bằng cú pháp của **sequencediagram.org** trong dự án TrustCabinet.

## 1. Mục tiêu

Sequence Diagram thể hiện **một lần thực thi của một Use Case/business action**, bám theo Use Case Specification trong Report 3.

Không biến Sequence Diagram thành mô tả toàn bộ thao tác UI trước khi người dùng thực hiện action chính.

## 2. Actor → System

### 2.1. Chỉ có một request từ Actor vào hệ thống

Mỗi Sequence Diagram chỉ có **một mũi tên Actor → Boundary ở đầu luồng**.

Ví dụ:

```text
Secretary->MeetingPage: Submit session information
```

hoặc:

```text
Chair->MeetingPage: Start Meeting Session
```

Với thao tác cần xác nhận, sequence bắt đầu tại action xác nhận cuối cùng:

```text
Secretary->MeetingPage: Confirm Delete Session
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
control ":Meeting Controller" as MeetingController
control ":Meeting Service" as MeetingService
participant ":Meeting Repository" as MeetingRepository
participant ":Meeting Session Repository" as MeetingSessionRepository
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
MeetingPage->MeetingController: Create Meeting Session
MeetingController->MeetingService: createMeetingSession(meetingId, sessionInfo)
MeetingService->MeetingRepository: Get selected meeting
MeetingRepository->Database: Retrieve meeting
```

### Return

Dùng mũi tên nét đứt:

```text
Database-->MeetingRepository: Meeting
MeetingRepository-->MeetingService: Meeting
MeetingService-->MeetingController: Meeting Session created successfully
```

### Message hiển thị cho Actor

Phải ghi rõ **MSG code + full message content**.

Không dùng:

```text
MeetingPage-->Secretary: Show MSG54
```

Dùng:

```text
MeetingPage-->Secretary: MSG54 - Meeting sessions can no longer\nbe added to this meeting
```

Success message cũng theo cùng convention:

```text
MeetingPage-->Secretary: MSG48 - Meeting session created successfully
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
MeetingService->MeetingService: Validate session information\nand planned time (BR-62)
```

hoặc:

```text
MeetingService->MeetingSessionRepository: Create Meeting Session\nwith Planned status (BR-54, BR-58)
```

Không tạo lifeline riêng cho Business Rule.

## 8. Alternative / Exception Flow

### 8.1. Dùng `alt` cho các nhánh kết quả khác nhau

```text
alt Session information is invalid - EX-1
    ...
else Session information is valid
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
alt Other Meeting Sessions remain - AF-1
    ...
else No unfinished Meeting Sessions remain
    ...
end
```

Nếu mục tiêu Use Case không đạt thì dùng Exception Flow.

## 9. Self-call

Service có thể self-call để biểu diễn validation/business decision ngắn gọn:

```text
MeetingService->MeetingService: Validate session information\nand planned time (BR-62)
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

`MeetingSession` là business session. `CallSession` là technical LiveKit room run. Không trộn hai khái niệm trong các UC Meeting Session.

## 11. Source of truth

Khi vẽ Sequence Diagram, ưu tiên:

1. Use Case Specification trong Report 3
2. [Danh mục Business Rules BR-01 → BR-77](../01-requirements/06-business-rules.md) được UC tham chiếu
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

## 13. Reference — UC-26 Create Meeting Session

```text
title UC-26 - Create Meeting Session

actor "Secretary" as Secretary
boundary "Meeting Management Page" as MeetingPage
control ":Meeting Controller" as MeetingController
control ":Meeting Service" as MeetingService
participant ":Meeting Repository" as MeetingRepository
participant ":Meeting Session Repository" as MeetingSessionRepository
database ":Database" as Database

Secretary->MeetingPage: Submit session information
activate MeetingPage

MeetingPage->MeetingController: Create Meeting Session
activate MeetingController

MeetingController->MeetingService: createMeetingSession(meetingId, sessionInfo)
activate MeetingService

MeetingService->MeetingRepository: Get selected meeting
activate MeetingRepository

MeetingRepository->Database: Retrieve meeting
activate Database

Database-->MeetingRepository: Meeting
deactivate Database

MeetingRepository-->MeetingService: Meeting
deactivate MeetingRepository

MeetingService->MeetingService: Validate session information\nand planned time (BR-62)

alt Session information is invalid - EX-1
    MeetingService-->MeetingController: MSG53 - Invalid meeting session information
    deactivate MeetingService

    MeetingController-->MeetingPage: Return validation errors
    deactivate MeetingController

    MeetingPage-->Secretary: MSG53 - Invalid meeting session information

else Session information is valid

    alt Meeting is not open for session planning - EX-2
        MeetingService-->MeetingController: MSG54 - Meeting sessions can no longer\nbe added to this meeting
        deactivate MeetingService

        MeetingController-->MeetingPage: Return session creation failure
        deactivate MeetingController

        MeetingPage-->Secretary: MSG54 - Meeting sessions can no longer\nbe added to this meeting

    else Meeting is Draft, Rejected, or Approved
        MeetingService->MeetingSessionRepository: Create Meeting Session\nwith Planned status (BR-54, BR-58)
        activate MeetingSessionRepository

        MeetingSessionRepository->Database: Insert Meeting Session
        activate Database

        Database-->MeetingSessionRepository: Meeting Session created
        deactivate Database

        MeetingSessionRepository-->MeetingService: Created Meeting Session
        deactivate MeetingSessionRepository

        MeetingService-->MeetingController: Meeting Session created successfully
        deactivate MeetingService

        MeetingController-->MeetingPage: MSG48 - Meeting session created successfully
        deactivate MeetingController

        MeetingPage-->Secretary: MSG48 - Meeting session created successfully
    end
end

deactivate MeetingPage
```

## 14. Checklist trước khi chốt

- [ ] Title đúng UC ID và Use Case Name.
- [ ] Actor đúng Primary Actor trong Report 3.
- [ ] Chỉ có **một Actor → Boundary request** trong toàn diagram.
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
- [ ] Không trộn technical `CallSession` với business `MeetingSession`.
- [ ] Không tự thêm hành vi không có trong Report 3 / BR / MSG / design đã duyệt.

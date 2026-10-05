# Mô hình dữ liệu (ERD)

Tài liệu này mô tả **mô hình dữ liệu khái niệm/logic mục tiêu** của TrustCabinet để làm nguồn cho Report 3, không phải bản trích xuất schema hiện tại. Mô hình đích có đúng **30 thực thể nghiệp vụ/dữ liệu**. Hệ thống được triển khai bằng EF Core code-first và PostgreSQL; thay đổi ERD không tự thay đổi entity, cấu hình, schema hay migration.

Hiện thực: EF Core 10 code-first, PostgreSQL qua Npgsql
([ADR-0003](../05-decisions/0003-chon-postgresql-thay-cho-sql-server.md)). Entity ở
[backend/TrustCabinet.Domain/Entities/](../../backend/TrustCabinet.Domain/Entities/), cấu hình bảng
ở [backend/TrustCabinet.Infrastructure/Configurations/](../../backend/TrustCabinet.Infrastructure/Configurations/).
Cách chạy: [03-co-so-du-lieu.md](03-co-so-du-lieu.md).

Thứ tự nguồn sự thật khi có khác biệt: **Requirement/Use Case/Business Rules mới nhất → ADR đã chốt và còn phù hợp → code hiện tại để xác định trạng thái triển khai**. Vì vậy code có thể cần migration hoặc tái cấu trúc để đạt mô hình đích. Xung đột giữa mô hình đích và ADR được nêu ở cuối tài liệu; phạm vi này không sửa ADR.

Vòng đời Meeting là `Draft → WaitingApproval → Approved | Rejected → Published → InProgress → Ended → MinutesIssued → Archived`; Meeting bị từ chối có thể được gửi duyệt lại. Lịch dự kiến chỉ thuộc MeetingSession.

## Trạng thái triển khai

Các trạng thái dưới đây nói về code/schema hiện tại. `NOT IMPLEMENTED` là entity của mô hình đích chưa tồn tại trong domain/schema; không được hiểu là bảng hiện có.

| Entity | Mục đích trong mô hình đích | Trạng thái code | Thay đổi cần thiết |
|---|---|---|---|
| User | Tài khoản nội bộ | IMPLEMENTED | Giữ. |
| Role | Vai trò RBAC cấp hệ thống | IMPLEMENTED | Giữ; độc lập với MeetingRole. |
| RoleCapability | Quyền năng gắn với Role | IMPLEMENTED | Giữ. |
| Meeting | Hồ sơ và vòng đời cuộc họp | IMPLEMENTED | Không lưu lịch dự kiến; lịch thuộc MeetingSession. |
| MeetingStatusHistory | Bằng chứng chuyển trạng thái | NEED MIGRATION / RESTRUCTURE | Giữ append-only; đồng bộ vòng đời đích. |
| MeetingParticipant | Phân công người tham dự nội bộ | NEED MIGRATION / RESTRUCTURE | Bỏ Guest; hỗ trợ phân công Current/Removed và lưu lịch sử. |
| MeetingGuestInvitation | Lời mời khách ngoài theo cuộc họp | NOT IMPLEMENTED | Thêm entity riêng; khách không phải User/người tham dự nội bộ. |
| MeetingSession | Lịch và vòng đời của phiên họp | IMPLEMENTED | Lịch dự kiến và API lập lịch (SCRUM-68); bắt đầu/kết thúc ghi `ActualStartedAt`/`ActualEndedAt` (SCRUM-64). Unique index lọc `status = 'InProgress'` trên `meeting_id` ép tối đa một phiên InProgress mỗi cuộc họp (BR-59). |
| AgendaItem | Mục nghị sự của phiên họp | NEED MIGRATION / RESTRUCTURE | Đổi MeetingId thành MeetingSessionId; trạng thái đích Pending/InProgress/Done. |
| Notification | Thông báo của User | IMPLEMENTED | Giữ User bắt buộc, Meeting tùy chọn. |
| Document | Tài liệu trong kho cá nhân | NEED MIGRATION / RESTRUCTURE | Bỏ MeetingId/AgendaItemId; thêm OwnerId. |
| DocumentVersion | Phiên bản bất biến của tài liệu | IMPLEMENTED | Giữ. |
| MeetingDocument | Gắn phiên bản tài liệu vào cuộc họp | NOT IMPLEMENTED | Thêm; ghim một phiên bản cụ thể. |
| DocumentComment | Góp ý trong ngữ cảnh tài liệu đính kèm cuộc họp | NEED MIGRATION / RESTRUCTURE | Đổi DocumentId thành MeetingDocumentId. |
| SpeakingRequest | Yêu cầu/lượt phát biểu | IMPLEMENTED | Giữ. |
| MeetingNote | Ghi chú thư ký | IMPLEMENTED | Giữ. |
| VotingSession | Phiên biểu quyết | IMPLEMENTED | Giữ; AgendaItem liên kết tùy chọn. |
| VotingOption | Phương án | IMPLEMENTED | Giữ. |
| VoteReceipt | Bằng chứng người bỏ phiếu đã bỏ phiếu | IMPLEMENTED | Giữ tính duy nhất theo session và voter. |
| VoteChoice | Lựa chọn đã bỏ | IMPLEMENTED | Giữ tách khỏi receipt; voter nullable nếu ẩn danh. |
| Recording | Bản ghi | IMPLEMENTED | Giữ consent, mục đích, lưu giữ và xóa dữ liệu cá nhân. |
| Transcript | Transcript của Recording | IMPLEMENTED | Tối đa một cho mỗi Recording. |
| TranscriptSegment | Đoạn transcript | IMPLEMENTED | Giữ. |
| MeetingMinutes | Phiên bản biên bản | IMPLEMENTED | Không ghi đè bản đã ban hành. |
| MinutesSignature | Chữ ký trên phiên bản biên bản | IMPLEMENTED | Giữ. |
| MeetingSummary | Tóm tắt AI hiện hành | IMPLEMENTED | Tối đa một bản hiện hành mỗi Meeting. |
| EmailMessage | Bản ghi gửi email/outbox | IMPLEMENTED | Giữ khóa chống trùng và thông tin thử gửi lại. |
| AuditLogEntry | Bằng chứng kiểm toán nghiệp vụ | IMPLEMENTED | Chỉ ghi thêm. |
| CallSession | Lượt gọi hoặc phòng trực tuyến | NEED MIGRATION / RESTRUCTURE | StartedById nullable; phòng gắn với Meeting, không gắn với MeetingSession. |
| CallParticipant | Một lượt kết nối của User hoặc khách | NEED MIGRATION / RESTRUCTURE | UserId XOR GuestInvitationId. |

## Phạm vi mô hình đích

Các ERD bên dưới mô tả mô hình đích. Bảng trạng thái triển khai ở trên phân biệt mô hình đích với code hiện hành.

## Nhóm thực thể trong mô hình đích

| Nhóm | Thực thể |
|---|---|
| Truy cập | User, Role, RoleCapability |
| Cuộc họp, thành phần và lịch | Meeting, MeetingStatusHistory, MeetingParticipant, MeetingGuestInvitation, MeetingSession, AgendaItem |
| Nền tảng và liên lạc | Notification, EmailMessage, AuditLogEntry |
| Kho tài liệu cá nhân | Document, DocumentVersion, MeetingDocument, DocumentComment |
| Hoạt động trong cuộc họp | SpeakingRequest, MeetingNote, VotingSession, VotingOption, VoteReceipt, VoteChoice |
| Bản ghi, biên bản và AI sau họp | Recording, Transcript, TranscriptSegment, MeetingMinutes, MinutesSignature, MeetingSummary |
| Phòng họp trực tuyến | CallSession, CallParticipant |

Các nhóm trên gồm 30 thực thể; trạng thái triển khai được liệt kê riêng ở trên.

## 1. Truy cập, cuộc họp và thành phần tham dự

```mermaid
erDiagram
    Role ||--o{ User : "governing access for"
    Role ||--o{ RoleCapability : "granting capabilities through"
    User ||--o{ Meeting : "organizing"
    User o|--o{ Meeting : "approving"
    User ||--o{ MeetingParticipant : "participating in"
    User ||--o{ MeetingStatusHistory : "acting in"
    Meeting ||--o{ MeetingParticipant : "including internal participants"
    Meeting ||--o{ MeetingStatusHistory : "recording lifecycle changes in"

    User {
        uuid Id PK
        string FullName
        citext Email UK
        string PasswordHash
        uuid RoleId FK
        bool IsActive
    }
    Role {
        uuid Id PK
        citext Name UK "tối đa 64"
        string Description
        bool IsBuiltIn "4 vai trò mặc định, id cố định"
    }
    RoleCapability {
        uuid RoleId PK
        string Capability PK
    }
    Meeting {
        uuid Id PK
        string Title
        string Status "9 trạng thái vòng đời"
        string Sensitivity "Normal|Internal|Confidential|Secret"
        string Mode "InPerson|Online|Hybrid"
        string Location
        string Description
        uuid CreatedById FK
        uuid AssignedApproverId FK "nullable"
    }
    MeetingParticipant {
        uuid Id PK
        uuid MeetingId FK
        uuid UserId FK
        string MeetingRole "Chair|Secretary|Member"
        string InvitationResponse
        string Attendance "Unknown|Present|Absent|Excused|Delegated"
        uuid DelegatedToUserId FK
        timestamptz RespondedAt
        string AssignmentStatus "Current|Removed"
        timestamptz RemovedAt
    }
    MeetingStatusHistory {
        uuid Id PK
        uuid MeetingId FK
        string FromStatus "null ở bản ghi tạo mới"
        string ToStatus
        uuid ActorId FK
        string Reason "bắt buộc khi từ chối"
        timestamptz OccurredAt
    }
    MeetingGuestInvitation {
        uuid Id PK
        uuid MeetingId FK
        string GuestName
        string GuestEmail
        string InvitationResponse
        timestamptz RespondedAt
        string InvitationStatus
        string TokenHash
        timestamptz ExpiresAt
        timestamptz VerifiedAt
        timestamptz RevokedAt
        timestamptz CreatedAt
    }
```

`InvitationResponse` và `Attendance` là **hai câu hỏi khác nhau**: một là người ta trả lời thư mời thế nào,
hai là thực tế có mặt hay không. Nhận lời rồi không đến là chuyện bình thường, và số liệu quorum
tính trên `Attendance`.

Guest không có User account, system Role hoặc internal MeetingRole; quyền Guest cố định và chỉ trong meeting được mời. Email phải được xác minh trước khi thiết lập guest session.

## 2. Phiên họp, nghị sự và tài liệu

```mermaid
erDiagram
    Meeting ||--o{ MeetingSession : "scheduling"
    MeetingSession ||--o{ AgendaItem : "containing"
    User o|--o{ AgendaItem : "presenting"
    User ||--o{ Document : "owning"
    Meeting ||--o{ MeetingDocument : "receiving"
    Document ||--o{ MeetingDocument : "attaching"
    DocumentVersion ||--o{ MeetingDocument : "pinning"
    AgendaItem o|--o{ MeetingDocument : "associating with"
    MeetingDocument ||--o{ DocumentComment : "providing context for"
    Document ||--o{ DocumentVersion : "versioning"
    DocumentComment ||--o{ DocumentComment : "replying to"

    MeetingSession {
        uuid Id PK
        uuid MeetingId FK
        string Title
        timestamptz PlannedStartTime
        timestamptz PlannedEndTime
        string Status "Planned|InProgress|Completed|Cancelled"
        timestamptz ActualStartedAt
        timestamptz ActualEndedAt
    }

    AgendaItem {
        uuid Id PK
        uuid MeetingSessionId FK
        int Position
        string Title
        string Description "nullable"
        int PlannedDurationMinutes
        uuid PresenterId FK "null nếu người presenting là khách"
        string PresenterName
        string Status "Pending|InProgress|Done"
        timestamptz ActualStartedAt
        timestamptz ActualEndedAt
    }
    Document {
        uuid Id PK
        uuid OwnerId FK
        string Title
        string Category "Material|Draft|Administrative|Other"
        uuid CurrentVersionId FK "nullable"
    }
    DocumentVersion {
        uuid Id PK
        uuid DocumentId FK
        int VersionNumber UK
        string FileName
        string StoragePath
        string ContentType "xác định theo nội dung thật"
        bigint ByteSize
        string ContentSha256
        uuid UploadedById FK
    }
    MeetingDocument {
        uuid Id PK
        uuid MeetingId FK
        uuid DocumentId FK
        uuid DocumentVersionId FK
        uuid AgendaItemId FK "nullable"
        uuid AddedById FK
        timestamptz AddedAt
    }
    DocumentComment {
        uuid Id PK
        uuid MeetingDocumentId FK
        uuid ParentCommentId FK "null nếu là góp ý gốc"
        uuid AuthorId FK
        int PageNumber "nullable"
        string Body
        bool IsResolved
    }
```

File không bao giờ bị ghi đè: tải bản mới là thêm một `DocumentVersion`. Mỗi lần gắn tài liệu vào meeting ghim một version cụ thể; cùng Document có thể dùng ở nhiều meeting. Comment trỏ vào MeetingDocument để giữ đúng meeting context.

> **`Document.MeetingId` đang có trong code nhưng không thuộc target.**
> [ADR-0006](../05-decisions/0006-kho-tai-lieu-ca-nhan-va-quyen-doc.md) chốt tách tài liệu thành
> **kho cá nhân** (chủ sở hữu là người tải lên) cộng với bảng nối `MeetingDocument` để gắn vào
> cuộc họp. Khi đó `AgendaItemId` chuyển từ `Document` sang dòng gắn kết, `DocumentComment` cũng
> trỏ vào dòng gắn kết thay vì vào `Document`, và mỗi lần gắn **ghim đúng một `DocumentVersion`**.
> Sơ đồ này là target model; implementation hiện tại vẫn cần migration/restructure.

## 3. Biểu quyết và hoạt động trong cuộc họp

```mermaid
erDiagram
    Meeting ||--o{ SpeakingRequest : "receiving"
    Meeting ||--o{ MeetingNote : "recording"
    Meeting ||--o{ VotingSession : "hosting"
    AgendaItem o|--o{ VotingSession : "contextualizing"
    VotingSession ||--o{ VotingOption : "offering"
    VotingSession ||--o{ VoteReceipt : "receiving"
    VotingSession ||--o{ VoteChoice : "recording"
    VotingOption ||--o{ VoteChoice : "being selected in"

    SpeakingRequest {
        uuid Id PK
        uuid MeetingId FK
        uuid AgendaItemId FK "nullable"
        uuid UserId FK
        timestamptz RequestedAt
        int QueuePosition "chủ toạ đổi được"
        string Status "Queued|Invited|Speaking|Finished|Cancelled"
        timestamptz StartedAt
        timestamptz EndedAt
    }
    MeetingNote {
        uuid Id PK
        uuid MeetingId FK
        uuid AgendaItemId FK "nullable"
        uuid AuthorId FK
        string Body
    }
    VotingSession {
        uuid Id PK
        uuid MeetingId FK
        uuid AgendaItemId FK
        string Title
        string Kind "Live|Asynchronous"
        bool IsAnonymous
        string Status "Draft|Open|Closed"
        timestamptz ClosesAt
        int EligibleCount "chốt lúc mở phiên"
        string Conclusion
    }
    VotingOption {
        uuid Id PK
        uuid VotingSessionId FK
        int Position UK
        string Label
    }
    VoteReceipt {
        uuid Id PK
        uuid VotingSessionId FK
        uuid VoterId FK
        timestamptz CastAt
    }
    VoteChoice {
        uuid Id PK
        uuid VotingSessionId FK
        uuid VotingOptionId FK
        uuid VoterId FK "NULL khi phiên ẩn danh"
        timestamptz CastAt
    }
```

### Biểu quyết ẩn danh — đọc kỹ chỗ này

Tách **`VoteReceipt`** (ai đã bỏ phiếu) khỏi **`VoteChoice`** (phiếu chọn gì) là cách làm cho phiếu
kín thực sự kín:

- `VoteReceipt` có unique index `(VotingSessionId, VoterId)` → một người một phiếu, do database ép,
  không phải do một câu `if` mà hai request đồng thời có thể cùng vượt qua.
- `VoteChoice.VoterId` **để NULL khi phiên ẩn danh** → không dòng nào trong database nối người với
  lựa chọn. Ẩn tên trên giao diện là không đủ: liên kết vẫn nằm đó cho bất cứ ai đọc được DB.

**Hệ quả phải biết:** phiên ẩn danh thì **không đổi phiếu được**, vì không có gì xác định phải thay
dòng nào. `SCRUM-28` yêu cầu đổi phiếu tới khi đóng phiên — hiện chỉ làm được với phiên công khai.
Muốn có cả hai thì cần cơ chế token cấp cho từng người bỏ phiếu, và đó là một quyết định thiết kế
riêng, không phải thứ chắp thêm. **Việc này cần BA chốt trước khi `SCRUM-28` bắt đầu.**

## 7. Bản ghi, transcript, biên bản và AI

```mermaid
erDiagram
    Meeting ||--o{ Recording : "recording"
    Recording ||--o| Transcript : "transcribing"
    Transcript ||--o{ TranscriptSegment : "containing"
    Meeting ||--o{ MeetingMinutes : "issuing"
    MeetingMinutes ||--o{ MinutesSignature : "receiving"
    MeetingMinutes ||--o| MeetingMinutes : "superseding"
    Meeting ||--o| MeetingSummary : "generating"

    Recording {
        uuid Id PK
        uuid MeetingId FK
        string Source "BrowserCapture|Upload|CallRoom"
        string Status "Pending|Available|Failed|Deleted"
        string StoragePath
        bigint DurationMs
        timestamptz ConsentObtainedAt "bắt buộc trước khi ghi"
        string ConsentPurpose
        timestamptz RetainUntil
        timestamptz DeletedAt
    }
    Transcript {
        uuid Id PK
        uuid RecordingId FK UK
        string Status "Pending|Processing|Completed|Failed"
        string Provider "ghi rõ nếu là dịch vụ ngoài"
        string Language
        int ProgressPercent
        string ErrorMessage
    }
    TranscriptSegment {
        uuid Id PK
        uuid TranscriptId FK
        int Position UK
        bigint StartMs
        bigint EndMs
        string Text
        string SpeakerLabel "Người nói 1, 2, 3…"
        uuid SpeakerUserId FK "thư ký gán tay"
        uuid EditedById FK
    }
    MeetingMinutes {
        uuid Id PK
        uuid MeetingId FK
        int RevisionNumber UK
        uuid SupersedesId FK
        string Content
        string Status "Draft|WaitingApproval|Rejected|Issued|Superseded"
        uuid CreatedById FK
        uuid ApprovedById FK
        string RejectionReason
        timestamptz IssuedAt "đặt rồi là khoá"
    }
    MinutesSignature {
        uuid Id PK
        uuid MinutesId FK
        uuid SignerId FK
        timestamptz SignedAt
        string ContentSha256
        string Signature
        string PublicKey
    }
    MeetingSummary {
        uuid Id PK
        uuid MeetingId FK UK
        string Content
        string Model
        timestamptz GeneratedAt
        uuid ReviewedById FK "null = chưa ai soát"
    }
```

## 8. Phòng họp trực tuyến

Phòng trực tuyến thuộc Meeting, không thuộc MeetingSession. MeetingSession và CallSession là hai khái niệm độc lập; việc vào/ra phòng không làm thay đổi lifecycle cuộc họp hoặc session.

```mermaid
erDiagram
    Meeting ||--o{ CallSession : "hosting"
    CallSession ||--o{ CallParticipant : "connecting through"
    User o|--o{ CallSession : "initiating"
    Recording o|--o{ CallSession : "recording"
    MeetingGuestInvitation o|--o{ CallParticipant : "connecting as guest"
    User o|--o{ CallParticipant : "connecting as user"

    CallSession {
        uuid Id PK
        string Kind "Direct|MeetingRoom"
        uuid MeetingId FK "null nếu gọi 1-1"
        string Status "Ringing|Active|Ended|Missed"
        uuid StartedById FK "nullable"
        timestamptz StartedAt
        timestamptz EndedAt
        uuid RecordingId FK
    }
    CallParticipant {
        uuid Id PK
        uuid CallSessionId FK
        uuid UserId FK "nullable"
        uuid GuestInvitationId FK "nullable"
        timestamptz JoinedAt
        timestamptz LeftAt
        bool Declined
    }
```

Chỉ lưu **sự kiện cuộc gọi**, không lưu media. Mỗi CallParticipant phải tham chiếu đúng một identity: User XOR MeetingGuestInvitation. Rớt mạng vào lại sinh một dòng mới cho connection stint tiếp theo. Mỗi Meeting có tối đa một MeetingRoom CallSession Active.

## 9. Nền tảng, thông báo và kiểm toán

`Notification` thuộc về đúng một User (`UserId`) và có thể trỏ tới một Meeting
(`meeting_id`). `read_at` null nghĩa là chưa đọc. `deduplication_key` nullable có unique index;
nhắc họp dùng `meeting-starting-soon:<meeting-id>:<user-id>`. Notification lời mời sẽ được tạo cho
tất cả participant trong luồng publish meeting của SCRUM-19, không phải khi thêm từng participant.
`pushed_at` null nghĩa là worker chưa push dòng đã commit qua SignalR; client mất kết nối vẫn
đọc lại từ DB qua API. Migration SCRUM-14 thêm bốn cột `deduplication_key`, `pushed_at`,
`target_type` và `target_id` mà không thay đổi quan hệ user/meeting có sẵn.

`email_messages.deduplication_key` cũng có unique index nhưng định danh một **sự kiện gửi cụ thể**,
không phải chỉ một cặp meeting/người nhận. Nhờ đó retry transaction của cùng lần công bố không tạo
thư trùng, còn các sự kiện đổi lịch, hủy họp hoặc phát hành biên bản vẫn có thể gửi thư mới cho cùng
người nhận.

Notification lưu `target_type` và `target_id`; với thông báo họp, các giá trị là `Meeting` và ID
cuộc họp. Route UI không được lưu trong database: API dựng `actionUrl` từ hai trường target khi trả
response, nên thay đổi route không làm hỏng các notification đã có. `meeting_id` tiếp tục là khóa
ngoại để giữ quan hệ và bảo vệ tính toàn vẹn dữ liệu. Migration điền target và đánh dấu các dòng
cũ đã được xử lý realtime để worker không push lại hàng loạt ở lần chạy đầu. `isRead` được suy ra
từ `read_at`, không lưu thêm một cột bool.

```mermaid
erDiagram
    User ||--o{ Notification : "receiving"
    Meeting o|--o{ Notification : "providing context for"
    Meeting o|--o{ EmailMessage : "triggering"
    User o|--o{ AuditLogEntry : "acting in"

    Notification {
        uuid Id PK
        uuid UserId FK
        uuid MeetingId FK "nullable"
        string TargetType
        uuid TargetId
        timestamptz ReadAt "nullable"
    }
    EmailMessage {
        uuid Id PK
        uuid MeetingId FK "nullable"
        string DeduplicationKey
        string Status
        int RetryCount
        timestamptz CreatedAt
    }
    AuditLogEntry {
        uuid Id PK
        uuid ActorId FK "nullable"
        string Action
        string ObjectType
        uuid ObjectId
        string IpAddress
        json Details
        timestamptz OccurredAt
    }
}
```
## Tổng hợp đầy đủ các quan hệ

| Thực thể nguồn | Số lượng liên kết | Thực thể đích | Quan hệ nghiệp vụ |
|---|---:|---|---|
| Role | 1 → 0..* | User | Quy định quyền truy cập |
| Role | 1 → 0..* | RoleCapability | Cấp quyền năng thông qua |
| User | 1 → 0..* | Meeting | Tổ chức |
| User | 0..1 → 0..* | Meeting | Phê duyệt; mỗi Meeting có tối đa một người duyệt được gán |
| Meeting | 1 → 0..* | MeetingStatusHistory | Ghi lại thay đổi vòng đời |
| User | 1 → 0..* | MeetingStatusHistory | Thực hiện hành động |
| Meeting | 1 → 0..* | MeetingParticipant | Bao gồm thành phần nội bộ |
| User | 1 → 0..* | MeetingParticipant | Tham gia |
| MeetingParticipant | 0..1 → 1 | User | Ủy quyền cho |
| Meeting | 1 → 0..* | MeetingGuestInvitation | Mời khách |
| Meeting | 1 → 0..* | MeetingSession | Lập lịch |
| MeetingSession | 1 → 0..* | AgendaItem | Chứa |
| User | 0..* → 0..1 | AgendaItem | Trình bày |
| User | 1 → 0..* | Document | Sở hữu |
| Document | 1 → 1..* | DocumentVersion | Quản lý phiên bản |
| User | 1 → 0..* | DocumentVersion | Tải lên |
| Meeting | 1 → 0..* | MeetingDocument | Nhận tài liệu |
| Document | 1 → 0..* | MeetingDocument | Được đính kèm |
| DocumentVersion | 1 → 0..* | MeetingDocument | Được ghim |
| AgendaItem | 0..1 → 0..* | MeetingDocument | Gắn với |
| MeetingDocument | 1 → 0..* | DocumentComment | Cung cấp ngữ cảnh cho |
| DocumentComment | 0..1 → 0..* | DocumentComment | Trả lời |
| Meeting | 1 → 0..* | SpeakingRequest | Tiếp nhận |
| Meeting | 1 → 0..* | MeetingNote | Ghi nhận |
| Meeting | 1 → 0..* | VotingSession | Tổ chức |
| AgendaItem | 0..1 → 0..* | VotingSession | Cung cấp ngữ cảnh cho |
| VotingSession | 1 → 1..* | VotingOption | Đưa ra lựa chọn |
| VotingSession | 1 → 0..* | VoteReceipt | Ghi nhận việc bỏ phiếu |
| VotingSession | 1 → 0..* | VoteChoice | Ghi nhận lựa chọn |
| VotingOption | 1 → 0..* | VoteChoice | Được chọn trong |
| Meeting | 1 → 0..* | Recording | Ghi âm |
| Recording | 1 → 0..1 | Transcript | Chuyển thành văn bản |
| Transcript | 1 → 0..* | TranscriptSegment | Chứa |
| Meeting | 1 → 0..* | MeetingMinutes | Ban hành |
| MeetingMinutes | 1 → 0..* | MinutesSignature | Nhận chữ ký |
| MeetingMinutes | 0..1 → 0..1 | MeetingMinutes | Thay thế phiên bản trước |
| Meeting | 1 → 0..1 | MeetingSummary | Tạo tóm tắt |
| User | 1 → 0..* | Notification | Nhận |
| Meeting | 0..1 → 0..* | Notification | Cung cấp ngữ cảnh cho |
| Meeting | 0..1 → 0..* | EmailMessage | Kích hoạt gửi |
| User | 0..1 → 0..* | AuditLogEntry | Thực hiện hành động |
| Meeting | 0..1 → 0..* | CallSession | Cung cấp phòng |
| CallSession | 1 → 0..* | CallParticipant | Kết nối qua |
| User | 0..1 → 0..* | CallParticipant | Kết nối với tư cách nội bộ |
| MeetingGuestInvitation | 0..1 → 0..* | CallParticipant | Kết nối với tư cách khách ngoài |

## Quy tắc nghiệp vụ và ràng buộc dữ liệu không thể hiện trên ERD

1. Lịch dự kiến chỉ thuộc MeetingSession. MeetingSession là tùy chọn; gửi duyệt, công bố và bắt đầu Meeting không yêu cầu phải có session.
2. Mỗi Meeting có tối đa một MeetingSession ở trạng thái InProgress; vòng đời Meeting và MeetingSession độc lập. Session chuyển từ Planned → InProgress → Completed hoặc Planned → Cancelled; Completed và Cancelled là trạng thái kết thúc.
3. Mỗi AgendaItem thuộc đúng một MeetingSession; chỉ sắp xếp lại trong session đó; mỗi session có tối đa một AgendaItem InProgress. Trạng thái đích: Pending → InProgress → Done.
4. Mỗi participant nội bộ có đúng một MeetingRole: Chair, Secretary hoặc Member. MeetingRole độc lập với system Role.
5. Guest bên ngoài không có User account hay system Role; quyền Guest cố định và chỉ áp dụng trong Meeting được mời. Phải xác minh email trước khi thiết lập guest session.
6. Mỗi cặp (MeetingId, UserId) có tối đa một phân công Current. Khuyến nghị dùng unique index có điều kiện trên `(meeting_id, user_id)` khi trạng thái phân công là Current. Giữ bằng chứng Removed/lịch sử khi cần truy vết.
7. Mỗi cặp (MeetingId, GuestEmail) có tối đa một lời mời Guest còn hiệu lực. Không mời email Guest nếu email đó đã thuộc một participant nội bộ hiện hành trong cùng Meeting.
8. Để công bố hoặc bắt đầu Meeting, phải có ít nhất một Chair và một Secretary đang được phân công hiện hành, đồng thời tài khoản của họ còn hoạt động.
9. Chỉ participant nội bộ mới được ủy quyền; không được tự ủy quyền, và một User không thể đại diện nhiều hơn một participant trong cùng Meeting. Ủy quyền không tạo participant row và không chuyển MeetingRole. Guest không được ủy quyền.
10. MeetingDocument ghim một DocumentVersion cụ thể. Nếu có AgendaItemId, AgendaItem đó phải thuộc một MeetingSession của cùng Meeting.
11. VoteReceipt duy nhất theo (VotingSessionId, VoterId). VoteChoice ẩn danh phải có VoterId null; không gộp receipt với choice.
12. Mỗi Recording có tối đa một Transcript; mỗi Meeting có tối đa một MeetingSummary hiện hành.
13. MeetingStatusHistory và AuditLogEntry là bằng chứng chỉ ghi thêm.
14. Phòng trực tuyến thuộc Meeting, không thuộc MeetingSession. Vào/rời phòng và điều khiển media không làm đổi vòng đời meeting/session; mỗi Meeting có tối đa một CallSession MeetingRoom đang Active.
15. Mỗi CallParticipant tham chiếu đúng một identity: User nội bộ hoặc MeetingGuestInvitation bên ngoài. Mỗi lần kết nối lại tạo một row mới cho lượt kết nối đó.
16. Trước khi ghi âm phải có consent và lưu mục đích, thời hạn lưu giữ cùng bằng chứng xóa. Guest phải được hiển thị thông báo ghi âm trước khi vào; lưu bằng chứng đã thông báo và thực hiện nghĩa vụ xóa dữ liệu của Guest.
17. Lời mời Guest giới hạn trong một Meeting, có hạn dùng, có thể thu hồi và dùng token được bảo vệ (chỉ lưu hash); kiểm tra hiệu lực ở mỗi request.

## Khác biệt với implementation hiện tại và ghi chú migration

| Code/schema hiện tại | Mô hình đích | Thay đổi cần thiết |
|---|---|---|
| Meeting trước SCRUM-70 có StartsAt/EndsAt. | Meeting không lưu lịch dự kiến. | Đã backfill Meeting chưa có session và bỏ field/index bằng migration SCRUM-70. |
| AgendaItem hiện có MeetingId. | Bắt buộc có AgendaItem.MeetingSessionId. | Thêm session và gán mỗi agenda item vào một session. |
| MeetingRole hiện có Guest. | Chỉ còn Chair/Secretary/Member. | Dùng MeetingGuestInvitation cho khách bên ngoài. |
| Tính duy nhất participant áp dụng cho mọi row. | Chỉ duy nhất với phân công Current. | Thêm trạng thái/bằng chứng gỡ phân công và unique constraint có điều kiện. |
| Document hiện có MeetingId và AgendaItemId. | Document cá nhân có OwnerId; MeetingDocument giữ liên kết với cuộc họp. | Bỏ liên kết trực tiếp và thêm entity liên kết. |
| DocumentComment hiện tham chiếu Document. | DocumentComment tham chiếu MeetingDocument. | Giữ ngữ cảnh góp ý theo từng cuộc họp. |
| CallParticipant.UserId hiện bắt buộc. | UserId XOR GuestInvitationId. | Cho phép hai cột identity null riêng rẽ và ràng buộc chỉ một cột có giá trị. |
| CallSession.StartedById hiện bắt buộc. | Có thể null với MeetingRoom. | Cho phép phòng tồn tại mà không cần starter nội bộ. |
| MeetingGuestInvitation, MeetingDocument chưa có trong domain/schema. | Cả hai là entity đích. | Sẽ triển khai/migration sau; hiện chưa phải bảng trong DB. MeetingSession đã có schema. |

### Xung đột yêu cầu cần đối chiếu

- **ADR-0010:** hiện gắn CallSession với MeetingSession; mô hình đích mới xác định phòng ở cấp Meeting và không cho phép quan hệ đó. ERD này theo yêu cầu mới; nhóm cần đối chiếu hoặc thay thế ADR riêng.
- **ADR-0005:** nêu rằng danh tính khách không được xác minh và mô tả link/mã mời có giới hạn lượt dùng, trong khi mô hình đích yêu cầu xác minh email và lưu TokenHash, hạn dùng, thời điểm xác minh/thu hồi. Cần đối chiếu thay đổi này với ADR. Quyền chỉ nghe, không xem danh sách người dự/tài liệu và bằng chứng thông báo ghi âm vẫn áp dụng nếu chưa có yêu cầu mới thay thế.
- **Tài liệu vòng đời cuộc họp:** giữ nguyên trục trạng thái; mô hình đích cho phép gửi duyệt lại sau Rejected. Cần chỉnh mũi tên nếu sơ đồ hiện tại bị hiểu là không cho phép gửi lại.
- **ADR-0006:** kho cá nhân, quyền sở hữu, ghim phiên bản và ngữ cảnh comment qua MeetingDocument phù hợp với mô hình đích. Câu hỏi chưa chốt về hạn mức kho và quyền truy cập của người đã rời tổ chức nằm ngoài phạm vi ERD này.

## Những quyết định cần biết

| Quyết định | Lý do |
|---|---|
| Khoá chính `uuid`, sinh bằng `Guid.CreateVersion7()` | Không đoán được từ bên ngoài, nhưng tăng dần theo thời gian nên chèn tuần tự không làm phân mảnh index |
| Tên bảng và cột **snake_case** | PostgreSQL hạ chữ thường identifier không đóng ngoặc kép |
| `users.email` kiểu **`citext`** | Không đổi kiểu thì unique index vẫn cho `Anh@x.vn` và `anh@x.vn` thành hai tài khoản |
| `users.password_hash` và `users.google_subject_id` đều **nullable**, kèm check constraint phải có ít nhất một | Hai phương thức đăng nhập (ADR-0004): tài khoản chỉ dùng Google không có mật khẩu, tài khoản chỉ dùng mật khẩu không có liên kết Google. Bắt buộc một trong hai thì phương thức kia không lưu được; cho phép cả hai null thì tạo ra dòng không ai đăng nhập được bao giờ |
| `users.google_subject_id` liên kết theo claim `sub`, **không** theo email | `sub` ổn định và duy nhất suốt đời tài khoản Google; email đổi được và trên domain workspace còn bị cấp lại cho người khác — match theo email là trao tài khoản người cũ cho người mới |
| Enum lưu dạng **chuỗi** | Đọc thẳng trong DB là hiểu; thêm trạng thái mới không đánh số lại dòng cũ |
| `Status`, `Sensitivity` **không có default** ở DB | Tầng ứng dụng phải nói rõ. Mặc định ngầm về giá trị dễ dãi nhất là cách một cuộc họp mật bị lộ |
| Mọi khoá ngoại trỏ tới `users` đều `Restrict` | Người dùng vô hiệu hoá chứ không xoá; bằng chứng kiểm toán phải giữ được quy kết trách nhiệm |
| `Meeting` → dữ liệu con là `Cascade` | Xoá một cuộc họp nháp thì dữ liệu con đi theo, không để lại rác |
| `agenda_items.Position` **không** unique | Kéo thả đổi thứ tự ghi nhiều dòng một lúc; unique index sẽ chặn trạng thái trung gian |
| `meeting_notes` unique có `AreNullsDistinct(false)` | PostgreSQL mặc định coi hai NULL là khác nhau, nên ghi chú cấp cuộc họp sẽ lọt nếu không khai báo |
| `audit_log.Details` kiểu `jsonb` | Hỏi được câu mà các cột không trả lời, không phải sửa schema |
| `MeetingStatusHistory` và `audit_log` chỉ ghi thêm | Đây là bằng chứng, không phải log kỹ thuật |

## Ranh giới đã chốt — đừng thêm bảng cho những thứ này

| Không có bảng cho | Vì sao |
|---|---|
| Nhiệm vụ / action item | Ngoài phạm vi. TrustCabinet dừng ở biên bản đã chốt — xem mục "Không làm" trong [README](../../README.md). Không có `tasks`, `assignments`, không có cột hạn hoàn thành ở đâu cả |
| Voiceprint / đặc trưng giọng | Dữ liệu sinh trắc học theo Nghị định 13/2023/NĐ-CP. Chỉ tách người nói **trong một bản ghi** (`SpeakerLabel`), rồi người gán tên tay. Có test tự động chặn việc thêm cột kiểu này |
| Khoá mã hoá, sổ huỷ khoá | Không làm mã hoá phong bì |
| Tổ chức (`organizations`, `tenants`) | Hệ thống phục vụ **đúng một tổ chức**. Không có bảng tổ chức, không multi-tenant, không vùng lưu trú dữ liệu — xem [ADR-0004](../05-decisions/0004-mot-to-chuc-khong-co-don-vi.md) |
| Đơn vị / phòng ban (`org_units`) | Cũng theo ADR-0004: không có phân cấp cha–con, không gán người dùng vào đơn vị, không có "đơn vị chủ trì" cuộc họp. Mời họp là mời **từng người**. Có test tự động chặn việc thêm lại cột kiểu này |
| Hàng đợi job | ASR chạy nền bằng cơ chế đơn giản, không dựng job spine |

Về bản ghi âm: `recordings` có `ConsentObtainedAt`, `ConsentPurpose`, `RetainUntil`, `DeletedAt` —
bốn cột đó tồn tại để hệ thống **chứng minh được** đã xin đồng ý trước khi ghi, đã nêu mục đích, và
đã xoá khi tới hạn hoặc khi có yêu cầu. Nghĩa vụ theo Nghị định 13/2023/NĐ-CP, không phải thứ trang
trí. Ứng dụng phải từ chối ghi âm khi `ConsentObtainedAt` còn null, chứ không ghi trước rồi hỏi sau.

## Tham chiếu AI-eCabinet: lấy gì, bỏ gì

Hệ thống gốc có 34 bảng trên PostgreSQL. Phần lớn phần chênh lệch là hạ tầng ngoài phạm vi đồ án.

**Lấy ý tưởng:** tách lịch sử trạng thái thành bảng riêng chỉ ghi thêm; `sensitivity` là trục độc
lập với `status`; tách `documents`/`document_versions`; tách phiếu bầu khỏi người bầu để làm phiếu
kín; vai trò trong cuộc họp tách khỏi vai trò hệ thống.

**Bỏ hẳn:**

| Của hệ thống gốc | Vì sao không làm |
|---|---|
| `tenants` + Row-Level Security trên mọi bảng | TrustCabinet phục vụ một tổ chức |
| `residency_zone`, `key_domain`, `key_id` | Kéo theo yêu cầu lưu trú dữ liệu và mã hoá theo vùng |
| `persons` + `sessions` + Zitadel | Không dựng IdP riêng: đăng nhập bằng email/mật khẩu hoặc Google (`SCRUM-11`) |
| `relation_tuples`, `authz_epochs` (ReBAC) | RBAC phẳng đủ: vai trò là bảng `roles` quản trị viên tự thêm được ([ADR-0008](../05-decisions/0008-vai-tro-la-du-lieu.md)), quyền vẫn là enum |
| `encryption_keys`, `key_shred_ledger` | Không làm mã hoá phong bì |
| `job_outbox`, `job_stage`, `pipeline_job` | Không dựng job spine |
| `audit_log` chuỗi hash | `SCRUM-16` chỉ cần chỉ-ghi-thêm |
| `meeting_external_participants` | ~~Chưa nằm trong `SCRUM-20`~~ — **đã lật lại**: khách ngoài vào nghe bằng link mời, xem [ADR-0005](../05-decisions/0005-khach-ngoai-tham-du-bang-link-moi.md). Bảng chưa có, sẽ thêm khi làm story |

**Thêm so với hệ thống gốc:** `Meeting.Mode` và toàn bộ nhóm `call_sessions` / `call_participants`
— hệ thống gốc không tự làm phần gọi, TrustCabinet có gọi thoại/video trong ứng dụng nên đó là
điểm khác biệt chính của đồ án.

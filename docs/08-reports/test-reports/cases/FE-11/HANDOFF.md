# Handoff — FE-11: unit test mới phủ domain, service `VoteService` chưa test trực tiếp được

Ngày: 2026-11-14 · Nhánh: `docs/integration-test` · Người nhận: điều phối viên / người làm tiếp FE-11

## 1. Hiện trạng (đã có gì)

- `cases/FE-11/ui.json` (20 case), `cases/FE-11/api.json` (43 case): hoàn chỉnh, `check` sạch.
- `cases/FE-11/unit/`: `Vote.Validate` (6 UTC), `Vote.EligibleUsers` (4 UTC), `Vote.Percentage` (4 UTC).
- `tests/TrustCabinet.Web.Tests/Votes/VoteRulesTests.cs`: 17 test trực tiếp (in-process, không HTTP/không DB), chạy pass 17/17.
- Workbook `Report5.1_Unit_Test.xlsx` build lại được, mở bằng Excel không báo repair.

## 2. Vấn đề: thiếu hẳn lớp test service

14 UTC của FE-11 hiện chỉ phủ `VoteRules` (Validate/EligibleUsers/Percentage) — là logic thuần domain.
Phần chính của nghiệp vụ nằm ở `VoteService`
(`backend/TrustCabinet.Infrastructure/Votes/VoteService.cs`, 552 dòng, 11 operation:
Create/Update/Delete/Open/Cast/Close/RecordConclusion/AttachDocuments/AttachFromLibrary/
AttachUpload/DetachDocument) **chưa có một direct unit test nào** — các nhánh này hiện chỉ được
phủ gián tiếp qua HTTP (5.2.2, `VoteEndpointTests`).

Nguyên nhân (đã đối chiếu code, không phải đoán):

- `VoteService` ôm `TrustCabinetDbContext` + `VoteRepository` cụ thể (sealed class, không
  interface) ngay ở constructor (`VoteService.cs:22`), mọi public method đều mở DB transaction
  trước khi làm gì, các hàm xử lý từng operation đều `private`.
- Không có seam để fake/stub — khác hẳn các service đã test được như `UserService`
  (`backend/TrustCabinet.Infrastructure/Users/UserService.cs:23`: toàn bộ dependency là
  interface nên `UserServiceTests` fake được).
- Toàn repo có 35 `*Service.cs` nhưng chỉ ~11 file có `*ServiceTests.cs` trực tiếp
  (`UserService`, `RoleService`, `RolePermissionService`, `AuthenticationService`,
  `MeetingService`, `MeetingWorkflowService`, `NotificationService`, `CallRoomService`…).
  Nhóm chưa có gồm `VoteService`, `AttendanceService`, `ParticipantService`,
  `MeetingDocumentService`, `RecordingService`… — đa số là các service ôm `DbContext`/repository
  cụ thể. Đây là nợ chung, không riêng FE-11.

## 3. Đề xuất (chưa làm — cần quyết định trước vì đụng production code)

1. Tách seam tối thiểu, giữ nguyên behavior, theo mẫu `IMeetingRepository.InTransactionAsync`:
   `IVoteRepository` (extract toàn bộ method `VoteService` đang gọi trên `VoteRepository`),
   cộng các method persistence `VoteService` đang gọi thẳng `db` (user-active check, đếm/tìm
   meeting document, add/remove `VotingSession`/`VotingOption`/`VoteEligibleVoter`/`VoteReceipt`/
   `VoteChoice`/`DocumentReadConfirmation`, `InTransactionAsync`/`SaveChangesAsync`/
   `ClearChanges`, attach tài liệu từ thư viện).
2. `VoteService` chỉ phụ thuộc interface (`IVoteRepository`, `IParticipantRepository`,
   `IMeetingDocumentRepository`, `IVoteChangePublisher`…), bỏ `TrustCabinetDbContext` khỏi
   constructor; `VoteRepository` implement `IVoteRepository`, DI đăng ký lại, endpoint không đổi.
3. Viết `VoteServiceTests` với fake in-memory, chia theo từng operation đúng kiểu mẫu của các
   service khác: create problem / update problem / open / cast / close / conclude / attach /
   detach — mỗi nhánh failure một test (`DraftDenied`, `OpenDenied`, `AnotherOpen`,
   `NoEligibleVoters`, `NotEligible`, `NotAccepting`, `AlreadyCast`, `DocumentsUnread`,
   `CloseDenied`, `ConclusionDenied`…).
4. Cập nhật `cases/FE-11/unit/` + `functions.json`: Note các UTC chuyển từ `Manual only` sang
   `Automated: …VoteServiceTests.cs (Method)`; các nhánh vẫn HTTP-only giữ nguyên khai báo gap.

## 4. Ràng buộc nhắc lại

- Lần này **không sửa production code** (theo yêu cầu): chỉ ghi nhận hiện trạng, không nhận vơ
  coverage service trong báo cáo.
- Mọi UTC vẫn `Pending`; mọi `MSGnn`/route/field/message trong case đều từ code, test,
  `docs/03-api/` — không bịa giá trị.

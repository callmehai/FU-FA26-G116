# 07 · Class diagram

Một đầu mối cho **mục lục, quy ước Design Class Diagram (DCD) và kết quả rà soát**. Đọc file này
trước khi tạo, sửa hoặc review class diagram. Tên UML, stereotype, attribute và operation dùng
English; phần giải thích dùng tiếng Việt.

## 1. Mục lục và phạm vi

Mỗi feature có một source `.drawio` và một ảnh `.drawio.png` cùng tên. Source là bản editable;
PNG phải xuất từ đúng source đó. Các bản hiện có là **thiết kế theo feature**, không phải bản
trích xuất nguyên trạng các class C# đã triển khai.

| Feature | Source / PNG | UC trong phạm vi |
|---|---|---|
| FE-01 Authentication | [drawio](feature-01-auth-class-diagram.drawio) · [PNG](feature-01-auth-class-diagram.drawio.png) | UC-01–UC-04 |
| FE-02 User Management | [drawio](feature-02-user-management-class-diagram.drawio) · [PNG](feature-02-user-management-class-diagram.drawio.png) | UC-05, UC-06a/b/c, UC-07–UC-09 |
| FE-03 Role Management | [drawio](feature-03-role-management-class-diagram.drawio) · [PNG](feature-03-role-management-class-diagram.drawio.png) | UC-10, UC-11, UC-12a/b/c, UC-13 |
| FE-04 Meeting Management | [drawio](feature-04-meeting-management-class-diagram.drawio) · [PNG](feature-04-meeting-management-class-diagram.drawio.png) | UC-14–UC-22, UC-75 |
| FE-05 Agenda Management | [drawio](feature-05-agenda-management-class-diagram.drawio) · [PNG](feature-05-agenda-management-class-diagram.drawio.png) | UC-23a/b/c/d, UC-24–UC-26, UC-77 |
| FE-06 Meeting Participant Management | [drawio](feature-06-meeting-participant-management-class-diagram.drawio) · [PNG](feature-06-meeting-participant-management-class-diagram.drawio.png) | UC-27, UC-28a/b/c, UC-29–UC-31, UC-73 |
| FE-07 Meeting Document Management (một phần) | [drawio](feature-07-document-management-class-diagram.drawio) · [PNG](feature-07-document-management-class-diagram.drawio.png) | UC-32a, UC-32c, UC-35 |
| FE-13 Notification | [drawio](feature-13-notification-management-class-diagram.drawio) · [PNG](feature-13-notification-management-class-diagram.drawio.png) | UC-66, UC-67a/b, UC-68 |

FE-07 mới có phần đọc tài liệu cuộc họp và góp ý (UC-32a, UC-32c, UC-35 — SCRUM-21/22/23, 22/10/2026); kho cá nhân, thêm/gỡ/gắn tài liệu (UC-33a–d, UC-34, UC-36, UC-71, UC-72 — SCRUM-58) chưa vẽ. FE-08–FE-12, FE-14 và FE-15 chưa có class diagram trong thư mục này. FE-05 Meeting Session cũ
đã bị bỏ theo [ADR-0013](../05-decisions/0013-mot-cuoc-hop-la-mot-buoi-hop.md); FE-05 hiện tại là Agenda.
UC-07 hiện nằm trong FE-02, không còn một bản 02b độc lập.

## 2. Nguồn và cách xử lý khác biệt

1. **Yêu cầu mới nhất đã chốt:** Jira và ghi chú thay đổi; đối chiếu UC/BR trong
   [Report 3](../08-reports/Report-3_Software_Requirement_Specification.md),
   [UC inventory](../01-requirements/00-master-use-case-inventory.md),
   [UC Specification](../01-requirements/05-use-case-specifications.md) và
   [Business Rules](../01-requirements/06-business-rules.md). Kiểm tra phiên bản/ngày và tên UC;
   không suy ra UC hiện hành từ số cũ trong tiêu đề sequence.
2. **Sequence cùng phạm vi:** đọc toàn bộ source `.txt` của feature trong
   [06-sequencediagram](../06-sequencediagram/README.md), kiểm tra trạng thái phê duyệt và lập
   traceability. Sequence là bằng chứng về tương tác; mâu thuẫn với UC/BR phải được ghi riêng.
3. **Entity và kiến trúc:** dùng Entity Descriptions của Report 3,
   [ERD mục tiêu](../02-design/02-erd.md) và ADR đã chấp nhận còn phù hợp. ERD mục tiêu khác schema
   hiện tại; không tự đổi entity, enum, quyền hay lifecycle chỉ để khớp code.
4. **Code hiện tại:** dùng để kiểm tra trạng thái triển khai; chỉ lấy chữ ký C# làm chuẩn bắt buộc
   khi diagram được tuyên bố là **as-built**. Ghi rõ khác biệt design/code.
5. **Message:** nguồn chuẩn duy nhất là
   [catalogue MSG](../03-api/00-danh-muc-ma-thong-bao.md), kể cả khi Report 3 ghi khác.

Theo [development workflow](../DEVELOPMENT_WORKFLOW.md#21-changing-a-requirement-during-implementation),
BE/FE được điều chỉnh yêu cầu trong phạm vi UC và ghi vào Jira; BA chốt UC/diagram/BR sau tích hợp.
Không coi tài liệu chậm cập nhật là lý do tự động chặn implementation. Khi nguồn mâu thuẫn hoặc
chưa xác nhận, ghi `SOURCE_CONFLICT` / `NEEDS REVIEW`; class/DTO/interface mới chưa có bằng chứng
phải ghi `PROPOSED / NEEDS APPROVAL`, không trình bày như thiết kế đã chốt.

## 3. Quy trình tạo và sửa DCD

Luồng mặc định: **UC + BR → Sequence → Class → đối chiếu hai chiều**; có thể quay lại sửa artifact
trước khi thiết kế phát hiện mâu thuẫn. Duy trì một định nghĩa canonical cho mỗi logical type
trong feature, gộp operation từ tất cả UC. Domain diagram chỉ mô tả entity/value object; DCD theo
feature có thể gồm Boundary, Controller, Service, Repository và các type liên quan.

| Bước | Việc cần làm | Bằng chứng cần giữ |
|---|---|---|
| P0 · Phạm vi | Chốt feature, UC, phiên bản SRS, source diagram, sequence thiếu/chưa duyệt | Danh sách source và giới hạn review |
| P1 · Yêu cầu | Đọc actor, pre/postcondition, main/alternative/exception flow, BR và side effect | UC → hành vi/BR; audit, email, notification, kết thúc phiên nếu có |
| P2 · Lifeline | Chuẩn hóa alias thành type, phân loại và gộp instance trùng type | Lifeline → class/interface hoặc lý do loại |
| P3 · Operation | Ánh xạ lời gọi có ý nghĩa vào đúng class nhận message | Sequence/message → receiver → operation; chữ ký chưa biết phải ghi rõ |
| P4 · Cấu trúc | Bổ sung field/type/quan hệ từ domain, BR, kiến trúc hoặc code có căn cứ | Catalogue type và nguồn cho từng bổ sung |
| P5 · Vẽ | Sửa cục bộ, giữ ID, nội dung không liên quan và bố cục thủ công | Source editable và PNG đồng bộ |
| P6 · Đối chiếu | Chạy Gates A–F, ghi mâu thuẫn và kết luận có giới hạn | Traceability, discrepancy log, trạng thái review |

### Ánh xạ lifeline và message

- `:UserService` là ký hiệu instance ở sequence; tên class là `UserService`, bỏ dấu `:`.
  Hai instance `a:User`, `b:User` vẫn chỉ cho một type `User`.
- Actor là vai trò bên ngoài; Database là lifeline hạ tầng. Không tự tạo class `Admin`, `Database`
  hay kế thừa `Admin extends User`. Google là external system; gateway/interface chỉ thêm khi có
  căn cứ kiến trúc. DTO/enum/entity có thể cần trong DCD dù không là lifeline.
- Với `Controller -> Service: createUser(...)`, operation thuộc **Service**. Không sao chép cùng
  một method vào mọi tầng. Thông điệp ngôn ngữ tự nhiên chỉ chứng minh trách nhiệm, chưa chứng minh
  tên method, kiểu tham số hay kiểu trả về.
- Return, SQL I/O, activation, `alt`/`loop` và note không tự sinh operation. Self-call chỉ thành
  private operation khi cần thể hiện trách nhiệm nội bộ; thao tác UI có thể được ghi là UI event.
- Không suy ra `bool`/`void`, DTO, interface, field, FK, enum hay business rule từ tên action.
  Được ẩn getter/setter và chi tiết framework; không ẩn operation nghiệp vụ cần để hiểu sequence.

### Tên, chữ ký và trình bày

- Type dùng `PascalCase`; field/operation dùng `camelCase`. Chữ ký:
  `+ getUser(userId: UUID): User?`. `+` public, `-` private, `#` protected, `~` package.
  Attribute OO mặc định `- name: Type`; enum ghi literal đã có nguồn. Thống nhất type và nullability.
- Stereotype dùng `<<boundary>>`, `<<control>>`, `<<service>>`, `<<repository>>`, `<<entity>>`,
  `<<interface>>`, `<<enumeration>>`; đây là legend của dự án, không thay thế ngữ nghĩa UML.
- Căn giữa stereotype/tên, căn trái member; tách rõ name/attribute/operation compartment. Chiều
  ngang vừa dòng dài nhất với lề nhỏ; không cắt chữ, chồng class/label hay để cạnh xuyên class.
- Giữ style hiện có; FE-05/06 là mẫu bố cục và typography, **không phải bảo chứng đúng toàn bộ
  member/ngữ nghĩa**. Đường nối rõ đầu cuối, layer có khoảng cách; không thêm màu hoặc package dư.
- Controller có thể biểu diễn vai trò của Minimal API endpoint, không bắt buộc class kế thừa
  `ControllerBase`. Có thể bỏ hậu tố `Async`/`Task` khi diagram là design; không tạo interface chỉ
  vì một framework thường dùng interface. Repository xác thực và quản trị là các abstraction
  khác phạm vi, không gộp chỉ vì tên gần giống.

## 4. Quan hệ UML

| Quan hệ | Ký hiệu và hướng | Điều kiện sử dụng |
|---|---|---|
| Association | Nét liền; navigability nếu cần | Tham chiếu/cấu trúc giữa instance |
| Dependency | Nét đứt, đầu mũi tên mở; client → supplier | Sự phụ thuộc đáng thể hiện; không vẽ mọi call tạm thời |
| Generalization | Nét liền, tam giác rỗng trỏ về parent | Quan hệ subtype thực sự |
| Interface Realization | Nét đứt, tam giác rỗng trỏ về interface | Implementation của interface đã có căn cứ |
| Aggregation | Nét liền, hình thoi rỗng ở whole | Whole–part chia sẻ có căn cứ; chưa rõ thì dùng association |
| Composition | Nét liền, hình thoi đặc ở owner | Sở hữu part với ràng buộc lifetime; không dùng cho “gọi service” |

Multiplicity (`1`, `0..1`, `0..*`, `1..*`, `m..n`) đặt **gần đầu association tương ứng**:
số tại đầu đó đếm instance tại đầu đó với một instance ở đầu đối diện. Không đặt multiplicity
trên dependency để diễn tả tần suất gọi. Nhãn `uses`/`manages` không thay thế ký hiệu quan hệ.
Không đưa activation bar, return message hay khung điều khiển sequence vào DCD.

Nguồn chuẩn notation: [OMG UML 2.5.1](https://www.omg.org/spec/UML/2.5.1/).
Hướng dẫn thiết kế bổ trợ: [Larman §16.21](https://www.informit.com/articles/article.aspx?p=1398623&seqNum=22),
[Operations and Methods §16.6](https://www.informit.com/articles/article.aspx?p=1398623&seqNum=7) và
[Fowler: Dependency and Association](https://martinfowler.com/bliki/DependencyAndAssociation.html).
Quy trình, traceability và style ở đây là quy ước TrustCabinet, không phải tất cả đều do OMG bắt buộc.

## 5. Kiểm tra trước khi bàn giao

| Gate | Điều kiện |
|---|---|
| A · Yêu cầu | Đúng UC/phiên bản; có sequence hoặc `MISSING_SEQUENCE`; đủ hành vi và side effect; không bịa yêu cầu |
| B · Traceability | Mọi lifeline nội bộ có mapping hoặc lý do loại; call quan trọng có receiver operation; không copy return/SQL thành method; type canonical thống nhất |
| C · Domain/type | Field, enum, ID, nullability, tham số/return và quyền khớp nguồn; proposed type và nguồn mâu thuẫn được ghi rõ |
| D · UML | Đúng kiểu/hướng arrow, ownership, stereotype; multiplicity có căn cứ và đặt đúng đầu; không nhầm actor role với class inheritance |
| E · Source/ảnh | XML hợp lệ, ID duy nhất, reference tồn tại; render và xem ảnh; không cắt/chồng chữ; PNG mở được, model nhúng khớp source |
| F · Đối chiếu cuối | Class ↔ Sequence ↔ UC/BR ↔ ERD; code nếu as-built; log nguồn mâu thuẫn và giới hạn kiểm tra |

Kết quả chỉ là `PASS` khi đủ bằng chứng cho tất cả gate; `NEEDS REVIEW` khi nguồn/chữ ký/phê duyệt
chưa đủ; `FAIL` khi xác định sai hành vi hoặc notation. Báo cáo phải có feature/UC, phiên bản SRS,
sequence đã đọc, mapping lớp/operation và lý do loại, quan hệ đã kiểm tra, discrepancy, file thay đổi
và kết luận. Không ghi “fully verified” khi chưa đọc đủ source.

Khi sửa `.drawio`, dùng [drawio-skill](../../.agents/skills/drawio-skill/SKILL.md) và
[DRAWIO_EDITING_RULES.md](../../DRAWIO_EDITING_RULES.md). Chạy validator của skill và `git diff --check`;
render draft không nhúng XML để xem, rồi xuất PNG editable. Với draw.io 29.6.1 trên máy hiện tại,
`-e` có thể sinh PNG thiếu IEND và metadata zTXt dùng raw DEFLATE/CRC sai: phải sửa container,
kiểm tra giải mã PNG **và mở lại model bằng draw.io** trước khi bàn giao. Dạng nhúng đã kiểm tra
được là chunk `tEXt`, keyword `mxfile`, nội dung XML qua `encodeURIComponent`, theo
[file format của draw.io](https://github.com/jgraph/drawio/wiki/File-Format).

## 6. Đồng bộ theo Report 3 ngày 18/10/2026

Cập nhật 19/10/2026 theo UC/BR/MSG mới (trao đổi PO 16/10/2026, góp ý hội đồng lần 1). Chỉ sửa cục bộ member, thêm
class và cạnh cần thiết; giữ ID, bố cục và quan hệ cũ.

- **FE-04:** thêm `cancelMeeting` ở boundary/controller/service (UC-75), `saveCancellation` ở `MeetingRepository`,
  `notifyMeetingCancelled`, `queueCancellationEmails`; enum `MeetingStatus` thêm `Cancelled`. Lý do, người huỷ và thời
  điểm nằm ở `MeetingStatusHistory` (BR-298), nên `Meeting` không thêm field.
- **FE-05:** `AgendaItem` bỏ `plannedDurationMinutes`, `elapsedDuration`; thêm `conclusion`, `conclusionSavedById`,
  `conclusionSavedAt` (BR-309 → BR-311). Thêm `recordAgendaItemConclusion` (UC-77), `saveConclusion`,
  `verifyPresenter` (BR-306), class `MinutesRepository` (kiểm tra đã sinh nháp biên bản, BR-310) và `NotificationService`
  (báo Presenter, BR-307). Field Presenter cho Guest vẫn là `presenterId? / presenterName?` như Entity 9 — **NEEDS REVIEW**
  khi Report 3 §I.3.2 cập nhật entity.
- **FE-06:** thêm `handOverChair` (UC-73) ở boundary/controller/service/repository, `notifyChairHandedOver`,
  `removeAndClearPresenter` (UC-28c, BR-308).
- **Kiểm tra:** validator 0 lỗi cho cả ba source; đã render và xem ảnh. PNG FE-04/05/06 xuất bằng draw.io 31.7.0, nhúng
  model ở chunk `zTXt` keyword `mxGraphModel` (khác dạng `tEXt`/`mxfile` ghi ở §5). Đã giải mã chunk và đối chiếu
  nội dung với source; **chưa** mở lại PNG bằng draw.io để kiểm tra roundtrip.

## 6b. FE-07 — tài liệu cuộc họp · 22/10/2026

Vẽ mới từ UC-32a, UC-32c, UC-35 (bản sửa 22/10/2026) và ba sequence cùng tên trong
[document-management](../06-sequencediagram/document-management/README.md). Entity theo Report 3 §I.3.2 (Entity 19,
20, 20a, 21) — tức mô hình **đích**, chưa phải code: code vẫn là `Document.MeetingId` + `DocumentVersion`, migration làm
khi triển khai SCRUM-58/SCRUM-21. `MeetingAccessPolicy` dùng chung với FE-05; `FileStorage` là trách nhiệm đọc object
trong kho đối tượng, chưa có class tương ứng (**PROPOSED**). `MeetingDocumentGroups`, `FileContent` là DTO trả về,
không vẽ thành class. `AuditLogRepository` phục vụ BR-318. Validator 0 lỗi; đã render và xem ảnh. PNG xuất bằng draw.io
desktop, nhúng model ở chunk `zTXt`; chưa mở lại PNG bằng draw.io để kiểm tra roundtrip.

## 7. Rà soát hiện trạng · 14/10/2026

Đã đọc 7 source DCD, 44 sequence `.txt` theo feature, các README tương ứng, UC/BR/Entity Descriptions
trong snapshot Report 3 local và các điểm implementation được dẫn trong log bên dưới. Snapshot
Report 3 và một số tài liệu yêu cầu đang có thay đổi chưa commit; lần rà soát này không xác nhận
phê duyệt trên Jira và không đóng dấu BA Final Review.

- Cả 7 source XML có ID duy nhất, không có edge reference hỏng; validator báo **0 lỗi, 0 cảnh báo**.
  103 quan hệ được kiểm tra kiểu arrow; không thấy dùng inheritance/realization sai ký hiệu.
- Lifeline nội bộ của cả 44 sequence đều có type tương ứng sau chuẩn hóa alias. Actor/Database
  được loại khỏi mapping class; Google vẫn là external system. Không tìm thấy duplicate class
  trong một feature; field khác nhau giữa các view được ghi ở DCD-02.
- Có 33 call ghi rõ tên operation, 24 call trùng tên operation của receiver. 9 call còn lại thuộc
  FE-05/06 ở DCD-06/07. Các message ngôn ngữ tự nhiên được đối chiếu trách nhiệm, chưa chứng minh
  toàn bộ chữ ký hay tất cả guard nghiệp vụ; bảng dưới ghi operation chính của từng UC.
- Đã render mới và xem cả 7 diagram: chưa thấy cắt chữ, chồng class hoặc cạnh xuyên class.
  Multiplicity ở FE-05/06 cần chỉnh cách ghi theo DCD-09; validator hình học không xác minh được
  ý nghĩa này. Các kiểu dependency/association/composition hiện dùng có hình dạng phù hợp.
- **6/7 PNG cũ không giải mã được**; đã xuất lại FE-01/02/03/04/06/13 từ source không đổi.
  FE-05 hiển thị được nhưng draw.io không đọc được model nhúng, nên sửa riêng metadata và giữ
  nguyên pixel. Cả 7 PNG được chuẩn hóa metadata theo định dạng editable của draw.io.
  Sau sửa: 7/7 ảnh giải mã đầy đủ, XML nhúng khớp source. Đã dùng draw.io mở từng PNG và xuất lại
  XML: ID, member, style, source/target và geometry đều khớp source, chỉ khác cách viết số tương
  đương như `505.0`/`505` và thứ tự attribute/whitespace của XML.

| Feature | Sequence đã đối chiếu | Kết luận thiết kế | Điểm cần xử lý |
|---|---|---|---|
| FE-01 | 4 | NEEDS REVIEW | DCD-02/03/04 |
| FE-02 | 7 | NEEDS REVIEW | DCD-02/05 |
| FE-03 | 6 | NEEDS REVIEW | DCD-08/10 |
| FE-04 | 9 | NEEDS REVIEW | DCD-11 |
| FE-05 | 7 | NEEDS REVIEW | DCD-06/09 |
| FE-06 | 7 | NEEDS REVIEW | DCD-07/09/12 |
| FE-13 | 4 | NEEDS REVIEW | DCD-02/10/13 |

Kết quả Gates A–F: **A NEEDS REVIEW** (số UC/phê duyệt nguồn), **B NEEDS REVIEW** (operation gộp
và chữ ký chưa đủ), **C NEEDS REVIEW** (field/nullability/domain), **D NEEDS REVIEW** (multiplicity
FE-05/06; các kiểu arrow đã kiểm tra phù hợp), **E PASS** (source, render, PNG và native roundtrip),
**F NEEDS REVIEW** (các xung đột dưới đây còn mở). Đây là kết quả review tài liệu, không phải kết
luận implementation đã vi phạm yêu cầu hay yêu cầu BA sửa trước khi BE/FE hoàn thành.

### Discrepancy log

| ID | Bằng chứng và ảnh hưởng | Hướng xử lý / trạng thái |
|---|---|---|
| DCD-01 | PNG FE-01/02/03/04/06/13 lỗi metadata/container, bộ đọc PNG không mở được. FE-05 hiển thị được nhưng không mở lại model bằng draw.io. | **FIXED:** xuất lại 6 ảnh, sửa metadata cả 7 ảnh và kiểm tra PNG → XML bằng draw.io; source DCD không đổi. |
| DCD-02 | `User` ở FE-01 (`user`), FE-02 (`user`), FE-13 (`c7`) có `username`, `status`/`AccountStatus`; FE-04/06 dùng `isActive`. Report 3 §I.3.2 Entity 1 và `Domain/Entities/User.cs` dùng Email, GoogleSubjectId, IsActive; PasswordHash/GoogleSubjectId nullable. Các projection được phép bỏ field, nhưng không được tự thay biểu diễn trạng thái. UC-01 mới dùng email; sequence UC01 và DCD vẫn dùng username. | **SOURCE_CONFLICT:** thống nhất conceptual model và tên đăng nhập giữa UC, sequence, DCD; phân biệt alias với field thật. Chưa sửa ngữ nghĩa nguồn. |
| DCD-03 | FE-01 `authController`/`authService` nhận `googleAccount: string`, `userRepository.findByGoogleAccount`; UC02 sequence cũng dùng tên chung này. Code `AuthenticationService.SignInWithGoogleAsync` xác minh `idToken` qua `googleTokenVerifier.VerifyAsync`, rồi tra subject. Diagram chưa giải thích được bước xác minh/định danh. | **NEEDS REVIEW:** làm rõ token/gateway và subject theo thiết kế được chốt; không kết luận diagram đã chứng minh an toàn chỉ từ tên `googleAccount`. |
| DCD-04 | UC03 sequence và FE-01 `authenticatedPage` chỉ thể hiện xóa JWT ở client. `Web/Endpoints/AuthEndpoints.cs.SignOutAsync` gọi `AuthenticationService.SignOutAsync` để xử lý refresh token phía server. | **DESIGN/CODE DIVERGENCE:** ghi rõ scope sign-out; đồng bộ UC/sequence trước khi bổ sung collaborator vào DCD. |
| DCD-05 | FE-02 có `SessionService.endActiveSessions` và UC06c đã gọi collaborator này: ghi chú cũ “thiếu hoàn toàn TokenService” không còn đúng với source hiện tại. Code `Users/UserService.cs` có các lần `refreshTokens.RevokeAllForUserAsync`. | **NEEDS REVIEW:** phân biệt trách nhiệm kết thúc phiên với tên implementation; rà soát trigger UC06b/06c/08 theo yêu cầu, không đổi tên máy móc. Xem phụ lục token bên dưới. |
| DCD-06 | FE-05 `service` chỉ ghi `create / update / delete agenda item`; UC23a/b/c gọi `createAgendaItem`, `updateAgendaItem`, `deleteAgendaItem` riêng. Repository còn dòng `saveOrder / saveExecutionState`; nhiều chữ ký thiếu type/return. `AgendaItem.elapsedDuration` chưa đánh dấu derived. | **NEEDS REVIEW:** khai báo từng operation theo receiver và chữ ký có căn cứ; elapsed duration là giá trị tính từ thời điểm, không ngầm thành cột persisted. |
| DCD-07 | FE-06 `service`: UC27 gọi `viewMeetingParticipants`, UC28a `addMeetingParticipants`, UC28b `manageMeetingRoleAndPermissions`, UC28c `removeMeetingParticipant`, UC29 `respondToMeetingInvitation`, UC30 `viewInvitationResponses`; DCD dùng alias hoặc câu gộp. Chỉ UC31 `recordMeetingAttendance` trùng tên. | **NEEDS REVIEW:** thống nhất operation canonical với 7 sequence; ghi rõ bulk input, kiểu trả về và atomic-save thay vì suy đoán từ câu gộp. |
| DCD-08 | FE-03 `rolePermissionController`/`rolePermissionService`/`rolePermissionRepository` có `resetRolePermissionsToDefault`, boundary có `confirmResetToDefault`. UC-11 Report 3 không có alternative reset; sequence UC11 cũng không gọi reset. | **UNTRACED OPERATION:** cần nguồn quyết định/UC hỗ trợ hoặc đánh dấu proposed/loại bỏ sau khi chốt; không coi operation đã được duyệt. |
| DCD-09 | FE-05 `a15/a16` và FE-06 `p12/p13` gộp multiplicity vào label giữa cạnh (`1 contains 0..*`, `0..1 linked agenda item`...). Hình thoi composition đặt phía Meeting, nhưng nhãn giữa cạnh không xác định rõ từng association end. | **NEEDS REVIEW:** chuyển số ra đúng đầu association, xác nhận cardinality với data model; không tự đoán số còn thiếu. |
| DCD-10 | Role sequence: UC10 có title UC-08, UC11 title UC-10, UC12a title UC-11, UC12b title UC-12a, UC12c title UC-12b. Notification UC66/67a/67b/68 có title UC-17/18/19/20. File và inventory đã theo số mới. | **SOURCE_CONFLICT:** sửa title trong nguồn sequence và đồng bộ ảnh/tab trong task sequence; lần này không sửa các source đó. |
| DCD-11 | FE-04 `meeting.actualStartTime/actualEndTime: DateTime` là non-null; Report 3 Entity 4 và `Domain/Entities/Meeting.cs` dùng ActualStartedAt/ActualEndedAt nullable. Method dùng AgendaItem nhưng không có định nghĩa type trong view. | **NEEDS REVIEW:** thống nhất alias/nullability; dẫn nguồn cho type được lược khỏi view. Không đưa MeetingSession trở lại. |
| DCD-12 | FE-06 `guest` chưa có Attendance, AttendanceRecordedById/At; `participant` cũng thiếu recorder/time. Report 3 Entity 7/8 và UC31 yêu cầu lưu attendance cho cả internal/Guest kèm Secretary và thời điểm. Entity 7a MeetingParticipantPermission đã có trong snapshot nhưng DCD chưa mô tả type hay nơi lưu grant bổ sung. | **NEEDS REVIEW:** làm rõ projection và persistence responsibility cho attendance/grants. Ma trận PermissionCode chưa chốt nên không tự thiết kế enum/schema mới. |
| DCD-13 | FE-13 `c6` có recipientUserId/kind/actionUrl; Entity 28 Report 3 dùng UserId/MeetingId/TargetType/TargetId/ReadAt; `Domain/Entities/Notification.cs` có Type, Title, Body, TargetId nullable và không có ActionUrl persisted. UC68 sequence còn “linked meeting or task”, trái ranh giới scope. | **SOURCE_CONFLICT:** phân biệt domain field với response/computed URL; loại task tracking khi chỉnh sequence theo nguồn đã chốt. |

### UC → Sequence → Class → Operation

Các đường dẫn là source đã đọc. Cột operation ghi **operation chính hiện có trong DCD**; dấu `≈`
là mapping trách nhiệm từ message tự nhiên/alias, không phải chứng minh trùng chữ ký. `Thiếu`
ghĩa là source sequence có call nhưng DCD chưa khai báo riêng. Các collaborator persistence,
audit/email/session và các mâu thuẫn còn lại được ghi ở log; bảng này không thay thế trace từng
message khi sửa thiết kế. Tất cả feature còn `NEEDS REVIEW` theo bảng kết luận phía trên.

| UC / feature | Sequence | Class → operation chính | Đối chiếu |
|---|---|---|---|
| UC-01 / FE-01 | [UC01_SignIn.txt](../06-sequencediagram/auth/UC01_SignIn.txt) | `AuthenticationService → signIn` | Trùng tên; DCD-02 |
| UC-02 / FE-01 | [UC02_SignInWithGoogle.txt](../06-sequencediagram/auth/UC02_SignInWithGoogle.txt) | `AuthenticationService → signInWithGoogle` | Trùng tên; DCD-03 |
| UC-03 / FE-01 | [UC03_SignOut.txt](../06-sequencediagram/auth/UC03_SignOut.txt) | `AuthenticatedPage → signOut / removeJwtToken` | ≈ UI event; DCD-04 |
| UC-04 / FE-01 | [UC04_AccessMeetingViaGuestInvitation.txt](../06-sequencediagram/auth/UC04_AccessMeetingViaGuestInvitation.txt) | `GuestAccessService → checkInvitationAndAccess / verifyCode / completeGuestAccess` | ≈ luồng truy cập Guest |
| UC-05 / FE-02 | [UC05_ViewUserAccount.txt](../06-sequencediagram/user-management/UC05_ViewUserAccount.txt) | `UserService → getUserAccounts / getUser` | ≈ đọc tài khoản; DCD-02 |
| UC-06a / FE-02 | [UC06a_CreateUserAccount.txt](../06-sequencediagram/user-management/UC06a_CreateUserAccount.txt) | `UserService → createUser` | ≈ tạo; audit |
| UC-06b / FE-02 | [UC06b_UpdateUserAccount.txt](../06-sequencediagram/user-management/UC06b_UpdateUserAccount.txt) | `UserService → updateUser` | ≈ sửa; DCD-05 |
| UC-06c / FE-02 | [UC06c_DeactivateReactivateUserAccount.txt](../06-sequencediagram/user-management/UC06c_DeactivateReactivateUserAccount.txt) | `UserService → changeStatus; SessionService → endActiveSessions` | ≈ đổi trạng thái; DCD-02/05 |
| UC-07 / FE-02 | [UC07_ResetUserPassword.txt](../06-sequencediagram/user-management/UC07_ResetUserPassword.txt) | `UserService → requestPasswordReset` | Trùng tên; token store/email/audit có collaborator |
| UC-08 / FE-02 | [UC08_AssignSystemRoleToUser.txt](../06-sequencediagram/user-management/UC08_AssignSystemRoleToUser.txt) | `UserService → assignSystemRole` | Trùng tên; DCD-05 |
| UC-09 / FE-02 | [UC09_UnlockUserAccount.txt](../06-sequencediagram/user-management/UC09_UnlockUserAccount.txt) | `UserService → unlockUserAccount; UserAccountRepository → findById` | Trùng tên; lock/audit có collaborator |
| UC-10 / FE-03 | [UC10_ViewRolePermissionMatrix.txt](../06-sequencediagram/role-management/UC10_ViewRolePermissionMatrix.txt) | `RolePermissionService → getRolePermissionMatrix` | ≈ đọc; DCD-10 |
| UC-11 / FE-03 | [UC11_ConfigureRolePermissions.txt](../06-sequencediagram/role-management/UC11_ConfigureRolePermissions.txt) | `RolePermissionService → updateRolePermissions` | ≈ sửa; DCD-08/10 |
| UC-12a / FE-03 | [UC12a_CreateRole.txt](../06-sequencediagram/role-management/UC12a_CreateRole.txt) | `RoleService → createRole` | ≈ tạo; DCD-10 |
| UC-12b / FE-03 | [UC12b_UpdateRole.txt](../06-sequencediagram/role-management/UC12b_UpdateRole.txt) | `RoleService → updateRole` | ≈ sửa; DCD-10 |
| UC-12c / FE-03 | [UC12c_DeleteRole.txt](../06-sequencediagram/role-management/UC12c_DeleteRole.txt) | `RoleService → deleteRole` | ≈ xóa; DCD-10 |
| UC-13 / FE-03 | [UC13_ViewAuditLog.txt](../06-sequencediagram/role-management/UC13_ViewAuditLog.txt) | `AuditLogQueryService → getAuditEntries; AuditLogRepository → findReadOnlyPage` | Trùng tên |
| UC-14 / FE-04 | [UC14_CreateMeeting.txt](../06-sequencediagram/meeting-management/UC14_CreateMeeting.txt) | `MeetingService → createMeeting` | ≈ tạo |
| UC-15 / FE-04 | [UC15_UpdateMeeting.txt](../06-sequencediagram/meeting-management/UC15_UpdateMeeting.txt) | `MeetingService → updateMeeting / rescheduleMeeting` | ≈ cập nhật lịch |
| UC-16 / FE-04 | [UC16_ViewMeetingDetails.txt](../06-sequencediagram/meeting-management/UC16_ViewMeetingDetails.txt) | `MeetingService → getMeetingDetails` | Trùng tên |
| UC-17 / FE-04 | [UC17_ViewMeetingList.txt](../06-sequencediagram/meeting-management/UC17_ViewMeetingList.txt) | `MeetingService → getMeetingList` | Trùng tên |
| UC-18 / FE-04 | [UC18_SubmitMeetingForApproval.txt](../06-sequencediagram/meeting-management/UC18_SubmitMeetingForApproval.txt) | `MeetingService → prepareMeetingSubmission / submitMeetingForApproval` | Trùng tên |
| UC-19 / FE-04 | [UC19_ReviewMeetingApproval.txt](../06-sequencediagram/meeting-management/UC19_ReviewMeetingApproval.txt) | `MeetingService → reviewMeetingApproval` | Trùng tên |
| UC-20 / FE-04 | [UC20_PublishMeeting.txt](../06-sequencediagram/meeting-management/UC20_PublishMeeting.txt) | `MeetingService → publishMeeting` | Trùng tên; notification/invitation có collaborator |
| UC-21 / FE-04 | [UC21_StartMeeting.txt](../06-sequencediagram/meeting-management/UC21_StartMeeting.txt) | `MeetingService → startMeeting` | Trùng tên |
| UC-22 / FE-04 | [UC22_EndMeeting.txt](../06-sequencediagram/meeting-management/UC22_EndMeeting.txt) | `MeetingService → endMeeting` | Trùng tên; room/repository có collaborator |
| UC-23a / FE-05 | [UC23a_CreateAgendaItem.txt](../06-sequencediagram/agenda-management/UC23a_CreateAgendaItem.txt) | `AgendaService → create / update / delete agenda item` | Thiếu createAgendaItem; DCD-06 |
| UC-23b / FE-05 | [UC23b_UpdateAgendaItem.txt](../06-sequencediagram/agenda-management/UC23b_UpdateAgendaItem.txt) | `AgendaService → create / update / delete agenda item` | Thiếu updateAgendaItem; DCD-06 |
| UC-23c / FE-05 | [UC23c_DeleteAgendaItem.txt](../06-sequencediagram/agenda-management/UC23c_DeleteAgendaItem.txt) | `AgendaService → create / update / delete agenda item` | Thiếu deleteAgendaItem; DCD-06 |
| UC-23d / FE-05 | [UC23d_ReorderAgendaItems.txt](../06-sequencediagram/agenda-management/UC23d_ReorderAgendaItems.txt) | `AgendaService → reorderAgendaItems` | Trùng tên; chữ ký thiếu type |
| UC-24 / FE-05 | [UC24_SetCurrentAgendaItem.txt](../06-sequencediagram/agenda-management/UC24_SetCurrentAgendaItem.txt) | `AgendaService → setCurrentAgendaItem` | Trùng tên; chữ ký thiếu type |
| UC-25 / FE-05 | [UC25_CompleteAgendaItem.txt](../06-sequencediagram/agenda-management/UC25_CompleteAgendaItem.txt) | `AgendaService → completeAgendaItem` | Trùng tên; chữ ký thiếu type |
| UC-26 / FE-05 | [UC26_ViewMeetingAgenda.txt](../06-sequencediagram/agenda-management/UC26_ViewMeetingAgenda.txt) | `AgendaService → getMeetingAgenda; MeetingAccessPolicy → canViewMeeting` | Trùng tên service; ≈ policy; DCD-06 |
| UC-27 / FE-06 | [UC27_ViewMeetingParticipants.txt](../06-sequencediagram/meeting-participant-management/UC27_ViewMeetingParticipants.txt) | `MeetingParticipantService → listParticipants` | ≈ viewMeetingParticipants; DCD-07 |
| UC-28a / FE-06 | [UC28a_AddMeetingParticipant.txt](../06-sequencediagram/meeting-participant-management/UC28a_AddMeetingParticipant.txt) | `MeetingParticipantService → add / manageRoleAndPermissions / remove participant` | Thiếu addMeetingParticipants riêng; DCD-07 |
| UC-28b / FE-06 | [UC28b_AssignChangeMeetingRole.txt](../06-sequencediagram/meeting-participant-management/UC28b_AssignChangeMeetingRole.txt) | `MeetingParticipantService → add / manageRoleAndPermissions / remove participant` | ≈ manageMeetingRoleAndPermissions; DCD-07 |
| UC-28c / FE-06 | [UC28c_RemoveMeetingParticipant.txt](../06-sequencediagram/meeting-participant-management/UC28c_RemoveMeetingParticipant.txt) | `MeetingParticipantService → add / manageRoleAndPermissions / remove participant` | Thiếu removeMeetingParticipant riêng; DCD-07 |
| UC-29 / FE-06 | [UC29_RespondToMeetingInvitation.txt](../06-sequencediagram/meeting-participant-management/UC29_RespondToMeetingInvitation.txt) | `MeetingParticipantService → respondToInvitation` | ≈ respondToMeetingInvitation; DCD-07 |
| UC-30 / FE-06 | [UC30_ViewParticipantResponseStatus.txt](../06-sequencediagram/meeting-participant-management/UC30_ViewParticipantResponseStatus.txt) | `MeetingParticipantService → getResponseStatus` | ≈ viewInvitationResponses; DCD-07 |
| UC-31 / FE-06 | [UC31_RecordAttendance.txt](../06-sequencediagram/meeting-participant-management/UC31_RecordAttendance.txt) | `MeetingParticipantService → recordMeetingAttendance` | Trùng tên; DCD-12 |
| UC-66 / FE-13 | [UC66_ViewNotifications.txt](../06-sequencediagram/notification-management/UC66_ViewNotifications.txt) | `NotificationService → getNotifications / getUnreadCount` | ≈ đọc; DCD-10/13 |
| UC-67a / FE-13 | [UC67a_MarkNotificationAsRead.txt](../06-sequencediagram/notification-management/UC67a_MarkNotificationAsRead.txt) | `NotificationService → markAsRead` | ≈ đánh dấu đã đọc; DCD-10 |
| UC-67b / FE-13 | [UC67b_MarkAllNotificationsAsRead.txt](../06-sequencediagram/notification-management/UC67b_MarkAllNotificationsAsRead.txt) | `NotificationService → markAllAsRead` | ≈ đánh dấu tất cả; DCD-10 |
| UC-68 / FE-13 | [UC68_OpenNotificationTarget.txt](../06-sequencediagram/notification-management/UC68_OpenNotificationTarget.txt) | `NotificationService → openTarget` | ≈ điều hướng; DCD-10/13 |

<a id="phụ-lục--có-dùng-jwt-không-mà-sao-lại-có-session-server"></a>

## 8. Ghi chú tương thích với các tài liệu cũ

Các ghi chú A–F của README trước đây mô tả bản vẽ tháng 9/đầu tháng 10, không còn là checklist lỗi
hiện hành của bản vẽ ngày 13/10. Nội dung hữu ích đã được gộp vào quy ước và log phía trên; lịch sử
tracked README vẫn tra được bằng Git. Hai bản nháp `CLASS-DIAGRAM-CONVENTION.md` và
`README-CLASS-SECTION.md` đã được hợp nhất vào README này.

**Role và capability:** [ADR-0008](../05-decisions/0008-vai-tro-la-du-lieu.md) chốt Role là dữ liệu,
RoleCapability là mapping; không đổi Role trở lại enum theo đoạn review cũ. Capability không phải
bảng quyền cho admin tự tạo. Vấn đề reset quyền được giữ tại DCD-08, không coi là chức năng đã có.

**JWT và “SessionService”:** service trong diagram có thể diễn tả trách nhiệm kết thúc phiên.
Implementation hiện tại dùng access JWT và refresh token store phía server; hủy refresh token và
xóa JWT ở trình duyệt là hai việc khác nhau. Đọc `Domain/Authentication/IRefreshTokenStore.cs`,
`Infrastructure/Authentication/AuthenticationService.cs` và `Infrastructure/Users/UserService.cs`
trước khi đổi collaborator. Không suy ra có session server truyền thống từ tên class thiết kế.

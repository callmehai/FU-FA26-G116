# Kế hoạch Backend — SCRUM-31: Chuyển giọng nói thành văn bản tiếng Việt

Ngày cập nhật: **2026-09-23**. Trạng thái: **kế hoạch local, chưa triển khai, chưa chốt nhà cung cấp**.

Tài liệu này là hướng dẫn triển khai dự kiến, không thay UC Specification, Jira hoặc ADR đã được nhóm chấp nhận. Chỉ lập kế hoạch trong lượt làm việc hiện tại; không thêm endpoint, entity, migration, package, tài khoản cloud hay gửi bản ghi thật. Không commit file này theo yêu cầu lưu kế hoạch local.

## 1. Phạm vi và mức độ sẵn sàng

**Mục tiêu:** thư ký yêu cầu ASR từ một bản ghi đã có; HTTP trả sớm; hệ thống lưu và phục hồi công việc nền; người dùng xem tiến độ, đọc các đoạn có timestamp, sửa text và chạy lại khi thất bại.

Phạm vi Backend gồm API, phân quyền, trạng thái bền vững trong PostgreSQL, worker, adapter dịch vụ, chuẩn hoá kết quả, kiểm soát consent, xử lý xoá, test và hợp đồng bàn giao FE. Thêm TypeScript type/API wrapper theo AGENTS.md; giao diện, audio player và nút bấm do FE thực hiện.

Không làm ASR trực tiếp khi đang họp, tự vận hành mô hình/GPU, voiceprint, diarization/gán người nói, tóm tắt, sinh biên bản, một hệ thống hàng đợi dùng chung hoặc tự động gửi dữ liệu sang nhà cung cấp dự phòng. Các cột người nói hiện có giữ nguyên cho story khác.

**Đã xác nhận từ người dùng:** chưa có tài khoản/ngân sách ASR; chọn nhà cung cấp để sau. Không được ghi Google/Azure/Viettel là phương án đã duyệt. Tự vận hành mô hình AI nằm ngoài phạm vi yêu cầu này.

**Có thể chốt thiết kế độc lập nhà cung cấp:** hợp đồng REST dự kiến, state machine, chống trùng, phục hồi worker, chuẩn timestamp, sửa tay, test với adapter giả.

**Chưa đủ điều kiện triển khai production toàn bộ UC:** thiếu BA spec/diagram ASR, quyết định phân quyền, hợp đồng bản ghi/consent/playback và lựa chọn dịch vụ. Theo `docs/DEVELOPMENT_WORKFLOW.md`, không biến đề xuất nghiệp vụ trong kế hoạch thành luật production khi chưa được xác nhận. Khi được giao triển khai, chỉ nhận phần đã đủ đầu vào; các điểm còn mở phải được cập nhật vào kế hoạch trước phần phụ thuộc.

## 2. Căn cứ trong dự án và hiện trạng

Đã đối chiếu bộ tài liệu hiện có trong chuỗi lập kế hoạch: AGENTS.md, CLAUDE.md, README.md; toàn bộ nhóm docs 00-overview, 01-requirements, 02-design, 03-api, 04-meetings, 05-decisions, DEVELOPMENT_WORKFLOW; các sequence/class diagram hiện có và ghi chú sai khác trong 07-classdiagram. Lượt ASR rà lại trực tiếp phần vòng đời, ERD, privacy, workflow, API/auth và các entity/configuration liên quan.

| Nguồn | Ràng buộc phải giữ |
|---|---|
| `AGENTS.md` | .NET 10, PostgreSQL code-first, enum string, Guid v7, server authorization, DTO + TS wrapper + API docs, không CORS, không secret trong Git |
| `docs/DEVELOPMENT_WORKFLOW.md` | BA → BE → FE → Tester; Jira là nguồn yêu cầu; AI tạo bản nháp; nêu rõ consent và xoá bản ghi/transcript |
| `README.md`, `docs/00-overview/04-so-sanh-ai-ecabinet.md` | ASR sau họp, dịch vụ ngoài; không tự vận hành AI theo phạm vi người dùng đã chỉ định |
| `docs/02-design/01-vong-doi-cuoc-hop.md` | `Ended` dành cho hậu kỳ ASR/biên bản; `Archived` chỉ đọc; mức mật liên quan quyền đọc bản ghi |
| `docs/02-design/02-erd.md` | Có recordings → transcript → transcript_segments; một transcript/bản ghi; không job spine; không voiceprint |
| `docs/02-design/03-co-so-du-lieu.md` | Migration + ModelSnapshot, không sửa migration đã lên main, chỉ tự migrate/seed trong Development |
| ADR-0001…0004 | Giữ kiến trúc hiện có, PostgreSQL, một tổ chức, không org unit hoặc multi-tenancy |
| ADR-0005 | Khách ngoài chỉ nghe cuộc họp; không mặc nhiên đọc transcript; bằng chứng thông báo ghi âm cho khách là phần riêng |
| ADR-0006 | Quyền tài liệu đã thay đổi; không tự suy ra quyền bản ghi/transcript từ quyền Document |
| ADR-0007 và API phân quyền | Capability đọc từ ma trận đang chạy; không so sánh tên SystemRole để cấp quyền |
| API auth và note cho FE | JWT `sub`, capability PascalCase; auth thật khác scaffold FE; phải xử lý 401/403 khi quyền thay đổi |
| `docs/01-requirements/README.md`, bộ diagram | Chưa có UC spec/sequence/class diagram ASR đủ để chốt luật còn thiếu; các file được ghi “dự kiến có” không phải tài liệu đã tồn tại |

Các điểm cần ghi nhận, không tự sửa trong lượt lập kế hoạch:

1. README vẫn gọi tự vận hành AI là mục “cân nhắc thu hẹp”, trong khi tài liệu so sánh và yêu cầu hiện tại loại bỏ nó. Khi khóa scope, cập nhật requirements + README + `frontend/src/pages/scopeData.ts` nếu nội dung phạm vi ở đó thay đổi; không sửa ADR đã chấp nhận để ghi đè lịch sử.
2. AGENTS/README có mô tả skeleton cũ. Code thực tế đã có authentication, user management, role-capability APIs; chưa có business endpoint cho recording/transcript.
3. `TranscriptConfiguration` có unique `RecordingId`; `TranscriptSegmentConfiguration` unique `(TranscriptId, Position)`, text tối đa 8000. Giữ một transcript, không âm thầm tạo nhiều bản transcript cho cùng recording.
4. `Recording.ConsentObtainedAt` và `ConsentPurpose` chưa chứng minh đủ consent gửi âm thanh ra ngoài. Không suy ra consent ASR từ “đã ghi âm”, “đã hiện thông báo” hoặc một boolean client gửi.
5. `Transcript` có bốn trạng thái, provider/language/progress/error/completedAt; chưa có operation ID, retry attempt, lease hay concurrency token.
6. `AuditService` dùng cùng DbContext scoped và gọi SaveChanges. Comment nói “unit of work riêng” không phản ánh cách code đang chạy. Khi cần atomic, dùng transaction ngoài và kiểm thử rollback thực tế.
7. `frontend/src/api/client.ts` chưa giữ error body có cấu trúc và chưa tự gắn Bearer token. Không coi việc viết wrapper là đã tích hợp auth/UI xong.
8. Không tìm thấy file kế hoạch ASR cũ trong workspace. Các lựa chọn từ kế hoạch cuộc họp/nghị sự trước chỉ là bối cảnh thiết kế, không phải service đã được triển khai.

## 3. Sổ quyết định và phụ thuộc

| ID | Nội dung | Trạng thái / việc cần làm |
|---|---|---|
| D01 | Dịch vụ ngoài, không self-host | Đã xác nhận phạm vi; vendor/tài khoản/ngân sách **để sau theo người dùng** |
| D02 | Ai start/retry/edit/read | Chờ BA/người dùng; phương án tham khảo ở mục 4, chưa được xác nhận |
| D03 | Consent ra ngoài, phạm vi người nói, thu hồi, retention | Chờ hợp đồng với story bản ghi và BA; bắt buộc trước gửi dữ liệu thật |
| D04 | Quyền gửi dữ liệu với từng Sensitivity | Chờ BA. Không tự áp dụng luật mời khách của ADR-0005 hoặc quyền Document của ADR-0006 cho ASR |
| D05 | File nguồn, codec, duration chuẩn, phát lại có seek | Chờ backend bản ghi; phải có bằng chứng tích hợp, không chỉ fake |
| D06 | API sửa tay | Đề xuất thêm PUT segment bên dưới vì hai endpoint của task không hỗ trợ sửa; ghi vào API contract/UC trước triển khai |
| D07 | Hiện tiến độ không có % từ provider | Đề xuất giai đoạn + nullable %, không giả tiến độ; BA/FE xác nhận cách hiển thị |
| D08 | Retry sau submit không rõ kết quả | Không tự submit lại. Vendor spike phải xác định cách reconcile hoặc quy trình vận hành có chứng cứ trước mở retry |
| D09 | Hạn dung lượng, thời lượng, số job, timeout, retention | Giá trị production chốt sau vendor/budget; không đoán từ giới hạn của vendor khác |
| D10 | ASR khi biên bản được ban hành / sửa transcript đã dùng sinh biên bản | Đề xuất chỉ ghi trong Ended; khóa chuyển sang MinutesIssued khi ASR đang chạy; phụ thuộc owner vòng đời/biên bản xác nhận |

Các phụ thuộc có thể chuẩn bị bằng contract và test double, nhưng **test double không được đăng ký trong production**:

- Story bản ghi (entity dẫn SCRUM-29/SCRUM-44): storage reader, media metadata, quyền đọc, consent, xoá, phát lại.
- Story meetings/participants: xác định Secretary/Chair của đúng cuộc họp, trạng thái và mức mật.
- Auth/RBAC: dùng implementation hiện có; kiểm tra lại user active/capability ở DB.
- Story privacy/khách ngoài: bằng chứng người nói đồng ý xử lý bên ngoài, liên kết lượt khách nếu có; thông báo đã hiện chưa đồng nghĩa đã đồng ý.
- Story biên bản: một snapshot transcript/revision đã dùng sinh draft; sửa transcript không tự sửa biên bản, đặc biệt bản đã ban hành.

## 4. Luật nghiệp vụ đề xuất để BA xác nhận

Đây là **phương án cụ thể để review**, không phải quyết định đã có trong Jira.

| Thao tác | Luật đề xuất |
|---|---|
| Start / retry / edit | User active, thuộc `MeetingRole.Secretary` của cuộc họp, có capability `MinutesEditor` hiện tại; meeting `Ended` |
| Read | User active, Secretary hoặc Chair của cuộc họp; vượt qua policy đọc recording theo mức mật; trạng thái Ended/MinutesIssued/Archived; dữ liệu chưa bị xoá/hết hạn |
| Chủ toạ | Đọc; không tự cấp quyền sửa/start chỉ vì là Chair |
| Creator, approver toàn hệ thống, admin | Không tự có quyền transcript nếu không thỏa policy trên |
| Participant khác / khách ngoài | Không đọc bản gỡ băng nháp trong phương án này; nếu BA cho đọc thì đổi policy + test, không đổi lõi worker |
| Start | Recording Available, file bất biến tồn tại, duration hợp lệ, consent còn hiệu lực và đúng provider/purpose, retention chưa hết, provider đã cấu hình |
| Retry | Chỉ Failed, nguyên nhân đã được khắc phục, cleanup/reconcile lần trước cho phép, bản ghi/consent/quyền vẫn hợp lệ |
| Completed | Không chạy lại để ghi đè. POST trả bản hiện tại; sửa tay qua PUT |
| Edit | Completed, chưa xoá; chỉ sửa Text; không chỉnh timestamp/position/provider/speaker trong story này |
| MinutesIssued / Archived | Không nhận thao tác ghi mới; đọc theo policy. Xoá theo chính sách dữ liệu là luồng riêng, không phải sửa nội dung hồ sơ |

Phải thống nhất thứ tự kiểm tra: JWT → tài khoản → quyền đọc tài nguyên → quyền thao tác → dữ liệu/state. Không cho lỗi consent/provider tiết lộ thông tin recording mà caller không được đọc.

- `401`: thiếu JWT, JWT sai/hết hạn.
- `403`: token hợp lệ nhưng account inactive; hoặc caller được đọc tài nguyên nhưng thiếu quyền thao tác.
- `404`: ID không tồn tại hoặc không được đọc recording/transcript đó; segment không thuộc đúng transcript/recording cũng 404.
- Không so `SystemRole == Secretary`; `MeetingRole.Secretary` và capability là hai điều kiện khác nhau.

## 5. Lựa chọn dịch vụ: việc cần làm sau

Chỉ xây **một adapter thật** sau khi chọn. Không triển khai ba vendor và không thêm UI chọn vendor trong v1.

| Phương án tham khảo | Điều đã kiểm chứng | Việc chưa được phép giả định |
|---|---|---|
| Google Cloud Speech-to-Text V1 | Có `vi-VN`, long-running recognition, word offsets và progress metadata | Chất lượng audio cuộc họp của nhóm, ngân sách, endpoint xử lý, khả năng cancel/delete thực tế |
| Azure Speech batch | Có tiếng Việt, batch job/status và word timestamp options | Có % tiến độ chi tiết, tốc độ xử lý, chi phí tài khoản nhóm, việc xoá cả input blob lẫn kết quả |
| Viettel AI | Có cổng tài liệu công khai; chưa lấy được contract ASR đủ rõ trong lần tra cứu | Không tự đoán async/polling/timestamp/retention/cancel; yêu cầu tài liệu chính thức hiện hành và tài khoản thử |

Nguồn chính thức tham khảo, kiểm tra ngày 2026-09-23:

- [Google — ngôn ngữ/model](https://docs.cloud.google.com/speech-to-text/docs/v1/speech-to-text-supported-languages).
- [Google — nhận dạng file dài](https://docs.cloud.google.com/speech-to-text/docs/v1/async-recognize): V1 yêu cầu Cloud Storage cho audio dài hơn 60 giây; giới hạn long-running được tài liệu nêu là 480 phút. Đây là giới hạn tham khảo, không phải hạn mức ứng dụng đã chốt.
- [Google — progress metadata](https://docs.cloud.google.com/speech-to-text/docs/reference/rest/v1/LongRunningRecognizeMetadata) và [word timestamps](https://docs.cloud.google.com/speech-to-text/docs/v1/async-time-offsets).
- [Google — sử dụng dữ liệu](https://docs.cloud.google.com/speech-to-text/docs/v1/data-usage-faq): async transcript được giữ khoảng 5 ngày; không bật data logging opt-in; bucket input là nơi lưu riêng phải quản lý. V1 mặc định xử lý toàn cầu, không được mô tả là lưu/xử lý tại Việt Nam.
- [Google — cancel](https://docs.cloud.google.com/speech-to-text/docs/reference/rest/v1/operations/cancel), [delete operation](https://docs.cloud.google.com/speech-to-text/docs/reference/rest/v1/operations/delete): cancel chỉ best effort, delete không đồng nghĩa cancel, có thể UNIMPLEMENTED. Không hứa xoá tức thì mọi bản sao provider.
- [Azure — hỗ trợ ngôn ngữ](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/language-support), [submit batch](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/batch-transcription-create), [lấy kết quả/status](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/batch-transcription-get).
- [Viettel AI — cổng tài liệu](https://viettelai.vn/en/tai-lieu); cần lấy được đặc tả ASR trước khi lựa chọn.

Vendor spike phải bàn giao: endpoint/API version/model/locale; định dạng và hạn file; async operation ID và phục hồi; progress thật; timestamp và kết quả im lặng; retry/idempotency; cancel/delete; vị trí xử lý; input/output storage và retention; giá theo cấu hình đã chọn; quotas; kết quả thử bộ audio tiếng Việt có quyền sử dụng. Không gọi dịch vụ thật trong CI. Chọn model/locale phải được pin trong provider profile và ghi lại theo attempt; không tự nâng model khi restart.

## 6. Consent, dữ liệu ra ngoài và xoá

### 6.1 Luồng consent bắt buộc

1. Story recording cung cấp bằng chứng có thể kiểm chứng cho **những người có giọng trong file**, kể cả file upload từ ngoài và khách không có tài khoản. Không dùng danh sách người được mời làm bằng chứng họ đều có mặt/đã đồng ý.
2. Nội dung consent nêu audio rời TrustCabinet; nhà cung cấp và nơi xử lý; mục đích gỡ băng; bên nhận/storage phụ trợ; thời gian lưu; cách thu hồi/yêu cầu xoá và giới hạn kỹ thuật của provider.
3. Backend tra bằng chứng từ kho dữ liệu đáng tin cậy. Request start không nhận `consent: true` như một bằng chứng, không nhận callerId/provider/URL tùy ý.
4. Lưu snapshot các **tham chiếu** bằng chứng và phiên bản notice/provider profile theo attempt. Không sao chép tên người nói hoặc text transcript vào audit.
5. Kiểm tra tại start, trước mỗi upload/submit và trước publish kết quả. Chỉ submit sau khi policy trả Allowed cho đúng identity file + provider profile.
6. Nếu consent chưa có/không bao phủ ASR ngoài: từ chối bằng lỗi có hướng xử lý. Không tự điền ConsentObtainedAt hoặc tạo bằng chứng giả để demo.

Mẫu nội dung cần BA hoàn thiện, không dùng placeholder để xin consent thật:

> Âm thanh của bản ghi sẽ được gửi ra ngoài TrustCabinet tới [nhà cung cấp], xử lý tại [phạm vi xử lý], để tạo bản gỡ băng tự động. Dữ liệu và kết quả được lưu [thời hạn/chính sách đã kiểm chứng]. Bạn có thể yêu cầu thu hồi/xoá qua [kênh xử lý]; [nêu giới hạn xử lý/xoá của dịch vụ nếu có].

Tài liệu dự án viện dẫn Nghị định 13/2023/NĐ-CP. Đây là yêu cầu thiết kế theo tài liệu, không phải kết luận rằng vài field consent đã đủ đáp ứng mọi nghĩa vụ pháp lý. BA/owner dữ liệu phải rà soát chính sách áp dụng tại thời điểm vận hành.

### 6.2 Thu hồi, hết hạn và xoá

- Phân biệt `RevokedAt` (cấm xử lý thêm) với `DeletedAt` (đã thực sự xoá dữ liệu thuộc phạm vi đó). Không đánh dấu xoá bytes chỉ vì đã đưa lệnh vào queue.
- Thu hồi/xoá/hết hạn: chặn GET text và phát lại theo policy; đánh dấu Cancelled cho job còn hoạt động; loại bỏ segment text, temp file và input/output trên storage ngoài theo hợp đồng đã chốt. Giữ metadata tối thiểu để theo dõi cleanup, không giữ raw transcript trong audit/log.
- Job Completed cần xoá transcript thì đặt Transcript.DeletedAt sau khi xoá segments, không biến thành “ASR thất bại”. Recording.DeletedAt vẫn do luồng xoá bytes của recording quản lý.
- Cleanup provider tách khỏi trạng thái nhận dạng. ASR có thể Completed nhưng cleanup vẫn Pending/Failed. Không ghi “đã xoá khỏi dịch vụ” khi chưa có bằng chứng; lưu trạng thái chờ provider hết retention nếu API không hỗ trợ xoá.
- Không hard-delete Recording/Meeting trước khi cleanup ngoài hoàn tất hoặc được bàn giao vào cơ chế lưu tombstone đáng tin cậy; cascade DB không xoá object trên cloud. Tích hợp guard với owner luồng xoá, không trông chờ cascade.
- Không giữ transaction DB khi gọi HTTP/cloud. Có race với dữ liệu đã gửi: nếu thu hồi commit trước khi cấp phép gửi thì không gửi; nếu đang truyền thì ngừng phần còn lại, yêu cầu cancel/cleanup và không publish kết quả. Không tuyên bố có thể thu hồi byte đã tới provider.
- Worker kiểm tra revocation trong các bước dài; service xoá phải sử dụng cùng thứ tự khóa/điểm kiểm tra với ASR. Test cả hai thứ tự race.
- Biên bản đã ban hành có chính sách riêng; không cascade sửa/xoá nội dung biên bản từ việc sửa/xoá transcript. Handoff trường hợp privacy ảnh hưởng biên bản cho BA.

## 7. Thiết kế Backend dự kiến

Giữ ba project hiện tại. Domain chứa contract/enum/DTO nghiệp vụ; Infrastructure chứa EF, service, worker, adapter; Web chứa mapping HTTP. Chưa cần thêm Application project, message broker hoặc job framework.

### 7.1 Các contract cần tạo hoặc tái sử dụng

| Contract | Trách nhiệm rõ ràng |
|---|---|
| `ITranscriptService` | StartAsync, GetAsync, UpdateSegmentAsync; quyết định nghiệp vụ, không lộ EF entity cho Web |
| `ITranscriptRepository` | Đọc DTO, transaction, khóa/CAS, claim lease, lưu segments nguyên tử; không gọi cloud |
| `IRecordingContentReader` | Mở stream theo recording đã xác thực, trả identity bất biến + metadata; không nhận URL client; owner recording cung cấp |
| `IRecordingProcessingPolicy` | Kiểm tra read/process/consent/retention/sensitivity theo D02–D05; trả reason code + consent snapshot; không mặc định Allow |
| `IAsrProvider` | SubmitAsync, GetOperationAsync, GetResultAsync, TryCancelAsync, CleanupAsync; trả outcome có kiểu, không lộ SDK exception |
| `IAsrInputPreparer` | Chuẩn bị media nếu adapter cần; bảo toàn timeline; không tự trim khoảng lặng; temp cleanup |
| `ITranscriptWorkProcessor` | Thực hiện một bước công việc từ trạng thái DB; có thể test trực tiếp, không gắn với vòng lặp vô hạn |

Provider contract tối thiểu:

- `SubmitOutcome`: Accepted(operationId), Rejected(errorCode, retryable), Unknown(correlationId). Timeout/network failure sau gửi request không được coi là Rejected chắc chắn.
- `OperationOutcome`: Queued/Running/Succeeded/Failed/Cancelled, `int? percent`, provider timestamp nếu có, error code đã lọc.
- `AsrResult`: danh sách đoạn/từ có text + offset; thống nhất canonical đơn vị millisecond; không truyền raw SDK object lên Web.
- `ProviderCapabilities`: hỗ trợ real progress, word/segment timestamps, idempotency/reconcile, cancel/delete. Adapter thật phải qua contract test.
- Metadata nội bộ `operationId`, đường dẫn cloud, SAS/signed URL, credentials không nằm trong public DTO.
- Thiếu provider/config: ASR feature Disabled, POST trả 503 sau authorization; các endpoint khác như health/auth vẫn khởi động. Fake chỉ nằm trong test DI.

### 7.2 State machine

```text
Chưa có transcript → Pending → Processing → Completed
                        │          ├──────→ Failed
                        ├──────────→ Failed
                        └──────────→ Cancelled ← Processing

Failed → Pending       chỉ explicit retry hợp lệ, attempt mới
Completed → Completed chỉ sửa Text, tăng Revision
Cancelled              không retry trong v1
```

Thêm `Cancelled` để thu hồi/xoá/hết hạn không bị trình bày như lỗi nhận dạng cho phép chạy lại. Không thêm public cancel endpoint trong story này; hook nội bộ từ recording/privacy thực hiện.

`Stage` mô tả bước: Queued, Preparing, Uploading, Submitting, Recognizing, Importing, Finished, Failed, Cancelled, SubmissionUnknown. `Status` vẫn là trạng thái nghiệp vụ chính. Chỉ cho các cặp hợp lệ, ví dụ SubmissionUnknown đi với Failed và canRetry=false.

- Pending có Stage Queued; Processing dùng Preparing…Importing; Completed dùng Finished; Failed dùng Failed/SubmissionUnknown; Cancelled dùng Cancelled.
- `ProgressPercent` là phần trăm **nhận dạng** provider cung cấp, nullable nếu không biết; không phải ETA hay phần trăm toàn pipeline.
- Pending: null; Recognizing: 0…100 nếu provider có; Importing: có thể 100 nhưng Status vẫn Processing; Completed: 100 sau commit segments; Failed/Cancelled giữ giá trị cuối (có thể null).
- Không tăng % theo timer. Trong cùng attempt không giảm % đã biết do provider trả số cũ; retry reset null. Trả ProgressUpdatedAt để FE biết số liệu được quan sát lúc nào; không cam kết provider cập nhật đều.
- Dùng UTC và TimeProvider cho code/test. Mất kết nối browser không hủy job.

### 7.3 Thay đổi dữ liệu dự kiến

Không tạo job_outbox/job_stage/pipeline_job hoặc bảng queue dùng chung. Lưu metadata công việc trên Transcript hiện có. Các field sau là thiết kế đề xuất để chốt trong ERD/diagram trước migration:

| Entity / field | Kiểu và ý nghĩa |
|---|---|
| Transcript.Revision | int, bắt đầu 1 khi tạo; optimistic concurrency cho start/retry/edit/lifecycle; mỗi mutation nghiệp vụ +1 |
| Transcript.AttemptId / AttemptNumber | Guid v7 nullable / int ≥0; null/0 chỉ cho legacy, request mới từ 1; đổi khi explicit retry; bảo vệ kết quả lần cũ |
| Transcript.RequestedById / RequestedAt | Guid? FK User Restrict / UTC? để nâng cấp dữ liệu cũ không rõ actor; bắt buộc có cho mọi request mới, actor lấy từ JWT, không lấy từ body |
| Transcript.StartedAt / FinishedAt | UTC nullable; reset khi retry; CompletedAt chỉ đặt khi Completed |
| Transcript.Stage | enum string, tối đa 32 |
| Transcript.ProgressPercent / ProgressUpdatedAt | int? có constraint 0…100 / UTC nullable |
| Transcript.ErrorCode | string max 64; ErrorMessage hiện có giữ max 2000, nội dung đã lọc |
| Transcript.ResultNoticeCode | string? max 64; ASR_NO_SPEECH cho kết quả im lặng hợp lệ, khác ErrorCode |
| Transcript.NextPollAt / PollFailureCount | UTC nullable / int ≥0; điều phối polling/backoff bền vững |
| Transcript.ProcessingDeadlineAt | UTC nullable; đặt khi bắt đầu xử lý từ timeout snapshot của attempt, không gia hạn vô hạn mỗi restart |
| Transcript.LeaseToken / LeaseUntil | Guid? / UTC?; claim worker, hai field cùng null hoặc cùng có giá trị |
| Transcript.ProviderOperationId | string nullable max 1024; lưu ngay sau Accepted, dùng phục hồi |
| Transcript.ProviderProfileSnapshot | jsonb có schema version; tên vendor, API/model/locale, endpoint xử lý, phiên bản chính sách, media limits, MaxQueueWait và MaxProcessingDuration; tuyệt đối không credential |
| Transcript.SourceIdentity | string max 256; immutable version/checksum từ recording service, không dựa mỗi filename |
| Transcript.ConsentSnapshot | jsonb: evidence references, consentVersion, noticeVersion, providerProfileVersion, verifiedAt; chưa có bằng chứng thì không tạo snapshot Allowed |
| Transcript.ExternalArtifacts | jsonb array nội bộ: attemptId, loại input/output/operation, opaque key, cleanup status, lần thử, nextCleanupAt, lastErrorCode; không URL chứa token |
| Transcript.DeletedAt | UTC nullable; tombstone của transcript sau khi content bị xoá |
| TranscriptSegment | Giữ Text/Position/StartMs/EndMs/EditedById/EditedAt; không cần bảng version text trong v1 |

`Revision` dùng EF concurrency token hoặc điều kiện UPDATE tương đương. Poll progress/renew lease không tăng Revision; dùng điều kiện AttemptId + LeaseToken cho chúng, tránh làm yêu cầu chỉnh sửa bị stale vì heartbeat. Thay đổi Status/retry/edit/deletion phải tăng Revision.

JSON metadata phải có record/serializer và schema version cụ thể trong Infrastructure, không cập nhật tùy ý bằng dictionary không kiểm soát. Không thay profile của attempt đang chạy khi cấu hình deployment đổi.

Quy tắc nâng cấp dữ liệu cũ: giữ text, edited metadata, provider/language và timestamps đã biết; backfill Revision=1. Nếu chưa có attempt thì dùng AttemptNumber=0 và AttemptId=null để biểu thị legacy, không bịa người yêu cầu, consent, operation hay lần chạy mới. Sửa constraint tương ứng: attempt 0 chỉ với legacy không được worker nhận; attempt ≥1 phải có AttemptId/RequestedById/RequestedAt/profile. Pending/Processing legacy chuyển Failed với ASR_LEGACY_REVIEW_REQUIRED, canRetry=false cho tới khi được rà soát; Completed legacy vẫn đọc/sửa theo policy. Mọi yêu cầu mới tạo attempt từ 1 và có metadata đầy đủ. Không tự nhận backfill schema là bằng chứng việc xử lý ngoài từng được đồng ý.

Ràng buộc DB: unique RecordingId giữ nguyên; unique segment position giữ nguyên; Position >0; StartMs ≥0; EndMs > StartMs; ProgressPercent null hoặc [0,100]; Revision ≥1; AttemptNumber theo quy tắc legacy ở trên. EndMs ≤ Recording.DurationMs là cross-table invariant kiểm tra ở service/parser, không viết CHECK tham chiếu bảng khác. FK actor Restrict. Enum string.

Index worker: `(Status, NextPollAt)` và LeaseUntil theo truy vấn thực tế; không index text transcript. Không làm migration thay seed capability. Model test đang pin danh sách 26 bảng; phương án này không thêm bảng, không xóa assertion để “cho xanh”. Consent entity nếu cần mới thuộc thiết kế riêng đã duyệt, cập nhật test với lý do cụ thể.

## 8. Hợp đồng API dự kiến

Các code `ASR_*` dưới đây là tên đề xuất có namespace riêng, **không giả là mã MSG của BA đã cấp**. Chốt message catalog trước triển khai. Error payload `{ code, message, traceId }`, có `currentRevision` khi xung đột. Message tiếng Việt, không chứa raw exception/URL/token/text audio.

### 8.1 POST /api/recordings/{id}/transcribe

```json
{ "expectedRevision": 0 }
```

- Chưa có transcript: expectedRevision phải 0; kiểm tra đủ điều kiện, tạo Pending/attempt 1/revision 1 trong transaction; trả **202** và Location trỏ GET transcript.
- Pending/Processing: trả **202** trạng thái hiện tại, không tạo attempt khác. Coalesce request trùng sau khi kiểm tra quyền; không reset dữ liệu.
- Completed: trả **200** bản hiện tại, không gọi provider, không thay text sửa tay.
- Failed: expectedRevision phải trùng Revision hiện tại; cleanup/reconcile phải cho phép; explicit retry tăng attempt/revision, reset progress/error/time/operation metadata sau khi đã bảo toàn audit và cleanup lần cũ; trả **202**.
- Cancelled/đã xoá: không chạy lại. Lỗi theo policy 404 hoặc 409 đối với tài nguyên vẫn được đọc.
- Không nhận file/audio URL, language tùy ý, provider, userId hoặc raw consent ở endpoint này.
- Response là envelope như GET nhưng `segments: null`; client GET khi cần text.
- POST chỉ lưu yêu cầu bền vững; không upload/convert/submit ASR trong request. Mục tiêu kiểm thử: response không đợi provider dù fake provider đang bị chặn.

### 8.2 GET /api/recordings/{id}/transcript?includeSegments=true

Mặc định includeSegments=true. Khi polling, FE gửi false để không tải lại toàn bộ text. Đọc summary và segments bằng snapshot nhất quán, ví dụ transaction RepeatableRead; tránh lấy Revision mới kèm segments cũ hoặc ngược lại.

```json
{
  "recordingId": "00000000-0000-0000-0000-000000000001",
  "permissions": { "canStart": false, "canRetry": false, "canEdit": false },
  "blockingReasonCode": null,
  "transcript": {
    "id": "00000000-0000-0000-0000-000000000002",
    "status": "Processing",
    "stage": "Recognizing",
    "revision": 1,
    "attemptNumber": 1,
    "language": "vi-VN",
    "provider": "configured-provider",
    "isMachineGenerated": true,
    "progress": { "percent": 42, "updatedAt": "2026-09-23T03:00:00Z" },
    "error": null,
    "resultNoticeCode": null,
    "completedAt": null,
    "segments": null
  }
}
```

- Ví dụ minh hoạ cấu trúc; provider chưa chốt, không seed giá trị placeholder vào DB thật.
- Chưa tạo transcript: **200**, transcript=null, permissions/canStart theo tình trạng hiện tại. Không nhầm “chưa chạy ASR” với “không được thấy recording”.
- Completed + includeSegments=true: segments là array có `id, position, startMs, endMs, text, editedById, editedAt`; im lặng hợp lệ trả array rỗng kèm result notice `ASR_NO_SPEECH`.
- Chưa Completed hoặc includeSegments=false: segments=null; không expose kết quả nhập dở. Chốt `resultNoticeCode: string?` vào response/result metadata để phân biệt im lặng và lỗi.
- Failed: error gồm code/message/canRetry; message hướng dẫn xử lý nguyên nhân. GET vẫn 200 vì đọc trạng thái thành công.
- DTO ghi rõ machine-generated cả sau chỉnh sửa; EditedAt/EditedById nhận diện đoạn người đã sửa. Không hứa bản transcript đã được xác nhận đúng toàn bộ.
- `Cache-Control: no-store`; permissions chỉ phục vụ UI, mỗi mutation kiểm tra lại server.
- Nếu record hoặc transcript bị thu hồi quyền/xoá/hết hạn và policy không cho đọc, trả 404; không trả nội dung cũ từ cache.

### 8.3 PUT /api/recordings/{id}/transcript/segments/{segmentId}

Endpoint bổ sung để thực hiện AC sửa tay, cần ghi chính thức vào UC/API contract.

```json
{ "expectedRevision": 7, "text": "Nội dung thư ký đã sửa." }
```

- Text bắt buộc, trim hai đầu, không rỗng, tối đa 8000 ký tự theo quy tắc độ dài .NET đã ghi trong contract; không xử lý như HTML.
- Chỉ Completed + meeting state/policy cho ghi; kiểm tra segment thuộc đúng transcript/recording.
- Khóa rồi so revision. Thành công update Text, EditedById=current user, EditedAt=UTC, UpdatedAt, Transcript.Revision+1 và audit trong cùng transaction.
- Sửa cùng text chuẩn hoá hiện tại là no-op: trả 200 revision hiện tại, không thay EditedAt/audit; vẫn phải kiểm tra expectedRevision trước.
- Trả 200 `{ transcriptId, revision, segment: {...} }`.
- Hai request sửa cùng revision: một thắng, một 409 `ASR_REVISION_CONFLICT`; không last-write-wins và không tự merge text.
- Không cho split/merge/delete segment hay sửa timestamp trong v1. Không ghi đè `CompletedAt` khi sửa text.

### 8.4 Các lỗi phải phân biệt

| HTTP | Code đề xuất / tình huống |
|---|---|
| 400 | ASR_INVALID_REQUEST: expectedRevision thiếu/âm; text thiếu/rỗng/quá dài |
| 401 / 403 / 404 | Theo mục 4; không tiết lộ ID mà caller không có quyền đọc |
| 409 | ASR_REVISION_CONFLICT, ASR_MEETING_STATE_INVALID, ASR_RECORDING_NOT_READY, ASR_CONSENT_REQUIRED, ASR_RETENTION_UNDEFINED, ASR_RETRY_NOT_READY |
| 422 | ASR_UNSUPPORTED_MEDIA, ASR_MEDIA_LIMIT_EXCEEDED, ASR_INVALID_TIMESTAMPS nếu phát hiện trước enqueue; lỗi phát hiện trong worker lưu vào Failed thay vì trả ngược POST đã 202 |
| 429 | ASR_CAPACITY_EXCEEDED khi hàng chờ ứng dụng chạm limit đã cấu hình; không nhầm với provider 429 xử lý nền |
| 503 | ASR_NOT_CONFIGURED hoặc không thể lưu queue DB; không nhận 202 khi chưa commit |

Runtime error code riêng: ASR_PROVIDER_AUTH_FAILED, ASR_PROVIDER_QUOTA, ASR_PROVIDER_UNAVAILABLE, ASR_SOURCE_MISSING, ASR_SOURCE_CHANGED, ASR_SUBMISSION_UNKNOWN, ASR_PROCESSING_TIMEOUT, ASR_INVALID_RESULT. canRetry là phép tính từ error + policy + cleanup, không phải mọi Failed đều lập tức retry được.

## 9. Worker, transaction và phục hồi

### 9.1 Vòng xử lý

1. BackgroundService tick mỗi 2 giây theo cấu hình; tạo DI scope mới, không giữ DbContext scoped trong singleton.
2. Claim một transcript đến hạn bằng atomic SQL update/CAS khi lease trống/hết hạn; mặc định capstone xử lý tối đa một công việc ASR một lúc. Mỗi tick chỉ một bước hữu hạn; không để một provider poll treo chặn mọi cleanup.
3. Gán LeaseToken mới, LeaseUntil dự kiến 90 giây; renew mỗi 20 giây khi upload/preparation kéo dài. Các con số là default kỹ thuật để thử, timeout thực tế chốt trong vendor profile. Clock lease lấy từ DB hoặc một cơ chế nhất quán được test, không dựa giờ lệch của hai worker.
4. Re-read policy, state, source identity; lấy grant/snapshot cho bước gửi. Kiểm tra người yêu cầu còn active/quyền xử lý trước upload/submit; đã mất quyền thì dừng theo policy, không mượn quyền một admin hệ thống.
5. Preparing/Uploading: stream file, giới hạn size/time; object key xác định theo recordingId/attemptId. Đăng ký artifact key **trước** tạo object để crash vẫn dọn được; ghi đè có điều kiện/checksum, không tạo object mới mỗi retry.
6. Commit Stage=Submitting và metadata attempt trước gọi submit. Sau Accepted, lưu operationId ngay bằng CAS đúng attempt/lease. Không chờ ASR xong mới lưu ID.
7. Có operationId thì poll operation cũ; không submit lần hai. NextPollAt mặc định 5 giây, điều chỉnh tôn trọng Retry-After/quota vendor. Client polling độc lập với worker polling.
8. Succeeded: lấy result, parse/validate ngoài transaction, vào Importing. Ghi toàn bộ segments + Completed + completedAt/progress/revision/audit trong **một transaction** sau khi revalidate source/consent/state/attempt/lease.
9. Failed từ provider: lưu mã lỗi tiếng Việt đã chuẩn hoá + FinishedAt, giữ operation/artifacts cho cleanup; không tự clear metadata.
10. Release lease có điều kiện token còn thuộc worker; đặt lịch cleanup. Restart đọc DB tiếp tục từ stage/operationId.

Không dùng Task.Run fire-and-forget từ endpoint; không lấy CancellationToken của request làm lifetime của job; không giữ transaction/row lock trong lúc chờ network/provider/media conversion.

### 9.2 Các bất biến đồng thời

- Giao dịch business dùng thứ tự khóa **Meeting → Recording → Transcript**, ReadCommitted, đọc lại điều kiện sau lấy khóa; khi chưa có Transcript, khóa Recording để serialize tạo lần đầu. Unique RecordingId là lớp bảo vệ cuối.
- Claim lease là transaction/statement riêng chỉ đụng Transcript; kết thúc trước transaction nhiều bảng, tránh khóa ngược thứ tự.
- Mọi kết quả worker ghi với điều kiện AttemptId + LeaseToken còn đúng và chưa Cancelled/Deleted. Lease cũ không được overwrite sau retry/revocation.
- Lease ngăn hai worker xử lý DB đồng thời nhưng **không tự đảm bảo external submit exactly-once**. Không retry submit tự động chỉ vì mất lease.
- Stage Submitting mà chưa có operationId khi restart/timeout: đánh dấu SubmissionUnknown; nếu provider có idempotency/reconcile đã chứng minh thì nối lại, nếu không thì canRetry=false và chuyển quy trình vận hành D08. Không tự gửi lần nữa để “chạy cho xong”.
- Poll GET/network transient có thể backoff 5/10/20/40/60 giây, có jitter và Retry-After; counter/deadline tồn tại trong DB. Không kết luận provider job Failed chỉ vì một lần poll lỗi.
- Pending quá RequestedAt + MaxQueueWait snapshot phải nhận ASR_QUEUE_TIMEOUT, không nằm im vô hạn; chưa gửi provider thì retry không cần external reconcile. Deadline xử lý dùng ProcessingDeadlineAt đã lưu, không tính lại từ lúc worker vừa khởi động.
- Sau timeout tổng thể đã cấu hình, UI nhận lỗi rõ, nhưng cleanup/reconcile vẫn theo dõi operation còn có thể chạy; không cho retry tạo job mới trước khi xử lý sự không chắc chắn.
- Kết quả cũ về sau retry: bỏ theo AttemptId; cleanup artifact đúng attempt, không xoá artifact của attempt hiện tại.
- Không reset ExternalArtifacts chưa dọn khi retry. Phương án v1: chỉ mở retry sau khi lần cũ đã được giải quyết và có trạng thái cleanup cho phép; nếu vendor retention không cho dọn sớm phải chốt rõ D08/D09 trước khi dùng vendor đó.
- Bộ chọn công việc phải bao gồm cleanup còn đến hạn của cả Completed/Failed/Cancelled/Deleted transcript, không chỉ Pending/Processing. Xử lý một bước nhận dạng rồi một bước cleanup đến hạn để tránh cleanup bị bỏ đói; dùng cùng cơ chế lease/CAS. JSON artifact không chứa secret: signed URL được tạo ngắn hạn tại lúc gọi từ opaque object key.
- Transition sang MinutesIssued phải phối hợp kiểm tra ASR đang chạy theo D10; nếu chưa có phối hợp, worker phải chặn publish kết quả ở state không cho ghi và báo nguyên nhân, không sửa hồ sơ đã khóa.

## 10. Chuẩn hoá timestamp và sửa tay

1. Provider phải trả word hoặc segment offsets có nguồn gốc từ audio. Không suy ra mốc bằng độ dài text, tốc độ đọc hoặc chia duration đều.
2. Timeline 0 ms là đầu recording gốc được phát lại. Không trim khoảng lặng đầu file; conversion resample/mono phải giữ timeline. Nếu vendor bắt buộc chunk, cần thiết kế offsets riêng và test ranh giới; v1 không tự thêm chunking khi vượt limit, báo limit đã chốt.
3. Chuyển đơn vị bằng số nguyên/decimal, tránh float gây drift: start floor tới ms, end ceil tới ms. Chỉ clamp chênh lệch làm tròn ≤1 ms ở cuối file; offset âm, đảo hoặc vượt nhiều phải báo invalid result.
4. Nếu provider đã trả segment có timestamp: giữ text/đoạn hợp lệ, sort theo StartMs và thứ tự nguồn để ổn định; không phân đoạn lại ngẫu nhiên.
5. Nếu chỉ có word timestamps: gom lần lượt; đóng đoạn trước word tiếp theo khi khoảng nghỉ ≥1500 ms, thêm word vượt 30 giây/500 ký tự, hoặc đoạn hiện tại kết thúc bằng `.?!` (có thể kèm dấu ngoặc/ngoặc kép). Thuật toán này là quy tắc kỹ thuật dự kiến, phải pin bằng fixture. Một word đơn lẻ vượt soft limit vẫn là một đoạn; >8000 ký tự hoặc time không hợp lệ thì fail.
6. Text lấy từ chuỗi từ/display text provider có mapping timestamp; dùng một khoảng trắng khi cần nối token, chuẩn NFC, không dùng LLM viết lại nội dung hay tự thêm từ nghe thiếu.
7. StartMs là word đầu, EndMs là word cuối; Position từ 1, liên tục. Không áp constraint không-overlap cho mọi provider; overlap có thể hợp lệ, thứ tự StartMs vẫn phải rõ. Không tự bật multichannel/diarization.
8. Kết quả im lặng hợp lệ: Completed với segments=[] và ASR_NO_SPEECH. Có text mà không có timestamp tương ứng: Failed ASR_INVALID_RESULT; không trả timestamp giả.
9. Sửa tay giữ timestamp gốc và identity segment; đánh dấu edited. Không giữ bản text cũ trong audit để rồi nghĩa vụ xoá trở nên bất khả thi; nếu BA cần full revision history phải thiết kế retention riêng trước.
10. Click-to-seek thuộc FE: seekSeconds = startMs / 1000. Backend phải bàn giao file/stream chính xác cùng timeline và quyền phát lại; chưa có playback tích hợp thì chưa nghiệm thu AC này.

## 11. Bản đồ file dự kiến

Tên có thể đổi để khớp implementation của dependency đã merge; không tạo interface trùng chỉ vì tên trong plan khác.

| Vị trí | Nội dung |
|---|---|
| `backend/TrustCabinet.Domain/Transcription/` | Contracts, result/outcome, request nội bộ, canonical provider result, policy result |
| `backend/TrustCabinet.Domain/Entities/Transcript.cs` | Metadata job/concurrency/consent/provider/tombstone đã chốt |
| `backend/TrustCabinet.Domain/Enums/RecordingEnums.cs` | Cancelled và enum stage/cleanup nếu phù hợp; enum lưu string |
| `backend/TrustCabinet.Infrastructure/Transcription/TranscriptService.cs` | Business logic start/read/edit |
| `.../TranscriptRepository.cs` | EF/SQL transaction, lease/CAS, snapshot read |
| `.../TranscriptWorker.cs`, `TranscriptWorkProcessor.cs` | Hosted loop và từng bước xử lý có thể test |
| `.../TranscriptResultNormalizer.cs` | Offset/segment normalizer thuần, deterministic |
| `.../Providers/<SelectedProvider>AsrProvider.cs` | Chỉ tạo sau D01; SDK nằm trong Infrastructure |
| `.../TranscriptionOptions.cs`, `TranscriptionServiceCollectionExtensions.cs` | Feature flag, limits, validate config, DI |
| `backend/TrustCabinet.Infrastructure/Configurations/TranscriptConfiguration.cs` | Constraints/indexes/JSON/concurrency/FK |
| `.../TranscriptSegmentConfiguration.cs`, `Migrations/` | Timestamp constraints, migration mới + snapshot |
| `backend/TrustCabinet.Web/Endpoints/TranscriptEndpoints.cs` | HTTP mapping, DTO và auth, không đặt nghiệp vụ trong handler |
| `backend/TrustCabinet.Web/Program.cs` | AddTranscription + MapTranscriptEndpoints; không bật CORS |
| `tests/TrustCabinet.Web.Tests/Transcription/` | Service/endpoint/provider contract/normalizer/worker/concurrency/privacy tests |
| `frontend/src/types/transcript.ts`, `frontend/src/api/transcripts.ts` | Backend handoff bắt buộc, thông qua api client |
| `docs/03-api/NN-asr.md`, `NN-asr-note-cho-fe.md` | Chọn số còn trống khi thực hiện, cập nhật docs index |
| `docs/02-design/02-erd.md`, BA diagrams, ADR mới | Đồng bộ thiết kế đã được duyệt; không sửa ADR đã chấp nhận |

## 12. Các bước triển khai nhỏ, đầu ra và cách kiểm tra

Không coi thứ tự dưới đây là lệnh triển khai ngay. Mỗi bước chỉ làm khi đầu vào được ghi đủ. Sau một bước, cập nhật checklist và bằng chứng kiểm tra; nếu dependency đổi thì sửa plan trước bước phụ thuộc.

### P00 — Chụp baseline

- Đầu vào: code nhánh sẽ triển khai và docs mới nhất.
- Làm: đọc AGENTS/workflow; git status; kiểm tra lại ASR/recording code đã có; không ghi đè phần đồng đội vừa merge.
- Đầu ra: bảng dependency thực tế, baseline build/test, danh sách file sẽ dùng lại.
- Kiểm tra: mọi class/service trong plan được đánh dấu “đã có” hoặc “sẽ thêm”; không có nhận định auth vẫn chưa tồn tại.

### P01 — Chốt BA contract

- Đầu vào: Jira/UC spec/sequence/class diagram cho SCRUM-31 và bản ghi.
- Làm: giải quyết D02–D07, D10; bổ sung PUT sửa tay; phân biệt recording consent và external-ASR consent.
- Đầu ra: bảng quyền theo actor/state/sensitivity, error catalog, trường hợp xóa/retention, spec và diagram thống nhất.
- Kiểm tra: tester suy ra được allowed/denied cho từng actor; không còn chỗ developer phải tự đoán điều kiện nghiệp vụ. Chưa đạt thì không triển khai policy production.

### P02 — Chốt REST và DTO

- Đầu vào: P01.
- Làm: viết API docs và JSON ví dụ đủ null/empty/completed/failed; PascalCase enums; giá trị ms; revision semantics; chuẩn hoá lỗi.
- Đầu ra: contract của ba endpoint và note FE, chưa cần vendor.
- Kiểm tra: FE biết cách phân biệt chưa chạy, đang chạy, im lặng, lỗi, lỗi quyền, conflict; không DTO nào chứa storage path/token/operationId.

### P03 — Chốt contract bản ghi/consent/playback

- Đầu vào: owner recording/privacy/meetings.
- Làm: ký hợp đồng IRecordingContentReader/IRecordingProcessingPolicy, identity file bất biến, evidence references, hook revoke/delete, playback có seek và thời điểm khóa hồ sơ.
- Đầu ra: method/input/output cụ thể và fixture đã ẩn dữ liệu cá nhân; dependency IDs.
- Kiểm tra: chứng minh không có đường production Allow mặc định; upload ngoài/khách có bằng chứng tương ứng; kiểm tra cùng một audio identity khi phát và ASR. Thiếu implementation thật thì giữ gate tích hợp.

### P04 — Tạo domain contracts và state machine

- Đầu vào: P02/P03.
- Làm: tạo DTO/outcome/interface/enum; policy interface; TimeProvider sử dụng thống nhất.
- Đầu ra: Domain compile không reference EF/cloud SDK; fake provider chỉ trong test.
- Kiểm tra: unit test bảng transition hợp lệ/bị cấm, no Completed→Pending, retry tăng attempt, revoke→Cancelled.

### P05 — Model và configuration

- Đầu vào: P04, ERD thay đổi đã review.
- Làm: thêm field ở mục 7.3; khai báo FK/length/index/check/concurrency; chốt typed JSON schema.
- Đầu ra: model/config có constraint và metadata đầy đủ để restart.
- Kiểm tra: DatabaseModelTests kiểm tra unique/FK/enum/no voiceprint/no org unit; không thay capability seed; fixture serialize/deserialize snapshot khớp.

### P06 — Migration

- Đầu vào: P05.
- Làm: sinh migration mới (ví dụ AddTranscriptProcessingState) + snapshot; xử lý dữ liệu cũ rõ ràng.
- Đầu ra: migration áp được từ latest main và từ DB trống. Không đoán consent/provider cho dòng cũ; transcript Pending/Processing cũ thiếu operation metadata phải đánh dấu cần phục hồi, không tự submit.
- Kiểm tra: PostgreSQL thật migrate cả hai đường; constraint reject dữ liệu sai; dữ liệu Completed/edited cũ không mất; migration không drop/reset bảng quyền hoặc tạo job spine. Preflight tìm segment cũ sai timestamp trước áp constraint; nếu có thì dừng để xử lý dữ liệu có review, không xóa hoặc tự sửa timestamp để migration pass.

### P07 — Repository đọc và mutation transaction

- Đầu vào: P06.
- Làm: query theo recording + policy; transaction lock order; tạo lần đầu, retry, sửa text, import; snapshot GET.
- Đầu ra: repository dùng PostgreSQL provider thực, atomic audit khi mutation thành công.
- Kiểm tra: rollback giữa chừng không để segments dở/Completed thiếu text; hai start cùng recording chỉ một row; GET không trộn revision và segment từ hai transaction.

### P08 — Authorization/policy production

- Đầu vào: P01/P03 và implementation dependency.
- Làm: đọc user active, MeetingRole, capability DB, state, sensitivity, consent, retention; quyền read trước write.
- Đầu ra: policy và auth filter/service reuse hợp lý; không copy RequireAdminUsers cho ASR.
- Kiểm tra: JWT cũ sau vô hiệu hóa/gỡ capability bị chặn; người khác đoán ID nhận 404; role hệ thống cùng tên nhưng khác meeting không được quyền.

### P09 — Service start/retry

- Đầu vào: P07/P08.
- Làm: triển khai POST semantics bằng service, chỉ enqueue DB; audit actor/sub, recordingId, attemptId, provider profile reference, reason code.
- Đầu ra: start nhanh và retry có expectedRevision; không cloud call trong request.
- Kiểm tra: provider fake giữ vô hạn nhưng POST vẫn trả sau commit; DB failure không trả 202; click đúp không tạo attempt thứ hai; request cũ sau Failed nhận 409.

### P10 — Service GET

- Đầu vào: P07/P08.
- Làm: envelope, null/empty semantics, no-store, permissions và blockingReason, includeSegments.
- Đầu ra: API đọc tiến độ đúng DB và text theo quyền.
- Kiểm tra: chưa có transcript 200/null; pending không lộ segments; failed 200/error; record inaccessible 404; includeSegments=false không query/tải text thừa.

### P11 — Service sửa tay

- Đầu vào: P07/P08.
- Làm: validate text, association và revision; update text/edited metadata/audit cùng transaction.
- Đầu ra: PUT segment không làm lệch timestamp, không tự đổi biên bản.
- Kiểm tra: hai người cùng revision chỉ một thắng; segment thuộc recording khác 404; text rỗng/quá dài 400; timestamp trước/sau giống nhau; no-op không thêm audit.

### P12 — Endpoint mapping và đăng ký DI

- Đầu vào: P09–P11.
- Làm: TranscriptEndpoints, DI và Program; map error; không bắt mọi exception thành 200/Failed giả; worker disabled khi chưa cấu hình.
- Đầu ra: ba endpoint trả đúng status/body/header, auth vẫn hoạt động.
- Kiểm tra: WebApplicationFactory + JWT test; health boot với ASR disabled; route/API unknown không bị nhầm thành JSON thành công; cấu hình thiếu không gọi provider.

### P13 — Normalizer kết quả

- Đầu vào: canonical result contract P04.
- Làm: thuật toán mục 10 và fixture word/segment/empty/malformed.
- Đầu ra: hàm thuần trả segments hợp lệ, không phụ thuộc network/DB.
- Kiểm tra: ms conversion, floor/ceil, UTF-8/NFC, pause/sentence/length boundary, khoảng lặng đầu file, cuối file ±1 ms, overlap hợp lệ, offset invalid, im lặng, text không timestamp.

### P14 — Claim lease và vòng worker

- Đầu vào: P07/P13.
- Làm: BackgroundService gọi processor từng bước, atomic claim/lease renew/NextPollAt; fake controlled provider.
- Đầu ra: job Pending sống qua restart và tiến độ không phụ thuộc browser.
- Kiểm tra: hai worker không cùng commit một attempt; process dừng ở Preparing khởi động lại được; lease cũ không ghi sau takeover; không reuse DbContext singleton; cancellation host không biến mọi job thành lỗi vĩnh viễn.

### P15 — Submit/poll/recovery logic

- Đầu vào: P14, typed provider outcomes.
- Làm: checkpoint trước submit, lưu operationId, poll và bounded backoff; SubmissionUnknown không tự resubmit.
- Đầu ra: processor có trace theo attempt, không phát sinh call lặp vô ý.
- Kiểm tra: fake Accepted → process chết → restart poll cùng ID; Unknown → không submit lần hai; transient polling → chưa Failed sớm; max deadline → lỗi rõ + reconcile/cleanup tiếp tục.

### P16 — Import kết quả atomic

- Đầu vào: P13/P15.
- Làm: parse rồi transaction revalidate + insert segments + Completed + audit; return stale result thành ignored.
- Đầu ra: chỉ trạng thái Completed mới có tập segments hoàn chỉnh.
- Kiểm tra: lỗi giữa insert rollback hết; callback/poll cũ sau retry/revoke không ghi; process chết sau commit trước cleanup không gọi ASR lại.

### P17 — Cleanup và privacy races

- Đầu vào: P03/P15/P16.
- Làm: cleanup artifacts đăng ký trước upload; hook revoke/expire/delete; chặn cascade làm mất cleanup tombstone; retry cleanup có lịch riêng.
- Đầu ra: nội dung bị chặn/xoá đúng chính sách; metadata cho biết external cleanup đã xong hay còn chờ.
- Kiểm tra: revoke trước upload → zero byte gửi; revoke trong request → ngừng/cleanup và không publish; completed transcript xoá text; provider delete fail vẫn có retry record sau restart; audit không chứa text/credentials.

### P18 — Chọn vendor và spike, chờ người dùng tính sau

- Đầu vào: tài khoản thử, ngân sách, yêu cầu xử lý dữ liệu được nhóm xác nhận.
- Làm: checklist mục 5; test audio có quyền sử dụng; giải quyết D01/D08/D09; đo chất lượng/timestamp/latency và chi phí trên sample đại diện.
- Đầu ra: ADR mới trạng thái đúng, provider profile, bằng chứng spike, giới hạn và retention đã chốt.
- Kiểm tra: không tuyên bố provider “tốt nhất” nếu chưa đo; không gửi audio thật chưa consent; vendor không cung cấp timestamp hoặc recovery phù hợp thì chưa được nhận làm adapter production.

### P19 — Adapter thật và media preparation

- Đầu vào: P18 + P15.
- Làm: chỉ adapter vendor đã chọn; pin SDK/API/model, credentials qua secret/env, upload riêng tư, polling và error mapping; nếu cần conversion thì sandbox process với timeout/size limits, không trim timeline.
- Đầu ra: adapter qua chung provider contract tests; hướng dẫn cấu hình không có secret.
- Kiểm tra: fixture response → cùng canonical result; HTTP429/5xx/auth/malformed/unknown submit; không SSRF URL do user chọn; không path traversal; không log raw audio/transcript. Integration thật opt-in riêng CI.

### P20 — Kết nối đầy đủ recording/provider

- Đầu vào: P17/P19 + implementation recording/privacy/playback thật.
- Làm: smoke từ audio được phép → POST202 → progress → GETCompleted; lặp kịch bản lỗi và retry.
- Đầu ra: bằng chứng luồng hoàn chỉnh Backend, cleanup thật, tài liệu chạy local.
- Kiểm tra: stream phát đúng identity, duration/timestamp khớp; file bị thay thế sau enqueue bị từ chối; provider unavailable không giữ HTTP request; restart giữa job không mất operation ID.

### P21 — TS contract và bàn giao FE

- Đầu vào: P02/P12/P20.
- Làm: types/transcript.ts, api/transcripts.ts, note auth/error/progress/seek/edit conflict; phối hợp sửa api client để giữ error body theo convention chung nếu chưa có.
- Đầu ra: wrapper get/start/updateSegment dùng relative `/api`, nhận RequestInit/token qua cơ chế auth chung; không component fetch trực tiếp.
- Kiểm tra: TypeScript compile, lint; FE phân biệt 401/403/404/409, percent null và segments null/[]; không đưa provider URL/credential vào client.

### P22 — Kiểm thử tích hợp và concurrency

- Đầu vào: P08–P21.
- Làm: suite PostgreSQL thật cho locking/unique/checks/rollback/lease; test doubles cho cloud; time điều khiển được thay vì sleep dài.
- Đầu ra: ma trận test mục 13 tự động hoặc có bước manual/owner cụ thể.
- Kiểm tra: lỗi cố ý ở claim/revision/attempt guard khiến test liên quan fail; không dùng EF InMemory để chứng minh transaction hoặc row lock đúng.

### P23 — Đồng bộ docs và bàn giao nghiệm thu

- Đầu vào: mọi gate nghiệp vụ/vendor đã chốt và test pass.
- Làm: cập nhật ERD, UC/diagrams, API docs/index, ADR mới, setup/privacy/retention runbook và README/scope nếu cần; không sửa accepted ADR nội dung.
- Đầu ra: BE handoff có endpoints, limits, sample request, errors, flags, kiểm tra provider, test report, mục FE/Tester còn phải làm.
- Kiểm tra: chạy bốn CI checks ở mục 14; FE demo seek/edit/retry; Tester xác nhận AC. Chỉ Backend xong không đánh dấu cả UC Done.

## 13. Ma trận kiểm tra bắt buộc

| Nhóm | Tình huống / kết quả mong đợi |
|---|---|
| AC chạy nền | POST không đợi ASR; đóng tab/restart server vẫn phục hồi; progress từ DB; unknown % hiển thị stage |
| AC timestamp | File có khoảng lặng đầu; đoạn đầu/giữa/cuối; click seek đúng bản ghi; không sai đơn vị ms/giây; text sửa không đổi time |
| AC sửa tay | Completed sửa được; pending/failed bị chặn; editor quy đúng user; stale revision 409; không sửa biên bản đã phát hành |
| AC thất bại/retry | Lỗi provider/network/media rõ ràng; canRetry phản ánh nguyên nhân; retry mới attempt; kết quả cũ không overwrite; Completed không bị rerun |
| Quyền | Anonymous401; inactive403; resource invisible404; người đọc thiếu write403; current DB capability thay đổi có tác dụng; không ngoại lệ admin ngầm |
| Consent | Chưa consent, consent capture-only, sai provider/notice/source, revoked, hết retention, khách chưa có evidence đều không gửi cloud |
| Dữ liệu | Một transcript/recording; unique position; segment time hợp lệ; Completed chỉ sau toàn bộ insert; im lặng không bị coi là exception |
| Concurrency | 2 start, 2 retry, 2 editor, 2 worker, lease hết giữa upload, stale result, revoke trong import, meeting khóa cùng lúc |
| Recovery | Crash trước submit; sau external Accepted trước DB operationId; sau lưu ID; trong import; sau Completed trước cleanup; đều có nhánh rõ |
| Cleanup | Temp/cloud input/output/operation tracking; API cancel unsupported; delete error; retry cleanup qua restart; hard-delete guard |
| Bảo mật output | Không StoragePath, signed URL, token, raw stack trace; error/log/audit không lưu transcript text; no-store |
| Tích hợp media | MP3/file upload và định dạng browser thực tế theo D05; codec hỗ trợ/không hỗ trợ; vượt limits; duration/file identity đúng |
| Chất lượng ASR | Audio tiếng Việt đại diện giọng/vùng/độ nhiễu/tên riêng; báo tỷ lệ sai và điểm cần sửa, không đặt ngưỡng nghiệm thu chất lượng chưa có BA |

Fixture dùng giọng có quyền sử dụng hoặc dữ liệu tổng hợp hợp lệ, không commit bản ghi họp riêng tư. Test cloud mặc định tắt; tests thường phải pass không có tài khoản vendor.

## 14. Lệnh kiểm tra khi triển khai và điều kiện bàn giao

Đứng tại repo root:

```powershell
dotnet build TrustCabinet.sln -warnaserror
dotnet test TrustCabinet.sln
```

Đứng tại `frontend`:

```powershell
npm run lint
npm run build
```

Khi tạo migration sau khi model đã được duyệt:

```powershell
dotnet dotnet-ef migrations add AddTranscriptProcessingState --project backend/TrustCabinet.Infrastructure --startup-project backend/TrustCabinet.Web --output-dir Migrations
```

Chạy test DB trên database/container test riêng theo pattern repo; không trỏ vào DB người dùng. Migration/snapshot cùng thay đổi model. Không chạy cloud smoke mặc định từ `dotnet test`; ghi rõ cách bật bằng môi trường riêng và audio có consent.

BE được bàn giao khi: ba endpoint đúng contract; worker restart được; policy thật đã tích hợp; vendor thật vượt spike; timestamp hợp lệ; sửa text có concurrency; lỗi/retry/cleanup có bằng chứng; docs/test đồng bộ. UC Done còn cần FE/Tester/review/Jira theo workflow.

### Checklist trước khi giao một model khác triển khai

- [ ] Đọc lại AGENTS/workflow và tài liệu mới từ thành viên khác.
- [ ] D02–D07/D10 được chốt; không dùng phương án tham khảo như yêu cầu chính thức.
- [ ] Cập nhật file/path theo code vừa merge, tránh tạo module trùng.
- [ ] Chỉ làm các bước đủ đầu vào; không mở tính năng production với fake provider/policy.
- [ ] Nhà cung cấp, ngân sách, D08/D09 vẫn để sau cho tới khi người dùng/nhóm chốt.
- [ ] Mỗi bước có bằng chứng kiểm tra, không ghi “đã test” nếu chỉ viết test hoặc chỉ build.
- [ ] Không commit/push kế hoạch hay code nếu chưa được giao việc đó.

## 15. Cách cập nhật kế hoạch khi có tài liệu mới

1. Ghi tài liệu/phiên bản/ngày mới, đối chiếu sổ D01–D10.
2. Cập nhật rule trước, rồi DTO/state/model, rồi step/test bị ảnh hưởng; không chỉ thêm một ghi chú ở cuối để hai nơi mâu thuẫn.
3. Vendor đổi: rà adapter, profile snapshot, consent, retention, progress, limits, cancel/recovery; không reset attempt đang chạy theo cấu hình mới.
4. Quyền đổi: sửa policy + permissions DTO + test 401/403/404; không rải kiểm tra role trong endpoint/worker.
5. Retention/consent đổi: xử lý dữ liệu đã lưu và job đang chạy, không backfill thành “đã đồng ý”.
6. Giữ lịch sử ADR bằng quyết định mới; kế hoạch local có thể chỉnh trực tiếp vì chưa phải ADR đã chấp nhận.

Lịch sử kế hoạch: 2026-09-23 — bản đầu; vendor giữ mở theo câu trả lời “chưa này tính sau”; chưa triển khai và chưa chạy kiểm thử code trong lượt lập kế hoạch.

# Danh sách unit test theo module — nguồn cho Report 5.1

Ngày: 03/11/2026. Phạm vi: toàn bộ module backend đang chạy thật của TrustCabinet.
Mục đích: bảng thiết kế test case để điền sheet **MethodList** của
[Report5.1_Unit_Test.xlsx](../08-reports/test-reports/Report5.1_Unit_Test.xlsx), theo đúng sáu cột
mẫu `No | Module Name | Method Name | Sheet Name | Description | Pre-Condition`.

Đây là note làm việc, không thay thế Report 5.1 và không phải nguồn sinh workbook: workbook vẫn
sinh bằng `scripts/generate-test-reports.cmd` (Roslyn đọc test thật trong `tests/`).

## Ba quy tắc khi điền bảng

1. **Unit test ở repo này là tầng service/domain**, không phải endpoint. `Program.cs` và
   `Endpoints/*.cs` chỉ dịch HTTP ↔ service, nên `*EndpointTests`, `*PostgresTests`,
   `*MigrationTests` và test dùng `TestServer` là **integration** — thuộc Report 5.2, không đưa
   vào 5.1. Ứng viên hợp lệ: `*ServiceTests`, `*RulesTests`, `*PolicyTests`, `*RepositoryTests`
   (dùng fake trong `*TestDoubles.cs`).
2. **`Method Name` ghi theo `<Service>.<Method>`**, như mẫu `AccountService.list_users`. Không ghi
   tên test method (`Create_rejects_an_email_that_differs_only_in_case`) vào cột này; tên đó dùng
   cho cột Description hoặc cột Source.
3. **Một dòng là một nhóm case**, không phải một test method. Bộ test hiện có 1.253
   `[Fact]`/`[Theory]`, nên bảng thiết kế giữ ở mức ~120 dòng; danh sách method thật đã nằm trong
   sheet MethodList của workbook.

`Sheet Name` giữ đúng nghĩa cũ: tên worksheet trong workbook (một sheet mỗi module). Nếu mẫu cho
phép thêm cột, thêm **Source** = file test thật, xem bảng cuối note.

## 1. Auth, token, tài khoản, phân quyền

| No | Module Name | Method Name | Sheet Name | Description | Pre-Condition |
|---|---|---|---|---|---|
| 1 | AUTHENTICATION | `AuthenticationService.SignInAsync` | auth | Signs in with a correct email and password, issues an access/refresh pair | Active account with a known password |
| 2 | AUTHENTICATION | `AuthenticationService.SignInAsync` | auth | Refuses a wrong password without revealing whether the email exists | Account exists |
| 3 | AUTHENTICATION | `AuthenticationService.SignInAsync` | auth | Refuses a deactivated account even with the right password | Account with IsActive = false |
| 4 | AUTHENTICATION | `AuthenticationService.SignInAsync` | auth | Returns the role name and today's capabilities, not a cached set | Role capabilities changed after seeding |
| 5 | AUTHENTICATION | `AuthenticationService.RefreshAsync` | auth | Exchanges a valid refresh token and re-reads the account | Signed-in session |
| 6 | AUTHENTICATION | `AuthenticationService.RefreshAsync` | auth | Refuses the refresh once the account is deactivated | Token issued before deactivation |
| 7 | AUTHENTICATION | `AuthenticationService.SignOutAsync` | auth | Revokes the refresh token so replaying it fails | Signed-in session |
| 8 | AUTHENTICATION | `AuthenticationService.SignInWithGoogleAsync` | auth | Signs in an account already linked to the Google subject | User row with GoogleSubjectId set |
| 9 | AUTHENTICATION | `AuthenticationService.SignInWithGoogleAsync` | auth | Refuses an unlinked Google identity and never creates an account | Verified token, no matching sub |
| 10 | AUTHENTICATION | `AuthenticationService.SignInWithGoogleAsync` | auth | Links the subject on the first Google sign-in of an admin-created email, only once | Active account, no Google link |
| 11 | TOKEN | `JwtTokenIssuer.Issue` | token | Emits sub, role and capability claims with an expiry | Account with role capabilities |
| 12 | TOKEN | `GoogleTokenVerifier.VerifyAsync` | token | Rejects a wrong audience/signature or an unverified email | Client id configured |
| 13 | TOKEN | `PasswordHasher.Hash / Verify` | token | Salts so the same password yields different hashes, and verifies them | — |
| 14 | TOKEN | `RedisRefreshTokenStore.ConsumeAsync` | token | Consumes a refresh token once; an unknown token answers null | Redis test instance |
| 15 | TOKEN | `RedisPasswordResetTokenStore.ConsumeAsync` | token | Spends the reset token once and refuses an expired one | Token created |
| 16 | ACCOUNT | `UserService.ListAsync` | account | Lists every account including deactivated, sorted, without the password hash | Administrator |
| 17 | ACCOUNT | `UserService.CreateAsync` | account | Creates an active account and records the audit entry | Free email, existing role, valid input |
| 18 | ACCOUNT | `UserService.CreateAsync` | account | Rejects a duplicate email that differs only in case | Same email already stored |
| 19 | ACCOUNT | `UserService.CreateAsync` | account | Reports every invalid field at once and audits nothing | Blank name, bad email, short password |
| 20 | ACCOUNT | `UserService.CreateAsync` | account | Rejects a role id that no longer exists | Deleted role id |
| 21 | ACCOUNT | `UserService.UpdateAsync` | account | Updates name/email/role and lets an account keep its own email | Existing account |
| 22 | ACCOUNT | `UserService.UpdateAsync` | account | Ends the sessions and notifies the account when its role changes | Account currently signed in |
| 23 | ACCOUNT | `UserService.ChangeStatusAsync` | account | Deactivates an account and ends every session it holds | Another active account |
| 24 | ACCOUNT | `UserService.ChangeStatusAsync` | account | Refuses deactivating oneself and a no-op status change | Caller targets their own id |
| 25 | ACCOUNT | `UserService.RequestPasswordResetAsync` | account | Sends a single-use link; the current password keeps working | Active account with a password |
| 26 | ACCOUNT | `UserService.RequestPasswordResetAsync` | account | Refuses a Google-only or deactivated account, reports a failed send | No password / mail provider down |
| 27 | ACCOUNT | `UserService.ResetPasswordAsync` | account | Spends the token, sets the password, revokes sessions; reuse fails | Valid unexpired link |
| 28 | ACCESS | `AdministratorInvariantGuard` | access | Keeps at least one active account holding AdminUsers | Only one administrator left |
| 29 | ACCESS | `CapabilityGate` | access | 403 for a fixed endpoint without the capability, 404 for a gated route | Signed-in member |
| 30 | ACCESS | `PermissionCatalog` | access | Declares every capability once and the four built-in roles keep theirs | Reference data seeded |
| 31 | ROLE | `RoleService.CreateAsync` | role | Rejects a duplicate name compared without case and an unknown capability | Role already exists |
| 32 | ROLE | `RoleService.UpdateAsync` | role | Refuses built-in roles and writes nothing when nothing changed | Built-in role id |
| 33 | ROLE | `RoleService.DeleteAsync` | role | Refuses a role any account holds, deactivated accounts included | Role with two holders |
| 34 | ROLE | `RolePermissionService.UpdateAsync` | role | Refuses a matrix that leaves nobody able to administer users | Stripping the only admin role |

## 2. Cuộc họp

| No | Module Name | Method Name | Sheet Name | Description | Pre-Condition |
|---|---|---|---|---|---|
| 35 | MEETING | `MeetingService.CreateAsync` | meeting | Stores a draft, its first history row and the seeded participants | Organizer with create capability |
| 36 | MEETING | `MeetingService.CreateAsync` | meeting | Rejects a start in the past and a title/place over the limit | Clock past planned start |
| 37 | MEETING | `MeetingService.ListAsync` | meeting | Returns only meetings the caller created, attends, approves or administers | Mixed data set |
| 38 | MEETING | `MeetingListQueryParser.Parse` | meeting | Parses status/role/date/search filters, falls back safely on junk | Query string |
| 39 | MEETING | `MeetingService.GetAsync` | meeting | Answers not-found for a meeting the caller may not see | Meeting owned by someone else |
| 40 | MEETING | `MeetingService.UpdateAsync` | meeting | Edits only Draft/Rejected; refuses a published, ended or cancelled one | Meeting in each status |
| 41 | MEETING | `MeetingService.GetSubmissionReadinessAsync` | meeting | Names every missing item before submit | Incomplete draft |
| 42 | MEETING | `MeetingService.TransitionAsync (submit)` | meeting | Draft → WaitingApproval notifies the approver and appends history | Draft with approver |
| 43 | MEETING | `MeetingService.TransitionAsync (approve)` | meeting | Publishes, stages invitation emails, refuses an organizer approving itself | WaitingApproval |
| 44 | MEETING | `MeetingService.TransitionAsync (reject)` | meeting | Returns to Draft with the trimmed reason and notifies the organizer | WaitingApproval |
| 45 | MEETING | `MeetingService.TransitionAsync (cancel)` | meeting | Cancels before start and notifies invitees and guests with the reason | Published, future start |
| 46 | MEETING | `MeetingService.TransitionAsync` | meeting | Refuses an illegal transition and a caller without the capability | Ended meeting / plain member |
| 47 | MEETING | `MeetingService.GetHistoryAsync` | meeting | Pages the append-only history newest first and hides it from non-readers | Published meeting |
| 48 | MEETING | `MeetingSubmissionReadinessRules.Validate` | meeting | Same persisted-data policy for the readiness screen and the submit action | Meeting row |

## 3. Nội dung họp, thành phần, điểm danh

| No | Module Name | Method Name | Sheet Name | Description | Pre-Condition |
|---|---|---|---|---|---|
| 49 | AGENDA | `AgendaService.CreateAsync` | agenda | Adds an item only while the meeting is a Draft | Draft, caller is Chair |
| 50 | AGENDA | `AgendaRules.Validate` | agenda | Rejects blank title, zero or oversized duration, unknown presenter | Bad input |
| 51 | AGENDA | `AgendaService.ReorderAsync` | agenda | Reorders and rejects a stale order version | Two editors |
| 52 | AGENDA | `AgendaService.StartAsync` | agenda | Starts one item and refuses a second running item | InProgress meeting |
| 53 | AGENDA | `AgendaService.RecordConclusionAsync` | agenda | Records one conclusion, refuses a second and a non-chair caller | Item in progress |
| 54 | AGENDA | `AgendaService.DeleteAsync` | agenda | Refuses deleting an item once minutes exist | Meeting with minutes |
| 55 | AGENDA | `AgendaService.GetForGuestAsync` | agenda | Shows the agenda to a guest of an open meeting, nothing after it ends | Guest session |
| 56 | PARTICIPANT | `ParticipantService.ReadCreationOptionsAsync` | participant | Offers active internal users with their roles | Draft meeting |
| 57 | PARTICIPANT | `ParticipantService.MutateAsync (AddInternal)` | participant | Adds an internal participant once, refuses a duplicate | Draft meeting |
| 58 | PARTICIPANT | `ParticipantService.MutateAsync (AddGuest)` | participant | Creates a guest invitation whose link only opens that meeting | Draft meeting |
| 59 | PARTICIPANT | `ParticipantService.MutateAsync (ChangeAssignments)` | participant | Refuses removing the last Chair and an unknown role | One Chair only |
| 60 | PARTICIPANT | `ParticipantService.MutateAsync (Respond)` | participant | An invitee answers once, the Chair is never asked, a final answer is frozen | Published meeting |
| 61 | PARTICIPANT | `ParticipantService.MutateAsync (HandOverChair)` | participant | Hands the Chair over atomically; a concurrent second handover loses | Two simultaneous requests |
| 62 | PARTICIPANT | `ParticipantService.ReadAsync (Roster)` | participant | Hides a removed participant from the roster | Participant removed |
| 63 | PARTICIPANT | `ParticipantRules.CanRespond` | participant | Response only while Published/InProgress, never for the Chair | Each status |
| 64 | ATTENDANCE | `AttendanceService.SaveAsync` | attendance | Saves a whole sheet and rejects a stale version token | Two secretaries saving |
| 65 | ATTENDANCE | `AttendanceRules.ReadStored` | attendance | Rejects a corrupt stored status instead of reading it as unmarked | Unknown status string |

## 4. Tài liệu, thư viện, biểu quyết

| No | Module Name | Method Name | Sheet Name | Description | Pre-Condition |
|---|---|---|---|---|---|
| 66 | DOCUMENT | `MeetingDocumentService.UploadAsync` | document | Stores PDF/DOCX for the Chair or Secretary until the meeting ends | Meeting not ended |
| 67 | DOCUMENT | `DocumentFileRules.Validate` | document | Checks real bytes, 50 MB/file, 10 files and 200 MB per submission | Crafted payloads |
| 68 | DOCUMENT | `MeetingDocumentService.UploadAsync` | document | Refuses upload after end/cancel or by a non-chair | Ended meeting |
| 69 | DOCUMENT | `MeetingDocumentService.AddFromLibraryAsync` | document | Adds a library document to the meeting without duplicating its bytes | Document owned by the Chair |
| 70 | DOCUMENT | `MeetingDocumentService.RemoveAsync` | document | Removes the meeting link and purges the orphan file | Meeting-only upload |
| 71 | DOCUMENT | `MeetingDocumentService.AttachAsync` | document | Attaches a linked meeting document to an agenda item, refuses duplicates | Document + agenda item of that meeting |
| 72 | DOCUMENT | `MeetingDocumentService.OpenContentAsync` | document | Streams the file to a reader and answers not-found for anyone else | Signed-in participant |
| 73 | DOCUMENT | `DocumentCommentService.AddAsync` | document | Adds a comment, refuses a blank or >4000-character body | Document in an open meeting |
| 74 | DOCUMENT | `OrphanFilePurger.PurgeAsync` | document | Deletes stored files no document row references, keeps the rest | Storage with orphans |
| 75 | LIBRARY | `PersonalLibraryService.CreateFolderAsync` | library | Refuses a duplicate name at the same level and depth beyond 5 | Owner |
| 76 | LIBRARY | `PersonalLibraryService.MoveFolderAsync` | library | Refuses moving a folder into itself or its own subtree | Nested folders |
| 77 | LIBRARY | `PersonalLibraryService.DeleteFolderAsync` | library | Refuses deleting a folder that still holds items | Folder with documents |
| 78 | LIBRARY | `PersonalLibraryService.UploadAsync` | library | Stores files within the quota and reports each rejected file | Owner with files |
| 79 | LIBRARY | `PersonalLibraryService.ViewAsync` | library | Shows the owner's tree; an administrator may read but not write | Two sessions |
| 80 | VOTING | `VoteService.MutateAsync (Create)` | vote | Creates a draft with at least two unique options, only in preparation | Approved/Published, Chair |
| 81 | VOTING | `VoteService.MutateAsync (Open)` | vote | Opens one session, refuses a second open one and an empty electorate | Draft with two options |
| 82 | VOTING | `VoteService.MutateAsync (Cast)` | vote | Accepts one ballot from an eligible voter, refuses a repeat or an outsider | Open vote |
| 83 | VOTING | `VoteService.MutateAsync (Close)` | vote | Closes, counts and freezes the result | Open vote with ballots |
| 84 | VOTING | `VoteService.MutateAsync (RecordConclusion)` | vote | Records one conclusion, refuses a second or a non-chair caller | Closed vote |
| 85 | VOTING | `VoteService.ConfirmReadAsync` | vote | Blocks a ballot until every attached document was confirmed read | Open vote with one document |
| 86 | VOTING | `VoteService.AttachUploadAsync` | vote | Attaches a file, refuses a document belonging to another meeting | Open vote |
| 87 | VOTING | `VoteService.ListAsync` | vote | Shows the tally to the Chair and hides it from a voter until closed | Different roles |

## 5. Phòng họp, ghi âm, bản ghi, voice

| No | Module Name | Method Name | Sheet Name | Description | Pre-Condition |
|---|---|---|---|---|---|
| 88 | CALL | `CallRoomService.JoinAsync` | call | Issues a join ticket with room name and identity for a participant | Published meeting |
| 89 | CALL | `CallRoomService.JoinAsync` | call | Refuses a non-participant and a meeting that already ended | Removed participant |
| 90 | CALL | `GuestCallRoomService.JoinAsync` | call | Lets a verified guest of this meeting in, refuses another meeting's guest | Guest session |
| 91 | CALL | `CallRoomService.MuteAsync` | call | Mutes another participant, refuses self-mute and an outsider | InProgress room |
| 92 | CALL | `LiveKitWebhookReceiver.Receive` | call | Verifies the signature and closes the session on room_finished | Signed webhook body |
| 93 | RECORDING | `RecordingService.AddAsync` | recording | Accepts an audio/video upload from the Chair, rejects an unsupported type | Ended meeting |
| 94 | RECORDING | `RecordingService.PlaybackAsync / DownloadAsync` | recording | Signs a short-lived URL and refuses download for a secret meeting | Ended meeting with recordings |
| 95 | RECORDING | `RecordingService.EraseAsync / EraseExpiredAsync` | recording | Erases bytes and row with an audit entry; only past the retention date | Mixed retention dates |
| 96 | RECORDING | `RoomTakeService.AppendAsync` | recording | Appends ordered parts, rejects a gap, a closed take and an oversized part | Open take |
| 97 | TRANSCRIPT | `TranscriptionService.GetAsync` | transcript | Returns segments with speakers to a reader, not-found to an outsider | Transcribed recording |
| 98 | TRANSCRIPT | `TranscriptionService.EditSegmentAsync` | transcript | Saves an edited segment text and rejects a blank one | Segment exists |
| 99 | TRANSCRIPT | `SpeakerMatcher` | transcript | Matches a diarised label to a user by folded Vietnamese name, never guesses | Transcript with labels |
| 100 | TRANSCRIPT | `TranscriptionService.ExportAsync` | transcript | Exports the transcript as text with diacritics folded | Transcribed recording |
| 101 | TRANSCRIPT | `TranscriptionService.ProcessNextAsync` | transcript | Claims one queued transcript at a time and requeues interrupted work | Two workers started |
| 102 | SPEECH CLIP | `SpeechClipService.AddAsync` | speech | Accepts a clip only while capture is on and the consent notice was accepted | Room capturing |
| 103 | SPEECH CLIP | `SpeechClipService.SetExclusionAsync` | speech | Excludes a clip from the minutes with a reason, refuses a blank reason | Clip exists |
| 104 | SPEECH CLIP | `SpeechClipService.ExportAsync` | speech | Exports eligible clips and leaves excluded ones out | Meeting with clips |
| 105 | SPEECH CLIP | `SpeechClipService.DeleteForGuestAsync` | speech | Lets a guest delete only their own clip | Guest session |
| 106 | VOICE ID | `VoiceIdService.GrantAsync` | voiceid | Records consent at the current notice version | Signed-in user |
| 107 | VOICE ID | `VoiceIdService.AddSampleAsync` | voiceid | Refuses a sample without consent and stores only an encrypted embedding | Consent granted |
| 108 | VOICE ID | `VoiceIdService.WithdrawAsync` | voiceid | Withdraws consent and deletes every stored sample | Consent with samples |
| 109 | VOICE ID | `VoiceprintProtector` | voiceid | Round-trips an embedding through encryption, never writes plaintext | Key configured |

## 6. Thông báo, email, khách, audit, model

| No | Module Name | Method Name | Sheet Name | Description | Pre-Condition |
|---|---|---|---|---|---|
| 110 | NOTIFICATION | `NotificationService.ListAsync` | notification | Returns only the caller's rows with paging and the unread count | Two users with rows |
| 111 | NOTIFICATION | `NotificationService.MarkReadAsync` | notification | Marks the caller's row read, not-found for someone else's | Row owned by another user |
| 112 | NOTIFICATION | `NotificationService.MarkAllReadAsync` | notification | Clears only the caller's unread rows | Mixed read states |
| 113 | NOTIFICATION | `NotificationWorker` | notification | Queues one starting-soon reminder per invitee and pushes pending rows once | Meeting starting in 15 minutes |
| 114 | EMAIL | `ResendEmailSender.SendAsync` | email | Sends the message and maps a 4xx to a permanent failure without retry | API key configured |
| 115 | EMAIL | `EmailDeliveryWorker` | email | Claims a queued message, retries a transient failure, gives up after the limit | Queue with a failing job |
| 116 | EMAIL | `MeetingInvitationDispatchProcessor` | email | Builds the invitation with an ICS attachment and the full Unicode title | Published meeting |
| 117 | EMAIL | `ResendWebhookReceiver.Receive` | email | Rejects a bad signature and records a bounce | Signed webhook |
| 118 | GUEST | `GuestAccessService.OpenAsync` | guest | Opens the invitation with a valid link token, refuses a bad or expired one | Invitation row |
| 119 | GUEST | `GuestAccessService.VerifyAsync` | guest | Accepts the emailed code once, refuses a wrong and an expired code | Code requested |
| 120 | GUEST | `GuestAccessService.SaveProfileAsync` | guest | Saves display name and job title for the session | Verified session |
| 121 | GUEST | `GuestAccessRules.MeetingOpen` | guest | Blocks documents, roster, agenda and room once the meeting ended | Ended meeting |
| 122 | AUDIT | `AuditService.RecordAsync` | audit | Writes actor, action, target and details, never a password | Any audited action |
| 123 | MODEL | `DatabaseModelTests` | model | Enums as strings, Guid v7 keys, no org unit, voice embedding only in voice_samples | Model built |
| 124 | MODEL | `MessageCodeCatalogueTests` | model | Every MSG code exists in the catalogue with the status it is thrown with | Source tree |

## Cột Source (nếu mẫu cho thêm cột)

| Sheet Name | Source file |
|---|---|
| auth | `tests/TrustCabinet.Web.Tests/Authentication/AuthenticationServiceTests.cs`, `GoogleSignInServiceTests.cs` |
| token | `JwtTokenIssuerTests.cs`, `GoogleTokenVerifierTests.cs`, `PasswordHasherTests.cs`, `RedisRefreshTokenStoreTests.cs`, `RedisPasswordResetTokenStoreTests.cs` |
| account | `tests/TrustCabinet.Web.Tests/Users/UserServiceTests.cs` |
| access | `tests/TrustCabinet.Web.Tests/Access/AdministratorInvariantGuardTests.cs`, `CapabilityGateTests.cs`, `PermissionCatalogTests.cs` |
| role | `tests/TrustCabinet.Web.Tests/Access/RoleServiceTests.cs`, `RolePermissionServiceTests.cs` |
| meeting | `tests/TrustCabinet.Web.Tests/Meetings/MeetingServiceTests.cs`, `MeetingWorkflowServiceTests.cs`, `MeetingListQueryParserTests.cs`, `MeetingSubmissionReadinessTests.cs` |
| agenda | `tests/TrustCabinet.Web.Tests/Agenda/AgendaRulesTests.cs`, `AgendaPoRequirementsTests.cs` |
| participant | `tests/TrustCabinet.Web.Tests/Participants/ParticipantRolePolicyTests.cs`, `ChairHandoverConcurrencyTests.cs` |
| attendance | `tests/TrustCabinet.Web.Tests/Attendance/AttendanceRulesTests.cs`, `AttendanceConcurrencyTests.cs` |
| document | `tests/TrustCabinet.Web.Tests/Documents/DocumentFileRulesTests.cs`, `OrphanFilePurgerTests.cs`, `DocumentCommentEndpointsTests.cs` |
| library | `tests/TrustCabinet.Web.Tests/Documents/PersonalLibraryEndpointsTests.cs` |
| vote | `tests/TrustCabinet.Web.Tests/Votes/VoteReadConfirmationTests.cs`, `VoteDocumentEndpointTests.cs` |
| call | `tests/TrustCabinet.Web.Tests/Calls/CallRoomServiceTests.cs`, `GuestCallRoomServiceTests.cs`, `LiveKitWebhookParsingTests.cs` |
| recording | `tests/TrustCabinet.Web.Tests/Recordings/RecordingEndpointTests.cs`, `RoomTakeEndpointTests.cs` |
| transcript | `tests/TrustCabinet.Web.Tests/Recordings/TranscriptEndpointTests.cs`, `SpeakerMatcherTests.cs`, `TranscriptTakeFitTests.cs` |
| speech | `tests/TrustCabinet.Web.Tests/Recordings/SpeechClipEndpointTests.cs`, `GuestSpeechClipTests.cs` |
| voiceid | `tests/TrustCabinet.Web.Tests/Recordings/VoiceIdEndpointTests.cs` |
| notification | `tests/TrustCabinet.Web.Tests/Notifications/NotificationServiceTests.cs`, `NotificationWorkerTests.cs` |
| email | `tests/TrustCabinet.Web.Tests/Email/ResendEmailSenderTests.cs`, `EmailDeliveryWorkerTests.cs`, `ResendWebhookReceiverTests.cs` |
| guest | `tests/TrustCabinet.Web.Tests/GuestAccess/GuestSessionTests.cs`, `GuestVerificationTests.cs`, `GuestDocumentAccessTests.cs` |
| audit | `tests/TrustCabinet.Web.Tests/Users/AuditServiceTests.cs` |
| model | `tests/TrustCabinet.Web.Tests/DatabaseModelTests.cs`, `MessageCodeCatalogueTests.cs` |

Một số file ở bảng trên là test qua `TestServer`/Postgres (ví dụ `*EndpointTests`, `*ConcurrencyTests`).
Khi điền Report 5.1, chỉ giữ dòng mà case thật sự chạy ở tầng service với fake; phần còn lại chuyển
sang Report 5.2.

## Lưu ý khi nộp

- Bảng phủ 22 module đang chạy thật. **Không** thêm case cho biên bản họp (UC-58–UC-65) và AI summary
  (UC-47–UC-51): chưa implement, chỉ có entity trong ERD.
- Đừng đánh Passed trước khi chạy: `Report5.1` để trống kết quả cho tới khi có TRX, đúng như
  `scripts/generate-test-reports.cmd --overwrite --run-tests`.
- 124 dòng × 3 loại case (normal/failure/boundary) ≈ 370 dòng ở sheet Condition/Confirm; khi trình bày
  chỉ nên show sheet MethodList.
- Tên test thật trong repo theo văn phong `Subject_does_something`
  (`Create_rejects_an_email_that_differs_only_in_case`), dùng cho cột Description hoặc Source khi bảo vệ.

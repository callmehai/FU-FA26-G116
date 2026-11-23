"""Chapter 4 (Design): 1.1 / 1.2 text and tables, 2 Database Design, 4 NFR Design, 5 Other Design.

Figures are placed by s00_diagrams.py (this filler runs after it) and are kept; only text and tables are
added around them. 3.1 / 3.2 are not touched (class and sequence diagrams stay in Report 4).
Sources: Report 4 (section I.1, I.2), docs/02-design/02-erd.md, 03-co-so-du-lieu.md, ADRs, deploy/Caddyfile,
backend/appsettings.json and the authentication code.
"""

from __future__ import annotations

from docx.oxml.ns import qn

from report7_lib import DOCS, Report, _text

W = 6.3

CLASS_FE12 = DOCS / "07-classdiagram" / "feature-12-minutes-management-class-diagram.drawio.png"
SEQ_UC61 = DOCS / "06-sequencediagram" / "minutes-management" / "UC61_SubmitMeetingMinutesForApproval.png"
SEQ_UC64 = DOCS / "06-sequencediagram" / "minutes-management" / "UC64_ApproveAndIssueMeetingMinutes.png"


def _keep_figures(r: Report, h):
    """Drop everything under `h` except picture paragraphs and the first caption of each picture."""
    seen = set()
    for el in r.section(h):
        is_pic = el.tag == qn("w:p") and list(el.iter(qn("w:drawing")))
        t = _text(el).strip()
        if is_pic:
            continue
        if t.startswith("Figure ") and t not in seen:
            seen.add(t)
            continue
        r.body.remove(el)
    sec = r.section(h)
    return sec[-1] if sec else h


def _append(r: Report, anchor, els):
    r.insert_after(anchor, els)


def fill_architecture(r: Report):
    h = r.find_heading("1.1 System Architecture")
    anchor = _keep_figures(r, h)
    els = [
        r.text("TrustCabinet is a three-tier web application with one back-end process. A React single-page application "
               "runs in the browser; one ASP.NET Core 10 application serves the REST API, the SignalR hubs and, in "
               "production, the built front-end from the same origin (so there is no CORS); PostgreSQL holds the data. "
               "Voice and video go through a self-hosted LiveKit media server and speech recognition runs on an in-house "
               "worker, so meeting audio is not sent to an outside AI service. The diagram shows the test server, where "
               "every part is a Docker Compose service behind the Caddy reverse proxy. In development the Vite dev "
               "server proxies /api and /hubs instead of Caddy, and SeaweedFS stands in for Cloudflare R2. Full "
               "component list: Report 4, section I.1."),
        r.table(
            ["Tier", "Component", "Responsibility and key design choice"],
            [
                ("Client", "React 19 SPA (TypeScript, Vite)",
                 "Screens of each business area, one feature folder per area. Capability checks only decide what to "
                 "render; the server authorises every request again."),
                ("Client", "api/client.ts, @microsoft/signalr, livekit-client",
                 "Only module that calls the back-end (relative /api paths, access token refreshed before expiry and "
                 "retried once on 401); WebSocket to /hubs/meetings and /hubs/notifications; WebRTC to LiveKit with a "
                 "token issued by the back-end."),
                ("Application", "Caddy 2 reverse proxy",
                 "Terminates HTTPS (Let's Encrypt), adds the security headers and the Content-Security-Policy, "
                 "forwards /rtc to LiveKit and everything else to the application."),
                ("Application", "ASP.NET Core 10: JWT Bearer, capability policies, Minimal API endpoints",
                 "One endpoint group per business area. An endpoint binds the request, re-authorises it, calls one "
                 "Application service and maps the outcome to an HTTP status and an MSGnn message code."),
                ("Application", "SignalR hubs and change publishers",
                 "MeetingHub and NotificationHub push \"this changed\" events to the groups a user may see; the "
                 "client then refetches through the API, which authorises again."),
                ("Application", "Service layer, ports, repositories, domain",
                 "Clean Architecture in four projects (Web, Application, Infrastructure, Domain). Services depend on "
                 "ports only, run storage inside IUnitOfWork and never see EF Core or an outside SDK; Infrastructure "
                 "implements the ports; Domain references nothing. Direction is enforced by ArchitectureTests."),
                ("Application", "Background workers (hosted services)",
                 "Notification dispatch, email outbox, transcription queue, AI draft queue, call-room reconciliation "
                 "with LiveKit every 30 s, recording retention, auto-cancel of meetings nobody started, orphan file purge."),
                ("Data", "PostgreSQL 17",
                 "System of record, EF Core code-first (migrations, enums as strings, UUID v7 keys)."),
                ("Data", "Redis 8",
                 "Refresh tokens and password-reset tokens only, expiring by TTL. Losing it only signs users out."),
                ("Data", "Object storage and server volume",
                 "Meeting documents and personal library on S3-compatible storage (Cloudflare R2 on the test server, "
                 "SeaweedFS in development); audio recordings on a server disk volume, erased after the retention period."),
                ("External", "LiveKit SFU (self-hosted), speech worker, local language model",
                 "Call rooms; Vietnamese transcription, speaker separation and voice vectors; AI draft of the minutes. "
                 "See section 5."),
                ("External", "Google Identity Services, Resend",
                 "Google sign-in (ID token verified by the back-end); invitation and password-reset email with a "
                 "delivery webhook. See sections 4.1 and 5."),
            ],
            [0.8, 1.9, 3.6]),
    ]
    _append(r, anchor, els)


def fill_packages(r: Report):
    h = r.find_heading("1.2 Package Diagram")
    anchor = _keep_figures(r, h)
    els = [
        r.text("The five views are drawn from the code (project references, using directives and front-end imports). "
               "Dashed arrows are UML dependencies. Descriptions of every package are in Report 4, section I.2."),
        r.table(
            ["Package", "Content", "Depends on"],
            [
                ("TrustCabinet.Domain", "Entities, enums, business rules and policies of each business area.", "Nothing (no project, no package)"),
                ("TrustCabinet.Application", "Use case services with their I*Service interfaces, DTOs and every port "
                 "(repository, query, adapter, IUnitOfWork); about two dozen business modules.", "Domain"),
                ("TrustCabinet.Infrastructure", "DbContext, entity configurations, migrations, seed; repositories, queries "
                 "and adapters (PostgreSQL, Redis, LiveKit, file storage, email, speech worker, language model).", "Application, Domain"),
                ("TrustCabinet.Web", "Endpoints, SignalR hubs, change publishers, background workers, composition root "
                 "(Program.cs); serves the built front-end.", "Application; Infrastructure only to register it"),
                ("frontend/src", "app (route table), features (one folder per business area), components (shell, shared UI), "
                 "lib, api (HTTP and SignalR layer), types.", "Back-end over HTTP and SignalR"),
                ("tests", "TrustCabinet.Web.Tests (in-memory host on a real PostgreSQL) and TrustCabinet.Application.Tests "
                 "(hand-written fakes, no database).", "Web / Application"),
            ],
            [1.7, 3.2, 1.4]),
    ]
    _append(r, anchor, els)


def fill_intro(r: Report):
    h = r.find_heading("Chapter 4 - System Design")
    els = [
        r.text("This chapter describes how TrustCabinet is built: section 1 presents the system architecture and the "
               "package structure, section 2 the database design, section 3 the detailed design of the key feature "
               "(the meeting-minutes approval loop, from draft to issued minutes, with three representative diagrams), section 4 how the design meets the "
               "non-functional requirements, and section 5 the integration of outside systems and background processing. "
               "The full set of class and sequence diagrams of every feature stays in Report 4 (Software Design Specification), "
               "Part II; this chapter shows only the representative ones and summarises the rest in compact tables."),
    ]
    r.replace_section(h, els)


def fill_database(r: Report):
    h = r.find_heading("2. Database Design")
    anchor = _keep_figures(r, h)
    els = [
        r.text("The database is PostgreSQL 17 through EF Core 10, code-first: the model is C# code and the schema comes "
               "from migrations; nobody edits the schema by hand. The target model has 37 business entities, four join "
               "tables (MeetingDocumentAgendaItem, VoteEligibleVoter, VoteDocument, DocumentReadConfirmation) and "
               "technical tables such as GuestVerificationChallenge and MeetingInvitationDispatch (43 tables in the "
               "ERD). The full per-table description with columns is in Report 4, section I.3; this section states the "
               "conventions that apply across the model, then describes each core table in detail."),
        r.text("General conventions", bold=True),
        r.table(
            ["Convention", "Decision and reason"],
            [
                ("Keys", "Guid.CreateVersion7(): time-ordered, so inserts stay index-friendly and ids need no round trip."),
                ("Enums", "Stored as strings, readable in the database and safe against reordering of the enum."),
                ("Users", "Deactivated, never deleted, so historical records stay attributable (ADR-0049)."),
                ("E-mail", "citext column with a unique index: case-insensitive sign-in without lower-casing in queries."),
                ("Concurrency and uniqueness", "Rules the application depends on are enforced by the database: filtered unique "
                 "indexes (one Chair per meeting, one open vote per meeting, one open speaking request per person) and "
                 "check constraints (exactly one of member or guest on votes, comments and speech clips)."),
                ("Evidence", "MeetingStatusHistory and the audit log are append-only: the application never updates or deletes a row."),
                ("Migrations", "A migration that reached the main branch is immutable and corrected by a new one. "
                 "Outside Development the app never migrates at start-up; the deploy runs TrustCabinet.Web.dll --migrate "
                 "after a pg_dump backup (ADR-0047)."),
            ],
            [1.6, 4.7]),
        r.text("Core tables in detail (14 groups; full column list: Report 4, section I.3; attribute "
                "reference: docs/02-design/02-erd.md)", bold=True),
        r.table(
            ["Table(s)", "Purpose, key columns and integrity rules"],
            [
                ("users, roles, role_capabilities",
                 "Access. User: FullName, Email (citext, unique, case-insensitive sign-in), PasswordHash "
                 "(nullable for Google-only accounts), RoleId, IsActive. Users are deactivated, never deleted. "
                 "Roles are data (ADR-0008); three built-in roles have fixed ids; capabilities are checked per "
                 "request, not stored on the token beyond claims."),
                ("meetings",
                 "One row per session (ADR-0013): Title, Status (Draft..Cancelled), Sensitivity, Mode, Location, "
                 "OnlineLink, PlannedStartTime/PlannedEndTime, ActualStartedAt/ActualEndedAt, cancellation fields, "
                 "CreatedById, AssignedApproverId. No MeetingSession table."),
                ("meeting_status_history",
                 "Append-only lifecycle evidence: MeetingId, Action, FromStatus/ToStatus, ActorId (null = system), "
                 "Reason (required on reject/cancel), OccurredAt. Never updated or deleted."),
                ("meeting_participants, meeting_guest_invitations (+ guest_sessions)",
                 "Two rosters. Participants: MeetingId, UserId, MeetingRole (Chair/Secretary/Member), "
                 "InvitationResponse vs Attendance (separate questions), AssignmentStatus Current/Removed with "
                 "RemovedAt/By (history kept); filtered unique index keeps one Chair per meeting. Guests have no "
                 "User/role: InvitedName, Email, SensitivityAtInvitation, Response, RevokedAt/By, self-declared "
                 "name/job title. A verified guest holds one GuestSession row (12 h, per device, revoked on "
                 "sign-out or invitation revoke); invitations store no token (HMAC-derived link)."),
                ("agenda_items",
                 "MeetingId, Position, Title, Description, Status (Pending/InProgress/Done), presenter as member "
                 "(PresenterId) or guest (PresenterGuestInvitationId, never both), Conclusion + ConclusionUpdatedBy/At "
                 "(max 2,000 chars), ActualStartedAt/EndedAt."),
                ("stored_files, documents, folders",
                 "Immutable file objects + personal library. StoredFile: StorageKey (R2 object), OriginalFileName, "
                 "ContentType (from content, PDF/DOCX), ByteSize, ContentSha256, PageCount (PDF only), UploadedById/At, "
                 "PurgedAt. Document: OwnerId, FolderId (null = library root), StoredFileId, FileName. Folder: OwnerId, "
                 "ParentId (max 5 levels), MeetingId for auto-created upload folders. No versioning (ADR-0018); "
                 "an orphan-file job purges unreferenced objects."),
                ("meeting_documents (+ meeting_document_agenda_items), document_comments",
                 "Meeting's view of files. MeetingDocument: MeetingId, StoredFileId, DocumentId (nullable source row, "
                 "SetNull), FileName, AddedBy/At, RemovedAt/By (soft remove); unique (MeetingId, StoredFileId) while "
                 "active. Join table links one file to many agenda items. DocumentComment: MeetingDocumentId, exactly "
                 "one of AuthorId (member) / GuestInvitationId, PageNumber, Body, IsResolved; one reply level (BR-317)."),
                ("voting_sessions, voting_options (+ vote_eligible_voters, vote_documents)",
                 "One vote per meeting, open only while InProgress (ADR-0014). Session: Title, IsAnonymous, "
                 "EligibleRoles (Chair/Secretary/Member/Guest), Status (Draft/Open/Closed), OpenedAt/ClosedAt, "
                 "EligibleCount frozen at open, Conclusion (Passed/NotPassed/Deferred) + ConcludedBy/At. Option: "
                 "Position, Label, BallotCount tally. Filtered unique index allows one Open vote per meeting. "
                 "Eligible voters and attached documents are frozen when the vote opens."),
                ("vote_receipts, vote_choices (+ document_read_confirmations)",
                 "Ballots. Receipt: exactly one of VoterId/GuestInvitationId, unique per session (one person, one "
                 "vote, enforced by the database). Choice rows exist only for public ballots; secret ballots live "
                 "only in VotingOption.BallotCount, so no row links voter to choice. Read confirmations (UC-74) bind "
                 "a reader to a meeting document, independent of any vote."),
                ("recordings",
                 "Secretary-uploaded audio: MeetingId, UploadedById, Source (BrowserCapture/Upload/CallRoom/Combined), "
                 "Status, StoragePath on a server volume, DurationMs, ConsentObtainedAt + ConsentPurpose (required "
                 "before recording), RetainUntil/DeletedAt for the retention purge. Personal data: erased after retention."),
                ("transcripts, transcript_segments, transcript_speakers (+ speech_clips)",
                 "Transcript: one per recording (RecordingId unique), Status, Provider, Language, ProgressPercent, "
                 "ErrorMessage. Segment: Position, StartMs/EndMs, Text, WordStartsMs/WordConfidence arrays (per-word "
                 "timing and confidence, nulled when the Secretary edits the text), SpeakerLabel, EditedById, "
                 "SpeakerAssignedBy/At (Secretary stays the decision maker). Speaker: Label, SpeechMs, machine "
                 "suggestion (SuggestedUserId + score) apart from assignment (AssignedUserId or AssignedGuestId). "
                 "SpeechClip: one spoken sentence with its audio for replay, exactly one of SpeakerId/GuestInvitationId."),
                ("meeting_minutes",
                 "One revision per row: MeetingId, RevisionNumber, Content (allow-listed HTML, ADR-0055), Status "
                 "(Draft/WaitingApproval/Rejected/Issued/Superseded), SubmittedBy/At, ApprovedBy/At, IssuedAt, "
                 "RejectionReason (max 2,000), ReturnedBy/At, SupersedesId (correction points at the replaced "
                 "revision), BuiltFromDraftAt. Issued text is immutable; a correction is a new revision."),
                ("meeting_summaries, ai_provider_settings",
                 "AI draft loop (SCRUM-36). MeetingSummary: one row per meeting doubling as the job queue "
                 "(RequestedBy/At, Status Queued/Running/Ready/Failed, ErrorMessage, GeneratedAt), Content (JSON of "
                 "the three fixed sections), review fields (ReviewedBy/At, EditedBy/At). AiProviderSetting: single "
                 "row (fixed id), Mode Local/Api, ApiBaseUrl/ApiModel, ApiKeyCiphertext (AES-256-GCM, never "
                 "returned) + 4-char hint."),
                ("notifications, email_messages",
                 "Notifications: UserId (exactly one owner), MeetingId (nullable context), TargetType/TargetId "
                 "(API builds the action URL; routes are not stored), ReadAt (null = unread), DeduplicationKey "
                 "(unique, e.g. meeting-starting-soon), PushedAt (worker push state). EmailMessage: outbox with "
                 "DeduplicationKey per send event, Status/RetryCount, provider message id and webhook delivery "
                 "state; sending never blocks a request."),
                ("audit_log_entries (+ call_sessions, call_participants, calendar_shares)",
                 "AuditLogEntry: append-only business evidence (ActorId nullable, Action, ObjectType/ObjectId, "
                 "IpAddress, Details JSON, OccurredAt). Minor tables in one line: call_sessions/call_participants "
                 "(one open stint per person, member = UserId xor guest = GuestSessionId), calendar_shares "
                 "(owner, viewer, level, date range, status; busy times computed at read, never stored)."),
            ],
            [1.9, 4.4]),
    ]
    _append(r, anchor, els)


def fill_functional(r: Report):
    h = r.find_heading("3. Functional Design")
    end = r.find_heading("4. NFR Design Specifications")
    H = r.heading
    els = [
        r.text("The key feature is the meeting-minutes loop (FE-12, UC-58–UC-61, UC-64, UC-65 and UC-83 (UC-62/UC-63 signing dropped by PO 14/11/2026); SCRUM-32, SCRUM-33): the "
               "Secretary drafts the minutes from the meeting records, the Chair approves and issues them, and an issued "
               "revision is never edited \u2014 a correction is a new revision. It is the feature that closes the meeting "
               "loop: TrustCabinet stops at the approved minutes. Full class and sequence diagrams of every feature are in "
               "Report 4, Part II; this section shows the three representative FE-12 diagrams and summarises the rest "
               "in compact tables with the key design decisions."),
        H("3.1 Class Diagram", 3),
        r.text("Participating classes of the minutes approval loop (FE-12, UC-58–UC-61, UC-64, UC-65, UC-83). "
               "Source: docs/07-classdiagram/feature-12-minutes-management-class-diagram.drawio.png "
               "(drawn 20/11/2026, PR #419 review; entities per docs/02-design/02-erd.md)."),
        *r.figure(4, CLASS_FE12, "Class Diagram of FE-12 Minutes Management (UC-58–UC-61, UC-64, UC-65, UC-83)"),
        r.text("Reading guide for the diagram", bold=True),
        r.table(
            ["Class", "Responsibility"],
            [
                ("MeetingMinutes (Domain entity)", "One revision of the minutes: MeetingId, RevisionNumber, Content, Status "
                 "(Draft, WaitingApproval, Rejected, Issued, Superseded), SubmittedBy/At, ApprovedBy/At, RejectionReason, "
                 "ReturnedBy/At, IssuedAt, SupersedesId pointing at the replaced revision, BuiltFromDraftAt."),
                ("MeetingSummary (Domain entity)", "The AI draft the minutes are built from (SCRUM-36, UC-47-UC-50); one row "
                 "per meeting; the Secretary reviews it (UC-50), and the minutes take the current draft reviewed or not (BR-382)."),
                ("MinutesService (Application, IMinutesService)", "Use cases UC-58-UC-61, UC-64, UC-65 and UC-83: read, build from records, "
                 "save, submit, approve, return, correct, export. Runs inside IUnitOfWork; state change and notifications "
                 "are saved together so nobody is told what did not happen."),
                ("MinutesEndpoints (Web)", "REST routes: GET+POST on meetings/{id}/minutes (read, build), PUT (save), POST /submit, /approve, /reject (return UC-65), "
                 "/corrections, read one revision, /export. Re-authorises the caller on every route."),
                ("MinutesTemplate / MinutesRenderer (Application)", "Builds the draft content from agenda, conclusions, vote "
                 "results and the Secretary's notes; sanitises saved HTML against a fixed allow-list (ADR-0055) and "
                 "renders the Word/PDF export."),
                ("Notification, EmailMessage, AuditLogEntry", "In-app notification saved with each state change (BR-411) and "
                 "pushed by the dispatcher; email outbox; the business action is written to the append-only audit log."),
            ],
            [2.0, 4.3]),
        H("3.2 Sequence Diagram(s)", 3),
        r.text("Main flow of the approval loop, condensed from the sequence diagrams in Report 4, section II.12 "
               "(UC-58-UC-61, UC-64, UC-65, UC-83). Only the Chair and the Secretary see a revision before it is issued "
               "(BR-386, BR-412); everyone entitled to read the meeting sees it once issued. "
               "Two representative diagrams are shown below; the full set of seven FE-12 sequence diagrams stays in "
               "Report 4 and docs/06-sequencediagram/minutes-management/."),
        *r.figure(4, SEQ_UC61, "Sequence Diagram of UC-61 Submit Meeting Minutes for Approval (source: "
                               "docs/06-sequencediagram/minutes-management/UC61_SubmitMeetingMinutesForApproval.png)"),
        *r.figure(4, SEQ_UC64, "Sequence Diagram of UC-64 Approve and Issue Meeting Minutes (source: "
                               "docs/06-sequencediagram/minutes-management/UC64_ApproveAndIssueMeetingMinutes.png)"),
        r.table(
            ["Step", "Actor and action", "State change"],
            [
                ("1. View (UC-58)", "Secretary or Chair opens the minutes tab; the service returns the current revision "
                 "plus the list of earlier revisions.", "None; a reader who is neither officer learns nothing about the draft."),
                ("2. Generate draft (UC-59)", "Secretary builds the draft from the meeting records (agenda conclusions, vote "
                 "results, Secretary's notes) and the current AI draft (SCRUM-36; reviewed or not, per BR-382).", "Draft created (RevisionNumber 1, or kept); BuiltFromDraftAt recorded."),
                ("3. Edit and save (UC-60)", "Secretary edits the rich text and saves as often as needed; the server keeps "
                 "only the allow-listed HTML (ADR-0055).", "Stays Draft (or Rejected); only Draft or Rejected revisions are editable."),
                ("4. Submit (UC-61)", "Secretary sends the revision to the Chair; the Chair is notified (BR-411).",
                 "Draft/Rejected becomes WaitingApproval; SubmittedBy/At recorded."),
                ("5a. Approve and issue (UC-64)", "Chair approves; earlier issued revisions become Superseded; readers of the "
                 "meeting are notified in-app and by email (BR-411) and the issue is written to the audit log (BR-446).", "WaitingApproval becomes Issued; IssuedAt and ApprovedBy/At recorded; content locked."),
                ("5b. Return (UC-65)", "Chair returns the revision with a reason; the Secretary is notified.",
                 "WaitingApproval becomes Rejected; RejectionReason, ReturnedBy/At recorded; editable again."),
                ("6. Correct (UC-83)", "After issue, Secretary creates a correction: a new revision pointing at the issued "
                 "one, going through steps 3-5 again.", "New revision starts at Draft; the old one stays Issued until the new one is approved, then becomes Superseded."),
            ],
            [1.2, 2.9, 2.2]),
        r.text("Key design decisions", bold=True),
        *r.bullets([
            "One approver: only the Chair issues or returns, only the Secretary submits (meeting-role check in the "
            "service, not just the UI).",
            "Issued text is immutable: a correction is a new revision with SupersedesId, so what was issued at the time "
            "survives as evidence.",
            "Draft confidentiality: until issued, a revision is visible only to the Chair and the Secretary (BR-386, BR-412).",
            "Notifications are part of the transaction: the state change and the bell notification are saved in one unit "
            "of work (BR-411).",
            "Export (Word/PDF) renders the locked issued content through MinutesRenderer; the two officers may also export a draft "
            "for review but is not the issued record.",
        ]),
    ]
    r.replace_until(h, end, els)


def fill_nfr(r: Report):
    h = r.find_heading("4. NFR Design Specifications")
    end = r.find_heading("5. Other Design Specifications")
    H = r.heading
    els = [
        r.text("Non-functional requirements are specified in Chapter 3, section 4. Each subsection below states how the "
               "design meets them; every statement was checked against the code or the deploy files."),
        H("4.1 Security Design", 3),
        H("4.1.1 Authentication Flow", 4),
        r.text("Authentication is stateless: a short-lived signed access token plus a rotating refresh token. There is "
               "no server-side session and no CORS (same origin)."),
        r.table(
            ["Aspect", "Design decision"],
            [
                ("Sign-in methods", "Email and password, or Google. Google: the browser obtains a Google ID token and sends it "
                 "to the back-end, which verifies it against Google's signing keys and links it to the existing account with the "
                 "same email on first use (ADR-0009). No separate identity provider."),
                ("Password storage", "ASP.NET Core PasswordHasher: PBKDF2-HMAC-SHA512, 210,000 iterations, random salt per "
                 "password; parameters are embedded in the hash so old hashes stay verifiable."),
                ("Access token", "JWT signed with HMAC-SHA256, issuer and audience TrustCabinet, 15 minutes, no clock skew. "
                 "Claims: user id, token id (jti), email, name and one claim per capability. There is no role claim, because "
                 "nothing authorises on the role name (ADR-0008). Kept in browser memory only; localStorage holds just a "
                 "\"signed in\" marker."),
                ("Refresh token", "256-bit random opaque string, 7 days, sent only in the cookie tc_refresh_token with "
                 "HttpOnly, Secure, SameSite=Strict and Path=/api/auth, so JavaScript (and an XSS) cannot read it and other "
                 "sites cannot spend it. The back-end keeps only its SHA-256 hash in Redis with a TTL."),
                ("Rotation and theft detection", "A refresh token is single use: refreshing consumes it atomically (Lua script in "
                 "Redis) and issues a new pair. A second use after a 60-second grace window is treated as theft and revokes every "
                 "session of the account. The front-end refreshes before expiry and retries a 401 once."),
                ("Brute-force limits", "Per minute: 60 requests per IP on the authentication endpoints and 5 sign-in attempts per "
                 "email; excess answers 429 with MSG414 and Retry-After. The account is not locked (UC-09 was dropped)."),
                ("Password reset", "Single-use token held in Redis with a TTL, delivered by email."),
                ("Real-time channel", "SignalR hubs authenticate with the same access token and close the connection when it expires."),
                ("Guests", "Link derived by HMAC from the invitation id (no stored token), email one-time code stored only as an "
                 "HMAC with a counter of wrong tries, then a GuestSession of 12 hours that is revoked on sign-out or when the "
                 "invitation is revoked."),
            ],
            [1.5, 4.8]),
        H("4.1.2 Authorization and RBAC Implementation", 4),
        r.table(
            ["Aspect", "Decision"],
            [
                ("Model", "Role to capabilities (role_capabilities). The back-end checks capabilities, not role names, through "
                 "RequireCapability on every endpoint group. The administrator edits the matrix at run time (ADR-0007); a change "
                 "does not sign users out (ADR-0022, ADR-0023) and is pushed to connected browsers."),
                ("Two layers", "System role (what screens and functions a person may use) is independent of the meeting role "
                 "Chair, Secretary, Member (what they may do in one meeting) and of the guest, who acts as a Member only inside "
                 "the invited meeting."),
                ("Row-level access", "List endpoints filter rows on the server by participation, sensitivity and meeting role "
                 "(MeetingAudience, read policies); a record the caller may not see never reaches the browser. Documents follow "
                 "the right to view the meeting (ADR-0020)."),
                ("403 or 404", "401 when not signed in; 403 for a missing capability on a fixed endpoint; 404 for one specific "
                 "resource the caller may not read, so its existence is not confirmed; a restricted UI route renders the same "
                 "404 as an unknown URL."),
                ("UI is not the boundary", "RequireCapability and sidebar filtering in the SPA only decide what to render; "
                 "every endpoint authorises again."),
                ("Audit", "Business actions are written to the append-only audit log with the caller's IP address."),
            ],
            [1.5, 4.8]),
        H("4.1.3 Data Protection", 4),
        r.table(
            ["Data", "At rest", "In transit"],
            [
                ("All traffic", "-", "HTTPS only: Caddy obtains and renews the certificate; HSTS max-age one year."),
                ("Passwords", "PBKDF2-HMAC-SHA512 hash, never reversible.", "Sent once over HTTPS at sign-in."),
                ("Tokens", "Refresh and reset tokens in Redis as SHA-256 hashes with TTL; guest codes as HMAC.", "Refresh token in an HttpOnly Secure cookie."),
                ("Voice embeddings (biometric)", "AES-256-GCM, key from VoiceId:EncryptionKey (32 bytes); the associated data binds "
                 "the ciphertext to the person, the sample and the model, so a copied value cannot be moved to another row. "
                 "Sample audio is encrypted the same way. Stored nowhere else.", "Between the back-end and the speech worker only; the worker stores nothing."),
                ("Consent and deletion", "Voice ID needs its own consent (version of the notice, purpose, time). Withdrawing "
                 "deletes every sample and suggestion in one transaction and cannot be undone; deactivating the account ends "
                 "Voice ID (ADR-0049). Recordings and speech clips are erased after the retention period.", "-"),
                ("AI provider key", "AES-256-GCM ciphertext; only the last four characters are ever shown.", "Sent to the provider over HTTPS."),
                ("Documents", "Immutable objects in object storage reached only by the back-end; type checked by content (PDF, DOCX).", "Streamed through the API after the access check."),
                ("Media", "Recordings on a server volume.", "WebRTC (DTLS-SRTP) between browser and LiveKit."),
            ],
            [1.4, 3.1, 1.8]),
        H("4.1.4 Input Validation Strategy", 4),
        r.table(
            ["Layer", "Validation"],
            [
                ("Front-end forms", "Checks for fast feedback only; never trusted."),
                ("Endpoint and service", "The endpoint binds the request; the Application service validates every rule and answers "
                 "with an MSGnn code from one catalogue (a test fails on a code that is not catalogued)."),
                ("Persistence", "EF Core parameterised queries; check constraints, unique indexes and foreign keys back every business invariant."),
                ("Uploads", "File type decided from the content, page count read by a PDF inspector, size and storage quota checked (ADR-0034)."),
                ("Rich text", "Minutes are HTML from an editor; the server filters every write against a fixed allow-list and returns the cleaned text (ADR-0055)."),
                ("Rate limiting", "Authentication endpoints, see 4.1.1."),
            ],
            [1.6, 4.7]),
        H("4.1.5 Security Headers", 4),
        r.text("TrustCabinet is same-origin: the front-end is served by the same host as the API, so CORS is not enabled. "
               "On the test server Caddy adds these headers to every response, /rtc included (deploy/Caddyfile). Local "
               "development has none."),
        r.table(
            ["Header", "Value", "Purpose"],
            [
                ("Strict-Transport-Security", "max-age=31536000", "Browser uses HTTPS only"),
                ("X-Content-Type-Options", "nosniff", "Uploaded files are not run as script"),
                ("X-Frame-Options", "DENY", "No clickjacking"),
                ("Referrer-Policy", "strict-origin-when-cross-origin", "Links carry the domain, not meeting ids"),
                ("Content-Security-Policy", "default-src 'self'; script-src 'self' and Google Sign-In; connect-src 'self' and "
                 "Google; object-src 'none'; frame-ancestors 'none'; base-uri and form-action 'self'",
                 "A script injected by XSS cannot run or send data out. 'unsafe-inline' and 'unsafe-eval' are never allowed for "
                 "scripts; a new outside source must be added to the policy first."),
            ],
            [1.7, 2.6, 2.0]),
        H("4.2 Performance and Scalability Design", 3),
        H("4.2.1 Caching Strategy", 4),
        r.text("The application has no response cache and no in-memory cache layer: the code does not use IMemoryCache, "
               "IDistributedCache or output caching. Redis is used for tokens only (4.1.1). Freshness comes from "
               "SignalR events that tell the client what changed, after which it refetches. Static front-end files are "
               "served with compression by Caddy (zstd, gzip)."),
        H("4.2.2 Database Query Optimisation", 4),
        r.table(
            ["Technique", "Where"],
            [
                ("Indexes", "About 76 indexes declared in entity configurations, many filtered or unique to carry business rules."),
                ("Read-only queries", "AsNoTracking on reads (157 uses); AsSplitQuery on queries that load several collections."),
                ("Paging", "List endpoints page on the server (Skip/Take); the browser never receives the whole table."),
                ("Set-based writes", "ExecuteUpdate and ExecuteDelete for bulk changes such as retention purges."),
                ("Token checks", "The access token carries the capabilities, so an ordinary request does not read the role tables."),
                ("Queues in the database", "Email, transcription and AI draft jobs are rows with a lease, so one worker takes one job and retries are safe."),
            ],
            [1.7, 4.6]),
        H("4.2.3 Scaling Approach", 4),
        r.text("Current deployment is one Lightsail machine (2 vCPU, 4 GB) running all Compose services; scale-out is "
               "not implemented. Measured with tests/load on that server (3 Nov 2026):"),
        r.table(
            ["Aspect", "Measured or designed behaviour"],
            [
                ("Joining a call room", "30 people in 10 s: p95 169 ms; 120 connections (30 x 4 tabs) in 10 s: p95 1.5 s, no error."),
                ("Real-time speech", "Up to about 80 sentences per minute for the whole server stays at p95 under 2 s; the limit is about 85 "
                 "per minute, after which the speech queue grows and the client retries after MSG312."),
                ("Practical size", "A meeting of 25-30 people with fewer than 8 cameras runs comfortably; the speech worker, which shares the CPU, is the main limit."),
                ("Next steps", "A 4 vCPU machine or a separate speech machine; the back-end holds no session state (tokens in Redis, "
                 "files in object storage), so more than one app instance is possible, but SignalR would then need a backplane, "
                 "which is not built."),
            ],
            [1.7, 4.6]),
    ]
    r.replace_until(h, end, els)


def fill_other(r: Report):
    h = r.find_heading("5. Other Design Specifications")
    nxt = r.find_heading("Chapter 5 – Delivery Package")
    H = r.heading
    els = [
        H("5.1 System Integrations", 3),
        r.text("Every outside system sits behind a port declared in Application and an adapter in Infrastructure, so a "
               "service never calls a third-party SDK and an adapter can be replaced or faked in tests."),
        r.table(
            ["System", "Used for", "Design", "When it is down"],
            [
                ("LiveKit (self-hosted SFU, WebRTC)", "Call rooms, audio and video (ADR-0010)",
                 "Back-end creates rooms and issues 10-minute access tokens through LiveKit's server API; signalling "
                 "passes Caddy at /rtc, media goes straight to ports 7881/7882; a worker reconciles rooms every 30 s.",
                 "Call endpoints answer 503; the rest of the system works."),
                ("Speech worker (Python, sherpa-onnx, CPU)", "Vietnamese transcription, speaker separation, voice vectors (ADR-0026)",
                 "In-house HTTP service with an API key, reached over the Docker network or Tailscale (ADR-0036); keeps no data and "
                 "calls no outside AI. Transcription runs one recording at a time.",
                 "Transcription and Voice ID answer 503 / MSG312; browsers keep and resend the speech clips."),
                ("Local language model", "AI draft of the minutes (ADR-0054)",
                 "OpenAI-compatible chat completions API served by a team machine (for example Ollama) over Tailscale; "
                 "transcript cut per agenda item, temperature 0, spoken numbers converted by code; output is always labelled "
                 "as AI and visible only to the Chair and Secretary until approved. Normal meetings may use an external API "
                 "chosen by the administrator (ADR-0056); a second local machine is the fallback (ADR-0063).",
                 "Draft button reports an error; the rest works."),
                ("Resend (email API)", "Invitations, reminders, password reset (ADR-0031)",
                 "Outbox table with de-duplication key; a worker sends with retry (8 attempts, 60 s apart); a signed webhook "
                 "returns delivery status. SMTP is available as an alternative provider.",
                 "Mail waits in the outbox and is retried."),
                ("Cloudflare R2 (S3 API)", "Meeting documents and personal library (ADR-0024)",
                 "S3 client with bucket and keys from configuration; SeaweedFS replaces it in development.",
                 "Upload and download answer 503 / MSG166."),
                ("Google Identity Services", "Sign-in with Google (ADR-0009)", "ID token verified by the back-end; allowed in the CSP.", "Password sign-in still works."),
            ],
            [1.3, 1.3, 2.5, 1.2]),
        H("5.2 Background Processing and Data Retention", 3),
        r.table(
            ["Worker", "Behaviour"],
            [
                ("NotificationWorker / email delivery", "Creates due reminders and sends the outbox."),
                ("TranscriptionWorker, SummaryWorker, CombinedRecordingWorker", "Queues for the speech worker, the language model and the mix of a hybrid meeting into one recording."),
                ("CallRoomJanitor", "Reconciles call rooms with LiveKit every 30 s."),
                ("MeetingOverdueWorker", "Cancels a Published meeting nobody started 24 h after its planned start and ends a forgotten InProgress one (ADR-0032, ADR-0062)."),
                ("RecordingRetentionWorker and periodic clean-up", "Erases recordings and speech clips after the retention period; read notifications after 90 days, others after 180; email bodies after 30 days; orphan files after a 24 h grace (ADR-0067)."),
            ],
            [2.3, 4.0]),
    ]
    r.replace_until(h, nxt, els)


def fill(r: Report):
    fill_intro(r)
    fill_architecture(r)
    fill_packages(r)
    fill_database(r)
    fill_functional(r)
    fill_nfr(r)
    fill_other(r)

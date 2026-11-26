# Danh mục Quy tắc Nghiệp vụ (Business Rules) — TrustCabinet

> **GENERATED — DO NOT EDIT.** Bản trích nguyên văn Report 3 §V.1. Sửa DOCX trong
> [`docs/08-reports/source/`](../08-reports/source/README.md) rồi chạy
> `python scripts/sync_reports.py --source docs/08-reports/source/Report_3_Software_Requirement_Specification.docx --report 3`
> (hoặc `python scripts/sync_requirement_extracts.py`). Chi tiết: [08-reports/README.md](../08-reports/README.md).

Có **154 BR**, đánh số `BR-001` → `BR-154` theo [ADR-0079](../05-decisions/0079-cho-danh-so-lai-br-uc-co-bang-anh-xa.md)
và [kế hoạch gộp BR](08-ke-hoach-gop-business-rules.md). Mỗi BR một câu, một quyết định nghiệp vụ; giao diện, kiểm tra
dữ liệu nhập và giới hạn kỹ thuật nằm trong UC Specification hoặc mục IV (NFR) của Report 3. Mã `BR` độc lập với mã
thông báo `MSG`.

## Quy ước

- UC Specification và Sequence Diagram dẫn chiếu BR ID tại bước áp dụng, không chép lại nội dung.
- Mã cũ (`BR-01` → `BR-468`) đã ngừng dùng; tra mã mới hoặc nơi chuyển đến ở
  [09-bang-anh-xa-br-dot-1.md](09-bang-anh-xa-br-dot-1.md). Bảng đó gồm cả các BR đã ngừng trước đợt đánh số lại.
  ADR, note và test report cũ giữ mã cũ.

## Danh mục BR

| ID | Rule |
|---|---|
| `BR-001` | A deactivated account cannot sign in or use any function; all its active sessions are ended and its historical data is kept. |
| `BR-002` | An active account can sign in even when its role has no capability. |
| `BR-003` | Google sign-in succeeds only when the email matches an existing internal account; no account is created automatically. |
| `BR-004` | A sign-in error message does not reveal whether the email exists. |
| `BR-005` | An email address belongs to at most one account. |
| `BR-006` | A newly created account is Active. |
| `BR-007` | Only capabilities defined by the system can be granted to a role. |
| `BR-008` | A change to a role's capabilities or to a user's role takes effect from the user's next action, without ending the session. |
| `BR-009` | Account and role administration changes and every sign-in are recorded in the audit log. |
| `BR-010` | Issuing minutes, viewing a document of a Secret Meeting, exporting a report and sharing a calendar are recorded in the audit log. |
| `BR-011` | Recording, playing or deleting a voice sample, and reading a deactivated account's library, are recorded in the audit log. |
| `BR-012` | Only a role with the user-administration capability views the audit log; the audit log is read-only and nobody can edit or delete it. |
| `BR-013` | A new Meeting starts in Draft; its planned start is later than now and its planned end is later than its planned start. |
| `BR-014` | Meeting information can be edited only in Draft or Rejected; editing does not change the status. |
| `BR-015` | For a Published Meeting, only the Member who created it changes the schedule or the location; the new start must be later than now. |
| `BR-016` | Only the Member who created the Meeting, its Approver, the Chair, the Secretary or a participant of a Meeting can access that Meeting. |
| `BR-017` | The participant list and the invitation responses are shown only to people who can view the Meeting. |
| `BR-018` | Submitting for approval moves a Draft or Rejected Meeting to WaitingApproval; while it waits, its general information cannot be edited. |
| `BR-019` | Each approval round has one valid Approver, and only that Approver approves or rejects; without an Approver the Meeting cannot be submitted. |
| `BR-020` | Approval moves a WaitingApproval Meeting to Approved; rejection moves it to Rejected and requires a reason. |
| `BR-021` | Only the Member who created a Meeting publishes it, only from Approved, and only when it has exactly one Chair and at least one active Secretary. |
| `BR-022` | Every status change is recorded in the history with the actor, time, action, new status and reason. |
| `BR-023` | The status history is append-only; no entry is edited or deleted. |
| `BR-024` | Only an active Chair or Secretary starts a Meeting, only from Published and only when both a Chair and a Secretary are assigned. |
| `BR-025` | Only the Chair ends a Meeting, only while it is InProgress; ending the Meeting closes its online room. |
| `BR-026` | Ending a Meeting does not close its Votes; ballots already cast are kept. |
| `BR-027` | A Meeting still InProgress 24 hours after its planned end is ended automatically. |
| `BR-028` | Only the Member who created a Meeting cancels it, only before it starts; Cancelled is final and read-only. |
| `BR-029` | A Meeting not started 24 hours after its planned start is cancelled automatically by the system. |
| `BR-030` | Read access by status: a Member reads only Published, InProgress, Ended, or Cancelled after Published; the Chair, the Secretary and the Member who created the Meeting read every status; the Approver reads from submission, never a Draft. |
| `BR-031` | Once a Meeting is published, people are notified when invited, when their role changes or they are removed, and when the Meeting is rescheduled or cancelled. |
| `BR-032` | A failure to send a notification does not undo the action that triggered it. |
| `BR-033` | Each person reads and changes the status of their own notifications only. |
| `BR-034` | Opening a link in a notification checks access again. |
| `BR-035` | Nobody receives an in-app notification about their own action. |
| `BR-036` | Only the current Secretary and the Chair manage the participant list; the Member who created the Meeting has no separate right to it. |
| `BR-037` | The participant list can be edited in Draft, Rejected, Approved, Published and InProgress; in WaitingApproval it is read-only. |
| `BR-038` | An internal participant is an active account and appears at most once in a Meeting. |
| `BR-039` | Each participant has exactly one meeting role, Chair, Secretary or Member, independent of their system role. |
| `BR-040` | A Meeting has at most one Chair; a Published or InProgress Meeting always has a Chair and at least one Secretary. |
| `BR-041` | Before publication, the Secretary assigns or changes the Chair. |
| `BR-042` | From publication, only the current Chair hands the Chair role to an active internal participant who is not a Secretary, and the former Chair becomes a Member; if the Chair is deactivated, the Secretary may replace them. |
| `BR-043` | A role change does not change the invitation response or the attendance; response and attendance are independent of each other. |
| `BR-044` | Removing a participant or revoking a Guest invitation ends the access gained through that relationship; other independent access remains. |
| `BR-045` | The responses and attendance of a removed person are kept as history and do not count towards the current list, access or role counts. |
| `BR-046` | Only the invitee answers an invitation, only Accepted or Declined, once, while the Meeting is Published or InProgress, and cannot change it; the Chair does not answer. |
| `BR-047` | An invitation not yet answered counts as attending. |
| `BR-048` | Declining only hides the Meeting from that person's calendar; it does not change their role or access, including reading documents. |
| `BR-049` | A Guest is a meeting-specific invitation, not an internal participant. |
| `BR-050` | A Guest has no system role and cannot be the Chair or a Secretary. |
| `BR-051` | Within one Meeting an email has at most one Guest invitation, and no Guest invitation is made for the email of an internal participant. |
| `BR-052` | A Guest enters through their own invitation, only into that Meeting, without an internal account. |
| `BR-053` | A Guest must prove control of the invited email address. |
| `BR-054` | Guest access lasts 12 hours from verification without extension, and ends earlier on sign-out, invitation revocation or the end of the Meeting. |
| `BR-055` | While the Meeting is Published or InProgress, a Guest has the rights of a participant, except moderating the room, a personal library and sharing documents. |
| `BR-056` | Only an active Secretary records or corrects official attendance, while the Meeting is InProgress. |
| `BR-057` | Attendance covers internal participants and Guests as Present or Absent; a person not yet recorded stays blank and is not counted as absent. |
| `BR-058` | An agenda item belongs to one Meeting. |
| `BR-059` | The Secretary or the Chair adds, edits, deletes and reorders agenda items from Draft to InProgress; from Ended the agenda is locked. |
| `BR-060` | Only the Chair starts or completes an agenda item. |
| `BR-061` | Any Pending item can be started, but at most one item is InProgress at a time. |
| `BR-062` | An item must be started before it is completed; completing it neither starts another item nor ends the Meeting. |
| `BR-063` | Only an item that has not started (Pending) can be deleted. |
| `BR-064` | A Presenter is a current participant or a Guest of the Meeting. |
| `BR-065` | Removing a person from the Meeting leaves their agenda items without a Presenter. |
| `BR-066` | Each agenda item has at most one conclusion. |
| `BR-067` | The Chair or the Secretary records the conclusion of an InProgress or Done item until the minutes draft is built. |
| `BR-068` | An uploaded file never changes and has no versions; to change it, a new file is uploaded to replace the meeting document. |
| `BR-069` | Only PDF and DOCX files are accepted. |
| `BR-070` | Only the Chair or the Secretary adds or removes meeting documents and links them to agenda items. |
| `BR-071` | Documents are added or removed from the time the Meeting can be viewed until it ends. |
| `BR-072` | A meeting document comes from the adder's personal library or their computer; a file uploaded straight into a Meeting also goes into the personal library. |
| `BR-073` | A file has at most one active attachment in a Meeting. |
| `BR-074` | Removing a document from a Meeting does not delete it from the personal library or other Meetings, and unlinks it from every agenda item. |
| `BR-075` | Comments on a removed document are kept as history; adding the document again starts afresh, without the old comments. |
| `BR-076` | Deleting a document from the personal library does not remove it from a Meeting. |
| `BR-077` | A file no longer used anywhere is purged periodically; its history and comments are kept. |
| `BR-078` | A document links only to agenda items of the same Meeting. |
| `BR-079` | Anyone who can view a Meeting reads its documents, except a document attached only to a draft Vote, which only the Chair and the Secretary see. |
| `BR-080` | After the Meeting ends, its documents and comments are read-only. |
| `BR-081` | A document of a Secret Meeting can be read only online and never downloaded. |
| `BR-082` | Anyone who can view a document can comment on it until the Meeting ends. |
| `BR-083` | A personal library and its folders belong to one person, and only that person accesses them. |
| `BR-084` | A folder cannot be moved into itself or one of its subfolders; only an empty folder can be deleted. |
| `BR-085` | The Admin only reads the library of a deactivated account, and never sees files of a Secret Meeting. |
| `BR-086` | Only the Chair or the Secretary creates, configures, views and deletes draft Votes, while the Meeting is Approved, Published or InProgress; an opened or closed Vote cannot be deleted. |
| `BR-087` | A Vote belongs to the Meeting, not to an agenda item; draft Votes never opened are deleted when the Meeting ends. |
| `BR-088` | The content, options, type and voting roles of an opened or closed Vote cannot be changed. |
| `BR-089` | A Vote has at least two distinct options and is either Public or Secret. |
| `BR-090` | Only the Chair opens a draft Vote, only while the Meeting is InProgress; at most one Vote is Open at a time and it must have eligible voters. |
| `BR-091` | The voter list is fixed at opening from the chosen roles and current access, not from attendance; later role changes do not alter it. |
| `BR-092` | A ballot is cast only while the Meeting is InProgress, the Vote is Open and the voter still has access. |
| `BR-093` | Each voter casts one ballot for one option of the Vote and cannot change or withdraw it; losing access only blocks new ballots. |
| `BR-094` | Vote documents are attached only while the Vote is Draft and are fixed when it opens. |
| `BR-095` | A voter must confirm reading every Vote document before casting a ballot. |
| `BR-096` | A Secret Vote never links a voter to an option, read confirmations included; only totals are published. |
| `BR-097` | After a Public Vote closes, each voter's choice is shown. |
| `BR-098` | Only the Chair closes and tallies a Vote, while the Meeting is InProgress or Ended; percentages are based on the ballots cast. |
| `BR-099` | The Chair records Passed, NotPassed or Deferred for a closed Vote and cannot change it afterwards; the system never derives it from the result. |
| `BR-100` | Only assigned participants and valid Guests join the meeting room; having only created or approved the Meeting is not enough. |
| `BR-101` | Leaving the room or losing the connection does not change the Meeting, list, roles, attendance or Votes; a room connection is not attendance. |
| `BR-102` | Only the Chair mutes another person's microphone; that person may unmute again. |
| `BR-103` | Chat messages in the room are not stored. |
| `BR-104` | Each person has at most one speaking request. |
| `BR-105` | Only the Chair orders, invites, declines and closes speaking turns. |
| `BR-106` | The speaking history stays with the Meeting and is not a task. |
| `BR-107` | The recording method follows the meeting mode and is not chosen by the user; the room is recorded only while InProgress, and audio files only for in-person Meetings. |
| `BR-108` | Only the Secretary records the room, uploads audio files, deletes takes or deletes recordings. |
| `BR-109` | Before recording, participants must be informed; the Secretary confirms this and the system stores the time and purpose. |
| `BR-110` | Recordings, transcripts and recorded sentences are deleted automatically 180 days after the first take; later takes do not extend this, and deleting a recording deletes its audio and transcript. |
| `BR-111` | The Chair, the Secretary and the participants can listen to the recordings. |
| `BR-112` | Only the Chair or the Secretary downloads a recording or exports a transcript, never for a Secret Meeting; a Guest only reviews their own microphone sentences. |
| `BR-113` | An online Meeting is recorded only while a microphone is on; each person withdraws their own sentences, and the Secretary deletes any sentence. |
| `BR-114` | The Chair or the Secretary excludes a sentence from the record with a reason, without deleting it; only they read excluded sentences. |
| `BR-115` | Only the Secretary edits the text of the transcript. |
| `BR-116` | Voice ID needs its own consent, separate from recording consent; a changed notice requires consent again. |
| `BR-117` | Withdrawing consent or deactivating the account deletes all voice samples, suggestions and names filled in by the system; names the Secretary assigned stay. |
| `BR-118` | Each person has two voice sample sets under one consent, up to 5 self-recorded and up to 5 collected on their behalf; only the owner plays the self-recorded samples. |
| `BR-119` | A holder of the collection right records, plays and deletes collected samples, only of people who consented, and asks the person at each collection. |
| `BR-120` | Speaker suggestions compare only consenting people on the current list, are shown only to the Secretary, never suggest a Guest, and are assigned only to people on the list. |
| `BR-121` | Audio and voice samples are processed inside the system and never sent to an outside service. |
| `BR-122` | The combined recording is only for listening and downloading, and is never transcribed. |
| `BR-123` | The system fills in a name when a voice clearly matches a consenting person on the current list. |
| `BR-124` | Only people who consented to notice version 1.2 or later have names filled in automatically, others are only suggested; collection on someone's behalf needs version 1.4. |
| `BR-125` | When transcription runs again, hand-assigned names, corrected text and moved sentences carry over to the matching sentence or voice. |
| `BR-126` | Only the Chair or the Secretary requests, edits and reviews the AI draft, only when the Meeting is Ended and has at least one recorded sentence. |
| `BR-127` | Other people read the AI draft only after it has been reviewed. |
| `BR-128` | The AI writes only from the meeting record; it does not write attendance, guess names or write vote results. |
| `BR-129` | The AI draft is written only by the team's own language model inside the system; no transcript is sent to an outside AI service. |
| `BR-130` | Minutes follow one fixed template (Form 1.4, Decree 30/2020/ND-CP); templates are not managed. |
| `BR-131` | Only the Chair or the Secretary reads and edits the minutes draft, only after the Meeting is Ended; other people see only issued minutes. |
| `BR-132` | Minutes of a Secret Meeting cannot be downloaded as Word or PDF. |
| `BR-133` | The system does not track assignees, due dates or task progress; the Tasks section is plain text. |
| `BR-134` | Only the Secretary writes meeting notes, from InProgress until the minutes draft is built; the Chair can read them. |
| `BR-135` | Only the Secretary submits the minutes for approval, only after the Meeting is Ended. |
| `BR-136` | Only the Chair approves or returns the minutes; returning them requires a reason. |
| `BR-137` | Approval issues the minutes immediately; the Meeting stays Ended. |
| `BR-138` | Issued minutes never change; a correction creates a revision from the issued version, with at most one revision not yet issued. |
| `BR-139` | Issued and superseded minutes are read and downloaded by everyone who can read the Meeting except Guests; drafts only by the Chair and the Secretary. |
| `BR-140` | Calendar sharing has two levels: level 1 covers Normal Meetings, level 2 Normal and Internal; Secret Meetings are never shared. |
| `BR-141` | The viewer sees only busy, free, unknown or not shared, never the title, location, attendees or sensitivity; only users who may create Meetings use calendar sharing. |
| `BR-142` | Each pair of calendar owner and viewer has at most one Pending or Effective share. |
| `BR-143` | A share stops when the viewer is no longer allowed to create Meetings or is deactivated, and expires after its end date. |
| `BR-144` | Days outside the shared range are unknown; a person who has not shared counts as neither free nor busy. |
| `BR-145` | A Meeting above the shared level counts as free; the searcher's own Meetings always count. |
| `BR-146` | Only a Chair, Secretary or Member assignment in a WaitingApproval, Approved, Published, InProgress or Ended Meeting counts as busy. |
| `BR-147` | A person who declined, and Draft, Rejected or Cancelled Meetings, do not count as busy. |
| `BR-148` | A slot that touches a Secret Meeting can still create a Draft, without a special warning. |
| `BR-149` | Internal users view and export reports; Guests do not. |
| `BR-150` | A report counts only Meetings the user can read, by month of planned start, current status and current sensitivity. |
| `BR-151` | Attendance rate, time to minutes, meeting duration and recorded hours follow their formulas; combined recordings add no hours; Meetings missing the data are left out of the averages. |
| `BR-152` | Header figures show numbers only; query tables show a person's name or a Meeting's code and title, only for Meetings the viewer reads; a Guest shows only a name. |
| `BR-153` | The report query counts only records the viewer can read: draft Votes and unissued minutes for the Chair and Secretary, recordings for participants. |
| `BR-154` | When transcription runs again, what cannot be matched leaves only a trace in the audit log, without the transcript text. |

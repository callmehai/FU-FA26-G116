# Meeting.Transition — Unit (FE-04, MEET)

Description: Meeting workflow transitions: submit, approve, reject, publish, cancel and organizer transfer with guards.

Precondition: Meeting in the required start status with a valid roster; actor with the required capability.

## MeetingService.TransitionAsync

Layer: Application · `backend/TrustCabinet.Application/Meetings/MeetingService.cs:420`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Meeting is Draft; organizer holds MeetingCreator.<br>2. Persisted meeting information passes the submission readiness check.<br>3. Approver is active, holds MeetingApprover and differs from the organizer. | Meeting status: Draft; actor: organizer; command: SubmitForApproval with AssignedApproverId of an eligible approver. | Return: Success(WaitingApproval); AssignedApproverId set; MSG78.<br>Exception: N/A<br>Log Message: Saved: status WaitingApproval; history row SubmitForApproval with the approver; notification "Cuộc họp cần được duyệt" to the approver. | Normal | UC-18 · BR-33 · BR-34 · BR-35 · MSG78 |
| UTC002 | Draft meeting with an empty stored title. | actor: organizer; command: SubmitForApproval with a valid approver. | Return: Failure SubmissionNotReady with errors { title }; MSG81; status stays Draft.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-18 · BR-33 · MSG81 |
| UTC003 | 1. Meeting is WaitingApproval with the actor as assigned approver.<br>2. Actor holds MeetingApprover and is not the organizer. | Meeting status: WaitingApproval; actor: assigned approver; command: Approve. | Return: Success(Approved); MSG43.<br>Exception: N/A<br>Log Message: Saved: status Approved; history row Approve; organizer notified "Cuộc họp đã được duyệt". | Normal | UC-19 · BR-36 · BR-38 · MSG43 |
| UTC004 | 1. Meeting is Approved; organizer holds MeetingCreator.<br>2. Roster has exactly one active Chair, one active Secretary and two Members. | command: Publish; actor: organizer. | Return: Success(Published); MSG45.<br>Exception: N/A<br>Log Message: Saved: status Published; history row Publish; "Cuộc họp đã được công bố" notification for the Chair, Secretary and Members but not the organizer; invitation emails staged for every active participant. | Normal | UC-20 · BR-37 · BR-39 · BR-124 · BR-125 · BR-322 · MSG45 |
| UTC005 | Meeting is Published; organizer holds MeetingCreator. | command: Cancel with reason "  Hoãn do bão  ". | Return: Success(Cancelled); MSG261; reason trimmed to "Hoãn do bão".<br>Exception: N/A<br>Log Message: Saved: status Cancelled with CancelledAt, CancelledById, reason and CancelledFromStatus Published; history row Cancel with the reason; vote drafts deleted in the same transaction (ADR-0067); invitees (and guests) are notified with the reason by the staged cancellation messages. | Normal | UC-75 · BR-297 · BR-298 · BR-299 · BR-300 · MSG261 |

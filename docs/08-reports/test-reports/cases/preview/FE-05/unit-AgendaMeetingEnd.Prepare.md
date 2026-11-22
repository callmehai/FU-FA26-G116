# AgendaMeetingEnd.Prepare — Unit (FE-05, AGENDA)

Description: Ending a meeting settles the agenda: running items complete.

Precondition: Meeting with a running agenda item.

## AgendaMeetingEnd.PrepareAsync

Layer: Application · `backend/TrustCabinet.Application/Agenda/AgendaMeetingEnd.cs:10`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Meeting being ended inside the caller's meeting transaction.<br>2. An agenda item is InProgress, started 2026-12-01T09:00:00Z. | meetingId; endedAt: 2026-12-01T10:30:00Z. | Return: AgendaChange {ChangeType: "MeetingEnded"; AgendaItemId: that item; Title: the item title; OccurredAt: 10:30:00Z; new EventId}.<br>Exception: N/A<br>Log Message: Item Status Done, ActualEndedAt 10:30:00Z; meeting row locked first; change to be published after the caller commits. | Normal | UC-22 · UC-25 · BR-72 |
| UTC002 | Meeting has items that are all Pending or Done; none InProgress. | meetingId; endedAt: now. | Return: null; nothing changes.<br>Exception: N/A<br>Log Message: No item is modified. | Boundary | UC-22 · BR-72 |
| UTC003 | An item is InProgress, started 2026-12-01T09:00:00Z. | endedAt: 2026-12-01T08:00:00Z (before the start). | Return: No value is returned.<br>Exception: InvalidAgendaExecutionException: The current agenda item has no valid start time or the end precedes its start.<br>Log Message: Item stays InProgress; the caller's transaction is expected to roll back. | Abnormal | UC-22 · BR-72 |

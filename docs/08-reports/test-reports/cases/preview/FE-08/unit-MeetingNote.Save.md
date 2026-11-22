# MeetingNote.Save — Unit (FE-08, ROOM)

Description: Saving a personal meeting note: length and meeting status limits.

Precondition: Meeting in a note-accepting status.

## MeetingNoteService.SaveAsync

Layer: Application · `backend/TrustCabinet.Application/Notes/MeetingNoteService.cs:46`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Meeting InProgress; actor is the current Secretary.<br>2. The actor never saved a note at this place; no note exists there. | meetingId: the meeting; actorId: the Secretary; agendaItemId: null (the meeting as a whole); body: "Ghi chú buổi họp"; loadedAt: null. | Return: Success with the saved note ("Đã lưu ghi chú." MSG415); a change notice is published so the other open windows refetch.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-82 · MSG415 |
| UTC002 | 1. Meeting InProgress; actor is the current Secretary.<br>2. The actor never saved a note at this place. | meetingId: the meeting; actorId: the Secretary; agendaItemId: null; body: exactly 20.000 characters; loadedAt: null. | Return: Success; a note of exactly the maximum length is kept whole (MSG415).<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-82 · BR-400 · MSG415 |
| UTC003 | Meeting InProgress; actor is the current Chair (a reader of the meeting, not its Secretary). | meetingId: the meeting; actorId: the Chair; agendaItemId: null; body: "Ghi chú"; loadedAt: null. | Return: Forbidden; 403 "Chỉ Thư ký được ghi chú cuộc họp." (MSG416); the panel keeps the typed text to copy and nothing about the notes is disclosed.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-82 · BR-395 · MSG416 |
| UTC004 | Meeting Published (not started yet); actor is the current Secretary. | meetingId: the meeting; actorId: the Secretary; agendaItemId: null; body: "Ghi chú"; loadedAt: null. | Return: Closed; 409 "Chỉ ghi chú khi cuộc họp đang diễn ra, hoặc sau khi kết thúc mà chưa tạo bản nháp biên bản." (MSG417).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-82 · BR-396 · MSG417 |
| UTC005 | Meeting InProgress; actor is the current Secretary. | meetingId: the meeting; actorId: the Secretary; agendaItemId: null; body: 20.001 characters; loadedAt: null. | Return: TooLong; 400 "Ghi chú quá dài, tối đa 20.000 ký tự." (MSG419); nothing is saved.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-82 · BR-400 · MSG419 |
| UTC006 | 1. Meeting InProgress; actor is the current Secretary.<br>2. The note was saved from another window after the actor read it. | meetingId: the meeting; actorId: the Secretary; agendaItemId: the item; body: "Ghi chú mới"; loadedAt: the older updatedAt. | Return: Stale; 409 "Ghi chú vừa được sửa ở cửa sổ khác. Tải lại để xem bản mới nhất." (MSG418); nothing is overwritten.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-82 · BR-399 · MSG418 |

# AgendaPresenterNotifier.Notify — Unit (FE-05, AGENDA)

Description: Presenter notification when an item is assigned or changed.

Precondition: Agenda item with a presenter.

## AgendaPresenterNotifier.NotifyAsync

Layer: Application · `backend/TrustCabinet.Application/Agenda/AgendaPresenterNotifier.cs:15`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Meeting M (title "Budget", planned 2026-12-01T02:00:00Z) is Published.<br>2. Agenda item "Q3 review" has presenter P; P is an active current participant; no notification with the key exists. | assignment {EventId: E1; MeetingId: M; AgendaItemId: item; PresenterId: P; Guest: null; OccurredAt: T}. | Return: Completes; one in-app notification is saved.<br>Exception: N/A<br>Log Message: Notification for P: type AgendaPresenterAssigned; title "Phân công trình bày mục nghị sự"; body names item "Q3 review", meeting "Budget" and "09:00 01/12/2026" (Ho Chi Minh time); target Meeting M; deduplication key "agenda-presenter:E1:P"; created at T. | Normal | UC-23a · BR-66 |
| UTC002 | Same assignment as before; a notification with key "agenda-presenter:E1:P" already exists. | the same assignment delivered a second time. | Return: Completes; nothing is added (idempotent).<br>Exception: N/A<br>Log Message: No new notification. | Boundary | UC-23a |
| UTC003 | Presenter P was removed from the roster after the change. | assignment for P. | Return: Completes; nothing is added.<br>Exception: N/A<br>Log Message: No notification. | Abnormal | UC-28c |
| UTC004 | 1. Item presenter is Guest invitation G (current, email "khach@example.com", name "Tran Khach"); organizer "Le Chu Tri".<br>2. Guest link provider is available and returns a ready link. | assignment {PresenterGuestInvitationId: G}. | Return: Completes; one email is queued.<br>Exception: N/A<br>Log Message: Email to khach@example.com: subject "Phân công trình bày mục nghị sự"; text body greets "Tran Khach", gives the item, meeting and time, the verification link line and "Người tổ chức: Le Chu Tri"; HTML body from the guest-presenter template; deduplication key "agenda-presenter:E1:G". | Normal | UC-23a · BR-91 |

# Agenda.Validate — Unit (FE-05, AGENDA)

Description: Agenda item validation: title, description, presenter and document links.

Precondition: Meeting exists; presenter is a participant of the meeting.

## AgendaRules.Validate

Layer: Domain · `backend/TrustCabinet.Domain/Agenda/AgendaRules.cs:32`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Title and description in range, no presenter, no documents. | input {Title: "Budget review"; Description: "Q3"; PresenterId: null; PresenterGuestInvitationId: null; DocumentIds: null}. | Return: No errors.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-23a · BR-66 |
| UTC002 | Title is empty. | input {Title: ""}. | Return: Errors: title only.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-23a · BR-66 · MSG66 |
| UTC003 | Title is 301 characters. | input {Title: 301 characters}. | Return: Errors: title only.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-23a · BR-66 · MSG66 |
| UTC004 | Title is one character. | input {Title: "A"}. | Return: No errors.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-23a · BR-66 |

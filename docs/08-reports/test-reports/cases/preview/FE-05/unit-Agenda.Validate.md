# Agenda.Validate — Unit (FE-05, AGENDA)

Description: Agenda item validation: title, description, presenter and document links.

Precondition: Meeting exists; presenter is a participant of the meeting.

## AgendaRules.Validate

Layer: Domain · `backend/TrustCabinet.Domain/Agenda/AgendaRules.cs:32`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Title and description in range, no presenter, no documents. | input {Title: "Budget review"; Description: "Q3"; PresenterId: null; PresenterGuestInvitationId: null; DocumentIds: null}. | Return: No errors.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-23a · BR-66 |
| UTC002 | Title is empty. | input {Title: ""}. | Return: Errors: title only.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-23a · BR-66 · MSG66 |
| UTC003 | Title is null. | input {Title: null}. | Return: Errors: title only.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-23a · BR-66 · MSG66 |
| UTC004 | Title is exactly 300 characters. | input {Title: 300 characters}. | Return: No errors.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-23a · BR-66 |
| UTC005 | Title is 301 characters. | input {Title: 301 characters}. | Return: Errors: title only.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-23a · BR-66 · MSG66 |
| UTC006 | Title is one character. | input {Title: "A"}. | Return: No errors.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-23a · BR-66 |
| UTC007 | Description is exactly 4000 characters. | input {Title: "A"; Description: 4000 characters}. | Return: No errors.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-23a · BR-66 |
| UTC008 | Description is 4001 characters. | input {Title: "A"; Description: 4001 characters}. | Return: Errors: description only.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-23a · BR-66 · MSG66 |
| UTC009 | Description is not given. | input {Title: "A"; Description: null}. | Return: No errors.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-23a · BR-66 |
| UTC010 | Internal presenter only. | input {Title: "A"; PresenterId: a user id}. | Return: No errors.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-23a · BR-66 |
| UTC011 | Guest presenter only. | input {Title: "A"; PresenterGuestInvitationId: an invitation id}. | Return: No errors.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-23a · BR-66 |
| UTC012 | Both an internal and a Guest presenter are given. | input {Title: "A"; PresenterId: a user id; PresenterGuestInvitationId: an invitation id}. | Return: Errors: presenterId only.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-23a · BR-66 · MSG66 |
| UTC013 | Presenter id is the empty GUID. | input {Title: "A"; PresenterId: 00000000-0000-0000-0000-000000000000}. | Return: Errors: presenterId only.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-23a · BR-66 · MSG66 |
| UTC014 | Guest invitation id is the empty GUID. | input {Title: "A"; PresenterGuestInvitationId: 00000000-0000-0000-0000-000000000000}. | Return: Errors: presenterId only.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-23a · BR-66 · MSG66 |
| UTC015 | Document list has two different ids. | input {Title: "A"; DocumentIds: [D1, D2]}. | Return: No errors.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-23a · BR-278 |
| UTC016 | Document list is empty. | input {Title: "A"; DocumentIds: []}. | Return: No errors.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-23a · BR-278 |
| UTC017 | Document list has the same id twice. | input {Title: "A"; DocumentIds: [D1, D1]}. | Return: Errors: documentIds only.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-23a · BR-278 · MSG66 |
| UTC018 | Document list contains the empty GUID. | input {Title: "A"; DocumentIds: [00000000-0000-0000-0000-000000000000]}. | Return: Errors: documentIds only.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-23a · BR-278 · MSG66 |
| UTC019 | Several fields are wrong at once. | input {Title: ""; Description: 4001 characters; PresenterId: empty GUID; DocumentIds: [D1, D1]}. | Return: Errors for title, description, presenterId and documentIds together.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-23a · BR-66 · MSG66 |

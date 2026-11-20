# Agenda.HasRole — Unit (FE-05, AGENDA)

Description: Role rule: whether the actor holds a role as a current participant.

Precondition: No database needed; pure rules.

## AgendaRules.HasRole

Layer: Domain · `backend/TrustCabinet.Domain/Agenda/AgendaRules.cs:18`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Actor is the active current Chair. | meeting; actor; role: Chair; participant {MeetingId: meeting; UserId: actor; Chair; Current; active}. | Return: true.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-24 · BR-69 |
| UTC002 | Actor holds a different role. | role asked: Chair; actor's participant role: Secretary. | Return: false.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-24 · BR-69 |
| UTC003 | Actor's account is deactivated. | role: Chair; participant Chair with User.IsActive: false. | Return: false.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-24 · BR-69 |
| UTC004 | Participant row belongs to another meeting. | participant {MeetingId: other meeting; UserId: actor; Chair}. | Return: false.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-24 · BR-64 |
| UTC005 | Actor not on the roster. | no participant with UserId: actor. | Return: false.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-24 · BR-69 |

# Agenda.CurrentParticipant — Unit (FE-05, AGENDA)

Description: Current-participant rule: roster membership that is still current.

Precondition: No database needed; pure rules.

## AgendaRules.CurrentParticipant

Layer: Domain · `backend/TrustCabinet.Domain/Agenda/AgendaRules.cs:16`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Current participant with an active account. | participant {Current; Member; User.IsActive: true}. | Return: true.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-23a · BR-66 |
| UTC002 | Participant account deactivated. | participant {Current; Member; User.IsActive: false}. | Return: false.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-23a · BR-66 |
| UTC003 | Participant removed from the roster. | participant {AssignmentStatus: Removed; User.IsActive: true}. | Return: false.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-28c · BR-66 |
| UTC004 | Guest-role row. | participant {Role: Guest; Current; active}. | Return: false.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-23a · BR-150 |

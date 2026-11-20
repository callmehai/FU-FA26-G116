# Recording.Export — Unit (FE-09, REC)

Description: Recording rules: upload allowed, who records and listens, export and validation.

Precondition: No database needed; pure rules.

## RecordingRules.Export

Layer: Application · `backend/TrustCabinet.Application/Recordings/RecordingContracts.cs:247`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | The actor is the Chair of a Normal meeting. | actor: the Chair. | Return: None; export allowed.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-43c · UC-45b · BR-351 |
| UTC002 | The actor is the Secretary of an Internal meeting. | actor: the Secretary. | Return: None.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-43c · UC-45b · BR-351 |
| UTC003 | The actor is a Member of a Normal meeting. | actor: a Member. | Return: ExportForbidden (shown as MSG355).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-43c · UC-45b · BR-351 · MSG355 |
| UTC004 | The actor is not on the roster. | actor: an outsider. | Return: ExportForbidden.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-43c · BR-351 · MSG355 |
| UTC005 | The actor is the Chair of a Secret meeting. | actor: the Chair. | Return: SecretMeeting (shown as MSG356).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-43c · UC-45b · BR-351 · MSG356 |
| UTC006 | The actor is the Secretary of a Secret meeting. | actor: the Secretary. | Return: SecretMeeting.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-43c · BR-351 · MSG356 |
| UTC007 | The actor is a Member of a Secret meeting. | actor: a Member. | Return: ExportForbidden; the role check comes before the Secret check.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-43c · BR-351 · MSG355 |
| UTC008 | The actor's Chair assignment was replaced. | actor: the former Chair. | Return: ExportForbidden.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-43c · BR-351 · MSG355 |

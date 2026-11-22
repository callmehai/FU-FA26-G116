# Agenda.Actions — Unit (FE-05, AGENDA)

Description: Agenda actions rule: the operations a viewer may take on an item.

Precondition: No database needed; pure rules.

## AgendaRules.Actions

Layer: Domain · `backend/TrustCabinet.Domain/Agenda/AgendaRules.cs:57`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Running meeting; Chair; item Pending; no item in progress. | meeting InProgress; actor: Chair; item Pending; items: [item Pending, another Pending]; hasMinutes: false. | Return: CanEdit true; CanEditPlanningFields true; CanDelete true; CanSetCurrent true; CanComplete false; CanRecordConclusion false; CanLinkDocuments true.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-23a · UC-24 · BR-65 · BR-69 · BR-70 |
| UTC002 | Running meeting; Secretary; item Pending. | meeting InProgress; actor: Secretary; item Pending. | Return: CanEdit true; CanDelete true; CanSetCurrent false; CanComplete false; CanLinkDocuments true (execution actions are for the Chair only).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-24 · BR-65 · BR-69 |
| UTC003 | Plain Member views the agenda. | meeting InProgress; actor: Member; item Pending. | Return: All seven actions false.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-26 · BR-65 |
| UTC004 | Ended meeting with minutes issued. | meeting Ended; actor: Secretary; item Done; hasMinutes: true. | Return: CanRecordConclusion false; all others false.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-77 · MSG273 |

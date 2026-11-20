# Agenda.Actions — Unit (FE-05, AGENDA)

Description: Agenda actions rule: the operations a viewer may take on an item.

Precondition: No database needed; pure rules.

## AgendaRules.Actions

Layer: Domain · `backend/TrustCabinet.Domain/Agenda/AgendaRules.cs:57`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Running meeting; Chair; item Pending; no item in progress. | meeting InProgress; actor: Chair; item Pending; items: [item Pending, another Pending]; hasMinutes: false. | Return: CanEdit true; CanEditPlanningFields true; CanDelete true; CanSetCurrent true; CanComplete false; CanRecordConclusion false; CanLinkDocuments true.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-23a · UC-24 · BR-65 · BR-69 · BR-70 |
| UTC002 | Running meeting; Chair; another item is already in progress. | meeting InProgress; actor: Chair; item Pending; items include one InProgress. | Return: CanSetCurrent false; CanEdit true; CanDelete true.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-24 · BR-71 |
| UTC003 | Running meeting; Chair; the item is in progress. | meeting InProgress; actor: Chair; item InProgress. | Return: CanComplete true; CanDelete false; CanSetCurrent false; CanRecordConclusion true; CanEdit true.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-25 · UC-77 · BR-69 |
| UTC004 | Running meeting; Secretary; item Pending. | meeting InProgress; actor: Secretary; item Pending. | Return: CanEdit true; CanDelete true; CanSetCurrent false; CanComplete false; CanLinkDocuments true (execution actions are for the Chair only).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-24 · BR-65 · BR-69 |
| UTC005 | Published meeting; Chair; item Pending. | meeting Published; actor: Chair; item Pending. | Return: CanEdit true; CanDelete true; CanSetCurrent false; CanComplete false (meeting not running).<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-23a · BR-69 |
| UTC006 | Plain Member views the agenda. | meeting InProgress; actor: Member; item Pending. | Return: All seven actions false.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-26 · BR-65 |
| UTC007 | Ended meeting; Chair; item Done; no minutes. | meeting Ended; actor: Chair; item Done; hasMinutes: false. | Return: CanEdit false; CanDelete false; CanSetCurrent false; CanComplete false; CanRecordConclusion true; CanLinkDocuments false (meeting ended).<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-77 · BR-74 · BR-279 |
| UTC008 | Ended meeting with minutes issued. | meeting Ended; actor: Secretary; item Done; hasMinutes: true. | Return: CanRecordConclusion false; all others false.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-77 · MSG273 |

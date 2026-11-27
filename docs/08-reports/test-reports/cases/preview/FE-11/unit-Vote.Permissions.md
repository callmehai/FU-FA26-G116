# Vote.Permissions — Unit (FE-11, VOTE)

Description: Voting permissions: preparation and closing statuses, chair and manager, draft management, viewer actions.

Precondition: No database needed; pure rules.

## VoteRules.Permissions

Layer: Domain · `backend/TrustCabinet.Domain/Votes/VoteRules.cs:32`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Meeting Ended; viewer is the Chair. | meeting: Ended; viewer: Chair. | Return: CanManageDraft false, CanOpen false, CanClose true, CanRecordConclusion true.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-66 · UC-67 · BR-098 |
| UTC002 | Meeting InProgress; viewer is the Secretary. | meeting: InProgress; viewer: Secretary. | Return: CanManageDraft true; CanOpen, CanClose and CanRecordConclusion false.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-63 · BR-086, BR-087 |
| UTC003 | Meeting Published; viewer is the Chair. | meeting: Published; viewer: Chair. | Return: CanManageDraft true; CanOpen false (not started); CanClose false; CanRecordConclusion false.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-63 · BR-086, BR-087 |
| UTC004 | Meeting InProgress; viewer is a Member. | meeting: InProgress; viewer: Member. | Return: All four permissions false.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-62a |

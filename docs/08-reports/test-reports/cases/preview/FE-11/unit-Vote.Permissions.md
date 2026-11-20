# Vote.Permissions — Unit (FE-11, VOTE)

Description: Voting permissions: preparation and closing statuses, chair and manager, draft management, viewer actions.

Precondition: No database needed; pure rules.

## VoteRules.Permissions

Layer: Domain · `backend/TrustCabinet.Domain/Votes/VoteRules.cs:32`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Meeting InProgress; viewer is the Chair. | meeting: InProgress; viewer: Chair. | Return: CanManageDraft true, CanOpen true, CanClose true, CanRecordConclusion true.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-52a · UC-54 · UC-56 · UC-57 |
| UTC002 | Meeting Ended; viewer is the Chair. | meeting: Ended; viewer: Chair. | Return: CanManageDraft false, CanOpen false, CanClose true, CanRecordConclusion true.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-56 · UC-57 · BR-270 |
| UTC003 | Meeting InProgress; viewer is the Secretary. | meeting: InProgress; viewer: Secretary. | Return: CanManageDraft true; CanOpen, CanClose and CanRecordConclusion false.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-53 · BR-252 |
| UTC004 | Meeting Published; viewer is the Chair. | meeting: Published; viewer: Chair. | Return: CanManageDraft true; CanOpen false (not started); CanClose false; CanRecordConclusion false.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-53 · BR-252 |
| UTC005 | Meeting InProgress; viewer is a Member. | meeting: InProgress; viewer: Member. | Return: All four permissions false.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-52a |

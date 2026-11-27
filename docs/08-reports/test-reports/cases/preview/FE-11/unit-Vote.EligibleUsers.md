# Vote.EligibleUsers — Unit (FE-11, VOTE)

Description: Eligible voter freeze: who may vote is fixed when the vote opens.

Precondition: Meeting with participants and roles; selected eligible roles.

## VoteRules.EligibleUsers

Layer: Domain · `backend/TrustCabinet.Domain/Votes/VoteRules.cs:91`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Meeting with an active Chair and an active Member who may read it. | Meeting with active Chair and active Member; roles: [Chair, Member]. | Return: Both user ids, distinct and ordered.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-64 · BR-091 |
| UTC002 | Meeting with an active Chair and a deactivated Member. | Meeting with an active Chair and a deactivated Member; roles: [Chair, Member]. | Return: Only the Chair id.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-64 · BR-091 |
| UTC003 | Meeting with no Guest participants. | Meeting with an active Chair; roles: [Guest]. | Return: Empty list; the vote cannot open (MSG220).<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-64 · BR-091 |

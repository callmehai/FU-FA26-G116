# Vote.EligibleUsers — Unit (FE-11, VOTE)

Description: Eligible voter freeze: who may vote is fixed when the vote opens.

Precondition: Meeting with participants and roles; selected eligible roles.

## VoteRules.EligibleUsers

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Meeting with an active Chair and an active Member who may read it. | Meeting with active Chair and active Member; roles: [Chair, Member]. | Return: Both user ids, distinct and ordered.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-54 · BR-190 Automated: tests/TrustCabinet.Web.Tests/Votes/VoteRulesTests.cs (Eligible_users_are_the_active_readable_holders_of_the_selected_roles) |
| UTC002 | Meeting with an active Member who may read it and has no attendance row. | Meeting with an active Member who has not marked attendance; roles: [Member]. | Return: The Member id; attendance plays no part.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-54 · BR-190 Automated: tests/TrustCabinet.Web.Tests/Votes/VoteRulesTests.cs (Attendance_plays_no_part_in_eligibility) |
| UTC003 | Meeting with an active Chair and a deactivated Member. | Meeting with an active Chair and a deactivated Member; roles: [Chair, Member]. | Return: Only the Chair id.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-54 · BR-190 Automated: tests/TrustCabinet.Web.Tests/Votes/VoteRulesTests.cs (A_deactivated_holder_is_excluded) |
| UTC004 | Meeting with no Guest participants. | Meeting with an active Chair; roles: [Guest]. | Return: Empty list; the vote cannot open (MSG220).<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-54 · BR-190 Automated: tests/TrustCabinet.Web.Tests/Votes/VoteRulesTests.cs (Nobody_eligible_means_an_empty_list) |

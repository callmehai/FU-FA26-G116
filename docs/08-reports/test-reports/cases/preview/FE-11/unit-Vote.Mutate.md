# Vote.Mutate — Unit (FE-11, VOTE)

Description: Voting commands: create, edit, delete draft, open, cast, close, conclude.

Precondition: Meeting in a voting status; chair or manager actor.

## VoteService.MutateAsync

Layer: Application · `backend/TrustCabinet.Application/Votes/VoteService.cs:104`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Meeting Approved; actor is the current Secretary. | operation: Create; Title: "Chọn địa điểm họp"; Options: ["Hà Nội", "Đà Nẵng"]; IsAnonymous: false; EligibleRoles: ["Chair", "Member"]; OpenImmediately: false. | Return: Success; VoteId of the new Draft vote; options saved at positions 1 and 2; MSG210.<br>Exception: N/A<br>Log Message: Draft vote row with creator = actor; DraftCreated event published to Chair and Secretary only. | Normal | UC-63 · BR-086, BR-087 · BR-089 · MSG210 |
| UTC002 | Meeting InProgress; Open vote with options A and B; voter is on the eligible list; public ballot; no vote documents. | operation: Cast; OptionId: option A. | Return: Success; MSG227; one receipt and one choice stored for the voter; option A count +1.<br>Exception: N/A<br>Log Message: Ballot saved; BallotCast event published (never names the voter). | Normal | UC-65 · BR-092, BR-093 · MSG227 |
| UTC003 | Meeting InProgress; Open vote; actor was not on the list fixed at opening (joined later). | operation: Cast; OptionId: option A. | Return: Failure NotEligible; 409 MSG224.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-65 · BR-091 · MSG224 |
| UTC004 | Meeting InProgress; Open vote with 3 ballots; actor is the Chair. | operation: Close. | Return: Success; MSG229; status Closed, ClosedAt = now; the result (ballot count and per-option counts and percentages) is returned.<br>Exception: N/A<br>Log Message: Vote closed; Closed event published. | Normal | UC-66 · BR-098 · MSG229 |
| UTC005 | Closed vote without a conclusion; actor is the Chair; meeting Ended. | operation: Conclude (RecordConclusion); Conclusion: "Passed"; Note: "  Thống nhất  ". | Return: Success; MSG235; conclusion Passed, note "Thống nhất", concluded by the Chair at now.<br>Exception: N/A<br>Log Message: ConclusionRecorded event published. | Normal | UC-67 · BR-099 · MSG235 |

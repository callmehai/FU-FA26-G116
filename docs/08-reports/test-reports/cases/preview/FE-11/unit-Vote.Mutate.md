# Vote.Mutate — Unit (FE-11, VOTE)

Description: Voting commands: create, edit, delete draft, open, cast, close, conclude.

Precondition: Meeting in a voting status; chair or manager actor.

## VoteService.MutateAsync

Layer: Application · `backend/TrustCabinet.Application/Votes/VoteService.cs:104`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Meeting Approved; actor is the current Secretary. | operation: Create; Title: "Chọn địa điểm họp"; Options: ["Hà Nội", "Đà Nẵng"]; IsAnonymous: false; EligibleRoles: ["Chair", "Member"]; OpenImmediately: false. | Return: Success; VoteId of the new Draft vote; options saved at positions 1 and 2; MSG210.<br>Exception: N/A<br>Log Message: Draft vote row with creator = actor; DraftCreated event published to Chair and Secretary only. | Normal | UC-53 · BR-252 · BR-256 · BR-257 · MSG210 |
| UTC002 | Meeting InProgress; actor is a Member. | operation: Create; Title: "Chọn địa điểm họp"; Options: ["Hà Nội", "Đà Nẵng"]; IsAnonymous: false; EligibleRoles: ["Chair", "Member"]. | Return: Failure DraftDenied; 409 MSG213.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-53 · BR-252 · MSG213 |
| UTC003 | Meeting InProgress; actor is the current Chair; another vote is already Open. | operation: Create; Title: "Chọn địa điểm họp"; Options: ["Hà Nội", "Đà Nẵng"]; IsAnonymous: false; EligibleRoles: ["Chair", "Member"]; OpenImmediately: true. | Return: Failure AnotherOpen; 409 MSG219; nothing saved.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-53 · UC-54 · BR-266 · MSG219 |
| UTC004 | Meeting InProgress; Open vote with options A and B; voter is on the eligible list; public ballot; no vote documents. | operation: Cast; OptionId: option A. | Return: Success; MSG227; one receipt and one choice stored for the voter; option A count +1.<br>Exception: N/A<br>Log Message: Ballot saved; BallotCast event published (never names the voter). | Normal | UC-55 · BR-258 · BR-271 · MSG227 |
| UTC005 | Meeting InProgress; Open vote; actor was not on the list fixed at opening (joined later). | operation: Cast; OptionId: option A. | Return: Failure NotEligible; 409 MSG224.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-55 · BR-190 · BR-268 · MSG224 |
| UTC006 | Meeting InProgress; Open vote with options A and B; voter is on the eligible list; the voter already cast a ballot. | operation: Cast; OptionId: option B. | Return: Failure AlreadyCast; 409 MSG226; the first ballot stands.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-55 · BR-258 · BR-265 · MSG226 |
| UTC007 | Meeting InProgress; Open vote with 3 ballots; actor is the Chair. | operation: Close. | Return: Success; MSG229; status Closed, ClosedAt = now; the result (ballot count and per-option counts and percentages) is returned.<br>Exception: N/A<br>Log Message: Vote closed; Closed event published. | Normal | UC-56 · BR-261 · BR-270 · MSG229 |
| UTC008 | Closed vote without a conclusion; actor is the Chair; meeting Ended. | operation: Conclude (RecordConclusion); Conclusion: "Passed"; Note: "  Thống nhất  ". | Return: Success; MSG235; conclusion Passed, note "Thống nhất", concluded by the Chair at now.<br>Exception: N/A<br>Log Message: ConclusionRecorded event published. | Normal | UC-57 · BR-262 · BR-263 · MSG235 |

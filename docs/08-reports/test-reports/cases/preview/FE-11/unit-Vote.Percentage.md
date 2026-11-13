# Vote.Percentage — Unit (FE-11, VOTE)

Description: Vote percentage display: per-option share with banker's rounding and the zero-ballot case.

Precondition: Ballot count and per-option counts.

## VoteRules.Percentage

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | A closed vote with 3 ballots and 1 ballot for the option. | count: 1; ballots: 3. | Return: 33.33.<br>Exception: N/A<br>Log Message: N/A | Normal | BR-261 Automated: tests/TrustCabinet.Web.Tests/Votes/VoteRulesTests.cs (One_of_three_is_33_33_percent) |
| UTC002 | A vote with no ballots yet. | count: 0; ballots: 0. | Return: null; no percentage is shown.<br>Exception: N/A<br>Log Message: N/A | Boundary | BR-261 Automated: tests/TrustCabinet.Web.Tests/Votes/VoteRulesTests.cs (No_ballot_means_no_percentage) |
| UTC003 | A vote where every ballot chose the option. | count: 3; ballots: 3. | Return: 100.<br>Exception: N/A<br>Log Message: N/A | Boundary | BR-261 Automated: tests/TrustCabinet.Web.Tests/Votes/VoteRulesTests.cs (A_unanimous_vote_is_100_percent) |
| UTC004 | A vote with 6 ballots and 1 ballot for the option. | count: 1; ballots: 6. | Return: 16.67; rounded away from zero to 2 decimals.<br>Exception: N/A<br>Log Message: N/A | Boundary | BR-261 Automated: tests/TrustCabinet.Web.Tests/Votes/VoteRulesTests.cs (Rounding_is_away_from_zero_with_two_decimals) |

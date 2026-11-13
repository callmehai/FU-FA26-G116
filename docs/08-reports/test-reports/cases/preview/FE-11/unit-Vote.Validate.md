# Vote.Validate — Unit (FE-11, VOTE)

Description: Vote draft validation: question, options, eligibility roles and anonymity flag.

Precondition: Draft input with title, options and eligible roles.

## VoteRules.Validate

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Draft input with two distinct options and at least one eligible role. | Title: "Chọn địa điểm họp"; Description: ""; Options: ["Hà Nội", "Đà Nẵng"]; IsAnonymous: false; EligibleRoles: ["Chair", "Member"]. | Return: No errors; roles parsed as [Chair, Member].<br>Exception: N/A<br>Log Message: N/A | Normal | UC-53 · MSG209 Automated: tests/TrustCabinet.Web.Tests/Votes/VoteRulesTests.cs (A_valid_draft_passes_with_its_roles_parsed) |
| UTC002 | Draft input with a blank title. | Title: ""; Description: ""; Options: ["Hà Nội", "Đà Nẵng"]; IsAnonymous: false; EligibleRoles: ["Member"]. | Return: errors[title] = ["Nội dung biểu quyết phải có từ 1 đến 300 ký tự."].<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-53 · MSG209 Automated: tests/TrustCabinet.Web.Tests/Votes/VoteRulesTests.cs (A_blank_title_is_rejected) |
| UTC003 | Draft input with a description over the limit. | Title: "Chọn địa điểm họp"; Description: 4001 characters; Options: ["Hà Nội", "Đà Nẵng"]; IsAnonymous: true; EligibleRoles: ["Member"]. | Return: errors[description] = ["Mô tả không quá 4000 ký tự."].<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-53 · MSG209 Automated: tests/TrustCabinet.Web.Tests/Votes/VoteRulesTests.cs (A_4001_character_description_is_rejected_while_4000_passes) |
| UTC004 | Draft input with two options that differ only by case. | Title: "Chọn địa điểm họp"; Description: ""; Options: ["Hà Nội", "hà nội"]; IsAnonymous: false; EligibleRoles: ["Member"]. | Return: errors[options] = ["Cần ít nhất 2 phương án khác nhau, mỗi phương án từ 1 đến 200 ký tự."].<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-53 · MSG209 Automated: tests/TrustCabinet.Web.Tests/Votes/VoteRulesTests.cs (Duplicate_options_that_differ_only_by_case_are_rejected) |
| UTC005 | Draft input without an anonymity choice. | Title: "Chọn địa điểm họp"; Description: ""; Options: ["Hà Nội", "Đà Nẵng"]; IsAnonymous: null; EligibleRoles: ["Member"]. | Return: errors[isAnonymous] = ["Chọn biểu quyết công khai hoặc kín."].<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-53 · MSG209 Automated: tests/TrustCabinet.Web.Tests/Votes/VoteRulesTests.cs (A_missing_anonymity_choice_is_rejected) |
| UTC006 | Draft input with no eligible roles. | Title: "Chọn địa điểm họp"; Description: ""; Options: ["Hà Nội", "Đà Nẵng"]; IsAnonymous: false; EligibleRoles: []. | Return: errors[eligibleRoles] = ["Chọn ít nhất một vai trò được bỏ phiếu trong Chủ toạ, Thư ký, Thành viên, Khách mời."].<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-53 · MSG209 Automated: tests/TrustCabinet.Web.Tests/Votes/VoteRulesTests.cs (An_empty_eligible_role_list_is_rejected) |

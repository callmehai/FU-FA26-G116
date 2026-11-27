# Vote.Validate — Unit (FE-11, VOTE)

Description: Vote draft validation: question, options, eligibility roles and anonymity flag.

Precondition: Draft input with title, options and eligible roles.

## VoteRules.Validate

Layer: Domain · `backend/TrustCabinet.Domain/Votes/VoteRules.cs:60`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Draft input with two distinct options and at least one eligible role. | Title: "Chọn địa điểm họp"; Description: ""; Options: ["Hà Nội", "Đà Nẵng"]; IsAnonymous: false; EligibleRoles: ["Chair", "Member"]. | Return: No errors; roles parsed as [Chair, Member].<br>Exception: N/A<br>Log Message: N/A | Normal | UC-63 · MSG209 |
| UTC002 | Draft input with a blank title. | Title: ""; Description: ""; Options: ["Hà Nội", "Đà Nẵng"]; IsAnonymous: false; EligibleRoles: ["Member"]. | Return: errors[title] = ["Nội dung biểu quyết phải có từ 1 đến 300 ký tự."]<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-63 · MSG209 |
| UTC003 | Draft input with a description over the limit. | Title: "Chọn địa điểm họp"; Description: 4001 characters; Options: ["Hà Nội", "Đà Nẵng"]; IsAnonymous: true; EligibleRoles: ["Member"]. | Return: errors[description] = ["Mô tả không quá 4000 ký tự."]; 4000 characters pass.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-63 · MSG209 |
| UTC004 | Draft input with a title at the length limit. | Title: 301 characters; Description: ""; Options: ["Hà Nội", "Đà Nẵng"]; IsAnonymous: false; EligibleRoles: ["Member"]. | Return: errors[title] = ["Nội dung biểu quyết phải có từ 1 đến 300 ký tự."]; 300 characters pass.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-63 · MSG209 |

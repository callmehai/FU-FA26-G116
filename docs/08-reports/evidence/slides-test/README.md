# Ảnh kiểm thử cho slide bảo vệ

Ảnh cho ba slide Unit Test, Integration Test, System Test, chụp ngày 25/11/2026 trên app local (nhánh cắt từ `app`).

| Slide | Ô ảnh | Tệp |
|---|---|---|
| Unit Test | Unit Test Case Sample | `unit-01-test-case-sheet-Report5.1.png` — sheet `Authentication.SignIn` của `Report5.1_Unit_Test.xlsx` |
| Unit Test | Unit Test Code Sample (xUnit) | `unit-02-xunit-code-AuthenticationServiceTests.png` — code test trong `tests/TrustCabinet.Application.Tests` |
| Unit Test | (ảnh phụ) | `unit-03-dotnet-test-ket-qua.png` — `dotnet test`: 19/19 test lọc, toàn project 1034/1034 đạt |
| Integration Test | Integration Test Case Sample | `integration-01-test-case-sheet-Report5.2.png` — sheet `Authentication` của `Report5.2_Integration_Test.xlsx` |
| Integration Test | API Test With Postman Sample | `integration-02-newman-chay-API-phan-dau.png`, `integration-03-newman-chay-API-tong-ket.png` — collection `tests/postman/` chạy bằng Newman: 35 request, 71 assertion, 0 lỗi. Ảnh giao diện Postman: `integration-04-postman-giao-dien-API10.png` (request API10 tạo họp, 201 MSG29, 3/3 test) |
| System Test | System Test Case Sample | `system-01-test-case-sheet-Report5.3.png` — sheet `WorkFlow1` của `Report5.3_System_Test_FRs.xlsx` |
| System Test | System Test Execution Sample | `system-02-qa-runner-report-smoke.png` (report HTML), `system-03-qa-runner-terminal-smoke.png` (terminal) — `tests/qa` plan `smoke`, 5/5 đạt |
| Unit Test | Code mẫu thay thế (biểu quyết) | `unit-04-xunit-code-VoteServiceTests.png` — mở phiếu, phiếu kín không lưu lựa chọn |
| Unit Test | Code mẫu thay thế (nghị sự) | `unit-06-xunit-code-AgendaService.png` — `AgendaService.CreateAsync`, mã UTC001–UTC003 trùng Report 5.1 |
| Unit Test | (ảnh phụ) | `unit-05-dotnet-test-vote-agenda.png` — `dotnet test` lọc Votes và Agenda: 91/91 đạt |
| System Test | Execution Sample (nhiều màn hình) | `system-04`, `system-05`, `system-06` — ảnh màn hình khi chạy WF-02 (nghị sự, thông báo người duyệt) và WF-03 (kết quả biểu quyết) |
| System Test | Execution Sample (terminal) | `system-07-qa-runner-terminal-WF02-WF03.png` — plan `sys-wf02` 8/8 và `sys-wf03` 7/7 đạt |

Chạy WF-02 và WF-03 cần các tài khoản `duc.hoang`, `lan.vu`, `bao.dang`, `mai.phan`, `son.ngo` (@trustcabinet.local, vai Thành viên) trong DB local; bản seed Development chỉ có 4 tài khoản gốc, nên tạo thêm bằng màn Người dùng trước khi chạy.


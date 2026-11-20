# Password.Verify — Unit (FE-01, AUTH)

Description: Password hashing and verification: same password verifies, different password fails, malformed hash fails instead of throwing.

Precondition: No database needed; pure hashing check.

## PasswordHasher.Verify

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | No database needed; pure hashing check. | passwordHash: Hash("MatKhau@2026"); providedPassword: "MatKhau@2026". | Return: true.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-01 · Automated: tests/TrustCabinet.Web.Tests/Authentication/PasswordHasherTests.cs (A_password_verifies_against_its_own_hash) |
| UTC002 | No database needed; pure hashing check. | passwordHash: Hash("MatKhau@2026"); providedPassword: "MatKhau@2027". | Return: false.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-01 · Automated: tests/TrustCabinet.Web.Tests/Authentication/PasswordHasherTests.cs (A_different_password_does_not_verify) |
| UTC003 | No database needed; pure hashing check. | passwordHash: Hash("MatKhau@2026"); providedPassword: "matkhau@2026". | Return: false; unlike the email, the password is case-sensitive.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-01 · Automated: tests/TrustCabinet.Web.Tests/Authentication/PasswordHasherTests.cs (Password_comparison_is_case_sensitive) |
| UTC004 | No database needed; pure hashing check. | passwordHash: one of "", "khong-phai-hash", "SET-BY-SCRUM-11"; providedPassword: "bat-ky-mat-khau". | Return: false; no exception escapes.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-01 · Automated: tests/TrustCabinet.Web.Tests/Authentication/PasswordHasherTests.cs (A_malformed_stored_hash_fails_verification_instead_of_throwing) |
| UTC005 | No database needed; pure hashing check. | passphrase: 200 characters "a"; passwordHash: Hash(passphrase); providedPassword: passphrase. | Return: true.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-01 · Automated: tests/TrustCabinet.Web.Tests/Authentication/PasswordHasherTests.cs (A_long_passphrase_is_supported) |

## PasswordHasher.Hash

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC006 | No database needed; pure hashing check. | password: "MatKhau@2026"; called twice. | Return: Two different hash strings; both verify against "MatKhau@2026".<br>Exception: N/A<br>Log Message: N/A | Normal | UC-01 · Automated: tests/TrustCabinet.Web.Tests/Authentication/PasswordHasherTests.cs (The_same_password_hashes_differently_every_time) |
| UTC007 | No database needed; pure hashing check. | password: "MatKhau@2026". | Return: Hash string that does not contain the password text.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-01 · Automated: tests/TrustCabinet.Web.Tests/Authentication/PasswordHasherTests.cs (The_hash_does_not_contain_the_password) |
| UTC008 | No database needed; users.password_hash is varchar(512). | password: "MatKhau@2026". | Return: Hash string of at most 512 characters.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-01 · Automated: tests/TrustCabinet.Web.Tests/Authentication/PasswordHasherTests.cs (The_hash_fits_the_column) |

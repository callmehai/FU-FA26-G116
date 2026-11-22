# Password.Verify — Unit (FE-01, AUTH)

Description: Password hashing and verification: same password verifies, different password fails, malformed hash fails instead of throwing.

Precondition: No database needed; pure hashing check.

## PasswordHasher.Verify

Layer: Infrastructure · `backend/TrustCabinet.Infrastructure/Authentication/PasswordHasher.cs:30`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | No database needed; pure hashing check. | passwordHash: Hash("MatKhau@2026"); providedPassword: "MatKhau@2026". | Return: true.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-01 |
| UTC002 | No database needed; pure hashing check. | passwordHash: Hash("MatKhau@2026"); providedPassword: "MatKhau@2027". | Return: false.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-01 |
| UTC003 | No database needed; pure hashing check. | passwordHash: one of "", "khong-phai-hash", "SET-BY-SCRUM-11"; providedPassword: "bat-ky-mat-khau". | Return: false; no exception escapes.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-01 |

## PasswordHasher.Hash

Layer: Infrastructure · `backend/TrustCabinet.Infrastructure/Authentication/PasswordHasher.cs:28`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC004 | No database needed; pure hashing check. | password: "MatKhau@2026"; called twice. | Return: Two different hash strings; both verify against "MatKhau@2026".<br>Exception: N/A<br>Log Message: N/A | Normal | UC-01 |

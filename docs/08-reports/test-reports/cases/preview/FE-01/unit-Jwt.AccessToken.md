# Jwt.AccessToken — Unit (FE-01, AUTH)

Description: Access token contents and lifetime: subject, capability claims, expiry, tamper rejection.

Precondition: Signing key configured; test user with known capabilities.

## JwtTokenIssuer.CreateAccessToken

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Signing key configured (test key); issuer and audience TrustCabinet; lifetime 15 minutes.<br>2. Test user Nguyễn Thư Ký with known id and email. | User: active Secretary "Nguyễn Thư Ký" <thuky@trustcabinet.local>; capabilities: [MeetingCreator, MinutesEditor]. | Return: Signed JWT with claims sub (user id), jti (unique), email, name, and one "cap" claim per capability: [MeetingCreator, MinutesEditor]; no role claim.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-01 · Automated: tests/TrustCabinet.Web.Tests/Authentication/JwtTokenIssuerTests.cs (The_access_token_identifies_the_user; The_access_token_carries_one_claim_per_capability) |
| UTC002 | Signing key configured; test user Nguyễn Thư Ký. | User: active Secretary "Nguyễn Thư Ký" <thuky@trustcabinet.local>; capabilities: []. | Return: Signed JWT with no "cap" claim.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-01 · Automated: tests/TrustCabinet.Web.Tests/Authentication/JwtTokenIssuerTests.cs (An_access_token_for_a_member_carries_no_capability_claim) |
| UTC003 | Signing key configured; test user Nguyễn Thư Ký. | Same user and empty capabilities; called twice. | Return: Two signed JWTs with different jti values.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-01 · Automated: tests/TrustCabinet.Web.Tests/Authentication/JwtTokenIssuerTests.cs (Every_access_token_has_its_own_id) |
| UTC004 | Signing key configured with a 5-minute access token lifetime. | User: active Secretary "Nguyễn Thư Ký"; AccessTokenMinutes: 5. | Return: Signed JWT expiring about 5 minutes after creation.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-01 · Automated: tests/TrustCabinet.Web.Tests/Authentication/JwtTokenIssuerTests.cs (The_access_token_expires_after_the_configured_lifetime) |
| UTC005 | Signing key configured; test user Nguyễn Thư Ký. | Token created for the Secretary; validated with a different signing key. | Return: Signed JWT that the validator accepts under the signing key and rejects under any other key or after tampering.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-01 · Automated: tests/TrustCabinet.Web.Tests/Authentication/JwtTokenIssuerTests.cs (A_token_signed_with_another_key_is_rejected; A_tampered_token_is_rejected) |
| UTC006 | Signing key configured; test user with a known password hash. | User with PasswordHash "khong-lien-quan"; capabilities: []. | Return: Signed JWT whose raw text does not contain the password hash.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-01 · Automated: tests/TrustCabinet.Web.Tests/Authentication/JwtTokenIssuerTests.cs (The_access_token_never_carries_the_password_hash) |

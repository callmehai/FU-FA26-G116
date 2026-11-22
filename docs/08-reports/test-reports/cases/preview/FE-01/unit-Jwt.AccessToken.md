# Jwt.AccessToken — Unit (FE-01, AUTH)

Description: Access token contents and lifetime: subject, capability claims, expiry, tamper rejection.

Precondition: Signing key configured; test user with known capabilities.

## JwtTokenIssuer.CreateAccessToken

Layer: Infrastructure · `backend/TrustCabinet.Infrastructure/Authentication/JwtTokenIssuer.cs:25`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Signing key configured (at least 32 characters); issuer and audience "TrustCabinet"; access token lifetime 15 minutes.<br>2. Active account "Nguyễn Thư Ký" <thuky@trustcabinet.local> with a known id. | user: Nguyễn Thư Ký (thuky@trustcabinet.local); capabilities: [MeetingCreator, MinutesEditor]. | Return: Signed HS256 JWT with claims sub = user id, jti (unique), email = "thuky@trustcabinet.local", claim "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name" = "Nguyễn Thư Ký", and one "cap" claim per capability: MeetingCreator and MinutesEditor; no role claim; issuer and audience "TrustCabinet".<br>Exception: N/A<br>Log Message: N/A | Normal | UC-01 · BR-08 · Deviation: real token carries the full name under claim type "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name" (ClaimTypes.Name), not "name"; sub, jti, email, cap x2, no role claim, iss/aud "TrustCabinet", HS256, exp ~15 min, no nbf — all as expected. |
| UTC002 | Signing key configured (at least 32 characters); issuer and audience "TrustCabinet"; access token lifetime 15 minutes. | user: Nguyễn Thư Ký; capabilities: [] (empty). | Return: Signed JWT with sub, jti, email and name claims and no "cap" claim.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-01 · BR-04 |
| UTC003 | Signing key configured with AccessTokenMinutes = 5. | user: Nguyễn Thư Ký; capabilities: []. | Return: Signed JWT whose exp is about 5 minutes after creation (within a few seconds); it carries no nbf claim.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-01 |
| UTC004 | Signing key configured with AccessTokenMinutes = 1 (the smallest allowed). | user: Nguyễn Thư Ký; capabilities: []. | Return: Signed JWT whose exp is about 1 minute after creation.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-01 |
| UTC005 | Signing key configured (at least 32 characters); issuer and audience "TrustCabinet"; access token lifetime 15 minutes. | Token created for the Secretary, then validated with a different signing key, and again after one character of the payload is changed. | Return: The token validates under the configured signing key and fails validation under any other key or after tampering.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-01 |

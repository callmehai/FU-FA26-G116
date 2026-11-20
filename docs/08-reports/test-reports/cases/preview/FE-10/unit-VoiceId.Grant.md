# VoiceId.Grant — Unit (FE-10, TRANS)

Description: Voice ID consent: grant and withdraw, with erasure on withdrawal.

Precondition: User with and without consent.

## VoiceIdService.GrantAsync

Layer: Application · `backend/TrustCabinet.Application/VoiceId/VoiceIdService.cs:35`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | User has no active consent. | actorId: a user without consent; noticeVersion: "1.3". | Return: Success with the voice profile showing the new consent (MSG305 "Đã bật nhận diện giọng nói cho tài khoản của bạn.").<br>Exception: N/A<br>Log Message: Saved: consent row with notice 1.3 and its purpose; audit row records the grant. | Normal | UC-81b · BR-361 · BR-362 · MSG305 |
| UTC002 | User consented to notice "1.2" and keeps voice samples. | actorId: a user consented to "1.2"; noticeVersion: "1.3". | Return: Success (MSG305); the consent is renewed in place to 1.3 and the samples stay.<br>Exception: N/A<br>Log Message: Saved: consent notice renewed; audit row records the renewal with the old and new versions. | Normal | UC-81b · BR-362 · MSG305 |
| UTC003 | User already consented to notice "1.3". | actorId: a user consented to "1.3"; noticeVersion: "1.3". | Return: Success with the same profile (MSG305); agreeing again creates no new row.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-81b · BR-362 · MSG305 |
| UTC004 | Current notice is "1.3". | actorId: a user; noticeVersion: "1.2". | Return: Failure Invalid (MSG313); errors[noticeVersion] = ["Nội dung thông báo đã thay đổi. Hãy tải lại trang và đọc lại trước khi đồng ý."]; nothing is stored.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-81b · BR-362 · MSG313 |
| UTC005 | User sends no notice version. | actorId: a user; noticeVersion: blank. | Return: Failure Invalid (MSG313); consent is to the words the person actually read, so a missing version is refused.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-81b · BR-362 · MSG313 |
| UTC006 | User has no active consent; the agree button is pressed twice at once. | actorId: a user without consent; noticeVersion: "1.3" sent twice at the same time. | Return: Success (MSG305); one row wins and the other answer reads the same, so the person is agreed either way.<br>Exception: N/A<br>Log Message: Saved: one consent row; audit row records the grant. | Normal | UC-81b · BR-362 · MSG305 |

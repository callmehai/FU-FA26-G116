# VoiceId.Withdraw — Unit (FE-10, TRANS)

Description: Voice ID consent: grant and withdraw, with erasure on withdrawal.

Precondition: User with and without consent.

## VoiceIdService.WithdrawAsync

Layer: Application · `backend/TrustCabinet.Application/VoiceId/VoiceIdService.cs:88`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | User consented and keeps own and collected samples plus machine suggestions pointing at them. | actorId: a consented user with samples. | Return: Success with the profile showing no consent (MSG306 "Đã tắt nhận diện giọng nói và xoá toàn bộ giọng mẫu."); every sample and every suggestion pointing at the person is gone in the same write.<br>Exception: N/A<br>Log Message: Saved: samples and suggestions erased, consent closed; audit row records the withdrawal with the erased counts. | Normal | UC-81f · BR-365 · BR-454 · MSG306 |
| UTC002 | User has no active consent. | actorId: a user without consent. | Return: Success with the profile showing no consent (MSG306); nothing to erase.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-81f · MSG306 |
| UTC003 | User consented; the store fails while erasing. | actorId: a consented user. | Return: Failure SaveFailed (MSG315 "Không lưu được thay đổi. Vui lòng thử lại.").<br>Exception: PersistenceException is caught inside the service.<br>Log Message: Error logged with the user id. | Abnormal | UC-81f · MSG315 |

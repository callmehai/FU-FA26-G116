# VoiceCollection.Add — Unit (FE-10, TRANS)

Description: Collecting voice samples for a person: who may collect, add, hear and delete (ADR-0071, ADR-0075).

Precondition: Active person with Voice ID consent; collector holds the Thu giọng mẫu hộ capability.

## VoiceCollectionService.AddForPersonAsync

Layer: Application · `backend/TrustCabinet.Application/VoiceId/VoiceCollectionService.cs:68`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Collector holds the Thu giọng mẫu hộ capability.<br>2. Person is active and consented to notice "1.4" or later, with 2 collected samples.<br>3. Audio holds 20 seconds of one clear voice. | actorId: the collector; userId: the person recorded; audio: a clear voice recording; contentType: audio/mpeg; personAgreed: true. | Return: Success with the collected sample (MSG473 "Đã lưu giọng mẫu thu hộ."); the sample belongs to the person recorded, with the collector on it and no meeting.<br>Exception: N/A<br>Log Message: Saved: encrypted sample with its recording; notification to the person; audit row records the collection with the proxy scope. | Normal | UC-86b · BR-452 · BR-453 · MSG473 · Deviation: no meeting scope since ADR-0075 |
| UTC002 | The person has no active consent, or consented only to a notice before 1.4. | actorId: the collector; userId: the person recorded; audio: a voice recording; contentType: audio/mpeg; personAgreed: true. | Return: Failure NoConsent (MSG475 "Người này chưa đồng ý dùng Voice ID theo điều khoản mới. Họ bật ở trang Giọng nói của tôi."); the audio never goes to the worker.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-86b · BR-449 · MSG475 |
| UTC003 | The person already has 50 proxy samples. | actorId: the collector; userId: the person recorded; audio: a voice recording; contentType: audio/mpeg; personAgreed: true. | Return: Failure LimitReached (MSG476 "Đã đủ 50 giọng mẫu thu hộ cho người này. Xoá bớt rồi thu lại."); nothing is kept.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-86b · BR-448 · MSG476 · Deviation: limit is 50 per person, 5 per collector is gone (ADR-0075) |
| UTC004 | 1. The person consented to notice "1.4" or later.<br>2. Audio holds 3 seconds of speech. | actorId: the collector; userId: the person recorded; audio: a 3-second recording; contentType: audio/mpeg; personAgreed: true. | Return: Failure Rejected (MSG309); errors[file] = ["Giọng mẫu quá ngắn — cần ít nhất 6 giây có tiếng nói. Hãy đọc hết đoạn văn mẫu."]; nothing is kept.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-86b · BR-363 · MSG309 |

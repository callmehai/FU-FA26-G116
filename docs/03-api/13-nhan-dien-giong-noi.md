# 13 · Nhận diện người nói — Voice ID (SCRUM-35)

Bản gỡ băng (SCRUM-31) chia lời nói theo *Người nói 1, 2, 3…*: đó là **tách người nói**
(*diarization*), chỉ biết hai đoạn là cùng hay khác giọng, không biết là ai. Voice ID trả lời câu
"ai": mỗi người **tự nguyện** đăng ký giọng mẫu; sau khi gỡ băng, hệ thống so từng giọng trong bản ghi
với giọng mẫu của **những người có trong cuộc họp và đã đồng ý**, rồi **gợi ý** tên. Giọng khớp rõ thì
máy tự điền tên, nhưng chỉ với người đã đồng ý thông báo 1.2 trở lên
([ADR-0045](../05-decisions/0045-may-tu-gan-ten-khi-giong-khop-ro.md),
[ADR-0048](../05-decisions/0048-thong-bao-voice-id-1-2-may-tu-dien-ten.md)); Thư ký luôn sửa được tên.
Quyết định thiết kế: [ADR-0027](../05-decisions/0027-voice-id-co-dong-y.md).

Mỗi người có **hai bộ giọng mẫu**, mỗi bộ một đồng ý riêng ([ADR-0060](../05-decisions/0060-thu-ky-thu-voice-id-ho.md)):
bộ **tự thu** trên máy của mình, và bộ **Thư ký thu hộ** bằng micro phòng họp. Máy so giọng với cả hai bộ.

## Dữ liệu sinh trắc học — nghĩa vụ và cách giữ

Giọng nói dùng để nhận ra một người là **dữ liệu sinh trắc học** (Luật Bảo vệ dữ liệu cá nhân
91/2025/QH15, Điều 31 khoản 2), thuộc danh mục **dữ liệu cá nhân nhạy cảm** (Nghị định 356/2025/NĐ-CP,
Điều 4 khoản 1 điểm đ). Vì vậy:

- Phải có sự đồng ý riêng. Im lặng hay không phản hồi không phải là đồng ý (Luật, Điều 9 khoản 4
  điểm d), và không được đặt sẵn đồng ý (Nghị định, Điều 6 khoản 3).
- Trước khi đồng ý, người dùng phải biết loại dữ liệu và mục đích, bên kiểm soát dữ liệu là ai,
  các quyền của mình (Luật, Điều 9 khoản 2), và biết đây là dữ liệu nhạy cảm (Nghị định, Điều 6
  khoản 4).
- Đơn vị triển khai phải lập hồ sơ đánh giá tác động xử lý dữ liệu cá nhân trong 60 ngày (Luật,
  Điều 21). Miễn trừ cho doanh nghiệp nhỏ không áp dụng khi xử lý dữ liệu nhạy cảm (Điều 38
  khoản 2). Bảng dưới là những gì hồ sơ đó cần lấy từ hệ thống.

| Nghĩa vụ | Cách hệ thống giữ |
|---|---|
| Đồng ý riêng, rõ ràng, do chính người đó | `voice_consents`: một lần đồng ý riêng, không gộp với đồng ý ghi âm; lưu phiên bản thông báo (`noticeVersion`) và mục đích người đó đã đọc. Mỗi bộ mẫu một dòng đồng ý (`scope`: `Self` / `Proxy`). Không ai đồng ý thay người khác được — không route `/api/me/voice` nào nhận id người khác, và Thư ký thu hộ cần dòng `Proxy` do chính người đó tạo |
| Nêu mục đích | `Purpose`: *Gợi ý tên người phát biểu trong bản gỡ băng của các cuộc họp tôi tham dự, để Thư ký lập biên bản.* |
| Thông báo đủ trước khi đồng ý | Màn "Giọng nói của tôi" nêu mục đích, thứ được lưu, phạm vi dùng, quyền của người dùng, **bên kiểm soát dữ liệu** (`controller`, lấy từ cấu hình `VoiceId:Controller`; mỗi đơn vị triển khai đặt tên pháp nhân của mình), và ghi rõ đây là dữ liệu cá nhân nhạy cảm. Ô đồng ý không đánh dấu sẵn |
| Thu thập tối thiểu | Lưu vector đặc trưng giọng (512 số) và, từ thông báo 1.1, **bản ghi giọng mẫu** để chính người đó nghe lại và giữ mẫu rõ ([ADR-0042](../05-decisions/0042-luu-ban-ghi-giong-mau-de-nghe-lai.md)). Mẫu ghi dưới đồng ý 1.0 không lưu bản ghi. Vector của các giọng trong bản ghi họp chỉ dùng để so một lần rồi bỏ, không lưu |
| Bảo mật (Luật, Điều 31 khoản 4) | Vector mã hoá AES-256-GCM; dữ liệu kèm (AAD) gắn với người, id mẫu và mô hình — chép sang người khác là không giải mã được. Bản ghi mẫu mã hoá cùng khoá, AAD gắn với người và id mẫu. Chỉ backend giải mã; không endpoint nào trả vector ra ngoài, và bản ghi mẫu chỉ trả cho chính chủ. Mọi lần đọc ghi đều có nhật ký kiểm toán |
| Rút đồng ý, xoá | `DELETE /api/me/voice/consent`: trong một transaction, xoá **mọi** giọng mẫu, **mọi gợi ý** trỏ tới người đó và **tên máy tự gán** là người đó (ADR-0045); tên Thư ký đã gán giữ nguyên (đó là quyết định của người, không phải dữ liệu sinh trắc). Bản gỡ băng đang chạy dở không ghi lại được gợi ý cho người vừa rút: máy so giọng dưới khoá chia sẻ trên dòng đồng ý, việc rút đồng ý phải chờ nó ghi xong rồi mới xoá ([ADR-0049](../05-decisions/0049-vo-hieu-hoa-tai-khoan-ket-thuc-voice-id.md)) |
| Người bị vô hiệu hoá tài khoản | Không đăng nhập được nên không tự rút được. Vô hiệu hoá (`PUT /api/users/{id}/status`, `isActive: false`) **kết thúc Voice ID như khi rút đồng ý**, trong cùng transaction: xoá giọng mẫu, gợi ý và tên máy tự gán, đóng dòng đồng ý; người làm trong nhật ký là quản trị viên. Kích hoạt lại không khôi phục gì: người đó phải đồng ý và ghi giọng mẫu lại ([ADR-0049](../05-decisions/0049-vo-hieu-hoa-tai-khoan-ket-thuc-voice-id.md)). Máy cũng không so giọng với tài khoản đã vô hiệu hoá |
| Giới hạn phạm vi dùng | Chỉ so với người có trong danh sách thành phần hiện tại của **chính cuộc họp đó**; người ngoài cuộc họp có giọng giống đến đâu cũng không được gợi ý |
| Người khác chạm vào giọng mẫu (ADR-0060) | Chỉ Thư ký **hiện tại** của **chính cuộc họp đó**, có quyền `VoiceCollector` do quản trị viên cấp. Thư ký chỉ thấy, nghe, xoá mẫu thu ở cuộc họp của mình; mỗi lần nghe ghi `voice.sample_played`; mỗi lần thu, chủ mẫu nhận thông báo `VoiceSampleCollected`. Chủ mẫu thấy mọi mẫu thu hộ kèm cuộc họp và người thu, nghe lại và xoá được |
| Bằng chứng | Nhật ký kiểm toán: `voice.consent_granted`, `voice.consent_renewed` (đồng ý thông báo mới, kèm hai phiên bản), `voice.consent_withdrawn` (kèm `reason`: `withdrawn` khi chính người đó rút, `accountDeactivated` khi quản trị viên vô hiệu hoá tài khoản; số mẫu đã xoá, số gợi ý đã bỏ), `voice.sample_added`, `voice.sample_deleted`, `voice.sample_played` (Thư ký nghe mẫu thu hộ), `transcript.speaker_assigned` (kèm gợi ý của máy và Thư ký có chấp nhận không). Các dòng của bộ thu hộ có `scope: "proxy"` trong `details` |

## Voice ID của chính mình (`/api/me/voice`)

### `GET /api/me/voice`

```json
{
  "consent": { "grantedAt": "…", "noticeVersion": "1.2", "purpose": "Nhận ra người phát biểu …" },
  "samples": [ { "id": "…", "createdAt": "…", "speechMs": 7200, "playable": true } ],
  "maxSamples": 5, "noticeVersion": "1.2", "purpose": "Nhận ra người phát biểu …",
  "controller": "Nhóm đồ án SEP490-G116, đơn vị vận hành bản thử nghiệm TrustCabinet",
  "proxy": {
    "consent": { "grantedAt": "…", "noticeVersion": "1.0", "purpose": "Cho phép Thư ký của cuộc họp tôi tham dự …" },
    "samples": [ { "id": "…", "createdAt": "…", "speechMs": 9100, "playable": true,
                   "meetingId": "…", "meetingTitle": "Họp giao ban tháng 11", "collectedByName": "Lê Thư Ký" } ],
    "maxSamples": 5, "noticeVersion": "1.0", "purpose": "Cho phép Thư ký của cuộc họp tôi tham dự …"
  }
}
```

`samples` ở gốc chỉ là mẫu tự thu; `proxy.samples` là mẫu Thư ký thu hộ.

`consent` là `null` khi chưa đồng ý hoặc đã rút. `noticeVersion`, `purpose` và `controller` ở gốc là
thông báo hiện hành mà FE hiện cho người dùng đọc trước khi đồng ý. `consent.noticeVersion` cũ hơn
thông báo hiện hành thì FE mời đồng ý điều khoản mới. `playable`: bản ghi mẫu được lưu, chủ mẫu nghe lại được.

### `POST /api/me/voice/consent` — `{ "noticeVersion": "1.2" }`

Phải gửi đúng phiên bản thông báo đang hiện (khác → `400 MSG313`, `errors.noticeVersion`), để đồng ý
luôn gắn với câu chữ người đó đã đọc. Đã đồng ý rồi thì không tạo thêm. Đang đồng ý theo thông báo cũ
thì **gia hạn ngay dòng đó** lên phiên bản mới: giọng mẫu giữ nguyên, nhật ký ghi `voice.consent_renewed`.
`200 MSG305`, `data` là hồ sơ như `GET`.

### `DELETE /api/me/voice/consent`

`200 MSG306`. Xoá mọi mẫu **tự thu**; mẫu Thư ký thu hộ giữ nguyên (đồng ý riêng). Gợi ý và tên máy tự điền trỏ tới
người này đều bị bỏ, lần gỡ băng sau gợi ý lại từ bộ còn lại. Đồng ý lại sau đó tạo **dòng đồng ý mới**; dòng đã rút
giữ nguyên làm bằng chứng.

### `POST /api/me/voice/samples` — `multipart/form-data`, trường `file`

Tệp ≤ 10 MB, tối đa 60 giây. FE ghi bằng micro khi người dùng đọc một đoạn văn mẫu. Worker kiểm tra
chất lượng trước khi tính vector:

| `reason` (`422 MSG309`) | Điều kiện |
|---|---|
| `too_short` | Ít hơn 6 giây có tiếng nói (sau khi cắt khoảng lặng) |
| `too_quiet` | Âm lượng phần có tiếng nói dưới −40 dBFS |
| `clipped` | Hơn 1% mẫu bị bão hoà (rè) |
| `multiple_speakers` | Các đoạn ~2 giây trong mẫu không giống nhau đủ — nghe như hơn một người |
| `too_long` | Quá 60 giây hoặc quá 10 MB |

`201 MSG307`, `data`: `{ id, createdAt, speechMs, playable }`. Dưới đồng ý 1.1 trở lên, tệp gửi lên được lưu
mã hoá để chủ mẫu nghe lại (`playable: true`); dưới 1.0 thì không. Tối đa 5 mẫu (`409 MSG310`); chưa đồng ý
`409 MSG308`; worker không chạy `503 MSG312`.

### `GET /api/me/voice/samples/{sampleId}/audio`

Bản ghi mẫu, giải mã cho chính chủ nghe lại ([ADR-0042](../05-decisions/0042-luu-ban-ghi-giong-mau-de-nghe-lai.md)):
`200` với đúng loại tệp đã gửi, `Cache-Control: no-store`, hỗ trợ `Range`. Mẫu của người khác, mẫu
không lưu bản ghi (đồng ý 1.0) hay không tồn tại: cùng một `404`. FE tải bằng phiên đăng nhập rồi phát
từ bộ nhớ, không ghi ra đĩa.

### `DELETE /api/me/voice/samples/{sampleId}`

`200 MSG311`, bản ghi mẫu bị xoá cùng. Xoá được cả mẫu Thư ký thu hộ cho mình. Mẫu của người khác: `404`.
`GET …/audio` cũng nghe được cả mẫu thu hộ của mình.

### `POST /api/me/voice/proxy-consent` — `{ "noticeVersion": "1.0" }`

Cho phép Thư ký của cuộc họp mình dự thu giọng mẫu hộ bằng micro phòng họp
([ADR-0060](../05-decisions/0060-thu-ky-thu-voice-id-ho.md)). Đồng ý riêng, không cần đồng ý tự thu và ngược lại.
Sai phiên bản thông báo `400 MSG313`. Đã cho phép rồi thì không tạo thêm. `200 MSG471`, `data` là hồ sơ như `GET`.

### `DELETE /api/me/voice/proxy-consent`

`200 MSG472`. Trong một transaction: xoá **mọi** mẫu thu hộ (ở mọi cuộc họp), bỏ gợi ý và tên máy tự điền trỏ tới người
này, đóng dòng đồng ý. Mẫu tự thu giữ nguyên.

## Thư ký thu giọng mẫu hộ — ADR-0060

Họp trực tiếp và kết hợp được gỡ băng từ micro phòng họp. Giọng mẫu ghi bằng chính micro đó khớp tốt hơn giọng mẫu mỗi
người tự ghi trên laptop, nên Thư ký thu hộ mọi người trước hoặc trong buổi họp. Ba lớp chặn:

1. Người gọi có quyền **`VoiceCollector`** ("Thu giọng mẫu hộ" trong ma trận phân quyền). Thiếu: `403`.
2. Người gọi là **Thư ký hiện tại** của **chính cuộc họp đó**, cuộc họp **trực tiếp hoặc kết hợp**, không phải **Mật**,
   chưa **huỷ**. Kiểm lại ở mỗi lần gọi. Không đạt: `404` không body, như cuộc họp không tồn tại.
3. Người được thu đã tự bật **đồng ý thu hộ** (`POST /api/me/voice/proxy-consent`). Chưa: `409 MSG475`.

Mẫu thu hộ thuộc về người được thu: mã hoá gắn với người đó như mẫu tự thu, nằm dưới đồng ý `Proxy` của họ, họ nghe và
xoá được. Bản ghi mẫu luôn được lưu (mã hoá) để Thư ký nghe lại kiểm tra.

### `GET /api/meetings/{meetingId}/voice-collection`

```json
{
  "canCollect": true,
  "maxSamples": 5,
  "people": [
    { "userId": "…", "fullName": "Nguyễn Văn A", "role": "Member", "consented": true, "sampleCount": 3,
      "samples": [ { "id": "…", "createdAt": "…", "speechMs": 9100, "collectedByName": "Lê Thư Ký" } ] }
  ]
}
```

`people`: người trong danh sách thành phần hiện tại (không gồm Khách). `sampleCount`: mọi mẫu thu hộ của người đó, ở mọi
cuộc họp, để so với giới hạn 5. `samples`: chỉ mẫu thu **ở cuộc họp này**. `canCollect`: cuộc họp đang Đã công bố hoặc
Đang họp; khi khác (ví dụ đã kết thúc) vẫn xem, nghe, xoá được nhưng không thu thêm.

### `POST /api/meetings/{meetingId}/voice-collection/{userId}/samples` — `multipart/form-data`, trường `file`

Kiểm tra tệp và chất lượng như `POST /api/me/voice/samples` (`415 MSG292`, `422 MSG309`, `503 MSG312`). `201 MSG473`,
`data`: `{ id, createdAt, speechMs, collectedByName }`. Người được thu nhận thông báo `VoiceSampleCollected` (mở
`/mgmt/my-voice?tab=proxy`). `userId` không phải người trong cuộc họp: `404`. Chưa đồng ý thu hộ `409 MSG475`; đủ 5 mẫu
thu hộ `409 MSG476`; cuộc họp chưa công bố hoặc đã kết thúc `409 MSG477`.

### `GET /api/meetings/{meetingId}/voice-collection/samples/{sampleId}/audio`

Bản ghi mẫu thu ở cuộc họp này, như `GET /api/me/voice/samples/{id}/audio`; mỗi lần nghe ghi `voice.sample_played`. Mẫu
thu ở cuộc họp khác hoặc mẫu tự thu: `404`.

### `DELETE /api/meetings/{meetingId}/voice-collection/samples/{sampleId}`

`200 MSG474`. Chỉ mẫu thu ở cuộc họp này; còn lại `404`. Thu lại = xoá rồi thu mẫu mới.

## Gợi ý và gán tên trong bản gỡ băng

`GET /api/recordings/{id}/transcript` (xem [12](12-ban-ghi-cuoc-hop.md)) trả thêm:

```json
"speakers": [
  { "label": "SPEAKER_00", "speechMs": 30000,
    "suggestedUserId": "…", "suggestedName": "Nguyễn Thư Ký", "suggestionScore": 0.83,
    "assignedUserId": null, "assignedName": null, "sameAsLabel": null },
  { "label": "SPEAKER_01", "speechMs": 7000,
    "suggestedUserId": "…", "suggestedName": "Nguyễn Thư Ký", "suggestionScore": 0.76,
    "assignedUserId": null, "assignedName": null, "sameAsLabel": "SPEAKER_00", "assignedGuestId": null,
    "autoAssigned": false }
],
"people": [ { "id": "…", "fullName": "…", "guest": false }, { "id": "…", "fullName": "Bà Lan", "guest": true } ]
```

`people` là thành phần hiện tại của cuộc họp và Khách được mời chưa bị thu hồi lời mời (`guest: true`, tên Khách tự
khai, chưa khai thì tên lúc mời; như AI-eCabinet), chỉ trả cho Thư ký (để chọn tên); người khác nhận mảng rỗng. Khách
không có giọng mẫu nên không bao giờ được gợi ý. **`suggestedUserId`, `suggestedName`, `suggestionScore` cũng chỉ trả cho Thư ký**: đó là phỏng đoán
của máy về danh tính sinh trắc học của một người, để Thư ký quyết định, không phải thông tin cho cả cuộc
họp; người khác nhận `null`. Tên hiển thị của một giọng: `assignedName` nếu Thư ký đã gán, nếu không thì
*Người nói N*.

**Cách gợi ý** (`SpeakerMatcher`): điểm của một người với một giọng là cosine cao nhất giữa vector giọng
và các giọng mẫu của người đó. Chỉ gợi ý khi điểm ≥ `VoiceId:SuggestThreshold` (0,6) **và** hơn người
xếp sau ít nhất `VoiceId:SuggestMargin` (0,1). Ngưỡng 0,6 đo trên 24 cuộc họp giả lập bằng giọng thật (VIVOS, sạch và qua phòng họp, xem
`workers/speech/eval/`). Ở 0,5, 3% thời gian nói bị gợi ý sai người, có cả người chưa đăng ký bị gợi ý
tên. Ở 0,6, không gợi ý sai lần nào, và 97–98% lời của người đã đăng ký vẫn được gợi ý đúng tên.

**Một người bị tách làm hai** (như AI-eCabinet): máy tách người nói có thể chia một người thành hai nhãn, nên
một người được gợi ý cho nhiều giọng. Giọng thứ hai trở đi phải nghe giống giọng được gợi ý chắc nhất
(cosine giữa hai vector giọng ≥ `VoiceId:SameVoiceThreshold`, 0,7). Thiếu điều kiện này, người chưa đăng ký
có giọng gần một người đã đăng ký bị gợi ý tên (11% lời của họ trên bộ sạch); ở 0,7 không còn trường hợp nào,
còn lời của người đã đăng ký được gợi ý đúng tăng từ 97% lên 99% (đo lại 29/10/2026, `evaluate.py --rule`).
Giọng gợi ý kém chắc hơn có `sameAsLabel`: nhãn của giọng chắc hơn (giọng đã được Thư ký gán đúng người đó
thì nó đứng đầu, gán người khác thì bị loại khỏi nhóm). FE hiện "Cùng giọng với Người nói N" và cho gán cả
hai trong một lần. Hai giọng đều giữ gợi ý; máy không tự gộp, vì gộp sai là gán nhầm tên người.

Giọng thứ hai trở đi còn phải đạt `VoiceId:SecondVoiceThreshold` (0,7) so với giọng mẫu (ADR-0045). Hai người
ngồi chung một mic phòng có thể giống nhau như một người bị tách làm hai: trên server test ngày 31/10/2026, hai người
khác nhau giống nhau 0,745, một người bị tách làm hai giống nhau 0,777. Điểm so với giọng mẫu thì tách được: người lạ
0,645, người thật 0,771.

Giọng còn chưa có tên mà giống giọng đầu tiên của một người đã nhận ra từ `VoiceId:InheritVoiceThreshold` (0,8)
trở lên thì nhận luôn người đó, kèm điểm của giọng đầu, bất kể điểm so với giọng mẫu (ADR-0050). Đó là một người bị
máy tách ra: trên server test ngày 03/11/2026, Hải hát hai lượt, bản chạy lại cả băng tách giọng Hải thành bốn giọng
giống nhau 0,81–0,85, giọng hát lại khớp kém với giọng mẫu đọc, nên ba giọng phụ không được gợi ý ai. Hai người khác
nhau chưa từng giống nhau quá 0,745. Giọng đầu được máy tự gán thì các giọng nhận theo cũng được tự gán.

**Máy tự gán** (ADR-0045, PO 01/11/2026): giọng có điểm ≥ `VoiceId:AutoAssignThreshold` (0,75) được gán luôn tên
người được gợi ý lúc chuyển chữ xong: `assignedUserId` có giá trị, `autoAssigned: true`. Nhật ký ghi
`transcript.speaker_assigned` không có người làm, kèm `automatic: true` và điểm. Thư ký đổi hoặc bỏ tên bằng `PUT`
như thường; sau đó `autoAssigned` là `false`. Người đó rút đồng ý Voice ID thì tên máy gán bị bỏ cùng gợi ý, còn tên
Thư ký gán thì giữ. Máy không gán Khách. Máy chỉ tự gán người có đồng ý từ thông báo 1.2 trở lên (ADR-0048):
người còn ở bản 1.1 hay 1.0 vẫn chỉ được gợi ý, cho tới khi đồng ý điều khoản mới.

Đo trên 12 cuộc họp VIVOS qua phòng họp (`evaluate.py --second-threshold 0 0.65 0.7 0.75 --auto`, 02/11/2026):

| Giọng thứ hai cần | Lời người đã đăng ký được gợi ý đúng | Gợi ý sai | Người lạ bị gợi ý |
|---|---|---|---|
| không có (cũ) | 99,1% | 0% | 0% |
| 0,7 | 98,4% | 0% | 0% |

| Điểm | Số giọng được gợi ý | Đúng người |
|---|---|---|
| 0,60–0,70 | 18 | 18 |
| 0,70–0,75 | 13 | 13 |
| ≥ 0,75 (máy tự gán) | 20 | 20 |

VIVOS không có lần gợi ý sai nào ở mọi khoảng điểm, nên ngưỡng tự gán lấy theo dữ liệu thật trên server test: người
lạ 0,645; người thật 0,737–0,938. 0,75 để cách người lạ một khoảng rộng. Ở 0,7, hai giọng bị tách của cùng một người
hiện thành hai người nói; Thư ký gán cùng một tên là gộp lại.

### `PUT /api/recordings/{recordingId}/speakers/{label}` — `{ "userId": "…" | null, "guestId": "…" | null }`

Thư ký gán tên thật (một lần áp dụng cho mọi đoạn của giọng đó): một người dùng (`userId`) hoặc một Khách của cuộc
họp (`guestId`), không cả hai; cả hai `null` là bỏ gán. Người được gán phải là thành phần hiện tại của cuộc họp, Khách
phải có lời mời của cuộc họp này chưa bị thu hồi, gửi cả hai cũng vậy (`400 MSG317`). Response có `assignedGuestId`. Không phải Thư ký: `403 MSG295`. Nhãn
không có trong bản ghi: `404`. `200 MSG316` `{ code, message, speaker }`.

### `PUT /api/transcript-segments/{segmentId}/speaker` — `{ "userId": "…" }`, `{ "guestId": "…" }` hoặc `{ "label": "SPEAKER_NN" }`

Thư ký nói ai nói **riêng một câu**, khi máy tách giọng xếp nhầm câu đó vào giọng của người khác. Như "chỉ câu này" của
AI-eCabinet, câu được **chuyển sang một giọng khác** của bản ghi, các câu khác giữ nguyên giọng của mình:

- `userId` / `guestId`: câu chuyển sang giọng đã được gán tên người đó (giọng của chính câu nếu đã là người đó, không thì
  giọng có nhãn nhỏ nhất). Chưa có giọng nào mang tên người đó thì tạo một giọng mới với nhãn `SPEAKER_NN` nhỏ nhất chưa
  dùng, gán sẵn người đó (không có vector, không có gợi ý). Người đó vì thế có thẻ giọng, màu và thời lượng riêng; câu
  sau của cùng người vào lại giọng ấy, không thêm thẻ.
- `label`: câu chuyển về một giọng có sẵn của **bản ghi này** (ví dụ giọng câu bị lấy nhầm khỏi). Nhãn không thuộc bản
  ghi này (của bản ghi khác hay không có): `404`.

Gửi đúng một trong ba trường; gửi nhiều hơn hoặc không gửi gì: `400 MSG317`. Cùng điều kiện với gán giọng: người dùng là
thành phần hiện tại, Khách có lời mời chưa thu hồi (`400 MSG317`); không phải Thư ký `403 MSG295`; câu không tồn tại hay
không đọc được `404`. `200 MSG407` `{ code, message, segment: { segmentId, speakerLabel, previousLabel, newVoice } }`.
Câu đã ở đúng giọng thì không ghi gì.

Độ dài của câu chuyển theo câu: `speechMs` của giọng câu rời đi trừ đi, của giọng câu đến cộng thêm (giọng mới bắt đầu
bằng độ dài câu đó). Không cộng lại từ các câu, vì máy tách giọng đo thời gian nói theo cách riêng: cộng lại có thể làm
một giọng vừa nhận thêm câu lại thành nói ít đi. Chuyển câu về chỗ cũ thì mỗi giọng lấy lại đúng thời gian cũ. Một giọng không còn câu nào không còn trong `speakers` (như AI-eCabinet, thẻ giọng lấy từ các câu) và
không bị đếm là giọng chưa gán tên; tên của nó vẫn giữ, nên chuyển một câu về người đó thì giọng hiện lại. Bản gỡ băng
tải về và dự thảo AI đọc tên theo giọng của từng câu. Mỗi lần chuyển ghi một dòng audit
`transcript.segment_speaker_assigned`. Chạy lại chuyển chữ thì các câu và giọng được viết lại từ đầu, các lần chuyển
câu mất theo.

## Cấu hình (`VoiceId:*`)

| Khoá | Mặc định | Ghi chú |
|---|---|---|
| `EncryptionKey` | rỗng | Base64 của 32 byte ngẫu nhiên. Rỗng (máy dev) thì dẫn xuất bằng HKDF từ `Authentication:SigningKey` với nhãn riêng. Server phải đặt khoá riêng. Đổi khoá thì giọng mẫu cũ không giải mã được — người dùng ghi lại |
| `SuggestThreshold` | `0.6` | Xem phần *Cách gợi ý* |
| `SuggestMargin` | `0.1` | |
| `SameVoiceThreshold` | `0.7` | Giọng thứ hai được gợi ý cùng một người phải giống giọng đầu tới mức này, xem *Một người bị tách làm hai* |
| `SecondVoiceThreshold` | `0.7` | Điểm tối thiểu so với giọng mẫu của giọng thứ hai trở đi của cùng một người |
| `InheritVoiceThreshold` | `0.8` | Giọng chưa có tên giống giọng đầu của một người từ mức này thì nhận người đó (ADR-0050) |
| `AutoAssignThreshold` | `0.75` | Từ điểm này máy tự gán tên, xem *Máy tự gán* |

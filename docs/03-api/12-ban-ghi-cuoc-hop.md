# 12 · Bản ghi cuộc họp và gỡ băng (SCRUM-29, SCRUM-31, SCRUM-44, SCRUM-109)

Thư ký lưu bản ghi âm của cuộc họp: ghi thẳng trên trình duyệt hoặc tải lên tệp ghi bằng thiết bị
khác. Bản ghi là đầu vào cho gỡ băng (SCRUM-31) và nhận diện người nói (SCRUM-35).

Bản ghi là **dữ liệu cá nhân** của mọi người phát biểu (Luật Bảo vệ dữ liệu cá nhân 91/2025/QH15,
hiệu lực 01/01/2026, và Nghị định 356/2025/NĐ-CP; trước đó là Nghị định 13/2023/NĐ-CP).

**Ghi âm cuộc họp không dựa vào sự đồng ý của từng người.** Luật không coi im lặng là đồng ý (Điều 9
khoản 4 điểm d), nên việc "không ai phản đối" không thay được chữ ký đồng ý của từng người. Căn cứ để
ghi âm là của đơn vị triển khai. Với cơ quan nhà nước, đó là phục vụ hoạt động của cơ quan (Điều 19
khoản 1 điểm c). Với tổ chức khác, đó là thoả thuận hoặc nội quy mà người lao động đã biết (Điều 19
khoản 1 điểm d, Điều 25 khoản 3). Dù căn cứ nào, người dự cũng phải được thông báo, chỉ được dùng
đúng mục đích, và phải xoá khi hết hạn (Điều 3 khoản 3, Điều 14). Hệ thống giữ ba nghĩa vụ đó bằng
dữ liệu, không chỉ bằng lời hứa trên tài liệu.

Nhận ra **ai** đang nói từ giọng (Voice ID) là chuyện khác: đó là dữ liệu sinh trắc học, cần sự đồng
ý riêng của từng người (xem [13 · Nhận diện giọng nói](13-nhan-dien-giong-noi.md)).

| Nghĩa vụ | Cột / cơ chế |
|---|---|
| Thông báo trước khi lưu | `consentConfirmed=true` bắt buộc: Thư ký xác nhận đã thông báo cho người dự và không ai phản đối. Thời điểm xác nhận lưu ở `ConsentObtainedAt`. Có người phản đối thì không ghi |
| Nêu mục đích | `consentPurpose` (mặc định *Lập biên bản cuộc họp*) lưu cùng bản ghi |
| Thời hạn lưu trữ và xoá | `RetainUntil` = lúc lưu + `Recordings:RetentionDays` (mặc định 180 ngày); job `RecordingRetentionWorker` xoá tự động mỗi 15 phút; Thư ký xoá theo yêu cầu bất cứ lúc nào |

Khi xoá, **tệp âm thanh và transcript** bị xoá; dòng `recordings` giữ lại với `Status = Deleted`
và `DeletedAt`, kèm một dòng nhật ký kiểm toán `recording.erased` (ai xoá, hay do hết hạn). Như vậy
biên bản vẫn nói được "đã có bản ghi và đã xoá ngày …", và yêu cầu xoá chứng minh được là đã làm.

## Quyền

| Ai | Xem danh sách, nghe | Lưu, xoá | Tải tệp gốc, xuất bản gỡ băng `.txt` |
|---|---|---|---|
| Thư ký hiện tại của cuộc họp | ✓ | ✓ | ✓ (trừ cuộc họp Mật: `403 MSG356`) |
| Chủ toạ hiện tại | ✓ | `403 MSG295` | ✓ (trừ cuộc họp Mật: `403 MSG356`) |
| Thành viên hiện tại | ✓ | `403 MSG295` | `403 MSG355` |
| Người tạo / người duyệt **không** có trong danh sách thành phần | `404` | `404` | `404` |
| Người ngoài cuộc họp | `404` | `404` | `404` |

Tải về và xuất chữ là đưa giọng nói, lời nói của người khác ra khỏi hệ thống, nên chỉ người điều hành biên
bản (Chủ toạ, Thư ký) làm được, như AI-eCabinet; cuộc họp Mật thì không ai tải được. Mỗi lần tải ghi nhật ký
kiểm toán (`recording.downloaded`, `transcript.exported`, `speech_clip.exported`), không ghi nội dung.

Người tạo và người duyệt đọc được cuộc họp nhưng không nghe được bản ghi: họ không có mặt trong
cuộc họp, mà bản ghi là giọng nói của người khác. Khách mời (Guest) chưa có quyền với bản ghi.

Chỉ lưu khi cuộc họp `InProgress` hoặc `Ended` (`409 MSG294`); xoá được ở mọi trạng thái.

## Endpoint

### `GET /api/meetings/{meetingId}/recordings`

```json
{
  "meetingId": "…",
  "canUpload": true,
  "items": [
    {
      "id": "…", "fileName": "phien-hop.webm", "contentType": "audio/webm",
      "byteSize": 1048576, "durationMs": 65000, "source": "BrowserCapture",
      "status": "Available", "createdAt": "…", "createdByName": "Nguyễn Văn A",
      "consentObtainedAt": "…", "consentPurpose": "Lập biên bản cuộc họp",
      "retainUntil": "…", "deletedAt": null, "canErase": true
    }
  ]
}
```

`status`: `Available` | `Deleted`. `source`: `Upload` | `BrowserCapture`. Mới nhất lên đầu.
`durationMs = 0` nghĩa là chưa biết (trình duyệt không gửi); gỡ băng sẽ điền giá trị thật.
Không trả đường dẫn lưu trữ.

### `POST /api/meetings/{meetingId}/recordings` — `multipart/form-data`

| Trường | Bắt buộc | Ghi chú |
|---|---|---|
| `file` | ✓ | Một tệp. `Content-Type` của phần tệp: `audio/mpeg`, `audio/mp4`, `audio/x-m4a`, `audio/aac`, `audio/wav`, `audio/webm`, `audio/ogg` (tham số codec như `;codecs=opus` được bỏ qua) |
| `consentConfirmed` | ✓ | Đúng chữ `true` — Thư ký xác nhận đã thông báo cho người dự và không ai phản đối |
| `consentPurpose` | | ≤ 1000 ký tự; bỏ trống thì dùng mục đích mặc định |
| `source` | | `Upload` (mặc định) hoặc `BrowserCapture` |
| `durationMs` | | Số nguyên ≥ 0, trình duyệt đo được |

Quyền và trạng thái cuộc họp được kiểm **trước** khi nhận byte đầu tiên. Tệp được ghi thẳng xuống
đĩa (không giữ cả form trong RAM), tên tệp trên đĩa là id mới, không dùng tên người dùng gửi. Bất cứ
lý do gì khiến bản ghi không được giữ (thiếu đồng ý, lỗi database…), tệp đã ghi bị xoá ngay.

`201 MSG290` `{ code, message, recordingId }`. Lỗi: `400 MSG291`, `415 MSG292`, `413 MSG293`,
`409 MSG294`, `403 MSG295`, `503 MSG298`.

### `GET /api/recordings/{recordingId}/playback`

Thẻ `<audio>` không gửi được bearer token, nên FE xin một **link ký sẵn** rồi gán vào `src`:

```json
{ "url": "/api/recordings/…/audio?expires=1761210000&signature=…", "expiresAt": "…" }
```

Link sống `Recordings:PlaybackUrlMinutes` (mặc định 120 phút), ký HMAC-SHA256 bằng khoá dẫn xuất
(HKDF) từ `Authentication:SigningKey` với nhãn riêng — không cần cấu hình thêm bí mật. Quyền nghe
được kiểm ở bước xin link.

### `GET /api/recordings/{recordingId}/audio?expires=…&signature=…`

Không cần đăng nhập; trả tệp với `Range` (tua được), `Cache-Control: private, no-store`. Sai chữ ký,
hết hạn, hoặc bản ghi đã xoá: `404` không body.

### `GET /api/recordings/{recordingId}/download`

Chủ toạ hoặc Thư ký xin link lưu tệp gốc, cùng dạng với `/playback` nhưng link có `&download=1` và
trả `Content-Disposition: attachment` với tên tệp gốc. Chữ ký của link tải **khác** link nghe (ký thêm
`.download`), nên thêm `&download=1` vào link nghe mà mọi người dự đều có chỉ nhận `404`. Ghi nhật ký
`recording.downloaded` lúc phát link. Danh sách bản ghi có `canDownload` cho từng bản.

### `DELETE /api/recordings/{recordingId}`

Thư ký xoá theo yêu cầu. `200 MSG296`; gọi lại trên bản đã xoá vẫn `200 MSG296`.

## Cấu hình (`Recordings:*`)

| Khoá | Mặc định | Ghi chú |
|---|---|---|
| `StorageRoot` | `App_Data/recordings` (tính từ content root) | Server test phải mount volume vào đây, nếu không redeploy là mất bản ghi |
| `RetentionDays` | `180` | Thời hạn lưu trữ ghi vào từng bản ghi lúc lưu |
| `MaxUploadMegabytes` | `300` | Ba giờ mp3, hoặc một giờ WAV 16 kHz |
| `PlaybackUrlMinutes` | `120` | |

## Gỡ băng (SCRUM-31)

Lưu bản ghi xong là bản ghi tự vào **hàng chờ chuyển thành chữ** (`Transcript.Status = Pending`).
`TranscriptionWorker` lấy từng bản ghi một (`FOR UPDATE SKIP LOCKED`), gửi âm thanh tới **worker
nhận dạng giọng nói tự chạy trong mạng nội bộ** (`workers/speech`, [ADR-0026](../05-decisions/0026-tu-chay-nhan-dang-giong-noi.md)),
rồi lưu từng đoạn kèm thời điểm và nhãn người nói. Âm thanh **không rời khỏi hệ thống**.

Trạng thái: `None` (bản ghi lưu trước khi có SCRUM-31) → `Pending` → `Processing` → `Completed`
hoặc `Failed` (kèm `errorMessage` viết cho Thư ký đọc). App khởi động lại giữa chừng thì bản đang
`Processing` quay về `Pending`. Bản ghi bị xoá trong lúc đang chạy thì kết quả bị bỏ, không ghi lại.

Danh sách bản ghi trả thêm `transcriptStatus` cho mỗi dòng.

### `GET /api/recordings/{recordingId}/transcript`

```json
{
  "recordingId": "…", "status": "Completed",
  "provider": "self-hosted:gipformer-1.5-68m-int8@b528768939c7",
  "language": "vi", "errorMessage": "", "completedAt": "…", "durationMs": 23380, "canEdit": true,
  "segments": [
    { "id": "…", "position": 1, "startMs": 0, "endMs": 5800, "text": "Kính thưa các đồng chí, …",
      "speakerLabel": "SPEAKER_00", "editedAt": null, "editedByName": null }
  ]
}
```

`speakerLabel` là nhãn trong **riêng bản ghi này** (`SPEAKER_00` → FE hiện *Người nói 1*); gán tên
thật là việc của SCRUM-35. Không có vector giọng nói nào trong response hay trong database.

### `POST /api/recordings/{recordingId}/transcribe`

Thư ký chạy lần đầu cho bản ghi `None`, chạy lại khi lần trước lỗi, hoặc **làm lại bản đã xong** (model
mới, có người vừa đăng ký giọng). `202 MSG300`; đang chờ hoặc đang chạy: `409 MSG301`. Làm lại bản đã xong
thay toàn bộ đoạn, chữ đã sửa và tên đã gán khi chạy xong (nhãn người nói của lần chạy mới không khớp lần
cũ), nên FE hỏi lại trước; ghi nhật ký `transcript.rerun`.

### `GET /api/recordings/{recordingId}/transcript.txt`

Chủ toạ hoặc Thư ký lưu bản gỡ băng đã xong thành tệp `.txt` (UTF-8): đầu tệp ghi tên cuộc họp, nguồn, giờ
bắt đầu, giờ xuất và lời nhắc chữ do máy viết; rồi mỗi đoạn một dòng `[HH:MM:SS] Tên: chữ`, giờ tính từ đầu
bản ghi, tên là tên Thư ký đã gán hoặc *Người nói N*. Chưa xong: `409 MSG357`. Response `transcript` có
`canExport`.

### `PUT /api/transcript-segments/{segmentId}` — `{ "text": "…" }`

Thư ký sửa chữ máy nhận sai. Cắt khoảng trắng hai đầu, 1–8000 ký tự (`400 MSG303`). Lưu `EditedBy`,
`EditedAt` để phân biệt chữ người sửa với chữ máy, và ghi nhật ký `transcript.segment_edited` (không ghi chữ). `200 MSG302` `{ code, message, segment: { segmentId, text, editedAt } }`.

### Cấu hình (`Speech:*`)

| Khoá | Mặc định | Ghi chú |
|---|---|---|
| `BaseUrl` | rỗng (Development: `http://localhost:8090`) | Rỗng thì app vẫn chạy, bản gỡ băng báo *Máy chủ nhận dạng giọng nói chưa được cấu hình.* |
| `ApiKey` | rỗng | Khoá dùng chung với worker (`SPEECH_API_KEY`) |
| `TimeoutMinutes` | `120` | |

## Ghi âm theo micro từng người (SCRUM-109)

Cuộc họp có **cách ghi âm** (`captureMode`), cùng hai lựa chọn như AI-eCabinet:

| `captureMode` | Cách thu | Lưu gì | Tên người nói |
|---|---|---|---|
| `PerMic` (mặc định) | Trình duyệt của mỗi người dự tự thu micro của chính họ, cắt theo câu khi ngừng nói (theo mức âm lượng, không dùng WebAssembly) và gửi từng câu lên | Mỗi câu: chữ, lúc bắt đầu, thời lượng, chủ micro, mỗi quãng nói trong câu (thời gian, số từ, độ to), và âm thanh của câu để nghe lại ([ADR-0039](../05-decisions/0039-luu-am-thanh-tung-cau-de-nghe-lai.md)) | Chủ micro |
| `Room` | Thư ký tải tệp lên hoặc ghi bằng trình duyệt (các mục trên) | Bản ghi và bản gỡ băng | Thư ký gán, có gợi ý Voice ID |

- Chọn ở form tạo/sửa cuộc họp (`captureMode` trong body của `POST`/`PUT /api/meetings`, bỏ trống thì
  giữ nguyên, khi tạo là `PerMic`), hoặc ở tab Bản ghi bằng `PUT …/capture-mode`. Cuộc họp đã công bố
  chỉ đổi được bằng `PUT …/capture-mode` (form dời lịch trả lỗi trường `captureMode`), để không ai nhận
  thông báo "dời lịch". **Khoá khi cuộc họp đã bắt đầu**: người dự phải biết trước micro mình có được gỡ
  băng không, và một bản gỡ băng không thể nửa theo micro, nửa thu chung.
- Không ai bật micro của người khác được, kể cả Chủ tọa (khác eCab). Mỗi người thấy mình đang được ghi
  và tự tắt được; người nói rút lại được câu của mình.
- Backend gửi âm thanh một câu cho worker (`diarize=false&profile=clip`, tối đa 2 câu cùng lúc trên cả server).
  Câu có chữ thì giữ cả âm thanh vào kho bản ghi để nghe lại (ADR-0039); câu toàn im lặng không tạo dòng nào và
  không lưu gì. Hồ sơ `clip`
  chỉ tăng âm tối đa 6 dB (thu cả phòng: 30 dB) và lọc khoảng lặng chặt hơn (VAD 0,5), vì chỗ nhỏ tiếng
  trên micro riêng phần lớn là tiếng người ngồi cạnh lọt vào.
- **Câu bị lọt tiếng được đánh dấu, không bị ẩn** (như AI-eCabinet, đo ở `workers/speech/eval/bleed.py`).
  Họp trực tiếp, laptop nào cũng nghe người ngồi cạnh: cùng một câu đến hai lần, to ở micro người nói, nhỏ ở
  micro bên cạnh. Một quãng của câu bị đánh dấu khi câu của người khác, ở độ lệch đồng hồ tốt nhất trong
  ±2 giây, phủ ≥ 60 % quãng đó, chứa ≥ 50 % số từ (≤ 3 từ lạ), và to hơn ≥ 6 dB. Đo trên cuộc họp tổng hợp
  từ VIVOS: từ trùng 114 % → 36 %, không quãng nào của chủ micro bị đánh dấu nhầm. Hai bản to gần bằng nhau
  (hai laptop cách đều người nói) thì không đoán được bản nào là thật nên không đánh dấu. Câu đã sửa tay
  không được so. Chủ tọa hoặc Thư ký quyết định **loại câu khỏi bản ghi**.
- Trình duyệt giữ câu chưa gửi được trong IndexedDB (mất mạng, đóng tab, máy chuyển chữ tắt) và gửi tiếp
  khi người đó mở lại cuộc họp. Backend nhận câu như thế **đến 7 ngày sau khi cuộc họp kết thúc**
  (`SpeechClipRules.LateUpload`), miễn là câu được nói trong giờ họp.
- Chữ và âm thanh của các câu là dữ liệu cá nhân, xoá theo cùng hạn với bản ghi (`Recordings:RetentionDays`).
  Xoá một câu thì xoá luôn âm thanh của nó.

Quyền: đọc, nghe, gửi câu và xoá câu của mình là người trong danh sách cuộc họp (như nghe bản ghi); sửa chữ
là Thư ký; xoá câu của người khác là Thư ký; loại câu khỏi bản ghi và đưa lại là Chủ tọa hoặc Thư ký; đổi
cách ghi âm là Chủ tọa hoặc Thư ký. Người ngoài danh sách nhận `404`.

### `GET /api/meetings/{meetingId}/speech-clips[?since=…]`

```json
{
  "meetingId": "…", "captureMode": "PerMic", "canCapture": true, "canEdit": false, "canChangeMode": false,
  "canExclude": false, "canExport": false, "retentionDays": 180, "readAt": "2026-10-26T08:01:02.123Z",
  "items": [
    { "id": "…", "speakerId": "…", "speakerName": "Lê Thành Viên", "startedAt": "2026-10-26T08:00:57Z",
      "durationMs": 2400, "text": "Tôi đồng ý với dự thảo.", "updatedAt": "…", "editedAt": null,
      "editedByName": null, "canDelete": true, "excluded": false, "exclusionReason": null, "excludedByName": null,
      "leak": { "clipId": "…", "speakerId": "…", "speakerName": "Nguyễn Chủ Toạ", "whole": true, "words": [[0, 5]] },
      "hasAudio": true, "peaks": "AAC0tLQ=" }
  ]
}
```

- `leak`: câu (`whole: true`) hoặc các từ `[từ, đến)` của nó có lẽ là lời của `speakerName` lọt sang, từ câu
  `clipId`. Mọi người đều thấy dấu này. `null` khi không lọt, hoặc câu đã bị loại.
- `excluded`: câu đã bị loại khỏi bản ghi. Chủ tọa và Thư ký vẫn nhận chữ, lý do (`Leak` | `Other`) và
  người loại; người khác nhận `text: ""` để biết ở đó từng có một câu.
- `hasAudio`: người gọi nghe được câu này (`GET /api/speech-clips/{id}/playback`). `false` với câu lưu trước
  ADR-0039, câu mà ổ đĩa không ghi được âm thanh, và câu đã bị loại khi người gọi không phải Chủ tọa hay Thư ký.
- `peaks`: dạng sóng, base64, mỗi 100 ms một byte 0–255 (thang căn bậc hai của biên độ đỉnh). `null` khi âm thanh
  không phải WAV PCM 16-bit, hoặc như `hasAudio`.

`retentionDays` là số ngày chữ và âm thanh của câu được giữ; ô micro nói con số này trước khi người dự bật
micro. Xếp theo `startedAt`. `canCapture` là micro **của chính người gọi** được gửi câu lúc này (cuộc họp
`InProgress` và `PerMic`). `since` = `readAt` của lần đọc trước: trả câu mới hoặc vừa sửa, **cùng các câu
gần nó trong khoảng ±2 giây**, vì dấu lọt tiếng của một câu cũ có thể đổi khi bản to hơn đến sau hoặc bị
loại. Câu bị xoá thì đọc lại từ đầu (sự kiện `speechChanged` có `changeType: "ClipDeleted"`).

### `PUT /api/meetings/{meetingId}/speech-clips/{clientClipId}?startedAt=…&durationMs=…`

Thân là âm thanh của **một câu** (`audio/wav`, `audio/webm`, `audio/ogg`, tối đa 3 MB, 1 ms–60 giây).
`clientClipId` do trình duyệt sinh; gửi lại cùng id (mất phản hồi, mạng chập chờn) trả lại câu đã lưu,
không gỡ băng lần hai. `startedAt` theo đồng hồ trình duyệt; lệch quá 2 phút hoặc trước giờ họp thì lấy
giờ server; gửi sau khi họp xong thì câu kết thúc muộn nhất lúc cuộc họp kết thúc. `200 MSG318` kèm
`clip`, `204` nếu toàn im lặng, `503 MSG312` khi worker chưa sẵn sàng (trình duyệt giữ câu và gửi lại),
`409 MSG320` khi cuộc họp không ghi theo micro, chưa bắt đầu, hoặc đã kết thúc quá 7 ngày.

### `GET /api/speech-clips/{clipId}/playback`

Link nghe một câu, như `GET /api/recordings/{id}/playback`: `200 { "url": "/api/speech-clips/{id}/audio?expires=…&signature=…",
"expiresAt": "…" }`. `404` khi không có câu, người gọi không đọc được cuộc họp, câu không có âm thanh, hoặc câu đã bị
loại mà người gọi không phải Chủ tọa hay Thư ký. `500 MSG297` khi lỗi database.

`GET /api/speech-clips/{clipId}/audio?expires=…&signature=…` không cần token. Nó phát âm thanh đúng định dạng trình
duyệt đã gửi, hỗ trợ `Range` và `Cache-Control: private, no-store`. Link sai, hết hạn hoặc câu đã xoá đều trả `404`.
Không có link tải về.

### `PUT /api/speech-clips/{clipId}/text` — `{ "text": "…" }`

Thư ký sửa chữ một câu, như sửa đoạn gỡ băng (`MSG302`).

### `DELETE /api/speech-clips/{clipId}`

Người nói rút lại câu của mình, hoặc Thư ký xoá (`MSG322`). Xoá hẳn cả chữ lẫn âm thanh, ghi nhật ký kiểm toán.

### `PUT /api/speech-clips/{clipId}/exclusion` — `{ "excluded": true, "reason": "Leak" | "Other" }`

Chủ tọa hoặc Thư ký loại câu khỏi bản ghi (`200 MSG351`), hoặc đưa lại với `{ "excluded": false }`
(`200 MSG352`). Không xoá gì: câu vẫn nằm đó, chỉ ra khỏi bản ghi. Bấm lại khi câu đã ở trạng thái đó thì
không đổi và không ghi nhật ký. Người khác nhận `403 MSG353`; `reason` sai `400 MSG354`. Ghi nhật ký kiểm
toán `speech_clip.excluded` / `speech_clip.restored` kèm lý do.

### `GET /api/meetings/{meetingId}/speech-clips.txt`

Chủ toạ hoặc Thư ký lưu bản gỡ băng theo micro thành `.txt`, cùng dạng như trên; giờ mỗi dòng là giờ đồng
hồ trong phòng (UTC+7). **Câu đã loại khỏi bản ghi không có trong tệp**; câu mới chỉ bị đánh dấu lọt tiếng
thì vẫn có. `403 MSG355` / `MSG356` như tải bản ghi. Danh sách câu có `canExport`.

### `PUT /api/meetings/{meetingId}/capture-mode` — `{ "mode": "PerMic" | "Room" }`

Chủ tọa hoặc Thư ký, trước khi họp (`MSG324`; đã bắt đầu thì `409 MSG325`, cuộc họp đã huỷ thì `409 MSG349`).

### Sự kiện hub `speechChanged`

`{ eventId, meetingId, changeType: "ClipAdded" | "ClipEdited" | "ClipDeleted" | "CaptureModeChanged", occurredAt }`,
gửi cho người trong danh sách cuộc họp. Không mang chữ: ai nhận thì đọc lại.

## Ghi cả cuộc gọi (SCRUM-44)

Cuộc họp thu chung cả phòng thì trong phòng họp, panel **Bản ghi** thay panel Lời nói. Nút **Ghi âm** ở đó
thu micro của Thư ký cùng tiếng mọi người trong cuộc gọi, rồi lưu bằng `POST …/recordings` như trên
(`source = BrowserCapture`). Không có endpoint mới. Lý do và giới hạn: [ADR-0030](../05-decisions/0030-ghi-ca-cuoc-goi-tu-may-thu-ky.md).

Mọi người trong phòng thấy chip **Đang ghi âm cuộc họp**. Thư ký phát tín hiệu trên kênh dữ liệu
của LiveKit, không qua backend:

| Topic | Payload | Gửi khi | Người nhận |
|---|---|---|---|
| `recording` | `{ "recording": true }` | Mỗi 4 giây trong lúc ghi | Chỉ tin người có vai trò Thư ký; tắt chip sau 10 giây không nghe nhịp nào |
| `recording` | `{ "recording": false }` | Dừng, tạm dừng hoặc rời phòng | Tắt chip ngay |

## Chưa làm trong SCRUM-29

- Banner "Đang ghi âm" cho mọi người dự khi Thư ký ghi trên **trang chi tiết** (họp trực tiếp): chỉ
  trình duyệt của Thư ký hiện trạng thái ghi. Trong phòng họp trực tuyến thì đã có (SCRUM-44, ở trên).
- Quyền nghe của khách mời (chờ SCRUM-59).

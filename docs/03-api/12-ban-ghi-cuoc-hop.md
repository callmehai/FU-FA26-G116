# 12 · Bản ghi cuộc họp và gỡ băng (SCRUM-29, SCRUM-31)

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

| Ai | Xem danh sách, nghe | Lưu, xoá |
|---|---|---|
| Thư ký hiện tại của cuộc họp | ✓ | ✓ |
| Chủ toạ, thành viên hiện tại | ✓ | `403 MSG295` |
| Người tạo / người duyệt **không** có trong danh sách thành phần | `404` | `404` |
| Người ngoài cuộc họp | `404` | `404` |

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

Thư ký chạy lại khi lần trước lỗi, hoặc chạy lần đầu cho bản ghi `None`. `202 MSG300`; đang chờ,
đang chạy hoặc đã xong: `409 MSG301`.

### `PUT /api/transcript-segments/{segmentId}` — `{ "text": "…" }`

Thư ký sửa chữ máy nhận sai. Cắt khoảng trắng hai đầu, 1–8000 ký tự (`400 MSG303`). Lưu `EditedBy`,
`EditedAt` để phân biệt chữ người sửa với chữ máy. `200 MSG302` `{ code, message, segment: { segmentId, text, editedAt } }`.

### Cấu hình (`Speech:*`)

| Khoá | Mặc định | Ghi chú |
|---|---|---|
| `BaseUrl` | rỗng (Development: `http://localhost:8090`) | Rỗng thì app vẫn chạy, bản gỡ băng báo *Máy chủ nhận dạng giọng nói chưa được cấu hình.* |
| `ApiKey` | rỗng | Khoá dùng chung với worker (`SPEECH_API_KEY`) |
| `TimeoutMinutes` | `120` | |

## Chưa làm trong SCRUM-29

- Banner "Đang ghi âm" phát cho **mọi** người dự qua hub (tiêu chí nghiệm thu của ticket): hiện
  chỉ trình duyệt của Thư ký hiện trạng thái ghi. Ghi chú trong Jira.
- Quyền nghe của khách mời (chờ SCRUM-59).

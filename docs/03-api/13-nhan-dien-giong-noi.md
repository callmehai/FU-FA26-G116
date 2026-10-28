# 13 · Nhận diện người nói — Voice ID (SCRUM-35)

Bản gỡ băng (SCRUM-31) chia lời nói theo *Người nói 1, 2, 3…*: đó là **tách người nói**
(*diarization*), chỉ biết hai đoạn là cùng hay khác giọng, không biết là ai. Voice ID trả lời câu
"ai": mỗi người **tự nguyện** đăng ký giọng mẫu; sau khi gỡ băng, hệ thống so từng giọng trong bản ghi
với giọng mẫu của **những người có trong cuộc họp và đã đồng ý**, rồi **gợi ý** tên. Chỉ Thư ký quyết
định tên — hệ thống không tự gán. Quyết định thiết kế: [ADR-0027](../05-decisions/0027-voice-id-co-dong-y.md).

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
| Đồng ý riêng, rõ ràng, do chính người đó | `voice_consents`: một lần đồng ý riêng, không gộp với đồng ý ghi âm; lưu phiên bản thông báo (`noticeVersion`) và mục đích người đó đã đọc. Không ai đồng ý thay người khác được — không có route nào nhận id người khác |
| Nêu mục đích | `Purpose`: *Gợi ý tên người phát biểu trong bản gỡ băng của các cuộc họp tôi tham dự, để Thư ký lập biên bản.* |
| Thông báo đủ trước khi đồng ý | Màn "Giọng nói của tôi" nêu mục đích, thứ được lưu, phạm vi dùng, quyền của người dùng, **bên kiểm soát dữ liệu** (`controller`, lấy từ cấu hình `VoiceId:Controller`; mỗi đơn vị triển khai đặt tên pháp nhân của mình), và ghi rõ đây là dữ liệu cá nhân nhạy cảm. Ô đồng ý không đánh dấu sẵn |
| Thu thập tối thiểu | Chỉ lưu vector đặc trưng giọng (512 số), **không lưu âm thanh mẫu**. Vector của các giọng trong bản ghi họp chỉ dùng để so một lần rồi bỏ, không lưu |
| Bảo mật (Luật, Điều 31 khoản 4) | Vector mã hoá AES-256-GCM; dữ liệu kèm (AAD) gắn với người, id mẫu và mô hình — chép sang người khác là không giải mã được. Chỉ backend giải mã, và không endpoint nào trả vector ra ngoài. Mọi lần đọc ghi đều có nhật ký kiểm toán |
| Rút đồng ý, xoá | `DELETE /api/me/voice/consent`: trong một transaction, xoá **mọi** giọng mẫu và **mọi gợi ý** trỏ tới người đó; tên Thư ký đã gán giữ nguyên (đó là quyết định của người, không phải dữ liệu sinh trắc) |
| Giới hạn phạm vi dùng | Chỉ so với người có trong danh sách thành phần hiện tại của **chính cuộc họp đó**; người ngoài cuộc họp có giọng giống đến đâu cũng không được gợi ý |
| Bằng chứng | Nhật ký kiểm toán: `voice.consent_granted`, `voice.consent_withdrawn` (kèm số mẫu đã xoá), `voice.sample_added`, `voice.sample_deleted`, `transcript.speaker_assigned` (kèm gợi ý của máy và Thư ký có chấp nhận không) |

## Voice ID của chính mình (`/api/me/voice`)

### `GET /api/me/voice`

```json
{
  "consent": { "grantedAt": "…", "noticeVersion": "1.0", "purpose": "Gợi ý tên người phát biểu …" },
  "samples": [ { "id": "…", "createdAt": "…", "speechMs": 7200 } ],
  "maxSamples": 5, "noticeVersion": "1.0", "purpose": "Gợi ý tên người phát biểu …",
  "controller": "Nhóm đồ án SEP490-G116, đơn vị vận hành bản thử nghiệm TrustCabinet"
}
```

`consent` là `null` khi chưa đồng ý hoặc đã rút. `noticeVersion`, `purpose` và `controller` ở gốc là
thông báo hiện hành mà FE hiện cho người dùng đọc trước khi đồng ý.

### `POST /api/me/voice/consent` — `{ "noticeVersion": "1.0" }`

Phải gửi đúng phiên bản thông báo đang hiện (khác → `400 MSG313`, `errors.noticeVersion`), để đồng ý
luôn gắn với câu chữ người đó đã đọc. Đã đồng ý rồi thì không tạo thêm. `200 MSG305`, `data` là hồ sơ
như `GET`.

### `DELETE /api/me/voice/consent`

`200 MSG306`. Đồng ý lại sau đó tạo **dòng đồng ý mới**; dòng đã rút giữ nguyên làm bằng chứng.

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

`201 MSG307`, `data`: `{ id, createdAt, speechMs }`. Tối đa 5 mẫu (`409 MSG310`); chưa đồng ý
`409 MSG308`; worker không chạy `503 MSG312`.

### `DELETE /api/me/voice/samples/{sampleId}`

`200 MSG311`. Mẫu của người khác: `404`.

## Gợi ý và gán tên trong bản gỡ băng

`GET /api/recordings/{id}/transcript` (xem [12](12-ban-ghi-cuoc-hop.md)) trả thêm:

```json
"speakers": [
  { "label": "SPEAKER_00", "speechMs": 30000,
    "suggestedUserId": "…", "suggestedName": "Nguyễn Thư Ký", "suggestionScore": 0.83,
    "assignedUserId": null, "assignedName": null }
],
"people": [ { "id": "…", "fullName": "…" } ]
```

`people` là thành phần hiện tại của cuộc họp, chỉ trả cho Thư ký (để chọn tên); người khác nhận mảng
rỗng. **`suggestedUserId`, `suggestedName`, `suggestionScore` cũng chỉ trả cho Thư ký**: đó là phỏng đoán
của máy về danh tính sinh trắc học của một người, để Thư ký quyết định, không phải thông tin cho cả cuộc
họp; người khác nhận `null`. Tên hiển thị của một giọng: `assignedName` nếu Thư ký đã gán, nếu không thì
*Người nói N*.

**Cách gợi ý** (`SpeakerMatcher`): điểm của một người với một giọng là cosine cao nhất giữa vector giọng
và các giọng mẫu của người đó. Chỉ gợi ý khi điểm ≥ `VoiceId:SuggestThreshold` (0,6) **và** hơn người
xếp sau ít nhất `VoiceId:SuggestMargin` (0,1). Mỗi người được gợi ý cho tối đa một giọng trong một bản
ghi. Ngưỡng 0,6 đo trên 24 cuộc họp giả lập bằng giọng thật (VIVOS, sạch và qua phòng họp, xem
`workers/speech/eval/`). Ở 0,5, 3% thời gian nói bị gợi ý sai người, có cả người chưa đăng ký bị gợi ý
tên. Ở 0,6, không gợi ý sai lần nào, và 97–98% lời của người đã đăng ký vẫn được gợi ý đúng tên.

### `PUT /api/recordings/{recordingId}/speakers/{label}` — `{ "userId": "…" | null }`

Thư ký gán tên thật (một lần áp dụng cho mọi đoạn của giọng đó), hoặc bỏ gán bằng `null`. Người được
gán phải là thành phần hiện tại của cuộc họp (`400 MSG317`). Không phải Thư ký: `403 MSG295`. Nhãn
không có trong bản ghi: `404`. `200 MSG316` `{ code, message, speaker }`.

## Cấu hình (`VoiceId:*`)

| Khoá | Mặc định | Ghi chú |
|---|---|---|
| `EncryptionKey` | rỗng | Base64 của 32 byte ngẫu nhiên. Rỗng (máy dev) thì dẫn xuất bằng HKDF từ `Authentication:SigningKey` với nhãn riêng. Server phải đặt khoá riêng. Đổi khoá thì giọng mẫu cũ không giải mã được — người dùng ghi lại |
| `SuggestThreshold` | `0.6` | Xem phần *Cách gợi ý* |
| `SuggestMargin` | `0.1` | |

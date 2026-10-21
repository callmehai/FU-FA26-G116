# Kế hoạch triển khai: phòng họp trực tuyến (SCRUM-39 … SCRUM-44)

- **Trạng thái:** Kế hoạch — chưa có dòng code nào. Viết ngày 2026-09-30, trên `main` = `0cefb6c`.
- **Phụ trách:** Hải Trần Việt (BE + FE).
- **Dùng file này thế nào:** đây là bản hướng dẫn để code theo, cho cả người lẫn AI agent. Đọc
  §1 → §3 trước khi viết dòng đầu tiên. Code theo từng PR ở §8, tick checklist của PR đó. Gặp
  chỗ plan sai với code thật thì **code thật thắng** — sửa plan trong cùng PR và ghi vào Jira.
- **Không phải tài liệu BA.** UC Specification, sequence diagram và business rule do BA chốt ở
  `BA Final Review`, dựa trên code và các ghi chú Jira ở §11.
- **Quyết định chính thức** nằm ở hai ADR:
  - [ADR-0010](../05-decisions/0010-phong-hop-truc-tuyen-dung-livekit-sfu.md): LiveKit;
  - [ADR-0011](../05-decisions/0011-tinh-nang-hop-la-panel-dung-chung.md): panel dùng chung.

  File này là cách làm. Hai bên lệch nhau thì ADR thắng.

Mọi tên API, config key và phiên bản của LiveKit trong file này đã được kiểm tra với nguồn gốc
(docs.livekit.io, mã nguồn livekit/livekit, livekit/protocol, client-sdk-js v2.22.3,
pabloFuente/livekit-server-sdk-dotnet) và **chạy thử thật** trên máy Mac: livekit-server v1.13.7
trong Docker Desktop, .NET SDK 1.2.3 trên net10.0, livekit-client 2.22.3 trong Chrome với hai người
cùng vào một phòng. Những chỗ chưa chạy thử được đánh dấu **CHƯA KIỂM**.

---

## 1. Quyết định

D1–D6 và D11–D13 được ghi thành ADR-0010; D10 thành ADR-0011. D7–D9 là thay đổi scope, chờ nhóm
quyết.

| # | Quyết định | Trạng thái |
|---|---|---|
| D1 | Media đi qua **SFU LiveKit tự host** (không mesh P2P). Report 2 rủi ro R01 cũng chọn "proven open-source media server rather than raw P2P". | Đã chốt với Hải |
| D2 | **TURN dùng bản nhúng sẵn trong LiveKit**, không dựng coturn. Dev không cần TURN (cùng máy); prod cần domain + chứng chỉ TLS. | Đã chốt (thay cho lựa chọn coturn ban đầu) |
| D3 | **Phòng họp không dùng SignalR.** LiveKit tự lo signalling (SDP/ICE) và trạng thái phòng. SignalR `/hubs/calls` chỉ cần nếu làm gọi 1–1 (§8 PR5). | Đã chốt — ADR-0010 |
| D4 | **Backend là ranh giới bảo mật:** chỉ backend ký token LiveKit, sau khi kiểm quyền. Tắt mic người khác, luật một người chia sẻ màn hình, đá người mất quyền đều do server làm qua RoomService API. | Đã chốt |
| D5 | Tên phòng LiveKit = `meeting-{meetingId:D}`, một phòng cho mỗi cuộc họp. Phòng tự tạo khi người đầu tiên vào. `CallSession` (lượt mở phòng, không phải một buổi họp) được tạo từ webhook, không tạo lúc cấp token. | Đã chốt — ADR-0010 |
| D6 | Phòng chỉ mở khi cuộc họp **`InProgress`** **và** `Mode` là `Online` hoặc `Hybrid` (UC-37). Chỉ người có phân công hiện hành trong `meeting_participants`, hoặc Guest có guest session hợp lệ (UC-04), được vào. Khi cuộc họp `Ended`, phòng ngừng nhận người và ngắt kết nối mọi người (BR-143). Không còn Meeting Session ([ADR-0013](../05-decisions/0013-mot-cuoc-hop-la-mot-buoi-hop.md)) nên room code kiểm `Meeting.Status = InProgress` là đúng mô hình đích. | Đã chốt — ADR-0010, ADR-0013 |
| D7 | **Chat trong phòng là tạm thời:** LiveKit text stream topic `lk.chat`, không lưu database, người vào sau không thấy tin cũ (giống Google Meet). Report 1 FE-08 có chat (UC-42); Jira chưa có ticket. | Chờ nhóm — đề xuất gộp vào SCRUM-41 |
| D8 | **Gọi 1–1 (SCRUM-40):** Report 1/2 không có; Jira/README để Must. Không làm chat 1–1. | **Chờ nhóm quyết** — plan để thành PR5 tuỳ chọn |
| D9 | **SCRUM-44 (ghi cuộc gọi)** mâu thuẫn Report 1 LI-01 ("no recording bot joins the meeting room… excluded, not merely deferred"). Không làm Egress. Hướng hợp LI-01: thư ký bấm ghi âm (SCRUM-29) trong phòng, trình duyệt thư ký trộn audio phòng và ghi trên máy mình. | **Chờ nhóm quyết** — ngoài plan này |
| D10 | **Tính năng họp (vote, nghị sự, phát biểu, tài liệu) nằm trong phòng dưới dạng panel**, mỗi tính năng code một lần, màn chi tiết bên ngoài và phòng họp cùng dùng (§7). Agenda tách planning context (cuộc họp chưa `InProgress`) và live context (cuộc họp `InProgress`). | Đã chốt — ADR-0011 |
| D11 | URL LiveKit do backend trả kèm token (`LiveKit:PublicUrl`), không có biến `VITE_*`, không hardcode localhost trong FE. | Đã chốt |
| D12 | Backend **vẫn khởi động được khi chưa cấu hình LiveKit**; các endpoint gọi trả `503`. Đồng đội làm tính năng khác không phải dựng LiveKit. | Đã chốt |
| D13 | FE dùng `livekit-client` + **hooks của `@livekit/components-react`**, không dùng gói style của họ; giao diện tự vẽ theo `frontend-ui-design`. | Đề xuất — ghi vào ADR-0010 |

---

## 2. Kiến thức nền (đọc 30 phút trước khi code)

- **WebRTC**: trình duyệt gửi âm thanh/hình trực tiếp. Hai bên phải trao đổi SDP ("danh thiếp") và
  ICE candidate (các đường có thể đi) qua một kênh trung gian — gọi là **signalling**. Với LiveKit,
  signalling đi qua WebSocket tới LiveKit server (`ws://…:7880/rtc`), ta không tự viết.
- **NAT/STUN/TURN/ICE**: máy sau NAT không nhận kết nối vào được. STUN giúp biết địa chỉ public,
  TURN là trạm chuyển tiếp khi bị chặn, ICE thử lần lượt các đường.
- **SFU**: mỗi người gửi một luồng lên server, server chuyển tiếp cho người cần. Với **simulcast**
  mỗi người gửi 3 độ phân giải; **dynacast** tắt lớp không ai xem; **adaptive stream** chỉ nhận đúng
  cỡ ô đang hiển thị, ô bị ẩn thì tạm dừng. Nhờ vậy phòng 20–50 người vẫn nhẹ.
- **Token LiveKit**: JWT HS256 ký bằng API secret, chứa `identity`, tên phòng và quyền (grants).
  LiveKit chỉ cho vào phòng khi token hợp lệ → ai ký token người đó quyết định ai vào phòng.
- **Webhook**: LiveKit gọi ngược về backend khi có người vào/ra, phòng đóng, track được publish.
  **Không đảm bảo tới nơi** (có thể mất khi hàng đợi quá 30 giây/200 sự kiện) → phải có job đối
  soát định kỳ (§5.8).

Đọc thêm: [WebRTC là gì (TopDev)](https://topdev.vn/blog/webrtc-la-gi/) ·
[NAT, STUN, TURN, ICE (Viblo)](https://viblo.asia/p/webrtc-basic-phan-1-tim-hieu-ve-nat-stun-turn-vs-ice-MJykjWlDvPB) ·
[Media Server (Viblo)](https://viblo.asia/p/gioi-thieu-ve-webrtc-va-huong-tiep-can-media-server-maGK7k3MKj2) ·
[Mesh/MCU/SFU (DEV)](https://dev.to/christosalexiou/the-multiple-faces-of-webrtc-n-peer-calling-mesh-mcu-and-sfu-39dg) ·
[LiveKit self-hosting](https://docs.livekit.io/transport/self-hosting/) ·
[Webhooks](https://docs.livekit.io/intro/basics/rooms-participants-tracks/webhooks-events/) ·
[Text streams](https://docs.livekit.io/transport/data/text-streams.md)

---

## 3. Kiến trúc

```
                      (1) GET  /api/meetings/{id}/call          ┌──────────────────────┐
 ┌──────────────┐     (2) POST /api/meetings/{id}/call/join ───▶│  ASP.NET Core        │
 │ Trình duyệt  │◀──── token + serverUrl ─────────────────────── │  CallRoomService     │
 │ React +      │     (5) POST …/participants/{uid}/mute ──────▶ │  kiểm quyền, ký token│
 │ livekit-     │                                                │                      │
 │ client       │                                                │  RoomService API ───┐│
 └──────┬───────┘                                                │  (mute, remove,     ││
        │ (3) WebSocket /rtc + media UDP 7882 / TCP 7881         │   delete, list)     ││
        ▼                                                        └──────▲──────────────┘│
 ┌──────────────────────┐  (4) webhook POST /api/livekit/webhook ──────┘  ▲            │
 │ LiveKit server (SFU) │  participant_joined/left, room_finished,         │            │
 │ + TURN nhúng sẵn     │◀─────────────────────────────────────────────────┴────────────┘
 └──────────────────────┘        (6) CallRoomJanitor mỗi 30 giây: đối soát
```

### 3.1 Luồng vào phòng

1. FE mở `/mgmt/meetings/{id}/room` → `GET /api/meetings/{id}/call` lấy trạng thái phòng, vai trò
   của mình và số người đang trong phòng.
2. Màn chờ: xem trước camera, chọn thiết bị, bật/tắt mic/cam.
3. Bấm "Vào phòng" → `POST /api/meetings/{id}/call/join`. Token lấy **lúc bấm**, không lấy lúc mở
   trang (token sống 10 phút; StrictMode chạy effect hai lần).
4. FE `room.connect(serverUrl, token)` qua `<LiveKitRoom>`.
5. LiveKit gửi webhook `participant_joined` → backend tạo `CallSession` nếu phòng chưa có phiên
   `Active`, thêm dòng `call_participants`.

### 3.2 Luồng rời phòng / phòng đóng

- `participant_left` / `participant_connection_aborted` → đóng dòng `call_participants` đang mở
  (`LeftAt`).
- `room_finished` → `CallSession.Status = Ended`, `EndedAt`, đóng mọi dòng đang mở.
- Janitor: cuộc họp không còn `InProgress` → `DeleteRoom` (mọi người nhận `ROOM_DELETED`) và đóng
  phiên. Người bị vô hiệu hoá hoặc bị gỡ khỏi thành phần tham dự → `RemoveParticipant`.

### 3.3 Chủ toạ tắt mic người khác

`POST /api/meetings/{id}/call/participants/{userId}/mute` → server kiểm người gọi là `Chair` của
cuộc họp → `ListParticipants` tìm track `Microphone` của người đó → `MutePublishedTrack(muted:true)`.
Chỉ tắt, **không bao giờ bật hộ** (LiveKit mặc định cũng cấm remote unmute).

### 3.4 Một người chia sẻ màn hình

- Client hợp tác: thấy người khác bắt đầu chia sẻ trong khi mình đang chia sẻ → tự dừng.
- Server chặn cuối: webhook `track_published` với `source = SCREEN_SHARE` → tắt (mute) track
  `ScreenShare` của mọi người khác trong phòng.

### 3.5 Chat

`useChat()` của components-react (topic `lk.chat`). Tin đi thẳng qua LiveKit tới người đang trong
phòng; server TrustCabinet không thấy, không lưu.

### 3.6 Dữ liệu cá nhân (Nghị định 13/2023/NĐ-CP)

Chỉ lưu **ai vào/ra phòng, lúc nào** (`call_sessions`, `call_participants`). Không lưu media, không
lưu chat, không ghi âm. Thời hạn lưu theo hồ sơ cuộc họp. Nêu rõ trong ADR-0010.

---

## 4. Hạ tầng dev

### 4.1 `docker/livekit.dev.yaml` (file mới, commit — không chứa secret)

Cấu hình này đã chạy thật trên Docker Desktop cho Mac: ICE chọn `udp4 host 127.0.0.1:7882`, và
webhook tới được Kestrel trên máy host.

```yaml
# LiveKit cho máy dev. KHÔNG chứa secret: key được truyền qua biến môi trường LIVEKIT_KEYS
# (docker-compose.yml đọc từ .env). Hướng dẫn: docs/02-design/03-co-so-du-lieu.md.
port: 7880
rtc:
  tcp_port: 7881        # ICE/TCP dự phòng khi UDP bị chặn
  udp_port: 7882        # một cổng UDP gom mọi peer; KHÔNG đặt port_range_start/end
  use_external_ip: false
  node_ip: 127.0.0.1    # thiếu dòng này LiveKit quảng bá IP container 172.x, trình duyệt không tới được
room:
  max_participants: 50
webhook:
  api_key: trustcabinet-dev          # phải trùng tên key trong LIVEKIT_KEYS
  urls:
    - http://host.docker.internal:5225/api/livekit/webhook
logging:
  level: info
```

### 4.2 `docker-compose.yml` — thêm service (profile `calls`)

```yaml
  # Phòng họp trực tuyến (SCRUM-39). Chỉ chạy khi cần: docker compose --profile calls up -d
  # Profile để ai không làm phần gọi vẫn `docker compose up -d` như cũ, không cần secret LiveKit.
  livekit:
    image: livekit/livekit-server:v1.13.7
    container_name: trustcabinet-livekit
    profiles: ["calls"]
    command: --config /etc/livekit.yaml
    environment:
      # "tên-key: secret". Secret >= 32 ký tự — SDK .NET ném ArgumentException nếu ngắn hơn.
      LIVEKIT_KEYS: "trustcabinet-dev: ${LIVEKIT_API_SECRET:-}"
    ports:
      - "7880:7880"       # HTTP API + WebSocket signalling
      - "7881:7881"       # ICE/TCP
      - "7882:7882/udp"   # ICE/UDP (mux)
    volumes:
      - ./docker/livekit.dev.yaml:/etc/livekit.yaml:ro
    extra_hosts:
      # Docker Desktop có sẵn host.docker.internal; Linux thì cần dòng này.
      - "host.docker.internal:host-gateway"
```

- Không dùng `${LIVEKIT_API_SECRET:?…}`: compose nội suy **cả file** kể cả service ngoài profile,
  nên `:?` sẽ làm `docker compose up -d` của đồng đội lỗi.
- **CHƯA KIỂM:** `LIVEKIT_KEYS` áp lên file YAML không có `keys:` (đọc từ source thấy flag/env áp
  sau YAML). Bước đầu tiên của PR1 là chạy thử; nếu không được, đổi sang biến `LIVEKIT_CONFIG` chứa
  cả nội dung YAML, để compose nội suy `${LIVEKIT_API_SECRET}` vào đó.
- Không dùng `--dev`: chế độ dev tự bind 127.0.0.1 trong container (không tới được) và dùng secret
  `secret` (quá ngắn cho SDK .NET).
- Không cần Redis cho một node LiveKit. Log sẽ in `using single-node routing`.

### 4.3 `.env.example` — thêm

```
# LiveKit (phòng họp trực tuyến, SCRUM-39). Ít nhất 32 ký tự ngẫu nhiên; đặt giống hệt
# LiveKit:ApiSecret trong dotnet user-secrets. Chỉ cần khi chạy: docker compose --profile calls up -d
LIVEKIT_API_SECRET=Doi_Chuoi_Nay_Thanh_It_Nhat_32_Ky_Tu_1
```

### 4.4 Cấu hình backend

`appsettings.json` (commit, không secret):

```json
"LiveKit": {
  "PublicUrl": "",
  "ApiUrl": "",
  "ApiKey": "",
  "TokenTtlMinutes": 10,
  "EnforceRoster": true
}
```

`appsettings.Development.json`:

```json
"LiveKit": {
  "PublicUrl": "ws://localhost:7880",
  "ApiUrl": "http://localhost:7880",
  "ApiKey": "trustcabinet-dev"
}
```

Secret chỉ ở user-secrets:
`dotnet user-secrets set "LiveKit:ApiSecret" "<giống .env>" --project backend/TrustCabinet.Web`

- `PublicUrl` là địa chỉ **trình duyệt** dùng (`ws://`/`wss://`); `ApiUrl` là địa chỉ **backend**
  gọi RoomService (`http://`/`https://`, cùng cổng 7880).
- `EnforceRoster = false` chỉ để load test bằng `lk` CLI (danh tính giả không có trong DB).

### 4.5 Kiểm tra hạ tầng

```bash
docker compose --profile calls up -d livekit
docker compose logs livekit | tail -20          # thấy "starting LiveKit server", không lỗi key
curl -s http://localhost:7880                   # trả "OK"
```

Linux: Kestrel dev bind `localhost` nên webhook từ container có thể không tới. Chạy backend với
`--urls http://0.0.0.0:5225`, hoặc chấp nhận để janitor đối soát (§5.8) — hệ thống vẫn đúng, chỉ
chậm tối đa 30 giây.

---

## 5. Backend

### 5.1 Bản đồ file

| File | Mới/Sửa | Nội dung |
|---|---|---|
| `backend/TrustCabinet.Domain/Calls/CallRoomNames.cs` | Mới | `ForMeeting`, `TryParseMeeting` |
| `backend/TrustCabinet.Domain/Calls/CallContracts.cs` | Mới | enum, record kết quả, DTO trung lập với SDK |
| `backend/TrustCabinet.Domain/Calls/CallRoomRules.cs` | Mới | trạng thái phòng từ `MeetingStatus` + `MeetingMode` |
| `backend/TrustCabinet.Domain/Calls/ICallRepository.cs` | Mới | truy vấn/ghi `call_sessions`, `call_participants`, quyền vào phòng |
| `backend/TrustCabinet.Domain/Calls/ILiveKitGateway.cs` | Mới | trừu tượng LiveKit — Domain **không** tham chiếu SDK |
| `backend/TrustCabinet.Domain/Calls/ICallRoomService.cs` | Mới | nghiệp vụ |
| `backend/TrustCabinet.Infrastructure/Calls/LiveKitOptions.cs` | Mới | section `LiveKit` |
| `backend/TrustCabinet.Infrastructure/Calls/LiveKitGateway.cs` | Mới | dùng `Livekit.Server.Sdk.Dotnet` |
| `backend/TrustCabinet.Infrastructure/Calls/CallRepository.cs` | Mới | EF Core |
| `backend/TrustCabinet.Infrastructure/Calls/CallRoomService.cs` | Mới | luật §5.5 |
| `backend/TrustCabinet.Infrastructure/Calls/CallServiceCollectionExtensions.cs` | Mới | `AddTrustCabinetCalls(IConfiguration)` |
| `backend/TrustCabinet.Infrastructure/Configurations/CallSessionConfiguration.cs` | Sửa | partial unique index (§5.9) |
| `backend/TrustCabinet.Infrastructure/Migrations/*_CallRoomOneActiveSessionPerMeeting.cs` | Mới | sinh bằng `dotnet dotnet-ef` |
| `backend/TrustCabinet.Infrastructure/Seed/DevelopmentDataSeeder.cs` | Sửa | thành phần tham dự cho cuộc họp `InProgress` (§5.10) |
| `backend/TrustCabinet.Infrastructure/TrustCabinet.Infrastructure.csproj` | Sửa | `Livekit.Server.Sdk.Dotnet` 1.2.3, `Microsoft.Extensions.Http` 10.0.x |
| `backend/TrustCabinet.Web/Endpoints/CallEndpoints.cs` | Mới | 3 endpoint + webhook, hằng số MSG |
| `backend/TrustCabinet.Web/CallRoomJanitor.cs` | Mới | `BackgroundService`, cạnh `NotificationWorker.cs` |
| `backend/TrustCabinet.Web/Program.cs` | Sửa | 3 dòng: đăng ký service, hosted service, `MapCallEndpoints()` |
| `backend/TrustCabinet.Web/appsettings*.json` | Sửa | §4.4 |
| `tests/TrustCabinet.Web.Tests/TrustCabinetApiFactory.cs` | Sửa | gỡ `CallRoomJanitor` như đang gỡ `NotificationWorker` |
| `tests/TrustCabinet.Web.Tests/Calls/*` | Mới | §9.1 |

**Không sửa** `MeetingEndpoints.cs`, `MeetingRepository.cs`, `MeetingService.cs`: PR #41 (SCRUM-19,
Quang Huy) đang sửa chúng — đụng vào là conflict. Quyền vào phòng truy vấn riêng trong
`CallRepository`.

### 5.2 Domain — `CallRoomNames`, `CallContracts`, `CallRoomRules`

```csharp
namespace TrustCabinet.Domain.Calls;

/// <summary>
/// LiveKit room names for meeting rooms. The name is derived from the meeting id on the server, so
/// a caller can never ask for a token to a room of their own choosing.
/// </summary>
public static class CallRoomNames
{
    private const string MeetingPrefix = "meeting-";

    public static string ForMeeting(Guid meetingId) => MeetingPrefix + meetingId.ToString("D");

    public static bool TryParseMeeting(string? roomName, out Guid meetingId)
    {
        meetingId = Guid.Empty;
        return roomName is not null
            && roomName.StartsWith(MeetingPrefix, StringComparison.Ordinal)
            && Guid.TryParseExact(roomName[MeetingPrefix.Length..], "D", out meetingId);
    }
}

public enum CallRoomStatus { Open, NotStarted, Ended, InPersonOnly }

public enum CallOutcome
{
    Success,
    NotFound,          // no access, or not an invited participant — 404, hides existence
    RoomNotOpen,       // meeting is not InProgress — 409
    InPersonMeeting,   // Mode = InPerson — 409
    NotChair,          // mute by someone who is not Chair — 403
    TargetNotInRoom,   // mute target absent or has no microphone track — 409
    NotConfigured,     // LiveKit section missing — 503
    ProviderFailed,    // LiveKit API error or timeout — 502
    ReadFailed,        // database failure — 500
}

/// <summary>What the caller may do with a meeting's room, read before joining.</summary>
public sealed record CallRoomAccess(
    Guid MeetingId, MeetingStatus Status, MeetingMode Mode, MeetingRole Role, string FullName);

public sealed record CallRoomView(
    Guid MeetingId, CallRoomStatus Status, MeetingRole MyRole, bool Available, int? ParticipantCount);

public sealed record CallJoinTicket(
    string ServerUrl, string Token, string RoomName, string Identity, DateTimeOffset ExpiresAt);

public sealed record CallResult<T>(CallOutcome Outcome, T? Value = default)
{
    public static CallResult<T> Ok(T value) => new(CallOutcome.Success, value);
    public static CallResult<T> Fail(CallOutcome outcome) => new(outcome);
}

// SDK-neutral views of LiveKit state, so Domain never references Livekit.Server.Sdk.Dotnet.
public enum CallTrackSource { Unknown, Camera, Microphone, ScreenShare, ScreenShareAudio }
public sealed record RoomTrack(string Sid, CallTrackSource Source, bool Muted);
public sealed record RoomParticipant(
    string Identity, string Name, DateTimeOffset JoinedAt, IReadOnlyList<RoomTrack> Tracks);

public sealed record CallWebhookEvent(
    string Id,
    string Event,                     // "participant_joined", "room_finished", …
    string? RoomName,
    string? ParticipantIdentity,
    DateTimeOffset? ParticipantJoinedAt,
    string? TrackSid,
    CallTrackSource TrackSource,
    DateTimeOffset CreatedAt);
```

`CallRoomRules.StatusOf(MeetingStatus status, MeetingMode mode)`:
- `mode == InPerson` → `InPersonOnly` (xét trước);
- `InProgress` → `Open`;
- `Ended` → `Ended`;
- còn lại → `NotStarted`.

### 5.3 `ICallRepository`

```csharp
public interface ICallRepository
{
    /// <summary>
    /// The caller's standing in the meeting's room: an invited participant (any response except
    /// Delegated) of a meeting in a participant-visible status (Published, InProgress, Ended). Null
    /// otherwise, which the endpoints turn into the same 404 as an unknown meeting.
    /// </summary>
    Task<CallRoomAccess?> FindRoomAccessAsync(Guid meetingId, Guid userId, CancellationToken ct = default);

    /// <summary>Active users allowed in the room right now — the janitor's roster.</summary>
    Task<IReadOnlySet<Guid>> ListAllowedUserIdsAsync(Guid meetingId, CancellationToken ct = default);

    Task<CallSession?> FindActiveRoomSessionAsync(Guid meetingId, CancellationToken ct = default);

    /// <summary>
    /// The meeting's Active room session, created if there is none. Safe under concurrent webhooks:
    /// the partial unique index rejects a second Active row, and the loser re-reads the winner's.
    /// </summary>
    Task<CallSession> EnsureActiveRoomSessionAsync(
        Guid meetingId, Guid startedById, DateTimeOffset startedAt, CancellationToken ct = default);

    /// <summary>Opens a stint unless one is already open for this user — webhooks repeat.</summary>
    Task RecordJoinAsync(Guid sessionId, Guid userId, DateTimeOffset joinedAt, CancellationToken ct = default);

    Task RecordLeaveAsync(Guid sessionId, Guid userId, DateTimeOffset leftAt, CancellationToken ct = default);

    /// <summary>Ends the session and closes every stint still open.</summary>
    Task EndSessionAsync(Guid sessionId, DateTimeOffset endedAt, CancellationToken ct = default);

    Task<IReadOnlyList<ActiveRoomSession>> ListActiveRoomSessionsAsync(CancellationToken ct = default);
}

public sealed record ActiveRoomSession(
    Guid SessionId, Guid MeetingId, MeetingStatus MeetingStatus, MeetingMode MeetingMode,
    IReadOnlySet<Guid> OpenUserIds);
```

Chi tiết cài đặt `CallRepository`:

- `FindRoomAccessAsync`: truy vấn từ `db.MeetingParticipants`, không qua `MeetingRepository`:
  `p.MeetingId == meetingId && p.UserId == userId && p.Response != InvitationResponse.Delegated
  && p.User.IsActive && (status ∈ {Published, InProgress, Ended})`,
  select `new CallRoomAccess(p.MeetingId, p.Meeting.Status, p.Meeting.Mode, p.Role, p.User.FullName)`.
  Dùng `AsNoTracking()`.
- `EnsureActiveRoomSessionAsync`: thử đọc → nếu không có thì `Add(new CallSession { Kind = MeetingRoom,
  MeetingId, Status = CallStatus.Active, StartedById, StartedAt })` → `SaveChangesAsync`. Bắt
  `DbUpdateException` có `InnerException is PostgresException { SqlState: "23505",
  ConstraintName: "ux_call_sessions_active_meeting_room" }` → detach entity, đọc lại bản đã có.
- `RecordJoinAsync`: nếu đã có dòng `LeftAt == null` cho (session, user) thì bỏ qua; ngược lại thêm.
- `RecordLeaveAsync`: đặt `LeftAt` cho dòng đang mở; không có dòng mở thì bỏ qua.
- Mọi thời điểm lấy từ payload webhook (`createdAt`, `participant.joinedAt`), không lấy
  `DateTimeOffset.UtcNow` lúc nhận — webhook có thể tới trễ.

### 5.4 `ILiveKitGateway` và `LiveKitGateway`

```csharp
public interface ILiveKitGateway
{
    bool IsConfigured { get; }

    CallJoinTicket CreateJoinTicket(
        string roomName, Guid userId, string displayName, string metadataJson, TimeSpan ttl);

    /// <summary>Empty when the room does not exist (LiveKit answers Not_Found).</summary>
    Task<IReadOnlyList<RoomParticipant>> ListParticipantsAsync(string roomName, CancellationToken ct);

    Task MuteTrackAsync(string roomName, string identity, string trackSid, CancellationToken ct);
    Task RemoveParticipantAsync(string roomName, string identity, CancellationToken ct);
    Task DeleteRoomAsync(string roomName, CancellationToken ct);
    Task<IReadOnlyList<string>> ListRoomNamesAsync(CancellationToken ct);

    /// <summary>Verifies the signature and parses; null when the signature is missing or wrong.</summary>
    CallWebhookEvent? ReadWebhook(ReadOnlySpan<byte> body, string? authorizationHeader);
}
```

Cài đặt (`Infrastructure/Calls/LiveKitGateway.cs`) — những điều **bắt buộc**:

1. **Token** — đã chạy thật:
   ```csharp
   var jwt = new AccessToken(o.ApiKey, o.ApiSecret)
       .WithIdentity(userId.ToString("D"))
       .WithName(displayName)
       .WithMetadata(metadataJson)                 // {"meetingRole":"Chair"}
       .WithTtl(ttl)                               // default SDK là 6h — luôn truyền
       .WithGrants(new VideoGrants
       {
           RoomJoin = true, Room = roomName,       // thiếu một trong hai → ToJwt() ném lỗi
           CanPublish = true, CanSubscribe = true, CanPublishData = true,
           CanPublishSources = new List<string> { "camera", "microphone", "screen_share", "screen_share_audio" },
           CanUpdateOwnMetadata = false, RoomAdmin = false,
       })
       .ToJwt();
   ```
   `CanPublishSources` là chuỗi **chữ thường** và chỉ có hiệu lực khi `CanPublish = true`.
   Token chỉ dùng để kết nối lần đầu; LiveKit tự cấp token mới cho client đang kết nối, nên
   **thu hồi quyền giữa chừng là việc của janitor** (`RemoveParticipant`), không phải của TTL.
2. **RoomServiceClient không an toàn khi dùng chung** (mỗi lời gọi ghi đè
   `DefaultRequestHeaders.Authorization`). Tạo mới mỗi lần:
   `new RoomServiceClient(o.ApiUrl, o.ApiKey, o.ApiSecret, httpClientFactory.CreateClient("livekit"))`.
   Named client `"livekit"` đặt `Timeout = 5s`.
3. **SDK không nhận `CancellationToken`** → bọc `await call.WaitAsync(ct)`.
4. Lỗi: `Twirp.Exception` (namespace **global**), loại lỗi ở `.Type`
   (`Twirp.ErrorCode.Not_Found`, …). `Not_Found` khi list/remove/delete → coi như rỗng/đã xong.
   Lỗi khác + `HttpRequestException` + `TaskCanceledException` (timeout) → ném
   `LiveKitUnavailableException` (tự định nghĩa trong Domain/Calls) để service trả `ProviderFailed`.
5. Map `ParticipantInfo` → `RoomParticipant`: `Identity`, `Name`,
   `DateTimeOffset.FromUnixTimeSeconds(p.JoinedAt)`, `Tracks` (`TrackInfo.Sid`, `.Source`,
   `.Muted`). Map `TrackSource.Microphone/Camera/ScreenShare/ScreenShareAudio`, còn lại `Unknown`.
6. **Webhook — KHÔNG dùng `WebhookReceiver.Receive`.** Nó ném
   `InvalidProtocolBufferException: Unknown field: roomEndReason` với server v1.13.7, và tham số
   `ignoreUnknownFields` không có tác dụng (lỗi SDK). Dùng cách đã kiểm:
   ```csharp
   ClaimsModel claims;
   try { claims = new TokenVerifier(o.ApiKey, o.ApiSecret).Verify(authorizationHeader); }
   catch (Exception) { return null; }                         // chữ ký sai hoặc hết hạn (5 phút)
   var hash = Convert.ToBase64String(SHA256.HashData(body));
   if (!CryptographicOperations.FixedTimeEquals(
           Encoding.UTF8.GetBytes(claims.Sha256 ?? ""), Encoding.UTF8.GetBytes(hash)))
       return null;
   var ev = new JsonParser(JsonParser.Settings.Default.WithIgnoreUnknownFields(true))
       .Parse<WebhookEvent>(Encoding.UTF8.GetString(body));
   ```
   (Tên kiểu trả về của `Verify` lấy theo SDK khi code — kiểm bằng IntelliSense.)
   Header `Authorization` là JWT **trần, không có tiền tố `Bearer `**.
7. **Không đọc `WebhookEvent.NumDropped`**, `TrackInfo.Simulcast/Layers/Stereo/DisableDtx`,
   `ParticipantPermission.Recorder/Agent` — `[Obsolete]` → CS0612 → CI đỏ vì `-warnaserror`.
8. Kiểu protobuf của SDK nằm trong namespace `Livekit.Server.Sdk.Dotnet`, **không phải**
   `LiveKit.Proto`.
9. `IsConfigured` = `PublicUrl`, `ApiUrl`, `ApiKey` khác rỗng **và** `ApiSecret.Length >= 32`.
   Chưa cấu hình thì mọi method (trừ `IsConfigured`) ném `InvalidOperationException` — service kiểm
   `IsConfigured` trước nên không bao giờ gọi tới.

### 5.5 `CallRoomService` — luật nghiệp vụ

```csharp
public interface ICallRoomService
{
    Task<CallResult<CallRoomView>> GetRoomAsync(Guid meetingId, Guid actorId, CancellationToken ct = default);
    Task<CallResult<CallJoinTicket>> JoinAsync(Guid meetingId, Guid actorId, CancellationToken ct = default);
    Task<CallOutcome> MuteAsync(Guid meetingId, Guid actorId, Guid targetUserId, CancellationToken ct = default);
    Task HandleWebhookAsync(CallWebhookEvent ev, CancellationToken ct = default);
    Task ReconcileAsync(CancellationToken ct = default);
}
```

**GetRoomAsync** (theo thứ tự):
1. `access = FindRoomAccessAsync` → null → `NotFound`.
2. `status = CallRoomRules.StatusOf(access.Status, access.Mode)`.
3. `participantCount`: chỉ khi `status == Open && IsConfigured` → `ListParticipantsAsync(...).Count`;
   lỗi LiveKit → `null` (không làm hỏng cả request).
4. `Ok(new CallRoomView(meetingId, status, access.Role, gateway.IsConfigured, count))`.
5. Lỗi DB (`DbUpdateException`, `NpgsqlException`, `TimeoutException`) → `ReadFailed`.

**JoinAsync**:
1. `access` null → `NotFound`.
2. `Mode == InPerson` → `InPersonMeeting`.
3. `Status != InProgress` → `RoomNotOpen`.
4. `!IsConfigured` → `NotConfigured`.
5. `metadata = JsonSerializer.Serialize(new { meetingRole = access.Role.ToString() })`.
6. `Ok(gateway.CreateJoinTicket(CallRoomNames.ForMeeting(id), actorId, access.FullName, metadata,
   TimeSpan.FromMinutes(o.TokenTtlMinutes)))`.

Không ghi DB ở bước này — phiên được tạo khi LiveKit báo người đó **thật sự** đã vào.

**MuteAsync**:
1. `access` null → `NotFound`.
2. `access.Role != Chair` → `NotChair`.
3. `Status != InProgress` → `RoomNotOpen`.
4. `!IsConfigured` → `NotConfigured`.
5. Tìm participant có `Identity == targetUserId.ToString("D")` và track `Microphone` → không có →
   `TargetNotInRoom`; đã `Muted` → `Success` (idempotent).
6. `MuteTrackAsync` → `Success`; `LiveKitUnavailableException` → `ProviderFailed`.

Chủ toạ tắt mic chính mình: cho phép (vô hại).

**HandleWebhookAsync** — bỏ qua (log Debug) nếu tên phòng không phải `meeting-…` hoặc
identity không phải Guid (danh tính giả của `lk load-test`):

| Event | Xử lý |
|---|---|
| `participant_joined` | `EnsureActiveRoomSessionAsync(meetingId, userId, joinedAt)` → `RecordJoinAsync` |
| `participant_left`, `participant_connection_aborted` | có phiên Active → `RecordLeaveAsync(…, ev.CreatedAt)` |
| `room_finished` | có phiên Active → `EndSessionAsync(…, ev.CreatedAt)` |
| `track_published` với `TrackSource == ScreenShare` | (PR4) list participants → mọi người **khác** đang có track `ScreenShare` chưa mute → `MuteTrackAsync` |
| khác | bỏ qua |

Không cần bảng chống trùng theo `ev.Id`: mọi xử lý đều idempotent theo trạng thái. Webhook tới theo
thứ tự **trong cùng một resource** nhưng có thể mất → janitor bù.

### 5.6 Endpoints — `CallEndpoints.cs`

```csharp
public static class CallEndpoints
{
    // Codes from docs/03-api/00-danh-muc-ma-thong-bao.md. Take the NEXT FREE numbers at implementation
    // time: sync main and read the current catalogue. The catalogue gives MSG117 to "room not available" (MSG65 belongs to Complete Agenda Item).
    private const string MsgRoomNotOpen = "MSG__";       // 409
    private const string MsgInPersonMeeting = "MSG__";   // 409
    private const string MsgCallUnavailable = "MSG__";   // 503
    private const string MsgJoinFailed = "MSG__";        // 500
    private const string MsgNotChair = "MSG__";          // 403
    private const string MsgTargetNotInRoom = "MSG__";   // 409
    private const string MsgProviderFailed = "MSG__";    // 502
    private const string MsgRoomLoadFailed = "MSG__";    // 500
    …
}
```

Tên hằng số theo mẫu `MsgNNRoomNotOpen` khi catalogue đã cấp số. `MessageCodeCatalogueTests` chỉ quét
`backend/TrustCabinet.Web/Endpoints/**` — **mọi mã phải khai báo ở file này**, và mỗi mã phải có
test assert chuỗi `"MSGnn"`.

| Method | Path | Nhóm | Thành công | Lỗi |
|---|---|---|---|---|
| GET | `/api/meetings/{id:guid}/call` | `RequireActiveUser(ServerError(MsgRoomLoadFailed))` | `200 CallRoomResponse` | `404` rỗng · `500 MsgRoomLoadFailed` |
| POST | `/api/meetings/{id:guid}/call/join` | `RequireActiveUser(ServerError(MsgJoinFailed))` | `200 CallJoinResponse` | `404` · `409 MsgInPersonMeeting` · `409 MsgRoomNotOpen` · `503 MsgCallUnavailable` · `500 MsgJoinFailed` |
| POST | `/api/meetings/{id:guid}/call/participants/{userId:guid}/mute` | như trên | `204` | `404` · `403 MsgNotChair` · `409 MsgRoomNotOpen` · `409 MsgTargetNotInRoom` · `503` · `502 MsgProviderFailed` · `500` |
| POST | `/api/livekit/webhook` | `.AllowAnonymous()` — xác thực bằng chữ ký | `200` | `401` rỗng (chữ ký sai) · `503` rỗng (chưa cấu hình) · `500` rỗng (lỗi DB → LiveKit gửi lại) |

Nội dung `message` (tiếng Việt, người dùng đọc được):

| Hằng số | `message` |
|---|---|
| `MsgRoomNotOpen` | `Phòng họp chỉ mở khi cuộc họp đang diễn ra.` |
| `MsgInPersonMeeting` | `Cuộc họp này họp trực tiếp nên không có phòng họp trực tuyến.` |
| `MsgCallUnavailable` | `Phòng họp trực tuyến chưa sẵn sàng. Vui lòng liên hệ quản trị viên.` |
| `MsgJoinFailed` | `Không thể vào phòng họp. Vui lòng thử lại.` |
| `MsgNotChair` | `Chỉ chủ toạ mới tắt được micro của người khác.` |
| `MsgTargetNotInRoom` | `Người này không còn trong phòng hoặc chưa bật micro.` |
| `MsgProviderFailed` | `Máy chủ phòng họp không phản hồi. Vui lòng thử lại.` |
| `MsgRoomLoadFailed` | `Không thể tải trạng thái phòng họp. Vui lòng thử lại.` |

403 hay 404 (theo bảng trong AGENTS.md): người không được mời → `404` (giấu sự tồn tại của cuộc
họp). Người **được mời nhưng không phải chủ toạ** bấm tắt mic → `403`, vì họ vốn thấy cuộc họp.

Response records (khai báo cuối `CallEndpoints.cs`, như `MeetingEndpoints.cs`):

```csharp
public sealed record CallRoomResponse(
    Guid MeetingId, string Status, string MyRole, bool Available, int? ParticipantCount);
public sealed record CallJoinResponse(
    string ServerUrl, string Token, string RoomName, string Identity, DateTimeOffset ExpiresAt);
public sealed record CallErrorResponse(string Code, string Message);
```

Webhook handler:
- Đọc raw body vào `MemoryStream`, giới hạn 1 MB (quá → `413`).
- `Authorization` lấy nguyên chuỗi (`request.Headers.Authorization.ToString()`).
- `gateway.ReadWebhook(...)` null → `401`.
- Gọi `HandleWebhookAsync`; lỗi DB → log Error + `500` để LiveKit gửi lại.
- **Không** đặt trong group có `RequireAuthorization`.

### 5.7 `Program.cs` — chỉ thêm

```csharp
builder.Services.AddTrustCabinetCalls(builder.Configuration);
builder.Services.AddHostedService<CallRoomJanitor>();
…
// SCRUM-39/41: meeting call room — token, chair mute, LiveKit webhook.
app.MapCallEndpoints();
```

Không thêm `/hubs/calls` vào `OnMessageReceived` — phòng họp không có hub (D3).

### 5.8 `CallRoomJanitor`

`BackgroundService`, `PeriodicTimer(TimeSpan.FromSeconds(30))`. Mỗi vòng tạo scope, gọi
`ICallRoomService.ReconcileAsync`, bắt và log mọi exception (không để service chết).

`ReconcileAsync`:
1. `!IsConfigured` → return.
2. Với mỗi `ActiveRoomSession`:
   - cuộc họp không còn `InProgress`, hoặc `Mode == InPerson` → `DeleteRoomAsync` → `EndSessionAsync(now)`;
   - ngược lại `live = ListParticipantsAsync(room)`:
     - `live` rỗng → `EndSessionAsync(now)` (lần sau có người vào sẽ tạo phiên mới);
     - `EnforceRoster`: identity không parse được Guid hoặc không thuộc
       `ListAllowedUserIdsAsync` → `RemoveParticipantAsync`;
     - đóng các dòng đang mở mà identity không còn trong `live` (bù `participant_left` bị mất);
     - mở dòng cho identity hợp lệ trong `live` mà chưa có dòng mở (bù `participant_joined` bị
       mất), `JoinedAt = p.JoinedAt`.
3. `ListRoomNamesAsync` → phòng `meeting-…` đang sống mà **không có** phiên Active → xử lý như bước 2
   sau khi `EnsureActiveRoomSessionAsync` (người `StartedById` = participant hợp lệ đầu tiên; không
   có ai hợp lệ thì chỉ `DeleteRoomAsync`).

Nhờ janitor, cuộc họp chuyển sang `Ended` (ai làm luồng đó cũng không cần biết tới phòng gọi) thì
tối đa 30 giây sau phòng tự đóng — đúng AC "chỉ mở khi `InProgress`".

### 5.9 Migration

Trong `CallSessionConfiguration.Configure` thêm:

```csharp
// One live room per meeting. Two participant_joined webhooks for the first two people can arrive
// together; without this both would create a session.
builder.HasIndex(c => c.MeetingId)
    .IsUnique()
    .HasFilter("status = 'Active' AND meeting_id IS NOT NULL")
    .HasDatabaseName("ux_call_sessions_active_meeting_room");
```

Kiểm tên cột thật trong migration cũ `20260919181909_ExpandDomainModel` (EFCore.NamingConventions
→ snake_case) trước khi viết filter. Sau đó:

```bash
dotnet dotnet-ef migrations add CallRoomOneActiveSessionPerMeeting \
  --project backend/TrustCabinet.Infrastructure --startup-project backend/TrustCabinet.Web \
  --output-dir Migrations
```

Commit migration **cùng** `TrustCabinetDbContextModelSnapshot.cs`. Nếu `main` đã có migration mới
hơn (PR #41) thì rebase **trước** khi tạo migration.

### 5.10 Seeder

Cuộc họp demo `"Rà soát kế hoạch chuyển đổi số"` đã là `InProgress` + `Online` + `Confidential`
nhưng **chưa có thành phần tham dự** → không ai vào phòng được.

- Trong `SeedAsync` (DB trống): thêm Chair = `chair`, Secretary = `secretary`, Member = `member`
  cho `meetings[2]`, `Response = Accepted`.
- DB đã có dữ liệu: thêm `EnsureCallRoomDemoParticipantsAsync(db, ct)`, gọi ngay sau
  `RepairPlaceholderPasswordsAsync`. Hẹp như hàm đó: chỉ khi tìm thấy đúng cuộc họp theo tiêu đề +
  `InProgress` **và** nó có 0 participant; lấy ba user theo email demo; thiếu user nào thì bỏ qua.

Tài khoản test: `chutoa@`, `thuky@`, `thanhvien@trustcabinet.local`, mật khẩu
`TrustCabinet@2026` (`DevelopmentDataSeeder.DemoPassword`).

---

## 6. Frontend

### 6.1 Thư viện

```bash
cd frontend && npm i livekit-client@^2.22.3 @livekit/components-react@^2.9.24
```

- Không cài `@livekit/components-styles`.
- Components-react 2.9.24 có peer dep `react >=18`, `livekit-client ^2.20.1`, nên dùng được với
  React 19.
- Dùng từ components-react:
  - `LiveKitRoom` (quản lý kết nối), `RoomAudioRenderer` (phát audio **mọi** người, kể cả người ở
    trang lưới khác), `StartAudio`, `VideoTrack`;
  - hooks `useTracks`, `useParticipants`, `useLocalParticipant`, `useIsSpeaking`,
    `useConnectionState`, `useTrackToggle`, `useMediaDeviceSelect`, `usePreviewTracks`, `useChat`,
    `useRoomContext`.
  - **Props/kiểu cụ thể lấy từ typings trong `node_modules`, không đoán.**

### 6.2 Bản đồ file

| File | Nội dung |
|---|---|
| `src/types/call.ts` | `CallRoomStatus`, `CallRoomState`, `CallJoinResponse`, `MeetingRole`, nhãn tiếng Việt |
| `src/api/calls.ts` | `getMeetingCall(id)`, `joinMeetingCall(id)`, `muteCallParticipant(id, userId)` qua `api.*` |
| `src/lib/routes.ts` | `meetingRoom: (id) => /mgmt/meetings/${id}/room` |
| `src/app/App.tsx` | route phòng họp **trong `RequireAuth`, ngoài `AppShell`** (toàn màn hình), lazy |
| `src/lib/avatar.ts` | tách `avatarColors` từ `AppShell.tsx` + thêm `initials(name)`; `AppShell` import lại |
| `src/components/Icon.tsx` | thêm `mic`, `micOff`, `videoOff`, `monitor`, `messageSquare`, `phoneOff`, `settings`, `users` (đã có) |
| `src/features/calls/room/MeetingRoomPage.tsx` | máy trạng thái trang (§6.3) |
| `src/features/calls/room/PreJoin.tsx` | xem trước, chọn thiết bị, bật/tắt, "Vào phòng" |
| `src/features/calls/room/RoomView.tsx` | bố cục trong phòng: header, stage, panel, control bar |
| `src/features/calls/room/ParticipantGrid.tsx` | lưới có phân trang |
| `src/features/calls/room/ParticipantTile.tsx` | video hoặc chữ cái đầu, tên, mic, viền đang nói |
| `src/features/calls/room/ScreenShareStage.tsx` | khung lớn khi có người chia sẻ + dải camera |
| `src/features/calls/room/ControlBar.tsx` | mic, cam, chia sẻ, panel, thiết bị, rời phòng |
| `src/features/calls/room/DeviceMenu.tsx` | chọn mic/cam/loa bằng `Select.tsx` |
| `src/features/calls/room/SidePanel.tsx` | khung panel + registry (§7) |
| `src/features/calls/room/ParticipantsPanel.tsx` | danh sách người, nút "Tắt mic" cho chủ toạ |
| `src/features/calls/room/ChatPanel.tsx` | chat tạm thời |
| `src/features/calls/room/ScreenShareConfirmDialog.tsx` | nhắc quyền riêng tư trước khi chia sẻ |
| `src/features/calls/room/roomMessages.ts` | map `DisconnectReason`, `MediaDeviceFailure` → câu tiếng Việt |
| `src/features/calls/room/participantMeta.ts` | đọc `metadata` JSON → `meetingRole` |
| `src/features/calls/room/roomOptions.ts` | `RoomOptions` **hằng số cấp module** |
| `src/features/meetings/detail/MeetingDetailPage.tsx` | nút "Vào phòng họp" |
| `src/styles/global.css` | lớp `.room-*`; token mới trên `:root` (`--stage-bg`, `--speaking`) |

### 6.3 Máy trạng thái `MeetingRoomPage`

```
loading ──GET call──▶ unavailable (404 → NotFound; InPersonOnly/NotStarted/Ended → màn báo + link về chi tiết)
   │
   ▼
prejoin ──bấm "Vào phòng"──▶ joining ──POST join──▶ connected ──Disconnected──▶ left(reason)
                               │ 409/503/500 → quay lại prejoin + alert       │
                               ▼                                              ▼
                           (giữ lựa chọn thiết bị)                  "Vào lại" (nếu reason cho phép)
```

- `roomOptions.ts`:
  ```ts
  export const ROOM_OPTIONS: RoomOptions = {
    adaptiveStream: true,          // mặc định false — phải bật
    dynacast: true,                // mặc định false — phải bật
    publishDefaults: { simulcast: true },
    videoCaptureDefaults: { resolution: VideoPresets.h720.resolution },
  }
  ```
  Để ở cấp module: object mới mỗi lần render có thể làm `LiveKitRoom` kết nối lại.
- Token lấy trong **event handler** của nút "Vào phòng", không trong `useEffect` (StrictMode chạy
  effect hai lần → hai token, hai lần vào).
- Rời phòng: nút "Rời phòng" → `ConfirmDialog` → `room.disconnect()` →
  `navigate(routes.meeting(id))`. Khi đang kết nối: `useBeforeUnload` cảnh báo đóng tab. App dùng
  `BrowserRouter` nên **không có `useBlocker`**: nút Back trình duyệt sẽ rời phòng (giống Meet) —
  chấp nhận.
- Trong phòng **không có link nào chuyển tab hiện tại**. Link sang màn khác dùng
  `target="_blank" rel="noopener"`.

### 6.4 Màn chờ (PreJoin)

- Tiêu đề cuộc họp; "Đang có N người trong phòng" (từ `participantCount`, `null` thì ẩn).
- Khung xem trước camera (`usePreviewTracks`); camera tắt → chữ cái đầu tên trên nền
  `avatarColors`.
- Thanh mức âm mic (audio level của preview track) để người dùng biết mic đang nhận tiếng.
- Hai `Select`: micro, camera (`Room.getLocalDevices(kind)`). Có loa (`audiooutput`) nếu trình duyệt
  hỗ trợ.
- Nút bật/tắt mic, cam. Mặc định: **mic bật, cam tắt**. Nhớ lựa chọn trong `localStorage` (bọc
  try/catch).
- Lỗi thiết bị (`MediaDeviceFailure.getFailure(err)`):

| Failure | Câu hiển thị |
|---|---|
| `PermissionDenied` | `Trình duyệt đang chặn micro/camera. Bấm biểu tượng ổ khoá cạnh thanh địa chỉ, chọn Cho phép rồi tải lại trang. Bạn vẫn có thể vào phòng để nghe.` |
| `NotFound` | `Không tìm thấy micro/camera trên máy này.` |
| `DeviceInUse` | `Micro/camera đang được ứng dụng khác dùng (Zoom, Teams…). Đóng ứng dụng đó rồi thử lại.` |
| `Other` | `Không mở được micro/camera. Bạn vẫn có thể vào phòng để nghe.` |

  Bị chặn mic **vẫn cho vào phòng** (chỉ nghe).
- Nút "Vào phòng" disabled khi đang gọi API, nhãn "Đang vào phòng…".

### 6.5 Trong phòng

**Bố cục** (desktop ≥ 1024px):

```
┌──────────────────────────────────────────────────────────────────────┐
│ Tên cuộc họp · Đang diễn ra · 00:42:10                    [Mật] [≡] │  header 56px
├──────────────────────────────────────────┬───────────────────────────┤
│  Lưới video / màn hình chia sẻ           │  Panel 360px (thu gọn được)│
├──────────────────────────────────────────┴───────────────────────────┤
│   Mic  Camera  Chia sẻ  │  Người dự  Chat  (panel khác)  │ Rời phòng  │  control bar 72px
└──────────────────────────────────────────────────────────────────────┘
```

Dưới 1024px, panel đè lên stage dạng sheet. Nền stage là `--stage-bg` (đặt bằng giá trị `--ink`).
Đây không phải dark mode — UI vẫn chỉ có bản sáng.

**Lưới:**
- Tối đa 9 ô/trang (3×3), 4 ô khi cửa sổ < 1024px; có nút trang trước/sau.
- Thứ tự: người đang nói lên trang 1 **chỉ khi họ đang ở trang khác** (tránh ô nhảy liên tục),
  mình ở cuối.
- Ô bị ẩn do phân trang không attach video → adaptive stream tự dừng tải.

**Ô người dự:**
- Có video → `VideoTrack`. Không có → chữ cái đầu tên, nền `avatarColors`.
- Góc dưới: tên, biểu tượng mic tắt, huy hiệu "Chủ toạ" nếu `meetingRole == Chair`.
- Đang nói (`isSpeaking`) → viền `--speaking` 3px, không đổi kích thước ô (dùng `outline` hoặc
  `box-shadow`, không dùng `border`).

**Chia sẻ màn hình:**
- Có người chia sẻ → `ScreenShareStage` (khung lớn) + dải camera; nhãn "X đang trình bày".
- Bấm Chia sẻ → `ScreenShareConfirmDialog`:
  - tiêu đề `Chia sẻ màn hình`;
  - nội dung `Mọi người trong phòng sẽ thấy nội dung bạn chọn. Trước khi chia sẻ, hãy đóng tin
    nhắn, email hoặc tài liệu riêng tư. Nên chọn một tab hoặc một cửa sổ thay vì toàn màn hình.`;
  - thêm dòng `X đang trình bày — bạn chia sẻ thì phần của X sẽ dừng.` nếu có người đang chia sẻ;
  - nút `Chọn nội dung để chia sẻ` / `Huỷ`.
- Gọi `setScreenShareEnabled(true, { audio: true, selfBrowserSurface: 'exclude',
  surfaceSwitching: 'include', contentHint: 'detail' })`.
- Người dùng bấm "Dừng chia sẻ" trên thanh trình duyệt → SDK tự unpublish → `LocalTrackUnpublished`
  với `source === Track.Source.ScreenShare` → cập nhật nút.
- Người khác bắt đầu chia sẻ khi mình đang chia sẻ → tự `setScreenShareEnabled(false)` + toast
  `X bắt đầu trình bày, phần chia sẻ của bạn đã dừng.`

**Âm thanh:**
- `<RoomAudioRenderer />` luôn có trong cây.
- Trình duyệt chặn autoplay → `<StartAudio label="Bấm để nghe mọi người" />` dạng banner.

**Kết nối:**

| Sự kiện | Hiển thị |
|---|---|
| `Reconnecting` | banner `Mất kết nối, đang kết nối lại…` (không che stage) |
| `Reconnected` | ẩn banner |
| `Disconnected(DUPLICATE_IDENTITY)` | `Bạn đã vào phòng này từ tab hoặc thiết bị khác. Tab này đã rời phòng.` |
| `Disconnected(PARTICIPANT_REMOVED)` | `Bạn không còn trong danh sách tham dự phòng họp này.` |
| `Disconnected(ROOM_DELETED \| ROOM_CLOSED)` | `Cuộc họp đã kết thúc, phòng họp đã đóng.` |
| khác | `Mất kết nối tới phòng họp.` + nút `Vào lại` |

**Bị chủ toạ tắt mic:** mic của mình chuyển `muted` mà mình không bấm → toast
`Chủ toạ đã tắt micro của bạn. Bạn có thể bật lại khi cần phát biểu.`

### 6.6 Panel "Người dự" và "Chat"

- **Người dự:**
  - Sắp xếp: Chủ toạ → Thư ký → theo tên.
  - Mỗi dòng: chữ cái đầu, tên, "(Bạn)", trạng thái mic/cam, chấm "đang nói".
  - Chủ toạ thấy nút `Tắt mic` ở người khác đang bật mic → `muteCallParticipant` → toast
    `Đã tắt micro của {tên}.`
  - Lỗi: hiện `message` backend; nhánh xử lý theo `code` bằng `isApiError`.
- **Chat:**
  - `useChat()` → danh sách `{ timestamp, message, from }`.
  - Dòng chú thích cố định: `Tin nhắn chỉ hiện với người đang trong phòng và không được lưu lại sau
    cuộc họp.`
  - Ô nhập: Enter gửi, Shift+Enter xuống dòng, tối đa 1000 ký tự có bộ đếm.
  - Giờ `HH:mm`, giữ xuống dòng (`white-space: pre-wrap`).
  - Panel đóng mà có tin mới → badge số trên nút Chat.
  - Topic đăng ký hai lần sẽ ném `HandlerAlreadyRegistered` — dùng `useChat` (đã xử lý), không tự
    `registerTextStreamHandler` trong component.

### 6.7 Màn chi tiết cuộc họp

- Nút `Vào phòng họp` (icon `video`, `btn` chính) ở `page-header-actions`. Chỉ hiện khi
  `mode !== 'InPerson'`, `status === 'InProgress'` **và** `GET /call` trả `status: 'Open'`.
  - Chỉ gọi `GET /call` khi hai điều kiện đầu đúng.
  - `GET /call` trả 404 (người xem không phải thành phần tham dự, ví dụ người tạo) → ẩn nút.
- Ô "Địa điểm" đang ghi `Phòng gọi trực tuyến` (dòng 172) giữ nguyên.

---

## 7. Hợp đồng component panel (tóm tắt ADR-0011)

Bản chính thức, kèm kịch bản kiểm thử cho tester, ở
[ADR-0011](../05-decisions/0011-tinh-nang-hop-la-panel-dung-chung.md). Phần dưới là những gì phòng
họp cần từ panel.

Để vote, nghị sự, phát biểu, tài liệu vừa là tab ở màn chi tiết vừa là panel trong phòng họp mà
**không code hai lần**, mỗi tính năng viết thành một component:

```tsx
// src/features/<tính năng>/<TenTinhNang>Panel.tsx
export function VotingPanel({ meetingId, variant }: { meetingId: string; variant: 'page' | 'room' }) { … }
```

1. Nhận `meetingId`, tự gọi API qua `src/api/`, tự nghe cập nhật realtime (SignalR
   `/hubs/meetings` của SCRUM-25). **Không** nhận dữ liệu từ trang cha.
2. Hiển thị tốt ở bề ngang **360px**. Không có `PageHeader`, không có breadcrumb bên trong.
3. Mọi thao tác dùng `Dialog`/`ConfirmDialog`, **không chuyển trang**. Link sang màn khác trong
   `variant='room'` phải mở tab mới.
4. Quyền (ai mở vote, ai bỏ phiếu) lấy từ server, không tự suy từ vai trò ở FE.
5. Sự kiện cần mọi người chú ý (mở biểu quyết) thì phát qua hub. Phòng họp hiện thông báo nổi
   `Chủ toạ mở biểu quyết: … — Bỏ phiếu` → bấm mở panel tương ứng.

Registry panel trong phòng (`SidePanel.tsx`):

```ts
const ROOM_PANELS: RoomPanel[] = [
  { id: 'people', label: 'Người dự', icon: 'users', render: (id) => <ParticipantsPanel meetingId={id} /> },
  { id: 'chat', label: 'Chat', icon: 'messageSquare', render: (id) => <ChatPanel /> },
  // Thêm khi component tương ứng có: agenda (SCRUM-25), attendance (26), speaking (27),
  // voting (28), documents (21), recording (29), notes (30).
]
```

Panel chưa có component thì **không** thêm vào registry. Header phòng có link "Mở chi tiết cuộc họp"
(tab mới) để dùng tạm.

---

## 8. Chia PR và checklist

Mỗi PR một nhánh từ `main` mới nhất. Tiêu đề và mô tả PR tiếng Anh, commit tiếng Việt, **không
thêm dòng ghi công AI**. Chạy đủ bốn lệnh trước khi push:

```bash
dotnet build TrustCabinet.sln -warnaserror && dotnet test TrustCabinet.sln
cd frontend && npm run lint && npm run build
```

### PR1 — `feat/scrum-39-livekit-infrastructure` (BE + hạ tầng, ~1,5 ngày)

- [ ] Kiểm PR #41 và catalogue hiện tại: đồng bộ nhánh trước khi lấy mã MSG còn trống;
      Phòng chưa mở dùng `MSG117` theo catalogue (ADR-0010 ghi `MSG65` là nhầm — `MSG65` thuộc UC-25 Complete Agenda Item); báo Quang Huy để tránh cấp trùng giữa các PR.
- [ ] `docker/livekit.dev.yaml`, service `livekit` (profile `calls`), `.env.example`; chạy §4.5,
      xác nhận `LIVEKIT_KEYS` hoạt động (**CHƯA KIỂM**), không thì chuyển `LIVEKIT_CONFIG`.
- [ ] `appsettings*.json` §4.4.
- [ ] Gói NuGet: `Livekit.Server.Sdk.Dotnet` 1.2.3, `Microsoft.Extensions.Http`.
- [ ] Domain `Calls/*` §5.2–5.5.
- [ ] Infrastructure: `LiveKitOptions`, `LiveKitGateway`, `CallRepository`, `CallRoomService`,
      `AddTrustCabinetCalls`.
- [ ] Partial unique index + migration §5.9.
- [ ] `CallEndpoints`: `GET /call`, `POST /call/join`, webhook. Mute để PR2.
- [ ] `CallRoomJanitor` + gỡ nó trong `TrustCabinetApiFactory`.
- [ ] Seeder §5.10.
- [ ] Test §9.1 (trừ mute và screen share).
- [ ] FE tối thiểu: `src/types/call.ts`, `src/api/calls.ts` (quy tắc "thêm endpoint = 3 chỗ").
- [ ] Docs:
  - ADR-0010 đã có từ PR thiết kế. Code phải khác quyết định thì viết ADR mới, không sửa ADR đã
    chấp nhận;
  - `docs/03-api/09-phong-hop-truc-tuyen.md`;
  - catalogue MSG (dòng + "Mã tiếp theo");
  - `docs/02-design/02-erd.md` (index mới, phiên tạo từ webhook);
  - `docs/02-design/03-co-so-du-lieu.md` (cách bật LiveKit);
  - bảng công nghệ trong `README.md` (Realtime/WebRTC → LiveKit).
- [ ] Jira SCRUM-39: chuyển `Backend`, comment ghi chú §11.
- [ ] Thử tay: đăng nhập `thanhvien@`, gọi `POST /call/join` bằng curl, dán token vào
      <https://meet.livekit.io> (tab "Custom", server `ws://localhost:7880`) → vào được phòng;
      kiểm `call_sessions`/`call_participants` có dòng; tắt tab → dòng có `left_at`.
      (**CHƯA KIỂM:** trình duyệt có thể chặn trang https mở `ws://localhost`; khi đó dùng một
      trang HTML nhỏ chạy local gọi `livekit-client` — không commit trang đó.)

### PR2 — `feat/scrum-41-meeting-room` (BE mute + FE phòng họp audio, ~2 ngày)

- [ ] BE: `MuteAsync` + endpoint mute + test (`403`, `409`, `502`, `204`).
- [ ] FE: route, `MeetingRoomPage`, `PreJoin` (chỉ mic ở PR này), `RoomView`, `ParticipantTile`
      (chữ cái đầu), lưới, `ControlBar` (mic, rời phòng), `RoomAudioRenderer`, `StartAudio`,
      banner kết nối, `ParticipantsPanel` (+ nút tắt mic cho chủ toạ), `ChatPanel`, `SidePanel`.
- [ ] Nút "Vào phòng họp" ở màn chi tiết.
- [ ] `src/lib/avatar.ts`, icon mới, CSS `.room-*`.
- [ ] Verify trên trình duyệt §9.2 với 3 người.
- [ ] Jira SCRUM-41: ghi chú §11 (kể cả chat nếu nhóm đồng ý D7).

### PR3 — `feat/scrum-42-video` (~1 ngày)

- [ ] Camera trong PreJoin (xem trước), bật/tắt trong phòng không gọi lại.
- [ ] Chọn thiết bị trong phòng: `DeviceMenu` + `room.switchActiveDevice(kind, deviceId)`.
- [ ] Lưới video phân trang, viền người đang nói, chữ cái đầu khi tắt cam.
- [ ] Load test lưới §9.3.

### PR4 — `feat/scrum-43-screen-share` (~0,5–1 ngày)

- [ ] FE: nút chia sẻ, dialog nhắc riêng tư, `ScreenShareStage`, tự dừng khi người khác chia sẻ,
      bắt `LocalTrackUnpublished`.
- [ ] BE: nhánh `track_published`/`ScreenShare` trong `HandleWebhookAsync` + test.

### PR5 (tuỳ chọn) — gọi 1–1, **chỉ làm nếu nhóm giữ SCRUM-40**

Thiết kế tóm tắt, chi tiết hoá khi được duyệt:

- **REST:**
  - `GET /api/calls/contacts?q=&page=`: user đang hoạt động trừ mình; chỉ `id`, `fullName`.
  - `POST /api/calls/direct {calleeId}`: tạo `CallSession(Direct, Ringing)` + dòng caller; trả token
    phòng `direct-{sessionId}`.
  - `POST /api/calls/{id}/accept`: người nhận; khoá dòng, chỉ lần accept đầu thắng; `Active`; trả
    token.
  - `POST /api/calls/{id}/decline`, `/cancel`: `Missed`; `Declined = true`.
  - Bận (người nhận đang có dòng `call_participants` mở hoặc cuộc gọi `Ringing`) → `409`.
- **Hub `/hubs/calls`** — chỉ đẩy, không có method cho client gọi, giống `NotificationHub`:
  - sự kiện `incomingCall`, `callAccepted`, `callDeclined`, `callCancelled`, `callEnded`,
    `callAnsweredElsewhere`;
  - thêm `/hubs/calls` vào `OnMessageReceived`;
  - FE: `@microsoft/signalr`, export hàm lấy access token từ `client.ts`.
- **Đổ chuông:** job quét `Ringing` quá 30 giây → `Missed`.
- **Kết thúc:** một bên rời → `DeleteRoom` → `Ended`.
- **FE:** `IncomingCallDialog` gắn trong `AppShell`, cửa sổ gọi nổi (trạng thái, đồng hồ, mic,
  cúp máy), màn danh bạ.
- **Không làm:** chat 1–1, thông báo cuộc gọi nhỡ.

---

## 9. Kiểm thử

### 9.1 Backend (`tests/TrustCabinet.Web.Tests/Calls/`)

| File | Nội dung |
|---|---|
| `CallRoomNamesTests.cs` | round-trip; tên lạ, `meeting-` + Guid sai định dạng → false |
| `CallRoomRulesTests.cs` | bảng trạng thái × hình thức → `CallRoomStatus` |
| `CallRoomServiceTests.cs` | fake repository + fake gateway: mọi nhánh §5.5; token có identity = userId, room = `meeting-{id}`, metadata `meetingRole`; webhook joined hai lần → một dòng mở; left không có joined → không lỗi; room_finished → `Ended`; identity lạ/phòng lạ → bỏ qua; janitor: meeting `Ended` → `DeleteRoom` + end; roster → `RemoveParticipant`; bù joined/left bị mất |
| `CallEndpointsTests.cs` | theo mẫu `MeetingEndpointsTests` (factory con, fake, `ClientFor`): mọi status ở §5.6, **assert từng chuỗi `"MSGnn"`**; webhook chữ ký sai → 401; chưa cấu hình → 503 |
| `LiveKitWebhookParsingTests.cs` | tự ký header bằng `System.IdentityModel.Tokens.Jwt` (HS256, `iss` = api key, claim `sha256` = base64 SHA-256 của body, `exp` +5 phút); body sửa một byte → null; fixture `room_finished` có `roomEndReason` **phải parse được** (khoá lỗi SDK) |
| `CallRepositoryPostgresTests.cs` | theo `NotificationRepositoryPostgresTests`: hai `EnsureActiveRoomSessionAsync` song song → một dòng; `RecordJoin` idempotent; `EndSession` đóng mọi dòng mở |

Fixture `Calls/Fixtures/room_finished.json` (payload thật từ server v1.13.7, đã bỏ mật khẩu TURN):

```json
{"event":"room_finished","room":{"sid":"RM_X5eULkqkUoEu","name":"meeting-0192f5c4-8a4e-7d2b-9c1a-3f6e5d4c3b2a","emptyTimeout":300,"departureTimeout":20,"creationTime":"1790732775","creationTimeMs":"1790732775933","turnPassword":"redacted","enabledCodecs":[{"mime":"audio/opus"},{"mime":"video/VP8"}],"numParticipants":1},"id":"EV_oGbfc5Kvgok3","createdAt":"1790732822","roomEndReason":"ROOM_END_API_DELETE"}
```

Fixture phải chép vào output test (`<None Update="Calls/Fixtures/*.json"
CopyToOutputDirectory="PreserveNewest" />`), hoặc nhúng thẳng thành chuỗi trong test cho gọn.

### 9.2 Trình duyệt (skill `verify-ui-in-browser`)

- **Camera/mic giả:** MCP chrome-devtools cần thêm
  `--chromeArg=--use-fake-device-for-media-stream --chromeArg=--use-fake-ui-for-media-stream`
  trong `~/.claude.json` → `mcpServers.chrome-devtools.args`.
  - Đây là cấu hình máy của Hải: **hỏi trước khi sửa**, khởi động lại MCP sau khi sửa.
  - Muốn thử màn "bị chặn quyền" thì bỏ `--use-fake-ui-for-media-stream`.
- **Ba người cùng lúc:** `new_page` với `isolatedContext: "chutoa"`, `"thuky"`, `"thanhvien"`
  (refresh cookie dùng chung trong một context, nên mỗi người phải một context).

Kịch bản — ghi kết quả cụ thể vào mục "Tested locally" của PR:

1. Thành viên mở chi tiết cuộc họp `InProgress` → thấy "Vào phòng họp" → màn chờ thấy "Đang có 0
   người" → vào.
2. Chủ toạ vào → cả hai thấy nhau; chủ toạ nói (thiết bị giả phát tiếng bíp) → viền đang nói ở phía
   thành viên.
3. Chủ toạ bấm "Tắt mic" thành viên → phía thành viên thấy toast, icon mic tắt.
4. Thành viên (không phải chủ toạ) gọi thẳng API mute → `403` + mã đúng.
5. Người không được mời (`quantri@`) mở URL phòng → `NotFound`.
6. Chat: gửi từ chủ toạ, thư ký thấy; người vào sau không thấy tin cũ.
7. Mở phòng ở tab thứ hai cùng người → tab cũ hiện câu `DUPLICATE_IDENTITY`.
8. Đổi cuộc họp sang `Ended` trong DB → ≤ 30 giây mọi người thấy "Cuộc họp đã kết thúc".
9. `docker stop trustcabinet-livekit` khi đang trong phòng → banner đang kết nối lại → mất kết nối
   + "Vào lại"; `POST /call/join` lúc này vẫn trả token (LiveKit sống lại là vào được).
10. Tắt LiveKit config (xoá user-secret) → `POST /call/join` → `503` + câu `MsgCallUnavailable`.
11. Đo: control bar 72px, panel 360px, nút 36px, không có scroll ngang ở 1200px và 390px.

### 9.3 Load test lưới (PR3)

- Cài: `brew install livekit-cli`. Đặt `LiveKit:EnforceRoster=false`.
- Chạy `lk load-test` vào phòng `meeting-{id}` với vài publisher video
  (**CHƯA KIỂM** tên cờ — chạy `lk load-test --help` trước).
- Mở phòng bằng tài khoản thật: phân trang lưới, chuyển trang không giật, CPU trình duyệt chấp nhận
  được.

---

## 10. Bẫy đã biết

1. SDK .NET `WebhookReceiver.Receive` ném lỗi khi gặp field mới (`roomEndReason`) → dùng parser tự
   viết §5.4.6.
2. Đọc `[Obsolete]` (`NumDropped`, `TrackInfo.Simulcast`…) → CS0612 → CI đỏ.
3. `RoomServiceClient` không dùng chung giữa các request đồng thời.
4. API secret < 32 byte → SDK .NET ném `ArgumentException`.
5. Header webhook không có `Bearer `.
6. Webhook có thể mất → janitor là nguồn đúng cuối cùng; handler phải idempotent.
7. `participant_joined` chỉ bắn khi media đã thông (trạng thái `active`), trễ hơn lúc connect.
8. `adaptiveStream`, `dynacast` mặc định **false** ở JS.
9. `RoomOptions` phải là hằng số cấp module.
10. StrictMode: token lấy trong event handler; không tự `registerTextStreamHandler` trong effect.
11. Audio: thiếu `RoomAudioRenderer` thì không nghe ai; autoplay bị chặn cần `StartAudio`.
12. `node_ip: 127.0.0.1` chỉ cho dev cùng máy; thử bằng điện thoại trong LAN thì đặt IP LAN của
    Mac.
13. `docker compose` nội suy cả file: không dùng `${VAR:?}` cho biến chỉ service trong profile cần.
14. `MessageCodeCatalogueTests` chỉ quét `Endpoints/**`; mã phải là `private const string`; mỗi mã
    phải có test chứa `"MSGnn"`; "Mã tiếp theo" phải bằng mã lớn nhất + 1.
15. PR #41 đang sửa file meeting — không đụng; rebase trước khi tạo migration.
16. `TrustCabinetApiFactory` phải gỡ `CallRoomJanitor`, nếu không test sẽ gọi LiveKit thật.
17. Tắt mic (`setMicrophoneEnabled(false)`) là **mute** (track còn publish); tắt chia sẻ màn hình
    là **unpublish**.

---

## 11. Ghi chú Jira

Đã dán vào comment và mô tả từng ticket ngày 2026-09-30. Khi code khác kế hoạch, thêm comment mới
thay vì sửa comment cũ.

**SCRUM-39**
- Media dùng SFU LiveKit tự host (ADR-0010), không mesh.
- SDP/ICE đi qua LiveKit, **không qua hub SignalR** — lệch AC "Hub SignalR trao đổi SDP/ICE".
- Không có `/hubs/calls` cho phòng họp.
- Quản lý "ai đang trong phòng" = LiveKit + webhook + janitor đối soát 30 giây.
- Kết nối lại do `livekit-client` tự lo.
- Quyền vào phòng kiểm ở `POST /api/meetings/{id}/call/join`.
- TURN dùng bản nhúng của LiveKit thay coturn.
- Endpoint mới: `GET /api/meetings/{id}/call`, `POST …/call/join`, `POST /api/livekit/webhook`.
- Cần BA cập nhật: sequence diagram vào phòng, bỏ "SignalR signalling" khỏi kiến trúc.

**SCRUM-41**
- Phòng mở khi `InProgress` **và** hình thức `Online`/`Hybrid` (thêm điều kiện hình thức).
- "Được mời" = có phân công hiện hành trong thành phần tham dự. Không có uỷ quyền tham dự (BR-105).
- Chỉ chủ toạ tắt mic người khác, chỉ tắt không bật.
- Endpoint `POST …/call/participants/{userId}/mute`.
- Nếu nhóm đồng ý: chat tạm thời không lưu (Report 1 FE-08, UC-42).
- Phụ thuộc: UC-21 Start Meeting / UC-22 End Meeting. Không còn Meeting Session ([ADR-0013](../05-decisions/0013-mot-cuoc-hop-la-mot-buoi-hop.md)); điều kiện mở phòng là `Meeting.Status = InProgress`.

**SCRUM-42**
- Mặc định vào phòng mic bật, cam tắt.
- Lưới 9 ô/trang.

**SCRUM-43**
- Luật một người chia sẻ: client tự dừng + server tắt track qua webhook.
- Nhắc riêng tư bằng dialog trước khi chọn nội dung.

**SCRUM-40, SCRUM-44** — chờ quyết định nhóm (D8, D9); ghi rõ mâu thuẫn với Report 1 (không có gọi
1–1; LI-01 cấm ghi âm phía server).

---

## 12. Câu hỏi mở

1. Nhóm giữ hay bỏ SCRUM-40 (gọi 1–1)? Đóng hay sửa SCRUM-44 cho khớp LI-01?
2. Chat trong phòng: thêm ticket riêng hay gộp SCRUM-41? Có cần lưu lại không (hiện đề xuất
   không)?
3. ~~Ai làm vòng đời Meeting Session?~~ Đã bỏ Meeting Session ngày 09/10/2026 ([ADR-0013](../05-decisions/0013-mot-cuoc-hop-la-mot-buoi-hop.md)); phòng theo vòng đời cuộc họp.
4. Môi trường deploy thật (máy nào, domain nào)? Quyết định cấu hình TURN/TLS và `node_ip`.
5. Giới hạn số người một phòng: tạm 50 (`room.max_participants`).
6. Thư ký có được tắt mic người khác không? AC chỉ nói chủ toạ.

## Bổ sung Guest theo PO 13/10/2026

[ADR-0016](../05-decisions/0016-khach-tham-gia-nhu-thanh-vien.md) là policy đích: Guest xác minh email rồi tự khai tên/chức vụ, có media/chat/screen-share/connected-list như Member; không moderate. Identity room liên kết Guest invitation, không display name; rejoin tạo connection stint mới, không voter mới. UC-40 không đòi grant UC-27/30. Đây là yêu cầu bổ sung cho kế hoạch, chưa xác nhận các đoạn implementation trong tài liệu đã hỗ trợ Guest.

# ADR-0010: Phòng họp trực tuyến dùng LiveKit (SFU) tự host

- **Trạng thái:** Đã chấp nhận — phần gắn phòng họp với Meeting Session thay thế bởi ADR-0013
- **Ngày:** 2026-09-30
- **Người quyết định:** Hải Trần Việt, chờ nhóm SEP490-G116 duyệt qua pull request

## Bối cảnh

Report 1 (FE-07) cam kết một phòng họp trực tuyến chạy trên hạ tầng của chính tổ chức: âm thanh,
video, chia sẻ màn hình và chat. Report 2, rủi ro R01, đã lường trước rằng đây là phần media thời
gian thực đầu tiên của nhóm, và chọn hướng "build on proven open-source media server rather than
raw P2P".

Jira thì viết theo hướng khác:

- `SCRUM-39` giả định hub SignalR tự trao đổi SDP offer/answer và ICE candidate.
- `SCRUM-41` yêu cầu chốt giữa mesh và SFU **trước khi code**.

Mesh (mỗi máy nối tới mọi máy) chỉ chịu được khoảng 4–6 người có video. Nhóm muốn phòng họp đủ cho
cuộc họp nội bộ thông thường, vài chục người.

Ràng buộc:

- Đồ án 12 tuần, chưa ai làm WebRTC.
- Repo công khai, không được commit secret.
- Không phụ thuộc dịch vụ hội nghị bên ngoài (GAP-02, LI-05).
- Không ghi âm phía server (LI-01).

Report 3 thêm khái niệm **Meeting Session** (phiên họp): một cuộc họp có một hay nhiều phiên, ví
dụ phiên sáng 08:00–10:00 và phiên chiều 14:00–16:00.

- `Start Meeting Session` chuyển phiên `Planned → InProgress`. Phiên đầu tiên bắt đầu thì cuộc họp
  chuyển `Published → InProgress`.
- `Cancel Meeting Session` chuyển một phiên tương lai `Planned → Cancelled`; bản ghi được giữ lại để bảo toàn lịch sử lịch họp.
- `End Meeting Session` chuyển phiên `InProgress → Completed`. Cuộc họp chỉ chuyển `Ended` khi
  không còn phiên `Planned` hoặc `InProgress`.

Vì vậy giữa hai phiên, cuộc họp vẫn `InProgress` dù không ai đang họp.

### Thuật ngữ: hai loại "phiên"

| | `MeetingSession` — phiên họp | `CallSession` — lượt mở phòng gọi |
|---|---|---|
| Là gì | Khái niệm nghiệp vụ: một buổi trong cuộc họp | Khái niệm kỹ thuật: một lần phòng LiveKit hoạt động, từ người đầu tiên vào tới khi phòng đóng |
| Ai tạo | Thư ký tạo/lập kế hoạch; Chủ toạ bắt đầu / kết thúc phiên | Webhook và job đối soát của backend |
| Bảng | `meeting_sessions` (chưa có trong code) | `call_sessions`, `call_participants` |
| Quan hệ | Một phiên họp có **không, một hoặc nhiều** lượt mở phòng (LiveKit khởi động lại thì phòng mở lại) | Mỗi lượt mở phòng thuộc **đúng một** phiên họp |

Hai khái niệm không thay thế nhau. Tài liệu và code của phòng gọi chỉ dùng chữ "phiên" cho
`MeetingSession`, còn `CallSession` gọi là "lượt mở phòng".

## Quyết định

Chúng tôi sẽ dùng **LiveKit** — SFU mã nguồn mở (Apache-2.0) — **tự host** bằng container
`livekit/livekit-server` (v1.13.x), cho mọi cuộc gọi trong ứng dụng.

1. **Media và signalling đi qua LiveKit.** Trình duyệt dùng `livekit-client`. Backend không tự trao
   đổi SDP/ICE, và **phòng họp không có hub SignalR riêng** (`/hubs/calls`).
2. **Backend là ranh giới bảo mật.**
   - Chỉ backend giữ API secret và ký token vào phòng. Token được ký sau khi kiểm: tài khoản còn
     hoạt động, là thành phần tham dự (trừ `Delegated`), hình thức `Online`/`Hybrid`, và cuộc họp
     **đang có một phiên họp `InProgress`**. Chỉ `Meeting.Status = InProgress` là **không đủ**:
     giữa hai phiên cuộc họp vẫn `InProgress` nhưng phòng phải đóng.
   - Tên phòng (`meeting-{meetingId}`) và danh tính (`userId`) do server đặt, người gọi không chọn
     được. Mọi phiên của một cuộc họp dùng chung một tên phòng, vì giữa hai phiên phòng đóng.
     Thiết kế này tuân theo Report 3: **mỗi lúc một cuộc họp có tối đa một phiên `InProgress`** (BR-59). Nếu sau này cho phép hai phiên chạy song song thì tên phòng phải đổi thành theo phiên.
   - Tắt mic người khác (chỉ chủ toạ, chỉ tắt không bật), luật một người chia sẻ màn hình, và đá
     người mất quyền đều làm qua RoomService API của LiveKit từ backend.
3. **Trạng thái phòng lấy từ webhook, có đối soát.**
   - LiveKit báo người vào/ra và phòng đóng qua webhook có chữ ký. Webhook không đảm bảo tới nơi,
     nên một job nền đối soát mỗi 30 giây với danh sách thật trong phòng.
   - Cũng job đó đóng phòng khi không còn phiên họp nào `InProgress`. Nhờ vậy `End Meeting
     Session` không cần biết tới phòng gọi: kết thúc phiên xong, tối đa 30 giây sau phòng tự đóng.
   - `call_sessions` / `call_participants` được ghi từ đây. Mỗi cuộc họp có tối đa một lượt mở
     phòng `Active` (index duy nhất có điều kiện).
   - `call_sessions` có cột `meeting_session_id` (khoá ngoại tới `meeting_sessions`, nullable vì
     cuộc gọi 1–1 `SCRUM-40` không thuộc cuộc họp nào). Nhờ cột này, danh sách ai vào/ra phòng
     tách được theo từng phiên họp.
4. **TURN dùng bản nhúng sẵn của LiveKit**, không dựng coturn riêng. Dev chạy cùng máy nên không
   cần TURN. Môi trường thật cần domain và chứng chỉ TLS.
5. **Không có biến `VITE_*`, không hardcode địa chỉ.** Địa chỉ LiveKit cho trình duyệt nằm trong
   cấu hình backend (`LiveKit:PublicUrl`) và trả về kèm token.
6. **Backend vẫn chạy khi chưa cấu hình LiveKit.** Các endpoint gọi trả `503`, để người làm tính
   năng khác không phải dựng LiveKit. Container nằm trong profile `calls` của `docker-compose.yml`.
7. **Frontend** dùng `livekit-client` và hooks của `@livekit/components-react`. Không dùng gói giao
   diện của họ: màn hình tự vẽ theo quy ước của dự án.
8. **Tối thiểu dữ liệu cá nhân** (Nghị định 13/2023/NĐ-CP):
   - chỉ lưu *ai vào/ra phòng, lúc nào*;
   - không lưu media, không lưu chat (chat là LiveKit text stream, chỉ tới người đang trong
     phòng);
   - không dùng LiveKit Egress, không có bot ghi âm (LI-01).
9. Giới hạn **50 người một phòng** (`room.max_participants`), sửa được qua cấu hình.

Kế hoạch triển khai chi tiết: [08-ke-hoach-phong-hop-truc-tuyen.md](../02-design/08-ke-hoach-phong-hop-truc-tuyen.md).

## Phương án đã cân nhắc

| Phương án | Ưu | Nhược | Vì sao không chọn |
|---|---|---|---|
| Mesh P2P, SignalR làm signalling (như `SCRUM-39` giả định) | Không thêm server media; ít thành phần | Quá ~4–6 người có video là nghẽn uplink; tự viết đàm phán lại, kết nối lại, chọn thiết bị; không có simulcast | Không đạt "nhiều người"; Report 2 R01 đã loại raw P2P |
| **LiveKit tự host** (đã chọn) | SFU đã kiểm chứng (benchmark hãng: 150 người video trên một node 16 nhân); có sẵn TURN, simulcast/dynacast/adaptive stream, SDK trình duyệt, RoomService API, webhook; chạy một container, không cần Redis khi một node | Thêm một container và một secret; SDK .NET là bản cộng đồng, có lỗi khi đọc webhook (đã có cách tránh, xem kế hoạch §5.4) | — |
| mediasoup / Janus | Mạnh, linh hoạt | mediasoup là thư viện Node (phải viết server media riêng); Janus viết bằng C, cấu hình plugin phức tạp | Quá nhiều công cho đồ án 12 tuần |
| Nhúng Jitsi Meet | Có sẵn giao diện đầy đủ | Giao diện và xác thực riêng, khó gắn quyền theo thành phần tham dự và nhúng panel nghiệp vụ (ADR-0011); hệ thống nặng | Không kiểm soát được quyền vào phòng |
| Dịch vụ đám mây (LiveKit Cloud, Twilio, Agora) | Không phải vận hành | Media đi qua bên thứ ba | Trái GAP-02: cuộc họp phải chạy trên hạ tầng của tổ chức |

## Hệ quả

- **Tiêu chí nghiệm thu `SCRUM-39` đổi.** "Hub SignalR trao đổi SDP/ICE" thay bằng "backend cấp token
  + webhook + đối soát". Đã ghi vào ticket. Hub SignalR `/hubs/calls` chỉ còn cần nếu nhóm giữ gọi
  1–1 (`SCRUM-40`) — để đổ chuông.
- **Thêm việc vận hành.**
  - Container `livekit` (profile `calls`) và file `docker/livekit.dev.yaml`.
  - Secret `LIVEKIT_API_SECRET` trong `.env` và `LiveKit:ApiSecret` trong user-secrets — cùng một
    giá trị, ít nhất 32 ký tự.
  - Môi trường thật cần domain, TLS, và mở cổng 7881/TCP, 7882/UDP (hoặc TURN/TLS 443).
- **Điều kiện mở phòng theo phiên họp phụ thuộc bảng `meeting_sessions`, mà bảng này chưa có
  trong code.** Vòng đời phiên thuộc các UC `Create / Update / Cancel / Start / End Meeting Session` (Report 3).
  Hai UC này chưa có ticket Jira. Cho tới khi có:
  - PR hạ tầng (`SCRUM-39`) tạm mở phòng khi `Meeting.Status = InProgress`. Điều kiện nằm gọn
    trong `CallRoomRules.StatusOf`, `CallRepository.ListAllowedUserIdsAsync` và job đối soát;
  - ticket làm `meeting_sessions` phải đồng thời đổi ba chỗ đó sang "có phiên `InProgress`", thêm
    cột `call_sessions.meeting_session_id` bằng migration mới, và thêm test cho trường hợp giữa
    hai phiên. Mã `MSG65` ("phòng chưa mở") giữ nguyên nghĩa, chỉ đổi điều kiện.
- **Webhook không tin tuyệt đối.** Mọi xử lý webhook phải idempotent; job đối soát là nguồn đúng
  cuối cùng.
- **`SCRUM-59` (khách ngoài hệ thống) phải thiết kế thêm:**
  - khách vào phòng bằng token LiveKit chỉ nghe (`canPublish = false`, `canPublishData = false`);
  - LiveKit gửi tên mọi người trong phòng tới mọi người đăng ký — kể cả khách — trong khi
    ADR-0005 nói khách không thấy danh sách người dự. Phải quyết trước khi làm (ẩn danh hoá tên
    trong phòng có khách, hoặc chấp nhận và ghi ADR mới);
  - `CallParticipant.UserId` cho nullable như ADR-0005 đã nêu.
- **Ghi âm.** `SCRUM-44` (ghi cuộc gọi phía server) mâu thuẫn LI-01 nên không làm bằng Egress. Hướng
  hợp LI-01 nằm ở ADR-0011: thư ký ghi trên máy mình ngay trong phòng.
- **Kiểm thử cần** Chrome với thiết bị giả (`--use-fake-device-for-media-stream`) và nhiều phiên
  trình duyệt tách biệt; load test bằng `lk load-test`.

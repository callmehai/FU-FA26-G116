# ADR-0011: Tính năng dùng trong lúc họp là panel dùng chung cho màn chi tiết và phòng họp

- **Trạng thái:** Đề xuất
- **Ngày:** 2026-09-30
- **Người quyết định:** Hải Trần Việt, Gia Khánh Phạm; chờ nhóm SEP490-G116 duyệt qua pull request

## Bối cảnh

Các tính năng dùng trong lúc họp đang được làm ở **màn chi tiết cuộc họp** (các tab của
`MeetingDetailPage`): nghị sự (`SCRUM-25`), điểm danh (`SCRUM-26`), đăng ký phát biểu
(`SCRUM-27`), biểu quyết trực tiếp (`SCRUM-28`), tài liệu (`SCRUM-21`), ghi âm (`SCRUM-29`), ghi chú
của thư ký (`SCRUM-30`).

Với cuộc họp **trực tuyến**, người dự ngồi trong **phòng họp** (ADR-0010) — một màn toàn màn hình
giống Google Meet. Kết nối gọi nằm trong tab trình duyệt: chuyển tab khác vẫn nghe nói bình
thường, nhưng **chính tab đó chuyển sang trang khác là rớt cuộc gọi**. Nếu các tính năng họp chỉ có
ở màn chi tiết, người dự phải liên tục đổi tab để bỏ phiếu hay xem mục đang bàn.

Gia Khánh (BA) đề nghị đưa các tương tác vào trong phòng họp, nhưng màn ngoài vẫn phải có đủ dữ
liệu. Viết mỗi tính năng hai lần — một cho màn ngoài, một cho phòng họp — thì gấp đôi công sức, và
hai bản sẽ lệch nhau.

## Quyết định

Chúng tôi sẽ viết **mỗi tính năng dùng trong lúc họp thành một component "panel"**, code một lần.
Màn chi tiết hiện nó dạng tab, phòng họp hiện nó ở cột bên phải.

### 1. Màn chính theo hình thức họp

| Hình thức | Màn chính khi đang họp | Ghi chú |
|---|---|---|
| `InPerson` | Màn chi tiết | Không có phòng gọi |
| `Online` | Phòng họp | Màn chi tiết vẫn đọc được, dùng trước và sau buổi họp |
| `Hybrid` | Người ở xa: phòng họp. Người ngồi phòng thật: màn chi tiết trên máy mình | Hai màn cùng dữ liệu, cùng cập nhật realtime |

### 2. Hợp đồng component panel

```tsx
// src/features/<tính năng>/<TenTinhNang>Panel.tsx
export function VotingPanel({ meetingId, variant }: { meetingId: string; variant: 'page' | 'room' }) { … }
```

1. Nhận `meetingId`, **tự** gọi API qua `src/api/`, **tự** nghe cập nhật realtime qua hub
   `/hubs/meetings`. Không nhận dữ liệu từ trang cha.
2. Hiển thị tốt ở bề ngang **360px**. Không có `PageHeader` hay breadcrumb bên trong.
3. Thao tác dùng `Dialog`/`ConfirmDialog`, **không chuyển trang**. Với `variant = 'room'`, link sang
   màn khác (xem tài liệu, soạn ghi chú dài) mở **tab mới**.
4. Quyền (ai mở biểu quyết, ai bỏ phiếu, ai chuyển mục) do **server** quyết định và trả về. FE không
   tự suy từ vai trò. Server kiểm lại ở mọi request như mọi endpoint khác.
5. Hàm gọi API của tính năng nằm trong `src/api/<tính năng>.ts`, để phòng họp gọi lại được. Ví dụ:
   nút "Giơ tay" trên thanh điều khiển gọi đúng hàm đăng ký phát biểu của `SCRUM-27`.
6. **Dữ liệu theo phiên họp** (Meeting Session, Report 3 — xem thuật ngữ ở
   [ADR-0010](0010-phong-hop-truc-tuyen-dung-livekit-sfu.md)):
   - Tính năng có dữ liệu gắn với một phiên thì lưu kèm `meeting_session_id` ở phía server. Ứng
     viên: điểm danh, đăng ký phát biểu, ghi âm, và biểu quyết hay nghị sự nếu nghiệp vụ gắn theo
     phiên. BA chốt danh sách trong UC của từng tính năng.
   - Props **không** đổi: panel vẫn chỉ nhận `meetingId`. **Server** xác định phiên đang
     `InProgress`, trả kèm `meetingSessionId` trong response, và từ chối thao tác ghi khi không có
     phiên nào đang diễn ra. FE không tự chọn phiên để ghi.
   - Trong `variant = 'page'`, panel được phép cho xem lại dữ liệu của phiên đã `Completed`, với
     danh sách phiên lấy từ API.
   - Hub `/hubs/meetings` phát sự kiện bắt đầu / kết thúc phiên. Panel nghe sự kiện này để tải lại,
     còn phòng họp nghe để báo "Phiên họp đã kết thúc".

### 3. Realtime

`/hubs/meetings` là hub **dùng chung** cho nghị sự, phát biểu, biểu quyết, điểm danh, ghi âm:

- Client xin vào group của một cuộc họp; server chỉ cho khi người đó **xem được** cuộc họp
  (cùng quy tắc với `GET /api/meetings/{id}`). Server chỉ gửi sự kiện của cuộc họp tới group đó.
- Sự kiện cần mọi người chú ý phải có trên hub, để phòng họp hiện **thông báo nổi**:
  - mở biểu quyết → `Chủ toạ mở biểu quyết: … — Bỏ phiếu`;
  - mời phát biểu → báo riêng người được mời;
  - bắt đầu / dừng ghi âm;
  - bắt đầu / kết thúc phiên họp;
  - đổi mục đang bàn → header phòng hiện "Mục đang bàn: …".
- Ai làm tính năng có hub trước thì dựng hub, người sau dùng lại. Không mỗi tính năng một hub.

### 4. Phòng họp

- Trong phòng **không có link nào đưa tab hiện tại đi chỗ khác**. Rời phòng chỉ qua nút "Rời
  phòng" có xác nhận.
- Panel được thêm vào phòng khi component của tính năng đã có. Trước đó phòng có link "Mở chi tiết
  cuộc họp" (tab mới) để dùng tạm.
- Panel của phòng họp: Người dự, Chat (`SCRUM-41`); Nghị sự; Biểu quyết; Phát biểu; Điểm danh
  (cho thư ký và để người dự tự điểm danh); Tài liệu; Ghi chú (thư ký).

### 5. Ghi âm trong cuộc họp trực tuyến (đề xuất cho `SCRUM-29`)

Report 1 LI-01 cấm ghi âm phía server ("no recording bot joins the meeting room"). Trong cuộc họp
trực tuyến, thư ký bấm ghi âm **trong phòng họp**:

- trình duyệt của thư ký trộn micro của mình với âm thanh mọi người trong phòng (WebAudio +
  `MediaRecorder`), rồi tải file lên như luồng ghi âm hiện có;
- đó vẫn là "một người được chỉ định tự ghi trên thiết bị của mình" (FE-08), khớp LI-01;
- phòng họp cung cấp sẵn luồng âm thanh đã trộn cho component ghi âm (phối hợp `SCRUM-41` ↔
  `SCRUM-29`);
- trạng thái "đang ghi âm" phát qua `/hubs/meetings`, để **mọi người trong phòng, kể cả người vào
  sau**, thấy banner suốt thời gian ghi.

## Phương án đã cân nhắc

| Phương án | Ưu | Nhược | Vì sao không chọn |
|---|---|---|---|
| Mọi tương tác chỉ ở màn chi tiết, người dự tự đổi tab | Không phải làm gì thêm | Đổi tab liên tục; lỡ bấm link trong tab gọi là rớt cuộc gọi; bỏ lỡ biểu quyết vì không thấy | Trải nghiệm kém đúng lúc quan trọng nhất |
| Viết riêng giao diện cho phòng họp | Mỗi bên tối ưu riêng | Gấp đôi công sức; hai bản lệch nhau; tester phải thử hai lần | Tốn và dễ sai |
| Nhúng cả màn chi tiết vào phòng họp bằng `iframe` | Không phải sửa tính năng | Hai phiên đăng nhập và hai kết nối hub trong một trang; bố cục màn chi tiết không vừa 360px | Chắp vá |
| **Panel dùng chung** (đã chọn) | Code một lần, dữ liệu một nguồn, hai màn luôn khớp | Mỗi tính năng phải tuân hợp đồng panel | — |

## Hệ quả

- **Ticket đã được bổ sung tiêu chí nghiệm thu và kiểm thử** (mục "Bổ sung 2026-09-30"): `SCRUM-21`,
  `25`, `26`, `27`, `28`, `29`, `30`, cùng các ticket gọi `SCRUM-39`…`44`.
- **Skill [`frontend-ui-design`](../../.claude/skills/frontend-ui-design/SKILL.md)** có thêm quy tắc
  panel, để người và agent làm tính năng họp đều theo.
- **Hub `/hubs/meetings` thành phụ thuộc chung.** Ai làm trước dựng hub; tên sự kiện ghi vào
  `docs/03-api/` để người sau dùng lại.
- **`SCRUM-29` phải chốt với nhóm** hướng ghi âm ở mục 5. Nếu được duyệt thì `SCRUM-44` (ghi cuộc gọi
  phía server) nên đóng hoặc gộp vào `SCRUM-29`.

## Kiểm thử (cho tester)

Với mỗi tính năng dùng trong lúc họp, ngoài kịch bản trên màn chi tiết, thử thêm trên một cuộc
họp `Online` đang `InProgress`, có ít nhất ba người (chủ toạ, thư ký, thành viên), mỗi người một
phiên trình duyệt riêng:

1. **Làm được trong phòng họp.** Thao tác ngay trong panel, không rời phòng, cuộc gọi không rớt.
2. **Đồng bộ hai chiều.** Thao tác trong phòng → người đang mở màn chi tiết thấy cập nhật không
   cần tải lại, và ngược lại.
3. **Thông báo nổi.** Sự kiện cần chú ý (mở biểu quyết, mời phát biểu, bắt đầu ghi âm) hiện trong
   phòng với mọi người, kể cả người vào phòng **sau** khi sự kiện đã xảy ra (với trạng thái còn
   hiệu lực như "đang ghi âm").
4. **Quyền.** Người không có quyền không thấy nút, và gọi thẳng API thì bị server từ chối.
5. **Khung hẹp.** Panel ở bề ngang 360px không vỡ bố cục, không cuộn ngang.
6. **Link mở tab mới.** Mở tài liệu hay ghi chú dài từ trong phòng → tab mới, cuộc gọi ở tab cũ vẫn
   chạy.
7. **Theo phiên họp** (khi đã có `meeting_sessions`), với cuộc họp có hai phiên:
   - dữ liệu ghi ở phiên 1 không lẫn sang phiên 2;
   - giữa hai phiên, thao tác ghi bị từ chối và phòng họp đóng;
   - trên màn chi tiết vẫn xem lại được dữ liệu phiên 1.

# ERD dạng bảng

- [trustcabinet-erd-muc-tieu.drawio](trustcabinet-erd-muc-tieu.drawio) — source editable.
- [trustcabinet-erd-muc-tieu.drawio.png](trustcabinet-erd-muc-tieu.drawio.png) — ảnh xuất (có nhúng XML,
  mở lại được bằng draw.io).

Từ 04/11/2026 hình được **vẽ lại theo code**, giữ kiểu vẽ của bản trước (bảng trắng viền đen, crow's foot,
chỗ giao nhau vẽ cầu vượt). Hình có đủ 41 bảng trong `TrustCabinetDbContextModelSnapshot.cs` (đến migration
`GuestSpeechClips`, 03/11/2026): 35 thực thể và 4 bảng nối của [02-erd.md](../../02-design/02-erd.md) (kể
cả `DocumentReadConfirmation`, bản trước chưa vẽ), cộng 2 bảng kỹ thuật `MeetingInvitationDispatch` và
`GuestVerificationChallenge`. Hình dùng trong Report 3 §I.3.2. Góc nhìn Chen (không thuộc tính) ở
[../chen/](../chen/README.md) là mô hình đích 35 thực thể.

## Cách đọc

- Cột và kiểu lấy từ code. `?` là cho phép NULL; enum lưu dạng chuỗi nên ghi `enum`; `PK`, `FK`, `UK`
  (duy nhất trên đúng cột đó, không tính unique index có điều kiện `WHERE`). Hai cột `CreatedAt`,
  `UpdatedAt` mà mọi entity kế thừa từ `Entity` không vẽ.
- Mỗi cặp bảng có khoá ngoại được vẽ **một đường**, kể cả khi có nhiều FK giữa hai bảng (ví dụ
  `Meeting` → `User` gồm `CreatedById`, `AssignedApproverId`, `CancelledById`). Phía cha là `||` khi có ít
  nhất một FK bắt buộc, `o|` khi mọi FK đều được NULL. Phía con là `o<`, hoặc `o|` với quan hệ 1–0..1
  (`Transcript.RecordingId`, `MeetingSummary.MeetingId`).
- Đường tới `User` không vẽ cho 14 bảng chỉ ghi người thao tác: `CallSession`, `DocumentComment`,
  `MeetingDocument`, `MeetingGuestInvitation`, `MeetingMinutes`, `MeetingNote`, `MeetingSummary`,
  `Recording`, `SpeakingRequest`, `StoredFile`, `TranscriptSegment`, `TranscriptSpeaker`, `VoteChoice`,
  `VotingSession`. Đây là các đường bản trước cũng không vẽ. Các cột đó vẫn ghi `FK` trong bảng.

## Sinh lại

Hình sinh từ code bằng [scripts/draw_erd.py](../../../scripts/draw_erd.py), rồi chỉnh tay một số đường nối trong
draw.io (06/11/2026). Chạy lại script sẽ ghi đè các chỉnh tay đó: sau khi sinh lại, so ảnh với bản cũ và kéo lại
các đường cần thiết. Bảng xếp trên lưới
trong biến `GRID`. Hai bảng liên quan nằm ở hai ô kề nhau (kể cả kề chéo) được nối bằng một đường thẳng; hai bảng
cách nhau hai ô chỉ nối thẳng khi đường đó đứng hoặc ngang hẳn. Hai bảng thẳng hàng thì đường nối đứng/ngang tuyệt
đối, không xiên. Không đường thẳng nào cắt qua bảng khác; các đường còn lại chạy theo khe giữa các ô lưới. Mọi đầu
nối trên một cạnh bảng được chia đều, không hai đầu nào trùng chỗ. Lưới hiện tại (06/11/2026) đặt `Meeting`, `MeetingGuestInvitation` và `User` ở giữa, các bảng con
xếp quanh từng bảng đó. Khi schema đổi:

```bash
python scripts/draw_erd.py
"C:\Program Files\draw.io\draw.io.exe" -x -f png -e -s 2 -o docs/13-erd/muc-tieu/trustcabinet-erd-muc-tieu.drawio.png docs/13-erd/muc-tieu/trustcabinet-erd-muc-tieu.drawio
python .agents/skills/drawio-skill/scripts/repair_png.py docs/13-erd/muc-tieu/trustcabinet-erd-muc-tieu.drawio.png
```

Bảng mới phải được đặt vào một ô trống trong `GRID`; thiếu thì script dừng và báo tên bảng.
`python scripts/draw_erd.py --optimize` thử đổi chỗ các bảng để giảm số chỗ giao nhau, vẽ theo lưới tốt nhất
tìm được và in lưới đó ra; chép nó vào `GRID` để lần chạy sau ra cùng hình. Xem lại ảnh sau khi xuất; thêm hoặc bỏ thực thể thì rà cả Chen và `02-erd.md`.

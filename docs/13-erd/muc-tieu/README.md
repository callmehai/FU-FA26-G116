# ERD dạng bảng

- [trustcabinet-erd-muc-tieu.drawio](trustcabinet-erd-muc-tieu.drawio) — source editable.
- [trustcabinet-erd-muc-tieu.drawio.png](trustcabinet-erd-muc-tieu.drawio.png) — ảnh xuất (có nhúng XML,
  mở lại được bằng draw.io).

Hình **theo code** đến migration `RemoveDelegationLeftovers` (24/11/2026), trên bố cục sắp xếp tay của PR #272
(06/11/2026; khôi phục 22/11/2026 sau khi lần sinh lại ngày 15/11 ghi đè nó). Kiểu vẽ: bảng trắng viền đen, crow's foot,
chỗ giao nhau vẽ cầu vượt. Hình có đủ 41 bảng của `TrustCabinetDbContextModelSnapshot.cs` (`AiProviderSetting` không vẽ, và từ 27/11/2026 migration
`RemoveAiProviderSetting` đã xoá bảng, ADR-0088). Các thay đổi trước đó: thêm `CalendarShare` 10/11/2026 và hai cột thu hộ `CollectedById`, `MeetingId` của `voice_samples` 12/11/2026;
bỏ `MinutesSignature` 15/11/2026 khi migration `DropMinutesSignature` xoá bảng):
35 thực thể và 4 bảng nối của [02-erd.md](../../02-design/02-erd.md) (kể
cả `DocumentReadConfirmation`, bản trước chưa vẽ), cộng 2 bảng kỹ thuật `MeetingInvitationDispatch` và
`GuestVerificationChallenge`. Hình chưa đưa vào Report 3: §I.3.2 của Report 3 liệt kê 34 thực thể
(thiếu `CalendarShare`, chờ note BA Final Review của SCRUM-36/SCRUM-142; hai cột thu hộ
của `voice_samples` đã vào Entity 34). Góc nhìn Chen (không thuộc tính) ở
[../chen/](../chen/README.md) là cùng mô hình, 36 thực thể.

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

**Không chạy `scripts/draw_erd.py` đè lên file `.drawio` này.** Hình sinh từ script rồi được sắp xếp tay trong
draw.io (PR #272, 06/11/2026), nên vị trí bảng và đường nối không còn khớp `GRID`; chạy script sẽ xoá các chỉnh
tay đó (đã xảy ra ngày 15/11/2026). Khi schema đổi, sửa thẳng file `.drawio` trong draw.io: thêm/bớt hàng, đặt
bảng mới vào chỗ trống, kéo đường nối đi theo khe giữa các bảng. Muốn lấy sẵn hàng của bảng mới thì chạy script
trên một bản sao (sao lưu file trước, chạy, chép file sinh ra ra chỗ khác, trả lại bản cũ) rồi chép các ô cần dùng
sang. Phần dưới mô tả script. Bảng xếp trên lưới
trong biến `GRID`. Hai bảng liên quan nằm ở hai ô kề nhau (kể cả kề chéo) được nối bằng một đường thẳng; hai bảng
cách nhau hai ô chỉ nối thẳng khi đường đó đứng hoặc ngang hẳn. Hai bảng thẳng hàng thì đường nối đứng/ngang tuyệt
đối, không xiên. Không đường thẳng nào cắt qua bảng khác; các đường còn lại chạy theo khe giữa các ô lưới. Mọi đầu
nối trên một cạnh bảng được chia đều, không hai đầu nào trùng chỗ. Lưới hiện tại (06/11/2026) đặt `Meeting`, `MeetingGuestInvitation` và `User` ở giữa, các bảng con
xếp quanh từng bảng đó. Xuất ảnh sau khi sửa `.drawio`:

```bash
"C:\Program Files\draw.io\draw.io.exe" -x -f png -e -s 2 -o docs/13-erd/muc-tieu/trustcabinet-erd-muc-tieu.drawio.png docs/13-erd/muc-tieu/trustcabinet-erd-muc-tieu.drawio
python .agents/skills/drawio-skill/scripts/repair_png.py docs/13-erd/muc-tieu/trustcabinet-erd-muc-tieu.drawio.png
```

Bảng mới phải được đặt vào một ô trống trong `GRID`; thiếu thì script dừng và báo tên bảng.
`python scripts/draw_erd.py --optimize` thử đổi chỗ các bảng để giảm số chỗ giao nhau, vẽ theo lưới tốt nhất
tìm được và in lưới đó ra; chép nó vào `GRID` để lần chạy sau ra cùng hình. Xem lại ảnh sau khi xuất; thêm hoặc bỏ thực thể thì rà cả Chen và `02-erd.md`.

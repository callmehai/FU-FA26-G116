# Đồng bộ sơ đồ lên Drive

Một đầu mối cho việc đưa sơ đồ từ repo lên thư mục **Diagrams** trên Google Drive. Áp dụng cho
`scripts/push_diagrams.py` và toàn bộ ảnh/source bên dưới.

## Nguyên tắc

- Chiều dữ liệu là **một chiều: repo → Drive**. Repo là nguồn duy nhất; không tải ảnh từ Drive về
  ghi đè file local, không sửa trực tiếp trên Drive rồi chép ngược vào repo.
- Sửa sơ đồ bằng source trong repo (`.drawio`, `.txt` sequence, `.mmd`), xuất lại ảnh, rồi mới đẩy.
- File đã có trên Drive được **thay nội dung tại chỗ**: giữ nguyên file ID, tên, thư mục cha và link.
  Không tạo bản sao, không xoá rồi tạo lại.
- File local chưa có trên Drive được tạo mới trong đúng thư mục. Thư mục feature chưa có (ví dụ
  `Document Management`, `Voting Management`) cũng được tạo.
- File chỉ có trên Drive mà repo không còn thì **để nguyên và chỉ liệt kê**. Chỉ khi truyền
  `--prune --confirm` mới chuyển chúng vào thùng rác Drive. Phạm vi dọn là các thư mục đang được map
  và vẫn còn file local; thư mục không còn file local nào (hoặc thư mục không có trong bảng đích)
  không bị đụng tới.
- Push cần credential Drive (scope `https://www.googleapis.com/auth/drive`) trong môi trường, cùng
  cách với report: đặt `GOOGLE_DRIVE_REFRESH_TOKEN` cùng `GOOGLE_DRIVE_CLIENT_ID` và
  `GOOGLE_DRIVE_CLIENT_SECRET` — tạo một lần bằng `python scripts/google_drive_token.py` — hoặc
  `GOOGLE_DRIVE_ACCESS_TOKEN` cho một lần chạy. Kiểm tra bằng
  `python scripts/google_drive_token.py --check`. Không commit token; không dùng `drive.file` vì các
  file đích có từ trước, không do client này tạo. Chi tiết và lưu ý hết hạn token ở
  [08-reports/README.md](08-reports/README.md#quy-trình-một-lần-sửa).

## Đích trên Drive

Thư mục gốc: [Diagrams](https://drive.google.com/drive/folders/18E52RJTqrrd3j9D9ROJllVC2kAvtOHJx).

| Nhóm (`target`) | Nguồn local | Thư mục Drive | Folder ID |
|---|---|---|---|
| `sequence` | `docs/06-sequencediagram/` | Sequence diagram | `18oxf85AjCDC16443ceP1Ok_LA2lfojab` |
| `class` | `docs/07-classdiagram/` | Class diagram | `1nctA4Ffgewj_iFy2qe8KvVyeyLVx1Oui` |
| `context` | `docs/11-contextdiagram/` | Context diagram | `1FzilzjQjoQtbNscTVoPiA1N-sWMMkD0y` |
| `erd` | `docs/13-erd/muc-tieu/`, `docs/13-erd/chen/` | ERD | `1S-3R7ys_ymQEC9E9Zkw3AHztoOjnR4lB` |
| `main-flow` | `docs/09-bussinessflow/` | Main Flow | `1sgtXubMouySCezShiYC8PYtab7OJ2A2K` |
| `screen-flow` | `docs/12-screenflow/` | Screen flow | `1FI9ytaVh7zNZmW4oflbcO874rdHEwtgr` |
| `uc` | `docs/14-uc-diagram/` | UC diagram | `1YPCo8mUbu8QCS_el_M_SbnSzHfsqWPKf` |

`docs/10-statemachine/` chưa có thư mục tương ứng trên Drive nên chưa nằm trong phạm vi đồng bộ.
`docs/13-erd/hien-tai/` là snapshot schema đang chạy, sinh lại từ database, không phải nguồn nghiệp
vụ: nó **không** được đẩy lên Drive và cũng không có thư mục đích. Chi tiết ba bản ERD ở
[13-erd/README.md](13-erd/README.md).

### Tên file lệch giữa local và Drive

Hai chiều xử lý khác nhau, tuỳ bên nào đang giữ tên đúng:

- `file_map` / `dir_map`: **Drive giữ tên cũ**, local đặt tên riêng cho dễ đọc. File giữ nguyên tên
  trên Drive, không sinh bản trùng.
- `renames`: **Drive đổi sang tên local**. Script tìm file theo tên cũ, cập nhật nội dung, rồi PATCH
  `name` — file ID, thư mục cha và link không đổi. Dùng khi tên trên Drive là lỗi chính tả.

Tên local → tên Drive đang dùng (`file_map`, `dir_map`):

| Local | Drive |
|---|---|
| `06-sequencediagram/auth/` | `Authentication/` |
| `06-sequencediagram/user-management/` | `User Management/` |
| `06-sequencediagram/role-management/` | `Role & Permission Management/` |
| `06-sequencediagram/meeting-management/` | `Meeting Management/` |
| `06-sequencediagram/agenda-management/` | `Agenda Management/` |
| `06-sequencediagram/meeting-participant-management/` | `Meeting Participant Management/` |
| `06-sequencediagram/notification-management/` | `Notification Management/` |
| `06-sequencediagram/document-management/` | `Document Management/` |
| `06-sequencediagram/voting-management/` | `Voting Management/` |
| `06-sequencediagram/drawio-png/` | `drawio/` |
| `06-sequencediagram/TrustCabinet_Sequence_Diagrams.drawio` | `drawio/TrustCabinet_Sequence_Diagrams.drawio` |
| `14-uc-diagram/uc-chair.png` | `trustcabinet-use-cases-Chair.drawio.png` |
| `14-uc-diagram/uc-guest.png` | `trustcabinet-use-cases-Guest.drawio.png` |
| `14-uc-diagram/uc-meeting-approver.png` | `trustcabinet-use-cases-Meeting Approver.drawio.png` |
| `14-uc-diagram/uc-meeting-organizer.png` | `trustcabinet-use-cases-Meeting Organizer.drawio.png` |
| `14-uc-diagram/uc-participant.png` | `trustcabinet-use-cases-Participant.drawio.png` |
| `14-uc-diagram/uc-secretary.png` | `trustcabinet-use-cases-Secretary.drawio.png` |
| `14-uc-diagram/uc-system-admin.png` | `trustcabinet-use-cases-System Admin.drawio.png` |

Tên trên Drive đã được sửa lại theo local (áp dụng 25/10/2026):

| Local | Tên cũ trên Drive | Sau khi push |
|---|---|---|
| `13-erd/muc-tieu/trustcabinet-erd-muc-tieu.drawio.png` | `trustcabinet-erd-muc-tieu .drawio.png` | `trustcabinet-erd-muc-tieu.drawio.png` |

Dấu cách trước `.drawio.png` là lỗi từ lần xuất trước; file giữ nguyên ID nên link cũ vẫn mở đúng.
Sau khi đổi tên xong, entry tương ứng đã được bỏ khỏi `Source.renames` (cơ chế vẫn còn cho lần sau),
và `--check` thấy `same` cho tên mới, không còn file nào tên cũ trên Drive.

Các tên còn lại giữ nguyên. Script bỏ qua `README.md`, `state-machines.json` và các file không nằm
trong pattern của nhóm (`.drawio`, `.png`, `.txt`, `.pdf`).

## Cách dùng

Chạy từ thư mục gốc repo:

```powershell
# Xem các nhóm và thư mục local, không gọi Drive
python scripts/push_diagrams.py --list

# Xem Drive có khớp local không; lệch thì exit 1
python scripts/push_diagrams.py all --check
python scripts/push_diagrams.py sequence --check

# Dry run: in ra file nào create / update / unchanged / rename, không ghi gì
python scripts/push_diagrams.py sequence
python scripts/push_diagrams.py sequence/document-management
python scripts/push_diagrams.py all

# Ghi thật
python scripts/push_diagrams.py sequence --confirm
python scripts/push_diagrams.py all --confirm

# Dọn file chỉ còn trên Drive (mặc định chỉ liệt kê)
python scripts/push_diagrams.py sequence --prune
python scripts/push_diagrams.py sequence --prune --confirm
```

`target` nhận một trong ba dạng:

- tên nhóm: `sequence`, `class`, `erd`, `main-flow`, `screen-flow`, `context`, `uc`, hoặc `all`;
- `nhóm/đoạn-đường-dẫn`: `sequence/auth/UC01`, `class/feature-07`;
- một đoạn tên bất kỳ: `UC32a`, `BF-02`.

`--all` tương đương target `all`. `--check` và `--confirm` không dùng cùng nhau.

## Script làm gì

1. Dựng danh sách file local theo manifest `GROUPS` ở đầu `scripts/push_diagrams.py`.
2. Liệt kê thư mục Drive tương ứng, so `md5Checksum` với MD5 của file local.
3. In kế hoạch: `same`, `update`, `create`, kèm nhãn `[rename from ...]` khi file phải đổi tên trên
   Drive, và mục `remote-only` nếu có file chỉ còn trên Drive.
4. Với `--confirm`: tạo thư mục/file còn thiếu, PATCH nội dung file đã có, rồi đọc lại metadata và
   kiểm tra MD5 đúng bằng file local. Sai thì dừng, không coi là thành công.
5. Với `--confirm` trên file đã có, không gửi `name`/`parents`/`mimeType` nên không thể đổi tên,
   di chuyển hay đổi loại file ngoài ý muốn. Ngoại lệ duy nhất là file khai báo trong `renames`:
   sau khi cập nhật nội dung, script PATCH riêng trường `name`; ID, thư mục cha và link giữ nguyên.
6. `--check` trả exit 1 khi còn `create`, `update` **hoặc** `rename` chưa thực hiện, nên dùng được
   trong CI hay trước khi họp.

## Hiện trạng (25/10/2026)

Đã chạy `all --confirm` bằng credential thật và `all --check` trả **UP TO DATE: 202 file, 0 create,
0 update, 0 rename, không còn file remote-only**. Chi tiết lần đồng bộ đầu tiên:

- `sequence`: 162 file — 86 khớp sẵn, 47 update, 29 tạo mới (5 cặp UC của Document Management:
  UC-32a, UC-32c, UC-33a, UC-35, UC-74; UC-73, UC-75, UC-76a, UC-76b, UC-77; 9 ảnh trong `drawio/`).
  4 file UC-04 cũ (`RequestGuestVerificationCode`, `VerifyGuestInvitation`) đã được đưa vào thùng rác
  Drive; bản local gộp thành `UC04_AccessMeetingViaGuestInvitation` đã có trên Drive.
- `class`: 16 file — 4 khớp, 10 update, 2 tạo mới (FE-07 document management); thư mục `draft/` là
  bản cũ, không do script quản lý nên vẫn để nguyên.
- `erd`: 4 file — tạo `trustcabinet-erd-muc-tieu.drawio`, update source Chen, và **đổi tên tại chỗ**
  ảnh mục tiêu từ `trustcabinet-erd-muc-tieu .drawio.png` sang `trustcabinet-erd-muc-tieu.drawio.png`
  (giữ ID/link). Không file nào của `13-erd/hien-tai` được đẩy lên.
- `context` 2 update, `main-flow` 8 (3 khớp + 5 update), `screen-flow` 2 update, `uc` 8 (6 khớp +
  2 update: `trustcabinet-use-cases.drawio`, `uc-chair.png`).

Lần push bị đứt kết nối giữa đường một lần; script dừng sạch và chạy lại thì tiếp tục đúng chỗ, nên
các lần sau cứ chạy lại cùng lệnh. `--check` là nguồn xác nhận, số ở đây chỉ để biết quy mô.

## Khi thêm sơ đồ mới

- Thêm file vào đúng thư mục local; nếu là feature mới của sequence thì thêm một dòng vào `dir_map`
  với tên thư mục Drive mong muốn.
- Nếu tên file trên Drive phải khác local, thêm vào `file_map` của `Source` tương ứng.
- Nếu tên trên Drive đang sai và cần sửa cho giống local, thêm vào `renames`; đừng xoá rồi upload
  lại, vì làm vậy là mất file ID và link đang được chia sẻ.
- Chạy `python scripts/push_diagrams.py <nhóm>` để xem dry run trước, rồi `--confirm`.
- Không sửa `GROUPS` để trỏ sang folder Drive khác nếu chưa đổi cả bảng đích ở trên và ghi chú lý do.

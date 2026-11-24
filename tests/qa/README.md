# QA theo tiêu chí chấp nhận (auto QA local)

Bộ chạy kiểm thử như một tester độc lập: mỗi tiêu chí chấp nhận (AC) của một ticket được kiểm trên app thật, theo
từng vai trò, trên máy tính và điện thoại, và để lại ảnh làm bằng chứng. Cách làm lấy từ con QA bot mà nhóm của anh
Quang dùng: kiểm từng tiêu chí bằng trình duyệt thật, mỗi tiêu chí một ảnh, kết luận Đạt / Lỗi / Chưa kết luận, ghi
rõ đã kiểm trên commit nào, và lỗi nào cũng có các bước, mong đợi, thực tế.

Agent làm theo skill [qa-ticket](../../.claude/skills/qa-ticket/SKILL.md): đọc ticket, đặc tả UC, BR để viết tiêu chí
và bảng quyền theo vai trò, viết kế hoạch vào `plans/`, chạy, xem từng ảnh rồi mới kết luận, và báo lên Jira. Không
chạy trong CI: cần app đang chạy.

## Chạy

```bash
# Dựng stack local cho agent (dùng đúng container sẵn có, không tạo mới):
powershell -ExecutionPolicy Bypass -File scripts/agent-up.ps1
# app phải đang chạy (backend :5225, frontend :5173) — xem .claude/skills/verify-ui-in-browser §1
cd tests/qa
npm ci                                   # lần đầu
node run.mjs plans/smoke.mjs             # kiểm bộ chạy, app và tài khoản seed
node run.mjs plans/scrum-33.mjs          # một ticket
node run.mjs plans/scrum-33.mjs --only AC2,AC5 --headed
node run.mjs plans/smoke.mjs --base http://localhost:5174   # Vite chạy cổng khác
```

Bộ chạy từ chối chạy khi `/api/health` ở địa chỉ đó không phải của TrustCabinet (một dev server khác giữ cổng 5173
trả 200 cho mọi đường dẫn). Server test chung chỉ chạy kế hoạch **chỉ đọc**.

## Kết quả

`out/<ticket>/<giờ>-<commit>/`, không commit:

| Tệp | Dùng để |
|---|---|
| `report.html` | Mở bằng trình duyệt: bảng tiêu chí, kết quả, ảnh bằng chứng bấm để phóng to |
| `summary.md` | Bình luận `[qa-bot]` dán lên Jira: bảng kết quả và từng lỗi (các bước, mong đợi, thực tế, ảnh) |
| `results.json` | Dữ liệu thô |
| `NN-<vai trò>-<AC>-<thiết bị>-<mô tả>.png` | Ảnh bằng chứng, đánh số theo thứ tự chụp |

Mã thoát: 0 khi mọi tiêu chí đạt hoặc chưa kết luận, 1 khi có lỗi, 2 khi không chạy được.

## Bộ chạy tự bắt

- **Mỗi tiêu chí một phiên trình duyệt riêng**, đăng nhập đúng vai trò của nó: tiêu chí sau không ăn theo phiên của
  tiêu chí trước.
- **Gọi ra ngoài:** trang gọi tới host khác app (ngoài Google Fonts, Google Sign-In) thì tiêu chí bị tính Lỗi, vì CSP
  của server test sẽ chặn đúng lời gọi đó.
- **Lỗi JavaScript chưa bắt** trên trang cũng tính Lỗi.
- Lỗi thì tự chụp thêm ảnh `failure` (trừ tiêu chí chỉ gọi API, chưa mở trang nào).
- Ảnh chụp sau khi hiệu ứng chạy xong, không dính lúc control đang mờ dần.
- Request trong iframe Google Sign-In không bị tính là gọi ra ngoài: CSP của app không áp dụng trong iframe đó.

Bộ chạy đạt chưa phải là kết luận: agent phải xem từng ảnh. Ảnh sai (chữ bị cắt, hiện mã thô, sai số liệu) thì đổi
thành Lỗi.

## Kế hoạch

Mỗi ticket một tệp `plans/<ticket>.mjs`, giữ trong repo để chạy lại sau khi sửa lỗi (kiểm thử hồi quy). Mẫu:
[plans/smoke.mjs](plans/smoke.mjs). Tài khoản seed: `admin` (quantri@), `chair` (chutoa@), `secretary` (thuky@),
`member` (thanhvien@), mật khẩu dev `TrustCabinet@2026` (đổi bằng `QA_PASSWORD`). Dữ liệu kế hoạch tạo ra đặt tên
`[QA] …` để tìm và xoá.

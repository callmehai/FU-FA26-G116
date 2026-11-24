# Nguồn test case Report 5.3 System Test

Nội dung của [Report5.3_System_Test_FRs.xlsx](../../Report5.3_System_Test_FRs.xlsx). Mỗi workflow end-to-end là một file JSON
và một sheet `WorkFlowN`. Workbook được sinh từ thư mục này: không sửa tay file Excel, kể cả để ghi kết quả.

| Đường dẫn | Vai trò | Ai sửa |
|---|---|---|
| `plan.json` | Danh sách workflow (WF-01 → WF-10), tên sheet, flow BF gốc, mô tả, tiền điều kiện, và các nhóm (bước) mỗi workflow phải có, theo thứ tự | Chỉ người điều phối |
| `WF-xx.json` | Case của một workflow | Người nhận workflow đó |
| `preview/WF-xx.md` | Bản Markdown sinh ra để đọc trong PR | Không sửa tay |

Cả mười workflow đã có case và đã chạy Round 1; [WF-02.json](WF-02.json) (Chuẩn bị và duyệt họp) là mẫu để đọc trước.

## Lệnh

Tại thư mục gốc repository (đóng Excel trước khi build):

```powershell
python scripts/test_reports/system_cases.py check              # kiểm tra; ERROR phải sửa, TODO là nhóm chưa có case
python scripts/test_reports/system_cases.py build              # check + preview + in số case từng sheet (không ghi Excel)
python scripts/test_reports/system_cases.py build --overwrite  # sao lưu .bak-… rồi sinh lại workbook
```

Không commit file `.bak-…`.

## Workflow là gì, khác 5.2 ở đâu

5.2 kiểm **một chức năng**. 5.3 đi **một luồng nghiệp vụ trọn vẹn qua nhiều Feature và nhiều người dùng**, theo bốn
swimlane [BF-01 → BF-04](../../../../09-bussinessflow/README.md) và sáu luồng lớn ngoài BF. Mỗi workflow là một câu chuyện
liên tục: case sau dùng dữ liệu case trước để lại (`TC02` làm trên cuộc họp `TC01` tạo ra). Chạy theo thứ tự; nếu một
case Failed thì các case phụ thuộc phía sau ghi `N/A` kèm lý do trong `note`.

## Cách viết một case

```json
{
  "id": "SYS_WF02_TC05",
  "description": "Organizer submits the prepared meeting to the Approver.",
  "procedure": ["As the Organizer open the TC01 meeting and click \"Gửi duyệt\".", "..."],
  "expected": ["Toast \"Đã gửi cuộc họp để duyệt.\"; status \"Chờ duyệt\" ...", "..."],
  "preconditions": ["TC01 to TC04 passed."],
  "aiNote": "BF-02 · UC-18 · BR-33 · MSG78 · 5.2: MEET_FT41 · Evidence: QA run SYS-WF02 20261123-064947"
}
```

- Viết bằng tiếng Anh; chữ trên giao diện giữ nguyên tiếng Việt trong ngoặc kép, chép đúng từ màn hình đang chạy.
- `id` = `SYS_WF<nn>_TC<nn>`, đánh số liên tục qua cả workflow (không đánh lại theo nhóm). `check` báo nếu lệch.
- `groups` phải đúng danh sách và thứ tự trong `plan.json`. Thiếu bước thì báo người điều phối, không tự thêm nhóm.
  Một nhóm có thể có nhiều case (happy path trước, nhánh lỗi chính sau).
- `procedure`: thao tác người dùng thật trên app, mỗi bước một câu, nói rõ **ai** làm (Organizer, Approver, Member A…).
  Có bước đổi người dùng thì dùng trình duyệt thứ hai.
- `expected`: điều nhìn thấy được, kiểm được ở mỗi bên: toast, trạng thái, chuông thông báo, email, lịch, lịch sử.
  Hiệu ứng xuyên Feature là trọng tâm của 5.3.
- `preconditions`: case nào trước đó phải Passed, và dữ liệu hay tài khoản cần thêm.
- Không có trường `note`: cột Note của workbook để trống (`check` chặn nếu có).
- `aiNote`: bắt đầu bằng BF của workflow, rồi `UC · BR · MSG · 5.2: <ID case 5.2 liên quan>`, ai chạy (người hay
  `QA runner`), mã lượt chạy, môi trường và lý do mọi thay đổi. Workbook đặt nó ở cột **AI context** (cột P) bị ẩn, bên phải Note: để agent và reviewer đọc, không hiện
  khi in báo cáo.
- Role theo [ADR-0075](../../../../05-decisions/0075-bo-vai-tro-he-thong-chu-toa-thu-ky.md): vai trò hệ thống chỉ có
  Thành viên, Phê duyệt viên, Quản trị viên; Chủ toạ và Thư ký là vai trò **trong cuộc họp**.
- Cấm: `Automated:`, tên test method, đường dẫn `tests/`, `frontend/src`, `backend/`. 5.3 mô tả hệ thống đang chạy, không
  mô tả code test. `check` chặn những chữ này.
- Không viết case cho chức năng đã bỏ: ký biên bản (SCRUM-37), danh sách bình luận của Thư ký (SCRUM-106), dashboard cá nhân
  (UC-69), khoá tài khoản khi sai mật khẩu (UC-09).

## Chạy test và ghi kết quả

1. Chạy trên server test (`app.trustcabinet.workspace-hub.space`) hoặc bản local của cùng commit `app`; ghi commit hoặc bản
   deploy vào `note` nếu khác lần trước.
2. Mỗi case chạy xong, ghi vào JSON của case:

   ```json
   "round1": "Passed", "date1": "2026-11-25", "tester1": "<tên viết tắt>"
   ```

   `round2/date2/tester2`, `round3/…` cho các vòng sau. Giá trị hợp lệ: `Passed`, `Failed`, `Pending`, `N/A`.
   Chưa chạy thì để trống (workbook hiện `Pending`).
3. `Failed` bắt buộc có `Bug: SCRUM-xxx` (hoặc lý do ngắn nếu chưa có ticket) trong `aiNote`. `N/A` ghi lý do trong `aiNote`.
   Ai chạy, môi trường và commit `app` đã test cũng ghi trong `aiNote`.
4. Ảnh bằng chứng để ngoài repo hoặc trong `tests/qa/out/` (đã gitignore); không nhúng vào workbook.
5. `check`, `build --overwrite`, rồi commit cả JSON, preview và workbook trong cùng một commit.

Chỉ một lần chạy thật trên app mới được đánh `Passed`/`Failed`. Không suy kết quả từ test tự động của 5.1/5.2.

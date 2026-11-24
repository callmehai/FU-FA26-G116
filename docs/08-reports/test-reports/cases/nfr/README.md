# Nguồn test case Report 5.4 System Test (NFRs)

Nội dung của [Report5.4_System_Test_NFRs.xlsx](../../Report5.4_System_Test_NFRs.xlsx): kiểm thử **yêu cầu phi chức năng**
của [Report 3 §IV.2 Quality Attributes](../../../Report-3_Software_Requirement_Specification.md#iv-non-functional-requirements).
5.3 ([../system/](../system/README.md)) kiểm yêu cầu chức năng (FRs) theo luồng nghiệp vụ; 5.4 kiểm hệ thống *tốt đến mức
nào*: bảo mật, tốc độ, dễ dùng, độ tin cậy, sẵn sàng. Workbook sinh từ thư mục này: không sửa tay file Excel, kể cả để ghi kết quả.

| Đường dẫn | Vai trò | Ai sửa |
|---|---|---|
| `plan.json` | Các sheet, layout, tiền tố ID, và danh sách yêu cầu Report 3 mà mỗi sheet phải phủ | Chỉ người điều phối |
| `<Sheet>.json` | Case của một sheet | Người nhận sheet đó |
| `preview/<Sheet>.md` | Bản Markdown sinh ra để đọc trong PR | Không sửa tay |

Mẫu đã viết: [Security.json](Security.json) (4 case) và [Performance.json](Performance.json) (4 case). Usability, Reliability,
Availability mới có file rỗng. Maintainability, Portability và Compliance của Report 3 không có sheet: chúng được chứng minh
bằng CI, tài liệu và cách triển khai, không bằng một lần chạy trên app.

## Lệnh

Tại thư mục gốc repository (đóng Excel trước khi build):

```powershell
python scripts/test_reports/nfr_cases.py check              # ERROR phải sửa; TODO là yêu cầu Report 3 chưa có case
python scripts/test_reports/nfr_cases.py build              # check + preview + in số case từng sheet (không ghi Excel)
python scripts/test_reports/nfr_cases.py build --overwrite  # sao lưu .bak-… rồi sinh lại workbook
```

Không commit file `.bak-…`. Template là `scripts/test_reports/templates/nfr-template.xlsx` (hai sheet mẫu `GWT` và `Metric`
lấy từ mẫu môn học).

## Hai layout

**`gwt`** (Security, Usability, Reliability, Availability): một kịch bản Given / When / Then.

```json
{
  "id": "TC-SEC-003",
  "requirement": "Role-based access control and least privilege",
  "scenario": "Member cannot read a meeting they are not invited to by guessing its URL",
  "role": "Member (not a participant)",
  "given": "A published meeting of which Member D is not a participant; its URL copied ...",
  "when": "Signed in as Member D, open the copied URL; then call GET /api/meetings/{id}",
  "then": "The page shows the same not-found screen ...; the API answers 404",
  "priority": "Critical",
  "aiNote": "Measured: meeting 404 = unknown id 404 ... · Evidence: QA run SYS-NFR 20261123-070126"
}
```

**`metric`** (Performance): một chỉ số đo được so với mục tiêu.

```json
{
  "id": "TC-PERF-002",
  "requirement": "Document upload and preview",
  "scenario": "Upload a 10 MB PDF to a meeting and open its preview",
  "tool": "Manual (browser DevTools)",
  "target": "Listed ≤ 5 s after transfer; preview opens ≤ 3 s",
  "condition": "One upload at a time; transfer time excluded"
}
```

## Cách viết một case

- `requirement` chép **đúng tên dòng** trong Report 3 §IV.2, đúng như trong `plan.json`; `check` báo nếu sai hoặc nếu một yêu
  cầu chưa có case nào. Một yêu cầu có thể có nhiều case.
- `target`, `then` lấy số từ Report 3 (3 s, 10 MB, 5 lần/phút…), không tự đặt số khác. Report 3 sai so với app thì ghi
  `Fail` hoặc báo người điều phối, không sửa mục tiêu cho khớp.
- `id` = `TC-<tiền tố>-<nnn>`, liên tục trong sheet (`TC-SEC-001`, `TC-SEC-002`…).
- `priority` (gwt): `Critical`, `High`, `Medium`, `Low`.
- Viết bằng tiếng Anh; chữ trên giao diện giữ nguyên tiếng Việt trong ngoặc kép. Role theo
  [ADR-0075](../../../../05-decisions/0075-bo-vai-tro-he-thong-chu-toa-thu-ky.md).
- Cấm `Automated:`, đường dẫn `tests/`, `frontend/src`, `backend/`: 5.4 mô tả hệ thống đang chạy. Lượt chạy CI, coverage
  thuộc 5.1/5.2, không ghi kết quả 5.4 từ đó.

## Chạy test và ghi kết quả

Thêm vào JSON của case:

```json
"status": "Pass", "date": "2026-11-25", "tester": "<tên viết tắt>", "actual": "2.1 s (slowest of 5)"
```

- `status`: `Not Run` (mặc định), `Pass`, `Fail`, `Blocked`, `Skip` — giống dropdown của mẫu.
- `Pass`/`Fail` cần `date`, `tester`; sheet `metric` cần thêm `actual` (số đo thật, kèm đơn vị).
- `Fail` cần `defect` (`SCRUM-xxx` hoặc lý do ngắn). `Blocked`/`Skip` ghi lý do trong `aiNote` (ví dụ chưa có k6, chưa
  có môi trường 100 người dùng).
- **Cột Notes để trống**; không có trường `note` (`check` chặn nếu có). Mọi điều cần ghi — ai chạy, số đo, mã lượt chạy,
  môi trường, lý do — vào `aiNote`; đạt khi điều kiện test chưa đủ thì mở đầu bằng `Partial pass: …`. Cùng dòng Report 3,
  `condition`, ngày và người test, `aiNote` nằm ở cột **AI context** bị ẩn ngay bên phải Notes: để agent và reviewer đọc,
  không hiện khi in báo cáo.
- Chạy trên server test hoặc bản local cùng commit `app`; ghi môi trường vào `aiNote`. Ảnh để trong `tests/qa/out/`
  (đã gitignore).
- `check`, `build --overwrite`, rồi commit JSON, preview và workbook trong cùng một commit.

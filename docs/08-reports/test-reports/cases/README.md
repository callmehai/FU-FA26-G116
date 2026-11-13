# Nguồn test case Report 5.1 / 5.2 / 5.2.2

Nội dung của ba workbook nằm ở đây, **mỗi Feature một thư mục**: ai nhận FE nào thì chỉ sửa trong
`FE-xx/` của mình. Workbook được sinh từ thư mục này, không sửa tay file Excel. Cách viết case và
cách chia việc: [AGENTS.md](../AGENTS.md).

| Đường dẫn | Vai trò |
|---|---|
| `sheets.json` | Kế hoạch 5.2 / 5.2.2: danh sách sheet theo Feature, thứ tự, và các nhóm (*group*) mỗi sheet phải có — UI theo chức năng, API theo *endpoint* |
| `functions.json` | Kế hoạch 5.1: mỗi production function một sheet, các method (nhóm) của nó |
| `routes.json` | Mọi route `/api` backend đang map, lấy từ bảng route khi app chạy (`integration_cases.py routes`) |
| `FE-xx/ui.json` | Case UI của Feature → `Report5.2_Integration_Test.xlsx` |
| `FE-xx/api.json` | Case API của Feature → `Report5.2.2_Integration_Test_API_Test.xlsx` |
| `FE-xx/unit/<sheet>.json` | Các dòng UTC của một sheet unit → `Report5.1_Unit_Test.xlsx` |
| `preview/FE-xx/` | Bản Markdown sinh ra để đọc trong PR — không sửa tay |

Sinh lại, tại thư mục gốc repository (đóng Excel trước):

```powershell
python scripts/test_reports/integration_cases.py build   # 5.2 + 5.2.2
python scripts/test_reports/unit_cases.py build          # 5.1
```

Kế hoạch (`sheets.json`, `functions.json`) chỉ người điều phối sửa; người viết case không thêm,
đổi tên hay đổi thứ tự sheet và nhóm.

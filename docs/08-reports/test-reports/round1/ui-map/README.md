# Bảng ánh xạ AC sang case UI, bản nháp, mỗi Feature một file

Thư mục này chứa bảng ánh xạ giữa AC trong `tests/qa/plans/fe-NN.mjs`
(mỗi AC một phiên trình duyệt riêng) và case trong
`docs/08-reports/test-reports/cases/FE-NN/ui.json` (Report 5.2).

- `D` = một AC phủ đủ case; `P` = AC chỉ phủ một phần (ví dụ chỉ nhánh
  API, hoặc thiếu một bước); `N` = chưa có AC nào. Mức xét theo **thân
  hàm `run()` của AC**, không chỉ tiêu đề.
- Cột `Nhóm` lấy theo `plan-ui.md`: `A` = agent chạy được,
  `AX` = cần chuẩn bị thêm (hai phiên, chèn lỗi, chờ thời gian),
  `H` = người làm. `H*`/`AX*` = case lạ mà plan chưa liệt kê, người
  chạy xác nhận lại (đúng như plan dặn).
- Không sửa `ui.json`, không chạy plan, không commit từ task này.

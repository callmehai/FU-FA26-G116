# Hình dùng trong report

Nguồn `.drawio` và bản `.png` xuất ra của các hình chèn vào DOCX trong [`source/`](../README.md). Sửa hình thì sửa
`.drawio`, rồi xuất lại PNG bằng draw.io desktop:

```powershell
& "C:\Program Files\draw.io\draw.io.exe" -x -f png -s 3 -b 10 -o <tên>.png <tên>.drawio
```

`-s 3` giữ chữ nét khi hình được thu về bề rộng trang. Sau khi xuất, đẩy lên Drive bằng `python scripts/push_diagrams.py report-figures --confirm` (thư mục *Diagrams › Report
figures*; link dưới mỗi hình trong DOCX trỏ tới bản này), thay hình trong DOCX rồi sinh lại snapshot theo
[README của 08-reports](../../README.md).

| Tệp | Hình |
|---|---|
| `report2-org-chart.drawio` / `.png` | Report 2, Figure 2: Project organisational chart |
| `report2-iterative-model.drawio` / `.png` | Report 2, Figure 1: Phases of the Iterative Incremental Model (vẽ lại 25/11/2026 thay ảnh JPG mờ lấy từ web) |

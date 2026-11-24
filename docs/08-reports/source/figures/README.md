# Hình dùng trong report

Nguồn `.drawio` và bản `.png` xuất ra của các hình chèn vào DOCX trong [`source/`](../README.md). Sửa hình thì sửa
`.drawio`, rồi xuất lại PNG bằng draw.io desktop:

```powershell
& "C:\Program Files\draw.io\draw.io.exe" -x -f png -s 3 -b 10 -o <tên>.png <tên>.drawio
```

`-s 3` giữ chữ nét khi hình được thu về bề rộng trang. Sau khi xuất, thay hình trong DOCX rồi sinh lại snapshot theo
[README của 08-reports](../../README.md).

| Tệp | Hình |
|---|---|
| `report2-org-chart.drawio` / `.png` | Report 2, Figure 2: Project organisational chart |

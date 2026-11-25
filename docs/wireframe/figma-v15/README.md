# Plugin Figma v15

Plugin dựng lại các màn của [TrustCabinet-wireframe-v15.html](../TrustCabinet-wireframe-v15.html) (25/11/2026, đánh số lại nhóm chức năng: FE-15 Báo cáo thành FE-14, FE-16 Lịch làm việc & tìm giờ họp thành FE-15) trong Figma Desktop. Chạy trên file đang ở v14 thì plugin xoá các frame 15.x, 16.x cũ và dựng 20 màn theo số mới; 116 màn khác giữ nguyên.

- `manifest.json` — khai báo plugin, import qua **Plugins → Development → Import plugin from manifest…**
- `code.js` — dữ liệu các màn (`LAYOUT`) và code dựng màn; sinh bằng `_tools/mk_plugin.js`, không sửa tay.

Cách tạo, chạy thử và đưa vào Figma: [README của wireframe](../README.md) mục 7 và 9.

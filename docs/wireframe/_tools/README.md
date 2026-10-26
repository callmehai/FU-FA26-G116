# Script sinh plugin và ảnh wireframe

Sinh plugin Figma (`figma-v<N>/`) và ảnh `screens/` từ file HTML wireframe; không sửa HTML.

- `run.mjs` — mở HTML bằng Chrome headless: `extract` trích layout, `shots` chụp ảnh từng màn.
- `extract.js` — chạy trong trang, đọc DOM thành cây layout cho Figma.
- `mk_plugin.js` — so layout bản mới với bản cũ, tạo `figma-v<mới>/`.
- `mock.js` — chạy thử plugin trên Figma giả lập.
- [out/](out/README.md) — layout đã trích của từng phiên bản.

Quy trình đầy đủ: [README của wireframe](../README.md) mục 3–8.

# NOTE — Sửa plan QA round 1 cho khớp sản phẩm + việc còn lại cho reviewer

Ngày: 25/11/2026. Chạy trên `origin/app` @ `60389fc5` (không đụng code backend/frontend — diff `backend frontend`
giữa bản chạy và `origin/app` mới nhất trống). Stack: Postgres/Redis/SeaweedFS/Mailpit (docker),
LiveKit (profile calls), speech worker `:8090`, backend + frontend build sẵn trên `:5225`
(`npm run build`, không cần Vite).

## 1. Kết luận sau khi rà từng fail

**Chưa xác nhận bug sản phẩm nào.** Mọi fail đã rõ nguyên nhân đều thuộc một trong ba loại:

- **Plan cũ** (seed/spec đổi mà plan chưa theo: ADR-0075 bỏ vai trò hệ thống Chủ toạ/Thư ký,
  Thành viên seed có `MeetingCreator`; PR #430 bỏ chọn sẵn thẻ "Họp hôm nay"): sửa plan, chạy lại xanh.
- **Harness race** (đọc state trước khi async xong: reorder, drag-keyboard, `unroute` trước `continue`):
  sửa plan bằng cách chờ điều kiện thật, chạy lại xanh.
- **Kẹt môi trường** (thiếu máy AI Ollama `:11434`, thiếu fixture âm thanh nói thật, cần mailbox thật,
  cần 2 browser/2 người thật): giữ `inconclusive` hoặc fail có ghi lý do, KHÔNG sửa plan cho qua.

Hai điểm reviewer nên tự xem (không kết luận vội):

- `fe-08 AC30` (chấm "Đang nói" trong phòng): lần chạy cuối rớt đúng lúc backend chết vì hết RAM
  → kết quả vứt đi, chưa có verdict sạch.
- `fe-10 AC23` (bấm mốc giờ nghe từ câu đó): có bằng chứng "không có audio response nào được gọi"
  (file missing, không phải player bug). Nếu chạy lại mà audio 200 mà nút vẫn không chuyển "Tạm dừng"
  thì mới là bug sản phẩm (nghi `TranscriptView.tsx`/`RecordingsPanel.tsx`).

## 2. Các fix plan đã làm (12 file, xem `git show` để đối chiếu)

- `fe-01`: AC17 expect `['MeetingCreator']` (seed cho, `ReferenceData.cs:58`); AC21 inconclusive khi lần 6
  trả 200 (dev config `SignInPerEmail=1000`, chỉ Staging mới 429); fix race `unroute` sau khi response về.
- `fe-02`: `ROLE.secretary` → id + tên Phê duyệt viên (vai trò Thư ký hệ thống đã xoá).
- `fe-03`: `BUILT_IN.secretary.name` → `approver` (2 chỗ, plan đã tự ghi ADR-0075 mà còn sót).
- `fe-04`: AC6 thanhvien@ → 404 (có MeetingCreator nên qua cửa, 404 đúng luật giấu tồn tại);
  AC28 member mới có nút "Tạo cuộc họp", đổi vai sang Phê duyệt viên (id cũ đã xoá).
- `fe-05`: AC21 build PDF inline (fixture `e2e/...` không tồn tại); AC22 drain meeting dùng chung trước khi
  assert trống; AC41 chờ `agenda-item-dragging` sau Space + poll snapshot tới khi order mới về.
- `fe-06`: AC29 chỉ bấm "Tuần sau" khi thẻ chưa hiện sau 10s (tránh race list chưa tải).
- `fe-09`: `SAID_WAV` bớt một `'..'` (file ở `tests/qa/fixtures`); AC13/21/22 dùng `said.wav` TTS thật thay cho
  tone tổng hợp (`s1/s2.pcm` worker không chuyển được → transcript rỗng/`204` đúng thiết kế);
  AC19/25/34 trỏ/nhấn vào nút `(i)` (`.recording-meta .info-tip`) thay vì dòng chữ (TooltipLayer chỉ đáp
  ứng nút, đúng `InfoTip.tsx` + ADR-0052).
- `fe-10`: AC12 tab "Người khác thu hộ" + bỏ hardcode notice `1.3`; AC20 message key theo DB thật
  + goto thẳng (phone không có sidebar) và bỏ assert sai; `openCollection` search trước khi bấm
  (AC16/51-56); `backdateConsent` đúng DB `trustcabinet` + fail rõ thay vì nuốt lỗi; AC1/4/5/6/7/9/2
  lấy chữ dò từ transcript API thay vì hardcode tiếng Việt (worker đổi output là rớt oan);
  AC10 viết lại theo UC-55c (hệ thống tự chọn lý do, không hỏi); AC18 enrol2 loop gấp đôi
  (tempo 1.35x làm mẫu ngắn dưới cổng VAD 6s); AC25 API dùng stub https `:11439` thay vì http
  (backend bắt https); AC43/45 scope nút "Tạo lại" vào `#summary-ask`.
- `fe-11`: AC13 `exact:true` cho radio "Thông qua"; AC31 expect 1 checked (mặc định đã check) thay vì 0;
  AC33 thu hồi khách mời trong `finally` (khách tồn làm eligible 2/3 → 2/5 ở các AC sau);
  đã thu hồi tay 2 khách tồn trên meeting live.
- `fe-12`: AC5 tách fixture (`api-draft-export`) khỏi AC4 (chung meeting làm bẩn content).
- `fe-14`: AC3 theo PR #430 (mở list không lọc, không thẻ nào pressed; bấm thẻ rồi mới so).
- `fe-16`: AC4 member direct share → 201 MSG441 (có MeetingCreator) + dọn share đã có trước khi assert;
  AC18 end thủ công → `Ended`, assert tuần hết giờ bận + nút người chia sẻ (nếu còn) luôn tắt —
  chip "Đã dừng" chỉ dành cho stop tự động (service `EndAsync` → `Ended`);
  AC19 role `'Thư ký'` → `'Thành viên'`; AC24 nút "Tạo cuộc họp" hiện là đúng (ADR-0075);
  AC25 ngày động theo `WED` thay vì hardcode 16/12.

## 3. Reviewer chạy nốt (backend + docker đã có, xem §5 dựng stack)

```bash
cd <worktree-sach>
# A. Đã sửa plan, chạy lại để xanh (không cần AI, không cần mailbox):
node run.mjs plans/fe-08.mjs --only AC30 --base http://localhost:5225
node run.mjs plans/fe-01.mjs --only AC21 --base http://localhost:5225   # mong đợi: inconclusive (dev limit 1000)
node run.mjs plans/fe-10.mjs --only AC18,AC23,AC27 --base http://localhost:5225
# B. Kẹt máy AI (cần Ollama + RAM ~5GB trống, xem §5): chạy khi đủ máy
ollama serve   # model qwen2.5:7b-instruct đã có sẵn trong ~/.ollama
node run.mjs plans/fe-10.mjs --only AC25,AC34,AC35,AC36,AC37,AC38,AC39,AC43,AC44,AC45,AC59,AC60 --base http://localhost:5225
node run.mjs plans/fe-12.mjs --only AC21,AC22,AC24 --base http://localhost:5225
# AC40-42 fe-10 đã inconclusive đúng (thiếu draft Ready), tự hết khi B xanh.
# C. Full regression các plan đã sửa (sau A+B), vẫn --base http://localhost:5225:
for p in fe-01 fe-02 fe-03 fe-04 fe-05 fe-06 fe-09 fe-11 fe-12 fe-13 fe-14 fe-16; do node run.mjs plans/$p.mjs --base http://localhost:5225; done
# fe-07, fe-15 sạch từ đầu, không đụng tới. fe-08 (trừ AC30), fe-10 (trừ nhóm B) chạy full khi rảnh.
# D. Cần người thật (không tự động được, giữ inconclusive): AC12 fe-02 (link email), AC23-24 fe-08
#    (hộp thư + kiểm tay), AC16/AC17 fe-02, AC4 fe-01, các AC "2 browser thật" (manual-r1, fe-05 AC29/30).
```

## 4. Bằng chứng đang ở đâu

`tests/qa/out/<TICKET>/<timestamp>-60389fc5/` — `report.html` (mở bằng browser), `summary.md` (dán Jira),
ảnh `*-failure.png`. Lần chạy đã sửa xong có timestamp mới nhất; lần chạy dính crash hạ tầng
(`ECONNREFUSED`, backend chết) đã ghi rõ trong log và KHÔNG dùng.

## 5. Lưu ý hạ tầng (gặp 3 lần trong đợt này)

- RAM: máy còn ~600MB trống là Ollama nạp model 4.6GB → backend bị kill (exit 1, không báo lỗi).
  Chạy nhóm B phải dọn RAM trước (Edge/VS Code/Discord đang ăn nhiều GB).
- Disk C từng đầy 0 byte: `backend-qa*.log` (EF info, đã xoá; khi chạy nhớ
  `Logging__LogLevel__Default=Warning`), `out/` ~500MB giữ lại làm evidence, đừng xoá.
- Backend từng start sai content-root (chạy `dotnet run` từ worktree root thì `:5225/` 404):
  luôn start từ `backend/TrustCabinet.Web`.
- `docker start trustcabinet-postgres trustcabinet-redis trustcabinet-files e2e-mailpit trustcabinet-livekit`
  (volume `trustcabinet_*` giữ nguyên DB; đừng `compose up` tạo project mới làm lệch volume).

## 6. Round 2 chiều 25/11 — AC18/23/30 xanh, nhóm AI chốt thiếu RAM (không chạy)

- AC18 xanh (SCRUM-197/071949): enrolled B đúng giọng voice-2 thì matcher gợi ý B, score 0.9956,
  tooltip "Khớp giọng nhất". Fix: enrol2 = p1.mp3 loop 6 lần qua đúng chain `low` của s2a
  (cùng một giọng với voice-2 trong transcript); bản munchkin-said cũ VAD từ chối (422 too_short),
  bản rubberband tuy qua gate (speech 12.9 s) nhưng matcher không nhận (suggestion rỗng) — bằng chứng
  trong log. Lookup giọng trong plan bỏ hardcode "Tôi đề nghị…/Kính thưa…" (wording trôi theo worker),
  lấy giọng khác giọng câu đầu; expect chữ "tên đã gán" thêm flag /i (dialog viết hoa Chữ).
- AC23 xanh (SCRUM-197/071140): nguyên nhân là bytes 2 bản ghi meeting A mất từ trước round 1
  (backend 500 FileNotFoundException trong App_Data, log backend-qa3 đã có) — player `err: 4`,
  không phải bug player. Đã xoá 2 hàng hỏng qua API (MSG296), plan tự tải lại + gỡ băng lại.
  Probe "no audio response" cũ trong plan là sai (audio nạp lúc mở dòng, seek không gọi mới) —
  đã thay bằng đọc trạng thái element audio.
- AC30 xanh 2/2 (SCRUM-195/071335): path fixture sai `tests/fixtures` → `tests/qa/fixtures`
  (cùng lỗi SAID_WAV đợt 1); assertion nới thành "Thư ký xuất hiện chấm trong 60 s" vì mic giả
  của người xem cũng chớp "đang nói" (desktop chớp đúng người xem trước, phone đúng Thư ký).
- AC45: fix plan sót — scope nút "Tạo lại" vào `#summary-ask` (3 chỗ, strict-mode 2 nút).
- Nhóm AI fe-10 trong lượt nhiễu (backend chết 07:21–07:27 vì hết RAM, ollama bị tắt 07:26 để cứu
  backend): 10 pass (AC25, AC34, AC35, AC37 desktop+phone, AC38, AC39, AC43, AC44);
  AC36/59/60 fail trong cửa outage → reviewer chạy lại (không kết luận bug).
  Run out/SCRUM-197/20261125-072019-9289d8ae BỎ, không dùng.
- fe-12 AI: 4 inconclusive đúng (AC24 cần macOS `say`, máy Windows không có).
- Hạ tầng round 2: model qwen2.5:7b (4.7GB) + backend + browser không vừa RAM còn lại (~2.8GB,
  app dev ăn hết); backend bị kill 2 lần (BACKEND5_EXIT=1, BACKEND6_EXIT=1, log chỉ có 3 dòng,
  health check ECONNREFUSED). Ollama đã tắt để cứu backend; reviewer chạy nhóm AI phải dọn
  6GB+ RAM trống trước (tắt Edge/VS Code/Discord/Zalo), start `ollama serve`, kiểm tra
  `/api/tags` rồi mới chạy. Backend start từ `backend/TrustCabinet.Web`, kiểm tra `/api/health`.
- Reviewer chạy (mỗi lệnh có `--base http://localhost:5225`):
  `node run.mjs plans/fe-10.mjs --only AC36,AC45,AC59,AC60`,
  `node run.mjs plans/fe-12.mjs --only AC21,AC22,AC24` (AC24 sẽ inconclusive trên Windows,
  cần macOS mới xanh), rồi full regression §3C. Ghi kết quả vào bảng §4 (report.html mới nhất).

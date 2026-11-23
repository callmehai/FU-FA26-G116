# Kế hoạch chạy Round 1 — Report 5.2 (test tích hợp qua giao diện)

> Ghi chú ngày 23/11/2026: bộ case đã thu gọn còn **UI 556 case** (commit `03594e25`). Con số 875 case và các bảng phân nhóm A/AX/H trong tài liệu này là số trước thu gọn, chỉ giữ lại để tham khảo cách chia nhóm. Nguồn chính xác là `cases/FE-xx/ui.json` và `sheets.json` (`integration_cases.py check` 0 lỗi).

Phân việc cho 875 case trong `cases/FE-xx/ui.json`. Chia ba nhóm theo cách chạy. Máy phân nhóm bằng từ khoá trong bước và kết quả mong đợi, nên **người chạy xác nhận lại khi gặp case lạ**. Tester Round 1: **Phạm Gia Khánh** (chịu trách nhiệm kết quả, kể cả case do agent chạy).

| Nhóm | Ai chạy | Cách chạy | Số case |
|---|---|---|---:|
| **A** | agent | Playwright một người dùng bằng runner `tests/qa/` (desktop 1200 và điện thoại 390), có ảnh bằng chứng | 672 |
| **AX** | agent, cần chuẩn bị thêm | Cần hai phiên đăng nhập song song, chèn lỗi backend (503) hoặc chờ thời gian. Agent làm được nếu dựng sẵn kịch bản; làm sau nhóm A | 152 |
| **H** | **người** | Cần Google thật, hộp thư email thật, micro/loa/camera thật, giọng nói thật hoặc đánh giá bằng mắt | 51 |
| | | **Tổng** | **875** |

## Cách điền kết quả

1. Chạy từng case, ghi vào case trong JSON: `"round1": "Passed"` hoặc `"Failed"` (hoặc `"N/A"`), `"date1": "YYYY-MM-DD"`, `"tester1": "Phạm Gia Khánh"`.
2. Case `Failed` hoặc cần bằng chứng: bổ sung vào cuối trường `note` (cột *Actual result / Evidence / Defect*) đường dẫn ảnh hoặc mã ticket Jira. Không sửa code để case xanh: lỗi thì ghi `Failed`.
3. Chạy `python scripts/test_reports/integration_cases.py build` để dựng lại workbook 5.2. Không gõ kết quả vào Excel, vì lần build sau sẽ mất.
4. Case chưa chạy thì không điền `round1`. Không đánh `Passed` nếu chưa chạy thật.

## Cách phân test UI

Đã có sẵn bộ QA Playwright `tests/qa/plans/fe-01.mjs` … `fe-16.mjs` (kiểm theo tiêu chí chấp nhận, gọi là **AC**, mỗi AC một phiên trình duyệt riêng, desktop và điện thoại, có ảnh và `report.html`). Dùng lại bộ này, không viết lại. Chỗ lệch: plan đánh số theo AC (`AC1`…), còn Report 5.2 đánh số theo case trong `ui.json` (`MEET_FT01`…), và chưa có bảng ánh xạ giữa hai bên.

Mỗi Feature là một gói cho một agent, làm ba bước:

1. **Cập nhật plan theo hành vi mới ngày 19–20/11/2026.** Danh sách mở không lọc (#430), Thư ký được bắt đầu họp (chỉ Chủ trì kết thúc), tab Biên bản khoá đến khi họp kết thúc, tải tệp 10 MB / 100 MB, lịch chia sẻ 7 ngày 00:00–24:00 và ô tìm giờ chọn được thứ Bảy/Chủ nhật. Không cập nhật thì plan báo `Failed` oan.
2. **Chạy plan của Feature** (`cd tests/qa; node run.mjs plans/fe-04.mjs`) trên desktop và điện thoại. Agent xem từng ảnh rồi mới kết luận. Cần app đang chạy: backend :5225, frontend :5173, PostgreSQL (xem `.claude/skills/verify-ui-in-browser`).
3. **Ánh xạ vào `ui.json`.** Với mỗi case: nếu một AC phủ đúng thì điền `round1`, `date1`, `tester1` và đường dẫn ảnh vào `note`. Nếu AC chỉ phủ một phần thì agent mở rộng AC (thêm bước vào plan) rồi chạy lại. Nếu chưa AC nào phủ thì thêm AC mới vào plan; riêng case thuộc nhóm H (danh sách bên dưới) thì để người làm. Case chưa chạy thì để trống `round1`.

Tương ứng với API: ở API là "viết test xUnit rồi đổi `Manual only` sang `Automated:`", ở UI là "viết AC trong plan Playwright rồi dẫn kết quả vào case".

Thứ tự: FE-04, FE-12, FE-10, FE-07, FE-16, rồi các Feature còn lại. Làm API trước vì chạy nhanh.

### Bảng ánh xạ mẫu — FE-04 (74 case; bản nháp, agent xác nhận lại)

`D` = một AC phủ đủ, `P` = AC chỉ phủ một phần (ví dụ chỉ nhánh API, hoặc một bước), `N` = chưa có AC. Tổng: **21 D, 20 P, 33 N**. Case ghi "phải sửa" nghĩa là AC hiện tại kiểm hành vi cũ.

| Case | Mức | AC |
|---|---|---|
| MEET_FT01 | D | AC1 |
| MEET_FT02 | N | - |
| MEET_FT03 | N | - |
| MEET_FT04 | D | AC2 |
| MEET_FT05 | N | - |
| MEET_FT06 | P | AC3 |
| MEET_FT07 | P | AC3 |
| MEET_FT08 | N | - |
| MEET_FT09 | D | AC4 |
| MEET_FT10 | N | - |
| MEET_FT11 | N | - |
| MEET_FT12 | D | AC5 |
| MEET_FT13 | D | AC5 |
| MEET_FT14 | P | AC6 |
| MEET_FT15 | D | AC15 |
| MEET_FT16 | P | AC16 |
| MEET_FT17 | N | - |
| MEET_FT18 | P | AC7 |
| MEET_FT19 | N | - |
| MEET_FT20 | P | AC7 |
| MEET_FT21 | N | - |
| MEET_FT22 | D | AC23 (phải sửa: không còn mở sẵn Họp hôm nay) |
| MEET_FT23 | N | - |
| MEET_FT24 | N | - |
| MEET_FT25 | P | AC27 |
| MEET_FT26 | N | - |
| MEET_FT27 | N | - |
| MEET_FT28 | P | AC27 |
| MEET_FT29 | D | AC21, AC24 |
| MEET_FT30 | P | AC24 |
| MEET_FT31 | N | - |
| MEET_FT32 | N | - |
| MEET_FT33 | P | AC21 |
| MEET_FT34 | D | AC25 |
| MEET_FT35 | D | AC25 |
| MEET_FT36 | D | AC21, AC22 |
| MEET_FT37 | N | - |
| MEET_FT38 | N | - |
| MEET_FT39 | N | - |
| MEET_FT40 | N | - |
| MEET_FT41 | D | AC8 |
| MEET_FT42 | P | AC8 |
| MEET_FT43 | N | - |
| MEET_FT44 | N | - |
| MEET_FT45 | P | AC9 |
| MEET_FT46 | P | AC11 |
| MEET_FT47 | N | - |
| MEET_FT48 | N | - |
| MEET_FT49 | D | AC10 |
| MEET_FT50 | D | AC11 |
| MEET_FT51 | D | AC11 |
| MEET_FT52 | P | AC12 |
| MEET_FT53 | N | - |
| MEET_FT54 | N | - |
| MEET_FT55 | D | AC13 |
| MEET_FT56 | P | AC14 |
| MEET_FT57 | N | - |
| MEET_FT58 | N | - |
| MEET_FT59 | P | AC13, AC14 |
| MEET_FT60 | D | AC17 (phải sửa: Thư ký được bắt đầu) |
| MEET_FT61 | N | - |
| MEET_FT62 | P | AC14 |
| MEET_FT63 | D | AC18 (phải sửa: Thư ký thấy Bắt đầu) |
| MEET_FT64 | D | AC17 |
| MEET_FT65 | P | AC17 |
| MEET_FT66 | N | - |
| MEET_FT67 | N | - |
| MEET_FT68 | D | AC19 |
| MEET_FT69 | N | - |
| MEET_FT70 | D | AC19 |
| MEET_FT71 | P | AC20 |
| MEET_FT72 | P | AC19, AC20 |
| MEET_FT73 | N | - |
| MEET_FT74 | N | - |

### Bảng ánh xạ — FE-12 (44 case; bản nháp, agent xác nhận lại)

Cùng ký hiệu D / P / N. Tổng: **15 D, 19 P, 10 N**. Plan `fe-12.mjs` có 22 AC; AC19–AC22 là dự thảo AI (FE-10 dùng chung). AC2 và AC19 đã tính theo luật "chỉ tạo khi họp đã kết thúc" (MSG398).

| Case | Mức | AC |
|---|---|---|
| MIN_FT01 | D | AC1 |
| MIN_FT02 | P | AC22 |
| MIN_FT03 | P | AC2 |
| MIN_FT04 | N | - |
| MIN_FT05 | P | AC2, AC19 |
| MIN_FT06 | D | AC12 |
| MIN_FT07 | N | - |
| MIN_FT08 | D | AC13 |
| MIN_FT09 | D | AC16 |
| MIN_FT10 | D | AC17 |
| MIN_FT11 | P | AC18 |
| MIN_FT12 | N | - |
| MIN_FT13 | D | AC3 |
| MIN_FT14 | N | - |
| MIN_FT15 | P | AC4 |
| MIN_FT16 | N | - |
| MIN_FT17 | P | AC4 |
| MIN_FT18 | P | AC6 |
| MIN_FT19 | D | AC5 |
| MIN_FT20 | D | AC5 |
| MIN_FT21 | P | AC5, AC13 |
| MIN_FT22 | P | AC17 |
| MIN_FT23 | N | - |
| MIN_FT24 | D | AC6 |
| MIN_FT25 | D | AC6 |
| MIN_FT26 | P | AC7 |
| MIN_FT27 | N | - |
| MIN_FT28 | P | AC7 |
| MIN_FT29 | D | AC9 |
| MIN_FT30 | N | - |
| MIN_FT31 | D | AC11 |
| MIN_FT32 | P | AC11 |
| MIN_FT33 | P | AC10 |
| MIN_FT34 | N | - |
| MIN_FT35 | D | AC8 |
| MIN_FT36 | D | AC8 |
| MIN_FT37 | P | AC10 |
| MIN_FT38 | N | - |
| MIN_FT39 | P | AC12 |
| MIN_FT40 | D | AC14 |
| MIN_FT41 | P | AC14 |
| MIN_FT42 | P | AC15 |
| MIN_FT43 | P | AC15 |
| MIN_FT44 | P | AC13 |

### Bảng ánh xạ — FE-10 (85 case; bản nháp, agent xác nhận lại)

Tổng: **14 D, 30 P, 41 N**. Plan `fe-10.mjs` có 19 AC cho bản gỡ băng và Voice ID; phần dự thảo AI (TRANS_FT40–59) nằm trong AC19–AC22 của `fe-12.mjs` (ghi "fe-12 ACn"). Chưa có AC nào cho Cấu hình AI (TRANS_FT79–85) và cho nhóm kiểm lỗi, đồng thời nhiều bước (chờ, sinh lại, hai người dùng) thuộc nhóm AX.

| Case | Mức | AC |
|---|---|---|
| TRANS_FT01 | D | AC1 |
| TRANS_FT02 | N | - |
| TRANS_FT03 | N | - |
| TRANS_FT04 | P | AC2 |
| TRANS_FT05 | N | - |
| TRANS_FT06 | N | - |
| TRANS_FT07 | D | AC3 |
| TRANS_FT08 | P | AC7 |
| TRANS_FT09 | P | AC7 |
| TRANS_FT10 | N | - |
| TRANS_FT11 | N | - |
| TRANS_FT12 | P | AC8 |
| TRANS_FT13 | P | AC2 |
| TRANS_FT14 | N | - |
| TRANS_FT15 | P | AC18 |
| TRANS_FT16 | N | - |
| TRANS_FT17 | P | AC2 |
| TRANS_FT18 | P | AC3 |
| TRANS_FT19 | N | - |
| TRANS_FT20 | N | - |
| TRANS_FT21 | D | AC4 |
| TRANS_FT22 | N | - |
| TRANS_FT23 | P | AC4 |
| TRANS_FT24 | P | AC4 |
| TRANS_FT25 | N | - |
| TRANS_FT26 | D | AC5 |
| TRANS_FT27 | P | AC18 |
| TRANS_FT28 | N | - |
| TRANS_FT29 | D | AC6 |
| TRANS_FT30 | P | AC3 |
| TRANS_FT31 | N | - |
| TRANS_FT32 | N | - |
| TRANS_FT33 | D | AC9 |
| TRANS_FT34 | N | - |
| TRANS_FT35 | P | AC11 |
| TRANS_FT36 | N | - |
| TRANS_FT37 | N | - |
| TRANS_FT38 | P | AC11 |
| TRANS_FT39 | N | - |
| TRANS_FT40 | P | fe-12 AC21 |
| TRANS_FT41 | P | fe-12 AC19, AC20 |
| TRANS_FT42 | N | - |
| TRANS_FT43 | P | fe-12 AC19 |
| TRANS_FT44 | N | - |
| TRANS_FT45 | N | - |
| TRANS_FT46 | N | - |
| TRANS_FT47 | P | fe-12 AC20 |
| TRANS_FT48 | N | - |
| TRANS_FT49 | P | fe-12 AC19 |
| TRANS_FT50 | N | - |
| TRANS_FT51 | P | fe-12 AC20 |
| TRANS_FT52 | P | fe-12 AC22 |
| TRANS_FT53 | N | - |
| TRANS_FT54 | N | - |
| TRANS_FT55 | N | - |
| TRANS_FT56 | N | - |
| TRANS_FT57 | P | fe-12 AC22 |
| TRANS_FT58 | N | - |
| TRANS_FT59 | N | - |
| TRANS_FT60 | D | AC12 |
| TRANS_FT61 | D | AC12 |
| TRANS_FT62 | D | AC13 |
| TRANS_FT63 | D | AC13 |
| TRANS_FT64 | P | AC13 |
| TRANS_FT65 | P | AC13 |
| TRANS_FT66 | D | AC14 |
| TRANS_FT67 | D | AC17 |
| TRANS_FT68 | P | AC15, AC17 |
| TRANS_FT69 | N | - |
| TRANS_FT70 | N | - |
| TRANS_FT71 | N | - |
| TRANS_FT72 | P | AC15 |
| TRANS_FT73 | P | AC16 |
| TRANS_FT74 | D | AC15 |
| TRANS_FT75 | D | AC15 |
| TRANS_FT76 | P | AC16 |
| TRANS_FT77 | P | AC16 |
| TRANS_FT78 | P | AC13 |
| TRANS_FT79 | N | - |
| TRANS_FT80 | N | - |
| TRANS_FT81 | N | - |
| TRANS_FT82 | N | - |
| TRANS_FT83 | N | - |
| TRANS_FT84 | N | - |
| TRANS_FT85 | N | - |

## Case của người (nhóm H)

**FE-01** (4)

- AUTH_FT09: Verify an account created with a Gmail links itself at the first Google sign-in.
- AUTH_FT22: Verify opening the invitation link sends a code to the invited email.
- AUTH_FT27: Verify "Gửi lại mã" waits for its countdown and the old code stops working.
- AUTH_FT29: Verify reopening the link with a live session needs no new code.

**FE-02** (5)

- USER_FT03: Verify searching by name or email ignores case and diacritics.
- USER_FT18: Verify the dialog shows the sign-in methods and where the password is changed.
- USER_FT37: Verify the Administrator sends a reset link from the list.
- USER_FT38: Verify the reset button is disabled for deactivated and Google-only accounts.
- USER_FT40: Verify a stale list is corrected when the account was deactivated meanwhile.

**FE-03** (1)

- RBAC_FT01: Verify the matrix shows every role against every capability, grouped by function.

**FE-06** (2)

- PART_FT25: Secretary invites a Guest by name and email.
- PART_FT52: A removed Presenter and a removed speaker no longer appear in the meeting content.

**FE-08** (7)

- ROOM_FT02: Member joins the room from the lobby and sees the call grid with own tile and the control bar.
- ROOM_FT12: The lobby stays when the video server cannot be reached.
- ROOM_FT34: The microphone is blocked by the browser while in the room.
- ROOM_FT41: Screen share is not offered on a phone.
- ROOM_FT44: The lobby lists microphones, speakers and cameras and the choice is used in the room.
- ROOM_FT48: A browser that cannot pick a speaker says so.
- ROOM_FT80: The Chair closes a speaker's turn.

**FE-09** (7)

- REC_FT17: Nobody can download a recording of a Secret meeting.
- REC_FT32: The Secretary uploads a recorded file after confirming consent.
- REC_FT51: Speaking with the call's mic on records sentences that join the transcript under the speaker's name.
- REC_FT53: A sentence that could not be sent is kept and sent again.
- REC_FT60: A listener hears a sentence again from the transcript.
- REC_FT64: A member listens to one of their own sentences.
- REC_FT67: A sentence the Chair left out is shown as such to its speaker.

**FE-10** (18)

- TRANS_FT01: Verify the Secretary opens a recording and reads the transcript with speakers and times.
- TRANS_FT07: Verify a Member reads the transcript but sees no editing tools and no machine guess.
- TRANS_FT10: Verify the per-mic record of an online meeting lists the sentences live and exports them.
- TRANS_FT19: Verify the combined recording of a hybrid meeting is for listening only.
- TRANS_FT28: Verify two voices the machine split from one person can be named together.
- TRANS_FT30: Verify a Member cannot reach any naming or editing control.
- TRANS_FT37: Verify a speaker withdraws their own sentence from "Ghi âm của tôi".
- TRANS_FT43: Verify the Secretary names the voices from the card before generating.
- TRANS_FT61: Verify agreeing turns Voice ID on.
- TRANS_FT62: Verify the person records a sample and hears it before keeping it.
- TRANS_FT67: Verify turning Voice ID off erases every sample after a warning.
- TRANS_FT68: Verify the tab "Thư ký thu hộ" lists what Secretaries recorded and lets the person hear and delete it.
- TRANS_FT69: Verify a person on an older notice is asked to agree again before anything new applies.
- TRANS_FT71: Verify the page is for the signed-in person only and needs a session.
- TRANS_FT72: Verify the Secretary with the capability sees the panel on the "Bản ghi" tab of an in-person or hybrid meeting.
- TRANS_FT73: Verify the panel is hidden from everyone but the meeting's Secretary and for online or Secret meetings.
- TRANS_FT74: Verify the Secretary records a participant on the room's mic.
- TRANS_FT77: Verify a participant who withdrew meanwhile cannot be recorded and the dialog says why.

**FE-12** (1)

- MIN_FT32: Verify every reader is told of the issue and the email opens the minutes tab.

**FE-13** (6)

- NOTI_FT29: Verify publishing a meeting sends an invitation email to the invitee.
- NOTI_FT30: Verify the Organizer who publishes also gets the invitation email but no bell notification.
- NOTI_FT31: Verify a reschedule sends the "Cuộc họp đã dời lịch" email to every participant.
- NOTI_FT32: Verify cancelling a meeting sends the cancellation email without a way in.
- NOTI_FT33: Verify a reminder email goes out about 15 minutes before the start.
- NOTI_FT35: Verify no email is sent for a draft or unpublished meeting.

## Case cần chuẩn bị (nhóm AX)

**FE-01** (7): AUTH_FT14 (chờ thời gian), AUTH_FT15 (hai phiên), AUTH_FT17 (hai phiên), AUTH_FT20 (hai phiên), AUTH_FT21 (hai phiên), AUTH_FT30 (hai phiên), AUTH_FT32 (hai phiên)

**FE-02** (12): USER_FT09 (hai phiên), USER_FT14 (hai phiên), USER_FT16 (hai phiên), USER_FT20 (hai phiên), USER_FT25 (hai phiên), USER_FT26 (hai phiên), USER_FT29 (hai phiên), USER_FT32 (hai phiên), USER_FT35 (hai phiên), USER_FT41 (chèn lỗi), USER_FT42 (hai phiên), USER_FT43 (chờ thời gian)

**FE-03** (8): RBAC_FT10 (hai phiên), RBAC_FT12 (hai phiên), RBAC_FT25 (hai phiên), RBAC_FT26 (hai phiên), RBAC_FT27 (hai phiên), RBAC_FT38 (hai phiên), RBAC_FT44 (hai phiên), RBAC_FT45 (hai phiên)

**FE-04** (23): MEET_FT07 (hai phiên), MEET_FT10 (chờ thời gian), MEET_FT11 (chèn lỗi), MEET_FT15 (hai phiên), MEET_FT17 (chờ thời gian), MEET_FT18 (hai phiên), MEET_FT32 (chèn lỗi), MEET_FT37 (chèn lỗi), MEET_FT39 (hai phiên), MEET_FT41 (hai phiên), MEET_FT47 (chèn lỗi), MEET_FT48 (hai phiên), MEET_FT49 (hai phiên), MEET_FT53 (hai phiên), MEET_FT54 (chèn lỗi), MEET_FT55 (hai phiên), MEET_FT58 (chèn lỗi), MEET_FT60 (hai phiên), MEET_FT66 (chèn lỗi), MEET_FT67 (hai phiên), MEET_FT68 (hai phiên), MEET_FT73 (chèn lỗi), MEET_FT74 (chờ thời gian)

**FE-05** (11): AGENDA_FT05 (hai phiên), AGENDA_FT06 (chèn lỗi), AGENDA_FT20 (chèn lỗi), AGENDA_FT26 (hai phiên), AGENDA_FT27 (chèn lỗi), AGENDA_FT29 (hai phiên), AGENDA_FT31 (hai phiên), AGENDA_FT32 (chèn lỗi), AGENDA_FT36 (hai phiên), AGENDA_FT38 (chèn lỗi), AGENDA_FT47 (chèn lỗi)

**FE-06** (19): PART_FT11 (hai phiên), PART_FT12 (hai phiên), PART_FT13 (chèn lỗi), PART_FT22 (hai phiên), PART_FT23 (hai phiên), PART_FT24 (hai phiên), PART_FT41 (hai phiên), PART_FT44 (hai phiên), PART_FT51 (hai phiên), PART_FT53 (hai phiên), PART_FT60 (hai phiên), PART_FT61 (hai phiên), PART_FT64 (hai phiên), PART_FT74 (hai phiên), PART_FT76 (hai phiên), PART_FT77 (hai phiên), PART_FT79 (hai phiên), PART_FT86 (hai phiên), PART_FT88 (hai phiên)

**FE-07** (17): DOC_FT09 (hai phiên), DOC_FT10 (chèn lỗi), DOC_FT18 (hai phiên), DOC_FT24 (hai phiên), DOC_FT39 (hai phiên), DOC_FT41 (chèn lỗi), DOC_FT48 (hai phiên), DOC_FT50 (chèn lỗi/hai phiên), DOC_FT60 (hai phiên), DOC_FT61 (hai phiên), DOC_FT68 (hai phiên), DOC_FT74 (hai phiên), DOC_FT75 (hai phiên), DOC_FT76 (chèn lỗi), DOC_FT88 (hai phiên), DOC_FT89 (hai phiên), DOC_FT98 (hai phiên)

**FE-08** (14): ROOM_FT03 (hai phiên), ROOM_FT05 (hai phiên), ROOM_FT07 (hai phiên), ROOM_FT16 (hai phiên), ROOM_FT25 (hai phiên), ROOM_FT31 (hai phiên), ROOM_FT32 (hai phiên), ROOM_FT38 (hai phiên), ROOM_FT43 (hai phiên), ROOM_FT46 (hai phiên), ROOM_FT86 (chèn lỗi), ROOM_FT93 (hai phiên), ROOM_FT96 (chèn lỗi), ROOM_FT97 (hai phiên)

**FE-09** (10): REC_FT04 (chờ thời gian), REC_FT11 (chèn lỗi), REC_FT12 (hai phiên/chờ thời gian), REC_FT18 (hai phiên), REC_FT23 (hai phiên), REC_FT28 (hai phiên), REC_FT31 (hai phiên), REC_FT39 (hai phiên), REC_FT43 (hai phiên), REC_FT49 (chờ thời gian)

**FE-10** (11): TRANS_FT17 (hai phiên), TRANS_FT25 (chèn lỗi), TRANS_FT32 (hai phiên), TRANS_FT39 (hai phiên), TRANS_FT46 (chèn lỗi), TRANS_FT47 (chèn lỗi), TRANS_FT48 (hai phiên), TRANS_FT51 (hai phiên), TRANS_FT57 (hai phiên), TRANS_FT58 (hai phiên), TRANS_FT63 (chèn lỗi)

**FE-11** (2): VOTE_FT04 (chèn lỗi), VOTE_FT17 (chèn lỗi)

**FE-12** (7): MIN_FT07 (hai phiên), MIN_FT17 (hai phiên), MIN_FT24 (hai phiên), MIN_FT28 (hai phiên), MIN_FT34 (hai phiên), MIN_FT35 (hai phiên), MIN_FT43 (hai phiên)

**FE-13** (8): NOTI_FT14 (chèn lỗi), NOTI_FT15 (hai phiên), NOTI_FT16 (hai phiên), NOTI_FT18 (hai phiên), NOTI_FT23 (hai phiên), NOTI_FT24 (hai phiên), NOTI_FT26 (hai phiên), NOTI_FT28 (hai phiên)

**FE-15** (2): RPT_FT10 (chèn lỗi), RPT_FT16 (chèn lỗi)

**FE-16** (1): CAL_FT29 (hai phiên)

Nhóm A là các case còn lại của mỗi Feature, không nằm trong hai danh sách trên.

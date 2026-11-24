# Bàn giao: case UI Round 1 làm manual (đã đánh lại số ID)

Ngày 23/11/2026, thay bản trước cùng ngày. Bộ case đã được thu gọn và đánh lại ID liền mạch (UI 556, API 819),
nên **mọi ID trong bản cũ (`REC_FT13`, `NOTI_FT29`…) không còn đúng**: dùng ID dưới đây. Nguồn:
`docs/08-reports/test-reports/cases/FE-xx/{ui,api}.json`. Chưa ghi kết quả vào `ui.json`: người làm manual
chạy xong thì điền `round1` / `date1` / `tester1`, rồi sinh lại workbook
(`python scripts/test_reports/integration_cases.py build`, đóng Excel trước).

## Tình trạng

- **API**: 825 Passed (thêm 6 case truy vấn/xuất báo cáo RPT_API_IT19-24, TRX 24/11).
- **UI**: 443 Passed, 110 chưa chạy (phần 2), 3 Failed (phần 1).

## 1. Ba case Failed: tái hiện và xác nhận

| Mã | Kết luận |
|---|---|
| `REC_FT27`: file quá giới hạn bị từ chối (BUG-02, trước là REC_FT36) | **Bug thật.** Backend trả 413/MSG293 sớm rồi đóng kết nối, trình duyệt nhận lỗi mạng, không thấy mã 413. Tái hiện: Secretary, tab Bản ghi, tải file lớn hơn giới hạn (đã thử 301 MB), xem toast |
| `MEET_FT01`: Organizer tạo Draft từ danh sách và vào trang chi tiết | Chưa phân tích nguyên nhân. Chạy tay lại, ghi bước nào hỏng |
| `MIN_FT09`: Secretary sửa biên bản, chữ đã lưu còn nguyên | Chưa phân tích nguyên nhân. Chạy tay lại, so chữ sau khi tải lại trang |

Bug đã kết luận trước đó, nhưng chưa có case riêng: SUS-02 (tooltip dòng `.recording-meta` không hiện khi chạm ở 390px, bug thật),
SUS-03 (gợi ý vẫn hiện khi LiveKit chạy tốt, nhẹ), SUS-01 (con trỏ biên bản nhảy về ô đầu sau Enter ở 390px, nhiều khả năng là bug; cần máy thật).

## 2. Case chưa chạy, chia theo cách làm

### A. Cần giọng nói tiếng Việt thật

Vào phòng hoặc tải lên file ghi âm người nói tiếng Việt, đợi bản ghi lời. Máy dev chỉ tạo được tiếng bíp nên worker nhận dạng trả 0 đoạn; worker và backend vẫn chạy đúng.

- `REC_FT20`: Switching the mic off saves the take and has the recording transcribed.
- `REC_FT24`: The Secretary uploads a recorded file after confirming consent.
- `REC_FT38`: Speaking with the call's mic on records sentences that join the transcript under the speaker's name.
- `REC_FT39`: The Chair takes a sentence out of the record and puts it back.
- `REC_FT40`: A Member has no exclude button.
- `REC_FT41`: The Secretary corrects the text of a sentence.
- `REC_FT42`: "Ghi âm của tôi" lists my own sentences, sent and waiting.
- `REC_FT43`: A member listens to one of their own sentences.
- `REC_FT44`: With nothing said the dialog shows an empty state.
- `TRANS_FT01`: the Secretary opens a recording and reads the transcript with speakers and times.
- `TRANS_FT04`: a recording without words and one not yet transcribed show their own messages.
- `TRANS_FT05`: a failed transcript shows the reason and a retry for the Secretary.
- `TRANS_FT06`: a Member reads the transcript but sees no editing tools and no machine guess.
- `TRANS_FT09`: the per-mic record of an online meeting lists the sentences live and exports them.
- `TRANS_FT13`: "Chạy lại" retries a failed transcript.
- `TRANS_FT22`: a Member cannot reach any naming or editing control.
- `TRANS_FT27`: a speaker withdraws their own sentence from "Ghi âm của tôi".
- `TRANS_FT29`: the Secretary asks the AI for the first draft and the card shows its progress.
- `TRANS_FT30`: the readiness note lists what the draft would miss.
- `TRANS_FT31`: the Secretary names the voices from the card before generating.
- `TRANS_FT32`: regenerating asks for confirmation and warns that edits are replaced.
- `TRANS_FT33`: a draft that failed shows the reason and keeps the earlier one.
- `TRANS_FT34`: the request button is disabled with a reason when the AI is not ready or edits are unsaved.
- `TRANS_FT36`: the Chair or Secretary reads the draft as three folded sections.
- `TRANS_FT37`: a Member sees the draft only after it is reviewed, read-only and without the sources.
- `TRANS_FT38`: the Secretary edits a section and the save bar appears.
- `TRANS_FT39`: saving an edit of a reviewed draft warns that it must be reviewed again.
- `TRANS_FT40`: "Hoàn tác" discards unsaved edits after confirming.
- `TRANS_FT41`: the Secretary approves the draft and others can then read it.

### B. Voice ID và thu giọng mẫu hộ

Cần micro thật: ghi mẫu rồi nghe lại. Tài khoản thu hộ cần capability "Thu giọng mẫu hộ".

- `TRANS_FT43`: agreeing turns Voice ID on.
- `TRANS_FT44`: the person records a sample and hears it before keeping it.
- `TRANS_FT45`: a rejected recording shows the reason and keeps the dialog open.
- `TRANS_FT47`: the person plays a saved sample back.
- `TRANS_FT49`: turning Voice ID off erases every sample after a warning.
- `TRANS_FT50`: the card "Người khác thu hộ" lists what collectors recorded and lets the person hear and delete it.
- `TRANS_FT51`: a person on an older notice is asked to agree again before anything new applies.
- `TRANS_FT52`: an account with "Thu giọng mẫu hộ" opens one person's voice collection from the user list.
- `TRANS_FT53`: the collector records a person who agreed, with no meeting involved.
- `TRANS_FT54`: a rejected take shows the reason in the dialog.
- `TRANS_FT55`: the collector plays back a sample collected for a person.
- `TRANS_FT56`: deleting a collected sample asks first and removes it only after the collector confirms.
- `TRANS_FT57`: an account without "Thu giọng mẫu hộ" has no voice collection on the user list.

### C. Cấu hình AI (Administrator)

Cần khoá API thử; kiểm khoá không hiện lại sau khi lưu.

- `TRANS_FT59`: the Administrator switches to the API and saves with a key that is never shown again.
- `TRANS_FT61`: "Thử kết nối" tests the chosen machine without saving.
- `TRANS_FT64`: the saved choice drives the next draft and persists after a reload.

### D. Email hoặc mã mời qua mailpit

Hộp thư test http://localhost:8025. Làm thao tác rồi mở mailpit xem thư.

- `NOTI_FT22`: publishing a meeting sends an invitation email to the invitee.
- `NOTI_FT23`: a reschedule sends the "Cuộc họp đã dời lịch" email to every participant.
- `NOTI_FT24`: cancelling a meeting sends the cancellation email without a way in.
- `AUTH_FT20`: opening the invitation link sends a code to the invited email.
- `USER_FT29`: the Administrator sends a reset link from the list.
- `USER_FT31`: the person sets a new password from the emailed link.
- `USER_FT32`: a used, expired or replaced link shows the dead-link notice.
- `RPT_FT11`: Report is reachable without any capability, and a Guest cannot use it.

### E. Khách (Guest) và Google

Cần thư mời thật (mailpit) hoặc tài khoản Gmail thử; dùng trình duyệt riêng cho Guest.

- `AUTH_FT08`: a linked user signs in with Google.
- `AUTH_FT09`: an account created with a Gmail links itself at the first Google sign-in.
- `AUTH_FT10`: a Gmail without an account is refused with guidance.
- `AUTH_FT11`: a deactivated account cannot sign in with Google.
- `AUTH_FT14`: an expired access token is renewed without interrupting the user.
- `AUTH_FT21`: the right code leads to the profile form and then into the meeting.
- `AUTH_FT22`: a wrong code is refused.
- `AUTH_FT23`: 5 wrong codes lock the code until a new one is sent.
- `AUTH_FT24`: an empty display name is not accepted.
- `AUTH_FT26`: a Guest session does not open internal screens.
- `VOTE_FT15`: a verified Guest casts a secret ballot from the meeting page.
- `ROOM_FT14`: Guest must confirm the recording notice before joining.
- `ROOM_FT15`: Guest cannot enter once the meeting has ended.
- `ROOM_FT25`: A Guest is listed with the name they gave and no email.
- `DOC_FT27`: a Guest sees the documents of the invited meeting and nothing to change.
- `PART_FT13`: Secretary invites a Guest by name and email.

### F. Cần hai người trong phòng cùng lúc

Mở hai trình duyệt, hai tài khoản.

- `ROOM_FT24`: The people panel lists everyone connected with their meeting role.
- `ROOM_FT45`: The Chair closes a speaker's turn.
- `AGENDA_FT04`: a second user sees a change made by the Secretary without reloading.

### G. Tải tài liệu lên cuộc họp (có thể dính lỗi 500)

Cuộc họp phải đang InProgress. Nếu trả 500 thì lấy log backend lúc gọi `POST /api/meetings/{id}/documents/upload`, để phân loại lỗi cấu hình `FileStorage:*` hay lỗi code.

- `DOC_FT50`: the vote panel asks to read each document before the ballot.
- `DOC_FT51`: the confirm button unlocks only at the last page and records the confirmation.

### H. Case dễ, chạy tay thẳng trên giao diện (làm trước)

Không cần môi trường đặc biệt.

- `MEET_FT03`: the create dialog rejects an end before the start and a start in the past.
- `MEET_FT04`: the create dialog shows the backend roster error next to the person it names.
- `MEET_FT06`: an expired session while creating sends the user to sign in.
- `MEET_FT14`: the "Cần xử lý" card shows what waits on the user.
- `MEET_FT15`: the "Thời điểm" filter offers only the fixed choices.
- `MEET_FT16`: paging keeps the filters and shows the right rows.
- `MEET_FT20`: a meeting in progress shows the call to enter the room.
- `MEET_FT25`: a meeting the Chair forgot to end shows the system end in its history.
- `MEET_FT28`: an incomplete meeting lists what is missing and blocks the button.
- `MEET_FT32`: a user who is not the assigned approver gets no approval buttons.
- `MEET_FT34`: Publish is refused when the roster lacks a Secretary.
- `MEET_FT37`: Start is blocked without a Secretary.
- `MIN_FT02`: the card warns that the minutes use an AI draft not reviewed yet.
- `MIN_FT03`: no draft can be built before the meeting starts.
- `MIN_FT08`: an outsider cannot reach the minutes of a meeting.
- `MIN_FT11`: "Huỷ" leaves the editor without saving.
- `MIN_FT18`: the minutes cannot be sent before the meeting ends.
- `MIN_FT19`: the Chair is not offered "Gửi duyệt".
- `MIN_FT21`: cancelling the dialog sends nothing.
- `MIN_FT23`: every reader is told of the issue and the email opens the minutes tab.
- `MIN_FT24`: the Secretary cannot approve.
- `MIN_FT27`: cancelling the return keeps the minutes waiting.
- `MIN_FT31`: an issued correction replaces the original, which stays readable.
- `AGENDA_FT03`: an item shows its documents and an empty hint when it has none.
- `AGENDA_FT20`: only one item can be current.
- `RBAC_FT01`: the matrix shows every role against every capability, grouped by function.
- `RBAC_FT06`: the server refuses a save that would leave no administrator and the row shows why.
- `USER_FT27`: demoting the last administrator is refused with a clear message.
- `ROOM_FT02`: Member joins the room from the lobby and sees the call grid with own tile and the control bar.
- `ROOM_FT03`: Chair and Secretary join like a Member; the Chair's tile carries the role chip.
- `ROOM_FT54`: Notes close once the draft minutes exist.
- `REC_FT01`: Secretary sees the recordings of the meeting with source, length, size and an open player.
- `REC_FT13`: Nobody can download a recording of a Secret meeting.
- `REC_FT35`: The Secretary deletes one take of a room recording.
- `REC_FT36`: A Member sees no take delete button.
- `CAL_FT34`: the no-common-slot result says so and shows the most-free slots.

## 3. Chạy lại bằng runner

```
cd tests/qa
node run.mjs plans/fe-11.mjs --only AC17 --device desktop --base http://localhost:5225
```

Kết quả ở `tests/qa/out/<SCRUM-n>/<giờ>-<commit>/`. Tài khoản seed dùng mật khẩu `TrustCabinet@2026`. Trên Windows: tắt EventLog logger
(`Logging__EventLog__LogLevel__Default=None`), dùng `localhost` thay `127.0.0.1` (Vite nghe IPv6), và đừng để đĩa đầy.

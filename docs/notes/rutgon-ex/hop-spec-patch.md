# Spec patch — nhóm hop (meeting-management + agenda-management + meeting-participant-management + online-meeting-room)
n> **Lưu ý:** nếu mục này lệch với `hop-spec-patch.md` thì theo file `hop-ex-final.md` (sinh từ diagram sau lượt chọn cuối của BA).

Áp dụng sau khi merge. Chỉ ghi EX giữ lại (số mới + MSG) và EX bỏ/gộp (số cũ + lý do). Không đổi MSG/BR.

## meeting-management

- UC-14 Create Meeting: giữ EX-1 (MSG31 invalid meeting info), EX-2 (MSG277 invalid participants). Bỏ EX-2 cũ (MSG35 unable to create, hạ tầng).
- UC-15 Update Meeting: giữ EX-1 (MSG31/MSG33 invalid or unauthorized), EX-2 (MSG33 reschedule not permitted), EX-3 (planned start >24h, BR-301). Bỏ EX-3 cũ (MSG36 unable to update, hạ tầng).
- UC-16 View Meeting Details: giữ EX-1 (MSG37 not accessible). Bỏ EX-2 cũ (MSG37 unable to load, hạ tầng).
- UC-17 View Meeting List: giữ EX-1 (MSG32 invalid criteria). Bỏ EX-1 cũ (MSG38 unable to load, hạ tầng).
- UC-18 Submit Meeting for Approval: giữ EX-1 (incomplete info), EX-2 (MSG41 status not eligible), EX-3 (MSG140 no eligible approver). Bỏ EX-3 cũ (approver not eligible, gộp vào EX-2), EX-4 (MSG42 unable to submit, hạ tầng), EX-5 (MSG140, gộp vào EX-3). BA xác nhận.
- UC-19 Review Meeting Approval: giữ EX-1 (MSG83 reason missing), EX-2 (no longer awaiting), EX-3 (MSG84 reason too long). Bỏ EX-3 cũ (unable to complete, hạ tầng), EX-4 (no longer authorized, gộp vào EX-2).
- UC-20 Publish Meeting: giữ EX-1 (MSG46 not eligible). Bỏ EX-2 cũ (MSG47 unable to publish, hạ tầng).
- UC-21 Start Meeting: giữ EX-1 (MSG112 cannot be started). Bỏ EX-2 cũ (MSG115 unable to start, hạ tầng).
- UC-22 End Meeting: giữ EX-1 (MSG114 cannot be ended). Bỏ EX-2 cũ (MSG116 unable to end, hạ tầng).
- UC-75 Cancel Meeting: giữ EX-1 (MSG262 reason missing), EX-2 (MSG345 reason too long), EX-3 (MSG263 cannot be cancelled). Bỏ EX-3 cũ (MSG264 unable to cancel, hạ tầng), EX-5 (MSG85 not organizer, gộp vào EX-3).
- SYS Auto-End Forgotten Meeting: không đổi EX; sửa thanh activation.

## agenda-management

- UC-23a Create Agenda Item: giữ EX-1 (MSG66 invalid info), EX-2 (MSG67 planning not permitted). Bỏ EX-3 cũ (MSG68 unable to create, hạ tầng).
- UC-23b Update Agenda Item: giữ EX-1 (MSG66 invalid info), EX-2 (MSG67 editing not permitted). Bỏ EX-3 cũ (MSG69 unable to update, hạ tầng).
- UC-23c Delete Agenda Item: giữ EX-1 (MSG67 planning not permitted), EX-2 (MSG142 already started). Bỏ EX-3 cũ (MSG70 unable to delete, hạ tầng).
- UC-23d Reorder Agenda Items: giữ EX-1 (MSG67 not permitted), EX-2 (MSG143 invalid/outdated order). Bỏ EX-3 cũ (MSG71 unable to save, hạ tầng).
- UC-24 Set Current Agenda Item: giữ EX-1 (MSG73 cannot become current), EX-2 (MSG72 another in progress). Không bỏ.
- UC-25 Complete Agenda Item: giữ EX-1 (MSG74 cannot be completed). Bỏ EX-2 cũ (MSG76 unable to complete, hạ tầng).
- UC-26 View Meeting Agenda: giữ EX-1 (not accessible). Bỏ EX-2 cũ (MSG128 unable to load, hạ tầng).
- UC-77 Record Agenda Item Conclusion: giữ EX-1 (MSG272 blank/too long), EX-2 (MSG273 can no longer be changed). Bỏ EX-3 (MSG274 unable to save, hạ tầng), EX-4 (MSG174 meeting unavailable, gộp vào EX-2).
- UC-82 Take Meeting Notes: giữ EX-1 (MSG416 not secretary), EX-2 (MSG417 no longer accepts notes), EX-3 (MSG418 changed in another window). Bỏ EX-4 (MSG419 too long, gộp vào EX-1? BA xem), EX-5 (MSG420 item removed, gộp vào EX-3), EX-6 (MSG421 unable to load, hạ tầng), EX-7 (MSG422 unable to save, hạ tầng).

## meeting-participant-management

- UC-27 View Meeting Participants: giữ EX-1 (not accessible). Bỏ EX-2 cũ (MSG95 unable to load, hạ tầng).
- UC-28a Add Meeting Participant: giữ EX-1 (MSG97 invalid), EX-2 (MSG85 not allowed), EX-3 (MSG98 no longer permitted). Bỏ EX-2 cũ (MSG275 list changed, hạ tầng), EX-5 (MSG109 second chair, gộp vào EX-1), EX-6 (404 no longer available, hạ tầng), EX-7 (MSG99 unable to add, hạ tầng).
- UC-28b Manage Meeting Role: giữ EX-1 (MSG101 invalid role), EX-2 (MSG109 required role lost), EX-3 (MSG98 no longer eligible). Bỏ EX-4..EX-10 (MSG85, 404, MSG275, MSG102, MSG256, MSG259, MSG257/MSG154) — gộp vào EX-3/EX-1.
- UC-28c Remove Meeting Participant: giữ EX-1 (MSG98 no longer permitted), EX-2 (MSG109 role violation), EX-3 (MSG85 not allowed). Bỏ EX-3 cũ (MSG275 list changed, hạ tầng), EX-5 (404 no longer available, hạ tầng), EX-6 (MSG104 unable to remove, hạ tầng).
- UC-29 Respond to Meeting Invitation: giữ EX-1 (MSG107 no longer answerable), EX-2 (MSG106 invalid value), EX-3 (MSG285 different response after answering). Bỏ EX-3 cũ (MSG108 unable to save, hạ tầng), EX-5 (MSG286 chair cannot respond, gộp vào EX-1).
- UC-30 View Participant Response Status: giữ EX-1 (not accessible). Bỏ EX-2 cũ (MSG95 unable to load, hạ tầng).
- UC-31 Record Attendance: giữ EX-1 (MSG148 invalid value), EX-2 (MSG149 not in progress), EX-3 (not authorized). Bỏ EX-4 (MSG151 changed, hạ tầng), EX-5 (MSG150 unable to save, hạ tầng), EX-6 (MSG289 unable to load, hạ tầng).
- UC-73 Hand Over Chair: giữ EX-1 (MSG259 no longer eligible), EX-2 (MSG260 no longer permitted). Bỏ EX-3 (MSG102 unable to save, hạ tầng), EX-4 (MSG275 list changed, hạ tầng).

## online-meeting-room

- UC-37 Join Meeting Room: giữ EX-1 (MSG331 ended), EX-2 (MSG280 in person), EX-3 (MSG281 not ready). Bỏ các EX còn lại (MSG350 cancelled, MSG117 not started, MSG283 unable to load, MSG118 unable to join, MSG378 guest session ended, MSG380 recording notice, MSG338 audio blocked) — gộp/hạ tầng.
- UC-38 Leave Meeting Room: giữ EX-1 (MSG331 ended), EX-2 (MSG329 joined elsewhere), EX-3 (MSG330 no longer participant). Bỏ EX-4 (MSG333 connection lost, hạ tầng), EX-5 (MSG328 left without leave, hạ tầng), EX-6 (MSG378 guest session ended, gộp vào EX-3).
- UC-39a Manage Microphone: giữ EX-1 (MSG334 browser blocks), EX-2 (MSG335 no microphone), EX-3 (MSG320 cannot be recorded). Bỏ EX-3 cũ (MSG336 another app, gộp vào EX-2), EX-4 (MSG312 speech recognition unavailable, hạ tầng), EX-6/EX-7 (MSG319/MSG321 invalid sentence, gộp vào EX-3).
- UC-39a Record Speech While Microphone On: giữ EX-1 (MSG320 cannot be recorded). Bỏ EX-4 (MSG312 speech recognition unavailable, hạ tầng). EX remaining as AF.
- UC-39b Manage Camera: giữ EX-1 (MSG334 browser blocks), EX-2 (MSG335 no camera). Bỏ EX-3 (MSG336 another app, hạ tầng).
- UC-39c Manage Screen Sharing: không giữ EX (EX-1 MSG341 unable to share là hạ tầng). 
- UC-39d Choose Audio/Video Devices: giữ EX-1 (MSG335 not found), EX-2 (MSG337 cannot be opened). Bỏ EX-2 cũ (MSG336 another app, hạ tầng).
- UC-40 View Meeting Room Participants: không giữ EX (EX-1 MSG333 connection lost, hạ tầng).
- UC-41 Moderate Meeting Room: giữ EX-1 (MSG123 no longer chair), EX-2 (MSG117 no longer in progress), EX-3 (MSG282 target not in room). Bỏ EX-3 cũ (MSG124 no response, hạ tầng), EX-4 (MSG284 unable to mute, hạ tầng).
- UC-42 Use In-room Chat: không giữ EX (EX-1 MSG343 unable to send, hạ tầng; EX-2 MSG333 connection lost, hạ tầng).
- UC-78 Floating Window: không giữ EX (EX-1 refused, hạ tầng).
- UC-79 Register to Speak: giữ EX-1 (MSG117 no longer in progress), EX-2 (already open request), EX-3 (no longer chair). Bỏ EX-4 (chair controls, gộp vào EX-3), EX-5 (MSG124 no response, hạ tầng).
EOF
echo written
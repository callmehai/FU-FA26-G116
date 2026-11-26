// Builds TrustCabinet-API.postman_collection.json (Postman v2.1) from the request list below.
// Run: node tests/postman/build-collection.mjs — edit this file, not the generated JSON.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const json = { key: 'Content-Type', value: 'application/json' };
const bearer = (v) => ({ key: 'Authorization', value: `Bearer {{${v}}}` });

// One request: [id, name, method, url, tokenVar|null, body|null, testLines[], preRequestLines?]
function req(id, name, method, url, token, body, tests, pre) {
  const header = [];
  if (token) header.push(bearer(token));
  const request = { method, header, url: `{{baseUrl}}${url}` };
  if (body?.formdata) request.body = { mode: 'formdata', formdata: body.formdata };
  else if (body) { header.push(json); request.body = { mode: 'raw', raw: JSON.stringify(body, null, 2) }; }
  const event = [{ listen: 'test', script: { type: 'text/javascript', exec: tests } }];
  if (pre) event.unshift({ listen: 'prerequest', script: { type: 'text/javascript', exec: pre } });
  return { name: `${id} — ${name}`, event, request };
}
const status = (s) => `pm.test('HTTP ${s}', () => pm.response.to.have.status(${s}));`;
const code = (c) => `pm.test('code = ${c}', () => pm.expect(pm.response.json().code).to.eql('${c}'));`;

// Planned start: tomorrow 09:00 local, so the meeting can be approved and published; it is started later anyway.
const futureStart = [
  "const d = new Date(Date.now() + 24 * 3600 * 1000); d.setUTCHours(2, 0, 0, 0);",
  "pm.collectionVariables.set('startAt', d.toISOString().replace('.000Z', 'Z'));",
  "pm.collectionVariables.set('endAt', new Date(d.getTime() + 3600 * 1000).toISOString().replace('.000Z', 'Z'));",
  "pm.collectionVariables.set('stamp', new Date().toISOString().slice(0, 19).replace('T', ' '));",
];
const meetingBody = (title) => ({
  title: `[Postman] ${title} {{stamp}}`,
  plannedStartTime: '{{startAt}}',
  plannedEndTime: '{{endAt}}',
  mode: 'InPerson',
  location: 'Phòng họp Postman',
  sensitivity: 'Normal',
  description: 'Cuộc họp do collection Postman tạo để kiểm thử API.',
  participants: [
    { userId: '{{approverId}}', role: 'Chair' },
    { userId: '{{secretaryId}}', role: 'Secretary' },
    { userId: '{{organizerId}}', role: 'Member' },
  ],
});

const signIn = (id, who, emailVar, tokenVar, idVar) => req(id, `Đăng nhập ${who}`, 'POST', '/api/auth/login', null,
  { email: `{{${emailVar}}}`, password: '{{password}}' }, [
    status(200),
    "const b = pm.response.json();",
    "pm.test('trả accessToken và user', () => { pm.expect(b.accessToken).to.be.a('string'); pm.expect(b.user.id).to.be.a('string'); });",
    `pm.environment.set('${tokenVar}', b.accessToken);`,
    `pm.collectionVariables.set('${idVar}', b.user.id);`,
  ]);

const item = [
  { name: '1. Xác thực (UC-01)', item: [
    req('API01', 'Sai mật khẩu → MSG01', 'POST', '/api/auth/login', null,
      { email: '{{organizerEmail}}', password: 'sai-mat-khau' }, [status(401), code('MSG01')]),
    signIn('API02', 'Thành viên (người tổ chức, lưu token)', 'organizerEmail', 'token', 'organizerId'),
    req('API03', 'Làm mới token bằng cookie refresh', 'POST', '/api/auth/refresh', null, null, [
      status(200),
      "const b = pm.response.json();",
      "pm.test('cấp accessToken mới', () => pm.expect(b.accessToken).to.be.a('string'));",
      "pm.environment.set('token', b.accessToken);",
    ]),
    req('API04', 'Thông tin người đăng nhập', 'GET', '/api/auth/me', 'token', null, [
      status(200),
      "pm.test('đúng tài khoản thành viên', () => pm.expect(pm.response.json().email).to.eql(pm.environment.get('organizerEmail')));",
    ]),
    req('API05', 'Không có token → 401', 'GET', '/api/meetings', null, null, [status(401)]),
    signIn('API06', 'Phê duyệt viên (sẽ làm Chủ toạ cuộc họp)', 'approverEmail', 'approverToken', 'approverId'),
    signIn('API07', 'Phê duyệt viên khác (sẽ làm Thư ký cuộc họp)', 'secretaryEmail', 'secretaryToken', 'secretaryId'),
  ] },
  { name: '2. Cuộc họp: tạo, duyệt, công bố, huỷ', item: [
    req('API08', 'Danh sách cuộc họp', 'GET', '/api/meetings?page=1&pageSize=10', 'token', null, [
      status(200),
      "pm.test('có items và totalCount', () => { const b = pm.response.json(); pm.expect(b.items).to.be.an('array'); pm.expect(b.totalCount).to.be.a('number'); });",
    ]),
    req('API09', 'Tạo cuộc họp thiếu tiêu đề → MSG31', 'POST', '/api/meetings', 'token',
      { ...meetingBody('x'), title: '' }, [status(400), code('MSG31')], futureStart),
    req('API10', 'Tạo cuộc họp', 'POST', '/api/meetings', 'token', meetingBody('Họp kiểm thử API'), [
      status(201), code('MSG29'),
      "const b = pm.response.json();",
      "pm.test('trạng thái Draft', () => pm.expect(b.meeting.status).to.eql('Draft'));",
      "pm.collectionVariables.set('meetingId', b.meeting.id);",
    ], futureStart),
    req('API11', 'Gửi duyệt cho Phê duyệt viên', 'POST', '/api/meetings/{{meetingId}}/transitions', 'token',
      { action: 'SubmitForApproval', assignedApproverId: '{{approverId}}' }, [status(200), code('MSG78'),
        "pm.test('WaitingApproval', () => pm.expect(pm.response.json().meeting.status).to.eql('WaitingApproval'));"]),
    req('API12', 'Thành viên không được duyệt cuộc họp → 403', 'POST', '/api/meetings/{{meetingId}}/transitions', 'token',
      { action: 'Approve' }, [status(403)]),
    req('API13', 'Phê duyệt viên duyệt', 'POST', '/api/meetings/{{meetingId}}/transitions', 'approverToken',
      { action: 'Approve' }, [status(200), code('MSG43'),
        "pm.test('Approved', () => pm.expect(pm.response.json().meeting.status).to.eql('Approved'));"]),
    req('API14', 'Công bố', 'POST', '/api/meetings/{{meetingId}}/transitions', 'token',
      { action: 'Publish' }, [status(200), code('MSG45'),
        "pm.test('Published', () => pm.expect(pm.response.json().meeting.status).to.eql('Published'));"]),
    req('API15', 'Tạo cuộc họp thứ hai để huỷ', 'POST', '/api/meetings', 'token', meetingBody('Họp sẽ huỷ'), [
      status(201), code('MSG29'), "pm.collectionVariables.set('cancelMeetingId', pm.response.json().meeting.id);",
    ], futureStart),
    req('API16', 'Huỷ không lý do → MSG262', 'POST', '/api/meetings/{{cancelMeetingId}}/transitions', 'token',
      { action: 'Cancel', reason: '' }, [status(400), code('MSG262')]),
    req('API17', 'Huỷ cuộc họp', 'POST', '/api/meetings/{{cancelMeetingId}}/transitions', 'token',
      { action: 'Cancel', reason: 'Đổi kế hoạch, kiểm thử Postman.' }, [status(200), code('MSG261'),
        "pm.test('Cancelled', () => pm.expect(pm.response.json().meeting.status).to.eql('Cancelled'));"]),
  ] },
  { name: '3. Chương trình nghị sự, thành phần', item: [
    req('API18', 'Thêm nội dung họp', 'POST', '/api/meetings/{{meetingId}}/agenda', 'secretaryToken',
      { title: 'Báo cáo tiến độ quý', description: 'Thư ký trình bày.', presenterId: null, presenterGuestInvitationId: null },
      [status(201), code('MSG60')]),
    req('API19', 'Thêm nội dung tiêu đề rỗng → MSG66', 'POST', '/api/meetings/{{meetingId}}/agenda', 'secretaryToken',
      { title: '   ', description: '' }, [status(400), code('MSG66')]),
    req('API20', 'Đọc chương trình nghị sự', 'GET', '/api/meetings/{{meetingId}}/agenda', 'token', null, [
      status(200), "pm.test('có 1 nội dung', () => pm.expect(pm.response.json().items).to.have.lengthOf(1));"]),
    req('API21', 'Danh sách thành phần tham dự', 'GET', '/api/meetings/{{meetingId}}/participants', 'token', null, [
      status(200), "pm.test('đủ 3 người', () => pm.expect(pm.response.json().items.length).to.be.at.least(3));"]),
  ] },
  { name: '4. Tài liệu', item: [
    req('API22', 'Tải tài liệu PDF lên', 'POST', '/api/meetings/{{meetingId}}/documents/upload', 'secretaryToken',
      { formdata: [{ key: 'files', type: 'file', src: 'fixtures/tai-lieu-mau.pdf' }] },
      [status(201), code('MSG161'), "pm.test('addedCount = 1', () => pm.expect(pm.response.json().addedCount).to.eql(1));"]),
    req('API23', 'Danh sách tài liệu cuộc họp', 'GET', '/api/meetings/{{meetingId}}/documents', 'token', null, [
      status(200), "pm.test('có tài liệu vừa tải', () => pm.expect(pm.response.json().general.length).to.be.at.least(1));"]),
  ] },
  { name: '5. Diễn ra cuộc họp: điểm danh, biểu quyết', item: [
    req('API24', 'Chủ toạ bắt đầu cuộc họp', 'POST', '/api/meetings/{{meetingId}}/transitions', 'approverToken',
      { action: 'Start' }, [status(200), code('MSG110')]),
    req('API25', 'Đọc danh sách điểm danh', 'GET', '/api/meetings/{{meetingId}}/attendance', 'secretaryToken', null, [
      status(200),
      "const e = pm.response.json().items.find(x => x.kind === 'Internal');",
      "pm.collectionVariables.set('attendanceId', e.id); pm.collectionVariables.set('attendanceVersion', e.rowVersion);",
    ]),
    req('API26', 'Thư ký lưu điểm danh', 'POST', '/api/meetings/{{meetingId}}/attendance', 'secretaryToken',
      { entries: [{ id: '{{attendanceId}}', kind: 'Internal', attendance: 'Present', expectedVersion: '{{attendanceVersion}}' }] },
      [status(200), code('MSG147')]),
    req('API27', 'Thành viên không được điểm danh → 403', 'POST', '/api/meetings/{{meetingId}}/attendance', 'token',
      { entries: [{ id: '{{attendanceId}}', kind: 'Internal', attendance: 'Absent', expectedVersion: '{{attendanceVersion}}' }] },
      [status(403)]),
    req('API28', 'Chủ toạ tạo và mở biểu quyết', 'POST', '/api/meetings/{{meetingId}}/votes', 'approverToken',
      { title: 'Thông qua kế hoạch quý', description: '', options: ['Đồng ý', 'Không đồng ý'], isAnonymous: false,
        eligibleRoles: ['Chair', 'Secretary', 'Member'], openImmediately: true },
      [status(200), code('MSG217'), "pm.collectionVariables.set('voteId', pm.response.json().voteId);"]),
    req('API29', 'Xem biểu quyết đang mở', 'GET', '/api/meetings/{{meetingId}}/votes/{{voteId}}', 'token', null, [
      status(200),
      "const v = pm.response.json();",
      "pm.test('đang Open, có 2 phương án', () => { pm.expect(v.status).to.eql('Open'); pm.expect(v.options).to.have.lengthOf(2); });",
      "pm.collectionVariables.set('optionId', v.options[0].id ?? v.options[0].optionId);",
    ]),
    req('API30', 'Thành viên bỏ phiếu', 'POST', '/api/meetings/{{meetingId}}/votes/{{voteId}}/ballots', 'token',
      { optionId: '{{optionId}}' }, [status(200), code('MSG227')]),
    req('API31', 'Bỏ phiếu lần hai → MSG226', 'POST', '/api/meetings/{{meetingId}}/votes/{{voteId}}/ballots', 'token',
      { optionId: '{{optionId}}' }, [status(409), code('MSG226')]),
    req('API32', 'Chủ toạ kết thúc biểu quyết', 'POST', '/api/meetings/{{meetingId}}/votes/{{voteId}}/close', 'approverToken',
      null, [status(200), code('MSG229')]),
  ] },
  { name: '6. Thông báo', item: [
    req('API33', 'Danh sách thông báo của Thành viên', 'GET', '/api/notifications/?page=1&pageSize=10', 'token', null, [
      status(200), "pm.test('có items', () => pm.expect(pm.response.json().items).to.be.an('array'));"]),
    req('API34', 'Số thông báo chưa đọc', 'GET', '/api/notifications/unread-count', 'token', null, [
      status(200), "pm.test('là số', () => pm.expect(pm.response.json().unreadCount).to.be.a('number'));"]),
    req('API35', 'Đánh dấu đã đọc tất cả', 'POST', '/api/notifications/read-all', 'token', null, [status(200)]),
  ] },
];

const collection = {
  info: {
    name: 'TrustCabinet API — luồng chính',
    description: 'Luồng chính của TrustCabinet: đăng nhập, cuộc họp (tạo, duyệt, công bố, huỷ), chương trình nghị sự, thành phần, tài liệu, điểm danh, biểu quyết, thông báo. Chạy tuần tự trên app local đã seed, cùng môi trường TrustCabinet-local. Xem tests/postman/README.md.',
    schema: 'https://schema.getpostman.com/json/collection/v2.1.0/collection.json',
  },
  item,
  variable: ['meetingId', 'cancelMeetingId', 'organizerId', 'approverId', 'secretaryId', 'startAt', 'endAt', 'stamp',
    'attendanceId', 'attendanceVersion', 'voteId', 'optionId'].map((key) => ({ key, value: '' })),
};
fs.writeFileSync(path.join(here, 'TrustCabinet-API.postman_collection.json'), JSON.stringify(collection, null, 2) + '\n');
console.log('requests:', item.reduce((n, f) => n + f.item.length, 0));

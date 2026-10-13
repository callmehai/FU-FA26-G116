// Uses the existing frontend SignalR dependency; run npm ci in frontend first.
import { createRequire } from 'node:module';
const require = createRequire(new URL('../../../frontend/package.json', import.meta.url));
const { HubConnectionBuilder, HttpTransportType, LogLevel } = require('@microsoft/signalr');
const base = process.env.AGENDA_BASE_URL ?? 'http://localhost:5225';
const meetingId = process.env.AGENDA_MEETING_ID;
const token = process.env.AGENDA_TOKEN;
if (!meetingId || !token) throw new Error('Cần đặt AGENDA_MEETING_ID và AGENDA_TOKEN trong môi trường local.');
const connection = new HubConnectionBuilder()
  .withUrl(`${base}/hubs/meetings`, { accessTokenFactory: () => token, transport: HttpTransportType.LongPolling })
  .withAutomaticReconnect([0, 2000, 5000, 10000])
  .configureLogging(LogLevel.Warning).build();
let reload = Promise.resolve();
function refresh() {
  reload = reload.then(async () => {
    const response = await fetch(`${base}/api/meetings/${meetingId}/agenda`, { headers: { Authorization: `Bearer ${token}` } });
    if (!response.ok) throw new Error(`Tải agenda thất bại: ${response.status}`);
    const data = await response.json();
    console.log(JSON.stringify({ meetingStatus: data.meetingStatus, orderVersion: data.orderVersion, items: data.items }, null, 2));
  }).catch(error => console.error(error.message));
  return reload;
}
async function join() { await connection.invoke('JoinMeeting', meetingId); await refresh(); }
connection.on('agendaChanged', event => { console.log('agendaChanged', event); void refresh(); });
connection.onreconnected(join);
connection.onclose(error => console.error('Đã ngắt kết nối', error?.message ?? ''));
await connection.start();
await join();
const timer = setInterval(refresh, 30000);
process.on('SIGINT', async () => { clearInterval(timer); await connection.invoke('LeaveMeeting', meetingId).catch(() => {}); await connection.stop(); process.exit(0); });

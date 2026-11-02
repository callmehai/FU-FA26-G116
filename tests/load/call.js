// The call room's own API under the "everyone joins at 8:30 sharp" burst: each member reads the room, asks for a
// LiveKit token, then holds the meeting hub (SignalR) open as the room page does, for HOLD seconds. Media itself is
// measured apart with `lk load-test` (see README): k6 cannot speak WebRTC.
//
//   k6 run -e FIXTURE=out/fixture.json -e TABS=2 -e SPREAD=10 -e HOLD=120 tests/load/call.js
//
// TABS: open pages per member (a second device, or a tab left open), so 20 members × TABS=3 = 60 hub connections.
// SPREAD: seconds over which the joins arrive.
import http from 'k6/http'
import ws from 'k6/ws'
import { check, sleep } from 'k6'
import { Counter, Trend } from 'k6/metrics'
import { login, fixture, meetingsOf } from './lib.js'

const TABS = Number(__ENV.TABS || 1)
const SPREAD = Number(__ENV.SPREAD || 10)
const HOLD = Number(__ENV.HOLD || 60)
const members = fixture().members.length

const joinTime = new Trend('call_join', true)
const hubReady = new Trend('hub_ready', true)
const hubMessages = new Counter('hub_messages')
const hubFailed = new Counter('hub_failed')
const RS = '\u001e'

export const options = {
  scenarios: {
    burst: { executor: 'per-vu-iterations', vus: members * TABS, iterations: 1, maxDuration: `${SPREAD + HOLD + 120}s` },
  },
  thresholds: {
    call_join: ['p(95)<1500'],
    http_req_failed: ['rate<0.01'],
    hub_failed: ['count==0'],
  },
}

export function setup() {
  const f = fixture()
  return { meeting: meetingsOf(f, ['Online', 'Hybrid'])[0], tokens: f.members.map((email) => login(f.base, email)) }
}

export default function (data) {
  const f = fixture()
  const token = data.tokens[(__VU - 1) % data.tokens.length]
  const auth = { headers: { authorization: `Bearer ${token}`, 'content-type': 'application/json' } }
  sleep(Math.random() * SPREAD)

  const room = http.get(`${f.base}/api/meetings/${data.meeting}/call`, { ...auth, tags: { name: 'GET call' } })
  const join = http.post(`${f.base}/api/meetings/${data.meeting}/call/join`, '{}', { ...auth, tags: { name: 'POST call/join' } })
  joinTime.add(room.timings.duration + join.timings.duration)
  check(join, { 'LiveKit token issued': (r) => r.status === 200 && !!r.json('token') })

  const url = `${f.base.replace(/^http/, 'ws')}/hubs/meetings?access_token=${token}`
  const opened = Date.now()
  const res = ws.connect(url, {}, (socket) => {
    socket.on('open', () => socket.send(JSON.stringify({ protocol: 'json', version: 1 }) + RS))
    socket.on('message', (msg) => {
      for (const frame of msg.split(RS).filter(Boolean)) {
        const m = JSON.parse(frame)
        if (m.type === undefined && !m.error) {
          // Handshake accepted: join the meeting's groups the way the room page does.
          socket.send(JSON.stringify({ type: 1, invocationId: '1', target: 'JoinMeeting', arguments: [data.meeting] }) + RS)
        } else if (m.type === 3 && m.invocationId === '1') {
          if (m.error) { hubFailed.add(1); socket.close() } else hubReady.add(Date.now() - opened)
        } else if (m.type === 1) {
          hubMessages.add(1)
        } else if (m.type === 6) {
          socket.send(JSON.stringify({ type: 6 }) + RS)
        } else if (m.type === 7) {
          hubFailed.add(1)
        }
      }
    })
    socket.setInterval(() => socket.send(JSON.stringify({ type: 6 }) + RS), 15000)
    socket.setTimeout(() => socket.close(), HOLD * 1000)
  })
  if (!res || res.status !== 101) hubFailed.add(1)
}

// Per-person microphones (SCRUM-109): members send one sentence at a time while the meeting runs, and each sentence
// is transcribed before the request returns. The arrival rate climbs in steps so the report shows where the speech
// worker stops keeping up: the server sends at most 2 sentences to it at once, the rest wait in line.
//
//   k6 run -e FIXTURE=out/fixture.json -e PEAK=60 tests/load/speech-clips.js
//
// PEAK is sentences per minute across the whole server at the last step. A real meeting with one speaker at a time
// produces about 10–15 per minute; a hybrid room where three laptops hear the same speaker, three times that.
import http from 'k6/http'
import { check } from 'k6'
import { Counter, Trend } from 'k6/metrics'
import { login, fixture, meetingsOf } from './lib.js'

const PEAK = Number(__ENV.PEAK || 60)
const STEP = __ENV.STEP || '2m'
const clips = Array.from({ length: 12 }, (_, i) => open(`./out/audio/clip${String(i + 1).padStart(2, '0')}.wav`, 'b'))
// Seconds of speech in each file: 16 kHz mono 16-bit after a 44-byte header.
const seconds = clips.map((c) => (c.byteLength - 44) / 32000)

const transcribed = new Counter('clips_transcribed')
const silent = new Counter('clips_silent')
const busy = new Counter('clips_worker_busy')
const audio = new Counter('clip_audio_seconds')
const roundTrip = new Trend('clip_round_trip', true)

export const options = {
  scenarios: {
    speakers: {
      executor: 'ramping-arrival-rate',
      startRate: Math.max(1, Math.round(PEAK / 6)),
      timeUnit: '1m',
      preAllocatedVUs: 20,
      maxVUs: 200,
      stages: [
        { target: Math.round(PEAK / 6), duration: STEP },
        { target: Math.round(PEAK / 3), duration: STEP },
        { target: Math.round(PEAK / 2), duration: STEP },
        { target: PEAK, duration: STEP },
        { target: PEAK, duration: STEP },
      ],
    },
  },
  thresholds: {
    // A sentence should appear on screen within a few seconds of being spoken.
    // Past 30 s the queue only grows: stop instead of piling more onto a shared server.
    clip_round_trip: ['p(95)<10000', { threshold: 'p(95)<30000', abortOnFail: true, delayAbortEval: '30s' }],
    http_req_failed: ['rate<0.01'],
  },
}

export function setup() {
  const f = fixture()
  const meeting = meetingsOf(f, ['Online', 'Hybrid'])[0]
  return { meeting, tokens: f.members.map((email) => login(f.base, email)) }
}

export default function (data) {
  const f = fixture()
  const n = Math.floor(Math.random() * clips.length)
  const token = data.tokens[(__VU + __ITER) % data.tokens.length]
  const startedAt = new Date(Date.now() - seconds[n] * 1000).toISOString()
  const url = `${f.base}/api/meetings/${data.meeting}/speech-clips/${uuid()}`
    + `?startedAt=${encodeURIComponent(startedAt)}&durationMs=${Math.round(seconds[n] * 1000)}`
  const res = http.put(url, clips[n], {
    headers: { authorization: `Bearer ${token}`, 'content-type': 'audio/wav' },
    responseCallback: http.expectedStatuses(200, 204),
    timeout: '120s',
    tags: { name: 'PUT speech-clip' },
  })
  roundTrip.add(res.timings.duration)
  if (res.status === 200) { transcribed.add(1); audio.add(seconds[n]) }
  else if (res.status === 204) silent.add(1)
  else if (res.status === 503) busy.add(1)
  check(res, { 'sentence stored': (r) => r.status === 200 })
}

function uuid() {
  const h = '0123456789abcdef'
  let s = ''
  for (let i = 0; i < 32; i++) s += h[Math.floor(Math.random() * 16)]
  return `${s.slice(0, 8)}-${s.slice(8, 12)}-4${s.slice(13, 16)}-a${s.slice(17, 20)}-${s.slice(20, 32)}`
}

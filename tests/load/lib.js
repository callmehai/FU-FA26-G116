// Shared by the k6 scripts: the fixture written by setup.py, and signing in.
import http from 'k6/http'

const raw = open(__ENV.FIXTURE || './out/fixture.json')
const memberPassword = __ENV.LOAD_MEMBER_PASSWORD || 'LoadTest@2026-members'

export function fixture() {
  return JSON.parse(raw)
}

export function meetingsOf(f, modes) {
  const ids = f.meetings.filter((m) => modes.includes(m.mode)).map((m) => m.id)
  if (ids.length === 0) throw new Error(`fixture has no ${modes.join('/')} meeting`)
  return ids
}

export function login(base, email, password = memberPassword) {
  const res = http.post(`${base}/api/auth/login`, JSON.stringify({ email, password }), {
    headers: { 'content-type': 'application/json' },
    tags: { name: 'POST login' },
  })
  if (res.status !== 200) throw new Error(`login ${email}: ${res.status} ${res.body}`)
  return res.json('accessToken')
}

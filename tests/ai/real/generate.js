/*
 * TrustCabinet — AI-draft evaluation on real meetings: ask the app itself for drafts and save them for kit.py.
 *
 * Paste into the DevTools console of a tab signed in at https://app.trustcabinet.workspace-hub.space, as the Chair or
 * Secretary of the meetings. The app writes every draft with its in-house model (ADR-0088). Then, for example:
 *
 *   const r = await tcEval.draft('<meeting id>')             // { content, model, elapsedMs, ... }
 *   tcEval.save(r, 'test-mic-local-1')                       // downloads test-mic-local-1.md and test-mic-local-1.json
 *   await tcEval.campaign([{ id: '<meeting id>', slug: 'test-mic' }], { runs: 3 })   // three drafts per meeting
 *
 * No secret lives here. The access token is obtained the way the app does (features/auth/session.ts): POST
 * /api/auth/refresh with the HttpOnly refresh cookie the browser already holds, inside the Web Lock
 * 'trustcabinet.refresh' so it never races the app's own refresh (a refresh token used twice ends every session).
 * The token stays in this closure. Asking for a draft REPLACES the meeting's stored draft (and any edit or review of it);
 * export.py keeps a copy of the one there before.
 */
(() => {
  const LOCK = 'trustcabinet.refresh'
  const SIGNED_IN_KEY = 'trustcabinet.signedIn'
  let token = null
  let expiresAt = 0
  const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms))

  async function accessToken(force = false) {
    if (!force && token && Date.now() < expiresAt - 60_000) return token
    const work = async () => {
      if (localStorage.getItem(SIGNED_IN_KEY) === null) throw new Error('This tab is not signed in.')
      const res = await fetch('/api/auth/refresh', { method: 'POST', headers: { Accept: 'application/json' } })
      if (!res.ok) throw new Error(`POST /api/auth/refresh → HTTP ${res.status}`)
      const body = await res.json()
      token = body.accessToken
      expiresAt = Date.parse(body.accessTokenExpiresAt)
      return token
    }
    return navigator.locks ? navigator.locks.request(LOCK, work) : work()
  }

  async function call(method, path, body) {
    const send = async (force) =>
      fetch(path, {
        method,
        headers: {
          Accept: 'application/json',
          Authorization: `Bearer ${await accessToken(force)}`,
          ...(body === undefined ? {} : { 'Content-Type': 'application/json' }),
        },
        body: body === undefined ? undefined : JSON.stringify(body),
      })
    let res = await send(false)
    if (res.status === 401) res = await send(true)
    const text = await res.text()
    const data = text ? JSON.parse(text) : null
    if (!res.ok) {
      const error = new Error(`${method} ${path} → HTTP ${res.status} ${data?.code ?? ''} ${data?.message ?? ''}`.trim())
      error.status = res.status
      error.data = data
      throw error
    }
    return data
  }

  /** The same three sections export.py writes, so kit.py splits every draft alike. */
  function toMarkdown(draft) {
    return (
      `## I. Mục tiêu và phạm vi cuộc họp\n\n${(draft.objectives ?? '').trim()}\n\n` +
      `## II. Nội dung thảo luận\n\n${(draft.discussion ?? '').trim()}\n\n` +
      `## III. Kết luận\n\n${(draft.conclusions ?? '').trim()}\n`
    )
  }

  /**
   * Asks for a new draft of one meeting and waits until it is Ready (or Failed). elapsedMs runs from the POST to the
   * first poll that sees it Ready, so it includes any wait in the queue and is coarse by `pollMs`.
   */
  async function draft(meetingId, { expectModel = null, pollMs = 2000, timeoutMs = 30 * 60_000 } = {}) {
    const path = `/api/meetings/${meetingId}/summary`
    const before = await call('GET', path)
    if (!before.canManage) throw new Error('Only the Chair or the Secretary of this meeting can ask for a draft.')
    if (['Queued', 'Running'].includes(before.summary?.status)) throw new Error('A draft is already being written; wait for it.')
    const startedAt = new Date().toISOString()
    const t0 = performance.now()
    const queued = await call('POST', path)
    const requestedAt = queued.summary?.requestedAt
    for (;;) {
      await sleep(pollMs)
      const state = await call('GET', path)
      const summary = state.summary
      if (summary && summary.requestedAt === requestedAt && (summary.status === 'Ready' || summary.status === 'Failed')) {
        const elapsedMs = Math.round(performance.now() - t0)
        if (summary.status === 'Failed') throw new Error(`Draft failed after ${elapsedMs} ms: ${summary.errorMessage}`)
        const result = {
          meetingId,
          content: toMarkdown(summary.draft),
          draft: summary.draft,
          model: summary.model,
          elapsedMs,
          startedAt,
          requestedAt,
          transcriptChangedAt: state.readiness?.transcriptChangedAt ?? null,
          sentences: state.readiness?.sentences ?? null,
        }
        if (expectModel && summary.model !== expectModel) {
          result.modelMismatch = true
          console.warn(`Expected ${expectModel} but the draft was written by ${summary.model}.`)
        }
        return result
      }
      if (performance.now() - t0 > timeoutMs) throw new Error(`No draft after ${timeoutMs / 60000} minutes.`)
    }
  }

  function download(name, text, type) {
    const url = URL.createObjectURL(new Blob([text], { type }))
    const a = Object.assign(document.createElement('a'), { href: url, download: name })
    document.body.append(a)
    a.click()
    a.remove()
    setTimeout(() => URL.revokeObjectURL(url), 10_000)
  }

  /** Saves `<name>.md` (the draft) and `<name>.json` (model, elapsedMs, …) to the Downloads folder. */
  function save(result, name) {
    const { content, draft: _draft, ...meta } = result
    download(`${name}.md`, content, 'text/markdown;charset=utf-8')
    download(`${name}.json`, JSON.stringify({ ...meta, source: 'generate.js' }, null, 1), 'application/json')
  }

  /** Every meeting, `runs` drafts from the in-house model, saved as <slug>-<provider>-<run>. */
  async function campaign(meetings, { runs = 3, provider = 'local', pollMs = 2000 } = {}) {
    const done = []
    for (const { id, slug } of meetings) {
      for (let run = 1; run <= runs; run++) {
        const result = await draft(id, { pollMs })
        save(result, `${slug}-${provider}-${run}`)
        done.push({ slug, provider, run, model: result.model, elapsedMs: result.elapsedMs })
        console.log(`${slug}-${provider}-${run}: ${result.model}, ${(result.elapsedMs / 1000).toFixed(1)} s`)
      }
    }
    console.table(done)
    return done
  }

  window.tcEval = { draft, save, campaign }
  console.log('tcEval ready: draft, save, campaign')
})()

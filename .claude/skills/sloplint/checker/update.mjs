// Tells someone running a pinned or cached sloplint that a newer release exists. Asks GitHub at most
// once a day, gives up after a second, stays silent on any error, and never runs in CI or when
// SLOPLINT_NO_UPDATE_CHECK is set.
import { existsSync, readFileSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const REPO = 'callmehai/sloplint'
const CACHE = join(tmpdir(), 'sloplint-update.json')
const DAY = 24 * 60 * 60 * 1000

/** Whether version `a` (v-prefixed or not) is newer than `b`. */
export function newer(a, b) {
  const parts = (v) => String(v).replace(/^v/, '').split(/[.-]/).slice(0, 3).map((x) => Number.parseInt(x, 10) || 0)
  const [x, y] = [parts(a), parts(b)]
  for (let i = 0; i < 3; i++) if (x[i] !== y[i]) return x[i] > y[i]
  return false
}

/** The running version, from the package this file ships in; undefined for a copied skill folder. */
export function currentVersion() {
  const p = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..', 'package.json')
  try { const pkg = JSON.parse(readFileSync(p, 'utf8')); return pkg.name === 'sloplint' ? pkg.version : undefined } catch { return undefined }
}

/** Resolves to a one-line notice when a newer release exists, otherwise to undefined. Never rejects. */
export async function checkForUpdate(env = process.env) {
  const current = currentVersion()
  if (!current || env.CI || env.SLOPLINT_NO_UPDATE_CHECK) return undefined
  try {
    let cache = existsSync(CACHE) ? JSON.parse(readFileSync(CACHE, 'utf8')) : {}
    if (!(Date.now() - (cache.checkedAt ?? 0) < DAY)) {
      const res = await fetch(`https://api.github.com/repos/${REPO}/releases/latest`, {
        headers: { accept: 'application/vnd.github+json', 'user-agent': `sloplint/${current}` },
        signal: AbortSignal.timeout(1000),
      })
      if (!res.ok) return undefined
      cache = { checkedAt: Date.now(), latest: (await res.json()).tag_name }
      writeFileSync(CACHE, JSON.stringify(cache))
    }
    if (!cache.latest || !newer(cache.latest, current)) return undefined
    return `sloplint ${cache.latest} is out (this is v${current}). Run it with npx github:${REPO}#${cache.latest}\n` +
      `What changed: https://github.com/${REPO}/releases/tag/${cache.latest}  (SLOPLINT_NO_UPDATE_CHECK=1 turns this off)`
  } catch {
    return undefined
  }
}

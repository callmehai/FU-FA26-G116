// Runs one QA plan against a running TrustCabinet and writes the evidence into out/<ticket>/<time>-<commit>/.
//
//   node run.mjs plans/smoke.mjs                       # every criterion, on the devices the plan names
//   node run.mjs plans/scrum-27.mjs --only AC2,AC5     # some criteria
//   node run.mjs plans/scrum-27.mjs --headed           # watch it
//   node run.mjs plans/scrum-27.mjs --base https://trustcabinet.workspace-hub.space   # read-only plans only
//
// Exit code 1 when a criterion fails, 2 when the run itself could not start. The app is never started here: start it
// first (.claude/skills/verify-ui-in-browser §1), so the run tests what is really running.
import { chromium } from '@playwright/test'
import { execSync } from 'node:child_process'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath, pathToFileURL } from 'node:url'
import { Inconclusive, openCriterion } from './lib/qa.mjs'
import { writeReport } from './lib/report.mjs'

const here = dirname(fileURLToPath(import.meta.url))
const args = process.argv.slice(2)
const flag = (name) => {
  const i = args.indexOf(`--${name}`)
  return i >= 0 ? (args[i + 1] ?? '') : undefined
}
const VALUED = new Set(['--only', '--device', '--base'])
const planPath = args.find((a, i) => !a.startsWith('--') && !VALUED.has(args[i - 1]))
if (!planPath) {
  console.error('usage: node run.mjs <plan.mjs> [--only AC1,AC2] [--device desktop|phone] [--base URL] [--headed]')
  process.exit(2)
}
const base = (flag('base') ?? process.env.QA_BASE ?? 'http://localhost:5173').replace(/\/$/, '')
const only = flag('only')?.split(',').map((s) => s.trim())
const deviceOverride = flag('device')
const headed = args.includes('--headed')

const plan = (await import(pathToFileURL(resolve(planPath)).href)).default
if (!plan?.ticket || !Array.isArray(plan.criteria)) {
  console.error(`${planPath}: a plan exports default { ticket, title, criteria: [...] }`)
  process.exit(2)
}

// Test what is running: the app must answer, and the report names the commit it was built from.
// Another dev server on the same port answers every path with its own index.html, so check it is really this app.
try {
  const health = await fetch(`${base}/api/health`)
  const body = await health.json().catch(() => ({}))
  if (!health.ok || body.service !== 'TrustCabinet.Web') throw new Error(`HTTP ${health.status}, not TrustCabinet`)
  if (body.database !== 'ok') throw new Error(`database ${body.database}`)
} catch (error) {
  console.error(`${base}/api/health: ${error.message}. Start the app first (or pass --base).`)
  process.exit(2)
}
const git = (cmd) => {
  try {
    return execSync(`git ${cmd}`, { cwd: here, encoding: 'utf8' }).trim()
  } catch {
    return ''
  }
}
const commit = git('rev-parse --short HEAD') || 'unknown'
const dirty = git('status --porcelain -- ":!tests/qa/out"') !== ''

const startedAt = new Date()
const stamp = startedAt.toISOString().replace(/[-:]/g, '').replace('T', '-').slice(0, 15)
const outDir = join(here, 'out', plan.ticket, `${stamp}-${commit}`)
const browser = await chromium.launch({
  headless: !headed,
  args: ['--use-fake-ui-for-media-stream', '--use-fake-device-for-media-stream'],
})
const counter = { n: 0 }
const results = []

for (const criterion of plan.criteria) {
  if (only && !only.includes(criterion.id)) continue
  const devices = deviceOverride ? [deviceOverride] : (criterion.devices ?? plan.devices ?? ['desktop'])
  for (const device of devices) {
    const t0 = Date.now()
    let verdict = 'PASS'
    let message = ''
    // A criterion's own data that must exist before it signs in (an account the seed lacks): a setup that fails is a
    // failure of the criterion, not "could not open", so it is not mistaken for a machine without the right tools.
    if (criterion.prepare) {
      const failed = await criterion.prepare({ base, device }).then(
        () => null,
        (error) => error,
      )
      if (failed) {
        const message = `prepare: ${String(failed.message ?? failed).slice(0, 1500)}`
        results.push(record(criterion, device, failed instanceof Inconclusive ? 'INCONCLUSIVE' : 'FAIL', message, t0))
        console.log(`FAIL  ${criterion.id} [${criterion.role ?? 'anon'}/${device}] ${message}`)
        continue
      }
    }
    const opened = await openCriterion({
      browser,
      base,
      outDir,
      criterion,
      device,
      counter,
      allowHosts: plan.allowHosts ?? [],
    }).catch((error) => ({ error }))
    if (opened.error) {
      results.push(record(criterion, device, 'INCONCLUSIVE', `could not open: ${opened.error.message}`, t0))
      continue
    }
    const { qa, close } = opened
    try {
      await criterion.run(qa)
    } catch (error) {
      verdict = error instanceof Inconclusive ? 'INCONCLUSIVE' : 'FAIL'
      message = String(error.message ?? error).replace(/\u001b\[[0-9;]*m/g, '').slice(0, 1500)
      // A criterion that only called the API never opened a page: a blank shot is no evidence.
      if (qa.page.url() !== 'about:blank') await qa.shot(verdict === 'FAIL' ? 'failure' : 'inconclusive').catch(() => undefined)
    }
    const seen = await close()
    if (verdict === 'PASS' && seen.outside.length) {
      verdict = 'FAIL'
      message = 'The page called a host outside the app; the test server CSP blocks it.'
    }
    if (verdict === 'PASS' && seen.pageErrors.length && !criterion.allowPageErrors) {
      verdict = 'FAIL'
      message = 'The page threw an uncaught error.'
    }
    results.push({ ...record(criterion, device, verdict, message, t0), ...seen })
    const mark = { PASS: 'PASS', FAIL: 'FAIL', INCONCLUSIVE: '????' }[verdict]
    console.log(`${mark}  ${criterion.id} [${qa.role}/${device}] ${criterion.title}${message ? `\n      ${message.split('\n')[0]}` : ''}`)
  }
}

await browser.close()

function record(criterion, device, verdict, message, t0) {
  return {
    id: criterion.id,
    title: criterion.title,
    role: criterion.role ?? 'anon',
    device,
    steps: criterion.steps,
    expected: criterion.expected,
    verdict,
    message,
    ms: Date.now() - t0,
    shots: [],
    notes: [],
    outside: [],
    pageErrors: [],
  }
}

const run = {
  ticket: plan.ticket,
  title: plan.title ?? '',
  base,
  commit,
  dirty,
  startedAt: startedAt.toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' }),
  results,
}
writeReport(outDir, run)
const failed = results.filter((r) => r.verdict === 'FAIL').length
const inconclusive = results.filter((r) => r.verdict === 'INCONCLUSIVE').length
console.log(`\n${results.length - failed - inconclusive} pass, ${failed} fail, ${inconclusive} inconclusive`)
console.log(`report:  ${join(outDir, 'report.html')}`)
console.log(`jira:    ${join(outDir, 'summary.md')}`)
process.exit(failed ? 1 : 0)

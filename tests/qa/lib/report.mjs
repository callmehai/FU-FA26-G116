// Writes a run's three outputs: results.json (the data), report.html (what a person reads: the criteria table with
// every screenshot, which zooms on click) and summary.md (the [qa-bot] comment to paste on the Jira ticket).
import { writeFileSync } from 'node:fs'
import { join } from 'node:path'

const VERDICT_LABEL = { PASS: 'Đạt', FAIL: 'Lỗi', INCONCLUSIVE: 'Chưa kết luận' }
const esc = (s) =>
  String(s ?? '')
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')

export function writeReport(outDir, run) {
  writeFileSync(join(outDir, 'results.json'), JSON.stringify(run, null, 2))
  writeFileSync(join(outDir, 'report.html'), html(run))
  writeFileSync(join(outDir, 'summary.md'), markdown(run))
}

function counts(results) {
  const c = { PASS: 0, FAIL: 0, INCONCLUSIVE: 0 }
  for (const r of results) c[r.verdict] += 1
  return c
}

function markdown(run) {
  const c = counts(run.results)
  const head = `[qa-bot] ${run.ticket} · ${run.title}\nBản đã kiểm: ${run.commit}${run.dirty ? ' (có thay đổi chưa commit)' : ''} · ${run.base} · ${run.startedAt}\nKết quả: ${c.PASS} đạt, ${c.FAIL} lỗi, ${c.INCONCLUSIVE} chưa kết luận.`
  const rows = run.results.map(
    (r) =>
      `| ${r.id} | ${r.title} | ${r.role} | ${r.device} | ${VERDICT_LABEL[r.verdict]} | ${(r.message || r.notes.join('; ')).replace(/\|/g, '/').replace(/\n/g, ' ').slice(0, 200)} |`,
  )
  const table = ['| AC | Tiêu chí | Vai trò | Thiết bị | Kết quả | Ghi chú |', '|---|---|---|---|---|---|', ...rows].join('\n')
  const defects = run.results
    .filter((r) => r.verdict !== 'PASS')
    .map(
      (r) =>
        `### ${r.id} · ${r.title} (${r.role}, ${r.device}) — ${VERDICT_LABEL[r.verdict]}\n` +
        `- Các bước: ${(r.steps ?? []).join(' → ') || '(xem kế hoạch)'}\n` +
        `- Mong đợi: ${r.expected ?? '(xem kế hoạch)'}\n` +
        `- Thực tế: ${r.message}\n` +
        (r.outside.length ? `- Gọi ra ngoài: ${r.outside.join(', ')}\n` : '') +
        (r.pageErrors.length ? `- Lỗi trên trang: ${r.pageErrors.join(' | ')}\n` : '') +
        `- Ảnh: ${r.shots.join(', ') || '(không có)'}`,
    )
  return [head, '', table, ...(defects.length ? ['', '## Lỗi và chưa kết luận', '', ...defects] : []), ''].join('\n')
}

function html(run) {
  const c = counts(run.results)
  const rows = run.results
    .map(
      (r) => `<tr class="v-${r.verdict}">
  <td>${esc(r.id)}</td>
  <td><b>${esc(r.title)}</b>${r.expected ? `<div class="sub">Mong đợi: ${esc(r.expected)}</div>` : ''}${
    r.message ? `<div class="msg">${esc(r.message)}</div>` : ''
  }${r.notes.map((n) => `<div class="sub">• ${esc(n)}</div>`).join('')}${
    r.outside.length ? `<div class="msg">Gọi ra ngoài: ${esc(r.outside.join(', '))}</div>` : ''
  }${r.pageErrors.length ? `<div class="msg">Lỗi trên trang: ${esc(r.pageErrors.join(' | '))}</div>` : ''}</td>
  <td>${esc(r.role)}</td><td>${esc(r.device)}</td>
  <td><span class="chip">${VERDICT_LABEL[r.verdict]}</span><div class="sub">${(r.ms / 1000).toFixed(1)} giây</div></td>
  <td class="shots">${r.shots.map((s) => `<img src="${esc(s)}" alt="${esc(s)}" title="${esc(s)}" loading="lazy">`).join('')}</td>
</tr>`,
    )
    .join('\n')
  return `<!doctype html>
<html lang="vi"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>QA ${esc(run.ticket)}</title>
<style>
:root{--ink:#1d2433;--muted:#5b6475;--line:#d9dee7;--pass:#146c2e;--fail:#b3261e;--inc:#8a5a00}
body{margin:0;padding:24px 16px;font:14px/1.5 system-ui,sans-serif;color:var(--ink);background:#f6f7f9}
main{max-width:1280px;margin:0 auto}h1{font-size:20px;margin:0 0 4px}.meta{color:var(--muted);margin-bottom:16px}
.counts span{margin-right:16px;font-weight:600}.counts .p{color:var(--pass)}.counts .f{color:var(--fail)}.counts .i{color:var(--inc)}
.wrap{overflow-x:auto;background:#fff;border:1px solid var(--line);border-radius:10px}
table{border-collapse:collapse;width:100%;min-width:860px}th,td{padding:10px;border-bottom:1px solid var(--line);vertical-align:top;text-align:left}
th{font-size:12px;color:var(--muted)}.sub{color:var(--muted);font-size:12px;margin-top:4px}.msg{color:var(--fail);font-size:12px;margin-top:4px;white-space:pre-wrap}
.chip{display:inline-block;padding:2px 8px;border-radius:999px;font-size:12px;font-weight:600;border:1px solid currentColor}
.v-PASS .chip{color:var(--pass)}.v-FAIL .chip{color:var(--fail)}.v-INCONCLUSIVE .chip{color:var(--inc)}
.shots img{width:180px;margin:0 6px 6px 0;border:1px solid var(--line);border-radius:6px;cursor:zoom-in}
dialog{padding:0;border:0;max-width:96vw;max-height:96vh;background:transparent}dialog img{max-width:96vw;max-height:92vh;display:block}
dialog::backdrop{background:rgba(0,0,0,.75)}
</style></head><body><main>
<h1>QA ${esc(run.ticket)} · ${esc(run.title)}</h1>
<div class="meta">Bản ${esc(run.commit)}${run.dirty ? ' (có thay đổi chưa commit)' : ''} · ${esc(run.base)} · ${esc(run.startedAt)}</div>
<p class="counts"><span class="p">${c.PASS} đạt</span><span class="f">${c.FAIL} lỗi</span><span class="i">${c.INCONCLUSIVE} chưa kết luận</span></p>
<div class="wrap"><table><thead><tr><th>AC</th><th>Tiêu chí</th><th>Vai trò</th><th>Thiết bị</th><th>Kết quả</th><th>Bằng chứng</th></tr></thead>
<tbody>${rows}</tbody></table></div>
</main>
<dialog id="zoom"><img alt=""></dialog>
<script>
const d=document.getElementById('zoom'),z=d.querySelector('img');
document.querySelectorAll('.shots img').forEach(i=>i.addEventListener('click',()=>{z.src=i.src;z.alt=i.alt;d.showModal()}));
d.addEventListener('click',()=>d.close());
</script></body></html>`
}

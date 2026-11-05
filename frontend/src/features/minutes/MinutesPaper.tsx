import type { ReactNode } from 'react'

/**
 * The minutes as a sheet of paper (SCRUM-32), drawn from the plain text and its line marks (MinutesContracts.cs):
 * `@@ a | b` rows, `#` the title, `^` a centred line, `##` / `###` headings, `- ` a listed line, `~ ` a small line,
 * `**bold**` and `__italic__` inside a line. Everything becomes React text, never HTML, so nothing in the minutes
 * can run in the browser.
 */
export function MinutesPaper({ content }: { content: string }) {
  return <article className="minutes-paper">{blocks(content)}</article>
}

function blocks(content: string): ReactNode[] {
  const out: ReactNode[] = []
  let rows: string[] = []
  const flush = () => {
    if (rows.length === 0) return
    out.push(
      <table key={`t${out.length}`} className="minutes-frame">
        <tbody>
          {rows.map((row, i) => {
            const [left, right = ''] = row.split('|', 2)
            return (
              <tr key={i}>
                <td>{inline(left.trim())}</td>
                <td>{inline(right.trim())}</td>
              </tr>
            )
          })}
        </tbody>
      </table>,
    )
    rows = []
  }
  for (const raw of content.replace(/\r/g, '').split('\n')) {
    const line = raw.trimEnd()
    if (line.startsWith('@@')) {
      rows.push(line.slice(2).trim())
      continue
    }
    flush()
    if (!line) continue
    const key = `l${out.length}`
    if (line.startsWith('# ')) out.push(<h2 key={key} className="minutes-title">{inline(line.slice(2))}</h2>)
    else if (line.startsWith('^ ')) out.push(<p key={key} className="minutes-center">{inline(line.slice(2))}</p>)
    else if (line.startsWith('### ')) out.push(<p key={key} className="minutes-sub">{inline(line.slice(4))}</p>)
    else if (line.startsWith('## ')) out.push(<h3 key={key} className="minutes-section">{inline(line.slice(3))}</h3>)
    else if (line.startsWith('~ ')) out.push(<p key={key} className="minutes-small">{inline(line.slice(2))}</p>)
    else out.push(<p key={key}>{inline(line)}</p>)
  }
  flush()
  return out
}

/** `**bold**` and `__italic__`, toggled as they open and close. */
function inline(text: string): ReactNode[] {
  const parts = text.split(/(\*\*|__)/)
  const out: ReactNode[] = []
  let bold = false
  let italic = false
  parts.forEach((part, i) => {
    if (part === '**') bold = !bold
    else if (part === '__') italic = !italic
    else if (part) {
      let node: ReactNode = part
      if (italic) node = <em key={`e${i}`}>{node}</em>
      if (bold) node = <strong key={`s${i}`}>{node}</strong>
      out.push(typeof node === 'string' ? <span key={i}>{node}</span> : node)
    }
  })
  return out
}

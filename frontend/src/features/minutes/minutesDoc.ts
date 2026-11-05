import type { JSONContent } from '@tiptap/core'

/**
 * The minutes' line-marked text (MinutesContracts.cs) to and from the editor's document, so the Word-like editor edits
 * the very text the page, the Word file and the PDF are drawn from. One line is one block:
 *
 * | Line          | Block                                   |
 * |---------------|-----------------------------------------|
 * | `# x`         | heading 1 (the title, "BIÊN BẢN")       |
 * | `## x`        | heading 2 (a section, I–III)            |
 * | `### x`       | heading 3 (a bold sub-heading)          |
 * | `^ x`         | paragraph, variant "center"             |
 * | `~ x`         | paragraph, variant "small"              |
 * | `@@ a \| b`   | a row of a two-column table             |
 * | anything else | paragraph ("- " lines stay as typed)    |
 *
 * `**bold**` and `__italic__` are the two marks inside a line. Blank lines carry nothing on paper and are dropped.
 */

export type Variant = 'center' | 'small' | null

type Marks = { bold: boolean; italic: boolean }

export function toDoc(content: string): JSONContent {
  const blocks: JSONContent[] = []
  let rows: JSONContent[] = []
  const flush = () => {
    if (rows.length > 0) blocks.push({ type: 'table', content: rows })
    rows = []
  }
  for (const raw of content.replace(/\r/g, '').split('\n')) {
    const line = raw.trimEnd()
    if (line.startsWith('@@')) {
      // Cut at the first bar only, as the Word file and the PDF do: the rest of the row is the right cell.
      const row = line.slice(2).trim()
      const bar = row.indexOf('|')
      const [left, right] = bar < 0 ? [row, ''] : [row.slice(0, bar), row.slice(bar + 1)]
      rows.push({ type: 'tableRow', content: [cell(left.trim()), cell(right.trim())] })
      continue
    }
    flush()
    if (!line) continue
    if (line.startsWith('### ')) blocks.push(heading(3, line.slice(4)))
    else if (line.startsWith('## ')) blocks.push(heading(2, line.slice(3)))
    else if (line.startsWith('# ')) blocks.push(heading(1, line.slice(2)))
    else if (line.startsWith('^ ')) blocks.push(paragraph(line.slice(2), 'center'))
    else if (line.startsWith('~ ')) blocks.push(paragraph(line.slice(2), 'small'))
    else blocks.push(paragraph(line, null))
  }
  flush()
  if (blocks.length === 0) blocks.push({ type: 'paragraph' })
  return { type: 'doc', content: blocks }
}

export function toContent(doc: JSONContent): string {
  const lines: string[] = []
  for (const block of doc.content ?? []) {
    if (block.type === 'table') {
      for (const row of block.content ?? []) {
        // The sheet has two columns: a pasted wider row keeps its other cells in the right one rather than losing them.
        const [left, ...rest] = (row.content ?? []).map((c) => cellText(c).replace(/\|/g, '/'))
        const right = rest.filter((t) => t).join(' / ')
        lines.push(`@@ ${left ? `${left} ` : ''}| ${right}`.trimEnd())
      }
      continue
    }
    const text = inlineText(block.content)
    if (!text.trim()) continue
    if (block.type === 'heading') {
      const level = Number(block.attrs?.level ?? 2)
      lines.push(`${level === 1 ? '#' : level === 3 ? '###' : '##'} ${text}`)
    } else if (block.attrs?.variant === 'center') lines.push(`^ ${text}`)
    else if (block.attrs?.variant === 'small') lines.push(`~ ${text}`)
    else lines.push(lineSafe(text))
  }
  return lines.join('\n') + '\n'
}

/** A plain line must not start with a mark it does not mean: typed "# x" in a paragraph stays a paragraph. */
function lineSafe(text: string): string {
  return /^(#{1,3} |\^ |~ |@@)/.test(text) ? `${ZWSP}${text}` : text
}

/** A zero-width space: invisible on paper, it keeps characters that would read as a mark from reading as one. */
const ZWSP = '\u200B'

/** Appends a piece so that no marker forms across the seam: `a*` then `**` would otherwise read `***`. */
function append(out: string, piece: string): string {
  const last = out.at(-1)
  return (last === '*' || last === '_') && piece.startsWith(last) ? `${out}${ZWSP}${piece}` : out + piece
}

function heading(level: 1 | 2 | 3, text: string): JSONContent {
  return { type: 'heading', attrs: { level }, content: inline(text) }
}

function paragraph(text: string, variant: Variant): JSONContent {
  return { type: 'paragraph', attrs: { variant }, content: inline(text) }
}

function cell(text: string): JSONContent {
  return { type: 'tableCell', content: [{ type: 'paragraph', attrs: { variant: null }, content: inline(text) }] }
}

/** `**bold**` and `__italic__`, toggled as they open and close, the way MinutesPaper reads them. */
function inline(text: string): JSONContent[] | undefined {
  const out: JSONContent[] = []
  const state: Marks = { bold: false, italic: false }
  for (const part of text.split(/(\*\*|__)/)) {
    if (part === '**') state.bold = !state.bold
    else if (part === '__') state.italic = !state.italic
    else if (part) {
      const marks = [...(state.bold ? [{ type: 'bold' }] : []), ...(state.italic ? [{ type: 'italic' }] : [])]
      out.push(marks.length > 0 ? { type: 'text', text: part, marks } : { type: 'text', text: part })
    }
  }
  return out.length > 0 ? out : undefined
}

function inlineText(content: JSONContent[] | undefined): string {
  let out = ''
  const open: Marks = { bold: false, italic: false }
  for (const node of content ?? []) {
    const text = node.type === 'hardBreak' ? ' ' : (node.text ?? '')
    if (!text) continue
    const want: Marks = {
      bold: !!node.marks?.some((m) => m.type === 'bold'),
      italic: !!node.marks?.some((m) => m.type === 'italic'),
    }
    // Close inner (italic) before outer (bold), open outer before inner, so the toggles nest.
    if (open.italic && (!want.italic || (open.bold && !want.bold))) {
      out = append(out, '__')
      open.italic = false
    }
    if (open.bold && !want.bold) {
      out = append(out, '**')
      open.bold = false
    }
    if (want.bold && !open.bold) {
      out = append(out, '**')
      open.bold = true
    }
    if (want.italic && !open.italic) {
      out = append(out, '__')
      open.italic = true
    }
    // Markers typed as text ("Họ tên: ______", "a***b") would toggle marks on reading: part every repeated one.
    out = append(out, text.replace(/([*_])(?=\1)/g, `$1${ZWSP}`))
  }
  if (open.italic) out = append(out, '__')
  if (open.bold) out = append(out, '**')
  return out.replace(/\n/g, ' ')
}

function cellText(cell: JSONContent): string {
  return (cell.content ?? [])
    .map((p) => inlineText(p.content))
    .filter((t) => t.trim())
    .join(' ')
}

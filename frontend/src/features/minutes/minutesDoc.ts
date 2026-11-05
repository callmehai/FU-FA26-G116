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
      const [left, right = ''] = line.slice(2).trim().split('|', 2)
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
        const [left, right] = (row.content ?? []).map((c) => cellText(c).replace(/\|/g, '/'))
        lines.push(`@@ ${left ? `${left} ` : ''}| ${right ?? ''}`.trimEnd())
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
  return /^(#{1,3} |\^ |~ |@@)/.test(text) ? `​${text}` : text
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
      out += '__'
      open.italic = false
    }
    if (open.bold && !want.bold) {
      out += '**'
      open.bold = false
    }
    if (want.bold && !open.bold) {
      out += '**'
      open.bold = true
    }
    if (want.italic && !open.italic) {
      out += '__'
      open.italic = true
    }
    // A marker typed as text would toggle a mark on reading: keep it as text.
    out += text.replace(/\*\*/g, '*​*').replace(/__/g, '_​_')
  }
  if (open.italic) out += '__'
  if (open.bold) out += '**'
  return out.replace(/\n/g, ' ')
}

function cellText(cell: JSONContent): string {
  return (cell.content ?? [])
    .map((p) => inlineText(p.content))
    .filter((t) => t.trim())
    .join(' ')
}

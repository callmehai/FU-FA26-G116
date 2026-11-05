import { Extension, Node, type Editor } from '@tiptap/core'
import { Plugin, PluginKey, type EditorState } from '@tiptap/pm/state'
import { Decoration, DecorationSet } from '@tiptap/pm/view'
import type { Node as PmNode } from '@tiptap/pm/model'
import { Subscript } from '@tiptap/extension-subscript'
import { Superscript } from '@tiptap/extension-superscript'
import { Table, TableCell, TableHeader, TableRow, TableView } from '@tiptap/extension-table'
import { TextAlign } from '@tiptap/extension-text-align'
import { BackgroundColor, Color, FontSize, TextStyle } from '@tiptap/extension-text-style'
import StarterKit from '@tiptap/starter-kit'

/**
 * The minutes document the editor writes (SCRUM-32, ADR-0055): sanitized HTML that the server keeps to one allow-list
 * (MinutesHtml.cs) and the page, the Word file and the PDF all read. Nothing here may write an element, attribute or
 * style the allow-list lacks, or the server drops it on save and the sheet changes under the writer's hands.
 */

/** Word's standard colours and its highlighter pens: the only ones the allow-list keeps. */
export const TEXT_COLORS = [
  { value: '#000000', label: 'Đen' },
  { value: '#c00000', label: 'Đỏ sẫm' },
  { value: '#ff0000', label: 'Đỏ' },
  { value: '#ed7d31', label: 'Cam' },
  { value: '#00b050', label: 'Xanh lá' },
  { value: '#0070c0', label: 'Xanh dương' },
  { value: '#002060', label: 'Xanh đậm' },
  { value: '#7030a0', label: 'Tím' },
] as const
export const HIGHLIGHTS = [
  { value: '#ffff00', label: 'Vàng' },
  { value: '#00ff00', label: 'Xanh lá' },
  { value: '#00ffff', label: 'Xanh ngọc' },
  { value: '#ff00ff', label: 'Hồng' },
  { value: '#d9d9d9', label: 'Xám' },
] as const

/** The first line of a body paragraph is indented this much (Decree 30/2020); one indent step is the same. */
export const INDENT_MM = 12.7

declare module '@tiptap/core' {
  interface Commands<ReturnType> {
    pageBreak: { insertPageBreak: () => ReturnType }
  }
}

type BlockAttr = 'textIndent' | 'marginLeft' | 'lineHeight' | 'marginTop' | 'marginBottom'
const BLOCK_STYLES: Record<BlockAttr, string> = {
  textIndent: 'text-indent',
  marginLeft: 'margin-left',
  lineHeight: 'line-height',
  marginTop: 'margin-top',
  marginBottom: 'margin-bottom',
}

/** A paragraph's or heading's indents and spacing, each one style property the allow-list knows. */
const BlockFormat = Extension.create({
  name: 'blockFormat',
  addGlobalAttributes() {
    return [
      {
        types: ['paragraph', 'heading'],
        attributes: Object.fromEntries(
          (Object.entries(BLOCK_STYLES) as [BlockAttr, string][]).map(([key, property]) => [
            key,
            {
              default: null,
              parseHTML: (element: HTMLElement) => element.style.getPropertyValue(property) || null,
              renderHTML: (attributes: Record<string, unknown>) =>
                attributes[key] ? { style: `${property}: ${String(attributes[key])}` } : {},
            },
          ]),
        ),
      },
    ]
  },
})

/**
 * `line-height` is Word's "multiple" spacing, as the Word file and the PDF read it, and one Word line is about 1.15 em of
 * Times New Roman: on screen the sheet draws it that much taller, so a paragraph set to 1.5 looks as it will print.
 * A decoration, so the HTML keeps Word's number.
 */
export const SINGLE_LINE = 1.15

const WordLineHeight = Extension.create({
  name: 'wordLineHeight',
  addProseMirrorPlugins() {
    return [
      new Plugin({
        props: {
          decorations(state) {
            const found: Decoration[] = []
            state.doc.descendants((node, pos) => {
              const value = parseFloat(String(node.attrs.lineHeight ?? ''))
              if (value > 0) found.push(Decoration.node(pos, pos + node.nodeSize, { style: `line-height: ${(value * SINGLE_LINE).toFixed(3)}` }))
              return node.isBlock && !node.isTextblock
            })
            return DecorationSet.create(state.doc, found)
          },
        },
      }),
    ]
  },
})

/** A table drawn without borders, as the header and the signature blocks are: `data-layout="frame"`. */
const MinutesTable = Table.extend({
  addAttributes() {
    return {
      ...this.parent?.(),
      layout: {
        default: null,
        parseHTML: (element: HTMLElement) => (element.getAttribute('data-layout') === 'frame' ? 'frame' : null),
        renderHTML: (attributes: Record<string, unknown>) => (attributes.layout ? { 'data-layout': 'frame' } : {}),
      },
    }
  },
})

/**
 * The resizable table's node view draws its own `<table>` and drops the node's attributes: put the frame mark back on
 * it, so a borderless table looks borderless while it is edited too.
 */
class MinutesTableView extends TableView {
  constructor(...args: ConstructorParameters<typeof TableView>) {
    super(...args)
    this.mark(args[0])
  }

  update(node: PmNode) {
    const kept = super.update(node)
    if (kept) this.mark(node)
    return kept
  }

  private mark(node: PmNode) {
    if (node.attrs.layout === 'frame') this.table.setAttribute('data-layout', 'frame')
    else this.table.removeAttribute('data-layout')
  }
}

/** A page break: `<hr data-page-break="true">`, read before the plain rule. */
const PageBreak = Node.create({
  name: 'pageBreak',
  group: 'block',
  atom: true,
  selectable: true,
  parseHTML() {
    return [{ tag: 'hr[data-page-break]', priority: 60 }]
  },
  renderHTML() {
    return ['hr', { 'data-page-break': 'true' }]
  },
  addCommands() {
    return {
      insertPageBreak:
        () =>
        ({ chain }) =>
          chain().insertContent({ type: this.name }).run(),
    }
  },
})

export function minutesExtensions() {
  return [
    StarterKit.configure({
      heading: { levels: [1, 2, 3] },
      blockquote: false,
      code: false,
      codeBlock: false,
      link: false,
      // The default dashed outline around a dragged block reads as a frame on the sheet.
      dropcursor: { color: 'var(--brand)', width: 2 },
    }),
    TextStyle,
    Color,
    BackgroundColor,
    FontSize,
    Subscript,
    Superscript,
    TextAlign.configure({ types: ['heading', 'paragraph'], alignments: ['left', 'center', 'right', 'justify'] }),
    BlockFormat,
    WordLineHeight,
    MinutesTable.configure({ resizable: true, cellMinWidth: 40, lastColumnResizable: false, View: MinutesTableView }),
    TableRow,
    TableHeader,
    TableCell,
    PageBreak,
    FindReplace,
  ]
}

/**
 * Column widths are shares of the text width (the page, the Word file and the PDF spread a table over it). Stored in
 * pixels of whatever sheet they were last set on, so on opening they are scaled to this sheet: the table then fills it
 * exactly and dragging a border moves it where the pointer is. Left out of the undo history: it is no edit.
 */
export function fitTables(editor: Editor) {
  const sheet = editor.view.dom
  const style = getComputedStyle(sheet)
  const width = sheet.clientWidth - parseFloat(style.paddingLeft) - parseFloat(style.paddingRight)
  if (width <= 0) return
  const { tr } = editor.state
  editor.state.doc.descendants((table, at) => {
    if (table.type.name !== 'table') return true
    const widths: number[] = []
    table.firstChild?.forEach((cell) => {
      const set = cell.attrs.colwidth as number[] | null
      for (let i = 0; i < Number(cell.attrs.colspan ?? 1); i++) widths.push(set?.[i] ?? 0)
    })
    const known = widths.filter((w) => w > 0)
    if (known.length === 0) return false
    const filler = known.reduce((a, b) => a + b, 0) / known.length
    const total = widths.reduce((sum, w) => sum + (w || filler), 0)
    if (Math.abs(total - width) < 2) return false
    const scale = width / total
    table.descendants((cell, offset) => {
      if (cell.type.name !== 'tableCell' && cell.type.name !== 'tableHeader') return true
      const set = cell.attrs.colwidth as number[] | null
      if (set) tr.setNodeMarkup(at + 1 + offset, undefined, { ...cell.attrs, colwidth: set.map((w) => Math.max(40, Math.round(w * scale))) })
      return false
    })
    return false
  })
  if (tr.docChanged) editor.view.dispatch(tr.setMeta('addToHistory', false))
}

// ---- Paragraph commands the toolbar uses ------------------------------------------------------------------------

const BLOCKS = ['paragraph', 'heading'] as const

/** Sets block attributes on every paragraph and heading in the selection. */
export function setBlock(editor: Editor, attributes: Partial<Record<BlockAttr, string | null>>) {
  let chain = editor.chain().focus()
  for (const type of BLOCKS) chain = chain.updateAttributes(type, attributes)
  chain.run()
}

export function blockAttr(editor: Editor, key: BlockAttr): string | null {
  const type = editor.isActive('heading') ? 'heading' : 'paragraph'
  return (editor.getAttributes(type)[key] as string | null | undefined) ?? null
}

/** A length the allow-list accepts, in millimetres: "12.7mm" → 12.7, "18pt" → 6.35. */
export function millimetres(value: string | null): number {
  if (!value) return 0
  const n = parseFloat(value)
  if (Number.isNaN(n)) return 0
  return value.endsWith('pt') ? (n * 25.4) / 72 : n
}

const mm = (n: number) => `${Math.round(n * 10) / 10}mm`

/**
 * Inside a list indenting nests the item; elsewhere each selected paragraph's left edge moves one step from where it
 * is, as Word does, so paragraphs indented differently keep their difference.
 */
export function indent(editor: Editor, step: 1 | -1) {
  if (editor.isActive('listItem')) {
    if (step > 0) editor.chain().focus().sinkListItem('listItem').run()
    else editor.chain().focus().liftListItem('listItem').run()
    return
  }
  const { state, view } = editor
  const tr = state.tr
  for (const range of state.selection.ranges)
    state.doc.nodesBetween(range.$from.pos, range.$to.pos, (node, pos) => {
      if (node.type.name !== 'paragraph' && node.type.name !== 'heading') return true
      const next = Math.min(150, Math.max(0, millimetres((node.attrs.marginLeft as string | null) ?? null) + step * INDENT_MM))
      tr.setNodeMarkup(pos, undefined, { ...node.attrs, marginLeft: next > 0 ? mm(next) : null })
      return false
    })
  if (tr.docChanged) view.dispatch(tr)
  editor.commands.focus()
}

/** In a cell or a list a paragraph starts flush; elsewhere its first line is indented unless told otherwise. */
function indentedByDefault(editor: Editor) {
  // The title (h1) is centred and never indented; neither is a paragraph in a cell or a list.
  return (
    !editor.isActive('heading', { level: 1 }) &&
    !editor.isActive('tableCell') &&
    !editor.isActive('tableHeader') &&
    !editor.isActive('listItem')
  )
}

export function firstLineIndented(editor: Editor): boolean {
  const value = blockAttr(editor, 'textIndent')
  return value === null ? indentedByDefault(editor) : millimetres(value) > 0
}

export function toggleFirstLine(editor: Editor) {
  const byDefault = indentedByDefault(editor)
  const on = firstLineIndented(editor)
  setBlock(editor, { textIndent: on ? (byDefault ? '0mm' : null) : byDefault ? null : mm(INDENT_MM) })
}

/** Back to a plain body paragraph: no marks, no alignment, no indents or spacing. */
export function clearFormatting(editor: Editor) {
  let chain = editor.chain().focus().unsetAllMarks().unsetTextAlign()
  for (const type of BLOCKS)
    chain = chain.updateAttributes(type, { textIndent: null, marginLeft: null, lineHeight: null, marginTop: null, marginBottom: null })
  chain.run()
}

export type TextCase = 'upper' | 'lower' | 'words'

/** Changes the case of the selected text in place, keeping each piece's marks. */
export function changeCase(editor: Editor, to: TextCase) {
  const { state, view } = editor
  if (state.selection.empty) return
  const tr = state.tr
  const pieces: { pos: number; node: PmNode; start: number; stop: number }[] = []
  // Each range of the selection: one for text, one per cell when cells are selected.
  for (const range of state.selection.ranges) {
    const from = range.$from.pos
    const end = range.$to.pos
    state.doc.nodesBetween(from, end, (node, pos) => {
      if (!node.isText) return
      pieces.push({ pos, node, start: Math.max(from, pos) - pos, stop: Math.min(end, pos + node.nodeSize) - pos })
    })
  }
  pieces.sort((a, b) => a.pos - b.pos)
  // Back to front, so the positions of the pieces not yet changed stay where they were.
  for (const { pos, node, start, stop } of pieces.reverse()) {
    const text = node.text!.slice(start, stop)
    const changed =
      to === 'upper'
        ? text.toLocaleUpperCase('vi')
        : to === 'lower'
          ? text.toLocaleLowerCase('vi')
          : text.toLocaleLowerCase('vi').replace(/(^|[\s(“"'-])(\p{L})/gu, (_, gap: string, letter: string) => gap + letter.toLocaleUpperCase('vi'))
    if (changed !== text) tr.replaceWith(pos + start, pos + stop, state.schema.text(changed, node.marks))
  }
  if (tr.docChanged) view.dispatch(tr)
  editor.commands.focus()
}

// ---- Find and replace -------------------------------------------------------------------------------------------

type Match = { from: number; to: number }
type FindState = { query: string; matches: Match[]; current: number; decorations: DecorationSet }

export const findKey = new PluginKey<FindState>('minutesFind')

function search(doc: PmNode, query: string): Match[] {
  const out: Match[] = []
  if (!query) return out
  const needle = query.toLocaleLowerCase('vi')
  doc.descendants((node, pos) => {
    if (!node.isTextblock) return true
    // A block's text with each character's document position: a hard break ends a run, so no match spans it.
    let text = ''
    const at: number[] = []
    node.forEach((child, offset) => {
      if (child.isText) {
        for (let i = 0; i < child.text!.length; i++) at.push(pos + 1 + offset + i)
        text += child.text
      } else {
        at.push(-1)
        text += '\n'
      }
    })
    const hay = text.toLocaleLowerCase('vi')
    for (let i = hay.indexOf(needle); i >= 0; i = hay.indexOf(needle, i + needle.length)) {
      const first = at[i]
      const last = at[i + needle.length - 1]
      if (first >= 0 && last >= 0 && !hay.slice(i, i + needle.length).includes('\n')) out.push({ from: first, to: last + 1 })
    }
    return false
  })
  return out
}

function decorate(doc: PmNode, matches: Match[], current: number) {
  return DecorationSet.create(
    doc,
    matches.map((m, i) => Decoration.inline(m.from, m.to, { class: i === current ? 'find-match is-current' : 'find-match' })),
  )
}

const FindReplace = Extension.create({
  name: 'findReplace',
  addProseMirrorPlugins() {
    return [
      new Plugin<FindState>({
        key: findKey,
        state: {
          init: () => ({ query: '', matches: [], current: 0, decorations: DecorationSet.empty }),
          apply(tr, previous, _old, state) {
            const meta = tr.getMeta(findKey) as Partial<Pick<FindState, 'query' | 'current'>> | undefined
            if (!meta && !tr.docChanged) return previous
            const query = meta?.query ?? previous.query
            const matches = search(state.doc, query)
            const wanted = meta?.current ?? previous.current
            const current = matches.length === 0 ? 0 : ((wanted % matches.length) + matches.length) % matches.length
            return { query, matches, current, decorations: decorate(state.doc, matches, current) }
          },
        },
        props: {
          decorations: (state) => findKey.getState(state)?.decorations,
        },
      }),
    ]
  },
})

export function findState(state: EditorState): FindState {
  return findKey.getState(state) ?? { query: '', matches: [], current: 0, decorations: DecorationSet.empty }
}

export function setQuery(editor: Editor, query: string) {
  editor.view.dispatch(editor.state.tr.setMeta(findKey, { query, current: 0 }))
  reveal(editor)
}

export function step(editor: Editor, by: 1 | -1) {
  const { current } = findState(editor.state)
  editor.view.dispatch(editor.state.tr.setMeta(findKey, { current: current + by }))
  reveal(editor)
}

/** Selects the current match and scrolls it into view, without taking focus from the find field. */
function reveal(editor: Editor) {
  const { matches, current } = findState(editor.state)
  const match = matches[current]
  if (!match) return
  editor.chain().setTextSelection(match).scrollIntoView().run()
}

export function replaceCurrent(editor: Editor, text: string) {
  const { query, matches, current } = findState(editor.state)
  const match = matches[current]
  if (!match) return
  const tr = editor.state.tr.insertText(text, match.from, match.to)
  // Go on past the inserted text: a replacement that contains the query (ubnd → UBND, Văn A → Ông Văn A) would
  // otherwise be the current match again, and the next "Thay" would stay put or replace inside its own words.
  const end = match.from + text.length
  const next = search(tr.doc, query).findIndex((m) => m.from >= end)
  editor.view.dispatch(tr.setMeta(findKey, { current: next < 0 ? 0 : next }))
  reveal(editor)
}

export function replaceAll(editor: Editor, text: string): number {
  const { matches } = findState(editor.state)
  if (matches.length === 0) return 0
  const tr = editor.state.tr
  for (const m of [...matches].reverse()) tr.insertText(text, m.from, m.to)
  editor.view.dispatch(tr)
  return matches.length
}

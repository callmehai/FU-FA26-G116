import { useEditorState, type Editor } from '@tiptap/react'
import { useCallback, useRef, useState, type KeyboardEvent, type ReactNode } from 'react'
import { Icon, type IconName } from '../../../components/Icon'
import { Select } from '../../../components/Select'
import { useAnchoredPopover, useOutsidePress } from '../../../components/useAnchoredPopover'
import {
  HIGHLIGHTS,
  TEXT_COLORS,
  blockAttr,
  changeCase,
  clearFormatting,
  firstLineIndented,
  indent,
  setBlock,
  toggleFirstLine,
  type TextCase,
} from './schema'

type Block = 'paragraph' | 'h1' | 'h2' | 'h3'
const BLOCKS: { value: Block; label: string }[] = [
  { value: 'paragraph', label: 'Đoạn văn' },
  { value: 'h1', label: 'Tiêu đề biên bản' },
  { value: 'h2', label: 'Tiêu đề mục' },
  { value: 'h3', label: 'Tiêu đề nhỏ' },
]

/** Point sizes, as Word lists them; 13 pt is the body size of Decree 30 and is what "no size" means. */
const SIZES = ['10', '11', '12', '13', '14', '16', '18', '20', '24'] as const
type Size = (typeof SIZES)[number]
const BODY_SIZE: Size = '13'

const SPACINGS = [
  { value: '1', label: '1,0' },
  { value: '1.15', label: '1,15' },
  { value: null, label: '1,2 (mặc định)' },
  { value: '1.5', label: '1,5' },
  { value: '2', label: '2,0' },
] as const

/** Characters a minutes needs that a keyboard lacks; every one is in the PDF's font. */
const SYMBOLS = ['–', '—', '…', '•', '§', '№', '°', '±', '×', '÷', '≤', '≥', '≠', '≈', '→', '←', '«', '»', '“', '”', '©', '®', '½', '¼']

const CASES: { value: TextCase; label: string }[] = [
  { value: 'upper', label: 'VIẾT HOA TẤT CẢ' },
  { value: 'lower', label: 'viết thường tất cả' },
  { value: 'words', label: 'Viết Hoa Đầu Mỗi Từ' },
]

/**
 * The editor's ribbon, in Word's order: history, styles, character, colour, paragraph, lists, insert, tools. Each
 * control writes only what the minutes' allow-list keeps (schema.ts); the last group is Huỷ / Lưu, so they stay in
 * view however long the minutes are.
 */
export function Toolbar({ editor, onFind, actions }: { editor: Editor; onFind: () => void; actions: ReactNode }) {
  const s = useEditorState({
    editor,
    selector: ({ editor: e }) => ({
      block: (e.isActive('heading', { level: 1 })
        ? 'h1'
        : e.isActive('heading', { level: 2 })
          ? 'h2'
          : e.isActive('heading', { level: 3 })
            ? 'h3'
            : 'paragraph') as Block,
      size: (String(e.getAttributes('textStyle').fontSize ?? '').replace(/pt$/, '') || BODY_SIZE) as Size,
      color: hex(e.getAttributes('textStyle').color),
      highlight: hex(e.getAttributes('textStyle').backgroundColor),
      bold: e.isActive('bold'),
      italic: e.isActive('italic'),
      underline: e.isActive('underline'),
      strike: e.isActive('strike'),
      sup: e.isActive('superscript'),
      sub: e.isActive('subscript'),
      align: (['left', 'center', 'right', 'justify'] as const).find((a) => e.isActive({ textAlign: a })) ?? null,
      lineHeight: blockAttr(e, 'lineHeight'),
      spaceBefore: !!blockAttr(e, 'marginTop'),
      spaceAfter: !!blockAttr(e, 'marginBottom'),
      firstLine: firstLineIndented(e),
      bullet: e.isActive('bulletList'),
      ordered: e.isActive('orderedList'),
      inTable: e.isActive('table'),
      frame: e.isActive('table', { layout: 'frame' }),
      canMerge: e.can().mergeCells(),
      canSplit: e.can().splitCell(),
      canUndo: e.can().undo(),
      canRedo: e.can().redo(),
    }),
  })
  const chain = () => editor.chain().focus()

  const setBlockType = (block: Block) => {
    if (block === 'paragraph') chain().setParagraph().run()
    else chain().setHeading({ level: Number(block[1]) as 1 | 2 | 3 }).run()
  }

  return (
    <div className="minutes-toolbar" role="toolbar" aria-label="Định dạng biên bản">
      <div className="minutes-toolbar-tools">
        <Group>
          <Tool icon="editUndo" label="Hoàn tác (Ctrl+Z)" disabled={!s.canUndo} onClick={() => chain().undo().run()} />
          <Tool icon="editRedo" label="Làm lại (Ctrl+Y)" disabled={!s.canRedo} onClick={() => chain().redo().run()} />
        </Group>
        <Group>
          <div className="minutes-toolbar-block">
            <Select<Block> label="Kiểu đoạn" size="sm" value={s.block} options={BLOCKS} onChange={setBlockType} tooltip="Kiểu đoạn" />
          </div>
          <div className="minutes-toolbar-size">
            <Select<Size>
              label="Cỡ chữ"
              size="sm"
              value={s.size}
              options={SIZES.map((v) => ({ value: v, label: v }))}
              onChange={(v) => (v === BODY_SIZE ? chain().unsetFontSize().run() : chain().setFontSize(`${v}pt`).run())}
              tooltip="Cỡ chữ (pt)"
            />
          </div>
        </Group>
        <Group>
          <Tool icon="bold" label="Đậm (Ctrl+B)" pressed={s.bold} onClick={() => chain().toggleBold().run()} />
          <Tool icon="italic" label="Nghiêng (Ctrl+I)" pressed={s.italic} onClick={() => chain().toggleItalic().run()} />
          <Tool icon="underline" label="Gạch chân (Ctrl+U)" pressed={s.underline} onClick={() => chain().toggleUnderline().run()} />
          <Tool icon="strike" label="Gạch ngang (Ctrl+Shift+S)" pressed={s.strike} onClick={() => chain().toggleStrike().run()} />
          <Tool icon="superscript" label="Chỉ số trên (Ctrl+.)" pressed={s.sup} onClick={() => chain().toggleSuperscript().run()} />
          <Tool icon="subscript" label="Chỉ số dưới (Ctrl+,)" pressed={s.sub} onClick={() => chain().toggleSubscript().run()} />
          <ToolMenu icon="textCase" label="Chữ hoa, chữ thường">
            {(close) => (
              <MenuList
                items={CASES.map((c) => ({ key: c.value, label: c.label, onSelect: () => changeCase(editor, c.value) }))}
                close={close}
              />
            )}
          </ToolMenu>
        </Group>
        <Group>
          <ToolMenu icon="textColor" label="Màu chữ" swatch={s.color ?? '#000000'}>
            {(close) => (
              <Swatches
                label="Màu chữ"
                colors={TEXT_COLORS}
                current={s.color}
                none="Tự động"
                onPick={(c) => {
                  if (c) chain().setColor(c).run()
                  else chain().unsetColor().run()
                  close()
                }}
              />
            )}
          </ToolMenu>
          <ToolMenu icon="highlight" label="Tô nền chữ" swatch={s.highlight ?? 'transparent'}>
            {(close) => (
              <Swatches
                label="Tô nền chữ"
                colors={HIGHLIGHTS}
                current={s.highlight}
                none="Không tô"
                onPick={(c) => {
                  if (c) chain().setBackgroundColor(c).run()
                  else chain().unsetBackgroundColor().run()
                  close()
                }}
              />
            )}
          </ToolMenu>
        </Group>
        <Group>
          <Tool icon="alignLeft" label="Căn trái (Ctrl+Shift+L)" pressed={s.align === 'left'} onClick={() => chain().setTextAlign('left').run()} />
          <Tool icon="alignCenter" label="Căn giữa (Ctrl+Shift+E)" pressed={s.align === 'center'} onClick={() => chain().setTextAlign('center').run()} />
          <Tool icon="alignRight" label="Căn phải (Ctrl+Shift+R)" pressed={s.align === 'right'} onClick={() => chain().setTextAlign('right').run()} />
          <Tool icon="alignJustify" label="Căn đều hai bên (Ctrl+Shift+J)" pressed={s.align === 'justify'} onClick={() => chain().setTextAlign('justify').run()} />
          <ToolMenu icon="lineSpacing" label="Giãn dòng và khoảng cách đoạn">
            {(close) => (
              <MenuList
                close={close}
                items={[
                  ...SPACINGS.map((sp) => ({
                    key: `lh-${sp.value}`,
                    label: `Giãn dòng ${sp.label}`,
                    checked: s.lineHeight === sp.value,
                    onSelect: () => setBlock(editor, { lineHeight: sp.value }),
                  })),
                  {
                    key: 'before',
                    label: 'Cách trên đoạn 6 pt',
                    checked: s.spaceBefore,
                    separated: true,
                    onSelect: () => setBlock(editor, { marginTop: s.spaceBefore ? null : '6pt' }),
                  },
                  {
                    key: 'after',
                    label: 'Cách dưới đoạn 6 pt',
                    checked: s.spaceAfter,
                    onSelect: () => setBlock(editor, { marginBottom: s.spaceAfter ? null : '6pt' }),
                  },
                ]}
              />
            )}
          </ToolMenu>
          <Tool icon="outdent" label="Giảm thụt lề" onClick={() => indent(editor, -1)} />
          <Tool icon="indent" label="Tăng thụt lề" onClick={() => indent(editor, 1)} />
          <Tool icon="firstLine" label="Thụt dòng đầu 1,27 cm" pressed={s.firstLine} onClick={() => toggleFirstLine(editor)} />
        </Group>
        <Group>
          <Tool icon="bulletList" label="Danh sách dấu chấm (Ctrl+Shift+8)" pressed={s.bullet} onClick={() => chain().toggleBulletList().run()} />
          <Tool icon="orderedList" label="Danh sách số (Ctrl+Shift+7)" pressed={s.ordered} onClick={() => chain().toggleOrderedList().run()} />
        </Group>
        <Group>
          <ToolMenu icon="table" label={s.inTable ? 'Bảng' : 'Chèn bảng'} pressed={s.inTable}>
            {(close) =>
              s.inTable ? (
                <MenuList
                  close={close}
                  items={[
                    { key: 'ra', icon: 'rowAbove', label: 'Thêm hàng phía trên', onSelect: () => chain().addRowBefore().run() },
                    { key: 'rb', icon: 'rowAdd', label: 'Thêm hàng phía dưới', onSelect: () => chain().addRowAfter().run() },
                    { key: 'ca', icon: 'colAddBefore', label: 'Thêm cột bên trái', onSelect: () => chain().addColumnBefore().run() },
                    { key: 'cb', icon: 'colAdd', label: 'Thêm cột bên phải', onSelect: () => chain().addColumnAfter().run() },
                    { key: 'm', icon: 'cellsMerge', label: 'Gộp ô', disabled: !s.canMerge, separated: true, onSelect: () => chain().mergeCells().run() },
                    { key: 's', icon: 'cellsSplit', label: 'Tách ô', disabled: !s.canSplit, onSelect: () => chain().splitCell().run() },
                    {
                      key: 'f',
                      icon: 'tableFrame',
                      label: 'Ẩn đường kẻ',
                      checked: s.frame,
                      onSelect: () => chain().updateAttributes('table', { layout: s.frame ? null : 'frame' }).run(),
                    },
                    { key: 'dr', icon: 'rowRemove', label: 'Xoá hàng', separated: true, onSelect: () => chain().deleteRow().run() },
                    { key: 'dc', icon: 'colRemove', label: 'Xoá cột', onSelect: () => chain().deleteColumn().run() },
                    { key: 'dt', icon: 'trash', label: 'Xoá bảng', onSelect: () => chain().deleteTable().run() },
                  ]}
                />
              ) : (
                <TableGrid
                  onPick={(rows, cols) => {
                    chain().insertTable({ rows, cols, withHeaderRow: false }).run()
                    close()
                  }}
                />
              )
            }
          </ToolMenu>
          <Tool icon="rule" label="Đường kẻ ngang" onClick={() => chain().setHorizontalRule().run()} />
          <Tool icon="pageBreak" label="Ngắt trang" onClick={() => chain().insertPageBreak().run()} />
          <ToolMenu icon="symbol" label="Ký hiệu">
            {(close) => (
              <div className="tool-grid tool-symbols" role="group" aria-label="Ký hiệu">
                {SYMBOLS.map((c) => (
                  <button
                    key={c}
                    type="button"
                    className="tool-cell"
                    aria-label={`Chèn ${c}`}
                    onMouseDown={keep}
                    onClick={() => {
                      chain().insertContent(c).run()
                      close()
                    }}
                  >
                    {c}
                  </button>
                ))}
              </div>
            )}
          </ToolMenu>
        </Group>
        <Group last>
          <Tool icon="clearFormat" label="Xoá định dạng" onClick={() => clearFormatting(editor)} />
          <Tool icon="search" label="Tìm và thay thế (Ctrl+F)" onClick={onFind} />
        </Group>
      </div>
      <div className="row-actions minutes-toolbar-actions">{actions}</div>
    </div>
  )
}

/** A colour as the allow-list spells it: the browser hands styles back as rgb(r, g, b). */
function hex(value: unknown): string | null {
  if (typeof value !== 'string' || !value) return null
  const rgb = /^rgba?\((\d+),\s*(\d+),\s*(\d+)/.exec(value)
  if (!rgb) return value.toLowerCase()
  return `#${rgb.slice(1, 4).map((n) => Number(n).toString(16).padStart(2, '0')).join('')}`
}

function Group({ children, last }: { children: ReactNode; last?: boolean }) {
  return (
    <>
      <div className="minutes-toolbar-group" role="group">
        {children}
      </div>
      {!last && <span className="minutes-toolbar-sep" aria-hidden="true" />}
    </>
  )
}

/** Keeps the selection in the sheet: a press on the toolbar must not move the caret out of it. */
const keep = (event: { preventDefault: () => void }) => event.preventDefault()

function Tool({
  icon,
  label,
  pressed,
  disabled,
  onClick,
}: {
  icon: IconName
  label: string
  pressed?: boolean
  disabled?: boolean
  onClick: () => void
}) {
  return (
    <button
      type="button"
      className={pressed ? 'icon-button icon-button-sm is-pressed' : 'icon-button icon-button-sm'}
      aria-label={label}
      aria-pressed={pressed}
      data-tooltip={label}
      disabled={disabled}
      onMouseDown={keep}
      onClick={onClick}
    >
      <Icon name={icon} size={16} />
    </button>
  )
}

/** A toolbar button that opens a small panel under it: colours, a table grid, symbols, a short list of choices. */
function ToolMenu({
  icon,
  label,
  swatch,
  pressed,
  children,
}: {
  icon: IconName
  label: string
  swatch?: string
  pressed?: boolean
  children: (close: () => void) => ReactNode
}) {
  const [open, setOpen] = useState(false)
  const root = useRef<HTMLDivElement>(null)
  const trigger = useRef<HTMLButtonElement>(null)
  const panel = useRef<HTMLDivElement>(null)
  const close = useCallback(() => setOpen(false), [])
  useAnchoredPopover(open, trigger, panel, { matchWidth: false })
  useOutsidePress(open, root, close)
  const onKey = (event: KeyboardEvent) => {
    if (event.key !== 'Escape') return
    event.preventDefault()
    setOpen(false)
    trigger.current?.focus()
  }
  return (
    <div ref={root} className="tool-menu" onKeyDown={onKey}>
      <button
        ref={trigger}
        type="button"
        className={pressed || open ? 'icon-button icon-button-sm tool-menu-trigger is-pressed' : 'icon-button icon-button-sm tool-menu-trigger'}
        aria-label={label}
        aria-haspopup="true"
        aria-expanded={open}
        data-tooltip={open ? undefined : label}
        onMouseDown={keep}
        onClick={() => setOpen((v) => !v)}
      >
        <span className="tool-menu-glyph">
          <Icon name={icon} size={16} />
          {swatch && <span className="tool-swatch" style={{ background: swatch }} />}
        </span>
        <Icon name="chevronDown" size={12} />
      </button>
      {open && (
        <div ref={panel} popover="manual" className="select-menu tool-menu-panel" role="dialog" aria-label={label}>
          {children(close)}
        </div>
      )}
    </div>
  )
}

type ListItem = {
  key: string
  label: string
  icon?: IconName
  checked?: boolean
  disabled?: boolean
  separated?: boolean
  onSelect: () => void
}

function MenuList({ items, close }: { items: ListItem[]; close: () => void }) {
  return (
    <div className="tool-list" role="menu">
      {items.map((item) => (
        <button
          key={item.key}
          type="button"
          role={item.checked === undefined ? 'menuitem' : 'menuitemcheckbox'}
          aria-checked={item.checked}
          disabled={item.disabled}
          className={item.separated ? 'select-option tool-list-item is-separated' : 'select-option tool-list-item'}
          onMouseDown={keep}
          onClick={() => {
            item.onSelect()
            close()
          }}
        >
          <span className="tool-list-lead" aria-hidden="true">
            {item.icon ? <Icon name={item.icon} size={16} /> : item.checked ? <Icon name="check" size={16} /> : null}
          </span>
          {item.label}
          {item.icon && item.checked && (
            <span className="tool-list-check" aria-hidden="true">
              <Icon name="check" size={16} />
            </span>
          )}
        </button>
      ))}
    </div>
  )
}

function Swatches({
  label,
  colors,
  current,
  none,
  onPick,
}: {
  label: string
  colors: readonly { value: string; label: string }[]
  current: string | null
  none: string
  onPick: (color: string | null) => void
}) {
  return (
    <div className="tool-swatches">
      <button type="button" className="select-option tool-list-item" onMouseDown={keep} onClick={() => onPick(null)}>
        <span className="tool-swatch-chip is-none" aria-hidden="true" />
        {none}
      </button>
      <div className="tool-grid" role="group" aria-label={label}>
        {colors.map((c) => (
          <button
            key={c.value}
            type="button"
            className={current === c.value ? 'tool-cell is-current' : 'tool-cell'}
            aria-label={c.label}
            aria-pressed={current === c.value}
            data-tooltip={c.label}
            onMouseDown={keep}
            onClick={() => onPick(c.value)}
          >
            <span className="tool-swatch-chip" style={{ background: c.value }} aria-hidden="true" />
          </button>
        ))}
      </div>
    </div>
  )
}

/** Word's table grid: point at the size, press to insert. On a touch screen the first press inserts. */
function TableGrid({ onPick }: { onPick: (rows: number, cols: number) => void }) {
  const [at, setAt] = useState({ rows: 2, cols: 2 })
  const ROWS = 8
  const COLS = 6
  return (
    <div className="tool-table">
      <div className="tool-table-grid" role="grid" aria-label="Chọn số hàng và cột" style={{ gridTemplateColumns: `repeat(${COLS}, 1fr)` }}>
        {Array.from({ length: ROWS * COLS }, (_, i) => {
          const rows = Math.floor(i / COLS) + 1
          const cols = (i % COLS) + 1
          const lit = rows <= at.rows && cols <= at.cols
          return (
            <button
              key={i}
              type="button"
              className={lit ? 'tool-table-cell is-lit' : 'tool-table-cell'}
              aria-label={`${rows} hàng × ${cols} cột`}
              onMouseDown={keep}
              onPointerEnter={() => setAt({ rows, cols })}
              onFocus={() => setAt({ rows, cols })}
              onClick={() => onPick(rows, cols)}
            />
          )
        })}
      </div>
      <p className="tool-table-size">
        {at.rows} hàng × {at.cols} cột
      </p>
    </div>
  )
}

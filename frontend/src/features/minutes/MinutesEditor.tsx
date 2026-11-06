import { Extension } from '@tiptap/core'
import { Table, TableCell, TableHeader, TableRow } from '@tiptap/extension-table'
import { EditorContent, useEditor, useEditorState, type Editor } from '@tiptap/react'
import { useState, type ReactNode } from 'react'
import StarterKit from '@tiptap/starter-kit'
import { Icon, type IconName } from '../../components/Icon'
import { Select } from '../../components/Select'
import { toContent, toDoc, type Variant } from './minutesDoc'

/**
 * The minutes as a Word-like page to type on (SCRUM-32), as AI-eCabinet's draft editor is: a toolbar over the sheet,
 * the sheet set the way it prints. It edits the line-marked text the page, the Word file and the PDF are drawn from
 * (minutesDoc.ts), so what is typed here is exactly what they show; the toolbar offers what those can carry.
 */
export function MinutesEditor({
  content,
  onChange,
  actions,
}: {
  content: string
  onChange: (content: string) => void
  /** Huỷ / Lưu, at the toolbar's end: it stays in view however long the minutes are. */
  actions: ReactNode
}) {
  /** The text is read into the editor once; what is typed is then the editor's own (its options rebuild per render). */
  const [initial] = useState(() => toDoc(content))
  const editor = useEditor({
    extensions: [
      StarterKit.configure({
        heading: { levels: [1, 2, 3] },
        blockquote: false,
        bulletList: false,
        orderedList: false,
        listItem: false,
        listKeymap: false,
        code: false,
        codeBlock: false,
        horizontalRule: false,
        strike: false,
        underline: false,
        link: false,
      }),
      ParagraphVariant,
      Table.configure({ resizable: false, HTMLAttributes: { class: 'minutes-frame' } }),
      TableRow,
      TableHeader,
      TableCell,
    ],
    content: initial,
    editorProps: { attributes: { class: 'minutes-paper minutes-editor-doc', 'aria-label': 'Nội dung biên bản' } },
    onUpdate: ({ editor: e }) => onChange(toContent(e.getJSON())),
  })

  return (
    <div className="minutes-editor-frame">
      <Toolbar editor={editor} actions={actions} />
      <div className="minutes-editor-canvas">
        <EditorContent editor={editor} />
      </div>
    </div>
  )
}

type Style = 'body' | 'title' | 'section' | 'sub' | 'center' | 'small'

const STYLES: { value: Style; label: string }[] = [
  { value: 'body', label: 'Đoạn văn' },
  { value: 'title', label: 'Tiêu đề biên bản' },
  { value: 'section', label: 'Tiêu đề mục' },
  { value: 'sub', label: 'Tiêu đề nhỏ' },
  { value: 'center', label: 'Dòng căn giữa' },
  { value: 'small', label: 'Dòng nhỏ' },
]

function Toolbar({ editor, actions }: { editor: Editor; actions: ReactNode }) {
  const state = useEditorState({
    editor,
    selector: ({ editor: e }) => ({
      style: styleAt(e),
      bold: e.isActive('bold'),
      italic: e.isActive('italic'),
      center: e.isActive('paragraph', { variant: 'center' }),
      inTable: e.isActive('table'),
      canUndo: e.can().undo(),
      canRedo: e.can().redo(),
    }),
  })

  const setStyle = (style: Style) => {
    const chain = editor.chain().focus()
    if (style === 'title') chain.setHeading({ level: 1 }).run()
    else if (style === 'section') chain.setHeading({ level: 2 }).run()
    else if (style === 'sub') chain.setHeading({ level: 3 }).run()
    else chain.setParagraph().updateAttributes('paragraph', { variant: style === 'body' ? null : style }).run()
  }

  return (
    <div className="minutes-toolbar" role="toolbar" aria-label="Định dạng biên bản">
      <div className="minutes-toolbar-tools">
        <Tool icon="editUndo" label="Hoàn tác (Ctrl+Z)" disabled={!state.canUndo} onClick={() => editor.chain().focus().undo().run()} />
        <Tool icon="editRedo" label="Làm lại (Ctrl+Shift+Z)" disabled={!state.canRedo} onClick={() => editor.chain().focus().redo().run()} />
        <span className="minutes-toolbar-sep" aria-hidden="true" />
        <Tool icon="bold" label="Đậm (Ctrl+B)" pressed={state.bold} onClick={() => editor.chain().focus().toggleBold().run()} />
        <Tool icon="italic" label="Nghiêng (Ctrl+I)" pressed={state.italic} onClick={() => editor.chain().focus().toggleItalic().run()} />
        <Tool icon="alignCenter" label="Căn giữa" pressed={state.center} onClick={() => setStyle(state.center ? 'body' : 'center')} />
        {state.inTable && (
          <>
            <span className="minutes-toolbar-sep" aria-hidden="true" />
            <Tool icon="rowAdd" label="Thêm hàng hai cột bên dưới" onClick={() => editor.chain().focus().addRowAfter().run()} />
            <Tool icon="rowRemove" label="Xoá hàng này" onClick={() => editor.chain().focus().deleteRow().run()} />
          </>
        )}
        <span className="minutes-toolbar-sep" aria-hidden="true" />
      </div>
      {/* On a phone the tools take the first row; the style menu and Huỷ / Lưu share the second. */}
      <div className="minutes-toolbar-tail">
        <div className="minutes-toolbar-style">
          <Select<Style> label="Kiểu dòng" size="sm" value={state.style} options={STYLES} onChange={setStyle} />
        </div>
        <div className="row-actions minutes-toolbar-actions">{actions}</div>
      </div>
    </div>
  )
}

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
      // Keep the selection in the sheet: a click on the toolbar must not move the caret out of it.
      onMouseDown={(event) => event.preventDefault()}
      onClick={onClick}
    >
      <Icon name={icon} size={16} />
    </button>
  )
}

function styleAt(editor: Editor): Style {
  if (editor.isActive('heading', { level: 1 })) return 'title'
  if (editor.isActive('heading', { level: 2 })) return 'section'
  if (editor.isActive('heading', { level: 3 })) return 'sub'
  const variant = editor.getAttributes('paragraph').variant as Variant
  return variant ?? 'body'
}

/** A paragraph's place on the sheet: a centred line (`^`), a small left line (`~`), or a body paragraph. */
const ParagraphVariant = Extension.create({
  name: 'paragraphVariant',
  addGlobalAttributes() {
    return [
      {
        types: ['paragraph'],
        attributes: {
          variant: {
            default: null,
            parseHTML: (element) => element.getAttribute('data-variant'),
            renderHTML: (attributes) =>
              attributes.variant
                ? {
                    'data-variant': attributes.variant,
                    class: attributes.variant === 'center' ? 'minutes-center' : 'minutes-small',
                  }
                : {},
          },
        },
      },
    ]
  },
})

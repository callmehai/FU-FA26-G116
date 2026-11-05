import { EditorContent, useEditor } from '@tiptap/react'
import { useState, type ReactNode } from 'react'
import { FindBar } from './editor/FindBar'
import { fitTables, minutesExtensions } from './editor/schema'
import { Toolbar } from './editor/Toolbar'

/**
 * The minutes as a Word-like page to type on (SCRUM-32, ADR-0055), as AI-eCabinet's draft editor is: a ribbon over the
 * sheet, the sheet set the way it prints. It edits the sanitized HTML the page, the Word file and the PDF are drawn
 * from, so what is typed here is what they show.
 */
export function MinutesEditor({
  content,
  onChange,
  actions,
}: {
  /** Read once, when the editor opens: what is typed is then the editor's own. */
  content: string
  onChange: (html: string, hasText: boolean) => void
  /** Huỷ / Lưu, at the ribbon's end: it stays in view however long the minutes are. */
  actions: ReactNode
}) {
  const [extensions] = useState(minutesExtensions)
  const [initial] = useState(content)
  const [finding, setFinding] = useState(false)
  const editor = useEditor({
    extensions,
    content: initial,
    editorProps: {
      attributes: {
        class: 'minutes-paper minutes-doc minutes-editor-doc',
        role: 'textbox',
        'aria-multiline': 'true',
        'aria-label': 'Nội dung biên bản',
        spellcheck: 'false',
      },
      handleKeyDown: (_view, event) => {
        // Ctrl+F finds in the minutes, not in the whole web page.
        if ((event.ctrlKey || event.metaKey) && !event.altKey && event.key.toLowerCase() === 'f') {
          event.preventDefault()
          setFinding(true)
          return true
        }
        return false
      },
    },
    onCreate: ({ editor: e }) => fitTables(e),
    onUpdate: ({ editor: e }) => onChange(e.getHTML(), !!e.state.doc.textContent.trim()),
  })

  return (
    <div className="minutes-editor-frame">
      <Toolbar editor={editor} onFind={() => setFinding(true)} actions={actions} />
      {finding && <FindBar editor={editor} onClose={() => setFinding(false)} />}
      <div className="minutes-editor-canvas">
        <EditorContent editor={editor} />
      </div>
    </div>
  )
}

import { useEditorState, type Editor } from '@tiptap/react'
import { useEffect, useRef, useState } from 'react'
import { Icon } from '../../../components/Icon'
import { findState, replaceAll, replaceCurrent, setQuery, step } from './schema'

/**
 * Find and replace over the sheet (Ctrl+F), as Word's: every match is marked on the page, the current one stronger;
 * Enter goes to the next, Shift+Enter back, Escape closes and returns to the text.
 */
export function FindBar({ editor, onClose }: { editor: Editor; onClose: () => void }) {
  const [query, setText] = useState(() => findState(editor.state).query)
  const [replacement, setReplacement] = useState('')
  const input = useRef<HTMLInputElement>(null)
  const { count, current } = useEditorState({
    editor,
    selector: ({ editor: e }) => {
      const f = findState(e.state)
      return { count: f.matches.length, current: f.current }
    },
  })

  useEffect(() => {
    input.current?.focus()
    input.current?.select()
    // Closing clears the marks: nothing stays highlighted on a sheet nobody is searching.
    return () => {
      if (!editor.isDestroyed) setQuery(editor, '')
    }
  }, [editor])

  const close = () => {
    onClose()
    editor.commands.focus()
  }

  return (
    <div className="minutes-find" role="search" aria-label="Tìm và thay thế">
      <div className="minutes-find-row">
        <input
          ref={input}
          className="minutes-find-field"
          type="search"
          aria-label="Tìm"
          placeholder="Tìm"
          value={query}
          onChange={(event) => {
            setText(event.target.value)
            setQuery(editor, event.target.value)
          }}
          onKeyDown={(event) => {
            if (event.key === 'Enter') {
              event.preventDefault()
              step(editor, event.shiftKey ? -1 : 1)
            } else if (event.key === 'Escape') {
              event.preventDefault()
              close()
            }
          }}
        />
        <span className="minutes-find-count" aria-live="polite">
          {query ? (count ? `${current + 1}/${count}` : 'Không thấy') : ''}
        </span>
        <button type="button" className="icon-button icon-button-sm" aria-label="Kết quả trước (Shift+Enter)" data-tooltip="Kết quả trước" disabled={!count} onClick={() => step(editor, -1)}>
          <Icon name="chevronUp" size={16} />
        </button>
        <button type="button" className="icon-button icon-button-sm" aria-label="Kết quả sau (Enter)" data-tooltip="Kết quả sau" disabled={!count} onClick={() => step(editor, 1)}>
          <Icon name="chevronDown" size={16} />
        </button>
      </div>
      <div className="minutes-find-row">
        <input
          className="minutes-find-field"
          type="text"
          aria-label="Thay bằng"
          placeholder="Thay bằng"
          value={replacement}
          onChange={(event) => setReplacement(event.target.value)}
          onKeyDown={(event) => {
            if (event.key === 'Enter') {
              event.preventDefault()
              replaceCurrent(editor, replacement)
            } else if (event.key === 'Escape') {
              event.preventDefault()
              close()
            }
          }}
        />
        <button type="button" className="icon-button icon-button-sm" aria-label="Thay" data-tooltip="Thay" disabled={!count} onClick={() => replaceCurrent(editor, replacement)}>
          <Icon name="replace" size={16} />
        </button>
        <button type="button" className="icon-button icon-button-sm" aria-label="Thay tất cả" data-tooltip="Thay tất cả" disabled={!count} onClick={() => replaceAll(editor, replacement)}>
          <Icon name="replaceAll" size={16} />
        </button>
        <button type="button" className="icon-button icon-button-sm" aria-label="Đóng tìm kiếm (Esc)" data-tooltip="Đóng" onClick={close}>
          <Icon name="x" size={16} />
        </button>
      </div>
    </div>
  )
}

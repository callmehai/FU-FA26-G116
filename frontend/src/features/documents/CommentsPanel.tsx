import { useCallback, useEffect, useRef, useState } from 'react'
import { getDocumentComments, postDocumentComment } from '../../api/documents'
import { Badge } from '../../components/Badge'
import { Icon } from '../../components/Icon'
import { errorMessage, isApiError } from '../../lib/errors'
import { formatDateTime } from '../../lib/format'
import type { DocumentComment, DocumentCommentList } from '../../types/document'
import { useAccessLost } from '../auth/useAccessLost'

/** The server's limit (MSG348). */
const MAX_LENGTH = 4000

/**
 * UC-35 Comment on Meeting Document (SCRUM-23), beside the viewer. Anyone who can read the meeting reads and
 * writes here (BR-249, BR-275); once the meeting has ended or was cancelled the list stays and the box goes
 * (BR-282). Replies are one level (BR-317). A PDF comment may carry the page being read (AF-1, BR-248).
 *
 * Posting does not reach the hub, so the list is read again after each comment and on "Làm mới".
 */
export function CommentsPanel({
  meetingId,
  meetingDocumentId,
  pdf,
  currentPage,
  closedNote,
  onCount,
  onGone,
  onClose,
}: {
  meetingId: string
  meetingDocumentId: string
  pdf: boolean
  /** Why the box is gone, when `canComment` is false. */
  closedNote: string
  /** The page in view, for "Gắn trang". */
  currentPage: number | null
  onCount: (count: number) => void
  /** MSG156: the document was removed from the meeting, or the meeting can no longer be read. */
  onGone: () => void
  onClose: () => void
}) {
  const handleAccessLost = useAccessLost()
  const [list, setList] = useState<DocumentCommentList | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [attempt, setAttempt] = useState(0)
  const [replyTo, setReplyTo] = useState<DocumentComment | null>(null)
  const onCountRef = useRef(onCount)
  const onGoneRef = useRef(onGone)

  useEffect(() => {
    onCountRef.current = onCount
    onGoneRef.current = onGone
  })

  useEffect(() => {
    let live = true
    getDocumentComments(meetingId, meetingDocumentId)
      .then((data) => {
        if (!live) return
        setLoadError(null)
        setList(data)
        onCountRef.current(data.comments.reduce((n, c) => n + 1 + c.replies.length, 0))
      })
      .catch((err) => {
        if (!live || handleAccessLost(err)) return
        if (isApiError(err, 404, 'MSG156')) onGoneRef.current()
        else setLoadError(errorMessage(err))
      })
    return () => {
      live = false
    }
  }, [meetingId, meetingDocumentId, attempt, handleAccessLost])

  const reload = useCallback(() => setAttempt((n) => n + 1), [])

  return (
    <aside className="viewer-comments" aria-label="Góp ý">
      <div className="viewer-comments-head">
        <h2>Góp ý</h2>
        <button
          type="button"
          className="icon-button icon-button-sm"
          aria-label="Làm mới góp ý"
          data-tooltip="Làm mới"
          onClick={reload}
        >
          <Icon name="refresh" size={16} />
        </button>
        <button
          type="button"
          className="icon-button icon-button-sm"
          aria-label="Đóng góp ý"
          data-tooltip="Đóng"
          onClick={onClose}
        >
          <Icon name="x" size={16} />
        </button>
      </div>

      <div className="viewer-comments-list" aria-busy={list === null && !loadError}>
        {loadError ? (
          <div className="alert alert-error" role="alert">
            <p>{loadError}</p>
            <button
              type="button"
              className="btn btn-outline btn-sm"
              onClick={() => {
                setLoadError(null)
                reload()
              }}
            >
              <Icon name="refresh" size={16} />
              Thử lại
            </button>
          </div>
        ) : !list ? (
          <span className="skeleton documents-skeleton" />
        ) : list.comments.length === 0 ? (
          <p className="documents-empty">Chưa có góp ý nào.</p>
        ) : (
          <ul className="comment-threads">
            {list.comments.map((c) => (
              <li key={c.id} className="comment-thread">
                <Comment comment={c} />
                {c.replies.length > 0 && (
                  <ul className="comment-replies">
                    {c.replies.map((r) => (
                      <li key={r.id}>
                        <Comment comment={r} />
                      </li>
                    ))}
                  </ul>
                )}
                {list.canComment && (
                  <button type="button" className="btn btn-ghost btn-sm comment-reply" onClick={() => setReplyTo(c)}>
                    <Icon name="reply" size={14} />
                    Trả lời
                  </button>
                )}
              </li>
            ))}
          </ul>
        )}
      </div>

      {list &&
        (list.canComment ? (
          <Composer
            key={replyTo?.id ?? 'new'}
            meetingId={meetingId}
            meetingDocumentId={meetingDocumentId}
            pdf={pdf}
            currentPage={currentPage}
            replyTo={replyTo}
            onCancelReply={() => setReplyTo(null)}
            onPosted={() => {
              setReplyTo(null)
              reload()
            }}
            onClosed={reload}
            onGone={() => onGoneRef.current()}
          />
        ) : (
          <p className="viewer-comments-closed">{closedNote}</p>
        ))}
    </aside>
  )
}

function Comment({ comment }: { comment: DocumentComment }) {
  return (
    <article className="comment">
      <header className="comment-head">
        <strong>{comment.author.name}</strong>
        {comment.author.isGuest && <Badge tone="outline">Khách mời</Badge>}
        {comment.pageNumber !== null && <span className="comment-page">Trang {comment.pageNumber}</span>}
        <time dateTime={comment.createdAt}>{formatDateTime(comment.createdAt)}</time>
      </header>
      <p className="comment-body">{comment.body}</p>
    </article>
  )
}

function Composer({
  meetingId,
  meetingDocumentId,
  pdf,
  currentPage,
  replyTo,
  onCancelReply,
  onPosted,
  onClosed,
  onGone,
}: {
  meetingId: string
  meetingDocumentId: string
  pdf: boolean
  currentPage: number | null
  replyTo: DocumentComment | null
  onCancelReply: () => void
  onPosted: () => void
  /** MSG251/MSG349: the meeting ended or was cancelled while writing; the text stays to copy. */
  onClosed: () => void
  onGone: () => void
}) {
  const handleAccessLost = useAccessLost()
  const [body, setBody] = useState('')
  const [withPage, setWithPage] = useState(true)
  const [pending, setPending] = useState(false)
  const [failure, setFailure] = useState<string | null>(null)
  // A reply carries no page of its own; a DOCX has none (BR-248).
  const page = pdf && !replyTo && currentPage !== null ? currentPage : null
  const blank = body.trim() === ''

  async function send() {
    if (blank || pending) return
    setPending(true)
    setFailure(null)
    try {
      await postDocumentComment(meetingId, meetingDocumentId, {
        body: body.trim(),
        pageNumber: page !== null && withPage ? page : null,
        parentCommentId: replyTo?.id ?? null,
      })
      setBody('')
      onPosted()
    } catch (err) {
      if (handleAccessLost(err)) return
      if (isApiError(err, 404, 'MSG156')) return onGone()
      setFailure(errorMessage(err))
      if (isApiError(err, 409, 'MSG251') || isApiError(err, 409, 'MSG349')) onClosed()
    } finally {
      setPending(false)
    }
  }

  return (
    <form
      className="comment-composer"
      onSubmit={(event) => {
        event.preventDefault()
        void send()
      }}
    >
      {replyTo && (
        <p className="comment-replying">
          Trả lời <strong>{replyTo.author.name}</strong>
          <button
            type="button"
            className="icon-button icon-button-sm"
            aria-label="Bỏ trả lời"
            data-tooltip="Bỏ trả lời"
            onClick={onCancelReply}
          >
            <Icon name="x" size={14} />
          </button>
        </p>
      )}
      <textarea
        value={body}
        maxLength={MAX_LENGTH}
        rows={3}
        placeholder={replyTo ? 'Viết câu trả lời…' : 'Viết góp ý…'}
        aria-label={replyTo ? `Trả lời ${replyTo.author.name}` : 'Nội dung góp ý'}
        onChange={(event) => setBody(event.target.value)}
        onKeyDown={(event) => {
          // Ctrl/⌘ + Enter sends, as in most comment boxes; Enter alone keeps a new line.
          if (event.key === 'Enter' && (event.metaKey || event.ctrlKey)) void send()
        }}
        autoFocus={!!replyTo}
      />
      {failure && (
        <p className="field-error" role="alert">
          {failure}
        </p>
      )}
      <div className="comment-composer-foot">
        {page !== null && (
          <label className="comment-page-toggle">
            <input type="checkbox" checked={withPage} onChange={(e) => setWithPage(e.target.checked)} />
            Gắn trang {page}
          </label>
        )}
        <span className="field-counter">
          {body.length}/{MAX_LENGTH}
        </span>
        <button type="submit" className="btn btn-primary btn-sm" disabled={blank || pending}>
          {pending ? 'Đang gửi…' : replyTo ? 'Gửi trả lời' : 'Gửi góp ý'}
        </button>
      </div>
    </form>
  )
}

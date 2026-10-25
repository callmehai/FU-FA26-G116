import { useEffect, useLayoutEffect, useRef, useState } from 'react'
import type { PDFDocumentLoadingTask, PDFPageProxy } from 'pdfjs-dist/legacy/build/pdf.mjs'
import { loadPdfJs } from './pdf'

/** The widest a page gets at 100%, the width of a comfortable line of text. */
const MAX_WIDTH = 880
/** Gutter left and right of the page inside the stage. */
const GUTTER = 24
/** MSG158's wording: the file arrived but cannot be drawn. */
const CANNOT_DISPLAY = 'Không hiển thị được tài liệu. Vui lòng thử lại.'

/** The page width at 100%: the stage's width minus the gutters, capped. Follows the window. */
function useFitWidth() {
  const ref = useRef<HTMLDivElement>(null)
  const [width, setWidth] = useState(0)
  useLayoutEffect(() => {
    const el = ref.current
    if (!el) return
    const observer = new ResizeObserver(() => setWidth(Math.min(MAX_WIDTH, el.clientWidth - 2 * GUTTER)))
    observer.observe(el)
    return () => observer.disconnect()
  }, [])
  return [ref, width] as const
}

/**
 * A PDF, one canvas per page, each drawn only when it comes near the screen (a 200-page file must
 * not draw 200 canvases up front). Reports which page holds the middle of the screen.
 */
export function PdfView({
  blob,
  zoom,
  onPage,
}: {
  blob: Blob
  zoom: number
  onPage: (page: { current: number; total: number }) => void
}) {
  const [ref, fit] = useFitWidth()
  const [pages, setPages] = useState<PDFPageProxy[] | null>(null)
  const [failed, setFailed] = useState(false)

  useEffect(() => {
    // Destroying the loading task frees the document and its worker, loaded or not.
    const open: { task?: PDFDocumentLoadingTask } = {}
    let cancelled = false
    void (async () => {
      try {
        const pdfjs = await loadPdfJs()
        const data = new Uint8Array(await blob.arrayBuffer())
        // Left while pdf.js was still loading: create no worker the cleanup has already missed.
        if (cancelled) return
        open.task = pdfjs.getDocument({ data })
        const doc = await open.task.promise
        // ponytail: every page proxy is fetched up front for its size; fine for meeting papers,
        // switch to per-page sizes on demand if 1000-page files show up.
        const all = await Promise.all(Array.from({ length: doc.numPages }, (_, i) => doc.getPage(i + 1)))
        if (!cancelled) setPages(all)
      } catch {
        if (!cancelled) setFailed(true)
      }
    })()
    return () => {
      cancelled = true
      void open.task?.destroy()
    }
  }, [blob])

  const total = pages?.length ?? 0
  useEffect(() => {
    const stage = ref.current
    if (!stage || total === 0) return
    const report = () => {
      const middle = stage.getBoundingClientRect().top + stage.clientHeight / 2
      const sheets = stage.querySelectorAll<HTMLElement>('.pdf-page')
      let current = 1
      sheets.forEach((sheet, i) => {
        if (sheet.getBoundingClientRect().top <= middle) current = i + 1
      })
      onPage({ current, total })
    }
    report()
    stage.addEventListener('scroll', report, { passive: true })
    return () => stage.removeEventListener('scroll', report)
  }, [ref, total, onPage, zoom, fit])

  return (
    <div className="viewer-scroll" ref={ref}>
      {failed ? (
        <div className="viewer-message">
          <p className="alert alert-error">{CANNOT_DISPLAY}</p>
        </div>
      ) : (
        fit > 0 &&
        pages?.map((page, i) => (
          <PdfPage key={i} page={page} width={fit * zoom} label={`Trang ${i + 1} / ${pages.length}`} />
        ))
      )}
    </div>
  )
}

function PdfPage({ page, width, label }: { page: PDFPageProxy; width: number; label: string }) {
  const canvas = useRef<HTMLCanvasElement>(null)
  const [near, setNear] = useState(false)
  const base = page.getViewport({ scale: 1 })
  const height = (width / base.width) * base.height

  useEffect(() => {
    const el = canvas.current
    if (!el) return
    const observer = new IntersectionObserver(([entry]) => setNear(entry.isIntersecting), { rootMargin: '800px 0px' })
    observer.observe(el)
    return () => observer.disconnect()
  }, [])

  useEffect(() => {
    const el = canvas.current
    if (!near || !el) return
    // Drawn at the screen's pixel density, so text stays sharp on a retina display.
    const viewport = page.getViewport({ scale: (width / base.width) * window.devicePixelRatio })
    el.width = Math.floor(viewport.width)
    el.height = Math.floor(viewport.height)
    const task = page.render({ canvas: el, viewport })
    task.promise.catch(() => {})
    return () => {
      task.cancel()
      // Free the bitmap of a page scrolled far away: a long PDF on a phone runs out of canvas memory.
      el.width = 0
      el.height = 0
    }
  }, [near, page, width, base.width])

  return (
    <div className="pdf-page" style={{ width, height }}>
      <canvas ref={canvas} style={{ width, height }} aria-label={label} role="img" />
    </div>
  )
}

/** A DOCX drawn as one flowing text — no pages, so no page counter (wireframe 7.5b). */
export function DocxView({ blob, zoom }: { blob: Blob; zoom: number }) {
  const [ref, fit] = useFitWidth()
  const body = useRef<HTMLDivElement>(null)
  const [failed, setFailed] = useState(false)

  useEffect(() => {
    const el = body.current
    if (!el) return
    let cancelled = false
    void (async () => {
      try {
        const { renderAsync } = await import('docx-preview')
        // Drawn off-screen and made safe before it joins the page.
        const target = document.createElement('div')
        await renderAsync(blob, target, undefined, {
          className: 'docx',
          inWrapper: false,
          breakPages: false,
          ignoreLastRenderedPageBreak: true,
          // Word's fixed page width would overflow a phone; the stage sets the width instead.
          ignoreWidth: true,
          ignoreHeight: true,
          // An altChunk is HTML embedded in the file, drawn as a same-origin iframe: it could run script.
          renderAltChunks: false,
        })
        if (cancelled) return
        neutralizeLinks(target)
        el.replaceChildren(target)
      } catch {
        if (!cancelled) setFailed(true)
      }
    })()
    return () => {
      cancelled = true
      el.replaceChildren()
    }
  }, [blob])

  return (
    <div className="viewer-scroll" ref={ref}>
      {failed && (
        <div className="viewer-message">
          <p className="alert alert-error">{CANNOT_DISPLAY}</p>
        </div>
      )}
      {/* CSS zoom scales the text with the sheet, as a word processor's zoom does. */}
      <div className="docx-sheet" style={{ width: fit > 0 ? fit : undefined, zoom }} hidden={failed}>
        <div ref={body} />
      </div>
    </div>
  )
}

const SAFE_PROTOCOLS = new Set(['http:', 'https:', 'mailto:'])

/**
 * docx-preview copies a hyperlink's target into `href` as written in the file, so an uploaded DOCX
 * could carry `javascript:` and run in our origin, where the access token lives. Keep web, mail and
 * in-document links only; the outside ones open in a new tab, away from the viewer.
 */
function neutralizeLinks(root: HTMLElement) {
  root.querySelectorAll('iframe, object, embed, script').forEach((el) => el.remove())
  root.querySelectorAll<HTMLAnchorElement>('a[href]').forEach((a) => {
    const href = a.getAttribute('href') ?? ''
    if (href.startsWith('#')) return
    let protocol = ''
    try {
      protocol = new URL(href, window.location.href).protocol
    } catch {
      // Not a URL at all: drop it.
    }
    if (!SAFE_PROTOCOLS.has(protocol)) return a.removeAttribute('href')
    a.target = '_blank'
    a.rel = 'noopener noreferrer'
  })
}

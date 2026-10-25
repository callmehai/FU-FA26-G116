import workerUrl from 'pdfjs-dist/legacy/build/pdf.worker.min.mjs?url'

/**
 * pdf.js, loaded on first use so the meeting pages do not carry it. The legacy build polyfills what
 * older Safari lacks; the worker is a file of our own origin, which the test server's CSP allows
 * (`worker-src 'self'`), and pdf.js never evaluates code from the document.
 */
export async function loadPdfJs() {
  const pdfjs = await import('pdfjs-dist/legacy/build/pdf.mjs')
  pdfjs.GlobalWorkerOptions.workerSrc = workerUrl
  return pdfjs
}

/** The page count the upload sends with a PDF (the server only stores it); null when unreadable. */
export async function countPdfPages(file: File): Promise<number | null> {
  try {
    const pdfjs = await loadPdfJs()
    const task = pdfjs.getDocument({ data: new Uint8Array(await file.arrayBuffer()) })
    try {
      return (await task.promise).numPages
    } finally {
      void task.destroy()
    }
  } catch {
    return null
  }
}

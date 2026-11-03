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

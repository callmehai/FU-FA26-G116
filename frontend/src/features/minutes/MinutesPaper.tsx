import { useMemo } from 'react'
import { SINGLE_LINE } from './editor/schema'

/**
 * The minutes as a sheet of paper (SCRUM-32, ADR-0055). The content is HTML the server has already cut down to the
 * minutes' allow-list on every write (MinutesHtml.cs): blocks, marks, tables and a few style properties, no script,
 * no link, no image, no event attribute. That allow-list is what makes setting it as HTML safe here, and the page's
 * Content-Security-Policy would refuse an inline script even if one got through.
 */
export function MinutesPaper({ content }: { content: string }) {
  const html = useMemo(() => fitTables(content), [content])
  return <article className="minutes-paper minutes-doc" dangerouslySetInnerHTML={{ __html: html }} />
}

/**
 * Column widths are stored in pixels of the editor's sheet. As shares of the table they fit any width of page, a
 * phone's included, the way Word and the PDF spread them over the text width.
 */
function fitTables(content: string): string {
  if (!content.includes('<col') && !content.includes('line-height')) return content
  const doc = new DOMParser().parseFromString(`<body>${content}</body>`, 'text/html')
  // `line-height` is Word's "multiple" spacing; one Word line is about 1.15 em of Times New Roman.
  for (const block of doc.querySelectorAll<HTMLElement>('[style*="line-height"]')) {
    const value = parseFloat(block.style.lineHeight)
    if (value > 0) block.style.lineHeight = (value * SINGLE_LINE).toFixed(3)
  }
  for (const table of doc.querySelectorAll('table')) {
    const cols = [...table.querySelectorAll(':scope > colgroup > col')] as HTMLTableColElement[]
    const widths = cols.map((col) => parseFloat(col.style.width) || 0)
    const known = widths.filter((w) => w > 0)
    const share = known.length ? known.reduce((a, b) => a + b, 0) / known.length : 1
    const total = widths.reduce((sum, w) => sum + (w || share), 0)
    cols.forEach((col, i) => {
      col.removeAttribute('style')
      col.style.width = `${(((widths[i] || share) / total) * 100).toFixed(2)}%`
    })
    table.style.removeProperty('width')
    table.style.removeProperty('min-width')
  }
  return doc.body.innerHTML
}

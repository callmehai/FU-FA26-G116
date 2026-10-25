import { Icon } from '../../components/Icon'

/** The file-type tag in front of a name: red PDF, blue DOCX, like the library and AI-eCabinet. */
export function FileTypeTag({ pdf }: { pdf: boolean }) {
  return <span className={pdf ? 'file-tag file-tag-pdf' : 'file-tag file-tag-docx'}>{pdf ? 'PDF' : 'DOCX'}</span>
}

/** BR-188: a Secret meeting's documents are read in the browser only. */
export function OnlineOnly() {
  return (
    <span className="online-only" data-tooltip="Cuộc họp Mật: tài liệu chỉ xem trực tuyến, không tải về">
      <Icon name="lock" size={14} />
      Chỉ xem trực tuyến
    </span>
  )
}

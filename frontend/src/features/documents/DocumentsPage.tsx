import { Icon } from '../../components/Icon'
import { PageHeader } from '../../components/PageHeader'

/**
 * Each user's own document library (ADR-0006, ADR-0018). Nothing reads or writes documents yet, so the page
 * says what it will hold and what it waits on instead of showing upload buttons that cannot work.
 */
export function DocumentsPage() {
  return (
    <>
      <PageHeader
        title="Kho tài liệu"
        description="Tài liệu riêng của bạn — chỉ bạn thấy và quản lý được những gì ở đây."
      />

      <div className="card placeholder-card">
        <span className="placeholder-icon">
          <Icon name="folder" size={28} />
        </span>
        <p className="placeholder-title">Kho tài liệu chưa hoạt động</p>
        <p>
          Bạn sẽ tải tài liệu lên, xếp vào thư mục, rồi gắn vào cuộc họp mà mình làm thư ký hoặc chủ toạ. Tài liệu chưa
          gắn cuộc họp nào chỉ mình bạn đọc được.
        </p>
        <p className="placeholder-blocked">
          Sẽ hiện thực khi backend có <code>GET /api/documents</code>.
        </p>
      </div>
    </>
  )
}

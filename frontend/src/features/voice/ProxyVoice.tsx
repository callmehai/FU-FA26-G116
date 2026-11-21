import { useState } from "react";
import { Link } from "react-router-dom";
import { deleteVoiceSample } from "../../api/voice";
import { ConfirmDialog } from "../../components/ConfirmDialog";
import { Icon } from "../../components/Icon";
import { useToast } from "../../components/toastContext";
import { errorMessage } from "../../lib/errors";
import { formatDate, formatDateTime } from "../../lib/format";
import { routes } from "../../lib/routes";
import type { VoiceProfile } from "../../types/voice";
import { formatDuration } from "../recordings/format";
import { useSamplePlayer } from "./samplePlayer";

/**
 * ADR-0060 — the samples meetings' Secretaries recorded for the person on the room's mic. A second set under the same
 * one Voice ID consent: the Secretary records, the person decides. Everything taken is listed with who took it and
 * where, can be heard back and deleted; turning Voice ID off erases this set with the other.
 */
export function ProxyTab({
  profile,
  onChange,
  onReload,
}: {
  profile: VoiceProfile;
  onChange: (profile: VoiceProfile) => void;
  onReload: () => Promise<void>;
}) {
  const toast = useToast();
  const proxy = profile.proxy;
  const player = useSamplePlayer();
  // UC-81e step 2: a sample is erased for good, so the person confirms first.
  const [erasing, setErasing] = useState<string | null>(null);

  const remove = async (sampleId: string) => {
    player.stop();
    try {
      const result = await deleteVoiceSample(sampleId);
      toast.show(result.message, "success");
      onChange(result.data);
    } catch (error) {
      // The dialog shows why; the list is read again in case the sample is already gone.
      await onReload();
      throw error;
    }
  };

  return (
    <section className="card voice-card" aria-labelledby="voice-proxy-title">
      <div className="voice-status">
        <h2 id="voice-proxy-title" className="card-title">
          Thư ký thu hộ giọng tôi
        </h2>
        <span className="voice-count">
          {proxy.samples.length}/{proxy.maxSamples}
        </span>
      </div>
      {!proxy.allowed ? (
        <p className="alert alert-info">
          Đồng ý điều khoản mới ở trên để Thư ký cuộc họp thu giọng mẫu hộ bạn.
        </p>
      ) : (
        proxy.samples.length === 0 && (
          <p className="alert alert-info">
            Chưa có mẫu nào. Thư ký thu ở cuộc họp trực tiếp hoặc kết hợp bạn dự.
          </p>
        )
      )}
      {proxy.samples.length > 0 && (
        <ol className="voice-samples">
          {proxy.samples.map((sample, index) => (
            <li key={sample.id} className="voice-sample voice-sample-stacked">
              <span className="voice-sample-text">
                <span className="voice-sample-name">
                  <Link to={routes.meeting(sample.meetingId)}>{sample.meetingTitle}</Link>
                </span>
                <span
                  className="field-hint"
                  data-tooltip={`${formatDuration(sample.speechMs)} giọng nói, thu lúc ${formatDateTime(sample.createdAt)}`}
                  tabIndex={0}
                >
                  {sample.collectedByName} · {formatDate(sample.createdAt)}
                </span>
              </span>
              {sample.playable && (
                <button
                  type="button"
                  className="btn btn-ghost btn-icon btn-sm"
                  onClick={() =>
                    void player
                      .toggle(sample.id)
                      .catch((error) => toast.show(errorMessage(error), "error"))
                  }
                  aria-label={`${player.playing === sample.id ? "Dừng" : "Nghe lại"} mẫu thu hộ ${index + 1}`}
                  aria-pressed={player.playing === sample.id}
                  data-tooltip={player.playing === sample.id ? "Dừng" : "Nghe lại"}
                >
                  <Icon name={player.playing === sample.id ? "pause" : "play"} size={16} />
                </button>
              )}
              <button
                type="button"
                className="btn btn-ghost btn-icon btn-sm"
                onClick={() => setErasing(sample.id)}
                aria-label={`Xoá mẫu thu hộ ${index + 1}`}
                data-tooltip="Xoá mẫu"
              >
                <Icon name="trash" size={16} />
              </button>
            </li>
          ))}
        </ol>
      )}

      {erasing && (
        <ConfirmDialog
          title="Xoá mẫu thu hộ?"
          confirmLabel="Xoá"
          pendingLabel="Đang xoá…"
          cancelLabel="Giữ lại"
          tone="danger"
          onClose={() => setErasing(null)}
          onConfirm={() => remove(erasing)}
        >
          <p>Mẫu này bị xoá hẳn, không lấy lại được.</p>
        </ConfirmDialog>
      )}
    </section>
  );
}

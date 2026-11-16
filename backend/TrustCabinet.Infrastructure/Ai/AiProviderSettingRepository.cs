using Microsoft.EntityFrameworkCore;
using TrustCabinet.Application.Ai;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Ai;

public sealed class AiProviderSettingRepository(TrustCabinetDbContext db) : IAiProviderSettingRepository
{
    public Task<AiProviderSetting?> FindAsync(Guid id, bool tracked, CancellationToken ct)
        => tracked
            ? db.AiProviderSettings.FirstOrDefaultAsync(s => s.Id == id, ct)
            : db.AiProviderSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<AiProviderSetting?> FindCurrentAsync(CancellationToken ct)
        => db.AiProviderSettings.AsNoTracking().FirstOrDefaultAsync(ct);

    public Task<AiSettingsRow?> FindViewAsync(Guid id, CancellationToken ct)
        => db.AiProviderSettings.AsNoTracking().Where(s => s.Id == id)
            .Select(s => new AiSettingsRow(s.Mode, s.ApiBaseUrl, s.ApiModel, s.ApiKeyCiphertext != null, s.ApiKeyHint, s.UpdatedAt,
                s.UpdatedBy != null ? s.UpdatedBy.FullName : null))
            .FirstOrDefaultAsync(ct);

    public void Add(AiProviderSetting setting) => db.AiProviderSettings.Add(setting);
}

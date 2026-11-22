using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Access;

/// <summary>
/// The human-readable description of every <see cref="Capability"/> — the diagram's
/// <c>getPermissionCatalog()</c>.
///
/// Read from the enum rather than from a table, because the enum is what code checks against: a
/// capability exists exactly when some endpoint asks for it, so one an administrator could add
/// would grant nothing and one they could delete would still be enforced. SCRUM-60 says it
/// outright — the screen has no "create permission" button.
///
/// Wording lives here rather than in the frontend so a capability added to the enum cannot ship
/// without a label, and so the tester reads the same words as the administrator.
/// </summary>
public static class PermissionCatalog
{
    private static readonly Dictionary<Capability, (string Name, string Description, string Group)> Descriptions =
        new()
        {
            [Capability.MeetingCreator] = (
                "Tạo cuộc họp",
                "Tạo cuộc họp mới và gửi lời mời cho người tham dự.",
                "Cuộc họp"),
            [Capability.MeetingApprover] = (
                "Phê duyệt cuộc họp",
                "Phê duyệt hoặc từ chối cuộc họp do người khác tạo.",
                "Cuộc họp"),
            [Capability.VoiceCollector] = (
                "Thu giọng mẫu hộ",
                "Thu giọng mẫu Voice ID hộ người đã đồng ý (hỏi ý họ mỗi lần thu), nghe lại và xoá mọi mẫu thu hộ của họ.",
                "Quản trị"),
            [Capability.AdminUsers] = (
                "Quản trị người dùng",
                "Quản lý tài khoản người dùng và ma trận phân quyền.",
                "Quản trị"),
        };

    /// <summary>
    /// Every capability the system enforces, grouped: each functional group's capabilities sit together, groups in the
    /// order they first appear in the enum, and within a group in enum order.
    ///
    /// Built from <see cref="Enum.GetValues{TEnum}"/> rather than from the dictionary's keys, so
    /// adding a capability without describing it fails loudly here instead of quietly leaving a
    /// column off the administrator's screen. Grouped because the matrix draws one heading over each run of a group:
    /// in plain enum order <see cref="Capability.VoiceCollector"/>, added after <see cref="Capability.AdminUsers"/>,
    /// split "Cuộc họp" into two headings. The enum itself is not reordered.
    /// </summary>
    public static IReadOnlyCollection<PermissionDescriptor> All { get; } = Grouped();

    private static PermissionDescriptor[] Grouped()
    {
        var all = Enum.GetValues<Capability>().Select(Describe).ToArray();
        var groups = all.Select(p => p.FunctionalGroup).Distinct().ToList();
        // OrderBy is stable, so enum order holds inside each group.
        return [.. all.OrderBy(p => groups.IndexOf(p.FunctionalGroup))];
    }

    public static PermissionDescriptor Describe(Capability capability)
    {
        if (!Descriptions.TryGetValue(capability, out var text))
        {
            throw new InvalidOperationException(
                $"Capability '{capability}' has no entry in PermissionCatalog. Add its Vietnamese "
                + "name and description there — a capability with no label reaches the "
                + "administrator's screen as a blank column.");
        }

        return new PermissionDescriptor(
            capability.ToString(), text.Name, text.Description, text.Group);
    }
}

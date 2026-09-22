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
            [Capability.MinutesEditor] = (
                "Soạn biên bản",
                "Soạn và chỉnh sửa biên bản cuộc họp trước khi ban hành.",
                "Biên bản"),
            [Capability.AdminUsers] = (
                "Quản trị người dùng",
                "Quản lý tài khoản người dùng và ma trận phân quyền.",
                "Quản trị"),
        };

    /// <summary>
    /// Every capability the system enforces, in enum order.
    ///
    /// Built from <see cref="Enum.GetValues{TEnum}"/> rather than from the dictionary's keys, so
    /// adding a capability without describing it fails loudly here instead of quietly leaving a
    /// column off the administrator's screen.
    /// </summary>
    public static IReadOnlyCollection<PermissionDescriptor> All { get; } =
        [.. Enum.GetValues<Capability>().Select(Describe)];

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

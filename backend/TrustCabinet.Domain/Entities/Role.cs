using TrustCabinet.Domain.Entities.Common;

namespace TrustCabinet.Domain.Entities;

/// <summary>
/// A named bundle of capabilities (ADR-0008). Data rather than an enum: business code never
/// compares roles — it asks for a <see cref="Enums.Capability"/> — so a role an administrator
/// creates needs no code to check it, unlike a capability, which only means anything because an
/// endpoint asks for it.
///
/// The four roles the system ships with are rows too, with fixed ids declared in
/// <see cref="Access.BuiltInRoles"/>. They cannot be renamed or deleted; their capabilities can.
/// </summary>
public class Role : Entity
{
    /// <summary>
    /// Display name, unique regardless of case — the column is <c>citext</c>, as
    /// <see cref="User.Email"/> is, so the unique index itself refuses "thư ký" beside "Thư ký".
    /// </summary>
    public required string Name { get; set; }

    public string? Description { get; set; }

    /// <summary>One of the four roles the system ships with: fixed name, never deleted.</summary>
    public bool IsBuiltIn { get; set; }

    /// <summary>The class diagram's <c>isBuiltInRole()</c>.</summary>
    public bool IsBuiltInRole() => IsBuiltIn;
}

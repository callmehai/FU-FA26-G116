using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TrustCabinet.Application.Access;
using TrustCabinet.Application.Agenda;
using TrustCabinet.Application.Attendance;
using TrustCabinet.Application.Audit;
using TrustCabinet.Application.Authentication;
using TrustCabinet.Application.CalendarSharing;
using TrustCabinet.Application.Documents;
using TrustCabinet.Application.GuestAccess;
using TrustCabinet.Application.Meetings;
using TrustCabinet.Application.Notes;
using TrustCabinet.Application.Notifications;
using TrustCabinet.Application.Participants;
using TrustCabinet.Application.Reports;
using TrustCabinet.Application.Users;
using TrustCabinet.Application.Votes;

namespace TrustCabinet.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the use case services. Each module moves its registrations here, into a
    /// <c>private static void Add&lt;Module&gt;(IServiceCollection)</c> of its own and with the lifetime it had,
    /// in the subtask that moves its services (SCRUM-143, rule R10).
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        AddAudit(services);
        AddNotifications(services);
        AddAuthentication(services);
        AddAccess(services);
        AddUsers(services);
        AddMeetings(services);
        AddCalendarSharing(services);
        AddAgenda(services);
        AddParticipants(services);
        AddAttendance(services);
        AddGuestAccess(services);
        AddReports(services);
        AddDocuments(services);
        AddNotes(services);
        AddVotes(services);
        return services;
    }

    /// <summary><see cref="ICallerContext"/> is the web project's to supply, since it reads the HTTP request.</summary>
    private static void AddAudit(IServiceCollection services)
    {
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IAuditLogQueryService, AuditLogQueryService>();
    }

    private static void AddNotifications(IServiceCollection services)
    {
        services.AddScoped<INotificationService, NotificationService>();
    }

    private static void AddAuthentication(IServiceCollection services)
    {
        services.AddScoped<IAuthenticationService, AuthenticationService>();
    }

    private static void AddAccess(IServiceCollection services)
    {
        services.AddScoped<IRolePermissionService, RolePermissionService>();
        services.AddScoped<IRoleService, RoleService>();

        // One registration for the lock that keeps role-matrix writes and user writes from each removing the
        // last administrator at the same time: RolePermissionService, RoleService and UserService guard one
        // invariant between them. See IAdministratorInvariantGuard.
        services.AddScoped<IAdministratorInvariantGuard, AdministratorInvariantGuard>();
    }

    private static void AddUsers(IServiceCollection services)
    {
        services.AddScoped<IUserService, UserService>();
    }

    /// <summary><see cref="MeetingOverdueOptions"/> is bound by the infrastructure project, which reads configuration.</summary>
    private static void AddMeetings(IServiceCollection services)
    {
        services.AddScoped<IMeetingService, MeetingService>();
        services.AddScoped<IMeetingLifecycleService, MeetingLifecycleService>();
        services.AddScoped<IMeetingOverdueService, MeetingOverdueService>();
    }

    private static void AddCalendarSharing(IServiceCollection services)
    {
        services.AddScoped<CalendarShareCloser>();
        services.AddScoped<CalendarBusyReader>();
        services.AddScoped<ICalendarSharingService, CalendarSharingService>();
        services.AddScoped<ICalendarShareStopper, CalendarShareStopper>();
        services.AddScoped<ISharedCalendarService, SharedCalendarService>();
        services.AddScoped<IMeetingSlotService, MeetingSlotService>();
    }

    /// <summary>
    /// <see cref="UnavailableAgendaGuestAccess"/> fails closed until the guest access module replaces it with the
    /// real adapter, which it does with <c>Replace</c>, so it stays a <c>TryAdd</c>.
    /// </summary>
    private static void AddAgenda(IServiceCollection services)
    {
        services.AddScoped<IAgendaService, AgendaService>();
        services.AddScoped<IAgendaPresenterNotifier, AgendaPresenterNotifier>();
        services.AddScoped<IAgendaMeetingEnd, AgendaMeetingEnd>();
        services.TryAddScoped<IAgendaGuestAccess, UnavailableAgendaGuestAccess>();
    }

    /// <summary>
    /// The guest session adapter (CA-09) replaces <see cref="UnavailableParticipantGuestAccess"/> with
    /// <c>Replace</c>, and the authoritative link provider and access publisher do the same for their
    /// null objects, so all three stay a <c>TryAdd</c>.
    /// </summary>
    private static void AddParticipants(IServiceCollection services)
    {
        services.AddScoped<IParticipantService, ParticipantService>();
        services.TryAddScoped<IParticipantGuestAccess, UnavailableParticipantGuestAccess>();
        services.TryAddScoped<IGuestInvitationLinkProvider, UnavailableGuestInvitationLinkProvider>();
        services.TryAddScoped<IMeetingParticipantAccessPublisher, UnavailableMeetingParticipantAccessPublisher>();
    }

    private static void AddAttendance(IServiceCollection services)
    {
        services.AddScoped<IAttendanceService, AttendanceService>();
    }

    private static void AddGuestAccess(IServiceCollection services)
    {
        services.AddScoped<IGuestAccessService, GuestAccessService>();
    }

    private static void AddReports(IServiceCollection services)
    {
        services.AddScoped<IReportService, ReportService>();
    }

    private static void AddDocuments(IServiceCollection services)
    {
        services.AddScoped<IMeetingDocumentService, MeetingDocumentService>();
        services.AddScoped<IMeetingDocumentAttacher, MeetingDocumentAttacher>();
        services.AddScoped<StoredFileWriter>();
        services.AddScoped<IPersonalLibraryService, PersonalLibraryService>();
        services.AddScoped<IDocumentCommentService, DocumentCommentService>();
    }

    private static void AddNotes(IServiceCollection services)
    {
        services.AddScoped<IMeetingNoteService, MeetingNoteService>();
    }

    private static void AddVotes(IServiceCollection services)
    {
        services.AddScoped<IVoteService, VoteService>();
        services.AddScoped<IVoteMeetingEnd, VoteMeetingEnd>();
    }
}

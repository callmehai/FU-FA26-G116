using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TrustCabinet.Application.Access;
using TrustCabinet.Application.Agenda;
using TrustCabinet.Application.Ai;
using TrustCabinet.Application.Attendance;
using TrustCabinet.Application.Audit;
using TrustCabinet.Application.Authentication;
using TrustCabinet.Application.CalendarSharing;
using TrustCabinet.Application.Calls;
using TrustCabinet.Application.Documents;
using TrustCabinet.Application.Email;
using TrustCabinet.Application.GuestAccess;
using TrustCabinet.Application.Meetings;
using TrustCabinet.Application.Notes;
using TrustCabinet.Application.Notifications;
using TrustCabinet.Application.Participants;
using TrustCabinet.Application.Recordings;
using TrustCabinet.Application.Reports;
using TrustCabinet.Application.Speaking;
using TrustCabinet.Application.Summaries;
using TrustCabinet.Application.Transcription;
using TrustCabinet.Application.Users;
using TrustCabinet.Application.VoiceId;
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
        AddCalls(services);
        AddSpeaking(services);
        AddRecordings(services);
        AddTranscription(services);
        AddVoiceId(services);
        AddAi(services);
        AddSummaries(services);
        AddEmail(services);
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

    private static void AddCalls(IServiceCollection services)
    {
        services.AddScoped<ICallRoomService, CallRoomService>();
        // In place of the no-op Participants registers, whichever of the two is added first.
        services.Replace(ServiceDescriptor.Scoped<IMeetingParticipantAccessPublisher, CallRoomAccessPublisher>());
    }

    private static void AddSpeaking(IServiceCollection services)
    {
        services.AddScoped<ISpeakingService, SpeakingService>();
        services.AddScoped<ISpeakingMeetingEnd, SpeakingMeetingEnd>();
    }

    /// <summary>
    /// <see cref="RecordingOptions"/>, the storage and the signers are the infrastructure project's to supply.
    /// <see cref="SpeechClipGate"/> is one for the whole server: it limits the sentences sent to the speech worker.
    /// </summary>
    private static void AddRecordings(IServiceCollection services)
    {
        services.AddScoped<IRecordingService, RecordingService>();
        services.AddScoped<IRoomTakeService, RoomTakeService>();
        services.AddScoped<ICombinedRecordingService, CombinedRecordingService>();
        services.AddSingleton<SpeechClipGate>();
        services.AddScoped<ISpeechClipService, SpeechClipService>();
    }

    /// <summary>The speech worker client and the transcript repository are the infrastructure project's to supply.</summary>
    private static void AddTranscription(IServiceCollection services)
    {
        services.AddScoped<ITranscriptionService, TranscriptionService>();
    }

    /// <summary>
    /// <see cref="VoiceIdOptions"/>, the voiceprint protector and the repository are the infrastructure project's to supply.
    /// </summary>
    private static void AddVoiceId(IServiceCollection services)
    {
        services.AddScoped<IVoiceIdErasure, VoiceIdErasure>();
        services.AddScoped<IVoiceIdService, VoiceIdService>();
        services.AddScoped<IVoiceCollectionService, VoiceCollectionService>();
    }

    /// <summary>The key protector, the API client factory and the repository are the infrastructure project's to supply.</summary>
    private static void AddAi(IServiceCollection services)
    {
        services.AddScoped<ILanguageModelProvider, LanguageModelProvider>();
        services.AddScoped<IAiSettingsService, AiSettingsService>();
    }

    /// <summary>
    /// <see cref="LanguageModelOptions"/>, the in-house model, the minutes renderer, the repositories and the queries are the
    /// infrastructure project's to supply.
    /// </summary>
    private static void AddSummaries(IServiceCollection services)
    {
        services.AddScoped<MeetingTranscriptReader>();
        services.AddScoped<ISummaryService, SummaryService>();
        services.AddScoped<IMinutesService, MinutesService>();
    }

    /// <summary>
    /// <see cref="EmailDeliveryOptions"/>, the transports behind <see cref="IEmailSender"/>, the delivery worker and the
    /// repositories are the infrastructure project's to supply.
    /// </summary>
    private static void AddEmail(IServiceCollection services)
    {
        services.AddScoped<IMeetingInvitationStager, MeetingInvitationEvents>();
        services.AddScoped<MeetingInvitationDispatchProcessor>();
        services.AddScoped<EmailQueueProcessor>();
        services.AddScoped<IEmailQueueProcessor>(provider =>
            provider.GetRequiredService<EmailQueueProcessor>());
        services.AddScoped<IEmailWebhookReceiver, ResendWebhookReceiver>();
    }
}

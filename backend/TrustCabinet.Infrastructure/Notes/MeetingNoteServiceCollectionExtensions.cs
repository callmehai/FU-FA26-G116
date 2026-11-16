using Microsoft.Extensions.DependencyInjection;
using TrustCabinet.Application.Notes;

namespace TrustCabinet.Infrastructure.Notes;

public static class MeetingNoteServiceCollectionExtensions
{
    public static IServiceCollection AddTrustCabinetMeetingNotes(this IServiceCollection services)
    {
        services.AddScoped<IMeetingNoteRepository, MeetingNoteRepository>();
        return services;
    }
}

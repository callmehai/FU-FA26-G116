using Microsoft.Extensions.DependencyInjection;
using TrustCabinet.Application.Votes;

namespace TrustCabinet.Infrastructure.Votes;

public static class VoteServiceCollectionExtensions
{
    public static IServiceCollection AddTrustCabinetVotes(this IServiceCollection services)
    {
        services.AddScoped<IVoteRepository, VoteRepository>();
        return services;
    }
}

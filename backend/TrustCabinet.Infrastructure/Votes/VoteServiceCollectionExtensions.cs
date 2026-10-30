using Microsoft.Extensions.DependencyInjection;
using TrustCabinet.Domain.Votes;

namespace TrustCabinet.Infrastructure.Votes;

public static class VoteServiceCollectionExtensions
{
    public static IServiceCollection AddTrustCabinetVotes(this IServiceCollection services)
    {
        services.AddScoped<VoteRepository>();
        services.AddScoped<IVoteService, VoteService>();
        return services;
    }
}

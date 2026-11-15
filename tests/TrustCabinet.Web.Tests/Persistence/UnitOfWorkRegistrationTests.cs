using Microsoft.Extensions.DependencyInjection;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Infrastructure;
using TrustCabinet.Infrastructure.Persistence;

namespace TrustCabinet.Web.Tests.Persistence;

/// <summary>
/// SCRUM-143 (CA-00): the host hands a use case the unit of work of its own request. Nothing resolves
/// <see cref="IUnitOfWork"/> until the first service moves to Application, so without this the registration
/// could be lost unnoticed.
/// </summary>
public sealed class UnitOfWorkRegistrationTests(TrustCabinetApiFactory factory) : IClassFixture<TrustCabinetApiFactory>
{
    [Fact]
    public void The_host_gives_each_request_one_unit_of_work_over_its_own_context()
    {
        using var first = factory.Services.CreateScope();
        using var second = factory.Services.CreateScope();

        var unitOfWork = first.ServiceProvider.GetRequiredService<IUnitOfWork>();

        Assert.IsType<EfUnitOfWork>(unitOfWork);
        Assert.Same(unitOfWork, first.ServiceProvider.GetRequiredService<IUnitOfWork>());
        Assert.NotSame(unitOfWork, second.ServiceProvider.GetRequiredService<IUnitOfWork>());

        // Discarding through it empties the tracker of the context the repositories of that request get.
        var db = first.ServiceProvider.GetRequiredService<TrustCabinetDbContext>();
        db.Roles.Add(new() { Name = "Staged, never saved", IsBuiltIn = false });
        unitOfWork.DiscardChanges();
        Assert.Empty(db.ChangeTracker.Entries());
    }
}

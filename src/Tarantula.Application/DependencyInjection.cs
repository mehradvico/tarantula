using Microsoft.Extensions.DependencyInjection;
using Tarantula.Application.Abstractions;
using Tarantula.Application.Business;
using Tarantula.Application.Ingest;
using Tarantula.Application.Live;

namespace Tarantula.Application;

public static class DependencyInjection
{
    /// <summary>Registers every use case by hand, so this list is the index of what the system can do.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ICommandHandler<IngestEventsCommand, IngestEventsResult>, IngestEventsHandler>();
        services.AddScoped<IQueryHandler<GetLiveSnapshotQuery, LiveSnapshot>, GetLiveSnapshotHandler>();
        services.AddScoped<IQueryHandler<GetBusinessOverviewQuery, BusinessOverview>, GetBusinessOverviewHandler>();
        services.AddScoped<IQueryHandler<GetUsersReportQuery, UsersReport>, GetUsersReportHandler>();
        services.AddScoped<IQueryHandler<GetVendorsReportQuery, VendorsReport>, GetVendorsReportHandler>();
        return services;
    }
}

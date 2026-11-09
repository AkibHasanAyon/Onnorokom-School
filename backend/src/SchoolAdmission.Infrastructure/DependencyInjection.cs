using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchoolAdmission.Application.Abstractions.Persistence;
using SchoolAdmission.Application.Abstractions.ReadModels;
using SchoolAdmission.Infrastructure.Persistence;
using SchoolAdmission.Infrastructure.ReadStores;
using SchoolAdmission.Infrastructure.Repositories;

namespace SchoolAdmission.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Read lazily so test hosts can supply the connection string after Program's registrations.
        // Do NOT call EnableRetryOnFailure(): it conflicts with the explicit transactions used for row locks.
        services.AddDbContext<AdmissionDbContext>(o => o.UseNpgsql(
            configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings__Default is not set.")));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IClassRepository, ClassRepository>();
        services.AddScoped<IHoldRepository, HoldRepository>();
        services.AddScoped<IClassReadStore, ClassReadStore>();
        services.AddScoped<IHoldReadStore, HoldReadStore>();
        services.AddScoped<IDatabaseHealthCheck, DatabaseHealthCheck>();

        return services;
    }
}

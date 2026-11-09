using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchoolAdmission.Infrastructure.Persistence;

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

        return services;
    }
}

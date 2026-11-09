using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchoolAdmission.Application.Behaviors;
using SchoolAdmission.Application.Common.Options;

namespace SchoolAdmission.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
            // Order matters: log → validate (400) → transaction (commands only) → handler.
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(TransactionBehavior<,>));
        });
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);

        services.AddOptions<HoldOptions>()
            .Configure<IConfiguration>((o, c) =>
            {
                o.HoldDurationSeconds = c.GetValue("HOLD_DURATION_SECONDS", HoldOptions.DefaultHoldDurationSeconds);
                o.SweepIntervalSeconds = c.GetValue("HOLD_SWEEP_INTERVAL_SECONDS", HoldOptions.DefaultSweepIntervalSeconds);
                o.SweeperEnabled = c.GetValue("HOLD_SWEEPER_ENABLED", true);
            })
            .Validate(o => o.HoldDurationSeconds is >= 1 and <= 86_400, "HOLD_DURATION_SECONDS must be 1..86400")
            .Validate(o => o.SweepIntervalSeconds is >= 1 and <= 300, "HOLD_SWEEP_INTERVAL_SECONDS must be 1..300")
            .ValidateOnStart();

        return services;
    }
}

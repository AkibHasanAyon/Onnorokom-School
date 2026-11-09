namespace SchoolAdmission.Application.Common.Options;

/// <summary>Hold timing, bound from env vars HOLD_DURATION_SECONDS, HOLD_SWEEP_INTERVAL_SECONDS, HOLD_SWEEPER_ENABLED.</summary>
public sealed class HoldOptions
{
    public const int DefaultHoldDurationSeconds = 600;
    public const int DefaultSweepIntervalSeconds = 5;

    public int HoldDurationSeconds { get; set; } = DefaultHoldDurationSeconds;
    public int SweepIntervalSeconds { get; set; } = DefaultSweepIntervalSeconds;
    public bool SweeperEnabled { get; set; } = true;
}

namespace SchoolAdmission.Application.Holds;

public sealed class HoldSettings
{
    public HoldSettings(int durationSeconds = 600)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(durationSeconds);
        DurationSeconds = durationSeconds;
    }

    public int DurationSeconds { get; }
}

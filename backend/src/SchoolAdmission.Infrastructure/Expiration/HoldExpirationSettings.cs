using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace SchoolAdmission.Infrastructure.Expiration;

public sealed class HoldExpirationSettings
{
    public HoldExpirationSettings(int intervalSeconds = 5, int batchSize = 50)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(intervalSeconds);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);
        IntervalSeconds = intervalSeconds;
        BatchSize = batchSize;
    }

    public int IntervalSeconds { get; }
    public int BatchSize { get; }

    public static HoldExpirationSettings FromConfiguration(IConfiguration configuration) => new(
        ReadPositiveInteger(configuration, "HOLD_EXPIRATION_INTERVAL_SECONDS", 5),
        ReadPositiveInteger(configuration, "HOLD_EXPIRATION_BATCH_SIZE", 50));

    private static int ReadPositiveInteger(IConfiguration configuration, string key, int defaultValue)
    {
        var text = configuration[key];
        if (text is null) return defaultValue;
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value <= 0)
            throw new InvalidOperationException($"{key} must be a positive 32-bit integer.");
        return value;
    }
}

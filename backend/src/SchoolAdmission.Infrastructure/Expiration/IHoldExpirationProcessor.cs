namespace SchoolAdmission.Infrastructure.Expiration;

// A scoped cycle boundary also allows worker lifecycle tests without a database.
public interface IHoldExpirationProcessor
{
    Task<HoldExpirationBatchResult> ProcessBatchAsync(CancellationToken cancellationToken);
}

public sealed record HoldExpirationBatchResult(int CandidateClasses, long ExpiredHolds);

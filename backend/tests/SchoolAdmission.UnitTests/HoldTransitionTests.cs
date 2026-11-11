using SchoolAdmission.Domain.Entities;
using SchoolAdmission.Domain.Enums;

namespace SchoolAdmission.UnitTests;

public sealed class HoldTransitionTests
{
    private static readonly DateTime Created = new(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
    private static AdmissionHold NewHold() => new("h_test", "c_test", "001", Created, Created.AddSeconds(600));

    [Fact]
    public void ConfirmationIsIdempotentAndCannotBeCancelledOrExpired()
    {
        var hold = NewHold();
        Assert.True(hold.Confirm(Created.AddSeconds(1)));
        Assert.True(hold.Confirm(Created.AddSeconds(700)));
        Assert.Equal(Created.AddSeconds(1), hold.ConfirmedAt);
        Assert.Equal(HoldStatus.Confirmed, hold.Status);
        Assert.False(hold.Cancel(Created.AddSeconds(2)));
        Assert.False(hold.Expire(Created.AddSeconds(700)));
        Assert.Null(hold.CancelledAt);
    }

    [Fact]
    public void CancellationPreservesHistoryAndRejectsOtherTransitions()
    {
        var hold = NewHold();
        Assert.True(hold.Cancel(Created.AddSeconds(1)));
        Assert.False(hold.Cancel(Created.AddSeconds(2)));
        Assert.False(hold.Confirm(Created.AddSeconds(2)));
        Assert.False(hold.Expire(Created.AddSeconds(700)));
        Assert.Equal(HoldStatus.Cancelled, hold.Status);
        Assert.Equal(Created.AddSeconds(1), hold.CancelledAt);
        Assert.Null(hold.ConfirmedAt);
    }

    [Theory]
    [InlineData(600)]
    [InlineData(601)]
    public void AtOrAfterDeadlineOnlyExpirationIsAllowed(int seconds)
    {
        var hold = NewHold();
        Assert.False(hold.Confirm(Created.AddSeconds(seconds)));
        Assert.False(hold.Cancel(Created.AddSeconds(seconds)));
        Assert.True(hold.Expire(Created.AddSeconds(seconds)));
        Assert.False(hold.Expire(Created.AddSeconds(seconds)));
        Assert.False(hold.Confirm(Created.AddSeconds(seconds)));
        Assert.False(hold.Cancel(Created.AddSeconds(seconds)));
        Assert.Equal(HoldStatus.Expired, hold.Status);
        Assert.Null(hold.ConfirmedAt);
        Assert.Null(hold.CancelledAt);
    }

    [Fact]
    public void BeforeDeadlineExpirationDoesNotChangeHold()
    {
        var hold = NewHold();
        Assert.False(hold.Expire(Created.AddSeconds(599)));
        Assert.Equal(HoldStatus.Held, hold.Status);
        Assert.True(hold.Confirm(Created.AddSeconds(599)));
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void TransitionsRejectNonUtcTime(DateTimeKind kind)
    {
        var hold = NewHold();
        var time = DateTime.SpecifyKind(Created, kind);
        Assert.Throws<ArgumentException>(() => hold.Confirm(time));
        Assert.Throws<ArgumentException>(() => hold.Cancel(time));
        Assert.Throws<ArgumentException>(() => hold.Expire(time));
        Assert.Equal(HoldStatus.Held, hold.Status);
    }
}

using SchoolAdmission.Domain.Entities;
using SchoolAdmission.Domain.Enums;
using SchoolAdmission.Domain.Errors;

namespace SchoolAdmission.UnitTests.Domain;

public sealed class HoldTests
{
    private const string Student = "20152691234567890";
    private static readonly DateTimeOffset T0 = new(2026, 11, 1, 4, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Duration = TimeSpan.FromSeconds(600);
    private static readonly DateTimeOffset Expiry = T0 + Duration;

    private static Hold NewHold() => Hold.Create(Guid.CreateVersion7(T0), Guid.CreateVersion7(T0), Student, T0, Duration);

    // ---- Create ---------------------------------------------------------------

    [Fact]
    public void Create_SetsHeldStatusAndExpiryFromDuration()
    {
        var id = Guid.CreateVersion7(T0);
        var classId = Guid.CreateVersion7(T0);

        var hold = Hold.Create(id, classId, Student, T0, Duration);

        Assert.Equal(id, hold.Id);
        Assert.Equal(classId, hold.ClassId);
        Assert.Equal(Student, hold.StudentId);
        Assert.Equal(HoldStatus.Held, hold.Status);
        Assert.Equal(T0, hold.CreatedAt);
        Assert.Equal(Expiry, hold.ExpiresAt);
        Assert.Equal(T0, hold.UpdatedAt);
        Assert.Null(hold.ConfirmedAt);
        Assert.Null(hold.CancelledAt);
        Assert.Null(hold.ExpiredAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("2015269123456789")]   // 16 digits
    [InlineData("201526912345678901")] // 18 digits
    [InlineData("2015269123456789a")]
    public void Create_WithInvalidStudentId_Throws(string studentId) =>
        Assert.Throws<ArgumentException>(() => Hold.Create(Guid.NewGuid(), Guid.NewGuid(), studentId, T0, Duration));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WithNonPositiveDuration_Throws(int seconds) =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Hold.Create(Guid.NewGuid(), Guid.NewGuid(), Student, T0, TimeSpan.FromSeconds(seconds)));

    // ---- EffectiveStatus boundaries ------------------------------------------

    [Fact]
    public void EffectiveStatus_OneTickBeforeExpiry_IsHeld() =>
        Assert.Equal(HoldStatus.Held, NewHold().EffectiveStatus(Expiry.AddTicks(-1)));

    [Fact]
    public void EffectiveStatus_ExactlyAtExpiry_IsExpired() =>
        Assert.Equal(HoldStatus.Expired, NewHold().EffectiveStatus(Expiry));

    [Fact]
    public void EffectiveStatus_AfterExpiry_IsExpired_ButStoredStatusUnchanged()
    {
        var hold = NewHold();

        Assert.Equal(HoldStatus.Expired, hold.EffectiveStatus(Expiry.AddHours(1)));
        Assert.Equal(HoldStatus.Held, hold.Status);
    }

    [Fact]
    public void EffectiveStatus_OfConfirmedHold_StaysConfirmedAfterExpiry()
    {
        var hold = NewHold();
        hold.Confirm(T0.AddSeconds(10));

        Assert.Equal(HoldStatus.Confirmed, hold.EffectiveStatus(Expiry.AddDays(1)));
    }

    // ---- Confirm ----------------------------------------------------------------

    [Fact]
    public void Confirm_ActiveHold_ConfirmsAndReturnsTrue()
    {
        var hold = NewHold();
        var now = T0.AddSeconds(30);

        Assert.True(hold.Confirm(now));
        Assert.Equal(HoldStatus.Confirmed, hold.Status);
        Assert.Equal(now, hold.ConfirmedAt);
        Assert.Equal(now, hold.UpdatedAt);
    }

    [Fact]
    public void Confirm_Twice_IsIdempotent_AndKeepsFirstConfirmationTime()
    {
        var hold = NewHold();
        var first = T0.AddSeconds(30);
        hold.Confirm(first);

        Assert.False(hold.Confirm(T0.AddSeconds(60)));
        Assert.False(hold.Confirm(Expiry.AddHours(1))); // even after the original expiry
        Assert.Equal(HoldStatus.Confirmed, hold.Status);
        Assert.Equal(first, hold.ConfirmedAt);
        Assert.Equal(first, hold.UpdatedAt);
    }

    [Fact]
    public void Confirm_ExactlyAtExpiry_ThrowsHoldExpired()
    {
        var hold = NewHold();

        var ex = Assert.Throws<DomainException>(() => hold.Confirm(Expiry));

        Assert.Equal(ErrorCodes.HoldExpired, ex.Code);
        Assert.Equal(HoldStatus.Held, hold.Status);
        Assert.Null(hold.ConfirmedAt);
    }

    [Fact]
    public void Confirm_StoredExpiredHold_ThrowsHoldExpired()
    {
        var hold = NewHold();
        hold.Expire(Expiry);

        var ex = Assert.Throws<DomainException>(() => hold.Confirm(Expiry.AddSeconds(1)));
        Assert.Equal(ErrorCodes.HoldExpired, ex.Code);
    }

    [Fact]
    public void Confirm_CancelledHold_ThrowsHoldCancelled()
    {
        var hold = NewHold();
        hold.Cancel(T0.AddSeconds(5));

        var ex = Assert.Throws<DomainException>(() => hold.Confirm(T0.AddSeconds(10)));

        Assert.Equal(ErrorCodes.HoldCancelled, ex.Code);
        Assert.Equal(HoldStatus.Cancelled, hold.Status);
    }

    // ---- Cancel -----------------------------------------------------------------

    [Fact]
    public void Cancel_ActiveHold_CancelsAndReturnsTrue()
    {
        var hold = NewHold();
        var now = T0.AddSeconds(30);

        Assert.True(hold.Cancel(now));
        Assert.Equal(HoldStatus.Cancelled, hold.Status);
        Assert.Equal(now, hold.CancelledAt);
        Assert.Equal(now, hold.UpdatedAt);
    }

    [Fact]
    public void Cancel_Twice_IsIdempotent()
    {
        var hold = NewHold();
        var first = T0.AddSeconds(30);
        hold.Cancel(first);

        Assert.False(hold.Cancel(T0.AddSeconds(40)));
        Assert.False(hold.Cancel(Expiry.AddHours(1)));
        Assert.Equal(first, hold.CancelledAt);
    }

    [Fact]
    public void Cancel_ConfirmedHold_ThrowsHoldAlreadyConfirmed()
    {
        var hold = NewHold();
        hold.Confirm(T0.AddSeconds(5));

        var ex = Assert.Throws<DomainException>(() => hold.Cancel(T0.AddSeconds(10)));

        Assert.Equal(ErrorCodes.HoldAlreadyConfirmed, ex.Code);
        Assert.Equal(HoldStatus.Confirmed, hold.Status);
    }

    [Fact]
    public void Cancel_AfterExpiry_ThrowsHoldExpired()
    {
        var hold = NewHold();

        var ex = Assert.Throws<DomainException>(() => hold.Cancel(Expiry));

        Assert.Equal(ErrorCodes.HoldExpired, ex.Code);
        Assert.Null(hold.CancelledAt);
    }

    // ---- Expire -----------------------------------------------------------------

    [Fact]
    public void Expire_BeforeExpiry_DoesNothing()
    {
        var hold = NewHold();

        Assert.False(hold.Expire(Expiry.AddTicks(-1)));
        Assert.Equal(HoldStatus.Held, hold.Status);
        Assert.Null(hold.ExpiredAt);
    }

    [Fact]
    public void Expire_AtOrAfterExpiry_StoresExpired()
    {
        var hold = NewHold();
        var now = Expiry.AddSeconds(3);

        Assert.True(hold.Expire(now));
        Assert.Equal(HoldStatus.Expired, hold.Status);
        Assert.Equal(now, hold.ExpiredAt);
        Assert.Equal(now, hold.UpdatedAt);
        Assert.False(hold.Expire(now.AddSeconds(5))); // already expired
    }

    [Fact]
    public void Expire_NeverTouchesConfirmedOrCancelledHolds()
    {
        var confirmed = NewHold();
        confirmed.Confirm(T0.AddSeconds(1));
        var cancelled = NewHold();
        cancelled.Cancel(T0.AddSeconds(1));

        Assert.False(confirmed.Expire(Expiry.AddHours(1)));
        Assert.False(cancelled.Expire(Expiry.AddHours(1)));
        Assert.Equal(HoldStatus.Confirmed, confirmed.Status);
        Assert.Equal(HoldStatus.Cancelled, cancelled.Status);
    }
}

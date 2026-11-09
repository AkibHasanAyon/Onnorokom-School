using SchoolAdmission.Application.Classes;

namespace SchoolAdmission.UnitTests.Application;

public sealed class ClassSeatSummaryDtoTests
{
    [Theory]
    [InlineData(120, 15, 80, 25)] // API.md §4.2 example
    [InlineData(10, 0, 0, 10)]
    [InlineData(2, 1, 1, 0)]
    [InlineData(1, 2, 0, 0)]      // never negative, even if data were ever inconsistent
    public void Available_IsTotalMinusHeldMinusConfirmed_NeverNegative(int total, int held, int confirmed, int available)
    {
        var dto = ClassSeatSummaryDto.From(Guid.NewGuid(), "Class", total, held, confirmed);

        Assert.Equal(held, dto.HeldSeats);
        Assert.Equal(confirmed, dto.ConfirmedSeats);
        Assert.Equal(available, dto.AvailableSeats);
    }
}

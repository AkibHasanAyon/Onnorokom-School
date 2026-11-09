namespace SchoolAdmission.Application.Classes;

/// <summary>
/// One class with live seat accounting (contract, API.md §4.2/§4.3).
/// heldSeats = HELD with expiresAt &gt; now; confirmedSeats = CONFIRMED;
/// availableSeats = max(0, totalSeats − heldSeats − confirmedSeats).
/// </summary>
public sealed record ClassSeatSummaryDto(
    Guid Id, string Name, int TotalSeats, int HeldSeats, int ConfirmedSeats, int AvailableSeats)
{
    public static ClassSeatSummaryDto From(Guid id, string name, int totalSeats, int held, int confirmed) =>
        new(id, name, totalSeats, held, confirmed, Math.Max(0, totalSeats - held - confirmed));
}

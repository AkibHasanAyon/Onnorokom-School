using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SchoolAdmission.Domain.Enums;

namespace SchoolAdmission.Infrastructure.Persistence.Converters;

/// <summary>Stores <see cref="HoldStatus"/> as the contract strings: Held ↔ "HELD", etc.</summary>
internal sealed class HoldStatusConverter() : ValueConverter<HoldStatus, string>(
    v => v.ToString().ToUpperInvariant(),
    v => Enum.Parse<HoldStatus>(v, true))
{
    public static readonly HoldStatusConverter Instance = new();
}

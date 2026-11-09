using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolAdmission.Domain.Entities;

namespace SchoolAdmission.Infrastructure.Persistence.Configurations;

internal sealed class SchoolClassConfiguration : IEntityTypeConfiguration<SchoolClass>
{
    public void Configure(EntityTypeBuilder<SchoolClass> b)
    {
        b.ToTable("classes", t => t.HasCheckConstraint(
            "ck_classes_total_seats", $"total_seats BETWEEN {SchoolClass.MinSeats} AND {SchoolClass.MaxSeats}"));

        b.HasKey(c => c.Id).HasName("pk_classes");
        b.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(c => c.Name).HasColumnName("name").HasMaxLength(SchoolClass.MaxNameLength).IsRequired();
        b.Property(c => c.NormalizedName).HasColumnName("normalized_name").HasMaxLength(SchoolClass.MaxNameLength).IsRequired();
        b.Property(c => c.TotalSeats).HasColumnName("total_seats");
        b.Property(c => c.CreatedAt).HasColumnName("created_at");

        // Case-insensitive name uniqueness (CLASS_NAME_EXISTS) without an expression index.
        b.HasIndex(c => c.NormalizedName).IsUnique().HasDatabaseName("ux_classes_normalized_name");
    }
}

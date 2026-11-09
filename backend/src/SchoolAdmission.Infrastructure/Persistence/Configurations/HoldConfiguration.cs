using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolAdmission.Domain.Entities;
using SchoolAdmission.Infrastructure.Persistence.Converters;

namespace SchoolAdmission.Infrastructure.Persistence.Configurations;

internal sealed class HoldConfiguration : IEntityTypeConfiguration<Hold>
{
    public void Configure(EntityTypeBuilder<Hold> b)
    {
        b.ToTable("holds", t =>
        {
            t.HasCheckConstraint("ck_holds_status", "status IN ('HELD','CONFIRMED','EXPIRED','CANCELLED')");
            t.HasCheckConstraint("ck_holds_student_id", "student_id ~ '^[0-9]{17}$'");
            t.HasCheckConstraint("ck_holds_expiry_order", "expires_at > created_at");
        });

        b.HasKey(h => h.Id).HasName("pk_holds");
        b.Property(h => h.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(h => h.ClassId).HasColumnName("class_id");
        b.Property(h => h.StudentId).HasColumnName("student_id").HasMaxLength(17).IsRequired();
        b.Property(h => h.Status).HasColumnName("status").HasMaxLength(16).HasConversion(HoldStatusConverter.Instance);
        b.Property(h => h.CreatedAt).HasColumnName("created_at");
        b.Property(h => h.ExpiresAt).HasColumnName("expires_at");
        b.Property(h => h.ConfirmedAt).HasColumnName("confirmed_at");
        b.Property(h => h.CancelledAt).HasColumnName("cancelled_at");
        b.Property(h => h.ExpiredAt).HasColumnName("expired_at");
        b.Property(h => h.UpdatedAt).HasColumnName("updated_at");

        b.HasOne<SchoolClass>().WithMany().HasForeignKey(h => h.ClassId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_holds_classes_class_id");

        // R3 backstop: at most one HELD-or-CONFIRMED hold per (class, student).
        b.HasIndex(h => new { h.ClassId, h.StudentId }).IsUnique()
            .HasFilter("status IN ('HELD','CONFIRMED')")
            .HasDatabaseName("ux_holds_class_student_active");
        // Seat counting per class.
        b.HasIndex(h => new { h.ClassId, h.Status, h.ExpiresAt }).HasDatabaseName("ix_holds_class_status_expires");
        // GET /me/holds (newest first).
        b.HasIndex(h => new { h.StudentId, h.CreatedAt }).IsDescending(false, true).HasDatabaseName("ix_holds_student_created");
        // Expiry sweeper.
        b.HasIndex(h => h.ExpiresAt).HasFilter("status = 'HELD'").HasDatabaseName("ix_holds_held_expires");
    }
}

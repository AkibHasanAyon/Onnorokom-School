using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Infrastructure.Persistence;

namespace SchoolAdmission.IntegrationTests.Persistence;

/// <summary>
/// Checks the EF model against the target schema in docs/Database.md §2 by generating the DDL.
/// No database connection is needed.
/// </summary>
public sealed class ModelTests
{
    private static readonly string Ddl = GenerateDdl();

    private static string GenerateDdl()
    {
        var options = new DbContextOptionsBuilder<AdmissionDbContext>()
            .UseNpgsql("Host=unused;Database=unused")
            .Options;
        using var db = new AdmissionDbContext(options);
        return db.Database.GenerateCreateScript();
    }

    [Theory]
    [InlineData("CREATE TABLE classes")]
    [InlineData("id uuid NOT NULL")]
    [InlineData("name character varying(100) NOT NULL")]
    [InlineData("normalized_name character varying(100) NOT NULL")]
    [InlineData("total_seats integer NOT NULL")]
    [InlineData("created_at timestamp with time zone NOT NULL")]
    [InlineData("CONSTRAINT ck_classes_total_seats CHECK (total_seats BETWEEN 1 AND 10000)")]
    [InlineData("CREATE UNIQUE INDEX ux_classes_normalized_name ON classes (normalized_name)")]
    public void Classes_Table_MatchesTargetSchema(string expected) => Assert.Contains(expected, Ddl);

    [Theory]
    [InlineData("CREATE TABLE holds")]
    [InlineData("class_id uuid NOT NULL")]
    [InlineData("student_id character varying(17) NOT NULL")]
    [InlineData("status character varying(16) NOT NULL")]
    [InlineData("expires_at timestamp with time zone NOT NULL")]
    [InlineData("confirmed_at timestamp with time zone")]
    [InlineData("cancelled_at timestamp with time zone")]
    [InlineData("expired_at timestamp with time zone")]
    [InlineData("updated_at timestamp with time zone NOT NULL")]
    [InlineData("CONSTRAINT ck_holds_status CHECK (status IN ('HELD','CONFIRMED','EXPIRED','CANCELLED'))")]
    [InlineData("CONSTRAINT ck_holds_student_id CHECK (student_id ~ '^[0-9]{17}$')")]
    [InlineData("CONSTRAINT ck_holds_expiry_order CHECK (expires_at > created_at)")]
    [InlineData("REFERENCES classes (id) ON DELETE RESTRICT")]
    [InlineData("CREATE UNIQUE INDEX ux_holds_class_student_active ON holds (class_id, student_id) WHERE status IN ('HELD','CONFIRMED')")]
    [InlineData("CREATE INDEX ix_holds_class_status_expires ON holds (class_id, status, expires_at)")]
    [InlineData("CREATE INDEX ix_holds_student_created ON holds (student_id, created_at DESC)")]
    [InlineData("CREATE INDEX ix_holds_held_expires ON holds (expires_at) WHERE status = 'HELD'")]
    public void Holds_Table_MatchesTargetSchema(string expected) => Assert.Contains(expected, Ddl);

    [Fact]
    public void Model_HasNoSeatCounterColumns() // counts are always derived (Architecture.md §6)
    {
        Assert.DoesNotContain("held_seats", Ddl);
        Assert.DoesNotContain("confirmed_seats", Ddl);
        Assert.DoesNotContain("available_seats", Ddl);
    }
}

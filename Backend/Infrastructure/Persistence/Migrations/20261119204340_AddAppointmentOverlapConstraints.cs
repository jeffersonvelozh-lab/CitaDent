using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddAppointmentOverlapConstraints : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {

        migrationBuilder.Sql("""
            ALTER TABLE appointments ADD CONSTRAINT ex_appointments_dentist_no_overlap
                EXCLUDE USING gist (dentist_id WITH =, tstzrange(starts_at, ends_at, '[)') WITH &&)
                WHERE (status = 1);
            """);

        migrationBuilder.Sql("""
            ALTER TABLE appointments ADD CONSTRAINT ex_appointments_chair_no_overlap
                EXCLUDE USING gist (chair_id WITH =, tstzrange(starts_at, ends_at, '[)') WITH &&)
                WHERE (status = 1);
            """);

    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE appointments DROP CONSTRAINT ex_appointments_chair_no_overlap;");
        migrationBuilder.Sql("ALTER TABLE appointments DROP CONSTRAINT ex_appointments_dentist_no_overlap;");
    }
}

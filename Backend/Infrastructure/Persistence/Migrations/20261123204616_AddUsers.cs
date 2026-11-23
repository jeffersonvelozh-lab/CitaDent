using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddUsers : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "users",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                full_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                password_hash = table.Column<string>(type: "text", nullable: false),
                role = table.Column<short>(type: "smallint", nullable: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_users", x => x.id);
                table.CheckConstraint("ck_users_role", "role in (1, 2, 3)");
            });

        migrationBuilder.CreateIndex(
            name: "ix_appointments_created_by_user_id",
            table: "appointments",
            column: "created_by_user_id");

        migrationBuilder.CreateIndex(
            name: "ix_users_email",
            table: "users",
            column: "email",
            unique: true);

        migrationBuilder.AddForeignKey(
            name: "fk_appointments_users_created_by_user_id",
            table: "appointments",
            column: "created_by_user_id",
            principalTable: "users",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "fk_dentists_users_user_id",
            table: "dentists",
            column: "user_id",
            principalTable: "users",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "fk_appointments_users_created_by_user_id",
            table: "appointments");

        migrationBuilder.DropForeignKey(
            name: "fk_dentists_users_user_id",
            table: "dentists");

        migrationBuilder.DropTable(
            name: "users");

        migrationBuilder.DropIndex(
            name: "ix_appointments_created_by_user_id",
            table: "appointments");
    }
}

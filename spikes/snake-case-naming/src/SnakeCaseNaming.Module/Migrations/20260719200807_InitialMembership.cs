using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SnakeCaseNaming.Module.Migrations;

/// <inheritdoc />
public partial class _20260719200807_InitialMembership : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "membership");

        migrationBuilder.CreateTable(
            name: "user_accounts",
            schema: "membership",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                external_user_id = table.Column<string>(type: "text", nullable: false),
                display_name = table.Column<string>(type: "text", nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                iana_time_zone_id = table.Column<string>(type: "text", nullable: false),
                primary_contact_email_address = table.Column<string>(type: "text", nullable: false),
                primary_contact_mobile_phone_number = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_user_accounts", x => x.id);
                table.UniqueConstraint("ak_user_accounts_external_user_id", x => x.external_user_id);
            });

        migrationBuilder.CreateTable(
            name: "medication_profiles",
            schema: "membership",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                profile_display_name = table.Column<string>(type: "text", nullable: false),
                daily_dose_limit = table.Column<int>(type: "integer", nullable: false),
                is_archived = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_medication_profiles", x => x.id);
                table.CheckConstraint("ck_medication_profiles_daily_dose_limit_positive", "daily_dose_limit > 0");
                table.ForeignKey(
                    name: "fk_medication_profiles_user_accounts_user_account_id",
                    column: x => x.user_account_id,
                    principalSchema: "membership",
                    principalTable: "user_accounts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "dose_log_entries",
            schema: "membership",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                medication_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                taken_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                amount_taken = table.Column<decimal>(type: "numeric(9,3)", precision: 9, scale: 3, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_dose_log_entries", x => x.id);
                table.ForeignKey(
                    name: "fk_dose_log_entries_medication_profiles_medication_profile_id",
                    column: x => x.medication_profile_id,
                    principalSchema: "membership",
                    principalTable: "medication_profiles",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_dose_log_entries_medication_profile_id_taken_at_utc",
            schema: "membership",
            table: "dose_log_entries",
            columns: new[] { "medication_profile_id", "taken_at_utc" });

        migrationBuilder.CreateIndex(
            name: "ix_medication_profiles_user_account_id_profile_display_name",
            schema: "membership",
            table: "medication_profiles",
            columns: new[] { "user_account_id", "profile_display_name" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_user_accounts_display_name",
            schema: "membership",
            table: "user_accounts",
            column: "display_name",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "dose_log_entries",
            schema: "membership");

        migrationBuilder.DropTable(
            name: "medication_profiles",
            schema: "membership");

        migrationBuilder.DropTable(
            name: "user_accounts",
            schema: "membership");
    }
}

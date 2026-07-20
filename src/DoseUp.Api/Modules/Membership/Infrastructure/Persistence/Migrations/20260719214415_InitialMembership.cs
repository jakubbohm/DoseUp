using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoseUp.Api.Modules.Membership.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class _20260719214415_InitialMembership : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "membership");

        migrationBuilder.CreateTable(
            name: "accounts",
            schema: "membership",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                entra_object_id = table.Column<Guid>(type: "uuid", nullable: false),
                display_name = table.Column<string>(type: "text", nullable: false),
                email = table.Column<string>(type: "text", nullable: false),
                status = table.Column<int>(type: "integer", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_accounts", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "ix_accounts_entra_object_id",
            schema: "membership",
            table: "accounts",
            column: "entra_object_id",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "accounts",
            schema: "membership");
    }
}

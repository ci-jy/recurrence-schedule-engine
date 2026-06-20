using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rse.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "series",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Resource = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RRule = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DtStart = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    TimeZone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    ExDates = table.Column<List<DateTime>>(type: "timestamp without time zone[]", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_series", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_series_Resource",
                table: "series",
                column: "Resource");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "series");
        }
    }
}

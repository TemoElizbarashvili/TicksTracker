using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TickTracker.Utils.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialBaseLine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "AppUsageAggregates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProcessName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    TotalSeconds = table.Column<double>(type: "REAL", nullable: false),
                    SessionCount = table.Column<int>(type: "INTEGER", nullable: false),
                    FirstSeenUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSeenUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppUsageAggregates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppUsageIntervals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProcessName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    StartUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ProcessUsingDate = table.Column<DateOnly>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppUsageIntervals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BlacklistedApps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProcessName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlacklistedApps", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "AppSettings",
                columns: new[] { "Key", "Value" },
                values: new object[] { "IgnoreWindowsApps", "true" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppSettings");

            migrationBuilder.DropTable(
                name: "AppUsageAggregates");

            migrationBuilder.DropTable(
                name: "AppUsageIntervals");

            migrationBuilder.DropTable(
                name: "BlacklistedApps");
        }
    }
}

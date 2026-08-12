using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NewAlbumsDiscovery.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AggregatedBuckets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BucketName = table.Column<string>(type: "TEXT", nullable: false),
                    BucketType = table.Column<string>(type: "TEXT", nullable: false),
                    Country = table.Column<string>(type: "TEXT", nullable: false),
                    Language = table.Column<string>(type: "TEXT", nullable: true),
                    Genre = table.Column<string>(type: "TEXT", nullable: true),
                    TrackCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AggregatedBuckets", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AggregatedBuckets");
        }
    }
}

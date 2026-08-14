using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NewAlbumsDiscovery.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscoveredAlbums : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DiscoveredAlbums",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReferenceBucketId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Artist = table.Column<string>(type: "TEXT", nullable: false),
                    AlbumName = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    DiscoveredAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscoveredAlbums", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiscoveredAlbums_AggregatedBuckets_ReferenceBucketId",
                        column: x => x.ReferenceBucketId,
                        principalTable: "AggregatedBuckets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DiscoveredAlbums_ReferenceBucketId",
                table: "DiscoveredAlbums",
                column: "ReferenceBucketId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiscoveredAlbums");
        }
    }
}

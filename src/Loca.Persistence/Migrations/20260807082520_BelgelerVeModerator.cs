using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loca.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BelgelerVeModerator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // defaultValue ELLE true yapildi; uretilen hâli false'ti.
            // Sutun eklendiginde veritabanindaki her dosya gorsel ucundan
            // gelmisti (afis, mekan kapagi) — belge ucu bu migration'la
            // birlikte dogdu. false birakilsaydi mevcut butun afisler bir
            // anda erisilemez hâle gelir ve sebebi kodun hicbir yerinde
            // gorunmezdi.
            migrationBuilder.AddColumn<bool>(
                name: "IsPublic",
                table: "UploadedFiles",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "EventDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadedFileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventDocuments_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EventDocuments_UploadedFiles_UploadedFileId",
                        column: x => x.UploadedFileId,
                        principalTable: "UploadedFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "CreatedAt", "CreatedBy", "Description", "Name", "UpdatedAt", "UpdatedBy" },
                values: new object[] { new Guid("44444444-4444-4444-4444-444444444444"), new DateTime(2026, 7, 28, 0, 0, 0, 0, DateTimeKind.Utc), null, "Etkinlik ve organizator basvurularini inceleyip karara baglayan ekip.", "Moderator", null, null });

            migrationBuilder.CreateIndex(
                name: "IX_EventDocuments_EventId_Kind",
                table: "EventDocuments",
                columns: new[] { "EventId", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_EventDocuments_EventId_UploadedFileId",
                table: "EventDocuments",
                columns: new[] { "EventId", "UploadedFileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EventDocuments_UploadedFileId",
                table: "EventDocuments",
                column: "UploadedFileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventDocuments");

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"));

            migrationBuilder.DropColumn(
                name: "IsPublic",
                table: "UploadedFiles");
        }
    }
}

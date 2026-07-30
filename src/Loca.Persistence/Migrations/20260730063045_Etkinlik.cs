using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Loca.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Etkinlik : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OldValues = table.Column<string>(type: "jsonb", nullable: true),
                    NewValues = table.Column<string>(type: "jsonb", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditLogs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "EventCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrganizerApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TaxNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ContactEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ContactPhone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Website = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    DocumentFileId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ReviewedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizerApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganizerApplications_UploadedFiles_DocumentFileId",
                        column: x => x.DocumentFileId,
                        principalTable: "UploadedFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_OrganizerApplications_Users_ReviewedBy",
                        column: x => x.ReviewedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_OrganizerApplications_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrganizerProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TaxNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ContactEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ContactPhone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Website = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizerProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganizerProfiles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudentVerifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    InstitutionName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StudentNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    NationalIdentityNumber = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    DocumentFileId = table.Column<Guid>(type: "uuid", nullable: true),
                    ValidUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentVerifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentVerifications_UploadedFiles_DocumentFileId",
                        column: x => x.DocumentFileId,
                        principalTable: "UploadedFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_StudentVerifications_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    CancellationPolicy = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CityId = table.Column<Guid>(type: "uuid", nullable: false),
                    VenueId = table.Column<Guid>(type: "uuid", nullable: false),
                    HallId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    SalesStartsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SalesEndsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PosterFileId = table.Column<Guid>(type: "uuid", nullable: true),
                    MinimumAge = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Events_Cities_CityId",
                        column: x => x.CityId,
                        principalTable: "Cities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Events_EventCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "EventCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Events_Halls_HallId",
                        column: x => x.HallId,
                        principalTable: "Halls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Events_UploadedFiles_PosterFileId",
                        column: x => x.PosterFileId,
                        principalTable: "UploadedFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Events_Users_OrganizerId",
                        column: x => x.OrganizerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Events_Venues_VenueId",
                        column: x => x.VenueId,
                        principalTable: "Venues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EventSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    HallId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeatLayoutId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SalesStartsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SalesEndsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SeatsGenerated = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventSessions_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EventSessions_Halls_HallId",
                        column: x => x.HallId,
                        principalTable: "Halls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EventSessions_SeatLayouts_SeatLayoutId",
                        column: x => x.SeatLayoutId,
                        principalTable: "SeatLayouts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TicketTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeatSectionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Quota = table.Column<int>(type: "integer", nullable: false),
                    SalesStartsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SalesEndsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RequiresVerification = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    Price_Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Price_Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketTypes_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TicketTypes_SeatSections_SeatSectionId",
                        column: x => x.SeatSectionId,
                        principalTable: "SeatSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "EventSeats",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeatId = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    LockedUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LockedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: true),

                    // xmin BILINCLI OLARAK BURADA YOK.
                    // EventSeat.Version alani PostgreSQL'in xmin sistem
                    // kolonuna esleniyor; o kolon her tabloda kendiliginden
                    // var. Uretilen migration onu normal bir kolon gibi
                    // eklemeye calisiyordu ve PostgreSQL bunu reddediyor:
                    // "column name \"xmin\" conflicts with a system column name".
                    // Satir elle kaldirildi; model snapshot'inda kalmasi
                    // dogru, cunku kolon gercekten mevcut.
                    Price_Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Price_Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventSeats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventSeats_EventSessions_EventSessionId",
                        column: x => x.EventSessionId,
                        principalTable: "EventSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EventSeats_Seats_SeatId",
                        column: x => x.SeatId,
                        principalTable: "Seats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EventSeats_TicketTypes_TicketTypeId",
                        column: x => x.TicketTypeId,
                        principalTable: "TicketTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EventSeats_Users_LockedByUserId",
                        column: x => x.LockedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.InsertData(
                table: "EventCategories",
                columns: new[] { "Id", "CreatedAt", "CreatedBy", "Description", "IsActive", "Name", "Slug", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("22222222-0000-0000-0000-000000000001"), new DateTime(2026, 7, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, null, true, "Konser", "konser", null, null },
                    { new Guid("22222222-0000-0000-0000-000000000002"), new DateTime(2026, 7, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, null, true, "Tiyatro", "tiyatro", null, null },
                    { new Guid("22222222-0000-0000-0000-000000000003"), new DateTime(2026, 7, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, null, true, "Stand-up", "stand-up", null, null },
                    { new Guid("22222222-0000-0000-0000-000000000004"), new DateTime(2026, 7, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, null, true, "Konferans", "konferans", null, null },
                    { new Guid("22222222-0000-0000-0000-000000000005"), new DateTime(2026, 7, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, null, true, "Festival", "festival", null, null },
                    { new Guid("22222222-0000-0000-0000-000000000006"), new DateTime(2026, 7, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, null, true, "Çocuk", "cocuk", null, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityName_EntityId",
                table: "AuditLogs",
                columns: new[] { "EntityName", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_OccurredAtUtc",
                table: "AuditLogs",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId",
                table: "AuditLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_EventCategories_Name",
                table: "EventCategories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EventCategories_Slug",
                table: "EventCategories",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Events_CategoryId",
                table: "Events",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Events_CityId_CategoryId_EventDateUtc",
                table: "Events",
                columns: new[] { "CityId", "CategoryId", "EventDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Events_EventDateUtc",
                table: "Events",
                column: "EventDateUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Events_HallId",
                table: "Events",
                column: "HallId");

            migrationBuilder.CreateIndex(
                name: "IX_Events_OrganizerId",
                table: "Events",
                column: "OrganizerId");

            migrationBuilder.CreateIndex(
                name: "IX_Events_PosterFileId",
                table: "Events",
                column: "PosterFileId");

            migrationBuilder.CreateIndex(
                name: "IX_Events_Status",
                table: "Events",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Events_VenueId",
                table: "Events",
                column: "VenueId");

            migrationBuilder.CreateIndex(
                name: "IX_EventSeats_EventSessionId_SeatId",
                table: "EventSeats",
                columns: new[] { "EventSessionId", "SeatId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EventSeats_EventSessionId_Status",
                table: "EventSeats",
                columns: new[] { "EventSessionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_EventSeats_LockedByUserId",
                table: "EventSeats",
                column: "LockedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EventSeats_LockedUntilUtc",
                table: "EventSeats",
                column: "LockedUntilUtc",
                filter: "\"Status\" = 2");

            migrationBuilder.CreateIndex(
                name: "IX_EventSeats_ReservationId",
                table: "EventSeats",
                column: "ReservationId");

            migrationBuilder.CreateIndex(
                name: "IX_EventSeats_SeatId",
                table: "EventSeats",
                column: "SeatId");

            migrationBuilder.CreateIndex(
                name: "IX_EventSeats_TicketTypeId",
                table: "EventSeats",
                column: "TicketTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_EventSessions_EventId",
                table: "EventSessions",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_EventSessions_HallId_StartsAtUtc_EndsAtUtc",
                table: "EventSessions",
                columns: new[] { "HallId", "StartsAtUtc", "EndsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_EventSessions_SeatLayoutId",
                table: "EventSessions",
                column: "SeatLayoutId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizerApplications_DocumentFileId",
                table: "OrganizerApplications",
                column: "DocumentFileId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizerApplications_ReviewedBy",
                table: "OrganizerApplications",
                column: "ReviewedBy");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizerApplications_Status",
                table: "OrganizerApplications",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizerApplications_UserId",
                table: "OrganizerApplications",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizerProfiles_UserId",
                table: "OrganizerProfiles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentVerifications_DocumentFileId",
                table: "StudentVerifications",
                column: "DocumentFileId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentVerifications_InstitutionName_StudentNumber",
                table: "StudentVerifications",
                columns: new[] { "InstitutionName", "StudentNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentVerifications_NationalIdentityNumber",
                table: "StudentVerifications",
                column: "NationalIdentityNumber",
                unique: true,
                filter: "\"NationalIdentityNumber\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StudentVerifications_UserId",
                table: "StudentVerifications",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketTypes_EventId_Name",
                table: "TicketTypes",
                columns: new[] { "EventId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TicketTypes_EventId_SeatSectionId",
                table: "TicketTypes",
                columns: new[] { "EventId", "SeatSectionId" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketTypes_SeatSectionId",
                table: "TicketTypes",
                column: "SeatSectionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "EventSeats");

            migrationBuilder.DropTable(
                name: "OrganizerApplications");

            migrationBuilder.DropTable(
                name: "OrganizerProfiles");

            migrationBuilder.DropTable(
                name: "StudentVerifications");

            migrationBuilder.DropTable(
                name: "EventSessions");

            migrationBuilder.DropTable(
                name: "TicketTypes");

            migrationBuilder.DropTable(
                name: "Events");

            migrationBuilder.DropTable(
                name: "EventCategories");
        }
    }
}

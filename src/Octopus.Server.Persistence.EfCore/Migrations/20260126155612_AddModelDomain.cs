using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Octopus.Server.Persistence.EfCore.Migrations
{
    /// <inheritdoc />
    public partial class AddModelDomain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Models",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Models", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Models_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModelVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ModelId = table.Column<Guid>(type: "TEXT", nullable: false),
                    VersionNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    IfcFileId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WexBimFileId = table.Column<Guid>(type: "TEXT", nullable: true),
                    PropertiesFileId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelVersions_Files_IfcFileId",
                        column: x => x.IfcFileId,
                        principalTable: "Files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ModelVersions_Files_PropertiesFileId",
                        column: x => x.PropertiesFileId,
                        principalTable: "Files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ModelVersions_Files_WexBimFileId",
                        column: x => x.WexBimFileId,
                        principalTable: "Files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ModelVersions_Models_ModelId",
                        column: x => x.ModelId,
                        principalTable: "Models",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Models_ProjectId",
                table: "Models",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Models_ProjectId_Name",
                table: "Models",
                columns: new[] { "ProjectId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelVersions_IfcFileId",
                table: "ModelVersions",
                column: "IfcFileId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelVersions_ModelId",
                table: "ModelVersions",
                column: "ModelId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelVersions_ModelId_VersionNumber",
                table: "ModelVersions",
                columns: new[] { "ModelId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModelVersions_PropertiesFileId",
                table: "ModelVersions",
                column: "PropertiesFileId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelVersions_Status",
                table: "ModelVersions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ModelVersions_WexBimFileId",
                table: "ModelVersions",
                column: "WexBimFileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ModelVersions");

            migrationBuilder.DropTable(
                name: "Models");
        }
    }
}

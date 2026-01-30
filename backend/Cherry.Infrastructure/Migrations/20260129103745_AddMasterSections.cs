using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cherry.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMasterSections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Templates",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "RegionId",
                table: "Templates",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TemplateId",
                table: "Proposals",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MasterSections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionKey = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    DefaultContentJson = table.Column<string>(type: "text", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MasterSections", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Templates_RegionId",
                table: "Templates",
                column: "RegionId");

            migrationBuilder.CreateIndex(
                name: "IX_Proposals_TemplateId",
                table: "Proposals",
                column: "TemplateId");

            migrationBuilder.AddForeignKey(
                name: "FK_Proposals_Templates_TemplateId",
                table: "Proposals",
                column: "TemplateId",
                principalTable: "Templates",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Templates_Regions_RegionId",
                table: "Templates",
                column: "RegionId",
                principalTable: "Regions",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Proposals_Templates_TemplateId",
                table: "Proposals");

            migrationBuilder.DropForeignKey(
                name: "FK_Templates_Regions_RegionId",
                table: "Templates");

            migrationBuilder.DropTable(
                name: "MasterSections");

            migrationBuilder.DropIndex(
                name: "IX_Templates_RegionId",
                table: "Templates");

            migrationBuilder.DropIndex(
                name: "IX_Proposals_TemplateId",
                table: "Proposals");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Templates");

            migrationBuilder.DropColumn(
                name: "RegionId",
                table: "Templates");

            migrationBuilder.DropColumn(
                name: "TemplateId",
                table: "Proposals");
        }
    }
}

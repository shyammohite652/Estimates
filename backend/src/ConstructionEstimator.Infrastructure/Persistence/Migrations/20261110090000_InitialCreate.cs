using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConstructionEstimator.Infrastructure.Persistence.Migrations;

/// <summary>
/// Initial schema, generated from the EF Core model (see docs/AI-USE.md for how it was produced and verified).
/// ArchitectureTests.MigrationTests fails if the model drifts from this migration.
/// </summary>
public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "CatalogueItems",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Unit = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CatalogueItems", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Projects",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Client = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Projects", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Users",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                DisplayName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                Role = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Users", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "CatalogueRates",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CatalogueItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                Rate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                LabourComponent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CatalogueRates", x => x.Id);
                table.CheckConstraint("CK_CatalogueRates_Labour", "[LabourComponent] >= 0 AND [LabourComponent] <= [Rate]");
                table.ForeignKey(
                    name: "FK_CatalogueRates_CatalogueItems_CatalogueItemId",
                    column: x => x.CatalogueItemId,
                    principalTable: "CatalogueItems",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "Estimates",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Number = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                PricingDate = table.Column<DateOnly>(type: "date", nullable: false),
                Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Estimates", x => x.Id);
                table.ForeignKey(
                    name: "FK_Estimates_Projects_ProjectId",
                    column: x => x.ProjectId,
                    principalTable: "Projects",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Estimates_Users_CreatedById",
                    column: x => x.CreatedById,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "ProjectAssignments",
            columns: table => new
            {
                ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProjectAssignments", x => new { x.ProjectId, x.UserId });
                table.ForeignKey(
                    name: "FK_ProjectAssignments_Projects_ProjectId",
                    column: x => x.ProjectId,
                    principalTable: "Projects",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ProjectAssignments_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "EstimateHistory",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                EstimateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Action = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                FromStatus = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                ToStatus = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                Comment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EstimateHistory", x => x.Id);
                table.ForeignKey(
                    name: "FK_EstimateHistory_Estimates_EstimateId",
                    column: x => x.EstimateId,
                    principalTable: "Estimates",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_EstimateHistory_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "EstimateLines",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                EstimateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CatalogueItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ItemCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                ItemDescription = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Unit = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                RateEffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                UnitRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                UnitLabourCost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                MarkupPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                NetAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                MarkupAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                LineTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                LabourAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EstimateLines", x => x.Id);
                table.ForeignKey(
                    name: "FK_EstimateLines_CatalogueItems_CatalogueItemId",
                    column: x => x.CatalogueItemId,
                    principalTable: "CatalogueItems",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_EstimateLines_Estimates_EstimateId",
                    column: x => x.EstimateId,
                    principalTable: "Estimates",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_CatalogueItems_Code",
            table: "CatalogueItems",
            column: "Code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_CatalogueRates_CatalogueItemId_EffectiveFrom",
            table: "CatalogueRates",
            columns: new[] { "CatalogueItemId", "EffectiveFrom" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_EstimateHistory_EstimateId_OccurredAt",
            table: "EstimateHistory",
            columns: new[] { "EstimateId", "OccurredAt" });

        migrationBuilder.CreateIndex(
            name: "IX_EstimateHistory_UserId",
            table: "EstimateHistory",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "UX_EstimateHistory_SingleApproval",
            table: "EstimateHistory",
            column: "EstimateId",
            unique: true,
            filter: "[Action] = 'Approved'");

        migrationBuilder.CreateIndex(
            name: "IX_EstimateLines_CatalogueItemId",
            table: "EstimateLines",
            column: "CatalogueItemId");

        migrationBuilder.CreateIndex(
            name: "IX_EstimateLines_EstimateId_CatalogueItemId",
            table: "EstimateLines",
            columns: new[] { "EstimateId", "CatalogueItemId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_EstimateLines_EstimateId_Totals",
            table: "EstimateLines",
            column: "EstimateId")
            .Annotation("SqlServer:Include", new[] { "NetAmount", "MarkupAmount", "LineTotal", "LabourAmount" });

        migrationBuilder.CreateIndex(
            name: "IX_Estimates_CreatedById",
            table: "Estimates",
            column: "CreatedById");

        migrationBuilder.CreateIndex(
            name: "IX_Estimates_ProjectId_Number",
            table: "Estimates",
            columns: new[] { "ProjectId", "Number" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ProjectAssignments_UserId",
            table: "ProjectAssignments",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_Projects_Code",
            table: "Projects",
            column: "Code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Users_Email",
            table: "Users",
            column: "Email",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "EstimateLines");

        migrationBuilder.DropTable(name: "EstimateHistory");

        migrationBuilder.DropTable(name: "ProjectAssignments");

        migrationBuilder.DropTable(name: "Estimates");

        migrationBuilder.DropTable(name: "CatalogueRates");

        migrationBuilder.DropTable(name: "Users");

        migrationBuilder.DropTable(name: "Projects");

        migrationBuilder.DropTable(name: "CatalogueItems");
    }
}

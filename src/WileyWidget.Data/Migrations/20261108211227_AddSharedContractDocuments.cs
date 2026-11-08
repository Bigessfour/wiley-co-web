using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WileyWidget.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSharedContractDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "shared_contract_documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    enterprise_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    period = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shared_contract_documents", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_shared_contract_documents_kind_accepted_at",
                table: "shared_contract_documents",
                columns: new[] { "kind", "accepted_at" });

            migrationBuilder.CreateIndex(
                name: "IX_shared_contract_documents_kind_enterprise_id_period",
                table: "shared_contract_documents",
                columns: new[] { "kind", "enterprise_id", "period" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "shared_contract_documents");
        }
    }
}

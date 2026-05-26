using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace WileyWidget.Data.Migrations
{
    /// <inheritdoc />
    public partial class SchemaAlignmentProductionReadiness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_budget_snapshot_artifacts_budget_snapshots_budget_snapshot_~",
                table: "budget_snapshot_artifacts");

            migrationBuilder.DropForeignKey(
                name: "FK_budget_snapshots_source_files_source_file_id",
                table: "budget_snapshots");

            migrationBuilder.DropForeignKey(
                name: "FK_BudgetEntries_Departments_DepartmentId",
                table: "BudgetEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_BudgetEntries_Funds_FundId",
                table: "BudgetEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_BudgetInteraction_Enterprises_EnterpriseId",
                table: "BudgetInteraction");

            migrationBuilder.DropForeignKey(
                name: "FK_BudgetInteraction_Enterprises_PrimaryEnterpriseId",
                table: "BudgetInteraction");

            migrationBuilder.DropForeignKey(
                name: "FK_Charges_UtilityBills_BillId",
                table: "Charges");

            migrationBuilder.DropForeignKey(
                name: "FK_Charges_UtilityBills_UtilityBillId",
                table: "Charges");

            migrationBuilder.DropForeignKey(
                name: "FK_chart_of_accounts_source_files_source_file_id",
                table: "chart_of_accounts");

            migrationBuilder.DropForeignKey(
                name: "FK_customers_source_files_source_file_id",
                table: "customers");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_MunicipalAccounts_MunicipalAccountId",
                table: "Invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_ledger_entries_source_files_source_file_id",
                table: "ledger_entries");

            migrationBuilder.DropForeignKey(
                name: "FK_ledger_entry_lines_ledger_entries_ledger_entry_id",
                table: "ledger_entry_lines");

            migrationBuilder.DropForeignKey(
                name: "FK_MunicipalAccounts_Funds_FundId",
                table: "MunicipalAccounts");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Invoices_InvoiceId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_MunicipalAccounts_MunicipalAccountId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Vendor_VendorId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_profit_loss_monthly_lines_source_files_source_file_id",
                table: "profit_loss_monthly_lines");

            migrationBuilder.DropForeignKey(
                name: "FK_source_files_import_batches_batch_id",
                table: "source_files");

            migrationBuilder.DropForeignKey(
                name: "FK_source_files_source_file_variants_source_file_variant_id",
                table: "source_files");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_MunicipalAccounts_MunicipalAccountId",
                table: "Transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_trial_balance_lines_source_files_source_file_id",
                table: "trial_balance_lines");

            migrationBuilder.DropForeignKey(
                name: "FK_vendors_source_files_source_file_id",
                table: "vendors");

            migrationBuilder.DropIndex(
                name: "IX_Charges_BillId",
                table: "Charges");

            migrationBuilder.DropIndex(
                name: "IX_BudgetInteraction_EnterpriseId",
                table: "BudgetInteraction");

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "Charges"
                        WHERE COALESCE("UtilityBillId", 0) = 0
                          AND COALESCE("BillId", 0) = 0
                    ) THEN
                        RAISE EXCEPTION 'SchemaAlignmentProductionReadiness: Charges row(s) missing BillId/UtilityBillId';
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql("""
                UPDATE "Charges"
                SET "UtilityBillId" = "BillId"
                WHERE ("UtilityBillId" IS NULL OR "UtilityBillId" = 0) AND "BillId" IS NOT NULL;
                """);

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "Charges"
                        WHERE "UtilityBillId" IS NULL OR "UtilityBillId" = 0
                    ) THEN
                        RAISE EXCEPTION 'SchemaAlignmentProductionReadiness: Charges still missing UtilityBillId after copy';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropColumn(
                name: "BillId",
                table: "Charges");

            migrationBuilder.DropColumn(
                name: "EnterpriseId",
                table: "BudgetInteraction");

            migrationBuilder.AddColumn<int>(
                name: "EnterpriseId",
                table: "UtilityCustomers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "applied_allocation_profile_id",
                table: "ledger_entries",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "applied_allocation_profile_name",
                table: "ledger_entries",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "applied_routing_rule_id",
                table: "ledger_entries",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "applied_routing_rule_name",
                table: "ledger_entries",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "original_entry_scope",
                table: "ledger_entries",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "routing_allocation_percent",
                table: "ledger_entries",
                type: "numeric(18,2)",
                precision: 19,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "routing_reason",
                table: "ledger_entries",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "source_amount",
                table: "ledger_entries",
                type: "numeric(18,2)",
                precision: 19,
                scale: 4,
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "UtilityBillId",
                table: "Charges",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "EntityType",
                table: "AuditEntries",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                table: "AuditEntries",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateTable(
                name: "ApartmentUnitTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EnterpriseId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BedroomCount = table.Column<int>(type: "integer", nullable: false),
                    UnitCount = table.Column<int>(type: "integer", nullable: false),
                    MonthlyRent = table.Column<decimal>(type: "numeric(18,2)", precision: 19, scale: 4, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApartmentUnitTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApartmentUnitTypes_Enterprises_EnterpriseId",
                        column: x => x.EnterpriseId,
                        principalTable: "Enterprises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "quickbooks_allocation_profiles",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quickbooks_allocation_profiles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "quickbooks_allocation_targets",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    allocation_profile_id = table.Column<long>(type: "bigint", nullable: false),
                    enterprise_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    allocation_percent = table.Column<decimal>(type: "numeric(18,2)", precision: 19, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quickbooks_allocation_targets", x => x.id);
                    table.ForeignKey(
                        name: "FK_quickbooks_allocation_targets_quickbooks_allocation_profile~",
                        column: x => x.allocation_profile_id,
                        principalTable: "quickbooks_allocation_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "quickbooks_routing_rules",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    source_file_pattern = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    default_enterprise_pattern = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    account_pattern = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    memo_pattern = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    name_pattern = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    split_account_pattern = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    target_enterprise = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    allocation_profile_id = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quickbooks_routing_rules", x => x.id);
                    table.ForeignKey(
                        name: "FK_quickbooks_routing_rules_quickbooks_allocation_profiles_all~",
                        column: x => x.allocation_profile_id,
                        principalTable: "quickbooks_allocation_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UtilityCustomers_EnterpriseId",
                table: "UtilityCustomers",
                column: "EnterpriseId");

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_applied_allocation_profile_id",
                table: "ledger_entries",
                column: "applied_allocation_profile_id");

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_applied_routing_rule_id",
                table: "ledger_entries",
                column: "applied_routing_rule_id");

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_entry_date",
                table: "ledger_entries",
                column: "entry_date");

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_entry_scope",
                table: "ledger_entries",
                column: "entry_scope");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetEntries_SourceFilePath",
                table: "BudgetEntries",
                column: "SourceFilePath");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_EntityType_EntityId_Timestamp",
                table: "AuditEntries",
                columns: new[] { "EntityType", "EntityId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_Timestamp",
                table: "AuditEntries",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_ApartmentUnitTypes_EnterpriseId_Name",
                table: "ApartmentUnitTypes",
                columns: new[] { "EnterpriseId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_quickbooks_allocation_profiles_is_active",
                table: "quickbooks_allocation_profiles",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_quickbooks_allocation_targets_allocation_profile_id",
                table: "quickbooks_allocation_targets",
                column: "allocation_profile_id");

            migrationBuilder.CreateIndex(
                name: "IX_quickbooks_routing_rules_allocation_profile_id",
                table: "quickbooks_routing_rules",
                column: "allocation_profile_id");

            migrationBuilder.CreateIndex(
                name: "IX_quickbooks_routing_rules_is_active",
                table: "quickbooks_routing_rules",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_quickbooks_routing_rules_priority",
                table: "quickbooks_routing_rules",
                column: "priority");

            migrationBuilder.AddForeignKey(
                name: "FK_budget_snapshot_artifacts_budget_snapshots_budget_snapshot_~",
                table: "budget_snapshot_artifacts",
                column: "budget_snapshot_id",
                principalTable: "budget_snapshots",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_budget_snapshots_source_files_source_file_id",
                table: "budget_snapshots",
                column: "source_file_id",
                principalTable: "source_files",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetEntries_Departments_DepartmentId",
                table: "BudgetEntries",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetEntries_Funds_FundId",
                table: "BudgetEntries",
                column: "FundId",
                principalTable: "Funds",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetInteraction_Enterprises_PrimaryEnterpriseId",
                table: "BudgetInteraction",
                column: "PrimaryEnterpriseId",
                principalTable: "Enterprises",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Charges_UtilityBills_UtilityBillId",
                table: "Charges",
                column: "UtilityBillId",
                principalTable: "UtilityBills",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_chart_of_accounts_source_files_source_file_id",
                table: "chart_of_accounts",
                column: "source_file_id",
                principalTable: "source_files",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_customers_source_files_source_file_id",
                table: "customers",
                column: "source_file_id",
                principalTable: "source_files",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_MunicipalAccounts_MunicipalAccountId",
                table: "Invoices",
                column: "MunicipalAccountId",
                principalTable: "MunicipalAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ledger_entries_source_files_source_file_id",
                table: "ledger_entries",
                column: "source_file_id",
                principalTable: "source_files",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ledger_entry_lines_ledger_entries_ledger_entry_id",
                table: "ledger_entry_lines",
                column: "ledger_entry_id",
                principalTable: "ledger_entries",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MunicipalAccounts_Funds_FundId",
                table: "MunicipalAccounts",
                column: "FundId",
                principalTable: "Funds",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Invoices_InvoiceId",
                table: "Payments",
                column: "InvoiceId",
                principalTable: "Invoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_MunicipalAccounts_MunicipalAccountId",
                table: "Payments",
                column: "MunicipalAccountId",
                principalTable: "MunicipalAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Vendor_VendorId",
                table: "Payments",
                column: "VendorId",
                principalTable: "Vendor",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_profit_loss_monthly_lines_source_files_source_file_id",
                table: "profit_loss_monthly_lines",
                column: "source_file_id",
                principalTable: "source_files",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_source_files_import_batches_batch_id",
                table: "source_files",
                column: "batch_id",
                principalTable: "import_batches",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_source_files_source_file_variants_source_file_variant_id",
                table: "source_files",
                column: "source_file_variant_id",
                principalTable: "source_file_variants",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_MunicipalAccounts_MunicipalAccountId",
                table: "Transactions",
                column: "MunicipalAccountId",
                principalTable: "MunicipalAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_trial_balance_lines_source_files_source_file_id",
                table: "trial_balance_lines",
                column: "source_file_id",
                principalTable: "source_files",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UtilityCustomers_Enterprises_EnterpriseId",
                table: "UtilityCustomers",
                column: "EnterpriseId",
                principalTable: "Enterprises",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_vendors_source_files_source_file_id",
                table: "vendors",
                column: "source_file_id",
                principalTable: "source_files",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_budget_snapshot_artifacts_budget_snapshots_budget_snapshot_~",
                table: "budget_snapshot_artifacts");

            migrationBuilder.DropForeignKey(
                name: "FK_budget_snapshots_source_files_source_file_id",
                table: "budget_snapshots");

            migrationBuilder.DropForeignKey(
                name: "FK_BudgetEntries_Departments_DepartmentId",
                table: "BudgetEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_BudgetEntries_Funds_FundId",
                table: "BudgetEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_BudgetInteraction_Enterprises_PrimaryEnterpriseId",
                table: "BudgetInteraction");

            migrationBuilder.DropForeignKey(
                name: "FK_Charges_UtilityBills_UtilityBillId",
                table: "Charges");

            migrationBuilder.DropForeignKey(
                name: "FK_chart_of_accounts_source_files_source_file_id",
                table: "chart_of_accounts");

            migrationBuilder.DropForeignKey(
                name: "FK_customers_source_files_source_file_id",
                table: "customers");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_MunicipalAccounts_MunicipalAccountId",
                table: "Invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_ledger_entries_source_files_source_file_id",
                table: "ledger_entries");

            migrationBuilder.DropForeignKey(
                name: "FK_ledger_entry_lines_ledger_entries_ledger_entry_id",
                table: "ledger_entry_lines");

            migrationBuilder.DropForeignKey(
                name: "FK_MunicipalAccounts_Funds_FundId",
                table: "MunicipalAccounts");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Invoices_InvoiceId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_MunicipalAccounts_MunicipalAccountId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Vendor_VendorId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_profit_loss_monthly_lines_source_files_source_file_id",
                table: "profit_loss_monthly_lines");

            migrationBuilder.DropForeignKey(
                name: "FK_source_files_import_batches_batch_id",
                table: "source_files");

            migrationBuilder.DropForeignKey(
                name: "FK_source_files_source_file_variants_source_file_variant_id",
                table: "source_files");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_MunicipalAccounts_MunicipalAccountId",
                table: "Transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_trial_balance_lines_source_files_source_file_id",
                table: "trial_balance_lines");

            migrationBuilder.DropForeignKey(
                name: "FK_UtilityCustomers_Enterprises_EnterpriseId",
                table: "UtilityCustomers");

            migrationBuilder.DropForeignKey(
                name: "FK_vendors_source_files_source_file_id",
                table: "vendors");

            migrationBuilder.DropTable(
                name: "ApartmentUnitTypes");

            migrationBuilder.DropTable(
                name: "quickbooks_allocation_targets");

            migrationBuilder.DropTable(
                name: "quickbooks_routing_rules");

            migrationBuilder.DropTable(
                name: "quickbooks_allocation_profiles");

            migrationBuilder.DropIndex(
                name: "IX_UtilityCustomers_EnterpriseId",
                table: "UtilityCustomers");

            migrationBuilder.DropIndex(
                name: "IX_ledger_entries_applied_allocation_profile_id",
                table: "ledger_entries");

            migrationBuilder.DropIndex(
                name: "IX_ledger_entries_applied_routing_rule_id",
                table: "ledger_entries");

            migrationBuilder.DropIndex(
                name: "IX_ledger_entries_entry_date",
                table: "ledger_entries");

            migrationBuilder.DropIndex(
                name: "IX_ledger_entries_entry_scope",
                table: "ledger_entries");

            migrationBuilder.DropIndex(
                name: "IX_BudgetEntries_SourceFilePath",
                table: "BudgetEntries");

            migrationBuilder.DropIndex(
                name: "IX_AuditEntries_EntityType_EntityId_Timestamp",
                table: "AuditEntries");

            migrationBuilder.DropIndex(
                name: "IX_AuditEntries_Timestamp",
                table: "AuditEntries");

            migrationBuilder.DropColumn(
                name: "EnterpriseId",
                table: "UtilityCustomers");

            migrationBuilder.DropColumn(
                name: "applied_allocation_profile_id",
                table: "ledger_entries");

            migrationBuilder.DropColumn(
                name: "applied_allocation_profile_name",
                table: "ledger_entries");

            migrationBuilder.DropColumn(
                name: "applied_routing_rule_id",
                table: "ledger_entries");

            migrationBuilder.DropColumn(
                name: "applied_routing_rule_name",
                table: "ledger_entries");

            migrationBuilder.DropColumn(
                name: "original_entry_scope",
                table: "ledger_entries");

            migrationBuilder.DropColumn(
                name: "routing_allocation_percent",
                table: "ledger_entries");

            migrationBuilder.DropColumn(
                name: "routing_reason",
                table: "ledger_entries");

            migrationBuilder.DropColumn(
                name: "source_amount",
                table: "ledger_entries");

            migrationBuilder.AlterColumn<int>(
                name: "UtilityBillId",
                table: "Charges",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "BillId",
                table: "Charges",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EnterpriseId",
                table: "BudgetInteraction",
                type: "integer",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "EntityType",
                table: "AuditEntries",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                table: "AuditEntries",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.CreateIndex(
                name: "IX_Charges_BillId",
                table: "Charges",
                column: "BillId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetInteraction_EnterpriseId",
                table: "BudgetInteraction",
                column: "EnterpriseId");

            migrationBuilder.AddForeignKey(
                name: "FK_budget_snapshot_artifacts_budget_snapshots_budget_snapshot_~",
                table: "budget_snapshot_artifacts",
                column: "budget_snapshot_id",
                principalTable: "budget_snapshots",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_budget_snapshots_source_files_source_file_id",
                table: "budget_snapshots",
                column: "source_file_id",
                principalTable: "source_files",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetEntries_Departments_DepartmentId",
                table: "BudgetEntries",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetEntries_Funds_FundId",
                table: "BudgetEntries",
                column: "FundId",
                principalTable: "Funds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetInteraction_Enterprises_EnterpriseId",
                table: "BudgetInteraction",
                column: "EnterpriseId",
                principalTable: "Enterprises",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetInteraction_Enterprises_PrimaryEnterpriseId",
                table: "BudgetInteraction",
                column: "PrimaryEnterpriseId",
                principalTable: "Enterprises",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Charges_UtilityBills_BillId",
                table: "Charges",
                column: "BillId",
                principalTable: "UtilityBills",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Charges_UtilityBills_UtilityBillId",
                table: "Charges",
                column: "UtilityBillId",
                principalTable: "UtilityBills",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_chart_of_accounts_source_files_source_file_id",
                table: "chart_of_accounts",
                column: "source_file_id",
                principalTable: "source_files",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_customers_source_files_source_file_id",
                table: "customers",
                column: "source_file_id",
                principalTable: "source_files",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_MunicipalAccounts_MunicipalAccountId",
                table: "Invoices",
                column: "MunicipalAccountId",
                principalTable: "MunicipalAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ledger_entries_source_files_source_file_id",
                table: "ledger_entries",
                column: "source_file_id",
                principalTable: "source_files",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ledger_entry_lines_ledger_entries_ledger_entry_id",
                table: "ledger_entry_lines",
                column: "ledger_entry_id",
                principalTable: "ledger_entries",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MunicipalAccounts_Funds_FundId",
                table: "MunicipalAccounts",
                column: "FundId",
                principalTable: "Funds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Invoices_InvoiceId",
                table: "Payments",
                column: "InvoiceId",
                principalTable: "Invoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_MunicipalAccounts_MunicipalAccountId",
                table: "Payments",
                column: "MunicipalAccountId",
                principalTable: "MunicipalAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Vendor_VendorId",
                table: "Payments",
                column: "VendorId",
                principalTable: "Vendor",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_profit_loss_monthly_lines_source_files_source_file_id",
                table: "profit_loss_monthly_lines",
                column: "source_file_id",
                principalTable: "source_files",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_source_files_import_batches_batch_id",
                table: "source_files",
                column: "batch_id",
                principalTable: "import_batches",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_source_files_source_file_variants_source_file_variant_id",
                table: "source_files",
                column: "source_file_variant_id",
                principalTable: "source_file_variants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_MunicipalAccounts_MunicipalAccountId",
                table: "Transactions",
                column: "MunicipalAccountId",
                principalTable: "MunicipalAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_trial_balance_lines_source_files_source_file_id",
                table: "trial_balance_lines",
                column: "source_file_id",
                principalTable: "source_files",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_vendors_source_files_source_file_id",
                table: "vendors",
                column: "source_file_id",
                principalTable: "source_files",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}

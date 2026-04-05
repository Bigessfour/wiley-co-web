using Microsoft.EntityFrameworkCore;
using WileyWidget.Models.Amplify;
using WileyWidget.Models;
using System;

namespace WileyWidget.Data
{
    public interface IAppDbContext : IDisposable
    {
        DbSet<MunicipalAccount> MunicipalAccounts { get; }
        DbSet<ImportBatch> ImportBatches { get; }
        DbSet<SourceFileVariant> SourceFileVariants { get; }
        DbSet<SourceFile> SourceFiles { get; }
        DbSet<ChartOfAccount> ChartOfAccounts { get; }
        DbSet<AmplifyCustomer> AmplifyCustomers { get; }
        DbSet<AmplifyVendor> AmplifyVendors { get; }
        DbSet<LedgerEntry> LedgerEntries { get; }
        DbSet<LedgerEntryLine> LedgerEntryLines { get; }
        DbSet<TrialBalanceLine> TrialBalanceLines { get; }
        DbSet<ProfitLossMonthlyLine> ProfitLossMonthlyLines { get; }
        DbSet<BudgetSnapshot> BudgetSnapshots { get; }
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}

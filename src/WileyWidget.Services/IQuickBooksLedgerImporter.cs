using WileyCoWeb.Contracts;

namespace WileyWidget.Services;

public interface IQuickBooksLedgerImporter
{
    Task<QuickBooksImportPreviewResponse> PreviewAsync(
        byte[] fileBytes,
        string fileName,
        string selectedEnterprise,
        int selectedFiscalYear,
        CancellationToken cancellationToken = default);

    Task<QuickBooksImportCommitResponse> CommitAsync(
        byte[] fileBytes,
        string fileName,
        string selectedEnterprise,
        int selectedFiscalYear,
        CancellationToken cancellationToken = default);
}

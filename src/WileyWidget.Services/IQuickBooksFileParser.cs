using WileyCoWeb.Contracts;

namespace WileyWidget.Services;

public interface IQuickBooksFileParser
{
    Task<List<QuickBooksImportPreviewRow>> ParseAsync(byte[] fileBytes, string fileName);
}

public sealed record HeaderLookup(
    int TypeIndex,
    int DateIndex,
    int TransactionNumberIndex,
    int NameIndex,
    int MemoIndex,
    int AccountIndex,
    int SplitIndex,
    int AmountIndex,
    int BalanceIndex,
    int ClearedFlagIndex)
{
    public int FirstMappedColumnIndex => new[]
    {
        TypeIndex,
        DateIndex,
        TransactionNumberIndex,
        NameIndex,
        MemoIndex,
        AccountIndex,
        SplitIndex,
        AmountIndex,
        BalanceIndex,
        ClearedFlagIndex
    }.Where(index => index >= 0).DefaultIfEmpty(0).Min();
}
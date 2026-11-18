namespace WileyCoWeb.Contracts;

/// <summary>
/// October 26, 2026 Town of Wiley bill. Three thousand gallons is still the water minimum.
/// </summary>
public static class AdoptedResidentialBill
{
    public static readonly DateOnly BillDate = new(2026, 10, 26);

    public static readonly DateOnly ServiceFrom = new(2026, 9, 25);

    public static readonly DateOnly ServiceTo = new(2026, 10, 26);

    public static readonly DateOnly DueDate = new(2026, 11, 20);

    public const int UsageGallons = 3_000;

    public const decimal Water = 58.00m;

    public const decimal Sewage = 72.00m;

    public const decimal Garbage = 30.00m;

    public const decimal TotalDue = Water + Sewage + Garbage;

    public const decimal LatePenalty = 16.00m;

    public const decimal AfterDueDatePay = TotalDue + LatePenalty;
}

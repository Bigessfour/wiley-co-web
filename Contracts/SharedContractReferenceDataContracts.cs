using System.Text.Json;

namespace WileyCoWeb.Contracts;

/// <summary>Matches shared-contracts ActualsSummary (schemas/v1/actuals-summary.schema.json).</summary>
public sealed record ActualsSummaryContract(
    string EnterpriseId,
    string Period,
    string AmountsBasis,
    decimal Income,
    decimal Expenses,
    decimal CashBalance,
    IReadOnlyList<string>? CashBalanceAccounts = null,
    string? SchemaVersion = null);

/// <summary>Matches shared-contracts OperationalEvent (schemas/v1/operational-event.schema.json).</summary>
public sealed record OperationalEventContract(
    string Source,
    string Type,
    string Date,
    decimal Amount,
    string Description,
    JsonElement Metadata,
    string? SchemaVersion = null);

public sealed record ReferenceDataIngestAcceptedResponse(
    Guid Id,
    string Kind,
    string AcceptedAt);

public sealed record ReferenceDataActualsDocument(
    Guid Id,
    string AcceptedAt,
    ActualsSummaryContract Actuals);

public sealed record ReferenceDataEventsDocument(
    Guid Id,
    string AcceptedAt,
    IReadOnlyList<OperationalEventContract> Events);

using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using WileyCoWeb.Api.Configuration;
using WileyCoWeb.Contracts;
using WileyWidget.Data;
using WileyWidget.Models.Amplify;

namespace WileyCoWeb.Api;

internal static partial class ReferenceDataEndpoints
{
    internal const string ActualsSummaryKind = "actuals-summary";
    internal const string OperationalEventsKind = "operational-events";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> AllowedAmountsBasis = new(StringComparer.Ordinal)
    {
        "fy-ytd",
        "monthly"
    };

    private static readonly HashSet<string> AllowedEventSources = new(StringComparer.Ordinal)
    {
        "council-packet",
        "quickbooks-desktop",
        "nas-export",
        "other"
    };

    private static readonly HashSet<string> AllowedEventTypes = new(StringComparer.Ordinal)
    {
        "check",
        "deposit",
        "transfer",
        "summary"
    };

    public static void Map(WebApplication app)
    {
        var config = app.Configuration;

        app.MapPost("/api/reference-data/actuals", PostActualsAsync)
            .RequireWorkspaceMutatingAuth(config);
        app.MapGet("/api/reference-data/actuals", GetActualsAsync)
            .RequireWorkspaceReadAuth(config);
        app.MapGet("/api/reference-data/actuals/{id:guid}", GetActualsByIdAsync)
            .RequireWorkspaceReadAuth(config);

        app.MapPost("/api/reference-data/events", PostEventsAsync)
            .RequireWorkspaceMutatingAuth(config);
        app.MapGet("/api/reference-data/events", GetEventsAsync)
            .RequireWorkspaceReadAuth(config);
        app.MapGet("/api/reference-data/events/{id:guid}", GetEventsByIdAsync)
            .RequireWorkspaceReadAuth(config);
    }

    private static async Task<IResult> PostActualsAsync(
        ActualsSummaryContract request,
        IDbContextFactory<AppDbContext> contextFactory,
        CancellationToken cancellationToken)
    {
        if (!TryValidateActuals(request, out var error))
        {
            return error!;
        }

        var acceptedAt = DateTimeOffset.UtcNow;
        var document = new SharedContractDocument
        {
            Id = Guid.NewGuid(),
            Kind = ActualsSummaryKind,
            EnterpriseId = request.EnterpriseId.Trim(),
            Period = request.Period.Trim(),
            Payload = JsonSerializer.Serialize(request, JsonOptions),
            AcceptedAt = acceptedAt
        };

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        context.SharedContractDocuments.Add(document);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var body = new ReferenceDataIngestAcceptedResponse(
            document.Id,
            document.Kind,
            acceptedAt.ToString("O"));

        return Results.Accepted($"/api/reference-data/actuals/{document.Id}", body);
    }

    private static async Task<IResult> PostEventsAsync(
        IReadOnlyList<OperationalEventContract> request,
        IDbContextFactory<AppDbContext> contextFactory,
        CancellationToken cancellationToken)
    {
        if (request is null || request.Count == 0)
        {
            return Results.BadRequest("At least one OperationalEvent is required.");
        }

        for (var i = 0; i < request.Count; i++)
        {
            if (!TryValidateEvent(request[i], out var error, i))
            {
                return error!;
            }
        }

        var acceptedAt = DateTimeOffset.UtcNow;
        var document = new SharedContractDocument
        {
            Id = Guid.NewGuid(),
            Kind = OperationalEventsKind,
            EnterpriseId = ResolveBatchEnterpriseId(request),
            Period = null,
            Payload = JsonSerializer.Serialize(request, JsonOptions),
            AcceptedAt = acceptedAt
        };

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        context.SharedContractDocuments.Add(document);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var body = new ReferenceDataIngestAcceptedResponse(
            document.Id,
            document.Kind,
            acceptedAt.ToString("O"));

        return Results.Accepted($"/api/reference-data/events/{document.Id}", body);
    }

    private static async Task<IResult> GetActualsAsync(
        IDbContextFactory<AppDbContext> contextFactory,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await context.SharedContractDocuments
            .AsNoTracking()
            .Where(d => d.Kind == ActualsSummaryKind)
            .OrderByDescending(d => d.AcceptedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = new List<ReferenceDataActualsDocument>(rows.Count);
        foreach (var row in rows)
        {
            if (TryMapActualsDocument(row, out var mapped))
            {
                items.Add(mapped!);
            }
        }

        return Results.Ok(items);
    }

    private static async Task<IResult> GetActualsByIdAsync(
        Guid id,
        IDbContextFactory<AppDbContext> contextFactory,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var row = await context.SharedContractDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id && d.Kind == ActualsSummaryKind, cancellationToken)
            .ConfigureAwait(false);

        if (row is null || !TryMapActualsDocument(row, out var mapped))
        {
            return Results.NotFound();
        }

        return Results.Ok(mapped);
    }

    private static async Task<IResult> GetEventsAsync(
        IDbContextFactory<AppDbContext> contextFactory,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await context.SharedContractDocuments
            .AsNoTracking()
            .Where(d => d.Kind == OperationalEventsKind)
            .OrderByDescending(d => d.AcceptedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = new List<ReferenceDataEventsDocument>(rows.Count);
        foreach (var row in rows)
        {
            if (TryMapEventsDocument(row, out var mapped))
            {
                items.Add(mapped!);
            }
        }

        return Results.Ok(items);
    }

    private static async Task<IResult> GetEventsByIdAsync(
        Guid id,
        IDbContextFactory<AppDbContext> contextFactory,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var row = await context.SharedContractDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id && d.Kind == OperationalEventsKind, cancellationToken)
            .ConfigureAwait(false);

        if (row is null || !TryMapEventsDocument(row, out var mapped))
        {
            return Results.NotFound();
        }

        return Results.Ok(mapped);
    }

    private static bool TryMapActualsDocument(SharedContractDocument row, out ReferenceDataActualsDocument? mapped)
    {
        var actuals = JsonSerializer.Deserialize<ActualsSummaryContract>(row.Payload, JsonOptions);
        if (actuals is null)
        {
            mapped = null;
            return false;
        }

        mapped = new ReferenceDataActualsDocument(
            row.Id,
            row.AcceptedAt.ToString("O"),
            actuals);
        return true;
    }

    private static bool TryMapEventsDocument(SharedContractDocument row, out ReferenceDataEventsDocument? mapped)
    {
        var events = JsonSerializer.Deserialize<List<OperationalEventContract>>(row.Payload, JsonOptions);
        if (events is null)
        {
            mapped = null;
            return false;
        }

        mapped = new ReferenceDataEventsDocument(
            row.Id,
            row.AcceptedAt.ToString("O"),
            events);
        return true;
    }

    private static bool TryValidateActuals(ActualsSummaryContract request, out IResult? error)
    {
        if (request is null)
        {
            error = Results.BadRequest("ActualsSummary body is required.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.EnterpriseId)
            || string.IsNullOrWhiteSpace(request.Period)
            || string.IsNullOrWhiteSpace(request.AmountsBasis))
        {
            error = Results.BadRequest("enterpriseId, period, and amountsBasis are required.");
            return false;
        }

        if (!PeriodMonthPattern().IsMatch(request.Period.Trim()))
        {
            error = Results.BadRequest("period must be YYYY-MM.");
            return false;
        }

        if (!AllowedAmountsBasis.Contains(request.AmountsBasis.Trim()))
        {
            error = Results.BadRequest("amountsBasis must be fy-ytd or monthly.");
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryValidateEvent(OperationalEventContract item, out IResult? error, int index)
    {
        if (item is null)
        {
            error = Results.BadRequest($"events[{index}] is required.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(item.Source)
            || string.IsNullOrWhiteSpace(item.Type)
            || string.IsNullOrWhiteSpace(item.Date)
            || string.IsNullOrWhiteSpace(item.Description))
        {
            error = Results.BadRequest($"events[{index}] requires source, type, date, and description.");
            return false;
        }

        if (!AllowedEventSources.Contains(item.Source.Trim()))
        {
            error = Results.BadRequest($"events[{index}].source is not a supported value.");
            return false;
        }

        if (!AllowedEventTypes.Contains(item.Type.Trim()))
        {
            error = Results.BadRequest($"events[{index}].type is not a supported value.");
            return false;
        }

        if (!EventDatePattern().IsMatch(item.Date.Trim()))
        {
            error = Results.BadRequest($"events[{index}].date must be YYYY-MM-DD.");
            return false;
        }

        if (item.Metadata.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null or not JsonValueKind.Object)
        {
            error = Results.BadRequest($"events[{index}].metadata must be a JSON object.");
            return false;
        }

        if (string.Equals(item.Type, "check", StringComparison.Ordinal)
            && string.IsNullOrWhiteSpace(TryReadEnterpriseId(item.Metadata)))
        {
            error = Results.BadRequest($"events[{index}].metadata.enterpriseId is required for type=check.");
            return false;
        }

        if (string.Equals(item.Type, "summary", StringComparison.Ordinal)
            && (!item.Metadata.TryGetProperty("grandTotal", out var grandTotal)
                || grandTotal.ValueKind is not (JsonValueKind.Number or JsonValueKind.String)))
        {
            error = Results.BadRequest($"events[{index}].metadata.grandTotal is required for type=summary.");
            return false;
        }

        error = null;
        return true;
    }

    private static string? ResolveBatchEnterpriseId(IReadOnlyList<OperationalEventContract> events)
    {
        var ids = events
            .Select(e => TryReadEnterpriseId(e.Metadata))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return ids.Count == 1 ? ids[0] : null;
    }

    private static string? TryReadEnterpriseId(JsonElement metadata)
    {
        if (metadata.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (metadata.TryGetProperty("enterpriseId", out var value)
            && value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString();
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }

        return null;
    }

    [GeneratedRegex("^[0-9]{4}-(0[1-9]|1[0-2])$", RegexOptions.CultureInvariant)]
    private static partial Regex PeriodMonthPattern();

    [GeneratedRegex("^[0-9]{4}-(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01])$", RegexOptions.CultureInvariant)]
    private static partial Regex EventDatePattern();
}

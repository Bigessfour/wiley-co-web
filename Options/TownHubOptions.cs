using System.Collections.Generic;

namespace WileyCoWeb.Options;

/// <summary>
/// Configurable deep links for the Town Hub suite cards (local ports vs App Runner URLs).
/// Bound from configuration section <c>TownHub</c>.
/// </summary>
public sealed class TownHubOptions
{
    public const string SectionName = "TownHub";

    /// <summary>Apps shown as hub cards. Order is display order.</summary>
    public List<TownHubAppLink> Apps { get; set; } = [];
}

/// <summary>One external or in-app destination for a Town Hub card.</summary>
public sealed class TownHubAppLink
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    /// <summary>Relative path (same origin) or absolute URL (other apps / App Runner).</summary>
    public string Url { get; set; } = string.Empty;
    /// <summary>When true, open in a new tab (typical for sibling apps).</summary>
    public bool OpenInNewTab { get; set; } = true;

    /// <summary>
    /// Optional <c>metadata.enterpriseId</c> / ActualsSummary enterprise match for card status
    /// (e.g. <c>tow-apartments</c>). Empty = no enterprise match for this card.
    /// </summary>
    public string? ReferenceEnterpriseId { get; set; }

    /// <summary>
    /// Optional <c>metadata.publisherSystem</c> match (e.g. <c>wiley-apartments</c>).
    /// </summary>
    public string? ReferencePublisherSystem { get; set; }

    /// <summary>
    /// Optional OperationalEvent <c>source</c> values that count for this card
    /// (e.g. council-packet for Widget).
    /// </summary>
    public List<string> ReferenceSources { get; set; } = [];
}

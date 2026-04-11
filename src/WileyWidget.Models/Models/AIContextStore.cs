using System;
using System.Collections.Generic;

namespace WileyWidget.Models.Models
{
    /// <summary>
    /// Lightweight AI context store for scenario summaries and recommendation history.
    /// Can be persisted via repository or EF mapping to support auditability in the municipal finance context.
    /// Integrates with WileyWidgetContextService and UserContextPlugin for Jarvis chat persistence.
    /// </summary>
    public class AIContextStore
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string EnterpriseId { get; set; } = string.Empty;

        public string FiscalYear { get; set; } = string.Empty;

        public string ScenarioSummary { get; set; } = string.Empty;

        public List<RecommendationEntry> RecommendationHistory { get; set; } = new List<RecommendationEntry>();

        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        public class RecommendationEntry
        {
            public DateTime Timestamp { get; set; } = DateTime.UtcNow;
            public string Prompt { get; set; } = string.Empty;
            public string Recommendation { get; set; } = string.Empty;
            public string ConfidenceLevel { get; set; } = "Medium";
            public string Source { get; set; } = "Grok-4";
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel;
using Microsoft.SemanticKernel;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services.Plugins.Data
{
    public class DataReportingPlugin
    {
        private readonly IAnalyticsService _analyticsService;

        public DataReportingPlugin(IAnalyticsService analyticsService)
        {
            _analyticsService = analyticsService ?? throw new ArgumentNullException(nameof(analyticsService));
        }

        [KernelFunction]
        [Description("Performs an exploratory analysis of budget data for a given period.")]
        public async Task<string> AnalyzeBudgetAsync([Description("The start date for the analysis (yyyy-MM-dd).")] DateTime startDate,
            [Description("The end date for the analysis (yyyy-MM-dd).")] DateTime endDate,
            [Description("Optional entity name to filter by (e.g. 'Water Fund').")] string? entityName = null, CancellationToken cancellationToken = default)
        {
            var result = await _analyticsService.PerformExploratoryAnalysisAsync(startDate, endDate, entityName);

            // Format result as a readable summary for the LLM
            var sb = new StringBuilder();
            sb.AppendLine($"Budget Analysis ({startDate:d} to {endDate:d})");
            sb.AppendLine($"Data Trend: {result.TrendData.OverallTrend} (Growth: {result.TrendData.GrowthRate:P2})");

            if (result.Insights.Any())
            {
                sb.AppendLine("Key Insights:");
                foreach (var insight in result.Insights)
                    sb.AppendLine($"- {insight}");
            }

            if (result.TopVariances.Any())
            {
                sb.AppendLine("Top Variances:");
                foreach (var v in result.TopVariances.Take(5))
                {
                    sb.AppendLine($"- {v.AccountName}: Budget {v.BudgetedAmount:C}, Actual {v.ActualAmount:C}, Variance {v.VariancePercentage:F1}%");
                }
            }

            return sb.ToString();
        }

        // Isolated from council-facing Jarvis guidance per p1-rate-consolidation.
        // RunRateScenarioAsync remains available via IAnalyticsService for non-AI paths (dashboards, internal tools).
        // Do not re-add as [KernelFunction] without council review of rate advice surface.
    }
}

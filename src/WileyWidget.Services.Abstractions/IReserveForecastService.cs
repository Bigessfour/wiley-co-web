using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WileyWidget.Models;

namespace WileyWidget.Services.Abstractions;

public interface IReserveForecastService
{
    Task<ReserveTrajectoryForecastResult> GetTrajectoryAsync(
        IReadOnlyCollection<string> enterpriseTypes,
        int fiscalYear,
        decimal currentRate,
        decimal totalCosts,
        decimal projectedVolume,
        decimal recommendedRate,
        int years = 5,
        CancellationToken cancellationToken = default);
}
using System.Collections.Immutable;
using WileyCoWeb.Contracts;

namespace WileyCoWeb.State;

public sealed class CustomerFilterService
{
    private const string AllServicesOption = "All Services";
    private const string AllCityLimitsOption = "All";

    public static ImmutableArray<CustomerRow> FilterCustomers(
        IReadOnlyList<CustomerRow> customers,
        string searchTerm,
        string selectedService,
        string selectedCityLimits)
    {
        return customers
            .Where(customer => MatchesSearchTerm(customer, searchTerm))
            .Where(customer => MatchesServiceFilter(customer, selectedService))
            .Where(customer => MatchesCityLimitsFilter(customer, selectedCityLimits))
            .ToImmutableArray();
    }

    private static bool MatchesSearchTerm(CustomerRow customer, string searchTerm)
    {
        return string.IsNullOrWhiteSpace(searchTerm) || MatchesSearchFields(customer, searchTerm);
    }

    private static bool MatchesSearchFields(CustomerRow customer, string searchTerm)
    {
        return MatchesSearchField(customer.Name, searchTerm)
            || MatchesSearchField(customer.Service, searchTerm)
            || MatchesSearchField(customer.CityLimits, searchTerm);
    }

    private static bool MatchesSearchField(string value, string searchTerm)
    {
        return value.Contains(searchTerm, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesServiceFilter(CustomerRow customer, string selectedService)
    {
        return string.Equals(selectedService, AllServicesOption, StringComparison.Ordinal) ||
               string.Equals(customer.Service, selectedService, StringComparison.Ordinal);
    }

    private static bool MatchesCityLimitsFilter(CustomerRow customer, string selectedCityLimits)
    {
        return string.Equals(selectedCityLimits, AllCityLimitsOption, StringComparison.Ordinal) ||
               string.Equals(customer.CityLimits, selectedCityLimits, StringComparison.Ordinal);
    }
}
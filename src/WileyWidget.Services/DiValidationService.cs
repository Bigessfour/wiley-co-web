using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services
{
    /// <summary>
    /// Implementation of IDiValidationService that provides runtime DI container validation.
    /// Redirects to the implementation in Abstractions if needed, or provides core logic.
    /// </summary>
    public class DiValidationService : WileyWidget.Services.Abstractions.IDiValidationService
    {
        private static readonly Type[] CoreServiceInterfaces =
        [
            typeof(IAppEventBus),
            typeof(IFileImportService),
            typeof(IAnalyticsService),
            typeof(IAnalyticsRepository)
        ];

        private readonly ILogger<DiValidationService> _logger;

        public DiValidationService(ILogger<DiValidationService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public DiValidationReport ValidateRegistrations(IEnumerable<Assembly>? assembliesToScan = null, bool includeGenerics = false)
        {
            var report = new DiValidationReport();
            var candidateAssemblies = GetCandidateAssemblies(assembliesToScan).ToArray();
            var discoveredInterfaces = GetDiscoveredInterfaces(candidateAssemblies, includeGenerics);

            foreach (var serviceInterface in discoveredInterfaces)
            {
                var implementations = FindImplementations(serviceInterface, candidateAssemblies, includeGenerics);
                if (implementations.Count == 0)
                {
                    report.MissingServices.Add(serviceInterface.FullName ?? serviceInterface.Name);
                    report.Errors.Add(new DiValidationError
                    {
                        ServiceType = serviceInterface.FullName ?? serviceInterface.Name,
                        ErrorMessage = "No concrete implementation was found in the scanned assemblies.",
                        SuggestedFix = $"Add a class that implements {serviceInterface.Name} and register it in the service collection."
                    });
                    continue;
                }

                report.ResolvedServices.Add(serviceInterface.FullName ?? serviceInterface.Name);

                if (implementations.Count > 1)
                {
                    _logger.LogWarning(
                        "Multiple implementations found for {ServiceInterface}: {Implementations}",
                        serviceInterface.FullName ?? serviceInterface.Name,
                        string.Join(", ", implementations.Select(type => type.FullName ?? type.Name)));
                }
            }

            return report;
        }

        public bool ValidateCoreServices()
        {
            var candidateAssemblies = GetCandidateAssemblies(null).ToArray();

            foreach (var serviceInterface in CoreServiceInterfaces)
            {
                var implementations = FindImplementations(serviceInterface, candidateAssemblies, includeGenerics: false);
                if (implementations.Count == 0)
                {
                    _logger.LogWarning("Core service implementation missing for {ServiceInterface}", serviceInterface.FullName ?? serviceInterface.Name);
                    return false;
                }
            }

            return true;
        }

        #pragma warning disable S2325
        public IEnumerable<string> GetDiscoveredServiceInterfaces(IEnumerable<Assembly>? assembliesToScan = null)
        {
            var candidateAssemblies = GetCandidateAssemblies(assembliesToScan);
            return GetDiscoveredInterfaces(candidateAssemblies, includeGenerics: true)
                .Select(type => type.FullName ?? type.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
        }
        #pragma warning restore S2325

        public DiValidationResult ValidateServiceCategory(IServiceProvider serviceProvider, IEnumerable<Type> serviceTypes, string categoryName)
        {
            return ValidateServiceCategory(serviceProvider, serviceTypes, categoryName, false);
        }

        public DiValidationResult ValidateServiceCategory(IServiceProvider serviceProvider, IEnumerable<Type> serviceTypes, string categoryName, bool fastValidation)
        {
            var result = new DiValidationResult();
            var stopwatch = Stopwatch.StartNew();

            _logger.LogInformation("=== Starting {Category} Validation ({Mode}) ===", categoryName, fastValidation ? "Fast" : "Full");

            try
            {
                var isService = serviceProvider.GetService<IServiceProviderIsService>();

                foreach (var type in serviceTypes ?? Enumerable.Empty<Type>())
                {
                    ValidateServiceType(serviceProvider, isService, result, type, fastValidation);
                }
            }
            catch (OperationCanceledException oce)
            {
                // Timeout during entire category validation
                var message = $"Category '{categoryName}' validation was canceled (timeout): {oce.Message}";
                result.Warnings.Add(message);
                result.Errors.Add(message);
                _logger.LogWarning(oce, "CATEGORY TIMEOUT {Category}", categoryName);
            }
            catch (TimeoutException tex)
            {
                // Explicit timeout during category validation
                var message = $"Category '{categoryName}' validation timed out: {tex.Message}";
                result.Warnings.Add(message);
                result.Errors.Add(message);
                _logger.LogWarning(tex, "CATEGORY TIMEOUT {Category}", categoryName);
            }
            catch (Exception ex)
            {
                // Unexpected error during category validation
                var message = $"Unexpected error during '{categoryName}' validation ({ex.GetType().Name}): {ex.Message}";
                result.Errors.Add(message);
                _logger.LogError(ex, "CATEGORY ERROR {Category}: {ExceptionType}", categoryName, ex.GetType().Name);
            }
            finally
            {
                stopwatch.Stop();
                result.ValidationDuration = stopwatch.Elapsed;
                result.IsValid = result.Errors.Count == 0;

                _logger.LogInformation("=== Completed {Category} Validation in {Duration}ms (Valid: {IsValid}) ===",
                    categoryName, result.ValidationDuration.TotalMilliseconds, result.IsValid);
            }

            return result;
        }

        private void ValidateServiceType(IServiceProvider serviceProvider, IServiceProviderIsService? isService, DiValidationResult result, Type type, bool fastValidation)
        {
            try
            {
                if (fastValidation && isService != null)
                {
                    if (isService.IsService(type))
                    {
                        result.SuccessMessages.Add($"✓ {type.Name} registered successfully");
                        _logger.LogInformation("OK {Type}", type.Name);
                    }
                    else
                    {
                        result.Errors.Add($"✗ {type.Name} is NOT registered");
                        _logger.LogError("FAIL {Type}", type.Name);
                    }

                    return;
                }

                using var scope = serviceProvider.CreateScope();
                var instance = scope.ServiceProvider.GetService(type);
                if (instance is not null)
                {
                    result.SuccessMessages.Add($"✓ {type.Name} resolved successfully");
                    _logger.LogInformation("OK {Type}", type.Name);
                }
                else
                {
                    result.Errors.Add($"✗ {type.Name} failed to resolve");
                    _logger.LogError("FAIL {Type}", type.Name);
                }
            }
            catch (OperationCanceledException oce)
            {
                // Timeout during service resolution (e.g., telemetry service)
                var message = $"✗ {type.Name} resolution timed out (OperationCanceledException): {oce.Message}";
                result.Errors.Add(message);
                result.Warnings.Add($"{type.Name} initialization may have timed out");
                _logger.LogWarning(oce, "TIMEOUT {Type}", type.Name);
            }
            catch (TimeoutException tex)
            {
                // Explicit timeout during service resolution
                var message = $"✗ {type.Name} resolution timed out: {tex.Message}";
                result.Errors.Add(message);
                result.Warnings.Add($"{type.Name} took too long to initialize");
                _logger.LogWarning(tex, "TIMEOUT {Type}", type.Name);
            }
            catch (Exception ex)
            {
                result.Errors.Add($"✗ {type.Name} resolution error: {ex.GetType().Name}: {ex.Message}");
                _logger.LogError(ex, "ERROR {Type}: {ExceptionType}", type.Name, ex.GetType().Name);
            }
        }

        private static IEnumerable<Assembly> GetCandidateAssemblies(IEnumerable<Assembly>? assembliesToScan)
        {
            if (assembliesToScan != null)
            {
                return assembliesToScan.Where(assembly => assembly != null && !assembly.IsDynamic).Distinct();
            }

            return AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly =>
                    !assembly.IsDynamic &&
                    (assembly.GetName().Name?.StartsWith("WileyWidget", StringComparison.Ordinal) == true ||
                     assembly.GetName().Name?.StartsWith("WileyCoWeb", StringComparison.Ordinal) == true))
                .Distinct();
        }

        private static IReadOnlyList<Type> GetDiscoveredInterfaces(IEnumerable<Assembly> assemblies, bool includeGenerics)
        {
            return assemblies
                .SelectMany(GetLoadableTypes)
                .Where(type => type.IsInterface && type.IsPublic)
                .Where(type => includeGenerics || !type.ContainsGenericParameters)
                .Distinct()
                .OrderBy(type => type.FullName ?? type.Name, StringComparer.Ordinal)
                .ToArray();
        }

        private static IReadOnlyList<Type> FindImplementations(Type serviceInterface, IEnumerable<Assembly> assemblies, bool includeGenerics)
        {
            return assemblies
                .SelectMany(GetLoadableTypes)
                .Where(type => type.IsClass && !type.IsAbstract && type.IsPublic)
                .Where(type => includeGenerics || !type.ContainsGenericParameters)
                .Where(type => ImplementsInterface(type, serviceInterface))
                .Distinct()
                .OrderBy(type => type.FullName ?? type.Name, StringComparer.Ordinal)
                .ToArray();
        }

        private static bool ImplementsInterface(Type candidateType, Type serviceInterface)
        {
            if (serviceInterface.IsGenericTypeDefinition)
            {
                return candidateType.GetInterfaces().Any(interfaceType =>
                    interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == serviceInterface);
            }

            return serviceInterface.IsAssignableFrom(candidateType);
        }

        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(type => type != null)!;
            }
        }
    }
}

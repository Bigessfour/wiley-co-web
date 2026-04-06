using System.ComponentModel.DataAnnotations;
using System.Reflection;
using WileyWidget.Models;

namespace WileyWidget.Tests;

public sealed class ModelReflectionSmokeTests
{
    [Fact]
    public void PublicModelTypes_WithParameterlessConstructors_CanBeInstantiatedAndExercised()
    {
        var assembly = typeof(BudgetEntry).Assembly;
        var modelTypes = assembly
            .GetExportedTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false, IsEnum: false })
            .Where(type => type.Namespace != null && type.Namespace.StartsWith("WileyWidget.Models", StringComparison.Ordinal))
            .Where(type => type.GetConstructor(Type.EmptyTypes) is not null)
            .ToList();

        Assert.NotEmpty(modelTypes);

        foreach (var type in modelTypes)
        {
            var instance = Activator.CreateInstance(type)!;

            foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                _ = property.GetValue(instance);

                if (!property.CanWrite)
                {
                    continue;
                }

                var value = CreateSampleValue(property.PropertyType, property.Name);
                if (value is not null)
                {
                    property.SetValue(instance, value);
                    _ = property.GetValue(instance);
                }
            }

            if (instance is IValidatableObject validatable)
            {
                _ = validatable.Validate(new ValidationContext(instance)).ToList();
            }
        }
    }

    private static object? CreateSampleValue(Type propertyType, string propertyName)
    {
        var targetType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

        return CreateSimpleValue(targetType, propertyName)
            ?? CreateCollectionValue(targetType)
            ?? CreateReferenceValue(targetType);
    }

    private static object? CreateSimpleValue(Type targetType, string propertyName)
    {
        return targetType switch
        {
            Type t when t == typeof(string) => propertyName,
            Type t when t == typeof(bool) => true,
            Type t when t == typeof(int) => 1,
            Type t when t == typeof(long) => 1L,
            Type t when t == typeof(decimal) => 1m,
            Type t when t == typeof(double) => 1.0d,
            Type t when t == typeof(DateTime) => DateTime.UtcNow,
            Type t when t == typeof(DateTimeOffset) => DateTimeOffset.UtcNow,
            Type t when t == typeof(Guid) => Guid.NewGuid(),
            Type t when t.IsEnum => Enum.GetValues(t).GetValue(0),
            _ => null
        };
    }

    private static object? CreateCollectionValue(Type targetType)
    {
        if (!targetType.IsGenericType || !typeof(System.Collections.IEnumerable).IsAssignableFrom(targetType))
        {
            return null;
        }

        var elementType = targetType.GetGenericArguments()[0];
        var listType = typeof(List<>).MakeGenericType(elementType);
        return targetType.IsAssignableFrom(listType) ? Activator.CreateInstance(listType) : null;
    }

    private static object? CreateReferenceValue(Type targetType)
    {
        if (!targetType.IsClass || targetType.GetConstructor(Type.EmptyTypes) is null)
        {
            return null;
        }

        if (targetType.Namespace is null || targetType.Namespace.StartsWith("System", StringComparison.Ordinal))
        {
            return null;
        }

        return Activator.CreateInstance(targetType);
    }
}
using System.Reflection;

namespace WileyCoWeb.IntegrationTests;

public sealed class HelperTypeTests
{
    [Fact]
    public void AppDbStartupState_MarksAndResetsFlags_AsExpected()
    {
        AppDbStartupState.ResetForTests();

        try
        {
            Assert.False(AppDbStartupState.InitializationAttempted);
            Assert.False(AppDbStartupState.FallbackActivated);
            Assert.Null(AppDbStartupState.FallbackReason);
            Assert.False(AppDbStartupState.IsDegradedMode);

            AppDbStartupState.MarkInitializationAttempted();
            AppDbStartupState.ActivateFallback("primary database unavailable");
            AppDbStartupState.ActivateFallback("ignored replacement reason");

            Assert.True(AppDbStartupState.InitializationAttempted);
            Assert.True(AppDbStartupState.FallbackActivated);
            Assert.Equal("primary database unavailable", AppDbStartupState.FallbackReason);
            Assert.True(AppDbStartupState.IsDegradedMode);
        }
        finally
        {
            AppDbStartupState.ResetForTests();
        }
    }

    [Fact]
    public void GridDisplayAttribute_StoresConfiguredValues()
    {
        var attribute = new GridDisplayAttribute(4, 120)
        {
            Visible = false,
            DecimalDigits = 2,
            Format = "N2"
        };

        Assert.Equal(4, attribute.DisplayOrder);
        Assert.Equal(120, attribute.Width);
        Assert.False(attribute.Visible);
        Assert.Equal(2, attribute.DecimalDigits);
        Assert.Equal("N2", attribute.Format);
    }

    [Fact]
    public void ExcelColumnAttribute_StoresConfiguredValues()
    {
        var attribute = new ExcelColumnAttribute("Current Rate", order: 3, format: "C2", isTotaled: true);

        Assert.Equal("Current Rate", attribute.Name);
        Assert.Equal(3, attribute.Order);
        Assert.Equal("C2", attribute.Format);
        Assert.True(attribute.IsTotaled);
    }

    [Fact]
    public void ConcurrencyConflictException_PreservesMetadata_AndFormatsMessage()
    {
        var databaseValues = new Dictionary<string, object?>
        {
            ["Rate"] = 32.5m,
            ["UpdatedAt"] = new DateTime(2026, 4, 5)
        };
        var clientValues = new Dictionary<string, object?>
        {
            ["Rate"] = 31.25m,
            ["UpdatedAt"] = new DateTime(2026, 4, 4)
        };
        var inner = new InvalidOperationException("stale data");

        var exception = new ConcurrencyConflictException("WorkspaceSnapshot", databaseValues, clientValues, inner);

        Assert.Contains("WorkspaceSnapshot", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("WorkspaceSnapshot", exception.EntityName);
        Assert.Same(databaseValues, exception.DatabaseValues);
        Assert.Same(clientValues, exception.ClientValues);
        Assert.Same(inner, exception.InnerException);
    }

    [Fact]
    public void ConcurrencyConflictException_ToDictionary_ReturnsNull_ForNullPropertyValues()
    {
        var method = typeof(ConcurrencyConflictException).GetMethod("ToDictionary", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        var result = method!.Invoke(null, new object?[] { null });

        Assert.Null(result);
    }
}

using Microsoft.Extensions.Configuration;
using WileyWidget.Services;

namespace WileyWidget.Tests;

public sealed class SettingsServiceTests
{
	[Fact]
	public void Save_WritesSettingsFile_ToConfiguredDirectory()
	{
		var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

		try
		{
			var configuration = new ConfigurationBuilder()
				.AddInMemoryCollection(new Dictionary<string, string?>
				{
					["Settings:Directory"] = tempDir
				})
				.Build();

			var service = new SettingsService(configuration);
			service.Current.Theme = "FluentLight";
			service.Save();

			var settingsFile = Path.Combine(tempDir, "settings.json");

			Assert.True(File.Exists(settingsFile));
			var json = File.ReadAllText(settingsFile);
			Assert.Contains("FluentLight", json, StringComparison.OrdinalIgnoreCase);
		}
		finally
		{
			if (Directory.Exists(tempDir))
			{
				Directory.Delete(tempDir, recursive: true);
			}
		}
	}

	[Fact]
	public void SaveFiscalYearSettings_UpdatesCurrentSettings_AndPersistsValues()
	{
		var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

		try
		{
			var configuration = new ConfigurationBuilder()
				.AddInMemoryCollection(new Dictionary<string, string?>
				{
					["Settings:Directory"] = tempDir
				})
				.Build();

			var service = new SettingsService(configuration);

			service.SaveFiscalYearSettings(7, 1);

			Assert.Equal(7, service.Current.FiscalYearStartMonth);
			Assert.Equal(1, service.Current.FiscalYearStartDay);
			Assert.NotNull(service.Current.FiscalYearStart);
			Assert.NotNull(service.Current.FiscalYearEnd);
			Assert.True(File.Exists(Path.Combine(tempDir, "settings.json")));
		}
		finally
		{
			if (Directory.Exists(tempDir))
			{
				Directory.Delete(tempDir, recursive: true);
			}
		}
	}

	[Theory]
	[InlineData(0, 1)]
	[InlineData(13, 1)]
	[InlineData(2, 30)]
	public void SaveFiscalYearSettings_ThrowsForInvalidDateParts(int month, int day)
	{
		var service = new SettingsService(new ConfigurationBuilder().Build());

		Assert.ThrowsAny<ArgumentOutOfRangeException>(() => service.SaveFiscalYearSettings(month, day));
	}
}
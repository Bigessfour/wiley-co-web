using Microsoft.Extensions.Configuration;
using System.Text.Json;
using WileyWidget.Models;
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

	[Fact]
	public async Task LoadAsync_RebuildsCorruptedSettingsFile_AndCreatesBackup()
	{
		var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

		try
		{
			Directory.CreateDirectory(tempDir);
			await File.WriteAllTextAsync(Path.Combine(tempDir, "settings.json"), "{ not valid json");

			var configuration = new ConfigurationBuilder()
				.AddInMemoryCollection(new Dictionary<string, string?>
				{
					["Settings:Directory"] = tempDir
				})
				.Build();

			var service = new SettingsService(configuration);
			await service.LoadAsync();

			var backups = Directory.GetFiles(tempDir, "settings.json.bad_*");

			Assert.Equal("FluentDark", service.Current.Theme);
			Assert.True(File.Exists(Path.Combine(tempDir, "settings.json")));
			Assert.NotEmpty(backups);
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
	public async Task LoadAsync_MigratesLegacyQuickBooksTokens_ToCanonicalQboValues()
	{
		var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

		try
		{
			Directory.CreateDirectory(tempDir);

			var legacySettings = new AppSettings
			{
				QuickBooksAccessToken = "legacy-access-token",
				QuickBooksRefreshToken = "legacy-refresh-token",
				QuickBooksTokenExpiresUtc = new DateTime(2026, 4, 7, 12, 0, 0, DateTimeKind.Utc)
			};

			await File.WriteAllTextAsync(
				Path.Combine(tempDir, "settings.json"),
				JsonSerializer.Serialize(legacySettings, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = null }));

			var configuration = new ConfigurationBuilder()
				.AddInMemoryCollection(new Dictionary<string, string?>
				{
					["Settings:Directory"] = tempDir
				})
				.Build();

			var service = new SettingsService(configuration);
			await service.LoadAsync();

			Assert.Equal("legacy-access-token", service.Current.QboAccessToken);
			Assert.Equal("legacy-refresh-token", service.Current.QboRefreshToken);
			Assert.Equal(legacySettings.QuickBooksTokenExpiresUtc, service.Current.QboTokenExpiry);
		}
		finally
		{
			if (Directory.Exists(tempDir))
			{
				Directory.Delete(tempDir, recursive: true);
			}
		}
	}
}
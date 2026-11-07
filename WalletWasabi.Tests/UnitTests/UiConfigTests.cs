using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using WalletWasabi.Fluent.Configuration;
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests;

/// <summary>
/// Tests for <see cref="UiConfig"/>.
/// </summary>
public class UiConfigTests
{
	[Theory]
	[InlineData(null)]
	[InlineData("{invalid json")]
	public async Task LoadFileCompletesDefaultFileWriteBeforeReturningAsync(string? existingContent)
	{
		var workDir = await Common.GetEmptyWorkDirAsync();
		var filePath = Path.Combine(workDir, "config.json");

		if (existingContent is not null)
		{
			File.WriteAllText(filePath, existingContent);
		}

		var config = UiConfig.LoadFile(filePath);

		using (var persisted = JsonDocument.Parse(File.ReadAllText(filePath)))
		{
			Assert.True(persisted.RootElement.GetProperty(nameof(UiConfig.Oobe)).GetBoolean());
			Assert.False(persisted.RootElement.GetProperty(nameof(UiConfig.RunOnSystemStartup)).GetBoolean());
		}

		// A subsequent save must not overlap the initial write or be overwritten by it.
		config.Oobe = false;
		config.ToFile();
		using var updated = JsonDocument.Parse(File.ReadAllText(filePath));
		Assert.False(updated.RootElement.GetProperty(nameof(UiConfig.Oobe)).GetBoolean());
	}

	/// <summary>
	/// Unknown and unsupported fiat currencies should fall back to the default fiat currency (USD) when loading the config from file.
	/// </summary>
	[Theory]
	[InlineData(null)]
	[InlineData("SEK")]
	public async Task FallBackToDefaultFiatCurrencyAsync(string? testedCurrency)
	{
		var workDir = await Common.GetEmptyWorkDirAsync();
		var filePath = Path.Combine(workDir, "config.json");

		// Store default config to file.
		new UiConfig(filePath).ToFile();

		// Modify the fiat currency in the file with the tested currency.
		{
			var json = File.ReadAllText(filePath);
			var jsonObject = JsonNode.Parse(json)!.AsObject();
			jsonObject["FiatCurrency"] = testedCurrency;
			File.WriteAllText(filePath, jsonObject.ToJsonString());
		}

		// Load the config from file and check that the fiat currency is USD.
		{
			var config = UiConfig.LoadFile(filePath);
			Assert.Equal(UiConstants.DefaultFiatCurrency, config.FiatCurrency);
		}
	}
}

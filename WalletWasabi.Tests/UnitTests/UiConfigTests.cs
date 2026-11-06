using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using WalletWasabi.Fluent.Configuration;
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests;

public class UiConfigTests
{
	[Theory]
	[InlineData(null)]
	[InlineData("{invalid json")]
	public void LoadFileCompletesDefaultFileWriteBeforeReturning(string? existingContent)
	{
		string workDir = Common.GetWorkDir();
		Directory.CreateDirectory(workDir);
		string filePath = Path.Combine(workDir, $"{Guid.NewGuid():N}.json");
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

	[Theory]
	[InlineData(null)]
	[InlineData("SEK")]
	public void FiatCurrencyIsUsdWhenTheFileHasNoneOrOneNobodyQuotes(string? fiatCurrencyInFile)
	{
		string workDir = Common.GetWorkDir();
		Directory.CreateDirectory(workDir);
		string filePath = Path.Combine(workDir, $"{Guid.NewGuid():N}.json");
		new UiConfig(filePath).ToFile();
		var json = JsonNode.Parse(File.ReadAllText(filePath))!.AsObject();
		json[nameof(UiConfig.FiatCurrency)] = fiatCurrencyInFile;
		File.WriteAllText(filePath, json.ToJsonString());

		var config = UiConfig.LoadFile(filePath);

		Assert.Equal(UiConstants.DefaultFiatCurrency, config.FiatCurrency);
	}

	[Fact]
	public void FiatCurrencyIsPersisted()
	{
		string workDir = Common.GetWorkDir();
		Directory.CreateDirectory(workDir);
		string filePath = Path.Combine(workDir, $"{Guid.NewGuid():N}.json");
		var config = new UiConfig(filePath) { FiatCurrency = "JPY" };

		config.ToFile();

		Assert.Equal("JPY", UiConfig.LoadFile(filePath).FiatCurrency);
	}
}

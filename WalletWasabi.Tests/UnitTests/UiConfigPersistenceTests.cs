using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using WalletWasabi.Fluent;
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests;

public class UiConfigPersistenceTests
{
	[Fact]
	public async Task CreationPublishesCompleteFileBeforeReturningAsync()
	{
		var directory = await Common.GetEmptyWorkDirAsync();
		for (var i = 0; i < 20; i++)
		{
			var path = Path.Combine(directory, "UiConfig" + i + ".json");
			_ = UiConfig.LoadFile(path);
			using var stream = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
			using var document = JsonDocument.Parse(stream);
			Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
		}
	}

	[Fact]
	public async Task ReadFailureDoesNotReplaceExistingSettingsAsync()
	{
		var path = Path.Combine(await Common.GetEmptyWorkDirAsync(), "UiConfig.json");
		CreateConfig(path, 410).ToFile();
		var original = File.ReadAllBytes(path);
		using (var stream = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
		{
			Assert.Throws<IOException>(() => UiConfig.LoadFile(path));
		}
		Assert.Equal(original, File.ReadAllBytes(path));
	}

	[Theory]
	[InlineData("{")]
	[InlineData("{}")]
	[InlineData("{\"LastVersionHighlightsDisplayed\":\"invalid-version\"}")]
	public async Task CorruptSettingsPublishUsableDefaultsAsync(string json)
	{
		var path = Path.Combine(await Common.GetEmptyWorkDirAsync(), "UiConfig.json");
		File.WriteAllText(path, json);
		var config = UiConfig.LoadFile(path);
		Assert.True(config.Oobe);
		Assert.Equal(new Version(2, 3, 1), config.LastVersionHighlightsDisplayed);
		await Task.Delay(TimeSpan.FromMilliseconds(1250));
		var reloaded = UiConfig.LoadFile(path);
		Assert.Equal(config.LastVersionHighlightsDisplayed, reloaded.LastVersionHighlightsDisplayed);
	}

	[Fact]
	public async Task ConcurrentInstancesAndReadersPreserveCompleteSettingsAsync()
	{
		var directory = await Common.GetEmptyWorkDirAsync();
		var path = Path.Combine(directory, "UiConfig.json");
		var first = CreateConfig(path, 410);
		var second = CreateConfig(path, 510);
		first.ToFile();
		await Task.WhenAll(
			Task.Run(() => { for (var i = 0; i < 100; i++) { first.ToFile(); } }),
			Task.Run(() => { for (var i = 0; i < 100; i++) { second.ToFile(); } }),
			Task.Run(() =>
			{
				for (var i = 0; i < 100; i++)
				{
					var loaded = UiConfig.LoadFile(path);
					Assert.Contains(loaded.WindowWidth, new double?[] { 410, 510 });
				}
			}));
	}

	private static UiConfig CreateConfig(string path, double width) => new(
		path,
		privacyMode: false,
		isCustomChangeAddress: false,
		autocopy: true,
		darkModeEnabled: true,
		lastSelectedWallet: null,
		windowState: "Normal",
		runOnSystemStartup: false,
		oobe: true,
		lastVersionHighlightsDisplayed: new Version(2, 3, 1),
		hideOnClose: false,
		autoPaste: false,
		feeTarget: 2,
		sendAmountConversionReversed: false,
		windowWidth: width,
		windowHeight: 600);
}

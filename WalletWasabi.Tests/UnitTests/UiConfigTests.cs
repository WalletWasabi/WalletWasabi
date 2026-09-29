using System.IO;
using System.Text.Json;
using WalletWasabi.Fluent;
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
}

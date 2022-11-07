using NBitcoin;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Services;
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Services;

public class WasabiSignerToolsTests
{
	private static Key PrivateKey { get; } = WasabiSignerTools.GeneratePrivateKey();
	private static DirectoryInfo InstallerFolder { get; } = CreateTestFolderWithFiles();
	private Task<string> ShaSumsFilePath => CreateShaSumsFileAsync(InstallerFolder);

	private static DirectoryInfo CreateTestFolderWithFiles()
	{
		var installerFolder = Directory.CreateDirectory(Path.Combine(Common.GetWorkDir(nameof(WasabiSignerToolsTests)), "installers"));
		string[] filenames = new[] { "Wasabi.msi", "Wasabi.deb", "Wasabi.dmg", "Wasabi.tar.gz" };
		string path;
		for (int i = 0; i < filenames.Length; i++)
		{
			path = Path.Combine(installerFolder.FullName, filenames[i]);
			if (!File.Exists(path))
			{
				File.WriteAllText(path, i.ToString());
			}
		}

		return installerFolder;
	}

	private static async Task<string> CreateShaSumsFileAsync(DirectoryInfo installerFolder)
	{
		StringBuilder fileContent = new();
		fileContent.AppendLine($"-----BEGIN PGP SIGNED MESSAGE-----");
		fileContent.AppendLine("Hash: SHA256");
		fileContent.AppendLine();
		foreach (FileInfo file in installerFolder.GetFiles())
		{
			uint256 fileHash = await WasabiSignerTools.GenerateHashFromFileAsync(file.FullName);
			fileContent.AppendLine($"{fileHash} {file.Name}");
		}

		fileContent.AppendLine($"-----BEGIN PGP SIGNATURE-----");
		fileContent.AppendLine();
		fileContent.AppendLine("iHUEARYIAB0WIQSzezSaXyNsjjmXathSPF4ghfWtAQUCYz7rVAAKCRBSPF4ghfWt\r\nAUjNAP0da7wUClzLL/MEAJ7UDfRJ9vSVuJ11KNqZj4yStWBzlAD+P+ZEUd3gCW3J\r\nR8y3yqiZplCIdDzmtToIr/48peW5SgM=\r\n=VsaF");
		fileContent.AppendLine($"-----END PGP SIGNATURE-----");
		string shaSumsFilePath = Path.Combine(installerFolder.Parent!.FullName, "SHASUMS.asc");
		await File.WriteAllTextAsync(shaSumsFilePath, fileContent.ToString()).ConfigureAwait(false);
		return shaSumsFilePath;
	}

	[Fact]
	public async void WritingAndVerifyingShaSumsTestAsync()
	{
		string signedShaSumsFilePath = Path.Combine(InstallerFolder.Parent!.FullName, WasabiSignerTools.ShaSumsFileName);
		string filePath = await ShaSumsFilePath;
		await WasabiSignerTools.SignAndSaveSHASumsFileAsync(filePath, signedShaSumsFilePath, PrivateKey).ConfigureAwait(false);
		Assert.True(File.Exists(signedShaSumsFilePath));

		PubKey publicKey = PrivateKey.PubKey;
		uint256 installerHash = await WasabiSignerTools.GetAndVerifyInstallerFromShaSumsFileAsync(signedShaSumsFilePath, "Wasabi.msi", publicKey);

		Assert.NotNull(installerHash);
	}

	[Fact]
	public async void CanSaveAndReadKeyFromFileTestAsync()
	{
		var tmpFolder = Path.Combine(InstallerFolder.FullName, "..");
		var tmpKeyFilePath = Path.Combine(tmpFolder, "WasabiKey.txt");
		File.Delete(tmpKeyFilePath);

		await WasabiSignerTools.SavePrivateKeyToFileAsync(tmpKeyFilePath, PrivateKey);
		Assert.True(File.Exists(tmpKeyFilePath));
		await Assert.ThrowsAsync<ArgumentException>(async () => await WasabiSignerTools.SavePrivateKeyToFileAsync(tmpKeyFilePath, PrivateKey)).ConfigureAwait(false);

		bool canReadKey = WasabiSignerTools.TryGetPrivateKeyFromFile(tmpKeyFilePath, out Key? key);
		Assert.True(canReadKey);
		Assert.NotNull(key);
	}
}

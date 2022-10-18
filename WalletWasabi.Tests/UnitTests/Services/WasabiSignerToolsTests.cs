using NBitcoin;
using NBitcoin.Crypto;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
		string destinationPath = Path.Combine(InstallerFolder.Parent!.FullName, WasabiSignerTools.ShaSumsFileName);
		await WasabiSignerTools.SignAndSaveSHASumsFileAsync(await ShaSumsFilePath, destinationPath, PrivateKey).ConfigureAwait(false);
		Assert.True(File.Exists(destinationPath));

		PubKey publicKey = PrivateKey.PubKey;
		(string content, Dictionary<string, uint256> installerFiles, string signature) = await WasabiSignerTools.ReadShaSumsContentAsync(destinationPath, publicKey).ConfigureAwait(false);
		Assert.NotEmpty(installerFiles);
		Assert.NotNull(content);
		Assert.NotNull(signature);
	}

	[Fact]
	public async void VerifyingShaSumsFileWithInvalidArgumentsFailsTestAsync()
	{
		string destinationPath = Path.Combine(InstallerFolder.Parent!.FullName, WasabiSignerTools.ShaSumsFileName);
		await WasabiSignerTools.SignAndSaveSHASumsFileAsync(await ShaSumsFilePath, destinationPath, PrivateKey).ConfigureAwait(false);
		Assert.True(File.Exists(destinationPath));
	}

	[Fact]
	public async void CanGenerateAndReadHashFromFileTestAsync()
	{
		string[] filepaths = InstallerFolder.GetFiles().Select(file => file.FullName).ToArray();
		PubKey publicKey = PrivateKey.PubKey;

		uint256 firstInstallerHash = await WasabiSignerTools.GenerateHashFromFileAsync(filepaths[0]).ConfigureAwait(false);
		uint256 secondInstallerHash = await WasabiSignerTools.GenerateHashFromFileAsync(filepaths[1]).ConfigureAwait(false);

		string shaSumsDestinationPath = Path.Combine(InstallerFolder.Parent!.FullName, WasabiSignerTools.ShaSumsFileName);
		await WasabiSignerTools.SignAndSaveSHASumsFileAsync(await ShaSumsFilePath, shaSumsDestinationPath, PrivateKey).ConfigureAwait(false);
		Assert.True(File.Exists(shaSumsDestinationPath));

		string firstInstallerFileName = filepaths[0].Split("\\").Last();
		string secondInstallerFileName = filepaths[1].Split("\\").Last();
		(string _, Dictionary<string, uint256> fileDictionary, string _) = await WasabiSignerTools.ReadShaSumsContentAsync(shaSumsDestinationPath, publicKey).ConfigureAwait(false);

		uint256 firstInstallerHashFromFile = fileDictionary[firstInstallerFileName];
		uint256 secondInstallerHashFromFile = fileDictionary[secondInstallerFileName];
		Assert.Equal(firstInstallerHash, firstInstallerHashFromFile);
		Assert.Equal(secondInstallerHash, secondInstallerHashFromFile);
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

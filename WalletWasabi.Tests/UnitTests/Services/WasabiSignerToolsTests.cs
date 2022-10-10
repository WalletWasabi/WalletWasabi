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
	private string ShaSumsFilePath { get; } = CreateShaSumsFile(InstallerFolder);

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

	private static string CreateShaSumsFile(DirectoryInfo installerFolder)
	{
		StringBuilder fileContent = new();
		fileContent.AppendLine($"-----BEGIN PGP SIGNED MESSAGE-----");
		fileContent.AppendLine("Hash: SHA256");
		fileContent.AppendLine();
		foreach (FileInfo file in installerFolder.GetFiles())
		{
			uint256 fileHash = WasabiSignerTools.GenerateHashFromFile(file.FullName);
			fileContent.AppendLine($"{fileHash} {file.Name}");
		}

		fileContent.AppendLine($"-----BEGIN PGP SIGNATURE-----");
		fileContent.AppendLine();
		fileContent.AppendLine("iHUEARYIAB0WIQSzezSaXyNsjjmXathSPF4ghfWtAQUCYz7rVAAKCRBSPF4ghfWt\r\nAUjNAP0da7wUClzLL/MEAJ7UDfRJ9vSVuJ11KNqZj4yStWBzlAD+P+ZEUd3gCW3J\r\nR8y3yqiZplCIdDzmtToIr/48peW5SgM=\r\n=VsaF");
		fileContent.AppendLine($"-----END PGP SIGNATURE-----");
		string shaSumsFilePath = Path.Combine(installerFolder.Parent!.FullName, "SHASUMS.asc");
		File.WriteAllText(shaSumsFilePath, fileContent.ToString());
		return shaSumsFilePath;
	}

	[Fact]
	public void WritingAndVerifyingShaSumsTest()
	{
		string destinationPath = Path.Combine(InstallerFolder.Parent!.FullName, WasabiSignerTools.ShaSumsFileName);
		WasabiSignerTools.SignAndSaveSHASumsFile(ShaSumsFilePath, destinationPath, PrivateKey);
		Assert.True(File.Exists(destinationPath));

		PubKey publicKey = PrivateKey.PubKey;
		(string content, Dictionary<string, uint256> installerFiles, string signature) = WasabiSignerTools.ReadShaSumsContent(destinationPath, publicKey);
		Assert.NotEmpty(installerFiles);
		Assert.NotNull(content);
		Assert.NotNull(signature);
	}

	[Fact]
	public void VerifyingShaSumsFileWithInvalidArgumentsFailsTest()
	{
		string destinationPath = Path.Combine(InstallerFolder.Parent!.FullName, WasabiSignerTools.ShaSumsFileName);
		WasabiSignerTools.SignAndSaveSHASumsFile(ShaSumsFilePath, destinationPath, PrivateKey);
		Assert.True(File.Exists(destinationPath));
	}

	[Fact]
	public void CanGenerateAndReadHashFromFileTest()
	{
		string[] filepaths = InstallerFolder.GetFiles().Select(file => file.FullName).ToArray();
		PubKey publicKey = PrivateKey.PubKey;

		uint256 firstInstallerHash = WasabiSignerTools.GenerateHashFromFile(filepaths[0]);
		uint256 secondInstallerHash = WasabiSignerTools.GenerateHashFromFile(filepaths[1]);

		string shaSumsDestinationPath = Path.Combine(InstallerFolder.Parent!.FullName, WasabiSignerTools.ShaSumsFileName);
		WasabiSignerTools.SignAndSaveSHASumsFile(ShaSumsFilePath, shaSumsDestinationPath, PrivateKey);
		Assert.True(File.Exists(shaSumsDestinationPath));

		string firstInstallerFileName = filepaths[0].Split("\\").Last();
		string secondInstallerFileName = filepaths[1].Split("\\").Last();
		(string _, Dictionary<string, uint256> fileDictionary, string _) = WasabiSignerTools.ReadShaSumsContent(shaSumsDestinationPath, publicKey);

		uint256 firstInstallerHashFromFile = fileDictionary[firstInstallerFileName];
		uint256 secondInstallerHashFromFile = fileDictionary[secondInstallerFileName];
		Assert.Equal(firstInstallerHash, firstInstallerHashFromFile);
		Assert.Equal(secondInstallerHash, secondInstallerHashFromFile);
	}

	[Fact]
	public void CanSaveAndReadKeyFromFileTest()
	{
		var tmpFolder = Path.Combine(InstallerFolder.FullName, "..");
		var tmpKeyFilePath = Path.Combine(tmpFolder, "WasabiKey.txt");
		File.Delete(tmpKeyFilePath);

		WasabiSignerTools.SavePrivateKeyToFile(tmpKeyFilePath, PrivateKey);
		Assert.True(File.Exists(tmpKeyFilePath));
		Assert.Throws<ArgumentException>(() => WasabiSignerTools.SavePrivateKeyToFile(tmpKeyFilePath, PrivateKey));

		bool canReadKey = WasabiSignerTools.TryGetPrivateKeyFromFile(tmpKeyFilePath, out Key? key);
		Assert.True(canReadKey);
		Assert.NotNull(key);
	}
}

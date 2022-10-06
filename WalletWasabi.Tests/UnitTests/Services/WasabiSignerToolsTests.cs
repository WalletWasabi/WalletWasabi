using NBitcoin;
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
	private Key PrivateKey { get; } = WasabiSignerTools.GeneratePrivateKey();
	private DirectoryInfo InstallerFolder { get; } = CreateTestFolderWithFiles();

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

	[Fact]
	public void WritingAndVerifyingShaSumsTest()
	{
		string[] filepaths = InstallerFolder.GetFiles().Select(file => file.FullName).ToArray();
		string destinationPath = Path.Combine(InstallerFolder.Parent!.FullName, WasabiSignerTools.ShaSumsFileName);
		WasabiSignerTools.SignAndSaveSHASumsFile(filepaths, destinationPath, PrivateKey);
		Assert.True(File.Exists(destinationPath));

		PubKey publicKey = PrivateKey.PubKey;
		bool isSignatureValid = WasabiSignerTools.VerifyShaSumsFile(destinationPath, publicKey);
		Assert.True(isSignatureValid);
	}

	[Fact]
	public void WritingShaSumsThrowsErrorWithWrongArgumentTest()
	{
		string[] invalidFilePaths = new[] { "notAValidFilePath" };
		string destinationPath = Path.Combine(InstallerFolder.Parent!.FullName, WasabiSignerTools.ShaSumsFileName);
		Assert.Throws<FileNotFoundException>(() => WasabiSignerTools.SignAndSaveSHASumsFile(invalidFilePaths, destinationPath, PrivateKey));
	}

	[Fact]
	public void VerifyingShaSumsFileWithInvalidArgumentsFailsTest()
	{
		string[] filepaths = InstallerFolder.GetFiles().Select(file => file.FullName).ToArray();
		string destinationPath = Path.Combine(InstallerFolder.Parent!.FullName, WasabiSignerTools.ShaSumsFileName);
		WasabiSignerTools.SignAndSaveSHASumsFile(filepaths, destinationPath, PrivateKey);
		Assert.True(File.Exists(destinationPath));

		PubKey goodPublicKey = PrivateKey.PubKey;
		PubKey wrongPublicKey = WasabiSignerTools.GeneratePrivateKey().PubKey;

		bool withWrongKey = WasabiSignerTools.VerifyShaSumsFile(destinationPath, wrongPublicKey);
		Assert.False(withWrongKey);
		bool withWrongFile = WasabiSignerTools.VerifyShaSumsFile(filepaths.First(), goodPublicKey);
		Assert.False(withWrongFile);
	}

	[Fact]
	public void CanGenerateAndReadHashFromFileTest()
	{
		string[] filepaths = InstallerFolder.GetFiles().Select(file => file.FullName).ToArray();

		uint256 firstInstallerHash = WasabiSignerTools.GenerateHashFromFile(filepaths[0]);
		uint256 secondInstallerHash = WasabiSignerTools.GenerateHashFromFile(filepaths[1]);

		string shaSumsDestinationPath = Path.Combine(InstallerFolder.Parent!.FullName, WasabiSignerTools.ShaSumsFileName);
		WasabiSignerTools.SignAndSaveSHASumsFile(filepaths, shaSumsDestinationPath, PrivateKey);
		Assert.True(File.Exists(shaSumsDestinationPath));

		string firstInstallerFileName = filepaths[0].Split("\\").Last();
		string secondInstallerFileName = filepaths[1].Split("\\").Last();
		(string _, Dictionary<string, uint256> fileDictionary, string _) = WasabiSignerTools.ReadShaSumsContent(shaSumsDestinationPath);

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

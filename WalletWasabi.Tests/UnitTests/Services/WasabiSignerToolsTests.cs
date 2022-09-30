using NBitcoin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Services;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Services;

public class WasabiSignerToolsTests
{
	private Key _privateKey = WasabiSignerTools.GenerateKey();
	private DirectoryInfo _installerFolder = CreateTestFolderWithFiles();

	private static DirectoryInfo CreateTestFolderWithFiles()
	{
		var installerFolder = Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "tmp", "installers"));
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
	public void WritingAndVerifyingSHASumsTest()
	{
		string[] filepaths = _installerFolder.GetFiles().Select(file => file.FullName).ToArray();
		string destinationPath = Path.Combine(_installerFolder.Parent!.FullName, WasabiSignerTools.SHASumsFileName);
		WasabiSignerTools.SignAndSaveSHASumsFile(filepaths, destinationPath, _privateKey);
		Assert.True(File.Exists(destinationPath));

		PubKey publicKey = _privateKey.PubKey;
		bool isSignatureValid = WasabiSignerTools.VerifySHASumsFile(destinationPath, publicKey);
		Assert.True(isSignatureValid);
	}

	[Fact]
	public void WritingSHASumsThrowsErrorWithWrongArgumentTest()
	{
		string[] invalidFilePaths = new[] { "notAValidFilePath" };
		string destinationPath = Path.Combine(_installerFolder.Parent!.FullName, WasabiSignerTools.SHASumsFileName);
		Assert.Throws<FileNotFoundException>(() => WasabiSignerTools.SignAndSaveSHASumsFile(invalidFilePaths, destinationPath, _privateKey));
	}

	[Fact]
	public void VerifyingSUMSFileWithInvalidArgumentsFailsTest()
	{
		string[] filepaths = _installerFolder.GetFiles().Select(file => file.FullName).ToArray();
		string destinationPath = Path.Combine(_installerFolder.Parent!.FullName, WasabiSignerTools.SHASumsFileName);
		WasabiSignerTools.SignAndSaveSHASumsFile(filepaths, destinationPath, _privateKey);
		Assert.True(File.Exists(destinationPath));

		PubKey goodPublicKey = _privateKey.PubKey;
		PubKey wrongPublicKey = WasabiSignerTools.GenerateKey().PubKey;

		bool withWrongKey = WasabiSignerTools.VerifySHASumsFile(destinationPath, wrongPublicKey);
		Assert.False(withWrongKey);
		bool withWrongFile = WasabiSignerTools.VerifySHASumsFile(filepaths.First(), goodPublicKey);
		Assert.False(withWrongFile);
	}

	[Fact]
	public void CanGenerateReadHashFromFileTest()
	{
		string[] filepaths = _installerFolder.GetFiles().Select(file => file.FullName).ToArray();

		uint256 fileHash = WasabiSignerTools.GenerateHashFromFile(filepaths.First());
		uint256 otherFileHash = WasabiSignerTools.GenerateHashFromFile(filepaths.ElementAt(1));

		string destinationPath = Path.Combine(_installerFolder.Parent!.FullName, WasabiSignerTools.SHASumsFileName);
		WasabiSignerTools.SignAndSaveSHASumsFile(filepaths, destinationPath, _privateKey);
		Assert.True(File.Exists(destinationPath));

		uint256 sameFileHash = WasabiSignerTools.ReadHashFromFile(destinationPath, filepaths.First().Split("\\").Last());
		uint256 otherFileSameHash = WasabiSignerTools.ReadHashFromFile(destinationPath, filepaths.ElementAt(1).Split("\\").Last());
		Assert.Equal(fileHash, sameFileHash);
		Assert.Equal(otherFileHash, otherFileSameHash);
	}

	[Fact]
	public void CanSaveAndReadKeyFromFileTest()
	{
		var tmpFolder = Path.Combine(_installerFolder.FullName, "..");
		var keyFilePath = Path.Combine(tmpFolder, "WasabiKey.txt");

		WasabiSignerTools.SavePrivateKeyToFile(keyFilePath, _privateKey);
		Assert.True(File.Exists(keyFilePath));
		Assert.Throws<ArgumentException>(() => WasabiSignerTools.SavePrivateKeyToFile(keyFilePath, _privateKey));

		bool canReadKey = WasabiSignerTools.TryGetKeyFromFile(keyFilePath, out Key? key);
		Assert.True(canReadKey);
		Assert.NotNull(key);
		File.Delete(keyFilePath);
	}
}

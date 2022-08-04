using NBitcoin;
using System.IO;
using System.Linq;
using System.Security;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Logging;
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests;

public class KeyManagementTests
{
	[Fact]
	public void CanCreateNew()
	{
		string password = "password";
		var manager = KeyManager.CreateNew(out Mnemonic mnemonic, password, Network.Main);
		var manager2 = KeyManager.CreateNew(out Mnemonic mnemonic2, "", Network.Main);
		var manager3 = KeyManager.CreateNew(out _, "P@ssw0rdé", Network.Main);

		Assert.Equal(12, mnemonic.ToString().Split(' ').Length);
		Assert.Equal(12, mnemonic2.ToString().Split(' ').Length);
		Assert.Equal(12, mnemonic2.ToString().Split(' ').Length);

		Assert.NotNull(manager.ChainCode);
		Assert.NotNull(manager.EncryptedSecret);
		Assert.NotNull(manager.ExtPubKey);

		Assert.NotNull(manager2.ChainCode);
		Assert.NotNull(manager2.EncryptedSecret);
		Assert.NotNull(manager2.ExtPubKey);

		Assert.NotNull(manager3.ChainCode);
		Assert.NotNull(manager3.EncryptedSecret);
		Assert.NotNull(manager3.ExtPubKey);

		var sameManager = new KeyManager(manager.EncryptedSecret, manager.ChainCode, manager.MasterFingerprint, manager.ExtPubKey, true, null, new BlockchainState(Network.Main));
		var sameManager2 = new KeyManager(manager.EncryptedSecret, manager.ChainCode, password, Network.Main);
		Logger.TurnOff();
		Assert.Throws<SecurityException>(() => new KeyManager(manager.EncryptedSecret, manager.ChainCode, "differentPassword", Network.Main));
		Logger.TurnOn();

		Assert.Equal(manager.ChainCode, sameManager.ChainCode);
		Assert.Equal(manager.EncryptedSecret, sameManager.EncryptedSecret);
		Assert.Equal(manager.ExtPubKey, sameManager.ExtPubKey);

		Assert.Equal(manager.ChainCode, sameManager2.ChainCode);
		Assert.Equal(manager.EncryptedSecret, sameManager2.EncryptedSecret);
		Assert.Equal(manager.ExtPubKey, sameManager2.ExtPubKey);

		var differentManager = KeyManager.CreateNew(out Mnemonic mnemonic4, password, Network.Main);
		Assert.NotEqual(mnemonic, mnemonic4);
		Assert.NotEqual(manager.ChainCode, differentManager.ChainCode);
		Assert.NotEqual(manager.EncryptedSecret, differentManager.EncryptedSecret);
		Assert.NotEqual(manager.ExtPubKey, differentManager.ExtPubKey);

		var manager5 = new KeyManager(manager2.EncryptedSecret, manager2.ChainCode, password: null!, Network.Main);
		Assert.Equal(manager2.ChainCode, manager5.ChainCode);
		Assert.Equal(manager2.EncryptedSecret, manager5.EncryptedSecret);
		Assert.Equal(manager2.ExtPubKey, manager5.ExtPubKey);
	}

	[Fact]
	public void CanRecover()
	{
		string password = "password";
		var manager = KeyManager.CreateNew(out Mnemonic mnemonic, password, Network.Main);
		var sameManager = KeyManager.Recover(mnemonic, password, Network.Main, KeyManager.GetAccountKeyPath(Network.Main));

		Assert.Equal(manager.ChainCode, sameManager.ChainCode);
		Assert.Equal(manager.EncryptedSecret, sameManager.EncryptedSecret);
		Assert.Equal(manager.ExtPubKey, sameManager.ExtPubKey);

		var differentManager = KeyManager.Recover(mnemonic, "differentPassword", Network.Main, KeyPath.Parse("m/999'/999'/999'"), null, 55);
		Assert.NotEqual(manager.ChainCode, differentManager.ChainCode);
		Assert.NotEqual(manager.EncryptedSecret, differentManager.EncryptedSecret);
		Assert.NotEqual(manager.ExtPubKey, differentManager.ExtPubKey);

		differentManager.AssertCleanKeysIndexed();
		var newKey = differentManager.GenerateNewKey("some-label", KeyState.Clean, true, false);
		Assert.Equal(newKey.Index, differentManager.MinGapLimit);
		Assert.Equal("999'/999'/999'/1/55", newKey.FullKeyPath.ToString());
	}

	[Fact]
	public void CanHandleGap()
	{
		string password = "password";
		var manager = KeyManager.CreateNew(out _, password, Network.Main);

		manager.AssertCleanKeysIndexed();
		var lastKey = manager.GetKeys(KeyState.Clean, isInternal: false).Last();
		lastKey.SetKeyState(KeyState.Used);
		var newKeys = manager.AssertCleanKeysIndexed();
		Assert.Equal(manager.MinGapLimit, newKeys.Count());
	}

	[Fact]
	public void CanSerialize()
	{
		string password = "password";

		var filePath = Path.Combine(Common.GetWorkDir(), "Wallet.json");
		DeleteFileAndDirectoryIfExists(filePath);

		Logger.TurnOff();
		Assert.Throws<FileNotFoundException>(() => KeyManager.FromFile(filePath));
		Logger.TurnOn();

		var manager = KeyManager.CreateNew(out _, password, Network.Main, filePath);
		KeyManager.FromFile(filePath);

		manager.ToFile();

		manager.ToFile(); // assert it does not throw

		for (int i = 0; i < 1000; i++)
		{
			var isInternal = Random.Shared.Next(2) == 0;
			var label = RandomString.AlphaNumeric(21);
			var keyState = (KeyState)Random.Shared.Next(3);
			manager.GenerateNewKey(label, keyState, isInternal, toFile: false);
		}
		manager.ToFile();

		Assert.True(File.Exists(filePath));

		var sameManager = KeyManager.FromFile(filePath);

		Assert.Equal(manager.ChainCode, sameManager.ChainCode);
		Assert.Equal(manager.EncryptedSecret, sameManager.EncryptedSecret);
		Assert.Equal(manager.ExtPubKey, sameManager.ExtPubKey);

		DeleteFileAndDirectoryIfExists(filePath);
	}

	[Fact]
	public void CanGenerateKeys()
	{
		string password = "password";
		var network = Network.Main;
		var manager = KeyManager.CreateNew(out _, password, network);

		var k1 = manager.GenerateNewKey(SmartLabel.Empty, KeyState.Clean, true);
		Assert.Equal(SmartLabel.Empty, k1.Label);

		for (int i = 0; i < 1000; i++)
		{
			var isInternal = Random.Shared.Next(2) == 0;
			var label = RandomString.AlphaNumeric(21);
			var keyState = (KeyState)Random.Shared.Next(3);
			var generatedKey = manager.GenerateNewKey(label, keyState, isInternal);

			Assert.Equal(isInternal, generatedKey.IsInternal);
			Assert.Equal(label, generatedKey.Label);
			Assert.Equal(keyState, generatedKey.KeyState);
			Assert.StartsWith(KeyManager.GetAccountKeyPath(network).ToString(), generatedKey.FullKeyPath.ToString());
		}
	}

	[Fact]
	public void GapCountingTests()
	{
		var km = KeyManager.CreateNew(out _, "", Network.Main);
		Assert.Equal(0, km.CountConsecutiveUnusedKeys(true, ignoreTail: false));
		Assert.Equal(0, km.CountConsecutiveUnusedKeys(false, ignoreTail: false));

		var k = km.GenerateNewKey("", KeyState.Clean, true);
		Assert.Equal(1, km.CountConsecutiveUnusedKeys(true, ignoreTail: false));

		km.GenerateNewKey("", KeyState.Locked, true);
		Assert.Equal(2, km.CountConsecutiveUnusedKeys(true, ignoreTail: false));

		k.SetKeyState(KeyState.Used);
		Assert.Equal(1, km.CountConsecutiveUnusedKeys(true, ignoreTail: false));

		for (int i = 0; i < 100; i++)
		{
			var k2 = km.GenerateNewKey("", KeyState.Clean, true);
			if (i == 50)
			{
				k = k2;
			}
		}
		Assert.Equal(101, km.CountConsecutiveUnusedKeys(true, ignoreTail: false));
		k.SetKeyState(KeyState.Locked);
		Assert.Equal(101, km.CountConsecutiveUnusedKeys(true, ignoreTail: false));
		k.SetKeyState(KeyState.Used);
		Assert.Equal(51, km.CountConsecutiveUnusedKeys(true, ignoreTail: false));
	}

	private static void DeleteFileAndDirectoryIfExists(string filePath)
	{
		var dir = Path.GetDirectoryName(filePath);

		if (File.Exists(filePath))
		{
			File.Delete(filePath);
		}

		if (dir is not null && Directory.Exists(dir))
		{
			if (Directory.GetFiles(dir).Length == 0)
			{
				Directory.Delete(dir);
			}
		}
	}
}

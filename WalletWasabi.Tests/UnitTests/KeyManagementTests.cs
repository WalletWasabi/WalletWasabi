using NBitcoin;
using System;
using System.IO;
using System.Security;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Tests.XunitConfiguration;
using Xunit;

namespace WalletWasabi.Tests.UnitTests
{
	public class KeyManagementTests
	{
		[Fact]
		public void CanCreateNew()
		{
			string password = "password";
			var manager = KeyManager.CreateNew(out Mnemonic mnemonic, password);
			var manager2 = KeyManager.CreateNew(out Mnemonic mnemonic2, "");
			var manager3 = KeyManager.CreateNew(out _, "P@ssw0rdé");

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

			var sameManager = new KeyManager(manager.EncryptedSecret, manager.ChainCode, manager.MasterFingerprint, manager.ExtPubKey, true, null, new BlockchainState());
			var sameManager2 = new KeyManager(manager.EncryptedSecret, manager.ChainCode, password);
			Logger.TurnOff();
			Assert.Throws<SecurityException>(() => new KeyManager(manager.EncryptedSecret, manager.ChainCode, "differentPassword"));
			Logger.TurnOn();

			Assert.Equal(manager.ChainCode, sameManager.ChainCode);
			Assert.Equal(manager.EncryptedSecret, sameManager.EncryptedSecret);
			Assert.Equal(manager.ExtPubKey, sameManager.ExtPubKey);

			Assert.Equal(manager.ChainCode, sameManager2.ChainCode);
			Assert.Equal(manager.EncryptedSecret, sameManager2.EncryptedSecret);
			Assert.Equal(manager.ExtPubKey, sameManager2.ExtPubKey);

			var differentManager = KeyManager.CreateNew(out Mnemonic mnemonic4, password);
			Assert.NotEqual(mnemonic, mnemonic4);
			Assert.NotEqual(manager.ChainCode, differentManager.ChainCode);
			Assert.NotEqual(manager.EncryptedSecret, differentManager.EncryptedSecret);
			Assert.NotEqual(manager.ExtPubKey, differentManager.ExtPubKey);

			var manager5 = new KeyManager(manager2.EncryptedSecret, manager2.ChainCode, password: null);
			Assert.Equal(manager2.ChainCode, manager5.ChainCode);
			Assert.Equal(manager2.EncryptedSecret, manager5.EncryptedSecret);
			Assert.Equal(manager2.ExtPubKey, manager5.ExtPubKey);
		}

		[Fact]
		public void CanRecover()
		{
			string password = "password";
			var manager = KeyManager.CreateNew(out Mnemonic mnemonic, password);
			var sameManager = KeyManager.Recover(mnemonic, password);

			Assert.Equal(manager.ChainCode, sameManager.ChainCode);
			Assert.Equal(manager.EncryptedSecret, sameManager.EncryptedSecret);
			Assert.Equal(manager.ExtPubKey, sameManager.ExtPubKey);

			var differentManager = KeyManager.Recover(mnemonic, "differentPassword", null, KeyPath.Parse("m/999'/999'/999'"), 55);
			Assert.NotEqual(manager.ChainCode, differentManager.ChainCode);
			Assert.NotEqual(manager.EncryptedSecret, differentManager.EncryptedSecret);
			Assert.NotEqual(manager.ExtPubKey, differentManager.ExtPubKey);

			differentManager.AssertCleanKeysIndexed();
			var newKey = differentManager.GenerateNewKey("some-label", KeyState.Clean, true, false);
			Assert.Equal(newKey.Index, differentManager.MinGapLimit);
			Assert.Equal("999'/999'/999'/1/55", newKey.FullKeyPath.ToString());
		}

		[Fact]
		public void CanSerialize()
		{
			string password = "password";

			var filePath = Path.Combine(Global.Instance.DataDir, EnvironmentHelpers.GetCallerFileName(), EnvironmentHelpers.GetMethodName(), "Wallet.json");
			DeleteFileAndDirectoryIfExists(filePath);

			Logger.TurnOff();
			Assert.Throws<FileNotFoundException>(() => KeyManager.FromFile(filePath));
			Logger.TurnOn();

			var manager = KeyManager.CreateNew(out _, password, filePath);
			KeyManager.FromFile(filePath);

			manager.ToFile();

			manager.ToFile(); // assert it does not throw

			var random = new Random();

			for (int i = 0; i < 1000; i++)
			{
				var isInternal = random.Next(2) == 0;
				var label = RandomString.Generate(21);
				var keyState = (KeyState)random.Next(3);
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
			var manager = KeyManager.CreateNew(out _, password);

			var random = new Random();

			var k1 = manager.GenerateNewKey(SmartLabel.Empty, KeyState.Clean, true);
			var k2 = manager.GenerateNewKey(null, KeyState.Clean, true);
			Assert.Equal(SmartLabel.Empty, k1.Label);
			Assert.Equal(SmartLabel.Empty, k2.Label);

			for (int i = 0; i < 1000; i++)
			{
				var isInternal = random.Next(2) == 0;
				var label = RandomString.Generate(21);
				var keyState = (KeyState)random.Next(3);
				var generatedKey = manager.GenerateNewKey(label, keyState, isInternal);

				Assert.Equal(isInternal, generatedKey.IsInternal);
				Assert.Equal(label, generatedKey.Label);
				Assert.Equal(keyState, generatedKey.KeyState);
				Assert.StartsWith(KeyManager.DefaultAccountKeyPath.ToString(), generatedKey.FullKeyPath.ToString());
			}
		}

		private static void DeleteFileAndDirectoryIfExists(string filePath)
		{
			var dir = Path.GetDirectoryName(filePath);

			if (File.Exists(filePath))
			{
				File.Delete(filePath);
			}

			if (Directory.Exists(dir))
			{
				if (Directory.GetFiles(dir).Length == 0)
				{
					Directory.Delete(dir);
				}
			}
		}
	}
}

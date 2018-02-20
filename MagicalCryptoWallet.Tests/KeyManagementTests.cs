using MagicalCryptoWallet.KeyManagement;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Text;
using Xunit;

namespace MagicalCryptoWallet.Tests
{
	public class KeyManagementTests : IClassFixture<SharedFixture>
	{
		private SharedFixture SharedFixture { get; }

		public KeyManagementTests(SharedFixture fixture)
		{
			SharedFixture = fixture;
		}

		[Fact]
		public void CanCreateNew()
		{
			string password = "password";
			var manager = KeyManager.CreateNew(out Mnemonic mnemonic, password);
			var manager2 = KeyManager.CreateNew(out Mnemonic mnemonic2, "");
			var manager3 = KeyManager.CreateNew(out Mnemonic mnemonic3, "P@ssw0rdé");

			Assert.Equal(12, mnemonic.ToString().Split(' ').Length);
			Assert.Equal(12, mnemonic2.ToString().Split(' ').Length);
			Assert.Equal(12, mnemonic2.ToString().Split(' ').Length);

			Assert.NotNull(manager.ChainCode);
			Assert.NotNull(manager.EncryptedSecret);
			Assert.NotNull(manager.MasterPubKey);
			Assert.NotNull(manager.ExtPubKey);

			Assert.NotNull(manager2.ChainCode);
			Assert.NotNull(manager2.EncryptedSecret);
			Assert.NotNull(manager2.MasterPubKey);
			Assert.NotNull(manager2.ExtPubKey);

			Assert.NotNull(manager3.ChainCode);
			Assert.NotNull(manager3.EncryptedSecret);
			Assert.NotNull(manager3.MasterPubKey);
			Assert.NotNull(manager3.ExtPubKey);

			var sameManager = new KeyManager(manager.EncryptedSecret, manager.ChainCode, manager.MasterPubKey);
			var sameManager2 = new KeyManager(manager.EncryptedSecret, manager.ChainCode, password);
			Assert.Throws<SecurityException>(() => new KeyManager(manager.EncryptedSecret, manager.ChainCode, "differentPassword"));

			Assert.Equal(manager.ChainCode, sameManager.ChainCode);
			Assert.Equal(manager.EncryptedSecret, sameManager.EncryptedSecret);
			Assert.Equal(manager.MasterPubKey, sameManager.MasterPubKey);
			Assert.Equal(manager.ExtPubKey, sameManager.ExtPubKey);

			Assert.Equal(manager.ChainCode, sameManager2.ChainCode);
			Assert.Equal(manager.EncryptedSecret, sameManager2.EncryptedSecret);
			Assert.Equal(manager.MasterPubKey, sameManager2.MasterPubKey);
			Assert.Equal(manager.ExtPubKey, sameManager2.ExtPubKey);

			var differentManager = KeyManager.CreateNew(out Mnemonic mnemonic4, password);
			Assert.NotEqual(mnemonic, mnemonic4);
			Assert.NotEqual(manager.ChainCode, differentManager.ChainCode);
			Assert.NotEqual(manager.EncryptedSecret, differentManager.EncryptedSecret);
			Assert.NotEqual(manager.MasterPubKey, differentManager.MasterPubKey);
			Assert.NotEqual(manager.ExtPubKey, differentManager.ExtPubKey);

			var manager5 = new KeyManager(manager2.EncryptedSecret, manager2.ChainCode, password: null);
			Assert.Equal(manager2.ChainCode, manager5.ChainCode);
			Assert.Equal(manager2.EncryptedSecret, manager5.EncryptedSecret);
			Assert.Equal(manager2.MasterPubKey, manager5.MasterPubKey);
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
			Assert.Equal(manager.MasterPubKey, sameManager.MasterPubKey);
			Assert.Equal(manager.ExtPubKey, sameManager.ExtPubKey);

			var differentManager = KeyManager.Recover(mnemonic, "differentPassword");
			Assert.NotEqual(manager.ChainCode, differentManager.ChainCode);
			Assert.NotEqual(manager.EncryptedSecret, differentManager.EncryptedSecret);
			Assert.NotEqual(manager.MasterPubKey, differentManager.MasterPubKey);
			Assert.NotEqual(manager.ExtPubKey, differentManager.ExtPubKey);
		}

		[Fact]
		public void CanSerialize()
		{
			string password = "password";
			var manager = KeyManager.CreateNew(out Mnemonic mnemonic, password);

			var filePath = "WalletDir/Wallet.json";
			DeleteFileAndDirectoryIfExists(filePath);

			Assert.Throws<FileNotFoundException>(() => KeyManager.FromFile(filePath));
			
			manager.ToFile(filePath);

			manager.ToFile(filePath); // assert it doesn't throw

			var random = new Random();

			for (int i = 0; i < 1000; i++)
			{
				var isInternal = random.Next(2) == 0;
				var label = RandomString.Generate(21);
				var keyState = (KeyState)random.Next(3);
				manager.GenerateNewKey(label, keyState, isInternal);
			}
			manager.ToFile(filePath);

			Assert.True(File.Exists(filePath));			

			var sameManager = KeyManager.FromFile(filePath);

			Assert.Equal(manager.ChainCode, sameManager.ChainCode);
			Assert.Equal(manager.EncryptedSecret, sameManager.EncryptedSecret);
			Assert.Equal(manager.MasterPubKey, sameManager.MasterPubKey);
			Assert.Equal(manager.ExtPubKey, sameManager.ExtPubKey);

			DeleteFileAndDirectoryIfExists(filePath);
		}

		[Fact]
		public void CanGenerateKeys()
		{
			string password = "password";
			var manager = KeyManager.CreateNew(out Mnemonic mnemonic, password);

			var random = new Random();

			var k1 = manager.GenerateNewKey("", KeyState.Clean, true);
			var k2 = manager.GenerateNewKey(null, KeyState.Clean, true);
			Assert.Equal("", k1.Label);
			Assert.Equal("", k2.Label);

			for (int i = 0; i < 1000; i++)
			{
				var isInternal = random.Next(2) == 0;
				var label = RandomString.Generate(21);
				var keyState = (KeyState)random.Next(3);
				var generatedKey = manager.GenerateNewKey(label, keyState, isInternal);

				Assert.Equal(isInternal, generatedKey.IsInternal());
				Assert.Equal(label, generatedKey.Label);
				Assert.Equal(keyState, generatedKey.KeyState);
				Assert.StartsWith("44'/0'/0'", generatedKey.FullKeyPath.ToString());
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
				if(Directory.GetFiles(dir).Length == 0)
				{
					Directory.Delete(dir);
				}
			}
		}
	}
}

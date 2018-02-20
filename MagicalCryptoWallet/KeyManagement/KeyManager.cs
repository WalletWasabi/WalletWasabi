using MagicalCryptoWallet.Converters;
using MagicalCryptoWallet.Helpers;
using MagicalCryptoWallet.JsonConverters;
using NBitcoin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MagicalCryptoWallet.KeyManagement
{
	[JsonObject(MemberSerialization.OptIn)]
	public class KeyManager
    {
		[JsonProperty(Order = 1)]
		[JsonConverter(typeof(BitcoinEncryptedSecretNoECConverter))]
		public BitcoinEncryptedSecretNoEC EncryptedSecret { get; }

		[JsonProperty(Order = 2)]
		[JsonConverter(typeof(ByteArrayConverter))]
		public byte[] ChainCode { get; }

		[JsonProperty(Order = 3)]
		[JsonConverter(typeof(PubKeyConverter))]
		public PubKey MasterPubKey { get; }

		[JsonProperty(Order = 4)]
		private List<HdPubKey> HdPubKeys { get; }

		// BIP44-ish derivation scheme
		// m / purpose' / coin_type' / account' / change / address_index
		private static readonly KeyPath AccountKeyPath = new KeyPath("m/44'/0'/0'");

		private readonly object HdPubKeysLock;

		private ExtPubKey _extPubKey;
		public ExtPubKey ExtPubKey
		{
			get
			{
				if(_extPubKey == null)
				{
					_extPubKey = new ExtPubKey(MasterPubKey, ChainCode);
				}
				return _extPubKey;
			}
		}

		[JsonConstructor]
		public KeyManager(BitcoinEncryptedSecretNoEC encryptedSecret, byte[] chainCode, PubKey masterPubKey)
		{
			_extPubKey = null;
			HdPubKeys = new List<HdPubKey>();
			HdPubKeysLock = new object();

			EncryptedSecret = Guard.NotNull(nameof(encryptedSecret), encryptedSecret);
			ChainCode = Guard.NotNull(nameof(chainCode), chainCode);
			MasterPubKey = Guard.NotNull(nameof(masterPubKey), masterPubKey);
		}

		public KeyManager(BitcoinEncryptedSecretNoEC encryptedSecret, byte[] chainCode, string password)
		{
			_extPubKey = null;
			HdPubKeys = new List<HdPubKey>();
			HdPubKeysLock = new object();

			if (password == null)
			{
				password = "";
			}

			EncryptedSecret = Guard.NotNull(nameof(encryptedSecret), encryptedSecret);
			ChainCode = Guard.NotNull(nameof(chainCode), chainCode);
			var extKey = new ExtKey(encryptedSecret.GetKey(password), chainCode);

			MasterPubKey = extKey.Derive(AccountKeyPath).PrivateKey.PubKey;
		}

		public static KeyManager CreateNew(out Mnemonic mnemonic, string password)
		{
			if(password == null)
			{
				password = "";
			}

			mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);
			ExtKey extKey = mnemonic.DeriveExtKey(password);
			var encryptedSecret = extKey.PrivateKey.GetEncryptedBitcoinSecret(password, Network.Main);

			return new KeyManager(encryptedSecret, extKey.ChainCode, extKey.Derive(AccountKeyPath).PrivateKey.PubKey);
		}

		public static KeyManager Recover(Mnemonic mnemonic, string password)
		{
			Guard.NotNull(nameof(mnemonic), mnemonic);
			if (password == null)
			{
				password = "";
			}

			ExtKey extKey = mnemonic.DeriveExtKey(password);
			var encryptedSecret = extKey.PrivateKey.GetEncryptedBitcoinSecret(password, Network.Main);

			return new KeyManager(encryptedSecret, extKey.ChainCode, extKey.Derive(AccountKeyPath).PrivateKey.PubKey);
		}

		public void ToFile(string filePath)
		{
			filePath = Guard.NotNullOrEmptyOrWhitespace(nameof(filePath), filePath);

			var directoryPath = Path.GetDirectoryName(Path.GetFullPath(filePath));
			if (directoryPath != null) Directory.CreateDirectory(directoryPath);

			string jsonString = JsonConvert.SerializeObject(this, Formatting.Indented);
				File.WriteAllText(filePath,
				jsonString,
				Encoding.UTF8);
		}

		public static KeyManager FromFile(string filePath)
		{
			filePath = Guard.NotNullOrEmptyOrWhitespace(nameof(filePath), filePath);

			if(!File.Exists(filePath))
			{
				throw new FileNotFoundException($"Walle file not found at: `{filePath}`.");
			}

			string jsonString = File.ReadAllText(filePath, Encoding.UTF8);
			return JsonConvert.DeserializeObject<KeyManager>(jsonString);
		}

		public HdPubKey GenerateNewKey(string label, KeyState keyState, bool isInternal)
		{
			// BIP44-ish derivation scheme
			// m / purpose' / coin_type' / account' / change / address_index
			lock (HdPubKeysLock)
			{
				var change = isInternal ? 1 : 0;

				IEnumerable<HdPubKey> relevantHdPubKeys;
				if (isInternal)
				{
					relevantHdPubKeys = HdPubKeys.Where(x => x.IsInternal());
				}
				else
				{
					relevantHdPubKeys = HdPubKeys.Where(x => !x.IsInternal());
				}

				KeyPath path;
				if (relevantHdPubKeys.Count() == 0)
				{
					path = new KeyPath($"{change}/0");
				}
				else
				{
					path = relevantHdPubKeys.OrderBy(x => x.GetIndex()).Last().GetNonHardenedKeyPath().Increment();
				}

				var fullPath = AccountKeyPath.Derive(path);
				var pubKey = ExtPubKey.Derive(path).PubKey;

				var hdPubKey = new HdPubKey(pubKey, fullPath, label, keyState);
				HdPubKeys.Add(hdPubKey);

				return hdPubKey;
			}
		}
	}
}

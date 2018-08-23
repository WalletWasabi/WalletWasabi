using WalletWasabi.JsonConverters;
using WalletWasabi.Helpers;
using NBitcoin;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Security;

namespace WalletWasabi.KeyManagement
{
	[JsonObject(MemberSerialization.OptIn)]
	public class KeyManager
	{
		[JsonProperty(Order = 1)]
		[JsonConverter(typeof(BitcoinEncryptedSecretNoECJsonConverter))]
		public BitcoinEncryptedSecretNoEC EncryptedSecret { get; }

		[JsonProperty(Order = 2)]
		[JsonConverter(typeof(ByteArrayJsonConverter))]
		public byte[] ChainCode { get; }

		[JsonProperty(Order = 3)]
		[JsonConverter(typeof(ExtPubKeyJsonConverter))]
		public ExtPubKey ExtPubKey { get; }

		[JsonProperty(Order = 4)]
		private List<HdPubKey> HdPubKeys { get; }

		// BIP84-ish derivation scheme
		// m / purpose' / coin_type' / account' / change / address_index
		// https://github.com/bitcoin/bips/blob/master/bip-0084.mediawiki
		private static readonly KeyPath AccountKeyPath = new KeyPath("m/84'/0'/0'");

		private readonly object HdPubKeysLock;

		public string FilePath { get; private set; }
		private object ToFileLock { get; }

		[JsonConstructor]
		public KeyManager(BitcoinEncryptedSecretNoEC encryptedSecret, byte[] chainCode, ExtPubKey extPubKey, string filePath = null)
		{
			HdPubKeys = new List<HdPubKey>();
			HdPubKeysLock = new object();

			EncryptedSecret = Guard.NotNull(nameof(encryptedSecret), encryptedSecret);
			ChainCode = Guard.NotNull(nameof(chainCode), chainCode);
			ExtPubKey = Guard.NotNull(nameof(extPubKey), extPubKey);
			SetFilePath(filePath);
			ToFileLock = new object();
			ToFile();
		}

		public KeyManager(BitcoinEncryptedSecretNoEC encryptedSecret, byte[] chainCode, string password, string filePath = null)
		{
			HdPubKeys = new List<HdPubKey>();
			HdPubKeysLock = new object();

			if (password == null)
			{
				password = "";
			}

			EncryptedSecret = Guard.NotNull(nameof(encryptedSecret), encryptedSecret);
			ChainCode = Guard.NotNull(nameof(chainCode), chainCode);
			var extKey = new ExtKey(encryptedSecret.GetKey(password), chainCode);

			ExtPubKey = extKey.Derive(AccountKeyPath).Neuter();

			SetFilePath(filePath);
			ToFileLock = new object();
			ToFile();
		}

		public static KeyManager CreateNew(out Mnemonic mnemonic, string password, string filePath = null)
		{
			if (password == null)
			{
				password = "";
			}

			mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);
			ExtKey extKey = mnemonic.DeriveExtKey(password);
			var encryptedSecret = extKey.PrivateKey.GetEncryptedBitcoinSecret(password, Network.Main);

			return new KeyManager(encryptedSecret, extKey.ChainCode, extKey.Derive(AccountKeyPath).Neuter(), filePath);
		}

		public static KeyManager Recover(Mnemonic mnemonic, string password, string filePath = null)
		{
			Guard.NotNull(nameof(mnemonic), mnemonic);
			if (password == null)
			{
				password = "";
			}

			ExtKey extKey = mnemonic.DeriveExtKey(password);
			var encryptedSecret = extKey.PrivateKey.GetEncryptedBitcoinSecret(password, Network.Main);

			return new KeyManager(encryptedSecret, extKey.ChainCode, extKey.Derive(AccountKeyPath).Neuter(), filePath);
		}

		public void SetFilePath(string filePath)
		{
			FilePath = string.IsNullOrWhiteSpace(filePath) ? null : filePath;
			if (FilePath == null) return;
			MakeSureContainingDirectoryExists(filePath);
		}

		private static void MakeSureContainingDirectoryExists(string filePath)
		{
			var directoryPath = Path.GetDirectoryName(Path.GetFullPath(filePath));
			Directory.CreateDirectory(directoryPath);
		}

		public void ToFile()
		{
			if (FilePath == null) return;
			ToFile(FilePath);
		}

		public void ToFile(string filePath)
		{
			MakeSureContainingDirectoryExists(filePath);
			lock (ToFileLock)
			{
				string jsonString = JsonConvert.SerializeObject(this, Formatting.Indented);
				IoHelpers.SafeWriteAllText(filePath, jsonString, Encoding.UTF8);
			}
		}

		public static KeyManager FromFile(string filePath)
		{
			filePath = Guard.NotNullOrEmptyOrWhitespace(nameof(filePath), filePath);

			if (!IoHelpers.TryGetSafestFileVersion(filePath, out var safestFile))
			{
				throw new FileNotFoundException($"Wallet file not found at: `{filePath}`.");
			}

			string jsonString = File.ReadAllText(safestFile, Encoding.UTF8);
			var km = JsonConvert.DeserializeObject<KeyManager>(jsonString);
			km.SetFilePath(filePath);
			return km;
		}

		public HdPubKey GenerateNewKey(string label, KeyState keyState, bool isInternal, bool toFile = true)
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
				if (!relevantHdPubKeys.Any())
				{
					path = new KeyPath($"{change}/0");
				}
				else
				{
					int largestIndex = relevantHdPubKeys.Max(x => x.GetIndex());
					List<int> missingIndexes = Enumerable.Range(0, largestIndex).Except(relevantHdPubKeys.Select(x => x.GetIndex())).ToList();
					if (missingIndexes.Any())
					{
						int smallestMissingIndex = missingIndexes.Min();
						path = relevantHdPubKeys.First(x => x.GetIndex() == (smallestMissingIndex - 1)).GetNonHardenedKeyPath().Increment();
					}
					else
					{
						path = relevantHdPubKeys.First(x => x.GetIndex() == largestIndex).GetNonHardenedKeyPath().Increment();
					}
				}

				var fullPath = AccountKeyPath.Derive(path);
				var pubKey = ExtPubKey.Derive(path).PubKey;

				var hdPubKey = new HdPubKey(pubKey, fullPath, label, keyState);
				HdPubKeys.Add(hdPubKey);

				if (toFile)
				{
					ToFile();
				}

				return hdPubKey;
			}
		}

		public IEnumerable<HdPubKey> GetKeys(KeyState? keyState = null, bool? isInternal = null)
		{
			// BIP44-ish derivation scheme
			// m / purpose' / coin_type' / account' / change / address_index
			lock (HdPubKeysLock)
			{
				if (keyState == null && isInternal == null)
				{
					return HdPubKeys;
				}
				if (keyState != null && isInternal == null)
				{
					return HdPubKeys.Where(x => x.KeyState == keyState);
				}
				else if (keyState == null)
				{
					return HdPubKeys.Where(x => x.IsInternal() == isInternal);
				}
				return HdPubKeys.Where(x => x.KeyState == keyState && x.IsInternal() == isInternal);
			}
		}

		public IEnumerable<ExtKey> GetSecrets(string password, params Script[] scripts)
		{
			Key secret;
			try
			{
				secret = EncryptedSecret.GetKey(password);
			}
			catch (SecurityException ex)
			{
				throw new SecurityException("Invalid password.", ex);
			}
			var extKey = new ExtKey(secret, ChainCode);
			var extKeys = new List<ExtKey>();

			lock (HdPubKeysLock)
			{
				foreach (HdPubKey key in HdPubKeys.Where(x =>
					scripts.Contains(x.GetP2wpkhScript())
					|| scripts.Contains(x.GetP2shOverP2wpkhScript())
					|| scripts.Contains(x.GetP2pkhScript())
					|| scripts.Contains(x.GetP2pkScript())))
				{
					ExtKey ek = extKey.Derive(key.FullKeyPath);
					extKeys.Add(ek);
				}
				return extKeys;
			}
		}
	}
}

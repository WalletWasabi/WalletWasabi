using NBitcoin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Helpers;
using WalletWasabi.Hwi.Models;
using WalletWasabi.JsonConverters;
using WalletWasabi.Logging;
using WalletWasabi.Models;

namespace WalletWasabi.Blockchain.Keys
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
		[JsonConverter(typeof(HDFingerprintJsonConverter))]
		public HDFingerprint? MasterFingerprint { get; private set; }

		[JsonProperty(Order = 4)]
		[JsonConverter(typeof(ExtPubKeyJsonConverter))]
		public ExtPubKey ExtPubKey { get; }

		[JsonProperty(Order = 5)]
		public bool? PasswordVerified { get; private set; }

		[JsonProperty(Order = 6)]
		public int? MinGapLimit { get; private set; }

		[JsonProperty(Order = 7)]
		[JsonConverter(typeof(KeyPathJsonConverter))]
		public KeyPath AccountKeyPath { get; private set; }

		[JsonProperty(Order = 8)]
		private BlockchainState BlockchainState { get; }

		private object BlockchainStateLock { get; }

		[JsonProperty(Order = 9)]
		private List<HdPubKey> HdPubKeys { get; }

		private object HdPubKeysLock { get; }

		private List<byte[]> HdPubKeyScriptBytes { get; }

		private object HdPubKeyScriptBytesLock { get; }

		private Dictionary<Script, HdPubKey> ScriptHdPubKeyMap { get; }

		private object ScriptHdPubKeyMapLock { get; }

		// BIP84-ish derivation scheme
		// m / purpose' / coin_type' / account' / change / address_index
		// https://github.com/bitcoin/bips/blob/master/bip-0084.mediawiki
		public static readonly KeyPath DefaultAccountKeyPath = new KeyPath("m/84h/0h/0h");

		public string FilePath { get; private set; }
		private object ToFileLock { get; }

		public bool IsWatchOnly => EncryptedSecret is null;
		public bool IsHardwareWallet => EncryptedSecret is null && MasterFingerprint != null;

		public const int AbsoluteMinGapLimit = 21;

		[JsonConstructor]
		public KeyManager(BitcoinEncryptedSecretNoEC encryptedSecret, byte[] chainCode, HDFingerprint? masterFingerprint, ExtPubKey extPubKey, bool? passwordVerified, int? minGapLimit, BlockchainState blockchainState, string filePath = null, KeyPath accountKeyPath = null)
		{
			HdPubKeys = new List<HdPubKey>();
			HdPubKeyScriptBytes = new List<byte[]>();
			ScriptHdPubKeyMap = new Dictionary<Script, HdPubKey>();
			HdPubKeysLock = new object();
			HdPubKeyScriptBytesLock = new object();
			ScriptHdPubKeyMapLock = new object();
			BlockchainStateLock = new object();

			EncryptedSecret = encryptedSecret;
			ChainCode = chainCode;
			MasterFingerprint = masterFingerprint;
			ExtPubKey = Guard.NotNull(nameof(extPubKey), extPubKey);

			PasswordVerified = passwordVerified;
			SetMinGapLimit(minGapLimit);

			BlockchainState = blockchainState ?? new BlockchainState();
			AccountKeyPath = accountKeyPath ?? DefaultAccountKeyPath;

			SetFilePath(filePath);
			ToFileLock = new object();
			ToFile();
		}

		public KeyManager(BitcoinEncryptedSecretNoEC encryptedSecret, byte[] chainCode, string password, int minGapLimit = AbsoluteMinGapLimit, string filePath = null, KeyPath accountKeyPath = null)
		{
			HdPubKeys = new List<HdPubKey>();
			HdPubKeyScriptBytes = new List<byte[]>();
			ScriptHdPubKeyMap = new Dictionary<Script, HdPubKey>();
			HdPubKeysLock = new object();
			HdPubKeyScriptBytesLock = new object();
			ScriptHdPubKeyMapLock = new object();
			BlockchainState = new BlockchainState();
			BlockchainStateLock = new object();

			if (password is null)
			{
				password = "";
			}

			SetMinGapLimit(minGapLimit);

			EncryptedSecret = Guard.NotNull(nameof(encryptedSecret), encryptedSecret);
			ChainCode = Guard.NotNull(nameof(chainCode), chainCode);
			var extKey = new ExtKey(encryptedSecret.GetKey(password), chainCode);

			MasterFingerprint = extKey.Neuter().PubKey.GetHDFingerPrint();
			AccountKeyPath = accountKeyPath ?? DefaultAccountKeyPath;
			ExtPubKey = extKey.Derive(AccountKeyPath).Neuter();

			SetFilePath(filePath);
			ToFileLock = new object();
			ToFile();
		}

		public static KeyManager CreateNew(out Mnemonic mnemonic, string password, string filePath = null)
		{
			if (password is null)
			{
				password = "";
			}

			mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);
			ExtKey extKey = mnemonic.DeriveExtKey(password);
			var encryptedSecret = extKey.PrivateKey.GetEncryptedBitcoinSecret(password, Network.Main);

			HDFingerprint masterFingerprint = extKey.Neuter().PubKey.GetHDFingerPrint();
			KeyPath keyPath = DefaultAccountKeyPath;
			ExtPubKey extPubKey = extKey.Derive(keyPath).Neuter();
			return new KeyManager(encryptedSecret, extKey.ChainCode, masterFingerprint, extPubKey, false, AbsoluteMinGapLimit, new BlockchainState(), filePath, keyPath);
		}

		public static KeyManager CreateNewWatchOnly(ExtPubKey extPubKey, string filePath = null)
		{
			return new KeyManager(null, null, null, extPubKey, null, AbsoluteMinGapLimit, new BlockchainState(), filePath);
		}

		public static KeyManager CreateNewHardwareWalletWatchOnly(HDFingerprint masterFingerprint, ExtPubKey extPubKey, string filePath = null)
		{
			return new KeyManager(null, null, masterFingerprint, extPubKey, null, AbsoluteMinGapLimit, new BlockchainState(), filePath);
		}

		public static KeyManager Recover(Mnemonic mnemonic, string password, string filePath = null, KeyPath accountKeyPath = null, int minGapLimit = AbsoluteMinGapLimit)
		{
			Guard.NotNull(nameof(mnemonic), mnemonic);
			if (password is null)
			{
				password = "";
			}

			ExtKey extKey = mnemonic.DeriveExtKey(password);
			var encryptedSecret = extKey.PrivateKey.GetEncryptedBitcoinSecret(password, Network.Main);

			HDFingerprint masterFingerprint = extKey.Neuter().PubKey.GetHDFingerPrint();

			KeyPath keyPath = accountKeyPath ?? DefaultAccountKeyPath;
			ExtPubKey extPubKey = extKey.Derive(keyPath).Neuter();
			return new KeyManager(encryptedSecret, extKey.ChainCode, masterFingerprint, extPubKey, true, minGapLimit, new BlockchainState(), filePath, keyPath);
		}

		public void SetMinGapLimit(int? minGapLimit)
		{
			MinGapLimit = minGapLimit is int val ? Math.Max(AbsoluteMinGapLimit, val) : AbsoluteMinGapLimit;
			// AssertCleanKeysIndexed(); Do not do this. Wallet file is null yet.
		}

		public void SetFilePath(string filePath)
		{
			FilePath = string.IsNullOrWhiteSpace(filePath) ? null : filePath;
			if (FilePath is null)
			{
				return;
			}

			IoHelpers.EnsureContainingDirectoryExists(FilePath);
		}

		public void ToFile()
		{
			lock (HdPubKeysLock)
				lock (BlockchainStateLock)
					lock (ToFileLock)
					{
						ToFileNoLock();
					}
		}

		public void ToFileNoBlockchainStateLock()
		{
			lock (HdPubKeysLock)
				lock (ToFileLock)
				{
					ToFileNoLock();
				}
		}

		public void ToFile(string filePath)
		{
			lock (HdPubKeysLock)
				lock (BlockchainStateLock)
					lock (ToFileLock)
					{
						ToFileNoLock(filePath);
					}
		}

		private void ToFileNoLock()
		{
			if (FilePath is null)
			{
				return;
			}

			ToFileNoLock(FilePath);
		}

		private void ToFileNoLock(string filePath)
		{
			IoHelpers.EnsureContainingDirectoryExists(filePath);
			// Remove the last 100 blocks to ensure verification on the next run. This is needed of reorg.
			int maturity = 101;
			Height prevHeight = BlockchainState.Height;
			int matureHeight = Math.Max(0, prevHeight.Value - maturity);

			BlockchainState.Height = new Height(matureHeight);

			string jsonString = JsonConvert.SerializeObject(this, Formatting.Indented);
			File.WriteAllText(filePath, jsonString, Encoding.UTF8);

			// Re-add removed items for further operations.
			BlockchainState.Height = prevHeight;
		}

		public static KeyManager FromFile(string filePath)
		{
			filePath = Guard.NotNullOrEmptyOrWhitespace(nameof(filePath), filePath);

			if (!File.Exists(filePath))
			{
				throw new FileNotFoundException($"Wallet file not found at: `{filePath}`.");
			}

			string jsonString = File.ReadAllText(filePath, Encoding.UTF8);
			var km = JsonConvert.DeserializeObject<KeyManager>(jsonString);

			km.SetFilePath(filePath);
			lock (km.HdPubKeyScriptBytesLock)
			{
				km.HdPubKeyScriptBytes.AddRange(km.GetKeys(x => true).Select(x => x.P2wpkhScript.ToCompressedBytes()));
			}

			lock (km.ScriptHdPubKeyMapLock)
			{
				foreach (var key in km.GetKeys())
				{
					km.ScriptHdPubKeyMap.Add(key.P2wpkhScript, key);
				}
			}

			// Backwards compatibility:
			if (km.PasswordVerified is null)
			{
				km.PasswordVerified = true;
			}

			return km;
		}

		public static bool TryGetEncryptedSecretFromFile(string filePath, out BitcoinEncryptedSecretNoEC encryptedSecret)
		{
			encryptedSecret = default;
			filePath = Guard.NotNullOrEmptyOrWhitespace(nameof(filePath), filePath);

			if (!File.Exists(filePath))
			{
				throw new FileNotFoundException($"Wallet file not found at: `{filePath}`.");
			}

			// Example text to handle: "ExtPubKey": "03BF8271268000000013B9013C881FE456DDF524764F6322F611B03CF6".
			var encryptedSecretLine = File.ReadLines(filePath) // Enumerated read.
				.Take(21) // Limit reads to x lines.
				.FirstOrDefault(line => line.Contains("\"EncryptedSecret\": \"", StringComparison.OrdinalIgnoreCase));

			if (string.IsNullOrEmpty(encryptedSecretLine))
			{
				return false;
			}

			var parts = encryptedSecretLine.Split("\"EncryptedSecret\": \"");
			if (parts.Length != 2)
			{
				throw new FormatException($"Could not split line: {encryptedSecretLine}");
			}

			var encsec = parts[1].TrimEnd(',', '"');
			if (string.IsNullOrEmpty(encsec) || encsec.Equals("null", StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}

			encryptedSecret = new BitcoinEncryptedSecretNoEC(encsec);
			return true;
		}

		public static bool TryGetExtPubKeyFromFile(string filePath, out ExtPubKey extPubKey)
		{
			extPubKey = default;
			filePath = Guard.NotNullOrEmptyOrWhitespace(nameof(filePath), filePath);

			if (!File.Exists(filePath))
			{
				throw new FileNotFoundException($"Wallet file not found at: `{filePath}`.");
			}

			// Example text to handle: "ExtPubKey": "03BF8271268000000013B9013C881FE456DDF524764F6322F611B03CF6".
			var extPubKeyLine = File.ReadLines(filePath) // Enumerated read.
				.Take(21) // Limit reads to x lines.
				.FirstOrDefault(line => line.Contains("\"ExtPubKey\": \"", StringComparison.OrdinalIgnoreCase));

			if (string.IsNullOrEmpty(extPubKeyLine))
			{
				return false;
			}

			var parts = extPubKeyLine.Split("\"ExtPubKey\": \"");
			if (parts.Length != 2)
			{
				throw new FormatException($"Could not split line: {extPubKeyLine}");
			}

			var xpub = parts[1].TrimEnd(',', '"');
			if (string.IsNullOrEmpty(xpub) || xpub.Equals("null", StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}

			extPubKey = NBitcoinHelpers.BetterParseExtPubKey(xpub);
			return true;
		}

		public static bool TryGetMasterFingerprintFromFile(string filePath, out HDFingerprint masterFingerprint)
		{
			masterFingerprint = default;
			filePath = Guard.NotNullOrEmptyOrWhitespace(nameof(filePath), filePath);

			if (!File.Exists(filePath))
			{
				throw new FileNotFoundException($"Wallet file not found at: `{filePath}`.");
			}

			// Example text to handle: "ExtPubKey": "03BF8271268000000013B9013C881FE456DDF524764F6322F611B03CF6".
			var masterFpLine = File.ReadLines(filePath) // Enumerated read.
				.Take(21) // Limit reads to x lines.
				.FirstOrDefault(line => line.Contains("\"MasterFingerprint\": \"", StringComparison.OrdinalIgnoreCase));

			if (string.IsNullOrEmpty(masterFpLine))
			{
				return false;
			}

			var parts = masterFpLine.Split("\"MasterFingerprint\": \"");
			if (parts.Length != 2)
			{
				throw new FormatException($"Could not split line: {masterFpLine}");
			}

			var hex = parts[1].TrimEnd(',', '"');
			if (string.IsNullOrEmpty(hex) || hex.Equals("null", StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}

			var mfp = new HDFingerprint(ByteHelpers.FromHex(hex));
			masterFingerprint = mfp;
			return true;
		}

		public HdPubKey GenerateNewKey(SmartLabel label, KeyState keyState, bool isInternal, bool toFile = true)
		{
			// BIP44-ish derivation scheme
			// m / purpose' / coin_type' / account' / change / address_index
			var change = isInternal ? 1 : 0;

			lock (HdPubKeysLock)
			{
				HdPubKey[] relevantHdPubKeys = HdPubKeys.Where(x => x.IsInternal == isInternal).ToArray();

				KeyPath path = new KeyPath($"{change}/0");
				if (relevantHdPubKeys.Any())
				{
					int largestIndex = relevantHdPubKeys.Max(x => x.Index);
					var smallestMissingIndex = largestIndex;
					var present = new bool[largestIndex + 1];
					for (int i = 0; i < relevantHdPubKeys.Length; ++i)
					{
						present[relevantHdPubKeys[i].Index] = true;
					}
					for (int i = 1; i < present.Length; ++i)
					{
						if (!present[i])
						{
							smallestMissingIndex = i - 1;
							break;
						}
					}

					path = relevantHdPubKeys[smallestMissingIndex].NonHardenedKeyPath.Increment();
				}

				var fullPath = AccountKeyPath.Derive(path);
				var pubKey = ExtPubKey.Derive(path).PubKey;

				var hdPubKey = new HdPubKey(pubKey, fullPath, label, keyState);
				HdPubKeys.Add(hdPubKey);
				lock (HdPubKeyScriptBytesLock)
				{
					HdPubKeyScriptBytes.Add(hdPubKey.P2wpkhScript.ToCompressedBytes());
				}

				lock (ScriptHdPubKeyMapLock)
				{
					ScriptHdPubKeyMap.Add(hdPubKey.P2wpkhScript, hdPubKey);
				}

				if (toFile)
				{
					ToFile();
				}

				return hdPubKey;
			}
		}

		public HdPubKey GetNextReceiveKey(SmartLabel label, out bool minGapLimitIncreased)
		{
			if (label.IsEmpty)
			{
				throw new InvalidOperationException("Label is required.");
			}

			minGapLimitIncreased = false;

			AssertCleanKeysIndexed(isInternal: false);

			// Find the next clean external key with empty label.
			var newKey = GetKeys(x => x.IsInternal == false && x.KeyState == KeyState.Clean && x.Label.IsEmpty).FirstOrDefault();

			// If not found, generate a new.
			if (newKey is null)
			{
				SetMinGapLimit(MinGapLimit.Value + 1);
				newKey = AssertCleanKeysIndexed(isInternal: false).First();

				// If the new is over the MinGapLimit, set minGapLimitIncreased to true.
				minGapLimitIncreased = true;
			}

			newKey.SetLabel(label, kmToFile: this);

			return newKey;
		}

		public IEnumerable<HdPubKey> GetKeys(Func<HdPubKey, bool> wherePredicate)
		{
			// BIP44-ish derivation scheme
			// m / purpose' / coin_type' / account' / change / address_index
			lock (HdPubKeysLock)
			{
				if (wherePredicate is null)
				{
					return HdPubKeys.ToList();
				}
				else
				{
					return HdPubKeys.Where(wherePredicate).ToList();
				}
			}
		}

		public void SetPasswordVerified()
		{
			PasswordVerified = true;
			ToFile();
		}

		public IEnumerable<HdPubKey> GetKeys(KeyState? keyState = null, bool? isInternal = null)
		{
			if (keyState is null)
			{
				if (isInternal is null)
				{
					return GetKeys(x => true);
				}
				else
				{
					return GetKeys(x => x.IsInternal == isInternal);
				}
			}
			else
			{
				if (isInternal is null)
				{
					return GetKeys(x => x.KeyState == keyState);
				}
				else
				{
					return GetKeys(x => x.IsInternal == isInternal && x.KeyState == keyState);
				}
			}
		}

		public int CountConsecutiveCleanKeys(bool isInternal)
		{
			var keys = GetKeys(KeyState.Clean, isInternal).OrderBy(x => x.Index).ToArray();
			var keyCount = keys.Length;
			if (keyCount < 2)
			{
				return keyCount;
			}

			var largerConsecutiveSequence = 1;
			var consecutives = 1;
			for (int i = 1; i < keyCount; i++)
			{
				if (keys[i].Index == keys[i - 1].Index + 1)
				{
					consecutives++;
				}
				else if (consecutives > largerConsecutiveSequence)
				{
					largerConsecutiveSequence = consecutives;
					consecutives = 1;
				}
			}

			return Math.Max(consecutives, largerConsecutiveSequence);
		}

		public IEnumerable<byte[]> GetPubKeyScriptBytes()
		{
			lock (HdPubKeyScriptBytesLock)
			{
				return HdPubKeyScriptBytes;
			}
		}

		public HdPubKey GetKeyForScriptPubKey(Script scriptPubKey)
		{
			lock (ScriptHdPubKeyMapLock)
			{
				if (ScriptHdPubKeyMap.TryGetValue(scriptPubKey, out var key))
				{
					return key;
				}

				return default;
			}
		}

		public IEnumerable<ExtKey> GetSecrets(string password, params Script[] scripts)
		{
			return GetSecretsAndPubKeyPairs(password, scripts).Select(x => x.secret);
		}

		public IEnumerable<(ExtKey secret, HdPubKey pubKey)> GetSecretsAndPubKeyPairs(string password, params Script[] scripts)
		{
			ExtKey extKey = GetMasterExtKey(password);
			var extKeysAndPubs = new List<(ExtKey secret, HdPubKey pubKey)>();

			lock (HdPubKeysLock)
			{
				foreach (HdPubKey key in HdPubKeys.Where(x =>
					scripts.Contains(x.P2wpkhScript)
					|| scripts.Contains(x.P2shOverP2wpkhScript)
					|| scripts.Contains(x.P2pkhScript)
					|| scripts.Contains(x.P2pkScript)))
				{
					ExtKey ek = extKey.Derive(key.FullKeyPath);
					extKeysAndPubs.Add((ek, key));
				}
			}
			return extKeysAndPubs;
		}

		public ExtKey GetMasterExtKey(string password)
		{
			if (password is null)
			{
				password = "";
			}

			if (IsWatchOnly)
			{
				throw new SecurityException("This is a watchonly wallet.");
			}

			try
			{
				Key secret = EncryptedSecret.GetKey(password);
				var extKey = new ExtKey(secret, ChainCode);

				// Backwards compatibility:
				if (MasterFingerprint is null)
				{
					MasterFingerprint = secret.PubKey.GetHDFingerPrint();
				}

				return extKey;
			}
			catch (SecurityException ex)
			{
				throw new SecurityException("Invalid password.", ex);
			}
		}

		/// <summary>
		/// Make sure there's always clean keys generated and indexed.
		/// Call SetMinGapLimit() to set how many keys should be asserted.
		/// </summary>
		public IEnumerable<HdPubKey> AssertCleanKeysIndexed(bool? isInternal = null)
		{
			var newKeys = new List<HdPubKey>();

			if (isInternal.HasValue)
			{
				while (CountConsecutiveCleanKeys(isInternal.Value) < MinGapLimit)
				{
					newKeys.Add(GenerateNewKey(SmartLabel.Empty, KeyState.Clean, isInternal.Value, toFile: false));
				}
			}
			else
			{
				while (CountConsecutiveCleanKeys(true) < MinGapLimit)
				{
					newKeys.Add(GenerateNewKey(SmartLabel.Empty, KeyState.Clean, true, toFile: false));
				}
				while (CountConsecutiveCleanKeys(false) < MinGapLimit)
				{
					newKeys.Add(GenerateNewKey(SmartLabel.Empty, KeyState.Clean, false, toFile: false));
				}
			}

			if (newKeys.Any())
			{
				ToFile();
			}

			return newKeys;
		}

		/// <summary>
		/// Make sure there's always locked internal keys generated and indexed.
		/// </summary>
		public bool AssertLockedInternalKeysIndexed(int howMany = 14)
		{
			var generated = false;

			while (GetKeys(KeyState.Locked, true).Count() < howMany)
			{
				GenerateNewKey(SmartLabel.Empty, KeyState.Locked, true, toFile: false);
				generated = true;
			}

			if (generated)
			{
				ToFile();
			}

			return generated;
		}

		#region BlockchainState

		public Height GetBestHeight()
		{
			Height res;
			lock (BlockchainStateLock)
			{
				res = BlockchainState.Height;
			}
			return res;
		}

		public void SetNetwork(Network network)
		{
			lock (BlockchainStateLock)
			{
				BlockchainState.Network = network;
				ToFileNoBlockchainStateLock();
			}
		}

		public void SetBestHeight(Height height)
		{
			lock (BlockchainStateLock)
			{
				BlockchainState.Height = height;
				ToFileNoBlockchainStateLock();
			}
		}

		public void SetMaxBestHeight(Height height)
		{
			lock (BlockchainStateLock)
			{
				BlockchainState.Height = Math.Min(BlockchainState.Height, height);
				ToFileNoBlockchainStateLock();
			}
		}

		public void AssertNetworkOrClearBlockState(Network expectedNetwork)
		{
			lock (BlockchainStateLock)
			{
				var lastNetwork = BlockchainState.Network;
				if (lastNetwork is null || lastNetwork != expectedNetwork)
				{
					BlockchainState.Network = expectedNetwork;
					BlockchainState.Height = 0;
					ToFileNoBlockchainStateLock();

					if (lastNetwork != null)
					{
						Logger.LogWarning($"Wallet is opened on {expectedNetwork}. Last time it was opened on {lastNetwork}.");
					}
					Logger.LogInfo("Blockchain cache is cleared.");
				}
			}
		}

		#endregion BlockchainState
	}
}

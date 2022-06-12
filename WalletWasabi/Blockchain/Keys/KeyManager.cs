using NBitcoin;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Security;
using System.Text;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Helpers;
using WalletWasabi.Io;
using WalletWasabi.JsonConverters;
using WalletWasabi.JsonConverters.Bitcoin;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Wallets;
using static WalletWasabi.Blockchain.Keys.WpkhOutputDescriptorHelper;

namespace WalletWasabi.Blockchain.Keys;

[JsonObject(MemberSerialization.OptIn)]
public class KeyManager
{
	public const int DefaultAnonScoreTarget = 5;
	public const bool DefaultAutoCoinjoin = false;
	public const int DefaultFeeRateMedianTimeFrameHours = 0;

	public const int AbsoluteMinGapLimit = 21;
	public const int MaxGapLimit = 10_000;
	public static Money DefaultPlebStopThreshold = Money.Coins(0.01m);

	// BIP84-ish derivation scheme
	// m / purpose' / coin_type' / account' / change / address_index
	// https://github.com/bitcoin/bips/blob/master/bip-0084.mediawiki
	private static readonly KeyPath DefaultAccountKeyPath = new("m/84h/0h/0h");

	private static readonly KeyPath TestNetAccountKeyPath = new("m/84h/1h/0h");

	[JsonConstructor]
	public KeyManager(BitcoinEncryptedSecretNoEC encryptedSecret, byte[] chainCode, HDFingerprint? masterFingerprint, ExtPubKey extPubKey, bool skipSynchronization, int? minGapLimit, BlockchainState blockchainState, string? filePath = null, KeyPath? accountKeyPath = null)
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

		SkipSynchronization = skipSynchronization;
		SetMinGapLimit(minGapLimit);

		BlockchainState = blockchainState;

		AccountKeyPath = accountKeyPath ?? GetAccountKeyPath(BlockchainState.Network);

		SetFilePath(filePath);
		ToFileLock = new object();
		ToFile();
	}

	public KeyManager(BitcoinEncryptedSecretNoEC encryptedSecret, byte[] chainCode, string password, Network network)
	{
		HdPubKeys = new List<HdPubKey>();
		HdPubKeyScriptBytes = new List<byte[]>();
		ScriptHdPubKeyMap = new Dictionary<Script, HdPubKey>();
		HdPubKeysLock = new object();
		HdPubKeyScriptBytesLock = new object();
		ScriptHdPubKeyMapLock = new object();
		BlockchainState = new BlockchainState(network);
		BlockchainStateLock = new object();

		password ??= "";

		SetMinGapLimit(AbsoluteMinGapLimit);

		EncryptedSecret = Guard.NotNull(nameof(encryptedSecret), encryptedSecret);
		ChainCode = Guard.NotNull(nameof(chainCode), chainCode);
		var extKey = new ExtKey(encryptedSecret.GetKey(password), chainCode);

		MasterFingerprint = extKey.Neuter().PubKey.GetHDFingerPrint();
		AccountKeyPath = GetAccountKeyPath(BlockchainState.Network);
		ExtPubKey = extKey.Derive(AccountKeyPath).Neuter();
		ToFileLock = new object();
	}

	[OnDeserialized]
	private void OnDeserializedMethod(StreamingContext context)
	{
		// This should be impossible but in any case, coinjoin can only happen,
		// if a profile is selected. Otherwise, the user's money can be drained.
		if (AutoCoinJoin && !IsCoinjoinProfileSelected)
		{
			AutoCoinJoin = false;
		}
	}

	public static KeyPath GetAccountKeyPath(Network network) =>
		network == Network.TestNet ? TestNetAccountKeyPath : DefaultAccountKeyPath;

	public WpkhDescriptors GetOutputDescriptors(string password, Network network)
	{
		if (!MasterFingerprint.HasValue)
		{
			throw new InvalidOperationException($"{nameof(MasterFingerprint)} is not defined.");
		}

		return WpkhOutputDescriptorHelper.GetOutputDescriptors(network, MasterFingerprint.Value, GetMasterExtKey(password), AccountKeyPath);
	}

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

	[JsonProperty(Order = 5)] public bool SkipSynchronization { get; private set; } = false;

	[JsonProperty(Order = 6)]
	public int MinGapLimit { get; private set; }

	[JsonProperty(Order = 7)]
	[JsonConverter(typeof(KeyPathJsonConverter))]
	public KeyPath AccountKeyPath { get; private set; }

	public string? FilePath { get; private set; }

	[MemberNotNullWhen(returnValue: false, nameof(EncryptedSecret))]
	public bool IsWatchOnly => EncryptedSecret is null;

	[MemberNotNullWhen(returnValue: true, nameof(MasterFingerprint))]
	public bool IsHardwareWallet => EncryptedSecret is null && MasterFingerprint is not null;

	[JsonProperty(Order = 8)]
	private BlockchainState BlockchainState { get; }

	[JsonProperty(Order = 9, PropertyName = "PreferPsbtWorkflow")]
	public bool PreferPsbtWorkflow { get; set; }

	[JsonProperty(Order = 10, PropertyName = "AutoCoinJoin")]
	public bool AutoCoinJoin { get; set; } = DefaultAutoCoinjoin;

	/// <summary>
	/// Won't coinjoin automatically if there are less than this much non-private coins in the wallet.
	/// </summary>
	[JsonProperty(Order = 11, PropertyName = "PlebStopThreshold")]
	[JsonConverter(typeof(MoneyBtcJsonConverter))]
	public Money PlebStopThreshold { get; set; } = DefaultPlebStopThreshold;

	[JsonProperty(Order = 12, PropertyName = "Icon")]
	public string? Icon { get; private set; }

	[JsonProperty(Order = 13, PropertyName = "AnonScoreTarget")]
	public int AnonScoreTarget { get; private set; } = DefaultAnonScoreTarget;

	[JsonProperty(Order = 14, PropertyName = "FeeRateMedianTimeFrameHours")]
	public int FeeRateMedianTimeFrameHours { get; private set; } = DefaultFeeRateMedianTimeFrameHours;

	[JsonProperty(Order = 15, PropertyName = "IsCoinjoinProfileSelected")]
	public bool IsCoinjoinProfileSelected { get; set; } = false;

	[JsonProperty(Order = 999)]
	private List<HdPubKey> HdPubKeys { get; }

	private object BlockchainStateLock { get; }

	private object HdPubKeysLock { get; }

	private List<byte[]> HdPubKeyScriptBytes { get; }

	private object HdPubKeyScriptBytesLock { get; }

	private Dictionary<Script, HdPubKey> ScriptHdPubKeyMap { get; }

	private object ScriptHdPubKeyMapLock { get; }
	private object ToFileLock { get; }
	public string WalletName => string.IsNullOrWhiteSpace(FilePath) ? "" : Path.GetFileNameWithoutExtension(FilePath);

	public static KeyManager CreateNew(out Mnemonic mnemonic, string password, Network network, string? filePath = null)
	{
		password ??= "";

		mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);
		ExtKey extKey = mnemonic.DeriveExtKey(password);
		var encryptedSecret = extKey.PrivateKey.GetEncryptedBitcoinSecret(password, Network.Main);

		HDFingerprint masterFingerprint = extKey.Neuter().PubKey.GetHDFingerPrint();
		BlockchainState blockchainState = new(network);
		KeyPath keyPath = GetAccountKeyPath(network);
		ExtPubKey extPubKey = extKey.Derive(keyPath).Neuter();
		return new KeyManager(encryptedSecret, extKey.ChainCode, masterFingerprint, extPubKey, skipSynchronization: true, AbsoluteMinGapLimit, blockchainState, filePath, keyPath);
	}

	public static KeyManager CreateNewWatchOnly(ExtPubKey extPubKey, string? filePath = null)
	{
		return new KeyManager(null, null, null, extPubKey, skipSynchronization: false, AbsoluteMinGapLimit, new BlockchainState(), filePath);
	}

	public static KeyManager CreateNewHardwareWalletWatchOnly(HDFingerprint masterFingerprint, ExtPubKey extPubKey, Network network, string? filePath = null)
	{
		return new KeyManager(null, null, masterFingerprint, extPubKey, skipSynchronization: false, AbsoluteMinGapLimit, new BlockchainState(network), filePath);
	}

	public static KeyManager Recover(Mnemonic mnemonic, string password, Network network, KeyPath accountKeyPath, string? filePath = null, int minGapLimit = AbsoluteMinGapLimit)
	{
		Guard.NotNull(nameof(mnemonic), mnemonic);
		password ??= "";

		ExtKey extKey = mnemonic.DeriveExtKey(password);
		var encryptedSecret = extKey.PrivateKey.GetEncryptedBitcoinSecret(password, Network.Main);

		HDFingerprint masterFingerprint = extKey.Neuter().PubKey.GetHDFingerPrint();

		KeyPath keyPath = accountKeyPath ?? DefaultAccountKeyPath;
		ExtPubKey extPubKey = extKey.Derive(keyPath).Neuter();
		return new KeyManager(encryptedSecret, extKey.ChainCode, masterFingerprint, extPubKey, skipSynchronization: false, minGapLimit, new BlockchainState(network), filePath, keyPath);
	}

	public static KeyManager FromFile(string filePath)
	{
		filePath = Guard.NotNullOrEmptyOrWhitespace(nameof(filePath), filePath);

		if (!File.Exists(filePath))
		{
			throw new FileNotFoundException($"Wallet file not found at: `{filePath}`.");
		}

		SafeIoManager safeIoManager = new(filePath);
		string jsonString = safeIoManager.ReadAllText(Encoding.UTF8);

		KeyManager km = JsonConvert.DeserializeObject<KeyManager>(jsonString)
			?? throw new JsonSerializationException($"Wallet file at: `{filePath}` is not a valid wallet file or it is corrupted.");

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

		return km;
	}

	public void SetFilePath(string? filePath)
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
		{
			lock (BlockchainStateLock)
			{
				lock (ToFileLock)
				{
					ToFileNoLock();
				}
			}
		}
	}

	public void ToFile(string filePath)
	{
		lock (HdPubKeysLock)
		{
			lock (BlockchainStateLock)
			{
				lock (ToFileLock)
				{
					ToFileNoLock(filePath);
				}
			}
		}
	}

	public HdPubKey GenerateNewKey(SmartLabel label, KeyState keyState, bool isInternal, bool toFile = true)
	{
		// BIP44-ish derivation scheme
		// m / purpose' / coin_type' / account' / change / address_index
		var change = isInternal ? 1 : 0;

		lock (HdPubKeysLock)
		{
			HdPubKey[] relevantHdPubKeys = HdPubKeys.Where(x => x.IsInternal == isInternal).ToArray();

			KeyPath path = new($"{change}/0");
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
			SetMinGapLimit(MinGapLimit + 1);
			newKey = AssertCleanKeysIndexed(isInternal: false).First();

			// If the new is over the MinGapLimit, set minGapLimitIncreased to true.
			minGapLimitIncreased = true;
		}

		newKey.SetLabel(label, kmToFile: this);

		SetDoNotSkipSynchronization();

		return newKey;
	}

	public IEnumerable<HdPubKey> GetKeys(Func<HdPubKey, bool>? wherePredicate)
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

	public void SetDoNotSkipSynchronization()
	{
		// Don't set it unnecessarily
		if (SkipSynchronization == false)
		{
			return;
		}

		SkipSynchronization = false;
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

	/// <param name="ignoreTail">If true it does only consider the gap between used keys and does not care about the nonused keys at the end.</param>
	public int CountConsecutiveUnusedKeys(bool isInternal, bool ignoreTail)
	{
		var keyIndexes = GetKeys(x => x.IsInternal == isInternal && x.KeyState != KeyState.Used).Select(x => x.Index).ToArray();

		if (ignoreTail)
		{
			var lastUsedIndex = GetKeys(x => x.IsInternal == isInternal && x.KeyState == KeyState.Used).LastOrDefault()?.Index;
			if (lastUsedIndex is null)
			{
				return 0;
			}
			else
			{
				keyIndexes = keyIndexes.Where(x => x < lastUsedIndex).ToArray();
			}
		}

		var hs = keyIndexes.ToHashSet();
		int largerConsecutiveSequence = 0;

		for (int i = 0; i < keyIndexes.Length; ++i)
		{
			if (!hs.Contains(keyIndexes[i] - 1))
			{
				int j = keyIndexes[i];
				while (hs.Contains(j))
				{
					j++;
				}

				var sequenceLength = j - keyIndexes[i];
				if (largerConsecutiveSequence < sequenceLength)
				{
					largerConsecutiveSequence = sequenceLength;
				}
			}
		}
		return largerConsecutiveSequence;
	}

	public IEnumerable<byte[]> GetPubKeyScriptBytes()
	{
		lock (HdPubKeyScriptBytesLock)
		{
			return HdPubKeyScriptBytes.ToImmutableArray();
		}
	}

	public bool TryGetKeyForScriptPubKey(Script scriptPubKey, [NotNullWhen(true)] out HdPubKey? hdPubKey)
	{
		hdPubKey = default;

		lock (ScriptHdPubKeyMapLock)
		{
			if (ScriptHdPubKeyMap.TryGetValue(scriptPubKey, out var key))
			{
				hdPubKey = key;
				return true;
			}

			return false;
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

	public IEnumerable<SmartLabel> GetChangeLabels() => GetKeys(x => x.IsInternal).Select(x => x.Label);

	public IEnumerable<SmartLabel> GetReceiveLabels() => GetKeys(x => !x.IsInternal).Select(x => x.Label);

	public ExtKey GetMasterExtKey(string password)
	{
		password ??= "";

		if (IsWatchOnly)
		{
			throw new SecurityException("This is a watchonly wallet.");
		}

		try
		{
			Key secret = EncryptedSecret.GetKey(password);
			var extKey = new ExtKey(secret, ChainCode);

			// Backwards compatibility:
			MasterFingerprint ??= secret.PubKey.GetHDFingerPrint();

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
	public IEnumerable<HdPubKey> AssertCleanKeysIndexed(bool? isInternal = null, bool toFile = true)
	{
		var newKeys = new List<HdPubKey>();

		if (isInternal.HasValue)
		{
			while (CountConsecutiveUnusedKeys(isInternal.Value, ignoreTail: false) < MinGapLimit)
			{
				newKeys.Add(GenerateNewKey(SmartLabel.Empty, KeyState.Clean, isInternal.Value, toFile: false));
			}
		}
		else
		{
			while (CountConsecutiveUnusedKeys(true, ignoreTail: false) < MinGapLimit)
			{
				newKeys.Add(GenerateNewKey(SmartLabel.Empty, KeyState.Clean, true, toFile: false));
			}
			while (CountConsecutiveUnusedKeys(false, ignoreTail: false) < MinGapLimit)
			{
				newKeys.Add(GenerateNewKey(SmartLabel.Empty, KeyState.Clean, false, toFile: false));
			}
		}

		if (toFile && newKeys.Any())
		{
			ToFile();
		}

		return newKeys;
	}

	/// <summary>
	/// Make sure there's always locked internal keys generated and indexed.
	/// </summary>
	public bool AssertLockedInternalKeysIndexed(int howMany)
	{
		var changed = false;

		while (GetKeys(KeyState.Locked, true).Count() < howMany)
		{
			var firstUnusedInternalKey = GetKeys(x => x.IsInternal == true && x.KeyState == KeyState.Clean && x.Label.IsEmpty).FirstOrDefault();

			if (firstUnusedInternalKey is null)
			{
				// If not found, generate a new.
				GenerateNewKey(SmartLabel.Empty, KeyState.Locked, true, toFile: false);
			}
			else
			{
				firstUnusedInternalKey.SetKeyState(KeyState.Locked);
			}

			changed = true;
		}

		AssertCleanKeysIndexed(isInternal: true, toFile: false);

		if (changed)
		{
			ToFile();
		}

		return changed;
	}

	private void SetMinGapLimit(int? minGapLimit)
	{
		MinGapLimit = minGapLimit is int val ? Math.Max(AbsoluteMinGapLimit, val) : AbsoluteMinGapLimit;
		// AssertCleanKeysIndexed(); Do not do this. Wallet file is null yet.
	}

	private void ToFileNoBlockchainStateLock()
	{
		lock (HdPubKeysLock)
		{
			lock (ToFileLock)
			{
				ToFileNoLock();
			}
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

		SafeIoManager safeIoManager = new(filePath);
		safeIoManager.WriteAllText(jsonString, Encoding.UTF8);

		// Re-add removed items for further operations.
		BlockchainState.Height = prevHeight;
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

	public Network GetNetwork()
	{
		lock (BlockchainStateLock)
		{
			return BlockchainState.Network;
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
			var prevHeight = BlockchainState.Height;
			var newHeight = Math.Min(prevHeight, height);
			if (prevHeight != newHeight)
			{
				BlockchainState.Height = newHeight;
				ToFileNoBlockchainStateLock();
				Logger.LogWarning($"Wallet ({WalletName}) height has been set back by {prevHeight - newHeight}. From {prevHeight} to {newHeight}.");
			}
		}
	}

	public void SetIcon(string icon)
	{
		Icon = icon;
		ToFile();
	}

	public void SetIcon(WalletType type)
	{
		SetIcon(type.ToString());
	}

	public void SetAnonScoreTarget(int anonScoreTarget, bool toFile = true)
	{
		AnonScoreTarget = anonScoreTarget;
		if (toFile)
		{
			ToFile();
		}
	}

	public void SetFeeRateMedianTimeFrame(int hours, bool toFile = true)
	{
		if (hours != 0 && !Constants.CoinJoinFeeRateMedianTimeFrames.Contains(hours))
		{
			throw new ArgumentOutOfRangeException(nameof(hours), $"Hours can be only one of {string.Join(",", Constants.CoinJoinFeeRateMedianTimeFrames)}.");
		}

		FeeRateMedianTimeFrameHours = hours;
		if (toFile)
		{
			ToFile();
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

				if (lastNetwork is { })
				{
					Logger.LogWarning($"Wallet is opened on {expectedNetwork}. Last time it was opened on {lastNetwork}.");
				}
				Logger.LogInfo("Blockchain cache is cleared.");
			}
		}
	}

	#endregion BlockchainState
}

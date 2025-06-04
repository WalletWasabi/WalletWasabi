using NBitcoin;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Text.Json.Nodes;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.CoinJoinProfiles;
using WalletWasabi.Helpers;
using WalletWasabi.Io;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Serialization;
using WalletWasabi.Wallets;
using WalletWasabi.Wallets.Slip39;
using static WalletWasabi.Blockchain.Keys.WpkhOutputDescriptorHelper;
using Decode = WalletWasabi.Serialization.Decode;
using Encode = WalletWasabi.Serialization.Encode;
using Array = System.Array;
using Network = NBitcoin.Network;
using OutPoint = NBitcoin.OutPoint;

namespace WalletWasabi.Blockchain.Keys;

public class KeyManager
{
	public const bool DefaultAutoCoinjoin = false;
	public const bool DefaultRedCoinIsolation = false;

	public const int AbsoluteMinGapLimit = 21;
	public const int MaxGapLimit = 10_000;
	public static readonly Money DefaultPlebStopThreshold = Money.Coins(0.01m);

	internal KeyManager(BitcoinEncryptedSecretNoEC? encryptedSecret, byte[]? chainCode, HDFingerprint? masterFingerprint, ExtPubKey extPubKey, ExtPubKey? taprootExtPubKey, int? minGapLimit, BlockchainState blockchainState, string? filePath = null, KeyPath? segwitAccountKeyPath = null, KeyPath? taprootAccountKeyPath = null)
	{
		EncryptedSecret = encryptedSecret;
		ChainCode = chainCode;
		MasterFingerprint = masterFingerprint;
		SegwitExtPubKey = Guard.NotNull(nameof(extPubKey), extPubKey);
		TaprootExtPubKey = taprootExtPubKey;

		MinGapLimit = Math.Max(AbsoluteMinGapLimit, minGapLimit ?? 0);

		_blockchainState = blockchainState;

		SegwitAccountKeyPath = segwitAccountKeyPath ?? GetAccountKeyPath(_blockchainState.Network, ScriptPubKeyType.Segwit);
		SegwitExternalKeyGenerator = new HdPubKeyGenerator(SegwitExtPubKey.Derive(0), SegwitAccountKeyPath.Derive(0), MinGapLimit);
		_segwitInternalKeyGenerator = new HdPubKeyGenerator(SegwitExtPubKey.Derive(1), SegwitAccountKeyPath.Derive(1), MinGapLimit);

		TaprootAccountKeyPath = taprootAccountKeyPath ?? GetAccountKeyPath(_blockchainState.Network, ScriptPubKeyType.TaprootBIP86);
		if (TaprootExtPubKey is { })
		{
			TaprootExternalKeyGenerator = new HdPubKeyGenerator(TaprootExtPubKey.Derive(0), TaprootAccountKeyPath.Derive(0), MinGapLimit);
			_taprootInternalKeyGenerator = new HdPubKeyGenerator(TaprootExtPubKey.Derive(1), TaprootAccountKeyPath.Derive(1), MinGapLimit);
		}
		SetFilePath(filePath);

		ToFile();
	}

	public KeyManager(BitcoinEncryptedSecretNoEC encryptedSecret, byte[] chainCode, string password, Network network)
	{
		_blockchainState = new BlockchainState(network);

		password ??= "";

		MinGapLimit = AbsoluteMinGapLimit;

		EncryptedSecret = Guard.NotNull(nameof(encryptedSecret), encryptedSecret);
		ChainCode = Guard.NotNull(nameof(chainCode), chainCode);
		var extKey = new ExtKey(encryptedSecret.GetKey(password), chainCode);

		MasterFingerprint = extKey.Neuter().PubKey.GetHDFingerPrint();

		SegwitAccountKeyPath = GetAccountKeyPath(network, ScriptPubKeyType.Segwit);
		SegwitExtPubKey = extKey.Derive(SegwitAccountKeyPath).Neuter();

		TaprootAccountKeyPath = GetAccountKeyPath(network, ScriptPubKeyType.TaprootBIP86);
		TaprootExtPubKey = extKey.Derive(TaprootAccountKeyPath).Neuter();

		SegwitExternalKeyGenerator = new HdPubKeyGenerator(SegwitExtPubKey.Derive(0), SegwitAccountKeyPath.Derive(0), MinGapLimit);
		_segwitInternalKeyGenerator = new HdPubKeyGenerator(SegwitExtPubKey.Derive(1), SegwitAccountKeyPath.Derive(1), MinGapLimit);
		TaprootExternalKeyGenerator = new HdPubKeyGenerator(TaprootExtPubKey.Derive(0), TaprootAccountKeyPath.Derive(0), MinGapLimit);
		_taprootInternalKeyGenerator = new HdPubKeyGenerator(TaprootExtPubKey.Derive(1), TaprootAccountKeyPath.Derive(1), MinGapLimit);
	}

	public static KeyPath GetAccountKeyPath(Network network, ScriptPubKeyType scriptPubKeyType) =>
		new((network.Name, scriptPubKeyType) switch
		{
			("TestNet4", ScriptPubKeyType.Segwit) => "m/84h/1h/0h",
			("RegTest", ScriptPubKeyType.Segwit) => "m/84h/0h/0h",
			("Main", ScriptPubKeyType.Segwit) => "m/84h/0h/0h",
			("TestNet4", ScriptPubKeyType.TaprootBIP86) => "m/86h/1h/0h",
			("RegTest", ScriptPubKeyType.TaprootBIP86) => "m/86h/0h/0h",
			("Main", ScriptPubKeyType.TaprootBIP86) => "m/86h/0h/0h",
			_ => throw new ArgumentException($"Unknown account for network '{network}' and script type '{scriptPubKeyType}'.")
		});

	public WpkhDescriptors GetOutputDescriptors(string password, Network network)
	{
		if (!MasterFingerprint.HasValue)
		{
			throw new InvalidOperationException($"{nameof(MasterFingerprint)} is not defined.");
		}

		return WpkhOutputDescriptorHelper.GetOutputDescriptors(network, MasterFingerprint.Value, GetMasterExtKey(password), SegwitAccountKeyPath);
	}

	#region Properties

	/// <remarks><c>null</c> if the watch-only mode is on.</remarks>
	public BitcoinEncryptedSecretNoEC? EncryptedSecret { get; }

	/// <remarks><c>null</c> if the watch-only mode is on.</remarks>
	public byte[]? ChainCode { get; }

	public HDFingerprint? MasterFingerprint { get; private set; }

	public ExtPubKey SegwitExtPubKey { get; }

	public ExtPubKey? TaprootExtPubKey { get; private set; }

	public int MinGapLimit { get; private set; }

	public KeyPath SegwitAccountKeyPath { get; private set; }

	public KeyPath TaprootAccountKeyPath { get; private set; }

	private readonly BlockchainState _blockchainState;

	public bool PreferPsbtWorkflow { get; set; }

	public bool AutoCoinJoin { get; set; } = DefaultAutoCoinjoin;

	/// <summary>
	/// Won't coinjoin automatically if the confirmed wallet balance is below this.
	/// </summary>
	public Money PlebStopThreshold { get; set; } = DefaultPlebStopThreshold;

	public string? Icon { get; private set; }

	public int AnonScoreTarget { get; set; } = PrivacyProfiles.DefaultProfile.AnonScoreTarget;

	public bool NonPrivateCoinIsolation { get; set; } = PrivacyProfiles.DefaultProfile.NonPrivateCoinIsolation;

	public ScriptPubKeyType DefaultReceiveScriptType { get; set; } = ScriptPubKeyType.Segwit;

	public PreferredScriptPubKeyType ChangeScriptPubKeyType { get; set; } = PreferredScriptPubKeyType.Unspecified.Instance;

	public SendWorkflow DefaultSendWorkflow { get; set; } = SendWorkflow.Automatic;

	public List<OutPoint> ExcludedCoinsFromCoinJoin { get; private set; } = new();

	public string? FilePath { get; private set; }

	[MemberNotNullWhen(returnValue: false, nameof(EncryptedSecret))]
	[MemberNotNullWhen(returnValue: false, nameof(ChainCode))]
	public bool IsWatchOnly => EncryptedSecret is null;

	[MemberNotNullWhen(returnValue: true, nameof(MasterFingerprint))]
	public bool IsHardwareWallet => EncryptedSecret is null && MasterFingerprint is not null;

	public IEnumerable<ScriptPubKeyType> AvailableScriptPubKeyTypes => TaprootExtPubKey is null
		? [ScriptPubKeyType.Segwit]
		: [ScriptPubKeyType.Segwit, ScriptPubKeyType.TaprootBIP86];

	private readonly HdPubKeyCache _hdPubKeyCache = new();

	// `_criticalStateLock` is aimed to synchronize read/write access to the "critical" properties:
	// keys (stored in the `_hdPubKeyCache`), minGapLimit, secrets, height, network.
	private readonly object _criticalStateLock = new();

	#endregion Properties

	private HdPubKeyGenerator SegwitExternalKeyGenerator { get; set; }
	private readonly HdPubKeyGenerator _segwitInternalKeyGenerator;
	private HdPubKeyGenerator? TaprootExternalKeyGenerator { get; set; }
	private readonly HdPubKeyGenerator? _taprootInternalKeyGenerator;

	public string WalletName => string.IsNullOrWhiteSpace(FilePath) ? "" : Path.GetFileNameWithoutExtension(FilePath);

	public static KeyManager CreateNew(out Mnemonic mnemonic, string password, Network network, string? filePath = null)
	{
		mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);
		return CreateNew(mnemonic, password, network, filePath);
	}

	public static KeyManager CreateNew(Mnemonic mnemonic, string password, Network network, string? filePath = null)
	{
		password ??= "";
		var seed = mnemonic.DeriveSeed(password);
		return CreateNew(seed, password, network, filePath);
	}

	public static KeyManager CreateNew(Share[] shares, string password, Network network, string? filePath = null)
	{
		password ??= "";
		var seed = Shamir.Combine(shares, password);
		return CreateNew(seed, password, network, filePath);
	}

	private static KeyManager CreateNew(byte[] seed, string password, Network network, string? filePath = null)
	{
		var extKey = ExtKey.CreateFromSeed(seed);
		var encryptedSecret = extKey.PrivateKey.GetEncryptedBitcoinSecret(password, Network.Main);

		HDFingerprint masterFingerprint = extKey.Neuter().PubKey.GetHDFingerPrint();
		BlockchainState blockchainState = new(network);
		KeyPath segwitAccountKeyPath = GetAccountKeyPath(network, ScriptPubKeyType.Segwit);
		ExtPubKey segwitExtPubKey = extKey.Derive(segwitAccountKeyPath).Neuter();

		KeyPath taprootAccountKeyPath = GetAccountKeyPath(network, ScriptPubKeyType.TaprootBIP86);
		ExtPubKey taprootExtPubKey = extKey.Derive(taprootAccountKeyPath).Neuter();

		return new KeyManager(encryptedSecret, extKey.ChainCode, masterFingerprint, segwitExtPubKey, taprootExtPubKey, AbsoluteMinGapLimit, blockchainState, filePath, segwitAccountKeyPath, taprootAccountKeyPath);
	}


	public static KeyManager CreateNewWatchOnly(ExtPubKey segwitExtPubKey, ExtPubKey taprootExtPubKey, string? filePath = null, int? minGapLimit = null)
	{
		return new KeyManager(null, null, null, segwitExtPubKey, taprootExtPubKey, minGapLimit ?? AbsoluteMinGapLimit, new BlockchainState(), filePath);
	}

	public static KeyManager CreateNewHardwareWalletWatchOnly(HDFingerprint masterFingerprint, ExtPubKey segwitExtPubKey, ExtPubKey? taprootExtPubKey, Network network, string? filePath = null)
	{
		return new KeyManager(null, null, masterFingerprint, segwitExtPubKey, taprootExtPubKey, AbsoluteMinGapLimit, new BlockchainState(network), filePath);
	}

	public static KeyManager Recover(Mnemonic mnemonic, string password, Network network, KeyPath swAccountKeyPath, KeyPath? trAccountKeyPath = null, string? filePath = null, int minGapLimit = AbsoluteMinGapLimit)
	{
		Guard.NotNull(nameof(mnemonic), mnemonic);
		password ??= "";
		var seed = mnemonic.DeriveSeed(password);
		return Recover(seed, password, network, swAccountKeyPath, trAccountKeyPath, filePath, minGapLimit);
	}

	public static KeyManager Recover(Share[] shares, string password, Network network, KeyPath swAccountKeyPath, KeyPath? trAccountKeyPath = null, string? filePath = null, int minGapLimit = AbsoluteMinGapLimit)
	{
		password ??= "";
		var seed = Shamir.Combine(shares, password);
		return Recover(seed, password, network, swAccountKeyPath, trAccountKeyPath, filePath, minGapLimit);
	}

	private static KeyManager Recover(byte[] seed, string password, Network network, KeyPath swAccountKeyPath, KeyPath? trAccountKeyPath = null, string? filePath = null, int minGapLimit = AbsoluteMinGapLimit)
	{
		ExtKey extKey = ExtKey.CreateFromSeed(seed);
		var encryptedSecret = extKey.PrivateKey.GetEncryptedBitcoinSecret(password, Network.Main);

		HDFingerprint masterFingerprint = extKey.Neuter().PubKey.GetHDFingerPrint();

		KeyPath segwitAccountKeyPath = swAccountKeyPath ?? GetAccountKeyPath(network, ScriptPubKeyType.Segwit);
		ExtPubKey segwitExtPubKey = extKey.Derive(segwitAccountKeyPath).Neuter();
		KeyPath taprootAccountKeyPath = trAccountKeyPath ?? GetAccountKeyPath(network, ScriptPubKeyType.TaprootBIP86);
		ExtPubKey taprootExtPubKey = extKey.Derive(taprootAccountKeyPath).Neuter();

		var km = new KeyManager(encryptedSecret, extKey.ChainCode, masterFingerprint, segwitExtPubKey, taprootExtPubKey, minGapLimit, new BlockchainState(network), filePath, segwitAccountKeyPath, taprootAccountKeyPath);
		km.AssertCleanKeysIndexed();
		return km;
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

		KeyManager km = JsonDecoder.FromString(jsonString, Decoder)
			?? throw new DataException($"Wallet file at: `{filePath}` is not a valid wallet file or it is corrupted.");

		km.SetFilePath(filePath);

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

	internal HdPubKey GenerateNewKey(LabelsArray labels, KeyState keyState, bool isInternal, ScriptPubKeyType scriptPubKeyType = ScriptPubKeyType.Segwit)
	{
		var hdPubKeyRegistry = GetHdPubKeyGenerator(isInternal, scriptPubKeyType)
							   ?? throw new NotSupportedException($"Script type '{scriptPubKeyType}' is not supported.");

		lock (_criticalStateLock)
		{
			var view = _hdPubKeyCache.GetView(hdPubKeyRegistry.KeyPath);
			var (keyPath, extPubKey) = hdPubKeyRegistry.GenerateNewKey(view);
			var hdPubKey = new HdPubKey(extPubKey.PubKey, keyPath, labels, keyState);
			_hdPubKeyCache.AddKey(hdPubKey, scriptPubKeyType);
			return hdPubKey;
		}
	}

	public HdPubKey GetNextReceiveKey(LabelsArray labels, ScriptPubKeyType scriptPubKeyType = ScriptPubKeyType.Segwit)
	{
		lock (_criticalStateLock)
		{
			var newKey = scriptPubKeyType switch
			{
				ScriptPubKeyType.Segwit => GetNextReceiveSegwitKey(),
				ScriptPubKeyType.TaprootBIP86 => GetNextReceiveTaprootKey(),
				_ => throw new NotSupportedException($"Script type '{scriptPubKeyType}' is not supported.")
			};

			newKey.SetLabel(labels);

			ToFile();
			return newKey;
		}
	}

	private HdPubKey GetNextReceiveSegwitKey()
	{
		var (newKey, newlyGeneratedKeySet, newHdPubKeyGenerator) = GetNextReceiveKey(SegwitExternalKeyGenerator);
		SegwitExternalKeyGenerator = newHdPubKeyGenerator;
		_hdPubKeyCache.AddRangeKeys(newlyGeneratedKeySet);
		return newKey;
	}

	private HdPubKey GetNextReceiveTaprootKey()
	{
		if (TaprootExternalKeyGenerator is not { } nonNullTaprootExternalKeyGenerator)
		{
			throw new NotSupportedException("Taproot is not supported in this wallet.");
		}
		var (newKey, newlyGeneratedKeySet, newHdPubKeyGenerator) = GetNextReceiveKey(nonNullTaprootExternalKeyGenerator);
		TaprootExternalKeyGenerator = newHdPubKeyGenerator;
		_hdPubKeyCache.AddRangeKeys(newlyGeneratedKeySet);
		return newKey;
	}

	private (HdPubKey, HdPubKey[], HdPubKeyGenerator) GetNextReceiveKey(HdPubKeyGenerator hdPubKeyGenerator)
	{
		// Find the next clean external key with empty label.
		var externalView = _hdPubKeyCache.GetView(hdPubKeyGenerator.KeyPath);
		if (externalView.CleanKeys.FirstOrDefault(x => x.Labels.IsEmpty) is { } cachedKey)
		{
			return (cachedKey, Array.Empty<HdPubKey>(), hdPubKeyGenerator);
		}

		var newHdPubKeyGenerator = hdPubKeyGenerator with { MinGapLimit = hdPubKeyGenerator.MinGapLimit + 1 };
		var newHdPubKeys = newHdPubKeyGenerator.AssertCleanKeysIndexed(externalView).Select(CreateHdPubKey).ToArray();

		var newKey = newHdPubKeys.First();
		return (newKey, newHdPubKeys, newHdPubKeyGenerator);
	}

	public HdPubKey GetNextChangeKey() =>
		GetKeys(x =>
			x.KeyState == KeyState.Clean &&
			x.IsInternal &&
			MatchesChangeScriptPubKeyType(x))
			.First();

	public IEnumerable<HdPubKey> GetNextCoinJoinKeys() =>
		GetKeys(x =>
				x.KeyState == KeyState.Locked &&
				x.IsInternal == true);

	private bool MatchesChangeScriptPubKeyType(HdPubKey hd) =>
		ChangeScriptPubKeyType switch
		{
			PreferredScriptPubKeyType.Unspecified => true,
			PreferredScriptPubKeyType.Specified scriptType => hd.FullKeyPath.GetScriptTypeFromKeyPath() == scriptType.ScriptType,
			_ => throw new ArgumentOutOfRangeException()
		};

	public IEnumerable<HdPubKey> GetKeys(Func<HdPubKey, bool>? wherePredicate)
	{
		// BIP44-ish derivation scheme
		// m / purpose' / coin_type' / account' / change / address_index
		lock (_criticalStateLock)
		{
			AssertCleanKeysIndexed();
			var predicate = wherePredicate ?? (_ => true);
			return _hdPubKeyCache.HdPubKeys.Where(predicate).OrderBy(x => x.Index);
		}
	}

	public IEnumerable<HdPubKey> GetKeys(KeyState? keyState = null, bool? isInternal = null) =>
		(keyState, isInternal) switch
		{
			(null, null) => GetKeys(x => true),
			(null, { } i) => GetKeys(x => x.IsInternal == i),
			({ } k, null) => GetKeys(x => x.KeyState == k),
			({ } k, { } i) => GetKeys(x => x.IsInternal == i && x.KeyState == k)
		};

	/// <summary>
	/// This function can only be called for wallet synchronization.
	/// It's unsafe because it doesn't assert that the GapLimit is respected.
	/// GapLimit should be enforced whenever a transaction is discovered.
	/// </summary>
	public IEnumerable<byte[]> UnsafeGetSynchronizationInfos(bool isBIP158)
	{
		lock (_criticalStateLock)
		{
			return _hdPubKeyCache.Select(x => GetScriptPubKeyBytes(x));
		}

		byte[] GetScriptPubKeyBytes(HdPubKeyInfo hdPubKeyInfo) =>
			isBIP158
				? hdPubKeyInfo.ScriptPubKeyBytes  // BIP158 compatible script to test against filters
				: hdPubKeyInfo.CompressedScriptPubKeyBytes; // Legacy Wasabi indexer scripts used to build filters
 	}

	public bool TryGetKeyForScriptPubKey(Script scriptPubKey, [NotNullWhen(true)] out HdPubKey? hdPubKey)
	{
		lock (_criticalStateLock)
		{
			return _hdPubKeyCache.TryGetPubKey(scriptPubKey, out hdPubKey);
		}
	}

	public IEnumerable<Key> GetSecrets(string password, params Script[] scripts)
	{
		ExtKey extKey = GetMasterExtKey(password);

		lock (_criticalStateLock)
		{
			foreach (HdPubKey key in GetKeys(x =>
				scripts.Contains(x.P2wpkhScript)
				|| scripts.Contains(x.P2Taproot)))
			{
				yield return extKey.Derive(key.FullKeyPath).PrivateKey;
			}
		}
	}

	private (int PasswordHash, ExtKey MasterKey)? MasterKeyAndPasswordHash { get; set; }

	public ExtKey GetMasterExtKey(string password)
	{
		if (IsWatchOnly)
		{
			throw new SecurityException("This is a watch-only wallet.");
		}

		password ??= "";

		var passwordHash = password.GetHashCode();

		if (MasterKeyAndPasswordHash is { MasterKey: var masterKey, PasswordHash: var storedPasswordHash })
		{
			if (passwordHash != storedPasswordHash)
			{
				throw new SecurityException("Invalid passphrase.");
			}

			return masterKey;
		}

		try
		{
			Key secret = EncryptedSecret.GetKey(password);
			var extKey = new ExtKey(secret, ChainCode);

			// Backwards compatibility:
			MasterFingerprint ??= secret.PubKey.GetHDFingerPrint();
			DeriveTaprootExtPubKey(extKey);

			MasterKeyAndPasswordHash = (passwordHash, extKey);

			return extKey;
		}
		catch (SecurityException ex)
		{
			throw new SecurityException("Invalid passphrase.", ex);
		}
	}

	private void DeriveTaprootExtPubKey(ExtKey extKey)
	{
		if (TaprootExtPubKey is null)
		{
			TaprootAccountKeyPath = GetAccountKeyPath(GetNetwork(), ScriptPubKeyType.TaprootBIP86);
			TaprootExtPubKey = extKey.Derive(TaprootAccountKeyPath).Neuter();
		}
	}

	public void SetKeyState(KeyState newKeyState, HdPubKey hdPubKey)
	{
		if (hdPubKey.KeyState == newKeyState)
		{
			return;
		}

		hdPubKey.SetKeyState(newKeyState);
		if (newKeyState is KeyState.Locked or KeyState.Used)
		{
			var keySource = GetHdPubKeyGenerator(hdPubKey.IsInternal, hdPubKey.FullKeyPath.GetScriptTypeFromKeyPath());

			// This can happen after downgrading to pre-taproot wasabi version the switching back to a supporting
			// version so taproot keys are detected. However, the user has not login yet so taprootextpubkey is
			// not derived yet (because pre-taproot wasabi do not serialize fields that it doesn't know)
			if (keySource is { })
			{
				var view = _hdPubKeyCache.GetView(keySource.KeyPath);
				_hdPubKeyCache.AddRangeKeys(keySource.AssertCleanKeysIndexed(view).Select(CreateHdPubKey));
			}
		}
	}

	private HdPubKeyGenerator? GetHdPubKeyGenerator(bool isInternal, ScriptPubKeyType scriptPubKeyType) =>
		(isInternal, scriptPubKeyType) switch
		{
			(true, ScriptPubKeyType.Segwit) => _segwitInternalKeyGenerator,
			(false, ScriptPubKeyType.Segwit) => SegwitExternalKeyGenerator,
			(true, ScriptPubKeyType.TaprootBIP86) => _taprootInternalKeyGenerator,
			(false, ScriptPubKeyType.TaprootBIP86) => TaprootExternalKeyGenerator,
			_ => throw new NotSupportedException($"There is not available generator for '{scriptPubKeyType}.")
		};

	private IEnumerable<HdPubKey> AssertCleanKeysIndexed()
	{
		var keys = new[]
			{
				_segwitInternalKeyGenerator,
				SegwitExternalKeyGenerator,
				_taprootInternalKeyGenerator,
				TaprootExternalKeyGenerator
			}
			.Where(x => x is not null)
			.SelectMany(gen => gen!.AssertCleanKeysIndexed(_hdPubKeyCache.GetView(gen.KeyPath)))
			.Select(CreateHdPubKey);

		return _hdPubKeyCache.AddRangeKeys(keys);
	}

	/// <summary>
	/// Make sure there's always locked internal keys generated and indexed.
	/// </summary>
	public void AssertLockedInternalKeysIndexedAndPersist(int howMany, bool preferTaproot)
	{
		if (AssertLockedInternalKeysIndexed(howMany, preferTaproot))
		{
			ToFile();
		}
	}

	public bool AssertLockedInternalKeysIndexed(int howMany, bool preferTaproot)
	{
		var hdPubKeyGenerator = (_taprootInternalKeyGenerator, preferTaproot) switch
		{
			({ }, true) => _taprootInternalKeyGenerator,
			_ => _segwitInternalKeyGenerator
		};

		Guard.InRangeAndNotNull(nameof(howMany), howMany, 0, hdPubKeyGenerator.MinGapLimit);
		var internalView = _hdPubKeyCache.GetView(hdPubKeyGenerator.KeyPath);
		var lockedKeyCount = internalView.LockedKeys.Count();
		var missingLockedKeys = Math.Max(howMany - lockedKeyCount, 0);

		_hdPubKeyCache.AddRangeKeys(hdPubKeyGenerator.AssertCleanKeysIndexed(internalView).Select(CreateHdPubKey));

		var availableCandidates = _hdPubKeyCache
			.GetView(hdPubKeyGenerator.KeyPath)
			.CleanKeys
			.Where(x => x.Labels.IsEmpty)
			.Take(missingLockedKeys)
			.ToList();

		foreach (var hdPubKeys in availableCandidates)
		{
			SetKeyState(KeyState.Locked, hdPubKeys);
		}

		return availableCandidates.Count > 0;
	}

	public void ToFile()
	{
		if (FilePath is { } filePath)
		{
			ToFile(filePath);
		}
	}

	public void ToFile(string filePath)
	{
		string jsonString = string.Empty;

		lock (_criticalStateLock)
		{
			jsonString = JsonEncoder.ToReadableString(this, EncodeKeyManager);
		}

		IoHelpers.EnsureContainingDirectoryExists(filePath);

		SafeIoManager safeIoManager = new(filePath);
		safeIoManager.WriteAllText(jsonString, Encoding.UTF8);
	}

	#region _blockchainState

	public Height GetBestHeight()
	{
		lock (_criticalStateLock)
		{
			return _blockchainState.Height;
		}
	}

	public Network GetNetwork()
	{
		return _blockchainState.Network;
	}

	public void SetBestHeight(Height height, bool toFile = true)
	{
		lock (_criticalStateLock)
		{
			_blockchainState.Height = height;
			if (toFile)
			{
				ToFile();
			}
		}
	}

	public void SetMaxBestHeight(Height newHeight)
	{
		lock (_criticalStateLock)
		{
			var prevHeight = _blockchainState.Height;
			if (newHeight < prevHeight)
			{
				SetBestHeight(newHeight);
				Logger.LogWarning($"Wallet ({WalletName}) height has been set back by {prevHeight - (int)newHeight}. From {prevHeight} to {newHeight}.");
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

	public void AssertNetworkOrClearBlockState(Network expectedNetwork)
	{
		lock (_criticalStateLock)
		{
			var lastNetwork = _blockchainState.Network;
			if (lastNetwork is null || lastNetwork != expectedNetwork)
			{
				_blockchainState.Network = expectedNetwork;
				SetBestHeight(0);

				if (lastNetwork is { })
				{
					Logger.LogWarning($"Wallet is opened on {expectedNetwork}. Last time it was opened on {lastNetwork}.");
				}
				Logger.LogInfo("Blockchain cache is cleared.");
			}
		}
	}

	#endregion _blockchainState

	private static HdPubKey CreateHdPubKey((KeyPath KeyPath, ExtPubKey ExtPubKey) x) =>
		new(x.ExtPubKey.PubKey, x.KeyPath, Analysis.Clustering.LabelsArray.Empty, KeyState.Clean);

	internal void SetExcludedCoinsFromCoinJoin(IEnumerable<OutPoint> excludedOutpoints)
	{
		ExcludedCoinsFromCoinJoin = excludedOutpoints.ToList();
		ToFile();
	}

	private static JsonNode EncodeKeyManager(KeyManager keyManager) =>
		Encode.Object([
			("EncryptedSecret", Encode.Optional(keyManager.EncryptedSecret, Encode.BitcoinEncryptedSecretNoEC)),
			("ChainCode", Encode.Optional(keyManager.ChainCode, Encode.ChainCode)),
			("MasterFingerprint", Encode.Optional(keyManager.MasterFingerprint, Encode.HDFingerprint)),
			("ExtPubKey", Encode.ExtPubKey(keyManager.SegwitExtPubKey)),
			("TaprootExtPubKey", Encode.Optional(keyManager.TaprootExtPubKey, Encode.ExtPubKey)),
			("MinGapLimit", Encode.Int(Math.Max(keyManager.SegwitExternalKeyGenerator.MinGapLimit, keyManager.TaprootExternalKeyGenerator?.MinGapLimit ?? 0))),
			("AccountKeyPath", Encode.KeyPath(keyManager.SegwitAccountKeyPath)),
			("TaprootAccountKeyPath", Encode.KeyPath(keyManager.TaprootAccountKeyPath)),
			("BlockchainState", Encode.BlockchainState(keyManager._blockchainState)),
			("PreferPsbtWorkflow", Encode.Bool(keyManager.PreferPsbtWorkflow)),
			("AutoCoinJoin", Encode.Bool(keyManager.AutoCoinJoin)),
			("PlebStopThreshold", Encode.MoneyBitcoins(keyManager.PlebStopThreshold)),
			("Icon", Encode.Optional(keyManager.Icon, Encode.String)),
			("AnonScoreTarget", Encode.Int(keyManager.AnonScoreTarget)),
			("RedCoinIsolation", Encode.Bool(keyManager.NonPrivateCoinIsolation)),
			("DefaultReceiveScriptType", Encode.ScriptPubKeyType(keyManager.DefaultReceiveScriptType)),
			("ChangeScriptPubKeyType", Encode.PreferredScriptPubKeyType(keyManager.ChangeScriptPubKeyType)),
			("DefaultSendWorkflow", Encode.SendWorkflow(keyManager.DefaultSendWorkflow)),
			("ExcludedCoinsFromCoinJoin", Encode.Array(keyManager.ExcludedCoinsFromCoinJoin.Select(Encode.Outpoint))),
			("HdPubKeys", Encode.Array(keyManager._hdPubKeyCache.HdPubKeys.Select(Encode.HdPubKey)))
		]);

	private static readonly Decoder<KeyManager> Decoder =
		Decode.Object(get =>
		{
			var fingerprint = Decode.Field("MasterFingerprint", Decode.HDFingerprint)(get.Value).Match(v => v, _ => (HDFingerprint?)null);
			var km = new KeyManager(
				get.Optional("EncryptedSecret", Decode.BitcoinEncryptedSecretNoEC),
				get.Optional("ChainCode", Decode.ByteArray),
				fingerprint,

				get.Required("ExtPubKey", Decode.ExtPubKey),
				get.Optional("TaprootExtPubKey", Decode.ExtPubKey),
				get.Optional("MinGapLimit", Decode.Int),
				get.Required("BlockchainState", Decode.BlockchainState),
				(string?) "",
				get.Optional("AccountKeyPath", Decode.KeyPath),
				get.Optional("TaprootAccountKeyPath", Decode.KeyPath)
			)
			{
				PreferPsbtWorkflow = get.Optional("PreferPsbtWorkflow", Decode.Bool, false),
				AutoCoinJoin = get.Optional("AutoCoinJoin", Decode.Bool, false),
				PlebStopThreshold = get.Optional("PlebStopThreshold", Decode.MoneyBitcoins) ?? DefaultPlebStopThreshold,
				Icon = get.Optional("Icon", Decode.String),
				AnonScoreTarget = get.Optional("AnonScoreTarget", Decode.Int, 10),
				NonPrivateCoinIsolation = get.Optional("RedCoinIsolation", Decode.Bool, false),
				DefaultReceiveScriptType = get.Optional("DefaultReceiveScriptType", Decode.ScriptPubKeyType, ScriptPubKeyType.Segwit),
				ChangeScriptPubKeyType = get.Optional("ChangeScriptPubKeyType", Decode.PreferredScriptPubKeyType) ?? PreferredScriptPubKeyType.Unspecified.Instance,
				DefaultSendWorkflow = get.Optional("DefaultSendWorkflow", Decode.SendWorkflow, SendWorkflow.Automatic),
				ExcludedCoinsFromCoinJoin = get.Optional("ExcludedCoinsFromCoinJoin", Decode.Array(Decode.OutPoint))?.ToList() ?? []
			};
			km._hdPubKeyCache.AddRangeKeys(get.Required("HdPubKeys", Decode.Array(Decode.HdPubKey)));
			return km;
		});
}

public static class KeyPathExtensions
{
	public static ScriptPubKeyType GetScriptTypeFromKeyPath(this KeyPath keyPath) =>
		keyPath.ToBytes().First() switch
		{
			84 => ScriptPubKeyType.Segwit,
			86 => ScriptPubKeyType.TaprootBIP86,
			_ => ScriptPubKeyType.Segwit // User can specify a specify whatever (like m/999'/999'/999')
										 // throw new NotSupportedException("Unknown script type.")
		};
}

public static class HdPubKeyExtensions
{
	public static BitcoinAddress GetAddress(this HdPubKey me, Network network) =>
		me.PubKey.GetAddress(me.FullKeyPath.GetScriptTypeFromKeyPath(), network);

	public static Script GetAssumedScriptPubKey(this HdPubKey me) =>
		me.PubKey.GetScriptPubKey(me.FullKeyPath.GetScriptTypeFromKeyPath());
}

using NBitcoin;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Bases;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Helpers;
using WalletWasabi.JsonConverters;
using WalletWasabi.Models;

namespace WalletWasabi.Blockchain.Keys;

[JsonObject(MemberSerialization.OptIn)]
public class HdPubKey : NotifyPropertyChangedBase, IEquatable<HdPubKey>
{
	public const int DefaultHighAnonymitySet = int.MaxValue;

	private double _anonymitySet = DefaultHighAnonymitySet;
	private Cluster _cluster;

	public HdPubKey(PubKey pubKey, KeyPath fullKeyPath, LabelsArray labels, KeyState keyState)
	{
		PubKey = Guard.NotNull(nameof(pubKey), pubKey);
		FullKeyPath = Guard.NotNull(nameof(fullKeyPath), fullKeyPath);
		_cluster = new Cluster(this);
		Labels = labels;
		Cluster.UpdateLabels();
		KeyState = keyState;

		P2pkScript = PubKey.ScriptPubKey;
		P2pkhScript = PubKey.GetScriptPubKey(ScriptPubKeyType.Legacy);
		P2wpkhScript = PubKey.GetScriptPubKey(ScriptPubKeyType.Segwit);
		P2shOverP2wpkhScript = PubKey.GetScriptPubKey(ScriptPubKeyType.SegwitP2SH);
		P2Taproot = PubKey.GetScriptPubKey(ScriptPubKeyType.TaprootBIP86);

		PubKeyHash = PubKey.Hash;
		HashCode = PubKeyHash.GetHashCode();

		Index = (int)FullKeyPath.Indexes[4];
		NonHardenedKeyPath = new KeyPath(FullKeyPath[3], FullKeyPath[4]);

		int change = (int)FullKeyPath.Indexes[3];
		if (change == 0)
		{
			IsInternal = false;
		}
		else if (change == 1)
		{
			IsInternal = true;
		}
		else
		{
			throw new ArgumentException(nameof(FullKeyPath));
		}
	}

	public Cluster Cluster
	{
		get => _cluster;
		set => RaiseAndSetIfChanged(ref _cluster, value);
	}

	public HashSet<uint256> OutputAnonSetReasons { get; } = new();

	public double AnonymitySet
	{
		get => _anonymitySet;
		private set => RaiseAndSetIfChanged(ref _anonymitySet, value);
	}

	public HashSet<SmartCoin> Coins { get; } = new HashSet<SmartCoin>();

	[JsonProperty(Order = 1)]
	[JsonConverter(typeof(PubKeyJsonConverter))]
	public PubKey PubKey { get; }

	[JsonProperty(Order = 2)]
	[JsonConverter(typeof(KeyPathJsonConverter))]
	public KeyPath FullKeyPath { get; }

	[JsonProperty(Order = 3, PropertyName = "Label")]
	[JsonConverter(typeof(LabelsArrayJsonConverter))]
	public LabelsArray Labels { get; private set; }

	[JsonProperty(Order = 4)]
	public KeyState KeyState { get; private set; }

	/// <summary>Height of the block where all coins associated with the key were spent, or <c>null</c> if not yet spent.</summary>
	/// <remarks>Value can be non-<c>null</c> only for <see cref="IsInternal">internal keys</see> as they should be used just once.</remarks>
	[JsonProperty(Order = 5)]
	[JsonConverter(typeof(HeightJsonConverter))]
	public Height? LatestSpendingHeight { get; set; }

	public Script P2pkScript { get; }
	public Script P2pkhScript { get; }
	public Script P2wpkhScript { get; }
	public Script P2shOverP2wpkhScript { get; }
	public Script P2Taproot { get; }

	public KeyId PubKeyHash { get; }

	public int Index { get; }
	public KeyPath NonHardenedKeyPath { get; }
	public bool IsInternal { get; }

	private int HashCode { get; }

	public void SetAnonymitySet(double anonset, uint256? outputAnonSetReason = null)
	{
		if (outputAnonSetReason is not null)
		{
			OutputAnonSetReasons.Add(outputAnonSetReason);
		}

		AnonymitySet = anonset;
	}

	public void SetLabel(LabelsArray labels, KeyManager? kmToFile = null)
	{
		if (Labels == labels)
		{
			return;
		}

		Labels = labels;
		Cluster.UpdateLabels();

		kmToFile?.ToFile();
	}

	public void SetKeyState(KeyState state, KeyManager? kmToFile = null)
	{
		if (KeyState == state)
		{
			return;
		}

		KeyState = state;

		kmToFile?.ToFile();
	}

	public BitcoinPubKeyAddress GetP2pkhAddress(Network network) => (BitcoinPubKeyAddress)PubKey.GetAddress(ScriptPubKeyType.Legacy, network);

	public BitcoinWitPubKeyAddress GetP2wpkhAddress(Network network) => (BitcoinWitPubKeyAddress)PubKey.GetAddress(ScriptPubKeyType.Segwit, network);

	public BitcoinScriptAddress GetP2shOverP2wpkhAddress(Network network) => (BitcoinScriptAddress)PubKey.GetAddress(ScriptPubKeyType.SegwitP2SH, network);

	public bool ContainsScript(Script scriptPubKey)
	{
		var scripts = new[]
		{
			P2pkScript,
			P2pkhScript,
			P2wpkhScript,
			P2shOverP2wpkhScript,
			P2Taproot
		};

		return scripts.Contains(scriptPubKey);
	}

	#region Equality

	public override bool Equals(object? obj) => Equals(obj as HdPubKey);

	public bool Equals(HdPubKey? other) => this == other;

	public override int GetHashCode() => HashCode;

	public static bool operator ==(HdPubKey? x, HdPubKey? y) => x?.PubKeyHash == y?.PubKeyHash;

	public static bool operator !=(HdPubKey? x, HdPubKey? y) => !(x == y);

	#endregion Equality
}

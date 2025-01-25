using System.Linq;
using System.Text.Json.Nodes;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Helpers;
using WalletWasabi.Models;

namespace WalletWasabi.Serialization;

public static partial class Encode
{
	public static JsonNode KeyPath(KeyPath kp) =>
		String(kp.ToString());

	public static JsonNode ByteArray(byte[] bytes) =>
		String(Convert.ToBase64String(bytes));

	public static JsonNode? ChainCode(byte[]? bytes) =>
		ByteArray(bytes);

	public static JsonNode PubKey(PubKey pk) =>
		String(pk.ToHex());

	public static JsonNode LabelsArray(LabelsArray a) =>
		String(a.ToString());

	public static JsonNode HdPubKey(HdPubKey hd) =>
		Object([
			("PubKey", PubKey(hd.PubKey)),
			("FullKeyPath", KeyPath(hd.FullKeyPath) ),
			("Label", LabelsArray(hd.Labels) ),
			("KeyState", Int((int)hd.KeyState) ),
		]);

	public static JsonNode HDFingerprint(HDFingerprint fp) =>
		String(fp.ToString());

	public static JsonNode BitcoinEncryptedSecretNoEC(BitcoinEncryptedSecretNoEC s) =>
		String(s.ToWif());

	public static JsonNode WalletHeight(Height height) =>
		String(Math.Max(0, height.Value - 101).ToString());

	public static JsonNode BlockchainState(BlockchainState s) =>
		Object([
			("Network", Network(s.Network)),
			("Height", WalletHeight(s.Height)),
			("TurboSyncHeight", WalletHeight(s.TurboSyncHeight))
		]);

	public static JsonNode CoinjoinSkipFactors(CoinjoinSkipFactors f) =>
		String(f.ToString());

	public static JsonNode RuntimeParams(RuntimeParams p) =>
		Object([
			("NetworkNodeTimeout", Int(p.NetworkNodeTimeout))
		]);
}


public static partial class Decode
{
	public static readonly Decoder<byte[]> ByteArray =
		String.Map(Convert.FromBase64String);

	public static readonly Decoder<HDFingerprint> HDFingerprint =
		String.Map(NBitcoin.HDFingerprint.Parse);

	public static readonly Decoder<Height> WalletHeight =
		Int.Map(h => new Height(h));

	public static readonly Decoder<KeyPath> KeyPath =
		String.Map(NBitcoin.KeyPath.Parse);

	public static readonly Decoder<PubKey> PubKey =
		String.Map(s => new PubKey(s));

	public static readonly Decoder<LabelsArray> LabelsArray =
		String.Map(s => new LabelsArray(s));

	public static readonly Decoder<HdPubKey> HdPubKey =
		Object(get => new HdPubKey(
			get.Required("PubKey", PubKey),
			get.Required("FullKeyPath", KeyPath),
			get.Required("Label", LabelsArray),
			get.Required("KeyState", Int.Map(x => (KeyState)x))
		));

	public static readonly Decoder<BlockchainState> BlockchainState =
		Object(get => new BlockchainState(
			get.Required("Network", Network),
			get.Required("Height", WalletHeight),
			get.Required("TurboSyncHeight", WalletHeight)
			));

	public static readonly Decoder<CoinjoinSkipFactors> CoinjoinSkipFactors =
		String.Map(WalletWasabi.Models.CoinjoinSkipFactors.FromString);

	public static readonly Decoder<BitcoinEncryptedSecretNoEC> BitcoinEncryptedSecretNoEC =
		String.Map(s => new BitcoinEncryptedSecretNoEC(s, NBitcoin.Network.Main));

	public static readonly Decoder<RuntimeParams> RuntimeParams =
		Object(get => new RuntimeParams{
			NetworkNodeTimeout = get.Required("NetworkNodeTimeout", Int)
		});
}

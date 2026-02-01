using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using NBitcoin;
using NBitcoin.Secp256k1;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Models;
using WalletWasabi.WabiSabi.Client.Banning;

namespace WalletWasabi.Serialization;

public static partial class Encode
{
	public static JsonNode KeyPath(KeyPath kp) =>
		String(kp.ToString());

	public static JsonNode ByteArray(byte[] bytes) =>
		String(Convert.ToBase64String(bytes));

	public static JsonNode? ChainCode(byte[]? bytes) =>
		Optional(bytes, ByteArray);

	public static JsonNode PubKey(PubKey pk) =>
		String(pk.ToHex());

	public static JsonNode ECPubKey(ECPubKey pk) =>
		ByteArray(pk.ToBytes());

	public static JsonNode LabelsArray(LabelsArray a) =>
		String(a.ToString());

	public static JsonNode HdPubKey(HdPubKey hd) =>
		hd.TweakData is null
			? HdPubKeyEncoder(hd)
			: SpPubKeyEncoder(hd);

	public static JsonNode HdPubKeyEncoder(HdPubKey hd) =>
		Object([
			("PubKey", PubKey(hd.PubKey)),
			("FullKeyPath", KeyPath(hd.FullKeyPath) ),
			("Label", LabelsArray(hd.Labels) ),
			("KeyState", Int((int)hd.KeyState) ),
		]);

	public static JsonNode SpPubKeyEncoder(HdPubKey hd) =>
		Object([
			("PubKey", PubKey(hd.PubKey)),
			("Label", LabelsArray(hd.Labels) ),
			("TweakData", ECPubKey(hd.TweakData!))
		]);

	public static JsonNode? HDFingerprint(HDFingerprint? fp) =>
		Optional(fp?.ToString(), String);

	public static JsonNode? BitcoinEncryptedSecretNoEC(BitcoinEncryptedSecretNoEC? s) =>
		Optional(s?.ToWif(), String);

	public static JsonNode WalletHeight(Height height) =>
		String(Math.Max(0, height.Value - 101).ToString());

	public static JsonNode BlockchainState(BlockchainState s) =>
		Object([
			("Network", Network(s.Network)),
			("Height", WalletHeight(s.Height)),
		]);

	public static JsonNode PreferredScriptPubKeyType(PreferredScriptPubKeyType t) =>
		t switch
		{
			PreferredScriptPubKeyType.Specified {ScriptType: NBitcoin.ScriptPubKeyType.Segwit} => String("Segwit"),
			PreferredScriptPubKeyType.Specified {ScriptType: NBitcoin.ScriptPubKeyType.TaprootBIP86} => String("Taproot"),
			PreferredScriptPubKeyType.Unspecified _ => String("Random"),
			_ => throw new ArgumentOutOfRangeException(nameof(t))
		};

	public static JsonNode ScriptPubKeyType(ScriptPubKeyType w) =>
		w switch
		{
			NBitcoin.ScriptPubKeyType.Segwit => "Segwit",
			NBitcoin.ScriptPubKeyType.TaprootBIP86 => "Taproot",
			_ => throw new ArgumentOutOfRangeException(nameof(w))
		};

	public static JsonNode SendWorkflow(SendWorkflow w) =>
		w switch
		{
			Models.SendWorkflow.Automatic => "Automatic",
			Models.SendWorkflow.Manual => "Manual",
			_ => throw new ArgumentOutOfRangeException(nameof(w))
		};

	public static JsonNode SerializableException(SerializableException e) =>
		Object([
			("ExceptionType", Optional(e.ExceptionType, String)),
			("Message", String(e.Message)),
			("StackTrace", Optional(e.StackTrace, String)),
			("InnerException", Optional(e.InnerException, SerializableException))
		]);

	public static JsonNode PrisonedCoinRecord(PrisonedCoinRecord r) =>
		Object([
			("Outpoint", Outpoint(r.Outpoint)),
			("BannedUntil", DatetimeOffset(r.BannedUntil))
		]);

	public static JsonNode ClientPrison(IEnumerable<PrisonedCoinRecord> p) =>
		Array(p.Select(PrisonedCoinRecord));
}


public static partial class Decode
{
	public static Decoder<byte[]> ByteArray =>
		String.Map(Convert.FromBase64String);

	public static Decoder<HDFingerprint> HDFingerprint =>
		String.Map(NBitcoin.HDFingerprint.Parse);

	private static Decoder<Height> WalletHeight =>
		Int.Map(h => new Height(h));

	public static Decoder<KeyPath> KeyPath =>
		String.Map(NBitcoin.KeyPath.Parse);

	private static Decoder<PubKey> PubKey =>
		String.Map(s => new PubKey(s));

	public static Decoder<ECPubKey> ECPubKey =>
		ByteArray.Map(a => NBitcoin.Secp256k1.ECPubKey.Create(a));

	private static Decoder<LabelsArray> LabelsArray =>
		String.Map(s => new LabelsArray(s));

	private static Decoder<HdPubKey> HdPubKeyDecoder =>
		Object(get => new HdPubKey(
			get.Required("PubKey", PubKey),
			get.Required("FullKeyPath", KeyPath),
			get.Required("Label", LabelsArray),
			get.Required("KeyState", Int.Map(x => (KeyState)x))
		));

	private static Decoder<HdPubKey> SpPubKeyDecoder =>
		Object(get => new HdPubKey(
			get.Required("PubKey", PubKey),
			get.Required("FullKeyPath", KeyPath),
			get.Required("Label", LabelsArray),
			get.Required("KeyState", Int.Map(x => (KeyState)x))
		));

	public static Decoder<HdPubKey> HdPubKey =>
		OneOf([HdPubKeyDecoder, SpPubKeyDecoder]);

	public static Decoder<BlockchainState> BlockchainState =>
		Object(get => new BlockchainState(
			get.Required("Network", Network),
			get.Required("Height", WalletHeight)
			));

	public static Decoder<BitcoinEncryptedSecretNoEC> BitcoinEncryptedSecretNoEC =>
		String.Map(s => new BitcoinEncryptedSecretNoEC(s, NBitcoin.Network.Main));

	public static Decoder<PreferredScriptPubKeyType> PreferredScriptPubKeyType =>
		String.Map(s => s switch
		{
			"Segwit" => new PreferredScriptPubKeyType.Specified(NBitcoin.ScriptPubKeyType.Segwit),
			"Taproot" => new PreferredScriptPubKeyType.Specified(NBitcoin.ScriptPubKeyType.TaprootBIP86),
			"Random" => (PreferredScriptPubKeyType)WalletWasabi.Models.PreferredScriptPubKeyType.Unspecified.Instance,
			_ => throw new Exception($"Unknown ScriptPubKeyType '{s}'")
		}).Catch();

	public static Decoder<ScriptPubKeyType> ScriptPubKeyType =>
		String.Map(s => s switch
		{
			"Segwit" => NBitcoin.ScriptPubKeyType.Segwit,
			"Taproot" => NBitcoin.ScriptPubKeyType.TaprootBIP86,
			_ => throw new Exception($"Unknown ScriptPubKeyType '{s}'")
		}).Catch();

	public static Decoder<SendWorkflow> SendWorkflow =>
		String.Map(s => s switch
		{
			"Automatic" => Models.SendWorkflow.Automatic,
			"Manual" => Models.SendWorkflow.Manual,
			_ => throw new Exception($"Unknown SendWorkflow value '{s}'")
		}).Catch();

	public static Decoder<SerializableException> SerializableException =>
		Object(get => new SerializableException(
			get.Required("ExceptionType", String),
			get.Required("Message", String),
			get.Required("StackTrace", String),
			get.Optional("InnerException", SerializableException)
		));

	public static Decoder<PrisonedCoinRecord> PrisonedCoinRecord =>
		Object(get => new PrisonedCoinRecord(
			get.Required("Outpoint", OutPoint),
			get.Required("BannedUntil", DateTimeOffset)
		));
}

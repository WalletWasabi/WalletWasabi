using System.Text.Json.Nodes;
using NBitcoin;
using NBitcoin.DataEncoders;
using WabiSabi.Helpers;
using WalletWasabi.Backend.Models.Responses;

namespace WalletWasabi.Serialization;

public static partial class Encode
{
	// Common primitive types
	private static JsonNode Hexadecimal(byte[] bytes) =>
		String(ByteHelpers.ToHex(bytes));

	public static JsonNode Network(Network network) =>
		String(network.Name);

	public static JsonNode UInt256(uint256 n) =>
		String(n.ToString());

	public static JsonNode Outpoint(OutPoint outPoint) =>
		Hexadecimal(outPoint.ToBytes());

	private static JsonNode Script(Script script) =>
		String(script.ToString());

	public static JsonNode MoneySatoshis(Money money) =>
		Int64(money.Satoshi);

	public static JsonNode MoneyBitcoins(Money money) =>
		String(money.ToString(fplus: false, trimExcessZero: true));

	private static JsonNode TxOut(TxOut txo) =>
		Object([
			("ScriptPubKey", Script(txo.ScriptPubKey)),
			("Value", MoneySatoshis(txo.Value))
		]);

	private static JsonNode Coin(Coin coin) =>
		Object([
			("Outpoint", Outpoint(coin.Outpoint)),
			("TxOut", TxOut(coin.TxOut))
		]);

	private static JsonNode FeeRate(FeeRate feeRate) =>
		MoneySatoshis(feeRate.FeePerK);

	private static JsonNode WitScript(WitScript witScript) =>
		Hexadecimal(witScript.ToBytes());
}

public static partial class Decode
{
	public static readonly Decoder<VersionsResponse> VersionsResponse =
		Object(get =>
			new VersionsResponse
			{
				BackendMajorVersion = get.Required("BackendMajorVersion", String),
				ClientVersion = get.Required("ClientVersion", String),
				CommitHash = get.Required("CommitHash", String),
				Ww1LegalDocumentsVersion = get.Required("Ww1LegalDocumentsVersion", String),
				Ww2LegalDocumentsVersion = get.Required("Ww2LegalDocumentsVersion", String)
			});

	public static readonly Decoder<uint256> UInt256 =
		String.Map(s => new uint256(s));

	public static readonly Decoder<Network> Network =
		String.AndThen(name =>
		{
			var network = NBitcoin.Network.GetNetwork(name);
			return network is { }
				? Succeed(network)
				: Fail<Network>($"'{name}' is not a valid network.");
		});

	public static readonly Decoder<byte[]> Hexadecimal =
		String.AndThen(hex =>
		{
			try
			{
				return Succeed(Encoders.Hex.DecodeData(hex));
			}
			catch (Exception e)
			{
				return Fail<byte[]>(e.Message);
			}
		});

	public static readonly Decoder<Money> MoneySatoshis =
		Int64.AndThen(v => Succeed(Money.Satoshis(v)));

	public static readonly Decoder<Money> MoneyBitcoins =
		String.AndThen(s => Succeed(Money.Parse(s)));

	public static readonly Decoder<FeeRate> FeeRate =
		MoneySatoshis.AndThen(m => Succeed(new FeeRate(m)));

	public static readonly Decoder<WitScript> WitScript =
		Hexadecimal.AndThen(hex => Succeed(new WitScript(hex)));

	public static readonly Decoder<OutPoint> OutPoint  =
		Hexadecimal.AndThen(bytes =>
		{
			var op = new OutPoint();
			op.FromBytes(bytes);
			return Succeed(op);
		});

	public static readonly Decoder<Script> Script =
		String.Map(s => new Script(s));

	public static readonly Decoder<TxOut> TxOut =
		Object(get => new TxOut(
			get.Required("Value", MoneySatoshis),
			get.Required("ScriptPubKey", Script)
		));

	public static readonly Decoder<Coin> Coin =
		Object(get => new Coin(
			get.Required("Outpoint", OutPoint),
			get.Required("TxOut", TxOut)
		));
}

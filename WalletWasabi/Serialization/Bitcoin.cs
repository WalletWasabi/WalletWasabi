using System.Text.Json.Nodes;
using NBitcoin;
using NBitcoin.DataEncoders;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.Helpers;
using ByteHelpers = WabiSabi.Helpers.ByteHelpers;

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
			new VersionsResponse(get.Required("backenMajordVersion", String)));

	public static Decoder<T> Catch<T>(this Decoder<T> decoder) =>
		value =>
		{
			try
			{
				return decoder(value);
			}
			catch (Exception e)
			{
				return Result<T, string>.Fail(e.Message);
			}
		};

	public static readonly Decoder<uint256> UInt256 =
		String.Map(s => new uint256(s)).Catch();

	public static readonly Decoder<Network> Network =
		String.AndThen(name =>
		{
			var network = NBitcoin.Network.GetNetwork(name);
			return network is { }
				? Succeed(network)
				: Fail<Network>($"'{name}' is not a valid network.");
		});

	public static readonly Decoder<byte[]> Hexadecimal =
		String.Map(Encoders.Hex.DecodeData).Catch();

	public static readonly Decoder<Money> MoneySatoshis =
		Int64.Map(Money.Satoshis);

	public static readonly Decoder<Money> MoneyBitcoins =
		String.Map(Money.Parse).Catch();

	public static readonly Decoder<FeeRate> FeeRate =
		MoneySatoshis.Map(m => new FeeRate(m)).Catch();

	public static readonly Decoder<WitScript> WitScript =
		Hexadecimal.Map(hex => new WitScript(hex)).Catch();

	public static readonly Decoder<OutPoint> OutPoint  =
		Hexadecimal.Map(bytes =>
		{
			var op = new OutPoint();
			op.FromBytes(bytes);
			return op;
		}).Catch();

	public static readonly Decoder<Script> Script =
		String.Map(s => new Script(s)).Catch();

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

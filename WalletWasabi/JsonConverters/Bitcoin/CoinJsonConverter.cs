using NBitcoin;
using Newtonsoft.Json;

namespace WalletWasabi.JsonConverters.Bitcoin;

public class CoinJsonConverter : JsonConverter<Coin>
{
	/// <inheritdoc />
	public override Coin? ReadJson(JsonReader reader, Type objectType, Coin? existingValue, bool hasExistingValue, JsonSerializer serializer)
	{
		var coin = serializer.Deserialize<SerializableCoin>(reader)
			?? throw new JsonSerializationException("Coin could not be deserialized.");
		return new Coin(coin.Outpoint, coin.TxOut);
	}

	/// <inheritdoc />
	public override void WriteJson(JsonWriter writer, Coin? coin, JsonSerializer serializer)
	{
		if (coin is null)
		{
			throw new ArgumentNullException(nameof(coin));
		}

		var newCoin = new SerializableCoin(coin.Outpoint, coin.TxOut);
		serializer.Serialize(writer, newCoin);
	}

	private class SerializableCoin
	{
		[JsonConstructor]
		public SerializableCoin(OutPoint outpoint, TxOut txOut)
		{
			Outpoint = outpoint;
			TxOut = txOut;
		}

		public OutPoint Outpoint { get; }
		public TxOut TxOut { get; }
	}
}

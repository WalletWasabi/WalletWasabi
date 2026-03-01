using NBitcoin;
using Newtonsoft.Json;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Wallets.SilentPayment;

namespace WalletWasabi.Rpc.JsonConverters;

public class DestinationJsonConverter(Network network): JsonConverter<Destination>
{
	public override void WriteJson(JsonWriter writer, Destination? value, JsonSerializer serializer)
	{
		var wip = value switch
		{
			Destination.Loudly l => l.ScriptPubKey.GetDestinationAddress(network)?.ToString(),
			Destination.Silent s => s.Address.ToWip(network),
			_ => throw new ArgumentException($"Unknown destination type: {value?.GetType().Name}")
		};
		writer.WriteValue(wip);
	}

	public override Destination? ReadJson(JsonReader reader, Type objectType, Destination? existingValue, bool hasExistingValue,
		JsonSerializer serializer)
	{
		var str = reader.Value as string;
		ArgumentException.ThrowIfNullOrWhiteSpace(str);
		try
		{
			var address = BitcoinAddress.Create(str, network);
			return new Destination.Loudly(address.ScriptPubKey);
		}
		catch (Exception)
		{
			var address = SilentPaymentAddress.Parse(str, network);
			return new Destination.Silent(address);
		}
	}
}

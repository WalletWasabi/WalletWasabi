using NBitcoin;
using Newtonsoft.Json;
using WalletWasabi.Helpers;

namespace WalletWasabi.JsonConverters;

public class ExtPubKeyJsonConverter : JsonConverter<ExtPubKey>
{
	/// <inheritdoc />
	public override ExtPubKey? ReadJson(JsonReader reader, Type objectType, ExtPubKey? existingValue, bool hasExistingValue, JsonSerializer serializer)
	{
		string? s = (string?)reader.Value;

		if (s is not null)
		{
			ExtPubKey epk = NBitcoinHelpers.BetterParseExtPubKey(s);
			return epk;
		}

		return null;
	}

	/// <inheritdoc />
	public override void WriteJson(JsonWriter writer, ExtPubKey? value, JsonSerializer serializer)
	{
		string? xpub = value?.GetWif(Network.Main).ToWif()
			?? throw new ArgumentNullException(nameof(value));
		writer.WriteValue(xpub);
	}
}

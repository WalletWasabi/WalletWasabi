using NBitcoin;
using System.Text.Json;
using System.Text.Json.Serialization;
using WalletWasabi.Helpers;

namespace WalletWasabi.JsonConverters;

public class ExtPubKeyJsonConverterNg : JsonConverter<ExtPubKey>
{
	/// <inheritdoc />
	public override ExtPubKey? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType != JsonTokenType.String)
		{
			throw new JsonException("Expected a JSON string value.");
		}

		string? extPubKeyString = reader.GetString();

		if (extPubKeyString is not null)
		{
			ExtPubKey epk = NBitcoinHelpers.BetterParseExtPubKey(extPubKeyString);
			return epk;
		}

		return null;
	}

	/// <inheritdoc />
	public override void Write(Utf8JsonWriter writer, ExtPubKey? value, JsonSerializerOptions options)
	{
		string xpub = value?.GetWif(Network.Main).ToWif()
			?? throw new ArgumentNullException(nameof(value));
		writer.WriteStringValue(xpub);
	}
}

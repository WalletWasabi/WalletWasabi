using NBitcoin;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WalletWasabi.JsonConverters;

/// <summary>
/// Converter used to convert <see cref="Network"/> to and from JSON.
/// </summary>
/// <seealso cref="JsonConverter" />
public class NetworkJsonConverterNg : JsonConverter<Network>
{
	/// <inheritdoc/>
	public override bool HandleNull => true;

	/// <inheritdoc/>
	public override Network? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		string? networkString;

		if (reader.TokenType == JsonTokenType.Null)
		{
			throw new ArgumentNullException(nameof(networkString));
		}
		else if (reader.TokenType != JsonTokenType.String)
		{
			throw new JsonException("Expected a JSON string.");
		}

		networkString = reader.GetString()!.Trim();

		if ("regression".Equals(networkString, StringComparison.OrdinalIgnoreCase))
		{
			return Network.RegTest;
		}

		return Network.GetNetwork(networkString);
	}

	/// <inheritdoc/>
	public override void Write(Utf8JsonWriter writer, Network? value, JsonSerializerOptions options)
	{
		if (value is null)
		{
			writer.WriteNullValue();
		}
		else
		{
			writer.WriteStringValue(value.ToString());
		}
	}
}

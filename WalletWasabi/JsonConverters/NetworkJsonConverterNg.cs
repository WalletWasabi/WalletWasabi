using NBitcoin;
using System.Globalization;
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
	public override Network? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType != JsonTokenType.String)
		{
			throw new JsonException("Expected a JSON string.");
		}

		string? networkString = reader.GetString();

		if (networkString is null)
		{
			throw new ArgumentNullException(nameof(networkString));
		}

		if ("regression".Equals(networkString, StringComparison.OrdinalIgnoreCase))
		{
			return Network.RegTest;
		}

		return Network.GetNetwork(networkString);
	}

	/// <inheritdoc/>
	public override void Write(Utf8JsonWriter writer, Network? value, JsonSerializerOptions options)
	{
		string network = value?.ToString()
			?? throw new ArgumentNullException(nameof(value));

		writer.WriteStringValue(network);
	}
}

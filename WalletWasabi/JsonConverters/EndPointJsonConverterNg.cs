using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using WalletWasabi.Userfacing;

namespace WalletWasabi.JsonConverters;

public class EndPointJsonConverterNg : JsonConverter<EndPoint>
{
	public override bool HandleNull => true;

	public override EndPoint? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType != JsonTokenType.String && reader.TokenType != JsonTokenType.Null)
		{
			throw new JsonException("Expected a JSON string value.");
		}

		if (reader.GetString() is not { } endPointString)
		{
			throw new FormatException("endpoint is null");
		};

		return EndPointParser
			.Parse(endPointString)
			.Match(
				success: endPoint => endPoint,
				failure: error => throw new FormatException(error));
	}

	/// <inheritdoc />
	public override void Write(Utf8JsonWriter writer, EndPoint? value, JsonSerializerOptions options)
	{
		if (value is null)
		{
			writer.WriteNullValue();
		}
		else
		{
			var endPointString = value.ToString();
			writer.WriteStringValue(endPointString);
		}
	}
}

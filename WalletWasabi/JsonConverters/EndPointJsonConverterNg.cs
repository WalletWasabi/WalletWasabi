using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using WalletWasabi.Userfacing;

namespace WalletWasabi.JsonConverters;

public class EndPointJsonConverterNg : JsonConverter<EndPoint>
{
	public EndPointJsonConverterNg(int defaultPort)
	{
		if (defaultPort == 0)
		{
			throw new ArgumentException("Default port not specified.", nameof(defaultPort));
		}

		DefaultPort = defaultPort;
	}

	/// <inheritdoc/>
	public override bool HandleNull => true;

	private int DefaultPort { get; }

	/// <inheritdoc />
	public override EndPoint? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		string? endPointString;

		if (reader.TokenType != JsonTokenType.String && reader.TokenType != JsonTokenType.Null)
		{
			throw new JsonException("Expected a JSON string value.");
		}

		endPointString = reader.GetString();

		if (EndPointParser.TryParse(endPointString, DefaultPort, out EndPoint? endPoint))
		{
			return endPoint;
		}

		throw new FormatException($"{nameof(endPointString)} is in the wrong format: {endPointString}.");
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
			string endPointString = value.ToString(DefaultPort);
			writer.WriteStringValue(endPointString);
		}
	}
}

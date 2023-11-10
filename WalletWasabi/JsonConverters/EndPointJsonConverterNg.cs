using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using WalletWasabi.Userfacing;

namespace WalletWasabi.JsonConverters;

public class EndPointJsonConverterNg : JsonConverter<EndPoint>
{
	public EndPointJsonConverterNg(int defaultPort)
	{
		DefaultPort = defaultPort;
	}

	private int DefaultPort { get; }

	/// <inheritdoc />
	public override EndPoint? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType != JsonTokenType.String)
		{
			throw new JsonException("Expected a JSON string value.");
		}

		string? endPointString = reader.GetString();

		if (EndPointParser.TryParse(endPointString, DefaultPort, out EndPoint? endPoint))
		{
			return endPoint;
		}
		else
		{
			throw new FormatException($"{nameof(endPointString)} is in the wrong format: {endPointString}.");
		}
	}

	/// <inheritdoc />
	public override void Write(Utf8JsonWriter writer, EndPoint? value, JsonSerializerOptions options)
	{
		if (value is null)
		{
			throw new NotSupportedException($"{nameof(EndPointJsonConverter)} can only convert {nameof(EndPoint)}.");
		}
		else
		{
			string endPointString = value.ToString(DefaultPort);
			writer.WriteStringValue(endPointString);
		}
	}
}

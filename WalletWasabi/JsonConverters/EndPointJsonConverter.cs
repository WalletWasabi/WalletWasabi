using System;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WalletWasabi.JsonConverters
{
	public class EndPointJsonConverter : JsonConverter<EndPoint>
	{
		public int DefaultPort { get; }

		private EndPointJsonConverter()
		{
		}

		public EndPointJsonConverter(int defaultPort)
		{
			if (defaultPort == 0)
			{
				throw new ArgumentException("Default port not specified.", nameof(defaultPort));
			}

			DefaultPort = defaultPort;
		}

		public override EndPoint Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
			=> reader.CreateObject(value =>
			{
				if (EndPointParser.TryParse(value, DefaultPort, out EndPoint endPoint))
				{
					return endPoint;
				}

				throw new FormatException($"{nameof(value)} is in the wrong format: {value}.");
			});

		public override void Write(Utf8JsonWriter writer, EndPoint value, JsonSerializerOptions options)
			=> writer.WriteStringValue(value.ToString(DefaultPort));
	}
}

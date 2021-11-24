using Newtonsoft.Json;
using System;
using System.Net;
using WalletWasabi.Userfacing;

namespace WalletWasabi.JsonConverters
{
	public class EndPointJsonConverter : JsonConverter<EndPoint>
	{
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

		public int DefaultPort { get; }

		/// <inheritdoc />
		public override EndPoint? ReadJson(JsonReader reader, Type objectType, EndPoint? existingValue, bool hasExistingValue, JsonSerializer serializer)
		{
			var endPointString = reader.Value as string;
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
		public override void WriteJson(JsonWriter writer, EndPoint? value, JsonSerializer serializer)
		{
			if (value is EndPoint endPoint)
			{
				var endPointString = endPoint.ToString(DefaultPort);
				writer.WriteValue(endPointString);
			}
			else
			{
				throw new NotSupportedException($"{nameof(EndPointJsonConverter)} can only convert {nameof(EndPoint)}.");
			}
		}
	}
}

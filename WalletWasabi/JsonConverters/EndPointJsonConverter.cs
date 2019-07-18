using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using WalletWasabi.Helpers;

namespace WalletWasabi.JsonConverters
{
	public class EndPointJsonConverter : JsonConverter
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

		/// <inheritdoc />
		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(EndPoint);
		}

		/// <inheritdoc />
		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			string endPointString = reader.Value as string;
			if (EndPointParser.TryParse(endPointString, DefaultPort, out EndPoint endPoint))
			{
				return endPoint;
			}
			else
			{
				throw new FormatException($"{nameof(endPointString)} is in the wrong format: {endPointString}.");
			}
		}

		/// <inheritdoc />
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
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

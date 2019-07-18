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
			string endPointString = Guard.Correct(reader.Value as string);
			endPointString = endPointString.TrimEnd('/');
			endPointString = endPointString.TrimEnd(':');

			var lastIndex = endPointString.LastIndexOf(':');

			string portString = null;
			if (lastIndex != -1)
			{
				portString = endPointString.Substring(endPointString.LastIndexOf(':') + 1);
			}

			if (portString is null || !int.TryParse(portString, out int port))
			{
				port = DefaultPort;
			}

			string host = endPointString.TrimEnd(portString, StringComparison.OrdinalIgnoreCase).TrimEnd(':');

			EndPoint endPoint;
			if (IPAddress.TryParse(host, out IPAddress addr))
			{
				endPoint = new IPEndPoint(addr, port);
			}
			else
			{
				endPoint = new DnsEndPoint(host, port);
			}

			return endPoint;
		}

		/// <inheritdoc />
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			string host;
			int port;
			if (value is DnsEndPoint dnsEndPoint)
			{
				host = dnsEndPoint.Host;
				port = dnsEndPoint.Port;
			}
			else if (value is IPEndPoint ipEndPoint)
			{
				host = ipEndPoint.Address.ToString();
				port = ipEndPoint.Port;
			}
			else
			{
				throw new FormatException($"Invalid endpoint: {value.ToString()}");
			}

			if (port == 0)
			{
				port = DefaultPort;
			}

			var endPointString = $"{host}:{port}";

			writer.WriteValue(endPointString);
		}
	}
}

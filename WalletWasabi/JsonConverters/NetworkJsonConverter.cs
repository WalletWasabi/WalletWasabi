using NBitcoin;

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WalletWasabi.JsonConverters
{
	public class NetworkJsonConverter : JsonConverter<Network>
	{
		public override Network Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
			=> reader.CreateObject(value =>
			{
				if ("regression".Equals(value, StringComparison.OrdinalIgnoreCase))
				{
					return Network.RegTest;
				}

				return Network.GetNetwork(value);
			});

		public override void Write(Utf8JsonWriter writer, Network value, JsonSerializerOptions options)
			=> writer.WriteStringValue(value.ToString());
	}
}

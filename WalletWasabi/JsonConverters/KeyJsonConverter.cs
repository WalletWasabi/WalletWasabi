using NBitcoin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.JsonConverters
{
	public class KeyJsonConverter : JsonConverter
	{
		/// <inheritdoc />
		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(Key);
		}

		/// <inheritdoc />
		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			var serializedKey = ((string)reader.Value).Trim();
			foreach (var network in Network.GetNetworks())
			{
				try
				{
					return Key.Parse(serializedKey, network);
				}
				catch (FormatException)
				{
				}
			}
			return null;
		}

		/// <inheritdoc />
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			var key = (Key)value;
			writer.WriteValue(key.GetWif(Network.Main));
		}
	}
}

using NBitcoin;
using Newtonsoft.Json;
using System;

namespace WalletWasabi.JsonConverters
{
	public class ExtPubKeyJsonConverter : JsonConverter
	{
		/// <inheritdoc />
		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(ExtPubKey);
		}

		/// <inheritdoc />
		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			var s = (string)reader.Value;
			ExtPubKey epk;

			try
			{
				epk = ExtPubKey.Parse(s);
			}
			catch
			{
				// Try hex, Old wallet format was like this.
				epk = new ExtPubKey(ByteHelpers.FromHex(s));
			}
			return epk;
		}

		/// <inheritdoc />
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			var epk = (ExtPubKey)value;

			var xpub = epk.GetWif(Network.Main).ToWif();
			writer.WriteValue(xpub);
		}
	}
}

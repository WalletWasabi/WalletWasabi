using NBitcoin.RPC;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace HiddenWallet.Converters
{
	public class EstimateSmartFeeModeConverter : JsonConverter
	{
		/// <inheritdoc />
		public override bool CanConvert(Type objectType)
		{

			return objectType == typeof(EstimateSmartFeeMode) || objectType == typeof(EstimateSmartFeeMode?);
		}

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			// check additional strings those are not checked by GetNetwork
			string value = ((string)reader.Value).Trim();
			if(string.IsNullOrWhiteSpace(value) || value.Equals("null", StringComparison.OrdinalIgnoreCase))
			{
				return null;
			}
			if(EstimateSmartFeeMode.Conservative.ToString().Equals(value, StringComparison.OrdinalIgnoreCase))
			{
				return EstimateSmartFeeMode.Conservative;
			}
			if (EstimateSmartFeeMode.Economical.ToString().Equals(value, StringComparison.OrdinalIgnoreCase))
			{
				return EstimateSmartFeeMode.Economical;
			}

			throw new ArgumentException(value);
		}

		/// <inheritdoc />
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			writer.WriteValue(((EstimateSmartFeeMode?)value).ToString());
		}
	}
}

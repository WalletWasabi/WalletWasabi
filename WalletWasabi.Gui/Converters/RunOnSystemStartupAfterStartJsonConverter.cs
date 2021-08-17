using System;
using System.Threading;
using Newtonsoft.Json;
using WalletWasabi.Helpers;

namespace WalletWasabi.Gui.Converters
{
	public class RunOnSystemStartupAfterStartJsonConverter : JsonConverter
	{
		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(bool);
		}

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			using var cts = new CancellationTokenSource(10000);
			var value = StartupChecker.GetCurrentValueAsync(cts.Token).Result;

			return value;
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			writer.WriteValue(value);
		}
	}
}

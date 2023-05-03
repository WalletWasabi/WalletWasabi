using NBitcoin;
using Newtonsoft.Json;
using WalletWasabi.Extensions;

namespace WalletWasabi.JsonConverters;

internal class OutPointDateTimeJsonConverter : JsonConverter<(OutPoint OutPoint, DateTimeOffset? BannedUntil)>
{
	public override (OutPoint OutPoint, DateTimeOffset? BannedUntil) ReadJson(JsonReader reader, Type objectType, (OutPoint OutPoint, DateTimeOffset? BannedUntil) existingValue, bool hasExistingValue, JsonSerializer serializer)
	{
		if (reader.Value is string serialized)
		{
			var op = new OutPoint();
			var date = DateTimeOffset.MinValue;
			var splitValueArray = serialized.Split(":");
			op.FromHex(splitValueArray[0]);
			date = DateTimeOffset.Parse(splitValueArray[1]);
			return (op, date);
		}
		throw new ArgumentException($"No valid serialized {nameof(OutPoint)} passed.");
	}

	public override void WriteJson(JsonWriter writer, (OutPoint OutPoint, DateTimeOffset? BannedUntil) value, JsonSerializer serializer)
	{
		string opHex = value.OutPoint?.ToHex() ?? throw new ArgumentNullException(nameof(value.OutPoint));
		string date = value.BannedUntil?.ToString() ?? throw new ArgumentNullException(nameof(value.BannedUntil));
		writer.WriteValue($"{opHex}:{date}");
	}
}

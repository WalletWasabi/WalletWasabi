using Newtonsoft.Json;
using WalletWasabi.Models;

namespace WalletWasabi.JsonConverters;

public class WalletHeightJsonConverter : HeightJsonConverter
{
	/// <inheritdoc />
	public override void WriteJson(JsonWriter writer, Height value, JsonSerializer serializer)
	{
		var safeHeight = Math.Max(0, value.Value - 101 /* maturity */);

		writer.WriteValue(safeHeight.ToString());
	}
}

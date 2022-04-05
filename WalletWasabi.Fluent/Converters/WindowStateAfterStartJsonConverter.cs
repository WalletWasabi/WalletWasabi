using Avalonia.Controls;
using Newtonsoft.Json;

namespace WalletWasabi.Fluent.Converters;

public class WindowStateAfterStartJsonConverter : JsonConverter<string>
{
	/// <inheritdoc />
	public override string? ReadJson(JsonReader reader, Type objectType, string? existingValue, bool hasExistingValue, JsonSerializer serializer)
	{
		try
		{
			var value = reader.Value as string;

			// If minimized, then go with Maximized, because at start it shouldn't run with minimized.
			if (Enum.TryParse(value, out WindowState ws) && ws != WindowState.Minimized)
			{
				return ws.ToString();
			}
		}
		catch
		{
			// ignored
		}

		return WindowState.Maximized.ToString();
	}

	/// <inheritdoc />
	public override void WriteJson(JsonWriter writer, string? value, JsonSerializer serializer)
	{
		writer.WriteValue(value);
	}
}

using Newtonsoft.Json;

namespace WalletWasabi.JsonConverters
{
	/// <summary>
	/// Converter used to convert <see cref="byte"/> arrays to and from JSON.
	/// </summary>
	/// <seealso cref="JsonConverter" />
	public class ByteArrayJsonConverter : JsonConverter<byte[]>
	{
		/// <inheritdoc />
		public override byte[]? ReadJson(JsonReader reader, Type objectType, byte[]? existingValue, bool hasExistingValue, JsonSerializer serializer)
		{
			var value = reader.Value as string;

			if (string.IsNullOrWhiteSpace(value))
			{
				return null;
			}

			return Convert.FromBase64String(value);
		}

		/// <inheritdoc />
		public override void WriteJson(JsonWriter writer, byte[]? value, JsonSerializer serializer)
		{
			if (value is null)
			{
				throw new ArgumentNullException(nameof(value));
			}
			else
			{
				writer.WriteValue(Convert.ToBase64String(value));
			}
		}
	}
}

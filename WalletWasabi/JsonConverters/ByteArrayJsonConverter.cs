﻿using Newtonsoft.Json;
using System;

namespace WalletWasabi.JsonConverters
{
	/// <summary>
	/// Converter used to convert <see cref="byte"/> arrays to and from JSON.
	/// </summary>
	/// <seealso cref="Newtonsoft.Json.JsonConverter" />
	public class ByteArrayJsonConverter : JsonConverter
	{
		/// <inheritdoc />
		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(byte[]);
		}

		/// <inheritdoc />
		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			if (reader.Value is null)
			{
				return null;
			}

			return Convert.FromBase64String((string)reader.Value);
		}

		/// <inheritdoc />
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			writer.WriteValue(Convert.ToBase64String((byte[])value));
		}
	}
}

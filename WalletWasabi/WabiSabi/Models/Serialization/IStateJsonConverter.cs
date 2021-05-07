using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;

namespace WalletWasabi.WabiSabi.Models.Serialization
{
	public class IStateJsonConverter : JsonConverter
	{
		// This converter is a bit unusual because we need to add a new property to the
		// serialized json string but the converter is called recursively and fails with
		// an "Self referencing loop" exception.
		// The workaround is detect it and prevent reentering by setting CanRead and
		// CanWrite to false immediately after entering.
		// see: https://github.com/JamesNK/Newtonsoft.Json/issues/386
		[ThreadStatic]
		private static bool _isReading;

		[ThreadStatic]
		private static bool _isWriting;

		public override bool CanWrite
		{
			get
			{
				if (!_isWriting)
				{
					return true;
				}
				_isWriting = false;

				return false;
			}
		}

		public override bool CanRead
		{
			get
			{
				if (!_isReading)
				{
					return true;
				}
				_isReading = false;

				return false;
			}
		}

		public override bool CanConvert(Type objectType)
		{
			return typeof(IState).IsAssignableFrom(objectType);
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			try
			{
				_isWriting = true;

				var stateTypeName = value switch
				{
					ConstructionState => "Constructing",
					SigningState => "Signing",
					_ => throw new InvalidOperationException("")
				};
				var jObject = (JObject)JToken.FromObject(value, serializer);
				jObject.AddFirst(new JProperty("State", stateTypeName));
				jObject.WriteTo(writer);
			}
			finally
			{
				_isWriting = false;
			}
		}

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			try
			{
				_isReading = true;

				var jsonObject = JObject.Load(reader);
				var stateType = jsonObject.Value<string>("State") switch
				{
					"Constructing" => typeof(ConstructionState),
					"Signing" => typeof(SigningState),
					_ => throw new InvalidOperationException("")
				};
				return serializer.Deserialize(jsonObject.CreateReader(), stateType);
			}
			finally
			{
				_isReading = false;
			}
		}
	}
}

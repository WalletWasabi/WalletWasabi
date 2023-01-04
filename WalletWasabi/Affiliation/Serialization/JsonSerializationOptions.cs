using System.Collections.Generic;
using Newtonsoft.Json;

namespace WalletWasabi.Affiliation.Serialization;

public static class JsonSerializationOptions
{
	private static readonly List<JsonConverter> Converters = new() {
		new ByteArrayJsonConverter(),
		new FeeRateJsonConverter()
	};

	public static readonly JsonSerializerSettings Settings = new() { Converters = Converters };
}

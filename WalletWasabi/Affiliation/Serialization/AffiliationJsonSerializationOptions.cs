using System.Collections.Generic;
using Newtonsoft.Json;

namespace WalletWasabi.Affiliation.Serialization;

public static class AffiliationJsonSerializationOptions
{
	public static readonly List<JsonConverter> Converters = new()
	{
		new AffiliationByteArrayJsonConverter(),
		new AffiliationFeeRateJsonConverter()
	};

	public static readonly JsonSerializerSettings Settings = new() { Converters = Converters };
}

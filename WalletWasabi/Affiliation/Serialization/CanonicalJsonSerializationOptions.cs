using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace WalletWasabi.Affiliation.Serialization;

public static class CanonicalJsonSerializationOptions
{
	public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings() { ContractResolver = new OrderedContractResolver(), Converters = JsonSerializationOptions.Converters, Formatting = Formatting.None };

	// Taken from https://stackoverflow.com/a/11309106/3744182
	private class OrderedContractResolver : DefaultContractResolver
	{
		protected override System.Collections.Generic.IList<JsonProperty> CreateProperties(System.Type type, MemberSerialization memberSerialization)
		{
			return base.CreateProperties(type, memberSerialization).OrderBy(p => p.PropertyName).ToList();
		}
	}
}

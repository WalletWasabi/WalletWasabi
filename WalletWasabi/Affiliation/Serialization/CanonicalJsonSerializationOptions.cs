using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Collections.Generic;
using System.Linq;

namespace WalletWasabi.Affiliation.Serialization;

public static class CanonicalJsonSerializationOptions
{
	/// <summary>
	/// JSON settings that enforces JSON's objects' properties' to be serialized in alphabetical order.
	/// </summary>
	public static readonly JsonSerializerSettings Settings = new()
	{
		ContractResolver = new OrderedContractResolver(),
		Converters = AffiliationJsonSerializationOptions.Converters,

		// Intentionally enforced default value.
		Formatting = Formatting.None
	};

	/// <seealso href="https://stackoverflow.com/a/11309106/3744182"/>
	private class OrderedContractResolver : DefaultContractResolver
	{
		protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
		{
			return base.CreateProperties(type, memberSerialization).OrderBy(p => p.PropertyName, StringComparer.Ordinal).ToList();
		}
	}
}

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
		private static bool IsValidCharacter(char c)
		{
			return char.IsAscii(c) && ((char.IsLetter(c) && char.IsLower(c)) || char.IsDigit(c) || c == '_');
		}

		private static bool IsValidPropertyName(string name)
		{
			return name.All(IsValidCharacter);
		}

		protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
		{
			IEnumerable<JsonProperty> properties = base.CreateProperties(type, memberSerialization);

			foreach (JsonProperty property in properties)
			{
				if (!IsValidPropertyName(property.PropertyName))
				{
					throw new JsonSerializationException("Object property contains an invalid character.");
				}
			}

			return properties.OrderBy(p => p.PropertyName, StringComparer.Ordinal).ToList();
		}
	}
}

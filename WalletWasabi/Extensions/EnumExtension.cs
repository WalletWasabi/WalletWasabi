using System.Linq;
using WalletWasabi.Models;

namespace WalletWasabi.Extensions;

public static class EnumExtensions
{
	public static T? GetFirstAttribute<T>(this Enum value) where T : Attribute
	{
		var stringValue = value.ToString();
		var type = value.GetType();
		var memberInfo = type.GetMember(stringValue).FirstOrDefault()
			?? throw new InvalidOperationException($"Enum of type '{typeof(T).FullName}' does not contain value '{stringValue}'");

		var attributes = memberInfo.GetCustomAttributes(typeof(T), false);

		return attributes.Length != 0 ? (T)attributes[0] : null;
	}

	public static string FriendlyName(this Enum value)
	{
		var attribute = value.GetFirstAttribute<FriendlyNameAttribute>();

		return attribute is { } ? attribute.FriendlyName : value.ToString();
	}

	public static T? GetEnumValueOrDefault<T>(this int value, T defaultValue) where T : Enum
	{
		if (Enum.IsDefined(typeof(T), value))
		{
			return (T?)Enum.ToObject(typeof(T), value);
		}

		return defaultValue;
	}
}
